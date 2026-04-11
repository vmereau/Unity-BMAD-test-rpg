---
title: 'Idle Wander Behavior for EnemyBrain'
slug: 'idle-wander-behavior'
created: '2026-04-11'
status: 'Implementation Complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['C#', 'Unity NavMesh', 'ScriptableObject']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs'
  - 'Assets/_Game/Scripts/AI/EnemyBrain.cs'
code_patterns: ['NavMesh.SamplePosition', 'FormerlySerializedAs', 'GameLog', 'EnemyState']
test_patterns: ['playtest wander bounds', 'detection transition', 'disengage return']
---

# Tech-Spec: Idle Wander Behavior for EnemyBrain

**Created:** 2026-04-11

## Overview

### Problem Statement

`HandleIdle()` is a no-op — enemies with no waypoints assigned stand frozen in place. They cannot detect the player and have no ambient life. The `PatrolSpeed` field name on `EnemyTypeSO` is also semantically wrong since the same speed is now used for idle wandering.

### Solution

Give Idle enemies a random wander loop within a configurable radius around their spawn/origin position. Extract shared player-detection logic to avoid duplication with `HandlePatrol`. Track the disengagement return state so enemies that were Idle return to their origin when the player leaves `DisengageRange`. Rename `PatrolSpeed` → `BaseSpeed` across the board.

### Scope

**In Scope:**
- `EnemyTypeSO`: rename `PatrolSpeed` → `BaseSpeed` (with `FormerlySerializedAs`), add `IdleWanderRadius`
- `EnemyBrain.HandleIdle()`: random wander within `IdleWanderRadius` of stored origin, using `BaseSpeed` and `PatrolWaitTime`
- Shared `IsPlayerInDetectionRange()` helper used by both `HandleIdle` and `HandlePatrol`
- `TransitionToIdle(Vector3 origin)` method, called from `Start()` and on disengage
- `_disengageState` field — tracks which state to return to after disengaging (Idle or Patrolling)
- `HandleEngage()` and `HandleAttack()` disengage paths both respect `_disengageState`
- NavMesh sampling failure: stay put and retry on next timer tick

**Out of Scope:**
- Idle animation variants (no animation changes)
- Idle sound or VFX
- Enemies with waypoints are unaffected in behaviour (only the `PatrolSpeed` rename touches them)
- Pathfinding tuning / NavMesh obstacle avoidance changes

---

## Context for Development

### Codebase Patterns

- All log calls use `GameLog.Info/Warn/Error(TAG, msg)` — never `Debug.Log` directly
- `EnemyTypeSO` uses `[SerializeField, FormerlySerializedAs("oldName")]` for every renamed field — **mandatory** to preserve existing asset data on all prefabs
- `NavMesh.SamplePosition(candidate, out NavMeshHit hit, maxRadius, NavMesh.AllAreas)` returns `false` if no valid point found — enemy must stay put silently and retry, no log spam
- `_waitTimer` is already used for patrol wait; **reuse the same field** for idle wait
- `_agent.isStopped = true` is used to park the agent while waiting at a waypoint; same pattern for idle wait
- All transition methods follow the pattern: set `_state`, configure agent properties, call `GameLog.Info`

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EnemyBrain.cs` | State machine to modify |
| `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs` | Config SO to extend and rename |

### Technical Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Wander speed | Reuse `BaseSpeed` (renamed from `PatrolSpeed`) | Same movement feel as patrol; avoids unnecessary proliferation of fields |
| Wander wait time | Reuse `PatrolWaitTime` | Consistent feel; user confirmed reuse |
| NavMesh sample failure | Stay put, no log, retry on next timer tick | Avoids log spam at navmesh edges |
| Disengage return state | `_disengageState` field (EnemyState) set in `TransitionToEngaging()` | Clean, no special-casing `_waypoints.Length` scattered around |
| `Start()` Idle init | Call `TransitionToIdle(transform.position)` instead of raw `_state = EnemyState.Idle` | Ensures `_idleOrigin` is set and first wander target is picked immediately |

---

## Implementation Plan

### Tasks

#### Task 1 — `EnemyTypeSO.cs`: Rename `PatrolSpeed` → `BaseSpeed`, add `IdleWanderRadius`

File: `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs`

**1a. Rename the backing field and property:**

```csharp
// BEFORE:
[SerializeField, FormerlySerializedAs("patrolSpeed")] private float _patrolSpeed = 2f;
// ...
public float PatrolSpeed => _patrolSpeed;

