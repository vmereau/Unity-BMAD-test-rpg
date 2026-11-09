---
title: 'Move patrol waypoints from EntityBrain into NpcRoutine blocks (Patrol activity)'
type: 'refactor'
created: '2026-11-09'
status: 'done'
baseline_commit: 'b1142ce'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Patrol waypoints live on `EntityBrain`, while `NpcRoutine` blocks only send a stand spot; a spotless block silently means "home = brain waypoints". An NPC can have only one patrol route, only as its home behaviour, and the brain keeps a second patrol return path (`_disengageState`) next to the routine one.

**Approach:** Make patrolling an explicit routine activity: `NpcActivity.Patrol` with a per-block waypoint list. Remove `_waypoints` and the non-routine patrol fallback from `EntityBrain`; the routine hands the brain either a stand spot or a route, and the brain stays the only mover.

## Boundaries & Constraints

**Always:** Brain remains the only mover (`NavMeshAgent`). Existing combat / witness / theft / trespass / stance flows keep returning to the routine target unchanged. Migrate all StartingTown patrol data (DockGuard, TentMate, WarehouseGuard, bandit) so in-game behaviour is identical. Waypoints authored as a plain `Transform` list on the block. Snap into a Patrol block warps to the **nearest** waypoint and continues the loop from it.

**Ask First:** Any change to monster prefabs / `MonsterEntity` idle behaviour; any scene change beyond the routine/waypoint migration.

**Never:** Monster patrol support (deferred). Saving routine state. A route-parent / children-based route format. Keeping `_waypoints` on the brain as a fallback.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Patrol block | block activity Patrol, ≥1 waypoint | Brain Patrolling the loop, `PatrolWaitTime` at each waypoint | N/A |
| Snap into Patrol | load / sleep lands in Patrol block | Warp to nearest waypoint, next destination = following one | Warp fails → walk to nearest, one warn |
| Walk into Patrol | natural hour change | Walk to nearest waypoint, then loop | N/A |
| Patrol, no waypoints | Patrol block with empty / all-null list | Treated as Stand at spot / spawn spot | One warn per block index |
| Stand block | spot set / null | Stand at spot / spawn spot, face its yaw (unchanged) | N/A |
| No block for hour | none matches | Stand at spawn spot (no more patrol "home") | N/A |
| No clock | `GameClock.Active == null` | First all-day block (start == end) applies; none → no routine (brain idle wander) | One info log |
| After combat | same route still active | Resume the current waypoint index | N/A |
| No routine component | e.g. monster | Idle wander around spawn; after combat back to Idle at origin | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs` -- block data + `NpcActivity` enum + pure `NpcRoutineSchedule`
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs` -- clock polling, block change → brain
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `_waypoints`, `AdvanceToNextWaypoint`, `SetRoutineSpot`, `ApplyRoutineTarget`, `ResumeNonCombat`, `TransitionToPatrol`, `DisengageFromCombat`, `CancelWarning`, `_disengageState`
- `Assets/_Game/Scenes/StartingTown.unity` -- 4 patrollers; `Data/Events/OnTimeSkipped.asset` for new routine components
- `Assets/Tests/EditMode/NpcRoutineScheduleTests.cs` -- schedule tests
- `Assets/_Game/Scripts/AI/CLAUDE.md` -- routine docs

## Tasks & Acceptance

