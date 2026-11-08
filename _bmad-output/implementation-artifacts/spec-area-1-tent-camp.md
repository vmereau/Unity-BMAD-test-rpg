---
title: 'Area 1 worker tent camp and camp centre blockout'
type: 'feature'
created: '2026-11-08'
status: 'done'
baseline_commit: 'c2b6119'
context:
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/docs/World/Areas/area-001-company-encampment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The worker half (inner fence to north palisade) is a few hand-placed tent clusters and a small stage against the inner gate. Zones have no readable borders, the centre can't be seen from the entrances, and the black market has no home. Reference: top-down capture of the worker half (2026-11-08, no marked screenshot yet).

**Approach:** A tool-built graybox camp with a west-to-east social gradient. Wretches' Corner is in the north-west by the latrine, with lean-tos. The Player's Zone is in the middle, with standard tents. The Mafia Zone is in the east, past a drainage ditch at x 24.5, with wall tents on platforms. The black market is a gang tent on the bank by the gang house. The player's tent moves deeper, to (10, 40), with the tent mate, repair trunk, campfire and card game. The camp centre moves to a cleared square at (1.5, 25), with a raised punishment post and Company flags, and three tent-free sight lanes run to the arrival gate, the wilderness opening and the inner gate. The arrival walk follows them.

## Boundaries & Constraints

**Always:** Graybox only, tool-owned (new Sites roots `TentCamp` and `CampCentre`), positions in `layout.json` with the same features in `layout.seed.json`. Delete the hand-placed `Camp/PlayersZone`, `WretchesCorner`, `MafiaZone` and `CenterStage`. Move `Bed`, `PlayerSpawnPoint`, TentMate's waypoints and night spot, and BeatenWorker to the new player's tent by hand. Drop `zone-1` and `zone-2`. Lakeside stays empty. Scan hand edits before the first run. Save the alphamap before the terrain run and put back every sample outside the old and new paint of the changed paths. Rebake the NavMesh.

**Ask First:** A Scan shows new rows that aren't height-only. The terrain tool reports uncaptured edits. Heights or trees change. A sight lane or the hunter's-fire view is blocked by something the tools don't own.

**Never:** New NPCs, dialogue, quests, interaction or locks. Moving the Warden or Cook. Gang house or stash changes. Tents on the lakeside. Sculpt changes. Editing existing fence points or panels. Touching the Fort, Garrison, Farm, `FightPit`, `CampBridge` or `CompanyCliff`. Committing `Core.unity`, the TMP font or the HDRP `.meta`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full rebuild | Terrain, Camp Edge, Sites, rebake | `TentCamp` and `CampCentre` built, paths repainted, other roots unchanged | The tool logs the error and changes nothing |
| Missing feature | A new id is removed | Sites stops before replacing any root | `GameLog.Error` names the id |
| Tent in a lane | A tent point sits within 3 m (plus its half size) of a sight lane, or inside the square, `lakeside` or the fight-pit bank | Same as a missing feature | Names the tent and what it hits |

</frozen-after-approval>

## Code Map

