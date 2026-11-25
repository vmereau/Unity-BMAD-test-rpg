using System.Collections.Generic;
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
    /// <item>Down (knocked out / dead): the weapon and the shield are unequipped into the inventory (lootable), the armor
    /// stays. Waking with them still there re-equips them; a looted weapon → fists for good, a looted shield → none.</item>
    /// <item>Ranged (spec-ai-ranged-combat-style): a <see cref="BowSO"/> in the loadout next to a melee weapon stays in the
    /// inventory. <see cref="SetRanged"/> swaps weapons as one locked chain (sheathe → swap the equipped weapon → draw);
    /// with the bow in hand no melee hitbox is registered.</item>
    /// <item>Save / load (spec-npc-equipment-save): <see cref="Game.World.SaveableObject"/> captures <see cref="Equipped"/>
    /// + <see cref="DroppedWeapon"/>; <see cref="RestoreLoadout"/> replaces the granted loadout (and <c>Start</c> then grants
    /// nothing).</item>
    /// </list>
    /// A controller swap resets every animator parameter and state, so it never happens while down / dead, and
    /// <c>IsInCombat</c> is re-pushed after each one.
    /// </summary>
    public class NpcArmament : MonoBehaviour
    {
        private const string TAG = "[AI]";
        public const string WEAPON_HITBOX_ID = "Weapon";
        public const string UNARMED_HITBOX_ID = "Unarmed";
        /// <summary>Left fist — opened by the left-hand unarmed clips' events (spec-unarmed-combo-rework).</summary>
        public const string UNARMED_LEFT_HITBOX_ID = "UnarmedLeft";
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
        private WeaponHitbox _unarmedHitboxLeft;
        private bool _ready;
        private bool _started;
        private bool _loadoutRestored; // a save restored the loadout: Start must not grant it again
        private bool _modePending; // a mode change waiting for the attack / stance reaction to end (no mid-clip swap)
        private EntityStance _stance;

        private bool _fistsRequested;
        private bool _inCombat;
        private WeaponSO _wielded;
        private WeaponSO _droppedWeapon;
        private bool _isSwitching;
        private bool _pendingDraw;
        private float _switchTimer;
        private bool _rangedRequested;      // the tactics want the bow (SetRanged)
        private bool _weaponChangePending;  // the equipped weapon doesn't match the request yet (Update advances it)
        private EntityArcher _archer;       // optional: never swap mid-shot
        private ShieldSO _stowedShield;     // shield a bow / two-hander swap pushed into the inventory, re-equipped on the way back

        /// <summary>True while a draw / sheathe clip runs — the brain doesn't attack until the weapon is in hand.</summary>
        public bool IsSwitchingWeapon => _isSwitching;

        /// <summary>The weapon fighting with right now (null = fists).</summary>
        public WeaponSO WieldedWeapon => _wielded;

        /// <summary>The bow is the wielded weapon (ranged mode in effect). No melee hitbox is registered.</summary>
        public bool IsRanged => _wielded is BowSO;

        /// <summary>
        /// The bow was requested (<see cref="SetRanged"/>) — true from the request, through the switch clips. Re-synced to the
        /// wielded weapon whenever the mode is applied without a pending change (wake-up, save restore, end of a fists fight,
        /// a bow that can't be wielded), so the tactics never reason from a stale request.
        /// </summary>
        public bool RangedRequested => _rangedRequested;

        /// <summary>A weapon change is pending or a draw / sheathe clip plays.</summary>
        public bool IsChangingWeapon => _weaponChangePending || _isSwitching;

        /// <summary>True while the fight is fought with fists (a <see cref="SetFists"/> request).</summary>
        public bool FistsRequested => _fistsRequested;

        /// <summary>The bow this NPC carries — equipped, else the first one in its inventory (null = none, e.g. looted).</summary>
        public BowSO CarriedBow
        {
            get
            {
                if (!_ready) return null;
                if (EquippedWeapon is BowSO equipped) return equipped;
                for (int i = 0; i < _inventory.Count; i++)
                    if (_inventory.Items[i].Item is BowSO bow) return bow;
                return null;
            }
        }

        /// <summary>Arrows (the carried bow's ammo item) left in the inventory.</summary>
        public int AmmoCount
        {
            get
            {
                BowSO bow = CarriedBow;
                return bow != null && bow.IsConfigured ? _inventory.CountOf(bow.ammoItem) : 0;
            }
        }

        /// <summary>True once a loadout is in place (see <see cref="Equipped"/>'s save note) — the guard needs it.</summary>
        public bool IsReady => _ready;

        /// <summary>
        /// A shield is held in the offhand of the drawn weapon (spec-shield-offhand-blocking) — guarding with it uses the full
        /// perfect-block chance. False with fists, a sheathed weapon, a bow / two-hander, or no shield.
        /// </summary>
        public bool ShieldInHand => _ready && _visuals.ShieldInHand;

        /// <summary>
        /// Armor added to <c>Entity.Armor</c>: the equipped shield's <c>defenseBonus</c> (body armor isn't counted). None in a fists
        /// fight — the shield stays on the back (spec-unshielded-blocking).
        /// </summary>
        public float ShieldArmorBonus
        {
            get
            {
                ShieldSO shield = _ready && !_fistsRequested ? _equipment.EquippedShield : null;
                return shield != null ? shield.defenseBonus : 0f;
            }
        }

        /// <summary>Health damage added to <c>Entity.AttackDamage</c> (wielded weapon's <c>damageBonus</c>, fists 0).</summary>
        public float DamageBonus => _wielded != null ? _wielded.damageBonus : 0f;

        /// <summary>
        /// True once a loadout is in place (granted in <c>Start</c> or restored from a save) — only then is <see cref="Equipped"/>
        /// worth saving. False when <c>Awake</c> disabled the component, or before <c>Start</c> (a corpse hidden at load).
        /// </summary>
        public bool HasLoadout => _ready && (_started || _loadoutRestored);

        /// <summary>Equipped slots (save capture). Empty when not ready.</summary>
        public IReadOnlyDictionary<EquipmentSlot, ItemSO> Equipped =>
            _ready ? _equipment.Equipped : EMPTY_EQUIPPED;

        /// <summary>The weapon dropped into the inventory when the NPC went down, until it wakes (save capture).</summary>
        public WeaponSO DroppedWeapon => _droppedWeapon;

        private static readonly Dictionary<EquipmentSlot, ItemSO> EMPTY_EQUIPPED = new();

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
            _archer = GetComponent<EntityArcher>();
            if (_inventory == null) _inventory = GetComponent<InventorySystem>();
            if (_equipment == null || _visuals == null || _inventory == null || _attacker == null)
            {
                GameLog.Error(TAG, $"{gameObject.name}: NpcArmament needs EquipmentSystem, EquipmentVisuals, InventorySystem " +
                                   "and EntityMeleeAttacker — disabled (NPC fights unarmed)");
                enabled = false;
                return;
            }
            foreach (EntityMeleeAttacker.NamedHitbox entry in _attacker.Hitboxes)
            {
                if (entry.Id == UNARMED_HITBOX_ID) _unarmedHitbox = entry.Hitbox;
                else if (entry.Id == UNARMED_LEFT_HITBOX_ID) _unarmedHitboxLeft = entry.Hitbox;
            }
            _ready = true;
        }

        private void Start()
        {
            if (!_ready) return;
            // A corpse keeps what its saved inventory holds: armor only (worn, never lootable), no fresh weapon / shield.
            // A restored loadout (a hidden corpse re-activated by the save restore) is already in place.
            if (!_loadoutRestored) GrantLoadout(includeWeapons: _health == null || !_health.IsDead);
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
            if (_weaponChangePending) AdvanceWeaponChange();
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
            // A bow next to a melee weapon is carried, not equipped: fights start with the melee weapon and the tactics
            // switch to the bow at range (spec-ai-ranged-combat-style).
            bool hasMelee = false;
            foreach (EquipableItemSO item in npc.StartingEquipment)
                if (item is WeaponSO and not BowSO) hasMelee = true;
            // Shields after everything else, so list order never decides between a shield and a two-handed weapon.
            foreach (EquipableItemSO item in npc.StartingEquipment)
                if (item is not ShieldSO) GrantItem(item, includeWeapons, hasMelee);
            foreach (EquipableItemSO item in npc.StartingEquipment)
                if (item is ShieldSO) GrantItem(item, includeWeapons, hasMelee);
        }

        private void GrantItem(EquipableItemSO item, bool includeWeapons, bool hasMelee)
        {
            if (item == null || (!includeWeapons && item is WeaponSO or ShieldSO)) return; // a corpse wears armor only
            if (!_inventory.AddItem(item))
            {
                GameLog.Warn(TAG, $"{gameObject.name}: loadout item {item.itemName} could not be added to the inventory");
                return;
            }
            if (item is BowSO && hasMelee) return; // carried in the inventory
            if (item is ShieldSO shield && EquippedWeapon is { OccupiesOffhand: true })
            {
                GameLog.Warn(TAG, $"{gameObject.name}: loadout shield {shield.itemName} can't be held next to " +
                                  $"{EquippedWeapon.itemName} (both hands) — carried in the inventory");
                _stowedShield = shield;
                return;
            }
            _equipment.Equip(item);
        }

        /// <summary>
        /// Bow (true) or the melee weapon / fists (false), requested by the tactics (spec-ai-ranged-combat-style). Played as
        /// one locked chain once safe: sheathe the drawn weapon, swap the equipped one, draw the new one (in combat).
        /// Ignored while the fight is fought with fists.
        /// </summary>
        public void SetRanged(bool ranged)
        {
            if (!_ready || _rangedRequested == ranged) return;
            _rangedRequested = ranged;
            _weaponChangePending = true;
            GameLog.Info(TAG, $"{gameObject.name} switches to {(ranged ? "its bow" : "melee")}");
            if (!IsDown) AdvanceWeaponChange();
        }

        // Weapon for the requested mode: the carried bow, else the melee weapon (equipped non-bow, or the first in the
        // inventory; null = fists).
        private WeaponSO DesiredWeapon()
        {
            if (_rangedRequested)
            {
                BowSO bow = CarriedBow;
                if (bow != null) return bow;
            }
            WeaponSO equipped = EquippedWeapon;
            if (equipped != null && equipped is not BowSO) return equipped;
            for (int i = 0; i < _inventory.Count; i++)
                if (_inventory.Items[i].Item is WeaponSO weapon && weapon is not BowSO) return weapon;
            return null;
        }

        // One step of the switch chain per call: wait (switching, attacking, staggered, aiming, down), sheathe a drawn
        // weapon, then swap the equipment while sheathed and rebuild (the rebuild draws the new weapon in combat).
        private void AdvanceWeaponChange()
        {
            if (_fistsRequested || IsDown)
            {
                _weaponChangePending = false;
                return;
            }
            if (_isSwitching || _modePending || !CanSwapNow || (_archer != null && _archer.IsAiming)) return;
            WeaponSO desired = DesiredWeapon();
            WeaponSO equipped = EquippedWeapon;
            if (desired == equipped)
            {
                _weaponChangePending = false;
                SyncDrawState();
                return;
            }
            if (_wielded != null && _visuals.IsDrawn)
            {
                StartSwitch(false); // sheathe first; the next call swaps once it completed
                return;
            }
            if (equipped != null) _equipment.Unequip(EquipmentSlot.Weapon);
            if (desired is { OccupiesOffhand: true } && _equipment.EquippedShield != null)
                _stowedShield = _equipment.EquippedShield; // the Equip below pushes it into the inventory
            if (desired != null) _equipment.Equip(desired);
            EquipCarriedShield(); // back to a one-handed weapon: the shield returns to the offhand
            _weaponChangePending = false;
            GameLog.Info(TAG, $"{gameObject.name} now wields {(desired != null ? desired.itemName : "fists")}");
            Rebuild(); // visual + controller (bow override) + hitboxes, then the draw clip in combat
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

        /// <summary>
        /// Knocked out / killed: the weapon and the shield go into the (lootable) inventory, the armor stays equipped
        /// (spec-npc-shield-loot-on-down). The shield is kept as <c>_stowedShield</c>, re-equipped on wake if not looted.
        /// </summary>
        public void OnDowned()
        {
            if (!_ready) return;
            _isSwitching = false;
            _modePending = false; // the wake-up rebuild applies the mode
            _rangedRequested = false; // wakes up with what it held; the tactics request again
            _weaponChangePending = false;
            _fistsRequested = false; // the fight is over (don't rely on SetCombatState(false) having run first)
            if (_driver != null) _driver.ClearWeaponSwitch();
            WeaponSO weapon = EquippedWeapon;
            ShieldSO shield = _equipment.EquippedShield;
            if (weapon == null && shield == null) return;
            if (weapon != null)
            {
                _equipment.Unequip(EquipmentSlot.Weapon);
                _droppedWeapon = weapon;
                GameLog.Info(TAG, $"{gameObject.name} is down — {weapon.itemName} dropped into its inventory");
            }
            if (shield != null)
            {
                // A shield in hand means none is stowed (a bow / two-hander pushes it out), so nothing is overwritten.
                _equipment.Unequip(EquipmentSlot.Offhand);
                _stowedShield = shield;
                GameLog.Info(TAG, $"{gameObject.name} is down — {shield.itemName} dropped into its inventory");
            }
            _visuals.SetCombatState(false);
            _visuals.Refresh(false); // keep the controller: a swap would stand the body up
            ApplyHitboxes(null);
            _attacker.SetWieldedWeapon(null);
        }

        /// <summary>
        /// Back up: re-equips the dropped weapon if it is still in the inventory (else fists), then the dropped shield if still
        /// there and the weapon leaves the offhand free, and rebuilds the mode.
        /// </summary>
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
                else
                {
                    // Taken: fall back to another weapon it still carries (a looted bow leaves the axe), else fists.
                    WeaponSO fallback = DesiredWeapon();
                    if (fallback == null) fallback = CarriedBow;
                    if (fallback != null) _equipment.Equip(fallback);
                    GameLog.Info(TAG, $"{gameObject.name}'s {_droppedWeapon.itemName} was taken — " +
                                      (fallback != null ? $"{fallback.itemName} instead" : "fists from now on"));
                }
                _droppedWeapon = null;
            }
            EquipCarriedShield();
            _visuals.SetCombatState(false);
            Rebuild();
        }

        // Offhand empty and the weapon leaves it free: re-equip the shield a bow / two-hander swap (or going down) stowed —
        // only that one, never trade stock or another shield the inventory happens to hold. Gone (looted / sold) → forgotten.
        private void EquipCarriedShield()
        {
            if (_stowedShield == null || _equipment.EquippedShield != null || EquippedWeapon is { OccupiesOffhand: true }) return;
            ShieldSO shield = _stowedShield;
            _stowedShield = null;
            if (_inventory.CountOf(shield) <= 0)
            {
                GameLog.Info(TAG, $"{gameObject.name}'s {shield.itemName} is gone — no shield");
                return;
            }
            _equipment.Equip(shield);
            GameLog.Info(TAG, $"{gameObject.name} takes its {shield.itemName} back in hand");
        }

        // Restore (no stowed state is saved): a living NPC without an offhand item takes back its starting shield if its
        // inventory still holds it (stowed by a bow, or dropped while down at save time: stowed again while the bow is held).
        // Never granted anew — a shield looted from it stays gone (spec-npc-shield-loot-on-down).
        private void ReEquipCarriedStartingShield()
        {
            if (_equipment.EquippedShield != null) return;
            if (_persistentID == null || _persistentID.Entity is not NPCEntity npc || npc.StartingEquipment == null) return;
            ShieldSO starting = null;
            foreach (EquipableItemSO item in npc.StartingEquipment)
                if (item is ShieldSO shield) { starting = shield; break; }
            if (starting == null) return;
            if (_inventory.CountOf(starting) <= 0) return;
            if (EquippedWeapon is { OccupiesOffhand: true }) _stowedShield = starting;
            else _equipment.Equip(starting);
        }

        /// <summary>
        /// Save / load (no gameplay events): replaces the equipped slots without touching the already-restored inventory, so
        /// the loadout granted in <c>Start</c> is dropped. Knockout isn't saved: a living NPC saved while down picks its
        /// dropped weapon back up if the inventory still holds it (like <see cref="OnWokeUp"/>); a corpse leaves it in its loot.
        /// </summary>
        public void RestoreLoadout(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped, WeaponSO droppedWeapon, bool killed)
        {
            if (!_ready) return;
            _equipment.RestoreEquipped(equipped);
            _loadoutRestored = true;
            _droppedWeapon = null;
            _isSwitching = false;
            _modePending = false;
            _rangedRequested = false;
            _weaponChangePending = false;
            if (droppedWeapon != null && EquippedWeapon == null)
            {
                if (killed) _droppedWeapon = droppedWeapon; // stays lootable; kept so the next capture still has it
                else if (_inventory.CountOf(droppedWeapon) > 0)
                {
                    _equipment.Equip(droppedWeapon);
                    GameLog.Info(TAG, $"{gameObject.name}: saved while down — {droppedWeapon.itemName} re-equipped");
                }
            }
            _stowedShield = null;
            if (!killed) ReEquipCarriedStartingShield();
            if (!_started || !isActiveAndEnabled) return; // Start / OnEnable rebuild later
            if (_driver != null) _driver.ClearWeaponSwitch();
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
            // A bow has no melee hitbox: wielded with no hitbox registered at all (spec-ai-ranged-combat-style).
            bool ranged = weapon is BowSO && _visuals.ActiveWeaponGO != null;
            WeaponHitbox weaponHitbox = null;
            if (weapon != null && !ranged && _visuals.ActiveWeaponGO != null)
            {
                weaponHitbox = _visuals.ActiveWeaponGO.GetComponentInChildren<WeaponHitbox>(true); // Drawn is inactive while sheathed
                if (weaponHitbox == null)
                    GameLog.Warn(TAG, $"{gameObject.name}: {weapon.itemName} visual has no WeaponHitbox — fighting with fists");
            }
            bool armed = weaponHitbox != null || ranged;
            if (ranged) RemoveAllHitboxes();
            else ApplyHitboxes(weaponHitbox);
            _wielded = armed ? weapon : null;
            // The request follows what is actually in hand once no change is pending (see RangedRequested).
            if (!_weaponChangePending && !_fistsRequested) _rangedRequested = _wielded is BowSO;
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

        // Bow in hand: neither the weapon nor the fists can hit.
        private void RemoveAllHitboxes()
        {
            _attacker.UnregisterHitbox(WEAPON_HITBOX_ID);
            _attacker.UnregisterHitbox(UNARMED_HITBOX_ID);
            _attacker.UnregisterHitbox(UNARMED_LEFT_HITBOX_ID);
        }

        private void ApplyHitboxes(WeaponHitbox weaponHitbox)
        {
            _attacker.UnregisterHitbox(WEAPON_HITBOX_ID); // the previous visual's hitbox (maybe destroyed)
            if (weaponHitbox != null)
            {
                _attacker.UnregisterHitbox(UNARMED_HITBOX_ID);
                _attacker.UnregisterHitbox(UNARMED_LEFT_HITBOX_ID);
                _attacker.RegisterHitbox(WEAPON_HITBOX_ID, weaponHitbox);
            }
            else
            {
                if (_unarmedHitbox != null && !_attacker.HasHitbox(UNARMED_HITBOX_ID))
                    _attacker.RegisterHitbox(UNARMED_HITBOX_ID, _unarmedHitbox);
                if (_unarmedHitboxLeft != null && !_attacker.HasHitbox(UNARMED_LEFT_HITBOX_ID))
                    _attacker.RegisterHitbox(UNARMED_LEFT_HITBOX_ID, _unarmedHitboxLeft);
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
