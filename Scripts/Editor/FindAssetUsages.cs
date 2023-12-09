using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public class FindAssetUsages : EditorWindow
{
    // Look for usages only in these assets (and their .meta files)
    private static readonly string[] extensions =
    {
        "prefab", "asset", "unity", "preset",
        "fbx", "obj", "blend", "mesh",
        "mat", "cubemap",
        "spriteatlas",
        "controller", "overrideController",
        "flare",
        "mask",
        "shader", "compute", "shadergraph", "shadersubgraph",
        "terrainlayer",
        "brush",
        "cs.meta", "asmdef", "asmref",
    };

    private List<Object> assets = new();
    private string selectedAssetGuid;
    private string selectedAssetPath;
    private string selectedAssetName;
    private Vector2 scroll = Vector2.zero;
    private int progressId;
    private float progress;
    private bool isSearching;
    private CancellationTokenSource tokenSource;

    [MenuItem("Assets/Find Asset Usage _#F12")]
    public static void CreateWindow()
    {
        var window = CreateWindow<FindAssetUsages>();
        window.Show();

        string[] assetGuids = Selection.assetGUIDs;
        if (assetGuids.Length == 0)
        {
            Debug.Log("Cannot find asset usages when no assets are selected.");
            return;
        }

        window.selectedAssetGuid = assetGuids[0];
        window.selectedAssetPath = AssetDatabase.GUIDToAssetPath(window.selectedAssetGuid).Replace("/", "\\");
        window.selectedAssetName = Path.GetFileName(window.selectedAssetPath);

        _ = window.FindAssetUsage(window.selectedAssetGuid);
    }

    private void OnEnable()
    {
        Progress.added += OnSearchStarted;
        Progress.updated += OnSearchUpdated;
        Progress.removed += OnSearchEnded;
    }

    private void OnGUI()
    {
        if (selectedAssetGuid == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }
        string searchedAssetFilename = AssetDatabase.GUIDToAssetPath(selectedAssetGuid);
        if (searchedAssetFilename == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }

        var searchedAsset = AssetDatabase.LoadAssetAtPath<Object>(searchedAssetFilename);
        if (searchedAsset == null)
        {
            EditorGUILayout.LabelField("Right click on an Asset and select 'Find Asset Usages'");
            return;
        }

        EditorGUILayout.LabelField(searchedAssetFilename);
        GUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField(searchedAsset, searchedAsset.GetType(), true);
        GUIContent searchContent = EditorGUIUtility.IconContent("Search Icon");
        if (GUILayout.Button(searchContent, GUILayout.MaxWidth(40), GUILayout.MaxHeight(18)))
        {
            if (!string.IsNullOrEmpty(selectedAssetGuid))
                _ = FindAssetUsage(selectedAssetGuid);
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        if (isSearching)
        {
            EditorGUI.ProgressBar(GUILayoutUtility.GetRect(position.width, 20),
                progress, $"Searching {Mathf.RoundToInt((progress * 100))}%");
        }


        GUILayout.Label("Found Asset References", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var asset in assets)
        {
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset));
            if (asset != null)
            {
                var type = asset.GetType();
                EditorGUILayout.ObjectField(asset, type, true);
            }
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndScrollView();
    }

    private void OnDisable()
    {
        Progress.added -= OnSearchStarted;
        Progress.updated -= OnSearchUpdated;
        Progress.removed -= OnSearchEnded;
        CancelSearch();
    }


    private async Task FindAssetUsage(string assetGuid)
    {
        CancelSearch();
        tokenSource = new CancellationTokenSource();
        progressId = Progress.Start($"Find Asset Usages {selectedAssetName}");
        Progress.RegisterCancelCallback(progressId, CancelSearch);
        assets = await FindAssetUsageAsync(assetGuid, tokenSource.Token, progressId);
        Repaint();
        tokenSource = null;
    }

    private static List<Object> LoadAssetPaths(List<string> assetPaths)
    {
        var assets = new List<Object>();
        foreach (string assetFilename in assetPaths)
        {
            string fileName = assetFilename.EndsWith(".meta") ?
                assetFilename.Substring(0, assetFilename.Length - 5) : assetFilename;
            var asset = AssetDatabase.LoadAssetAtPath<Object>(fileName);
            if (asset != null)
                assets.Add(asset);
        }
        return assets;
    }

    public static async Task<List<Object>> FindAssetUsageAsync(string assetGuid, CancellationToken token, int? progressId = null,
        bool showProgressBar = false)
    {
        var usedByAssetsPaths = new List<string>(); // Empty old results
        float progress = 0;
        string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        var otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        List<string> extensionsWithMeta = new List<string>(extensions);
        extensionsWithMeta.AddRange(extensions.Select(x => x + ".meta"));
        otherFilesPaths = otherFilesPaths.Where(x => Regex.IsMatch(x, $"\\.({string.Join("|", extensionsWithMeta)})$")).ToList();

        string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid).Replace("/", "\\");
        var assetName = Path.GetFileName(assetPath);
        string assetFilePath = projectPath + "\\" + assetPath;
        string assetMetaFilePath = assetFilePath + ".meta";
        Regex regex = new Regex(assetGuid, RegexOptions.Compiled);

        if (progressId == null)
            progressId = Progress.Start($"Find Asset Usages {assetName}");
        new ItemProgress(progressId.Value, showProgressBar);
        await Task.Run(() => Find());
        return LoadAssetPaths(usedByAssetsPaths);

        void Find()
        {
            int total = otherFilesPaths.Count;
            int current = 0;
            foreach (string otherFilePath in otherFilesPaths)
            {
                if (token.IsCancellationRequested) break;
                if (!File.Exists(otherFilePath))
                {
                    Debug.LogWarning($"File does not exist, path too long? Path: {otherFilePath}");
                    continue;
                }
                var otherFileName = Path.GetFileName(otherFilePath);
                progress = current / (float)total;
                Progress.Report(progressId.Value, current, total, otherFileName);
                current++;

                if (regex.IsMatch(File.ReadAllText(otherFilePath)))
                {
                    if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue;
                    string otherFileAssetPath = otherFilePath.Replace(projectPath + "\\", "").Replace("\\", "/");
                    usedByAssetsPaths.Add(otherFileAssetPath);   // Not referencing self, add ref
                }
            }
            Progress.Remove(progressId.Value);
        }
    }

    private bool CancelSearch()
    {
        if (tokenSource != null) tokenSource.Cancel();
        return true;
    }

    private void OnSearchStarted(Progress.Item[] items)
    {
        var item = items.FirstOrDefault(x => x.id == progressId);
        if (item == null) return;
        isSearching = true;
        Repaint();
    }

    private void OnSearchUpdated(Progress.Item[] items)
    {
        var item = items.FirstOrDefault(x => x.id == progressId);
        if (item == null) return;
        progress = item.progress;
        Repaint();
    }

    private void OnSearchEnded(Progress.Item[] items)
    {
        var item = items.FirstOrDefault(x => x.id == progressId);
        if (item == null) return;
        isSearching = false;
        Repaint();
    }

    public static List<Object> FindAssetUsageSync(string assetGuid)
    {
        var usedByAssetsPaths = new List<string>(); // Empty old results

        string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 7).Replace("/", "\\");
        var otherFilesPaths = Directory.EnumerateFiles(projectPath + "\\Assets", "*", SearchOption.AllDirectories).ToList();
        otherFilesPaths.AddRange(Directory.EnumerateFiles(projectPath + "\\ProjectSettings", "*", SearchOption.AllDirectories).ToList());
        List<string> extensionsWithMeta = new List<string>(extensions);
        extensionsWithMeta.AddRange(extensions.Select(x => x + ".meta"));
        otherFilesPaths = otherFilesPaths.Where(x => Regex.IsMatch(x, $"\\.({string.Join("|", extensionsWithMeta)})$")).ToList();

        int total = otherFilesPaths.Count;
        int current = 0;

        string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid).Replace("/", "\\");
        var assetName = Path.GetFileName(assetPath);
        string assetFilePath = projectPath + "\\" + assetPath;
        string assetMetaFilePath = assetFilePath + ".meta";
        Regex regex = new Regex(assetGuid, RegexOptions.Compiled);

        foreach (string otherFilePath in otherFilesPaths)
        {
            if (!File.Exists(otherFilePath))
            {
                Debug.LogWarning($"File does not exist, path too long? Path: {otherFilePath}");
                continue;
            }
            var otherFileName = Path.GetFileName(otherFilePath);
            float progress = current / (float)total;
            if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", progress))
            {
                EditorUtility.ClearProgressBar();
                return LoadAssetPaths(usedByAssetsPaths);
            }
            current++;

            if (regex.IsMatch(File.ReadAllText(otherFilePath)))
            {
                if (assetFilePath == otherFilePath || assetMetaFilePath == otherFilePath) continue;
                string otherFileAssetPath = otherFilePath.Replace(projectPath + "\\", "").Replace("\\", "/");
                usedByAssetsPaths.Add(otherFileAssetPath);   // Not referencing self, add ref
            }
        }
        EditorUtility.ClearProgressBar();

        return LoadAssetPaths(usedByAssetsPaths);
    }

    public class ItemProgress
    {
        public int progressId;
        public float progress;
        public bool showProgressBar;

        public ItemProgress(int progressId, bool showProgressBar = false)
        {
            this.progressId = progressId;
            this.showProgressBar = showProgressBar;
            Progress.added += OnSearchStarted;
            Progress.updated += OnSearchUpdated;
            Progress.removed += OnSearchEnded;
        }

        private void OnSearchStarted(Progress.Item[] items)
        {
            var item = items.FirstOrDefault(x => x.id == progressId);
            if (item == null) return;
        }

        private void OnSearchUpdated(Progress.Item[] items)
        {
            var item = items.FirstOrDefault(x => x.id == progressId);
            if (item == null) return;
            progress = item.progress;
            if (showProgressBar)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Searching...", "Searching for asset references", progress))
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private void OnSearchEnded(Progress.Item[] items)
        {
            var item = items.FirstOrDefault(x => x.id == progressId);
            if (item == null) return;
            EditorUtility.ClearProgressBar();
            Progress.added -= OnSearchStarted;
            Progress.updated -= OnSearchUpdated;
            Progress.removed -= OnSearchEnded;
        }
    }
}
