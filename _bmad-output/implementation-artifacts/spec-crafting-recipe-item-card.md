---
title: 'Crafting Recipes tab: item card with stats and description'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: 'd51e4fd'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The cooking / smithing Recipes tab shows only the output icon, name, craft count, requirements and the Cook / Forge button. The player can't see what the crafted item does (stats) or what it is (description) before spending materials.

**Approach:** Turn the recipe detail column into an "item card": the shared `ItemDetailPanel` (header with icon, name and category; stat rows; description), with the price hidden. Below it, a "Requires" block nested in the panel's `ActionsContainer` holds the ingredients `have/need`, the craft count and the Cook / Forge button. Stats and text come from `ItemDetailFormatter`, so they match the inventory exactly, and empty sections hide themselves.

## Boundaries & Constraints

**Always:** Reuse `ItemDetailPanelUI` + `ItemDetailFormatter` (no duplicated stat logic). Follow the ItemDetailPanel nesting rules: the host overrides only the nested panel's **root**, `ActionsContainer` is never recreated, and the actions prefab is nested under it like the other hosts. Hide the price (new `ItemPriceContext.None`). Keep existing behaviour: missing ingredients tinted, button label "Cook" / "Forge", craft-count strings, the selection and refresh flow.

**Ask First:** Changing `ItemDetailPanel.prefab` layout beyond wiring a `PriceSection` reference. Showing the card on the Experiment tab.

**Never:** Change the Experiment tab, the recipe rows, the formatter's stat rows / category, or crafting rules. Never add a hover tooltip or a toggle.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Forged weapon | Recipe → `Weapon_ShortIronSword_Forged` | Card: "Short Iron Sword", "Weapon · Forged", Damage row, description; Requires list + "Forge" | N/A |
| Meal with first-meal bonus | Apple Mushroom Sandwich | "Meal", Restores Health row, "First meal: +5 Max HP" (muted "(gained)" once claimed) | No Core → rows without the claim check (existing panel behaviour) |
| No description / no stats | Output with empty description | That section and its divider are hidden | N/A |
| No known recipes | Empty list | Detail hidden, "No known recipes" shown | N/A |
| Price context None | `Show(item, ItemPriceContext.None)` | `PriceSection` inactive; other contexts show it again | Unwired `_priceSection` → price texts hidden only |

</frozen-after-approval>

## Code Map

