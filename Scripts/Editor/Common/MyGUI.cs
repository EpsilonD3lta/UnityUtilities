using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static EditorHelper;
using System.Linq;
using System.Collections.Generic;
using System;

public static class MyGUI
{
    public const int objectRowHeight = 16;
    public static class Styles
    {
        public static GUIStyle insertion = new("TV Insertion");
        public static GUIStyle lineStyle = new("TV Line");
        public static GUIStyle selectionStyle = new("TV Selection");
        public static GUIStyle pingButtonStyle = new GUIStyle(GUI.skin.button);

        static Styles()
        {
            lineStyle.alignment = TextAnchor.MiddleLeft;
            lineStyle.padding.right += objectRowHeight;
            pingButtonStyle.padding = new RectOffset(2, 0, 0, 1);
            pingButtonStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    private static bool wasDoubleClick;

    public static (bool isHovered, bool isShortRectHovered, bool pingButtonClicked)
        DrawObjectRow(Rect rect, Object obj, bool isSelected, bool isPinned, string pingButtonContent = null)
    {
        var ev = Event.current;

        Rect shortRect = new Rect(rect.x, rect.y, rect.width - rect.height, rect.height);
        Rect pingButtonRect = new Rect(shortRect.xMax, shortRect.yMax - shortRect.height, shortRect.height, shortRect.height);
        bool isHovered = rect.Contains(ev.mousePosition);
        bool isShortRectHovered = shortRect.Contains(ev.mousePosition);

        if (ev.type == EventType.Repaint)
        {
            int height = (int)rect.height;
            Color oldBackGroundColor = GUI.backgroundColor;
            Color oldColor = GUI.contentColor;
            Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(height, height));
            bool isDragged = DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences.Contains(obj);

            if (isHovered && isSelected) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
            if (isSelected) Styles.selectionStyle.Draw(rect, false, false, true, true);
            if ((isHovered || isDragged) && !isSelected) Styles.selectionStyle.Draw(rect, false, false, false, false);

            var style = Styles.lineStyle;
            var oldPadding = style.padding.right;

            GUIContent content = EditorGUIUtility.ObjectContent(obj, obj.GetType());
            bool isAddedGameObject = false;
            if (IsNonAssetGameObject(obj))
            {
                var go = (GameObject)obj;
                if (!go.activeInHierarchy) GUI.contentColor = Color.white * 0.694f;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                    content.image = EditorGUIUtility.IconContent("GameObject Icon").image;
                if (PrefabUtility.IsAddedGameObjectOverride(go)) isAddedGameObject = true;
            }
            if (isPinned) style.padding.right += height;
            style.Draw(rect, content, false, false, isSelected, true);
            GUI.contentColor = oldColor;
            if (isPinned)
            {
                var pinnedIconContent = EditorGUIUtility.IconContent("Favorite On Icon");
                Rect pinnedIconRect = new Rect(rect.xMax - 2 * height, rect.yMax - height, height, height);
                EditorStyles.label.Draw(pinnedIconRect, pinnedIconContent, false, false, true, true);
            }
            if (isAddedGameObject)
            {
                var iconContent = EditorGUIUtility.IconContent("PrefabOverlayAdded Icon");
                Rect iconRect = new Rect(rect.xMin, rect.yMin, height + 5, height);
                EditorStyles.label.Draw(iconRect, iconContent, false, false, true, true);
            }

            style.padding.right = oldPadding;
            EditorGUIUtility.SetIconSize(oldIconSize);
            GUI.backgroundColor = oldBackGroundColor;
        }
        bool pingButtonClicked = DrawPingButton(pingButtonRect, obj, pingButtonContent);
        return (isHovered, isShortRectHovered, pingButtonClicked);
    }

    public static bool DrawPingButton(Rect rect, Object obj, string content = null)
    {
        int height = (int)rect.height;
        Color oldBackgroundColor = GUI.backgroundColor;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(height / 2 + 3, height / 2 + 3));

