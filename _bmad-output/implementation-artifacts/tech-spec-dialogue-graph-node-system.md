---
title: 'Dialogue Graph Node System'
slug: 'dialogue-graph-node-system'
created: '2026-04-07'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C#', 'ScriptableObjects', 'GameEventSO<T>']
files_to_modify:
  - Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs
  - Assets/_Game/Scripts/AI/NPCPresence.cs
  - Assets/_Game/Scripts/AI/NPCMemoryComponent.cs
  - Assets/_Game/Scripts/World/DialogueSystem.cs
  - Assets/_Game/Scripts/UI/DialogueUI.cs
files_to_create:
  - Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs
  - Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs
  - Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs
  - Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs
  - Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs
code_patterns:
  - 'Abstract ScriptableObject base with concrete SO subclasses (no CreateAssetMenu on abstract)'
  - 'NPCDialogueGraphComponent filters StartDialogueNodes/choices by NPCMemoryComponent active set'
  - 'DialogueSystem.AdvanceToNode() dispatches by node type using pattern matching'
  - 'DialogueUI renders three distinct panel states: topic-list, text-node, choice-node'
test_patterns:
  - 'Manual playtest only — no automated tests for graph traversal (MonoBehaviour-heavy)'
---

# Tech-Spec: Dialogue Graph Node System

**Created:** 2026-04-07

---

## Overview

### Problem Statement

The current dialogue system (`DialogueSystem` + `DialogueUI`) supports only a flat topic list where clicking a topic shows a single `dialogueLines[0]` from `NPCMemoryEntrySO`. There is no support for multi-step NPC conversations, player choice branching, or memory-gated dialogue paths. Epic 6 stories 6-2 and 6-3 require conditional topic display and dialogue actions — both of which demand a proper node graph.

### Solution

Introduce a ScriptableObject-based dialogue graph with an abstract `DialogueNode` base and three concrete types: `StartDialogueNode` (entry points shown as topic list), `TextDialogueNode` (single NPC line → advance), and `ChoiceDialogueNode` (NPC text + player choices, memory-gated). A new `NPCDialogueGraphComponent` holds an NPC's start nodes and filters them against `NPCMemoryComponent.GetActiveMemories()`. `DialogueSystem` gains graph traversal via `AdvanceToNode()`. `DialogueUI` gains three rendering states. This replaces the current flat `dialogueLines[0]` topic display.

### Scope

**In Scope:**
- Abstract `DialogueNode` ScriptableObject base: `text`, `nextNode`, `IsEndNode()`
- `StartDialogueNode : DialogueNode` — topic label + optional `requiredMemory` for conditional display
- `TextDialogueNode : DialogueNode` — single NPC speech line, "Continue…"/"Farewell." advance button
- `ChoiceDialogueNode : DialogueNode` — NPC text above, `ChoiceOption[]` (each with optional `requiredMemory`, own `nextNode`); always appends "Farewell."
- `ChoiceOption` serializable class (lives in `ChoiceDialogueNode.cs` — not a SO, no separate file needed)
- `NPCDialogueGraphComponent` MonoBehaviour — `List<StartDialogueNode>`, `GetAvailableStartNodes()`, `GetAvailableChoices()`
- Extend `NPCDialogueRequestData` with `NPCDialogueGraphComponent graph` field
- Extend `NPCPresence.Interact()` to get graph component via `GetComponent`
- Refactor `DialogueSystem`: track `_currentNPCMemory` + `_currentGraph`, add `AdvanceToNode()`
- Refactor `DialogueUI.Open()` signature → `StartDialogueNode[]`, add `ShowTextNode()` + `ShowChoiceNode()`
- Remove `NPCMemoryComponent.GetActiveDialogueMemories()` (replaced by graph flow)
- New folder `Assets/_Game/ScriptableObjects/Dialogue/`
- Demo: update StartingTown Villager NPC with `NPCDialogueGraphComponent` + authored `StartDialogueNode` → `TextDialogueNode` chain (replacing old `dialogueLines`-based memories)

