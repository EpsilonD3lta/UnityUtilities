using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// TODO: Save on disk
public class AssetsHistory : EditorWindow, IHasCustomMenu
{
    public Object hoverObject;
    private List<Object> groupedHistory = new List<Object>();
    private List<Object> history = new List<Object>();
    private List<Object> pinned = new List<Object>();
    private Object lastGlobalSelectedObject;
    private Object currentMouseUppedObject;
    private int limit = 10;
    private const int rowHeight = 16;

    private static class Styles
    {
        public static GUIStyle foldout = "IN Foldout";
        public static GUIStyle insertion = "TV Insertion";
        public static GUIStyle ping = "TV Ping";
        public static GUIStyle toolbarButton = "ToolbarButton";
        public static GUIStyle lineStyle = "TV Line";
        public static GUIStyle lineBoldStyle = "TV LineBold";
        public static GUIStyle selectionStyle = "TV Selection";
        public static bool areStylesSet;
    }

    [MenuItem("Window/Assets History")]
    static void CreateWindow()
    {
        var window = GetWindow(typeof(AssetsHistory), false, "Assets History") as AssetsHistory;
        window.Show();
    }

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
    }

    private void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChange;
        Selection.selectionChanged += SelectionChange;
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;
        //EditorApplication.update += Test;
    }

    private void Test()
    {
        Debug.Log("test");
    }

    // This is received only when visible
    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {

        if (!Styles.areStylesSet)
        {
            Styles.lineStyle = new GUIStyle(Styles.lineStyle);
            Styles.lineStyle.alignment = TextAnchor.MiddleLeft;
            Styles.lineStyle.padding.right += rowHeight;
            Styles.areStylesSet = true;
        }
        var ev = Event.current; //Debug.Log(ev.type);
        var height = position.height;
        var width = position.width;
        int lines = Mathf.FloorToInt(height / rowHeight);
        bool shouldLimitAndOrderHistory = false;
        bool isAnyShortRectHover = false;
        hoverObject = null;
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
        float x = 0, y = 0;
        if (limit != lines * 2)
        {
            limit = lines * 2;
            LimitAndOrderHistory();
        }
        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.DownArrow)
        {
            int lastHighlightedIndex = groupedHistory.FindLastIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex + 1, groupedHistory.Count);
            Selection.objects = new Object[] { groupedHistory[selectIndex] };
        }
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.UpArrow)
        {
            int lastHighlightedIndex = groupedHistory.FindIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex - 1, groupedHistory.Count);
            Selection.objects = new Object[] { groupedHistory[selectIndex] };
        }
        if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Return)
        {
            var asset = history.FirstOrDefault(x => Selection.objects.Contains(x));
            AssetDatabase.OpenAsset(asset);
        }
        for (int i = 0; i < groupedHistory.Count; i++)
        {
            var asset = groupedHistory[i];
            if (i == lines)
            {
                x += width / 2;
                y = 0;
            }
            if (asset != null)
            {
                Rect fullRect = new Rect(x, y, width / 2, rowHeight);
                Rect shortRect = new Rect(x, y, width/2 - rowHeight, rowHeight);
                Rect pingButtonRect = new Rect(shortRect.xMax, shortRect.yMax - shortRect.height, shortRect.height, shortRect.height);
                bool selected = Selection.objects.Contains(asset);
                bool isPinned = pinned.Contains(asset);
                bool isHover = fullRect.Contains(ev.mousePosition);
                bool isShortRectHover = shortRect.Contains(ev.mousePosition);
                if (isShortRectHover) isAnyShortRectHover = true;
                if (isHover) hoverObject = asset;
                if (ev.type == EventType.Repaint)
                {
                    DrawAssetRow(fullRect, rowHeight, asset, isHover, selected, isPinned);
                }

                if (shortRect.Contains(ev.mousePosition))
                {
                    if (ev.button == 0)
                    {
                        if (ev.type == EventType.MouseUp && ev.clickCount == 1)
                        {
                            if (ev.modifiers == EventModifiers.Alt)
                            {
                                if (!pinned.Contains(asset)) pinned.Add(asset);
                                else pinned.Remove(asset);
                                shouldLimitAndOrderHistory = true;
                                ev.Use();
                            }
                            else if (ev.modifiers == EventModifiers.Control)
                                if (!Selection.objects.Contains(asset))
                                    Selection.objects = Selection.objects.Append(asset).ToArray();
                                else Selection.objects = Selection.objects.Where(x => x != asset).ToArray();
                            else if (ev.modifiers == EventModifiers.Shift)
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
                            else
                            {
                                Selection.objects = new Object[] { asset };
                                currentMouseUppedObject = asset;
                            }
                            ev.Use();
                        }
                        else if (ev.type == EventType.MouseDown && ev.clickCount == 2)
                        {
                            AssetDatabase.OpenAsset(asset);
                            ev.Use();
                        }
                        else if (ev.type == EventType.MouseDrag && DragAndDrop.objectReferences.Length == 0)
                        {
                            DragAndDrop.PrepareStartDrag();
                            DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                            if (Selection.objects.Contains(asset))
                                DragAndDrop.objectReferences = groupedHistory.Where(x => Selection.objects.Contains(x))
                                .ToArray();
                            else DragAndDrop.objectReferences = new Object[] { asset };
                            DragAndDrop.StartDrag("AssetsHistory Drag");
                            ev.Use();
                        }
                        if (ev.type == EventType.DragUpdated)
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                            GUI.Label(fullRect, GUIContent.none, Styles.insertion);
                        }
                        if (ev.type == EventType.DragPerform && isPinned)
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
                    if (ev.type == EventType.MouseDown && ev.button == 1)
                    {
                        Selection.objects = new Object[] { asset };
                        EditorUtility.DisplayPopupMenu(
                            new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), "Assets/", null);
                        ev.Use();
                    }
                    if (ev.type == EventType.MouseDown && ev.button == 2)
                    {
                        if (ev.modifiers == EventModifiers.Control)
                            if (isPinned) pinned.Clear();
                            else history.Clear();
                        else
                        {
                            history.RemoveAll(x => Selection.objects.Contains(x));
                            pinned.RemoveAll(x => Selection.objects.Contains(x));
                        }
                        shouldLimitAndOrderHistory = true;
                        ev.Use();
                        Repaint();
                    }
                }
                DrawPingButton(pingButtonRect, rowHeight, asset);
                if (isShortRectHover && isPinned && DragAndDrop.visualMode != DragAndDropVisualMode.None)
                {
                    Rect lineRect = new Rect(fullRect.x, fullRect.y -4, fullRect.width, 3);
                    GUI.Label(lineRect, GUIContent.none, Styles.insertion);
                }
                if (!isAnyShortRectHover && i == pinned.Count && DragAndDrop.visualMode != DragAndDropVisualMode.None)
                {
                    Rect lineRect = new Rect(fullRect.x, fullRect.y - 4, fullRect.width, 3);
                    GUI.Label(lineRect, GUIContent.none, Styles.insertion);
                }
                y += rowHeight;
            }
            else history.Remove(asset);
        }
        if (ev.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            ev.Use();
        }
        if (ev.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (!pinned.Contains(obj)) pinned.Add(obj);
                else
                {
                    pinned.Remove(obj);
                    pinned.Add(obj);
                }
            }
            shouldLimitAndOrderHistory = true;
            ev.Use();
        }
        if (shouldLimitAndOrderHistory) LimitAndOrderHistory();
        if (ev.type == EventType.MouseLeaveWindow)
            hoverObject = null;
    }

    private void SelectionChange()
    {
        foreach (var guid in Selection.assetGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            lastGlobalSelectedObject = asset;
            if (!history.Contains(asset)) history.Insert(0, asset);
            else MoveToFront(asset);
            LimitAndOrderHistory();
        }
    }

    private void MoveToFront(Object asset)
    {
        var index = history.IndexOf(asset);
        history.RemoveAt(index);
        history.Insert(0, asset);
    }

    private void LimitAndOrderHistory()
    {
        int onlyPinned = pinned.Where(x => !history.Contains(x)).Count();
        int historyLimit = limit - onlyPinned;
        if (history.Count > historyLimit) history = history.Take(historyLimit).ToList();
        groupedHistory = history.Where(x => !pinned.Contains(x)).OrderBy(x => x.GetType().Name).ThenBy(x => x.name).ToList();
        groupedHistory.InsertRange(0, pinned);
    }

    private void DrawAssetRow(Rect rect, int rowHeight, Object asset, bool hover, bool selected, bool pinned)
    {
        Color oldColor = GUI.backgroundColor;
        bool isDragged = DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences.Contains(asset);
        if (hover && selected) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        if (selected) Styles.selectionStyle.Draw(rect, false, false, true, true);
        if ((hover || isDragged) && !selected) Styles.selectionStyle.Draw(rect, false, false, false, false);
        var style = Styles.lineStyle;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(rowHeight, rowHeight));
        GUIContent content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
        var oldPadding = style.padding.right;
        if (pinned) style.padding.right += rowHeight;
        style.Draw(rect, content, false, false, selected, true);
        if (pinned)
        {
            var pinnedIconContent = EditorGUIUtility.IconContent("Favorite On Icon");
            Rect pinnedIconRect = new Rect(rect.xMax - 2 * rowHeight, rect.yMax - rowHeight, rowHeight, rowHeight);
            EditorStyles.label.Draw(pinnedIconRect, pinnedIconContent, false, false, true, true);
        }
        EditorGUIUtility.SetIconSize(oldIconSize);
        style.padding.right = oldPadding;
        GUI.backgroundColor = oldColor;
    }

    private void DrawPingButton(Rect rect, int rowHeight, Object asset)
    {
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(rowHeight / 2, rowHeight / 2));
        var pingButtonContent = EditorGUIUtility.IconContent("HoloLensInputModule Icon");
        pingButtonContent.tooltip = AssetDatabase.GetAssetPath(asset);
        if (GUI.Button(rect, pingButtonContent))
        {
            if (Event.current.button == 0) EditorGUIUtility.PingObject(asset);
            else if (Event.current.button == 1) OpenPropertyEditor(asset);
        }
        EditorGUIUtility.SetIconSize(oldIconSize);
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

    private static int Mod(int x, int m)
    {
        return (x % m + m) % m;
    }

}
