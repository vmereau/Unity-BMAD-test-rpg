namespace Game.Core
{
    /// <summary>
    /// <b>Static, pure.</b> Day / night lighting math for <see cref="DayNightController"/>: where the sun and moon sit on
    /// their arcs and how much daylight there is, from the hour of the day. Hours are floats in [0, 24).
    /// The day runs from sunrise to sunset (sunrise &lt; sunset); the night wraps past midnight. Tested by <c>DayNightMathTests</c>.
    /// </summary>
    public static class DayNightMath
    {
        private const float HOURS_PER_DAY = GameTime.HOURS_PER_DAY;

        /// <summary>Hour of the day as a float in [0, 24) (14.5 = 14:30).</summary>
        public static float HourOfDay(double worldSeconds)
        {
            if (!(worldSeconds > 0d)) return 0f;
            return (float)(worldSeconds % GameTime.SECONDS_PER_DAY / GameTime.SECONDS_PER_HOUR);
        }

        /// <summary>True when the hours describe a valid day (both in [0, 24), sunrise before sunset).</summary>
        public static bool IsValidDay(float sunrise, float sunset) =>
            sunrise >= 0f && sunset < HOURS_PER_DAY && sunrise < sunset;

        public static bool IsDay(float hour, float sunrise, float sunset) => hour >= sunrise && hour < sunset;

        /// <summary>0 at sunrise → 1 at sunset; 0 or 1 (clamped) outside the day.</summary>
        public static float DayProgress(float hour, float sunrise, float sunset) =>
            Clamp01((hour - sunrise) / (sunset - sunrise));

        /// <summary>
        /// 0 at sunset → 1 at the next sunrise, wrapping past midnight. During the day the moon rests where it set (1)
        /// until mid-day, then where it will rise (0) — so it never jumps across the sky while still fading out.
        /// </summary>
        public static float NightProgress(float hour, float sunrise, float sunset)
        {
            if (IsDay(hour, sunrise, sunset)) return hour < (sunrise + sunset) * 0.5f ? 1f : 0f;
            float nightLength = HOURS_PER_DAY - (sunset - sunrise);
            float sinceSunset = hour >= sunset ? hour - sunset : hour + HOURS_PER_DAY - sunset;
            return Clamp01(sinceSunset / nightLength);
        }

        /// <summary>
        /// 0 at night → 1 in full day: ramps up over <paramref name="twilightHours"/> after sunrise and down over the same
        /// time before sunset (capped at half the day). No twilight = a hard switch.
        /// </summary>
        public static float Daylight(float hour, float sunrise, float sunset, float twilightHours)
        {
            if (!IsDay(hour, sunrise, sunset)) return 0f;
            float ramp = twilightHours < (sunset - sunrise) * 0.5f ? twilightHours : (sunset - sunrise) * 0.5f;
            if (ramp <= 0f) return 1f;
            float fromEdge = hour - sunrise < sunset - hour ? hour - sunrise : sunset - hour;
            return Clamp01(fromEdge / ramp);
        }

        /// <summary>Elevation in degrees along a sine arc: 0 at both ends, <paramref name="maxElevation"/> at mid-arc.</summary>
        public static float ArcElevation(float progress, float maxElevation) =>
            (float)System.Math.Sin(Clamp01(progress) * System.Math.PI) * maxElevation;

        /// <summary>Heading in degrees along the arc: rises at <paramref name="riseAzimuth"/>, sets 180° later.</summary>
        public static float ArcAzimuth(float progress, float riseAzimuth) => riseAzimuth + Clamp01(progress) * 180f;

        // NaN → 0 (a degenerate day must never reach a light rotation).
        private static float Clamp01(float value) => !(value > 0f) ? 0f : value > 1f ? 1f : value;
    }
}
