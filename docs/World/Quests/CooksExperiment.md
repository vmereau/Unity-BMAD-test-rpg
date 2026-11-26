# Quest Spec — CooksExperiment

> Template version: 1.1
> Status: `ready`
> Spec path: `docs/World/Quests/CooksExperiment.md`
> Design with `/quests:design`, build with `/quests:implement CooksExperiment`, check with `/quests:audit CooksExperiment`.
> Chain: `WorkersRation` → `WorkersSandwich` → **CooksExperiment** (Tob's cooking tutorial, npc-020).
> **Ready to implement** — all prerequisites done (2026-11-26): `WorldFact_Cooking_MealDiscovered` setter, experiment-only Forager's Stew.

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `CooksExperiment` |
| `title` | Something New |
| `description` | Tob says a good cook finds dishes by mixing ingredients, not by reading recipes. He only gave me a hint: onion and pork aren't only good on bread, try them with something from the woods. I should experiment at a fire until I find a new dish. |

---

## Prerequisites (outside the quest data)

1. ✅ **`WorldFact_Cooking_MealDiscovered`** **Done** (`spec-tob-cooking-prerequisites`). The asset exists in `Data/Facts/` and is wired on `CookingUI.prefab`. Set by `CookingUI`'s Experiment tab the first time
   it **discovers** a recipe whose output is a Meal (`PotionItemSO.Kind == Meal`), i.e. the cook that shows "New recipe: …".
   Not set by a recipe item, by cooking an already-known recipe, by the failed dish or by a potion. Stored, so it's true
   for discoveries made **before** the quest. Serialized field on `CookingUI` (fact reference), Quest Explorer setter.
2. ✅ **An experiment-only dish** **Done** (2026-11-26) (otherwise a dead end, see *Flow*): proposed `Item_ForagersStew` (Salt Pork + Onion +
   Brown Mushroom), `Recipe_ForagersStew` + `RecipeFact_ForagersStew`, on `CookingStation_Base._recipes`, **no recipe
   item anywhere**. Meal, heal ~30, first-meal bonus **+1 Endurance** (confirmed 2026-11-26). Assets built by `/items:create`, 18 g / 8 g; heal 30 over 60 min; `Recipe_ForagersStew` + `RecipeFact_ForagersStew` on `CookingStation_Base`. Tob's hint points at it.

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_KitchenHand_Experiment_Offered` | `DialogueFact` | Set when `Start_KitchenHand_ExperimentOffer` plays |
| `DialogueFact_KitchenHand_Experiment_Accepted` | `DialogueFact` | Choice "Give me a hint." |
| `DialogueFact_KitchenHand_Experiment_AlreadyFound` | `DialogueFact` | Gated choice "I've already found a dish of my own." |
| `DialogueFact_KitchenHand_Experiment_Done` | `DialogueFact` | Choice "I'll keep trying things." in `ExperimentReport` |
| `QuestFact_CooksExperiment_Started` | `QuestFact` | IsStarted |
| `QuestFact_CooksExperiment_Completed` | `QuestFact` | IsCompleted |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `QuestFact_WorkersSandwich_Completed` | `QuestFact` | created by `WorkersSandwich` |
| `WorldFact_Cooking_MealDiscovered` | `WorldFact` | `Data/Facts/`, set by `CookingUI` (Prerequisite 1) |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_KitchenHand_Experiment_Offered
  entry: "Tob wants me to find a dish of my own by mixing ingredients at a fire."

completedParts:
  - fact:  DialogueFact_KitchenHand_Experiment_Done
    entry: "I found a new dish by experimenting, and Tob was pleased."
  - fact:  DialogueFact_KitchenHand_Experiment_AlreadyFound
    entry: "I had already found a dish of my own."

failedParts: []
```

---

## Steps

```
steps:
  - title: "Find a new dish"
    description: "At a fire, put ingredients together in the Experiment tab until something new comes out. Tob's hint: onion and pork, with something from the woods."
    parts:
      - fact:  WorldFact_Cooking_MealDiscovered
        entry: "I discovered a new dish."
```

---

## Quest Dependencies

```
reads:
  - QuestFact_WorkersSandwich_Completed: unlocks Tob's offer
read_by: []
```

---

## NPC Involvement

```
npcs:
  - npc: KitchenHand (Tob)
    exists: true
    folder: Assets/_Game/Data/NPCs/KitchenHand/

    memories:
      - asset: Mem_KitchenHand_ExperimentOffer
        unlock:     [QuestFact_WorkersSandwich_Completed]
        invalidate: [DialogueFact_KitchenHand_Experiment_Accepted, DialogueFact_KitchenHand_Experiment_AlreadyFound]
        dialogue_topic: MixingThings       # label "Mixing things"
        dialogue:
          - NPC: "Recipes are fine. But a good cook finds dishes by mixing things, not reading them."   [start node, sets: DialogueFact_KitchenHand_Experiment_Offered]
          - NPC: "Put three things together at a fire and see what comes out. Burn a few. Everyone does."
          - Player: "Give me a hint."                      → Text_KitchenHand_ExperimentHint  [sets: DialogueFact_KitchenHand_Experiment_Accepted]
          - Player: "I've already found a dish of my own." → Text_KitchenHand_ExperimentKnown [sets: DialogueFact_KitchenHand_Experiment_AlreadyFound]  [requires: Mem_KitchenHand_Gate_MealDiscovered]
          - Player: "Later."                               → END
          # Text_KitchenHand_ExperimentHint:  NPC: "Onion and pork aren't only good on bread. Try them with something from the woods."  → END
          # Text_KitchenHand_ExperimentKnown: NPC: "Already? Then you don't need me. Keep mixing."  → END

      - asset: Mem_KitchenHand_Gate_MealDiscovered    # gate memory, no start dialogue
        unlock:     [WorldFact_Cooking_MealDiscovered]
        invalidate: []

      - asset: Mem_KitchenHand_ExperimentReport
        unlock:     [QuestFact_CooksExperiment_Started, WorldFact_Cooking_MealDiscovered]
        invalidate: [QuestFact_CooksExperiment_Completed]
        dialogue_topic: FoundADish         # label "I found a dish"
        dialogue:
          - NPC: "You found one yourself? Told you. Mix, taste, remember."
          - NPC: "And eat it with care the first time. Something new does the body good."
          - NPC: "The Storekeeper sells written recipes too, if you'd rather read than burn."
          - Player: "I'll keep trying things."  → END  [sets: DialogueFact_KitchenHand_Experiment_Done]
```

---

## Rewards

```
rewards:
  onStart: null
  onStepCompleted: []
  onCompleted:
    xp: 100
    lp: 0
    gold: 0
    stats: []
  onFailed: null
```

---

## Flow / edge cases

1. `WorkersSandwich` completed → *Mixing things* opens → start node → quest started.
2. "Give me a hint." → player experiments (Pork + Onion + Mushroom → Forager's Stew, or any undiscovered meal) →
   `WorldFact_Cooking_MealDiscovered` → step done → *I found a dish* opens.
3. "I'll keep trying things." → completed (100 XP).

- **Already discovered a meal before** (any time, e.g. Apple Mushroom Sandwich found by experimenting, or the
  Worker's Sandwich found before `WorkersSandwich`): the gate opens, "I've already found a dish of my own." completes the quest at once.
- **Dead end without Prerequisite 2:** a player who bought both Storekeeper recipe items (Apple Mushroom, Apple Trout)
  and got the Worker's Sandwich from Tob would have no meal left to discover. The experiment-only Forager's Stew
  guarantees one.
- "Later.": quest started, the topic stays until a choice is made. The step can still complete in the meantime;
  the report topic then opens alongside it.

---

## Implementation Checklist

- [ ] Prerequisites 1–2 done (`gds-quick-dev` spec + `/items:create`)
- [ ] Fact assets created — `Data/NPCs/KitchenHand/Facts/` (dialogue facts), `Data/Facts/` (World / Quest facts)
- [ ] QuestSO created — `Assets/_Game/Data/Quests/CooksExperiment/Quest_CooksExperiment.asset`
- [ ] Dialogue chains created — `Data/NPCs/KitchenHand/Dialogues/MixingThings/`, `FoundADish/`
- [ ] Memory entries created under `Data/NPCs/KitchenHand/Memories/`
- [ ] PlayerRewardSO created — `PlayerReward_CooksExperiment_Completed` (100 XP)
- [ ] Registered — `QuestEventsManager` (auto) and `QuestLogUI._allQuests`
- [ ] `quest_report CooksExperiment` shows no Error / Warning
