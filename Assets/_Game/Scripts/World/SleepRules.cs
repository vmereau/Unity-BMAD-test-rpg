using Game.Core;

namespace Game.World
{
    /// <summary>Sleep durations offered by a bed.</summary>
    public enum SleepChoice { UntilMorning, UntilEvening, UntilMidnight, OneHour }

    /// <summary><b>Static, pure.</b> Sleep choice → wake-up world time. Tested by <c>SleepRulesTests</c>.</summary>
    public static class SleepRules
    {
        public const string MSG_HOSTILES_NEARBY = "You can't sleep with enemies nearby";
        public const string MSG_DEAD = "You can't sleep now";
        public const int MIDNIGHT_HOUR = 0;

        /// <summary>
        /// Absolute world time the player wakes at. Fixed-hour choices land exactly on HH:00 and a target equal to
        /// the current time means a full day; <see cref="SleepChoice.OneHour"/> is now + 1 h.
        /// </summary>
        public static double WakeTime(SleepChoice choice, double now, int morningHour, int eveningHour) => choice switch
        {
            SleepChoice.UntilMorning => GameTime.NextHour(now, morningHour),
            SleepChoice.UntilEvening => GameTime.NextHour(now, eveningHour),
            SleepChoice.UntilMidnight => GameTime.NextHour(now, MIDNIGHT_HOUR),
            _ => now + GameTime.SECONDS_PER_HOUR
        };
    }
}
