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
    /// Story 2.9: ExecuteHitDetection() added — sphere overlap on attack to apply damage to EntityHealth components.
    /// Story 3.6: ComputeEffectiveDamage() — Strength bonus and Power Strike skill effect applied to damage.
    /// Attach to the Player prefab root alongside StaminaSystem and PlayerStateManager.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(PlayerStateManager))]
    public class PlayerCombat : MonoBehaviour
    {
        private const string TAG = "[Combat]";
        private const string POWER_STRIKE_SKILL_ID = "power_strike"; // SkillSO id (Story 3.5)

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

        // TODO(Epic7-tech-debt): Cross-system direct ref (Game.Combat → Game.Inventory) — bow ammo is consumed from the
        // player inventory. Same prototype exception as _equipmentSystem.
        [SerializeField] private InventorySystem _inventory;
        [Tooltip("Toast channel for combat feedback (e.g. \"Out of arrows\").")]
        [SerializeField] private GameEventSO_String _onCombatNotification;

        private StaminaSystem _staminaSystem;
        private PlayerStateManager _stateManager;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;            // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;

        // Attack lifecycle as the Animator plays it (active attack states, queued step, stall watchdog)
        private readonly PlayerAttackTracker _attackTracker = new();

        // Story 7.10: cached weapon SO — set when visuals refresh, cleared on unequip
        private WeaponSO _currentWeaponSO;

        // Perfect block timing state (owned by PlayerCombat, not PlayerStateManager)
        private bool _isPerfectBlockWindowOpen = false;
        private float _perfectBlockWindowTimer = 0f;

        // Hit detection — pre-allocated buffer; reused per attack to avoid per-frame allocation
        private readonly Collider[] _hitBuffer = new Collider[10];

        // Bow aiming / firing
        private const string OUT_OF_AMMO_MESSAGE = "Out of arrows";
        private const float FALLBACK_LAUNCH_HEIGHT = 1.4f; // chest height when no bow visual / camera exists
        private const float MIN_AIM_DIRECTION_SQR = 0.0001f;
        private readonly RaycastHit[] _aimBuffer = new RaycastHit[32];
        private int _aimLayers;
        private int _hurtboxLayer;
        private Camera _cachedCamera;     // crosshair ray (cached — Camera.main is a lookup; re-resolved if destroyed)
        private float _nextBowShotTime;   // fire cooldown
        // Bow aim (PlayerStateManager.IsAiming): a shot needs the arrow nocked — set when BowAim_State is entered
        // (SMB_BowAimState), cleared by a shot (Fire arrow → Draw arrow play before the next one) and on aim start.
        private bool _arrowNocked;
        private float _nockWaitStartTime; // nock watchdog
        private bool _nockForced;         // nocked by the watchdog, not by the aim state
        // Aim spread: 0 = full cone (aimSpreadMaxAngle), 1 = perfect shot. Shrinks only while nocked and still, widens
        // back while moving; reset on aim start and after every shot.
        private float _aimProgress;
        private CharacterController _characterController; // movement check for the spread (optional)

        private WeaponHitbox _activeHitbox;

        // Draw / sheathe in flight (PlayerStateManager.IsSwitchingWeapon): direction and start time for the watchdog
        private bool _pendingDraw;
        private float _weaponSwitchStartTime;

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
            if (_unarmedHitbox == null)
                GameLog.Warn(TAG, "Unarmed hitbox not assigned — unarmed attacks cannot hit");
            if (_inventory == null)
                GameLog.Warn(TAG, "InventorySystem not assigned — bows cannot fire");
            if (_onCombatNotification == null)
                GameLog.Warn(TAG, "_onCombatNotification not assigned — 'Out of arrows' will only be logged");

            _aimLayers = LayerMask.GetMask(GameConstants.DEFAULT_LAYER_NAME, GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _hurtboxLayer = LayerMask.NameToLayer(GameConstants.CHARACTER_HITBOX_LAYER_NAME);
            _cachedCamera = Camera.main;
            if (_cachedCamera == null)
                GameLog.Warn(TAG, "Camera.main not found — bows aim along the player's forward");

            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
                GameLog.Warn(TAG, "CharacterController not found — moving won't widen the bow aim spread");

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
            CompleteWeaponSwitch(); // no Update watchdog while disabled
            StopAim();
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
#if false // DISABLED: debug OnGUI — to be reworked
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

            // Watchdog: an attack trigger the Animator never took (or a lost OnStateExit) leaves IsAttacking on
            // with no callback ever arriving — force the end instead of locking every further attack.
            if (_attackTracker.IsStalled(_stateManager.IsAttacking, Time.time, _config.attackStallTimeout))
            {
                GameLog.Warn(TAG, $"Attack stalled ({_config.attackStallTimeout:F2}s without attack state or event) — forcing end");
                _attackTracker.Reset();
                EndAttack();
            }

            UpdateBowAim();

            if (_stateManager.IsSwitchingWeapon)
            {
                // A dodge / jump taken on the same frame as R wins the Attack layer's AnyState order: Draw_State is
                // never entered (no exit net) — finish the swap now instead of replaying the clip after landing.
                if (_stateManager.IsDodging || _stateManager.IsAirborne)
                {
                    CompleteWeaponSwitch();
                }
                // Watchdog: a draw / sheathe whose WeaponSwap event and state exit never arrived (trigger not taken,
                // controller swapped mid-clip) would lock R, Attack and Block forever. Scaled with the clip speed.
                else
                {
                    float timeout = _config.weaponSwitchTimeout / _config.weaponSwitchAnimSpeed;
                    if (Time.time - _weaponSwitchStartTime > timeout)
                    {
                        GameLog.Warn(TAG, $"Weapon switch stalled ({timeout:F2}s without WeaponSwap event or state exit) — forcing swap");
                        CompleteWeaponSwitch();
                    }
                }
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        // R: flips IsInCombat now and plays the draw / sheathe clip; the weapon visual moves at the clip's WeaponSwap
        // event (or the state-exit / watchdog safety nets) — see CompleteWeaponSwitch.
        private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)
        {
            // R ends a bow aim, then draws / sheathes (the switch gate itself refuses while aiming).
            StopAim();
            if (!_stateManager.CanSwitchWeapon()) return;
            bool entering = !_stateManager.IsInCombat;
            _pendingDraw = entering;
            _weaponSwitchStartTime = Time.time;
            _stateManager.SetInCombat(entering);
            _stateManager.BeginWeaponSwitch(entering, _config.weaponSwitchAnimSpeed);
        }

        private void OnBlockStarted(InputAction.CallbackContext ctx)
        {
            // A bow can't block: the Block input (right click) is hold-to-aim instead.
            if (EquippedBow != null)
            {
                StartAim();
                return;
            }

            if (!_stateManager.CanBlock())
            {
                GameLog.Warn(TAG, "Cannot block — airborne, dodging, drawing / sheathing, or weapon sheathed");
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
            StopAim(); // no-op unless aiming; a block raised before a bow was equipped still lowers below
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
                GameLog.Warn(TAG, "Cannot attack — airborne, blocking or drawing / sheathing");
                return;
            }

            // Ranged weapon: no combo, no melee hitbox. A shot needs the aim held and the arrow nocked (no hip-fire).
            BowSO bow = EquippedBow;
            if (bow != null)
            {
                // A melee swing still playing (weapon swapped mid-combo) finishes first.
                if (_stateManager.IsAttacking) return;
                if (!_stateManager.IsAiming || !_arrowNocked) return;
                if (!TryFireBow(bow)) return; // refused shot: nothing spent, arrow stays nocked
                _arrowNocked = false;
                _aimProgress = 0f;
                _nockWaitStartTime = Time.time;
                // Fire arrow → Draw arrow → BowAim_State re-nocks. A watchdog-forced nock means the layer is not in
                // BowAim_State: a BowFire set now would latch and replay the release (and re-nock) on its next enter.
                if (!_nockForced) _stateManager.PlayBowFire();
                _nockForced = false;
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
                EndAttack();
                return;
            }

            ExecuteAttack();
            ManageComboStep();
            _attackTracker.Press(Time.time, isChainStep: _comboStep > 1);
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
            // No hitbox bound (e.g. _unarmedHitbox unassigned) — this attack cannot deal damage.
            if (_activeHitbox == null)
                GameLog.Warn(TAG, $"Warning: No active hitbox");
        }

        private bool IsMaxCombo()
        {
            // Story 7.10: query combo steps from equipped weapon SO (weapon > 0 overrides archetype); unarmed uses the config
            int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.ResolvedComboSteps : _config.unarmedComboSteps;
            return _comboStep == maxSteps;
        }

        private void ManageComboStep()
        {
            if (IsMaxCombo())
            {
                return;
            }
            
            IncreaseAttackCombo();
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
            _stateManager.SetAttacking(false); // also clears pending Attack_1/2/3 triggers
            _activeHitbox?.Disable();
        }

        // Full end of an attack: combo reset, IsAttacking off, hitbox closed, queued step forgotten.
        private void EndAttack()
        {
            _attackTracker.ClearQueue();
            ResetAttackCombo();
            ExitAttack();
        }

        /// <summary>
        /// Cancels any attack in progress (dodge). The attack states still exit on their own through the SMB;
        /// those exits are then harmless no-ops.
        /// </summary>
        public void CancelAttack()
        {
            if (!_stateManager.IsAttacking && _comboStep == 0) return;
            EndAttack();
        }

        /// <summary>
        /// Player stance reaction started (PlayerStance): ends the attack and combo, lowers the block (perfect-block
        /// window closed) and ends a bow aim. A held right click does not raise the block again.
        /// </summary>
        public void InterruptForStance()
        {
            if (_stateManager == null) return;
            CancelAttack();
            StopAim();
            if (!_stateManager.IsBlocking) return;
            _stateManager.SetBlocking(false);
            _isPerfectBlockWindowOpen = false;
            _perfectBlockWindowTimer = 0f;
        }

        // Story 7.9: weapon hitbox helpers -------------------------------------------

        // Called by OnVisualsRefreshed GameEventSO — raised at the END of EquipmentVisuals.Refresh(),
        // guaranteeing ActiveWeaponGO is already set before we try to bind.
        private void HandleVisualsRefreshed(bool _)
        {
            // Equipment changed mid draw / sheathe: the new visual (and animator override) replace the clip in flight.
            CompleteWeaponSwitch();
            // The controller reset below drops the aim states; the player presses aim again.
            StopAim();
            // Refresh() reassigns runtimeAnimatorController, which resets every animator parameter — re-push the
            // stance, or a drawn player stays in LockOn Locomotion and Attack_1 (only taken from CombatIdle) latches.
            if (_stateManager != null) _stateManager.SetInCombat(_stateManager.IsInCombat);
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
            {
                _activeHitbox.SetOwner(transform);
                _activeHitbox.OnHit += OnWeaponHit;
            }
            else
                BindUnarmedHitbox(); // Weapon visual exists but has no WeaponHitbox — fallback
        }

        private void BindUnarmedHitbox()
        {
            if (_unarmedHitbox == null)
            {
                GameLog.Warn(TAG, "Unarmed hitbox not assigned — unarmed attacks cannot hit");
                _activeHitbox = null;
                return;
            }
            _unarmedHitbox.SetActive(true);
            _activeHitbox = _unarmedHitbox.GetComponent<WeaponHitbox>();
            if (_activeHitbox != null)
            {
                _activeHitbox.SetOwner(transform);
                _activeHitbox.OnHit += OnWeaponHit;
            }
        }

        private void UnbindWeaponHitbox()
        {
            _currentWeaponSO = null; // Story 7.10: clear cached weapon SO on unbind
            _unarmedHitbox?.SetActive(false);
            if (_activeHitbox == null) return;
            _activeHitbox.Disable();
            _activeHitbox.OnHit -= OnWeaponHit;
            _activeHitbox = null;
        }

        private void OnWeaponHit(IDamageable target, Vector3 hitPoint)
        {
            ResolveHit(target, hitPoint, ComputeEffectiveDamage(), ComputeMeleeStanceDamage(), MeleeCanKnockDown());
        }

        // Mirrors EntityBrain.ExecuteAttack: TryReceiveHit first, damage only when NotBlocked.
        // Shared by melee hitboxes and arrows (arrow damage is snapshotted at fire time).
        private void ResolveHit(IDamageable target, Vector3 hitPoint, float damage, float stanceDamage, bool canKnockDown)
        {
            if (this == null) return; // arrow landing after the player was destroyed
            if (target == null || target.IsDead) return;
            HitResult result = target.TryReceiveHit(gameObject);
            if (result != HitResult.NotBlocked)
            {
                GameLog.Info(TAG, $"Weapon hit {result} at {hitPoint}");
                return;
            }
            // Sneak attack: an entity that hasn't noticed the player takes bonus damage. The hit itself alerts it
            // (engage, or a damage-alert window for passive / neutral entities), so only the opening hit gets it.
            if (target is Component targetComponent &&
                targetComponent.TryGetComponent(out ISneakAttackTarget sneakTarget))
            {
                if (sneakTarget.IsUnawareOf(gameObject))
                {
                    damage *= _config.sneakAttackDamageMultiplier;
                    GameLog.Info(TAG, $"Sneak attack x{_config.sneakAttackDamageMultiplier}");
                }
                sneakTarget.NotifyHitBy(gameObject);
            }
            target.TakeDamage(damage);
            GameLog.Info(TAG, $"Weapon hit landed at {hitPoint} ({damage} dmg)");
            // Stance after health: the damage reaction acquires the attacker first; a lethal hit skips stance.
            if (!target.IsDead && target is Component stanceComponent &&
                stanceComponent.TryGetComponent(out IStanceTarget stanceTarget))
                stanceTarget.ApplyStanceDamage(stanceDamage, canKnockDown);
        }

        // Bow ------------------------------------------------------------------------

        // Gate order: cooldown → stamina → ammo → consume stamina → consume ammo → fire. Nothing is spent on a
        // refused shot (ammo is checked before stamina is consumed, and consumed only after it succeeded).
        // Returns true when an arrow was fired.
        private bool TryFireBow(BowSO bow)
        {
            if (Time.time < _nextBowShotTime) return false;

            if (!bow.IsConfigured)
            {
                GameLog.Error(TAG, $"Bow '{bow.name}' has no ammoItem or projectilePrefab — cannot fire");
                return false;
            }
            if (_inventory == null)
            {
                GameLog.Error(TAG, "InventorySystem not assigned — bow cannot fire");
                return false;
            }

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, "Cannot fire: insufficient stamina");
                return false;
            }

            if (_inventory.CountOf(bow.ammoItem) <= 0)
            {
                GameLog.Info(TAG, "Cannot fire: out of ammo");
                _onCombatNotification?.Raise(OUT_OF_AMMO_MESSAGE);
                return false;
            }

            if (!_staminaSystem.Consume(_config.attackStaminaCost))
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                return false;
            }
            _inventory.TryConsume(bow.ammoItem);
            _nextBowShotTime = Time.time + bow.fireCooldown;
            FireArrow(bow);
            return true;
        }

        // Equipped bow — read from the equipment, not _currentWeaponSO (that cache is only set by OnVisualsRefreshed and
        // is null after a disable/enable). Null for melee / unarmed.
        private BowSO EquippedBow =>
            _equipmentSystem != null ? _equipmentSystem.GetEquipped(EquipmentSlot.Weapon) as BowSO : null;

        // Right click with a bow drawn: Draw arrow → Aim Overdraw (loop); the arrow is nocked at BowAim_State enter.
        private void StartAim()
        {
            if (_stateManager.IsAiming || !_stateManager.CanAim()) return;
            _arrowNocked = false;
            _nockForced = false;
            _aimProgress = 0f;
            _nockWaitStartTime = Time.time;
            _stateManager.SetAiming(true);
            GameLog.Info(TAG, "Bow aim started");
        }

        private void StopAim()
        {
            if (_stateManager == null || !_stateManager.IsAiming) return;
            _stateManager.SetAiming(false);
            _arrowNocked = false;
            GameLog.Info(TAG, "Bow aim ended");
        }

        // Any interrupt (dodge, jump, R, menu / dialogue, death, sheathed, bow unequipped) ends the aim for good: a
        // still-held right click does not resume it (Block.started only fires on a new press).
        private void UpdateBowAim()
        {
            if (!_stateManager.IsAiming) return;
            if (!_stateManager.CanAim() || EquippedBow == null)
            {
                StopAim();
                return;
            }
            // Watchdog: a lost BowAim_State enter (trigger not taken, controller swapped) would lock every shot.
            if (!_arrowNocked && Time.time - _nockWaitStartTime > _config.bowNockTimeout)
            {
                GameLog.Warn(TAG, $"Bow nock stalled ({_config.bowNockTimeout:F2}s without BowAim_State enter) — forcing nock");
                _arrowNocked = true;
                _nockForced = true;
            }
            // Spread: full until the nock, then shrinks while still / widens back while moving.
            if (_arrowNocked)
                _aimProgress = BowSpreadMath.StepProgress(_aimProgress, Time.deltaTime, _config.aimSpreadShrinkDuration,
                    IsMovingForSpread(), _config.aimMoveWidenMultiplier);
        }

        private bool IsMovingForSpread()
        {
            if (_characterController == null) return false;
            Vector3 velocity = _characterController.velocity;
            velocity.y = 0f;
            float threshold = _config.aimMoveSpeedThreshold;
            return velocity.sqrMagnitude > threshold * threshold;
        }

        // Crosshair camera, re-resolved after a reload (the camera may also appear after this Awake).
        private Camera _camera
        {
            get
            {
                if (_cachedCamera == null) _cachedCamera = Camera.main;
                return _cachedCamera;
            }
        }

        /// <summary>The camera the crosshair ray (and so the spread cone) is cast from; null when there is none.</summary>
        public Camera AimCamera => _camera;

        /// <summary>
        /// Current aim spread cone half-angle (degrees) for the HUD ring. False when not aiming a bow or without a
        /// camera (the ring is hidden).
        /// </summary>
        public bool TryGetAimSpread(out float angleDeg)
        {
            angleDeg = 0f;
            if (_config == null || _stateManager == null || !_stateManager.IsAiming || _camera == null) return false;
            if (EquippedBow == null) return false;
            angleDeg = BowSpreadMath.SpreadAngle(_arrowNocked ? _aimProgress : 0f, _config.aimSpreadMaxAngle);
            return true;
        }

        private void FireArrow(BowSO bow)
        {
            Vector3 origin = GetLaunchOrigin();
            // The shot goes through a uniformly random point of the current spread cone around the crosshair ray.
            float spread = BowSpreadMath.SpreadAngle(_aimProgress, _config.aimSpreadMaxAngle);
            Vector3 direction = GetAimPoint(bow.maxAimDistance, spread) - origin;
            if (direction.sqrMagnitude < MIN_AIM_DIRECTION_SQR) direction = transform.forward;
            if (spread > 0f)
            {
                // A sampled ray grazing a near edge the centre ray missed would bend the shot (bow → near point) far
                // beyond the cone: keep it within the spread of the bow → crosshair point direction.
                Vector3 centre = GetAimPoint(bow.maxAimDistance) - origin;
                if (centre.sqrMagnitude >= MIN_AIM_DIRECTION_SQR)
                    direction = BowSpreadMath.ClampToCone(centre, direction, spread);
            }

            GameObject go = Instantiate(bow.projectilePrefab, origin, Quaternion.LookRotation(direction));
            if (!go.TryGetComponent(out ArrowProjectile projectile))
            {
                GameLog.Error(TAG, $"Projectile prefab '{bow.projectilePrefab.name}' has no ArrowProjectile");
                Destroy(go);
                return;
            }

            float damage = ComputeRangedDamage(bow);
            float stanceDamage = bow.ResolvedStanceDamage; // snapshotted like damage; no Strength / finisher
            bool canKnockDown = bow.ResolvedCanKnockDown;
            projectile.Launch(transform, direction, bow.projectileSpeed, bow.projectileLifetime,
                (target, point) => ResolveHit(target, point, damage, stanceDamage, canKnockDown),
                bow.ammoItem.worldItemPrefab, bow.gravityScale, bow.gravityDelay, bow.gravityRampTime);
            GameLog.Info(TAG, $"Arrow fired ({damage} dmg, spread {spread:F1}°), {_inventory.CountOf(bow.ammoItem)} left");
        }

        // Equipped bow (hand socket while drawn); falls back to chest height in front of the player.
        private Vector3 GetLaunchOrigin()
        {
            GameObject weapon = _equipmentVisuals != null ? _equipmentVisuals.ActiveWeaponGO : null;
            return weapon != null ? weapon.transform.position : transform.position + Vector3.up * FALLBACK_LAUNCH_HEIGHT;
        }

        /// <summary>
        /// Crosshair aim point for the equipped bow (same ray as the shot). False when no bow is equipped or there is
        /// no camera (the fallback point follows the body facing, not the crosshair).
        /// </summary>
        public bool TryGetAimPoint(out Vector3 point)
        {
            BowSO bow = EquippedBow;
            if (bow == null || _camera == null)
            {
                point = default;
                return false;
            }
            point = GetAimPoint(bow.maxAimDistance);
            return true;
        }

        // Crosshair (viewport centre) ray, skipping the player's own colliders and everything between the camera and
        // the player (a wall behind the OTS camera, an NPC behind the player); nothing hit → max range.
        // spreadDeg > 0 → the ray goes through a uniformly random point of that cone around the crosshair ray.
        private Vector3 GetAimPoint(float maxDistance, float spreadDeg = 0f)
        {
            Camera cam = _camera;
            if (cam == null)
            {
                Vector3 forward = spreadDeg > 0f
                    ? BowSpreadMath.SampleCone(transform.forward, spreadDeg, Random.value, Random.value)
                    : transform.forward;
                return transform.position + Vector3.up * FALLBACK_LAUNCH_HEIGHT + forward * maxDistance;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (spreadDeg > 0f)
                ray.direction = BowSpreadMath.SampleCone(ray.direction, spreadDeg, Random.value, Random.value);
            float minDistance = Mathf.Max(0f, Vector3.Dot(transform.position - ray.origin, ray.direction));
            int count = Physics.RaycastNonAlloc(ray, _aimBuffer, maxDistance, _aimLayers,
                QueryTriggerInteraction.Collide);
            float best = maxDistance;
            for (int i = 0; i < count; i++)
            {
                Collider col = _aimBuffer[i].collider;
                if (col.transform.IsChildOf(transform)) continue;
                if (_aimBuffer[i].distance < minDistance) continue;
                // Zones / interaction triggers don't stop the aim — only solid colliders and hurtboxes do.
                if (col.isTrigger && col.gameObject.layer != _hurtboxLayer) continue;
                if (_aimBuffer[i].distance < best) best = _aimBuffer[i].distance;
            }
            return ray.GetPoint(best);
        }

        /// <summary>Bow damage: base + Dexterity bonus + bow damageBonus (no Strength, no Power Strike).</summary>
        private float ComputeRangedDamage(BowSO bow)
        {
            int dexterity = _playerStats != null ? _playerStats.Dexterity : 0;
            int baseDexterity = _progressionConfig != null ? _progressionConfig.baseDexterity : dexterity;
            float perDex = _progressionConfig != null ? _progressionConfig.damagePerDexterity : 0f;
            return RangedDamage.Compute(_config.attackDamage, dexterity, baseDexterity, perDex, bow.damageBonus);
        }

        // Story 7.10: public methods called by AnimationEventReceiver ----------------

        public void OnComboWindowOpen()
        {
            // Guard: after a cancel (dodge) or a forced end, the outgoing clip keeps firing events while it
            // blends out. A stale ComboWindowOpen must be ignored, otherwise _comboWindowOpen becomes true
            // while IsAttacking is false, allowing TryAttack() to start a new chain mid-animation.
            _attackTracker.Event(Time.time);
            if (!_stateManager.IsAttacking) return;
            _comboWindowOpen = true;
            GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
        }

        public void OnComboWindowClose()
        {
            _attackTracker.Event(Time.time);
            // Next step queued, or this is the OUTGOING clip's close event firing during the crossfade
            // (e.g. Uppercut's 0.93 inside the Attack_2 → Attack_3 blend) — not an expired window.
            if (_attackTracker.ShouldIgnoreWindowClose) return;
            ResetAttackCombo();
            GameLog.Info(TAG, "Combo window closed");
        }

        public void OnHitboxEnable()
        {
            _attackTracker.Event(Time.time);
            // Same guard as OnComboWindowOpen: a cancelled / force-ended attack still blending out must not
            // reopen the hit window (phantom hits during the dodge).
            if (!_stateManager.IsAttacking) return;
            _activeHitbox?.Enable();
        }

        public void OnHitboxDisable()  // Story 7.11
        {
            _attackTracker.Event(Time.time);
            _activeHitbox?.Disable();
        }

        /// <summary>
        /// Called by SMB_AttackState.OnStateEnter — animator confirmed this attack state started.
        /// Guarantees hitbox is disabled at state entry (safety net for back-to-back attacks
        /// where the exit cleanup was skipped because we transitioned directly to this state).
        /// Does NOT set IsAttacking — that is owned by input (ExecuteAttack).
        /// </summary>
        public void OnAttackStateEntered(int attackIndex)
        {
            _attackTracker.Enter(Time.time);
            _activeHitbox?.Disable();
            _comboWindowOpen = false;
        }

        /// <summary>
        /// Called by SMB_AttackState.OnStateExit for every attack state. The attack ends only when no attack
        /// state is left (a combo crossfade enters the next state before the previous one exits). Guarantees
        /// hitbox off and combo reset for interrupts (dodge/stagger/death) and for presses the Animator
        /// never took (e.g. during the uninterruptible blend-out to CombatIdle).
        /// </summary>
        public void OnAttackStateExited()
        {
            if (!_attackTracker.Exit(Time.time)) return; // chain in flight
            EndAttack();
        }

        /// <summary>WeaponSwap clip event — the hand reached the hilt / sheath: move the weapon visual now.</summary>
        public void OnWeaponSwapEvent() => CompleteWeaponSwitch();

        /// <summary>Called by SMB_BowAimState.OnStateEnter — the aim pose is reached: the arrow is nocked.</summary>
        public void OnBowAimEntered()
        {
            if (_stateManager == null || !_stateManager.IsAiming) return; // stale enter after a release
            _arrowNocked = true;
            _nockForced = false;
        }

        /// <summary>
        /// Called by SMB_WeaponSwitchState.OnStateExit — safety net when the clip was interrupted before its event.
        /// Only the state matching the switch in flight completes it: after R is re-pressed, the previous state still
        /// blending out must not finish the new switch early.
        /// </summary>
        public void OnWeaponSwitchStateExited(bool drawState)
        {
            if (drawState != _pendingDraw) return;
            CompleteWeaponSwitch();
        }

        // ----------------------------------------------------------------------------

        // Moves the weapon visual to match IsInCombat and releases the switch lock. No-op when no switch is in flight
        // (late events / exits, or a load that already forced the sheathed stance).
        private void CompleteWeaponSwitch()
        {
            if (_stateManager == null || !_stateManager.IsSwitchingWeapon) return;
            _stateManager.EndWeaponSwitch(); // lock released first — a throwing visual must not leave R locked
            if (_equipmentVisuals != null) _equipmentVisuals.SetCombatState(_stateManager.IsInCombat); // Unity null: may be destroyed on unload
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
                && _playerSkills.HasSkill(POWER_STRIKE_SKILL_ID))
                damage += _progressionConfig.powerStrikeDamageBonus;
            // Story 7.3: direct weapon damage bonus (flat, unscaled by stats).
            damage += _equipmentSystem?.GetWeaponDamageBonus() ?? 0f;
            return damage;
        }

        /// <summary>
        /// Melee stance damage: equipped weapon's resolved value (unarmed: config) + Strength bonus, × finisher
        /// multiplier on the last combo step, + Power Strike bonus. See StanceMath.ComputeMeleeStanceDamage.
        /// </summary>
        private float ComputeMeleeStanceDamage()
        {
            float baseStance = _currentWeaponSO != null ? _currentWeaponSO.ResolvedStanceDamage : _config.unarmedStanceDamage;
            float strengthDelta = _playerStats != null && _progressionConfig != null
                ? _playerStats.Strength - _progressionConfig.baseStrength : 0f;
            bool powerStrike = _playerSkills != null && _playerSkills.HasSkill(POWER_STRIKE_SKILL_ID);
            return StanceMath.ComputeMeleeStanceDamage(baseStance, strengthDelta, _config.stancePerStrength,
                IsMaxCombo(), _config.finisherStanceMultiplier, powerStrike ? _config.powerStrikeStanceBonus : 0f);
        }

        // Unarmed never knocks down.
        private bool MeleeCanKnockDown() => _currentWeaponSO != null && _currentWeaponSO.ResolvedCanKnockDown;

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
                StopAim(); // a landed hit breaks the bow aim
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

#if false // DISABLED: debug OnGUI — to be reworked
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
