# Story 4.11: Quick Action Bar

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want a quick action bar with 6 slots at the bottom of the screen where I can assign UsableItems from my inventory, trigger them with keys 1–6, and manage assignments via drag-and-drop,
so that I can use consumables during combat without interrupting the action to open the inventory panel.

## Acceptance Criteria

1. **`InputSystem_Actions.inputactions`** + **`InputSystem_Actions.cs`** updated — 6 new `ActionBar1`–`ActionBar6` `<Button>` actions added to the `Player` action map, bound to keyboard keys `1`–`6`. The existing `Previous` (Key 1) and `Next` (Key 2) keyboard bindings are removed to avoid input conflicts (their Gamepad D-Pad bindings may remain).

2. **`ActionBarSystem.cs`** created at `Assets/_Game/Scripts/Inventory/ActionBarSystem.cs` in namespace `Game.Inventory`:
   - `private const string TAG = "[Inventory]";`
   - Internal struct `ActionBarSlotData { public int InventoryIndex; public ItemSO Item; }` (not `readonly struct` — mutable by design)
   - `private readonly ActionBarSlotData?[] _slots = new ActionBarSlotData?[6];` — null means unassigned
   - `[SerializeField] private InventorySystem _inventorySystem;`
   - `[SerializeField] private Transform _playerTransform;`
   - Private `InputSystem_Actions _input` — instantiated in `Awake`
   - `Awake()` — null-guard `_inventorySystem` and `_playerTransform`; log error and disable if missing
   - `OnEnable()` — subscribe `ActionBar1.performed`…`ActionBar6.performed` to `HandleHotkey0`…`HandleHotkey5` (or single dispatcher with captured index)
   - `OnDisable()` — unsubscribe all; guard `if (_input == null) return`
   - `OnDestroy()` — `_input?.Dispose()`
   - `public void Assign(int slotIndex, int inventoryIndex, ItemSO item)` — bounds-check slotIndex; set `_slots[slotIndex]`; log info
   - `public void ClearSlot(int slotIndex)` — bounds-check; set `_slots[slotIndex] = null`; log info
   - `public ActionBarSlotData? GetSlot(int slotIndex)` — returns `_slots[slotIndex]`
   - `public void ValidateSlots()` — for each assigned slot, search inventory by item reference (not by stored index); if item not found → clear slot; if found at a different index → update stored index. This correctly handles index shifts caused by stack removal (e.g. item at index 0 consumed shifts all subsequent items down). Log warn only when clearing.
   - Private `HandleHotkeyPressed(int slotIndex)`:
     - If slot is null → `GameLog.Warn(TAG, $"Action bar slot {slotIndex + 1} is empty")` return
     - Get `ItemSO item = _slots[slotIndex].Value.Item`
     - If `item is not UsableItemSO usable` → `GameLog.Warn(TAG, $"Slot {slotIndex + 1} item is not usable")` return
     - `bool used = usable.OnUse(_playerTransform.gameObject)`
     - If `used && usable.consumable` → `_inventorySystem.DecrementStack(_slots[slotIndex].Value.InventoryIndex)` then `ValidateSlots()`
     - Raise `OnActionBarUsed` event (see AC 3)

3. **`GameEventSO<int>` event asset** `OnActionBarUsed.asset` at `Assets/_Game/Data/Events/OnActionBarUsed.asset` — payload is the slot index (0-based). `ActionBarSystem` raises it after every hotkey use (successful or not) so `ActionBarUI` can `Refresh()`. (Alternative: `ActionBarUI` calls `Refresh()` directly after each hotkey if both are on the same GameObject — acceptable for prototype; use direct call pattern over event if on same prefab.)

4. **`ActionBarUI.cs`** created at `Assets/_Game/Scripts/UI/ActionBarUI.cs` in namespace `Game.UI`:
   - `private const string TAG = "[Inventory]";`
   - `[SerializeField] private ActionBarSystem _actionBarSystem;`
   - `[SerializeField] private ActionBarSlotUI[] _slotUIs;` — 6 elements, assigned in Inspector
   - `[SerializeField] private InventorySystem _inventorySystem;` — needed for `ValidateSlots()` trigger
   - `Awake()` — null-guard `_actionBarSystem` and `_slotUIs`; iterate `_slotUIs` to call `slot.Initialize(i, this)`
   - **Always visible** — the action bar is a persistent HUD element, never toggled. No `SetActive` logic needed.
   - `public void Refresh()` — calls `_actionBarSystem.ValidateSlots()` then for each slot `i`: `var data = _actionBarSystem.GetSlot(i)` → if null call `_slotUIs[i].BindEmpty(i)`, else `_slotUIs[i].Bind(i, data.Value.Item, data.Value.InventoryIndex, GetStackCount(data.Value.InventoryIndex, data.Value.Item))`
   - `private int GetStackCount(int invIndex, ItemSO item)` — bounds-check; return `_inventorySystem.Items[invIndex].Count` if valid and item matches; else 0
   - `public void OnInventorySlotDroppedOnActionBar(int inventorySlotIndex, ItemSO item, int actionBarSlotIndex)` — calls `_actionBarSystem.Assign(actionBarSlotIndex, inventorySlotIndex, item)` then `Refresh()`
   - `public void OnActionBarSlotSwap(int fromSlot, int toSlot)` — swaps `_actionBarSystem` slot data manually (ClearSlot both, re-Assign swapped values) then `Refresh()`
   - `public void OnActionBarSlotClearedByDragToInventory(int actionBarSlotIndex)` — calls `_actionBarSystem.ClearSlot(actionBarSlotIndex)` then `Refresh()`

