# Meshy MCP — Generation Quirks

Read before calling any `mcp__meshy__*` tool that costs credits. Add a line here whenever a generation behaves
unexpectedly. Prompt rules and the per-clip prompt log for combat clips:
`Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md`.

## Before spending

- Present the cost and wait for the user's OK, every time (`meshy_check_balance` is free). Agree a credit cap for
  regenerations up front, and stop at the cap.
- Tasks made in the Meshy web app are **not** visible through the API (`meshy_list_tasks` is empty for them), so
  nothing rigged or generated there can be reused by id.
- After a timeout or 5xx on a create call, check `meshy_list_tasks` before retrying: the task may exist and be charged.
- Save downloads with `save_to` into a fresh scratchpad folder, then copy what is kept into the project.

## Text-to-motion (`meshy_text_to_motion`)

- `prime` = 10 credits, FBX. `swift` = 3 credits, BVH. Finishes in 10–30 s. Clips expire after 3 days: download at once.
- Hard limits: prompt **400 characters** (the call is rejected above, count before sending), duration 2–10 s in 0.5 s steps.
- Output FBX (~16 MB): **SMPL-H skeleton**, not Mixamo names — `Pelvis`, `L_Hip` (thigh), `L_Knee` (shin), `L_Ankle`
  (foot), `L_Foot` (toes), `Spine1–3`, `L_Collar`, `L_Shoulder` (upper arm), `L_Elbow`, `L_Wrist` (hand). Armature
  `Reference` at scale 0.01, mannequin meshes parented to it, action `Reference|SMPLH_Animation|Base Layer`, 60 frames at
  30 fps for 2 s. In Blender the character faces **−Y**, its left is **+X**.
- **Distance words are ignored.** "one short step", "about 0.3 m", "tiny shuffle step, half a foot length" gave 0.65 m,
  1.22 m and 0.9 m of travel. Do not regenerate to fix a distance: scale the stride in Blender.
- **Direction drifts.** "Straight ahead" still travels 23–45° toward the lead-foot side, with the hips yawing 30–45°.
  Plan to straighten the travel in Blender.
- **"Returns to the guard" / "ends in the starting pose" makes the character step back** to the start (net travel ≈ 0).
  For a clip that must end ahead of its start, describe the rear foot following and "finishes one step ahead".
- "Shuffle", "slides" and "tiny" pushed the body into a deep crouch (pelvis 0.85 → 0.59 m) and dropped the punch to
  waist height. Plain verbs ("steps", "jabs at head height") kept the upper body upright.
- The action starts slowly (first ~10 frames barely move) and the clip can end mid-motion (a foot lifting again):
  expect to trim both ends and retime.
- Measure before judging: import in a new Blender scene and sample pelvis, ankles and wrists per frame. Three clips
  that read the same in the prompt differed by a factor of 10 in net travel.
- **A described arm path is not a punch.** "Throws a horizontal hook … across the front" and "drops to waist height then
  drives up" gave the step and no strike. "Throws a fast right hook punch" / "a fast left uppercut punch: …" in the same
  sentence as the lead step gave both. Check the wrist relative to the pelvis and its speed (the kept punches: 5.8 and 6.7 m/s).
  Prompt wording: `MESHY_PROMPTS.md` → *Clips that travel*.
- **The guard is different in every generation** (stance 0.26–0.55 m long, 0.05–0.48 m wide for the same opening words),
  so clips meant to chain need their starts matched in Blender (`Tools/Blender/meshy_motion_fit.py`, `start_match`).
- Two create calls sent in parallel both run; each is charged.

## Multi-image to 3D (`meshy_multi_image_to_3d`) — NPC heads

- Used for NPC heads from the existing portrait pair (`refs/<name>_front.png` + `_side.png`, local `file_paths`):
  `meshy-7.1` + texture `meshy-7`, 4K, `remove_lighting`, GLB = 30 credits, done in ~90 s. Pipeline:
  `Assets/_Game/Art/Characters/Humanoids/HEADS_SKIN.md` → *NPC Meshy heads*.
- From a head-and-shoulders front + side profile it still builds the **whole head**: back of the skull, both ears, hair
  volume and the shoulders, with a delit texture close to the portrait (identity kept).
- Output GLB 60–256 MB (1.6M–7.6M faces, varies per generation; the 256 MB one takes ~3 min to import and trim), one 4096 base colour, normalised to about ±1 unit (shoulders width), glTF import faces
  **−Y**, Z up. Trim and decimate it (`meshy_trim_source`) before keeping anything in the repo.
- Hair and beard are **sculpted** (curl bumps on the scalp): use it as a shape source only through a smoothed fit.
- The generated head's proportions differ from the portrait-derived base: Storekeeper's ears came out 19 mm lower and the
  crown ~20 mm lower than the canonical head once aligned on the eyes.

## Preset animations (`meshy_animate`)

- Needs a `rig_task_id` from an API rigging task (5 credits, textured humanoid `.glb`, public URL). There is none for
  this project's characters (they are rigged through Mixamo), so the preset library is not directly usable.
- `meshy_list_animations` is free; filter with `category` / `search` (the full library is long).
