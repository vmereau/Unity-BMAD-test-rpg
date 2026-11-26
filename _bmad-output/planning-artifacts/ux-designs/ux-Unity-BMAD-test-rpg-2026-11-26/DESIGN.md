---
name: Echoes of the Fallen — Inventory Equipment (paper doll)
description: Visual identity of the equipment section of the Inventory screen, redesigned as a paper doll, in the game's Gothic-style art direction.
status: final
created: 2026-11-26
updated: 2026-11-26
scope: Inventory screen, equipment section (left area). Item grid and detail panel are unchanged.
sources:
  - Assets/_Game/Art/ART_DIRECTION.md
  - Assets/_Game/Art/UI/CLAUDE.md
  - Assets/_Game/Art/UI/UI_METRICS.md
  - Assets/_Game/Scripts/UI/Inventory/CLAUDE.md
experience: ./EXPERIENCE.md
colors:
  panel: '#2B2118'
  panel-edge: '#4A3828'
  ink: '#6B5440'
  well: '#171310'
  well-edge: '#55534E'
  silhouette: '#352B21'
  silhouette-edge: '#5E4C38'
  text: '#D8CCB0'
  text-muted: '#807C74'
  empty-label: '#4D4840'
  selected: '#8C7343'
  hover: '#A8835A'
  valid: '#6F7D50'
  valid-hover: '#8A9466'
typography:
  header:
    fontSize: 18px
    letterSpacing: 2px
    note: 'uppercase, colors.text-muted; font family = the current UICanvas TMP font'
  slot-caption:
    fontSize: 17px
    note: 'centred under the slot, colors.text-muted; colors.text when the slot is selected or a valid target'
  empty-label:
    fontSize: 15px
    letterSpacing: 0.5px
    note: 'uppercase, inside an empty slot well, colors.empty-label'
rounded:
  DEFAULT: 0px
spacing:
  panel-x: 40px
  panel-y: 140px
  panel-width: 496px
  panel-height: 800px
  inner-margin: 32px
  slot: 88px
  slot-border: 4px
  slot-icon: '84%'
  column-left-x: 72px
  column-right-x: 416px
  caption-gap: 4px
  silhouette-x: 158px
  silhouette-y: 214px
  silhouette-width: 260px
  silhouette-height: 700px
components:
  equipment-panel:
    sprite: 'UI_Panel (Sliced, on-screen border 16)'
    background: '{colors.panel}'
    edge: '{colors.panel-edge}'
    rect: '{spacing.panel-x}, {spacing.panel-y}, {spacing.panel-width} x {spacing.panel-height}'
  panel-header:
    text: 'Equipment'
    typography: '{typography.header}'
    divider: '2px {colors.ink}, full inner width, 32px under the header top'
  silhouette:
    fill: '{colors.silhouette}'
    edge: '{colors.silhouette-edge}'
    rect: '{spacing.silhouette-x}, {spacing.silhouette-y}, {spacing.silhouette-width} x {spacing.silhouette-height}'
    note: 'one static flat sprite, never changes with gear'
  equipment-slot:
    sprite: 'UI_SlotWell (Sliced, on-screen border 4, pixelsPerUnitMultiplier 32)'
    size: '{spacing.slot}'
    well: '{colors.well}'
    edge-normal: '{colors.well-edge}'
    edge-hover: '{colors.hover}'
    edge-selected: '{colors.selected}'
    edge-valid: '{colors.valid}'
    edge-valid-hover: '{colors.valid-hover}'
  leader-line:
    stroke: '2px dashed {colors.ink}'
    stroke-selected: '3px dashed {colors.selected}'
    stroke-valid: '3px dashed {colors.valid}'
  offhand-ghost:
    icon: 'equipped two-handed weapon icon, 28% opacity, desaturated'
---

# DESIGN.md — Inventory equipment section (paper doll)

> Scope: the left area of the **Inventory** tab (x 0–576, y 140–940 at 1920×1080). The tab bar, item grid
> (576, 140, 768×800) and shared item detail panel (1440, 140, 384×800) keep their current look and layout.
> Behaviour lives in [EXPERIENCE.md](./EXPERIENCE.md). Visual reference:
> [mockups/equipment-paperdoll-states.html](./mockups/equipment-paperdoll-states.html). When a mock and this file disagree,
> this file wins.

## Brand & Style

The equipment section is a **paper doll**: a plain flat silhouette of the hero with a slot for each piece of
gear placed next to the body part it covers, joined to it by a thin dashed ink line. It replaces today's
unlabelled column of slots, which reads like a list rather than gear worn on a body.

It follows `ART_DIRECTION.md`: sober, diegetic and functional, in the spirit of Gothic 1 & 2. It uses
dark weathered-oak and leather panels, blackened-iron slot wells, bone text and dull brass for selection.
The silhouette is a flat shape with no painting, shading or gear on it, so it never competes with the item
icons, which are the only detailed art in the panel. There is no stat summary: the panel shows *what* is
worn, and the detail panel shows *how good* it is.

## Colors

All values sit in the `ART_DIRECTION.md` reference palette (no pure black or white, no saturated hues).

