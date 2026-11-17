using Game.AI;
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for CombatTacticsRules.Decide (spec-ai-combat-tactics-foundation): approach / circle / back off /
/// attack priorities, aggression roll, token gating.
/// </summary>
public class CombatTacticsRulesTests
{
    // AttackRange 1.8 + ring margin 1.2 → ring 3.0; approach beyond 4.5; back off under 1.08.
    private static CombatTacticsInput Input(float distance, float circleElapsed = 0f, bool hasToken = false,
        bool tokenAvailable = true, bool attackReady = true) => new CombatTacticsInput
    {
        Distance = distance,
        AttackRange = 1.8f,
        RingMargin = 1.2f,
        ApproachMargin = 1.5f,
        BackOffFraction = 0.6f,
        Aggression = 0.5f,
        CircleElapsed = circleElapsed,
        CircleTime = 2f,
        HasToken = hasToken,
        TokenAvailable = tokenAvailable,
        AttackReady = attackReady,
    };

    [Test]
    public void RingRadius_IsAttackRangePlusMargin()
    {
        Assert.AreEqual(3f, CombatTacticsRules.RingRadius(1.8f, 1.2f), 0.0001f);
    }

    [Test]
    public void Far_Approaches()
    {
        Assert.AreEqual(CombatAction.Approach, CombatTacticsRules.Decide(Input(6f), 0f));
    }

    [Test]
    public void AtRing_BeforeCircleTime_Circles()
    {
        Assert.AreEqual(CombatAction.Circle, CombatTacticsRules.Decide(Input(3f, circleElapsed: 1f), 0f));
    }

    [Test]
    public void CircleTimeElapsed_RollUnderAggression_Attacks()
    {
        Assert.AreEqual(CombatAction.Attack, CombatTacticsRules.Decide(Input(3f, circleElapsed: 2f), 0.49f));
    }

    [Test]
    public void CircleTimeElapsed_RollOverAggression_KeepsCircling()
    {
        Assert.AreEqual(CombatAction.Circle, CombatTacticsRules.Decide(Input(3f, circleElapsed: 2f), 0.5f));
    }

    [Test]
    public void NoTokenAvailable_KeepsCircling()
    {
        Assert.AreEqual(CombatAction.Circle,
            CombatTacticsRules.Decide(Input(3f, circleElapsed: 5f, tokenAvailable: false), 0f));
    }

    [Test]
    public void NotAttackReady_KeepsCircling()
    {
        Assert.AreEqual(CombatAction.Circle,
            CombatTacticsRules.Decide(Input(3f, circleElapsed: 5f, attackReady: false), 0f));
    }

    [Test]
    public void TooClose_BacksOff()
    {
        Assert.AreEqual(CombatAction.BackOff, CombatTacticsRules.Decide(Input(0.8f, circleElapsed: 0.5f), 0f));
    }

    [Test]
    public void TooClose_AttackRollPasses_Attacks()
    {
        Assert.AreEqual(CombatAction.Attack, CombatTacticsRules.Decide(Input(0.8f, circleElapsed: 3f), 0.1f));
    }

    [Test]
    public void HeldToken_AlwaysAttacks_EvenFar()
    {
        Assert.AreEqual(CombatAction.Attack, CombatTacticsRules.Decide(Input(8f, hasToken: true, attackReady: false), 0.99f));
    }

    [Test]
    public void FlipDirection_RollUnderChance()
    {
        Assert.IsTrue(CombatTacticsRules.ShouldFlipDirection(0.05f, 0.1f));
        Assert.IsFalse(CombatTacticsRules.ShouldFlipDirection(0.1f, 0.1f));
    }
}
