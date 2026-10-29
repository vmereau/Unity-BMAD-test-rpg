# CLAUDE.md — Art/UI

> UI art: AI-generated **UI sprites** (`Sprites/`), layout previews (`Previews/`) and item icons (`Items/`,
> own CLAUDE.md). Covers the sprite catalog, the sprite prompt template, the Unity import recipe and the UI
> metrics. Use `/ui:create <element|screen>` to get ready-to-use prompts. Art direction (palette, materials,
> banned words): `Art/ART_DIRECTION.md`. UI **code** rules: `Scripts/UI/CLAUDE.md`; `UICanvas` hierarchy:
> `Prefabs/UI/CLAUDE.md`.

---

## Goal: Implementable Sprites, Not Screenshots

Every generation produces **one reusable sprite** that drops into an existing `Image` — never a screen
mockup. Tested 2026-10-22: a full-screen restyle (`Mockups/action bar mockup test.png`) comes back as a
painted scene — UI baked into the background, items painted into slots, layout ignored, Gemini watermark —
nothing in it can be sliced or reused.

What makes a sprite implementable:

- **Full-bleed** — the element fills the whole image edge to edge. Gemini has no alpha, so there must be
  no background to cut out; a rectangular full-bleed sprite needs none.
- **9-slice ready** — even border on all four sides, plain uniform centre. One square source then
  stretches to any `RectTransform` (a 64×64 slot, a 420×72 button, a 768×800 panel) — **the aspect ratio of
  the generated image doesn't matter**, only the border.
- **Empty** — no icons, items, text, numbers: content comes from child `Image` / `TMP_Text`.
- **Flat, even light** — Unity tints it (`Image.color`, Button Color Tint) and stretches it; baked highlights
  and shadows break both.
- **Shared** — a small set reused across screens (the slot well serves the action bar, inventory and
  equipment), not per-screen art.

### Who owns what

| Concern | Owner |
|---------|-------|
| Exact sizes, layout, fonts | The **prefab** `RectTransform`s — mirrored in *UI Metrics* |
| Look of each element | The **sprite** (this folder) |
| Border thickness on screen | **Sprite border** (Sprite Editor) + `Image.pixelsPerUnitMultiplier` |
| States (hover / selected / pressed / disabled) | **Unity tint** by default (one sprite); a variant sprite only when the shape must change (Sprite Swap) |

---

## Sprite Catalog

Shared sprites, file `Sprites/UI_<Sprite>.png` (variants `UI_<Sprite>_<State>.png`). "On-screen border" is
the border thickness wanted in game; the import step converts it.

| Sprite | Used by (prefab → `Image`) | In-game sizes | On-screen border | Image type | States |
|--------|---------------------------|---------------|------------------|------------|--------|
| `SlotWell` ✅ | `ActionBarSlot` background, `ItemSlot` background (wired 2026-10-22, border 128/134 src px); `EquipmentSlot` not yet | 64×64, 80×80, 200×64 | 6 (action bar, icon 52), 4 (item slot, icon 72) | Sliced | Tint — `_normalColor` 0.85 gray, `_hoverColor` warm (1, .95, .85), `_selectedColor` brass (1, .82, .5); optional `SlotWell_Selected` |
| `Panel` | Inventory / detail / quest / skills / stats panels, container & trade columns, dialogue band, game-menu window, confirm window | 384×800 … 1100×300 | 16 | Sliced | — |
| `Card` | Interaction prompt card, notification toast, speech bubble | ~147×61, toast rows | 6 | Sliced | — |
| `Button` | `Common/ActionButton` (menus, death screen, confirm, detail actions) | 420×72, 220×72, 160×80 | 8 | Sliced | Tint (`ActionButton` uses Color Tint) |
| `Tab` | `TabBar` buttons, quest-log Started / Completed / Failed | 243×100, 160×80 | 8 | Sliced | Tint; optional `Tab_Active` |
| `KeyBadge` | Interaction prompt `KeyBadge`, action-bar key label backing | 32×32, 20×16 | 4 | Sliced | — |
| `BarTrack` | Health / stamina / XP bar background | 384×22, 384×6 | 3 (XP: 1) | Sliced | — |
| `BarFill` | Health / stamina / XP fill — **one neutral sprite tinted per bar** (dull red, ochre, brass) | 384×22 inset 2 | none | Health: **Filled** horizontal (`fillAmount`); stamina / XP: Simple (scaled by `localScale.x`) | — |

