using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Balancing values for the combat system. Assign CombatConfig.asset in Inspector.
    /// All tunable gameplay values live here — never hardcode in combat scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Combat", fileName = "CombatConfig")]
    public class CombatConfigSO : ScriptableObject
    {
        [Header("Stamina Pool")]
        public float baseStaminaPool = 100f;

        [Header("Stamina Costs")]
        public float attackStaminaCost = 20f;
        public float blockStaminaCostPerHit = 15f;
        public float dodgeStaminaCost = 25f;

        [Header("Stamina Recovery")]
        [Tooltip("Stamina units recovered per second after the regen delay elapses.")]
        public float staminaRegenRate = 20f;
        [Tooltip("Seconds after last Consume() call before regen begins.")]
        public float staminaRegenDelay = 1.5f;

        [Header("Combo Attack")]
        [Tooltip("Seconds after an attack fires before the combo window opens (~50% of clip length).")]
        public float comboWindowDelay = 0.3f;
        [Tooltip("Seconds the combo window stays open once it opens (~30% of clip length).")]
        public float comboWindowDuration = 0.18f;
    }
}
