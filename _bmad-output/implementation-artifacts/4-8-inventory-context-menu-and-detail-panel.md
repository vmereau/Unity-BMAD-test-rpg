# Story 4.8: Inventory Context Menu & Item Detail Panel

Status: done

## Story

As a player,
I want to right-click an inventory slot to open a context menu (starting with
"Drop Item"), and left-click a slot to select it and see its name, description,
and icon in a detail panel on the right side of the inventory,
so that item management feels intentional and informative.

## Acceptance Criteria

1. **Right-click on an `ItemSlotUI`** opens a context menu near the cursor:
   - Context menu is a small Panel with one Button: "Drop Item"
   - `ItemSlotUI.OnPointerClick(Right)` calls
     `GetComponentInParent<InventoryUI>().ShowContextMenu(SlotIndex, eventData.position)`
     instead of `DropItem` directly
   - The "Drop Item" button calls `InventoryUI.DropItem(contextMenuSlotIndex)` then
     closes the menu
   - Context menu closes on: Escape key, inventory close (`Close()`), or clicking
     outside the menu (pointer click on the canvas background)
   - Only one context menu visible at a time; opening a new one closes the previous

2. **Left-click on an `ItemSlotUI`** (without drag) selects that slot:
   - `OnPointerClick(Left)` calls
     `GetComponentInParent<InventoryUI>().SelectSlot(SlotIndex)`
   - Previously selected slot visually deselects; new slot visually highlights
     (e.g., distinct background tint — `[SerializeField]` color, Inspector-driven)
   - `ItemSlotUI` exposes `public void SetSelected(bool selected)` to toggle highlight
   - If the same slot is clicked again it remains selected (no toggle-off)
   - Selection clears on inventory close (all slots deselected, detail panel cleared)

3. **`InventoryUI` context menu management**:
   - `[SerializeField] private GameObject _contextMenuPrefab` — Inspector-wired
   - `private GameObject _activeContextMenu` — runtime instance (null when hidden)
   - `private GameObject _contextMenuBlocker` — full-screen transparent click-outside blocker
   - `private int _contextMenuSlotIndex = -1`
   - `public void ShowContextMenu(int slotIndex, Vector2 screenPos)`:
     1. `HideContextMenu()` (close any open one)
     2. Instantiate a full-screen transparent blocker Button as child of Canvas root (behind menu)
     3. Wire blocker `onClick` → `HideContextMenu`
     4. Instantiate `_contextMenuPrefab` as child of Canvas root (above blocker)
     5. Position `RectTransform` at `screenPos`, clamped to screen bounds
     6. Wire "Drop Item" button `onClick` → `() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); }`
     7. Store instances in `_activeContextMenu` / `_contextMenuBlocker`, store `slotIndex` in `_contextMenuSlotIndex`
   - `public void HideContextMenu()`: destroy `_activeContextMenu` and `_contextMenuBlocker` if not null, reset to null
   - `Close()` calls `HideContextMenu()` and `ClearSelection()`
   - `HandleClose(CallbackContext)` (Escape) also calls `HideContextMenu()` before `Close()`

4. **`InventoryUI` selection and detail panel**:
   - `[SerializeField] private GameObject _detailPanel` — Inspector-wired (right-side panel)
   - `[SerializeField] private Image _detailIcon` — item icon display
   - `[SerializeField] private TMP_Text _detailName` — item name display
   - `[SerializeField] private TMP_Text _detailDescription` — item description display
   - `private int _selectedSlotIndex = -1`
   - `private ItemSlotUI _selectedSlotUI = null`
   - `public void SelectSlot(int slotIndex)`:
     1. Call `_selectedSlotUI?.SetSelected(false)` to deselect previous
     2. Find the new `ItemSlotUI` via `_contentRoot.GetChild(slotIndex).GetComponent<ItemSlotUI>()`
     3. Call `newSlot.SetSelected(true)`
     4. Store references in `_selectedSlotIndex` and `_selectedSlotUI`
     5. Call `UpdateDetailPanel(item)` with the item at `slotIndex`
   - `private void UpdateDetailPanel(ItemSO item)`:
     - `_detailIcon.sprite = item.icon;`
     - `_detailIcon.color = item.icon != null ? Color.white : Color.gray;`
     - `_detailName.text = item.itemName;`
     - `_detailDescription.text = item.description;`
     - `_detailPanel.SetActive(true)`
   - `private void ClearSelection()`:
     - `_selectedSlotUI?.SetSelected(false)`
     - `_selectedSlotIndex = -1; _selectedSlotUI = null`
     - `_detailPanel.SetActive(false)`
   - `RefreshSlots()` restores selection if `_selectedSlotIndex` is still valid;
     clears it if the selected slot no longer exists (e.g. item was dropped)

