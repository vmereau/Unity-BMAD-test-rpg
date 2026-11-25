# Tools/Blender

Blender-side tools for editing animation clips and preparing character meshes. Outside `Assets/`, so Unity never imports them. Driven from Claude Code
through the Blender MCP (`mcp__blender__execute_blender_code`) or from the Blender command line. Recipe and gotchas:
`Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md` → *Blender edit recipe*.

| File | What |
|------|------|
| `twohand_blade_line.py` | Re-aims the blade line (left → right hand) of a Mixamo 2H clip by moving the left arm with IK inside an edit window, bakes it, exports an armature-only FBX |
| `split_head_body.py` | Cuts a Mixamo character at a level neck plane into `<name>_head` / `<name>_body` on the same armature, copies the uncut smooth normals to both, exports one FBX (no animation, no leaf bones). Base hero: 1.585 m → `base hero split.fbx` |
| `make_head_variant.py` | NPC face from the neck-prepared base head: reshape above a frozen neck + repaint a texture copy (hair, beard, brows, eyes, scars), render mockups, export `models/heads/<Name>/` (FBX, texture, per-head skin mask; `export=[parts]` for a subset). Configs in `configs/heads/`; recipe and gotchas in `Humanoids/CLAUDE.md` → NPC head variants |
| `make_head_variant.py` `portrait` block + `render_base_refs()` | Portrait faces (spec-npc-portrait-faces): a user-generated front portrait + side profile reshape the face (MediaPipe landmark RBF warp, profile depth) and are projected onto it as its texture. Inputs in `configs/heads/refs/`; recipe in `Humanoids/CLAUDE.md` → NPC portrait faces |
| `../FaceRef/face_landmarks.py` | Outside Blender (`uv run`, Python 3.12, MediaPipe): 478 front landmarks + crown row, side-profile edge + features (nose tip, brow, nasion, chin, crown) → `refs/<name>_landmarks.json` + debug overlays |
| `verify_head_variants.cs` | Unity MCP `execute_code` body: posed neck-join gap + neck / portrait captures of every `HeadAppearance_*`, bare and Company armor |
| `verify_head_tones.cs` | Unity MCP `execute_code` body: untoned `SG_SkinLit` vs URP Lit diff of every head material (must be 0), tone sheet of each planned face + tone (face, left neck bare / armored, body) → `Temp/HeadTones/` (+ `report.txt`) |
| `make_skin_masks.py` + `configs/skin_masks.json` | Skin masks for `SG_SkinLit` (NPC skin tones): body + base head atlas, plus one per outfit that shows skin (hand bone weights). Prints each atlas's `_SkinRefLuminance`. Recipe in `Humanoids/CLAUDE.md` → NPC skin tones |
| `verify_skin_tones.cs` | Unity MCP `execute_code` body: untoned `SG_SkinLit` vs URP Lit pixel diff + tone sheet (face, hands, body, armor) of every `Tone_*` → `Temp/SkinTones/` |
| `configs/2hand_attack_1.json` | Final inputs that produced `two handed/2hand attack refined.fbx` (Attack_1) — constant `target_pitch_deg` |
| `configs/2hand_attack_2.json` | Final inputs that produced `two handed/2hand attack 2 refined.fbx` (Attack_2 finisher) — `pitch_curve` |
| `unarmed_foot_step.py` + `configs/unarmed/` | Leg IK edit of the unarmed combo clips so the feet follow the scripted `AttackStep` (see *Unarmed foot step*) — the previous Mixamo clips, unreferenced since spec-action-root-motion-unarmed-combo, kept for a revert |
| `unarmed_foot_step_measure.cs` | Unity MCP `execute_code` body: per-frame toe / ankle positions of a clip on the Player rig (player-local JSON) |
| `meshy_motion_fit.py` + `configs/unarmed/meshy_jab.json` / `meshy_hook.json` / `meshy_uppercut.json` + `sources/meshy_*_armature.fbx` | Fits a Meshy text-to-motion clip that travels (SMPL-H skeleton) to a wanted forward distance and to the stance of the clip it chains from, for clip root motion (see *Meshy motion fit*) |
| `configs/sword/meshy_sword_cut.json` / `meshy_sword_backhand.json` / `meshy_sword_thrust.json` + `sources/meshy_sword_*_armature.fbx` | Inputs of the one-handed sword combo attempt (spec-sword-three-hit-combo, **abandoned: rejected on look, the clips are not in the project**; running a config writes `one handed/sword * step.fbx` again): same fit script, plus the blade aim and retime options below |
| `sword_blade_measure.cs` | Unity MCP `execute_code` body (edit mode): per-frame blade direction, edge, tip and off-hand distances of one-handed clips on the Player rig, rebuilt from the right-hand socket and the archetype's `drawnPose`; optional shield held as `EquipmentVisuals` holds it, with its distance to the blade |
| `sword_combo_play_measure.cs` | Unity MCP `execute_code` body (play mode): equips a weapon and an optional off-hand item and runs a list of chains (press times, Animator speed, a cancel on a given step), one CSV per run with the `action_root_motion_play_measure.cs` columns plus blade tip, hand and off-hand item distances; read by `action_root_motion_analyse.py` |
| `meshy_motion_measure.cs` | Unity MCP `execute_code` body (edit mode): per-frame root travel and root-relative hips / feet / hands of the three clips on the Player rig — the input of the `start_match` / `end_match` offsets |
| `action_root_motion_play_measure.cs` + `action_root_motion_analyse.py` | Unity MCP `execute_code` body (play mode) that runs an unarmed chain through `PlayerCombat.TryAttack` with chosen press times and logs one CSV row per frame; the Python script turns the CSVs into travel per attack, sideways drift, root speed inside the crossfades, planted-foot slide and velocity jumps |

