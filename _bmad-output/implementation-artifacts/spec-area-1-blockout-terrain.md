---
title: 'Area 1 blockout: valley terrain, water, roads and forest'
type: 'feature'
created: '2026-11-04'
status: 'done'
baseline_commit: 'ffdd42e'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `StartingTown` is a 500 m flat field with a crater, so it holds neither the approved valley nor the mountains in the art, and the quarry is only 20 s away at a run.

**Approach:** Grow the terrain to 1000 m without moving the camp, spread the far sites 1.6×, and build the valley with a re-runnable editor tool: mountains, pass, ridges, river, lake, ford, roads and forest, then a NavMesh bake. Palisade, gates, site props and enemy moves are the next spec.

## Boundaries & Constraints

**Always:** Camp objects keep their world position and ground height. The tool reads `layout.json`, uses a fixed seed and gives the same terrain on every run. Artworks are reference only. Commit `.meta` files with their assets.

**Ask First:** Any forest size that drops the camp below 30 fps in the editor. Any change to tents, center stage, inner fence or the Company house. Any site move beyond the table in Design Notes.

**Never:** Palisade, gates, site props, the hollow's rock fold, enemy moves (next spec). Terrain tiling or streaming. Touching the 5 unrelated working-tree changes. `refresh_unity(mode="force")` after raw YAML edits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First run | 513 heightmap, 500 m | 1025 heightmap, 1000 m, old samples copied at +256 | Abort with no changes if the size is neither 500 nor 1000 |
| Re-run | 1025 heightmap | Same heights and paint, no second offset | N/A |
| Broken layout | Missing feature id | Logs which id | Abort before writing |
| Dry hollow | Non-water ground below water level | Raised to water level + 0.3 m | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Terrain/StartingTownTerrain.asset` -- TerrainData, kept at the same path
- `Assets/_Game/Scenes/StartingTown.unity` -- `Terrain_StartingArea` (has `NavMeshSurface`), `Water_Volume`, `Decor`
- `Assets/_Game/Prefabs/World/Water_Volume*` -- swim box and surface; `Prefabs/CLAUDE.md` has the recipe
- `Assets/_Game/Art/Terrain/StartingTownTerrain_Source.asset` -- new; the original 500 m terrain, read for the camp's ground, paint and holes
- `Assets/_Game/Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- rebaked
- `_bmad-output/implementation-artifacts/deferred-work.md` -- next spec and side findings
- `docs/World/Areas/area-001/` -- `layout.json`, `layout.seed.json`, `planner.html` (`LIMIT`), `terrain-map.png`

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json`, `planner.html` -- apply the Design Notes table, set terrain origin −500 and size 1000, `LIMIT` 500 -- layout stays the source of truth
- [x] Forest prefabs -- render candidates in the URP camera, pick 3 with no magenta, record the cause in `deferred-work.md` -- Valentin reimported the packs as URP on 2026-11-04. Beech `L_02`, `L_03`, `XL_01` are used: they have LODs and match the art. `SM_MountainTree_*` render, but are orange, have no usable LODs and fail as terrain trees.
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- pure polyline distance, falloff and grade helpers
- [x] `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- menu `Tools/World/Build Area 1 Terrain`: resize, height offset, sculpt, paint, trees
- [x] `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- cover distance, falloff and grade edge cases
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run the tool; replace `Water_Volume` with the valley water and the waterfall strip; add the pass blocker; snap outside objects to the ground; rebake the NavMesh
- [x] `docs/World/Areas/area-001/terrain-map.png` -- recapture from the new terrain, then republish the planner artifact
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Prefabs/CLAUDE.md`, `area-001-company-encampment.md` -- update the size, terrain offset, water and lake facts

**Acceptance Criteria:**
- Given the scene before and after, when every `Camp` child, NPC and `PlayerSpawnPoint` is sampled, then ground height differs by under 0.05 m, except `CivilizationGate`.
- Given the player in the valley, when they run at the mountain wall, then a band steeper than 50° stops them, and the only low route out, the pass, ends at the blocker.
- Given the river, when the player enters it, then they swim; at the ford they wade across without swimming, and NavMesh agents can cross only there.
- Given NavMesh paths from the camp centre, then the entrances of the quarry, wood camp and hunting grounds (their road ends) each take 25–40 s at a run, and the pass mouth and ruins ridge are reachable.
- Given sight rays against the terrain, then the south lip of the ruins ridge is visible from the tents and the hunter's fire point from the docks and tents, and no tree stands within 4 m of either fire ray.
- Given eye-level screenshots from the tents, docks and quarry road, then they read like Master layout 2 for landform, with no magenta trees.

