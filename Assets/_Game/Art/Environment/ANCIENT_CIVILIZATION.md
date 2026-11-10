# ANCIENT_CIVILIZATION.md — The Vanished Civilization and Its Ruins

> Design language for everything the ancient civilization left behind: architecture, symbols, props.
> Read `Art/ART_DIRECTION.md` first — this file specialises it, it never overrides it.
> Prompt templates and the look-test batch live in `Art/Environment/CLAUDE.md`.
> Lore source of truth: `_bmad-output/narrative-design.md` (*The Ancient Civilization*, *The Ruins*,
> *Visual Storytelling*). Status: **first pass, 2026-11-10 — to be validated by the look test.**

---

## Identity in One Paragraph

**Stone that was never built.** The ruins are made of the territory's own grey rock, but nothing about
them was quarried, cut, carried or stacked. They look as if the bedrock was told what shape to be:
single seamless masses, perfect circles, razor edges, proportions that fit no human body. Nothing is
decorated, nothing depicts anything, and after an unknown span of time nothing has worn down. The ruins
are not decayed — they are **interrupted**: tilted, sunk, sheared, as if whatever ended their makers
moved the ground itself. The dread is quiet. There are no monsters on the walls; there is only the
slow realisation that no one alive could make a single one of these surfaces.

**Mood references (mood only, never copied):** weird-fiction architecture and brutalist mass — the
feeling of a building that is indifferent to its visitor; megalithic sites whose precision outruns
their supposed tools; Lovecraft's *geometry* (wrong angles, wrong scale, deep time), not his creatures.

---

## What Canon Fixes, and What Stays Open

| Fixed by `narrative-design.md` | Deliberately left open here |
|--------------------------------|-----------------------------|
| They mastered magic and were destroyed by magic itself | Their name |
| No one living understands why they vanished | What they looked like — **the architecture never shows a body** |
| Their ruins are the only real source of magical knowledge | What the structures were *for* |
| Ruins are "ancient, silent, and wrong in ways that are hard to name" | How the mountain range contains the magic |
| Factions build on top of the ruins (temporal layering) | What lies at the centre of the great circles (see *Form Vocabulary*) |

Do not answer an open question in an asset. No statues, no thrones, no beds, no tools, no murals: any
of them would tell the player who the makers were. The absence is the design.

---

## The Three Pillars

| Pillar | Means | Never |
|--------|-------|-------|
| **Impossible local stone** | The same grey rock as the surrounding cliffs. One seamless mass: no blocks, joints, mortar or tool marks. The natural strata and veins of the bedrock run unbroken from the cliff *through* the structure | A foreign material, metal, glass, crystal, concrete; visible masonry courses; chisel marks |
| **Pure geometry** | Dread from scale, proportion, precision and blankness. Symbols read as measurement, not decoration | Creatures, eyes, tentacles, faces, idols, figurative reliefs, pictograms, ornament |
| **Intact but displaced** | The stone itself does not erode: edges sharp, circles true. The *site* is tilted, sunk, sheared or swallowed by the land | Crumbling edges, rubble heaps, cracked webs, rounded weathering, missing chunks |

**Reconciling with the "Worn" pillar of `ART_DIRECTION.md`:** wear is the signature of *people and
nature*; the absence of wear is the signature of the ancients. A ruin is a perfect form wearing a coat
of moss, soot, scaffolding and banners. The contrast between the two is the whole effect — so the human
layer on top must be *extra* worn, never cleaned up to match.

### Two tests for any ruin asset

1. **The mason test** — could a skilled mason with iron tools, ropes and time have made this piece?
   If yes, it is wrong. Add scale, remove joints, perfect the curve.
2. **The strip test** — remove the moss, the soil and every human addition. Is what remains
   geometrically exact and undamaged? If not, it is wrong.

---

## Form Vocabulary

Architecture **by subtraction**: a huge simple mass, with perfect voids removed from it. Mass always
dominates void — walls are thicker than the spaces they enclose.

| Form | Use |
|------|-----|
| **Monolith** | Upright slab or block, one piece. The basic unit of every site |
| **Bore** | A perfectly circular hole through a mass — the only kind of "window". Openings are bores or slots, never arches |
| **Slot** | A tall, narrow, parallel-sided opening, 1 : 7. The only kind of "door" |
| **Ring / arc** | Square-section ring, or a segment of one. Always a true circle |
| **Shaft** | Vertical cylinder cut down into the rock; interior spaces are cylinders or slots, never rooms with four walls and a flat ceiling |
| **Disc** | Flat circular platform or floor, flush with or cut into the ground |
| **Ramp** | Constant shallow grade, straight or helical. The ancients' answer to stairs |
| **Cut plane** | A perfectly flat face slicing a natural cliff or boulder — where raw rock simply *stops* being raw |

Rules:

- **Circles are never whole stories.** A curved wall or ring is usually a segment of a far larger
  circle whose centre lies outside the site — under a hill, inside the mountains. Several sites can
  share one centre. Level design may use this as a silent pointer; what is at the centres is left open.