**Execution:**
- [x] `NpcRoutineBlock.cs` -- add `NpcActivity.Patrol`, `List<Transform> waypoints` (tooltip); add pure `NpcRoutineSchedule.FindAllDayBlock(blocks)` and `NearestIndex(IReadOnlyList<Vector3>, Vector3)` (flat distance) -- data + testable logic
- [x] `NpcRoutine.cs` -- send `SetRoutineTarget(spot, route-or-null, snap)`; route = non-null waypoints of a Patrol block (empty → null + warn); no-clock → all-day block once -- routine owns where to go
- [x] `EntityBrain.cs` -- remove `_waypoints`, `_disengageState`, `TransitionToPatrol`; `SetRoutineSpot` → `SetRoutineTarget(Transform spot, IReadOnlyList<Transform> route, bool snap)`; new route resets index to nearest, same route keeps it; patrol uses `_routeWaypoints`; snap warps to nearest waypoint; non-routine exits go to `TransitionToIdle(_idleOrigin)`; `Start` always enters Idle -- single patrol path
- [x] Migration (temporary `execute_code` before removing the brain field): DockGuard 21–06 block → Patrol with its 5 waypoints; TentMate 06–21 block → Patrol with its 2; WarehouseGuard, bandit → add `NpcRoutine` (brain + `OnTimeSkipped` wired) with one 0–0 Patrol block holding their waypoints; save scene -- behaviour parity
- [x] `NpcRoutineScheduleTests.cs` -- tests for `FindAllDayBlock`, `NearestIndex` -- matrix edge cases
- [x] `Scripts/AI/CLAUDE.md` -- rewrite "NPC routines" section; update `EntityBrain` row -- docs

**Acceptance Criteria:**
- Given the migrated scene, when played through a day, then DockGuard stands at the guard post 06–21 and patrols the deck 21–06, TentMate patrols by day and stands at its night spot, WarehouseGuard and bandit patrol all day.
- Given `EntityBrain`, when inspected, then it has no waypoint field and the code has no `_disengageState`.
- Given a patroller that disengages from combat, when it returns, then it resumes its current routine route.

## Design Notes

Route identity: compare the new route list reference / block index to the current one; only a different route recomputes the nearest index, so post-combat `ApplyRoutineTarget` resumes the loop. `HandlePatrol` / `AdvanceToNextWaypoint` keep their logic but read `_routeWaypoints`.

## Verification

**Commands:**
- Unity EditMode tests `NpcRoutineScheduleTests` -- expected: all pass
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play StartingTown, `GameClock.SkipTo` around 06 / 21: DockGuard warps to the nearest deck waypoint at 21, the guard post at 06.

## Suggested Review Order

**Routine data — patrol becomes an activity**

- New `Patrol` activity; routes now live on the block, not the brain.
  [`NpcRoutineBlock.cs:13`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs#L13)

- Block → brain: Patrol with live waypoints sends a route, otherwise a stand spot.
  [`NpcRoutine.cs:112`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L112)

- No clock: first all-day block applies once; snap kept for a late clock.
  [`NpcRoutine.cs:87`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs#L87)

**Brain — single patrol path**

- Entry point: route-or-spot API; new route defers its nearest-waypoint pick.
  [`EntityBrain.cs:1155`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1155)

- Prunes destroyed waypoints, resolves the pending nearest pick at apply time.
  [`EntityBrain.cs:1174`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1174)

- Content comparison so post-combat resends keep the current index.
  [`EntityBrain.cs:1198`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1198)

- Snap warps onto nearest waypoint, then continues with the following one.
  [`EntityBrain.cs:1245`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1245)

- Non-combat target: patrol route, else Idle walk to the stand spot.
  [`EntityBrain.cs:1266`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1266)

- No-routine exits collapse to Idle (`_disengageState` / `TransitionToPatrol` removed).
  [`EntityBrain.cs:1374`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1374)

- Start always Idle; OnEnable re-applies a stored request (respawn).
  [`EntityBrain.cs:298`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L298)
  [`EntityBrain.cs:250`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L250)

**Peripherals**

- Pure helper tests (`FindAllDayBlock`, `NearestIndex`).
  [`NpcRoutineScheduleTests.cs:67`](../../Assets/Tests/EditMode/NpcRoutineScheduleTests.cs#L67)

- Routine docs rewritten; StartingTown migration recorded.
  [`CLAUDE.md:37`](../../Assets/_Game/Scripts/AI/CLAUDE.md#L37)
