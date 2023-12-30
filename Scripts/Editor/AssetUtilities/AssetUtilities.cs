using System;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using Object = UnityEngine.Object;
public class AssetUtilities
{
    [MenuItem("Assets/Mark Assets Dirty", priority = 38)]
    public static void MarkDirty()
    {
        foreach (var obj in Selection.objects)
        {
            EditorUtility.SetDirty(obj);
        }
    }

    [MenuItem("Assets/Force Reserialize", priority = 39)]
    public static void ForceReserialize()
    {
        var assetPaths = Selection.assetGUIDs.ToList().Select(x => AssetDatabase.GUIDToAssetPath(x));
        AssetDatabase.ForceReserializeAssets(assetPaths);
    }

    [Shortcut("Save ShaderGraphs", KeyCode.S, ShortcutModifiers.Control | ShortcutModifiers.Shift)]
    public static void SaveShaderGraphs()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(x => x.GetName().Name == "Unity.ShaderGraph.Editor");
        string windowTypeName = "UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow";
        var windowType = assembly.GetType(windowTypeName);
        Object[] shaderGraphWindows = Resources.FindObjectsOfTypeAll(windowType);
        if (shaderGraphWindows != null && shaderGraphWindows.Length != 0)
        {
            foreach (var w in shaderGraphWindows)
            {
                var window = w as EditorWindow;
                window.SaveChanges();
            }
        }

        // Also do regular save
        //EditorApplication.ExecuteMenuItem("File/Save");
    }
}
