---
description: Give an NPC a Meshy-generated 3D head — multi-image-to-3D from its portrait pair, trim, landmarks, ear fit, bake onto the canonical head, verify and assign
argument-hint: <NPC or head name(s), e.g. "Cook" or "Cook, Blacksmith"> (empty = pick from the NPCs with a portrait pair)
---

Your task is to give one or more NPCs a **Meshy head** (spec-npc-meshy-heads): a Meshy multi-image-to-3D generation of the
NPC's front + side portraits is used as a **shape + texture source only**, shrinkwrapped and baked onto the canonical
head, so the neck join, skin mask, tones, bake and verify contracts stay those of every other head. Work through the
steps in order and **stop at every HALT**. Several NPCs: run each step for all of them before moving on (generations in
parallel), but never skip a HALT.

**Never** call `generate_image` / OpenRouter, rig the Meshy mesh, or ship it as a mesh.

## Step 0 — Load the rules

Read in full, they are the source of truth (never restate them from memory):
- `.claude/rules/meshy-mcp.md` (cost rules, multi-image head quirks)
- `Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md`: *NPC Meshy heads* (recipe, gotchas), *NPC skin tones*
- the `meshy` section of `Tools/Blender/make_head_variant.py` (`MESHY_DEFAULTS` and the comments under it)
- an existing config to copy: `Tools/Blender/configs/heads/storekeepermeshy.json`

## Step 1 — Pick the NPC(s)

- Candidates need a portrait pair: `Tools/Blender/configs/heads/refs/<name>_front.png` + `<name>_side.png`. An NPC
  without one → run `/characters:portrait-face <NPC>` first for the prompts (stop at its Step 3 images), then come back.
- A head already has a Meshy head if `Tools/Blender/configs/heads/<name>meshy.json` exists: say so and ask whether to redo it.
- Map head → NPC: search `Assets/_Game/Data/NPCs/*/NPC_*.asset` for the head's `HeadAppearance_<Name>.asset` GUID
  (a head can be used by an NPC with another name: the `Tam` head is on `NPC_BeatenWorker`). Read the NPC's `_skinTone`
  (null = untoned).

If `$ARGUMENTS` names NPCs or heads, use them; otherwise ask with `AskUserQuestion` (up to 4 candidates with a pair).

## Step 2 — Cost (HALT)

`meshy_check_balance` (free). Present: one `meshy_multi_image_to_3d` per NPC, `meshy-7.1` + texture `meshy-7`, 4K,
`remove_lighting`, GLB = **30 credits each**, total, balance, and the cap (one regeneration per NPC only if a result is
catastrophically wrong — minor defects are fixed in Blender / config). **HALT** for the user's OK.

## Step 3 — Generate and download

- `meshy_multi_image_to_3d` with `file_paths: [<abs>/refs/<name>_front.png, <abs>/refs/<name>_side.png]` and the settings
  above, `target_formats: ["glb"]`. Several NPCs: send the calls in parallel.
- `meshy_get_task_status` (wait), then `meshy_download_model` (`format: "glb"`, `save_to` a fresh scratchpad folder
  `<scratchpad>/meshy_<name>/model.glb`). Note the task id. Timeout / 5xx on a create → `meshy_list_tasks` before retrying.

## Step 4 — Trim, config, refs, landmarks

1. Blender MCP (`execute_blender_code`; a big GLB can take minutes and move to the background — wait for its notification):
   ```python
   ns = {}; exec(open(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/make_head_variant.py", encoding="utf-8").read(), ns)
   ns["meshy_trim_source"](r"<scratchpad>/meshy_<name>/model.glb",
       r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/sources/meshy_heads/<name>", -0.45, stem="<name>_meshy")
   ```
2. Config `Tools/Blender/configs/heads/<name>meshy.json` (copy `storekeepermeshy.json`): `name` = `<Name>Meshy`,
   `preview_tone` = the NPC tone's colour (omit when untoned), `note` (who uses it, tone, task id, models), `deforms: []`,
   `paint: []`, `meshy` paths → `<name>_meshy`, `exclude: []` (the ear fit comes in Step 5).
3. `ns["render_meshy_refs"](r".../configs/heads/<name>meshy.json")`, then the command it returns:
   ```
   uv run Tools/FaceRef/face_landmarks.py Tools/Blender/configs/heads/refs/<name>_meshy_front.png --front-only --out Tools/Blender/configs/heads/refs/<name>_meshy_landmarks.json
   ```
   Read `refs/<name>_meshy_landmarks_debug_front.png`: landmarks on the eyes, nose, mouth and jaw. A failure (no face)
   → look at the render; a catastrophic generation is the only reason to regenerate (ask first, Step 2 cap).

## Step 5 — Mockup and ear fit (HALT)

