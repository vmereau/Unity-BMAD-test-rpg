using System;
using System.Collections.Generic;
using Game.Inventory;

namespace Game.Core
{
    /// <summary>
    /// Converts equipped slots to / from their save form (slot name → itemId) via the item catalog.
    /// Shared by the player (<c>PlayerSaveData.equipped</c>) and NPCs (<c>ObjectSaveData.equipped</c>).
    /// </summary>
    public static class EquipmentSaveConverter
    {
        private const string TAG = "[Save]";

        public static Dictionary<string, string> ToSave(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped)
        {
            var result = new Dictionary<string, string>();
            if (equipped == null) return result;
            foreach (var pair in equipped)
            {
                if (pair.Value == null) continue;
                if (string.IsNullOrEmpty(pair.Value.itemId))
                {
                    GameLog.Warn(TAG, $"Equipped item '{pair.Value.name}' has no itemId — not saved. Run Tools/Save/Sync Item Catalog.");
                    continue;
                }
                result[pair.Key.ToString()] = pair.Value.itemId;
            }
            return result;
        }

        /// <summary>Resolves saved slots. Unknown slot names and unknown itemIds are skipped with a warning.</summary>
        public static Dictionary<EquipmentSlot, ItemSO> FromSave(Dictionary<string, string> saved, ItemCatalogSO catalog)
        {
            var result = new Dictionary<EquipmentSlot, ItemSO>();
            if (saved == null) return result;
            if (catalog == null)
            {
                GameLog.Error(TAG, "No ItemCatalog — saved equipment cannot be restored.");
                return result;
            }
            foreach (var pair in saved)
            {
                if (!Enum.TryParse(pair.Key, out EquipmentSlot slot) || !Enum.IsDefined(typeof(EquipmentSlot), slot))
                {
                    GameLog.Warn(TAG, $"Unknown equipment slot '{pair.Key}' in save — skipped");
                    continue;
                }
                var item = catalog.FindById(pair.Value);
                if (item == null)
                {
                    GameLog.Warn(TAG, $"Unknown itemId '{pair.Value}' equipped in {slot} — skipped");
                    continue;
                }
                result[slot] = item;
            }
            return result;
        }
    }
}
