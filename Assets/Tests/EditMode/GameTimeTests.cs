using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure GameTime world-time math used by GameClock and SleepRules.
    /// </summary>
    public class GameTimeTests
    {
        private const double H = GameTime.SECONDS_PER_HOUR;
        private const double D = GameTime.SECONDS_PER_DAY;

        [Test]
        public void DayHourMinute_DerivedFromAbsoluteTime()
        {
            double t = 3 * D + 22 * H + 30 * 60; // day 4, 22:30
            Assert.AreEqual(4, GameTime.DayOf(t));
            Assert.AreEqual(22, GameTime.HourOf(t));
            Assert.AreEqual(30, GameTime.MinuteOf(t));
        }

        [Test]
        public void Zero_IsDayOneMidnight()
        {
            Assert.AreEqual(1, GameTime.DayOf(0));
            Assert.AreEqual(0, GameTime.HourOf(0));
            Assert.AreEqual(0, GameTime.MinuteOf(0));
        }

        [Test]
        public void Negative_ClampsToZero()
        {
            Assert.AreEqual(1, GameTime.DayOf(-100));
            Assert.AreEqual(0, GameTime.HourOf(-100));
        }

        [Test]
        public void StartTime_IsDayOneAtStartHour()
        {
            Assert.AreEqual(8 * H, GameTime.StartTime(8));
            Assert.AreEqual("Day 1, 08:00", GameTime.Format(GameTime.StartTime(8)));
        }

        [Test]
        public void NextHour_LaterToday()
        {
            double now = 10 * H; // day 1 10:00
            Assert.AreEqual(18 * H, GameTime.NextHour(now, 18));
        }

        [Test]
        public void NextHour_WrapsPastMidnight()
        {
            double now = 2 * D + 22 * H + 30 * 60; // day 3 22:30
            double wake = GameTime.NextHour(now, 6);
            Assert.AreEqual("Day 4, 06:00", GameTime.Format(wake));
            Assert.AreEqual(7.5 * H, wake - now, 1e-6);
        }

        [Test]
        public void NextHour_TargetEqualsNow_IsFullDayLater()
        {
            double now = 6 * H;
            Assert.AreEqual(now + D, GameTime.NextHour(now, 6));
        }

        [Test]
        public void NextHour_SlightlyPastTarget_IsNextDay()
        {
            double now = 6 * H + 0.001;
            Assert.AreEqual("Day 2, 06:00", GameTime.Format(GameTime.NextHour(now, 6)));
        }

        [Test]
        public void NextHour_ExactOnTheHour_DespiteFractionalNow()
        {
            double now = 13.123456789 * H;
            double wake = GameTime.NextHour(now, 18);
            Assert.AreEqual(18, GameTime.HourOf(wake));
            Assert.AreEqual(0, GameTime.MinuteOf(wake));
        }

        [Test]
        public void Format_PadsHoursAndMinutes()
        {
            Assert.AreEqual("Day 12, 03:07", GameTime.Format(11 * D + 3 * H + 7 * 60));
        }

        [Test]
        public void Rate_48MinuteDay_Is30GameSecondsPerRealSecond()
        {
            Assert.AreEqual(30d, GameTime.Rate(48f), 1e-9);
            Assert.AreEqual(0d, GameTime.Rate(0f));
        }

        [Test]
        public void CrossedHours_WithinHour_None()
        {
            Assert.AreEqual(0, GameTime.CrossedHours(8 * H + 10, 8 * H + 3000, out _));
        }

        [Test]
        public void CrossedHours_OneBoundary_RaisesThatHour()
        {
            int count = GameTime.CrossedHours(8 * H + 3599, 9 * H + 1, out long first);
            Assert.AreEqual(1, count);
            Assert.AreEqual(9, GameTime.HourOfAbsolute(first));
        }

        [Test]
        public void CrossedHours_HitchAcrossMidnight_EachHourInOrder()
        {
            int count = GameTime.CrossedHours(22.5 * H, D + 1.5 * H, out long first);
            Assert.AreEqual(3, count);
            Assert.AreEqual(23, GameTime.HourOfAbsolute(first));
            Assert.AreEqual(0, GameTime.HourOfAbsolute(first + 1));
            Assert.AreEqual(1, GameTime.HourOfAbsolute(first + 2));
        }

        [Test]
        public void CrossedHours_MoreThanADay_CappedToLastDayEndingAtNow()
        {
            double now = 3 * D + 5 * H;
            int count = GameTime.CrossedHours(0, now, out long first);
            Assert.AreEqual(GameTime.HOURS_PER_DAY, count);
            Assert.AreEqual(GameTime.AbsoluteHourOf(now), first + count - 1);
        }

        [Test]
        public void CrossedHours_Backwards_None()
        {
            Assert.AreEqual(0, GameTime.CrossedHours(10 * H, 2 * H, out _));
        }

        [Test]
        public void AbsoluteHour_CountsAcrossDays()
        {
            Assert.AreEqual(25, GameTime.AbsoluteHourOf(D + H + 1));
        }
    }
}