**Out of Scope:**
- Custom visual node graph editor (Unity 6 inspector handles SO references adequately)
- Voice acting / audio hooks on nodes
- Dialogue-triggered animations
- Quest integration via `effects.questDialogueKey` (field stays on `NPCMemoryEntrySO`, unused by graph for now)
- Save/restore of dialogue traversal state within a session
- Looping dialogue chains (spec assumes DAG — no cycles)
- Migrating all existing `NPCMemoryEntrySO` assets to graph format (only the demo Villager is migrated)

---

## Context for Development

### Codebase Patterns

- **Abstract SO base:** Declare `public abstract class DialogueNode : ScriptableObject` — no `[CreateAssetMenu]` on the abstract class. Each concrete subclass gets its own `[CreateAssetMenu]`. Unity handles SO polymorphism cleanly via serialized references of the abstract type.
- **`ChoiceOption` in same file as `ChoiceDialogueNode`:** `ChoiceOption` is `[Serializable]` C# class, not a MonoBehaviour or SO. The project memory rule ("separate files") applies to SO subclasses sharing a file with their data type. A plain `[Serializable]` class alongside its owning SO is fine — no `.meta` GUID conflict.
- **Separate files for SO subclasses:** `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode` must each be in their own `.cs` file. Unity's `m_Script` reference breaks on domain reload if SO subclasses share a file.
- **Memory filtering pattern:** `NPCDialogueGraphComponent.GetAvailableStartNodes()` calls `NPCMemoryComponent.GetActiveMemories()` to get the NPC's active `NPCMemoryEntrySO[]`, then checks `node.requiredMemory == null || Array.IndexOf(activeMemories, node.requiredMemory) >= 0`. Same pattern for choice filtering.
- **`AdvanceToNode()` dispatch:** Use `switch (node)` C# pattern matching (`case TextDialogueNode t:`, etc.) — cleaner than `is` chains, no casting needed.
- **Namespace:** New SO types → `Game.Dialogue`. `NPCDialogueGraphComponent` → `Game.AI` (alongside `NPCMemoryComponent`).
- **Logging:** Always `GameLog.Info/Warn/Error(TAG, ...)` — never `Debug.Log`.
- **No singleton:** `DialogueSystem` remains a scene-local MonoBehaviour, not a singleton.
- **OnDisable null guard:** `DialogueUI` initializes `_input` in `Awake`. The existing null guard in `OnDisable` is already present — preserve it.

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Modify: add graph traversal, `AdvanceToNode()`, `_currentNPCMemory/_currentGraph` state |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | Modify: change `Open()` signature, add `ShowTextNode()`/`ShowChoiceNode()` |
| `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` | Modify: remove `GetActiveDialogueMemories()`; keep `GetActiveMemories()` |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Modify: `Interact()` gets `NPCDialogueGraphComponent` via `GetComponent` |
| `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs` | Modify: add `NPCDialogueGraphComponent graph` field |
| `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs` | Reference: `IsActive()`, `memoryId` — node unlock conditions reference this type |
| `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs` | Reference only — nodes do NOT use string-key conditions (they use SO references) |
| `Assets/_Game/Scripts/UI/UIScreenManager.cs` | Reference: input/cursor pattern for `DialogueUI` — do not change |
| `_bmad-output/implementation-artifacts/6-1-npc-topic-dialogue.md` | Reference: existing wiring (prefabs, scene placement, prefab overrides) — Story B does NOT change prefab hierarchy |

### Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Node storage | Separate `.asset` per node | Cross-referenceable; follows project SO convention; Unity's inspector handles SO polymorphism |
| Memory unlock mechanism | Direct `NPCMemoryEntrySO` reference on node | Scoped to THIS NPC's active memory set (not global world facts); simple author UX; consistent with graph's memory-component-centric design |
| `ChoiceOption` file placement | Same file as `ChoiceDialogueNode` | Plain `[Serializable]` class — no `m_Script` concern; avoids file clutter for a tightly coupled type |
| `ChoiceDialogueNode.nextNode` | Always null; overrides `IsEndNode() → false` | Choice nodes branch via `ChoiceOption.nextNode`; base `nextNode` is meaningless here; override prevents false "end" detection |
| `TextDialogueNode.nextNode == null` | Button label becomes "Farewell." | Zero authoring overhead for chain endings; visual clarity to player |
| `NPCDialogueGraphComponent` separate from `NPCMemoryComponent` | New component | Separation of concerns: memory system manages facts; graph component manages dialogue structure; NPCs without graphs still work |
| `GetAvailableChoices()` on `NPCDialogueGraphComponent` | Graph component owns filtering | Graph component already has the filtering logic for start nodes; reusing it for choices keeps the pattern in one place |
| `AdvanceToNode(null)` | Calls `Close()` | Uniform end-of-chain handling regardless of which node type last pointed to null |

