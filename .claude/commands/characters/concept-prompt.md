---
description: Build a Gothic-style T-pose character concept prompt (for Meshy image-to-3D) with an optional outfit
argument-hint: <outfit / variation, e.g. "leather armor"> (empty = base hero)
---

Your task is to turn an outfit description into a ready-to-use character concept prompt that follows
the project's template. Do **not** generate the image unless the user asks — just return the prompt.

## Step 1 — Load the template

Read `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md` in full. Its *Prompt template*, slot table,
*Writing `{OUTFIT}`* rules and outfit examples are the single source of truth — never restate the
template from memory.

## Step 2 — Identify the variation

Use `$ARGUMENTS` as the outfit / variation.

- **Empty** → the base hero: use the base `{OUTFIT}` line from the slot table verbatim.
- **Matches an example row** → reuse that `{OUTFIT}` line verbatim.
- **Otherwise** → if an `ArmorSO` exists for it under `Assets/_Game/ScriptableObjects/Items/` (Grep the
  name), read its display name / description for material cues, then write `{OUTFIT}` following the
  rules (close-fitting, clear armpits / legs / hands, no helmet or hood, ends with a neck cue, concrete
  materials and wear only).

If the variation asks for something the rules forbid (cape, long robe, helmet, held weapon), say so in
one line and suggest making it a separate asset instead.

## Step 3 — Same face or new character

Default to **same character** (the base hero): fill `{REFERENCE}` with the reference line and note the
reference image to attach — `Assets/_Game/Art/UI/Items/tests/test hero front view.png` (front) or
`test hero back view.png` (back). Leave `{REFERENCE}` empty only if the user asks for a new character.

## Step 4 — Output

Reply with exactly these sections:

1. **Front prompt** — the full template with `{VIEW}` = front view, `{REFERENCE}` and `{OUTFIT}`
   filled in, in a single fenced code block, ready to paste.
2. **Back prompt** — the same with the back-view `{VIEW}`, in a fenced code block.
3. **Attach** — which reference image(s) to attach for each prompt (image-to-image), or "none".
4. One line reminding the next steps: Meshy image-to-3D → upload the **full zip** to Mixamo → import
   per the *Pipeline* section (offer to do the Unity import).

Then offer to run the generation with `generate_image` (mode `image` with the reference when a
provider is configured).
