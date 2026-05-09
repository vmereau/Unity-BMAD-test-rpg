---
title: 'ItemDetailPanel Actions Prefab Separation'
slug: 'itemdetailpanel-actions-prefab-separation'
created: '2026-05-09'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C# (.NET Standard 2.1)', 'URP 17.x', 'Unity UI (UGUI)', 'TextMeshPro', 'Unity Input System']
files_to_modify:
  - 'Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab (strip ActionsWrapper -> empty ActionsContainer)'
  - 'Assets/_Game/Prefabs/UI/Inventory/InventoryDetailActions.prefab (NEW)'
  - 'Assets/_Game/Prefabs/UI/Trade/TradeDetailActions.prefab (NEW)'
  - 'Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs (NEW)'
  - 'Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs (NEW)'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs (refactor: display-only)'
  - 'Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs (UpdateDetailPanel + new _invActions field)'
  - 'Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs (UpdateDetailPanel + new _tradeActions field; BuyItem/SellItem -> public)'
  - 'Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs (OnSlotClicked also binds InventoryDetailActions)'
  - 'Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab (nest InventoryDetailActions; wire _invActions on InventoryUI + EquipmentUI)'
  - 'Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab (nest TradeDetailActions; wire _tradeActions on NPCTradeUI)'
code_patterns:
  - 'Per-mode prefab separation (mirrors tech-spec-inventory-trade-context-menu-separation)'
  - 'Static nested prefab + typed component (no runtime Instantiate; SerializeField button refs)'
  - 'Bind(...) takes systems via parameter, NOT SerializeField — keeps actions prefab free of system wiring'
  - 'Bind always RemoveAllListeners() before AddListener — prevents listener leak on item-change'
  - 'GameLog.Warn(TAG, ...) on null/missing dependencies; never throw'
  - 'CanvasGroup-based Hide() on ItemDetailPanel root — hides panel + nested actions together'
  - 'Editor-driven prefab edits (NEVER raw YAML + refresh_unity force)'
test_patterns:
  - 'Manual playtest only — no automated UI test infrastructure in this project'
---

# Tech-Spec: ItemDetailPanel Actions Prefab Separation

**Created:** 2026-05-09

## Overview

### Problem Statement

`ItemDetailPanel.prefab` is a shared item viewer used by both `InventoryUI` (inventory grid + equipment slots) and `NPCTradeUI` (NPC + Player trade grids). Today its `ActionsWrapper` child has **DropButton / UseButton / EquipButton** baked in, and `ItemDetailPanelUI.cs` directly references those buttons through `[SerializeField]` and manages all of their state (equip-toggle label, hide-when-equipped, hide-when-not-usable, internal `EquipmentSystem` / `InventorySystem` refs).

Two consequences fall out of this:

1. **Trade mode has the wrong actions.** `NPCTradeUI` calls the no-action `Show(item)` overload, but that overload still shows the equip button (because `ManageEquipButton` runs unconditionally). It also has no path to surface trade-specific actions (Buy / Sell).
2. **The detail panel script is overloaded.** `ItemDetailPanelUI` owns both display logic (icon/name/description/sections) AND inventory action logic, mixing concerns and blocking reuse from any non-inventory caller.

This mirrors the pattern resolved earlier for context menus (`tech-spec-inventory-trade-context-menu-separation`), which split a single context menu prefab into two purpose-built prefabs (`InventoryContextMenu` and `TradeContextMenu`).

### Solution

Apply the same prefab-separation pattern to the detail panel's actions area:

- Strip the inventory-specific `ActionsWrapper` out of `ItemDetailPanel.prefab` and replace it with an empty `ActionsContainer` (just a Transform child slot).
- Create two new prefabs:
  - `Assets/_Game/Prefabs/UI/Inventory/InventoryDetailActions.prefab` — DropButton / UseButton / EquipButton, plus a typed `InventoryDetailActions` MonoBehaviour with serialized button refs.
  - `Assets/_Game/Prefabs/UI/Trade/TradeDetailActions.prefab` — BuyButton / SellButton, plus a typed `TradeDetailActions` MonoBehaviour.
- In `InventoryUI.prefab` and `NPCTradeUI.prefab`, nest the appropriate actions prefab statically (in the editor) as a child of the local ItemDetailPanel's ActionsContainer. **No runtime Instantiate of the actions prefab.**
- Refactor `ItemDetailPanelUI.cs` down to display-only (icon, name, description, type sections). Remove `_dropButton` / `_useButton` / `_equipButton` / `_equipmentSystem` / `_inventorySystem` and the `ManageDropButton` / `ManageUseButton` / `ManageEquipButton` / `OnEquipClicked` / `OnUnequipClicked` methods. Drop the `Show(item, onDrop, onUse)` overload — leave one canonical `Show(item)` that only paints display data.
- Owner UIs orchestrate actions binding alongside display:
  - `InventoryUI.UpdateDetailPanel(item, slotIndex)` → `_detailPanelUI.Show(item); _invActions.Bind(this, slotIndex, item);`
  - `NPCTradeUI.UpdateDetailPanel(item, side)` → `_detailPanelUI.Show(item); _tradeActions.Bind(this, slotIndex, item, side);`
- The equipment-slot single-click flow keeps working: `EquipmentSlotUI` already routes through `InventoryUI`; `InventoryUI` calls `_invActions.BindForEquipmentSlot(item)` (or equivalent), which hides Drop / Use and only surfaces Equip / Unequip.
- Buy / Sell visibility per side: `TradeDetailActions.Bind` shows only BuyButton when `side == TradeSide.NPC` and only SellButton when `side == TradeSide.Player`. Wires onClick to `NPCTradeUI.BuyItem(slotIndex)` / `SellItem(slotIndex)`. Reuses the existing private buy/sell methods on `NPCTradeUI` — no business logic duplication.

### Scope

**In Scope:**