5. **`ActionBarSlotUI.cs`** created at `Assets/_Game/Scripts/UI/ActionBarSlotUI.cs` in namespace `Game.UI`:
   - `private const string TAG = "[Inventory]";`
   - Implements: `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`, `IPointerEnterHandler`, `IPointerExitHandler`
   - `[SerializeField] private Image _iconImage;`
   - `[SerializeField] private Image _backgroundImage;`
   - `[SerializeField] private TMP_Text _stackCountText;`
   - `[SerializeField] private TMP_Text _keyLabelText;` — shows "1"–"6", set from `Initialize()`
   - `[SerializeField] private Color _normalColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);`
   - `[SerializeField] private Color _hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);`
   - `public int SlotIndex { get; private set; }`
   - `public ItemSO Item { get; private set; }`
   - `public int InventoryIndex { get; private set; } = -1;`
   - Private `ActionBarUI _actionBarUI;`
   - Private `Canvas _canvas;` — cached in `Awake` via `GetComponentInParent<Canvas>()`
   - Private `GameObject _ghostImage;`
   - `public void Initialize(int slotIndex, ActionBarUI actionBarUI)` — sets `SlotIndex`, `_actionBarUI`, updates `_keyLabelText.text = (slotIndex + 1).ToString()`
   - `public void Bind(int slotIndex, ItemSO item, int inventoryIndex, int stackCount)`:
     - Sets `Item = item`, `InventoryIndex = inventoryIndex`
     - Sets `_iconImage.sprite = item.icon`, `_iconImage.color = item.icon != null ? Color.white : Color.gray`
     - `_stackCountText.text = stackCount.ToString(); _stackCountText.gameObject.SetActive(stackCount > 1);`
   - `public void BindEmpty(int slotIndex)`:
     - Sets `Item = null`, `InventoryIndex = -1`
     - Clears icon and badge: `_iconImage.sprite = null; _iconImage.color = Color.clear; _stackCountText.gameObject.SetActive(false);`
   - `OnPointerEnter` / `OnPointerExit` — change `_backgroundImage.color` to hover/normal
   - **Drag out** (`OnBeginDrag` / `OnDrag` / `OnEndDrag`): same ghost-image pattern as `ItemSlotUI`
     - `OnBeginDrag`: if `Item == null` → ignore (`eventData.pointerDrag = null` to cancel). Else create ghost parented to root Canvas (`SetAsLastSibling`), `raycastTarget = false`, 64×64
     - `OnDrag`: move ghost to `eventData.position`
     - `OnEndDrag`: destroy ghost (null-guard)
   - Private `bool _dropHandled` — tracks whether the current drag was accepted by a valid target
   - `public void NotifyDropHandled()` — sets `_dropHandled = true`; called by drop targets before handling the drop
   - **Drag out** (`OnBeginDrag`): sets `_dropHandled = false` before creating ghost
   - **Drag end** (`OnEndDrag`): destroys ghost; if `!_dropHandled && Item != null` → calls `_actionBarUI.OnActionBarSlotClearedByDragToInventory(SlotIndex)` (covers drops on any non-target UI area)
   - **Drop onto action bar slot** (`OnDrop`):
     - Check `eventData.pointerDrag.GetComponent<ItemSlotUI>()` → if non-null: inventory→action-bar assign
       - `_actionBarUI.OnInventorySlotDroppedOnActionBar(source.SlotIndex, source.Item, SlotIndex)`
     - Check `eventData.pointerDrag.GetComponent<ActionBarSlotUI>()` → if non-null and source ≠ this: action-bar→action-bar swap
       - `source.NotifyDropHandled()` then `_actionBarUI.OnActionBarSlotSwap(source.SlotIndex, SlotIndex)`

6. **`ItemSlotUI.cs`** — extend `OnDrop` to handle action-bar-to-inventory drags:
   - In `ItemSlotUI.OnDrop`, add check for `ActionBarSlotUI` source before the existing `ItemSlotUI` check:
     - `abSource.NotifyDropHandled()` — prevents `OnEndDrag` from double-clearing
     - `abSource.RemoveGhostImage()`
     - Get `ActionBarUI` via `abSource.GetComponentInParent<ActionBarUI>()`
     - Call `actionBarUI?.OnActionBarSlotClearedByDragToInventory(abSource.SlotIndex)` and return

