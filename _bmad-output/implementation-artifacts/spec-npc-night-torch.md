---
title: 'NPC night torch — routine flag, combat shield swap, loot'
type: 'feature'
created: '2026-11-25'
baseline_commit: '4be54eedf54d24927901c2e21e7098b793263f06'
status: 'done'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/RANGED_EQUIPMENT.md'
  - '{project-root}/Assets/_Game/Scripts/AI/NPC/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The torch (spec-torch-offhand-item) is player-only. Guards awake at night walk in the dark, and
`NpcArmament` handles shields only: a torch equipped on an NPC would stay lit and unlootable when it goes down.

**Approach:** A `torchAtNight` flag on `NpcRoutine`. At night (sunset → sunrise, `DayNightConfig`) and while not asleep,
the NPC holds a torch in its offhand (granted if it has none) and its daytime offhand item (shield) goes to its
inventory; by day the torch goes back to the inventory and the shield returns. In a fight it holds the shield instead,
and takes the torch back when the fight ends.

## Boundaries & Constraints

**Always:**
- Night = `!DayNightMath.IsDay(hour, sunrise, sunset)` from `DayNightConfigSO` (fallback 6 / 22); no clock → no torch.
- Asleep (brain `IsAsleep`) → no torch; woken at night and up → torch.
- Torch granted only when the inventory holds none (`Item_Torch`); looted torches may be re-granted (accepted farm).
- Fight with an own shield carried: shield replaces the torch at the draw clip's `WeaponSwap`; torch back at the sheathe
  `WeaponSwap`. No own shield, fists or no weapon → keeps the torch in the fight (user, review 2026-11-25).
- Down (knockout / death): the torch goes into the inventory (lootable), like the shield. Wake at night → torch again.
- The shield re-equip paths (`EquipCarriedShield`, `ReEquipCarriedStartingShield`) never replace a held torch.
- Dusk / dawn swaps are instant (no clip). NPC torch lights cast no shadows (light budget).
- Flag on: DockGuard, WarehouseGuard, CompanyGuard_ArrivalLeft / Right, RacketGuard (Harl), BarnGuard (Venn).

**Ask First:** Changes to the save format (`ObjectSaveData`); any change to the player's torch behaviour.

