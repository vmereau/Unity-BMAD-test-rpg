---
title: 'Area 1 warehouse quay: concept pack, then the quay, tally room, cellar side room and dock gate'
type: 'feature'
created: '2026-11-27'
status: 'done'
baseline_commit: 'eef1550ac361b09c19b9fb745e93ddab8ecd2304'
context:
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CELLAR_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/PALISADE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md'
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The river side of the warehouse is unfinished: the shelf in front of the cellar is bare mud falling into the water, the bank between the hall's east gable and `dock-fence-upper` is empty grass, and `DockGate` and its guard lean-to are graybox. The user wants a working quay there, a building on the bank and a real gateway (sketch: `Art/Artworks/Starting area/Camp/Warehouse/refs/Warehouse dock + dock entrance.PNG`).

**Approach:** Two phases. First a concept pack: captures of the site with temporary massing shapes, image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then the build, from existing kit assets and Blender (MCP) models.

## Boundaries & Constraints

**Always:**
- **Quay:** plank deck x 6.5..24.2, z 61..65.5, top y 0.0, on tarred piles (water y −2), a sleeper beam sunk in the shelf (y −0.17) so there is no step, a kerb beam on the river edge, four mooring posts, one ladder. The deck's south 2 m and the shelf strip z 59.8..61 stay a clear cart lane from the river ramp's foot to the gate. Nothing taller than 2 m west of x 9.
- **Quay dressing:** crate stacks, tarred barrels, sacks under a tarp, rope coils, a net rack, two lantern posts; a moored cargo barge of about 7 × 2.2 m, half unloaded, with a gangplank.
- **Tally room:** a lean-to on the hall's east gable, on the dock side of the palisade only (x 21..24.2, z 52.4..56, floor y 4.65). `outer-fence-3/Panel_062` stays as its back wall; the warehouse zone, the Camp Edge clipping and the junction log are unchanged. Mono-pitch shingle roof under the palisade tops, one shuttered hatch to the quay, a 1 × 2.1 m door from the hall in the east gable (about z 53.5..54.5), a desk, a ledger shelf, a strongbox (visual), a stool and a candle. `RA_Warehouse` is extended over it.
- **Retaining wall:** dry-stone rubble under the room's north face (x 20.7..24.2, z about 57.5), from the shelf to the floor, continuing the cellar's front wall. The hoist and `HoistGoods` stay.
- **Cellar side room:** about 3 × 4 m east of the cellar, under the tally room, opening through the cellar's east wall, floor y 0.1; a second terrain hole from the parts file; `RA_WarehouseCellar` enlarged over it. Ray-test its volume against the `SM_Rock*` colliders before fixing its walls.
- **Dock gate:** a palisade gateway in place, 3 m wide (x 24.5, z 57.5..60.5): two heavy flat-topped log posts, a log lintel at 3.2 m clear, pointed logs above it stepping from the bank side down to the river side, two leaves of lashed logs standing open. The guard lean-to east of it is rebuilt in planks with a bench, a spear rack and a brazier.
- **`RA_Docks`** grows about 1.5 m north to cover the whole quay (Company, 21–06).
- The cellar door, the hoist goods, the guard's and the dock guard's waypoints stay reachable on walk surfaces; everything walkable is on the NavMesh. Doorways at least 2.1 × 1 m, steps under 0.3 m.
- Build quality between the warehouse and the huts; `M_Camp_*` materials on kit textures, rules of `Camp/CLAUDE.md`, kit materials never edited, kit props size- and triangle-checked. Night lights use `ClockNightLight`.
- Sites owns `Warehouse` and `DockGate`; run order unchanged; one script is the only source of each model's sizes.
- Two pauses: after the concept pack; after the first build is captured by day and at 23:00 in Play. Rebake with `StartBakingSurfaces` (check for a `... 1.asset`), save the scene.

**Ask First:**
- Any image or Meshy generation (cost and plan first).
- Moving a rock, flag, lane or waypoint.
- Terrain changes beyond the side room's hole.
- More than 40,000 new triangles.
- Anything restricted beyond the three boxes named above.

