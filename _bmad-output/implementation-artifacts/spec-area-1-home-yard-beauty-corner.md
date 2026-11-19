---
title: 'Area 1 beauty corner: the player''s hut and home yard'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: 'cf457f0b43da24a48e88b5e02b2cadc5365ccf49'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp is a finished graybox, but nothing in it shows the game's final look. The player's hut, where the arrival ends and where they sleep, is a 2.9 m box filled by an oversized placeholder bed: it can't be entered, and the yard has no light at night.

**Approach:** Bring one small spot to final quality as the visual reference for the rest of Area 1: the player's hut (enterable, with a themed bed), Hobb's hut and the shared fire yard. Use the Hivemind kit for shell pieces and props, a Blender-modelled bed, and a local lighting pass, matched against the concepts `Art/Artworks/Starting area/Camp/H1`–`H4`.

## Boundaries & Constraints

**Always:** Follow `ART_DIRECTION.md` (worn, muted, functional; fire is the only warm accent). Targets: H1 (yard by day), H2 (interior), H3 (night), H4 (bed). The player's hut keeps a front doorway and a rear opening toward the hunter fire; doorways at least 0.9 m wide and 2.1 m high; steps under 0.3 m; a free walking strip of at least 1.2 m inside beside the bed. The bed stays a `Bed_Base` variant and sleeping keeps working. Arrival event-001 still ends at the hut with Hobb's briefing. Rebake the NavMesh after shell changes. All new objects stay hand-owned inside `TentCamp/PlayersZone`.

**Ask First:** Growing the hut needs a neighbour hut, tarp bay or Wretches' Corner piece moved. Any change to `Core.unity`, `DayNightConfig`, global fog or post-processing. A kit piece that can't hold the look and would need a second custom model.

**Never:** The other 37 huts of the zone, other zones, terrain, roads. New NPCs, dialogue, quests or interactions. Sleeping-NPC animations. Image or Meshy generation (the user runs those). Deleting `Bed_WoodBed_01a` or the blockout builders.

</frozen-after-approval>

## Code Map

- `Scenes/StartingTown.unity` -- `Area1_Blockout/TentCamp/PlayersZone`: `worker-player` (−0.52, 47.97, yaw 154, 2.88 m square, walls 2.35 m, openings on both gable ends), `worker-mate` (−2.18, 44.56, yaw 64), `Campfire` (8.5, 36.5; stones + `Embers`, no light), `RepairTrunk`, `CardGame`. Root `Bed` (−0.5, 47.89). Palisade 4.4 m north; a Wretches' Corner piece 2 m west
- `Prefabs/World/Beds/` -- `Bed_Base`, `Bed_WoodBed_01a` (2.4 × 1.8 m, the placeholder)
- `Scripts/World/ClockNightLight.cs` -- fades a `Light` + glow quads by night level; used by `HuntersFire`
- `Scenes/StartingTown.unity` → `ArrivalSequence` -- `HutLanes`, `HutWatchSpot`, `TentMateSpot`, `DevPlayerSpot` (0.41, 46.09); Hobb's routine spots under `NpcRoutineSpots`
- `Assets/Hivemind/**/Prefabs` (shader `S_MasterORM`, URP target) -- shell: `SM_planksA_a1`, `SM_planksCorner_a1`, `SM_AssembledPlanks*`, `SM_Log_*`, `SM_WoodBeam_01a`, `SM_Damaged_Plank_*`, `SM_RockStone_01a/b`; cloth: `SK_Roof_Tarp_AsStaticMesh`; yard: `PF_Firecamp01a`, `SM_Cauldron` + `SM_Pot_Stand`, `SM_Log_Wide_*`, `SM_WoodStool_01a`, `SM_WoodChest_01a`, `SM_Outskirts_CuttedWoodTrunk`, `SM_Axe_01`, `SM_firewoodSetA_a1`, `SM_burlapSack*`, `SM_Crate_01`; interior: `SM_Candle_01*`, `SM_Bowl_01`, `SM_Hay_02a`, `SM_Rope_Bundle_01`, `PS_CandleFlame`

## Tasks & Acceptance

