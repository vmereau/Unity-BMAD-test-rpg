---
title: 'Quest Info Panel Prefab-Based Content Rendering'
slug: 'quest-info-panel-prefab-content'
created: '2026-04-17'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6]
tech_stack: ['C#', 'Unity 6', 'UnityUI', 'TextMeshPro', 'ScriptableObjects']
files_to_modify:
  - Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs
  - Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab
files_to_create:
  - Assets/_Game/Scripts/UI/Quest/QuestPartUI.cs
  - Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs
  - Assets/_Game/Prefabs/UI/QuestLog/QuestPartPrefab.prefab
  - Assets/_Game/Prefabs/UI/QuestLog/QuestStepPrefab.prefab
code_patterns:
  - Destroy/recreate content pattern (same as QuestListPanelUI.RefreshButtons)
  - Prefab-based dynamic list rendering
  - MonoBehaviour Bind() pattern (QuestButtonUI precedent)
test_patterns: []
---

# Tech-Spec: Quest Info Panel Prefab-Based Content Rendering

**Created:** 2026-04-17

## Overview

### Problem Statement

`QuestInfoPanelUI.BuildContent()` renders quest step and part data as a single flat string into one `TMP_Text` component. There is no visual hierarchy — steps and parts are indistinguishable except by indentation characters. This prevents independent styling, spacing, or future interactivity per step/part.

### Solution

Replace the single `_contentText` TMP_Text field with prefab-based rendering. Two new prefabs are introduced: `QuestPartPrefab` (a TMP_Text for one `entry` value) and `QuestStepPrefab` (a Title TMP_Text + Description TMP_Text + a vertical parts-list root). `QuestInfoPanelUI.BuildContent()` is rewritten to destroy/recreate prefab instances in the existing `Content` scroll root. Non-step sections (startPart, Completed/Failed footer) are rendered as `QuestStepPrefab` instances with synthesized titles (`"Start"`, `"Completed"`, `"Failed"`).

### Scope

**In Scope:**
- `QuestPartUI.cs` — MonoBehaviour with `Bind(QuestPart)` and a `[SerializeField] TMP_Text _entryText`
- `QuestStepUI.cs` — MonoBehaviour with `Bind(string title, string description, List<QuestPart> activeParts)` and `[SerializeField] GameObject _questPartPrefab`
- `QuestPartPrefab.prefab` — root with `QuestPartUI` component
- `QuestStepPrefab.prefab` — root with `QuestStepUI` component
- `QuestInfoPanelUI.cs` update — replace `_contentText` with `_contentRoot` + two prefab refs, rewrite `BuildContent` to instantiate prefabs
- `QuestLogUI.prefab` update — remove `QuestContent` TMP_Text child, assign `_contentRoot` and prefab refs on `QuestInfoPanelUI`

**Out of Scope:**
- `QuestListPanelUI`, `QuestButtonUI`, `QuestLogUI` — no changes
- `QuestSO` data model — no changes
- Visual styling/theming beyond functional layout (color, fonts, spacing)
- Quest tracker HUD overlay

---

## Context for Development

### Codebase Patterns

- **Destroy/recreate content pattern**: `foreach (Transform child in _root) Destroy(child.gameObject)` then `Instantiate(_prefab, _root)`. Used in `QuestListPanelUI.RefreshButtons()` — same pattern applies here.
- **MonoBehaviour `Bind()` pattern**: `QuestButtonUI.Bind(QuestSO)` sets text and wires listeners. `QuestPartUI.Bind(QuestPart)` and `QuestStepUI.Bind(...)` follow the same convention.
- **TMP_Text**: always `TMP_Text` (TextMeshPro), never `UnityEngine.UI.Text`.
- **GameLog**: `GameLog.Warn(TAG, ...)` / `GameLog.Info(TAG, ...)`. `TAG` only in classes that call `GameLog`.
- **Namespace**: `Game.UI` for all scripts in `Assets/_Game/Scripts/UI/`.
- **`QuestSO.GetActiveParts(list)`**: static helper that filters a `List<QuestPart>` to those whose fact is true in `WorldStateManager`. Use this — do not inline the filtering logic.
- **`QuestStep.IsActive()`**: returns true if at least one part's fact is true. Use to gate whether a step prefab is spawned at all.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs` | File to update — current `BuildContent` logic to migrate to prefab-based |
| `Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs` | Destroy/recreate pattern precedent (`RefreshButtons`) |
| `Assets/_Game/Scripts/UI/Quest/QuestButtonUI.cs` | `Bind()` pattern precedent |
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | Data model: `QuestPart`, `QuestStep`, `GetActiveParts`, `IsActive()` |
| `Assets/_Game/Scripts/UI/Quest/CLAUDE.md` | Quest UI subsystem rules |
| `Assets/_Game/Scripts/UI/CLAUDE.md` | UI patterns — TMP, Canvas rebuild, layout |

