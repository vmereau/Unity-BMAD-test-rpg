# CLAUDE.md — Assets/_Game/Prefabs/World/Warehouse

> The Area 1 warehouse (`StartingTown` → `Area1_Blockout/Warehouse`): a script-built Blender model wrapped in a
> generated prefab. Look and build brief: `Art/Environment/Camp/WAREHOUSE_CONCEPTS.md`; the cellar under it:
> `Art/Environment/Camp/WAREHOUSE_CELLAR_CONCEPTS.md`; the quay, the tally room and the cellar's side room:
> `Art/Environment/Camp/QUAY_CONCEPTS.md`; wood rules: `Art/Environment/Camp/CLAUDE.md`.

---

## Pipeline

`Warehouse.prefab` is **generated**: never edit it or its scene instance by hand.

| Step | What | Writes |
|------|------|--------|
| 1 | `Tools/World/Capture Warehouse Ground` (`Editor/World/WarehouseBuilder`), with `StartingTown` open | `models/warehouse/Source~/warehouse_ground.json`: terrain + `SM_Rock*` scene roots, 0.25 m grid, heights relative to the floor |
| 2 | `models/warehouse/Source~/build_warehouse.py` in Blender (`blender -b -P`, or in a session `runpy.run_path(path, run_name="__main__")`) | `Warehouse.fbx`, `Warehouse.json` (parts file), `Source~/Warehouse.blend` |
| 3 | `Tools/World/Build Warehouse Prefab` | `Warehouse.prefab` |
| 4 | `Tools/World/Build Area 1 Sites` places it (`Area1CampInterior.BuildWarehouse`), then rebake the NavMesh | the scene root, `HoistGoods`, `CellarGoods` |
| 5 | `Tools/World/Place Warehouse Cellar` (once, and again if the cellar's, the side room's, the tally room's or the quay's plan changes) | the terrain holes over the cellar and its side room, `RestrictedAreas/RA_WarehouseCellar` (with its `SideRoom` and `Passage` boxes), `RA_Warehouse/TallyRoom`, `RA_Docks/Quay` |

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
  two fence panels that end inside the gable walls (`outer-fence-3/Panel_060`, `Panel_061`) keep their boxes hidden
  (their logs stop outside, at the junction logs).
- **Eaves are tight (0.08 m)** because `CampCentre/Flag_Warehouse` stands 0.3 m off the south-west corner; the gables
  overhang 0.5 m.
- **Lanterns:** kit `SM_Lantern_Hanging` (unpacked) on the model's iron bracket, a point light and a glow quad driven
  by `ClockNightLight`. Values: the constants at the top of `WarehouseBuilder`.
- Tests: `Tests/EditMode/WarehousePrefabTests` (ramp box, clear doorways and decks, handrails, lanterns).

## Cellar (`spec-area-1-warehouse-cellar`)

A room under the landing and most of the hall (6.1 × 8.9 m), dry-stone rubble on every side: inside faces local
x −1.2..4.9, z −1.9..7.0, floor at world y 0.1 (local −4.55), about 4.3 m to the hall's joists. Sizes are the `CELLAR_*` constants of the script.

