using UnityEngine;

namespace Game.Combat
{
    public enum StanceReaction { None, Stagger, KnockDown }

    public readonly struct StanceResult
    {
        public readonly float Stance;
        public readonly StanceReaction Reaction;

        public StanceResult(float stance, StanceReaction reaction)
        {
            Stance = stance;
            Reaction = reaction;
        }
    }

    /// <summary>
    /// Pure stance rules (spec-ai-stance-stagger-knockdown). Stance drains on hits and regenerates.
    /// Crossing the stagger threshold (from above to at/below) staggers once per drain — it re-arms only after
    /// regen brings stance back above it. Reaching 0 knocks down. A hit that can't knock down never takes stance
    /// below the threshold (and deals nothing once stance is already at/below it). Covered by StanceMathTests.
    /// </summary>
    public static class StanceMath
    {
        public static float ThresholdValue(float max, float thresholdFraction) => max * Mathf.Clamp01(thresholdFraction);

        public static StanceResult Apply(float current, float max, float thresholdFraction, float damage, bool canKnockDown)
        {
            if (damage <= 0f || max <= 0f) return new StanceResult(current, StanceReaction.None);

            float threshold = ThresholdValue(max, thresholdFraction);
            float floor = canKnockDown ? 0f : Mathf.Min(current, threshold);
            float next = Mathf.Max(floor, current - damage);

            if (canKnockDown && next <= 0f && current > 0f) return new StanceResult(0f, StanceReaction.KnockDown);
            if (current > threshold && next <= threshold) return new StanceResult(next, StanceReaction.Stagger);
            return new StanceResult(next, StanceReaction.None);
        }

        public static float Regen(float current, float max, float rate, float deltaTime)
            => Mathf.Min(max, current + Mathf.Max(0f, rate) * Mathf.Max(0f, deltaTime));

        /// <summary>Hyper-armor: damage scaled by (1 − resistance), resistance clamped to 0..1.</summary>
        public static float ApplyResistance(float damage, float resistance) => damage * (1f - Mathf.Clamp01(resistance));

        /// <summary>
        /// Player melee stance damage: (base + max(0, strength delta) × perStrength) × finisher multiplier on the
        /// last combo step, then + Power Strike bonus.
        /// </summary>
        public static float ComputeMeleeStanceDamage(float baseDamage, float strengthDelta, float perStrength,
            bool isFinisher, float finisherMultiplier, float powerStrikeBonus)
        {
            float damage = Mathf.Max(0f, baseDamage) + Mathf.Max(0f, strengthDelta) * Mathf.Max(0f, perStrength);
            if (isFinisher) damage *= Mathf.Max(1f, finisherMultiplier);
            return damage + Mathf.Max(0f, powerStrikeBonus);
        }
    }
}
