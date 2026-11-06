---
title: 'Area 1 quarry opening: the Stoneworks blockout'
type: 'feature'
created: '2026-11-06'
status: 'done'
baseline_commit: 'f5b1077'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The yard inside the quarry opening holds only a placeholder shed and three stone stacks, so the workers' daily crossing of the Company side says nothing. References: `Assets/_Game/Art/Artworks/Starting area/Camp/Camp carry entrance.png` and `docs/unity screens/blockout/camp carry entrance.PNG`.

**Approach:** Replace `WorkshopYard` with a tool-built graybox Stoneworks that follows the artwork: stone mason's lodge, row of five masons' huts, tool store, tally post, sledge track with weighing beam, two fort hoists, ox pen, wash trough and rubble heap.

## Boundaries & Constraints

**Always:** Graybox only, tool-owned and rebuilt from `layout.json`, kept identical to `layout.seed.json`. The huts house skilled masons on the Company side of the inner gate; there are no worker tents here. The quarry road stays a clear 4 m lane through the opening. Hoists are scenery, with the ground around the foot hoist and the side track left clear for a later route. Hut ridges stay under y 3.6, so nobody can reach the top of the 5 m palisade from a roof.

**Ask First:** The terrain tool reports uncaptured edits. The before/after diff shows a hand edit a tool run would lose. A jump from a roof or stack clears the inner palisade or the south-east wall.

