---
title: 'Sealed Passage — goat-track route into the Ruins 2 ring (B2)'
type: 'feature'
created: '2026-11-18'
status: 'done'
baseline_commit: 'bd22c716d38864cc55b7a21f9c9ae4d9cfc030d9'
context:
  - '{project-root}/docs/World/Quests/SealedPassage.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Quest-002 promises a third way past the seal (the old goat track, "take a proper weapon"), but the Ruins 2
ring is closed on all sides except the cleft door. Also, a player who arrives that way and leaves by the door can
focus the owned leaf instead of the small `InsideLatch`, which counts as a theft and spends a lockpick.

**Approach:** Add a `path-goat-track` line to `layout.json`, graded into the live terrain by an editor tool (sculpt
captured), that climbs from the forest edge south-east of the quarry onto the natural shelf east of the ring, crosses
the rim in a shallow notch and benches down the inner face onto the pad. `BuildRuinsRing` leaves a gap only where the
track crosses it. Three existing darkness spiders guard the shelf. The door leaf defers to its latch whenever the
latch is usable (prompt, action and legality).

## Boundaries & Constraints

**Always:** Track grade ≤ 35° on average per segment and no 2 m run over 40° (player `slopeLimit` 45°); the ring stays
closed everywhere but the gap; ring gap logic in `Area1SitesBuilder` (survives a Sites rebuild); grading heights live
in the layout (`heights` per point), like `path-fort-ramp`; capture the sculpt after grading; rebake the NavMesh on
`Terrain_StartingArea` with `NavMeshAssetManager.StartBakingSurfaces`; hand-placed objects under StartingTown
`SealedPassage/GoatTrack`; pure rules in static helpers with EditMode tests.

**Ask First:** A full Terrain, Camp Edge or Sites rerun (apply the ring change by rebuilding only `Quarry/RuinsRing`);
any change to the cleft door, its seal or `Cap`; moving the track line by more than ~10 m from the plan below.

**Never:** Ropes, climbing mechanics or new creature types; new skill or key gates on the track; changes to the
cleft route; final art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Goat track entry | Player walks the track over the rim onto the pad | `RingEntered` set (second trigger box) | N/A |
| Leaf, inside, locked | Player on the latch side focuses the door leaf | Prompt "Lift the latch", not red; opens with no pick, no theft | N/A |
| Leaf, outside, locked | Player outside focuses the leaf | Unchanged: locked prompt, owned (red), key / pick rules | N/A |
| Leaf, unlocked | Door unlocked, any side | Unchanged open / close; owned rules as before | N/A |
| Ring wall crossing | Wall segment crossed by the track | Segment split, gap = track width + 1 m each side measured across the track | No crossing → wall unchanged |
| Explore first | Token taken before accepting the quest | Report topic only after the quest has started | N/A |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json` -- add `path-goat-track` (road, width 3, `points` + `heights`)
- `Assets/_Game/Editor/World/Area1FortRamp.cs` -- live-terrain grader for `path-fort-ramp`; template for the track grader
- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- `PolylineDistance`, `GradeAlong`, `GradedHeight`, `LineBlend`
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `TreeAllowed` (fort-ramp tree clearance pattern), `path-*` paint
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` (`BuildRuinsRing`) -- invisible ring walls, east wall at x ≈ 297
- `Assets/_Game/Scripts/World/DoorInteractable.cs`, `DoorLatch.cs`, `Ownership.cs` -- leaf / latch / theft rule
- StartingTown `SealedPassage/RingEntryTrigger` (`WorldFactTrigger`, child boxes) -- ring-entry setter
- `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` -- creature used at the cave
- `Assets/_Game/Data/NPCs/QuarryWorker/Memories/Mem_QuarryWorker_Report.asset` -- report unlock

## Tasks & Acceptance

