using System;
using Game.Core;

namespace Game.World
{
    /// <summary><b>Static, pure.</b> Bed hour picker math: target hour → wake-up world time. Tested by <c>SleepRulesTests</c>.</summary>
    public static class SleepRules
    {
        public const string MSG_HOSTILES_NEARBY = "You can't sleep with enemies nearby";
        public const string MSG_DEAD = "You can't sleep now";

        /// <summary>
        /// Absolute world time the player wakes at: the next <paramref name="targetHour"/>:00. A target equal to the
        /// current hour (even mid-hour past it) means the same hour tomorrow — never a zero-length or backward sleep.
        /// </summary>
        public static double WakeTime(int targetHour, double now) => GameTime.NextHour(now, WrapHour(targetHour));

        /// <summary>
        /// Whole hours of sleep shown on the picker label: the real gap rounded up (22:30 → 01:00 = 3 h), or 0 when the
        /// gap is under an hour (22:59 → 23:00), so the label never promises an hour it won't give.
        /// </summary>
        public static int HoursUntil(int targetHour, double now)
        {
            double gapHours = (WakeTime(targetHour, now) - now) / GameTime.SECONDS_PER_HOUR;
            return gapHours < 1d ? 0 : (int)Math.Ceiling(gapHours - 1e-9);
        }

        /// <summary>Picker label for <see cref="HoursUntil"/>: "3 h", or "under 1 h" for 0.</summary>
        public static string FormatHours(int hours) => hours <= 0 ? "under 1 h" : $"{hours} h";

        /// <summary>Steps <paramref name="hour"/> by <paramref name="delta"/>, wrapping inside 0–23.</summary>
        public static int StepHour(int hour, int delta) => WrapHour(hour + delta);

        private static int WrapHour(int hour)
        {
            int h = hour % GameTime.HOURS_PER_DAY;
            return h < 0 ? h + GameTime.HOURS_PER_DAY : h;
        }
    }
}