### Technical Decisions

- **`QuestStepUI` owns its `_questPartPrefab` ref (Option A)**: the prefab is self-contained. `QuestInfoPanelUI` only needs `_questStepPrefab`. Each `QuestStepUI` instance spawns its own `QuestPartPrefab` children into its `_partsRoot`. This avoids threading the prefab reference through `Bind()` parameters.
- **Non-step sections use `QuestStepPrefab`**: `startPart` → `QuestStepPrefab` with title `"Start"`, no description, one part. `Completed`/`Failed` footer → `QuestStepPrefab` with title `"Completed"` or `"Failed"`, no description, active completion/failure parts. This reuses the same visual treatment for all content.
- **`_contentText` is removed**: `QuestInfoPanelUI` loses the `_contentText` field and gains `_contentRoot` (Transform) + `_questStepPrefab` (GameObject). The `Show()`/`Hide()` toggles that previously toggled `_contentText.gameObject` now toggle the `_contentRoot` GameObject.
- **`BuildContent` becomes `void BuildContent(QuestSO)`**: no longer returns a string. Destroys existing children of `_contentRoot` then instantiates step prefabs. Called from `Show()`.
- **`Content` Transform in the prefab** at `QuestInfoPanel/ContentScrollView/Viewport/Content` becomes `_contentRoot`. The `QuestContent` TMP_Text child is removed.

---

## Implementation Plan

### Tasks

Tasks are ordered dependency-first.

---

**Task 1 — Create `QuestPartUI.cs`**

File: `Assets/_Game/Scripts/UI/Quest/QuestPartUI.cs` *(new file)*

```csharp
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestPartUI : MonoBehaviour
    {
        private const string TAG = "[QuestPartUI]";

        [SerializeField] private TMP_Text _entryText;

        public void Bind(QuestPart part)
        {
            if (_entryText == null) { GameLog.Warn(TAG, "_entryText is not assigned."); return; }
            _entryText.text = part.entry;
        }
    }
}
```

---

**Task 2 — Create `QuestStepUI.cs`**

File: `Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs` *(new file)*

```csharp
using System.Collections.Generic;
using Game.Core;
using Game.Quest;
using TMPro;
using UnityEngine;

namespace Game.UI
{
    public class QuestStepUI : MonoBehaviour
    {
        private const string TAG = "[QuestStepUI]";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Transform _partsRoot;
        [SerializeField] private GameObject _questPartPrefab;

        public void Bind(string title, string description, List<QuestPart> activeParts)
        {
            if (_titleText == null)      { GameLog.Warn(TAG, "_titleText is not assigned."); return; }
            if (_partsRoot == null)      { GameLog.Warn(TAG, "_partsRoot is not assigned."); return; }
            if (_questPartPrefab == null){ GameLog.Warn(TAG, "_questPartPrefab is not assigned."); return; }

            _titleText.text = title;

            if (_descriptionText != null)
            {
                if (string.IsNullOrEmpty(description))
                {
                    _descriptionText.gameObject.SetActive(false);
                }
                else
                {
                    _descriptionText.gameObject.SetActive(true);
                    _descriptionText.text = description;
                }
            }

            foreach (Transform child in _partsRoot)
                Destroy(child.gameObject);

            foreach (var part in activeParts)
            {
                if (string.IsNullOrEmpty(part.entry)) continue;
                var go = Instantiate(_questPartPrefab, _partsRoot);
                var partUI = go.GetComponent<QuestPartUI>();
                if (partUI == null)
                {
                    GameLog.Error(TAG, $"_questPartPrefab is missing QuestPartUI component on '{go.name}'.");
                    Destroy(go);
                    continue;
                }
                partUI.Bind(part);
            }
        }
    }
}
```

---

**Task 3 — Create `QuestPartPrefab.prefab`**

Path: `Assets/_Game/Prefabs/UI/QuestLog/QuestPartPrefab.prefab`

Hierarchy (build via MCP):

```
QuestPartPrefab  [RectTransform, QuestPartUI]
└── EntryText    [RectTransform anchored fill, TMP_Text]
```

Setup:
- Root `RectTransform`: `anchorMin=(0,0)`, `anchorMax=(1,0)`, `sizeDelta=(0,24)` — horizontal stretch, fixed height 24.
- `TMP_Text` on `EntryText`: font size 12, alignment = MiddleLeft, `enableWordWrapping = true`.
- Wire `QuestPartUI._entryText` → `EntryText`.

---

**Task 4 — Create `QuestStepPrefab.prefab`**

Path: `Assets/_Game/Prefabs/UI/QuestLog/QuestStepPrefab.prefab`

Hierarchy (build via MCP):

