using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Handles player movement relative to the main camera using CharacterController.
    /// Supports walk speed (default), run speed (Sprint held) and sneak speed (PlayerStateManager.IsSneaking),
    /// configurable via PlayerConfigSO.
    /// Uses the new Input System via the generated InputSystem_Actions wrapper.
    /// Manual gravity is applied each frame — CharacterController does not apply it automatically.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float GRAVITY = -9.81f;
        private const float GROUNDED_VELOCITY = -2f;

        [SerializeField] private PlayerConfigSO _config;

        [SerializeField] private LockOnSystem _lockOnSystem;

        private CharacterController _characterController;
        private Camera _mainCamera;
        private InputSystem_Actions _input;
        private float _verticalVelocity;

        /// <summary>Teleports (save load) drop any fall speed accumulated while the region was gone.</summary>
        public void ResetVerticalVelocity() => _verticalVelocity = GROUNDED_VELOCITY;
        private PlayerStateManager _stateManager;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController component not found. PlayerController cannot function.");
                return;
            }

            if (_config == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO not assigned. PlayerController cannot function.");
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                GameLog.Warn(TAG, "Camera.main not found in Awake. Movement will use world-space axes until a camera is available.");
            }

            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
                GameLog.Warn(TAG, "PlayerStateManager not found on Player — dodge gating unavailable");

            if (_lockOnSystem == null) _lockOnSystem = GetComponent<LockOnSystem>();
            if (_lockOnSystem == null)
                GameLog.Warn(TAG, "LockOnSystem not found on Player — lock-on movement unavailable");
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            GameLog.Info(TAG, "Input actions enabled.");
        }

        private void OnDisable()
        {
            if (_input == null) return;  // Guard: OnDisable may fire before OnEnable
            _input.Player.Disable();
            _input.Dispose();
            GameLog.Info(TAG, "Input actions disabled.");
        }

        private void Update()
        {
            if (_characterController == null || _config == null)
                return;

            // Yield control to DodgeController during dodge
            if (_stateManager != null && _stateManager.IsDodging)
                return;

            ApplyJump();
            ApplyGravity();
            ApplyMovement();
        }

        private void ApplyJump()
        {
            bool canJump = _stateManager != null ? _stateManager.CanJump() : _characterController.isGrounded;
            if (canJump && _input.Player.Jump.WasPressedThisFrame())
            {
                _verticalVelocity = _config.jumpForce;
                _stateManager?.NotifyJumpStarted();
                GameLog.Info(TAG, $"Jump triggered. Vertical velocity set to {_verticalVelocity}");
            }
        }

        private void ApplyGravity()
        {
            if (_characterController.isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = GROUNDED_VELOCITY;
            }
            else
            {
                _verticalVelocity += GRAVITY * Time.deltaTime;
            }
        }

        private void ApplyMovement()
        {
            Vector2 rawInput = _input.Player.Move.ReadValue<Vector2>();
            Vector2 moveInput = rawInput;

            if(_stateManager != null && !_stateManager.CanMove()) {
                moveInput = new Vector2(0f, 0f);
            }

            // Attack wind-up: no movement (PlayerAttackStep moves the swing), but the raw input still turns the player
            // until the step's HitboxEnable commits it.
            bool steering = _stateManager != null && _stateManager.CanSteerAttack();

            Vector3 moveDir = Vector3.zero;

            // --- Lock-on movement branch ---
            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
            {
                Vector3 toTarget = _lockOnSystem.LockedTarget.position - transform.position;
                toTarget.y = 0f;

                // Staggered / knocked down: no turning toward the target (the reaction clip owns the body).
                bool incapacitated = _stateManager != null && _stateManager.IsIncapacitated;
                if (toTarget.sqrMagnitude > 0.0001f && !incapacitated)
                {
                    Vector3 lockForward = toTarget.normalized;
                    Vector3 lockRight   = Vector3.Cross(Vector3.up, lockForward).normalized;
                    moveDir = lockForward * moveInput.y + lockRight * moveInput.x;
                    if (moveDir.sqrMagnitude > 0.01f) moveDir = moveDir.normalized;

                    // Always face the target (even when stationary)
                    Quaternion targetRot = Quaternion.LookRotation(lockForward);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        _config.rotationSpeed * Time.deltaTime);
                }
                // else: coincident with target — keep moveDir = zero, skip rotation to avoid NaN
            }
            // --- Free movement branch ---
            else
            {
                Vector3 inputDir;
                if (_mainCamera != null)
                {
                    Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
                    Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
                    inputDir = (camForward * rawInput.y + camRight * rawInput.x).normalized;
                }
                else
                {
                    // Fallback: world-space axes when no camera is present
                    inputDir = new Vector3(rawInput.x, 0f, rawInput.y).normalized;
                }
                moveDir = moveInput.sqrMagnitude > 0f ? inputDir : Vector3.zero;

                // Bow aim: always face the camera yaw (strafe), like lock-on faces its target
                bool isAiming = _stateManager != null && _stateManager.IsAiming;
                if (isAiming && _mainCamera != null)
                {
                    Quaternion targetRot = Quaternion.Euler(0f,
                        _mainCamera.transform.eulerAngles.y + _config.aimStanceYaw, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        _config.rotationSpeed * Time.deltaTime);
                }
                // Attack wind-up: turn toward the input, slower than free movement
                else if (steering)
                {
                    if (inputDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(inputDir),
                            _config.attackSteerSpeed * Time.deltaTime);
                }
                // Rotate body to face movement direction
                else if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        _config.rotationSpeed * Time.deltaTime);
                }
            }

            // No sprint while aiming the bow (walk speed max; sneak speed still applies)
            bool isSprinting = _input.Player.Sprint.IsPressed() && (_stateManager == null || !_stateManager.IsAiming);
            // Sneak overrides both; PlayerSneak ends sneak as soon as Sprint is held while moving.
            float currentSpeed = (_stateManager != null && _stateManager.IsSneaking)
                ? _config.sneakSpeed
                : (isSprinting ? _config.runSpeed : _config.walkSpeed);
            Vector3 velocity = moveDir * currentSpeed + Vector3.up * _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }
    }
}
