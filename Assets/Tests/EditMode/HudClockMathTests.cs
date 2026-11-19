using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for the HUD clock dial layout.</summary>
    public class HudClockMathTests
    {
        private const float R = 40f;
        private const float EPS = 1e-3f;

        private static void AssertPos(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, EPS, "x");
            Assert.AreEqual(expected.y, actual.y, EPS, "y");
        }

        [Test]
        public void Progress0_LeftHorizon() => AssertPos(new Vector2(-R, 0f), HudClockMath.DialPosition(0f, R));

        [Test]
        public void ProgressHalf_Top() => AssertPos(new Vector2(0f, R), HudClockMath.DialPosition(0.5f, R));

        [Test]
        public void Progress1_RightHorizon() => AssertPos(new Vector2(R, 0f), HudClockMath.DialPosition(1f, R));

        [Test]
        public void OutOfRange_Clamped()
        {
            AssertPos(new Vector2(-R, 0f), HudClockMath.DialPosition(-1f, R));
            AssertPos(new Vector2(R, 0f), HudClockMath.DialPosition(2f, R));
        }
    }
}
