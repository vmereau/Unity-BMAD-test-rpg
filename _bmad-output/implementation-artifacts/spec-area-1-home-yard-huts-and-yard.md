---
title: 'Area 1 home yard: concept pack, then owned huts for Tam, Pim, Gerrit and Bren and a rearranged yard'
type: 'feature'
created: '2026-11-26'
status: 'in-progress'
baseline_commit: '239e1a0a'
context:
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Huts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CANTEEN_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Four workers of the home yard have no home: Pim's and Gerrit's pallets stand in the open in Wretches' Corner (−9, 40.5 / 43.5), Tam stands at the fire day and night, Bren stands at the Wretches' fire all night. The yard's props (repair trunk, card game, chopping block) sit in the escort's lane corridor and read as dropped at random, and three kit crate tables cost about 940k of the dressing's 1.24M triangles.

**Approach:** Two phases. First a concept pack: captures of the site with temporary massing shapes, image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then the build, from existing kit assets and Blender (MCP) models: four furnished, owned huts and a yard rearranged into purposeful corners.

## Boundaries & Constraints

**Always:**
- Plan (agreed 2026-11-26), existing shells, no hut moved: **Tam** → `PlayersZone/worker-hut-03` (door (4.1, 36.3), facing the fire); **Pim + Gerrit** share `worker-hut` (Home shell, door (6.2, 45.1)), two pallets; **Bren** → `worker-hut-02 (5)` (door (−6.4, 32.9), facing the Wretches' fire).
- Each owned hut: a `Bed_WorkerPallet` per owner (`Ownership`, `NpcApproach` on the NavMesh, `NpcSleepAnchor`), one owned unlocked `Base_Container` with a `PersistentID` and modest loot (a few coins, food or a personal item; no new facts), the shell's `RestrictedArea` switched on through `SerializedObject` with its owner(s), all day, one warning (Hobb's settings). Pim's and Gerrit's existing beds move into the hut and keep their parent `TitheBarnShortfall`.
- Routines: Tam keeps the fire by day and sleeps 22–06; Bren keeps the Wretches' fire 21–23 and sleeps 23–05 (`NpcRoutine` on the scene instances).
- Interiors follow the Camp density rule (bed, light crate table with candle and bowl, two sacks, straw, one thing on the wall) plus two or three belongings per owner. Candles are point lights with `ClockNightLight`, no shadows.
- Yard (fire stays at (5.55, 38.95)): hearth, wood corner against a wall in the east pocket, work corner (the repair trunk) under `worker-hut`'s west eave, evening corner (card game + lantern) in the west pocket off `HutLane_L_4`, water corner by the doors pocket, boards on the wet ground and at each owned door.
- Nothing solid within 0.7 m of a `HutLanes` segment or in the 1.5 m doors pocket; the dresser's lane warnings for the trunk, card game and doorstep end.
- Kit crate tables (`SM_Crate_01`, `SM_Crate_02`) are replaced everywhere in `HomeYardDressing`, the player's and Hobb's huts included, by a crate under 3,000 triangles. New dressing ≤ 60,000 triangles, beds excluded.
- `M_Camp_*` on kit textures, `Camp/CLAUDE.md` rules; kit materials never edited; kit props size- and triangle-checked. Doorways 2.1 × 1 m, steps < 0.3 m.
- Ownership: the dresser rebuilds only `HomeYardDressing`; one rerunnable authoring command places beds, boxes, RA owners and routine blocks from the same hut-local table. Blender pieces: one script per piece under `Source~`.
- Two pauses: after the concept pack, and after the first build is captured by day and at 23:00 in Play. Rebake (`StartBakingSurfaces`, check for `... 1.asset`), save the scene.

**Ask First:** Any image or Meshy generation (cost and plan first). Moving a hut, the fire, a lane, a lane spot or a waypoint. Terrain changes. More than 60,000 new triangles. Restricting anything besides the four rooms.

