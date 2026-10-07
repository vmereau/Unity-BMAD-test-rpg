using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Inventory
{
    /// <summary>
    /// Every <see cref="ItemSO"/> in the project, resolvable by its stable <see cref="ItemSO.itemId"/>.
    /// Kept in sync by the editor auto-sync (<c>Tools/Save/Sync Item Catalog</c>). Used by the save
    /// system to turn saved item IDs back into assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Item Catalog", fileName = "ItemCatalog")]
    public class ItemCatalogSO : ScriptableObject
    {
        private const string TAG = "[ItemCatalog]";

        [SerializeField] private List<ItemSO> _items = new List<ItemSO>();

        private Dictionary<string, ItemSO> _byId;
        private int _indexedCount = -1;

        /// <summary>May contain null entries — consumers must skip them.</summary>
        public IReadOnlyList<ItemSO> Items => _items;

        /// <summary>Returns the item with this ID, or null when the ID is empty or unknown.</summary>
        public ItemSO FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null || _indexedCount != _items.Count) BuildIndex();
            return _byId.TryGetValue(id, out var item) ? item : null;
        }

        private void BuildIndex()
        {
            _byId = new Dictionary<string, ItemSO>(_items.Count);
            _indexedCount = _items.Count;
            foreach (var item in _items)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                if (_byId.TryGetValue(item.itemId, out var existing))
                {
                    GameLog.Warn(TAG, $"Duplicate itemId '{item.itemId}' on '{existing.name}' and '{item.name}' — keeping '{existing.name}'.");
                    continue;
                }
                _byId.Add(item.itemId, item);
            }
        }

        // Asset reloads and list edits (inspector, auto-sync) must not serve a stale index.
        private void OnEnable() => _byId = null;

#if UNITY_EDITOR
        private void OnValidate() => _byId = null;
#endif
    }
}
