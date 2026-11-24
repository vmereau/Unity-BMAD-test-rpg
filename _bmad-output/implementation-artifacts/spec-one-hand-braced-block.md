---
title: 'One-hand braced block animation (no shield)'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: 'de2e189f38c1a6147d20ce1f66d09d8ad113422b'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/meshy-mcp.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Since `spec-unshielded-blocking`, a one-hander without a shield blocks, but it plays the sword's `Block Idle`, a shield pose with an empty left arm. The shield is an offhand that changes the block's stats, the item bonuses and the animation, so the block clip has to follow whether a shield is in hand, not only the weapon.

**Approach:** A new looping **braced diagonal guard** for the one-hander without a shield: bladed stance, knees bent; right hand holds the hilt in front of the right shoulder / chest, blade rising diagonally up-left across the face line (tip above the left shoulder, ~45°); left palm braced flat on the blade, about mid-length. Body source from Meshy text-to-motion (`prime`), then a constant muscle-offset bake fits the blade angle and the left palm (Meshy holds no weapon). `Block_State` becomes a 1D blend tree on a new float `ShieldInHand` (0 = weapon guard, 1 = shield guard), set at each raise.

## Boundaries & Constraints

**Always:** Meshy budget 30 credits total (3 × `prime`), stop at the cap; log every prompt in `MESHY_PROMPTS.md`. Shield in hand → `Block Idle`, exactly as today. Fists → `Unarmed Block Guard`, two-hander → `2hand blocking`, unchanged. `ShieldInHand` is written only on a raise (and its re-sends), never on lower — the exit blend must not swap clips. Animator writes go through `HumanoidAnimationBridge`. `Block_State` keeps its name, tag `UpperBodyAction` and transitions.

**Ask First:** Spending past 30 credits; a pose that can't reach the targets with muscle offsets within ±1 (would need a Blender edit); any block stat or rule change.

**Never:** Changes to block values, NPC guard rules or block gates; a per-shield-state override controller (controller swaps reset parameters); editing `Block Idle.fbx`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 1H no shield | Sword drawn, no offhand, RMB | Braced diagonal guard | N/A |
| 1H + shield | Sword + Kite drawn, RMB | `Block Idle` | N/A |
| Fists / 2H | RMB | `Unarmed Block Guard` / `2hand blocking` | N/A |
| Lower | RMB released | Blend out of the same clip (no pop) | N/A |
| Controller reset mid-block | Same source, visuals refreshed | Block re-pushed with the same `ShieldInHand` | N/A |
| NPC | Sword NPC guards, with / without shield | Shield → `Block Idle`, none → braced guard | N/A |

</frozen-after-approval>

## Code Map

- `Art/.../Controllers/Humanoid_Template.controller` -- `Block_State` (UpperBody) motion `Unarmed Block Guard.anim`; add float `ShieldInHand`.
- `Art/.../Combat/one handed/Sword_AnimatorOverride` -- maps `Unarmed Block Guard` → `Block Idle` today.
- `Art/.../Combat/two handed/TwoHanded_AnimatorOverride` -- `Unarmed Block Guard` → `2hand blocking`.
- `Scripts/Core/Animations/HumanoidAnimationBridge.cs:109` -- `SetBlocking(bool)`.
- `Scripts/Core/Animations/AIAnimationDriver.cs:35`, `HumanoidAIAnimationDriver.cs:127` -- NPC `SetBlocking`.
- `Scripts/Player/PlayerAnimationDriver.cs:89`, `PlayerStateManager.cs:237` -- player `SetBlocking`.
- `Scripts/Combat/PlayerCombat.cs:351, 613` -- raise + re-push (`_blockShield`).
- `Scripts/AI/EntityGuard.cs:87, 133` -- NPC raise + per-tick re-send (`_armament.ShieldInHand`).
- `Tests/EditMode/HumanoidTemplateControllerTests.cs` -- controller structure tests.
- `Art/.../Combat/Unarmed/lead jab step.fbx` (+ `meshy smplh rig.fbx`) -- SMPL-H import settings / avatar to copy.

