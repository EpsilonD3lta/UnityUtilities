using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MarkAssetsDirty : EditorWindow
{
    [MenuItem("Assets/Mark Assets Dirty", priority = 20)]
    public static void FindAssetUsage()
    {
        foreach (var obj in Selection.objects)
        {
            EditorUtility.SetDirty(obj);
        }
    }
}
