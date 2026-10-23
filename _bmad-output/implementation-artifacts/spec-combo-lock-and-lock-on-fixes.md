---
title: 'Fix combo lock-up and lock-on target misses'
type: 'bugfix'
created: '2026-10-23'
status: 'done'
baseline_commit: 'fb61f2f'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** (1) The player combo sometimes locks up: `PlayerCombat` advances its state on input, but the Animator can ignore the trigger (press during the uninterruptible blend-out to `CombatIdle`, `Attack_1 → Attack_2` cancelled by its `Source` interruption, dodge clearing only `IsAttacking`, stale latched triggers). The single `_IsComboAttacking` flag then swallows the only `OnStateExit`, leaving `IsAttacking` true forever. (2) Lock-on sometimes finds no target in an obvious spider: `OverlapSphereNonAlloc` uses a 10-slot buffer while one spider has ~13 colliders on layer 6 (ragdoll bones), so the aimed spider is often truncated out.

**Approach:** Make the attack lifecycle follow what the Animator actually does (count active attack states, end the attack when it reaches 0, clear latched triggers on end, stall watchdog, full reset on dodge) and fix the `Attack_1 → Attack_2` interruption. Lock-on: large buffer, one candidate per `EntityHealth` measured at its root.

## Boundaries & Constraints

**Always:**
- Animator writes only via `PlayerStateManager` → `PlayerAnimationDriver` → `HumanoidAnimationBridge`.
- Every attack-ending path leaves the hitbox disabled (PLAYER_COMBO.md).
- Pure logic in a plain C# class covered by EditMode tests (like `HitSweepTracker`).
- Controller edit via Unity (MCP) or careful YAML + `refresh_unity(if_dirty)`, never `force` after YAML.

**Ask First:**
- Retiming any clip event or changing exit times / blend durations.

