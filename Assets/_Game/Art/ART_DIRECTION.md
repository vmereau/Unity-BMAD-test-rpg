# ART_DIRECTION.md — Echoes of the Fallen

> Shared art direction for every visual asset: icons, UI, characters, models, textures, environment.
> Read it **before writing any image / 3D generation prompt or designing an asset**. Prompt
> *templates* live in the folder CLAUDE.md that owns each asset type (index at the bottom); this file
> holds the "why" and the shared vocabulary they are built from.
> Source of truth: `_bmad-output/gdd.md` → *Art and Audio Direction* → *Art Style*. If the GDD
> changes, update this file and then the templates.

---

## Identity in One Paragraph

A **realistic, dark medieval fantasy** world in the spirit of **Gothic 1 & 2**. Grounded, lived-in,
worn and functional — never polished, heroic or glamorous. Atmosphere and believability beat
spectacle: things look like someone made them, used them and repaired them. Magic is rare and
physical (ore, runes, stained glass), not glowing particle effects.

**References:** Gothic 1 & 2 (primary — mood, materials, UI sobriety). Technical quality bar:
Kingdom Come: Deliverance (material realism), A Plague Tale: Innocence (dark atmosphere, lighting).

---

## Pillars

| Pillar | Means | Never |
|--------|-------|-------|
| **Grounded** | Realistic proportions, plausible construction, materials with weight | Oversized weapons, giant pauldrons, cartoon proportions |
| **Worn** | Scratches, rust, stains, frayed edges, patched repairs | Pristine, shiny, factory-new surfaces |
| **Functional** | Every object looks like it has a job; a guard looks practical, a farmer weathered | Decorative excess, gold filigree, gemstones, heraldic bling |
| **Muted** | Earthy, desaturated palette; warm firelight is the only strong accent | Saturated or vibrant colors, neon, rainbow rarity tiers |
| **Diegetic** | Information looks like it belongs to the world (parchment, wood, iron) | Floating icons, glowing quest items, sci-fi / mobile-game UI |

---

## Palette

Muted and earthy. The hex values are **reference swatches** for UI and material tuning, not a locked
palette — stay in their neighbourhood.

| Family | Use | Reference swatches |
|--------|-----|--------------------|
| Earth browns | Wood, leather, soil, UI panels | `#2B2118` `#4A3828` `#6B5440` |
| Stone grays | Rock, iron, walls, neutral UI | `#2E2E2C` `#55534E` `#807C74` |
| Mossy greens | Foliage, moss, cloth dyes | `#3A4230` `#5A6346` |
| Night blues | Night sky, shadows, cold light | `#1E2533` `#34405A` |
| Bone / parchment | Paper, bone, UI text | `#D8CCB0` `#B8A988` |
| Fire / torch | Warm light, embers — the only "hot" accent | `#C27A3A` `#E0A35A` |
| Dull brass | UI selection / highlight, fittings | `#8C7343` |
| Dried blood | Health, danger, illegal actions | `#7A2A22` |

Rules: no pure black or pure white; no saturated hues; accents are small and earned (torchlight,
a selected slot, a health bar).

---

## Materials Vocabulary

Name **concrete materials**, then **form**, then **one or two wear cues**. This is the core prompt rule
for every asset type.

- **Metals:** aged iron, blackened iron, dull steel, rusted iron, tarnished bronze, dull brass.
- **Organics:** dark leather, boiled leather, rough undyed linen, coarse wool, fur, bone, horn.
- **Wood:** weathered wood, dark oak, split planks, pitch-stained timber.
- **Stone / earth:** rough-cut stone, mossy rock, packed dirt, clay, raw ore.
- **Paper:** aged parchment, cracked wax seals, faded ink.
- **Wear cues:** scratched, chipped, rust spots, stained, frayed, patched, soot-darkened, sun-worn.

**Banned words** in any prompt: *magical, legendary, epic, glowing, mystical, ornate, heroic,
shiny, vibrant, sparkle*. They trigger glow, particles and ornament. Express magic with a **physical
cue** instead (dull-blue ore, faded runes scratched into wood, a cracked stone with veins).

---

## Lighting

- **World:** dynamic day/night, dramatic directional sun filtered through canopies; overcast, rain and
  fog shift the mood. Interiors and night are lit by fire and torches (warm vs. cold-blue contrast).
- **Generated 2D assets** (icons, UI): soft warm light from the **upper left**, like torchlight; subtle
  shadows on the object only; no rim light, no lens flare, no bloom.
- **3D generation inputs** (concept sheets for image-to-3D): flat, even, shadowless studio light — the
  mesh needs readable geometry, the mood comes from in-engine lighting.

---

## By Asset Type

### Item icons
Single object, centered, three-quarter view, crisp silhouette, hand-painted realistic look, flat
neutral gray background. Consistency across the set beats individual beauty. Template:
`Art/UI/Items/CLAUDE.md` (`/items:icon-prompt`).

### UI
Sober and diegetic, like Gothic's menus: dark weathered wood / blackened iron frames, aged parchment
and worn leather panels, bone-white readable serif text, dull brass for selection. Minimal and
functional (GDD: *no UI clutter*) — information first, frames thin, no filigree. Never mobile-game
gloss, sci-fi panels or saturated rarity colors. Generated as a small shared set of full-bleed, 9-slice
sprites tinted in Unity — never as screen mockups. Template: `Art/UI/CLAUDE.md` (`/ui:create`).

### Characters
Realistic adult proportions, weathered commoners rather than heroes; outfits close-fitting, layered,
practical, muted and dirty. Role is readable from clothing (guard = functional armor, farmer = linen
and dirt). Template + Meshy pipeline: `Art/Characters/Humanoids/CLAUDE.md` (`/characters:concept-prompt`).

### Props, weapons, 3D models
Plausible real-world construction (a sword has a real crossguard and a wrapped grip), believable scale,
modest polycount suited to a PC prototype. Weapons are tools, not trophies — no oversized blades,
no spikes for show. Pipeline: `/items:create`, `Prefabs/Items/CLAUDE.md`.

### Textures & materials
URP Lit, PBR-plausible values. Roughness high on almost everything (leather, wood, cloth, rust);
metals dull, never mirror-like. Albedo desaturated with grime in crevices; no baked strong lighting
in albedo.

### Environment
History in every detail: ruins, repaired fences, worn paths, soot above hearths. Nature is
overgrown and slightly hostile. Act changes are visible (camps, opened gates, scorched areas).
No floating markers or glowing quest objects — the world itself guides the player.

---

## Writing Any Prompt — Checklist

1. Open with the style anchor: *"… for a dark medieval fantasy RPG in the style of Gothic 1 and 2."*
2. Describe the subject with **materials + form + wear**, never qualities.
3. Restate the palette: *muted earthy palette … no saturated colors, no glow.*
4. Specify the light for that asset type (see *Lighting*).
5. Specify framing / background required by the pipeline (icon gray, concept-sheet gray, UI sprite
   full-bleed with no background).
6. Keep shared template lines **verbatim** — set consistency comes from identical wording.

---

## Template Index

| Asset | Template (single source of truth) | Command |
|-------|-----------------------------------|---------|
| Item icons | `Art/UI/Items/CLAUDE.md` | `/items:icon-prompt` |
| UI sprites (slots, panels, buttons, bars) | `Art/UI/CLAUDE.md` | `/ui:create` |
| Character concept sheets | `Art/Characters/Humanoids/CLAUDE.md` | `/characters:concept-prompt` |
