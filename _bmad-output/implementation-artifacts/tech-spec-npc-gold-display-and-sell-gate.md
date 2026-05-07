---
title: 'NPC Gold Display and Sell Gate'
slug: 'npc-gold-display-and-sell-gate'
created: '2026-05-07'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C#', 'URP 17.x', 'Unity UI (UGUI)', 'TextMeshPro', 'Unity.InputSystem']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs'
  - 'Assets/_Game/Scripts/AI/NPCPresence.cs'
  - 'Assets/_Game/Scripts/World/DialogueSystem.cs'
  - 'Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs'
  - 'Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab (Editor wiring)'
code_patterns:
  - 'Runtime injection via Open() — NpcGoldSystem is not serialized in the Trade UI prefab; it is passed at open time from the NPC entity'
  - 'GameEventSO_Int _onGoldChanged already subscribed in OnEnable/OnDisable — UpdateGoldDisplay() reads from _npcGoldSystem directly, not from the event parameter'
  - 'GoldSystem.TrySpend() returns false and logs a warning on failure — callers check the return value and bail early'
  - 'Context menu buttons use Transform.Find("ButtonName") — null result must be guarded with GameLog.Warn'
  - 'All tags follow the [Prefix] convention; use GameLog.Info / GameLog.Warn / GameLog.Error — never Debug.Log'
test_patterns:
  - 'Manual playtest only — no automated UI test infrastructure in this project'
---

# Tech-Spec: NPC Gold Display and Sell Gate

**Created:** 2026-05-07

## Overview

### Problem Statement

