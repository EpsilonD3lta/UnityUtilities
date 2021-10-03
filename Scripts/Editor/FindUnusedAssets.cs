using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class FindUnusedAssets : EditorWindow
{
    private static readonly string[] excludedExtensions =
{
       "preset",
       "spriteatlas",
       "unity",
       "dll",
       "m",
       "java",
       "aar",
       "asmdef",
    };

    private static readonly string[] excludedSearchInExtensions =
    {
        "dll",
        "m",
        "java",
        "aar",
        "cs"
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
        Regex regex = new Regex(guid, RegexOptions.Compiled);
        foreach (var otherFilePath in otherFilesPaths)
        {
            if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue; // Skip the file itself or own .meta file

            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Looking for asset references", progress))
            {
                canceled = true;
                return true;
            }

            if (regex.IsMatch(File.ReadAllText(otherFilePath))) return true;
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

        projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        otherFilesPaths = otherFilesPaths.Where(x => !x.Contains("/Plugins/")).ToList();
        otherFilesPaths = otherFilesPaths.Where(x => !Regex.IsMatch(x, $"\\.({string.Join("|", excludedSearchInExtensions)})$")).ToList();

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
