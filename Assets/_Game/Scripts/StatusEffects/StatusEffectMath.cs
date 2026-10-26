using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.StatusEffects
{
    /// <summary>
    /// <b>Static, pure.</b> Payout, stacking and display math for status effects. Tested by <c>StatusEffectMathTests</c>.
    /// </summary>
    public static class StatusEffectMath
    {
        /// <summary>
        /// Amount an effect has delivered by <paramref name="now"/>: <c>total × whole ticks elapsed × tick / duration</c>,
        /// and exactly <paramref name="total"/> once <paramref name="now"/> reaches <paramref name="expiresAt"/>.
        /// </summary>
        public static float PaidBy(double appliedAt, double expiresAt, float total, double tickSeconds, double now)
        {
            if (total <= 0f) return 0f;
            double duration = expiresAt - appliedAt;
            if (duration <= 0d || now >= expiresAt) return total;
            double elapsed = now - appliedAt;
            if (elapsed <= 0d) return 0f;
            double tick = tickSeconds > 0d ? tickSeconds : duration;
            double paidTime = Math.Floor(elapsed / tick) * tick;
            double ratio = paidTime / duration;
            return ratio >= 1d ? total : (float)(total * ratio);
        }

        /// <summary><see cref="PaidBy"/> for an instance.</summary>
        public static float PaidByNow(StatusEffectInstance instance, double now) =>
            instance?.Effect == null ? 0f
                : PaidBy(instance.AppliedAt, instance.ExpiresAt, instance.TotalAmount, instance.Effect.tickIntervalSeconds, now);

        /// <summary>Amount owed now but not yet delivered (never negative — a clock moved backwards owes nothing).</summary>
        public static float DueNow(StatusEffectInstance instance, double now)
        {
            if (instance == null) return 0f;
            float due = PaidByNow(instance, now) - instance.PaidAmount;
            return due > 0f ? due : 0f;
        }

        public static bool IsExpired(StatusEffectInstance instance, double now) => instance == null || now >= instance.ExpiresAt;

        /// <summary>
        /// Adds <paramref name="added"/> to <paramref name="active"/> under its effect's stacking policy:
        /// <see cref="StatusEffectStacking.Refresh"/> removes every instance of the effect first; <see cref="StatusEffectStacking.Stack"/>
        /// drops the oldest instances (earliest applied) while the cap would be exceeded.
        /// </summary>
        public static void AddInstance(List<StatusEffectInstance> active, StatusEffectInstance added)
        {
            if (active == null || added?.Effect == null) return;
            var effect = added.Effect;
            if (effect.stacking == StatusEffectStacking.Refresh)
            {
                active.RemoveAll(i => i.Effect == effect);
            }
            else if (effect.maxStacks > 0)
            {
                while (CountOf(active, effect) >= effect.maxStacks)
                    active.Remove(OldestOf(active, effect));
            }
            active.Add(added);
        }

        /// <summary>Sum of the undelivered amounts of every <paramref name="kind"/> instance.</summary>
        public static float PendingOf(IReadOnlyList<StatusEffectInstance> active, StatusEffectKind kind)
        {
            if (active == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < active.Count; i++)
                if (active[i]?.Effect != null && active[i].Effect.kind == kind) sum += active[i].RemainingAmount;
            return sum;
        }

        public static int CountOf(IReadOnlyList<StatusEffectInstance> active, StatusEffectSO effect)
        {
            if (active == null) return 0;
            int count = 0;
            for (int i = 0; i < active.Count; i++)
                if (active[i]?.Effect == effect) count++;
            return count;
        }

        /// <summary>
        /// HUD remaining-time label from game seconds, rounded up to the minute: "20m" under an hour, else "1h05".
        /// </summary>
        public static string FormatRemaining(double seconds)
        {
            long minutes = RemainingMinutes(seconds);
            return minutes < MINUTES_PER_HOUR ? $"{minutes}m" : $"{minutes / MINUTES_PER_HOUR}h{minutes % MINUTES_PER_HOUR:00}";
        }

        /// <summary>Game seconds rounded up to whole minutes (0 when not positive) — what <see cref="FormatRemaining"/> shows.</summary>
        public static long RemainingMinutes(double seconds) =>
            seconds > 0d ? (long)Math.Ceiling(seconds / GameTime.SECONDS_PER_MINUTE) : 0;

        /// <summary>Item detail duration from game minutes, at least 1: "20 min", "1 h", "1 h 30 min".</summary>
        public static string FormatDuration(float gameMinutes)
        {
            int minutes = Math.Max(1, (int)Math.Round(gameMinutes));
            if (minutes < MINUTES_PER_HOUR) return $"{minutes} min";
            int hours = minutes / MINUTES_PER_HOUR, rest = minutes % MINUTES_PER_HOUR;
            return rest == 0 ? $"{hours} h" : $"{hours} h {rest} min";
        }

        private const int MINUTES_PER_HOUR = 60;

        private static StatusEffectInstance OldestOf(List<StatusEffectInstance> active, StatusEffectSO effect)
        {
            StatusEffectInstance oldest = null;
            foreach (var i in active)
                if (i.Effect == effect && (oldest == null || i.AppliedAt < oldest.AppliedAt)) oldest = i;
            return oldest;
        }
    }
}