- **The terrain has a hole over it** (cells x 526..532, z 563..571 of the 1024 hole map). Heights are untouched,
  so Sites still finds the same floor. The parts file's `cellar.hole` is a rectangle in the prefab's frame; every
  hole cell wholly inside it is cut (`WarehouseBuilder.HoleCells`). `Place Warehouse Cellar` cuts it in the scene;
  the terrain tool cuts it again on every run (`CutCellarHole`, after the source asset's holes are copied).
- **The ground capture still holds the old bank.** `Ground.at` in the script answers with the cellar's floor inside
  the room, so the hall's two door posts and the landing's middle posts come down to it as pillars on their own. A beam on
  two more posts (`CELLAR_BEAM_Z`) carries the joists midway to the back wall.
  `Ground.raw` is the captured ground, for what stands outside.
- **Walls hide the hole's edge:** 1.2 m thick under the hall's west side and at the back, 0.7 m on the east. The
  hole stops 0.4 m short of the front wall (its next row of cells would open the shelf outside): the ground left
  there rises above the floor at x −1.2..0.8, and the front wall's west bay is built 0.55 m into the room over it
  (`CELLAR_BAY_IN`). Moving a wall inward means checking `cellar.hole` against it
  (`WarehousePrefabTests.CellarRestrictedBox_AndHole_StayInsideTheWalls`).
- **The back wall cannot go further south.** Hand-placed rocks (`SM_Rock_06 (20)`, `(21)`, `SM_Rock_07 (2)`, `(4)`)
  are buried under the hall's south part and reach world z 49.8; the back face stands at z 50.1. One of them pokes
  0.4 m in at the south-west corner, 2.5–3.5 m up: the corner pier (`CELLAR_PIER`) is there to hide it. Before
  moving a cellar wall, ray-test the new volume against the `SM_Rock*` colliders: the hole bares what is buried.
- **Model objects:** `Cellar` (earth floor, door frame, sealing boards under the hall's floor and the landing's
  deck, joists, ladder) and `CellarWalls` (rubble stones on cores). Both are in the bake. The floor is `M_Camp_Earth` (kit mud texture).
  A stone on a face seen only from the room has no underside below 1.2 m and no top above 2.1 m (`room=True`).
- **Closed overhead:** a tight layer of boards under the floor and the deck. Without it the sun draws stripes on
  the cellar floor through the 4 mm gaps between the boards.
- **Dock door:** `CellarDoor`, a nested instance of `Prefabs/World/Doors/Poor small door` (1.1 m leaf, stretched
  1.06 in height for 2.1 m of headroom), hung on the east jamb, **locked** (`Key_WarehouseCellar` or a pick at Lockpicking 1, set by the builder through
  `RouteDoorLocks`), opening into the room, save id
  `area1-warehouse-cellar-door`. It is left out of the bake, so the shut leaf does not cut the room off the NavMesh.
- **Hatch:** boards, two strap hinges and a ring lying 1.6 cm proud of the hall's floor at local x −0.8..0.2,
  z 2.3..3.3. Visual only: the `Floor` walk box runs under it. Opening it and climbing the ladder is a later spec.
- **Colliders** `Colliders/Cellar` (walls, the door's step, the hall's and the beam's posts, the ladder); walk box `Cellar`.
  `Colliders/Rubble/Skirt_River` is in two pieces (`Skirt_River`, `Skirt_River_East`) either side of the room, and
  so is the palisade line's (`Skirt`, `Skirt_Line_East`): the room passes under that line and its side walls close it;
  `NavMesh/UnderFloor` is in three, leaving the room out.
- **Lanterns:** `CellarLantern_Door` outside (a night light like the cart doors') and, inside, `CellarLantern_Inside` on
  the landing's middle pillar and `CellarLantern_Back` on the beam's west post, marked `always` in the parts file:
  low lights that are simply on, with no clock. They cast soft shadows: unshadowed, a light that is on by day shows
  through the front wall on the dock.
- **Stores:** `CellarGoods` (Sites, the table in `Area1CampInterior`): kit casks, crates and sacks, placed in the
  root's frame. They keep the walk from the door round the pillars to the back clear.
- **`RA_WarehouseCellar`** is the warehouse guard's at all hours. It lies inside `RA_Docks` (Company, 21–06) too.

## Quay (`spec-area-1-warehouse-quay`)

Everything added on the river side after the cellar, in the same script and prefab. Sizes are the `QUAY_*`,
`BARGE*`, `TALLY_*`, `RETAIN_*` and `SIDE_*` constants of the script; the parts file carries them as `quay`,
`tally`, `retaining`, `cellar.side`, `cellar.sideHole` and `cellar.restrictedMore`.

- **The hall and the cellar keep their look.** The new parts (`Quay`, `Barge`, `Tally`, `TallyRoof`,
  `TallyFurniture`, `Retaining`, `CellarSide`) draw from their own random stream (`rng2`). What they take out of
  the old parts (the gable's boards and girt at the tally door, the cellar's east wall at the passage) is drawn
  into a scratch mesh with the hall's stream, so every other board, shingle and stone stays where it was. Add new
  pieces to a `rng2` part, never to an old one.
- **Quay:** deck local x −8.5..9.2, z 9.0..13.5, top at world y 0.0 (walk box `Quay`), on bearers and tarred piles
  fitted to the captured riverbed. Colliders for the mooring posts, the lantern posts and the net rack; none for
  the kerb (0.15 m) or the ladder. Its south 2 m is the cart lane: the test sweeps it, and no goods entry may stand
  in it.
- **Goods are the script's** (`quay.goods`: kit piece, position, euler, scale, solid). Sites only maps a kit piece
  to each (`Area1CampInterior.BuildQuayGoods` → `Warehouse/QuayGoods`), all left out of the bake. The crates and
  sacks stand where the user moved them by hand: move a prop in the script's table, or it comes back on a Sites run.
- **Barge:** `Barge` object, Not Walkable; colliders for its floor, sides and the gangplank (23.6°).
- **Tally room:** local x 6.0..9.3, z 0.2..5.6, floor at the hall's. The palisade's panels are its south and east
  walls (`outer-fence-3/Panel_061`, `_062`, `dock-fence-upper/Panel_147`, `_148`: the scene's, not the prefab's);
  the model gives the floor, the plank wall, a short west wall north of the hall's corner, and the roof.
  `TallyDoor` is a nested `Poor small door` in the east gable (z 2.3..3.44, hinge on the south jamb, opens east,
  unlocked, save id `area1-warehouse-tally-door`, left out of the bake). `Wall_East` is in three colliders round it.
- **Retaining wall:** `Retaining` object, face at local z 5.7 from the cellar's shoulder (x 6.1) to the dock spur;
  its body is also the side room's north wall and carries the tally room's floor.
- **Side room:** inside local x 6.25..8.2, z 0.95..4.4, ceiling at −1.7, floor the cellar's (walk boxes
  `CellarSide`, `CellarPassage`). Its hole rectangle is `cellar.sideHole` (cells x 533..535, z 566..569); the hall's
  east gable posts hang over it inside the wall between the two rooms (x 5.6..6.25). `Skirt_East`, `Wall_East` (the
  cellar's) and `UnderFloor_East` are each split round the passage. `CellarLantern_Side` is always on, with shadows.
- **Tunnel opening** (`spec-area-1-gang-river-house`): the side room's south wall is pierced at local
  x 6.7..7.9, 2 m high (`SIDE_TUNNEL`; `cellar.tunnel` in the parts file), with timber jambs and a lintel. That
  wall is drawn into its own part, `CellarTunnel`, on a third random stream (`rng3`); the wall it replaces is
  still drawn into a mesh nobody keeps, so `rng2` and every later part stay as they were. Colliders `Side_South`,
  `Side_South_East`, `Side_South_Header`; walk box `CellarTunnelSill`. The tunnel beyond is the gang house's
  model (`Prefabs/World/GangHouse/CLAUDE.md` → Tunnel): its first point stands on this wall's outer face, in the
  middle of the opening, and `Place Area 1 Gang House` refuses when the two are 5 cm apart. Moving the warehouse
  or the side room means moving `TUNNEL_PATH` in `build_gang_house.py` too. The side room's two crates (`Area1CampInterior.CellarGoods`) stand in
  front of the opening, not against the east wall: the way through passes west of them.
- **Tunnel door** (spec-smuggling-route-locks-and-keys): `TunnelDoor_Cellar`, a nested `Poor small door` on the
  side room's face of the opening, built from `cellar.tunnel` (no model change, constants `TUNNEL_DOOR_*` in
  `WarehouseBuilder`): the 1.1 m leaf covers the jambs' 1.0 m clear width, stands 0.15 m off the inner face (clear of
  the jambs and the lintel), hangs on the **east** jamb and swings into the room against the east wall. The side
  room's two crates do **not** stand against its east wall: they stand right in front of the opening (world x
  22.1–22.7); a west hinge would have left 0.27 m beside them. Locked with `Key_GangTunnel` (Lockpicking 1), save id
  `area1-warehouse-tunnel-door`, out of the bake. The `TallyDoor` stays unlocked.
