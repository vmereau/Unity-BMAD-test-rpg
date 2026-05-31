using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Factions;
using Game.World;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Game.AI
{
    /// <summary>
    /// Generic entity state machine: Idle → Patrolling → (Engaging → Attacking) → Dead.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EntityBrain : MonoBehaviour, ICombatStateProvider
    {
        private const string TAG = "[AI]";

        private enum EntityState { Idle, Patrolling, Warning, Engaging, Attacking, Dead }

        public bool IsInCombat { get; private set; }

        // Single writer for combat state. Keeps the readable flag and the animator in lockstep,
        // replacing the previously-scattered _animationDriver?.SetInCombat(...) calls. Idempotent.
        private void SetCombatState(bool inCombat)
        {
            if (IsInCombat == inCombat) return;
            IsInCombat = inCombat;
            _animationDriver?.SetInCombat(inCombat);
        }

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Transform[] _waypoints;
        [FormerlySerializedAs("_animationBridge")]
        [FormerlySerializedAs("_entityAnimator")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        [Header("Behavior")]
        [Tooltip("Skip the warning telegraph and engage the instant a target is detected.")]
        [SerializeField] private bool _engageImmediately = false;

        [Tooltip("This entity's faction membership — drives detection & engagement decisions.")]
        [SerializeField] private FactionMember _selfFactionMember;
        [Tooltip("Seconds between target-acquisition registry scans while Idle/Patrolling. Bounds per-frame scan cost.")]
        [SerializeField] private float _targetScanInterval = 0.25f;

        private NavMeshAgent _agent;
        private EntityHealth _entityHealth;
        private FactionMember _currentTarget;
        private float _targetScanTimer;

        private EntityState _state = EntityState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;
        private float _attackCooldownTimer;
        private float _warningTimer;

        private Vector3 _idleOrigin;
        private EntityState _disengageState = EntityState.Patrolling;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                GameLog.Error(TAG, "NavMeshAgent not found — EntityBrain disabled");
                enabled = false;
                return;
            }

            if (_persistentID.Entity == null)
            {
                GameLog.Error(TAG, $"Entity SO not assigned on {gameObject.name} — EntityBrain disabled");
                enabled = false;
                return;
            }

            _entityHealth = GetComponent<EntityHealth>();
            if (_entityHealth == null)
            {
                GameLog.Error(TAG, "EntityHealth not found on same GameObject — EntityBrain disabled");
                enabled = false;
                return;
            }

            if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();
            if (_selfFactionMember == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: FactionMember not found on same GameObject — EntityBrain disabled");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            // FactionMember.Awake has run by now (all Awakes precede any Start). If it failed to resolve a
            // faction it disabled itself but GetComponent still found it, leaving the brain silently passive —
            // surface that here instead of running forever with no possible target.
            if (_selfFactionMember.Faction == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: FactionMember resolved no faction — brain can never acquire a target. Disabling.");
                enabled = false;
                return;
            }

            // Runtime guard: OnValidate only clamps in-editor. An un-migrated SO with
            // WarningRange >= DetectionRange has an empty warning band, so the entity will
            // always instant-engage and the telegraph silently never fires. Warn once.
            if (!_engageImmediately &&
                _persistentID.Entity.WarningRange >= _persistentID.Entity.DetectionRange)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: WarningRange ({_persistentID.Entity.WarningRange}) >= DetectionRange ({_persistentID.Entity.DetectionRange}) — warning band empty; entity will instant-engage. Check the Entity SO.");
            }

            if (_waypoints == null || _waypoints.Length == 0)
            {
                GameLog.Info(TAG, $"{gameObject.name}: No waypoints assigned — entering Idle wander");
                TransitionToIdle(transform.position);
                return;
            }
            // Initialize to last index so the first AdvanceToNextWaypoint() lands at index 0.
            _currentWaypoint = _waypoints.Length - 1;
            AdvanceToNextWaypoint();
            _state = EntityState.Patrolling;
        }

        private void Update()
        {
            if (_entityHealth.IsDead && _state != EntityState.Dead)
            {
                TransitionToDead();
                return;
            }

            switch (_state)
            {
                case EntityState.Idle:       HandleIdle();     break;
                case EntityState.Patrolling: HandlePatrol();   break;
                case EntityState.Warning:    HandleWarning();  break;
                case EntityState.Engaging:   HandleEngage();   break;
                case EntityState.Attacking:  HandleAttack();   break;
                case EntityState.Dead:       HandleDead();     break;
            }

            HandleCooldowns();
            if (_animationDriver != null) _animationDriver.DriveLocomotion(_agent);
        }

        private void HandleCooldowns()
        {
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer = Mathf.Max(0f, _attackCooldownTimer - Time.deltaTime);
        }

        // --- Shared detection helpers ---

        // Throttled wrapper for the Idle/Patrol acquisition path so a crowd of idle entities does not
        // each run a full registry scan every frame. Bounded to one scan per _targetScanInterval.
        private bool TryAcquireTargetThrottled()
        {
            _targetScanTimer -= Time.deltaTime;
            if (_targetScanTimer > 0f) return false;
            _targetScanTimer = _targetScanInterval;
            return TryAcquireTarget();
        }

        // Queries the registry for the closest hostile within detection range and caches it.
        private bool TryAcquireTarget()
        {
            if (_selfFactionMember.Faction == null) return false;
            _currentTarget = TargetRegistry.FindClosestHostile(
                _selfFactionMember.Faction,
                transform.position,
                _persistentID.Entity.DetectionRange);
            return _currentTarget != null;
        }

        private bool HasValidTarget() =>
            _currentTarget != null && _currentTarget.Damageable != null && !_currentTarget.Damageable.IsDead;

        // --- State handlers ---

        private void HandleIdle()
        {
            if (TryAcquireTargetThrottled())
            {
                RespondToDetectedTarget();
                return;
            }

            if (_agent.pathPending) return;

            _persistentID.Entity.ExecuteIdle(_agent, _idleOrigin, ref _waitTimer);
        }

        private void HandlePatrol()
        {
            if (TryAcquireTargetThrottled())
            {
                RespondToDetectedTarget();
                return;
            }

            if (_agent.pathPending) return;

            if (_agent.remainingDistance <= _persistentID.Entity.WaypointArrivalThreshold)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                    AdvanceToNextWaypoint();
                else
                    _agent.isStopped = true;
            }
        }

        // Decide how to react to first contact: instant engage, cross-inner-ring engage, or warn.
        private void RespondToDetectedTarget()
        {
            if (_engageImmediately) { TransitionToEngaging(); return; }
            float dist = Vector3.Distance(transform.position, _currentTarget.Transform.position);
            if (dist <= _persistentID.Entity.WarningRange) TransitionToEngaging();
            else TransitionToWarning();
        }

        private void HandleWarning()
        {
            if (!HasValidTarget()) { CancelWarning(); return; }
            float dist = Vector3.Distance(transform.position, _currentTarget.Transform.position);
            if (dist > _persistentID.Entity.DetectionRange) { CancelWarning(); return; }      // target escaped
            if (dist <= _persistentID.Entity.WarningRange) { TransitionToEngaging(); return; } // crossed inner ring
            FaceTarget();
            _warningTimer -= Time.deltaTime;
            if (_warningTimer <= 0f) TransitionToEngaging();
        }

        // Y-only rotation to face the target. Manual because the agent is stopped while warning,
        // so NavMeshAgent auto-rotation does not apply.
        private void FaceTarget()
        {
            Vector3 dir = _currentTarget.Transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            float maxDeg = _persistentID.Entity.WarningTurnSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDeg);
        }

        private void HandleEngage()
        {
            if (!HasValidTarget())
            {
                GameLog.Warn(TAG, "Target lost — disengaging");
                DisengageFromCombat();
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.Transform.position);

            if (distToTarget > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToTarget <= _persistentID.Entity.AttackRange)
            {
                TransitionToAttacking();
                return;
            }

            _agent.SetDestination(_currentTarget.Transform.position);
        }

        private void HandleAttack()
        {
            if (!HasValidTarget())
            {
                DisengageFromCombat();
                return;
            }

            float distToTarget = Vector3.Distance(transform.position, _currentTarget.Transform.position);

            if (distToTarget > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToTarget > _persistentID.Entity.AttackRange)
            {
                TransitionToEngaging();
                return;
            }

            if (_attackCooldownTimer > 0f) return;

            ExecuteAttack();
        }

        private void HandleDead()
        {
            // No-op: death animation and ragdoll handled by the AIAnimationDriver.
        }

        // --- Combat ---

        private void ExecuteAttack()
        {
            _animationDriver?.TriggerAttack();
            _attackCooldownTimer = _persistentID.Entity.AttackCooldown;
            GameLog.Info(TAG, $"{gameObject.name} attacks {_currentTarget.Transform.name}");

            IDamageable target = _currentTarget.Damageable;
            if (target == null || target.IsDead) return;

            HitResult result = target.TryReceiveHit(gameObject);
            switch (result)
            {
                case HitResult.PerfectBlock:
                    GameLog.Info(TAG, $"{gameObject.name} attack staggered by perfect block");
                    break;
                case HitResult.Blocked:
                    GameLog.Info(TAG, $"{gameObject.name} attack blocked — no damage");
                    break;
                case HitResult.Dodged:
                    GameLog.Info(TAG, $"{gameObject.name} attack dodged — no damage");
                    break;
                case HitResult.NotBlocked:
                    target.TakeDamage(_persistentID.Entity.AttackDamage);
                    break;
            }
        }

        // --- Movement helpers ---

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.BaseSpeed;
            _agent.SetDestination(_waypoints[_currentWaypoint].position);
            _waitTimer = _persistentID.Entity.PatrolWaitTime;
        }

        // --- State transitions ---

        private void TransitionToIdle(Vector3 origin)
        {
            _state = EntityState.Idle;
            _idleOrigin = origin;
            _waitTimer = 0f; // pick a wander target immediately on first HandleIdle tick
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _persistentID.Entity.BaseSpeed;
            SetCombatState(false);
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Idle at {origin}");
        }

        private void TransitionToWarning()
        {
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Warning;
            _agent.isStopped = true;
            _warningTimer = _persistentID.Entity.WarningEngageTime;
            _animationDriver?.SetWarning(true);
            SetCombatState(true);
            GameLog.Info(TAG, $"{gameObject.name} detected target — warning");
        }

        private void CancelWarning()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(false);
            GameLog.Info(TAG, $"{gameObject.name} lost target during warning — standing down");
            if (_disengageState == EntityState.Idle) TransitionToIdle(_idleOrigin);
            else TransitionToPatrol();
        }

        private void TransitionToEngaging()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(true);
            // Only capture return state from non-combat states. Warning→Engaging and Attacking→Engaging
            // both preserve the disengage state already captured on the original Idle/Patrol entry.
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Engaging;
            _agent.isStopped = false;
            _agent.stoppingDistance = _persistentID.Entity.EngageStoppingDistance;
            _agent.speed = _persistentID.Entity.EngageSpeed;
            _agent.SetDestination(_currentTarget.Transform.position);
            GameLog.Info(TAG, $"{gameObject.name} engaged {_currentTarget.Transform.name}");
        }

        private void TransitionToAttacking()
        {
            _state = EntityState.Attacking;
            _agent.isStopped = true;
            SetCombatState(true);
            GameLog.Info(TAG, $"{gameObject.name} entering attack range — switching to Attacking");
        }

        private void TransitionToPatrol()
        {
            _state = EntityState.Patrolling;
            SetCombatState(false);
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

        private void TransitionToDead()
        {
            _animationDriver?.SetWarning(false);
            SetCombatState(false);
            _state = EntityState.Dead;
            _agent.isStopped = true;
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Dead state");
        }

        private void DisengageFromCombat()
        {
            _currentTarget = null;
            SetCombatState(false);
            if (_disengageState == EntityState.Idle)
            {
                GameLog.Info(TAG, $"{gameObject.name} disengaged — resuming Idle at origin");
                TransitionToIdle(_idleOrigin);
            }
            else
            {
                GameLog.Info(TAG, $"{gameObject.name} disengaged — returning to patrol");
                TransitionToPatrol();
            }
        }
    }
}
