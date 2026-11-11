---
title: 'Area 1 timber and tarp worker settlement'
type: 'feature'
created: '2026-11-11'
status: 'done'
baseline_commit: 'cacee57'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001/worker-settlement-redesign.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Repeated small tents understate the camp's established timber supply and provide limited spatial variety. The canteen has no building, and housing offers few sheltered intermediate spaces.

**Approach:** Rebuild the worker housing as rough timber huts in small courtyard groups, with selected gaps bridged by tarp sleeping bays. Preserve the existing zones and social hierarchy, retain full tents in the work-gang pocket, and add a timber canteen with covered outdoor eating space. Implement the design proposal in a reproducible graybox.

## Boundaries & Constraints

**Always:** Keep zone locations, Company Road, square, three 6 m sight lanes, ditch crossings, hunter-fire view and lakeside access. Adapt to the current saved hand-arranged camp, preserving the player anchor at (-0.52,47.97), the current bed and routine placements, with a front entrance and rear viewing opening. Keep fires uncovered. Record footprint, orientation and connecting shelters in both layout files. Preserve hand-owned gameplay objects and their references, including Cook, cooking station, bed, spawn, NPC routines and HiddenStashQuest. Scan before rebuilding. Validate before replacing roots. Preserve baseline scene inventory changes and Company art.

**Ask First:** A layout cannot fit without moving a zone, changing terrain, altering a protected view or removing unrelated hand edits. Unexpected hand-owned content would be lost or displaced.

**Never:** New NPCs, dialogue, quests, locking systems, final art or changes to fort, farm, work sites, warehouse, gang house, pit, fences or Core.unity. No terrain rebuild merely to place buildings. Do not rename Tent Mate's gameplay identity in this pass.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Rebuild | Valid settlement features | Same huts, tarps, canteen and local walkways on repeated builds | Preserve hand-owned objects |
| Bad geometry | Missing dimensions, intersecting solids or obstructed protected corridor | Abort before replacing live roots | Identify the offending feature |
| Connected tarp | Valid attachments to two shelters | Roof connects; posts and beds leave passage clear | Reject ambiguous attachments or insufficient clearance |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1TentCamp.cs`: plan parsing, circular footprint validation, housing builders and hardcoded player-group offsets.
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs`: validation at TryLoadFeatures, full Sites rebuild and root ownership; needs scoped settlement entry.
- `Assets/_Game/Editor/World/Area1CampInterior.cs`: timber shell and visual boardwalk helpers; larger shell defaults are unsuitable for every hut.
- `docs/World/Areas/area-001/layout.json`, `layout.seed.json`: shared layout source and seed.
- `docs/World/Areas/area-001/planner.html`: import/export preserves feature fields; rotated footprint rendering must match new yaw values.

## Tasks & Acceptance

**Wrap-up scope amendment (user request, 2026-11-11):** The user completed the blockout by hand and asked to wrap up against that actualized version. The saved scene now supersedes generator footprints and earlier placement targets. Preserve the entire TentCamp root as hand-owned; do not regenerate it to satisfy the obsolete tasks below. Original unchecked tasks are retained as history, not claims of completed verification.

**Completed wrap-up:**
- [x] Inspect current saved scene and record its 75 huts, 26 tarp bays, canteen and gameplay anchors.
- [x] Protect all TentCamp contents from both scoped and full regeneration through explicit ownership in both layout files.
- [x] Keep current scene, terrain, Core and hand-authored objects unchanged.
- [x] Add focused oriented-footprint/corridor tests and run relevant existing tests (97/97 pass).
- [x] Update area design, scene instructions and proposal to state current authority and builder ownership.
- [x] Record guard non-mutation and existing NavMesh connectivity checks plus remaining manual validation in `../../docs/World/Areas/area-001/worker-settlement-verification.md`.

**Execution:**
- [ ] `docs/World/Areas/area-001/worker-settlement-redesign.md`: finalize cluster arrangements against scene measurements; retain artwork briefs for visual iteration.
- [ ] `layout.json`, `layout.seed.json`: add explicit hut, tarp and canteen footprints and local walkway features. Keep existing route geometry; use courtyard anchors from the proposal. Record intentional tarp attachments explicitly.
- [ ] `Area1TentCamp.cs`: implement compact enterable hut shells, partial Wretches shelters, attached sleeping bays and timber market frontage; account for roofs, posts, beds, porches and steps in validation. Add protected route and hunter-view checks without broadly bypassing overlap checks.
- [ ] `Assets/_Game/Editor/World/Area1WorkerCanteen.cs`: build timber cookhouse, serving opening, eating canopy and service access around the measured Cook/station positions.
- [ ] `Area1SitesBuilder.cs`: integrate canteen and provide scoped settlement rebuild using existing hand-edit safeguards, leaving other site roots untouched.
- [ ] `planner.html`: render the new oriented footprints and ensure load/export round-trips them.
- [ ] `Assets/Tests/EditMode/Area1WorkerSettlementTests.cs`: test invalid footprints, protected corridors, attachment handling and failed-validation non-mutation using focused geometry/build fixtures.
- [ ] `Assets/_Game/Scenes/StartingTown.unity`: scan and record baseline, rebuild settlement, verify gameplay placements, rebake the existing terrain NavMesh asset, save and compare unrelated roots.
- [ ] `docs/World/Areas/area-001-company-encampment.md`, `Assets/_Game/Scenes/CLAUDE.md`: update housing/canteen descriptions and tool ownership to the implemented layout. Preserve completed historical specs.

