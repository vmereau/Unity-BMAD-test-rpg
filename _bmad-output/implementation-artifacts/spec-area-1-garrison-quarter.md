---
title: 'Area 1 garrison quarter blockout'
type: 'feature'
created: '2026-11-07'
status: 'done'
baseline_commit: 'b051532'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The ground under the fort ramp, between the Timberworks and the south-west palisade, is empty, so the Company side has nowhere for its guards and staff to live. The palisade there stands below the bank, so a player on the hill can step over it. Reference: `docs/unity screens/blockout/camp wood workers entrance + fort entrance.PNG` (blue circle).

**Approach:** Move `outer-fence` onto the top of the bank, then add a tool-built graybox quarter on one spine off the fort road: barracks with a locked armoury, Company staff house, The Short Measure alehouse, drill yard, grain store on stilts, workers' graves on the bank, and reserved plots for kennels and a barber-surgeon.

## Boundaries & Constraints

**Always:** Graybox only, tool-owned, new root `Garrison`, positions in `layout.json`, features identical in `layout.seed.json`. One way in: the spine from the fort road, with the drill yard as the square. Barracks, staff house and alehouse are enterable. Armoury and grain store are closed rooms with a solid door, ready for later theft quests. Extras: staff-house pay hatch and queue rail, ledger room, well, alarm bell, archery butts. Hand edits are scanned before the first tool run and saved `Panel_NNN` paths still resolve afterwards.

**Ask First:** A Scan shows new rows that aren't height-only. The terrain tool reports uncaptured edits. A terrain run changes hand-painted camp ground. A roof lets a jump clear a palisade. A planned footprint doesn't fit the measured ground. Grave numbers can't render without magenta.

**Never:** NPCs, dialogue, interaction, locks, quests, kennels or barber-surgeon buildings, brew shed, back lane, sculpt changes. Touching `Core.unity`, the TMP font, the HDRP `.meta`, `FightPit`, `CampBridge`, `CompanyCliff`. Committing those three files.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Terrain, Camp Edge, Sites, rebake on `Terrain_StartingArea` via `NavMeshAssetManager.StartBakingSurfaces` | `Garrison` built, line moved, paths painted, other roots unchanged, hand edits reapplied | Tool logs the error and changes nothing |
| Missing feature | A new layout id is removed | Sites stops before replacing any root | `GameLog.Error` names the id |
| Hill to camp | Player on the hill jumps at the moved line | Can't clear it anywhere | Raise or move the point |
| Panel renumbering | Moved line changes the panel count | Saved rows point at their same panels, no warning on run | Stop and fix the paths |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- feature source; identical today
- `docs/World/Areas/area-001/hand-edits.json` -- 19 `CampEdge/*/Panel_NNN` rows, all after `outer-fence` in `FenceIds`, so they shift
- `Assets/_Game/Editor/World/Area1BlockoutBuilder.cs:132` -- `BuildPalisade`; numbers panels across lines
- `Assets/_Game/Editor/World/Area1Timberworks.cs` -- closest pattern
- `Assets/_Game/Editor/World/Area1CampInterior.cs:426` -- `BuildHouseShell` takes a rotation; door on local +Z
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:353,391` -- site builds, feature and path validation
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs:94` -- `path-` lines are painted

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json` -- set the new `outer-fence` points; add the features in Design Notes -- one source for positions
- [x] `docs/World/Areas/area-001/hand-edits.json` -- shift saved panel numbers by the count change -- names count across lines
- [x] `Assets/_Game/Editor/World/Area1Garrison.cs` -- new builders for every site part -- keeps `Area1Timberworks` focused
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- validate the new ids and paths, add the `Garrison` root to the built roots -- same rebuild route
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- leave Play mode, Scan, record a before list, run Terrain, Camp Edge, Sites, rebake, diff -- proves nothing else moved
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md`, `_bmad-output/implementation-artifacts/deferred-work.md` -- describe the site, fix the old Company House "guards' quarters" line, close the garrison and panel entries, add kennels and barber-surgeon -- keeps docs true

**Acceptance Criteria:**
- Given the built quarter, when viewed from above, then the spine, yard, barracks by the ramp gate, staff house on the road and graves on the bank all read, and the reserved plots stay empty.
- Given the rebaked NavMesh, when paths run from the fort road to the barracks, staff house, alehouse, yard and graves, then each is complete.
- Given the armoury and grain store, when a path targets their interiors, then none exists.
- Given a player on any new roof, when jumping at a palisade, then they can't clear it.
- Given the rebuild, when compared with the before list, then only `Garrison`, `outer-fence` and identically rebuilt roots differ, plus paint under the new paths.

## Spec Change Log

- Scan before the first run: 84 saved rows, no new rows, so no Ask First. Terrain: map colour changed only inside the garrison box, heights and all 4419 trees unchanged. Rebuild comparison: the 3892 objects outside `CampEdge` and `Garrison` match the before list exactly, and the 234 objects on the other fence lines keep their poses under their shifted names. `BuildHouseShell` gained optional door and ramp arguments, because the 4 m default crossed the spine.
- Review pass 1: three independent reviewers, patches only, no code reverted, frozen block untouched. Fixed: two unrelated deferred entries lost to mixed line endings; a butt touching a grave; props inside the reserved plots (kennels moved 0.5 m, barber plot now 3.2 m wide); pells on the path and the bell in its mouth; a ledger room too cramped to use; a counter too high for someone on the ground; culture-dependent names; plot minimum sizes and the two reserve ids missing from validation. Numbers moved from `TextMesh` to TextMeshPro, as the built-in font material ignores depth. Docs now say the spine is the one made path, as the ground east of the staff house stays open. KEEP: positions, `Garrison` root, the armoury as the barracks' east end.
- Hill to camp, found by a play test after review: a running start from 4.5–5 m cleared the panels at the north end, two of them on the stretch this spec had left alone. The bank-top line now continues along the hilltop and turns along the hill's north foot into the wood opening. Keep testing from a run-up, not just at the wall.
- Accepted exception to the roof AC, decided by Valentin: the barracks ridge is at y 5.6 and the palisade behind it at about y 5.5, so a player standing on that roof could clear it. The roof can't be reached: its eave is 3 m over the ground, and the weapon rack beside it stops about 0.3 m short even with a jump. Keep props there low.