## Meshy motion fit

`meshy_motion_fit.py` turns a stepping Meshy clip into a clip `ActionRootMotion` reads (`Scripts/Combat/PLAYER_COMBO.md`):
`Unarmed/lead jab step.fbx`, `hook step.fbx`, `uppercut step.fbx`, one config each. `run(config)`: trim + retime
(`first_frame`, `last_frame`, `speed`), turn the whole motion so the net pelvis travel is straight ahead, move the pelvis
start onto the origin, scale every displacement from the start pose to `travel_m` (planted feet stay planted, start stance
unchanged), shrink the pelvis dip and foot lift (`pelvis_height_scale`, `foot_lift_scale`), re-solve both legs with an
analytic 2-bone IK, key every bone, export. `strip_source(raw, out)` makes the small armature-only source from the 16 MB
download; `export_rig(raw, out)` exports the T-pose skeleton Unity builds the SMPL-H avatar from (`Unarmed/meshy smplh
rig.fbx`, shared by the three clips).

Options added for clips that chain (all off by default; `meshy_jab.json` uses none and still reproduces
`lead jab step.fbx` bone for bone):

| Key | What |
|-----|------|
| `lateral_scale` | Scales the sideways part of every displacement (hook 0.25, uppercut 0.5): a straighter path, planted feet stay planted |
| `travel_yaw_deg` | Turns the travel line, not the body, to the character's right (hook 8°) |
| `backtrack_scale` | Scales how far the pelvis settles back after its furthest point (hook 0.4); feet untouched |
| `facing_offset_deg` | Turns the body without turning the travel (unused so far) |
| `foot_shift` | `{"R": [x, y, z]}` constant offset of a foot's whole path (hook: rear foot 0.27 m inward) |
| `start_match` / `end_match` | Per foot, one spec or a list of `{"offset": [x, y, z], "fade": [f0, f1], "lift_m": 0}`: the foot is moved by `offset` at the first / last frame and the offset fades out / in over the output frames `fade` |

Options added for the abandoned sword combo attempt, kept for the next one (all off by default; the three unarmed configs still reproduce their FBX with 0
difference):