**Never:** Torch equip / unequip animations (later); world pickups; burn time; NPCs other than the six above.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dusk | Guard, flag on, 22:00 | Torch in hand, lit; shield in inventory | No torch item wired → warn once, none |
| No torch carried | Inventory has none at dusk | One `Item_Torch` added, then equipped | N/A |
| Dawn | 06:00 | Torch to inventory; shield back in offhand | N/A |
| Night fight | Player attacks a torch guard | Draw → shield in hand at `WeaponSwap`; win / flee → sheathe → torch back | N/A |
| Night fight, no shield | NPC with no shield, or a fists brawl | Fights with the torch in hand | N/A |
| Down at night | Knockout / death | Torch + weapon + shield in loot; light gone | N/A |
| Wake, torch looted | Night | New torch granted and held | N/A |
| Asleep | Venn in bed 03–06 | No torch while lying; torch if woken at night | N/A |
| Save / load at night | Torch equipped | Loads holding it; shield stays in inventory | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/NPC/NpcRoutine.cs` -- poll loop; add flag, config, `NpcArmament.SetTorchWanted` each evaluation
- `Scripts/AI/NpcArmament.cs` -- offhand owner: `_stowedShield`, `EquipCarriedShield`, `OnCombatStateChanged`, `CompleteWeaponSwitch`, `OnDowned` / `OnWokeUp`, `RestoreLoadout`
- `Scripts/AI/EntityBrain.cs` -- `IsAsleep`; calls `OnCombatStateChanged` / `OnDowned` / `OnWokeUp`
- `Scripts/Inventory/EquipmentVisuals.cs` + `TorchLight.cs` -- torch visual; add NPC no-shadow switch
- `Scripts/Core/Time/DayNightMath.cs`, `ScriptableObjects/Config/DayNightConfigSO.cs` -- night rule
- `Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- `NpcArmament` torch item, visuals shadow flag
- `Scenes/StartingTown.unity` -- the six `NpcRoutine` instances

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/AI/NpcArmament.cs` -- `[SerializeField] TorchSO _nightTorch`; `SetTorchWanted(bool)`; one `ApplyOffhand()` deciding torch vs own shield (pure static `ShouldHoldTorch(wanted, combatOffhand, hasOwnShield)`); combat-offhand flag flipped at draw / sheathe completion (or at once when unarmed / fists); torch unequipped on down; shield re-equip paths skip a held torch; restore keeps a saved torch and remembers the shield as stowed -- offhand ownership
- [x] `Scripts/AI/NPC/NpcRoutine.cs` -- `_torchAtNight`, `_dayNight` config; pure `NpcTorchRules.WantsTorch(flag, hour, sunrise, sunset, asleep)`; push to the armament every poll and on time skip -- the routine decides night
- [x] `Scripts/Inventory/TorchLight.cs`, `EquipmentVisuals.cs` -- `_torchShadows` flag (false on NPCs) applied to the built torch light -- light budget
- [x] `NPC_base Variant.prefab` + StartingTown six instances -- wire `_nightTorch` = `Item_Torch`, shadows off, flag + config on -- data
- [x] `Assets/Tests/EditMode/` -- `WantsTorch` night / day / asleep / wrap; `ShouldHoldTorch`; armament dusk / dawn swap, grant only when none, down drops torch, wake re-grants, shield path never replaces the torch -- regression
- [x] Docs -- `RANGED_EQUIPMENT.md` (NPC equipment), `NPC/CLAUDE.md` (routine flag + cast), `deferred-work.md` (strike done notes, torch clip follow-up) -- knowledge

**Acceptance Criteria:**
- Given DockGuard at 23:00, when the player watches his deck patrol, then he walks with a lit torch in his left hand.
- Given that guard attacked at night, when the fight ends with the player fled, then he sheathes and holds the torch again.
- Given the EditMode suite, when run, then it passes.

## Spec Change Log

- **2026-11-25, review (edge-case hunter) → user decision:** the frozen rule swapped the torch for the shield "immediately
  with no wielded weapon", but a shield is only held next to a drawn weapon, so a fists brawl at night put the torch away
  for a shield on the back (no block, no armor). User chose to keep the torch in fists fights / with no weapon; the Always
  rule and the matrix row were amended. Patches: the fight swap is a latch (`_fightOffhand`) so a melee ↔ bow switch no
  longer flashes the torch between clips; only the night torch is ever taken off; no swap while the NPC is inactive.
  KEEP: `SyncOffhand` as the single offhand decision point, called from `ApplyMode` / `CompleteWeaponSwitch`.

## Design Notes

`_stowedShield` keeps its meaning ("my own shield, in the inventory, back in the offhand when free"); the torch just
counts as "not free". `ApplyOffhand` runs on each `SetTorchWanted` change, combat-offhand flip, wake and restore:

```
holdTorch = wanted && !(combatOffhand && OwnShieldCarried)
holdTorch  → shield in offhand? _stowedShield = it; grant torch if none; Equip(torch)
!holdTorch → offhand is a torch? Unequip it; EquipCarriedShield()
```
Down NPCs only store the request; `OnWokeUp` applies it.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all pass; `read_console` -- no errors

**Manual checks:**
- Play StartingTown at night: guards hold torches, fight one (shield at the draw, torch after), knock one out and loot
  the torch, skip to day (shields back).

## Review (2026-11-25)

Three reviewers (blind, edge case, acceptance). Patched: fists / unarmed fights keep the torch (user), fight latch, night
torch only, inactive guard, tests for the armed draw / sheathe path, the shield re-equip path, restore and the loot. Rejected:
torch next to a two-hander (allowed by `CoexistsWithTwoHands`), "saved torch kept by day" (the restore rebuild re-syncs), a
1 s stale state after a load / re-enable, the night loss of the shield's armor bonus (by design: the shield is in the
inventory), blanket `ignoreFailingMessages` in the tests (state assertions carry them). Play-checked in StartingTown: five
guards lit at 23:00 (Venn asleep), shield at the draw and torch back after the brawl, torch in the knockout loot, re-held on
wake, shields back at 07:00 with one torch each.

## Suggested Review Order

**Offhand decision**

- Entry point: the one place that picks torch vs own shield, grants, and refreshes the offhand only.
  [`NpcArmament.cs:620`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L620)

- Fight latch: shield once a weapon is drawn in combat, until sheathed out of combat.
  [`NpcArmament.cs:606`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L606)

- Pure rule: wanted and not (fighting with a drawn weapon and an own shield).
  [`NpcArmament.cs:161`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L161)

- Routine request; stored until the loadout exists.
  [`NpcArmament.cs:376`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L376)

- Draw / sheathe WeaponSwap re-syncs the offhand.
  [`NpcArmament.cs:566`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L566)

**Shield and loot paths**

- Down: the held torch joins weapon and shield in the loot.
  [`NpcArmament.cs:429`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L429)

- Shield re-equip needs an empty offhand, so it never replaces the torch.
  [`NpcArmament.cs:489`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L489)

- Restore: a saved torch keeps the offhand, the starting shield is stowed.
  [`NpcArmament.cs:507`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L507)

**Night rule (routine)**

- Polls the clock, the config hours and `IsAsleep`, pushes the request.
  [`NpcRoutine.cs:123`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L123)

- Night = outside sunrise → sunset, awake, valid day.
  [`NpcTorchRules.cs:18`](../../Assets/_Game/Scripts/AI/NPC/NpcTorchRules.cs#L18)

**Visuals and light**

- Offhand-only rebuild: no weapon visual, hitbox or controller change mid-clip.
  [`EquipmentVisuals.cs:197`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L197)

- NPC torches without shadows (light budget).
  [`EquipmentVisuals.cs:375`](../../Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs#L375)

- Shadow switch on the light.
  [`TorchLight.cs:68`](../../Assets/_Game/Scripts/Inventory/TorchLight.cs#L68)

**Docs and tests**

- Night torch rules next to the NPC equipment rules.
  [`RANGED_EQUIPMENT.md:86`](../../Assets/_Game/Scripts/AI/RANGED_EQUIPMENT.md#L86)

- Night rule, hold rule, dusk / dawn, fights, down / wake, restore, authored torch.
  [`NpcNightTorchTests.cs:12`](../../Assets/Tests/EditMode/NpcNightTorchTests.cs#L12)
