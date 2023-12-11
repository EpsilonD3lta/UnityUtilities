using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static EditorHelper;

public class FindAssetUsages : EditorWindow
{
    private List<Object> assets = new();
    private string selectedAssetGuid;
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static void CreateWindow()
    {
        var window = CreateWindow<FindAssetUsages>();
        window.Show();

        string[] assetGuids = Selection.assetGUIDs;
        if (assetGuids.Length == 0)
        {
            Debug.Log("Cannot find asset usages when no assets are selected.");
            return;
        }

        window.selectedAssetGuid = assetGuids[0];
        window.assets = FindAssetUsageFilteredSorted(window.selectedAssetGuid);
    }

    private void OnGUI()
    {
        if (selectedAssetGuid == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }
        string searchedAssetFilename = AssetDatabase.GUIDToAssetPath(selectedAssetGuid);
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
            if (!string.IsNullOrEmpty(selectedAssetGuid))
                assets = FindAssetUsageFilteredSorted(selectedAssetGuid);
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        GUILayout.Label("Found Asset References", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var asset in assets)
        {
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset));
            if (asset != null)
            {
                var type = asset.GetType();
                EditorGUILayout.ObjectField(asset, type, true);
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }

    public static List<Object> FindAssetUsage(string assetGuid)
    {
        string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
        var results = SearchService.Request($"ref={assetPath}", SearchFlags.Synchronous).Fetch()
            .Select(x => x.ToObject()).ToList();
        return results;
    }

    public static List<Object> FindAssetUsageFiltered(string assetGuid)
    {
        var results = FindAssetUsage(assetGuid);
        results = results.Where(x => IsAsset(x) ||
            (IsNonAssetGameObject(x) && PrefabUtility.IsAnyPrefabInstanceRoot((GameObject)x))).ToList();
        return results;
    }

    public static List<Object> FindAssetUsageFilteredSorted(string assetGuid)
    {
        var results = FindAssetUsageFiltered(assetGuid);
        var sortedResults = results.Where(x => IsAsset(x))
            .OrderBy(x => AssetDatabase.GetAssetPath(x), new TreeViewComparer()).ToList();

        // NonAssets last
        sortedResults.AddRange(results.Where(x => !IsAsset(x)));
        return sortedResults;
    }

    //private async Task FindAssetUsage(string assetGuid)
    //{
    //    CancelSearch();
    //    tokenSource = new CancellationTokenSource();
    //    //progressId = Progress.Start($"Find Asset Usages {selectedAssetName}");
    //    //Progress.RegisterCancelCallback(progressId, CancelSearch);
    //    assets = await FindAssetUsageAsync(assetGuid, tokenSource.Token, progressId);
    //    //assets = FindAssetUsageSync(assetGuid);
    //    Repaint();
    //    tokenSource = null;
    //}


    //public static async Task<List<Object>> FindAssetUsageAsync(string assetGuid)
    //{
    //    string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
    //    bool finished = false;
    //    List<SearchItem> its = new();
    //    SearchService.Request($"ref={assetPath}",
    //        (SearchContext context, IList<SearchItem> items)
    //        => Found(ref finished, context, items, ref its), SearchFlags.WantsMore);
    //    await WaitUntil(() => finished);
    //    return its.Select(x => x.ToObject()).ToList();
    //}

    //private static void Found(ref bool finished, SearchContext context, IList<SearchItem> items, ref List<SearchItem> results)
    //{
    //    results = items.ToList();
    //    finished = true;
    //}

    //public static async Task WaitUntil(System.Func<bool> condition, int timeout = -1)
    //{
    //    var waitTask = Task.Run(async () =>
    //    {
    //        while (!condition()) await Task.Delay(1);
    //    });

    //    if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout))) throw new System.TimeoutException();
    //}
}
