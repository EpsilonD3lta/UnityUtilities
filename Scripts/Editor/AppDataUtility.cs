using System.IO;
using UnityEditor;
using UnityEngine;

public class AppDataUtility : EditorWindow
{
    [MenuItem("Tools/AppData Utility")]
    public static void ShowWindow()
    {
        Rect rect = new Rect(Screen.width / 2f, Screen.height / 2f, 220, 100);
        var window = GetWindow<AppDataUtility>(title: "AppData Utility");
        window.position = rect;
        window.minSize = new Vector2(100, 40);
        window.Show();
    }

    private void OnGUI()
    {
        Event ev = Event.current;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Show Folder"))
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
        GUIContent label = new GUIContent("AppData Path:", "Application.persistentDataPath");
        GUILayout.Label(label, EditorStyles.boldLabel);
        GUILayout.TextField(Application.persistentDataPath);

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Delete Contents"))
        {
            if (ev.modifiers == EventModifiers.Shift ||
                EditorUtility.DisplayDialog("AppData Utility", "Delete all files in the persistent data folder? This cannot be undone.", "Yes", "Cancel"))
            {
                var directoryInfo = new DirectoryInfo(Application.persistentDataPath);

                foreach (var file in directoryInfo.GetFiles())
                    file.Delete();
                foreach (var dir in directoryInfo.GetDirectories())
                    dir.Delete(true);
                Debug.LogWarning("AppData Utility: All folder contents were deleted.");
            }
        }

        if (GUILayout.Button("Delete all PlayerPrefs"))
        {
            if (ev.modifiers == EventModifiers.Shift ||
                EditorUtility.DisplayDialog("AppData Utility", "Delete all PlayerPrefs? This cannot be undone.", "Yes", "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                Debug.LogWarning("AppData Utility: All PlayerPrefs were deleted.");
            }
        }
        GUILayout.EndHorizontal();
    }
}
