# CLAUDE.md — Assets/_Game/Scripts/Core/Time

> Loaded when Claude accesses files in this folder. The world clock and visual day / night: `GameClock`, `GameTime`,
> `DayNightController`, `DayNightMath`. Namespace: `Game.Core`. Saving the time: `Scripts/Core/Save/CLAUDE.md`.

---

## What's here

| File | Role |
|------|------|
| `GameClock` | The single world clock (GO `GameClock` in `Core.unity`) — see GameClock below. |
| `GameTime` | **Static, pure.** Day / hour / minute derived from world seconds, `NextHour(now, hour)` (exact HH:00, target = now → +24 h), `CrossedHours` (per-frame hour events, capped at a day), `Format` ("Day 4, 06:00"), `Rate`. Tested by `GameTimeTests`. |
| `DayNightController` | Visual day / night, the only `RenderSettings` writer — see DayNightController below. |
| `DayNightMath` | **Static, pure.** `HourOfDay`, `Daylight` (0 night → 1, twilight ramps), `DayProgress` / `NightProgress` (night wraps midnight; in the day the moon rests where it set until mid-day), `ArcElevation` / `ArcAzimuth`, `IsValidDay`. Tested by `DayNightMathTests`. |

---

## GameClock

The single world clock (GO `GameClock` in `Core.unity`) — no `Instance` singleton (only `WorldStateManager` /
`SaveSystem` have one): Core readers hold a serialized ref (scene override on the Core `Player` instance) or listen to
its events; **region-scene** readers (can't reference Core) read `static GameClock.Active` (set in `Awake`, cleared in
`OnDestroy`, reset on play; null without Core — e.g. `NpcRoutine`). `Now` = absolute game seconds since day 1 00:00
(`double`), advanced by scaled `deltaTime` × `GameTime.Rate` (`TimeConfigSO.realMinutesPerGameDay`, asset: 180 → 1 h =
7.5 real min), so pausing stops it; frozen while `SaveSystem.IsLoading` (restore happens before the async reload).
Raises `OnHourChanged` (`GameEventSO_Int`, 0–23) per crossed hour; `bool SkipTo(target)` (false = not in the future,
nothing changes) raises `OnTimeSkipped` (`GameEventSO_Float`, seconds) then one `OnHourChanged`. `Restore(double?)`
(null / NaN / negative → `startHour`) raises nothing — hour listeners must re-read the time on `OnLoadFinished`.
`[ContextMenu] Skip 1 hour` for dev.

---

## DayNightController

Visual day / night (GO `DayNightController` in `Core.unity`, children `Sun` — soft shadows — and `Moon`). Reads
`GameClock.Now` **every frame** (no events → sleep skips / loads follow on the next frame) and drives sun / moon arcs
(pitch ≥ `minLightElevation`), intensities, trilight ambient, reflection intensity and the skybox `_Exposure` (runtime
material copies, one per source, kept for the session) from `DayNightConfigSO` (`Data/Config/DayNightConfig`: sunrise
6 / sunset 22 → 120 real min day, 60 night at 180 min/day; 1 h twilight fade). The sun is disabled at night, so URP
promotes the `Moon` to main light — it has soft shadows and the procedural skybox's disc follows it.
`[DefaultExecutionOrder(100)]` (after `GameClock`). **The only writer of `RenderSettings`**; re-applies them on
`SceneManager.activeSceneChanged` (RenderSettings belong to the active scene — in the editor that can be the region).
