using _Game.ScriptableObjects.Entities;
using Game.AI;
using Game.Core;
using Game.Inventory;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Base interactable surface for ANY entity (NPC, monster, …). Provides the generic
    /// IInteractable plumbing — name tag, alive/out-of-combat gating helper, and a no-op
    /// Interact. Lives on Entity_base.prefab so every entity is discoverable by
    /// InteractionSystem and shows its world-space EntityUI on hover. Subclasses
    /// (e.g. NPCPresence) override Interact()/InteractPrompt/CanInteract to add behaviour
    /// (dialogue now; loot-corpse in a future spec).
    /// </summary>
    public class EntityPresence : MonoBehaviour, IInteractable
    {
        private const string TAG = "[Entity]";

        [SerializeField] protected PersistentID _persistentID;
        [SerializeField] private GameEventSO_ContainerOpenRequest _onLootRequested;

        protected Entity Data => _persistentID != null ? _persistentID.Entity : null;

        protected EntityHealth _entityHealth;
        protected ICombatStateProvider _combatState;
        private InventorySystem _inventory;

        /// <summary>True when this entity has died (corpse remains in the scene as a loot target).</summary>
        protected bool IsDead => _entityHealth != null && _entityHealth.IsDead;

        /// <summary>True when this entity carries at least one item to loot.</summary>
        protected bool HasLoot => _inventory != null && _inventory.Count > 0;

        /// <summary>A dead body is lootable only while it still holds items — an empty corpse is inert.</summary>
        protected bool IsLootable => IsDead && HasLoot;

        // A non-empty dead body is lootable; an alive (or emptied) base entity offers no prompt.
        public virtual string InteractPrompt => IsLootable ? "Loot" : string.Empty;

        // Null-guarded: the name-tag scan can reach this via GetComponentInParent even when
        // the component is disabled (GetComponentInParent ignores `enabled`).
        public string NameTag => Data != null ? Data.entityName : string.Empty;

        // Base: only a corpse with loot is interactable. Alive base entities (and emptied corpses)
        // show name/HP UI but no `[E]` prompt because the name-tag scan (scan-2) ignores CanInteract.
        public virtual bool CanInteract => IsLootable;

        protected virtual void Awake()
        {
            // PersistentID lives on the same root on Entity_base.prefab; resolve it when the
            // serialized reference is left unwired so Data still resolves.
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (Data == null)
            {
                GameLog.Error(TAG, $"EntityPresence on {gameObject.name} has no Entity assigned (PersistentID.Entity is null)");
                enabled = false;
                return;
            }
            _entityHealth = GetComponent<EntityHealth>();
            _combatState  = GetComponent<ICombatStateProvider>(); // null-safe: entities without a brain are never in combat
            _inventory    = GetComponent<InventorySystem>();
        }

        // A dead body with loot opens its inventory as a take-only container; otherwise nothing.
        public virtual void Interact()
        {
            if (IsLootable) OpenLoot();
        }

        /// <summary>Opens the dead entity's inventory as a take-only container (corpse loot).</summary>
        protected void OpenLoot()
        {
            if (_onLootRequested == null)
            {
                GameLog.Warn(TAG, $"No loot event assigned on {gameObject.name} — cannot loot corpse");
                return;
            }
            if (_inventory == null)
            {
                GameLog.Warn(TAG, $"No InventorySystem on {gameObject.name} — nothing to loot");
                return;
            }
            _onLootRequested.Raise(new ContainerOpenRequestData
            {
                containerInventory = _inventory,
                isLocked = false,
                requiredSkillId = null,
                takeOnly = true
            });
        }

        /// <summary>True when this entity is alive and not currently in combat.</summary>
        protected bool IsAliveAndOutOfCombat =>
            (_entityHealth == null || !_entityHealth.IsDead) &&
            (_combatState  == null || !_combatState.IsInCombat);
    }
}
