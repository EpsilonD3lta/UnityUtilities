using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static EditorHelper;
using System.Linq;

public static class MyGUI
{
    public const int objectRowHeight = 16;
    public static class Styles
    {
        public static GUIStyle insertion = "TV Insertion";
        public static GUIStyle lineStyle = "TV Line";
        public static GUIStyle selectionStyle = "TV Selection";
        public static GUIStyle pingButtonStyle;

        static Styles()
        {
            lineStyle = new GUIStyle(lineStyle);
            lineStyle.alignment = TextAnchor.MiddleLeft;
            lineStyle.padding.right += objectRowHeight;
            pingButtonStyle = new GUIStyle(GUI.skin.button);
            pingButtonStyle.padding = new RectOffset(2, 0, 0, 1);
            pingButtonStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    public static (bool isHovered, bool isShortRectHovered, bool pingButtonClicked)
        DrawObjectRow(Rect rect, Object obj, bool isSelected, bool pinned, string pingButtonContent = null)
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
            if (pinned) style.padding.right += height;
            style.Draw(rect, content, false, false, isSelected, true);
            GUI.contentColor = oldColor;
            if (pinned)
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
}
