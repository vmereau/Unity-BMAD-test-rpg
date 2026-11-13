using Game.Animations;
using Game.Combat;
using Game.Core;
using Game.Inventory;
using Game.NPC;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// NPC-side owner of worn / wielded equipment (spec-npc-equipment), on the NPC root next to <see cref="EntityBrain"/>.
    /// Reuses the player's <see cref="EquipmentSystem"/> + <see cref="EquipmentVisuals"/> with their player-only channels
    /// unwired, refreshing the visuals directly.
    /// <list type="bullet">
    /// <item>Loadout: <see cref="NPCEntity.StartingEquipment"/> is added to the NPC inventory and equipped in <c>Start</c>
    /// (equipped items leave the inventory, so they are not lootable).</item>
    /// <item>Mode: armed (weapon equipped, its <see cref="WeaponHitbox"/> registered as <c>Weapon</c>, weapon animator
    /// override) unless the current fight asked for fists (<see cref="SetFists"/>, cleared when the fight ends) or there is
    /// no weapon — then <c>Unarmed</c> + the default controller. Exactly one of the two hitboxes is registered: clip events
    /// with an empty id open every hitbox.</item>
    /// <item>Draw / sheathe: armed NPCs play the clips on combat entry / exit; the visual moves on the <c>WeaponSwap</c>
    /// event (<see cref="CompleteWeaponSwitch"/>), or on a timeout.</item>
    /// <item>Down (knocked out / dead): the weapon is unequipped into the inventory (lootable), the armor stays. Waking
    /// with it still there re-equips it; looted → fists for good.</item>
    /// </list>
    /// A controller swap resets every animator parameter and state, so it never happens while down / dead, and
    /// <c>IsInCombat</c> is re-pushed after each one.
    /// </summary>
    public class NpcArmament : MonoBehaviour
    {
        private const string TAG = "[AI]";
        public const string WEAPON_HITBOX_ID = "Weapon";
        public const string UNARMED_HITBOX_ID = "Unarmed";
        private const float DEFAULT_SWITCH_SPEED = 1.5f;
        private const float DEFAULT_SWITCH_TIMEOUT = 2.5f;

        [SerializeField] private EquipmentSystem _equipment;
        [SerializeField] private EquipmentVisuals _visuals;
        [SerializeField] private InventorySystem _inventory;
        [Tooltip("Draw / sheathe clip speed (weaponSwitchAnimSpeed) and the WeaponSwap watchdog (weaponSwitchTimeout). " +
                 "Null = 1.5 / 2.5 s.")]
        [SerializeField] private CombatConfigSO _combatConfig;

        private EntityMeleeAttacker _attacker;
        private AIAnimationDriver _driver;
        private EntityHealth _health;
        private PersistentID _persistentID;
        private WeaponHitbox _unarmedHitbox;
        private bool _ready;
        private bool _started;
        private bool _modePending; // a mode change waiting for the attack / stance reaction to end (no mid-clip swap)
        private EntityStance _stance;

        private bool _fistsRequested;
        private bool _inCombat;
        private WeaponSO _wielded;
        private WeaponSO _droppedWeapon;
        private bool _isSwitching;
        private bool _pendingDraw;
        private float _switchTimer;

        /// <summary>True while a draw / sheathe clip runs — the brain doesn't attack until the weapon is in hand.</summary>
        public bool IsSwitchingWeapon => _isSwitching;

        /// <summary>The weapon fighting with right now (null = fists).</summary>
        public WeaponSO WieldedWeapon => _wielded;

        /// <summary>Health damage added to <c>Entity.AttackDamage</c> (wielded weapon's <c>damageBonus</c>, fists 0).</summary>
        public float DamageBonus => _wielded != null ? _wielded.damageBonus : 0f;

        private WeaponSO EquippedWeapon => _equipment != null ? _equipment.GetEquipped(EquipmentSlot.Weapon) as WeaponSO : null;
        private bool IsDown => _health != null && (_health.IsDead || _health.IsUnconscious);
        private float SwitchSpeed => _combatConfig != null ? _combatConfig.weaponSwitchAnimSpeed : DEFAULT_SWITCH_SPEED;
        private float SwitchTimeout => (_combatConfig != null ? _combatConfig.weaponSwitchTimeout : DEFAULT_SWITCH_TIMEOUT)
                                       / Mathf.Max(0.1f, SwitchSpeed);

        private void Awake()
        {
            _attacker = GetComponent<EntityMeleeAttacker>();
            _driver = GetComponent<AIAnimationDriver>();
            _health = GetComponent<EntityHealth>();
            _stance = GetComponent<EntityStance>();
            _persistentID = GetComponent<PersistentID>();
            if (_inventory == null) _inventory = GetComponent<InventorySystem>();
            if (_equipment == null || _visuals == null || _inventory == null || _attacker == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: NpcArmament needs EquipmentSystem, EquipmentVisuals, InventorySystem " +
                                   "and EntityMeleeAttacker — disabled (NPC fights unarmed)");
                enabled = false;
                return;
            }
            foreach (EntityMeleeAttacker.NamedHitbox entry in _attacker.Hitboxes)
                if (entry.Id == UNARMED_HITBOX_ID) _unarmedHitbox = entry.Hitbox;
            _ready = true;
        }

        private void Start()
        {
            if (!_ready) return;
            // A corpse keeps what its saved inventory holds: armor only (worn, never lootable), no fresh weapon.
            GrantLoadout(includeWeapons: _health == null || !_health.IsDead);
            Rebuild();
            _started = true;
        }

        // Re-enabled (deactivated GO, respawn): EquipmentVisuals.OnEnable rebuilt the weapon visual (new hitbox) and may have
        // swapped the controller — re-sync the mode. The first enable is handled by Start.
        private void OnEnable()
        {
            if (_started) Rebuild();
        }

        // Full rebuild: visuals (controller only while up), then the post-swap fixes, hitboxes / controller, draw state.
        private void Rebuild()
        {
            _modePending = false;
            bool up = !IsDown;
            _visuals.Refresh(up);
            if (up) AfterControllerSwap(); // Refresh(true) may assign the controller twice and end on the same one
            ApplyMode();
            SyncDrawState(); // a fight started before this point still draws
        }

        private void Update()
        {
            if (_modePending && CanSwapNow) ApplyPendingMode();
            if (!_isSwitching) return;
            _switchTimer -= Time.deltaTime;
            if (_switchTimer > 0f) return;
            GameLog.Info(TAG, $"{gameObject.name}: no WeaponSwap event — weapon {(_pendingDraw ? "drawn" : "sheathed")} by timeout");
            CompleteWeaponSwitch();
            if (_driver != null) _driver.ClearWeaponSwitch();
        }

        private void OnDisable()
        {
            if (!_isSwitching) return;
            CompleteWeaponSwitch(); // no watchdog while disabled: leave the visual where the clip was taking it
        }

        // A controller swap cuts the playing clip: never while down, attacking or in a stagger / knockdown reaction.
        private bool CanSwapNow => !IsDown && !_attacker.IsInAttackState && (_stance == null || !_stance.IsIncapacitated);

        // Applies the current mode now when safe, else once the clip ends (Update).
        private void RequestModeApply()
        {
            if (CanSwapNow) ApplyPendingMode();
            else _modePending = true;
        }

        private void ApplyPendingMode()
        {
            _modePending = false;
            ApplyMode();
            SyncDrawState();
        }

        private void GrantLoadout(bool includeWeapons)
        {
            if (_persistentID == null || _persistentID.Entity is not NPCEntity npc || npc.StartingEquipment == null) return;
            foreach (EquipableItemSO item in npc.StartingEquipment)
            {
                if (item == null || (!includeWeapons && item is WeaponSO)) continue;
                if (!_inventory.AddItem(item))
                {
                    GameLog.Warn(TAG, $"{gameObject.name}: loadout item {item.itemName} could not be added to the inventory");
                    continue;
                }
                _equipment.Equip(item);
            }
        }

        /// <summary>
        /// Fists for the fight being started (true) or the weapon (false). Applied at once (a drawn weapon snaps back to its
        /// sheath for fists); cleared when the fight ends.
        /// </summary>
        public void SetFists(bool useFists)
        {
            if (!_ready || _fistsRequested == useFists) return;
            _fistsRequested = useFists;
            GameLog.Info(TAG, $"{gameObject.name} fights with {(useFists ? "fists" : "its weapon")}");
            if (IsDown) return; // applied on wake-up
            RequestModeApply();
        }

        /// <summary>Brain combat state changed: draw on entry, sheathe on exit; a fists request ends with the fight.</summary>
        public void OnCombatStateChanged(bool inCombat)
        {
            if (!_ready) return;
            _inCombat = inCombat;
            if (!inCombat && _fistsRequested)
            {
                _fistsRequested = false;
                if (IsDown) return; // wake-up rebuilds the mode
                RequestModeApply(); // back to the weapon once the last punch / reaction clip is over
                return; // fists drew nothing: nothing to sheathe
            }
            SyncDrawState();
        }

        /// <summary>Knocked out / killed: the weapon goes into the (lootable) inventory, the armor stays equipped.</summary>
        public void OnDowned()
        {
            if (!_ready) return;
            _isSwitching = false;
            _modePending = false; // the wake-up rebuild applies the mode
            _fistsRequested = false; // the fight is over (don't rely on SetCombatState(false) having run first)
            if (_driver != null) _driver.ClearWeaponSwitch();
            WeaponSO weapon = EquippedWeapon;
            if (weapon == null) return;
            _equipment.Unequip(EquipmentSlot.Weapon);
            _droppedWeapon = weapon;
            _visuals.SetCombatState(false);
            _visuals.Refresh(false); // keep the controller: a swap would stand the body up
            ApplyHitboxes(null);
            _attacker.SetWieldedWeapon(null);
            GameLog.Info(TAG, $"{gameObject.name} is down — {weapon.itemName} dropped into its inventory");
        }

        /// <summary>Back up: re-equips the dropped weapon if it is still in the inventory (else fists), rebuilds the mode.</summary>
        public void OnWokeUp()
        {
            if (!_ready) return;
            if (_droppedWeapon != null)
            {
                if (_inventory.CountOf(_droppedWeapon) > 0)
                {
                    _equipment.Equip(_droppedWeapon);
                    GameLog.Info(TAG, $"{gameObject.name} picks its {_droppedWeapon.itemName} back up");
                }
                else GameLog.Info(TAG, $"{gameObject.name}'s {_droppedWeapon.itemName} was taken — fists from now on");
                _droppedWeapon = null;
            }
            _visuals.SetCombatState(false);
            Rebuild();
        }

        /// <summary>
        /// WeaponSwap clip event (or the timeout): the weapon visual moves to the hand / sheath now.
        /// <paramref name="fromDraw"/> = which clip fired it (null = unknown / timeout): a superseded clip still blending
        /// out (sheathe → draw reversal) must not complete the new switch.
        /// </summary>
        public void CompleteWeaponSwitch(bool? fromDraw = null)
        {
            if (!_isSwitching) return;
            if (fromDraw.HasValue && fromDraw.Value != _pendingDraw) return;
            _isSwitching = false;
            if (_wielded != null) _visuals.SetCombatState(_pendingDraw);
        }

        // Hitboxes + controller for the current mode. Only while up: a controller swap resets the animator.
        private void ApplyMode()
        {
            WeaponSO weapon = _fistsRequested ? null : EquippedWeapon;
            WeaponHitbox weaponHitbox = null;
            if (weapon != null && _visuals.ActiveWeaponGO != null)
            {
                weaponHitbox = _visuals.ActiveWeaponGO.GetComponentInChildren<WeaponHitbox>(true); // Drawn is inactive while sheathed
                if (weaponHitbox == null)
                    GameLog.Warn(TAG, $"{gameObject.name}: {weapon.itemName} visual has no WeaponHitbox — fighting with fists");
            }
            bool armed = weaponHitbox != null;
            ApplyHitboxes(armed ? weaponHitbox : null);
            _wielded = armed ? weapon : null;
            _attacker.SetWieldedWeapon(_wielded);

            if (IsDown) return;
            if (!armed)
            {
                _isSwitching = false;
                if (_driver != null) _driver.ClearWeaponSwitch(); // a pending draw must not play with an empty hand
                _visuals.SetCombatState(false); // a drawn weapon snaps back to its sheath (fists)
            }
            RuntimeAnimatorController before = _visuals.AppliedController;
            _visuals.ForceDefaultController(!armed);
            if (_visuals.AppliedController != before) AfterControllerSwap();
        }

        private void ApplyHitboxes(WeaponHitbox weaponHitbox)
        {
            _attacker.UnregisterHitbox(WEAPON_HITBOX_ID); // the previous visual's hitbox (maybe destroyed)
            if (weaponHitbox != null)
            {
                _attacker.UnregisterHitbox(UNARMED_HITBOX_ID);
                _attacker.RegisterHitbox(WEAPON_HITBOX_ID, weaponHitbox);
            }
            else if (_unarmedHitbox != null && !_attacker.HasHitbox(UNARMED_HITBOX_ID))
            {
                _attacker.RegisterHitbox(UNARMED_HITBOX_ID, _unarmedHitbox);
            }
            if (weaponHitbox == null) _wielded = null;
        }

        // Swapped controller: every parameter is back to default and attack states restarted without SMB exits.
        private void AfterControllerSwap()
        {
            _attacker.ResetAttackStates();
            if (_driver != null) _driver.SetInCombat(_inCombat);
            if (_isSwitching)
            {
                _isSwitching = false;
                if (_wielded != null) _visuals.SetCombatState(_pendingDraw);
            }
        }

        // Armed: start the draw / sheathe clip when the visual doesn't match the combat state.
        private void SyncDrawState()
        {
            if (_wielded == null || IsDown) return;
            if (_isSwitching)
            {
                if (_pendingDraw != _inCombat) StartSwitch(_inCombat);
                return;
            }
            if (_visuals.IsDrawn != _inCombat) StartSwitch(_inCombat);
        }

        private void StartSwitch(bool draw)
        {
            _isSwitching = true;
            _pendingDraw = draw;
            _switchTimer = SwitchTimeout;
            if (_driver != null) _driver.TriggerWeaponSwitch(draw, SwitchSpeed);
            else CompleteWeaponSwitch();
        }
    }
}
