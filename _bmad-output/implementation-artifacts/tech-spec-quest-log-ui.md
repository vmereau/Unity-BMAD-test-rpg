---
title: 'Quest Log UI'
slug: 'quest-log-ui'
created: '2026-04-16'
status: 'completed'
stepsCompleted: ['Task 1 - QuestButtonUI.cs', 'Task 2 - QuestListPanelUI.cs', 'Task 3 - QuestInfoPanelUI.cs', 'Task 4 - Replace QuestLogUI.cs', 'Task 5 - QuestLogButton.prefab', 'Task 6 - Wire QuestLogUI.prefab', 'Task 7 - Adversarial review (20 findings, 17 fixed, 3 skipped as design/perf non-issues)']
reviewNotes:
  adversarialReview: completed
  findingsTotal: 20
  findingsFixed: 17
  findingsSkipped: 3
  skippedReason: 'F16 (GC micro-opt), F17 (selection-loss UX, not a bug), F18 (tab-reset fragility, not a bug)'
  resolutionApproach: auto-fix
tech_stack: ['C#', 'Unity 6', 'UnityUI', 'TextMeshPro', 'ScriptableObjects']
files_to_modify:
  - Assets/_Game/Scripts/UI/QuestLogUI.cs
files_to_create:
  - Assets/_Game/Scripts/UI/QuestListPanelUI.cs
  - Assets/_Game/Scripts/UI/QuestInfoPanelUI.cs
  - Assets/_Game/Scripts/UI/QuestButtonUI.cs
  - Assets/_Game/Prefabs/UI/QuestLog/QuestLogButton.prefab
prefab_to_wire:
  - Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab
code_patterns:
  - IScreenPanel OnScreenOpen/OnScreenClose pattern
  - GameEventSO AddListener/RemoveListener in OnEnable/OnDisable
  - Destroy/recreate slot pattern (same as InventoryUI.RefreshSlots)
  - StringBuilder for multi-line TMP_Text content
  - IPointerEnterHandler/IPointerExitHandler for hover
---

# Tech-Spec: Quest Log UI

**Created:** 2026-04-16

## Overview

### Problem Statement

`QuestLogUI.cs` is a placeholder stub. The `QuestLogUI.prefab` is empty. Players have no way to view their quest states, objectives, or progress.

### Solution

Implement the full Quest Log screen as two panels:
1. **QuestListPanel** — Three tabs (Started / Completed / Failed) listing `QuestSO.title` as clickable buttons.
2. **QuestInfoPanel** — Displays the selected quest's header (title + description) and a dynamically built content body showing the start entry, numbered active steps with their completed parts, and the completion/failure line if applicable.

### Scope

**In Scope:**
- `QuestLogUI.cs` replacement (IScreenPanel + event wiring)
- `QuestListPanelUI.cs` — tab switching + button list management
- `QuestButtonUI.cs` — individual quest button with hover
- `QuestInfoPanelUI.cs` — content builder using `StringBuilder`
- New `QuestLogButton.prefab`
- Wiring the existing `QuestLogUI.prefab` hierarchy via MCP
- Subscribing to `GameEventSO_Quest` channels to refresh on state change

**Out of Scope:**
- Quest tracker HUD (overlay while playing, no open panel — separate story)
- Save/Load of which quest is selected
- Fixing the pre-existing `IsCompleted` logic bug in `QuestSO.cs` (always returns `true` when `completedParts` is non-empty — tracked separately)
- Quest rewards display (Story 6-8)

---

## Context for Development

### Codebase Patterns

