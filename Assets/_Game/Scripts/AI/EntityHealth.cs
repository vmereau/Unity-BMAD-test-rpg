using System;
using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.NPC;
using Game.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Generic health component for any world entity (enemies, NPCs, neutral).
    /// On death: stops NavMeshAgent if present, calls PersistentID.RegisterDeath(playerCredited) (KillCredit), triggers death animation.
    /// Body remains in scene permanently after ragdoll activates (no SetActive(false)).
    /// Attach to entity root. Entity SO drives BaseHealth; AIAnimationDriver and PersistentID are optional.
    /// Story 2.9: Initial implementation as EnemyHealth.
    /// Enemy Creature System: Migrated to EnemyTypeSO; death triggers AIAnimationDriver instead of SetActive(false).
    /// Renamed EntityHealth: generic for any entity type; NavMeshAgent stop is optional via TryGetComponent.
    /// </summary>
    public class EntityHealth : MonoBehaviour, IDamageable, IExecutable
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

        /// <summary>Shared combat tunables (null = knockouts disabled). Also read by the brain (knockout loot).</summary>
        public CombatConfigSO CombatConfig => _combatConfig;

        /// <summary>True during the get-up clip at the end of a knockout (still <see cref="IsUnconscious"/>).</summary>
        public bool IsGettingUp => IsUnconscious && _gettingUp;

        /// <summary>Same-GO notifications: knocked out / get-up clip starts (<see cref="EntityPresence"/>) / back on its feet.</summary>
        public event Action KnockedOut;
        public event Action GettingUp;
        public event Action RegainedConsciousness;

        /// <summary>
        /// Raised whenever CurrentHealth changes (damage, heal, or respawn reset).
        /// Args: (currentHealth, maxHealth). Listeners (e.g. EntityUI) should subscribe in
        /// OnEnable and unsubscribe in OnDisable, then seed their initial value from
        /// CurrentHealth/MaxHealth since this may have already fired during Awake/OnEnable.
        /// </summary>
        public event Action<float, float> HealthChanged;

        /// <summary>
        /// Execute stomp target (spec-execute-unconscious-npc): lying unconscious (not getting up, not dead) and the
        /// NPC data allows it. Monsters are never knocked out, so never executable.
        /// </summary>
        public bool CanBeExecuted => !IsDead && IsUnconscious && !_gettingUp
                                     && _persistentID != null && _persistentID.Entity is NPCEntity npc && npc.CanBeExecuted;

        /// <summary>The lying body's chest (falls back to the hips, then the root).</summary>
        public Vector3 ExecutePoint
        {
            get
            {
                if (_executeBone == null && _bodyAnimator != null && _bodyAnimator.isHuman)
                {
                    _executeBone = _bodyAnimator.GetBoneTransform(HumanBodyBones.Chest);
                    if (_executeBone == null) _executeBone = _bodyAnimator.GetBoneTransform(HumanBodyBones.Hips);
                }
                return _executeBone != null ? _executeBone.position : transform.position;
            }
        }

        private EntityStance _stance;
        private EntityGuard _guard;
        private NpcArmament _armament;
        private FactionMember _factionMember;
        private Animator _bodyAnimator;
        private Transform _executeBone;
        private float _unconsciousElapsed;
        private bool _gettingUp;
        // The player landed damage since this entity was enabled → assist credit (spec-kill-xp-credit-assist).
        private bool _playerDamaged;

        /// <summary>True once the player landed damage on this entity (cleared on re-enable). Assist credit.</summary>
        public bool PlayerDamaged => _playerDamaged;

        private void Awake()
        {
            _stance = GetComponent<EntityStance>(); // optional
            _guard = GetComponent<EntityGuard>(); // optional — none = never blocks
            _armament = GetComponent<NpcArmament>(); // optional — none = no shield armor
            _factionMember = GetComponent<FactionMember>(); // optional — none = never knocked out
            _bodyAnimator = GetComponentInChildren<Animator>(); // optional — execute point falls back to the root
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
            _playerDamaged = false;
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
                GettingUp?.Invoke();
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
            FactionMember by = FactionMember.Of(attacker);
            bool knockout = _combatConfig != null && FactionMember.IsKnockoutPair(by, _factionMember);
            if (IsUnconscious && knockout) return; // no finishing blow from a brawl opponent

            // An equipped shield adds its defenseBonus (spec-shield-offhand-blocking); body armor stays in Entity.Armor.
            float armor = _persistentID.Entity.Armor + (_armament != null ? _armament.ShieldArmorBonus : 0f);
            float effective = DamageMath.Mitigate(amount, armor, _combatConfig != null ? _combatConfig.minDamageFraction : 0f);
            if (effective > 0f && by != null && by.IsPlayer) _playerDamaged = true;
            CurrentHealth -= effective;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);
            GameLog.Info(TAG, $"{gameObject.name} took {effective:F1} damage (raw {amount:F1} - armor {armor:F0}) — HP: {CurrentHealth:F0}/{MaxHealth:F0}");
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);

            if (CurrentHealth <= 0f)
            {
                if (knockout) KnockOut(attacker); else Die(attacker); // no GetHit either way
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

        /// <summary>
        /// Block check before damage: a raised <see cref="EntityGuard"/> facing the attacker blocks (spec-ai-npc-blocking).
        /// No guard (monsters, unstyled entities), dead or unconscious → every hit lands.
        /// </summary>
        public HitResult TryReceiveHit(GameObject attacker)
        {
            if (IsDead || IsUnconscious || _guard == null) return HitResult.NotBlocked;
            return _guard.ResolveHit(attacker);
        }

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

        // Non-lethal blow at zero health: down on the ground, the brain pauses (KnockedOut). No kill registered; a
        // knockout credited to the player (own blow, Friendly blow or assist — spec-kill-xp-credit-assist) registers the
        // one-time knockout reward (spec-knockout-xp-reward). An uncredited one leaves the claim free.
        private void KnockOut(GameObject attacker)
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

            KillCreditKind credit = KillCredit.Resolve(attacker, _playerDamaged);
            GameLog.Info(TAG, $"{gameObject.name} knockout — player credit: {credit}");
            if (credit != KillCreditKind.None) _persistentID?.RegisterKnockout();
        }

        /// <summary>
        /// Execute stomp kill (spec-execute-unconscious-npc): a normal death (kill registered, lootable corpse), but the
        /// body goes straight to ragdoll — it is already lying, the standing death clip would lift it first.
        /// </summary>
        public bool Execute(GameObject executor)
        {
            if (!CanBeExecuted) return false;
            GameLog.Info(TAG, $"{gameObject.name} executed by {(executor != null ? executor.name : "unknown")}");
            CurrentHealth = 0f;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            Die(executor, ragdollNow: true);
            return true;
        }

        // killer = the killing blow's source (null = unknown) — decides the player's kill credit.
        private void Die(GameObject killer, bool ragdollNow = false)
        {
            IsDead = true;
            if (IsUnconscious)
            {
                // Killed on the ground (monster / hostile hit): the death clip replaces the held knockdown.
                IsUnconscious = false;
                _gettingUp = false;
                // An execute keeps the held on-the-ground pose: releasing it would start the get-up clip, which a
                // ragdoll fallback (no bodies cached) would then play on the corpse.
                if (!ragdollNow) _animationDriver?.SetUnconscious(false);
            }
            KillCreditKind credit = KillCredit.Resolve(killer, _playerDamaged);
            GameLog.Info(TAG, $"{gameObject.name} died — registering kill (player credit: {credit})");

            // Stop NavMeshAgent so it doesn't thrash during death animation
            if (TryGetComponent<NavMeshAgent>(out var agent))
                agent.isStopped = true;

            _persistentID?.RegisterDeath(credit != KillCreditKind.None);

            // Guarantee the warning telegraph clears at the instant of death, independent of
            // EntityBrain.Update() timing — avoids a stuck warning pose if the brain is disabled
            // by the ragdoll path before its next tick runs TransitionToDead().
            _animationDriver?.SetWarning(false);
            _animationDriver?.SetInCombat(false);
            if (ragdollNow)
            {
                _animationDriver?.EnableRagdoll();
                return;
            }
            _animationDriver?.TriggerDeath();
            // Body remains active in scene — ragdoll activated by SMB_DeathState.OnStateExit via AIAnimationDriver.EnableRagdoll()
            // If AIAnimationDriver is absent, this is a no-op and body stays active indefinitely.
        }
    }
}