5. **`InventoryContextMenu.prefab`** created at
   `Assets/_Game/Prefabs/UI/InventoryContextMenu.prefab`:
   - Root: Panel with semi-transparent dark `Image`, width 160 height 48
   - Child: `DropItemButton` — `Button` + `TMP_Text` label "Drop Item"
   - Button text: white, fontSize 14
   - Prefab does **not** wire the `onClick` — `InventoryUI.ShowContextMenu` wires
     it at runtime via `GetComponentInChildren<Button>()`

6. **TestScene wiring**:
   - `InventoryUI._contextMenuPrefab` → `InventoryContextMenu.prefab`
   - `InventoryUI._detailPanel` → new `ItemDetailPanel` GO (right side of inventory Canvas)
   - `InventoryUI._detailIcon` → `ItemDetailPanel/Icon` Image component
   - `InventoryUI._detailName` → `ItemDetailPanel/ItemName` TMP_Text
   - `InventoryUI._detailDescription` → `ItemDetailPanel/Description` TMP_Text
   - `ItemDetailPanel` starts inactive (shown only when item is selected)
   - Detail panel positioned to the right of `InventoryPanel` — suggested:
     anchor right-center, width 240, height 500, same vertical position as inventory

7. **No compile errors.** All 132 existing Edit Mode tests pass. No new Edit Mode
   tests required (UI pointer interactions are not testable in Edit Mode).

8. **Play Mode validation**:
   - Left-click a slot → slot highlights; detail panel appears on right with correct
     icon, name, and description
   - Left-click a different slot → previous deselects, new highlights, detail panel updates
   - Right-click a slot → context menu appears near cursor with "Drop Item" button
   - Click "Drop Item" → item removed from inventory, world item spawned, menu closes,
     detail panel clears if the dropped item was selected
   - Click outside context menu → menu closes, selection unchanged
   - Press Escape or close inventory → context menu dismissed, selection cleared,
     detail panel hidden
   - Drag-to-reorder still works; selection restored after slot refresh if item still present
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Modify `ItemSlotUI.cs` (AC: 1, 2)
  - [x] 1.1 Change `OnPointerClick(Right)` to call `ShowContextMenu(SlotIndex, eventData.position)` instead of `DropItem`
  - [x] 1.2 Add `OnPointerClick(Left)` → calls `SelectSlot(SlotIndex)`
  - [x] 1.3 Add `[SerializeField] private Image _backgroundImage` for selection highlight
  - [x] 1.4 Add `[SerializeField] private Color _normalColor` and `_selectedColor` (Inspector-driven)
  - [x] 1.5 Implement `public void SetSelected(bool selected)` — sets `_backgroundImage.color`

- [x] Task 2: Extend `InventoryUI.cs` with context menu (AC: 3)
  - [x] 2.1 Add `_contextMenuPrefab`, `_activeContextMenu`, `_contextMenuBlocker`, `_contextMenuSlotIndex` fields
  - [x] 2.2 Implement `ShowContextMenu(int slotIndex, Vector2 screenPos)` with blocker + menu instantiation + position clamping
  - [x] 2.3 Implement `HideContextMenu()` — destroy both blocker and menu
  - [x] 2.4 Call `HideContextMenu()` inside `Close()` and `HandleClose()`

- [x] Task 3: Extend `InventoryUI.cs` with selection + detail panel (AC: 4)
  - [x] 3.1 Add `_detailPanel`, `_detailIcon`, `_detailName`, `_detailDescription` fields
  - [x] 3.2 Add `_selectedSlotIndex`, `_selectedSlotUI` tracking fields
  - [x] 3.3 Implement `SelectSlot(int slotIndex)`
  - [x] 3.4 Implement `UpdateDetailPanel(ItemSO item)`
  - [x] 3.5 Implement `ClearSelection()`
  - [x] 3.6 Call `ClearSelection()` in `Close()`
  - [x] 3.7 Update `RefreshSlots()` to restore or clear selection after refresh

- [x] Task 4: Create `InventoryContextMenu.prefab` (AC: 5)
  - [x] 4.1 Create Panel (160×48, semi-transparent dark) + Button child hierarchy
  - [x] 4.2 Add TMP_Text "Drop Item" label (white, fontSize 14) to button

