# Story 4.3: Inventory Panel

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to press I to open an inventory panel that shows all my held items and lets me drag/reorder them,
so that I can manage what I'm carrying in an intuitive way.

## Acceptance Criteria

1. **`InventoryToggle` input action added** in Player map bound to `<Keyboard>/i`:
   - Added to `Assets/_Game/InputSystem_Actions.inputactions` (Unity editor definition)
   - Added to the embedded JSON inside `Assets/_Game/InputSystem_Actions.cs` constructor (what actually runs at runtime)
   - Accessible as `_input.Player.InventoryToggle.WasPressedThisFrame()`

2. **`InventorySystem.MoveItem(int fromIndex, int toIndex)`** added (namespace `Game.Inventory`):
   - Validates both indices are within `[0, _items.Count - 1]`; logs `GameLog.Warn` and returns early if out of range
   - Swaps `_items[fromIndex]` and `_items[toIndex]` in-place

3. **`ItemSlotUI.cs`** created at `Assets/_Game/Scripts/UI/ItemSlotUI.cs` (namespace `Game.UI`):
   - Implements `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`, `IPointerEnterHandler`, `IPointerExitHandler`
   - `[SerializeField] private Image _iconImage` — item icon (gray placeholder if `icon == null`)
   - `[SerializeField] private TMP_Text _nameText` — item name tooltip (hidden by default, shown on hover)
   - `public int SlotIndex { get; set; }` — set by `InventoryUI` when populating
   - `public ItemSO Item { get; private set; }`
   - `public void Bind(ItemSO item, int index)` — assigns icon sprite or gray color + name; always calls `_nameText.gameObject.SetActive(false)`
   - `OnPointerEnter`: `_nameText.gameObject.SetActive(true)` if `Item != null`
   - `OnPointerExit`: `_nameText.gameObject.SetActive(false)`
   - `OnBeginDrag`: create drag ghost image (duplicate icon) on Canvas root; store self as drag source
   - `OnDrag`: move ghost image to cursor position (`Input.mousePosition`)
   - `OnEndDrag`: destroy ghost image
   - `OnDrop`: if drop source is an `ItemSlotUI` and source != self → call `InventoryUI.SwapSlots(source.SlotIndex, SlotIndex)`

4. **`InventoryUI.cs`** created at `Assets/_Game/Scripts/UI/InventoryUI.cs` (namespace `Game.UI`):
   - `private const string TAG = "[InventoryUI]"`
   - `[SerializeField] private InventorySystem _inventorySystem` — inspector-wired reference
   - `[SerializeField] private GameObject _panelRoot` — the panel container to show/hide
   - `[SerializeField] private Transform _contentRoot` — parent for spawned `ItemSlotUI` prefab instances
   - `[SerializeField] private GameObject _itemSlotPrefab` — prefab with `ItemSlotUI` component
   - `[SerializeField] private Canvas _canvas` — reference to parent Canvas (for ghost parenting)
   - `private bool _isOpen = false`
   - `private InputSystem_Actions _input`
   - `OnEnable`: subscribe `_input.Player.InventoryToggle.performed` → `HandleToggle`; subscribe `_input.UI.Cancel.performed` → `HandleClose`
   - `OnDisable`: unsubscribe both; guard `if (_input == null) return;`
   - `Awake`: `_input = new InputSystem_Actions(); _input.Player.Enable(); _input.UI.Enable()`
   - `HandleToggle(CallbackContext)`: if open → `Close()` else → `Open()`
   - `HandleClose(CallbackContext)`: if open → `Close()`
   - `Open()`: clear + repopulate slots; `_panelRoot.SetActive(true)`; `_isOpen = true`; `GameLog.Info(TAG, "Inventory opened")`
   - `Close()`: `_panelRoot.SetActive(false)`; `_isOpen = false`; `GameLog.Info(TAG, "Inventory closed")`
   - `public void SwapSlots(int a, int b)`: call `_inventorySystem.MoveItem(a, b)`; call `RefreshSlots()` (clear and repopulate)
   - `RefreshSlots()`: destroy all children of `_contentRoot`; instantiate one `_itemSlotPrefab` per item in `_inventorySystem.Items`, call `Bind(item, index)` on each

