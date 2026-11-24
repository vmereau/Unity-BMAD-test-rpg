# Combat Clip Bakes — Blender edits and `.anim` bake recipes

> Read when re-editing or re-baking a combat clip: the 2H blade-line Blender edit, the unarmed foot-step leg edit,
> the unarmed idle / block guard bakes, or a two-part draw / sheathe merge. Clip events and timings: `CLAUDE.md`
> (this folder).

---

## 2H blade line (Blender edit of `2hand attack refined` / `2hand attack 2 refined`)

Attack_1 (`2hand attack refined.fbx`): only the left arm, 0.38–0.62, so the blade (left → right hand line, `TwoHandGripConstraint`)
runs horizontal through the cut: pitch +18…+35° → +1…+7°, tip 1.23–1.58 → 0.91–1.11 m. Body and right hand within 2.4 mm.

Attack_2 (`2hand attack 2 refined.fbx`, `configs/2hand_attack_2.json`, `pitch_curve`): left arm only, 0.18–0.69, yaw kept. The source swing was a near-flat descending sweep (blade never raised, flat behind the head at 0.42,
plane tilted 21°, tip above the hands +11…+19° at 0.45–0.50). Refined: blade lifts over the right shoulder from 0.21
(+48° at 0.42, tip 2.6 m), pitch falls monotonically to −38° at 0.605, then eases back to the source by 0.69; plane tilt
42°. Body and right socket within 2.7 mm. Known limit: the hands are at 0.88 m when the blade points forward (0.55), so
the tip passes the centre low (~0.7 m); raising it needs the body / right arm.

### Recipe (weapon line of a 2H clip)

Claude Code Blender MCP (`mcp__blender__*`, Blender 5.2). Script + per-clip configs: `Tools/Blender/` (repo root,
`twohand_blade_line.py`, `configs/2hand_attack_1.json` / `2hand_attack_2.json`, README with the Unity sampling snippet, the
measure → edit loop and the constant vs `pitch_curve` target).
1. Sample the clip in Unity (PlayableGraph on `Player.prefab`, `WeaponSocket` / `LeftWeaponSocket`, per frame); set
   `animator.fireEvents = false` first or the clip events throw NREs into the uninitialised instance (the one NRE from
   `EquipmentVisuals.OnEnable` → `PlayerAnimationDriver.SetInCombat` on instantiation is expected and harmless).
2. Import the FBX in Blender (`automatic_bone_orientation=False`; armature scale 0.01, armature space +Y up, +Z forward,
   −X right); wrap import / export in `temp_override(window=…)` — after `read_homefile` the context has no active object.
3. Key an empty (parented to the armature, armature space) as the left-hand IK target `R − d′ × dist`, `d′` = Unity-measured
   correction applied to the Middle1→Middle1 line; IK on `LeftForeArm` (chain 2, pole from the original elbow, pole angle
   calibrated ≈ 1.5°), influence keyed with a smoothstep ramp; bake LOCAL quats (`convert_space`) only in the window.
4. Hand bones are only a proxy for the Unity sockets (retarget + palm offsets ≈ 10° pitch, distances off by up to 70 %):
   re-measure in Unity and correct per frame — 3 passes reached 0.022 m on the hand distance. Attack_1 final values:
   target pitch +4°, pole angle 1.5°, distance scale per Blender frame 24: 1.69, 25: 1.36, 26–31: 1.07–1.28, others ≈ 1.
5. Export armature-only FBX (baked, no leaf bones, no simplify) at scene fps 30. Copy the source root-bake flags, avatar
   setup, compression and events, but keep the new take (`Scene`) and its frame range (1–56), never the source's.

---

## Unarmed foot step (Blender leg edit, spec-unarmed-attack-foot-step)
The three shipped FBX are **Blender re-exports** whose legs follow the scripted `AttackStep` (each foot world-fixed or
lifted, never sliding); the untouched Mixamo sources are in `Unarmed/old/` (own GUIDs, same import settings, unreferenced
— the edit inputs, and what an in-place user would want). Tool + recipe: `Tools/Blender/unarmed_foot_step.py`
(README → *Unarmed foot step*). Upper body / arms / events / lengths / import flags unchanged; body pose relative to the
hips identical, the whole body shifts 1.8–3.0 cm in the hit windows (Unity re-centres the body on the moved legs).

These three clips are no longer referenced: the combo plays the `* step` clips since spec-action-root-motion-unarmed-combo
(`CLAUDE.md` → Unarmed). The recipe is kept for a revert.

