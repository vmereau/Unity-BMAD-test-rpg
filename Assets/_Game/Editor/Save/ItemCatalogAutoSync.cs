using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Inventory;
using UnityEditor;

namespace Game.Editor.Save
{
    /// <summary>
    /// Keeps the single <see cref="ItemCatalogSO"/> asset in sync with every <see cref="ItemSO"/> in the
    /// project and guarantees every item has a unique, non-empty <see cref="ItemSO.itemId"/>. Runs after
    /// any item asset is imported / moved / deleted, and on demand via <c>Tools/Save/Sync Item Catalog</c>.
    /// </summary>
    public class ItemCatalogAutoSync : AssetPostprocessor
    {
        private const string TAG = "[ItemCatalogSync]";
        private const string DEFAULT_CATALOG_PATH = "Assets/_Game/Data/Items/ItemCatalog.asset";

        private static bool _syncScheduled;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool needsSync =
                importedAssets.Any(IsItemAsset) ||
                movedAssets.Any(IsItemAsset) ||
                (deletedAssets.Any(IsAssetFile) && CatalogHasMissingEntries());

            if (!needsSync || _syncScheduled) return;

            // Delay so all imports are committed before reading them back; coalesce bursts into one sync.
            _syncScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _syncScheduled = false;
                SyncItemCatalog();
            };
        }

        private static bool IsAssetFile(string path) => path.EndsWith(".asset");

        // A deleted item leaves a missing reference in the catalog — cheaper than rescanning on every delete.
        // (Moves keep references valid; moved items are caught by IsItemAsset on movedAssets.)
        private static bool CatalogHasMissingEntries()
        {
            var guids = AssetDatabase.FindAssets("t:ItemCatalogSO");
            if (guids.Length == 0) return true;
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return catalog == null || catalog.Items.Any(i => i == null);
        }

        private static bool IsItemAsset(string path) =>
            IsAssetFile(path) && AssetDatabase.LoadAssetAtPath<ItemSO>(path) != null;

        [MenuItem("Tools/Save/Sync Item Catalog")]
        public static void SyncItemCatalog()
        {
            var items = AssetDatabase.FindAssets("t:ItemSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(p => AssetDatabase.LoadAssetAtPath<ItemSO>(p))
                .Where(i => i != null)
                .ToList();

            AssignMissingIds(items);
            ReportDuplicateIds(items);

            var catalog = LoadOrCreateCatalog();
            if (catalog == null) return;

            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("_items");
            if (ListMatches(prop, items)) return;

            prop.ClearArray();
            for (int i = 0; i < items.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(catalog);
            GameLog.Info(TAG, $"Synced {items.Count} ItemSO(s) → {AssetDatabase.GetAssetPath(catalog)}.");
        }

        private static void AssignMissingIds(List<ItemSO> items)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.itemId))
                {
                    // ItemSO.OnValidate may have filled the ID in memory only (not always flagged dirty) —
                    // persist it, otherwise it is regenerated every session and saved games lose the item.
                    // Checking the file keeps this loop-safe: once written, the save → reimport → sync stops.
                    if (!IdIsOnDisk(item))
                    {
                        EditorUtility.SetDirty(item);
                        AssetDatabase.SaveAssetIfDirty(item);
                        GameLog.Info(TAG, $"Persisted itemId of '{AssetDatabase.GetAssetPath(item)}'.");
                    }
                    continue;
                }
                var so = new SerializedObject(item);
                so.FindProperty(nameof(ItemSO.itemId)).stringValue = System.Guid.NewGuid().ToString("N");
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(item);
                GameLog.Info(TAG, $"Assigned itemId to '{AssetDatabase.GetAssetPath(item)}'.");
            }
        }

        private static bool IdIsOnDisk(ItemSO item)
        {
            try
            {
                return System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(item))
                    .Contains($"itemId: {item.itemId}");
            }
            catch (System.Exception e)
            {
                GameLog.Warn(TAG, $"Could not read '{AssetDatabase.GetAssetPath(item)}': {e.Message}");
                return true; // don't force a write we can't verify
            }
        }

        private static void ReportDuplicateIds(List<ItemSO> items)
        {
            foreach (var group in items.GroupBy(i => i.itemId).Where(g => g.Count() > 1))
            {
                var paths = string.Join("', '", group.Select(AssetDatabase.GetAssetPath));
                GameLog.Error(TAG,
                    $"Duplicate itemId '{group.Key}' on '{paths}'. This happens when an item asset is duplicated — " +
                    "clear the copy's itemId field so it regenerates, then run Tools/Save/Sync Item Catalog.");
            }
        }

        private static ItemCatalogSO LoadOrCreateCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:ItemCatalogSO");
            if (guids.Length > 1)
                GameLog.Error(TAG, $"{guids.Length} ItemCatalogSO assets found — only one is supported. Syncing the first.");

            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var catalog = UnityEngine.ScriptableObject.CreateInstance<ItemCatalogSO>();
            AssetDatabase.CreateAsset(catalog, DEFAULT_CATALOG_PATH);
            GameLog.Info(TAG, $"Created {DEFAULT_CATALOG_PATH}.");
            return catalog;
        }

        private static bool ListMatches(SerializedProperty prop, List<ItemSO> items)
        {
            if (prop.arraySize != items.Count) return false;
            for (int i = 0; i < items.Count; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue != items[i]) return false;
            return true;
        }
    }
}
