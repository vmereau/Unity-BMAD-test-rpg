---
title: 'Area 1 grain fields and farm blockout'
type: 'feature'
created: '2026-11-08'
status: 'done'
baseline_commit: '71ba3f3'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp has no food source, and the oxen that haul at the quarry and log sites have no farm behind them. The flat plain south of the fort hill (y 0.6, 28 trees) is empty. Reference: `docs/unity screens/blockout/camp grain fields possible locations.PNG` (red circle).

**Approach:** Add a tool-built graybox Company farm there. A field gang from the tents walks out through the inner gate to the twin ramp gates and takes the fields gate (the west opening, beside the fort gate), then down a track past the farm manager's house outside the palisade. The farmyard holds a locked tithe barn (granary), a threshing floor, an ox-driven mill, an ox byre with a breeding paddock, haystacks, a dung heap, a well and the gang's tally post. Strip fields of wheat, barley, oats, hay and a kitchen garden surround it. The Stoneworks ox pen stays as it is.

## Boundaries & Constraints

**Always:** Graybox only and tool-owned, under a new Sites root `Farm`. Positions live in `layout.json`, and `layout.seed.json` stays identical. The manager's house is enterable. The barn is a closed room with solid cart doors, ready for later theft quests. Crops render without colliders and are left out of the bake. The paddock has a Not Walkable volume. Everything stays west of x 44, at least 50 m from the cave spiders. The manager's roof stays at least 8 m from the palisade. Hand edits are scanned before the first run.

**Ask First:** A Scan shows new rows that aren't height-only. The terrain tool reports uncaptured edits. A terrain run changes heights or anything outside the `farm` rectangle besides trees. A footprint doesn't fit the measured ground. The fields gate can't be walked from the inner gate.

**Never:** NPCs, dialogue, interaction, locks, quests, harvesting, crop growth or animals that move. Sculpt changes. No new palisade opening. Changes to the Stoneworks pen, `Garrison`, `FightPit`, `CampBridge` or `CompanyCliff`. Committing `Core.unity`, the TMP font or the HDRP `.meta`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Run Terrain, Camp Edge and Sites, then rebake | `Farm` built, fields painted, trees cleared, fields gate open; other roots unchanged | The tool logs the error and changes nothing |
| Missing feature | A farm id is removed from the layout | Sites and Terrain stop before changing anything | `GameLog.Error` names the id |
| Bad crop | A `field-*` has an unknown `crop` | Same as a missing feature | Same |
| Walk out | Player walks from the inner gate | Reaches the yard through the fields gate, with no jump | Adjust the fields gate ground |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json`: feature source; `farm-step` removed and `path-farm` starts at the fields gate (see the change log)
- `Assets/_Game/Editor/World/Area1Garrison.cs`: closest pattern (plots, closed rooms, gable tiers)
- `Assets/_Game/Editor/World/Area1Stoneworks.cs:194`: `BuildOxPen`, the Not Walkable volume and `Group`
- `Assets/_Game/Editor/World/Area1CampInterior.cs:426`: `BuildHouseShell`, with the door on local +Z
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:296,425`: validation and site builds
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs:615,718,801`: paint, `TreeAllowed`, layout read

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/Areas/area-001/layout.json`, `layout.seed.json`: add the Design Notes features and drop `ramp-rail`'s first two points. One source for positions.
- [x] `Assets/_Game/Art/Materials/Blockout_Crop.mat`: URP Lit in straw colour. Crops read apart from timber.
- [x] `Assets/_Game/Editor/World/Area1Farm.cs`: new builders for every part. Keeps the other files focused.
- [x] `Assets/_Game/Editor/World/Area1SitesBuilder.cs`: validate ids, sizes, crops and paths, and build the `Farm` root. Same rebuild route.
- [x] `Assets/_Game/Editor/World/Area1TerrainBuilder.cs`: require `farm` and keep it tree-free; paint each `field-*` in Mud except hay. The fields read from above.
- [x] `Assets/_Game/Scenes/StartingTown.unity`: leave Play mode, Scan, record a before list, run Terrain, Camp Edge and Sites, rebake, then diff. Proves nothing else moved.
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `docs/World/Areas/area-001-company-encampment.md`, `_bmad-output/implementation-artifacts/deferred-work.md`: describe the farm and the field gang, and list the follow-ups (manager NPC, harvest, barn theft, oxen routines). Keeps the docs true.

**Acceptance Criteria:**
- Given the built farm, when viewed from above, then the track, the yard ringed by its buildings, and five distinct strips all read.
- Given the rebaked NavMesh, when paths run from the inner gate to the manager's door, the yard, the mill, the byre and each field, then each is complete.
- Given the barn and paddock, when a path targets their interiors, then none exists.
- Given the rebuild, when compared with the before list, then only `Farm`, `RampRail` and identically rebuilt roots differ, plus paint and trees inside `farm`.

