---
title: 'Area 1 warehouse: script-built Blender hall, ramps, landing and hoist in a generated prefab'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: '314108bc'
context:
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Huts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Company warehouse over the starting corner is still 31 graybox boxes (`Area1_Blockout/Warehouse`) beside the finished huts. Its look is settled (artworks W1–W6 and the three decisions in `WAREHOUSE_CONCEPTS.md`).

**Approach:** Model it in Blender from a script, as the huts were: plank hall on squared braced posts and padstones over a dry-stone rubble bank, shingle roof, open strap-hinged cart doors, both ramps and the landing as plank decks on trestles with outside handrails, the hoist, a round junction log at each gable end. A builder wraps the model in a generated prefab (colliders, walk surfaces, door lanterns) and the Sites tool places that prefab in place of the boxes. The rock clump is rearranged by hand round it.

## Boundaries & Constraints

**Always:**
- Build brief of `WAREHOUSE_CONCEPTS.md`: floor 12 × 8 m, top at y 4.65, centre (15, 52); walls 4 m; ridge east–west at 30°; cart doorways 2.4 × 2.8 m centred on x 17, open, at least 2.1 m clear; camp ramp to (17, 34.8) and river ramp to (4, 58), 3.5 m clear deck, same slopes, never stairs; landing 6 × 3.8 m; hoist at the north-east corner. Where an artwork differs, the game wins.
- **Ownership: the root stays in the Sites tool.** `Area1CampInterior.BuildWarehouse` places the prefab under the same root name and parent, so a Sites run stays safe. Everything inside the root is generated (script → FBX → builder → prefab); nothing is edited by hand in it.
- Posts, trestles and the rubble bank are fitted to a ground capture (terrain plus rocks) stored beside the script; the script is the source, the `.blend` only stores the result.
- One wood: the huts' texture set (`T_ModularWood`, one plank per board, no board over 0.2 m). Stone, shingle tone and iron come from kit textures in new `M_Camp_*` URP Lit materials; kit materials are never edited.
- Rubble is rough dry-stone, 1–2 m high under the perimeter, and on the palisade line (z 52) it reaches the floor: nothing passes under the hall from camp to river, visually or physically.
- `Ramp_Camp` stays an active child of the root with a `BoxCollider`, scaled (3.5, 0.2, 13.85), its faces on the deck's faces.
- The guard's five waypoints, his spawn point and `RA_Warehouse` are not moved; all five waypoints stay on the NavMesh. The gap west of the hall stays `BrokenStretch`. Trestles stay out of the bed pocket beside hut-05. The roof clears `Flag_Warehouse`'s pole and the east yard's ramp tarp.
- Lanterns: one by each cart door, a light with `ClockNightLight`, unlit by day.
- Rebake the NavMesh (`StartBakingSurfaces`, check for a `... 1.asset`).

**Ask First:** Any image or Meshy generation. No kit texture reads as rubble, shingle or iron. Moving a waypoint, `RA_Warehouse`, a hut, the flag pole or a Camp Edge panel. Terrain edits. Changing a brief dimension. More than 60,000 triangles for the whole building.