| Key | What |
|-----|------|
| `blade_aim` | `{"keys": [{"frame", "dir"}], "forearm_angle_deg": [60, 120]}`: replaces the right wrist so a one-handed weapon points along `dir` (slerped between keys), kept within the given angles of the forearm, the hand rolled about the blade to stay on the forearm's line. `SWORD_BLADE_LOCAL` in the script is the blade direction in the SMPL-H `R_Wrist` bone's space, fitted against Unity (below) |
| `time_map` | `[[1, 1], [18, 18], [36, 27]]` (output frame, source frame): piecewise-linear retime instead of `first_frame` / `last_frame` / `speed` (an error when both are given) — the strike at or near real time, the follow-through slowed (cut × 0.5, backhand × 0.31); the speed changes in one frame at each knee |
| `upper_body` | `{"first_frame", "speed", "keep_prefixes"}`: spine, head, arms and pelvis orientation from another stretch of the same source; bones matching `keep_prefixes` (e.g. `"L_"`) stay on the legs' stretch, and so do the toes (`L_Foot` / `R_Foot`) |
| `hand_shift` | `{"L": {"offset", "frames": [f0, f1, f2, f3]}}`: moves a wrist by a faded offset with a 2-bone arm solve, hand orientation kept |
| `pelvis_drop_m` | Lowers the pelvis by a constant. The report's `reach_clamp_max_m` says how far a foot falls short of its target (a stance widened to the 0.9 m sword idle is out of reach at the source's standing height: 5.6 cm short until the pelvis was dropped 7 cm) |

The report also gives, per frame, `r_arm`, `forearm`, `wrist_height`, the predicted `blade` / `edge` and
`wrist_bend_deg`, with or without `blade_aim`: run once without keys to read the arm, then key the blade.

**Blade calibration.** Unity's blade (socket × `drawnPose`, `sword_blade_measure.cs`) against the wrist's world rotation
in Blender over `hook step` and `uppercut step`: one constant direction in `R_Wrist` space reproduces it within 8° on
average, 16° at worst; on the sword clips Unity's blade landed within about 15° of the keyed direction. Sample the same
frame on both sides: a re-imported FBX's action starts at frame 2 in Blender, one frame later than Unity's time 0 (a
one-frame offset doubled the error). Re-derive the constant when the `drawnPose`, the socket or the avatar changes.

**Keying a blade.** A hammer grip holds the blade across the forearm, so a direction along the arm cannot be reached
(it asked for a 75–115° wrist bend): key the blade trailing the swing (pointing back where the hand comes from) and
tilting forward at the hit. The cutting edge is not a free choice, it faces along the forearm.

Offsets are metres in fitted space: **−Y forward, +X the character's left, Z up** (Unity x right, z forward → Blender
x = −x, y = −z). A new chained clip, in order:

1. Run the config without matching, import (copy `lead jab step.fbx.meta`: new GUID, clip name, frame range — the controller and the overrides then
   reference that clip by its `internalID`, so edit events on the existing clip entry, never rebuild the list), sample the
   previous clip and the new one with `meshy_motion_measure.cs`.
2. Offset = previous clip's foot inside its combo window − new clip's foot at frame 0, both relative to the root. Fade it
   while that foot is in the air. The measured gain is 1: one pass put the feet within 4 cm.
3. Tune `travel_m` against the travel **in a chain**, measured in play (`action_root_motion_play_measure.cs`): the
   crossfade eats part of each clip (`PLAYER_COMBO.md` → Action root motion).
4. Author the four events with the Hitbox Tuner or `ModelImporter.clipAnimations` (normalized time).


Gotchas:
- Imported SMPL-H bones do not point along their own Y axis: aim a bone with the direction to its **child joint**.
- A stripped (armature-only) FBX re-imports with keys shifted by + 1 frame **and its rest pose replaced by the first
  frame's pose**: build the avatar rig from the raw download, never from the stripped source.
- Scale foot heights about each ankle's lowest point, not its start height, or a planted foot floats.
- `travel_m` is clip-space pelvis travel: 0.42 gives 0.40 m on the Player (0.42 peak, the exit blend cuts the settle back).
  With `backtrack_scale` the clip ends further than `travel_m` (hook: 0.33 → 0.37 net in the clip, 0.42 on the Player);
  the report's `net_travel_m` / `peak_travel_m` give the clip's own values.
- Unity's root follows the **centre of mass**, not the pelvis: a straight pelvis path still gives a root that leans with
  the torso (hook: 13 cm to the left before `travel_yaw_deg`). Setting the importer's motion node to `Pelvis` is no fix
  (the root then sits at hip height and turns with the hips).