- Strip `ActionsWrapper` (DropButton, UseButton, EquipButton) from `Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab` and replace with an empty `ActionsContainer` Transform child (same RectTransform position the wrapper used).
- New prefab `Assets/_Game/Prefabs/UI/Inventory/InventoryDetailActions.prefab` — HorizontalLayoutGroup root, three button children (DropButton / UseButton / EquipButton), `InventoryDetailActions` component on the root.
- New prefab `Assets/_Game/Prefabs/UI/Trade/TradeDetailActions.prefab` — HorizontalLayoutGroup root, two button children (BuyButton / SellButton), `TradeDetailActions` component on the root.
- New script `Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs` (namespace `Game.UI`).
- New script `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs` (namespace `Game.UI`; lives alongside the existing `NPCTradeUI.cs` / `TradeSideUI.cs` since the project keeps inventory + trade UI scripts under `Scripts/UI/Inventory/`).
- Refactor `ItemDetailPanelUI.cs` to display-only.
- Update `InventoryUI.cs` to expose `DropItem(int)` / `UseItem(int)` / `EquipItem(int)` (the two former are already public; `EquipItem` may need a thin wrapper) so `InventoryDetailActions` can call them, and to call `_invActions.Bind(...)` from `UpdateDetailPanel`.
- Update `NPCTradeUI.cs` to call `_tradeActions.Bind(...)` from `UpdateDetailPanel`. Change `BuyItem` / `SellItem` from `private` to internal-callable (e.g. `public` or `internal` — see Step 2).
- Wire serialized refs in `InventoryUI.prefab` and `NPCTradeUI.prefab` (in editor) to point at the nested `InventoryDetailActions` / `TradeDetailActions` instance.

**Out of Scope:**

- Drag-and-drop between trade panels (still deferred — same as prior context-menu spec).
- Visual / styling redesign of any buttons.
- Refactoring or moving `TradeContextMenu.prefab` (already correct).
- Adding new actions (compare, drop quantity, split stack, etc.) to either prefab.
- Replacing the existing `IItemSlotContainer` / `TradeSideUI` intercept pattern.
- Test infrastructure — manual playtest only (project has no automated UI tests).

## Context for Development

### Codebase Patterns

- **Per-mode prefab separation precedent** — `tech-spec-inventory-trade-context-menu-separation` (status: implementation-complete) split a shared context-menu prefab into `InventoryContextMenu.prefab` + `TradeContextMenu.prefab`. Trade-side prefab ended up under `Assets/_Game/Prefabs/UI/Trade/` (not `Inventory/` as that older spec said). Follow the actual filesystem convention: trade prefabs live under `UI/Trade/`.
- **`ItemDetailPanel.prefab` is nested as a `PrefabInstance` in both owners** — confirmed by reading both prefabs:
  - `InventoryUI.prefab` line 63: `_detailPanelUI: {fileID: 5905112853055029648}` — points to a stripped MonoBehaviour ref tied to a `PrefabInstance` of `c3be44359bd5fa84898ee7c3ce739060` (ItemDetailPanel guid).
  - `NPCTradeUI.prefab` line 525: `_detailPanelUI: {fileID: 8525910066641873871}` — likewise. PrefabInstance lives at line 717 (`m_TransformParent: {fileID: 6551849258686380710}` = ItemDetailContainer in the trade UI; `m_SourcePrefab guid: c3be44359bd5fa84898ee7c3ce739060`).
  - **Implication:** stripping `ActionsWrapper` from the source `ItemDetailPanel.prefab` automatically propagates to both nested instances. New `ActionsContainer` shows up identically in both. Each owner then adds its own actions child as an "Added GameObject" override on the PrefabInstance.
- **`ItemSlotUI` resolves its container via `GetComponentInParent<IItemSlotContainer>()`.** Inventory grid → `InventoryUI`; trade grid → `TradeSideUI` (which forwards to `NPCTradeUI` with `TradeSide` enum). Do not change this.
- **Cursor state**: must go through `CursorManager.Lock()` / `CursorManager.Unlock()`. The new actions components do not touch cursor.
- **Logging**: `private const string TAG = "[InventoryDetailActions]"` / `"[TradeDetailActions]"`; `GameLog.Info` / `GameLog.Warn`. No `Debug.Log`.
- **TMP everywhere** — `TMP_Text` for button labels (`UnityEngine.UI.Text` is forbidden).
- **Assembly**: New scripts under `Assets/_Game/Scripts/UI/Inventory/` compile into the `Game` assembly automatically (no asmdef change).
- **Public method null guards** — every public method that dereferences a `[SerializeField]` dep needs a null guard, because `Awake` setting `enabled = false` does not block external callers (project HIGH-severity rule).
- **System refs are wired at `Player.prefab` level**, NOT inside `InventoryUI.prefab` itself — confirmed by InventoryUI.prefab lines 54/67 showing `_inventorySystem: {fileID: 0}` and `_equipmentSystem: {fileID: 0}`. Don't add a SerializeField `EquipmentSystem` on the new actions components — it would force a third layer of override wiring. Pass systems via `Bind(...)` instead.
- **`Show()` / `Hide()` call sites** (full enumeration via grep on `*.cs`):
  - `Show(item, onDrop, onUse)`: only `InventoryUI.cs:281` (`UpdateDetailPanel`).
  - `Show(item)` (no-arg): `EquipmentUI.cs:75` (`OnSlotClicked`) and `NPCTradeUI.cs:152` (`UpdateDetailPanel`).
  - `Hide()`: `InventoryUI.cs:289` + `:319`, `NPCTradeUI.cs:105` + `:157`. Equipment slot click never calls Hide directly.