1. `ns["run"](r".../<name>meshy.json", export=False)`. Report `head_ratio`, `depth_offset_mm`, `max_shape_mm`,
   `clamped_vertices`, `missed_texels` and warnings. Show `Temp/HeadVariants/<Name>Meshy_meshy.png` and `_side.png`.
2. **Ears:** the side mockup almost always shows the Meshy ear printed on the skull (it sits 15–20 mm lower than the base
   ear). Render the side overlay (aligned Meshy mesh vs base head, ortho 0.36 m at 700 px from +X, camera
   `(2, 0, 1.70)`, 0.514 mm/px; same code as in the session that built TentMate / Tam / Warden: `_meshy_fit` with
   `exclude = []`, mesh from `surface[1]` / `surface[2]`, the two renders blended 50/50). The base ear centre sits at
   about column 345, row 320. Measure the Meshy ear centre, then set in the config:
   - `deforms`: `{"center": [0.087, 0.006, 1.721], "radius": [0.03, 0.08, 0.14], "scale": [1,1,1], "offset": [0, dy, dz], "mirror_x": true}`
     (`dz` = −rows × 0.000514, `dy` = +columns × 0.000514, columns to the right = toward the back);
   - `meshy.exclude`: `{"center": [0.087, 0.006, 1.721], "radius": [0.022, 0.04, 0.06], "inner": 0.6, "mirror_x": true}`.
   Re-run the mockup: the ear must sit on the Meshy ear, with no ghost behind it.
3. Look also at the eyes close-up (`_eyes.png`) and the neck (`_neck.png`). **HALT** with the sheets for approval; loop
   on tweaks (`MESHY_DEFAULTS` overrides in the `meshy` block: `skin_patches` when hair or a mark covers the forehead /
   cheeks, `depth_band_m`, `eye_front_scale`).

## Step 6 — Export, bake, assign

1. `ns["run"](config, export=True, render=False)`.
2. Unity: `refresh_unity(mode="if_dirty", wait_for_ready=true)`, then `execute_code`:
   `try { Game.Editor.Characters.SkinnedHeadVariantBaker.BakeAll(); return "baked"; } catch (System.Exception e) { return "FAIL: " + e.Message; }`
   (not in play mode). If the call returns nothing (MCP reconnect during import), wait for ready and run it again.
3. Assign `Data/Characters/Heads/HeadAppearance_<Name>Meshy.asset` to the NPC's `NPCEntity._head` (SerializedObject on
   `_head`, `AssetDatabase.SaveAssets()`); keep `_skinTone`. Check `git diff` on the NPC asset (an extra default line such
   as `_combatStyle: {fileID: 0}` is Unity serializing a new field: harmless).
4. Tests that list shipped heads: update the NPC's row in `Assets/Tests/EditMode/NpcHeadAppearanceTests.cs` →
   `Assigned` to the new head, and add 1 per new head to the head-material count in
   `NpcSkinToneTests.SkinMaterials_UseSkinShader_BaseAndArmorHaveMasks` (`Assert.AreEqual(<n>, heads, ...)`).

## Step 7 — Verify (HALT)

- Join gap: the posed exposed-join check of `Tools/Blender/verify_head_variants.cs` (5 clips × 8 poses) for the new
  head(s): ≤ 0.02 mm (shipped heads: 0.0002 mm).
- Tones: `Tools/Blender/verify_head_tones.cs` logic with a `PLAN` of old head / new head / new head in Company armor per
  NPC, in its tone ("" = untoned: plain materials). Run the untoned loop **twice and compare only the second pass** (the
  first render of a fresh preview scene can differ): must be 0/255 on every head. A Fair row is a good extra check for
  untoned texels. An IO error writing the PNG → write to a new file name (an open viewer locks it).
- Run the EditMode tests (`run_tests`): all must pass.
- Show the sheet. **HALT** for approval.

## Step 8 — Wrap up

- Docs: add the head to the *Shipped* line of `HEADS_SKIN.md` → *NPC Meshy heads*; record any new gotcha there, and any
  new Meshy behaviour in `.claude/rules/meshy-mcp.md`.
- Byte-identity spot check: re-export one portrait head with `export=["texture","mask"], render=False` and confirm
  `git status` shows no change for it (only if `make_head_variant.py` changed this session).
- Summarize (credits used, ear offsets, checks, files) and offer to commit with `/perso:commit` (scope `npc`, e.g.
  `feat(npc): give the cook a meshy-generated head`). Commit the trimmed sources (`sources/meshy_heads/<name>/`, LFS),
  the refs renders / frame / landmarks, the config, the head folder and assets, the NPC asset and the test change; the
  `_debug_*` overlays are gitignored and the raw GLB stays in the scratchpad.
