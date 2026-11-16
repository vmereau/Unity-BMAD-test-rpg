---
title: 'Editor play scenarios (main menu / arrival / after arrival)'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '737630f'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** What Play does in the editor depends on which scenes are open, and starting in or after the arrival means editing `ArrivalSequence._devStart` in StartingTown. Switching between the menu, the arrival and the post-arrival game takes manual scene juggling every time.

**Approach:** An editor-only "play scenario" picked from a dropdown on the main toolbar (next to Play), with a `Tools/Play Scenario/...` menu mirror. The choice is sticky per machine (EditorPrefs) and changes what the normal Play button does. It sets `EditorSceneManager.playModeStartScene` and, for the arrival scenarios, overrides the arrival's dev start and the origin (Wretch).

## Boundaries & Constraints

**Always:** Four scenarios: **Open Scenes** (default, today's behaviour: no start scene, the scene's `_devStart` applies), **Main Menu** (start `MainMenu.unity`; the arrival runs normally after New Game), **Arrival (Wretch)** (start `Core.unity`, origin Wretch, arrival forced to play), **After Arrival (Wretch)** (start `Core.unity`, origin Wretch, arrival forced to `SkipAll`). The override only exists in the editor: builds behave exactly as today. The start scene is reapplied on editor load (domain reload / restart), since `playModeStartScene` isn't persisted.

**Ask First:** Changing the serialized `_devStart` in StartingTown, or any Core / MainMenu scene content.

