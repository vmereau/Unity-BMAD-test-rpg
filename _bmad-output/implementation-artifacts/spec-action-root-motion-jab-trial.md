---
title: 'Action root motion trial: unarmed jab (Meshy clip + OnAnimatorMove)'
type: 'feature'
created: '2026-11-23'
status: 'done'
baseline_commit: '3c0b51e9'
context:
  - '{project-root}/docs/IDEAS.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md'
  - '{project-root}/Tools/Blender/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Attack movement has two sources of truth: the scripted `AttackStep` push and feet retouched by hand in Blender to match it. They drift apart in blends and whenever Animator speed changes, so feet slide.

**Approach:** Trial the hybrid from `docs/IDEAS.md` on one attack, the player's unarmed jab. A Meshy text-to-motion jab that really steps forward is fitted in Blender, and a new `ActionRootMotion` component reads the clip's root displacement at runtime (`OnAnimatorMove`), filters it and moves the `CharacterController`. Everything else keeps the scripted step.

## Boundaries & Constraints

**Always:**
- Runtime `OnAnimatorMove` (user decision 2026-11-23), not extracted curves. Locomotion stays code-driven.
- Root motion applies only to an attack whose `AttackStep.rootMotionScale` > 0 (jab only in this spec); for it `PlayerAttackStep` does not run. All other attacks, weapons and NPCs behave exactly as before.
- Filter: horizontal displacement only, × `rootMotionScale`, same ledge stop as `PlayerAttackStep`, same stop conditions (not attacking, dodging, busy, airborne). Rotation from the clip is never applied (steering / lock-on own facing).
- Clip source: Meshy generation v2 (task `01a209e7-cb7e-74f0-b0c4-dc1e619fc3bf`, 30 of the 40 approved credits spent, v1 / v3 rejected). Fitted in Blender: travel straightened to pure forward, stride scaled from 1.25 m to **0.40 m**, tail trimmed where the lead foot lifts again (~f54), retimed so the hit lands at most 0.40 s after state enter.
- The new clip carries all four attack events with the `UnarmedLeft` id, root XZ **not** baked, rotation and Y baked.
- The old `lead jab.fbx` stays in the project, unreferenced, so reverting is one controller slot + one config value.
- Log all three prompts and verdicts in `MESHY_PROMPTS.md`.

**Ask First:**
- Any further Meshy generation (credits).
- Changing jab damage, combo timings of Hook / Uppercut, or the 0.40 m travel by more than ± 0.10 m.
- Extending to any other attack, to NPCs, or enabling `applyRootMotion` on NPC Animators.

**Never:** motion warping / lock-on distance correction, dodge / execute / stagger root motion, removing `PlayerAttackStep` or `attackSteps`, editing Hook / Uppercut clips.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Single jab | Player in unarmed guard, attack | Player ends 0.40 m ± 0.05 ahead; planted foot world drift < 3 cm outside blends | Re-fit in Blender |
| Ledge | Jab facing a drop > 0.75 m | Movement stops at the edge, clip keeps playing | N/A |
| Cancel | Dodge / block / jump mid-jab | Root motion stops the same frame | N/A |
| Animator slowed | `animator.speed` 0.5 during the jab | Same total travel, feet still planted | N/A |
| Other attacks | Hook, Uppercut, sword, 2H | Scripted step unchanged, no double movement | N/A |
| NPC jab | NPC plays the new clip | Scripted step still drives it; visual result reported | Report, no fix here |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/Weapons/AttackStep.cs` -- step struct; gains `rootMotionScale` (0 = scripted step)
- `Assets/_Game/Scripts/Player/PlayerAttackStep.cs` -- scripted push + `HasGroundAhead` ledge probe to share
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs:1056` -- `OnAttackStateEntered` → `_attackStep.Begin(ResolveAttackStep(index))`
- `Assets/_Game/Data/Config/CombatConfig.asset` -- `unarmedAttacks[0].step` (0.30 / 0.07 / 0.43); also read by `EntityAttackStep` for NPCs
- `Assets/_Game/Prefabs/Player/Player.prefab` -- Animator on the root (`applyRootMotion` 0), `CharacterController`
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `Attack_1_State` motion slot, shared with NPCs
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/` -- `lead jab.fbx` (current), new clip goes here
- `Tools/Blender/unarmed_foot_step.py` -- import / IK / bake / export pattern to reuse
- Meshy output: SMPL-H skeleton (`Pelvis`, `L_Hip`, `L_Knee`, `L_Ankle`, `L_Foot`, `L_Collar`, `L_Shoulder`, `L_Elbow`, `L_Wrist`…), armature scale 0.01, 60 frames @ 30 fps, with a mannequin mesh

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/meshy_jab_fit.py` + `configs/unarmed/meshy_jab.json` -- import v2, straighten and scale the pelvis XY travel and both foot paths (leg IK, bake), trim, retime, export armature-only FBX -- reproducible fit
- [x] `Unarmed/lead jab step.fbx` (+ `.meta`) -- import: Humanoid avatar from this model (explicit SMPL-H mapping), root rotation / Y baked on Original, XZ unbaked, four events via Hitbox Tuner -- the trial clip
- [x] `AttackStep.cs` -- add `rootMotionScale` -- per-attack switch and tuning
- [x] `Assets/_Game/Scripts/Player/ActionRootMotion.cs` (+ pure `ActionRootMotionMath`) -- `OnAnimatorMove` on the Player root: when active, filter `animator.deltaPosition` and `CharacterController.Move`; `Begin(scale)` / stop conditions; ledge probe shared with `PlayerAttackStep` -- the hybrid
- [x] `PlayerCombat.cs` -- on attack-state enter route to `ActionRootMotion` when the step's scale > 0, else `PlayerAttackStep` (each stops the other) -- single owner per attack
- [x] `Player.prefab`, `Humanoid_Template.controller`, `CombatConfig.asset` -- add the component, point `Attack_1_State` to the new clip, set jab step `rootMotionScale` 1 and `distance` 0.40 (NPC scripted push ≈ clip travel)
- [x] `Assets/Tests/EditMode/ActionRootMotionMathTests.cs` -- vertical stripped, scale applied, zero scale = no move
- [x] `PLAYER_COMBO.md`, `Combat/CLAUDE.md` (clip table), `Controllers/CLAUDE.md`, `.claude/rules/attack-pipeline.md`, `MESHY_PROMPTS.md`, `Tools/Blender/README.md`, `docs/IDEAS.md` (entry → `designé`, link this spec) -- rule exception and new pipeline

