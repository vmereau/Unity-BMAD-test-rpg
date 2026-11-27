---
title: 'Area 1 warehouse cellar: concept pack, then a rock-walled basement under the landing and the hall''s north half'
type: 'feature'
created: '2026-11-27'
status: 'done'
context:
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md'
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The warehouse has no basement: the ground under its floor is solid (y 4.4–4.6 under a floor at y 4.65), and the bank under the river landing is bare. The user wants a rock-walled cellar there, entered by a door from the dock shelf and, later, by a hatch and ladder from the hall (sketch: `Art/Artworks/Starting area/Camp/Warehouse/refs/Warehouse basement entrance.PNG`).

**Approach:** Two phases. First a concept pack: captures of the site with temporary massing shapes, image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then the build, from existing kit assets and Blender (MCP) models. The room is made by a terrain hole (heights unchanged) filled by a cellar modelled into the generated warehouse prefab.

## Boundaries & Constraints

**Always:**
- Plan (world coordinates): interior x 13.8..19.9, z 52.6..59.0, floor y 0.1, clear up to the hall's joists (about 4 m). Back wall is the cut rock; sides and front are dry-stone rubble (`M_Camp_Rubble`) with a timber lintel; never dressed masonry. Walls at least 1.2 m thick wherever they hide the hole's edge. The front face stands behind the landing's front posts (z 59.6).
- Dock door: a working, unlocked door of the existing door prefab family, opening 1.3 × 2.1 m at x 17.8..19.1, one step of 0.2 m up from the shelf (y −0.2).
- Hatch: 1 × 1 m in the hall floor at x 14.2..15.2, z 54.3..55.3, a shut solid trapdoor with a ladder modelled below it.
- The landing's middle posts and a new post row under the hall's north wall stand inside as pillars; the `Skirt_River` collider opens only over the cellar's width; the palisade-line `Skirt` on z 52 stays closed.
- The hall floor, both doorways, both ramps, the landing, the `Ramp_Camp` contract and the guard's five waypoints stay as they are and walkable.
- The cellar floor is on the NavMesh and linked to the shelf through the doorway. Doorways at least 2.1 × 1 m, steps under 0.3 m.
- `RestrictedAreas/RA_WarehouseCellar`: owner `NPC_WarehouseGuard`, all hours, inside the cellar's walls.
- Terrain: a hole over the cellar's plan, cut by a one-shot `Tools/World` command and re-cut by the terrain tool on every run, its rectangle read from the warehouse parts file.
- `build_warehouse.py` is the only source of sizes; Sites owns the `Warehouse` root; run order unchanged. `M_Camp_*` materials on kit textures, rules of `Camp/CLAUDE.md`, kit materials never edited, kit props size- and triangle-checked. Night lights use `ClockNightLight`.
- Before any modelling, prove the hole in memory: NavMesh bake, light leaks at its edge, collision at its edge, the third-person camera in the room.
- Two pauses: after the concept pack; after the first build is captured by day and at 23:00 in Play. Rebake with `StartBakingSurfaces` (check for a `... 1.asset`), save the scene.

**Ask First:**
- Any image or Meshy generation (cost and plan first).
- An inside light that is always on instead of clock-driven.
- Running the terrain tool with the new hole pass; any terrain change beyond the hole (the strip of ground left inside the front wall at x 14..15.5 may need a dip).
- Moving a neighbour, flag, lane or waypoint.
- A warehouse model above 55,000 triangles.
- Anything restricted beyond `RA_WarehouseCellar`.

**Never:** loot, locks, keys; opening the hatch or climbing the ladder; new NPC routines, dialogue, quests; other areas; `generate_image` / OpenRouter; hand-placed art inside a tool-owned root; editing `hand-edits.json` or the prefab instance.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First run | Script, prefab build, cut command, Sites | Cellar in the prefab, hole in the terrain, door, hatch lid, restricted area | N/A |
| Rerun | Same inputs | Same poses, no duplicates, no new assets, hole unchanged | N/A |
| Missing input | Model object, material or parts field absent or malformed | Prefab build stops before saving; cut command changes nothing | Error naming what is missing, no throw |
| Terrain run after the cut | `Build Area 1 Terrain` | The hole is still there | Parts file unreadable: error, no hole, run continues |
| Player enters the cellar | Through the dock door, any hour | Trespass handled by the guard's area rules | N/A |
| Guard patrol | Day and night | Same five waypoints reached | N/A |
| Door | Interact | Opens and closes, never locked | N/A |
| Hatch | Walk over it in the hall | Solid, nothing to interact with | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py` -- the model script; writes `Warehouse.fbx`, `Warehouse.json`, `Warehouse.blend`
- `Assets/_Game/Editor/World/WarehouseBuilder.cs` -- ground capture and prefab build from the parts file
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildWarehouse`: places the prefab and `HoistGoods`
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `CopyHoles` (line 330): holes come only from the source asset today
- `Assets/_Game/Prefabs/World/Doors/` -- `Door Base`, `Simple door`, `Poor small door`
- `Assets/Tests/EditMode/WarehousePrefabTests.cs` -- prefab contract tests
- `Assets/_Game/Art/Environment/Camp/CANTEEN_CONCEPTS.md` -- the concepts doc to copy the sections from

