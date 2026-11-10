# CLAUDE.md — Assets/_Game/Art/Environment

> Environment art: ruins of the ancient civilization (first family documented here).
> **Design language:** `ANCIENT_CIVILIZATION.md` (this folder) — read it, and `Art/ART_DIRECTION.md`,
> before writing any ruins prompt or modelling any ruin piece.
> This file holds the prompt templates, the Meshy prop notes and the look-test batch.
> Claude writes prompts and save paths only — the user runs image and Meshy generation.

---

## Folder Layout

```
Art/Environment/
├── ANCIENT_CIVILIZATION.md      — design language (pillars, forms, metrics, notation, layers)
└── Ruins/
    ├── Concepts/                — image-to-3D input sheets (one object, off-white background)
    └── models/<piece name>/     — Meshy zip content (FBX + textures), one folder per piece
Art/Artworks/Ruins/              — mood concepts (full scenes). `Artworks/Starting area/Ruins.png`
                                   is the pre-design concept, kept as the "before"
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
  invented cracks and blocks? Record the answer in the verdict log.
- **Fallback if Meshy cannot hold the geometry:** the forms are simple solids — model the piece exactly
  (Blender or ProBuilder, both reachable by MCP) and use the generated image / Meshy texture only for
  the stone material. Decide after the first three pieces, not before.
- Unity import of validated pieces (prefabs, colliders, materials) is a follow-up spec — nothing in
  this folder is wired into a scene yet.

---

## Look-Test Batch 1 (2026-11-10)

Six generations to answer one question: *does this read as part of the world, yet impossible for
humans?* Generate the three mood concepts first; they are the reference when judging the 3D pieces.

### M1 — The ring on the ridge (exterior landmark, tier 2)

Same viewpoint as `Artworks/Starting area/Ruins.png`, for a direct before / after.
Save as `Assets/_Game/Art/Artworks/Ruins/M1 ring on the ridge.png`.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
View from a muddy labor camp of patched canvas tents and rough timber frames in a pine forest, looking up at a rocky forested ridge. On the ridge stands a colossal upright slab of stone, as tall as ten men, pierced by one enormous circular hole through which the grey sky is visible. Beside it, a segment of a huge square-section stone ring emerges from the hillside like the rim of a buried wheel.
The structure is one seamless piece of the same grey stone as the surrounding cliffs: no blocks, no joints, no mortar, no tool marks, a honed matte surface, razor-sharp edges and geometrically perfect circles, with the natural strata of the bedrock running unbroken through it.
The stone itself is not eroded, not cracked and not crumbled. The whole slab leans noticeably to one side as if the ridge had shifted under it, and the ring segment is half-sunk into the slope.
A few thin incised grooves of constant width form sparse concentric rings and radial tick marks, like a measuring diagram; most of the stone is blank.
Moss and lichen only on the top ledges and at the base, the vertical faces bare and clean; pine trees and scree crowd the foot of the stone. The camp in the foreground is worn and dirty: stained canvas, split logs, mud, a smoking fire pit.
Two tiny human figures stand at the foot of the slab, barely visible, making its size readable.
Overcast sky, low mist in the valley, cold diffuse daylight, the only warm light is the small campfire.
Muted earthy palette: stone grays, mossy greens, earth browns; no saturated colors, no glow, no light coming from the stone, no particles.
No statues, no faces, no creatures, no carved figures, no ornament, no columns, no arches, no bricks, no rubble, no concrete, no metal structures, no text, no frame. Wide 16:9 composition.
```

### M2 — The slot (threshold with Company overlay, tier 1)

