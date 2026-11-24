using Game.AI;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for GuardPlan / CombatBlockRules (spec-ai-npc-blocking): per-swing reads, reaction delay, hold,
/// failed reads, can't-guard states, front arc, block resolution, guard break.
/// </summary>
public class CombatBlockRulesTests
{
    private const float PASS = 0f;   // roll that passes any chance > 0
    private const float FAIL = 0.99f;

    // Reaction 0.2–0.4 s (delayRoll 0 → 0.2 s), hold 0.3 s, block chance 0.4.
    private static GuardInput Input(bool threat, bool windUp, bool canGuard = true, bool inFront = true) => new GuardInput
    {
        CanGuard = canGuard,
        ThreatActive = threat,
        WindUp = windUp,
        InFront = inFront,
        BlockChance = 0.4f,
        ReactionDelayMin = 0.2f,
        ReactionDelayMax = 0.4f,
        HoldTime = 0.3f,
    };

    // A swing starts (wind-up edge) with the given block roll, delay roll 0 (= 0.2 s).
    private static bool StartSwing(GuardPlan plan, float blockRoll, bool canGuard = true, bool inFront = true) =>
        plan.Tick(Input(true, true, canGuard, inFront), 0.016f, blockRoll, 0f);

    [Test]
    public void ReadSwing_RaisesAfterReactionDelay()
    {
        var plan = new GuardPlan();
        Assert.IsFalse(StartSwing(plan, PASS));
        Assert.AreEqual(GuardPlan.Phase.Reacting, plan.Current);
        Assert.IsFalse(plan.Tick(Input(true, true), 0.1f, FAIL, 0f), "still reacting");
        Assert.IsTrue(plan.Tick(Input(true, true), 0.1f, FAIL, 0f), "up after 0.2 s");
    }

    [Test]
    public void ReactionDelay_LerpsBetweenMinAndMax()
    {
        var plan = new GuardPlan();
        plan.Tick(Input(true, true), 0f, PASS, 1f); // 0.4 s
        Assert.IsFalse(plan.Tick(Input(true, false), 0.35f, FAIL, 0f));
        Assert.IsTrue(plan.Tick(Input(true, false), 0.1f, FAIL, 0f));
    }

    [Test]
    public void FailedRoll_NeverRaises()
    {
        var plan = new GuardPlan();
        StartSwing(plan, FAIL);
        Assert.IsFalse(plan.Tick(Input(true, true), 1f, PASS, 0f), "same swing is not re-rolled");
        Assert.AreEqual(GuardPlan.Phase.Down, plan.Current);
    }

    [Test]
    public void ThreatEndsBeforeReaction_StaysDown()
    {
        var plan = new GuardPlan();
        StartSwing(plan, PASS);
        Assert.IsFalse(plan.Tick(Input(false, false), 0.3f, FAIL, 0f));
    }

    [Test]
    public void ThreatEnds_LingersForHoldTime_ThenDown()
    {
        var plan = Raised();
        Assert.IsTrue(plan.Tick(Input(false, false), 0.016f, FAIL, 0f));
        Assert.AreEqual(GuardPlan.Phase.Lingering, plan.Current);
        Assert.IsTrue(plan.Tick(Input(false, false), 0.2f, FAIL, 0f));
        Assert.IsFalse(plan.Tick(Input(false, false), 0.2f, FAIL, 0f));
    }

    [Test]
    public void NewComboStep_PassWhileUp_StaysUpWithoutDelay()
    {
        var plan = Raised();
        plan.Tick(Input(true, false), 0.1f, FAIL, 0f);   // committed (hit window)
        Assert.IsTrue(plan.Tick(Input(true, true), 0.016f, PASS, 1f)); // next step wind-up
        Assert.AreEqual(GuardPlan.Phase.Up, plan.Current);
    }

    [Test]
    public void NewComboStep_FailedRead_LowersGuard()
    {
        var plan = Raised();
        plan.Tick(Input(true, false), 0.1f, FAIL, 0f);
        Assert.IsFalse(plan.Tick(Input(true, true), 0.016f, FAIL, 0f));
    }

    [Test]
    public void SwingOutsideFrontArc_NotRead()
    {
        var plan = new GuardPlan();
        StartSwing(plan, PASS, inFront: false);
        Assert.IsFalse(plan.Tick(Input(true, true, inFront: false), 1f, PASS, 0f));
    }

    [Test]
    public void CannotGuard_DropsRaisedGuard()
    {
        var plan = Raised();
        Assert.IsFalse(plan.Tick(Input(true, false, canGuard: false), 0.016f, PASS, 0f));
        Assert.AreEqual(GuardPlan.Phase.Down, plan.Current);
    }

    [Test]
    public void SwingStartedWhileUnableToGuard_IsNotReadLater()
    {
        var plan = new GuardPlan();
        StartSwing(plan, PASS, canGuard: false);              // own swing / stagger at the wind-up
        Assert.IsFalse(plan.Tick(Input(true, true), 0.016f, PASS, 0f), "edge consumed while unable to guard");
        Assert.IsFalse(plan.Tick(Input(true, false), 1f, PASS, 0f));
    }

    [Test]
    public void Reset_DropsGuard()
    {
        var plan = Raised();
        plan.Reset();
        Assert.IsFalse(plan.IsRaised);
    }

