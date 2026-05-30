using System;
using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Generic health component for any world entity (enemies, NPCs, neutral).
    /// On death: stops NavMeshAgent if present, calls PersistentID.RegisterDeath(), triggers death animation.
    /// Body remains in scene permanently after ragdoll activates (no SetActive(false)).
    /// Attach to entity root. Entity SO drives BaseHealth; AIAnimationDriver and PersistentID are optional.
    /// Story 2.9: Initial implementation as EnemyHealth.
    /// Enemy Creature System: Migrated to EnemyTypeSO; death triggers AIAnimationDriver instead of SetActive(false).
    /// Renamed EntityHealth: generic for any entity type; NavMeshAgent stop is optional via TryGetComponent.
    /// </summary>
    public class EntityHealth : MonoBehaviour, IDamageable
    {
        private const string TAG = "[Combat]";

        [SerializeField] private PersistentID _persistentID;
        [FormerlySerializedAs("_animationBridge")]
        [FormerlySerializedAs("_entityAnimator")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.BaseHealth : 100f;
        public bool IsDead { get; private set; }

        /// <summary>
        /// Raised whenever CurrentHealth changes (damage, heal, or respawn reset).
        /// Args: (currentHealth, maxHealth). Listeners (e.g. EntityUI) should subscribe in
        /// OnEnable and unsubscribe in OnDisable, then seed their initial value from
        /// CurrentHealth/MaxHealth since this may have already fired during Awake/OnEnable.
        /// </summary>
        public event Action<float, float> HealthChanged;

        private void Awake()
        {
            if (_persistentID.Entity == null)
            {
                GameLog.Error(TAG, $"Entity SO not assigned on {gameObject.name} — EntityHealth disabled");
                enabled = false;
                return;
            }

            if (_persistentID == null)
                GameLog.Warn(TAG, $"{gameObject.name}: PersistentID not assigned — kill will not be registered");

            CurrentHealth = _persistentID.Entity.BaseHealth;
        }

        /// <summary>
        /// Called by Unity when the GameObject is re-enabled (e.g. by EnemyRespawner).
        /// Resets health and dead flag so the entity is fully combat-ready again.
        /// </summary>
        private void OnEnable()
        {
            if (_persistentID.Entity == null) return;
            CurrentHealth = _persistentID.Entity.BaseHealth;
            IsDead = false;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        /// <summary>
        /// Applies damage to this entity. Triggers death when health reaches zero.
        /// Calls are ignored if the entity is already dead.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            CurrentHealth -= amount;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"{gameObject.name} took {amount} damage — HP: {CurrentHealth:F0}/{_persistentID.Entity.BaseHealth:F0}");
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f) { Die(); return; }  // death path — no GetHit
            _animationDriver?.TriggerGetHit();              // hit reaction only if still alive
        }

        /// <summary>
        /// Restores health up to MaxHealth. Ignored if the entity is dead.
        /// Fires <see cref="HealthChanged"/> so listeners (UI) update immediately.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
            GameLog.Info(TAG, $"{gameObject.name} healed {amount} — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        // AI entities do not block today — every hit lands. Part of IDamageable contract.
        public HitResult TryReceiveHit(GameObject attacker) => HitResult.NotBlocked;

        private void Die()
        {
            IsDead = true;
            GameLog.Info(TAG, $"{gameObject.name} died — registering kill");

            // Stop NavMeshAgent so it doesn't thrash during death animation
            if (TryGetComponent<NavMeshAgent>(out var agent))
                agent.isStopped = true;

            _persistentID?.RegisterDeath();

            // Guarantee the warning telegraph clears at the instant of death, independent of
            // EntityBrain.Update() timing — avoids a stuck warning pose if the brain is disabled
            // by the ragdoll path before its next tick runs TransitionToDead().
            _animationDriver?.SetWarning(false);
            _animationDriver?.SetInCombat(false);
            _animationDriver?.TriggerDeath();
            // Body remains active in scene — ragdoll activated by SMB_DeathState.OnStateExit via AIAnimationDriver.EnableRagdoll()
            // If AIAnimationDriver is absent, this is a no-op and body stays active indefinitely.
        }
    }
}
