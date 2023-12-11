using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class FindUnusedAssets : EditorWindow
{
    private static readonly string[] excludedExtensions =
{
       "unity", "preset", "spriteatlas",
       "dll", "m", "java", "aar", "jar", "mm", "h", "plist",
       "xml", "json", "txt", "md", "pdf",
       "asmdef", "asmref",
    };

    private string subfolder = "";
    private bool canceled = false;
    private float progress = 0;
    private List<Object> unusedAssets = new();
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Tools/Find Unused Assets")]
    public static void CreateWindow()
    {
        FindUnusedAssets window = GetWindow<FindUnusedAssets>();
        window.Show();
    }

    private static void FindAssets()
    {
        FindUnusedAssets window = GetWindow<FindUnusedAssets>();
        window.Show();
        window.canceled = false;

        var assetPaths = AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/" + window.subfolder)
            && !AssetDatabase.IsValidFolder(x));
        assetPaths = assetPaths.Where(x => !x.Contains("/Resources/") &&
            !x.Contains("/Editor/") && !x.Contains("/Plugins/") && !x.Contains("/StreamingAssets/") &&
            !x.Contains("/Addressables/") && !x.Contains("/External/") && !x.Contains("/ExternalAssets/")
            && !x.Contains("/IgnoreSCM/") && !x.Contains("/AddressableAssetsData/") && !x.Contains("/FacebookSDK/")
            && !x.Contains("/GoogleMobileAds/") && !x.Contains("/GooglePlayGames/"));
        assetPaths = assetPaths.Where(x => !Regex.IsMatch(x, $"\\.({string.Join("|", excludedExtensions)})$"));

        // Do not check scripts that do not contain class derived from UnityEngine.Object
        {
            var assetPathsList = assetPaths.ToList();
            var scripts = assetPathsList.Where(x => x.EndsWith(".cs")).ToList();
            var nonMonos = new List<string>();
            foreach (var s in scripts)
            {
                var loadedScript = AssetDatabase.LoadAssetAtPath<MonoScript>(s);
                if (loadedScript != null &&
                    (loadedScript.GetClass() == null || !loadedScript.GetClass().IsSubclassOf(typeof(Object))))
                {
                    assetPathsList.Remove(s);
                }
            }
            assetPaths = assetPathsList;
        }

        window.unusedAssets = new(); // Empty old results

        int total = assetPaths.Count();
        int current = 0;
        foreach (var assetPath in assetPaths)
        {
            current++;
            window.progress = current / (float)total;

            if (window.canceled || EditorUtility.DisplayCancelableProgressBar("Searching...", $"Canceled", window.progress))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!FindAnyAssetUsage(guid))
            {
                window.unusedAssets.Add(AssetDatabase.LoadMainAssetAtPath(assetPath));
            }
        }

        EditorUtility.ClearProgressBar();
    }

    /// <summary> Tries to find any asset usage. If one is found, returns true </summary>
    private static bool FindAnyAssetUsage(string guid)
    {
        var usedIn = FindAssetUsages.FindAssetUsage(guid);
        return usedIn.Any();
    }

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
        foreach (var asset in unusedAssets)
        {
            if (asset == null) continue;
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset));
            EditorGUILayout.ObjectField(asset, asset.GetType(), true);
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }
}
