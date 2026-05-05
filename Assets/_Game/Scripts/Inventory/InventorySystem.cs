using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Represents a single inventory slot — an item reference paired with a stack count.
    /// Immutable struct: mutate by replacing the slot in the list.
    /// Story 4.10: Introduced to replace the flat List&lt;ItemSO&gt; and support stacking.
    /// </summary>
    public readonly struct InventorySlot
    {
        public readonly ItemSO Item;
        public readonly int Count;

        public InventorySlot(ItemSO item, int count)
        {
            Item = item;
            Count = count;
        }
    }

    [System.Serializable]
    public struct StartingItem
    {
        public ItemSO item;
        public int count;
    }

    public class InventorySystem : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        [SerializeField] private List<StartingItem> _startingItems = new List<StartingItem>();

        private readonly List<InventorySlot> _slots = new List<InventorySlot>();

        public IReadOnlyList<InventorySlot> Items => _slots;

        public int Count => _slots.Count;

        private void Awake()
        {
            foreach (var starting in _startingItems)
            {
                if (starting.item == null) continue;
                
                // Use AddItem logic to handle stacking if necessary, 
                // but since we might want specific stack counts from the start:
                for (int i = 0; i < starting.count; i++)
                {
                    AddItem(starting.item);
                }
            }
        }

        /// <summary>
        /// Adds an item. Stackable items (maxStacks > 1) are merged into an existing slot
        /// if one exists with room. Otherwise a new slot is appended with Count = 1.
        /// </summary>
        public bool AddItem(ItemSO item)
        {
            if (item == null)
            {
                GameLog.Warn(TAG, "AddItem called with null item");
                return false;
            }

            if (item.IsStackable)
            {
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Item == item && _slots[i].Count < item.maxStacks)
                    {
                        _slots[i] = new InventorySlot(item, _slots[i].Count + 1);
                        GameLog.Info(TAG, $"Stacked: {item.itemName} (slot {i}, count={_slots[i].Count})");
                        return true;
                    }
                }
            }

            _slots.Add(new InventorySlot(item, 1));
            GameLog.Info(TAG, $"Picked up: {item.itemName} (total slots: {_slots.Count})");
            return true;
        }

        /// <summary>
        /// Removes the entire slot at the given index regardless of stack count.
        /// Returns the ItemSO that was removed, or null if index is out of range.
        /// </summary>
        public ItemSO RemoveItem(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                GameLog.Warn(TAG, $"RemoveItem: index out of range ({index}, count={_slots.Count})");
                return null;
            }

            var item = _slots[index].Item;
            _slots.RemoveAt(index);
            GameLog.Info(TAG, $"Removed slot: {item.itemName} (total slots: {_slots.Count})");
            return item;
        }

        /// <summary>
        /// Decrements the stack count at the given index by one.
        /// If Count reaches 0 the slot is removed. Returns the ItemSO, or null if out of range.
        /// Use this for UseItem and DropItem — prefer RemoveItem for "remove entire stack" operations.
        /// </summary>
        public ItemSO DecrementStack(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                GameLog.Warn(TAG, $"DecrementStack: index out of range ({index}, count={_slots.Count})");
                return null;
            }

            var existing = _slots[index];
            if (existing.Count > 1)
            {
                _slots[index] = new InventorySlot(existing.Item, existing.Count - 1);
                GameLog.Info(TAG, $"Decremented: {existing.Item.itemName} (count={_slots[index].Count})");
            }
            else
            {
                _slots.RemoveAt(index);
                GameLog.Info(TAG, $"Consumed last: {existing.Item.itemName} (total slots: {_slots.Count})");
            }

            return existing.Item;
        }

        public void MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _slots.Count || toIndex < 0 || toIndex >= _slots.Count)
            {
                GameLog.Warn(TAG, $"MoveItem: index out of range (from={fromIndex}, to={toIndex}, count={_slots.Count})");
                return;
            }
            (_slots[fromIndex], _slots[toIndex]) = (_slots[toIndex], _slots[fromIndex]);
        }
    }
}
