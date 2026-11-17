---
title: 'Shield-exclusive blocking with an Offhand equipment slot'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: 'b886c4e1baa7456eb82b1f0e543fe274cd818468'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Anyone in combat can block with any weapon (or fists) using global `CombatConfigSO` block values; there is no offhand gear, so shields can't exist and blocking has no equipment trade-off.

**Approach:** Add an `Offhand` equipment slot and a `ShieldSO` (armor = `defenseBonus`, block stamina per hit, perfect-block window). Blocking (player and NPC) requires a shield held in hand — a drawn one-handed melee weapon plus an equipped shield. Two-handed weapons and bows occupy the offhand. Ship Round and Kite wood shields, give them to guards / bandits, and stock them at the Storekeeper.

## Boundaries & Constraints

**Always:**
- Equip conflicts resolve automatically: equipping a two-handed weapon / bow moves the shield back to the inventory; equipping a shield while a two-handed weapon / bow is equipped moves that weapon back.
- Shield in the hand opposite the weapon's `drawnHand` only while the weapon visual is drawn; otherwise (sheathed, fists, no weapon) on `BackWeaponSocket`.
- Player block values come from the equipped shield; `blockStaminaCostPerHit` and `perfectBlockWindowDuration` are removed from `CombatConfigSO`.
- NPCs: shield required to guard; its `defenseBonus` adds to `Entity.Armor` in mitigation; stamina / window are player-only (NPCs keep the style's `perfectBlockChance`).
- Values: Round — armor 3, stamina 20, window 0.35 s. Kite — armor 6, stamina 12, window 0.2 s.

**Ask First:** Changing NPC body-armor mitigation (only the offhand item counts); new animator states / parameters.

**Never:** A shield block animation (deferred); shield bash / attacks; shields for unarmed combat; per-hit block-angle rules for the player.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Block with shield | Sword + Kite drawn, RMB | Block raised; window 0.2 s; regular block costs 12 stamina | N/A |
| No shield | Sword drawn, no offhand, RMB | Block refused | `GameLog.Info` reason |
| Fists | Shield equipped, no weapon, in combat | Shield stays on back, block refused | N/A |
| 2H over shield | Shield equipped, equip Flamberge | Shield returns to inventory, Flamberge in Weapon | N/A |
| Shield over 2H / bow | Bow equipped, equip Round shield | Bow returns to inventory, shield in Offhand | N/A |
| Load bad save | Save holds 2H + shield | Weapon kept, shield skipped | `GameLog.Warn` |
| NPC bow swap | Bandit axe+shield switches to bow, then back | Shield bumped to inventory, re-equipped on return to melee | N/A |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Items/EquipmentSlot.cs` -- enum; append `Offhand` (saves store slot names).
- `ScriptableObjects/Items/WeaponSO.cs`, `Weapons/BowSO.cs` -- add `OccupiesOffhand` (archetype `twoHandedGrip`; bow always).
- `Scripts/Inventory/EquipmentSystem.cs` -- slot resolution, conflict bumps, `FitsSlot`, restore conflict check.
- `Scripts/Inventory/EquipmentVisuals.cs` -- weapon visual + sockets; add shield visual.
- `Scripts/Combat/PlayerCombat.cs` -- `OnBlockStarted` / `TryReceiveHit` use config block values today.
- `Scripts/AI/EntityGuard.cs` (`CanGuard`), `Scripts/AI/NpcArmament.cs` (`AdvanceWeaponChange`, `OnWokeUp`), `Scripts/AI/EntityHealth.cs:181` (armor).
- `Scripts/UI/Inventory/EquipmentUI.cs` + `Prefabs/UI/Equipment/EquipmentPanel.prefab` -- slot widgets.
- `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- category + stat rows.
- `Data/NPCs/{Bandit,CompanyGuard,DockGuard,RacketGuard,Warden,WarehouseGuard}/NPC_*.asset` -- `_startingEquipment`.
- Storekeeper `InventorySystem` in `StartingTown.unity` -- shop stock (via `Editor/NPCs/NpcShopEditor` pattern).

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/ShieldSO.cs` -- new `ShieldSO : EquipableItemSO` (`blockStaminaCost`, `perfectBlockWindow`, `heldPose` / `backPose` `WeaponPose`), `[CreateAssetMenu]` -- data carrier.
- [x] `EquipmentSlot.cs`, `WeaponSO.cs`, `BowSO.cs` -- `Offhand` + `OccupiesOffhand` -- slot rule source.
- [x] `EquipmentSystem.cs` -- `ShieldSO` → Offhand; bump conflicting item (add before remove, single `_onEquipmentChanged` raise); `FitsSlot`; `RestoreEquipped` drops a shield conflicting with an offhand-occupying weapon; add `EquippedShield` helper -- equip rules.
- [x] `EquipmentVisuals.cs` -- `RefreshShield` (instantiate `_Visual`, apply poses to `Drawn` / `Sheathed`), re-socket in `SetCombatState` / `RefreshWeapon`; `ShieldInHand` = shield visual exists && drawn && melee weapon visual -- one source of truth for "can block".
- [x] `PlayerCombat.cs` + `CombatConfigSO.cs` -- block gated on `ShieldInHand`; stamina / window from the shield (snapshotted at raise); remove the two config fields -- shield-driven player block.
- [x] `EntityGuard.cs`, `NpcArmament.cs`, `EntityHealth.cs` -- `CanGuard` requires `ShieldInHand`; re-equip a carried shield after returning to a one-handed weapon / waking; add offhand `defenseBonus` to NPC armor -- NPC parity.
- [x] `EquipmentUI.cs`, `EquipmentPanel.prefab` -- `_offhandSlot` widget next to Weapon -- player can see / unequip it.
- [x] `ItemDetailFormatter.cs` + `Tests/EditMode/ItemDetailFormatterTests` -- category "Shield", rows "Block Stamina", "Perfect Block" (s) -- detail panel.
- [x] `Prefabs/Items/Shields/{RoundWoodShield,KiteWoodShield}/` -- `_World` (Rigidbody, ItemPickup, BoxCollider, Interactable) + `_Visual` (`Drawn`/`Sheathed` → `Mesh`, no hitbox) from the Hivemind prefabs; `Data/Items/Shields/Shield_{Round,Kite}Wood.asset` with values above, icons via `Tools/Items/Generate Missing Icons`, catalog synced; tune poses in play mode.
- [x] NPC data + Storekeeper stock -- Kite to the five guard NPCs + Warden, Round to Bandit; one of each in Storekeeper stock.
- [x] `Tests/EditMode/` -- `EquipmentSystem` slot / bump / restore tests; extend existing block tests if any.
- [x] Docs -- `ScriptableObjects/Items/CLAUDE.md`, `Scripts/Inventory/CLAUDE.md`, `Scripts/Combat/CLAUDE.md`, `deferred-work.md` (shield block clip).

**Acceptance Criteria:**
- Given a drawn sword with a shield equipped, when the player sheathes, then the shield moves to the back; when drawing, to the left hand.
- Given a guard NPC with a Kite shield, when it guards a hit, then blocks resolve as before; given the shield removed, it never raises a guard.
- Given a shield equipped, when the game is saved and reloaded, then the shield is still in Offhand.

## Design Notes

`OccupiesOffhand` lives on `WeaponSO` (`archetype != null && archetype.twoHandedGrip`, `BowSO` overrides `true`) so new families need no `EquipmentSystem` change. Shield poses live on each `ShieldSO` (two meshes, no family preset); the held hand is the mirror of the weapon archetype's `drawnHand` (null archetype → left).

## Verification

**Commands:**
- Unity Test Runner EditMode (`run_tests`) -- expected: all pass.
- `read_console` after compile -- expected: no errors.

**Manual checks:**
- Play StartingTown: buy both shields from the Storekeeper, equip each, draw / sheathe, block (stamina cost and PB window in the F-debug overlay), equip Flamberge (shield bumped), fight a guard.

## Suggested Review Order

**Slot rules (entry point)**

- Pure slot resolution: shields → Offhand, armor can't take hand slots.
  [`EquipmentRules.cs:15`](../../Assets/_Game/Scripts/Inventory/EquipmentRules.cs#L15)

- Two-hander / bow and shield push each other out.
  [`EquipmentRules.cs:45`](../../Assets/_Game/Scripts/Inventory/EquipmentRules.cs#L45)

- Which weapons need both hands — archetype flag, bows always.
  [`WeaponSO.cs:74`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L74)

- Equip applies the bump: leave `_equipped` before `AddItem`, one change event.
  [`EquipmentSystem.cs:81`](../../Assets/_Game/Scripts/Inventory/EquipmentSystem.cs#L81)

- Bad save with both: weapon kept, shield back into the inventory.
  [`EquipmentSystem.cs:142`](../../Assets/_Game/Scripts/Inventory/EquipmentSystem.cs#L142)

**Shield in hand (single source of truth)**

- `ShieldInHand` is only true after PlaceShield parented it to the offhand.
  [`EquipmentVisuals.cs:62`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L62)

- Hand vs back decision, socket fallbacks, warn once.
  [`EquipmentVisuals.cs:228`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L228)

- Rebuild only when the shield item changes; re-place otherwise.
  [`EquipmentVisuals.cs:189`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L189)

**Player block**

- Raise gated on a held shield; values snapshotted from it.
  [`PlayerCombat.cs:329`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L329)

- Hit resolution fails closed when the snapshot no longer matches the hand.
  [`PlayerCombat.cs:1207`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1207)

- Equipment refresh lowers a block whose shield left or changed.
  [`PlayerCombat.cs:589`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L589)

**NPC side**

- Guard requires a shield in hand (no armament = never).
  [`EntityGuard.cs:125`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L125)

- Bow swap stows the exact shield; only that one returns.
  [`NpcArmament.cs:339`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L339)

- Loadout equips shields after weapons; conflicting shield is carried.
  [`NpcArmament.cs:265`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L265)

- Pre-shield saves: living NPCs get their starting shield back.
  [`NpcArmament.cs:442`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L442)

- Shield `defenseBonus` added to NPC mitigation.
  [`EntityHealth.cs:184`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L184)

**Data, UI, tests**

- Shield data carrier (stamina, window, poses).
  [`ShieldSO.cs:12`](../../Assets/_Game/ScriptableObjects/Items/ShieldSO.cs#L12)

- Detail rows: Block Stamina, Perfect Block.
  [`ItemDetailFormatter.cs:107`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L107)

- Offhand slot widget wiring.
  [`EquipmentUI.cs:50`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs#L50)

- Slot / bump / restore / hold-rule tests.
  [`EquipmentSystemTests.cs:208`](../../Assets/Tests/EditMode/EquipmentSystemTests.cs#L208)
