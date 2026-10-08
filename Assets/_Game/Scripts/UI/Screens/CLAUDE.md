# CLAUDE.md — Assets/_Game/Scripts/UI/Screens

> Screen management contract, full-screen tab panels, the Esc Game Menu (save / load / options / quit), death screen
> and loading overlay.
> The Skills tab panel lives in `Scripts/UI/Skills/`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `UIScreenManager` | Opens/closes full-screen tabs. Owns `InputSystem_Actions`; listens to `InventoryToggle` (I), `QuestLogToggle` (J), `CharacterStatsToggle` (C) and `SkillsToggle` (K) input actions. Manages `PlayerStateManager` state transitions and tab-button wiring. |
| `IScreenPanel` | Interface contract: `OnScreenOpen()` and `OnScreenClose()`. All full-screen panels must implement this. |
| `CharacterStatsUI` | Character stats screen. Shows level, XP, LP, HP, stamina, and all base stats. Implements `IScreenPanel`. |
| `OptionsUI` | Options placeholder, now a Game Menu sub-panel (not a tab). Implements `IScreenPanel`; logs open/close only. |
| `GameMenuUI` | Esc menu: Resume / Save Game / Load Game / Options / Quit (confirm). Pauses (`timeScale = 0`) + unlocks cursor; Close restores both. Save button disabled with `SaveSystem.CanSave` reason. Hides (without touching timeScale / cursor) on `OnLoadStarted`. |
| `SaveSlotListUI` / `SaveSlotEntryUI` | Slot list sub-panel. Save mode: manual slots only, overwrite confirm. Load mode: all slots, valid rows clickable, load confirm. Delete on every existing row. Static `FormatSlotLabel` / `FormatDetails` / `FormatTimestamp` / `FormatPlaytime` (tested by `SaveSlotListFormatTests`). |
| `ConfirmDialogUI` | Shared Yes / No modal (`Show(message, onYes, onNo)`, `Cancel`, `Hide`, `IsOpen`). `SaveSystem` ignores F9 while it's open. |
| `DeathScreenUI` | On `OnPlayerDied` waits 2 s (realtime), pauses, unlocks the cursor: Load last save (newest valid slot) / Load quicksave (if valid) / Restart (only with no valid save → `SaveSystem.RestartNewGame`) / Quit. Re-shows if a load finishes with the player still dead. |
| `LoadingOverlayUI` | Black "Loading…" panel on its own Canvas (sort 100), driven by `OnLoadStarted` / `OnLoadFinished`. |

---

## ScreenTab Enum

```csharp
public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Skills = 3 }
```

- `_tabPanelRoots[]` and `_tabButtons[]` in `UIScreenManager` are indexed by this enum.
- Adding a new tab requires: new enum value + new entry in both arrays (on `UICanvas.prefab`) + a `TabButton_*` in
  `TabBar.prefab`. Inserting before an existing value shifts it — reorder both arrays in the same change.

---

## UIScreenManager — Open/Close Flow

1. Input action fires (or tab button clicked).
2. If the requested tab is already active → close it (toggle).
3. Otherwise → close current tab, open new tab.
4. `OnScreenOpen()` / `OnScreenClose()` are called on the panel's `IScreenPanel` implementation.
5. `PlayerStateManager` is set to `InMenu` when any tab is open, restored to `Idle` on close.

---

## IScreenPanel Contract

Every panel in the tab bar must implement `IScreenPanel`:

```csharp
public interface IScreenPanel
{
    void OnScreenOpen();   // Called by UIScreenManager when this tab becomes active
    void OnScreenClose();  // Called by UIScreenManager when this tab is dismissed
}
```

- `OnScreenOpen()` is responsible for calling `CursorManager.Unlock()`.
- `OnScreenClose()` is responsible for calling `CursorManager.Lock()`.
- Do **not** call `SetActive` inside the panel — `UIScreenManager` handles panel root activation.

---

## CharacterStatsUI Notes

- Holds direct MonoBehaviour refs (`LevelSystem`, `XPSystem`, etc.) — intentional prototype shortcut; no dedicated event channel exposes all needed values yet.
- Refreshes all labels in `OnScreenOpen()` rather than subscribing to per-stat events.
- Subscribes to `GameEventSO_Int _onLevelUp`, `_onLPChanged`, and `GameEventSO_Float _onPlayerHealthChanged` in `OnEnable` for live updates while open.

---

## Input Handling

- `UIScreenManager` owns the `InputSystem_Actions` instance for menu toggles.
- `_input` is initialized in `Awake` — the `OnDisable` null guard is mandatory (see root `CLAUDE.md`).
- `UI.Cancel` (Esc) routing in `UIScreenManager.HandleCancel`: open tab → close it; Game Menu open →
  `GameMenuUI.HandleBack()` (confirm → sub-panel → Resume); otherwise open the Game Menu **only if**
  `_wasGameplayLastFrame` (sampled in `LateUpdate`: cursor locked, not dead, not loading). The previous-frame sample is
  what stops the Esc that closes dialogue / container / trade (their own Cancel handlers re-lock the cursor in the same
  frame, in undefined order) from also opening the menu.
- Tab toggles and `OpenTab` are ignored while the Game Menu is open or the player is dead / in dialogue.
- `OnLoadStarted` closes every tab.

---

## Pause Rule

Only the Game Menu and the death screen set `Time.timeScale = 0`; character tabs never pause. Every exit path restores
it (Resume / Esc, load finish / fail). Coroutines and fades used while paused must use realtime waits.

---

## Gotchas

- `_tabPanelRoots` and `_tabButtons` must have the same length and be ordered by `ScreenTab` value — mismatch causes `IndexOutOfRangeException` at runtime.
- `UIScreenManager` does **not** implement `IScreenPanel` itself — it is the controller, not a panel.
