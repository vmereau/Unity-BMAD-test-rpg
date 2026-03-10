using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Enemy state machine: Idle → Patrolling → Engaging.
    /// Patrol: cycles between waypoints, waits at each.
    /// Engage: chases player via NavMesh when within detectionRange; disengages if > disengageRange.
    /// Requires NavMeshAgent on same GameObject. AIConfigSO drives all tunable values.
    /// Story 2.8: Initial implementation (patrol + engage only; attack/health in Story 2.9).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        private const string TAG = "[AI]";

        private enum EnemyState { Idle, Patrolling, Engaging }

        [SerializeField] private AIConfigSO _config;
        [SerializeField] private Transform[] _waypoints;

        private NavMeshAgent _agent;
        private Transform _player;
        private EnemyState _state = EnemyState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;

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

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
                GameLog.Warn(TAG, "Player not found (tag 'Player') — enemy cannot engage");
            else
                _player = playerObj.transform;
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
            // Without this, AdvanceToNextWaypoint() would do (0+1)%N = 1, skipping waypoint 0.
            _currentWaypoint = _waypoints.Length - 1;
            AdvanceToNextWaypoint();
            _state = EnemyState.Patrolling;
        }

        private void Update()
        {
            switch (_state)
            {
                case EnemyState.Idle:       HandleIdle();    break;
                case EnemyState.Patrolling: HandlePatrol();  break;
                case EnemyState.Engaging:   HandleEngage();  break;
            }
        }

        private void HandleIdle()
        {
            // Nothing to do — enemy stays put. Will only move if waypoints are later assigned.
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

            _agent.SetDestination(_player.position);
        }

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            _agent.isStopped = false;
            _agent.stoppingDistance = 0f; // Walk exactly to waypoint so remainingDistance reaches threshold
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

        private void TransitionToPatrol()
        {
            _state = EnemyState.Patrolling;
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            string distStr = _player != null
                ? $"{Vector3.Distance(transform.position, _player.position):F1}m"
                : "?";
            GUI.Label(new Rect(10, 220, 500, 26),
                $"Enemy: {_state} | PlayerDist:{distStr} | DetectRange:{_config.detectionRange}m",
                style);
        }
#endif
    }
}
