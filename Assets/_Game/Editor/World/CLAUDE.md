# CLAUDE.md — Assets/_Game/Editor/World

> Loaded when Claude accesses files in this folder. Covers the Area 1 (StartingTown) build tools under `Tools/World/...`:
> valley terrain, Camp Edge, Sites, hand edits, grading, one-shot authoring commands, run order, root ownership,
> NavMesh rebake and play tests. Per-site layout facts live in `Scenes/STARTING_TOWN.md`.

---

## Tools

| Menu (`Tools/World/...`) | Class | Writes |
|--------------------------|-------|--------|
| `Build Area 1 Terrain` | `Area1TerrainBuilder` (+ `Area1TerrainMath`, `Area1TerrainSculpt`) | `StartingTownTerrain.asset`: sculpt, paint, forest |
| `Capture Area 1 Terrain Sculpt` | `Area1TerrainSculpt` | `docs/World/Areas/area-001/terrain-sculpt.bin` |
| `Build Area 1 Camp Edge` | `Area1BlockoutBuilder` (+ `PalisadeMesh`, `PalisadeKit`, `BrokenStretchBuilder`, `BrokenStretchLayout`) | `Area1_Blockout/CampEdge` only, and the wall meshes in `Art/Environment/Camp/Generated/Palisade.asset` |
| `Sink Area 1 Broken Stretch Ground` | `BrokenStretchBuilder` | Live terrain: a 0.3 m hollow and bare-earth paint outside the broken stretch (one-time; see **Broken stretch**) |
| `Build Area 1 Sites` | `Area1SitesBuilder` (+ `Area1CampInterior`, `Area1TentCamp`, `Area1Stoneworks`, `Area1Timberworks`, `Area1Garrison`, `Area1Farm`, `Area1RemoteSites`, `Area1Worker*`) | The site roots under `Area1_Blockout` (see **Ownership**) |
| `Build Area 1 Worker Settlement` | `Area1WorkerSettlement` | Scoped settlement build; refuses while the settlement is hand-owned |
| `Area 1 Hand Edits` | `Area1HandEditsWindow` / `Area1HandEdits` | `docs/World/Areas/area-001/hand-edits.json` |
| `Grade Area 1 Fort Ramp` / `Grade Area 1 Goat Track` | `Area1FortRamp` | Live terrain heights along one path |
| `Rebuild Mafia Quarters`, `Author Mafia Buildings And Overlooks` | `Area1MafiaBlockout` | One-shot authoring (see below) |
| `Dress Home Yard` | `HomeYardDresser` | `PlayersZone/HomeYardDressing` |
| `Build Worker Hut Kit Prefabs` | `WorkerHutKitBuilder` | `Prefabs/World/Huts/` from the shell models in `Art/Environment/Camp/models/worker hut/` (see the Huts folder's `CLAUDE.md`); rebake afterwards |
| `Capture Warehouse Ground`, `Build Warehouse Prefab` | `WarehouseBuilder` | The ground capture the warehouse model is fitted to, then `Prefabs/World/Warehouse/Warehouse.prefab` from the model and its parts file (pipeline: that folder's `CLAUDE.md`); Sites places the prefab, rebake afterwards |
| `Place Warehouse Cellar` | `WarehouseBuilder` | The terrain holes over the warehouse's cellar and its side room (live terrain), `RestrictedAreas/RA_WarehouseCellar` and the child boxes `RA_Warehouse/TallyRoom` and `RA_Docks/Quay`, all from the parts file (see **One-shot authoring commands**); rebake afterwards |
| `Build Dock Gate Prefab` | `DockGateBuilder` | `Prefabs/World/DockGate/DockGate.prefab` from the dock gate model and its parts file (pipeline: that folder's `CLAUDE.md`); Sites places the prefab as the `DockGate` root |
| `Build Gang House Prefab`, `Build Wilderness Gate Prefab`, `Place Area 1 Gang House` | `GangHouseBuilder`, `DockGateBuilder` | `Prefabs/World/GangHouse/GangHouse.prefab` and `Prefabs/World/DockGate/WildernessGate.prefab` from their models and parts files; then, in the scene, the lowered bank under the gang house's deck, the terrain hole over its basement, the old wilderness opening's barricades removed and `TollGates/TollGate_Wilderness` moved into the gateway (pipeline: `Prefabs/World/GangHouse/CLAUDE.md`); capture the sculpt, run Camp Edge and Sites, rebake afterwards |
| `Build Cookhouse Prefab`, `Place Area 1 Canteen` | `CookhouseBuilder` | `Prefabs/World/Cookhouse/Cookhouse.prefab` from the cookhouse model and its parts file, then the canteen in the scene: levelled ground, the instance, `Canteen/Dressing`, moved gameplay objects, `RestrictedAreas/RA_CookhouseStore` (pipeline: that folder's `CLAUDE.md`); capture the sculpt and rebake afterwards |
| `Replace Graybox Worker Huts` | `WorkerHutRollout` (+ `WorkerHutRolloutRules`) | Swaps the graybox 3 × 3 m huts of `TentCamp/PlayersZone` and `MafiaZone` for shell prefabs, in place (rules: Huts folder's `CLAUDE.md` → Rollout); rebake afterwards |

Sites, river, lake, ford, roads and site features are read from `docs/World/Areas/area-001/layout.json`
(`Area1BlockoutBuilder.TryReadLayout`).

---

## Run order

**Terrain → (capture first if you sculpted) → Camp Edge → Sites → rebake the NavMesh.**

- Re-run Camp Edge after any terrain re-run, because panels follow the ground; re-run Sites after any terrain re-run,
  because heights (e.g. the quarry blocker) come from the ground. Then rebake.
- Rerun the tools after a sculpt or layout change, before you hand-edit (a hand-edit scan treats any stale object as
  moved).
- Moving a paint-only `path-*` / road line (`company-road`, `delivery-road`, `path-sledge`, `path-timberworks`,
  `path-garrison`, `path-farm`…) needs a terrain run, then Camp Edge and Sites. These lines never reach `CarveRoads`.
- Rebake after a run if a render mesh changed.

---

## Valley terrain

1000 m square (1025 heightmap, about 1 m per sample), origin (−500, −500), with the object at y = −30 and heights +30
so ground can sit below y = 0. The sculpt, paint and forest come from `Tools/World/Build Area 1 Terrain`.

- Sites, river, lake, ford and roads are read from `layout.json`; ridges, pad heights and the camp footprint are
  constants in the tool, so moving the ruins or quarry far means moving their ridge too.
- Every run rewrites `StartingTownTerrain.asset` but keeps hand edits stored in `terrain-sculpt.bin`: **after sculpting,
  run `Tools/World/Capture Area 1 Terrain Sculpt`.** The tool refuses to run while the terrain has uncaptured height
  edits.
- That check and the capture cover heights only: hand-painted textures and hand-placed trees are overwritten without
  warning, so those changes go into `layout.json` or the tool.
- The camp's ground, paint and holes come from `Art/Terrain/StartingTownTerrain_Source.asset` (the original 500 m
  terrain): don't delete it, and make lasting camp ground edits there.
- One hole is not in the source asset: the warehouse cellar's. After copying the source's holes, a run opens the
  cells the warehouse parts file names (`WarehouseBuilder.CutCellarHole`). Without a readable parts file it logs an
  error, leaves that ground solid and goes on: the cellar is then filled with terrain until `Place Warehouse Cellar`
  is run again. No terrain run has exercised this yet. The gang house's basement has a hole of its own, cut the
  same way right after (`GangHouseBuilder.CutBasementHole`; `Place Area 1 Gang House` cuts it in the scene).
- A rebuild scatters the forest from the current heights, so the first run after a sculpt changes trees on the sculpted
  ground, and a sculpt's whole-centimetre rounding can move heights by one heightmap step (1.83 cm).
- Water is one `Water_Volume` covering the valley, top at y = −2. Any non-water ground must stay above that, and
  `NotWalkable_West` / `NotWalkable_East` leave x 31..39 open for the ford. `Area1_Blockout` holds the pass blocker and
  the waterfall placeholder.
- **Fight pit:** the `fight-pit` feature's `w` is the floor's diameter, 7–16 m, or the terrain tool refuses it.
  `Area1TerrainMath.FightPit.Offset` is added last in `SculptHeights`, so sculpts stay offsets over it, but moving the
  pit changes what a capture subtracts: **capture hand edits before changing that code or the feature**, or the capture
  stores the old pit as a hand edit and the next build cancels the new one.
- Tree-free rectangles and field paint (`compound`, `zone-3`, `farm`, `field-*`) and the checks on them are listed per
  site in `Scenes/STARTING_TOWN.md`.

### NavMesh bake

Bake with `NavMeshAssetManager.StartBakingSurfaces`, on `Terrain_StartingArea` only: `NavMeshSurface.BuildNavMesh()`
keeps the data in memory, so a scene save drops the reference and the next bake writes a second asset named
`... 1.asset`. A second `StartBakingSurfaces` in the same editor session did it too (pass 2): after every bake, list
`Scenes/StartingTown/NavMesh*`. If a `... 1.asset` appeared, the surface now points at it: remove the old file (+ `.meta`)
on disk, refresh, then `AssetDatabase.MoveAsset` the new one onto the old name (a disk rename leaves the binary
asset's internal name stale). The `.meta` GUID then changes in git, which is expected.

- Scene bakes collect render meshes, so an invisible collider never blocks the NavMesh: add a Not Walkable
  `NavMeshModifierVolume` where agents could reach it. Rails, bars and posts too thin for a bake need one too.
- A `MeshRenderer` on `ShadowsOnly` draws only into the shadow map but is collected; a disabled renderer isn't.
- **A script compile during a bake drops it silently**: the domain reload ends the async bake, the asset keeps its old
  time stamp and paths run through whatever was built since. Check `NavMeshAssetManager.IsSurfaceBaking` (or the
  asset's write time) before testing paths.
- Sparse meshes with box colliders (e.g. barricades) leave NavMesh inside them; close it with a Not Walkable volume.

### Hand-painted camp ground

The live terrain carries hand paint in the camp (Road at the centre and elsewhere) that isn't in
`StartingTownTerrain_Source.asset`, so every terrain run repaints the camp from the source asset.

1. Before a run, save the alphamap.
2. Afterwards, put back every sample outside the old and new lines of the paths you changed.
3. The Company Road is hand-painted Road through the centre, so also put back samples within 3.5 m of `company-road`
   when a changed path crosses it, or the tool's Cobble shows there.

Moving that paint into the source asset would end this.

---

## Ownership

Each build tool replaces only the roots it owns; anything else in the scene is hand-owned.

- **Camp Edge** replaces only `Area1_Blockout/CampEdge` (`outer-fence*`, `hill-palisade`, `inner-fence-*`,
  `dock-fence-*`, `compound-fence-*`, `shore-rail`, `ramp-rail`).
- **Sites** replaces `Quarry`, `Fort`, `RampGate`, `Docks`, `BridgeRails`, `Barricades`, `HuntersFire`, the camp
  interior roots, `TimberYard`, `WorkshopYard`, `Garrison`, `Farm`, `CampCentre`, `Walkways`, the remote-site roots
  (`Ruins`, `QuarryDressing`, `Ruins2Dressing`, `WoodCamp`, `HuntingCamp`, `Cave`) and — only when not hand-owned —
  `TentCamp`.
- `Warehouse` is a Sites root whose content is an **instance of the generated warehouse prefab**
  (`Area1SitesBuilder.NewRoot(parent, name, prefab)`), plus `HoistGoods` and `CellarGoods` (the cellar's casks,
  crates and sacks, placed in the root's frame) and `QuayGoods` (the quay's and the barge's, each placed where the
  parts file's `quay.goods` says). Change the model script or
  `WarehouseBuilder`, never the instance; a hand-edit scan compares the prefab's children like any built object.
- `DockGate` is a Sites root too, an **instance of the generated dock gate prefab** seated on the lane's ground at
  the layout's `dock-gate`; without the prefab Sites builds the graybox gate there.
- `GangHouse` is a Sites root holding an **instance of the generated gang house prefab** at the pose its parts
  file gives (which must match the layout's `gang-house`, or Sites builds the graybox house), plus `Goods` (kit
  props placed where the parts file's `goods` says). `GangDock` then holds the boat alone: the deck and the pier
  are the prefab's. `WildernessGate` is a Sites root too, an instance of the generated gateway at the layout's
  optional `wilderness-gate` (a point with a `yaw`); without that prefab no gate is built.
- Sites never touches the hand-built `CampBridge`, `CompanyCliff`, `FightPit` or the root `SM_Barricade_*`. In `Docks`
  it replaces only its own parts, by name (`DeckFloor_*`, `Boat`, `MooringPost`, `MooringRope`; never rebuild
  `Walkway_*`).
- A run replaces its roots whole, with no undo, and hand edits only survive through a capture (**Hand edits**), so move
  any art out of a root and take that root out of the tool before rerunning.
- Hand-owned roots outside the builders (`WorkSiteManagers`, `SealedPassage`, `HiddenStashQuest`, `RestrictedAreas`,
  `NpcPatrolRoutes`, `NpcRoutineSpots`, `ArrivalSequence`, enemies) are never authored by running a builder.
- **Keep `GameObject.Find` out of the builders:** `Run` takes its root, and anything a builder writes outside that root
  would be changed by every scan.

### Camp Edge specifics

- A panel deleted by hand comes back on the next Camp Edge run unless `layout.json` loses it too or a hand-edit capture
  saves it, so prefer changing the layout.
- Each line in `FenceIds` has its own seeded jitter, so changing one leaves the others' heights alone.
- The numbers on `Panel_NNN` count across lines, so adding length to an early line shifts every later name, and saved
  `hand-edits.json` paths must move with them. A new line goes **last** in `FenceIds` for that reason (`gang-fence`).
- A line can be buried less deep than the others (`LineBury`): `gang-fence` panels reach 0.4 m under their lowest
  ground, not 1.5 m, because the smuggling tunnel passes under them (`Prefabs/World/GangHouse/CLAUDE.md` → Tunnel).

### Palisade log walls

Every `Panel_NNN` is still the tool's box: its transform and `BoxCollider` are what the game collides with and what
hand edits pin. Camp Edge only changes what the panel **draws**: after the hand edits are applied, each standing panel
gets a wall of logs assembled from the palisade kit (`Art/Environment/Camp/models/palisade/`, built by
`build_palisade.py`; look and sizes: `Art/Environment/Camp/PALISADE_CONCEPTS.md`).

- **Per panel:** a heavier flat-topped post on its start point, ordinary logs pressed together to the next joint, two
  pole rails on the camp side, a rope lashing where a rail crosses a post. A panel with nothing running on from it
  (a line's end, a gap, a removed neighbour) also gets a closing post.
- **Fitted to the ground:** each log's lowest shoulder (where its point starts; the kit's sizes file gives it per
  log) stands 2 cm above the panel's box top, so the notches between points clear it too. The top line climbs to a
  taller neighbour at a joint; the high rail hangs 0.75 m under the *lower* of the two tops there, so rails meet at
  the joint and never rise above the logs. A log's foot goes 0.7 m under the lowest ground across the wall's thickness
  and never ends above the box's own bottom. The kit holds only the top metre of a log; the shaft is carried down with its U, so the
  grain keeps its scale on a 14 m wall.
- **Read from the panel as it stands**, so a panel raised or removed by hand decides its own logs. Two panels count
  as joined only while their ends are within 5 cm: one moved or turned by hand closes on its own post, and so does
  its old neighbour. A panel moved or resized by hand *after* a run keeps its old mesh, stretched with the box, until
  the next run.
- **Camp side** (rails and lashings) is decided per line from `Area1BlockoutBuilder.CampSide`: the outer ring faces the
  camp, `hill-palisade` the fort, the inner fences the Company yard, the dock and compound spurs the compound. A new
  line that is not in that table faces the camp's middle.
- **At the warehouse** the two clipped panels end without a post, 0.06 m outside the footprint, against the hall's
  round junction logs (`WAREHOUSE_LOG_CLEAR` mirrors `build_warehouse.py`).
- **Meshes** are written in the panel's local space (a panel is a scaled box) and saved as sub-assets of
  `Generated/Palisade.asset`, named `<line>_Panel_NNN`. A mesh already there is refilled, so a rerun on the same ground
  leaves the file and the scene's references byte-identical; one no panel asks for is removed. The file is about 20 MB.
- **The asset is written during the run, the scene is not: save the scene after every Camp Edge run**, and commit or
  revert the two together. A scene on disk that is older than the asset draws meshes made for other panel poses.
- **Without the kit** (no model, no sizes file or one with a size missing or not above 0, a missing
  `M_Camp_PalisadeLog`, `M_Camp_PalisadeFoot` or `M_Camp_Rope`, model and sizes a version apart) the run logs an
  error and leaves the boxes graybox; the saved meshes stay on disk, unreferenced. A hand-edit scan builds boxes only
  and writes no asset (`Run` returns before the log pass when `applyEdits` is off).
- The `ArrivalGate` towers and both rails are still graybox.
- Look test on one line: `Area1BlockoutBuilder.Run(root, true, out problem, "outer-fence-3")` (internal; the other
  lines draw boxes until the next full run, their saved meshes are kept; an unknown line is refused before anything
  is rebuilt).
- Sizes (log, post and rail radii, the head's length, offsets across the wall) live in `build_palisade.py` and reach
  Unity through `Palisade.json`. The assembly's own numbers (bury depth, raggedness, rail heights) are the constants at
  the top of `PalisadeMesh`. Tests: `Tests/EditMode/PalisadeMeshTests` (a stand-in kit, no model needed).

### Broken stretch

`CampEdge/BrokenStretch` (the collapsed 8 m at the layout's `fence-break`) is dressed by `BrokenStretchBuilder` from
`Art/Environment/Camp/models/broken stretch/` (`build_broken_stretch.py` writes `BrokenStretch.fbx` and
`BrokenStretch.json`). Look, layout rules and where the fire shows from: `Art/Environment/Camp/BROKEN_STRETCH_CONCEPTS.md`.

- **The script owns the layout.** The parts file lists every part: its mesh, where it stands in the stretch's own
  frame (x along the wall, z out of the camp), how it meets the ground (`ground`, `surface` = terrain or a camp rock,
  `span` = a log resting on two points, `strut` = a unit pole stretched between two points) and its colliders. Move
  a prop there, run the script (`blender -b -P`), then Camp Edge. `BrokenStretchLayout` is the pure placement.
- **No invisible wall.** Every part big enough to stop a person has capsule or box colliders of its own shape
  (child `Col_N`): logs, stumps, stones, the scaffold's uprights and deck, the hoist's legs, props, trestles, the
  rope line's stakes. The rope itself, the ladder, bearers, boards and chips have none. The player walks through
  the torn-out middle; nothing is on Ignore Raycast. A strut's capsule is sized for its stretched length.
- **Agents stay out of the gap**: `BrokenStretch/NotWalkable` is a strip across it (the file's `notWalkable`). It does
  not cover the work yard, because the walk from the warehouse ramp to the dock crosses the shelf.
- **Walks kept clear** (`Tests/EditMode/BrokenStretchTests` checks every collider's reach against them, in the
  stretch's frame): a 2.5 m walk from the gap to the ramp's foot (boards on the mud mark it), the walk from the
  ramp's foot west to the dock, and the ground east of the ramp that the docks' other entrance crosses. The hoist
  stands west of the gap walk, not over it: its legs lean, and narrow anything under them at head height. The new
  logs lie on the west shore (world x -3..-10, z 62-66), a place chosen by hand: the ramp's foot lands on the shelf
  at world (4, 56.2-59.7), and the east shore is the docks' other entrance. The script holds that group as one
  turned block (`SHORE_AT`, `SHORE_TURN`).
- **A part moved by hand is reset by the next Camp Edge run.** Put the new place in the script, or save it as a hand
  edit (a saved part stops following the ground).
- The log radii in the script are copied from `build_palisade.py`: change both together.
- **Rocks count as ground** only when they are the camp's hand-placed `SM_Rock*` objects outside `Area1_Blockout`
  (`IsCampRock`): a scan builds a second copy of every tool root, so anything else under a ray would differ between
  a scan and a run.
- **It writes no asset**, so a hand-edit scan runs it (without logging). Without the model, a usable parts file (a
  wrong shape anywhere in it, another `version`, or triangle counts that differ from the model's), or a material,
  the run logs an error and builds the old rail, posts and blocker.
- **Ground:** `Tools/World/Sink Area 1 Broken Stretch Ground` lowered the shelf by up to 0.3 m outside the old line
  and painted it Mud, and wrote `docs/World/Areas/area-001/broken-stretch-ground.json`: while that file exists the
  command only repaints. The hollow is in the captured sculpt, and the terrain tool paints the same patch
  (`BrokenStretchBuilder.ScarWeight` at `ScarCentre`) whenever the layout has `fence-break`, so a terrain run should
  keep both; no terrain run has exercised that yet. Hollow and paint stay where they were sunk (`SunkAt`): if
  `fence-break` moves, Camp Edge warns.
- `hand-edits.json` still holds four entries for the old `Blocker`, `Post_A`, `Post_B` and `Rail`: each run warns that
  they match nothing. Untick them in the Hand Edits window to drop them.

### Settlement hand-owned flag

`settlement-hand-owned.enabled` in `layout.json` and `layout.seed.json` makes full Sites builds skip
`Area1_Blockout/TentCamp` (site facts: `Scenes/STARTING_TOWN.md` → Worker settlement). The scoped Worker Settlement
command refuses to overwrite it. Keep this flag enabled when rebuilding unrelated sites. `hand-edits.json` cannot
preserve added child geometry, so it is not the source for finalized housing. `Area1TentCamp.TryRead` still validates
the layout's tent / fire points and builds `CampCentre`.

---

## Hand edits

`Tools/World/Area 1 Hand Edits` (`Area1HandEditsWindow`) keeps edits made inside tool-owned roots.

- **Scan** builds both tools into a temporary clone of `Area1_Blockout` and lists what differs from the live scene;
  **Save** writes the ticked rows to `docs/World/Areas/area-001/hand-edits.json` and replaces the whole file.
- Sites and Camp Edge reapply it at the end of each run, with `CampEdge/*` going to Camp Edge and the rest to Sites.
- It lives beside `layout.json`, not in it, because the planner exports a fixed field list and would drop it.
- Saved objects are pinned: they keep their stored local transform and stop following the ground themselves, though a
  saved child still moves with its parent, so a later sculpt can leave them floating.
- Rerun the tools after a sculpt or layout change, before you hand-edit, because a scan treats any stale object as
  moved. New height-only rows (position y and scale y, as a panel's height comes from the ground) start unticked for
  that reason, so tick the ones you moved yourself.
- Only transforms and existence are compared: materials, components and a disabled object aren't kept.
- A renamed or reparented object reads as removed plus added, so saving deletes the built original. An object added
  inside a tool root is listed as lost and can't be saved, so move it out of the root.
- Saved edits with no difference in the scene stay listed and ticked: untick them to drop them.
- Each moved entry also stores where the tool built it, because generated names like `Panel_140` count across lines and
  shift with the layout: a run warns when that changes. A removed entry has no such check.
- An edit doesn't reach what was built from it, so pinning `GangHouse/GangDeck` leaves `GangDock` at its built height.
- A part of a prefab instance can't be destroyed, so a removal switches it off.
- A scan can mark the scene modified, because it builds a clone there.
- A broken file stops a run before anything is destroyed, and a path that no longer resolves only logs a warning.
- **Index-named objects are pinned by name, not by what they stand for.** `Barricades/NotWalkable_N` follows the
  scene's root order: removing a hand-placed `SM_Barricade_*` renumbers the rest, and a saved edit then moves
  another barricade's volume. Untick those rows before removing a barricade.

---

## Grading tools

Both live in `Area1FortRamp` and grade the **live** terrain; capture the sculpt after each.

**`Tools/World/Grade Area 1 Fort Ramp`** grades along `path-fort-ramp` (site: `Scenes/STARTING_TOWN.md` → Fort hill),
from its `foot` to `head` heights in the layout.

- Those heights live in the layout because sampling them from the ground shifts them by a few centimetres each pass.
- A heightmap sample's grade comes from its nearest point on the line, so sharp bends tilt the road: keep them rounded.
- The tool only reshapes ground around the current line, so moving it leaves the old cut in the sculpt: smooth that by
  hand first. Then regrade, capture, run Terrain, Camp Edge and Sites, and rebake.

**`Tools/World/Grade Area 1 Goat Track`** grades `path-goat-track` (site: `Scenes/STARTING_TOWN.md` → Quarry and
Ruins 2) from its per-point `heights`.

- Steepest segment 35° max, warns over 40° on any 2 m run; a `drops` segment is left out of both checks (it measures
  33.2°, 33.4° max).
- Paints it in Mud and clears trees, and cuts a pit behind each drop down to the foot height (7 m deep, 12 m wide,
  starting 1.5 m past the ledge) as a clean landing floor.

---

## Climbability

- **A terrain face is never one-way on its own:** a `CharacterController` counts terrain contacts as ground at any
  steepness, so holding jump walks up even the 16 m, ~86° goat-track pit face (measured: y 31 → 71). Only vertical box
  faces stop it.
- `PlayerController` has no slope slide and allows jumps on steep ground, so a player holding jump can still climb any
  cliff; invisible colliders (e.g. `Quarry/RuinsRing`) are what stop that.
- Don't prove a player can't climb with a heightmap flood fill or slope checks: they gave a false pass here. Prove it with
  a `CharacterController` jump-climb run.
- Ground slope, not path grade, decides what the player can climb, so check reachability against surface slope.

---

## One-shot authoring commands

These author hand-owned geometry once; they are not part of the run order.

- **`Tools/World/Rebuild Mafia Quarters`** moves `TentCamp/MafiaZone/MafiaQuarters` to fixed coordinates and replaces
  its children. It refuses when another solid collider stands inside the wall footprint, a coarse check that does not
  cover the roof overhang, the doorstep or `GapBoards`; rerunning it discards hand edits made inside the root.
- **`Tools/World/Author Mafia Buildings And Overlooks`** (`Area1MafiaBlockout.cs`) is a one-time additive authoring
  command with grouped Undo. It refuses if any of its five roots exists and requires both settlement ownership flags.
  Do not remove roots just to rerun it: edit the saved geometry by hand. Its `QUARTERS_*` constants describe the
  original small hut, not the rebuilt hall (`HALL_*`). It never saves or bakes automatically. Navigation uses the
  existing `Terrain_StartingArea` surface and asset-manager bake path.
- **`Tools/World/Sink Area 1 Broken Stretch Ground`** (`BrokenStretchBuilder.SinkGround`) is done: see **Broken
  stretch** → Ground. It refuses outside the Area 1 scene.
- **`Tools/World/Dress Home Yard`** (`HomeYardDresser`) rebuilds `PlayersZone/HomeYardDressing` (every home-yard prop,
  both night lights, the tarp bay, the lean-to, the east yard tarps, the drying line and the mud boards): rerunning asks first, then discards
  hand edits inside it with no undo; rebake afterwards. It refuses unless both huts are unscaled `WorkerHut_Home`
  instances. The east yard tarps hang from huts `worker-hut`, `worker-hut-06` and `worker-hut-05` and the lean-to from
  `worker-hut-02`: run `Replace Graybox Worker Huts` first, or the east yard tarps are skipped with an error (their graybox tarps then stay on) and the lean-to keeps its old graybox tie line with a warning. It doesn't place Hobb's bed (hand-owned `PlayersZone/HobbsBed`). Tarps and ropes are generated by
  `CampClothMesh` and saved under `Art/Environment/Camp/Generated/` (same assets reused on every run). At the end it
  warns for every solid collider in the dressing within 0.7 m of an escort lane (`ArrivalSequence/HutLanes`); the
  card game, trunk and doorstep props from the first pass are listed every run (known, the escort walks round them).

- **`Tools/World/Place Area 1 Canteen`** (`CookhouseBuilder.Place`) edits the hand-owned `TentCamp/Canteen` and other
  hand-owned objects by design (`RestrictedAreas/RA_CookhouseStore`, `NpcRoutineSpots/Spot_Cook_Night`, `Cook`,
  `KitchenHand`, `CookingStation_Fireplace`, `Decor/SM_cartB_a1`, the children of `Camp/Canteen`): it is
  an authoring command, not a builder, so it uses `GameObject.Find`. Rerunnable; it cuts the live terrain on its first
  run (capture the sculpt), never saves and never bakes. What it replaces: `Prefabs/World/Cookhouse/CLAUDE.md`.

- **`Tools/World/Place Warehouse Cellar`** (`WarehouseBuilder.PlaceCellar`) cuts the terrain holes over the cellar
  under the warehouse and over its side room, rebuilds the hand-owned `RestrictedAreas/RA_WarehouseCellar` (the
  warehouse guard's, at all hours, with child boxes for the side room and its passage) and the child boxes
  `RA_Warehouse/TallyRoom` and `RA_Docks/Quay` (each takes its parent's owner and hours), so it uses
  `GameObject.Find`. It refuses without `RA_Warehouse` or `RA_Docks`. Rerunnable; it refuses when the scene's `Warehouse` root stands more
  than 5 cm from where the model was fitted, never saves and never bakes. A hole is not a height: the sculpt
  capture does not hold it, and the terrain tool cuts it again itself. Contract:
  `Prefabs/World/Warehouse/CLAUDE.md` → Cellar.

- **`Tools/World/Place Area 1 Gang House`** (`GangHouseBuilder.Place`) lowers the live terrain under the gang
  house's deck and the lake side of its basement, opens the terrain hole over the basement, deletes the scene
  roots named `SM_Barricade_*` in the old wilderness opening and moves the hand-owned
  `TollGates/TollGate_Wilderness` into the gateway, so it uses `GameObject.Find`. Rerunnable; it refuses when the
  smuggling tunnel no longer meets the opening in the warehouse cellar's side room, never saves and never bakes.
  Contract: `Prefabs/World/GangHouse/CLAUDE.md`.

- **`Tools/World/Replace Graybox Worker Huts`** (`WorkerHutRollout`) edits the hand-owned `TentCamp` by design: it
  deletes each graybox hut it swaps (one undo step, no save, no bake) and never moves one. It measures plots with
  `Physics.OverlapBox` in edit mode, so it calls `Physics.SyncTransforms()` first; inactive objects are not seen.
  Order for the camp: `Build Worker Hut Kit Prefabs` → `Replace Graybox Worker Huts` → `Dress Home Yard` → rebake.

Site facts for all of them: `Scenes/STARTING_TOWN.md` → Mafia buildings / Home yard art pass / Hut rollout.

---

## Play tests

- Set `Application.runInBackground = true` in one call, then test in the next: `Time.frameCount` stays at 2 for the rest
  of the first call.
- Walk the player by `CharacterController.Move` along NavMesh corners, and start inside the opening, because camp props
  snag a simple walker.
- Include run-offs from high ground as well as pushes at the wall.
- Test sealing (cleft blocker) in play mode: edit-mode paths go through the cleft.

---

## Checklist — before / after an Area 1 tool run

1. Capture the terrain sculpt if you sculpted (and before changing `fight-pit` / `FightPit.Offset`).
2. Save the alphamap if the run touches hand-painted camp ground.
3. Move any art out of a tool-owned root (or save it as a hand edit) — the run replaces the root with no undo.
4. Keep `settlement-hand-owned.enabled` on.
5. Run Terrain → Camp Edge → Sites; restore hand paint.
6. Rebake with `NavMeshAssetManager.StartBakingSurfaces` on `Terrain_StartingArea`; recheck spots listed in
   `Scenes/STARTING_TOWN.md` for the sites you moved.
