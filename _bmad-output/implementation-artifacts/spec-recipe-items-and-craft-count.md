---
title: 'Cooking v3: recipe items, recipe status in item details, craft count'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: '7ce79ef'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Crafting/CLAUDE.md'
  - '{project-root}/Assets/_Game/ScriptableObjects/Items/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Inventory/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The only way to learn a recipe is to discover it in the Experiment tab. Item details don't show anything that depends on the player, and the cooking screen doesn't record how often a recipe was cooked.

**Approach:** Add a `RecipeItemSO`, a usable item that teaches a `RecipeSO` by setting its `knownFact`, and is consumed when used. The Merchant sells "Recipe: Apple Mushroom Sandwich". The item detail panel shows what the recipe teaches and whether it is already known. A per-recipe craft counter is stored in `WorldStateManager`, saved in `SaveData`, and shown in the Recipes tab.

## Boundaries & Constraints

**Always:**
- Using a recipe item whose recipe is unknown → `SetRecipeKnown`, the item is consumed, and a toast shows "Recipe learned: <output name>". Using one whose recipe is already known → `OnUse` returns false, nothing is consumed and no toast shows.
- The Use button (detail actions) and the context-menu Use are shown but not interactable when the recipe is known. Action bar / double-click go through `OnUse` and simply do nothing. Selling still works.
- Detail rows for a recipe item: `Teaches` = output name; status row "Already known" (negative polarity) or "Not learned" (positive). Category "Recipe". This applies in every host (inventory, trade, container) through `ItemDetailFormatter`, which stays pure: the known check is injected.
- Craft count: +1 per successful cook of a recipe's output (Recipes tab or a matching Experiment cook); a failed-dish cook doesn't count, and neither does a refunded craft. The key is the recipe's `knownFact` (no fact → not counted). The Recipes-tab detail shows "Cooked N times" ("Never cooked" for 0, "Cooked once" for 1).
- Counts are saved as a new optional `SaveData.recipeCrafts` (`Dictionary<string,int>`). A missing / null value (older save) → empty. No `SAVE_FORMAT_VERSION` bump. Restoring raises no events.
- Item: `Item_Recipe_AppleMushroomSandwich`, "Recipe: Apple Mushroom Sandwich", buy 40 / sell 10, stack 10, consumable, not an ingredient. The world prefab nests `Multistory Dungeons 2/.../Paper_01.prefab` per `Prefabs/Items/CLAUDE.md`. No icon (gray placeholder); give the user an icon prompt.
- Merchant stock +2 via `NpcShopEditor.SetStock` (StartingTown scene instance).

**Ask First:** Changes to `InventorySystem` or to existing save fields; bumping the save version.

**Never:** Recipe items that need skills / LP, recipe items as loot, `generate_image`, new UI sprites, prefab layout changes beyond the Recipes-tab count text.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Learn | Recipe item used, sandwich unknown | Fact set, −1 item, toast, Recipes tab lists sandwich | N/A |
| Already known | Recipe item, sandwich known | Detail "Already known", Use disabled; action bar use → nothing consumed | N/A |
| Shop preview | Merchant trade, select recipe | Price 40g, Teaches + status rows | N/A |
| Count | Cook sandwich twice (tab or experiment) | "Cooked 2 times" | N/A |
| Failed dish | Apple + Bread | Stew, no count change | N/A |
| Save / load | Count 2, save, cook once, load | "Cooked 2 times" | N/A |
| Old save | File without `recipeCrafts` | Loads, all counts 0 | N/A |
| Misconfigured item | `_recipe` null or no `knownFact` | Use rejected, no status row | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs`, `SkillItemSO.cs` -- usable base + closest analog
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `SetRecipeKnown` / `IsRecipeKnown`, `CaptureFacts` / `RestoreFacts`
- `Assets/_Game/Scripts/Core/Save/SaveData.cs`, `SaveSystem.cs` (`BuildSaveData` ~173, `BeginLoad` ~354) -- save model
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs`, `ItemDetailPanelUI.cs` -- category / rows (pure) + painter
- `Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs`, `InventoryUI.cs` (`ShowContextMenu`) -- Use gating
- `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs` -- `CookExperiment`, `CookSelectedRecipe`, `RefreshRecipes`
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- per-event toasts (`GameEventSO_String`)
- `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- Merchant stock

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/UsableItemSO.cs` -- add `public virtual bool CanUse() => true;` -- UI gating without a user
- [x] `ScriptableObjects/Items/RecipeItemSO.cs` (new) -- `_recipe` (`RecipeSO`), `_onRecipeLearned` (`GameEventSO_String`); `IsKnown`, `CanUse`, `OnUse` -- the item
- [x] `Scripts/Core/State/WorldStateManager.cs` -- `AddRecipeCraft(RecipeFact)`, `GetRecipeCraftCount(RecipeFact)`, `CaptureRecipeCrafts()`, `RestoreRecipeCrafts(dict)` -- counter store
- [x] `Scripts/Core/Save/SaveData.cs`, `SaveSystem.cs` -- `recipeCrafts` capture / restore (null-tolerant) -- persistence
- [x] `Scripts/UI/Inventory/ItemDetailFormatter.cs` + `ItemDetailPanelUI.cs` -- Recipe category, Teaches + status rows via injected `Func<RecipeFact,bool>` -- conditional info
- [x] `Scripts/UI/Inventory/InventoryDetailActions.cs`, `InventoryUI.cs` -- Use `interactable = usable.CanUse()` -- no wasted use
- [x] `Scripts/UI/Crafting/CookingUI.cs` + `CookingUI.prefab` -- count after a successful recipe cook; `_recipeCraftCountText` under the recipe detail -- tracking
- [x] `Scripts/UI/HUD/NotificationToastUI.cs` + `Data/Events/` new `GameEventSO_String` `OnRecipeLearned` wired on the HUD prefab -- toast
- [x] `Data/Items/Item_Recipe_AppleMushroomSandwich.asset`, `Prefabs/Items/Item_Recipe_AppleMushroomSandwich.prefab`, sync `ItemCatalog`; Merchant stock 2 -- content
- [x] `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` (+ craft-count / recipe-item tests where pure) -- matrix rows
- [x] `Scripts/Crafting/CLAUDE.md`, `ScriptableObjects/Items/CLAUDE.md`, `Data/Facts/CLAUDE.md` (second RecipeFact setter) -- docs