- **IScreenPanel** (`Assets/_Game/Scripts/UI/IScreenPanel.cs`): two-method interface `OnScreenOpen()` / `OnScreenClose()`. `UIScreenManager` calls these when toggling the `QuestLog` tab (index 1 in `ScreenTab` enum).
- **GameEventSO pattern**: subscribe in `OnEnable`, unsubscribe in `OnDisable`. Never in `Start`. See `InventoryUI.cs` or `HealthBarUI.cs`.
- **Destroy/recreate slot pattern**: `foreach (Transform child in _contentRoot) Destroy(child.gameObject)` then `Instantiate(_prefab, _contentRoot)` per item. Used in `InventoryUI.RefreshSlots`. Fine for small lists.
- **TMP_Text**: always `TMP_Text` (TextMeshPro), never `UnityEngine.UI.Text`.
- **Hover without polling**: `IPointerEnterHandler` / `IPointerExitHandler`, not `Update`. See `ItemSlotUI.cs`.
- **GameLog**: `GameLog.Info(TAG, ...)` / `GameLog.Warn(TAG, ...)`. `TAG` field only in classes that call `GameLog` — otherwise dead code (code review LOW).
- **Canvas**: existing `QuestLogUI.prefab` is a child of the main `UICanvas`. Do not add a new Canvas.
- **Namespace**: `Game.UI` for all scripts in `Assets/_Game/Scripts/UI/`.
- **`GetComponentInParent`**: `QuestButtonUI` must call `GetComponentInParent<QuestLogUI>()` in `Awake` (not per-click) to cache the parent reference.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/QuestLogUI.cs` | Replace stub — implement IScreenPanel + event subscriptions |
| `Assets/_Game/Scripts/UI/IScreenPanel.cs` | Interface to implement |
| `Assets/_Game/Scripts/UI/UIScreenManager.cs` | Shows how `OnScreenOpen`/`OnScreenClose` are called; `ScreenTab.QuestLog = 1` |
| `Assets/_Game/Scripts/UI/InventoryUI.cs` | Destroy/recreate pattern, OnEnable/OnDisable event wiring |
| `Assets/_Game/Scripts/UI/ItemSlotUI.cs` | IPointerEnterHandler/IPointerExitHandler hover pattern |
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | Data model — `title`, `description`, `startPart`, `steps`, `completedParts`, `failedParts`, `IsStarted`, `IsCompleted`, `IsFailed` |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Quest.cs` | `GameEventSO<QuestSO>` — fired on quest started/completed/failed |
| `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` | Produces `_onQuestStarted`, `_onQuestCompleted`, `_onQuestFailed` channels |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | `WorldStateManager.Instance.GetFact(Fact)` — used in content builder |
| `Assets/_Game/Scripts/UI/CLAUDE.md` | UI patterns — cursor, TMP, canvas rebuild rules |

### Technical Decisions

- **Single `TMP_Text` for content body**: The content section is a single `TMP_Text` populated via `StringBuilder`. Simpler than instantiating per-step GameObjects, avoids layout rebuild overhead, and is sufficient for the amount of text.
- **`QuestButtonUI` caches parent via `GetComponentInParent<QuestLogUI>()` in `Awake`**: clean, avoids per-click lookup and avoids a `[SerializeField]` injected reference.
- **Tabs use `Button.interactable = false` on active tab**: same pattern as `UIScreenManager.UpdateTabButtonStates()`.
- **Active step definition**: a step is shown if at least one of its `parts` has `WorldStateManager.Instance.GetFact(part.fact) == true`. Steps with no true parts are hidden (not yet triggered).
- **Completed/Failed parts display**: reads `WorldStateManager.Instance.GetFact(part.fact)` directly (not via `IsCompleted` / `IsFailed`) to avoid being affected by the pre-existing inversion bug in `QuestSO.IsCompleted`.
- **`_onQuestStarted`/`_onQuestCompleted`/`_onQuestFailed` in QuestLogUI**: subscribed in `OnEnable`/`OnDisable` so the list auto-refreshes when a quest transitions state while the log is open. When closed (panel inactive), events are not received; `OnScreenOpen` always calls `RefreshList()` to catch up.
- **`_allQuests` list on `QuestLogUI`**: serialized `List<QuestSO>` assigned in the Inspector — same pattern as `QuestEventsManager._quests`. No coupling to `QuestEventsManager`.

---

## Implementation Plan

### Tasks

Tasks are ordered dependency-first.

---

**Task 1 — Create `QuestButtonUI.cs`**

File: `Assets/_Game/Scripts/UI/QuestButtonUI.cs` *(new file)*

