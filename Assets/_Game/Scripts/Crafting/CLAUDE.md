# CLAUDE.md — Assets/_Game/Scripts/Crafting

> Loaded when Claude accesses files in this folder. Recipes and cooking stations. Namespace: `Game.Crafting`.
> The cooking screen lives in `Scripts/UI/Crafting/` (`Game.UI`); the station prefab contract in `Prefabs/CLAUDE.md`.

---

## What's here

| File | Role |
|------|------|
| `RecipeSO` (`ScriptableObjects/Crafting/`) | 1–3 `ingredients` (order-free; repeat an item to need several) → one `output`, plus `knownFact` (`RecipeFact`). Assets in `Data/Recipes/` (`Recipe_<Output>`), facts in `Data/Facts/` (`RecipeFact_<Output>`). |
| `CookingRules` | **Static, pure.** `FindRecipe(recipes, slots)` (exact multiset match, empty slots / null / incomplete recipes skipped), `HasIngredients(needs, countOf)` (duplicates counted), `EvaluateExperiment(slots, countOf)` → `ExperimentState` `Empty` / `NotEnough` / `Ready`. Tested by `CookingRulesTests`. |
| `CookingStation` | `IInteractable` on the station root: prompt `Cook`, name tag `Cooking station`, `_recipes` (`RecipeSO[]` = what can be cooked here; `Awake` warns on incomplete recipes / missing `knownFact`). `Interact()` raises `OnCookingStationOpenRequested` (`GameEventSO_CookingStationOpenRequest { recipes, stationName }`). Behaviour only — the look is the prefab's `Model` child. |
| `CookingSystem` | Player-side listener on `Player.prefab` (next to `ContainerSystem`) → `CookingUI.Open(recipes, name)`. Future gates (skills, tools) go here, not on the station. |

---

## Known recipes = stored facts

- Knowing a recipe is the stored `RecipeFact` (`Recipe.Known.{recipeId}`) in `WorldStateManager` — no player-side list,
  no recipe catalog, no `SaveData` field: facts are already saved / restored, and quest parts, memory conditions and dialogue
  read it like any `Fact` (`PlayerRewardSO` does not — its `RewardFactType` has no Recipe entry yet). Write it only through `WorldStateManager.SetRecipeKnown`; read with `IsRecipeKnown`.
- Setters: the Experiment tab's first successful cook of a recipe (Quest Explorer shows the recipe as the fact's
  `[Recipe]` setter) and **recipe items** (`RecipeItemSO`, e.g. `Item_Recipe_AppleMushroomSandwich` sold by the
  Merchant): `OnUse` sets the fact, raises `OnRecipeLearned` (toast "Recipe learned: …") and is consumed; it is
  rejected (`CanUse()` false → Use disabled, item kept, only sellable) once the recipe is known. Quest Explorer
  doesn't list recipe items as setters yet (the recipe is enough to avoid "never set").
- `RecipeFact._recipeId` is the save key — never rename it once shipped. It also keys the craft count.

## Craft count (saved, not a fact)

- `WorldStateManager.AddRecipeCraft` / `GetRecipeCraftCount` (keyed by `RecipeFact.ToString()`), a separate int store
  so facts stay bool-only. Saved as the optional `SaveData.recipeCrafts` (`CaptureRecipeCrafts` / `RestoreRecipeCrafts`;
  null in older saves → all 0, no version bump). No event.
- `CookingUI` counts every successful cook of a recipe's output (Recipes tab **or** a matching Experiment cook — not
  the failed dish, not a refunded craft) and shows `FormatCraftCount` ("Never cooked" / "Cooked once" / "Cooked N
  times") in `_recipeCraftCountText` under the recipe detail.

## First-meal bonus

A cooked food can grant a one-time permanent bonus (e.g. the sandwich: +5 Max HP) the first time it is eaten — data on
the food item, not on the recipe, so a bought / looted copy counts too. Details: `ScriptableObjects/Items/CLAUDE.md`
(`PotionItemSO`); claim key in `Data/Facts/CLAUDE.md`.

---

## Cooking screen (`Scripts/UI/Crafting/`)

- `CookingUI` (`IScreenPanel`, nested in `UICanvas`, inactive): shared player grid + **Experiment** / **Recipes** tabs
  (opens on Experiment, inactive tab tinted). Same lifecycle as `ContainerUI` (Esc / `OnPlayerDied` close, input
  disposed in `OnDestroy`); `SaveSystem` refuses save / load while `IsOpen`.
- **Experiment:** only `ItemSO.isIngredient` items are accepted (others show "<name> isn't an ingredient"). 3 `CookingIngredientSlotUI` (`_slotIndex` 0–2) **references** (`ItemSO`, like action bar slots), reset
  on every open. Set by dropping an inventory / action bar slot or double-clicking a grid slot (first empty slot);
  cleared by right-click or dragging out. Cook consumes one unit per filled slot → the matching station recipe's output
  (learning it the first time: "New recipe: …") or `_failedDish` (`Item_ReplenishingStew`). The preview never reveals
  an unknown recipe ("Unknown result"). After a cook, slots whose item ran short clear.
- **Recipes:** station recipes ∩ known, as `CookingRecipeRowUI` rows (`Prefabs/UI/Cooking/CookingRecipeRow.prefab`);
  the detail lists ingredients `have/need` (missing tinted) and Cook needs them all.
- Every cook goes through `TryCraft`: consume all, add the result, refund everything if a step fails; then
  `OnActionBarUsed(REFRESH_ONLY_SLOT)` so the HUD bar and this screen refresh.
