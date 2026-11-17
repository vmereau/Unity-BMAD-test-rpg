using Game.Combat;
using Game.Core;
using Game.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Forward step of an AI humanoid's melee swing — the player's <c>PlayerAttackStep</c> rule applied to NPCs
    /// (spec-ai-attack-root-and-step). <see cref="EntityMeleeAttacker"/> calls <see cref="Begin"/> on every attack-state enter
    /// with the state's combo index; the step comes from <see cref="CombatConfigSO.ResolveAttackStep"/> with the wielded
    /// weapon (fists = <c>unarmedAttacks</c>), so NPCs and the player step the same. The brain keeps the agent stopped while
    /// an attack state plays; this moves the entity through <see cref="NavMeshAgent.Move"/> (stays on the navmesh, works while
    /// stopped, leaves the agent velocity — and so the locomotion params — at 0).
    /// Stops when the attack ends (<see cref="Stop"/>), the agent is off / off-mesh, and never brings the bodies of the entity and
    /// <see cref="EntityMeleeAttacker.AttackTarget"/> closer than <see cref="_contactMargin"/> (agent radius + target radius —
    /// <c>NavMeshAgent.Move</c> skips avoidance).
    /// </summary>
    [RequireComponent(typeof(EntityMeleeAttacker))]
    public class EntityAttackStep : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [Tooltip("Shared combat config: defaultAttackStep + unarmedAttacks (the player's asset).")]
        [SerializeField] private CombatConfigSO _combatConfig;
        [Tooltip("Gap (metres) the step keeps between the entity's agent radius and its attack target's body radius " +
                 "(NavMeshAgent / CharacterController / CapsuleCollider; none = 0).")]
        [Min(0f)] [SerializeField] private float _contactMargin = 0.05f;

        private EntityMeleeAttacker _attacker;
        private NavMeshAgent _agent;

        private AttackStep _step;
        private float _elapsed;
        private bool _active;
        private Transform _radiusTarget; // target whose body radius is cached in _targetRadius
        private float _targetRadius;

        /// <summary>True while a step window is running (tests / debug).</summary>
        public bool IsStepping => _active;

        private void Awake()
        {
            _attacker = GetComponent<EntityMeleeAttacker>();
            _agent = GetComponent<NavMeshAgent>();
            if (_combatConfig == null || _agent == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: EntityAttackStep needs a CombatConfigSO and a NavMeshAgent — swings stay in place");
                enabled = false;
            }
        }

        private void OnDisable() => _active = false;

        /// <summary>Ends the running step (attack ended / a new attack begins).</summary>
        public void Stop() => _active = false;

        /// <summary>Attack state <paramref name="attackIndex"/> (1 = Attack_1) entered: starts its step, replacing a running one.
        /// 0 = no step.</summary>
        public void Begin(int attackIndex)
        {
            _active = false;
            if (!enabled || _combatConfig == null || attackIndex < 1) return;
            _step = _combatConfig.ResolveAttackStep(_attacker.WieldedWeapon, attackIndex);
            _elapsed = 0f;
            _active = _step.distance > 0f && _step.duration > 0f;
        }

        private void Update()
        {
            if (!_active) return;
            if (!_attacker.IsAttacking || !_agent.isActiveAndEnabled || !_agent.isOnNavMesh)
            {
                _active = false;
                return;
            }

            float from = _elapsed;
            _elapsed += Time.deltaTime;
            float metres = _step.DisplacementBetween(from, _elapsed);
            if (_elapsed >= _step.startTime + _step.duration) _active = false;
            if (metres <= 0f) return;

            metres = ClampToTarget(metres);
            if (metres <= 0f) return;
            _agent.Move(transform.forward * metres);
        }

        // Shortens the step so the two bodies (ground plane) stay _contactMargin apart.
        private float ClampToTarget(float metres)
        {
            Transform target = _attacker.AttackTarget;
            if (target == null) return metres;
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float minDistance = _agent.radius + TargetRadius(target) + _contactMargin;
            float room = toTarget.magnitude - minDistance;
            return Mathf.Clamp(metres, 0f, Mathf.Max(0f, room));
        }

        private float TargetRadius(Transform target)
        {
            if (target == _radiusTarget) return _targetRadius;
            _radiusTarget = target;
            if (target.TryGetComponent(out NavMeshAgent agent)) _targetRadius = agent.radius;
            else if (target.TryGetComponent(out CharacterController controller)) _targetRadius = controller.radius;
            else if (target.TryGetComponent(out CapsuleCollider capsule)) _targetRadius = capsule.radius;
            else _targetRadius = 0f;
            return _targetRadius;
        }
    }
}
