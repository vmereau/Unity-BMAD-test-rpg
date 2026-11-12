---
title: 'Area 1 mafia quarters rebuilt as a plot-filling barracks/warehouse'
type: 'feature'
created: '2026-11-12'
status: 'done'
baseline_commit: '223b0db'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `MafiaQuarters` is a 4.1 × 3 m hut that Valentin moved to (24.5, 13.7). It leaves open ground on every side of its plot and reads as one more worker hut, not as the gang's own building.

**Approach:** Replace its blockout with one larger timber barracks/warehouse that fills the whole plot between the neighbouring huts and the inner palisade, closed on every side except a single entrance on the side leading to the mafia fire.

## Boundaries & Constraints

**Always:** Fill the open plot bounded by `worker-hut-11` (west), `worker-hut-15` / `worker-hut-27` (north), `worker-hut-25` (east) and `inner-fence-east` (south), leaving only a hairline clearance to each. One entrance only, on the east wall in the lane between hut 25 and hut 27, facing the mafia fire; at least 2.1 m headroom, steps under 0.3 m. Rough but sturdy timber, clearly bigger and taller than the worker huts and below the boss house in quality. Enterable, with a collidable floor and an interior that reads as barracks plus warehouse through a few blockout props that keep the door and a walking lane clear. Keep the root name `MafiaQuarters` under `TentCamp/MafiaZone`, hand-owned ordinary scene geometry. Preserve every other object, including the unsaved hand edits in the open scene. Roof must not give a route over the inner palisade.

**Ask First:** Moving, resizing or deleting any neighbouring hut, tarp, fence panel or fire; changing terrain; a footprint that blocks a neighbour's door.

**Never:** Add a second door, window openings a player fits through, NPCs, interactables, quest content or final art. Touch `BossHouse`, the arena platforms, the lakeside `GangHouse` or tool-owned roots. Rerun `Author Mafia Buildings And Overlooks` or any Sites / Camp Edge / Terrain builder.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scenes/StartingTown.unity` -- live `Area1_Blockout/TentCamp/MafiaZone/MafiaQuarters` (pos 24.54, 0.66, 13.70, yaw 90, 34 children); scene currently has unsaved hand edits.
- `Assets/_Game/Editor/World/Area1MafiaBlockout.cs` -- one-time author of the old quarters; holds reusable `House`/`DoorWall`/`Stairs`/`Root` helpers and the `QUARTERS_*` constants.
- `Assets/_Game/Editor/World/Area1WorkerHut.cs` -- `PlankWall` rough boarded wall helper.
- `Assets/_Game/Editor/World/Area1RemoteSites.cs` -- `Local` box helper (mesh + collider + material).
- `Assets/_Game/Art/Materials/Blockout_Timber.mat`, `Blockout_Canvas.mat` -- blockout palette.
- `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md` -- record the quarters' position, size and purpose.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- re-measure neighbour bounds and ground heights in the live scene, and record the pre-change object inventory of everything outside `MafiaQuarters` -- measured plot today: x 21.7..28.1, z 9.3..17.1, ground 0.49..0.53.
- [x] `Assets/_Game/Editor/World/Area1MafiaBlockout.cs` -- add a second menu command `Tools/World/Rebuild Mafia Quarters` that replaces only the children of the existing `MafiaQuarters` root with the new building, with grouped Undo and the same ownership-flag guards; leave the original one-time command and its refusal untouched -- the rebuild stays reproducible and reviewable without a throwaway script.
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run the command: footprint about x 21.9..27.95, z 9.5..16.95 (roughly 6 × 7.5 m), floor just above the highest ground in the footprint, eaves about 3.4 m, gabled boarded roof, closed gables and foundation skirt, east door about 2 m wide by 2.4 m high centred near z 15.4 with entry steps into the lane; inside, bunk frames along one wall and crate/barrel stacks at the south end -- the single goal of this spec.
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- rebake navigation with `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`, then save -- the building now closes three gaps agents could path through.
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md` -- update the `MafiaQuarters` entry (new position, footprint, single east door, barracks/warehouse role, rebuild command) -- the current text describes the old hut at (31.5, 16.5).

**Acceptance Criteria:**
- Given an overhead view, when comparing the plot's edges with the new building, then no walkable gap remains between it and hut 11, huts 15/27, hut 25 or the inner palisade.
- Given the mafia fire, when walking the baked path to the quarters' interior, then the route enters through the east door and the player capsule (1.7 m high, 0.3 m radius, 0.3 m step) reaches the interior.
- Given the Player's Zone west of the building, when requesting a path to the mafia fire, then it no longer passes through the plot.
- Given hut 11, 15, 25 and 27, when approaching their doors, then each stays reachable.
- Given a ground-level view from the fire, when comparing the quarters with the huts and the boss house, then it reads as the largest communal timber building, rougher than the boss house.
- Given the pre-change inventory, when comparing everything outside `MafiaQuarters`, then no other object's existence or transform changed.
- Given a running jump from any nearby walkable surface, when aiming at the roof, then the eave stays out of reach.

## Design Notes

