using UnityEditor;
using UnityEngine;

public static class InspectorExtensions
{
    [MenuItem("CONTEXT/RectTransform/Anchors to Corners")]
    static void AnchorsToCorners(MenuCommand command)
    {
        if (Selection.transforms == null || Selection.transforms.Length == 0)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("AnchorsToCorners");
        var undoGroup = Undo.GetCurrentGroup();

        foreach (Transform transform in Selection.transforms)
        {
            RectTransform t = transform as RectTransform;
            RectTransform pt = Selection.activeTransform.parent as RectTransform;
            if (t == null || pt == null) return;

            Undo.RecordObject(t, "AnchorsToCorners");

            Vector2 newAnchorsMin = new Vector2(t.anchorMin.x + t.offsetMin.x / pt.rect.width,
                                                t.anchorMin.y + t.offsetMin.y / pt.rect.height);
            Vector2 newAnchorsMax = new Vector2(t.anchorMax.x + t.offsetMax.x / pt.rect.width,
                                                t.anchorMax.y + t.offsetMax.y / pt.rect.height);
            t.anchorMin = newAnchorsMin;
            t.anchorMax = newAnchorsMax;
            t.offsetMin = t.offsetMax = new Vector2(0, 0);
        }
        Undo.CollapseUndoOperations(undoGroup);
    }

    [MenuItem("CONTEXT/RectTransform/Corners to Anchors")]
    static void CornersToAnchors(MenuCommand command)
    {
        if (Selection.transforms == null || Selection.transforms.Length == 0)
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("CornersToAnchors");
        var undoGroup = Undo.GetCurrentGroup();

        foreach (Transform transform in Selection.transforms)
        {
            RectTransform t = transform as RectTransform;
            if (t == null) continue;

            Undo.RecordObject(t, "CornersToAnchors");
            t.offsetMin = t.offsetMax = new Vector2(0, 0);
        }
        Undo.CollapseUndoOperations(undoGroup);
    }
}