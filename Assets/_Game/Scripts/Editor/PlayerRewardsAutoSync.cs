using System.Linq;
using Game.Progression;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Automatically keeps the Player prefab's PlayerRewards._rewards list in sync with
    /// all PlayerRewardSO assets in the project. Runs on asset import/delete/move and
    /// is also available as a manual menu item.
    /// </summary>
    public class PlayerRewardsAutoSync : AssetPostprocessor
    {
        private const string PLAYER_PREFAB_PATH = "Assets/_Game/Prefabs/Player/Player.prefab";
        private const string REWARDS_FOLDER     = "Assets/_Game/ScriptableObjects/Rewards/";

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool needsSync =
                importedAssets.Any(IsOrMayBeRewardAsset) ||
                deletedAssets.Any(IsOrMayBeRewardAsset) ||
                movedAssets.Any(IsOrMayBeRewardAsset) ||
                movedFromAssetPaths.Any(IsOrMayBeRewardAsset);

            if (!needsSync) return;

            // Delay one frame so all imports are fully committed before we read them back.
            EditorApplication.delayCall += SyncRewardsToPrefab;
        }

        // For imported / moved-to: verify actual type. For deleted / moved-from: check by path.
        private static bool IsOrMayBeRewardAsset(string path)
        {
            if (!path.EndsWith(".asset")) return false;
            if (path.StartsWith(REWARDS_FOLDER)) return true;
            var asset = AssetDatabase.LoadAssetAtPath<PlayerRewardSO>(path);
            return asset != null;
        }

        [MenuItem("Game/Dev/Sync Player Rewards to Prefab")]
        public static void SyncRewardsToPrefab()
        {
            var allRewards = AssetDatabase
                .FindAssets("t:PlayerRewardSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<PlayerRewardSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(r => r != null)
                .OrderBy(r => r.name)
                .ToList();

            var prefab = PrefabUtility.LoadPrefabContents(PLAYER_PREFAB_PATH);
            if (prefab == null)
            {
                Debug.LogError("[PlayerRewardsAutoSync] Player prefab not found at: " + PLAYER_PREFAB_PATH);
                return;
            }

            var playerRewards = prefab.GetComponentInChildren<PlayerRewards>(includeInactive: true);
            if (playerRewards == null)
            {
                PrefabUtility.UnloadPrefabContents(prefab);
                Debug.LogError("[PlayerRewardsAutoSync] No PlayerRewards component found in Player prefab.");
                return;
            }

            var so   = new SerializedObject(playerRewards);
            var prop = so.FindProperty("_rewards");

            prop.ClearArray();
            for (int i = 0; i < allRewards.Count; i++)
            {
                prop.InsertArrayElementAtIndex(i);
                prop.GetArrayElementAtIndex(i).objectReferenceValue = allRewards[i];
            }
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefab, PLAYER_PREFAB_PATH);
            PrefabUtility.UnloadPrefabContents(prefab);

            Debug.Log($"[PlayerRewardsAutoSync] Synced {allRewards.Count} PlayerRewardSO(s) → Player prefab._rewards.");
        }
    }
}
