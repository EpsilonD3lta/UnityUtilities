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
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("AppData Path (Application.persistentDataPath)", EditorStyles.boldLabel);
        GUILayout.TextField(Application.persistentDataPath);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Show Folder"))
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }
        if (GUILayout.Button("Delete Contents"))
        {
            if (EditorUtility.DisplayDialog("AppData Utility", "Delete all files in the persistent data folder? This cannot be undone.", "Yes", "Cancel"))
            {
                var directoryInfo = new DirectoryInfo(Application.persistentDataPath);

                foreach (var file in directoryInfo.GetFiles())
                    file.Delete();
                foreach (var dir in directoryInfo.GetDirectories())
                    dir.Delete(true);
                Debug.LogWarning("AppData Utility: All folder contents were deleted.");
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("PlayerPrefs", EditorStyles.boldLabel);
        if (GUILayout.Button("Delete all PlayerPrefs"))
        {
            if (EditorUtility.DisplayDialog("AppData Utility", "Delete all PlayerPrefs? This cannot be undone.", "Yes", "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                Debug.LogWarning("AppData Utility: All PlayerPrefs were deleted.");
            }
        }
    }
}