- `docs/World/Areas/area-001/layout.json`, `layout.seed.json`: feature source
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:213,296,425`: build entry, validation, site roots
- `Assets/_Game/Editor/World/Area1CampInterior.cs:216`: `BuildWorkGangTents` (tent prefab and fire ring pattern)
- `Assets/_Game/Editor/World/Area1Farm.cs`: closest recent pattern (props, Not Walkable, validation)
- `Assets/_Game/Editor/World/Area1TerrainBuilder.cs:941`: every `path-*` is painted in Mud
- `Assets/_Game/Prefabs/World/Tent_Graybox.prefab`: standard tent, 4.6 × 3.3 m

## Tasks & Acceptance

**Execution:**
- [x] `layout.json`, `layout.seed.json`: add the Design Notes features, reroute `path-arrival`, `path-commute` and `path-daily`, drop `zone-1`/`zone-2`.
- [x] `Assets/_Game/Art/Materials/Blockout_Canvas.mat`, `Blockout_CompanyFlag.mat`: URP Lit, off-white canvas and dark red.
- [x] `Assets/_Game/Editor/World/Area1TentCamp.cs`: builders for the lean-tos, standard tents, wall tents, fires, the player's group, the black market, the ditch and the camp centre. Tents face the nearest fire of their zone.
- [x] `Area1SitesBuilder.cs`: validate ids, tent points and lanes, and build both roots.
- [x] `StartingTown.unity`: leave Play mode, Scan, record a before list, save the alphamap, delete the old groups, run Terrain, Camp Edge and Sites, restore the paint, move the gameplay objects, rebake, diff.
- [x] `Scenes/CLAUDE.md`, `area-001-company-encampment.md`, `deferred-work.md`: zone map, Camp Center, Tent Camp Zones, black market location, and follow-ups (Warden to the square, gang stash, lakeside use, field gang beds).

**Acceptance Criteria:**
- Given the built camp, when viewed from above, then the three zones, the ditch, the square and the three lanes all read.
- Given eyes 1.7 m above the arrival gate (−38.4, 36.3), the wilderness opening (33, 51.7) and the inner gate (0, 9), when a ray is cast to the post top, then nothing blocks it.
- Given an eye at the player's tent door, when rays are cast to the hunter's fire, then it is visible through `BrokenStretch`, and the ruins still read from (15, 2.17, 28).
- Given the rebaked NavMesh, when paths run from the player's tent to the square, the arrival gate, the inner gate, the wilderness opening and the gang house ramp, then each is complete.
- Given the rebuild, when compared with the before list, then only the new roots, the deleted groups, the moved gameplay objects and paint on the changed paths differ.

## Spec Change Log

- Implementation, before review: the Scan showed the 110 saved rows and only the two layout-caused rows (the new roots), so no Ask First. The plan's positions failed the tool's own checks in places, so points moved (Design Notes now give the built values): the black market came off the bank (it stood in the wilderness lane and on a 1.5 m slope) to flat ground at the Mafia Zone's north end, the Wretches' tents moved either side of `path-daily` (one stood on it), the mafia fire dropped its low rail, and the tent mate's tent and two Player's Zone tents moved for spacing. Terrain: heights and the 4394 trees unchanged. Paint: 6733 samples changed; 6038 outside the old and new changed paths were put back from the saved alphamap. The Company Road then showed the tool's Cobble through the centre where the commute zone had kept post-run paint, but before the run it was hand-painted Road, so 251 more samples within 3.5 m of `company-road` were put back. Bed rotated 90° to the tent so it fits under the A-frame. Results: sight lines from all three lane ends to the post clear, hunter's fire clear from the tent, ruins tower visible from (15, 2.17, 28). NavMesh paths from the spawn point are complete: square 19 m, stage top 21 m, arrival gate 48 m, inner gate 34 m, wilderness opening 28 m, gang house ramp 39 m and inside 46 m, black market 26 m. The comparison with the before list shows only the new roots, the four deleted groups and the moved gameplay objects (`Walkways` and `Camp/Canteen` changed sibling index only). EditMode `Area1HandEditsTests` and `Area1Terrain*`: 101/101 pass.
- Play test, before review: with the player's `CharacterController`, the arrival walk (gate, square, stage top, tent) and tent to inner gate pass. Going east, a NavMesh corner cut across the low foot of the warehouse's camp ramp, which the controller can't step onto, and the tent at (21.5, 33) closed the gap round the ramp foot, so that tent left the layout. Round the foot, the player reaches the wilderness opening, the gang house ramp and the black market. The bake still links the ramp tip to the ground (existing ramp, logged in deferred work). The wall tents' gable tiers poked through the roof slopes; they now take the roof's width at their top. Sites and the bake reran after both changes.

- Review pass 1: three reviewers, patches only, frozen block untouched, no loopback.
  - Fixed: the player's tent faced its campfire, which left the back opening 49° off the hunter's fire. It now turns its back opening to `hunter-fire` (0° off, line clear), and the bed and spawn moved with it. `path-daily` cut the square's corner and the arrival path ran 0.7 m from the campfire stones, so all three paths now share a spur from the tent door east of the fire, and the daily path runs along the square's north side (one lean-to moved to (−5, 34.2) to make room). `TryRead` now rejects zero-length lanes and repeated ditch points (both gave NaN distances that skipped checks), reads the ditch width from the layout, and keeps footprints off the warehouse, the work-gang tents and the latrine. Steps: the stage has four 0.24 m risers facing the inner gate, and wall tents add steps until each rise is under 0.28 m, as the player's `stepOffset` is 0.3. The warehouse pole height is clamped. The trunk moved nearer the tent mate, clear of the player's tent sacks. The black market moved to (32.5, 37.5), because on the bank its platform stood 2 m over the door ground (7 steps; now 3). Its counter moved to the porch side, because across the front it blocked the steps. A play test then stopped at the wall tents' door: the gable started at 1.8 m, level with the top of the player's capsule (1.7 m tall, centre 0.9 m), so the walls are now 2.1 m and the awning rises with them. Docs: bounds and lane wording corrected.
  - Deferred: the Company Road runs past the player's tent (paint only).
  - Rejected: `zone-2` as a tree mask (no tool reads it; trees unchanged), rotated warehouse or gate (neither has a yaw), NaN layout values (shared helper), thin props in the bake (harmless), the straight inner-fence check (the fence is straight at z 9), required paint-only paths (same as the other sites).
  - Rerun: Terrain (heights and 4394 trees unchanged; 5901 samples restored outside the changed paths and 374 on the Company Road, 53 kept), Camp Edge, Sites, rebake.
  - KEEP: positions, the two roots, the lanes and the checks.

## Design Notes

**Positions (x, z), flat ground y ≈ 0.5, as built:**

| Feature | Kind | Value |
|---------|------|-------|
| `camp-centre` | zone | (1.5, 25), 14 × 12. Stage 4.5 × 4.5, 1.2 m high, steps south, post with a pillory board and a banner. Flag poles where the square's corners and side middles clear the lanes (5 today), plus two on the inner gate's camp side and one at the warehouse's south-west corner |
| `path-camp-square` | road | (−3, 25) → (6, 25), width 12 (paint) |
| `players-tent` | point | (10, 40). Its back opening faces `hunter-fire`. In code: tent mate's tent at (−5.6, 1), trunk between them (nearer the mate), campfire at (−1.5, −3.5), card crate and three logs 2.25 m west of the fire |
| `tents-players` | road (points) | (1, 36.5), (12.5, 45.5), (21.5, 27.4), (13, 15), (19.5, 14.5), (13, 22), (19.5, 22) |
| `fire-players` | point | (16.25, 18.25), two logs |
| `tents-wretches` | road (points) | (−5, 34.2), (−5.5, 39.5), (−4, 45), (−13.5, 34.5), (−14, 39.5), (−18.5, 36.5), (−19, 41), (−16, 45), (−22.5, 38.5); lean-tos 2.3 × 2.2, 1.4 m, either side of `path-daily` |
| `fire-wretches` | point | (−8.5, 36.5) |
| `tents-mafia` | road (points) | (28.2, 13.5), (35.8, 13.5), (28.2, 21.5), (35.8, 21.5), (30, 30.5); platform 4 × 5 over the highest ground, canvas room, ridge roof, awning |
| `fire-mafia` | point | (32, 17.5), four benches |
| `black-market` | point | (32.5, 37.5), yaw 200: a wall tent with a counter, shelves and crates, at the Mafia Zone's north end on the night path |
| `zone-ditch` | road | (24.5, 9.8) → (24.5, 41), 0.8 wide, dark strip without a collider, plank crossings at z ≈ 18 and 31 |

**Rerouted paths:** all three start on a shared spur at the player's tent door, (11, 37.3) → (10.5, 33.8), east of the campfire. `path-arrival` (−38.4, 36.3) → (−20, 31.1) → (−6, 28.5) → (1.5, 30) → (7, 30) → spur. `path-commute` spur → (9, 30) → (8.5, 21), then as before from (6.5, 14). `path-daily` spur → (−1, 31.8) → (−6.5, 31.8) along the square's north side → (−11, 25) → (−10.5, 41.5).

**Lanes:** from the post to the arrival gate, to `wilderness-trail`'s first point and to `inner-gate`, each 6 m wide. The player's tent at (10, 40) sees the fire through the break (the line passes x 3.9 at z 52.4, clear of the warehouse).

**Footprint radii used by the checks:** standard tent 2.3, lean-to 1.45, wall tent 3.2, fires 0.9–2, card game 1.2; tents keep 1 m apart. Footprints also stay off the warehouse, the work-gang tents and the latrine (3.2 m). Steps: the stage has four 0.24 m risers facing the inner gate, and wall tents get as many steps as keep each rise under 0.28 m, both under the player's 0.3 m step offset.

## Verification

**Commands:**
- `run_tests` EditMode, groups `Area1HandEditsTests` and `Area1Terrain.*`: all pass
- `read_console`: no compile errors

**Manual checks:**
- Top-down shot and eye-level views from the three lane ends and the player's tent, with nothing magenta. Play test: the arrival walk from the gate through the square to the tent.

## Suggested Review Order

**Reading and checking the layout**

- Entry point: every tent camp feature is read and checked before any root is replaced.
  [`Area1TentCamp.cs:121`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L121)

- Zero-length lanes and repeated ditch points stop the run instead of skipping checks.
  [`Area1TentCamp.cs:171`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L171)

- Each footprint stays out of the lanes, square, lakeside, pit bank, ditch, warehouse and latrine.
  [`Area1TentCamp.cs:190`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L190)

- Sites calls the check next to the other validations, then builds both roots.
  [`Area1SitesBuilder.cs:424`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L424)

**Camp centre**

- Stage with steps facing the inner gate, post, pillory and flags round the square.
  [`Area1TentCamp.cs:216`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L216)

- Flag poles use the square's corners and side middles that clear every lane.
  [`Area1TentCamp.cs:287`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L287)

**Tent zones**

- Three zones, tents facing their fires; the player's tent turns its back to the hunter's fire.
  [`Area1TentCamp.cs:243`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L243)

- Wall tent: platform over the highest ground, steps under the player's step offset, 2.1 m door.
  [`Area1TentCamp.cs:349`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L349)

- Black market counter along the porch side, clear of steps and door.
  [`Area1TentCamp.cs:277`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L277)

- Ditch: dark strip and plank crossings, no colliders, out of the bake.
  [`Area1TentCamp.cs:445`](../../Assets/_Game/Editor/World/Area1TentCamp.cs#L445)

**Layout**

- The square and its paint, then the zones' tent points and fires.
  [`layout.json:1398`](../../docs/World/Areas/area-001/layout.json#L1398)

- Rerouted arrival path; arrival, commute and daily share a spur from the tent door.
  [`layout.json:1234`](../../docs/World/Areas/area-001/layout.json#L1234)

**Supporting**

- Who owns what, the checks, and the hand-placed objects that don't follow the tent.
  [`CLAUDE.md:72`](../../Assets/_Game/Scenes/CLAUDE.md#L72)

- Camp Center and Tent Camp Zones as built.
  [`area-001-company-encampment.md:84`](../../docs/World/Areas/area-001-company-encampment.md#L84)
