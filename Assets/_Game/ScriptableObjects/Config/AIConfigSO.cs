using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Tunable values for enemy AI behaviour. Assign AIConfig.asset in Inspector.
    /// All AI gameplay values live here — never hardcode in EnemyBrain or other AI scripts.
    /// Story 2.8: Initial implementation (patrol + engage). Attack values added in Story 2.9.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/AI", fileName = "AIConfig")]
    public class AIConfigSO : ScriptableObject
    {
        [Header("Movement")]
        public float patrolSpeed = 2f;
        public float engageSpeed = 4f;

        [Header("Detection")]
        public float detectionRange = 8f;
        public float disengageRange = 12f;

        [Header("Engage")]
        public float engageStoppingDistance = 1.5f;

        [Header("Patrol")]
        public float waypointArrivalThreshold = 0.5f;
        public float patrolWaitTime = 2f;

        [Header("Health")]
        public float baseHealth = 50f;

        [Header("Attack")]
        public float attackRange = 1.8f;
        public float attackCooldown = 2f;
        public float attackDamage = 10f;
        [Tooltip("Duration in seconds of the white flash visual when the enemy attacks.")]
        public float attackFlashDuration = 0.15f;
    }
}
