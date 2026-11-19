using Game.AI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure TalkOpenerRules used by NPCPresence talk openers.
    /// </summary>
    public class TalkOpenerRulesTests
    {
        [Test]
        public void IsPending_ConditionMetAndNotPlayed_True() =>
            Assert.IsTrue(TalkOpenerRules.IsPending(true, true, false, false));

        [Test]
        public void IsPending_ConditionNotMet_False() =>
            Assert.IsFalse(TalkOpenerRules.IsPending(true, false, false, false));

        [Test]
        public void IsPending_AlreadyPlayed_False() =>
            Assert.IsFalse(TalkOpenerRules.IsPending(true, true, true, false));

        [Test]
        public void IsPending_Incomplete_False() =>
            Assert.IsFalse(TalkOpenerRules.IsPending(false, true, false, false));

        [Test]
        public void IsPending_WhileChasingThief_False() =>
            Assert.IsFalse(TalkOpenerRules.IsPending(true, true, false, true));

        [Test]
        public void CancelsOnWitness_FlaggedConditionMetNotPlayed_True() =>
            Assert.IsTrue(TalkOpenerRules.CancelsOnWitness(true, true, true, false));

        [Test]
        public void CancelsOnWitness_NothingTakenYet_False() =>
            Assert.IsFalse(TalkOpenerRules.CancelsOnWitness(true, true, false, false));

        [Test]
        public void CancelsOnWitness_NotFlagged_False() =>
            Assert.IsFalse(TalkOpenerRules.CancelsOnWitness(true, false, true, false));

        [Test]
        public void CancelsOnWitness_AlreadyPlayed_False() =>
            Assert.IsFalse(TalkOpenerRules.CancelsOnWitness(true, true, true, true));

        [Test]
        public void CancelsOnWitness_Incomplete_False() =>
            Assert.IsFalse(TalkOpenerRules.CancelsOnWitness(false, true, true, false));
    }
}
