---
title: 'NPC hearing — walking / running player noise feeds awareness'
type: 'feature'
created: '2026-11-09'
status: 'done'
baseline_commit: '9783f95'
context: ['{project-root}/Assets/_Game/Scripts/Stealth/CLAUDE.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Perception is vision-only (cone + LOS-gated 360° proximity circle). A player running right behind a guard, or walking past a wall, is never noticed — how the player moves (walk vs run) has no effect, only sneaking does.

**Approach:** Add a hearing layer: the player exposes a movement-noise level (Silent / Walk / Run, from actual speed), each `Entity` gets a walk and a run hearing radius (360°, halved when LOS is blocked), and a heard target fills the awareness meter at a fixed rate in **hostile** and **trespass** perception modes. Vision keeps working unchanged alongside it.

## Boundaries & Constraints

**Always:** Pure math in `Game.Stealth` statics with EditMode tests. Sneaking = Silent; standing still (speed < min) = Silent. Swimming = Walk, swim-sprinting = Run. Hearing feeds the same awareness meter (→ Suspicious turn toward the noise → full awareness → existing reactions). New tunables on `Entity` (radii) / `StealthConfigSO` (fill time, thresholds, wall factor). Debug read-outs on `EntityPerception`; gizmos + F3 overlay show the hearing circles and a "Heard" flag.

**Ask First:** Hearing for witness mode; instant detection by noise; changing vision values.

**Never:** Hearing in witness mode; hearing in theft / container detection (`CanSeeTheft`); unknown-source hit retargeting picking a merely-heard target; NPC-vs-NPC hearing; audio-event-driven noise (footstep SFX).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Run behind hostile | Bandit idle, player runs 7 m behind it, LOS clear | Heard (run radius 9) → meter fills ~2 s → Suspicious faces player → vision completes | N/A |
| Walk behind hostile | Player walks 3 m behind (walk radius 4) | Heard → fills | N/A |
| Walk out of range | Walks 6 m behind | Not heard, not seen → drains | N/A |
| Behind a wall | Runs 6 m away, LOS blocked | Radius halved (4.5) → not heard | N/A |
| Sneak / still | Sneaking or standing still at 1 m behind, LOS blocked | Silent → nothing (proximity still LOS-gated as today) | N/A |
| Trespass | Owner, player runs inside its area out of view | Heard → trespass meter → existing trespass response | N/A |
| Area not owned / witness | Non-hostile player running near a non-owner NPC | No hearing (witness mode only reacts to sneaking) | N/A |
| Theft | Steal while walking near an NPC, out of view | No theft detection (vision only) | N/A |
| Engaged, out of sight | Engaging, player runs behind a crate within run radius | Still sensed → `TimeSinceSeen` reset, chases heard position, no Searching | N/A |
| Searching | Player walks within walk radius | Meter refills from search awareness → re-engages | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Stealth/IStealthTarget.cs` -- add `MovementNoise Noise { get; }`
- `Assets/_Game/Scripts/Stealth/StealthDetection.cs` -- pure math; add noise classification + hearing
- `Assets/_Game/Scripts/Player/PlayerSneak.cs` -- the player's `IStealthTarget`; measures speed
- `Assets/_Game/Scripts/Player/PlayerSwimming.cs` -- `IsSwimSprinting`
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- per-entity perception fields + `OnValidate`
- `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` -- global stealth tunables
- `Assets/_Game/Scripts/AI/EntityPerception.cs` -- `Tick` / `ScanForTarget`, gizmos
- `Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs` -- F3 overlay
- `Assets/Tests/EditMode/StealthDetectionTests.cs` -- tests

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Stealth/MovementNoise.cs` (+ meta) -- `enum MovementNoise { Silent, Walk, Run }` in `Game.Stealth` -- shared contract
- [x] `Scripts/Stealth/IStealthTarget.cs` -- add `Noise` -- polled by perception
- [x] `Scripts/Stealth/StealthDetection.cs` -- `ClassifyNoise(sneaking, swimming, swimSprinting, horizontalSpeed, minSpeed, runSpeed)`; `HearingRadius(noise, walkRadius, runRadius, hasLOS, occludedFactor)` (Silent → 0); `IsHeard(distance, radius)` (radius > 0 && distance ≤ radius) -- pure, testable
- [x] `ScriptableObjects/Config/StealthConfigSO.cs` -- `[Header("Hearing")]`: `hearingFillTime` 2 s, `occludedHearingFactor` 0.5, `noiseMinSpeed` 0.5 m/s, `runNoiseSpeed` 4.5 m/s -- global tuning
- [x] `ScriptableObjects/Entities/Entity.cs` -- `_walkHearingRadius` 4, `_runHearingRadius` 9 (Perception header, tooltips, getters); `OnValidate`: ≥ 0, run ≥ walk -- per-entity (spider vs guard)
- [x] `Scripts/Player/PlayerSneak.cs` -- smoothed flat speed from position delta each `Update`; `Noise` via `ClassifyNoise` (optional same-GO `PlayerSwimming`) -- actual movement, not the Sprint key
- [x] `Scripts/AI/EntityPerception.cs` -- outside witness mode compute `CanHearTarget` (radius from noise, halved if `!_hasLineOfSight`). Not engaged: fill = max(vision fill, heard ? 1/`hearingFillTime` : 0); meter steps while seen **or** heard. Engaged: sensed = seen or heard. `LastSeenPosition` / `TimeSinceSeen` refresh while sensed (doc: "last sensed"). `CanSeeTarget` stays vision-only. Scan ranges include `RunHearingRadius`. Read-outs `CanHearTarget`, `CurrentHearingRadius`, `TargetNoise`, cleared with the others; gizmo walk/run circles + label "Heard" -- the hearing layer
- [x] `Scripts/Debug/StealthDebugOverlay.cs` -- hearing circles + "Heard" in label/summary -- tuning
- [x] `Assets/Tests/EditMode/StealthDetectionTests.cs` -- cover classification (sneak, still, walk, run, swim, swim sprint), radius (silent 0, occluded half), `IsHeard` edges
- [x] `Scripts/Stealth/CLAUDE.md`, `Scripts/AI/CLAUDE.md` -- document hearing formula, values, mode rules

**Acceptance Criteria:**
- Given the F3 overlay, when an entity is drawn, then its walk and run hearing circles are visible and the label shows "Heard" while the player is heard.
- Given the EditMode suite, when run, then all tests pass.
- Given existing entity assets, when loaded, then they get the default radii (no asset migration needed).

## Design Notes

Noise is read from real speed so Shift held while standing still, or pushed against a wall, stays silent. Hearing does not set `CanSeeTarget`: brain checks that mean "sees" (unknown-source retarget, Watching trespass check) keep vision semantics, while the chase / search logic already reads `LastSeenPosition` + `TimeSinceSeen`, which now track "last sensed".

## Verification

**Commands:**
- `run_tests` (EditMode) -- expected: all pass
- `read_console` -- expected: no compile errors

**Manual checks:**
- Play mode, F3: run behind the bandit → Suspicious turn then engage; sneak behind → nothing; walk through the docks restricted area behind a guard → trespass meter fills.

## Suggested Review Order

**Hearing in perception**

- Entry point: hearing radius computed per tick, witness mode excluded.
  [`EntityPerception.cs:219`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L219)

- Radii capped at the sight range — prevents the Warning ⇄ cancel loop.
  [`EntityPerception.cs:95`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L95)

- Meter takes the max of vision and hearing fill; fills while seen or heard.
  [`EntityPerception.cs:244`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L244)

- Last seen / time since seen now mean "last sensed"; CanSeeTarget stays vision-only.
  [`EntityPerception.cs:257`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L257)

- Engaged: hearing never holds a target past DisengageRange.
  [`EntityPerception.cs:230`](../../Assets/_Game/Scripts/AI/EntityPerception.cs#L230)

**Player noise**

- Noise from real smoothed speed, not the Sprint key.
  [`PlayerSneak.cs:34`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L34)

- Speed sampling; implausible speed (teleport / warp) resets to silent.
  [`PlayerSneak.cs:94`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L94)

**Pure math**

- Silent / Walk / Run classification, swimming rules.
  [`StealthDetection.cs:79`](../../Assets/_Game/Scripts/Stealth/StealthDetection.cs#L79)

- Radius per noise, halved without LOS.
  [`StealthDetection.cs:91`](../../Assets/_Game/Scripts/Stealth/StealthDetection.cs#L91)

**Tunables, debug, tests**

- Per-entity walk / run radii (4 / 9 m defaults).
  [`Entity.cs:44`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L44)

- Global hearing fill time, occlusion factor, speed thresholds.
  [`StealthConfigSO.cs:47`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L47)

- F3 overlay hearing circles and player noise read-out.
  [`StealthDebugOverlay.cs:264`](../../Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs#L264)

- Classification / radius / IsHeard edge tests.
  [`StealthDetectionTests.cs:178`](../../Assets/Tests/EditMode/StealthDetectionTests.cs#L178)
