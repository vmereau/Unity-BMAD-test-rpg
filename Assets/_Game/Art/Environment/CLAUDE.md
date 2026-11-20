# CLAUDE.md — Assets/_Game/Art/Environment

> Loaded when Claude accesses files in this folder. Environment art: ruins of the ancient civilization; the worker
> camp kit has its own `Camp/CLAUDE.md`.
> **Design language:** `ANCIENT_CIVILIZATION.md` (this folder) — read it, and `Art/ART_DIRECTION.md`,
> before writing any ruins prompt or modelling any ruin piece.
> This file holds the prompt templates and the Meshy prop notes.
> Claude writes prompts and save paths only — the user runs image and Meshy generation.

---

## Topic Files

| File | Read when |
|------|-----------|
| `ANCIENT_CIVILIZATION.md` | Before writing any ruins prompt or modelling any ruin piece (design language) |
| `RUINS_LOOK_TEST.md` | Generating or judging look-test batch 1 (M1–M3, P1–P3), filling the verdict log |

---

## Folder Layout

```
Art/Environment/
├── ANCIENT_CIVILIZATION.md      — design language (pillars, forms, metrics, notation, layers)
├── RUINS_LOOK_TEST.md           — look-test batch 1 prompts + verdict log
└── Camp/                        — worker camp kit (own CLAUDE.md)
Art/Artworks/Ruins/              — mood concepts (full scenes). `Artworks/Starting area/Ruins.png`
                                   is the pre-design concept, kept as the "before"
└── Concepts/                    — ruin piece image-to-3D sheets (one object, off-white background) and their
                                   FBX (`P3 slot gateway.fbx`, hand-repaired `P3 slot gateway repaired.blend` / `.fbx`)
```

---

## Template A — Mood Concept (full scene)

For judging the *feel*. Keep every line verbatim; only the slots change.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
{SCENE}
The structure is one seamless piece of the same grey stone as the surrounding cliffs: no blocks, no joints, no mortar, no tool marks, a honed matte surface, razor-sharp edges and geometrically perfect circles, with the natural strata of the bedrock running unbroken through it.
The stone itself is not eroded, not cracked and not crumbled. {DISPLACEMENT}
A few thin incised grooves of constant width form sparse concentric rings and radial tick marks, like a measuring diagram; most of the stone is blank.
{OVERLAY}
{SCALE}
{LIGHT}
Muted earthy palette: stone grays, mossy greens, earth browns; no saturated colors, no glow, no light coming from the stone, no particles.
No statues, no faces, no creatures, no carved figures, no ornament, no columns, no arches, no bricks, no rubble, no concrete, no metal structures, no text, no frame. Wide 16:9 composition.
```

| Slot | Content |
|------|---------|
| `{SCENE}` | Where we are and the main form(s), using the form names and metrics of `ANCIENT_CIVILIZATION.md` (monolith, bore, slot, ring, shaft, disc, ramp, cut plane) |
| `{DISPLACEMENT}` | One or two displacement types, described physically (tilted / sunk / sheared / swallowed / scattered) |
| `{OVERLAY}` | The worn layers on top: nature first, then at most one faction. Materials + wear, per `ART_DIRECTION.md` |
| `{SCALE}` | One or a few small human figures that make the size readable |
| `{LIGHT}` | Weather and light. Exterior: overcast or low sun. Interior: a hard disc of daylight through a bore, or a single torch |

## Template B — Image-to-3D Input Sheet (one piece)

For Meshy image-to-3D. Written for **geometry**: bare ancient stone only. Moss, soil, timber and
banners are separate assets or in-engine dressing — baked in, they become lumps on the mesh.

```
Single isolated object, three-quarter view from slightly above, reference image for a 3D model, for a dark medieval fantasy RPG in the style of Gothic 1 and 2.
{OBJECT}
Made of one seamless piece of matte grey stone with faint natural strata lines: no blocks, no joints, no mortar, no tool marks, no cracks, no chips, no erosion. Razor-sharp outside edges, smooth rounded inside corners, perfectly flat faces and geometrically perfect circles.
{NOTATION}
Clean bare stone: no moss, no plants, no dirt, no ground, no people.
Muted cool stone gray, no saturated colors, no glow.
Flat, even, shadowless studio lighting, no rim light, no dramatic shadows.
Centered, the entire object visible with margin around it, orthographic-like camera with no perspective distortion.
Plain solid off-white background, no floor, no floor shadow, no text, no frame.
```

| Slot | Content |
|------|---------|
| `{OBJECT}` | One form, with proportions stated as ratios (image models ignore metres) |
| `{NOTATION}` | Empty line, or one sentence placing a single groove diagram on one face |

The background is **off-white**, not the light gray of the character template — the stone is gray and
must separate from it.

---

## Meshy Props — Notes for Ruin Pieces

- Image-to-3D, download the full zip (FBX + textures). No Mixamo step: these are static props.
- **Scale:** Meshy output has no reliable size. Scale in Unity to the metric in
  `ANCIENT_CIVILIZATION.md` → *Proportions and Metrics* (module 1.2 m).
- **What to judge** — the design depends on exactness, and image-to-3D tends to soften it. On each
  result check: are the flat faces flat, the edges sharp, the circle round, the surface free of
  invented cracks and blocks? Record the answer in the verdict log (`RUINS_LOOK_TEST.md`).
- **Fallback if Meshy cannot hold the geometry:** the forms are simple solids — model the piece exactly
  (Blender or ProBuilder, both reachable by MCP) and use the generated image / Meshy texture only for
  the stone material. Decide after the first three pieces, not before.
- Unity import of validated pieces (prefabs, colliders, materials) is a follow-up spec — nothing in
  this folder is wired into a scene yet.
