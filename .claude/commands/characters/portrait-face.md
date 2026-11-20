---
description: Give an NPC a portrait-matched face — write the front + side portrait prompts, then (once the images exist) fit, project, bake and assign the head
argument-hint: <NPC or head name, e.g. "Cook"> (empty = pick from the NPCs without a portrait face)
---

Your task is to give one NPC its own face with the **portrait-face pipeline** (spec-npc-portrait-faces): the user
generates a front portrait and a side profile from your prompts, then MediaPipe landmarks reshape the head and both
images are projected onto it. Work through the steps in order and **stop at every HALT**.

**Never** call `generate_image` / OpenRouter: give the prompts and the save paths, the user generates the images.

## Step 0 — Load the rules

Read in full, they are the source of truth (never restate them from memory):
- `Assets/_Game/Art/ART_DIRECTION.md` (banned words, character direction)
- `Assets/_Game/Art/Characters/Humanoids/CLAUDE.md`: *Face Portrait Prompt*
- `Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md`: *NPC head variants*, *NPC portrait faces*, *NPC skin tones*
  (recipe, landmarks, gotchas)
- `Tools/Blender/README.md` and `Tools/Blender/configs/heads/refs/PROMPTS.md` (the `{FACE}` lines already used)

## Step 1 — Pick the NPC

Build the candidate list:
- head ↔ NPC asset pairs: the shipped list in `Assets/Tests/EditMode/NpcHeadAppearanceTests.cs` (top of the class);
- a head **already has a portrait face** if `Tools/Blender/configs/heads/<name>.json` has a `"portrait"` block;
- NPCs under `Assets/_Game/Data/NPCs/*/NPC_*.asset` with no `_head` at all are candidates too (they need a new head).

If `$ARGUMENTS` names an NPC or head, use it (if it already has a portrait face, say so and ask whether to redo it).
Otherwise ask with `AskUserQuestion`: up to 4 candidates without a portrait face (the user can type another in
"Other"), each with its current look (config `note`) and tone.

## Step 2 — Describe the face

Gather what the face must say, from:
- the head config `note` and its paint layers (hair colour, beard, scars, marks, which side);
- the NPC's identity / dialogue docs in its `Data/NPCs/<Folder>/` (age, role, temperament, injuries) and the
  `NPCEntity` asset (`_skinTone`);
- whether it wears Company armor (`_startingEquipment`): Company rank-and-file guards keep their neck **brand**
  (`decal` layer), which is outside the projection;
- the faces already done (`PROMPTS.md`): make this one **clearly different** in face structure (jaw, cheekbones, nose,
  eyes, lips, age), not only in hair.

Write the `{FACE}` line by the template's slot rules: age, face build, then hair / beard, then marks; the man's own
left / right; concrete shapes only. The portrait's skin colour should roughly match the NPC's tone. Hair and beard must
stay achievable as paint on the scalp (short hair, cropped beards — no long hair, no hair over the face or ears).

## Step 3 — Prompts (HALT)

Reply with:
1. **Front prompt** and **Side prompt**: the full template from *Face Portrait Prompt*, `{VIEW}` / `{REFERENCE}` /
   `{FACE}` filled, each in one fenced code block ready to paste.
2. **Save as**: `Tools/Blender/configs/heads/refs/<name>_front.png` and `<name>_side.png` (`<name>` = the config file
   name, lowercase), 8-bit PNG, side made image-to-image from the front, face pointing **left** on the side image.
3. One line on what to regenerate for: tilted head, open mouth, strong shadows, hair over the forehead or ears, crop
   touching the edge.

Add the `{FACE}` row to `refs/PROMPTS.md`. **HALT** until the user says the images are in place.

## Step 4 — Landmarks

```
uv run Tools/FaceRef/face_landmarks.py Tools/Blender/configs/heads/refs/<name>_front.png Tools/Blender/configs/heads/refs/<name>_side.png --out Tools/Blender/configs/heads/refs/<name>_landmarks.json
```

