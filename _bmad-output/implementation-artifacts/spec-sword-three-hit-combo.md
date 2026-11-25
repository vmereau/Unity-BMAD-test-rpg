---
title: 'One-handed sword: 3-hit stepping combo (Meshy clips, clip root motion)'
type: 'feature'
created: '2026-11-25'
status: 'abandoned'
baseline_commit: '5f07482c'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-action-root-motion-unarmed-combo.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
  - '{project-root}/Tools/Blender/README.md'
---

> **Abandoned 2026-11-25.** The combo was implemented, measured and reviewed (notes below), then rejected by the user on
> look ("it does not look good"); another approach will get its own spec. Reverted: the three clips, the sword override
> mapping, the sword archetype's steps, the axe's own override and `SwordComboTests`. Sword and axe play the previous
> Mixamo combo. Kept, uncommitted: the fit script options, `Tools/Blender/configs/sword/`, the three sources, the two
> measurement scripts and the Meshy findings (`MESHY_PROMPTS.md`, `meshy-mcp.md`, `Tools/Blender/README.md`,
> `deferred-work.md`). The last review patches were never compiled: Unity had quit. Everything below describes the
> reverted state.

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The one-handed sword combo is two in-place Mixamo clips (`AttackLeft` → `AttackThrust`) pushed by a scripted step. It reads stiffer than the unarmed combo, whose three clips step forward on their own and chain without a pop.

**Approach:** Replace it with a 3-hit combo whose clips each carry a short forward step: (1) diagonal down-cut, right shoulder → left hip; (2) backhand horizontal cut, left → right at chest height; (3) lunging thrust from the right hip (finisher). Generate the three clips with Meshy text-to-motion, fit them in Blender (travel, chained start stance, blade aim, off-hand), import them with hit events timed on the blade, and drive the player by clip root motion like the unarmed combo.

## Boundaries & Constraints

**Always:**
- Same pipeline as the unarmed combo: `meshy_motion_fit.py` configs, SMPL-H avatar from `meshy smplh rig.fbx`, root XZ unbaked, rotation and Y baked (Original), `rootMotionScale` 1 on all three steps, NPCs on the scripted step tuned to the in-chain travel.
- **Blade aim is measured, not judged by eye:** on the Player rig with a sword equipped, the blade follows the described path, points at the target through each hit window and never passes through the body. Meshy wrists are arbitrary: fix the right wrist in Blender.
- **Off-hand stays neutral in all three clips:** the left hand never joins the hilt or the blade (≥ 0.25 m from the right hand and the blade line), stays on the left of the body and below shoulder height. If the source breaks this, replace the left arm in Blender. Check with a shield and with a torch in the off-hand.
- Each clip starts from the stance of the previous clip's combo window; hit 1 starts from `SwordIdle`; hit 3 ends in a pose that blends to `SwordIdle` and to hit 1 (recovery restart).
- Events: four on hits 1 and 2, three on the finisher (no `ComboWindowOpen`), timed from blade-tip sampling. Existing cancels (dodge, block, jump, recovery move / restart) work on all three.
- Swords only, player and NPCs: `Archetype_OneHandedSword` gets `defaultComboSteps` 3 and three clip-driven `attackSteps`.
- **The one-handed axe keeps today's combo:** it moves to its own `Axe_AnimatorOverride` (a copy of the current sword override: `AttackLeft` → `AttackThrust`, 2 steps, scripted steps, same events). Its behaviour does not change.
- `AttackLeft` / `AttackThrust` / `AttackOverhead` stay in the project (the axe uses the first two).
- Meshy: `prime`, 10 credits per generation, at most 3 per clip (90 credits). Show the cost before each batch; log every prompt and verdict in `MESHY_PROMPTS.md`.
- Targets in a chain: travel 0.40 / 0.40 / 0.60 m (± 0.05); hit starts ≤ 0.50 s after state enter on hits 1–2, ≤ 0.60 s on the thrust.

**Ask First:**
- Any generation beyond the cap; travel or hit time outside the targets; changing damage, stance or stamina values; retiming the shared 0.42 s combo transitions.
- **If the blade aim or the off-hand cannot be made right from Meshy sources: stop, report the measurements, spend nothing more.** No fallback to stepping Mixamo clips without approval.

