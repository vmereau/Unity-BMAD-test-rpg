---
title: 'Hour- and NPC-gated WorldFactTrigger'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '9c698a73838b9c36e495fbf0949df6d47aa0fe22'
context:
  - '{project-root}/docs/World/Quests/TitheBarnShortfall.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `WorldFactTrigger` sets its fact whenever the player stands in its box. The TitheBarnShortfall stakeout (P3) needs `WorldFact_TitheBarn_ThiefCaught` to set only at the barn door between 00 and 03, while the guard Osric Venn is alive, conscious and standing at `Spot_BarnGuard_BarnDoor`.

**Approach:** Add two optional gates to `WorldFactTrigger`: an hour window (read from `GameClock.Active`) and a required NPC that must be present, alive, conscious and either in a named `NpcRoutine` block or within a radius of a spot. A pure static `WorldFactTriggerRules` makes the decision; EditMode tests cover it. Polling stays.

## Boundaries & Constraints

**Always:** The hour window starts inclusive, ends exclusive and wraps past midnight through `GameTime.IsHourInWindow`. Read the time only via `GameClock.Active` (region scenes can't reference Core). A trigger with the hour gate on and no clock never fires. The fact is set only when the player is inside a box **and** every enabled gate passes in the same poll. With no gates enabled, the behaviour must not change (same fields, same `_fact` name, existing scene instances unaffected). The NPC reference is a scene `EntityBrain`; health and routine come from its GameObject. An NPC counts as absent when the reference is null or destroyed, or when its GameObject is inactive in the hierarchy. It counts as down when `EntityHealth.IsDead` or `IsUnconscious` is true (the get-up clip included).

**Ask First:** Adding a gate mode beyond the ones listed. Changing how Quest Explorer validates or indexes triggers beyond reading `_fact`.

**Never:** No `OnTriggerEnter`. Don't create Venn, `Spot_BarnGuard_BarnDoor`, the barn trigger or any quest or fact asset (that's `/quests:implement`). No saved state on the trigger. Don't change routine scheduling.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No gates | player in box | fact set (as today) | N/A |
| Hour gate, in window | window 0→3, hour 1, clock present | passes | N/A |
| Hour gate, wrap | window 22→3, hour 23 / 2 / 3 / 21 | pass / pass / fail / fail | N/A |
| Hour gate, no clock | `GameClock.Active == null` | fails, never fires | N/A |
| NPC dead / unconscious | required NPC down | fails | N/A |
| NPC absent | reference null, destroyed or GO inactive | fails | N/A |
| Block mode | NPC current block label ≠ required (or no block) | fails | N/A |
| Spot mode | NPC flat (XZ) distance > radius | fails; ≤ radius passes | N/A |
| Misconfigured | NPC gate on but spot missing (spot mode) / empty label or no `NpcRoutine` (block mode) | trigger disabled | `GameLog.Error` in `Awake` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/WorldFactTrigger.cs` -- polled box trigger; gets the gate fields and calls the rules.
- `Assets/_Game/Scripts/World/RestrictedAreaRules.cs` -- `IsInsideBox`, `IsRestrictedHour` (pattern for the hour rule).
- `Assets/_Game/Scripts/Core/Time/GameTime.cs` / `GameClock.cs` -- `IsHourInWindow`, `GameClock.Active.Hour`.
- `Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs` / `NpcRoutine.cs` -- blocks have no name today; `NpcRoutine.CurrentBlock`.
- `Assets/_Game/Scripts/AI/EntityHealth.cs` -- `IsDead`, `IsUnconscious`.
- `Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs` -- indexes `WorldFactTrigger._fact` only; no change needed while `_fact` keeps its name.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs` -- add an optional `public string label` (tooltip: name used by gates such as `WorldFactTrigger`) -- gives "named block" a meaning; empty by default, so scenes are unaffected.
- [x] `Assets/_Game/Scripts/World/WorldFactTriggerRules.cs` -- new pure static class: `HourGatePasses(useHours, hasClock, hour, start, end)`, `NpcGatePasses(present, isDead, isUnconscious, placementOk)`, `IsInBlock(currentLabel, requiredLabel)` (ordinal, non-empty required), `IsNearSpot(npcPos, spotPos, radius)` (flat XZ, inclusive) -- testable decision.
- [x] `Assets/_Game/Scripts/World/WorldFactTrigger.cs` -- add the `Hour gate` fields (`_useHours`, `_startHour`, `_endHour` in [0, 23]) and the `Required NPC` fields (`_requiredNpc` EntityBrain, `_npcPlacement` enum Anywhere / InRoutineBlock / NearSpot, `_routineBlockLabel`, `_spot`, `_spotRadius` default 2). Cache `EntityHealth` / `NpcRoutine` in `Awake` and validate there (misconfigured → error + disable). Check the gates after the box test. Update the class doc -- the feature itself.
- [x] `Assets/Tests/EditMode/WorldFactTriggerRulesTests.cs` -- cover the I/O matrix rows on the rules -- required by the intent.
- [x] `Assets/_Game/Scripts/World/CLAUDE.md`, `Assets/_Game/Scripts/AI/CLAUDE.md` (block `label`), `docs/World/Quests/TitheBarnShortfall.md` (P3 done), `_bmad-output/implementation-artifacts/deferred-work.md` (strike P3) -- docs.

