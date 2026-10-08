using Game.Stealth;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure theft-witness / chase rules in <see cref="TheftDetection"/>
    /// (used by EntityPerception.CanSeeTheft and EntityBrain Pursuing). Values mirror the spec's defaults.
    /// </summary>
    public class TheftDetectionTests
    {
        private const float EPS = 0.0001f;
        private const float THEFT_RANGE = 10f;
        private const float SNEAK_MULT = 0.6f;
        private const float VIEW = 110f;
        private const float PROX = 0.7f; // sneaking proximity (2 × 0.35)

        // ── EffectiveTheftRange ────────────────────────────────────────────────

        [Test]
        public void EffectiveTheftRange_Standing_FullRange()
        {
            Assert.AreEqual(10f, TheftDetection.EffectiveTheftRange(THEFT_RANGE, false, SNEAK_MULT), EPS);
        }

        [Test]
        public void EffectiveTheftRange_Sneaking_Shortened()
        {
            Assert.AreEqual(6f, TheftDetection.EffectiveTheftRange(THEFT_RANGE, true, SNEAK_MULT), EPS);
        }

        // ── IsInView ───────────────────────────────────────────────────────────

        [Test]
        public void IsInView_AheadWithinRange_True()
        {
            Assert.IsTrue(TheftDetection.IsInView(8f, 0f, THEFT_RANGE, VIEW, PROX));
        }

        [Test]
        public void IsInView_BeyondRange_False()
        {
            Assert.IsFalse(TheftDetection.IsInView(8f, 0f, 6f, VIEW, PROX));
        }

        [Test]
        public void IsInView_OutsideCone_False()
        {
            Assert.IsFalse(TheftDetection.IsInView(3f, 90f, THEFT_RANGE, VIEW, PROX));
        }

        [Test]
        public void IsInView_BehindInsideProximity_True()
        {
            Assert.IsTrue(TheftDetection.IsInView(0.5f, 180f, THEFT_RANGE, VIEW, PROX));
        }

        [Test]
        public void IsInView_BehindOutsideProximity_False()
        {
            Assert.IsFalse(TheftDetection.IsInView(1f, 180f, THEFT_RANGE, VIEW, PROX));
        }

        [Test]
        public void IsInView_ZeroRange_False()
        {
            Assert.IsFalse(TheftDetection.IsInView(3f, 0f, 0f, VIEW, 0f));
        }

        // ── ShouldGiveUp ───────────────────────────────────────────────────────

        private static bool GiveUp(float noSight = 0f, float distance = 5f, float elapsed = 1f) =>
            TheftDetection.ShouldGiveUp(noSight, 4f, distance, 20f, elapsed, 30f);

        [Test]
        public void ShouldGiveUp_NoCondition_False()
        {
            Assert.IsFalse(GiveUp());
        }

        [Test]
        public void ShouldGiveUp_LostSightLongEnough_True()
        {
            Assert.IsTrue(GiveUp(noSight: 4f));
            Assert.IsFalse(GiveUp(noSight: 3.99f));
        }

        [Test]
        public void ShouldGiveUp_TooFar_True()
        {
            Assert.IsTrue(GiveUp(distance: 20.01f));
            Assert.IsFalse(GiveUp(distance: 20f));
        }

        [Test]
        public void ShouldGiveUp_TooLong_True()
        {
            Assert.IsTrue(GiveUp(elapsed: 30f));
            Assert.IsFalse(GiveUp(elapsed: 29.99f));
        }

        // ── CanCatch ───────────────────────────────────────────────────────────

        [Test]
        public void CanCatch_CursorUnlocked_False()
        {
            Assert.IsFalse(TheftDetection.CanCatch(1f, 1.8f, false));
        }

        [Test]
        public void CanCatch_AtCatchDistance_True()
        {
            Assert.IsTrue(TheftDetection.CanCatch(1.8f, 1.8f, true));
        }

        [Test]
        public void CanCatch_BeyondCatchDistance_False()
        {
            Assert.IsFalse(TheftDetection.CanCatch(1.81f, 1.8f, true));
        }
    }
}
