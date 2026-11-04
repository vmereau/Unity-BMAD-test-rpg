using Game.Player;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class SwimRulesTests
    {
        private const float ENTER = 1.3f;
        private const float EXIT = 1.1f;
        private const float FLOAT = 1.45f;

        [Test]
        public void ShouldStart_InShallows_IsFalse() => Assert.IsFalse(SwimRules.ShouldStart(10f, 9f, ENTER));

        [Test]
        public void ShouldStart_PastEnterDepth_IsTrue() => Assert.IsTrue(SwimRules.ShouldStart(10f, 8.6f, ENTER));

        [Test]
        public void ShouldStop_WhileFloating_IsFalse() =>
            Assert.IsFalse(SwimRules.ShouldStop(10f, SwimRules.FloatFeetY(10f, FLOAT), EXIT));

        [Test]
        public void ShouldStop_WhenGroundLiftsFeetPastExitDepth_IsTrue() => Assert.IsTrue(SwimRules.ShouldStop(10f, 9f, EXIT));

        [Test]
        public void BetweenThresholds_NeitherStartsNorStops()
        {
            const float feet = 8.8f; // depth 1.2
            Assert.IsFalse(SwimRules.ShouldStart(10f, feet, ENTER));
            Assert.IsFalse(SwimRules.ShouldStop(10f, feet, EXIT));
        }

        [Test]
        public void VerticalSpeed_FarBelowTarget_ClampsToMaxSpeed() =>
            Assert.AreEqual(2f, SwimRules.VerticalSpeed(0f, 5f, 2f, 0.02f), 1e-4f);

        [Test]
        public void VerticalSpeed_NearTarget_DoesNotOvershoot() =>
            Assert.AreEqual(0.5f, SwimRules.VerticalSpeed(0f, 0.01f, 2f, 0.02f), 1e-4f);

        [Test]
        public void VerticalSpeed_ZeroDeltaTime_IsZero() => Assert.AreEqual(0f, SwimRules.VerticalSpeed(0f, 5f, 2f, 0f));

        [Test]
        public void HighestSurface_PicksTheHighestWithinCount()
        {
            float[] surfaces = { 3f, 7f, 99f };
            Assert.IsTrue(SwimRules.TryGetHighestSurface(surfaces, 2, out float surface));
            Assert.AreEqual(7f, surface);
        }

        [Test]
        public void HighestSurface_NoVolumes_IsFalse() =>
            Assert.IsFalse(SwimRules.TryGetHighestSurface(new float[1], 0, out _));
    }
}
