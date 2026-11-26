---
title: 'Area 1 canteen: concept pack, then the cookhouse (kitchen, restricted store), cook porch, mess shelter and service yard'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: '846a6c33'
context:
  - '{project-root}/Assets/_Game/Editor/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/Environment/Camp/WAREHOUSE_CONCEPTS.md'
  - '{project-root}/Assets/_Game/Prefabs/World/Warehouse/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/STARTING_TOWN.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The workers' canteen west of the punishment square, where Aldric (Cook) and Tob (KitchenHand) spend their day, is a graybox: an open-fronted box, a canvas bay and a few kit tables. It needs a real kitchen building that stores food and tools, a cooking place, somewhere for workers to eat, and a store the player may not enter.

**Approach:** Two phases, as for the broken stretch. First a concept pack: captures of the site with temporary massing shapes, image-to-image prompts and a build brief, then a pause while the user generates and judges the artworks. Then the build, from existing kit assets and Blender (MCP) models: cookhouse, cook porch, mess shelter, service yard, and the restricted store.

## Boundaries & Constraints

**Always:**
- Plan (agreed 2026-11-26): the cookhouse stands on the flat plot (x −20..−8, z 16..30), long axis north–south. South room = kitchen (about 5.5 × 5.5 m) with a serving hatch and counter facing the square and a rubble hearth with chimney; north room = store (about 3.5 × 5.5 m) with a lockable inner door from the kitchen and a wide delivery door at the back. Behind the store, a fenced service yard (cart, woodpile, chopping block, water butts, wash tub, slop trough, fish rack, crates, barrels). On the kitchen's south side an open cook porch holds the player's cooking station. South of it, an open-sided mess shelter with long trestle tables and benches, and more tables in the open.
- The store is restricted at all hours; owners are both `NPC_Cook` and `NPC_KitchenHand`. The kitchen, porch, mess and yard are not restricted. The volume lives under the scene root `RestrictedAreas`.
- The player reaches `CookingStation` without entering a restricted volume. Its recipes and component values are unchanged.
- Build quality sits between the huts and the warehouse, in the same wood (`M_Camp_*`, the rules of `Camp/CLAUDE.md`). Stone is rubble only. Night lights use `ClockNightLight`. Kit materials are never edited; kit props are checked for size before placing.
- The building is one Blender script (`Source~`, FBX and parts file beside it) wrapped in a generated prefab. An editor command places it and the dressing, and can be rerun without duplicates.
- Doorways are at least 2.1 m high and 1 m wide; steps under 0.3 m. Both rooms, the porch and the mess are on the NavMesh. Tob's bed, `Spot_Cook_Night`, both NPCs' stand spots and the station move with the build and still work (routine, sleep, shop, dialogue).
- The square's three 6 m sight lanes stay clear.
- Two pauses: after the concept pack, and after the first build is captured by day and at 23:00 in Play.
- Rebake the NavMesh (`StartBakingSurfaces`, check for a `... 1.asset`), then save the scene.

**Ask First:** Any image or Meshy generation (cost and plan first). Moving a `CampCentre` flag, a hut or the stage. Terrain changes outside the plot. More than 60,000 triangles for the whole canteen. Restricting anything besides the store.

