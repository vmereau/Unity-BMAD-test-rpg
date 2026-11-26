# CLAUDE.md — Assets/_Game/Prefabs/World/Cookhouse

> The Area 1 canteen (`StartingTown` → `Area1_Blockout/TentCamp/Canteen`): the cookhouse (kitchen + restricted
> store), cook porch, mess shelter, tables and back-alley fence as one script-built Blender model wrapped in a
> generated prefab, plus a command that places it with its dressing. Look and build brief:
> `Art/Environment/Camp/CANTEEN_CONCEPTS.md`; wood rules: `Art/Environment/Camp/CLAUDE.md`.

---

## Pipeline

`Cookhouse.prefab` is **generated**: never edit it or its scene instance by hand.

| Step | What | Writes |
|------|------|--------|
| 1 | `models/cookhouse/Source~/build_cookhouse.py` in Blender (`blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) | `Cookhouse.fbx`, `Cookhouse.json` (parts file), `Source~/Cookhouse.blend` |
| 2 | `Tools/World/Build Cookhouse Prefab` (`Editor/World/CookhouseBuilder`) | `Cookhouse.prefab` |
| 3 | `Tools/World/Place Area 1 Canteen`, with `StartingTown` open | Scene: levelled ground, the instance, `Dressing`, moved gameplay objects, `RestrictedAreas/RA_CookhouseStore` |
| 4 | `Tools/World/Capture Area 1 Terrain Sculpt` if step 3 logged "Levelled", then rebake the NavMesh and save the scene | `terrain-sculpt.bin`, the NavMesh asset |

- **The script borrows the warehouse's helpers**: it loads `models/warehouse/Source~/build_warehouse.py` as a library
  (`Mesh`, materials, FBX export), so the two buildings share one wood. A change to those helpers changes both.
- **The model's sizes are written once**, in the script. The builder reads colliders, walk surfaces, light points and
  table poses from `Cookhouse.json`; the place command reads its `centre`, `floorAboveGround` and `anchors` (station,
  bed, both cooks' spots, the store's box, shelf levels, hooks) from the same file, and checks every one before it
  changes anything; the tests read the door openings from it. **Three things are still written twice**: the two
  ground rectangles (`CookhouseBuilder.Level`, world coordinates), the table set's collider boxes (`BuildModel`) and
  the dressing's offsets. Move `CENTRE`, `MESS` or the porch in the script and they must follow by hand.
- The place command does not check that the prefab was rebuilt from the current parts file: run step 2 after step 1.
- The model's materials are `Wh_*` like the warehouse's; the builder copies the warehouse importer's remap
  (`Wh_*` → `M_Camp_*`) onto `Cookhouse.fbx` on every build.
- A missing model, parts list or camp material stops the build before saving. The place command checks everything
  it needs before it changes anything.
- Rebake rules, including what a script compile does to a running bake: `Editor/World/CLAUDE.md` → NavMesh bake.

## Frame and contract

Local frame: x east, y up, z north; origin on the floor's top at the building's centre (world (−15.75, 0.15, 20)).
The ground is flat 0.15 m below the floor: the place command cuts the plot down to world y 0 first.

- **Root children:** `Model` (`Frame`, `Walls`, `Floor`, `Roof`, `Doors`, `Hearth`, `Rubble`, `Iron`, `Furniture`,
  `Shelters`, `Yard`), `Tables` (six copies of the model's `TableSet`, three box colliders each), the walk boxes
  `Floor`, `Step_Kitchen`, `Step_Delivery`, `Colliders` (`Walls`, `Roof`, `Doors`, `Furniture`, `Structure`, `Fence`),
  `HatchLantern`, `HearthFire`, `PorchFire`.
- **Rooms:** kitchen z −4.5..0.8 (door in the south gable, x 0.75..1.85, into the porch), store z 1.04..4.26 (inner
  door in the partition, x 1.15..2.15; delivery door in the west wall, z 2.0..3.6). All three are 2.1–2.2 m high and
  at least 1 m wide; their leaves are fixed, standing open.
- **Hatch** in the east wall, z −1.4..0.8, sill 0.95: a wall collider under the sill and the counter's box keep
  everyone out. The shutter is solid.
- **NavMesh** (the bake collects render meshes, so the collider boxes play no part): `Roof`, `Shelters`, `Furniture`,
  `Hearth`, `Yard`, `Doors`, `Rubble` and each table carry a Not Walkable `NavMeshModifier`, so no island forms on a
  roof, a table or the hearth. `Frame`, `Walls` and `Iron` carry none: a modifier on the frame would cut the floor at
  every post. Dressing props are left out of the bake.
- **Night lights** (`ClockNightLight`, dark by day): the kit lantern on the hatch's north jamb, and `HearthFire`, a
  shadow-casting point light over the fire bench, and `PorchFire`, the same over the cooking station (the tripod prop has no light of its own). Values: the constants
  at the top of `CookhouseBuilder`.
- **Mess tables** stand in two rows 2.5 m apart under the shelter and two in the open; poses are world x, z, yaw in
  the script's `TABLES`. At 2.1 m apart the benches left 0.48 m and the NavMesh closed the aisle.
- Tests: `Tests/EditMode/CookhousePrefabTests` (walk surfaces, clear doorways, hatch, partition, the restricted box
  inside the store and clear of every spot NPCs and the player use, lights, tables).

## What the place command does

`CookhouseBuilder.Place` is rerunnable and gives the same scene each time:

- **Ground:** lowers the terrain to y 0 under the cookhouse, porch and alley (x −21..−12, z 12.4..25.2) and the mess
  (x −12.5..−5, z 9.9..15.2), under a bank that rises 0.6 m over 1.2 m. It never fills, and a second run changes nothing.
- **Removes** `Canteen/worker-canteen`, `worker-dining` and every child of `Camp/Canteen` (the old kit tables).
- **Replaces** `Canteen/Cookhouse` (the prefab instance) and `Canteen/Dressing` (kit props: the store's casks, sacks,
  shelf goods and hanging joints, the kitchen's pots and bowls, the alley's barrels and crates, fish on the rack,
  firewood, bowls on the tables). Hand edits inside either are lost on a rerun.
- **Moves** `CookingStation_Fireplace` (porch), `Canteen/KitchenHandBed` (kitchen, south-west corner), `KitchenHand`
  (behind the counter), `Cook` (outside, north of the hatch), `NpcRoutineSpots/Spot_Cook_Night` (by the hearth) and
  `Decor/SM_cartB_a1` (the alley).
- **Rebuilds** `RestrictedAreas/RA_CookhouseStore`: the store inside its boards, owners `NPC_Cook` and
  `NPC_KitchenHand`, all hours, default one warning.

## Known limits

- Doors are fixed open leaves; the store is closed by the restricted area only (lock, key, lootable goods: later specs).
- `CampCentre/Flag_0` stands 1 m in front of the east wall, 1 m south of the hatch. Sites owns it.
- The ground cut follows the heightmap (about 1 m per sample) and stops at x −21: the alley's west metre
  (x −22..−21) stands on the bank, 0.1–0.3 m high, so the west fence panel and gate leaf are sunk by that much. The
  lane north of the fence rises too (up to 0.4 m at z 26), which is why the west leaf opens into the alley.
