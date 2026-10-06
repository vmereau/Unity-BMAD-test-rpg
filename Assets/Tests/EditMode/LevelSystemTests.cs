using Game.Progression;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the level-up formula logic (Story 3.2) and the in-level progress formula
    /// (HUD experience bar). Level tests use a private formula copy; progress tests call the real
    /// LevelSystem.CalculateLevelProgress. No MonoBehaviour, no scene required.
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

        // ── LevelSystem.CalculateLevelProgress (HUD experience bar) ──

        [Test]
        public void Progress_ZeroXPAtLevel1_IsEmpty()
        {
            Assert.AreEqual(0f, LevelSystem.CalculateLevelProgress(0, 1, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_HalfwayThroughLevel1_IsHalf()
        {
            Assert.AreEqual(0.5f, LevelSystem.CalculateLevelProgress(50, 1, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_JustLevelledTo2_IsEmpty()
        {
            Assert.AreEqual(0f, LevelSystem.CalculateLevelProgress(100, 2, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_HalfwayThroughLevel2_UsesPreviousThreshold()
        {
            // (175 − 100) / (250 − 100) = 0.5
            Assert.AreEqual(0.5f, LevelSystem.CalculateLevelProgress(175, 2, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_OneXPBeforeLastThreshold_IsNearlyFull()
        {
            // (1399 − 900) / (1400 − 900) = 0.998
            Assert.AreEqual(499f / 500f, LevelSystem.CalculateLevelProgress(1399, 5, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_AtMaxLevel_IsFull()
        {
            Assert.AreEqual(1f, LevelSystem.CalculateLevelProgress(1400, 6, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_BeyondMaxXP_IsFull()
        {
            Assert.AreEqual(1f, LevelSystem.CalculateLevelProgress(9999, 6, DefaultThresholds), 0.001f);
        }

        [Test]
        public void Progress_NullThresholds_IsFull()
        {
            Assert.AreEqual(1f, LevelSystem.CalculateLevelProgress(50, 1, null), 0.001f);
        }

        [Test]
        public void Progress_EmptyThresholds_IsFull()
        {
            Assert.AreEqual(1f, LevelSystem.CalculateLevelProgress(50, 1, new int[0]), 0.001f);
        }

        [Test]
        public void Progress_DegenerateSpan_IsFull()
        {
            Assert.AreEqual(1f, LevelSystem.CalculateLevelProgress(100, 2, new[] { 100, 100 }), 0.001f);
        }

        [Test]
        public void Progress_LevelZero_TreatedAsLevel1()
        {
            Assert.AreEqual(0.5f, LevelSystem.CalculateLevelProgress(50, 0, DefaultThresholds), 0.001f);
        }
    }
}
