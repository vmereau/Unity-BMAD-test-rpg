#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.Inventory;

namespace Game.Editor
{
    /// <summary>
    /// One-time utility: adds ActionBarSystem to Player prefab and wires internal references.
    /// Run via: Tools > Game > Setup ActionBarSystem on Player
    /// After running, delete this script.
    /// </summary>
    public static class SetupActionBarSystem
    {
        private const string PLAYER_PREFAB = "Assets/_Game/Prefabs/Player/Player.prefab";

        [MenuItem("Tools/Game/Setup ActionBarSystem on Player")]
        public static void Setup()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (playerPrefab == null)
            {
                Debug.LogError("[SetupActionBarSystem] Player prefab not found at: " + PLAYER_PREFAB);
                return;
            }

            // Check if ActionBarSystem already exists
            var existing = playerPrefab.GetComponent<ActionBarSystem>();
            if (existing != null)
            {
                Debug.Log("[SetupActionBarSystem] ActionBarSystem already exists on Player prefab.");
                return;
            }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(PLAYER_PREFAB))
            {
                var root = scope.prefabContentsRoot;

                var abs = root.AddComponent<ActionBarSystem>();
                var invSystem = root.GetComponent<InventorySystem>();

                using var so = new SerializedObject(abs);
                so.FindProperty("_inventorySystem").objectReferenceValue = invSystem;
                so.FindProperty("_playerTransform").objectReferenceValue = root.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[SetupActionBarSystem] ActionBarSystem added and wired on Player prefab.");
        }
    }
}
#endif
