using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Edit Mode tests for <see cref="HourWindowFactRules"/>.</summary>
    public class HourWindowFactTests
    {
        [TestCase(0, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(23, false)]
        public void PlainWindow(int hour, bool expected) =>
            Assert.AreEqual(expected, HourWindowFactRules.IsTrue(true, hour, 0, 3));

        [TestCase(21, true)]
        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(20, false)]
        public void WrapsMidnight(int hour, bool expected) =>
            Assert.AreEqual(expected, HourWindowFactRules.IsTrue(true, hour, 21, 6));

        [Test]
        public void StartEqualsEnd_AllDay() => Assert.IsTrue(HourWindowFactRules.IsTrue(true, 14, 4, 4));

        [Test]
        public void NoClock_False() => Assert.IsFalse(HourWindowFactRules.IsTrue(false, 1, 0, 3));
    }
}
