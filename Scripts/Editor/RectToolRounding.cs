using UnityEditor;
using UnityEngine;

public class RectToolRounding
{
    public static bool snappingOn = true;
    public static Vector2 anchorMin;
    public static Vector2 anchorMax;

    [InitializeOnLoadMethod]
    public static void Initialize()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    [MenuItem("Editor/UI snapping")]
    public static void SwitchUISnapping()
    {
        snappingOn = !snappingOn;
        Debug.Log($"UI snapping: {snappingOn}");
    }

    public static void OnSceneGui(SceneView sceneView)
    {
        if (!snappingOn) return;
        if (Selection.transforms.Length == 1 && Selection.transforms[0] is RectTransform r)
        {
            // MouseDrag triggers when RectTool is dragged, but not when anchors are dragged
            if ((anchorMin != r.anchorMin || anchorMax != r.anchorMax) && Event.current.type != EventType.MouseDrag) return;
            if (r.drivenByObject != null) return; // When driven by layout groups etc.
            //Debug.Log($"{r.sizeDelta}, {r.offsetMin}, {r.offsetMax}, {r.anchoredPosition}");
            r.sizeDelta = new Vector2(Round(r.sizeDelta.x), Round(r.sizeDelta.y));

            if (r.anchorMin.x != r.anchorMax.x)
            {
                r.offsetMin = new Vector2(Round(r.offsetMin.x), r.offsetMin.y);
                r.offsetMax = new Vector2(Round(r.offsetMax.x), r.offsetMax.y);
            }
            else r.anchoredPosition = new Vector2(Round(r.anchoredPosition.x), r.anchoredPosition.y);
            if (r.anchorMin.y != r.anchorMax.y)
            {
                r.offsetMin = new Vector2(r.offsetMin.x, Round(r.offsetMin.y));
                r.offsetMax = new Vector2(r.offsetMax.x, Round(r.offsetMax.y));
            }
            else r.anchoredPosition = new Vector2(r.anchoredPosition.x, Round(r.anchoredPosition.y));

            anchorMin = r.anchorMin;
            anchorMax = r.anchorMax;
        }
    }

    public static int Round(float x)
    {
        //return Mathf.RoundToInt(x);
        return (int)System.Math.Round(x, 0, System.MidpointRounding.AwayFromZero);
    }

}