**Never:** loot, locks, keys; opening the cellar hatch; new NPC routines, dialogue, quests; the placeholder pier (`Docks`); other gates; other areas; `generate_image` / OpenRouter; hand-placed art inside a tool-owned root; editing `hand-edits.json`, a prefab instance or kit materials.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First run | Scripts, prefab builds, place command, Sites | Quay, room, side room, gate and dressing in the scene | N/A |
| Rerun | Same inputs | Same poses, no duplicates, no new assets | N/A |
| Missing input | Model object, material or parts field absent or malformed | Prefab build stops before saving; the gate falls back to graybox | Error naming what is missing, no throw |
| Player in the tally room or the side room | Any hour | Trespass handled by the warehouse guard's rules | N/A |
| Player on the quay | 21–06 / by day | Restricted at night, free by day | N/A |
| Walk | Ramp foot → gate; guard and dock guard patrols | Complete paths, same waypoints reached | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py` -- the warehouse model script (FBX, `Warehouse.json`, `.blend`); library for other camp scripts
- `Assets/_Game/Editor/World/WarehouseBuilder.cs` -- ground capture, prefab build, `Place Warehouse Cellar` (hole, restricted box)
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildWarehouse`, `HoistGoods`, `CellarGoods`, `BuildGate` (shared by every gate)
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- line 518: the `DockGate` root
- `Assets/_Game/Art/Environment/Camp/models/palisade/Source~/build_palisade.py` -- log heads, post and rail sizes for the gateway
- `Assets/Tests/EditMode/WarehousePrefabTests.cs` -- prefab contract tests
- `Assets/_Game/Art/Environment/Camp/WAREHOUSE_CELLAR_CONCEPTS.md` -- the concepts doc to copy the sections from

## Tasks & Acceptance

**Execution (phase 1):**
- [x] `StartingTown` (nothing saved) -- massing shapes for the plan, captures D1–D6 at 1920 × 1080 to `Assets/_Game/Art/Artworks/Starting area/Camp/Quay/refs/` -- inputs for the artworks
- [x] `Assets/_Game/Art/Environment/Camp/QUAY_CONCEPTS.md` -- concepts doc (place, questions, how to generate, template, views, build brief, empty verdict log) -- the pack
- [x] `Assets/_Game/Art/Environment/Camp/CLAUDE.md` -- pointer to the doc
- [x] **Pause:** the user generates and judges the artworks; verdicts and the build pipeline are agreed

**Execution (phase 2):**
- [x] `build_warehouse.py` -- quay, tally room, retaining wall, cellar side room; parts file gains their colliders, walk surfaces, Not Walkable volumes, light points, hole rectangle and restricted boxes
- [x] `Assets/_Game/Art/Environment/Camp/models/dock gate/Source~/build_dock_gate.py` -- gateway, leaves and guard lean-to with a parts file
- [x] `WarehouseBuilder.cs` -- build the new parts, validate before saving, second hole and the enlarged restricted boxes
- [x] `Area1CampInterior.cs`, `Area1SitesBuilder.cs` -- quay goods and barge placement; the dock gate from its prefab with the graybox as fallback
- [x] `Assets/Tests/EditMode/WarehousePrefabTests.cs`, `DockGateTests.cs` -- doorway and gate sizes from the parts files, walk surfaces, clear cart lane (collider reach), restricted boxes inside their walls, lights carry `ClockNightLight`, malformed parts refused
- [x] `StartingTown` -- builds, place commands, `RA_Warehouse` and `RA_Docks`, rebake, captures to `.../Quay/built/` by day and at 23:00, **pause**, play test
- [x] Docs -- *As built* in the concepts doc, `Prefabs/World/Warehouse/CLAUDE.md`, `Editor/World/CLAUDE.md`, `STARTING_TOWN.md`, `deferred-work.md`

**Acceptance Criteria:**
- Given the built scene, when the player walks from the river ramp's foot along the quay and through the gate, then nothing blocks a 2 m wide lane and no step is higher than 0.3 m.
- Given the hall, when the player goes through the east gable door, then they stand in the tally room, and a path exists from the hall's floor to it.
- Given the cellar, when the player walks into the side room, then no terrain or buried rock shows inside it.
- Given a path request from the camp side to the dock that avoids the hall's doors and the gate, then none exists.

## Spec Change Log

- 2026-11-27, phase 1: plan agreed with the user through `/world:redesign-area` (lean-to tally room with `Panel_062` kept as its back wall, enterable from the hall; cellar extended under it; moored barge; guard lean-to rebuilt; `RA_Docks` grown over the quay; the pier is a placeholder and is left alone). Captures D1–D6 taken with massing shapes and `QUAY_CONCEPTS.md` written; the scene was reloaded unsaved and is unchanged. Found: the room needs no east wall (the dock spur's logs close it); the retaining wall's face stands at z 57.2, not 57.5, because of `HoistGoods`, and the bank's toe shows 0.3 m in front of it; D3 (from the water) is the look-setting view, not D1, since only it shows every part; the side room has no capture (it takes the cellar's as-built look). Phase 1 ran before the spec's formal approval, on the agreed plan.
- 2026-11-27, artworks: D3 and D1 generated by the user with their own tool; D2, D4, D5 and D6 through Meshy at the user's request (`nano-banana-pro`, 9 credits each, 36 in all, one pass, balance 678 before). Verdict notes written in the concepts doc.
- 2026-11-27, first pause (user): spec approved; all six views kept; decisions recorded under the verdict log (one full-height retaining wall instead of two steps; no tarp canopy; net rack on the shore; pipeline as proposed).

