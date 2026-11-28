---
title: 'Lock the Area 1 smuggling route (SmugglersTunnel P1–P3) and add a Key item type'
type: 'feature'
created: '2026-11-28'
status: 'done'
baseline_commit: '0dec5c728fe595630a5f9bb5c24a59636bd48686'
context:
  - '{project-root}/docs/World/Quests/SmugglersTunnel.md'
  - '{project-root}/Assets/_Game/Prefabs/World/GangHouse/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The smuggling route (warehouse cellar → tunnel → gang basement) is wide open: the tunnel has no doors, the cellar and hidden doors are unlocked, and the keys, key holders and the dock tally evidence of SmugglersTunnel P1–P3 don't exist. Keys are plain quest-flagged `ItemSO`s with no type of their own.

**Approach:** Add a `KeyItemSO` type (never parted with, worth 0, Misc tab, category "Key") and convert the quarry key. Builders hang two new tunnel doors and lock all four route doors (Lockpicking 1 + key). New keys and the tally are placed in NPC inventories and owned chests, and those chests set WorldFacts.

## Boundaries & Constraints

**Always:**
- Generated prefabs (`GangHouse`, `Warehouse`) change only through their script and builder. Locks, keys and skill are set by the builders, never as scene overrides (a rebuild would orphan them).
- Every door is out of the NavMesh bake (`NavMeshModifier.ignoreFromBuild`) and has a fixed save id.
- All four locks: `_isLocked` true, `_requiredSkill` = `Skill_Begginer_Lockpicking`.
- New scene objects go under a hand-owned root `SmugglersTunnel`, never under `Area1_Blockout`. Each owned chest gets a `PersistentID` + `KilledFact` (the `HobbsBox` pattern).
- `KeyItemSO`: `CanPlayerPart` false (no drop, sell, container put or action bar), buy and sell forced to 0 in `OnValidate`, stackable copies allowed.

**Ask First:** moving the tunnel line or a room; touching the terrain; any change to `StartingTownTerrain.asset`, the TMP font asset or `Assets/_Recovery/` (pre-existing dirty files: leave and never stage them).

