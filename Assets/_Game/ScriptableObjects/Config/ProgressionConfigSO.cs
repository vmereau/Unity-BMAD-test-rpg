using UnityEngine;

namespace Game.Progression
{
    /// <summary>
    /// Balancing values for the progression system. Assign ProgressionConfig.asset in Inspector.
    /// All tunable progression values live here — never hardcode in progression scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Progression", fileName = "ProgressionConfig")]
    public class ProgressionConfigSO : ScriptableObject
    {
        [Header("Level Thresholds — Story 3.2")]
        [Tooltip("Cumulative XP to leave level index+1: 100·n(n+1)/2 for n = index+1 (100, 300, 600, …). See _bmad-output/balance.md.")]
        public int[] xpPerLevel = BuildDefaultCurve(30);

        [Header("Learning Points — Story 3.3")]
        [Tooltip("Learning points awarded each time the player levels up.")]
        public int learningPointsPerLevel = 10;

        [Header("Health — level-up")]
        [Tooltip("Permanent max health (and current health) gained each time the player levels up. 0 = none.")]
        public int healthPerLevel = 10;

        [Header("Base Stats — Story 3.4")]
        [Tooltip("Starting value for each base stat.")]
        public int baseStrength = 10;
        public int baseDexterity = 10;
        public int baseEndurance = 10;
        public int baseIntelligence = 10;

        [Header("Stat Combat Effects — Story 3.6 (weapon damage scales 1:1 with WeaponSO.scalingStat)")]
        [Tooltip("Flat stamina pool added per Endurance point above base.")]
        public float staminaPerEndurance = 5f;

        /// <summary>Cumulative thresholds 100·n(n+1)/2 for n = 1..<paramref name="levels"/>.</summary>
        public static int[] BuildDefaultCurve(int levels)
        {
            var curve = new int[levels];
            for (int n = 1; n <= levels; n++) curve[n - 1] = 100 * n * (n + 1) / 2;
            return curve;
        }
    }
}