- 2026-11-27, phase 2 (first build): quay, barge, tally room, retaining wall and cellar side room added to `build_warehouse.py` (model 73,534 triangles, 16,450 of them new; the hall and the cellar are unchanged piece for piece: the new parts draw from their own random stream and what they cut out is drawn into a scratch mesh). New `build_dock_gate.py` (3,520 triangles) and `DockGateBuilder` (`Tools/World/Build Dock Gate Prefab`, `Prefabs/World/DockGate/DockGate.prefab`); Sites places it with the graybox as fallback. `WarehouseBuilder`: tally door, second hole, child boxes of the restricted areas, quay facts; `Area1CampInterior`: `QuayGoods` from the parts file. 58 of 58 tests pass (`WarehousePrefabTests` 49, `DockGateTests` 9). Scene: both prefabs built and placed with the scoped calls, `Place Warehouse Cellar` run (second hole: cells x 533..535, z 566..569), NavMesh rebaked once (no second asset), saved. Captures by day (edit mode, temporary sun) and at 23:00 in Play in `Quay/built/`.
  Checked in the scene: paths ramp foot -> gate, hall -> tally room, shelf -> side room are complete; every guard and dock guard waypoint is on the NavMesh; no NavMesh on the retaining wall's ledge, the roofs or the barge; capsule sweeps find the deck lane clear and the tally room's walls, the dock spur and the gate posts solid; by day the quay is free and the tally room, the side room and the passage are restricted, at 23:00 the quay and the barge are restricted too.
  Deviations from the frozen text and the agreed decisions: (1) side room 1.95 x 3.45 m and 2.85 m high, not about 3 x 4 m: the terrain's hole cells (0.98 m), the dock spur and a rock's underside (-1.5 under the floor) bound it; no rock was moved; (2) the retaining wall is one face at z 57.7 (not 57.5): past the bank's toe, so the hoist goods stay; (3) the tally room has no wall of its own on the east (the dock spur's logs), and its door is at z 54.3..55.44, not 53.5..54.5 (a gable post stands at z 54); (4) the net rack stands on the deck's west end, not on the shore as decided: the dock guard's walk between `WP_0_Shore` and `WP_3_RampFoot` crosses that shore; (5) the guard lean-to is 2.3 m long, not 3.1 m, and the fire basket stands north of the lane: east of x 28.6 the bank rises and rocks stand behind; (6) the restricted areas grow by child boxes (`RA_Warehouse/TallyRoom`, `RA_WarehouseCellar/SideRoom` and `/Passage`, `RA_Docks/Quay`, which also covers the barge), not by enlarging their boxes, which would have reached ground outside the walls; `Place Warehouse Cellar` builds all of them; (7) the instances were placed with the scoped calls, not a full Sites run; no terrain run was made; (8) no light in the tally room (dark at night); (9) two cellar crates that stood in front of the new passage moved into the side room, with two more casks.
  Found on the way: `Capture Warehouse Ground` failed since the cellar's hole was cut (961 points met no ground): it now reads the height map where no ray lands, and its grid reaches the riverbed (z +17.5). The capture differs from the committed one only on the broken stretch's sunk ground and at its old north edge.
  **Ask First reached:** the build adds 19,970 model triangles, under the 40,000 cap, but the 25 kit props on the quay and in the barge add 34,276 more at LOD 0 (cask 1,716, crate 1,336, sack 910 / 496), and the two extra cellar casks 3,432: about 57,700 new triangles in view at close range. Put to the user at the second pause.
  Files not named in a task: `DockGateBuilder.cs`, `Prefabs/World/DockGate/DockGate.prefab`, `models/dock gate/DockGate.fbx` / `.json` / `Source~/DockGate.blend`, `Warehouse.fbx.meta` (remap `Wh_Tarp`), `DockGate.fbx.meta` (the warehouse's remaps, `Wh_Frame` on `M_Camp_PalisadeLog`), `warehouse_ground.json`, `Art/Terrain/StartingTownTerrain.asset` (the hole), `NavMesh-Terrain_StartingArea.asset`, the captures and their `.meta` files.

- 2026-11-27, second pause (user): "I've manually moved some elements to better fit the scene, only thing I ask is maybe expand the Tally a little bit forward so it covers the space left on the stone in front of the window." Found in the saved scene: the quay's eight crates and four sacks moved; their places are now the script's goods table (a rebuild keeps them). Built: `TALLY_Z` to 5.6 (the plank wall stands flush on the retaining wall's face, world z 57.6), roof 15 degrees, a short west wall of vertical boards north of the hall's corner beside the hoist's post, no cap stones and no Not Walkable ledge on the wall; model 74,132 triangles. Prefab rebuilt, `Place Warehouse Cellar` rerun (the `TallyRoom` box follows), NavMesh rebaked (no second asset), scene saved, day captures retaken (D1 to D4, D6, D9). 58 of 58 tests pass. The user did not answer on the triangle count (kit props past the 40,000 cap) or on the tally room's night light: both are listed in `deferred-work.md` and left as built.
- 2026-11-27, review (blind hunter, edge-case hunter, acceptance auditor, run inline, no subagents). Nothing patched. Checked: the hall's and the cellar's own triangle counts differ from the committed model only by the pieces cut out (girt, gable boards, the passage's stones); the three split collider sets (`Wall_East`, the cellar's `Wall_East`, `Skirt_East`) and `UnderFloor_East` close everything but the two openings; a parts file without `sideHole` still reads as a cellar alone; `AreaBox` divides by the parent's scale. Deferred (`deferred-work.md`): eight saved hand edits of the old graybox gate; no full Sites or terrain run; unshadowed lights through walls; the tarp beside the moved sacks; the prop count. Not done: a walk-through with real input and trespass with the guard near (script checks only); night captures were not retaken after the tally room grew.

## Verification

**Commands:**
- EditMode tests `WarehousePrefabTests`, `DockGateTests` -- expected: all pass
- `read_console` after each tool run -- expected: no errors, no kit-light warnings

**Manual checks (if no CLI):**
- Built captures D1–D6 beside the artworks, by day and at 23:00 in Play
- Play test: walk the quay, the gate, the tally room and the side room; push at rails, posts and walls; trespass by day and at night

## Suggested Review Order

**The model: new parts that leave the old ones alone**

- Own random stream and a scratch mesh: the hall and cellar keep every piece.
  [`build_warehouse.py:466`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L466)

- Gable boards at the tally door are burnt into the scratch mesh, then rebuilt round it.
  [`build_warehouse.py:584`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L584)

- Same trick for the cellar's east face where the passage opens.
  [`build_warehouse.py:1172`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1172)

- Quay: deck, piles fitted to the riverbed, goods table (the user's places).
  [`build_warehouse.py:1342`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1342)

- Tally room: palisade logs as two walls, plank wall on the stone.
  [`build_warehouse.py:1564`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1564)

- Retaining wall, also the side room's north wall.
  [`build_warehouse.py:1750`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1750)

- Side room and passage: cores hide the second terrain hole's edge.
  [`build_warehouse.py:1772`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1772)

- Barge and gangplank.
  [`build_warehouse.py:1488`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L1488)

**Builder: parts file to prefab and scene**

- Reads `tally` and `quay`; a malformed field stops the build.
  [`WarehouseBuilder.cs:156`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L156)

- Cuts both holes, rebuilds the restricted boxes of three areas.
  [`WarehouseBuilder.cs:408`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L408)

- Child boxes instead of larger parents: no open ground restricted.
  [`WarehouseBuilder.cs:505`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L505)

- Ground capture reads the height map over terrain holes.
  [`WarehouseBuilder.cs:594`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L594)

- One door builder for the cellar's and the tally room's doors.
  [`WarehouseBuilder.cs:782`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L782)

- Sites maps kit pieces to the script's goods entries.
  [`Area1CampInterior.cs:276`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L276)

**Dock gate**

- Gateway, leaves, lean-to and fire basket on the warehouse's mesh library.
  [`build_dock_gate.py:75`](../../Assets/_Game/Art/Environment/Camp/models/dock%20gate/Source~/build_dock_gate.py#L75)

- Prefab build: validates the parts file before changing anything.
  [`DockGateBuilder.cs:114`](../../Assets/_Game/Editor/World/DockGateBuilder.cs#L114)

- The fire: light and glow on `ClockNightLight`.
  [`DockGateBuilder.cs:231`](../../Assets/_Game/Editor/World/DockGateBuilder.cs#L231)

- Sites places the prefab; the graybox gate is the fallback.
  [`Area1SitesBuilder.cs:521`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L521)

**Tests**

- Cart lane swept on the deck; goods kept out of it.
  [`WarehousePrefabTests.cs:419`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L419)

- Passage and side room: reachable, closed beyond.
  [`WarehousePrefabTests.cs:453`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L453)

- Tally doorway through the gable.
  [`WarehousePrefabTests.cs:365`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L365)

- Gateway clear a cart wide and high along the path.
  [`DockGateTests.cs:51`](../../Assets/Tests/EditMode/DockGateTests.cs#L51)
