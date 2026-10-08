# CLAUDE.md — Assets/_Game/Scripts/Stealth

> Loaded when Claude accesses files in this folder. Namespace `Game.Stealth`. Stealth contracts, the
> detection math and its debug geometry. The perception component itself is `Scripts/AI/EntityPerception`
> (see `Scripts/AI/CLAUDE.md` → Perception & stealth); the sneak stance is `Scripts/Player/PlayerSneak`.

---

## What's here

| File | Role |
|------|------|
| `IStealthTarget` | `IsSneaking`, `VisibilityPoint`. Implemented by `PlayerSneak`; resolved once into `FactionMember.StealthTarget` (reads null while the component is disabled → plain radius detection). A member with one is acquired **only** through `EntityPerception` (skipped by the radius scan). |
| `StealthDetection` | Pure static math (no Unity objects, no allocations) — `StealthDetectionTests`. |
| `TheftDetection` | Pure theft rules — `EffectiveTheftRange`, `IsInView` (cone / proximity geometry, no falloff), `ShouldGiveUp`, `CanCatch`. `TheftDetectionTests`. |
| `StealthDebugGeometry` | Cone / circle line-segment builders into caller-owned buffers + `AwarenessColor`. Shared by the gizmos and the F3 overlay — `StealthDebugGeometryTests`. |
| `StealthDebug` | `#if UNITY_EDITOR` `DrawAllGizmos` flag (menu in `Editor/StealthDebugMenu`). |

Related: `ScriptableObjects/Config/StealthConfigSO` (`Game.Stealth`, asset `Data/Config/StealthConfig.asset`),
`Game.Combat.ISneakAttackTarget` (in `Scripts/Combat/`).

---

## Detection formula

```
distance <= proximityRadius            → visibility 1 (any angle)
distance > sightRange or angle > view/2 → 0
otherwise  Lerp(1, edgeDistanceFactor, distance / sightRange) × Lerp(1, peripheralAngleFactor, angle / (view/2))
fill/s = visibility / Entity.AwarenessFillTime × (sneaking ? sneakFillRateMultiplier : 1)
awareness += fill × dt while visible (and LOS clear), else −= awarenessDrainPerSecond × dt; clamp 0..1
```

Witness (non-hostile target, `NPCEntity.WitnessProfile`): `ComputeWitnessVisibility(sneaking, …)` =
`ComputeVisibility(…)` with sight range = profile `WitnessRange` while the target sneaks, **0 otherwise**;
proximity radius is always the sneaking one.

Sneaking multiplies sight range and proximity radius (`sneakSightRangeMultiplier`, `sneakProximityMultiplier`).
Distance and angle are flat (XZ). Line of sight is a single `Physics.Raycast` from the entity's eye
(`Entity.EyeHeight`) to `VisibilityPoint` against `lineOfSightMask` (Default), every `losCheckInterval`.

| Where | Values |
|-------|--------|
| `Entity` SO (per entity) | `ViewAngle` 110, `EyeHeight` 1.6 (spider 0.4), `ProximityRadius` 2, `AwarenessFillTime` 1 s, `LoseSightTime` 2 s, `SearchDuration` 6 s; sight range = `DetectionRange` |
| `StealthConfigSO` (global) | sneak ×0.6 sight / ×0.35 proximity / ×0.5 fill, edge 0.2, peripheral 0.5, drain 0.25/s, suspicion 0.5 (clamped > 0 — 0 makes Idle/Suspicious flip-flop), search start 0.5, damage alert 10 s, visibility heights 1.4 / 0.9, turn speeds 180 / 90; witness: turn 240, lose sight 1.5 s, warn cooldown 20 s, bubble 3 s / priority 0; theft chase: catch 1.8 m, lose sight 4 s, max distance 20 m, max 30 s, confront open timeout 0.5 s, alert bubble 2.5 s / priority 1 |
| `WitnessProfileSO` (`Game.NPC`, `Data/Entities/WitnessProfile_Humanoid`) | `WitnessRange` 6 (0 = off), `WatchRange` 9 (never below the range), `Barks` (`Barks_SneakWarning`); theft: `TheftSightRange` 10 (×0.6 sneaking = 6 m; 0 = ignores thefts), `TheftAlertBarks`, `TheftScoldBarks`, `ItemTheftScoldBarks`. Referenced by every `NPCEntity` (`_witnessProfile`); `Entity.WitnessProfile` is virtual, null for plain entities / monsters. Tune one NPC by swapping its profile. |

Tuning start points: standing in front at 8 m fills in ≈ 5 s, sneaking can't be seen past 4.8 m and fills at
half rate; sneaking behind an entity is only noticed inside 0.7 m.

---

## Debug views

- **Gizmos:** select an entity (or `Tools/Stealth/Show Detection Gizmos` for all, saved in EditorPrefs) — cone
  at standing and sneaking range, proximity circles, colour green → yellow (suspicion) → red (aware); in play
  mode the LOS line (green clear / red blocked), last seen sphere and a label (state, awareness, visibility,
  fill/s, distance, LOS, time since seen).
- **F3 overlay** (`Scripts/Debug/StealthDebugOverlay`, `Core.unity`, editor + development builds): same lines
  drawn with GL, a label per entity within 40 m and a summary panel. Use it for tuning.
- Entities with a witness profile also draw the witness cone (`WitnessRange`) in cyan
  (`StealthDebugGeometry.WitnessConeColor`), awareness-coloured while `IsWitnessing`; labels prefixed `Witness · `.
  Use `SightRange` / `SneakingSightRange` / `WitnessRange` from `EntityPerception` — never recompute
  `DetectionRange × multiplier` in debug code.
- New debug read-outs belong on `EntityPerception` (read-only properties updated in `Tick`), so the gizmos and
  the overlay stay in sync.
