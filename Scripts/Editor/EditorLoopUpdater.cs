using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
