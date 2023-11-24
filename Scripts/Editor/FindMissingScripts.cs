using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Modified from: http://wiki.unity3d.com/index.php?title=FindMissingScripts&oldid=17367
// License: Content is available under Creative Commons Attribution Share Alike https://www.apache.org/licenses/LICENSE-2.0
public class FindMissingScripts : EditorWindow
{
    string folderPath = "";
    [MenuItem("Tools/Find Missing Scripts/Find")]
    public static void FindMissingScriptsShow()
    {
        EditorWindow.GetWindow(typeof(FindMissingScripts));
    }

    static int missingCount = -1;
    void OnGUI()
    {
        EditorGUILayout.LabelField("Folder path from Assets. Start with /, eg.: /Prefabs");
        folderPath = EditorGUILayout.TextField(folderPath);

        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.LabelField("Missing Scripts:");
            EditorGUILayout.LabelField("" + (missingCount == -1 ? "---" : missingCount.ToString()));
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Find missing scripts"))
        {
            missingCount = 0;
            EditorUtility.DisplayProgressBar("Searching Prefabs", "", 0.0f);

            string[] files = System.IO.Directory.GetFiles(Application.dataPath + folderPath, "*.prefab", System.IO.SearchOption.AllDirectories);
            EditorUtility.DisplayCancelableProgressBar("Searching Prefabs", "Found " + files.Length + " prefabs", 0.0f);

            Scene currentScene = EditorSceneManager.GetActiveScene();
            string scenePath = currentScene.path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

            for (int i = 0; i < files.Length; i++)
            {
                string prefabPath = files[i].Replace(Application.dataPath, "Assets");
                if (EditorUtility.DisplayCancelableProgressBar("Processing Prefabs " + i + "/" + files.Length, prefabPath, (float)i / (float)files.Length))
                    break;

                GameObject go = UnityEditor.AssetDatabase.LoadAssetAtPath(prefabPath, typeof(GameObject)) as GameObject;

                if (go != null)
                {
                    FindInGO(go);
                    go = null;
                    EditorUtility.UnloadUnusedAssetsImmediate(true);
                }
            }

            EditorUtility.DisplayProgressBar("Cleanup", "Cleaning up", 1.0f);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorUtility.UnloadUnusedAssetsImmediate(true);
            System.GC.Collect();

            EditorUtility.ClearProgressBar();
        }

        if (GUILayout.Button("Find missing scripts in selected GO"))
        {
            FindInSelected();
        }
    }

    private static void FindInSelected()
    {
        GameObject[] go = Selection.gameObjects;
        missingCount = 0;
        foreach (GameObject g in go)
        {
            FindInSelectedGO(g);
        }
        Debug.Log(string.Format("Found {0} missing", missingCount));
    }

    private static void FindInSelectedGO(GameObject g)
    {
        Component[] components = g.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missingCount++;
                string s = g.name;
                Transform t = g.transform;
                while (t.parent != null)
                {
                    s = t.parent.name + "/" + s;
                    t = t.parent;
                }
                Debug.LogWarning(s + " has an empty script attached in position: " + i, g);
            }
        }
        // Now recurse through each child GO (if there are any):
        foreach (Transform childT in g.transform)
        {
            //Debug.Log("Searching " + childT.name  + " " );
            FindInSelectedGO(childT.gameObject);
        }
    }

    private static void FindInGO(GameObject go, string prefabName = "")
    {
        Component[] components = go.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                missingCount++;
                Transform t = go.transform;

                string componentPath = go.name;
                while (t.parent != null)
                {
                    componentPath = t.parent.name + "/" + componentPath;
                    t = t.parent;
                }
                Debug.LogWarning("Prefab " + prefabName + " has an empty script attached:\n" + componentPath, go);
            }
        }

        foreach (Transform child in go.transform)
        {
            FindInGO(child.gameObject, prefabName);
        }
    }

    [MenuItem("Tools/Find Missing Scripts/Remove Missing Scripts Recursively")]
    private static void FindAndRemoveMissingInSelected()
    {
        var deepSelection = EditorUtility.CollectDeepHierarchy(Selection.gameObjects);
        int compCount = 0;
        int goCount = 0;
        foreach (var o in deepSelection)
        {
            if (o is GameObject go)
            {
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (count > 0)
                {
                    // Edit: use undo record object, since undo destroy wont work with missing
                    Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    compCount += count;
                    goCount++;
                }
            }
        }
        Debug.Log($"Found and removed {compCount} missing scripts from {goCount} GameObjects");
    }

    [MenuItem("Tools/Find Missing Scripts/Remove Missing Scripts Recursively Visit Prefabs")]
    private static void FindAndRemoveMissingEverywhere()
    {
        // EditorUtility.CollectDeepHierarchy does not include inactive children
        var deeperSelection = Selection.gameObjects.SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject);
        var prefabs = new HashSet<Object>();
        int compCount = 0;
        int goCount = 0;
        foreach (var go in deeperSelection)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (count > 0)
            {
                if (PrefabUtility.IsPartOfAnyPrefab(go))
                {
                    RecursivePrefabSource(go, prefabs, ref compCount, ref goCount);
                    count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    // if count == 0 the missing scripts has been removed from prefabs
                    if (count == 0)
                        continue;
                    // if not the missing scripts must be prefab overrides on this instance
                }

                Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                compCount += count;
                goCount++;
            }
        }

        Debug.Log($"Found and removed {compCount} missing scripts from {goCount} GameObjects");
    }

    // Prefabs can both be nested or variants, so best way to clean all is to go through them all
    // rather than jumping straight to the original prefab source.
    private static void RecursivePrefabSource(GameObject instance, HashSet<Object> prefabs, ref int compCount,
        ref int goCount)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        // Only visit if source is valid, and hasn't been visited before
        if (source == null || !prefabs.Add(source))
            return;

        // Go deep before removing, to differentiate local overrides from missing in source
        RecursivePrefabSource(source, prefabs, ref compCount, ref goCount);
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(source);
        if (count > 0)
        {
            Undo.RegisterCompleteObjectUndo(source, "Remove missing scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(source);
            compCount += count;
            goCount++;
        }
    }
}
