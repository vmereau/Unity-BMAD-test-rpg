using Game.Combat;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Enemy state machine: Idle → Patrolling → Engaging → Attacking.
    /// Patrol: cycles between waypoints, waits at each.
    /// Engage: chases player via NavMesh when within detectionRange; disengages if > disengageRange.
    /// Attack: stops moving, strikes player on cooldown when within attackRange.
    /// Dead: no-op state (entity SetActive(false) by EnemyHealth.Die() almost immediately).
    /// Requires NavMeshAgent and EnemyHealth on same GameObject. AIConfigSO drives all tunable values.
    /// Story 2.8: Initial implementation (patrol + engage only).
    /// Story 2.9: Added Attacking and Dead states; EnemyHealth, PlayerCombat, PlayerHealth integration.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        private const string TAG = "[AI]";

        private enum EnemyState { Idle, Patrolling, Engaging, Attacking, Dead }

        [SerializeField] private AIConfigSO _config;
        [SerializeField] private Transform[] _waypoints;

        private NavMeshAgent _agent;
        private Transform _player;
        private EnemyHealth _enemyHealth;
        private PlayerCombat _playerCombat;
        private PlayerHealth _playerHealth;

        private EnemyState _state = EnemyState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;
        private float _attackCooldownTimer;

        // Visual feedback
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private float _attackFlashTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                GameLog.Error(TAG, "NavMeshAgent not found — EnemyBrain disabled");
                enabled = false;
                return;
            }

            if (_config == null)
            {
                GameLog.Error(TAG, "AIConfigSO not assigned — EnemyBrain disabled");
                enabled = false;
                return;
            }

            _enemyHealth = GetComponent<EnemyHealth>();
            if (_enemyHealth == null)
            {
                GameLog.Error(TAG, "EnemyHealth not found on same GameObject — EnemyBrain disabled");
                enabled = false;
                return;
            }

            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer != null) _propBlock = new MaterialPropertyBlock();

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
            {
                GameLog.Warn(TAG, "Player not found (tag 'Player') — enemy cannot engage");
            }
            else
            {
                _player = playerObj.transform;
                _playerCombat = playerObj.GetComponent<PlayerCombat>();
                _playerHealth = playerObj.GetComponent<PlayerHealth>();
                if (_playerCombat == null)
                    GameLog.Warn(TAG, "PlayerCombat not found on Player — block/dodge checks skipped");
                if (_playerHealth == null)
                    GameLog.Warn(TAG, "PlayerHealth not found on Player — enemy attacks will deal no damage");
            }
        }

        private void Start()
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No waypoints assigned — remaining Idle");
                _state = EnemyState.Idle;
                return;
            }
            // Initialize to last index so the first AdvanceToNextWaypoint() lands at index 0.
            _currentWaypoint = _waypoints.Length - 1;
            AdvanceToNextWaypoint();
            _state = EnemyState.Patrolling;
        }

        private void Update()
        {
            // Transition to Dead state if health depleted (EnemyHealth.Die() will follow SetActive(false))
            if (_enemyHealth.IsDead && _state != EnemyState.Dead)
            {
                TransitionToDead();
                return;
            }

            switch (_state)
            {
                case EnemyState.Idle:       HandleIdle();     break;
                case EnemyState.Patrolling: HandlePatrol();   break;
                case EnemyState.Engaging:   HandleEngage();   break;
                case EnemyState.Attacking:  HandleAttack();   break;
                case EnemyState.Dead:       HandleDead();     break;
            }

            UpdateAttackVisuals();
        }

        private void HandleIdle()
        {
            // Nothing to do — enemy stays put.
        }

        private void HandlePatrol()
        {
            // Check for player detection
            if (_player != null && Vector3.Distance(transform.position, _player.position) <= _config.detectionRange)
            {
                TransitionToEngaging();
                return;
            }

            // Wait for path to compute
            if (_agent.pathPending) return;

            // Check arrival at waypoint
            if (_agent.remainingDistance <= _config.waypointArrivalThreshold)
            {
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    AdvanceToNextWaypoint();
                }
                else
                {
                    _agent.isStopped = true;
                }
            }
        }

        private void HandleEngage()
        {
            if (_player == null)
            {
                GameLog.Warn(TAG, "Player lost — returning to patrol");
                TransitionToPatrol();
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _player.position);

            if (distToPlayer > _config.disengageRange)
            {
                GameLog.Info(TAG, "Disengaged — player out of range");
                TransitionToPatrol();
                return;
            }

            // Within attack range → switch to attacking
            if (distToPlayer <= _config.attackRange)
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
                TransitionToPatrol();
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _player.position);

            // Player moved beyond disengage range
            if (distToPlayer > _config.disengageRange)
            {
                GameLog.Info(TAG, "Disengaged from attack — player out of range");
                TransitionToPatrol();
                return;
            }

            // Player moved out of attack range — resume chase
            if (distToPlayer > _config.attackRange)
            {
                TransitionToEngaging();
                return;
            }

            // Count down attack cooldown
            _attackCooldownTimer -= Time.deltaTime;
            if (_attackCooldownTimer > 0f) return;

            // Execute attack
            ExecuteAttack();
        }

        private void HandleDead()
        {
            // No-op: EnemyHealth.Die() calls SetActive(false) which stops Update.
        }

        private void ExecuteAttack()
        {
            _attackCooldownTimer = _config.attackCooldown;
            _attackFlashTimer = _config.attackFlashDuration;
            GameLog.Info(TAG, $"{gameObject.name} attacks player");

            HitResult result = HitResult.NotBlocked;
            if (_playerCombat != null)
            {
                result = _playerCombat.TryReceiveHit(gameObject);
            }

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
                        _playerHealth.TakeDamage(_config.attackDamage);
                    break;
            }
        }

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _agent.speed = _config.patrolSpeed;
            _agent.SetDestination(_waypoints[_currentWaypoint].position);
            _waitTimer = _config.patrolWaitTime;
        }

        private void TransitionToEngaging()
        {
            _state = EnemyState.Engaging;
            _agent.isStopped = false;
            _agent.stoppingDistance = _config.engageStoppingDistance;
            _agent.speed = _config.engageSpeed;
            _agent.SetDestination(_player.position);
            GameLog.Info(TAG, $"{gameObject.name} engaged player");
        }

        private void TransitionToAttacking()
        {
            _state = EnemyState.Attacking;
            _agent.isStopped = true;
            _attackCooldownTimer = 0f; // First attack fires on the very next frame
            GameLog.Info(TAG, $"{gameObject.name} entering attack range — switching to Attacking");
        }

        private void TransitionToPatrol()
        {
            _state = EnemyState.Patrolling;
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

        private void TransitionToDead()
        {
            _state = EnemyState.Dead;
            _agent.isStopped = true;
            GameLog.Info(TAG, $"{gameObject.name} transitioned to Dead state");
        }

        private void SetRendererColor(Color color)
        {
            if (_renderer == null) return;
            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", color);
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void UpdateAttackVisuals()
        {
            if (_renderer == null) return;

            if (_state == EnemyState.Dead)
            {
                SetRendererColor(Color.gray);
                return;
            }

            if (_state != EnemyState.Attacking)
            {
                SetRendererColor(Color.white);
                return;
            }

            // Brief white flash when attack fires
            if (_attackFlashTimer > 0f)
            {
                _attackFlashTimer -= Time.deltaTime;
                SetRendererColor(Color.white);
                return;
            }

            // Yellow (just attacked / cooldown high) → Red (about to attack / cooldown low)
            float t = _config.attackCooldown > 0f ? _attackCooldownTimer / _config.attackCooldown : 0f;
            SetRendererColor(Color.Lerp(Color.red, Color.yellow, t));
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private GUIStyle _guiStyle;

        private void OnGUI()
        {
            if (_config == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            string distStr = _player != null
                ? $"{Vector3.Distance(transform.position, _player.position):F1}m"
                : "?";
            GUI.Label(new Rect(10, 220, 600, 26),
                $"Enemy: {_state} | PlayerDist:{distStr} | DetectRange:{_config.detectionRange}m | AtkCD:{_attackCooldownTimer:F1}s",
                _guiStyle);
        }
#endif
    }
}
