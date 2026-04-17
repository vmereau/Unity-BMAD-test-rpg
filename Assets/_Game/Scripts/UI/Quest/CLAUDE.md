# CLAUDE.md — Assets/_Game/Scripts/UI/Quest

> Quest Log screen, opened via `UIScreenManager`. Implements `IScreenPanel`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `QuestLogUI` | Root panel. Holds the `_allQuests` list and owns `QuestListPanelUI` + `QuestInfoPanelUI`. Implements `IScreenPanel` (`OnScreenOpen` / `OnScreenClose`). |
| `QuestListPanelUI` | Left-side panel. Renders a filtered list of `QuestButtonUI` entries based on active tab (Started / Completed / Failed). |
| `QuestButtonUI` | Single quest entry button. Binds to a `QuestSO`; notifies parent `QuestLogUI` on click. Handles hover highlight. |
| `QuestInfoPanelUI` | Right-side panel. Shows title, description, and step-by-step content for the selected quest. Displays an empty-state placeholder when nothing is selected. |
| `QuestTab` | Enum: `Started`, `Completed`, `Failed`. Used by `QuestListPanelUI` to filter the quest list. |

---

## Data Flow

```
QuestLogUI
  ├── QuestListPanelUI  (filters _allQuests by QuestTab, spawns QuestButtonUI per quest)
  │     └── QuestButtonUI × N  (click → QuestLogUI.OnQuestSelected)
  └── QuestInfoPanelUI  (Show(quest) / Hide())
```

- `QuestLogUI._allQuests` is the master list, wired in the Inspector (ScriptableObjects).
- Live refresh: `QuestLogUI` subscribes to `GameEventSO_Quest` channels (`_onQuestStarted`, `_onQuestCompleted`, `_onQuestFailed`) and calls `RefreshList()` on any change.

---

## IScreenPanel Contract

- `OnScreenOpen()` → `CursorManager.Unlock()` + `RefreshList()`.
- `OnScreenClose()` → `CursorManager.Lock()`.

---

## QuestButtonUI Notes

- Finds its parent `QuestLogUI` via `GetComponentInParent` (includes inactive) in `Awake`.
- Uses a cached `UnityAction _selectAction` to safely remove the listener on rebind — avoids listener leaks when the same button GO is reused across multiple quests.

---

## QuestInfoPanelUI Notes

- Uses prefab-based rendering: `BuildContent` destroys existing `_contentRoot` children then instantiates `QuestStepPrefab` per step/section. Each `QuestStepPrefab` instantiates `QuestPartPrefab` children for its active parts.
- `_emptyState` GameObject (e.g. "Select a quest" label) is toggled opposite to the detail fields.
- `_contentRoot` is the `Content` Transform inside `ContentScrollView/Viewport/Content`.
- Completed/Failed sections only spawn if `GetActiveParts` returns a non-empty list.

---

## Gotchas

- `QuestButtonUI` depends on finding `QuestLogUI` in a parent — the prefab **must** be instantiated inside the `QuestLogUI` hierarchy, never as a standalone GO.
- `QuestTab` is in `namespace Game.UI` — keep it there; do not move to `Game.Quest`.