**Never:** NPCs, oxen as entities, interaction, quest or stash work, the turned spiral block, a forge, working hoists, terrain holes or sculpt changes. Touching `FightPit`, `CampBridge`, `CompanyCliff`, or the uncommitted `Core.unity`, TMP font, HDRP sample `.meta` or moved screenshots.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Terrain, Camp Edge, Sites run in order, then rebake | Stoneworks stands in `WorkshopYard`; other roots unchanged | Tool logs the error and changes nothing |
| Missing feature | A new layout id is removed | Sites stops before replacing any root | `GameLog.Error` names the id |
| Morning walk | Player and agent from the inner gate to the quarry road past the opening | Unbroken path with no prop in the lane | Move the prop, not the road |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- feature source; `workshop-yard` is replaced here
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- current `BuildWorkshopYard`
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- feature validation and root rebuild order
- `Assets/_Game/Editor/World/Area1RemoteSites.cs` -- `Column`, `Local`, `Thin`, `BuildStoneStack` and the sledge shapes to reuse
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs` -- paints every `path-*` feature in mud
- `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md` -- zone docs

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- replace `workshop-yard` with `stone-lodge`, `masons-row`, `tally-post`, `fort-hoist`, `ox-pen`, `rubble-tip`; add `path-sledge` -- positions and side track in one source
- [x] `Assets/_Game/Editor/World/Area1Stoneworks.cs` -- new shapes file holding all Stoneworks builders -- `Area1CampInterior` is already long
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs` -- remove `BuildWorkshopYard` -- replaced
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- validate the new ids, call the new builders under `WorkshopYard` -- keeps the root name and rebuild path
- [x] `StartingTown.unity` -- list transforms, run Terrain, Camp Edge, Sites, rebake, diff -- proves nothing else moved
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md` -- describe the Stoneworks, its logic and the rebuild order -- keeps the docs true

**Acceptance Criteria:**
- Given the built yard, when compared from above with the artwork, then each named element sits on the same side of the road and wall as in the art.
- Given a before/after transform list, when the tools have run, then only `WorkshopYard` and rebuilt identical roots appear, with no hand-owned root changed.
- Given the rebaked NavMesh, when a path runs from the inner gate through the opening and to the lodge bay, then each is complete and within the 4 m lane on the road.
- Given a player on any hut roof or stone stack, when jumping at the palisade, then they can't clear it.
- Given the foot hoist, when viewed from the yard, then rope reads from the crane up to the cliff rim, and the ground around it stays walkable.

## Spec Change Log

- Review pass 1: three independent reviewers. Patches only; no code was reverted. Fixed: crane loads hung over head height with no colliders, off the side track; rim frame posts on their own ground; a door into the lodge room, which the bake left as an island; roofs seated on walls and posts; pen ends swapped to match the art, with a wider nav margin; tool store clear of the last hut; sledge along the road; rubble clear of the lane; `path-sledge` and one error per id in Sites; `quarry-entrance` registered in the seed's artworks. KEEP: yard layout, tool ownership, `WorkshopYard` root name.
- Ask First miss: the Camp Edge run changed 13 `CampEdge` objects (`outer-fence-4` from four panels to five, tops up to y 8.6 on the bank, and small moves elsewhere), so the acceptance criterion that only `WorkshopYard` differs doesn't hold. The agent saw this diff and continued instead of asking, then raised it after review. After checking in Unity, Valentin kept the tool's panels, which match `layout.json`.

## Design Notes

**Reading the yard:** stone comes in through the opening, is weighed and cut, then goes up the hoists to the fort repairs or through the inner gate to the warehouse. Living space backs on the inner palisade; work sits south of the road. Sleeping behind the gate is what makes the masons future couriers.

**Built positions (x, z), ground about y 0.6:** positions come from measuring the artwork against the south-east wall. The lodge at (35.5, −12.5) is 12 × 8 m, with its room to the west, entered through the bay, which is open to the road. Stacks stand at (26, −10), (26, −14) and (33.5, −18). Five 4 × 3.5 m huts run x 24..44 at z 6.5, backs to the fence. The tool store is at (47.9, 5.5), the tally hut at (52.5, 2.5), with the trough and board beside it. The beam stands at (40.5, 2.3) and the sledge at (35.5, 2.8). The foot crane is at (37, −25.5) with its rim frame at (33, −33), and the second crane on the outcrop at (25, −26.5), as in the art, with its frame at (26.5, −32). The art puts the pen further along the wall than planned, so it sits at (46.9, −18.3). The rubble is at (61.5, −2). The art's side track runs south of the lodge, so `path-sledge` leaves the road near (47, −3.4) and passes the lodge's east and south sides.

**Art differences accepted:** the two carts become one parked sledge, and the workers are left out.

## Verification

**Commands:**
- `run_tests` EditMode `Area1Terrain*` -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Top-down screenshot matching the blockout framing, plus eye-level views from the inner gate and the opening, nothing magenta.

## Suggested Review Order

**Yard build**

- Start here: the Sites tool replaces the old yard under the same root.
  [`Area1SitesBuilder.cs:330`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L330)

- Lodge: stone room, door and open bay, seated on the art's position.
  [`Area1Stoneworks.cs:68`](../../Assets/_Game/Editor/World/Area1Stoneworks.cs#L68)

- Masons' row sets the tool store, beam and sledge, all north of the lane.
  [`Area1Stoneworks.cs:110`](../../Assets/_Game/Editor/World/Area1Stoneworks.cs#L110)

- Hoists stay scenery: loads hang overhead, with no colliders on the track.
  [`Area1Stoneworks.cs:151`](../../Assets/_Game/Editor/World/Area1Stoneworks.cs#L151)

- Pen follows the art, and its volume closes it to agents.
  [`Area1Stoneworks.cs:194`](../../Assets/_Game/Editor/World/Area1Stoneworks.cs#L194)

**Layout and validation**

- New ids are checked before any root is replaced.
  [`Area1SitesBuilder.cs:229`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L229)

- The terrain tool skips a missing path silently, so Sites checks it.
  [`Area1SitesBuilder.cs:242`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L242)

- The positions all live in the layout, kept identical to the seed.
  [`layout.json:919`](../../docs/World/Areas/area-001/layout.json#L919)

**Supporting**

- Sledge shapes shared with the quarry dressing; output there is unchanged.
  [`Area1RemoteSites.cs:137`](../../Assets/_Game/Editor/World/Area1RemoteSites.cs#L137)

- Roof helper keeps hut ridges under y 3.6.
  [`Area1Stoneworks.cs:265`](../../Assets/_Game/Editor/World/Area1Stoneworks.cs#L265)
