# CLAUDE.md — Assets/_Game/Art/Environment/Camp

> Loaded when Claude accesses files in this folder. Worker camp art pass: the board-built hut shell, the Hivemind kit
> rules (props, scale, kit lights), custom Blender camp pieces and interior density. Hut prefabs:
> `Prefabs/World/Huts/CLAUDE.md`. Warehouse look (captures, prompts, build brief): `WAREHOUSE_CONCEPTS.md`; its
> model and prefab pipeline: `Prefabs/World/Warehouse/CLAUDE.md`.
> Palisade look (captures, prompts, build brief): `PALISADE_CONCEPTS.md`.

---

## Folder Layout

```
Camp/
├── Concepts/                — image-to-3D input sheets rendered from the Blender models
├── Generated/               — tarp and rope meshes written by `Editor/World/CampClothMesh` (rebuilt by the dresser)
├── Materials/               — `M_Camp_*`: URP Lit materials on kit textures, for custom camp pieces and the hut shell
└── models/<piece name>/     — FBX + model prefab; `Source~/` holds the .blend (Unity ignores `~` folders)
```

Custom pieces so far: `worker bed`, `door flap`, `floor bed` (+ `FloorBed_Model.prefab`), `cloth bundle`
(+ `ClothBundle.prefab`, a separate prop, not part of the bed), `roof pole` (`RoofPole.fbx` 3.7 m along X,
`RoofLashing.fbx` coil round X with the rope end down −Y), `wooden spade`, `drying shirt` (hangs from its origin
down −Y, sleeves along X), `worker hut` (the hut shell, below), `warehouse` (the Area 1 hall with its ramps,
landing, hoist and rubble: one FBX, a parts file and a ground capture; see `Prefabs/World/Warehouse/CLAUDE.md`),
`palisade` (a kit, not a wall: six log heads, two posts, a rail and a rope lashing, with `Palisade.json`; the Camp
Edge tool assembles them, see `Editor/World/CLAUDE.md` → Palisade log walls).

---

## Camp Kit (worker camp art pass)

Rules from the player's home yard beauty corner (`spec-area-1-home-yard-beauty-corner`, `spec-area-1-home-yard-pass-2`,
`spec-area-1-worker-hut-board-shell`).
Targets: `Art/Artworks/Starting area/Camp/H1 home yard day`, `H2 hut interior`, `H3 home yard night`, `H4 worker bed`,
`H5 tarp bay`, `H6 floor bed`, `H7 hut roof poles`.

- **Source.** Hivemind packs under `Assets/Hivemind` (about 2,200 prefabs; their Shader Graphs have URP targets).
  `WorkerHutKitBuilder.Piece(name, parent, position)` instantiates a piece by file name, with its colliders off.
  The kit gives the camp its **props**. It no longer gives the huts: pieces from different packs keep their own
  texture, grain, board width and texel density, and tinting them to one tone (pass 2) still did not read as one
  building. Never edit the kit materials: other kit pieces use them.
- **Hut shell (custom, board by board).** `models/worker hut/Source~/build_worker_hut.py` builds every shell of its
  `VARIANTS` list (`WorkerHut_Home.fbx`, `_Home_B`, `_Home_C`, `WorkerHut_Tight` to `_Tight_D`, `WorkerHut_Long.fbx`:
  a name, a half length, a seed, the doorway's side and the eave overhang; about 2,300 triangles each, objects `Walls` and `Roof`) from separate
  boards: boards inside, posts and plates outside, vertical gable boards, roof boards down the slope with ragged ends
  and up to three wide gaps per slope (sun stripes), a bark ridge log, barge boards. The roof boards' underside is
  2 cm above the eave line (`ROOF_LIFT`). Run it in Blender (`blender -b -P`, or in a session
  `runpy.run_path(path, run_name="__main__")`); it writes the FBX files and `Source~/WorkerHut.blend`. The script is
  the source: the .blend only stores the built objects and materials (no scene: append them to look at them). It
  refuses to export when the session already holds an object called `Walls` or `Roof`. It is written in the
  prefab's Unity frame and its constants mirror `WorkerHutKitBuilder`'s: change a size in both, then run the script,
  `Tools/World/Build Worker Hut Kit Prefabs`, the dresser if a wall moved, and rebake. Target: `Artworks/Starting area/Camp/H7 hut roof poles`.
- **One wood, by construction.** The shell and the roof poles use one texture set, `T_ModularWood` (MedievalFantasyVillage):
  `M_Camp_HutShell` (boards), `M_Camp_HutShellDark` (posts, plates, log) and `M_Camp_PoleWood`, URP Lit. That texture
  holds **ten planks per tile**, stacked in V with a dark seam between them, so it is not box-projected: each board
  is mapped onto one plank (grain along the board, a random shift along it) at 2 m per tile, so a plank is 0.2 m.
  A board wider than that (`MAX_BOARD`), or a box projection, shows a seam along the board. The texture is dark:
  base colours above 1 are intended (boards 1.3, poles 1.2, the frame deliberately darker at 0.95). Keep boards and
  poles within about 10% of each other. The Inspector's colour picker clamps to 1: set these values by script or in
  the `.mat` file.
- **Timbers wider than a plank.** The warehouse's posts and beams (0.26 m) use the same texture: `build_warehouse.py`
  squeezes the mapping across the grain so the piece stays inside one plank (no seam; the grain reads a little
  finer across). Its other materials are also kit textures in own URP Lit materials: `M_Camp_Shingle` and
  `M_Camp_ShinglePale` (the wood texture tinted grey / pale; each shingle is one plank), `M_Camp_TarredWood` (post
  feet), `M_Camp_Rubble` (`T_RockTile`, box-projected at 1 m per tile), `M_Camp_Iron` (`T_Iron` is an **atlas** of
  hinge shapes: every iron face is mapped inside its one plain patch, `IRON_UV`).
- **Round logs.** A trunk is wrapped onto two planks of the same texture (three for a post), going there and back
  round it, so the wrap closes without a seam and half a 0.3 m trunk's girth covers about its true width of texture.
  The warehouse's junction logs and the palisade (`M_Camp_PalisadeLog` at 1.2, a slightly darker `M_Camp_PalisadeFoot`
  for the lowest 30 cm) are mapped this way. It reads as long drying cracks, not knots.