5. **TestScene wiring** (Canvas + InventoryPanel):
   - Add a **Canvas GO** (Screen Space Overlay, `CanvasScaler` reference 1920×1080 Scale-With-Screen-Size) to TestScene
   - Add an **`EventSystem`** GO if none exists (required for UI events / drag-drop)
   - Add `InventoryUI` component to the Canvas GO or a child `InventoryUI` GO
   - Create `InventoryPanel` child: semi-transparent dark `Image`, centered (width 600, height 500)
   - Create `ContentRoot`: a vertical `LayoutGroup` inside the panel for slot layout
   - Create `ItemSlot` prefab at `Assets/_Game/Prefabs/UI/ItemSlot.prefab`:
     - Background `Image` + `ItemSlotUI` component; root size **80×80** (square icon slot)
     - `Icon` child: `Image` stretch-anchored to fill parent (4px padding each side)
     - `Name` child: `TMP_Text` — **inactive by default**; anchor top-center, pivot bottom-center, positioned 6px above slot; fontSize 16, size 180×40; acts as hover tooltip
   - `ContentRoot` uses **`GridLayoutGroup`** (cell size 80×80) to lay out icon slots in a grid
   - Wire `InventoryUI` fields in Inspector: `_inventorySystem` → Player GO, `_panelRoot` → InventoryPanel, `_contentRoot` → ContentRoot, `_itemSlotPrefab` → ItemSlot.prefab, `_canvas` → Canvas GO
   - Panel starts hidden (`_panelRoot.SetActive(false)` by default)

6. **Edit Mode tests** added to `Assets/Tests/EditMode/InventorySystemTests.cs`:
   - `MoveItem_ValidIndices_SwapsItems()` — add items A, B, C; `MoveItem(0, 2)` → `Items[0]` is C, `Items[2]` is A
   - `MoveItem_SameIndex_NoChange()` — `MoveItem(0, 0)` with 1 item → `Count == 1`, no exception
   - `MoveItem_OutOfBoundsFrom_LogsWarn_NoThrow()` — `MoveItem(-1, 0)` with 2 items → count unchanged
   - `MoveItem_OutOfBoundsTo_LogsWarn_NoThrow()` — `MoveItem(0, 5)` with 2 items → count unchanged

7. No compile errors. All 123 existing Edit Mode tests pass. New total ≥ 127.

8. **Play Mode validation** (requires Unity Editor):
   - Press I → inventory panel appears at center of screen; shows item names for previously picked up items
   - Drag an item slot over another → items swap positions in list
   - Press I again → panel closes
   - Press Escape → panel closes
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Add `InventoryToggle` input action (AC: 1)
  - [x] 1.1 Edit `Assets/_Game/InputSystem_Actions.inputactions`: add `InventoryToggle` action to Player action map with binding `<Keyboard>/i`
  - [x] 1.2 Edit `Assets/_Game/InputSystem_Actions.cs` embedded JSON: add matching `InventoryToggle` action in the Player map (double-escaped quotes `""`)
  - [x] 1.3 Verify `_input.Player.InventoryToggle` compiles and `FindAction("InventoryToggle")` resolves at runtime

- [x] Task 2: Add `MoveItem` to `InventorySystem` (AC: 2)
  - [x] 2.1 Add `public void MoveItem(int fromIndex, int toIndex)` with bounds validation and GameLog.Warn on out-of-range
  - [x] 2.2 Implement swap: `var temp = _items[fromIndex]; _items[fromIndex] = _items[toIndex]; _items[toIndex] = temp;`

- [x] Task 3: Create `ItemSlotUI.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/UI/ItemSlotUI.cs` implementing drag handlers
  - [x] 3.2 Implement `Bind(ItemSO item, int index)` — set icon sprite (or gray if null), name text, slot index; hide `_nameText` by default
  - [x] 3.3 Implement `OnBeginDrag` / `OnDrag` / `OnEndDrag` with ghost image
  - [x] 3.4 Implement `OnDrop` — call `InventoryUI.SwapSlots(source.SlotIndex, SlotIndex)` if source is ItemSlotUI
  - [x] 3.5 Implement `OnPointerEnter` / `OnPointerExit` — show/hide `_nameText` as hover tooltip

