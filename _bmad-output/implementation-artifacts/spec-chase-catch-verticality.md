---
title: 'Chase catch takes height and line of sight into account'
type: 'bugfix'
created: '2026-11-27'
status: 'done'
baseline_commit: 'fd4ff221'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/PERCEPTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A guard chasing a trespasser (or thief) "catches" them on flat (XZ) distance only. With the player in the
warehouse cellar and the guard on the hall floor right above, the flat distance is under the 1.8 m catch distance, so
the scold dialogue opens through the floor instead of the guard walking down to the cellar.

**Approach:** The catch also needs the target on roughly the same level (vertical gap ≤ a new config value) and in
line of sight. While chasing, hearing the target counts like seeing it, so the guard follows the heard spot along the
NavMesh, and it does not count "time without sight" while it is still walking to the last spot it sensed the target at.

## Boundaries & Constraints

**Always:** Rules stay pure and unit-tested in `TheftDetection`. Applies to every Pursuing chase (theft and trespass —
same bug for both). Existing tuning values unchanged; the new height limit lives in `StealthConfigSO`.

**Ask First:** Changing `theftChaseLoseSightTime`, `theftChaseMaxDuration` or the catch distance.

**Never:** No 3D catch distance swap (a stair step must still count as caught); no change to perception / hearing math,
to the scold dialogue or to Confronting; no hatch / ladder work.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Same floor | flat 1.5 m, gap 0.1 m, LOS, cursor locked | caught → Confronting | N/A |
| Floor above | flat 0.5 m, gap 2.5 m | not caught; guard keeps pathing (via the dock door) | N/A |
| Stair / ramp | flat 1.5 m, gap 0.8 m, LOS | caught | N/A |
| Thin wall | flat 1.5 m, gap 0, no LOS | not caught; keeps pathing | N/A |
| Heard, not seen | player walking in cellar within hearing | last sensed spot updates, no-sight timer stays 0 | N/A |
| Sneaking silent in cellar | not seen nor heard | guard walks to last sensed spot; timer counts only once it got there; then gives up after `theftChaseLoseSightTime` | 30 s max duration still caps the chase |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Stealth/TheftDetection.cs` -- `CanCatch` (flat distance + cursor), `ShouldGiveUp`.
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `HandlePursuing` (~L1249): LOS timer, `_pursuitLastSeen`, `_pursuitNoSightTimer`, catch, destination.
- `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` -- Theft Chase header + `OnValidate`.
- `Assets/_Game/Data/Config/StealthConfig.asset` -- serialized values.
- `Assets/Tests/EditMode/TheftDetectionTests.cs` -- `CanCatch` tests.
- `Assets/_Game/Scripts/AI/PERCEPTION.md`, `Assets/_Game/Scripts/Stealth/CLAUDE.md` -- docs of the catch rule / config values.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Stealth/TheftDetection.cs` -- `CanCatch(flatDistance, catchDistance, heightGap, maxHeightGap, hasLineOfSight, cursorLocked)`: all must hold (|gap| ≤ max). Add `NextNoSightTime(current, dt, sensed, progressing)`: progressing → (sensed ? 0 : hold), standing still → +dt. -- pure rules.
- [x] `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` + `StealthConfig.asset` -- `theftCatchMaxHeight = 1.2f` (tooltip, `OnValidate` ≥ 0.3); tooltip of `theftCatchDistance` / lose-sight updated. -- one storey is ~2.5 m, a stair step well under 1.2 m.
- [x] `Assets/_Game/Scripts/AI/EntityBrain.cs` -- in `HandlePursuing`: `sensed = _pursuitHasLos || (perception target is the chased target && CanHearTarget)`; sensed → refresh `_pursuitLastSeen`; timer via `NextNoSightTime` (progressing = `pathPending`, or a path with `remainingDistance > stoppingDistance + 0.5` and speed ≥ 0.2 m/s); re-request the last spot only when it moved; catch passes the vertical gap and `_pursuitHasLos`; destination = target when sensed, else last spot. -- follow the guard down instead of scolding through the floor.
- [x] `Assets/Tests/EditMode/TheftDetectionTests.cs` -- update `CanCatch` tests to the new signature; add height-gap, no-LOS, stair cases, height-limit boundaries and `NextNoSightTime` cases. -- matrix coverage.
- [x] `Assets/_Game/Scripts/AI/PERCEPTION.md`, `Assets/_Game/Scripts/Stealth/CLAUDE.md` -- document the new catch rule, hearing during a chase, and the 1.2 m value.

