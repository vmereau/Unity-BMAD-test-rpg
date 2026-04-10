---
title: 'Dialogue Played Tracking — One-shot vs Repeatable Topics'
slug: 'dialogue-played-tracking'
created: '2026-04-07'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6]
tech_stack: ['Unity 6', 'C#', 'ScriptableObjects']
files_to_modify:
  - Assets/_Game/Scripts/Core/WorldFactPrefix.cs
  - Assets/_Game/Scripts/Core/WorldStateManager.cs
  - Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs
  - Assets/_Game/Scripts/AI/NPCMemoryComponent.cs
  - Assets/_Game/Scripts/World/DialogueSystem.cs
  - Assets/_Game/Scripts/UI/DialogueUI.cs
code_patterns:
  - 'WorldStateManager typed-API pattern (SetQuestStep / SetWorldEvent)'
  - 'StartDialogueNode SO flag drives filter in NPCMemoryComponent'
  - 'DialogueSystem tracks active start node; UI calls back on chain-end'
test_patterns:
  - 'PlayMode: play a one-shot topic → reopen NPC → topic absent'
  - 'PlayMode: play a repeatable topic → reopen NPC → topic present'
  - 'PlayMode: close mid-dialogue without finishing → topic still present'
---

# Tech-Spec: Dialogue Played Tracking — One-shot vs Repeatable Topics

**Created:** 2026-04-07

---

## Overview

### Problem Statement

All `StartDialogueNode` topics are shown to the player whenever their associated `NPCMemoryEntrySO` is active. There is no mechanism to mark a topic as "one-shot" (show once, then hide permanently) versus "repeatable" (always available while memory is active). This means important story moments, quest-kick-off lines, and one-time reactions can be re-triggered indefinitely.

### Solution

Add a `bool isRepeatable` flag to `StartDialogueNode`. When a dialogue chain **completes** (the player clicks through to the last node — one with no `nextNode`), `DialogueSystem` writes the fact `Dialogue.Played.{nodeName} = true` into `WorldStateManager`. `NPCMemoryComponent.GetActiveStartDialogNodes()` skips any non-repeatable node whose played fact is already set. Because `WorldStateManager._worldFacts` is already snapshotted by `GetSaveData()`, persistence is free when Epic 8 save/load is wired.

### Scope

**In Scope:**
- `bool isRepeatable` field on `StartDialogueNode` (default `true` — fully backward-compatible)
- New `Dialogue` entry in `WorldFactPrefix` enum
- Two typed API methods on `WorldStateManager`: `SetDialoguePlayed(string nodeId)` and `IsDialoguePlayed(string nodeId)`
- Filtering of played one-shot nodes in `NPCMemoryComponent.GetActiveStartDialogNodes()`
- `DialogueSystem.StartTopic(StartDialogueNode)` — replaces the direct `AdvanceToNode(nextNode)` call from the UI, tracks the active start node
- `DialogueSystem.NotifyTopicCompleted()` — called by `DialogueUI` when a chain ends; marks the node as played if non-repeatable
- `DialogueUI` call-site updates: topic buttons call `StartTopic`, chain-end branches call `NotifyTopicCompleted` before `RestoreTopics`

**Out of Scope:**
- Save-to-disk implementation (Epic 8 owns that; `GetSaveData()` already captures `_worldFacts`)
- Choice-level played tracking (only `StartDialogueNode` granularity)
- Per-NPC tracking (global by design — NPCs do not share `StartDialogueNode` assets)
- Node ID migration tooling (renaming a SO asset breaks save compatibility; enforced by naming convention)

---

## Context for Development

### Codebase Patterns