    [Test]
    public void ZeroBlockChance_NeverRaises()
    {
        var plan = new GuardPlan();
        GuardInput input = Input(true, true);
        input.BlockChance = 0f;
        plan.Tick(input, 0.016f, 0f, 0f); // even the lowest roll fails
        Assert.IsFalse(plan.Tick(input, 1f, 0f, 0f));
        Assert.AreEqual(GuardPlan.Phase.Down, plan.Current);
    }

    [Test]
    public void Reset_ReadsANewTargetsOngoingWindUp()
    {
        var plan = new GuardPlan();
        StartSwing(plan, FAIL);                 // old target's swing, not read
        plan.Reset();                           // retarget
        StartSwing(plan, PASS);                 // new target already winding up: still an edge
        Assert.AreEqual(GuardPlan.Phase.Reacting, plan.Current);
    }

    [Test]
    public void Up_ThreatLeavesArcOrRange_Lingers()
    {
        var plan = Raised();
        Assert.IsTrue(plan.Tick(Input(true, false, inFront: false), 0.016f, FAIL, 0f));
        Assert.AreEqual(GuardPlan.Phase.Lingering, plan.Current);
        Assert.IsFalse(plan.Tick(Input(true, false, inFront: false), 0.4f, FAIL, 0f));
    }

    [Test]
    public void Reacting_ExpiresOutOfArc_StaysDown()
    {
        var plan = new GuardPlan();
        StartSwing(plan, PASS);
        Assert.IsFalse(plan.Tick(Input(true, false, inFront: false), 0.3f, FAIL, 0f));
        Assert.AreEqual(GuardPlan.Phase.Down, plan.Current);
    }

    [Test]
    public void Lingering_NewSwingRead_BackUpWithoutDelay()
    {
        var plan = Raised();
        plan.Tick(Input(false, false), 0.016f, FAIL, 0f); // swing over → lingering
        Assert.IsTrue(plan.Tick(Input(true, true), 0.016f, PASS, 1f));
        Assert.AreEqual(GuardPlan.Phase.Up, plan.Current);
    }

    [Test]
    public void TooSlow_GuardStillRisesForTheRestOfTheSwing()
    {
        var plan = new GuardPlan();
        plan.Tick(Input(true, true), 0.016f, PASS, 1f);  // 0.4 s reaction
        plan.Tick(Input(true, false), 0.2f, FAIL, 0f);   // committed: the hit landed meanwhile
        Assert.IsTrue(plan.Tick(Input(true, false), 0.2f, FAIL, 0f), "up for the recovery (spec: too slow row)");
    }

    [Test]
    public void FrontArc_ZeroForward_IsNotInFront()
    {
        Assert.IsFalse(CombatBlockRules.IsInFrontArc(Vector3.zero, Vector3.forward, 60f));
        Assert.IsTrue(CombatBlockRules.IsInFrontArc(Vector3.forward, Vector3.back, 180f));
    }

    [Test]
    public void FrontArc_InsideAndOutside()
    {
        Assert.IsTrue(CombatBlockRules.IsInFrontArc(Vector3.forward, new Vector3(0.5f, 0f, 1f), 60f));
        Assert.IsFalse(CombatBlockRules.IsInFrontArc(Vector3.forward, new Vector3(1f, 0f, 0f), 60f));
        Assert.IsFalse(CombatBlockRules.IsInFrontArc(Vector3.forward, Vector3.back, 60f));
        Assert.IsTrue(CombatBlockRules.IsInFrontArc(Vector3.forward, new Vector3(0f, 5f, 1f), 10f), "height ignored");
    }

    [Test]
    public void ResolveBlock_Outcomes()
    {
        Assert.AreEqual(HitResult.NotBlocked, CombatBlockRules.ResolveBlock(false, true, 0f, 0.1f));
        Assert.AreEqual(HitResult.NotBlocked, CombatBlockRules.ResolveBlock(true, false, 0f, 0.1f), "flank");
        Assert.AreEqual(HitResult.Blocked, CombatBlockRules.ResolveBlock(true, true, 0.5f, 0.1f));
        Assert.AreEqual(HitResult.PerfectBlock, CombatBlockRules.ResolveBlock(true, true, 0.05f, 0.1f));
        Assert.AreEqual(HitResult.Blocked, CombatBlockRules.ResolveBlock(true, true, 0f, 0f), "0 chance never parries");
    }

    [Test]
    public void GuardBroken_AtOrBelowThreshold()
    {
        Assert.IsTrue(CombatBlockRules.IsGuardBroken(50f, 100f, 0.5f));
        Assert.IsTrue(CombatBlockRules.IsGuardBroken(10f, 100f, 0.5f));
        Assert.IsFalse(CombatBlockRules.IsGuardBroken(51f, 100f, 0.5f));
    }

    private static GuardPlan Raised()
    {
        var plan = new GuardPlan();
        StartSwing(plan, PASS);
        plan.Tick(Input(true, true), 0.3f, FAIL, 0f);
        Assert.IsTrue(plan.IsRaised);
        return plan;
    }

    // --- spec-unshielded-blocking ---

    [Test]
    public void PerfectBlockChance_FullWithShield_ScaledWithout()
    {
        Assert.AreEqual(0.4f, CombatBlockRules.PerfectBlockChance(0.4f, true, 0.5f), 1e-5f);
        Assert.AreEqual(0.2f, CombatBlockRules.PerfectBlockChance(0.4f, false, 0.5f), 1e-5f);
        Assert.AreEqual(0f, CombatBlockRules.PerfectBlockChance(0f, false, 0.5f), 1e-5f);
    }
}
