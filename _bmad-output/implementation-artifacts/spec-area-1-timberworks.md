---
title: 'Area 1 Timberworks and Company store blockout'
type: 'feature'
created: '2026-11-07'
status: 'done'
baseline_commit: '9b50fbc'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The wood workers' side of the Company yard holds only a few log stacks and carts, so wood has no story like stone does. The Storekeeper has no building, and Edric's open-air forge doesn't match his wish for a bigger one. References: `docs/unity screens/blockout/camp wood workers entrance + fort entrance.PNG` (red circle), `Assets/_Game/Art/Artworks/Starting area/Timber yard.png`.

**Approach:** Replace `TimberYard` with a tool-built graybox Timberworks that follows the wood from the opening to finished goods: receiving yard and sawpit, seasoning shed, carpenters' shop with a cooper's lean-to, charcoal clamp and a bigger forge. Add a Company store building, and move Edric and his stations to the forge.

## Boundaries & Constraints

**Always:** Graybox only, tool-owned, positions in `layout.json`, features kept identical to `layout.seed.json`, root name `TimberYard`. The wood road and commute stay clear. A new lane links the wood road to the fort road, leaving room south of it for the garrison spec. The forge, store and carpenters' room are enterable. Edric's objects stay hand-owned scene roots, outside `Area1_Blockout`.

**Ask First:** A hand-edit Scan shows new rows that aren't height-only before the first tool run. The terrain tool reports uncaptured edits. A terrain run changes hand-painted ground in the camp. A roof or stack lets a jump clear a palisade.

**Never:** Garrison quarter, alehouse, barracks, NPCs other than Edric, interaction changes, moving the Storekeeper, smoke or other VFX, sculpt changes. Touching `Core.unity`, the TMP font, the HDRP `.meta`, `FightPit`, `CampBridge`, `CompanyCliff`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Terrain, Camp Edge, Sites, then rebake | Timberworks in `TimberYard`, lane painted, other roots unchanged, hand edits reapplied | Tool logs the error and changes nothing |
| Missing feature | A new layout id is removed | Sites stops before replacing any root | `GameLog.Error` names the id |
| Edric at work | Day and night routine blocks | Reaches his forge spot and his night spot | Move the spot, not the shell |
| Smithing | Player with Smithing at the anvil | Station works as before | N/A |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- feature source; `timber-yard` stays and shrinks to the receiving yard
- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildTimberYard`, `BuildCart`, `BuildHouseShell` (private), `BuildHearth`
- `Assets/_Game/Editor/World/Area1Stoneworks.cs` -- closest pattern; `Group`, `GableRoof` are private
- `Assets/_Game/Editor/World/Area1RemoteSites.cs` -- `BuildLogStack`, `BuildSawPit`, `BuildLeanTo`, `Local`, `Column`
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:363` -- feature validation and the `TimberYard` build call
- `Assets/_Game/Art/Terrain/StartingTownTerrain_Source.asset` -- still holds the 6 chamber holes near (−4, −46)
- `Assets/_Game/Scenes/StartingTown.unity` -- `Blacksmith`, `SmithingStation_Anvil`, `SM_Chimney_Segment`, `SM_WoodBench_01a`, `NpcRoutineSpots/Spot_Blacksmith_Night`

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- add `forge`, `company-store`, `seasoning-shed`, `carpenters-shop`, `charcoal-clamp`, `path-timberworks`; refit `timber-yard` -- one source for positions
- [x] `Assets/_Game/Editor/World/Area1Timberworks.cs` -- new file with all builders; share the private helpers it needs -- `Area1CampInterior` is already long
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs` -- remove `BuildTimberYard` -- replaced
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- validate the new ids, including the path, and build them under `TimberYard` -- same rebuild route
- [x] `Assets/_Game/Art/Terrain/StartingTownTerrain_Source.asset` -- clear the 6 chamber holes -- a terrain run would restore them, as listed in `deferred-work.md`
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- Scan hand edits, list transforms and hole count, run Terrain, Camp Edge, Sites, rebake, diff -- proves nothing else moved
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- move Edric, anvil, chimney, bench and night spot to the forge, and check his routine and home position -- his wish comes true
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md`, `Assets/_Game/Data/NPCs/BlackSmith/CLAUDE.md`, `Assets/_Game/Data/NPCs/Storekeeper/CLAUDE.md`, `Assets/_Game/Scripts/AI/CLAUDE.md` -- describe the site, fix `Village house` references -- keeps docs true

**Acceptance Criteria:**
- Given the built yard, when viewed from above, then wood reads in order from the opening: tally and stacks, sawpit, seasoning shed, carpenters, with the forge and store along the lane side.
- Given the rebaked NavMesh, when paths run from the inner gate to the wood opening, the forge, the store, the carpenters' room and the fort road via the lane, then each is complete.
- Given a player on any new roof or stack, when jumping at a palisade, then they can't clear it.
- Given the rebuild, when compared with the before list, then only `TimberYard`, Edric's objects and identically rebuilt roots differ, and the chamber holes stay gone.

