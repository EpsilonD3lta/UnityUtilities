using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static EditorHelper;
using static MyGUI;

public class OpenScenes : MyEditorWindow
{
    private static TreeViewComparer treeViewComparer = new();
    private Vector2 scroll;
    private int lastSelectedIndex = -1;
    private List<Object> sceneAssets = new();
    private bool adjustSize = true;

    [MenuItem("File/Open Scenes... %o", false, 160)]
    protected static void CreateWindow()
    {
        var window = GetWindow<OpenScenes>(false, "Open Scenes");
        window.autoRepaintOnSceneChange = true;
        window.minSize = new Vector2(100, 40);
        var scenePaths = AssetDatabase.FindAssets("t:scene", new string[] { "Assets" })
            .Select(x => AssetDatabase.GUIDToAssetPath(x)).OrderBy(x => x, treeViewComparer);
        window.sceneAssets = scenePaths.Select(x => AssetDatabase.LoadMainAssetAtPath(x)).ToList();
    }

    private void OnEnable()
    {
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;
    }

    protected virtual void OnGUI()
    {
        var ev = Event.current;
        if (ev.type == EventType.MouseMove) Repaint();
        if (ev.type == EventType.KeyDown) KeyboardNavigation(
            ev, ref lastSelectedIndex, sceneAssets, enterKey: OnEnterKey, escapeKey: OnEscapeKey);

        bool isAnyHover = false;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < sceneAssets.Count; i++)
        {
            var obj = sceneAssets[i];
            if (obj == null) continue;

            var guiStyle = new GUIStyle(); guiStyle.margin = new RectOffset();
            Rect rect = EditorGUILayout.GetControlRect(false, objectRowHeight, guiStyle);
            var buttonResult = ObjectRow(rect, i, obj, sceneAssets, ref lastSelectedIndex, doubleClick: OnEnterKey);
            if (buttonResult.isHovered) { isAnyHover = true; hoverObject = obj; }
        }
        if (!isAnyHover) hoverObject = null;
        EditorGUILayout.EndScrollView();
        if (adjustSize)
        {
            float height = sceneAssets.Count * objectRowHeight;
            float windowHeight = Mathf.Min(height, 1200f);
            position = new Rect(position.position,
                new Vector2(position.width, windowHeight));
            adjustSize = false;
        }
    }

    private void OnEnterKey()
    {
        if (!docked) Close();
    }

    //protected virtual void OpenScene(string scenePath, EventModifiers eventModifiers)
    //{
    //    if (EditorApplication.isPlaying || string.IsNullOrEmpty(scenePath)) return;

    //    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
    //    {
    //        bool additive = eventModifiers == EventModifiers.Control;
    //        var sceneMode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
    //        EditorSceneManager.OpenScene(scenePath, sceneMode);
    //        Repaint();
    //        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    //    }

    //    if (!docked) Close();
    //}
}