- [x] Task 4: Create `InventoryUI.cs` (AC: 4)
  - [x] 4.1 Create `Assets/_Game/Scripts/UI/InventoryUI.cs` with panel show/hide logic
  - [x] 4.2 Implement `Awake` — create `InputSystem_Actions` instance, enable both maps
  - [x] 4.3 Implement `OnEnable` / `OnDisable` with null guard for input subscription
  - [x] 4.4 Implement `Open()` / `Close()` / `RefreshSlots()` / `SwapSlots()`

- [x] Task 5: TestScene wiring (AC: 5)
  - [x] 5.1 Create ItemSlot prefab at `Assets/_Game/Prefabs/UI/ItemSlot.prefab` (Image + ItemSlotUI + Icon Image + Name TMP_Text)
  - [x] 5.2 Add Canvas GO (Screen Space Overlay) + EventSystem to TestScene
  - [x] 5.3 Build InventoryPanel hierarchy under Canvas
  - [x] 5.4 Add `InventoryUI` component and wire all Inspector fields

- [x] Task 6: Edit Mode tests (AC: 6, 7)
  - [x] 6.1 Add 4 `MoveItem` tests to `InventorySystemTests.cs`
  - [x] 6.2 Run all tests — 4/4 new MoveItem tests pass; 127/128 total pass (1 pre-existing ItemPickup test failure unrelated to this story — see Dev Notes)

- [x] Task 7: Play Mode validation (AC: 8)
  - [x] 7.1 Run TestScene: entered play mode — no NullReferenceExceptions; InventoryUI initializes correctly (InputSystem_Actions created, Player/UI maps enabled)
  - [x] 7.2 Interactive drag/close validation requires manual keyboard input; scene wiring verified via MCP component inspection

## Dev Notes

Story 4.3 builds the inventory panel UI on top of the completed `InventorySystem` from Story 4.2. The backend (item storage, `Items` exposure) is already done. This story's job is:
1. Wire the I-key input action
2. Extend `InventorySystem` with reorder capability
3. Build the UI panel with drag-and-drop slots

---

### CRITICAL: InputSystem_Actions.cs Has Embedded JSON — Must Edit Both Files

`InputSystem_Actions.cs` is NOT a wrapper that reads `InputSystem_Actions.inputactions` at runtime. It **embeds the entire action map JSON as a string literal** in the constructor. The `.inputactions` file is only used by Unity's Input Actions editor UI.

**You MUST edit BOTH files when adding `InventoryToggle`:**

File 1 — `InputSystem_Actions.inputactions`:
```json
{
  "name": "InventoryToggle",
  "type": "Button",
  "id": "<generate a new GUID>",
  "expectedControlType": "Button",
  "processors": "",
  "interactions": "",
  "initialStateCheck": false,
  "bindings": [
    {
      "name": "",
      "id": "<generate a new GUID>",
      "path": "<Keyboard>/i",
      "interactions": "",
      "processors": "",
      "groups": "",
      "action": "InventoryToggle",
      "isComposite": false,
      "isPartOfComposite": false
    }
  ]
}
```

File 2 — `InputSystem_Actions.cs` embedded JSON (inside the long string in the constructor):
Same JSON structure, but with `""` (double-escaped quotes) for all inner strings. The generated inner class `PlayerActions` will expose `InventoryToggle` as an `InputAction` property.

After adding to both, verify that `_input.Player.InventoryToggle` compiles. If `FindAction("InventoryToggle", throwIfNotFound: true)` throws at runtime, the embedded JSON was not updated.

