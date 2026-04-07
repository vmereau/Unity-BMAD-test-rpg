# Story 6.3: Dialogue Graph Integration

Status: review

## Story

As a game developer,
I want the dialogue graph nodes from Story 6-2 wired into DialogueSystem, DialogueUI, NPCPresence, and NPCDialogueRequestData,
so that talking to an NPC with a graph component opens a fully traversable dialogue tree.

## Acceptance Criteria

1. **AC1 — Topic list from graph:** NPC with `NPCDialogueGraphComponent` (2 start nodes: 1 unconditional, 1 memory-gated inactive) → dialogue opens showing 1 topic button + "Farewell." only.
2. **AC2 — Memory-gated topic appears when active:** Same NPC with the memory now active → dialogue shows 2 topic buttons + "Farewell."
3. **AC3 — Text node display:** Clicking a start topic whose `nextNode` is a `TextDialogueNode` → response area shows `TextDialogueNode.text`; topic buttons are cleared; single advance button appears.
4. **AC4 — Farewell on end node:** `TextDialogueNode.nextNode == null` → advance button label is "Farewell." and clicking it closes dialogue.
5. **AC5 — Continue on chained text node:** `TextDialogueNode.nextNode` is another `TextDialogueNode` → advance button shows "Continue..." and clicking it shows next text node.
6. **AC6 — Choice node display:** `ChoiceDialogueNode` reached via `AdvanceToNode()` → `ChoiceDialogueNode.text` in response area; filtered choice buttons + "Farewell." shown.
7. **AC7 — Choice null nextNode closes:** Clicking a choice button whose `nextNode` is null → dialogue closes; `CursorManager.Lock()` fires; `IsOpen = false`.
8. **AC8 — No graph = only Farewell:** NPC with no `NPCDialogueGraphComponent` → dialogue opens with 0 topics + "Farewell." — no crash, no null ref.
9. **AC9 — Escape closes:** Pressing Escape closes dialogue regardless of current node state.
10. **AC10 — Demo Villager updated:** `StartingTown_NPC_Villager` has `NPCDialogueGraphComponent` with 3 start nodes linked to text nodes; opening dialogue shows 3 topics that work end-to-end.

## Tasks / Subtasks

