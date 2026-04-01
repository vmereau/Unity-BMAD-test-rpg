using UnityEngine;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Per-creature-type data: stats, movement, and animator override controller.
    /// Replaces AIConfigSO as the single data source for enemy behaviour.
    /// All tunable gameplay values live here — never hardcode in EnemyBrain or EnemyHealth.
    /// Enemy Creature System: Initial implementation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/Enemy Type", fileName = "EnemyType_")]
    public class EnemyTypeSO : ScriptableObject
    {
        [Header("Stats")]
        [SerializeField, FormerlySerializedAs("baseHealth")]   private float _baseHealth   = 50f;
        [SerializeField, FormerlySerializedAs("attackDamage")] private float _attackDamage = 10f;

        [Header("Movement")]
        [SerializeField, FormerlySerializedAs("patrolSpeed")] private float _patrolSpeed = 2f;
        [SerializeField, FormerlySerializedAs("engageSpeed")] private float _engageSpeed = 4f;

        [Header("Detection")]
        [SerializeField, FormerlySerializedAs("detectionRange")]  private float _detectionRange  = 8f;
        [SerializeField, FormerlySerializedAs("disengageRange")]  private float _disengageRange  = 12f;

        [Header("Engage")]
        [SerializeField, FormerlySerializedAs("engageStoppingDistance")] private float _engageStoppingDistance = 1.5f;

        [Header("Patrol")]
        [SerializeField, FormerlySerializedAs("waypointArrivalThreshold")] private float _waypointArrivalThreshold = 0.5f;
        [SerializeField, FormerlySerializedAs("patrolWaitTime")]           private float _patrolWaitTime           = 2f;

        [Header("Attack")]
        [SerializeField, FormerlySerializedAs("attackRange")]   private float _attackRange   = 1.8f;
        [SerializeField, FormerlySerializedAs("attackCooldown")] private float _attackCooldown = 2f;

        [Header("Animation")]
        [SerializeField, FormerlySerializedAs("animatorOverride")] private AnimatorOverrideController _animatorOverride;

        public float BaseHealth               => _baseHealth;
        public float AttackDamage             => _attackDamage;
        public float PatrolSpeed              => _patrolSpeed;
        public float EngageSpeed              => _engageSpeed;
        public float DetectionRange           => _detectionRange;
        public float DisengageRange           => _disengageRange;
        public float EngageStoppingDistance   => _engageStoppingDistance;
        public float WaypointArrivalThreshold => _waypointArrivalThreshold;
        public float PatrolWaitTime           => _patrolWaitTime;
        public float AttackRange              => _attackRange;
        public float AttackCooldown           => _attackCooldown;
        public AnimatorOverrideController AnimatorOverride => _animatorOverride;
    }
}
