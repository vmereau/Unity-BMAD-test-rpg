---
title: 'Blender: flatten the 2H attack 1 slash (first Blender pipeline run)'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: 'd53c823'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In `2hand attack` (Attack_1, 1.83 s) the blade never reaches a horizontal slash: measured on the Player rig
(blade = right socket + normalize(right − left) × 1.1 m), the sweep in front (0.45–0.55) keeps the blade 18–34° above
horizontal with the tip at 1.23–1.56 m, so the cut reads as an upward wave, not the slash the posture implies.

**Approach:** First run of the Blender pipeline (Claude Code Blender MCP): import the Mixamo FBX, keep the body and right
arm, and drive the left hand with IK to a target `R − d′ × |R − L|`, where `d′` is the original hand-line direction with
its pitch pulled toward horizontal through the strike. That changes the only thing `TwoHandGripConstraint` reads (the
left→right hand line). Export a new FBX, compare it in game with the original, and switch the override if it's better.

## Boundaries & Constraints

**Always:** clip length, frame count (0–55 @ 30 fps), body, legs, root and right arm stay as in the source; the
original `2hand attack.fbx` stays untouched as the fallback; the new clip copies the original import settings
(Humanoid, Create From This Model, root rotation / Y / XZ baked, Based Upon Original, the four events); the `.blend`
working file and the Blender script stay in the scratchpad (not committed).

**Ask First:** switching `TwoHanded_AnimatorOverride` to the new clip (only after the user plays both); retiming any
event by more than 0.05; editing the right arm or spine if left-hand IK alone can't reach the targets.

