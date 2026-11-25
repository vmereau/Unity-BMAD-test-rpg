# CLAUDE.md — Assets/_Game/Art/Environment/Camp

> Loaded when Claude accesses files in this folder. Worker camp art pass: the board-built hut shell, the Hivemind kit
> rules (props, scale, kit lights), custom Blender camp pieces and interior density. Hut prefabs:
> `Prefabs/World/Huts/CLAUDE.md`.

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
down −Y, sleeves along X), `worker hut` (the hut shell, below).

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
- **Hut shell (custom, board by board).** `models/worker hut/Source~/build_worker_hut.py` builds both sizes
  (`WorkerHut_Home.fbx`, `WorkerHut_Long.fbx`, about 2,300 triangles each, objects `Walls` and `Roof`) from separate
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
- **Cloth.** Tarps and ropes are tied to different points each time (eaves, poles, stakes), so they are generated, not
  modelled: `CampClothMesh.Sheet(a, b, c, d, sag, seed, patches)` (two-sided, sag, wrinkles, frayed edge, patch
  submesh, 1 m UVs) and `CampClothMesh.Rope(from, to, sag)`, saved under `Generated/` (an existing asset is reused,
  so its GUID and the scene's references survive a rebuild). Tarp canvas `M_Camp_Tarp`, patches `M_Camp_SackCloth`.
- **Floor beds.** A camp bed on the ground (`Bed_FloorPallet`) has no solid collider: the player and the escort walk
  over a mattress, and the model stays out of the NavMesh bake.
- **Density.** A hut interior holds a bed, one crate table with a candle and a bowl, two sacks, straw on the floor
  and one thing on the wall. Hobb's adds a stool, a kettle, a mug, a bucket and kindling. More than that stops
  reading as poor.
