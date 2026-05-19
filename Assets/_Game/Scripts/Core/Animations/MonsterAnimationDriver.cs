using Game.Core;
using Game.World;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Animations
{
    /// <summary>
    /// Monster concrete <see cref="AIAnimationDriver"/>. Owns Speed smoothing, ragdoll-bone
    /// discovery, <c>Entity.AnimatorOverride</c> application, and death-component-disable
    /// lifecycle. Delegates animator parameter writes to <see cref="MonsterAnimationBridge"/>.
    /// Humanoid AI uses <c>HumanoidAIAnimationDriver</c> instead — its <c>Speed</c> value is in
    /// raw units and would saturate the humanoid 2D blend tree.
    /// </summary>
    public class MonsterAnimationDriver : AIAnimationDriver
    {
        private const string TAG = "[AI]";
        private const float SPEED_SMOOTHING_RATE = 10f;

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private MonsterAnimationBridge _bridge;
        [SerializeField] private MonoBehaviour[] _componentsToDisableOnDeath;

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;
        private float _smoothedAnimSpeed;

        private void Awake()
        {
            if (_bridge == null) _bridge = GetComponent<MonsterAnimationBridge>();
            if (_bridge == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No MonsterAnimationBridge assigned — MonsterAnimationDriver is a no-op");
                return;
            }

            if (_persistentID != null && _persistentID.Entity != null && _persistentID.Entity.AnimatorOverride != null && _bridge.Animator != null)
                _bridge.Animator.runtimeAnimatorController = _persistentID.Entity.AnimatorOverride;

            if (_bridge.Animator != null)
            {
                _ragdollBodies = _bridge.Animator.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in _ragdollBodies)
                    rb.isKinematic = true;
            }
        }

        public override void DriveLocomotion(NavMeshAgent agent)
        {
            if (_bridge == null || agent == null) return;
            float target = agent.velocity.magnitude;
            _smoothedAnimSpeed = Mathf.Lerp(_smoothedAnimSpeed, target, Time.deltaTime * SPEED_SMOOTHING_RATE);
            _bridge.SetMoveSpeed(_smoothedAnimSpeed);
        }

        public override void TriggerAttack() => _bridge?.TriggerAttack();
        public override void TriggerGetHit() => _bridge?.TriggerGetHit();
        public override void TriggerDeath()  => _bridge?.TriggerDeath();

        public override void EnableRagdoll()
        {
            if (_ragdollActive) return;

            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                DisableDeathComponents();
                return;
            }

            if (_bridge != null && _bridge.Animator != null) _bridge.Animator.enabled = false;
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
