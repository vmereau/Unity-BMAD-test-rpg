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

        [Header("Perfect Block")]
        [Tooltip("Seconds after raising block during which a hit counts as a perfect block.")]
        public float perfectBlockWindowDuration = 0.25f;

        [Header("Dodge")]
        [Tooltip("Duration in seconds of the dodge roll movement and i-frame window.")]
        public float dodgeDuration = 0.5f;
        [Tooltip("Horizontal speed in units/sec during the dodge roll.")]
        public float dodgeSpeed = 8f;

        [Header("Player Health")]
        public float baseHealth = 100f;

        [Header("Attack Damage")]
        [Tooltip("Damage dealt to enemies per player attack hit.")]
        public float attackDamage = 25f;
        [Tooltip("Radius of the sphere overlap used for player attack hit detection.")]
        public float attackHitRange = 2f;
    }
}