**Never:** Locks, keys, new facts, talk openers, dialogue, quests, new NPCs. Other zones' huts or tarps (Wretches' graybox hovels stay). `generate_image` / OpenRouter. Deleting graybox groups. Editing `hand-edits.json` or a tool-owned root.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dresser / command, first run | Shells and assets present | Interiors, yard, beds, boxes, RAs, routines in place | N/A |
| Rerun | Already built | Same poses, no duplicates, no new assets; box contents and save IDs kept | N/A |
| Asset or hut missing | No prop, model, bed prefab or a hut is not a shell | Nothing changes | Error names what is missing |
| Player in an owned hut | Owner sees him | Trespass warning, then fight on repeat | N/A |
| Player takes from a box | Owner sees it | Theft rules (as `HobbsBox`) | N/A |
| Night | 22:00 / 23:00 | Tam, Pim, Gerrit, Bren reach and sleep in their beds | N/A |
| Escort | New game | Lane paths as before; Hobb's briefing opens | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/HomeYardDresser.cs` -- groups `Fire`, `RepairTrunk`, `CardGame`, `Doorstep`, `MudBoards`, `PlayerHut`, `MateHut`; `Prop` / `Model` / `NightLight`; lane-clearance warning
- `Assets/_Game/Editor/World/WorkerHutKitBuilder.cs` -- shell variants (`DoorMinX/MaxX`, mirrored `_B`/`_D`/`Home_C`), `RestrictedArea` child
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs`, `NpcRoutineBlock.cs` -- routine blocks (Stand / Sleep)
- `Prefabs/World/Beds/Bed_WorkerPallet.prefab`, `Prefabs/World/Containers/Base_Container.prefab` -- pattern: `PlayersZone/HobbsBed`, `PlayersZone/HobbsBox`
- Scene: `TitheBarnShortfall/FieldHandBed`, `BarnWorkerBed`; `BeatenWorker` (7.4, 40.4); `WorkSiteManagers/QuarryWorker`, `NpcRoutineSpots/Spot_QuarryWorker_Night` (−7.2, 37.8); `worker-hut/RestrictedArea` (on, null owner today); `ArrivalSequence/HutLanes`

## Tasks & Acceptance

**Execution:**
- [x] Captures `Art/Artworks/Starting area/Camp/Home yard/refs/Y1`–`Y7` (massing, nothing saved) -- concept inputs
- [x] `Art/Environment/Camp/HOME_YARD_CONCEPTS.md` + pointer in `Camp/CLAUDE.md` -- template, views, build brief, empty verdict log; **pause**
- [x] `Art/Environment/Camp/models/<piece>/Source~/build_<piece>.py` -- belongings the kit lacks (decided at the verdict)
- [x] `HomeYardDresser.cs` -- three hut interiors, rearranged yard groups, light crate, candles and lantern
- [x] `Editor/World/HomeYardOwners.cs` (`Tools/World/Settle Home Yard Owners`) -- beds, boxes, RA owners, routine blocks; validates before changing anything
- [x] `Assets/Tests/EditMode/HomeYardOwnersTests.cs` -- table: beds and boxes inside their rooms, clear of the walking strip and lanes; lights carry `ClockNightLight`
- [ ] Scene: run dresser + command, rebake, captures by day and at 23:00 in Play; **pause**; play test
- [ ] Docs: `As built`, `STARTING_TOWN.md`, `Editor/World/CLAUDE.md`, `Huts/CLAUDE.md`, four NPC `CLAUDE.md`, `Scripts/AI/NPC/CLAUDE.md` routine cast, `deferred-work.md`

**Acceptance Criteria:**
- Given the 23:00 captures, then each owned hut shows a lit candle and the fire and lantern light the yard; by day nothing in the yard shows a graybox material.
- Given `HomeYardDressing`, then it holds no `SM_Crate_01` / `SM_Crate_02` and the dresser logs no lane warning.
- Given the artworks, then each built view matches its kept artwork's layout, with every difference listed in *As built*.

## Spec Change Log

- 2026-11-26, phase 1 (concept pack, no loopback): seven captures `Y1`–`Y7` with massing (scene reloaded unsaved, `git status` clean) and `HOME_YARD_CONCEPTS.md`. Found while massing: the work corner cannot go under `worker-hut`'s west eave (that eave faces the tarp bay 0.5 m away), so the trunk stands against the hut's south-west front left of the doorway; the sawhorse had to come 0.4 m toward the wall to clear `HutLanes` (0.72 m); props inside Tam's hut measure 0.33–0.56 m from the lane behind his wall (the wall itself is that close since the rollout), so the lane check must skip props inside a hut room.

- 2026-11-26, artworks: Y1–Y7 generated through Meshy at the user's request (`nano-banana-pro`, 16:9, 9 credits each, 63 in all, no regeneration). Decisions recorded under the verdict log of `HOME_YARD_CONCEPTS.md`: free-standing wood and work corners (no tool rack), one lantern over the card game, Bren's tally as a notched board.
- 2026-11-27, phase 2 build (no loopback). Deviations from the frozen text: **Bren sleeps 23–06**, not 23–05 (a 05–06 gap left him with no block; `HomeYardHomes.CoversTheDay` now refuses gaps); **Pim's bed at z −0.34** and the doorway's clear entry pocket 0.53 m deep (a 2 m bed in a 2.56 m room leaves 0.6 m at its foot; the way in is the doorway, then the 0.86 m strip between the two beds); the work corner stands against `worker-hut`'s south-west front (logged at phase 1); Pim's tool is the existing wooden spade, outside by the door, instead of a hoe. Also: three `KilledFact` save keys for the boxes (`Data/NPCs/<npc>/Facts/KilledFact_*Box.asset`), needed by `PersistentID` like `HobbsBox`, not narrative facts; three extra Blender pieces (`Plank`, `Boards`, `PegRail`) replacing kit planks of 3,300–3,900 triangles each, which brought the new dressing from about 77k to about 54k triangles (budget 60k); the lane check skips props inside a hut room. The table is `Editor/World/HomeYardHomes.cs` and its tests `Tests/EditMode/HomeYardHomesTests.cs` (the task named `HomeYardOwnersTests`). Dressing total 1.24M → 311k triangles at LOD0. Unity crashed once during captures before the scene was saved; both commands were rerun from scratch (same result) and the scene saved before the bake. Rebaked (no `... 1.asset`); every bed's NPC approach is on the NavMesh, lane paths as before (L3→4 detour round hut-03 and R0→2 broken at R_1, both known). At 23:00 in Play all six night lights are on and Tam, Bren, Pim, Gerrit and Hobb sleep in their beds. EditMode 2,180/2,180.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after the runs and in Play -- expected: no errors, no kit-light warnings

**Manual checks:**
- Play: enter each owned hut in sight of its owner (warning); each NPC sleeps at his hour; escort to Hobb's briefing.
