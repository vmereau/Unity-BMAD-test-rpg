# MEMORIES.md — NPC Memory System

> Read this before creating or modifying any `NPCMemoryEntrySO` assets.

---

## Overview

`NPCMemoryEntrySO` (`Game/NPC/Memory Entry`, filename prefix `Mem_`) is the central
bridge between world state and NPC behaviour. Each memory entry:

1. Declares **conditions** (world fact keys) that must be true for the memory to be active
2. Applies **effects** while active: dialogue topics, shop modifiers, routine overrides, quest hooks

`NPCMemoryComponent` (on the NPC prefab) holds a reference to `NPCDataSO` and evaluates all
memory entries on demand via `GetActiveMemories()`. The dialogue system queries this to build
the list of available topics.

---

## Folder Structure

Memory assets for an NPC live in:

```
<NPCName>/Memories/
  Mem_<NPC>_<Topic>.asset
```

**Rule:** If a dedicated `Memories/` folder does not exist under the NPC folder, create it
before placing memory assets.

---

## NPCMemoryEntrySO Fields

### Conditions

| Field | Type | Purpose |
|-------|------|---------|
| `unlockConditions` | `string[]` | **ALL** must be true in `WorldStateManager` for memory to be active |
| `invalidationConditions` | `string[]` | If **ANY** is true, memory is permanently closed (supersedes unlock) |

Leave both arrays empty for a memory that is always active (e.g. a default greeting).

World fact keys follow the `WorldFactPrefix` enum format:
- `Killed.<EnemyName>` — registered by `WorldStateManager.RegisterKill()`
- `Quest.<QuestId>.<Step>` — registered by `WorldStateManager.SetQuestStep()`
- `World.<EventId>` — registered by `WorldStateManager.SetWorldEvent()`
- `Dialogue.<NodeName>` — registered automatically when a non-repeatable topic completes

**Never construct key strings manually** — use the typed setter methods on `WorldStateManager`.

### Effects (`NPCMemoryEffects`)

| Field | Type | Purpose |
|-------|------|---------|
| `effects.startdialog` | `StartDialogueNode` | Topic entry point exposed while this memory is active |
| `effects.choicesDialogues` | `ChoiceDialogueNode[]` | Choice nodes whose available options may be gated by this memory |
| `effects.shopPriceModifier` | `float [-1, 1]` | Price modifier (0 = no effect, -0.1 = 10% discount) |
| `effects.shopRevealDialogueLine` | `string` | One-shot line on first shop open while active |
| `effects.routineOverride` | `NPCState` | Overrides the NPC's schedule while active |
| `effects.overrideRoutine` | `bool` | Must be `true` for `routineOverride` to apply |
| `effects.questDialogueKey` | `string` | Dialogue key for quest initiation (future system) |

---

## Activation Logic

```
IsUnlocked()    → all unlockConditions keys are true in WorldStateManager
IsInvalidated() → any invalidationConditions key is true
IsActive()      → IsUnlocked() && !IsInvalidated()
```

`GetActiveMemories()` is called on demand (not cached). When the player opens a dialogue
panel or shop, the system re-evaluates all memories at that moment.

---

## Dialogue Gating via Memory

Only memories whose `IsActive()` returns `true` contribute topics. The `StartDialogueNode`
linked in `effects.startdialog` is added to the topic list if:

- The memory `IsActive()`, **and**
- The node's `isRepeatable == true`, **or** the node has not been recorded as played in
  `WorldStateManager` (key: `Dialogue.<nodeName>`)

`ChoiceOption.requiredMemory` on a `ChoiceDialogueNode` hides individual choice buttons
unless the referenced memory `IsActive()`.

---

## Checklist When Creating a New Memory Entry

- [ ] `Memories/` subfolder exists under the NPC folder
- [ ] Asset named `Mem_<NPC>_<Topic>.asset`
- [ ] `unlockConditions` populated (or left empty for always-active)
- [ ] `effects.startdialog` set to the corresponding `StartDialogueNode` (if dialogue-bearing)
- [ ] Memory added to `NPCDataSO.memories` list in the NPC's `NPC_<Name>.asset`