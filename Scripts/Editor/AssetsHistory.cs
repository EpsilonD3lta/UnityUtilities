using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class AssetsHistory : EditorWindow, IHasCustomMenu
{
    protected const int rowHeight = 16;
    protected const int minColumnWidth = 150;
    private static string prefId => PlayerSettings.companyName + "." +
        PlayerSettings.productName + ".EpsilonDelta.MeasureTool.";

    private static class Styles
    {
        public static GUIStyle insertion = "TV Insertion";
        public static GUIStyle lineStyle = "TV Line";
        public static GUIStyle selectionStyle = "TV Selection";
        public static GUIStyle pingButtonStyle;
        public static bool areStylesSet;
    }

    private Object hoverObject;
    private List<Object> groupedHistory = new List<Object>();
    private List<Object> history = new List<Object>();
    private List<Object> pinned = new List<Object>();
    protected Object lastGlobalSelectedObject;
    private Object currentMouseUppedObject;
    private int limit = 10;

    [MenuItem("Window/Assets History")]
    private static void CreateWindow()
    {
        var window = GetWindow(typeof(AssetsHistory), false, "Assets History") as AssetsHistory;
        window.minSize = new Vector2(100, rowHeight + 1);
        window.Show();
    }

    public void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear History"), false, ClearHistory);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear Pinned"), false, ClearPinned);
    }

    protected virtual void Awake()
    {
        LoadHistoryFromEditorPrefs();
    }

    protected virtual void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChange;
        Selection.selectionChanged += SelectionChange;
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

    private void Test()
    {
        Debug.Log(this.GetHashCode());
    }

    // This is received only when window is visible
    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        SetStyles();
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

        AddPinnedOnGlobalAltClick(ev);
        if (limit != lines * columns)
        {
            limit = lines * columns;
            LimitAndOrderHistory();
        }
        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown) KeyboardNavigation(ev);
        for (int i = 0; i < groupedHistory.Count; i++)
        {
            var asset = groupedHistory[i];
            if (asset == null)
            {
                history.Remove(asset);
                pinned.Remove(asset);
                shouldLimitAndOrderHistory = true; // Don't modify groupedHistory in this loop
                continue;
            }

            Rect fullRect = new Rect(xPos, yPos, columnWidth, rowHeight);
            Rect shortRect = new Rect(xPos, yPos, columnWidth - rowHeight, rowHeight);
            Rect pingButtonRect = new Rect(shortRect.xMax, shortRect.yMax - shortRect.height, shortRect.height, shortRect.height);
            bool isSelected = Selection.objects.Contains(asset);
            bool isPinned = pinned.Contains(asset);
            bool isHover = fullRect.Contains(ev.mousePosition);
            bool isShortRectHover = shortRect.Contains(ev.mousePosition);
            if (isHover) isAnyHover = true;
            if (isShortRectHover) isAnyShortRectHover = true;
            if (isHover) hoverObject = asset;

            if (ev.type == EventType.Repaint) DrawAssetRow(fullRect, rowHeight, asset, isHover, isSelected, isPinned);
            DrawPingButton(pingButtonRect, rowHeight, asset);

            if (isShortRectHover)
            {
                if (ev.type == EventType.MouseUp && ev.button == 0 &&  ev.clickCount == 1) // Select on MouseUp
                {
                    if (ev.modifiers == EventModifiers.Alt) // Add or remove pinned item
                    {
                        if (!isPinned) pinned.Add(asset);
                        else pinned.Remove(asset);
                        shouldLimitAndOrderHistory = true;
                    }
                    else if (ev.modifiers == EventModifiers.Control) // Ctrl select
                        if (!isSelected) Selection.objects = Selection.objects.Append(asset).ToArray();
                        else Selection.objects = Selection.objects.Where(x => x != asset).ToArray();
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
                        else Selection.objects = Selection.objects.Append(asset).ToArray();
                    }
                    else // Ordinary select
                    {
                        Selection.activeObject = asset;
                        currentMouseUppedObject = asset;
                    }
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2)
                {
                    DoubleClick(asset);
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 1)
                {
                    ContextClick(new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), asset);
                    ev.Use();
                }
                else if (ev.type == EventType.MouseDown && ev.button == 2) // Middle click
                {
                    if (ev.modifiers == EventModifiers.Control)
                        if (isPinned) pinned.Clear();
                        else history.Clear();
                    else if (isSelected)
                    {
                        history.RemoveAll(x => Selection.objects.Contains(x));
                        pinned.RemoveAll(x => Selection.objects.Contains(x));
                    }
                    else
                    {
                        history.Remove(asset);
                        pinned.Remove(asset);
                    }
                    shouldLimitAndOrderHistory = true;
                    ev.Use();
                    Repaint();
                }
                else if (ev.type == EventType.MouseDrag && ev.button == 0 && // Start dragging this asset
                    DragAndDrop.visualMode == DragAndDropVisualMode.None)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    if (isSelected)
                        DragAndDrop.objectReferences = groupedHistory.Where(x => Selection.objects.Contains(x))
                        .ToArray();
                    else DragAndDrop.objectReferences = new Object[] { asset };
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
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (!pinned.Contains(obj)) pinned.Insert(i, obj);
                        else if (pinned.IndexOf(obj) != i)
                        {
                            int insertIndex = pinned.IndexOf(obj) > i ? i + k : i - 1;
                            pinned.Remove(obj);
                            pinned.Insert(insertIndex, obj);
                        }
                        k++;
                    }
                    shouldLimitAndOrderHistory = true;
                    ev.Use();
                }
            }
            // Draw insertion line
            if (isShortRectHover && isPinned && DragAndDrop.visualMode != DragAndDropVisualMode.None)
            {
                DrawDragInsertionLine(fullRect);
            }
            // Draw insertion line at the end of pinned if dragging and mouse position is not above any pinned asset
            if (!isAnyShortRectHover && i == pinned.Count && DragAndDrop.visualMode != DragAndDropVisualMode.None)
            {
                DrawDragInsertionLine(fullRect);
            }
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

    private void DoubleClick(Object obj)
    {
        if (IsAsset(obj)) AssetDatabase.OpenAsset(obj);
        else if (IsNonAssetGameObject(obj)) SceneView.lastActiveSceneView.FrameSelected();
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

    private void DropObjectToWindow()
    {
        foreach (var obj in DragAndDrop.objectReferences)
        {
            if (!pinned.Contains(obj)) pinned.Add(obj);
            else // Move to the end
            {
                pinned.Remove(obj);
                pinned.Add(obj);
            }
        }
    }

    private void KeyboardNavigation(Event ev)
    {
        if (ev.keyCode == KeyCode.DownArrow)
        {
            int lastHighlightedIndex = groupedHistory.FindLastIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex + 1, groupedHistory.Count);
            Selection.objects = new Object[] { groupedHistory[selectIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.UpArrow)
        {
            int lastHighlightedIndex = groupedHistory.FindIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex - 1, groupedHistory.Count);
            Selection.objects = new Object[] { groupedHistory[selectIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Return)
        {
            var asset = history.FirstOrDefault(x => Selection.objects.Contains(x));
            DoubleClick(asset);
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Delete)
        {
            history.RemoveAll(x => Selection.objects.Contains(x));
            pinned.RemoveAll(x => Selection.objects.Contains(x));
            LimitAndOrderHistory();
            Repaint();
            ev.Use();
        }
    }

    /// <summary> Ads Last selected item from other windows </summary>
    private void AddPinnedOnGlobalAltClick(Event ev)
    {
        if (lastGlobalSelectedObject != null)
        {
            if (ev.modifiers == EventModifiers.Alt)
            {
                if (!pinned.Contains(lastGlobalSelectedObject))
                {
                    pinned.Add(lastGlobalSelectedObject);
                    LimitAndOrderHistory();
                }
            }
            lastGlobalSelectedObject = null;
        }
    }

    protected virtual void SelectionChange()
    {
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            lastGlobalSelectedObject = asset;
            AddToHistory(asset);
            LimitAndOrderHistory();
        }
    }

    private void AssetImported(Object asset)
    {
        AddToHistory(asset);
        LimitAndOrderHistory();
    }

    private void SceneOpened(Scene scene, OpenSceneMode mode)
    {
        AddToHistory(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
        LimitAndOrderHistory();
    }

    protected void AddToHistory(Object asset)
    {
        if (!history.Contains(asset)) history.Insert(0, asset);
        else MoveToFront(asset);
    }

    private void MoveToFront(Object asset)
    {
        var index = history.IndexOf(asset);
        history.RemoveAt(index);
        history.Insert(0, asset);
    }

    private void ClearHistory()
    {
        history.Clear();
        LimitAndOrderHistory();
    }

    private void ClearPinned()
    {
        pinned.Clear();
        LimitAndOrderHistory();
    }

    protected void LimitAndOrderHistory()
    {
        history.RemoveAll(x => x == null);
        pinned.RemoveAll(x => x == null);
        int onlyPinned = pinned.Where(x => !history.Contains(x)).Count();
        int historyLimit = limit - onlyPinned;
        if (history.Count > historyLimit) history = history.Take(historyLimit).ToList();
        groupedHistory = history.Where(x => !pinned.Contains(x)).OrderBy(x => x.GetType().Name).ThenBy(x => x.name).ToList();
        groupedHistory.InsertRange(0, pinned);
    }

    private void SaveHistoryToEditorPrefs()
    {
        string pinnedPaths = string.Join("|", pinned.Select(x => AssetDatabase.GetAssetPath(x)));
        EditorPrefs.SetString(prefId + nameof(pinned), pinnedPaths);
        string historyPaths = string.Join("|", history.Select(x => AssetDatabase.GetAssetPath(x)));
        EditorPrefs.SetString(prefId + nameof(history), historyPaths);
    }

    private void LoadHistoryFromEditorPrefs()
    {
        string[] pinnedPaths = EditorPrefs.GetString(prefId + nameof(pinned)).Split('|');
        foreach (var path in pinnedPaths)
            pinned.Add(AssetDatabase.LoadMainAssetAtPath(path));
        string[] historyPaths = EditorPrefs.GetString(prefId + nameof(history)).Split('|');
        foreach (var path in historyPaths)
            history.Add(AssetDatabase.LoadMainAssetAtPath(path));
    }

    private void DrawAssetRow(Rect rect, int rowHeight, Object asset, bool hover, bool selected, bool pinned)
    {
        Color oldColor = GUI.backgroundColor;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(rowHeight, rowHeight));
        bool isDragged = DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences.Contains(asset);

        if (hover && selected) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        if (selected) Styles.selectionStyle.Draw(rect, false, false, true, true);
        if ((hover || isDragged) && !selected) Styles.selectionStyle.Draw(rect, false, false, false, false);

        var style = Styles.lineStyle;
        var oldPadding = style.padding.right;

        GUIContent content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
        if (pinned) style.padding.right += rowHeight;
        style.Draw(rect, content, false, false, selected, true);
        if (pinned)
        {
            var pinnedIconContent = EditorGUIUtility.IconContent("Favorite On Icon");
            Rect pinnedIconRect = new Rect(rect.xMax - 2 * rowHeight, rect.yMax - rowHeight, rowHeight, rowHeight);
            EditorStyles.label.Draw(pinnedIconRect, pinnedIconContent, false, false, true, true);
        }

        style.padding.right = oldPadding;
        EditorGUIUtility.SetIconSize(oldIconSize);
        GUI.backgroundColor = oldColor;
    }

    private void DrawPingButton(Rect rect, int rowHeight, Object asset)
    {
        Color oldBackgroundColor = GUI.backgroundColor;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(rowHeight / 2 + 3, rowHeight / 2 + 3));

        var pingButtonContent = EditorGUIUtility.IconContent("HoloLensInputModule Icon");
        pingButtonContent.tooltip = AssetDatabase.GetAssetPath(asset);

        if (IsComponent(asset)) GUI.backgroundColor = new Color(1f, 1.5f, 1f);
        if (!IsAsset(asset)) pingButtonContent = EditorGUIUtility.IconContent("GameObject Icon");

        if (GUI.Button(rect, pingButtonContent, Styles.pingButtonStyle))
        {
            if (Event.current.button == 0) EditorGUIUtility.PingObject(asset);
            else if (Event.current.button == 1) OpenPropertyEditor(asset);
        }

        EditorGUIUtility.SetIconSize(oldIconSize);
        GUI.backgroundColor = oldBackgroundColor;
    }

    private void DrawDragInsertionLine(Rect fullRect)
    {
        Rect lineRect = new Rect(fullRect.x, fullRect.y - 4, fullRect.width, 3);
        GUI.Label(lineRect, GUIContent.none, Styles.insertion);
    }

    private static void SetStyles()
    {
        if (!Styles.areStylesSet)
        {
            Styles.lineStyle = new GUIStyle(Styles.lineStyle);
            Styles.lineStyle.alignment = TextAnchor.MiddleLeft;
            Styles.lineStyle.padding.right += rowHeight;
            Styles.pingButtonStyle = new GUIStyle(GUI.skin.button);
            Styles.pingButtonStyle.padding = new RectOffset(1, 0, 0, 0);
            Styles.pingButtonStyle.alignment = TextAnchor.MiddleCenter;
            Styles.areStylesSet = true;
        }
    }

    private static void OpenPropertyEditor(Object asset)
    {
        string windowTypeName = "UnityEditor.PropertyEditor";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        MethodInfo builderMethod = windowType.GetMethod("OpenPropertyEditor", BindingFlags.Static | BindingFlags.NonPublic);
        builderMethod.Invoke(null, new object[] { asset , true});
    }

    [UnityEditor.ShortcutManagement.Shortcut("PropertyEditor/AssetsHistoryOpenMouseOver")]
    private static void OpenPropertyEditorHoverItem()
    {
        var window = GetWindow(typeof(AssetsHistory), false, "Assets History") as AssetsHistory;
        if (window && window.hoverObject) OpenPropertyEditor(window.hoverObject);
        else
        {
            string windowTypeName = "UnityEditor.PropertyEditor";
            var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
            MethodInfo builderMethod = windowType.GetMethod("OpenHoveredItemPropertyEditor", BindingFlags.Static | BindingFlags.NonPublic);
            builderMethod.Invoke(null, new object[] { null });
        }
    }

    protected static void OpenHierarchyContextMenu(int itemID)
    {
        string windowTypeName = "UnityEditor.SceneHierarchyWindow";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        EditorWindow window = GetWindow(windowType);
        FieldInfo sceneField = windowType.GetField("m_SceneHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
        var sceneHierarchy = sceneField.GetValue(window);

        string hierarchyTypeName = "UnityEditor.SceneHierarchy";
        var hierarchyType = typeof(Editor).Assembly.GetType(hierarchyTypeName);
        MethodInfo builderMethod = hierarchyType.GetMethod("ItemContextClick", BindingFlags.Instance | BindingFlags.NonPublic);
        builderMethod.Invoke(sceneHierarchy, new object[] { itemID });
    }

    protected static void OpenObjectContextMenu(Rect rect, Object obj)
    {
        var classType = typeof(EditorUtility);
        MethodInfo builderMethod =
            classType.GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic, null,
            new Type[] { typeof(Rect), typeof(Object), typeof(int)}, null);
        builderMethod.Invoke(null, new object[] { rect, obj, 0 });
    }

    private static int Mod(int x, int m)
    {
        return (x % m + m) % m; // Always positive modulus
    }

    protected static bool IsComponent(Object obj)
    {
        return obj is Component;
    }

    protected static bool IsAsset(Object obj)
    {
        return AssetDatabase.Contains(obj);
    }

    protected static bool IsNonAssetGameObject(Object obj)
    {
        return !IsAsset(obj) && obj is GameObject;
    }
}

public class HierarchyHistory : AssetsHistory
{
    [MenuItem("Window/Hierarchy History")]
    private static void CreateHierarchyHistory()
    {
        var window = GetWindow(typeof(HierarchyHistory), false, "Hierarchy History") as HierarchyHistory;
        window.minSize = new Vector2(100, rowHeight + 1);
        window.Show();
    }

    protected override void Awake() { }

    protected override void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChange;
        Selection.selectionChanged += SelectionChange;
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        LimitAndOrderHistory();
    }
    protected override void SelectionChange()
    {
        foreach (var t in Selection.transforms)
        {
            lastGlobalSelectedObject = t.gameObject;
            AddToHistory(t.gameObject);
            LimitAndOrderHistory();
        }
    }
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