- **`Hide()` mechanism is CanvasGroup-based** (`alpha = 0`, `blocksRaycasts = false`) — child actions are dimmed and pointer-blocked along with the panel root. No special teardown needed on the actions component.
- **`NPCTradeUI.BuyItem` / `SellItem` are `private` today** — must become `public` (or `internal` within the `Game` assembly) so `TradeDetailActions` can call them. Both already perform their own bounds and gold-gate checks, so opening visibility is safe.
- **`InventoryUI.DropItem(int)` and `UseItem(int)` are already `public`** — `InventoryDetailActions` can call them directly. Equip uses `_equipmentSystem.Equip(slotIndex)` / `Unequip(slot)` directly today; the new component will continue that pattern with a system reference passed via `Bind`.
- **`EquipmentUI.OnSlotClicked` is the equipment-slot path** — currently calls `_itemDetailPanel?.Show(item)` only. After the refactor it must also call `_invActions?.BindForEquipmentSlot(item, _equipmentSystem)`. EquipmentUI already has its own `[SerializeField] _equipmentSystem` so passing it is free.
- **TradeContextMenu.prefab guid is `a1b2c3d4e5f6789012345678abcdef01`** (deliberate placeholder pattern committed in the prior spec) — informational only; not relevant here.
- **Listener-leak risk**: every `Bind(...)` call replaces the previous binding. Both new components must `RemoveAllListeners()` on each button before each `AddListener`, mirroring the existing `Manage*Button` pattern in `ItemDetailPanelUI`.
- **MCP gotcha (project CLAUDE.md)**: raw YAML edit + `refresh_unity(mode="force")` destroys edits. All prefab structural changes happen in the Editor (or via MCP `manage_prefabs` tools); use `refresh_unity(mode="if_dirty")` only.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs` | Refactor target — strip action management; keep display-only |
| `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` | Add `_invActions` field; rewire `UpdateDetailPanel` |
| `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` | Add `_tradeActions` field; `BuyItem`/`SellItem` → public; rewire `UpdateDetailPanel` |
| `Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs` | Add `_invActions` field; `OnSlotClicked` calls `BindForEquipmentSlot` |
| `Assets/_Game/Scripts/UI/Inventory/EquipmentSlotUI.cs` | Read-only — confirms double-click vs single-click routing |
| `Assets/_Game/Scripts/UI/Inventory/TradeSideUI.cs` | Read-only — confirms how `TradeSide` is established |
| `Assets/_Game/Scripts/UI/Inventory/IItemSlotContainer.cs` | Read-only — interface contract (unchanged) |
| `Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab` | Strip `ActionsWrapper` → empty `ActionsContainer` (keep RectTransform anchored at the wrapper's old layout slot inside the panel) |
| `Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab` | Nest `InventoryDetailActions.prefab` as Added GameObject under the `ItemDetailPanel/ActionsContainer` PrefabInstance; wire `_invActions` on `InventoryUI` and `EquipmentUI` |
| `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab` | Nest `TradeDetailActions.prefab` as Added GameObject under `ItemDetailPanel/ActionsContainer`; wire `_tradeActions` on `NPCTradeUI` |
| `_bmad-output/implementation-artifacts/tech-spec-inventory-trade-context-menu-separation.md` | Reference for the same pattern applied to context menus |
| `_bmad-output/project-context.md` | Mandatory rules: GameLog usage, [SerializeField] private convention, naming, no Debug.Log, no Resources.Load |

### Technical Decisions

- **Static nested prefab + typed component** (chosen over runtime `Instantiate` + `Find` by name). Detail-panel actions live for the entire panel lifetime, so editor-time wiring beats per-selection allocation. Type-safe button refs eliminate stringly-typed lookups.
- **Per-mode component** (`InventoryDetailActions`, `TradeDetailActions`) — separate prefabs already exist; separate scripts let each carry its own typed `Bind(...)` signature. A single shared component with nullable button fields would just smear unrelated state across both prefabs.
- **`Bind` takes systems by parameter, no SerializeField systems on actions component.** Avoids forcing a third level of editor wiring (`Player.prefab → InventoryUI → InventoryDetailActions`) for refs that the owner UI already holds. Each `Bind` call signature passes only what the component needs:
  - `InventoryDetailActions.Bind(InventoryUI owner, int slotIndex, ItemSO item, EquipmentSystem equipmentSystem)`
  - `InventoryDetailActions.BindForEquipmentSlot(ItemSO item, EquipmentSystem equipmentSystem)` — hides Drop/Use, surfaces Equip/Unequip only
  - `TradeDetailActions.Bind(NPCTradeUI owner, int slotIndex, ItemSO item, TradeSide side, GoldSystem playerGold, GoldSystem npcGold)`
- **Equipment-slot single-click reuses `InventoryDetailActions`** via `BindForEquipmentSlot`. One component, two binding entrypoints, no third `EquipmentDetailActions` prefab.
- **`ItemDetailPanelUI` becomes display-only.** Removes `_dropButton` / `_useButton` / `_equipButton` / `_equipmentSystem` / `_inventorySystem` SerializeFields and the `Manage*Button` / `OnEquipClicked` / `OnUnequipClicked` methods. Drops the `Show(item, onDrop, onUse)` overload; only `Show(item)` and `Hide()` remain. Does NOT keep an `_actionsContainer` Transform field — the owner UI references the actions component directly, so the panel script does not need to know about it.
- **`AllSlots` constant moves to `InventoryDetailActions`** — `static readonly EquipmentSlot[] AllSlots = (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));` (used inside `OnUnequipClicked`) follows the equip-button logic to its new home.
- **`NPCTradeUI.BuyItem` / `SellItem` become `public`.** All callers stay within the `Game` assembly; no API risk.
- **`InventoryUI.UpdateDetailPanel` signature stays `(ItemSO item, int slotIndex)`** but body becomes:
  ```csharp
  _detailPanelUI.Show(item);
  _invActions.Bind(this, slotIndex, item, _equipmentSystem);
  ```
- **`NPCTradeUI.UpdateDetailPanel` signature stays `(ItemSO item, TradeSide side)`** but takes/uses the slotIndex (already in scope from caller `OnSlotSelected`). New body:
  ```csharp
  _detailPanelUI.Show(item);
  _tradeActions.Bind(this, slotIndex, item, side, _goldSystem, _npcGoldSystem);
  ```
  → call site `OnSlotSelected` passes `slotIndex` through.
- **Layout**: `InventoryDetailActions.prefab` keeps the existing `HorizontalLayoutGroup` settings from `ActionsWrapper` (Spacing 8, ChildForceExpand both, ChildControl both). `TradeDetailActions.prefab` mirrors the same root layout for visual consistency, sized to host 2 buttons.
- **No `m_AddedComponents` hacks** — the actions root `GameObject` carries the new MonoBehaviour as part of its own prefab definition; the owner prefab adds the actions prefab as a nested instance via standard Unity nesting.

## Implementation Plan

### Tasks

> Ordered by dependency — lowest level first. Each task is atomic; verify Unity console is clean after every script-level task before continuing.

**Phase A — C# (must compile cleanly before any prefab work)**

**Task 1 — `NPCTradeUI.cs`: open visibility on `BuyItem` / `SellItem`**

- File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`
- Action: Change both methods from `private` to `public`. No body changes.
  - Line 245: `private void BuyItem(int index)` → `public void BuyItem(int index)`
  - Line 261: `private void SellItem(int index)` → `public void SellItem(int index)`
