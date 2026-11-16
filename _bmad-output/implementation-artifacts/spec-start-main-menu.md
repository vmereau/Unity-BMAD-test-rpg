---
title: 'Start menu: New Game, Continue, Load Game, Quit'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: 'fbfd734f453540b49ad64d1802e9cb0db9aeeb91'
context:
  - '{project-root}/Assets/_Game/Scripts/UI/Screens/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/UI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game boots straight into `Core` + `StartingTown` and the arrival sequence. There's no start menu, so there's nowhere to continue a save from, and nowhere to put the planned origin pick (`docs/World/player-origins.md`).

**Approach:** A new `MainMenu` scene, first in the build: a plain dark screen titled "Echoes of the Fallen" with New Game, Continue, Load Game and Quit. New Game loads `Core` exactly as today. Continue and Load Game load `Core` with a pending save slot, so `Core` skips the default start and loads that save's region directly. Load Game reuses the Esc menu's slot list and confirm dialog, extracted into prefabs.

## Boundaries & Constraints

**Always:** Pressing Play with `Core` / `StartingTown` open in the editor still boots straight into the game (no pending request = today's flow). Continue never plays the arrival sequence or loads `StartingTown` first. The in-game Esc menu's Save / Load lists and confirm dialog behave exactly as before after the extraction, with every existing reference to them rewired and checked. Cursor through `CursorManager`, logs through `GameLog`, input through `InputSystem_Actions` (UI map). The menu sets `Time.timeScale = 1` on start.

**Ask First:** Any change to `SaveData` / the save format, or to `ArrivalSequence`.

**Never:** "Quit to main menu" from the Esc menu (needs a `Core` teardown, so it's deferred). Origin pick. Options in the start menu. Artwork or music on the menu. `CanvasScaler`.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| New Game | any | `Core` loads, then the arrival sequence as today; new-game snapshot built |
| Continue | ≥1 valid save | Newest valid slot (`SaveFileStore.MostRecentValid`) loads into its region; loading overlay from the first frame; no arrival |
| No saves | no valid slot | Continue disabled; its detail line reads "No saved game"; Load Game opens (rows non-clickable / Empty) |
| Load Game | click valid row → confirm "Load this save?" | Same path as Continue with that slot |
| Delete in list | confirm | File deleted; Continue state + detail refreshed on Back |
| File broken after menu | pending slot fails `TryRead` | `Core` falls back to New Game; toast with the read error |
| Esc in menu | confirm open / list open / main | Cancel confirm → back to main → nothing |
| Quit | — | Quits (stops Play Mode in editor), no confirm |

</frozen-after-approval>

## Code Map

- `Scripts/Core/SceneLoader.cs` -- `Start` auto-loads `_startupScene`; needs to skip it when a load is pending
- `Scripts/Core/Save/SaveSystem.cs` -- `Load` / `BeginLoad` / `RestartNewGame`; consumes the pending request
- `Scripts/Core/Save/SaveFileStore.cs` -- `MostRecentValid`, `ListSlots`, `TryRead`
- `Scripts/Core/GameConstants.cs` -- `MAIN_MENU_SCENE_NAME`, `CORE_SCENE_NAME` already exist
- `Scripts/UI/Screens/SaveSlotListUI.cs` -- Load mode calls `SaveSystem.Instance.Load` directly
- `Scripts/UI/Screens/GameMenuUI.cs` -- `Wire`, `QuitGame` helpers to reuse
- `Prefabs/UI/UICanvas.prefab` -- `GameMenu/Panel/Window/SaveSlotList` and root `ConfirmDialog` (inline today)
- `ProjectSettings/EditorBuildSettings.asset` -- Core (0), StartingTown (1)

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Core/GameSession.cs` -- new static: `RequestLoad(slotId)`, `HasPendingLoad`, `TryTakePendingLoad(out slotId)`; reset on `SubsystemRegistration` -- hand-off from the menu scene to `Core`
- [x] `Scripts/Core/SceneLoader.cs` -- read `GameSession.HasPendingLoad` in `Awake` (before any `Start`); `Start` skips the startup load when set; add public `LoadStartupRegion()` (same as today's startup path) -- fallback entry
- [x] `Scripts/Core/Save/SaveSystem.cs` -- `Start`: take the pending slot; `TryRead` → `BeginLoad`, else `Notify(error)` + `_sceneLoader.LoadStartupRegion()` -- Continue / fallback
- [x] `Scripts/UI/Screens/SaveSlotListUI.cs` -- `Show(Mode, Action<string> onLoad = null)`: with `onLoad`, Load confirm reads "Load this save?" and calls it instead of `SaveSystem` -- reuse in the menu
- [x] `Scripts/UI/Screens/MainMenuUI.cs` -- new: buttons New Game / Continue (+ detail text via `SaveSlotListUI.FormatSlotLabel` / `FormatDetails`) / Load Game / Quit, Back, Esc routing, `CursorManager.Unlock`, refresh Continue on show; New Game / load → `SceneManager.LoadScene(Core)` (after `GameSession.RequestLoad` for loads) -- the screen
- [x] `Prefabs/UI/SaveSlotList.prefab`, `Prefabs/UI/ConfirmDialog.prefab` -- extract from `UICanvas.prefab` as nested prefab instances (layout unchanged); rewire `GameMenuUI`, `SaveSlotListUI`, `DeathScreenUI`, `SaveSystem` (Core) and any other referrer; grep the old fileIDs to confirm none dangle -- shared panels
- [x] `Scenes/MainMenu.unity` -- camera (solid near-black), Screen Space Overlay canvas, `EventSystem` + `InputSystemUIInputModule`, title TMP "Echoes of the Fallen", `Common/ActionButton` instances, `SaveSlotList` + `ConfirmDialog` instances, `MainMenuUI` wired -- the scene
- [x] `ProjectSettings/EditorBuildSettings.asset` -- MainMenu at index 0, then Core, StartingTown -- build boots to the menu
- [x] `Assets/Tests/EditMode/GameSessionTests.cs` -- request / take / take-again-empty / reset -- hand-off contract
- [x] Docs -- `Scripts/UI/Screens/CLAUDE.md` (MainMenuUI, SaveSlotList `onLoad`), `Prefabs/UI/CLAUDE.md` (extracted prefabs), `Scenes/CLAUDE.md` (MainMenu scene + boot flow), `Scripts/Core/CLAUDE.md` (`GameSession`); `deferred-work.md`: "Quit to main menu" -- keep docs true

**Acceptance Criteria:**
- Given the build settings, when the game starts from `MainMenu`, then the title and four buttons show with an unlocked cursor.
- Given Play pressed in `Core`, when it boots, then it behaves exactly as before (arrival plays, no menu).
- Given the Esc menu in game, when opening Save Game / Load Game and confirming, then rows, confirms and loading work as before the extraction.
- Given Continue, when `Core` loads, then the console shows no `StartingTown` load before the save's region and no arrival log.

## Design Notes

`SceneLoader` must decide in `Awake`, not `Start`: `SaveSystem.Start` takes (clears) the request, and `Start` order between the two is undefined.

A Continue session has no new-game snapshot. That's fine: the death screen only offers Restart when no valid save exists, and Continue implies one does.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass, incl. `GameSessionTests`, `SaveSlotListFormatTests`
- MCP `read_console` after Play from `MainMenu` (New Game, then Continue) -- expected: no errors

**Manual checks:**
- Play from `MainMenu`: New Game → arrival; F5; stop; Play → Continue → save restored; Load Game lists slots; Quit stops Play Mode.
- Play from `Core`: unchanged boot. In game, Esc → Load Game and Save Game still work.

## Suggested Review Order

**Menu → Core hand-off**

- Entry point: a menu load requests the slot, then loads Core as a single scene.
  [`MainMenuUI.cs:107`](../../Assets/_Game/Scripts/UI/Screens/MainMenuUI.cs#L107)

- Static request, taken exactly once; reset on play.
  [`GameSession.cs:23`](../../Assets/_Game/Scripts/Core/GameSession.cs#L23)

- Decided in Awake so Start order with SaveSystem doesn't matter.
  [`SceneLoader.cs:39`](../../Assets/_Game/Scripts/Core/SceneLoader.cs#L39)

- Takes the request; unreadable file, unknown region or broken wiring fall back to New Game.
  [`SaveSystem.cs:103`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L103)

**Load robustness (review patches)**

- A region failure on the first load resets to a clean New Game instead of an empty world.
  [`SaveSystem.cs:441`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L441)

- Re-entrancy guard: F9 in Core's first frame can't start a second load.
  [`SaveSystem.cs:407`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L407)

**Menu screen**

- Continue = newest valid save, disabled with "No saved game".
  [`MainMenuUI.cs:87`](../../Assets/_Game/Scripts/UI/Screens/MainMenuUI.cs#L87)

- Shared slot list: start-menu loads go through a callback, not SaveSystem.
  [`SaveSlotListUI.cs:77`](../../Assets/_Game/Scripts/UI/Screens/SaveSlotListUI.cs#L77)

- Esc: confirm → list → nothing.
  [`MainMenuUI.cs:116`](../../Assets/_Game/Scripts/UI/Screens/MainMenuUI.cs#L116)

**Docs and tests**

- Boot flow and build order for future agents.
  [`Scenes/CLAUDE.md:7`](../../Assets/_Game/Scenes/CLAUDE.md#L7)

- Hand-off contract tests.
  [`GameSessionTests.cs:1`](../../Assets/Tests/EditMode/GameSessionTests.cs#L1)
