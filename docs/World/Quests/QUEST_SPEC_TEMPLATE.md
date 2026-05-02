# Quest Spec — {QuestId}

> Template version: 1.0
> Status: `draft` | `ready` | `implemented`
> Spec path: `docs/Quests/{QuestId}.md`

---

## Metadata

| Field | Value |
|---|---|
| `questId` | Unique PascalCase key — e.g. `FindHerbalist` |
| `title` | Display name shown in Quest Log |
| `description` | Full quest description shown in Quest Log (2–4 sentences) |

---

## Facts

List every Fact asset this quest depends on. Mark whether it needs to be created or already exists.

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| e.g. `WorldFact_HerbalistMet` | `WorldFact` | Set by dialogue when player first speaks to Herbalist |
| e.g. `DialogueFact_HerbalistDone` | `DialogueFact` | Set when `Start_Herbalist_QuestDone` node is played |
| e.g. `KilledFact_BanditBoss` | `KilledFact` | Set by EnemyController on boss kill |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| e.g. `WorldFact_TownGateOpened` | `WorldFact` | `Assets/_Game/Data/Facts/` |

---

## Quest Conditions

```
startPart:
  fact:  <FactAssetName>
  entry: "<text shown in Quest Log when quest is active>"

completedParts:
  - fact:  <FactAssetName>
    entry: "<text shown in Quest Log>"

failedParts:        # leave empty if quest cannot fail
  - fact:  <FactAssetName>
    entry: "<text shown in Quest Log>"
```

---

## Steps

```
steps:
  - title: "<step title>"
    description: "<step objective description>"
    parts:
      - fact:  <FactAssetName>
        entry: "<text shown in Quest Log for this part>"
      # All parts must be true for the step to be completed.
      # Any part true = step is shown as active in the log.
```

---

## NPC Involvement

One block per NPC. If the NPC already exists, note its folder path.

```
npcs:
  - npc: <NPCName>
    exists: true | false
    folder: Assets/_Game/Data/NPCs/<NPCName>/    # if exists
    identity_notes: "<brief notes if NPC needs to be created>"

    memories:
      - asset: Mem_<NPC>_<Topic>
        unlock:     [<FactAssetName>, ...]   # ALL must be true
        invalidate: [<FactAssetName>, ...]   # ANY true → closed
        dialogue_topic: <TopicName>

        dialogue:
          # Write the chain as a script. Prefix lines with NPC: / Player: / [END]
          # Flag choices with →, note which DialogueFact (if any) each choice sets.
          - NPC: "<speech line>"
          - Player: "<choice text>"  → <nextNode or END>  [sets: DialogueFact_X]
          - Player: "<choice text>"  → <nextNode or END>
```

---

## Rewards

```
rewards:
  onStart:            # null if none
    xp: 0
    lp: 0
    gold: 0
    stats: []         # e.g. [{stat: Strength, points: 1}]

  onStepCompleted:    # one block per step index
    - stepIndex: 0
      xp: 0
      lp: 0
      gold: 0
      stats: []

  onCompleted:
    xp: 0
    lp: 0
    gold: 0
    stats: []

  onFailed:           # null if none
    xp: 0
    lp: 0
    gold: 0
    stats: []
```

---

## Implementation Checklist

Fill in asset paths as they are created.

- [ ] Fact assets created — `Assets/_Game/Data/Facts/`
- [ ] QuestSO created — `Assets/_Game/Data/Quests/Quest_{QuestId}.asset`
- [ ] NPC exists or created — `Assets/_Game/Data/NPCs/<NPCName>/`
- [ ] Memory entries created and wired to NPCDataSO
- [ ] Dialogue chains created — `Assets/_Game/Data/NPCs/<NPCName>/Dialogues/<Topic>/`
- [ ] PlayerRewardSO assets created — `Assets/_Game/Data/Rewards/`
- [ ] Quest registered in `QuestEventsManager._quests`
- [ ] Quest registered in `QuestLogUI._allQuests`
