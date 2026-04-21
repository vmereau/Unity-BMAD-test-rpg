# Quest System

Covers the fact system, quest data model, NPC memory, dialogue nodes, quest events, and the reward pipeline.

---

## The Fact System (foundation of everything)

**`Fact`** (abstract SO, `Assets/_Game/ScriptableObjects/Facts/`) is the atomic unit. Every fact is a ScriptableObject with a `ToString()` that produces a string key. Subtypes:

| Type | Key format | Notes |
|---|---|---|
| `WorldFact` | `World.{eventKey}` | Generic world events |
| `KilledFact` | `Killed.{guid}` | Per-entity, GUID-identified |
| `DialogueFact` | `Dialogue.Played.{nodeId}` | A specific dialogue node was played |
| `QuestFact` | `Quest.{id}.{state}` | **Computed** — not stored in `_worldFacts` |
| `SkillFact` / `StatFact` | — | Player capability checks |

**`WorldStateManager`** (`Assets/_Game/Scripts/Core/State/`) is the single runtime truth: a `Dictionary<string, bool>` keyed by `fact.ToString()`. `SetFact()` stores the value and raises typed events: `_onEntityKilled` for kills, `_onDialoguePlayed` for dialogue, `_onFactChanged` for everything. `QuestFact` is evaluated by delegation to `QuestSO`'s computed properties via `IsQuestFactTrue()` — never stored directly.

---

## Quest Structure (QuestSO)

`Assets/_Game/ScriptableObjects/Quest/QuestSO.cs`

A quest is a pure data SO — **no mutable state, entirely derived from WorldStateManager at query time**.

```
QuestSO
├── startPart         → QuestPart { Fact, entry }     one fact true → quest is started
├── completedParts[]  → QuestPart list                ANY fact true → completed
├── failedParts[]     → QuestPart list                ANY fact true → failed
└── steps[]           → QuestStep list
      └── parts[]     → QuestPart list                ALL true → step done; ANY true → step active (shown in log)
```

`QuestPart` pairs a `Fact` with a display string — the `entry` text is what the quest log shows for that condition. `QuestStep.IsActive()` = any part's fact true. `QuestSO.IsStepCompleted()` = all parts' facts true.

**`QuestFact`** (`Assets/_Game/ScriptableObjects/Facts/QuestFact.cs`) encodes both quest-level states and step states in a single int field: `0=IsStarted`, `1=IsCompleted`, `2=IsFailed`, `3+N=step N`. This is what rewards and NPC memory conditions reference when they need to react to quest progression.

---

## Quest Event Pipeline (QuestEventsManager)

`Assets/_Game/Scripts/Quest/QuestEventsManager.cs`

Subscribes to `_onFactChanged`. On every fact write, re-evaluates all registered quests using **snapshot diffing** (stores a `QuestStateSnapshot` per quest). Fires the following event channels exactly once per `false→true` edge:

| Channel | Payload |
|---|---|
| `_onQuestStarted` | `QuestSO` |
| `_onQuestCompleted` | `QuestSO` |
| `_onQuestFailed` | `QuestSO` |
| `_onQuestStepCompleted` | `QuestStepData { QuestSO, stepIndex }` |

---

## NPC Memory as World-State Bridge

`Assets/_Game/ScriptableObjects/NPC/`

```
NPCDataSO
└── memories: NPCMemoryEntrySO[]
      ├── unlockConditions: Fact[]        ALL must be true
      ├── invalidationConditions: Fact[]  ANY true → closed
      └── effects: NPCMemoryEffects
            └── startdialog: StartDialogueNode  (dialogue chain to run)
```

`NPCMemoryEntrySO.IsActive()` = all unlock conditions met AND no invalidation condition true. Invalidation supersedes unlock. The NPC system evaluates the memory list at interaction time to select which `StartDialogueNode` to play — world state drives NPC dialogue.

---

## Dialogue Nodes

`Assets/_Game/ScriptableObjects/Dialogue/`

A dialogue chain is a linked list of `DialogueNode` SOs:

| Node | Purpose |
|---|---|
| `StartDialogueNode` | Entry point; carries a `DialogueFact` that is written to WorldStateManager when the chain is entered |
| `TextDialogueNode` | Single NPC speech line; `nextNode` continues or null ends |
| `ChoiceDialogueNode` | NPC text + array of `ChoiceOption`; each option has player text, optional `requiredMemory` gate (hidden if that `NPCMemoryEntrySO` is not active), `nextNode`, and its own `DialogueFact` |

When a node with a `DialogueFact` is played, the dialogue system calls `WorldStateManager.SetDialoguePlayed(dialogueFact)`, which writes the fact and raises `_onDialoguePlayed`.

---

## Reward System

`Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs`  
`Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs`

**`PlayerRewardSO`** defines rewards (XP, LP, Gold, stat upgrades) triggered by a specific fact:

| `RewardFactType` | Event channel | Match condition |
|---|---|---|
| `Killed` | `_onEntityKilled` | Exact `KilledFact` asset match |
| `Dialogue` | `_onDialoguePlayed` | Exact `DialogueFact` asset match |
| `Quest` | `_onQuestStarted/Completed/Failed/StepCompleted` | `QuestSO` + state or step index |

`PlayerRewards` (MonoBehaviour) subscribes to all channels and scans `_rewards: List<PlayerRewardSO>` for matches on each event. Kill events also apply base XP directly from `EnemyTypeSO.XpOnKill` before consulting reward SOs.

---

## Full Data Flow

Example: killing a named enemy whose `KilledFact` is wired as the `startPart` of a quest.

```
Enemy dies
  → EnemyController.RegisterKill(killedFact)
    → WorldStateManager.SetFact(killedFact, true)
      → raises _onEntityKilled, _onFactChanged

_onEntityKilled
  → PlayerRewards.HandleEntityKilled()        applies KilledFact rewards (XP/LP/Gold)

_onFactChanged
  → QuestEventsManager.HandleWorldFactChanged()
    → re-evaluates quest: startPart.fact is now true
    → fires _onQuestStarted

_onQuestStarted
  → PlayerRewards.HandleQuestStarted()        applies Quest.IsStarted rewards
  → QuestLogUI.HandleQuestStateChanged()      refreshes the quest list UI
```

---

## Quest Log UI

`Assets/_Game/Scripts/UI/Quest/`

| Script | Purpose |
|---|---|
| `QuestLogUI` | Root panel, `IScreenPanel`, owns `_allQuests` list |
| `QuestListPanelUI` | Filters quests by `QuestTab` (Started/Completed/Failed), spawns `QuestButtonUI` |
| `QuestButtonUI` | One quest entry; click notifies parent `QuestLogUI` |
| `QuestInfoPanelUI` | Shows title, description, active steps and parts for selected quest |

Live refresh: `QuestLogUI` subscribes to `_onQuestStarted/Completed/Failed` and calls `RefreshList()` on any transition.

---

## Future Work

- **Shop system** — `NPCMemoryEffects` reserved fields for price modifiers and shop dialogue lines (not yet implemented)
- **Routine/schedule system** — `NPCMemoryEffects` reserved fields for NPC state overrides (not yet implemented)
- **Save/load** — `WorldStateManager.GetSaveData()` returns a snapshot stub; wired in Epic 8