// AFTER:
[SerializeField, FormerlySerializedAs("patrolSpeed")] private float _baseSpeed = 2f;
// ...
public float BaseSpeed => _baseSpeed;
```

Note: `FormerlySerializedAs("patrolSpeed")` must be kept — it was already present and preserves data from older assets. The rename is purely on the C# identifier, not the serialized key.

**1b. Add `IdleWanderRadius` under a new `[Header("Idle")]` group, after the `[Header("Patrol")]` block:**

```csharp
[Header("Idle")]
[SerializeField] private float _idleWanderRadius = 5f;

// ...
public float IdleWanderRadius => _idleWanderRadius;
```

Default `5f` is a reasonable starting value; each `EnemyTypeSO` asset can override it.

---

#### Task 2 — `EnemyBrain.cs`: Fix all `_type.PatrolSpeed` references → `_type.BaseSpeed`

File: `Assets/_Game/Scripts/AI/EnemyBrain.cs`

In `AdvanceToNextWaypoint()`:
```csharp
// BEFORE:
_agent.speed = _type.PatrolSpeed;
// AFTER:
_agent.speed = _type.BaseSpeed;
```

This is the only callsite. Verify with a grep for `PatrolSpeed` — there should be no remaining references after this change.

---

#### Task 3 — `EnemyBrain.cs`: Add new fields

Add these private fields in the fields block (after `_attackCooldownTimer`):

```csharp
private Vector3 _idleOrigin;
private EnemyState _disengageState = EnemyState.Patrolling;
```

`_idleOrigin` stores the world position saved when the enemy first enters Idle.
`_disengageState` stores which state to return to when the player leaves `DisengageRange`.

---

#### Task 4 — `EnemyBrain.cs`: Extract `IsPlayerInDetectionRange()` helper

Add this private helper method (place it near `HandlePatrol` for locality):

```csharp
private bool IsPlayerInDetectionRange() =>
    _player != null && Vector3.Distance(transform.position, _player.position) <= _type.DetectionRange;
```

Then update `HandlePatrol()` to use it:

```csharp
// BEFORE:
if (_player != null && Vector3.Distance(transform.position, _player.position) <= _type.DetectionRange)