- Notes: Required so `TradeDetailActions.Bind(...)` can wire button onClicks to these methods. Both already perform their own bounds and gold-gate checks, so opening visibility does not relax invariants.

**Task 2 — Create `Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs`**

- File: `Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs` (NEW)
- Action: Create the new MonoBehaviour. Namespace `Game.UI`. Standalone — no dependency on Inventory prefab being updated yet.

```csharp
using Game.Core;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hosts the inventory-mode action buttons (Drop / Use / Equip) for ItemDetailPanel.
    /// Lives on the root of InventoryDetailActions.prefab. Bound by the owner UI on item-change.
    /// </summary>
    public class InventoryDetailActions : MonoBehaviour
    {
        private const string TAG = "[InventoryDetailActions]";

        [SerializeField] private Button _dropButton;
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _equipButton;

        private static readonly EquipmentSlot[] AllSlots =
            (EquipmentSlot[])System.Enum.GetValues(typeof(EquipmentSlot));

        /// <summary>
        /// Bind for an inventory grid slot click — Drop/Use visible per item type and equipped state,
        /// Equip toggles between Equip/Unequip text.
        /// </summary>
        public void Bind(InventoryUI owner, int slotIndex, ItemSO item, EquipmentSystem equipmentSystem)
        {
            if (item == null) { GameLog.Warn(TAG, "Bind: item is null"); return; }
            if (owner == null) { GameLog.Warn(TAG, "Bind: owner is null"); return; }
            ManageDropButton(owner, slotIndex, item, equipmentSystem);
            ManageUseButton(owner, slotIndex, item);
            ManageEquipButton(item, equipmentSystem);
        }

        /// <summary>
        /// Bind for an equipment-slot single-click — only Equip/Unequip visible; Drop and Use hidden.
        /// </summary>
        public void BindForEquipmentSlot(ItemSO item, EquipmentSystem equipmentSystem)
        {
            if (item == null) { GameLog.Warn(TAG, "BindForEquipmentSlot: item is null"); return; }
            if (_dropButton != null) _dropButton.gameObject.SetActive(false);
            if (_useButton != null) _useButton.gameObject.SetActive(false);
            ManageEquipButton(item, equipmentSystem);
        }

        private void ManageDropButton(InventoryUI owner, int slotIndex, ItemSO item, EquipmentSystem es)
        {
            if (_dropButton == null) return;
            _dropButton.onClick.RemoveAllListeners();

            bool isEquipped = es != null && es.IsEquipped(item);
            if (isEquipped) { _dropButton.gameObject.SetActive(false); return; }

            _dropButton.gameObject.SetActive(true);
            _dropButton.onClick.AddListener(() => owner.DropItem(slotIndex));
        }

        private void ManageUseButton(InventoryUI owner, int slotIndex, ItemSO item)
        {
            if (_useButton == null) return;
            _useButton.onClick.RemoveAllListeners();

            if (item is not UsableItemSO) { _useButton.gameObject.SetActive(false); return; }

            _useButton.gameObject.SetActive(true);
            _useButton.onClick.AddListener(() => owner.UseItem(slotIndex));
        }

        private void ManageEquipButton(ItemSO item, EquipmentSystem es)
        {
            if (_equipButton == null) return;
            _equipButton.onClick.RemoveAllListeners();

            if (item is not EquipableItemSO) { _equipButton.gameObject.SetActive(false); return; }

            _equipButton.gameObject.SetActive(true);
            bool isEquipped = es != null && es.IsEquipped(item);

            var label = _equipButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = isEquipped ? "Unequip" : "Equip";

            if (isEquipped)
                _equipButton.onClick.AddListener(() => OnUnequipClicked(item, es));
            else
                _equipButton.onClick.AddListener(() => OnEquipClicked(item, es));
        }

        private void OnEquipClicked(ItemSO item, EquipmentSystem es)
        {
            if (es == null) { GameLog.Warn(TAG, "OnEquipClicked: equipmentSystem is null"); return; }
            // Locate the inventory slot index by item identity — mirrors the old ItemDetailPanelUI pattern.
            // EquipmentSystem.Equip takes an InventorySystem index, so we ask EquipmentSystem to do that lookup.
            // (If a more direct API exists, prefer it; otherwise this preserves the prior behavior.)
            es.Equip(item);
        }

        private void OnUnequipClicked(ItemSO item, EquipmentSystem es)
        {
            if (es == null) return;
            foreach (var slot in AllSlots)
            {
                if (es.GetEquipped(slot) == item) { es.Unequip(slot); return; }
            }
        }
    }
}
```

