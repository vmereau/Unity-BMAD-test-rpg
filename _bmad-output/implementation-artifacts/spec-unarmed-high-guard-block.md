---
title: 'Unarmed high guard block pose (baked muscle-offset clip)'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '9d656a5f2548dd24709850872116628608e0ddb8'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Unarmed blocking (player and humanoid NPCs) plays `Block Idle.fbx`, a shield block, so a bare-handed
fighter blocks as if holding a shield. Mixamo has no boxing high guard.

**Approach:** Bake `Unarmed/Unarmed Block Guard.anim` from `Unarmed Idle Guard.anim` (keeps the breathing and lean),
adding constant humanoid muscle offsets that raise both fists to the face. Fit the offsets by measuring the Player
against the reference pictures `Unarmed/refs/high guard front.png` / `high guard side.png`. Point `Block_State` at it
and keep each weapon's own block through the overrides.

## Boundaries & Constraints

**Always:** Same bake recipe as `Unarmed Idle Guard.anim` (curves + clip settings copied, loopTime on, constant offsets
only, fitted on `Player.prefab` with `UpperBodyMask`). Re-key the override entries in the same change: Sword gains
`Unarmed Block Guard → Block Idle` (sword keeps its current block); TwoHanded re-keys `Block Idle → 2hand blocking`
onto the new clip and the stale entry is deleted (`SerializedObject`). Keep `Block Idle.fbx` (still the sword block).
Commit the two reference PNGs.

**Ask First:** Offsets that must change over time (not constants), a Blender edit, any change to `UpperBodyMask`,
the `Block_State` transitions or its tag, or blocking gameplay code.

**Never:** Edit `Unarmed Idle Guard.anim`, `Block Idle.fbx` or any weapon clip. Add runtime IK. Touch block logic
(`PlayerCombat`, AI block code).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unarmed block | Player unarmed, in combat, block held, standing | Both fists between chin and cheekbone height, in front of the face (not inside the head), forearms near vertical, elbows down near the ribs; left (lead) fist slightly further forward; matches the reference pictures | N/A |
| Block while moving | Same, walking / strafing | Same upper-body guard over the legs; no hand through the head or torso | N/A |
| NPC unarmed block | Humanoid NPC (shared controller), unarmed, blocking | Same high guard | N/A |
| Sword / 2H block | Weapon override active | Sword still plays `Block Idle`, two-handed `2hand blocking` | Stale key → verify with `GetOverrides` |
| Idle ↔ block | Block pressed / released from `CombatIdle` | Fists rise to / drop from the guard through the existing transition blend, no pop | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Unarmed Idle Guard.anim` -- bake source (already proportion-corrected)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/refs/high guard front.png`, `high guard side.png` -- pose targets (untracked)
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- UpperBody `Block_State` (motion = `Block Idle`)
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one handed/Sword_AnimatorOverride.overrideController` -- no block entry today
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two handed/TwoHanded_AnimatorOverride.overrideController` -- `Block Idle → 2hand blocking`
- `Assets/Tests/EditMode/UnarmedAttackTests.cs` -- `WeaponOverride_MapsTheUnarmedBaseClips`
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md`, `../../Controllers/CLAUDE.md` -- docs

## Tasks & Acceptance

**Execution:**
- [x] `Unarmed/Unarmed Block Guard.anim` (new) -- bake from `Unarmed Idle Guard.anim` with constant offsets on arm (Down-Up, Front-Back, Forearm Stretch, Twist), shoulder and hand muscles; measure fists / elbows vs the head bone on the Player (base `Idle` + mask, several times) and iterate until the matrix pose holds; render front + side for comparison with the refs -- the pose
- [x] `Humanoid_Template.controller` -- `Block_State.motion` = new clip; confirm it is in `ctrl.animationClips` (stale-cache gotcha) -- wiring
- [x] Sword / TwoHanded overrides -- entries as in Always; remove the stale `Block Idle` key from TwoHanded -- weapons keep their blocks
- [x] `UnarmedAttackTests.cs` -- melee overrides must map `Unarmed Block Guard` -- regression guard
- [x] `Combat/CLAUDE.md` + `Controllers/CLAUDE.md` -- document the clip (source, offsets, measured result, refs) and the new override key -- knowledge capture

**Acceptance Criteria:**
- Given the Player is unarmed in combat, when the block is held, then the measured pose meets the matrix and the user confirms it in play mode against the refs.
- Given the change, when EditMode tests run, then `UnarmedAttackTests` passes and the console shows no new errors.

## Design Notes

Target numbers (player-local, **metres** from the head bone, index knuckle): L (−0.06, 0.00, +0.25), R (+0.07, 0.00,
+0.21); wrists ≈ 0.10 below; elbows (±0.17, −0.32, +0.03…+0.06). Tune by coordinate descent like the idle bake, then
judge visually against the refs. The head bone sits at the neck seam: the face (mouth +0.05, nose +0.11) is above it,
so the fist targets are at its height, not below. Head pitch: Neck / Head Nod raised so the head looks ~11° down
instead of the idle guard's 36° (the hanging face otherwise lands inside the fists).

## Spec Change Log

- 2026-11-16 (review, acceptance auditor): the first Design Notes targets (body heights, y −0.10…−0.04, z ≤ +0.12)
  put the fists at the throat and inside the face; replaced by the measured metre targets above, which match the
  frozen matrix (chin–cheekbone, in front of the face). Neck / Head Nod offsets added to satisfy "not inside the head";
  recorded here because the task list named only arm muscles. KEEP: the bake, wiring and override keys.

## Verification

**Commands:**
- `run_tests` EditMode `UnarmedAttackTests` -- expected: all pass

**Manual checks:**
- `execute_code` measurement + front / side renders of the Player in the block pose; `GetOverrides` on both melee overrides; `read_console` clean

## Suggested Review Order

**The pose (baked clip)**

- Entry point: source, head + arm offsets, measured result, fitting gotchas
  [`Combat/CLAUDE.md:160`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md#L160)

- Baked clip: idle-guard curves + 18 constant offsets, loop settings copied
  [`Unarmed Block Guard.anim:1`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Unarmed%20Block%20Guard.anim#L1)

**Wiring**

- Block_State now plays the high guard (tag and transitions untouched)
  [`Humanoid_Template.controller:1746`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L1746)

- Sword gains an explicit entry so it keeps the shield-style Block Idle
  [`Sword_AnimatorOverride.overrideController:24`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/one%20handed/Sword_AnimatorOverride.overrideController#L24)

- Two-handed re-keyed onto the new clip; stale Block Idle key removed
  [`TwoHanded_AnimatorOverride.overrideController:80`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/two%20handed/TwoHanded_AnimatorOverride.overrideController#L80)

**Peripherals**

- Test pins each melee family's block target clip
  [`UnarmedAttackTests.cs:161`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L161)

- Override keys documented; NPCs never set IsBlocking today
  [`Controllers/CLAUDE.md:95`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L95)

- Deferred: strafe hip yaw turns every upper-body pose; idle guard head 36° down
  [`deferred-work.md:734`](deferred-work.md#L734)
