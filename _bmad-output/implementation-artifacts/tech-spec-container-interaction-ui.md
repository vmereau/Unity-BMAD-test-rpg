---
title: 'Container Interaction UI'
slug: 'container-interaction-ui'
created: '2026-05-14'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
tech_stack: ['Unity 6', 'C#', 'URP', 'Unity UI (UGUI)', 'TextMeshPro', 'Unity Input System']
files_to_modify:
  - 'Assets/_Game/Scripts/World/InteractionSystem.cs'
  - 'Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs'
  - 'Assets/_Game/Prefabs/World/Containers/Base_Container.prefab'
files_to_create:
  - 'Assets/_Game/Scripts/World/ContainerInteractable.cs'
  - 'Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs'
  - 'Assets/_Game/Scripts/UI/Inventory/ContainerSideUI.cs'
  - 'Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs'
  - 'Assets/_Game/Prefabs/UI/Container/ContainerUI.prefab'
  - 'Assets/_Game/Prefabs/UI/Container/ContainerContextMenu.prefab'
code_patterns:
  - 'IItemSlotContainer event dispatch via GetComponentInParent'
  - 'Dual-panel HorizontalLayoutGroup (NPCTradeUI pattern)'
  - 'Context menu + blocker overlay (runtime instantiation)'
  - 'CursorManager.Unlock/Lock in OnScreenOpen/OnScreenClose'
  - '_input created in Awake; UI.Cancel subscribed in OnEnable'
test_patterns: []
---

# Tech-Spec: Container Interaction UI

**Created:** 2026-05-14

## Overview

### Problem Statement

World containers (chests, etc.) have no player-facing UI. `Base_Container.prefab` already has an `InventorySystem` component, but pressing Interact does nothing — players have no way to browse, take, or deposit items.

### Solution

Add a `ContainerInteractable` component (implementing `IInteractable`) to containers. Interacting opens a dual-panel `ContainerUI` (Container Inventory | Item Detail | Player Inventory) that mirrors the `NPCTradeUI` layout. Players can take/put items via double-click, right-click context menu, and drag-and-drop. Player state becomes `IsBusy` (cursor unlocked via `CursorManager`) while the UI is open, blocking all player actions.

### Scope

**In Scope:**
- `ContainerInteractable.cs` — implements `IInteractable`, opens/closes ContainerUI
- `ContainerSide` enum (`Container | Player`) — scoping enum for slot events
- `ContainerSideUI.cs` — implements `IItemSlotContainer`, forwards slot events to ContainerUI with side context
- `ContainerDetailActions.cs` — manages Take / Put action buttons in the item detail panel
- `ContainerUI.cs` — dual-panel controller (Open, Close, IsOpen, TakeItem, PutItem, slot event handlers)
- `ContainerUI.prefab` — 3-panel horizontal layout mirroring NPCTradeUI.prefab structure
- `Base_Container.prefab` update — add `ContainerInteractable` component, wire references
- `InteractionSystem.cs` update — replace `_dialogueSystem.IsOpen` gate with generic `!CursorManager.IsLocked` check so ALL open UIs block Interact (future-proof)
- Double-click: Container side → Take; Player side → Put
- Right-click context menu: Container side → "Take"; Player side → "Put"
- Drag-and-drop: Container→Player (take), Player→Container (put); may require `ItemSlotUI` cross-container drop extension

**Out of Scope:**
- Container lid open/close animations
- Item persistence across scene reloads (containers reset on load)
- Loot-All / Take-All button
- Range-based auto-close (player IsBusy gate already prevents action)
- Multiple container types beyond `Base_Container.prefab`

---

## Context for Development

### Codebase Patterns

**IsBusy / Cursor unlock pattern (how all menus work):**
- `PlayerStateManager.IsBusy` returns `!CursorManager.IsLocked`
- Opening any menu calls `CursorManager.Unlock()` → all `CanAttack/Move/Dodge` return false
- Closing calls `CursorManager.Lock()` → player regains control
- `ContainerUI.OnScreenOpen()` must call `CursorManager.Unlock()`; `ContainerUI.OnScreenClose()` must call `CursorManager.Lock()`

