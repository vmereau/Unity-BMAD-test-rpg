using Game.Core;
using Game.Stealth;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// Owns the sneak toggle (Crouch action, C) and exposes the player as an <see cref="IStealthTarget"/> to AI
    /// perception. State lives in <see cref="PlayerStateManager"/> (<c>IsSneaking</c> / <c>SetSneaking</c>);
    /// dodge, jump and death exits are handled there. This component detects the running exit: Sprint held
    /// while moving → <see cref="PlayerStateManager.ExitSneakToSprint"/>.
    /// Attach to the Player prefab root.
    /// </summary>
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerSneak : MonoBehaviour, IStealthTarget
    {
        private const string TAG = "[Player]";
        private const float MOVE_INPUT_THRESHOLD_SQR = 0.01f;
        private const float MAX_PLAUSIBLE_SPEED = 15f; // m/s — above it the frame was a teleport / warp (run speed 6)
        private const float SPEED_SMOOTHING = 10f; // 1/s, exponential smoothing of the measured speed

        [SerializeField] private StealthConfigSO _stealthConfig;

        private PlayerStateManager _stateManager;
        private PlayerSwimming _swimming; // optional: swim-sprint = running noise
        private InputSystem_Actions _input;
        private Vector3 _lastPosition;
        private float _horizontalSpeed;

        public bool IsSneaking => _stateManager != null && _stateManager.IsSneaking;

        /// <summary>Movement noise from the smoothed flat speed (not the Sprint key): Silent while sneaking or still.</summary>
        public MovementNoise Noise =>
            _stealthConfig == null || _stateManager == null
                ? MovementNoise.Silent
                : StealthDetection.ClassifyNoise(_stateManager.IsSneaking, _stateManager.IsSwimming,
                    _swimming != null && _swimming.IsSwimSprinting, _horizontalSpeed,
                    _stealthConfig.noiseMinSpeed, _stealthConfig.runNoiseSpeed);

        public Vector3 VisibilityPoint
        {
            get
            {
                if (_stealthConfig == null) return transform.position;
                float height = IsSneaking
                    ? _stealthConfig.sneakingVisibilityHeight
                    : _stealthConfig.standingVisibilityHeight;
                return transform.position + Vector3.up * height;
            }
        }

        private void Awake()
        {
            _stateManager = GetComponent<PlayerStateManager>();
            _swimming = GetComponent<PlayerSwimming>();
            if (_stealthConfig == null)
            {
                GameLog.Error(TAG, "StealthConfigSO not assigned — PlayerSneak disabled.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Crouch.started += HandleCrouchStarted;
            _lastPosition = transform.position;
            _horizontalSpeed = 0f;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Crouch.started -= HandleCrouchStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            UpdateHorizontalSpeed();
            if (!_stateManager.IsSneaking) return;
            if (_stateManager.IsAirborne)
                _stateManager.SetSneaking(false); // walked off a ledge (coyote-smoothed — steps don't count)
            else if (IsSprintingWhileMoving())
                _stateManager.ExitSneakToSprint();
        }

        // Smoothed flat speed from the position delta (real movement: Shift against a wall or standing still stays silent).
        // A teleport or warp (respawn, load, controller snap) reads as an implausible speed — speed reset to 0.
        private void UpdateHorizontalSpeed()
        {
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            delta.y = 0f;
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // paused
            float speed = delta.magnitude / dt;
            if (speed > MAX_PLAUSIBLE_SPEED) { _horizontalSpeed = 0f; return; }
            _horizontalSpeed = Mathf.Lerp(_horizontalSpeed, speed, 1f - Mathf.Exp(-SPEED_SMOOTHING * dt));
        }

        private void HandleCrouchStarted(InputAction.CallbackContext ctx)
        {
            if (_stateManager.IsSneaking)
            {
                _stateManager.SetSneaking(false);
                return;
            }
            if (_stateManager.CanSneak() && !IsSprintingWhileMoving())
                _stateManager.SetSneaking(true);
        }

        // No sprint while aiming the bow (PlayerController ignores Sprint then) or during a swing (no move input) — Shift must
        // not drop the sneak for nothing.
        private bool IsSprintingWhileMoving() =>
            !_stateManager.IsAiming
            && _stateManager.CanMove()
            && _input.Player.Sprint.IsPressed()
            && _input.Player.Move.ReadValue<Vector2>().sqrMagnitude > MOVE_INPUT_THRESHOLD_SQR;
    }
}
