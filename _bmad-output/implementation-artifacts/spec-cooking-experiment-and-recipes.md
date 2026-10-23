---
title: 'Cooking v2: experiment tab, known-recipes tab, recipe facts, first foods'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: '8335fa0'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Crafting/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/Facts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Items/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cooking is one ingredient → one output, with nothing to discover or remember, and only one recipe exists.

**Approach:** The cooking screen gets two tabs. **Experiment** has 3 ingredient slots. A combination that exactly matches a station recipe gives that recipe's food and teaches the recipe permanently. Anything else gives a Replenishing Stew. **Recipes** lists the known recipes this station can cook; the player picks one and cooks it if the inventory holds its ingredients. Knowing a recipe is a stored `RecipeFact` in `WorldStateManager`, so it is saved for free and quests, memories and dialogue can read it. New foods: Apple, Bread, Brown Mushroom, Replenishing Stew, Apple Mushroom Sandwich (Apple + Bread + Brown Mushroom).

## Boundaries & Constraints

**Always:**
- A recipe has 1–3 ingredients. Matching ignores order and must be exact (same items, same counts). The same item can fill several slots if the inventory holds enough units.
- Experiment: Cook needs ≥1 filled slot and enough units for every slot. It consumes all slotted units and gives 1 result. If the result can't be added, everything is refunded (`GameLog.Warn`). Slots stay references and are re-checked after each cook; a slot whose item runs short clears.
- The experiment preview never reveals an unknown recipe: it shows the output only for a known matching recipe, otherwise "Unknown result".
- Discovery = `WorldStateManager.SetRecipeKnown(recipe.knownFact)`, only the first time; the result text shows "New recipe: <name>". The player starts with no known recipes; Healing Plant → Health Potion is discoverable too.
- `RecipeFact` key `Recipe.Known.{recipeId}`, stored (`_worldFacts`), raises `OnFactChanged`. No `SaveData` change.
- The Recipes tab shows `station recipes ∩ known`. A row shows the output icon and name; selecting it shows its ingredients as have/need. Cook is enabled only when every ingredient is available.
- The screen opens on the Experiment tab with empty slots. The inventory grid is shared by both tabs. Double-click fills the first empty experiment slot.
- Foods are `PotionItemSO`: consumable, stack 20. Heal amounts: Apple 8, Bread 10, Brown Mushroom 6, Stew 15, Sandwich 35. Health Potion `_healAmount` → 50.
- World prefabs follow `Prefabs/Items/CLAUDE.md`, with the HouseForge model nested.

**Ask First:** Changes to the `SaveData` format, `InventorySystem`, or `ItemSlotUI` / `ActionBarSlotUI` behaviour.

