namespace Game.Core
{
    /// <summary>
    /// <b>Static, pure.</b> World-time math. World time is one absolute count of game seconds since day 1 00:00;
    /// day / hour / minute are always derived from it, never stored. Tested by <c>GameTimeTests</c>.
    /// </summary>
    public static class GameTime
    {
        public const double SECONDS_PER_MINUTE = 60d;
        public const double SECONDS_PER_HOUR = 3600d;
        public const double SECONDS_PER_DAY = 86400d;
        public const int HOURS_PER_DAY = 24;

        /// <summary>1-based day number.</summary>
        public static int DayOf(double worldSeconds) => (int)(Clamp(worldSeconds) / SECONDS_PER_DAY) + 1;

        /// <summary>Hour of the day, 0–23.</summary>
        public static int HourOf(double worldSeconds) => (int)(Clamp(worldSeconds) % SECONDS_PER_DAY / SECONDS_PER_HOUR);

        /// <summary>Minute of the hour, 0–59.</summary>
        public static int MinuteOf(double worldSeconds) => (int)(Clamp(worldSeconds) % SECONDS_PER_HOUR / SECONDS_PER_MINUTE);

        /// <summary>Absolute index of the hour (0 = day 1 00:00) — compared to detect crossed hours.</summary>
        public static long AbsoluteHourOf(double worldSeconds) => (long)(Clamp(worldSeconds) / SECONDS_PER_HOUR);

        /// <summary>
        /// Hours whose start was crossed going from <paramref name="previous"/> to <paramref name="now"/>, capped at the
        /// last day's worth: raise one hour event for each absolute hour in [<paramref name="firstHour"/>,
        /// firstHour + count). Count 0 when no hour boundary was crossed (or time went backwards).
        /// </summary>
        public static int CrossedHours(double previous, double now, out long firstHour)
        {
            long from = AbsoluteHourOf(previous);
            long to = AbsoluteHourOf(now);
            long crossed = to - from;
            if (crossed <= 0) { firstHour = to; return 0; }
            int count = crossed < HOURS_PER_DAY ? (int)crossed : HOURS_PER_DAY;
            firstHour = to - count + 1;
            return count;
        }

        /// <summary>Hour of the day (0–23) of an absolute hour index.</summary>
        public static int HourOfAbsolute(long absoluteHour) => (int)(absoluteHour % HOURS_PER_DAY);

        /// <summary>World time of day 1 at <paramref name="startHour"/> (new game).</summary>
        public static double StartTime(int startHour) => Mod(startHour, HOURS_PER_DAY) * SECONDS_PER_HOUR;

        /// <summary>
        /// Next world time strictly after <paramref name="now"/> whose hour is <paramref name="targetHour"/> (minute 0).
        /// A target equal to now is a full day later. Exact (no accumulated float error), so the result reads HH:00.
        /// </summary>
        public static double NextHour(double now, int targetHour)
        {
            now = Clamp(now);
            double dayStart = System.Math.Floor(now / SECONDS_PER_DAY) * SECONDS_PER_DAY;
            double target = dayStart + Mod(targetHour, HOURS_PER_DAY) * SECONDS_PER_HOUR;
            return target <= now ? target + SECONDS_PER_DAY : target;
        }

        /// <summary>"Day 4, 06:00".</summary>
        public static string Format(double worldSeconds) =>
            $"Day {DayOf(worldSeconds)}, {HourOf(worldSeconds):00}:{MinuteOf(worldSeconds):00}";

        /// <summary>Game seconds that pass per real second for a day lasting <paramref name="realMinutesPerGameDay"/>.</summary>
        public static double Rate(float realMinutesPerGameDay) =>
            realMinutesPerGameDay > 0f ? SECONDS_PER_DAY / (realMinutesPerGameDay * SECONDS_PER_MINUTE) : 0d;

        private static double Clamp(double worldSeconds) => worldSeconds > 0d ? worldSeconds : 0d;

        private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
    }
}
