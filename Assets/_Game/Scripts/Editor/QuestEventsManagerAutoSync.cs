using System.Linq;
using Game.Quest;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Automatically keeps the QuestEventsManager prefab's _quests list in sync with
    /// all QuestSO assets in the project. Runs on asset import/delete/move and
    /// is also available as a manual menu item.
    /// </summary>
    public class QuestEventsManagerAutoSync : AssetPostprocessor
    {
        private const string QUEST_EVENTS_PREFAB_PATH = "Assets/_Game/Prefabs/QuestEventsManager.prefab";
        private const string QUESTS_DATA_FOLDER       = "Assets/_Game/Data/Quests/";

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool needsSync =
                importedAssets.Any(IsOrMayBeQuestAsset) ||
                deletedAssets.Any(IsOrMayBeQuestAsset) ||
                movedAssets.Any(IsOrMayBeQuestAsset) ||
                movedFromAssetPaths.Any(IsOrMayBeQuestAsset);

            if (!needsSync) return;

            EditorApplication.delayCall += SyncQuestsToPrefab;
        }

        private static bool IsOrMayBeQuestAsset(string path)
        {
            if (!path.EndsWith(".asset")) return false;
            if (path.StartsWith(QUESTS_DATA_FOLDER))
            {
                var asset = AssetDatabase.LoadAssetAtPath<QuestSO>(path);
                return asset != null;
            }
            return AssetDatabase.LoadAssetAtPath<QuestSO>(path) != null;
        }

        [MenuItem("Game/Dev/Sync Quests to QuestEventsManager Prefab")]
        public static void SyncQuestsToPrefab()
        {
            var allQuests = AssetDatabase
                .FindAssets("t:QuestSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<QuestSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(q => q != null)
                .OrderBy(q => q.name)
                .ToList();

            var prefab = PrefabUtility.LoadPrefabContents(QUEST_EVENTS_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[QuestEventsManagerAutoSync] Prefab not found at: " + QUEST_EVENTS_PREFAB_PATH);
                return;
            }

            var manager = prefab.GetComponentInChildren<QuestEventsManager>(includeInactive: true);
            if (manager == null)
            {
                PrefabUtility.UnloadPrefabContents(prefab);
                Debug.LogError("[QuestEventsManagerAutoSync] No QuestEventsManager component found in prefab.");
                return;
            }

            var so   = new SerializedObject(manager);
            var prop = so.FindProperty("_quests");

            prop.ClearArray();
            for (int i = 0; i < allQuests.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = allQuests[i];
            }
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefab, QUEST_EVENTS_PREFAB_PATH);
            PrefabUtility.UnloadPrefabContents(prefab);

            Debug.Log($"[QuestEventsManagerAutoSync] Synced {allQuests.Count} QuestSO(s) → QuestEventsManager prefab._quests.");
        }
    }
}
