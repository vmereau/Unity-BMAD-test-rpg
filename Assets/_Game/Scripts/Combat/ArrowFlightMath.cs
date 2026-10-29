using UnityEngine;

namespace Game.Combat
{
    /// <summary>Pure arrow-flight math: straight first, then gravity eased in.</summary>
    public static class ArrowFlightMath
    {
        /// <summary>
        /// Share of the arrow's gravity applied <paramref name="elapsed"/> seconds after launch: 0 until
        /// <paramref name="delay"/>, then linear to 1 over <paramref name="rampTime"/> (0 ramp = instant).
        /// </summary>
        public static float GravityFactor(float elapsed, float delay, float rampTime)
        {
            float t = elapsed - Mathf.Max(0f, delay);
            if (t <= 0f) return 0f;
            if (rampTime <= 0f) return 1f;
            return Mathf.Clamp01(t / rampTime);
        }
    }
}
