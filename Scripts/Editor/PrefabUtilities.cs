using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityEngine.UI.Extensions
{
    public static class PrefabUtilities
    {
        [MenuItem("GameObject/Prefab/RevertName", false, 49)]
        static void RevertName()
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
        static bool RevertNameValidation()
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

        [MenuItem("Assets/Propagate Name/Preserve numbering", false)]
        static void PropagateNamePreserveNumbering()
        {
            PropagateName(true);
        }

        [MenuItem("Assets/Propagate Name/Also remove numbering", false)]
        static void PropagateNameRemoveNumbering()
        {
            PropagateName(false);
        }

        [MenuItem("Assets/Propagate Name/Preserve numbering", true)]
        [MenuItem("Assets/Propagate Name/Also remove numbering", true)]
        static bool PropagateNameValidation()
        {
            if (Selection.assetGUIDs.Length == 0) return false;
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject selectedPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool valid = false;
            valid = PrefabUtility.IsPartOfPrefabAsset(selectedPrefabAsset);
            if (!valid) valid = PrefabUtility.IsAnyPrefabInstanceRoot(selectedPrefabAsset);
            if (!valid) valid = PrefabUtility.IsPartOfModelPrefab(selectedPrefabAsset);
            return valid;
        }

        static void PropagateName(bool preserveNumbering)
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
    }
}
