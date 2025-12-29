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

    protected override void EditorUpdate()
    {
        if (DragAndDrop.objectReferences.Length != draggedObjectsCount)
        {
            draggedObjectsCount = DragAndDrop.objectReferences.Length;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (!EditorHelper.IsNonAssetGameObject(obj)) continue;
                AddHistory(obj);
                LimitAndOrderHistory();
            }
            Repaint();
        }
    }

    protected override void OnEnable()
    {
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChanged;
        Selection.selectionChanged += SelectionChanged;
        // DragAndDrop is now better handled in Update method
        //DragAndDrop.RemoveDropHandler(OnDragDroppedSceneHandler);
        //DragAndDrop.AddDropHandler(OnDragDroppedSceneHandler);
        //DragAndDrop.RemoveDropHandler(OnDragDroppedHierarchyHandler);
        //DragAndDrop.AddDropHandler(OnDragDroppedHierarchyHandler);
        //DragAndDrop.RemoveDropHandler(OnDragDroppedInspectorHandler);
        //DragAndDrop.AddDropHandler(OnDragDroppedInspectorHandler);
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;

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
