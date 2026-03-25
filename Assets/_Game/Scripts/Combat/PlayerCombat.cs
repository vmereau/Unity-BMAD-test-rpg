using Game.AI;
using Game.Core;
using Game.Inventory;
using Game.Player;
using Game.Progression;
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
    /// Story 2.9: ExecuteHitDetection() added — sphere overlap on attack to apply damage to EnemyHealth components.
    /// Story 3.6: ComputeEffectiveDamage() — Strength bonus and Power Strike skill effect applied to damage.
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

        // TODO(Epic4-tech-debt): Cross-system direct refs (Game.Combat → Game.Player/Game.Progression).
        // Prototype exception per Story 3.6 dev notes. Replace with a StatBonusProvider/SkillEffectRegistry event channel.
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private ProgressionConfigSO _progressionConfig;
        [SerializeField] private PlayerSkills _playerSkills;

        // TODO(Epic7-tech-debt): Cross-system direct ref (Game.Combat → Game.Inventory).
        // Prototype exception per Story 7.3 dev notes. Same pattern as existing _playerStats ref.
        [SerializeField] private EquipmentSystem _equipmentSystem;

        // Story 7.9: Weapon hitbox binding — direct ref acceptable (prototype exception, same boundary as _equipmentSystem)
        [SerializeField] private EquipmentVisuals _equipmentVisuals;
        [SerializeField] private GameEventSO_Void _onVisualsRefreshed;

        [SerializeField] private GameObject _unarmedHitbox;

        private StaminaSystem _staminaSystem;
        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;            // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;
        private bool _IsComboAttacking = false; // true from ExecuteAttack until SMB OnStateEnter — combo chain guard

        // Story 7.10: cached weapon SO — set when visuals refresh, cleared on unequip
        private WeaponSO _currentWeaponSO;

        // Perfect block timing state (owned by PlayerCombat, not PlayerStateManager)
        private bool _isPerfectBlockWindowOpen = false;
        private float _perfectBlockWindowTimer = 0f;

        // Hit detection — pre-allocated buffer; reused per attack to avoid per-frame allocation
        private readonly Collider[] _hitBuffer = new Collider[10];

        private WeaponHitbox _activeHitbox;

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

            if (_playerStats == null)
                GameLog.Warn(TAG, "PlayerStats not assigned — Strength damage bonus inactive");
            if (_progressionConfig == null)
                GameLog.Warn(TAG, "ProgressionConfigSO not assigned — all stat bonuses inactive");
            if (_playerSkills == null)
                GameLog.Warn(TAG, "PlayerSkills not assigned — Power Strike bonus inactive");
            if (_equipmentSystem == null)
                GameLog.Warn(TAG, "EquipmentSystem not assigned — weapon damage bonus inactive");
            if (_equipmentVisuals == null)
                GameLog.Warn(TAG, "EquipmentVisuals not assigned — draw/sheathe will have no visual effect");

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
            _input.Player.DrawWeapon.started += OnDrawWeaponStarted;
            // Story 7.9: subscribe to OnVisualsRefreshed SO — raised at the END of
            // EquipmentVisuals.Refresh(), so ActiveWeaponGO is valid when HandleVisualsRefreshed runs.
            _onVisualsRefreshed?.AddListener(HandleVisualsRefreshed);
            BindUnarmedHitbox();
        }

        private void OnDisable()
        {
            // Story 7.9: clean up hitbox binding before input
            _onVisualsRefreshed?.RemoveListener(HandleVisualsRefreshed);
            UnbindWeaponHitbox();
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Block.started -= OnBlockStarted;
            _input.Player.Block.canceled -= OnBlockCanceled;
            _input.Player.DrawWeapon.started -= OnDrawWeaponStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            DrawAttackRangeDebug();
#endif
            // Perfect block window countdown — independent of combo state, runs every frame
            if (_isPerfectBlockWindowOpen && _perfectBlockWindowTimer > 0f)
            {
                _perfectBlockWindowTimer -= Time.deltaTime;
                if (_perfectBlockWindowTimer <= 0f)
                {
                    _isPerfectBlockWindowOpen = false;
                    GameLog.Info(TAG, "Perfect block window closed — regular block mode");
                }
            }
            // Story 7.10: Combo window open/close is now driven by Animation Events via
            // AnimationEventReceiver → OnComboWindowOpen() / OnComboWindowClose().
            // No timer phases needed here.
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)
        {
            if (_stateManager.IsBusy) return;
            if (_stateManager.IsAttacking) return;
            bool entering = !_stateManager.IsInCombat;
            _stateManager.SetInCombat(entering);
            _equipmentVisuals?.SetCombatState(entering);
        }

        private void OnBlockStarted(InputAction.CallbackContext ctx)
        {
            if (!_stateManager.CanBlock())
            {
                GameLog.Warn(TAG, "Cannot block — airborne, dodging, or weapon sheathed");
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
            ResetAttackCombo();

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
            if (!_stateManager.CanAttack())
            {
                GameLog.Warn(TAG, "Cannot attack — airborne or blocking");
                return;
            }

            if (IsMaxCombo())
            {
                GameLog.Info(TAG, "Attack input ignored — Max combo");
                return;
            }

            // Story 7.10: If we're attacking but the window has not opened yet (AnimationEvent
            // ComboWindowOpen hasn't fired), ignore new input to prevent re-triggering Attack_1.
            // IsAttacking is true from SetAttacking(true) until SetAttacking(false) or animation exit.
            if (_stateManager.IsAttacking && !_comboWindowOpen)
            {
                GameLog.Info(TAG, "Attack input ignored — waiting for combo window (animation event)");
                return;
            }

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, $"Cannot attack: insufficient stamina (combo step {_comboStep})");
                ResetAttackCombo();
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                ExitAttack();
                return;
            }

            ExecuteAttack();
            ManageComboStep();
        }

        private void ExecuteAttack()
        {
            int triggerHash = _comboStep switch
            {
                0 => Attack1Hash,
                1 => Attack2Hash,
                _ => Attack3Hash,
            };
            
            // SetAttacking(true, triggerHash) sets state and fires the animator trigger atomically
            _stateManager.SetAttacking(true, triggerHash);

            // Story 7.11: hitbox enabled/disabled by HitboxEnable/HitboxDisable animation events.
            // Unarmed fallback (no hitbox): sphere overlap fires immediately on input frame.
            if (_activeHitbox == null)
                GameLog.Warn(TAG, $"Warning: No active hitbox");
        }

        private bool IsMaxCombo()
        {
            // Story 7.10: query comboSteps from equipped weapon SO; unarmed defaults to 2
            int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 2;
            return _comboStep == maxSteps;
        }

        private void ManageComboStep()
        {
            if (IsMaxCombo())
            {
                return;
            }
            
            IncreaseAttackCombo();
            if (_comboStep > 1)
            {
                _IsComboAttacking = true;
            }
        }

        private void IncreaseAttackCombo()
        {
            _comboStep++;
            _comboWindowOpen = false;
            // Story 7.10: window timing is now driven by ComboWindowOpen animation event
        }

        private void ResetAttackCombo()
        {
            _comboStep = 0;
            _comboWindowOpen = false;
        }

        private void ExitAttack()
        {
            _stateManager.SetAttacking(false);
            _activeHitbox?.Disable();
        }

        // Story 7.9: weapon hitbox helpers -------------------------------------------

        // Called by OnVisualsRefreshed GameEventSO — raised at the END of EquipmentVisuals.Refresh(),
        // guaranteeing ActiveWeaponGO is already set before we try to bind.
        private void HandleVisualsRefreshed(bool _)
        {
            UnbindWeaponHitbox();
            // Story 7.10: cache weapon SO for comboSteps query
            BindWeaponHitbox();
        }

        private void BindWeaponHitbox()
        {
            _currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;
            if (_equipmentVisuals == null) return;
            var weaponGO = _equipmentVisuals.ActiveWeaponGO;
            if (weaponGO == null)
            {
                BindUnarmedHitbox(); // Unarmed — no weapon visual, bind unarmed hitbox
                return;
            }
            // includeInactive: true — Drawn child may be inactive when weapon is equipped while sheathed
            _activeHitbox = weaponGO.GetComponentInChildren<WeaponHitbox>(true);
            if (_activeHitbox != null)
                _activeHitbox.OnEnemyHit += OnWeaponHit;
            else
                BindUnarmedHitbox(); // Weapon visual exists but has no WeaponHitbox — fallback
        }

        private void BindUnarmedHitbox()
        {
            _unarmedHitbox.SetActive(true);
            _activeHitbox = _unarmedHitbox.GetComponent<WeaponHitbox>();
            if (_activeHitbox != null)
                _activeHitbox.OnEnemyHit += OnWeaponHit;
        }

        private void UnbindWeaponHitbox()
        {
            _currentWeaponSO = null; // Story 7.10: clear cached weapon SO on unbind
            _unarmedHitbox?.SetActive(false);
            if (_activeHitbox == null) return;
            _activeHitbox.Disable();
            _activeHitbox.OnEnemyHit -= OnWeaponHit;
            _activeHitbox = null;
        }

        private void OnWeaponHit(EnemyHealth health)
        {
            health.TakeDamage(ComputeEffectiveDamage());
            GameLog.Info(TAG, $"Weapon hit: {health.gameObject.name}");
        }

        // Story 7.10: public methods called by AnimationEventReceiver ----------------

        public void OnComboWindowOpen()
        {
            // Guard: finisher calls ResetAttackCombo() (SetAttacking(false)) before the animation
            // completes. The stale ComboWindowOpen event at ~50% of the finisher clip must be
            // ignored, otherwise _comboWindowOpen becomes true while IsAttacking is false,
            // allowing TryAttack() to start a new chain mid-finisher animation.
            if (!_stateManager.IsAttacking) return;
            _comboWindowOpen = true;
            GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
        }

        public void OnComboWindowClose()
        {
            ResetAttackCombo();
            GameLog.Info(TAG, "Combo window closed");
        }

        public void OnHitboxEnable()
        {
            _activeHitbox?.Enable();
        }
        public void OnHitboxDisable() => _activeHitbox?.Disable();  // Story 7.11

        /// <summary>
        /// Called by SMB_AttackState.OnStateEnter — animator confirmed this attack state started.
        /// Guarantees hitbox is disabled at state entry (safety net for back-to-back attacks
        /// where the exit cleanup was skipped because we transitioned directly to this state).
        /// Does NOT set IsAttacking — that is owned by input (ExecuteAttack) and the
        /// finisher path intentionally clears it via ResetAttackCombo before the clip ends.
        /// </summary>
        public void OnAttackStateEntered(int attackIndex)
        {
            // this attack state started — safe to allow exit cleanup again
            _activeHitbox?.Disable();
            _comboWindowOpen = false;
        }

        /// <summary>
        /// Called by SMB_AttackState.OnStateExit when NOT transitioning to another attack state.
        /// Guarantees hitbox off and combo state reset for interrupt cases (dodge/stagger/death)
        /// where ComboWindowClose animation event never fired.
        /// </summary>
        public void OnAttackStateExited()
        {
            if (_IsComboAttacking)
            {
                _IsComboAttacking = false;
                return;
            }

            ResetAttackCombo();
            ExitAttack();
        }

        // ----------------------------------------------------------------------------

        /// <summary>
        /// Computes effective attack damage: base damage + Strength bonus + Power Strike skill bonus.
        /// Called each time damage is applied or displayed. Lightweight — no allocations, safe to call per-frame.
        /// </summary>
        private float ComputeEffectiveDamage()
        {
            float damage = _config.attackDamage;
            if (_playerStats != null && _progressionConfig != null)
                // Clamp to 0 — negative Strength delta (future debuff/penalty system) must not reduce damage below base.
                damage += Mathf.Max(0f, (_playerStats.Strength - _progressionConfig.baseStrength)
                          * _progressionConfig.damagePerStrength);
            // Prototype: hardcoded skill ID "power_strike" defined in SkillSO (Story 3.5).
            // Full system would query skill effects generically via a SkillEffectRegistry.
            if (_playerSkills != null && _progressionConfig != null
                && _playerSkills.HasSkill("power_strike"))
                damage += _progressionConfig.powerStrikeDamageBonus;
            // Story 7.3: direct weapon damage bonus (flat, unscaled by stats).
            damage += _equipmentSystem?.GetWeaponDamageBonus() ?? 0f;
            return damage;
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
        private GUIStyle _guiStyle;

        private void DrawAttackRangeDebug()
        {
            if (_config == null) return;
            const int segments = 32;
            float r = _config.attackHitRange;
            Vector3 center = transform.position;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i * Mathf.PI * 2f / segments;
                float a2 = (i + 1) * Mathf.PI * 2f / segments;
                Debug.DrawLine(
                    center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r),
                    center + new Vector3(Mathf.Cos(a2) * r, 0f, Mathf.Sin(a2) * r),
                    Color.yellow);
            }
        }

        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            var style = _guiStyle;
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 400, 26), $"Combat: [{state}]", style);
            string windowState = _comboWindowOpen ? "OPEN (animation-event)" : "closed";
            GUI.Label(new Rect(10, 100, 400, 26), $"Combo: step {_comboStep} | {windowState}", style);
            string pbWindow = _isPerfectBlockWindowOpen ? $"PB: {_perfectBlockWindowTimer:F2}s" : "PB: closed";
            GUI.Label(new Rect(10, 130, 400, 26), $"Block: {(_stateManager.IsBlocking ? "RAISED" : "lowered")} | {pbWindow}", style);
            GUI.Label(new Rect(10, 160, 500, 26),
                $"State: Airborne:{_stateManager.IsAirborne} | Blocking:{_stateManager.IsBlocking} | Attacking:{_stateManager.IsAttacking} | InCombat:{_stateManager.IsInCombat}",
                style);
            GUI.Label(new Rect(10, 410, 300, 26), $"DMG: {ComputeEffectiveDamage():F1}", style);
        }
#endif
    }
}