- **WorldStateManager typed API pattern:** Never write raw key strings at call sites. All world fact domains have a prefix in `WorldFactPrefix` and a dedicated setter/getter on `WorldStateManager`. This spec follows that exact pattern.
- **Key format:** `{prefix}.{domain}.{id}` — for this feature: `Dialogue.Played.{startNode.name}` where `name` is the SO asset name (set by `ScriptableObject.name`).
- **`StartDialogueNode.name` as stable ID:** The SO asset filename is the runtime identifier. **Never rename a `StartDialogueNode` asset** after it has been played in a save file — existing saves will lose the played record for that dialogue. This is a designer constraint, not enforced in code.
- **`NPCMemoryComponent` filters on demand:** `GetActiveStartDialogNodes()` already filters by memory active state; the played-state filter is additive — same loop, one extra condition.
- **`DialogueUI` currently bypasses `DialogueSystem` at chain-end:** When `_pendingNextNode == null` (Text node end) or `choice.nextNode == null` (Choice end), `DialogueUI` calls `RestoreTopics()` directly without going through `DialogueSystem`. The two call sites in `DialogueUI` that trigger `RestoreTopics()` must each call `_dialogueSystem.NotifyTopicCompleted()` first.
- **Assembly:** All files are in `Game` assembly (`Assets/_Game/Game.asmdef`). No new assembly references needed.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Core/WorldFactPrefix.cs` | Add `Dialogue` entry to enum |
| `Assets/_Game/Scripts/Core/WorldStateManager.cs` | Add `SetDialoguePlayed` / `IsDialoguePlayed` typed methods |
| `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs` | Add `isRepeatable` serialized field |
| `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` | Filter played one-shot nodes in `GetActiveStartDialogNodes()` |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Add `_currentStartNode`, `StartTopic()`, `NotifyTopicCompleted()`, clear on `Close()` |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | `AddStartNodeButton` → call `StartTopic`; two chain-end paths → call `NotifyTopicCompleted` |

### Technical Decisions

- **Key identifier is `ScriptableObject.name`** (the asset filename), not a separate `_id` field. Rationale: no extra designer friction, consistent with how `WorldStateManager` uses string keys elsewhere. Trade-off: renaming the SO asset breaks save compatibility. Document as naming convention.
- **`isRepeatable` defaults to `true`** so all existing `StartDialogueNode` assets in the project continue to behave as today — zero migration needed.
- **`DialogueSystem` owns played-state writing** (not `DialogueUI` or `NPCMemoryComponent`). It already holds `_currentNPCMemory` and `_currentGraph`, making it the right place to accumulate dialogue session state.
- **`DialogueUI` calls `NotifyTopicCompleted()` at chain-end** (not `DialogueSystem.AdvanceToNode(null)`). The existing null-node shortcut paths in `OnPointerClick` and `AddChoiceButton` are the exact locations where chain-end is currently detected; calling `NotifyTopicCompleted()` there is the smallest change.
- **Persistence is zero-cost:** `WorldStateManager._worldFacts` is already snapshotted in `GetSaveData()`. New `Dialogue.Played.*` keys appear naturally in the snapshot. No changes needed to save serialization until Epic 8.

---

## Implementation Plan

### Tasks

Tasks are ordered lowest-dependency first. Each task is independently compilable before the next.

---

**Task 1 — Add `Dialogue` prefix to `WorldFactPrefix`**

File: `Assets/_Game/Scripts/Core/WorldFactPrefix.cs`

Add `Dialogue` to the enum:

```csharp
public enum WorldFactPrefix
{
    Killed,
    Quest,
    World,
    Dialogue   // NEW
}
```

---

**Task 2 — Add typed dialogue-played API to `WorldStateManager`**

File: `Assets/_Game/Scripts/Core/WorldStateManager.cs`

Add two methods in the "Typed write APIs" region (after `SetWorldEvent`):

```csharp
/// <summary>Formats <c>Dialogue.Played.{nodeId}</c> and marks the dialogue as played.</summary>
public void SetDialoguePlayed(string nodeId)
{
    SetFact($"{WorldFactPrefix.Dialogue}.Played.{nodeId}", true);
}

/// <summary>Returns true if the dialogue node with <paramref name="nodeId"/> has been played.</summary>
public bool IsDialoguePlayed(string nodeId)
{
    return GetFact($"{WorldFactPrefix.Dialogue}.Played.{nodeId}");
}
```

No other changes to `WorldStateManager`.

---

**Task 3 — Add `isRepeatable` to `StartDialogueNode`**

File: `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs`

```csharp
using Game.NPC;
using UnityEngine;

