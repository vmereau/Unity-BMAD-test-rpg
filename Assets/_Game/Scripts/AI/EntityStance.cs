using System;
using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Hidden stance meter of an AI entity (spec-ai-stance-stagger-knockdown), on the <c>Entity_base</c> root. Player
    /// hits and perfect blocks drain it (rules in <see cref="StanceMath"/>); it regenerates after
    /// <c>Entity.StanceRegenDelay</c> without stance damage. Crossing the threshold staggers for
    /// <c>StaggerDuration</c>; reaching 0 knocks down for <c>KnockdownDuration</c>, then stance refills.
    /// While incapacitated the brain pauses (<see cref="EntityBrain"/>) and <see cref="EntityHealth"/> skips GetHit.
    /// Runtime only — not saved; reset on enable.
    /// </summary>
    public class EntityStance : MonoBehaviour, IStanceTarget
    {
        private const string TAG = "[Combat]";
        private const int STAGGER_VARIANTS = 2;

        [SerializeField] private PersistentID _persistentID;
        [Tooltip("Plays stagger / knockdown. Auto-resolved on the same GO if null.")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        public float CurrentStance => _meter.Current;
        public float MaxStance => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.MaxStance : 100f;
        public bool IsStaggered => _meter.IsStaggered;
        public bool IsKnockedDown => _meter.IsKnockedDown;
        public bool IsIncapacitated => _meter.IsIncapacitated;

        /// <summary>Same-GO notifications for <see cref="EntityBrain"/>.</summary>
        public event Action Staggered;
        public event Action KnockedDown;
        public event Action Recovered;

        private readonly StanceMeter _meter = new();
        private EntityHealth _health;
        private EntityMeleeAttacker _attacker;

        private StanceSettings Settings
        {
            get
            {
                Entity e = _persistentID.Entity;
                return new StanceSettings(e.MaxStance, e.StaggerThreshold, e.StanceRegenDelay, e.StanceRegenRate,
                    e.StaggerDuration, e.KnockdownDuration);
            }
        }

        private void Awake()
        {
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();
            _health = GetComponent<EntityHealth>();
            _attacker = GetComponent<EntityMeleeAttacker>();
            if (_persistentID == null || _persistentID.Entity == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: EntityStance has no Entity SO — stance disabled");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_persistentID == null || _persistentID.Entity == null) return;
            _meter.Reset(Settings);
        }

        private void Update()
        {
            if (_health != null && _health.IsDead)
            {
                // Death overrides the reaction; no Recovered — the brain resets itself in TransitionToDead.
                _meter.ClearReaction();
                return;
            }

            if (_meter.Tick(Time.deltaTime, Settings)) Recover();
        }

        public void ApplyStanceDamage(float amount, bool canKnockDown) => Apply(amount, canKnockDown, hyperArmor: true);

        /// <summary>AI entities can't block — treated as a hit that can't knock down.</summary>
        public void ApplyBlockedStanceDamage(float amount) => Apply(amount, false, hyperArmor: true);

        /// <summary>
        /// Perfect block: never knocks down, and skips hyper-armor — the parried attacker is always mid-attack, so
        /// the resistance would otherwise always shrink it.
        /// </summary>
        public void ApplyPerfectBlockStanceDamage(float amount) => Apply(amount, false, hyperArmor: false);

        private void Apply(float amount, bool canKnockDown, bool hyperArmor)
        {
            if (!enabled || IsKnockedDown || amount <= 0f) return;
            if (_health != null && _health.IsDead) return;

            Entity entity = _persistentID.Entity;
            if (hyperArmor && _attacker != null && _attacker.IsInAttackState)
                amount = StanceMath.ApplyResistance(amount, entity.AttackStanceResistance);
            if (amount <= 0f) return; // fully absorbed

            float before = CurrentStance;
            StanceReaction reaction = _meter.Apply(amount, canKnockDown, Settings, out bool applied);
            if (!applied) return; // floored (no-knockdown hit at/below the threshold): regen keeps running
            GameLog.Info(TAG, $"{gameObject.name} stance {before:F0} → {CurrentStance:F0}/{MaxStance:F0} " +
                              $"(hit {amount:F0}, knockdown {(canKnockDown ? "on" : "off")}) {reaction}");

            switch (reaction)
            {
                case StanceReaction.Stagger:
                    if (_animationDriver != null) _animationDriver.TriggerStagger(UnityEngine.Random.Range(0, STAGGER_VARIANTS));
                    Staggered?.Invoke();
                    break;
                case StanceReaction.KnockDown:
                    if (_animationDriver != null) _animationDriver.TriggerKnockDown();
                    KnockedDown?.Invoke();
                    break;
            }
        }

        // The meter already refilled (knockdown) and cleared the reaction.
        private void Recover()
        {
            if (_animationDriver != null) _animationDriver.ClearStanceReaction();
            GameLog.Info(TAG, $"{gameObject.name} recovered — stance {CurrentStance:F0}/{MaxStance:F0}");
            Recovered?.Invoke();
        }
    }
}
