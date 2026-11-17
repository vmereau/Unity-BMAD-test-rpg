---
title: 'Attack recovery cancel (move or re-attack after the combo window closes)'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '43b44155fcb424b4ee9f3f9a15d2e50051c8f26e'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A melee swing roots the player until its attack state fully exits — about 1.0–1.4 s after the hit (lead jab 1.80 s clip, hit at 0.33 s). Attacks without a follow-up feel locked in place.

**Approach:** Once the current step's combo window closes, the attack enters **recovery**: move input ends the attack and blends to locomotion; an attack press cancels it and starts a fresh Attack_1. Applies to every melee family (unarmed, 1H, 2H). Finisher clips that have no `ComboWindowClose` get one.

## Boundaries & Constraints

**Always:** Commitment before recovery stays unchanged (no move input until `ComboWindowClose`; dodge / block / jump cancels as today). Recovery cancels go through `CancelAttack()` (hitbox closed, combo reset). Animator calls only through `HumanoidAnimationBridge` → `PlayerAnimationDriver` → `PlayerStateManager`. NPCs (shared `Humanoid_Template`) must behave exactly as before.

**Ask First:** Moving any existing `ComboWindowClose` event time; touching NPC attack logic.

**Never:** No root motion, no new clips, no foot-step animation work (deferred: Blender spec). No change to combo chaining before the window closes. No stamina refund.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Move out of recovery | Attack_N, window closed, move input > threshold | Attack ends this frame (`IsAttacking` false, move works); Base blends Attack_N → LockOn Locomotion | N/A |
| Re-attack in recovery | Attack_N, window closed, attack press | Fresh Attack_1 (step 1 stamina, step, steering), Base Attack_N → Attack_1 | Insufficient stamina → press refused, recovery continues |
| Finisher | Uppercut / 2H attack 2 / sword thrust (step = max) | Recovery opens at its `ComboWindowClose` | N/A |
| Press before close | Window open | Combo chains as today (not a restart) | N/A |
| Press while blending out at exit time | Trigger not taken | Dropped cleanly (outgoing exit ends the attack, triggers cleared) | Existing tracker path |
| Cancel during crossfade | Close event of outgoing clip (`ShouldIgnoreWindowClose`) | No recovery opened | N/A |
| NPC | NPC plays same states | `AttackCancel` never set → no new transition fires | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerAttackTracker.cs` -- pure lifecycle; gains recovery flag
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `TryAttack`, `OnComboWindowClose`, `OnAttackStateEntered`, `Update`, `CancelAttack`
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `SetAttacking`; new `SetAttackCancel`
- `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs`, `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- new `AttackCancel` bool setter
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- move threshold tunable
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- param + transitions
- `Art/.../Combat/Unarmed/Uppercut.fbx.meta`, `Art/.../Combat/two handed/2hand attack 2 refined.fbx.meta` -- add `ComboWindowClose`
- `Assets/Tests/EditMode/PlayerAttackTrackerTests.cs` -- tests

## Tasks & Acceptance

**Execution:**
- [x] `PlayerAttackTracker.cs` -- add `IsRecoveryOpen`: `OpenRecovery()` sets it; `Enter`, `Press`, `Reset` clear it -- pure, testable recovery state
- [x] `PlayerAttackTrackerTests.cs` -- cover open/clear rules and that a restart press (`isChainStep: true`) keeps `ActiveStates` so the outgoing exit doesn't end the new attack
- [x] `HumanoidAnimationBridge.cs` / `PlayerAnimationDriver.cs` / `PlayerStateManager.cs` -- `AttackCancel` bool setter; `SetAttacking(...)` resets it to false -- player-only Animator signal
- [x] `CombatConfigSO.cs` -- `recoveryCancelMoveThreshold` (move input magnitude, default 0.2)
- [x] `PlayerCombat.cs` -- `OnComboWindowClose` (not ignored, `IsAttacking`) → `OpenRecovery()`. `Update`: `IsAttacking && IsRecoveryOpen && Move ≥ threshold` → `SetAttackCancel(true)` + `CancelAttack()`. `TryAttack`: when `IsAttacking && IsRecoveryOpen`, after the stamina gate → `CancelAttack()`, `ExecuteAttack()` (step 1), `ManageComboStep()`, `Press(isChainStep: true)`, `SetAttackCancel(true)`. `OnAttackStateEntered` → `SetAttackCancel(false)`
- [x] `Humanoid_Template.controller` (editor script via MCP) -- bool `AttackCancel`; each `Attack_N_State`, after the `IsBlocking` exit: `→ Attack_1_State` (`AttackCancel` true + `Attack_1`, 0.15 s, Destination), then `→ LockOn Locomotion` (`AttackCancel` true, 0.2 s, Destination)
- [x] `Uppercut.fbx` / `2hand attack 2 refined.fbx` importer events -- add `ComboWindowClose` at normalized 0.70 (0.93 s) / 0.80 (1.02 s) -- finishers get a recovery point
- [x] `PLAYER_COMBO.md`, `Controllers/CLAUDE.md`, `Animations/Combat/CLAUDE.md` -- document recovery, `AttackCancel`, new events

