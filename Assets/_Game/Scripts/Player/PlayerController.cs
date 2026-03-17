using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Handles player movement relative to the main camera using CharacterController.
    /// Supports walk speed (default) and run speed (Sprint held) configurable via PlayerConfigSO.
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
            Vector2 moveInput = _input.Player.Move.ReadValue<Vector2>();

            if(_stateManager != null && !_stateManager.CanMove()) {
                moveInput = new Vector2(0f, 0f);
            }

            Vector3 moveDir = Vector3.zero;

            // --- Lock-on movement branch ---
            if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
            {
                Vector3 toTarget = _lockOnSystem.LockedTarget.position - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.0001f)
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
                if (_mainCamera != null)
                {
                    Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
                    Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
                    moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;
                }
                else
                {
                    // Fallback: world-space axes when no camera is present
                    moveDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                }

                // Rotate body to face movement direction
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                        _config.rotationSpeed * Time.deltaTime);
                }
            }

            bool isSprinting = _input.Player.Sprint.IsPressed();
            float currentSpeed = isSprinting ? _config.runSpeed : _config.walkSpeed;
            Vector3 velocity = moveDir * currentSpeed + Vector3.up * _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }
    }
}
