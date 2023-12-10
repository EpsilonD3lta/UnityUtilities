using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

public class EditorUtilities
{
    [MenuItem("Editor/Recompile Scripts")]
    public static void RecompileScripts()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }

    [MenuItem("Editor/Recompile Scripts Clean")]
    public static void RecompileScriptsClean()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
    }

    [MenuItem("Editor/Open Editor Log")]
    public static void OpenEditorLog()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string VSCodePath = localAppData + "/Programs/Microsoft VS Code/Code.exe";
        Debug.Log(VSCodePath + " \"" + localAppData + "/Unity/Editor.log\"");
        ProcessStartInfo process = new ProcessStartInfo(
            VSCodePath, " \"" + localAppData + "/Unity/Editor/Editor.log\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(process);
    }
}

public class EditorLoopUpdater
{
    private static bool isUpdating = false;

    [MenuItem("Editor/Loop")]
    public static void Loop()
    {
        if (!isUpdating) EditorApplication.update += QueryUpdate;
        Application.runInBackground = true;
        isUpdating = true;
    }

    [MenuItem("Editor/StopLoop")]
    public static void StopLoop()
    {
        EditorApplication.update -= QueryUpdate;
        isUpdating = false;
    }

    public static void QueryUpdate()
    {
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
