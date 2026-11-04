using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Pure swim-mode rules (tested in SwimRulesTests). Depth = water surface height above the player's feet.
    /// Keep exitDepth &lt; enterDepth &lt;= floatDepth, or the float height would immediately end the swim.
    /// </summary>
    public static class SwimRules
    {
        /// <summary>True when the water is deep enough to start swimming.</summary>
        public static bool ShouldStart(float surfaceY, float feetY, float enterDepth) => surfaceY - feetY >= enterDepth;

        /// <summary>True when rising ground has lifted the feet out of swimming depth.</summary>
        public static bool ShouldStop(float surfaceY, float feetY, float exitDepth) => surfaceY - feetY < exitDepth;

        /// <summary>Height the feet are held at while floating.</summary>
        public static float FloatFeetY(float surfaceY, float floatDepth) => surfaceY - floatDepth;

        /// <summary>Vertical speed that moves the feet to <paramref name="targetFeetY"/> without exceeding maxSpeed or overshooting.</summary>
        public static float VerticalSpeed(float feetY, float targetFeetY, float maxSpeed, float deltaTime) =>
            deltaTime <= 0f ? 0f : Mathf.Clamp((targetFeetY - feetY) / deltaTime, -maxSpeed, maxSpeed);

        /// <summary>Highest of the first <paramref name="count"/> surfaces (overlapping volumes), or false when there are none.</summary>
        public static bool TryGetHighestSurface(float[] surfaces, int count, out float surfaceY)
        {
            surfaceY = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                surfaceY = Mathf.Max(surfaceY, surfaces[i]);
            return count > 0;
        }
    }
}