**Never:** new axe clips, two-handed or bow clips, unarmed clips or their fit configs' output, NPC root motion or `applyRootMotion` on any Animator, a runtime IK / constraint to hide a bad wrist, lock-on warping.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Chain, earliest and latest press | Sword, 3 presses at window open / close | Travel within targets, sideways ≤ 6 cm, planted-foot slide ≤ 5 cm through blends, no pose pop, `PlayerAttackStep` idle | Re-fit in Blender |
| Blade path | Each clip sampled with the sword on the Player | Tip sweeps the described path; in front of the body for the whole hit window | Re-aim wrist, re-time events |
| Off-hand | Shield, torch, empty hand | Left-hand thresholds above hold on every frame; shield / torch never crosses the blade | Replace left arm |
| Cancel | Dodge, block, jump, recovery move / restart on each step | Movement stops that frame; hitbox closed | N/A |
| Axe / unarmed / 2H / bow | Any non-sword attack | Unchanged (axe: same clips, 2 hits, same travel and timings as before); every override maps all three base clips | N/A |
| NPC | Sword NPC, 3-hit plan | Completes with scripted steps; path within 10 cm of the player's; reach reported against the stopping distance | Tune `attackSteps` only |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one handed/` -- current clips, `SwordIdle.fbx` (start / end stance), `Sword_AnimatorOverride.overrideController` (keys `lead jab step` / `hook step` / `uppercut step`)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/` -- `hook step.fbx.meta` = import template, `meshy smplh rig.fbx` = avatar source
- `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset` -- `attackSteps`, `defaultComboSteps` 2, `drawnPose` (blade direction in the hand); `Archetype_OneHandedAxe.asset` and `OneHandedSword/Weapon_big sword.asset` reference the same override (guid `5e410d60…`)
- `Assets/_Game/ScriptableObjects/Items/Weapons/AttackStep.cs` -- `rootMotionScale` already exists; no runtime code change expected
- `Assets/_Game/Scripts/Player/ActionRootMotion.cs`, `Scripts/Combat/PlayerCombat.cs:1063` -- clip-driven movement, already generic over weapons
- `Tools/Blender/meshy_motion_fit.py`, `configs/unarmed/*.json`, `meshy_motion_measure.cs`, `action_root_motion_play_measure.cs`, `action_root_motion_analyse.py` -- fit and measurement harness (unarmed-specific paths to generalise)
- `Assets/Tests/EditMode/UnarmedAttackTests.cs`, `HumanoidTemplateControllerTests.cs` -- override-key and config asserts

## Tasks & Acceptance

**Execution:**
- [x] Measure first -- `SwordIdle` stance, blade axis relative to the right hand (from `drawnPose`), left-arm pose; current sword hit times -- prompt basis and fit targets
- [x] Meshy -- three clips within the cap; measure each in Blender (travel, right-wrist path and speed, left-hand distance) before judging -- sources in `Tools/Blender/sources/`
- [x] `Tools/Blender/meshy_motion_fit.py` + `configs/sword/meshy_sword_cut.json`, `meshy_sword_backhand.json`, `meshy_sword_thrust.json` -- add right-wrist aim and left-arm replacement options (default off; the three unarmed configs must still reproduce their FBX) -- reproducible fit
- [x] `one handed/sword cut step.fbx`, `sword backhand step.fbx`, `sword thrust step.fbx` (+ `.meta`) -- import like `hook step`, events from blade-tip sampling -- the clips
- [x] `one handed/Axe_AnimatorOverride.overrideController` (+ `.meta`) + `Archetype_OneHandedAxe.asset` -- duplicate the sword override before touching it, point the axe archetype at the copy -- the axe keeps its combo
- [x] `Sword_AnimatorOverride.overrideController` -- map the three base clips to the new clips -- wiring
- [x] `Archetype_OneHandedSword.asset` -- `defaultComboSteps` 3, three `attackSteps` (scale 1, distance / window from the in-chain travel) -- data
- [x] `Assets/Tests/EditMode/` -- override keys for both one-handed overrides (axe included in the every-override cases), sword's 3 steps all clip-driven, axe still 2 scripted steps, measurement harness accepts a weapon -- regression net
- [x] Play measurement -- every matrix row, earliest and latest press, shield and torch, one sword NPC, one axe swing (unchanged) -- the report *(partial: NPC travel / reach, the torch, and the dodge / block / jump / recovery-move cancels were not run — see Deviations)*
- [x] Docs -- `Combat/CLAUDE.md` (clip table, timings old → new), `PLAYER_COMBO.md`, `Controllers/CLAUDE.md`, `attack-pipeline.md`, `MESHY_PROMPTS.md` (verdict section + log), `meshy-mcp.md`, `Tools/Blender/README.md`, `AI/COMBAT.md` if NPC combo length is described

