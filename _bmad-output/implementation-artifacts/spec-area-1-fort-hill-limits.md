---
title: 'Area 1 fort: new hill limits, walls and cart ramp'
type: 'feature'
created: '2026-11-06'
status: 'done'
baseline_commit: 'c48eaeb'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The palisade cuts across the fort hill and the fort walls enclose only its north end, so the plateau has no defined edge before the interior is designed. Reference: `docs/unity screens/blockout/camp castle limit redefinition.PNG` (dashed = new limit, solid = ramp road, X = entrances) and `Art/Artworks/Starting area/Camp/Company fort.png`.

**Approach:** Run the perimeter along the plateau rim, stone facing the camp and the ramp, timber palisade on the east and south. Keep the foot entrance, add a wide cart entrance at the top of a graded, railed ramp, and a gate where the ramp passes through the palisade.

## Boundaries & Constraints

**Always:** Graybox, tool-owned, from `layout.json`, kept identical to `layout.seed.json`. Stone runs from the east junction along the north rim, past the tower, down the west side to the cart gate; palisade runs from the cart gate round the south and east rims to that junction. The ring stays closed: the only ways onto the plateau are the ruin-pillar foot entrance and the cart gate. Lines stay about 1.5 m inside the rim, and no panel reaches into the chamber around (−3, −44). The ramp is 5 m wide, about 16°, never over 17.6° (the warehouse cart ramps), with a rail on its outer edge. Its lower half stays outside the palisade, as sketched. The ramp is a sculpt, stored in `terrain-sculpt.bin`, so it survives rebuilds and stays hand-editable.

**Ask First:** The terrain tool reports uncaptured edits. The diff shows a hand edit a tool run would lose. A `CampEdge`, `Fort` or terrain change outside the hill and the expected ±0.6 m panel-top shift. The ramp can't reach 17.6° or less. A hoist or Stoneworks element has to move.

