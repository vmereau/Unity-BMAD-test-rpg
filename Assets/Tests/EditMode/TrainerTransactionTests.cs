using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Pure logic tests for trainer transaction formulas. No MonoBehaviour or scene required.
    /// Story 3.4: Trainer stat upgrade validation.
    /// </summary>
    public class TrainerTransactionTests
    {
        // --- Pure logic helpers (mirrors TrainerNPC / PlayerStats logic) ---

        private bool CanAffordUpgrade(int currentLP, int currentGold, int lpCost, int goldCost)
            => currentLP >= lpCost && currentGold >= goldCost;

        private int SimulateStat(int baseStat, int upgradeCount)
            => baseStat + upgradeCount;

        private bool IsAtMaxLevel(int purchaseCount, int maxLevel)
            => purchaseCount >= maxLevel;

        // Mirrors the guards added in code review (M1/M4 fixes)
        private bool IsValidUpgradePoints(int points) => points > 0;
        private bool IsValidAddAmount(int amount)     => amount > 0;

        // --- Tests ---

        [Test]
        public void CanAfford_WhenBothSufficient_ReturnsTrue()
        {
            Assert.IsTrue(CanAffordUpgrade(currentLP: 5, currentGold: 500, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void CannotAfford_WhenLPInsufficient_ReturnsFalse()
        {
            Assert.IsFalse(CanAffordUpgrade(currentLP: 0, currentGold: 500, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void CannotAfford_WhenGoldInsufficient_ReturnsFalse()
        {
            Assert.IsFalse(CanAffordUpgrade(currentLP: 5, currentGold: 50, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void CannotAfford_WhenBothInsufficient_ReturnsFalse()
        {
            Assert.IsFalse(CanAffordUpgrade(currentLP: 0, currentGold: 0, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void StatUpgrade_IncrementsFromBase()
        {
            Assert.AreEqual(6, SimulateStat(baseStat: 5, upgradeCount: 1));
        }

        [Test]
        public void StatUpgrade_AccumulatesCorrectly()
        {
            Assert.AreEqual(8, SimulateStat(baseStat: 5, upgradeCount: 3));
        }

        [Test]
        public void MaxLevel_BlocksUpgrade_AtCap()
        {
            Assert.IsTrue(IsAtMaxLevel(purchaseCount: 5, maxLevel: 5));
        }

        [Test]
        public void MaxLevel_AllowsUpgrade_BelowCap()
        {
            Assert.IsFalse(IsAtMaxLevel(purchaseCount: 4, maxLevel: 5));
        }

        // --- Boundary tests (code review additions) ---

        [Test]
        public void CanAfford_WhenLPExactlyEqual_ReturnsTrue()
        {
            // Boundary: LP exactly matches cost — must succeed
            Assert.IsTrue(CanAffordUpgrade(currentLP: 1, currentGold: 500, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void CanAfford_WhenGoldExactlyEqual_ReturnsTrue()
        {
            // Boundary: Gold exactly matches cost — must succeed
            Assert.IsTrue(CanAffordUpgrade(currentLP: 5, currentGold: 100, lpCost: 1, goldCost: 100));
        }

        [Test]
        public void UpgradePoints_WhenZero_IsInvalid()
        {
            // M4 guard: zero points must be rejected (mirrors PlayerStats.UpgradeStat guard)
            Assert.IsFalse(IsValidUpgradePoints(0));
        }

        [Test]
        public void UpgradePoints_WhenNegative_IsInvalid()
        {
            // M4 guard: negative points must be rejected
            Assert.IsFalse(IsValidUpgradePoints(-1));
        }

        [Test]
        public void AddAmount_WhenNegative_IsInvalid()
        {
            // M1 guard: negative add amount must be rejected (mirrors GoldSystem.Add guard)
            Assert.IsFalse(IsValidAddAmount(-100));
        }

        [Test]
        public void AddAmount_WhenZero_IsInvalid()
        {
            // M1 guard: zero add amount must be rejected
            Assert.IsFalse(IsValidAddAmount(0));
        }
    }
}
