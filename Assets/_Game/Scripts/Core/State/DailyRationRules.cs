using System;

namespace Game.Core
{
    /// <summary>Saved state of one daily ration: the ration day it was last taken in and how many were taken then.</summary>
    public struct DailyRationState
    {
        public long period;
        public int taken;
    }

    /// <summary>
    /// Pure rules of the daily ration topic (<c>Game.Dialogue.DailyRationDialogueNode</c>): a ration "day" starts at the
    /// NPC's wake hour (end of its routine Sleep block), not at midnight. Tested by <c>DailyRationRulesTests</c>.
    /// </summary>
    public static class DailyRationRules
    {
        /// <summary>
        /// Index of the ration day containing <paramref name="now"/> (game seconds): days start at
        /// <paramref name="wakeHour"/> o'clock. Before the first wake of the game → -1.
        /// </summary>
        public static long Period(double now, int wakeHour)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) now = 0d;
            int hour = Math.Max(0, Math.Min(23, wakeHour));
            return (long)Math.Floor((now - hour * GameTime.SECONDS_PER_HOUR) / GameTime.SECONDS_PER_DAY);
        }

        /// <summary>Rations left: <paramref name="perDay"/> minus what was taken in the current period (none recorded = full).</summary>
        public static int Remaining(int perDay, DailyRationState? stored, long currentPeriod)
        {
            int max = Math.Max(0, perDay);
            if (!stored.HasValue || stored.Value.period != currentPeriod) return max;
            return Math.Max(0, max - Math.Max(0, stored.Value.taken));
        }

        /// <summary>State after taking one ration in <paramref name="currentPeriod"/> (a new period starts the count over).</summary>
        public static DailyRationState Take(DailyRationState? stored, long currentPeriod)
        {
            int taken = stored.HasValue && stored.Value.period == currentPeriod ? Math.Max(0, stored.Value.taken) : 0;
            return new DailyRationState { period = currentPeriod, taken = taken + 1 };
        }

        /// <summary>Topic label: "<paramref name="text"/> (N left)".</summary>
        public static string Label(string text, int remaining) =>
            $"{(text ?? string.Empty).TrimEnd()} ({Math.Max(0, remaining)} left)";
    }
}
