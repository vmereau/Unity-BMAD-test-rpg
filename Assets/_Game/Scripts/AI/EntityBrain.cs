using _Game.ScriptableObjects.Entities;
using Game.Combat;
using Game.Core;
using Game.Player;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Generic entity state machine: Idle → Patrolling → (Engaging → Attacking) → Dead.
    /// Idle: delegates to Entity.ExecuteIdle() (wander, stand still, etc.); optionally detects player → Engaging.
    /// Patrolling: cycles between waypoints; optionally detects player → Engaging.
    /// Engaging: chases player via NavMesh within DetectionRange; disengages beyond DisengageRange.
    /// Attacking: stops moving, strikes player on cooldown within AttackRange.
    /// Dead: no-op state (EntityHealth handles death animation via EntityAnimator).
    /// Engaging and Attacking are only active when _canEngagePlayer = true (hostile entities).
    /// Patrol without waypoints falls back to Idle wander at spawn origin.
    /// Story 2.8: Initial implementation as EnemyBrain (patrol + engage only).
    /// Story 2.9: Added Attacking and Dead states.
    /// Enemy Creature System: Migrated to EnemyTypeSO; added EntityAnimator calls.
    /// Renamed EntityBrain: generic for any entity type; _canEngagePlayer toggles combat behavior;
    ///   idle wander logic moved into monsterTypeSO.ExecuteIdle(); Entity base is stand-still.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EntityBrain : MonoBehaviour
    {
        private const string TAG = "[AI]";

        private enum EntityState { Idle, Patrolling, Engaging, Attacking, Dead }

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private EntityAnimator _entityAnimator;

        [Header("Behavior")]
        [Tooltip("True for hostile entities (enemies). Enables player detection, chase, and attack states.")]
        [SerializeField] private bool _canEngagePlayer = true;

        private NavMeshAgent _agent;
        private Transform _player;
        private EntityHealth _entityHealth;
        private PlayerCombat _playerCombat;
        private PlayerHealth _playerHealth;

        private EntityState _state = EntityState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;
        private float _attackCooldownTimer;
        private float _smoothedAnimSpeed;

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
                case EntityState.Engaging:   HandleEngage();   break;
                case EntityState.Attacking:  HandleAttack();   break;
                case EntityState.Dead:       HandleDead();     break;
            }

            HandleCooldowns();
            HandleMovementAnimation();
        }

        private void HandleMovementAnimation()
        {
            float target = _agent.velocity.magnitude;
            _smoothedAnimSpeed = Mathf.Lerp(_smoothedAnimSpeed, target, Time.deltaTime * 10f);
            _entityAnimator?.SetMoveSpeed(_smoothedAnimSpeed);
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
                TransitionToEngaging();
                return;
            }

            if (_agent.pathPending) return;

            _persistentID.Entity.ExecuteIdle(_agent, _idleOrigin, ref _waitTimer);
        }

        private void HandlePatrol()
        {
            if (_canEngagePlayer && IsPlayerInDetectionRange())
            {
                TransitionToEngaging();
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
            // No-op: death animation and ragdoll handled by EntityAnimator.
        }

        // --- Combat ---

        private void ExecuteAttack()
        {
            _entityAnimator?.TriggerAttack();
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

        private void TransitionToEngaging()
        {
            // Only capture return state from non-combat states; Attacking→Engaging preserves the original origin state
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
