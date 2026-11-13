---
title: 'NPC equipment: loadout, armed / fists combat, weapon loot'
type: 'feature'
created: '2026-11-13'
status: 'done'
baseline_commit: 'e1b47d984d07738d032157b70416b9f0fdad2ede'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Entities/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPCs (`NPC_base Variant` on `Humanoid_BaseHero`, player sockets) can't wear or wield anything: they always punch, and `NPCEntity._weapon` is data-only (stance / knockdown, no visual, no hitbox).

**Approach:** Reuse `EquipmentSystem` + `EquipmentVisuals` on NPCs (player channels unwired, refreshed directly) and add one NPC-side owner, `NpcArmament`, that grants the `NPCEntity` starting loadout, picks weapon vs fists per fight, drives draw / sheathe, and drops the weapon into the NPC inventory when it goes down.

## Boundaries & Constraints

**Always:**
- Loadout: guards = `Faction_Company` — CompanyGuard_ArrivalLeft / Right, DockGuard, WarehouseGuard, plus Harl (`NPC_RacketGuard`) and the Warden, moved to Faction_Company. Each gets `Armor_CompanyGrunt` + `Weapon_ShortIronSword`; every other NPC nothing.
- Armor hides the body SMR like on the player; the `Head` SMR is never touched.
- Armed is the default for every fight, brawls included. Fists only on explicit request (`StartBrawl(target, useFists: true)`); today only the arrival sequence's Warden → Tam beating asks.
- Exactly one of `Unarmed` / `Weapon` hitbox registered on `EntityMeleeAttacker` at a time (clip events with `""` open all).
- Armed = weapon's animator override (sword clips); fists = default controller. Every controller swap re-pushes `IsInCombat`. Never swap the controller while unconscious, dying / dead or mid-attack.
- Armed draws on combat entry / sheathes on exit (draw / sheathe clips); the visual moves on `WeaponSwap`, with a timeout fallback. No attack while switching.
- Armed damage = `Entity.AttackDamage + weapon.damageBonus`; stance + knockdown from the weapon actually wielded; fists never knock down.
- Knocked out / killed → weapon unequipped into the NPC inventory (lootable), armor stays equipped (not lootable). Wakes with it still there → re-equips; looted → fists from then on.

**Ask First:** any change to faction hostility lists; giving Brann equipment.

**Never:** saving NPC equipment; touching Brann (unpacked, Mixamo dummy); Animator culling changes; wiring `OnEquipmentChanged` / `OnVisualsRefreshed` on NPCs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Guard spawns | Guard NPCEntity loadout | Armor visible, body hidden, sword on hip, `Weapon` hitbox registered, sword override controller | Item not equippable → warn, skipped |
| Hostile fight | Bandit spotted | Draw clip → sword in hand at `WeaponSwap` → sword combo, damage 10+bonus | No `WeaponSwap` → timeout moves it |
| Toll brawl | `StartBrawl(player)` | Harl draws and fights armed; player knocked out, not killed | — |
| Warden beating | `StartBrawl(Tam, useFists: true)` | Default controller, `Unarmed` hitbox, no draw; weapon mode restored when the fight ends | — |
| Knockout | Guard knocked out | Sword → inventory (Loot prompt), armor kept, no controller swap while down | — |
| Wake, sword kept / looted | Get-up ends | Kept → re-equipped (sheathed, armed); looted → fists, default controller | — |
| Death | Guard killed | Sword → corpse inventory, armor stays on the corpse | — |
| No loadout / no armament | Worker, Brann | Unchanged unarmed behaviour | — |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` -- data-only `_weapon` + `ResolveAttackStance` to replace.
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- virtual `ResolveAttackStance(step, hits, out kd)`.
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` / `EquipmentVisuals.cs` -- player equipment; Awake warns on unwired player channels; `Refresh()` always reassigns the controller.
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `SetCombatState`, `StartBrawl`, `ExecuteAttack`, `HandleAttack`, `HandleKnockedOut`, `HandleRegainedConsciousness`, `TransitionToDead`.
- `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs` -- `Register/UnregisterHitbox`, `ResolveStanceDamage`.
- `Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs` -- needs `WeaponSwap()`.
- `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs` -- bridge already has `PlayWeaponSwitch` / `ResetWeaponSwitchTriggers`.
- `Assets/_Game/Scripts/World/Story/ArrivalSequence.cs:444` -- Warden's `StartBrawl`.
- `Assets/_Game/Prefabs/Entities/NPC_base Variant.prefab` -- root `InventorySystem`, `EntityMeleeAttacker [Unarmed]`; nested `Humanoid_BaseHero` (sockets, `base_hero` / `Head`, default controller `Humanoid_Base`).
- `Assets/_Game/Data/NPCs/*/NPC_*.asset` -- 6 guard loadouts; Warden + RacketGuard `_faction` → Company.