7. **`InventoryUI.cs`** — extended with action bar integration:
   - `[SerializeField] private ActionBarUI _actionBarUI;` — wired in scene Inspector; `RefreshSlots()` calls `_actionBarUI?.Refresh()` (with `refreshActionBar` param to avoid re-entrant refresh from `HandleActionBarUsed`)
   - `[SerializeField] private ActionBarSystem _actionBarSystem;` — wired in scene Inspector
   - `Open()` subscribes `_actionBarSystem.OnActionBarUsed += HandleActionBarUsed`
   - `Close()` unsubscribes — keeps subscription scoped to when panel is visible
   - `private void HandleActionBarUsed(int _) => RefreshSlots(false)` — refreshes inventory slots when an item is consumed via hotkey while panel is open; `false` prevents redundant `_actionBarUI.Refresh()` (already triggered by `ActionBarUI` directly)

7. **`ActionBar.prefab`** at `Assets/_Game/Prefabs/UI/ActionBar/ActionBar.prefab`:
   - Root GameObject `"ActionBar"` with `ActionBarUI` + `HorizontalLayoutGroup` (spacing 4px, child force expand width = false)
   - `RectTransform`: anchored bottom-center, pivot (0.5, 0), position (0, 20) — sits at bottom of screen
   - 6 `ActionBarSlot.prefab` children — assigned into `ActionBarUI._slotUIs[0..5]`

8. **`ActionBarSlot.prefab`** at `Assets/_Game/Prefabs/UI/ActionBar/ActionBarSlot.prefab`:
   - Root: 64×64 Image (`_backgroundImage`, dark gray)
   - Children:
     - `"Icon"` — Image 52×52, center anchor (`_iconImage`)
     - `"StackCountText"` — TMP 28×18, upper-left anchor, size 12, bold, white, inactive by default (`_stackCountText`)
     - `"KeyLabel"` — TMP 20×16, lower-right anchor, size 10, white, `_keyLabelText` (text "1"–"6" set via `Initialize()`)
   - `ActionBarSlotUI` component with all fields wired

9. **Edit Mode tests** in `Assets/Tests/EditMode/ActionBarSystemTests.cs`:
   - `Assign_StoresSlotReference` — assign slot 0, `GetSlot(0)` returns correct item + inventoryIndex
   - `ClearSlot_RemovesReference` — assign then clear; `GetSlot(0)` returns null
   - `ValidateSlots_ClearsStaleSlot_WhenInventoryIndexOutOfRange` — assign at index 3 on empty inventory → ValidateSlots clears it
   - `ValidateSlots_ClearsStaleSlot_WhenItemMismatch` — assign item A at index 0, but inventory[0] is item B → ValidateSlots clears it
   - `ClearSlot_OutOfRange_DoesNotThrow` — `ClearSlot(-1)` and `ClearSlot(6)` log warn, no exception
   - `Assign_OutOfRange_DoesNotThrow` — `Assign(-1, 0, null)` logs warn, no exception

10. **Play Mode validation**:
    - Open inventory, drag health potion to action bar slot 1 → icon and badge visible; potion still in inventory
    - Press key 1 → health restored, stack count decrements (badge updates or slot clears if last)
    - Press key for empty slot → no crash, warn logged
    - Drag action bar slot 1 to action bar slot 3 → assignments swap
    - Drag action bar slot back onto inventory panel → action bar slot clears, potion still in inventory
    - Consume last potion via inventory context menu "Use" → action bar slot auto-clears on next hotkey press or refresh
    - Open and close inventory panel → action bar remains visible throughout
    - All Edit Mode tests pass — no regressions from stories 4.9/4.10

## Tasks / Subtasks

- [x] Task 1: Update InputSystem_Actions (AC: 1)
  - [x] 1.1 Edit `InputSystem_Actions.inputactions` — add 6 new Button actions `ActionBar1`–`ActionBar6` bound to `<Keyboard>/1`–`<Keyboard>/6` in the Player action map
  - [x] 1.2 Remove Keyboard 1 binding from `Previous`; remove Keyboard 2 binding from `Next` (keep Gamepad D-Pad bindings)
  - [x] 1.3 Edit `InputSystem_Actions.cs` embedded JSON — mirror exact same changes (double-escaped quotes `""`)
  - [x] 1.4 Verified — compilation clean, no ArgumentException

