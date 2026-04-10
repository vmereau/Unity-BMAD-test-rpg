# DIALOGUE.md — NPC Dialogue System

> Read this before creating or modifying any dialogue assets.

---

## Overview

Dialogues are chains of `ScriptableObject` nodes. The player opens a conversation, picks a topic
(a `StartDialogueNode`), and the system walks the chain until it hits a null `nextNode`.

All dialogue assets live inside the NPC's dedicated folder:

```
<NPCName>/Dialogues/<TopicName>/
  Start_<NPC>_<Topic>.asset     ← entry point, one per topic
  Text_<NPC>_<Topic>.asset      ← NPC speech line(s)
  Choice_<NPC>_<Topic>.asset    ← branching node (optional)
```

**Rule:** If a dedicated `Dialogues/` folder does not exist under the NPC folder, create it.
**Rule:** If a dialogue topic folder does not exist, create it before placing node assets.

---

## Node Types

### StartDialogueNode
`CreateAssetMenu: Game/Dialogue/Start Node` — filename prefix `Start_`

The **entry point** for a single conversation topic. The player sees its `text` as the topic
button label in the dialogue topic list.

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | Label shown on the topic button (e.g. "Tell me about this place") |
| `nextNode` | `DialogueNode` | First real node of the chain (must not be null) |
| `isRepeatable` | `bool` | If `false`, the topic is hidden after it has been played once (default: `true`) |

**Important — memory association:**
`StartDialogueNode` is not directly referenced by `NPCDataSO`. It is exposed to the dialogue
system through an `NPCMemoryEntrySO` (via `effects.startdialog`). When creating a new
`StartDialogueNode`, always ask the user:

> "Should I create a new `NPCMemoryEntrySO` for this topic and add it to the NPC's memories list,
> or link it to an existing memory entry?"

Then either:
- Create `Mem_<NPC>_<Topic>.asset` in `<NPCName>/Memories/`, set `effects.startdialog` to the
  new `StartDialogueNode`, and add the memory to the `NPCDataSO.memories` list; OR
- Open the existing `NPCMemoryEntrySO` and set its `effects.startdialog` to the new node.

---

### TextDialogueNode
`CreateAssetMenu: Game/Dialogue/Text Node` — filename prefix `Text_`

A single NPC speech line.

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | NPC speech. **Max 300 characters.** |
| `nextNode` | `DialogueNode` | Next node. Null = end of chain (UI shows "Farewell." button) |

**Text length rule:** If the intended text exceeds 300 characters, split it into multiple
`TextDialogueNode` assets chained via `nextNode`. Split at natural sentence boundaries so the
text reads as a normal conversation — not mid-sentence. When in doubt, prefer shorter nodes
(one or two sentences each) so the player can pace reading comfortably.

**Naming:** For chained text nodes within the same topic, append a sequence number:
`Text_<NPC>_<Topic>.asset`, `Text_<NPC>_<Topic>_2.asset`, etc.

**End of chain:** The last `TextDialogueNode` in a topic chain must have `nextNode = null`.
When reached, `DialogueSystem.NotifyTopicCompleted()` is called — if `isRepeatable` is `false`
on the `StartDialogueNode`, the topic is recorded as played and hidden on the next open.

---

### ChoiceDialogueNode
`CreateAssetMenu: Game/Dialogue/Choice Node` — filename prefix `Choice_`

Presents the player with a set of labelled buttons. The NPC's `text` is shown above the
choices. Each `ChoiceOption` routes to its own sub-chain.

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | NPC prompt above the choices (max 300 characters) |
| `choices` | `ChoiceOption[]` | Array of player options |

**ChoiceOption fields:**

| Field | Type | Purpose |
|-------|------|---------|
| `text` | `string` | Label on the choice button (player's voice) |
| `requiredMemory` | `NPCMemoryEntrySO` | If set, choice is hidden unless this memory `IsActive()`. Null = always shown |
| `nextNode` | `DialogueNode` | Node to advance to when selected. Null = close dialogue |

`ChoiceDialogueNode.IsEndNode()` always returns `false` — the chain continues via each
choice's own `nextNode`.

**When to use:** Use `ChoiceDialogueNode` when the player's selection determines the NPC
response (branching). If it is just sequential NPC speech, use chained `TextDialogueNode`s.

**Clarification rule:** If the user's intent around a `ChoiceDialogueNode` is ambiguous
(e.g. unclear what the choices are, what they lead to, or whether memory-gating is needed),
**ask for clarification before creating any assets.**

---

## Dialogue Chain Example

```
Start_Blacksmith_Make         isRepeatable=true, text="What can you make?"
  └─ nextNode → Text_Blacksmith_Make   text="I can forge swords, axes, and hammers."
                  └─ nextNode → Choice_Blacksmith_Make   text="What would you like?"
                                  ├─ choices[0]: "A sword"  → Text_Blacksmith_Make_Sword (nextNode=null)
                                  ├─ choices[1]: "An axe"   → Text_Blacksmith_Make_Axe   (nextNode=null)
                                  └─ choices[2]: "A hammer" → Text_Blacksmith_Make_Hammer (nextNode=null)
```

---

## Naming Conventions

| Asset | Convention |
|-------|-----------|
| Start node | `Start_<NPC>_<Topic>.asset` |
| Text node | `Text_<NPC>_<Topic>.asset`, `Text_<NPC>_<Topic>_2.asset`, ... |
| Choice node | `Choice_<NPC>_<Topic>.asset` |
| Topic folder | `<TopicName>/` — PascalCase |

---

## Checklist When Creating a New Dialogue Topic

- [ ] NPC subfolder exists (e.g. `Villager/`)
- [ ] `Dialogues/<TopicName>/` subfolder created
- [ ] `StartDialogueNode` asset created and linked to a memory (`effects.startdialog`)
- [ ] Memory added to `NPCDataSO.memories` list (or existing memory updated)
- [ ] No single `TextDialogueNode.text` exceeds 300 characters
- [ ] Last node in each chain has `nextNode = null`
- [ ] `ChoiceDialogueNode` usage confirmed with user if branching is involved