- Notes:
  - `EquipmentSystem.Equip(ItemSO)` overload may not exist today (current code calls `Equip(int slotIndex)`). The dev implementing this task must check `EquipmentSystem.cs` and either:
    - Add a `public void Equip(ItemSO item)` overload that resolves the slot index via the inventory (mirroring the old `ItemDetailPanelUI.OnEquipClicked` loop), OR
    - Keep the original loop here: take `InventorySystem` via Bind and walk it to find the slot.
  - Recommended: add the overload to `EquipmentSystem.cs`; it's a one-method addition that simplifies all callers and keeps `InventoryDetailActions` minimal.
  - All public Bind methods guard for null inputs and use `GameLog.Warn` (per project rule).

**Task 3 — Create `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs`**

- File: `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs` (NEW)
- Action: Create the new MonoBehaviour. Namespace `Game.UI`. Depends on Task 1 (public BuyItem/SellItem on NPCTradeUI).

```csharp
using Game.Core;
using Game.Economy;
using Game.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Hosts the trade-mode action buttons (Buy / Sell) for ItemDetailPanel.
    /// Lives on the root of TradeDetailActions.prefab. Bound by NPCTradeUI on item/side-change.
    /// </summary>
    public class TradeDetailActions : MonoBehaviour
    {
        private const string TAG = "[TradeDetailActions]";

        [SerializeField] private Button _buyButton;
        [SerializeField] private Button _sellButton;

        public void Bind(NPCTradeUI owner, int slotIndex, ItemSO item, TradeSide side,
                         GoldSystem playerGold, GoldSystem npcGold)
        {
            if (item == null)  { GameLog.Warn(TAG, "Bind: item is null");  return; }
            if (owner == null) { GameLog.Warn(TAG, "Bind: owner is null"); return; }

            if (_buyButton != null)  _buyButton.onClick.RemoveAllListeners();
            if (_sellButton != null) _sellButton.onClick.RemoveAllListeners();

            if (side == TradeSide.NPC)
            {
                ShowBuy(owner, slotIndex, item, playerGold);
                if (_sellButton != null) _sellButton.gameObject.SetActive(false);
            }
            else
            {
                ShowSell(owner, slotIndex, item, npcGold);
                if (_buyButton != null) _buyButton.gameObject.SetActive(false);
            }
        }

        private void ShowBuy(NPCTradeUI owner, int slotIndex, ItemSO item, GoldSystem playerGold)
        {
            if (_buyButton == null) { GameLog.Warn(TAG, "Bind: BuyButton is not assigned"); return; }
            _buyButton.gameObject.SetActive(true);
            var label = _buyButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"Buy ({item.buyValue}g)";
            _buyButton.interactable = playerGold != null && playerGold.Gold >= item.buyValue;
            _buyButton.onClick.AddListener(() => owner.BuyItem(slotIndex));
        }

        private void ShowSell(NPCTradeUI owner, int slotIndex, ItemSO item, GoldSystem npcGold)
        {
            if (_sellButton == null) { GameLog.Warn(TAG, "Bind: SellButton is not assigned"); return; }
            _sellButton.gameObject.SetActive(true);
            var label = _sellButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"Sell ({item.sellValue}g)";
            _sellButton.interactable = npcGold != null && npcGold.Gold >= item.sellValue;
            _sellButton.onClick.AddListener(() => owner.SellItem(slotIndex));
        }
    }
}
```

- Notes:
  - Mirrors the buy/sell visibility + price-label + gold-gate pattern that currently lives in `NPCTradeUI.ShowContextMenu` (lines 215–236) — just relocated.
  - `RemoveAllListeners()` on every Bind prevents listener leak when selection toggles between sides.

**Task 4 — Refactor `ItemDetailPanelUI.cs`: display-only**

- File: `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs`
- Action: Strip all action-button logic. The class becomes display-only.
- Remove:
  - SerializeFields: `_dropButton`, `_useButton`, `_equipButton`, `_equipmentSystem`, `_inventorySystem`.
  - The `private static readonly EquipmentSlot[] AllSlots` field.
  - Methods: `OnEquipClicked`, `OnUnequipClicked`, `ManageUseButton`, `ManageDropButton`, `ManageEquipButton`.
  - The `Show(ItemSO item, System.Action onDrop, System.Action onUse)` overload entirely.
- Keep:
  - All display fields (`_icon`, `_nameText`, `_descriptionText`, all section GameObjects + their TMP labels).
  - `Awake` (CanvasGroup cache).
  - `Show(ItemSO item)` — single canonical overload — body becomes only `ShowBaseItemDetails(item); ShowSections(item);` plus the `_canvasGroup` show flow and `gameObject.SetActive(true)`.
  - `Hide()` (unchanged).
  - All `Show*Section` / `HideTypeSections` / `Format*` helpers.
- Imports: drop `using Game.Progression;` if no longer used (verify by inspection).
- Notes: After this task, `ItemDetailPanel.prefab` will have stale serialized refs (`_dropButton`/`_useButton`/`_equipButton`/`_equipmentSystem`/`_inventorySystem`) that point to fields that no longer exist on the script. Unity will log a "missing field" reimport warning per field. These resolve once Task 9 strips `ActionsWrapper` from the prefab.

**Task 5 — Refactor `InventoryUI.cs`: wire `_invActions`**

- File: `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs`
- Action:
  - Add new SerializeField: `[SerializeField] private InventoryDetailActions _invActions;` (header `[Header("Detail Actions")]` is fine).
  - Replace `UpdateDetailPanel(ItemSO item, int slotIndex)` body:
    ```csharp
    private void UpdateDetailPanel(ItemSO item, int slotIndex)
    {
        _detailPanelUI.Show(item);
        _invActions?.Bind(this, slotIndex, item, _equipmentSystem);
    }
    ```
  - The old call `_detailPanelUI.Show(item, () => DropItem(slotIndex), onUse);` is replaced as above; the `onUse` local can be deleted.
  - `RefreshSlots(...)` already calls `UpdateDetailPanel` for the restored selection — no extra change needed.
