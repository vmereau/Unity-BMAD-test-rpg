#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Game.NPC;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Keeps each NPCEntity.memories list in sync with all NPCMemoryEntrySO assets found
    /// under its adjacent Memories/ subfolder (Data/NPCs/NPC_XXX/Memories/**).
    /// Runs automatically on asset import/delete/move and is also available as a menu item.
    /// </summary>
    public class NPCMemoriesAutoSync : AssetPostprocessor
    {
        private const string NPC_DATA_ROOT      = "Assets/_Game/Data/NPCs/";
        private const string MEMORIES_FOLDER    = "Memories";

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var affectedNpcFolders = new HashSet<string>();
            CollectAffectedFolders(importedAssets,       affectedNpcFolders);
            CollectAffectedFolders(deletedAssets,        affectedNpcFolders);
            CollectAffectedFolders(movedAssets,          affectedNpcFolders);
            CollectAffectedFolders(movedFromAssetPaths,  affectedNpcFolders);

            if (affectedNpcFolders.Count == 0) return;

            // Delay one frame so all imports are fully committed before we read them back.
            var captured = new HashSet<string>(affectedNpcFolders);
            EditorApplication.delayCall += () =>
            {
                foreach (var folder in captured)
                    SyncMemoriesForNpc(folder);
            };
        }

        private static void CollectAffectedFolders(string[] paths, HashSet<string> result)
        {
            foreach (var path in paths)
            {
                if (!path.EndsWith(".asset")) continue;
                if (!path.StartsWith(NPC_DATA_ROOT)) continue;

                // path relative to NPC root: "NPC_Guard/Memories/Spiders/Mem_xxx.asset"
                string relative = path.Substring(NPC_DATA_ROOT.Length);
                int slash = relative.IndexOf('/');
                if (slash < 0) continue;

                string npcFolder = relative.Substring(0, slash);
                string rest      = relative.Substring(slash + 1);

                if (!rest.StartsWith(MEMORIES_FOLDER + "/") && rest != MEMORIES_FOLDER)
                    continue;

                result.Add(NPC_DATA_ROOT + npcFolder);
            }
        }

        [MenuItem("Game/Dev/Sync All NPC Memories")]
        public static void SyncAllNpcMemories()
        {
            foreach (var folder in AssetDatabase.GetSubFolders(NPC_DATA_ROOT))
                SyncMemoriesForNpc(folder);
        }

        private static void SyncMemoriesForNpc(string npcFolder)
        {
            // Find NPCEntity asset at the root of the NPC folder (not in subfolders).
            NPCEntity entity     = null;
            string    entityPath = null;

            foreach (var guid in AssetDatabase.FindAssets("t:NPCEntity", new[] { npcFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Skip anything deeper than one level (memories, dialogues, …)
                string relative = path.Substring(npcFolder.Length + 1);
                if (relative.Contains("/")) continue;

                entity = AssetDatabase.LoadAssetAtPath<NPCEntity>(path);
                if (entity != null) { entityPath = path; break; }
            }

            if (entity == null)
            {
                Debug.LogWarning($"[NPCMemoriesAutoSync] No NPCEntity found at root of '{npcFolder}' — skipping.");
                return;
            }

            // Collect all NPCMemoryEntrySO assets under Memories/, sorted for stability.
            string memoriesPath = npcFolder + "/" + MEMORIES_FOLDER;
            List<NPCMemoryEntrySO> discovered;

            if (AssetDatabase.IsValidFolder(memoriesPath))
            {
                discovered = AssetDatabase
                    .FindAssets("t:NPCMemoryEntrySO", new[] { memoriesPath })
                    .Select(g => AssetDatabase.LoadAssetAtPath<NPCMemoryEntrySO>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(m => m != null)
                    .OrderBy(m => m.name)
                    .ToList();
            }
            else
            {
                discovered = new List<NPCMemoryEntrySO>();
            }

            // Skip write if the list is already identical.
            if (entity.memories != null
                && entity.memories.Count == discovered.Count
                && discovered.SequenceEqual(entity.memories))
                return;

            var so   = new SerializedObject(entity);
            var prop = so.FindProperty("memories");

            prop.ClearArray();
            for (int i = 0; i < discovered.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = discovered[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(entity);
            AssetDatabase.SaveAssets();

            string names = discovered.Count > 0
                ? string.Join(", ", discovered.Select(m => m.name))
                : "(none)";
            Debug.Log($"[NPCMemoriesAutoSync] '{entity.name}': {discovered.Count} memor{(discovered.Count == 1 ? "y" : "ies")} → [{names}]");
        }
    }
}
#endif