- **The spiral** (canon motif) is reserved: one exact, shallow spiral per major site, as a helical ramp
  or a single floor groove. Never the hand-carved decorative swirls of the old `Ruins.png` concept.
- **Sevenfold division.** Rings, discs and radial marks divide into 7 (or 14). A regular heptagon
  cannot be drawn with compass and straightedge — a scholar NPC would know it, a player just feels the
  symmetry is off. Never 4, 6, 8 or 12.
- **No architectural grammar a human would recognise:** no arches with voussoirs, columns, capitals,
  cornices, bricks, roof pitches, battlements, windows, doors with frames, furniture or fixtures.
- **Edges:** outside edges are razor-sharp arrises. Inside corners are always a clean constant fillet,
  as if the void had been poured rather than cut.
- **Silhouette first:** every surface site has one shape readable from across a valley — a bore
  against the sky, a tilted disc, a ring segment. It is the landmark; no markers are ever needed.

---

## Proportions and Metrics

The base module is **1.2 m**; the signature ratio is **1 : 7**. Sized for Unity (1 unit = 1 m, player
≈ 1.8 m). Nothing is sized for a human, and nothing tells you what it *was* sized for.

| Element | Metric | Why it feels wrong |
|---------|--------|--------------------|
| Slot doorway | 1.2 m wide × 8.4 m tall | Walkable, but the void above your head is five times your height |
| Bore | Ø 4.2 m or Ø 8.4 m, lower lip 0.6–1.2 m above the floor | You climb into an opening that was never a door |
| Wall / mass thickness | ≥ 2.4 m, usually more than the space it encloses | Thresholds are tunnels |
| Monolith | 1.2 × 2.4 × 8.4 m (stele); blocks up to 8.4 m cubes | One piece, unliftable |
| Steps (rare) | Riser 0.6 m, tread 2.4 m | Too tall to walk, too long to stride |
| Ramp | ≤ 10° grade, constant | No landings, no rhythm |
| Inside fillet | Radius 0.3 m everywhere | Same radius at every scale |
| Groove (notation) | 3 cm wide, 1.5 cm deep, square section, constant | No hand keeps a line that even for 40 m |
| Interior height | ≥ 8.4 m; shafts 20 m and more | Torchlight never reaches the ceiling |

**Gameplay:** the player path must stay walkable (slots ≥ 1.2 m, grades ≤ 10°). Where ancient metrics
block a human — 0.6 m risers, a bore lip, a shaft — the route is a **human addition**: timber half-steps,
a plank ramp, a ladder, a rope. Traversal itself tells the layering story.

---

## Surface and Material

- **Colour:** the territory's stone grays (`#55534E`–`#807C74`), slightly cooler and far more uniform
  than quarried rubble. No staining of its own.
- **Finish:** honed and matte, like a river-smoothed pebble that was never wet. High roughness, never
  polished, never mirror-like. No pitting, no chips.
- **Strata:** faint natural bedding lines and mineral veins pass straight through the form, continuous
  with the adjacent cliff. This is the proof that the stone never moved from where it formed.
- **Breaks:** when the stone has parted, it parted along a perfectly flat plane or a true curve. The
  two faces still match exactly. Fragments are large geometric solids, never gravel.
- **The physical cue for magic** (deep sites only): notation grooves filled flush with **dull
  blue-grey ore** (`#34405A` neighbourhood), matte and dark like a seam of slate. It does not shine and
  it does not light anything.

---

## Notation — the Symbol System

The ancients left no pictures and nothing that looks like an alphabet. What they left reads like
**working diagrams**: measurement, not message. This is the "ancient inscriptions" that scholars copy
and that the outside world's entire magical knowledge was reverse-engineered from.

- **Elements:** concentric rings, arcs with deliberate gaps, radial ticks in sevens, straight chords
  across circles, single drilled dots at intersections. Nothing else.
- **Line:** the constant groove (3 cm × 1.5 cm). Never V-cut, never varying, never freehand.
- **Density:** sparse. Nine tenths of every surface is blank. A large empty wall with one ring is
  stronger than a covered one.
- **Placement:** mostly on floors and horizontal faces, around bores and at the foot of slots. Rarely
  at eye level — it was not put there to be read by someone standing.
- **Continuity:** a diagram ignores the object it is on. It runs across separate monoliths, over
  edges and through walls, and resumes on the far side. Where a site has sheared, the lines no longer
  meet — the offset *is* the record of the catastrophe.

| Scale | Size | Where | Game use |
|-------|------|-------|----------|
| Site | 10–100 m | Discs, plazas, cliff cut planes | Landmark, only fully readable from above |
| Threshold | 1–8 m | Around bores, at slots, on shaft walls | Marks importance; puzzle and lock dressing |
| Hand | 20–40 cm | Loose stone discs and plates | The lore-document pickup ("ancient inscription") |

Never: runes, glyph alphabets, celtic knots, eyes, stars, pentagrams, hands, animals, constellations.

---

## Displacement — How a Site Is "Ruined"

