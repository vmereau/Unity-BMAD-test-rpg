using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]
    public class ItemSO : ScriptableObject
    {
        private const string TAG = "[ItemSO]";

        [Tooltip("Stable save ID — auto-generated, never edit. Clear it on a duplicated asset so it regenerates.")]
        public string itemId;
        public string itemName;
        public string description;
        public Sprite icon;
        public int maxStacks = 1;
        public bool IsStackable => maxStacks > 1;
        public GameObject worldItemPrefab;
        public int buyValue = 1;
        public int sellValue = 1;
        [Tooltip("Can be placed in a cooking station's Experiment slots.")]
        public bool isIngredient;
        [Tooltip("Smithing material (ore, ingots…): listed by an anvil's screen.")]
        public bool isCraftingMaterial;
        [Tooltip("Quest item: can't be sold, dropped, stored in a container or bound to the action bar — it only leaves " +
                 "the player through dialogue (ChoiceOption.requiredItem).")]
        public bool isQuestItem;

        /// <summary>
        /// True when the item may leave the player's inventory through trade, drop, container or action bar
        /// (never for quest items and keys).
        /// </summary>
        public static bool CanPlayerPart(ItemSO item) => item != null && !item.isQuestItem && !(item is KeyItemSO);

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            // A quest item must not be consumable by another system (cooking, smithing, use, equip).
            if (isQuestItem && (isIngredient || isCraftingMaterial || this is UsableItemSO || this is EquipableItemSO))
                Game.Core.GameLog.Warn(TAG, $"{name}: isQuestItem combined with an ingredient / material / usable / " +
                                                   "equipable item — only the inventory UI guards it, cooking skips it");
            if (!string.IsNullOrEmpty(itemId)) return;
            itemId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
