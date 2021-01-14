using System.Diagnostics;
using UnityEditor;
using UnityEngine;

public class FileUtilities : Editor
{
    [MenuItem("Assets/File/Copy GUID")]
    public static void CopyGuid()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            GUIUtility.systemCopyBuffer = guid;
            UnityEngine.Debug.Log("GUID copied to clipboard: " + guid);
        }
    }

    private static string VisualStudioPath = "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/Common7/IDE/devenv.exe";

    [MenuItem("Assets/File/Open as Textfile")]
    public static void OpenAsTextfile()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProcessStartInfo process = new ProcessStartInfo(VisualStudioPath, "/edit \"" + path + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
    }

    [MenuItem("Assets/File/Open Metafile")]
    public static void OpenMetafile()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProcessStartInfo process = new ProcessStartInfo(VisualStudioPath, "/edit \"" + path + ".meta\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
    }

    private static string GExtensionsPath = "C:/Program Files (x86)/GitExtensions/GitExtensions.exe";

    [MenuItem("Assets/File/File History GE")]
    public static void FileHistoryGitExtensions()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProcessStartInfo process = new ProcessStartInfo(GExtensionsPath, " filehistory \"" + path + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
    }

    private static string GIMPPath = "C:/Program Files/GIMP 2/bin/gimp-2.10.exe";

    [MenuItem("Assets/File/Open In GIMP")]
    public static void OpenInGimp()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ProcessStartInfo process = new ProcessStartInfo(GIMPPath, "\"" + path + "\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
    }
}
