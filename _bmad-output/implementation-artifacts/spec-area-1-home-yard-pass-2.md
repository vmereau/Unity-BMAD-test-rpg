---
title: 'Area 1 home yard pass 2: one hut wood, roof poles, tarp bay with floor beds, yard details'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '74ef0a97'
context:
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Huts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The two kit huts of the home yard mix five wood materials from three packs (pale painted door and window frames, warmer roof boards, other browns on the gables and ridge), so they don't read as one building; the roof stones look like round boulders. The tarp bay in front of Hobb's hut is still graybox, and the yard has one stool at the fire and boards only at the player's door.

**Approach:** In the hut builder, put every wood part on one camp wood look and replace the stones with lashed weight poles. Model a sagging tarp and a floor bed in Blender, and dress the tarp bay with two sleepable floor beds. Add fire seats, mud boards and small details (drying line, a leaning tool) through the home-yard dresser. Targets: H1 and, once the user saves them, `H5 tarp bay`, `H6 floor bed`, `H7 hut roof poles` in `Art/Artworks/Starting area/Camp/`.

## Boundaries & Constraints

**Always:** Follow `ART_DIRECTION.md` and the Camp Kit rules. Hut changes go in `WorkerHutKitBuilder` (both prefabs follow), never by editing the prefab; `RestrictedArea` keeps its name and path. New outdoor objects are built by `HomeYardDresser` inside `HomeYardDressing`. The graybox `worker-tarp-north (1)` is switched off, not deleted. Floor beds are a `Bed_Base` variant (`Bed_FloorPallet`), sleepable, no save ID, not NPC beds. Nothing solid within 0.7 m of a `HutLanes` segment or in the 1.5 m pocket between the two doors. Custom pieces use the `M_Camp_*` materials. Rebake the NavMesh.

**Ask First:** The tarp bay needs more than its graybox footprint (2 × 1.8 m) plus 0.5 m on a side, or any lane / NPC spot moved. The one-wood look needs new textures rather than kit textures and tints. Any Meshy or image generation (credits; the user approves first). Changes to `Core.unity`, `DayNightConfig`, fog or post-processing.