`NPCTradeUI` has a `_npcGoldText` field missing: the NpcSide `InventoryPanel > header > GoldText` TMP is not wired to anything. `_npcGoldSystem` (the NPC's `GoldSystem`) is never injected into the Trade UI — the injection chain (`NPCPresence → NPCDialogueRequestData → DialogueSystem → NPCTradeUI.Open()`) does not carry the NPC's gold component. As a result, the NpcSide header always shows no gold, and `SellItem()` lets the player sell items to the NPC even when the NPC has 0 gold.

### Solution

Thread the NPC's `GoldSystem` through the existing dialogue-request chain, inject it into `NPCTradeUI` at open time, display it in the NpcSide header, and gate `SellItem()` (both double-click and context menu paths) on the NPC having enough gold.

### Scope

**In Scope:**
- Add `GoldSystem npcGoldSystem` to `NPCDialogueRequestData`
- Populate it in `NPCPresence.Interact()` via `GetComponent<GoldSystem>()`
- Store it in `DialogueSystem` and forward to `_tradeUI.Open()`
- Add `[SerializeField] private TMP_Text _npcGoldText` + `private GoldSystem _npcGoldSystem` to `NPCTradeUI`
- Update `NPCTradeUI.Open()` signature to accept `GoldSystem npcGoldSystem`
- `UpdateGoldDisplay()` populates `_npcGoldText` with NPC gold
- `SellItem()` gates on `_npcGoldSystem.TrySpend(item.sellValue)` before granting player gold
- Context menu sell button: `interactable = _npcGoldSystem != null && _npcGoldSystem.Gold >= item.sellValue`
- Wire `_npcGoldText` in `NPCTradeUI.prefab` (Editor step)

**Out of Scope:**
- Wiring `_goldSystem` (player GoldSystem) — it is null in the prefab and used only for player gold accumulation; fixing that wiring is a separate task
- Handling multiple NPCs sharing the same `_onGoldChanged` GameEventSO — current single-event design is a known limitation, out of scope
- `_canvas` being null in `NPCTradeUI.prefab` — pre-existing issue, separate fix

---

## Context for Development

### Codebase Patterns

- **`GoldSystem`** (`Game.Economy`): `MonoBehaviour` on both Player and NPC prefabs. Public API: `int Gold { get; }`, `bool TrySpend(int amount)` (returns false + warns on failure), `void Add(int amount)`. Raises `GameEventSO_Int _onGoldChanged` on every change.
- **`NPCDialogueRequestData`** (`Game.Core`): a plain `[System.Serializable]` struct used as the event payload. Fields are all public and nullable. Add fields freely.
- **`NPCPresence.Interact()`**: assembles the struct and raises it via `_onDialogueRequested.Raise(...)`. Uses `GetComponent<T>()` for optional components (inventory, memory, graph) — null is handled downstream.
- **`DialogueSystem`**: stores `_currentNPCInventory` from the request data; forwards it to `_tradeUI.Open(inv)` when a `ShopDialogueNode` is reached. Same pattern applies for NPC gold.
- **`NPCTradeUI.Open()`**: currently `Open(InventorySystem npcInventory)` — sets `_npcInventory` then calls `OnScreenOpen()`. Extend to also accept and store `GoldSystem npcGoldSystem`.
- **`UpdateGoldDisplay()`**: reads `_goldSystem.Gold` (player) into `_goldText` (PlayerSide). Add a parallel write: `_npcGoldText` ← `_npcGoldSystem.Gold`. Both writes are null-guarded.
- **`SellItem(int index)`** current flow: remove from player inventory → `_goldSystem.Add(item.sellValue)` → add to NPC inventory. After fix: gate → NPC spends gold → player receives gold → add to NPC inventory.
- **`ShowContextMenu()` sell path**: currently no `interactable` gate on the sell button. Add `sellBtn.interactable = _npcGoldSystem != null && _npcGoldSystem.Gold >= item.sellValue`.
- **Prefab wiring**: `_npcGoldText` targets the TMP component inside the NpcSide's `InventoryPanel` prefab instance (same inner path as `_goldText` which is already wired to PlayerSide's `InventoryPanel > header > GoldText`). Done in the Unity Editor Inspector on `NPCTradeUI.prefab`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs` | Struct to extend with `npcGoldSystem` |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Assembles and raises the dialogue request |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Stores request data; calls `_tradeUI.Open()` |
| `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` | All trade UI logic — main file to change |
| `Assets/_Game/Scripts/Economy/GoldSystem.cs` | API reference: `TrySpend`, `Add`, `Gold` |
| `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab` | Editor wiring for `_npcGoldText` |
| `Assets/_Game/Prefabs/Entities/NPCs/NPC_base Variant.prefab` | NPC already has `GoldSystem` (_startingGold: 500) |

### Technical Decisions

- **`_npcGoldSystem` is not `[SerializeField]`** — it lives on the NPC prefab, not the Trade UI prefab, so it cannot be statically serialized. It is a private field set at runtime via `Open()`.
- **`_npcGoldText` is `[SerializeField]`** — it references a child of the Trade UI prefab and must be wired in the Inspector like `_goldText`.
- **`SellItem()` blocks entirely when NPC cannot afford** — no partial sell, no silent pass-through. If `_npcGoldSystem` is null (NPC has no economy component), log a warning and return without transferring the item. This matches the existing `BuyItem()` gate pattern.
- **No changes to `_onGoldChanged` subscription** — it is already wired to the NPC's gold event SO and already calls `UpdateGoldDisplay()`. No new subscription needed.

---

## Implementation Plan

### Tasks

- [ ] Task 1: Add `npcGoldSystem` to `NPCDialogueRequestData`
  - File: `Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs`
  - Action: Add `public GoldSystem npcGoldSystem;` field after `npcInventory`. Add `using Game.Economy;` import.
  - Notes: Field is nullable — no NPC is required to have a GoldSystem.

- [ ] Task 2: Populate `npcGoldSystem` in `NPCPresence.Interact()`
  - File: `Assets/_Game/Scripts/AI/NPCPresence.cs`
  - Action: In the `NPCDialogueRequestData` initializer inside `Interact()`, add `npcGoldSystem = GetComponent<GoldSystem>()`. No null guard needed — null is handled by `DialogueSystem`.

- [ ] Task 3: Store and forward NPC GoldSystem in `DialogueSystem`
  - File: `Assets/_Game/Scripts/World/DialogueSystem.cs`
  - Action:
    1. Add `private GoldSystem _currentNPCGoldSystem;` field.
    2. In `HandleDialogueRequested()`, set `_currentNPCGoldSystem = data.npcGoldSystem;`.
    3. In `AdvanceToNode()` ShopDialogueNode branch, change `_tradeUI.Open(inv)` to `_tradeUI.Open(inv, _currentNPCGoldSystem)`.
    4. In `Close()`, add `_currentNPCGoldSystem = null;` alongside the other `_current*` null-outs.

- [ ] Task 4: Update `NPCTradeUI` — fields, Open(), UpdateGoldDisplay()
  - File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`
  - Action:
    1. In the `[Header("Economy")]` block, add `[SerializeField] private TMP_Text _npcGoldText;` directly after `_goldText`.
    2. Below the serialized fields, add `private GoldSystem _npcGoldSystem;` (no attribute — runtime-injected).
    3. Change `Open(InventorySystem npcInventory)` to `Open(InventorySystem npcInventory, GoldSystem npcGoldSystem)`. Inside, add `_npcGoldSystem = npcGoldSystem;` before the `SetActive` call.
    4. In `UpdateGoldDisplay()`, add after the existing `_goldText` write:
       ```csharp
       if (_npcGoldText != null && _npcGoldSystem != null)
           _npcGoldText.text = $"Gold: {_npcGoldSystem.Gold}g";
       ```