---

## Implementation Plan

### Story 6-2 (revised): Dialogue Graph Node SO Foundation

**Goal:** All new ScriptableObjects and `NPCDialogueGraphComponent` — no changes to existing systems yet. Can be reviewed in isolation.

---

#### Task 1 — Create `Assets/_Game/ScriptableObjects/Dialogue/` folder

Register with Unity asset database. Create a placeholder `.gitkeep` or just create the first file in the folder directly.

---

#### Task 2 — Create `DialogueNode.cs` (abstract base)

**File:** `Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs`
**Namespace:** `Game.Dialogue`

```csharp
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Abstract base for all dialogue graph nodes. Concrete types:
    /// StartDialogueNode, TextDialogueNode, ChoiceDialogueNode.
    /// </summary>
    public abstract class DialogueNode : ScriptableObject
    {
        [Header("Content")]
        [Tooltip("Text for this node. StartDialogueNode: topic label. TextDialogueNode: NPC speech. ChoiceDialogueNode: NPC text above choices.")]
        public string text;

        [Header("Navigation")]
        [Tooltip("Next node in the chain. Null = end of dialogue (shows Farewell button). Not used by ChoiceDialogueNode (choices define navigation).")]
        public DialogueNode nextNode;

        /// <summary>Returns true when nextNode is null (no continuation).</summary>
        public virtual bool IsEndNode() => nextNode == null;
    }
}
```

---

#### Task 3 — Create `StartDialogueNode.cs`

**File:** `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs`
**Namespace:** `Game.Dialogue`

```csharp
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// Entry point for a dialogue chain. Appears as a topic button in the opening topic list.
    /// Shown unconditionally when requiredMemory is null; shown only when the memory is active otherwise.
    /// nextNode points to the first node of the chain (TextDialogueNode or ChoiceDialogueNode).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]
    public class StartDialogueNode : DialogueNode
    {
        [Header("Unlock Condition")]
        [Tooltip("Memory that must be active (IsActive() == true) for this topic to appear. Null = always shown.")]
        public NPCMemoryEntrySO requiredMemory;
    }
}
```

---

#### Task 4 — Create `TextDialogueNode.cs`

**File:** `Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs`
**Namespace:** `Game.Dialogue`

```csharp
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// A single line of NPC speech. Displays text and a "Continue..." / "Farewell." button.
    /// nextNode = null → button shows "Farewell." and closes dialogue on click.
    /// nextNode = another node → button shows "Continue..." and advances the chain.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Text Node", fileName = "Text_")]
    public class TextDialogueNode : DialogueNode
    {
        // Inherits: text (NPC line), nextNode (next in chain), IsEndNode()
        // No additional fields. Future: speakerName override if multi-NPC dialogue is needed.
    }
}
```

---

#### Task 5 — Create `ChoiceDialogueNode.cs` (includes `ChoiceOption`)

**File:** `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs`
**Namespace:** `Game.Dialogue`

```csharp
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    /// <summary>
    /// A single serializable player choice within a ChoiceDialogueNode.
    /// </summary>
    [System.Serializable]
    public class ChoiceOption
    {
        [Tooltip("Text shown on the choice button (player's voice).")]
        public string text;

        [Tooltip("Memory that must be active for this choice to appear. Null = always shown.")]
        public NPCMemoryEntrySO requiredMemory;

        [Tooltip("Node to advance to when this choice is selected. Null = close dialogue.")]
        public DialogueNode nextNode;
    }

    /// <summary>
    /// Displays NPC text above a set of player choices. Choices are memory-gated.
    /// A "Farewell." button is always appended by DialogueUI.
    /// nextNode from base is intentionally unused — choices define navigation.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]
    public class ChoiceDialogueNode : DialogueNode
    {
        [Header("Choices")]
        [Tooltip("Player choices shown after NPC text. Each choice can be memory-gated.")]
        public ChoiceOption[] choices;

        /// <summary>
        /// Choice nodes are never terminal — choices (or Farewell) determine flow.
        /// </summary>
        public override bool IsEndNode() => false;
    }
}
```