**Never:** Other ways to learn recipes (books, NPCs), a global recipe catalog, cooking time / VFX / XP, skill gates, new UI sprites, `generate_image`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Discover | Apple, Bread, Mushroom slotted (any order), sandwich unknown | Preview "Unknown result"; Cook → −1 each, +1 Sandwich, "New recipe: Apple Mushroom Sandwich", fact set | N/A |
| Known via experiment | Same, sandwich known | Preview shows Sandwich; Cook gives it, no "New recipe" | N/A |
| Failed experiment | Apple + Bread | −1 each, +1 Replenishing Stew | N/A |
| Duplicates | 2 slots = Apple, 1 Apple owned | Cook disabled, hint "Not enough ingredients" | N/A |
| Recipe tab | Sandwich known, no Mushroom | Row listed, Mushroom 0/1, Cook disabled | N/A |
| Nothing known | Fresh game | Recipes tab: "No known recipes" | N/A |
| Save / load | Learn sandwich, save, quit, load | Sandwich still known | N/A |
| Misconfigured recipe | `knownFact` null / 0 or >3 ingredients | Null fact: cooks, never learned or listed. Bad ingredient count: skipped | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Crafting/RecipeSO.cs`, `Scripts/Crafting/CookingRules.cs` -- recipe data + pure rules (currently 1→1)
- `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs`, `CookingIngredientSlotUI.cs`, `CookingInventorySideUI.cs` -- screen, reference slot, grid adapter
- `Assets/_Game/Prefabs/UI/Cooking/CookingUI.prefab` -- current layout (PlayerSide + IngredientColumn/OutputColumn + Cook)
- `Assets/_Game/ScriptableObjects/Facts/WorldFact.cs`, `Scripts/Core/State/WorldFactPrefix.cs`, `WorldStateManager.cs` -- stored-fact pattern, typed setters
- `Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs`, `QuestValidator.cs` -- setter attribution, `IsStored`
- `Assets/_Game/Data/Items/Item_HealingPlant.asset`, `Prefabs/Items/Item_HealingPlant.prefab` -- food item + world prefab template
- `Assets/Hivemind/HouseForge_01/HDRP (Default)/Art/Prefabs/Props/SM_FoodStandMeal_01b|SM_FoodKitApple_01a|SM_FoodKitBread_01a|SM_Mushroom_01d.prefab` -- models

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Facts/RecipeFact.cs`, `WorldFactPrefix.cs` (append `Recipe`), `WorldStateManager.cs` -- stored fact + `SetRecipeKnown` / `IsRecipeKnown` -- discovery as a fact
- [x] `RecipeSO.cs` -- `ingredients` (`ItemSO[]`), `output`, `knownFact`; `IsComplete` = output + 1–3 non-null ingredients -- multi-ingredient data
- [x] `CookingRules.cs` -- `FindRecipe(recipes, slots)` exact multiset; `HasIngredients(needs, countOf)`; experiment + recipe-tab evaluation -- testable logic
- [x] `CookingUI.cs` + `CookingIngredientSlotUI.cs` (slot index) + `CookingInventorySideUI.cs` + new `CookingRecipeRowUI.cs` -- tabs, 3 slots, experiment cook/learn, recipe list/detail/cook, `_failedDish` (Stew) -- screen
- [x] `CookingUI.prefab` -- tab bar (Experiment / Recipes), 3 slots, recipe list + detail panel, shared grid -- UI
- [x] `Editor/QuestExplorer/QuestReferenceIndex.cs`, `QuestValidator.cs` -- `RecipeSO.knownFact` counts as a setter; `RecipeFact` is stored -- no false "never set"
- [x] `Data/Items/Item_Apple|Item_Bread|Item_BrownMushroom|Item_ReplenishingStew|Item_AppleMushroomSandwich.asset` + `Prefabs/Items/Item_*.prefab`; `Item_Health_Potion` heal 50; sync `ItemCatalog` -- foods
- [x] `Data/Recipes/Recipe_HealthPotion.asset` (migrate), `Recipe_AppleMushroomSandwich.asset`, `Data/Facts/RecipeFact_*.asset`; add both to `CookingStation_Base._recipes` -- content
- [x] `Scenes/StartingTown.unity` -- 3 Apple, 3 Bread, 3 Mushroom pickups next to the cooking fireplace -- playtest
- [x] `Assets/Tests/EditMode/CookingRulesTests.cs` -- matrix rows (order, duplicates, partial, incomplete recipes, availability) -- regressions
- [x] `Scripts/Crafting/CLAUDE.md`, `Data/Facts/CLAUDE.md` (RecipeFact row + naming), `Editor/QuestExplorer/CLAUDE.md` if setters are listed -- docs

**Acceptance Criteria:**
- Given a recipe was discovered, when a quest part, memory condition or dialogue checks its `RecipeFact`, then `GetFact` returns true and Quest Explorer shows the recipe as its setter.
- Given the Recipes tab is open, when a recipe is cooked, then the inventory grid, the have/need counts and the HUD action bar refresh.
- Given the cooking screen, when it is closed and reopened, then it shows the Experiment tab with empty slots.

## Design Notes

