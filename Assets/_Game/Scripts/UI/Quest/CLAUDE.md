# CLAUDE.md — Assets/_Game/Scripts/UI/Quest

> Quest Log screen, opened via `UIScreenManager`. Implements `IScreenPanel`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `QuestLogUI` | Root panel. Holds the `_allQuests` list and owns `QuestListPanelUI` + `QuestInfoPanelUI`. Implements `IScreenPanel` (`OnScreenOpen` / `OnScreenClose`). |
| `QuestListPanelUI` | Left-side panel. Renders a filtered list of `QuestButtonUI` entries based on active tab (Started / Completed / Failed), tab labels with counts (`Started (n)`), and **owns the selection** (raises `SelectionChanged`). |
| `QuestButtonUI` | Single quest entry button. Binds to a `QuestSO`; notifies parent `QuestLogUI` on click. Hover + selected tint (selected wins). |
| `QuestInfoPanelUI` | Right-side panel. Header (title, status label, divider, description) + content blocks for the selected quest. Empty-state placeholder when nothing is selected. |
| `QuestStepUI` | One content block: collapsible step (`BindStep`) or fixed section (`BindSection` — "Quest accepted" / outcome). |
| `QuestPartUI` | One journal line (`• ` + `QuestPart.entry`). |
| `QuestTab` | Enum: `Started`, `Completed`, `Failed`. Used by `QuestListPanelUI` to filter the quest list. |

---

## Data Flow

```
QuestLogUI
  ├── QuestListPanelUI  (filters _allQuests by QuestTab, spawns QuestButtonUI per quest)
  │     └── QuestButtonUI × N  (click → QuestLogUI.SelectQuest → QuestListPanelUI.Select)
  └── QuestInfoPanelUI  (Show(quest) / Hide(), driven by QuestListPanelUI.SelectionChanged)
```

- **Selection:** `QuestListPanelUI` keeps the selected quest across refreshes / tab switches while it is
  visible, otherwise falls back to the first row (or none → info panel empty state), and raises
  `SelectionChanged` after **every** refresh — that is what rebuilds the info panel on live updates.

- `QuestLogUI._allQuests` is the master list, wired in the Inspector (ScriptableObjects).
- Live refresh: `QuestLogUI` subscribes to `GameEventSO_Quest` channels (`_onQuestStarted`, `_onQuestCompleted`, `_onQuestFailed`) and `_onFactChanged` (`GameEventSO_Fact`, raised by `WorldStateManager` on every fact write) and flags a refresh, run once in `LateUpdate` (coalesces event bursts) — the fact channel is what catches a single part becoming active, which raises no quest event. Subscriptions live in `OnEnable`, so they only run while the log is open.

---

## IScreenPanel Contract

- `OnScreenOpen()` → `CursorManager.Unlock()` + `RefreshList()` (auto-selects).
- `OnScreenClose()` → `_infoPanel.Hide()` (clears step toggles) + `CursorManager.Lock()`.

---

## QuestButtonUI Notes

- Finds its parent `QuestLogUI` via `GetComponentInParent` (includes inactive) in `Awake`.
- Uses a cached `UnityAction _selectAction` to safely remove the listener on rebind — avoids listener leaks when the same button GO is reused across multiple quests.

---

## QuestInfoPanelUI Notes

- Uses prefab-based rendering: `BuildContent` destroys existing `_contentRoot` children then instantiates `QuestStepPrefab` per block, in order: "Quest accepted" section (start part) → visible steps (no numbering) → divider + "Quest completed" / "Quest failed" section. Each block instantiates `QuestPartPrefab` children for its active parts; `PartsRoot` is hidden when no part has entry text.
- Step state: `IsStepCompleted(i)` → completed (dimmed title, green marker, **collapsed**), else in progress (brass marker + accent bar, **expanded**). Clicking the header toggles the body; `_stepOverrides` stores the player's choice per step index **with the completion state it was made in** (dropped when the step completes / regresses, so a newly completed step still collapses), kept across rebuilds of the same quest, cleared when another quest is shown or on `Hide()` (closing the log). Showing a different quest scrolls the content to the top.
- `QuestInfoPanel` is a `VerticalLayoutGroup` (Header preferred height, `ContentScrollView` flexible) so long descriptions push the steps down instead of overlapping. Colors are `[SerializeField]` defaults from `Art/ART_DIRECTION.md`.
- `_emptyState` GameObject (e.g. "Select a quest" label) is toggled opposite to the detail fields.
- `_contentRoot` is the `Content` Transform inside `ContentScrollView/Viewport/Content`.
- Completed/Failed sections only spawn if `GetActiveParts` returns a non-empty list.

---

## Gotchas

- `QuestButtonUI` depends on finding `QuestLogUI` in a parent — the prefab **must** be instantiated inside the `QuestLogUI` hierarchy, never as a standalone GO.
- `QuestTab` is in `namespace Game.UI` — keep it there; do not move to `Game.Quest`.
