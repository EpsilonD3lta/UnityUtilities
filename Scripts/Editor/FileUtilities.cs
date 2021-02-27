using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public class FileUtilities : Editor
{
    [MenuItem("Assets/File/Copy GUID %#c")]
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

    private static string GIMPBinFolderPath = "C:/Program Files/GIMP 2/bin/";
    private static string GIMPPath = "";

    [MenuItem("Assets/File/Open In GIMP")]
    public static void OpenInGimp()
    {
        if (string.IsNullOrEmpty(GIMPPath))
        {
            GIMPPath = Directory.GetFiles(GIMPBinFolderPath, "*.exe").FirstOrDefault(x => Regex.IsMatch(x, @"gimp-[0-9]+"));
            if (string.IsNullOrEmpty(GIMPPath)) return;
        }
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

    // Inspired by https://blog.kikicode.com/2018/12/double-click-fbx-files-to-import-to.html
    private static string BlenderPath = "C:/Program Files/Blender Foundation/Blender 2.91/blender.exe";

    [MenuItem("Assets/File/Open FBX in Blender")]
    public static void OpenFBXInBlender()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // r'pathstring' - the parameter r means literal string
            ProcessStartInfo process = new ProcessStartInfo(BlenderPath, " --python-expr  \"import bpy; bpy.context.preferences.view.show_splash = False; bpy.ops.import_scene.fbx(filepath = r'" + path + "'); \"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(process);
        }
    }

    [OnOpenAsset(0)]
    public static bool OnOpenWithModifiers(int instanceID, int line)
    {
        if (Event.current.modifiers == EventModifiers.None) return false;
        if (Event.current.modifiers == EventModifiers.Alt)
        {
            OpenAsTextfile();
            return true;
        }
        else if (Event.current.modifiers == EventModifiers.Shift)
        {
            OpenMetafile();
            return true;
        }
        else if (Event.current.modifiers == (EventModifiers.Alt | EventModifiers.Command)) // Command == Windows key
        {
            Object asset = EditorUtility.InstanceIDToObject(instanceID);
            string assetPath = AssetDatabase.GetAssetPath(asset);
            EditorUtility.RevealInFinder(assetPath);
            return true;
        }
        else return false;
    }

    [OnOpenAsset(1)]
    public static bool OnOpenFBX(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
        {
            OpenFBXInBlender();
            return true;
        }
        else return false;
    }

    [OnOpenAsset(2)]
    public static bool OnOpenImage(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (Regex.IsMatch(assetPath, @".*\.png$|.*\.jpg$|.*\.jpeg$", RegexOptions.IgnoreCase))
        {
            OpenInGimp();
            return true;
        }
        else return false;
    }

    [OnOpenAsset(3)]
    public static bool OnOpenText(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        // Last expression of the regex is for files without '.' in the name == no file extension
        if (Regex.IsMatch(assetPath, @".*\.txt$|.*\.json$|.*\.md$|^([^.]+)$", RegexOptions.IgnoreCase))
        {
            OpenAsTextfile();
            return true;
        }
        else return false;
    }
}
