using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Pure damage formulas (spec-stat-scaling-balance, reference table in <c>_bmad-output/balance.md</c>).
    /// Covered by <c>DamageMathTests</c>.
    /// </summary>
    public static class DamageMath
    {
        /// <summary>
        /// Raw hit damage: weapon damage + the weapon's scaling stat, 1:1 (Gothic-style). A negative stat or
        /// weapon value never makes the total negative (a negative amount must not heal through TakeDamage).
        /// </summary>
        public static float WeaponRaw(float weaponDamage, int scalingStatValue) =>
            Mathf.Max(0f, weaponDamage + Mathf.Max(0, scalingStatValue));

        /// <summary>
        /// Damage taken after armor: <c>max(raw − armor, raw × minFraction)</c>, never below 1 for a positive hit.
        /// The fraction floor keeps late-game trash able to scratch the player and keeps bosses killable.
        /// </summary>
        public static float Mitigate(float raw, float armor, float minFraction)
        {
            if (raw <= 0f) return 0f;
            float floor = raw * Mathf.Clamp01(minFraction);
            return Mathf.Max(1f, Mathf.Max(raw - Mathf.Max(0f, armor), floor));
        }
    }
}