- Notes:
  - `_equipmentSystem` is already a SerializeField on `InventoryUI` — pass it through. No new wiring.
  - Behavior on `null` item: existing `ClearSelection`/`Hide()` paths continue to dim the panel via `CanvasGroup`; the actions component does not need to be told.

**Task 6 — Refactor `EquipmentUI.cs`: wire `_invActions` for the equipment-slot path**

- File: `Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs`
- Action:
  - Add `[SerializeField] private InventoryDetailActions _invActions;` next to `_itemDetailPanel`.
  - Update `OnSlotClicked`:
    ```csharp
    public void OnSlotClicked(EquipmentSlot slot, ItemSO item)
    {
        _itemDetailPanel?.Show(item);
        _invActions?.BindForEquipmentSlot(item, _equipmentSystem);
    }
    ```
- Notes: `_equipmentSystem` is already a SerializeField on `EquipmentUI` — reuse it.

**Task 7 — Refactor `NPCTradeUI.cs`: wire `_tradeActions`, propagate `slotIndex`**

- File: `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`
- Action:
  - Add `[SerializeField] private TradeDetailActions _tradeActions;` under the `[Header("Detail Panel")]` block (or a new `[Header("Detail Actions")]`).
  - Change `UpdateDetailPanel` signature from `(ItemSO item, TradeSide side)` to `(ItemSO item, int slotIndex, TradeSide side)` and rewrite body:
    ```csharp
    private void UpdateDetailPanel(ItemSO item, int slotIndex, TradeSide side)
    {
        if (_detailPanelUI == null) return;
        _detailPanelUI.Show(item);
        _tradeActions?.Bind(this, slotIndex, item, side, _goldSystem, _npcGoldSystem);
    }
    ```
  - Update the only caller (`OnSlotSelected`, line 127–135) to pass `index` through:
    ```csharp
    public void OnSlotSelected(int index, TradeSide side)
    {
        InventorySystem inv = (side == TradeSide.NPC) ? _npcInventory : _playerInventory;
        if (inv != null && index >= 0 && index < inv.Count)
        {
            ItemSO item = inv.Items[index].Item;
            UpdateDetailPanel(item, index, side);
        }
    }
    ```
  - Delete the obsolete comment `// No Use/Drop/Equip buttons in Trade UI`.
- Notes: `BuyItem` / `SellItem` were made public in Task 1, so `TradeDetailActions` can reach them.

**Phase B — Prefabs (only after Phase A compiles cleanly via `read_console`)**

**Task 8 — Create `Assets/_Game/Prefabs/UI/Inventory/InventoryDetailActions.prefab`**

In the Unity Editor (or via MCP `manage_prefabs` tool):

1. Create a new GameObject named `InventoryDetailActions` with components: `RectTransform`, `HorizontalLayoutGroup`, `InventoryDetailActions` (script).
2. `HorizontalLayoutGroup`: Spacing 8, ChildForceExpand W+H ON, ChildControl W+H ON (mirror current `ActionsWrapper` settings — see ItemDetailPanel.prefab line 985–999).
3. Add three children, each a UI `Button` with a `TMP_Text` child:
   - `DropButton` — TMP text "Drop"
   - `UseButton` — TMP text "Use"
   - `EquipButton` — TMP text "Equip"
4. On the `InventoryDetailActions` component, wire the three serialized refs (`_dropButton`, `_useButton`, `_equipButton`) to the corresponding child Button components.
5. Save as a prefab at `Assets/_Game/Prefabs/UI/Inventory/InventoryDetailActions.prefab`.

> **Why Editor / MCP, not raw YAML:** Per project CLAUDE.md, raw YAML edits + `refresh_unity(mode="force")` destroy edits. Use Editor / MCP `manage_prefabs` and `refresh_unity(mode="if_dirty")`.

**Task 9 — Create `Assets/_Game/Prefabs/UI/Trade/TradeDetailActions.prefab`**

1. Create a new GameObject named `TradeDetailActions` with `RectTransform`, `HorizontalLayoutGroup`, `TradeDetailActions` (script).
2. `HorizontalLayoutGroup`: same settings as Task 8 for visual consistency.
3. Add two children:
   - `BuyButton` (Button + TMP_Text "Buy")
   - `SellButton` (Button + TMP_Text "Sell")
4. Wire `_buyButton` and `_sellButton` SerializeFields on the `TradeDetailActions` component.
5. Save at `Assets/_Game/Prefabs/UI/Trade/TradeDetailActions.prefab`.

**Task 10 — Strip `ActionsWrapper` from `ItemDetailPanel.prefab`**

In the Unity Editor:

1. Open `Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab` in Prefab Mode.
2. Locate the `ActionsWrapper` child (currently a `HorizontalLayoutGroup` with three Button children: `DropButton`, `UseButton`, `EquipButton` — see `ItemDetailPanel.prefab` lines 935–999).
3. Delete the three Button children but keep the parent GameObject.
4. Rename the parent GameObject from `ActionsWrapper` to `ActionsContainer`.
5. Keep the `RectTransform` settings (anchored position + sizeDelta) so the layout slot remains where it was. Remove the `HorizontalLayoutGroup` if desired (the nested actions prefab brings its own).
6. The `ItemDetailPanelUI` MonoBehaviour on the panel root will already have lost its `_dropButton` / `_useButton` / `_equipButton` / `_equipmentSystem` / `_inventorySystem` field bindings due to Task 4. After saving, those serialized fields disappear from the YAML automatically.
7. Save the prefab.
8. Run `refresh_unity(mode="if_dirty")` if needed.

