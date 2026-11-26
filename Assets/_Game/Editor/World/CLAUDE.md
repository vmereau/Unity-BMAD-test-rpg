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
| `Build Area 1 Camp Edge` | `Area1BlockoutBuilder` | `Area1_Blockout/CampEdge` only |
| `Build Area 1 Sites` | `Area1SitesBuilder` (+ `Area1CampInterior`, `Area1TentCamp`, `Area1Stoneworks`, `Area1Timberworks`, `Area1Garrison`, `Area1Farm`, `Area1RemoteSites`, `Area1Worker*`) | The site roots under `Area1_Blockout` (see **Ownership**) |
| `Build Area 1 Worker Settlement` | `Area1WorkerSettlement` | Scoped settlement build; refuses while the settlement is hand-owned |
| `Area 1 Hand Edits` | `Area1HandEditsWindow` / `Area1HandEdits` | `docs/World/Areas/area-001/hand-edits.json` |
| `Grade Area 1 Fort Ramp` / `Grade Area 1 Goat Track` | `Area1FortRamp` | Live terrain heights along one path |
| `Rebuild Mafia Quarters`, `Author Mafia Buildings And Overlooks` | `Area1MafiaBlockout` | One-shot authoring (see below) |
| `Dress Home Yard` | `HomeYardDresser` | `PlayersZone/HomeYardDressing` |
| `Build Worker Hut Kit Prefabs` | `WorkerHutKitBuilder` | `Prefabs/World/Huts/` from the shell models in `Art/Environment/Camp/models/worker hut/` (see the Huts folder's `CLAUDE.md`); rebake afterwards |
| `Capture Warehouse Ground`, `Build Warehouse Prefab` | `WarehouseBuilder` | The ground capture the warehouse model is fitted to, then `Prefabs/World/Warehouse/Warehouse.prefab` from the model and its parts file (pipeline: that folder's `CLAUDE.md`); Sites places the prefab, rebake afterwards |
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
  (`Area1SitesBuilder.NewRoot(parent, name, prefab)`), plus `HoistGoods`. Change the model script or
  `WarehouseBuilder`, never the instance; a hand-edit scan compares the prefab's children like any built object.
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
  `hand-edits.json` paths must move with them.

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
- **`Tools/World/Dress Home Yard`** (`HomeYardDresser`) rebuilds `PlayersZone/HomeYardDressing` (every home-yard prop,
  both night lights, the tarp bay, the lean-to, the east yard tarps, the drying line and the mud boards): rerunning asks first, then discards
  hand edits inside it with no undo; rebake afterwards. It refuses unless both huts are unscaled `WorkerHut_Home`
  instances. The east yard tarps hang from huts `worker-hut`, `worker-hut-06` and `worker-hut-05` and the lean-to from
  `worker-hut-02`: run `Replace Graybox Worker Huts` first, or the east yard tarps are skipped with an error (their graybox tarps then stay on) and the lean-to keeps its old graybox tie line with a warning. It doesn't place Hobb's bed (hand-owned `PlayersZone/HobbsBed`). Tarps and ropes are generated by
  `CampClothMesh` and saved under `Art/Environment/Camp/Generated/` (same assets reused on every run). At the end it
  warns for every solid collider in the dressing within 0.7 m of an escort lane (`ArrivalSequence/HutLanes`); the
  card game, trunk and doorstep props from the first pass are listed every run (known, the escort walks round them).

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