**Acceptance Criteria:**
- Given the warehouse guard hears the player walking in `RA_WarehouseCellar` from the hall, when the chase starts, then no scold opens until the guard has reached the cellar and sees the player within 1.8 m.
- Given a theft chase on open ground, when the guard reaches the thief, then the catch behaves as before.
- Given EditMode tests, when run, then all `TheftDetectionTests` pass.

### Review loop 1 (2026-11-27)
- **Finding:** heard-but-unreachable thief (cellar under a shut hatch, wall), thief on a ledge > 1.2 m, blocked agent or
  `remainingDistance` Infinity / `pathPending` re-queued every frame → timer frozen at 0, chase hangs until the 30 s cap.
- **Amended:** timer rule `ShouldCountLostSight` → `NextNoSightTime` (standing still runs the timer even when sensed),
  "progressing" needs real speed, the last spot is re-requested only when it moved, `theftCatchMaxHeight` ≥ 0.3.
- **Avoids:** a guard standing 30 s over a player it can hear but never reach.
- **KEEP:** `CanCatch` signature (flat distance + height gap + LOS + cursor), heard-counts-as-sensed, the 1.2 m value.

## Design Notes

Hearing only exists in hostile / trespass perception modes (a theft inside an area the witness owns is trespass mode, so
it hears too). Pursuing is not an alerted state, so perception re-scans and re-acquires the trespasser after the
`ResetPerception` at chase start — `CanHearTarget` is valid once `_perception.Target == _currentTarget`.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode, filter `TheftDetectionTests` -- expected: all pass.
- `read_console` errors after compile -- expected: none.

**Manual checks (if no CLI):**
- Play: walk in the cellar under the guard; he must leave the hall, enter through the dock door and scold you in the cellar.

## Suggested Review Order

**Catch rule**

- Entry point: catch now needs the same level and line of sight, not just flat distance.
  [`EntityBrain.cs:1324`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1324)

- Pure rule: flat distance, absolute height gap, LOS, cursor.
  [`TheftDetection.cs:24`](../../Assets/_Game/Scripts/Stealth/TheftDetection.cs#L24)

**Following and giving up**

- Heard counts as sensed, so the guard paths after the noise into the cellar.
  [`EntityBrain.cs:1306`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1306)

- Give-up timer: runs whenever the guard stands still uncaught, even if it still hears.
  [`TheftDetection.cs:33`](../../Assets/_Game/Scripts/Stealth/TheftDetection.cs#L33)

- "Progressing" needs real speed, so blocked agents and Infinity distances don't freeze the timer.
  [`EntityBrain.cs:1339`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1339)

- Last spot re-requested only when it moved, so pathPending can't stay stuck.
  [`EntityBrain.cs:1333`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1333)

**Config, tests, docs**

- New 1.2 m height limit, clamped ≥ 0.3 m.
  [`StealthConfigSO.cs:89`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L89)

- Floor above / below, stair, wall and timer cases.
  [`TheftDetectionTests.cs:132`](../../Assets/Tests/EditMode/TheftDetectionTests.cs#L132)

- Chase rule documented for future sessions.
  [`PERCEPTION.md:52`](../../Assets/_Game/Scripts/AI/PERCEPTION.md#L52)
