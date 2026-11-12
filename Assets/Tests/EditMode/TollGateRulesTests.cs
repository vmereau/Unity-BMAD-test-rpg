using Game.AI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="TollGateRules"/> (collect gate, hours, opener, answer, brawl outcome).</summary>
    public class TollGateRulesTests
    {
        // --- CanCollect: (inHours, paid, beaten, armed, onDuty) ---

        [Test]
        public void CanCollect_AllConditionsMet_True() =>
            Assert.IsTrue(TollGateRules.CanCollect(inHours: true, paid: false, beaten: false, armed: true, onDuty: true));

        [TestCase(false, false, false, true, true, TestName = "CanCollect_OutOfHours_False")]
        [TestCase(true, true, false, true, true, TestName = "CanCollect_GatePaid_False")]
        [TestCase(true, false, true, true, true, TestName = "CanCollect_CollectorBeaten_False")]
        [TestCase(true, false, false, false, true, TestName = "CanCollect_NotRearmed_False")]
        [TestCase(true, false, false, true, false, TestName = "CanCollect_CollectorOffDuty_False")]
        public void CanCollect_AnyConditionMissing_False(bool inHours, bool paid, bool beaten, bool armed, bool onDuty) =>
            Assert.IsFalse(TollGateRules.CanCollect(inHours, paid, beaten, armed, onDuty));

        // --- Hours: quarry 06–14, wilderness 14–21 ---

        [TestCase(6, true)]
        [TestCase(9, true)]
        [TestCase(13, true)]
        [TestCase(14, false)]
        [TestCase(22, false)]
        [TestCase(5, false)]
        public void QuarryHours(int hour, bool expected) =>
            Assert.AreEqual(expected, TollGateRules.IsCollectingHour(true, hour, 6, 14));

        [TestCase(14, true)]
        [TestCase(20, true)]
        [TestCase(21, false)]
        [TestCase(9, false)]
        public void WildernessHours(int hour, bool expected) =>
            Assert.AreEqual(expected, TollGateRules.IsCollectingHour(true, hour, 14, 21));

        [Test]
        public void Hours_NoClock_AlwaysCollecting() => Assert.IsTrue(TollGateRules.IsCollectingHour(false, 3, 6, 14));

        // --- Opener ---

        [Test]
        public void Opener_OtherGatePaid_UsesMockingOpener() => Assert.IsTrue(TollGateRules.UseOtherGateOpener(true));

        [Test]
        public void Opener_NothingPaid_UsesFirstOpener() => Assert.IsFalse(TollGateRules.UseOtherGateOpener(false));

        // --- Answer ---

        [Test]
        public void Outcome_Pay_Paid() => Assert.AreEqual(TollOutcome.Paid, TollGateRules.ResolveOutcome(true, false));

        [Test]
        public void Outcome_Defer_Deferred() => Assert.AreEqual(TollOutcome.Deferred, TollGateRules.ResolveOutcome(false, true));

        [Test]
        public void Outcome_NoAnswer_Refused() => Assert.AreEqual(TollOutcome.Refused, TollGateRules.ResolveOutcome(false, false));

        [Test]
        public void Outcome_PayWinsOverDefer() => Assert.AreEqual(TollOutcome.Paid, TollGateRules.ResolveOutcome(true, true));

        // --- Brawl ---

        [Test]
        public void Brawl_PlayerDown_PlayerLost() =>
            Assert.AreEqual(TollBrawlResult.PlayerLost, TollGateRules.EvaluateBrawl(true, false, false));

        [Test]
        public void Brawl_PlayerDownFirst_WinsOverCollectorDown() =>
            Assert.AreEqual(TollBrawlResult.PlayerLost, TollGateRules.EvaluateBrawl(true, true, false));

        [Test]
        public void Brawl_CollectorDown_CollectorLost() =>
            Assert.AreEqual(TollBrawlResult.CollectorLost, TollGateRules.EvaluateBrawl(false, true, false));

        [Test]
        public void Brawl_StillFighting_Ongoing() =>
            Assert.AreEqual(TollBrawlResult.Ongoing, TollGateRules.EvaluateBrawl(false, false, true));

        [Test]
        public void Brawl_CollectorStoppedFighting_Ended() =>
            Assert.AreEqual(TollBrawlResult.Ended, TollGateRules.EvaluateBrawl(false, false, false));
    }
}
