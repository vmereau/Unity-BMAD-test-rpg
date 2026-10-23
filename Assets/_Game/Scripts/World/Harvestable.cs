using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A fixed world resource (plant, …) the player gathers in place. Unlike <see cref="ItemPickup"/> it is never
    /// the item's drop prefab: no Rigidbody, trigger collider on layer Harvestable. Harvesting adds the yield to the
    /// player inventory and removes the node permanently (marked consumed when it carries a <see cref="SaveableObject"/>).
    /// </summary>
    public class Harvestable : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Harvestable]";
        private const string HARVEST_PROMPT = "Harvest";
        private const string STEAL_PROMPT = "Steal";
        private const string DEFAULT_NAME = "plant";

        [Tooltip("Item added to the player inventory on harvest. Its itemName is the prompt name tag.")]
        [SerializeField] private ItemSO _item;
        [Tooltip("How many items one harvest yields (min 1).")]
        [SerializeField, Min(1)] private int _yieldCount = 1;

        private InventorySystem _inventory;
        private Ownership _ownership; // optional sibling — owned by a living NPC → "Steal"

        public string InteractPrompt => ResolvePrompt(_ownership != null && _ownership.IsIllegal);

        public string NameTag => ResolveNameTag(_item);

        // Disabled by Awake / Start when it can't work (no item / no player inventory) → no prompt, no theft.
        public bool CanInteract => enabled;

        public ItemSO Item => _item;
        public int YieldCount => ClampYield(_yieldCount);

        public static string ResolvePrompt(bool illegal) => illegal ? STEAL_PROMPT : HARVEST_PROMPT;

        public static int ClampYield(int count) => Mathf.Max(1, count);

        public static string ResolveNameTag(ItemSO item) =>
            item != null && !string.IsNullOrEmpty(item.itemName) ? item.itemName : DEFAULT_NAME;

        private void Awake()
        {
            TryGetComponent(out _ownership);
            if (_item == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: _item not assigned — Harvestable disabled");
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled) return;

            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _inventory = player.GetComponentInChildren<InventorySystem>();

            if (_inventory == null)
            {
                GameLog.Error(TAG, "Player InventorySystem not found — Harvestable disabled");
                enabled = false;
            }
        }

        public void Interact()
        {
            if (_inventory == null || _item == null) return;

            int added = 0;
            for (int i = 0; i < YieldCount; i++)
                if (_inventory.AddItem(_item)) added++;
            // Nothing went in → keep the node so the resource isn't lost.
            if (added == 0)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: no {_item.itemName} could be added — harvest skipped");
                return;
            }
            GameLog.Info(TAG, $"Harvested {added}x {_item.itemName} from {gameObject.name}");

            // Scene-authored harvestables carry a SaveableObject — record it so a load doesn't respawn the node.
            if (TryGetComponent<SaveableObject>(out var saveable) && !string.IsNullOrEmpty(saveable.SaveKey))
                SaveSystem.Instance?.MarkConsumed(saveable.SaveKey);
            else
                GameLog.Warn(TAG, $"{gameObject.name}: no save key — it will respawn on load. Run Tools/Save/Validate Save IDs.");
            Destroy(gameObject);
        }
    }
}