---

#### Task 6 — Create `NPCDialogueGraphComponent.cs`

**File:** `Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs`
**Namespace:** `Game.AI`

```csharp
using System.Collections.Generic;
using Game.Dialogue;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Holds an NPC's dialogue graph entry points and filters them against active memories.
    /// Attach alongside NPCMemoryComponent and NPCPresence on NPC prefabs.
    /// </summary>
    public class NPCDialogueGraphComponent : MonoBehaviour
    {
        private const string TAG = "[DialogueGraph]";

        [SerializeField] private List<StartDialogueNode> _startNodes;

        /// <summary>
        /// Returns start nodes available given the NPC's currently active memories.
        /// A node is available when requiredMemory is null OR the memory is in the NPC's active set.
        /// </summary>
        public StartDialogueNode[] GetAvailableStartNodes(NPCMemoryComponent memoryComponent)
        {
            if (_startNodes == null || _startNodes.Count == 0)
                return System.Array.Empty<StartDialogueNode>();

            NPCMemoryEntrySO[] activeMemories = memoryComponent != null
                ? memoryComponent.GetActiveMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<StartDialogueNode>(_startNodes.Count);
            foreach (var node in _startNodes)
            {
                if (node == null) continue;
                if (node.requiredMemory == null || System.Array.IndexOf(activeMemories, node.requiredMemory) >= 0)
                    result.Add(node);
            }

            GameLog.Info(TAG, $"GetAvailableStartNodes: {result.Count}/{_startNodes.Count} available");
            return result.ToArray();
        }

        /// <summary>
        /// Returns the available choices from a ChoiceDialogueNode given the NPC's active memories.
        /// </summary>
        public ChoiceOption[] GetAvailableChoices(ChoiceDialogueNode choiceNode, NPCMemoryComponent memoryComponent)
        {
            if (choiceNode == null || choiceNode.choices == null || choiceNode.choices.Length == 0)
                return System.Array.Empty<ChoiceOption>();

            NPCMemoryEntrySO[] activeMemories = memoryComponent != null
                ? memoryComponent.GetActiveMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<ChoiceOption>(choiceNode.choices.Length);
            foreach (var choice in choiceNode.choices)
            {
                if (choice == null) continue;
                if (choice.requiredMemory == null || System.Array.IndexOf(activeMemories, choice.requiredMemory) >= 0)
                    result.Add(choice);
            }
            return result.ToArray();
        }
    }
}
```

---

### Story 6-3 (revised): Wire Graph into DialogueSystem + DialogueUI

**Goal:** Integrate the graph into all existing systems. After this story, talking to an NPC with a graph component opens a fully traversable dialogue tree.
**Depends on:** Story 6-2 complete.

---

#### Task 1 — Extend `NPCDialogueRequestData`

**File:** `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs`

Add `NPCDialogueGraphComponent graph` field:

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

---

#### Task 2 — Modify `NPCPresence.Interact()`

**File:** `Assets/_Game/Scripts/AI/NPCPresence.cs`

Add `GetComponent<NPCDialogueGraphComponent>()` alongside existing memory component lookup:

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

---

#### Task 3 — Refactor `DialogueSystem`

**File:** `Assets/_Game/Scripts/World/DialogueSystem.cs`

Full rewrite (preserving existing using statements + namespace):

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

---

#### Task 4 — Refactor `DialogueUI`

**File:** `Assets/_Game/Scripts/UI/DialogueUI.cs`

Full rewrite preserving all `[SerializeField]` fields, prefab references, input wiring, and canvas hierarchy (no prefab changes needed). Only logic changes:

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
        /// Adds a single button that advances to nextNode.
        /// Label: "Continue..." if nextNode is not null, "Farewell." if null.
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

