---
title: 'Unarmed attack foot step (Blender leg edit)'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '0107256'
context:
  - '{project-root}/Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md'
  - '{project-root}/Tools/Blender/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `PlayerAttackStep` moves the capsule forward during each unarmed swing (`lead jab`, `Hook`, `Uppercut`), but the clips keep the feet planted, so the feet slide along the ground.

**Approach:** Edit the legs of the three clips in Blender (IK + bake, like `Tools/Blender/twohand_blade_line.py`): in each step window the rear foot slides back in clip space by exactly the scripted displacement (world-fixed in game), the lead foot does a lift / forward / plant push-step, then the rear foot follows and the stance settles back to the guard. Measure → correct in Unity until the planted foot holds.

## Boundaries & Constraints

**Always:**
- Step data (CombatConfigSO.unarmedAttacks, metres / start s / duration s since state enter, constant speed = `AttackStep.DisplacementBetween`): jab 0.30 / 0.07 / 0.43, Hook 0.35 / 0.27 / 0.37, Uppercut 0.50 / 0.10 / 0.37. Hook's importer trims source frames 10–65: its window is source f18.1–29.2.
- Keep clip lengths, frame content, every event (HitboxEnable / Disable with their string ids, ComboWindowOpen / Close incl. the Uppercut close at 0.70), and the import settings (root rotation + Y baked on Original, root XZ not baked on Original, Loop Pose off).
- Upper body and arms unchanged (hips / spine / arms / head within a few mm of the source on the Player rig).
- The planted foot must read as world-fixed **in Unity** (XZ unbaked, `applyRootMotion` off), measured on the Player in play mode.
- New FBX files keep the original paths, names and `.meta` GUIDs (so the template and the Sword / TwoHanded override keys follow); the source FBX are moved to `Unarmed/old/` (new GUIDs, unreferenced).

**Ask First:**
- Changing any step distance / timing to fit a natural foot plant.
- Any change to code, `PlayerAttackStep`, root motion settings or the controller.
- Hook and Uppercut: only after the user has seen and approved the jab.
- How to handle NPCs if the in-place step reads badly on them.

**Never:** root motion, script changes, touching the arms / hit arcs / event times.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Single jab | Player idle in combat, TryAttack | Rear foot world drift < 3 cm over 0.07–0.50 s; lead foot lifts, lands ahead; stance back to guard by the combo window | Re-correct per frame, repeat the pass |
| Combo chain | Jab → Hook → Uppercut within windows | Combo blend mixes two clips' feet; no pop or foot cross-over | Report if the blend slides visibly |
| NPC | NPC plays the clip in place (no step) | Feet step on the spot; reported to the user with options | — |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/lead jab.fbx|Hook.fbx|Uppercut.fbx` (+ `.meta`) -- clips replaced in place; meta keeps GUID / internal clip id / events, take → `Scene`, frame range +1
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/old/` -- untouched Mixamo sources (edit inputs)
- `Assets/_Game/Data/Config/CombatConfig.asset` -- `unarmedAttacks` step data (read only)
- `Assets/_Game/ScriptableObjects/Items/Weapons/AttackStep.cs` -- `DisplacementBetween`: constant speed inside [start, start+duration]
- `Assets/_Game/Scripts/Player/PlayerAttackStep.cs` -- applies the step from attack-state enter (read only)
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- Attack_1/2/3 states, speed 1, no foot IK; shared with NPCs
- `Tools/Blender/twohand_blade_line.py` -- import / IK / bake / export pattern to reuse

## Tasks & Acceptance

**Execution:**
- [x] `Unarmed/old/` -- move the three source FBX there with fresh metas -- keep sources, free the original GUIDs for the edits
- [x] `Tools/Blender/unarmed_foot_step.py` + `unarmed_foot_step_measure.cs` + `configs/unarmed/*.json` -- leg IK edit: per-frame foot targets = source foot + player-local correction (Unity measured), foot orientation kept, bake legs only, export armature FBX -- reproducible, same loop as the blade tool
- [x] `Unarmed/lead jab.fbx` (+ meta) -- export, measure the Player feet in play mode (frame step), correct, repeat until < 3 cm; show the user -- look-test gate
- [x] `Unarmed/Hook.fbx`, `Unarmed/Uppercut.fbx` (+ metas) -- same after approval
- [x] `Combat/CLAUDE.md`, `deferred-work.md`, `Tools/Blender/README.md` -- document the leg recipe, new files, NPC result

**Acceptance Criteria:**
- Given the Player attacks unarmed in play mode, when each step window runs, then the planted foot's world XZ drifts < 3 cm.
- Given the edited clips, when compared with the sources on the Player rig, then upper-body bones and fist positions match within 1 cm and the events / import settings / lengths are unchanged.
- Given an NPC playing the clips, when observed, then the user has a description and options.

## Design Notes

