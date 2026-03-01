using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Handles WASD movement relative to the main camera using CharacterController.
    /// Uses the new Input System via the generated InputSystem_Actions wrapper.
    /// Manual gravity is applied each frame — CharacterController does not apply it automatically.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float GRAVITY = -9.81f;
        private const float GROUNDED_VELOCITY = -2f;

        [SerializeField] private float _moveSpeed = 5f;

        private CharacterController _characterController;
        private Camera _mainCamera;
        private InputSystem_Actions _input;
        private float _verticalVelocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController component not found. PlayerController cannot function.");
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                GameLog.Warn(TAG, "Camera.main not found in Awake. Movement will use world-space axes until a camera is available.");
            }
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            GameLog.Info(TAG, "Input actions enabled.");
        }

        private void OnDisable()
        {
            _input.Player.Disable();
            _input.Dispose();
            GameLog.Info(TAG, "Input actions disabled.");
        }

        private void Update()
        {
            if (_characterController == null)
                return;

            ApplyGravity();
            ApplyMovement();
        }

        private void ApplyGravity()
        {
            if (_characterController.isGrounded)
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

            Vector3 moveDir;
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

            Vector3 velocity = moveDir * _moveSpeed + Vector3.up * _verticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);
        }
    }
}
