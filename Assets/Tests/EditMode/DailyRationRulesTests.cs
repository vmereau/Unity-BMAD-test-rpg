using Game.Core;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary><see cref="DailyRationRules"/>: ration days start at the NPC's wake hour, not at midnight.</summary>
    public class DailyRationRulesTests
    {
        private const double HOUR = GameTime.SECONDS_PER_HOUR;
        private const double DAY = GameTime.SECONDS_PER_DAY;

        [Test] public void Period_BeforeWakeHour_IsPreviousDay() => Assert.AreEqual(0L, DailyRationRules.Period(DAY + 4 * HOUR, 5));
        [Test] public void Period_AtWakeHour_IsNewDay() => Assert.AreEqual(1L, DailyRationRules.Period(DAY + 5 * HOUR, 5));
        [Test] public void Period_Midnight_DoesNotStartADay() =>
            Assert.AreEqual(DailyRationRules.Period(DAY - HOUR, 5), DailyRationRules.Period(DAY + HOUR, 5));
        [Test] public void Period_GameStartBeforeFirstWake_IsMinusOne() => Assert.AreEqual(-1L, DailyRationRules.Period(0d, 5));
        [Test] public void Period_WakeHourClamped() => Assert.AreEqual(DailyRationRules.Period(10 * HOUR, 23), DailyRationRules.Period(10 * HOUR, 40));

        [Test] public void Remaining_NothingStored_IsFull() => Assert.AreEqual(2, DailyRationRules.Remaining(2, null, 3));
        [Test] public void Remaining_SamePeriod_SubtractsTaken() =>
            Assert.AreEqual(1, DailyRationRules.Remaining(2, new DailyRationState { period = 3, taken = 1 }, 3));
        [Test] public void Remaining_OtherPeriod_IsFull() =>
            Assert.AreEqual(2, DailyRationRules.Remaining(2, new DailyRationState { period = 2, taken = 2 }, 3));
        [Test] public void Remaining_NeverNegative() =>
            Assert.AreEqual(0, DailyRationRules.Remaining(2, new DailyRationState { period = 3, taken = 5 }, 3));

        [Test]
        public void Take_SamePeriod_Increments()
        {
            var s = DailyRationRules.Take(new DailyRationState { period = 3, taken = 1 }, 3);
            Assert.AreEqual(3L, s.period);
            Assert.AreEqual(2, s.taken);
        }

        [Test]
        public void Take_NewPeriod_RestartsAtOne()
        {
            var s = DailyRationRules.Take(new DailyRationState { period = 2, taken = 2 }, 3);
            Assert.AreEqual(3L, s.period);
            Assert.AreEqual(1, s.taken);
        }

        [Test] public void Label_AppendsCount() => Assert.AreEqual("My daily ration (2 left)", DailyRationRules.Label("My daily ration ", 2));
        [Test] public void Label_NoneLeft() => Assert.AreEqual("My daily ration (0 left)", DailyRationRules.Label("My daily ration", -1));
    }
}
