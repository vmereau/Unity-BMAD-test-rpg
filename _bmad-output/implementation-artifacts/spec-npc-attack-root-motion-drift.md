---
title: 'Fix NPC body drifting from its entity root during full-body attacks'
type: 'bugfix'
created: '2026-11-03'
status: 'done'
baseline_commit: 'f6fb10b'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Humanoid NPCs (bandits) visibly step forward during an unarmed attack, but the entity root stays where it was. The NavMeshAgent, colliders, lock-on point and hitbox owners keep using the old position, so the body and the gameplay position separate. The cause is that the NPC Animator (`Character` child of `NPC_base Variant`, inherited from the nested model) has **Apply Root Motion on**. Since attacks moved to the full-body Base Layer (spec-full-body-attacks-root-motion), the unbaked `Unarmed Jab` root motion is applied to the `Character` transform itself. Measured in play: 0.33 m forward mid-swing, and a permanent (−0.24, 0, 0.23) offset when the clip is cut. Each interrupted attack adds to the offset.

**Approach:** Turn root motion off on the NPC Animator (the project rule is "applyRootMotion off everywhere"; NPCs attack in place). Add a runtime guard in the humanoid AI driver that forces it off and warns, so a re-imported model can't bring the drift back. No NPC lunge.

## Boundaries & Constraints

**Always:** NPCs attack in place, as spec-full-body-attacks-root-motion intended. The guard logs through `GameLog.Warn` once per entity. Prefab edits are made with the prefab API, not raw YAML.

**Ask First:** Any NPC forward step / agent movement during attacks. Changing clip import settings (bake into pose).

**Never:** No `OnAnimatorMove` root-motion forwarding. No change to player movement or to `PlayerAttackStep`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Full swing | Bandit plays Attack_1 (Jab) | `Character` local position stays (0,0,0); body in place | N/A |
| Cut swing | Jab interrupted by stagger / combo / disengage | No residual offset | N/A |
| Misconfigured prefab | NPC Animator with Apply Root Motion on | Forced off in Awake | `GameLog.Warn` naming the entity |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- `Character` child Animator (controller `Humanoid_Base` override), `m_ApplyRootMotion` inherited as 1 from the nested model
- `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs:42` -- `Awake` already fetches the Animator (ragdoll setup): the guard goes there
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Jab.fbx` -- Attack_1 base clip, XZ not baked (the only attack with real root travel)
- `Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md` -- states "applyRootMotion off everywhere"

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- set the `Character` Animator's `applyRootMotion` to false (variant override); verify scene NPC instances don't override it back -- root cause
- [x] `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs` -- in `Awake`, if the found Animator has `applyRootMotion` on: `GameLog.Warn` and set it false -- regression guard for re-imported models / new variants
- [x] `Assets/_Game/Scripts/Core/Animations/CLAUDE.md` + `Prefabs/Entities/CLAUDE.md` -- document: NPC Animator root motion must stay off (nested models default it on); the driver enforces it -- prevent recurrence

**Acceptance Criteria:**
- Given a bandit in play mode, when it plays and cuts an unarmed jab, then its `Character` child local position stays at (0,0,0).
- Given any humanoid NPC prefab whose Animator has root motion on, when it awakes, then root motion is off and one warning names it.
- Given the player, when attacking, then `PlayerAttackStep` behaviour is unchanged.

## Spec Change Log

- 2026-11-04, review (patches, no loopback): (1) the guard runs before the `_runSpeed` early return (the bridge keeps animating a driver-disabled NPC) and uses the new `HumanoidAnimationBridge.Animator` (same Animator as the bridge; ragdoll code too). (2) Reach regression: the in-place jab reaches 0.61 m (hitbox centre, measured with `cullingMode = AlwaysAnimate`; a culled off-camera Animator doesn't pose bones, so the first measurements were invalid) vs 0.79 m with the drift, so jabs whiffed at the 1.3 m stopping distance. Human chose option 1: NPC `UnarmedHitbox._reachPadding` 0.1 → 0.3 (≈1.16 m reach). Rejected: "unbaked root motion slides the hips then snaps" (hips stay at −0.01 m with root motion off); `OnAnimatorMove` concerns (none in the project). KEEP: NPCs attack in place, no agent movement change.

## Verification

**Commands:**
- `read_console` after compile -- expected: no errors, no root-motion warning for the fixed prefab
- EditMode tests (`run_tests`) -- expected: all pass

**Manual checks:**
- Play: `execute_code` drives a bandit Animator through Attack_1, cut mid-clip → `Character.localPosition` stays zero.

## Suggested Review Order

- Root cause: variant override turns the Character Animator's root motion off
  [`NPC_base Variant.prefab:347`](../../Assets/_Game/Prefabs/Entities/Humanoids/NPC_base%20Variant.prefab#L347)

- Regression guard, before the early returns, on the bridge's own Animator
  [`HumanoidAIAnimationDriver.cs:52`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs#L52)

- Bridge exposes its Animator, resolved lazily (driver Awake may run first)
  [`HumanoidAnimationBridge.cs:53`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L53)

- Ragdoll path uses the same Animator
  [`HumanoidAIAnimationDriver.cs:149`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs#L149)

- Reach compensation for the in-place jab (hitbox forgiveness, no AI movement change)
  [`NPC_base Variant.prefab:81`](../../Assets/_Game/Prefabs/Entities/Humanoids/NPC_base%20Variant.prefab#L81)

- Docs: NPC root-motion rule and the jab reach measurement
  [`Core/Animations/CLAUDE.md`](../../Assets/_Game/Scripts/Core/Animations/CLAUDE.md)
  [`Prefabs/Entities/CLAUDE.md`](../../Assets/_Game/Prefabs/Entities/CLAUDE.md)
