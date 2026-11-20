# Ranged & NPC equipment — EntityArcher, NpcArmament

> Read when touching `EntityArcher`, `BowAimCorrection` on NPCs, `NpcArmament` (loadout, weapon vs fists, draw / sheathe,
> ranged switch, weapon loot, save) or the receiver's `WeaponSwap` event.
> Specs: `spec-ai-ranged-combat-style`, `spec-npc-equipment`, `spec-npc-equipment-save`.

---

## Ranged (EntityArcher)

- **Opt-in by data:** a `BowSO` in `NPCEntity._startingEquipment` + a `CombatStyleSO` (its "Ranged" values) + ammo (the bow's
  `ammoItem`) in the NPC `InventorySystem` + `EntityArcher` and `BowAimCorrection` on the root (`NPC_base Variant`). Only the
  bandit today: Hunting Bow + Axe, 12 `Item_Arrow` (scene `_startingItems`), `DisengageRange` 18.
- **Weapon choice** (`CombatTacticsRules.WantsRanged`, at each decision; `ShouldDecideWeaponMode`: never while holding a
  token or while a switch plays out): `CanUseRanged` = a wieldable bow, ammo left, **faction-hostile** target (brawls /
  aggro-only fights stay melee), no fists request → bow from `rangedSwitchDistance`,
  melee below `meleeSwitchDistance`, kept inside the band and during `weaponSwitchCooldown`; losing the bow / ammo → melee at
  once. Requests `NpcArmament.SetRanged` (see **NPC equipment** below). Spacing follows the **requested** mode during the switch clips.
- **Ranged branch of `Decide`** (after Drink — Drink outranks Shoot): held token → Shoot; beyond `preferredRange` +
  `approachMargin` → Approach; inside `rangedBackOffDistance` → BackOff (kite); `ShotReady` (bow in hand, off `shotCooldown`,
  ammo, clear line of fire) + free token → **Shoot**; else Circle on the `preferredRange` ring. Shots take a token from the
  **same pool** as melee swings (held only for the ~1.5 s shot).
- **Shoot:** tactics hold the agent still and face the target + `CombatConfigSO.npcAimStanceYaw` (side-on); `EntityArcher`
  sets `IsAiming` (re-pushed every frame), the nock comes from `SMB_BowAimState` → `EntityAnimationEventReceiver`
  (`IBowNockListener`) or the `bowNockTimeout` watchdog (a forced nock skips `BowFire`), the arrow leaves `aimTime` after the
  nock (the tell), the bow stays up `npcBowReleaseHold` for the Fire clip, then lowers; token released, circle. Watchdog =
  `npcBowNockTimeout`. Dropped with no arrow (ammo is spent at the release) by: a drink wish (checked at the decision interval
  — Drink outranks Shoot), `EndTactics` / retarget, the target dying / going down, a line of fire closed at the release (the
  clear-shot sweep runs again there).
- **Arrow:** `ArrowProjectile` (owner = the NPC) aimed at the target's body height × `npcAimHeightFraction` (CharacterController
  / NavMeshAgent), led by the smoothed target velocity × flight time × `leadFactor`, spread `shotSpreadAngle`
  (`BowSpreadMath.SampleCone`). Clear-shot sweep (`npcClearShotRadius`) before a shot: the first collider must be the target, a
  hostile body or nothing — a wall or a non-hostile body → circle. Hit = `EntityMeleeAttacker.IsHostileTarget` filter (a
  non-hostile absorbs it), then `TryReceiveHit(shooter)`: `NotBlocked` → `NotifyHitBy` + `TakeDamage(AttackDamage +
  bow.damageBonus, shooter)` + stance (`bow.ResolvedStanceDamage`); `Blocked` → blocker `ApplyBlockedStanceDamage`;
  `PerfectBlock` / `Dodged` → nothing for the shooter. Sound via `EntityMeleeAttacker.PlayHitSound`.
- **No guard while a bow is wielded** — the bow bumps the shield into the inventory, so `ShieldInHand` is false (`EntityGuard`); no melee swing (`IsAttackReady`; `HandleAttack` — a fight the tactics
  don't drive asks `SetRanged(false)` instead). Target lead velocity is smoothed with `npcTargetVelocitySmoothTime`
  (frame-rate independent). The clear-shot sweep skips corpses (the arrow does too) and holds the shot if its buffer fills.
- **Spine aim:** `BowAimCorrection` reads `EntityArcher` as its `IBowAimSource`; the player's calibration (`_arrowLineLocal` /
  `_arrowOriginLocal`) fits the `base_hero` NPC rig — measured ≈ 2° average error in play. Re-calibrate both if the rig changes.
- **Perception LOS ignores the entity's own colliders** (all entities) — the side-on aim swings the archer's own head (ragdoll
  collider) into the eye ray, which would otherwise drop it into the last-seen chase mid-shot. A full hit buffer counts as blocked.
- `BowAimCorrection` sits on every `NPC_base Variant`; it returns before capturing the pose while idle and resets (no writes)
  once the animator is off (ragdoll at death mid-aim).

---

## NPC equipment (NpcArmament)