Unity drops the body's XZ travel, so what matters is each foot **relative to the Player root** per frame. Sample the source on the Player (PlayableGraph, like the blade snippet), compute the wanted player-local foot path, and feed the difference to Blender as the IK target offset (armature space ≈ player-local × 100, +Z forward). Wanted paths, D(t) = step displacement since its start:
- rear: `src(t0) − D(t)·fwd` in the window, then smoothstep back to the source path (drag) over ~0.2 s.
- lead: world = `D·s(t)` with `s` a smoothstep over the first ~70 % of the window plus a small lift arc, i.e. rel = `src + (D·s(t) − D(t))·fwd`.
Iterate per-frame corrections from play-mode measurements (blade tool needed 3 passes).

**Decisions taken during implementation (2026-11-17):**
- Jab approved as edited (bladed stance kept). Anchoring the clips on the square combat-idle stance (to remove the 0.26 s
  entry-blend slide) was tried and dropped: the Player legs can't reach it with the source hips twisted.
- Hook / Uppercut capture bodies travel only 0.10 / 0.06 m vs the scripted 0.35 / 0.50 m, so a rear foot pinned for the
  whole window over-stretches the legs. User chose a **shuffle**: lead steps, then the rear foot lifts and steps up while
  the capsule still moves (every foot world-fixed or lifted). Distances unchanged.
- Hook lead foot plants on its source f17 spot, then settles to the guard at 1.50–1.80 s (reach limit mid-clip).
- NPCs: the user will give them the same scripted step in a future spec (deferred-work). Blend slides (idle → jab, combo
  blends) deferred too.

**Results (Player rig):** planted feet outside blends: jab rear 0.9 cm / lead 0.1 cm in play mode (lead creeps ~5 cm at
1.13–1.30 s, recovery); Hook rear 0.8 cm / lead 3.1 cm (f16, just after landing); Uppercut rear 1.3 cm / lead 2.7 cm
(landing frame) — edit-mode worst frame vs the wanted path. Events, lengths, clip flags unchanged; avatar description
rewritten explicitly (Blender `Armature` node, rest transforms + mapping copied from `old/`, poses identical).

**Open against the ACs (need the user's call, not yet accepted):**
- AC1 in real play: the CombatIdle → Attack_1 blend slides the jab's rear foot ~22 cm inside its step window, and the
  Hook / Uppercut steps run mostly inside the 0.42 s combo blends (5–20 cm mixed-pose slide). Clips alone meet it
  (Hook lead 3.1 cm is 1 mm over).
- AC2 / Always "upper body within a few mm": pose relative to the hips is unchanged, but the whole upper body shifts
  1.8–3.0 cm in the hit windows (Unity re-centres the body on the moved legs).

## Verification

**Manual checks (if no CLI):**
- Play mode, MCP: pause + `EditorApplication.Step()`, TryAttack by reflection; log both foot bones' world XZ each frame of the window; max planted drift < 3 cm.
- Blender screenshots of the jab at window start / middle / end.

## Suggested Review Order

**Foot plan (what the legs should do)**

- Entry point: per-frame wanted toe paths, world-fixed or lifted moves against the scripted step.
  [`unarmed_foot_step.py:89`](../../Tools/Blender/unarmed_foot_step.py#L89)

- The Hook's two-step lead plan and shuffle timings (user-chosen shuffle).
  [`hook.json:13`](../../Tools/Blender/configs/unarmed/hook.json#L13)

**Blender edit (IK + bake + export)**

- Unit detection: the Uppercut's Mixamo export is in centimetres.
  [`unarmed_foot_step.py:194`](../../Tools/Blender/unarmed_foot_step.py#L194)

- Reach clamp: out-of-reach targets spun the 2-bone IK (foot popping behind).
  [`unarmed_foot_step.py:255`](../../Tools/Blender/unarmed_foot_step.py#L255)

- Foot keeps its armature-space orientation; only translated by the IK.
  [`unarmed_foot_step.py:278`](../../Tools/Blender/unarmed_foot_step.py#L278)

- Damped, clamped measure → correct pass (low, cross-coupled gain in Unity).
  [`unarmed_foot_step.py:321`](../../Tools/Blender/unarmed_foot_step.py#L321)

**Unity assets**

- Re-exported take: `Scene`, frames +1; GUID, internal id and events kept.
  [`Hook.fbx.meta:36`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Hook.fbx.meta#L36)

- Player-rig sampler feeding the loop (source samples + measurements).
  [`unarmed_foot_step_measure.cs:18`](../../Tools/Blender/unarmed_foot_step_measure.cs#L18)

**Docs and follow-ups**

- Clip table, foot plans, measured drift and the avatar / runtime gotchas.
  [`CLAUDE.md:140`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L140)

- Tool recipe and loop.
  [`README.md:23`](../../Tools/Blender/README.md#L23)

- Deferred: NPC scripted step, blend slides, review follow-ups.
  [`deferred-work.md:867`](../../_bmad-output/implementation-artifacts/deferred-work.md#L867)

