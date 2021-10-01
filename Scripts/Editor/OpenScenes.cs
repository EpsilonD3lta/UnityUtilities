using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class OpenScenes : EditorWindow
{
    private GUIStyle sceneButtonStyle;
    private GUIStyle indexLabelStyle;
    private Vector2 scrollPosition;

    [MenuItem("File/Open Scenes... %o", false, 160)]
    protected static void OpenSceneWindow()
    {
        EditorWindow window = GetWindow<OpenScenes>(false, "Open Scenes");
        window.autoRepaintOnSceneChange = true;
    }

    protected virtual void OnGUI()
    {
        SetStyles();

        int pressedKey = -1;
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if ((e.keyCode >= KeyCode.Alpha0) && (e.keyCode <= KeyCode.Alpha9))
            {
                pressedKey = (e.keyCode - KeyCode.Alpha0);
            }
            if (pressedKey >= 0)
            {
                if ((Event.current.modifiers & EventModifiers.Shift) != 0)
                {
                    pressedKey += 10;
                }
            }
            if (e.keyCode == KeyCode.Escape && !docked) Close();
        }

        var scenePaths = AssetDatabase.FindAssets("t:scene", new string[] { "Assets" }).Select(x => AssetDatabase.GUIDToAssetPath(x)).ToList();

        GUILayout.Space(5);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        {
            if (scenePaths.Count == 0) EditorGUILayout.HelpBox("No scenes found.", MessageType.None);
            else
            {
                for (int i = 0; i < scenePaths.Count; i++)
                {
                    bool shouldOpenScene;
                    string scenePath = scenePaths[i];

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label(i.ToString(), indexLabelStyle, GUILayout.Width(30), GUILayout.Height(25));
                        shouldOpenScene = GUILayout.Button(GetFileNameFromPath(scenePath), sceneButtonStyle, GUILayout.Height(25));
                        var pingButtonContent = EditorGUIUtility.IconContent("HoloLensInputModule Icon");
                        pingButtonContent.tooltip = scenePath;
                        if (GUILayout.Button(pingButtonContent, GUILayout.Width(30), GUILayout.Height(25)))
                        {
                            PingOrSelectScene(scenePath, Event.current.modifiers);
                        }
                    }
                    GUILayout.EndHorizontal();

                    if (i == pressedKey) shouldOpenScene = true;
                    if (shouldOpenScene && !EditorApplication.isPlaying)
                    {
                        OpenScene(scenePath, Event.current.modifiers);
                        Repaint();
                    }
                }
            }
        }
        GUILayout.EndScrollView();
    }

    protected virtual void OpenScene(string scenePath, EventModifiers eventModifiers)
    {
        if (EditorApplication.isPlaying || string.IsNullOrEmpty(scenePath)) return;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            bool additive = eventModifiers == EventModifiers.Control;
            var sceneMode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
            EditorSceneManager.OpenScene(scenePath, sceneMode);
            Repaint();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        if (!docked) Close();
    }

    protected virtual void PingOrSelectScene(string scenePath, EventModifiers eventModifiers)
    {
        var sceneAsset = AssetDatabase.LoadMainAssetAtPath(scenePath);
        if (!sceneAsset) return;

        Object[] selection = null;
        switch (eventModifiers)
        {
            case EventModifiers.Alt: // Select
                selection = new Object[] { sceneAsset };
                break;
            case EventModifiers.Control: // Add to selection
                selection = Selection.objects;
                if (selection == null) selection = System.Array.Empty<Object>();
                var selectionList = selection.ToList();

                if (selectionList.Contains(sceneAsset)) selectionList.Remove(sceneAsset);
                else selectionList.Add(sceneAsset);

                selection = selectionList.ToArray();
                break;
            case EventModifiers.None:
            default:
                EditorGUIUtility.PingObject(sceneAsset);
                break;
        }
        if (selection != null) Selection.objects = selection;
    }

    private string GetFileNameFromPath(string path)
    {
        string name = Path.GetFileName(path);
        int length = name.LastIndexOf('.');
        if (length <= 0) length = name.Length;
        return name.Substring(0, length);
    }

    private void SetStyles()
    {
        if (indexLabelStyle == null || sceneButtonStyle == null)
        {
            indexLabelStyle = new GUIStyle(GUI.skin.label);
            indexLabelStyle.alignment = TextAnchor.MiddleRight;
            sceneButtonStyle = new GUIStyle(GUI.skin.button);
            sceneButtonStyle.alignment = TextAnchor.MiddleLeft;
        }
    }
}
