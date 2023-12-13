using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using static EditorHelper;
using static MyGUI;

public class AssetsHistory : MyEditorWindow, IHasCustomMenu
{
    protected const int rowHeight = objectRowHeight;
    protected const int minColumnWidth = 150;
    protected virtual string prefId => PlayerSettings.companyName + "." +
        PlayerSettings.productName + ".EpsilonDelta.AssetsHistory.";

    protected List<Object> groupedHistory = new List<Object>();
    protected List<Object> history = new List<Object>();
    protected List<Object> pinned = new List<Object>();
    private int limit = 10;
    private int lastSelectedIndex = -1;

    [MenuItem("Window/Assets History")]
    private static void CreateWindow()
    {
        AssetsHistory window;
        if (Resources.FindObjectsOfTypeAll<AssetsHistory>().Any(x => x.GetType() == typeof(AssetsHistory)))
            window = GetWindow(typeof(AssetsHistory), false, "Assets History") as AssetsHistory;
        else window = CreateWindow<AssetsHistory>("Assets History");
        window.minSize = new Vector2(100, rowHeight + 1);
        window.Show();
    }

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear All"), false, ClearAll);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear History"), false, ClearHistory);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear Pinned"), false, ClearPinned);
    }

    protected virtual void Test()
    {

    }

    protected virtual void Awake()
    {
        LoadHistoryFromEditorPrefs();
    }

    protected virtual void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChanged;
        Selection.selectionChanged += SelectionChanged;
        AssetImportHistory.assetImported -= AssetImported;
        AssetImportHistory.assetImported += AssetImported;
        EditorSceneManager.sceneOpened -= SceneOpened;
        EditorSceneManager.sceneOpened += SceneOpened;
        EditorApplication.quitting -= SaveHistoryToEditorPrefs;
        EditorApplication.quitting += SaveHistoryToEditorPrefs;
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        LimitAndOrderHistory();
    }

    protected virtual void OnDisable()
    {

    }

    protected virtual void OnDestroy()
    {
        SaveHistoryToEditorPrefs();
    }

    // This is received only when window is visible
    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        var ev = Event.current; //Debug.Log(ev.type);
        var height = position.height;
        var width = position.width;
        int lines = Mathf.FloorToInt(height / rowHeight);
        int columns = Mathf.FloorToInt(width / minColumnWidth);
        if (columns <= 1) columns = 2;
        float columnWidth = width / columns;
        float xPos = 0, yPos = 0;
        bool shouldLimitAndOrderHistory = false;
        bool isAnyShortRectHover = false;
        bool isAnyHover = false;

        if (limit != lines * columns)
        {
            limit = lines * columns;
            LimitAndOrderHistory();
        }
        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown) KeyboardNavigation(ev, ref lastSelectedIndex, groupedHistory, OnDeleteKey);
        for (int i = 0; i < groupedHistory.Count; i++)
        {
            var obj = groupedHistory[i];
            if (obj == null)
            {
                RemoveHistory(obj);
                RemovePinned(obj);
                shouldLimitAndOrderHistory = true; // Don't modify groupedHistory in this loop
                continue;
            }

            Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
            bool isSelected = Selection.objects.Contains(obj);
            bool isPinned = pinned.Contains(obj);

            var b = ObjectRow(rect, i, obj, groupedHistory, ref lastSelectedIndex, null, isPinned,
                () => MiddleClick(obj, isSelected, isPinned, ref shouldLimitAndOrderHistory),
                () => PingButtonMiddleClick(obj, isPinned, ref shouldLimitAndOrderHistory),
                () => DragStarted(obj, isSelected),
                () => DragPerformed(obj, i, isPinned, ref shouldLimitAndOrderHistory));

            if (b.isHovered)
            {
                isAnyHover = true;
                hoverObject = obj;
            }

            // Draw insertion line at the end of pinned if dragging and mouse position is not above any pinned asset
            if (!isAnyShortRectHover && i == pinned.Count && DragAndDrop.visualMode != DragAndDropVisualMode.None)
            {
                DrawDragInsertionLine(rect);
            }

            if (b.isShortRectHovered) isAnyShortRectHover = true;
            yPos += rowHeight;
            if ((i + 1) % lines == 0) { xPos += columnWidth; yPos = 0; } // Draw next column
        }
        // DragAndDrop to window empty space (with no asset rows)
        if (ev.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            ev.Use();
        }
        if (ev.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            DropObjectToWindow();
            shouldLimitAndOrderHistory = true;
            ev.Use();
        }
        if (shouldLimitAndOrderHistory) LimitAndOrderHistory();
        if (!isAnyHover) hoverObject = null;
    }

    private void MiddleClick(Object obj, bool isSelected, bool isPinned, ref bool shouldLimitAndOrderHistory)
    {
        var ev = Event.current;
        if (ev.modifiers == EventModifiers.Control)
            if (isPinned) ClearPinned();
            else ClearHistory();
        else if (isSelected)
        {
            RemoveAllHistory(x => Selection.objects.Contains(x));
            RemoveAllPinned(x => Selection.objects.Contains(x));
        }
        else
        {
            RemoveHistory(obj);
            RemovePinned(obj);
        }
        shouldLimitAndOrderHistory = true;
        ev.Use();
        Repaint();
    }

    private void PingButtonMiddleClick(Object obj, bool isPinned, ref bool shouldLimitAndOrderHistory)
    {
        if (!isPinned) AddPinned(obj);
        else RemovePinned(obj);
        shouldLimitAndOrderHistory = true; // Only return dirtied if we change something
    }

    private void DropObjectToWindow()
    {
        foreach (var obj in DragAndDrop.objectReferences)
        {
            AddPinned(obj);
        }
    }

    // Drag started from ObjectRow
    private void DragStarted(Object obj, bool isSelected)
    {
        DragAndDrop.SetGenericData(GetInstanceID().ToString(), true);
    }


    // Drag performed on pinned or not pinned ObjectRow
    private void DragPerformed(Object obj, int i, bool isPinned, ref bool shouldLimitAndOrderHistory)
    {
        var ev = Event.current;
        if (isPinned)
        {
            DragAndDrop.AcceptDrag();
            int k = 0; // Insert would revert order if we do not compensate
            foreach (var droppedObj in DragAndDrop.objectReferences)
            {
                if (!pinned.Contains(droppedObj)) AddPinned(droppedObj, i);
                else if (pinned.IndexOf(droppedObj) != i)
                {
                    int insertIndex = pinned.IndexOf(droppedObj) > i ? i + k : i - 1;
                    RemovePinned(droppedObj);
                    AddPinned(droppedObj, insertIndex);
                }
                k++;
            }
            shouldLimitAndOrderHistory = true;
            ev.Use();
        }
        // Prevent accidental drags
        else if (ev.type == EventType.DragPerform && ev.button == 0)
        {
            var dragData = DragAndDrop.GetGenericData(GetInstanceID().ToString());
            bool preventDrop = dragData is bool b && b && DragAndDrop.objectReferences.Length == 1 &&
                DragAndDrop.objectReferences[0] == obj; // Same object row
            if (preventDrop)
            {
                DragAndDrop.AcceptDrag();
                ev.Use();
            }
        }
    }

    private void OnDeleteKey()
    {
        RemoveAllHistory(x => Selection.objects.Contains(x));
        RemoveAllPinned(x => Selection.objects.Contains(x));
        LimitAndOrderHistory();
        Repaint();
    }

    protected virtual void SelectionChanged()
    {
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            AddHistory(asset);
            LimitAndOrderHistory();
        }
    }

    [UnityEditor.ShortcutManagement.Shortcut("Assets History/Add to AssetsHistory")]
    public static void AddPinnedGlobal()
    {
        var windows = Resources.FindObjectsOfTypeAll<AssetsHistory>();
        foreach (var window in windows)
        {
            foreach (var obj in Selection.objects)
            {
                if (!window.pinned.Contains(obj))
                {
                    window.AddFilteredPinned(obj);
                }
            }
            window.LimitAndOrderHistory();
            window.Repaint();
        }
    }

    private void AssetImported(Object obj)
    {
        AddHistory(obj);
        LimitAndOrderHistory();
    }

    protected virtual void SceneOpened(Scene scene, OpenSceneMode mode)
    {
        AddHistory(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
        LimitAndOrderHistory();
    }

    protected virtual void AddHistory(Object obj)
    {
        AddToFront(obj, history);
    }

    protected virtual void AddPinned(Object obj, int i = -1)
    {
        if (i == -1) AddToEnd(obj, pinned);
        else
        {
            RemovePinned(obj);
            pinned.Insert(i, obj);
        }
    }

    protected virtual void AddFilteredPinned(Object obj)
    {
        if (IsAsset(obj)) AddPinned(obj);
    }

    protected virtual void RemoveHistory(Object obj)
    {
        history.Remove(obj);
    }

    protected virtual void RemovePinned(Object obj)
    {
        pinned.Remove(obj);
    }

    protected virtual void RemoveAllPinned(Predicate<Object> predicate)
    {
        foreach (var obj in pinned.Where(x => predicate(x)).ToList())
        {
            RemovePinned(obj);
        }
    }

    protected virtual void RemoveAllHistory(Predicate<Object> predicate)
    {
        foreach (var obj in history.Where(x => predicate(x)).ToList())
        {
            RemoveHistory(obj);
        }
    }

    protected void AddToFront<T>(T obj, List<T> list, int limit = -1)
    {
        list.Remove(obj);
        list.Insert(0, obj);
        if (limit > -1 && list.Count > limit)
        {
            for (int i = list.Count - 1; i >= limit; i--) list.RemoveAt(i);
        }
    }

    protected void AddToEnd<T>(T obj, List<T> list)
    {
        list.Remove(obj);
        list.Add(obj);
    }

    protected virtual void ClearAll()
    {
        history.Clear();
        pinned.Clear();
        LimitAndOrderHistory();
    }

    protected virtual void ClearHistory()
    {
        history.Clear();
        LimitAndOrderHistory();
    }

    protected virtual void ClearPinned()
    {
        pinned.Clear();
        LimitAndOrderHistory();
    }

    protected virtual void LimitAndOrderHistory()
    {
        RemoveAllHistory(x => x == null);
        RemoveAllPinned(x => x == null);
        int onlyPinned = pinned.Where(x => !history.Contains(x)).Count();
        int historyLimit = limit - onlyPinned;
        if (history.Count > historyLimit)
            RemoveAllHistory(x => history.IndexOf(x) >= historyLimit);
        //history = history.Take(historyLimit).ToList();
        groupedHistory = history.Where(x => !pinned.Contains(x)).OrderBy(x => x.GetType().Name).ThenBy(x => x.name).
            ThenBy(x => x.GetInstanceID()).ToList();
        groupedHistory.InsertRange(0, pinned);
    }

    protected virtual void SaveHistoryToEditorPrefs()
    {
        string pinnedPaths = string.Join("|", pinned.Select(x => AssetDatabase.GetAssetPath(x)));
        EditorPrefs.SetString(prefId + nameof(pinned), pinnedPaths);
        string historyPaths = string.Join("|", history.Select(x => AssetDatabase.GetAssetPath(x)));
        EditorPrefs.SetString(prefId + nameof(history), historyPaths);
    }

    protected virtual void LoadHistoryFromEditorPrefs()
    {
        string[] pinnedPaths = EditorPrefs.GetString(prefId + nameof(pinned)).Split('|');
        foreach (var path in pinnedPaths)
            AddPinned(AssetDatabase.LoadMainAssetAtPath(path));
        string[] historyPaths = EditorPrefs.GetString(prefId + nameof(history)).Split('|');
        foreach (var path in historyPaths)
            history.Add(AssetDatabase.LoadMainAssetAtPath(path)); // Preserve order
    }

    #region Drawing
    private void DrawDragInsertionLine(Rect fullRect)
    {
        Rect lineRect = new Rect(fullRect.x, fullRect.y - 4, fullRect.width, 3);
        GUI.Label(lineRect, GUIContent.none, Styles.insertion);
    }
    #endregion
}

public class AssetImportHistory : AssetPostprocessor
{
    public static Action<Object> assetImported;

    // Number of parameters changes in newer Unity versions
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (var path in importedAssets)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) assetImported?.Invoke(asset);
        }
    }
}
