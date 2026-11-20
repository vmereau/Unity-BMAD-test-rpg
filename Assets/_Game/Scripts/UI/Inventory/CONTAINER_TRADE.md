# Container & Trade Screens — `ContainerUI`, `NPCTradeUI`, bulk transfer

> Read when touching `ContainerUI`, `NPCTradeUI`, `ContainerSideUI` / `TradeSideUI`, `ContainerDetailActions` /
> `TradeDetailActions` or `BulkTransferRules`. Shared detail-panel / prefab rules: `CLAUDE.md` (this folder).

---

## ContainerUI Take-Only (Loot) Mode

- `ContainerUI.TakeItem` validates destination/source and adds successfully before removing one source item, then raises `OnContainerItemTaken` with source inventory and item. The channel is wired on `Prefabs/UI/Container/ContainerUI.prefab`; quest listeners filter the source. Opening and restoring inventories never raise it.
- `ContainerUI.Open(InventorySystem containerInventory, bool takeOnly = false)` — the two-pane take/put screen. `takeOnly = true` is **corpse loot**: the player can take from the container side but cannot deposit. World containers pass `false` (full take/put).
- The flag arrives via `ContainerOpenRequestData.takeOnly` (raised by `ContainerInteractable` = `false`, by `EntityPresence`/`NPCPresence` corpse loot = `true`), forwarded through `ContainerSystem.HandleContainerOpenRequested` → `ContainerUI.Open(inv, takeOnly)`.
- In take-only mode all four player→container Put paths are suppressed: double-click (`OnSlotDoubleClicked`), context menu (`ShowContextMenu` early-returns for the player side), the `PutItems` backstop guard (behind `PutItem` / `PutBatch`), and the detail-action Put button (`ContainerDetailActions.Bind(..., takeOnly)` hides it). Take paths stay fully functional; the player grid stays visible (you see your own inventory while looting).
- Auto-close: `_onPlayerDied` always; `_onLootSourceLost` (`GameEventSO_Inventory`) when the raised inventory is the open
  one — a knocked-out NPC starting to get up.
- `_takeOnly` is reset on **every** `Open`, so a corpse open never leaks take-only state into a later world-container open (and vice-versa).

---

## Bulk Transfer (Take All + Shift ×10)

- `BulkTransferRules` (pure, `BulkTransferRulesTests`): `Quantity(bulk, slotCount, unitPrice, budget)` = 1 without Shift,
  else `min(10, slot units, budget / unitPrice)`, never below 1 while the slot exists (unaffordable → ×1, button
  disabled by the caller); price ≤ 0 = no cap. `ActionLabel("Buy", 7)` → `Buy ×7`.
- `ContainerUI` / `NPCTradeUI` poll `UI.BulkModifier` in `Update` and repaint the selected slot's buttons (and the
  trade price row: `ItemDetailPanelUI.Show(item, ctx, quantity)` → `Price (×7)` + total) when Shift changes.
  Double-click / detail buttons call `TakeBatch` / `PutBatch` / `BuyBatch` / `SellBatch` (batch recomputed at click);
  the context menu captures its quantity when it opens. Each unit still goes through the one-unit rules, so
  `OnContainerItemTaken` fires once per unit.
- **Take All** (`ContainerSide/TakeAllButton`, an `ActionButton` with an added label, + `UI.TakeAll` = R): moves every
  unit, closes the window once the container is empty (a refused unit, e.g. gold, keeps it open). R is polled in
  `Update` on purpose: the Player map's R (`DrawWeapon`) callback runs first and is refused while the cursor is
  unlocked (`IsBusy`) — closing from a callback could re-lock the cursor first and draw the weapon.

---

## Trade Window Close Paths

- `NPCTradeUI` closes on Escape (`UI.Cancel`), on `OnPlayerKnockedOut`, and on `OnDialogueCloseRequested`
  (`GameEventSO_DialogueCloseRequest`, wired on `UICanvas.prefab`) when the requesting `NPCPresence`'s own
  `InventorySystem` is the one being traded with, e.g. an NPC leaving the world through `FactRemovedObject`
  (`Scripts/World/FACT_OBJECTS.md`). All three are listened to only while the window is open (`OnEnable` / `OnDisable`).
