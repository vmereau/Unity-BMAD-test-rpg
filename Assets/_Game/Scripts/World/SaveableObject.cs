using Game.AI;
using Game.Core;
using Game.Economy;
using Game.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Game.World
{
    /// <summary>
    /// Makes a world object's runtime state persist through save / load. Captures whichever siblings
    /// exist: InventorySystem, GoldSystem, Lockable, DoorInteractable, and for entities EntityHealth
    /// (dead state + root transform).
    /// Key: entities use their PersistentID's KilledFact GUID; containers, doors and scene-authored
    /// ItemPickups use <see cref="_saveId"/>, assigned on the scene instance (Tools/Save/Validate Save IDs).
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveableObject : MonoBehaviour, ISaveable
    {
        private const string TAG = "[Save]";

        [Tooltip("Unique per scene instance. Leave empty on entities (their KilledFact GUID is the key) and on prefab assets.")]
        [SerializeField] private string _saveId;

        private InventorySystem _inventory;
        private GoldSystem _gold;
        private Lockable _lockable;
        private DoorInteractable _door;
        private EntityHealth _entityHealth;
        private PersistentID _persistentID;
        private bool _cached;

        public string SaveId => _saveId;

        public string SaveKey
        {
            get
            {
                // Uncached until Awake so edit-mode tools (validator, inspector) see component changes.
                var pid = _cached ? _persistentID : GetComponent<PersistentID>();
                var fact = pid != null ? pid.KilledFact : null;
                if (fact != null && !string.IsNullOrEmpty(fact.EntityGuid)) return fact.EntityGuid;
                return _saveId;
            }
        }

        private void Awake() => CacheSiblings();

        // Also called lazily by Capture / Restore in case they run before Awake (inactive objects).
        private void CacheSiblings()
        {
            _inventory = GetComponent<InventorySystem>();
            _gold = GetComponent<GoldSystem>();
            _lockable = GetComponent<Lockable>();
            _door = GetComponent<DoorInteractable>();
            _entityHealth = GetComponent<EntityHealth>();
            _persistentID = GetComponent<PersistentID>();
            _cached = true;
        }

        public ObjectSaveData Capture()
        {
            if (!_cached) CacheSiblings();
            var data = new ObjectSaveData();
            if (_inventory != null) data.inventory = ItemStackConverter.ToSave(_inventory.Items);
            if (_gold != null) data.gold = _gold.Gold;
            if (_lockable != null) data.isLocked = _lockable.IsLocked;
            if (_door != null) data.isOpen = _door.IsOpen;
            if (_entityHealth != null)
            {
                data.hasTransform = true;
                data.position = SVector3.From(transform.position);
                data.rotation = SQuaternion.From(transform.rotation);
            }
            return data;
        }

        /// <summary>
        /// Writes saved state back. Must run after PersistentID.Start() has hidden killed entities —
        /// a killed entity with loot left is re-activated here as a corpse.
        /// </summary>
        public void Restore(ObjectSaveData data)
        {
            if (data == null) return;
            if (!_cached) CacheSiblings();

            if (_inventory != null && data.inventory != null)
                _inventory.RestoreSlots(ItemStackConverter.FromSave(data.inventory, SaveSystem.Instance?.Catalog));
            if (_gold != null && data.gold.HasValue) _gold.RestoreGold(data.gold.Value);
            if (_lockable != null && data.isLocked.HasValue) _lockable.RestoreLocked(data.isLocked.Value);
            if (_door != null && data.isOpen.HasValue) _door.SetOpenImmediate(data.isOpen.Value);

            // Alive entities keep their authored spawn position — AI state is not persisted.
            if (_entityHealth == null || !IsKilled()) return;

            // No saved inventory, or looted dry → stays hidden (never fall back to authored starting loot).
            if (_inventory == null || data.inventory == null || _inventory.Count == 0) return;

            gameObject.SetActive(true);
            if (data.hasTransform) PlaceAt(data.position.To(), data.rotation.To());
            _entityHealth.RestoreAsCorpse();
        }

        private bool IsKilled()
        {
            var fact = _persistentID != null ? _persistentID.KilledFact : null;
            return fact != null && WorldStateManager.Instance != null && WorldStateManager.Instance.IsKilled(fact);
        }

        // Warp keeps the NavMeshAgent valid (EntityBrain.TransitionToDead sets isStopped on it next tick);
        // the saved root position came from an agent-driven body, so it lies on the mesh.
        private void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (TryGetComponent<NavMeshAgent>(out var agent) && agent.isActiveAndEnabled && agent.Warp(position))
            {
                transform.rotation = rotation;
                return;
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