Not 9-slice sprites — **filter glyph icons** `Inventory/UI_Filter_<Name>.png` (All, Weapons, Armor, Potions,
Meals, Ingredients, Crafting, BooksRecipes, Misc): the `Icon` of `InventoryFilterTab` (inset 5 in a 52×52
`SlotWell` tab), imported Sprite, Max Size 128. Bone glyph on the flat panel brown, made by the user from:

```
Square minimalist UI icon for a dark medieval fantasy RPG in the style of Gothic 1 and 2, square image.
Subject: {GLYPH}.
A simple bold flat silhouette emblem, like a mark burned into dark leather: few shapes, thick clean
strokes, no fine detail, no texture noise, readable at 32 pixels.
The emblem is one flat color, pale bone-parchment (#D8CCB0), with a very slightly hand-made edge.
Centered, seen straight-on, filling about 70% of the frame, with an even margin on all sides.
On a flat solid near-black brown background (#2B2118), no gradient, no vignette, no frame, no border.
No text, no letters, no numbers, no shading, no highlights, no glow, no drop shadow, no watermark.
```

`{GLYPH}` = one plain object (e.g. Weapons `a single straight sword pointing upward, with a plain crossguard
and a round pommel`). A new filter tab needs a new glyph in the same wording.

Sprites not worth generating: crosshair (8×8), dividers / hairlines (use a 1-px tinted `Image`), scrollbars
(tinted `Panel` / `Button` at small size).

---

## AI Generation (done by the user, outside Unity)

The user generates sprites in their own image tool from the prompt — Claude does **not** call
`generate_image`. Save the result as `Sprites/UI_<Sprite>.png`. Assume no real alpha (Gemini-class models
paint a background) — hence full-bleed sprites. Ask for a **square** image in the prompt.

State variant: attach the base sprite `Sprites/UI_<Sprite>.png` as the reference image and save as
`Sprites/UI_<Sprite>_<State>.png` — image-to-image from the base keeps the two consistent.

### Prompt template

Keep every unslotted line **verbatim** across sprites — that is what makes the set read as one UI.

```
A single game UI texture tile for a dark medieval fantasy RPG in the style of Gothic 1 and 2, square image.
Subject: {ELEMENT}.
The element fills the entire image edge to edge: its outer border touches all four edges of the image.
No background, no margin, no drop shadow, no scene around it.
Seen perfectly flat and straight-on, orthographic, no perspective, no tilt, perfectly symmetrical,
rectangular outer edge with square corners.
{BORDER}
Materials: {MATERIALS}.
Even, flat, soft lighting with no strong highlights, no cast shadows, no vignette, no glow.
Muted earthy palette: near-black brown, charcoal, aged iron, dark leather, weathered wood, bone and
parchment tones. No saturated colors, no gloss.
No text, no letters, no numbers, no icons, no items, no symbols, no ornaments in the centre, no watermark.
{STATE}
```

| Slot | Fill |
|------|------|
| `{ELEMENT}` | One line from *Writing `{ELEMENT}`* |
| `{BORDER}` | Framed sprites: `A frame of even thickness on all four sides, about one eighth of the image width, with identical corners; inside it a plain, uniform, low-detail centre that can be stretched without visible distortion.` — `BarFill`: `No frame and no border at all: a uniform surface texture that looks the same at the left and right edges, so it can be stretched or cropped horizontally.` |
| `{MATERIALS}` | From `ART_DIRECTION.md` *Materials Vocabulary*, 2–3 materials + one wear cue (per-sprite defaults below) |
| `{STATE}` | Empty for the base sprite. Variant: `Same tile as the reference image, identical shape, materials and proportions; only change: <state change>.` |

### Writing `{ELEMENT}`

Material + form, no quality words, nothing that would end up in the stretched centre.

| Sprite | `{ELEMENT}` | `{MATERIALS}` default |
|--------|-------------|------------------------|
| `SlotWell` | `a square recessed inventory slot: a raised frame around a sunken, dark, plain well` | `blackened iron frame with four small rivets at the corners, dark worn leather well, light scratches` |
| `Panel` | `a large panel background: a narrow frame around a plain dark surface` | `dark weathered oak frame, aged dark leather surface, faint grain and wear` |
| `Card` | `a small information card: a thin frame around a plain dark surface` | `thin blackened iron edge, dark aged parchment surface, soot-darkened` |
| `Button` | `a flat rectangular button plate: a bevelled frame around a plain surface` | `dark weathered wood plate, blackened iron edging, scuffed` |
| `Tab` | `a tab button plate: a frame on all four sides around a plain surface` | `dark leather over wood, stitched edge, worn` |
| `KeyBadge` | `a small square key badge: a thick frame around a plain dark centre` | `tarnished bronze frame, near-black centre, chipped` |
| `BarTrack` | `an empty recessed track for a status bar: a frame around a dark sunken groove` | `blackened iron frame, near-black groove, rust spots` |
| `BarFill` | `a status bar fill texture, light neutral beige-gray so it can be tinted any color` | `brushed, slightly mottled matte paint with faint grain` |

