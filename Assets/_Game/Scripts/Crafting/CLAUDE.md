# CLAUDE.md — Assets/_Game/Scripts/Crafting

> Loaded when Claude accesses files in this folder. Recipes and cooking stations. Namespace: `Game.Crafting`.
> The cooking screen lives in `Scripts/UI/Crafting/` (`Game.UI`); the station prefab contract in `Prefabs/CLAUDE.md`.

---

## What's here

| File | Role |
|------|------|
| `RecipeSO` (`ScriptableObjects/Crafting/`) | One `input` item → one `output` item. Assets in `Data/Recipes/` (`Recipe_<Output>`). |
| `CookingRules` | **Static, pure.** `FindRecipe(recipes, ingredient)` (first complete match, null/incomplete skipped) and `Evaluate(recipes, ingredient, count, out recipe)` → `CookingState` `Empty` / `NoRecipe` / `Ready`. Tested by `CookingRulesTests`. |
| `CookingStation` | `IInteractable` on the station root: prompt `Cook`, name tag `Cooking station`, `_recipes` (`RecipeSO[]`). `Interact()` raises `OnCookingStationOpenRequested` (`GameEventSO_CookingStationOpenRequest { recipes, stationName }`). Behaviour only — the look is the prefab's `Model` child. |
| `CookingSystem` | Player-side listener on `Player.prefab` (next to `ContainerSystem`) → `CookingUI.Open(recipes, name)`. Future gates (skills, tools) go here, not on the station. |

---

## Cooking screen (`Scripts/UI/Crafting/`)

- `CookingUI` (`IScreenPanel`, nested in `UICanvas`, inactive): player grid + one ingredient slot + output preview +
  Cook button. Same lifecycle as `ContainerUI` (Esc / `OnPlayerDied` close, input disposed in `OnDestroy`);
  `SaveSystem` refuses save / load while `IsOpen`.
- The ingredient slot is a **reference** (`ItemSO`, like an action bar slot), never moved out of the inventory and
  reset on every open. Set by dropping an inventory `ItemSlotUI` on `CookingIngredientSlotUI` or double-clicking a
  grid slot (`CookingInventorySideUI.PrimaryAction`); cleared by right-click or dragging it out.
- Cook = `TryConsume(input)` then `AddItem(output)` (input refunded if the add fails). When the input count hits 0
  the slot clears. Hints: `Place an ingredient` (empty), `No recipe`; Cook is interactable only when `Ready`.
