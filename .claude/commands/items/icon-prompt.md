---
description: Build a Gothic-style AI image prompt (and generate_image call) for an item icon
argument-hint: <item description, e.g. "lockpicking set">
---

Your task is to turn an item name into a ready-to-use icon generation prompt that follows the
project's icon template. Do **not** generate the image unless the user asks — just return the prompt.

## Step 1 — Load the template

Read `Assets/_Game/Art/UI/Items/CLAUDE.md` in full. Its *Prompt template*, `{BACKGROUND}` table,
*Writing `{ITEM}`* rules and *Call parameters* are the single source of truth — never restate the
template from memory.

## Step 2 — Identify the item

Use `$ARGUMENTS` as the item. If empty, ask: "Which item do you need an icon prompt for?"

If an `ItemSO` asset for it exists under `Assets/_Game/ScriptableObjects/Items/` (Grep the item
name), read its display name / description and use them for detail cues.

## Step 3 — Write `{ITEM}`

Follow the *Writing `{ITEM}`* rules: one phrase, concrete materials + form + one or two wear cues,
no "magical / legendary / epic" wording. If the examples table already covers the item, reuse
that line verbatim.

Derive `<Name>` in PascalCase for the asset name (`lockpicking set` → `Lockpick`, keeping it short
and matching any existing `ItemSO` name).

## Step 4 — Output

Reply with exactly these sections:

1. **Prompt** — the full template with `{ITEM}` filled in and the Gemini/default `{BACKGROUND}` line
   (unless the user names an alpha-capable model), in a single fenced code block, ready to paste.
2. **Call** — the `generate_image` parameters from *Call parameters* with `name` filled in
   (`Item_<Name>_Icon`), in a fenced code block.
3. One line noting if an icon with that name already exists in `Assets/_Game/Art/UI/Items/`
   (Glob for it) — generating would overwrite or duplicate it.

Then offer to run the generation.
