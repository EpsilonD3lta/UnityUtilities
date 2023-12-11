using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static EditorHelper;
using static MyGUI;

public class AssetDependencies : EditorWindow, IHasCustomMenu
{
    private const int rowHeight = objectRowHeight;
    private static class Styles
    {
        public static GUIStyle foldoutStyle = new GUIStyle();
        static Styles()
        {
            foldoutStyle = new GUIStyle(EditorStyles.miniPullDown);
            foldoutStyle.alignment = TextAnchor.MiddleLeft;
            foldoutStyle.padding = new RectOffset(19, 0, 0, 0);
        }
    }

    private static TreeViewComparer treeViewComparer;

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
    private bool packageRecursive = false;
    private bool packages = false;
    private bool searchAgain = true;

    private List<string> selectedPaths = new();
    private List<string> sameNamePaths = new();
    private List<string> usesPaths = new();
    private List<string> usedByPaths = new();
    private List<string> packagesUsesPaths = new();
    private List<string> allItemsPaths = new();

    private List<Object> allItems = new List<Object>();
    private Object hoverObject;

    [MenuItem("Window/Asset Dependencies _#F11")]
    private static void CreateWindow()
    {
        AssetDependencies window;
        window = CreateWindow<AssetDependencies>("Asset Dependencies");
        window.minSize = new Vector2(100, rowHeight + 1);

        var selectedPaths = Selection.assetGUIDs.Select(x => AssetDatabase.GUIDToAssetPath(x));
        selectedPaths = selectedPaths.OrderBy(x => x, treeViewComparer);

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
            var sameNameGuids = names.SelectMany(x => AssetDatabase.FindAssets(x)).Distinct();
            var sameNamePaths = sameNameGuids
                .Select(x => AssetDatabase.GUIDToAssetPath(x)); // This does Contains
            sameNamePaths = sameNamePaths.Where(x => !x.StartsWith("Packages") && !selectedPaths.Contains(x));

            if (!window.containsName)
                sameNamePaths = sameNamePaths
                    .Where(x => names.Contains(Path.GetFileNameWithoutExtension(x)));
            sameNamePaths = sameNamePaths.OrderBy(x => x, treeViewComparer).ToList();
            window.sameNamePaths = sameNamePaths.ToList();
            allItemsPaths.AddRange(sameNamePaths);
        }

        if (window.uses)
        {
            var usesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), window.recursive);
            usesPaths = usesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            usesPaths = usesPaths.Where(x => !x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
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
                usedByPaths = usedByPaths.Distinct().OrderBy(x => x, treeViewComparer).ToList();
                window.usedByPaths = usedByPaths;
                window.adjustSize = true;
                window.Repaint();
            }
            allItemsPaths.AddRange(window.usedByPaths);
        }

        if (window.packages)
        {
            var packagesUsesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), window.packageRecursive);
            packagesUsesPaths = packagesUsesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            packagesUsesPaths = packagesUsesPaths.Where(x => x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
            window.packagesUsesPaths = packagesUsesPaths.ToList();

            allItemsPaths.AddRange(packagesUsesPaths);
        }

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

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
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
                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth, pingButtonContent);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
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

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
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

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref packages, "Packages");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 100, headerHeight + 2), ref packageRecursive, "Recursive");
        yPos += 20;
        if (packages)
        {
            for (int j = 0; j < packagesUsesPaths.Count; j++)
            {
                var obj = allItems[i];
                if (obj == null) continue;

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
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

    private bool ObjectRow(int i, Object obj,
        float xPos, float yPos, float columnWidth, string pingButtonContent = null)
    {
        var ev = Event.current;
        Rect fullRect = new Rect(xPos, yPos, columnWidth, rowHeight);
        bool isSelected = Selection.objects.Contains(obj);

        var buttonResult = DrawObjectRow(fullRect, obj, isSelected, false, pingButtonContent);
        if (buttonResult.pingButtonClicked)
        {
            if (Event.current.button == 0)
                PingButtonLeftClick(obj);
            else if (Event.current.button == 1)
                PingButtonRightClick(obj);
            else if (Event.current.button == 2)
                PingButtonMiddleClick(obj);
        }

        if (buttonResult.isShortRectHovered)
        {
            if (ev.type == EventType.MouseUp && ev.button == 0 && ev.clickCount == 1) // Select on MouseUp
            {
                LeftMouseUp(obj, isSelected, i);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 0 && ev.clickCount == 2)
            {
                DoubleClick(obj);
                ev.Use();
            }
            else if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                RightClick(obj);
                ev.Use();
            }
            else if (ev.type == EventType.ContextClick)
            {
                ContextClick(new Rect(ev.mousePosition.x, ev.mousePosition.y, 0, 0), obj);
            }
        }
        return buttonResult.isHovered;
    }

    private void LeftMouseUp(Object obj, bool isSelected, int i)
    {
        var ev = Event.current;
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
    }

    private void DoubleClick(Object obj)
    {
        if (IsAsset(obj)) AssetDatabase.OpenAsset(obj);
        else if (IsNonAssetGameObject(obj)) SceneView.lastActiveSceneView.FrameSelected();
    }

    // This is different event then context click, bot are executed, context after right click
    private void RightClick(Object obj)
    {
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

    private void PingButtonMiddleClick(Object obj)
    {
        if (Event.current.modifiers == EventModifiers.Alt)
            Debug.Log($"{GlobalObjectId.GetGlobalObjectIdSlow(obj)} InstanceID: {obj.GetInstanceID()}");
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
}