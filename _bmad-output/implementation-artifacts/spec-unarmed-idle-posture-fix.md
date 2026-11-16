---
title: 'Unarmed combat idle posture fix (baked corrected clip)'
type: 'bugfix'
created: '2026-11-16'
status: 'done'
baseline_commit: '8c5af6b7d6a445f70b25ef8959de60520abd795b'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In game, the unarmed combat idle (`CombatIdle`, UpperBody layer) shows the fists tucked against the chest
and an upright torso, unlike the source clip's forward-leaning guard. Measured: the arm joint angles retarget within
~3°, but the Player's forearm/upper-arm ratio (1.1) is much shorter than the source rig's (1.7), so the fists sit
0.12 instead of 0.22 body-heights in front of the chest. `UpperBodyMask` takes the hips from the locomotion clip,
which drops the 11° forward lean (−4° in game).

**Approach:** Bake a corrected copy of the `Unarmed Idle` FBX clip to a `.anim`, with constant offsets added to a few
humanoid muscle curves (spine/chest forward bend, arm forward / elbow opening). Tune the offsets until the in-game
pose on the Player matches the source pose. Point `CombatIdle` at the baked clip and re-key the weapon overrides.
Also clean up the FBX import settings and record the stale-animator-cache gotcha.

## Boundaries & Constraints

**Always:** The FBX stays the untouched source; the `.anim` is derived from it. Change importer settings only through
`ModelImporter.clipAnimations` (never raw `.meta` YAML). Re-key the `Sword_AnimatorOverride` (`→ SwordIdle`) and
`TwoHanded_AnimatorOverride` (`→ 2hand idle`) entries onto the new clip and delete the stale `m_Clips` entries in the
same change. The baked clip loops and keeps the source's loop seam.

**Ask First:** Restoring the bladed (side-on) stance, any change to `UpperBodyMask`, or offsets that need curves
changing over time (not constants).

**Never:** Edit the weapon idles or the attack clips. Add runtime IK. Change `UpperBodyWeight` or the drivers.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unarmed guard | Player, unarmed, `IsInCombat`, Base `LockOn Locomotion` idle | Lead fist ≈0.22, rear ≈0.19 body-heights in front of the chest (±0.03); torso lean ≈11° (±3°); no hand through the body | N/A |
| Running in guard | Same, running forward | Same upper-body pose over the running legs; chest faces forward (shoulder yaw within ±10°) | N/A |
| Sword / 2H drawn | Weapon override active | `CombatIdle` still plays `SwordIdle` / `2hand idle` | Stale override key → old clip plays: verify via `GetOverrides` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Unarmed Idle.fbx(.meta)` -- source clip; `lastFrame` 109 is stale (take = 89 frames), `bodyMask` line has a stray `u`
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- UpperBody `CombatIdle` state (`m_Motion`)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one handed/Sword_AnimatorOverride.overrideController` -- keyed on `Unarmed Idle`
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/TwoHanded_AnimatorOverride.overrideController` -- keyed on `Unarmed Idle`
- `Assets/_Game/Art/Characters/Humanoids/Animations/UpperBodyMask.mask` -- Body/Head/Arms/Fingers on; hips come from Base
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md`, `../../Controllers/CLAUDE.md` -- docs to update

## Tasks & Acceptance

**Execution:**
- [x] `Unarmed/Unarmed Idle.fbx` -- set the clip `lastFrame` to the take end (89) via `ModelImporter.clipAnimations`; save and reimport (the trailing `u` on `bodyMask` turned out to be normal Unity 6.6 serialization, present on every re-serialized clip meta — left as is) -- stale settings from the old clip
- [x] `Unarmed/Unarmed Idle Guard.anim` (new) -- bake from the FBX clip: copy all curves + clip settings (loopTime on), add constant offsets to the spine/chest Front-Back and the arm/forearm muscles; iterate (measure the Player with the run/idle base + `UpperBodyMask`, like the investigation) until the matrix targets hold -- fixes the posture
- [x] `Humanoid_Template.controller` -- `CombatIdle.motion` = the baked clip; confirm it's in `ctrl.animationClips` and plays at runtime -- wiring
- [x] Sword / TwoHanded overrides -- re-key the entries onto the new clip and remove the stale `Unarmed Idle` `m_Clips` entries (`SerializedObject`) -- keeps the weapon idles
- [x] `Combat/CLAUDE.md` + `Controllers/CLAUDE.md` -- document the baked clip (source, offsets, targets, re-bake recipe) and the gotcha that replacing an FBX in place can leave a state playing no clip (check `ctrl.animationClips`; fix by reassigning `state.motion` or restarting the editor) -- knowledge capture

**Acceptance Criteria:**
- Given the Player is unarmed in combat, when standing or running, then the measured pose meets the matrix targets and the user confirms it in play mode.
- Given a sword or a two-handed weapon is drawn, when idle, then `CombatIdle` plays that weapon's idle.
- Given the change, when the console is read, then no new errors or warnings appear, and `git status` shows only the listed files.

## Design Notes

Retargeting copies muscle angles, not hand positions, so proportion differences can only be fixed by changing the
angles. Constant offsets keep the clip's breathing and bounce. The bake follows the existing two-handed `.anim` bake
precedent (curves copied with `AnimationUtility`, settings copied, recipe documented, no committed tool). Muscle names
(examples): `Spine Front-Back`, `Chest Front-Back`, `UpperChest Front-Back`, `Left Arm Front-Back`, `Left Arm Down-Up`,
`Left Forearm Stretch`, plus the right side.

## Verification

**Manual checks (if no CLI):**
- `execute_code` measurement (Player.prefab, `Idle` / `Running` base + `UpperBodyMask` + baked clip): fist offsets and lean within targets; a render from the side and the front shows no clipping
- `GetOverrides` on both weapon overrides maps the new key to `SwordIdle` / `2hand idle`; `read_console` clean

## Suggested Review Order

**Posture fix (baked clip)**

- Entry point: why the raw clip looked wrong, the muscle offsets, and the measured result
  [`Combat/CLAUDE.md:140`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L140)

- Baked clip keeps the source loop and root-bake settings (89 frames, loopTime on)
  [`Unarmed Idle Guard.anim:35757`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Unarmed%20Idle%20Guard.anim#L35757)

**Wiring**

- CombatIdle now plays the baked `.anim` instead of the FBX clip
  [`Humanoid_Template.controller:2884`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L2884)

- Sword override re-keyed on the new clip; stale FBX key removed
  [`Sword_AnimatorOverride.overrideController:22`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one%20handed/Sword_AnimatorOverride.overrideController#L22)

- Two-handed override re-keyed the same way
  [`TwoHanded_AnimatorOverride.overrideController:80`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L80)

**Peripherals**

- Source clip range trimmed to the real 89-frame take
  [`Unarmed Idle.fbx.meta:39`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Unarmed%20Idle.fbx.meta#L39)

- Stale-animator-cache gotcha and the CombatIdle override keys
  [`Controllers/CLAUDE.md:95`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L95)

- Test guards the melee overrides' idle mapping
  [`UnarmedAttackTests.cs:160`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L160)
