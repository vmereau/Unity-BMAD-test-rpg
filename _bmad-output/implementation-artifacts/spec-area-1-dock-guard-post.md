---
title: 'Area 1 blockout: dock guard post and working bank'
type: 'feature'
created: '2026-11-06'
status: 'done'
baseline_commit: '171bfed'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/docs/World/Areas/area-001/layout.json', '{project-root}/Assets/_Game/Scenes/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The compound has no Company presence near its road gate, and the bank doesn't read as a working fishing dock. The design also puts illegal fishing at the docks, which no longer fits a guarded compound.

**Approach:** Fill the palisade corner by the compound gate with a small, enterable guard post, and dress the bank as a working dock, using `Camp/Docks/docks example 1.png` for layout and `example 2` for guard-yard detail. Illegal fishing moves to the far side of the lake, in the doc only.

## Boundaries & Constraints

**Always:** A tool re-run replaces only the roots it owns. Positions come from `layout.json`. Sites are simple placeholder shapes. The compound stays closed to the four ways in: compound gate, warehouse, dock gate, swimming. Nothing taller than 1 m stands in the tents-to-fire strip (about x 3 at z 52 to x −8 at z 75). `BrokenStretch` stays open to sight, with no banner on it. `delivery-road` stays as painted, so the art's second road branch is not built. Runtime `NavMeshObstacle`s stay disabled. `ProjectSettings.asset` ends unchanged.

**Ask First:** Any terrain sculpt or paint. Moving a fence, gate, road, building or NPC not named here. A guard house over 8 × 5 m.