**Acceptance Criteria:**
- Given the player jabs unarmed in play mode, when the clip plays, then the capsule moves only by the filtered clip displacement and `PlayerAttackStep` stays idle.
- Given jab → Hook → Uppercut, when chained, then the combo still completes and Hook / Uppercut move by their scripted steps.
- Given the EditMode suite, when run, then it passes with the new tests.
- Given the trial, when finished, then the user gets a feel report: travel, foot drift, hit time, lock-on behaviour, NPC look.

## Design Notes

A script implementing `OnAnimatorMove` on the Animator's GameObject takes over root motion ("Handled by Script"), so `applyRootMotion` stays off in the prefab and NPCs are untouched. `deltaPosition` is the blended delta of every playing state: in-place clips contribute 0, and during the idle → jab blend the jab's delta is weighted down, which is the wanted behaviour. Unity Humanoid retargets the SMPL-H clip onto the Mixamo player body, so no bone retarget is needed in Blender, only the stride fit.

## Implementation Notes (2026-11-23)

**Measured in play on the final code (Player, StartingTown ground, driven through `PlayerCombat.TryAttack`):**

| Case | Result |
|------|--------|
| Jab from the guard, Animator speed 1 | 0.39 m travel (0.42 peak, the clip settles back), 1.0 cm max sideways, planted lead foot within 2.1 cm |
| Jab, Animator speed 0.5 | 0.40 m travel, 3.0 cm max sideways, planted lead foot within 1.7 cm |
| Hit time | fist furthest at 0.40 s, 0.63 m ahead of the root (hit window 0.29–0.44 s) |
| Cancel (`CancelAttack` at 0.30 s) | movement stops on that frame |
| Jab → Hook → Uppercut | completes; Hook / Uppercut move by `PlayerAttackStep`; chaining stops the jab's travel at the Hook's enter (0.32 m covered) |
| Recovery restart Uppercut → jab | no travel during the 0.15 s blend, then the jab moves straight (3 mm sideways) |
| `feetPivotActive` | 1 before and after every attack |

EditMode: 1903 / 1903 on the final code.

