using UnityEngine;

namespace Game.Animations
{
    /// <summary>
    /// Maps code-level commands to the specific parameters of the Humanoid_Template animator controller.
    /// Used by both Players and complex NPCs.
    /// </summary>
    public class HumanoidAnimationBridge : MonoBehaviour
    {
        private const float DAMP_TIME = 0.1f;

        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsRisingHash = Animator.StringToHash("IsRising");
        private static readonly int VelocityXHash = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZHash = Animator.StringToHash("VelocityZ");
        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int IsDodgingBackwardsHash = Animator.StringToHash("IsDodgingBackwards");
        private static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");
        private static readonly int GetHitHash = Animator.StringToHash("GetHit");
        private static readonly int DeathHash = Animator.StringToHash("Death");

        [SerializeField] private Animator _animator;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        public void SetMovement(float x, float z)
        {
            if (_animator == null) return;
            _animator.SetFloat(VelocityXHash, x, DAMP_TIME, Time.deltaTime);
            _animator.SetFloat(VelocityZHash, z, DAMP_TIME, Time.deltaTime);
        }

        public void SetGrounded(bool value) => _animator?.SetBool(IsGroundedHash, value);
        public void SetRising(bool value) => _animator?.SetBool(IsRisingHash, value);
        public void SetBlocking(bool value) => _animator?.SetBool(IsBlockingHash, value);
        public void SetInCombat(bool value) => _animator?.SetBool(IsInCombatHash, value);

        public void PlayAttack(int triggerHash)
        {
            if (_animator != null && triggerHash != 0) _animator.SetTrigger(triggerHash);
        }

        public void PlayDodge(bool isBackwardRoll = false)
        {
            if (_animator != null)
                _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwardsHash : IsDodgingHash);
        }

        public void TriggerGetHit() => _animator?.SetTrigger(GetHitHash);
        public void TriggerDeath()  => _animator?.SetTrigger(DeathHash);
    }
}