```csharp
using Game.Quest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    public class QuestButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Button _button;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        [SerializeField] private Color _hoverColor  = new Color(0.30f, 0.30f, 0.30f, 0.9f);

        private QuestSO _quest;
        private QuestLogUI _parent;

        private void Awake()
        {
            _parent = GetComponentInParent<QuestLogUI>(includeInactive: true);
        }

        public void Bind(QuestSO quest)
        {
            _quest = quest;
            _titleText.text = quest.title;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _parent.SelectQuest(_quest));
            SetHover(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHover(true);
        public void OnPointerExit(PointerEventData eventData)  => SetHover(false);

        private void SetHover(bool hovered)
        {
            if (_backgroundImage != null)
                _backgroundImage.color = hovered ? _hoverColor : _normalColor;
        }
    }
}
```

> **Note:** `GetComponentInParent(includeInactive: true)` is required because the prefab is
> instantiated as a child of the list content which is inside `QuestLogUI`. The root may be
> inactive during first `Awake` depending on panel open state.

---

**Task 2 — Create `QuestListPanelUI.cs`**

File: `Assets/_Game/Scripts/UI/QuestListPanelUI.cs` *(new file)*

```csharp
using System.Collections.Generic;
using Game.Quest;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public enum QuestTab { Started, Completed, Failed }

    public class QuestListPanelUI : MonoBehaviour
    {
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _questButtonPrefab;
        [SerializeField] private Button _tabStarted;
        [SerializeField] private Button _tabCompleted;
        [SerializeField] private Button _tabFailed;

        private List<QuestSO> _allQuests;
        private QuestTab _activeTab = QuestTab.Started;

        private void OnEnable()
        {
            _tabStarted.onClick.AddListener(  () => SwitchTab(QuestTab.Started));
            _tabCompleted.onClick.AddListener(() => SwitchTab(QuestTab.Completed));
            _tabFailed.onClick.AddListener(   () => SwitchTab(QuestTab.Failed));
        }

        private void OnDisable()
        {
            _tabStarted.onClick.RemoveAllListeners();
            _tabCompleted.onClick.RemoveAllListeners();
            _tabFailed.onClick.RemoveAllListeners();
        }

        public void Refresh(List<QuestSO> allQuests)
        {
            _allQuests = allQuests;
            RefreshButtons();
            UpdateTabStyles();
        }

        private void SwitchTab(QuestTab tab)
        {
            _activeTab = tab;
            RefreshButtons();
            UpdateTabStyles();
        }

        private void RefreshButtons()
        {
            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            if (_allQuests == null) return;

            foreach (var quest in _allQuests)
            {
                if (quest == null) continue;
                bool show = _activeTab switch
                {
                    QuestTab.Started   => quest.IsStarted,
                    QuestTab.Completed => quest.IsCompleted,
                    QuestTab.Failed    => quest.IsFailed,
                    _                  => false
                };
                if (!show) continue;

                var go = Instantiate(_questButtonPrefab, _contentRoot);
                go.GetComponent<QuestButtonUI>().Bind(quest);
            }
        }

        private void UpdateTabStyles()
        {
            _tabStarted.interactable   = _activeTab != QuestTab.Started;
            _tabCompleted.interactable = _activeTab != QuestTab.Completed;
            _tabFailed.interactable    = _activeTab != QuestTab.Failed;
        }
    }
}
```

---

**Task 3 — Create `QuestInfoPanelUI.cs`**

File: `Assets/_Game/Scripts/UI/QuestInfoPanelUI.cs` *(new file)*