**Note:** `using Game.NPC;` and `using Game.Core;` are removed (no longer needed in DialogueUI). `using Game.Dialogue;` added.

---

#### Task 5 — Remove `GetActiveDialogueMemories()` from `NPCMemoryComponent`

**File:** `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`

Remove the `GetActiveDialogueMemories()` method (lines 53–62 of current file). `GetActiveMemories()` is retained — it is called by `NPCDialogueGraphComponent`. The `using Game.NPC;` import on `NPCMemoryComponent` remains (needed for `NPCMemoryEntrySO`).

---

#### Task 6 — Demo: Update Villager NPC (Editor work)

Replace the flat `NPCMemoryEntrySO` topic flow on `StartingTown_NPC_Villager` with a graph:

**6a. Create start + text node assets** in `Assets/_Game/Data/NPCs/Dialogue/Villager/`:

| Asset | Type | `text` | `nextNode` | `requiredMemory` |
|-------|------|--------|------------|------------------|
| `Start_Villager_Greetings.asset` | StartDialogueNode | `"Greetings"` | `Text_Villager_Greetings.asset` | null |
| `Text_Villager_Greetings.asset` | TextDialogueNode | `"Good day, traveller. These are troubled times."` | null | — |
| `Start_Villager_AboutPlace.asset` | StartDialogueNode | `"About this place"` | `Text_Villager_AboutPlace.asset` | null |
| `Text_Villager_AboutPlace.asset` | TextDialogueNode | `"This is Alderath, a quiet town — or it was, before the troubles began."` | null | — |
| `Start_Villager_Work.asset` | StartDialogueNode | `"What do you do?"` | `Text_Villager_Work.asset` | null |
| `Text_Villager_Work.asset` | TextDialogueNode | `"I tend the fields. Not much else left to do around here."` | null | — |

**6b. Add `NPCDialogueGraphComponent`** to `StartingTown_NPC_Villager` in `StartingTown.unity`:
- Add component
- Assign 3 start nodes to `_startNodes` list

**6c. Existing `NPCMemoryEntrySO` assets** (`Mem_Villager_*.asset`): keep as-is (they are still used by `NPCMemoryComponent` for `GetActiveMemories()`; their `dialogueLines` field becomes unused but harmless).

---

## Acceptance Criteria

### Story 6-2 ACs (SO Foundation)

**Given** `StartDialogueNode` with `requiredMemory = null`,
**When** `NPCDialogueGraphComponent.GetAvailableStartNodes(memComp)` is called,
**Then** the node appears in results regardless of NPC memory state.

**Given** `StartDialogueNode` with `requiredMemory = MemX`, and `MemX` is NOT in `NPCMemoryComponent._memories` active set,
**When** `GetAvailableStartNodes()` is called,
**Then** the node is NOT in results.

