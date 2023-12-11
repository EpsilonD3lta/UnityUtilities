using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static EditorHelper;
using static MyGUI;

public class AssetDependencies : MyEditorWindow, IHasCustomMenu
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

    private bool showSelected = true;
    private bool showSameName = true; // name without file extension
    private bool isContainsName = false; // name without file extension
    private bool showUses = true;
    private bool isRecursive = false;
    private bool showUsedBy = false;
    private bool searchInScene = false;
    private bool showPackages = false;
    private bool isPackageRecursive = false;
    private bool searchAgain = true;

    private List<string> selectedPaths = new();

    private List<Object> selected = new();
    private List<Object> sameName = new();
    private List<Object> uses = new();
    private List<Object> usedBy = new();
    private List<Object> packagesUses = new();
    private List<Object> shownItems = new List<Object>();
    private int lastSelectedIndex = -1;

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
        window.selected = selectedPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();

        SetAssets(window);
        window.lastSelectedIndex = window.selected.Count - 1;
        window.Show();
    }

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
    }

    public static void SetAssets(AssetDependencies window)
    {
        var selectedPaths = window.selectedPaths;
        var shownItems = new List<Object>();

        shownItems.AddRange(window.selected);

        if (window.showSameName)
        {
            var names = selectedPaths.Select(x => Path.GetFileNameWithoutExtension(x));
            var sameNameGuids = names.SelectMany(x => AssetDatabase.FindAssets(x)).Distinct();
            var sameNamePaths = sameNameGuids
                .Select(x => AssetDatabase.GUIDToAssetPath(x)); // This does Contains
            sameNamePaths = sameNamePaths.Where(x => !x.StartsWith("Packages") && !selectedPaths.Contains(x));

            if (!window.isContainsName)
                sameNamePaths = sameNamePaths
                    .Where(x => names.Contains(Path.GetFileNameWithoutExtension(x)));
            sameNamePaths = sameNamePaths.OrderBy(x => x, treeViewComparer).ToList();
            window.sameName = sameNamePaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(window.sameName);
        }

        if (window.showUses)
        {
            var usesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), window.isRecursive);
            usesPaths = usesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            usesPaths = usesPaths.Where(x => !x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
            window.uses = usesPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(window.uses);
        }

        if (window.showUsedBy)
        {
            if (window.searchAgain)
            {
                var selectedGuids = selectedPaths.Select(x => AssetDatabase.AssetPathToGUID(x));
                var usedByAll = new List<Object>();
                foreach (var selectedGuid in selectedGuids)
                    usedByAll.AddRange(FindAssetUsages.FindAssetUsageFiltered(selectedGuid));
                window.searchAgain = false;
                window.usedBy = usedByAll.Where(x => IsAsset(x))
                    .OrderBy(x => AssetDatabase.GetAssetPath(x), treeViewComparer).ToList();

                if (window.searchInScene)
                    window.usedBy.AddRange(usedByAll.Where(x => !IsAsset(x)));

                window.usedBy = window.usedBy.Distinct().ToList();
                window.adjustSize = true;
                window.Repaint();
            }
            shownItems.AddRange(window.usedBy);
        }

        if (window.showPackages)
        {
            var packagesUsesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), window.isPackageRecursive);
            packagesUsesPaths = packagesUsesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            packagesUsesPaths = packagesUsesPaths.Where(x => x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
            window.packagesUses = packagesUsesPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(window.packagesUses);
        }

        window.shownItems = shownItems;
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

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showSelected, "Selected");
        yPos = 20;
        int i = 0;
        if (showSelected)
        {
            for (int j = 0; j < selected.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
                yPos += rowHeight;
                i++;
            }
        }
        else i = selected.Count;

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showSameName, "SameName");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 16, headerHeight + 2), ref isContainsName, "Contains");

        yPos += 20;
        if (showSameName)
        {
            for (int j = 0; j < sameName.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                string pingButtonContent = uses.Contains(shownItems[i]) ? "U" : "";
                pingButtonContent = usedBy.Contains(shownItems[i]) ? "I" : "" + pingButtonContent;
                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth, pingButtonContent);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showUses, "Uses");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 16, headerHeight + 2), ref isRecursive, "Recursive");
        yPos += 20;
        if (showUses)
        {
            for (int j = 0; j < uses.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showUsedBy, "Is Used By");
        AdditionalToggle(
            new Rect(xPos + headerWidth, yPos, 16, headerHeight + 2), ref searchInScene, "Search in Scene", true);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUI.Button(new Rect(xPos + headerWidth + 16, yPos, 20, headerHeight + 2), searchContent))
        {
            searchAgain = true;
            SetAssets(this);
        }
        yPos += 20;
        if (showUsedBy)
        {
            for (int j = 0; j < usedBy.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                var isHover = ObjectRow(i, obj, xPos, yPos, columnWidth);
                if (isHover) { isAnyHover = true; hoverObject = obj; }
                yPos += rowHeight;
                i++;
            }
        }

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showPackages, "Packages");
        AdditionalToggle(new Rect(xPos + headerWidth, yPos, 16, headerHeight + 2), ref isPackageRecursive, "Recursive");
        yPos += 20;
        if (showPackages)
        {
            for (int j = 0; j < packagesUses.Count; j++)
            {
                var obj = shownItems[i];
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
                RightClick(obj, i);
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
        lastSelectedIndex = i;
        if (ev.modifiers == EventModifiers.Control) // Ctrl select
            if (!isSelected) Selection.objects = Selection.objects.Append(obj).ToArray();
            else Selection.objects = Selection.objects.Where(x => x != obj).ToArray();
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
        else if (ev.keyCode == KeyCode.Return)
        {
            var obj = shownItems.FirstOrDefault(x => Selection.objects.Contains(x));
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

    private void AdditionalToggle(Rect rect, ref bool value, string tooltip, bool searchAgain = false)
    {
        EditorGUI.BeginChangeCheck();
        value = GUI.Toggle(rect, value, new GUIContent("", tooltip));
        if (EditorGUI.EndChangeCheck())
        {
            if (searchAgain) this.searchAgain = true;
            SetAssets(this);
            adjustSize = true;
        }
    }
    #endregion
}