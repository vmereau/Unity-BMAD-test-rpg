using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Inventory
{
    public class EquipmentSystem : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private GameEventSO_Void _onEquipmentChanged;

        // TODO(Epic7-tech-debt): Cross-system direct refs (Game.Inventory → Game.Player).
        // Prototype exception. Replace with a StatBonusProvider event channel.
        [SerializeField] private PlayerStats _playerStats;

        private readonly Dictionary<EquipmentSlot, ItemSO> _equipped = new();

        private void Awake()
        {
            if (_inventorySystem == null)
            {
                GameLog.Error(TAG, "EquipmentSystem: _inventorySystem is not assigned");
                enabled = false;
                return;
            }

            if (_onEquipmentChanged == null)
                GameLog.Warn(TAG, "EquipmentSystem: _onEquipmentChanged is not assigned — equipment changes will not broadcast");

            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — equipment stat bonuses inactive");
        }

        /// <summary>Equips the item at the given inventory index.</summary>
        public void Equip(int inventoryIndex)
        {
            if (inventoryIndex < 0 || inventoryIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Equip: inventoryIndex {inventoryIndex} is out of range");
                return;
            }

            var item = _inventorySystem.Items[inventoryIndex].Item;

            if (item is not EquipableItemSO)
            {
                GameLog.Warn(TAG, "Item is not equippable");
                return;
            }

            EquipmentSlot targetSlot;

            if (item is WeaponSO)
            {
                targetSlot = EquipmentSlot.Weapon;
            }
            else if (item is ArmorSO armor)
            {
                if (armor.slot == EquipmentSlot.Ring1)
                {
                    if (!_equipped.ContainsKey(EquipmentSlot.Ring1))
                        targetSlot = EquipmentSlot.Ring1;
                    else if (!_equipped.ContainsKey(EquipmentSlot.Ring2))
                        targetSlot = EquipmentSlot.Ring2;
                    else
                        targetSlot = EquipmentSlot.Ring1; // bump Ring1 back to inventory below
                }
                else
                {
                    targetSlot = armor.slot;
                }
            }
            else
            {
                GameLog.Warn(TAG, "Unknown equippable slot type");
                return;
            }

            // If target slot is occupied, return old item to inventory first (AddItem before RemoveItem to avoid index drift)
            if (_equipped.TryGetValue(targetSlot, out var oldItem))
            {
                _inventorySystem.AddItem(oldItem);
                GameLog.Info(TAG, $"Unequipped {oldItem.itemName} from {targetSlot}");
            }

            // Remove new item from inventory AFTER potential AddItem to keep indices stable
            _inventorySystem.RemoveItem(inventoryIndex);

            _equipped[targetSlot] = item;
            GameLog.Info(TAG, $"Equipped {item.itemName} to {targetSlot}");
            _onEquipmentChanged?.Raise(true);
            RecomputeAndApplyBonuses();
        }

        /// <summary>Unequips the item in the given slot, returning it to the inventory.</summary>
        public void Unequip(EquipmentSlot slot)
        {
            if (!_equipped.TryGetValue(slot, out var item))
            {
                GameLog.Warn(TAG, $"Unequip: slot {slot} is empty");
                return;
            }

            _inventorySystem.AddItem(item);
            _equipped.Remove(slot);
            GameLog.Info(TAG, $"Unequipped {item.itemName} from {slot}");
            _onEquipmentChanged?.Raise(true);
            RecomputeAndApplyBonuses();
        }

        /// <summary>Returns the item equipped in the given slot, or null if empty.</summary>
        public ItemSO GetEquipped(EquipmentSlot slot)
            => _equipped.TryGetValue(slot, out var item) ? item : null;

        /// <summary>Returns true if the item can be equipped.</summary>
        public bool IsEquippable(ItemSO item) => item is EquipableItemSO;

        /// <summary>Returns true if the item is currently equipped in any slot.</summary>
        public bool IsEquipped(ItemSO item)
        {
            if(!IsEquippable(item)) return false;

            foreach (var equipped in _equipped.Values)
            {
                if (equipped == item) return true;
            }
            return false;
        }

        /// <summary>Flat damage bonus from the currently equipped weapon (0 if no weapon or damageBonus is 0).</summary>
        public float GetWeaponDamageBonus()
            => GetEquipped(EquipmentSlot.Weapon) is WeaponSO w ? w.damageBonus : 0f;

        private void RecomputeAndApplyBonuses()
        {
            if (_playerStats == null) return;
            int str = 0, dex = 0, end = 0, intl = 0, def = 0;
            foreach (var item in _equipped.Values)
            {
                if (item is not EquipableItemSO eq) continue;
                str += eq.strengthBonus;
                dex += eq.dexterityBonus;
                end += eq.enduranceBonus;
                intl += eq.intelligenceBonus;
                def += eq.defenseBonus;
            }
            _playerStats.ApplyEquipmentBonuses(str, dex, end, intl, def);
        }
    }
}
