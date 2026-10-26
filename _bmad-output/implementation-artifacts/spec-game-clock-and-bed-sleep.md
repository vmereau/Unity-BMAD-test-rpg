---
title: 'Game clock + bed sleep (time skip)'
type: 'feature'
created: '2026-10-26'
status: 'done'
baseline_commit: 'a6141f3e2490ed9b5590d5272a372eec3badeb48'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game has no notion of in-game time. Future NPC routines and over-time effects (food, poison, magic) need one shared, saved clock, and the player needs a way to pass time.

**Approach:** Add a single authoritative `GameClock` in `Core.unity`. It holds the absolute world time (in seconds) and runs at 48 real minutes per game day. It is saved, and it raises hour-change and time-skip events. Beds let the player sleep until a fixed time. Sleeping fades the screen, jumps the clock forward and fully restores health. It is refused when a hostile is nearby.

## Boundaries & Constraints

**Always:**
- Time is one absolute `double` of world seconds. Day and hour are derived from it, never stored separately.
- The clock advances by scaled `Time.deltaTime`, so pause menus (`timeScale = 0`) stop it.
- Day 1 starts at `TimeConfigSO.startHour` (08:00).
- Sleep choices: "Until morning" (next 06:00), "Until evening" (next 18:00), "Until midnight" (next 00:00), "One hour", Cancel. A target hour equal to the current time means +24h.
- Any completed sleep fully heals the player.
- Sleep is refused, with a toast, when a hostile `FactionMember` is within `TimeConfigSO.sleepHostileRadius` (default 15 m) of the player, or when the player is dead.
- A time skip raises `OnTimeSkipped` once and `OnHourChanged` once, with the final hour. Normal flow raises `OnHourChanged` once per crossed hour.
- Saves made before this feature have no time field, and they load at the start time (no save version bump).

**Ask First:** Any change to `SaveData` beyond the one optional time field. Placing beds anywhere other than the one test bed in `StartingTown`.

