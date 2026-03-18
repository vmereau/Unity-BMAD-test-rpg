using UnityEngine;
using Game.Core;
using Game.Inventory;

namespace Game.UI
{
    public class ActionBarUI : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private ActionBarSystem _actionBarSystem;
        [SerializeField] private ActionBarSlotUI[] _slotUIs;
        [SerializeField] private InventorySystem _inventorySystem;

        private void Awake()
        {
            if (_actionBarSystem == null)
            {
                GameLog.Error(TAG, "ActionBarUI: _actionBarSystem is not assigned.");
                return;
            }
            if (_slotUIs == null || _slotUIs.Length != 6)
            {
                GameLog.Error(TAG, "ActionBarUI: _slotUIs must have exactly 6 elements.");
                return;
            }

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                if (_slotUIs[i] != null)
                    _slotUIs[i].Initialize(i, this);
            }
        }

        private void OnEnable()
        {
            if (_actionBarSystem != null)
                _actionBarSystem.OnActionBarUsed += HandleActionBarUsed;
        }

        private void OnDisable()
        {
            if (_actionBarSystem != null)
                _actionBarSystem.OnActionBarUsed -= HandleActionBarUsed;
        }

        private void HandleActionBarUsed(int _)
        {
            Refresh();
        }

        public void Refresh()
        {
            _actionBarSystem.ValidateSlots();
            for (int i = 0; i < 6; i++)
            {
                var data = _actionBarSystem.GetSlot(i);
                if (data == null)
                {
                    _slotUIs[i].BindEmpty(i);
                }
                else
                {
                    int stackCount = GetStackCount(data.Value.InventoryIndex, data.Value.Item);
                    _slotUIs[i].Bind(i, data.Value.Item, data.Value.InventoryIndex, stackCount);
                }
            }
        }

        private int GetStackCount(int invIndex, Game.Inventory.ItemSO item)
        {
            if (_inventorySystem == null) return 0;
            var items = _inventorySystem.Items;
            if (invIndex < 0 || invIndex >= items.Count) return 0;
            if (items[invIndex].Item != item) return 0;
            return items[invIndex].Count;
        }

        public void OnInventorySlotDroppedOnActionBar(int inventorySlotIndex, Game.Inventory.ItemSO item, int actionBarSlotIndex)
        {
            _actionBarSystem.Assign(actionBarSlotIndex, inventorySlotIndex, item);
            Refresh();
        }

        public void OnActionBarSlotSwap(int fromSlot, int toSlot)
        {
            var fromData = _actionBarSystem.GetSlot(fromSlot);
            var toData = _actionBarSystem.GetSlot(toSlot);

            _actionBarSystem.ClearSlot(fromSlot);
            _actionBarSystem.ClearSlot(toSlot);

            if (toData != null)
                _actionBarSystem.Assign(fromSlot, toData.Value.InventoryIndex, toData.Value.Item);
            if (fromData != null)
                _actionBarSystem.Assign(toSlot, fromData.Value.InventoryIndex, fromData.Value.Item);

            Refresh();
        }

        public void OnActionBarSlotClearedByDragToInventory(int actionBarSlotIndex)
        {
            _actionBarSystem.ClearSlot(actionBarSlotIndex);
            Refresh();
        }
    }
}
