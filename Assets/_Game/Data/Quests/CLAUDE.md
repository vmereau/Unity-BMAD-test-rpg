# CLAUDE.md — Assets/_Game/Data/Quests

> Quest ScriptableObject assets live here. Full system doc: `docs/World/Quests/Quest.md`.
> Inspect / validate a quest (setters, readers, orphan facts, registration): `Tools/Quests/Quest Explorer`
> (see `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`). Fact types: `Assets/_Game/Data/Facts/CLAUDE.md`.

---

## QuestSO Structure

`Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` — `namespace Game.Quest`

A quest has **no mutable state**. All status is derived at runtime by reading `WorldStateManager` facts.

```
QuestSO
├── questId          string — unique key, used by QuestFact and in log keys
├── title / description
│
├── startPart        QuestPart { Fact, entry }   — quest is started when fact is true
├── completedParts[] QuestPart list              — completed when ANY fact is true
├── failedParts[]    QuestPart list              — failed when ANY fact is true
└── steps[]          QuestStep list
      ├── title / description
      └── parts[]    QuestPart list              — step active if ANY true; done if ALL true
```

`QuestPart.entry` is the text shown in the Quest Log for that condition. It may be empty on step parts
(the validator only reports that as Info); keep it filled on start / completed / failed parts.

---

## Connecting to Other Systems

| System | How to wire |
|---|---|
| **Facts** | Every `QuestPart.fact` must be a `Fact` SO asset (`WorldFact`, `KilledFact`, `DialogueFact`, etc.). The fact is written externally (kill, dialogue played, etc.) — `QuestSO` only reads it. |
| **QuestFact** | Create a `QuestFact` SO (`Game/Facts/Quest Fact`) referencing this quest + a state (IsStarted / IsCompleted / IsFailed / step index). Use it as an unlock/invalidation condition on `NPCMemoryEntrySO` or as a `QuestPart.fact` in another quest. |
| **QuestEventsManager** | Prefab `Prefabs/QuestEventsManager.prefab` — `_quests` is **auto-synced** with every `QuestSO` by `QuestEventsManagerAutoSync` (manual: `Game/Dev/Sync Quests to QuestEventsManager Prefab`). It fires `_onQuestStarted/Completed/Failed/StepCompleted` on transitions. |
| **PlayerRewardSO** | Create a `PlayerRewardSO` (`Game/Rewards/Player Reward`) with `FactType = Quest`, point it at this quest + state. `PlayerRewards._rewards` on `Prefabs/Player/Player.prefab` is auto-synced. See `docs/World/Quests/Quest.md`. |
| **NPC Memory** | Create a `QuestFact` for the desired state and add it to an `NPCMemoryEntrySO.unlockConditions` or `invalidationConditions` to gate NPC dialogue on quest progress. See `Assets/_Game/ScriptableObjects/Entities/NPC/`. |
| **Quest Log UI** | Prefab `Prefabs/UI/QuestLog/QuestLogUI.prefab` — add this `QuestSO` to `_allQuests` **manually** (or Quest Explorer's "QuestLog ✗ → Fix"). No other wiring needed — the UI reads state directly from the SO. |

---

## Naming Convention

`Quest_{QuestId}.asset` — e.g. `Quest_FindHerbalist.asset`

`questId` must match the filename suffix and be unique across all quests (used as the key in `QuestFact.ToString()`).
