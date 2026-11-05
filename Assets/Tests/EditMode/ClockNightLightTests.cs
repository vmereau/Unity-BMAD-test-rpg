using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class ClockNightLightTests
    {
        private const float SUNRISE = 6f;
        private const float SUNSET = 22f;
        private const float TWILIGHT = 1f;

        [TestCase(12f, 0f)]    // full day
        [TestCase(7f, 0f)]     // end of the morning ramp
        [TestCase(21f, 0f)]    // start of the evening ramp
        [TestCase(6.5f, 0.5f)] // morning ramp
        [TestCase(21.5f, 0.5f)] // evening ramp
        [TestCase(22f, 1f)]    // sunset
        [TestCase(23.5f, 1f)]  // before midnight
        [TestCase(0f, 1f)]     // midnight wrap
        [TestCase(3f, 1f)]     // after midnight
        [TestCase(5.99f, 1f)]  // just before sunrise
        public void NightLevel_FollowsTheDay(float hour, float expected)
        {
            Assert.That(ClockNightLight.NightLevel(hour, SUNRISE, SUNSET, TWILIGHT), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void NightLevel_NoTwilight_IsAHardSwitch()
        {
            Assert.That(ClockNightLight.NightLevel(6f, SUNRISE, SUNSET, 0f), Is.EqualTo(0f));
            Assert.That(ClockNightLight.NightLevel(22f, SUNRISE, SUNSET, 0f), Is.EqualTo(1f));
        }
    }
}