- [x] Task 2: Create `ActionBarSystem.cs` (AC: 2, 3)
  - [x] 2.1 Create `Assets/_Game/Scripts/Inventory/ActionBarSystem.cs` with `ActionBarSlotData?[]` internal struct array
  - [x] 2.2 Implement `Awake()` with null-guards for serialized refs
  - [x] 2.3 Implement `OnEnable/OnDisable` hotkey subscriptions (subscribe 6 lambda callbacks with captured index)
  - [x] 2.4 Implement `Assign()`, `ClearSlot()`, `GetSlot()`
  - [x] 2.5 Implement `ValidateSlots()` — cross-check all assigned slots against current InventorySystem state
  - [x] 2.6 Implement `HandleHotkeyPressed(int slotIndex)` — execute UsableItem and decrement if consumable
  - [x] 2.7 Verified — 175/175 tests pass, no compilation errors

- [x] Task 3: Create `ActionBarUI.cs` (AC: 4)
  - [x] 3.1 Create `Assets/_Game/Scripts/UI/ActionBarUI.cs`
  - [x] 3.2 Implement `Awake()` — initialize each `ActionBarSlotUI` via `Initialize(i, this)`
  - [x] 3.3 Implement `Refresh()` with `ValidateSlots()` + per-slot `Bind/BindEmpty` calls
  - [x] 3.4 Implement `OnInventorySlotDroppedOnActionBar()`, `OnActionBarSlotSwap()`, `OnActionBarSlotClearedByDragToInventory()`
  - [x] 3.5 Implement `GetStackCount()` with bounds-check
  - [x] 3.6 Verified — compilation clean

- [x] Task 4: Create `ActionBarSlotUI.cs` (AC: 5)
  - [x] 4.1 Create `Assets/_Game/Scripts/UI/ActionBarSlotUI.cs` with all interface implementations
  - [x] 4.2 Implement `Initialize()` — set slot index, update key label text
  - [x] 4.3 Implement `Bind()` and `BindEmpty()` — set icon, badge, clear state
  - [x] 4.4 Implement drag: `OnBeginDrag` (ghost image, cancel if empty slot), `OnDrag` (move ghost), `OnEndDrag` (destroy ghost)
  - [x] 4.5 Implement `OnDrop` — handle `ItemSlotUI` source (inventory→actionbar assign) and `ActionBarSlotUI` source (actionbar→actionbar swap)
  - [x] 4.6 Implement `RemoveGhostImage()` — null-guard before `Destroy`
  - [x] 4.7 Verified — compilation clean

- [x] Task 5: Update `ItemSlotUI.cs` (AC: 6)
  - [x] 5.1 In `ItemSlotUI.OnDrop`, add check for `ActionBarSlotUI` source before the existing `ItemSlotUI` source check
  - [x] 5.2 If source is `ActionBarSlotUI`: call `source.NotifyDropHandled()`, `source.RemoveGhostImage()`, get `ActionBarUI` via `source.GetComponentInParent<ActionBarUI>()`, call `OnActionBarSlotClearedByDragToInventory(source.SlotIndex)` and return
  - [x] 5.3 Verified — 175/175 tests pass, no regressions
  - [x] 5.4 Verified — compilation clean

- [x] Task 5b: QOL — drag action bar slot to any UI area clears the slot (AC: 6)
  - [x] 5b.1 Add `_dropHandled` bool + `NotifyDropHandled()` to `ActionBarSlotUI`
  - [x] 5b.2 `OnBeginDrag` sets `_dropHandled = false`; `OnEndDrag` clears slot if `!_dropHandled`
  - [x] 5b.3 Drop targets (`ActionBarSlotUI.OnDrop` swap case, `ItemSlotUI.OnDrop` ab case) call `source.NotifyDropHandled()` before processing
  - [x] 5b.4 Verified — compilation clean

- [x] Task 5c: Update `InventoryUI.cs` with action bar integration (AC: 6)
  - [x] 5c.1 Add `[SerializeField] private ActionBarUI _actionBarUI` — `RefreshSlots()` calls `_actionBarUI?.Refresh()` to keep action bar in sync when inventory operations change item stacks
  - [x] 5c.2 Add `[SerializeField] private ActionBarSystem _actionBarSystem` — `InventoryUI` subscribes to `OnActionBarUsed` while panel is open to refresh inventory slots when hotkeys consume items
  - [x] 5c.3 `Open()` subscribes, `Close()` unsubscribes `_actionBarSystem.OnActionBarUsed`
  - [x] 5c.4 `HandleActionBarUsed` calls `RefreshSlots(false)` — `false` skips the `_actionBarUI?.Refresh()` call (already handled by `ActionBarUI` directly) to avoid double refresh
  - [x] 5c.5 Verified — compilation clean

