using UnityEngine;

namespace Game.Combat
{
    /// <summary>Pure ranged damage formula — covered by <c>RangedDamageTests</c>.</summary>
    public static class RangedDamage
    {
        /// <summary>
        /// base + max(0, (dexterity - baseDexterity) * damagePerDexterity) + weaponBonus.
        /// A Dexterity below base never reduces the damage (same clamp as the melee Strength bonus); the total
        /// is never negative (a negative bonus must not heal through TakeDamage).
        /// </summary>
        public static float Compute(float baseDamage, int dexterity, int baseDexterity,
            float damagePerDexterity, float weaponBonus)
        {
            return Mathf.Max(0f, baseDamage
                   + Mathf.Max(0f, (dexterity - baseDexterity) * damagePerDexterity)
                   + weaponBonus);
        }
    }
}