namespace Game.Dialogue
{
    [CreateAssetMenu(menuName = "Game/Dialogue/Start Node", fileName = "Start_")]
    public class StartDialogueNode : DialogueNode
    {
        [Tooltip("If false, this topic is hidden after it has been played once (chain reached an end node). Default true.")]
        public bool isRepeatable = true;
    }
}
```

---

**Task 4 — Filter played one-shot nodes in `NPCMemoryComponent`**

File: `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`

Modify `GetActiveStartDialogNodes()` only. Replace the current body:

```csharp
public List<StartDialogueNode> GetActiveStartDialogNodes()
{
    NPCMemoryEntrySO[] activeMemories = GetActiveMemories();

    List<StartDialogueNode> result = new List<StartDialogueNode>(activeMemories.Length);
    foreach (NPCMemoryEntrySO npcMemoryEntrySo in activeMemories)
    {
        if (!npcMemoryEntrySo.HasDialogue()) continue;

        StartDialogueNode node = npcMemoryEntrySo.effects.startdialog;
        if (!node.isRepeatable
            && WorldStateManager.Instance != null
            && WorldStateManager.Instance.IsDialoguePlayed(node.name))
            continue;

        result.Add(node);
    }

    return result;
}
```

Add `using Game.Core;` at the top if not already present (it is — `GameLog` already uses it).

---

**Task 5 — Add `StartTopic` / `NotifyTopicCompleted` to `DialogueSystem`**

File: `Assets/_Game/Scripts/World/DialogueSystem.cs`

**5a.** Add private field after `_currentGraph`:

```csharp
private StartDialogueNode _currentStartNode;
```

**5b.** Add two public methods after `AdvanceToNode`:

```csharp
/// <summary>
/// Called by DialogueUI when the player selects a topic.
/// Tracks the active start node for played-state recording, then advances.
/// </summary>
public void StartTopic(StartDialogueNode startNode)
{
    _currentStartNode = startNode;
    AdvanceToNode(startNode.nextNode);
}

/// <summary>
/// Called by DialogueUI when a dialogue chain reaches its end node (nextNode == null).
/// If the active topic is non-repeatable, records it as played in WorldStateManager.
/// </summary>
public void NotifyTopicCompleted()
{
    if (_currentStartNode == null || _currentStartNode.isRepeatable) return;
    if (WorldStateManager.Instance != null)
        WorldStateManager.Instance.SetDialoguePlayed(_currentStartNode.name);
    GameLog.Info(TAG, $"Dialogue topic '{_currentStartNode.name}' marked as played");
    _currentStartNode = null;
}
```

**5c.** In `Close()`, clear `_currentStartNode` (add one line after `_currentGraph = null`):

```csharp
_currentStartNode = null;
```

---

**Task 6 — Update `DialogueUI` call sites**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

**6a.** `AddStartNodeButton` — change the button click handler from `AdvanceToNode(nextNode)` to `StartTopic`:

```csharp
// BEFORE:
btn.onClick.AddListener(() =>
{
    if (captured.nextNode != null)
        _dialogueSystem.AdvanceToNode(captured.nextNode);
    else
        GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
});

// AFTER:
btn.onClick.AddListener(() =>
{
    if (captured.nextNode != null)
        _dialogueSystem.StartTopic(captured);
    else
        GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
});
```

**6b.** `OnPointerClick` — insert `NotifyTopicCompleted()` before `RestoreTopics()` on the chain-end branch:

```csharp
// BEFORE:
if (_pendingNextNode != null)
    _dialogueSystem.AdvanceToNode(_pendingNextNode);
else
    RestoreTopics();

// AFTER:
if (_pendingNextNode != null)
    _dialogueSystem.AdvanceToNode(_pendingNextNode);
else
{
    _dialogueSystem.NotifyTopicCompleted();
    RestoreTopics();
}
```

**6c.** `AddChoiceButton` — insert `NotifyTopicCompleted()` before `RestoreTopics()` on the chain-end branch:

```csharp
// BEFORE:
btn.onClick.AddListener(() =>
{
    if (captured.nextNode != null)
        _dialogueSystem.AdvanceToNode(captured.nextNode);
    else
        RestoreTopics();
});

