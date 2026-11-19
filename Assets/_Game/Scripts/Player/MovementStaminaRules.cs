using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Pure rules for movement stamina costs and fall damage (tested in MovementStaminaRulesTests).
    /// </summary>
    public static class MovementStaminaRules
    {
        /// <summary>Damage for a fall of <paramref name="height"/> metres: 0 up to the safe height, then per metre above.</summary>
        public static float FallDamage(float height, float safeHeight, float damagePerMetre) =>
            height <= safeHeight ? 0f : (height - safeHeight) * Mathf.Max(0f, damagePerMetre);

        /// <summary>
        /// One frame of a per-second drain paid in whole points (StaminaSystem.Consume logs and raises the HUD event per call).
        /// Adds <paramref name="perSecond"/> × <paramref name="deltaTime"/> to <paramref name="debt"/>; returns the whole
        /// points to consume now (0 while the debt is under 1). <paramref name="exhausted"/> is true when those points exceed
        /// <paramref name="available"/>: nothing is consumed and the debt resets.
        /// </summary>
        public static float DrainStep(ref float debt, float perSecond, float deltaTime, float available, out bool exhausted)
        {
            exhausted = false;
            debt += Mathf.Max(0f, perSecond) * Mathf.Max(0f, deltaTime);
            if (debt < 1f) return 0f;
            float whole = Mathf.Floor(debt);
            if (available < whole)
            {
                exhausted = true;
                debt = 0f;
                return 0f;
            }
            debt -= whole;
            return whole;
        }

        /// <summary>Base value × modifier, never negative.</summary>
        public static float Modified(float baseValue, float multiplier) => Mathf.Max(0f, baseValue * multiplier);
    }
}
