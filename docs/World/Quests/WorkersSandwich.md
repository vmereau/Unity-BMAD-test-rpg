# Quest Spec — WorkersSandwich

> Template version: 1.1
> Status: `ready`
> Spec path: `docs/World/Quests/WorkersSandwich.md`
> Design with `/quests:design`, build with `/quests:implement WorkersSandwich`, check with `/quests:audit WorkersSandwich`.
> Chain: `WorkersRation` → **WorkersSandwich** → `CooksExperiment` (Tob's cooking tutorial, npc-020).
> **Ready to implement** — all prerequisites done (2026-11-26): `CraftedFact`, items, recipe and recipe item.

---

## Metadata

| Field | Value |
|---|---|
| `questId` | `WorkersSandwich` |
| `title` | Bread, Onion and Pork |
| `description` | Tob gave me his recipe for the sandwich the workers carry to the quarry: bread, onion and salt pork. He sells the onion and the pork himself. I should cook one at a fire. |

---

## Prerequisites (outside the quest data)

1. ✅ **`CraftedFact`** **Done** (`spec-tob-cooking-prerequisites`). Computed fact type (`Game/Facts/Crafted Fact`, field `_recipe` = a `RecipeFact`), true when
   `WorldStateManager.GetRecipeCraftCount(recipe) > 0`. Key `Crafted.{recipeId}` (debug only). `AddRecipeCraft` must
   raise `OnFactChanged` with that key on the **first** craft (0 → 1). An Experiment cook that matches the recipe counts
   (it already calls `AddRecipeCraft`), so learning the sandwich by experimenting also sets it.
2. ✅ **New items** **Done** (2026-11-26) (`/items:create`, Meshy models + icons):
   - `Item_Onion`, `Item_SaltPork` — ingredients (`isIngredient`), sold by Tob (his shop, open once `WorkersRation` is completed; scene stock, `/NPC:shop-dialogue`). Built by `/items:create` (Onion 3 g / 1 g, Salt Pork 6 g / 2 g); Tob stock done (5 each, StartingTown; moved from the Storekeeper by `spec-tob-food-shop`); raw food (heal 4 / 8 over 20 / 30 min).
   - `Item_WorkersSandwich` — `PotionItemSO` Meal, heal ~25 over time, first-meal bonus **+5 Max HP** (decided 2026-11-26). Assets built by `/items:create`, 15 g / 7 g; heal 25 over 60 min.
3. ✅ **Recipe** **Done** (2026-11-26) — `Recipe_WorkersSandwich` (Bread + Onion + Salt Pork → `Item_WorkersSandwich`),
   `RecipeFact_WorkersSandwich` (`_recipeId` `WorkersSandwich`), added to `CookingStation_Base._recipes`.
4. ✅ **Recipe item** **Done** (2026-11-26; prefab copied from the Apple Trout recipe, its icon reused) — `Item_Recipe_WorkersSandwich` (`RecipeItemSO`), **not sold**: only given by Tob (reward below).

---

## Facts

### To create

| Asset name | Type | Key / trigger |
|---|---|---|
| `DialogueFact_KitchenHand_Sandwich_Offered` | `DialogueFact` | Set when `Start_KitchenHand_SandwichOffer` plays |
| `DialogueFact_KitchenHand_Sandwich_RecipeTaken` | `DialogueFact` | Choice "Show me." — triggers the recipe item reward |
| `DialogueFact_KitchenHand_Sandwich_AlreadyKnown` | `DialogueFact` | Gated choice "I already know this meal." |
| `DialogueFact_KitchenHand_Sandwich_Done` | `DialogueFact` | Choice "Thanks, Tob." in `SandwichReport` |
| `RecipeFact_WorkersSandwich` | `RecipeFact` | Experiment discovery or using `Item_Recipe_WorkersSandwich` (Prerequisite 3) |
| `CraftedFact_WorkersSandwich` | `CraftedFact` (new) | First successful cook of the sandwich |
| `QuestFact_WorkersSandwich_Started` | `QuestFact` | IsStarted |
| `QuestFact_WorkersSandwich_Step1` | `QuestFact` | Step 1 (cooked) |
| `QuestFact_WorkersSandwich_Completed` | `QuestFact` | IsCompleted — starts `CooksExperiment` |

### Already exists (reference only)

| Asset name | Type | Where it lives |
|---|---|---|
| `QuestFact_WorkersRation_Completed` | `QuestFact` | created by `WorkersRation` |

---

## Quest Conditions

```
startPart:
  fact:  DialogueFact_KitchenHand_Sandwich_Offered
  entry: "Tob wants to teach me the sandwich the workers carry to the quarry."

completedParts:
  - fact:  DialogueFact_KitchenHand_Sandwich_Done
    entry: "I cooked Tob's sandwich and he approved."
  - fact:  DialogueFact_KitchenHand_Sandwich_AlreadyKnown
    entry: "I already knew how to make Tob's sandwich."

failedParts: []
```

---

## Steps

```
steps:
  - title: "Learn the recipe"
    description: "Read the recipe Tob gave me."
    parts:
      - fact:  RecipeFact_WorkersSandwich
        entry: "I know how to make the Worker's Sandwich."
  - title: "Cook it"
    description: "Buy onion and salt pork from Tob, then cook the sandwich at a fire."
    parts:
      - fact:  CraftedFact_WorkersSandwich
        entry: "I cooked a Worker's Sandwich."
```

---

## Quest Dependencies

```
reads:
  - QuestFact_WorkersRation_Completed: unlocks Tob's offer
read_by:
  - Mem_KitchenHand_ExperimentOffer: unlocks on QuestFact_WorkersSandwich_Completed (starts CooksExperiment)
```

---

## NPC Involvement

```
npcs:
  - npc: KitchenHand (Tob)
    exists: true
    folder: Assets/_Game/Data/NPCs/KitchenHand/

    memories:
      - asset: Mem_KitchenHand_SandwichOffer
        unlock:     [QuestFact_WorkersRation_Completed]
        invalidate: [DialogueFact_KitchenHand_Sandwich_RecipeTaken, DialogueFact_KitchenHand_Sandwich_AlreadyKnown]
        # Not invalidated by _Offered: "Later." keeps the topic, so the recipe can't be lost.
        dialogue_topic: SomethingToCarry   # label "Something to carry"
        dialogue:
          - NPC: "Soup's fine, but a worker needs something to carry to the quarry."   [start node, sets: DialogueFact_KitchenHand_Sandwich_Offered]
          - NPC: "Bread, onion, salt pork. I sell the onion and the pork, now you've had your soup."
          - Player: "Show me."                  → Text_KitchenHand_SandwichGiven  [sets: DialogueFact_KitchenHand_Sandwich_RecipeTaken]
          - Player: "I already know this meal." → Text_KitchenHand_SandwichKnown  [sets: DialogueFact_KitchenHand_Sandwich_AlreadyKnown]  [requires: Mem_KitchenHand_Gate_SandwichKnown]
          - Player: "Later."                    → END
          # Text_KitchenHand_SandwichGiven:  NPC: "Here, I wrote it down. Any fire will do. Don't burn the onion."  → END
          # Text_KitchenHand_SandwichKnown:  NPC: "You do? Ha! Found it on your own. You've got the knack."     → END

      - asset: Mem_KitchenHand_Gate_SandwichKnown      # gate memory, no start dialogue
        unlock:     [RecipeFact_WorkersSandwich]
        invalidate: []

      - asset: Mem_KitchenHand_SandwichReport
        unlock:     [QuestFact_WorkersSandwich_Started, QuestFact_WorkersSandwich_Step1]
        invalidate: [QuestFact_WorkersSandwich_Completed]
        dialogue_topic: TheSandwich        # label "I made your sandwich"
        dialogue:
          - NPC: "You made it? Let me smell. Yes, that's right."
          - NPC: "That'll keep you on your feet till evening."
          - Player: "Thanks, Tob."  → END  [sets: DialogueFact_KitchenHand_Sandwich_Done]
```

---

## Rewards

```
rewards:
  onDialogue:                      # PlayerRewardSO, FactType = Dialogue
    - fact: DialogueFact_KitchenHand_Sandwich_RecipeTaken
      items: [Item_Recipe_WorkersSandwich]   # "Received: …" toast
  onStart: null
  onStepCompleted: []
  onCompleted:
    xp: 75
    lp: 0
    gold: 0
    stats: []
  onFailed: null
```

---

## Flow / edge cases

1. `WorkersRation` completed → *Something to carry* opens → start node → quest started.
2. "Show me." → recipe item given → player uses it → step 0 done.
3. Player buys onion + pork, cooks at any fire → `CraftedFact` → step 1 done → *I made your sandwich* opens.
4. "Thanks, Tob." → completed (75 XP) → `CooksExperiment` offer unlocks.

- **Already learned by experimenting** (Bread + Onion + Salt Pork before the offer): the gate opens, "I already know this
  meal." completes the quest at once (75 XP, no recipe item). Steps 0 and 1 are already true too (an experiment cook counts as a craft).
- "Later.": quest started, no recipe; the topic stays until a choice is made.
- Player learns the recipe by experimenting **after** "Show me.": both steps complete on their own, the recipe item
  can only be sold (`RecipeItemSO.CanUse` false).

---

## Implementation Checklist

- [ ] Prerequisites 1–4 done (`gds-quick-dev` spec + `/items:create` + Tob shop stock)
- [ ] Fact assets created — `Data/NPCs/KitchenHand/Facts/` (dialogue facts), `Data/Facts/` (Recipe / Crafted / Quest facts)
- [ ] QuestSO created — `Assets/_Game/Data/Quests/WorkersSandwich/Quest_WorkersSandwich.asset`
- [ ] Dialogue chains created — `Data/NPCs/KitchenHand/Dialogues/SomethingToCarry/`, `TheSandwich/`
- [ ] Memory entries created under `Data/NPCs/KitchenHand/Memories/`
- [ ] PlayerRewardSO created — `PlayerReward_WorkersSandwich_Recipe` (item), `PlayerReward_WorkersSandwich_Completed` (75 XP)
- [ ] Registered — `QuestEventsManager` (auto) and `QuestLogUI._allQuests`
- [ ] `quest_report WorkersSandwich` shows no Error / Warning
