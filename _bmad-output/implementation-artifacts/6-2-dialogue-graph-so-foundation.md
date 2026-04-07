# Story 6.2: Dialogue Graph Node SO Foundation

Status: ready-for-dev

## Story

As a game developer,
I want a ScriptableObject-based dialogue graph foundation (abstract node base + 3 concrete node types + NPCDialogueGraphComponent),
so that NPC dialogue can be authored as branching chains and integrated in Story 6-3.

## Acceptance Criteria

1. **AC1 — Unconditional Start Node:** `GetAvailableStartNodes()` returns a `StartDialogueNode` whose `requiredMemory == null`, regardless of NPC memory state.
2. **AC2 — Memory-gated Start Node (inactive):** `GetAvailableStartNodes()` excludes a `StartDialogueNode` whose `requiredMemory` is NOT in `NPCMemoryComponent.GetActiveMemories()`.
3. **AC3 — Memory-gated Start Node (active):** `GetAvailableStartNodes()` includes a `StartDialogueNode` whose `requiredMemory` IS in `GetActiveMemories()`.
4. **AC4 — Unconditional Choice:** `GetAvailableChoices()` returns a `ChoiceOption` with `requiredMemory == null`.
5. **AC5 — Memory-gated Choice (inactive):** `GetAvailableChoices()` excludes a `ChoiceOption` whose `requiredMemory` is NOT active.
6. **AC6 — Text node end detection:** `TextDialogueNode.IsEndNode()` returns `true` when `nextNode == null`.
7. **AC7 — Choice node end detection:** `ChoiceDialogueNode.IsEndNode()` always returns `false`.
8. **AC8 — Empty list safety:** `GetAvailableStartNodes()` returns empty array when `_startNodes` is null or empty; `GetAvailableChoices()` returns empty array when `choices` is null or empty.
9. **AC9 — Null safety:** Null entries in `_startNodes` and `choices` arrays are silently skipped.
10. **AC10 — Unity Editor:** All four node types appear under `Assets/Create > Game/Dialogue/` menu (except `DialogueNode` abstract, which has no `[CreateAssetMenu]`).

## Tasks / Subtasks

