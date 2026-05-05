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
            string.IsNullOrEmpty(_promptOverride) ? "Pick Up" : _promptOverride;

        public string NameTag => _item?.itemName ?? "item";

        public void Configure(ItemSO item)
        {
            _item = item;
        }

        private void Awake()
        {
            if (_item == null)
            {
                GameLog.Error(TAG, "_item not assigned — ItemPickup disabled");
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled) return;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                _inventory = player.GetComponentInChildren<InventorySystem>();
            }

            if (_inventory == null)
            {
                GameLog.Error(TAG, "Player InventorySystem not found — ItemPickup disabled");
                enabled = false;
            }
        }

        public void Interact()
        {
            if (_inventory == null) return;
            _inventory.AddItem(_item);
            Destroy(gameObject);
        }
    }
}
