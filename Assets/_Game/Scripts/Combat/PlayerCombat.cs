using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles player combat input and enforces stamina gating.
    /// Story 2.2: Stamina gate.
    /// Story 2.3: Timed 3-hit combo system (replaces directional attacks).
    /// Story 2.4 will add Block input and gate.
    /// Story 2.6 will add Dodge input and gate.
    /// Attach to the Player prefab root alongside StaminaSystem.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    public class PlayerCombat : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        // Trigger hashes — precomputed once, never per-frame (performance rule)
        private static readonly int Attack1Hash = Animator.StringToHash("Attack_1");
        private static readonly int Attack2Hash = Animator.StringToHash("Attack_2");
        private static readonly int Attack3Hash = Animator.StringToHash("Attack_3");

        [SerializeField] private CombatConfigSO _config;

        private StaminaSystem _staminaSystem;
        private Animator _animator;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;            // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;
        private float _comboWindowDelay = 0f;  // counts down before window opens
        private float _comboWindowTimer = 0f;  // counts down while window is open

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

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                GameLog.Error(TAG, "Animator not found on Player — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _staminaSystem == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Attack.started += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            // Phase 1: delay before window opens
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
                    GameLog.Info(TAG, "Combo window expired — chain reset to step 0");
                }
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void TryAttack()
        {
            // If window is not open, start the chain fresh (also cancels any pending delay)
            if (!_comboWindowOpen)
            {
                _comboWindowDelay = 0f;
                _comboStep = 0;
            }

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, $"Cannot attack: insufficient stamina (combo step {_comboStep})");
                _comboWindowOpen = false;
                _comboWindowDelay = 0f;
                _comboStep = 0;
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                return;
            }

            int triggerHash = _comboStep switch
            {
                0 => Attack1Hash,
                1 => Attack2Hash,
                _ => Attack3Hash,
            };

            _animator.SetTrigger(triggerHash);
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
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 400, 26), $"Combat: [{state}]", style);
            string windowState = _comboWindowDelay > 0f
                ? $"opening in {_comboWindowDelay:F2}s"
                : _comboWindowOpen ? $"OPEN ({_comboWindowTimer:F2}s)" : "closed";
            GUI.Label(new Rect(10, 100, 400, 26), $"Combo: step {_comboStep} | {windowState}", style);
        }
#endif
    }
}
