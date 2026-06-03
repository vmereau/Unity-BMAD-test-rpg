# CLAUDE.md — Assets/_Game/Scripts/UI/Inventory

> Inventory and equipment screens, opened via `UIScreenManager`. `InventoryUI` implements `IScreenPanel`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `InventoryUI` | Root inventory panel. Spawns `ItemSlotUI` from prefab, manages context menu, selection state, and wires `EquipmentUI` + `ActionBarUI`. Implements `IScreenPanel`. |
| `ItemSlotUI` | Single inventory slot. Supports drag-and-drop, hover highlight, selection, stack count display. Notifies parent `InventoryUI` on click/drag events. |
| `ItemDetailPanelUI` | Detail side-panel. Shows icon, name, description, and type-specific sections (usable, equipment, skill). Call `Show(ItemSO)` / `Hide()`. |
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

- Section GameObjects (`_usableSection`, `_weaponSection`, etc.) are optional — leave unassigned to hide that section for all items.
- All sections are hidden in `Hide()` and selectively shown in `Show(ItemSO)` based on item type.

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