- [ ] Task 5: Gate `SellItem()` on NPC gold
  - File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`
  - Action: Replace the body of `SellItem(int index)` with:
    ```csharp
    private void SellItem(int index)
    {
        if (_playerInventory == null || index < 0 || index >= _playerInventory.Count) return;
        ItemSO item = _playerInventory.Items[index].Item;

        if (_npcGoldSystem == null || !_npcGoldSystem.TrySpend(item.sellValue))
        {
            GameLog.Warn(TAG, $"SellItem blocked: NPC cannot afford {item.itemName} ({item.sellValue}g)");
            return;
        }

        _playerInventory.DecrementStack(index);
        _goldSystem?.Add(item.sellValue);
        _npcInventory?.AddItem(item);
        RefreshGrids();
        UpdateGoldDisplay();
        GameLog.Info(TAG, $"Sold {item.itemName} for {item.sellValue}g");
    }
    ```
  - Notes: `_npcGoldSystem.TrySpend()` already logs its own warning — the warn here provides the trade-UI context.

- [ ] Task 6: Gate context menu sell button on NPC gold
  - File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`
  - Action: In `ShowContextMenu()`, after `sellBtn.GetComponentInChildren<TMP_Text>().text = ...`, add:
    ```csharp
    sellBtn.interactable = _npcGoldSystem != null && _npcGoldSystem.Gold >= item.sellValue;
    ```
  - Notes: Mirrors the existing buy button gate (`buyBtn.interactable = _goldSystem != null && _goldSystem.Gold >= item.buyValue`).

- [ ] Task 7: Wire `_npcGoldText` in the NPCTradeUI prefab
  - File: `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab`
  - Action: Open `NPCTradeUI.prefab` in the Unity Editor. In the Inspector for the root `NPCTradeUI` component, drag the `NpcSide > InventoryPanel > header > GoldText` TMP into the `_npcGoldText` field. Save the prefab.
  - Notes: The parallel PlayerSide field (`_goldText`) is already wired to `PlayerSide > InventoryPanel > header > GoldText` — same path, opposite side.

### Acceptance Criteria

- [ ] AC 1: Given the Trade UI opens with an NPC that has 500g, when `UpdateGoldDisplay()` runs, then `_npcGoldText` displays `"Gold: 500g"`.

- [ ] AC 2: Given the NPC has 50g and the player right-clicks a player item worth 30g (sellValue), when the context menu appears, then the Sell button is enabled (`interactable = true`).

- [ ] AC 3: Given the NPC has 20g and the player right-clicks a player item worth 30g (sellValue), when the context menu appears, then the Sell button is disabled (`interactable = false`).

- [ ] AC 4: Given the NPC has 50g and the player double-clicks a 30g item to sell, when `SellItem()` runs, then: the item is removed from the player inventory, the item is added to the NPC inventory, `_npcGoldText` updates to show `"Gold: 20g"`, and the player's gold increases by 30 (if `_goldSystem` is wired).

- [ ] AC 5: Given the NPC has 10g and the player double-clicks a 30g item to sell, when `SellItem()` runs, then: the item stays in the player inventory, the NPC inventory is unchanged, and a warning is logged.

- [ ] AC 6: Given `_npcGoldSystem` is null (NPC has no GoldSystem component), when the player attempts to sell any item, then `SellItem()` logs a warning and returns without transferring the item.

- [ ] AC 7: Given the NPC sells an item to the player (BuyItem), when the transaction completes, then NPC gold and NPC gold display are unaffected (BuyItem does not touch `_npcGoldSystem`).

---

## Additional Context

### Dependencies

- `GoldSystem` (`Assets/_Game/Scripts/Economy/GoldSystem.cs`) — must already exist on the NPC prefab (confirmed: `_startingGold: 500` is set).
- `GameEventSO_Int _onGoldChanged` SO (guid: `8e355bdf2d2bd8d48b8e53c8b37ce0e3`) — already wired in both the NPC prefab and `NPCTradeUI` component. No changes needed.

### Testing Strategy

Manual playtest steps:
1. Enter the game, approach the trade NPC, open dialogue, select the trade option.
2. Confirm NpcSide header shows `"Gold: 500g"` (or NPC's configured starting amount).
3. Right-click a player item with `sellValue ≤ NPC gold` — Sell button should be enabled.
4. Right-click a player item with `sellValue > NPC gold` — Sell button should be greyed out.
5. Sell an affordable item via double-click — confirm NpcSide gold decreases, player gold increases, item transferred.
6. Attempt to sell an item the NPC can't afford via double-click — confirm item stays in player inventory, warning in console.
7. Buy an NPC item — confirm NpcSide gold is unchanged.

### Notes

- **`_goldSystem` (player) is still null in the prefab** — `SellItem` calls `_goldSystem?.Add(item.sellValue)` with null-conditional, so this does not break the sell flow. Player gold display (`_goldText`) already shows player gold when `_goldSystem` is wired in scene. Wiring `_goldSystem` is a separate follow-up.
- **`_canvas` is null in `NPCTradeUI.prefab`** — context menus will fail silently. Pre-existing bug, not in scope here.
- Future: if multiple shop NPCs are open simultaneously, the shared `_onGoldChanged` SO will cross-fire between them. For the current single-NPC-at-a-time design this is fine.