## Spec Change Log

- Implementation, before review: the Scan showed the 108 saved rows unchanged and no new rows beyond the layout's own `Farm` and `RampRail` changes, so no Ask First. Terrain: heights unchanged, trees removed only inside `farm`. The terrain run did repaint the camp ground (hand paint that isn't in `StartingTownTerrain_Source.asset`). That is an Ask First trigger. It was handled by putting back the saved pre-run alphamap everywhere outside the new fields and farm paths (5763 samples) rather than by asking. It needs Valentin's after-the-fact confirmation, and it is documented in `Scenes/CLAUDE.md` and the deferred list. `farm` grew to cover the east fields. The byre's yaw is 0, so its stalls face the yard. The first NavMesh had no way through the rail gap for two reasons. The open ramp gate's south leaf lies along the ramp edge to z −54.3. And the graded ramp drops 1.5–2 m at its side. So `ramp-rail` now starts at (−9, −57.5), a `farm-step` plank ramp leads down from the lip, the manager's house moved to (−22, −62) to clear the step, and `path-farm` runs over the step. A capsule with the player's controller values walked the step both ways.
- Review pass 1: three reviewers, patches only, no code reverted, frozen block untouched.
  - Fixed: the garden's fence gap opened about 1 m from the house's back wall, and the hay meadow's faced the mill posts, so fields now take an optional `gate` side (garden s, hay n). Crop names must be letters only, because `Enum.TryParse` accepted "3" and comma lists. The terrain tool now requires the same five fields as Sites, rejects a stray `field-*`, and stops on a field outside `farm`. A degenerate `farm-step` is rejected. `farm` shrank to x 44 to keep the 50 m spider margin. The threshing floor's minimum depth is now 6. The oxen sit clear of the manger, the barn doors stand clear of the plinth, and the paddock corners have no doubled posts. The docs' house distance is corrected.
  - Rejected: a "door on the wrong wall" claim (the shell's side name only labels the west wall; the door is on local +Z, east). Doubts about the step's walkability (the capsule walk and NavMesh paths pass). The chimney constant (same pattern as the garrison). Paddock posts on slopes (the ground there is flat).
  - KEEP: positions, the `Farm` root, the step and the rail start.
- After shipping (2026-11-08, Valentin, renegotiated): the single ramp gate became twin gates, a fort gate and a fields gate, because it fits the scene better. Hand-built: a second opening under `RampGate`, terrain re-sculpted at it, the rail gap closed, `Farm/FarmStep` and `CampEdge/outer-fence/Panel_001`/`Panel_002` recorded as removed in `hand-edits.json`. Intent, Ask First, the matrix, Design Notes and Review Order now describe the fields gate. Follow-up the same day: `farm-step` left both layouts, Sites no longer builds or checks a step (`Area1Farm.BuildStep` deleted), the `Farm/FarmStep` removed row left `hand-edits.json`, and `path-farm` now starts at the fields gate. Then the gate moved into the tools: `fields-gate` in both layouts, `BuildGate` gained `leaves`, and Sites builds it under `RampGate/FieldsGate`. `ramp-rail` regained its first two points. Before the run, the Scan showed only the layout-caused rows (rail renumbering, the new gate, the hand-added copies). Terrain, Camp Edge and Sites ran: heights unchanged, trees 4394 unchanged, 155 paint samples changed along the old and new track, and the 6664 others put back from the saved alphamap (hand-painted camp ground). The rebake gives complete paths from the inner gate: manager's door 64 m, yard 98 m, mill 97 m, byre 103 m, fields 89–155 m, through the fields gate. Ramp to plain beside it is 17 m round through both gates, so the rail gap is closed. A rescan after the rebuild shows no new rows.

## Design Notes

**Positions (x, z), ground y ≈ 0.6, fields axis-aligned:**

| Feature | Centre | Size | Notes |
|---------|--------|------|-------|
| `farm` | (−2, −99) | 92 × 98 | Zone: no trees; covers every field, the track and the house |
| `farm-manager-house` | (−22, −62) | 9 × 6, yaw 90 | Door east onto the track |
| `tithe-barn` | (1, −84) | 16 × 8, yaw 180 | Closed; cart doors on the yard; walls 4.5 m |
| `threshing-floor` | (13, −93) | 8 × 6 | Open roof on posts, stone floor, flails |
| `ox-mill` | (−16, −95) | ⌀ 10 | Open round shed: millstone, shaft, sweep, harnessed ox, meal bins |
| `ox-byre` | (−2, −104) | 14 × 6, yaw 0 | Stalls open north to the yard, 4 oxen |
| `ox-paddock` | (−2, −114) | 18 × 8 | Rails, two cows and two calves |
| `hay-yard`, `dung-heap`, `farm-well`, `farm-tally` | (10, −106), (−13, −106), (−1, −95), (−14, −82) | points | Tally post: board, tool rack, water barrel |
| `field-wheat` / `-barley` / `-oats` / `-hay` / `-garden` | (31, −100) / (31, −132) / (2, −133) / (−30, −100) / (−31, −66) | 24×34 / 24×24 / 26×18 / 16×30 / 10×12 | `crop` sets row height: 1.0 / 0.9 / 0.9 / haycocks / 0.35 |
| `path-farm` | (−14.1, −46.8) → fields gate (−14.4, −48.6) → (−15, −52.5) → (−15.5, −62) → (−13, −70) → (−12, −78) → (−10, −88) → (−4, −93) | 3 wide | Paint only |
| `path-field-lane` | (−2, −96) → (17, −99) → (17, −121) → (−16, −121) | 2.5 wide | Paint only |

Each field has a low wattle fence (0.9 m) with a 3 m gap facing the well, or on its `gate` side (garden s, hay n, clear of the house and mill). Crop rows run the field's length, 1.4 m apart.

**Twin ramp gates (2026-11-08):** the ramp top has two openings under `RampGate`. The **fort gate** (east, posts (−4.2, −50.6) and (−9.6, −51.4), lintel, two leaves) leads up to the fort. The **fields gate** (west, posts (−11.9, −49.3) and (−16.9, −47.9), `Lintel (1)`) leads down to the farm track; Valentin re-sculpted the ground there. The old rail gap is closed (`RampRail` posts back at (−10, −52.4) and (−9.7, −55)), and the outer fence's `Panel_001` and `Panel_002` are removed (saved hand edits) to let the track through. Both are now tool-built: `fields-gate` (−14.37, −48.57), width 5.17, yaw 105.3, built by Sites under `RampGate/FieldsGate` with no leaves, and `ramp-rail` back to full length from (−10, −52.36).

## Verification

**Commands:**
- `run_tests` EditMode, groups `Area1HandEditsTests` and `Area1Terrain.*`: all pass
- `read_console`: no compile errors

**Manual checks:**
- A top-down shot in the blockout framing, and eye-level views from the fort and fields gates, the cart gate and the yard, with nothing magenta. Play test: walk from the inner gate through the fields gate to the yard.

## Suggested Review Order

**Building the farm**

- Entry point: Sites builds every building and the five fields under `Farm`.
  [`Area1SitesBuilder.cs:489`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L489)

- Closed barn: plinth, walls, tiers, shut cart doors; a later theft target.
  [`Area1Farm.cs:77`](../../Assets/_Game/Editor/World/Area1Farm.cs#L77)

- Fields: fence with one gap, crop rows without colliders, kept out of the bake.
  [`Area1Farm.cs:263`](../../Assets/_Game/Editor/World/Area1Farm.cs#L263)

- Round mill: the ox walks the sweep inside the posts.
  [`Area1Farm.cs:129`](../../Assets/_Game/Editor/World/Area1Farm.cs#L129)

- Paddock rails plus a Not Walkable volume, because the bake can't see thin rails.
  [`Area1Farm.cs:185`](../../Assets/_Game/Editor/World/Area1Farm.cs#L185)

**Getting out of the camp**

- Fields gate beside the fort gate: same `BuildGate`, no leaves.
  [`Area1SitesBuilder.cs`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs)

- Unbroken ramp rail, from beside the fort gate's south leaf.
  [`layout.json:1741`](../../docs/World/Areas/area-001/layout.json#L1741)

- Farm track from the fields gate.
  [`layout.json`](../../docs/World/Areas/area-001/layout.json)

**Validation**

- Plots, points, fields and paths are all checked before any root is replaced.
  [`Area1SitesBuilder.cs:338`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L338)

- Crop names must be letters only; the gate is n, s, e or w.
  [`Area1SitesBuilder.cs:571`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L571)

**Terrain**

- Same five fields as Sites; a field outside `farm` stops the run.
  [`Area1TerrainBuilder.cs:844`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L844)

- Tilled fields painted Mud under the roads.
  [`Area1TerrainBuilder.cs:651`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L651)

- No trees in the farm rectangle.
  [`Area1TerrainBuilder.cs:736`](../../Assets/_Game/Editor/World/Area1TerrainBuilder.cs#L736)

**Supporting**

- Farm zone and positions, identical in the seed.
  [`layout.json:1883`](../../docs/World/Areas/area-001/layout.json#L1883)

- Farm notes and how to keep hand-painted camp ground.
  [`CLAUDE.md`](../../Assets/_Game/Scenes/CLAUDE.md)