Save as `Assets/_Game/Art/Artworks/Ruins/M2 the slot.png`.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Close view of a massive block of grey stone rising from a hillside, with a single doorway cut through it: a slot only as wide as one person but seven times taller than it is wide, with perfectly parallel sides and a wall as thick as a short tunnel. A company of laborers has turned it into a storehouse.
The structure is one seamless piece of the same grey stone as the surrounding cliffs: no blocks, no joints, no mortar, no tool marks, a honed matte surface, razor-sharp edges and geometrically perfect circles, with the natural strata of the bedrock running unbroken through it.
The stone itself is not eroded, not cracked and not crumbled. The block is buried to a third of its height in the slope and tilted a few degrees, so the tall slot leans slightly against the upright timber posts beside it.
A few thin incised grooves of constant width form sparse concentric rings and radial tick marks, like a measuring diagram; most of the stone is blank.
Nothing is fixed into the stone. Rough weathered timber scaffolding and a plank platform are wedged inside the slot at head height, a patched canvas awning hangs from ropes, a faded dull-red cloth banner is draped over the top edge, crates and barrels are stacked at the base, and white painted survey numbers are daubed on the stone. A lumpy dry-stone wall of ordinary rough fieldstones leans against one side of the perfect surface. Moss only at the base and on the top ledge.
A guard in worn leather and a laborer carrying a sack stand in the doorway, small beneath the height of the slot.
Late overcast afternoon, cold flat daylight, one burning torch in an iron holder standing on the ground beside the entrance.
Muted earthy palette: stone grays, mossy greens, earth browns; no saturated colors, no glow, no light coming from the stone, no particles.
No statues, no faces, no creatures, no carved figures, no ornament, no columns, no arches, no bricks, no rubble, no concrete, no metal structures, no text, no frame. Wide 16:9 composition.
```

### M3 — The shaft (deep interior, tier 3)

Save as `Assets/_Game/Art/Artworks/Ruins/M3 the shaft.png`.

```
Environment concept art for a dark medieval fantasy RPG in the style of Gothic 1 and 2, realistic and grounded.
Interior of a vast vertical cylindrical shaft cut straight down into bedrock, wide as a village square and so tall its top is lost in darkness. The floor is a perfectly flat circular disc. A shallow ramp winds once around the wall in an exact helix. High on the wall, a single perfectly circular opening lets in daylight.
The structure is one seamless piece of the same grey stone as the surrounding cliffs: no blocks, no joints, no mortar, no tool marks, a honed matte surface, razor-sharp edges and geometrically perfect circles, with the natural strata of the bedrock running unbroken through it.
The stone itself is not eroded, not cracked and not crumbled. The shaft has split along one perfectly flat plane and one half has dropped the height of a man, so the floor is in two levels and the ring lines on it no longer meet across the step.
A few thin incised grooves of constant width form sparse concentric rings and radial tick marks, like a measuring diagram; most of the stone is blank. On the floor the grooves are filled flush with a dull dark blue-grey ore, matte like slate.
A little dust and a few dead leaves have drifted under the opening; nothing grows here. Beside one groove lie a scholar's abandoned things: a wooden measuring frame, a string line with a plumb bob, chalk marks on the floor, a rolled parchment.
One lone man in rough dark clothes stands on the floor holding a burning torch, tiny against the wall.
A hard-edged disc of cold daylight from the opening falls across the curved wall; the torch gives a small pool of warm light; the rest fades into deep shadow.
Muted earthy palette: stone grays, mossy greens, earth browns; no saturated colors, no glow, no light coming from the stone, no particles.
No statues, no faces, no creatures, no carved figures, no ornament, no columns, no arches, no bricks, no rubble, no concrete, no metal structures, no text, no frame. Wide 16:9 composition.
```

### P1 — Bored stele (simplest piece: flat faces + one circle)

Target size 1.2 × 2.4 × 8.4 m. Save the image as
`Assets/_Game/Art/Environment/Ruins/Concepts/P1 bored stele.png`, the Meshy zip content in
`Assets/_Game/Art/Environment/Ruins/models/bored stele/`.

```
Single isolated object, three-quarter view from slightly above, reference image for a 3D model, for a dark medieval fantasy RPG in the style of Gothic 1 and 2.
A tall upright rectangular stone slab standing on its smallest end, seven times taller than it is thick and twice as wide as it is thick. One perfectly circular hole passes straight through the wide face in the upper third, its diameter three quarters of the slab's width.
Made of one seamless piece of matte grey stone with faint natural strata lines: no blocks, no joints, no mortar, no tool marks, no cracks, no chips, no erosion. Razor-sharp outside edges, smooth rounded inside corners, perfectly flat faces and geometrically perfect circles.