**Never:** No `UnityEditor` reference outside `#if UNITY_EDITOR` in the `Game` assembly. No change to the real boot flow (`SceneLoader`, `SaveSystem`, `MainMenuUI`). No "Hut Escort" scenario.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Main Menu | Any scenes open, Play | MainMenu loads; New Game → arrival plays (scene `_devStart` ignored) | N/A |
| Arrival | Any scenes open, Play | Core boots, StartingTown loads, origin Wretch, arrival starts from the gate even if the scene's `_devStart` is SkipAll | N/A |
| After Arrival | Any scenes open, Play | Core boots, origin Wretch, `DevSkip` runs: player outside the hut, arrival done | Hut not wired → existing SkipAll behaviour |
| Open Scenes | Play | Exactly today: open scenes play, scene `_devStart` applies | N/A |
| Missing scene | MainMenu / Core asset not found | Warning logged, start scene cleared (falls back to open scenes) | `GameLog.Warn` / `Debug.LogWarning` |
| Switch during Play | Dropdown / menu used while playing | Entries disabled while playing | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/Story/ArrivalSequence.cs` -- `TryStart` reads `_devStart` (None / SkipAll / SkipToHutEscort) gated by `DevSkipAllowed`; effective value must take the override into account
- `Assets/_Game/Scripts/Core/GameSession.cs` -- `SetOrigin(id)`; statics reset at `SubsystemRegistration`; origin consumed by `SaveSystem.HandleRegionLoaded` on the startup region
- `Assets/_Game/Data/Origins/Origin_Wretch.asset` -- `_originId: Wretch`
- `Assets/_Game/Scenes/MainMenu.unity`, `Core.unity` -- start scenes
- `Assets/_Game/Editor/` -- `Game.Editor` asmdef (refs `Game`), home of the toolbar
- Verified API (6000.6): `[MainToolbarElement(string path)]` (props `defaultDockPosition`, `defaultDockIndex`), `MainToolbarDropdown(MainToolbarContent, Action<Rect>)`, `MainToolbar.Refresh(path)`

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Core/PlayScenario.cs` -- enum `PlayScenario { OpenScenes, MainMenu, Arrival, AfterArrival }`, enum `ArrivalStartOverride { None, Play, SkipAll }`, static `PlayScenarios` with the EditorPrefs key (per checkout: includes `Application.dataPath`), `Saved` (EditorPrefs under `#if UNITY_EDITOR`, `OpenScenes` in builds), `Active` (snapshot taken once per Play; `OpenScenes` outside play or when the start scene couldn't be applied), pure `ArrivalOverrideFor(scenario)` and `OriginFor(scenario)` (Wretch for the two arrival scenarios, null otherwise), plus a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` that calls `GameSession.SetOrigin` (runs after the `SubsystemRegistration` reset) -- single runtime hand-off
- [x] `Assets/_Game/Scripts/World/Story/ArrivalSequence.cs` -- in `TryStart`, resolve the effective dev start: override `Play` → None, `SkipAll` → SkipAll, `None` → the serialized `_devStart`; still behind `DevSkipAllowed` -- scenario wins over the scene
- [x] `Assets/_Game/Editor/PlayScenarios/PlayScenarioToolbar.cs` -- `[InitializeOnLoad]` applies the start scene from the saved scenario; toolbar dropdown (current scenario label, `GenericMenu` with checkmarks, disabled while playing); `Tools/Play Scenario/<name>` menu items with validate (checked + disabled in play); on change save EditorPrefs, apply `playModeStartScene` (asset path → `SceneAsset`, missing → warning + null), `MainToolbar.Refresh` -- the picker
- [x] `Assets/Tests/EditMode/PlayScenarioTests.cs` -- `ArrivalOverrideFor` / `OriginFor` for all four scenarios -- covers the mapping
- [x] `Assets/_Game/Scenes/CLAUDE.md`, `Assets/_Game/Scripts/World/CLAUDE.md` -- document the play scenarios (boot flow section, ArrivalSequence dev line)

**Acceptance Criteria:**
- Given the Arrival scenario and StartingTown alone open with `_devStart = SkipAll`, when Play is pressed, then Core boots and the arrival plays from the gate as a Wretch.
- Given any scenario, when the editor restarts, then the dropdown shows the same scenario and Play uses it.
- Given a player build, when it runs, then no scenario override applies.

## Design Notes

The editor → play hand-off goes through EditorPrefs read at runtime under `#if UNITY_EDITOR` rather than a static set from the editor, because `GameSession.Reset` (SubsystemRegistration) and domain reloads would wipe a static. `PlayScenarios.Active` is a per-Play snapshot and is `OpenScenes` outside Play, so EditMode tests can call the pure mappings without touching prefs.

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`, EditMode) -- expected: all pass, incl. `PlayScenarioTests`
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Dropdown is visible on the main toolbar; each scenario starts where the matrix says

## Suggested Review Order

**Picking a scenario (editor)**

- Entry point: the selection saves the pref, sets the start scene and refreshes the toolbar
  [`PlayScenarioToolbar.cs:74`](../../Assets/_Game/Editor/PlayScenarios/PlayScenarioToolbar.cs#L74)

- Start scene per scenario; Open Scenes only clears a start scene we set
  [`PlayScenarioToolbar.cs:82`](../../Assets/_Game/Editor/PlayScenarios/PlayScenarioToolbar.cs#L82)

- Reapplied on every editor load, because `playModeStartScene` isn't persisted
  [`PlayScenarioToolbar.cs:32`](../../Assets/_Game/Editor/PlayScenarios/PlayScenarioToolbar.cs#L32)

- Toolbar dropdown next to Play, disabled while playing
  [`PlayScenarioToolbar.cs:51`](../../Assets/_Game/Editor/PlayScenarios/PlayScenarioToolbar.cs#L51)

**Hand-off into the running game**

- Per-Play snapshot taken at BeforeSceneLoad (after GameSession reset); forces the Wretch origin
  [`PlayScenario.cs:94`](../../Assets/_Game/Scripts/Core/PlayScenario.cs#L94)

- `Active` is OpenScenes outside Play and in builds
  [`PlayScenario.cs:71`](../../Assets/_Game/Scripts/Core/PlayScenario.cs#L71)

- Per-checkout EditorPrefs key
  [`PlayScenario.cs:49`](../../Assets/_Game/Scripts/Core/PlayScenario.cs#L49)

**Arrival override**

- Scenario wins over the serialized `_devStart`, logging when it does
  [`ArrivalSequence.cs:575`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L575)

- Single call site in `TryStart`
  [`ArrivalSequence.cs:195`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L195)

**Peripherals**

- Pure mapping tests for all four scenarios
  [`PlayScenarioTests.cs:8`](../../Assets/Tests/EditMode/PlayScenarioTests.cs#L8)
