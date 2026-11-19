using Game.Core;
using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure SleepRules (bed hour picker: target hour → wake-up time, hours label, stepping).
    /// </summary>
    public class SleepRulesTests
    {
        private const double H = GameTime.SECONDS_PER_HOUR;
        private const double D = GameTime.SECONDS_PER_DAY;
        private const double M = GameTime.SECONDS_PER_MINUTE;

        private static string Wake(int targetHour, double now) => GameTime.Format(SleepRules.WakeTime(targetHour, now));

        [Test]
        public void LaterHourToday_SameDay()
        {
            Assert.AreEqual("Day 1, 23:00", Wake(23, 20 * H + 15 * M));
        }

        [Test]
        public void PastMidnight_NextDay()
        {
            Assert.AreEqual("Day 2, 01:00", Wake(1, 22 * H + 30 * M));
        }

        [Test]
        public void MorningFromLateEvening_NextDay()
        {
            Assert.AreEqual("Day 4, 06:00", Wake(6, 2 * D + 22 * H + 30 * M));
        }

        [Test]
        public void TargetIsCurrentHour_FullDayLater()
        {
            Assert.AreEqual("Day 2, 06:00", Wake(6, 6 * H + 40 * M));
        }

        [Test]
        public void TargetIsExactNow_FullDayLater()
        {
            Assert.AreEqual("Day 3, 00:00", Wake(0, D));
        }

        [Test]
        public void HoursUntil_RoundsUp()
        {
            Assert.AreEqual(3, SleepRules.HoursUntil(1, 22 * H + 30 * M));
        }

        [Test]
        public void HoursUntil_ExactHour()
        {
            Assert.AreEqual(3, SleepRules.HoursUntil(23, 20 * H));
        }

        [Test]
        public void HoursUntil_SameHour_FullDay()
        {
            Assert.AreEqual(24, SleepRules.HoursUntil(6, 6 * H));
        }

        [Test]
        public void HoursUntil_SameHourMidHour_RoundsUp()
        {
            Assert.AreEqual(24, SleepRules.HoursUntil(6, 6 * H + 40 * M)); // 23 h 20 m
        }

        [Test]
        public void HoursUntil_UnderAnHour_Zero()
        {
            Assert.AreEqual(0, SleepRules.HoursUntil(23, 22 * H + 59 * M));
        }

        [Test]
        public void FormatHours_UnderAnHourAndWhole()
        {
            Assert.AreEqual("under 1 h", SleepRules.FormatHours(0));
            Assert.AreEqual("3 h", SleepRules.FormatHours(3));
        }

        [Test]
        public void StepHour_WrapsForward()
        {
            Assert.AreEqual(0, SleepRules.StepHour(23, 1));
        }

        [Test]
        public void StepHour_WrapsBackward()
        {
            Assert.AreEqual(23, SleepRules.StepHour(0, -1));
        }

        [Test]
        public void WakeTime_OutOfRangeTarget_Wraps()
        {
            Assert.AreEqual("Day 2, 01:00", Wake(25, 22 * H));
        }
    }
}