- A foot offset that asks for more than the leg's reach is clamped (`reach_clamped_frames` in the report) and the foot
  lifts: shorten the offset rather than lowering the pelvis.
- The script clears the open Blender file; run the play-mode harness on a flat spot and put the player back between runs
  (after ~3.5 m the StartingTown spawn has scenery in the way), and refill stamina (5 swings empty it).
- Keep the clip's start on the world origin, and `Animator.feetPivotActive` at 0 in game: at 1, blends drag the player
  sideways (`PLAYER_COMBO.md` → Action root motion).

## Unarmed foot step

`unarmed_foot_step.py` rewrites both legs of `Unarmed/old/<clip>.fbx` and exports `Unarmed/<clip>.fbx` (whose `.meta`
keeps the original GUID, clip internal id and events; take `Scene`, frame range +1). Config (`configs/unarmed/*.json`):
`step` = the `CombatConfigSO.unarmedAttacks` entry, `trim_first_frame` (Hook 10), `lead_moves` / `rear_moves` =
`[t0, t1, T]` lifted moves to the source's own spot at clip time T plus the step done by then (`null` = clip end, last
move; validated: sorted, after `step.start`, ending before the clip end), `<foot>_lift_m`, `correction` =
`{"L": {"<Unity frame>": [dx, dy, dz]}, "R": {...}}` (sparse, player-local metres) accumulated by `update_correction`.
`<clip>_source.json` = the source toes sampled with `unarmed_foot_step_measure.cs` (Unity frame index, Hook index 0 =
source frame 10). Final configs reproduce the shipped clips.

Run it with `__file__` set (see the script docstring), or relative config paths resolve from Blender's cwd.
Loop: `run(config)` → reimport in Unity → sample the exported clip with `unarmed_foot_step_measure.cs` →
`update_correction(config, measured.json)` (`correction += 0.8 × (wanted − measured)`, clamped to 8 cm per axis) →
repeat (3–6 passes). The configs hold the final corrections; re-running them reproduces the shipped clips on Blender 5.2.
Gotchas:
- Armature units differ per Mixamo export (Uppercut is centimetres, hips ≈ 88): the script detects it from the hips height.
- Command → result gain is far below 1 and cross-coupled (Unity places the body on the centre of mass and retargets leg
  muscles); a gain-1 or secant update overshoots into straight knees. Damp and clamp.
- Targets beyond the leg's reach make the 2-bone IK spin the leg (foot pops behind the other): targets are clamped to
  98.5 % of thigh + shin (`reach_clamped_frames` in the report). Where the Player knee locks anyway, plant the foot shorter
  (Hook first lead plant on its f17 spot) instead of pushing the correction.
- Pole angle is calibrated per leg against the source knee (≈ 1 cm mean error); it must run before the target is keyed.

**Pitch target.** `target_pitch_deg` is one constant and only ever pulls the blade down (Attack_1 path). `pitch_curve`
(`[[normalized t, Unity pitch deg], ...]`, sorted, piecewise linear) replaces it when present and can raise or lower the
blade per frame; yaw is always kept and the result is clamped to ±89°. Outside its first / last point the curve holds the
end value for the whole `window`, so the window's fade-in / fade-out blends toward that held value. The curve values are what you feed the script, not what you get: the hand-bone proxy
misses large pitches by up to ~10°, so after a pass set each point to `point + (wanted − measured)` (Attack_2: points on
every frame of the edit, 3 passes). Ramp a big change over several frames — Attack_2 spreads a 75° raise over 0.21–0.42
to keep the tip speed within the original's + 8 m/s.

## Running

Blender MCP (Blender open, add-on connected):

```python
exec(open(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/twohand_blade_line.py").read())
result = run(load_config(r"D:/github/perso/Unity-BMAD-test-rpg/Tools/Blender/configs/2hand_attack_1.json"))
```

Command line: `blender -b -P Tools/Blender/twohand_blade_line.py -- Tools/Blender/configs/2hand_attack_1.json`

The script clears the current Blender scene (objects, armatures, actions) before importing.

Verified 2026-11-03: running it with `configs/2hand_attack_1.json` reproduces the committed `2hand attack refined.fbx`
exactly (0° on every bone, every frame). When comparing in Blender, note that **re-importing a Blender-exported FBX shifts
its keys by +1 frame** (2–57 instead of 1–56); compare source frame f with export frame f + 1. Unity reads the take as
1/30 s – 1.867 s, and the clip range 1–56 lines up with the source frames 0–55.

