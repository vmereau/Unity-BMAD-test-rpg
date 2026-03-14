# Story 4.4: Item Drop Physics

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to right-click an item in my inventory to drop it,
so that it falls to the floor with physics and I can pick it back up.

## Acceptance Criteria

1. **`InventorySystem.RemoveItem(int index)`** added (namespace `Game.Inventory`):
   - Returns the `ItemSO` that was removed
   - Logs `GameLog.Info(TAG, $"Removed: {item.itemName} (total: {_items.Count})")` on success
   - Returns `null` and logs `GameLog.Warn` if `index < 0` or `index >= _items.Count`
   - No exception thrown for out-of-range indices

2. **`ItemPickup.Configure(ItemSO item)`** public method added:
   - Sets the `_item` field at runtime (used when spawning a dropped world item)
   - Does NOT find `_inventory` — Awake still handles that
   - Existing `Awake()` null guard on `_item` is preserved: if `_item` is still null when Awake runs (i.e., Configure was never called), it logs error and sets `enabled = false`

3. **`WorldItem.prefab`** created at `Assets/_Game/Prefabs/World/WorldItem.prefab`:
   - Root GameObject: `WorldItem` — **inactive by default** (`activeSelf = false` in prefab asset)
   - Components on root: `ItemPickup`, `Rigidbody` (useGravity: true, isKinematic: false, mass: 0.5), `SphereCollider` (radius: 0.15, center: (0,0,0)), `MeshFilter` (sphere mesh), `MeshRenderer` (URP Lit material)
   - `ItemPickup._item` is null in the prefab (set at runtime via `Configure()`)
   - No child objects needed (simple visual sphere is sufficient as a placeholder)

4. **`ItemSlotUI`** implements `IPointerClickHandler` — right-click drops the item:
   - Implement `OnPointerClick(PointerEventData eventData)`
   - If `eventData.button == PointerEventData.InputButton.Right` AND `Item != null`:
     → call `GetComponentInParent<InventoryUI>().DropItem(SlotIndex)`
   - Left-click and middle-click: do nothing (ignore)

5. **`InventoryUI`** extended with drop capability:
   - `[SerializeField] private Transform _playerTransform` — Inspector-wired to Player root GO
   - `[SerializeField] private GameObject _worldItemPrefab` — Inspector-wired to `WorldItem.prefab`
   - `public void DropItem(int slotIndex)`:
     1. `var item = _inventorySystem.RemoveItem(slotIndex)` — guard if null return
     2. Compute drop position: `_playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f`
     3. `var go = Instantiate(_worldItemPrefab, dropPos, Quaternion.identity)` (instantiates inactive)
     4. `go.GetComponent<ItemPickup>().Configure(item)` — sets item before activation
     5. `go.SetActive(true)` — triggers Awake, finds InventorySystem, enables interaction
     6. Apply impulse: `go.GetComponent<Rigidbody>().AddForce((_playerTransform.forward * 2f + Vector3.up * 1f), ForceMode.Impulse)`
     7. Call `RefreshSlots()` to update the inventory panel
     8. Log `GameLog.Info(TAG, $"Dropped: {item.itemName}")`

6. **TestScene wiring**:
   - `InventoryUI._playerTransform` → Player GO root Transform (the same Player used for `_inventorySystem` wiring)
   - `InventoryUI._worldItemPrefab` → `WorldItem.prefab` asset reference

7. **Edit Mode tests** added to `Assets/Tests/EditMode/InventorySystemTests.cs`:
   - `RemoveItem_ValidIndex_RemovesAndReturnsItem()` — add items A, B, C; `RemoveItem(1)` → returns B, count=2, `Items[0]`=A, `Items[1]`=C
   - `RemoveItem_OutOfBoundsIndex_ReturnsNull_NoThrow()` — 2 items, `RemoveItem(5)` → returns null, count unchanged, no exception
   - `RemoveItem_NegativeIndex_ReturnsNull_NoThrow()` — 2 items, `RemoveItem(-1)` → returns null, count unchanged, no exception