```csharp
using System.Text;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestInfoPanelUI : MonoBehaviour
    {
        private const string TAG = "[QuestInfoPanel]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private TMP_Text _contentText;
        [SerializeField] private GameObject _emptyState; // "Select a quest" placeholder GO

        public void Show(QuestSO quest)
        {
            if (quest == null) { Hide(); return; }

            _titleText.text       = quest.title;
            _descriptionText.text = quest.description;
            _contentText.text     = BuildContent(quest);

            _emptyState?.SetActive(false);
            _titleText.gameObject.SetActive(true);
            _descriptionText.gameObject.SetActive(true);
            _contentText.gameObject.SetActive(true);

            GameLog.Info(TAG, $"Showing quest: {quest.title}");
        }

        public void Hide()
        {
            _emptyState?.SetActive(true);
            _titleText.gameObject.SetActive(false);
            _descriptionText.gameObject.SetActive(false);
            _contentText.gameObject.SetActive(false);
        }

        private static string BuildContent(QuestSO quest)
        {
            var sb = new StringBuilder();

            // ── Start part entry ──────────────────────────────────────────────────
            if (quest.IsStarted
                && quest.startPart.fact != null
                && !string.IsNullOrEmpty(quest.startPart.entry))
            {
                sb.AppendLine($"- {quest.startPart.entry}");
            }

            // ── Numbered active steps ─────────────────────────────────────────────
            for (int i = 0; i < quest.steps.Count; i++)
            {
                var step = quest.steps[i];
                if (!step.IsActive()) continue;

                sb.AppendLine($"{i + 1}. {step.title}");
                if (!string.IsNullOrEmpty(step.description))
                    sb.AppendLine(step.description);

                foreach (var part in QuestSO.GetActiveParts(step.parts))
                {
                    if (!string.IsNullOrEmpty(part.entry))
                        sb.AppendLine($"   - {part.entry}");
                }
            }

            // ── Completion / failure footer ────────────────────────────────────────
            if (quest.IsCompleted)
            {
                sb.AppendLine();
                sb.AppendLine("Completed");
                foreach (var part in QuestSO.GetActiveParts(quest.completedParts))
                {
                    if (!string.IsNullOrEmpty(part.entry))
                        sb.AppendLine($"- {part.entry}");
                }
            }
            else if (quest.IsFailed)
            {
                sb.AppendLine();
                sb.AppendLine("Failed");
                foreach (var part in QuestSO.GetActiveParts(quest.failedParts))
                {
                    if (!string.IsNullOrEmpty(part.entry))
                        sb.AppendLine($"- {part.entry}");
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
```

---

**Task 4 — Replace `QuestLogUI.cs`**

File: `Assets/_Game/Scripts/UI/QuestLogUI.cs` *(replace stub)*

```csharp
using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using UnityEngine;

namespace Game.UI
{
    public class QuestLogUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[QuestLogUI]";

        [SerializeField] private List<QuestSO> _allQuests = new List<QuestSO>();
        [SerializeField] private QuestListPanelUI _listPanel;
        [SerializeField] private QuestInfoPanelUI _infoPanel;

        [Header("Event Channels (optional — for live refresh)")]
        [SerializeField] private GameEventSO_Quest _onQuestStarted;
        [SerializeField] private GameEventSO_Quest _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest _onQuestFailed;

        private void OnEnable()
        {
            _onQuestStarted?.AddListener(HandleQuestStateChanged);
            _onQuestCompleted?.AddListener(HandleQuestStateChanged);
            _onQuestFailed?.AddListener(HandleQuestStateChanged);
        }

        private void OnDisable()
        {
            _onQuestStarted?.RemoveListener(HandleQuestStateChanged);
            _onQuestCompleted?.RemoveListener(HandleQuestStateChanged);
            _onQuestFailed?.RemoveListener(HandleQuestStateChanged);
        }

        public void OnScreenOpen()
        {
            RefreshList();
            _infoPanel.Hide();
            GameLog.Info(TAG, "Quest Log opened");
        }

        public void OnScreenClose()
        {
            _infoPanel.Hide();
            GameLog.Info(TAG, "Quest Log closed");
        }

        public void SelectQuest(QuestSO quest)
        {
            _infoPanel.Show(quest);
        }

        private void HandleQuestStateChanged(QuestSO _) => RefreshList();

        private void RefreshList()
        {
            _listPanel.Refresh(_allQuests);
        }
    }
}
```

---

**Task 5 — Create `QuestLogButton.prefab`**

Path: `Assets/_Game/Prefabs/UI/QuestLog/QuestLogButton.prefab`

Hierarchy (build via MCP `manage_gameobject` + `manage_components`):

```
QuestLogButton  [Image(_backgroundImage), Button, QuestButtonUI]
└── ButtonText  [RectTransform anchored fill, TMP_Text]
```

Setup details:
- Root `RectTransform`: `sizeDelta = (0, 40)`, `anchorMin = (0,0)`, `anchorMax = (1,0)` (horizontal stretch, fixed height).
- `Image` on root: solid color `(0.15, 0.15, 0.15, 0.8)` — this is `_backgroundImage`.
- `Button` on root: `Transition = None` (hover color handled manually by `QuestButtonUI`).
- `TMP_Text` on `ButtonText`: anchors = full stretch (0,0)→(1,1), `sizeDelta=(0,0)`, `alignment = MiddleLeft`, font size 14, left padding ~8px via `margin`.
- Wire `QuestButtonUI` serialized fields:
  - `_titleText` → `ButtonText`
  - `_button` → root Button
  - `_backgroundImage` → root Image

