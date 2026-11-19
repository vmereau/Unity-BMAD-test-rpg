using Game.Combat;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Stamina costs of moving while an enemy fights the player (<see cref="PlayerStateManager.IsAggroed"/>): running
    /// drains <see cref="SprintStaminaPerSecond"/> (whole points, like swim sprint; empty → walk speed until Sprint is
    /// pressed again) and each jump costs <see cref="JumpStaminaCost"/> (<see cref="TryPayJump"/>). Out of aggro both are
    /// free. Base values in <see cref="PlayerConfigSO"/>; the multipliers are the hook for equipment / skills / stats.
    /// Attach to the Player root. Runs after PlayerSwimming and before PlayerController so the controller reads this
    /// frame's <see cref="IsRunExhausted"/>.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerMovementStamina : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float MOVE_INPUT_THRESHOLD_SQR = 0.01f;

        [SerializeField] private PlayerConfigSO _config;
        [SerializeField] private StaminaSystem _staminaSystem;

        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;
        private float _staminaDebt;
        private bool _exhausted;

        /// <summary>Multiplier on the aggro run drain (1 = base). Equipment / skills / stats write it.</summary>
        public float SprintDrainMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on the aggro jump cost (1 = base). Equipment / skills / stats write it.</summary>
        public float JumpCostMultiplier { get; set; } = 1f;

        /// <summary>Stamina per second spent running while aggroed (base × <see cref="SprintDrainMultiplier"/>, ≥ 0).</summary>
        public float SprintStaminaPerSecond =>
            _config != null ? MovementStaminaRules.Modified(_config.aggroRunStaminaPerSecond, SprintDrainMultiplier) : 0f;

        /// <summary>Stamina per jump while aggroed (base × <see cref="JumpCostMultiplier"/>, ≥ 0).</summary>
        public float JumpStaminaCost =>
            _config != null ? MovementStaminaRules.Modified(_config.aggroJumpStaminaCost, JumpCostMultiplier) : 0f;

        /// <summary>True while aggroed and out of run stamina: walk speed until Sprint is released and pressed again.</summary>
        public bool IsRunExhausted => _exhausted && _stateManager != null && _stateManager.IsAggroed;

        private void Awake()
        {
            _stateManager = GetComponent<PlayerStateManager>();
            if (_config == null)
            {
                GameLog.Error(TAG, "PlayerConfigSO not assigned — PlayerMovementStamina disabled (movement is free)");
                enabled = false;
                return;
            }
            if (_staminaSystem == null) _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
                GameLog.Warn(TAG, "StaminaSystem not found — aggro run / jump costs inactive");
        }

        private void OnEnable()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            _exhausted = false;
            _staminaDebt = 0f;
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (_input == null) return;
            bool held = _input.Player.Sprint.IsPressed();
            // Released, or the fight is over: the next run starts fresh. The fractional debt is kept, so tapping Sprint
            // never runs for free.
            if (!held || !_stateManager.IsAggroed) _exhausted = false;

            if (!IsPayingToRun(held)) return;

            float consume = MovementStaminaRules.DrainStep(ref _staminaDebt, SprintStaminaPerSecond, Time.deltaTime,
                _staminaSystem.CurrentStamina, out bool exhausted);
            if (exhausted)
            {
                _exhausted = true;
                GameLog.Info(TAG, "Out of stamina — running stopped until Sprint is pressed again");
                return;
            }
            if (consume > 0f) _staminaSystem.Consume(consume);
        }

        // Same conditions under which PlayerController picks the run speed, plus aggro and actual movement.
        private bool IsPayingToRun(bool sprintHeld) =>
            sprintHeld && !_exhausted && _staminaSystem != null && _stateManager.IsAggroed
            && _stateManager.CanMove() && !_stateManager.IsSwimming && !_stateManager.IsSneaking
            && !_stateManager.IsAiming && !_stateManager.IsDrinking && !_stateManager.IsDodging
            && _input.Player.Move.ReadValue<Vector2>().sqrMagnitude > MOVE_INPUT_THRESHOLD_SQR;

        /// <summary>
        /// Pays the jump cost when aggroed. Returns false (nothing spent) when stamina is short; true when paid or free.
        /// Call after the state gate (<see cref="PlayerStateManager.CanJump"/>) and the jump input passed.
        /// </summary>
        public bool TryPayJump()
        {
            if (!enabled || _staminaSystem == null || _stateManager == null || !_stateManager.IsAggroed) return true;
            float cost = JumpStaminaCost;
            if (cost <= 0f) return true;
            if (!_staminaSystem.HasEnough(cost))
            {
                GameLog.Info(TAG, $"Jump refused: needs {cost:F0} stamina while aggroed");
                return false;
            }
            return _staminaSystem.Consume(cost);
        }
    }
}