Clean bare stone: no moss, no plants, no dirt, no ground, no people.
Muted cool stone gray, no saturated colors, no glow.
Flat, even, shadowless studio lighting, no rim light, no dramatic shadows.
Centered, the entire object visible with margin around it, orthographic-like camera with no perspective distortion.
Plain solid off-white background, no floor, no floor shadow, no text, no frame.
```

### P2 — Ring segment (curvature + notation)

Target: one seventh of a ring of 8.4 m outer radius, section 1.2 × 1.2 m. Image →
`Assets/_Game/Art/Environment/Ruins/Concepts/P2 ring segment.png`, model →
`Assets/_Game/Art/Environment/Ruins/models/ring segment/`.

```
Single isolated object, three-quarter view from slightly above, reference image for a 3D model, for a dark medieval fantasy RPG in the style of Gothic 1 and 2.
A curved stone beam lying flat: a segment, about one seventh, of a very large circular ring with a square cross-section. The curve is a perfect arc of constant radius, and both ends are cut perfectly flat along the radius of the circle.
Made of one seamless piece of matte grey stone with faint natural strata lines: no blocks, no joints, no mortar, no tool marks, no cracks, no chips, no erosion. Razor-sharp outside edges, smooth rounded inside corners, perfectly flat faces and geometrically perfect circles.
On the top face, one thin incised groove of constant width follows the arc along its whole length, crossed by seven short evenly spaced tick marks.
Clean bare stone: no moss, no plants, no dirt, no ground, no people.
Muted cool stone gray, no saturated colors, no glow.
Flat, even, shadowless studio lighting, no rim light, no dramatic shadows.
Centered, the entire object visible with margin around it, orthographic-like camera with no perspective distortion.
Plain solid off-white background, no floor, no floor shadow, no text, no frame.
```

### P3 — Slot gateway block (kit piece: void, thickness, fillets)

Target: block 4.8 wide × 9.6 tall × 2.4 m deep, slot 1.2 × 8.4 m. Image →
`Assets/_Game/Art/Environment/Ruins/Concepts/P3 slot gateway.png`, model →
`Assets/_Game/Art/Environment/Ruins/models/slot gateway/`.

```
Single isolated object, three-quarter view from slightly above, reference image for a 3D model, for a dark medieval fantasy RPG in the style of Gothic 1 and 2.
A massive plain rectangular stone block, twice as tall as it is wide and half as deep as it is wide, with one doorway passing straight through its centre from front to back: a tall narrow slot with perfectly parallel vertical sides, seven times taller than it is wide, open at the bottom and ending just below the top of the block in a smoothly rounded head.
Made of one seamless piece of matte grey stone with faint natural strata lines: no blocks, no joints, no mortar, no tool marks, no cracks, no chips, no erosion. Razor-sharp outside edges, smooth rounded inside corners, perfectly flat faces and geometrically perfect circles.
On the front face, one thin incised groove of constant width forms a single perfect circle around the upper part of the slot, interrupted by one small gap.
Clean bare stone: no moss, no plants, no dirt, no ground, no people.
Muted cool stone gray, no saturated colors, no glow.
Flat, even, shadowless studio lighting, no rim light, no dramatic shadows.
Centered, the entire object visible with margin around it, orthographic-like camera with no perspective distortion.
Plain solid off-white background, no floor, no floor shadow, no text, no frame.
```

### Verdict log

Fill in after generating. "Tool" = image model or Meshy mode used. A rejected prompt stays in the log
with the reason — it is the evidence for the next template change.

| ID | Tool | Verdict (keep / retry / reject) | What worked | What drifted | Change for next pass |
|----|------|---------------------------------|-------------|--------------|----------------------|
| M1 | | | | | |
| M2 | | | | | |
| M3 | | | | | |
| P1 | | | | | |
| P2 | | | | | |
| P3 | | | | | |

After the log is filled: update the templates and `ANCIENT_CIVILIZATION.md` (drop its "first pass"
status line), then decide Meshy vs. exact modelling for the kit.
