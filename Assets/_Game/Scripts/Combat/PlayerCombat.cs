using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>Result of a hit attempt against the player.</summary>
    public enum HitResult { PerfectBlock, Blocked, NotBlocked, Dodged }

    /// <summary>
    /// Handles player combat input and enforces stamina gating.
    /// Story 2.2: Stamina gate.
    /// Story 2.3: Timed 3-hit combo system.
    /// Story 2.4: Manual block input and gate.
    /// Story 2.5: Perfect block timing window and TryReceiveHit API.
    /// Story 2.6: PlayerStateManager integration; airborne gates; attacking state tracking.
    /// Story 2.6 (refactor): Direct Animator calls removed — all animation driven via PlayerStateManager setters.
    /// Attach to the Player prefab root alongside StaminaSystem and PlayerStateManager.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerCombat : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        // Trigger hashes — passed to PlayerStateManager.SetAttacking(true, hash) to fire the animation
        private static readonly int Attack1Hash = Animator.StringToHash("Attack_1");
        private static readonly int Attack2Hash = Animator.StringToHash("Attack_2");
        private static readonly int Attack3Hash = Animator.StringToHash("Attack_3");

        [SerializeField] private CombatConfigSO _config;

        private StaminaSystem _staminaSystem;
        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;            // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;
        private float _comboWindowDelay = 0f;  // counts down before window opens
        private float _comboWindowTimer = 0f;  // counts down while window is open

        // Perfect block timing state (owned by PlayerCombat, not PlayerStateManager)
        private bool _isPerfectBlockWindowOpen = false;
        private float _perfectBlockWindowTimer = 0f;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
            {
                GameLog.Error(TAG, "StaminaSystem not found on Player — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found on Player — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Attack.started += OnAttackStarted;
            _input.Player.Block.started += OnBlockStarted;
            _input.Player.Block.canceled += OnBlockCanceled;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Block.started -= OnBlockStarted;
            _input.Player.Block.canceled -= OnBlockCanceled;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            // Phase 3: Perfect block window countdown — independent of combo state, runs every frame
            if (_isPerfectBlockWindowOpen && _perfectBlockWindowTimer > 0f)
            {
                _perfectBlockWindowTimer -= Time.deltaTime;
                if (_perfectBlockWindowTimer <= 0f)
                {
                    _isPerfectBlockWindowOpen = false;
                    GameLog.Info(TAG, "Perfect block window closed — regular block mode");
                }
            }

            // Phase 1: delay before combo window opens
            if (_comboWindowDelay > 0f)
            {
                _comboWindowDelay -= Time.deltaTime;
                if (_comboWindowDelay <= 0f)
                {
                    _comboWindowOpen = true;
                    _comboWindowTimer = _config.comboWindowDuration;
                    GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
                }
                return;
            }

            // Phase 2: window is open, count down
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_comboWindowTimer <= 0f)
                {
                    _comboWindowOpen = false;
                    _comboStep = 0;
                    _stateManager.SetAttacking(false);
                    GameLog.Info(TAG, "Combo window expired — chain reset to step 0");
                }
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void OnBlockStarted(InputAction.CallbackContext ctx)
        {
            // Airborne gate — highest priority
            if (_stateManager.IsAirborne)
            {
                GameLog.Warn(TAG, "Cannot block while airborne");
                return;
            }

            if (!_staminaSystem.HasEnough(_config.blockStaminaCostPerHit))
            {
                GameLog.Warn(TAG, "Cannot block: insufficient stamina");
                return;
            }

            _stateManager.SetBlocking(true); // also drives animator IsBlocking bool

            // Open perfect block window
            _isPerfectBlockWindowOpen = true;
            _perfectBlockWindowTimer = _config.perfectBlockWindowDuration;
            GameLog.Info(TAG, $"Perfect block window opened ({_config.perfectBlockWindowDuration:F2}s)");

            // Reset any in-progress combo — cannot combo mid-block
            _comboWindowOpen = false;
            _comboWindowDelay = 0f;
            _comboWindowTimer = 0f;
            _comboStep = 0;
            _stateManager.SetAttacking(false);

            GameLog.Info(TAG, "Block raised");
        }

        private void OnBlockCanceled(InputAction.CallbackContext ctx)
        {
            if (!_stateManager.IsBlocking) return; // Block was never raised (stamina denied entry)
            _stateManager.SetBlocking(false); // also drives animator IsBlocking bool
            _isPerfectBlockWindowOpen = false;
            _perfectBlockWindowTimer = 0f;
            GameLog.Info(TAG, "Block lowered");
        }

        private void TryAttack()
        {
            // Airborne gate — highest priority; airborne cancels everything
            if (_stateManager.IsAirborne)
            {
                GameLog.Warn(TAG, "Cannot attack while airborne");
                return;
            }

            // Cannot attack while blocking
            if (_stateManager.IsBlocking)
            {
                GameLog.Warn(TAG, "Cannot attack while blocking");
                return;
            }

            // Ignore input while combo window is warming up (prevents double-click re-triggering Attack_1)
            if (_comboWindowDelay > 0f)
            {
                GameLog.Info(TAG, "Attack input ignored — combo window not yet open");
                return;
            }

            // If window is not open (and no delay pending), start the chain fresh
            if (!_comboWindowOpen)
            {
                _comboStep = 0;
            }

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, $"Cannot attack: insufficient stamina (combo step {_comboStep})");
                _comboWindowOpen = false;
                _comboWindowDelay = 0f;
                _comboStep = 0;
                _stateManager.SetAttacking(false);
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                _stateManager.SetAttacking(false);
                return;
            }

            int triggerHash = _comboStep switch
            {
                0 => Attack1Hash,
                1 => Attack2Hash,
                _ => Attack3Hash,
            };

            // SetAttacking(true, triggerHash) sets state and fires the animator trigger atomically
            _stateManager.SetAttacking(true, triggerHash);
            GameLog.Info(TAG, $"Attack combo step {_comboStep + 1}");

            if (_comboStep < 2)
            {
                _comboStep++;
                _comboWindowOpen = false;  // window not open yet — delay starts
                _comboWindowDelay = _config.comboWindowDelay;
                _comboWindowTimer = 0f;
            }
            else
            {
                // Finisher fired — reset combo
                _comboStep = 0;
                _comboWindowOpen = false;
                _comboWindowDelay = 0f;
                _comboWindowTimer = 0f;
                _stateManager.SetAttacking(false);
            }
        }

        /// <summary>
        /// Called by enemy attack code when a hit connects with the player.
        /// Returns PerfectBlock (no stamina cost, stagger attacker), Blocked (stamina consumed),
        /// or NotBlocked (apply full damage). Story 2.7 implements the caller.
        /// </summary>
        /// <param name="attacker">The enemy GameObject that landed the hit.
        /// Reserved for Story 2.7: EnemyBrain reads PerfectBlock result to enter Stagger state.</param>
        public HitResult TryReceiveHit(GameObject attacker)
        {
            if (_stateManager.IsDodging)
            {
                GameLog.Info(TAG, "Dodge i-frame — hit missed");
                return HitResult.Dodged;
            }

            if (!_stateManager.IsBlocking)
            {
                GameLog.Info(TAG, "Hit received — not blocking");
                return HitResult.NotBlocked;
            }

            if (_isPerfectBlockWindowOpen)
            {
                _isPerfectBlockWindowOpen = false;
                _perfectBlockWindowTimer = 0f;
                GameLog.Info(TAG, "PERFECT BLOCK — no stamina cost, attacker staggers");
                return HitResult.PerfectBlock;
            }

            // Regular block — consume stamina per hit
            bool consumed = _staminaSystem.Consume(_config.blockStaminaCostPerHit);
            if (!consumed)
            {
                // Block broken by hit — stamina exhausted; SetBlocking also clears the animator bool
                _stateManager.SetBlocking(false);
                _isPerfectBlockWindowOpen = false;
                _perfectBlockWindowTimer = 0f;
                GameLog.Warn(TAG, "Block broken by hit — stamina depleted");
                return HitResult.NotBlocked;
            }

            GameLog.Info(TAG, "Block absorbed hit — stamina consumed");
            return HitResult.Blocked;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 400, 26), $"Combat: [{state}]", style);
            string windowState = _comboWindowDelay > 0f
                ? $"opening in {_comboWindowDelay:F2}s"
                : _comboWindowOpen ? $"OPEN ({_comboWindowTimer:F2}s)" : "closed";
            GUI.Label(new Rect(10, 100, 400, 26), $"Combo: step {_comboStep} | {windowState}", style);
            string pbWindow = _isPerfectBlockWindowOpen ? $"PB: {_perfectBlockWindowTimer:F2}s" : "PB: closed";
            GUI.Label(new Rect(10, 130, 400, 26), $"Block: {(_stateManager.IsBlocking ? "RAISED" : "lowered")} | {pbWindow}", style);
            GUI.Label(new Rect(10, 160, 500, 26),
                $"State: Airborne:{_stateManager.IsAirborne} | Blocking:{_stateManager.IsBlocking} | Attacking:{_stateManager.IsAttacking}",
                style);
        }
#endif
    }
}
