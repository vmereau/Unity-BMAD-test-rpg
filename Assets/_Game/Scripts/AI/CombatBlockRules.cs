using Game.Combat;
using UnityEngine;

namespace Game.AI
{
    /// <summary>Snapshot of one guard tick's inputs (no Unity objects — EditMode-testable).</summary>
    public struct GuardInput
    {
        public bool CanGuard;        // not swinging, not staggered / down / dead, not switching weapon, guard not broken
        public bool ThreatActive;    // the target is mid melee swing
        public bool WindUp;          // ... and the current swing step is not committed yet
        public bool InFront;         // target within the front arc and the threat range
        public float BlockChance;
        public float ReactionDelayMin;
        public float ReactionDelayMax;
        public float HoldTime;       // guard kept up after the threat ends
    }

    /// <summary>
    /// Pure guard state of one defender (spec-ai-npc-blocking): Down → (read a swing) Reacting → Up → (threat over)
    /// Lingering → Down. One roll per swing step (the wind-up rising edge): a pass while down starts the reaction delay,
    /// a pass while up keeps the guard, a fail lowers it. A step that starts while the defender can't guard is ignored.
    /// The guard only rises / stays up while the threat is in front and in range; it lingers once it isn't.
    /// </summary>
    public class GuardPlan
    {
        public enum Phase { Down, Reacting, Up, Lingering }

        public Phase Current { get; private set; } = Phase.Down;
        public bool IsRaised => Current == Phase.Up || Current == Phase.Lingering;

        private float _timer;
        private bool _prevWindUp;

        /// <param name="blockRoll">Uniform [0, 1) roll against <see cref="GuardInput.BlockChance"/>.</param>
        /// <param name="delayRoll">Uniform [0, 1) roll picking the reaction delay in [min, max].</param>
        /// <returns>True while the guard is raised.</returns>
        public bool Tick(in GuardInput input, float deltaTime, float blockRoll, float delayRoll)
        {
            bool newSwing = input.WindUp && !_prevWindUp;
            _prevWindUp = input.WindUp;

            if (!input.CanGuard)
            {
                Current = Phase.Down;
                return false;
            }

            if (newSwing)
            {
                bool read = input.InFront && blockRoll < input.BlockChance;
                if (!read) Current = Phase.Down;
                else if (IsRaised) Current = Phase.Up;
                else
                {
                    Current = Phase.Reacting;
                    _timer = Mathf.Lerp(input.ReactionDelayMin, input.ReactionDelayMax, delayRoll);
                }
            }

            switch (Current)
            {
                case Phase.Reacting:
                    _timer -= deltaTime;
                    if (_timer <= 0f) Current = input.ThreatActive && input.InFront ? Phase.Up : Phase.Down;
                    break;
                case Phase.Up:
                    if (!input.ThreatActive || !input.InFront)
                    {
                        Current = Phase.Lingering;
                        _timer = input.HoldTime;
                    }
                    break;
                case Phase.Lingering:
                    _timer -= deltaTime;
                    if (_timer <= 0f) Current = Phase.Down;
                    break;
            }
            return IsRaised;
        }

        /// <summary>
        /// Guard dropped (fight over, disabled, new target): the next wind-up seen is read as a new swing — a swing the new
        /// target is already winding up counts.
        /// </summary>
        public void Reset()
        {
            Current = Phase.Down;
            _timer = 0f;
            _prevWindUp = false;
        }
    }

    /// <summary>Pure block rules (spec-ai-npc-blocking).</summary>
    public static class CombatBlockRules
    {
        private const float MIN_SQR_LENGTH = 0.0001f;

        /// <summary>True when <paramref name="toAttacker"/> (flattened) is within <paramref name="halfAngle"/>° of <paramref name="forward"/>.</summary>
        public static bool IsInFrontArc(Vector3 forward, Vector3 toAttacker, float halfAngle)
        {
            forward.y = 0f;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < MIN_SQR_LENGTH) return true; // standing on us: treat as in front
            if (forward.sqrMagnitude < MIN_SQR_LENGTH) return false;
            return Vector3.Angle(forward, toAttacker) <= halfAngle;
        }

        /// <summary>Hit outcome against a defender: a raised guard facing the attacker blocks, rarely perfectly.</summary>
        public static HitResult ResolveBlock(bool raised, bool inFrontArc, float perfectRoll, float perfectBlockChance)
        {
            if (!raised || !inFrontArc) return HitResult.NotBlocked;
            return perfectRoll < perfectBlockChance ? HitResult.PerfectBlock : HitResult.Blocked;
        }

        /// <summary>
        /// Perfect-block chance of a guard (spec-unshielded-blocking): the style's chance with a shield in hand, × the style's
        /// <c>unshieldedPerfectBlockMultiplier</c> without one (fists, two-hander, bare one-hander).
        /// </summary>
        public static float PerfectBlockChance(float styleChance, bool shieldInHand, float unshieldedMultiplier) =>
            shieldInHand ? styleChance : styleChance * unshieldedMultiplier;

        /// <summary>Guard broken: stance at or below the stagger threshold — no guard until it regenerates above it.</summary>
        public static bool IsGuardBroken(float currentStance, float maxStance, float staggerThreshold) =>
            currentStance <= StanceMath.ThresholdValue(maxStance, staggerThreshold);
    }
}
