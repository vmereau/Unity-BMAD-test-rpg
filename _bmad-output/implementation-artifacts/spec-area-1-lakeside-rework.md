---
title: 'Area 1 lakeside rework: fight pit, open shore and gang house'
type: 'feature'
created: '2026-11-06'
status: 'done'
baseline_commit: '3b086b6'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp's east side still follows the old plan: a hollow and rock fold at (32, 30) and, in `layout.json`, a palisade closing the lake. The scene has 16 of those panels deleted by hand, so a tool re-run would bring them back. Reference: `docs/unity screens/blockout/camp lake side.PNG`.

**Approach:** Update the layout, then build the new east side from the tools: a palisade that stops before the shore, a broken low rail, a sunken round fight pit inside a raised bank at the lake end, and an enterable gang house with a short dock. The rest stays free as a reserved `lakeside` zone.

## Boundaries & Constraints

**Always:** Graybox only, tool-owned and rebuilt from `layout.json`. Keep `layout.json` and `layout.seed.json` in sync. The fight is out of sight from the fort, but not a secret: the Company tolerates it. Pit, bank and house stay above the `Water_Volume` top (y −2). House ridge stays under y 10, so the tents-to-ruins view is no worse than today. The palisade ends short of the shore.

**Ask First:** The terrain tool reports uncaptured edits. A re-run would lose a hand edit found in the before/after diff. The bank can't hide the fort-side lines with slopes the player can use on the low sides.

**Never:** Tents or other `lakeside` content. Fight gameplay, bets, NPC placement or stash quest work. Fence or rail reaching the water. Terrain holes. Touching uncommitted `Core.unity`, the TMP font, the HDRP sample `.meta` or the moved screenshots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tool re-run | Terrain, Camp Edge, Sites run in order | Deleted east panels and old `Hollow` stay gone; pit, rail and house come back the same | Missing or bad feature: log error, nothing changed |
| Pit math | Point on floor, crest, gap, past foot | Floor lowered, crest raised and highest towards the fort, gap at ground level, zero offset past the foot | N/A |
| Capture after build | Pit in generated heights | `Capture Area 1 Terrain Sculpt` stores no pit offsets | N/A |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- site source of truth
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- `SculptHeights`, `Paint`, `TreeAllowed`
- `Assets/_Game/Editor/World/Area1TerrainMath.cs` -- pure, tested math
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs` -- `FenceIds`, Camp Edge
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- `BuildHollow`, feature validation
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildGuardPost`, house parts
- `Assets/Tests/EditMode/Area1TerrainMathTests.cs` -- math tests

## Tasks & Acceptance

**Execution:**
- [x] `layout.json`, `layout.seed.json` -- cut `outer-fence-4` to (54, 11.8)→(55.5, −2.9); add `lake-fence` (54, 11.8)→(63.4, 15.6); add `shore-rail` (56, 36)→(46.5, 46.8); replace `hollow` with `fight-pit` (52.5, 24); add `gang-house` (43.5, 53.5, 9 × 6, yaw 205), `gang-dock` and `lakeside`; reroute `path-night` from the pit gap to (33, 44); update the `fence` decision and notes -- the layout now matches the sketch and answers
- [x] `Area1TerrainMath.cs`, `Area1TerrainMathTests.cs` -- add a pure `FightPitOffset` and test it -- additive offset, so hand sculpt offsets stay valid
- [x] `Area1TerrainBuilder.cs` -- add the offset in `SculptHeights`, paint the floor mud, keep trees out of `lakeside` and `fight-pit` -- capture and rebuild both see the pit
- [x] `Area1BlockoutBuilder.cs` -- append `lake-fence` last in `FenceIds`; build `ShoreRail` stretches with walkable gaps -- keeps seeded order for new lines
- [x] `Area1SitesBuilder.cs` -- replace `Hollow` with `FightPit` (crest rocks, tall ones only if the sight test needs them), delete the old root, validate new features -- the fold no longer has a purpose
- [x] `Area1CampInterior.cs` -- extract the guard post's enterable shell unchanged; add `BuildGangHouse` (one room, bed, table, chest, hearth, door facing the camp) and `BuildGangDock` (ground-level back deck, dock, small boat) -- reuse without changing the guard post
- [x] `StartingTown.unity` -- run Terrain, Camp Edge, Sites; move the hand-placed bench, stone and log to the pit (the fir was deleted by hand and stays gone); rebake the NavMesh; save
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `area-001-company-encampment.md` -- describe what was built and drop "planned" wording

