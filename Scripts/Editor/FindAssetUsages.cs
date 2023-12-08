using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager.UI;
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

    private List<string> assetPaths = new List<string>();
    private string selectedAssetGUID = null;
    private bool canceled = false;
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static void CreateWindow()
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

        window.selectedAssetGUID = assetGuids[0];
        window.assetPaths = FindAssetUsage(window.selectedAssetGUID, ref window.canceled);
    }

    private void OnGUI()
    {
        if (selectedAssetGUID == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }
        string searchedAssetFilename = AssetDatabase.GUIDToAssetPath(selectedAssetGUID);
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
        GUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(searchedAsset, searchedAsset.GetType(), true);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(18)))
        {
            if (!string.IsNullOrEmpty(selectedAssetGUID))
                assetPaths = FindAssetUsage(selectedAssetGUID, ref canceled);
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        GUILayout.Label("Found Asset References", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string assetFilename in assetPaths)
        {
            string fileName = assetFilename.EndsWith(".meta") ?
                assetFilename.Substring(0, assetFilename.Length - 5) : assetFilename;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(fileName);
            EditorGUILayout.LabelField(fileName);
            if (asset != null)
            {
                var type = asset.GetType();
                EditorGUILayout.ObjectField(asset, type, true);
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }

    public static List<string> FindAssetUsage(string assetGuid, ref bool canceled)
    {
        var usedByAssetsPaths = new List<string>(); // Empty old results

        string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        var otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        List<string> extensionsWithMeta = new List<string>(extensions);
        extensionsWithMeta.AddRange(extensions.Select(x => x + ".meta"));
        otherFilesPaths = otherFilesPaths.Where(x => Regex.IsMatch(x, $"\\.({string.Join("|", extensionsWithMeta)})$")).ToList();

        int total = otherFilesPaths.Count;
        int current = 0;

        string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid).Replace("/", "\\");
        string assetFilePath = projectPath + "\\" + assetPath;
        string assetMetaFilePath = assetFilePath + ".meta";
        Regex regex = new Regex(assetGuid, RegexOptions.Compiled);

        foreach (string otherFilePath in otherFilesPaths)
        {
            if (!File.Exists(otherFilePath))
            {
                Debug.LogWarning($"File does not exist, path too long? Path: {otherFilePath}");
                continue;
            }
            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", current / (float)total))
            {
                canceled = true;
                EditorUtility.ClearProgressBar();
                return usedByAssetsPaths;
            }

            current++;

            if (regex.IsMatch(File.ReadAllText(otherFilePath)))
            {
                if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue;
                string otherFileAssetPath = otherFilePath.Replace(projectPath + "\\", "").Replace("\\", "/");
                usedByAssetsPaths.Add(otherFileAssetPath);   // Not referencing self, add ref
            }
        }
        EditorUtility.ClearProgressBar();
        return usedByAssetsPaths;
    }
}