// AFTER:
if (IsPlayerInDetectionRange())
```

---

#### Task 5 — `EnemyBrain.cs`: Add `TransitionToIdle(Vector3 origin)`

Add the transition method alongside the other `TransitionTo*` methods:

```csharp
private void TransitionToIdle(Vector3 origin)
{
    _state = EnemyState.Idle;
    _idleOrigin = origin;
    _waitTimer = 0f; // pick a wander target immediately on next HandleIdle tick
    _agent.isStopped = false;
    _agent.stoppingDistance = 0f;
    _agent.speed = _type.BaseSpeed;
    GameLog.Info(TAG, $"{gameObject.name} transitioned to Idle at {origin}");
}
```

Setting `_waitTimer = 0f` ensures `HandleIdle` picks a wander destination on the very first frame.

---

#### Task 6 — `EnemyBrain.cs`: Implement `HandleIdle()` and `PickIdleWanderTarget()`

Replace the empty `HandleIdle()`:

```csharp
private void HandleIdle()
{
    if (IsPlayerInDetectionRange())
    {
        TransitionToEngaging();
        return;
    }

    if (_agent.pathPending) return;

    if (_agent.remainingDistance <= _type.WaypointArrivalThreshold)
    {
        _waitTimer -= Time.deltaTime;
        if (_waitTimer <= 0f)
        {
            PickIdleWanderTarget();
        }
        else
        {
            _agent.isStopped = true;
        }
    }
}
```

Add the wander target picker (place it near `AdvanceToNextWaypoint` for locality):

```csharp
private void PickIdleWanderTarget()
{
    Vector3 randomOffset = Random.insideUnitSphere * _type.IdleWanderRadius;
    randomOffset.y = 0f;
    Vector3 candidate = _idleOrigin + randomOffset;

    if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, _type.IdleWanderRadius, NavMesh.AllAreas))
    {
        _agent.isStopped = false;
        _agent.SetDestination(hit.position);
        _waitTimer = _type.PatrolWaitTime;
    }
    // If SamplePosition fails: stay put, timer stays <= 0, retry next frame
}
```

---

#### Task 7 — `EnemyBrain.cs`: Update `TransitionToEngaging()` to save `_disengageState`

```csharp
private void TransitionToEngaging()
{
    _disengageState = _state; // remember Idle or Patrolling for disengage return
    _state = EnemyState.Engaging;
    _agent.isStopped = false;
    _agent.stoppingDistance = _type.EngageStoppingDistance;
    _agent.speed = _type.EngageSpeed;
    _agent.SetDestination(_player.position);
    GameLog.Info(TAG, $"{gameObject.name} engaged player");
}
```

---

#### Task 8 — `EnemyBrain.cs`: Update disengage paths in `HandleEngage()` and `HandleAttack()`

Extract a shared disengage helper:

```csharp
private void DisenageFromCombat()
{
    if (_disengageState == EnemyState.Idle)
    {
        GameLog.Info(TAG, $"{gameObject.name} disengaged — resuming Idle at origin");
        TransitionToIdle(_idleOrigin);
    }
    else
    {
        GameLog.Info(TAG, $"{gameObject.name} disengaged — returning to patrol");
        TransitionToPatrol();
    }
}
```

In `HandleEngage()`, replace both `TransitionToPatrol()` disengage calls:

```csharp
// BEFORE (player lost):
GameLog.Warn(TAG, "Player lost — returning to patrol");
TransitionToPatrol();

// AFTER:
GameLog.Warn(TAG, "Player lost — disengaging");
DisenageFromCombat();
```

```csharp
// BEFORE (out of range):
GameLog.Info(TAG, "Disengaged — player out of range");
TransitionToPatrol();

// AFTER:
DisenageFromCombat();
```

In `HandleAttack()`, replace the disengage `TransitionToPatrol()` call:

```csharp
// BEFORE:
GameLog.Info(TAG, "Disengaged from attack — player out of range");
TransitionToPatrol();

// AFTER:
DisenageFromCombat();
```

---

#### Task 9 — `EnemyBrain.cs`: Update `Start()` to call `TransitionToIdle()`

```csharp
// BEFORE:
if (_waypoints == null || _waypoints.Length == 0)
{
    GameLog.Warn(TAG, $"{gameObject.name}: No waypoints assigned — remaining Idle");
    _state = EnemyState.Idle;
    return;
}

// AFTER:
if (_waypoints == null || _waypoints.Length == 0)
{
    GameLog.Info(TAG, $"{gameObject.name}: No waypoints assigned — entering Idle wander");
    TransitionToIdle(transform.position);
    return;
}
```

---

#### Task 10 — `EnemyBrain.cs`: Update summary comment block

Update the class-level `<summary>` to reflect Idle wander:

```
// BEFORE:
/// Idle → Patrolling → Engaging → Attacking.

