using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.Expando;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

public class EditorHelper
{
    #region Reflection
    public static void OpenPropertyEditor(Object obj)
    {
        string windowTypeName = "UnityEditor.PropertyEditor";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        MethodInfo builderMethod = windowType.GetMethod("OpenPropertyEditor",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(Object), typeof(bool) },
            null
            );
        builderMethod.Invoke(null, new object[] { obj, true });
    }

    [Shortcut("PropertyEditor/MyEditorWindowOpenMouseOver", KeyCode.Menu, ShortcutModifiers.Alt)]
    public static void OpenPropertyEditorHoverItem()
    {
        var windows = Resources.FindObjectsOfTypeAll<MyEditorWindow>();
        foreach (var window in windows)
        {
            if (window.hoverObject)
            {
                OpenPropertyEditor(window.hoverObject);
                return;
            }
        }
        string windowTypeName = "UnityEditor.PropertyEditor";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        MethodInfo builderMethod = windowType.GetMethod("OpenHoveredItemPropertyEditor",
            BindingFlags.Static | BindingFlags.NonPublic);
        builderMethod.Invoke(null, new object[] { null });
    }

    public static void OpenHierarchyContextMenu(int itemID)
    {
        string windowTypeName = "UnityEditor.SceneHierarchyWindow";
        var windowType = typeof(Editor).Assembly.GetType(windowTypeName);
        EditorWindow window = EditorWindow.GetWindow(windowType);
        FieldInfo sceneField = windowType.GetField("m_SceneHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
        var sceneHierarchy = sceneField.GetValue(window);

        string hierarchyTypeName = "UnityEditor.SceneHierarchy";
        var hierarchyType = typeof(Editor).Assembly.GetType(hierarchyTypeName);
        MethodInfo builderMethod = hierarchyType.GetMethod("ItemContextClick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        builderMethod.Invoke(sceneHierarchy, new object[] { itemID });
    }

    // Component menu
    public static void OpenObjectContextMenu(Rect rect, Object obj)
    {
        var classType = typeof(EditorUtility);
        MethodInfo builderMethod =
            classType.GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic, null,
            new Type[] { typeof(Rect), typeof(Object), typeof(int) }, null);
        builderMethod.Invoke(null, new object[] { rect, obj, 0 });
    }

    public static void ExpandFolder(int instanceID, bool expand)
    {
        int[] expandedFolders = InternalEditorUtility.expandedProjectWindowItems;
        bool isExpanded = expandedFolders.Contains(instanceID);
        if (expand == isExpanded) return;

        var unityEditorAssembly = Assembly.GetAssembly(typeof(Editor));
        var projectBrowserType = unityEditorAssembly.GetType("UnityEditor.ProjectBrowser");
        var projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);

        foreach (var p in projectBrowsers)
        {
            var treeViewControllerType = unityEditorAssembly.GetType("UnityEditor.IMGUI.Controls.TreeViewController");
            FieldInfo treeViewControllerField =
                projectBrowserType.GetField("m_AssetTree", BindingFlags.Instance | BindingFlags.NonPublic);
            // OneColumn has only AssetTree, TwoColumn has also FolderTree
            var treeViewController = treeViewControllerField.GetValue(p);
            if (treeViewController == null) continue;
            var changeGoldingMethod =
                treeViewControllerType.GetMethod("ChangeFolding", BindingFlags.Instance | BindingFlags.NonPublic);
            changeGoldingMethod.Invoke(treeViewController, new object[] { new int[] { instanceID }, expand });
            EditorWindow pw = (EditorWindow)p;
            pw.Repaint();
        }
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

    public static void SelectWithoutFocus(params Object[] objects)
    {
        var unityEditorAssembly = Assembly.GetAssembly(typeof(Editor));
        var projectBrowserType = unityEditorAssembly.GetType("UnityEditor.ProjectBrowser");
        var projectBrowsers = Resources.FindObjectsOfTypeAll(projectBrowserType);
        var isLockedProperty = projectBrowserType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.NonPublic);
        var oldValues = new List<bool>();
        foreach (var p in projectBrowsers)
        {
            oldValues.Add((bool)isLockedProperty.GetValue(p));
            isLockedProperty.SetValue(p, true);
            EditorWindow pw = (EditorWindow)p;
            pw.Repaint();
        }
        Selection.objects = objects;
        EditorApplication.delayCall += () =>
        {
            for (int i = 0; i < projectBrowsers.Length; i++)
            {
                var p = projectBrowsers[i];
                isLockedProperty.SetValue(p, oldValues[i]);

                EditorWindow pw = (EditorWindow)p;
                pw.Repaint();
            }
        };
    }

    public static void OpenObject(Object obj)
    {
        if (IsAsset(obj)) AssetDatabase.OpenAsset(obj);
        else if (IsNonAssetGameObject(obj)) SceneView.lastActiveSceneView.FrameSelected();
    }
    #endregion

    #region Helpers
    public static int Mod(int x, int m)
    {
        return (x % m + m) % m; // Always positive modulus
    }

    public static bool IsComponent(Object obj)
    {
        return obj is Component;
    }

    public static bool IsAsset(Object obj)
    {
        return AssetDatabase.Contains(obj);
    }

    public static bool IsNonAssetGameObject(Object obj)
    {
        return !IsAsset(obj) && obj is GameObject;
    }

    public static bool IsSceneObject(Object obj, out GameObject main)
    {
        if (IsNonAssetGameObject(obj))
        {
            main = (GameObject)obj;
            return true;
        }
        else if (IsComponent(obj) && IsNonAssetGameObject(((Component)obj).gameObject))
        {
            main = ((Component)obj).gameObject;
            return true;
        }
        main = null;
        return false;
    }

    public static bool ArePartOfSameMainAssets(Object asset1, Object asset2)
    {
         return AssetDatabase.GetAssetPath(asset1) == AssetDatabase.GetAssetPath(asset2);
    }

    /// <summary>
    /// Orders string paths in the same order as in Project Tab. Folders are first at the same level of depth
    /// </summary>
    public class TreeViewComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == y) return 0;
            if (string.IsNullOrEmpty(x)) return 1;
            if (string.IsNullOrEmpty(y)) return -1;
            var xDir = Path.GetDirectoryName(x);
            var yDir = Path.GetDirectoryName(y);
            if (xDir == yDir) return x.CompareTo(y);
            if (yDir.StartsWith(xDir)) return 1; // yDir is subdirectory of xDir, x > y, x after y, yDir will be on top
            if (xDir.StartsWith(yDir)) return -1;
            return x.CompareTo(y);
        }
    }

    public class MyEditorWindow : EditorWindow
    {
        public UnityEngine.Object hoverObject;
    }
    #endregion
}
