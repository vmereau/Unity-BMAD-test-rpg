using Game.Player;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Edit Mode tests for <see cref="MovementStaminaRules"/> (aggro run / jump costs, fall damage).</summary>
    public class MovementStaminaRulesTests
    {
        private const float SAFE = 11f;
        private const float PER_METRE = 4f;

        [TestCase(0f, 0f)]
        [TestCase(7f, 0f)]    // goat-track lower step
        [TestCase(10f, 0f)]   // goat-track upper step
        [TestCase(11f, 0f)]   // exactly the safe height
        [TestCase(16f, 20f)]
        [TestCase(21f, 40f)]  // kills a fresh 40 HP character
        public void FallDamage_ScalesAboveSafeHeight(float height, float expected)
        {
            Assert.That(MovementStaminaRules.FallDamage(height, SAFE, PER_METRE), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void FallDamage_NegativePerMetre_DealsNothing()
        {
            Assert.That(MovementStaminaRules.FallDamage(30f, SAFE, -4f), Is.EqualTo(0f));
        }

        [Test]
        public void DrainStep_UnderOnePoint_AccumulatesWithoutConsuming()
        {
            float debt = 0f;
            float consume = MovementStaminaRules.DrainStep(ref debt, 8f, 0.1f, 100f, out bool exhausted);
            Assert.That(consume, Is.EqualTo(0f));
            Assert.That(debt, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(exhausted, Is.False);
        }

        [Test]
        public void DrainStep_PaysWholePointsAndKeepsRemainder()
        {
            float debt = 0.8f;
            float consume = MovementStaminaRules.DrainStep(ref debt, 8f, 0.1f, 100f, out bool exhausted);
            Assert.That(consume, Is.EqualTo(1f));
            Assert.That(debt, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(exhausted, Is.False);
        }

        [Test]
        public void DrainStep_FiveSecondsAtEightPerSecond_CostsForty()
        {
            float debt = 0f, total = 0f;
            for (int i = 0; i < 300; i++)
                total += MovementStaminaRules.DrainStep(ref debt, 8f, 1f / 60f, 100f, out _);
            Assert.That(total, Is.InRange(39f, 40f));
        }

        [Test]
        public void DrainStep_NotEnoughStamina_ExhaustsAndResetsDebt()
        {
            float debt = 0.9f;
            float consume = MovementStaminaRules.DrainStep(ref debt, 8f, 0.1f, 0.5f, out bool exhausted);
            Assert.That(consume, Is.EqualTo(0f));
            Assert.That(exhausted, Is.True);
            Assert.That(debt, Is.EqualTo(0f));
        }

        [TestCase(15f, 1f, 15f)]
        [TestCase(15f, 0.5f, 7.5f)]
        [TestCase(15f, -1f, 0f)]
        public void Modified_AppliesMultiplierNeverNegative(float baseValue, float multiplier, float expected)
        {
            Assert.That(MovementStaminaRules.Modified(baseValue, multiplier), Is.EqualTo(expected).Within(0.0001f));
        }
    }
}
