using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class FindUnusedAssets : EditorWindow
{
    private static readonly string[] extensions =
    {
       "*.prefab",
       "*.meta",
       "*.spriteatlas",
       "*.asset",
       "*.unity",
       "*.mat",
       "*.controller",
       "*.overrideController",
       "*.flare",
       "*.mask",
       "*.preset",
       "*.shadergraph",
       "*.shadersubgraph",
       "*.terrainlayer",
       "*.brush"
    };

    public List<string> unusedAssets = new List<string>();

    static bool canceled = false;
    static float progress = 0;
    static string subfolder = "";

    [MenuItem("Tools/Find Unused Assets")]
    public static void OpenUsageWindow()
    {
        FindUnusedAssets window = GetWindow<FindUnusedAssets>();
        window.Show();
    }

    /// <summary>
    /// Tries to find any asset usage
    /// If one is found, returns true
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    static bool FindAnyAssetUsage(string guid)
    {
        List<string> references = new List<string>();
        List<string> allFiles = new List<string>();

        foreach (var ext in extensions)
        {
            var filenames = Directory.EnumerateFiles(Application.dataPath, ext, SearchOption.AllDirectories);
            allFiles.AddRange(filenames);
        }

        var path = AssetDatabase.GUIDToAssetPath(guid);
        foreach (var file in allFiles)
        {
            FileInfo assetFile = new FileInfo(file);

            var metaOriginal = file.Substring(0, file.Length - 5);
            metaOriginal = metaOriginal.Replace(Application.dataPath, "Assets");
            metaOriginal = metaOriginal.Replace("\\", "/");
            if (metaOriginal == path) continue; // Skip own .meta file

            var originalFilename = file.Replace(Application.dataPath, "Assets");
            originalFilename = originalFilename.Replace("\\", "/");
            if (originalFilename == path) continue; // Skip the file itself

            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Looking for asset references", progress))
            {
                canceled = true;
                return false;
            }

            foreach (var line in File.ReadAllLines(file))
            {
                if (line.Contains(guid))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void FindAssets()
    {
        FindUnusedAssets window = GetWindow<FindUnusedAssets>();
        window.Show();
        canceled = false;

        if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", 0))
        {
            canceled = true;
            EditorUtility.ClearProgressBar();
            return;
        }

        var assetPaths = AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/" + subfolder)).ToArray();

        window.unusedAssets = new List<string>(); // Empty old results

        int total = assetPaths.Length;
        int current = 0;
        foreach (var assetPath in assetPaths)
        {
            current++;
            progress = current / (float)total;

            if (AssetDatabase.IsValidFolder(assetPath)) continue;
            if (assetPath.Contains("/Resources/") || assetPath.Contains("/Editor/") || assetPath.Contains("/Plugins/")) continue;

            if (canceled || EditorUtility.DisplayCancelableProgressBar("Searching...", "Looking for asset references", progress))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!FindAnyAssetUsage(guid))
            {
                window.unusedAssets.Add(assetPath);
                File.AppendAllText("Usages.txt", assetPath + " Type[" + AssetDatabase.GetMainAssetTypeAtPath(assetPath) + "]\n");
            }
        }

        EditorUtility.ClearProgressBar();
    }

    private Vector2 scroll = Vector2.zero;

    private void OnGUI()
    {
        GUILayout.Label("Subfolder:");
        subfolder = GUILayout.TextField(subfolder);
        if (GUILayout.Button("Find Unused Assets"))
        {
            FindAssets();
        }

        GUILayout.Label("These assets are not referenced anywhere:", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var assetFilename in unusedAssets)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetFilename);
            EditorGUILayout.LabelField(assetFilename);
            EditorGUILayout.ObjectField(asset, asset.GetType(), true);
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }
}
