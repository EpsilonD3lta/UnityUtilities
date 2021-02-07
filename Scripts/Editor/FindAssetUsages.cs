using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Created by Michal Ferko:
// https://github.com/michalferko
// https://www.linkedin.com/in/michalferko/
// This script is distributed under MIT License included in Licenses folder as MIT_LICENSE_MichalFerko
public class FindAssetUsages : EditorWindow
{
    private static readonly string[] extensions =
    {
        "*.prefab",             // Prefabs
        "*.meta",               // Meta files for all assets
        "*.spriteatlas",        // Sprite atlases
        "*.asset",              // Custom assets
        "*.unity",              // Scenes
        "*.mat",                // Materials
        "*.controller",         // Animation controller
        "*.overrideController", // Animation controller
    };

    public List<string> assets = new List<string>();
    public string assetGUID = null;

    public bool canceled = false;

    [MenuItem("Tools/Asset Usage")]
    public static void OpenUsageWindow()
    {
        var window = GetWindow<FindAssetUsages>("Asset Usage");
        window.Show();
    }

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
            Debug.Log("Finding asset usages for multiple assets not working - only the first asset references will be found");
        }

        window.assets = new List<string>(); // Empty old results

        var references = new List<string>();
        var allFiles = new List<string>();

        foreach (string ext in extensions)
        {
            var filenames = Directory.EnumerateFiles(Application.dataPath, ext, SearchOption.AllDirectories);
            allFiles.AddRange(filenames);
        }

        int total = allFiles.Count;

        int current = 0;

        string guid = assetGuids[0];
        string path = AssetDatabase.GUIDToAssetPath(guid);
        foreach (string file in allFiles)
        {
            var assetFile = new FileInfo(file);
            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", current / (float)total))
            {
                window.canceled = true;
                EditorUtility.ClearProgressBar();
                return;
            }

            current++;
            string metaOriginal = file.Substring(0, file.Length - 5);
            metaOriginal = metaOriginal.Replace(Application.dataPath, "Assets");
            metaOriginal = metaOriginal.Replace("\\", "/");
            if (metaOriginal == path)       // Is this a self-reference?
                continue;                   // Skip this file, move to the next one

            foreach (string line in File.ReadAllLines(file))
            {
                if (line.Contains(guid))
                {
                    string originalFilename = file.Replace(Application.dataPath, "Assets");
                    originalFilename = originalFilename.Replace("\\", "/");
                    if (originalFilename != path)       // Is this a self-reference?
                    {
                        references.Add(originalFilename);   // Not referencing self, add ref
                        break;
                    }
                }
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
