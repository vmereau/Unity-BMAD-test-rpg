---
title: 'HUD clock dial and sleep-until-hour picker'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '5ae817bc'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Prefabs/UI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/HUD/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing on the HUD shows the time of day, and a bed only offers four fixed sleeps (morning, evening, midnight, one hour). The upcoming tithe-barn quest (`docs/World/Quests/TitheBarnShortfall.md`, P1 + P2) needs the player to know when it is past midnight and to wake at a chosen hour.

**Approach:** Add a small sun/moon dial with an `HH:MM` label to the HUD, top-right. It reads `GameClock` and places the sun along the day arc and the moon along the night arc with the same `DayNightMath` progress as the lighting. Replace the bed panel's four duration buttons with an hour picker: step the target hour, see "Sleep until HH:00 (N h)", then confirm. Sleep stays bed-only.

## Boundaries & Constraints

**Always:** The dial uses the same sunrise and sunset hours as the lighting (`DayNightConfigSO`), so the sun icon sets when the scene does. The HUD clock lives under `UICanvas/Game`, so it hides with the HUD (`HudVisibilityUI`) and keeps running while hidden. It refreshes only when the shown minute or icon changes. Sleep keeps its gates (dead / hostiles nearby, re-checked on confirm), the fade, the full heal and the save/load block. Wake time stays pure and unit-tested in `SleepRules`. A target equal to the current hour means the same hour tomorrow (a full day).

**Ask First:** Waiting anywhere (without a bed), and any change to sleep healing or the hostile gate.

**Never:** No new art pipeline in this spec: use placeholder graphics (built-in sprites, tinted) and record final dial art as deferred. No minute-level wake targets, no real-time display, no CanvasScaler.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Sleep to later hour today | 20:15, target 23 | Wake Day same, 23:00; label "Sleep until 23:00 (3 h)" | N/A |
| Sleep past midnight | 22:30, target 1 | Wake next day 01:00; label rounds to "(3 h)" | N/A |
| Target = current hour | 06:40, target 6 | Wake next day 06:00 (full day) | N/A |
| Picker wraps | target 23, step + | target 0; step − from 0 → 23 | N/A |
| Picker opens | bed used at any time | default target = `TimeConfigSO.morningHour` | N/A |
| Dial at night | 00:30, sunrise 6, sunset 22 | moon icon shown, mid-left of arc (`NightProgress` ≈ 0.31) | N/A |
| No clock | `GameClock.Active` null (region played alone) | dial hidden | one warning, no exceptions |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/SleepRules.cs` -- pure wake-time math; `SleepChoice` enum (4 fixed choices) to replace by a target hour
- `Assets/_Game/Scripts/World/SleepSystem.cs` -- gates, opens `SleepUI`, applies `SkipTo` + heal; takes the choice callback
- `Assets/_Game/Scripts/UI/Screens/SleepUI.cs` -- bed panel (4 duration buttons, Cancel, fade); `Open(header, onChoice)`
- `Assets/_Game/Scripts/Core/Time/GameTime.cs` -- `NextHour`, `Format`, `HourOf` / `MinuteOf`
- `Assets/_Game/Scripts/Core/Time/DayNightMath.cs` -- `HourOfDay`, `IsDay`, `DayProgress`, `NightProgress` (reuse for the dial)
- `Assets/_Game/Scripts/Core/Time/GameClock.cs` -- `GameClock.Active.Now` (static read path; UICanvas is in Player.prefab)
- `Assets/_Game/Data/Config/DayNightConfig.asset`, `TimeConfig.asset` -- sunrise/sunset, morning hour
- `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- `Game` (HUD) and `SleepScreen/Panel/Window/Buttons`
- `Assets/Tests/EditMode/SleepRulesTests.cs` -- existing wake-time tests to migrate

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/World/SleepRules.cs` -- replace `SleepChoice` with `WakeTime(int targetHour, double now)` (= `GameTime.NextHour`), plus `HoursUntil(int targetHour, double now)` (rounded whole hours for the label) and `StepHour(int hour, int delta)` (wraps 0–23) -- pure, testable picker math
- [x] `Assets/_Game/Scripts/World/SleepSystem.cs` -- callback takes the target hour; `ApplySleep(int)` uses the new `WakeTime`; pass `morningHour` as the default target -- keep gates, heal, logs
- [x] `Assets/_Game/Scripts/UI/Screens/SleepUI.cs` -- `Open(header, defaultHour, onConfirm(int))`; serialized `−` / `+` buttons, `_targetText` ("Sleep until HH:00 (N h)", refreshed on step and on open), `Sleep` confirm button, Cancel; arrow keys / A-D step via `UI.Navigate` while open and not fading; remove the 4 duration buttons -- the hour picker
- [x] `Assets/_Game/Scripts/UI/HUD/HudClockUI.cs` -- new: polls `GameClock.Active` in `LateUpdate`; computes hour-of-day, `IsDay`, progress from `DayNightConfigSO`; places `_sunIcon` / `_moonIcon` on a half-circle of `_radius` (progress 0 = left horizon, 1 = right), shows one, sets `_timeText` "HH:MM"; updates only when the minute or the day/night state changes; hides the root group when no clock -- the dial
- [x] `Assets/_Game/Scripts/UI/HUD/HudClockMath.cs` -- new pure static: `DialPosition(progress, radius)` → `Vector2` along the upper half-circle -- testable layout
- [x] `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- add `Game/Clock` (top-right, fixed pixels: arc background, sun + moon `Image`s using a built-in round sprite tinted warm / pale, `TimeText` TMP) with `HudClockUI`; rebuild `SleepScreen` buttons: `TimeText` header, `Minus` / `TargetText` / `Plus` row, `Sleep`, `Cancel`; wire `SleepUI` fields -- layout
- [x] `Assets/Tests/EditMode/SleepRulesTests.cs` -- migrate to target hours and cover the I/O matrix (later today, past midnight, same hour → full day, wrap both ways, hours label) -- regression
- [x] `Assets/Tests/EditMode/HudClockMathTests.cs` -- new: progress 0 / 0.5 / 1 → left / top / right -- regression
- [x] `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`, `Assets/_Game/Scripts/UI/Screens/CLAUDE.md`, `Assets/_Game/Prefabs/UI/CLAUDE.md` -- document `HudClockUI` and the new SleepScreen layout -- folder docs
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- add: final dial art (`ui:create`); mark TitheBarnShortfall P1 / P2 done -- backlog

