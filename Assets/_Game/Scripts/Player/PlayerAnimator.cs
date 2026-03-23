using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the Player Animator from CharacterController velocity and combat state.
    /// Owns all animator calls: 2D locomotion blend tree (VelocityX, VelocityZ, IsGrounded, IsRising),
    /// and combat animation triggers/bools (Attack, Block, Dodge).
    /// PlayerStateManager calls the public combat methods — never touches the Animator directly.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float DAMP_TIME = 0.1f;
        private const float RISING_VELOCITY_THRESHOLD = 0.1f;

        // Locomotion parameters
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsRisingHash = Animator.StringToHash("IsRising");
        private static readonly int VelocityXHash = Animator.StringToHash("VelocityX");
        private static readonly int VelocityZHash = Animator.StringToHash("VelocityZ");

        // Combat parameters
        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");
        private static readonly int IsDodgingHash = Animator.StringToHash("IsDodging");
        private static readonly int IsDodgingBackwardsHash = Animator.StringToHash("IsDodgingBackwards");
        private static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");

        [SerializeField] private PlayerConfigSO _config;

        private Animator _animator;
        private CharacterController _characterController;
        // Cached: IsInCombat parameter not present until Story 7.13 adds it to the controller
        private bool _hasIsInCombatParam;

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
            if (_config == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO not assigned — PlayerAnimator disabled.");
                enabled = false;
                return;
            }
            if (_config.runSpeed <= 0f)
            {
                GameLog.Error(TAG, "PlayerConfigSO.runSpeed must be > 0 — PlayerAnimator disabled.");
                enabled = false;
                return;
            }

            foreach (var p in _animator.parameters)
                if (p.nameHash == IsInCombatHash) { _hasIsInCombatParam = true; break; }
        }

        private void Update()
        {
            // Always drive 2D blend tree with local-space velocity.
            // When not locked on, the character faces the movement direction so
            // localVelocity.z ≈ speed and localVelocity.x ≈ 0 — the forward clips play correctly.
            // When locked on, the character faces the target so strafing produces non-zero x.
            Vector3 worldHoriz = new Vector3(
                _characterController.velocity.x, 0f, _characterController.velocity.z);
            Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
            float normX = Mathf.Clamp(localVelocity.x / _config.runSpeed, -1f, 1f);
            float normZ = Mathf.Clamp(localVelocity.z / _config.runSpeed, -1f, 1f);
            _animator.SetFloat(VelocityXHash, normX, DAMP_TIME, Time.deltaTime);
            _animator.SetFloat(VelocityZHash, normZ, DAMP_TIME, Time.deltaTime);

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

        /// <summary>Drives the IsInCombat animator bool. Layer weight set in Story 7.13.</summary>
        public void SetInCombat(bool value)
        {
            if (_animator != null && _hasIsInCombatParam) _animator.SetBool(IsInCombatHash, value);
        }
    }
}
