using _Game.ScriptableObjects.Entities;
using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Moment-to-moment combat movement for an engaged entity (spec-ai-combat-tactics-foundation). Driven by
    /// <see cref="EntityBrain"/> while Engaging / Attacking: every <see cref="CombatStyleSO.decisionInterval"/> it picks an
    /// action with <see cref="CombatTacticsRules.Decide"/> — Approach, Circle (strafe on a ring around the target, facing
    /// it), BackOff, or Attack (close in, holding an <see cref="AttackTokenRegistry"/> token). The brain keeps its states
    /// and only swings once <see cref="WantsAttackOn"/>. Inactive without a <see cref="CombatStyleSO"/> on the Entity SO.
    /// </summary>
    public class EntityCombatTactics : MonoBehaviour
    {
        private const string TAG = "[AI]";
        // Circle destination leads the strafe by this many decision intervals, so the agent never arrives and stops
        // between two decisions.
        private const float CIRCLE_LEAD_INTERVALS = 2f;

        [SerializeField] private PersistentID _persistentID;

        private NavMeshAgent _agent;
        private EntityHealth _health;
        private FactionMember _target;
        private float _decisionTimer;
        private float _circleElapsed;
        private float _circleTime;
        private float _attackElapsed; // seconds going for the current attack (token held) without swinging
        private int _circleDirection = 1;
        private bool _warnedNoConfig;

        /// <summary>Usable: enabled, with an agent and a combat style on the Entity SO.</summary>
        public bool IsAvailable => isActiveAndEnabled && _agent != null && Style != null;

        /// <summary>Currently driving movement (between the first <see cref="Tick"/> and <see cref="End"/>).</summary>
        public bool IsActive { get; private set; }

        public CombatAction CurrentAction { get; private set; } = CombatAction.Approach;

        /// <summary>Going for an attack with a token on <paramref name="target"/> — the brain may enter Attacking in range.</summary>
        public bool WantsAttackOn(FactionMember target) =>
            IsActive && target != null && target == _target && CurrentAction == CombatAction.Attack
            && AttackTokenRegistry.Holds(this, _target);

        private CombatStyleSO Style => _persistentID != null && _persistentID.Entity != null ? _persistentID.Entity.CombatStyle : null;

        // No config → no cap (warned once).
        private int TokenCapacity
        {
            get
            {
                if (_health != null && _health.CombatConfig != null) return _health.CombatConfig.maxAttackersPerTarget;
                if (!_warnedNoConfig) GameLog.Warn(TAG, $"{gameObject.name}: no CombatConfigSO on EntityHealth — attack tokens uncapped");
                _warnedNoConfig = true;
                return int.MaxValue;
            }
        }

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<EntityHealth>();
            if (_persistentID == null) _persistentID = GetComponent<PersistentID>();
        }

        private void OnDisable() => End();

        /// <summary>
        /// One frame of tactics against <paramref name="target"/> at <paramref name="distance"/> (the brain's distance). Decides at the
        /// style's interval, moves the agent and faces the target every frame. <paramref name="attackReady"/>: cooldown
        /// elapsed, weapon in hand, no attack state playing.
        /// </summary>
        public void Tick(FactionMember target, float distance, bool attackReady)
        {
            CombatStyleSO style = Style;
            if (style == null || _agent == null || target == null) return;

            if (!IsActive || target != _target) Begin(target, style);

            // Circle time counts only while actually on the ring (a long run-in doesn't skip the circling).
            if (CurrentAction == CombatAction.Circle || CurrentAction == CombatAction.BackOff) _circleElapsed += Time.deltaTime;

            // Token held but no swing (target unreachable, kiting, held back by avoidance): give it back to the others.
            if (CurrentAction == CombatAction.Attack)
            {
                _attackElapsed += Time.deltaTime;
                if (_attackElapsed >= style.attackCommitTimeout)
                {
                    AttackTokenRegistry.Release(this);
                    StartCircling(style);
                    CurrentAction = CombatAction.Circle;
                    _decisionTimer = 0f;
                }
            }

            _decisionTimer -= Time.deltaTime;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = style.decisionInterval;
                Decide(style, distance, attackReady);
                ApplyMovement(style);
            }
            else if (CurrentAction == CombatAction.Attack || CurrentAction == CombatAction.Approach)
            {
                // Chasing a moving target: refresh the destination every frame, like the plain Engaging chase.
                ApplyMovement(style);
            }

            if (!_agent.updateRotation) FaceTarget();
        }

        /// <summary>The swing ended: release the token and circle again for a fresh circle time.</summary>
        public void NotifySwingEnded()
        {
            AttackTokenRegistry.Release(this);
            CombatStyleSO style = Style;
            if (style != null) StartCircling(style);
            CurrentAction = CombatAction.Circle;
            _decisionTimer = 0f; // re-decide on the next tick
        }

        /// <summary>Stops driving: releases the token and gives the agent its rotation back. Idempotent.</summary>
        public void End()
        {
            if (!IsActive) return; // tokens are only taken while active
            AttackTokenRegistry.Release(this);
            IsActive = false;
            _target = null;
            CurrentAction = CombatAction.Approach;
            if (_agent != null) _agent.updateRotation = true;
        }

        private void Begin(FactionMember target, CombatStyleSO style)
        {
            AttackTokenRegistry.Release(this);
            _target = target;
            IsActive = true;
            CurrentAction = CombatAction.Approach;
            _decisionTimer = 0f;
            _circleDirection = Random.value < 0.5f ? -1 : 1;
            StartCircling(style);
        }

        private void StartCircling(CombatStyleSO style)
        {
            _circleElapsed = 0f;
            _circleTime = Random.Range(style.circleTimeMin, style.circleTimeMax);
        }

        private void Decide(CombatStyleSO style, float distance, bool attackReady)
        {
            int capacity = TokenCapacity;
            var input = new CombatTacticsInput
            {
                Distance = distance,
                AttackRange = _persistentID.Entity.AttackRange,
                RingMargin = style.ringMargin,
                ApproachMargin = style.approachMargin,
                BackOffFraction = style.backOffFraction,
                Aggression = style.aggression,
                CircleElapsed = _circleElapsed,
                CircleTime = _circleTime,
                HasToken = AttackTokenRegistry.Holds(this, _target),
                TokenAvailable = AttackTokenRegistry.IsAvailable(_target, this, capacity),
                AttackReady = attackReady,
            };

            CombatAction action = CombatTacticsRules.Decide(input, Random.value);
            if (action == CombatAction.Attack && !input.HasToken)
            {
                if (AttackTokenRegistry.TryTake(_target, this, capacity)) _attackElapsed = 0f;
                else action = CombatAction.Circle;
            }

            if (action == CombatAction.Circle && CurrentAction == CombatAction.Circle
                && CombatTacticsRules.ShouldFlipDirection(Random.value, style.directionChangeChance))
                _circleDirection = -_circleDirection;

            CurrentAction = action;
        }

        private void ApplyMovement(CombatStyleSO style)
        {
            if (!_agent.isActiveAndEnabled || !_agent.isOnNavMesh) return;
            Entity entity = _persistentID.Entity;
            Vector3 targetPos = _target.Transform.position;
            float ring = CombatTacticsRules.RingRadius(entity.AttackRange, style.ringMargin);
            _agent.isStopped = false;

            switch (CurrentAction)
            {
                case CombatAction.Approach:
                    _agent.updateRotation = true;
                    _agent.speed = entity.EngageSpeed;
                    _agent.stoppingDistance = ring;
                    _agent.SetDestination(targetPos);
                    break;

                case CombatAction.Attack:
                    _agent.updateRotation = false;
                    _agent.speed = entity.EngageSpeed;
                    _agent.stoppingDistance = entity.AttackRange * style.attackApproachFraction;
                    _agent.SetDestination(targetPos);
                    break;

                case CombatAction.BackOff:
                    _agent.updateRotation = false;
                    _agent.speed = style.backOffSpeed;
                    _agent.stoppingDistance = 0f;
                    if (TrySample(style, targetPos + Away(targetPos) * ring, out Vector3 back)) _agent.SetDestination(back);
                    else _agent.ResetPath(); // no room behind (wall / mesh edge): hold position, never keep a stale chase path
                    break;

                case CombatAction.Circle:
                    _agent.updateRotation = false;
                    _agent.speed = style.circleSpeed;
                    _agent.stoppingDistance = 0f;
                    bool found = TryCirclePoint(style, targetPos, ring, _circleDirection, out Vector3 point);
                    if (!found && TryCirclePoint(style, targetPos, ring, -_circleDirection, out point))
                    {
                        _circleDirection = -_circleDirection; // only flip when the other side is open
                        found = true;
                    }
                    if (found)
                    {
                        _agent.SetDestination(point);
                    }
                    else
                    {
                        CurrentAction = CombatAction.Approach; // boxed in: plain chase
                        ApplyMovement(style);
                    }
                    break;
            }
        }

        private bool TryCirclePoint(CombatStyleSO style, Vector3 center, float ring, int direction, out Vector3 point)
        {
            float angle = direction * style.circleSpeed * style.decisionInterval * CIRCLE_LEAD_INTERVALS / Mathf.Max(ring, 0.01f);
            Vector3 offset = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * Away(center) * ring;
            return TrySample(style, center + offset, out point);
        }

        // Flat unit vector target → self (forward-away when standing on the target).
        private Vector3 Away(Vector3 targetPos)
        {
            Vector3 away = transform.position - targetPos;
            away.y = 0f;
            return away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
        }

        private static bool TrySample(CombatStyleSO style, Vector3 position, out Vector3 result)
        {
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, style.navMeshSampleRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
            result = position;
            return false;
        }

        private void FaceTarget()
        {
            Vector3 dir = _target.Transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir),
                _persistentID.Entity.WarningTurnSpeed * Time.deltaTime);
        }
    }
}
