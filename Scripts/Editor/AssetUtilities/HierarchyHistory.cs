using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using System.Linq;
using System;
using static EditorHelper;
#if !UNITY_2021_2_OR_NEWER
using UnityEditor.Experimental.SceneManagement; // Out of experimental in 2021.2
#endif
// TODO: optimize, don't save to editorprefs?, inactive gameobjects - change color of text
// added gameobjects to prefabs - change icon at least?
// OnHierarchyChange, check everything and add to scene/prefab history newly instantiated prefab history? - too much overhead?
public class HierarchyHistory : AssetsHistory
{
    protected override string prefId => PlayerSettings.companyName + "." +
    PlayerSettings.productName + ".EpsilonDelta.HierarchyHistory.";

    protected Dictionary<GlobalObjectId, List<GlobalObjectId>> perObjectHistory =
    new Dictionary<GlobalObjectId, List<GlobalObjectId>>();
    protected Dictionary<GlobalObjectId, List<GlobalObjectId>> perObjectPinned =
    new Dictionary<GlobalObjectId, List<GlobalObjectId>>();

    private GlobalObjectId nullGid = default;

    [SerializeField]
    private List<string> perObjectHistoryKeys = new List<string>();
    [SerializeField]
    private List<StringList> perObjectHistoryValues = new List<StringList>();

    [SerializeField]
    private List<string> perObjectPinnedKeys = new List<string>();
    [SerializeField]
    private List<StringList> perObjectPinnedValues = new List<StringList>();

    [Serializable]
    public class StringList
    {
        public List<string> list;
        public StringList(List<string> newList)
        {
            list = newList;
        }

        public static implicit operator StringList(List<string> l) => new StringList(l);
        public static implicit operator List<string>(StringList l) => l.list;
    }

    [MenuItem("Window/Hierarchy History")]
    private static void CreateHierarchyHistory()
    {
        var window = GetWindow(typeof(HierarchyHistory), false, "Hierarchy History") as HierarchyHistory;
        window.minSize = new Vector2(100, rowHeight + 1);
        window.Show();
    }

    public override void AddItemsToMenu(GenericMenu menu)
    {
        base.AddItemsToMenu(menu);
        menu.AddItem(EditorGUIUtility.TrTextContent("Clear All for All Objects"), false, ClearAllForAllObjects);
        menu.AddItem(EditorGUIUtility.TrTextContent("List all objects"), false, ListAllObjects);
    }

