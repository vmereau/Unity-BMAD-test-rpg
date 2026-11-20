# CLAUDE.md — Assets/_Game/Scripts/UI/Screens

> Screen management contract, full-screen tab panels, the Esc Game Menu (save / load / options / quit), death screen
> and loading overlay.
> The Skills tab panel lives in `Scripts/UI/Skills/`. `IScreenPanel` contract, cursor and input lifecycle: `Scripts/UI/CLAUDE.md`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `UIScreenManager` | Opens/closes full-screen tabs. Owns `InputSystem_Actions`; listens to `InventoryToggle` (I), `QuestLogToggle` (J), `CharacterStatsToggle` (P) and `SkillsToggle` (K) input actions. Reads `PlayerStateManager` (never writes it) to refuse `OpenTab` while `IsInDialogue` / `IsDead` / `IsIncapacitated` / `IsExecuting`; wires the tab buttons. |
| `IScreenPanel` | Interface every full-screen tab panel implements — contract in `Scripts/UI/CLAUDE.md` → IScreenPanel Contract. |
| `CharacterStatsUI` | Character stats screen. Shows level, XP, LP, HP, stamina, and all base stats. Implements `IScreenPanel`. |
| `OptionsUI` | Game Menu sub-panel (not a tab), implements `IScreenPanel` (the Game Menu owns the cursor). `MusicVolumeRow` / `SfxVolumeRow` sliders → `MusicVolumeSettings.Set` / `SfxVolumeSettings.Set` live; `OnScreenOpen` syncs the slider without notify, `OnScreenClose` flushes PlayerPrefs. Add new settings as rows that are siblings of `Title` under `OptionsUI`. |
| `GameMenuUI` | Esc menu: Resume / Save Game / Load Game / Options / Quit (confirm). Pauses (`timeScale = 0`) + unlocks cursor; Close restores both. Save button disabled with `SaveSystem.CanSave` reason. Hides (without touching timeScale / cursor) on `OnLoadStarted`. |
| `SaveSlotListUI` / `SaveSlotEntryUI` | Slot list sub-panel (`Prefabs/UI/SaveSlotList.prefab`, shared by the Game Menu and the start menu). Save mode: manual slots only, overwrite confirm. Load mode: all slots, valid rows clickable, load confirm. `Show(mode, onLoad)`: with `onLoad` (start menu) a confirmed load row calls it with the slot id instead of `SaveSystem.Load`. Delete on every existing row. Static `FormatSlotLabel` / `FormatDetails` / `FormatTimestamp` / `FormatPlaytime` (tested by `SaveSlotListFormatTests`). |
| `MainMenuUI` | Start menu on `MainMenuCanvas` in `Scenes/MainMenu.unity`: New Game / Continue (newest valid slot via `SaveFileStore.MostRecentValid`, detail line under it, disabled with "No saved game") / Load Game (shared `SaveSlotList` with `onLoad`) / Quit. New Game opens `OriginPickUI`; Begin calls `GameSession.SetOrigin(id)` then loads `Core` (Single); Continue / Load call `GameSession.RequestLoad(slot)` first. Esc: confirm → list → nothing. Sets `timeScale = 1` and unlocks the cursor on start. |
| `OriginPickUI` | New Game origin pick (`Window/OriginPick` in `MainMenu.unity`): one `ActionButton` per `OriginCatalogSO` origin (built in `Awake`, label auto-sized, selected one tinted), the origin's artwork (`Artwork` Image left of `Details`, `OriginSO.artwork`, hidden when null; images in `Art/UI/Origins/`), name + detail text (`FormatDetails`: description, bonuses, skill, belongings "Sent ahead with the Company baggage cart:" or "You arrive with nothing.", tested by `PlayerOriginTests`), Begin. `Show(onBegin)` selects the default origin; `MainMenuUI` hides it in `ShowMain` (Back / Esc). |
| `ConfirmDialogUI` | Shared Yes / No modal (`Prefabs/UI/ConfirmDialog.prefab`) (`Show(message, onYes, onNo)`, `Cancel`, `Hide`, `IsOpen`). `SaveSystem` ignores F9 while it's open. |
| `DeathScreenUI` | On `OnPlayerDied` waits 2 s (realtime), pauses, unlocks the cursor: Load last save (newest valid slot) / Load quicksave (if valid) / Restart (only with no valid save → `SaveSystem.RestartNewGame`) / Quit. Re-shows if a load finishes with the player still dead. |
| `SleepUI` | Bed sleep panel (`SleepScreen`, always active): hour picker + sleep fade — see SleepUI below |
| `LoadingOverlayUI` | Black "Loading…" panel on its own Canvas (sort 100), driven by `OnLoadStarted` / `OnLoadFinished`. |

