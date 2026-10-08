using Game.Core;
using Game.Factions;
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

        [Header("Faction")]
        [SerializeField] private FactionSO _faction;

        [Header("Stats")]
        [SerializeField, FormerlySerializedAs("baseHealth")]   private float _baseHealth   = 50f;
        [SerializeField, FormerlySerializedAs("attackDamage")] private float _attackDamage = 10f;

        [Header("Detection")]
        [Tooltip("Detection / sight range. Also the vision-cone length for EntityPerception (shortened against a sneaking player). 0 = passive entity.")]
        [SerializeField, FormerlySerializedAs("detectionRange")]  private float _detectionRange  = 8f;
        [SerializeField, FormerlySerializedAs("disengageRange")]  private float _disengageRange  = 12f;

        [Tooltip("Inner radius. Player between WarningRange and DetectionRange triggers the warning telegraph. Must be below DetectionRange.")]
        [SerializeField] private float _warningRange = 5f;
        [Tooltip("Seconds the player may linger in the warning band before the entity escalates to Engaging.")]
        [SerializeField] private float _warningEngageTime = 3f;
        [Tooltip("Degrees/second the entity rotates to face the player while warning.")]
        [SerializeField] private float _warningTurnSpeed = 540f;

        [Header("Perception")]
        [Tooltip("Full width of the vision cone, in degrees (360 = sees all around).")]
        [SerializeField] private float _viewAngle = 110f;
        [Tooltip("Height of the eyes above the entity's root, origin of the line-of-sight ray.")]
        [SerializeField] private float _eyeHeight = 1.6f;
        [Tooltip("360° radius inside which a visible target is always noticed, even from behind.")]
        [SerializeField] private float _proximityRadius = 2f;
        [Tooltip("Seconds to fill awareness from 0 to 1 at best visibility (close, on the cone axis, standing).")]
        [SerializeField] private float _awarenessFillTime = 1f;
        [Tooltip("Seconds without line of sight before an alerted entity gives up the chase and searches.")]
        [SerializeField] private float _loseSightTime = 2f;
        [Tooltip("Seconds spent looking around at the last seen position before returning to idle/patrol.")]
        [SerializeField] private float _searchDuration = 6f;

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
        [Tooltip("Minimum hits per attack combo (1 = single attack).")]
        [SerializeField, Min(1)] private int _comboHitsMin = 1;
        [Tooltip("Maximum hits per attack combo; clamped by the animation driver's MaxComboSteps.")]
        [SerializeField, Min(1)] private int _comboHitsMax = 1;

        [Header("Animation")]
        [SerializeField, FormerlySerializedAs("animatorOverride")] private AnimatorOverrideController _animatorOverride;
        
        public FactionSO Faction              => _faction;
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
        public float ViewAngle                => _viewAngle;
        public float EyeHeight                => _eyeHeight;
        public float ProximityRadius          => _proximityRadius;
        public float AwarenessFillTime        => _awarenessFillTime;
        public float LoseSightTime            => _loseSightTime;
        public float SearchDuration           => _searchDuration;
        public float EngageStoppingDistance   => _engageStoppingDistance;
        public float WaypointArrivalThreshold => _waypointArrivalThreshold;
        public float PatrolWaitTime           => _patrolWaitTime;
        public float AttackRange              => _attackRange;
        public float AttackCooldown           => _attackCooldown;
        public int ComboHitsMin               => _comboHitsMin;
        public int ComboHitsMax               => _comboHitsMax;
        
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
            if (_comboHitsMax < _comboHitsMin) _comboHitsMax = _comboHitsMin;
            if (_warningRange < 0f) _warningRange = 0f;
            // _detectionRange <= 0 means a passive entity that never detects — no warning band to validate.
            if (_detectionRange > 0f && _warningRange >= _detectionRange)
            {
                GameLog.Warn(TAG, $"'{name}': _warningRange ({_warningRange}) must be below _detectionRange ({_detectionRange}) — clamped.");
                _warningRange = Mathf.Max(0f, _detectionRange - 0.5f);
            }

            _viewAngle         = Mathf.Clamp(_viewAngle, 1f, 360f);
            _eyeHeight         = Mathf.Max(0f, _eyeHeight);
            _proximityRadius   = Mathf.Max(0f, _proximityRadius);
            _awarenessFillTime = Mathf.Max(0.05f, _awarenessFillTime);
            _loseSightTime     = Mathf.Max(0f, _loseSightTime);
            _searchDuration    = Mathf.Max(0f, _searchDuration);
            if (_detectionRange > 0f && _proximityRadius > _detectionRange)
            {
                GameLog.Warn(TAG, $"'{name}': _proximityRadius ({_proximityRadius}) must not exceed _detectionRange ({_detectionRange}) — clamped.");
                _proximityRadius = _detectionRange;
            }
        }
#endif
    }
}