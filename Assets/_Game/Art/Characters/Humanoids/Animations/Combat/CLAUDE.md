# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Animations/Combat

> Loaded when Claude accesses files in this folder. Covers the combat clips and their per-clip event
> timings. Event routing / receivers / SMBs: `.claude/rules/attack-pipeline.md`. Controller wiring:
> `../../Controllers/CLAUDE.md`.

---

## Folder Structure

```
Combat/
├── one handed/ — AttackLeft, AttackOverhead, AttackThrust, SwordIdle, Draw Sword, Sheath Sword + Sword_AnimatorOverride
├── two handed/ — 2hand idle / attack (+ attack refined, Blender) / attack 2 (+ attack 2 refined, Blender) / blocking / stagger, draw 1+2 / sheath 1+2 (sources) → baked `2hand draw.anim` /
│                 `2hand sheath.anim` + TwoHanded_AnimatorOverride (→ `Archetype_TwoHanded`)
├── Bow/        — Standing Equip Bow (clip `Draw Bow`), Standing Disarm Bow (clip `Sheathe Bow`) + Bow_AnimatorOverride;
│                 aim: Standing Draw Arrow (`Draw arrow`), Standing Aim Overdraw (`Aim Overdraw`, loopTime on),
│                 Standing Fire Arrow (`Fire arrow`) — no events, used directly by the bow aim states
├── Unarmed/    — lead jab, Hook, Uppercut (the combo, Blender leg edits; Mixamo sources in `old/`), draw unarmed, sheath unarmed, stagger 1/2,
│                 Unarmed Idle (source) → baked `Unarmed Idle Guard.anim` (the CombatIdle clip, see below)
│                 → baked `Unarmed Block Guard.anim` (Block_State, high guard; `refs/` = the pose reference pictures)
├── Block Idle.fbx — shield-style block, now only the sword's (Sword override → `Block Idle`)
├── dodge roll.fbx
├── Dodge back.fbx
├── execute stomp.fbx — clip `Execute Stomp` (player execute, see below)
└── MESHY_PROMPTS.md — Meshy text-to-motion prompt rules, 2H templates, durations, import checklist, prompt log
```

**Generating a clip with Meshy text-to-motion:** read and follow `MESHY_PROMPTS.md`, and log every prompt there.

---

## Attack Clip Events

Every player attack clip carries **all four** events — `HitboxEnable`, `HitboxDisable`, `ComboWindowOpen`,
`ComboWindowClose` — stored in the FBX `.meta` under `clipAnimations[].events` with **normalized** `time`
(0–1). A missing event is a silent failure: the hitbox stays open, or the combo never chains.

Author/retime them with the **Hitbox Tuner** (`Tools/Combat/Hitbox Tuner`) — it writes FBX events via
`ModelImporter.clipAnimations`. If editing the `.meta` by hand, copy an existing event block (all fields:
`time`, `functionName`, `data`, `objectReferenceParameter`, `floatParameter`, `intParameter`, `messageOptions`).