- `NPC_base Variant` root: `EquipmentSystem` (`_npcOwned` = no warnings for the unwired player channels / `PlayerStats`),
  `EquipmentVisuals` (the base hero's 5 sockets, `base_hero` body, `mixamorig:Hips`, default controller `Humanoid_Base`,
  **`_onEquipmentChanged` / `_onVisualsRefreshed` unwired**) and `NpcArmament`, which refreshes the visuals directly.
- **Loadout:** `NPCEntity._startingEquipment` is added to the NPC inventory and equipped in `NpcArmament.Start` (skipped on a
  corpse). Equipped items leave the inventory → not lootable. Guards (all `Faction_Company`: CompanyGuard_ArrivalLeft /
  Right, DockGuard, WarehouseGuard, Harl, Warden, BarnGuard Venn) wear `Armor_CompanyGrunt` + `Weapon_ShortIronSword` + `Shield_KiteWoodShield`;
  the bandit carries `Weapon_HuntingBow` + `Weapon_Axe` + `Shield_RoundWoodShield` (shields equipped after weapons — see `COMBAT.md` → Blocking). A `BowSO` next to a melee weapon is **added to the inventory but not equipped** (fights
  start with the melee weapon).
- **Ranged switch:** `SetRanged(bool)` → `AdvanceWeaponChange` in `Update`, one step at a time,
  while `IsSwitchingWeapon` covers the clips: waits (switching, attacking, staggered, aiming, down) → sheathes a drawn weapon →
  swaps the equipped weapon while sheathed (`Unequip` + `Equip`) → `Rebuild` (bow override controller, draw in combat). Bow
  wielded = **no hitbox registered** (`RemoveAllHitboxes`). `CarriedBow` / `AmmoCount` / `IsRanged` / `RangedRequested` /
  `IsChangingWeapon`. **`RangedRequested` re-syncs to the wielded weapon** in `ApplyMode` whenever no change is pending and no
  fists are requested (wake-up, save restore, end of a fists fight, a bow without a visual) — a stale request once left an
  NPC circling with a bow it couldn't swing. Waking with the dropped weapon looted → another carried weapon (melee first, else
  the bow), else fists. Leaving combat keeps the current weapon.
- **Mode:** armed (weapon equipped with a `WeaponHitbox` under its visual → registered as `Weapon`, weapon animator override)
  unless the current fight asked for fists — `EntityBrain.StartBrawl(target, useFists: true)` (only the arrival sequence's
  Warden → Tam beating today) — or there is no weapon: `Unarmed` + `UnarmedLeft` + the default controller. **Weapon and
  fists are never registered together** (sword clips fire `HitboxEnable` with an empty id = every hitbox). The fists request ends with the fight
  (`SetCombatState(false)`). Every fight is armed by default, brawls included.
- **Controller swaps** (`EquipmentVisuals.ForceDefaultController` / `Refresh`) reset every animator parameter and state:
  `NpcArmament` re-pushes `IsInCombat` and calls `EntityMeleeAttacker.ResetAttackStates()` (attack states restart without
  SMB exits — a stuck counter blocks every attack). Never swapped while unconscious / dead (it would stand the body up),
  attacking or in a stagger / knockdown — a mode change then waits (`_modePending`, applied in `Update`). A re-enabled
  NPC rebuilds its mode in `OnEnable` (the visual and its hitbox were recreated).
- **Draw / sheathe:** armed NPCs play the clips on combat entry / exit (`AIAnimationDriver.TriggerWeaponSwitch`); the visual
  moves on `WeaponSwap`, or after `CombatConfigSO.weaponSwitchTimeout / weaponSwitchAnimSpeed`. `HandleAttack` waits while
  `IsSwitchingWeapon`.
- **Down:** knockout / death → `OnDowned`: weapon unequipped into the inventory (lootable), visual removed with
  `Refresh(false)` (controller kept), armor stays equipped (not lootable). `RegainedConsciousness` → `OnWokeUp`: the weapon
  still in the inventory is re-equipped, a looted one leaves the NPC on fists for good; full refresh.
- **Saved:** `SaveableObject` captures `NpcArmament.Equipped` + `DroppedWeapon` (`ObjectSaveData.equipped` / `droppedWeaponId`, via the shared `EquipmentSaveConverter`) once `HasLoadout` (null before
  `Start`, so a corpse hidden at load keeps its authored armor). No catalog → restore skipped, authored loadout kept. Restore runs after `Start`, so
  `RestoreLoadout` replaces the granted loadout (`RestoreEquipped` never touches the already-restored inventory) and sets
  `_loadoutRestored` so a corpse whose `Start` runs later doesn't grant again. Knockout isn't saved: a living NPC saved while
  down loads standing and re-equips its dropped weapon if its inventory still holds it; a corpse keeps it as loot.
  `equipped == null` (old save / no armament) → the loadout is granted as before. Brann (`NPC_StashOwner`) has no loadout.
- **`WeaponSwap` event:** `EntityAnimationEventReceiver.WeaponSwap(AnimationEvent)` (draw / sheathe clips) →
  `NpcArmament.CompleteWeaponSwitch(fromDraw)` — draw vs sheathe read from the firing state name (`Draw_State` /
  `Sheathe_State` in `Humanoid_Template`; **renaming them** makes a superseded clip's event complete a reversed switch); no
  armament → no-op.
