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
using UnityEditor.PackageManager.UI;
using System.IO;

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
        DragAndDrop.AddDropHandler(OnDragDroppedToProjectTab);
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        LimitAndOrderHistory();
    }

    private void OnDisable()
    {
        DragAndDrop.RemoveDropHandler(OnDragDroppedToProjectTab);
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

            Rect fullRect = new Rect(xPos, yPos, columnWidth, rowHeight);
            bool isSelected = Selection.objects.Contains(obj);
            bool isPinned = pinned.Contains(obj);

            var buttonResult = DrawObjectRow(fullRect, obj, isSelected, isPinned);

            if (buttonResult.isHovered) isAnyHover = true;
            if (buttonResult.isHovered) hoverObject = obj;
            if (buttonResult.pingButtonClicked)
            {
                if (Event.current.button == 0)
                    PingButtonLeftClick(obj);
                else if (Event.current.button == 1)
                    PingButtonRightClick(obj);
                else if (Event.current.button == 2)
                    if (PingButtonMiddleClick(obj, isPinned))
                        shouldLimitAndOrderHistory = true;
            }

            if (buttonResult.isShortRectHovered)
            {
                if (ev.type == EventType.MouseUp && ev.button == 0 && ev.clickCount == 1)
                {
                    LeftMouseUp(obj, isSelected, i); // Select on MouseUp
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2)
                {
                    DoubleClick(obj);
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 1)
                {
                    RightClick(obj, i);
                    ev.Use();
                }
                else if (ev.type == EventType.ContextClick)
                {
                    ContextClick(new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), obj);
                }
                else if (ev.type == EventType.MouseDown && ev.button == 2)
                {
                    MiddleClick(obj, isSelected, isPinned, ref shouldLimitAndOrderHistory);
                }
                // Drag
                else if (ev.type == EventType.MouseDrag && ev.button == 0 && // Start dragging this asset
                    DragAndDrop.visualMode == DragAndDropVisualMode.None)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData("StartedInAssetsHistoryWindow", true);
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    if (isSelected)
                        DragAndDrop.objectReferences = groupedHistory.Where(x => Selection.objects.Contains(x))
                        .ToArray();
                    else DragAndDrop.objectReferences = new Object[] { obj };
                    DragAndDrop.StartDrag("AssetsHistory Drag");
                    ev.Use();
                }
                else if (ev.type == EventType.DragUpdated && ev.button == 0) // Update drag
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                    GUI.Label(fullRect, GUIContent.none, Styles.insertion);
                    ev.Use();
                }
                else if (ev.type == EventType.DragPerform && ev.button == 0 && isPinned) // Receive drag and drop
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
                    var dragData = DragAndDrop.GetGenericData("StartedInAssetsHistoryWindow");
                    bool preventDrop = dragData is bool b && b && DragAndDrop.objectReferences.Length == 1 &&
                        DragAndDrop.objectReferences[0] == obj; // Same object row
                    if (preventDrop)
                    {
                        DragAndDrop.AcceptDrag();
                        ev.Use();
                    }
                }
                // Draw insertion line
                if (isPinned && DragAndDrop.visualMode != DragAndDropVisualMode.None)
                {
                    DrawDragInsertionLine(fullRect);
                }
            }

            // Draw insertion line at the end of pinned if dragging and mouse position is not above any pinned asset
            if (!isAnyShortRectHover && i == pinned.Count && DragAndDrop.visualMode != DragAndDropVisualMode.None)
            {
                DrawDragInsertionLine(fullRect);
            }

            if (buttonResult.isShortRectHovered) isAnyShortRectHover = true;
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

    private void LeftMouseUp(Object obj, bool isSelected, int i)
    {
        lastSelectedIndex = i;
        var ev = Event.current;
        if (ev.modifiers == EventModifiers.Control) // Ctrl select
            if (!isSelected) Selection.objects = Selection.objects.Append(obj).ToArray();
            else Selection.objects = Selection.objects.Where(x => x != obj).ToArray();
        else if (ev.modifiers == EventModifiers.Shift) // Shift select
        {
            int firstSelected = groupedHistory.FindIndex(x => Selection.objects.Contains(x));
            if (firstSelected != -1)
            {
                int startIndex = Mathf.Min(firstSelected + 1, i);
                int count = Mathf.Abs(firstSelected - i);
                Selection.objects = Selection.objects.
                    Concat(groupedHistory.GetRange(startIndex, count)).Distinct().ToArray();
            }
            else Selection.objects = Selection.objects.Append(obj).ToArray();
        }
        else
        {
            Selection.activeObject = obj; // Ordinary select
            if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                ExpandFolder(obj.GetInstanceID(), true);
        }
    }

    private void DoubleClick(Object obj)
    {
        OpenObject(obj);
    }

    // This is different event then context click, bot are executed, context after right click
    private void RightClick(Object obj, int i)
    {
        lastSelectedIndex = i;
        Selection.activeObject = obj;
    }

    private void ContextClick(Rect rect, Object obj)
    {
        Selection.activeObject = obj;
        if (IsComponent(obj)) OpenObjectContextMenu(rect, obj);
        else if (IsAsset(obj)) EditorUtility.DisplayPopupMenu(rect, "Assets/", null);
        else if (IsNonAssetGameObject(obj))
        {
            if (Selection.transforms.Length > 0) // Just to be sure it's really a HierarchyGameobject
                OpenHierarchyContextMenu(Selection.transforms[0].gameObject.GetInstanceID());
        }
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

    private void PingButtonLeftClick(Object obj)
    {
        if (Event.current.modifiers == EventModifiers.Alt) // Add or remove pinned item
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
            obj = AssetDatabase.LoadMainAssetAtPath(path);
            EditorGUIUtility.PingObject(obj);
        }
        else EditorGUIUtility.PingObject(obj);
    }

    private void PingButtonRightClick(Object obj)
    {
        OpenPropertyEditor(obj);
    }

    private bool PingButtonMiddleClick(Object obj, bool isPinned)
    {
        bool dirtied = false;
        if (Event.current.modifiers == EventModifiers.Alt)
            Debug.Log($"{GlobalObjectId.GetGlobalObjectIdSlow(obj)} InstanceID: {obj.GetInstanceID()}");
        else
        {
            if (!isPinned) AddPinned(obj);
            else RemovePinned(obj);
            dirtied = true; // Only return dirtied if we change something
        }
        return dirtied;
    }

    private void DropObjectToWindow()
    {
        foreach (var obj in DragAndDrop.objectReferences)
        {
            AddPinned(obj);
        }
    }

    private DragAndDropVisualMode OnDragDroppedToProjectTab(int dragInstanceId, string dropUponPath, bool perform)
    {
        if (!perform) return DragAndDropVisualMode.None; // Next Handler in order will handle this drag (Unity default)
        if (!AssetDatabase.IsValidFolder(dropUponPath)) return DragAndDropVisualMode.None;
        var dragData = DragAndDrop.GetGenericData("StartedInAssetsHistoryWindow");
        if (!(dragData is bool b && b))
            return DragAndDropVisualMode.None;
        foreach (var droppedObj in DragAndDrop.objectReferences)
        {
            if (IsAsset(droppedObj))
            {
                var oldPath = AssetDatabase.GetAssetPath(droppedObj);
                var assetName = Path.GetFileName(oldPath);
                var newPath = dropUponPath + "/" + assetName;
                AssetDatabase.MoveAsset(oldPath, newPath);
            }
        }
        AssetDatabase.Refresh();
        return DragAndDropVisualMode.Move;
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
