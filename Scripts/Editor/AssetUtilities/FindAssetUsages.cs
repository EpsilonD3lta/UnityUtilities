using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static EditorHelper;
using Object = UnityEngine.Object;

public class FindAssetUsages : EditorWindow
{
    private static TreeViewComparer treeViewComparer = new();

    private List<Object> usedByObjects = new();
    private Object selectedObject;
    private Vector2 scroll = Vector2.zero;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static async void CreateWindow()
    {
        var window = CreateWindow<FindAssetUsages>();
        window.Show();

        if (Selection.objects.Length > 0)
            window.selectedObject = Selection.objects[0];

        window.usedByObjects = await FindObjectUsageAsync(window.selectedObject, true, true);
        window.Repaint();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(selectedObject, selectedObject.GetType(), true);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(18)))
        {
            Find();
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

    private async void Find()
    {
        usedByObjects = await FindObjectUsageAsync(selectedObject, true, true);
        Repaint();
    }

    public static async Task<List<Object>> FindObjectUsageAsync(Object obj, bool filter = false, bool sort = false)
    {
        string objectPath = "";
        Object asset = null;
        if (IsAsset(obj))
        {
            asset = obj;
            objectPath = AssetDatabase.GetAssetPath(obj);
        }
        else if (IsNonAssetGameObject(obj))
        {
            objectPath = obj.GetInstanceID().ToString();
        }

        bool finished = false;
        List<SearchItem> resultItems = new();
        SearchService.Request($"ref={objectPath}",
            (SearchContext context, IList<SearchItem> items)
            => Found(ref finished, items, ref resultItems));
        await WaitUntil(() => finished);
        var results = resultItems.Select(x => x.ToObject()).Where(x => x != null).ToList();

        if (filter) results = FilterResults(results, asset);
        if (sort) results = SortResults(results);
        return results;
    }


    // This is faster for multiple searches e.g. in FindUnusedAssets, because Async version only does 1 search per editor Frame
    public static List<Object> FindObjectUsageSync(Object obj, bool filter = false, bool sort = false)
    {
        string objectPath = "";
        Object asset = null;
        if (IsAsset(obj))
        {
            asset = obj;
            objectPath = AssetDatabase.GetAssetPath(obj);
        }
        else if (IsNonAssetGameObject(obj))
        {
            objectPath = obj.GetInstanceID().ToString();
        }

        List<SearchItem> resultItems = new();
        var results = SearchService.Request($"ref={objectPath}", SearchFlags.Synchronous).Fetch()
            .Select(x => x.ToObject()).Where(x => x != null).ToList();

        if (filter) results = FilterResults(results, asset);
        if (sort) results = SortResults(results);
        return results;
    }

    // Without IsAnyPrefabInstanceRoot, results contain every childGameobject
    public static List<Object> FilterResults(List<Object> results, Object asset = null)
    {
        results = results.Distinct().ToList();
        if (asset != null) // Only for Assets, not for Hierarchy GameObjects
            results = results.Where(x => !ArePartOfSameMainAssets(x, asset)).ToList();
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

    private static void Found(ref bool finished, IList<SearchItem> items, ref List<SearchItem> resultItems)
    {
        resultItems = items.ToList();
        finished = true;
    }

    public static async Task WaitUntil(System.Func<bool> condition, int timeout = -1)
    {
        var waitTask = Task.Run(async () =>
        {
            while (!condition()) await Task.Delay(1);
        });

        if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout))) throw new System.TimeoutException();
    }

}