**NPCTradeUI dual-panel pattern (primary reference — confirmed by source read):**
- Root: `NPCTradeUI.cs` (controller) + `HorizontalLayoutGroup`
- Two `TradeSideUI` components (one per panel), each implementing `IItemSlotContainer`
- `TradeSideUI.Awake()` uses `GetComponentInParent<NPCTradeUI>()` to bind to owner
- `TradeSideUI` catches `ItemSlotUI` events (select, right-click, double-click) and forwards to `NPCTradeUI` with `TradeSide` enum for context
- Detail panel: `ItemDetailPanelUI` (display-only, call `Show(ItemSO)` / `Hide()`) + `TradeDetailActions` (Buy/Sell buttons, call `Bind(owner, idx, item, side, playerGold, npcGold)`)
- Context menu: runtime-instantiate prefab + blocker Image GO as children of Canvas. Blocker uses `AnyButtonClickListener.callback = (_) => HideContextMenu()`; menu is `SetAsLastSibling()`. Find buttons by name: `Transform.Find("TakeButton")` / `Transform.Find("PutButton")`
- Grid refresh: `foreach (Transform child in root) Destroy(child.gameObject)` then `Instantiate(_itemSlotPrefab, root)` per slot
- ESC close: `_input = new InputSystem_Actions()` in `Awake`; `_input.UI.Cancel.performed += HandleCancel` in `OnEnable`; unsubscribe in `OnDisable`; `_input.Dispose()` in `OnDestroy`

**IItemSlotContainer interface (full signature — confirmed by source read):**
```csharp
void SelectSlot(int slotIndex, IItemSlotContainer source);
void ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source);
void PrimaryAction(int slotIndex, IItemSlotContainer source);
void SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source);
```
`ItemSlotUI` calls these on its parent `IItemSlotContainer` — ContainerSideUI will implement this.

**CRITICAL: ItemSlotUI cross-container drag-drop bug (must fix):**
Current `ItemSlotUI.OnDrop()`:
```csharp
_container?.SwapSlots(source.SlotIndex, SlotIndex, _container); // WRONG: passes drop-target container as source
```
Fix (one line change):
```csharp
_container?.SwapSlots(source.SlotIndex, SlotIndex, source.GetComponentInParent<IItemSlotContainer>()); // passes actual source container
```
After fix, `ContainerSideUI.SwapSlots` can check `source != this` to detect cross-side drops and call `TakeItem/PutItem` on the owner.

**InteractionSystem gate (to fix):**
Current (line 143–144):
```csharp
private void LateUpdate()
{
    if (_dialogueSystem != null && _dialogueSystem.IsOpen) return;
    if (CurrentInteractable != null && _input.Player.Interact.WasPressedThisFrame())
        CurrentInteractable.Interact();
}
```
Fix: Replace with `if (!CursorManager.IsLocked) return;` — remove `_dialogueSystem` field + its null-check guard entirely.

