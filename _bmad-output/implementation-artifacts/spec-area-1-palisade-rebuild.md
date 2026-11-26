---
title: 'Area 1 palisade: concept pack, Blender log kit and log-built panels from the Camp Edge tool'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: 'bd3053fc'
context:
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp palisade is 165 flat graybox boxes (13 lines, about 420 m, `Area1_Blockout/CampEdge`) beside finished huts and a finished warehouse. No downloaded piece can replace it: the kit walls are 3.2–3.5 m high against 5 m, 50–120k triangles each, fixed in height on ground that needs 6–14 m, and of a different wood.

**Approach:** Two phases in one spec. First a concept pack as for the warehouse: daylight captures of the blockout, image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then a small log kit modelled in Blender from a script, which the Camp Edge tool assembles log by log into every existing panel, each log fitted to the ground. The collapsed stretch and all gates stay as they are.

## Boundaries & Constraints

**Always:**
- Starting look (the pack may refine it): upright round debarked logs with sharpened tops of uneven height, lashed to rails, dark and weathered, as painted in `Warehouse/W4 west end palisade`, `Morning gate` and `Arrival`.
- All 13 lines of `FenceIds` get the new wall. Each `Panel_NNN` keeps its name, parent, position, rotation, scale and `BoxCollider`; only its drawn mesh changes. No panel top ends lower than today's box top.
- Meshes are generated after hand edits are applied, from each panel's final pose; `hand-edits.json` is not edited, and a hand-edit scan after the build lists no new difference.
- Ownership and run order are unchanged: Camp Edge still replaces only `CampEdge`, and a terrain run is still followed by Camp Edge, Sites and a rebake.
- One source for sizes: the Blender script (`Source~`, FBX beside it), with new `M_Camp_*` URP Lit materials on kit textures. Kit materials are never edited.
- The wall butts into the warehouse's round junction logs (local x ±5.86, z 0) and ends on a full log at every opening.
- Two pauses for the user: after the concept pack (artworks and verdict), and after the first line (`outer-fence-3`) is built and captured, before the other twelve.
- Rebake the NavMesh (`StartBakingSurfaces`, check for a `... 1.asset`).

**Ask First:** Any image or Meshy generation. Moving or resizing a panel, a layout point, or a collider. Terrain edits. More than 200,000 triangles for the whole palisade. No kit texture reads as debarked log.

**Never:** `BrokenStretch` (own spec). Rebuilding any gate or the `ArrivalGate` towers (in the captures, not in the build). `ShoreRail`, `RampRail`, the fort's stone walls. `generate_image` / OpenRouter. Hand-placed art inside `CampEdge`. A kit wall piece used as the wall.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal run | Log kit present | Every active panel draws logs; none uses `Blockout_Timber` | N/A |
| Kit missing | No FBX or a foreign material | Graybox boxes built as today | Error logged, run completes |
| Hand-raised panel | Saved pose higher than built | Logs reach the saved top and still enter the ground | N/A |
| Removed panel | `outer-fence/Panel_001`, `_002` | No mesh, no asset left behind | N/A |
| Steep ground | Box 14 m tall | Logs lengthened without stretching the texture | N/A |
| Hand-edit scan | Tool builds into a clone | No mesh asset written, scene assets untouched | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `BuildPalisade` (`:132`), `Box` (`:286`), `Run` (`:85`, edits applied at `:120`), `PANEL_*` constants; the scan calls `Run(root, false, …)`
- `Assets/_Game/Editor/World/Area1TerrainMath.cs:285` -- `CutPanels`, `ClipOutsideRect` (panels stop at the warehouse rectangle)
- `Assets/_Game/Editor/World/Area1HandEdits.cs` -- applies saved poses; 47 `CampEdge` entries (31 panels with position and scale, 2 removed)
- `Assets/_Game/Editor/World/WarehouseBuilder.cs`, `models/warehouse/Source~/build_warehouse.py` -- the pattern: script-built model, importer material remap, a bad model stops the build
- `Assets/_Game/Editor/World/CampClothMesh.cs` -- generated meshes saved under `Camp/Generated/` and reused by name
- `Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md` -- pack format; its template already describes the palisade
- Kit references only: `BanditVillage/SM_Wall_A`, `SM_Wall_B`, `SM_Gate`, `SM_Log_A` (`T_Bark_01`), `Hivemind/Art/Prefabs/SM_Barricade_Var3`
- Scene: `outer-fence-3/Panel_060` ends at x 9.31, `Panel_061` starts at x 20.69; box heights 6.2–14.0 m

