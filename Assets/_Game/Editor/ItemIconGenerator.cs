using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Inventory;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class ItemIconGenerator
    {
        private const string IconsPath = "Assets/_Game/Art/UI/Items";
        private const string ItemsPath = "Assets/_Game/Data/Items";
        private const string SessionRunningKey = "ItemIconGenerator.IsRunning";

        private static readonly List<ItemSO> _pendingItems = new();
        private static bool _isRunning;

        private static readonly Regex _invalidChars = new(
            $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
            RegexOptions.Compiled);

        static ItemIconGenerator()
        {
            // Warn if a domain reload interrupted an in-progress generation run
            if (SessionState.GetBool(SessionRunningKey, false))
            {
                SessionState.EraseBool(SessionRunningKey);
                Debug.LogWarning("[ItemIconGenerator] Domain reload interrupted icon generation. Run Tools/Items/Generate Missing Icons again.");
            }
        }

        [MenuItem("Tools/Items/Generate Missing Icons")]
        public static void GenerateMissingIcons()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[ItemIconGenerator] Already running — wait for the current pass to complete.");
                return;
            }

            _pendingItems.Clear();

            string[] guids = AssetDatabase.FindAssets("t:ItemSO", new[] { ItemsPath });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemSO item = AssetDatabase.LoadAssetAtPath<ItemSO>(path);

                if (item == null || item.worldItemPrefab == null)
                    continue;

                if (item.icon != null)
                {
                    // Detect broken reference — sprite file deleted from disk
                    string spritePath = AssetDatabase.GetAssetPath(item.icon);
                    if (!string.IsNullOrEmpty(spritePath) && File.Exists(spritePath))
                        continue;

                    item.icon = null;
                    EditorUtility.SetDirty(item);
                }

                string unityPath = $"{IconsPath}/{SanitizeFileName(item.name)}_Icon.png";

                // If the PNG already exists, link it directly without re-rendering
                if (File.Exists(unityPath))
                {
                    AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceSynchronousImport);
                    Sprite existing = LoadSpriteAtPath(unityPath);
                    if (existing != null)
                    {
                        item.icon = existing;
                        EditorUtility.SetDirty(item);
                        continue;
                    }
                }

                // Warm up the async preview, queue for deferred processing
                AssetPreview.GetAssetPreview(item.worldItemPrefab);
                _pendingItems.Add(item);
            }

            if (_pendingItems.Count == 0)
            {
                Debug.Log("Icon generation: no items need new icons.");
                return;
            }

            _isRunning = true;
            SessionState.SetBool(SessionRunningKey, true);
            // Unsubscribe first to avoid double-registration if somehow called again
            EditorApplication.update -= WaitForPreviewsAndGenerate;
            EditorApplication.update += WaitForPreviewsAndGenerate;
            Debug.Log($"[ItemIconGenerator] Warming up {_pendingItems.Count} asset previews...");
        }

        // Runs every editor frame; waits until every queued item has a ready preview
        private static void WaitForPreviewsAndGenerate()
        {
            // Check per-item rather than the global IsLoadingAssetPreviews (which covers unrelated assets)
            foreach (ItemSO item in _pendingItems)
            {
                if (AssetPreview.GetAssetPreview(item.worldItemPrefab) == null)
                    return;
            }

            EditorApplication.update -= WaitForPreviewsAndGenerate;

            if (!Directory.Exists(IconsPath))
                Directory.CreateDirectory(IconsPath);

            int generated = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < _pendingItems.Count; i++)
                {
                    ItemSO item = _pendingItems[i];
                    EditorUtility.DisplayProgressBar(
                        "Generating Item Icons",
                        $"Processing {item.name} ({i + 1}/{_pendingItems.Count})",
                        (float)(i + 1) / _pendingItems.Count);

                    try
                    {
                        if (GenerateIconForItem(item))
                            generated++;
                        else
                            failed++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ItemIconGenerator] Error processing '{item.name}': {ex.Message}");
                        failed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _pendingItems.Clear();
                _isRunning = false;
                SessionState.EraseBool(SessionRunningKey);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string result = $"[ItemIconGenerator] Done. Generated: {generated}";
            if (failed > 0)
                result += $", Failed: {failed} — check warnings above.";
            Debug.Log(result);
        }

        private static bool GenerateIconForItem(ItemSO item)
        {
            Texture2D preview = AssetPreview.GetAssetPreview(item.worldItemPrefab);
            if (preview == null)
            {
                Debug.LogWarning($"[ItemIconGenerator] Preview still null for '{item.worldItemPrefab.name}'. Skipping.");
                return false;
            }

            string unityPath = $"{IconsPath}/{SanitizeFileName(item.name)}_Icon.png";

            // AssetPreview returns a render-target texture — not CPU-readable via GetPixels.
            // Blit into a temp RenderTexture, then ReadPixels into a new Texture2D.
            RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(preview, rt);

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
            byte[] pngBytes = null;
            try
            {
                copy.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
                copy.Apply();
                pngBytes = copy.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(copy);
            }

            if (pngBytes == null)
                return false;

            File.WriteAllBytes(unityPath, pngBytes);

            AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            // Re-import synchronously so the sprite sub-asset is immediately visible
            AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceSynchronousImport);

            Sprite sprite = LoadSpriteAtPath(unityPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ItemIconGenerator] Could not load sprite at '{unityPath}' after import.");
                return false;
            }

            item.icon = sprite;
            EditorUtility.SetDirty(item);
            return true;
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite s)
                    return s;
            }
            return null;
        }

        private static string SanitizeFileName(string name) =>
            _invalidChars.Replace(name, "_");
    }
}
