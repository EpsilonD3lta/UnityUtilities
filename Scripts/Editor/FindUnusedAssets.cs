using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class FindUnusedAssets : EditorWindow
{
    // Don't investigate if these assets are unused
    private static readonly string[] excludedExtensions =
    {
       "unity", "preset", "spriteatlas",
       "dll", "m", "java", "aar", "jar", "mm", "h", "plist",
       "xml", "json", "txt", "md", "pdf",
       "asmdef", "asmref",
    };

    // Look for usages only in these assets (and their .meta files)
    private static readonly string[] extensionsToSearchIn =
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

    private static bool canceled = false;
    private static float progress = 0;
    private static string subfolder = "";

    private static string projectPath;
    private static List<string> otherFilesPaths = new List<string>();

    public List<string> unusedAssets = new List<string>();

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
        string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace("/", "\\");
        string assetFilePath = projectPath + "\\" + assetPath;
        string assetMetaFilePath = assetFilePath + ".meta";
        if (!File.Exists(assetMetaFilePath))
        {
            Debug.LogWarning($"File does not exist, path too long? Path: {assetMetaFilePath}");
            return true;
        }

        Regex regex = new Regex(guid, RegexOptions.Compiled);
        foreach (var otherFilePath in otherFilesPaths)
        {
            if (!File.Exists(otherFilePath))
            {
                Debug.LogWarning($"File does not exist, path too long? Path: {otherFilePath}");
                continue;
            }

            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Looking for asset references", progress))
            {
                canceled = true;
                return true;
            }
            if (regex.IsMatch(File.ReadAllText(otherFilePath)))
            {
                if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue; // Skip the file itself or own .meta file
                return true;
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

        var assetPaths = AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/" + subfolder) && !AssetDatabase.IsValidFolder(x));
        assetPaths = assetPaths.Where(x => !x.Contains("/Resources/") &&
            !x.Contains("/Editor/") && !x.Contains("/Plugins/") && !x.Contains("StreamingAssets"));
        assetPaths = assetPaths.Where(x => !Regex.IsMatch(x, $"\\.({string.Join("|", excludedExtensions)})$"));

        window.unusedAssets = new List<string>(); // Empty old results

        List<string> extensionsToSearchInWithMeta = new List<string>(extensionsToSearchIn);
        extensionsToSearchInWithMeta.AddRange(extensionsToSearchIn.Select(x => x + ".meta"));

        projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        otherFilesPaths = otherFilesPaths.Where(x => Regex.IsMatch(x, $"\\.({string.Join("|", extensionsToSearchInWithMeta)})$")).ToList();

        int total = assetPaths.Count();
        int current = 0;
        foreach (var assetPath in assetPaths)
        {
            current++;
            progress = current / (float)total;

            if (canceled || EditorUtility.DisplayCancelableProgressBar("Searching...", "Looking for asset references", progress))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!FindAnyAssetUsage(guid))
            {
                window.unusedAssets.Add(assetPath);
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
            if (asset == null) continue;
            EditorGUILayout.LabelField(assetFilename);
            EditorGUILayout.ObjectField(asset, asset.GetType(), true);
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }
}