**Never:** Day/night lighting, NPC routines, status effects, or a "wait anywhere" key (all deferred). Healing from waiting without a bed. Stamina or other restores on sleep. Simulating world time per frame during a skip.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sleep until morning | 22:30, day 3 | clock → day 4 06:00, HP = max, `OnTimeSkipped(7.5h)` | N/A |
| Target = now | 06:00, "Until morning" | +24h → next day 06:00 | N/A |
| One hour | 23:30 | day+1 00:30 | N/A |
| Hostile near | wolf at 8 m | no panel, toast "You can't sleep with enemies nearby" | N/A |
| Cancel / Esc | panel open | panel closes, time unchanged | N/A |
| Old save | no time field | clock = day 1, start hour | N/A |
| Missing clock | Core not loaded | bed refuses with a warning log | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Core/Save/SaveData.cs`, `SaveSystem.cs` -- save model. `BuildSaveData` captures state; `BeginLoad` restores facts before the region reload.
- `Assets/_Game/ScriptableObjects/Config/` -- existing config SOs (`InteractionConfigSO`, …). Add `TimeConfigSO` here.
- `Assets/_Game/Scripts/Crafting/CookingStation.cs` + `CookingSystem.cs` -- reference pattern: interactable raises an event, a player-side system gates it and opens the UI.
- `Assets/_Game/Scripts/AI/TargetRegistry.cs` -- `FindClosestHostile(faction, origin, range)` for the nearby-hostile gate. The player has a `FactionMember`.
- `Assets/_Game/Scripts/Player/PlayerHealth.cs` -- `Heal`, `MaxHealth`, `IsDead`.
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- toast channels (`GameEventSO_String`).
- `Assets/_Game/Scripts/UI/Screens/ConfirmDialogUI.cs`, `CookingUI.cs` -- `IScreenPanel` + input lifecycle reference for the sleep panel.
- `Assets/_Game/Scenes/Core.unity`, `StartingTown.unity`, `Prefabs/Player/Player.prefab`, `Prefabs/UI/UICanvas.prefab` -- hosts.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Core/Time/GameTime.cs` -- static, pure: `SECONDS_PER_DAY`, `DayOf`, `HourOf`, `MinuteOf`, `Format(seconds)` → "Day 4, 06:00", `NextHour(now, targetHour)` (exact HH:00, target = now → +24h), `CrossedHours(previous, now)` (hour events per frame, capped at a day), `Rate` -- testable time math.
- [x] `Assets/_Game/ScriptableObjects/Config/TimeConfigSO.cs` + `Data/Config/TimeConfig.asset` -- `realMinutesPerGameDay` (48), `startHour` (8), `morningHour` (6), `eveningHour` (18), `sleepHostileRadius` (15), `sleepFadeSeconds` (0.5) -- tunables.
- [x] `Assets/_Game/Scripts/Core/Time/GameClock.cs` -- MonoBehaviour on a `GameClock` GO in `Core.unity` (not a singleton — project-context allows only `WorldStateManager` / `SaveSystem`; `SaveSystem` and `SleepSystem` hold serialized refs): `Now`, `Day`, `Hour`, `Minute`. `Update` advances time (not while `SaveSystem.IsLoading`) and raises `OnHourChanged` (`GameEventSO_Int`) per crossed hour. `bool SkipTo(target)` raises `OnTimeSkipped` (`GameEventSO_Float`, seconds) and the final hour; false for a non-future target. `Capture()` / `Restore(double?)` restores without events (NaN / negative → start time). `[ContextMenu]` "Skip 1 hour" for dev use -- the single source of time.
- [x] `SaveData.cs` / `SaveSystem.cs` -- optional `double? worldTimeSeconds`. Capture it in `BuildSaveData`, restore it in `BeginLoad` next to the facts -- persistence.
- [x] `Assets/_Game/Scripts/World/Bed.cs` -- `IInteractable` (prompt "Sleep", name tag "Bed") that raises `OnBedSleepRequested` (`GameEventSO_Void`) -- same pattern as `CookingStation`.
- [x] `Assets/_Game/Scripts/World/SleepSystem.cs` (on `Player.prefab`) -- on request: warn and ignore if not wired (missing clock / UI / config), then gate on dead and nearby hostile (toast via `OnSleepRefused` for both). Otherwise open `SleepUI`; the click is re-gated, then fade out, `GameClock.SkipTo(wake)`, heal to max only if the skip happened, fade in. A pure `SleepRules.WakeTime(choice, now, morningHour, eveningHour)` holds the choice-to-wake-time mapping -- player-side gate, mirroring `CookingSystem`.
- [x] `Assets/_Game/Scripts/UI/Screens/SleepUI.cs` + `UICanvas.prefab` panel -- `IScreenPanel` with header "Day N, HH:MM", 4 choice buttons + Cancel, Esc closes, closes on `OnPlayerDied`, full-screen fade `CanvasGroup`. Input lifecycle per `Scripts/UI/CLAUDE.md` -- choice UI.
- [x] `NotificationToastUI.cs` -- add an `_onSleepRefused` channel shown verbatim.
- [x] `Data/Events/` -- `OnHourChanged`, `OnTimeSkipped`, `OnBedSleepRequested`, `OnSleepRefused` assets (via MCP).
- [x] `Prefabs/World/Beds/Bed_Base.prefab` (`Bed` + a placeholder `Model` child + `InteractionHighlight`, Interactable layer) + one instance near the player spawn in `StartingTown.unity` -- testable in game.
- [x] `SaveSystem.CanSave` / `CanLoad` -- refuse while `SleepUI.IsOpen` or a sleep fade is running.
- [x] `Assets/Tests/EditMode/GameTimeTests.cs`, `SleepRulesTests.cs` -- cover the I/O matrix time cases (wrap past midnight, target = now, one hour across day boundary, formatting).
- [x] `Scripts/Core/CLAUDE.md`, `Scripts/World/CLAUDE.md`, `Scenes/CLAUDE.md` -- document `GameClock`, `Bed` / `SleepSystem`, the `Core.unity` GO.
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- add: status effects (food always over time; regen potions and magic undecided; NPC effects undecided), NPC routines on `OnHourChanged`, day/night lighting.

**Acceptance Criteria:**
- Given the game runs for 2 real minutes unpaused, when the clock is read, then exactly 1 game hour has passed.
- Given the player slept to 06:00 and saved, when the save is loaded, then the clock reads the saved day and 06:00, and no hour/skip events fire during restore.
- Given the sleep panel is open, when the player presses F5, then the save is refused with a reason toast.

## Design Notes

