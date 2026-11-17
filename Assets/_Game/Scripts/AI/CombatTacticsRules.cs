namespace Game.AI
{
    /// <summary>What <see cref="EntityCombatTactics"/> does with its movement while engaged.</summary>
    public enum CombatAction { Approach, Circle, BackOff, Attack }

    /// <summary>Snapshot of one tactical decision's inputs (no Unity objects — EditMode-testable).</summary>
    public struct CombatTacticsInput
    {
        public float Distance;          // flat distance to the target (m)
        public float AttackRange;       // Entity.AttackRange
        public float RingMargin;
        public float ApproachMargin;
        public float BackOffFraction;
        public float Aggression;
        public float CircleElapsed;     // seconds circled since the last swing / engage
        public float CircleTime;        // seconds to circle before an attack is considered
        public bool HasToken;           // already holds an attack token on this target
        public bool TokenAvailable;     // a token on this target could be taken now
        public bool AttackReady;        // cooldown elapsed, weapon in hand, no attack state playing
    }

    /// <summary>
    /// Pure tactical decision (spec-ai-combat-tactics-foundation). Order: a held token → Attack; too far → Approach;
    /// circled long enough + ready + free token + aggression roll → Attack; too close → BackOff; else Circle.
    /// </summary>
    public static class CombatTacticsRules
    {
        public static float RingRadius(float attackRange, float ringMargin) => attackRange + ringMargin;

        /// <param name="roll">Uniform roll in [0, 1) for the aggression check.</param>
        public static CombatAction Decide(in CombatTacticsInput input, float roll)
        {
            if (input.HasToken) return CombatAction.Attack;

            float ring = RingRadius(input.AttackRange, input.RingMargin);
            if (input.Distance > ring + input.ApproachMargin) return CombatAction.Approach;

            if (input.CircleElapsed >= input.CircleTime && input.AttackReady && input.TokenAvailable
                && roll < input.Aggression)
                return CombatAction.Attack;

            if (input.Distance < input.AttackRange * input.BackOffFraction) return CombatAction.BackOff;

            return CombatAction.Circle;
        }

        /// <summary>True when the circling direction should flip this decision.</summary>
        public static bool ShouldFlipDirection(float roll, float directionChangeChance) => roll < directionChangeChance;
    }
}
