---
title: 'Visual day/night cycle (sun, moon, ambient) driven by the game clock'
type: 'feature'
created: '2026-10-26'
status: 'done'
baseline_commit: 'add92fe'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The world clock runs (48 real min per game day) but the scene never changes: StartingTown has a fixed directional light and the `DayNightController` GO in Core.unity is an empty stub.

**Approach:** Script `DayNightController` to drive a **Sun** (soft shadows) across the sky from the `GameClock` time, fading it out at sunset, with a dim cool **Moon** light, ambient and sky exposure blending to night. Sun up 06:00–22:00 at the unchanged clock speed = 32 real min of day, 16 of night. All values live in a new `DayNightConfigSO`.

## Boundaries & Constraints

**Always:** Clock speed and `TimeConfigSO` unchanged. The controller reads `GameClock.Now` each frame (continuous visual, no events needed), so sleep skips and save loads are followed automatically. Pure math in a static class with EditMode tests. Lights live in Core.unity (always loaded) — region scenes own no sun.

**Ask First:** Changing the clock rate or sleep hours; touching TestScene lighting; adding baked lighting / reflection probes.

**Never:** No `OnDayNightChanged` event or NPC schedule work (no listeners yet). No modification of built-in / shared skybox material assets (runtime instance only). No `RenderSettings` writes outside the controller.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Noon | 14:00 (sunrise 6, sunset 22) | Sun at max elevation, full intensity, moon off | N/A |
| Sunrise / sunset | 06:00 → 07:00, 21:00 → 22:00 | Sun low on the horizon, fades in / out over `twilightHours` | N/A |
| Night | 23:00–05:59 | Sun off, moon at `moonIntensity`, night ambient, dark sky | N/A |
| Sleep / load | Clock jumps to 06:00 | Lighting matches 06:00 next frame | N/A |
| Bad config | sunrise ≥ sunset or missing config / clock | Controller logs a warning, leaves lights as they are | Warn once, disable itself |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Core/Time/GameClock.cs` -- `Now` (absolute game seconds); not a singleton, readers hold a serialized ref
- `Assets/_Game/Scripts/Core/Time/GameTime.cs` -- pure time math (`SECONDS_PER_DAY/HOUR`), tested by `GameTimeTests`
- `Assets/_Game/ScriptableObjects/Config/TimeConfigSO.cs` -- config SO pattern to mirror
- `Assets/_Game/Scenes/Core.unity` -- `DayNightController` stub GO; active scene, so its `RenderSettings` (default procedural skybox) apply at runtime
- `Assets/_Game/Scenes/StartingTown.unity` -- fixed `Directional Light` (no shadows) to remove

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Core/Time/DayNightMath.cs` -- static pure: `HourOfDay(worldSeconds)` (float 0–24), `Daylight(hour, sunrise, sunset, twilight)` (0 night → 1 full day, linear ramps after sunrise / before sunset), `DayProgress` / `NightProgress` (0–1 along each arc, night wraps past midnight), `ArcElevation(progress, maxElevation)` (sine arc) -- testable core
- [x] `Assets/_Game/ScriptableObjects/Config/DayNightConfigSO.cs` + `Assets/_Game/Data/Config/DayNightConfig.asset` -- sunrise 6, sunset 22, twilight 1 h, sun max elevation / azimuth, sun intensity + color `Gradient` over the day, moon intensity (0.15) / color / max elevation, ambient sky/equator/ground day + night colors, sky exposure day / night -- no magic numbers
- [x] `Assets/_Game/Scripts/Core/Time/DayNightController.cs` -- refs `GameClock`, config, `Light _sun`, `Light _moon`; `Awake` validates (warn + `enabled = false`), sets `RenderSettings.sun = _sun`, ambient mode Trilight, instantiates the skybox material; `Update` places sun/moon on their arcs (east → west), sets intensity / color, enables each only when lit, lerps ambient + skybox `_Exposure` by daylight -- the cycle
- [x] `Assets/_Game/Scenes/Core.unity` -- `DayNightController` component wired; children `Sun` (Directional, soft shadows, realtime) and `Moon` (Directional, no shadows) -- scene setup via MCP `execute_code` (open scene, add, save)
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- delete `Directional Light` -- avoid a second, static sun
- [x] `Assets/Tests/EditMode/DayNightMathTests.cs` -- matrix rows: noon, sunrise / sunset ramps, night, midnight wrap, hour-of-day across days
- [x] `Assets/_Game/Scripts/Core/CLAUDE.md`, `Assets/_Game/Scenes/CLAUDE.md` -- document the controller, config and that region scenes must not add a sun