**Execution:**
- [x] `Editor/World/Area1TerrainMath.cs` -- `GradeAlongProfile(point, line, heights, out distance)` (height lerped per segment) and `TrySplitAtCrossing(a, b, line, halfGap, out pieces)` (wall segment minus the gap where the line crosses, widened by the crossing angle) -- pure, testable
- [x] `Tests/EditMode/Area1TerrainMathTests.cs` (or existing file) -- profile lerp, split / no crossing / oblique crossing rows
- [x] `Editor/World/Area1FortRamp.cs` -- extract the grade pass so a second menu `Tools/World/Grade Area 1 Goat Track` grades `path-goat-track` from its `heights` (max 35°), paints Mud along it and removes trees within 3 m -- one grader, two lines
- [x] `docs/World/Areas/area-001/layout.json` -- `path-goat-track` ≈ (326,−66) y 6.3 → (328,−90) 17 → (325,−115) 33.5 → (320,−135) 45.6 → (312,−142) 47 → (303,−141) 50 → (282,−166) 30.5 → (277,−158) 30 (onto the pad, so the NavMesh joins)
- [x] `Editor/World/Area1TerrainBuilder.cs` -- tree clearance along `path-goat-track` like the fort ramp -- a terrain rerun keeps the track clear
- [x] `Editor/World/Area1SitesBuilder.cs` -- `BuildRuinsRing` splits any wall the goat track crosses (missing feature → closed ring + warning); rebuild only `Quarry/RuinsRing` in the scene
- [x] Scene -- run the grader, capture the sculpt, rebake; `SealedPassage/GoatTrack`: cairns at the forest-edge start and the rim, 3 `Monster_DarknessSpider Variant` on the shelf (~(318,−150)), generate their KilledFacts; `RingEntryTrigger` second box at the track's foot
- [x] `Scripts/World/DoorLatch.cs`, `DoorInteractable.cs`, `Ownership.cs` -- latch registers with its door; leaf uses the latch's prompt / `Interact` while it `CanInteract`; `Ownership.IsIllegalInteraction` gains a latch-usable flag (not illegal) -- the deferred ownership item
- [x] `Tests/EditMode/` ownership / latch tests -- leaf rows of the matrix
- [x] `Mem_QuarryWorker_Report.asset` -- also unlock on `QuestFact_SealedPassage_Started`
- [x] Docs -- `Scenes/CLAUDE.md` (goat track, ring gap, grader), `Scripts/World/CLAUDE.md` (latch deferral), quest spec checklist B2 + status, `deferred-work.md` strike-throughs

**Acceptance Criteria:**
- Given the scene after the grade, when the track is sampled every 0.5 m, then no 2 m run exceeds 40° and the NavMesh links the shelf to the pad through the notch.
- Given the ring after the rebuild, when the player pushes or jumps at any wall away from the gap, then they stay out; through the gap they reach the pad.
- Given the quest accepted, when the player reaches the pad by the track, then step 0 completes (+50 XP).
- `quest_report SealedPassage` and `quest_report audit` show no new issues; EditMode tests green; a scene screenshot shows the track and the gap.

## Design Notes

The shelf east of the ring (x 310–340, z −140..−180, y ≈ 47–52, slope ≤ 14°) and its north flank (≤ 37°) are
already walkable; only the rim (crest ≈ 55, 57° faces) needs grading. Notch at y 50 keeps the crossing shallow; the
inner descent (≈ 31°) benches along the face so it ends off the dressed centre. Spiders on the shelf mean the fight
happens before the rim, out of sight of the quarry. Deferring the leaf to the latch keeps one action per side without
resizing colliders.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- all pass
- MCP `quest_report SealedPassage`, `quest_report audit` -- no new issues

**Manual checks:**
- Grader log: average and steepest grade; ring gap width; scene-view screenshot of the track and the gap.
- Play mode: walk the track (NavMesh corners), confirm `RingEntered`; push at the ring beside the gap.

## Suggested Review Order

**Goat-track terrain**

- Entry point: the track is a layout line with one height per point, rising to the rim and falling.
  [`layout.json:2125`](../../docs/World/Areas/area-001/layout.json#L2125)

- Shared grade pass; profile mode, Mud paint and tree clearing only for the goat track.
  [`Area1FortRamp.cs:47`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L47)

- Undo now covers the alphamap textures the paint touches.
  [`Area1FortRamp.cs:111`](../../Assets/_Game/Editor/World/Area1FortRamp.cs#L111)

- Per-segment height lerp, so a line can climb over the rim and descend.
  [`Area1TerrainMath.cs:84`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L84)

- A terrain rerun keeps trees off the track, matching the grader's clearance.
  [`Area1TerrainBuilder.cs:741`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L741)

**Ring gap**

- Ring walls split only where the track crosses; logs the gap, warns unless exactly one.
  [`Area1SitesBuilder.cs:749`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L749)

- Cut ends stop flush; only corners overlap, so the gap keeps its 5 m.
  [`Area1SitesBuilder.cs:768`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L768)

- Gap widened by the crossing angle, measured square to the track.
  [`Area1TerrainMath.cs:116`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L116)

- Missing track feature keeps the ring closed with a warning.
  [`Area1SitesBuilder.cs:710`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L710)

**Door from the inside**

- Leaf defers prompt and action to the latch while it is usable.
  [`DoorInteractable.cs:46`](../../Assets/_Game/Scripts/World/DoorInteractable.cs#L46)

- Legality: a usable latch makes the locked leaf a legal use.
  [`Ownership.cs:49`](../../Assets/_Game/Scripts/World/Ownership.cs#L49)

- Latch registers on enable, so an initially inactive latch still takes over.
  [`DoorLatch.cs:49`](../../Assets/_Game/Scripts/World/DoorLatch.cs#L49)

**Tests**

- Profile lerp and split rows (square, oblique, none, near the end).
  [`Area1TerrainMathTests.cs:323`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L323)

- Latch-usable legality rows.
  [`OwnershipTests.cs:46`](../../Assets/Tests/EditMode/OwnershipTests.cs#L46)
