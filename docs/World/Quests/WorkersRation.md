# Quest Spec — WorkersRation

> Template version: 1.1
> Status: `implemented`
> Spec path: `docs/World/Quests/WorkersRation.md`
> Design with `/quests:design`, build with `/quests:implement WorkersRation`, check with `/quests:audit WorkersRation`.
> Chain: **WorkersRation** → `WorkersSandwich` → `CooksExperiment` (Tob's cooking tutorial, npc-020).
> **Implemented** 2026-11-26 (`/quests:implement`). `quest_report WorkersRation`: no Error / Warning (Info V13 only: "two bowls" in the description is not a step count).

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `WorkersRation` |
| `title` | A Bowl of Soup |
| `description` | Tob, the kitchen hand at the canteen, hands every worker two bowls of soup a day. He says the first bowl of something new does the body the most good. Eat the Worker's Soup and tell him how it sat. |

---

## Prerequisites (outside the quest data)

1. ✅ **`MealEatenFact`** **Done** (`spec-tob-cooking-prerequisites`). Computed fact type (`Game/Facts/Meal Eaten Fact`, field `_meal` = a `PotionItemSO`), true
   when `WorldStateManager.IsMealBonusClaimed(meal)`. Key `Meal.Eaten.{itemId}` (debug only). `ClaimMealBonus` must
   raise `OnFactChanged` with that key so quest parts and memories update. Only meaningful for a meal with a
   first-meal bonus (no bonus → never claimed → never true): `Awake`/validator warning when `_meal` has none.
   Quest Explorer: show the meal as the setter (like `RecipeFact`).
2. ✅ **`Item_WorkersSoup`** **Done** (2026-11-26) — new `PotionItemSO`, kind `Meal`, not an ingredient, heal ~15 over time (`SE_WellFed`),
   first-meal bonus **+5 Max HP**, not sold by anyone (only Tob's ration), no recipe. Icon + world prefab built by `/items:create`, 6 g / 3 g; heal 15 over 60 min.
3. ✅ **Daily ration give** **Done** (`spec-tob-cooking-prerequisites`). Built as a `DailyRationDialogueNode` topic (text "My daily ration", `item`, `perDay` 2,
   `noneLeftLine`, `notNowLine`): a dialogue topic that gives an item a limited number of times per day:
   - Tob's topic *"My daily ration (2 left)"* / *"(1 left)"*; the count is in the label.
   - 2 bowls per day; the count resets **when Tob wakes up at the end of his routine sleep** (not at midnight).
   - Refused while Tob is awake because the player woke him (line: "Not at this hour."), and while none are left
     ("That's your two for today.").
   - The remaining count is saved.
   - Generic (per NPC, item + count), not soup-specific.
4. ✅ **Tob placed in StartingTown** **Done** (2026-11-26). `KitchenHand` (NPC_base Variant) by the cookhouse fire, `PersistentID` + `KilledFact_KitchenHand`; routine 05–07 kitchen garden (farm), 07–21 cookhouse fire, 21–05 asleep in `KitchenHandBed` (cookhouse). Wake hour 05:00.

All four prerequisites are done. Shared with `WorkersSandwich` / `CooksExperiment`
prerequisites (same spec recommended).

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_KitchenHand_Ration_Offered` | `DialogueFact` | Set when `Start_KitchenHand_RationOffer` plays |
| `DialogueFact_KitchenHand_Ration_Done` | `DialogueFact` | Choice "Good to know." in `RationReport` |
| `MealEatenFact_WorkersSoup` | `MealEatenFact` (new) | First eat of `Item_WorkersSoup` (bonus claimed) |
| `QuestFact_WorkersRation_Started` | `QuestFact` | IsStarted |
| `QuestFact_WorkersRation_Step0` | `QuestFact` | Step 0 (soup eaten) |
| `QuestFact_WorkersRation_Completed` | `QuestFact` | IsCompleted — starts `WorkersSandwich` |

### Already exists (reference only)

None.

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_KitchenHand_Ration_Offered
  entry: "Tob at the canteen gives every worker a ration of soup. He wants me to eat my first bowl hot."

completedParts:
  - fact:  DialogueFact_KitchenHand_Ration_Done
    entry: "Tob explained it: the first time you eat something new, it does the body the most good."

failedParts: []
```

---

## Steps

```
steps:
  - title: "Eat the soup"
    description: "Ask Tob for my daily ration and eat a bowl of the Worker's Soup."
    parts:
      - fact:  MealEatenFact_WorkersSoup
        entry: "I ate my first bowl of the Worker's Soup."
```

---

## Quest Dependencies

```
reads: []
read_by:
  - Mem_KitchenHand_SandwichOffer: unlocks on QuestFact_WorkersRation_Completed (starts WorkersSandwich)
```

---

## NPC Involvement

```
npcs:
  - npc: KitchenHand (Tob)
    exists: true
    folder: Assets/_Game/Data/NPCs/KitchenHand/

    memories:
      - asset: Mem_KitchenHand_RationOffer
        unlock:     []
        invalidate: [DialogueFact_KitchenHand_Ration_Offered]
        dialogue_topic: NewHere            # topic label "New here?"
        # Tob's first topic. Replaces the greeting-opener pattern: a tutorial giver has no hostile branch
        # that could lock the cooking chain away.
        dialogue:
          - NPC: "New face! You look like you've been eating dust."      [start node, sets: DialogueFact_KitchenHand_Ration_Offered]
          - NPC: "The Company feeds its workers. Two bowls of soup a day. Come and get yours."
          - NPC: "Eat the first one hot. You'll feel it, I promise."
          - [END]

      - asset: Mem_KitchenHand_Ration
        unlock:     [DialogueFact_KitchenHand_Ration_Offered]
        invalidate: []                     # permanent: the daily ration stays after the quest
        dialogue_topic: DailyRation        # label "My daily ration (N left)", see Prerequisite 3
        dialogue:
          - NPC: "Here. Still hot."                                        [gives Item_WorkersSoup, 1 of 2 today]
          - [END]
          # none left today:   NPC: "That's your two for today. Come back tomorrow."
          # Tob woken up:      NPC: "Not at this hour. Let me sleep."

      - asset: Mem_KitchenHand_RationReport
        unlock:     [QuestFact_WorkersRation_Started, QuestFact_WorkersRation_Step0]
        invalidate: [QuestFact_WorkersRation_Completed]
        dialogue_topic: AboutTheSoup       # label "About the soup"
        dialogue:
          - NPC: "Well? Feel that? That's the first bowl doing its work."
          - NPC: "Same gruel every day and the body forgets how to be strong. Eat something new and it remembers."
          - NPC: "The first time does the most good. After that it's just a meal."
          - Player: "Good to know."  → END  [sets: DialogueFact_KitchenHand_Ration_Done]
```

Writing rules: no em-dashes, no game terms ("permanent", "Max HP", "bonus"), never mention Aldric's business.

---

## Rewards

```
rewards:
  onStart: null
  onStepCompleted: []
  onCompleted:
    xp: 50
    lp: 0
    gold: 0
    stats: []
  onFailed: null
```

The +5 Max HP comes from the soup itself (first-meal bonus), not from the quest.

---

## Flow / edge cases

1. Player talks to Tob → *New here?* plays → quest started, ration topic opens.
2. *My daily ration* → soup in the inventory (2 per Tob-day).
3. Player eats it → "Permanent bonus: +5 Max HP" toast → `MealEatenFact_WorkersSoup` → step 0 done → *About the soup* opens.
4. *About the soup* → "Good to know." → quest completed (50 XP) → `WorkersSandwich` offer unlocks.

- The soup can only come from the ration, which opens with the quest, so it can't be eaten before the quest starts.
- Player eats the soup without talking back: the step shows as done, the report topic waits.
- Player declines nothing here; there is no way to lock the chain.

---

## Implementation Checklist

- [x] Prerequisites 1–4 done (`spec-tob-cooking-prerequisites`, Tob placed)
- [x] Fact assets created
  - `Assets/_Game/Data/NPCs/KitchenHand/Facts/DialogueFact_KitchenHand_Ration_Offered.asset`
  - `Assets/_Game/Data/NPCs/KitchenHand/Facts/DialogueFact_KitchenHand_Ration_Done.asset`
  - `Assets/_Game/Data/Facts/MealEatenFact_WorkersSoup.asset` (`_meal` = `Item_WorkersSoup`)
  - `Assets/_Game/Data/Facts/QuestFact_WorkersRation_Started.asset`, `_Step0.asset`, `_Completed.asset`
- [x] QuestSO created: `Assets/_Game/Data/Quests/WorkersRation/Quest_WorkersRation.asset`
- [x] Dialogue chains created
  - `Data/NPCs/KitchenHand/Dialogues/NewHere/`: `Start_KitchenHand_NewHere` (sets `_Ration_Offered`) → `Text_KitchenHand_NewHere` → `_2` → `_3`
  - `Data/NPCs/KitchenHand/Dialogues/DailyRation/`: `Start_KitchenHand_DailyRation` (`DailyRationDialogueNode`, `Item_WorkersSoup`, 2 per day) → `Text_KitchenHand_DailyRation`
  - `Data/NPCs/KitchenHand/Dialogues/AboutTheSoup/`: `Start_KitchenHand_AboutTheSoup` → `Text_KitchenHand_AboutTheSoup` → `_2` → `Choice_KitchenHand_AboutTheSoup` ("Good to know." sets `_Ration_Done`)
- [x] Memory entries created under `Data/NPCs/KitchenHand/Memories/`: `Mem_KitchenHand_RationOffer`, `Mem_KitchenHand_Ration`, `Mem_KitchenHand_RationReport` (synced into `NPC_KitchenHand.memories`)
- [x] PlayerRewardSO created: `Data/Rewards/PlayerReward_WorkersRation_Completed.asset` (50 XP, synced to `Player.prefab`)
- [x] Registered: `QuestEventsManager` (auto) and `QuestLogUI._allQuests`
- [x] `quest_report WorkersRation` shows no Error / Warning
