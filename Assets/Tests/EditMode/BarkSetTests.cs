using Game.Dialogue;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="BarkSetSO.PickIndex"/> (random bark line that never repeats the last one).</summary>
    public class BarkSetTests
    {
        private static readonly float[] RANDOMS = { 0f, 0.25f, 0.49f, 0.5f, 0.75f, 0.99f, 1f };

        [Test] public void PickIndex_Empty_IsMinusOne() => Assert.That(BarkSetSO.PickIndex(0, -1, 0.5f), Is.EqualTo(-1));

        [Test]
        public void PickIndex_SingleLine_AlwaysZero()
        {
            foreach (int last in new[] { -1, 0, 3 })
                foreach (float r in RANDOMS)
                    Assert.That(BarkSetSO.PickIndex(1, last, r), Is.EqualTo(0));
        }

        [Test]
        public void PickIndex_NeverRepeatsLast()
        {
            for (int last = 0; last < 3; last++)
                foreach (float r in RANDOMS)
                {
                    int i = BarkSetSO.PickIndex(3, last, r);
                    Assert.That(i, Is.Not.EqualTo(last), $"last {last}, random {r}");
                    Assert.That(i, Is.InRange(0, 2));
                }
        }

        [Test]
        public void PickIndex_WithLast_CoversEveryOtherLine()
        {
            Assert.That(BarkSetSO.PickIndex(3, 1, 0f), Is.EqualTo(0));
            Assert.That(BarkSetSO.PickIndex(3, 1, 0.99f), Is.EqualTo(2));
        }

        [Test]
        public void PickIndex_NoValidLast_CoversFullRange()
        {
            foreach (int last in new[] { -1, 3, 99 })
            {
                Assert.That(BarkSetSO.PickIndex(3, last, 0f), Is.EqualTo(0));
                Assert.That(BarkSetSO.PickIndex(3, last, 0.5f), Is.EqualTo(1));
                Assert.That(BarkSetSO.PickIndex(3, last, 0.99f), Is.EqualTo(2));
                Assert.That(BarkSetSO.PickIndex(3, last, 1f), Is.EqualTo(2));
            }
        }
    }
}