- **Rocks:** `SM_Rock_06 (17)` stands 0.8 m east of the side room's east face, `SM_Rock_08 (4)` 0.2 m over its
  ceiling (and comes up through the tally room's floor in the south-east corner). Before growing either room,
  ray-test the new volume against the `SM_Rock*` colliders.
- **Restricted areas:** a restricted area's shape is every box under it, so the new rooms are child boxes built by
  `Place Warehouse Cellar` (`AreaBox`): enlarging the parents' boxes would have covered open ground.
- **Lanterns:** `QuayLantern_West` / `_East` are night lights like the cart doors'.
- **Ground capture:** over a terrain hole no ray meets the ground, so the capture reads the height map there
  (`TryGround`): the model is fitted to the bank as it was before the holes. The grid reaches local z +17.5 (the
  riverbed under the barge).
- Tests: `WarehousePrefabTests` (tally doorway and door, room's floor / wall / roof, quay lane and goods, lanterns,
  passage, side room's hole and boxes, malformed `quay` / `tally` / side room refused).

## Known limits

- The terrain rises up to 7 cm above the floor over about 2 × 2 m inside the hall (world x 13.5–15.5, z 52.2–54.5):
  the Sites tool samples the footprint on a 5 × 5 grid and misses that crest. North of z 51.8 the cellar's hole
  has removed it; a strip may still show through the boards south of that. Fix: a small terrain dip there (then
  capture the sculpt), not a higher floor.
- **The root's pose is a saved hand edit** (`hand-edits.json` → `Warehouse`, y 4.647). A later sculpt lowered what
  Sites computes to 4.574, so a Sites run warns about the fit and then the hand edit puts the root back. Placing
  the prefab without a full Sites run means setting that pose by hand. Goods Sites stands on the terrain
  (`HoistGoods`) are placed before the hand edit moves the root: an unpinned one ends 7 cm off the ground.
- `hand-edits.json` still pins `Warehouse/HoistGoods/SM_Crate_01` and `SM_Crate_02`, which are now
  `SM_Crate_B_01`: each Sites run warns that they match nothing. Untick them in the Hand Edits window.
- The palisade butts into the round junction log at the middle of each gable (0.4 m; the east one runs up to the
  ridge, the west one stops under the hatch): the Camp Edge tool ends its logs 0.06 m outside the footprint there.
  The wall's line passes 0.1–0.3 m north of the logs' axis (z 52.1–52.3 against z 52), so the last log leans on the
  junction log's side rather than its middle. Moving a junction log means changing `WAREHOUSE_LOG_CLEAR` in
  `Area1BlockoutBuilder` too.
- The river door's lantern is unshadowed and reaches into the tally room through the gable: at night the room is
  dimly lit although it has no light. Give that lantern shadows, or the room a candle on `ClockNightLight`.
- The quay's tarp lies beside the sacks since the user moved them; it is a low mound from far away.
- 25 kit props on the quay and in the barge are 34,276 triangles at LOD 0, twice the new model parts.
