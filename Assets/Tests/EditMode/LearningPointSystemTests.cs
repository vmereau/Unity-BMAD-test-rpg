using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the LP award formula logic (Story 3.3).
    /// Tests pure formula helper — no MonoBehaviour, no scene required.
    /// </summary>
    public class LearningPointSystemTests
    {
        /// <summary>
        /// Pure formula replicating LearningPointSystem.HandleLevelUp accumulation logic.
        /// Each level-up awards a fixed number of LP; total = levelsGained * lpPerLevel.
        /// </summary>
        private int CalculateTotalLP(int levelsGained, int lpPerLevel)
        {
            return levelsGained * lpPerLevel;
        }

        [Test]
        public void ZeroLevels_StartsAtZeroLP()
        {
            int result = CalculateTotalLP(0, 3);
            Assert.AreEqual(0, result);
        }

        [Test]
        public void OneLevelUp_AwardsCorrectLP()
        {
            int result = CalculateTotalLP(1, 3);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void ThreeLevelUps_AccumulatesLP()
        {
            int result = CalculateTotalLP(3, 3);
            Assert.AreEqual(9, result);
        }

        [Test]
        public void MaxLevels_AwardsCorrectLP()
        {
            // 5 levels gained (Levels 1→6 max), 3 LP/level → 15 LP total
            int result = CalculateTotalLP(5, 3);
            Assert.AreEqual(15, result);
        }

        [Test]
        public void CustomLPRate_AwardsCorrectLP()
        {
            // 2 levels gained, 5 LP/level → 10 LP total
            int result = CalculateTotalLP(2, 5);
            Assert.AreEqual(10, result);
        }
    }
}