**Acceptance Criteria:**
- Given an existing `WorldFactTrigger` in StartingTown with no gates, when the player enters it, then the fact is set exactly as before.
- Given a trigger gated 00–03 with Venn in NearSpot mode, when the player stands in the box at 01:00 while Venn is 1 m from the spot, then the fact is set once; when Venn is knocked out, dead or elsewhere, nothing is set until the gates pass again while the player is still inside.
- Given the project compiles, when the EditMode tests run, then `WorldFactTriggerRulesTests` and the existing suites pass.

## Design Notes

Block mode passes as soon as the routine switches block, even while the NPC is still walking there. Spot mode means the NPC is really there, so the stakeout uses NearSpot with a radius of about 2 m (the routine's arrival threshold plus some slack). Use block mode only when being on the way is good enough.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass, incl. `WorldFactTriggerRulesTests`.
- `read_console` (errors) after compile -- expected: none.

## Suggested Review Order

**Gate flow in the trigger**

- Entry point: gates run only after the box test, in the same poll.
  [`WorldFactTrigger.cs:96`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L96)

- Hour gate via `GameClock.Active`; NPC presence = Unity-null + `activeInHierarchy`, then health.
  [`WorldFactTrigger.cs:102`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L102)

- Placement modes; a suspended routine fails `InRoutineBlock`.
  [`WorldFactTrigger.cs:122`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L122)

- Awake validation: misconfiguration disables, soft issues warn.
  [`WorldFactTrigger.cs:48`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L48)

- New serialized gate fields; defaults keep existing instances ungated.
  [`WorldFactTrigger.cs:26`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L26)

**Pure rules**

- No clock = never passes (unlike `RestrictedAreaRules.IsRestrictedHour`).
  [`WorldFactTriggerRules.cs:28`](../../Assets/_Game/Scripts/World/WorldFactTriggerRules.cs#L28)

- Flat XZ radius, inclusive.
  [`WorldFactTriggerRules.cs:40`](../../Assets/_Game/Scripts/World/WorldFactTriggerRules.cs#L40)

**Routine label**

- Optional block label, no routine effect.
  [`NpcRoutineBlock.cs:25`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs#L25)

- Label lookup for the Awake warning.
  [`NpcRoutine.cs:66`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L66)

**Tests**

- Hour wrap, all-day, no clock, NPC down / absent, block / spot placement.
  [`WorldFactTriggerRulesTests.cs:8`](../../Assets/Tests/EditMode/WorldFactTriggerRulesTests.cs#L8)