| Clip | Step (m / start / dur, s) | Foot plan (clip time, s) | Planted drift on the Player (unblended) |
|------|------|------|------|
| lead jab | 0.30 / 0.07 / 0.43 | lead push-step 0.10–0.37; rear held, drags up 0.50–0.77 | rear 0.9 cm, lead 0.1 cm (lead creeps ≈ 5 cm f34–39: knee at full extension) |
| Hook | 0.35 / 0.27 / 0.37 | lead step 0.29–0.45 (plants on its f17 spot), rear steps up 0.42–0.62, lead settles 1.50–1.80 | rear 0.8 cm, lead 3.1 cm (f16, just after landing) |
| Uppercut | 0.50 / 0.10 / 0.37 | lead step 0.11–0.27, rear steps up 0.25–0.45 (shuffle) | rear 1.3 cm, lead 2.7 cm (landing frame) |

Hook / Uppercut capture bodies travel only 0.10 / 0.06 m: pinning the rear foot for the whole window over-stretches the
legs, hence the shuffle (user decision). Known limits: the 0.26 s CombatIdle → Attack_1 blend (square idle → bladed jab,
≈ 22 cm rear-foot slide) and the 0.42 s combo blends (the next step runs inside them) still slide the mixed pose
(`deferred-work.md`). NPCs play these clips in place — the step on the spot reads badly until they get the scripted step.

Gotchas:
- **Avatar after a Blender re-export:** the new FBX has an extra `Armature` node, so the source meta's `human` /
  `skeleton` fail ("Avatar creation failed"). Clear both, reimport (Unity auto-maps), then write an explicit description:
  the auto skeleton (keeps the `Armature` / root entries) with every other bone's rest transform and the `human` mapping
  copied from the `old/` importer by bone name (`EditorUtility.SetDirty(importer)` before `SaveAndReimport`, or an
  unchanged-looking description is not saved). The auto rest pose alone round-trips the jab / Hook exactly but not the
  Uppercut (5 cm off at an unedited frame: its Mixamo export is in centimetres and its stored rest skeleton differs); with
  the copied rest transforms all three match the source at unedited frames (0.00 mm).
- Play-mode foot checks: pause + `EditorApplication.Step()` is synchronous (Δt 0.02 s, ignores `captureDeltaTime`); set
  `Animator.cullingMode = AlwaysAnimate` or an unfocused editor never moves the bones; test on open ground (the
  `PlayerAttackStep` ledge probe can stop the capsule early and fake a foot slide).
- A failed import can leave the open `Humanoid_Template` with an empty attack state at runtime: see `../../Controllers/CLAUDE.md` (*Replacing an FBX in place can leave a state playing no clip*).

---

## Unarmed guard idle (`Unarmed Idle Guard.anim`, spec-unarmed-idle-posture-fix)
`CombatIdle` (UpperBody layer) plays **`Unarmed Idle Guard.anim`**, baked from `Unarmed Idle.fbx` (Mixamo, 89 frames,
loop, root rotation baked on Body orientation, Y baked on Original). The FBX itself is unreferenced, but keep it: it's
the bake source. Played raw, the guard looked wrong in game, for two reasons. Distances below are in **body heights**
(head bone height above the feet), measured from the chest bone in player-local space (x right, y up, z forward).
- **Arm proportions:** humanoid retargeting copies the arm *angles* (within 3°), but the Player's forearm/upper-arm ratio
  (1.1) is far shorter than the source rig's (1.7), so the fists sat 0.12 instead of 0.22 in front of the chest.
- **Mask:** `UpperBodyMask` takes the hips from locomotion, which drops the source's forward lean (11.6° → −4.6°).

