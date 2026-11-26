# UI Metrics — measured layout and screen catalog

> Read when sizing a sprite for a screen, resolving a screen name into the sprites it needs (`/ui:create`), or
> after a layout change. Sprite catalog, prompt template and import recipe: `CLAUDE.md` (this folder).

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
| Inventory | Equipment paper doll | 40, 140, 496×800 | Header 72,164 (font 18) + divider 72,196 432×2; silhouette 158,214 260×700; 7 slots 88×88 (frame 4, icon 84 %, caption 17 under, empty label 15): left x 72 Helmet y200 · Armor 370 · Weapon 540 · Ring1 710, right x 416 Necklace 286 · Offhand 540 · Ring2 710; dashed leader lines 2 px (3 px selected / valid). Measured 2026-11-26 (spec-inventory-equipment-paper-doll) |
| Inventory | Item grid panel | 576, 140, 768×800 | Filter strip 591,154 738×52 (9 tabs 52×52, gap 6); grid 591,216, cells 80×80, gap 5 → 8 columns; Gold footer 591,902 h30 font 24 |
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
| **Inventory** | I | Item grid, item detail panel, equipment paper doll (**7** slots around a silhouette: weapon, offhand, helmet, armor, ring ×2, necklace), context menu, tab bar (`Inventory/`) | `Panel`, `SlotWell`, `Button`, `Tab`, `EquipmentSilhouette` |
| **Container / loot** | E | Two grids + detail with Take / Put (Put hidden for corpse loot) (`Inventory/`) | `Panel`, `SlotWell`, `Button` |
| **Trade** | dialogue | Merchant + player grids, detail with Price / Sells for, Buy / Sell (`Inventory/`) | `Panel`, `SlotWell`, `Button` |
| **Quest log** | J | Started / Completed / Failed tabs, quest list, quest info (`Quest/`) | `Panel`, `Tab`, `Button` |
| **Character** | P | Level, XP, learning points, HP, stamina, base stats (`Screens/`) | `Panel` |
| **Skills** | K | Skill list rows, detail panel with requirement rows (`Skills/`) | `Panel`, `Button`, `SlotWell` |
| **Dialogue** | — | NPC name, response text, Continue / End / Trade button, numbered topics (1–9, 0), choices (`Dialogue/`) | `Panel`, `Button` |
| **Game menu** | Esc | Resume / Save / Load / Options / Quit, save-slot list, confirm dialog (`Screens/`) | `Panel`, `Button` |
| **Death screen** | — | Title, Load last save / Load quicksave / Restart / Quit (`Screens/`) | `Button` |
| **Loading** | — | Black overlay "Loading…" (`Screens/`) | none |
| **Crafting** (cooking / smithing) | E on a station | `CookingUI`: station-materials grid, Experiment (3 ingredient slots, cooking only) / Recipes tabs, recipe rows + item card with Cook / Forge (`Scripts/UI/Crafting/CLAUDE.md`; systems: `Scripts/Crafting/CLAUDE.md`). `UILayoutCapture` key `Crafting` | not catalogued |
| **Main menu** | — | New Game / Continue / Load Game / Quit, shared save-slot list, confirm (`MainMenuUI`, `Scenes/MainMenu.unity`; `Screens/`) | not catalogued |
| **Origin pick** | New Game | One button per origin, artwork, name + details, Begin (`OriginPickUI`; `Screens/`, art `Origins/`) | not catalogued |
| **Options** | Game menu | Music / SFX volume slider rows (`OptionsUI`, Game Menu sub-panel; `Screens/`) | not catalogued |
| **Sleep** | E on a bed | "Day N, HH:MM" header, hour picker (− / +), "Sleep until HH:00 (N h)", Sleep button, fade (`SleepUI`; `Screens/`) | not catalogued |
