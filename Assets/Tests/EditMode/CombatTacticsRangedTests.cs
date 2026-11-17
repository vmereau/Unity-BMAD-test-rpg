using Game.AI;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the ranged style of CombatTacticsRules (spec-ai-ranged-combat-style): bow-or-melee gate and
/// hysteresis, switch cooldown, ranged range bands, Shoot with a token, and priority of Drink over Shoot.
/// </summary>
public class CombatTacticsRangedTests
{
    // Preferred range 9, approach margin 1.5 (approach beyond 10.5), kite inside 6.
    private static CombatTacticsInput Ranged(float distance, bool shotReady = true, bool tokenAvailable = true,
        bool hasToken = false, float health = 1f, bool hasPotion = false) => new CombatTacticsInput
    {
        Distance = distance,
        AttackRange = 1.8f,
        RingMargin = 1.2f,
        ApproachMargin = 1.5f,
        BackOffFraction = 0.6f,
        Aggression = 0.5f,
        CircleElapsed = 10f,
        CircleTime = 2f,
        HasToken = hasToken,
        TokenAvailable = tokenAvailable,
        AttackReady = true,
        RangedMode = true,
        PreferredRange = 9f,
        RangedBackOffDistance = 6f,
        ShotReady = shotReady,
        HealthFraction = health,
        DrinkThreshold = 0.35f,
        HasPotion = hasPotion,
        MaxDrinks = 1,
        DrinkSafeDistance = 4.5f,
        DrinkBackOffMaxTime = 2.5f,
    };

    // Melee closer than 3.5, bow from 6.5, 4 s between switches.
    private static RangedModeInput Mode(float distance, bool isRanged, bool canUse = true, float sinceSwitch = 100f) =>
        new RangedModeInput
        {
            CanUseRanged = canUse,
            IsRanged = isRanged,
            Distance = distance,
            MeleeSwitchDistance = 3.5f,
            RangedSwitchDistance = 6.5f,
            SinceLastSwitch = sinceSwitch,
            SwitchCooldown = 4f,
        };

    // ---- Decide: ranged branch -------------------------------------------------

    [Test]
    public void Ranged_InBand_ShotReady_FreeToken_Shoots()
    {
        Assert.AreEqual(CombatAction.Shoot, CombatTacticsRules.Decide(Ranged(9f), 0.99f));
    }

    [Test]
    public void Ranged_ShotOnCooldown_Circles()
    {
        Assert.AreEqual(CombatAction.Circle, CombatTacticsRules.Decide(Ranged(9f, shotReady: false), 0f));
    }

    [Test]
    public void Ranged_TokenCapReached_Circles()
    {
        Assert.AreEqual(CombatAction.Circle, CombatTacticsRules.Decide(Ranged(9f, tokenAvailable: false), 0f));
    }

    [Test]
    public void Ranged_HeldToken_KeepsShooting()
    {
        Assert.AreEqual(CombatAction.Shoot,
            CombatTacticsRules.Decide(Ranged(9f, shotReady: false, tokenAvailable: false, hasToken: true), 0f));
    }

    [Test]
    public void Ranged_BeyondPreferredRange_Approaches()
    {
        Assert.AreEqual(CombatAction.Approach, CombatTacticsRules.Decide(Ranged(11f), 0f));
    }

    [Test]
    public void Ranged_JustBeyondPreferredRange_WithinMargin_Shoots()
    {
        Assert.AreEqual(CombatAction.Shoot, CombatTacticsRules.Decide(Ranged(10.4f), 0f));
    }

    [Test]
    public void Ranged_TargetClosingIn_BacksOff()
    {
        Assert.AreEqual(CombatAction.BackOff, CombatTacticsRules.Decide(Ranged(5f), 0f));
    }

    [Test]
    public void Ranged_ShootsWithoutAggressionRoll()
    {
        // The shot cooldown sets the cadence: no aggression roll, no circle time.
        CombatTacticsInput input = Ranged(9f);
        input.Aggression = 0f;
        input.CircleElapsed = 0f;
        Assert.AreEqual(CombatAction.Shoot, CombatTacticsRules.Decide(input, 0.99f));
    }

    [Test]
    public void Ranged_LowHealthWithPotion_DrinkBeatsShoot()
    {
        Assert.AreEqual(CombatAction.Drink, CombatTacticsRules.Decide(Ranged(9f, health: 0.2f, hasPotion: true), 0f));
    }

    [Test]
    public void Ranged_LowHealthWithPotion_HeldShotToken_StillDrinks()
    {
        Assert.AreEqual(CombatAction.Drink,
            CombatTacticsRules.Decide(Ranged(9f, hasToken: true, health: 0.2f, hasPotion: true), 0f));
    }