**OnDisable null guard (NOT needed for ContainerUI):**
NPCTradeUI creates `_input` in `Awake` (not OnEnable) so no null guard is needed. ContainerUI follows the same pattern (Awake creates, OnEnable subscribes, OnDisable unsubscribes, OnDestroy disposes).

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` | Primary reference — dual-panel controller to mirror |
| `Assets/_Game/Scripts/UI/Inventory/TradeSideUI.cs` | Reference — slot event forwarding pattern to replicate as ContainerSideUI |
| `Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs` | Reference — detail action button management to replicate as ContainerDetailActions |
| `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs` | Reuse as-is for container/player slot rendering |
| `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs` | Reuse as-is for center detail panel |
| `Assets/_Game/Scripts/UI/Inventory/IItemSlotContainer.cs` | Interface ContainerSideUI must implement |
| `Assets/_Game/Scripts/UI/Inventory/AnyButtonClickListener.cs` | Reuse for context menu blocker |
| `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab` | Reference hierarchy for ContainerUI.prefab |
| `Assets/_Game/Scripts/Inventory/InventorySystem.cs` | Container's existing inventory model |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Interface ContainerInteractable must implement |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Gate fix target (replace dialogueSystem check) |
| `Assets/_Game/Scripts/Player/PlayerStateManager.cs` | IsBusy docs; no code changes needed |
| `Assets/_Game/Scripts/Core/CursorManager.cs` | Lock/Unlock calls for open/close |
| `Assets/_Game/Prefabs/World/Containers/Base_Container.prefab` | Target prefab for ContainerInteractable addition |

### Technical Decisions

1. **`ContainerSideUI` not reusing `TradeSideUI`** — `TradeSideUI` is tightly coupled to `NPCTradeUI` and the `TradeSide` enum. Creating a parallel `ContainerSideUI` keeps both systems independent and avoids adding a new generic abstraction.

2. **InteractionSystem gate generalized to `CursorManager.IsLocked`** — removes the `_dialogueSystem` field coupling, correctly blocks interaction for ALL open UIs (dialogue, inventory, trade, container), no future changes needed when new panels are added.

3. **Drag-drop cross-container** — `ItemSlotUI` drag-drop may only work within one `IItemSlotContainer`. If cross-container drops aren't handled, extend `ItemSlotUI` to check for an `IItemSlotContainer` on the drop target's parent hierarchy and call `PrimaryAction` on the destination side. Investigate during implementation.

4. **ContainerUI implements `IScreenPanel`** — registers with the same screen management system as `InventoryUI` and `NPCTradeUI` to ensure consistent open/close lifecycle management.

---

## Implementation Plan

### Tasks

Tasks are ordered by dependency — lowest-level first.

- [x] Task 1: Fix `ItemSlotUI.OnDrop` cross-container source parameter
  - File: `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs`
  - Action: In `OnDrop`, change the last argument of `SwapSlots` from `_container` to the actual source container:
    ```csharp
    // Before:
    _container?.SwapSlots(source.SlotIndex, SlotIndex, _container);
    // After:
    _container?.SwapSlots(source.SlotIndex, SlotIndex, source.GetComponentInParent<IItemSlotContainer>());
    ```
  - Notes: One-line change. This is the prerequisite for cross-container drag-drop. The `source` variable here is the dragged `ItemSlotUI` (already declared above). `GetComponentInParent` returns the source slot's container (ContainerSideUI), which ContainerSideUI.SwapSlots uses to detect cross-side drops.

- [x] Task 2: Fix `InteractionSystem` — generalize the "busy" gate
  - File: `Assets/_Game/Scripts/World/InteractionSystem.cs`
  - Action 1: Remove the `[SerializeField] private DialogueSystem _dialogueSystem;` field and any related import.
  - Action 2: In `LateUpdate`, replace:
    ```csharp
    if (_dialogueSystem != null && _dialogueSystem.IsOpen) return;
    ```
    with:
    ```csharp
    if (!CursorManager.IsLocked) return;
    ```
  - Action 3: Add `using Game.Core;` at the top if not already present.
  - Notes: This makes any cursor-unlocked UI (dialogue, inventory, trade, container) block interaction. No new field, no future changes needed.

- [x] Task 3: Create `ContainerSideUI.cs`
  - File: `Assets/_Game/Scripts/UI/Inventory/ContainerSideUI.cs`
  - Action: Create the file with namespace `Game.UI`. Declare the `ContainerSide` enum in this file:
    ```csharp
    public enum ContainerSide { Container, Player }
    ```
    Then implement `ContainerSideUI : MonoBehaviour, IItemSlotContainer`:
    - `[SerializeField] private ContainerSide _side;`
    - `private ContainerUI _owner;`
    - `Awake()`: `_owner = GetComponentInParent<ContainerUI>();`
    - `SelectSlot(int slotIndex, IItemSlotContainer source)` → `_owner.OnSlotSelected(slotIndex, _side)`
    - `ShowContextMenu(int slotIndex, Vector2 screenPos, IItemSlotContainer source)` → `_owner.OnSlotRightClicked(slotIndex, screenPos, _side)`
    - `PrimaryAction(int slotIndex, IItemSlotContainer source)` → `_owner.OnSlotDoubleClicked(slotIndex, _side)`
    - `SwapSlots(int fromIndex, int toIndex, IItemSlotContainer source)`:
      - If `source == this`: return (same-side reorder not supported)
      - If `_side == ContainerSide.Player`: player side received a drop from container side → `_owner.TakeItem(fromIndex)`
      - If `_side == ContainerSide.Container`: container side received a drop from player side → `_owner.PutItem(fromIndex)`
  - Notes: `source` is the SOURCE container (after the Task 1 fix). Cross-side is identified by `source != this`.

- [x] Task 4: Create `ContainerDetailActions.cs`
  - File: `Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs`
  - Action: Create with namespace `Game.UI`. Implement `ContainerDetailActions : MonoBehaviour`:
    - Fields: `[SerializeField] private Button _takeButton;`, `[SerializeField] private Button _putButton;`
    - `public void Bind(ContainerUI owner, int slotIndex, ItemSO item, ContainerSide side)`:
      - Guard: `if (item == null || owner == null)` → log warn + return
      - Remove all listeners from both buttons
      - If `side == ContainerSide.Container`: `_takeButton.gameObject.SetActive(true)`, wire `onClick → owner.TakeItem(slotIndex)`, `_putButton.gameObject.SetActive(false)`
      - If `side == ContainerSide.Player`: `_putButton.gameObject.SetActive(true)`, wire `onClick → owner.PutItem(slotIndex)`, `_takeButton.gameObject.SetActive(false)`
  - Notes: Mirror of `TradeDetailActions`. No gold/value display needed.

- [x] Task 5: Create `ContainerUI.cs`
  - File: `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs`
  - Action: Create with namespace `Game.UI`, `using Game.Core; using Game.Inventory; using Game.World; using UnityEngine; using UnityEngine.UI; using TMPro;`. Implement `ContainerUI : MonoBehaviour, IScreenPanel`:
  
  **Fields:**
  ```csharp
  private const string TAG = "[ContainerUI]";
  [SerializeField] private InventorySystem _playerInventory;
  private InventorySystem _containerInventory;
  [SerializeField] private Transform _containerContentRoot;
  [SerializeField] private Transform _playerContentRoot;
  [SerializeField] private GameObject _itemSlotPrefab;
  [SerializeField] private ItemDetailPanelUI _detailPanelUI;
  [SerializeField] private ContainerDetailActions _containerActions;
  [SerializeField] private GameObject _contextMenuPrefab;
  [SerializeField] private Canvas _canvas;
  private GameObject _activeContextMenu;
  private GameObject _contextMenuBlocker;
  private int _contextMenuSlotIndex = -1;
  private ItemSlotUI _selectedSlotUI;
  private ContainerSide _selectedSide;
  private InputSystem_Actions _input;
  public bool IsOpen { get; private set; }
  ```
  
  **Lifecycle:**
  - `Awake()`: `_input = new InputSystem_Actions();`
  - `OnEnable()`: `if (_input == null) return; _input.UI.Enable(); _input.UI.Cancel.performed += HandleCancel;`
  - `OnDisable()`: `if (_input == null) return; _input.UI.Cancel.performed -= HandleCancel; _input.UI.Disable();`
  - `OnDestroy()`: `_input?.Dispose();`
  - `HandleCancel(CallbackContext ctx)`: `if (gameObject.activeInHierarchy) Close();`
  
  **Open/Close:**
  - `public void Open(InventorySystem containerInventory)`: set `_containerInventory = containerInventory`, `gameObject.SetActive(true)`, `OnScreenOpen()`
  - `public void Close()`: `OnScreenClose(); gameObject.SetActive(false);`
  - `public void OnScreenOpen()`: `RefreshGrids(); _detailPanelUI?.Hide(); CursorManager.Unlock(); IsOpen = true; GameLog.Info(TAG, "Container UI opened");`
  - `public void OnScreenClose()`: `HideContextMenu(); ClearSelection(); CursorManager.Lock(); IsOpen = false; GameLog.Info(TAG, "Container UI closed");`
  
  **Slot event handlers:**
  - `public void OnSlotSelected(int index, ContainerSide side)`:
    - Guard: get correct inventory (`_containerInventory` or `_playerInventory`), validate index
    - Deselect `_selectedSlotUI`; get new slot from correct content root; select it
    - `_selectedSide = side; UpdateDetailPanel(inv.Items[index].Item, index, side);`
  - `public void OnSlotRightClicked(int index, Vector2 pos, ContainerSide side)`: → `ShowContextMenu(index, pos, side)`
  - `public void OnSlotDoubleClicked(int index, ContainerSide side)`: if `Container` → `TakeItem(index)`; if `Player` → `PutItem(index)`
  
  **Transfer actions:**
  - `public void TakeItem(int index)`:
    - Guard: `_containerInventory == null || index < 0 || index >= _containerInventory.Count` → return
    - `ItemSO item = _containerInventory.Items[index].Item;`
    - `_containerInventory.DecrementStack(index);`
    - `_playerInventory.AddItem(item);`
    - `RefreshGrids(); RefreshDetailPanelAfterAction(item, index, ContainerSide.Container);`
    - `GameLog.Info(TAG, $"Took {item.itemName} from container");`
  - `public void PutItem(int index)`:
    - Guard: `_playerInventory == null || index < 0 || index >= _playerInventory.Count` → return
    - `ItemSO item = _playerInventory.Items[index].Item;`
    - `_playerInventory.DecrementStack(index);`
    - `_containerInventory.AddItem(item);`
    - `RefreshGrids(); RefreshDetailPanelAfterAction(item, index, ContainerSide.Player);`
    - `GameLog.Info(TAG, $"Put {item.itemName} into container");`
  
  **Context menu (mirror NPCTradeUI.ShowContextMenu exactly):**
  - `private void ShowContextMenu(int slotIndex, Vector2 screenPos, ContainerSide side)`:
    - `HideContextMenu(); _contextMenuSlotIndex = slotIndex;`
    - Get correct inventory; guard index
    - Create blocker GO (Image, clear, raycastTarget=true, full-screen anchors, `AnyButtonClickListener.callback = (_) => HideContextMenu()`)
    - Instantiate `_contextMenuPrefab` as child of `_canvas.transform`, `SetAsLastSibling()`
    - Position + clamp to screen bounds (copy InventoryUI clamp logic)
    - If `side == ContainerSide.Container`: find "TakeButton", wire `onClick → TakeItem(_contextMenuSlotIndex); HideContextMenu()`, hide "PutButton"
    - If `side == ContainerSide.Player`: find "PutButton", wire `onClick → PutItem(_contextMenuSlotIndex); HideContextMenu()`, hide "TakeButton"
    - Log null warning if button not found (match NPCTradeUI pattern)
  - `private void HideContextMenu()`: destroy `_activeContextMenu` and `_contextMenuBlocker`, reset index to -1
  
  **Grid helpers:**
  - `private void RefreshGrids()`: `_selectedSlotUI = null; RefreshGrid(_containerContentRoot, _containerInventory); RefreshGrid(_playerContentRoot, _playerInventory);`
  - `private void RefreshGrid(Transform root, InventorySystem inv)`: destroy children, instantiate `_itemSlotPrefab` per slot (mirror NPCTradeUI.RefreshGrid exactly)
  - `private void ClearSelection()`: deselect, null references, `_detailPanelUI?.Hide()`
  - `private void UpdateDetailPanel(ItemSO item, int slotIndex, ContainerSide side)`: `_detailPanelUI.Show(item); _containerActions?.Bind(this, slotIndex, item, side);`
  - `private void RefreshDetailPanelAfterAction(ItemSO item, int index, ContainerSide side)`: re-select if slot still valid + item unchanged, else `ClearSelection()` (mirror NPCTradeUI.RefreshDetailPanelAfterTrade)

- [x] Task 6: Create `ContainerInteractable.cs`
  - File: `Assets/_Game/Scripts/World/ContainerInteractable.cs`
  - Action: Create with namespace `Game.World`, `using Game.Inventory; using Game.UI; using Game.Core; using UnityEngine;`. Implement `ContainerInteractable : MonoBehaviour, IInteractable`:
    - `[SerializeField] private string _interactPrompt = "Open Container";`
    - `[SerializeField] private string _nameTag = "Chest";`
    - `private InventorySystem _inventory;`
    - `private ContainerUI _containerUI;`
    - `public string InteractPrompt => _interactPrompt;`
    - `public string NameTag => _nameTag;`
    - `Awake()`:
      - `_inventory = GetComponent<InventorySystem>(); if (_inventory == null) { GameLog.Error(TAG, "InventorySystem not found — ContainerInteractable disabled"); enabled = false; return; }`
      - `_containerUI = FindFirstObjectByType<ContainerUI>(); if (_containerUI == null) { GameLog.Error(TAG, "ContainerUI not found in scene — ContainerInteractable disabled"); enabled = false; return; }`
    - `public void Interact()`: `if (_containerUI == null) return; _containerUI.Open(_inventory);`
  - Notes: `FindFirstObjectByType<ContainerUI>()` is called once in Awake and cached. Assumes one shared ContainerUI per scene.

- [x] Task 7: Create `ContainerContextMenu.prefab`
  - Location: `Assets/_Game/Prefabs/UI/Container/ContainerContextMenu.prefab` (create the `Container/` subfolder)
  - Action: Create prefab hierarchy:
    - Root: `ContainerContextMenu` (RectTransform, VerticalLayoutGroup, ContentSizeFitter)
      - Child: `TakeButton` (Button + TMP_Text child "Take")
      - Child: `PutButton` (Button + TMP_Text child "Put")
  - Notes: Match style/sizing of the existing `TradeContextMenu.prefab` (check `Assets/_Game/Prefabs/UI/Trade/` for reference). Both buttons visible in prefab — ContainerUI hides the irrelevant one at runtime.

- [x] Task 8: Create `ContainerUI.prefab`
  - Location: `Assets/_Game/Prefabs/UI/Container/ContainerUI.prefab`
  - Action: Create prefab hierarchy (starts **inactive** — `gameObject.SetActive(false)` in prefab):
    - Root: `ContainerUI` (RectTransform, HorizontalLayoutGroup 30px padding + spacing, LayoutElement fill)
      - Component: `ContainerUI.cs`
      - Child: `ContainerSide` (RectTransform, VerticalLayoutGroup, LayoutElement flex 1)
        - Component: `ContainerSideUI.cs` (`_side = Container`)
        - Child: `Header` (TMP_Text, text: "Container")
        - Child: `ContentRoot` (RectTransform — assign to `ContainerUI._containerContentRoot`)
      - Child: `ItemDetailContainer` (RectTransform, LayoutElement flex 1)
        - Child: `ItemDetailPanel` (instance of `Assets/_Game/Prefabs/UI/ItemDetailPanel.prefab`)
          - Under it: `ContainerDetailActions` GO with `ContainerDetailActions.cs` + wired Take/Put buttons
      - Child: `PlayerSide` (RectTransform, VerticalLayoutGroup, LayoutElement flex 1)
        - Component: `ContainerSideUI.cs` (`_side = Player`)
        - Child: `Header` (TMP_Text, text: "Inventory")
        - Child: `ContentRoot` (RectTransform — assign to `ContainerUI._playerContentRoot`)
  - Inspector wiring on `ContainerUI.cs`: `_containerContentRoot`, `_playerContentRoot`, `_itemSlotPrefab` (drag `ItemSlot.prefab`), `_detailPanelUI`, `_containerActions`, `_contextMenuPrefab` (drag `ContainerContextMenu.prefab`)
  - Notes: `_playerInventory` and `_canvas` must be wired in the scene instance (not in the prefab) since they reference scene objects.

- [x] Task 9: Update `Base_Container.prefab`
  - File: `Assets/_Game/Prefabs/World/Containers/Base_Container.prefab`
  - Action: Add `ContainerInteractable.cs` component to the root GO.
  - Set `_interactPrompt = "Open Container"`, `_nameTag = "Chest"` in the Inspector.
  - Notes: `_inventory` and `_containerUI` are found at runtime via `GetComponent` and `FindFirstObjectByType` in Awake — no additional inspector wiring needed.
  - Verify the root GO (or a child collider) is on the **Interactable layer (Layer 8)** so `InteractionSystem` SphereCast detects it — already confirmed from prefab inspection.

- [x] Task 10: Scene setup in `StartingTown.unity`
  - File: `Assets/_Game/Scenes/StartingTown.unity`
  - Action 1: Instantiate `ContainerUI.prefab` as a child of the scene's main Canvas (same level as NPCTradeUI).
  - Action 2: Wire scene-specific refs on the `ContainerUI` instance:
    - `_playerInventory`: drag the Player GO's `InventorySystem` component
    - `_canvas`: drag the root Canvas
  - Action 3: Confirm any existing `Base_Container` prefab instances in the scene are on the Interactable layer and have a Collider (check Visual child or root — add BoxCollider if missing).
  - Notes: After this task, interacting with any chest in the scene will open the shared ContainerUI instance.

---

### Acceptance Criteria

- [x] AC 1 — Happy path (Take via double-click): Given the player is facing a container and presses Interact, when ContainerUI opens and the player double-clicks an item on the Container Side, then the item is removed from the container inventory and added to the player inventory, and both grids refresh to reflect the change.

- [x] AC 2 — Happy path (Put via double-click): Given ContainerUI is open, when the player double-clicks an item on the Player Side, then the item is removed from the player inventory and added to the container inventory, and both grids refresh.

- [x] AC 3 — Right-click Take: Given ContainerUI is open, when the player right-clicks an item on the Container Side, then a context menu appears with a "Take" button (no "Put" button). Clicking "Take" moves the item to player inventory and closes the context menu.

- [x] AC 4 — Right-click Put: Given ContainerUI is open, when the player right-clicks an item on the Player Side, then a context menu appears with a "Put" button (no "Take" button). Clicking "Put" moves the item to the container and closes the context menu.

- [x] AC 5 — Drag Container→Player: Given ContainerUI is open, when the player drags an item from the Container Side and drops it onto any slot on the Player Side, then the item moves from the container to the player inventory and both grids refresh.

- [x] AC 6 — Drag Player→Container: Given ContainerUI is open, when the player drags an item from the Player Side and drops it onto any slot on the Container Side, then the item moves from the player to the container inventory and both grids refresh.

- [x] AC 7 — ESC close: Given ContainerUI is open, when the player presses Escape, then ContainerUI closes (becomes inactive), the cursor is locked, and `PlayerStateManager.IsBusy` returns false.

- [x] AC 8 — IsBusy gate: Given ContainerUI is open, when the player attempts to attack, move, or dodge, then no action executes (`IsBusy = true` via `!CursorManager.IsLocked`).

- [x] AC 9 — Interact blocked while open: Given ContainerUI is open and another IInteractable is in range, when the player presses the Interact key, then no new `Interact()` call is made (`InteractionSystem.LateUpdate` returns early on `!CursorManager.IsLocked`).

- [x] AC 10 — Detail panel + action button: Given ContainerUI is open, when the player left-clicks an item on either side, then `ItemDetailPanelUI` shows the item's icon, name, and description, and `ContainerDetailActions` shows only the context-appropriate button (Take for Container Side, Put for Player Side).

- [x] AC 11 — Context menu dismissal: Given a context menu is open, when the player clicks anywhere outside the menu (on the blocker overlay), then the context menu closes without transferring any item.

- [x] AC 12 — Empty slot right-click: Given ContainerUI is open and a slot has no item (`Item == null`), when the player right-clicks that slot, then no context menu appears (ItemSlotUI already guards this: `if (Item == null) return;`).

- [x] AC 13 — InteractionSystem regression: Given the player is in gameplay (cursor locked), when the player presses Interact on any existing IInteractable (NPC, item pickup), then the interaction still triggers correctly (confirming the gate change doesn't break existing behavior).

---

## Additional Context

### Dependencies

- `InventorySystem.cs` on `Base_Container.prefab` — already in place, no changes
- `ItemSlot.prefab` — must be assigned to `ContainerUI._itemSlotPrefab` in scene inspector
- `ItemDetailPanel.prefab` — nested as child of ContainerUI.prefab's ItemDetailContainer
- `CursorManager` — static class, no scene wiring needed
- `IScreenPanel` interface — already in the project (implemented by NPCTradeUI and InventoryUI)
- `AnyButtonClickListener.cs` — reused as-is for context menu blocker

### Testing Strategy

Manual playtest steps (no automated tests exist for UI in this project):

1. **Open container**: Walk up to a chest → crosshair highlights → press E → ContainerUI opens, cursor visible, player cannot move.
2. **Take item (double-click)**: Double-click item in Container Side → item appears in Player Side → container slot vacated.
3. **Put item (double-click)**: Double-click item in Player Side → item appears in Container Side → player slot vacated.
4. **Take item (right-click)**: Right-click container item → context menu shows "Take" only → click Take → item transferred.
5. **Put item (right-click)**: Right-click player item → context menu shows "Put" only → click Put → item transferred.
6. **Drag Container→Player**: Drag item from Container Side, drop on Player Side → item transferred.
7. **Drag Player→Container**: Drag item from Player Side, drop on Container Side → item transferred.
8. **ESC close**: Press Escape while ContainerUI open → UI closes, player can move again.
9. **Interact blocked**: Open ContainerUI, try pressing E on another interactable → nothing happens.
10. **Detail panel**: Click any item → detail panel updates; action button is Take or Put depending on side.
11. **Blocker click**: Right-click to open context menu → click outside it → menu closes, nothing transferred.
12. **Regression — NPC trade**: Open NPC trade, buy/sell items → still works normally.
13. **Regression — Inventory**: Open Inventory (I key) → equip/use items → still works normally.

### Notes

- **High risk: `FindFirstObjectByType<ContainerUI>()`** — if ContainerUI prefab is not placed in the scene, all containers silently disable themselves in Awake. Verify scene setup in Task 10 before testing.
- **High risk: ItemSlotUI.OnDrop change (Task 1)** — this is a shared component used by InventoryUI and NPCTradeUI. After the fix, run regression tests (AC 13) to confirm existing drag-drop within InventoryUI and within NPCTradeUI sides still works. Since `InventoryUI.SwapSlots` uses `source == _container` equivalence before the fix was implicit — confirm TradeSideUI.SwapSlots no-op still fires correctly (it ignores both params).
- **Known limitation**: Only one ContainerUI instance per scene. If two players (multiplayer) or simultaneous container openings are ever needed, this architecture must change. Out of scope for this spec.
- **Future consideration**: `Base_Container.prefab` is currently a generic chest. Future container types (locked chest, crate, barrel) can override `_interactPrompt` and `_nameTag` via prefab variants or child overrides.
- **Collider check (Task 10)**: The `Base_Container.prefab` Visual child renders `Treasure_Chest_01` but it's unclear if that FBX has a collider. If the InteractionSystem SphereCast doesn't detect the container, add a `BoxCollider` to the Base_Container root on Layer 8.
