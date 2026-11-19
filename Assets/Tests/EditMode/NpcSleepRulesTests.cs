using Game.AI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure NpcSleepRules used by EntityBrain's Sleep routine (spec-npc-routine-sleep).
    /// </summary>
    public class NpcSleepRulesTests
    {
        private const float DELAY = 10f;
        private const float WALK_RADIUS = 4f;

        [Test]
        public void NeverWoken_LiesDownOnArrival()
        {
            Assert.IsTrue(NpcSleepRules.ShouldLieDown(false, 0f, DELAY));
        }

        [Test]
        public void Woken_WaitsUntilQuietForTheDelay()
        {
            Assert.IsFalse(NpcSleepRules.ShouldLieDown(true, 0f, DELAY));
            Assert.IsFalse(NpcSleepRules.ShouldLieDown(true, 9.9f, DELAY));
            Assert.IsTrue(NpcSleepRules.ShouldLieDown(true, 10f, DELAY));
        }

        [Test]
        public void ZeroDelay_LiesDownAtOnce()
        {
            Assert.IsTrue(NpcSleepRules.ShouldLieDown(true, 0f, 0f));
        }

        [Test]
        public void PlayerWithinWalkRadius_IsNotQuiet()
        {
            Assert.IsFalse(NpcSleepRules.IsQuiet(false, 2f, WALK_RADIUS));
            Assert.IsFalse(NpcSleepRules.IsQuiet(false, WALK_RADIUS, WALK_RADIUS)); // on the edge still counts as near
        }

        [Test]
        public void PlayerBeyondWalkRadius_IsQuiet()
        {
            Assert.IsTrue(NpcSleepRules.IsQuiet(false, 6f, WALK_RADIUS));
        }

        [Test]
        public void NoPlayer_IsQuiet()
        {
            Assert.IsTrue(NpcSleepRules.IsQuiet(true, 0f, WALK_RADIUS));
        }
    }
}
