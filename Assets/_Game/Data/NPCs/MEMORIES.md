# MEMORIES.md — NPC Memory System

> Read this before creating or modifying any `NPCMemoryEntrySO` assets.

---

## Overview

`NPCMemoryEntrySO` (`Game/NPC/Memory Entry`, filename prefix `Mem_`) is the central
bridge between world state and NPC behaviour. Each memory entry:

1. Declares **conditions** (world fact keys) that must be true for the memory to be active
2. Applies **effects** while active: dialogue topics, shop modifiers, routine overrides, quest hooks

`NPCMemoryComponent` (on the NPC prefab) holds a reference to `NPCEntity` and evaluates all
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
| `unlockConditions` | `Fact[]` | **ALL** must evaluate true for memory to be active |
| `invalidationConditions` | `Fact[]` | If **ANY** evaluates true, memory is permanently closed (supersedes unlock) |

Leave both arrays empty for a memory that is always active (e.g. a default greeting).

Conditions use typed `Fact` ScriptableObject assets. Create the appropriate subtype and reference it in the array — never raw strings. `WorldStateManager.GetFact(Fact)` dispatches by subtype: `SkillFact` and `StatFact` are computed live from `PlayerSkills`/`PlayerStats`; other types (`KilledFact`, `DialogueFact`, `WorldFact`) are stored as flat key/bool entries.

For gating dialogue choices by skill learned state, use `SkillFact` — see `Assets/_Game/Data/Skills/CLAUDE.md`.

### Effects (`NPCMemoryEffects`)

| Field | Type | Purpose |
|-------|------|---------|
| `effects.startdialog` | `StartDialogueNode` | Topic entry point exposed while this memory is active |

`startdialog` is the only effect that exists. Shops have no memory effect (no price modifier / reveal line):
a shop is a topic like any other, so its visibility is controlled by this memory's conditions — see
`DIALOGUE.md` → Shop Topics and `/NPC:shop-dialogue`.

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

- The memory `IsActive()`. One-shot topics hide themselves by setting `StartDialogueNode.dialogueFact` and
  listing that fact in the memory's `invalidationConditions` (see `DIALOGUE.md`).

`ChoiceOption.requiredMemory` on a `ChoiceDialogueNode` hides individual choice buttons
unless the referenced memory `IsActive()`.

---

## Checklist When Creating a New Memory Entry

- [ ] `Memories/` subfolder exists under the NPC folder
- [ ] Asset named `Mem_<NPC>_<Topic>.asset`
- [ ] `unlockConditions` populated (or left empty for always-active)
- [ ] `effects.startdialog` set to the corresponding `StartDialogueNode` (if dialogue-bearing)
- [ ] Memory saved under `<NPCName>/Memories/` — auto-added to `NPCEntity.memories` by `NPCMemoriesAutoSync` (the asset must live under `<NPCName>/Memories/`). Menu fallback: `Game/Dev/Sync All NPC Memories`