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
        [Header("XP — Story 3.1")]
        [Tooltip("Flat XP awarded per enemy kill.")]
        public int xpPerKill = 50;

        [Header("Level Thresholds — Story 3.2")]
        [Tooltip("XP required to reach level index+1. Level 1=100, Level 2=250, etc.")]
        public int[] xpPerLevel = { 100, 250, 500, 900, 1400 };

        [Header("Learning Points — Story 3.3")]
        [Tooltip("Learning points awarded each time the player levels up.")]
        public int learningPointsPerLevel = 3;

        [Header("Base Stats — Story 3.4")]
        [Tooltip("Starting value for each base stat.")]
        public int baseStrength = 5;
        public int baseDexterity = 5;
        public int baseEndurance = 5;
        public int baseMana = 5;
    }
}