---

**Task 6 — Wire `QuestLogUI.prefab` hierarchy**

The existing prefab at `Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab` is currently an empty root with just `QuestLogUI.cs` stub. Rebuild it via MCP to match this hierarchy:

```
QuestLogUI  [existing root — add QuestLogUI.cs (already there), RectTransform full-stretch]
├── QuestListPanel  [RectTransform left-half, Image bg, QuestListPanelUI.cs]
│   ├── TabBar  [HorizontalLayoutGroup, fixed height 36px]
│   │   ├── TabStarted    [Button, TMP_Text "Started"]
│   │   ├── TabCompleted  [Button, TMP_Text "Completed"]
│   │   └── TabFailed     [Button, TMP_Text "Failed"]
│   └── QuestListScrollView  [ScrollRect, vertical only]
│       └── Viewport  [Mask, Image]
│           └── Content  [VerticalLayoutGroup (spacing=2, padding top/bottom 4), ContentSizeFitter(vertical=PreferredSize)]
│               └── (empty — populated at runtime)
└── QuestInfoPanel  [RectTransform right-half, Image bg, QuestInfoPanelUI.cs]
    ├── EmptyState   [TMP_Text, text="Select a quest", alignment=MiddleCenter, initially active]
    ├── Header  [VerticalLayoutGroup, ContentSizeFitter(vertical=PreferredSize), initially inactive]
    │   ├── QuestTitle        [TMP_Text, bold, font size 18]
    │   └── QuestDescription  [TMP_Text, font size 13, italic]
    └── ContentScrollView  [ScrollRect, vertical only, initially inactive]
        └── Viewport  [Mask, Image]
            └── Content  [VerticalLayoutGroup, ContentSizeFitter(vertical=PreferredSize)]
                └── QuestContent  [TMP_Text, font size 13, alignment=TopLeft, enableWordWrapping=true]
```

Layout split: `QuestListPanel` left 35% of the panel, `QuestInfoPanel` right 65%. Use anchor presets on each.

Wire `QuestListPanelUI` serialized fields:
- `_contentRoot` → `QuestListPanel/QuestListScrollView/Viewport/Content`
- `_questButtonPrefab` → `QuestLogButton.prefab` asset reference
- `_tabStarted` → `TabBar/TabStarted` Button
- `_tabCompleted` → `TabBar/TabCompleted` Button
- `_tabFailed` → `TabBar/TabFailed` Button

Wire `QuestInfoPanelUI` serialized fields:
- `_titleText` → `QuestInfoPanel/Header/QuestTitle`
- `_descriptionText` → `QuestInfoPanel/Header/QuestDescription`
- `_contentText` → `QuestInfoPanel/ContentScrollView/Viewport/Content/QuestContent`
- `_emptyState` → `QuestInfoPanel/EmptyState` GameObject

Wire `QuestLogUI` serialized fields:
- `_listPanel` → `QuestListPanel` (QuestListPanelUI component)
- `_infoPanel` → `QuestInfoPanel` (QuestInfoPanelUI component)
- `_allQuests` → assign all `QuestSO` assets from `Assets/_Game/ScriptableObjects/Quest/`
- `_onQuestStarted` → `OnQuestStarted` GameEventSO asset
- `_onQuestCompleted` → `OnQuestCompleted` GameEventSO asset
- `_onQuestFailed` → `OnQuestFailed` GameEventSO asset

> **MCP Canvas quirk reminder**: If a Canvas is ever created via MCP `manage_gameobject(create)`, it defaults to `renderMode = 2` (World Space). Always follow up with `manage_components set_property renderMode 0` to set Screen Space - Overlay. QuestLogUI is a child panel, not a Canvas root, so this quirk does not apply here.

---

### Acceptance Criteria

