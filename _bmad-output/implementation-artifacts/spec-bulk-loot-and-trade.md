---
title: 'Take All loot + Shift bulk (×10) take / put / buy / sell'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: '95143b4a90384e1ff4ad7cbe3800a0da8961c074'
context: ['{project-root}/Assets/_Game/Scripts/UI/Inventory/CLAUDE.md', '{project-root}/Assets/_Game/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Containers, corpses and shops move one unit per click, so emptying a chest or buying a stack of arrows is tedious.

**Approach:** Add a **Take All** button (+ `R`) to `ContainerUI` that moves everything to the player and closes the window. Holding **Shift** turns every single-unit action (double-click, detail button, context menu) on the container and trade screens into a batch of up to 10, with button labels and the trade price updating live.

## Boundaries & Constraints

**Always:**
- Batch size = `min(10, units in the selected slot, affordable units)`, never below 1 while the slot exists. Affordable = buyer's gold / unit price (buy: player gold ÷ `buyValue`; sell: NPC gold ÷ `sellValue`; take/put: no cap; price 0 = no cap). Without Shift, batch = 1.
- If even 1 unit is unaffordable, show ×1 with the button disabled (as today).
- Batches reuse the existing one-unit transfer rules (gold via `TrySpend`/`Add`, `AddItem`/`DecrementStack`, quest items never put/sold, take-only corpses never accept puts). Each unit taken still raises `OnContainerItemTaken` once.
- Shift and `R` are read through new `InputSystem_Actions` **UI** actions (`BulkModifier` = left/right Shift, `TakeAll` = R), edited in **both** `.inputactions` and the embedded JSON in `.cs`. Poll them in `Update` (`IsPressed` / `WasPressedThisFrame`) so the Take All close happens after the Player map's `DrawWeapon` callback has already been refused (cursor still unlocked → `IsBusy`).
- Labels: `Buy` / `Sell` / `Take` / `Put` at ×1; `Buy ×7` etc. above 1. The price row shows the total and the label gains the count (`Price (×7)` → `105g`).

**Ask First:** any change to `InventorySystem` capacity rules or the `GoldSystem` API.

**Never:** a quantity slider/popup; `Keyboard.current`; Take All on the trade screen; a Put All button.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bulk buy | NPC slot 25 arrows @3g, player 100g, Shift held | Button `Buy ×10`, price `Price (×10)` `30g`; double-click buys 10 | N/A |
| Partly affordable | 10 units @15g, player 60g, Shift | `Buy ×4`, `60g`; buys 4 | N/A |
| Small stack | Slot holds 1, Shift | Shows ×1 (plain `Buy`) | N/A |
| Unaffordable | Price 50g, player 20g, Shift | `Buy`, disabled | Nothing bought |
| Bulk sell | Player 12 pelts @5g, NPC 30g, Shift | `Sell ×6`, `30g` | Quest item: Sell hidden |
| Take All | Chest: 3 items + gold | All moved, gold → `GoldSystem`, window closes | A refused unit (gold cap) stays; window stays open if anything remains |
| Take All empty | Empty container | Button disabled, `R` does nothing | N/A |
| Shift released | Labels at ×7 | Labels/price repaint to ×1 the same frame | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/InputSystem_Actions.inputactions` / `InputSystem_Actions.cs` -- UI map; add `BulkModifier`, `TakeAll` (hand-added, no `IUIActions` callback).
- `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs` -- take/put, context menu, detail panel; gains Take All, batch, Shift polling.
- `Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs` -- Take/Put buttons; needs a quantity for labels.
- `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` -- buy/sell, `CommitTrade`, context menu; does not store selected index/side yet.
- `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs` -- Buy/Sell buttons + affordability.
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs` / `ItemDetailFormatter.cs` -- `PaintPrice` / `GetPrice` / `GetPriceLabel`.
- `Assets/_Game/Prefabs/UI/Container/ContainerUI.prefab` -- needs the Take All button on the container pane.
- `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` -- existing price assertions to keep green.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/InputSystem_Actions.inputactions` + `InputSystem_Actions.cs` -- add UI actions `BulkModifier` (Button, `<Keyboard>/leftShift`, `<Keyboard>/rightShift`) and `TakeAll` (Button, `<Keyboard>/r`) in both files -- runtime reads the embedded JSON.
- [x] `Assets/_Game/Scripts/UI/Inventory/BulkTransferRules.cs` -- new pure static: `const int BulkSize = 10`; `Quantity(bool bulk, int slotCount, int unitPrice, int budget)` per the Always rule; `ActionLabel(string verb, int qty)` -- testable core.
- [x] `ItemDetailFormatter.cs` / `ItemDetailPanelUI.cs` -- `Show(item, context, quantity = 1)`; total price + `Price (×N)` label when N > 1 -- existing callers unchanged.
- [x] `ContainerUI.cs` + `ContainerDetailActions.cs` -- `TakeItems(index, qty)` / `PutItems(index, qty)` (loop the one-unit rules, single refresh), `TakeAll()` (every unit of every slot, close if container empty afterwards), `[SerializeField] Button _takeAllButton` (interactable = container non-empty, refreshed with the grids); `Update` polls `TakeAll` and repaints the selection when `BulkModifier` changes; double-click, detail and context-menu actions use the batch -- one entry point per action.
- [x] `NPCTradeUI.cs` + `TradeDetailActions.cs` -- store selected index/side; `BuyItems` / `SellItems(index, qty)` with the quantity recomputed at execution; repaint buttons + price on Shift change; context menu label `Buy ×N (Tg)` -- price always matches what is charged.
- [x] `ContainerUI.prefab` -- add a `TakeAllButton` (reuse the existing UI button style) on the container pane header, wire `_takeAllButton` via MCP -- visible in loot + world containers.
- [x] `Assets/Tests/EditMode/BulkTransferRulesTests.cs` + `ItemDetailFormatterTests.cs` -- cover the I/O matrix quantities, label format and quantity-aware price.
- [x] `Assets/_Game/Scripts/UI/Inventory/CLAUDE.md` + `Assets/_Game/CLAUDE.md` -- document Take All / bulk rules and the two new UI actions.

**Acceptance Criteria:**
- Given a corpse with items, when the player presses `R` with the loot window open, then all items move, the window closes, and the weapon is not drawn.
- Given Shift held over a world-container stack of 15, when the player double-clicks it, then 10 move and `OnContainerItemTaken` fired 10 times.
- Given the trade screen open, when Shift is pressed or released, then the selected item's button label and price row update without re-clicking.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green, including `BulkTransferRulesTests`.
- `read_console` after compile -- expected: no errors.

**Manual checks:**
- Play mode: loot a corpse with `R` and the button; Shift-buy/sell at a shop; check gold totals.

## Suggested Review Order

**Batch rule (the core)**

- One pure function decides every batch size: Shift, stack, affordability, floor of 1.
  [`BulkTransferRules.cs:18`](../../Assets/_Game/Scripts/UI/Inventory/BulkTransferRules.cs#L18)

- Trade feeds it the buyer's gold; free items never advertise a batch.
  [`NPCTradeUI.cs:189`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L189)

- Container feeds it no price cap.
  [`ContainerUI.cs:176`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L176)

**Take All + R timing**

- Polled R / Shift: close happens after Player-map DrawWeapon was refused.
  [`ContainerUI.cs:59`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L59)

- Walks every slot, skips refused units, stops if a reaction closed the window.
  [`ContainerUI.cs:207`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L207)

- Single unit path: add first, remove after, one taken event per unit.
  [`ContainerUI.cs:236`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L236)

- Button disabled on an empty container.
  [`ContainerUI.cs:346`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L346)

**Batched transfers**

- Buy / sell loop the one-unit trade; stops when gold runs out.
  [`NPCTradeUI.cs:363`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L363)

- Take / put batches keep quest-item and take-only guards.
  [`ContainerUI.cs:190`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L190)

- Context menu freezes its quantity at open so label equals action.
  [`NPCTradeUI.cs:287`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L287)

**Live labels and price**

- Shift edge and gold change both repaint the selection.
  [`NPCTradeUI.cs:87`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L87)

- Panel totals the price for the batch.
  [`NPCTradeUI.cs:205`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L205)

- `Price (×N)` label lives in the pure formatter.
  [`ItemDetailFormatter.cs:157`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L157)

- Affordability checks the whole batch; click recomputes it.
  [`TradeDetailActions.cs:50`](../../Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs#L50)

- Container Take / Put labels.
  [`ContainerDetailActions.cs:40`](../../Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs#L40)

**Peripherals**

- New UI actions (Shift with initial state check, R) in the embedded JSON.
  [`InputSystem_Actions.cs:1818`](../../Assets/_Game/InputSystem_Actions.cs#L1818)

- Same actions in the editor asset.
  [`InputSystem_Actions.inputactions:990`](../../Assets/_Game/InputSystem_Actions.inputactions#L990)

- Batch-rule tests covering the I/O matrix.
  [`BulkTransferRulesTests.cs:1`](../../Assets/Tests/EditMode/BulkTransferRulesTests.cs#L1)

- Quantity-aware price tests.
  [`ItemDetailFormatterTests.cs:380`](../../Assets/Tests/EditMode/ItemDetailFormatterTests.cs#L380)
