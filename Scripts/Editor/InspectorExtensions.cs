using System;
using System.IO;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class InspectorExtensions
{
    [MenuItem("CONTEXT/RectTransform/Anchors to Corners")]
    public static void AnchorsToCorners(MenuCommand command)
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
    public static void CornersToAnchors(MenuCommand command)
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

    [Shortcut("Anchors To Corners", KeyCode.T, ShortcutModifiers.Alt)]
    public static void AnchorsToCornersGlobal()
    {
        AnchorsToCorners(null);
    }

    [Shortcut("MakeScreenshot", KeyCode.R, ShortcutModifiers.Alt, displayName = "Make ScreenShot")]
    public static void Screenshot()
    {
        Screenshot(null);
    }

    [MenuItem("CONTEXT/Camera/Screenshot")]
    public static void Screenshot(MenuCommand command)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Screenshots"))
        {
            AssetDatabase.CreateFolder("Assets", "Screenshots");
        }
        var path = $"Assets/Screenshots/Screenshot_{DateTime.Now:yyyy-MM-dd-HH_mm_ss}.png";
        ScreenCapture.CaptureScreenshot(path);
        var timerStart = DateTime.Now;
        EditorApplication.update += Refresh;

        void Refresh()
        {
            if (timerStart.AddSeconds(0.5f) < DateTime.Now)
            {
                EditorApplication.update -= Refresh;
                AssetDatabase.ImportAsset(path);
            }
        }
    }
}