Hut 11's door faces north and hut 15's faces west, so neither opens onto the plot; hut 25's door faces north into the same lane the new entrance uses, so the steps must leave that door's approach clear. The building stands 0.2 m from the inner palisade (top y ≈ 5.8): the eave at about 3.9 m world height is far beyond the 1.27 m jump, and interior props stay under 1.2 m near the south wall as a second guard. Keep the root's world position inside the plot so the existing CLAUDE.md "coordinates are world (x, z)" convention still holds.

## Verification

**Manual checks (if no CLI):**
- Unity console is free of compile errors after the editor script change.
- Overhead and from-the-fire screenshots show the plot filled and one doorway.
- `NavMesh.CalculatePath` fire → interior completes through the door; Player's Zone → fire no longer crosses the plot; the four neighbouring hut doors still resolve complete paths.
- Play-mode capsule walk (`CharacterController.Move` along baked corners) from the fire into the building.
- Pre/post inventory of non-quarters objects matches; `NavMesh-Terrain_StartingArea.asset` stays the single assigned bake asset.

### Recorded evidence (2026-11-12)

- Built by `Tools/World/Rebuild Mafia Quarters`: root at world (24.925, 0.657, 13.225), yaw 90, 47 children; wall footprint x 21.90..27.95, z 9.50..16.95; ridge y 6.33; door world z 14.4..16.4 on the east wall; doorstep top y 0.64 (0.11–0.15 m rise).
- Preservation: a hash of name, local transform, active flag and component count over the 9,946 scene transforms outside `MafiaQuarters` was identical before and after every run of the command. The scene file also carries Valentin's own uncommitted hand edits, which this work saved along with the hall.
- Clearances, by raycast from the hall's walls to neighbouring colliders: inner palisade 0.10 m; huts 15 / 27 0.30–0.49 m; hut 25 0.35–0.39 m; hut 11 0.48–1.02 m (it stands askew). Neighbour roof edges come within a few centimetres, which is what fixed the footprint. All but the hut 11 wedge are narrower than the 0.6 m capsule; `GapBoards` closes that wedge at hut 11's north corner (`Physics.CheckCapsule` blocked across the mouth), touching hut 11's corner post and door-side panel end.
- Navigation rebaked with `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`; `NavMesh-Terrain_StartingArea.asset` stays the assigned asset and no `... 1.asset` appeared. `NavMesh.CalculatePath`: fire → interior complete through the east door (10.3 m); Player's Zone (20, 16) → fire complete, now 38.5 m round the north side instead of through the plot; hut 11, 15, 25 and 27 doors complete; no NavMesh within 0.15 m in any of the four side gaps.
- Play mode, real player `CharacterController` (1.7 m, radius 0.3, step 0.3) moved along baked corners with downward motion: fire → bunk end, fire → stores end, interior → fire and fire → hut 25 interior all arrived within 0.14 m. This tests capsule traversal, not keyboard/camera feel. It ran before the review patches (crates moved 0.15 m off the wall, gap boards lowered, gable and roof trim); the door, floor and lane were not changed by them.
- Overhead, from-the-fire and interior captures inspected. The interior has no light and is black in captures; it was inspected with a temporary unsaved point light.
- Unity compiled the script; the only console errors are the known Burst-server pipe errors.

### Review outcome

Three independent reviewers (blind, edge cases, acceptance) found no high-severity issue. Patched: rollback now removes a half-built hall before Undo restores the old children; the plot guard ignores triggers; the doorstep refuses an over-tall rise and skips a non-positive one; `FoundationSkirt` samples in the root's frame (it assumed an unrotated root); gable tiers and roof ends trimmed so steps stay under the roof; crates moved clear of the wall posts; plank relief turned outwards; gap boards run into the ground; docs corrected to the measured clearances. Rejected: ancestor-scale assumptions and check-order drift in `Build()`.

Known limit: "hairline clearance to each" holds against neighbour roof edges, not walls — the huts' roofs overhang, and hut 11 stands askew, so wall-to-wall gaps are 0.3–1.0 m on three sides. Closing them further means the hall's walls cutting into neighbour roofs, or moving the huts (Ask First).

## Suggested Review Order

**The rebuild command**

- Entry point: replaces only the quarters' children, guarded and undoable.
  [`Area1MafiaBlockout.cs:118`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L118)

- Refuses to build into a neighbour that was moved onto the plot.
  [`Area1MafiaBlockout.cs:129`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L129)

- Shared validation, extracted from the original one-time command.
  [`Area1MafiaBlockout.cs:167`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L167)

**The building**

- Shell: plot-filling walls, gabled roof, closed gables, foundations.
  [`Area1MafiaBlockout.cs:200`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L200)

- The single off-centre door facing the fire lane.
  [`Area1MafiaBlockout.cs:209`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L209)

- Boards closing the one wedge a player could enter beside hut 11.
  [`Area1MafiaBlockout.cs:255`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L255)

- Barracks and warehouse props, kept out of the door lane.
  [`Area1MafiaBlockout.cs:260`](../../Assets/_Game/Editor/World/Area1MafiaBlockout.cs#L260)

**Docs**

- Scene notes: position, clearances, door, rebuild command caveats.
  [`CLAUDE.md:107`](../../Assets/_Game/Scenes/CLAUDE.md#L107)

- Area description updated from "use open" to barracks/warehouse.
  [`area-001-company-encampment.md:262`](../../docs/World/Areas/area-001-company-encampment.md#L262)
