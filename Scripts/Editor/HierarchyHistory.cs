using UnityEditor;
using UnityEngine;

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
