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

        [Tooltip("NPC equipment (NpcArmament): the player-only channel and stats stay unassigned on purpose — no warnings. " +
                 "The NPC refreshes its EquipmentVisuals directly.")]
        [SerializeField] private bool _npcOwned;

        private readonly Dictionary<EquipmentSlot, ItemSO> _equipped = new();

        private void Awake()
        {
            if (_inventorySystem == null)
            {
                GameLog.Error(TAG, "EquipmentSystem: _inventorySystem is not assigned");
                enabled = false;
                return;
            }

            if (_npcOwned) return;

            if (_onEquipmentChanged == null)
                GameLog.Warn(TAG, "EquipmentSystem: _onEquipmentChanged is not assigned — equipment changes will not broadcast");

            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — equipment stat bonuses inactive");
        }

        /// <summary>Equips the item by identity — walks the inventory to find the first matching slot index.</summary>
        public void Equip(ItemSO item)
        {
            if (item == null) { GameLog.Warn(TAG, "Equip(ItemSO): item is null"); return; }
            for (int i = 0; i < _inventorySystem.Count; i++)
            {
                if (_inventorySystem.Items[i].Item == item) { Equip(i); return; }
            }
            GameLog.Warn(TAG, $"Equip(ItemSO): {item.itemName} not found in inventory");
        }

        /// <summary>Equips the item at the given inventory index into the slot <see cref="EquipmentRules.TryGetTargetSlot"/> picks.</summary>
        public void Equip(int inventoryIndex)
        {
            if (!TryGetEquippable(inventoryIndex, out var item)) return;

            if (!EquipmentRules.TryGetTargetSlot(item, _equipped, out EquipmentSlot targetSlot))
            {
                GameLog.Warn(TAG, $"Unknown equippable slot type for {item.itemName}");
                return;
            }
            EquipInto(inventoryIndex, item, targetSlot);
        }

        /// <summary>
        /// Equips the item at the given inventory index into <paramref name="slot"/> (inventory drag onto a slot). Refused
        /// (returns false, nothing changes) when the item doesn't fit that slot. An occupied slot swaps its item back.
        /// </summary>
        public bool Equip(int inventoryIndex, EquipmentSlot slot)
        {
            if (!TryGetEquippable(inventoryIndex, out var item)) return false;
            if (!EquipmentRules.FitsSlot(item, slot))
            {
                GameLog.Info(TAG, $"Equip refused: {item.itemName} doesn't go in {slot}");
                return false;
            }
            EquipInto(inventoryIndex, item, slot);
            return true;
        }

        /// <summary>
        /// Trades the contents of two equipped slots (Ring1 ↔ Ring2), one <c>_onEquipmentChanged</c>. Refused (false) unless
        /// <see cref="EquipmentRules.CanSwap"/> allows it. Stats don't change: the same items stay worn.
        /// </summary>
        public bool SwapEquipped(EquipmentSlot a, EquipmentSlot b)
        {
            if (!EquipmentRules.CanSwap(_equipped, a, b))
            {
                GameLog.Info(TAG, $"Swap refused: {a} ↔ {b}");
                return false;
            }
            ItemSO itemA = GetEquipped(a);
            ItemSO itemB = GetEquipped(b);
            _equipped.Remove(a);
            _equipped.Remove(b);
            if (itemB != null) _equipped[a] = itemB;
            if (itemA != null) _equipped[b] = itemA;
            GameLog.Info(TAG, $"Swapped {a} ↔ {b}");
            _onEquipmentChanged?.Raise(true);
            return true;
        }

        private bool TryGetEquippable(int inventoryIndex, out ItemSO item)
        {
            item = null;
            if (inventoryIndex < 0 || inventoryIndex >= _inventorySystem.Count)
            {
                GameLog.Warn(TAG, $"Equip: inventoryIndex {inventoryIndex} is out of range");
                return false;
            }

            item = _inventorySystem.Items[inventoryIndex].Item;
            if (item is not EquipableItemSO)
            {
                GameLog.Warn(TAG, "Item is not equippable");
                return false;
            }
            return true;
        }

        private void EquipInto(int inventoryIndex, ItemSO item, EquipmentSlot targetSlot)
        {
            // Return displaced items to the inventory first (AddItem before RemoveItem: appending never shifts the index).
            // A two-handed weapon / bow and a shield exclude each other (spec-shield-offhand-blocking).
            // Each displaced item leaves _equipped before AddItem, so inventory listeners never see it in both places.
            // (InventorySystem has no capacity limit: AddItem only refuses null / gold, never an equipped item.)
            if (EquipmentRules.TryGetConflictingSlot(item, _equipped, out EquipmentSlot conflictSlot)
                && _equipped.TryGetValue(conflictSlot, out var conflicting))
            {
                _equipped.Remove(conflictSlot);
                _inventorySystem.AddItem(conflicting);
                GameLog.Info(TAG, $"Unequipped {conflicting.itemName} from {conflictSlot} — {item.itemName} needs it free");
            }
            if (_equipped.TryGetValue(targetSlot, out var oldItem))
            {
                _equipped.Remove(targetSlot);
                _inventorySystem.AddItem(oldItem);
                GameLog.Info(TAG, $"Unequipped {oldItem.itemName} from {targetSlot}");
            }

            // Equipped before it leaves the inventory: the inventory-changed listeners (action bar ValidateSlots) must see it
            // in one of the two places, or the bar drops its slot (spec-action-bar-equip-swap).
            _equipped[targetSlot] = item;
            _inventorySystem.RemoveItem(inventoryIndex);
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

        /// <summary>Every occupied slot (save capture).</summary>
        public IReadOnlyDictionary<EquipmentSlot, ItemSO> Equipped => _equipped;

        /// <summary>
        /// Save/load: replaces the equipped set without touching the inventory (equipped items are not in
        /// the inventory list — Equip removes them). Skips null / non-equippable items, then refreshes
        /// visuals (OnEquipmentChanged) and stat bonuses.
        /// </summary>
        public void RestoreEquipped(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped)
        {
            _equipped.Clear();
            if (equipped != null)
            {
                foreach (var pair in equipped)
                {
                    if (!EquipmentRules.FitsSlot(pair.Value, pair.Key))
                    {
                        if (pair.Value != null)
                            GameLog.Warn(TAG, $"RestoreEquipped: {pair.Value.itemName} cannot go in {pair.Key} — skipped");
                        continue;
                    }
                    _equipped[pair.Key] = pair.Value;
                }
                if (EquipmentRules.HasOffhandConflict(_equipped))
                {
                    // The inventory is restored first (PlayerSaveAdapter / NPC SaveableObject): the item goes back into it.
                    ItemSO offhand = _equipped[EquipmentSlot.Offhand];
                    GameLog.Warn(TAG, $"RestoreEquipped: {offhand.itemName} cannot be held next to " +
                                      $"{_equipped[EquipmentSlot.Weapon].itemName} (both hands) — moved to the inventory");
                    _equipped.Remove(EquipmentSlot.Offhand);
                    _inventorySystem.AddItem(offhand);
                }
            }
            _onEquipmentChanged?.Raise(true);
            RecomputeAndApplyBonuses();
        }

        /// <summary>Returns the item equipped in the given slot, or null if empty.</summary>
        public ItemSO GetEquipped(EquipmentSlot slot)
            => _equipped.TryGetValue(slot, out var item) ? item : null;

        /// <summary>The shield in the Offhand slot, or null.</summary>
        public ShieldSO EquippedShield => GetEquipped(EquipmentSlot.Offhand) as ShieldSO;

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