8. No compile errors. All previous Edit Mode tests pass. New total ≥ 131 (128 previous + 3 new).

9. **Play Mode validation**:
   - Open inventory (I key) → right-click an item slot → item removed from panel, WorldItem sphere spawns in front of player, falls to ground with gravity
   - Approach dropped item → crosshair interaction prompt appears ("Press E to pick up [item name]")
   - Press E → item re-enters inventory
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Add `RemoveItem` to `InventorySystem` (AC: 1)
  - [x] 1.1 Add `public ItemSO RemoveItem(int index)` with bounds check returning null + `GameLog.Warn` on invalid
  - [x] 1.2 On valid index: capture item, `_items.RemoveAt(index)`, log `GameLog.Info`, return item

- [x] Task 2: Add `Configure` to `ItemPickup` (AC: 2)
  - [x] 2.1 Add `public void Configure(ItemSO item)` that sets `_item = item`
  - [x] 2.2 Verify existing `Awake()` null guard still protects against unconfigured items

- [x] Task 3: Create `WorldItem.prefab` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Prefabs/World/` folder if not exists
  - [x] 3.2 Create `WorldItem.prefab` — root inactive, with `ItemPickup`, `Rigidbody` (gravity on, mass 0.5), `SphereCollider` (r=0.15), `MeshFilter` (sphere), `MeshRenderer` (URP Lit)
  - [x] 3.3 Verify prefab `activeSelf = false` in asset (so Awake defers until SetActive(true))

- [x] Task 4: Add right-click drop to `ItemSlotUI` (AC: 4)
  - [x] 4.1 Add `IPointerClickHandler` to class implements list
  - [x] 4.2 Implement `OnPointerClick` — right-click only, guard `Item != null`, call `InventoryUI.DropItem(SlotIndex)`

- [x] Task 5: Extend `InventoryUI` with drop logic (AC: 5)
  - [x] 5.1 Add `[SerializeField] private Transform _playerTransform`
  - [x] 5.2 Add `[SerializeField] private GameObject _worldItemPrefab`
  - [x] 5.3 Implement `DropItem(int slotIndex)` — remove from inventory, instantiate inactive prefab, Configure, SetActive, AddForce, RefreshSlots

- [x] Task 6: TestScene wiring (AC: 6)
  - [x] 6.1 Wire `InventoryUI._playerTransform` → Player GO in TestScene
  - [x] 6.2 Wire `InventoryUI._worldItemPrefab` → WorldItem.prefab

- [x] Task 7: Edit Mode tests (AC: 7, 8)
  - [x] 7.1 Add 3 `RemoveItem` tests to `InventorySystemTests.cs`
  - [x] 7.2 Run all tests — all 3 new tests pass; all 128 previous tests pass (127 + 1 known pre-existing failure)

- [x] Task 8: Play Mode validation (AC: 9)
  - [x] 8.1 Open inventory, right-click item slot, verify item removed from UI and WorldItem spawns with physics
  - [x] 8.2 Walk up to dropped item, verify E-key interaction prompt and pick-up restores item to inventory

## Dev Notes

Story 4.4 extends the inventory system established in Stories 4.2 and 4.3. All backend (InventorySystem, ItemPickup, InventoryUI, ItemSlotUI) is in place. This story adds:
1. `RemoveItem()` to `InventorySystem` (the inverse of `AddItem()`)
2. Runtime item configuration for `ItemPickup` (needed for instantiated world items)
3. `WorldItem.prefab` — a simple physics-enabled droppable world item
4. Right-click drop UI gesture in `ItemSlotUI`

---

### CRITICAL: WorldItem Prefab Must Start Inactive

`ItemPickup.Awake()` will immediately disable the component if `_item == null`. Since `WorldItem.prefab` has `_item = null` at design time, the prefab root **must be inactive** (`activeSelf = false`) so that `Awake()` does not run on `Instantiate()`.

The drop sequence MUST follow this exact order:
```csharp
// 1. Instantiate inactive prefab
var go = Instantiate(_worldItemPrefab, dropPos, Quaternion.identity);
// go.activeSelf == false at this point; Awake has NOT run

