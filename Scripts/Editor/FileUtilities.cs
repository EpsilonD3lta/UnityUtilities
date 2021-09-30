using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

// Menu item shortcuts: % == ctrl, # == shift, & == alt, _ == no modifier, LEFT, RIGHT, UP, DOWN, F1..F12, HOME, END, PGUP, PGDN
public class FileUtilities : Editor
{

    [MenuItem("Assets/Recompile Scripts")]
    public static void RecompileScripts()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }

    [MenuItem("Assets/File/Copy GUID %#c")]
    public static void CopyGuid()
    {
        if (Selection.assetGUIDs.Length > 0)
        {
            string guid = Selection.assetGUIDs[0];
            GUIUtility.systemCopyBuffer = guid;
            string assetName = Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid));
            UnityEngine.Debug.Log($"{assetName} GUID copied to clipboard: {guid}");
        }
    }

    private static string VisualStudioPath = "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/Common7/IDE/devenv.exe";

    [MenuItem("Assets/File/Open as Textfile")]
    public static void OpenAsTextfile()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            OpenAsTextfile(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    public static void OpenAsTextfile(string path)
    {
        ProcessStartInfo process = new ProcessStartInfo(VisualStudioPath, "/edit \"" + path + "\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(process);
    }

    [MenuItem("Assets/File/Open Metafile")]
    public static void OpenMetafile()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            OpenMetafile(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    public static void OpenMetafile(string path)
    {
        ProcessStartInfo process = new ProcessStartInfo(VisualStudioPath, "/edit \"" + path + ".meta\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(process);
    }

    private static string GExtensionsPath = "C:/Program Files (x86)/GitExtensions/GitExtensions.exe";

    [MenuItem("Assets/File/File History GE  %&h")]
    public static void FileHistoryGitExtensions()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Remove "Assets" at the end of Application.dataPath, because asset contains Assets or Packages at the beginning
            path = Application.dataPath.Substring(0, Application.dataPath.Length - 6) + path;
            UnityEngine.Debug.Log(path);
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
        foreach (string guid in Selection.assetGUIDs)
        {
            OpenInGimp(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    public static void OpenInGimp(string path)
    {
        if (string.IsNullOrEmpty(GIMPPath))
        {
            GIMPPath = Directory.GetFiles(GIMPBinFolderPath, "*.exe").FirstOrDefault(x => Regex.IsMatch(x, @"gimp-[0-9]+"));
            if (string.IsNullOrEmpty(GIMPPath)) return;
        }
        ProcessStartInfo process = new ProcessStartInfo(GIMPPath, "\"" + path + "\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(process);
    }

    // Inspired by https://blog.kikicode.com/2018/12/double-click-fbx-files-to-import-to.html
    private static string BlenderPath = "C:/Program Files/Blender Foundation/Blender 2.91/blender.exe";

    [MenuItem("Assets/File/Open FBX in Blender")]
    public static void OpenFBXInBlender()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            OpenFBXInBlender(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    public static void OpenFBXInBlender(string path)
    {
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

    private static string CygwinPath = "C:/cygwin64/bin/mintty.exe";
    [MenuItem("Assets/File/Open Cygwin here")]
    public static void OpenCygwinHere()
    {
        foreach (string guid in Selection.assetGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path))
            {
                path = path.Substring(0, path.LastIndexOf('/') + 1);
            }
            path = "\"" + Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length) + path + "\"";
            UnityEngine.Debug.Log($"Opening Cygwin in: {path}");
            ProcessStartInfo process = new ProcessStartInfo(CygwinPath, "/bin/sh -lc 'cd " + path + "; exec bash'")
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
            OpenAsTextfile(AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID)));
            return true;
        }
        else if (Event.current.modifiers == EventModifiers.Shift)
        {
            OpenMetafile(AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject(instanceID)));
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
    public static bool OnOpenFolder(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            EditorUtility.RevealInFinder(assetPath);
            return true;
        }
        else return false;
    }

    [OnOpenAsset(2)]
    public static bool OnOpenFBX(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
        {
            OpenFBXInBlender(assetPath);
            return true;
        }
        else return false;
    }

    [OnOpenAsset(3)]
    public static bool OnOpenImage(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (Regex.IsMatch(assetPath, @".*\.png$|.*\.jpg$|.*\.jpeg$", RegexOptions.IgnoreCase))
        {
            OpenInGimp(assetPath);
            return true;
        }
        else return false;
    }

    [OnOpenAsset(4)]
    public static bool OnOpenText(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        // Last expression of the regex is for files without '.' in the name == no file extension
        if (Regex.IsMatch(assetPath, @".*\.txt$|.*\.json$|.*\.md$|.*\.java$|.*\.mm$|^([^.]+)$", RegexOptions.IgnoreCase))
        {
            OpenAsTextfile(assetPath);
            return true;
        }
        else return false;
    }

    // This should fold out/in folders on double click
    // Does not work in two columns layout, maybe could be invoked from EditorApplication.update
    public static bool OnOpenFolder2(int instanceID, int line)
    {
        Object asset = EditorUtility.InstanceIDToObject(instanceID);
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            int[] expandedFolders = InternalEditorUtility.expandedProjectWindowItems;
            bool isExpanded = expandedFolders.Contains(instanceID);
            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            if (focusedWindow != null)
            {
                focusedWindow.SendEvent(new Event
                {
                    keyCode = isExpanded ? KeyCode.LeftArrow : KeyCode.RightArrow,
                    type = EventType.KeyDown,
                    alt = Event.current.modifiers == EventModifiers.Alt
                });
            }
            return true;
        }
        else return false;
    }
}
