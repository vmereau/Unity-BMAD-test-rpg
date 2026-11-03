using Game.Inventory;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class AttackStepTests
    {
        private const float TOLERANCE = 1e-5f;

        // 0.6 m from 0.3 s to 0.6 s → 2 m/s
        private static readonly AttackStep Step = new AttackStep(0.6f, 0.3f, 0.3f);

        [Test]
        public void Displacement_BeforeTheWindow_IsZero()
            => Assert.That(Step.DisplacementBetween(0f, 0.3f), Is.EqualTo(0f));

        [Test]
        public void Displacement_InsideTheWindow_IsConstantSpeed()
            => Assert.That(Step.DisplacementBetween(0.35f, 0.45f), Is.EqualTo(0.2f).Within(TOLERANCE));

        [Test]
        public void Displacement_FrameStraddlingTheStart_CountsOnlyTheOverlap()
            => Assert.That(Step.DisplacementBetween(0.25f, 0.35f), Is.EqualTo(0.1f).Within(TOLERANCE));

        [Test]
        public void Displacement_AfterTheWindow_IsZero()
            => Assert.That(Step.DisplacementBetween(0.6f, 1f), Is.EqualTo(0f));

        [Test]
        public void Displacement_WholeAttack_IsTheStepDistance()
            => Assert.That(Step.DisplacementBetween(0f, 2f), Is.EqualTo(0.6f).Within(TOLERANCE));

        [Test]
        public void Displacement_SumOfFrames_IsTheStepDistance()
        {
            float total = 0f;
            for (float t = 0f; t < 1f; t += 1f / 60f) total += Step.DisplacementBetween(t, t + 1f / 60f);
            Assert.That(total, Is.EqualTo(0.6f).Within(1e-4f));
        }

        [Test]
        public void Displacement_ZeroDistance_IsZero()
            => Assert.That(new AttackStep(0f, 0f, 0.3f).DisplacementBetween(0f, 1f), Is.EqualTo(0f));

        [Test]
        public void Displacement_ZeroDuration_IsZero()
            => Assert.That(new AttackStep(0.5f, 0.2f, 0f).DisplacementBetween(0f, 1f), Is.EqualTo(0f));

        [Test]
        public void Resolve_IndexOutOfRange_UsesFallback()
        {
            var fallback = new AttackStep(0.5f, 0.35f, 0.35f);
            Assert.That(AttackStep.Resolve(new[] { Step }, 0, fallback).distance, Is.EqualTo(0.5f));
            Assert.That(AttackStep.Resolve(new[] { Step }, 2, fallback).distance, Is.EqualTo(0.5f));
            Assert.That(AttackStep.Resolve(null, 1, fallback).distance, Is.EqualTo(0.5f));
            Assert.That(AttackStep.Resolve(new[] { Step }, 1, fallback).distance, Is.EqualTo(0.6f));
        }
    }
}
