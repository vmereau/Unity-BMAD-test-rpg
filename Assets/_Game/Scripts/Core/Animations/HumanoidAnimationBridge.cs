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
        private static readonly int Attack1Hash = Animator.StringToHash("Attack_1");
        private static readonly int Attack2Hash = Animator.StringToHash("Attack_2");
        private static readonly int Attack3Hash = Animator.StringToHash("Attack_3");
        private static readonly int IsSneakingHash = Animator.StringToHash("IsSneaking");
        private static readonly int SneakToSprintHash = Animator.StringToHash("SneakToSprint");

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

        /// <summary>
        /// Sets the IsSneaking bool. Entering sneak also clears a pending SneakToSprint trigger so a stale one
        /// can't fire on the next sneak exit.
        /// </summary>
        public void SetSneaking(bool value)
        {
            if (_animator == null) return;
            _animator.SetBool(IsSneakingHash, value);
            if (value) _animator.ResetTrigger(SneakToSprintHash);
        }

        /// <summary>Fires SneakToSprint (sneak → sprint transition clip). Call after <see cref="SetSneaking"/>(false).</summary>
        public void TriggerSneakToSprint() => _animator?.SetTrigger(SneakToSprintHash);

        public void PlayAttack(int triggerHash)
        {
            if (_animator != null && triggerHash != 0) _animator.SetTrigger(triggerHash);
        }

        /// <summary>Trigger hash for combo step 1..3 (<c>Attack_1/2/3</c>); 0 for any other step.</summary>
        public int AttackTriggerHash(int step)
        {
            switch (step)
            {
                case 1: return Attack1Hash;
                case 2: return Attack2Hash;
                case 3: return Attack3Hash;
                default: return 0;
            }
        }

        /// <summary>Clears pending Attack_1/2/3 triggers so a stale one can't auto-chain the next attack.</summary>
        public void ResetAttackTriggers()
        {
            if (_animator == null) return;
            _animator.ResetTrigger(Attack1Hash);
            _animator.ResetTrigger(Attack2Hash);
            _animator.ResetTrigger(Attack3Hash);
        }

        public void PlayDodge(bool isBackwardRoll = false)
        {
            if (_animator != null)
                _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwardsHash : IsDodgingHash);
        }

        public void TriggerGetHit() => _animator?.SetTrigger(GetHitHash);
        public void TriggerDeath()  => _animator?.SetTrigger(DeathHash);

        /// <summary>
        /// Returns the Animator to its default state with default parameters (e.g. out of the terminal Death
        /// state after a revive). Callers must re-apply any parameter they still need (e.g. IsInCombat).
        /// </summary>
        public void ResetToDefaultState()
        {
            if (_animator == null) return;
            _animator.Rebind();
            _animator.Update(0f);
        }
    }
}
