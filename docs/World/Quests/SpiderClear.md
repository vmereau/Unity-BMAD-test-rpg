# Quest Spec — SpiderClear

> Template version: 1.0
> Status: `implemented`
> Spec path: `docs/World/Quests/SpiderClear.md`

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `SpiderClear` |
| `title` | Spider Infestation |
| `description` | Guard Harl has ordered you to clear out two Darkness Spiders nesting beyond the camp. Kill both and report back to him for your pay. |

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_RacketGuard_SpiderQuestAccepted` | `DialogueFact` | Set when `Start_RacketGuard_SpiderOffer` node is played (player accepts the job) |
| `DialogueFact_RacketGuard_SpiderQuestTurnIn` | `DialogueFact` | Set when `Start_RacketGuard_SpiderReward` node is played (player turns in) |

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
  fact:  DialogueFact_RacketGuard_SpiderQuestAccepted
  entry: "You accepted the guard's request to clear the Darkness Spiders."

completedParts:
  - fact:  DialogueFact_RacketGuard_SpiderQuestTurnIn
    entry: "You reported back to the guard. Quest complete."

failedParts: []
```

---

## Steps

```
steps:
  - title: "Kill the Darkness Spiders"
    description: "Hunt down the two Darkness Spiders nesting beyond the camp."
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
  - npc: NPC_RacketGuard
    exists: true
    folder: Assets/_Game/Data/NPCs/NPC_RacketGuard.asset

    memories:
      - asset: Mem_RacketGuard_SpiderOffer
        unlock:     []
        invalidate: [DialogueFact_RacketGuard_SpiderQuestAccepted]
        dialogue_topic: SpiderOffer

        dialogue:
          - NPC: "New meat. Two Darkness Spiders are nesting out past the rim, and the Warden wants it handled. Handled means you. There's coin in it, if you come back."
          - Player: "I'll take care of it."  → END  [sets: DialogueFact_RacketGuard_SpiderQuestAccepted]
          - Player: "Not interested."        → END

      - asset: Mem_RacketGuard_SpiderQuestActive
        unlock:     [DialogueFact_RacketGuard_SpiderQuestAccepted]
        invalidate: [DialogueFact_RacketGuard_SpiderQuestTurnIn]
        dialogue_topic: SpiderActive

        dialogue:
          - NPC: "The spiders are still out there. Stay sharp — don't let them get the better of you."
          - [END]

      - asset: Mem_RacketGuard_SpiderReward
        unlock:     [DialogueFact_RacketGuard_SpiderQuestAccepted,
                     KilledFact_Enemy_DarknessSpider,
                     KilledFact_Enemy_DarknessSpider (1),
                     KilledFact_Enemy_DarknessSpider (2),
                     KilledFact_Enemy_DarknessSpider (3),
                     KilledFact_Enemy_DarknessSpider (4)]
        invalidate: [DialogueFact_RacketGuard_SpiderQuestTurnIn]
        dialogue_topic: SpiderReward

        dialogue:
          - NPC: "Both of them? Hm. Didn't think you'd walk back. Here's your coin. Don't spend it where I can't see it."
          - [END]  [sets: DialogueFact_RacketGuard_SpiderQuestTurnIn]
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
  - [x] `DialogueFact_RacketGuard_SpiderQuestAccepted` — GUID `6dbfcecc138b29a42904b465db447573`
  - [x] `DialogueFact_RacketGuard_SpiderQuestTurnIn` — GUID `d526339be257ec5469d28593abdacafa`
  - [x] `QuestFact_SpiderClear_Completed` — GUID `9cc76620ed3adc444a154a4185a765db`
- [x] QuestSO created — `Assets/_Game/Data/Quests/Quest_SpiderClear.asset`
- [x] NPC_RacketGuard Memories and Dialogues folder scaffolded — `Assets/_Game/Data/NPCs/NPC_RacketGuard/`
  - [x] `Mem_RacketGuard_SpiderOffer` created and wired to NPC_RacketGuard.asset — GUID `6afbb16d35948ed4493a8f7cdccdd00a`
  - [x] `Mem_RacketGuard_SpiderQuestActive` created and wired — GUID `20cc73d53753bfa4dbd86dca14d513b6`
  - [x] `Mem_RacketGuard_SpiderReward` created and wired — GUID `f3673fbad53f1004a959f8f75032499c`
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_RacketGuard/Dialogues/SpiderOffer/` (Start + Choice)
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_RacketGuard/Dialogues/SpiderActive/` (Start + Text)
  - [x] Dialogue chains created — `Assets/_Game/Data/NPCs/NPC_RacketGuard/Dialogues/SpiderReward/` (Start + Text)
- [x] PlayerRewardSO asset created — `Assets/_Game/Data/Rewards/PlayerReward_SpiderClear_Completed.asset`
- [x] Quest registered in `QuestEventsManager._quests` (`Assets/_Game/Prefabs/QuestEventsManager.prefab`)
- [x] Quest registered in `QuestLogUI._allQuests` (`Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab`)
- [x] PlayerReward registered in `PlayerRewards._rewards` (`Assets/_Game/Prefabs/Player/Player.prefab`)