Absolute timestamps are what keep the later systems simple. Effects will store `expiresAt` and routines will evaluate `schedule(timeOfDay)`, so a skip or a load needs no simulation: listeners re-evaluate on `OnTimeSkipped` / `OnHourChanged`. `GameClock` stays a dumb counter, and every rule lives in the pure `GameTime` / `SleepRules`.

## Verification

**Commands:**
- MCP `read_console` -- expected: no compile errors.
- MCP `run_tests` (EditMode) -- expected: all pass, including the new `GameTimeTests` and `SleepRulesTests`.

**Manual checks:**
- Play `StartingTown`: use the bed, choose "Until morning". Check the fade, that HP is full, and that the panel header shows the new time on reopen. Then try with an enemy nearby and check for the refusal toast.

## Suggested Review Order

**World clock**

- Entry point: one absolute double of game seconds; day and hour are always derived from it.
  [`GameClock.cs:23`](../../Assets/_Game/Scripts/Core/Time/GameClock.cs#L23)

- Scaled delta time, so pauses stop it; frozen during async loads; one event per crossed hour.
  [`GameClock.cs:41`](../../Assets/_Game/Scripts/Core/Time/GameClock.cs#L41)

- A skip jumps exactly to the target: one skip event plus the final hour.
  [`GameClock.cs:57`](../../Assets/_Game/Scripts/Core/Time/GameClock.cs#L57)

- Save restore raises nothing; null, NaN and negative values fall back to the start hour.
  [`GameClock.cs:73`](../../Assets/_Game/Scripts/Core/Time/GameClock.cs#L73)

**Pure time math**

- The next HH:00 is computed from the day start, so the wake time reads exactly HH:00.
  [`GameTime.cs:52`](../../Assets/_Game/Scripts/Core/Time/GameTime.cs#L52)

- Per-frame hour crossings, capped at one day for big hitches.
  [`GameTime.cs:31`](../../Assets/_Game/Scripts/Core/Time/GameTime.cs#L31)

- Sleep choice to wake time; morning and evening hours come from the config.
  [`SleepRules.cs:19`](../../Assets/_Game/Scripts/World/SleepRules.cs#L19)

**Bed and sleep flow**

- The bed only raises a void event, following the CookingStation pattern.
  [`Bed.cs:28`](../../Assets/_Game/Scripts/World/Bed.cs#L28)

- Wiring check, then the dead and nearby-hostile gate, then the panel opens.
  [`SleepSystem.cs:56`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L56)

- The click is re-gated, because an enemy may have arrived while the panel was open.
  [`SleepSystem.cs:68`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L68)

- At the black of the fade: skip, then a full heal only if the skip happened.
  [`SleepSystem.cs:78`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L78)

- Hostile check through TargetRegistry within the config radius; it raises the refusal toast.
  [`SleepSystem.cs:89`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L89)

**Sleep panel and fade**

- Unscaled fade; the callback is guarded so a throw can't leave the screen black.
  [`SleepUI.cs:148`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L148)

- A _sleeping flag (not the coroutine handle) survives a zero-length fade.
  [`SleepUI.cs:125`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L125)

- Close resets everything; OnDisable closes so the screen is never stuck half-open.
  [`SleepUI.cs:110`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L110)

**Save integration**

- Optional field: older saves load as null, so no version bump is needed.
  [`SaveData.cs:104`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L104)

- Captured with facts and recipe crafts.
  [`SaveSystem.cs:202`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L202)

- Restored before the region reload (the clock is frozen while loading).
  [`SaveSystem.cs:378`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L378)

- Save and load are refused while the sleep panel or fade is up.
  [`SaveSystem.cs:151`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L151)

**Peripherals**

- Tunables: day length, start, morning and evening hours, hostile radius, fade.
  [`TimeConfigSO.cs:6`](../../Assets/_Game/ScriptableObjects/Config/TimeConfigSO.cs#L6)

- Refusal toast channel.
  [`NotificationToastUI.cs:106`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L106)

- Wrap past midnight and hour-crossing tests.
  [`GameTimeTests.cs:53`](../../Assets/Tests/EditMode/GameTimeTests.cs#L53)

- Sleep choice tests.
  [`SleepRulesTests.cs:21`](../../Assets/Tests/EditMode/SleepRulesTests.cs#L21)

- Old-save compatibility test.
  [`SaveDataSerializationTests.cs:74`](../../Assets/Tests/EditMode/SaveDataSerializationTests.cs#L74)
