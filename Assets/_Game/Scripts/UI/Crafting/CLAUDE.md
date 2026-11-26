# CLAUDE.md — Assets/_Game/Scripts/UI/Crafting

> Loaded when Claude accesses files in this folder. The cooking / smithing screen: `CookingUI`,
> `CookingIngredientSlotUI`, `CookingInventorySideUI`, `CookingRecipeRowUI`, `CookingRecipeActions` (`Game.UI`).
> Recipes, stations, `CookingRules`, known-recipe facts and the craft count store: `Scripts/Crafting/CLAUDE.md`.

---

## Cooking screen

- `CookingUI` (`IScreenPanel`, nested in `UICanvas`, inactive): shared player grid + **Experiment** / **Recipes** tabs
  (cooking opens on Experiment, inactive tab tinted; smithing — see Smithing mode below — has Recipes only). Same lifecycle as `ContainerUI` (Esc / `OnPlayerDied` close, input
  disposed in `OnDestroy`); `SaveSystem` refuses save / load while `IsOpen`.
- **Player grid lists the station's materials only** (both tabs; `CookingRules.IsMaterialFor` — ingredients for cooking, `isCraftingMaterial` for smithing). Each `ItemSlotUI` keeps its real inventory index as `SlotIndex`, so
  grid child position ≠ inventory index — resolve a clicked slot with `FindGridSlot(SlotIndex)`, never `GetChild(index)`.
- **Experiment:** only `ItemSO.isIngredient` items are accepted (others, e.g. a meal dragged from the action bar,
  show "<name> isn't an ingredient"). Cooked meals (`PotionItemSO` `Meal`) are never ingredients. 3
  `CookingIngredientSlotUI` (`_slotIndex` 0–2) **references** (`ItemSO`, like action bar slots), reset on every open. Set by dropping an inventory / action bar slot or double-clicking a grid slot (first empty slot);
  cleared by right-click, double-click or dragging out. Slots show the icon only — no stack count (a cook uses one unit). Cook consumes one unit per filled slot → the matching station recipe's output
  (learning it the first time: "New recipe: …") or `_failedDish` (`Item_ReplenishingStew`). A cook that learns a recipe
  whose output is a `Meal` (`CookingRules.IsMealDiscovery`; not a known recipe, a recipe item, the failed dish or a potion)
  sets `_mealDiscoveredFact` once (`WorldFact_Cooking_MealDiscovered`, wired on `CookingUI.prefab`; null = warned in `Awake`). The preview never reveals
  an unknown recipe ("Unknown result"). After a cook, slots whose item ran short clear.
- **Recipes:** station recipes ∩ known, as `CookingRecipeRowUI` rows (`Prefabs/UI/Cooking/CookingRecipeRow.prefab`).
  The detail is an **item card**: a nested `ItemDetailPanel` (`_recipeItemPanel`, `Show(output, ItemPriceContext.None)` →
  header + category, stat rows, description, no price) with `CookingRecipeActions.prefab` (`_recipeActions`) nested
  under its `ActionsContainer`: "Requires" + ingredients `have/need` (missing tinted), craft count, Cook / Forge
  button (`CraftButton`; needs every ingredient). `RecipeDetail` has no layout group — the panel is anchor-stretched
  (like `ContainerUI`), and its `LayoutElement.preferredWidth` 273 keeps the inventory / station split (889 / 942).
- Every cook goes through `TryCraft`: consume all, add the result, refund everything if a step fails; then
  `OnActionBarUsed(REFRESH_ONLY_SLOT)` so the HUD bar and this screen refresh.

---

## Smithing mode

- Smithing = **Recipes tab only** (Experiment tab button hidden, experiment input rejected), grid lists
  `isCraftingMaterial` items, button "Forge", count "Never forged / Forged once / Forged N times".

---

## Craft count

- `CookingUI` counts every successful cook of a recipe's output (Recipes tab **or** a matching Experiment cook — not
  the failed dish, not a refunded craft) and shows `FormatCraftCount` ("Never cooked" / "Cooked once" / "Cooked N
  times") via `CookingUI._recipeActions` → `CookingRecipeActions._craftCountText` (the recipe card's Requires block).