**Acceptance Criteria:**
- Given overhead and ground-level views, when comparing the rebuilt zones, then rough neutral huts, poorer partial shelters and better Mafia housing are distinct, with a retained full-tent pocket.
- Given each inhabited cluster, when approaching its yard, then its entrance, communal space and onward route are legible; tarp sleeping bays connect buildings without covering fires or principal lanes.
- Given the three lane endpoints and player rear opening, when checking the punishment post and hunter fire respectively, then geometry leaves their views clear.
- Given player controller traversal and baked navigation, when travelling from spawn to square, canteen, latrine, inner gate, wilderness opening, market and gang-house ramp, then all routes complete without snagging on new geometry.
- Given the canteen, when approaching the Cook and cooking station, then existing interactions work and the queue, tables and posts do not obstruct access.
- Given the saved baseline, when comparing after rebuild, then unrelated roots, terrain heights/paint/trees and gameplay references remain unchanged.

## Design Notes

Approved by Valentin on 2026-11-11. Visual reference: `Assets/_Game/Art/Artworks/Starting area/Camp/Tents side/Ground-level study.png` and `Overhead study.png`. The user confirms their vibe and look as the target: steep plank roofs, exposed frames, canvas sleeping bays and short boardwalks. Preserve the approved geographic constraints rather than copying the artwork's invented terrain or gates.

Use two neutral courtyard groups at existing northern and southern fire anchors; reserve Wretches' incomplete frames north-west and Mafia's enclosed platform huts east of the ditch. Start the canteen around (-14,22), connecting to existing boardwalk endpoint (-9,23); fit its final outline around measured hand-owned objects. Prefer small shells to wholesale enlargement of tent footprints. Target 2.5 m clear principal pedestrian routes, 1.5 m secondary passages, 2.1 m door headroom and steps below 0.28 m. Camera clearance may require wider passages. Avoid roof access that bypasses the inner palisade.

## Verification

- Unity compile and focused EditMode tests pass; existing Area1 hand-edit and terrain tests remain passing.
- Verify layout/seed parity and planner round-trip; run scoped rebuild twice and compare output.
- Capture overhead, player-courtyard, canteen and protected-view screenshots. Play-test traversal and existing interactions; do not rely solely on NavMesh paths around the warehouse ramp.
- Inspect final diff for unrelated scene roots, terrain, Core and asset changes.

## Spec Change Log

- Wrap-up request after the user's manual blockout: keep all new duplicates and child edits as authored. TentCamp is now scene-owned, including Canteen and Ditch; scoped regeneration refuses and full Sites skips this root. Historical plot data remains reference only. The original recreation and full visual certification goals are superseded by preservation and documented verification limits. No user geometry was reverted.

- User explicitly approved adapting to the current saved arrangement on 2026-11-11. Captured 31 housing anchors; player is (-0.52,47.97). Preserve courtyard props and ditch transforms rather than restoring the older layout. Scene backup: Temp/WorkerSettlement/StartingTown.before.unity.

- Review: excluded finalized TentCamp from hand-edit replay as well as generation; marked old generator instructions historical. Three review lenses checked preservation and verification claims.

## Suggested Review Order

- Full rebuild preserves finalized housing and excludes stale hand-edit replay.
  [Area1SitesBuilder.cs:452](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L452)
- Scoped rebuild refuses to overwrite the accepted scene.
  [Area1SitesBuilder.cs:223](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L223)
- Ownership flag distinguishes historical plots from the final scene.
  [Area1WorkerSettlement.cs:13](../../Assets/_Game/Editor/World/Area1WorkerSettlement.cs#L13)
- Evidence and remaining navigation, camera and view checks.
  [worker-settlement-verification.md:1](../../docs/World/Areas/area-001/worker-settlement-verification.md#L1)
- Focused geometry tests supplement existing hand-edit and terrain tests.
  [Area1WorkerSettlementTests.cs:1](../../Assets/Tests/EditMode/Area1WorkerSettlementTests.cs#L1)
