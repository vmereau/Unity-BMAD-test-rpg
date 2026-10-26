using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure DayNightMath used by DayNightController (sunrise 6, sunset 22, 1 h twilight).
    /// </summary>
    public class DayNightMathTests
    {
        private const float SUNRISE = 6f;
        private const float SUNSET = 22f;
        private const float TWILIGHT = 1f;
        private const float EPS = 0.0001f;

        [Test]
        public void HourOfDay_WrapsAcrossDays()
        {
            double t = 3 * GameTime.SECONDS_PER_DAY + 14.5 * GameTime.SECONDS_PER_HOUR;
            Assert.AreEqual(14.5f, DayNightMath.HourOfDay(t), EPS);
            Assert.AreEqual(0f, DayNightMath.HourOfDay(0d), EPS);
            Assert.AreEqual(0f, DayNightMath.HourOfDay(-5d), EPS);
        }

        [Test]
        public void Noon_FullDaylight_SunAtTop()
        {
            Assert.AreEqual(1f, DayNightMath.Daylight(14f, SUNRISE, SUNSET, TWILIGHT), EPS);
            float progress = DayNightMath.DayProgress(14f, SUNRISE, SUNSET);
            Assert.AreEqual(0.5f, progress, EPS);
            Assert.AreEqual(60f, DayNightMath.ArcElevation(progress, 60f), EPS);
            Assert.AreEqual(180f, DayNightMath.ArcAzimuth(progress, 90f), EPS);
        }

        [Test]
        public void Sunrise_RampsUpOverTwilight()
        {
            Assert.AreEqual(0f, DayNightMath.Daylight(6f, SUNRISE, SUNSET, TWILIGHT), EPS);
            Assert.AreEqual(0.5f, DayNightMath.Daylight(6.5f, SUNRISE, SUNSET, TWILIGHT), EPS);
            Assert.AreEqual(1f, DayNightMath.Daylight(7f, SUNRISE, SUNSET, TWILIGHT), EPS);
            Assert.AreEqual(0f, DayNightMath.ArcElevation(DayNightMath.DayProgress(6f, SUNRISE, SUNSET), 60f), EPS);
        }

        [Test]
        public void Sunset_RampsDownOverTwilight()
        {
            Assert.AreEqual(1f, DayNightMath.Daylight(21f, SUNRISE, SUNSET, TWILIGHT), EPS);
            Assert.AreEqual(0.5f, DayNightMath.Daylight(21.5f, SUNRISE, SUNSET, TWILIGHT), EPS);
            Assert.AreEqual(0f, DayNightMath.Daylight(22f, SUNRISE, SUNSET, TWILIGHT), EPS);
        }

        [TestCase(23f)]
        [TestCase(0f)]
        [TestCase(5.99f)]
        public void Night_NoDaylight(float hour)
        {
            Assert.IsFalse(DayNightMath.IsDay(hour, SUNRISE, SUNSET));
            Assert.AreEqual(0f, DayNightMath.Daylight(hour, SUNRISE, SUNSET, TWILIGHT), EPS);
        }

        [Test]
        public void NightProgress_WrapsPastMidnight()
        {
            // 8 h night: 22 → 0, 02:00 is the middle, 06:00 the end.
            Assert.AreEqual(0f, DayNightMath.NightProgress(22f, SUNRISE, SUNSET), EPS);
            Assert.AreEqual(0.25f, DayNightMath.NightProgress(0f, SUNRISE, SUNSET), EPS);
            Assert.AreEqual(0.5f, DayNightMath.NightProgress(2f, SUNRISE, SUNSET), EPS);
        }

        [Test]
        public void NightProgress_DuringDay_MoonRestsWhereItSetThenWhereItRises()
        {
            Assert.AreEqual(1f, DayNightMath.NightProgress(6.5f, SUNRISE, SUNSET), EPS);
            Assert.AreEqual(0f, DayNightMath.NightProgress(21.5f, SUNRISE, SUNSET), EPS);
        }

        [Test]
        public void NoTwilight_HardSwitch()
        {
            Assert.AreEqual(1f, DayNightMath.Daylight(6f, SUNRISE, SUNSET, 0f), EPS);
            Assert.AreEqual(0f, DayNightMath.Daylight(22f, SUNRISE, SUNSET, 0f), EPS);
        }

        [Test]
        public void LongTwilight_CappedAtHalfDay()
        {
            // 16 h day, 20 h twilight → ramp capped at 8 h: full light only at mid-day.
            Assert.AreEqual(0.5f, DayNightMath.Daylight(10f, SUNRISE, SUNSET, 20f), EPS);
            Assert.AreEqual(1f, DayNightMath.Daylight(14f, SUNRISE, SUNSET, 20f), EPS);
        }

        [Test]
        public void DayProgress_ClampedOutsideTheDay_AndAzimuthSetsInTheWest()
        {
            Assert.AreEqual(0f, DayNightMath.DayProgress(3f, SUNRISE, SUNSET), EPS);
            Assert.AreEqual(1f, DayNightMath.DayProgress(23f, SUNRISE, SUNSET), EPS);
            Assert.AreEqual(270f, DayNightMath.ArcAzimuth(1f, 90f), EPS);
        }

        [Test]
        public void DegenerateDay_NeverProducesNaN()
        {
            Assert.AreEqual(0f, DayNightMath.DayProgress(6f, 6f, 6f), EPS);
            Assert.AreEqual(0f, DayNightMath.ArcElevation(float.NaN, 60f), EPS);
            Assert.AreEqual(0f, DayNightMath.HourOfDay(double.NaN), EPS);
        }

        [TestCase(6f, 22f, true)]
        [TestCase(22f, 6f, false)]
        [TestCase(6f, 6f, false)]
        [TestCase(-1f, 22f, false)]
        [TestCase(6f, 24f, false)]
        public void IsValidDay(float sunrise, float sunset, bool expected)
        {
            Assert.AreEqual(expected, DayNightMath.IsValidDay(sunrise, sunset));
        }
    }
}