Variant `<state change>` examples: `SlotWell_Selected` → `the frame is dull brass instead of black iron`;
`Tab_Active` → `the surface is slightly lighter and the edge is dull brass`.

---

## Import Into Unity (Unity side)

1. **Texture importer:** Texture Type *Sprite (2D and UI)*, Sprite Mode *Single*, **Mesh Type Full Rect**
   (required for Sliced / Tiled), Max Size **256** (1024 source is overkill for UI), Filter Bilinear,
   Wrap Clamp.
2. **Border — in source-image pixels.** `TextureImporter.spriteBorder` (and the Sprite Editor) use the
   **source** resolution, not the Max-Size-downscaled one: on a 1024 source, measure the frame's inner edge
   (~128) and enter that — entering 32 gives a border of 8 at 256. `BarFill`: 0. Verify with
   `sprite.border` (reported at the imported size: 128 → 32 at 256).
3. **`Image`:** assign the sprite, set the type from the catalog. The importer scales pixels-per-unit with
   the downscale (100 → 25 at 256), so on-screen border = **source border ÷ `pixelsPerUnitMultiplier`** →
   multiplier = `source border / on-screen border` (`SlotWell`: 128 / 6 = 21.33 in `ActionBarSlot`,
   128 / 4 = 32 in `ItemSlot`).
4. **States:** keep the existing tint (`Image.color`, Button Color Tint). Tints **multiply** the sprite, so
   the old near-black placeholder colors (`_normalColor` ≈ 0.15–0.2 gray) hide it — set normal to ~0.85 gray
   (leaves room for a brighter hover) and use warm / brass tints for hover / selected, both on the script
   fields and on the `Image.color` itself. Sprite Swap only for generated variants.
5. **Preview:** `UILayoutCapture.Capture("<Screen>")` → `Previews/UI_<Screen>_Preview.png` to check the
   result in layout without entering play mode.

---

## Layout Tools (`Editor/UI/UILayoutCapture`)

- `UILayoutCapture.Measure("<Screen>", depth)` — the screen's rects at 1920×1080 (x,y top-left, w×h, grid
  cells, font sizes). Use it to refresh *UI Metrics* and to read the in-game size of an element.
- `UILayoutCapture.Capture("<Screen>")` — renders the screen of `UICanvas.prefab` offscreen (edit mode, no
  scene change) to `Previews/UI_<Screen>_Preview.png`; empty item grids get `ItemSlot` placeholders,
  runtime-toggled panels are shown, every `Image` rect is outlined. A **preview of the implemented UI**, not
  a generation input.
- Screens: `HUD`, `Inventory` (filter tabs built via `InventoryFilterBarUI.BuildTabs`, shared `header` hidden), `QuestLog`, `Character`, `Skills`, `Container`, `Trade`, `Crafting` (Recipes
  tab, item card without its runtime-hidden price row), `Dialogue`, `GameMenu`, `DeathScreen`,
  `ConfirmDialog`, `Loading` (`UILayoutCapture.Screens` — update it when the
  hierarchy changes). Menu: `Tools/UI/Capture All Layout Previews`. MCP: `execute_code` →
  `return Game.Editor.UILayoutCapture.Capture("Inventory");`.

---

## Constraints From the Implementation

- **1920×1080 reference, fixed pixel sizes** — `UICanvas` is Screen Space Overlay with no `CanvasScaler`.
- **Item icons are square with a flat mid-gray background baked in** (`Items/CLAUDE.md`) — the `SlotWell`
  centre must read well around a gray square: dark and plain.
- **Stat polarity colors** (`StatPolarity`) and the **illegal (theft) prompt red** are text tints — use the
  palette's muted green / dried-blood red, never saturated.
- Tab bar labels: **"Inventory" · "Quests" · "Stats" · "Skills"** (keys I · J · P · K).

---

## UI Metrics (1920×1080, origin top-left — measured 2026-10-22)

Re-measure with `UILayoutCapture.Measure` after any layout change and update this table.