- [x] Task 5: TestScene wiring (AC: 6)
  - [x] 5.1 Create `ItemDetailPanel` GO in TestScene Canvas (right of InventoryPanel, 240×500)
  - [x] 5.2 Add `Icon` (Image), `ItemName` (TMP_Text), `Description` (TMP_Text) children inside ItemDetailPanel
  - [x] 5.3 Wire all new `[SerializeField]` fields on `InventoryUI` component in Inspector
  - [x] 5.4 Ensure `ItemDetailPanel` starts inactive

- [x] Task 6: Play Mode validation (AC: 8)
  - [x] 6.1 Validate left-click selection + detail panel populates correctly
  - [x] 6.2 Validate right-click context menu appears and "Drop Item" drops and closes
  - [x] 6.3 Validate click-outside dismisses menu; Escape + close clears everything
  - [x] 6.4 Validate drag-to-reorder still works; selection restored after RefreshSlots

## Dev Notes

Story 4.8 is a pure UX layer on top of the completed inventory backend
(Stories 4.2–4.4). No changes to `InventorySystem`, `ItemPickup`, or `ItemSO`.
All `ItemSO` fields needed (`itemName`, `description`, `icon`) are already present.

---

### CRITICAL: OnPointerClick Left vs Drag — No Conflict

`OnPointerClick` fires only when the pointer was NOT dragged before release. Unity's
EventSystem suppresses click events when a drag threshold is exceeded. This means:
- Drag-to-reorder (existing): `OnBeginDrag` → `OnDrag` → `OnEndDrag` — no click fires
- Left-click-to-select: `OnPointerClick(Left)` fires — no conflict with drag

No special guard needed to distinguish drag from click.

---

### CRITICAL: Context Menu Position Clamping

Since the project Canvas is Screen Space Overlay, `rectTransform.position = screenPos`
works directly (screen space = world space for overlay canvas). Clamp to keep within screen:

```csharp
var rt = _activeContextMenu.GetComponent<RectTransform>();
rt.position = screenPos;
var pos = rt.position;
pos.x = Mathf.Clamp(pos.x, 0, Screen.width - rt.rect.width);
pos.y = Mathf.Clamp(pos.y, rt.rect.height, Screen.height);
rt.position = pos;
```

---

### CRITICAL: RefreshSlots Must Restore Selection

After drag-to-swap or any `RefreshSlots()` call, slots are destroyed and recreated.
The `_selectedSlotUI` reference becomes stale (destroyed GO). Always re-acquire from
`_contentRoot` children after rebuild:

```csharp
private void RefreshSlots()
{
    // ... destroy + repopulate as before ...

    // Restore selection if slot index still valid
    if (_selectedSlotIndex >= 0 && _selectedSlotIndex < _inventorySystem.Items.Count)
    {
        var slot = _contentRoot.GetChild(_selectedSlotIndex).GetComponent<ItemSlotUI>();
        _selectedSlotUI = slot;
        slot.SetSelected(true);
        UpdateDetailPanel(_inventorySystem.Items[_selectedSlotIndex]);
    }
    else
    {
        ClearSelection();
    }
}
```

---

### Context Menu Click-Outside Blocker Pattern

The blocker is a full-screen transparent overlay parented to the canvas root, positioned
**below `_panelRoot`** (using `SetSiblingIndex`) so that clicks on inventory slots still
reach their `IPointerClickHandler`. Any click outside the inventory panel hits the blocker
and closes the menu. Clicks on the panel background (not a slot) are handled by an
`AnyButtonClickListener` added to `_panelRoot` in `Awake`.

**CRITICAL INVARIANT:** `_panelRoot` must be a **direct child** of the canvas root for
the sibling-index placement to produce correct z-order (blocker below panel, menu on top).

`AnyButtonClickListener` (inner class in `InventoryUI.cs`) catches any pointer button (left,
right, middle) and fires a delegate — replacing the old left-only `Button.onClick` approach.

```csharp
public void ShowContextMenu(int slotIndex, Vector2 screenPos)
{
    if (_activeContextMenu != null && _contextMenuSlotIndex == slotIndex) return;
    HideContextMenu();
    _contextMenuSlotIndex = slotIndex;

    var canvas = _canvas; // serialized reference, NOT GetComponentInParent

    // Blocker below _panelRoot — slots above blocker still receive click events
    _contextMenuBlocker = new GameObject("ContextMenuBlocker");
    _contextMenuBlocker.transform.SetParent(canvas.transform, false);
    // ... stretch to full canvas ...
    _contextMenuBlocker.transform.SetSiblingIndex(_panelRoot.transform.GetSiblingIndex());
    var blockerListener = _contextMenuBlocker.AddComponent<AnyButtonClickListener>();
    blockerListener.callback = (_) => HideContextMenu();

    // Menu panel (topmost)
    _activeContextMenu = Instantiate(_contextMenuPrefab, canvas.transform);
    _activeContextMenu.transform.SetAsLastSibling();
    // ... position + clamp ...
    var btn = _activeContextMenu.GetComponentInChildren<Button>();
    btn.onClick.AddListener(() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); });
}

internal class AnyButtonClickListener : MonoBehaviour, IPointerClickHandler
{
    [System.NonSerialized] public System.Action<PointerEventData.InputButton> callback;
    public void OnPointerClick(PointerEventData eventData) => callback?.Invoke(eventData.button);
}
```