**Acceptance Criteria:**
- Given a running game, when a game day passes, then the sun rises in the east at 06:00, crosses the sky casting moving soft shadows, sets at 22:00 and the scene stays readable under moonlight until 06:00.
- Given the player sleeps "until morning", when the screen fades back, then the lighting is the 06:00 dawn look.

## Design Notes

URP picks `RenderSettings.sun` as the main light only while it is active; at night the moon becomes the main light, so the procedural skybox's disc follows the moon (reads as a moon) — the darkness comes from the skybox `_Exposure` lerp on a runtime material instance (destroyed in `OnDestroy`). Trilight ambient avoids per-frame `DynamicGI.UpdateEnvironment`.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass incl. `DayNightMathTests`
- Unity MCP `read_console` -- expected: no compile errors / new warnings

**Manual checks (if no CLI):**
- Play from Core: `GameClock` context menu "Skip 1 hour" through a full day — sun arc, sunset fade, moonlit night, dawn.

## Suggested Review Order

**Driving the lights**

- Entry point: per-frame read of the clock → sun / moon / ambient / sky / reflections.
  [`DayNightController.cs:89`](../../Assets/_Game/Scripts/Core/Time/DayNightController.cs#L89)

- Sun off at night → URP promotes the moon (soft shadows) to main light.
  [`DayNightController.cs:109`](../../Assets/_Game/Scripts/Core/Time/DayNightController.cs#L109)

- Arc → rotation, with a minimum pitch so horizon light isn't horizontal.
  [`DayNightController.cs:127`](../../Assets/_Game/Scripts/Core/Time/DayNightController.cs#L127)

- RenderSettings belong to the active scene: re-applied on change, skybox copied per source.
  [`DayNightController.cs:68`](../../Assets/_Game/Scripts/Core/Time/DayNightController.cs#L68)

- Runs after GameClock so a sleep skip never shows a stale frame.
  [`DayNightController.cs:16`](../../Assets/_Game/Scripts/Core/Time/DayNightController.cs#L16)

**Math**

- Daylight ramps over the twilight; night = 0.
  [`DayNightMath.cs:45`](../../Assets/_Game/Scripts/Core/Time/DayNightMath.cs#L45)

- Night arc wraps midnight; the moon rests where it set during the day.
  [`DayNightMath.cs:33`](../../Assets/_Game/Scripts/Core/Time/DayNightMath.cs#L33)

- NaN-safe clamp keeps a degenerate config out of light rotations.
  [`DayNightMath.cs:62`](../../Assets/_Game/Scripts/Core/Time/DayNightMath.cs#L62)

**Data & scenes**

- Sunrise 6 / sunset 22 = 32 / 16 real minutes at the unchanged clock speed.
  [`DayNightConfigSO.cs:11`](../../Assets/_Game/ScriptableObjects/Config/DayNightConfigSO.cs#L11)

- Controller + Sun / Moon children in Core; StartingTown's static light removed.
  [`Core.unity:166`](../../Assets/_Game/Scenes/Core.unity#L166)

**Tests**

- Noon, ramps, night, midnight wrap, degenerate input.
  [`DayNightMathTests.cs:26`](../../Assets/Tests/EditMode/DayNightMathTests.cs#L26)
