# CLAUDE.md — Assets/_Game/Art/Environment/Camp

> Loaded when Claude accesses files in this folder. Worker camp art pass: the Hivemind kit rules (wall modules, roofs,
> scale, kit lights), custom Blender camp pieces and interior density. Hut shells built from it:
> `Prefabs/World/Huts/CLAUDE.md`.

---

## Folder Layout

```
Camp/
├── Concepts/                — image-to-3D input sheets rendered from the Blender models
├── Generated/               — tarp and rope meshes written by `Editor/World/CampClothMesh` (rebuilt by the dresser)
├── Materials/               — `M_Camp_*`: URP Lit materials on kit textures, for custom camp pieces, and
│                              `M_Camp_Hut*`: tuned copies of the kit's wood materials for the hut shells
└── models/<piece name>/     — FBX + model prefab; `Source~/` holds the .blend (Unity ignores `~` folders)
```

Custom pieces so far: `worker bed`, `door flap`, `floor bed` (+ `FloorBed_Model.prefab`), `cloth bundle`
(+ `ClothBundle.prefab`, a separate prop, not part of the bed), `roof pole` (`RoofPole.fbx` 3.7 m along X,
`RoofLashing.fbx` coil round X with the rope end down −Y), `wooden spade`, `drying shirt` (hangs from its origin
down −Y, sleeves along X).

---

## Camp Kit (worker camp art pass)

Rules from the player's home yard beauty corner (`spec-area-1-home-yard-beauty-corner`, `spec-area-1-home-yard-pass-2`).
Targets: `Art/Artworks/Starting area/Camp/H1 home yard day`, `H2 hut interior`, `H3 home yard night`, `H4 worker bed`,
`H5 tarp bay`, `H6 floor bed`, `H7 hut roof poles`.

- **Source.** Hivemind packs under `Assets/Hivemind` (about 2,200 prefabs; their Shader Graphs have URP targets).
  `WorkerHutKitBuilder.Piece(name, parent, position)` instantiates a piece by file name, with its colliders off.
  No ready-made building is small enough: the smallest house is
  8 × 9 m, so huts are assembled from wall modules.
- **Walls.** MedievalFantasyVillage modules, 2.5 m high, pivot at one end, running along +X: `SM_wall2m/3m/4m_a1`
  (plain), `SM_wall3mB/4mB_a1` (dark, framed, braced), `SM_wallDoor2m_a1` (opening 1.05 × 1.95 m), `SM_wallWindow3m_a1`,
  `SM_barnWall*_1m_a1` (2 m high). `SM_planksA_a1` looks like a wall piece by name but is a column of short stacked
  boards: not a wall.
- **Roofs.** `SM_AssembledPlanksA/B/C_a1` (about 2.1 × 2.4 m gappy board panels) as low slopes at 28°, weighted with
  two bark poles per slope, parallel to the ridge and lashed over the gable boards (target
  `Artworks/Starting area/Camp/H7 hut roof poles`); `SM_Damaged_Plank_B_*` as gable boards. The gaps let sun stripes
  into the room. Kit stones (`SM_RockStone_01a/b`) on a roof read as round boulders: don't use them there.
- **One wood tone.** The shell mixes pieces from three packs (MedievalFantasyVillage, BanditVillage, CastleOfEternalMists)
  whose woods differ, and `SM_wallDoor2m_a1` / `SM_wallWindow3m_a1` carry a white paint layer (`M_WoodPaintedWhite_a1`).
  `WorkerHutKitBuilder.WoodRemap` swaps each kit material for an `M_Camp_Hut*` copy (painted white → the plain wood).
  The meshes keep their own textures (their UVs differ); the copies are tuned with the kit shaders' own inputs:
  `S_VertexBlendMaster` `_AlbedoBrightness` / `_L1Saturation`, `S_BasicTextured` `_Tint` / `_Saturation`, `S_General`
  `_BaseMapTint` / `_Desaturate`. The response is not linear: measure, don't guess. Measured in a neutral render of both
  huts, every part sits within 15% of the walls' luminance. Never edit the kit materials themselves: other kit pieces
  use them.
- **Scale.** Several props are oversized for a poor camp: crates are 1.05 m cubes (use 0.45–0.7), buckets 0.55, the
  chopping trunk 0.7. Check bounds before placing.
- **Kit lights.** Some props bring an always-on light with a missing HDRP script (`SM_FireCamp_01b`, `PF_Firecamp01a`),
  which warns on every load. `HomeYardDresser.Prop` unpacks such a prop and removes the light, so it is no longer a
  prefab instance. Add a point light with `ClockNightLight` instead; the values are the constants at the top of
  `Editor/World/HomeYardDresser`, and they can only be judged in Play (runtime ambient comes from Core).
- **Custom pieces.** When the kit has nothing poor enough (bed) or no such shape (hanging cloth), model it in Blender
  at real size, box-project the UVs at 1 m per tile, and assign `M_Camp_*` materials through the importer's material
  remap. Export with −Z forward, Y up and **Apply Transform** (`bake_space_transform=True`): without it the FBX root
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