**Never:** change the clip length or `attackSteps`; touch Attack_2, idle, block, stagger, draw / sheathe; touch
`TwoHandGripConstraint` or other code; commit the `meshy test/` folder.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Strike | 0.45–0.55 | blade pitch within −15°…+10°, tip 0.9–1.3 m high, in front (tip z > 0.5) | adjust the pitch target / blend, re-bake |
| Untouched span | 0–0.38 and 0.62–1.0 | blade direction within 2° of the original | widen nothing; fix the blend curve |
| Blend edges | 0.38–0.45, 0.55–0.62 | smoothstep weight, no tip jump > 0.15 m between samples (1/40) | re-bake with a softer ease |
| Grip | whole clip | left↔right hand distance within ±0.02 m of the original | IK target uses the original distance per frame |
| Elbow | IK active | left elbow stays below / outside the hands, no flip | pole target keyed from the original elbow |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/2hand attack.fbx` (+ `.meta`) -- Mixamo source and import settings / events to copy
- `.../two handed/TwoHanded_AnimatorOverride.overrideController` -- Attack_1 override (`b3f03a6d…` → new clip GUID on switch)
- `Assets/_Game/Scripts/Inventory/TwoHandGripConstraint.cs` -- blade aim = left→right socket line (read only)
- `.../Animations/Combat/CLAUDE.md` -- 2H section (outdated shoulder-rest idle note); `MESHY_PROMPTS.md` -- PlayableGraph blade sampling
- `_bmad-output/implementation-artifacts/deferred-work.md` -- Blender sword-path entry

## Tasks & Acceptance

**Execution:**
- [x] Blender (scratchpad script) -- import `2hand attack.fbx` at 30 fps, measure the hand line, key the left-hand IK target and pole, IK on the left forearm (chain 2), bake visual keys 0–55, remove constraints, export armature-only FBX (baked animation, no leaf bones) -- the edit itself
- [x] `.../two handed/2hand attack refined.fbx` (+ `.meta`) -- import; copy the original ModelImporter settings and clip events (normalized times unchanged) -- comparable clip
- [x] Unity sampling (`execute_code`, PlayableGraph on the Player prefab) -- re-run the 41-sample blade table on both clips, check the matrix, confirm the hit window 0.33–0.58 still covers the blade in front -- evidence
- [x] `.../two handed/TwoHanded_AnimatorOverride.overrideController` -- after user approval, point Attack_1 to the refined clip -- switch
- [x] `.../Animations/Combat/CLAUDE.md` -- fix the idle note (2H idle is the Mixamo front guard), record the refined clip, its source and the Blender recipe in 5–8 lines -- knowledge
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- update the Blender entry with the outcome -- traceability

**Acceptance Criteria:**
- Given the refined clip on the Player rig, when sampled at 41 points, then every row of the I/O matrix holds.
- Given the refined clip in Unity, when inspected, then its length is 1.83 s, the Humanoid mapping is valid, the four events match the original, and net root XZ travel is 0.
- Given the user plays both versions in game, when they pick one, then the override points to it and the console shows no new errors or warnings.

## Design Notes

Measured original (normalized t: pitch°, tip height m): 0.45: 32, 1.49 · 0.475: 18, 1.23 · 0.50: 24, 1.35 · 0.525: 34, 1.54.
The tip sweeps left → right in front (x −1.58 → +0.99) and the follow-through goes behind the right side (0.60–0.75).
Blender hand bones stand in for the Unity sockets (palm offsets); the result is verified on the Unity sockets.
Length unchanged on purpose: the problem is the path, not the timing, and a same-length clip keeps the events and
`attackSteps` valid, so the in-game A/B comparison shows only the path change.

## Verification

**Manual checks:**
- Blade table (original vs refined) pasted in the review summary; Blender viewport screenshot at t ≈ 0.50.
- In play mode: equip the flamberge, do attack 1 on a dummy, and confirm it hits; then the user does the A/B.

## Review Outcome (2026-11-03)

Final evidence (Player rig, 81 samples, original → refined): strike pitch +18…+35° → +0.6…+7.0°, tip 1.23–1.58 → 0.91–1.11 m,
min tip z 0.96; outside 0.38–0.62 ≤ 0.28°; hand distance ≤ 0.022 m off (grip band 0.10–0.40 m kept, min 0.15); body / right
hand ≤ 2.4 mm; elbow 0.03–0.15 m below the lower hand, no flip (bend plane ≤ 23°); root XZ 0; take `Scene` frame 1 = source frame 0.

Accepted deviation: the matrix "Blend edges" rule (≤ 0.15 m per 1/40 sample) is unmeetable — the original already moves
0.56 m per sample. Intent (no extra jerk) partly missed: the strike entry peaks at 39 m/s vs 24; the user play-tested and kept
it ("K, it is good"); an earlier tilt is logged in `deferred-work.md` if it ever reads as a jerk. Hand distance 0.022 vs ±0.02
accepted (no effect on `GripWeight`). Review patches: doc fixes in `Combat/CLAUDE.md` (combo line, window note, import-setting
wording, measured numbers, final parameters). Deferred: `WeaponHitbox` rotation sub-steps, committing the Blender script.
Rejected with evidence: avatar / scale differences (the skeleton is identical to the source `.meta`), stale-reference risk
(no other asset uses the old GUID), compression erosion (measured after import), idle note (override uses the Mixamo idle, user-confirmed).

## Suggested Review Order

**Clip switch**

- Entry point: Attack_1 slot now points at the Blender-refined clip.
  [`TwoHanded_AnimatorOverride.overrideController:15`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L15)

- Importer: Humanoid, root baked, same four events; take `Scene` 1–56.
  [`2hand attack refined.fbx.meta:33`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/2hand%20attack%20refined.fbx.meta#L33)

**Knowledge**

- What changed in the clip and how much, with revert path.
  [`CLAUDE.md:76`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L76)

- Reusable Blender recipe with the Unity-measured correction loop.
  [`CLAUDE.md:81`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L81)

- Corrected idle note and refined hit-window note.
  [`CLAUDE.md:68`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L68)

**Follow-ups**

- Blender pipeline outcome and next clip candidates.
  [`deferred-work.md:316`](deferred-work.md#L316)

- Review-surfaced deferrals (hitbox sub-steps, script commit).
  [`deferred-work.md:329`](deferred-work.md#L329)
