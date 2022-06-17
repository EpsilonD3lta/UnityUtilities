using UnityEditor;
using UnityEngine;

public class HierarchyHistorySimple : AssetsHistory
{
    [MenuItem("Window/Hierarchy History Simple")]
    private static void CreateHierarchyHistory()
    {
        var window = GetWindow(typeof(HierarchyHistorySimple), false, "Hierarchy History Simple") as HierarchyHistorySimple;
        window.minSize = new Vector2(100, rowHeight + 1);
        window.Show();
    }

    protected override void Awake() { }

    protected override void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChanged;
        Selection.selectionChanged += SelectionChanged;
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        LimitAndOrderHistory();
    }
    protected override void SelectionChanged()
    {
        foreach (var t in Selection.transforms)
        {
            AddHistory(t.gameObject);
            LimitAndOrderHistory();
        }
    }
}