## Tasks & Acceptance

**Execution:**
- [x] `Entity.cs`, `NPCEntity.cs` -- `ResolveAttackStance(step, hits, WeaponSO wielded, out kd)` (Entity ignores `wielded`); replace `_weapon` with `_startingEquipment` (`List<EquipableItemSO>`, `StartingEquipment`).
- [x] `EquipmentSystem.cs` -- `_npcOwned` flag silences the `_onEquipmentChanged` / `_playerStats` warnings.
- [x] `EquipmentVisuals.cs` -- `Refresh(bool applyAnimatorController = true)`; `ForceDefaultController(bool)` (fists) honoured by every controller apply.
- [x] `AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs` -- `TriggerWeaponSwitch(draw, speed)` / `ClearWeaponSwitch()`.
- [x] `EntityMeleeAttacker.cs` -- `HasHitbox(id)`; `WieldedWeapon` (set by armament) passed to `ResolveAttackStance`.
- [x] `Scripts/AI/NpcArmament.cs` (new, `Game.AI`) -- loadout in `Start` (alive only: add to inventory + equip + `Refresh`), mode apply (hitbox swap, controller, re-push `IsInCombat`), draw / sheathe + `CombatConfigSO.weaponSwitchTimeout` watchdog, `DamageBonus`, `IsSwitchingWeapon`, `RequestFists`, `OnCombatChanged`, `OnDowned` (unequip weapon, `Refresh(false)`), `OnWokeUp` (re-equip if still held, full refresh).
- [x] `EntityAnimationEventReceiver.cs` -- `WeaponSwap()` → armament (optional, resolved from parents).
- [x] `EntityBrain.cs` -- `StartBrawl(target, useFists = false)`; call armament from `SetCombatState`, knockout, wake, death; `HandleAttack` waits while switching; `ExecuteAttack` adds `DamageBonus`.
- [x] `ArrivalSequence.cs` -- Warden beating passes `useFists: true`.
- [x] `NPC_base Variant.prefab` -- add + wire `EquipmentSystem` (`_npcOwned`), `EquipmentVisuals` (5 sockets, `base_hero`, `mixamorig:Hips`, Animator, `Humanoid_Base`), `NpcArmament`.
- [x] NPC data -- 6 guard loadouts; Warden + RacketGuard faction → Company.
- [x] `Assets/Tests/EditMode/NpcArmamentTests.cs` -- stance with wielded weapon / fists, down → weapon in inventory + armor kept, wake re-equip vs looted.
- [x] Docs -- `Scripts/AI/CLAUDE.md` (armament, armed vs fists, loot rule), `Prefabs/Entities/CLAUDE.md` (new components); `deferred-work.md` known gaps: equipment not saved (save mid-knockout can duplicate the sword), Brann, Animator culling.