Each site uses **one or two** types. The stone is never damaged; its *position* is.

| Type | Look |
|------|------|
| **Tilted** | The whole mass, floor included, rotated 5–20°. Slots lean, ramps run the wrong way, water pools in one corner |
| **Sunk** | Buried to half height or more. Slot tops at ground level, bores turned into pits, a disc under a metre of soil |
| **Sheared** | A clean planar break, the halves offset by one to several metres, faces flat and matching |
| **Swallowed** | Natural rock, scree or old forest closed around it; a cut plane or bore emerging from a raw cliff |
| **Scattered** | Segments of one ring or one monolith lying far apart, obviously one object, each still perfect |

Never floating, rotating or suspended stone — displacement is finished and heavy, not active.

---

## The Layers on Top

Ancient stone does not take a chisel, a nail or mortar. **Nobody has ever modified it** — every later
addition is attached, leaned, wedged, hung or painted on, and all of it is ordinary, worn, on-pillar
Gothic material. This is where `ART_DIRECTION.md` applies at full strength.

| Layer | Adds |
|-------|------|
| **Nature** | Moss and lichen only where soil has gathered: ledges, grooves, the foot of walls, horizontal faces. Vertical faces stay bare. Roots drape over and around, never split. Leaf litter fills bores and slots |
| **The Company** | Timber scaffolds, plank floors inserted at human height inside too-tall voids, canvas roofs, clamped iron brackets, banners, painted survey numbers and inventory marks — the ruin catalogued as property |
| **Rebels / workers** | Hidden use: rope, salvaged planks, charcoal and paint graffiti, sleeping places tucked into bores |
| **The Kingdom** | Standards planted at the most visible silhouette |
| **Scholar exiles** | Chalk annotations beside grooves, string lines and plumb bobs, wooden measuring frames, rubbings on parchment — respectful, obsessive, temporary |
| **Creatures** | Nests of sticks and bone inside bores, smeared pigment, gnawed remains |
| **Dry-stone walling** | Ordinary quarried rubble stacked against ancient faces to close a gap. The lumpy human wall beside the perfect surface is the clearest single statement of the contrast |

---

## Site Tiers

Wrongness escalates with depth: bigger, more regular, quieter, darker, and with less of the upper layers.

| Tier | Where | Ancient content | Upper layers |
|------|-------|-----------------|--------------|
| **1 — Fragment** | Surface, near settlements (starting area ruins, Company camp foundations) | One or two forms: a stele, a ring segment, a sunk slot | Heavy — it is somebody's wall, store or shrine now |
| **2 — Structure** | Wilderness, creature territory | A full composition: disc, shaft, several monoliths, a helical ramp; site-scale notation | Patchy — creatures, an abandoned survey, one scholar |
| **3 — Deep site** | Underground or far interior | Whole interiors, vast shafts, ore-filled notation, no displacement of the stone's surface at all | Almost none — the last human trace is at the threshold |

---

## Light and Sound

- **No built-in light.** No sconces, braziers, niches or windows — nothing suggests the makers needed
  to see. Daylight enters only through bores, as hard-edged discs and shafts moving across blank stone.
  Everything else is the player's torch, and warm firelight on cold perfect stone is the key image.
- **Sound** follows `narrative-design.md`: echoes, long silence, drips, an unnatural quiet. Footsteps
  ring flatter and longer than in a cave; the upper layers (canvas, timber, voices) fall away with depth.

---

## Do / Never

**Do:** one seamless mass · perfect circles · razor edges and filleted insides · 1 : 7 slots · sevenfold
division · strata running through · blank surfaces · sparse constant-width grooves · clean displacement ·
worn human and natural layers on top · one landmark silhouette per site.

**Never:** masonry courses · crumbling or rubble · carved figures of any kind · decorative spirals or
knots · runes or letters · light coming from the stone · crystals · floating stone · metal or machinery ·
human-scale furniture · symmetry in fours and sixes · moss carpeting vertical faces.

---

## Prompt Vocabulary

Image models pull hard toward "mossy fantasy ruin". Word choice does most of the work.

| Use | Avoid — and what it triggers |
|-----|------------------------------|
| *one seamless piece of grey stone*, *monolithic*, *no joints, no blocks, no mortar* | *ruins*, *ancient temple* early in the prompt → crumbling masonry, columns |
| *geometrically perfect circle*, *circular bore*, *razor-sharp edges* | *carved*, *engraved*, *runes* → ornament and glyphs |
| *honed matte surface*, *same rock as the surrounding cliffs* | *alien*, *eldritch*, *Lovecraftian* → tentacles, sci-fi, biomech |
| *thin incised grooves of constant width*, *concentric rings and radial tick marks, like a measuring diagram* | *brutalist*, *concrete* → modern buildings, formwork marks |
| *tilted*, *half-sunk*, *split along one flat plane and offset* | *broken*, *destroyed*, *weathered* → rubble |
| *moss only on ledges and at the base* | *overgrown* → the whole structure turns green |

Always add the negative line from the templates, and never use the banned words of `ART_DIRECTION.md`.
