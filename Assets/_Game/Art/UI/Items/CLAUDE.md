# CLAUDE.md — Art/UI/Items

> Item icon textures used by `ItemSO` icons (inventory, action bar, loot UI). Covers naming and the
> AI-generation prompt template. Use `/items:icon-prompt <item>` to get a ready-to-use prompt.

---

## Naming & Import

- File name: `Item_<Name>_Icon.png` (PascalCase name, e.g. `Item_Lockpick_Icon.png`). Older files
  (`sword.png`, `health potion.png`, `Weapon_Axe_Icon.png`, `Armor_*_Icon.png`) predate this rule.
- Existing icons are ~128 px on a flat neutral gray background — generate at 512×512, then set the
  texture's Max Size to 128 or 256 in the importer. Texture Type: Sprite (2D and UI).

---

## AI Generation (Unity MCP `generate_image`)

- **Test provider:** OpenRouter, model `google/gemini-2.5-flash-image` (Gemini 2.5 Flash Image).
  OpenRouter refuses image output (HTTP 402) when the account balance is under $1.
- Gemini does **not** produce real alpha — asking for transparency yields a painted checkerboard or a
  random flat color. Default to the solid gray background line below (matches existing icons);
  use the transparent line only with a provider/model that outputs alpha.
- Gemini ignores exact `width`/`height`; it returns a square image when the prompt asks for a
  square icon. Resize via the texture importer, not the prompt.

### Call parameters

```
action: generate, provider: openrouter, mode: text,
model: google/gemini-2.5-flash-image,
width: 512, height: 512, transparent: false,
name: Item_<Name>_Icon, output_folder: Assets/_Game/Art/UI/Items
```

Then poll `action: status` with the returned `job_id`.

### Prompt template

Art direction comes from `_bmad-output/gdd.md` → *Art Style* (Gothic 1 & 2, muted earthy palette,
grounded and worn, no glowing items). Keep every line **verbatim** across icons — consistency of
angle, light and wording is what makes the set read as one family. Only `{ITEM}` changes.

```
Square inventory icon for a dark medieval fantasy RPG in the style of Gothic 1 and 2.
Subject: {ITEM}.
Realistic hand-painted look, grounded and functional, worn and used, not heroic or ornate.
Muted earthy palette: aged iron, dark leather, weathered wood, browns, stone grays, mossy greens.
No saturated colors, no glow, no magical sparkle, no lens flare.
Single object, centered, three-quarter view angled diagonally from bottom-left to top-right,
filling about 80% of the frame, crisp readable silhouette.
Soft warm light from the upper left, like torchlight, with subtle shadows on the object only.
{BACKGROUND}
```

`{BACKGROUND}` — pick one:

| Use | Line |
|-----|------|
| Gemini / default | `On a flat solid neutral mid-gray background, no frame, no border, no text, no drop shadow.` |
| Alpha-capable model | `Isolated on a plain transparent background, no frame, no border, no text, no drop shadow.` |

### Writing `{ITEM}`

One phrase: **material + form + one or two wear/detail cues**. Name concrete materials, never
qualities like "magical", "legendary" or "epic" (they trigger glow and ornament). Magic items get a
physical cue instead (dull-blue ore, faded runes scratched into wood).

| Item | `{ITEM}` |
|------|----------|
| Lockpick set | `a small set of two crude iron lockpicks, one hook and one rake, with handles wrapped in dark worn leather` |
| Health potion | `a small rough glass vial stoppered with cork, filled with dark red liquid, a twine loop around the neck` |
| Key | `a heavy blackened iron key with a simple ring bow, slightly rusted` |
| Bread | `a dense round loaf of dark rye bread with a torn crust` |
| Ore | `a jagged chunk of raw dull-blue magic ore embedded in gray rock` |
| Pelt | `a rolled wolf pelt tied with a leather cord` |
| Letter | `a folded parchment letter sealed with cracked dark red wax` |