- [x] Task 6: Create prefabs (AC: 7, 8)
  - [x] 6.1 Create folder `Assets/_Game/Prefabs/UI/ActionBar/`
  - [x] 6.2 Create `ActionBarSlot.prefab` — root 64×64 background Image, children: Icon 52×52, StackCountText TMP (upper-left, 12pt, bold, inactive default), KeyLabel TMP (lower-right, 10pt). Wired.
  - [x] 6.3 Create `ActionBar.prefab` — root with HorizontalLayoutGroup + ContentSizeFitter, 6 ActionBarSlot children. ActionBarUI._slotUIs[0..5] wired.
  - [x] 6.4 Position ActionBar RectTransform: anchored bottom-center, pivot (0.5, 0), offset Y = 20px
  - [x] 6.5 Add `ActionBar.prefab` as child of UICanvas (Screen Space Overlay canvas in TestScene)
  - [x] 6.6 `ActionBarSystem` added to Player prefab with `_inventorySystem` + `_playerTransform` wired; `ActionBarUI._actionBarSystem` + `_inventorySystem` wired to Player prefab refs

- [x] Task 7: Write Edit Mode tests (AC: 9)
  - [x] 7.1 Create `Assets/Tests/EditMode/ActionBarSystemTests.cs`
  - [x] 7.2 Implement 6 test methods per AC 9

- [ ] Task 8: Play Mode validation (AC: 10)
  - [ ] 8.1–8.6 Manual in-editor validation per AC 10 checklist

## Dev Notes

Story 4.11 adds the persistent HUD Quick Action Bar — the final deliverable of Epic 4. It builds entirely on infrastructure from stories 4.9 (`UsableItemSO.OnUse()`) and 4.10 (`InventorySlot`, `DecrementStack`, `ItemSlotUI.Bind()` badge). No architectural decisions are open — the ActionBar System Pattern is fully specified in `game-architecture.md`.

---

### CRITICAL: InputSystem_Actions Dual-File Contract

`InputSystem_Actions.cs` embeds the **entire action map JSON as a string literal** in its constructor. The `.inputactions` file is only the editor UI source — it does NOT drive runtime behavior.

**You MUST edit both files:**
1. `Assets/_Game/InputSystem_Actions.inputactions` — for Unity editor UI
2. `Assets/_Game/InputSystem_Actions.cs` — the embedded JSON (uses `""` for escaped quotes inside the string literal)

If you only edit `.inputactions`, `InputSystem_Actions.Player.ActionBar1` will throw `ArgumentException: ActionBar1 not found` at runtime.

---

### CRITICAL: Previous/Next Keyboard Binding Conflict

The current `Player` action map has:
- `Previous` action — bound to `<Keyboard>/1` and `<Gamepad>/dpad/left`
- `Next` action — bound to `<Keyboard>/2` and `<Gamepad>/dpad/right`

Adding `ActionBar1` → `<Keyboard>/1` and `ActionBar2` → `<Keyboard>/2` will cause BOTH actions to fire on the same key press. This is a bug.

**Required fix:** Remove the `<Keyboard>/1` binding from `Previous` and the `<Keyboard>/2` binding from `Next`. Their Gamepad D-Pad bindings are unrelated and may remain. Apply this fix in BOTH files (`.inputactions` + embedded `.cs` JSON).

---

### CRITICAL: ActionBarSystem is NOT a Singleton

`ActionBarSystem` is a MonoBehaviour on the Player prefab (same as `InventorySystem`). Access it via `[SerializeField]` reference wired in the Inspector — never `FindFirstObjectByType<ActionBarSystem>()` outside of `Awake` initialization contexts.

Both `ActionBarSystem` and `ActionBarUI` live on the Player prefab / HUD Canvas. Wire references directly in the Prefab Inspector.

---

### CRITICAL: ValidateSlots — When to Call

`ValidateSlots()` must be called after any action that could make a slot reference stale:
1. After `HandleHotkeyPressed()` if a `DecrementStack` was performed (item may now be gone)
2. At the start of `ActionBarUI.Refresh()` (called on every drag-drop operation)

Since `InventorySystem` has no change events (prototype scope), refresh is triggered at call sites:
- After hotkey use: `ActionBarSystem.HandleHotkeyPressed` → `ValidateSlots()` → raises `OnActionBarUsed` → `ActionBarUI.Refresh()` + `InventoryUI.RefreshSlots(false)` (if open)
- After inventory UI use/drop: `InventoryUI.UseItem/DropItem` → `RefreshSlots()` → `_actionBarUI.Refresh()`
- After drag-drop on action bar: each `ActionBarUI.On*` method calls `Refresh()` directly

Items removed by other means (e.g. bulk removal in future epics) will not auto-refresh the action bar — acceptable for prototype scope.

---

### CRITICAL: Ghost Image Must Have `raycastTarget = false`

The drag ghost image (created in `OnBeginDrag`) MUST set `raycastTarget = false` on its `Image` component. Without this, the ghost sits on top of drop targets and blocks `OnDrop` from firing on `ActionBarSlotUI` and `ItemSlotUI`. This was established in `Assets/_Game/Scripts/UI/CLAUDE.md`.

---