**Never:** Interior design, NPCs, door or gate interaction, moving `House`, the big building, `CompanyCliff`, `FightPit`, `CampBridge` or `SM_woodenCoverA`. Touching the uncommitted `Core.unity`, TMP font, HDRP sample `.meta` or moved screenshots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Ramp graded and captured; Terrain, Camp Edge, Sites run, then rebake | New perimeter stands; ramp heights unchanged by the rebuild | Tool logs the error and changes nothing |
| Missing feature | A new layout id is removed | The tool stops before replacing any root | `GameLog.Error` names the id |
| Grade tool rerun | Run twice | Second run changes no height beyond heightmap rounding | N/A |
| Unfinished capture | Grade tool run but not captured | Terrain tool refuses, as for any hand edit | Existing refusal message |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- fence, wall, gate, ramp and rail lines
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `FenceIds`, shared seeded jitter, `BuildShoreRail`
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- `BuildFort`, feature validation, root rebuild
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildGate`, which assumes a road along X
- `Assets/_Game/Editor/World/Area1TerrainMath.cs`, `Area1TerrainSculpt.cs` -- pure shaping helpers, height capture
- `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- math tests

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- `outer-fence` starts at the bottom gate; add `outer-fence-stub` (gate to rim); remove `outer-fence-6`, with `outer-fence-5` kept whole and its end (39, −37.7) as the junction; add `hill-palisade`, `ramp-gate`, `fort-cart-gate`, `ramp-rail`, `path-fort-road`, `path-fort-ramp`; reroute `fort-wall-west` and `fort-wall-east` onto the rim; update the fort zone -- single source of positions
- [x] `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- add a pure ramp height function: even grade along a line, flat across the road, blended shoulders -- testable
- [x] `Assets/_Game/Editor/World/Area1FortRamp.cs` -- new `Tools/World/Grade Area 1 Fort Ramp`: applies that shape from `path-fort-ramp` to the live terrain and logs the steepest grade -- sculpt, then capture
- [x] `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- update `FenceIds`, seed each line separately, generalise the rail for an ungapped `ramp-rail` -- removing a line otherwise reshuffles every later panel
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildGate` takes a yaw; yaw 0 output unchanged -- both new gates are angled
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- validate new ids, build `RampGate` and `Fort/CartGate` with stone piers -- gates end the wall lines
- [x] `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- grade, cross-section, shoulder and idempotence cases -- matrix coverage
- [x] `StartingTown.unity`, `StartingTownTerrain.asset`, `terrain-sculpt.bin`, NavMesh -- list transforms, grade, capture, run Terrain, Camp Edge, Sites, rebake, diff -- proves nothing else moved
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md` -- new limits, gates, ramp workflow; drop the "keep five panels" and "new ids last" rules -- keeps the docs true

**Acceptance Criteria:**
- Given a top-down capture, when compared with the sketch, then the perimeter follows the dashed line and all three X marks hold their entrances.
- Given the rebaked NavMesh, when pathing from the inner gate to the plateau, then a route exists through each entrance, and none exists with both blocked.
- Given the ramp, when sampled along its centre line, then no 2 m stretch exceeds 17.6° and the 5 m width has no cross-tilt over 5°.
- Given a player on the ramp or the plateau, when jumping at the perimeter, then they can't clear it.
- Given a before/after transform list, when the tools have run, then hand-owned roots are unchanged, and other `CampEdge` panels differ only in height by up to 0.6 m.

## Spec Change Log

- Review pass 1: three independent reviewers, patches only, no code reverted. Fixed: `ramp-gate` stood 25° off the road, so a leaf stood in the lane and the opening was about 4 m (now square at yaw 82.4, 5.5 m wide, no lean-to); fence seeds came from array position, so adding a line reseeded the rest (now a stable hash of the id); nothing kept trees off the ramp or plateau, and one stood at the cart gate (now excluded); the grade tool logged the mean grade and didn't dirty the asset (now logs the steepest 2 m, warns, saves, has Undo, and checks terrain and bounds); `ramp-rail` left a gap at the gate; the stone corner narrowed the cart gate; width validation; a test for the even grade; stale notes and docs. KEEP: perimeter lines, ramp line and its `foot`/`head`, sculpt-and-capture approach.
- Found in implementation: `outer-fence-5` stays whole, because its end (39, −37.7) is firm plateau and the planned junction sat in the rim notch. The grade tool reads `foot` and `head` from the layout: sampled from the ground they shifted by up to 5 cm each pass. The bend at (6, −68.6) was rounded, as it tilted the road 6.2°.
- Accepted exception to the jump criterion: the `outer-fence-stub` panel near (−1.9, −52.1) clears the cliff beside it by 1.23 m, just under the 1.27 m jump. Crossing it only moves between the ramp and the camp, which the open `RampGate` joins anyway, so it isn't a way onto the plateau.

## Design Notes

**Sketch to world:** the screenshot's camera was fitted to known wall and tower corners (1.8 px RMS) and the red marks projected on the terrain. Foot entrance (23.3, −34.6), cart entrance (8.7, −66.6), ramp bottom (−6.3, −52.1). Rim: z −33..−36 north, x ≈ 50 east, z ≈ −71 south, x ≈ 0 west.

**Removed:** the old inner wall lines, so `House` and the big building stand open in the bailey until the interior is designed.

**Road:** the sketch passes through `SM_woodenCoverA` at (−9, −46), so `path-fort-road` runs just west of it. The sketched ramp rises 7 m in 25.6 m but peaks at 27°; ending at the gate and starting north of the palisade gives about 30 m for 9 m of rise.

**Rim details:** the rim notch near (36, −31) pushes the stone line to z ≈ −36, leaving the hoist's rim frame at (33, −33) outside the wall.

**Sculpt, not generated:** the hill comes from hand edits, so a generated ramp would be added on top of them. The grade tool edits live heights and the capture stores them, the same path as a hand sculpt.

## Verification

**Commands:**
- `run_tests` EditMode `Area1Terrain*` -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Top-down capture matching the sketch framing, plus eye-level views from the ramp foot, cart gate and inner gate, nothing magenta.

## Suggested Review Order

**Perimeter**

- Start here: every wall, gate and ramp line lives in the layout.
  [`layout.json:1396`](../../docs/World/Areas/area-001/layout.json#L1396)

- Fort walls end in piers and solid ruins, keeping two ways in.
  [`Area1SitesBuilder.cs:506`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L506)

- Gate now turns with a yaw; yaw 0 output is unchanged.
  [`Area1CampInterior.cs:188`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L188)

- Ramp gate stands square to the road, with no lean-to.
  [`layout.json:1472`](../../docs/World/Areas/area-001/layout.json#L1472)

**Ramp grading**

- Sculpt tool, not generated ground: the hill is a hand sculpt.
  [`Area1FortRamp.cs:29`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L29)

- Bank steepens with distance, so a second pass changes nothing.
  [`Area1TerrainMath.cs:144`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L144)

- End heights are stored, because sampling them drifted each pass.
  [`layout.json:1594`](../../docs/World/Areas/area-001/layout.json#L1594)

- Logs the steepest 2 m, not the average.
  [`Area1FortRamp.cs:82`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L82)

**Rebuild safety**

- Seeds come from the id, ending the fence order rules.
  [`Area1BlockoutBuilder.cs:124`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L124)

- Trees stay off the ramp and plateau on every terrain run.
  [`Area1TerrainBuilder.cs:726`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L726)

**Supporting**

- Shared rail builder gives the ramp an unbroken, higher rail.
  [`Area1BlockoutBuilder.cs:107`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L107)

- Grade, bank and second-pass tests.
  [`Area1TerrainMathTests.cs:40`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L40)

- Layout rules and the ramp workflow for later sessions.
  [`CLAUDE.md:68`](../../Assets/_Game/Scenes/CLAUDE.md#L68)
