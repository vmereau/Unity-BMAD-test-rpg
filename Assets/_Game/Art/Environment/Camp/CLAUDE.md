# CLAUDE.md — Assets/_Game/Art/Environment/Camp

> Loaded when Claude accesses files in this folder. Worker camp art pass: the Hivemind kit rules (wall modules, roofs,
> scale, kit lights), custom Blender camp pieces and interior density. Hut shells built from it:
> `Prefabs/World/Huts/CLAUDE.md`.

---

## Folder Layout

```
Camp/
├── Concepts/                — image-to-3D input sheets rendered from the Blender models
├── Materials/               — `M_Camp_*`: URP Lit materials on kit textures, for custom camp pieces
└── models/<piece name>/     — FBX + model prefab; `Source~/` holds the .blend (Unity ignores `~` folders)
```

---

## Camp Kit (worker camp art pass)

Rules from the player's home yard beauty corner (`spec-area-1-home-yard-beauty-corner`).
Targets: `Art/Artworks/Starting area/Camp/H1 home yard day`, `H2 hut interior`, `H3 home yard night`, `H4 worker bed`.

- **Source.** Hivemind packs under `Assets/Hivemind` (about 2,200 prefabs; their Shader Graphs have URP targets).
  `WorkerHutKitBuilder.Piece(name, parent, position)` instantiates a piece by file name, with its colliders off.
  No ready-made building is small enough: the smallest house is
  8 × 9 m, so huts are assembled from wall modules.
- **Walls.** MedievalFantasyVillage modules, 2.5 m high, pivot at one end, running along +X: `SM_wall2m/3m/4m_a1`
  (plain), `SM_wall3mB/4mB_a1` (dark, framed, braced), `SM_wallDoor2m_a1` (opening 1.05 × 1.95 m), `SM_wallWindow3m_a1`,
  `SM_barnWall*_1m_a1` (2 m high). `SM_planksA_a1` looks like a wall piece by name but is a column of short stacked
  boards: not a wall.
- **Roofs.** `SM_AssembledPlanksA/B/C_a1` (about 2.1 × 2.4 m gappy board panels) as low slopes at 28°, weighted with
  `SM_RockStone_01a/b` at 0.3–0.45 scale; `SM_Damaged_Plank_B_*` as gable boards. The gaps let sun stripes into the room.
- **Scale.** Several props are oversized for a poor camp: crates are 1.05 m cubes (use 0.45–0.7), buckets 0.55, the
  chopping trunk 0.7. Check bounds before placing.
- **Kit lights.** Some props bring an always-on light with a missing HDRP script (`SM_FireCamp_01b`, `PF_Firecamp01a`),
  which warns on every load. `HomeYardDresser.Prop` unpacks such a prop and removes the light, so it is no longer a
  prefab instance. Add a point light with `ClockNightLight` instead; the values are the constants at the top of
  `Editor/World/HomeYardDresser`, and they can only be judged in Play (runtime ambient comes from Core).
- **Custom pieces.** When the kit has nothing poor enough (bed) or no such shape (hanging cloth), model it in Blender
  at real size, box-project the UVs at 1 m per tile, and assign `M_Camp_*` materials through the importer's material
  remap. Export with −Z forward, Y up: Blender +X arrives as Unity −X. Render the flat-lit sheet to `Camp/Concepts/`
  for a later Meshy pass.
- **Density.** A hut interior holds a bed, one crate table with a candle and a bowl, two sacks, straw on the floor
  and one thing on the wall. Hobb's adds a stool, a kettle, a mug, a bucket and kindling. More than that stops
  reading as poor.
