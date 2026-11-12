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
        [Tooltip("Unconscious timings (spec-unconscious-knockout). Null → every 0-HP blow kills.")]
        [SerializeField] private CombatConfigSO _combatConfig;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.BaseHealth : 100f;
        public bool IsDead { get; private set; }

        /// <summary>
        /// Knocked out by a non-hostile humanoid (spec-unconscious-knockout): on the ground, regaining health, then
        /// getting up. True until the get-up clip ends. Runtime only — a re-enable resets it.
        /// </summary>
        public bool IsUnconscious { get; private set; }

        /// <summary>Same-GO notifications for <see cref="EntityBrain"/>: knocked out / back on its feet.</summary>
        public event Action KnockedOut;
        public event Action RegainedConsciousness;

        /// <summary>
        /// Raised whenever CurrentHealth changes (damage, heal, or respawn reset).
        /// Args: (currentHealth, maxHealth). Listeners (e.g. EntityUI) should subscribe in
        /// OnEnable and unsubscribe in OnDisable, then seed their initial value from
        /// CurrentHealth/MaxHealth since this may have already fired during Awake/OnEnable.
        /// </summary>
        public event Action<float, float> HealthChanged;

        private EntityStance _stance;
        private FactionMember _factionMember;
        private float _unconsciousElapsed;
        private bool _gettingUp;

        private void Awake()
        {
            _stance = GetComponent<EntityStance>(); // optional
            _factionMember = GetComponent<FactionMember>(); // optional — none = never knocked out
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
            if (IsUnconscious) _animationDriver?.SetUnconscious(false); // re-enabled mid-knockout: release the held clip
            IsUnconscious = false;
            _gettingUp = false;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
        }

        // Unconscious: health climbs to the recovery fraction, then the get-up clip plays, then the entity is back.
        private void Update()
        {
            if (!IsUnconscious || _combatConfig == null) return;

            float duration = _combatConfig.unconsciousDuration;
            float before = _unconsciousElapsed;
            _unconsciousElapsed += Time.deltaTime;
            float regained = KnockoutRules.RecoveryHealth(MaxHealth, _combatConfig.unconsciousRecoverHealthFraction, _unconsciousElapsed, duration)
                           - KnockoutRules.RecoveryHealth(MaxHealth, _combatConfig.unconsciousRecoverHealthFraction, before, duration);
            if (regained > 0f)
            {
                CurrentHealth = Mathf.Min(CurrentHealth + regained, MaxHealth);
                HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            }

            if (!_gettingUp && _unconsciousElapsed >= duration)
            {
                _gettingUp = true;
                _animationDriver?.SetUnconscious(false); // getting up clip
                GameLog.Info(TAG, $"{gameObject.name} is getting up — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
            }
            if (_gettingUp && _unconsciousElapsed >= duration + _combatConfig.getUpDuration)
            {
                IsUnconscious = false;
                _gettingUp = false;
                GameLog.Info(TAG, $"{gameObject.name} regained consciousness");
                RegainedConsciousness?.Invoke();
            }
        }

        /// <summary>
        /// Applies damage from an unknown source — lethal at zero health. Ignored if the entity is already dead.
        /// </summary>
        public void TakeDamage(float amount) => TakeDamage(amount, null);

        /// <summary>
        /// Applies damage from <paramref name="attacker"/>. At zero health a non-hostile humanoid attacker knocks a
        /// humanoid out instead of killing it (<see cref="FactionMember.IsKnockoutPair"/>); its hits on an unconscious
        /// entity are ignored, any other hit still lands (and can kill). Ignored if the entity is already dead.
        /// </summary>
        public void TakeDamage(float amount, GameObject attacker)
        {
            if (IsDead) return;
            bool knockout = _combatConfig != null && FactionMember.IsKnockoutPair(FactionMember.Of(attacker), _factionMember);
            if (IsUnconscious && knockout) return; // no finishing blow from a brawl opponent

            CurrentHealth -= amount;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"{gameObject.name} took {amount} damage — HP: {CurrentHealth:F0}/{_persistentID.Entity.BaseHealth:F0}");
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
            {
                if (knockout) KnockOut(); else Die(); // no GetHit either way
                return;
            }
            if (IsUnconscious) return; // lying on the ground: no hit reaction
            // Hit reaction only if still alive, and not while staggered / knocked down (GetHit would cut the clip).
            if (_stance == null || !_stance.IsIncapacitated) _animationDriver?.TriggerGetHit();
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

        /// <summary>
        /// Save/load: puts a killed entity back as a lootable corpse — dead, agent stopped, ragdoll on.
        /// No RegisterDeath (no kill event / XP) and no death animation. EntityBrain switches itself to
        /// Dead on its next tick.
        /// </summary>
        public void RestoreAsCorpse()
        {
            IsDead = true;
            IsUnconscious = false;
            _gettingUp = false;
            CurrentHealth = 0f;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (TryGetComponent<NavMeshAgent>(out var agent) && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;

            _animationDriver?.SetWarning(false);
            _animationDriver?.SetInCombat(false);
            _animationDriver?.EnableRagdoll();
        }

        // Non-lethal blow at zero health: down on the ground, the brain pauses (KnockedOut). No kill registered.
        private void KnockOut()
        {
            CurrentHealth = 0f;
            IsUnconscious = true;
            _gettingUp = false;
            _unconsciousElapsed = 0f;
            GameLog.Info(TAG, $"{gameObject.name} knocked out");

            _animationDriver?.SetWarning(false);
            _animationDriver?.SetInCombat(false);
            _animationDriver?.SetUnconscious(true);
            KnockedOut?.Invoke();
        }

        private void Die()
        {
            IsDead = true;
            if (IsUnconscious)
            {
                // Killed on the ground (monster / hostile hit): the death clip replaces the held knockdown.
                IsUnconscious = false;
                _gettingUp = false;
                _animationDriver?.SetUnconscious(false);
            }
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