**Acceptance Criteria:**
- Given a marker head on the pit floor, when lines run from the tower top, fort wall and Company house door, then all are blocked, and screenshots confirm it.
- Given the pit and rail, when a player and a NavMesh path go from the tents, then both reach the pit floor through its gap and the shore through a rail gap.
- Given the gang house and dock, when a player and agent walk there, then the room and dock are reachable and the trail, bridge and hand-placed barricades stay clear.
- Given a before/after transform list of tool roots, when the tools have run, then only intended parts differ, and `GuardPost` is unchanged.
- Given the tents' eye, when viewed towards the ruins, then the ruins read as well as before.

## Spec Change Log

- Review pass 1: the audit measured the rail 0.6–2.8 m from the water where it rounded the bank, against the planned 3–7 m. Amended the Design Notes and `shore-rail` to start at (56, 36). Avoids a rail standing at the shoreline. KEEP: pit shape and screen rocks, house, dock, rail volumes. The bank's outer foot crosses natural lake bed below y −2 at its north-east edge; floor and crest stay above water, so the Always rule holds for what is built.

## Design Notes

**Pit:** floor radius 5.5 m at about y −0.8, crest at 9 m, outer foot at 12.5 m. Crest is about y 3.5 on the fort side (southwest; the tower sees the site today) and 1–1.5 m over ground elsewhere. The gap faces north, away from the fort. The bank is wider than the sketched circle and reaches the water to the east, so it marks that stretch itself. The rail runs from (56, 36) to the house, 3 m or more inland.

**House:** the sketch's front wall faces the camp, with the back yard and dock on the lake side. It sits 1 m further east than sketched to clear the barricades.

**Seeded panels:** shortening `outer-fence-4` shifts jitter on later panels by up to 0.3 m. Accepted.

## Verification

**Commands:**
- `run_tests` EditMode `Area1Terrain*` -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Eye-level screenshots of the pit, shore rail and house, nothing magenta.

## Suggested Review Order

**Fight pit shape**

- Start here: the pit is an additive offset, so hand sculpts stay valid.
  [`Area1TerrainMath.cs:156`](../../Assets/_Game/Editor/World/Area1TerrainMath.cs#L156)

- Added last in generation, so a capture stores no pit.
  [`Area1TerrainBuilder.cs:376`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L376)

- Rocks on the crest, and the fort-side screen the bank alone couldn't provide.
  [`Area1SitesBuilder.cs:710`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L710)

- Size limits stop a bad layout before any root is replaced.
  [`Area1SitesBuilder.cs:684`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L684)

**Fences**

- The palisade now stops short of the shore; the new line stays last in the seeded order.
  [`Area1BlockoutBuilder.cs:29`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L29)

- Broken rail, with Not Walkable volumes because a bake can't see it.
  [`Area1BlockoutBuilder.cs:170`](../../Assets/_Game/Editor/World/Area1BlockoutBuilder.cs#L170)

**Gang house**

- Shell extracted from the guard post, whose transforms are unchanged.
  [`Area1CampInterior.cs:428`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L428)

- Room and back deck on the shared shell.
  [`Area1CampInterior.cs:372`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L372)

- Dock takes its height from the deck.
  [`Area1CampInterior.cs:397`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L397)

**Peripherals**

- Trees stay clear of the house, deck and dock.
  [`Area1TerrainBuilder.cs:719`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L719)

- Old hollow root deleted on a run.
  [`Area1SitesBuilder.cs:303`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L303)

- Source of truth for all the new sites.
  [`layout.json:517`](../../docs/World/Areas/area-001/layout.json#L517)

- Pit math tests.
  [`Area1TerrainMathTests.cs:40`](../../Assets/Tests/EditMode/Area1TerrainMathTests.cs#L40)

- Lakeside notes, including the capture order trap.
  [`CLAUDE.md:66`](../../Assets/_Game/Scenes/CLAUDE.md#L66)

- The Company's tolerance of the fights.
  [`area-001-company-encampment.md:85`](../../docs/World/Areas/area-001-company-encampment.md#L85)
