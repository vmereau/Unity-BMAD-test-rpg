# Quest Spec — {QuestId}

> Template version: 1.1
> Status: `draft` | `ready` | `implemented`
> Spec path: `docs/World/Quests/{QuestId}.md`
> Design with `/quests:design`, build with `/quests:implement {QuestId}`, check with `/quests:audit {QuestId}`.

---

## Metadata

| Field | Value |
|---|---|
| `questId` | Unique PascalCase key — e.g. `FindHerbalist` (asset `Quest_{questId}`) |
| `title` | Display name shown in Quest Log |
| `description` | Full quest description shown in Quest Log (2–4 sentences). Counts in it ("kill five") should match a step's part count (validator V13) |

---

## Facts

List every Fact asset this quest depends on. Mark whether it needs to be created or already exists.
Who sets each type: `Assets/_Game/Data/Facts/CLAUDE.md`. **`WorldFact` has no setter in code yet** — a quest
part using one can never complete (V3) until a setter is built.

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| e.g. `DialogueFact_Herbalist_QuestDone` | `DialogueFact` | Set when `Start_Herbalist_QuestDone` plays, or a choice with this `dialogueFact` is picked |
| e.g. `KilledFact_BanditBoss` | `KilledFact` | Set when the scene object `BanditBoss` (its `PersistentID`) dies — one fact per entity |
| e.g. `QuestFact_FindHerbalist_Step0` | `QuestFact` | Computed from another quest's state / step |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| e.g. `DialogueFact_Guard_SpiderQuestAccepted` | `DialogueFact` | `Assets/_Game/Data/Facts/` |

---

## Quest Conditions

```
startPart:
  fact:  <FactAssetName>
  entry: "<text shown in Quest Log when quest is active>"

completedParts:     # ANY true → completed
  - fact:  <FactAssetName>
    entry: "<text shown in Quest Log>"

failedParts:        # ANY true → failed. Leave empty if quest cannot fail
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
        entry: "<text shown in Quest Log for this part>"   # may be empty (V8 Info)
      # All parts must be true for the step to be completed.
      # Any part true = step is shown as active in the log.
```

---

## Quest Dependencies

Other quests this one reads (via `QuestFact`), and quests / NPC memories that react to this one.

```
reads:        # e.g. startPart requires QuestFact_FindHerbalist_Completed
  - <QuestFactAssetName>: <why>
read_by:      # e.g. Mem_Elder_Thanks unlocks on QuestFact_{QuestId}_Completed
  - <asset>: <why>
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
      - asset: Mem_<NPC>_<Topic>               # saved in <NPCName>/Memories/ → auto-added to NPCEntity.memories
        unlock:     [<FactAssetName>, ...]     # ALL must be true (empty = always unlocked)
        invalidate: [<FactAssetName>, ...]     # ANY true → closed
        dialogue_topic: <TopicName>            # omit for a gate memory (no start dialogue)

        dialogue:
          # Write the chain as a script. Prefix lines with NPC: / Player: / [END]
          # Flag choices with →, note which DialogueFact (if any) each choice sets,
          # and which memory (if any) must be active for the choice to show.
          - NPC: "<speech line>"
          - Player: "<choice text>"  → <nextNode or END>  [sets: DialogueFact_X]  [requires: Mem_<NPC>_<Gate>]
          - Player: "<choice text>"  → <nextNode or END>
```

Gate memories (`requires:` above) must belong to the **same NPC** as the choice (validator V17), and every
memory needs a start dialogue or a gated choice (V15).

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

- [ ] Fact assets created — Dialogue / World / Quest facts in `Assets/_Game/Data/Facts/`; KilledFacts via
      `Game/World/Generate Missing KilledFacts` (`Data/Enemies/{Scene}/`) and wired on each `PersistentID`
- [ ] QuestSO created — `Assets/_Game/Data/Quests/{QuestId}/Quest_{QuestId}.asset`
- [ ] NPC exists or created — `Assets/_Game/Data/NPCs/<NPCName>/`
- [ ] Dialogue chains created — `Assets/_Game/Data/NPCs/<NPCName>/Dialogues/<Topic>/`
- [ ] Memory entries created under `Data/NPCs/<NPC>/Memories/` (auto-synced to `NPCEntity.memories`)
- [ ] PlayerRewardSO assets created — `Assets/_Game/Data/Rewards/` (auto-synced to `PlayerRewards`)
- [ ] Registered — `QuestEventsManager` (auto-synced) and `QuestLogUI._allQuests` (`QuestEditActions.AddToQuestLog`)
- [ ] `quest_report {QuestId}` shows no Error / Warning (or each remaining one is explained here)