---

### ItemSlotUI Selection Highlight

Use the existing root `Image` component (`_backgroundImage`) for the highlight.
Wire `_backgroundImage` to the root `Image` of `ItemSlot.prefab`:

```csharp
[SerializeField] private Image _backgroundImage;
[SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
[SerializeField] private Color _selectedColor = new Color(0.4f, 0.6f, 1f, 0.9f);

public void SetSelected(bool selected)
{
    if (_backgroundImage != null)
        _backgroundImage.color = selected ? _selectedColor : _normalColor;
}
```

Also call `SetSelected(false)` inside `Bind()` to reset state when slots are refreshed.

---

### Namespace and Usings

All modifications stay within existing namespaces — no new usings required:
- `ItemSlotUI.cs` → `namespace Game.UI` (`UnityEngine.UI` and `UnityEngine.EventSystems` already imported)
- `InventoryUI.cs` → `namespace Game.UI` (`Image`, `TMP_Text`, `Button` already imported via existing usings)

---

### Project Structure

**Files to CREATE:**
```
Assets/_Game/Prefabs/UI/InventoryContextMenu.prefab
Assets/_Game/Prefabs/UI/InventoryContextMenu.prefab.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/UI/ItemSlotUI.cs          ← OnPointerClick routing + SetSelected
Assets/_Game/Scripts/UI/InventoryUI.cs         ← Context menu + selection + detail panel
Assets/_Game/Scenes/TestScene.unity            ← ItemDetailPanel GO + new field wiring
_bmad-output/implementation-artifacts/sprint-status.yaml  ← 4-8 status: in-progress → done
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs   ← No changes
Assets/_Game/Scripts/Inventory/ItemPickup.cs        ← No changes
Assets/_Game/ScriptableObjects/Items/ItemSO.cs      ← No changes
Assets/_Game/InputSystem_Actions.cs                ← No changes (UI pointer events, not input actions)
Assets/_Game/InputSystem_Actions.inputactions      ← No changes
```

**Call chain (right-click → context menu → drop):**
```
Player right-clicks ItemSlotUI[1]
  → ItemSlotUI.OnPointerClick(Right)
  → GetComponentInParent<InventoryUI>().ShowContextMenu(1, eventData.position)
  → Blocker instantiated; InventoryContextMenu prefab instantiated + positioned
  → Player clicks "Drop Item" button
  → InventoryUI.DropItem(1) → item removed + world item spawned
  → HideContextMenu() → blocker + menu destroyed
  → RefreshSlots() → selection cleared (dropped item gone)
```

**Call chain (left-click → select → detail panel):**
```
Player left-clicks ItemSlotUI[0]
  → ItemSlotUI.OnPointerClick(Left)
  → GetComponentInParent<InventoryUI>().SelectSlot(0)
  → _selectedSlotUI?.SetSelected(false) — previous slot normal color
  → _contentRoot.GetChild(0).GetComponent<ItemSlotUI>().SetSelected(true) — highlight
  → UpdateDetailPanel(items[0]) → icon, name, description populated
  → _detailPanel.SetActive(true)
```

### References

- Story 4.3 — `ItemSlotUI` drag handler patterns, `InventoryUI` architecture, `GetComponentInParent<InventoryUI>()` pattern
- Story 4.4 — `DropItem` implementation, right-click `OnPointerClick` pattern (superseded by this story)
- CLAUDE.md — Canvas renderMode MCP gotcha, OnDisable null guard pattern
- project-context.md — GameLog mandatory, no `Resources.Load()`, `[SerializeField] private` preferred

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Completion Notes List