**Acceptance Criteria:**
- Given play mode at any hour, when the HUD is visible, then the top-right dial shows the current `HH:MM` and the sun (day) or moon (night) at the arc position the lighting implies, updating as the clock runs and after a sleep skip.
- Given a world screen is open, when the HUD hides, then the clock hides with it and is correct again when it returns.
- Given the bed panel is open, when the player steps the target to 0 and confirms, then the screen fades, the clock reads `00:00` on the right day, and the player is fully healed.
- Given a hostile walks up while the panel is open, when the player confirms, then sleep is refused with the existing toast and no time passes.

## Design Notes

Dial placement: `progress` in [0, 1] maps to the angle π → 0 along an upper half-circle: `(cos(π(1−p))·r, sin(π·p)·r)`. During the day, `DayProgress`. At night, `NightProgress`, which wraps past midnight, so at 00:30 with a 6/22 day the moon sits a third of the way across. The label's hour count is `ceil` of the real gap, so 22:30 → 01:00 reads "3 h".

Implementation note (review): `SleepUI.Open` takes `(GameClock clock, int defaultHour, Action<int> onConfirm)` instead of a header string, so the "Day N, HH:MM" header and the "(N h)" count stay current while the panel is open (the clock runs). `SleepSystem` fixes the wake time at the click, not at the black of the fade.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode, filter `SleepRulesTests|HudClockMathTests`) -- expected: all pass
- Unity MCP `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play from Core: dial visible top-right; use the player's bed, pick 00:00, confirm; dial shows the moon and `00:00` after the fade.

## Suggested Review Order

**Sleep hour picker**

- Entry point: the wake time is fixed at the click, before the fade lets the clock run on.
  [`SleepSystem.cs:77`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L77)

- A skip that the clock has already passed still counts as rest.
  [`SleepSystem.cs:81`](../../Assets/_Game/Scripts/World/SleepSystem.cs#L81)

- Pure picker math: the next HH:00, the same hour means tomorrow, and wrapping.
  [`SleepRules.cs:16`](../../Assets/_Game/Scripts/World/SleepRules.cs#L16)

- The label count rounds up and shows "under 1 h" for a sub-hour gap.
  [`SleepRules.cs:22`](../../Assets/_Game/Scripts/World/SleepRules.cs#L22)

- The picker opens on the morning hour, with the clock feeding the header and count.
  [`SleepUI.cs:132`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L132)

- Navigate steps once per direction change, which tames diagonals and stick drift.
  [`SleepUI.cs:108`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L108)

- Re-selecting Sleep keeps Enter / Space confirming, never a stale − / +.
  [`SleepUI.cs:240`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L240)

- Labels rebuild only when the minute, target or hour count changes.
  [`SleepUI.cs:246`](../../Assets/_Game/Scripts/UI/Screens/SleepUI.cs#L246)

**HUD clock**

- Polls the clock, repaints per minute, and uses the lighting's progress for the sun and moon.
  [`HudClockUI.cs:45`](../../Assets/_Game/Scripts/UI/HUD/HudClockUI.cs#L45)

- A tri-state forces the first show or hide, and warns once without a clock.
  [`HudClockUI.cs:85`](../../Assets/_Game/Scripts/UI/HUD/HudClockUI.cs#L85)

- Half-circle placement: progress 0 at the left horizon, 1 at the right.
  [`HudClockMath.cs:12`](../../Assets/_Game/Scripts/UI/HUD/HudClockMath.cs#L12)

**Peripherals**

- `eveningHour` removed; `morningHour` is now the picker's default.
  [`TimeConfigSO.cs:16`](../../Assets/_Game/ScriptableObjects/Config/TimeConfigSO.cs#L16)

- The I/O matrix as tests: wake times, label rounding, wrap.
  [`SleepRulesTests.cs:1`](../../Assets/Tests/EditMode/SleepRulesTests.cs#L1)

- Dial positions at 0 / 0.5 / 1 / clamped.
  [`HudClockMathTests.cs:1`](../../Assets/Tests/EditMode/HudClockMathTests.cs#L1)