```
QuestStepPrefab   [VerticalLayoutGroup (spacing=2, childForceExpandWidth=true), ContentSizeFitter(vertical=PreferredSize), QuestStepUI]
├── StepTitle     [RectTransform, TMP_Text bold font size 14]
├── StepDesc      [RectTransform, TMP_Text italic font size 12, initially active]
└── PartsRoot     [VerticalLayoutGroup (spacing=1, childForceExpandWidth=true), ContentSizeFitter(vertical=PreferredSize)]
    └── (empty — populated at runtime by QuestStepUI.Bind)
```

Setup:
- Root `RectTransform`: `anchorMin=(0,1)`, `anchorMax=(1,1)`, left-anchored stretch. `VerticalLayoutGroup` with `childControlHeight=true`, `childForceExpandHeight=false`, spacing=2.
- `ContentSizeFitter` on root: `verticalFit = PreferredSize`.
- `StepTitle` TMP_Text: bold, font size 14, `alignment=MiddleLeft`.
- `StepDesc` TMP_Text: italic, font size 12, `alignment=TopLeft`, `enableWordWrapping=true`.
- `PartsRoot`: `VerticalLayoutGroup` (spacing=1, `childControlHeight=true`, `childForceExpandHeight=false`), `ContentSizeFitter(verticalFit=PreferredSize)`.
- Wire `QuestStepUI` serialized fields:
  - `_titleText` → `StepTitle`
  - `_descriptionText` → `StepDesc`
  - `_partsRoot` → `PartsRoot`
  - `_questPartPrefab` → `QuestPartPrefab.prefab` asset reference

---

**Task 5 — Update `QuestInfoPanelUI.cs`**

File: `Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs` *(modify)*

Replace the full file content:

```csharp
using System.Collections.Generic;
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
        [SerializeField] private Transform _contentRoot;
        [SerializeField] private GameObject _questStepPrefab;
        [SerializeField] private GameObject _emptyState;

        public void Show(QuestSO quest)
        {
            if (quest == null)
            {
                GameLog.Warn(TAG, "Show called with null quest — hiding panel.");
                Hide();
                return;
            }
            if (_titleText == null || _descriptionText == null || _contentRoot == null || _questStepPrefab == null)
            {
                GameLog.Warn(TAG, "One or more required fields are not assigned — cannot show quest info.");
                return;
            }

            _emptyState?.SetActive(false);
            _titleText.gameObject.SetActive(true);
            _descriptionText.gameObject.SetActive(true);
            _contentRoot.gameObject.SetActive(true);

            _titleText.text       = quest.title;
            _descriptionText.text = quest.description;
            BuildContent(quest);

            GameLog.Info(TAG, $"Showing quest: {quest.title}");
        }

        public void Hide()
        {
            _emptyState?.SetActive(true);
            if (_titleText != null)       _titleText.gameObject.SetActive(false);
            if (_descriptionText != null) _descriptionText.gameObject.SetActive(false);
            if (_contentRoot != null)     _contentRoot.gameObject.SetActive(false);
            GameLog.Info(TAG, "Quest info panel hidden");
        }

        private void BuildContent(QuestSO quest)
        {
            foreach (Transform child in _contentRoot)
                Destroy(child.gameObject);

            // ── Start part ────────────────────────────────────────────────────────
            if (quest.IsStarted
                && quest.startPart.fact != null
                && !string.IsNullOrEmpty(quest.startPart.entry))
            {
                SpawnStep("Start", null, new List<QuestPart> { quest.startPart });
            }

            // ── Numbered active steps ─────────────────────────────────────────────
            if (quest.steps != null)
            {
                int displayNumber = 1;
                foreach (var step in quest.steps)
                {
                    if (!step.IsActive()) continue;
                    var activeParts = QuestSO.GetActiveParts(step.parts);
                    SpawnStep($"{displayNumber}. {step.title}", step.description, activeParts);
                    displayNumber++;
                }
            }

            // ── Completion / failure footer ────────────────────────────────────────
            if (quest.IsCompleted)
            {
                var parts = QuestSO.GetActiveParts(quest.completedParts);
                SpawnStep("Completed", null, parts);
            }
            else if (quest.IsFailed)
            {
                var parts = QuestSO.GetActiveParts(quest.failedParts);
                SpawnStep("Failed", null, parts);
            }
        }

        private void SpawnStep(string title, string description, List<QuestPart> activeParts)
        {
            var go = Instantiate(_questStepPrefab, _contentRoot);
            var stepUI = go.GetComponent<QuestStepUI>();
            if (stepUI == null)
            {
                GameLog.Error(TAG, $"_questStepPrefab is missing QuestStepUI component on '{go.name}'.");
                Destroy(go);
                return;
            }
            stepUI.Bind(title, description, activeParts);
        }
    }
}
```

---

**Task 6 — Update `QuestLogUI.prefab` QuestInfoPanel wiring**