## The measure → edit loop (new clip)

1. In Unity, sample the **source** clip with the snippet below → paste the values into `blade_pitch_unity`
   (one per source frame). Start with no `distance_scale`.
2. Run the script, re-import the output in Unity (first time: copy the source root-bake flags, avatar setup, compression and
   events onto the new `.meta`, keep its own take `Scene` and frame range).
3. Sample source and output: per frame, `distance_scale[f] *= sourceDist / outputDist` (key = Blender frame = source frame + 1),
   and nudge `target_pitch_deg` / `pitch_curve` if the cut lands off target. Repeat until the hand distance is within
   ~0.02 m (Attack_1 and Attack_2 each took 3 passes).
4. For a full check (yaw, tip, speeds vs the original, blade clearances to spine / head / legs, body bones), extend the
   snippet; exclude the left arm **and the left fingers** from the "body unchanged" check (they follow the hand).

```csharp
// Unity MCP execute_code: per-frame blade pitch and hand distance of a clip on the Player rig.
string path = "Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/2hand attack.fbx";
var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab"));
go.hideFlags = HideFlags.HideAndDontSave; go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
var sb = new System.Text.StringBuilder();
try {
  var anim = go.GetComponentInChildren<Animator>(); anim.fireEvents = false; // clip events would hit uninitialised receivers
  var so = new SerializedObject(go.GetComponentInChildren<Game.Inventory.EquipmentVisuals>());
  var rs = (Transform)so.FindProperty("_weaponSocket").objectReferenceValue;
  var ls = (Transform)so.FindProperty("_leftWeaponSocket").objectReferenceValue;
  AnimationClip clip = null; foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is AnimationClip c && !c.name.StartsWith("__preview")) clip = c;
  var graph = UnityEngine.Playables.PlayableGraph.Create("s");
  var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "o", anim);
  var p = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip); p.SetApplyFootIK(false);
  UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, p);
  int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
  for (int f = 0; f <= frames; f++) {
    UnityEngine.Playables.PlayableExtensions.SetTime(p, (double)(f / (float)frames * clip.length)); graph.Evaluate(0);
    Vector3 line = go.transform.InverseTransformDirection(rs.position - ls.position);
    sb.Append(System.FormattableString.Invariant($"{Mathf.Asin(line.normalized.y) * Mathf.Rad2Deg:F1}/{line.magnitude:F3},"));
  }
  graph.Destroy();
} finally { UnityEngine.Object.DestroyImmediate(go); }
return sb.ToString(); // "pitch/distance," per frame
```

Instantiating `Player.prefab` in edit mode logs one harmless NRE (`EquipmentVisuals.OnEnable` → `PlayerAnimationDriver`).

## Canonical armor neck