- Tasks 1–5 implemented and validated. Play Mode validation (Task 6) requires manual in-editor testing.
- `ItemSlotUI.cs`: routed `OnPointerClick` to Left→`SelectSlot` / Right→`ShowContextMenu`; added `SetSelected(bool)` + `_backgroundImage` highlight fields; `Bind()` calls `SetSelected(false)` on each slot refresh.
- `InventoryUI.cs`: added `ShowContextMenu` (full-screen blocker + prefab instantiation + screen-bound clamping + runtime button wiring), `HideContextMenu`, `SelectSlot`, `UpdateDetailPanel`, `ClearSelection`; `Close()` calls both `HideContextMenu()` and `ClearSelection()`; `RefreshSlots()` nulls stale `_selectedSlotUI` before destroy loop then restores selection if index still valid.
- `InventoryContextMenu.prefab`: Panel 160×48 dark background, `DropItemButton` child with stretch-fill RectTransform and `TextMeshProUGUI` "Drop Item" label (white, 14pt). `onClick` NOT wired in prefab — `ShowContextMenu` wires at runtime via `GetComponentInChildren<Button>()`.
- `ItemSlot.prefab`: added `_backgroundImage: {fileID: 4098734291305939011}` (root Image) to `ItemSlotUI` component via direct YAML edit.
- TestScene: `ItemDetailPanel` GO created (240×500, dark bg, inactive by default) with `Icon` (Image), `ItemName` (TMP_Text bold 16pt), `Description` (TMP_Text 12pt word-wrap) children. All 5 new `InventoryUI` fields wired in Inspector.
- **Pre-existing test failure**: `ItemPickup_Interact_WhenDisabledDueToNullItem_DoesNotThrow` (1/132) fails due to Edit Mode Awake behavior on `ItemPickup.cs` — file not modified in this story. 131/132 tests pass.

### File List

Assets/_Game/Scripts/UI/ItemSlotUI.cs
Assets/_Game/Scripts/UI/InventoryUI.cs
Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab
Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab.meta
Assets/_Game/Prefabs/UI/Inventory/ItemSlot.prefab
Assets/_Game/Prefabs/UI/Inventory/ItemSlot.prefab.meta
Assets/_Game/Prefabs/UI/Inventory/InventoryPanel.prefab
Assets/_Game/Prefabs/UI/Inventory/InventoryPanel.prefab.meta
Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab
Assets/_Game/Prefabs/UI/Inventory/ItemDetailPanel.prefab.meta
Assets/_Game/Prefabs/UI/Inventory.meta
Assets/_Game/Prefabs/UI/UICanvas.prefab
Assets/_Game/Prefabs/UI/UICanvas.prefab.meta
Assets/_Game/Prefabs/UI/InteractionUI.prefab
Assets/_Game/Prefabs/UI/InteractionUI.prefab.meta
Assets/_Game/Scenes/TestScene.unity
Assets/_Game/Scenes/TestScene.meta
Assets/_Game/Scenes/TestScene/NavMesh-Floor.asset
Assets/_Game/Scenes/TestScene/NavMesh-Floor.asset.meta
_bmad-output/implementation-artifacts/sprint-status.yaml
_bmad-output/implementation-artifacts/4-8-inventory-context-menu-and-detail-panel.md

## Change Log

- 2026-03-14: Story created via correct-course workflow — replaces direct right-click
  drop with context menu; adds left-click item selection and item detail panel.
  Supersedes the right-click behavior from Story 4.4. (claude-sonnet-4-6)
- 2026-03-14: Implemented Tasks 1–5; Play Mode validation (Task 6) pending manual testing.
  Modified ItemSlotUI, InventoryUI, ItemSlot prefab; created InventoryContextMenu prefab;
  wired ItemDetailPanel in TestScene. 131/132 Edit Mode tests pass (1 pre-existing
  ItemPickup failure unrelated to this story). (claude-sonnet-4-6)
- 2026-03-14: Play Mode validated. Refined right-click behavior: context menu now appears
  to right of cursor (pivot (0,1)), same-slot re-click is a no-op, right/left click anywhere
  outside the menu (panel background or outside panel) closes it. ItemDetailPanel scaled up
  (300×560, icon 96×96, ItemName 20pt, Description 15pt). (claude-sonnet-4-6)
- 2026-03-14: Code review (adversarial). Fixed: deleted stale Assets/InputSystem_Actions.cs
  duplicate at root (H1); replaced GetComponentInParent<Canvas>() in ShowContextMenu with
  cached _canvas field (H2); added SelectSlot same-slot early-return (L1); cached
  GetComponentInParent<InventoryUI>() in ItemSlotUI.Awake, used in OnPointerClick and
  OnDrop (L2); added [System.NonSerialized] to AnyButtonClickListener.callback (L3).
  File List corrected to Inventory/ subfolder paths; Dev Notes updated with actual
  AnyButtonClickListener blocker pattern. Story → done. (claude-sonnet-4-6)
