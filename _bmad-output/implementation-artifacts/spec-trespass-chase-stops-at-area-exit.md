---
title: 'Trespass chase stops when the player leaves the restricted area'
type: 'feature'
created: '2026-11-09'
status: 'done'
baseline_commit: 'fd6c5d8'
context: ['{project-root}/_bmad-output/implementation-artifacts/spec-restricted-areas-trespass.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An owner who spotted a trespasser runs the theft chase, which keeps going past the restricted area until a catch or the theft give-up rules — a player who already obeyed and left still gets hunted down and scolded.

**Approach:** While Pursuing a trespasser (`TheftKind.Trespass` with an area), the owner ends the chase as soon as the player is outside the area (or the area stops being restricted), shouts one speech-bubble line from a new `TrespassFledBarks` set ("You better flee!", "I hope to never see you again here.", …) and resumes its patrol / routine. Also confirm that detection works while an NPC walks between patrol waypoints.

## Boundaries & Constraints

**Always:** Reuse `EndPursuit` → `ResumeNonCombat` (no new state). Exit decision is a pure static rule with EditMode tests. Bubble through the existing `RaiseSpeechBubble` channel with theft bubble duration / alert priority. Null fled barks → stop silently (no fallback to other sets). GameLog + TAG.

**Ask First:** Adding grace / cooldown on exit; changing the theft give-up rules.

**Never:** Changing theft (item / container) chases — a theft seen during a trespass chase switches `_pursuitKind` and keeps the theft rules; changing instant-aggression fights; changing the scene; HUD toasts.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Player flees | Trespass chase, player steps outside every box of the area | Owner stops, one fled bubble, back to patrol / routine | N/A |
| Hours end mid-chase | Docks chase at 05:59 → 06:00 | Chase ends (area no longer restricted), fled bubble | N/A |
| Still inside | Player inside, in or out of LOS | Chase continues by existing rules (catch → scold) | N/A |
| Theft during trespass chase | Player steals while chased | Kind becomes Item/Container → leaving the area does not stop the chase | N/A |
| Re-entry | Player walks back in after the owner gave up | Meter fills again from 0 (perception reset) → new warning / chase | N/A |
| Two owners | Both chasing one incident, player exits | Both stop, each may bubble | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityBrain.cs:831` -- `HandlePursuing` (give-up / catch), `EndPursuit`, `RaiseAlertBark`, `_pursuitKind` / `_pursuitArea`
- `Scripts/World/RestrictedArea.cs:59,87` -- `IsRestrictedNow`, `Contains`
- `Scripts/Stealth/TheftDetection.cs` -- pure chase rules (`ShouldGiveUp`, `CanCatch`)
- `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs:40` -- trespass bark fields
- `Data/Entities/WitnessProfile_Humanoid.asset`, `Data/NPCs/Barks/` -- bark data

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Stealth/TheftDetection.cs` -- add pure `ShouldStopTrespassChase(bool isTrespass, bool hasArea, bool areaRestrictedNow, bool targetInside)` (true only for a trespass chase with an area that is no longer restricted or no longer contains the target) -- testable rule
- [x] `Scripts/AI/EntityBrain.cs` -- in `HandlePursuing`, after the claimed / hostile checks and before give-up, evaluate the rule with `_pursuitArea.Contains(target pos)`; true → raise a `TrespassFledBarks` bubble and `EndPursuit("trespasser left the area")` -- behaviour
- [x] `ScriptableObjects/Entities/NPC/WitnessProfileSO.cs` -- `_trespassFledBarks` + `TrespassFledBarks` getter (no fallback) -- data
- [x] `Data/NPCs/Barks/Barks_TrespassFled.asset` (NEW, + meta) -- "You better flee!", "I hope to never see you again here.", "And stay out!", "Don't let me catch you here again."; assign on `WitnessProfile_Humanoid` -- data
- [x] `Assets/Tests/EditMode/TheftDetectionTests.cs` -- rule cases: trespass outside / inside / not restricted, non-trespass outside, no area -- matrix edges
- [x] `Scripts/AI/CLAUDE.md` -- trespass bullet: exit stops the chase + fled bark; patrol detection note -- docs

**Acceptance Criteria:**
- Given the WarehouseGuard chasing the player, when the player runs out of the warehouse, then the guard says a fled line and walks back to its patrol.
- Given the WarehouseGuard walking between two waypoints, when the player stands in its cone ahead of it, then the meter fills and it reacts (F3 overlay shows the meter rising while it moves).
- Given EditMode tests, when run, then all pass.

## Design Notes

Patrol detection (code read): `Update` ticks perception every frame in every state; `HandlePatrol` calls `TryDetectFromNonCombat` before path handling; the cone uses `transform.forward`, which the NavMeshAgent rotates along the path (`updateRotation` never disabled). So a walking guard sees ahead; a waiting one keeps its arrival facing. To be confirmed in play mode.

The exit check runs every frame (cheap box tests) without needing LOS — the owner knows the player left even when out of sight. No grace on exit: re-entering simply restarts the meter.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown: get spotted in the warehouse, run out — chase stops with a fled bubble; F3 overlay on the patrolling guard while it walks.

## Suggested Review Order

**Chase exit (behaviour)**

- Entry point: trespass chases check the area every Pursuing tick, before give-up / catch.
  [`EntityBrain.cs:856`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L856)

- Crossing into another area the same owner holds hands the chase over (review patch).
  [`EntityBrain.cs:863`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L863)

- Fled line uses its own no-repeat index, same bubble channel as alerts.
  [`EntityBrain.cs:833`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L833)

**Pure rule, data and tests**

- Stop only trespass chases with an area: outside, or no longer restricted.
  [`TheftDetection.cs:28`](../../Assets/_Game/Scripts/Stealth/TheftDetection.cs#L28)

- New bark field, no fallback (null = silent give-up).
  [`WitnessProfileSO.cs:45`](../../Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs#L45)

- `Barks_TrespassFled` assigned on the shared humanoid profile.
  [`WitnessProfile_Humanoid.asset:24`](../../Assets/_Game/Data/Entities/WitnessProfile_Humanoid.asset#L24)

- Rule cases incl. theft chases that must never stop here.
  [`TheftDetectionTests.cs:125`](../../Assets/Tests/EditMode/TheftDetectionTests.cs#L125)