- [ ] Task 1 — Create folder `Assets/_Game/ScriptableObjects/Dialogue/` (AC: #10)
  - [ ] Create it by placing the first `.cs` file in it (no separate gitkeep needed; Unity registers folders on file creation)

- [ ] Task 2 — Create `DialogueNode.cs` abstract base (AC: #6, #7, #10)
  - [ ] File: `Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs`
  - [ ] Namespace: `Game.Dialogue`
  - [ ] `public abstract class DialogueNode : ScriptableObject` — NO `[CreateAssetMenu]`
  - [ ] `public string text` with `[Tooltip]`
  - [ ] `public DialogueNode nextNode` with `[Tooltip]`
  - [ ] `public virtual bool IsEndNode() => nextNode == null`

- [ ] Task 3 — Create `StartDialogueNode.cs` (AC: #1, #2, #3, #10)
  - [ ] File: `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs`
  - [ ] Namespace: `Game.Dialogue`
  - [ ] `[CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]`
  - [ ] `public NPCMemoryEntrySO requiredMemory` — null = always shown
  - [ ] using `Game.NPC` for `NPCMemoryEntrySO`

- [ ] Task 4 — Create `TextDialogueNode.cs` (AC: #6, #10)
  - [ ] File: `Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs`
  - [ ] Namespace: `Game.Dialogue`
  - [ ] `[CreateAssetMenu(menuName = "Game/Dialogue/Text Node", fileName = "Text_")]`
  - [ ] No additional fields — inherits `text`, `nextNode`, `IsEndNode()` from base

- [ ] Task 5 — Create `ChoiceDialogueNode.cs` + `ChoiceOption` (AC: #4, #5, #7, #10)
  - [ ] File: `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs`
  - [ ] Namespace: `Game.Dialogue`
  - [ ] `[Serializable] public class ChoiceOption` IN SAME FILE (not a SO — no GUID concern)
    - [ ] `public string text`
    - [ ] `public NPCMemoryEntrySO requiredMemory`
    - [ ] `public DialogueNode nextNode`
  - [ ] `[CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]`
  - [ ] `public ChoiceOption[] choices`
  - [ ] `public override bool IsEndNode() => false`

- [ ] Task 6 — Create `NPCDialogueGraphComponent.cs` (AC: #1–#9)
  - [ ] File: `Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs`
  - [ ] Namespace: `Game.AI`
  - [ ] `[SerializeField] private List<StartDialogueNode> _startNodes`
  - [ ] `public StartDialogueNode[] GetAvailableStartNodes(NPCMemoryComponent memoryComponent)`
    - [ ] Guard: return empty array if `_startNodes == null || _startNodes.Count == 0`
    - [ ] Get `activeMemories`: if `memoryComponent != null` call `GetActiveMemories()`, else empty array
    - [ ] Filter: include node if `node.requiredMemory == null || Array.IndexOf(activeMemories, node.requiredMemory) >= 0`
    - [ ] Skip null nodes
    - [ ] `GameLog.Info(TAG, $"GetAvailableStartNodes: {result.Count}/{_startNodes.Count} available")`
  - [ ] `public ChoiceOption[] GetAvailableChoices(ChoiceDialogueNode choiceNode, NPCMemoryComponent memoryComponent)`
    - [ ] Guard: return empty array if `choiceNode == null || choices == null || choices.Length == 0`
    - [ ] Same memory filtering pattern as above
    - [ ] Skip null choices
  - [ ] `private const string TAG = "[DialogueGraph]"`

## Dev Notes

### Scope — Story 6-2 is PURE NEW CODE

**Do NOT modify any existing system in this story.** Do not touch `DialogueSystem`, `DialogueUI`, `NPCPresence`, `NPCDialogueRequestData`, or any other existing script. Existing system wiring happens in Story 6-3.

### Exact Code — Use Verbatim from Tech Spec

The tech spec (`_bmad-output/implementation-artifacts/tech-spec-dialogue-graph-node-system.md`) contains complete, reviewed implementations for all 4 types. Implement exactly as specified — no paraphrasing.

#### `DialogueNode.cs` (abstract base)
```csharp
using UnityEngine;

namespace Game.Dialogue
{
    public abstract class DialogueNode : ScriptableObject
    {
        [Header("Content")]
        [Tooltip("Text for this node. StartDialogueNode: topic label. TextDialogueNode: NPC speech. ChoiceDialogueNode: NPC text above choices.")]
        public string text;

        [Header("Navigation")]
        [Tooltip("Next node in the chain. Null = end of dialogue. Not used by ChoiceDialogueNode.")]
        public DialogueNode nextNode;

        public virtual bool IsEndNode() => nextNode == null;
    }
}
```

#### `StartDialogueNode.cs`
```csharp
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]
    public class StartDialogueNode : DialogueNode
    {
        [Header("Unlock Condition")]
        [Tooltip("Memory that must be active for this topic to appear. Null = always shown.")]
        public NPCMemoryEntrySO requiredMemory;
    }
}
```

#### `TextDialogueNode.cs`
```csharp
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Text Node", fileName = "Text_")]
    public class TextDialogueNode : DialogueNode
    {
        // Inherits: text (NPC line), nextNode (next in chain), IsEndNode()
        // nextNode == null → UI shows "Farewell." button; non-null → "Continue..."
    }
}
```

#### `ChoiceDialogueNode.cs` (includes `ChoiceOption` in same file)
```csharp
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
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

    [CreateAssetMenu(menuName = "Game/Dialogue/Choice Node", fileName = "Choice_")]
    public class ChoiceDialogueNode : DialogueNode
    {
        [Header("Choices")]
        [Tooltip("Player choices shown after NPC text. Each choice can be memory-gated.")]
        public ChoiceOption[] choices;

        public override bool IsEndNode() => false;
    }
}
```

#### `NPCDialogueGraphComponent.cs`
```csharp
using System.Collections.Generic;
using Game.Dialogue;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    public class NPCDialogueGraphComponent : MonoBehaviour
    {
        private const string TAG = "[DialogueGraph]";

        [SerializeField] private List<StartDialogueNode> _startNodes;

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

### Critical Patterns from Project Rules

**Separate files for SO subclasses (HIGH PRIORITY)**
`StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode` must each be in their own `.cs` file. Unity's `m_Script` reference breaks on domain reload if SO subclasses share a file.

**`ChoiceOption` in same file as `ChoiceDialogueNode` (exception)**
`ChoiceOption` is `[Serializable]` C# class, not a MonoBehaviour or SO. The rule "separate files" applies to SO subclasses, not plain serializable classes. `ChoiceOption` alongside its owning SO is fine — no `.meta` GUID concern.

**No `[CreateAssetMenu]` on abstract `DialogueNode`**
Unity handles SO polymorphism via serialized `DialogueNode` references. The abstract class must NOT have `[CreateAssetMenu]` — that attribute on an abstract type leads to broken instantiation.

**Namespace rules**
- `DialogueNode`, `StartDialogueNode`, `TextDialogueNode`, `ChoiceDialogueNode` → `Game.Dialogue`
- `NPCDialogueGraphComponent` → `Game.AI` (alongside `NPCMemoryComponent`)

**Logging**
Always `GameLog.Info/Warn/Error(TAG, ...)` — never `Debug.Log`. Define `private const string TAG = "[DialogueGraph]"` in `NPCDialogueGraphComponent`.

**Memory filtering pattern**
`Array.IndexOf(activeMemories, node.requiredMemory) >= 0` — consistent with how story 6-1's existing memory filtering works. Do NOT use LINQ `.Contains()` (avoid LINQ allocations in hot paths per project conventions).

### Project Structure Notes

| New file | Location |
|----------|----------|
| `DialogueNode.cs` | `Assets/_Game/ScriptableObjects/Dialogue/` (new folder) |
| `StartDialogueNode.cs` | `Assets/_Game/ScriptableObjects/Dialogue/` |
| `TextDialogueNode.cs` | `Assets/_Game/ScriptableObjects/Dialogue/` |
| `ChoiceDialogueNode.cs` | `Assets/_Game/ScriptableObjects/Dialogue/` |
| `NPCDialogueGraphComponent.cs` | `Assets/_Game/Scripts/AI/` (alongside `NPCMemoryComponent.cs`) |

### Dependencies / What NOT to Touch

- `DialogueSystem.cs` — Story 6-3 only
- `DialogueUI.cs` — Story 6-3 only
- `NPCPresence.cs` — Story 6-3 only
- `NPCDialogueRequestData.cs` — Story 6-3 only
- `NPCMemoryComponent.cs` — read-only reference (`GetActiveMemories()` is already public)
- `NPCMemoryEntrySO.cs` — reference only (type used in `requiredMemory` fields)

### Existing API to Reuse

| API | File | Usage |
|-----|------|-------|
| `NPCMemoryComponent.GetActiveMemories()` | `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` | Returns `NPCMemoryEntrySO[]` of active memories |
| `NPCMemoryEntrySO` | `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs` | Type for `requiredMemory` fields |
| `GameLog.Info/Warn/Error(TAG, msg)` | `Assets/_Game/Scripts/Core/GameLog.cs` | Project-standard logging |

### Testing

Manual only — no automated tests for pure SO data containers. Verify in Unity Editor:
1. `Assets/Create > Game/Dialogue/` shows three menu items (Start Node, Text Node, Choice Node)
2. Creating a `StartDialogueNode` asset shows `text`, `nextNode`, `requiredMemory` in Inspector
3. Creating a `TextDialogueNode` asset shows `text`, `nextNode` in Inspector
4. Creating a `ChoiceDialogueNode` asset shows `text`, `nextNode`, `choices[]` in Inspector
5. `NPCDialogueGraphComponent` can be added to a GameObject; `_startNodes` list visible

### References

- Tech spec (complete implementations): [Source: _bmad-output/implementation-artifacts/tech-spec-dialogue-graph-node-system.md]
- Story 6-1 (prefab/scene context, existing wiring): [Source: _bmad-output/implementation-artifacts/6-1-npc-topic-dialogue.md]
- `NPCMemoryComponent.GetActiveMemories()`: [Source: Assets/_Game/Scripts/AI/NPCMemoryComponent.cs]
- `NPCMemoryEntrySO`: [Source: Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs]
- Project conventions (57 rules): [Source: _bmad-output/project-context.md]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
