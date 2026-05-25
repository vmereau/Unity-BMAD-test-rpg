using Game.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace _Game.ScriptableObjects.Entities
{
    [CreateAssetMenu(fileName = "", menuName = "", order = 0)]
    public class Entity : ScriptableObject
    {
        private const string TAG = "[Entity]";

        public string entityName;

        [Header("Stats")]
        [SerializeField, FormerlySerializedAs("baseHealth")]   private float _baseHealth   = 50f;
        [SerializeField, FormerlySerializedAs("attackDamage")] private float _attackDamage = 10f;
        
        [Header("Detection")]
        [SerializeField, FormerlySerializedAs("detectionRange")]  private float _detectionRange  = 8f;
        [SerializeField, FormerlySerializedAs("disengageRange")]  private float _disengageRange  = 12f;

        [Tooltip("Inner radius. Player between WarningRange and DetectionRange triggers the warning telegraph. Must be below DetectionRange.")]
        [SerializeField] private float _warningRange = 5f;
        [Tooltip("Seconds the player may linger in the warning band before the entity escalates to Engaging.")]
        [SerializeField] private float _warningEngageTime = 3f;
        [Tooltip("Degrees/second the entity rotates to face the player while warning.")]
        [SerializeField] private float _warningTurnSpeed = 540f;

        [Header("Engage")]
        [SerializeField, FormerlySerializedAs("engageStoppingDistance")] private float _engageStoppingDistance = 1.5f;
        
        [Header("Movement")]
        [SerializeField, FormerlySerializedAs("patrolSpeed")] private float _baseSpeed   = 2f;
        [SerializeField, FormerlySerializedAs("engageSpeed")] private float _engageSpeed = 4f;

        [Header("Rewards")]
        [SerializeField] private int _xpOnKill = 25;

        [Header("Patrol")]
        [SerializeField, FormerlySerializedAs("waypointArrivalThreshold")] private float _waypointArrivalThreshold = 0.5f;
        [SerializeField, FormerlySerializedAs("patrolWaitTime")]           private float _patrolWaitTime           = 2f;

        [Header("Attack")]
        [SerializeField, FormerlySerializedAs("attackRange")]    private float _attackRange    = 1.8f;
        [SerializeField, FormerlySerializedAs("attackCooldown")] private float _attackCooldown = 2f;

        [Header("Animation")]
        [SerializeField, FormerlySerializedAs("animatorOverride")] private AnimatorOverrideController _animatorOverride;
        
        public float BaseHealth               => _baseHealth;
        public float AttackDamage             => _attackDamage;
        public float BaseSpeed                => _baseSpeed;
        public float EngageSpeed              => _engageSpeed;
        public int XpOnKill => _xpOnKill;
        
        public float DetectionRange           => _detectionRange;
        public float DisengageRange           => _disengageRange;
        public float WarningRange             => _warningRange;
        public float WarningEngageTime        => _warningEngageTime;
        public float WarningTurnSpeed         => _warningTurnSpeed;
        public float EngageStoppingDistance   => _engageStoppingDistance;
        public float WaypointArrivalThreshold => _waypointArrivalThreshold;
        public float PatrolWaitTime           => _patrolWaitTime;
        public float AttackRange              => _attackRange;
        public float AttackCooldown           => _attackCooldown;
        
        public AnimatorOverrideController AnimatorOverride => _animatorOverride;

        /// <summary>
        /// Called by EntityBrain each frame when idle and not pathfinding.
        /// Override in subclasses to implement entity-specific idle movement (wander, stand still, etc.).
        /// waitTimer is per-instance runtime state owned by EntityBrain, passed by ref so implementations
        /// can reset it when they pick a new destination.
        /// </summary>
        public virtual void ExecuteIdle(NavMeshAgent agent, Vector3 origin, ref float waitTimer)
        {
            // Default: stand still.
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_warningRange < 0f) _warningRange = 0f;
            if (_warningRange >= _detectionRange)
            {
                GameLog.Warn(TAG, $"'{name}': _warningRange ({_warningRange}) must be below _detectionRange ({_detectionRange}) — clamped.");
                _warningRange = Mathf.Max(0f, _detectionRange - 0.5f);
            }
        }
#endif
    }
}