**Never:** The interior's dressing (the plank floor is built, nothing stands on it). The palisade itself, `DockGate`, its guard post, `Docks`. `generate_image` / OpenRouter. Hand-placed art inside the tool-owned root.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildWarehouse` (boxes, `Stilt`, `Slab`, `Skirt`, `Crate_0..2`), called from `Area1SitesBuilder.cs:514`; floor height = highest ground in the footprint (4.647 today)
- `Assets/_Game/Editor/World/WorkerHutKitBuilder.cs` + `Art/Environment/Camp/models/worker hut/Source~/build_worker_hut.py` -- the pattern: board boxes in Unity's frame, plank UVs, model taken apart into the prefab, a bad model blocks the save
- `Assets/_Game/Editor/World/HomeYardDresser.cs:59,599` -- `RAMP` path and `RampEdge` (unit-cube maths on `Ramp_Camp`)
- `docs/World/Areas/area-001/hand-edits.json` -- pins `Warehouse/Post_Camp_0..2` (paths that will no longer exist)
- Scene (measured): Camp Edge `outer-fence-3/Panel_060` ends at x 9.31 (top y 7.33), `Panel_061` starts at x 20.69 (top y 9.74), both 0.6 m thick on z ≈ 52.1–52.3, inside the old 0.4 m gable walls; ground under the hall 3.2–4.7 on z 52, down to 0.5 at the south-west corner (9, 48) and −0.2 under the landing's outer posts; rocks `SM_Rock_06 (20–22)`, `07 (2–4)`, `08 (4–6)`, `09 (3–5)` are scene roots with mesh colliders
- Kit: `Medieval_Docks/SM_Lantern_Hanging`, crates and sacks (`BanditVillage`, `Medieval_Docks`), stone textures (`T_RockTile`, `T_StoneWall`, the rocks' own maps), `T_Iron`
- `Assets/_Game/Scripts/World/ClockNightLight.cs` -- needs a `DayNightConfigSO` and a `Light`; set up as in `HomeYardDresser.cs:795`

## Tasks & Acceptance

**Execution:**
- [x] `StartingTown.unity` (scene roots `SM_Rock_*`) -- check the rock clump first, rearrange only what conflicts (nothing did) (posts and rubble are fitted to it): rocks carry the south-west corner and the camp ramp's east side as in W1 / W6, none pokes through the floor, a deck or the bed pocket -- the ground the model is fitted to
- [x] `Assets/_Game/Editor/World/WarehouseBuilder.cs` -- `Tools/World/Capture Warehouse Ground` writes a 0.25 m height grid (terrain + rocks, warehouse root ignored) to `models/warehouse/Source~/warehouse_ground.json`; `Tools/World/Build Warehouse Prefab` builds `Prefabs/World/Warehouse/Warehouse.prefab` from the FBX: model groups, colliders (walls with door gaps, roof, decks, handrails, posts, rubble, a full-height box on z 52), walk surfaces for floor, landing and both ramps (`Ramp_Camp` as specified), Not Walkable roof and handrail edges, rubble left out of the bake, two `DoorLantern`s with `ClockNightLight`; a missing model, group or foreign material stops before saving
- [x] `Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py` (+ `Warehouse.blend`, `Warehouse.fbx` with remap meta) -- objects `Frame`, `Walls`, `Floor`, `Roof`, `Doors`, `Ramps`, `Landing`, `Hoist`, `Rubble`, `Iron`: squared posts on padstones with diagonal braces and tarred feet, horizontal boards between posts, vertical gable boards with a shuttered hatch (west), shingle courses with ragged butts and pale mended patches, door leaves swung open against the wall on strap hinges, decks on stringers and trestles with cross battens, post-and-rail handrails outside the deck edge, braced hoist post with arm, pulley block and rope, lantern brackets, rubble of jittered stones, a round palisade-size log at the middle of each gable reaching the fence top and hiding the panel's end -- the building
- [x] `Assets/_Game/Art/Environment/Camp/Materials/` -- `M_Camp_Rubble`, `M_Camp_Shingle`, `M_Camp_ShinglePale`, `M_Camp_Iron` (+ a darker tarred wood if `M_Camp_HutShellDark` is not enough) -- kit textures, own materials
- [x] `Assets/_Game/Editor/World/Area1CampInterior.cs` -- `BuildWarehouse` instantiates the prefab at the computed floor (warns if it differs from the height the model was fitted to by over 5 cm), places crates and sacks under the hoist on the ground; a missing prefab logs an error and builds the old graybox boxes (kept as the fallback)
- [x] `docs/World/Areas/area-001/hand-edits.json` -- drop the `Warehouse/Post_Camp_*` entries
- [x] `Assets/Tests/EditMode/WarehousePrefabTests.cs` -- prefab contract: `Ramp_Camp` path, collider and scale; both doorways and both decks free of colliders over 2.4 × 2.1 m and 3.5 m; lanterns carry a light and `ClockNightLight`
- [x] `StartingTown.unity` -- place the prefab through the Sites code on the warehouse root (full Sites run and `Dress Home Yard` rerun left to the user, see the change log), rebake; captures from the W viewpoints by day and at 23:00; play test
- [x] Docs -- `Camp/CLAUDE.md`, `WAREHOUSE_CONCEPTS.md` (built state, verdicts), new `Prefabs/World/Warehouse/CLAUDE.md`, `Editor/World/CLAUDE.md`, `STARTING_TOWN.md`, root index; `deferred-work.md` (interior, palisade art pass)

**Acceptance Criteria:**
- Given the rebuilt scene, when the warehouse root is listed, then it holds one prefab instance plus ground-placed dressing, and no `Blockout_Timber` renderer.
- Given the rebake, when a NavMesh path is asked from the camp ramp's foot (17, 34.8) to the river ramp's foot (4, 58), then it exists, runs through both doorways and the landing, and each of the five waypoints samples within 0.3 m.
- Given the player in Play, when they walk that route both ways and push at every handrail and wall, then nothing snags, they never fall through a deck, and they cannot get from the camp side to the river side under the floor.
- Given a new game, then the guard patrols his five waypoints and `RA_Warehouse` warns the player inside as before.
- Given `Dress Home Yard` rerun, then the ramp bay tarp is tied to the camp ramp's west edge with no error.
- Given Play at 23:00 and at noon, then both door lanterns are lit at night and dark by day.
- Given the day captures beside W1–W6, then posts, braces, rubble, shingles, doors, handrails and hoist read as in the artworks, with the doorway at x 17, the west gap open and each fence end met by a round log at mid-gable.
- Given a Sites rerun, then the scene is unchanged apart from regenerated ids.

## Spec Change Log

- 2026-11-26, implementation (no loopback; **the first four points need the user's sign-off**, see the summary).
  (1) *Rocks:* the clump was measured against the new decks before modelling and left as it is: no rock rises through
  a deck or the floor (nearest: 8 cm under the camp ramp's deck at (17.5, 46)), and the artworks keep those rocks.
  (2) *Sites run / dresser rerun:* the prefab was placed through the tool's own code (`NewRoot` + `BuildWarehouse`) on
  the warehouse root only, not by a full Sites run, and `Dress Home Yard` was not rerun: both replace roots whole with
  no undo, and `Ramp_Camp` came out identical to the graybox box (same position, rotation and scale), so the tarp tie
  is unchanged. The last two acceptance criteria are therefore verified by that identity, not by a rerun.
  (3) *Graybox code kept:* the task said both "removed" and "builds the old boxes when the prefab is missing"; the
  fallback won, so `BuildWarehouse` still holds the graybox hall.
  (4) *Ground through the floor:* the terrain rises up to 7 cm above the floor over about 2 x 2 m inside the hall (the
  tool's 5 x 5 samples miss the crest; the graybox had it too). A terrain dip is **Ask First**; nothing was changed.
  Also: eaves 0.08 m (not the placeholder's 0.26 m) so the roof clears the flag pole; colliders, walk surfaces, Not
  Walkable volumes and lantern points come from a parts file the model script writes (`Warehouse.json`), so the
  builder holds no sizes; hand-edit entries dropped: `Warehouse/Post_Camp_0..2`, `Post_Landing_E/W`, `Ramp_River`
  (height pins of the old boxes) and the three removed `Crate_*`; the rubble is left out of the NavMesh bake (marked
  Not Walkable it cut the floor above it and took `WP_2_West` off the NavMesh); 39,598 triangles. KEEP: one script as
  the only source of sizes, the ground capture, walk boxes separate from the visual decks, the prefab as the Sites root.
  Further deviations, for the record: the capture grid is 0.25 m; the hoist is Not Walkable too; the graybox's two
  hand-duplicated posts went with it; `Area1SitesBuilder.NewRoot` takes a prefab (that file was not in the task
  list); the 0.6 m graybox fence panels hide the 0.4 m junction logs up to their top; along the east half the rubble
  is a low course, because the ground there is within 0.9 m of the floor (the frozen "1-2 m under the perimeter"
  cannot hold there); the hoist post stands 0.5 m off the wall to clear the eave.
- 2026-11-26, review (blind hunter, edge-case hunter, acceptance auditor). **Two findings were classified as
  loopbacks and were NOT looped back; they are the user's call:** (a) `intent_gap`: the frozen intent says the floor
  is the highest ground in the footprint (the terrain crests 7 cm above it) and asks for a 1-2 m bank all round (not
  possible on the east half); (b) `bad_spec`: two task lines did not describe what could be done (graybox "removed" yet
  kept as fallback; a full Sites run and dresser rerun). Reverting a verified building to re-derive it would not
  answer either, so the task lines were reworded to what was done and the questions go to the user. Patched: braces on
  the door leaves, ramp bents and landing were boards on edge (width and depth swapped); a gable board straddling the
  hatch's side could leave a slot (boards now joint at the hatch); the hatch's ledges and hinges were on the inside;
  the hoist's bolting blocks touched nothing (the post now stands at the corner post), its arm reached 3.1 m and its
  rope 4.1 m (now 2.6 m from the wall and 5 m); rail colliders reached 1-3 cm over the decks and the leaf colliders
  1.4 cm into the doorways (moved out; test margins tightened to 5 mm); the skirt boxes are invisible, so an
  under-floor Not Walkable volume was added; a parts file without a field threw in the middle of a Sites run (fields
  checked, lists validated before building); the prefab build reimports the FBX first and the script writes the parts
  file before the .blend, so model and parts cannot be a version apart; the Sites warning now compares position as
  well as height (`centre` goes into the parts file); the goods are placed from the parts file's `hoistDrop`, east of
  the rope and against the bank, clear of the way to the dock gate, and a missing kit piece is an error; a board
  length can no longer end in a sliver; the script refuses a capture query outside the grid; the landing's diagonals
  are skipped over high ground; tests: a positive control for the overlap queries, the rubble boxes, the ramp length
  as a range, `DontSave` on the instance; dead code removed (`LANTERN`, `patches`, `wall_side`); docs corrected (run
  command, west log, crate size, stale future tense in the build brief). Deferred: a flat-lit concept sheet of the
  warehouse (no piece but the beds has one). Rejected: the capture reading the root as it stands (documented instead:
  run Sites first after a floor change) and rocks under groups not being seen (documented, logged count).

## Design Notes

The root stays in the tool rather than leaving it: a Sites run replaces roots whole, so a hand-owned warehouse would need the tool taught to skip it, and its name and parent are already what the dresser, the brief and the docs point at. The cost is that the model is fitted to today's ground; the floor-height warning catches a sculpt that moves it.

Gable walls keep the placeholder's 0.4 m build-up (posts outside, boards on the inner face), so the fence panels that end inside it stay hidden.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after each tool and in Play -- expected: no errors

**Manual checks:**
- Captures listed above, shown to the user before the docs are written.
- Triangle count of the FBX reported per object.

## Suggested Review Order

**The building, piece by piece (Blender script)**

- Entry point: what is built, in order, and the facts handed to Unity.
  [`build_warehouse.py:935`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L935)

- The ground capture: every post, leg and stone reads its height here.
  [`build_warehouse.py:94`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L94)

- One plank per piece; wider timbers squeezed across the grain.
  [`build_warehouse.py:167`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L167)

- A post on a padstone, tarred foot, or just packing where the ground is close.
  [`build_warehouse.py:351`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L351)

- Hall frame, boards on the posts' inner face, junction logs, wall colliders.
  [`build_warehouse.py:384`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L384)

- Roof: boards, rafters, shingle courses with mended patches.
  [`build_warehouse.py:512`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L512)

- One function for both ramps: deck, bents, outside handrails, walk box.
  [`build_warehouse.py:630`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L630)

- Rubble bank, and the boxes that close the space under the floor.
  [`build_warehouse.py:843`](../../Assets/_Game/Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py#L843)

**The prefab round it (builder)**

- Model plus parts file in, prefab out; anything missing stops before saving.
  [`WarehouseBuilder.cs:162`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L162)

- Rubble left out of the bake: marked Not Walkable it cut the floor.
  [`WarehouseBuilder.cs:244`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L244)

- Walk boxes, colliders and NavMesh volumes straight from the parts file.
  [`WarehouseBuilder.cs:252`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L252)

- Kit lantern with a clock-driven light and glow.
  [`WarehouseBuilder.cs:312`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L312)

- Ground capture: terrain plus root rocks, relative to the floor.
  [`WarehouseBuilder.cs:91`](../../Assets/_Game/Editor/World/WarehouseBuilder.cs#L91)

**Placement by the Sites tool**

- The root is a prefab instance; graybox only as the fallback.
  [`Area1SitesBuilder.cs:515`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L515)

- Seats the prefab, warns when it no longer stands where it was fitted.
  [`Area1CampInterior.cs:101`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L101)

- Goods under the hoist, from the parts file's rope point.
  [`Area1CampInterior.cs:189`](../../Assets/_Game/Editor/World/Area1CampInterior.cs#L189)

**Supporting**

- Prefab contract tests: ramp box, clear doorways and decks, rubble boxes, lanterns.
  [`WarehousePrefabTests.cs:45`](../../Assets/Tests/EditMode/WarehousePrefabTests.cs#L45)

- Pipeline, contract and known limits.
  [`Warehouse/CLAUDE.md:9`](../../Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md#L9)

- What differs from the artworks and the brief, as built.
  [`WAREHOUSE_CONCEPTS.md:142`](../../Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md#L142)
