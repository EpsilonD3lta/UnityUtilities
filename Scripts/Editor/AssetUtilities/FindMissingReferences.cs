using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Creator and Copyright: https://github.com/liortal53/MissingReferencesUnity/blob/master/
// License: Apache License version 2.0: https://github.com/liortal53/MissingReferencesUnity/blob/master/LICENSE

/// <summary> A helper editor script for finding missing references to objects </summary>
public class FindMissingReferences
{
    private const string MenuRoot = "Tools/Find Missing References/";

    /// <summary> Finds all missing references to objects in the currently loaded scene </summary>
    [MenuItem(MenuRoot + "Search in scene", false, 0)]
    public static void FindMissingRefsInCurrentScene()
    {
        var sceneObjects = GetSceneObjects();
        FindMissingRefs(EditorSceneManager.GetActiveScene().path, sceneObjects);
        Debug.Log($"[FindMissingReferences] finished scene {EditorSceneManager.GetActiveScene().path}");
    }

    /// <summary>
    /// Finds all missing references to objects in all enabled scenes in the project.
    /// This works by loading the scenes one by one and checking for missing object references.
    /// </summary>
    [MenuItem(MenuRoot + "Search in all scenes enabled in build", false, 1)]
    public static void FindMissingRefsInAllScenesEnabledInBuild()
    {
        var currentScene = EditorSceneManager.GetActiveScene().path;
        foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            EditorSceneManager.OpenScene(scene.path);
            FindMissingRefsInCurrentScene();
        }
        EditorSceneManager.OpenScene(currentScene);
    }

    /// <summary>
    /// Finds all missing references to objects in all enabled scenes in the project.
    /// This works by loading the scenes one by one and checking for missing object references.
    /// </summary>
    [MenuItem(MenuRoot + "Search in all scenes", false, 2)]
    public static void FindMissingRefsInAllScenes()
    {
        var currentScene = EditorSceneManager.GetActiveScene().path;
        var scenePaths = AssetDatabase.FindAssets("t:scene", new string[] { "Assets" })
            .Select(x => AssetDatabase.GUIDToAssetPath(x));
        foreach (var path in scenePaths)
        {
            EditorSceneManager.OpenScene(path);
            FindMissingRefsInCurrentScene();
        }
        EditorSceneManager.OpenScene(currentScene);
    }

    /// <summary> Finds all missing references to objects in assets (objects from the project window) </summary>
    [MenuItem(MenuRoot + "Search in assets", false, 3)]
    public static void FindMissingReferencesInAssets()
    {
        var allAssets = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/")).ToArray();
        var objs = allAssets.Select(a => AssetDatabase.LoadAssetAtPath(a, typeof(GameObject)) as GameObject).Where(a => a != null).ToArray();

        FindMissingRefs("Project", objs);
        Debug.Log("[FindMissingReferences] Finished all assets");
    }

    private static void FindMissingRefs(string context, GameObject[] gameObjects)
    {
        if (gameObjects == null) return;

        foreach (var go in gameObjects)
        {
            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                // Missing components will be null, we can't find their type, etc.
                if (!component)
                {
                    Debug.LogError($"Missing Component in GameObject: {GetFullPath(go)}", go);
                    continue;
                }

                SerializedObject so = new SerializedObject(component);
                var sp = so.GetIterator();

                var objRefValueMethod = typeof(SerializedProperty).GetProperty("objectReferenceStringValue",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                // Iterate over the components' properties.
                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        string objectReferenceStringValue = string.Empty;

                        if (objRefValueMethod != null)
                            objectReferenceStringValue = (string)objRefValueMethod.GetGetMethod(true).Invoke(sp, new object[] { });
#if UNITY_6000_4_OR_NEWER
                        if (sp.objectReferenceValue == null && (sp.objectReferenceEntityIdValue.IsValid() || objectReferenceStringValue.StartsWith("Missing")))
#else
                        if (sp.objectReferenceValue == null && (sp.objectReferenceInstanceIDValue != 0 || objectReferenceStringValue.StartsWith("Missing")))
#endif
                            ShowError(context, go, component.GetType().Name, ObjectNames.NicifyVariableName(sp.name));
                    }
                }
            }
        }
    }

    private static GameObject[] GetSceneObjects()
    {
        // Use this method since GameObject.FindObjectsOfType will not return disabled objects.
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go))
                   && go.hideFlags == HideFlags.None).ToArray();
    }

    private static void ShowError(string context, GameObject go, string componentName, string propertyName) =>
        Debug.LogError($"Missing Ref in: [{context}] {GetFullPath(go)}. Component: {componentName}, Property: {propertyName}", go);

    private static string GetFullPath(GameObject go)
        => go.transform.parent == null ? go.name : GetFullPath(go.transform.parent.gameObject) + "/" + go.name;
}