namespace Game.AI
{
    /// <summary>What <see cref="EntityCombatTactics"/> does with its movement while engaged.</summary>
    // Drink (spec-potion-drink-animation) and Shoot (spec-ai-ranged-combat-style) appended: DebugStateName arrays are
    // indexed by this enum.
    public enum CombatAction { Approach, Circle, BackOff, Attack, Drink, Shoot }

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

        // Ranged (spec-ai-ranged-combat-style)
        public bool RangedMode;             // fighting with the bow (requested mode — spacing follows it during the switch)
        public float PreferredRange;        // CombatStyleSO.preferredRange — the ranged ring
        public float RangedBackOffDistance; // closer than this the bow user kites back out
        public bool ShotReady;              // bow in hand, nocking possible, shot cooldown over, ammo, clear line of fire
    }

    /// <summary>Inputs of the bow-or-melee choice (spec-ai-ranged-combat-style).</summary>
    public struct RangedModeInput
    {
        public bool CanUseRanged;           // bow carried, ammo left, faction-hostile target, no fists request
        public bool IsRanged;               // currently requested mode
        public float Distance;
        public float MeleeSwitchDistance;   // closer → melee
        public float RangedSwitchDistance;  // farther → bow (≥ MeleeSwitchDistance: hysteresis band)
        public float SinceLastSwitch;       // seconds since the last mode switch
        public float SwitchCooldown;        // CombatStyleSO.weaponSwitchCooldown
    }

    /// <summary>
    /// Pure tactical decision (spec-ai-combat-tactics-foundation). Order: wants a drink → Drink once far enough (or backed
    /// off long enough), else BackOff; then the ranged branch (bow mode) or the melee one: a held token → Attack; too far
    /// → Approach; circled long enough + ready + free token + aggression roll → Attack; too close → BackOff; else Circle.
    /// Ranged branch: a held token → Shoot; beyond the preferred range + approach margin → Approach; inside the ranged
    /// back-off distance → BackOff (kite); shot ready + free token → Shoot; else Circle on the preferred-range ring.
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

            if (input.RangedMode) return DecideRanged(input);

            if (input.HasToken) return CombatAction.Attack;

            float ring = RingRadius(input.AttackRange, input.RingMargin);
            if (input.Distance > ring + input.ApproachMargin) return CombatAction.Approach;

            if (input.CircleElapsed >= input.CircleTime && input.AttackReady && input.TokenAvailable
                && roll < input.Aggression)
                return CombatAction.Attack;

            if (input.Distance < input.AttackRange * input.BackOffFraction) return CombatAction.BackOff;

            return CombatAction.Circle;
        }

        // Bow mode: keep the preferred range, shoot on a free token (the shot cooldown sets the cadence — no circle time).
        private static CombatAction DecideRanged(in CombatTacticsInput input)
        {
            if (input.HasToken) return CombatAction.Shoot;
            if (input.Distance > input.PreferredRange + input.ApproachMargin) return CombatAction.Approach;
            if (input.Distance < input.RangedBackOffDistance) return CombatAction.BackOff;
            if (input.ShotReady && input.TokenAvailable) return CombatAction.Shoot;
            return CombatAction.Circle;
        }

        /// <summary>Circling ring for the current mode: the preferred range with the bow, else AttackRange + margin.</summary>
        public static float CurrentRing(in CombatTacticsInput input) =>
            input.RangedMode ? input.PreferredRange : RingRadius(input.AttackRange, input.RingMargin);

        /// <summary>
        /// Ranged gate (spec-ai-ranged-combat-style): a usable bow, arrows left, a faction-hostile target (brawls /
        /// aggro-only fights stay melee) and no fists request.
        /// </summary>
        public static bool CanUseRanged(bool hasBow, int ammo, bool targetHostile, bool fistsRequested) =>
            hasBow && ammo > 0 && targetHostile && !fistsRequested;

        /// <summary>
        /// The weapon mode is (re)decided only between actions: never while holding a token (mid-attack / mid-shot) or
        /// while a switch is still playing out (no reversal mid-chain).
        /// </summary>
        public static bool ShouldDecideWeaponMode(bool hasToken, bool changingWeapon) => !hasToken && !changingWeapon;

        /// <summary>
        /// Bow (true) or melee (false). No bow / ammo / hostile target → melee at once. Otherwise melee closer than
        /// <c>MeleeSwitchDistance</c>, bow from <c>RangedSwitchDistance</c>, the current mode kept inside the band and while
        /// the switch cooldown runs.
        /// </summary>
        public static bool WantsRanged(in RangedModeInput input)
        {
            if (!input.CanUseRanged) return false;
            bool wanted = input.IsRanged
                ? input.Distance > input.MeleeSwitchDistance
                : input.Distance >= input.RangedSwitchDistance;
            if (wanted != input.IsRanged && input.SinceLastSwitch < input.SwitchCooldown) return input.IsRanged;
            return wanted;
        }

        /// <summary>True when the circling direction should flip this decision.</summary>
        public static bool ShouldFlipDirection(float roll, float directionChangeChance) => roll < directionChangeChance;
    }
}
