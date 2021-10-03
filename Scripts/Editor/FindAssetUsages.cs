using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Adapted from script by Michal Ferko:
// https://github.com/michalferko
// https://www.linkedin.com/in/michalferko/
// This script is distributed under MIT License included in Licenses folder as MIT_LICENSE_MichalFerko
public class FindAssetUsages : EditorWindow
{
    // Look for usages only in these assets (and their .meta files)
    private static readonly string[] extensions =
    {
        "prefab", "asset", "unity", "preset",
        "fbx", "obj", "blend", "mesh",
        "mat", "cubemap",
        "spriteatlas",
        "controller", "overrideController",
        "flare",
        "mask",
        "shader", "compute", "shadergraph", "shadersubgraph",
        "terrainlayer",
        "brush",
        "cs.meta", "asmdef", "asmref",
    };

    public List<string> assets = new List<string>();
    public string assetGUID = null;

    public bool canceled = false;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static void FindAssetUsage()
    {
        var window = GetWindow<FindAssetUsages>();
        window.Show();
        window.canceled = false;

        if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", 0))
        {
            window.canceled = true;
            EditorUtility.ClearProgressBar();
            return;
        }

        string[] assetGuids = Selection.assetGUIDs;
        if (assetGuids.Length == 0)
        {
            Debug.Log("Cannot find asset usages when no assets are selected.");
            EditorUtility.ClearProgressBar();
            return;
        }
        if (assetGuids.Length > 1)
        {
            Debug.Log("Finding asset usages no multiple assets support - only the first asset references will be found");
        }

        window.assets = new List<string>(); // Empty old results

        var references = new List<string>();

        string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        var otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        List<string> extensionsWithMeta = new List<string>(extensions);
        extensionsWithMeta.AddRange(extensions.Select(x => x + ".meta"));
        otherFilesPaths = otherFilesPaths.Where(x => Regex.IsMatch(x, $"\\.({string.Join("|", extensionsWithMeta)})$")).ToList();

        int total = otherFilesPaths.Count;
        int current = 0;

        string guid = assetGuids[0];
        string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace("/", "\\");
        string assetFilePath = projectPath + "\\" + assetPath;
        string assetMetaFilePath = assetFilePath + ".meta";
        Regex regex = new Regex(guid, RegexOptions.Compiled);

        foreach (string otherFilePath in otherFilesPaths)
        {
            if (!File.Exists(otherFilePath))
            {
                Debug.LogWarning($"File does not exist, path too long? Path: {otherFilePath}");
                continue;
            }
            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", current / (float)total))
            {
                window.canceled = true;
                EditorUtility.ClearProgressBar();
                return;
            }

            current++;

            if (regex.IsMatch(File.ReadAllText(otherFilePath)))
            {
                if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue;
                string otherFileAssetPath = otherFilePath.Replace(projectPath + "\\", "").Replace("\\", "/");
                references.Add(otherFileAssetPath);   // Not referencing self, add ref
            }
        }

        EditorUtility.ClearProgressBar();

        window.assets = references;
        window.assetGUID = guid;
    }

    private Vector2 scroll = Vector2.zero;

    private void OnGUI()
    {
        if (assetGUID == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }
        string searchedAssetFilename = AssetDatabase.GUIDToAssetPath(assetGUID);
        if (searchedAssetFilename == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }

        var searchedAsset = AssetDatabase.LoadAssetAtPath<Object>(searchedAssetFilename);
        if (searchedAsset == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }

        EditorGUILayout.LabelField(searchedAssetFilename);
        EditorGUILayout.ObjectField(searchedAsset, searchedAsset.GetType(), true);
        EditorGUILayout.Space();
        GUILayout.Label("Found Asset References", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string assetFilename in assets)
        {
            string fn = assetFilename.EndsWith(".meta") ? assetFilename.Substring(0, assetFilename.Length - 5) : assetFilename;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(fn);
            EditorGUILayout.LabelField(fn);
            if (asset != null)
            {
                var type = asset.GetType();
                EditorGUILayout.ObjectField(asset, type, true);
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }
}
