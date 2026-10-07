# CLAUDE.md — Assets/_Game/Scripts/UI/Inventory

> Inventory and equipment screens, opened via `UIScreenManager`. `InventoryUI` implements `IScreenPanel`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `InventoryUI` | Root inventory panel. Spawns `ItemSlotUI` from prefab, manages context menu, selection state, and wires `EquipmentUI` + `ActionBarUI`. Implements `IScreenPanel`. |
| `ItemSlotUI` | Single inventory slot. Supports drag-and-drop, hover highlight, selection, stack count display. Notifies parent `InventoryUI` on click/drag events. |
| `ItemDetailPanelUI` | Shared display-only detail panel (header / stats / description / price). Call `Show(ItemSO, ItemPriceContext = Value)` / `Hide()`. Paints only. |
| `ItemDetailFormatter` | Pure static display logic for the panel: category line, stat rows, skill description, price label/value. Covered by `ItemDetailFormatterTests`. |
| `ItemStatRowUI` | One label/value stat row (`ItemStatRow.prefab`). Hides the value for tag rows (empty value). |
| `ItemStatLine` / `StatPolarity` | Row data (label, value, polarity → green/red/neutral). |
| `ItemPriceContext` | `Value` ("Value", sellValue) / `Buy` ("Price", buyValue) / `Sell` ("Sells for", sellValue). |
| `EquipmentUI` | Equipment panel with 6 named slots (weapon, helmet, armor, ring×2, necklace). Subscribes to `GameEventSO_Void _onEquipmentChanged`. |
| `EquipmentSlotUI` | Single equipment slot. Detects double-click (threshold = 0.3 s) to unequip; single-click on occupied slot shows detail. Notifies parent `EquipmentUI`. |

---

## Data Flow

```
InventoryUI  (IScreenPanel)
  ├── ItemSlotUI × N    (spawned from _itemSlotPrefab into _contentRoot)
  ├── ItemDetailPanelUI (shared between inventory slots and equipment slots)
  ├── ActionBarUI       (cross-panel drop target for action bar assignment)
  └── EquipmentUI
        └── EquipmentSlotUI × 6
```

---

## IScreenPanel Contract

- `OnScreenOpen()` → `CursorManager.Unlock()` + refresh slots.
- `OnScreenClose()` → `CursorManager.Lock()` + close context menu.

---

## Drag & Drop

- Both `ItemSlotUI` (inventory) and `ActionBarSlotUI` (HUD) can be drag sources and drop targets.
- Cross-panel drops (inventory → action bar) are handled by `ActionBarUI.HandleDrop`.
- Ghost image rules: parented to root Canvas, `raycastTarget = false`, destroyed in `OnEndDrag` and `OnDrop`.

---

## Context Menu

- `InventoryUI` instantiates a context-menu prefab on right-click, with a blocker overlay behind it.
- Context menu and blocker are both destroyed when the panel closes or the player clicks elsewhere.
- `_contextMenuSlotIndex` tracks which slot opened the menu; reset to -1 on close.

---

## ItemDetailPanelUI Notes

- Layout (root `VerticalLayoutGroup`): `Header` (icon + name + category) → `StatsSection` → `DescriptionSection` → `Spacer` (flexible) → `PriceSection` → `ActionsContainer`. Empty sections hide with their divider.
- `Show(item, ItemPriceContext)`: `NPCTradeUI` passes `Buy` (NPC side) / `Sell` (player side); inventory, equipment and containers use the default `Value`. The trade buttons read plain `Buy` / `Sell` (price lives in the panel).
- All display decisions (category, rows, order, polarity, price) live in `ItemDetailFormatter` — change and test them there, not in the MonoBehaviour.
- Stat rows are pooled under `StatsSection/StatRows`: instantiated only when more are needed, extras `SetActive(false)`. Never destroy rows on `Show`.
- **`ActionsContainer` must never be deleted/recreated** — each host statically nests its actions prefab under it (by fileID). Move/reorder only. It has no `ContentSizeFitter` (the root layout group sizes it).
- Host prefabs (`InventoryUI`, `NPCTradeUI`, `ContainerUI`) must only override the nested panel's **root** RectTransform/GameObject. Child overrides orphan on the next panel rebuild.
- `EquipmentUI._itemDetailPanel` points at the same shared panel inside `InventoryUI.prefab`.
- Item descriptions are flavour text; numeric effects belong in stat rows (`ItemDetailFormatter.BuildStatLines`).

---

## ContainerUI Take-Only (Loot) Mode

- `ContainerUI.Open(InventorySystem containerInventory, bool takeOnly = false)` — the two-pane take/put screen. `takeOnly = true` is **corpse loot**: the player can take from the container side but cannot deposit. World containers pass `false` (full take/put).
- The flag arrives via `ContainerOpenRequestData.takeOnly` (raised by `ContainerInteractable` = `false`, by `EntityPresence`/`NPCPresence` corpse loot = `true`), forwarded through `ContainerSystem.HandleContainerOpenRequested` → `ContainerUI.Open(inv, takeOnly)`.
- In take-only mode all four player→container Put paths are suppressed: double-click (`OnSlotDoubleClicked`), context menu (`ShowContextMenu` early-returns for the player side), the `PutItem` backstop guard, and the detail-action Put button (`ContainerDetailActions.Bind(..., takeOnly)` hides it). Take paths stay fully functional; the player grid stays visible (you see your own inventory while looting).
- `_takeOnly` is reset on **every** `Open`, so a corpse open never leaks take-only state into a later world-container open (and vice-versa).

---

## Gotchas

- `EquipmentSlotUI` does **not** subscribe to events — it is refreshed by `EquipmentUI` calling `Refresh(item)` on each slot after `_onEquipmentChanged` fires.
- `InventoryUI` adds an `AnyButtonClickListener` component to `_panelRoot` at runtime in `Awake` — do not add it manually in the prefab.