**Never:** Guards, NPCs, door leaves, locks or other gameplay. The lake fishing spot itself. A back way in or loose plank. Smoke or other VFX. Touching `Core.unity`, the TMP font, Shimmer2 meta, `.lighting` files or the meshy test folder. Using artworks in-game.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Re-run | Sites tool run twice | Same result; hand-built objects untouched | N/A |
| Broken layout | `guard-post` missing, or its `w`, `d` or `yaw` unusable | Logs the id, changes nothing | Abort before writing |
| Sloped footprint | Ground falls about 3 m across the house | Floor at the highest sampled ground, downhill side on posts | Existing floor sampling |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildWarehouse`, `DoorWall`, `Stilt`, `Slab`, `BuildStagingYard`, `BuildWalkway`
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- feature validation, `NewRoot`, `WalkwayIds`, build order
- `Assets/_Game/Editor/World/Area1RemoteSites.cs` -- `Local`, `Column` shape helpers
- `docs/World/Areas/area-001/layout.json` -- zones and walkways
- `Assets/_Game/Scenes/StartingTown.unity`, `Scenes/StartingTown/NavMesh-Terrain_StartingArea.asset` -- rebuilt and rebaked

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json` -- add `guard-post` (zone, x −30.5, z 48.5, w 8, d 5, yaw −43) and `walkway-guard-post`; widen `staging-yard` to w 30, d 12 and update its note -- the same centre keeps the existing offsets
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs` -- add `BuildGuardPost`; extend `BuildStagingYard` with smokehouse, net racks, gutting table and barrels; remove `WatchPost` -- the post takes over its job
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- validate `guard-post`, add the `GuardPost` root and the walkway -- abort before writing, like other features
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- run Sites, bake, save, run tests
- [x] `docs/World/Areas/area-001/layout.seed.json`, planner artifact -- refresh and republish
- [x] `docs/World/Areas/area-001-company-encampment.md`, `Assets/_Game/Scenes/CLAUDE.md`, `deferred-work.md` -- add the guard post, remove the watch post, move illegal fishing to the lake's far side (night description, Opportunities table), fix the artwork path, log the lake spot as deferred

**Acceptance Criteria:**
- Given the compound gate, when walking in along the delivery road, then the guard house, its door and its ramp read at eye level, and the road and gate stay clear.
- Given the player and a NavMesh agent, then both can climb the ramp, enter the house and walk its room, and still reach the compound, staging yard, deck, gates, fort gate and all sites.
- Given the guard post, then no shape overlaps a fence panel, the road, or the gate's swing.
- Given a marker at night, when viewed from (11, 35), the docks and the bridge, then a screenshot shows the fire's glow.
- Given eye-level and aerial screenshots, when compared with `docks example 1`, then the layout reads the same apart from the gate gap, and nothing is magenta.
- Given all NPC routines, then they complete as before.

## Spec Change Log

- Review pass 1: three independent reviewers. Patches only; no code was reverted. Fixed: walkway rerouted round the old gate lean-to at (−36.8, 54.1), which stays (moving it is Ask First); banner cloth added; bell post moved to the specified point; firewood moved beside the door; ramp warns at its cap and tests slope both ways; `guard-post` size limited to what the props fit; post filter applied to the front row only; smokehouse gables closed; tally shed roof seated. KEEP: floor at the highest ground, the skirt, and the 30° ramp.

## Design Notes

**Guard house:** 8 × 5 m, walls 3 m, one room about 7 × 4 m with hearth, table and four bunks. Its back runs 1 m off the palisade between (−20.7, 52.8) and `Tower_R`, and its long front faces the road. Ground is y 6.0 at the back corner and 3.1 at the front corner, so the floor sits at the highest ground, as at the warehouse.

**Door and ramp:** a 1.6 × 2.4 m doorway in the front wall, 2 m towards the gate from the centre. A straight 18° ramp would run 15 m, because the ground keeps falling, so this one is about 4.5 m at up to 30° and 1.6 m wide, built like the art's steps. Foot near (−36.5, 52), 3 m short of the road's edge. Agent slope limit is 45°.

**Pocket:** lockup cage 2.5 × 2.5 m at about (−35, 42.5), in the corner, closed for now. Tally shed about 3 × 2.5 m at (−24.3, 52.8), solid, with a hatch facing the road. Bell post at (−33, 53.5). Weapon rack, chopping block and firewood in the yard beside the door. Plain banner on its own pole beside the post.

**Beds:** the four bunks are two double-bunk blocks.

**Bank:** smokehouse 4 × 4 m at (−37.5, 65.5), solid, 1.7 m clear of the shed and clear of the gate. Net racks near (−25, 70) and (−16, 69.2), gutting table and barrels near (−21, 69.5). The shelf is y 0–0.6 here and the waterline (y −2) falls near z 72.

## Verification

**Commands:**
- Unity EditMode tests -- expected: all pass

**Manual checks:**
- `Tools/World/Build Area 1 Sites` twice gives the same result, and the console has no errors

## Suggested Review Order

**Guard house**

- Entry point: fits the house to sloped ground, with local Z facing the road.
  [`Area1CampInterior.cs:329`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L329)

- Lengthens until 30°, because the ground keeps falling; warns if it can't.
  [`Area1CampInterior.cs:426`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L426)

- Closes the space under the floor so nothing walks or bakes there.
  [`Area1CampInterior.cs:401`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L401)

**Pocket and bank dressing**

- Bell, weapon rack, firewood and banner, positioned from the house frame.
  [`Area1CampInterior.cs:385`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L385)

- Closed cage in the corner; bars carry colliders.
  [`Area1CampInterior.cs:442`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L442)

- Solid tally shed with its hatch to the road.
  [`Area1CampInterior.cs:464`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L464)

- Bank dressing replaces the watch post, all west of the fire strip.
  [`Area1CampInterior.cs:275`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L275)

**Layout and validation**

- Aborts before any root is replaced; size limits match the fitted props.
  [`Area1SitesBuilder.cs:234`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L234)

- The one feature with a yaw.
  [`layout.json:1087`](../../docs/World/Areas/area-001/layout.json#L1087)

- Fixed start by the ramp foot, routed round the gate lean-to.
  [`layout.json:1099`](../../docs/World/Areas/area-001/layout.json#L1099)

**Docs**

- Illegal fishing moves to the lake.
  [`area-001-company-encampment.md:175`](../../docs/World/Areas/area-001-company-encampment.md#L175)
