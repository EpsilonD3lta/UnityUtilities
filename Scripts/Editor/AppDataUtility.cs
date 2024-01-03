using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class AppDataUtility : EditorWindow
{
    [MenuItem("Tools/AppData Utility")]
    public static void ShowWindow()
    {
        Rect rect = new Rect(Screen.width / 2f, Screen.height / 2f, 220, 40);
        var window = GetWindow<AppDataUtility>(title: "AppData Utility");
        window.position = rect;
        window.minSize = new Vector2(100, 40);
        window.Show();
    }

    private void OnGUI()
    {
        Event ev = Event.current;
        GUILayout.BeginHorizontal();
#if UNITY_EDITOR_OSX
        EditorUtility.RevealInFinder(Application.persistentDataPath);
#else
        if (GUILayout.Button(EditorGUIUtility.IconContent("FolderOpened Icon"),
            GUILayout.MaxWidth(40), GUILayout.MaxHeight(17)))
        {
            string assetPath = Application.persistentDataPath;
            assetPath = "\"" + assetPath + "\"";
            assetPath = assetPath.Replace('/', '\\');
            ProcessStartInfo process = new ProcessStartInfo("explorer.exe", assetPath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
#endif
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
                Debug.LogWarning("[AppData Utility] All folder contents were deleted.");
            }
        }

        if (GUILayout.Button("Delete PlayerPrefs"))
        {
            if (ev.modifiers == EventModifiers.Shift ||
                EditorUtility.DisplayDialog("AppData Utility", "Delete all PlayerPrefs? This cannot be undone.", "Yes", "Cancel"))
            {
                PlayerPrefs.DeleteAll();
                Debug.LogWarning("AppData Utility: All PlayerPrefs were deleted.");
            }
        }
        if (GUILayout.Button("Load Backup"))
        {
            if (ev.modifiers == EventModifiers.Shift ||
                EditorUtility.DisplayDialog("AppData Utility", "Load Backup? This cannot be undone.", "Yes", "Cancel"))
            {
                var fromDir = Directory.GetParent(Application.persistentDataPath) + $"/{Application.productName}Backup";
                var toDir = Application.persistentDataPath;
                if (!Directory.Exists(fromDir)) Debug.LogError($"[AppData Utility] {fromDir} does not exist");
                else if (!Directory.Exists(toDir)) Debug.LogError($"[AppData Utility] {toDir} does not exist");
                else
                {
                    CopyFilesRecursively(fromDir, toDir);
                    Debug.Log("[AppData Utility] Backup loaded");
                }

            }
        }
        if (GUILayout.Button("Save Backup"))
        {
            if (EditorUtility.DisplayDialog("AppData Utility", "Save Backup? This cannot be undone.", "Yes", "Cancel"))
            {
                var fromDir = Application.persistentDataPath;
                var toDir = Directory.GetParent(Application.persistentDataPath) + $"/{Application.productName}Backup";
                if (!Directory.Exists(fromDir)) Debug.LogError($"[AppData Utility] {fromDir} does not exist");
                else
                {
                    CopyFilesRecursively(fromDir, toDir);
                    Debug.Log("[AppData Utility] Backup saved");
                }

            }
        }
        GUILayout.EndHorizontal();
    }

    private static void CopyFilesRecursively(string sourcePath, string targetPath)
    {
        // Create all of the directories
        foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
        }

        // Copy all the files & Replaces any files with the same name
        foreach (string newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
        }
    }
}
