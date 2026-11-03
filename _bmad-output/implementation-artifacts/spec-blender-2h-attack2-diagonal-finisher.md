---
title: 'Blender: turn the 2H finisher (attack 2) into a raised overhead diagonal cut'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: 'a4814de'
context: ['{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md', '{project-root}/Tools/Blender/README.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In `2hand attack 2` (Attack_2, finisher, 1.27 s) the blade (left → right hand line, Player rig) never rises. At
0.37–0.42 it lies flat or points down behind the head. During the strike it turns 155° in yaw while its pitch only goes from +19° to −15°. The swing
plane tilts about 15° from horizontal, and the tip rides above the hands (+11…+19°) at 0.45–0.50. It reads as a descending flat swing, not an overhead
diagonal chop.

**Approach:** Same Blender pipeline as Attack_1 (left arm IK only), but drive the pitch with a time curve and keep the
original yaw. The blade comes up over the right shoulder (about +40° at 0.42–0.45), crosses the centre around −10° (0.55) and ends low
left (about −35° at 0.60). Extend `twohand_blade_line.py` with an optional `pitch_curve`. Without it the script behaves exactly as now.

## Boundaries & Constraints

**Always:**
- Clip length, frame count (0–38 @ 30 fps), body, legs, root and right arm stay as in the source, within a few mm on the Player rig.
- `2hand attack 2.fbx` stays untouched.
- The new `2hand attack 2 refined.fbx` copies the source root-bake flags, avatar setup, compression and the 2 events, but keeps its own take (`Scene`) and frame range.
- `configs/2hand_attack_1.json` still reproduces `2hand attack refined.fbx` exactly (compare with the +1 frame re-import shift).
- Unity sampling sets `animator.fireEvents = false`.

**Ask First:**
- Switching `TwoHanded_AnimatorOverride` (the user A/Bs in game first).
- Any event retime > 0.05.
- Editing the right arm or spine if left-arm IK can't reach the targets.

**Never:**
- Change the clip length or `attackSteps`.
- Touch Attack_1 assets, idle, or `TwoHandGripConstraint` / code.
- Fix the backswing tempo (deferred).
- Commit `two handed/meshy test/`.
- Absolute speed thresholds in the matrix.

## I/O & Edge-Case Matrix

Normalized time; Unity Player rig; speeds per 1/30 s sample; "orig" = same sample in the source clip.

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Raise | 0.42–0.45 | blade pitch ≥ +30°, tip above head height | raise the curve / widen the ramp |
| Diagonal strike | 0.45–0.60 | pitch non-increasing (±2°/sample), ≥ +30° at 0.45 → ≤ −25° at 0.60; plane of blade directions 0.45–0.60 tilted ≥ 35° from horizontal (orig 15°) | adjust curve points |
| Untouched span | 0–0.18, 0.69–1.0 (renegotiated 2026-11-03: was 0–0.29) | blade direction within 2° of orig | fix the window edges |
| No extra jerk | whole clip | per sample tip speed ≤ orig + 8 m/s; strike peak ≤ 1.3 × orig peak (21 m/s) | soften ramp / move curve points |
| Grip | whole clip | hand distance within ±0.02 m of orig | `distance_scale` pass |
| Clipping | whole clip | blade (right socket → tip) ≥ 0.15 m from spine, head, legs | lower the raise |
| Elbow | IK active | no flip, elbow below / outside the hands | pole angle |

</frozen-after-approval>

## Code Map

- `Tools/Blender/twohand_blade_line.py` -- IK edit script; constant pull-down only (`max(0, unity − target)`)
- `Tools/Blender/configs/2hand_attack_1.json` -- regression reference; `README.md` -- run + sampling snippet
- `.../Combat/two handed/2hand attack 2.fbx` (+ `.meta`) -- source, events 0.42 / 0.63, settings to copy
- `.../Combat/two handed/TwoHanded_AnimatorOverride.overrideController` -- Attack_2 slot (switch only on approval)
- `.../Combat/CLAUDE.md` -- 2H table / notes / recipe; `_bmad-output/implementation-artifacts/deferred-work.md`

## Tasks & Acceptance

**Execution:**
- [x] `Tools/Blender/twohand_blade_line.py` -- add optional `pitch_curve` ([[t, deg], …], linear, clamped): signed delta = unity pitch − curve(t), so it can raise or lower; make `target_pitch_deg` required only when there's no curve -- diagonal needs a time-varying, two-way target
- [x] Regression -- run `2hand_attack_1.json` to a scratchpad output, compare every bone and frame against the committed refined FBX in Blender -- backward compatibility
- [x] `Tools/Blender/configs/2hand_attack_2.json` -- source pitches (39 values), window [0.18, 0.22, 0.61, 0.69], per-frame curve 0.211–0.632 (proxy-corrected, wanted ≈ +46° at 0.42–0.45 → −38° at 0.60), 3 `distance_scale` passes -- inputs
- [x] `.../two handed/2hand attack 2 refined.fbx` (+ `.meta`) -- export, import, copy importer settings / events -- the clip
- [x] Unity sampling -- per-frame blade table + speeds + clearances on both clips; iterate until the matrix holds -- evidence
- [x] `.../TwoHanded_AnimatorOverride.overrideController` -- Attack_2 → refined clip after user approval -- switch
- [x] `Tools/Blender/README.md`, `.../Combat/CLAUDE.md` -- `pitch_curve`, new config, Attack_2 row and note -- knowledge
- [x] `deferred-work.md` -- close "2H attack 2 blade check"; add "2H finisher backswing as fast as the strike (20 vs 21 m/s)" -- traceability

**Acceptance Criteria:**
- Given the refined clip on the Player rig, when sampled per frame, then every matrix row holds and body / right hand bones are within 3 mm of the original.
- Given the refined clip in Unity, when inspected, then it is 1.267 s, the Humanoid mapping is valid, events are 0.42 / 0.63, and net root XZ is 0.
- Given the user plays both clips, when they pick one, then the override points to it and the console shows no new errors or warnings.

## Spec Change Log

- 2026-11-03, review (all three reviewers): the "Untouched span 0–0.29" row failed — the raise had to start at 0.21 to
  meet the "no extra jerk" row (pass 1 with the 0.29 start: +10…+12 m/s at 0.34–0.37). User accepted the 0.21 start;
  row amended to 0–0.18 (measured ≤ 0.11°), 0.21–0.29 now up to 27° above the source. Task 3 / Design Notes updated to the
  final window and curve. KEEP: per-frame proxy-corrected curve, 0.18–0.69 window, the 3-pass `distance_scale`.
  Baseline note: the original plane tilt measures 21.4° with the multi-sample fit (the frozen row's 15° came from two end
  samples); the ≥ 35° threshold is unaffected.

## Design Notes

Yaw is kept per frame (horizontal component of the original line), so the sweep direction and hit window stay. Only the
tilt changes. Known limit: when the blade points forward (0.55) the hands are at 0.88 m, so with −10° the tip is ~0.7 m.
A higher impact would need the hands (right arm / body) retimed, which is out of scope. The mid-blade still crosses chest
height at 0.50–0.53. The raise starts at 0.21 (end of the backswing) and spreads ~75° over 7 frames; packed into
0.29–0.42 it added +10…+12 m/s tip speed over the original (pass 1).

## Verification

**Manual checks:**
- Original vs refined blade table, speeds and clearances in the review summary; Blender viewport screenshot at t ≈ 0.43 and 0.55.
- In play mode, the user does the A/B on attack 2.

## Review Outcome (2026-11-03)

Final evidence (Player rig, 39 samples, original → refined): pitch at 0.42 / 0.45 −5 / +11° → +48 / +46° (tip 2.6 m);
strike 0.45–0.60 +11…+19…−15° → monotonic +46 → −38°; plane tilt 21.4° → 42.4°; outside 0.18–0.69 ≤ 0.11°; tip speed
excess ≤ +7.5 m/s, strike peak 21.2 → 23.0 m/s; hand distance ≤ 0.008 m off; body + right socket ≤ 2.7 mm; blade clearance
≥ 0.35 m; elbow height vs hands within ~0.04 m of the source, bend smooth (no flip); 1.267 s, avatar valid, events 0.42 /
0.63, averageSpeed 0. Regression: `2hand_attack_1.json` → 0° vs the committed refined FBX.
Review patches: curve validation before the scene is cleared, ±89° clamp on curve pitch, explicit avatar mapping +
frame range 1–39 in the `.meta` (re-sampled, identical), Combat/CLAUDE.md placement / table row / tree, README clamp note,
config comment. Deferred: commit the extended Unity check. Rejected with evidence: HitboxEnable at 0.42 (source tip was
also behind at 0.42), avatar-setup mismatch (copied from source), `distance_scale` gap at frame 10 (within tolerance).
A/B: user played both in game and picked the refined clip ("refined is better"); Attack_2 slot (Unarmed Uppercut) now
points to it, original unreferenced; console shows no new errors or warnings.

## Suggested Review Order

**Clip switch**

- Attack_2 slot now plays the Blender-refined finisher.
  [`TwoHanded_AnimatorOverride.overrideController:17`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L17)

**Script: two-way pitch target**

- Entry point: curve replaces the constant; signed delta lets the edit raise the blade.
  [`twohand_blade_line.py:179`](../../Tools/Blender/twohand_blade_line.py#L179)

- Clamp keeps the kept-yaw rebuild valid when a raise nears vertical.
  [`twohand_blade_line.py:183`](../../Tools/Blender/twohand_blade_line.py#L183)

- Config validated before `_clear_scene` wipes the open Blender scene.
  [`twohand_blade_line.py:122`](../../Tools/Blender/twohand_blade_line.py#L122)

- Piecewise-linear lookup, held at both ends.
  [`twohand_blade_line.py:73`](../../Tools/Blender/twohand_blade_line.py#L73)

**Attack_2 inputs and clip**

- Final window (raise from 0.21) and per-frame proxy-corrected curve.
  [`2hand_attack_2.json:7`](../../Tools/Blender/configs/2hand_attack_2.json#L7)

- Hand-distance corrections from 3 Unity passes.
  [`2hand_attack_2.json:12`](../../Tools/Blender/configs/2hand_attack_2.json#L12)

- Importer: Humanoid with stored mapping, take `Scene` 1–39, source events.
  [`2hand attack 2 refined.fbx.meta:58`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/2hand%20attack%202%20refined.fbx.meta#L58)

**Knowledge**

- Attack_2 refined note: before / after numbers and the impact-height limit.
  [`CLAUDE.md:81`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L81)

- Attack_2 row in the 2H hit-window table.
  [`CLAUDE.md:62`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L62)

- Constant vs curve target, proxy correction and ramp advice.
  [`README.md:13`](../../Tools/Blender/README.md#L13)

**Follow-ups**

- Backswing tempo, impact height, commit the full Unity check.
  [`deferred-work.md:334`](deferred-work.md#L334)

