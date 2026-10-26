---
title: 'Cooking ingredient categories and Experiment UI polish'
type: 'feature'
created: '2026-10-26'
status: 'done'
baseline_commit: '408f8c3'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cooked meals (sandwiches, Replenishing Stew) carry `isIngredient`, so they can be thrown into the cooking station and lost for a stew; every healing item shows the category "Potion"; the cooking grid lists the whole inventory; an Experiment slot can't be emptied by double-click; and Experiment slots show the full stack count although a cook uses one unit.

**Approach:** Keep `ItemSO.isIngredient` as the single cooking gate and fix the data so only raw ingredients carry it. Add an explicit `Potion` / `Meal` kind on `PotionItemSO` so the detail panel shows three categories: Ingredient, Potion, Meal. In the cooking screen, list only ingredients, clear a slot on double-click, and never show a stack count in the Experiment slots.

## Boundaries & Constraints

**Always:** Ingredient = `isIngredient` (any item type), category checked before the class switch. The grid's `ItemSlotUI.SlotIndex` stays the real inventory index (double-click resolves the clicked slot through it). Right-click and drag-out still clear a slot. Existing `CookingRules` behavior unchanged.

**Ask First:** Renaming or removing the serialized `isIngredient` field; adding new item subclasses.

**Never:** No new item category for non-healing items; no change to the main inventory / trade / container grids; no change to recipe data.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Raw food detail | Apple (`isIngredient`, heals over time) | Category "Ingredient" | N/A |
| Meal detail | Apple Mushroom Sandwich (`Meal`, not ingredient) | Category "Meal" | N/A |
| Potion detail | Health Potion (`Potion`, not ingredient) | Category "Potion" | N/A |
| Meal on action bar dropped into slot | Sandwich via action bar drag | Slot stays empty, hint "… isn't an ingredient" | N/A |
| Double-click filled slot | Slot holds Apple | Slot cleared, preview refreshes | Empty slot: no-op |
| Stack in slot | 12 Apples in slot | Icon only, no count | N/A |
| Meal flagged ingredient | `PotionItemSO` with `Meal` and `isIngredient` | Editor warning in `OnValidate` | Warn only |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- healing items; gets the kind field
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- `GetCategory` (only caller: `ItemDetailPanelUI`)
- `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs` -- `RefreshGrid`, `SetIngredientFromInventory` (resolves a child by index)
- `Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs` -- Experiment slot: `Bind`, `OnPointerClick`
- `Assets/_Game/Data/Items/Item_*.asset` -- food data (`isIngredient` set on both sandwiches and the stew today)
- `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` -- category assertions

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- add `public enum ConsumableKind { Potion, Meal }` (same file is fine, it's not an SO), serialized `_kind` (default `Potion`) + `Kind` property; `OnValidate` warns when `Meal && isIngredient` -- explicit category instead of guessing from heal-over-time (raw food heals over time too)
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- `GetCategory`: `isIngredient` → "Ingredient" first; `PotionItemSO` → "Meal" or "Potion" by `Kind` -- three categories
- [x] `Assets/_Game/Data/Items/Item_AppleMushroomSandwich.asset`, `Item_AppleTroutSandwich.asset`, `Item_ReplenishingStew.asset` -- `isIngredient: 0`, `_kind: Meal` -- meals can't be cooked
- [x] `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs` -- `RefreshGrid` binds only `isIngredient` items, keeping the inventory index; `SetIngredientFromInventory` finds the child whose `SlotIndex == index` -- filtered grid, index-safe double-click
- [x] `Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs` -- left double-click on a filled slot → `ClearIngredient`; `Bind(item)` drops the count and keeps `_countText` hidden -- clear by double-click, no stack count
- [x] `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` -- tests for Ingredient (incl. a `PotionItemSO` flagged ingredient), Meal, Potion categories
- [x] `Assets/_Game/Scripts/Crafting/CLAUDE.md`, `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` -- document the kind, categories, filtered grid, double-click clear

**Acceptance Criteria:**
- Given the cooking screen is open (either tab), when the inventory holds meals, potions and ingredients, then the grid lists only ingredients.
- Given a filtered grid, when an ingredient is double-clicked, then that exact item fills the first empty Experiment slot.
- Given the Experiment tab, when cooking with the filled slots, then the result is the same as before this change.

## Design Notes

The grid shows only ingredients on both tabs: the screen only works with ingredients, and changing the grid when you switch tabs would be jarring. `Healing Plant` stays an ingredient (instant heal, `Potion` kind, shown as "Ingredient").

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass, including the new category tests
- Unity MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- Play: open a cooking station with a sandwich in the inventory → not listed; double-click a slot → it clears; no count on the slots.

## Suggested Review Order

**Item categories**

- Entry point: the ingredient flag wins over the class, then Meal / Potion by kind.
  [`ItemDetailFormatter.cs:57`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L57)

- Explicit kind instead of guessing from heal-over-time (raw food heals over time too).
  [`PotionItemSO.cs:11`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L11)

- Editor warning guards against a meal flagged as an ingredient.
  [`PotionItemSO.cs:178`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L178)

- Data fix: meals lose `isIngredient` and become `Meal` (same in the trout sandwich and the stew).
  [`Item_AppleMushroomSandwich.asset:25`](../../Assets/_Game/Data/Items/Item_AppleMushroomSandwich.asset#L25)

**Cooking screen**

- Grid lists ingredients only, keeping each slot's real inventory index.
  [`CookingUI.cs:357`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L357)

- Double-click resolves the clicked slot by SlotIndex, not child position.
  [`CookingUI.cs:221`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L221)

- Double-click clears an Experiment slot, like right-click.
  [`CookingIngredientSlotUI.cs:81`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L81)

- Icon only: the stack count is always hidden.
  [`CookingIngredientSlotUI.cs:36`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L36)

**Tests**

- Meal, Ingredient (food and plain item) and precedence cases.
  [`ItemDetailFormatterTests.cs:169`](../../Assets/Tests/EditMode/ItemDetailFormatterTests.cs#L169)
