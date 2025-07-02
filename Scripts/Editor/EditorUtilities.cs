using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class EditorUtilities
{
    [MenuItem("Editor/Recompile Scripts _F5")]
    public static void RecompileScripts()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }

    [MenuItem("Editor/Recompile Scripts Clean &F5")]
    public static void RecompileScriptsClean()
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
    }

    [MenuItem("Editor/Open Editor Log")]
    public static void OpenEditorLog()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string VSCodePath = localAppData + "/Programs/Microsoft VS Code/Code.exe";
        Debug.Log(VSCodePath + " \"" + localAppData + "/Unity/Editor/Editor.log\"");
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
    private static bool isLooping = false;
    private static string PrefId => PlayerSettings.companyName + "." + PlayerSettings.productName + ".EpsilonDelta.EditorLoop";

    [InitializeOnLoadMethod]
    public static void LoadSetting()
    {
        isLooping = EditorPrefs.GetBool(PrefId, false);
        if (isLooping) EditorApplication.update += QueryUpdate;
        Application.runInBackground = isLooping;
    }

    [MenuItem("Editor/Loop _F7")]
    public static void Loop()
    {
        isLooping = !isLooping;
        if (isLooping) EditorApplication.update += QueryUpdate;
        else EditorApplication.update -= QueryUpdate;
        //Application.runInBackground = isLooping; // This is not necessary and changes ProjectSettings
        EditorPrefs.SetBool(PrefId, isLooping);
    }

    [MenuItem("Editor/Loop _F7", true)]
    private static bool LoopValidate()
    {
        Menu.SetChecked("Editor/Loop", isLooping);
        return true;
    }

    public static void QueryUpdate()
    {
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
