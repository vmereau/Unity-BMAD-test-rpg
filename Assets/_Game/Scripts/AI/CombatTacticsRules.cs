namespace Game.AI
{
    /// <summary>What <see cref="EntityCombatTactics"/> does with its movement while engaged.</summary>
    // Drink appended (spec-potion-drink-animation): DebugStateName arrays are indexed by this enum.
    public enum CombatAction { Approach, Circle, BackOff, Attack, Drink }

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

        // Potions (spec-potion-drink-animation)
        public float HealthFraction;        // current / max health
        public float DrinkThreshold;        // CombatStyleSO.drinkHealthThreshold (0 = never)
        public bool HasPotion;              // carries a drinkable potion
        public int DrinksUsed;              // drinks started this fight
        public int MaxDrinks;               // CombatStyleSO.maxDrinksPerFight
        public float DrinkCooldownLeft;     // seconds until another drink may start
        public float DrinkSafeDistance;     // start drinking at least this far from the target
        public float DrinkBackOffElapsed;   // seconds backed off for the pending drink
        public float DrinkBackOffMaxTime;   // drink anyway after backing off this long
    }

    /// <summary>
    /// Pure tactical decision (spec-ai-combat-tactics-foundation). Order: wants a drink → Drink once far enough (or backed
    /// off long enough), else BackOff; a held token → Attack; too far → Approach; circled long enough + ready + free token
    /// + aggression roll → Attack; too close → BackOff; else Circle.
    /// </summary>
    public static class CombatTacticsRules
    {
        public static float RingRadius(float attackRange, float ringMargin) => attackRange + ringMargin;

        /// <summary>Low enough, carrying a potion, drinks left this fight and off cooldown.</summary>
        public static bool WantsDrink(in CombatTacticsInput input) =>
            input.DrinkThreshold > 0f && input.HealthFraction <= input.DrinkThreshold && input.HasPotion
            && input.DrinksUsed < input.MaxDrinks && input.DrinkCooldownLeft <= 0f;

        /// <param name="roll">Uniform roll in [0, 1) for the aggression check.</param>
        public static CombatAction Decide(in CombatTacticsInput input, float roll)
        {
            // Drinking outranks a held token: the caller gives the token back.
            if (WantsDrink(input))
                return input.Distance >= input.DrinkSafeDistance || input.DrinkBackOffElapsed >= input.DrinkBackOffMaxTime
                    ? CombatAction.Drink
                    : CombatAction.BackOff;

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