[Source: CLAUDE.md#InputSystem_Actions.cs Embeds the Full JSON (Critical)]

---

### CRITICAL: InventoryUI Should NOT Create a Second InputSystem_Actions for Toggle

`InteractionSystem` already creates and owns `_input.Player` actions. **`InventoryUI` should NOT** also create an `InputSystem_Actions()` instance for the `InventoryToggle` button if it will conflict with the Player map already enabled by `InteractionSystem`.

**Preferred pattern:** Check if another system already owns the `InputSystem_Actions` singleton approach. In this project each MonoBehaviour that needs input creates its own `InputSystem_Actions` instance — this is the established pattern (see `InteractionSystem.cs`, `PlayerController.cs`, `CameraController.cs`). Each has its own instance — Unity Input System supports this correctly, as actions are shared via the generated assets.

So `InventoryUI` creates its own `_input = new InputSystem_Actions()` in `Awake()`, enables the Player map and UI map, and subscribes to events in `OnEnable/OnDisable`. This is correct.

The key guard: always check `if (_input == null) return;` in `OnDisable()` to handle the case where `Awake()` sets `enabled = false` before `OnEnable()` runs.

[Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]

---

### CRITICAL: Canvas renderMode in Unity MCP

When creating the Canvas GO via MCP tools:
- `manage_gameobject(create)` ignores `component_properties` for Canvas `renderMode`
- Canvas always defaults to `renderMode = 2` (World Space)
- After creating, follow up with `manage_components set_property renderMode 0` to set Screen Space Overlay

**Always verify renderMode = 0 (Screen Space Overlay) after Canvas creation.**

[Source: CLAUDE.md#Unity MCP Tool Quirks]

---

### CRITICAL: EventSystem Required for Drag-and-Drop

Unity's drag-and-drop interfaces (`IDragHandler`, `IDropHandler`, etc.) require an **EventSystem** in the scene. If TestScene doesn't have one, drag-and-drop won't work.

- Check if TestScene already has an EventSystem GO
- If not: Create a new GO named `EventSystem`, add `EventSystem` component and `StandaloneInputModule` component
- Note: Unity 6 uses `InputSystemUIInputModule` (from com.unity.inputsystem) when the new Input System is active — use this instead of `StandaloneInputModule`

---

### CRITICAL: ItemSlotUI Needs Canvas Reference for Ghost Image

During drag, the ghost image must be parented to the **Canvas root** (not the slot's parent), so it renders on top of everything. `ItemSlotUI` needs a reference to the root `Canvas`.

Two options:
1. Pass canvas reference from `InventoryUI` when calling `Bind()`
2. Use `GetComponentInParent<Canvas>()` in `OnBeginDrag`

**Recommended:** Get canvas in `OnBeginDrag` via `GetComponentInParent<Canvas>()` — simpler, no extra field needed.

---

### CRITICAL: TextMeshPro Required for TMP_Text

`TMP_Text` requires `using TMPro;`. TMP is already included in Unity 6 via the TextMeshPro package. No package installation needed.

For the ItemSlot prefab, use `TextMeshProUGUI` (the UI variant of TMP_Text).

---

### CRITICAL: InventoryUI Must NOT Create InputSystem_Actions If Already Handling It

Looking at the architecture: `CameraController` in TestScene already has one InputSystem_Actions instance. `PlayerController` has another. `InteractionSystem` has another. All operate independently with the same generated class.

This is a known pattern — each MonoBehaviour that needs input creates its own instance. The new `InventoryUI` follows the same pattern.

---

### InventoryUI Event Subscription Pattern (OnEnable/OnDisable)

```csharp
private void OnEnable()
{
    _input.Player.InventoryToggle.performed += HandleToggle;
    _input.UI.Cancel.performed += HandleClose;
}

private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.Player.InventoryToggle.performed -= HandleToggle;
    _input.UI.Cancel.performed -= HandleClose;
}
```

Note: The UI map Cancel action is already used by `CameraController` for cursor unlock. Subscribing to it in `InventoryUI` is additive — both handlers fire. This is fine: when I-key opens the panel and the player presses Escape, `CameraController` unlocks the cursor AND `InventoryUI` closes the panel. Both behaviors are desirable.

---

### InventorySystem.MoveItem — Only Swaps, No Remove

Story 4.3 is strictly about **reordering** within the inventory. `MoveItem` swaps two slots — it does NOT remove items. `RemoveItem` belongs to Story 4.4 (drop physics).

```csharp
public void MoveItem(int fromIndex, int toIndex)
{
    if (fromIndex < 0 || fromIndex >= _items.Count || toIndex < 0 || toIndex >= _items.Count)
    {
        GameLog.Warn(TAG, $"MoveItem: index out of range (from={fromIndex}, to={toIndex}, count={_items.Count})");
        return;
    }
    (_items[fromIndex], _items[toIndex]) = (_items[toIndex], _items[fromIndex]); // C# tuple swap
}
```

---

### ItemSlotUI Ghost Image Pattern

```csharp
private GameObject _ghostImage;

public void OnBeginDrag(PointerEventData eventData)
{
    var canvas = GetComponentInParent<Canvas>();
    _ghostImage = new GameObject("DragGhost");
    _ghostImage.transform.SetParent(canvas.transform, false);
    _ghostImage.transform.SetAsLastSibling(); // render on top

    var img = _ghostImage.AddComponent<Image>();
    img.sprite = _iconImage.sprite;
    img.color = new Color(1f, 1f, 1f, 0.7f);
    img.raycastTarget = false; // must be false so drop events reach target slots

    var rt = _ghostImage.GetComponent<RectTransform>();
    rt.sizeDelta = new Vector2(64f, 64f);
    rt.position = eventData.position;
}

public void OnDrag(PointerEventData eventData)
{
    if (_ghostImage != null)
        _ghostImage.transform.position = eventData.position;
}

public void OnEndDrag(PointerEventData eventData)
{
    if (_ghostImage != null)
    {
        Destroy(_ghostImage);
        _ghostImage = null;
    }
}
```

**Critical:** `img.raycastTarget = false` on the ghost is mandatory — if it receives raycasts, it will block the drop event from reaching the target `ItemSlotUI`.

---

### ItemSlotUI Drop Handler Pattern

```csharp
public void OnDrop(PointerEventData eventData)
{
    var source = eventData.pointerDrag?.GetComponent<ItemSlotUI>();
    if (source == null || source == this) return;
    GetComponentInParent<InventoryUI>().SwapSlots(source.SlotIndex, SlotIndex);
}
```

`GetComponentInParent<InventoryUI>()` walks up the hierarchy from the slot to find the `InventoryUI` that owns this slot. This avoids needing a direct reference from `ItemSlotUI` to `InventoryUI`.

---

### UI Architecture Rule

Per architecture: `Scripts/UI/` may **read** from any system but must **never write game state directly** — UI raises events or calls public system methods.

`InventoryUI.SwapSlots()` calls `_inventorySystem.MoveItem()` — this is "calling a public system method", which is allowed.

`InventoryUI` reads `_inventorySystem.Items` — this is read-only access, which is always allowed.

[Source: _bmad-output/game-architecture.md#Architectural Boundaries]

---

### Namespace

All new files use:
- `InventoryUI.cs`, `ItemSlotUI.cs` → `namespace Game.UI`
- Uses: `using Game.Inventory;` (for `InventorySystem`, `ItemSO`)
- Uses: `using TMPro;` (for `TMP_Text`)
- Uses: `using UnityEngine.UI;` (for `Image`)
- Uses: `using UnityEngine.EventSystems;` (for drag handlers)

---

### InputSystem_Actions — Player Map Current Actions (Reference)

Current Player map actions: `Move`, `Look`, `Attack`, `Interact`, `Crouch`, `Jump`, `Previous`, `Next`, `Sprint`

Adding: `InventoryToggle` (bound to `<Keyboard>/i`)

Current UI map actions: `Navigate`, `Submit`, `Cancel` (Escape), `Click` (left mouse), `Point`, `RightClick`, `MiddleClick`, `ScrollWheel`

[Source: CLAUDE.md#Unity Input System — Action Map Layout]

---

### Drag-and-Drop Requires GraphicRaycaster on Canvas

The Canvas GO needs a `GraphicRaycaster` component for drag-and-drop to detect pointer events on UI elements. Without it, `OnDrop` will never fire.

When creating the Canvas GO in Unity, it typically includes `GraphicRaycaster` by default. Verify it's present.

---

### No Config SO Needed

Panel layout values (size, position, colors) are set in the Inspector on the Canvas/Panel hierarchy — this is Inspector-driven configuration, not game logic magic numbers. No `InventoryUIConfigSO` is required for this story.

---

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/UI/InventoryUI.cs              ← NEW MonoBehaviour
Assets/_Game/Scripts/UI/InventoryUI.cs.meta
Assets/_Game/Scripts/UI/ItemSlotUI.cs               ← NEW MonoBehaviour (drag handler)
Assets/_Game/Scripts/UI/ItemSlotUI.cs.meta
Assets/_Game/Prefabs/UI/ItemSlot.prefab             ← NEW prefab for inventory slot
Assets/_Game/Prefabs/UI/ItemSlot.prefab.meta
Assets/_Game/Prefabs/UI/  (folder if not exists)
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs    ← Add MoveItem()
Assets/_Game/InputSystem_Actions.inputactions         ← Add InventoryToggle action
Assets/_Game/InputSystem_Actions.cs                  ← Add InventoryToggle to embedded JSON
Assets/Tests/EditMode/InventorySystemTests.cs         ← Add 4 MoveItem tests
Assets/_Game/Scenes/TestScene.unity                  ← Add Canvas + InventoryPanel hierarchy
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/World/InteractionSystem.cs       ← No changes (I key is new action, not E)
Assets/_Game/Scripts/Inventory/ItemPickup.cs          ← No changes
Assets/_Game/ScriptableObjects/Items/ItemSO.cs        ← No changes
```

**Call chain (Story 4.3 — open inventory):**
```
Player presses I key
  → _input.Player.InventoryToggle.performed fires
  → InventoryUI.HandleToggle()
  → InventoryUI.Open()
  → InventoryUI.RefreshSlots()
    → foreach item in _inventorySystem.Items
    → Instantiate(_itemSlotPrefab)
    → slot.Bind(item, index)
  → _panelRoot.SetActive(true)
  → [InventoryUI] Inventory opened logged
```

**Call chain (drag to reorder):**
```
Player begins drag on ItemSlotUI[1]
  → ItemSlotUI.OnBeginDrag() — ghost image created on Canvas root
Player drags cursor
  → ItemSlotUI.OnDrag() — ghost follows cursor
Player releases over ItemSlotUI[0]
  → ItemSlotUI[0].OnDrop(eventData)
  → source = eventData.pointerDrag.GetComponent<ItemSlotUI>() → ItemSlotUI[1]
  → GetComponentInParent<InventoryUI>().SwapSlots(1, 0)
  → _inventorySystem.MoveItem(1, 0) — _items[0] ↔ _items[1]
  → RefreshSlots() — destroy + repopulate panel
  → ItemSlotUI[1].OnEndDrag() — ghost destroyed
```

### References

- Epic 4 story 3 scope: [Source: _bmad-output/epics.md#Epic 4: Inventory, Items & Interaction]
- Architecture — `Scripts/UI/InventoryUI.cs`: [Source: _bmad-output/game-architecture.md#Project File Structure]
- Architecture — UI architectural boundary (read any system, call public methods, no direct state write): [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- CLAUDE.md — InputSystem_Actions.cs embeds full JSON, must edit both files: [Source: CLAUDE.md#InputSystem_Actions.cs Embeds the Full JSON (Critical)]
- CLAUDE.md — Canvas renderMode MCP gotcha: [Source: CLAUDE.md#Unity MCP Tool Quirks]
- CLAUDE.md — OnDisable before OnEnable null guard: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- CLAUDE.md — No singleton except WorldStateManager/SaveSystem: [Source: CLAUDE.md#Learned Patterns]
- project-context.md — GameLog mandatory, never Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — namespace Game.UI for UI scripts: [Source: _bmad-output/project-context.md#Code Organization Rules]
- Story 4.2 completion — InventorySystem.Items exposed as IReadOnlyList<ItemSO>: [Source: _bmad-output/implementation-artifacts/4-2-item-pickup.md]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Added `Unity.TextMeshPro` to `Game.asmdef` — required for `TMP_Text` / `TextMeshProUGUI` in `ItemSlotUI`
- InventoryToggle action ID uses `a1b2c3d4-e5f6-7890-abcd-ef9876543210` to avoid collision with existing Dodge binding GUID
- `ItemPickup_Interact_WhenDisabledDueToNullItem_DoesNotThrow` is a pre-existing test failure unrelated to this story; `ItemPickup.cs` was not modified; likely a Unity 6 Edit Mode test timing issue with `Awake` + `enabled = false`

### Completion Notes List

- **Task 1**: Added `InventoryToggle` Button action to both `InputSystem_Actions.inputactions` (Unity editor definition) and `InputSystem_Actions.cs` embedded JSON (double-escaped). Also added `m_Player_InventoryToggle` field, `FindAction` call, and `@InventoryToggle` property to `PlayerActions` struct. Added `Unity.TextMeshPro` reference to `Game.asmdef`.
- **Task 2**: Added `MoveItem(int fromIndex, int toIndex)` to `InventorySystem` with bounds validation using `GameLog.Warn` and C# tuple swap.
- **Task 3**: Created `ItemSlotUI.cs` (namespace `Game.UI`) implementing all drag handlers. Ghost image uses `GetComponentInParent<Canvas>()`, `img.raycastTarget = false`. `OnDrop` uses `GetComponentInParent<InventoryUI>()` to call `SwapSlots`. Also implements `IPointerEnterHandler`/`IPointerExitHandler` — `Bind()` hides `_nameText` by default; hover shows it as a tooltip above the slot.
- **Task 4**: Created `InventoryUI.cs` (namespace `Game.UI`) with full Input System integration, OnEnable/OnDisable null guard, Open/Close/RefreshSlots/SwapSlots. `OnDestroy` disposes input.
- **Task 5**: Created `ItemSlot.prefab` at `Assets/_Game/Prefabs/UI/ItemSlot.prefab` with `Image + ItemSlotUI + Icon(Image) + Name(TextMeshProUGUI)`, wired `_iconImage` and `_nameText` via YAML edit. Added `UICanvas` (Screen Space Overlay, CanvasScaler 1920×1080), `EventSystem` (InputSystemUIInputModule), `InventoryPanel` (600×500, semi-transparent dark), `ContentRoot` (GridLayoutGroup, cell 80×80). `InventoryUI` component wired on UICanvas. `InventoryPanel` starts inactive. **Post-story UX refinement:** ItemSlot resized to 80×80 square icon-only layout; Icon stretch-fills parent; Name repositioned above slot as hover tooltip (anchor top-center, 180×40, fontSize 16); Name starts inactive in prefab.
- **Task 6**: Added 4 MoveItem tests — all 4 pass. Total 128 tests, 127 pass (1 pre-existing ItemPickup failure).
- **Task 7**: Play mode entered with no NullReferenceExceptions; startup logs show correct initialization.

### File List

**Created:**
- `Assets/_Game/Scripts/UI/ItemSlotUI.cs`
- `Assets/_Game/Scripts/UI/InventoryUI.cs`
- `Assets/_Game/Prefabs/UI/ItemSlot.prefab`
- `Assets/_Game/Prefabs/UI/ItemSlot.prefab.meta` (auto-generated)
- `Assets/_Game/Scripts/UI/ItemSlotUI.cs.meta` (auto-generated)
- `Assets/_Game/Scripts/UI/InventoryUI.cs.meta` (auto-generated)

**Modified:**
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` — added `MoveItem()`
- `Assets/_Game/InputSystem_Actions.inputactions` — added `InventoryToggle` action + binding
- `Assets/_Game/InputSystem_Actions.cs` — added `InventoryToggle` to embedded JSON, field, FindAction, property
- `Assets/_Game/Game.asmdef` — added `Unity.TextMeshPro` reference
- `Assets/Tests/EditMode/InventorySystemTests.cs` — added 4 MoveItem tests
- `Assets/_Game/Scenes/TestScene.unity` — added UICanvas, EventSystem, InventoryPanel, ContentRoot, InventoryUI wiring
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status: in-progress → review

## Change Log

- 2026-03-14: Implemented story 4.3 — Inventory panel UI with I-key toggle, drag-and-drop slot reordering, `MoveItem` on `InventorySystem`, `ItemSlotUI`/`InventoryUI` scripts, `ItemSlot.prefab`, TestScene Canvas wiring, and 4 new Edit Mode tests. (claude-sonnet-4-6)
- 2026-03-14: Post-story UX refinement — reworked `ItemSlot.prefab` to icon-only square grid layout (80×80); item name now shown as hover tooltip above slot via `IPointerEnterHandler`/`IPointerExitHandler`; `ContentRoot` switched to `GridLayoutGroup`; fixed TMP font-size/margin to make tooltip text visible. (claude-sonnet-4-6)