## Tasks & Acceptance

**Execution (phase 1):**
- [x] `StartingTown` (nothing saved) -- cut the hole in memory, add a box room and doorway, test bake, edge collision, light and camera; restore -- proves the terrain method before modelling
- [x] `Assets/_Game/Art/Artworks/Starting area/Camp/Warehouse/Cellar/refs/` -- massing captures B1–B6 at 1920 × 1080 -- inputs for the artworks
- [x] `Assets/_Game/Art/Environment/Camp/WAREHOUSE_CELLAR_CONCEPTS.md` -- concepts doc (place, questions, how to generate, template, views, build brief, empty verdict log) -- the pack
- [x] `Assets/_Game/Art/Environment/Camp/CLAUDE.md` -- pointer to the doc
- [x] **Pause:** the user generates and judges the artworks

**Execution (phase 2):**
- [x] `build_warehouse.py` -- cellar walls, floor, pillars, lintel, door frame, hatch lid and ladder; parts file gains cellar colliders, walk surface, hole rectangle, door and light anchors, restricted box
- [x] `WarehouseBuilder.cs` -- build the new parts, place the door instance and lights, validate before saving; `Tools/World/Cut Warehouse Cellar Hole`
- [x] `Area1TerrainBuilder.cs` -- re-cut the hole from the parts file after `CopyHoles`
- [x] `Area1CampInterior.cs` -- light crates instead of the film-size ones
- [x] `WarehousePrefabTests.cs` -- doorway size, cellar walk surface, pillars clear of the door lane, restricted box inside the walls, hole rectangle inside the walls' outer faces, malformed parts refused
- [x] `StartingTown` -- prefab build, cut, Sites, `RA_WarehouseCellar`, rebake, captures to `.../Cellar/built/`, **pause**, play test
- [x] Docs -- *As built* in the concepts doc, `Prefabs/World/Warehouse/CLAUDE.md`, `Editor/World/CLAUDE.md`, `STARTING_TOWN.md`, `deferred-work.md`

**Acceptance Criteria:**
- Given the built scene, when the player walks from the dock shelf through the door, then they reach every part of the cellar floor without falling through or seeing terrain inside the room.
- Given the cellar, when an agent is asked for a path from the shelf to the cellar's back wall, then the path is complete and passes through the doorway.
- Given the hall above, when the player or the guard crosses the floor over the cellar, then nothing differs from before the build.
- Given a path request from the camp side under the hall to the dock, then none exists (the fence line is closed).