        var pingButtonContent = EditorGUIUtility.IconContent("HoloLensInputModule Icon");
        if (!string.IsNullOrEmpty(content))
            pingButtonContent = new GUIContent(content);
        pingButtonContent.tooltip = AssetDatabase.GetAssetPath(obj);

        if (IsComponent(obj)) GUI.backgroundColor = new Color(1f, 1.5f, 1f);
        if (!IsAsset(obj)) pingButtonContent = EditorGUIUtility.IconContent("GameObject Icon");

        bool clicked = GUI.Button(rect, pingButtonContent, Styles.pingButtonStyle);

        EditorGUIUtility.SetIconSize(oldIconSize);
        GUI.backgroundColor = oldBackgroundColor;
        return clicked;
    }

    private static void DrawDragInsertionLine(Rect fullRect)
    {
        Rect lineRect = new Rect(fullRect.x, fullRect.y - 4, fullRect.width, 3);
        GUI.Label(lineRect, GUIContent.none, Styles.insertion);
    }

    public static void KeyboardNavigation(Event ev, ref int lastSelectedIndex, List<Object> shownItems,
        Action deleteKey = null, Action enterKey = null, Action escapeKey = null)
    {
        if (ev.keyCode == KeyCode.DownArrow)
        {
            lastSelectedIndex = Mod(lastSelectedIndex + 1, shownItems.Count);
            Selection.objects = new Object[] { shownItems[lastSelectedIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.UpArrow)
        {
            lastSelectedIndex = Mod(lastSelectedIndex - 1, shownItems.Count);
            Selection.objects = new Object[] { shownItems[lastSelectedIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
        {
            var objs = shownItems.Where(x => Selection.objects.Contains(x));
            foreach (var obj in objs)
                OpenObject(obj);
            enterKey?.Invoke();
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Delete)
        {
            deleteKey?.Invoke();
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Escape)
        {
            escapeKey?.Invoke();
            ev.Use();
        }
    }

    public static (bool isHovered, bool isShortRectHovered)
        ObjectRow(Rect rect, int i, Object obj, List<Object> shownItems, ref int lastSelectedIndex,
        string pingButtonContent = null, bool isPinned = false, Action doubleClick = null, Action middleClick = null,
        Action pingButtonMiddleClick = null, Action dragStarted = null, Action dragPerformed = null)
    {
        var ev = Event.current;
        bool isSelected = Selection.objects.Contains(obj);

        var buttonResult = DrawObjectRow(rect, obj, isSelected, isPinned, pingButtonContent);
        if (buttonResult.pingButtonClicked)
        {
            if (Event.current.button == 0)
                PingButtonLeftClick(obj);
            else if (Event.current.button == 1)
                PingButtonRightClick(obj);
            else if (Event.current.button == 2)
                PingButtonMiddleClick(obj, pingButtonMiddleClick);
        }

        if (buttonResult.isShortRectHovered)
        {
            if (ev.type == EventType.MouseUp && ev.button == 0 && ev.clickCount == 1) // Select on MouseUp
            {
                if (!wasDoubleClick)
                    LeftMouseUp(obj, isSelected, i, ref lastSelectedIndex);
                wasDoubleClick = false;
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2)
            {
                DoubleClick(obj);
                wasDoubleClick = true;
            }
            else if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                RightClick(obj, i, ref lastSelectedIndex);

            }
            else if (ev.type == EventType.ContextClick)
            {
                ContextClick(new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), obj);
            }
            else if (ev.type == EventType.MouseDown && ev.button == 2)
            {
                middleClick?.Invoke();
            }
            // Drag
            else if (ev.type == EventType.MouseDrag && ev.button == 0 && // Start dragging this asset
                DragAndDrop.visualMode == DragAndDropVisualMode.None)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(nameof(MyEditorWindow), true);
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                if (isSelected)
                    DragAndDrop.objectReferences = shownItems.Where(x => Selection.objects.Contains(x))
                    .ToArray();
                else DragAndDrop.objectReferences = new Object[] { obj };
                DragAndDrop.StartDrag("MyEditorWindow Drag");
                ev.Use();
                dragStarted?.Invoke();
            }
            else if (ev.type == EventType.DragUpdated && ev.button == 0) // Update drag
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                GUI.Label(rect, GUIContent.none, Styles.insertion);
                ev.Use();
            }
            else if (ev.type == EventType.DragPerform && ev.button == 0) // Receive drag and drop
            {
                dragPerformed?.Invoke();
            }
            // Draw insertion line
            if (isPinned && DragAndDrop.visualMode != DragAndDropVisualMode.None)
            {
                if (ev.modifiers != EventModifiers.Control) // Otherwise  we are trying to move asset to folder
                    DrawDragInsertionLine(rect);
            }
        }
        return (buttonResult.isHovered, buttonResult.isShortRectHovered);

        void LeftMouseUp(Object obj, bool isSelected, int i, ref int lastSelectedIndex)
        {
            lastSelectedIndex = i;
            var ev = Event.current;
            HandleSelection(true);
            ev.Use();
        }

        void DoubleClick(Object obj)
        {
            OpenObject(obj);
            doubleClick?.Invoke();
            ev.Use();
        }

        // This is different event then context click, bot are executed, context after right click
        void RightClick(Object obj, int i, ref int lastSelectedIndex)
        {
            lastSelectedIndex = i;
            HandleSelection(false);
            ev.Use();
        }

        void ContextClick(Rect rect, Object obj)
        {
            if (IsComponent(obj)) OpenObjectContextMenu(rect, obj);
            else if (IsAsset(obj)) EditorUtility.DisplayPopupMenu(rect, "Assets/", null);
            else if (IsNonAssetGameObject(obj))
            {
                if (Selection.transforms.Length > 0) // Just to be sure it's really a HierarchyGameobject
                    OpenHierarchyContextMenu(Selection.transforms[0].gameObject.GetInstanceID());
            }
        }

        void PingButtonLeftClick(Object obj)
        {
            if (Event.current.modifiers == EventModifiers.Alt)
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                obj = AssetDatabase.LoadMainAssetAtPath(path);
                EditorGUIUtility.PingObject(obj);
            }
            else EditorGUIUtility.PingObject(obj);
        }

        void PingButtonRightClick(Object obj)
        {
            OpenPropertyEditor(obj);
        }

        void PingButtonMiddleClick(Object obj, Action pingButtonMiddleClick = null)
        {
            if (Event.current.modifiers == EventModifiers.Alt)
                Debug.Log($"{GlobalObjectId.GetGlobalObjectIdSlow(obj)} InstanceID: {obj.GetInstanceID()}");
            else pingButtonMiddleClick?.Invoke();
        }

        void HandleSelection(bool leftClick)
        {
            if (ev.modifiers == EventModifiers.Control) // Ctrl select
            {
                if (!isSelected) Selection.objects = Selection.objects.Append(obj).ToArray();
                else if (leftClick) Selection.objects = Selection.objects.Where(x => x != obj).ToArray();
            }
            else if (ev.modifiers == EventModifiers.Shift) // Shift select
            {
                int firstSelected = shownItems.FindIndex(x => Selection.objects.Contains(x));
                if (firstSelected != -1)
                {
                    int startIndex = Mathf.Min(firstSelected + 1, i);
                    int count = Mathf.Abs(firstSelected - i);
                    Selection.objects = Selection.objects.
                        Concat(shownItems.GetRange(startIndex, count)).Distinct().ToArray();
                }
                else Selection.objects = Selection.objects.Append(obj).ToArray();
            }
            else if (leftClick || !isSelected)
            {
                Selection.activeObject = obj; // Ordinary select
                Selection.objects = new Object[] { obj };
            }
        }
    }
}
