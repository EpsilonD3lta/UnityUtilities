using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
}