**Never:** NPC door use (P10), route triggers (P4), the gate guard / doorman NPCs (P8), restricted areas, a rebake beyond what the builds need.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No key, no skill | Player at any of the 4 doors | Door stays shut, missing-skill toast | Nothing consumed |
| Skill, no pick | Lockpicking 1, 0 picks | Missing-lockpick toast | Nothing consumed |
| Pick | Lockpicking 1 + ≥1 pick | Opens, one pick used, stays unlocked | — |
| Key | Holds the door's key | Opens, nothing used, no skill needed | — |
| Gang tunnel key | `Key_GangTunnel` | Opens both tunnel doors, not the hidden or cellar door | — |
| Key in UI | Any `KeyItemSO` | Misc tab, "Key" category, no Drop / Sell / Put / bind | — |
| Take tally / key copy | Take from basement crate / guard chest | Its WorldFact set once | Other items set nothing |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Items/ItemSO.cs` -- `CanPlayerPart` (single parting rule), `OnValidate`
- `Scripts/UI/Inventory/ItemFilter.cs`, `ItemDetailFormatter.cs` -- tab and category switches
- `Scripts/Inventory/ActionBarSystem.cs:105`, `Scripts/Crafting/CookingRules.cs:28`, `ScriptableObjects/Items/GiveItemCategory.cs:22` -- direct `isQuestItem` reads, which should go through the parting rule
- `Data/Items/Quest/Item_Key_QuarrySouthDoor.asset` -- the only existing key
- `Art/Environment/Camp/models/gang house/Source~/build_gang_house.py` -- `basement()` mouth frame (≈1.59 m wide on the west face, 2.0 m high), `base()` hidden door, `self.door` parts entry
- `Editor/World/GangHouseBuilder.cs` -- `BuildDoor` (HiddenDoor), `TryReadHouse`
- `Editor/World/WarehouseBuilder.cs` -- `BuildDoor` (CellarDoor), reads `cellar.tunnel` = (ta, tb, height, outer z, inner z)
- `Tests/EditMode/GangHousePrefabTests.cs`, `WarehousePrefabTests.cs`, `ItemFilterRulesTests.cs`, `ItemDetailFormatterTests.cs`, `QuestItemTests.cs`
- Scene: `HiddenStashQuest/BrannsStash`, `WarehouseGuard`, `Area1_Blockout/CompoundGate/GuardPost` (lean-to + `Bedroll` at (−36.8, 2.62, 53.9), the Dock Guard's post), `Area1_Blockout/Garrison/Barracks/Bunk_1`, `HobbsBox` (owned-chest template)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/KeyItemSO.cs` -- new `KeyItemSO : ItemSO` (menu Items → Key), `OnValidate` override forcing buy/sell 0 -- the Key type
- [x] `ItemSO.cs` + the 3 direct readers -- `CanPlayerPart` false for `KeyItemSO`; the readers use the rule -- one parting rule
- [x] `ItemFilter.cs`, `ItemDetailFormatter.cs` -- `KeyItemSO` → Misc / "Key" (after the quest-item arm) -- UI
- [x] `Item_Key_QuarrySouthDoor.asset` -- swap `m_Script` to `KeyItemSO`, `isQuestItem` 0 -- keeps `itemId` and refs
- [x] `Data/Items/Quest/` -- `Key_WarehouseCellar`, `Key_GangTunnel` (`maxStacks` 5), `Key_GangBasement` (`KeyItemSO`, quarry key icon as placeholder, no world prefab); `Item_DockTally` (quest item, 0 value, description = the three beat-4 anecdotes); `ItemCatalog` synced -- items
- [x] `Data/Facts/` -- `WorldFact_Smuggling_DockTallyTaken`, `_DockGuardKeyFound`, `_GateGuardKeyFound` -- facts
- [x] `build_gang_house.py` -- `tunnel_door()` picks the last shoring frame outside the west wall whose leaf swing fits, writes a `tunnel.door` parts entry (position, yaw, scaleY, openAngle), moves `TunnelLantern_East` one frame back; no geometry; run in the Blender MCP session -- the basement-end door's place (see Spec Change Log)
- [x] `GangHouseBuilder.cs` -- `BuildDoor` takes name / save id / key; builds `HiddenDoor` (`Key_GangBasement`) and `TunnelDoor_Basement` (`Key_GangTunnel`, `area1-gang-house-tunnel-door`), both locked -- doors
- [x] `WarehouseBuilder.cs` -- `CellarDoor` locked (`Key_WarehouseCellar`); new `TunnelDoor_Cellar` from `cellar.tunnel` on the side room's inner face, opening into the room, `Key_GangTunnel`, `area1-warehouse-tunnel-door` -- doors
- [x] Tests -- new door tests (present, locked, key, skill, out of bake, unique save ids) in both prefab test files; Key rules in a new `KeyItemTests` (category, filter, parting, action bar, every key asset a worth-0 `KeyItemSO`) -- coverage
- [x] Rebuild both prefabs and Sites if needed, rebake; scene: key in `WarehouseGuard` inventory; `Key_GangTunnel` + `Key_GangBasement` added to `BrannsStash`; root `SmugglersTunnel` with `DockGuardBed` + `DockGuardChest` at the CompoundGate lean-to (owner `NPC_DockGuard`), `GateGuardBed` + `GateGuardChest` on `Bunk_1` (unowned until P8), `BasementCrate` in the basement (owner `NPC_StashOwner`), each chest with a `ContainerTakeFact`; `Validate Save IDs`; save -- holders
- [x] Docs -- GangHouse/Warehouse CLAUDE.md (doors, locks), `ScriptableObjects/Items/CLAUDE.md` (KeyItemSO), quest spec P1–P3 marked done, `deferred-work.md` (gate guard ownership → P8) -- docs

**Acceptance Criteria:**
- Given the scene in Play, when the player tries each of the 4 doors, then the I/O matrix holds (key, pick at Lockpicking 1, refusal).
- Given a rebuild of either prefab, when it's placed again, then the locks, keys and save ids are unchanged and nobody has to fix the scene by hand.
- Given the EditMode suite, when run, then all tests pass.

## Spec Change Log

- **2026-11-28, review (acceptance auditor): the basement-end door's place.** The plan put the door in the basement's mouth on the west face, with a jamb post and infill (`basement.mouthDoor`). Measured during implementation: the mouth is a 1.59 m slanted opening, and only 0.3 m of landing stands between it and the stair treads, so an inward leaf swings into the treads and an outward one into the slanted passage through the 1.5 m wall. Amended: the door hangs on the last shoring frame of the tunnel outside the wall (`tunnel.door`), which needs no new geometry. Known-bad state avoided: a leaf clipping the stair treads, or a model change for a jamb + infill. KEEP: `tunnel_door()` picking the frame from the leaf's swing room, `TunnelLantern_East` moved off the door's frame, `TunnelDoor_StandsAcrossTheTunnel_OutsideTheWall_AndSwingsClear`. Awaiting the user's sign-off at presentation.