**Acceptance Criteria:**
- Given StartingTown in play mode, when the scene loads, then every guard shows the Company armor with a sheathed sword and no worker is equipped.
- Given a guard knocked out by the player, when he is looted of the sword and gets up, then he brawls with fists and the default controller.
- Given the arrival sequence, when the Warden beats Tam, then he punches (no draw) and Tam is knocked out, not killed.
- Given the full EditMode suite, when run, then all tests pass.

## Design Notes

Faction move side effects: `Faction_Company` and `Faction_Neutral` have identical hostility (both hostile only to Monsters + Bandits; nobody else lists them), so hostility to the player / workers and every knockout pair (Warden → Tam, Harl → player) are unchanged. One real change: `RA_Docks` (owner faction Company, 21–06) now also has Harl and the Warden as owners — they chase a night trespasser on the docks they can see.

Controller swaps reset all animator params and state, so a swap at knockout would stand the NPC up: the weapon leaves via `Refresh(false)` and the full refresh waits for `RegainedConsciousness`. The corpse never gets a full refresh.

## Verification

**Commands:**
- `run_tests` (EditMode, MCP) -- expected: all pass.

**Manual checks:**
- Play StartingTown: guards armored + sword on hip; provoke Harl's toll brawl (draw, sword hits, knockout); loot his sword while down, watch him wake with fists; arrival sequence beating is fists.

## Suggested Review Order

**Armament core (one owner for loadout, mode, draw / sheathe, loot)**

- Entry point: the component contract — read the summary first.
  [`NpcArmament.cs:30`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L30)

- Mode = hitbox swap + controller + wielded weapon; never both hitboxes registered.
  [`NpcArmament.cs:253`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L253)

- Controller swaps wait for attacks / stance reactions to end.
  [`NpcArmament.cs:143`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L143)

- Down: weapon to inventory, controller kept; wake: re-equip or fists, full rebuild.
  [`NpcArmament.cs:203`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L203)

- Start / re-enable rebuild: visuals, post-swap fixes, mode, draw sync.
  [`NpcArmament.cs:115`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L115)

**Brain integration**

- Combat state drives draw / sheathe and ends a fists request.
  [`EntityBrain.cs:87`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L87)

- Fists only on explicit request; armed is the default.
  [`EntityBrain.cs:1329`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1329)

- Armed damage = AttackDamage + weapon bonus.
  [`EntityBrain.cs:1604`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1604)

- No swing while the sword is still being drawn.
  [`EntityBrain.cs:667`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L667)

- Knockout / wake / death hooks.
  [`EntityBrain.cs:1545`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1545)

**Combat pipeline**

- WeaponSwap reads the firing state so a reversed switch ignores the old clip.
  [`EntityAnimationEventReceiver.cs:75`](../../Assets/_Game/Scripts/AI/EntityAnimationEventReceiver.cs#L75)

- Stance / knockdown from the weapon actually wielded.
  [`EntityMeleeAttacker.cs:310`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L310)

- Controller swap restarts states without SMB exits — clear the counter.
  [`EntityMeleeAttacker.cs:201`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L201)

- NPC stance: base + wielded weapon; fists never knock down.
  [`NPCEntity.cs:45`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L45)

**Shared equipment (player blast radius)**

- `Refresh(false)` keeps the controller for downed NPCs.
  [`EquipmentVisuals.cs:110`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L110)

- Fists with a weapon equipped; same-controller assign now skipped (player too).
  [`EquipmentVisuals.cs:269`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L269)

- NPC flag silences player-channel warnings.
  [`EquipmentSystem.cs:34`](../../Assets/_Game/Scripts/Inventory/EquipmentSystem.cs#L34)

**Peripherals**

- The Warden beats Tam with fists.
  [`ArrivalSequence.cs:445`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L445)

- Loot rule + stance tests.
  [`NpcArmamentTests.cs:1`](../../Assets/Tests/EditMode/NpcArmamentTests.cs#L1)

- Prefab wiring (`NPC_base Variant`) and 6 guard assets (loadout + Faction_Company): see the Code Map.