`prepare_armor_neck.py` + `configs/leather_neck.json` reproducibly regenerate the sample head-owned neck and
leather collar from immutable canonical-split and fitted-armor inputs. Paths in JSON are project-relative.
Run in the connected Blender (it restores the user's active scene and cleans up its temporary scenes):

```python
import runpy
root = "D:/github/perso/Unity-BMAD-test-rpg/"
result = runpy.run_path(root + "Tools/Blender/prepare_armor_neck.py")["run"](
    root + "Tools/Blender/configs/leather_neck.json")
```

The original head/body interface remains at 1.585 m. Extra skin is inset inside the original body down to
1.48 m, concealed by Body when bare and by the garment when armored. The garment collar uses a complete
neck profile at 1.56 m with 6 mm authoring clearance. It joins its surviving exterior garment boundary,
rather than floating over a cutout. Skin UVs stay with the head; the collar uses a leather atlas patch.

Run Unity `Tools/Characters/Bake Leather Neck Parts` after generation. Unlike the general mesh baker, this
sample batch validates the exposed skin interface and neck UVs before replacing any baked assets. Inspect
posed coverage and close-ups too: successful import/size/name checks do not establish visual continuity.
`SkinnedNeckValidatorTests` exercises malformed interfaces; `SkinnedMeshRebinderTests` covers runtime binding.

For another outfit, fit and weight it against the canonical rig first, then author its neck finishing inputs.
For another head, preserve the canonical neck interface and bind poses, change the face above it, and assign
that head's skin material. Same bone names alone do not establish compatibility. See the humanoid CLAUDE.md
for the full shared-uniform contract and limits.

Collar regression guard: sample below the original exposed seam (current profile 1.56 m); reject a sampled radius above 0.10 m. Generated rim weights exclude Head influence and renormalize toward Neck; existing garment weights blend into that rim. Inspect the under-chin silhouette in addition to join diagnostics.

The sample damaged-collar trim radius is 0.16 m, covering the original folded rear rim. The inner lining descends to 1.485 m and tapers into the closed skin stump. Its raised lip must remain below the canonical exposed seam.

## Company grunt outfit

Run `runpy.run_path(root + "Tools/Blender/fit_company_grunt.py")["run"]()` in connected Blender,
then `Game.Editor.Characters.CompanyGruntArmorBuilder.Build()` through Unity MCP after compilation.
The immutable Meshy FBX has no rig or weights. Preparation aligns its feet and arm line, removes
its head, transfers barycentric weights from the canonical body, and exports on the original
canonical armature. No Mixamo pass is required. All working scenes and intermediate files are temporary.

The Company's generated neckline has concentric layers. Neck finishing opts into choosing the
outer opening and supplies its own atlas patch/name; leather defaults remain unchanged.
A separate close-fitting cloth sleeve copies the canonical head's lower surface and influences,
with 4 mm radial clearance. Its top slopes lower beneath the chin and higher at the sides/back,
covering the shoulder-weighted skin during attacks without changing the original head asset.
The source colors, emblem and UVs remain on the main garment. The generated bare hands are replaced at bake time by
the base hero v2 hands (`OutfitHandGraft`, see *Base hero v2 body*).

The builder creates a separate armor item, canonical baked mesh, visual prefab, compact torso pickup
and torso-cropped concept icon. Stats/prices initially match leather armor; no NPC, shop, quest or
player starting-inventory distribution is added. Pickups use the outfit torso's own UVs and material.

`verify_company_grunt.cs` is a Unity MCP method body for four-sided animation previews. Toggle
`closeups` for neck views and `neutral` to inspect geometry. **Set every preview SMR's
`forceMatrixRecalculationPerRender = true` before rendering sampled poses.** Edit-mode rendering
can otherwise retain stale skin matrices even while bone transforms and baked meshes update;
an apparently motionless sleeve is not proof of incorrect weights. Preview samples are finite,
not a guarantee for every possible pose or external head/rig.

## Base hero v2 body

`fit_base_body.py` + `configs/base_body_v2.json` rebuild the player / NPC body from the raw Meshy download in
`sources/base_hero_v2/` (gitignored, keep it locally: the fit cannot run without it):

```python
m = runpy.run_path(root + "Tools/Blender/fit_base_body.py")
rep = m["run"](m["load_config"](root + "Tools/Blender/configs/base_body_v2.json"))   # export=True by default
mk = runpy.run_path(root + "Tools/Blender/make_skin_masks.py")
mk["run"](root + "Tools/Blender/configs/skin_masks.json", only=["BaseHeroV2"])        # -> ref_luminance
```

Outputs: `models/base hero v2/base hero v2.fbx` (armature + `base_hero` body, 2 material slots; the canonical head is
not exported, it never changes), `T_BaseHeroV2_BaseColor.png` / `_Normal.png` (rewritten from the immutable sources
with the neck band baked in) and the mask. Then in Unity: copy `ref_luminance` into
`BaseHeroV2Applier.SKIN_REF_LUMINANCE`, reimport, run `Tools/Characters/Apply Base Hero V2 Body`, then re-bake the
outfits (`SkinnedNeckBaker.BakeLeather()`, `CompanyGruntArmorBuilder.Build()`) so their grafted hands follow the body.
Steps, contracts and gotchas: `Assets/_Game/Art/Characters/Humanoids/PIPELINE.md` → *Base hero v2 body*.
`render_views(scene, views, out_dir, size, clay)` renders ortho previews of the fit scene `_BodyV2Fit`.
