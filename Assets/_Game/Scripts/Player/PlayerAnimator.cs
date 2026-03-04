using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the Player Animator from CharacterController velocity.
    /// Reads horizontal speed and updates the "Speed" blend tree parameter.
    /// PlayerController owns movement; this component owns animation only.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float DAMP_TIME = 0.1f;
        private const float RISING_VELOCITY_THRESHOLD = 0.1f;

        // Hash the parameter name once at class init — never use string in hot path
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsRisingHash = Animator.StringToHash("IsRising");

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
    }
}
