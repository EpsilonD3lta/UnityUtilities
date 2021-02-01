﻿using System.IO;
using UnityEditor;
using UnityEditor.Presets;

//Modified from: https://docs.unity3d.com/Manual/DefaultPresetsByFolder.html
public class PresetImportPerFolder : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        // Make sure we are applying presets the first time an asset is imported.
        if (assetImporter.importSettingsMissing)
        {

            // Get the current imported asset folder.
            var path = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) return;
            while (!string.IsNullOrEmpty(path))
            {

                // Find all Preset assets in this folder.
                var presetGuids = AssetDatabase.FindAssets("t:Preset", new[] { path });
                foreach (var presetGuid in presetGuids)
                {
                    // Make sure we are not testing Presets in a subfolder.
                    string presetPath = AssetDatabase.GUIDToAssetPath(presetGuid);
                    if (Path.GetDirectoryName(presetPath) == path)
                    {
                        // Load the Preset and try to apply it to the importer.
                        var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
                        if (preset.ApplyTo(assetImporter))
                            return;
                    }
                }

                // Try again in the parent folder.
                path = Path.GetDirectoryName(path);
            }
        }
    }
}