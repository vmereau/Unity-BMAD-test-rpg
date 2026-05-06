---
title: 'Inventory/Trade Context Menu Separation'
slug: 'inventory-trade-context-menu-separation'
created: '2026-05-06'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'C#', 'URP', 'Unity UI (UGUI)', 'TextMeshPro']
files_to_modify:
  - 'Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs'
  - 'Assets/_Game/Prefabs/UI/Inventory/NPCTradeUI.prefab'
  - 'Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab (read-only reference)'
  - 'Assets/_Game/Prefabs/UI/Inventory/TradeContextMenu.prefab (NEW — create in Editor)'
code_patterns:
  - 'Context menu instantiated into Canvas, positioned at screenPos, blocker full-screen behind it'
  - 'Buttons found by Transform.Find("ButtonName") — name string is the contract'
  - 'GameLog.Warn for null Find results, no throw'
  - 'VerticalLayoutGroup root + 150px width + 43.3px button height'
test_patterns:
  - 'Manual playtest only — no automated UI test infrastructure'
---

# Tech-Spec: Inventory/Trade Context Menu Separation

**Created:** 2026-05-06

## Overview

### Problem Statement

`InventoryUI` and `NPCTradeUI` share the same `InventoryPanel.prefab` (which contains the item grid). Both UIs instantiate the same context menu prefab on right-click, but `NPCTradeUI.ShowContextMenu` must repurpose inventory-specific buttons at runtime — renaming "UseButton" to "Buy (Xg)" and "DropButton" to "Sell (Xg)", while force-hiding "EquipButton". This runtime mutation is fragile: any new button added to the inventory context menu must be manually hidden in trade mode, and the semantic mismatch makes the code hard to reason about.

### Solution

Create a dedicated **Trade context menu prefab** with Buy/Sell buttons already wired with correct labels and layout. Update `NPCTradeUI` to reference this prefab. Update `InventoryUI` to reference an explicitly-named Inventory context menu prefab. Remove all runtime button-label mutation from `NPCTradeUI.ShowContextMenu`.

The shared `InventoryPanel.prefab` remains a dumb layout component — no mode/state logic is added to it. The intercept pattern (`TradeSideUI` as `IItemSlotContainer`) is preserved as-is.

### Scope

**In Scope:**
- Create `Assets/_Game/Prefabs/UI/Inventory/TradeContextMenu.prefab` (duplicate of existing `InventoryContextMenu.prefab`, rename buttons to BuyButton/SellButton, remove EquipButton)
- Refactor `NPCTradeUI.ShowContextMenu` — remove all runtime label/visibility hacks, fix missing blocker stretch anchors
- Wire `TradeContextMenu.prefab` onto `NPCTradeUI._contextMenuPrefab` in the Editor
- Verify `InventoryUI.prefab` already references `InventoryContextMenu.prefab` (already correctly named — confirmed in Step 2)

**Out of Scope:**
- Drag-and-drop between trade panels (buy/sell via drag) — deferred
- Formal `InventoryPanelMode` enum or `IInventoryPanelOwner` interface — not needed while InventoryPanel is a dumb layout
- Changing context menu behaviour (existing equip/use/drop logic stays identical)

---

## Context for Development

### Codebase Patterns