**Never:**
- No change to AI attack code (`EntityMeleeAttacker`, `SMB_EntityAttackState`).
- No `GetNextAnimatorStateInfo` chain detection in `OnStateExit`.
- No change to combo feel (max steps, stamina costs, window timings).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal chain | Press in window | Next step plays; `Attack_n` exit during crossfade does not end the attack | N/A |
| Late press | Press after blend-out to `CombatIdle` started | Press dropped; on exit attack ends, triggers cleared, next press starts `Attack_1` | N/A |
| Outgoing close event | `ComboWindowClose` fires while 2 attack states are active or a step is queued | Ignored | N/A |
| Trigger never taken | `IsAttacking`, no attack callback/event for `attackStallTimeout` | Forced end (reset + hitbox off + triggers cleared) | `GameLog.Warn` |
| Dodge mid-combo | Dodge during any step | Full combo reset; next press after dodge starts `Attack_1` | N/A |
| Crowded lock-on | 2+ spiders / corpses (>10 colliders) in range | Nearest living entity in cone is locked | Warn if buffer full |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- combo state, SMB/event handlers, `_IsComboAttacking` flag
- `Assets/_Game/Scripts/Combat/DodgeController.cs` -- dodge cancels attack via `SetAttacking(false)` only
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `SetAttacking`
- `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs` -- player wrappers over `HumanoidAnimationBridge` (`ResetAttackTriggers` exists there)
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- combat tuning
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `Attack_1 → Attack_2` has `interruptionSource = Source`
- `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- 10-slot overlap buffer, per-collider candidates

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Combat/PlayerAttackTracker.cs` -- new pure class: active attack-state count (clamped ≥ 0), queued-step flag, last-activity time; `Press/Enter/Exit/Event(now)`, `Exit` returns true when count hits 0, `ShouldIgnoreWindowClose`, `IsStalled(isAttacking, now, timeout)`, `Reset()` -- testable lifecycle
- [x] `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- replace `_IsComboAttacking` with the tracker; end attack (`ResetAttackCombo` + `ExitAttack`) when exit returns true; stall check in `Update`; public `CancelAttack()` for dodge -- fixes lock-up paths
- [x] `Assets/_Game/Scripts/Combat/DodgeController.cs` -- call `PlayerCombat.CancelAttack()` instead of `SetAttacking(false)` -- full reset on dodge
- [x] `Assets/_Game/Scripts/Player/PlayerStateManager.cs` + `PlayerAnimationDriver.cs` -- `SetAttacking(false)` also clears attack triggers via a new driver wrapper -- no stale auto-chain
- [x] `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- `attackStallTimeout` (default 1.5 s, > longest gap between attack callbacks) -- watchdog tuning
- [x] `Humanoid_Template.controller` -- `Attack_1 → Attack_2` `interruptionSource = None` -- same fix as `Attack_2 → 3`
- [x] `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- buffer 64, `QueryTriggerInteraction.Collide`, dedupe by `EntityHealth`, angle/distance to the health root, warn when buffer full; selection through a pure static helper -- fixes misses
- [x] `Assets/Tests/EditMode/PlayerAttackTrackerTests.cs` + `LockOnTests.cs` -- matrix cases against the real code
- [x] `PLAYER_COMBO.md`, `Controllers/CLAUDE.md` -- document tracker, watchdog, both combo transitions at `None`

**Acceptance Criteria:**
- Given a 3-step unarmed or sword chain at normal timing, when played, then all steps play and the hit window behaves as before.
- Given any stuck path above, when the next attack is pressed after it resolves, then `Attack_1` plays without pressing R.
- Given all EditMode tests, when run, then they pass.

## Design Notes

End detection mirrors the AI's `EntityMeleeAttacker` counter: `OnStateEnter(next)` fires before `OnStateExit(prev)`, so a chain keeps the count ≥ 1. `ShouldIgnoreWindowClose = queued || activeStates > 1` keeps the Uppercut-close-in-crossfade guard. Stall timeout uses scaled time (pause-safe); activity = enter/exit/any of the four clip events. A late press stays dropped (no lock) — making it chain would need event retiming (Ask First).

## Verification

**Commands:**
- `mcp__UnityMCP__run_tests` (EditMode) -- expected: all pass
- `mcp__UnityMCP__read_console` (Error) -- expected: no compile errors

**Manual checks:**
- Play: spam attack late in each window, dodge mid-combo, lock on between two spiders and a corpse.

## Suggested Review Order

**Attack lifecycle follows the Animator**

- Entry point: attack ends only when no attack state is left, not on input.
  [`PlayerCombat.cs:485`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L485)

- Count/queue model; fresh press resyncs drift from a lost exit.
  [`PlayerAttackTracker.cs:27`](../../Assets/_Game/Scripts/Combat/PlayerAttackTracker.cs#L27)

- Crossfade close-event guard replaces the old `_IsComboAttacking` flag.
  [`PlayerAttackTracker.cs:57`](../../Assets/_Game/Scripts/Combat/PlayerAttackTracker.cs#L57)

- Single full-end path: queue, combo, IsAttacking, hitbox, triggers.
  [`PlayerCombat.cs:325`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L325)

- Watchdog: no attack callback for the timeout forces the end.
  [`PlayerCombat.cs:169`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L169)

- Combo press registered after the trigger fires.
  [`PlayerCombat.cs:267`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L267)

**Cancel & stale-event safety**

- Dodge does a full combo reset instead of clearing IsAttacking only.
  [`DodgeController.cs:193`](../../Assets/_Game/Scripts/Combat/DodgeController.cs#L193)

- Blending-out clip can't reopen the hitbox after a cancel.
  [`PlayerCombat.cs:456`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L456)

- Ending an attack clears latched Attack_1/2/3 triggers.
  [`PlayerStateManager.cs:137`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L137)

- `Attack_1 → Attack_2` interruption source now None (matches 2 → 3).
  [`Humanoid_Template.controller:2306`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L2306)

**Lock-on acquisition**

- One candidate per EntityHealth instead of per ragdoll collider.
  [`LockOnSystem.cs:90`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L90)

- Pure nearest-in-cone selection with range re-check at the root.
  [`LockOnSystem.cs:130`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L130)

- Buffer 10 → 64; one spider alone filled the old buffer.
  [`LockOnSystem.cs:27`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L27)

**Peripherals**

- Watchdog tuning field.
  [`CombatConfigSO.cs:54`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L54)

- Lifecycle tests incl. lost exit and late press.
  [`PlayerAttackTrackerTests.cs:107`](../../Assets/Tests/EditMode/PlayerAttackTrackerTests.cs#L107)

- Lock-on selection tests against the real helper.
  [`LockOnTests.cs:94`](../../Assets/Tests/EditMode/LockOnTests.cs#L94)
