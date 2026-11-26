---
title: 'Area 1 broken stretch: concept pack, then the collapsed palisade under repair, climbable, with no invisible wall'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: '8afbf3b6'
context:
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/PALISADE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `CampEdge/BrokenStretch` (8 m of the north palisade behind the player's hut, x −1..7, z 52.4) is a graybox rail on two posts with an invisible 6 m wall. It should tell its story: the ground moved (river, storm, wind), the stone footing gave way, the wall fell, and the Company is repairing it. At night the hunter's fire across the river shows through it.

**Approach:** Two phases, as for the palisade. First a concept pack: captures of the site, image-to-image prompts and a build brief, then a pause while the user generates and judges. Then the build: reshaped ground, fallen rocks, snapped and leaning logs, repair works, all with real colliders, and the invisible wall removed so the player can scramble over.

## Boundaries & Constraints

**Always:**
- Content (the pack may refine it): slip scar and washout in the ground; footing stones spilled down the river side; snapped stumps on the old line; leaning end logs with a hanging rail; fallen logs toward the water; pole scaffolding with ladder, raking props and a shear-legs hoist at the ends; pale new logs stacked and one half-pointed on trestles; quarry stone and a sledge; tools, rope, mud boards; a rope line with rags or low stakes as the temporary closure; a stool and an unlit brazier, with room left for a night guard.
- No invisible collider stops the player. Everything that blocks is visible and has its own collider. The player can cross in both directions by walking and jumping.
- The hunter's fire shows through the gap from the rock bank at its foot (x 1..8, z 47–51), over a window at least 3 m wide on the wall line. The yard keeps the sliver it has; the tarp bay is not moved. (Renegotiated by the user on 2026-11-26: the three yard points first written here, (3, 44), (10, 40) and (−2, 49), never saw the fire.)
- NPCs do not path through: a Not Walkable volume closes the gap. Props stand beside the walks workers use, never on them. (Second sentence added, and "the whole dressed footprint" narrowed to the gap, from the user's notes of 2026-11-26: the walk from the warehouse ramp to the dock crosses the work yard.)
- Old wood is the palisade's wood and log sizes (`Palisade.json`); new wood is clearly paler. Custom pieces come from one Blender script (`Source~`, FBX beside it) on `M_Camp_*` materials; kit materials are never edited.
- Camp Edge still owns and rebuilds `CampEdge/BrokenStretch`, fitted to the ground on every run; the two neighbouring panels keep their poses and colliders. Ground changes survive a terrain run (captured sculpt; paint through the tool).
- Two pauses: after the concept pack, and after the first build is captured by day and at 23:00.
- Rebake the NavMesh (`StartBakingSurfaces`, check for a `... 1.asset`); save the scene after the Camp Edge run.

**Ask First:** Any image or Meshy generation. Moving or removing a hand-placed object (the `SM_Rock_*` on the line, home yard dressing, the player's hut). Ground changes further than 12 m from the break point or under the warehouse. More than 40,000 triangles for the stretch. A lit light at the breach.

**Never:** NPCs, guard routines, quests or restricted-area changes (the far side is `RA_Docks`, 21–06, as today). Other palisade lines, gates, towers, rails. `generate_image` / OpenRouter. Hand-placed art inside `CampEdge`. Editing `hand-edits.json`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal run | Models and materials present | Dressed stretch, no `Blocker` collider, Not Walkable volume present | N/A |
| Model missing | No FBX or a missing material | Today's rail, posts and blocker built | Error logged, run completes |
| Rerun, same ground | Second Camp Edge run | Same poses, no new assets | N/A |
| Hand-edit scan | Tool builds into a clone | No asset written, no terrain change | N/A |
| Break point moved | `fence-break` shifted along the line | The stretch follows; the ground scar does not | Warning names the stale scar |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs:434` -- `BuildBrokenStretch` (rail, two posts, `Blocker` on Ignore Raycast, Not Walkable volume); constants at `:90-98`; called at `:159`
- `Assets/_Game/Editor/World/PalisadeKit.cs`, `PalisadeMesh.cs` -- kit loader and log assembly (closing posts at the gap's ends)
- `Assets/_Game/Editor/World/Area1FortRamp.cs` -- pattern for a tool that reshapes live terrain, then capture
- `Assets/_Game/Editor/World/HomeYardDresser.cs` -- `Prop` (kit piece, kit light removed), `ClockNightLight` values
- `docs/World/Areas/area-001/layout.json:304` -- `fence-break` (3, 52.4)
- Kit: `SM_Rock_01..09`, `SM_Debris_A..C`, `SM_WoodLadder_01a/b`, `SM_WoodTrestles_01a/02a`, `SM_Sledge_Hammer`, `SM_EuropeanBeech_Stump_01`
- Site: camp ground y 0.5; the wall stands on a bank at y 1.7–1.8 (z 52–57.5), which drops to the shore (y −0.2 at z 60–65) and the water (z 68+); dock deck at (1, 69). Hand-placed `SM_Rock_04_1 (3)`, `(4)`, `SM_Rock_01_1`, `SM_Rock_09 (5)` sit on the line. Player's hut at (−0.3, 47.6), tarp bay at (4.3, 47)

## Tasks & Acceptance

**Execution — phase 1, concept pack:**
- [x] `Assets/_Game/Art/Artworks/Starting area/Camp/Broken stretch/refs/` -- six 1920 × 1080 captures (`B1 from the hut yard`, `B2 night fire across the river`, `B3 from the shore`, `B4 along the wall`, `B5 at the breach`, `B6 from above`), each checked by eye; temporary light and temporary massing shapes, scene not saved -- input images
- [x] `Assets/_Game/Art/Environment/Camp/BROKEN_STRETCH_CONCEPTS.md` -- the story, how to generate (B5 first), one shared template, six view paragraphs, build brief (sizes, sight line, crossing, what stays), verdict log -- the user's generation guide
- [x] **Pause:** the user generates and judges; record verdicts and decisions in the document

**Execution — phase 2, build:**
- [x] `Assets/_Game/Art/Environment/Camp/models/broken stretch/Source~/build_broken_stretch.py` (+ FBX, sizes file, `.blend`) -- stumps, fallen and leaning logs, scaffold, props, hoist, new-log stack, stakes and rope line
- [x] `Assets/_Game/Art/Environment/Camp/Materials/` -- `M_Camp_NewLog` and any other new `M_Camp_*`
- [x] `Assets/_Game/Editor/World/BrokenStretchBuilder.cs` (new), `Area1BlockoutBuilder.cs` -- dress the stretch from the model and kit pieces, colliders per piece, Not Walkable volume, no blocker; graybox fallback; pure placement maths separated for tests
- [x] Terrain -- slip scar and washout near the break point, captured (`Capture Area 1 Terrain Sculpt`), mud paint through the terrain tool
- [x] `Assets/Tests/EditMode/BrokenStretchTests.cs` -- the matrix rows that are pure (placement repeatable, sight window clear, footprint covered by the volume)
- [x] `StartingTown.unity` -- Terrain, Camp Edge, Sites, rebake; captures from every B view by day and from B1, B2 at 23:00 in Play; **pause**; play test
- [x] Docs -- `Editor/World/CLAUDE.md`, `STARTING_TOWN.md`, `Camp/CLAUDE.md`, root index, `deferred-work.md` (stretch done; night guard open)

**Acceptance Criteria:**
- Given the built scene, when colliders under `BrokenStretch` are listed, then none is on Ignore Raycast and none lacks a visible mesh.
- Given the player in Play, when they walk and jump at the breach from the camp and from the shore, then they cross both ways; pushed at the neighbouring panels, they cross nowhere new.
- Given Play at 23:00, when the fire is looked for from the rock bank at the gap's foot, then it shows in a capture, and a render test from the bank finds clear rays over at least 3 m of the wall line. (Reworded on 2026-11-26 after the user's sight decision: physics rays pass through cloth and trees here.)
- Given the rebake, when a path is asked from the hut yard to the shore, then it does not pass through the breach, and the paths through the existing openings exist as before.
- Given a hand-edit scan after the build, then it lists no new difference.
- Given the day captures beside the accepted artworks, then the collapse, the repair works and the old-against-new wood read as in them.

- 2026-11-26, phase 1. Captures taken with temporary massing shapes (hidden objects, nothing saved; the scene stayed
  clean). Reworded above: `B5` is the close view at the breach instead of a repair detail, `B2` is taken from the rock
  bank and not from the yard, and B5 is the look-setting view. **Open for the user (frozen block):** the three sight
  points of the *Always* rule, (3, 44), (10, 40) and (−2, 49), do not see the fire today. A render test shows it fully
  only from the rock bank (x 1..8, z 47–51) and as a sliver from one lane ((6, 38), (5–6, 40)); the tarp bay's canvas,
  hut `worker-hut`, `SM_Rock_01_1` and near-bank birches hide it elsewhere. Map and causes:
  `BROKEN_STRETCH_CONCEPTS.md` → *Where the fire shows from*.

- 2026-11-26, phase 2 (no loopback). Pause 1: the user generated B5 and B1, asked for B2, B3, B4 and B6 through Meshy
  (45 credits, B6 twice), accepted all six and chose the rock-bank sight rule. Built: 38 meshes and 61 parts from
  `build_broken_stretch.py`, placed by the new `BrokenStretchLayout` (pure) and `BrokenStretchBuilder`; 3,924
  triangles, 57 colliders, none on Ignore Raycast. Deviations from the task lines: the layout lives in the script's
  parts file, not in C#; no kit prefabs are used (ladder, trestles and stones are modelled); the new-log stack is
  on the shore, because the ramp's foot lands on the shelf; the ground work is a one-time menu command
  (`Sink Area 1 Broken Stretch Ground`) plus a paint hook in `Area1TerrainBuilder`, with no terrain run and no Sites
  run (nothing they build moved); the "sight window" test became walk-clearance tests, since sight needs a render.
  Not in the task list: `BrokenStretchLayout.cs`, `BrokenStretch.json`, `M_Camp_Rag`, a line in
  `.claude/rules/meshy-mcp.md`. Checked: 2,103 EditMode tests green; rebake wrote no second asset; agents path from
  the yard to the shelf through the warehouse (53 m), not the gap; the dock walk's path is unchanged; a hand-edit
  scan lists nothing under `BrokenStretch`; in Play the player crosses both ways walking, with or without jumps, and
  is stopped at both neighbouring panels from both sides; the fire shows from the rock bank at 23:00.

- 2026-11-26, pause 2. The user moved the shore group (bearers, new logs, trestles, chips) by hand to the west shore,
  because it stood in the way of the docks' other entrance, sculpted the terrain, and accepted the rest. The group's
  place is now in the script (`SHORE_AT`, `SHORE_TURN`): after a Camp Edge run each part stands within 1 mm of where
  the user put it on plan, refitted to the ground in height. The user's sculpt was captured before that run.

- 2026-11-26, review (blind hunter, edge-case hunter, acceptance auditor). No intent gap and no spec loopback: every
  finding was a patch, a deferral or a rejection. Patched in code: a parts file of the wrong shape threw after
  `CampEdge` was destroyed (now refused, graybox built); a fence line listed the other way round mirrored the layout
  (the frame is now always a rotation); the "already sunk" check guessed from two heights (now a marker file,
  `broken-stretch-ground.json`); the command could dig in another scene's terrain; undo skipped the paint; a strut's
  capsule overshot its ends; a grouped rock stopped counting as ground; unknown `on`, collider shapes and non-list
  `col` were silently defaulted; a fractional `version` passed; model and file are now tied by triangle counts; the
  scar's paint followed a moved break point while the hollow did not (both now stay at `SunkAt`); scans logged the
  error and warning again; one `TAG`, the mud layer index taken from the terrain tool. Patched in the layout: the
  walk through the gap is a true 2.5 m (stumps and hoist moved, the hoist now beside it), `Spilled_11`, the sledge,
  the stool and the fire basket moved off it, trestles and rope stakes got colliders, the hanging rail rests on the
  rocks. Tests rewritten: collider reach instead of pivots, a third rectangle for the docks' other entrance, the
  strip's depth, a reversed line, thirteen refused files, and the can't-fail "same twice" test replaced: 11 tests.
  Docs corrected (the superseded Not Walkable and fan statements, lane width, counts, two stale lines in the
  palisade and warehouse packs, dated history removed). Measured: the fire's window from the rock bank is 6.3 m.
  Rechecked after the last layout change: 2,103 EditMode tests green, no second NavMesh asset, paths as before, a
  hand-edit scan with nothing under `BrokenStretch`, the crossing in Play. Deferred (`deferred-work.md`): no terrain
  or Sites run, no automated sight check, listed content not built, the write order of file and FBX. Rejected: the
  submesh order (rags and rope draw their own materials in the captures), captures kept in the repository (as for
  the palisade, they are not), `.meta` files without a `MonoImporter` block (Unity wrote them).
- Files of this change not named in a task line: `Area1TerrainBuilder.cs` (paint hook), `BrokenStretchLayout.cs`,
  `BrokenStretch.json`, `M_Camp_Rag.mat`, `docs/World/Areas/area-001/broken-stretch-ground.json`, the six artworks
  and six refs, `PALISADE_CONCEPTS.md` and `WAREHOUSE_CONCEPTS.md` (one line each), `.claude/rules/meshy-mcp.md`,
  every new `.meta`, and the regenerated `StartingTownTerrain.asset`, `terrain-sculpt.bin`,
  `NavMesh-Terrain_StartingArea.asset` and `Generated/Palisade.asset` (two `dock-fence-lower` panels, from the
  user's sculpt). Not part of it: `Assets/mixamo packs/`, the TextMesh Pro fallback font asset.

## Design Notes

A sight ray from the yard passes only 0.3–0.5 m above the bank at the wall line, so today's bank already limits the view. The slip scar lowers the bank inside the gap: that is what makes room for stumps and rubble under the rays, and for a scramble instead of a 1.3 m step.

The tool builds the stretch, not a hand-placed root: a Camp Edge run replaces `CampEdge` whole, and the pieces must follow the ground.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after each tool run and in Play -- expected: no errors

**Manual checks:**
- Captures shown to the user at both pauses; triangle count reported.
- `CharacterController` crossing run in Play, both directions, and a push along both neighbouring panels.

## Suggested Review Order

**The tool: Camp Edge dresses the stretch, or falls back**

- Entry point: load the model and parts, dress, or build the old graybox.
  [`Area1BlockoutBuilder.cs:435`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L435)

- Every part placed, given its mesh, materials and colliders; no asset written.
  [`BrokenStretchBuilder.cs:260`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L260)

- The strip that keeps agents out of the gap while the yard stays walkable.
  [`BrokenStretchBuilder.cs:285`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L285)

- Colliders of each part's own shape; a strut's capsule keeps its true ends.
  [`BrokenStretchBuilder.cs:292`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L292)

- Only hand-placed camp rocks count as ground, so a scan matches a run.
  [`BrokenStretchBuilder.cs:328`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L328)

**Reading the parts file safely**

- Nothing in the file can throw past this: a bad file means graybox.
  [`BrokenStretchBuilder.cs:159`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L159)

- Triangle counts tie the model to the file: a version apart is refused.
  [`BrokenStretchBuilder.cs:224`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L224)

- Part by part: fit, points, colliders; unknown values refused, not defaulted.
  [`BrokenStretchLayout.cs:79`](../../Assets/_Game/Editor/World/BrokenStretchLayout.cs#L79)

**Placement (pure, tested)**

- The stretch's frame: always a rotation, whichever way the line is listed.
  [`BrokenStretchLayout.cs:54`](../../Assets/_Game/Editor/World/BrokenStretchLayout.cs#L54)

- Four ways a part meets the ground: height, rocks, resting log, stretched pole.
  [`BrokenStretchLayout.cs:187`](../../Assets/_Game/Editor/World/BrokenStretchLayout.cs#L187)

**The layout, owned by the Blender script**

- Every part and where it goes; the walks' rectangles are named here.
  [`build_broken_stretch.py:561`](../../Assets/_Game/Art/Environment/Camp/models/broken%20stretch/Source~/build_broken_stretch.py#L561)

- The work yard, kept beside the walk to the ramp and south of the dock walk.
  [`build_broken_stretch.py:625`](../../Assets/_Game/Art/Environment/Camp/models/broken%20stretch/Source~/build_broken_stretch.py#L625)

- The shore group, turned and moved as one block to where the user put it.
  [`build_broken_stretch.py:669`](../../Assets/_Game/Art/Environment/Camp/models/broken%20stretch/Source~/build_broken_stretch.py#L669)

- A wall log with butt, axe-cut or snapped ends: old and new share it.
  [`build_broken_stretch.py:213`](../../Assets/_Game/Art/Environment/Camp/models/broken%20stretch/Source~/build_broken_stretch.py#L213)

**The ground**

- One-time hollow and bare-earth paint; a marker file stops a second sink.
  [`BrokenStretchBuilder.cs:68`](../../Assets/_Game/Editor/World/BrokenStretchBuilder.cs#L68)

- The terrain tool repaints the same patch on later runs.
  [`Area1TerrainBuilder.cs:673`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L673)

**Supporting**

- Tests: every collider's reach against the three walks.
  [`BrokenStretchTests.cs:150`](../../Assets/Tests/EditMode/BrokenStretchTests.cs#L150)

- Tests: how a collider's reach is worked out, up to head height.
  [`BrokenStretchTests.cs:190`](../../Assets/Tests/EditMode/BrokenStretchTests.cs#L190)

- Tests: thirteen wrongly shaped files, each refused without a throw.
  [`BrokenStretchTests.cs:111`](../../Assets/Tests/EditMode/BrokenStretchTests.cs#L111)

- Where the fire shows from, before and after the build.
  [`BROKEN_STRETCH_CONCEPTS.md:164`](../../Assets/_Game/Art/Environment/Camp/BROKEN_STRETCH_CONCEPTS.md#L164)

- What was built, and what differs from the artworks.
  [`BROKEN_STRETCH_CONCEPTS.md:217`](../../Assets/_Game/Art/Environment/Camp/BROKEN_STRETCH_CONCEPTS.md#L217)

- How the tool works and what to rerun after a change.
  [`Editor/World/CLAUDE.md:173`](../../Assets/_Game/Editor/World/CLAUDE.md#L173)