## Design Notes

**Moved line:** `outer-fence` keeps its first three and last points, with `(−46, −37)`, `(−47.5, −32)`, `(−50, −28)`, `(−53, −20)`, `(−56.5, −10)`, `(−56.5, 0.5)`, `(−55, 2.2)` between. Panels on the hilltop clear the ground outside by 4 m or more. No new flat ground; the gain is low bank for the graves and an empty shelf on the hill.

**Positions (x, z), ground y 0.15–0.6, sizes to confirm against the shell constants:**

| Feature | Centre | Size | Notes |
|---------|--------|------|-------|
| `garrison-barracks` | (−23, −41.7) | 15 × 5.5, yaw along the palisade | Door north. East 4 m is the stone armoury, nearest the ramp gate |
| `staff-house` | (−19.5, −34.5) | 12 × 5.5 | Door north onto the spine, ledger room at the west end, furthest from the road, pay hatch and queue rail on the road side |
| `short-measure` | (−17, −25.6) | 11 × 6.5 | Door south onto the spine, sign post, near the cooper |
| `grain-store` | (−26, −26.5) | 5 × 4 | On stilts, steps, closed |
| `drill-yard` | (−32, −33.3) | 12 × 7.5 | Pells, butts at the west end against the bank |
| `workers-graves` | (−37.5, −38.2) | 3 × 6.6, yaw −36.9 | Rows along the bank's toe (the first position sat on the moved line), stakes with Company numbers |
| `reserve-kennels` | (−40.5, −27.5) | 6 × 6 | Zone only |
| `reserve-barber` | (−32.7, −39.7) | 3.2 × 4 | Zone only, beside the barracks |
| `garrison-well`, `garrison-bell` | (−36.5, −30.8), (−12.4, −32.2) | points | Well at the yard's far side, bell at the spine mouth |
| `path-garrison` | (−8.7, −30.3) → (−26, −30.3) → (−34, −30.5) → (−36.3, −36.5) | 3 wide | Paint only |
| `path-drill-yard` | (−38, −33.3) → (−26, −33.3) | 7.5 wide | Paint only |

**Naming:** `Company house` already means the fort (`Camp/CompanyHouse`), so the new building is `staff-house` in tools and "Company staff house" in docs.

**Numbers:** a TextMeshPro label per stake, starting in the 200s with gaps, so the count reads as a ledger and not a list of people.

## Verification

**Commands:**
- `run_tests` EditMode, groups `Area1HandEditsTests`, `Area1Terrain.*` -- all pass
- `read_console` -- no compile errors

**Manual checks:**
- Top-down shot in the blockout framing, eye-level views from the fort road, ramp gate and bank top, nothing magenta. Play test: jump at the moved line from the hill, walk the spine.

## Suggested Review Order

**Moving the palisade**

- Start here: the fence leaves the bank's foot, follows the hilltop and turns into the wood opening.
  [`layout.json:152`](../../docs/World/Areas/area-001/layout.json#L152)

- Panel numbers count across lines, so saved rows move with the longer fence.
  [`hand-edits.json`](../../docs/World/Areas/area-001/hand-edits.json)

**Building the quarter**

- Entry point: Sites builds every part under the new root.
  [`Area1SitesBuilder.cs:425`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L425)

- Barracks open on the yard; the stone armoury end stays closed.
  [`Area1Garrison.cs:66`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L66)

- Ledger room, pay office and the hatch workers queue at.
  [`Area1Garrison.cs:90`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L90)

- Alehouse door on the spine, barrels on the cooper's side.
  [`Area1Garrison.cs:118`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L118)

- Closed room on capped stilts, ready for later theft quests.
  [`Area1Garrison.cs:151`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L151)

- Yard doubles as the square: pells east of the path, butts against the bank.
  [`Area1Garrison.cs:185`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L185)

- Mounds lie along the bank; each stands on its own ground.
  [`Area1Garrison.cs:220`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L220)

- TextMeshPro numbers, because the built-in font material ignores depth.
  [`Area1Garrison.cs:261`](../../Assets/_Game/Editor/World/Area1Garrison.cs#L261)

**Layout and validation**

- Minimum plot sizes, as doors and props are fixed.
  [`Area1SitesBuilder.cs:128`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L128)

- Checks stop a run before any root is replaced.
  [`Area1SitesBuilder.cs:296`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L296)

- Positions live in the layout, identical to the seed.
  [`layout.json:966`](../../docs/World/Areas/area-001/layout.json#L966)

**Supporting**

- Optional door and ramp arguments; existing callers build exactly as before.
  [`Area1CampInterior.cs:427`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L427)

- Who owns what, and why to test the fence with a run-up.
  [`CLAUDE.md`](../../Assets/_Game/Scenes/CLAUDE.md)
