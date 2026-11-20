# CLAUDE.md — Assets/_Game/Scripts/UI/Inventory

> Loaded when Claude accesses files in this folder. Inventory and equipment screens, opened via `UIScreenManager`
> (`InventoryUI` implements `IScreenPanel`), plus the shared item detail panel, container and trade screens.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `InventoryUI` | Root inventory panel. Reads `InventorySystem.Items`; calls `UseItem` / `DropItem`. Spawns `ItemSlotUI` from prefab, manages context menu, selection state, and wires `EquipmentUI` + `ActionBarUI`. Implements `IScreenPanel`. Repaints on `_onInventoryChanged` / action-bar / equipment events **only while open**. Selection follows its **item** (`_selectedItem`): same index if it still holds it, else the first slot holding it, else cleared. Spawns only the slots matching `_filterBar.Active`; `_slotViews[inventoryIndex]` → its `ItemSlotUI` or null when filtered out — **never `_contentRoot.GetChild(index)`** (grid order ≠ inventory index). Selection hidden by the filter is cleared. |
| `InventoryFilterBarUI` / `InventoryFilterTabUI` | Icon filter tabs at the top of the inventory grid panel (`InventoryFilterTab.prefab`, built from the bar's `_entries` in `Awake`). Shared hover tooltip "Weapons (3)" (slot counts pushed by `InventoryUI.RefreshSlots`), active = brass tint, empty category = dimmed icon. `ResetToAll()` on every `OnScreenOpen` (no event); `FilterChanged` → `InventoryUI` closes the context menu and repaints. |
| `ItemFilter` / `ItemFilterRules` | Pure tab logic: `Classify` (same precedence as `ItemDetailFormatter.GetCategory` — keep the two in sync), `Matches`, `Label`, `TooltipText`. Covered by `ItemFilterRulesTests`. |
| `ItemSlotUI` | Single inventory slot. Supports drag-and-drop, hover highlight, selection, stack count display. Notifies parent `InventoryUI` on click/drag events. |
| `ItemDetailPanelUI` | Shared display-only detail panel: paints an `ItemSO` (category, stat rows, description, price) from `ItemDetailFormatter`. Call `Show(ItemSO, ItemPriceContext = Value, int quantity = 1)` / `Hide()` (quantity > 1 totals the price row). Paints only. |
| `ItemDetailFormatter` | Pure static display logic for the panel: category line, stat rows, skill description (tome: taught skill's description + effect text), price label/value. Player-dependent rows get their state injected (`BuildStatLines(item, into, isRecipeKnown, isMealBonusClaimed)` → recipe "Status: Already known / Not learned", food "First meal: +5 Max HP" / Muted "… (gained)"; the panel passes `WorldStateManager` checks) so it stays pure. Covered by `ItemDetailFormatterTests`. |
| `ItemStatRowUI` | One label/value stat row (`ItemStatRow.prefab`). Hides the value for tag rows (empty value). |
| `ItemStatLine` / `StatPolarity` | Row data (label, value, polarity → green/red/neutral/muted gray). |
| `ItemPriceContext` | `Value` ("Value", sellValue) / `Buy` ("Price", buyValue) / `Sell` ("Sells for", sellValue) / `None` (price section hidden — crafting recipe card; `ItemDetailFormatter.ShowsPrice`). |
| `GiveItemUI` | One-item **Give window** (`Prefabs/UI/Give/GiveItemUI.prefab`, built from the trade prefab; `UICanvas` sibling right after `DialoguePanel`, `HideHudWhileOpen`, dimming `Image` on the root blocks clicks on the dialogue). Opened by `DialogueSystem` for a `ChoiceOption.requiredCategory` choice: left = player items matching the category (`GiveItemRules.Matches`; slot index = inventory index, implements `IItemSlotContainer` itself), right = title "Give to <NPC>", detail panel as the give slot (`ItemPriceContext.None`, `EmptyHint` while nothing is selected) and Give (disabled until a selection) / Cancel. Click / double-click / right-click only select. Give → callback → `DialogueSystem.CompleteGiveChoice`; Cancel / Escape → closes with nothing given. Not an `IScreenPanel`: the dialogue stays open behind it and owns the cursor. While open, `DialogueUI` ignores keys / clicks (`DialogueSystem.IsGiveWindowOpen`); `DialogueSystem.Close` (knockout, close request) closes it. |
| `ContainerUI` | Two-pane take / put container screen (world containers + corpse loot), `IScreenPanel` — `CONTAINER_TRADE.md` |
| `NPCTradeUI` | NPC shop buy / sell screen, `IScreenPanel` — `CONTAINER_TRADE.md` |
| `ContainerSideUI` / `TradeSideUI` | `IItemSlotContainer` adapters on each grid side (`ContainerSide` / `TradeSide`): forward slot events to the owning `ContainerUI` / `NPCTradeUI` |
| `InventoryDetailActions` / `TradeDetailActions` / `ContainerDetailActions` | Per-host action buttons nested under the detail panel's `ActionsContainer` (Use / Equip / Drop, Buy / Sell, Take / Put) |
| `BulkTransferRules` | Pure Shift ×10 batch-size + label rules (`BulkTransferRulesTests`) — `CONTAINER_TRADE.md` |
| `IItemSlotContainer` | Interface `ItemSlotUI` reports to (`SelectSlot`, `ShowContextMenu`, `PrimaryAction`, `SwapSlots`) — decouples slots from their host |
| `EquipmentUI` | Equipment panel with 7 named slots (weapon, offhand, helmet, armor, ring×2, necklace). Subscribes to `GameEventSO_Void _onEquipmentChanged`. |
| `EquipmentSlotUI` | Single equipment slot. Detects double-click (threshold = 0.3 s) to unequip; single-click on occupied slot shows detail. Notifies parent `EquipmentUI`. |

---

## Data Flow

```
InventoryUI  (IScreenPanel)
  ├── ItemSlotUI × N    (spawned from _itemSlotPrefab into _contentRoot)
  ├── ItemDetailPanelUI (shared between inventory slots and equipment slots)
  ├── ActionBarUI       (cross-panel drop target for action bar assignment)
  └── EquipmentUI
        └── EquipmentSlotUI × 7
```

---

## IScreenPanel & Drag & Drop

- `IScreenPanel`, cursor and drag-ghost rules: `Scripts/UI/CLAUDE.md`. `InventoryUI.OnScreenOpen()` also refreshes the
  slots; `OnScreenClose()` also closes the context menu.
- Both `ItemSlotUI` (inventory) and `ActionBarSlotUI` (HUD) can be drag sources and drop targets.
- Cross-panel drops (inventory → action bar) are handled by `ActionBarUI.HandleDrop`.

---

## Context Menu

- `InventoryUI` instantiates a context-menu prefab on right-click, with a blocker overlay behind it.
- Context menu and blocker are both destroyed when the panel closes or the player clicks elsewhere.
- `_contextMenuSlotIndex` tracks which slot opened the menu; reset to -1 on close.
- The **Equip** button in `InventoryUI.ShowContextMenu()` appears automatically — it checks
  `_equipmentSystem.IsEquippable(item) && !_equipmentSystem.IsEquipped(item)`, which resolves to true for any
  `EquipableItemSO` not yet equipped. There is **no Unequip path in the context menu** — unequip is only via double-click
  on the equipment slot or the Unequip detail button.
- The **Equip/Unequip** detail button is handled by `InventoryDetailActions` for any `EquipableItemSO`.

---

## ItemDetailPanelUI Notes

- No per-type sections (new rows / categories go in `ItemDetailFormatter`). Action buttons live in the per-host actions prefabs (`InventoryDetailActions`,
  `TradeDetailActions`, `ContainerDetailActions`).
- Layout (root `VerticalLayoutGroup`): `Header` (icon + name + category) → `StatsSection` → `DescriptionSection` → `Spacer` (flexible) → `PriceSection` → `ActionsContainer`. Empty sections hide with their divider.
- `Show(item, ItemPriceContext)`: `NPCTradeUI` passes `Buy` (NPC side) / `Sell` (player side); inventory, equipment and containers use the default `Value`; `CookingUI`'s recipe card passes `None` (`_priceSection` → `PriceSection` hidden). The trade buttons read `Buy` / `Sell` (`Buy ×N` with Shift — see `CONTAINER_TRADE.md` → Bulk Transfer; price lives in the panel).
- All display decisions (category, rows, order, polarity, price) live in `ItemDetailFormatter` — change and test them there, not in the MonoBehaviour.
- Stat rows are pooled under `StatsSection/StatRows`: instantiated only when more are needed, extras `SetActive(false)`. Never destroy rows on `Show`.
- **`ActionsContainer` must never be deleted/recreated** — each host statically nests its actions prefab under it (by fileID). Move/reorder only. It has no `ContentSizeFitter` (the root layout group sizes it).
- Host prefabs (`InventoryUI`, `NPCTradeUI`, `ContainerUI`, `CookingUI`) must only override the nested panel's **root** RectTransform/GameObject. Child overrides orphan on the next panel rebuild.
- `EquipmentUI._itemDetailPanel` points at the same shared panel inside `InventoryUI.prefab`.
- Item descriptions are flavour text; numeric effects belong in stat rows (`ItemDetailFormatter.BuildStatLines`).

---

## Topic files

| File | Read when |
|------|-----------|
| `CONTAINER_TRADE.md` | Touching `ContainerUI` / `NPCTradeUI` / their side + detail-action classes / `BulkTransferRules` (loot mode, Shift ×10, Take All, trade close paths) |

---

## Gotchas

- `InventoryPanel.prefab` is shared with `ContainerUI` and `NPCTradeUI`. The inventory-only parts (`FilterBar`, `FilterTooltip`, `GoldFooter`, top padding 76, `header` hidden) are **overrides on the instance in `InventoryUI.prefab`** — never add them to `InventoryPanel.prefab`. `InventoryUI._goldText` → `GoldFooter`.
- `EquipmentSlotUI` does **not** subscribe to events — it is refreshed by `EquipmentUI` calling `Refresh(item)` on each slot after `_onEquipmentChanged` fires.
- `InventoryUI` adds an `AnyButtonClickListener` component to `_panelRoot` at runtime in `Awake` — do not add it manually in the prefab.
