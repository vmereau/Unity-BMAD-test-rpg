---
title: 'Action root motion: full unarmed combo (Hook + Uppercut clips, seamless chain)'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: '32ca80e3'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/spec-action-root-motion-jab-trial.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md'
  - '{project-root}/.claude/rules/meshy-mcp.md'
  - '{project-root}/Tools/Blender/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Only the unarmed jab moves by its clip. Hook and Uppercut are Mixamo clips pushed by the scripted step, their starts were matched to the old jab, and `ActionRootMotion` drops the travel during attack → attack blends, so the combo pops, slides and loses distance when chained (0.32 m instead of 0.40 m on the jab).

**Approach:** Generate stepping Hook and Uppercut clips with Meshy, fit them with a generalised fit script that also matches each clip's start pose and foot stance to the previous clip's combo window, and let `ActionRootMotion` carry the blended travel through attack → attack crossfades. Seamless transitions matter more than the clips themselves.

## Boundaries & Constraints

**Always:**
- Player: `rootMotionScale` 1 on all three unarmed steps, root XZ unbaked, rotation and Y baked (Original), SMPL-H avatar copied from `meshy smplh rig.fbx`.
- Start matching is done in Blender (trim, pose-match the first frames, planted foot stays planted across the blend), not by regenerating. Hook starts from the jab's stance inside 0.44–0.83 s; Uppercut from the Hook's window; Uppercut ends in a pose that blends to the guard and to the jab (recovery restart).
- Travel is continuous through every attack → attack crossfade when both clips are clip-driven. A blend from an attack that is **not** clip-driven (weapon clips, scripted step) keeps today's protection against foreign travel.
- One fit script for all three clips (`meshy_jab_fit.py` generalised and renamed; the jab config must still reproduce `lead jab step.fbx`).
- NPCs keep the scripted step: `AttackStep.distance` / start / duration follow each new clip's travel curve. Re-measure fist reach and hit time per clip for them.
- New clips carry all four events (`Unarmed` on Hook, `UnarmedLeft` on Uppercut); Sword and TwoHanded overrides re-keyed, stale entries deleted, `UnarmedAttackTests` updated — same change.
- Old `Hook.fbx` / `Uppercut.fbx` stay in the project, unreferenced, for a revert.
- Meshy: `prime`, 10 credits each, at most 3 generations per clip (60 credits total). Log every prompt and verdict in `MESHY_PROMPTS.md`.
- Targets: travel Hook ≈ 0.35 m, Uppercut ≈ 0.50 m (current scripted values); hit time after state enter within ± 0.10 s of today (Hook 0.57 s, Uppercut 0.37 s).

**Ask First:**
- Any generation beyond 3 per clip / 60 credits.
- Travel or hit time outside the targets above; changing damage, stance or stamina values.
- If the transitions cannot be made clean with Meshy sources: stop, report what was found, spend nothing more.

