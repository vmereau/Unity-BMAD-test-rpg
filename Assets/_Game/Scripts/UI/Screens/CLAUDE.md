# CLAUDE.md — Assets/_Game/Scripts/UI/Screens

> Screen management contract and full-screen menu panels (Inventory, Quest Log, Character Stats, Options).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `UIScreenManager` | Opens/closes full-screen tabs. Owns `InputSystem_Actions`; listens to `InventoryToggle` and `CharacterStatsToggle` input actions. Manages `PlayerStateManager` state transitions and tab-button wiring. |
| `IScreenPanel` | Interface contract: `OnScreenOpen()` and `OnScreenClose()`. All full-screen panels must implement this. |
| `CharacterStatsUI` | Character stats screen. Shows level, XP, LP, HP, stamina, and all base stats. Implements `IScreenPanel`. |
| `OptionsUI` | Options/settings screen placeholder. Implements `IScreenPanel`. Currently logs open/close only. |

---

## ScreenTab Enum

```csharp
public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Options = 3 }
```

- `_tabPanelRoots[]` and `_tabButtons[]` in `UIScreenManager` are indexed by this enum.
- Adding a new tab requires: new enum value + new entry in both arrays in the Inspector.

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
- `UI.Cancel` closes the active tab (same as pressing the active tab button again).

---

## Gotchas

- `_tabPanelRoots` and `_tabButtons` must have the same length and be ordered by `ScreenTab` value — mismatch causes `IndexOutOfRangeException` at runtime.
- `UIScreenManager` does **not** implement `IScreenPanel` itself — it is the controller, not a panel.