File: `Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab` *(modify via MCP)*

Changes required:
1. **Remove** `QuestContent` TMP_Text child from `QuestInfoPanel/ContentScrollView/Viewport/Content`.
2. **Update `QuestInfoPanelUI` component** on `QuestInfoPanel`:
   - Remove the old `_contentText` field reference (now gone from the script).
   - Assign `_contentRoot` → `QuestInfoPanel/ContentScrollView/Viewport/Content` Transform.
   - Assign `_questStepPrefab` → `QuestStepPrefab.prefab` asset reference.
   - Confirm existing refs are intact: `_titleText`, `_descriptionText`, `_emptyState`.

> **MCP note**: Use `manage_prefabs` or `manage_components` to update the serialized fields. After any direct YAML edit, use `refresh_unity(mode="if_dirty")` — never `force` after raw YAML edits.

---

### Acceptance Criteria

**AC-1: Start part renders as QuestStepPrefab**
- Given a quest `IsStarted` with a non-empty `startPart.entry`, When the quest is selected in the Quest Log, Then a `QuestStepPrefab` instance is spawned in `_contentRoot` with title `"Start"`, no description, and a single `QuestPartPrefab` child showing `startPart.entry`.

**AC-2: Active steps render as QuestStepPrefab**
- Given a quest step where at least one part's fact is true, When the quest is selected, Then a `QuestStepPrefab` instance is spawned with title `"{n}. {step.title}"`, the step description (if non-empty), and one `QuestPartPrefab` per active part.
- Given a step with an empty description, Then `StepDesc` is hidden (`SetActive(false)`).
- Given a step has no active parts, Then no `QuestPartPrefab` children are spawned in `PartsRoot`.

**AC-3: Inactive steps are skipped**
- Given a step where no parts' facts are true, Then no `QuestStepPrefab` is spawned for that step.

**AC-4: Completion footer renders as QuestStepPrefab**
- Given `quest.IsCompleted` is true, When the quest is selected, Then a `QuestStepPrefab` with title `"Completed"` is spawned after the step list, with `QuestPartPrefab` children for each active completed part.

**AC-5: Failure footer renders as QuestStepPrefab**
- Given `quest.IsFailed` is true (and not IsCompleted), When the quest is selected, Then a `QuestStepPrefab` with title `"Failed"` is spawned, with `QuestPartPrefab` children for each active failed part.

**AC-6: Content refreshes on re-selection**
- Given a quest was already shown, When `Show(quest)` is called again (e.g. after quest state changes), Then existing `_contentRoot` children are destroyed and new prefabs are instantiated — no stale content remains.

**AC-7: EmptyState / visibility toggles unchanged**
- Given no quest is selected (Hide called), Then `_emptyState` is active and `_titleText`, `_descriptionText`, `_contentRoot` are hidden — same behaviour as before.
- Given a quest is shown, Then `_emptyState` is hidden and the three elements are active.

**AC-8: No regressions**
- Quest list panel (tab switching, button list) is unaffected.
- UIScreenManager tab switching still opens/closes the Quest Log correctly.

---

## Additional Context

### Dependencies

- `QuestStepPrefab` must exist (Task 4) before `QuestInfoPanelUI` can be wired in the prefab (Task 6).
- `QuestPartPrefab` must exist (Task 3) before `QuestStepPrefab` can be set up (Task 4) — `QuestStepUI._questPartPrefab` is assigned on the prefab.
- `QuestPartUI.cs` and `QuestStepUI.cs` must compile (Tasks 1–2) before prefab components can be added (Tasks 3–4).

### Testing Strategy

- **Manual**: Open Quest Log in Play mode with a quest that has `startPart.fact` true, at least one active step, and an active completion part. Verify three separate `QuestStepPrefab` instances appear in the scroll view content.
- **Manual**: Select a quest with a step whose description is empty — verify `StepDesc` is hidden.
- **Manual**: Trigger quest completion mid-session with the Quest Log open; re-select the quest and verify the `"Completed"` step block appears.
- No automated tests required (UI rendering not covered by EditMode tests in this project's test strategy).

### Notes

- **`_contentText` is fully removed** from `QuestInfoPanelUI` — the field name does not appear in the new script. Any prefab YAML referencing it will produce a missing reference warning on first load; it is cleared automatically once the prefab is resaved via MCP.
- **Step numbering**: the `displayNumber` counter increments only for steps that pass `IsActive()` — same logic as the original `BuildContent`, preserving the existing display numbering behaviour.
- **`QuestStepUI` description guard**: `SetActive(false)` on `StepDesc` when description is null/empty prevents empty TMP_Text from taking up space in the `VerticalLayoutGroup`.
- **Parts with empty `entry`**: `QuestStepUI.Bind` skips parts with `string.IsNullOrEmpty(part.entry)` — same guard as the original code.
