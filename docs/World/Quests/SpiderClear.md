# Quest Spec — SpiderClear

> Template version: 1.0
> Status: `implemented`
> Spec path: `docs/Quests/SpiderClear.md`

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `SpiderClear` |
| `title` | Spider Infestation |
| `description` | The town guard has spotted a nest of Darkness Spiders lurking near the outskirts of town. He's asked you to hunt them down before they become a real problem. Kill all five and report back to the guard for your reward. |

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_Guard_SpiderQuestAccepted` | `DialogueFact` | Set when `Start_Guard_SpiderOffer` node is played (player accepts the job) |
| `DialogueFact_Guard_SpiderQuestTurnIn` | `DialogueFact` | Set when `Start_Guard_SpiderReward` node is played (player turns in) |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `KilledFact_Enemy_DarknessSpider` | `KilledFact` | `Assets/_Game/Data/Enemies/StartingTown/Generic/` |
| `KilledFact_Enemy_DarknessSpider (1)` | `KilledFact` | `Assets/_Game/Data/Enemies/StartingTown/Generic/` |
| `KilledFact_Enemy_DarknessSpider (2)` | `KilledFact` | `Assets/_Game/Data/Enemies/StartingTown/Generic/` |
| `KilledFact_Enemy_DarknessSpider (3)` | `KilledFact` | `Assets/_Game/Data/Enemies/StartingTown/Generic/` |
| `KilledFact_Enemy_DarknessSpider (4)` | `KilledFact` | `Assets/_Game/Data/Enemies/StartingTown/Generic/` |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_Guard_SpiderQuestAccepted
  entry: "You accepted the guard's request to clear the Darkness Spiders."

completedParts:
  - fact:  DialogueFact_Guard_SpiderQuestTurnIn
    entry: "You reported back to the guard. Quest complete."

failedParts: []
```

---

## Steps

```
steps:
  - title: "Kill the Darkness Spiders"
    description: "Hunt down the five Darkness Spiders lurking near town."
    parts:
      - fact:  KilledFact_Enemy_DarknessSpider
        entry: "Kill the Darkness Spiders (0/5)"
      - fact:  KilledFact_Enemy_DarknessSpider (1)
        entry: "Kill the Darkness Spiders (1/5)"
      - fact:  KilledFact_Enemy_DarknessSpider (2)
        entry: "Kill the Darkness Spiders (2/5)"
      - fact:  KilledFact_Enemy_DarknessSpider (3)
        entry: "Kill the Darkness Spiders (3/5)"
      - fact:  KilledFact_Enemy_DarknessSpider (4)
        entry: "Kill the Darkness Spiders (4/5)"
```

---

## NPC Involvement

```
npcs:
  - npc: NPC_Guard
    exists: true
    folder: Assets/_Game/Data/NPCs/NPC_Guard.asset

    memories:
      - asset: Mem_Guard_SpiderOffer
        unlock:     []
        invalidate: [DialogueFact_Guard_SpiderQuestAccepted]
        dialogue_topic: SpiderOffer

        dialogue:
          - NPC: "Adventurer! We've got a problem. Darkness Spiders have been spotted near the edge of town — five of them. Left unchecked, they'll be a real danger. Would you be willing to deal with them?"
          - Player: "I'll take care of it."  → END  [sets: DialogueFact_Guard_SpiderQuestAccepted]
          - Player: "Not interested."        → END

      - asset: Mem_Guard_SpiderQuestActive
        unlock:     [DialogueFact_Guard_SpiderQuestAccepted]
        invalidate: [DialogueFact_Guard_SpiderQuestTurnIn]
        dialogue_topic: SpiderActive

        dialogue:
          - NPC: "The spiders are still out there. Stay sharp — don't let them get the better of you."
          - [END]

      - asset: Mem_Guard_SpiderReward
        unlock:     [DialogueFact_Guard_SpiderQuestAccepted,
                     KilledFact_Enemy_DarknessSpider,
                     KilledFact_Enemy_DarknessSpider (1),
                     KilledFact_Enemy_DarknessSpider (2),
                     KilledFact_Enemy_DarknessSpider (3),
                     KilledFact_Enemy_DarknessSpider (4)]
        invalidate: [DialogueFact_Guard_SpiderQuestTurnIn]
        dialogue_topic: SpiderReward

        dialogue:
          - NPC: "All five? You actually did it. The town owes you one, adventurer. Here's your reward — well earned."
          - [END]  [sets: DialogueFact_Guard_SpiderQuestTurnIn]
```

---

## Rewards

```
rewards:
  onStart: null

  onStepCompleted: null

  onCompleted:
    xp: 200
    lp: 0
    gold: 100
    stats: []

  onFailed: null
```

---

## Implementation Checklist

- [x] Fact assets created — `Assets/_Game/Data/Facts/`
  - [x] `DialogueFact_Guard_SpiderQuestAccepted` — GUID `6dbfcecc138b29a42904b465db447573`
  - [x] `DialogueFact_Guard_SpiderQuestTurnIn` — GUID `d526339be257ec5469d28593abdacafa`
  - [x] `QuestFact_SpiderClear_Completed` — GUID `9cc76620ed3adc444a154a4185a765db`
- [x] QuestSO created — `Assets/_Game/Data/Quests/Quest_SpiderClear.asset`
- [x] NPC_Guard Memories and Dialogues folder scaffolded — `Assets/_Game/Data/NPCs/NPC_Guard/`
  - [x] `Mem_Guard_SpiderOffer` created and wired to NPC_Guard.asset — GUID `6afbb16d35948ed4493a8f7cdccdd00a`
  - [x] `Mem_Guard_SpiderQuestActive` created and wired — GUID `20cc73d53753bfa4dbd86dca14d513b6`
  - [x] `Mem_Guard_SpiderReward` created and wired — GUID `f3673fbad53f1004a959f8f75032499c`
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderOffer/` (Start + Choice)
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderActive/` (Start + Text)
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderReward/` (Start + Text)
- [x] PlayerRewardSO asset created — `Assets/_Game/Data/Rewards/PlayerReward_SpiderClear_Completed.asset`
- [x] Quest registered in `QuestEventsManager._quests` (`Assets/_Game/Prefabs/QuestEventsManager.prefab`)
- [x] Quest registered in `QuestLogUI._allQuests` (`Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab`)
- [x] PlayerReward registered in `PlayerRewards._rewards` (`Assets/_Game/Prefabs/Player/Player.prefab`)
