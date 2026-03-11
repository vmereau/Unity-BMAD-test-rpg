using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the level-up formula logic (Story 3.2).
    /// Tests pure formula helper — no MonoBehaviour, no scene required.
    /// </summary>
    public class LevelSystemTests
    {
        // Default thresholds from ProgressionConfigSO: { 100, 250, 500, 900, 1400 }
        private static readonly int[] DefaultThresholds = { 100, 250, 500, 900, 1400 };

        /// <summary>
        /// Pure formula replicating LevelSystem.CheckLevelUp logic.
        /// Level 1 → index 0 (threshold 100), Level 5 → index 4 (threshold 1400).
        /// MaxLevel = thresholds.Length + 1 = 6.
        /// </summary>
        private int CalculateLevel(int totalXP, int[] xpThresholds)
        {
            int level = 1;
            for (int i = 0; i < xpThresholds.Length; i++)
            {
                if (totalXP >= xpThresholds[i]) level = i + 2;
                else break;
            }
            return level;
        }

        [Test]
        public void ZeroXP_StartsAtLevel1()
        {
            int result = CalculateLevel(0, DefaultThresholds);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void XPBelowFirstThreshold_StaysAtLevel1()
        {
            int result = CalculateLevel(99, DefaultThresholds);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void XPAtFirstThreshold_ReachesLevel2()
        {
            int result = CalculateLevel(100, DefaultThresholds);
            Assert.AreEqual(2, result);
        }

        [Test]
        public void XPAtSecondThreshold_ReachesLevel3()
        {
            int result = CalculateLevel(250, DefaultThresholds);
            Assert.AreEqual(3, result);
        }

        [Test]
        public void MaxXP_ReachesMaxLevel()
        {
            int maxLevel = DefaultThresholds.Length + 1; // 6
            int result = CalculateLevel(1400, DefaultThresholds);
            Assert.AreEqual(maxLevel, result);
        }

        [Test]
        public void XPBeyondMax_CapsAtMaxLevel()
        {
            int maxLevel = DefaultThresholds.Length + 1; // 6
            int result = CalculateLevel(9999, DefaultThresholds);
            Assert.AreEqual(maxLevel, result);
        }

        [Test]
        public void BulkXP_CanSkipMultipleLevels()
        {
            // 501 XP crosses threshold[0]=100 (→Level2), threshold[1]=250 (→Level3),
            // and threshold[2]=500 (→Level4). Stops before threshold[3]=900.
            int result = CalculateLevel(501, DefaultThresholds);
            Assert.AreEqual(4, result);
        }

        [Test]
        public void XPAtFourthThreshold_ReachesLevel5()
        {
            int result = CalculateLevel(900, DefaultThresholds);
            Assert.AreEqual(5, result);
        }
    }
}