**Task 11 — Wire `InventoryUI.prefab`: nest `InventoryDetailActions` + serialize refs**

In the Unity Editor:

1. Open `Assets/_Game/Prefabs/UI/Inventory/InventoryUI.prefab` in Prefab Mode.
2. Drill into the nested `ItemDetailPanel` PrefabInstance and locate its `ActionsContainer` child (now empty after Task 10).
3. Drag `InventoryDetailActions.prefab` (Task 8) onto `ActionsContainer` to add it as a child PrefabInstance (creates an "Added GameObject" override on the ItemDetailPanel PrefabInstance).
4. Select the `InventoryUI` GameObject root. In the Inspector, find the new `_invActions` field on the `InventoryUI` script and drag the nested `InventoryDetailActions` GameObject onto it.
5. Drill into the `EquipmentUI` child GameObject. Find the new `_invActions` field on the `EquipmentUI` script and drag the SAME nested `InventoryDetailActions` GameObject onto it.
6. Save the prefab.

> Both InventoryUI and EquipmentUI point at the SAME nested actions instance — there is only one detail panel and one set of action buttons at any time.

**Task 12 — Wire `NPCTradeUI.prefab`: nest `TradeDetailActions` + serialize ref**

In the Unity Editor:

1. Open `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab` in Prefab Mode.
2. Drill into the nested `ItemDetailPanel` PrefabInstance (under `ItemDetailContainer`) and locate `ActionsContainer`.
3. Drag `TradeDetailActions.prefab` (Task 9) onto `ActionsContainer` to add it as a child PrefabInstance.
4. Select the `NPCTradeUI` GameObject root. In the Inspector, find `_tradeActions` on the `NPCTradeUI` script and drag the nested `TradeDetailActions` GameObject onto it.
5. Save the prefab.

**Phase C — Verify**

**Task 13 — Manual playtest matrix**

Run through every row of the testing matrix in *Testing Strategy* below and verify console is clean (no NullReferenceException, MissingReferenceException, MissingFieldException, or compile errors).

### Acceptance Criteria

**AC1 — Inventory grid item: type-correct buttons + behavior unchanged**

- Given: player opens the inventory and clicks an item in the grid.
- When: the detail panel is shown.
- Then: Drop button is visible (unless item is currently equipped); Use button is visible only if `item is UsableItemSO`; Equip button is visible only if `item is EquipableItemSO` with label "Equip" (when not equipped). Clicking each button executes the same `DropItem` / `UseItem` / `EquipmentSystem.Equip` behavior as before this refactor.

**AC2 — Equipped item: Drop hidden, Equip toggles to "Unequip"**

- Given: an equippable item is already equipped on the player.
- When: player clicks the inventory slot containing that item.
- Then: Drop button is hidden; Equip button label reads "Unequip"; clicking Unequip calls `EquipmentSystem.Unequip(slot)` for the matching slot and the panel + grid refresh accordingly.

**AC3 — Equipment slot single-click: only Equip/Unequip surfaces**

- Given: player has any item equipped in an `EquipmentSlotUI`.
- When: player single-clicks (not double-clicks) that equipment slot.
- Then: detail panel opens with the item info; Drop and Use buttons are hidden; only the Equip/Unequip button is visible and labeled "Unequip" with a working onClick.

**AC4 — Trade NpcSide selection: Buy only**

- Given: trade UI is open and an NPC inventory has at least one item.
- When: player clicks an item on the NpcSide grid.
- Then: detail panel shows item info; in the actions area only "Buy ({item.buyValue}g)" is visible; Buy button `interactable == (player gold ≥ buyValue)`; clicking Buy executes `NPCTradeUI.BuyItem(slotIndex)`, gold and inventory grids update, the same item now appears on PlayerSide.

**AC5 — Trade PlayerSide selection: Sell only**

- Given: trade UI is open and the player has at least one item.
- When: player clicks an item on the PlayerSide grid.
- Then: only "Sell ({item.sellValue}g)" button is visible; `interactable == (npc gold ≥ sellValue)`; clicking Sell executes `NPCTradeUI.SellItem(slotIndex)`, gold and grids update, the item now appears on NpcSide.

**AC6 — Side toggle does not stack listeners**

- Given: trade UI is open and player rapidly alternates selection between NpcSide and PlayerSide items at least 5 times.
- When: player clicks Buy or Sell on the most recent selection.
- Then: exactly one buy or sell action fires (no leaked listeners from previous selections); the action targets the currently selected item only.

**AC7 — No inventory action buttons appear in trade mode**

