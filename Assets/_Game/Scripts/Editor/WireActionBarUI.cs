#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Game.UI;
using Game.Inventory;

namespace Game.Editor
{
    /// <summary>
    /// One-time utility: wires ActionBarUI (on UICanvas prefab) to ActionBarSystem and InventorySystem
    /// from the Player prefab. Because these live on different prefabs, wiring is done at the prefab
    /// level by finding the nested components.
    /// Run via: Tools > Game > Wire ActionBarUI References
    /// After running, delete this script.
    /// </summary>
    public static class WireActionBarUI
    {
        private const string UICANVAS_PREFAB = "Assets/_Game/Prefabs/UI/UICanvas.prefab";
        private const string PLAYER_PREFAB = "Assets/_Game/Prefabs/Player/Player.prefab";

        [MenuItem("Tools/Game/Wire ActionBarUI References")]
        public static void Wire()
        {
            // Load Player prefab to get ActionBarSystem and InventorySystem
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (playerPrefab == null) { Debug.LogError("[WireActionBarUI] Player prefab not found."); return; }

            var absOnPlayer = playerPrefab.GetComponent<ActionBarSystem>();
            var invOnPlayer = playerPrefab.GetComponent<InventorySystem>();

            if (absOnPlayer == null) { Debug.LogError("[WireActionBarUI] ActionBarSystem not found on Player prefab. Run 'Setup ActionBarSystem on Player' first."); return; }
            if (invOnPlayer == null) { Debug.LogError("[WireActionBarUI] InventorySystem not found on Player prefab."); return; }

            using (var scope = new PrefabUtility.EditPrefabContentsScope(UICANVAS_PREFAB))
            {
                var root = scope.prefabContentsRoot;
                var actionBarGO = root.transform.Find("ActionBar");
                if (actionBarGO == null) { Debug.LogError("[WireActionBarUI] ActionBar not found in UICanvas prefab."); return; }

                var abUI = actionBarGO.GetComponent<ActionBarUI>();
                if (abUI == null) { Debug.LogError("[WireActionBarUI] ActionBarUI not found on ActionBar GO."); return; }

                using var so = new SerializedObject(abUI);
                so.FindProperty("_actionBarSystem").objectReferenceValue = absOnPlayer;
                so.FindProperty("_inventorySystem").objectReferenceValue = invOnPlayer;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[WireActionBarUI] ActionBarUI references wired successfully.");
        }
    }
}
#endif
