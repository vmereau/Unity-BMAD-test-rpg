using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Inventory
{
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        private const string TAG = "[ItemPickup]";

        [SerializeField] private ItemSO _item;
        [SerializeField] private string _promptOverride = "";

        private InventorySystem _inventory;

        public string InteractPrompt =>
            string.IsNullOrEmpty(_promptOverride)
                ? $"Press E to pick up {_item?.itemName ?? "item"}"
                : _promptOverride;

        private void Awake()
        {
            if (_item == null)
            {
                GameLog.Error(TAG, "_item not assigned — ItemPickup disabled");
                enabled = false;
                return;
            }

            _inventory = FindFirstObjectByType<InventorySystem>();
            if (_inventory == null)
            {
                GameLog.Error(TAG, "InventorySystem not found in scene — ItemPickup disabled");
                enabled = false;
                return;
            }
        }

        public void Interact()
        {
            if (_inventory == null) return;
            _inventory.AddItem(_item);
            gameObject.SetActive(false);
        }
    }
}