**Never:** Lootable store containers, a lock or key on the store door, Aldric's stash, workers' eating routines, new dialogue or quests (later specs). `generate_image` / OpenRouter. Changes to Sites, Camp Edge or the settlement tools' outputs other than removing the canteen graybox. Editing `hand-edits.json`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Place command, first run | Prefab and materials present | Graybox `worker-canteen` / `worker-dining` and the old kit tables removed; cookhouse, dressing and restricted volume placed | N/A |
| Place command, rerun | Canteen already built | Same poses, no duplicates, no new assets | N/A |
| Model missing | No FBX, parts file or a missing material | Nothing in the scene changes | Error names what is missing |
| Player enters the store | Any hour, an owner sees it | Trespass warning from that owner | N/A |
| Player in kitchen, porch, mess or yard | Any hour | No trespass | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/World/Area1WorkerCanteen.cs` -- the graybox builder (refuses to run while the settlement is hand-owned; left alone)
- `Assets/_Game/Editor/World/WarehouseBuilder.cs`, `Art/Environment/Camp/models/warehouse/Source~/build_warehouse.py` -- pattern: script-built model, parts file, generated prefab, lanterns
- `Assets/_Game/Editor/World/HomeYardDresser.cs`, `WorkerHutKitBuilder.cs`, `CampClothMesh.cs` -- `Prop` / `Piece` (kit props, kit light removed), tarps and ropes
- `Assets/_Game/Scripts/World/RestrictedArea.cs` -- `_ownerEntities`, box shape (`Scripts/World/INTERACTION.md`)
- Scene: `Area1_Blockout/TentCamp/Canteen` (hand-owned: `worker-canteen` 5 × 6 m at (−15, 21), `worker-dining` at (−15, 15.8), `KitchenHandBed`), `Camp/Canteen` (kit tables, bench), `Decor/SM_cartB_a1` (−16.5, 12), `SM_Trough` (−20, 16), `CookingStation_Fireplace` (−9.4, 21.2), `Cook` (−6, 20), `KitchenHand` (−10.6, 22.3), `NpcRoutineSpots/Spot_Cook_Night` (−14.5, 23.3), `CampCentre/Flag_0` (−12, 18), stage x −5..1, z 21..27
- Ground: y 0 on the plot; +0.5 m west of x −21 and south of z 14 (x < −12); inner palisade on z 9

## Tasks & Acceptance

**Execution (phase 1, concept pack):**
- [x] `StartingTown.unity` (nothing saved) -- hide the graybox, add temporary massing shapes for the plan, take six 1920 × 1080 captures under temporary light, restore -- inputs for the artworks
- [x] `Assets/_Game/Art/Artworks/Starting area/Camp/Canteen/refs/` -- the six captures (`K1`–`K6`: from the square, mess and porch, service yard and back door, kitchen interior toward the hatch, store interior, night from the square), each checked by eye
- [x] `Assets/_Game/Art/Environment/Camp/CANTEEN_CONCEPTS.md` -- the place's story, what the artworks should settle, template and one view paragraph per capture, build brief, verdict log; pointer from `Camp/CLAUDE.md` -- **pause for the user's artworks and verdict**

**Execution (phase 2, build):**
- [x] `Assets/_Game/Art/Environment/Camp/models/cookhouse/Source~/build_cookhouse.py` -- shell with interior, partition, hatch and counter, doors, hearth and chimney, porch, mess shelter; writes `Cookhouse.fbx`, `Cookhouse.json`, `Cookhouse.blend`
- [x] `Assets/_Game/Editor/World/CookhouseBuilder.cs` -- `Tools/World/Build Cookhouse Prefab` (colliders, walk surfaces, lights from the parts file → `Prefabs/World/Cookhouse/Cookhouse.prefab`) and `Tools/World/Place Area 1 Canteen` (removes the graybox, places prefab, dressing and service yard, moves station, bed and spots, builds `RestrictedAreas/RA_CookhouseStore`)
- [x] `Assets/Tests/EditMode/CookhousePrefabTests.cs` -- doorway sizes, walk surfaces, store box inside the store room and clear of the kitchen
- [x] `StartingTown.unity` -- run both commands, rebake, captures from every K view by day and K6 at 23:00 in Play; **pause**; play test
- [x] `Assets/_Game/Prefabs/World/Cookhouse/CLAUDE.md`, `Scenes/CLAUDE.md`, `STARTING_TOWN.md`, `Editor/World/CLAUDE.md`, `Scripts/AI/NPC/CLAUDE.md`, `Data/NPCs/KitchenHand/CLAUDE.md`, root index -- pipeline, contract, moved spots

**Acceptance Criteria:**
- Given the concept pack, when the user opens `CANTEEN_CONCEPTS.md`, then each of the six views has a capture, a ready-to-paste prompt and a save name, and the scene is unchanged on disk.
- Given the built canteen in Play, when the player walks from the square to the cooking station and cooks, then no trespass is raised.
- Given Play with Tob or Aldric in sight, when the player steps into the store, then that owner warns him; in the kitchen he is left alone.
- Given a full day in Play, when the clock passes 05:00, 07:00 and 21:00, then Tob reaches the garden, his spot behind the counter and his bed.
- Given the day captures beside the accepted artworks, then the cookhouse, mess and yard read as in them.

## Spec Change Log

- 2026-11-26, phase 1. Seven captures instead of six: `K7 overview` added (the only view that shows the whole
  layout); `K3` is saved as `K3 service yard ref` and its artwork as `K3 back alley`. Taken with temporary massing
  shapes, a temporary sun and the ground levelled in memory; the scene was reloaded unsaved and the terrain heights
  restored (the terrain asset is flagged dirty with identical values).
- **Plan change found on site:** the 6 m sight lane from the punishment post to `arrival-gate` passes just north of
  the plot (south edge z 26.0 over x −13), so the cookhouse ends at z 24.5 and the service yard is the alley
  **west** of the store (between the cookhouse and the huts), not north of it. Brief: `CANTEEN_CONCEPTS.md` →
  *Build brief*.
- **For phase 2:** the south half of the footprint stands on ground 0.3–0.5 m higher; the build needs a small cut
  (inside the plot's block, z 12.4..25), then a sculpt capture. `CampCentre/Flag_0` stands 0.9 m south of the hatch
  (Ask First if it should move).
- 2026-11-26, artworks. The user's own image tool had hit its limit and they asked for Meshy: seven
  `meshy_image_to_image` runs, 63 credits (cost given first, no regeneration). On the results the user answered
  "all seems good, proceed" to the proposed build: horizontal boards between posts, shingles, the K4 hearth
  mid-wall, a stake fence with a gate, the tripod fire, three night lights, the flag left where it is.
- 2026-11-26, phase 2 (build). Deviations from the frozen text, all listed in `CANTEEN_CONCEPTS.md` → *As built*:
  the "lockable inner door" is a fixed leaf standing open (a lock is in *Never*; the restricted area closes the
  store); the mess rows stand 2.5 m apart; the fish rack is at the alley's south end; the Cook's night spot is by the
  hearth; a third night light (`PorchFire`) was added because the cooking station prop has none.
- **Open at the second pause (Ask First items the build ran into):**
  1. **Triangles.** The model is 26,800; the kit dressing adds about 124,000 at LOD0 (30,000 at its lowest LOD), over
     the 60,000 line. Two 95,000-triangle kit props were already swapped out.
  2. **Ground cut outside the plot.** The command levels x −21..−12, z 12.4..25.2 and x −12.5..−5, z 9.9..15.2 (plus a
     1.2 m bank): south of z 16 for the porch and the mess, which the plot as written (x −20..−8, z 16..30) did not cover.
- **Decided by the user at the second pause (2026-11-26): "keep it as is"** for both: the dressing stays at its
  weight, and the ground cut stays as made.
- Review (three reviewers) patches: the place command validates every anchor before it changes anything and finds
  scene objects by rooted path; the roof's shingle cut-out for the chimney was mirrored to the eave; the shutter's
  collider sat 0.3 m north of it; the porch roof poked through the main roof's corners (lowered 0.25 m); the west gate
  leaf swung into rising ground (now opens into the alley); door tests read the openings from the parts file.
- **Not verified in Play:** cooking at the station, Tob's 05:00 walk to the garden (the path is complete on the
  NavMesh), and opening Tob's dialogue and shop across the counter.

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`, `CookhousePrefabTests` + `RestrictedAreaRulesTests`) -- expected: all pass
- `read_console` after each command -- expected: no errors, no missing-script warnings from kit lights