## Design Notes

**Site moves** (everything else stays). The old grid maps exactly onto the new one, since 500/512 = 1000/1024.

| Feature | New position | Result |
|---------|--------------|--------|
| Mountain wall | Centreline ±425, width 150, pass gap z 55..160 | Valley ±350 |
| Quarry | (226, −96), road end (193, −66) | Road 194 m |
| Ruins 2 | (272, −147) | Beside the quarry |
| Wood camp | (−224, −8), road end (−182, 3) | Road 165 m |
| Hunting grounds | (−20, 250), trail `[44,160]`, `[31.7,208]` | Trail 159 m |
| River | West end (−500, 135), east end (385, 75) | Runs through the pass to a waterfall |
| Arrival road | Starts at (−500, 92) | About 360 m from the pass mouth |

**Heights:** terrain y = −30, heights +30, so existing ground stays at the same world height and can be dug. Water level −2, river bed −6, ford bed −2.5 (swim starts at 1.3 m). Banks are 14 m wide, centred on the layout width, so the river keeps about its approved width and open banks stay under the 45° slope limit; the ford ramps up to the rim at about 30°, or agents route around it. The camp footprint is copied unchanged. The crater rim outside it is reshaped: notched along the sight lines, and cut with ramps under 15° where roads cross. Ruins ridge top about 35 m, quarry pad about 25 m, mountains 180–260 m.

**Water:** one box covers the valley with its top at −2, because `PlayerSwimming` takes the highest `bounds.max.y` and the river has no gradient. The surface quad sits under the terrain wherever ground is higher. The Not Walkable volume is split into two boxes, west and east of the ford strip x 31..39, because Not Walkable always wins over a walkable volume. A separate quad fakes the waterfall, and the river runs into the cliff at x 385 so nothing can walk around its end.

**Forest:** about 4,500 instances (capped at 8,000), kept 4 m off roads, clear of the pads, camp and water, with sight corridors left open. `treeDistance` drops from 5000 to about 800.

## Verification

**Commands:**
- `run_tests` with EditMode filter `Area1TerrainMathTests` -- expected: all pass

**Manual checks (if no CLI):**
- Run the tool twice and compare a height hash -- expected: identical.
- Read the console after play mode -- expected: no "Tree prefab missing" errors.

## Spec Change Log

## Suggested Review Order

**Build flow**

- Entry point: guards first, then sculpt, paint, trees, and writes only at the end.
  [`Area1TerrainBuilder.cs:130`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L130)

- Layout loading: the feature list that makes the tool data-driven.
  [`Area1TerrainBuilder.cs:600`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L600)

- Camp ground is copied from the source asset, everything else is computed.
  [`Area1TerrainBuilder.cs:251`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L251)

**Camp protection**

- The footprint that keeps camp objects on their original ground.
  [`Area1TerrainBuilder.cs:364`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L364)

- Holes copied at their old positions; the door by the outcrop depends on them.
  [`Area1TerrainBuilder.cs:224`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L224)

**Landform and water**

- Hills, ridges and site pads, before water.
  [`Area1TerrainBuilder.cs:284`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L284)

- Mountain ring with a cliff band above the slope limit, and the pass.
  [`Area1TerrainBuilder.cs:308`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L308)

- River, lake and ford; the ford ramp keeps NavMesh crossing there.
  [`Area1TerrainBuilder.cs:328`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L328)

**Routes and sightlines**

- Roads graded from fixed camp ground out to the sites.
  [`Area1TerrainBuilder.cs:382`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L382)

- Notches for the fire and ruins sightlines, one cut per cell, never below dry ground.
  [`Area1TerrainBuilder.cs:439`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L439)

- Forest scatter and its exclusions.
  [`Area1TerrainBuilder.cs:524`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L524)

**Peripherals**

- Grade limiter behind the roads, and its test.
  [`Area1TerrainMath.cs:61`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L61)
  [`Area1TerrainMathTests.cs:73`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L73)

- Terrain, tool and water facts for future sessions.
  [`CLAUDE.md:52`](../../Assets/_Game/Scenes/CLAUDE.md#L52)

- The grown layout and the planner bounds.
  [`layout.json:6`](../../docs/World/Areas/area-001/layout.json#L6)
  [`planner.html:131`](../../docs/World/Areas/area-001/planner.html#L131)