### CRITICAL: Cancel Drag from Empty Action Bar Slot

If the player begins dragging an empty action bar slot (`Item == null`), the drag must be cancelled:
```csharp
public void OnBeginDrag(PointerEventData eventData)
{
    if (Item == null)
    {
        eventData.pointerDrag = null; // Cancel the drag
        return;
    }
    // ... create ghost
}
```
Without this, `OnEndDrag` fires with a null `_ghostImage` (no ghost was created) — harmless but messy. Cleaner to cancel at start.

---

### CRITICAL: InventorySlot Index Stability After DecrementStack

When `DecrementStack(index)` removes a depleted stack, all items after that index shift down by one. Any `ActionBarSlotData` storing an inventory index > the removed index is now stale.

`ValidateSlots()` handles this by **searching for the item by reference** (not by stored index):
```csharp
for (int j = 0; j < items.Count; j++)
{
    if (items[j].Item == slot.Item) { foundIndex = j; break; }
}
if (foundIndex == -1) _slots[i] = null;                                          // item gone
else if (foundIndex != slot.InventoryIndex)                                       // shifted
    _slots[i] = new ActionBarSlotData { InventoryIndex = foundIndex, Item = slot.Item };
```

This correctly updates shifted indices instead of incorrectly clearing valid slots. Call `ValidateSlots()` (via `ActionBarUI.Refresh()`) immediately after any `DecrementStack`.

**Edge case:** if two separate stacks of the same `ItemSO` exist in inventory, `ValidateSlots` always finds the first occurrence. Both action bar slots would point to stack 0. Acceptable since the potion stacking system (4.10) merges stacks of the same type.

---

### ActionBarUI Placement — HUD Canvas

The `ActionBar.prefab` must be placed on the **HUD Canvas** (the always-visible Screen Space Overlay canvas), not the inventory panel's canvas (which is toggled). The action bar is always visible — it must not disappear when the inventory panel is closed.

For the prototype test scene, if a dedicated HUD Canvas doesn't exist yet, create one as a child of the Player prefab or as a standalone Canvas in the test scene. Use `renderMode = 0` (Screen Space Overlay). **Never use World Space Canvas for HUD elements.**

---

### ActionBarSlotUI — OnDrop Priority Order

`ItemSlotUI.OnDrop` is the drop handler for inventory slots. When an `ActionBarSlotUI` is dragged onto an `ItemSlotUI`, the `ItemSlotUI.OnDrop` fires (the drag source is the `ActionBarSlotUI`). The current `ItemSlotUI.OnDrop` code:

```csharp
public void OnDrop(PointerEventData eventData)
{
    var source = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
    if (source == null || source == this) return;
    source.RemoveGhostImage();
    _inventoryUI.SwapSlots(source.SlotIndex, SlotIndex);
}
```

After this story, extend to:
```csharp
public void OnDrop(PointerEventData eventData)
{
    // Check action bar → inventory (clear action bar slot, item stays in inventory)
    var abSource = eventData.pointerDrag?.GetComponent<ActionBarSlotUI>();
    if (abSource != null)
    {
        abSource.RemoveGhostImage();
        var actionBarUI = abSource.GetComponentInParent<ActionBarUI>();
        actionBarUI?.OnActionBarSlotClearedByDragToInventory(abSource.SlotIndex);
        return;
    }

    // Existing: inventory → inventory swap
    var source = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
    if (source == null || source == this) return;
    source.RemoveGhostImage();
    _inventoryUI.SwapSlots(source.SlotIndex, SlotIndex);
}
```

---

### ActionBarSystem — Hotkey Subscription Pattern

Subscribe 6 separate hotkey callbacks in `OnEnable`. Use a lambda with a captured local to avoid closure-over-loop-variable bugs:

```csharp
private void OnEnable()
{
    if (_input == null) return;
    _input.Player.ActionBar1.performed += _ => HandleHotkeyPressed(0);
    _input.Player.ActionBar2.performed += _ => HandleHotkeyPressed(1);
    _input.Player.ActionBar3.performed += _ => HandleHotkeyPressed(2);
    _input.Player.ActionBar4.performed += _ => HandleHotkeyPressed(3);
    _input.Player.ActionBar5.performed += _ => HandleHotkeyPressed(4);
    _input.Player.ActionBar6.performed += _ => HandleHotkeyPressed(5);
}

private void OnDisable()
{
    if (_input == null) return;
    _input.Player.ActionBar1.performed -= _ => HandleHotkeyPressed(0);
    // ... etc.
}
```

**WARNING:** Lambda unsubscription with `+=` / `-=` using new lambdas does NOT work in C# — each `new` lambda is a different delegate instance. Store the 6 delegates as fields:

```csharp
private System.Action<InputAction.CallbackContext>[] _hotkeyHandlers;

private void Awake()
{
    _input = new InputSystem_Actions();
    _hotkeyHandlers = new System.Action<InputAction.CallbackContext>[6];
    for (int i = 0; i < 6; i++)
    {
        int captured = i;
        _hotkeyHandlers[i] = _ => HandleHotkeyPressed(captured);
    }
}

private void OnEnable()
{
    if (_input == null) return;
    _input.Player.ActionBar1.performed += _hotkeyHandlers[0];
    _input.Player.ActionBar2.performed += _hotkeyHandlers[1];
    // ...
}

private void OnDisable()
{
    if (_input == null) return;
    _input.Player.ActionBar1.performed -= _hotkeyHandlers[0];
    // ...
}
```

---

### ActionBarSlotUI — `Awake` Cannot Call `Initialize()`

`Initialize(int slotIndex, ActionBarUI actionBarUI)` is called from `ActionBarUI.Awake()` **after** Unity instantiates the slot children. `ActionBarSlotUI.Awake()` cannot know its slot index (it's not a fixed prefab property) — that's assigned at runtime by `ActionBarUI`. This is intentional. Do NOT try to derive slot index from sibling index in `Awake`.

---

### Test Infrastructure — ActionBarSystemTests Pattern

`ActionBarSystem` is a `MonoBehaviour` — cannot be instantiated directly in Edit Mode. Use the `GameObject` helper pattern:

```csharp
[Test]
public void Assign_StoresSlotReference()
{
    var go = new GameObject();
    var system = go.AddComponent<ActionBarSystem>();
    // Use reflection or a test-helper accessor if InventorySystem is required
    // For pure logic tests, test ValidateSlots with a mock InventorySystem approach
    // OR test only the data layer methods that don't need serialized refs
    Object.DestroyImmediate(go);
}
```

For `ValidateSlots`, the `_inventorySystem` must be assigned — create a second GO with `InventorySystem` and assign via reflection:
```csharp
var inventoryGO = new GameObject();
var inventory = inventoryGO.AddComponent<InventorySystem>();
typeof(ActionBarSystem).GetField("_inventorySystem",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
    .SetValue(system, inventory);
```

The `_playerTransform` null-guard disables the component if missing — for tests that don't need it, assign `system.transform` as a stand-in.

---

### Namespace Summary

| Class | Namespace | File Location |
|-------|-----------|---------------|
| `ActionBarSystem` | `Game.Inventory` | `Assets/_Game/Scripts/Inventory/ActionBarSystem.cs` |
| `ActionBarUI` | `Game.UI` | `Assets/_Game/Scripts/UI/ActionBarUI.cs` |
| `ActionBarSlotUI` | `Game.UI` | `Assets/_Game/Scripts/UI/ActionBarSlotUI.cs` |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Inventory/ActionBarSystem.cs
Assets/_Game/Scripts/UI/ActionBarUI.cs
Assets/_Game/Scripts/UI/ActionBarSlotUI.cs
Assets/_Game/Prefabs/UI/ActionBar/ActionBar.prefab
Assets/_Game/Prefabs/UI/ActionBar/ActionBarSlot.prefab
Assets/Tests/EditMode/ActionBarSystemTests.cs
```

**Files to MODIFY:**
```
Assets/_Game/InputSystem_Actions.inputactions     ← 6 new ActionBar1-6 actions; remove Keyboard 1/2 from Previous/Next
Assets/_Game/InputSystem_Actions.cs               ← embedded JSON mirror of inputactions changes
Assets/_Game/Scripts/UI/ItemSlotUI.cs             ← OnDrop extended to handle ActionBarSlotUI source
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs    ← API unchanged; DecrementStack already exists
Assets/_Game/Scripts/UI/InventoryUI.cs               ← no direct changes needed (drag logic extended in ItemSlotUI)
Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs ← OnUse() API is already correct
Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs ← no changes
Assets/_Game/Scripts/UI/ItemSlotUI.cs                ← Bind() signature unchanged
```

Wait — `ItemSlotUI.cs` appears in both MODIFY and NOT to modify. **Correction**: `ItemSlotUI.cs` IS modified (OnDrop extended per Task 5). Remove from "not to modify" list.

**Files to MODIFY (updated):**
```
Assets/_Game/InputSystem_Actions.inputactions     ← 6 new ActionBar1-6 actions; remove Keyboard 1/2 from Previous/Next
Assets/_Game/InputSystem_Actions.cs               ← embedded JSON mirror of inputactions changes
Assets/_Game/Scripts/UI/ItemSlotUI.cs             ← OnDrop extended to handle ActionBarSlotUI source + NotifyDropHandled
Assets/_Game/Scripts/UI/InventoryUI.cs            ← ActionBarUI + ActionBarSystem refs; RefreshSlots triggers action bar refresh; subscribes to OnActionBarUsed while open
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs    ← API unchanged
Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs ← unchanged
Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs ← unchanged
```

### References

- Architecture — ActionBar System Pattern (data flow, components, design rules): `_bmad-output/game-architecture.md` §ActionBar System Pattern
- Sprint Change Proposal — full rationale, impact analysis, feature spec: `_bmad-output/sprint-change-proposal-2026-03-18.md`
- Story 4.10 — `InventorySlot` struct, `DecrementStack`, `ItemSlotUI.Bind()` badge pattern: `_bmad-output/implementation-artifacts/4-10-potion-stacking-system.md`
- Story 4.9 — `UsableItemSO.OnUse()`, `SkillItemSO`, `consumable` flag: `_bmad-output/implementation-artifacts/4-9-usable-item-system.md`
- `project-context.md` — Inventory system patterns (MonoBehaviour on Player, not singleton), logging rules, input subscription rules, drag-and-drop ghost pattern
- `Assets/_Game/Scripts/UI/CLAUDE.md` — Canvas setup, cursor management, drag-drop ghost must have `raycastTarget = false`, OnDisable null guard
- `Assets/_Game/CLAUDE.md` — InputSystem_Actions dual-file contract (CRITICAL)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- All code tasks (1–7 + 5b + 5c) implemented. 175/175 Edit Mode tests pass. Task 8 (play mode validation) is a manual in-editor checklist.
- InputSystem_Actions dual-file contract updated: 6 ActionBar1–6 actions added, Keyboard 1/2 removed from Previous/Next bindings in BOTH `.inputactions` and embedded `.cs` JSON.
- ActionBarSystem uses stored delegate array pattern (not new lambda per subscribe/unsubscribe) per Dev Notes critical warning about C# lambda reference equality.
- ActionBarUI subscribes to OnActionBarUsed C# event in OnEnable/OnDisable (project rule compliance).
- Ghost image has `raycastTarget = false` per CLAUDE.md drag-drop rule.
- Empty slot drag cancelled via `eventData.pointerDrag = null` per Dev Notes critical note.
- ActionBarSlotData changed from `internal` to `public` struct (required: public GetSlot() return type must be at least as accessible as the method).
- Prefabs created via editor script (CreateActionBarPrefabs.cs). ActionBarSystem added to Player prefab via SetupActionBarSystem.cs. References wired via WireActionBarUI.cs. Editor scripts deleted after verification.
- ValidateSlots() fixed: now searches by item reference instead of stored index, preventing false clears when a prior stack is removed and indices shift.
- QOL: dragging an action bar slot to any UI area (not just an inventory slot) now clears the slot, via `_dropHandled` flag pattern in ActionBarSlotUI.
- InventoryUI refreshes when items are consumed via hotkey (subscribes to OnActionBarUsed GameEventSO_Int via OnEnable/OnDisable) and action bar refreshes when items are consumed via inventory UI (UseItem calls _actionBarUI?.Refresh()). InventoryUI requires _actionBarUI and _onActionBarUsed wired in scene Inspector.
- [Code Review] Architecture fix: replaced C# event OnActionBarUsed with GameEventSO_Int at Assets/_Game/Data/Events/OnActionBarUsed.asset per project cross-system communication rule. ActionBarSystem raises via Raise(), ActionBarUI and InventoryUI subscribe via AddListener/RemoveListener in OnEnable/OnDisable.
- [Code Review] GetSlot() bounds check added — out-of-range returns null with Warn log.
- [Code Review] ActionBarUI.Awake() now sets enabled = false on misconfiguration, preventing Refresh() NullReferenceException.
- [Code Review] ItemSlotUI caches Canvas in Awake instead of calling GetComponentInParent per drag.
- [Code Review] ValidateSlots() double-call removed — HandleHotkeyPressed no longer calls it directly; Refresh() handles it.
- [Code Review] ActionBarSlotUI.BindEmpty() resets _dropHandled = false to prevent stale flag on re-bind.
- [Code Review] Deleted 3 one-shot editor scripts (CreateActionBarPrefabs, SetupActionBarSystem, WireActionBarUI).

### File List

Assets/_Game/InputSystem_Actions.inputactions
Assets/_Game/InputSystem_Actions.cs
Assets/_Game/Scripts/Inventory/ActionBarSystem.cs
Assets/_Game/Scripts/UI/ActionBarUI.cs
Assets/_Game/Scripts/UI/ActionBarSlotUI.cs
Assets/_Game/Scripts/UI/ItemSlotUI.cs
Assets/_Game/Data/Events/OnActionBarUsed.asset
Assets/_Game/Prefabs/UI/ActionBar/ActionBarSlot.prefab
Assets/_Game/Prefabs/UI/ActionBar/ActionBar.prefab
Assets/_Game/Prefabs/Player/Player.prefab
Assets/_Game/Prefabs/UI/UICanvas.prefab
Assets/_Game/Scenes/TestScene.unity
Assets/Tests/EditMode/ActionBarSystemTests.cs
