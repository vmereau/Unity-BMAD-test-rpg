using UnityEngine;

namespace Game.UI
{
    /// <summary><b>Static, pure.</b> HUD clock dial layout. Tested by <c>HudClockMathTests</c>.</summary>
    public static class HudClockMath
    {
        /// <summary>
        /// Position along the upper half-circle of <paramref name="radius"/>, centred on the dial's base:
        /// progress 0 = left horizon (−r, 0), 0.5 = top (0, r), 1 = right horizon (r, 0). Progress is clamped to [0, 1].
        /// </summary>
        public static Vector2 DialPosition(float progress, float radius)
        {
            float p = Mathf.Clamp01(progress);
            float angle = Mathf.PI * (1f - p);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }
}
