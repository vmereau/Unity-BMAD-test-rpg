# CLAUDE.md — Assets/_Game/Prefabs/World/Warehouse

> The Area 1 warehouse (`StartingTown` → `Area1_Blockout/Warehouse`): a script-built Blender model wrapped in a
> generated prefab. Look and build brief: `Art/Environment/Camp/WAREHOUSE_CONCEPTS.md`; wood rules:
> `Art/Environment/Camp/CLAUDE.md`.

---

## Pipeline

`Warehouse.prefab` is **generated**: never edit it or its scene instance by hand.

| Step | What | Writes |
|------|------|--------|
| 1 | `Tools/World/Capture Warehouse Ground` (`Editor/World/WarehouseBuilder`), with `StartingTown` open | `models/warehouse/Source~/warehouse_ground.json`: terrain + `SM_Rock*` scene roots, 0.25 m grid, heights relative to the floor |
| 2 | `models/warehouse/Source~/build_warehouse.py` in Blender (`blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) | `Warehouse.fbx`, `Warehouse.json` (parts file), `Source~/Warehouse.blend` |
| 3 | `Tools/World/Build Warehouse Prefab` | `Warehouse.prefab` |
| 4 | `Tools/World/Build Area 1 Sites` places it (`Area1CampInterior.BuildWarehouse`), then rebake the NavMesh | the scene root |

- Rerun from step 1 after moving a rock near the hall, sculpting there, or moving the `warehouse` zone: posts,
  trestle legs, padstones and rubble are modelled against the capture. Sites warns when the floor it computes is
  more than 5 cm from the height the model was fitted to.
- The capture measures from the **scene root as it stands** (its position is the floor) and sees rocks only as
  active scene roots named `SM_Rock*` (its log says how many points landed on rock). After a change that moves the
  floor or the zone, run Sites first so the root is seated, then steps 1–3, then Sites again: its warning compares
  the root's position with the capture's (`Warehouse.json` → `floorWorldY`, `centre`).
- Rerun from step 2 for any change of shape. A size is written **once**, in the script: the builder reads colliders,
  walk surfaces, Not Walkable volumes and lantern points from `Warehouse.json`, and Sites the point under the
  hoist's rope (`hoistDrop`) for the goods.
- The first import needed the material remap (`Wh_*` → `M_Camp_*`, model importer); it lives in `Warehouse.fbx.meta`.
- A missing model, a model object the parts file names, a non-`M_Camp_` material or a missing `Ramp_Camp` stops the
  build before saving. Without the prefab, Sites logs an error and builds the old graybox hall.
- To place it without a full Sites run, call `Area1SitesBuilder.NewRoot(blockout, "Warehouse", prefab)` then
  `Area1CampInterior.BuildWarehouse(..., root, true)` (how it was first placed: a Sites run replaces every tool root).

## Frame and contract

Local frame: x east, y up, z north (the river); origin on the floor's top at the hall's centre (world (15, 4.65, 52)).

- **Root children:** `Model` (`Frame`, `Walls`, `Floor`, `Roof`, `Doors`, `Ramps`, `Landing`, `Hoist`, `Rubble`,
  `Iron`), the walk boxes `Floor`, `Landing`, `Ramp_River`, `Ramp_Camp`, `Colliders` (`Walls`, `Roof`, `Structure`,
  `Rails`, `Rubble`), `NavMesh` (Not Walkable volumes along the handrails and under the floor), `DoorLantern_Camp`, `DoorLantern_River`.
  Sites adds `HoistGoods` (kit crates and sacks at the foot of the bank, south of the rope: the way from the river
  ramp to `DockGate` passes north of them).
- **`Ramp_Camp`** stays a direct child, active, a unit `BoxCollider` scaled (3.5, 0.2, 13.85) with its faces on the
  deck's faces: `HomeYardDresser` ties the east yard's tarp to it by path. The walk boxes have no renderer.
- **Walking surface = the walk boxes** (smooth); the NavMesh comes from the deck and floor meshes. The grip battens
  are visual (2 cm).
- **Cart doorways** 2.4 × 2.8 m at local x 0.8–3.2 on both long walls, leaves standing open outside. Decks 3.5 m
  clear between handrails (rail posts and their colliders stand outside the deck's edge).
- **Nothing passes under the hall:** `Colliders/Rubble` holds a box under each wall and one on the palisade line
  (local z 0), from below the ground to the floor. Visually the bank stops 1–2 m up at the south-west corner.
- **`Rubble` is left out of the NavMesh bake** (`ignoreFromBuild`). Marked Not Walkable instead, the stones packed
  under the boards cut the floor above them out of the NavMesh (it took the guard's west waypoint with it).
- **Walls** are 0.4 m colliders as in the graybox; posts stand outside, boards on their inner face (0.30 m in), so the
  two fence panels that end inside the gable walls (`outer-fence-3/Panel_060`, `Panel_061`) stay hidden.
- **Eaves are tight (0.08 m)** because `CampCentre/Flag_Warehouse` stands 0.3 m off the south-west corner; the gables
  overhang 0.5 m.
- **Lanterns:** kit `SM_Lantern_Hanging` (unpacked) on the model's iron bracket, a point light and a glow quad driven
  by `ClockNightLight`. Values: the constants at the top of `WarehouseBuilder`.
- Tests: `Tests/EditMode/WarehousePrefabTests` (ramp box, clear doorways and decks, handrails, lanterns).

## Known limits

- The terrain rises up to 7 cm above the floor over about 2 × 2 m inside the hall (world x 13.5–15.5, z 52.2–54.5):
  the Sites tool samples the footprint on a 5 × 5 grid and misses that crest. It shows as a patch of ground through
  the boards. Fix: a small terrain dip there (then capture the sculpt), not a higher floor.
- The fence panels on both sides are still graybox and 0.6 m thick, so they swallow the round junction log at the
  middle of each gable (0.4 m; the east one runs up to the ridge, the west one stops under the hatch) up to their top.
