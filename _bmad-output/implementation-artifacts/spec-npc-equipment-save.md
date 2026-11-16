---
title: 'Save NPC equipment so knockout, loot and reload never duplicate or restore weapons'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
baseline_commit: '7366e461fd59bff5584152d7409fd7152f98111d'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPC equipment is not saved. On every region load `NpcArmament.Start` re-grants the loadout, and later `SaveableObject.Restore` restores the saved inventory. A save made while a guard lies knocked out (his sword dropped into his inventory) therefore loads with one sword equipped **and** one in his inventory. A looted sword comes back on reload. A save-restored corpse can get the same duplicate.

**Approach:** `SaveableObject` also captures and restores the sibling `NpcArmament`: the equipped slots (item ids) plus the weapon dropped while down. A restored loadout replaces the granted one, and `Start` no longer grants over it. Knockout state is not saved, so a living NPC saved while down loads standing and picks his dropped weapon back up if it is still in his inventory, exactly like `OnWokeUp`.

## Boundaries & Constraints

**Always:** Equipped items never sit in the inventory too, after any capture → restore order (restore runs one frame after every `Start`; a hidden corpse's `Start` may run after its restore). Item ids resolve through `SaveSystem.Instance.Catalog`, and unknown ids / slots are skipped with a warning. Restore raises no gameplay events. Slot ↔ id conversion is shared between the player and NPCs (one converter in `Core/Save`).

**Ask First:** Saving the knockout state itself (an NPC loading still unconscious); bumping `SAVE_FORMAT_VERSION`.

**Never:** Change `EquipmentSystem` semantics for the player; persist AI / combat / draw state; touch the revive-after-death path (`EntityHealth.OnEnable` also fires when a corpse is re-activated, so re-equipping there would arm corpses — it stays deferred).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Plain guard | Saved: armor + sword equipped, no dropped | Loads armor + sword equipped, inventory as saved, no extra sword | N/A |
| Saved while knocked out | Saved: armor equipped, dropped = sword, sword in inventory | Loads standing, sword re-equipped and gone from the inventory (one sword) | N/A |
| Looted while down | Saved: armor equipped, dropped = sword, sword not in inventory | Loads on fists; reload again stays fists | N/A |
| Killed guard (corpse) | Killed fact set, saved: armor equipped, sword in corpse inventory | Corpse wears the armor, sword lootable once, no extra armor / sword | N/A |
| Old save | `equipped` absent (null) in the object data | Loadout granted as today | Silent fallback |
| Unknown item id | Saved slot holds an id missing from the catalog | Slot skipped, rest restored | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/SaveableObject.cs` -- captures / restores sibling state; restore runs after `Start` (corpses re-activated here)
- `Assets/_Game/Scripts/AI/NpcArmament.cs` -- grants loadout in `Start`, `_droppedWeapon` on down, `OnWokeUp` re-equip, `Rebuild`
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` -- `Equipped`, `RestoreEquipped` (replaces without touching the inventory), `Equip(ItemSO)` (takes from inventory)
- `Assets/_Game/Scripts/Core/Save/SaveData.cs` -- `ObjectSaveData`; `PlayerSaveData.equipped` is `Dictionary<string,string>` slot → itemId
- `Assets/_Game/Scripts/Core/Save/ItemStackConverter.cs` -- converter pattern to mirror
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` -- player equipment capture loop + private `ResolveEquipped` (move into the converter)
- `Assets/_Game/Scripts/Core/Save/SaveSystem.cs` -- `ApplyRegion` → `SaveableObject.Restore`
- `Assets/Tests/EditMode/SaveDataSerializationTests.cs` -- round-trip test to extend

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Core/Save/EquipmentSaveConverter.cs` -- new static `ToSave(IReadOnlyDictionary<EquipmentSlot,ItemSO>)` → `Dictionary<string,string>` and `FromSave(dict, catalog)` → `Dictionary<EquipmentSlot,ItemSO>` (logic moved from `PlayerSaveAdapter`) -- one shared conversion
- [x] `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` -- use the converter; delete `ResolveEquipped` -- no duplication, no behaviour change
- [x] `Assets/_Game/Scripts/Core/Save/SaveData.cs` -- `ObjectSaveData` gets `Dictionary<string,string> equipped` (null = no `NpcArmament` / old save) and `string droppedWeaponId` -- persisted loadout
- [x] `Assets/_Game/Scripts/AI/NpcArmament.cs` -- add capture accessors (`HasLoadout`, `Equipped`, `DroppedWeapon`) and `RestoreLoadout(equipped, droppedWeapon, bool killed)`: `RestoreEquipped`, then (not killed) re-equip the dropped weapon if the inventory holds it, clear `_droppedWeapon`, set `_loadoutRestored` (`Start` skips `GrantLoadout`), and `Rebuild()` when already started and active; guard on `_ready` -- saved loadout replaces the granted one
- [x] `Assets/_Game/Scripts/World/SaveableObject.cs` -- cache `NpcArmament`; capture `equipped` / `droppedWeaponId` only once `HasLoadout` (null before `Start`); skip the loadout restore with an error when the catalog is missing (authored loadout kept); in `Restore`, after the inventory, call `RestoreLoadout` when `data.equipped != null`, passing `IsKilled()` -- wire it in
- [x] `Assets/Tests/EditMode/EquipmentSaveConverterTests.cs` -- round trip, unknown id skipped, unknown slot skipped, null input → empty; extend the serialization round trip with `equipped` / `droppedWeaponId` -- cover conversion + format
- [x] `Assets/_Game/Scripts/AI/CLAUDE.md` -- replace the **Not saved** bullet with the save rules (restore replaces the granted loadout, down-at-save → standing + re-equip) -- keep docs accurate
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- strike "NPC equipment is not saved" -- close the backlog item

**Acceptance Criteria:**
- Given a guard knocked out with his sword in his inventory, when the player saves and loads, then the guard stands with exactly one sword (equipped) and none in his loot.
- Given the player looted a knocked-out guard's sword and saved, when the save loads (twice in a row), then the guard fights with fists and has no sword.
- Given a killed guard was saved with his sword unlooted, when the save loads, then the corpse wears one armor set and its loot holds exactly one sword.

## Design Notes

Restore timing: `SceneLoader` raises `RegionLoaded` one frame after the load, so live NPCs already ran `Start` (loadout granted). `RestoreSlots` replaces the inventory and `RestoreEquipped` replaces the slots without returning items, so the granted copies just disappear. A killed NPC hidden by `PersistentID.Start` may run `Start` only when `Restore` re-activates it; `_loadoutRestored` stops it granting armor a second time.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass, incl. `EquipmentSaveConverterTests`, `SaveDataSerializationTests`

**Manual checks (if no CLI):**
- Play After Arrival: knock out DockGuard, F5, F9 → one sword equipped, none in loot; knock out again, loot the sword, F5, F9 → fists.

## Suggested Review Order

**Restore replaces the granted loadout**

- Entry point: saved equipment replaces `Start`'s loadout; down-at-save re-equips like waking.
  [`NpcArmament.cs:274`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L274)

- Restored before `Start` (hidden corpse) → no second grant.
  [`NpcArmament.cs:129`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L129)

- Re-equip only if the inventory still holds it (looted → fists).
  [`NpcArmament.cs:285`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L285)

**Save wiring**

- Runs after the inventory restore; null `equipped` = old save → granted loadout kept.
  [`SaveableObject.cs:101`](../../Assets/_Game/Scripts/World/SaveableObject.cs#L101)

- Missing catalog skips the restore instead of stripping the NPC bare.
  [`SaveableObject.cs:114`](../../Assets/_Game/Scripts/World/SaveableObject.cs#L114)

- Capture only once a loadout exists, so a pre-`Start` capture stays null.
  [`SaveableObject.cs:77`](../../Assets/_Game/Scripts/World/SaveableObject.cs#L77)

- Capture gate definition.
  [`NpcArmament.cs:84`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L84)

**Shared conversion and format**

- Slot ↔ itemId, moved out of the player adapter; unknown slots / ids skipped with a warning.
  [`EquipmentSaveConverter.cs:33`](../../Assets/_Game/Scripts/Core/Save/EquipmentSaveConverter.cs#L33)

- Player adapter now uses the converter (no behaviour change).
  [`PlayerSaveAdapter.cs:138`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L138)

- Two optional fields; no format version bump.
  [`SaveData.cs:100`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L100)

**Tests**

- Converter round trip and skip cases.
  [`EquipmentSaveConverterTests.cs:48`](../../Assets/Tests/EditMode/EquipmentSaveConverterTests.cs#L48)

- Serialization round trip + null-when-absent for the new fields.
  [`SaveDataSerializationTests.cs:1`](../../Assets/Tests/EditMode/SaveDataSerializationTests.cs#L1)