### One handed (`one handed/`)

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `AttackLeft.fbx` | Attack_1 / Attack_3 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackThrust.fbx` | Attack_2 | 0.25 | 0.50 | 0.50 | 0.90 |
| `AttackOverhead.fbx` | — (not mapped by `Sword_AnimatorOverride`) | 0.25 | 0.50 | 0.50 | 0.90 |

`HitboxDisable` and `ComboWindowOpen` share 0.50 — they are independent, so dispatch order is irrelevant.

### Two handed (`two handed/`)

2-step combo: `lead jab` → `2hand attack refined` (Attack_1), `Hook` → `2hand attack 2 refined` (Attack_2, finisher).
They twist the hips up to 112° — fine now that every attack plays full-body on the Base Layer (`Controllers/CLAUDE.md`).

**All weapon attack clips are in place** (the unarmed boxing clips are handled differently — see Unarmed below) (measured: net root XZ travel 0, body offset at most 0.63 m that returns to the start), so
they stay root-baked and the forward step is scripted per archetype (`AttackStep`, `Scripts/Combat/PLAYER_COMBO.md`). Tune
`attackSteps` start / duration in **seconds** against the clip lengths and hit windows (hit time = HitboxEnable × length).

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `2hand attack refined.fbx` (`2hand attack refined`, 1.83 s) | Attack_1 | 0.33 | 0.58 | 0.58 | 0.78 |
| `2hand attack 2 refined.fbx` (`2hand attack 2 refined`, 1.27 s) | Attack_2 (finisher) | 0.42 | 0.63 | — | 0.80 |

The finisher has no combo window (last step); its `ComboWindowClose` only opens the recovery cancel (`PLAYER_COMBO.md`). Its first fast phase (0.08–0.26) is the backswing behind the body; the
strike is the overhead diagonal 0.42–0.61 (blade tip in front from 0.45).

Attack_1 window from hand-speed sampling (original clip): wind-up overhead until ~0.30, downswing 0.33–0.50, follow-through to ~0.58.
In the refined clip the blade is horizontal and in front 0.44–0.55, inside the unchanged 0.33–0.58 window.
`ComboWindowClose` must stay **below `Attack_1_State → CombatIdle` exit time (0.81)** — a press after it spends stamina
(×2 here) on a trigger the exiting state never takes. 0.58 + the 0.42 s combo blend ends past 0.81, which is safe only
because the combo transition is listed first with `interruptionSource = Destination` (the outgoing exit can't cut it).
`2hand idle` / `2hand blocking` have `loopTime` on. All 2H clips: root rotation / Y / XZ baked into pose.
`2hand idle` (Mixamo) is a front guard like the attacks. Meshy text-to-motion replacements were tried and rejected
(`MESHY_PROMPTS.md` → Verdict).

**Attack_1 plays `2hand attack refined.fbx`** (take `Scene`, frames 1–56, same 1.83 s, avatar / root-bake / events as
`2hand attack.fbx`). The original is unreferenced but kept as the edit source; revert = point the override slot back to it.
Blender edit (2026-11-03): only the left arm, 0.38–0.62, so the blade (left → right hand line, `TwoHandGripConstraint`)
runs horizontal through the cut: pitch +18…+35° → +1…+7°, tip 1.23–1.58 → 0.91–1.11 m. Body and right hand within 2.4 mm.

**Attack_2 plays `2hand attack 2 refined.fbx`** (take `Scene`, frames 1–39, same 1.27 s, avatar / root-bake / events as
`2hand attack 2.fbx`; the original is kept as the edit source, revert = point the override slot back to it).
Blender edit (2026-11-03, `configs/2hand_attack_2.json`, `pitch_curve`): left arm only, 0.18–0.69, yaw kept. The source swing was a near-flat descending sweep (blade never raised, flat behind the head at 0.42,
plane tilted 21°, tip above the hands +11…+19° at 0.45–0.50). Refined: blade lifts over the right shoulder from 0.21
(+48° at 0.42, tip 2.6 m), pitch falls monotonically to −38° at 0.605, then eases back to the source by 0.69; plane tilt
42°. Body and right socket within 2.7 mm. Known limit: the hands are at 0.88 m when the blade points forward (0.55), so
the tip passes the centre low (~0.7 m); raising it needs the body / right arm.

### Blender edit recipe (weapon line of a 2H clip)

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

### Unarmed (`Unarmed/`)

Custom timings tuned to each arc (faster connect, different wind-up) — **never copy the sword timings**.

| Clip | State | HitboxEnable | HitboxDisable | ComboWindowOpen | ComboWindowClose |
|------|-------|-------------|--------------|-----------------|-----------------|
| `lead jab.fbx` (`lead jab`, 1.80 s, frames 0–54) | Attack_1 — left hand | 0.185 (f10) | 0.315 (f17) | 0.315 | 0.741 (f40) |
| `Hook.fbx` (`Hook`, 1.83 s, frames **10–65**) | Attack_2 — right hand | 0.309 (f27) | 0.436 (f34) | 0.436 | 0.764 (f52) |
| `Uppercut.fbx` (`Uppercut`, 1.33 s, frames 0–40) | Attack_3 — left hand, finisher | 0.275 (f11) | 0.400 (f16) | — | 0.70 (f28) |

Spec: `spec-unarmed-combo-rework`. Frame numbers are source frames; normalized = (frame − firstFrame) / clip frames.
Measured on the Player rig (fist hitbox, player-local, 2026-11-16):

- **lead jab** — left fist speeds up from f10 (peak 4.8 m/s at f12), full extension f14–15 at (0.20, 0.96, 0.79).
- **Hook** — right fist swings wide from f26 (10 m/s at f28–29), crosses the front f29–33, ends at (0.25, 1.45, 0.70).
  The first 10 source frames (slow guard-to-wind-up) are trimmed in the importer: the trimmed start has hip yaw 78°,
  the lead jab's yaw at its combo window (77°), and the hit lands 0.57 s after the chain instead of 0.90 s.
- **Uppercut** — left fist drops back f7–9, drives up f11–16 (12 m/s at f14), in front at f12–15 (z 0.36–0.65), then
  goes overhead. Its start is not trimmed: the 0.42 s combo blend covers the drop-back.

**Hit event ids:** the three clips' `HitboxEnable` / `HitboxDisable` carry a string — `UnarmedLeft` (lead jab,
Uppercut) or `Unarmed` (Hook) — the NPC hitbox id. The player ignores it and reads the hand from
`CombatConfigSO.unarmedAttacks` (`Scripts/Combat/PLAYER_COMBO.md`).

**Import settings (all three; Loop Pose off):** root rotation baked, **Original** orientation; root Y baked, Original; root **XZ not
baked**. The Mixamo boxing clips lunge up to 0.5 m and step back; with XZ unbaked and `applyRootMotion` off the Animator
drops that travel, so the punch is truly in place and the scripted `AttackStep` is the only movement. Orientation based
on **Body** turned the bladed stance square and sent the jab 35° to the left.

### Unarmed foot step (Blender leg edit, spec-unarmed-attack-foot-step)

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
- A failed import leaves the open `Humanoid_Template` with an **empty** attack state at runtime (state length 1 s, no clip)
  although the asset is fine: reassign `state.motion` (null, then the clip) — the YAML doesn't change.
- Play-mode foot checks: pause + `EditorApplication.Step()` is synchronous (Δt 0.02 s, ignores `captureDeltaTime`); set
  `Animator.cullingMode = AlwaysAnimate` or an unfocused editor never moves the bones; test on open ground (the
  `PlayerAttackStep` ledge probe can stop the capsule early and fake a foot slide).

### Unarmed guard idle (`Unarmed Idle Guard.anim`, spec-unarmed-idle-posture-fix)

`CombatIdle` (UpperBody layer) plays **`Unarmed Idle Guard.anim`**, baked from `Unarmed Idle.fbx` (Mixamo, 89 frames,
loop, root rotation baked on Body orientation, Y baked on Original). The FBX itself is unreferenced, but keep it: it's
the bake source. Played raw, the guard looked wrong in game, for two reasons. Distances below are in **body heights**
(head bone height above the feet), measured from the chest bone in player-local space (x right, y up, z forward).
- **Arm proportions:** humanoid retargeting copies the arm *angles* (within 3°), but the Player's forearm/upper-arm ratio
  (1.1) is far shorter than the source rig's (1.7), so the fists sat 0.12 instead of 0.22 in front of the chest.
- **Mask:** `UpperBodyMask` takes the hips from locomotion, which drops the source's forward lean (11.6° → −4.6°).

The bake copies every curve and the clip settings, and adds **constant** muscle offsets: Spine / Chest / UpperChest
Front-Back −0.22; Left Arm Front-Back −0.38, Down-Up +0.42, Forearm Stretch +0.38; Right Arm Front-Back −0.34, Down-Up
+0.11, Forearm Stretch +0.37; Neck Nod Down-Up +0.36, Head Nod Down-Up +0.18 (added 2026-11-16 with the block guard: the
source looks 36° down, now ~14°). Result on the Player (base `Idle` + mask, mean of t = 0.2 / 1.0 / 1.8 / 2.6 s):
lean 9.7°, fists L (−0.18, 0.03, 0.23), R (−0.04, 0.06, 0.20) vs source (−0.18, 0.03, 0.23), (−0.05, 0.06, 0.20).
Over `Walking` / `Running` the fists keep ≥ 0.18 forward and the shoulder line stays within ±8° (the lean rises to
15–20°, the run's own hip tilt). No committed bake tool: re-bake from `execute_code` and re-fit the offsets by
coordinate descent against the source pose (instantiate the FBX for the target, `Player.prefab` + `AnimationLayerMixerPlayable`
with the mask for the result) if the FBX or the player body changes. The Sword / TwoHanded overrides are keyed on the `.anim`.

### Unarmed block high guard (`Unarmed Block Guard.anim`, spec-unarmed-high-guard-block)

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
Result on the Player (measured 2026-11-16 on the current body; base `Idle` + mask, from the head bone, metres): index knuckles L (−0.05, +0.01, +0.26),
R (+0.07, +0.01, +0.22) — in front of the mouth / chin; elbows L (−0.17, −0.34, +0.05), R (+0.18, −0.35, +0.02).
Over Walking / Running / Walking Backwards the knuckles stay ≥ 0.20 in front of the head bone.
Fitting: coordinate descent per arm (steps 0.1 → 0.02, shoulder offsets capped at ±0.4, every muscle kept within ±1)
on knuckle (weight 3), wrist (1) and elbow (0.5) targets, then front / side renders against the refs. Gotchas:
- Fit fist targets **after** fixing the head pitch: a hanging head moves the face 5–10 cm forward / down into the fists.
- Don't touch the finger curves: the source fingers are already curled past −1 (clamped). "Closing" them to −0.75
  opens the fists. The Mixamo rig has index fingers only, so a fist never looks fully closed.
- Muscle offsets are unbounded in the solver unless capped: an uncapped fit pushed Right Shoulder Front-Back to +2.05.
Re-bake from `execute_code` like the idle guard (copy curves + settings, add the constants) if the idle guard changes.

**Tuning:** `HitboxEnable` ≈ when the weapon reaches the target zone (~0.2–0.25), `HitboxDisable` ≈ when it
retracts (~0.35–0.5). Thrust/jab clips need an earlier `HitboxEnable` than overhead swings. Retiming
`ComboWindowOpen` also moves the combo blend start — re-check the transition rule in
`../../Controllers/CLAUDE.md`.

---

## Draw / Sheathe Clip Events

Each draw / sheathe clip carries **one** `WeaponSwap` event (normalized, FBX importer) — the frame the hand reaches the
hilt / sheath; the weapon visual changes socket there. First guesses, tune in play mode:

| Clip | State | WeaponSwap |
|------|-------|-----------|
| `Unarmed/draw unarmed.fbx` (`Draw unarmed`, 1.00 s) | Draw_State (base — unarmed) | 0.50 (fists reach the guard at f15–18) |
| `Unarmed/sheath unarmed.fbx` (`sheath unarmed`, 1.07 s) | Sheathe_State (base — unarmed) | 0.50 (hands down by f15–18) |
| `one handed/Draw Sword.fbx` (`draw sword`) | Draw_State (sword override) | 0.40 |
| `one handed/Sheath Sword.fbx` (`sheath sword`) | Sheathe_State (sword override) | 0.65 |
| `two handed/2hand draw.anim` (1.40 s) | Draw_State (2H override) | 0.55 s (seam) |
| `two handed/2hand sheath.anim` (1.97 s) | Sheathe_State (2H override) | 0.78 s (seam) |
| `Bow/Standing Equip Bow.fbx` (`Draw Bow`) | Draw_State (bow override) | 0.45 |
| `Bow/Standing Disarm Bow.fbx` (`Sheathe Bow`) | Sheathe_State (bow override) | 0.65 |

A missing event is not fatal — the state exit completes the swap, just late (end of clip). Unarmed has no weapon
visual: its `WeaponSwap` only releases the switch lock, so attacks unlock there.

### Two-part draw / sheathe → one baked `.anim`

Draw / sheathe is **one** UpperBody-layer state with one clip, so a vendor draw split in two parts (reach to the back →
bring forward) is merged, not given a second state. No committed bake tool yet — the clips were baked once from `execute_code`. Recipe: for every
`AnimationUtility.GetCurveBindings(part1)` (Humanoid muscle curves) copy part 1's keys, then part 2's keys shifted by
`part1.length + 0.1 s`; set Auto tangents on the two seam keys (smooths the small pose jump); copy part 1's
`AnimationClipSettings` (loopTime off, `stopTime` = total); one `WeaponSwap` event **in seconds** at the middle of the gap
(the hand is at the back there). Re-bake after changing the source parts' import settings. Keep the longest clip under
`CombatConfigSO.weaponSwitchTimeout` (2.5 s at speed 1).

---

## Code Review Checklist — Combat Clips

| Severity | Pattern |
|----------|---------|
| HIGH | Attack clip missing one of the four events |
| HIGH | `functionName` typo in a `.meta` event — silent miss |
| MEDIUM | Unarmed clip given the sword timings (0.25 / 0.50 / 0.50 / 0.90) |

---

## Execute Stomp (`execute stomp.fbx`, spec-execute-unconscious-npc)

Mixamo, Humanoid (own avatar), 56 frames @ 30 fps = **1.867 s**, root rotation / Y / XZ baked into pose, no loop. Events
(normalized): `ExecuteImpact` **0.393** (frame 22 — the right foot touches down; it rests on the body until frame 28) and
`ExecuteEnd` 0.95. Landing point ≈ **(−0.45, 0, 0.88) m** in player-local space, mirrored in
`CombatConfigSO.executeFootOffset` / `executeClipDuration` — re-measure both (sample `RightFoot` / `RightToes` with an
`AnimationClipPlayable` on the FBX avatar, × 100: the avatar is in cm) if the clip or its trim changes.