- **A kit the tool assembles.** Where a piece must follow the ground over hundreds of metres (the palisade), model
  only what has a shape (the top metre of a log, ending in an open ring) and let the tool carry it down: a whole-wall
  model would need a ground capture and a Blender run after every sculpt.
- **Kit roofs elsewhere.** Kit stones (`SM_RockStone_01a/b`) on a roof read as round boulders: weight a roof with
  lashed bark poles instead (`roof pole`), two per slope, parallel to the ridge.
- **Scale.** Several props are oversized for a poor camp: crates are 1.05 m cubes (use 0.45–0.7), buckets 0.55, the
  chopping trunk 0.7. Check bounds before placing.
- **Kit lights.** Some props bring an always-on light with a missing HDRP script (`SM_FireCamp_01b`, `PF_Firecamp01a`),
  which warns on every load. `HomeYardDresser.Prop` unpacks such a prop and removes the light, so it is no longer a
  prefab instance. Add a point light with `ClockNightLight` instead; the values are the constants at the top of
  `Editor/World/HomeYardDresser`, and they can only be judged in Play (runtime ambient comes from Core).
- **Custom pieces.** When the kit has nothing poor enough (bed) or no such shape (hanging cloth), model it in Blender
  at real size, box-project the UVs at 1 m per tile (the hut shell is the exception, above), and assign `M_Camp_*`
  materials through the importer's material remap. Export with −Z forward, Y up and **Apply Transform** (`bake_space_transform=True`): without it the FBX root
  arrives rotated 270° on X, and any tool that sets the model's rotation (`HomeYardDresser.Model`) lays it on its side.
  Blender +X arrives as Unity −X. Render the flat-lit sheet to `Camp/Concepts/` for a later Meshy pass.
- **Tight eaves.** In a packed row use the tight shells (overhang 0.09 m instead of 0.3): the roof then ends 1.69 m
  from the middle, inside the graybox roof's 1.70 m, and no board is drawn broken off short (it would bare the top
  plate). A tight roof still crosses a neighbour standing closer than 3.4 m.
- **Tarps hang from eaves, not poles, where a hut stands near.** `HomeYardDresser.EaveTie` gives the point under a
  shell's eave (just inside the roof's edge, 0.1 m below the boards) and `EaveKnot` the rope's end round the roof's
  edge; a gable end has no eave, so tie to the top plates' ends there. A sheet's edges stay straight between their
  corners (only the middle sags), so an edge tied at eave height stays above the 2.1 m rule.
- **Cloth.** Tarps and ropes are tied to different points each time (eaves, poles, stakes), so they are generated, not
  modelled: `CampClothMesh.Sheet(a, b, c, d, sag, seed, patches)` (two-sided, sag, wrinkles, frayed edge, patch
  submesh, 1 m UVs) and `CampClothMesh.Rope(from, to, sag)`, saved under `Generated/` (an existing asset is reused,
  so its GUID and the scene's references survive a rebuild). Tarp canvas `M_Camp_Tarp`, patches `M_Camp_SackCloth`.
- **Floor beds.** A camp bed on the ground (`Bed_FloorPallet`) has no solid collider: the player and the escort walk
  over a mattress, and the model stays out of the NavMesh bake.
- **Density.** A hut interior holds a bed, one crate table with a candle and a bowl, two sacks, straw on the floor
  and one thing on the wall. Hobb's adds a stool, a kettle, a mug, a bucket and kindling. More than that stops
  reading as poor.
