---
title: 'Area 1 blockout: Company compound and road'
type: 'feature'
created: '2026-11-05'
status: 'done'
baseline_commit: '1b37bb4'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json', '{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Goods and guards have no route of their own, the docks can be reached past the warehouse along the north bank, and nothing shows the gap between the Company and the workers.

**Approach:** Make the north bank a Company compound, closed on the west, with a staging yard and a delivery road, and run a clean, maintained Company Road from the warehouse through the tents and the inner gate to the fort. The tool paints the roads and muddy paths.

## Boundaries & Constraints

**Always:** A tool re-run replaces only the roots it owns. Positions come from `layout.json`. Sites are simple placeholder shapes. Ground heights stay as they are: paint only, no road carving. Capture the sculpt before the terrain tool runs, and after it rerun Camp Edge and Sites, check the waterfall in-game, and rebake. Nothing tall stands in the tents-to-fire strip (about x 3 at z 52 to x −8 at z 75). `BrokenStretch` stays. Runtime `NavMeshObstacle`s stay disabled in the scene. Sightlines are confirmed by a screenshot with a marker. `ProjectSettings.asset` ends unchanged.

**Ask First:** Any ground height change. Moving more than one tent, or any building or NPC. Removing trees outside the compound.

**Never:** Guards, gate locking or other gameplay; arrival opening guards come later. New NPCs. Touching `Core.unity`, the TMP font, Shimmer2 meta, `.lighting` files or the meshy test folder. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Re-run | Any tool run twice | Same result; hand-built objects untouched | N/A |
| Uncaptured sculpt | Terrain has hand edits not captured | Terrain tool refuses | Existing check |
| Broken layout | Missing or malformed new feature | Logs the id, changes nothing | Abort before writing |
| Terrain re-run | Heights before and after | Unchanged within 1 cm | Stop and tell Valentin |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `Paint`, `Layer`, `TreeAllowed`, `TryLoadLayout`; roads are read by fixed id, and `CarveRoads` must not see the new ones
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `FenceIds`, panels, validation
- `Assets/_Game/Editor/World/Area1CampInterior.cs`, `Area1SitesBuilder.cs` -- `BuildWarehouse`, `BuildDockGate`, walkways, site roots
- `docs/World/Areas/area-001/layout.json` -- new lines and zones; `walkway-tents`, `path-company` go
- `Assets/_Game/Scenes/StartingTown.unity`, `Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset`, `Art/Terrain/` -- rebuilt and rebaked

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- add `compound`, `compound-fence-upper`, `compound-fence-lower`, `compound-gate`, `staging-yard`, `company-road`, `delivery-road`; remove `walkway-tents` and `path-company`; re-route `path-commute`
- [x] `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- paint-only pass: `company-road` in Cobble, `delivery-road` in Road, `path-*` in Mud; no trees inside `compound`
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs`, `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- paint blend along a line, with tests
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- add the compound fence lines
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs`, `Area1SitesBuilder.cs` -- wider, longer warehouse ramps; `StagingYard` and `CompoundGate` roots; gate width as a parameter; drop `walkway-tents`
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- move the props on the road; capture the sculpt; run Terrain, Camp Edge and Sites; bake; save; then run tests
- [x] `docs/World/Areas/area-001/layout.seed.json`, `terrain-map.png`, planner artifact and `layouts/current` -- refresh and republish
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md` -- record the compound and road; close the west bank and path paint items

**Acceptance Criteria:**
- Given the Company Road, when walked from the warehouse to the fort gate, then a 5 m wide clean road runs clear of tents, props and NPC spots to the foot of the rock ramp at (17.3, −21), continues up the ramp at its existing width, and reads against the mud at eye level.
- Given the arrival road, when a player leaves it for the docks, then the only ways in are the compound gate, the warehouse, the dock gate or swimming; the fence's river end stands deeper than `swimEnterDepth` (1.3 m), so nobody wades round it.
- Given carts, then both warehouse ramps are about 3.5 m wide and no steeper than 18°.
- Given a marker at night, when viewed from (11, 35), the docks and the bridge, then a screenshot shows the fire's glow.
- Given NavMesh paths and the player capsule, then the compound, staging yard, deck, four openings, fort gate, wade lanes and all areas are reachable, and all NPC routines complete.
- Given the terrain rebuild, then the waterfall works in-game, and the ruins still show from the tents with a marker.
- Given the walk-time tables, then they are re-measured from the same points, and any change over 1 s is explained.

## Spec Change Log

## Design Notes

**Road line:** warehouse ramp foot (17, 34.8), (10, 37), (8, 33), (6.5, 27), (4, 18), (3, 13), (0, 9.5), (1.5, 5.5), (3.5, −1), (4, −7), (6, −13), (12, −17), (17.3, −21), then the ramp's NavMesh line (16.5, −23.6), (13, −26.3), (12.6, −27.3), (13.4, −28.3), (20, −33). The yard stretch bends west of the straight NavMesh line, which runs through `SmithingStation_Anvil` and within 1.6 m of `Spot_Blacksmith_Night`; the bend clears both by over 3 m. The ramp is 2–4 m wide between hand-placed rocks, so it stays as built and the paint simply runs under them. The gap between the tents at z 33–35 is 5.1 m, so a tent may not need to move; moving one up to 1 m is approved. Props to move off it, and off the painted paths: `SM_Crate_01`, `SM_Crate_02`, `SM_crateLogs`, `SM_firewoodSetA_a1`, the `Decor` bench near (2, 6), `SM_tableA_a1` at (0.2, 4.6), and the small stones at (16.8, −21). The road replaces the `walkway-tents` boards.

**Warehouse ramps:** the floor is at y 4.65. The camp ramp drops 4.2 m to y 0.45, so 18° needs 13.2 m and its foot lands at z 34.8, not 36. The river ramp drops 2.8 m and runs 10 m to x 4. Both are 3.5 m wide, so the landing grows to 3.8 m deep; a narrower landing would push the ramp into the river wall and its posts.

**West closure:** from `Tower_R` at (−35.5, 39) to (−41, 50) and (−41, 54.5), then (−41, 59.5) to (−43, 84), into the water. At z 81 the river is only 1.2 m deep, which is a wade; at z 84 it is 3 m. A 5 m `compound-gate` at (−41, 57).

**Delivery road:** branches off the arrival road near (−60, 50), then (−50, 51.5) and (−44, 56.5) to pass south of the trees at (−48.3, 56.1) and (−53.3, 55.2), through the gate, along z 57–58 to the ramp foot near (4, 57.9).

**Staging yard:** within x −36..−12, z 56–70, west of the fire strip: cart shed, stacks of crates and sacks, a low watch post. `compound` is the rectangle x −43.5..24.5, z 39..89. Four trees stand in it and it clears them all on the terrain run; the river's existing margin already keeps trees off its north-bank corner. The tree at (−44.6, 68.8) is outside the fence and stays. Details return later.

**Paint:** the tool rewrites paint on each run, so the roads live there, not as hand paint. Cobble for the Company Road only, so it is the one clean surface in the camp. Paint order is mud, the carved roads, the delivery road, then Cobble, so every road stays clean where a path crosses it. That includes the inner gate: the road takes its middle 5 m and mud shows only at the sides. `path-commute` moves off the stage to (14, 27), (8.5, 21), (6.5, 14), (2, 10), (−1.5, 7), (−8, 4.5), (−42, 5). New features are checked for points and width before anything is written.

**Terrain run (Valentin accepted, 2026-11-05):** a rebuild moved heights by one heightmap step (1.83 cm) on 191 samples, over the 1 cm limit, because the sculpt rounds to whole centimetres. It also removed 31 trees and added 19 outside the compound: 24 removed and 17 added on the sculpted ground (x −70..254, z −175..41), because the committed forest was scattered before that sculpt, and 7 removed and 2 added around the hunting camp, because the forest predates its move to (32, 222). Any terrain run gives the same forest, so these aren't caused by this spec.

## Walk Times

Same points and method as `spec-area-1-blockout-remote-sites`. From camp, every time is within 0.2 s of before. Between sites, three pairs change by over 1 s: wood road to ruins lip 64.5 → 61.1, bandit site to ruins lip 49.5 → 47.5, and deck head to wood camp 42.8 → 41.5. All three now use the compound gate and the bank, and the four cleared trees straighten that line. Arrival road to deck is 73.0 m by the delivery road.

## Verification

**Commands:**
- `run_tests` EditMode, `Area1TerrainMathTests` and `ClockNightLightTests`, after the bake is saved -- expected: all pass

**Manual checks (if no CLI):**
- Compare terrain heights before and after the terrain run: expected within 1 cm everywhere.
- Play mode: set `Application.runInBackground = true` in one call, test in the next, and confirm `Time.frameCount` advances.
- Bake with `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`; leave `Ground` alone.

## Suggested Review Order

**Paint**

- Entry point: mud goes under the carved roads, then the delivery road and cobble.
  [`Area1TerrainBuilder.cs:623`](../../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L623)

- Reads the lines and validates them before anything is written.
  [`Area1TerrainBuilder.cs:829`](../../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L829)

- A fade never thins ground that already has the layer.
  [`Area1TerrainMath.cs:86`](../../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L86)

- The road itself, with the bend clear of the smith.
  [`layout.json:1087`](../../../docs/World/Areas/area-001/layout.json#L1087)

**Compound**

- The west closure; new lines go last to keep the seeded panels.
  [`Area1BlockoutBuilder.cs:29`](../../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L29)

- The fence lines, ending past wading depth.
  [`layout.json:1026`](../../../docs/World/Areas/area-001/layout.json#L1026)

- Clears the compound's trees and nothing else.
  [`Area1TerrainBuilder.cs:694`](../../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L694)

- One gate builder, now with a width.
  [`Area1CampInterior.cs:158`](../../../Assets/_Game/Editor/World/Area1CampInterior.cs#L158)

- Placeholder yard; posts follow the falling bank.
  [`Area1CampInterior.cs:233`](../../../Assets/_Game/Editor/World/Area1CampInterior.cs#L233)

**Ramps**

- Cart width and the lengths for 18° or less.
  [`Area1CampInterior.cs:19`](../../../Assets/_Game/Editor/World/Area1CampInterior.cs#L19)

**Tests**

- Blend math, including the fade that must not thin a layer.
  [`Area1TerrainMathTests.cs:113`](../../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L113)