A stored fact replaces both a player-side recipe list and a recipe catalog. `WorldStateManager` already saves, restores and broadcasts facts, and every condition system takes a `Fact`. Each recipe references its own `RecipeFact_<Name>` asset (`_recipeId` is a stable string, never renamed), mirroring `DialogueFact`. Restoring a save raises no fact events, which matches the "restore without events" rule.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass

**Manual checks:**
- Play StartingTown: pick up foods, run each matrix row at the fireplace, save/load, check the Recipes tab.

## Suggested Review Order

**Experiment cook and discovery**

- Entry point: match station recipe or fall back to the stew; learn the fact on first discovery.
  [`CookingUI.cs:232`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L232)

- Single craft path for both tabs: consume all, add result, refund everything on failure.
  [`CookingUI.cs:270`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L270)

- Preview never reveals an undiscovered recipe; Cook disabled without a match or failed dish.
  [`CookingUI.cs:350`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L350)

- Only `isIngredient` items enter the slots — no turning gear or lockpicks into stew.
  [`CookingUI.cs:192`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L192)

- After a cook, slots whose item ran short (duplicates counted) clear.
  [`CookingUI.cs:311`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L311)

**Pure rules**

- Exact order-free multiset match; empty slots ignored, incomplete recipes skipped.
  [`CookingRules.cs:20`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L20)

- Availability counts duplicate slots against inventory units.
  [`CookingRules.cs:33`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L33)

- Recipe data: 1–3 ingredients, output, `knownFact`.
  [`RecipeSO.cs:24`](../../Assets/_Game/ScriptableObjects/Crafting/RecipeSO.cs#L24)

**Known recipes as facts**

- Stored fact key `Recipe.Known.{id}` — saved, broadcast and readable by quests / memories for free.
  [`RecipeFact.cs:25`](../../Assets/_Game/ScriptableObjects/Facts/RecipeFact.cs#L25)

- Typed setter / getter, mirroring `SetDialoguePlayed`.
  [`WorldStateManager.cs:119`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L119)

- Quest Explorer: a recipe is its fact's setter, so gated quests don't read "never set".
  [`QuestReferenceIndex.cs:443`](../../Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs#L443)

- Recipe facts are stored (V3 orphan check, play-mode Toggle).
  [`QuestValidator.cs:263`](../../Assets/_Game/Editor/QuestExplorer/QuestValidator.cs#L263)

**Recipes tab**

- Station recipes ∩ known; first selected by default; have/need detail and Cook gate.
  [`CookingUI.cs:377`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L377)

- Row binding: output icon / name, dimmed when uncookable.
  [`CookingRecipeRowUI.cs:39`](../../Assets/_Game/Scripts/UI/Crafting/CookingRecipeRowUI.cs#L39)

- Tabs: open on Experiment, inactive tab tinted (no tab sprites).
  [`CookingUI.cs:173`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L173)

**Peripherals**

- Station authoring warnings: incomplete, non-ingredient, missing / empty / shared facts.
  [`CookingStation.cs:30`](../../Assets/_Game/Scripts/Crafting/CookingStation.cs#L30)

- Ingredient flag on items (a flag, not a subclass, so foods keep healing).
  [`ItemSO.cs:19`](../../Assets/_Game/ScriptableObjects/Items/ItemSO.cs#L19)

- Slot-to-slot drag moves the reference.
  [`CookingIngredientSlotUI.cs:65`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L65)

- Rule tests (order, duplicates, partial, incomplete, availability) and the index test.
  [`CookingRulesTests.cs:53`](../../Assets/Tests/EditMode/CookingRulesTests.cs#L53)
  [`QuestReferenceIndexTests.cs:227`](../../Assets/Tests/EditMode/QuestReferenceIndexTests.cs#L227)

- System doc: known recipes = stored facts.
  [`CLAUDE.md:19`](../../Assets/_Game/Scripts/Crafting/CLAUDE.md#L19)
