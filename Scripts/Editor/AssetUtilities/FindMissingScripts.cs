
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Modified from: http://wiki.unity3d.com/index.php?title=FindMissingScripts&oldid=17367
// License: Content is available under Creative Commons Attribution Share Alike https://www.apache.org/licenses/LICENSE-2.0
public class FindMissingScripts : EditorWindow
{
    string folderPath = "";
    [MenuItem("Tools/Find Missing Scripts")]
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
            GC.Collect();

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
}
