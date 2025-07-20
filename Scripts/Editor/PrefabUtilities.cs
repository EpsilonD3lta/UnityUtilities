using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PrefabUtilities
{
    [MenuItem("GameObject/Prefab/RevertName", false, 49)]
    private static void RevertName()
    {
        Object[] selection = Selection.GetFiltered(typeof(GameObject), SelectionMode.Editable);
        foreach (var prefabInstance in selection)
        {
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstance);
            if (prefabAsset != null)
            {
                Undo.RecordObject(prefabInstance, "Revert prefab instance name");
                prefabInstance.name = prefabAsset.name;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prefabInstance);
            }
        }
    }

    [MenuItem("GameObject/Prefab/RevertName", true, 49)]
    private static bool RevertNameValidation()
    {
        Object[] selection = Selection.GetFiltered(typeof(GameObject), SelectionMode.Editable);
        bool valid = true;
        foreach (var prefabInstance in selection)
        {
            if (!PrefabUtility.IsPartOfNonAssetPrefabInstance(prefabInstance)) valid = false;
            break;
        }
        return valid;
    }

    [MenuItem("Assets/Prefab/PrefaPropagate Name/Preserve numbering", false)]
    private static void PropagateNamePreserveNumbering()
    {
        PropagateName(true);
    }

    [MenuItem("Assets/Prefab/Propagate Name/Also remove numbering", false)]
    private static void PropagateNameRemoveNumbering()
    {
        PropagateName(false);
    }

    [MenuItem("Assets/Prefab/Propagate Name/Preserve numbering", true)]
    [MenuItem("Assets/Prefab/Propagate Name/Also remove numbering", true)]
    private static bool PropagateNameValidation()
    {
        if (Selection.assetGUIDs.Length == 0) return false;
        string guid = Selection.assetGUIDs[0];
        string path = AssetDatabase.GUIDToAssetPath(guid);
        GameObject selectedPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (selectedPrefabAsset == null) return false;
        bool valid = false;
        valid = PrefabUtility.IsPartOfPrefabAsset(selectedPrefabAsset);
        if (!valid) valid = PrefabUtility.IsAnyPrefabInstanceRoot(selectedPrefabAsset);
        if (!valid) valid = PrefabUtility.IsPartOfModelPrefab(selectedPrefabAsset);
        return valid;
    }

    private static void PropagateName(bool preserveNumbering)
    {
        if (Application.isPlaying) return;
        if (Selection.assetGUIDs.Length > 0)
        {
            // Get first selected gameobject and its credentials
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            GameObject selectedPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (type != typeof(GameObject)) return;

            Regex regex = new Regex(@"(\([0-9]+\))$");

            // Check all scenes

            // Get all existing scenes in folder Assets/Scenes
            string[] guids = AssetDatabase.FindAssets("t:scene", new[] { "Assets" });
            if (guids?.Length == 0) return;
            List<string> scenePaths = guids.Select(s => AssetDatabase.GUIDToAssetPath(s))
                .Where(s => !s.StartsWith("Assets/Plugins/")).ToList();

            // Save currently open and active scene
            string currentScene = EditorSceneManager.GetActiveScene().path; //scenePaths = new List<string>() { currentScene };

            foreach (string scenePath in scenePaths)
            {
                Scene scene;
                if (scenePath != currentScene)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }
                else
                {
                    scene = EditorSceneManager.GetActiveScene();
                }
                EditorUtility.DisplayProgressBar(
                    "Renaming in scenes", scene.name, (float)scenePaths.IndexOf(scenePath) / scenePaths.Count);
                var sceneGameObjects = scene.GetRootGameObjects()
                    .SelectMany(x => x.GetComponentsInChildren<Transform>(true)).Select(x => x.gameObject);

                // Find all scene gameobjects that are instances of a prefab
                List<GameObject> scenePrefabInstances = new List<GameObject>();
                foreach (GameObject go in sceneGameObjects)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    {
                        scenePrefabInstances.Add(go);
                    }
                }

                bool sceneChanged = false;
                // Get prefab assets of prefab instances and change the name
                foreach (GameObject prefabInstance in scenePrefabInstances)
                {
                    GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstance);
                    string oldName = prefabInstance.name;
                    if (prefabAsset && prefabAsset == selectedPrefabAsset)
                    {
                        if (preserveNumbering)
                        {
                            Match match = regex.Match(prefabInstance.name); // Match is the ordinal number
                            prefabInstance.name = prefabAsset.name + (match.Success ? (" " + match.Value) : "");
                        }
                        else prefabInstance.name = prefabAsset.name;
                    }
                    if (oldName != prefabInstance.name) sceneChanged = true;
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (scenePath != currentScene)
                    {
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                EditorSceneManager.UnloadSceneAsync(scene);
            }

            // Check all prefab assets
            List<string> prefabGuids = AssetDatabase.FindAssets("t:prefab").ToList();
            foreach (string prefabGuid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                if (prefabPath == AssetDatabase.GetAssetPath(selectedPrefabAsset)) continue;

                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                EditorUtility.DisplayProgressBar(
                    "Renaming in prefabs",
                    prefabAsset.name, (float)prefabGuids.IndexOf(prefabGuid) / prefabGuids.Count);

                var prefabAssetGameObjects = prefabAsset.GetComponentsInChildren<Transform>(true).Select(x => x.gameObject);

                // Find all scene gameobjects that are instances of a prefab
                List<GameObject> nestedPrefabInstances = new List<GameObject>();
                foreach (GameObject go in prefabAssetGameObjects)
                {
                    if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    {
                        nestedPrefabInstances.Add(go);
                    }
                }

                bool prefabChanged = false;
                // Get prefab assets of prefab instances and change the name
                foreach (GameObject nestedPrefabInstance in nestedPrefabInstances)
                {
                    GameObject nestedPrefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(nestedPrefabInstance);
                    string oldName = nestedPrefabInstance.name;
                    if (nestedPrefabAsset && nestedPrefabAsset == selectedPrefabAsset)
                    {
                        if (preserveNumbering)
                        {
                            Match match = regex.Match(nestedPrefabInstance.name); // Match is the ordinal number
                            nestedPrefabInstance.name = nestedPrefabAsset.name + (match.Success ? (" " + match.Value) : "");
                        }
                        else nestedPrefabInstance.name = nestedPrefabAsset.name;
                    }
                    if (oldName != nestedPrefabInstance.name) prefabChanged = true;
                }

                if (prefabChanged)
                {
                    EditorUtility.SetDirty(prefabAsset);
                    AssetDatabase.ForceReserializeAssets(new List<string> { prefabPath });
                    AssetDatabase.SaveAssetIfDirty(prefabAsset);
                }
            }

            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary> Removes unused overrides from prefab assets and their nested prefabs (not scene instances) </summary>
    [MenuItem("Assets/Prefab/Remove Unused Prefab Overrides", false)]
    private static void RemoveUnusedPrefabOverrides()
    {
        var classType = typeof(PrefabUtility);
        var infoType = typeof(PrefabUtility).Assembly.GetType("UnityEditor.PrefabUtility+InstanceOverridesInfo");

        var getInfoMethod =
            classType.GetMethod("GetPrefabInstanceOverridesInfo", BindingFlags.Static | BindingFlags.NonPublic, null,
            new Type[] { typeof(GameObject) }, null);
        var removeOverridesMethod =
            classType.GetMethod("RemovePrefabInstanceUnusedOverrides", BindingFlags.Static | BindingFlags.NonPublic, null,
            new Type[] { infoType }, null);

        var undoGroup = Undo.GetCurrentGroup();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets" });
        foreach (var guid in guids)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var g = t.gameObject;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(g))
                {
                    var infos = getInfoMethod.Invoke(null, new[] { g });
                    if (infos != null) removeOverridesMethod.Invoke(null, new[] { infos });
                }
            }
        }
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
    }
}
