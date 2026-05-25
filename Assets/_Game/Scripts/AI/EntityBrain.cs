using _Game.ScriptableObjects.Entities;
using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Player;
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
    public class EntityBrain : MonoBehaviour
    {
        private const string TAG = "[AI]";

        private enum EntityState { Idle, Patrolling, Warning, Engaging, Attacking, Dead }

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Transform[] _waypoints;
        [FormerlySerializedAs("_animationBridge")]
        [FormerlySerializedAs("_entityAnimator")]
        [SerializeField] private AIAnimationDriver _animationDriver;

        [Header("Behavior")]
        [Tooltip("True for hostile entities (enemies). Enables player detection, chase, and attack states.")]
        [SerializeField] private bool _canEngagePlayer = true;
        [Tooltip("Skip the warning telegraph and engage the instant the player is detected.")]
        [SerializeField] private bool _engageImmediately = false;

        private NavMeshAgent _agent;
        private Transform _player;
        private EntityHealth _entityHealth;
        private PlayerCombat _playerCombat;
        private PlayerHealth _playerHealth;

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
            }
        }

        private void Start()
        {
            if (_canEngagePlayer)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj == null)
                {
                    GameLog.Warn(TAG, "Player not found (tag 'Player') — entity cannot engage");
                }
                else
                {
                    _player = playerObj.transform;
                    _playerCombat = playerObj.GetComponent<PlayerCombat>();
                    _playerHealth = playerObj.GetComponent<PlayerHealth>();
                    if (_playerCombat == null)
                        GameLog.Warn(TAG, "PlayerCombat not found on Player — block/dodge checks skipped");
                    if (_playerHealth == null)
                        GameLog.Warn(TAG, "PlayerHealth not found on Player — attacks will deal no damage");
                }

                // Runtime guard: OnValidate only clamps in-editor. An un-migrated SO with
                // WarningRange >= DetectionRange has an empty warning band, so the entity will
                // always instant-engage and the telegraph silently never fires. Warn once.
                if (!_engageImmediately &&
                    _persistentID.Entity.WarningRange >= _persistentID.Entity.DetectionRange)
                {
                    GameLog.Warn(TAG, $"{gameObject.name}: WarningRange ({_persistentID.Entity.WarningRange}) >= DetectionRange ({_persistentID.Entity.DetectionRange}) — warning band empty; entity will instant-engage. Check the Entity SO.");
                }
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

        // --- Shared detection helper ---

        private bool IsPlayerInDetectionRange() =>
            _player != null && Vector3.Distance(transform.position, _player.position) <= _persistentID.Entity.DetectionRange;

        // --- State handlers ---

        private void HandleIdle()
        {
            if (_canEngagePlayer && IsPlayerInDetectionRange())
            {
                RespondToDetectedPlayer();
                return;
            }

            if (_agent.pathPending) return;

            _persistentID.Entity.ExecuteIdle(_agent, _idleOrigin, ref _waitTimer);
        }

        private void HandlePatrol()
        {
            if (_canEngagePlayer && IsPlayerInDetectionRange())
            {
                RespondToDetectedPlayer();
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
        private void RespondToDetectedPlayer()
        {
            if (_engageImmediately) { TransitionToEngaging(); return; }
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist <= _persistentID.Entity.WarningRange) TransitionToEngaging();
            else TransitionToWarning();
        }

        private void HandleWarning()
        {
            if (_player == null) { CancelWarning(); return; }
            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > _persistentID.Entity.DetectionRange) { CancelWarning(); return; }      // player escaped
            if (dist <= _persistentID.Entity.WarningRange) { TransitionToEngaging(); return; } // crossed inner ring
            FacePlayer();
            _warningTimer -= Time.deltaTime;
            if (_warningTimer <= 0f) TransitionToEngaging();
        }

        // Y-only rotation to face the player. Manual because the agent is stopped while warning,
        // so NavMeshAgent auto-rotation does not apply.
        private void FacePlayer()
        {
            Vector3 dir = _player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            float maxDeg = _persistentID.Entity.WarningTurnSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDeg);
        }

        private void HandleEngage()
        {
            if (_player == null)
            {
                GameLog.Warn(TAG, "Player lost — disengaging");
                DisengageFromCombat();
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _player.position);

            if (distToPlayer > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToPlayer <= _persistentID.Entity.AttackRange)
            {
                TransitionToAttacking();
                return;
            }

            _agent.SetDestination(_player.position);
        }

        private void HandleAttack()
        {
            if (_player == null)
            {
                DisengageFromCombat();
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _player.position);

            if (distToPlayer > _persistentID.Entity.DisengageRange)
            {
                DisengageFromCombat();
                return;
            }

            if (distToPlayer > _persistentID.Entity.AttackRange)
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
            GameLog.Info(TAG, $"{gameObject.name} attacks player");

            HitResult result = HitResult.NotBlocked;
            if (_playerCombat != null)
                result = _playerCombat.TryReceiveHit(gameObject);

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
                    if (_playerHealth != null)
                        _playerHealth.TakeDamage(_persistentID.Entity.AttackDamage);
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
            GameLog.Info(TAG, $"{gameObject.name} detected player — warning");
        }

        private void CancelWarning()
        {
            _animationDriver?.SetWarning(false);
            GameLog.Info(TAG, $"{gameObject.name} lost player during warning — standing down");
            if (_disengageState == EntityState.Idle) TransitionToIdle(_idleOrigin);
            else TransitionToPatrol();
        }

        private void TransitionToEngaging()
        {
            _animationDriver?.SetWarning(false);
            // Only capture return state from non-combat states. Warning→Engaging and Attacking→Engaging
            // both preserve the disengage state already captured on the original Idle/Patrol entry.
            if (_state == EntityState.Idle || _state == EntityState.Patrolling)
                _disengageState = _state;
            _state = EntityState.Engaging;
            _agent.isStopped = false;
            _agent.stoppingDistance = _persistentID.Entity.EngageStoppingDistance;
            _agent.speed = _persistentID.Entity.EngageSpeed;
            _agent.SetDestination(_player.position);
            GameLog.Info(TAG, $"{gameObject.name} engaged player");
        }

        private void TransitionToAttacking()
        {
            _state = EntityState.Attacking;
            _agent.isStopped = true;
            GameLog.Info(TAG, $"{gameObject.name} entering attack range — switching to Attacking");
        }

        private void TransitionToPatrol()
        {
            _state = EntityState.Patrolling;
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

        private void TransitionToDead()
        {
            _animationDriver?.SetWarning(false);
            _state = EntityState.Dead;
            _agent.isStopped = true;
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Dead state");
        }

        private void DisengageFromCombat()
        {
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