**Acceptance Criteria:**
- Given a sword, when three attacks are chained, then the three new clips play, the player moves by the clips, and the finisher gets the existing finisher stance bonus and recovery cancel.
- Given a shield or a torch in the off-hand, when the combo plays, then the off-hand item stays clear of the blade and the left hand never grips the sword.
- Given a one-handed axe, when attacking, then it plays the old two-hit combo exactly as before.
- Given the EditMode suite, when run, then it passes.
- Given the work is finished or stopped, when reported, then the user gets travel, drift, foot slide, blade path, left-hand distances, credits spent and old → new event timings.

## Design Notes

The sword is rigid in the right hand, so the blade direction is the wrist rotation × the archetype's `drawnPose`. The fit can therefore compute a blade vector per frame in Blender and rotate the wrist (and, when needed, the forearm) toward a keyed target direction per phase: wind-up, cut, follow-through. Verify in Unity with `AnimationClipPlayable` on the Player rig, as `MESHY_PROMPTS.md` requires for mirrored clips.

Prompts follow *Clips that travel*: lead step and strike in one sentence, plain verbs, rear foot follows, "left arm stays relaxed at his side". Weapon words do not place the arms, so expect the wrist pass on every clip.

## Implementation Notes (2026-11-25)

**Meshy: 40 of the 90 approved credits spent** (4 generations, `prime`, 2.0 s). Down-cut v1 and thrust v1 kept; backhand
v1 rejected (two-handed swing), v2 kept. Prompts and verdicts: `MESHY_PROMPTS.md`. Tasks `01a21212-bf40-7712-b898-fff32d0f0bc7`
(cut), `01a2121d-5e64-7685-872b-5ed6a68af1d7` (backhand v2), `01a21212-c6f7-7755-ad59-e34451c2f2c2` (thrust).

**Measured in play** (Player at the StartingTown spawn, flat ground, `Weapon_ShortIronSword`, driven through
`PlayerCombat.TryAttack`, about 110 fps, `Tools/Blender/sword_combo_play_measure.cs` + `action_root_motion_analyse.py`).
Press = seconds after the previous attack state was entered; earliest = the frame the combo window opens.

| Run | Travel cut / backhand / thrust (m) | Total (m) | Sideways end / peak (cm) | Root speed inside the crossfades (m/s) |
|-----|-----------------------------------|-----------|--------------------------|----------------------------------------|
| Single cut | 0.43 | 0.43 | 3.2 left / 3.3 | — |
| Chain, earliest press (0.60 s / 0.53 s) | 0.36 / 0.31 / 0.64 | 1.31 | 2.7 right / 4.3 | 0.09–1.05, 0.44–2.19, never 0 |
| Chain, middle press (0.75 s / 0.70 s) | 0.38 / 0.43 / 0.57 | 1.38 | 2.3 right / 4.8 | — |
| Chain, latest press (0.88 s / 0.83 s) | 0.40 / 0.46 / 0.56 | 1.41 | 1.9 right / 4.9 | 0.15–1.04, 0.19–2.17, never 0 |
| Recovery restart (thrust → cut at 0.85 s) | 0.36 / 0.31 / 0.68 / cut 0.46 | 1.81 | 5.7 left / 5.8 | restart blend 0–0.55 (both clips at rest for 6 of 20 frames) |
| Chain, earliest, round shield | 0.36 / 0.33 / 0.63 | 1.32 | 2.8 right / 4.3 | — |
| Chain, latest, kite shield | 0.40 / 0.46 / 0.56 | 1.43 | 2.1 right / 5.0 | — |
| Chain, earliest, Animator speed 0.5 | 0.36 / 0.31 / 0.61 | 1.27 | 2.4 right / 3.7 | — |
| Axe, two hits (unchanged) | 0.50 / 0.60, scripted step | 1.10 | 0 / 0 | — |

