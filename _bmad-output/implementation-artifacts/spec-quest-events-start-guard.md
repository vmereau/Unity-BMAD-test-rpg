---
title: 'Quest events wait for the quest to start'
type: 'bugfix'
created: '2026-11-25'
status: 'done'
baseline_commit: 'b727e4f9'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `QuestEventsManager` raises completed, failed and step-completed events (and `PlayerRewards` pays their rewards) as soon as their facts turn true, even when the quest hasn't started, so a reward lands with no quest in the log. Because the snapshot records steps that were already true while the quest wasn't started, those steps never fire once the quest does start.

**Approach:** Track a quest's *reported* state, not its raw facts: completed, failed and every step count as true only while the quest is started. A quest that starts with completion / steps already true then raises them all in the start evaluation, in order: started, then steps (by index), then completed / failed. A small pure rules class computes the snapshot and the ordered events so it can be unit-tested.

## Boundaries & Constraints

**Always:** `ReseedState` (start + after a save load) still raises nothing, and seeds the same reported state, so a loaded save neither re-fires nor loses events. Each event fires at most once per transition. Steps fire in index order.

**Ask First:** Changing `QuestSO` state getters (`IsStarted` / `IsCompleted` / `IsFailed` / `IsStepCompleted`) or `QuestFact`: other readers (quest log, memory conditions) use the raw values.

**Never:** No data / asset change, no change to `PlayerRewards` or the quest log UI. Quest items and theft-return are out of scope (deferred: *From spec-tithe-barn-proof-and-leads*).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Step before start | not started, step 0 fact turns true | no event | N/A |
| Start with steps done | prev: not started; now started, steps 0 and 2 true | started, step 0, step 2 | N/A |
| Start already completed | prev: not started; now started + completed, step 1 true | started, step 1, completed | N/A |
| Failed before start, then start | fail fact true first, then start fact | nothing, then started + failed | N/A |
| Normal progress | started; step 1 turns true | step 1 only | N/A |
| Load a started quest | `ReseedState` with started, step 0 true; next fact change, nothing new | no event | N/A |
| Load an unstarted quest with step 0 true, then start | reseed, then start fact | started, step 0 | N/A |
| Step count changed | previous snapshot length ≠ step count | missing previous entries count as false | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` -- snapshot (`QuestStateSnapshot`, `ReseedState`, `BuildStepSnapshot`) and transitions (`EvaluateQuest`); raises the four `GameEventSO_Quest*` channels
- `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` -- raw `IsStarted` / `IsCompleted` / `IsFailed` / `IsStepCompleted` (read only, unchanged)
- `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` -- pays rewards on those events (unchanged; the reason the guard matters)
- `Assets/_Game/Scripts/Core/Save/SaveSystem.cs:499` -- calls `ReseedState` after a load

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Quest/QuestEventRules.cs` -- new pure static class (`Game.Quest`): builds the reported state from raw values (started, completed, failed, step flags → completed / failed / steps forced false while not started) and lists the transitions between two reported states in the order started → steps → completed → failed -- testable without `WorldStateManager`
- [x] `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` -- seed and compare reported states through `QuestEventRules`, raise the listed events in order, keep the existing log lines -- the fix
- [x] `Assets/Tests/EditMode/QuestEventRulesTests.cs` -- one test per I/O matrix row -- pins the behaviour

**Acceptance Criteria:**
- Given TitheBarnShortfall not started, when the tally fact (step "Something with his name on it") turns true, then no step event or reward fires, and when Pell's start fact turns true the started event and that step event fire in the same evaluation.
- Given the EditMode suite, when it runs, then `QuestEventRulesTests` and the existing quest tests pass.

## Design Notes

Steps now fire before completed (previously after) also during normal play, when the last step and the completion fact turn true in the same change: toasts and rewards read "step → quest done", which matches the journal order.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode, filter `QuestEventRules`) -- expected: all pass; then the full EditMode suite -- no new failures
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown: take the tally from Brann's stash before talking to Pell → no step toast or reward; start the quest → started then step messages in the `[QuestEvents]` log.

## Suggested Review Order

**Reported state and event order**

- Entry point: completed / failed / steps count only while started.
  [`QuestEventRules.cs:41`](../../Assets/_Game/Scripts/Quest/QuestEventRules.cs#L41)

- Started latches once reported, so a start fact flipping back re-raises nothing (review patch).
  [`QuestEventRules.cs:58`](../../Assets/_Game/Scripts/Quest/QuestEventRules.cs#L58)

- Transitions in raise order: started → steps → completed → failed.
  [`QuestEventRules.cs:65`](../../Assets/_Game/Scripts/Quest/QuestEventRules.cs#L65)

**Manager wiring**

- Snapshot stored before raising; events raised from the ordered list.
  [`QuestEventsManager.cs:97`](../../Assets/_Game/Scripts/Quest/QuestEventsManager.cs#L97)

- Re-entrant fact changes queued until the current raise finishes (review patch).
  [`QuestEventsManager.cs:76`](../../Assets/_Game/Scripts/Quest/QuestEventsManager.cs#L76)

- Reseed uses the same rules; warns on a quest with no start fact.
  [`QuestEventsManager.cs:44`](../../Assets/_Game/Scripts/Quest/QuestEventsManager.cs#L44)

**Tests**

- One test per I/O row, plus latch and order cases.
  [`QuestEventRulesTests.cs:8`](../../Assets/Tests/EditMode/QuestEventRulesTests.cs#L8)

- Existing reseed tests now start their quest; new re-entrancy order test.
  [`WorldStateManagerFactsTests.cs:451`](../../Assets/Tests/EditMode/WorldStateManagerFactsTests.cs#L451)