**Not verified — acceptance criterion 4 is only partly met:**
- **Ledge stop:** not tested in play (shared probe with `PlayerAttackStep`; a failed probe now skips the frame's travel).
- **Lock-on:** not tested. The clip's rotation is never applied, so facing stays with `PlayerController`.
- **Cancel by dodge / block / jump:** covered by the same polled flags as the tested `CancelAttack`, not run one by one.
- **NPC look:** not observed in play. NPCs play `lead jab step` with the scripted step (0.40 m, constant speed, 0.07–0.50 s).
  Against the clip's own travel curve that is at most ~4 cm apart, so some foot slide is expected. The fist reaches
  0.63 m ahead of the root instead of 0.79 m (old jab), and the clip is 1.07 s instead of 1.80 s.

**Deviations and additions:**
- **`Animator.feetPivotActive`** is set to 0 by `ActionRootMotion.Begin` and restored by `Stop`. At 1 (Unity's default) every
  blend added root movement (+ 0.07 m sideways, + 0.06 m forward into the jab).
- **Two `Move` calls per frame** (horizontal, then 2 cm down): one combined move slid the capsule on slopes; a horizontal
  move alone left `isGrounded` false for the whole swing.
- **Avatar:** the clip copies the avatar of `Unarmed/meshy smplh rig.fbx` (T-pose export of the raw download) instead of
  building it "from this model": a Blender-exported clip has no T-pose.
- **Fit values:** source frames 7–53 of the stripped file (raw 6–52), speed × 1.4 → 33 frames / 1.07 s; `travel_m` 0.42
  (clip space); pelvis dip × 0.5, foot lift × 0.35. Import root Y offset 0.042.
- **Jab timing changed:** clip 1.80 s → 1.07 s, hit opens 0.33 s → 0.29 s, combo window 0.57–1.33 s → 0.44–0.83 s.
- **Overrides re-keyed** (Sword / TwoHanded) and the stale `lead jab` entries deleted; `UnarmedAttackTests` updated.
- **`CombatConfig.asset` reserialized** on save: two fields that no longer exist in `CombatConfigSO` dropped, nine
  existing-default fields written. No value changed besides the jab step.
- **Known visual limit:** the trailing foot drags 5–7 cm above the ground for most of the clip (source behaviour).
- Extra file: `.claude/rules/meshy-mcp.md` (Meshy MCP quirks, user request during this session).

**Review (three reviewers, no high-severity finding).** Patched: fallback to the scripted step when `Begin` can't run;
outgoing attack clip's travel ignored on an attack → attack blend; ledge probe per frame; `feetPivotActive` restored;
stick move on zero-delta frames; fit script guards (no net travel, knee fallback); stale override entries; doc
contradictions. Deferred: `deferred-work.md` → *From spec-action-root-motion-jab-trial*.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play mode, frame-stepped jab: log root and both foot bones' world XZ per frame; check the matrix rows (travel, drift, ledge, cancel, half speed).

## Suggested Review Order

**The hybrid: clip travel read and filtered by code**

- Entry point: per-frame filter, stop conditions, attack-to-attack guard, two moves.
  [`ActionRootMotion.cs:75`](../../Assets/_Game/Scripts/Player/ActionRootMotion.cs#L75)

- `Begin` turns the feet pivot off and reports whether it can run.
  [`ActionRootMotion.cs:51`](../../Assets/_Game/Scripts/Player/ActionRootMotion.cs#L51)

- One mover per attack state; falls back to the scripted step.
  [`PlayerCombat.cs:1063`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1063)

- Per-attack switch: 0 keeps the scripted step.
  [`AttackStep.cs:24`](../../Assets/_Game/ScriptableObjects/Items/Weapons/AttackStep.cs#L24)

- Ledge probe made static and shared; heading follows the delta.
  [`PlayerAttackStep.cs:77`](../../Assets/_Game/Scripts/Player/PlayerAttackStep.cs#L77)

- Pure filter: horizontal only, scaled.
  [`ActionRootMotionMath.cs:12`](../../Assets/_Game/Scripts/Player/ActionRootMotionMath.cs#L12)

**The clip: Meshy source fitted in Blender**

- Fit pipeline: sample, straighten, scale displacements, re-solve legs, export.
  [`meshy_jab_fit.py:127`](../../Tools/Blender/meshy_jab_fit.py#L127)

- Heights scale about the pelvis start and each ankle's lowest point.
  [`meshy_jab_fit.py:167`](../../Tools/Blender/meshy_jab_fit.py#L167)

- Bones are aimed through their child joint, not their own Y axis.
  [`meshy_jab_fit.py:116`](../../Tools/Blender/meshy_jab_fit.py#L116)

- Final fit inputs (frames, speed, travel, height scales).
  [`meshy_jab.json:1`](../../Tools/Blender/configs/unarmed/meshy_jab.json#L1)

- Import: XZ unbaked, Y offset, copied SMPL-H avatar, four events.
  [`lead jab step.fbx.meta:53`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/lead%20jab%20step.fbx.meta#L53)

**Data and wiring**

- Jab step: scale 1 for the player, 0.40 m scripted for NPCs.
  [`CombatConfig.asset:98`](../../Assets/_Game/Data/Config/CombatConfig.asset#L98)

- Override keys follow the new base clip.
  [`UnarmedAttackTests.cs:157`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L157)

**Docs and follow-ups**

- Rules of the component, the pivot and blend gotchas.
  [`PLAYER_COMBO.md:151`](../../Assets/_Game/Scripts/Combat/PLAYER_COMBO.md#L151)

- Meshy MCP quirks learned from the three generations.
  [`meshy-mcp.md:16`](../../.claude/rules/meshy-mcp.md#L16)

- Filter and step-switch tests.
  [`ActionRootMotionMathTests.cs:1`](../../Assets/Tests/EditMode/ActionRootMotionMathTests.cs#L1)
