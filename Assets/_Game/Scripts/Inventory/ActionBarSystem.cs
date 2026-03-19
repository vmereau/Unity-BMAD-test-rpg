using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Inventory
{
    public class ActionBarSystem : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private GameEventSO_Int _onActionBarUsed;

        public struct ActionBarSlotData
        {
            public int InventoryIndex;
            public ItemSO Item;
        }

        [SerializeField] private InventorySystem _inventorySystem;
        [SerializeField] private Transform _playerTransform;

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
                _hotkeyHandlers[i] = _ => HandleHotkeyPressed(captured);
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

                if (foundIndex == -1)
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

        private void HandleHotkeyPressed(int slotIndex)
        {
            if (_slots[slotIndex] == null)
            {
                GameLog.Warn(TAG, $"Action bar slot {slotIndex + 1} is empty.");
                return;
            }

            ItemSO item = _slots[slotIndex].Value.Item;
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
    }
}
