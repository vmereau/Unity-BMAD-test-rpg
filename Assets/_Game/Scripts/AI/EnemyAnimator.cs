using Game.Core;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Bridges EnemyBrain/EnemyHealth state transitions to Animator parameter calls.
    /// Lives on the enemy ROOT GameObject. The Animator lives on the CreatureVisual child GO —
    /// do NOT use [RequireComponent(typeof(Animator))]; assign via Inspector.
    ///
    /// At Awake: applies AnimatorOverrideController from EnemyTypeSO, collects ragdoll Rigidbodies
    /// and sets them kinematic so they don't interfere with NavMesh locomotion.
    ///
    /// EnableRagdoll() is called by SMB_DeathState.OnStateExit when the Death animation completes.
    /// Body is never deactivated via SetActive(false).
    /// Enemy Creature System: Initial implementation.
    /// </summary>
    public class EnemyAnimator : MonoBehaviour
    {
        private const string TAG = "[AI]";

        [SerializeField] private EnemyTypeSO _type;
        [SerializeField] private Animator _animator; // wire to CreatureVisual child's Animator in Inspector

        private Rigidbody[] _ragdollBodies;
        private bool _ragdollActive;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DeathHash  = Animator.StringToHash("Death");

        private void Awake()
        {
            if (_animator == null)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No Animator assigned — EnemyAnimator is a no-op");
                return;
            }

            if (_type != null && _type.AnimatorOverride == null)
                GameLog.Warn(TAG, $"{gameObject.name}: EnemyTypeSO has no animatorOverride — base controller clips will play");

            if (_type?.AnimatorOverride != null)
                _animator.runtimeAnimatorController = _type.AnimatorOverride;

            _ragdollBodies = _animator.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in _ragdollBodies)
                rb.isKinematic = true;
        }

        public void SetMoveSpeed(float speed)
        {
            _animator?.SetFloat(SpeedHash, speed);
        }

        public void TriggerAttack()
        {
            _animator?.SetTrigger(AttackHash);
        }

        public void TriggerGetHit()
        {
            _animator?.SetTrigger(GetHitHash);
        }

        public void TriggerDeath()
        {
            if (_animator == null) return;
            _animator.SetTrigger(DeathHash);
        }

        /// <summary>
        /// Called by SMB_DeathState.OnStateExit when the Death animation finishes.
        /// Disables the Animator (to freeze pose), makes all ragdoll Rigidbodies non-kinematic,
        /// then disables EnemyBrain and EnemyHealth to stop per-frame updates.
        /// If no ragdoll bodies are found (e.g. Enemy_Grunt without a ragdoll), components are
        /// disabled and the body remains in the scene without physics.
        /// </summary>
        public void EnableRagdoll()
        {
            if (_ragdollActive) return;  // guard against double-call

            if (_ragdollBodies == null || _ragdollBodies.Length == 0)
            {
                GameLog.Info(TAG, $"{gameObject.name} has no ragdoll bodies — disabling components");
                if (TryGetComponent<EnemyBrain>(out var brain)) brain.enabled = false;
                if (TryGetComponent<EnemyHealth>(out var health)) health.enabled = false;
                return;
            }

            _animator.enabled = false;
            foreach (var rb in _ragdollBodies)
                rb.isKinematic = false;

            _ragdollActive = true;

            if (TryGetComponent<EnemyBrain>(out var b)) b.enabled = false;
            if (TryGetComponent<EnemyHealth>(out var h)) h.enabled = false;

            GameLog.Info(TAG, $"{gameObject.name} ragdoll activated");
        }
    }
}