// AFTER:
/// Idle: saves origin, wanders randomly within IdleWanderRadius; detects player → Engaging.
/// Patrolling: cycles between waypoints; detects player → Engaging.
```

Also add a story reference line:
```
/// tech-spec-idle-wander-behavior: Added Idle wander, shared detection helper, disengage state tracking; renamed PatrolSpeed → BaseSpeed.
```

---

### Acceptance Criteria

**AC1 — Idle wander activates on spawn**
- Given: Enemy prefab has no waypoints assigned
- When: Scene starts
- Then: Enemy begins moving to a random position within `IdleWanderRadius` of its spawn point

**AC2 — Wander stays within radius**
- Given: Enemy is wandering in Idle state
- When: Observing over multiple wander cycles
- Then: All wander destinations are within `IdleWanderRadius` of the stored `_idleOrigin` (NavMesh sample may land slightly inside the radius)

**AC3 — Idle enemy detects player**
- Given: Enemy is in Idle state, wandering
- When: Player walks within `DetectionRange`
- Then: Enemy transitions to Engaging and chases the player

**AC4 — Disengage from Idle returns to Idle at origin**
- Given: Enemy was in Idle state when it detected the player
- When: Player moves beyond `DisengageRange`
- Then: Enemy stops chasing and resumes Idle wander around the original `_idleOrigin` (not the current position)

**AC5 — Disengage from Patrol returns to Patrol**
- Given: Enemy was in Patrolling state when it detected the player
- When: Player moves beyond `DisengageRange`
- Then: Enemy resumes patrolling between waypoints (existing behavior, must not regress)

**AC6 — Disengage from Attacking respects origin state**
- Given: Enemy transitioned Idle → Engaging → Attacking
- When: Player moves beyond `DisengageRange` while enemy is in Attacking state
- Then: Enemy returns to Idle wander at original origin

**AC7 — NavMesh sample failure: no crash, no spam**
- Given: Enemy's `_idleOrigin` is near a NavMesh boundary
- When: `NavMesh.SamplePosition` fails to find a valid point
- Then: Enemy remains stopped, no log error is emitted, and the picker is retried on the next timer tick

**AC8 — Patrol behavior unchanged for enemies with waypoints**
- Given: Enemy prefab has waypoints assigned
- When: Normal patrol cycle runs
- Then: Movement speed and behavior identical to before (only `PatrolSpeed` → `BaseSpeed` rename, no functional change)

**AC9 — Existing EnemyTypeSO assets preserve `_baseSpeed` value after rename**
- Given: Existing `.asset` files have `patrolSpeed: 2` serialized
- When: Unity loads the assets after the rename
- Then: `BaseSpeed` reads the correct value (verified by `FormerlySerializedAs("patrolSpeed")`)

---

## Additional Context

### Dependencies

- `NavMesh.SamplePosition` requires the scene to have a baked NavMesh — all existing enemy scenes already do
- `Random.insideUnitSphere` is available without any extra `using` directive (UnityEngine)
- `EnemyTypeSO` assets must be saved/reimported in the Editor after the script change for the new `IdleWanderRadius` field to appear in the Inspector

### Testing Strategy

1. **In-editor playtest**: Place an enemy with no waypoints in the test scene, enter Play mode, observe wander loop
2. **Detection test**: Walk player into detection range during wander — confirm state change
3. **Disengage test**: Engage from Idle, then walk player out of DisengageRange — confirm enemy returns to origin, not to current position
4. **Patrol regression**: Verify a waypointed enemy still patrols correctly and speed is unchanged
5. **Edge test**: Place enemy origin right at a NavMesh boundary — verify no errors when `SamplePosition` fails

### Notes

- `_disengageState` defaults to `EnemyState.Patrolling` — safe fallback if `TransitionToEngaging` is ever called before `Start()` completes (shouldn't happen in practice)
- `DisenageFromCombat()` method name has a deliberate check: verify spelling matches convention in the codebase (`Disengage` not `Disenage`) before committing — correct it if needed
- The `TAG = "[AI]"` constant is already used in this class alongside `GameLog` calls — no change needed there
- If `IdleWanderRadius = 0` on a `EnemyTypeSO` asset, `NavMesh.SamplePosition` will immediately find the current position — enemy will effectively stand still and wait, then immediately pick again. This is a degenerate but non-crashing case; document it as a designer note on the SO