## Tasks & Acceptance

**Execution — phase 1, concept pack:**
- [x] `Assets/_Game/Art/Artworks/Starting area/Camp/Palisade/refs/` -- six 1920 × 1080 daylight captures (`P1 north wall from the huts`, `P2 arrival gate from outside`, `P3 inner gate`, `P4 south-west wall from outside`, `P5 hill palisade from below`, `P6 construction detail`), each checked by eye; temporary light only, scene not saved -- input images
- [x] `Assets/_Game/Art/Environment/Camp/PALISADE_CONCEPTS.md` -- chosen direction, how to generate (P1 first), one shared template, six view paragraphs, build brief (lines, lengths, heights, junctions, what stays graybox), verdict log -- the user's generation guide
- [x] **Pause:** the user generates and judges; record the verdicts and any change of look in the document

**Execution — phase 2, build:**
- [x] `Assets/_Game/Art/Environment/Camp/models/palisade/Source~/build_palisade.py` (+ `Palisade.fbx`, `Palisade.json`, `.blend`) -- six log heads (diameter, taper, point), two posts, rail and lashing pieces, UVs that tile along the log, a sizes file for the tool -- the kit
- [x] `Assets/_Game/Art/Environment/Camp/Materials/` -- `M_Camp_PalisadeLog`, `M_Camp_PalisadeFoot` (the existing `M_Camp_Rope` fits the lashings)
- [x] `Assets/_Game/Editor/World/PalisadeMesh.cs` (new) -- pure assembly: a panel's pose, ground heights and a seed in, one mesh out in the panel's local space (logs along the length, tops jittered at or above the box top, shafts lengthened with their UVs, rails on the camp side)
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` (+ new `PalisadeKit.cs`: kit loader and mesh store) -- after edits are applied, give each active panel its mesh and materials; save meshes as sub-assets of `Camp/Generated/Palisade.asset`, drop orphans; graybox fallback; skipped in a scan
- [x] `Assets/Tests/EditMode/PalisadeMeshTests.cs` -- the matrix: top never under the box top, every log below its ground, texture scale constant on a 14 m panel, same seed gives the same mesh
- [x] `StartingTown.unity` -- build `outer-fence-3` first, capture from P1 and P6, **pause**; then all lines, rebake, captures from every P view by day and from P1, P3 and P6 in Play at 08:00 and 23:00, play test
- [x] Docs -- `Camp/CLAUDE.md`, `Editor/World/CLAUDE.md`, `STARTING_TOWN.md`, `Warehouse/CLAUDE.md` (known limit closed), root index; `deferred-work.md` (palisade art pass done; broken stretch and gates open)

**Acceptance Criteria:**
- Given the rebuilt scene, when `CampEdge` is listed, then all 165 panels keep their names, poses and colliders, and `BrokenStretch`, `ArrivalGate`, `ShoreRail` and `RampRail` are unchanged.
- Given a hand-edit scan after the build, then it lists no difference that was not there before.
- Given the player in Play, when they push and jump along both sides of each line, then they cannot cross anywhere they could not before, and nothing shows a gap between logs or under them.
- Given the rebake, when NavMesh paths are asked through the inner, dock, compound, fields and arrival openings, then each exists as before.
- Given the day captures beside the artworks, then log size, tops, lashing and tone read as in the accepted views, and both warehouse gables meet a full log.
- Given a second Camp Edge run, then the scene and the generated assets are unchanged apart from regenerated ids.

- 2026-11-26, implementation (no loopback). Pause 1: the user generated P1-P6 and chose P6 as the reference
  (verdict log in `PALISADE_CONCEPTS.md`). Pause 2: `outer-fence-3` shown from P1, P6 and outside; the user answered
  "roll out to the other twelve lines". Fixed on my own look before pause 2: log feet hung in the air on the outer
  bank (the foot now goes under the lowest ground across the wall, and never above the box's bottom), the foot band
  was near black with a hard edge (now 0.8 and 0.3 m), rails 0.11 -> 0.13 m. The first full build was 204,638
  triangles, over the Ask-First limit: the rope lashing went from 12 to 8 segments (187,454) instead of asking.
  Deviations from the task lines, reworded above: one mesh asset with 165 sub-meshes (about 20 MB of text) instead of
  a folder; no material remap on the FBX (the tool assigns materials; the model import takes none); no leaning logs
  (a lean opens gaps); a second new material for the foot; night captures from three viewpoints, not six. Not in the
  task list: `PalisadeKit.cs`, `Palisade.json`. The terrain asset and `terrain-sculpt.bin` in the working tree are
  the user's own sculpt and capture from before this spec (confirmed at the start): not part of this change. A Camp
  Edge run on that ground moved `inner-fence-east/Panel_141` and `_142` by 1-7 mm. KEEP: the panel box untouched and
  only its mesh swapped; the kit holding only a log's top metre; sizes crossing from Blender to Unity in one file.
- 2026-11-26, review (blind hunter, edge-case hunter, acceptance auditor). No intent gap and no spec loopback: every
  finding was a patch, a deferral or a rejection. Patched: a log's lowest shoulder could dip 5 cm under the box top
  (each log now stands on its own shoulder depth, exported per log, plus 2 cm); the high rail aimed at a taller
  neighbour's top and crossed the middle above the logs (it now hangs under the lower of the two tops at each
  joint); a look test on one line deleted every other line's saved meshes, and a misspelt line emptied the asset (the
  others are kept, an unknown line is refused, a run that dresses nothing leaves the asset alone); neighbour links
  and clip flags were trusted after hand edits (two panels now count as joined only while their ends are within
  5 cm); a sizes file with `head` or `tile` at 0 or a log without a radius could hang the editor (range-checked); the
  holder mesh was found by position (now by name, set as the main object); an exception in the log pass left the
  scene unmarked and leaked meshes; the rail ran 6 cm into the warehouse's junction log at a clipped end; logs
  pressed 7% together could leave slits near the tops (now 10%; 190,550 triangles); a Blender rerun left `Log_1.001`
  mesh data behind; the JSON is now written before the FBX; an unreadable sizes file is reported, not thrown; unnamed
  numbers named. Tests: the gap test could not fail (rewritten on shaft triangles), normals were only checked for
  length (now direction), plus notches, rail height against a taller and a lower neighbour, a clipped end, the camp
  on the left, a deep box bottom: 16 tests. Docs corrected (materials list, counts, save-the-scene rule, as-built
  table). Deferred (`deferred-work.md`): posts doubled where two lines share a point, rails at a line's corners,
  camp side decided per panel, no automated test for the kit loader and the scene pass, the 20 MB asset. Rejected:
  "a scan writes the asset" (it returns before the log pass); the terrain files (the user's own).

## Design Notes

The Blender script models the logs; Unity places them. A whole-ring model fitted to a ground capture (the warehouse's way) would need a capture and a Blender run after every terrain change, and would break the 47 saved panel edits. Keeping each panel's box and swapping only its mesh keeps collisions, hand edits and the run order exactly as they are.

Panels are scaled boxes, so a mesh is written in the panel's local space (world offsets divided by the scale); Unity corrects normals under non-uniform scale. A panel moved by hand later keeps its old mesh until the next Camp Edge run.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after each tool run and in Play -- expected: no errors

**Manual checks:**
- Captures listed above, shown to the user at both pauses.
- Triangle count of the kit and of the whole palisade reported.

## Suggested Review Order

**The tool: boxes stay, logs are drawn on them**

- Entry point: the log pass runs after the hand edits, on the panels as they stand.
  [`Area1BlockoutBuilder.cs:169`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L169)

- One mesh per standing panel; nothing saved if nothing was built or the build threw.
  [`Area1BlockoutBuilder.cs:242`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L242)

- What a panel hands the assembler: span, tops, ground, camp side, posts and clearances.
  [`Area1BlockoutBuilder.cs:318`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L318)

- Two panels are joined only while their ends still meet.
  [`Area1BlockoutBuilder.cs:312`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L312)

- Which face of each line gets the rails.
  [`Area1BlockoutBuilder.cs:38`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L38)

- The ground a log must go under: the lowest across the wall's thickness.
  [`Area1BlockoutBuilder.cs:353`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L353)

**The assembly (pure, tested)**

- Posts, logs, rails: the whole stretch in one read.
  [`PalisadeMesh.cs:128`](../../Assets/_Game/Editor/World/PalisadeMesh.cs#L128)

- Logs picked until they fill the span, each standing on its own shoulder depth.
  [`PalisadeMesh.cs:153`](../../Assets/_Game/Editor/World/PalisadeMesh.cs#L153)

- A head from the kit, then its ring carried down to the foot and into the ground.
  [`PalisadeMesh.cs:181`](../../Assets/_Game/Editor/World/PalisadeMesh.cs#L181)

- The shaft's U moves with the drop, so the grain keeps its scale.
  [`PalisadeMesh.cs:275`](../../Assets/_Game/Editor/World/PalisadeMesh.cs#L275)

- Rails under the lower top at each joint; lashings at the posts.
  [`PalisadeMesh.cs:202`](../../Assets/_Game/Editor/World/PalisadeMesh.cs#L202)

**The kit and the saved meshes**

- Kit loader: model plus sizes file, anything missing or out of range stops it.
  [`PalisadeKit.cs:25`](../../Assets/_Game/Editor/World/PalisadeKit.cs#L25)

- One asset, meshes refilled by name so ids survive; a look test keeps the others.
  [`PalisadeKit.cs:172`](../../Assets/_Game/Editor/World/PalisadeKit.cs#L172)

- A log's top metre: shaft, uneven shoulder, blunt point; the shoulder depth is exported.
  [`build_palisade.py:153`](../../Assets/_Game/Art/Environment/Camp/models/palisade/Source~/build_palisade.py#L153)

- The wrap round a trunk: there and back over two planks, no seam.
  [`build_palisade.py:64`](../../Assets/_Game/Art/Environment/Camp/models/palisade/Source~/build_palisade.py#L64)

- Sizes file first, then the FBX, then the .blend.
  [`build_palisade.py:310`](../../Assets/_Game/Art/Environment/Camp/models/palisade/Source~/build_palisade.py#L310)

**Supporting**

- Tests on a stand-in kit: no daylight through the wall.
  [`PalisadeMeshTests.cs:133`](../../Assets/Tests/EditMode/PalisadeMeshTests.cs#L133)

- Tests: the notches between points clear the panel's top.
  [`PalisadeMeshTests.cs:195`](../../Assets/Tests/EditMode/PalisadeMeshTests.cs#L195)

- The look: captures, prompts, verdicts, and what was built against them.
  [`PALISADE_CONCEPTS.md:1`](../../Assets/_Game/Art/Environment/Camp/PALISADE_CONCEPTS.md#L1)

- How the tool builds the wall, and the save-the-scene rule.
  [`Editor/World/CLAUDE.md`](../../Assets/_Game/Editor/World/CLAUDE.md)
