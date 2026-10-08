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

        [SerializeField] private StealthConfigSO _stealthConfig;

        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;

        public bool IsSneaking => _stateManager != null && _stateManager.IsSneaking;

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
            if (!_stateManager.IsSneaking) return;
            if (_stateManager.IsAirborne)
                _stateManager.SetSneaking(false); // walked off a ledge (coyote-smoothed — steps don't count)
            else if (IsSprintingWhileMoving())
                _stateManager.ExitSneakToSprint();
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

        private bool IsSprintingWhileMoving() =>
            _input.Player.Sprint.IsPressed()
            && _input.Player.Move.ReadValue<Vector2>().sqrMagnitude > MOVE_INPUT_THRESHOLD_SQR;
    }
}
