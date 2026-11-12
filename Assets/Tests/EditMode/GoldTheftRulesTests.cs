using Game.Economy;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class GoldTheftRulesTests
    {
        [TestCase(200, 0.10f, 35)]
        [TestCase(200, 0.20f, 55)]
        [TestCase(100, 0.15f, 30)]
        public void Amount_IsBasePlusFraction(int gold, float fraction, int expected)
        {
            Assert.AreEqual(expected, GoldTheftRules.Amount(gold, 15, fraction));
        }

        [Test]
        public void Amount_LessThanBase_TakesEverything()
        {
            Assert.AreEqual(8, GoldTheftRules.Amount(8, 15, 0.2f));
        }

        [Test]
        public void Amount_ExactlyBase_TakesEverything()
        {
            Assert.AreEqual(15, GoldTheftRules.Amount(15, 15, 0.1f));
        }

        [Test]
        public void Amount_NoGold_IsZero()
        {
            Assert.AreEqual(0, GoldTheftRules.Amount(0, 15, 0.2f));
            Assert.AreEqual(0, GoldTheftRules.Amount(-5, 15, 0.2f));
        }

        [Test]
        public void Amount_RoundsTheFraction()
        {
            // 17 × 0.1 = 1.7 → 2; base 15 → 17 = everything.
            Assert.AreEqual(17, GoldTheftRules.Amount(17, 15, 0.1f));
            // 33 × 0.1 = 3.3 → 3; 15 + 3 = 18.
            Assert.AreEqual(18, GoldTheftRules.Amount(33, 15, 0.1f));
        }

        [Test]
        public void Amount_NegativeInputsClampToZero()
        {
            Assert.AreEqual(0, GoldTheftRules.Amount(50, -10, -1f));
        }
    }
}
