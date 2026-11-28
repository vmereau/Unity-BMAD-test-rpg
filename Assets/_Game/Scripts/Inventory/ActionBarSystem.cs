using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Player;

namespace Game.Inventory
{
    public class ActionBarSystem : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        /// <summary>
        /// OnActionBarUsed payload meaning "slots changed, refresh" (no slot was used). Every listener must
        /// treat it as refresh-only — raised by <see cref="RestoreSlots"/> after a load.
        /// </summary>
        public const int REFRESH_ONLY_SLOT = -1;

        /// <summary><see cref="ActionBarSlotData.InventoryIndex"/> of a slot whose item is equipped (not in the inventory).</summary>
        public const int EQUIPPED_INDEX = -1;

        [SerializeField] private GameEventSO_Int _onActionBarUsed;
        [SerializeField] private PlayerStateManager _playerStateManager;

        public struct ActionBarSlotData
        {
            public int InventoryIndex;
            public ItemSO Item;
        }

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private Transform _playerTransform;

        [Tooltip("Equips equippable items used from the bar; also keeps the slot of an equipped item (it left the inventory).")]
        [SerializeField] private EquipmentSystem _equipmentSystem;
        [Tooltip("Raised with the item when a weapon / shield is used from the bar while drawn: PlayerCombat sheathes, equips, " +
                 "then draws. Unassigned → the item is equipped at once.")]
        [SerializeField] private GameEventSO_Item _onEquipSwapRequested;

        private readonly ActionBarSlotData?[] _slots = new ActionBarSlotData?[6];
        private InputSystem_Actions _input;
        private System.Action<InputAction.CallbackContext>[] _hotkeyHandlers;

        private void Awake()
        {
            if (_inventorySystem == null)
            {
                GameLog.Error(TAG, "ActionBarSystem: _inventorySystem is not assigned. Disabling.");
                enabled = false;
                return;
            }
            if (_playerTransform == null)
            {
                GameLog.Error(TAG, "ActionBarSystem: _playerTransform is not assigned. Disabling.");
                enabled = false;
                return;
            }

            _input = new InputSystem_Actions();

            _hotkeyHandlers = new System.Action<InputAction.CallbackContext>[6];
            for (int i = 0; i < 6; i++)
            {
                int captured = i;
                _hotkeyHandlers[i] = _ => TryUseSlot(captured);
            }
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.Player.ActionBar1.performed += _hotkeyHandlers[0];
            _input.Player.ActionBar2.performed += _hotkeyHandlers[1];
            _input.Player.ActionBar3.performed += _hotkeyHandlers[2];
            _input.Player.ActionBar4.performed += _hotkeyHandlers[3];
            _input.Player.ActionBar5.performed += _hotkeyHandlers[4];
            _input.Player.ActionBar6.performed += _hotkeyHandlers[5];
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.ActionBar1.performed -= _hotkeyHandlers[0];
            _input.Player.ActionBar2.performed -= _hotkeyHandlers[1];
            _input.Player.ActionBar3.performed -= _hotkeyHandlers[2];
            _input.Player.ActionBar4.performed -= _hotkeyHandlers[3];
            _input.Player.ActionBar5.performed -= _hotkeyHandlers[4];
            _input.Player.ActionBar6.performed -= _hotkeyHandlers[5];
            _input.Player.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        public void Assign(int slotIndex, int inventoryIndex, ItemSO item)
        {
            if (slotIndex < 0 || slotIndex >= 6)
            {
                GameLog.Warn(TAG, $"Assign: slotIndex {slotIndex} is out of range [0,5].");
                return;
            }
            if (item != null && !ItemSO.CanPlayerPart(item))
            {
                GameLog.Warn(TAG, $"Assign: {item.itemName} is a quest item or a key — it can't go on the action bar.");
                return;
            }
            _slots[slotIndex] = new ActionBarSlotData { InventoryIndex = inventoryIndex, Item = item };
            GameLog.Info(TAG, $"Assigned slot {slotIndex + 1}: {item?.name ?? "null"} at inventory index {inventoryIndex}.");
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6)
            {
                GameLog.Warn(TAG, $"ClearSlot: slotIndex {slotIndex} is out of range [0,5].");
                return;
            }
            _slots[slotIndex] = null;
            GameLog.Info(TAG, $"Cleared action bar slot {slotIndex + 1}.");
        }