- `Scripts/UI/Inventory/ItemPriceContext.cs` -- price context enum, gains `None`
- `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- `GetPriceLabel` / `GetPrice`; add `ShowsPrice(context)`
- `Scripts/UI/Inventory/ItemDetailPanelUI.cs` -- `PaintPrice`; needs a `_priceSection` toggle
- `Prefabs/UI/Items/ItemDetailPanel.prefab` -- wire `_priceSection` → `PriceSection`
- `Scripts/UI/Crafting/CookingUI.cs` -- `RefreshRecipes` paints the detail; holds the `_recipe*` fields to replace
- `Prefabs/UI/Cooking/CookingUI.prefab` -- `RecipesPanel/RecipeDetail` (430×~850, VerticalLayoutGroup)
- `Scripts/UI/Inventory/ContainerDetailActions.cs` + `Prefabs/UI/Container/ContainerUI.prefab` -- reference for the actions-prefab nesting
- `Editor/UI/UILayoutCapture.cs` -- `Screens` table (no crafting entry yet)
- `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` -- formatter tests

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/UI/Inventory/ItemPriceContext.cs` -- add `None` (no price, crafting) -- recipe card hides the price
- [x] `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- add `ShowsPrice(ItemPriceContext)` (false for None) -- testable rule
- [x] `Scripts/UI/Inventory/ItemDetailPanelUI.cs` + `Prefabs/UI/Items/ItemDetailPanel.prefab` -- add `[SerializeField] GameObject _priceSection` wired to `PriceSection`; `PaintPrice` sets it active from `ShowsPrice` -- every context other than None is unchanged
- [x] `Scripts/UI/Crafting/CookingRecipeActions.cs` (new, `Game.UI`) + `Prefabs/UI/Cooking/CookingRecipeActions.prefab` -- "Requires" label, ingredients text, craft-count text, craft button + label; `Show(ingredients, craftCount, label, canCraft)` and a `CraftButton` getter -- the requirements block of the card
- [x] `Prefabs/UI/Cooking/CookingUI.prefab` -- replace `RecipeDetail`'s children (OutputSlot, NameText, CraftCountText, IngredientsText, RecipeCookButton) with a nested `ItemDetailPanel` filling the column (root override only; the root background may be disabled if it double-frames), with `CookingRecipeActions` nested under its `ActionsContainer` -- the card layout
- [x] `Scripts/UI/Crafting/CookingUI.cs` -- replace `_recipeOutputIcon`, `_recipeNameText`, `_recipeIngredientsText`, `_recipeCookButton`, `_recipeCookLabel` and `_recipeCraftCountText` with `_recipeItemPanel` (`ItemDetailPanelUI`) and `_recipeActions` (`CookingRecipeActions`); `RefreshRecipes` calls `Show(output, None)` + `_recipeActions.Show(...)`; the button listener moves to `_recipeActions.CraftButton`; wire both in the prefab -- paints the card
- [x] `Editor/UI/UILayoutCapture.cs` -- add a `"Crafting"` screen (`CookingUI`, Experiment panel hidden) -- lets you preview the layout
- [x] `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` -- `ShowsPrice` for every context -- covers the matrix
- [x] `Scripts/Crafting/CLAUDE.md`, `Scripts/UI/Inventory/CLAUDE.md`, `Art/UI/CLAUDE.md` -- document the card, the `None` context and the new capture screen

**Acceptance Criteria:**
- Given a known recipe is selected, when the Recipes tab shows, then the card shows the output's icon, name, category, stat rows and description, with no price line, and the Requires block lists the ingredients `have/need` with missing ones tinted, then the craft count, then the Cook / Forge button.
- Given the player crafts from the card, when the inventory changes, then the requirement counts, the button state and the craft count refresh as before.
- Given the inventory, trade or container screens, when an item is selected, then their price lines are unchanged.

## Design Notes

The card sits in the existing ~430 px column, which is mostly empty today, so adding info means filling unused space rather than squeezing the layout. Keeping the panel's `Spacer` pushes the Requires block and the button to the bottom, so the button stays in the same place from one recipe to the next.

## Verification

**Commands:**
- Unity `run_tests` EditMode -- expected: all pass, incl. `ItemDetailFormatterTests`
- `read_console` (errors) after compilation -- expected: none

**Manual checks:**
- `UILayoutCapture.Capture("Crafting")` → `Art/UI/Previews/UI_Crafting_Preview.png`: the card reads top to bottom with no overlap or clipping in the 430 px column.
- Play mode at the anvil (Short Iron Sword) and at a cooking station (a known meal): the card is correct and Forge / Cook work.

## Suggested Review Order

**Recipe card wiring**

- Entry point: the Recipes detail now paints the shared item panel with no price.
  [`CookingUI.cs:444`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L444)

- Craft button moved into the nested actions prefab; listener follows it.
  [`CookingUI.cs:100`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L100)

- Unwired card / button now warns instead of a silent dead button.
  [`CookingUI.cs:95`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L95)

- Paint-only Requires block: ingredients, craft count, Cook / Forge label + interactable.
  [`CookingRecipeActions.cs:23`](../../Assets/_Game/Scripts/UI/Crafting/CookingRecipeActions.cs#L23)

**Price hiding (shared panel)**

- `None` hides the whole PriceSection; other contexts unchanged.
  [`ItemDetailPanelUI.cs:156`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs#L156)

- Testable rule plus explicit `None` arms so no caller leaks a price.
  [`ItemDetailFormatter.cs:146`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L146)

- New enum member appended last, so `default` stays `Value`.
  [`ItemPriceContext.cs:13`](../../Assets/_Game/Scripts/UI/Inventory/ItemPriceContext.cs#L13)

**Prefabs & layout (see Crafting CLAUDE.md)**

- RecipeDetail: anchor-stretched ItemDetailPanel, actions under ActionsContainer, preferredWidth 273 keeps the 889/942 split.
  [`Crafting/CLAUDE.md:74`](../../Assets/_Game/Scripts/Crafting/CLAUDE.md#L74)

**Peripherals**

- New `Crafting` layout preview, runtime-hidden rows hidden.
  [`UILayoutCapture.cs:48`](../../Assets/_Game/Editor/UI/UILayoutCapture.cs#L48)

- Price-context tests.
  [`ItemDetailFormatterTests.cs:371`](../../Assets/Tests/EditMode/ItemDetailFormatterTests.cs#L371)
