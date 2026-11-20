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

## Two-part draw / sheathe → one baked `.anim`
Draw / sheathe is **one** UpperBody-layer state with one clip, so a vendor draw split in two parts (reach to the back →
bring forward) is merged, not given a second state. No committed bake tool yet — the clips were baked once from `execute_code`. Recipe: for every
`AnimationUtility.GetCurveBindings(part1)` (Humanoid muscle curves) copy part 1's keys, then part 2's keys shifted by
`part1.length + 0.1 s`; set Auto tangents on the two seam keys (smooths the small pose jump); copy part 1's
`AnimationClipSettings` (loopTime off, `stopTime` = total); one `WeaponSwap` event **in seconds** at the middle of the gap
(the hand is at the back there). Re-bake after changing the source parts' import settings. Keep the longest clip under
`CombatConfigSO.weaponSwitchTimeout` (2.5 s at speed 1).