**Never:** Other huts, tarp bays or zones (the kit rollout); terrain; NPCs sleeping in the floor beds; new NPCs, dialogue, quests; deleting graybox groups or `Bed_WoodBed_01a`; `generate_image` / OpenRouter.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/WorkerHutKitBuilder.cs` -- shell build: walls (`BuildWalls`: side `SM_wall3mB`, door `SM_wallDoor2m`, window `SM_wallWindow3m`, filler `SM_barnWallB_1m`, gable `SM_Damaged_Plank_B_02/03`), roof (`BuildRoof`: `SM_AssembledPlanksA`, 10 `SM_RockStone_01a/b`, ridge `SM_Plank_Rope_300_A`), shutter `SM_AssembledPlanksC`
- Wood materials found on the shell: `M_Wood_Main_a1` (sides, filler), `M_WoodPaintedWhite_a1` (door, window: same atlas + white paint layer), `M_PlanksA_a3` (roof, shutter), `M_WoodPlanks_02` (gables), `M_WoodTrim` (ridge); `M_TrimsheetA_a1/a2`, `M_Decal_EdgeWear_01` are trims and stay
- `Assets/_Game/Editor/World/HomeYardDresser.cs` -- `HomeYardDressing` groups (`PlayerHut`, `MateHut`, `Fire` (5.55, 38.95), `RepairTrunk`, `CardGame`, `Doorstep`), `Prop` / `Model` helpers, graybox switch-off list
- `StartingTown.unity` → `PlayersZone/worker-tarp-north (1)` -- graybox bay: posts at x −2/0, z 41.75/43.55, two canvas slabs, two `OverflowBedroll` boxes; ground y ≈ 0.49–0.59
- `ArrivalSequence/HutLanes` -- `L_5` (2.97, 41.38) → `L_6` (0.11, 43.10) passes the bay's east edge; `L_4`/`R_4` 1.6 m either side of the fire
- `Prefabs/World/Beds/Bed_Base.prefab`, `Bed_WorkerPallet.prefab` -- bed variant pattern; `Art/Environment/Camp/models/worker bed/` -- Blender model pattern (`Source~/`, model prefab with one solid box)

## Tasks & Acceptance

**Execution:**
- [x] `Art/Environment/Camp/Materials/M_Camp_Hut*.mat` + `WorkerHutKitBuilder.cs` -- tinted copies of the board materials matched to the side walls, and a remap table applied to every shell renderer (painted-white → `M_Wood_Main_a1`, boards → the copies) -- one wood tone
- [x] `WorkerHutKitBuilder.cs` -- drop `RoofStone`; two thin weight poles per slope, parallel to the ridge, rope lashings at both ends; rebuild both prefabs -- poles only (user choice)
- [x] Blender → `Art/Environment/Camp/models/tarp shelter/` (`TarpShelter.fbx`, `Source~/`) -- patched sagging tarp, two tall front poles (open side ≥ 2.1 m), two back stakes, guy ropes; plus `DryingLine` cloths (two shirts on a rope)
- [x] Blender → `Art/Environment/Camp/models/floor bed/` (`FloorBed.fbx`, `FloorBed_Model.prefab`, `Source~/`) + `Prefabs/World/Beds/Bed_FloorPallet.prefab` -- straw spread, sackcloth mattress with a torn seam, patched blanket thrown back, rolled-sack pillow, tied bundle; about 1.9 × 0.8 × 0.15 m; render the flat-lit sheet to `Camp/Concepts/floor bed.png`
- [x] `HomeYardDresser.cs` -- `TarpBay` group (shelter, two `Bed_FloorPallet`, bowl and cup), switch off `worker-tarp-north (1)`; fire seats (plank bench and log seat north of the fire, replacing the stool); mud boards from both doors to the yard and the bay; drying line; a tool leaning on a hut wall; rerun
- [x] `StartingTown.unity` -- rerun both tools, rebake, captures, play test
- [x] Docs -- `Camp/CLAUDE.md` (one-wood rule, poles), `Huts/CLAUDE.md`, `Prefabs/CLAUDE.md` (beds), `STARTING_TOWN.md`, `Editor/World/CLAUDE.md`, `deferred-work.md` (resolve the tarp-bay item)

**Acceptance Criteria:**
- Given flat-lit captures of both huts, when the walls, door and window frames, roof, gables and ridge are compared, then none reads pale or white and their mean colours sit in one tone (luminance within 15% of the side walls).
- Given both huts, then no stone is on a roof, and each slope has two lashed poles that don't float above the boards.
- Given the player at the tarp bay, when they interact with a floor bed, then the Sleep panel opens and waking works; Hobb's door, the player's door and his night spot stay reachable.
- Given a new game, when the arrival escort runs, then both guards walk their lanes (lane-to-lane NavMesh paths within 10% of the straight length, as before) and Hobb's briefing opens at the hut.
- Given day and night captures from the H1 viewpoint, then nothing in the bay or yard shows a graybox material, and the fire light reaches the bench.

## Spec Change Log

- 2026-11-25, implementation (human-approved scope change, no loopback): the bay in front of Hobb's hut sits in a wedge 0.8–1.3 m deep between hut-02's north wall and Hobb's rotated hut (its graybox already overlapped the kit hut), and the escort's `HutLane_L_6` ends at its mouth: two floor beds don't fit. The user chose **both spots**: a one-bed lean-to there (tarp strung from Hobb's eave to hut-02's wall, no posts) and the two-bed bay at `worker-tarp-north (3)`, which the frozen **Never** list excluded. That bay moved 1.26 m west of its graybox (to (4.3, 47.11)) because graybox hut-07's north-west wall cut through its east half. Both graybox bays are switched off. Also changed while implementing: the tarps and ropes are generated meshes (`Editor/World/CampClothMesh`, saved under `Camp/Generated/`) instead of one Blender model, because each tarp is tied to different points (eaves, poles, stakes); the floor bed has no solid collider (a soft mattress the player and the escort walk over, kept out of the bake); the bundle from H6 is a separate prop (`ClothBundle.prefab`) at the user's request, not part of the bed; the roof poles, spade and drying shirt are Blender models (`roof pole/`, `wooden spade/`, `drying shirt/`). KEEP: one wood tone by tuned copies of the kit materials (measured: every part within 15% of the walls), poles with lashings, the lean-to without posts, beds as `Bed_Base` variants.
- 2026-11-25, review (blind hunter, edge-case hunter, acceptance auditor; patches only, no loopback). Fixed: rope tubes were wound inside out (back faces showed, lighting inverted) — winding flipped, radial normals set (no seam), tangents added on ropes and tarps (normal-mapped materials); the bay's poles and stakes were settled to the ground one by one while the tarp and rope tie points stayed at the origin's height — every bay point is now placed on its own ground (`OnGround`), including the drying line's pole end; the ridge rope sagged through the straight top edge of the tarp (sag 0.04 → 0.015, 1 cm higher); the front flap left the open side at 1.83 m — poles 2.25 → 2.4 m and flap 0.3 → 0.12 m give 2.16 m (2.1 m rule); thin poles and stakes had no NavMesh blocker — Not Walkable volumes added; regenerated mesh assets stayed in memory on a rerun — `SaveAssetIfDirty`, and `AssetDatabase.CreateFolder` instead of a mid-build refresh; patches lift along the local surface normal; wrinkles and fraying no longer move the tied corners; the shirt rotation can't flip; the lane check orders lanes by their index and samples every 5 cm; degenerate tarp / rope inputs warn; the missing-asset error names camp assets too; boards added in front of the bay. Logged here, not changed: with its guy and back stakes the bay spans about 2.9 × 3.2 m against the 3.0 × 2.8 m **Ask First** limit (the tarp itself is 2.6 × 2.3 m; nothing neighbouring was moved, so it was not raised before building — the user sees it in the summary); the log seat stands east of the east lane, not north of the fire (north of the fire only fits the bench between the lanes); Hobb's door has no board of its own (its doorway opens onto the player's doorstep boards, the shared pocket); the trim atlases are remapped to `M_Camp_HutTrim` (same textures, tuned) though the Code Map said trims stay. Rejected: lashings past the pole ends (the roof length includes the overhang), a null `Prop` (missing pieces return a placeholder), the rng shift from removing the stones (one-time), the painted-white modules' UVs (same atlas as the plain wood), graybox bays coming back if `TentCamp` is rebuilt (it is hand-owned).

## Design Notes

Each kit mesh keeps its own texture (their UV layouts differ), so the look is unified by tint and roughness, not by swapping in one texture: measure the side walls' mean albedo, then tint the board copies toward it. If the kit shader graph has no tint input, use URP Lit copies on the same maps (as `M_Camp_RoughWood` does).

The floor beds lie on the ground (no frame) and their trigger covers the mattress; with the bay's back low, the player reaches them from the open side.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after both tool runs and in Play -- expected: no errors, no `Missing_` pieces

**Manual checks:**
- Captures from the H1 viewpoint (day, 23:00) and close views of the roofs and the bay, against H1/H5/H6/H7.
- Play: arrival escort to the hut; sleep in a floor bed; walk round the bay and both doors.

## Suggested Review Order

**One wood tone on the hut shells**

- Entry point: every kit wood material swapped for a tuned camp copy; painted white goes plain.
  [`WorkerHutKitBuilder.cs:59`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L59)

- The swap runs on every renderer; a missing copy blocks the save like a missing piece.
  [`WorkerHutKitBuilder.cs:272`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L272)

**Roof poles instead of stones**

- Two bark poles per slope, scaled to the roof length, lashed over the gable boards.
  [`WorkerHutKitBuilder.cs:230`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L230)

- Lashing turned back by the slope's tilt so its rope end hangs plumb.
  [`WorkerHutKitBuilder.cs:249`](../../Assets/_Game/Editor/World/WorkerHutKitBuilder.cs#L249)

**Generated cloth**

- Two-sided sagging sheet from four tie points; corners stay exactly on them.
  [`CampClothMesh.cs:27`](../../Assets/_Game/Editor/World/CampClothMesh.cs#L27)

- Rope tube: winding flipped after review so the outside is the front face.
  [`CampClothMesh.cs:173`](../../Assets/_Game/Editor/World/CampClothMesh.cs#L173)

- Assets reused across rebuilds (stable GUIDs) and written to disk at once.
  [`CampClothMesh.cs:189`](../../Assets/_Game/Editor/World/CampClothMesh.cs#L189)

**Tarp bay and lean-to**

- Bay moved west of the graybox spot: hut-07's wall cut through it.
  [`HomeYardDresser.cs:42`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L42)

- Bay: poles, tarp, flap, ropes, two beds; every point placed on its own ground.
  [`HomeYardDresser.cs:297`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L297)

- Lean-to tied from Hobb's eave to hut-02's wall, no posts at the lane's end.
  [`HomeYardDresser.cs:353`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L353)

- Poles solid for the player and closed to agents by a Not Walkable volume.
  [`HomeYardDresser.cs:411`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L411)

**Yard details and checks**

- Bench between the lanes north of the fire, log seat east of the east lane.
  [`HomeYardDresser.cs:235`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L235)

- Drying line from the hut's eave corner to the bay's west pole.
  [`HomeYardDresser.cs:375`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L375)

- Lane clearance warning for every solid prop, chains ordered by lane index.
  [`HomeYardDresser.cs:453`](../../Assets/_Game/Editor/World/HomeYardDresser.cs#L453)

**Supporting**

- Floor bed: `Bed_Base` variant, trigger only, no solid collider.
  [`Bed_FloorPallet.prefab`](../../Assets/_Game/Prefabs/World/Beds/Bed_FloorPallet.prefab)

- Camp kit rules: one wood tone, poles, cloth, Blender export with Apply Transform.
  [`Camp/CLAUDE.md`](../../Assets/_Game/Art/Environment/Camp/CLAUDE.md)

- Scene layout of pass 2: bay, lean-to, ties to graybox neighbours.
  [`STARTING_TOWN.md`](../../Assets/_Game/Scenes/STARTING_TOWN.md)
