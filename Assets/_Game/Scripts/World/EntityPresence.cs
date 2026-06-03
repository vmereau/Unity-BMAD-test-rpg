using _Game.ScriptableObjects.Entities;
using Game.AI;
using Game.Core;
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

        protected Entity Data => _persistentID != null ? _persistentID.Entity : null;

        protected EntityHealth _entityHealth;
        protected ICombatStateProvider _combatState;

        // Base entities offer no prompt/interaction yet (loot is a future spec).
        public virtual string InteractPrompt => string.Empty;

        // Null-guarded: the name-tag scan can reach this via GetComponentInParent even when
        // the component is disabled (GetComponentInParent ignores `enabled`).
        public string NameTag => Data != null ? Data.entityName : string.Empty;

        // Base: nothing to interact with yet → no crosshair prompt. EntityUI (name + HP bar)
        // still shows because the name-tag scan does not check CanInteract.
        public virtual bool CanInteract => false;

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
        }

        // Default: no-op. The corpse intentionally remains a valid IInteractable target so the
        // future loot-corpse story can plug in without re-introducing detection logic.
        public virtual void Interact() { }

        /// <summary>True when this entity is alive and not currently in combat.</summary>
        protected bool IsAliveAndOutOfCombat =>
            (_entityHealth == null || !_entityHealth.IsDead) &&
            (_combatState  == null || !_combatState.IsInCombat);
    }
}
