---
title: 'Inventory filter tabs'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: '4f31a48'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Inventory/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The inventory grid shows every item in one list. As the item count grows (weapons, food, ingredients, recipes, ingots…), finding a given kind of item takes scanning the whole grid.

**Approach:** Add a row of small icon-only filter tabs at the top of the inventory's item grid panel. Hovering a tab shows a tooltip with its name and count. "All" is selected each time the inventory opens. The grid then shows only the slots whose item belongs to the active category, and every slot action keeps targeting the real inventory slot.

## Boundaries & Constraints

**Always:**
- Tabs, in order: **All · Weapons · Armor · Potions · Meals · Ingredients · Crafting Materials · Books & Recipes · Misc**.
- Category precedence matches `ItemDetailFormatter.GetCategory`:
  - `isIngredient` → Ingredients, then `isCraftingMaterial` → Crafting Materials (both flags win over the class).
  - `WeaponSO` (forged included) → Weapons; `ArmorSO` → Armor.
  - `PotionItemSO` Meal → Meals; other `PotionItemSO` → Potions.
  - `SkillItemSO` / `RecipeItemSO` → Books & Recipes.
  - Everything else → Misc.
- Classification is pure static logic, unit-tested. MonoBehaviours only paint.
- Layout: the strip goes **inside** the top of the grid panel, and the "Gold" line moves to a footer at the bottom of that panel. The panel keeps its 768×800 rect and its 8 columns.
- Tabs are 52×52 icon buttons. Active tab = brass tint (1, .82, .5), hover = warm tint (1, .95, .85), the same tints as `ItemSlotUI`.
- A category with 0 slots stays visible with its icon dimmed. It is still clickable and shows an empty grid.
- Tooltip text is `"<Label> (<slot count>)"`, e.g. "Weapons (3)", shown next to the hovered tab. It is not a raycast target.
- Opening the inventory always resets the filter to All.
- Drag-swap, context menu, double-click, Use / Drop / Equip and the detail actions act on the slot's **real** inventory index.
- Selection still follows its item. If a filter change or refresh hides the selected item, the selection is cleared and the detail panel hidden.

**Ask First:**
- Changing the size or position of the grid panel or of any other inventory column.
- Touching `ContainerUI`, `NPCTradeUI` or `CookingUI` grids.