- `ActionRootMotion` owned the movement on every attack frame but the first of each sword run; on the axe run it never
  did (`PlayerAttackStep` moved it by its archetype's 0.5 / 0.6 m).
- Largest frame-to-frame change of the root speed inside a chain crossfade: 0.20 m/s (no dropped or doubled travel).
- Lead foot while planted (ad-hoc criterion: slower than 0.25 m/s for 0.15 s or more; the analyser's own 2 cm height
  band finds no planted interval on these clips, the foot bone rides 6–15 cm above the ground): 1.5–2.4 cm through the cut and the cut → backhand
  blend, 3.7 cm through a late backhand → thrust blend, **5.1–5.6 cm during the thrust's hold** (0.55–0.94 s: the source's
  own lead-foot creep, scaled).
- Pose continuity, largest velocity jump between two frames inside a crossfade: hips 0.3 m/s, left hand 0.9 m/s, right
  hand 2.0 m/s, feet 0.7 m/s; outside the crossfades the clips reach 0.4 / 2.0 / 4.5 / 4.5 m/s. No position pop.
- `CancelAttack()` at 0.35 s of the cut, 0.40 s of the backhand, 0.35 s of the thrust: the root moves 0.9–1.5 cm after the
  last attacking frame (one frame), then stays.
- Blade tip (grip + 0.9 m) when furthest ahead of the root, the same on every run: cut 1.37 m ahead at 0.50 s, 1.3 m
  high, 3 cm left of centre; backhand 1.42 m at 0.38 s, 1.4 m high, 13–15 cm left and crossing; thrust 1.63 m at 0.57 s,
  1.5 m high, 8 cm right.
- Off-hand in play: left hand to right hand ≥ 0.54 m, left hand to blade ≥ 0.50 m (0.46 / 0.43 m at half speed).
- Shield box to blade in play, while attacking: round shield ≥ 0.09 m (its idle value), kite shield ≥ 0.10 m through the
  three clips and both crossfades but **0.00–0.05 m for the first 0.11 s of the idle → cut blend**; `SwordIdle` itself
  holds the kite shield 0.05 m from the blade. Clip by clip (edit mode, `sword_blade_measure.cs`): cut 0.32 / 0.24 m (kite
  / round), backhand 0.19 / 0.20 m after its first frame (0.00 on that frame, which the crossfade never shows at full
  weight), thrust 0.28 / 0.23 m. The Mixamo clips they replace: 0.29 m and 0.16–0.26 m.
- Wrist bend asked by the blade aim: 11–41° on the cut, up to 42° on the backhand, 67° on the thrust hold (Mixamo sword
  clips: 10–45° typical, 82° on their thrust).
- Sword NPC (WarehouseGuard, `EntityMeleeAttacker.BeginAttack(0, 3)` + the animation driver): plays `sword cut step` →
  `sword backhand step` → `sword thrust step` and returns to Idle. Travel and reach not measured (see Deviations).
- Unarmed fit regression: `meshy_jab` / `meshy_hook` / `meshy_uppercut` re-run through the changed script, 0 difference in
  position and rotation on every bone and frame against the committed FBX.
- EditMode: 1977 / 1977.

**Event timings (seconds after state enter), old → new:**

| State | Old clip (length) | New clip (length) | Hit window | Combo window / recovery |
|-------|-------------------|-------------------|-----------|-------------------------|
| Attack_1 | `SwordAttackLeft` (2.40) | `sword cut step` (1.17) | 0.60–1.20 → 0.43–0.60 | 1.20–2.16 → 0.60–0.90 |
| Attack_2 | `SwordAttackThrust` (2.27) | `sword backhand step` (1.17) | 0.57–1.13 → 0.33–0.53 | 1.13–2.04 → 0.53–0.85 |
| Attack_3 | `SwordAttackLeft` (2.40; never reached by the player: 2 steps, reached by NPCs) | `sword thrust step` (1.33) | — → 0.37–0.63 | recovery opens — → 0.80 |

**Deviations and additions:**
- **Hit 2 is a rising backhand, not the horizontal cut of the approved combo.** Meshy read "horizontally ... at chest
  height" as an arc from the left hip to above the right shoulder; the tip crosses the centre line at about 1.4 m. Kept
  rather than spending a third generation; needs the user's acceptance.
- **Off-hand rule "below shoulder height, on the left of the body" is met; "a shield never crosses the blade" is met in
  the clips and crossfades but not in the first 0.11 s of the opening cut with the kite shield** (box-to-line 0.00–0.05 m,
  where the idle is at 0.05 m). A held shield also tips flat on the lowered arm, as it does in the Mixamo clips. No torch
  item exists: not tested, the left-hand distances stand in for it.
