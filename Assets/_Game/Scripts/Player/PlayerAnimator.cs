using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the Player Animator from CharacterController velocity and combat state.
    /// Owns all animator calls: movement blend tree (Speed, IsGrounded, IsRising)
    /// and combat animation triggers/bools (Attack, Block, Dodge).
    /// PlayerStateManager calls the public combat methods — never touches the Animator directly.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float DAMP_TIME = 0.1f;
        private const float RISING_VELOCITY_THRESHOLD = 0.1f;

        // Movement parameters
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsRisingHash = Animator.StringToHash("IsRising");

        // Combat parameters
        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int IsDodgingBackwardsHash = Animator.StringToHash("IsDodgingBackwards");

        private Animator _animator;
        private CharacterController _characterController;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _characterController = GetComponent<CharacterController>();

            if (_animator == null)
            {
                GameLog.Error(TAG, "Animator component not found — PlayerAnimator disabled.");
                enabled = false;
                return;
            }
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found — PlayerAnimator cannot read speed.");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            Vector3 horizontalVelocity = new Vector3(
                _characterController.velocity.x, 0f, _characterController.velocity.z);
            float speed = horizontalVelocity.magnitude;
            _animator.SetFloat(SpeedHash, speed, DAMP_TIME, Time.deltaTime);
            _animator.SetBool(IsGroundedHash, _characterController.isGrounded);
            _animator.SetBool(IsRisingHash, _characterController.velocity.y > RISING_VELOCITY_THRESHOLD);
        }

        // ── Combat animation API ─────────────────────────────────────────────
        // Called exclusively by PlayerStateManager. No other script should call these.

        /// <summary>Drives the IsBlocking animator bool.</summary>
        public void SetBlocking(bool value)
        {
            if (_animator != null) _animator.SetBool(IsBlockingHash, value);
        }

        /// <summary>
        /// Fires an attack animator trigger. Pass the precomputed trigger hash
        /// (e.g. Animator.StringToHash("Attack1")) to play the corresponding clip.
        /// </summary>
        public void PlayAttack(int triggerHash)
        {
            if (_animator != null && triggerHash != 0) _animator.SetTrigger(triggerHash);
        }

        /// <summary>Fires the dodge animator trigger (forward or backward roll).</summary>
        public void PlayDodge(bool isBackwardRoll = false)
        {
            if (_animator != null)
                _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwardsHash : IsDodgingHash);
        }
    }
}
