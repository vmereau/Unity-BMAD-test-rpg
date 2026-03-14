using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    public class InventorySystem : MonoBehaviour
    {
        private const string TAG = "[Inventory]";

        private readonly List<ItemSO> _items = new List<ItemSO>();

        public IReadOnlyList<ItemSO> Items => _items;

        public int Count => _items.Count;

        public bool AddItem(ItemSO item)
        {
            if (item == null)
            {
                GameLog.Warn(TAG, "AddItem called with null item");
                return false;
            }

            _items.Add(item);
            GameLog.Info(TAG, $"Picked up: {item.itemName} (total: {_items.Count})");
            return true;
        }

        public ItemSO RemoveItem(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                GameLog.Warn(TAG, $"RemoveItem: index out of range ({index}, count={_items.Count})");
                return null;
            }

            var item = _items[index];
            _items.RemoveAt(index);
            GameLog.Info(TAG, $"Removed: {item.itemName} (total: {_items.Count})");
            return item;
        }

        public void MoveItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _items.Count || toIndex < 0 || toIndex >= _items.Count)
            {
                GameLog.Warn(TAG, $"MoveItem: index out of range (from={fromIndex}, to={toIndex}, count={_items.Count})");
                return;
            }
            (_items[fromIndex], _items[toIndex]) = (_items[toIndex], _items[fromIndex]);
        }
    }
}