**Given** `StartDialogueNode` with `requiredMemory = MemX`, and `MemX` IS active (in NPC's `GetActiveMemories()`),
**When** `GetAvailableStartNodes()` is called,
**Then** the node IS in results.

**Given** `ChoiceDialogueNode` with a choice where `requiredMemory = null`,
**When** `GetAvailableChoices()` is called,
**Then** the choice always appears.

**Given** `ChoiceDialogueNode` with a choice where `requiredMemory = MemX` and `MemX` is NOT active,
**When** `GetAvailableChoices()` is called,
**Then** the choice is hidden.

**Given** `TextDialogueNode` with `nextNode = null`,
**When** `IsEndNode()` is called,
**Then** it returns `true`.

**Given** `ChoiceDialogueNode`,
**When** `IsEndNode()` is called,
**Then** it returns `false` regardless of `nextNode` value.

---

### Story 6-3 ACs (System Integration)

**Given** NPC has `NPCDialogueGraphComponent` with 2 `StartDialogueNode`s (1 unconditional, 1 `requiredMemory`-locked),
**And** the required memory is NOT active,
**When** player presses E on the NPC,
**Then** dialogue opens showing 1 topic button + "Farewell."

**Given** the required memory IS active,
**When** player presses E on the NPC,
**Then** dialogue opens showing 2 topic buttons + "Farewell."

**Given** player clicks a start topic whose `nextNode` is a `TextDialogueNode`,
**When** the button is clicked,
**Then** the response text area shows `TextDialogueNode.text` and a single "Continue..." or "Farewell." button appears (topic buttons are cleared).

**Given** `TextDialogueNode.nextNode == null`,
**When** rendered,
**Then** the advance button label is "Farewell." and clicking it closes the dialogue.

**Given** `TextDialogueNode.nextNode` is another `TextDialogueNode`,
**When** player clicks "Continue...",
**Then** the next text node is displayed (chained correctly).

**Given** a `ChoiceDialogueNode` is the `nextNode` of a `TextDialogueNode`,
**When** player advances past the text node,
**Then** `ChoiceDialogueNode.text` appears in the response area and filtered choice buttons + "Farewell." appear.

**Given** player clicks a choice button whose `nextNode` is null,
**When** clicked,
**Then** dialogue closes (`CursorManager.Lock()` fires, `IsOpen` becomes false).

**Given** NPC has no `NPCDialogueGraphComponent` (graph is null in event data),
**When** player interacts,
**Then** dialogue opens with 0 topic buttons + "Farewell." only — no crash, no null ref.

**Given** dialogue is open and player presses Escape,
**Then** dialogue closes regardless of current node state.

---

## Additional Context

### Dependencies

- Story 6-2 has no dependencies (pure new code).
- Story 6-3 depends on Story 6-2 (`StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode`, `NPCDialogueGraphComponent` must compile).
- Story 6-3 does NOT change: `Player.prefab` hierarchy, `UICanvas.prefab` hierarchy, `DialoguePanel.prefab` wiring, or `TopicButtonPrefab.prefab`. All prefab structure from Story 6-1 is preserved.
- `using Game.NPC;` must be removed from `DialogueUI.cs` after refactor (verify no remaining references to `NPCMemoryEntrySO` in that file).

### Testing Strategy

- **Story 6-2:** Manual — create a `StartDialogueNode`, `TextDialogueNode`, and `ChoiceDialogueNode` asset in Unity Editor; confirm Inspector shows correct fields. No automated tests (pure SO data containers).
- **Story 6-3:** Manual playtest in `StartingTown.unity` with the demo Villager. Test: (a) open dialogue → 3 topics + Farewell, (b) click topic → text node → Farewell closes, (c) Escape closes at any point. For memory-gated nodes: verify a `StartDialogueNode` with a `requiredMemory` that is active/inactive appears/disappears correctly.

### New Asset Folder Convention

- Dialogue node assets: `Assets/_Game/Data/NPCs/Dialogue/{NpcName}/`
- Naming: `Start_{NpcName}_{TopicSlug}.asset`, `Text_{NpcName}_{NodeSlug}.asset`, `Choice_{NpcName}_{NodeSlug}.asset`

### Edge Cases

- `StartDialogueNode.nextNode = null` → clicking the topic immediately closes dialogue (degenerate, but handled gracefully by `AdvanceToNode(null)`)
- `StartDialogueNode.nextNode = another StartDialogueNode` — author error; `DialogueSystem.AdvanceToNode()` logs a warning and closes
- `ChoiceDialogueNode` with all choices memory-gated and none active → only "Farewell." button shown (handled by `ShowChoiceNode` iterating an empty array then calling `AddFarewellButton`)
- NPC with `NPCDialogueGraphComponent` but empty `_startNodes` list → `GetAvailableStartNodes()` returns empty array → dialogue shows only "Farewell."

### Sprint Status Updates

After Story 6-2 is complete, update `sprint-status.yaml`:
```yaml
6-2-dialogue-graph-so-foundation: done
```

After Story 6-3 is complete:
```yaml
6-3-dialogue-graph-integration: done
```

Rename/replace the old backlog entries:
```yaml
# 6-2-conditional-dialogue-topics: superseded by dialogue-graph-so-foundation
# 6-3-dialogue-actions: renamed to 6-3-dialogue-graph-integration
```