| Screen | Element | Rect (x, y, w×h) | Notes |
|--------|---------|------------------|-------|
| HUD | Health bar | 768, 928, 384×22 | Bottom-centre stack, 20 % of screen width |
| HUD | Stamina bar | 768, 961, 384×22 | |
| HUD | XP bar | 768, 988, 384×6 | No label |
| HUD | Action bar | 758, 996, 404×64 | 6 slots 64×64, gap 4, icon 52, key label font 10, stack font 12 |
| HUD | Interaction prompt | centre, ~147×61 | Auto-width card above target, key badge 32×32 |
| HUD | Toast stack | full width, y 236, h 100 | Upper third |
| Menus | Tab bar | 460, 31, 1000×100 | 4 tabs 243×100, font 40 |
| Inventory | Equipment column | 92, 140, 200×800 | 6 slots 200×64 stacked |
| Inventory | Item grid panel | 576, 140, 768×800 | Filter strip 591,154 738×52 (9 tabs 52×52, gap 6); grid 591,216, cells 80×80, gap 5 → 8 columns; Gold footer 591,902 h30 font 24 (measured 2026-10-29) |
| Inventory | Item detail | 1440, 140, 384×800 | Header 96 (icon + name + category), price row, action buttons (Drop / Use / Equip) |
| Container / Trade | 3 columns | x 30 / 660 / 1290, y 30, each 600×1020 | Grid · detail · grid; side headers 40 tall font 30; grids 6 columns |
| Quest log | List | 96, 140, 480×800 | Started / Completed / Failed tabs 80 tall |
| Quest log | Info | 672, 140, 1152×800 | Header 160 |
| Character | Panel | 660, 140, 600×800 | Single centred column |
| Skills | Panel | 660, 190, 600×700 | List 220 wide · detail 332 wide |
| Dialogue | Panel | 410, 618, 1100×300 | Bottom band (bottom edge y 918); content column ~1034; name font 28 (torch), text 25 (parchment), advance button 36 tall |
| Game menu | Window | 460, 80, 1000×920 | Centred column of 5 buttons 420×72 |
| Death screen | Column | centred | Title font 96, 4 buttons 420×72, gap 18 |
| Confirm dialog | Window | 600, 380, 720×320 | Message font 32, Yes / No 220×72 |

Typography scale in use: HUD 10–12, body 25–32, headers 30–40, title 96.

---

## Screen Catalog (what exists in code)

Use it to resolve a screen name into the sprites it needs. Elements come from `Scripts/UI/**/CLAUDE.md` —
re-check the owning file before relying on a count.

| Screen | Key | Elements (owning doc) | Sprites |
|--------|-----|-----------------------|---------|
| **HUD** | — | Health / stamina / XP bars, action bar of **6** slots (key labels 1–6, stack counts), interaction prompt card `[E] <verb> <name>`, toast stack, speech bubbles (`HUD/`) | `BarTrack`, `BarFill`, `SlotWell`, `KeyBadge`, `Card` |
| **Inventory** | I | Item grid, item detail panel, equipment column (**6** slots: weapon, helmet, armor, ring ×2, necklace), context menu, tab bar (`Inventory/`) | `Panel`, `SlotWell`, `Button`, `Tab` |
| **Container / loot** | E | Two grids + detail with Take / Put (Put hidden for corpse loot) (`Inventory/`) | `Panel`, `SlotWell`, `Button` |
| **Trade** | dialogue | Merchant + player grids, detail with Price / Sells for, Buy / Sell (`Inventory/`) | `Panel`, `SlotWell`, `Button` |
| **Quest log** | J | Started / Completed / Failed tabs, quest list, quest info (`Quest/`) | `Panel`, `Tab`, `Button` |
| **Character** | P | Level, XP, learning points, HP, stamina, base stats (`Screens/`) | `Panel` |
| **Skills** | K | Skill list rows, detail panel with requirement rows (`Skills/`) | `Panel`, `Button`, `SlotWell` |
| **Dialogue** | — | NPC name, response text, Continue / End / Trade button, numbered topics (1–9, 0), choices (`Dialogue/`) | `Panel`, `Button` |
| **Game menu** | Esc | Resume / Save / Load / Options / Quit, save-slot list, confirm dialog (`Screens/`) | `Panel`, `Button` |
| **Death screen** | — | Title, Load last save / Load quicksave / Restart / Quit (`Screens/`) | `Button` |
| **Loading** | — | Black overlay "Loading…" (`Screens/`) | none |
