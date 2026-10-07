using System.Collections.Generic;
using Game.Inventory;

namespace Game.Core
{
    /// <summary>Converts inventory slots to / from their save form via the item catalog.</summary>
    public static class ItemStackConverter
    {
        private const string TAG = "[Save]";

        public static List<ItemStackSaveData> ToSave(IReadOnlyList<InventorySlot> slots)
        {
            var result = new List<ItemStackSaveData>(slots?.Count ?? 0);
            if (slots == null) return result;
            foreach (var slot in slots)
            {
                if (slot.Item == null) continue;
                if (string.IsNullOrEmpty(slot.Item.itemId))
                {
                    GameLog.Warn(TAG, $"Item '{slot.Item.name}' has no itemId — not saved. Run Tools/Save/Sync Item Catalog.");
                    continue;
                }
                result.Add(new ItemStackSaveData { itemId = slot.Item.itemId, count = slot.Count });
            }
            return result;
        }

        /// <summary>Resolves saved stacks in order. Unknown IDs and non-positive counts are skipped.</summary>
        public static List<(ItemSO item, int count)> FromSave(List<ItemStackSaveData> stacks, ItemCatalogSO catalog)
        {
            var result = new List<(ItemSO item, int count)>(stacks?.Count ?? 0);
            if (stacks == null) return result;
            if (catalog == null)
            {
                GameLog.Error(TAG, "No ItemCatalog — saved items cannot be restored.");
                return result;
            }

            foreach (var stack in stacks)
            {
                if (stack == null || stack.count <= 0) continue;
                var item = catalog.FindById(stack.itemId);
                if (item == null)
                {
                    GameLog.Warn(TAG, $"Unknown itemId '{stack.itemId}' in save — skipped.");
                    continue;
                }
                result.Add((item, stack.count));
            }
            return result;
        }
    }
}
