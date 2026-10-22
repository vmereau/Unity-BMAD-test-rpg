---
description: Build Gothic-style prompts for implementable UI sprites (full-bleed, 9-slice ready) for a UI element or screen, with save paths and the Unity import settings
argument-hint: <sprite | element | screen, e.g. "SlotWell" | "action bar" | "inventory"> [state, e.g. "selected"]
---

Your task is to turn a UI element or screen name into ready-to-paste prompts for **implementable UI
sprites** — one full-bleed, 9-slice-ready sprite per prompt that drops into an existing `Image`. Never
produce a screen mockup or screenshot prompt. The user generates the images themselves.

## Step 1 — Load the art direction and the template

Read in full:

1. `Assets/_Game/Art/ART_DIRECTION.md` — palette, *Materials Vocabulary*, banned words.
2. `Assets/_Game/Art/UI/CLAUDE.md` — *Goal*, *Sprite Catalog*, *AI Generation*, *Prompt template*, slot
   table, *Writing `{ELEMENT}`*, *Import Into Unity*, *Screen Catalog*.

These are the single source of truth — never restate the template, the palette or the import values from
memory.

## Step 2 — Resolve the sprites

Parse `$ARGUMENTS`: a sprite, element or screen, then an optional state.
If empty, ask: "Which UI element or screen do you need sprites for (e.g. slot, button, panel, action bar,
inventory)?"

- **Sprite name** (`SlotWell`, `Button`…) → that sprite.
- **Element or screen** → the sprites from the *Screen Catalog* "Sprites" column, or the *Sprite Catalog*
  row whose "Used by" matches (`action bar` → `SlotWell`, `KeyBadge`; `health bar` → `BarTrack`,
  `BarFill`). Order them by how many screens reuse them.
- **State** (`selected`, `active`, `hover`…) → a variant of the base sprite. Default to the *tint* approach
  and say so; write a variant prompt only if the user wants a distinct shape or material change, or the
  catalog lists an optional variant.
- **Not in the catalog** → say so in one line, propose a new catalog row (sprite name, used by, on-screen
  border, Image type) following the same rules, and write its prompt. Add the row to the catalog only once
  the sprite is generated and wired.

Glob `Assets/_Game/Art/UI/Sprites/UI_<Sprite>*.png` for each sprite: an existing base sprite is reused, not
regenerated, unless the user asks (variants then use it as the image-to-image reference).

## Step 3 — Fill the slots (one prompt per sprite)

- `{ELEMENT}` and `{MATERIALS}` — from *Writing `{ELEMENT}`*; adapt the materials only if the user asks for
  a specific look, using the *Materials Vocabulary*.
- `{BORDER}` — framed line, or the `BarFill` line.
- `{STATE}` — empty for a base sprite; the variant line otherwise.

Run the *Writing Any Prompt — Checklist* from `ART_DIRECTION.md` over each prompt and remove any banned
word. Never add a scene, background, items, icons or text to a prompt.

## Step 4 — Output

For each sprite, in catalog order:

1. **`UI_<Sprite>` — Prompt** — the full template with every slot filled, in a fenced code block.
2. **Save as** — `Assets/_Game/Art/UI/Sprites/UI_<Sprite>.png` (or `UI_<Sprite>_<State>.png`; a variant
   says to attach the base sprite as the reference image). The user generates the image in their own tool —
   **never call `generate_image`**.
3. **Import** — one line: Image type, sprite border in source pixels (~1/8 of the source size, 0 for
   `BarFill`), `pixelsPerUnitMultiplier` (source border ÷ on-screen border), and the prefab `Image`(s) it
   goes into.

Then one closing line: existing sprites found (reused / would be overwritten), and an offer to import them
(importer + border), wire them into the listed prefabs and check the result with
`UILayoutCapture.Capture("<Screen>")` once the user says the files are in place.

## Step 5 — Import and wire (when the user says the file is saved)

Follow *Import Into Unity* exactly: read the PNG first and measure the frame's inner edge in source pixels
for the border; set it via `TextureImporter.spriteBorder` and verify `sprite.border`; wire with
`PrefabUtility.LoadPrefabContents` → set `Image` + the script tint fields → `SaveAsPrefabAsset`; check parent
prefabs / `Core.unity` for overrides on those fields; capture the screen preview and read it. Then mark the
sprite ✅ in the *Sprite Catalog* with where it is wired.
