using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class AssetsHistory : EditorWindow
{
    public Object hoverObject;
    private List<Object> groupedHistory = new List<Object>();
    private List<Object> history = new List<Object>();
    private List<Object> pinned = new List<Object>();
    private int limit = 10;

    private static GUIStyle _style;
    private static GUIStyle style
    {
        get
        {
            if (_style == null)
            {
                _style = new GUIStyle(EditorStyles.textField);
                _style.imagePosition = ImagePosition.ImageLeft;
            }
            return _style;
        }
    }

    [MenuItem("Window/Assets History")]
    static void CreateWindow()
    {
        var window = GetWindow(typeof(AssetsHistory), false, "Assets History") as AssetsHistory;
        window.wantsMouseEnterLeaveWindow = true;
        window.Show();
    }

    private void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChange;
        Selection.selectionChanged += SelectionChange;
    }

    // This is received only when visible
    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        var ev = Event.current;
        var height = position.height;
        var width = position.width;
        int rowHeight = 19;
        int lines = Mathf.FloorToInt(height / rowHeight);
        hoverObject = null;
        float x = 0, y = 0;
        if (limit != lines * 2)
        {
            limit = lines * 2;
            LimitAndOrderHistory();
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
                Rect rect = new Rect(x, y, width/2 - rowHeight, rowHeight);
                Rect pingButtonRect = new Rect(rect.xMax, rect.yMax - rect.height, rect.height, rect.height);
                var oldColor = GUI.backgroundColor;
                if (Selection.objects.Contains(asset)) GUI.backgroundColor = new Color(0.5f, 0.5f, 1);
                if (fullRect.Contains(ev.mousePosition)) hoverObject = asset;
                if (ev.type == EventType.Repaint)
                {
                    int id = GUIUtility.GetControlID($"assetHistoryItem{i}".GetHashCode(), FocusType.Keyboard, position);
                    DrawAssetRow(rect, rowHeight, asset, id, rect.Contains(ev.mousePosition), pinned.Contains(asset));
                }
                GUI.backgroundColor = oldColor;
                if (rect.Contains(ev.mousePosition))
                {
                    if (ev.button == 0)
                    {
                        if (ev.type == EventType.MouseUp && ev.clickCount == 1)
                        {
                            if (ev.modifiers == EventModifiers.Alt)
                            {
                                if (!pinned.Contains(asset)) pinned.Insert(0, asset);
                                else pinned.Remove(asset);
                                LimitAndOrderHistory();
                            }
                            else if (ev.modifiers == EventModifiers.Control)
                                if (!Selection.objects.Contains(asset))
                                    Selection.objects = Selection.objects.Append(asset).ToArray();
                                else Selection.objects = Selection.objects.Where(x => x != asset).ToArray();
                            else if (ev.modifiers == EventModifiers.Shift)
                            {
                                int firstSelected = -1;
                                for (int j = 0; j < groupedHistory.Count; j++)
                                    if (Selection.objects.Contains(groupedHistory[j]))
                                    {
                                        firstSelected = j;
                                        break;
                                    }
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
                                Selection.objects = new Object[] { asset };
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
                            DragAndDrop.objectReferences = new Object[] { asset };
                            DragAndDrop.StartDrag("AssetsHistory Drag");
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
                        if (ev.modifiers == EventModifiers.Control) history.Clear();
                        else
                        {
                            history.Remove(asset);
                            pinned.Remove(asset);
                        }
                        LimitAndOrderHistory();
                        ev.Use();
                        Repaint();
                    }

                }
                DrawPingButton(pingButtonRect, rowHeight, asset);
                y += rowHeight;
            }
            else history.Remove(asset);
        }
        if (ev.type == EventType.MouseLeaveWindow)
            hoverObject = null;
    }

    private void SelectionChange()
    {
        if (Selection.assetGUIDs.Length == 1)
        {
            var guid = Selection.assetGUIDs[0];
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
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
        if (history.Count > limit - pinned.Count) history = history.Take(limit - pinned.Count).ToList();
        groupedHistory = history.Where(x => !pinned.Contains(x)).OrderBy(x => x.GetType().Name).ThenBy(x => x.name).ToList();
        groupedHistory.InsertRange(0, pinned);
    }

    private void DrawAssetRow(Rect rect, int rowHeight, Object asset, int controlID, bool hover, bool pinned)
    {
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(rowHeight - 2, rowHeight - 2));
        GUIContent content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
        var oldPadding = style.padding.right;
        if (pinned) style.padding.right = rowHeight;
        style.Draw(rect, content, controlID, false, hover);
        if (pinned)
        {
            var pinnedIconContent = EditorGUIUtility.IconContent("Favorite On Icon");
            Rect pinnedIconRect = new Rect(rect.xMax - rowHeight, rect.yMax - rowHeight, rowHeight, rowHeight);
            EditorStyles.label.Draw(pinnedIconRect, pinnedIconContent, controlID, false, hover);
        }
        EditorGUIUtility.SetIconSize(oldIconSize);
        style.padding.right = oldPadding;
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

}