## Tasks & Acceptance

**Execution:**
- [x] Meshy -- generate the guard loop (`prime`, 3 s, "seamless looping … ends in the exact starting pose"), download, import as `one handed/sword braced guard source.fbx` (SMPL-H avatar like `lead jab step`, root baked, loop) -- body source.
- [x] `one handed/Sword Braced Guard.anim` -- bake: source curves + constant muscle offsets fitted on the Player (`Idle` + `UpperBodyMask`, sword visual in `WeaponSocket`): blade ~45° up-left, tip above the left shoulder, hilt in front of the right shoulder / chest, left palm on the blade flat at mid-length; check over Walking / Running -- the pose.
- [x] `Humanoid_Template.controller` -- add float `ShieldInHand`; `Block_State` motion = 1D blend tree on it: `Unarmed Block Guard` @0, `Block Idle` @1 -- shield picks the clip.
- [x] `Sword_AnimatorOverride` -- `Unarmed Block Guard` → `Sword Braced Guard`; `Block Idle` unmapped. `TwoHanded_AnimatorOverride` -- add `Block Idle` → `2hand blocking` (defensive) -- per-weapon clips.
- [x] Bridge / drivers -- `SetBlocking(bool value, bool shieldInHand)` writes `ShieldInHand` (0/1) only when `value`; player passes `_blockShield != null`, NPC `_armament.ShieldInHand` -- one write path.
- [x] `HumanoidTemplateControllerTests.cs` -- `Block_State` blend tree on `ShieldInHand` with the two children; Sword override maps the weapon guard to `Sword Braced Guard` and leaves `Block Idle` -- regression guard.
- [x] Docs -- `Controllers/CLAUDE.md` (param, block blend), `Combat/CLAUDE.md` animation folder + bake recipe in `BAKE_RECIPES.md`, prompt log, `Scripts/Combat/CLAUDE.md` blocking clips line, `deferred-work.md` entry closed if present.

**Acceptance Criteria:**
- Given the player with a sword and no shield, when blocking while idle, walking and running, then the braced guard plays: blade diagonal in front, left palm on the blade, no clipping through the head.
- Given EditMode tests run, then all pass and the console shows no new errors.

## Design Notes

Blend tree instead of two states: one state keeps the tag, transitions and block-cancel wiring; the base controller's shield child must be a distinct clip (`Block Idle`) so overrides can key the two separately. Writing `ShieldInHand` only on raise keeps the old value during the exit blend.

## Verification

**Commands:**
- `run_tests` EditMode -- expected: all pass.
- `read_console` after compile -- expected: no errors.

**Manual checks:**
- Front / side renders of the bake on the Player with the sword visual; play StartingTown: block with sword alone, sword + shield, fists, Flamberge.

## Suggested Review Order

**Clip selection (entry point)**

- One write path: the shield float is set only on a raise, so the exit blend keeps its clip.
  [`HumanoidAnimationBridge.cs:115`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L115)

- `Block_State` blend tree: weapon guard @0, `Block Idle` @1.
  [`Humanoid_Template.controller:3488`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L3488)

- Design rule and per-family mapping, documented next to the controller.
  [`Controllers/CLAUDE.md:122`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L122)

**Callers**

- Player raise passes the snapshotted shield; re-push after a controller reset keeps it.
  [`PlayerCombat.cs:353`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L353)
  [`PlayerCombat.cs:613`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L613)

- NPC snapshots the flag at the raise; per-tick re-send can't snap the clip mid-hold.
  [`EntityGuard.cs:135`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L135)

**The pose**

- Bake recipe: fitted arm constants, targets, results, gotchas.
  [`BAKE_RECIPES.md:123`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/BAKE_RECIPES.md#L123)

**Tests**

- Blend tree structure and sword / two-hand override mapping.
  [`HumanoidTemplateControllerTests.cs:125`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L125)

- Sword weapon guard is now the braced guard.
  [`UnarmedAttackTests.cs:187`](../../Assets/Tests/EditMode/UnarmedAttackTests.cs#L187)
