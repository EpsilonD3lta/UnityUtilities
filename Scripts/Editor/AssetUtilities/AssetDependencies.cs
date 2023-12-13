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
    private bool showUsedBy = true;
    private bool searchInScene = true;
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

        window.Select();
        window.SetShownItems();
        window.Show();
    }

    public virtual void AddItemsToMenu(GenericMenu menu)
    {
        menu.AddItem(EditorGUIUtility.TrTextContent("Test"), false, Test);
    }

    private void Select()
    {
        selected.Clear();
        var newSelectedPaths = Selection.assetGUIDs.Select(x => AssetDatabase.GUIDToAssetPath(x));
        newSelectedPaths = newSelectedPaths.OrderBy(x => x, treeViewComparer);

        // Prefab instances in Hierarchy. ExludePrefab does not exclude instances of prefabs, only assets.
        var selectedHierarchy = Selection.GetTransforms(SelectionMode.Unfiltered | SelectionMode.ExcludePrefab)
            .Select(x => x.gameObject);
        selectedHierarchy = selectedHierarchy.Where(x => PrefabUtility.IsAnyPrefabInstanceRoot(x));
        newSelectedPaths = newSelectedPaths.Concat(
            selectedHierarchy.Select(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x)));

        selectedPaths = newSelectedPaths.ToList();
        selected = newSelectedPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
        lastSelectedIndex = selected.Count - 1;
        searchAgain = true;
    }

    private async void SetShownItems()
    {
        sameName.Clear();
        uses.Clear();
        // usedBy are async and cached
        packagesUses.Clear();
        shownItems.Clear();
        shownItems.AddRange(selected);

        if (showSameName)
        {
            var names = selectedPaths.Select(x => Path.GetFileNameWithoutExtension(x));
            var sameNameGuids = names.SelectMany(x => AssetDatabase.FindAssets(x)).Distinct();
            var sameNamePaths = sameNameGuids
                .Select(x => AssetDatabase.GUIDToAssetPath(x)); // This does Contains
            sameNamePaths = sameNamePaths.Where(x => !x.StartsWith("Packages") && !selectedPaths.Contains(x));

            if (!isContainsName)
                sameNamePaths = sameNamePaths
                    .Where(x => names.Contains(Path.GetFileNameWithoutExtension(x)));
            sameNamePaths = sameNamePaths.OrderBy(x => x, treeViewComparer).ToList();
            sameName = sameNamePaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(sameName);
        }

        if (showUses)
        {
            var usesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), isRecursive);
            usesPaths = usesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            usesPaths = usesPaths.Where(x => !x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
            uses = usesPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(uses);
        }

        if (showUsedBy)
        {
            if (searchAgain)
            {
                usedBy.Clear();
                var usedByAll = new List<Object>();
                foreach (var sel in selected)
                    usedByAll.AddRange(await FindAssetUsages.FindObjectUsageAsync(sel, true));
                searchAgain = false;
                usedBy = usedByAll.Where(x => IsAsset(x))
                    .OrderBy(x => AssetDatabase.GetAssetPath(x), treeViewComparer).ToList();

                if (searchInScene)
                    usedBy.AddRange(usedByAll.Where(x => !IsAsset(x)));

                usedBy = usedBy.Distinct().ToList();
                adjustSize = true;
                Repaint();
            }
            shownItems.AddRange(usedBy);
        }

        if (showPackages)
        {
            var packagesUsesPaths = AssetDatabase.GetDependencies(selectedPaths.ToArray(), isPackageRecursive);
            packagesUsesPaths = packagesUsesPaths.Where(x => !selectedPaths.Contains(x)).ToArray();
            packagesUsesPaths = packagesUsesPaths.Where(x => x.StartsWith("Packages"))
                .OrderBy(x => x, treeViewComparer).ToArray();
            packagesUses = packagesUsesPaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
            shownItems.AddRange(packagesUses);
        }
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
        if (ev.type == EventType.KeyDown) KeyboardNavigation(ev, ref lastSelectedIndex, shownItems);

        var scrollRectHeight = height;
        var scrollRectWidth = columnWidth;
        if (scrollViewRectHeight > scrollRectHeight) columnWidth -= 13; // Vertical ScrollBar is visible
        scroll = GUI.BeginScrollView(new Rect(xPos, yPos, scrollRectWidth, scrollRectHeight), scroll, new Rect(0, 0, columnWidth, scrollViewRectHeight));
        yPos = 0;
        float headerWidth = 100; float headerHeight = 16;

        ToggleHeader(new Rect(xPos, yPos, headerWidth, headerHeight), ref showSelected, "Selected");
        GUIContent reselectContent = EditorGUIUtility.IconContent("Grid.Default@2x");
        if (GUI.Button(new Rect(xPos + headerWidth, yPos, 20, headerHeight + 2), reselectContent))
        {
            Select();
            SetShownItems();
        }
        yPos = 20;
        int i = 0;
        if (showSelected)
        {
            for (int j = 0; j < selected.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
                var (isHover, _) = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex);
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
                Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
                var (isHover, _) = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex, pingButtonContent);
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

                Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
                var (isHover, _) = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex);
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
            SetShownItems();
        }
        yPos += 20;
        if (showUsedBy)
        {
            for (int j = 0; j < usedBy.Count; j++)
            {
                var obj = shownItems[i];
                if (obj == null) continue;

                Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
                var (isHover, _) = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex);
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

                Rect rect = new Rect(xPos, yPos, columnWidth, rowHeight);
                var (isHover, _) = ObjectRow(rect, i, obj, shownItems, ref lastSelectedIndex);
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

    #region Drawing
    public void ToggleHeader(Rect rect, ref bool selected, string text)
    {
        var oldBackgroundColor = GUI.backgroundColor;
        if (selected) GUI.backgroundColor = Color.white * 0.3f;
        EditorGUI.BeginChangeCheck();
        selected = GUI.Toggle(rect, selected, text, Styles.foldoutStyle);
        if (EditorGUI.EndChangeCheck())
        {
            SetShownItems();
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
            SetShownItems();
            adjustSize = true;
        }
    }
    #endregion
}