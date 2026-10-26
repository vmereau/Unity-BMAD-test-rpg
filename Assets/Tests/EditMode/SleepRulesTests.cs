using Game.Core;
using Game.World;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>
    /// Edit Mode tests for the pure SleepRules (bed choice → wake-up time).
    /// </summary>
    public class SleepRulesTests
    {
        private const double H = GameTime.SECONDS_PER_HOUR;
        private const double D = GameTime.SECONDS_PER_DAY;
        private const int MORNING = 6;
        private const int EVENING = 18;

        private static string Wake(SleepChoice choice, double now) =>
            GameTime.Format(SleepRules.WakeTime(choice, now, MORNING, EVENING));

        [Test]
        public void UntilMorning_FromLateEvening_NextDaySix()
        {
            Assert.AreEqual("Day 4, 06:00", Wake(SleepChoice.UntilMorning, 2 * D + 22 * H + 30 * 60));
        }

        [Test]
        public void UntilMorning_AtSix_FullDayLater()
        {
            Assert.AreEqual("Day 2, 06:00", Wake(SleepChoice.UntilMorning, 6 * H));
        }

        [Test]
        public void UntilMorning_AfterMidnight_SameDay()
        {
            Assert.AreEqual("Day 2, 06:00", Wake(SleepChoice.UntilMorning, D + 2 * H));
        }

        [Test]
        public void UntilEvening_FromMorning_SameDay()
        {
            Assert.AreEqual("Day 1, 18:00", Wake(SleepChoice.UntilEvening, 8 * H));
        }

        [Test]
        public void UntilMidnight_FromEvening_NextDayZero()
        {
            Assert.AreEqual("Day 2, 00:00", Wake(SleepChoice.UntilMidnight, 20 * H));
        }

        [Test]
        public void UntilMidnight_AtMidnight_FullDayLater()
        {
            Assert.AreEqual("Day 3, 00:00", Wake(SleepChoice.UntilMidnight, D));
        }

        [Test]
        public void OneHour_AcrossDayBoundary()
        {
            Assert.AreEqual("Day 2, 00:30", Wake(SleepChoice.OneHour, 23 * H + 30 * 60));
        }
    }
}
