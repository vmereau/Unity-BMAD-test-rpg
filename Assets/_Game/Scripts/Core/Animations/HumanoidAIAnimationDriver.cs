using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Animations
{
    /// <summary>
    /// Concrete <see cref="AIAnimationDriver"/> for humanoid AI entities (NPCs and, later,
    /// humanoid enemies). Reads <c>NavMeshAgent.velocity</c>, normalizes it into local space
    /// against <c>_runSpeed</c>, and forwards <c>VelocityX</c>/<c>VelocityZ</c>/<c>IsGrounded</c>/<c>IsRising</c>
    /// to <see cref="HumanoidAnimationBridge"/>. Owns ragdoll-bone caching + death-component-disable
    /// lifecycle. Attacks play the unarmed combo on the upper-body <c>Attack</c> layer:
    /// <c>TriggerAttack</c> resets the Attack_1/2/3 triggers and sets <c>Attack_1</c>;
    /// <c>TriggerComboStep(2|3)</c> sets <c>Attack_2|Attack_3</c>.
    /// </summary>
    [RequireComponent(typeof(HumanoidAnimationBridge))]
    public class HumanoidAIAnimationDriver : AIAnimationDriver
    {
        private const string TAG = "[AI]";
        private const int MAX_COMBO_STEPS = 3; // Humanoid_Template Attack layer: Attack_1 → Attack_2 → Attack_3

        [SerializeField] private HumanoidAnimationBridge _bridge;

        [Tooltip("Velocity at which the humanoid 2D blend tree shows the run clip (normalized = ±1.0). Set to match the entity's NavMeshAgent peak speed. Default 4f matches Entity.EngageSpeed default.")]
        [SerializeField] private float _runSpeed = 4f;

        [Tooltip("Components disabled when the ragdoll activates (EntityBrain, EntityHealth, NavMeshAgent, etc.). NavMeshAgent is a Behaviour (not MonoBehaviour), so the field type is Behaviour[].")]
        [SerializeField] private Behaviour[] _componentsToDisableOnDeath;

        [Tooltip("Transforms (like InteractionCollider or Hitbox) that should follow the ragdoll hips after death instead of staying at the root.")]
        [SerializeField] private Transform[] _transformsToPinToHips;

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;
        private bool _warnedWarningNotImplemented;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No HumanoidAnimationBridge sibling — HumanoidAIAnimationDriver disabled");
                enabled = false;
                return;
            }
            if (_runSpeed <= 0f)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: _runSpeed must be > 0 — HumanoidAIAnimationDriver disabled");
                enabled = false;
                return;
            }

            var animator = _bridge.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                _ragdollBodies = animator.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in _ragdollBodies)
                    rb.isKinematic = true;
            }
        }

        public override void DriveLocomotion(NavMeshAgent agent)
        {
            if (_bridge == null || agent == null) return;
            Vector3 worldHoriz = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
            Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
            float normX = Mathf.Clamp(localVelocity.x / _runSpeed, -1f, 1f);
            float normZ = Mathf.Clamp(localVelocity.z / _runSpeed, -1f, 1f);
            _bridge.SetMovement(normX, normZ);
            _bridge.SetGrounded(true);
            _bridge.SetRising(false);
        }

        public override int MaxComboSteps => MAX_COMBO_STEPS;

        public override void TriggerAttack()
        {
            if (_bridge == null) return;
            _bridge.ResetAttackTriggers();
            _bridge.PlayAttack(_bridge.AttackTriggerHash(1));
        }

        public override void TriggerComboStep(int step)
        {
            if (_bridge == null || step < 2 || step > MAX_COMBO_STEPS) return;
            _bridge.PlayAttack(_bridge.AttackTriggerHash(step));
        }

        // An Attack_1 set before the Attack layer reached CombatIdle (e.g. disengage during the
        // IsInCombat blend) would otherwise fire on the next combat entry as a damage-less punch.
        public override void CancelAttack()
        {
            if (_bridge == null) return;
            _bridge.ResetAttackTriggers();
        }

        public override void TriggerGetHit() => _bridge?.TriggerGetHit();
        public override void TriggerDeath()  => _bridge?.TriggerDeath();
        public override void SetInCombat(bool active) => _bridge?.SetInCombat(active);

        public override void SetWarning(bool active)
        {
            // No-op for now; the Warning state in EntityBrain is handled via SetInCombat(true)
            // which toggles the humanoid animator into its CombatIdle/CombatLocomotion state.
        }

        public override void EnableRagdoll()
        {
            if (_ragdollActive) return;

            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                GameLog.Warn(TAG, $"{name}: no ragdoll bodies cached — disabling components only");
                DisableDeathComponents();
                return;
            }

            var animator = _bridge != null ? _bridge.GetComponentInChildren<Animator>() : null;
            if (animator != null)
            {
                animator.enabled = false;

                // Pin designated transforms (InteractionCollider, Hitbox, etc) to the hips 
                // so they follow the physical body instead of staying at the static root.
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null && _transformsToPinToHips != null)
                {
                    foreach (var t in _transformsToPinToHips)
                    {
                        if (t != null)
                        {
                            t.SetParent(hips, true);
                        }
                    }
                }
}

            foreach (var rb in _ragdollBodies)
                rb.isKinematic = false;

            _ragdollActive = true;
            DisableDeathComponents();
        }

        private void DisableDeathComponents()
        {
            if (_componentsToDisableOnDeath == null) return;
            foreach (var component in _componentsToDisableOnDeath)
            {
                if (component != null)
                    component.enabled = false;
            }
        }
    }
}