    [Test]
    public void MeleeMode_NeverShoots()
    {
        CombatTacticsInput input = Ranged(2.5f);
        input.RangedMode = false;
        Assert.AreNotEqual(CombatAction.Shoot, CombatTacticsRules.Decide(input, 0f));
        Assert.AreEqual(CombatAction.Attack, CombatTacticsRules.Decide(input, 0f)); // circled, ready, token, roll 0
    }

    [Test]
    public void CurrentRing_FollowsMode()
    {
        CombatTacticsInput input = Ranged(9f);
        Assert.AreEqual(9f, CombatTacticsRules.CurrentRing(input), 1e-4f);
        input.RangedMode = false;
        Assert.AreEqual(3f, CombatTacticsRules.CurrentRing(input), 1e-4f);
    }

    // ---- Ranged gate + when the mode may change ---------------------------------

    [Test]
    public void CanUseRanged_AllConditionsMet()
    {
        Assert.IsTrue(CombatTacticsRules.CanUseRanged(hasBow: true, ammo: 12, targetHostile: true, fistsRequested: false));
    }

    [Test]
    public void CanUseRanged_NoBow_False()
    {
        Assert.IsFalse(CombatTacticsRules.CanUseRanged(false, 12, true, false));
    }

    [Test]
    public void CanUseRanged_OutOfArrows_False()
    {
        Assert.IsFalse(CombatTacticsRules.CanUseRanged(true, 0, true, false));
    }

    [Test]
    public void CanUseRanged_Brawl_NonHostileTarget_False()
    {
        Assert.IsFalse(CombatTacticsRules.CanUseRanged(true, 12, targetHostile: false, fistsRequested: false));
    }

    [Test]
    public void CanUseRanged_FistsRequested_False()
    {
        Assert.IsFalse(CombatTacticsRules.CanUseRanged(true, 12, true, fistsRequested: true));
    }

    [Test]
    public void WeaponMode_NotDecided_MidAttackOrShot()
    {
        Assert.IsFalse(CombatTacticsRules.ShouldDecideWeaponMode(hasToken: true, changingWeapon: false));
    }

    [Test]
    public void WeaponMode_NotDecided_WhileSwitching()
    {
        Assert.IsFalse(CombatTacticsRules.ShouldDecideWeaponMode(hasToken: false, changingWeapon: true));
    }

    [Test]
    public void WeaponMode_Decided_BetweenActions()
    {
        Assert.IsTrue(CombatTacticsRules.ShouldDecideWeaponMode(false, false));
    }

    // ---- WantsRanged: gate, hysteresis, cooldown -------------------------------

    [Test]
    public void Gate_NoBowOrAmmoOrHostile_Melee()
    {
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(12f, isRanged: false, canUse: false)));
    }

    [Test]
    public void Gate_LostAbilityWhileRanged_MeleeAtOnce_IgnoresCooldown()
    {
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(9f, isRanged: true, canUse: false, sinceSwitch: 0f)));
    }

    [Test]
    public void Melee_TargetFar_SwitchesToBow()
    {
        Assert.IsTrue(CombatTacticsRules.WantsRanged(Mode(6.5f, isRanged: false)));
    }

    [Test]
    public void Melee_InsideBand_KeepsMelee()
    {
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(5f, isRanged: false)));
    }

    [Test]
    public void Ranged_InsideBand_KeepsBow()
    {
        Assert.IsTrue(CombatTacticsRules.WantsRanged(Mode(5f, isRanged: true)));
    }

    [Test]
    public void Ranged_TargetClose_SwitchesToMelee()
    {
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(3.5f, isRanged: true)));
    }

    [Test]
    public void Cooldown_BlocksSwitchBack()
    {
        Assert.IsTrue(CombatTacticsRules.WantsRanged(Mode(2f, isRanged: true, sinceSwitch: 1f)));
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(10f, isRanged: false, sinceSwitch: 1f)));
    }

    [Test]
    public void Cooldown_Elapsed_Switches()
    {
        Assert.IsFalse(CombatTacticsRules.WantsRanged(Mode(2f, isRanged: true, sinceSwitch: 4f)));
    }

    [Test]
    public void Hysteresis_NoFlickerAcrossBand()
    {
        // Walking 2 → 8 → 2 m with no cooldown: exactly one switch each way, at the band edges.
        bool ranged = false;
        int switches = 0;
        for (float d = 2f; d <= 8f; d += 0.25f) Step(d);
        for (float d = 8f; d >= 2f; d -= 0.25f) Step(d);
        Assert.AreEqual(2, switches);

        void Step(float distance)
        {
            bool next = CombatTacticsRules.WantsRanged(Mode(distance, ranged));
            if (next != ranged) switches++;
            ranged = next;
        }
    }
}
