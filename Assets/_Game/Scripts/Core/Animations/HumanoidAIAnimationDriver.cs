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
    /// lifecycle. Attacks play the unarmed combo full-body on the Base Layer:
    /// <c>TriggerAttack</c> resets the Attack_1/2/3 triggers and sets <c>Attack_1</c>;
    /// <c>TriggerComboStep(2|3)</c> sets <c>Attack_2|Attack_3</c>.
    /// </summary>
    [RequireComponent(typeof(HumanoidAnimationBridge))]
    public class HumanoidAIAnimationDriver : AIAnimationDriver
    {
        private const string TAG = "[AI]";
        private const int MAX_COMBO_STEPS = 3; // Humanoid_Template Base Layer: Attack_1 → Attack_2 → Attack_3

        [SerializeField] private HumanoidAnimationBridge _bridge;

        [Tooltip("Velocity at which the humanoid 2D blend tree shows the run clip (normalized = ±1.0). Set to match the entity's NavMeshAgent peak speed. Default 4f matches Entity.EngageSpeed default.")]
        [SerializeField] private float _runSpeed = 4f;

        [Tooltip("Components disabled when the ragdoll activates (EntityBrain, EntityHealth, NavMeshAgent, etc.). NavMeshAgent is a Behaviour (not MonoBehaviour), so the field type is Behaviour[].")]
        [SerializeField] private Behaviour[] _componentsToDisableOnDeath;

        [Tooltip("Transforms (like InteractionCollider or Hitbox) that should follow the ragdoll hips after death instead of staying at the root.")]
        [SerializeField] private Transform[] _transformsToPinToHips;

        [Tooltip("Seconds for a full 0↔1 fade of the UpperBody layer weight (combat carry pose in / out, attack start / end).")]
        [Min(0.01f)] [SerializeField] private float _upperBodyBlendTime = 0.15f;

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;
        private bool _warnedWarningNotImplemented;
        private bool _isInCombat;
        private float _upperBodyWeight;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No HumanoidAnimationBridge sibling — HumanoidAIAnimationDriver disabled");
                enabled = false;
                return;
            }
            // Before the _runSpeed early return: the bridge keeps animating even when this driver disables itself.
            var animator = _bridge.Animator;
            if (animator != null && animator.applyRootMotion)
            {
                // NPCs attack in place: with root motion on, full-body attack clips move the Animator's own transform
                // (Character child) away from the agent root, and a cut swing leaves the offset for good.
                // Nested models default it on — force it off.
                GameLog.Warn(TAG, $"{gameObject.name}: Animator '{animator.name}' had Apply Root Motion on — forced off (NPC body would drift from its root)");
                animator.applyRootMotion = false;
            }

            if (_runSpeed <= 0f)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: _runSpeed must be > 0 — HumanoidAIAnimationDriver disabled");
                enabled = false;
                return;
            }

            if (animator != null)
            {
                _ragdollBodies = animator.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in _ragdollBodies)
                    rb.isKinematic = true;
            }
        }

        // UpperBody layer: combat carry pose in combat, 0 while an attack / reaction / death plays full-body (UpperBodyWeight).
        private void Update()
        {
            if (_bridge == null || _ragdollActive) return;
            float target = UpperBodyWeight.Target(_bridge.IsBaseFullBody, _bridge.IsUpperBodyAction, _isInCombat);
            _upperBodyWeight = UpperBodyWeight.Step(_upperBodyWeight, target, Time.deltaTime, _upperBodyBlendTime);
            _bridge.SetUpperBodyWeight(_upperBodyWeight);
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

        // An Attack_1 set before the Base Layer took it (e.g. disengage during the
        // IsInCombat blend) would otherwise fire on the next combat entry as a damage-less punch.
        public override void CancelAttack()
        {
            if (_bridge == null) return;
            _bridge.ResetAttackTriggers();
        }

        public override void TriggerGetHit() => _bridge?.TriggerGetHit();
        public override void TriggerDeath()  => _bridge?.TriggerDeath();
        public override void TriggerStagger(int variant) => _bridge?.PlayStagger(variant);
        public override void TriggerKnockDown() => _bridge?.PlayKnockDown();
        public override void ClearStanceReaction() => _bridge?.ResetStanceTriggers();
        public override void SetInCombat(bool active)
        {
            _isInCombat = active;
            _bridge?.SetInCombat(active);
        }

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

            var animator = _bridge != null ? _bridge.Animator : null;
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