Read both `<name>_landmarks_debug_front.png` / `_debug_side.png` overlays: landmarks on the face, the side `nose_tip`
on the nose (not a beard or the chest), `crown` on the top of the head. A tool error (no / several faces, profile
facing right, cropped crown or nose) or a bad overlay → explain which image to regenerate, and HALT.

## Step 5 — Config

Edit (or create, copying the closest existing config) `Tools/Blender/configs/heads/<name>.json`:
- `"portrait": {"landmarks": "Tools/Blender/configs/heads/refs/<name>_landmarks.json"}` (overrides of
  `PORTRAIT_DEFAULTS` only when needed), `"deforms": []`, `preview_tone` = the NPC tone's colour if it has one;
- keep only the paint the portrait doesn't cover: scalp hair, back of the head, sideburns, the Company `decal`; drop
  face-detail layers (iris, brows, freckles, eye bags, face scars, beard fills on the face front);
- scalp hair must match the portrait (colour, hairline height); clear the base hero's own sideburn with a `skin` layer if
  the portrait has none (see `tam.json`);
- a one-sided mark (scar, bruise) on the cheek or jaw → `"side_mirror": false`;
- update `note` with the new look.

## Step 6 — Mockups (HALT)

Blender MCP (`execute_blender_code`):
```python
exec(open(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/make_head_variant.py", encoding="utf-8").read())
result = run(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/configs/heads/<name>.json")
```
Show the user `Temp/HeadVariants/<Name>_portrait.png` (renders beside the portrait) and a front / ¾ / side lineup, and
render the man's **right** side too (the default renders only show his left). Report `max_deform_m`, warnings and the
`portrait` block of the result. Fix obvious problems first (hairline mismatch, base sideburn, colour band at the
forehead or jaw). Remember the Blender lights wash out Fair / Olive tones. **HALT** for approval or tweaks; loop on
tweaks.

## Step 7 — Export, bake, assign

1. `run(config, export=True, render=False)` twice: the PNG hashes must match (reproducible).
2. Unity: `refresh_unity(mode="if_dirty")`, then `execute_code`:
   `try { Game.Editor.Characters.SkinnedHeadVariantBaker.BakeAll(); return "baked"; } catch (System.Exception e) { return "FAIL: " + e.Message; }`
   (not in play mode). A failure → read the message (exact-join / neck UV / bind space), fix, re-export.
3. **New head only:** assign `Data/Characters/Heads/HeadAppearance_<Name>.asset` to the NPC's `NPCEntity._head`, add the
   pair to the shipped lists in `NpcHeadAppearanceTests.cs` / `NpcSkinToneTests.cs` (head count). `Humanoids/HEADS_SKIN.md`
   keeps no per-NPC list (it points to `Data/Characters/Heads/`): don't add one.

## Step 8 — Verify (HALT)

- `Tools/Blender/verify_head_variants.cs` (read the file, pass its contents to `execute_code`): this head's max posed
  join gap must stay ≤ 0.02 mm.
- `Tools/Blender/verify_head_tones.cs` with `PLAN` limited to this NPC (head + tone, armored if it wears Company
  armor; an untoned NPC uses its plain materials): untoned diff must be 0/255. If the PNG write fails with an IO error,
  delete the old sheet first (an open viewer locks it).
- Show the sheet. **HALT** for approval.

## Step 9 — Wrap up

- Run EditMode tests (`run_tests`) — all must pass.
- Docs: remove the NPC from the
  "Portrait faces for the other NPCs" entry in `_bmad-output/implementation-artifacts/deferred-work.md`. Record any new
  gotcha in `HEADS_SKIN.md` → *NPC portrait faces* → *Gotchas*.
- Summarize (deform, checks, files changed) and offer to commit with `/perso:commit`
  (scope `npc`, e.g. `feat(npc): give the cook a portrait-matched face`). Commit the refs (images, landmark JSON,
  PROMPTS.md) with the head; the `_debug_*` overlays are gitignored.