- Given: trade UI is open.
- When: any item is selected on either side.
- Then: Drop, Use, and Equip buttons are not present anywhere in the detail panel (the trade UI's nested ItemDetailPanel only hosts `TradeDetailActions`, not `InventoryDetailActions`).

**AC8 — `ItemDetailPanelUI.cs` is display-only**

- Given: a `grep` of `ItemDetailPanelUI.cs` after the refactor.
- When: searching for the strings `_dropButton`, `_useButton`, `_equipButton`, `_equipmentSystem`, `_inventorySystem`, `OnEquipClicked`, `OnUnequipClicked`, `ManageDropButton`, `ManageUseButton`, `ManageEquipButton`, or the `Show(ItemSO item, System.Action onDrop, System.Action onUse)` overload signature.
- Then: zero matches for each.

**AC9 — Hide propagates to actions**

- Given: detail panel is showing with action buttons visible.
- When: `Hide()` is called (e.g. via `ClearSelection` or `OnScreenClose`).
- Then: panel `CanvasGroup.alpha == 0` and `blocksRaycasts == false`. Action buttons inside `ActionsContainer` are visually hidden and pointer-blocked along with the rest of the panel (no extra teardown logic needed in actions components).

**AC10 — Console clean across all flows**

- Given: the project is opened with the refactor merged.
- When: scenarios AC1–AC7 are exercised end-to-end during a manual playtest.
- Then: Unity console shows no `NullReferenceException`, `MissingReferenceException`, `MissingFieldException`, compile errors, or warnings about missing serialized references on `ItemDetailPanel.prefab`, `InventoryDetailActions.prefab`, or `TradeDetailActions.prefab`.

**AC11 — Insufficient gold disables Buy/Sell**

- Given: trade UI is open.
- When: player gold < item.buyValue (NpcSide select) OR npc gold < item.sellValue (PlayerSide select).
- Then: the corresponding button is rendered but `interactable == false`; clicking does nothing; no state change occurs.

**AC12 — Missing button reference logs a warning, does not throw**

- Given: a hypothetical misconfigured prefab where one of the SerializeField buttons (e.g. `_buyButton`) is unassigned.
- When: `Bind(...)` is called.
- Then: a `GameLog.Warn` is emitted naming the missing button; no `NullReferenceException` is thrown; the other button still binds normally.

## Additional Context

### Dependencies

- **No new assembly references.** All required types (`Button`, `TMP_Text`, `Image`, `HorizontalLayoutGroup`, `GameLog`, `EquipmentSystem`, `GoldSystem`, `UsableItemSO`, `EquipableItemSO`, `ItemSO`, `TradeSide`) are already in the `Game` assembly via `Assets/_Game/Game.asmdef`.
- **Prior tech-spec to mirror:** `_bmad-output/implementation-artifacts/tech-spec-inventory-trade-context-menu-separation.md` (status: implementation-complete) — same per-mode prefab pattern.
- **Possible `EquipmentSystem` API addition:** Task 2 may need a `public void Equip(ItemSO item)` overload alongside the existing `Equip(int slotIndex)`. Verify in the implementing pass; if absent, add it (one tiny method that walks the inventory by item identity — same logic the old `ItemDetailPanelUI.OnEquipClicked` used).

### Testing Strategy

Manual playtest only (no automated UI tests in this project). Run every row of the matrix; observe Unity console after each.

| # | Scenario | Expected |
|---|----------|----------|
| 1 | Open inventory → click a non-equipable, non-usable item | Detail shows; Drop visible; Use hidden; Equip hidden |
| 2 | Open inventory → click a UsableItemSO | Drop visible; Use visible; Equip hidden; Use button consumes item |
| 3 | Open inventory → click an EquipableItemSO not yet equipped | Drop visible; Use hidden; Equip visible labeled "Equip"; click equips |
| 4 | Equip the item → click its inventory slot | Drop hidden; Equip labeled "Unequip"; click unequips |
| 5 | Equip an item → single-click matching equipment slot | Only Equip/Unequip visible (no Drop, no Use); click toggles |
| 6 | Open trade → click NpcSide item, sufficient player gold | Buy only; price label correct; interactable; click buys, both grids refresh, gold updates |
| 7 | Open trade → click NpcSide item, insufficient player gold | Buy only; not interactable; click does nothing |
| 8 | Open trade → click PlayerSide item, NPC has gold | Sell only; price label correct; click sells |
| 9 | Open trade → click PlayerSide item, NPC has insufficient gold | Sell only; not interactable; click does nothing |
| 10 | Open trade → toggle NpcSide ↔ PlayerSide selection 5+ times rapidly, then click action | Exactly one action fires per click, targeting current selection only |
| 11 | Open trade → press Cancel/Escape | UI closes via `HandleCancel`; CursorManager re-locks |
| 12 | Open inventory → close inventory → reopen | No stale listener crash; all buttons still wire correctly |
| 13 | Inventory: equip-from-context-menu (right-click → Equip) | Existing context-menu flow still works (independent of detail-panel refactor) |
| 14 | Trade: right-click NpcSide / PlayerSide item | Existing TradeContextMenu flow still works (independent) |

### Notes

- **Pre-existing bug fixed as a side-effect:** today `NPCTradeUI.UpdateDetailPanel` calls `_detailPanelUI.Show(item)` (no-arg overload), and that overload still surfaces the Equip button via `ManageEquipButton`. So the current trade UI shows an "Equip" button on every selected trade item — meaningless in trade context. After this refactor that button no longer exists in the trade prefab tree at all.
- **Highest-risk path:** test row 10 (rapid side toggle). `Bind` re-binding must always `RemoveAllListeners()` first; both new components do this.
- **Listener cleanup on Hide:** intentional non-action — `Hide()` only sets `CanvasGroup.alpha = 0` and `blocksRaycasts = false`, so listeners stay attached but are pointer-blocked. The next `Bind` call replaces them. This matches the prior pattern in `ItemDetailPanelUI`.
- **`_actionsContainer` field NOT kept on `ItemDetailPanelUI`.** Decision in Step 2: the owner UI references the `InventoryDetailActions` / `TradeDetailActions` component directly; the panel script does not need a Transform handle. Less coupling, less prefab wiring.
- **Prefab GUID conventions:** `TradeContextMenu.prefab` uses a hand-edited placeholder GUID (`a1b2c3d4...`); the prior spec author appears to have committed it intentionally. New prefabs created in this task should let Unity generate normal GUIDs (no need to mimic the placeholder).
- **`m_AddedGameObjects` overrides:** Task 11 and 12 each add a child GameObject to a nested PrefabInstance (the ItemDetailPanel). Unity tracks this as an "Added GameObject" override on the parent prefab. This is supported and stable; same mechanism `UICanvas.prefab` uses to nest `DialoguePanel.prefab`.
- **Out-of-scope follow-up worth noting:** if a future feature needs different actions inside the trade UI (e.g. "Examine", "Compare to equipped"), this architecture extends naturally — add a button to `TradeDetailActions.prefab` + a SerializeField on `TradeDetailActions.cs` + a Bind branch. No `ItemDetailPanelUI` change required.