**Manual checks:**
- NavMesh covers kitchen, store, porch and mess; no `NavMesh 1.asset`
- Triangle count of the canteen reported at the second pause

## Suggested Review Order

**The look, and what was decided**

- The built canteen, day and night, beside the artworks one folder up.
  [`built/`](../../Assets/_Game/Art/Artworks/Starting%20area/Camp/Canteen/built/)

- Decisions taken from the artworks, then what the build changed.
  [`CANTEEN_CONCEPTS.md:229`](../../Assets/_Game/Art/Environment/Camp/CANTEEN_CONCEPTS.md#L229)

**The model**

- Entry point: every size the game depends on, in one block.
  [`build_cookhouse.py:34`](../../Assets/_Game/Art/Environment/Camp/models/cookhouse/Source~/build_cookhouse.py#L34)

- One framed wall with openings; refuses an opening that is not between posts.
  [`build_cookhouse.py:99`](../../Assets/_Game/Art/Environment/Camp/models/cookhouse/Source~/build_cookhouse.py#L99)

- Doors as fixed open leaves, the hatch's counter and propped shutter.
  [`build_cookhouse.py:307`](../../Assets/_Game/Art/Environment/Camp/models/cookhouse/Source~/build_cookhouse.py#L307)

- Anchors handed to Unity; the restricted box reaches under the floor.
  [`build_cookhouse.py:628`](../../Assets/_Game/Art/Environment/Camp/models/cookhouse/Source~/build_cookhouse.py#L628)

**Placing it in the scene**

- The place command: validates everything, then levels, replaces and moves.
  [`CookhouseBuilder.cs:357`](../../Assets/_Game/Editor/World/CookhouseBuilder.cs#L357)

- The ground cut: a ceiling under a bank, so a rerun changes nothing.
  [`CookhouseBuilder.cs:454`](../../Assets/_Game/Editor/World/CookhouseBuilder.cs#L454)

- The store's restricted area, owned by both cooks.
  [`CookhouseBuilder.cs:517`](../../Assets/_Game/Editor/World/CookhouseBuilder.cs#L517)

- Kit dressing; the heaviest part of the canteen (see the change log).
  [`CookhouseBuilder.cs:538`](../../Assets/_Game/Editor/World/CookhouseBuilder.cs#L538)

- Prefab build: remap, Not Walkable parts, table copies.
  [`CookhouseBuilder.cs:93`](../../Assets/_Game/Editor/World/CookhouseBuilder.cs#L93)

**Tests and docs**

- Doorways measured from the parts file, hatch, partition, restricted box.
  [`CookhousePrefabTests.cs:68`](../../Assets/Tests/EditMode/CookhousePrefabTests.cs#L68)

- Pipeline, contract, what a rerun discards, known limits.
  [`Cookhouse/CLAUDE.md:10`](../../Assets/_Game/Prefabs/World/Cookhouse/CLAUDE.md#L10)

- Site facts: footprint, sight lane, people, ground.
  [`STARTING_TOWN.md:386`](../../Assets/_Game/Scenes/STARTING_TOWN.md#L386)
