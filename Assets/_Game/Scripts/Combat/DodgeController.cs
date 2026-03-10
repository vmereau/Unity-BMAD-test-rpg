using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles dodge roll input, gating, stamina, movement, and i-frame state.
    /// Dodge direction is camera-relative based on Move input at the moment of dodge.
    /// Falls back to backward roll when no WASD input is held.
    /// During dodge, drives CharacterController.Move() directly (PlayerController pauses).
    /// Story 2.7: Initial implementation.
    /// Attach to the Player prefab root alongside PlayerCombat, StaminaSystem, PlayerStateManager.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(PlayerStateManager))]
    public class DodgeController : MonoBehaviour
    {
        private const string TAG = "[Combat]";
        private const float GRAVITY = -9.81f;
        private const float GROUNDED_VELOCITY = -2f;

        [SerializeField] private CombatConfigSO _config;

        private CharacterController _characterController;
        private StaminaSystem _staminaSystem;
        private PlayerStateManager _stateManager;
        private Camera _mainCamera;
        private InputSystem_Actions _input;

        private Vector3 _dodgeDirection;
        private float _dodgeTimer;
        private float _dodgeVerticalVelocity;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — DodgeController disabled");
                enabled = false;
                return;
            }

            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
            {
                GameLog.Error(TAG, "StaminaSystem not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                GameLog.Warn(TAG, "Camera.main not found — dodge direction will use world-space fallback");

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Dodge.started += OnDodgeStarted;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Dodge.started -= OnDodgeStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (!_stateManager.IsDodging) return;

            _dodgeTimer -= Time.deltaTime;
            _dodgeVerticalVelocity += GRAVITY * Time.deltaTime;

            Vector3 velocity = _dodgeDirection * _config.dodgeSpeed
                             + Vector3.up * _dodgeVerticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);

            if (_dodgeTimer <= 0f)
            {
                _dodgeTimer = 0f;
                _stateManager.SetDodging(false);
                _dodgeVerticalVelocity = 0f;
                GameLog.Info(TAG, "Dodge roll complete");
            }
        }

        private void OnDodgeStarted(InputAction.CallbackContext ctx)
        {
            if (_stateManager.IsAirborne)
            {
                GameLog.Warn(TAG, "Cannot dodge while airborne");
                return;
            }

            if (_stateManager.IsBlocking)
            {
                GameLog.Warn(TAG, "Cannot dodge while blocking");
                return;
            }

            if (_stateManager.IsDodging)
            {
                GameLog.Warn(TAG, "Cannot dodge: already dodging");
                return;
            }

            // Stamina gate
            if (!_staminaSystem.HasEnough(_config.dodgeStaminaCost))
            {
                GameLog.Warn(TAG, "Cannot dodge: insufficient stamina");
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.dodgeStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                return;
            }

            // Compute camera-relative dodge direction from current Move input
            Vector2 moveInput = _input.Player.Move.ReadValue<Vector2>();
            bool isBackwardRoll = false;
            if (moveInput.magnitude >= 0.1f)
            {
                if (_mainCamera != null)
                {
                    Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
                    Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
                    _dodgeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
                }
                else
                {
                    _dodgeDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                }
            }
            else
            {
                // No directional input — backward roll
                _dodgeDirection = -transform.forward;
                _dodgeDirection.y = 0f;
                if (_dodgeDirection.sqrMagnitude < 0.001f)
                    _dodgeDirection = Vector3.back; // absolute fallback
                else
                    _dodgeDirection.Normalize();
                isBackwardRoll = true;
            }

            _dodgeTimer = _config.dodgeDuration;
            _dodgeVerticalVelocity = GROUNDED_VELOCITY; // start with ground-snap velocity

            // Cancel any in-progress attack so combo state is cleared
            if (_stateManager.IsAttacking)
            {
                _stateManager.SetAttacking(false);
                GameLog.Info(TAG, "Dodge cancelled active attack");
            }

            _stateManager.SetDodging(true, isBackwardRoll);
            GameLog.Info(TAG, $"Dodge roll started: dir={_dodgeDirection}, dur={_config.dodgeDuration}s");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            string dodgeState = _stateManager.IsDodging
                ? $"ROLLING {_dodgeTimer:F2}s"
                : "ready";
            GUI.Label(new Rect(10, 190, 500, 26),
                $"Dodge: {dodgeState} | CanDodge:{_staminaSystem.HasEnough(_config.dodgeStaminaCost)}",
                style);
        }
#endif
    }
}