**Acceptance Criteria:**
- Given a single unarmed jab, when move input is held from 1.33 s, then the player walks immediately instead of waiting for the clip end.
- Given recovery in Attack_2 (Hook), when attack is pressed, then Attack_1 plays with the left hand and the combo continues to Hook / Uppercut.
- Given an NPC fighting with `Humanoid_Template`, when it attacks, then its combo and timings are unchanged.
- Given any recovery cancel, then no hitbox is open afterwards and `IsAttacking` never stays stuck (no stall-watchdog warning).

## Design Notes

A bool, not a trigger: a trigger set while the state is already blending out would latch and cancel the next attack. Restart uses `Attack_N → Attack_1` gated by `AttackCancel` because AnyState → `Attack_1` can't self-transition, and ungated it would let NPC `Attack_1` triggers restart swings. The restart press counts as a chain step in the tracker: the outgoing state is still active.

Known pre-existing gap (not fixed here): a fresh press during a cancel's blend-out zeroes the tracker count and the outgoing exit can end the new attack — same as block-cancel today.

## Verification

**Commands:**
- Unity Test Runner EditMode (`run_tests`) -- expected: all pass, incl. new tracker tests

**Manual checks:**
- Play mode, unarmed + 1H + 2H: single hit then move → walks at window close; re-press in recovery → new Attack_1; full combos unchanged; NPC fight unchanged; console clean.

## Suggested Review Order

**Recovery state**

- Entry point: a real combo-window close (finishers included) opens the recovery.
  [`PlayerCombat.cs:959`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L959)

- Pure flag, cleared by press / enter / reset — EditMode-tested.
  [`PlayerAttackTracker.cs:24`](../../Assets/_Game/Scripts/Combat/PlayerAttackTracker.cs#L24)

**Cancel paths**

- Move cancel: held move ≥ threshold → `AttackCancel` + `CancelAttack`.
  [`PlayerCombat.cs:277`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L277)

- Restart press; already blending out → falls back to the fresh path (AnyState interrupts).
  [`PlayerCombat.cs:394`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L394)

- Restart keeps the outgoing state counted (chain-step press) and sets `AttackCancel` last.
  [`PlayerCombat.cs:424`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L424)

- Fresh press while a cancelled swing still blends out keeps its pending exit.
  [`PlayerCombat.cs:442`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L442)

- Flag cleared on every attack-state enter, before the stale-enter guard.
  [`PlayerCombat.cs:992`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L992)

**Animator signal**

- Player-only `AttackCancel` transitions; AnyState → Attack_1 gated on it being false.
  [`Humanoid_Template.controller:208`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L208)

- Bridge setter + blend-out query (only animator writer).
  [`HumanoidAnimationBridge.cs:118`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L118)

- `SetAttacking(true)` clears the flag so a lingering one can't cut the next swing.
  [`PlayerStateManager.cs:200`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L200)

**Peripherals**

- Finisher recovery points (Uppercut 0.70, 2H finisher 0.80).
  [`Uppercut.fbx.meta:74`](../../Assets/_Game/Art/Characters/Humanoids/Animations/Combat/Unarmed/Uppercut.fbx.meta#L74)

- Move threshold tunable.
  [`CombatConfigSO.cs:172`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L172)

- Tracker tests incl. restart keeping the outgoing exit.
  [`PlayerAttackTrackerTests.cs:141`](../../Assets/Tests/EditMode/PlayerAttackTrackerTests.cs#L141)