**Execution:**
- [x] `Editor/World/WorkerHutKitBuilder.cs` → `Prefabs/World/Huts/WorkerHut_Home.prefab` (3 × 3 m) and `WorkerHut_Long.prefab` (3 × 4 m), + `CLAUDE.md` in that folder -- hut shells generated from the kit's wall modules (`SM_wall3mB/4mB`, `SM_wallDoor2m`, `SM_wallWindow3m`), board-panel roof at 28° weighted with stones, gable boards, plank shutter on the rear opening; own box colliders, kit colliders off, roof Not Walkable -- reusable shells for the kit rollout
- [x] Blender → `Art/Environment/Camp/models/worker bed/WorkerBed.fbx` (+ `WorkerBed_Model.prefab`, `Source~/WorkerBed.blend`) -- the H4 bed without the headboard rail: 2.0 × 0.9 m, rough-hewn frame, rope lashings and webbing, mismatched leg block, straw-sack mattress with a torn seam, wool blanket pushed aside with two patches, rolled sack as pillow. Flat-lit sheet in `Art/Environment/Camp/Concepts/worker bed.png` -- in-game bed now, Meshy input later
- [x] `Art/Environment/Camp/Materials/M_Camp_*.mat` -- five URP Lit materials on tiling kit textures (rough wood, rope, sack cloth, wool blanket, straw) for custom pieces
- [x] `Prefabs/World/Beds/Bed_WorkerPallet.prefab` -- `Bed_Base` variant with the new model, trigger sized to it
- [x] Blender → `Art/Environment/Camp/models/door flap/DoorFlap.fbx` -- linen door flap tied back to the side of the doorway (the kit has no hanging cloth)
- [x] `StartingTown.unity` -- `worker-player` and `worker-mate` are `WorkerHut_Home` instances (names kept; the player's hut 0.5 m forward of the graybox one); `Bed` swapped to `Bed_WorkerPallet` along the east wall, head to the back
- [x] `Editor/World/HomeYardDresser.cs` → `PlayersZone/HomeYardDressing` -- interiors (crate table, candle, bowl, sacks, floor straw, rope on a peg rail; Hobb's adds bed, stool, kettle, mug, bucket, kindling), door flaps, fire ring with its tripod and pot (`SM_FireCamp_01b`) and a stool, wood pile and chopping block with axe, trunk with hatchet and whetstone, card crate with three log seats, boards and a bucket at the player's door. Graybox `Campfire`, `RepairTrunk`, `CardGame` switched off
- [x] `HomeYardDresser` lights -- `FireLight` (soft shadows) with a night-only flame quad, `CandleLight` in the player's hut; both `ClockNightLight`; tuned in Play at 23:00
- [x] `StartingTown.unity` -- NavMesh rebaked; `DevPlayerSpot` 1.4 m outside the new door, `TentMateSpot` beside it, `Spot_TentMate_Night` on the south side of Hobb's door
- [x] Docs -- `Scenes/CLAUDE.md`, `Prefabs/CLAUDE.md`, `Prefabs/World/Huts/CLAUDE.md`, `Art/Environment/CLAUDE.md` (*Camp Kit*), root `CLAUDE.md` index, `deferred-work.md`

**Acceptance Criteria:**
- Given a new game, when the arrival escort ends, then Hobb's briefing opens at the hut and the player can walk in, around the bed and out through the same doorway.
- Given the player inside the hut, when they interact with the bed, then the Sleep panel opens and waking works as before.
- Given day and night captures in Play from the H1, H2 and H3 viewpoints, then the two huts, the bed and the yard props match their concept in materials, prop set and light balance, and none of them shows a graybox material. The neighbouring huts and the tarp bay stay graybox (out of scope) and are visible in wide views.
- Given night, then the fire lights the nearest walls and seats, the candle lights the bed, and the rest of the yard stays dark but readable.

## Spec Change Log

- 2026-11-19, review (acceptance audit, spec drift, no loopback): the third acceptance criterion asked for "no graybox material visible in frame", which the frozen **Never** list makes impossible in any wide view (the other 37 huts of the zone are out of scope). Reworded to cover the dressed objects only and to name what stays graybox. The task line said "tripod pot" where the build uses the kit fire ring with its own tripod and pot. Known-bad state avoided: marking the spec done against a criterion it cannot meet. KEEP: the two generated hut prefabs, the Blender bed as a `Bed_Base` variant, the dressing group rebuilt by one tool, lights judged in Play only, yard props on the graybox footprint clear of the escort lanes.
- 2026-11-19, review patches (no loopback): both tools refuse in Play mode and in Prefab Mode; a hut build with a missing kit piece no longer overwrites the prefab; the dressing tool checks its inputs before deleting anything, asks before a rebuild, refuses huts that are not `WorkerHut_Home`, sets each outdoor prop on the ground where it stands, keeps walk-through props out of the NavMesh bake, and removes kit lights safely. The crate table moved to the corner so the strip beside the bed is clear to the back wall. The duplicate fire particle was dropped. Stale coordinates in `Scenes/CLAUDE.md` corrected.

## Design Notes

No kit building fits: the smallest (`PF_PrimitiveHouse02`) is 8 × 9 m. The shells are assembled from the kit's 2.5 m wall modules, which also makes them the template for the other 73 huts later. The kit beds are too large and too well-made for a laborer, so the bed is modelled in Blender; the user regenerates it with Meshy from the rendered sheet afterwards.

`RenderSettings` come from Core at runtime (trilight ambient from `DayNightConfig`), so the lighting pass is local lights only and can only be judged in Play: edit mode shows skybox ambient and near-black interiors.

Found while implementing:

- **The player's hut stays 3 × 3 m.** The planned 3.2 × 4.2 m room does not fit: the ground rises about 1 m within 2 m of the back wall, Hobb's hut and a Wretches' Corner hut touch its west side, and terrain is out of scope. The 4 m shell exists as `WorkerHut_Long`. The narrow bed (0.9 m, was 1.8 m) leaves a 1.8 m walking strip, which meets the frozen 1.2 m rule.
- **Tools instead of hand placement.** Shell and dressing are built by two small editor tools so the result can be rebuilt and reviewed; `HomeYardDressing` is theirs, the rest of `TentCamp` stays hand-owned.
- **Yard props follow the graybox footprint.** The arrival escort's `HutLanes` pass 1.6 m either side of the fire, so extra seats there blocked a lane; they were removed and the wood pile moved east of the lanes.
- **The rear opening is a window.** The kit's window wall replaces the graybox's open gable end. It faces the rising ground, as the graybox opening did, not the hunter fire; logged in `deferred-work.md`.
- **Not done:** the tarp bay next to the huts keeps its graybox canvas (no fitting kit piece checked yet); logged in `deferred-work.md`.
- **Two points for the human (review):** the door flap is a second custom Blender model, which the frozen **Ask First** list gates, and `HomeYardDressing` is rebuilt by a tool where the frozen **Always** list says new objects stay hand-owned. Both were done without asking and are raised at the end of the session.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after the prefab work and in Play -- expected: no errors

**Manual checks:**
- Play from a new game through the arrival to the hut; Dev: Skip Arrival lands outside the door; sleep until morning.
- Walk Hobb's day and night routine: he reaches his door spots.
- Compare captures with H1–H4 at noon, dusk and night.

## Suggested Review Order

**Look first (in the editor, in Play)**

- The scene paragraph says what moved, where the doors are and what the tool owns.
  [`Scenes/CLAUDE.md:135`](../../Assets/_Game/Scenes/CLAUDE.md#L135)

- Camp kit rules found here: wall modules, roofs, scale, custom pieces, density.
  [`Environment/CLAUDE.md:227`](../../Assets/_Game/Art/Environment/CLAUDE.md#L227)

**Hut shells**

- Entry point: two sizes from one builder, refused in Play or Prefab Mode.
  [`WorkerHutKitBuilder.cs:62`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L62)

- Walls from kit modules; the door module is stretched to clear the player's head.
  [`WorkerHutKitBuilder.cs:107`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L107)

- Board-panel roof with stones, kept out of the NavMesh.
  [`WorkerHutKitBuilder.cs:156`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L156)

- Own box colliders per wall span; the doorway and window are the gaps.
  [`WorkerHutKitBuilder.cs:205`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L205)

- A missing kit piece stops the save instead of replacing a good prefab.
  [`WorkerHutKitBuilder.cs:84`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L84)

**Dressing and lights**

- Entry point: every input checked before the old group is deleted.
  [`HomeYardDresser.cs:57`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L57)

- The player's interior: crate table in the corner, strip beside the bed clear.
  [`HomeYardDresser.cs:144`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L144)

- The fire: kit ring, night-only flame quad, props clear of the escort lanes.
  [`HomeYardDresser.cs:193`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L193)

- Light values, tuned in Play at 23:00 against H3.
  [`HomeYardDresser.cs:35`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L35)

- Clock-driven point lights, wired like the hunter's fire.
  [`HomeYardDresser.cs:266`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L266)

- Kit lights carry a missing HDRP script; they are removed, not disabled.
  [`HomeYardDresser.cs:338`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L338)

- Outdoor props sit on the ground where they stand, not at the group origin.
  [`HomeYardDresser.cs:132`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L132)

**Contracts and backlog**

- Shell contract: door axis, off-centre doorway, generated prefabs.
  [`Huts/CLAUDE.md:20`](../../Assets/_Game/Prefabs/World/Huts/CLAUDE.md#L20)

- The new bed variant and which way it faces.
  [`Prefabs/CLAUDE.md:93`](../../Assets/_Game/Prefabs/CLAUDE.md#L93)

- What is left: kit rollout, Meshy pass, terrain behind the hut, older lane issues.
  [`deferred-work.md:1061`](deferred-work.md#L1061)