    protected override void Test()
    {
        var gos = FindObjectsOfType<Transform>(true);
        var objs = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
            ConvertToUnpackedGid(Parse(
                "GlobalObjectId_V1-2-2fd19a05ecb802843bd51e0f33d4a32b-4983886930841126834-1308519948")));
        Debug.Log(ConvertToUnpackedGid(Parse(
                "GlobalObjectId_V1-2-2fd19a05ecb802843bd51e0f33d4a32b-4983886930841126834-1308519948")));
        Debug.Log(objs, objs);
    }

    protected void ListAllObjects()
    {
        foreach (var obj in perObjectHistory)
        {
            Debug.Log("History Key: " + obj.Key + "\nValues:\n " + string.Join("\n", obj.Value));
        }
        foreach (var obj in perObjectPinned)
        {
            Debug.Log("Pinned Key: " + obj.Key + "\nValues:\n " + string.Join("\n", obj.Value));
        }
    }

    protected override void OnEnable()
    {
        //Debug.Log("custom enable");
        OnAfterDeserialize();
        // This is received even if invisible
        Selection.selectionChanged -= SelectionChanged;
        Selection.selectionChanged += SelectionChanged;
        EditorApplication.hierarchyChanged -= HierarchyChanged;
        EditorApplication.hierarchyChanged += HierarchyChanged;
        PrefabStage.prefabStageOpened -= PrefabStageOpened;
        PrefabStage.prefabStageOpened += PrefabStageOpened;
        PrefabStage.prefabStageClosing -= PrefabStageClosing;
        PrefabStage.prefabStageClosing += PrefabStageClosing;
        EditorSceneManager.sceneOpened -= SceneOpened;
        EditorSceneManager.sceneOpened += SceneOpened;
        EditorApplication.playModeStateChanged -= PlayModeExitted;
        EditorApplication.playModeStateChanged += PlayModeExitted;
        EditorApplication.quitting -= OnBeforeSerialize;
        EditorApplication.quitting += OnBeforeSerialize;
        EditorApplication.quitting -= SaveHistoryToEditorPrefs;
        EditorApplication.quitting += SaveHistoryToEditorPrefs;
        wantsMouseEnterLeaveWindow = true;
        wantsMouseMove = true;

        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage) LoadPrefabHistory(prefabStage);
        else LoadOpenScenesHistory();

        LimitAndOrderHistory();
    }
    protected override void OnDisable()
    {
        OnBeforeSerialize();
    }

    protected override void SelectionChanged()
    {
        foreach (var t in Selection.transforms)
        {
            AddHistory(t.gameObject);
        }
        LimitAndOrderHistory();
    }

    private void HierarchyChanged()
    {
        Repaint();
    }

    private void PlayModeExitted(PlayModeStateChange stateChange)
    {
        if (stateChange == PlayModeStateChange.EnteredEditMode)
        {
            LoadOpenScenesHistory();
            LimitAndOrderHistory();
            Repaint();
        }
    }

    protected override void SceneOpened(Scene scene, OpenSceneMode mode)
    {
        LoadSceneHistory(scene);
        LimitAndOrderHistory();
    }

    private void PrefabStageOpened(PrefabStage prefabStage)
    {
        history.Clear();
        pinned.Clear();
        LoadPrefabHistory(prefabStage);
        LimitAndOrderHistory();
        Repaint();
    }

    private void PrefabStageClosing(PrefabStage prefabStage)
    {
        // If we switch to other prefab, Closing old stage is called after opening new stage
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
        history.Clear();
        pinned.Clear();
        LoadOpenScenesHistory();
        LimitAndOrderHistory();
        Repaint();
    }

    private void LoadPrefabHistory(PrefabStage prefabStage)
    {
        var prefabAsset = AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath);
        var prefabRoot = prefabStage.prefabContentsRoot;
        var prefabGid = GlobalObjectId.GetGlobalObjectIdSlow(prefabAsset);
        var prefabChildren = prefabRoot.GetComponentsInChildren<Transform>(true);
        if (perObjectPinned.ContainsKey(prefabGid))
        {
            var toRemove = new List<GlobalObjectId>();
            foreach (var gid in perObjectPinned[prefabGid])
            {
                var obj = GlobalObjectIdentifierToPrefabObject(prefabChildren, gid);
                if (!obj) obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj) AddToEnd(obj, pinned);
                else toRemove.Add(gid);
            }
            foreach (var gid in toRemove)
            {
                perObjectPinned[prefabGid].Remove(gid);
                EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + gid);
            }
        }
        if (perObjectHistory.ContainsKey(prefabGid))
        {
            var toRemove = new List<GlobalObjectId>();
            foreach (var gid in perObjectHistory[prefabGid])
            {
                var obj = GlobalObjectIdentifierToPrefabObject(prefabChildren, gid);
                if (obj) AddToEnd(obj, history);
                else toRemove.Add(gid);
            }
            foreach (var gid in toRemove)
            {
                perObjectHistory[prefabGid].Remove(gid);
                EditorPrefs.DeleteKey(prefId + nameof(perObjectHistoryValues) + gid);
            }
        }
    }

    private void LoadOpenScenesHistory()
    {
        int countLoaded = EditorSceneManager.sceneCount;
        Scene[] loadedScenes = new Scene[countLoaded];
        for (int i = 0; i < countLoaded; i++)
        {
            loadedScenes[i] = EditorSceneManager.GetSceneAt(i);
        }
        foreach (var scene in loadedScenes)
        {
            LoadSceneHistory(scene);
        }
    }

    private void LoadSceneHistory(Scene scene)
    {
        var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
        AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
        if (perObjectPinned.ContainsKey(sceneGid))
        {
            var toRemove = new List<GlobalObjectId>();
            foreach (var gid in perObjectPinned[sceneGid])
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj) AddToEnd(obj, pinned);
                else toRemove.Add(gid);
            }
            foreach (var gid in toRemove)
            {
                perObjectPinned[sceneGid].Remove(gid);
                EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + gid);
            }
        }
        if (perObjectHistory.ContainsKey(sceneGid))
        {
            var toRemove = new List<GlobalObjectId>();
            foreach (var gid in perObjectHistory[sceneGid])
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                if (obj) AddToEnd(obj, history);
                else toRemove.Add(gid);
            }
            foreach (var gid in toRemove)
            {
                perObjectHistory[sceneGid].Remove(gid);
                EditorPrefs.DeleteKey(prefId + nameof(perObjectHistoryValues) + gid);
            }
        }
    }

    protected override void AddHistory(Object obj)
    {
        var objGid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        if (objGid.ToString() == nullGid.ToString())
        {
            AddToFront(obj, history);
            return;
        }

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        GlobalObjectId prefabGid = new GlobalObjectId();
        if (prefabStage != null)
            prefabGid = GlobalObjectId.GetGlobalObjectIdSlow(AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath));
        if (prefabStage != null)
        {
            if (!perObjectHistory.ContainsKey(prefabGid)) perObjectHistory[prefabGid] = new List<GlobalObjectId>();
            var subPrefabGid = ConstructGid(objGid.identifierType, prefabGid.assetGUID.ToString(), objGid.targetObjectId,
                objGid.targetPrefabId);
            if (!ComparePrefabObjectInstance(prefabGid, objGid))
            {
                AddToFront(obj, history);
                AddToFront(subPrefabGid, perObjectHistory[prefabGid]);
            }
        }
        else if (IsSceneObject(obj, out GameObject go) && !string.IsNullOrEmpty(go.scene.path))
        {
            var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(go.scene.path));
            if (!perObjectHistory.ContainsKey(sceneGid)) perObjectHistory[sceneGid] = new List<GlobalObjectId>();

            AddToFront(obj, history);
            AddToFront(objGid, perObjectHistory[sceneGid]);
        }
    }

    protected override void AddPinned(Object obj, int i = -1)
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        GlobalObjectId prefabGid = new GlobalObjectId();
        if (prefabStage != null)
            prefabGid = GlobalObjectId.GetGlobalObjectIdSlow(AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath));
        var objGid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        bool isNullGid = objGid.ToString() == nullGid.ToString();

        if (prefabStage != null)
        {
            if (!perObjectPinned.ContainsKey(prefabGid)) perObjectPinned[prefabGid] = new List<GlobalObjectId>();

            if (i == -1)
            {
                AddToEnd(objGid, perObjectPinned[prefabGid]);
                AddToEnd(obj, pinned);
            }
            else
            {
                RemovePinned(obj);
                pinned.Insert(i, obj);
                if (!isNullGid) perObjectPinned[prefabGid].Insert(i, objGid);
            }
        }
        else if (IsSceneObject(obj, out GameObject go) && !string.IsNullOrEmpty(go.scene.path))
        {
            var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(go.scene.path));
            if (!perObjectPinned.ContainsKey(sceneGid)) perObjectPinned[sceneGid] = new List<GlobalObjectId>();

            if (i == -1)
            {
                if (!isNullGid) AddToEnd(objGid, perObjectPinned[sceneGid]);
                AddToEnd(obj, pinned);
            }
            else
            {
                RemovePinned(obj);
                pinned.Insert(i, obj);
                perObjectPinned[sceneGid].Insert(i, objGid);
            }
        }
        else
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            if (!perObjectPinned.ContainsKey(sceneGid)) perObjectPinned[sceneGid] = new List<GlobalObjectId>();

            if (i == -1)
            {
                if (!isNullGid) AddToEnd(objGid, perObjectPinned[sceneGid]);
                AddToEnd(obj, pinned);
            }
            else
            {
                RemovePinned(obj);
                pinned.Insert(i, obj);
                if (!isNullGid) perObjectPinned[sceneGid].Insert(i, objGid);
            }
        }
    }

    protected override void AddFilteredPinned(Object obj)
    {
        if (IsNonAssetGameObject(obj)) AddPinned(obj);
    }

    protected override void RemoveHistory(Object obj)
    {
        base.RemoveHistory(obj);
        if (obj == null) return;

        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        if (prefabStage != null)
        {
            GlobalObjectId prefabGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath));

            if (perObjectHistory.ContainsKey(prefabGid))
            {
                perObjectHistory[prefabGid].RemoveAll(x => ComparePrefabObjectInstance(x, gid));
                if (perObjectHistory[prefabGid].Count == 0)
                {
                    perObjectHistory.Remove(prefabGid);
                    EditorPrefs.DeleteKey(prefId + nameof(perObjectHistoryValues) + prefabGid);
                }
            }
        }
        else if (IsSceneObject(obj, out GameObject go) && !string.IsNullOrEmpty(go.scene.path))
        {
            var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(go.scene.path));
            if (perObjectHistory.ContainsKey(sceneGid))
            {
                perObjectHistory[sceneGid].Remove(gid);
                if (perObjectHistory[sceneGid].Count == 0)
                {
                    perObjectHistory.Remove(sceneGid);
                    EditorPrefs.DeleteKey(prefId + nameof(perObjectHistoryValues) + sceneGid);
                }
            }

        }
    }

    protected override void RemoveAllHistory(Predicate<Object> predicate)
    {
        foreach (var obj in history.Where(x => predicate(x)).ToList())
        {
            RemoveHistory(obj);
        }
    }

    protected override void RemovePinned(Object obj)
    {
        base.RemovePinned(obj);
        if (obj == null) return;

        GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();

        if (prefabStage != null)
        {
            GlobalObjectId prefabGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadMainAssetAtPath(prefabStage.assetPath));

            if (perObjectPinned.ContainsKey(prefabGid))
            {
                perObjectPinned[prefabGid].RemoveAll(x => ComparePrefabObjectInstance(x, gid));
                if (perObjectPinned[prefabGid].Count == 0)
                {
                    perObjectPinned.Remove(prefabGid);
                    EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + prefabGid);
                }
            }
        }
        else if (IsSceneObject(obj, out GameObject go) && !string.IsNullOrEmpty(go.scene.path))
        {
            var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(go.scene.path));
            if (perObjectPinned.ContainsKey(sceneGid))
            {
                perObjectPinned[sceneGid].Remove(gid);
                if (perObjectPinned[sceneGid].Count == 0)
                {
                    perObjectPinned.Remove(sceneGid);
                    EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + sceneGid);
                }
            }
        }
        else
        {
            int countLoaded = EditorSceneManager.sceneCount;
            Scene[] loadedScenes = new Scene[countLoaded];
            for (int i = 0; i < countLoaded; i++)
            {
                loadedScenes[i] = EditorSceneManager.GetSceneAt(i);
            }
            foreach (var scene in loadedScenes)
            {
                var sceneGid = GlobalObjectId.GetGlobalObjectIdSlow(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
                if (perObjectPinned.ContainsKey(sceneGid) && perObjectPinned[sceneGid].Contains(gid))
                {
                    perObjectPinned[sceneGid].Remove(gid);
                    if (perObjectPinned[sceneGid].Count == 0)
                    {
                        perObjectPinned.Remove(sceneGid);
                        EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + sceneGid);
                    }
                    break;
                }
            }
        }

    }

    protected override void RemoveAllPinned(Predicate<Object> predicate)
    {
        foreach (var obj in pinned.Where(x => predicate(x)).ToList())
        {
            RemovePinned(obj);
        }
    }

    private void ClearAllForAllObjects()
    {
        ClearAll();
        foreach (var key in perObjectHistory.Keys)
        {
            EditorPrefs.DeleteKey(prefId + nameof(perObjectHistoryValues) + key);
        }
        perObjectHistory.Clear();
        perObjectHistoryKeys.Clear();
        perObjectHistoryValues.Clear();

        foreach (var key in perObjectPinned.Keys)
        {
            EditorPrefs.DeleteKey(prefId + nameof(perObjectPinnedValues) + key);
        }
        perObjectPinned.Clear();
        perObjectPinnedKeys.Clear();
        perObjectPinnedValues.Clear();
        SaveHistoryToEditorPrefs();
    }

    protected override void ClearAll()
    {
        RemoveAllHistory(_ => true);
        RemoveAllPinned(_ => true);
        LimitAndOrderHistory();
    }

    protected override void ClearHistory()
    {
        RemoveAllHistory(_ => true);
        LimitAndOrderHistory();
    }

    protected override void ClearPinned()
    {
        RemoveAllPinned(_ => true);
        LimitAndOrderHistory();
    }

    protected override void SaveHistoryToEditorPrefs()
    {
        //Debug.Log("custom save");
        string pinnedGidKeys = string.Join("|", perObjectPinnedKeys);
        EditorPrefs.SetString(prefId + nameof(perObjectPinnedKeys), pinnedGidKeys);

        for (int i = 0; i < perObjectPinnedKeys.Count; i++)
        {
            if (string.IsNullOrEmpty(perObjectPinnedKeys[i])) continue;
            string pinnedGidValue = string.Join("|", perObjectPinnedValues[i].list);
            EditorPrefs.SetString(prefId + nameof(perObjectPinnedValues) + perObjectPinnedKeys[i], pinnedGidValue);
        }

        string historyGidKeys = string.Join("|", perObjectHistoryKeys);
        EditorPrefs.SetString(prefId + nameof(perObjectHistoryKeys), historyGidKeys);

        for (int i = 0; i < perObjectHistoryKeys.Count; i++)
        {
            if (string.IsNullOrEmpty(perObjectHistoryKeys[i])) continue;
            string historyGidValue = string.Join("|", perObjectHistoryValues[i].list);
            EditorPrefs.SetString(prefId + nameof(perObjectHistoryValues) + perObjectHistoryKeys[i], historyGidValue);
        }
    }

    protected override void LoadHistoryFromEditorPrefs()
    {
        //Debug.Log("custom load");
        perObjectPinnedKeys = EditorPrefs.GetString(prefId + nameof(perObjectPinnedKeys)).Split('|').ToList();
        perObjectPinnedKeys.RemoveAll(x => string.IsNullOrEmpty(x));
        for (int i = 0; i < perObjectPinnedKeys.Count; i++)
        {
            perObjectPinnedValues.Add(
                EditorPrefs.GetString(prefId + nameof(perObjectPinnedValues) + perObjectPinnedKeys[i])
                .Split('|').ToList());
        }

        perObjectHistoryKeys = EditorPrefs.GetString(prefId + nameof(perObjectHistoryKeys)).Split('|').ToList();
        perObjectHistoryKeys.RemoveAll(x => string.IsNullOrEmpty(x));
        for (int i = 0; i < perObjectHistoryKeys.Count; i++)
        {
            perObjectHistoryValues.Add(
                EditorPrefs.GetString(prefId + nameof(perObjectHistoryValues) + perObjectHistoryKeys[i])
                .Split('|').ToList());
        }
        //Debug.Log(perObjectHistoryKeys.Serialize());
        //Debug.Log(perObjectHistoryValues.Select(x => x.list).Serialize());
    }

    // ISerializationCallbackReceiver is not called when window is closed, so we handle it manually in OnEnable/Disable
    // Recompilation - awake/destroy not called, only OnEnable/Disable
    // Closing and opening window - Awake, Destroy, OnEnable, OnDisable are all called
    public void OnBeforeSerialize()
    {
        //Debug.Log("custom before serialize");
        perObjectPinnedKeys.Clear();
        perObjectPinnedValues.Clear();
        foreach (KeyValuePair<GlobalObjectId, List<GlobalObjectId>> pair in perObjectPinned)
        {
            perObjectPinnedKeys.Add(pair.Key.ToString());
            perObjectPinnedValues.Add(pair.Value.Select(x => x.ToString()).ToList());
        }

        perObjectHistoryKeys.Clear();
        perObjectHistoryValues.Clear();
        foreach (KeyValuePair<GlobalObjectId, List<GlobalObjectId>> pair in perObjectHistory)
        {
            perObjectHistoryKeys.Add(pair.Key.ToString());
            perObjectHistoryValues.Add(pair.Value.Select(x => x.ToString()).ToList());
        }
        //Debug.Log(perObjectHistoryKeys.Serialize());
        //Debug.Log(perObjectHistoryValues.Select(x => x.list).Serialize());
    }

    public void OnAfterDeserialize()
    {
        //Debug.Log("custom after deserialize");
        perObjectPinned.Clear();
        for (int i = 0; i < perObjectPinnedKeys.Count; i++)
        {
            perObjectPinned.Add(Parse(perObjectPinnedKeys[i]), perObjectPinnedValues[i].list.Select(x => Parse(x)).ToList());
        }

        perObjectHistory.Clear();
        for (int i = 0; i < perObjectHistoryKeys.Count; i++)
        {
            perObjectHistory.Add(Parse(perObjectHistoryKeys[i]), perObjectHistoryValues[i].list.Select(x => Parse(x)).ToList());
        }
    }

    #region Helpers

    // The default method of GlobalObjectId does not work for some reason for objects in prefab stage
    private Object GlobalObjectIdentifierToPrefabObject(Transform[] prefabChildren, GlobalObjectId gid)
    {
        foreach (var c in prefabChildren)
        {
            var childrenGid = GlobalObjectId.GetGlobalObjectIdSlow(c.gameObject);
            if (ComparePrefabObjectInstance(childrenGid, gid))
            {
                return c.gameObject;
            }
        }
        return null;
    }

    // Usable for instances in the scene to select all copies
    private List<Object> GlobalObjectIdentifierToPrefabObjects(Transform[] prefabChildren, GlobalObjectId gid)
    {
        var objs = new List<Object>();
        foreach (var c in prefabChildren)
        {
            var childrenGid = GlobalObjectId.GetGlobalObjectIdSlow(c.gameObject);
            if (ComparePrefabObjectInstance(childrenGid, gid))
            {
                objs.Add(c.gameObject);
            }
        }
        return objs;
    }

    // Omits asset GUID and targetPrefabId. This is to sync prefab asset and prefab instances hierarchy history.
    // targetPrefabId is only assigned children of instances of prefabs in the scene, targetObjectId is the same everywhere.
    // On playmode entered, targetObjectId and targetPrefaId are added together (it's more complicated)
    private bool ComparePrefabObjectInstance(GlobalObjectId gid1, GlobalObjectId gid2)
    {
        return gid1.targetObjectId == gid2.targetObjectId;
    }

    private GlobalObjectId Parse(string gidString)
    {
        GlobalObjectId.TryParse(gidString, out GlobalObjectId gid);
        return gid;
    }

    // Parse method cannot parse GIDs with null GUIDs (a bug probably)
    private GlobalObjectId ConstructGid(
        int identifierType, string assetGUID, ulong targetObjecId, ulong targetPrefabId)
    {
        string newGidString = $"GlobalObjectId_V1-{identifierType}-{assetGUID}-{targetObjecId}-{targetPrefabId}";
        return Parse(newGidString);
    }

    private GlobalObjectId ConvertToUnpackedGid(GlobalObjectId gid)
    {
        ulong unpackedTargetObjectId = (gid.targetObjectId ^ gid.targetPrefabId) & 0x7fffffffffffffff;
        return ConstructGid(gid.identifierType, gid.assetGUID.ToString(), unpackedTargetObjectId, 0);
    }
    #endregion
}
