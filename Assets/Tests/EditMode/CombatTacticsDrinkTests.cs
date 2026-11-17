using Game.AI;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the Drink decision of CombatTacticsRules (spec-potion-drink-animation): threshold, potion, drinks
/// per fight, cooldown, back off then drink, and priority over a held attack token.
/// </summary>
public class CombatTacticsDrinkTests
{
    // Ring 3.0 (AttackRange 1.8 + 1.2); safe distance 4.5; back-off timeout 2.5 s; threshold 35 %.
    private static CombatTacticsInput Input(float distance, float health = 0.3f, bool hasPotion = true, int drinksUsed = 0,
        float cooldownLeft = 0f, float backOffElapsed = 0f, bool hasToken = false, float threshold = 0.35f) => new CombatTacticsInput
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
        TokenAvailable = true,
        AttackReady = true,
        HealthFraction = health,
        DrinkThreshold = threshold,
        HasPotion = hasPotion,
        DrinksUsed = drinksUsed,
        MaxDrinks = 1,
        DrinkCooldownLeft = cooldownLeft,
        DrinkSafeDistance = 4.5f,
        DrinkBackOffElapsed = backOffElapsed,
        DrinkBackOffMaxTime = 2.5f,
    };

    [Test]
    public void LowWithPotion_Close_BacksOffFirst()
    {
        Assert.AreEqual(CombatAction.BackOff, CombatTacticsRules.Decide(Input(3f), 0f));
    }

    [Test]
    public void LowWithPotion_PastSafeDistance_Drinks()
    {
        Assert.AreEqual(CombatAction.Drink, CombatTacticsRules.Decide(Input(4.5f), 0f));
    }

    [Test]
    public void LowWithPotion_Cornered_DrinksAfterBackOffTimeout()
    {
        Assert.AreEqual(CombatAction.Drink, CombatTacticsRules.Decide(Input(2f, backOffElapsed: 2.5f), 0f));
    }

    [Test]
    public void AtThreshold_WantsDrink()
    {
        Assert.IsTrue(CombatTacticsRules.WantsDrink(Input(5f, health: 0.35f)));
    }

    [Test]
    public void AboveThreshold_FightsOn()
    {
        Assert.IsFalse(CombatTacticsRules.WantsDrink(Input(5f, health: 0.36f)));
        Assert.AreEqual(CombatAction.Attack, CombatTacticsRules.Decide(Input(3f, health: 0.36f), 0f));
    }

    [Test]
    public void ThresholdZero_NeverDrinks()
    {
        Assert.IsFalse(CombatTacticsRules.WantsDrink(Input(5f, health: 0f, threshold: 0f)));
    }

    [Test]
    public void NoPotion_NeverDrinks()
    {
        Assert.IsFalse(CombatTacticsRules.WantsDrink(Input(5f, hasPotion: false)));
        Assert.AreEqual(CombatAction.Approach, CombatTacticsRules.Decide(Input(5f, hasPotion: false), 0.9f));
    }

    [Test]
    public void DrinksPerFightUsed_NeverDrinksAgain()
    {
        Assert.IsFalse(CombatTacticsRules.WantsDrink(Input(5f, drinksUsed: 1)));
    }

    [Test]
    public void OnCooldown_WaitsBeforeDrinking()
    {
        Assert.IsFalse(CombatTacticsRules.WantsDrink(Input(5f, cooldownLeft: 0.1f)));
    }

    [Test]
    public void HeldToken_DrinkWins()
    {
        Assert.AreEqual(CombatAction.BackOff, CombatTacticsRules.Decide(Input(1.5f, hasToken: true), 0f));
        Assert.AreEqual(CombatAction.Drink, CombatTacticsRules.Decide(Input(5f, hasToken: true), 0f));
    }
}
