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

## Writing Rules

- **No em-dashes (—) in any dialogue text**: speech lines, choice labels and topic labels. Use a comma, a period, a
  colon or "..." instead. Check with `grep -rlE 'u2014|—' Assets/_Game/Data/NPCs --include=*.asset` (Unity stores the
  character as the escape `\u2014` in YAML).
- **Greeting opener pattern** (most NPCs): a stranger doesn't share secrets on the first meeting. Give the NPC one
  opener topic (e.g. Tam's "Are you alright?") that ends on a `ChoiceDialogueNode` with attitude choices, each setting
  its own `ChoiceOption.dialogueFact` (`DialogueFact_<NPC>_<Attitude>`). The opener memory is invalidated by any of
  those facts; deeper topics (quest hints, gossip) unlock on the favourable fact and are invalidated by the hostile
  one. A neutral "leave" choice with no fact keeps the opener available. Example: `BeatenWorker/` (see its `CLAUDE.md`).

---

## Voice

NPCs talk like people, not like a trailer. The default "gritty" register (clipped fragments, ellipses, "(beat)"
pauses, one-line punches like "It's nobody's.") made every NPC sound the same; avoid it.

- **A node is natural speech.** Use the full 300 characters when the character would keep talking: complete
  sentences, joined the way people join them ("and", "so", "mind you"). Split only at the limit, at a sentence boundary.
- **Voice comes from the identity sheet**, not from short sentences: what the NPC cares about, their work vocabulary,
  their rhythm (a rambler, a grumbler, a careful speaker), how they address the player. Read `<NPC>/CLAUDE.md` →
  Speech Style first, and write it there if it is missing.
- **No ellipses or stage directions** ("...", "(pause)", "(beat)") unless the character truly hesitates in that line.
  At most one per topic.
- **Let lines breathe:** a complaint, a joke or a digression is what makes a speaker real (Gothic's NPCs ramble and
  grumble). Exposition reads better as the NPC's own story than as a list of facts.
- Clipped speech is a **trait**, not a default: keep it for the few NPCs whose identity says so (e.g. Overseer Dace).

Example (Bren, quarryman, "campfire storyteller"): "Night watch on the quarry is one guard and a brazier, and the
guard's usually asleep before the brazier is, so it wouldn't have been hard for him."

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
| `dialogueFact` | `DialogueFact` | When set, written to `WorldStateManager` when the topic completes. Use this to gate one-shot topics via memory `invalidationConditions`. Null = repeatable. |

**One-shot topics (play once, then disappear):** There is no `isRepeatable` flag. To make a
topic disappear after it is played:
1. Set `dialogueFact` on the `StartDialogueNode` to a `DialogueFact` asset.
2. Add that same fact to `invalidationConditions` on the `NPCMemoryEntrySO`.

When the chain ends the fact is written, the memory is invalidated, and the topic is hidden.

**Repeatable topics:** Leave `dialogueFact` null and omit any playback-triggered invalidation
condition. The topic stays visible for as long as the memory `IsActive()`.

**Important — memory association:**
`StartDialogueNode` is not directly referenced by `NPCEntity`. It is exposed to the dialogue
system through an `NPCMemoryEntrySO` (via `effects.startdialog`). When creating a new
`StartDialogueNode`, always ask the user:

> "Should I create a new `NPCMemoryEntrySO` for this topic and add it to the NPC's memories list,
> or link it to an existing memory entry?"

Then either:
- Create `Mem_<NPC>_<Topic>.asset` in `<NPCName>/Memories/`, set `effects.startdialog` to the
  new `StartDialogueNode`, and save the memory under `<NPCName>/Memories/` (auto-added to `NPCEntity.memories` by `NPCMemoriesAutoSync` (the asset must live under `<NPCName>/Memories/`)); OR
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
text reads as a normal conversation — not mid-sentence. This is a size rule, not a style rule: a node can
hold a full paragraph of speech (see **Voice** above).

**Naming:** For chained text nodes within the same topic, append a sequence number:
`Text_<NPC>_<Topic>.asset`, `Text_<NPC>_<Topic>_2.asset`, etc.

**End of chain:** The last `TextDialogueNode` in a topic chain must have `nextNode = null`.
When reached, if `StartDialogueNode.dialogueFact` is set, that fact is written to
`WorldStateManager`, which can trigger memory invalidation and hide the topic.

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
| `dialogueFact` | `DialogueFact` | When set, written to `WorldStateManager` when this choice is selected. Use to track which branch the player took. |
| `goldCost` | `int` | Gold the player pays the NPC on pick (coins go into the NPC's `InventorySystem` as `Item_Gold`, lootable back). The row shows " (Ng)" and is disabled + red while unaffordable. 0 = free. Teach choices inherit the same field. |
| `requiredItem` | `ItemSO` | Item the player hands over on pick: one unit is removed from the player's inventory (consumed, unless `giveToNpc`). A worn (equipped) copy counts as held and is unequipped on pick when none is loose in the bag. The row shows " (item name)" and is disabled + red while the player doesn't hold it. Checked before the gold is paid. Null = none. Not used by teach choices. Use it with a quest item (`isQuestItem`) so the player can't lose it, e.g. the Warden's-errand notices (`<Manager>/Dialogues/Notice/`); a normal item the player may sell or lose (Odo's ring) needs `hideWhenMissing` plus an always-visible fallback row. |
| `giveToNpc` | `bool` | The `requiredItem` moves into the NPC's `InventorySystem` (lootable back) instead of being consumed. Use it for things the NPC keeps (Bren's `Report` ring rows); leave it off for hand-ins that should vanish (the Warden's notices). |
| `requiredCategory` | `GiveItemCategory` | Kind of item handed over: `ReadyFood` (Apple / Bread, `PotionItemSO._readyToEat`) or `Meal` (cooked meals). Picking it opens the **Give window** (`GiveItemUI`): the player's matching items, one picked in the detail panel, Give / Cancel. The item **moves into the NPC's `InventorySystem`** (lootable back), unlike `requiredItem`. The row shows " (Food)" / " (Meal)", red + disabled while nothing matches; Cancel / Escape returns to the choices with nothing applied. Quest items never match. Ignored when `requiredItem` is set. Example: Tam's `Feed/` topic. |
| `hideWhenMissing` | `bool` | Hide the row (instead of red + disabled) while the player holds no `requiredItem` / no item of `requiredCategory` — for bonus answers like Tam's meal choice. Keep at least one always-visible way out (e.g. "Not yet."): Escape doesn't work on a choice node. |
| `signal` | `DialogueSignalSO` | Raised on `OnDialogueSignal` after the gold is paid — how gameplay systems react to an answer (e.g. `TollGate`). Never use a `DialogueFact` for that: they are permanent. Menu `Game/Dialogue/Signal`, name `Signal_<Topic>_<Answer>`, stored in the topic folder. |

`ChoiceDialogueNode.IsEndNode()` always returns `false` — the chain continues via each
choice's own `nextNode`.

**When to use:** Use `ChoiceDialogueNode` when the player's selection determines the NPC
response (branching). If it is just sequential NPC speech, use chained `TextDialogueNode`s.

**Clarification rule:** If the user's intent around a `ChoiceDialogueNode` is ambiguous
(e.g. unclear what the choices are, what they lead to, or whether memory-gating is needed),
**ask for clarification before creating any assets.**

---

### ShopDialogueNode
`CreateAssetMenu: Game/Dialogue/Shop Node` — filename prefix `Shop_`

Shows its `text` (NPC line), then opens the trade window on the NPC's `InventorySystem`
(`nextNode` unused — the trade window closes the dialogue). No `InventorySystem` → shown as plain text.

---

## Seen State, Tags & Order (player-facing)

- **Node id:** every `DialogueNode` has a hidden `_id` synced to its asset GUID by `OnValidate` (duplicates get
  their own). Nothing to author — but the id is only written to disk on **Save Project**: after creating
  nodes by script / MCP, save (`AssetDatabase.SaveAssets()`) and check the `.asset` has a **non-empty** `_id:` line
  before committing. Nodes made with `CreateInstance` + `CreateAsset` get `_id:` empty (`OnValidate` ran before the
  asset had a GUID): set `_id` to the asset GUID through `SerializedObject`, then save.
  A node with no id is never tracked (`DialogueSystem` warns once).
- **Seen:** picking a topic or a choice marks it seen (persisted with the save, no fact event). Seen options are
  drawn dimmed; seen topics sort below unseen ones (`[Farewell]` stays last). A choice is keyed by
  `{nodeId}.{index}` — **reordering a node's `choices` moves their seen state** to whatever lands on that index.
- **Tags:** an option whose chain reaches a `ShopDialogueNode` shows `[Trade]`, a `TeachChoiceDialogueNode`
  `[Teach]` (walk follows `nextNode` through text nodes, stops at a choice node). Tagged options are never dimmed
  — they are services the player returns to. There is no quest tag.
- Teach costs: the part the player can't pay (LP / gold / stat requirements) is shown in red.

## NPC-Initiated Topics (forced)

A system can open a topic on the player: `NPCPresence.RequestForcedTopic(startNode)` → `NPCDialogueRequestData.forcedStartNode`.
The panel opens straight into the chain (no topic list), Escape closes it at any point, and the end of the chain closes the
dialogue. The start node's `text` is never shown. Such topics are not exposed through a memory (Quest Explorer may flag them
as unattributed). Example: `RacketGuard/Dialogues/Toll/` (Harl's exit toll, `TollRacket_Harl`).

## Shop Topics

Use **`/NPC:shop-dialogue`** to create or edit a shop (topic, stock, conditions).

```
<NPCName>/Dialogues/Shop/
  Start_<NPC>_Shop.asset   ← topic button, dialogueFact null (repeatable)
  Shop_<NPC>.asset         ← ShopDialogueNode
<NPCName>/Memories/Mem_<NPC>_Shop.asset   ← effects.startdialog + visibility conditions
```

- **Stock** is not in the NPC folder: it is `InventorySystem._startingItems` on the NPC's **scene instance**
  (prefab override), edited with `NpcShopEditor` (`Assets/_Game/Editor/NPCs/`). The trade window lists the whole
  inventory at each item's `buyValue` / `sellValue`; there is no restocking.
- **Visibility** is per topic only — the shop memory's `unlockConditions` / `invalidationConditions`. Individual
  items can't be hidden, and there are no price modifiers.
- `Storekeeper/` predates the convention (`Start_Storekeeper`, `Shop_Storekeeper`, `Mem_Storekeeper_shop`).

---

## Dialogue Chain Example

```
Start_Blacksmith_Make         dialogueFact=null (repeatable), text="What can you make?"
  └─ nextNode → Text_Blacksmith_Make   text="I can forge swords, axes, and hammers."
                  └─ nextNode → Choice_Blacksmith_Make   text="What would you like?"
                                  ├─ choices[0]: "A sword"  → Text_Blacksmith_Make_Sword (nextNode=null)
                                  ├─ choices[1]: "An axe"   → Text_Blacksmith_Make_Axe   (nextNode=null)
                                  └─ choices[2]: "A hammer" → Text_Blacksmith_Make_Hammer (nextNode=null)
```

The example shows the naming rule. The real chain in `BlackSmith/Dialogues/Make/` predates it
(`Start_What_can_you_make`, `Text_Make`, `Choice_Make`, `Text_make_sword` / `_axe` / `_hammer`).

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

- [ ] NPC subfolder exists (e.g. `TentMate/`)
- [ ] `Dialogues/<TopicName>/` subfolder created
- [ ] `StartDialogueNode` asset created and linked to a memory (`effects.startdialog`)
- [ ] Memory saved under `<NPCName>/Memories/` — auto-added to `NPCEntity.memories` by `NPCMemoriesAutoSync` (the asset must live under `<NPCName>/Memories/`) (or existing memory updated)
- [ ] No single `TextDialogueNode.text` exceeds 300 characters
- [ ] Last node in each chain has `nextNode = null`
- [ ] `ChoiceDialogueNode` usage confirmed with user if branching is involved
