using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Inventory
{
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        private const string TAG = "[ItemPickup]";
        private const string PICK_UP_PROMPT = "Pick Up";
        private const string STEAL_PROMPT = "Steal";

        [SerializeField] private ItemSO _item;
        [SerializeField] private string _promptOverride = "";

        private InventorySystem _inventory;
        private Ownership _ownership; // optional sibling — owned by a living NPC → "Steal"

        public string InteractPrompt =>
            _ownership != null && _ownership.IsIllegal
                ? STEAL_PROMPT
                : string.IsNullOrEmpty(_promptOverride) ? PICK_UP_PROMPT : _promptOverride;

        public string NameTag => _item?.itemName ?? "item";

        // Disabled by Awake / Start when it can't work (no item / no player inventory) → no prompt, no theft.
        public bool CanInteract => enabled;

        public ItemSO Item => _item;

        public void Configure(ItemSO item)
        {
            _item = item;
        }

        private void Awake()
        {
            TryGetComponent(out _ownership);
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
            // Scene-authored pickups carry a SaveableObject — record the pickup so a load doesn't respawn it.
            if (TryGetComponent<SaveableObject>(out var saveable))
                SaveSystem.Instance?.MarkConsumed(saveable.SaveKey);
            Destroy(gameObject);
        }
    }
}
