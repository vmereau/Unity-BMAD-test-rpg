using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure LockpickRules shared by DoorSystem and ContainerSystem.
    /// </summary>
    public class LockpickRulesTests
    {
        [Test]
        public void Evaluate_SkillAndLockpick_Success()
        {
            Assert.AreEqual(LockpickResult.Success, LockpickRules.Evaluate(true, 1));
        }

        [Test]
        public void Evaluate_SkillNoLockpick_MissingLockpick()
        {
            Assert.AreEqual(LockpickResult.MissingLockpick, LockpickRules.Evaluate(true, 0));
        }

        [Test]
        public void Evaluate_NoSkillWithLockpick_MissingSkill()
        {
            Assert.AreEqual(LockpickResult.MissingSkill, LockpickRules.Evaluate(false, 5));
        }

        [Test]
        public void Evaluate_Neither_OnlyMissingSkill()
        {
            Assert.AreEqual(LockpickResult.MissingSkill, LockpickRules.Evaluate(false, 0));
        }

        [Test]
        public void Evaluate_Key_WinsWithoutSkillOrLockpick()
        {
            Assert.AreEqual(LockpickResult.Key, LockpickRules.Evaluate(true, false, 0));
        }

        [Test]
        public void Evaluate_KeyAndPick_KeyWins()
        {
            Assert.AreEqual(LockpickResult.Key, LockpickRules.Evaluate(true, true, 3));
        }

        [Test]
        public void Evaluate_NoKey_FallsBackToLockpickRules()
        {
            Assert.AreEqual(LockpickResult.Success, LockpickRules.Evaluate(false, true, 1));
            Assert.AreEqual(LockpickResult.MissingSkill, LockpickRules.Evaluate(false, false, 1));
            Assert.AreEqual(LockpickResult.MissingLockpick, LockpickRules.Evaluate(false, true, 0));
        }

        [Test]
        public void FailureMessage_MapsEachResult()
        {
            Assert.AreEqual("You lack the skill to pick this lock", LockpickRules.FailureMessage(LockpickResult.MissingSkill));
            Assert.AreEqual("You need a lockpick", LockpickRules.FailureMessage(LockpickResult.MissingLockpick));
            Assert.IsNull(LockpickRules.FailureMessage(LockpickResult.Success));
            Assert.IsNull(LockpickRules.FailureMessage(LockpickResult.Key));
        }
    }
}