## Spec Change Log

- Review pass 1: three independent reviewers, patches only, no code reverted, frozen block untouched. Fixed: the shed's posts stopped 3.3 m short of its east end; the cooper's roof hung in the air on its high side; two posts did nothing (`Post_East` inside the wall, the forge's `Post_West`); the bed floated; Storekeeper wording; an Open Thread removed without being asked; closed deferred entries. KEEP: positions, `TimberYard` root, `Area1Timberworks` as a separate file, hand-owned Edric objects.
- Ask First review. Scan before the first run: all 84 saved rows, 15 height-only rows from the flatten, and two "Added" rows that are generated objects, not hand work (`Barricades/NotWalkable_5`, already deferred, and `GuardPost/Post_1`, skipped because the ground at y 5.88 is now above the floor at y 3). Terrain run: 506 alphamap cells changed outside the lane, nearly all swaps with the slope-driven rock layer in strips along the banks and the ring, so read as regenerated, not brushwork. Neither stopped the work, and both are reported to Valentin here.
- Accepted differences from AC4: Camp Edge caught up with the flatten, so 13 `outer-fence` panels changed height, by up to 4.56 m. The camp's tree positions weren't compared, because no before list was taken; the count stayed 4419. See the deferred note on panels 012-015. `timber-yard`'s rectangle is the yard's core: `Logs_1` and the saw roof spill past it by up to 1.5 m, clear of the lane.
- Play test: Edric walked to his night spot and back on the clock, and the anvil refused without Smithing and opened with it.

## Design Notes

**Positions (x, z), ground y 0–0.6:**

| Feature | Centre | Size | Notes |
|---------|--------|------|-------|
| `timber-yard` | (−39, −3) | 12 × 12 | Stacks, carts, tally board, tool shelter, sawpit under an open roof |
| `path-timberworks` | (−30, 4) → (−30, −10.5) → (−6, −10.5) | 3.5 wide | Paint only |
| `forge` | (−23.5, −4) | 9 × 7 | Stone hearth and chimney, open towards the lane, small side room for Edric |
| `company-store` | (−12.5, −5) | 9 × 6 | House shell, door north towards the commute |
| `seasoning-shed` | (−37, −16.5) | 14 × 6 | Long open-sided roof over plank stacks: the warehouse |
| `carpenters-shop` | (−21, −17) | 12 × 8 | Room plus open bay on the lane; cooper's lean-to and barrels at the east end |
| `charcoal-clamp` | (−34, −24.5) | 7 across | Earth mound with wood stacks beside it |

**Why wood feeds the forge:** the clamp sits behind the shed, so wood visibly becomes fuel, and the forge sits on the lane where workers see it each morning.

**Terrain run:** only the lane needs it. Holes are cleared first so the run can't undo the hand removal.

## Verification

**Commands:**
- `run_tests` EditMode, groups `Area1HandEditsTests`, `Area1Terrain.*` -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Top-down shot in the blockout framing and eye-level views from the inner gate and wood opening, nothing magenta. Play test Edric's routine and the anvil.

## Suggested Review Order

**Building the yard**

- Entry point: Sites now builds every Timberworks piece under the old root.
  [`Area1SitesBuilder.cs:376`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L376)

- The wood's first stop: stacks, carts, tally and a roofed sawpit.
  [`Area1Timberworks.cs:47`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L47)

- Forge: hearth on the back wall, open to the lane, Edric's room east.
  [`Area1Timberworks.cs:66`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L66)

- Shed posts now reach both ends, after review found the east end bare.
  [`Area1Timberworks.cs:94`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L94)

- Shop and cooper's lean-to, whose high side now has its own posts.
  [`Area1Timberworks.cs:109`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L109)

- Stepped clamp stands in for a mound in the blockout.
  [`Area1Timberworks.cs:147`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L147)

- Store reuses the shared house shell, so its floor and ramp match the others.
  [`Area1Timberworks.cs:157`](../../Assets/_Game/Editor/World/Area1Timberworks.cs#L157)

**Layout and validation**

- Lane is checked here because the terrain tool skips a missing path silently.
  [`Area1SitesBuilder.cs:277`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L277)

- Positions live in the layout, identical to the seed.
  [`layout.json:924`](../../docs/World/Areas/area-001/layout.json#L924)

**Supporting**

- Shell made internal so the store can share it.
  [`Area1CampInterior.cs:426`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L426)

- Who owns what, including Edric's hand-placed objects.
  [`CLAUDE.md:76`](../../Assets/_Game/Scenes/CLAUDE.md#L76)
