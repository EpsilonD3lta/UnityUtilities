using System.Reflection;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public class MyShortcuts
{
    [Shortcut("Delete Alternative", KeyCode.X)]
    public static void DeleteAlternative()
    {
        EditorApplication.ExecuteMenuItem("Edit/Delete");
    }

    [Shortcut("Mute Game View Audio", KeyCode.M)]
    public static void MuteGameViewAudio()
    {
        EditorUtility.audioMasterMute = !EditorUtility.audioMasterMute;
    }

    [Shortcut("Lock Inspector", KeyCode.C, ShortcutModifiers.Alt)]
    public static void ToggleInspectorLock()
    {
        ActiveEditorTracker.sharedTracker.isLocked = !ActiveEditorTracker.sharedTracker.isLocked;
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    [Shortcut("Lock Project Tab", KeyCode.V, ShortcutModifiers.Alt)]
    public static void ToggleProjectTabLock()
    {
        var unityEditorAssembly = Assembly.GetAssembly(typeof(Editor));
        var projectBrowserType = unityEditorAssembly.GetType("UnityEditor.ProjectBrowser");
        var projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
        var isLockedProperty = projectBrowserType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var p in projectBrowsers)
        {
            var isLockedOldValue = (bool)isLockedProperty.GetValue(p);
            isLockedProperty.SetValue(p, !isLockedOldValue);

            EditorWindow pw = (EditorWindow)p;
            pw.Repaint();
        }
    }
}