- 2026-11-27, phase 1: plan agreed with the user through `/world:redesign-area` (extent: landing + north half; terrain hole; working unlocked door; `RA_WarehouseCellar` for the guard, always). The hole was cut in memory and restored: collision and a local NavMesh bake pass, two thin sun stripes come through the timber above, the camera is left for the first build. Captures B1–B6 and `WAREHOUSE_CELLAR_CONCEPTS.md` written. Found: the room is 6.1 m wide, not 6.6 m (east wall under the landing's edge); the hoist goods need not move; a strip of ground rises 0.2–0.5 m above the floor inside the front wall at x 14..15.5. The scene and the terrain are unchanged. Phase 1 ran before the spec's formal approval, on the agreed plan.
- 2026-11-27, artworks: generated by the user with their own tool (no Meshy credits). Verdicts in the concepts doc. User decisions: spec approved; front wall as B1 (rubble round the door, cut rock in the west bay), which replaces the frozen text's all-rubble front; inside lantern always on, dim; cellar closed overhead everywhere; B6 is no night reference.
- 2026-11-27, phase 2 (first build): cellar added to `build_warehouse.py` (model 51,110 triangles, cap 55,000), new materials `M_Camp_Earth` and `M_Camp_Rock`, door / lanterns / validation in `WarehouseBuilder`, `Tools/World/Place Warehouse Cellar` (hole + `RA_WarehouseCellar`), terrain-tool re-cut, cellar goods and light hoist crates in `Area1CampInterior`, 16 new tests (25 of 25 pass). Scene: prefab rebuilt, instance replaced at the pose the saved hand edit pins, hole cut (49 cells), NavMesh rebaked twice (no second asset), saved. Captures by day (noon) and at 23:00 in Play in `Cellar/built/`.
  Deviations: (1) the instance was placed with the scoped calls, not a full Sites run (a full run replaces every tool root); (2) the task named the command `Cut Warehouse Cellar Hole`: it is `Place Warehouse Cellar`, since it also builds the restricted area; (3) pillars under the hall's wall are the hall's two door posts, not three new ones; (4) door opening 1.14 m, the kit door's width, not 1.3 m; (5) east wall under the hall 0.7 m thick, not 1.2 m; (6) `WarehouseBuilder` and four of its members are now public so the tests reach them, as `PalisadeMesh` and `BrokenStretchLayout` are; (7) the terrain tool's re-cut is written but no terrain run was made (Ask First).
  Found by the tests during the build: the west walls stood up through the river ramp's deck (fixed: they stop under it). Found in the scene: the root's height is a saved hand edit 7 cm above what Sites computes.
  Files not named in a task: `Materials/M_Camp_Earth.mat`, `M_Camp_Rock.mat` (+ `.meta`), `Warehouse.fbx.meta` (two material remaps), `Art/Terrain/StartingTownTerrain.asset` (the hole), `NavMesh-Terrain_StartingArea.asset`, `Scenes/CLAUDE.md`, root `CLAUDE.md`, `Camp/CLAUDE.md`, the captures and their `.meta` files.

- 2026-11-27, second pause (user): "I like the rubble, can you make it so all the walls are rubble ? Lets remove the cut rock. Also, i've changed my mind, can you increase the size of the basement ?" Asked which way; answer: south, the whole hall depth. Built: every wall rubble (`sheet()`, `Wh_Rock` and `M_Camp_Rock` removed), back wall at local z -1.9 (world 50.1) instead of the hall's south wall, because four hand-placed rocks are buried there and reach z 49.8; a corner pier hides one that reaches in. Beam on two posts, lantern `CellarLantern_Back`, palisade-line collider split (`Skirt`, `Skirt_Line_East`), hole 63 cells, more stores. Deviations from the frozen text: extent (6.1 x 8.9 m, past the palisade line), two always-on lights inside, model 57,086 triangles (cap 55,000: Ask First, put to the user with this build). 25 of 25 tests pass; NavMesh rebaked, no second asset; captures retaken in Play (B6 shows the player: the user was playing during the capture).

- 2026-11-27, user: the higher triangle count (57,086) is accepted. Review (blind hunter, edge-case hunter, acceptance auditor, run inline, no subagents). Patched: the two always-on lanterns had no shadows and lit the dock ground through the front wall (seen in the B1 and B2 captures); they are now soft-shadowed, with a test. Checked: no NavMesh under the hall's floor after the palisade-line collider was split. Deferred (`deferred-work.md`): `Place Warehouse Cellar` only opens cells, so a smaller or moved hole leaves the old cells open; no terrain run has exercised the re-cut. Not done: the captures were not retaken after the shadow fix; a full Sites run; the guard hearing a trespasser through the floor (the player was aggroed after running out of the cellar in the user's own session, not tested on purpose).

## Design Notes

A terrain is a height field, so a room under it needs either a hole or a pit. A pit would lower samples in the footprint Sites reads for the floor height, and its sides slope over one sample (about 1 m) into the room. A hole leaves heights alone; its edge is jagged on a 0.977 m grid, which thick walls hide. Holes are rewritten from the source asset on every terrain run, hence the re-cut pass.

## Verification

**Commands:**
- EditMode tests `WarehousePrefabTests` -- expected: all pass
- `read_console` after each tool run -- expected: no errors, no kit-light warnings

**Manual checks (if no CLI):**
- Built captures B1–B6 beside the artworks, by day and at 23:00 in Play
- Play test: walk the door, the room, the hall above; push at walls and pillars; trespass with the guard overhead
