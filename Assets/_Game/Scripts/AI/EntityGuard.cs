using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Reactive guard of a styled entity (spec-ai-npc-blocking). Ticked by <see cref="EntityBrain"/> while tactics drive the
    /// fight: reads the current target's swing (<see cref="IMeleeThreat"/>), rolls per swing step with the
    /// <see cref="CombatStyleSO"/> block chance, raises the guard (<c>IsBlocking</c>) after a reaction delay and resolves
    /// hits through <see cref="ResolveHit"/> (called by <see cref="EntityHealth.TryReceiveHit"/>). Rules in
    /// <see cref="GuardPlan"/> / <see cref="CombatBlockRules"/>. No style or <c>blockChance</c> 0 = never blocks.
    /// </summary>
    public class EntityGuard : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [Tooltip("Plays the guard (IsBlocking). Auto-resolved on the same GO if null.")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        private readonly GuardPlan _plan = new();
        private EntityHealth _health;
        private EntityStance _stance;
        private EntityMeleeAttacker _attacker;
        private NpcArmament _armament;
        private EntityPotionDrinker _drinker;
        private FactionMember _target;
        private IMeleeThreat _threat;
        private bool _raised;

        /// <summary>Guard currently up (incl. the hold after the threat ended). The brain never swings while true.</summary>
        public bool IsRaised => _raised;

        private CombatStyleSO Style => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.CombatStyle : null;

        private void Awake()
        {
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
            if (_animationDriver == null) _animationDriver = GetComponent<AIAnimationDriver>();
            _health = GetComponent<EntityHealth>();
            _stance = GetComponent<EntityStance>();
            _attacker = GetComponent<EntityMeleeAttacker>();
            _armament = GetComponent<NpcArmament>();
            _drinker = GetComponent<EntityPotionDrinker>();
        }

        private void OnDisable() => End();

        /// <summary>One frame of guarding against <paramref name="target"/>.</summary>
        public void Tick(FactionMember target)
        {
            CombatStyleSO style = Style;
            if (style == null || style.blockChance <= 0f || target == null)
            {
                End();
                return;
            }
            if (target != _target)
            {
                _target = target;
                _threat = target.TryGetComponent(out IMeleeThreat threat) ? threat : null; // none = never a threat
                _plan.Reset();
            }

            Vector3 toTarget = target.Transform.position - transform.position;
            // Interface reference: a destroyed / disabled component (fake null, stale flags) is no threat.
            bool threatLive = _threat is Behaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;
            bool threatActive = threatLive && _threat.IsMeleeAttacking;
            var input = new GuardInput
            {
                CanGuard = CanGuard(),
                ThreatActive = threatActive,
                WindUp = threatActive && !_threat.IsMeleeCommitted,
                InFront = InReachAndArc(toTarget, style),
                BlockChance = style.blockChance,
                ReactionDelayMin = style.reactionDelayMin,
                ReactionDelayMax = style.reactionDelayMax,
                HoldTime = style.guardHoldTime,
            };
            SetRaised(_plan.Tick(input, Time.deltaTime, Random.value, Random.value));
            // A controller swap (NpcArmament) resets every animator parameter: keep re-sending a raised guard.
            if (_raised && _animationDriver != null) _animationDriver.SetBlocking(true);
        }

        /// <summary>Drops the guard and forgets the target. Idempotent.</summary>
        public void End()
        {
            _plan.Reset();
            _target = null;
            _threat = null;
            SetRaised(false);
        }

        /// <summary>
        /// Outcome of a hit by <paramref name="attacker"/>: blocked only by a raised guard facing it within
        /// <c>guardThreatRange</c> — a distant shooter's arrow is never blocked (or parried).
        /// </summary>
        public HitResult ResolveHit(GameObject attacker)
        {
            CombatStyleSO style = Style;
            if (!_raised || style == null || attacker == null || !CanGuard()) return HitResult.NotBlocked;
            bool inArc = InReachAndArc(attacker.transform.position - transform.position, style);
            HitResult result = CombatBlockRules.ResolveBlock(true, inArc, Random.value, style.perfectBlockChance);
            if (result != HitResult.NotBlocked) GameLog.Info(TAG, $"{gameObject.name} {result} vs {attacker.name}");
            return result;
        }

        private bool InReachAndArc(Vector3 toAttacker, CombatStyleSO style) =>
            toAttacker.sqrMagnitude <= style.guardThreatRange * style.guardThreatRange
            && CombatBlockRules.IsInFrontArc(transform.forward, toAttacker, style.guardArcAngle);

        // Never mid-swing, staggered / knocked down, down, switching weapon, drinking (the opening), or with a broken guard.
        private bool CanGuard() =>
            (_drinker == null || !_drinker.IsDrinking) &&
            (_health == null || (!_health.IsDead && !_health.IsUnconscious))
            && (_stance == null || (!_stance.IsIncapacitated && !_stance.IsGuardBroken))
            && (_attacker == null || !_attacker.IsInSwing)
            && (_armament == null || !_armament.IsSwitchingWeapon);

        private void SetRaised(bool raised)
        {
            if (raised == _raised) return;
            _raised = raised;
            if (_animationDriver != null) _animationDriver.SetBlocking(raised);
            GameLog.Info(TAG, $"{gameObject.name} guard {(raised ? "up" : "down")}");
        }
    }
}