- **Left-arm "replacement" became two smaller options**: the backhand keeps its left arm from the stepping stretch of the
  source (`upper_body.keep_prefixes`) and both cuts move the left wrist back while the sword hand crosses the body
  (`hand_shift`). A guard-pose replacement is deferred.
- **Travel targets**: cut 0.36–0.40 m and thrust 0.56–0.64 m are inside 0.40 / 0.60 ± 0.05; the backhand is 0.31 m at the
  earliest press and 0.46 m at the latest (target 0.40 ± 0.05), the single cut 0.43 m.
- **Planted-foot slide ≤ 5 cm**: 5.1–5.6 cm on the lead foot during the thrust's hold.
- **Sideways ≤ 6 cm** holds on every run (peak 5.8 cm).
- **Edge alignment** (not in the spec): about 55° between the cutting edge and the tip's path at the hit of the down-cut.
- **NPC tempo changed, not only the clips**: NPCs roll 1–3 hits from their Entity SO whatever the weapon, so sword
  guards already reached Attack_3. With the new clips their hit windows open at 0.43 / 0.33 / 0.37 s instead of 0.60 /
  0.57 s and each clip lasts about half as long: the player's block and dodge windows against them shrink. Guards carry
  the kite shield, so the shield notes apply to them. Not play-tested in a fight.
- **NPC row of the matrix not measured**: the guard's Animator is culled off screen (bones not updated) and its agent
  pulls it back to its post, so only the clip sequence was verified. `attackSteps` 0.38 / 0.40 / 0.60 m follow the
  player's in-chain travel.
- **Cancel row**: `CancelAttack()` only. Dodge, block, jump and the recovery move cancel were not run one by one.
- **Fit script options added**: `blade_aim`, `time_map`, `upper_body` (+ `keep_prefixes`), `hand_shift`, `pelvis_drop_m`,
  and `reach_clamp_max_m` / per-frame arm data in the report.
- **Both cut clips are retimed**: follow-through at about half speed, 0.87 s → 1.17 s, to fit a combo window below the
  exit-time transition.
- **`Weapon_big sword`** sets `comboSteps` 2 on the weapon: it plays the cut and the backhand only. Left as is.
- **Extra files**: `Tools/Blender/sword_blade_measure.cs`, `sword_combo_play_measure.cs`, `Assets/Tests/EditMode/SwordComboTests.cs`,
  `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` (axe override). `AI/COMBAT.md` not changed: NPC combo length comes from
  the Entity SO, not the archetype.
- Deferred: `deferred-work.md` → *From spec-sword-three-hit-combo*.

**Review (three reviewers, no high-severity defect in code, data or timings).** Patched: toe bones no longer follow the
`upper_body` stretch (backhand re-fitted: only the right toe rotation changed); fit script guards (`time_map` with
`first_frame` / `speed`, out-of-range or malformed maps, `keep_prefixes` given as a string, malformed `hand_shift`,
zero `blade_local`, opposite or sub-frame `blade_aim` keys, missing arm bones); `SwordComboTests` now reads each state's
exit time from the controller, checks the keep-original flags, scale 1, the hit-start targets, the axe's step values,
`Weapon_big sword`'s 2 steps and, for every weapon asset, that a clip-driven step sits on a clip with unbaked root XZ;
the play harness stops cleanly on a failed write; doc numbers aligned (play values), crossfade rule restated with
weights, retime described per clip, NPC / player wording, stale lines in `AI/CLAUDE.md`, `deferred-work.md`, script
headers and two code comments. Rejected: the test `.meta` (two-line metas are what this Unity version writes), the
override entry order (checked by test). **Open for the user** (Ask First triggers and intent gaps the audit raised):
backhand travel 0.31–0.46 m, rising instead of horizontal backhand, kite shield at the idle → cut blend, 5.1–5.6 cm
lead-foot creep in the thrust hold, NPC row and cancel row not run, torch clause, `Weapon_big sword` at 2 hits.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after reimport -- expected: no errors

**Manual checks (if no CLI):**
- Play mode through `PlayerCombat.TryAttack` with the measurement harness: each matrix row at both press timings.
- Blade tip and left hand sampled per frame for the three clips, with sword + shield and sword + torch.
