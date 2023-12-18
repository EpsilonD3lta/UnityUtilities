using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using static EditorHelper;
using static MyGUI;
using Object = UnityEngine.Object;

public class FindAssetUsages : MyEditorWindow
{
    private static TreeViewComparer treeViewComparer = new();

    private Object selectedObject;
    private List<Object> usedByObjects = new();
    private List<Object> shownItems = new();
    private Vector2 scroll = Vector2.zero;
    private int lastSelectedIndex = -1;
    private bool adjustSize;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static async void CreateWindow()
    {
        var window = CreateWindow<FindAssetUsages>();
        window.Show();

        if (Selection.objects.Length > 0)
            window.selectedObject = Selection.objects[0];

        await window.Find();
        window.Repaint();
    }

    private void OnEnable()
    {
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        var ev = Event.current;
        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown) KeyboardNavigation(
            ev, ref lastSelectedIndex, shownItems, escapeKey: OnEscapeKey);
        bool isAnyHover = false;
        GUILayout.BeginHorizontal();
        if (shownItems.Count > 0)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, objectRowHeight);
            var buttonResult = ObjectRow(rect, 0, shownItems[0], shownItems, ref lastSelectedIndex);
            if (buttonResult.isHovered) { isAnyHover = true; hoverObject = shownItems[0]; }
        }

        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(16)))
            _ = Find();

        GUILayout.EndHorizontal();
        GUILayout.Label("Is Used By:", EditorStyles.boldLabel, GUILayout.MaxHeight(16));

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 1; i < shownItems.Count; i++)
        {
            var obj = shownItems[i];
            if (obj == null) continue;

            var guiStyle = new GUIStyle(); guiStyle.margin = new RectOffset();
            Rect rect = EditorGUILayout.GetControlRect(false, objectRowHeight, guiStyle);
            var buttonResult = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex);
            if (buttonResult.isHovered) { isAnyHover = true; hoverObject = obj; }
        }
        if (!isAnyHover) hoverObject = null;
        EditorGUILayout.EndScrollView();
        if (adjustSize)
        {
            float height = shownItems.Count * objectRowHeight + 30;
            float windowHeight = Mathf.Min(height, 1200f);
            if (adjustSize) windowHeight = Mathf.Max(windowHeight, position.height); // Enlarge only
            position = new Rect(position.position,
                new Vector2(position.width, windowHeight));
            adjustSize = false;
        }
    }

    private async Task Find()
    {
        usedByObjects = await FindObjectUsageAsync(selectedObject, true, true);
        shownItems.Clear();
        shownItems.Add(selectedObject);
        shownItems.AddRange(usedByObjects);
        adjustSize = true;
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
        // This is copied from Unity's experimental package: https://github.com/Unity-Technologies/com.unity.search.extensions
        // from script Dependency.cs
        var searchContext = SearchService.CreateContext(new[] { "dep", "scene", "asset", "adb" }, $"ref=\"{objectPath}\"");
        SearchService.Request(searchContext,
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
        var searchContext = SearchService.CreateContext(new[] { "dep", "scene", "asset", "adb" }, $"ref=\"{objectPath}\"");
        var results = SearchService.Request(searchContext, SearchFlags.Synchronous).Fetch()
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