// AFTER:
btn.onClick.AddListener(() =>
{
    if (captured.nextNode != null)
        _dialogueSystem.AdvanceToNode(captured.nextNode);
    else
    {
        _dialogueSystem.NotifyTopicCompleted();
        RestoreTopics();
    }
});
```

---

### Acceptance Criteria

**AC1 — One-shot topic is hidden after completion**

Given a `StartDialogueNode` with `isRepeatable = false` wired to an active `NPCMemoryEntrySO`,
When the player opens dialogue, selects that topic, and clicks through to the last text node (one with `nextNode = null`),
Then closing and reopening dialogue with the same NPC does **not** show that topic in the list.

---

**AC2 — Repeatable topic remains visible after completion**

Given a `StartDialogueNode` with `isRepeatable = true` (or default),
When the player completes that topic's dialogue chain,
Then reopening dialogue shows the topic again.

---

**AC3 — Closing mid-dialogue does not mark topic as played**

Given a `StartDialogueNode` with `isRepeatable = false`,
When the player opens that topic but presses ESC (or otherwise closes the dialogue) before reaching the last text node,
Then reopening dialogue still shows the topic.

---

**AC4 — Played state persists in `WorldStateManager`**

Given a one-shot topic has been completed,
When `WorldStateManager.Instance.GetSaveData()` is called,
Then the returned `worldFacts` dictionary contains the key `Dialogue.Played.{nodeName}` with value `true`.

---

**AC5 — All existing `StartDialogueNode` assets are unaffected**

Given no existing `StartDialogueNode` SO has `isRepeatable` explicitly set,
When the project is opened after this change,
Then all existing dialogue topics behave identically to before (fully repeatable).

---

**AC6 — One-shot topic reappears if `WorldStateManager` state is reset**

Given `WorldStateManager._worldFacts` is cleared (e.g. new game),
Then a previously played one-shot topic is visible again.

---

## Additional Context

### Dependencies

- `WorldStateManager.Instance` must be non-null when the dialogue chain ends. `WorldStateManager` uses `DontDestroyOnLoad` and is a singleton on the Core scene — this is always satisfied during gameplay.
- No new Unity packages or assembly references required.

### Testing Strategy

Manual playtest steps (no automated test framework yet):

1. Create a `StartDialogueNode` asset with `isRepeatable = false`, wire it to an NPC memory entry.
2. Open dialogue → select the one-shot topic → click through to end → topic screen shows remaining topics (or empty if it was the only one).
3. Close and reopen NPC dialogue → one-shot topic is **absent**.
4. Repeat steps 2–3 with `isRepeatable = true` → topic is **present** after replay.
5. Open a one-shot topic, press ESC before the last node → reopen → topic still **present**.
6. Inspect `WorldStateManager` in Play Mode via the Inspector — `_worldFacts` should show `Dialogue.Played.{yourNodeName} = true` after step 2.

## Review Notes

- Adversarial review completed: 8 findings (3 High, 3 Medium, 2 Low)
- Findings fixed: 7 (F-01 through F-03, F-05 through F-08)
- Findings skipped: 1 (F-04 — `node.name` as key is a documented design constraint)
- Resolution approach: auto-fix

---

### Notes

- **Asset naming constraint:** The played-state key is `ScriptableObject.name` (the SO asset filename). Renaming a `StartDialogueNode` asset after it has been recorded in a save file breaks the lookup — the topic reappears. Enforce the convention: once a `StartDialogueNode` is shipped, its filename is immutable.
- **`DialogueUI` null guard:** `_dialogueSystem` is already null-checked in existing `DialogueUI` methods. The new calls to `StartTopic` and `NotifyTopicCompleted` are inside the same existing null-check branches — no additional guards needed.
- **Future extension:** If per-NPC granularity is ever needed, the key can be changed to `Dialogue.Played.{npcId}.{nodeName}` — the typed API surface (`SetDialoguePlayed` / `IsDialoguePlayed`) would just add a parameter. This change is isolated to `WorldStateManager`.
