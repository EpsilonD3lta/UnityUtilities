using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static EditorHelper;

public class FindAssetUsages : EditorWindow
{
    private static TreeViewComparer treeViewComparer = new();

    private List<Object> usedByObjects = new();
    private Object selectedObject;
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static void CreateWindow()
    {
        var window = CreateWindow<FindAssetUsages>();
        window.Show();

        if (Selection.objects.Length > 0)
            window.selectedObject = Selection.objects[0];

        window.usedByObjects = FindObjectUsage(window.selectedObject, true, true);
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(selectedObject, selectedObject.GetType(), true);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(18)))
        {
            usedByObjects = FindObjectUsage(selectedObject, false, true);
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        GUILayout.Label("Found Asset References", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var asset in usedByObjects)
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

    public static List<Object> FindObjectUsage(Object go, bool filtered = false, bool sorted = false)
    {
        var results = new List<Object>();
        if (IsAsset(go))
        {
            results = FindAssetUsage(GetGuid(go), filtered, sorted);
        }
        else if (IsNonAssetGameObject(go))
        {
            results = FindGameObjectUsage((GameObject)go);
        }
        return results;
    }

    public static List<Object> FindAssetUsage(string assetGuid, bool filtered = false, bool sorted = false)
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
        var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
        var results = SearchService.Request($"ref={assetPath}", SearchFlags.Synchronous).Fetch()
            .Select(x => x.ToObject()).Where(x => !ArePartOfSameMainAssets(x, asset)).ToList();

        if (filtered) results = FilterResults(results);
        if (sorted) results = SortResults(results);
        return results;
    }

    // Without IsAnyPrefabInstanceRoot, results contain every childGameobject
    public static List<Object> FilterResults(List<Object> results)
    {
        var filteredResults = new List<Object>();
        foreach (var obj in results)
        {
            if (IsAsset(obj))
            {
                filteredResults.Add(obj);
                continue;
            }
            if (IsNonAssetGameObject(obj))
            {
                var go = obj as GameObject;
                if (!PrefabUtility.IsPartOfAnyPrefab(go))
                {
                    filteredResults.Add(go);
                    continue;
                }
                if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    filteredResults.Add(go);
                    continue;
                }
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                if (!results.Contains(root))
                    filteredResults.Add(go);
            }
        }
        return filteredResults;
    }

    // Sort as treeView, NonAssets last
    public static List<Object> SortResults(List<Object> results)
    {
        var sortedResults = results.Where(x => IsAsset(x))
            .OrderBy(x => AssetDatabase.GetAssetPath(x), treeViewComparer).ToList();

        // NonAssets last
        sortedResults.AddRange(results.Where(x => !IsAsset(x)));
        return sortedResults;
    }

    public static List<Object> FindGameObjectUsage(GameObject go)
    {
        var results = SearchService.Request($"ref={go.GetInstanceID()}", SearchFlags.Synchronous).Fetch()
            .Select(x => x.ToObject()).ToList();
        return results;
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
