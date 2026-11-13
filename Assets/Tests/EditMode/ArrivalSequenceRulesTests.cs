using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class ArrivalSequenceRulesTests
    {
        // --- Escort ---

        [Test]
        public void Escort_GuardsStillWalking_Walking() =>
            Assert.AreEqual(EscortStep.Walking, ArrivalSequenceRules.EvaluateEscort(false, 1f, 7f, false));

        [Test]
        public void Escort_ArrivedPlayerLagging_Waits() =>
            Assert.AreEqual(EscortStep.WaitForPlayer, ArrivalSequenceRules.EvaluateEscort(true, 9f, 7f, false));

        [Test]
        public void Escort_ArrivedPlayerClose_Advances() =>
            Assert.AreEqual(EscortStep.Advance, ArrivalSequenceRules.EvaluateEscort(true, 3f, 7f, false));

        [Test]
        public void Escort_LastPointPlayerClose_ReachedEnd() =>
            Assert.AreEqual(EscortStep.ReachedEnd, ArrivalSequenceRules.EvaluateEscort(true, 7f, 7f, true));

        [Test]
        public void Escort_LastPointPlayerLagging_Waits() =>
            Assert.AreEqual(EscortStep.WaitForPlayer, ArrivalSequenceRules.EvaluateEscort(true, 8f, 7f, true));

        // --- Stray ---

        [Test]
        public void Stray_CloseNotWarned_None() =>
            Assert.AreEqual(StrayAction.None, ArrivalSequenceRules.EvaluateStray(5f, 14f, false, 0f, 4f, false));

        [Test]
        public void Stray_FarNotWarned_Warns() =>
            Assert.AreEqual(StrayAction.Warn, ArrivalSequenceRules.EvaluateStray(15f, 14f, false, 0f, 4f, false));

        [Test]
        public void Stray_FarWarnedInGrace_None() =>
            Assert.AreEqual(StrayAction.None, ArrivalSequenceRules.EvaluateStray(15f, 14f, true, 3f, 4f, false));

        [Test]
        public void Stray_FarWarnedAfterGrace_Brawls() =>
            Assert.AreEqual(StrayAction.Brawl, ArrivalSequenceRules.EvaluateStray(15f, 14f, true, 4.5f, 4f, false));

        [Test]
        public void Stray_BackAfterWarning_Clears() =>
            Assert.AreEqual(StrayAction.Clear, ArrivalSequenceRules.EvaluateStray(10f, 14f, true, 1f, 4f, false));

        [Test]
        public void Stray_WhileBrawling_None() =>
            Assert.AreEqual(StrayAction.None, ArrivalSequenceRules.EvaluateStray(30f, 14f, true, 10f, 4f, true));

        // --- Beating ---

        [Test]
        public void Beating_InProgress_Ongoing() =>
            Assert.AreEqual(BeatingOutcome.Ongoing, ArrivalSequenceRules.EvaluateBeating(false, false, 5f, 25f));

        [Test]
        public void Beating_VictimDown_VictimDown() =>
            Assert.AreEqual(BeatingOutcome.VictimDown, ArrivalSequenceRules.EvaluateBeating(true, false, 5f, 25f));

        [Test]
        public void Beating_VictimDownAtTimeout_StillVictimDown() =>
            Assert.AreEqual(BeatingOutcome.VictimDown, ArrivalSequenceRules.EvaluateBeating(true, false, 30f, 25f));

        [Test]
        public void Beating_BeaterDown_Interrupted() =>
            Assert.AreEqual(BeatingOutcome.Interrupted, ArrivalSequenceRules.EvaluateBeating(false, true, 5f, 25f));

        [Test]
        public void Beating_Timeout_Interrupted() =>
            Assert.AreEqual(BeatingOutcome.Interrupted, ArrivalSequenceRules.EvaluateBeating(false, false, 26f, 25f));

        // --- Hut briefing ---

        [Test]
        public void Briefing_SpeakerDown_Skips() =>
            Assert.AreEqual(BriefingStep.Skip, ArrivalSequenceRules.EvaluateBriefing(true, true, true, 0f, 8f));

        [Test]
        public void Briefing_PlayerOutOfRange_Waits() =>
            Assert.AreEqual(BriefingStep.Wait, ArrivalSequenceRules.EvaluateBriefing(false, false, true, 0f, 8f));

        [Test]
        public void Briefing_InRangeButBlocked_Waits() =>
            Assert.AreEqual(BriefingStep.Wait, ArrivalSequenceRules.EvaluateBriefing(false, true, false, 1f, 8f));

        [Test]
        public void Briefing_InRangeAndFree_Opens() =>
            Assert.AreEqual(BriefingStep.Open, ArrivalSequenceRules.EvaluateBriefing(false, true, true, 1f, 8f));

        [Test]
        public void Briefing_Timeout_Skips() =>
            Assert.AreEqual(BriefingStep.Skip, ArrivalSequenceRules.EvaluateBriefing(false, true, true, 8.5f, 8f));
    }
}
