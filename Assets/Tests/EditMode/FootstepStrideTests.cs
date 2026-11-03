using Game.Audio;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for FootstepStride (distance-based footsteps).</summary>
    public class FootstepStrideTests
    {
        private const float DT = 0.1f;
        private const float MIN_SPEED = 0.5f;

        private static int CountSteps(FootstepStride stride, int frames, float perFrame, float strideLength)
        {
            int steps = 0;
            for (int i = 0; i < frames; i++)
                if (stride.Advance(perFrame, DT, true, strideLength, MIN_SPEED)) steps++;
            return steps;
        }

        [Test]
        public void Walking_EmitsOneStepPerStride()
        {
            // 30 frames × 0.25 m = 7.5 m at a 1.25 m stride → 6 steps (binary-exact values).
            Assert.AreEqual(6, CountSteps(new FootstepStride(), 30, 0.25f, 1.25f));
        }

        [Test]
        public void Airborne_ResetsAndEmitsNothing()
        {
            var stride = new FootstepStride();
            CountSteps(stride, 4, 0.25f, 1.5f); // 1 m banked
            Assert.IsFalse(stride.Advance(0.25f, DT, false, 1.5f, MIN_SPEED));
            Assert.AreEqual(0f, stride.Accumulated, 0.0001f);
        }

        [Test]
        public void Teleport_ResetsWithoutStep()
        {
            var stride = new FootstepStride();
            Assert.IsFalse(stride.Advance(FootstepStride.TELEPORT_DISTANCE + 0.1f, DT, true, 1.5f, MIN_SPEED));
            Assert.AreEqual(0f, stride.Accumulated, 0.0001f);
        }

        [Test]
        public void BelowMinSpeed_PausesWithoutReset()
        {
            var stride = new FootstepStride();
            CountSteps(stride, 4, 0.25f, 1.5f); // 1 m banked
            Assert.IsFalse(stride.Advance(0.01f, DT, true, 1.5f, MIN_SPEED)); // 0.1 m/s stall
            Assert.AreEqual(1f, stride.Accumulated, 0.0001f);
            Assert.AreEqual(1, CountSteps(stride, 2, 0.25f, 1.5f)); // cadence kept: 1.5 m reached
        }

        [Test]
        public void ZeroDeltaTime_DoesNotStep()
        {
            Assert.IsFalse(new FootstepStride().Advance(0.25f, 0f, true, 1.5f, MIN_SPEED));
        }

        [Test]
        public void LargeFrame_NeverOwesMoreThanOneStep()
        {
            var stride = new FootstepStride();
            Assert.IsTrue(stride.Advance(1.9f, DT, true, 0.5f, MIN_SPEED)); // 3.8 strides in one frame
            Assert.Less(stride.Accumulated, 0.5f);
        }

        [TestCase(3f, 4.5f, false)]
        [TestCase(4.5f, 4.5f, true)]
        [TestCase(6f, 4.5f, true)]
        public void IsRunning(float speed, float threshold, bool expected)
        {
            Assert.AreEqual(expected, FootstepStride.IsRunning(speed, threshold));
        }
    }
}