        public ActionBarSlotData? GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 6)
            {
                GameLog.Warn(TAG, $"GetSlot: slotIndex {slotIndex} is out of range [0,5].");
                return null;
            }
            return _slots[slotIndex];
        }

        /// <summary>
        /// Save/load: clears all six slots, assigns the saved ones, re-validates them against the
        /// (already restored) inventory, then raises OnActionBarUsed(<see cref="REFRESH_ONLY_SLOT"/>) so the
        /// bar and inventory UIs refresh.
        /// </summary>
        public void RestoreSlots(IEnumerable<(int slot, int invIndex, ItemSO item)> slots)
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            if (slots != null)
                foreach (var (slot, invIndex, item) in slots)
                    if (item != null) Assign(slot, invIndex, item);
            if (_inventorySystem != null) ValidateSlots();
            _onActionBarUsed?.Raise(REFRESH_ONLY_SLOT);
        }

        public void ValidateSlots()
        {
            var items = _inventorySystem.Items;
            for (int i = 0; i < 6; i++)
            {
                if (_slots[i] == null) continue;
                var slot = _slots[i].Value;

                int foundIndex = -1;
                for (int j = 0; j < items.Count; j++)
                {
                    if (items[j].Item == slot.Item)
                    {
                        foundIndex = j;
                        break;
                    }
                }

                if (foundIndex == -1 && _equipmentSystem != null && _equipmentSystem.IsEquipped(slot.Item))
                {
                    // Equipped items leave the inventory: keep the slot so the bar can swap back to it.
                    if (slot.InventoryIndex != EQUIPPED_INDEX)
                        _slots[i] = new ActionBarSlotData { InventoryIndex = EQUIPPED_INDEX, Item = slot.Item };
                }
                else if (foundIndex == -1)
                {
                    GameLog.Warn(TAG, $"ValidateSlots: Slot {i + 1} stale (item removed). Clearing.");
                    _slots[i] = null;
                }
                else if (foundIndex != slot.InventoryIndex)
                {
                    _slots[i] = new ActionBarSlotData { InventoryIndex = foundIndex, Item = slot.Item };
                }
            }
        }

        /// <summary>
        /// True when the slot's item is equipped and no copy of it is left in the inventory (the bar highlights it). A second
        /// copy (ring, duplicate weapon) still in the inventory keeps the slot usable.
        /// </summary>
        public bool IsSlotEquipped(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex] == null || _equipmentSystem == null) return false;
            var slot = _slots[slotIndex].Value;
            return slot.InventoryIndex == EQUIPPED_INDEX && _equipmentSystem.IsEquipped(slot.Item);
        }

        /// <summary>
        /// Attempts to use the item in the given action bar slot.
        /// Blocked while the player is in dialogue or staggered / knocked down.
        /// Called by both hotkey handlers and the UI click path.
        /// </summary>
        public void TryUseSlot(int slotIndex)
        {
            // DialogueEndedThisFrame: the number key that closed a dialogue (Farewell / forced line) is also ActionBarN.
            if (_playerStateManager != null && (_playerStateManager.IsInDialogue || _playerStateManager.DialogueEndedThisFrame
                                                || _playerStateManager.IsIncapacitated || _playerStateManager.IsExecuting)) return;
            HandleHotkeyPressed(slotIndex);
        }

        private void HandleHotkeyPressed(int slotIndex)
        {
            // Re-sync indices first in case slots shifted since the last refresh (cheap; the HUD also
            // re-validates on OnPlayerInventoryChanged).
            if (_inventorySystem == null) return;
            ValidateSlots();

            if (_slots[slotIndex] == null)
            {
                GameLog.Warn(TAG, $"Action bar slot {slotIndex + 1} is empty.");
                return;
            }

            ItemSO item = _slots[slotIndex].Value.Item;
            if (item is EquipableItemSO)
            {
                if (TryEquipFromBar(slotIndex, item)) _onActionBarUsed?.Raise(slotIndex);
                return;
            }
            if (item is not UsableItemSO usable)
            {
                GameLog.Warn(TAG, $"Slot {slotIndex + 1} item is not usable.");
                return;
            }

            bool used = usable.OnUse(_playerTransform.gameObject);
            if (used && usable.consumable)
            {
                _inventorySystem.DecrementStack(_slots[slotIndex].Value.InventoryIndex);
            }

            _onActionBarUsed?.Raise(slotIndex);
        }

        // Equips in place of the current item (or into the empty slot). Already equipped → nothing. While drawn, a weapon /
        // shield goes through PlayerCombat (sheathe → equip → draw); everything else equips at once.
        private bool TryEquipFromBar(int slotIndex, ItemSO item)
        {
            if (_equipmentSystem == null)
            {
                GameLog.Warn(TAG, $"ActionBarSystem: _equipmentSystem is not assigned — can't equip {item.itemName} from the bar.");
                return false;
            }
            if (IsSlotEquipped(slotIndex)) return false;
            if (_playerStateManager != null && !_playerStateManager.CanChangeEquipment())
            {
                GameLog.Info(TAG, $"Can't change equipment now (attacking, blocking, aiming, dodging, airborne, drinking or drawing) — {item.itemName} not equipped.");
                return false;
            }

            bool drawn = _playerStateManager != null && _playerStateManager.IsInCombat;
            if (drawn && (item is WeaponSO || item is ShieldSO) && _onEquipSwapRequested != null)
            {
                _onEquipSwapRequested.Raise(item);
                return true;
            }

            _equipmentSystem.Equip(item);
            return true;
        }
    }
}
