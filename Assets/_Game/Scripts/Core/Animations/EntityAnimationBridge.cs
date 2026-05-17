using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Generic animator bridge for simple world entities driven by a single Speed parameter
    /// (monsters, ambient creatures, non-humanoid NPCs).
    /// Handles the "Entity contract": life-cycle (Death/Ragdoll), SO-driven Animator Overrides,
    /// and 1D Speed-based locomotion.
    ///
    /// Humanoid entities (Player, humanoid NPCs) use <see cref="HumanoidAnimationBridge"/> with a
    /// per-entity driver that owns velocity normalization. EntityAnimationBridge does NOT
    /// forward to HumanoidAnimationBridge — its Speed value is in raw units and would saturate
    /// the humanoid 2D blend tree.
    /// </summary>
    public class EntityAnimationBridge : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private PersistentID _persistentID;
        [SerializeField] private Animator _animator;
        [SerializeField] private MonoBehaviour[] _componentsToDisableOnDeath;

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DeathHash  = Animator.StringToHash("Death");

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            if (_animator == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No Animator assigned — EntityAnimationBridge is a no-op");
                return;
            }

            if (_persistentID != null && _persistentID.Entity != null && _persistentID.Entity.AnimatorOverride != null)
                _animator.runtimeAnimatorController = _persistentID.Entity.AnimatorOverride;

            _ragdollBodies = _animator.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in _ragdollBodies)
                rb.isKinematic = true;
        }

        public void SetMoveSpeed(float speed)
        {
            if (_animator == null) return;
            _animator.SetFloat(SpeedHash, speed);
        }

        public void TriggerAttack() => _animator?.SetTrigger(AttackHash);
        public void TriggerGetHit() => _animator?.SetTrigger(GetHitHash);

        public void TriggerDeath()
        {
            if (_animator == null) return;
            _animator.SetTrigger(DeathHash);
        }

        public void EnableRagdoll()
        {
            if (_ragdollActive) return;

            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                DisableDeathComponents();
                return;
            }

            _animator.enabled = false;
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
