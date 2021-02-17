using System.IO;
using UnityEditor;
using UnityEngine;

namespace Modules.Editor
{
    public class PersistentDataManager : EditorWindow
    {
        [MenuItem("Tools/Persistent Data Manager")]
        public static void ShowWindow()
        {
            Rect rect = new Rect(Screen.width / 2f, Screen.height / 2f, 220, 100);
            var window = GetWindow<PersistentDataManager>(title: "Persistent Data Manager");
            window.position = rect;
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Persistent Data Path (Application.persistentDataPath)", EditorStyles.boldLabel);
            GUILayout.TextField(Application.persistentDataPath);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Folder"))
            {
                EditorUtility.RevealInFinder(Application.persistentDataPath);
            }
            if (GUILayout.Button("Delete Contents"))
            {
                if (EditorUtility.DisplayDialog("Persistent Data Manager", "Delete all files in the persistent data folder? This cannot be undone.", "Yes", "Cancel"))
                {
                    var directoryInfo = new DirectoryInfo(Application.persistentDataPath);

                    foreach (var file in directoryInfo.GetFiles())
                        file.Delete();
                    foreach (var dir in directoryInfo.GetDirectories())
                        dir.Delete(true);
                    EditorUtility.DisplayDialog("Persistent Data Manager", "All folder contents were deleted.", "OK");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("PlayerPrefs", EditorStyles.boldLabel);
            if (GUILayout.Button("Delete all PlayerPrefs"))
            {
                if (EditorUtility.DisplayDialog("Persistent Data Manager", "Delete all PlayerPrefs? This cannot be undone.", "Yes", "Cancel"))
                {
                    PlayerPrefs.DeleteAll();
                    EditorUtility.DisplayDialog("Persistent Data Manager", "All PlayerPrefs were deleted.", "OK");
                }
            }
        }
    }
}