// 2. Configure before activation
go.GetComponent<ItemPickup>().Configure(item);

// 3. Activate — Awake runs now, _item is already set
go.SetActive(true);

// 4. Apply physics impulse (after activation, Rigidbody is live)
go.GetComponent<Rigidbody>().AddForce(_playerTransform.forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
```

**If you call `SetActive(true)` before `Configure(item)`, Awake will see `_item == null`, disable `ItemPickup`, and the item will be un-interactable.**

[Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]

---

### CRITICAL: `RemoveAt` Shifts Indices — Always Refresh Slots

`List<T>.RemoveAt(index)` shifts all subsequent elements down by one. After `RemoveItem(1)` on a 3-item list:
- Old: `[A, B, C]` → New: `[A, C]`
- Slot 2 (C) is now slot 1

`RefreshSlots()` destroys and re-creates all slot UI from scratch, so `SlotIndex` values are always correct after refresh. This is the correct pattern — do not try to surgically remove only the dropped slot.

```csharp
public void DropItem(int slotIndex)
{
    var item = _inventorySystem.RemoveItem(slotIndex);
    if (item == null) return; // invalid index, already logged

    var dropPos = _playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f;
    var go = Instantiate(_worldItemPrefab, dropPos, Quaternion.identity);
    go.GetComponent<ItemPickup>().Configure(item);
    go.SetActive(true);
    go.GetComponent<Rigidbody>().AddForce(_playerTransform.forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
    RefreshSlots();
    GameLog.Info(TAG, $"Dropped: {item.itemName}");
}
```

---

### CRITICAL: Right-Click During Drag — Edge Case

If a player begins a drag (`OnBeginDrag`) and then right-clicks (unlikely but possible), the ghost image will remain. The `OnPointerClick` handler fires when no drag is in progress (`PointerEventData.dragging == false`). Unity does NOT fire `OnPointerClick` if the pointer dragged before releasing. So right-click + drag + release will not accidentally trigger drop. This is safe by design.

However, add the guard `if (Item != null)` in `OnPointerClick` to prevent dropping empty slots.

---

### CRITICAL: `ItemPickup` Uses `FindFirstObjectByType<InventorySystem>()` in Awake

The existing `ItemPickup.Awake()` calls `FindFirstObjectByType<InventorySystem>()`. For dropped items in TestScene, `InventorySystem` is on the Player GO. `FindFirstObjectByType` will find it correctly.

**Do NOT add another `FindFirstObjectByType` call in `Configure()`** — Awake already handles this. `Configure()` only needs to set `_item`:
```csharp
public void Configure(ItemSO item)
{
    _item = item;
}
```

---

### CRITICAL: SphereCollider Radius and Layer

The `WorldItem.prefab` uses a `SphereCollider` (radius 0.15). This is small enough to not overlap with terrain but large enough for the `InteractionSystem` raycast to hit it reliably.

Per project-context.md: "Set collision layers explicitly". For this story, `WorldItem` stays on the **Default** layer. The `InteractionSystem` casts a ray from screen center — it hits Default layer objects. No custom layer needed until Epic 5 introduces terrain layers.

If you get "Interactable not hit by ray" issues: verify the SphereCollider is on the root GO (not a child), and check the `InteractionSystem` raycast distance (likely 3–5m).

[Source: _bmad-output/project-context.md#Performance Rules > Physics]

---

### CRITICAL: Rigidbody Constraints

The `WorldItem` Rigidbody should NOT freeze rotation on any axis — let it tumble freely for a natural "dropped item" feel. However, to prevent items from rolling indefinitely on flat surfaces, set angular drag to a moderate value (e.g., 2.0) in the prefab Inspector.

Suggested Rigidbody settings:
- `useGravity`: true
- `isKinematic`: false
- `mass`: 0.5
- `drag`: 0.5 (linear)
- `angularDrag`: 2.0 (rotational damping)
- Constraints: none (free rotation)

---

### CRITICAL: `IPointerClickHandler` Requires EventSystem

`OnPointerClick` fires only when an `EventSystem` is present in the scene. Story 4.3 already added an `EventSystem` (with `InputSystemUIInputModule`) to TestScene. No additional changes needed.

[Source: Story 4.3 Completion Notes — EventSystem added to TestScene]

---

### Do NOT Use `Resources.Load()`

Per project-context.md rule: `Resources.Load()` is **BANNED**. The `_worldItemPrefab` reference MUST be a `[SerializeField]` Inspector reference — not a runtime-loaded asset. Wire it in the Inspector on the `InventoryUI` component in TestScene.

[Source: _bmad-output/project-context.md#Asset Loading]

---

### ItemPickup Interaction Prompt for Dropped Items

When a dropped `WorldItem` is in the scene, hovering the crosshair over it will trigger `InteractionSystem` and show the `InteractPrompt`. The prompt defaults to `"Press E to pick up [item.itemName]"` (from `ItemPickup.InteractPrompt`). Since `Configure()` sets `_item`, the prompt will show the correct item name automatically. No changes needed to `InteractionSystem` or `InteractPrompt`.

---

### Physics Drop Position Calculation

The drop position formula `_playerTransform.position + _playerTransform.forward * 1.5f + Vector3.up * 0.5f` places the item ~1.5m in front of the player at mid-height. The Rigidbody gravity will pull it down immediately.

The `AddForce` call adds a slight forward+upward impulse so the item arcs outward rather than dropping straight down. This feels more like the player "tossing" the item.

If items clip into geometry on drop, reduce forward distance (1.0f) or check that the drop position is not inside a wall. For this story (flat TestScene), clipping is not a concern.

---

### Namespace and Usings for New/Modified Code

**`InventorySystem.cs`** (modifications only):
```csharp
// No new usings needed — all existing
namespace Game.Inventory { ... }
```

**`ItemPickup.cs`** (modification — add Configure method):
```csharp
// No new usings needed
namespace Game.Inventory { ... }
```

**`ItemSlotUI.cs`** (modification — add IPointerClickHandler):
```csharp
// No new usings needed — UnityEngine.EventSystems already imported
namespace Game.UI { ... }
```

**`InventoryUI.cs`** (modification — add drop fields and method):
```csharp
using Game.Core;
using Game.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Game.UI { ... }
```

---

### InputSystem_Actions — No Changes in This Story

Story 4.4 uses RIGHT-CLICK on UI elements — this is handled by Unity's UI event system (`IPointerClickHandler`), NOT by `InputSystem_Actions`. **Do NOT add a "Drop" action to the Input Actions asset.** The pointer event system already handles mouse button detection in UI.

[Source: CLAUDE.md#Unity Input System — Action Map Layout]

---

### Edit Mode Test Patterns

```csharp
[Test]
public void RemoveItem_ValidIndex_RemovesAndReturnsItem()
{
    // Arrange
    var system = new GameObject().AddComponent<InventorySystem>();
    var itemA = ScriptableObject.CreateInstance<ItemSO>();
    itemA.itemName = "A";
    var itemB = ScriptableObject.CreateInstance<ItemSO>();
    itemB.itemName = "B";
    var itemC = ScriptableObject.CreateInstance<ItemSO>();
    itemC.itemName = "C";
    system.AddItem(itemA);
    system.AddItem(itemB);
    system.AddItem(itemC);

    // Act
    var removed = system.RemoveItem(1);

    // Assert
    Assert.AreEqual(itemB, removed);
    Assert.AreEqual(2, system.Count);
    Assert.AreEqual(itemA, system.Items[0]);
    Assert.AreEqual(itemC, system.Items[1]);
}

[Test]
public void RemoveItem_OutOfBoundsIndex_ReturnsNull_NoThrow()
{
    var system = new GameObject().AddComponent<InventorySystem>();
    var item = ScriptableObject.CreateInstance<ItemSO>();
    system.AddItem(item);
    system.AddItem(item);

    var result = system.RemoveItem(5);

    Assert.IsNull(result);
    Assert.AreEqual(2, system.Count);
}

[Test]
public void RemoveItem_NegativeIndex_ReturnsNull_NoThrow()
{
    var system = new GameObject().AddComponent<InventorySystem>();
    var item = ScriptableObject.CreateInstance<ItemSO>();
    system.AddItem(item);

    var result = system.RemoveItem(-1);

    Assert.IsNull(result);
    Assert.AreEqual(1, system.Count);
}
```

---

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Prefabs/World/WorldItem.prefab        ← NEW dropped-item world representation
Assets/_Game/Prefabs/World/WorldItem.prefab.meta   ← auto-generated
Assets/_Game/Prefabs/World/                         ← folder (create if not exists)
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/Inventory/InventorySystem.cs  ← Add RemoveItem(int)
Assets/_Game/Scripts/Inventory/ItemPickup.cs       ← Add Configure(ItemSO) public method
Assets/_Game/Scripts/UI/InventoryUI.cs             ← Add _playerTransform, _worldItemPrefab, DropItem()
Assets/_Game/Scripts/UI/ItemSlotUI.cs              ← Add IPointerClickHandler, OnPointerClick
Assets/Tests/EditMode/InventorySystemTests.cs      ← Add 3 RemoveItem tests
Assets/_Game/Scenes/TestScene.unity               ← Wire _playerTransform + _worldItemPrefab on InventoryUI
_bmad-output/implementation-artifacts/sprint-status.yaml  ← Update 4-4 status
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/World/InteractionSystem.cs    ← No changes needed
Assets/_Game/InputSystem_Actions.cs               ← No changes (right-click is UI event, not input action)
Assets/_Game/InputSystem_Actions.inputactions     ← No changes
```

**Call chain (Story 4.4 — drop item):**
```
Player right-clicks ItemSlotUI[1]
  → ItemSlotUI.OnPointerClick(Right button)
  → GetComponentInParent<InventoryUI>().DropItem(1)
  → _inventorySystem.RemoveItem(1) → returns ItemSO
  → Instantiate(WorldItem.prefab) at drop position [INACTIVE]
  → pickup.Configure(itemSO) — sets _item
  → go.SetActive(true) → ItemPickup.Awake() runs → _item OK, _inventory found
  → rb.AddForce(forward * 2 + up * 1, Impulse) → item arcs forward and falls
  → RefreshSlots() — panel repopulates without dropped item
  → [InventoryUI] "Dropped: Health Potion" logged
```

**Call chain (re-pickup after drop):**
```
Player looks at WorldItem (SphereCollider hit by InteractionSystem raycast)
  → InteractionSystem detects IInteractable → shows prompt "Press E to pick up Health Potion"
Player presses E
  → ItemPickup.Interact()
  → _inventory.AddItem(_item) → adds back to InventorySystem
  → gameObject.SetActive(false) → WorldItem deactivated
```

### References

- Epic 4 story 4 scope: [Source: _bmad-output/epics.md#Epic 4: Inventory, Items & Interaction]
- Story 4.3 completion — EventSystem added, InventoryUI/ItemSlotUI patterns: [Source: _bmad-output/implementation-artifacts/4-3-inventory-panel.md]
- Story 4.2 — ItemPickup.Awake() null guard pattern: [Source: _bmad-output/implementation-artifacts/4-2-item-pickup.md]
- project-context.md — Resources.Load() banned, use Inspector refs: [Source: _bmad-output/project-context.md#Asset Loading]
- project-context.md — GameLog mandatory: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — `[SerializeField] private` preferred: [Source: _bmad-output/project-context.md#Engine-Specific Rules]
- project-context.md — `FindFirstObjectByType` (not deprecated `FindObjectOfType`): [Source: _bmad-output/project-context.md#Unity 6 Specific]
- CLAUDE.md — Unity lifecycle: Awake deferred when prefab inactive: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None.

### Completion Notes List

- `InventorySystem.RemoveItem(int)` added — bounds-checked, GameLog.Warn on invalid, GameLog.Info on success.
- `ItemPickup.Configure(ItemSO)` added — sets `_item` before activation so Awake null-guard passes.
- `ItemPickup.Interact()` changed from `SetActive(false)` to `Destroy(gameObject)` — prevents inactive clone accumulation over long play sessions.
- `WorldItem.prefab` was initially created as a generic sphere placeholder but superseded mid-story: `ItemSO` gained a `worldItemPrefab` field so each item references its own prefab (Health Potion / Mana Potion). Generic `WorldItem.prefab` deleted.
- `ItemSlotUI` implements `IPointerClickHandler` — right-click calls `InventoryUI.DropItem(SlotIndex)`.
- `InventoryUI.DropItem()` uses `item.worldItemPrefab` (not a shared prefab field); null-guards with `GameLog.Warn` if SO has no prefab assigned.
- TestScene: `InventoryUI._playerTransform` wired to Player root. `_worldItemPrefab` field removed (per-item prefab approach).
- Interactable layer (Layer 8) set on item prefabs — required for `InteractionSystem` raycast. CLAUDE.md updated with HIGH-severity reminder.
- 3 new Edit Mode tests added: `RemoveItem_ValidIndex`, `RemoveItem_OutOfBoundsIndex`, `RemoveItem_NegativeIndex` — all pass. Total: 131 tests, 1 known pre-existing failure.
- Play Mode validated by user: drop spawns correct item prefab with physics, E-key pickup re-adds to inventory, no NullReferenceExceptions.

### File List

Assets/_Game/ScriptableObjects/Items/ItemSO.cs
Assets/_Game/Scripts/Inventory/InventorySystem.cs
Assets/_Game/Scripts/Inventory/ItemPickup.cs
Assets/_Game/Scripts/UI/InventoryUI.cs
Assets/_Game/Scripts/UI/ItemSlotUI.cs
Assets/_Game/Scenes/TestScene.unity
Assets/_Game/Data/Items/Item_Health_Potion.asset
Assets/_Game/Data/Items/Item_Mana_Potion.asset
Assets/_Game/Prefabs/Items/TestItem_Health_Potion.prefab
Assets/_Game/Prefabs/Items/TestItem_Mana_Potion.prefab
Assets/Tests/EditMode/InventorySystemTests.cs
CLAUDE.md

## Change Log

- 2026-03-14: Implemented story 4-4 — item drop physics. Added `InventorySystem.RemoveItem`, `ItemPickup.Configure`, right-click drop in `ItemSlotUI`, `InventoryUI.DropItem`. Moved world-item prefab reference to `ItemSO.worldItemPrefab` (per-item prefab). Fixed `ItemPickup.Interact` to `Destroy` instead of `SetActive(false)`. Added Interactable layer rule to CLAUDE.md.
- 2026-03-14: Code review fixes — (1) guarded `DropItem` before `RemoveItem` to prevent item loss on missing prefab; (2) removed redundant `Configure`/`SetActive` calls (item prefabs are active with `_item` pre-baked); (3) added null-guard on `GetComponentInParent<InventoryUI>()` in `OnPointerClick`; (4) added `ItemPickup_Configure_SetsInteractPrompt` test; (5) completed File List with user-created prefabs and item assets. Total tests: 132.