The bake copies every curve and the clip settings, and adds **constant** muscle offsets: Spine / Chest / UpperChest
Front-Back −0.22; Left Arm Front-Back −0.38, Down-Up +0.42, Forearm Stretch +0.38; Right Arm Front-Back −0.34, Down-Up
+0.11, Forearm Stretch +0.37; Neck Nod Down-Up +0.36, Head Nod Down-Up +0.18 (added with the block guard: the
source looks 36° down, now ~14°). Result on the Player (base `Idle` + mask, mean of t = 0.2 / 1.0 / 1.8 / 2.6 s):
lean 9.7°, fists L (−0.18, 0.03, 0.23), R (−0.04, 0.06, 0.20) vs source (−0.18, 0.03, 0.23), (−0.05, 0.06, 0.20).
Over `Walking` / `Running` the fists keep ≥ 0.18 forward and the shoulder line stays within ±8° (the lean rises to
15–20°, the run's own hip tilt). No committed bake tool: re-bake from `execute_code` and re-fit the offsets by
coordinate descent against the source pose (instantiate the FBX for the target, `Player.prefab` + `AnimationLayerMixerPlayable`
with the mask for the result) if the FBX or the player body changes. The Sword / TwoHanded overrides are keyed on the `.anim`.

---

## Unarmed block high guard (`Unarmed Block Guard.anim`, spec-unarmed-high-guard-block)
`Block_State` (UpperBody) plays **`Unarmed Block Guard.anim`**: a second bake from **`Unarmed Idle Guard.anim`** (so it
keeps the proportion fix, lean, breathing and loop), with more constant muscle offsets. Pose targets:
`Unarmed/refs/high guard front.png` / `high guard side.png` (Gemini, image-to-image from the base hero). Offsets:
- Head: Neck Nod Down-Up +0.36, Head Nod Down-Up +0.18 — the old idle guard looked 36° down (head into the fists).
  **These now live in `Unarmed Idle Guard.anim` itself** (the shipped block clip already contains them): a re-bake from the
  current idle guard adds only the arm offsets below, or the head is raised twice.
- Left: Shoulder Down-Up +0.19, Front-Back −0.03; Arm Down-Up −0.59, Front-Back −0.24, Twist −0.08; Forearm Stretch −0.33;
  Hand Down-Up −0.10, In-Out +0.74.
- Right: Shoulder Down-Up +0.29, Front-Back +0.40; Arm Down-Up −0.48, Front-Back +0.24, Twist +0.17; Forearm Stretch −0.48,
  Twist +0.36; Hand Down-Up +0.24.
Result on the Player (current body; base `Idle` + mask, from the head bone, metres): index knuckles L (−0.05, +0.01, +0.26),
R (+0.07, +0.01, +0.22) — in front of the mouth / chin; elbows L (−0.17, −0.34, +0.05), R (+0.18, −0.35, +0.02).
Over Walking / Running / Walking Backwards the knuckles stay ≥ 0.20 in front of the head bone.
Fitting: coordinate descent per arm (steps 0.1 → 0.02, shoulder offsets capped at ±0.4, every muscle kept within ±1)
on knuckle (weight 3), wrist (1) and elbow (0.5) targets, then front / side renders against the refs. Gotchas:
- Fit fist targets **after** fixing the head pitch: a hanging head moves the face 5–10 cm forward / down into the fists.
- Don't touch the finger curves: the source fingers are already curled past −1 (clamped). "Closing" them to −0.75
  opens the fists. The Mixamo rig has index fingers only, so a fist never looks fully closed.
- Muscle offsets are unbounded in the solver unless capped: an uncapped fit pushed Right Shoulder Front-Back to +2.05.
Re-bake from `execute_code` like the idle guard (copy curves + settings, add the constants) if the idle guard changes.


---

## One-hand braced guard (`one handed/Sword Braced Guard.anim`, spec-one-hand-braced-block)
`Block_State` for a one-hander **without a shield** (Sword override, `ShieldInHand` 0): blade diagonal up-left across the
face line, hilt in front of the right chest, left palm flat behind the blade at mid-length. Source: Meshy text-to-motion
`sword braced guard source.fbx` (prompt log: `MESHY_PROMPTS.md`), armature-only, avatar copied from `Unarmed/meshy smplh
rig.fbx` (like `lead jab step`), root baked, loop. The bake keeps every source curve (spine / neck / head breathing,
loop seam ≤ 0.06 muscle units) and **replaces** both arms with constants (a brace is rigid; the source arms drift 13 cm):
- Right: Shoulder Down-Up −0.812, Front-Back 0.234; Arm Down-Up −0.438, Front-Back −0.207, Twist 0.269; Forearm Stretch
  −0.484, Twist −0.821; Hand Down-Up −0.715, In-Out 0.826. Right fingers (`RightHand.*`, thumb excluded): Stretched −0.8,
  Spread 0 (a fist; the `SwordIdle` values splay the fingers). Thumb from `SwordIdle` at t 0.
- Left: Shoulder Down-Up −0.564, Front-Back −0.429; Arm Down-Up 0.613, Front-Back −0.855, Twist 0.067; Forearm Stretch
  0.303, Twist −0.609; Hand Down-Up −0.159, In-Out 0.049. Left fingers (thumb excluded): Stretched 0.15, Spread 0 (positive
  over-extends: 0.6 looked clawed).
Targets (Player-local, x right, y up, z forward; base `Idle` + `UpperBodyMask`, sword = archetype `drawnPose` in
`WeaponSocket`, blade = `Drawn` +Y, edge = +Z): grip (0.15, 1.32, 0.32), blade (−0.62, 0.68, 0.25), edge forward ⟂ blade,
right elbow (0.28, 1.12, 0.10); left palm centre (lerp hand → middle proximal, 0.6) at grip + 0.42 × blade − 0.045 × edge,
palm normal (cross(thumb − middle, fingers)) along the edge, fingers across the blade (up), left elbow below the palm.
Result: grip within 3 cm, blade within 2°, edge 31° off forward, palm 4 mm; over Idle / Walking / Running the palm stays
on the blade (≤ 4.6 cm from its axis = the contact offset) and the blade ≥ 0.28 m from the head centre.
**Edge forward, not flat forward:** with a one-hand grip the flat-forward orientation is out of reach (edge 100° off,
every wrist muscle at ±1); the classic parry edge is what the wrist allows.
Fitting: random search (600 samples in ±0.9) then coordinate descent (steps 0.2 → 0.012), weights pos / 0.02–0.03 m,
blade / 4–5°, edge / 15–20°, elbow / 0.06 m, plus a penalty for |muscle| > 0.8. Evaluate by writing constant curves into
an in-memory copy of the clip and re-creating its `AnimationClipPlayable` (≈ 4 ms per pose). Gotchas:
- **`HumanPoseHandler.SetHumanPose` does nothing on an instantiated `Player.prefab`** (bones unchanged, Animator enabled or
  not) — `GetHumanPose` works. Fit through clip curves instead.
- **The raw Meshy download doesn't take the shared SMPL-H avatar** ("Transform 'Reference' not found"): strip it first
  (`meshy_motion_fit.strip_source` logic). The Blender MCP session may hold other work: import / export in a temporary
  scene (`bpy.data.scenes.new`, `window.scene = …`) and delete it, rather than running the script's `_clear()`.
- `AnimatorOverrideController.ApplyOverrides` serialises an empty `m_Clips` entry for every unmapped clip: delete the
  null-override entries through `SerializedObject` afterwards.
Re-bake from `execute_code` (copy the source clip, set the constants above, copy `AnimationClipSettings`) if the source or
the player body changes; re-fit if the arms no longer meet the targets.

---

## Two-part draw / sheathe → one baked `.anim`
Draw / sheathe is **one** UpperBody-layer state with one clip, so a vendor draw split in two parts (reach to the back →
bring forward) is merged, not given a second state. No committed bake tool yet — the clips were baked once from `execute_code`. Recipe: for every
`AnimationUtility.GetCurveBindings(part1)` (Humanoid muscle curves) copy part 1's keys, then part 2's keys shifted by
`part1.length + 0.1 s`; set Auto tangents on the two seam keys (smooths the small pose jump); copy part 1's
`AnimationClipSettings` (loopTime off, `stopTime` = total); one `WeaponSwap` event **in seconds** at the middle of the gap
(the hand is at the back there). Re-bake after changing the source parts' import settings. Keep the longest clip under
`CombatConfigSO.weaponSwitchTimeout` (2.5 s at speed 1).

---

## Bow wrist (`Bow/Bow Draw Arrow|Aim Overdraw|Fire Arrow.anim`, spec-bow-hand-grip)
The UpperBody bow states play bakes of the three Mixamo FBX made by **`Tools/Characters/Bake Bow Wrist Clips`**
(`Editor/Characters/BowClipWristBaker`, idempotent, GUIDs kept; rewires the states). The bake copies every curve and the
clip settings and adds **one constant offset set to all three clips** (no wrist turn at the state transitions):
`Left Hand Down-Up` +0.819, `Left Hand In-Out` +0.594, `Left Forearm Twist In-Out` +0.488. Each value is clamped to
±1, or to the source's own value where the clip already goes past it (Draw arrow's In-Out reaches 1.54: 15 keys held).
Why: `Archetype_Bow.drawnPose` puts the handle across the palm (a real grip), which turns the bow ~80° from where the
clips' pronated wrist held it. Fitted (coordinate descent, `HumanPoseHandler`, **restore `bodyPosition` /
`bodyRotation` per sample**) against the previous in-hand orientation over Aim Overdraw: 11° residual (3° on the
limbs' axis). Fire arrow: mean 23°, max 33° (ends 14–16°); Draw arrow ends 11° off (its middle, with the bow low, up to 55°: no target
there). Wrist / forearm-twist muscles don't move the hand bone (0 mm), so `BowAimCorrection`'s arrow line stays
calibrated. Re-fit if `drawnPose`, the clips or the body change.
