# CLAUDE.md — Assets/_Game/Data/Quests

> Quest ScriptableObject assets live here. Full system doc: `docs/Quests/Quest.md`.

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

`QuestPart.entry` is the text shown in the Quest Log for that condition.

---

## Connecting to Other Systems

| System | How to wire |
|---|---|
| **Facts** | Every `QuestPart.fact` must be a `Fact` SO asset (`WorldFact`, `KilledFact`, `DialogueFact`, etc.). The fact is written externally (kill, dialogue played, etc.) — `QuestSO` only reads it. |
| **QuestFact** | Create a `QuestFact` SO (`Game/Facts/Quest Fact`) referencing this quest + a state (IsStarted / IsCompleted / IsFailed / step index). Use it as an unlock/invalidation condition on `NPCMemoryEntrySO` or as a `QuestPart.fact` in another quest. |
| **QuestEventsManager** | Add this `QuestSO` to its `_quests` list. It will fire `_onQuestStarted/Completed/Failed/StepCompleted` on transitions. |
| **PlayerRewardSO** | Create a `PlayerRewardSO` (`Game/Rewards/Player Reward`) with `FactType = Quest`, point it at this quest + state. Wire it into `PlayerRewards._rewards`. See `docs/Systems/Quest.md`. |
| **NPC Memory** | Create a `QuestFact` for the desired state and add it to an `NPCMemoryEntrySO.unlockConditions` or `invalidationConditions` to gate NPC dialogue on quest progress. See `Assets/_Game/ScriptableObjects/NPC/`. |
| **Quest Log UI** | Add this `QuestSO` to `QuestLogUI._allQuests`. No other wiring needed — the UI reads state directly from the SO. |

---

## Naming Convention

`Quest_{QuestId}.asset` — e.g. `Quest_FindHerbalist.asset`

`questId` must match the filename suffix and be unique across all quests (used as the key in `QuestFact.ToString()`).