**AC-1: Tab switching populates quest list**
- Given the Quest Log is opened with at least one `IsStarted` quest, When the panel opens, Then the Started tab is active and the quest's title appears as a button.
- Given a quest is `IsCompleted`, When I click the Completed tab, Then that quest's title appears in the list.
- Given a quest is `IsFailed`, When I click the Failed tab, Then that quest's title appears in the list.
- Given no quests match the active tab, Then the list content is empty (no buttons).

**AC-2: Quest button hover style**
- Given a quest button is visible in the list, When I hover over it, Then its background color changes to `_hoverColor`.
- When I move the cursor off the button, Then its background reverts to `_normalColor`.

**AC-3: Quest selection shows info panel**
- Given a quest is listed, When I click its button, Then `QuestInfoPanel` shows the quest's `title` in the header and `description` below.
- When no quest is selected (on open or after close), Then the `EmptyState` text is visible and title/description/content are hidden.

**AC-4: Content body — start part**
- Given a quest `IsStarted` and `startPart.fact` is true in WSM, When I select it, Then the content body begins with `- <startPart.entry>`.

**AC-5: Content body — numbered active steps**
- Given a step has at least one part with a true fact, When I select the quest, Then the step is shown as `{i+1}. {step.title}` followed by `{step.description}` and a `   - {part.entry}` line for each true part.
- Given a step has no true parts, Then that step does not appear in the content body.

**AC-6: Content body — completion / failure footer**
- Given `quest.IsCompleted` is true, When I select the quest, Then the content body ends with a blank line, then `Completed`, then `- {entry}` for each `completedPart` whose fact is true.
- Given `quest.IsFailed` is true, When I select the quest, Then `Failed` and the relevant entry appear instead.
- Given the quest is neither completed nor failed, Then no footer appears.

**AC-7: Live refresh on quest state change**
- Given the Quest Log is open and a quest transitions to `IsStarted` (via `_onQuestStarted`), When the event fires, Then the Started tab list refreshes and the new quest appears without reopening the panel.

**AC-8: No regressions**
- Opening Inventory, Character Stats, or Options tabs still works.
- Closing the Quest Log via `Escape` (UIScreenManager `HandleCancel`) works.
- Existing `QuestEventsManager` scene wiring is untouched.

---

## Additional Context

### Dependencies

- `WorldStateManager.Instance` must be non-null at runtime for step/completion display. The content builder guards with `if (WorldStateManager.Instance != null)` and skips gracefully.
- `GameEventSO_Quest` assets (`OnQuestStarted`, `OnQuestCompleted`, `OnQuestFailed`) must be the same SO assets wired into `QuestEventsManager._onQuestStarted` etc. If not wired into `QuestLogUI`, the log still works — it simply won't auto-refresh while open.
- Story 6-4 (`quest-acquisition`, currently `review`) must be merged first if it introduces any `QuestSO` assets needed for manual testing. However, this story is code-only and can be compiled and tested with any `QuestSO` asset.

### Testing Strategy

- **Manual**: Open the Quest Log tab in Play mode. Verify three tabs. Assign at least one `QuestSO` to `_allQuests` in the Inspector; use the editor's WorldStateManager to set `startPart.fact` true; verify it appears in Started tab and clicking it shows correct content.
- **Manual**: Trigger a step part fact from a dialogue or script; reopen the Quest Log; confirm the step appears in the selected quest's info panel.
- No automated tests required for this story (UI rendering is not covered by EditMode tests in this project's test strategy).

### Notes

- **`IsCompleted` fix**: The original property had an inverted `!GetFact` check (always returned `true` when `completedParts` non-empty). This was fixed in `QuestSO.cs` as part of this story — `IsCompleted` now correctly returns `true` only when at least one `completedPart` fact is true.
- **`QuestButtonUI.Awake` uses `includeInactive: true`**: required because `QuestListPanel` may be active while `QuestInfoPanel` content is inactive. The entire `QuestLogUI` root is inactive until the log is opened, so without `includeInactive: true`, `GetComponentInParent` would return null.
- **`QuestTab` enum lives in `QuestListPanelUI.cs`** in `namespace Game.UI` — it is only used by `QuestListPanelUI` and does not need its own file.
- **`QuestLogButton.prefab` button transition = None**: visual feedback is handled entirely by `QuestButtonUI.SetHover`. Setting `Transition = None` prevents the default Unity `ColorBlock` animation from fighting with the manual color assignment.