- [x] Task 1 — Extend `NPCDialogueRequestData` (AC: #1, #8)
  - [x] File: `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs`
  - [x] Add `public NPCDialogueGraphComponent graph;` field with null-safe comment
  - [x] Add `using Game.AI;` import (already has `using Game.AI;` via `NPCMemoryComponent` — verify no duplicate needed)
  - [x] See exact code in Dev Notes

- [x] Task 2 — Modify `NPCPresence.Interact()` (AC: #1, #8)
  - [x] File: `Assets/_Game/Scripts/AI/NPCPresence.cs`
  - [x] Add `var graphComponent = GetComponent<NPCDialogueGraphComponent>();` after `memComponent` line
  - [x] Add `graph = graphComponent` to event data struct initializer
  - [x] See exact code in Dev Notes

- [x] Task 3 — Refactor `DialogueSystem` (AC: #1–#9)
  - [x] File: `Assets/_Game/Scripts/World/DialogueSystem.cs` — **full rewrite, preserve namespace + tag**
  - [x] Add `using Game.AI;` and `using Game.Dialogue;` imports
  - [x] Add `private NPCMemoryComponent _currentNPCMemory;` and `private NPCDialogueGraphComponent _currentGraph;` fields
  - [x] In `HandleDialogueRequested`: store both state fields, get `startNodes` from graph (empty array if null), call `_dialogueUI.Open(data.npcName, startNodes)` — **new signature**
  - [x] Add `public void AdvanceToNode(DialogueNode node)` with `switch (node)` pattern matching
  - [x] In `Close()`: null out `_currentNPCMemory` and `_currentGraph`
  - [x] Remove old `GetActiveDialogueMemories()` call
  - [x] See exact code in Dev Notes

- [x] Task 4 — Refactor `DialogueUI` (AC: #1–#9)
  - [x] File: `Assets/_Game/Scripts/UI/DialogueUI.cs` — **full rewrite, preserve all `[SerializeField]` fields and input wiring**
  - [x] Change `using Game.NPC;` and `using Game.Core;` → `using Game.Dialogue;` (verify no remaining `NPCMemoryEntrySO` refs)
  - [x] Change `Open(string npcName, NPCMemoryEntrySO[] topics)` → `Open(string npcName, StartDialogueNode[] startNodes)`
  - [x] Rename `PopulateTopics()` → `PopulateStartNodes()` internally
  - [x] Rename `AddTopicButton()` → `AddStartNodeButton()`: button onClick calls `_dialogueSystem.AdvanceToNode(captured.nextNode)`
  - [x] Add `ShowTextNode(TextDialogueNode node)`: clears buttons, sets `_responseText.text = node.text`, calls `AddAdvanceButton(node.nextNode)`
  - [x] Add `ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)`: clears buttons, sets response text, adds choice buttons + Farewell
  - [x] Add `AddAdvanceButton(DialogueNode nextNode)`: label "Continue..." if nextNode not null, "Farewell." if null; onClick → `AdvanceToNode(captured)`
  - [x] Add `AddChoiceButton(ChoiceOption choice)`: label = choice.text; onClick → `AdvanceToNode(captured.nextNode)`
  - [x] Change `AddFarewellButton()` onClick → `_dialogueSystem.AdvanceToNode(null)` (was `_dialogueSystem.Close()`)
  - [x] Keep `ClearTopicButtons()`, `HandleCancel()`, all input lifecycle unchanged
  - [x] See exact code in Dev Notes

- [x] Task 5 — Remove `GetActiveDialogueMemories()` from `NPCMemoryComponent` (AC: clean-up)
  - [x] File: `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`
  - [x] Remove lines 53–62 (the `GetActiveDialogueMemories()` method)
  - [x] `GetActiveMemories()` is kept — still called by `NPCDialogueGraphComponent`
  - [x] Grep for any other callers of `GetActiveDialogueMemories()` before removing — should be zero after Task 3

- [x] Task 6 — Demo: Update Villager NPC (Editor work) (AC: #10)
  - [x] Create folder `Assets/_Game/Data/NPCs/Dialogue/Villager/`
  - [x] Create 6 node assets via `Assets/Create > Game/Dialogue/` (requires Story 6-2 to compile first):

    | Asset filename | Type | `text` field | `nextNode` |
    |---|---|---|---|
    | `Start_Villager_Greetings.asset` | StartDialogueNode | `"Greetings"` | `Text_Villager_Greetings.asset` |
    | `Text_Villager_Greetings.asset` | TextDialogueNode | `"Good day, traveller. These are troubled times."` | null |
    | `Start_Villager_AboutPlace.asset` | StartDialogueNode | `"About this place"` | `Text_Villager_AboutPlace.asset` |
    | `Text_Villager_AboutPlace.asset` | TextDialogueNode | `"This is Alderath, a quiet town — or it was, before the troubles began."` | null |
    | `Start_Villager_Work.asset` | StartDialogueNode | `"What do you do?"` | `Text_Villager_Work.asset` |
    | `Text_Villager_Work.asset` | TextDialogueNode | `"I tend the fields. Not much else left to do around here."` | null |

  - [x] In `StartingTown.unity`, find `StartingTown_NPC_Villager` GameObject
  - [x] Add `NPCDialogueGraphComponent` component to it
  - [x] Assign all 3 Start node assets to `_startNodes` list in the Inspector
  - [x] All 3 `Start_*` nodes have `requiredMemory = null` (unconditional)
  - [x] **DO NOT** remove or modify existing `NPCMemoryComponent` or `Mem_Villager_*.asset` — keep as-is (their `dialogueLines` field becomes unused but harmless)

## Dev Notes

### Dependency — Must Have Story 6-2 Compiled

Story 6-3 depends on `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode`, and `NPCDialogueGraphComponent` from Story 6-2.
**Do not begin Task 3/4 unless the 6-2 types compile without errors.**

### Scope — No Prefab Changes

Story 6-3 does NOT change:
- `Player.prefab` hierarchy
- `UICanvas.prefab` hierarchy
- `DialoguePanel.prefab` wiring
- `TopicButtonPrefab.prefab`
- Any existing scene GO hierarchy other than adding `NPCDialogueGraphComponent` to the Villager

All prefab wiring from Story 6-1 is preserved. `DialogueUI` has the same `[SerializeField]` fields — no prefab re-wiring needed.

### Exact Code — Task 1: `NPCDialogueRequestData.cs`

Current file has: `npcName`, `memories`. Add `graph` field:

```csharp
using Game.AI;

namespace Game.Core
{
    [System.Serializable]
    public struct NPCDialogueRequestData
    {
        public string npcName;
        public NPCMemoryComponent memories;    // null-safe — DialogueSystem guards
        public NPCDialogueGraphComponent graph; // null-safe — null means no graph, show only Farewell
    }
}
```

### Exact Code — Task 2: `NPCPresence.Interact()`

Add `graphComponent` lookup and pass it in event data:

```csharp
public void Interact()
{
    if (_data == null) return;
    if (_onDialogueRequested == null)
    {
        GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
        return;
    }
    var memComponent = GetComponent<NPCMemoryComponent>();
    var graphComponent = GetComponent<NPCDialogueGraphComponent>(); // may be null — handled by DialogueSystem
    _onDialogueRequested.Raise(new NPCDialogueRequestData
    {
        npcName = _data.npcName,
        memories = memComponent,
        graph = graphComponent
    });
}
```

Add `using Game.AI;` is already present in file. No new imports needed.

### Exact Code — Task 3: `DialogueSystem.cs` (full rewrite)

```csharp
using Game.AI;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using Game.UI;
using UnityEngine;

namespace Game.World
{
    public class DialogueSystem : MonoBehaviour
    {
        private const string TAG = "[Dialogue]";

        [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;
        [SerializeField] private DialogueUI _dialogueUI;

        public bool IsOpen { get; private set; }

        private NPCMemoryComponent _currentNPCMemory;
        private NPCDialogueGraphComponent _currentGraph;

        private void OnEnable()
        {
            if (_onDialogueRequested == null)
            {
                GameLog.Warn(TAG, "No dialogue event assigned — DialogueSystem will not respond to NPC interactions");
                return;
            }
            _onDialogueRequested.AddListener(HandleDialogueRequested);
        }

        private void OnDisable()
        {
            if (_onDialogueRequested == null) return;
            _onDialogueRequested.RemoveListener(HandleDialogueRequested);
        }

        private void HandleDialogueRequested(NPCDialogueRequestData data)
        {
            if (_dialogueUI == null)
            {
                GameLog.Error(TAG, "DialogueUI not assigned — cannot open dialogue");
                return;
            }

            _currentNPCMemory = data.memories;
            _currentGraph = data.graph;

            StartDialogueNode[] startNodes = _currentGraph != null
                ? _currentGraph.GetAvailableStartNodes(_currentNPCMemory)
                : System.Array.Empty<StartDialogueNode>();

            _dialogueUI.Open(data.npcName, startNodes);
            IsOpen = true;
            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened dialogue with {data.npcName} — {startNodes.Length} topic(s) available");
        }

        /// <summary>
        /// Advances dialogue to the given node. null = close dialogue.
        /// Called by DialogueUI buttons (start topic, text advance, choice selection).
        /// </summary>
        public void AdvanceToNode(DialogueNode node)
        {
            if (node == null)
            {
                Close();
                return;
            }

            switch (node)
            {
                case TextDialogueNode textNode:
                    _dialogueUI.ShowTextNode(textNode);
                    break;

                case ChoiceDialogueNode choiceNode:
                    ChoiceOption[] availableChoices = _currentGraph != null
                        ? _currentGraph.GetAvailableChoices(choiceNode, _currentNPCMemory)
                        : choiceNode.choices ?? System.Array.Empty<ChoiceOption>();
                    _dialogueUI.ShowChoiceNode(choiceNode, availableChoices);
                    break;

                case StartDialogueNode _:
                    // Author error: StartDialogueNode should not appear mid-chain
                    GameLog.Warn(TAG, $"StartDialogueNode '{node.name}' referenced mid-chain — closing dialogue");
                    Close();
                    break;

                default:
                    GameLog.Warn(TAG, $"Unknown node type '{node.GetType().Name}' — closing dialogue");
                    Close();
                    break;
            }
        }

        public void Close()
        {
            if (_dialogueUI == null) return;
            _dialogueUI.Close();
            IsOpen = false;
            _currentNPCMemory = null;
            _currentGraph = null;
            CursorManager.Lock();
            GameLog.Info(TAG, "Closed dialogue");
        }
    }
}
```

**Note:** `using Game.NPC;` is still needed in DialogueSystem for `NPCMemoryComponent` type.

### Exact Code — Task 4: `DialogueUI.cs` (full rewrite)

```csharp
using Game.Dialogue;
using Game.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public class DialogueUI : MonoBehaviour
    {
        private const string TAG = "[DialogueUI]";

        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _npcNameText;
        [SerializeField] private TMP_Text _responseText;
        [SerializeField] private Transform _topicsContainer;
        [SerializeField] private GameObject _topicButtonPrefab;
        [SerializeField] private DialogueSystem _dialogueSystem;

        private InputSystem_Actions _input;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (_input == null) return;
            _input.UI.Enable();
            _input.UI.Cancel.performed += HandleCancel;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= HandleCancel;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>Opens the dialogue panel showing a list of StartDialogueNode topics.</summary>
        public void Open(string npcName, StartDialogueNode[] startNodes)
        {
            if (_panel == null)
            {
                GameLog.Error(TAG, "Panel not assigned — cannot open dialogue UI");
                return;
            }

            _panel.SetActive(true);

            if (_npcNameText != null)
                _npcNameText.text = npcName;

            if (_responseText != null)
                _responseText.text = string.Empty;

            PopulateStartNodes(startNodes);
            GameLog.Info(TAG, $"DialogueUI opened with {startNodes.Length} topic(s)");
        }

        /// <summary>Displays a TextDialogueNode: shows text and a single advance/farewell button.</summary>
        public void ShowTextNode(TextDialogueNode node)
        {
            ClearTopicButtons();

            if (_responseText != null)
                _responseText.text = node.text;

            AddAdvanceButton(node.nextNode);
        }

        /// <summary>Displays a ChoiceDialogueNode: shows NPC text and the provided (pre-filtered) choices + Farewell.</summary>
        public void ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)
        {
            ClearTopicButtons();

            if (_responseText != null)
                _responseText.text = node.text;

            foreach (var choice in availableChoices)
            {
                if (choice == null) continue;
                AddChoiceButton(choice);
            }

            AddFarewellButton();
        }

        public void Close()
        {
            if (_panel != null)
                _panel.SetActive(false);

            ClearTopicButtons();

            if (_responseText != null)
                _responseText.text = string.Empty;

            GameLog.Info(TAG, "DialogueUI closed");
        }

        // ── Private Helpers ─────────────────────────────────────────────────────

        private void PopulateStartNodes(StartDialogueNode[] startNodes)
        {
            ClearTopicButtons();

            if (_topicsContainer == null || _topicButtonPrefab == null)
            {
                GameLog.Error(TAG, "TopicsContainer or TopicButtonPrefab not assigned");
                return;
            }

            foreach (var node in startNodes)
            {
                if (node == null) continue;
                AddStartNodeButton(node);
            }

            AddFarewellButton();
        }

        private void AddStartNodeButton(StartDialogueNode node)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = node.text;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = node;
                btn.onClick.AddListener(() => _dialogueSystem.AdvanceToNode(captured.nextNode));
            }
        }

        /// <summary>
        /// Adds a single advance button. Label: "Continue..." if nextNode != null, "Farewell." if null.
        /// </summary>
        private void AddAdvanceButton(DialogueNode nextNode)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = nextNode != null ? "Continue..." : "Farewell.";

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = nextNode;
                btn.onClick.AddListener(() => _dialogueSystem.AdvanceToNode(captured));
            }
        }

        private void AddChoiceButton(ChoiceOption choice)
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = choice.text;

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                var captured = choice;
                btn.onClick.AddListener(() => _dialogueSystem.AdvanceToNode(captured.nextNode));
            }
        }

        private void AddFarewellButton()
        {
            var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
            var label = btnGO.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = "Farewell.";

            var btn = btnGO.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => _dialogueSystem.AdvanceToNode(null));
        }

        private void ClearTopicButtons()
        {
            if (_topicsContainer == null) return;
            for (int i = _topicsContainer.childCount - 1; i >= 0; i--)
                Destroy(_topicsContainer.GetChild(i).gameObject);
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_dialogueSystem != null)
                _dialogueSystem.Close();
        }
    }
}
```

**Critical import change:** Remove `using Game.NPC;` and `using Game.Core;` — verify no remaining references to `NPCMemoryEntrySO` or `GameEventSO_NPCDialogueRequest` in `DialogueUI.cs` after rewrite (there should be none).

### Critical Patterns

**`AdvanceToNode(null)` is the uniform close path** — Farewell buttons and null-nextNode advances all call `AdvanceToNode(null)` (not `Close()` directly). `HandleCancel` still calls `_dialogueSystem.Close()` directly — this is intentional (Escape bypasses node state).

**`switch (node)` C# pattern matching** — `AdvanceToNode` uses `case TextDialogueNode textNode:` syntax. No casting needed. Covers StartDialogueNode (author error), unknown types, and null (handled before switch).

**Namespace rules:**
- `DialogueNode`, `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode`, `ChoiceOption` → `Game.Dialogue`
- `NPCDialogueGraphComponent` → `Game.AI`
- `DialogueSystem` → `Game.World`
- `DialogueUI` → `Game.UI`

**Logging** — Always `GameLog.Info/Warn/Error(TAG, ...)`. `DialogueSystem` TAG = `"[Dialogue]"`. `DialogueUI` TAG = `"[DialogueUI]"`.

**OnDisable null guard** — `DialogueUI` already has it (line: `if (_input == null) return;`). Must be preserved in rewrite.

**No singleton** — `DialogueSystem` is a scene-local MonoBehaviour on Player root. Access via `[SerializeField]` references only.

**Do NOT call `Cursor.lockState` directly** — always `CursorManager.Lock()` / `CursorManager.Unlock()`.

### Task 5 — What to Remove from NPCMemoryComponent

Current `NPCMemoryComponent.cs` contains `GetActiveDialogueMemories()` at lines 53–62:

```csharp
public NPCMemoryEntrySO[] GetActiveDialogueMemories()
{
    NPCMemoryEntrySO[] active = GetActiveMemories();
    var result = new List<NPCMemoryEntrySO>(active.Length);
    foreach (var memory in active)
    {
        if (memory.HasDialogue()) result.Add(memory);
    }
    return result.ToArray();
}
```

Remove this entire method. The only caller was `DialogueSystem.HandleDialogueRequested()` — which is being rewritten in Task 3 to no longer call it. `GetActiveMemories()` (lines 40–51) is **kept**.

Before removing, grep `GetActiveDialogueMemories` across the codebase to confirm zero other callers.

### Edge Cases

- `StartDialogueNode.nextNode = null` → clicking topic calls `AdvanceToNode(null)` → closes dialogue immediately. Degenerate but handled.
- `ChoiceDialogueNode` with all choices memory-gated and none active → `ShowChoiceNode` iterates empty array → only "Farewell." shown. Works correctly.
- NPC with `NPCDialogueGraphComponent` but empty `_startNodes` → `GetAvailableStartNodes()` returns empty array → `PopulateStartNodes([])` adds only "Farewell.". Works correctly.
- `ChoiceOption.nextNode = null` → choice button onClick calls `AdvanceToNode(null)` → closes dialogue. Correct per AC7.

### Project Structure Notes

**Files to modify:**

| File | Change |
|------|--------|
| `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs` | Add `graph` field |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Add `graphComponent` GetComponent + pass in event |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Full rewrite — graph state + AdvanceToNode |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | Full rewrite — new Open() signature + node display methods |
| `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` | Remove `GetActiveDialogueMemories()` |

**New assets (Editor-created only — no .cs files):**

| Asset | Folder |
|-------|--------|
| `Start_Villager_Greetings.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |
| `Text_Villager_Greetings.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |
| `Start_Villager_AboutPlace.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |
| `Text_Villager_AboutPlace.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |
| `Start_Villager_Work.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |
| `Text_Villager_Work.asset` | `Assets/_Game/Data/NPCs/Dialogue/Villager/` |

**No new `.cs` files created in this story.** All new types (`StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode`, `NPCDialogueGraphComponent`) come from Story 6-2.

### Testing

Manual playtest in `StartingTown.unity` with `StartingTown_NPC_Villager`:

1. Open dialogue → 3 topic buttons + "Farewell." shown
2. Click "Greetings" → response text shows NPC line; topic buttons cleared; "Farewell." button shown (end node)
3. Click "Farewell." → dialogue closes; cursor locks; `IsOpen = false`
4. Press E again → topics reappear correctly (state resets)
5. Press Escape while dialogue open → dialogue closes
6. NPC without `NPCDialogueGraphComponent` → only "Farewell." shown (no crash)

### References

- Tech spec (complete code for all tasks): `_bmad-output/implementation-artifacts/tech-spec-dialogue-graph-node-system.md` (Story 6-3 section, lines 340–883)
- Story 6-2 (types being integrated): `_bmad-output/implementation-artifacts/6-2-dialogue-graph-so-foundation.md`
- Story 6-1 (prefab/scene wiring context): `_bmad-output/implementation-artifacts/6-1-npc-topic-dialogue.md`
- Current `DialogueSystem.cs`: `Assets/_Game/Scripts/World/DialogueSystem.cs`
- Current `DialogueUI.cs`: `Assets/_Game/Scripts/UI/DialogueUI.cs`
- Current `NPCPresence.cs`: `Assets/_Game/Scripts/AI/NPCPresence.cs`
- Current `NPCMemoryComponent.cs`: `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`
- Current `NPCDialogueRequestData.cs`: `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs`
- Project conventions (57 rules): `_bmad-output/project-context.md`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Added `graph` field to `NPCDialogueRequestData` struct (null-safe, no new imports needed).
- Extended `NPCPresence.Interact()` to do `GetComponent<NPCDialogueGraphComponent>()` and populate the `graph` field.
- Full rewrite of `DialogueSystem`: added `_currentNPCMemory`/`_currentGraph` state, new `AdvanceToNode(DialogueNode)` with C# pattern-matching switch, `GetAvailableStartNodes()` call on open, `Close()` nulls both state fields.
- Full rewrite of `DialogueUI`: signature changed to `Open(string, StartDialogueNode[])`, added `ShowTextNode`, `ShowChoiceNode`, `AddAdvanceButton`, `AddChoiceButton`; all Farewell/advance routes now call `AdvanceToNode(null)`; `HandleCancel` still calls `Close()` directly (intentional Escape bypass).
- Removed `GetActiveDialogueMemories()` from `NPCMemoryComponent` — grepped, zero callers outside the definition.
- Created 6 dialogue node assets in `Assets/_Game/Data/NPCs/Dialogue/Villager/` via Unity MCP `manage_scriptable_object`.
- Added `NPCDialogueGraphComponent` to `StartingTown_NPC_Villager` in `StartingTown.unity` and wired all 3 Start nodes (`requiredMemory = null` — unconditional). Scene saved.
- Zero compile errors after all changes.
- **Post-implementation update:** `DialogueUI.cs` — `using Game.Core;` re-added (required for `GameLog`, was inadvertently omitted in rewrite).
- **Post-implementation update:** `NPCMemoryComponent.cs` — `GetActiveStartDialogNodes()` method added; returns `List<StartDialogueNode>` by iterating active memories and collecting `effects.startdialog` where `HasDialogue()` is true. Added `using Game.Dialogue;` and `using System.Linq;` imports.

### File List

**Modified:**
- `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs`
- `Assets/_Game/Scripts/AI/NPCPresence.cs`
- `Assets/_Game/Scripts/World/DialogueSystem.cs`
- `Assets/_Game/Scripts/UI/DialogueUI.cs` (+ `using Game.Core;` re-added post-implementation)
- `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` (+ `GetActiveStartDialogNodes()` added post-implementation)
- `Assets/_Game/Scenes/StartingTown.unity`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Created:**
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_Greetings.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_Greetings.asset.meta`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_AboutPlace.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_AboutPlace.asset.meta`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_Work.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Text_Villager_Work.asset.meta`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_Greetings.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_Greetings.asset.meta`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_AboutPlace.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_AboutPlace.asset.meta`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_Work.asset`
- `Assets/_Game/Data/NPCs/Dialogue/Villager/Start_Villager_Work.asset.meta`

## Change Log

- 2026-04-07: Implemented all 6 tasks. Wired dialogue graph into DialogueSystem/DialogueUI/NPCPresence. Full rewrites of DialogueSystem and DialogueUI. Removed GetActiveDialogueMemories(). Created Villager dialogue assets and wired NPCDialogueGraphComponent in StartingTown scene. Zero compilation errors. Story moved to review.
- 2026-04-07: Post-implementation — `DialogueUI.cs`: `using Game.Core;` re-added. `NPCMemoryComponent.cs`: added `GetActiveStartDialogNodes()` returning `List<StartDialogueNode>` from active memory effects; added `using Game.Dialogue;` and `using System.Linq;`.
