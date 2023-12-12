using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

    private List<Object> unusedAssets = new();
    private string subfolder = "";
    private bool canceled = false;
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Tools/Find Unused Assets")]
    public static void CreateWindow()
    {
        FindUnusedAssets window = GetWindow<FindUnusedAssets>();
        window.Show();
    }

    private async void FindUnused()
    {
        canceled = false;
        unusedAssets.Clear(); // Empty old results

        var assetsSubfolder = "Assets/" + subfolder;
        var assetPaths = AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/" + subfolder)
            && !AssetDatabase.IsValidFolder(x)).ToList();
        assetPaths = assetPaths.Where(x => !x.Contains("/Resources/") &&
            !x.Contains("/Editor/") && !x.Contains("/Plugins/") && !x.Contains("/StreamingAssets/") &&
            !x.Contains("/Addressables/") && !x.Contains("/External/") && !x.Contains("/ExternalAssets/")
            && !x.Contains("/IgnoreSCM/") && !x.Contains("/AddressableAssetsData/") && !x.Contains("/FacebookSDK/")
            && !x.Contains("/GoogleMobileAds/") && !x.Contains("/GooglePlayGames/")
            && !x.Contains("/Settings/") && !x.Contains("/TextMesh Pro/")).ToList();
        assetPaths = assetPaths.Where(x => !Regex.IsMatch(x, $"\\.({string.Join("|", excludedExtensions)})$")).ToList();

        // If we deliberately select subfolder that is one of the above, add it again
        if (assetPaths.Count == 0)
            assetPaths = AssetDatabase.GetAllAssetPaths().Where(x => x.StartsWith("Assets/" + subfolder)
                && !AssetDatabase.IsValidFolder(x)).ToList();

        // Do not check scripts that do not contain class derived from UnityEngine.Object
        var assetPathsCopy = new List<string>(assetPaths);
        var scripts = assetPathsCopy.Where(x => x.EndsWith(".cs")).ToList();
        var nonMonos = new List<string>();
        foreach (var s in scripts)
        {
            var loadedScript = AssetDatabase.LoadAssetAtPath<MonoScript>(s);
            if (loadedScript != null &&
                (loadedScript.GetClass() == null || !loadedScript.GetClass().IsSubclassOf(typeof(Object))))
            {
                assetPathsCopy.Remove(s);
            }
        }
        assetPaths = assetPathsCopy;

        int total = assetPaths.Count;
        int current = 0;
        float progress = 0;

        if (total > 0)
        {
            // This will properly initialize SearchIndex, without it, Sync method could return incorrect (empty) results
            var testObj = AssetDatabase.LoadMainAssetAtPath(assetPaths.ToList()[0]);
            await FindAssetUsages.FindObjectUsageAsync(testObj);
        }

        var unusedAssetPaths = new List<string>();
        foreach (var assetPath in assetPaths)
        {
            current++;
            progress = current / (float)total;

            if (canceled ||
                EditorUtility.DisplayCancelableProgressBar($"Searching... {current}/{total}", $"Canceled", progress))
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            var obj = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (!FindAnyAssetUsage(obj))
            {
                unusedAssetPaths.Add(assetPath);
            }

        }
        unusedAssetPaths = unusedAssetPaths.OrderBy(x => x, new EditorHelper.TreeViewComparer()).ToList();
        unusedAssets = unusedAssetPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();

        EditorUtility.ClearProgressBar();
    }

    /// <summary> Tries to find any asset usage. If one is found, returns true </summary>
    private static bool FindAnyAssetUsage(Object obj)
    {
        var usedBy = FindAssetUsages.FindObjectUsageSync(obj);
        return usedBy.Any();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUIContent labelContent = new GUIContent("Subfolder:", "\"Assets/\" + subFolder");
        GUILayout.Label(labelContent, GUILayout.MaxWidth(65));
        subfolder = GUILayout.TextField(subfolder);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(20)))
        {
            FindUnused();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Unused Assets: ({unusedAssets.Count})", EditorStyles.boldLabel);
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
