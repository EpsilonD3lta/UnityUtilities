using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class AssetDependencies : EditorWindow, IHasCustomMenu
{
    private const int rowHeight = 16;
    private static class Styles
    {
        public static GUIStyle insertion = "TV Insertion";
        public static GUIStyle lineStyle = "TV Line";
        public static GUIStyle selectionStyle = "TV Selection";
        public static GUIStyle pingButtonStyle;
        public static GUIStyle foldoutStyle = new GUIStyle();
        public static bool areStylesSet;
    }

    private bool initialized;
    private bool adjustSize;
    private Vector2 scroll = Vector2.zero;
    private float scrollViewRectHeight = 100;

    private bool selected = true;
    private bool sameName = true; // name without file extension
    private bool containsName = false; // name without file extension
    private bool uses = true;
    private bool usedBy = false;
    private bool recursive = false;
    private bool packages = false;
    private bool searchAgain = true;

    private List<string> selectedPaths = new();
    private List<string> sameNamePaths = new();
    private List<string> usesPaths = new();
    private List<string> usedByPaths = new();
    private List<string> packageDependanciesPaths = new();
    private List<string> allItemsPaths = new();

    private List<Object> allItems = new List<Object>();
    private Object hoverObject;

    public class TreeViewComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            var xDir = Path.GetDirectoryName(x);
            var yDir = Path.GetDirectoryName(y);
            if (xDir == yDir) return x.CompareTo(y);
            if (yDir.StartsWith(xDir)) return 1; // yDir is subdirectory of xDir, x > y, x after y, yDir will be on top
            if (xDir.StartsWith(yDir)) return -1;
            return x.CompareTo(y);
        }
    }

    [MenuItem("Window/Asset Dependencies _#F11")]
    private static void CreateWindow()
    {
        AssetDependencies window;
        window = CreateWindow<AssetDependencies>("Asset Dependencies");
        window.minSize = new Vector2(100, rowHeight + 1);

        var selectedPaths = Selection.assetGUIDs.Select(x => AssetDatabase.GUIDToAssetPath(x));
        selectedPaths = selectedPaths.OrderBy(x => x, new TreeViewComparer());

        // Prefab instances in Hierarchy. ExludePrefab does not exclude instances of prefabs, only assets.
        var selectedHierarchy = Selection.GetTransforms(SelectionMode.Unfiltered | SelectionMode.ExcludePrefab)
            .Select(x => x.gameObject);
        selectedHierarchy = selectedHierarchy.Where(x => PrefabUtility.IsAnyPrefabInstanceRoot(x));
        selectedPaths = selectedPaths.Concat(
            selectedHierarchy.Select(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x)));

        window.selectedPaths = selectedPaths.ToList();

        SetAssets(window);
        window.Show();
    }

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
    }

    public static void SetAssets(AssetDependencies window)
    {
        var selectedPaths = window.selectedPaths;
        window.usesPaths = new();
        var allItemsPaths = new List<string>();

        allItemsPaths.AddRange(selectedPaths);

        if (window.sameName)
        {
            var names = selectedPaths.Select(x => Path.GetFileNameWithoutExtension(x));
            var sameNameGuids = names.SelectMany(x => AssetDatabase.FindAssets(x));
            var sameNamePaths = sameNameGuids
                .Select(x => AssetDatabase.GUIDToAssetPath(x)); // This does Contains
            if (!window.containsName)
                sameNamePaths = sameNamePaths
                    .Where(x => names.Contains(Path.GetFileNameWithoutExtension(x)));
            sameNamePaths = sameNamePaths.Where(x => !x.StartsWith("Packages") && !selectedPaths.Contains(x));
            sameNamePaths = sameNamePaths.OrderBy(x => x, new TreeViewComparer()).ToList();
            window.sameNamePaths = sameNamePaths.ToList();
            allItemsPaths.AddRange(sameNamePaths);
        }

        if (window.uses)
        {
            var usesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), window.recursive);
            usesPaths = usesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            window.packageDependanciesPaths = usesPaths.Where(x => x.StartsWith("Packages")).ToList()
                .OrderBy(x => x, new TreeViewComparer()).ToList();
            usesPaths = usesPaths.Where(x => !x.StartsWith("Packages")).ToArray();
            usesPaths = usesPaths.OrderBy(x => x, new TreeViewComparer()).ToArray();
            window.usesPaths = usesPaths.ToList();
            allItemsPaths.AddRange(usesPaths);
        }

        if (window.usedBy)
        {
            if (window.searchAgain)
            {
                var selectedGuids = selectedPaths.Select(x => AssetDatabase.AssetPathToGUID(x));
                var usedBy = new List<Object>();
                foreach (var selectedGuid in selectedGuids)
                    usedBy.AddRange(FindAssetUsages.FindAssetUsage(selectedGuid));
                window.searchAgain = false;
                var usedByPaths = usedBy.Where(x => IsAsset(x)).Select(x => AssetDatabase.GetAssetPath(x)).ToList();
                usedByPaths.OrderBy(x => x, new TreeViewComparer()).ToList();
                window.usedByPaths = usedByPaths;
                window.adjustSize = true;
                window.Repaint();
            }
            allItemsPaths.AddRange(window.usedByPaths);
        }

        if (window.packages)
            allItemsPaths.AddRange(window.packageDependanciesPaths);

        window.allItemsPaths = allItemsPaths;
        window.allItems = allItemsPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
    }

    private void Test()
    {

    }

    private void OnEnable()
    {
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        SetStyles();
        var ev = Event.current; //Debug.Log(ev.type);
        var height = position.height;
        var columnWidth = position.width;
        float xPos = 0, yPos = 0;
        bool isAnyHover = false;

        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown) KeyboardNavigation(ev);

        var scrollRectHeight = height;
        var scrollRectWidth = columnWidth;
        if (scrollViewRectHeight > scrollRectHeight) columnWidth -= 13; // Vertical ScrollBar is visible
        scroll = GUI.BeginScrollView(new Rect(xPos, yPos, scrollRectWidth, scrollRectHeight), scroll, new Rect(0, 0, columnWidth, scrollViewRectHeight));
        yPos = 0;
        float headerWidth = 100; float headerHeight = 16;

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref selected, "Selected");
        yPos = 20;
        int i = 0;
        if (selected)
        {
            for (int j = 0; j < selectedPaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                AddRow(i, obj, xPos, yPos, columnWidth, ref isAnyHover);
                yPos += rowHeight;
                i++;
            }
        }
        else i = selectedPaths.Count;

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref sameName, "SameName");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 100, headerHeight + 2), ref containsName, "Contains");

        yPos += 20;
        if (sameName)
        {
            for (int j = 0; j < sameNamePaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                string pingButtonContent = usesPaths.Contains(allItemsPaths[i]) ? "U" : "";
                pingButtonContent = usedByPaths.Contains(allItemsPaths[i]) ? "I" : "" + pingButtonContent;
                AddRow(i, obj, xPos, yPos, columnWidth, ref isAnyHover, pingButtonContent);
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref uses, "Uses");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 100, headerHeight + 2), ref recursive, "Recursive");
        yPos += 20;
        if (uses)
        {
            for (int j = 0; j < usesPaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                AddRow(i, obj, xPos, yPos, columnWidth, ref isAnyHover);
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref usedBy, "Is Used By");
        yPos += 20;
        if (usedBy)
        {
            for (int j = 0; j < usedByPaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                AddRow(i, obj, xPos, yPos, columnWidth, ref isAnyHover);
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref packages, "Packages");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 100, headerHeight + 2), ref recursive, "Recursive");
        yPos += 20;
        if (packages)
        {
            for (int j = 0; j < packageDependanciesPaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                AddRow(i, obj, xPos, yPos, columnWidth, ref isAnyHover);
                yPos += rowHeight;
                i++;
            }
        }

        GUI.EndScrollView();
        if (!isAnyHover) hoverObject = null;

        scrollViewRectHeight = yPos;
        if (!initialized || adjustSize)
        {
            float windowHeight = Mathf.Min(yPos, 600f);
            if (adjustSize) windowHeight = Mathf.Max(windowHeight, position.height); // Enlarge only
            position = new Rect(position.position,
                new Vector2(position.width, windowHeight));
            initialized = true; adjustSize = false;
        }
    }

    private void AddRow(int i, Object obj,
        float xPos, float yPos, float columnWidth, ref bool isAnyHover, string pingButtonContent = null)
    {
        var ev = Event.current;
        Rect fullRect = new Rect(xPos, yPos, columnWidth, rowHeight);
        Rect shortRect = new Rect(xPos, yPos, columnWidth - rowHeight, rowHeight);
        Rect pingButtonRect = new Rect(shortRect.xMax, shortRect.yMax - shortRect.height, shortRect.height, shortRect.height);
        bool isSelected = Selection.objects.Contains(obj);
        bool isHover = fullRect.Contains(ev.mousePosition);
        bool isShortRectHover = shortRect.Contains(ev.mousePosition);
        if (isHover) isAnyHover = true;
        if (isHover) hoverObject = obj;

        if (ev.type == EventType.Repaint) DrawObjectRow(fullRect, obj, isHover, isSelected, false);
        DrawPingButton(pingButtonRect, obj, false, pingButtonContent);

        if (isShortRectHover)
        {
            // Left button
            if (ev.type == EventType.MouseUp && ev.button == 0 && ev.clickCount == 1) // Select on MouseUp
            {
                if (ev.modifiers == EventModifiers.Control) // Ctrl select
                    if (!isSelected) Selection.objects = Selection.objects.Append(obj).ToArray();
                    else Selection.objects = Selection.objects.Where(x => x != obj).ToArray();
                else if (ev.modifiers == EventModifiers.Shift) // Shift select
                {
                    int firstSelected = allItems.FindIndex(x => Selection.objects.Contains(x));
                    if (firstSelected != -1)
                    {
                        int startIndex = Mathf.Min(firstSelected + 1, i);
                        int count = Mathf.Abs(firstSelected - i);
                        Selection.objects = Selection.objects.
                            Concat(allItems.GetRange(startIndex, count)).Distinct().ToArray();
                    }
                    else Selection.objects = Selection.objects.Append(obj).ToArray();
                }
                else
                {
                    Selection.activeObject = obj; // Ordinary select
                    Selection.objects = new Object[] { obj };
                }

                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2)
            {
                DoubleClick(obj);
                ev.Use();
            }
            // Right button
            else if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                Selection.activeObject = obj;
                ev.Use();
            }
            else if (ev.type == EventType.ContextClick)
            {
                ContextClick(new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), obj);
            }
        }
    }

    private void DoubleClick(Object obj)
    {
        if (IsAsset(obj)) AssetDatabase.OpenAsset(obj);
    }

    private void ContextClick(Rect rect, Object obj)
    {
        Selection.activeObject = obj;
        if (IsComponent(obj)) OpenObjectContextMenu(rect, obj);
        else if (IsAsset(obj)) EditorUtility.DisplayPopupMenu(rect, "Assets/", null);
    }

    private void KeyboardNavigation(Event ev)
    {
        if (ev.keyCode == KeyCode.DownArrow)
        {
            int lastHighlightedIndex = allItems.FindLastIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex + 1, allItems.Count);
            Selection.objects = new Object[] { allItems[selectIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.UpArrow)
        {
            int lastHighlightedIndex = allItems.FindIndex(x => Selection.objects.Contains(x));
            int selectIndex = Mod(lastHighlightedIndex - 1, allItems.Count);
            Selection.objects = new Object[] { allItems[selectIndex] };
            ev.Use();
        }
        else if (ev.keyCode == KeyCode.Return)
        {
            var obj = allItems.FirstOrDefault(x => Selection.objects.Contains(x));
            DoubleClick(obj);
            ev.Use();
        }
    }

    #region Drawing
    private void DrawObjectRow(Rect rect, Object obj, bool hover, bool selected, bool pinned)
    {
        int height = (int)rect.height;
        Color oldBackGroundColor = GUI.backgroundColor;
        Color oldColor = GUI.contentColor;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(height, height));
        bool isDragged = DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences.Contains(obj);

        if (hover && selected) GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
        if (selected) Styles.selectionStyle.Draw(rect, false, false, true, true);
        if ((hover || isDragged) && !selected) Styles.selectionStyle.Draw(rect, false, false, false, false);

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
        style.Draw(rect, content, false, false, selected, true);
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

    private bool DrawPingButton(Rect rect, Object obj, bool isPinned, string content = null)
    {
        int height = (int)rect.height;
        bool clicked = false;
        Color oldBackgroundColor = GUI.backgroundColor;
        Vector2 oldIconSize = EditorGUIUtility.GetIconSize();
        EditorGUIUtility.SetIconSize(new Vector2(height / 2 + 3, height / 2 + 3));

        var pingButtonContent = EditorGUIUtility.IconContent("HoloLensInputModule Icon");
        if (!string.IsNullOrEmpty(content))
            pingButtonContent = new GUIContent(content);
        pingButtonContent.tooltip = AssetDatabase.GetAssetPath(obj);

        if (IsComponent(obj)) GUI.backgroundColor = new Color(1f, 1.5f, 1f);
        if (!IsAsset(obj)) pingButtonContent = EditorGUIUtility.IconContent("GameObject Icon");

        if (GUI.Button(rect, pingButtonContent, Styles.pingButtonStyle))
        {
            if (Event.current.button == 0)
            {
                if (Event.current.modifiers == EventModifiers.Alt) // Add or remove pinned item
                {
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                    obj = AssetDatabase.LoadMainAssetAtPath(path);
                    EditorGUIUtility.PingObject(obj);
                }
                else EditorGUIUtility.PingObject(obj);
            }
            else if (Event.current.button == 1) OpenPropertyEditor(obj);
            else if (Event.current.button == 2)
            {
                if (Event.current.modifiers == EventModifiers.Alt)
                    Debug.Log($"{GlobalObjectId.GetGlobalObjectIdSlow(obj)} InstanceID: {obj.GetInstanceID()}");
                else
                {
                    //clicked = true; // Only return clicked if we change something
                }
            }
        }

        EditorGUIUtility.SetIconSize(oldIconSize);
        GUI.backgroundColor = oldBackgroundColor;
        return clicked;
    }

    private static void SetStyles()
    {
        if (!Styles.areStylesSet)
        {
            Styles.foldoutStyle = new GUIStyle(EditorStyles.miniPullDown);
            Styles.foldoutStyle.alignment = TextAnchor.MiddleLeft;
            Styles.foldoutStyle.padding = new RectOffset(19, 0, 0, 0);
            Styles.lineStyle = new GUIStyle(Styles.lineStyle);
            Styles.lineStyle.alignment = TextAnchor.MiddleLeft;
            Styles.lineStyle.padding.right += rowHeight;
            Styles.pingButtonStyle = new GUIStyle(GUI.skin.button);
            Styles.pingButtonStyle.padding = new RectOffset(2, 0, 0, 1);
            Styles.pingButtonStyle.alignment = TextAnchor.MiddleCenter;
            Styles.areStylesSet = true;
        }
    }

    public void ToggleHeader(Rect rect, ref bool selected, string text)
    {
        var oldBackgroundColor = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = Color.white * 0.3f;
        EditorGUI.BeginChangeCheck();
        selected = GUI.Toggle(rect, selected, text, Styles.foldoutStyle);
        if (EditorGUI.EndChangeCheck())
        {
            SetAssets(this);
            adjustSize = true;
        }
        GUI.backgroundColor = oldBackgroundColor;
    }

    private void AdditionalToggle(Rect rect, ref bool selected, string tooltip)
    {
        EditorGUI.BeginChangeCheck();
        selected = GUI.Toggle(rect, selected, new GUIContent("", tooltip));
        if (EditorGUI.EndChangeCheck())
        {
            SetAssets(this);
            adjustSize = true;
        }
    }
    #endregion

    #region Reflection
    private static void OpenPropertyEditor(Object obj)
    {
        string windowTypeName = "UnityEditor.PropertyEditor";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        MethodInfo builderMethod = windowType.GetMethod("OpenPropertyEditor",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(Object), typeof(bool) },
            null
            );
        builderMethod.Invoke(null, new object[] { obj, true });
    }


    // Component menu
    private static void OpenObjectContextMenu(Rect rect, Object obj)
    {
        var classType = typeof(EditorUtility);
        MethodInfo builderMethod =
            classType.GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic, null,
            new Type[] { typeof(Rect), typeof(Object), typeof(int) }, null);
        builderMethod.Invoke(null, new object[] { rect, obj, 0 });
    }
    #endregion

    #region Helpers
    private static int Mod(int x, int m)
    {
        return (x % m + m) % m; // Always positive modulus
    }

    private static bool IsComponent(Object obj)
    {
        return obj is Component;
    }

    private static bool IsAsset(Object obj)
    {
        return AssetDatabase.Contains(obj);
    }

    private static bool IsNonAssetGameObject(Object obj)
    {
        return !IsAsset(obj) && obj is GameObject;
    }

    private static bool IsSceneObject(Object obj, out GameObject main)
    {
        if (IsNonAssetGameObject(obj))
        {
            main = (GameObject)obj;
            return true;
        }
        else if (IsComponent(obj) && IsNonAssetGameObject(((Component)obj).gameObject))
        {
            main = ((Component)obj).gameObject;
            return true;
        }
        main = null;
        return false;
    }
    #endregion
}