**Acceptance Criteria:**
- Given the recipe was learned from the item, when Quest Explorer lists the `RecipeFact` setters, then it isn't reported as "never set".
- Given the inventory detail panel shows a recipe item, when the item is used, then the panel and grid refresh (no stale status or count).
- Given a new game, when the Recipes tab opens after learning a recipe, then it shows "Never cooked".

## Design Notes

`CanUse()` takes no argument because `InventoryDetailActions.Bind` has no player reference, and `RecipeItemSO` reads `WorldStateManager.Instance`. The formatter receives `isRecipeKnown` so its tests run without a scene. Counts live next to the facts, keyed by `RecipeFact.ToString()`, which is the same stable id. A separate dictionary keeps `_worldFacts` bool-only, so every fact consumer is left untouched.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass

**Manual checks:**
- Play StartingTown: buy the recipe from the Merchant, check the detail rows, use it, cook twice, save / load, try to use a second copy.

## Suggested Review Order

**Recipe item**

- Entry point: learn on use, reject (keep the item) when already known.
  [`RecipeItemSO.cs:31`](../../Assets/_Game/ScriptableObjects/Items/RecipeItemSO.cs#L31)

- Configured = complete recipe + non-empty recipe id (review fix: no shared empty key).
  [`RecipeItemSO.cs:26`](../../Assets/_Game/ScriptableObjects/Items/RecipeItemSO.cs#L26)

- `CanUse` hook on the base: UI disables Use, `OnUse` still guards itself.
  [`UsableItemSO.cs:19`](../../Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs#L19)
  [`RecipeItemSO.cs:29`](../../Assets/_Game/ScriptableObjects/Items/RecipeItemSO.cs#L29)

- Detail and context-menu Use buttons follow `CanUse`.
  [`InventoryDetailActions.cs:54`](../../Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs#L54)
  [`InventoryUI.cs:234`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L234)

**Conditional item info**

- Teaches + Status rows; known state injected so the formatter stays pure.
  [`ItemDetailFormatter.cs:83`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L83)

- Panel injects the WorldStateManager check; no status row without Core.
  [`ItemDetailPanelUI.cs:120`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs#L120)

**Craft count + persistence**

- Int store beside the facts, keyed by the RecipeFact key.
  [`WorldStateManager.cs:134`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L134)

- Restore is null-tolerant (older saves → 0), no events.
  [`WorldStateManager.cs:226`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L226)

- Optional save field, captured and restored next to the facts.
  [`SaveData.cs:100`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L100)
  [`SaveSystem.cs:194`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L194)
  [`SaveSystem.cs:370`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L370)

- Counted only after a successful craft, in both tabs; failed dish never counts.
  [`CookingUI.cs:255`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L255)
  [`CookingUI.cs:270`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L270)

- Display text under the recipe detail.
  [`CookingUI.cs:422`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L422)

**Peripherals**

- Toast "Recipe learned: …" on the new `OnRecipeLearned` channel.
  [`NotificationToastUI.cs:109`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L109)

- Quest Explorer setter label mentions recipe items.
  [`QuestReferenceIndex.cs:452`](../../Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs#L452)

- Tests: item use, craft counts, formatter rows, older-save JSON.
  [`RecipeItemTests.cs:94`](../../Assets/Tests/EditMode/RecipeItemTests.cs#L94)
  [`ItemDetailFormatterTests.cs:330`](../../Assets/Tests/EditMode/ItemDetailFormatterTests.cs#L330)
  [`SaveDataSerializationTests.cs:59`](../../Assets/Tests/EditMode/SaveDataSerializationTests.cs#L59)
