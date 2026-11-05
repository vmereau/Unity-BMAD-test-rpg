---
title: 'Area 1 blockout: camp interior'
type: 'feature'
created: '2026-11-05'
status: 'done'
baseline_commit: '1ecf3fc'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json', '{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp uses about half its ring, the inner fence can be walked around, and the Company has no presence at the docks. Several places the design needs don't exist yet.

**Approach:** Replace the inner fence with a tall palisade and one guarded gate, fill the empty ring with graybox work areas, and add a dock warehouse built into the north palisade plus a restricted gate on the bridge path. Room stays free for later NPCs and areas.

## Boundaries & Constraints

**Always:** A tool re-run replaces only the roots it owns. Positions come from `layout.json`. Sites are simple placeholder shapes. `BrokenStretch` stays as it is, so the hunter's fire stays in view from the tents. The camp palisade's four openings stay open. The bank route from the wilderness opening to the docks stays walkable: the dock gate stands open in the blockout, and locking it is later gameplay. Runtime `NavMeshObstacle`s stay disabled in the scene. Sightlines are confirmed by a screenshot with a marker. Commit `.meta` files with assets. `ProjectSettings.asset` ends unchanged.

**Ask First:** Any terrain sculpt or paint, including muddy path paint. Moving tents, center stage, buildings, NPCs or their routines. A gate through the warehouse or a barred gate in the cut. Sealing the docks off.

**Never:** Gate-locking or door gameplay, new NPCs or dialogue. Moving the Company house's gameplay objects. Touching `Core.unity`, the TMP font, Shimmer2 meta, `.lighting` files or the meshy test folder. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Re-run | Either tool run twice | Same result; hand-built objects untouched | N/A |
| Broken layout | Missing or malformed new feature | Logs the id, changes nothing | Abort before writing |
| Fence through warehouse | Panel inside the warehouse footprint | Panel skipped, so the building reads as part of the wall | N/A |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json` -- new fence lines, areas and planned paths
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `FenceIds`, `BuildPalisade`, `TryLoadFeatures`; owns `CampEdge`
- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- `CutPanels`; home for a footprint test
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs`, `Area1RemoteSites.cs` -- site roots and helpers
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- new: the interior sites' shapes
- `Assets/_Game/Prefabs/World/Tent_Graybox.prefab` -- the tent swap point
- `Assets/_Game/Scenes/StartingTown.unity` -- `Camp/InnerFence`: `SM_Fence_*` go, the two `SM_Gate_Side_C` stay
- `docs/World/Areas/area-001/planner.html`, `terrain-map.png`, `layout.seed.json` -- refreshed at the end

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- add `inner-fence-west`, `inner-fence-east`, `dock-fence-upper`, `dock-fence-lower`, `warehouse`, `dock-gate`, `inner-gate`, `work-gang-tents`, `latrine`, `timber-yard`, `workshop-yard`, and the five planned paths -- layout stays the source of truth
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs`, `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- footprint test for skipped panels, with tests -- keeps the rule pure
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- build the new fence lines; skip panels inside `warehouse`; validate first
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs`, `Area1SitesBuilder.cs` -- new shapes file; roots `InnerGate`, `Warehouse`, `DockGate`, `WorkGangTents`, `Latrine`, `TimberYard`, `WorkshopYard`, `Walkways`; validate first
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- remove `SM_Fence_*`, run both tools, bake, save, then run tests
- [x] `docs/World/Areas/area-001/layout.seed.json`, `terrain-map.png`, planner artifact and its `layouts/current` document -- refresh and republish
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md` -- record roots and the dock design; log path paint; remove the duplicate `PlayerAnimationDriver` entry
- [x] `Assets/_Game/Art/Artworks/Starting area/` -- commit the five new artworks and their `.meta` files

**Acceptance Criteria:**
- Given the inner palisade, when the player runs and jumps along it, then the gate at (0, 9) is the only way through between the camp's two halves, apart from going outside the ring by the openings.
- Given the warehouse, when viewed from the camp and the river, then the palisade runs into its walls, and the player and agents can enter from the camp and leave by the river side down to the deck.
- Given the bridge path, when walking to the docks, then the route passes through the dock gate, and the spur leaves no dry way round it.
- Given a marker at night, when viewed from (11, 35), the docks and the bridge, then a screenshot shows the fire's glow.
- Given NavMesh paths and the player capsule, then all four openings, the fort gate, both wade lanes, the deck and every new area are reachable, and every NPC reaches its routine spots.
- Given the walk-time table in `spec-area-1-blockout-remote-sites`, then the table is re-measured from the same points, and any change over 1 s is explained.
- Given eye-level screenshots against the five new artworks, then the massing reads like them and nothing is magenta.

## Spec Change Log

- Review pass 1: three independent reviewers. Patches only; no code was reverted. Fixed: panels clipped at the warehouse footprint (a midpoint test left a stub in the room), with a skirt under the floor; floor from the whole footprint; door post and landing supports; crates moved out from under the landing; `path-night` moved north of the ramp; dock gate posts share one top; guard post turned along the slope; workshop roof; warehouse and walkway sizes validated. `layout.seed.json` had been ticked but not refreshed, and now is, with the five artworks. Rebaked and retested on this geometry. KEEP: `BrokenStretch` unchanged; the spur at x 24.5, clear of the barricades.

## Design Notes

**Inner palisade:** same panels as the ring, along z 9 from the ring near (−51.3, 8.6) to (−4.2, 9), and from (4.2, 9) to the ring near (54.3, 9). The two `SM_Gate_Side_C` frame the gate, with the guard hut east of it near (9.7, 6), as in `Morning gate`. Today's NavMesh threads the sparse fence near x 28, so routes to the quarry and wood roads get longer through the gate. If a panel would cut a building, tell Valentin before bending the line.

**Warehouse:** about 12 × 8 m at (15, 52), straddling the palisade on the flat rim, with its floor at the highest ground in the footprint. It is hollow, with a door facing the camp. The river side stands on posts, with a hoist and crates below, as in `Dock checkpoint`, and a ramp runs down to the bank, because the ground drops at 18–38°. It takes over the doc's Storage Hut role.

**Dock gate:** a spur at x 24.5, clear of the hand-placed barricades, runs from the north palisade into the water near z 66, with a 3 m gate at about z 57.5–60.5 on the bank path, and a lean-to guard post on the bridge side. The fire is clear from the bridge and bank.

**Areas:** five `Tent_Graybox` at about (−36..−24, 14..23), south of the arrival road, giving 15 tents. Latrine trench at about (−10, 43), on the flat. Timber yard near (−35, −5): log stacks, saw trestle, tool lean-to, handcarts. Workshop and stone stacks near (35, −5). Keep the road ends clear, and leave about half of each empty block free for later. `Decor/SM_cartB_a1` and `SM_woodLogs_a1` moved clear of the inner palisade.

**Walkways:** flat boards along the main tent paths, as in `Tent rows dusk`. No muddy paths in this spec, because that needs terrain paint.

## Walk Times

Same points and method as `spec-area-1-blockout-remote-sites`. From camp (−0.45, 0.50, 16.06):

| To | Before | After |
|----|--------|-------|
| Quarry road end | 36.6 | 37.6 |
| Quarry pad centre | 44.0 | 44.9 |
| Wood road end | 30.5 | 31.7 |
| Wood camp | 37.6 | 38.8 |
| Trail end | 35.3 | 35.3 |
| Hunting camp | 37.7 | 37.7 |
| Ruins lip | 33.3 | 33.3 |
| Cave | 18.4 | 19.3 |
| Ruins 2 | 57.0 | 57.9 |
| Bandit site | 19.6 | 19.6 |
| Deck head | 15.1 | 12.3 |

Changes over 1 s between sites all come from the inner palisade: the old NavMesh threaded the sparse fence near x 28, and paths now use the gate. The largest are south-side sites to north-side sites by the wilderness opening: cave to trail end 45.4 → 53.5, cave to ruins lip 43.4 → 51.5, quarry road end to trail end 63.2 → 71.6, and quarry road end to ruins lip 61.4 → 69.8. Deck head gets closer because the warehouse is a shorter way down. Other paths from the camp: tents to deck root 42.7 m (66 m before), bridge head to deck root 31.2 m.

## Verification

**Commands:**
- `run_tests` EditMode, `Area1TerrainMathTests` and `ClockNightLightTests`, after the bake is saved -- expected: all pass

**Manual checks (if no CLI):**
- Play mode: set `Application.runInBackground = true` in one call, test in the next, and confirm `Time.frameCount` advances.
- Bake with `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`, so the asset is written; leave `Ground` alone.
- `git status` -- expected: no change to `ProjectSettings/ProjectSettings.asset` or the unrelated paths beyond their starting state.

## Suggested Review Order

**Fence and warehouse**

- Panels stop at the footprint, so the walls are the fence.
  [`Area1BlockoutBuilder.cs:114`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L114)

- Pure clipping rule, tested below.
  [`Area1TerrainMath.cs:27`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L27)

- Floor sits on the highest ground in the footprint.
  [`Area1CampInterior.cs:59`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L59)

- Skirt closes the old fence line under the floor.
  [`Area1CampInterior.cs:93`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L93)

- The building is a real route to the deck.
  [`Area1CampInterior.cs:105`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L105)

**Gates**

- Open leaves between the existing posts, with the guard hut beside them.
  [`Area1CampInterior.cs:31`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L31)

- Open for now: restricting it is later gameplay.
  [`Area1CampInterior.cs:154`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L154)

- Spur clears the barricades and runs into the river.
  [`layout.json:754`](../../docs/World/Areas/area-001/layout.json#L754)

**Tool safety**

- Sizes and features are checked before any root goes.
  [`Area1SitesBuilder.cs:208`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L208)

- Camp Edge refuses a layout with no warehouse.
  [`Area1BlockoutBuilder.cs:291`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L291)

**Areas**

- Tents stay south of the arrival road.
  [`Area1CampInterior.cs:170`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L170)

- Yards keep clear of the roads.
  [`Area1CampInterior.cs:206`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L206)

**Peripherals**

- Clipping tests.
  [`Area1TerrainMathTests.cs:130`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L130)

- Planned paths, not painted yet.
  [`layout.json:933`](../../docs/World/Areas/area-001/layout.json#L933)

- Notes for future sessions.
  [`CLAUDE.md:61`](../../Assets/_Game/Scenes/CLAUDE.md#L61)