**Never:**
- Filters on the container, trade or crafting screens (out of scope).
- Generating images (the user makes the final icons). Placeholders reuse existing item icons.
- Reordering or mutating `InventorySystem` to filter.
- Adding category logic to `ItemDetailPanelUI`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Filter | Inventory [Sword, Bread(ingredient), Stew(meal), Ingot]; click Meals | Grid shows only Stew, bound to inventory index 2 | N/A |
| Swap in filtered view | Weapons tab, drag weapon at index 4 onto weapon at index 0 | `MoveItem(4, 0)`; filtered grid repaints | N/A |
| Selection hidden | Stew selected; click Weapons | Selection cleared, detail panel hidden | N/A |
| Item consumed | Meals tab, eat the last Stew | Grid empty; Meals tab dims; tooltip count 0 | N/A |
| Reopen | Close on Potions tab, reopen | All tab active, full grid | N/A |
| Null item / missing icon | Classify(null); a tab entry with no sprite | null → Misc; tab shows its label text instead of an icon | Warn once per missing sprite |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- `GetCategory` is the precedence to mirror
- `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` -- `RefreshSlots` / `SelectSlot` assume child index == slot index (`GetChild(slotIndex)`)
- `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs` -- `Bind(item, index, count)` already carries the real index (drag, click, context menu use `SlotIndex`)
- `Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab` / `InventoryPanel.prefab` -- grid panel, `_contentRoot`, Gold text
- `Assets/_Game/Art/UI/Items/` -- existing icons used as placeholders
- `Assets/Tests/EditMode/` -- EditMode tests (`Tests.EditMode` asmdef)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemFilter.cs` -- `enum ItemFilter` + static `ItemFilterRules` with `Classify(ItemSO)` (never returns All), `Matches(item, filter)` and `Label(filter)` -- one source for grid and tooltip
- [x] `Assets/Tests/EditMode/ItemFilterRulesTests.cs` -- one case per type and flag (forged weapon, raw-food potion with `isIngredient`, meal, tome, recipe, plain item, null), All matches everything -- locks the precedence
- [x] `Assets/_Game/Scripts/UI/Inventory/InventoryFilterTabUI.cs` + `InventoryFilterBarUI.cs` -- the bar spawns one tab per serialized entry (filter, icon, label) and owns a single tooltip. It exposes `Active`, `event Action<ItemFilter> FilterChanged`, `ResetToAll()` and `SetCounts(counts)`. Each tab handles click, hover tint, active tint and dimming at count 0 -- view only
- [x] `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` -- `[SerializeField] InventoryFilterBarUI _filterBar` (null-guarded). `RefreshSlots` spawns only matching slots, keeps an inventory-index → `ItemSlotUI` lookup that replaces `GetChild(index)`, and pushes per-filter counts. `OnScreenOpen` calls `ResetToAll()`. `FilterChanged` → `RefreshSlots(false)` -- filtering without index drift
- [x] `Assets/_Game/Prefabs/UI/Inventory/InventoryFilterTab.prefab` + grid panel in the inventory prefab -- tab prefab (SlotWell background Image + `InventoryFilterTabUI` clicks via `IPointerClickHandler`, icon Image, fallback TMP label). FilterBar strip at the panel top; Gold moved to a footer; 9 entries wired with the user's icons `Art/UI/Inventory/UI_Filter_<Name>.png` (All, Weapons, Armor, Potions, Meals, Ingredients, Crafting, BooksRecipes, Misc; Sprite, Max Size 128) -- implements the layout
- [x] `Assets/_Game/Scripts/UI/Inventory/CLAUDE.md`, `Assets/_Game/Art/UI/CLAUDE.md` -- document the filter bar and the index lookup rule; add the `FilterIcon` entry (files `Art/UI/Inventory/UI_Filter_<Name>.png`, glyph prompt template) and the new metrics row -- keep the docs current

**Acceptance Criteria:**
- Given the inventory is open, when the player hovers any tab, then a tooltip shows its label and slot count, and it hides on exit.
- Given a non-All tab is active, when the inventory changes (loot, use, equip, action-bar use), then the grid repaints still filtered and the counts update.
- Given the preview capture runs, when `UILayoutCapture.Capture("Inventory")` renders, then the strip sits inside the grid panel above the grid, with Gold in the footer and no overlap.

## Spec Change Log

- 2026-10-29 (approval): the user generated the final icons in `Art/UI/Inventory/` before implementation, so they are wired directly instead of the placeholder item icons. The Never rule against generating images still holds.

## Design Notes

Icons are plain bar-entry sprites: replacing one is a reassignment, not a code change. Counts are slot counts, not unit totals: a stack counts as one, which matches what the grid shows.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass, including `ItemFilterRulesTests`
- MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- `UILayoutCapture.Capture("Inventory")` preview: 9 tabs in one row, Gold footer, grid unchanged

## Suggested Review Order

**Filtered grid with real inventory indices**

- Entry point: spawns only matching slots, records index → view, pushes counts.
  [`InventoryUI.cs:345`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L345)

- Lookup replaces `GetChild(index)`; null = slot hidden by the filter.
  [`InventoryUI.cs:415`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L415)

- Filter change closes the context menu (its slot may vanish) then repaints.
  [`InventoryUI.cs:120`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L120)

- Every open starts on All, silently, before the first repaint.
  [`InventoryUI.cs:85`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L85)

**Category rules**

- Mirrors `GetCategory` precedence: flags first, then class.
  [`ItemFilter.cs:27`](../../Assets/_Game/Scripts/UI/Inventory/ItemFilter.cs#L27)

**Tab bar view**

- Idempotent build from serialized entries; invalid enum values skipped.
  [`InventoryFilterBarUI.cs:48`](../../Assets/_Game/Scripts/UI/Inventory/InventoryFilterBarUI.cs#L48)

- Counts dim empty tabs and keep an open tooltip current.
  [`InventoryFilterBarUI.cs:89`](../../Assets/_Game/Scripts/UI/Inventory/InventoryFilterBarUI.cs#L89)

- Tooltip anchored under the hovered tab, drawn over the slots.
  [`InventoryFilterBarUI.cs:100`](../../Assets/_Game/Scripts/UI/Inventory/InventoryFilterBarUI.cs#L100)

- Tab tints: active brass > hover warm > normal; left click only.
  [`InventoryFilterTabUI.cs:52`](../../Assets/_Game/Scripts/UI/Inventory/InventoryFilterTabUI.cs#L52)

**Peripherals**

- Layout capture builds the runtime tabs and hides the shared header.
  [`UILayoutCapture.cs:239`](../../Assets/_Game/Editor/UI/UILayoutCapture.cs#L239)

- Precedence locked by tests (ingredient flag beats potion class).
  [`ItemFilterRulesTests.cs:65`](../../Assets/Tests/EditMode/ItemFilterRulesTests.cs#L65)