| Token | Hex | Use | Not for |
|-------|-----|-----|---------|
| `panel` / `panel-edge` | `#2B2118` / `#4A3828` | Equipment panel surface and frame (same as the grid and detail panels) | — |
| `ink` | `#6B5440` | Leader lines, header divider | Text |
| `well` / `well-edge` | `#171310` / `#55534E` | Slot well and its resting frame (the shared `SlotWell` look) | — |
| `silhouette` / `silhouette-edge` | `#352B21` / `#5E4C38` | Hero figure fill and outline: a shade above the panel, so it reads without drawing attention | Any slot state |
| `text` | `#D8CCB0` | Captions of the selected or valid slots | — |
| `text-muted` | `#807C74` | Header, resting slot captions | — |
| `empty-label` | `#4D4840` | Slot name inside an empty well | — |
| `selected` | `#8C7343` | Selected slot frame and its leader line (same brass as a selected grid slot) | Hover |
| `hover` | `#A8835A` | Hovered slot frame | — |
| `valid` / `valid-hover` | `#6F7D50` / `#8A9466` | Slots the held / selected item can go into, and the one under the cursor during a drag | Stat polarity text |

Unity tints **multiply** the `SlotWell` sprite (`Art/UI/CLAUDE.md`). The hexes above are the target
on-screen look: the `EquipmentSlotUI` tints follow the `ItemSlotUI` values (normal ≈ 0.85 gray, warm hover,
brass selected) plus one new **valid** tint tuned to read as `valid`.

## Typography

The panel uses the current `UICanvas` TMP font; no new font is introduced. Three roles:

- **Header** "EQUIPMENT": 18 px, uppercase, 2 px letter-spacing, `text-muted`.
- **Slot caption** (Helmet, Armor, Weapon, Ring, Necklace, Offhand, Ring): 17 px, centred under the slot,
  `text-muted`, switching to `text` when its slot is selected or a valid target.
- **Empty label** inside an empty well (HELMET, ARMOR, WEAPON, OFFHAND, NECK, RING): 15 px, uppercase,
  `empty-label`.

## Layout & Spacing

Fixed pixel layout at the 1920×1080 reference (`UICanvas` has no `CanvasScaler`). All positions are
top-left screen pixels.

| Element | Rect |
|---------|------|
| Equipment panel | 40, 140 · 496×800 |
| Header text / divider | 72, 164 / 72, 196 · 432×2 |
| Silhouette | 158, 214 · 260×700 |
| **Left column** (x 72) | Helmet y 200 · Armor y 370 · Weapon y 540 · Ring 1 y 710 |
| **Right column** (x 416) | Necklace y 286 · Offhand y 540 · Ring 2 y 710 |
| Slot | 88×88 (4 px frame, icon 84 % of the slot) |
| Caption | 88 px wide, 4 px under its slot |

Placement follows the body: the helmet by the head, the necklace by the throat, the armor by the chest, the
weapon and offhand at the hands, and each ring below its hand. Each leader line runs from the inner edge of
the slot (x 160 left, x 416 right) to its body part on the silhouette.

The layout holds only today's seven `EquipmentSlot` values. Adding a slot (boots, gloves, belt…) means a
re-layout, done when that happens; no empty placeholder spots are reserved.

## Elevation & Depth

Flat. The panel sits on the screen background like the grid and detail panels. Nothing in the
equipment panel casts a shadow. The only layering is the drag ghost (the existing `ItemSlotUI` ghost) above
everything while dragging.

## Shapes

Square corners everywhere (`rounded.DEFAULT` 0), as with the rest of the UI. Slots are square wells. Leader
lines are straight dashed segments (6 px dash, 5 px gap) with no arrowheads or end dots.

## Components

- **Equipment panel**: `UI_Panel` sprite (catalog sprite, Sliced, on-screen border 16) at the panel rect;
  header + divider at the top.
- **Silhouette**: one static flat `Image` sprite (new asset: a front-facing, arms-slightly-out hero
  outline filled `silhouette`, edge `silhouette-edge`). It shows no gear.
- **Equipment slot**: `UI_SlotWell` at 88×88, Sliced, on-screen border 4 (multiplier 32, as in
  `ItemSlot`). Occupied: item icon at 84 %. Empty: the empty label centred in the well. Frame by state:
  - resting: `well-edge`
  - hover: `hover`
  - selected: `selected`, plus a 3 px darker-brass outer ring
  - valid target: `valid`, plus a 3 px dark-moss outer ring
  - valid target under the cursor during a drag: `valid-hover`
- **Offhand ghost**: when the equipped weapon occupies the offhand, the Offhand slot shows that weapon's
  icon at 28 % opacity, desaturated. Its frame follows the normal states.
- **Leader line**: 2 px dashed `ink`. It takes the slot's colour at 3 px when its slot is selected
  (`selected`) or a valid target (`valid`). In Unity: a thin `Image` with a tiled dash sprite, or a
  solid 2 px `Image` if dashes prove costly to build. Dashes are preferred.

## Do's and Don'ts

- **Do** use the same `SlotWell` sprite and brass selection as the grid: an equipment slot is an inventory
  slot in a different place.
- **Do** keep the silhouette flat and gear-free; the icons carry all the detail.
- **Do** show an empty slot as a dark well with its slot name, never as a blank white box.
- **Don't** add stats, totals or comparisons to the equipment panel; they belong on the detail panel.
- **Don't** use glow, pulsing light, gold filigree or saturated colours for any state (`ART_DIRECTION.md`
  banned words apply).
- **Don't** let a state rely on hue alone. The selected state adds an outer ring and a heavier line, and the
  valid state recolours the caption and line as well as the frame.