---

## ScreenTab Enum

- `ScreenTab` is declared in `UIScreenManager.cs`; `_tabPanelRoots[]` and `_tabButtons[]` in `UIScreenManager` are
  indexed by it and must have the same length, ordered by `ScreenTab` value — a mismatch causes
  `IndexOutOfRangeException` at runtime.
- Adding a new tab requires: new enum value + new entry in both arrays (on `UICanvas.prefab`) + a `TabButton_*` in
  `TabBar.prefab`. Inserting before an existing value shifts it — reorder both arrays in the same change.

---

## UIScreenManager — Open/Close Flow

1. Input action fires (or tab button clicked).
2. If the requested tab is already active → close it (toggle).
3. Otherwise → close current tab, open new tab.
4. `OnScreenOpen()` / `OnScreenClose()` are called on the panel's `IScreenPanel` implementation.
5. No player state is set — `OpenTab` only reads `PlayerStateManager` (`IsInDialogue` / `IsDead` / `IsIncapacitated` / `IsExecuting` → refuse).

---

## CharacterStatsUI Notes

- Holds direct MonoBehaviour refs (`LevelSystem`, `XPSystem`, etc.) — intentional prototype shortcut; no dedicated event channel exposes all needed values yet.
- Refreshes all labels in `OnScreenOpen()` rather than subscribing to per-stat events.
- Subscribes to `GameEventSO_Int _onLevelUp`, `_onLPChanged`, and `GameEventSO_Float _onPlayerHealthChanged` in `OnEnable` for live updates while open.

---

## Input Handling

- `UIScreenManager` owns the `InputSystem_Actions` instance for menu toggles (lifecycle: `Scripts/UI/CLAUDE.md` → Input).
- `UI.Cancel` (Esc) routing in `UIScreenManager.HandleCancel`: open tab → close it; Game Menu open →
  `GameMenuUI.HandleBack()` (confirm → sub-panel → Resume); otherwise open the Game Menu **only if**
  `_wasGameplayLastFrame` (sampled in `LateUpdate`: cursor locked, not dead, not loading). The previous-frame sample is
  what stops the Esc that closes dialogue / container / trade (their own Cancel handlers re-lock the cursor in the same
  frame, in undefined order) from also opening the menu.
- Tab toggles and `OpenTab` are ignored while the Game Menu is open or the player is dead / in dialogue, and **whenever no
  tab is open but the cursor is already unlocked** — a world screen (container, cooking, sleep incl. fade, trade) owns it.
  This gate is generic: a new world screen needs no wiring as long as it unlocks the cursor while open.
- `OnLoadStarted` closes every tab.

---

## SleepUI

- `Open(clock, defaultHour, onConfirm(int))` (default = `TimeConfigSO.morningHour`).
- `−` / `+` buttons and horizontal `UI.Navigate` (arrows / A-D; stepped only when the direction changes from 0 — Navigate
  is a PassThrough Vector2 that performs on every value change) step the target with `SleepRules.StepHour`.
- The panel buttons have Navigation None and `SleepButton` is re-selected on open and after each step, so Enter / Space
  always confirm.
- `TargetText` "Sleep until HH:00 (N h)" ("under 1 h" for a sub-hour gap) and the "Day N, HH:MM" header refresh in
  `Update` only when the minute / target / hour count changes (the clock runs while open); Sleep → `onConfirm(targetHour)`.
- `PlaySleep(atBlack)` (unscaled fade out → callback → fade in → `Close`), `Cancel` (Esc, ignored mid-fade), closes on
  `OnPlayerDied` / `OnPlayerKnockedOut`. `IsOpen` covers the fade (save / load blocked).

---

## Pause Rule

Only the Game Menu and the death screen set `Time.timeScale = 0`; character tabs never pause. Every exit path restores
it (Resume / Esc, load finish / fail). Coroutines and fades used while paused must use realtime waits.

---

## Gotchas

- `UIScreenManager` does **not** implement `IScreenPanel` itself — it is the controller, not a panel.