## Design Notes

- **The cellar-end tunnel door goes in the Warehouse prefab, not the gang house's.** The opening, its jambs (clear width 1.0 m) and its parts data are the warehouse's. The 1.1 m leaf sits on the side room's inner face and covers the jambs like a door stop, so no Blender run is needed there. Its tests go in `WarehousePrefabTests`.
- **The basement-end door hangs on the tunnel's last shoring frame outside the west wall, not in the mouth** (see Spec Change Log). The 1.1 m leaf covers the frame's props (1.04 m apart) like a door stop and swings toward the house along the straight tunnel; nothing is modelled.
- **The cellar-end door hangs on the east jamb**: the side room's crates stand right in front of the opening, and a west hinge would leave 0.27 m beside them.
- The Dock Guard sleeps nowhere today. The bed is set up for P8 and his routine doesn't change.

## Verification

**Commands:**
- Unity `run_tests` EditMode -- expected: 0 failures

**Manual checks (if no CLI):**
- Play: all 4 doors behave as in the matrix, and the tunnel can be walked end to end with them open.
- Take the tally and a key copy: the facts are set (F-key debug or `quest_report`).

## Suggested Review Order

**Key type and the one parting rule**

- Entry point: keys join quest items in the single "never parted with" rule.
  [`ItemSO.cs:32`](../../Assets/_Game/ScriptableObjects/Items/ItemSO.cs#L32)

- The type itself: no fields, worth forced to 0.
  [`KeyItemSO.cs:10`](../../Assets/_Game/ScriptableObjects/Items/KeyItemSO.cs#L10)

- Category "Key" right after the quest-item arm; Misc tab, kept in sync.
  [`ItemDetailFormatter.cs:68`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L68)
  [`ItemFilter.cs:31`](../../Assets/_Game/Scripts/UI/Inventory/ItemFilter.cs#L31)

- Former direct `isQuestItem` readers now go through the rule.
  [`ActionBarSystem.cs:105`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L105)
  [`CookingRules.cs:28`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L28)
  [`GiveItemCategory.cs:22`](../../Assets/_Game/ScriptableObjects/Items/GiveItemCategory.cs#L22)

**Locks owned by the generated prefabs**

- One helper sets locked + Lockpicking 1 + key, so rebuilds keep the locks.
  [`RouteDoorLocks.cs:31`](../../Assets/_Game/Editor/World/RouteDoorLocks.cs#L31)

- Gang house: hidden door and new tunnel door, each with its own key and save id.
  [`GangHouseBuilder.cs:306`](../../Assets/_Game/Editor/World/GangHouseBuilder.cs#L306)

- Warehouse: cellar door locked, tunnel door built from `cellar.tunnel`.
  [`WarehouseBuilder.cs:693`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L693)

- Cellar-end leaf placement: east hinge, clear of jambs, lintel and the side room's crates.
  [`WarehouseBuilder.cs:139`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L139)

**Basement-end door placement (model script)**

- Picks the shoring frame whose leaf swing fits before the wall; writes `tunnel.door`.
  [`build_gang_house.py:667`](../../Assets/_Game/Art/Environment/Camp/models/gang%20house/Source~/build_gang_house.py#L667)

- East lantern moved one frame back, off the door's frame.
  [`build_gang_house.py:652`](../../Assets/_Game/Art/Environment/Camp/models/gang%20house/Source~/build_gang_house.py#L652)

- Builder refuses a parts file without the door.
  [`GangHouseBuilder.cs:221`](../../Assets/_Game/Editor/World/GangHouseBuilder.cs#L221)

**Tests**

- Route doors: locked, Lockpicking 1, right key, out of bake, fixed save ids.
  [`GangHousePrefabTests.cs:207`](../../Assets/Tests/EditMode/GangHousePrefabTests.cs#L207)
  [`WarehousePrefabTests.cs:502`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L502)

- Leaf placement and swing clear of solids, at both ends.
  [`GangHousePrefabTests.cs:226`](../../Assets/Tests/EditMode/GangHousePrefabTests.cs#L226)
  [`WarehousePrefabTests.cs:520`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L520)

- Every key asset is a worth-0 `KeyItemSO`; UI and action-bar rules.
  [`KeyItemTests.cs:57`](../../Assets/Tests/EditMode/KeyItemTests.cs#L57)