- `IItemSlotContainer` is the key decoupling interface. `ItemSlotUI.Awake` resolves it via `GetComponentInParent<IItemSlotContainer>()`. In inventory mode this resolves to `InventoryUI`; in trade mode it resolves to `TradeSideUI` (which delegates to `NPCTradeUI`). This is correct and must not be changed.
- Context menu lifecycle: the owning UI (`InventoryUI` or `NPCTradeUI`) instantiates the prefab into the Canvas, creates a full-screen transparent blocker behind it, and destroys both on dismiss. Pattern is identical; only the prefab differs.
- Button names inside the current context menu prefab: `"UseButton"`, `"DropButton"`, `"EquipButton"`. The trade prefab can name its buttons `"BuyButton"` and `"SellButton"` — `NPCTradeUI` will `Find` by those names.
- `GameLog.Warn(TAG, ...)` must be emitted if `Find("BuyButton")` or `Find("SellButton")` returns null (mirrors the existing EquipButton warn pattern).
- All cursor state changes go through `CursorManager` — context menu open/close does not touch cursor state.
- `TMP_Text` for button labels (already used throughout).

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` | ShowContextMenu to refactor |
| `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` | ShowContextMenu (reference for pattern; no logic change) |
| `Assets/_Game/Scripts/UI/Inventory/TradeSideUI.cs` | Intercept layer — read-only reference |
| `Assets/_Game/Scripts/UI/Inventory/IItemSlotContainer.cs` | Interface — read-only reference |
| `Assets/_Game/Prefabs/UI/Inventory/NPCTradeUI.prefab` | Update _contextMenuPrefab serialized reference |
| `Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab` | Update _contextMenuPrefab serialized reference |
| `Assets/_Game/Prefabs/UI/Inventory/InventoryPanel.prefab` | Layout-only — do not modify |

### Technical Decisions

- **Separate prefabs over mode parameter**: Chosen because each context menu has a genuinely different button set. A mode param on a shared prefab trades complexity now for a larger surface area to maintain forever.
- **InventoryPanel stays dumb**: No `IInventoryPanelOwner` interface introduced yet. This boundary holds as long as no per-panel-behavior (sort, filter, header title) is needed. If that changes in a future sprint, introduce an owner interface at that point.
- **Folder organisation**: Place both prefabs under `Assets/_Game/Prefabs/UI/Inventory/ContextMenu/` to keep them grouped.

---

## Implementation Plan

### Tasks

> Ordered by dependency — lowest level first.

**Task 1 — `InventoryContextMenu.prefab` already exists — no action needed**
- `Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab` already exists and is correctly named.
- Confirmed structure: root `InventoryContextMenu` (Image dark bg + VerticalLayoutGroup, 150px wide) → children `DropButton`, `UseButton`, `EquipButton` (each 43.3px tall, TMP child label).
- `InventoryUI` must reference this prefab for `_contextMenuPrefab` — verify in the Editor (no code change).

**Task 2 — Create `TradeContextMenu.prefab` in Unity Editor**

In the Unity Editor:
1. Duplicate `Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab`
2. Rename duplicate to `TradeContextMenu.prefab` (same folder: `Assets/_Game/Prefabs/UI/Inventory/`)
3. Open `TradeContextMenu.prefab` in Prefab Mode
4. Rename `UseButton` → `BuyButton`, update its child TMP text to `"Buy"` (runtime will set the price)
5. Rename `DropButton` → `SellButton`, update its child TMP text to `"Sell"`
6. Delete the `EquipButton` child entirely
7. Save and close Prefab Mode — Unity auto-generates the `.meta` GUID

> **Why Editor, not YAML:** Per project CLAUDE.md, direct YAML edits to prefabs followed by `refresh_unity(mode="force")` destroys the edits. Create in Editor, save, then use `refresh_unity(mode="if_dirty")` if needed.

**Task 3 — Refactor `NPCTradeUI.ShowContextMenu`**

File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`

Replace the entire `ShowContextMenu` method body. Current code:
- Finds `"UseButton"` and `"DropButton"` (inventory names) and mutates labels
- Finds `"EquipButton"` to force-hide it
- Creates blocker without stretch anchors (bug: blocker may not cover full screen)

New implementation:

```csharp
private void ShowContextMenu(int slotIndex, Vector2 screenPos, TradeSide side)
{
    HideContextMenu();
    _contextMenuSlotIndex = slotIndex;
    _contextMenuSide = side;

    InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
    if (inv == null || slotIndex < 0 || slotIndex >= inv.Count) return;
    ItemSO item = inv.Items[slotIndex].Item;

    // Blocker — full-screen transparent overlay; anchors stretch to fill canvas
    _contextMenuBlocker = new GameObject("ContextMenuBlocker");
    _contextMenuBlocker.transform.SetParent(_canvas.transform, false);
    var blockerImg = _contextMenuBlocker.AddComponent<Image>();
    blockerImg.color = Color.clear;
    blockerImg.raycastTarget = true;
    var blockerRect = _contextMenuBlocker.GetComponent<RectTransform>();
    blockerRect.anchorMin = Vector2.zero;
    blockerRect.anchorMax = Vector2.one;
    blockerRect.sizeDelta = Vector2.zero;
    _contextMenuBlocker.AddComponent<AnyButtonClickListener>().callback = (_) => HideContextMenu();

    _activeContextMenu = Instantiate(_contextMenuPrefab, _canvas.transform);
    _activeContextMenu.transform.SetAsLastSibling();
    var rt = _activeContextMenu.GetComponent<RectTransform>();
    rt.position = screenPos;

    var buyBtn = _activeContextMenu.transform.Find("BuyButton")?.GetComponent<Button>();
    var sellBtn = _activeContextMenu.transform.Find("SellButton")?.GetComponent<Button>();

    if (buyBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'BuyButton' not found in context menu prefab");
    if (sellBtn == null) GameLog.Warn(TAG, "ShowContextMenu: 'SellButton' not found in context menu prefab");

    if (side == TradeSide.NPC)
    {
        if (buyBtn != null)
        {
            buyBtn.gameObject.SetActive(true);
            buyBtn.GetComponentInChildren<TMP_Text>().text = $"Buy ({item.buyValue}g)";
            buyBtn.interactable = _goldSystem != null && _goldSystem.Gold >= item.buyValue;
            buyBtn.onClick.AddListener(() => { BuyItem(_contextMenuSlotIndex); HideContextMenu(); });
        }
        sellBtn?.gameObject.SetActive(false);
    }
    else
    {
        if (sellBtn != null)
        {
            sellBtn.gameObject.SetActive(true);
            sellBtn.GetComponentInChildren<TMP_Text>().text = $"Sell ({item.sellValue}g)";
            sellBtn.onClick.AddListener(() => { SellItem(_contextMenuSlotIndex); HideContextMenu(); });
        }
        buyBtn?.gameObject.SetActive(false);
    }
}
```