**Never:** weapon attacks, lock-on warping, NPC root motion or `applyRootMotion` on any Animator, removing `PlayerAttackStep`, fixing the jab's trailing-foot hover unless it falls out of the generalised fit for free.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Single attack | Each step alone, flat ground | Travel within ± 0.05 m of its target, sideways ≤ 3 cm, planted foot slide ≤ 3 cm outside blends | Re-fit in Blender |
| Chain, latest press | Press at each window's close | Root speed never drops to 0 or jumps between frames inside the crossfade; planted foot slide ≤ 5 cm through the blend; no pose pop | Re-fit start match |
| Chain, earliest press | Press at each window's open | Same continuity; total combo travel reported against the sum of single travels | Re-fit / tune guard |
| Recovery restart | Uppercut → jab on `AttackCancel` | Travel continuous through the 0.15 s blend, jab still straight (≤ 3 cm sideways) | N/A |
| Non clip-driven → jab | Sword / 2H attack blending out, or scripted step | No foreign travel added, as today | N/A |
| Cancel / ledge | Dodge, block, jump, drop ahead mid-combo | Movement stops the same frame / at the edge on any of the three steps | N/A |
| NPC combo | NPC plays the three clips with scripted steps | Completes; reach and hit time reported per clip against the 1.3 m stopping distance | Report, tune `AttackStep` only |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Player/ActionRootMotion.cs:85` -- attack → attack skip guard to rethink; `Begin` / `Stop` hand-over between states
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs:1063` -- `BeginAttackMovement`: stops the component when a scripted step takes over
- `Assets/_Game/Scripts/Player/ActionRootMotionMath.cs` -- pure filter; home for any new pure blend rule
- `Assets/_Game/Data/Config/CombatConfig.asset:93` -- `unarmedAttacks[1..2].step` (0.35 / 0.27 / 0.37, 0.50 / 0.10 / 0.37, scale 0); defaults in `CombatConfigSO`
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `Attack_2_State` / `Attack_3_State` motions, combo blend 0.42 s, restart blend 0.15 s
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/` -- `lead jab step.fbx` (+ `.meta` = import template), `meshy smplh rig.fbx`, `Hook.fbx`, `Uppercut.fbx`
- `Combat/one handed/Sword_AnimatorOverride`, `Combat/two handed/TwoHanded_AnimatorOverride` -- keyed on `Hook` / `Uppercut`
- `Tools/Blender/meshy_jab_fit.py`, `configs/unarmed/meshy_jab.json`, `sources/` -- fit pipeline to generalise
- `Tools/Blender/unarmed_foot_step_measure.cs` -- per-frame foot sampling on the Player rig
- `Assets/Tests/EditMode/UnarmedAttackTests.cs:157`, `ActionRootMotionMathTests.cs` -- override keys, filter tests

## Tasks & Acceptance

**Execution:**
- [x] Measure first -- sample `lead jab step` on the Player rig at 0.44 s and 0.83 s (pelvis, both feet, hip yaw, fists) -- the start target for the Hook and the basis of the prompts
- [x] Meshy -- generate Hook (right hand) then Uppercut (left hand), each with a real step, within the credit cap; measure each in Blender before judging -- sources
- [x] `Tools/Blender/meshy_motion_fit.py` (renamed from `meshy_jab_fit.py`) + `configs/unarmed/meshy_jab.json`, `meshy_hook.json`, `meshy_uppercut.json` + `sources/` -- add start-stance / start-pose matching and an optional end-pose match, input guards from the deferred list; keep the jab output identical -- one reproducible fit
- [x] `Unarmed/hook step.fbx`, `Unarmed/uppercut step.fbx` (+ `.meta`) -- import like `lead jab step`, four events each via Hitbox Tuner -- the clips
- [x] `ActionRootMotion.cs` (+ `ActionRootMotionMath.cs`, `PlayerCombat.cs` if needed) -- apply the blended delta through clip-driven attack → attack transitions, keep the skip for non clip-driven outgoing states -- continuous travel
- [x] `Humanoid_Template.controller`, both override controllers, `CombatConfig.asset` + `CombatConfigSO` defaults -- new motions, re-keyed overrides, steps (scale 1, distance / window from the clips); retune combo transition durations only if the measurements ask for it
- [x] `Assets/Tests/EditMode/` -- `UnarmedAttackTests` keys and config asserts, new `ActionRootMotionMath` cases for the blend rule -- regression net
- [x] Play measurement -- every matrix row, earliest and latest press, plus one NPC combo -- the report
- [x] Docs -- `PLAYER_COMBO.md`, `Combat/CLAUDE.md` (clip table, timings old → new), `Controllers/CLAUDE.md`, `attack-pipeline.md`, `MESHY_PROMPTS.md`, `meshy-mcp.md`, `Tools/Blender/README.md`, `deferred-work.md` (close resolved items)

**Acceptance Criteria:**
- Given jab → Hook → Uppercut unarmed in play, when chained at the earliest and at the latest press, then all three steps move by their clips, `PlayerAttackStep` stays idle, and the matrix thresholds hold.
- Given a sword or two-handed weapon, when attacking, then behaviour is unchanged and every override maps all three base clips.
- Given the EditMode suite, when run, then it passes.
- Given the work is finished, when reported, then the user gets measured travel, drift, foot slide, transition continuity and the old → new event timings per clip.

## Design Notes

`animator.deltaPosition` is already the weight-blended delta of both states in a crossfade, so with both clips unbaked and straight the travel is continuous by construction: the guard only has to tell "outgoing state is clip-driven" from "outgoing state carries a dropped Mixamo lunge". A single scale per frame is enough while every unarmed step uses 1.

Meshy cannot start from a pose. The fit therefore takes a start target (foot positions relative to the pelvis, pelvis yaw) measured on the previous clip and blends the source's first frames onto it, releasing the correction on each foot only while that foot is lifted.

## Implementation Notes (2026-11-24)

**Meshy: 40 of the 60 approved credits spent** (2 generations per clip). v1 of both had the step and no punch; v2 of both
kept. Prompts and verdicts: `MESHY_PROMPTS.md`. Tasks `01a20aad-15fa-7053-9c64-3e5b934a300f` (hook),
`01a20aad-195e-7670-97b6-a6c3995a67a3` (uppercut).

**Measured in play** (Player at the StartingTown spawn, flat ground, driven through `PlayerCombat.TryAttack`, 120 fps,
`Tools/Blender/action_root_motion_play_measure.cs` + `action_root_motion_analyse.py`). Press = seconds after the previous
attack state was entered; earliest = the frame the combo window opens.

| Run | Travel jab / hook / uppercut (m) | Total (m) | Sideways end / peak (cm) | Planted-foot slide, outside blends / through a chain blend (cm) | Root speed inside the crossfades (m/s) |
|-----|----------------------------------|-----------|--------------------------|-------------------------------|----------------------------------------|
| Single jab | 0.40 (0.42 peak) | 0.40 | 0.9 / 1.3 | 1.6 / — | — |
| Chain, earliest press (0.46 s / 0.64 s) | 0.32 / 0.40 / 0.52 | 1.24 | 2.9 left / 5.7 | 2.3 / none detected | 0.21–1.00, 0.24–1.21, never 0 |
| Chain, middle press (0.65 s / 0.91 s) | 0.39 / 0.40 / 0.46 | 1.24 | 2.3 right / 5.7 | — / 1.9 | 0.16–1.00, −0.09–1.22 |
| Chain, latest press (0.82 s / 1.12 s) | 0.42 / 0.33 / 0.47 | 1.21 | 5.6 right / 6.9 | — / 3.1 | 0–1.01 (both clips at rest), −0.20–1.23 |
| Recovery restart, early (Uppercut → jab at 0.82 s) | 0.32 / 0.40 / 0.45 / jab 0.48 | 1.65 | 3.1 left / 5.7 | 1.6 / 2.3 | restart blend 0.42–0.81, never 0 |
| Recovery restart, late (press at 0.96 s) | 0.42 / 0.33 / 0.46 / jab 0.45 | 1.66 | 4.0 right / 7.1 | — / 3.3 | restart blend 0.34–1.14, never 0 |
| Chain, earliest, Animator speed 0.5 | 0.31 / 0.39 / 0.48 | 1.19 | 2.9 left / 5.6 | — / 3.9 | — |
| Chain, latest, Animator speed 0.5 | 0.37 / 0.31 / 0.43 | 1.12 | 1.7 right / 5.7 | — / 4.8 | — |

- Planted = within 2 cm of the foot's lowest height and slower than 0.3 m/s for 0.1 s or more; "—" = no such interval
  in that part of the run. A faster slide is not counted, so these are lower bounds.
- Largest frame-to-frame change of the root speed inside a chain crossfade: 0.12 m/s (no dropped or doubled travel).
- The jab's own share at the earliest press is still 0.32 m (the Hook state enters then); what changed is the 0.22 m
  travelled inside the jab → hook blend, where the old code moved 0 and the scripted hook step started at 0.27 s.
- Pose continuity: largest velocity jump between two frames inside a crossfade 0.4 m/s on the hips, 3.9 m/s on a hand,
  1.5 m/s on a foot; outside the crossfades the clips themselves reach 0.8 / 5.0 / 2.7 m/s. No position pop.
- `ActionRootMotion` owned the movement on every attack frame but the first of each run; `PlayerAttackStep` stayed idle.
- Cancel (`CancelAttack` at 0.33 s of the Hook, then of the Uppercut): the root stops on that frame.
- Cancel, then a fresh press 4 frames later while the Hook still blends out (after the review patch): the blend into
  the new jab travels 0.11 m and the jab covers 0.40 m.
- NPC (TentMate, unarmed, 3-hit combo started through `EntityMeleeAttacker.BeginAttack` + the animation driver): completes;
  scripted travel 0.35 (the jab's 0.40 m step is cut when the hook enters at 0.46 s) / 0.35 / 0.48 = 1.18 m; path within 6.8 cm of the player's clip-driven path at the same chain timing.
- Fist reach in play (player and NPC alike; the jab sampled alone gives 0.63 m at 0.40 s): jab 0.62 m ahead of the root at 0.37 s, hook 0.61 m at 0.55 s, uppercut 0.59 m at
  0.49 s (old Mixamo clips: 0.79 / 0.70 / 0.65 m).
- EditMode: 1907 / 1907 (re-run after the review patches).

**Event timings (seconds after state enter), old → new:**

| Clip | Length | Hit window | Combo window / recovery |
|------|--------|-----------|-------------------------|
| Hook → `hook step` | 1.83 → 1.63 | 0.57–0.80 → 0.47–0.63 | 0.80–1.40 → 0.63–1.15 |
| Uppercut → `uppercut step` | 1.33 → 1.07 | 0.37–0.53 → 0.40–0.55 | recovery opens 0.93 → 0.75 |

**Deviations and additions:**
- **I/O matrix "sideways ≤ 3 cm" is met at the end of a single jab only.** The hook leans: the root goes 6 cm sideways and
  comes back, and a chain ends 3 cm left to 6 cm right depending on the press. Cause: Unity roots a Humanoid clip on the
  centre of mass. Reduced from 13 cm with `travel_yaw_deg` 8 on the hook.
- **Travel targets are met in a chain, not by the clips alone.** Hook and Uppercut only ever play chained: there they
  travel 0.33–0.40 and 0.45–0.52 m (targets 0.35 and 0.50 ± 0.05; 0.31 and 0.43 at Animator speed 0.5). The clips
  themselves carry 0.42 and 0.60 m, because the 0.42 s combo blend (shared with the weapon combos, left untouched)
  weights their first frames down. The matrix row "each step alone" could only be run for the jab.
- **"Root speed never drops to 0 inside the crossfade" does not hold at a late press**: the travel is continuous (no
  frame dropped), but both clips are at rest at the start of a late jab → hook blend, and the hook's settle back moves
  the root backward at up to 0.2 m/s for a few frames of a late hook → uppercut blend (0.09 m/s at a middle press).
- **`uppercut step` carries three events, not four**: like the Uppercut it replaces, the finisher has no
  `ComboWindowOpen`.
- **Fit script options added** beyond start / end matching: `lateral_scale`, `travel_yaw_deg`, `backtrack_scale`,
  `facing_offset_deg`, `foot_shift`; all default off, the jab config reproduces `lead jab step.fbx` with 0 difference.
- **Uppercut has no start offset**: once the hook's rear foot was made to land on the uppercut's start spot
  (`end_match` on the hook), the natural start was within 7 cm over the whole window.
- **NPC step values follow the in-chain travel**, not the clip's own (hook 0.37 / 0.17 / 0.50, uppercut 0.48 / 0.15 / 0.50).
- **Combo transitions not retimed.** `Attack_2 → Attack_3` stays at 0.42 s.
- **Extra files:** `Tools/Blender/meshy_motion_measure.cs`, `action_root_motion_play_measure.cs`,
  `action_root_motion_analyse.py` (the measurement harness), `docs/IDEAS.md` (script path).
- **Not verified:** ledge stop and lock-on on the two new steps; dodge / block / jump cancels one by one; a sword or
  two-handed swing in play (override keys are tested); a scripted attack blending into a clip-driven one (no such pair
  in the data, pure rule tested); a real NPC fight (reach against the 1.3 m stopping distance); NPC foot slide measured
  directly. Listed in `deferred-work.md` → *From spec-action-root-motion-unarmed-combo*.
- **Start target of the Hook** (task "Measure first"), `lead jab step` on the Player, root-relative, x right / y up / z
  forward: at 0.44 s left foot (−0.14, 0.08, 0.36), right foot (−0.10, 0.18, −0.52), hips yaw 56°; at 0.63 s
  (−0.13, 0.08, 0.30) / (−0.06, 0.17, −0.48), 53°; at 0.83 s (−0.13, 0.08, 0.25) / (−0.03, 0.17, −0.45), 40°.

**Review (three reviewers, no high-severity finding).** Patched: the chain decision follows the last attack state
entered instead of the component's active flag (a cancel followed by a fresh press lost the new jab's travel during the
blend-out); `OnDisable` hands the feet pivot back; the stale-override test reads the serialized entries (it could not
fail); fit script guards (duplicate JSON keys, `backtrack_scale` range, horizontal `forward`, malformed foot specs) and
`net_travel_m` / `peak_travel_m` / `last_source_frame` in its report; analyser guards; stale or contradicting doc lines
(nine files). Deviations from the approved matrix that the first notes left out are now listed above. Deferred:
`deferred-work.md` → *From spec-action-root-motion-unarmed-combo*.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile and reimport -- expected: no errors

**Manual checks (if no CLI):**
- Play mode through `PlayerCombat.TryAttack`, per-frame log of root XZ, both feet and hips: check each matrix row at both press timings.
- `AnimationClipPlayable` sampling of the three clips on an NPC rig: fist reach ahead of the root and hit time.

## Suggested Review Order

**Continuous travel through the chain**

- Entry point: the blended delta is applied when the previous attack was clip-driven too.
  [`ActionRootMotion.cs:114`](../../Assets/_Game/Scripts/Player/ActionRootMotion.cs#L114)

- `Begin` hears about every attack state and remembers whether the last one was clip-driven.
  [`ActionRootMotion.cs:62`](../../Assets/_Game/Scripts/Player/ActionRootMotion.cs#L62)

- The caller now reports scripted attacks too (scale 0) instead of calling `Stop`.
  [`PlayerCombat.cs:1063`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1063)

- Pure rule: wait a blend out only when the outgoing attack was scripted.
  [`ActionRootMotionMath.cs:25`](../../Assets/_Game/Scripts/Player/ActionRootMotionMath.cs#L25)

- Disabled mid-swing: the feet pivot is handed back.
  [`ActionRootMotion.cs:94`](../../Assets/_Game/Scripts/Player/ActionRootMotion.cs#L94)

**The clips: one fit script, start stances matched**

- Fit pipeline; the new options all default to the jab's behaviour.
  [`meshy_motion_fit.py:200`](../../Tools/Blender/meshy_motion_fit.py#L200)

- Per-foot start / end offsets and the constant lane shift.
  [`meshy_motion_fit.py:169`](../../Tools/Blender/meshy_motion_fit.py#L169)

- Travel line turned without turning the body (centre-of-mass lean).
  [`meshy_motion_fit.py:218`](../../Tools/Blender/meshy_motion_fit.py#L218)

- Settle-back scaling of the pelvis after its furthest point.
  [`meshy_motion_fit.py:276`](../../Tools/Blender/meshy_motion_fit.py#L276)

- Hook inputs: rear-foot lane, start match on the jab's window, landing spot for the uppercut.
  [`meshy_hook.json:17`](../../Tools/Blender/configs/unarmed/meshy_hook.json#L17)

- Uppercut inputs: retimed × 1.8, ends in the jab's start stance.
  [`meshy_uppercut.json:1`](../../Tools/Blender/configs/unarmed/meshy_uppercut.json#L1)

- Hook import: XZ unbaked, copied SMPL-H avatar, four events.
  [`hook step.fbx.meta:35`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/hook%20step.fbx.meta#L35)

- Uppercut import: same settings, three events (finisher).
  [`uppercut step.fbx.meta:35`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/uppercut%20step.fbx.meta#L35)

**Data and wiring**

- All three unarmed steps: scale 1, NPC step values from the in-chain travel.
  [`CombatConfig.asset:93`](../../Assets/_Game/Data/Config/CombatConfig.asset#L93)

- Same values as class defaults.
  [`CombatConfigSO.cs:212`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L212)

- Override keys follow the new base clips; stale entries are read from the serialized list.
  [`UnarmedAttackTests.cs:171`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L171)

- Every unarmed step must be clip-driven, or the chain's travel is cut.
  [`UnarmedAttackTests.cs:105`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L105)

- Blend rule truth table.
  [`ActionRootMotionMathTests.cs:45`](../../Assets/Tests/EditMode/ActionRootMotionMathTests.cs#L45)

**Measurement harness**

- Play-mode run: presses at chosen clip times, one CSV row per frame.
  [`action_root_motion_play_measure.cs:37`](../../Tools/Blender/action_root_motion_play_measure.cs#L37)

- Travel, drift, crossfade speed, planted-foot slide, velocity jumps.
  [`action_root_motion_analyse.py:48`](../../Tools/Blender/action_root_motion_analyse.py#L48)

- Edit-mode sampler behind the stance offsets.
  [`meshy_motion_measure.cs:1`](../../Tools/Blender/meshy_motion_measure.cs#L1)

**Docs and follow-ups**

- Rules of the component: chains, what a crossfade does to travel, centre of mass.
  [`PLAYER_COMBO.md:170`](../../Assets/_Game/Scripts/Combat/PLAYER_COMBO.md#L170)

- Per-clip notes, old → new timings, how to match a chained clip's start.
  [`CLAUDE.md:150`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L150)

- Fit options and the four-step recipe for a new chained clip.
  [`README.md:26`](../../Tools/Blender/README.md#L26)

- Prompts that gave a punch and those that did not.
  [`MESHY_PROMPTS.md:140`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/MESHY_PROMPTS.md#L140)

- What is left open.
  [`deferred-work.md:1195`](../../_bmad-output/implementation-artifacts/deferred-work.md#L1195)