Note: `_contextMenuSlotIndex` (not captured `slotIndex`) is used in listeners — same pattern as `InventoryUI` to avoid closure capture of a changing local.

**Task 4 — Wire `TradeContextMenu.prefab` in `NPCTradeUI.prefab`**

In the Unity Editor:
1. Open `Assets/_Game/Prefabs/UI/Inventory/NPCTradeUI.prefab`
2. Select the root `NPCTradeUI` GameObject
3. In the Inspector, locate `NPCTradeUI._contextMenuPrefab` (serialized field, Header: "Context Menu")
4. Drag `TradeContextMenu.prefab` onto the field (replacing whatever was there)
5. Save the prefab

> `InventoryUI.prefab` should already point to `InventoryContextMenu.prefab` — verify but no change expected.

---

### Acceptance Criteria

**AC1 — Inventory context menu unchanged**
- Given: player opens inventory and right-clicks an item
- When: context menu appears
- Then: Drop, Use (if usable), Equip (if equippable) buttons appear with correct labels and behaviour — identical to pre-change

**AC2 — Trade context menu shows correct buttons by side**
- Given: trade UI is open, player right-clicks an NPC item
- When: context menu appears
- Then: only "Buy (Xg)" button is visible; interactable only if player has enough gold; clicking buys the item and closes the menu

- Given: trade UI is open, player right-clicks a Player item
- When: context menu appears
- Then: only "Sell (Xg)" button is visible; clicking sells the item and closes the menu

**AC3 — No runtime button-name strings from inventory appear in NPCTradeUI**
- `"UseButton"`, `"DropButton"`, `"EquipButton"` string literals must not appear in `NPCTradeUI.cs` after this change

**AC4 — Blocker works in trade UI**
- Given: trade context menu is open
- When: player clicks outside the panel
- Then: context menu and blocker are both destroyed

**AC5 — Missing button logs a warning, does not throw**
- Given: TradeContextMenu prefab is misconfigured (button renamed or missing)
- When: context menu is opened
- Then: `GameLog.Warn` fires with the button name; no NullReferenceException is thrown

---

## Additional Context

### Dependencies

- `InventoryContextMenu.prefab` confirmed to exist at `Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab` — Task 2 duplicates it directly.
- No new assembly references required — all types (`Button`, `TMP_Text`, `Image`, `AnyButtonClickListener`, `GameLog`) are already in the `Game` assembly.

### Testing Strategy

Manual playtest only (no automated UI tests in this project):
1. Open inventory → right-click item with each type (usable, equippable, plain) → verify correct buttons appear
2. Open trade → right-click NPC item → verify Buy button only, correct price, gold-gate works
3. Open trade → right-click player item → verify Sell button only, correct price
4. Open trade → right-click with insufficient gold → Buy button disabled
5. Click outside any open context menu → verify both menu and blocker are destroyed

### Notes

- **Long-term architectural boundary**: `InventoryPanel.prefab` must remain a dumb layout component. If a future sprint adds per-panel behaviour (sort button, filter, header showing inventory owner's name), introduce an `IInventoryPanelOwner` interface at that point — the panel calls its owner for configuration, the owner (`InventoryUI` or `NPCTradeUI`) provides the answers. Do not add mode/state logic to the panel preemptively.
- The `TradeSideUI` intercept pattern is the correct long-term routing solution. Do not replace it with a mode enum on `ItemSlotUI`.
- `NPCTradeUI.ShowContextMenu` is currently missing the full-screen blocker anchor setup that `InventoryUI` has — this is a minor bug, Task 3 fixes it as a side-effect.
