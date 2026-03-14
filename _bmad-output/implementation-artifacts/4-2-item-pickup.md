# Story 4.2: Item Pickup

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to press Interact (E) while looking at a world item to pick it up into my inventory,
so that items enter my possession naturally through the look-at interaction model.

## Acceptance Criteria

1. **`ItemSO.cs`** created at `Assets/_Game/ScriptableObjects/Items/ItemSO.cs` (namespace `Game.Inventory`):
   - `[CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]`
   - `public string itemName`
   - `public string description`
   - `public Sprite icon`
   - `public bool isStackable`

2. **`InventorySystem.cs`** created at `Assets/_Game/Scripts/Inventory/InventorySystem.cs` (namespace `Game.Inventory`):
   - `private const string TAG = "[Inventory]"`
   - `private readonly List<ItemSO> _items = new List<ItemSO>()`
   - `public IReadOnlyList<ItemSO> Items => _items` — exposes items for future Story 4.3 UI
   - `public bool AddItem(ItemSO item)`:
     - Returns `false` (with `GameLog.Warn`) if `item == null`
     - Adds to `_items`, logs `GameLog.Info(TAG, $"Picked up: {item.itemName} (total: {_items.Count})")`
     - Returns `true`
   - `public int Count => _items.Count`

3. **`ItemPickup.cs`** created at `Assets/_Game/Scripts/Inventory/ItemPickup.cs` (namespace `Game.Inventory`):
   - Implements `IInteractable` (requires `using Game.World;`)
   - `[SerializeField] private ItemSO _item` — the item's data asset
   - `[SerializeField] private string _promptOverride = ""` — leave empty to use auto-generated prompt
   - `public string InteractPrompt => string.IsNullOrEmpty(_promptOverride) ? $"Press E to pick up {_item?.itemName ?? "item"}" : _promptOverride`
   - `private InventorySystem _inventory` — found via `FindFirstObjectByType<InventorySystem>()` in `Awake()`
   - `Awake()`:
     - Null-check `_item` → `GameLog.Error(TAG, "_item not assigned — ItemPickup disabled")` + `enabled = false; return`
     - `_inventory = FindFirstObjectByType<InventorySystem>()` → null-check → error+disable if not found
   - `Interact()`:
     - Calls `_inventory.AddItem(_item)`
     - Calls `gameObject.SetActive(false)` — item disappears from world
   - **DO NOT** create an `InputSystem_Actions` instance or add an E-key handler — `InteractionSystem.LateUpdate()` already calls `CurrentInteractable.Interact()` when E is pressed

4. **Test item asset** created: `Assets/_Game/Data/Items/Item_TestPotion.asset`
   - Type: `ItemSO`
   - `itemName = "Health Potion"`, `description = "Restores a small amount of health"`, `isStackable = true`

5. **TestScene update**:
   - Add `InventorySystem` component to the **Player** GO (same as PlayerController, InteractionSystem, etc.)
   - Add a new GO named `TestItem_Potion` to TestScene:
     - Position: ~3m in front of player spawn at floor level (pick up differs from story 4.1 test cube which is at eye height)
     - Add `ItemPickup` component: `_item = Item_TestPotion.asset`
     - Add a `SphereCollider` (radius ~0.3)
     - Add a `Rigidbody` (Use Gravity: true, Is Kinematic: false, Constraints: freeze all rotations to prevent tumbling)
     - Assign to **"Interactable" layer (layer 8)** — same as TestInteractableCube from Story 4.1
     - No `InteractableObject` component (ItemPickup handles the IInteractable implementation)

6. **Edit Mode tests** at `Assets/Tests/EditMode/InventorySystemTests.cs` (namespace `Tests.EditMode`):
   - Tests must use a stub `ItemSO` or create real `ItemSO` instances via `ScriptableObject.CreateInstance<ItemSO>()`
   ```csharp
   // Pattern for creating test ItemSO in edit mode (no scene required)
   private ItemSO CreateTestItem(string name = "Test Item")
   {
       var item = ScriptableObject.CreateInstance<ItemSO>();
       item.itemName = name;
       return item;
   }
   ```
   Required tests:
   - `AddItem_ValidItem_ReturnsTrue()` — `AddItem(testItem)` returns `true`
   - `AddItem_ValidItem_IncreasesCount()` — `Count` goes from 0 to 1
   - `AddItem_NullItem_ReturnsFalse()` — `AddItem(null)` returns `false`
   - `AddItem_NullItem_CountUnchanged()` — `Count` remains 0 after null add
   - `AddItem_MultipleItems_AllAdded()` — add 3 items → `Count == 3`
   - `Items_ReflectsAddedItems()` — `Items` list contains all added items

7. No compile errors. All 109 existing Edit Mode tests pass. New total ≥ 115.

8. **Play Mode validation** (requires Unity Editor):
   - Run TestScene; look at `TestItem_Potion` (near floor): crosshair turns yellow, dev overlay shows "Press E to pick up Health Potion"
   - Press E: item GO disappears; Console shows `[Inventory] Picked up: Health Potion (total: 1)`
   - Look away from where the item was: crosshair returns to white
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Create `ItemSO.cs` (AC: 1)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Items/ItemSO.cs` with `itemName`, `description`, `icon`, `isStackable` fields

- [x] Task 2: Create `InventorySystem.cs` (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Scripts/Inventory/InventorySystem.cs` with `List<ItemSO>`, `AddItem()`, `Items`, and `Count`

- [x] Task 3: Create `ItemPickup.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/Inventory/ItemPickup.cs` implementing `IInteractable`
  - [x] 3.2 Implement `Awake()` null-checks: `_item` (error+disable) and `FindFirstObjectByType<InventorySystem>()` (error+disable)
  - [x] 3.3 Implement `Interact()`: `_inventory.AddItem(_item)` + `gameObject.SetActive(false)`
  - [x] 3.4 Verify **no** `InputSystem_Actions` instance is created — `InteractionSystem` handles all E-key input

- [x] Task 4: Create test item asset (AC: 4)
  - [x] 4.1 Create `Assets/_Game/Data/Items/` folder (it already has a `.meta` file — use MCP `create_folder` or just create the asset directly)
  - [x] 4.2 Create `Item_TestPotion.asset` via `manage_scriptable_object` with `itemName="Health Potion"`, `description="Restores a small amount of health"`, `isStackable=true`

- [x] Task 5: Update TestScene (AC: 5)
  - [x] 5.1 Add `InventorySystem` component to Player GO
  - [x] 5.2 Create `TestItem_Potion` GO: position ~3m in front of player, add `ItemPickup`, `SphereCollider`, `Rigidbody`, assign to layer 8 (Interactable), wire `_item = Item_TestPotion.asset`

- [x] Task 6: Edit Mode tests (AC: 6, 7)
  - [x] 6.1 Create `Assets/Tests/EditMode/InventorySystemTests.cs` with ≥ 6 tests covering AddItem success/failure paths and Items list reflection
  - [x] 6.2 Run all tests — verify ≥ 115 total pass, no regressions (actual: 123 passed, 0 failed)
- [x] Review Follow-ups (AI)
  - [x] [AI-Review][HIGH] `ItemPickup.Interact()` NullReferenceException when `_inventory == null` — added null guard `if (_inventory == null) return;` [ItemPickup.cs:40]
  - [x] [AI-Review][HIGH] Missing `return;` after `enabled = false` in inventory null check — added for consistency and defensive safety [ItemPickup.cs:33]
  - [x] [AI-Review][MEDIUM] ScriptableObject memory leak in tests — added `_createdItems` tracking list, destroy all in TearDown [InventorySystemTests.cs:17]
  - [x] [AI-Review][MEDIUM] No ItemPickup null-guard test — added `ItemPickup_Interact_WhenDisabledDueToNullItem_DoesNotThrow` [InventorySystemTests.cs:83]

- [x] Task 7: Play Mode validation (AC: 8)
  - [x] 7.1 Verify item pickup in TestScene — item disappears on E press, console logs pickup message

## Dev Notes

Story 4.2 builds on the look-at interaction foundation from Story 4.1. The heavy lifting (E-key detection, crosshair highlight, IInteractable dispatch) is already done. This story's job is to implement the **three new components** that the interaction system will call: an item data type (ItemSO), an inventory holder (InventorySystem), and an interactable world item (ItemPickup).

---

### CRITICAL: E-Key is Already Wired — DO NOT Duplicate It

**`InteractionSystem.LateUpdate()` already calls `CurrentInteractable.Interact()`** when the player presses E while looking at an interactable object. The implementation from Story 4.1 (added ahead of schedule):

```csharp
// InteractionSystem.cs:90–93 (DO NOT REPLICATE THIS LOGIC)
private void LateUpdate()
{
    if (CurrentInteractable != null && _input.Player.Interact.WasPressedThisFrame())
        CurrentInteractable.Interact();
}
```

`ItemPickup.Interact()` is the **only** new entry point needed. Do NOT:
- Create a second `InputSystem_Actions` instance in `ItemPickup`
- Listen for E-key events in `ItemPickup` directly
- Duplicate the `Interact.WasPressedThisFrame()` check

---

### CRITICAL: ItemPickup Uses FindFirstObjectByType (Unity 6 API)

`ItemPickup.Awake()` must find `InventorySystem` via:

```csharp
_inventory = FindFirstObjectByType<InventorySystem>();
if (_inventory == null)
{
    GameLog.Error(TAG, "InventorySystem not found in scene — ItemPickup disabled");
    enabled = false;
    return;
}
```

**Use `FindFirstObjectByType<T>()`** (Unity 6 API) — NOT the deprecated `FindObjectOfType<T>()`.
This is called once in `Awake()` and cached — not called per frame, so performance is acceptable.

---

### CRITICAL: ItemPickup.Interact() Deactivates the GO

```csharp
public void Interact()
{
    _inventory.AddItem(_item);
    gameObject.SetActive(false);
}
```

When `gameObject.SetActive(false)` is called:
- `OnDisable()` fires on all components including `ItemPickup`
- There are no `OnEnable`/`OnDisable` subscriptions in `ItemPickup` (no input, no events)
- `InteractionSystem.Update()` will detect the hit collider is gone (GO deactivated) and set `CurrentInteractable = null` on the next frame — the crosshair will return to white automatically

No PersistentID is needed for this test item (world persistence is Epic 5). The item simply disappears permanently (SetActive false) for the duration of the play session.

---

### CRITICAL: ItemPickup Must Be on "Interactable" Layer

Story 4.1 introduced **layer 8 = "Interactable"** and wired `InteractionSystem._raycastMask` to only hit this layer. Without the correct layer:
- The raycast won't detect the item
- Crosshair won't highlight
- `Interact()` will never be called

Set `TestItem_Potion` to the "Interactable" layer in the Inspector (TagManager.asset, layer 8).

---

### CRITICAL: Use GetComponentInParent for Hit Collider (ItemPickup Architecture)

`ItemPickup` should be on the **root GO** of a world item, and the collider should also be on the root GO for simple items. However, following the established pattern (see Enemy prefab gotcha, see Story 4.1), if future items have a visual child:

```csharp
// InteractionSystem.Update() — already written correctly
found = hitInfo.collider.GetComponentInParent<IInteractable>();
```

This is handled by `InteractionSystem` — `ItemPickup` doesn't need to do anything special. Just ensure `ItemPickup` is on the root GO (or any ancestor of the collider).

---

### CRITICAL: InventorySystem is NOT a Singleton

Per architecture: *"Only WorldStateManager and SaveSystem are singletons."* InventorySystem is a plain MonoBehaviour on the Player GO. `ItemPickup` finds it via `FindFirstObjectByType<InventorySystem>()` in `Awake()`.

Do NOT add a static `Instance` property or `DontDestroyOnLoad` to `InventorySystem`.

---

### ItemSO Fields Use Public (Not SerializeField Private)

`ItemSO` is a data ScriptableObject. Per project convention (same as `CombatConfigSO`, `ProgressionConfigSO`), use **public fields** directly — inspector-assignable, readable from code without getters. This is the established SO pattern:

```csharp
[CreateAssetMenu(menuName = "Items/Item", fileName = "Item_")]
public class ItemSO : ScriptableObject
{
    public string itemName;
    public string description;
    public Sprite icon;
    public bool isStackable;
}
```

---

### InventorySystem.Items Returns IReadOnlyList

Story 4.3 (inventory panel) will read `InventorySystem.Items` to populate the UI. Use `IReadOnlyList<ItemSO>` for the return type to prevent callers from mutating the backing list:

```csharp
public IReadOnlyList<ItemSO> Items => _items;
```

---

### Rigidbody on TestItem_Potion — Minimal Config

Add a `Rigidbody` to the `TestItem_Potion` GO to establish the pattern for Story 4.4 (item drop physics). Config:
- `Use Gravity: true`
- `Is Kinematic: false`
- `Constraints: Freeze Rotation X, Y, Z` — prevents items rolling away
- The `SphereCollider` keeps it above the ground

Story 4.4 will add drop spawning with physics velocity. For 4.2, the item just sits on the ground waiting to be picked up.

---

### Namespace: Game.Inventory

All new files use namespace `Game.Inventory`:
- `ItemSO.cs` — `namespace Game.Inventory`
- `InventorySystem.cs` — `namespace Game.Inventory`
- `ItemPickup.cs` — `namespace Game.Inventory`

`ItemPickup.cs` also needs `using Game.World;` to implement `IInteractable`.

---

### Data/Items Folder Already Has .meta

The glob shows `Assets/_Game/Data/Items.meta` exists — the folder is registered with Unity. Creating `Item_TestPotion.asset` inside it directly (via MCP `manage_scriptable_object`) should work without creating the folder first. Verify with Unity MCP.

---

### InventorySystem Requires No Config SO

Item pickup has no tunable values at this stage (Story 4.2 scope). A future `InventoryConfigSO` (max slots, etc.) belongs to Story 4.3+. No config SO needed here.

---

### Edit Mode Test Pattern for ScriptableObjects

`ScriptableObject.CreateInstance<T>()` works in edit-mode tests without a scene:

```csharp
using NUnit.Framework;
using Game.Inventory;
using UnityEngine;

namespace Tests.EditMode
{
    public class InventorySystemTests
    {
        private InventorySystem _inventory;

        [SetUp]
        public void SetUp()
        {
            // InventorySystem is a MonoBehaviour — create via new GameObject in edit mode
            var go = new GameObject("TestInventory");
            _inventory = go.AddComponent<InventorySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_inventory.gameObject);
        }

        private ItemSO CreateTestItem(string name = "Test Item")
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.itemName = name;
            return item;
        }
    }
}
```

Note: `new GameObject()` + `AddComponent<T>()` IS allowed in Edit Mode tests. Unity Test Framework supports this. The GameObject must be destroyed in `[TearDown]` to avoid leaks.

---

### Story 4.2 Scope Boundary

This story deliberately defers:
- **Inventory UI panel** → Story 4.3 (I key, drag/move items)
- **Item drop physics** → Story 4.4 (drop from inventory with Rigidbody velocity)
- **Full ItemSO developer story** → Story 4.5 (technically front-loaded into 4.2 since it's required, but 4.5 may extend ItemSO further)
- **TomePickup refactor** → Story 4.6
- **TrainerNPC refactor** → Story 4.7

Story 4.2 ONLY:
- `ItemSO` data class (minimal: name, desc, icon, isStackable)
- `InventorySystem` (add, expose list — no remove/drop yet)
- `ItemPickup` IInteractable implementation
- One test item asset + TestScene wiring

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Items/ItemSO.cs              ← NEW SO class
Assets/_Game/ScriptableObjects/Items/ItemSO.cs.meta
Assets/_Game/Scripts/Inventory/InventorySystem.cs           ← NEW MonoBehaviour
Assets/_Game/Scripts/Inventory/InventorySystem.cs.meta
Assets/_Game/Scripts/Inventory/ItemPickup.cs                ← NEW MonoBehaviour implementing IInteractable
Assets/_Game/Scripts/Inventory/ItemPickup.cs.meta
Assets/_Game/Data/Items/Item_TestPotion.asset               ← NEW ItemSO asset
Assets/_Game/Data/Items/Item_TestPotion.asset.meta
Assets/Tests/EditMode/InventorySystemTests.cs               ← NEW Edit Mode tests
Assets/Tests/EditMode/InventorySystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity   ← Add InventorySystem to Player GO + TestItem_Potion GO
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/World/InteractionSystem.cs      ← No changes (E-key already handles pickup)
Assets/_Game/Scripts/World/IInteractable.cs          ← No changes (interface is complete)
Assets/_Game/Scripts/World/InteractableObject.cs     ← No changes (test stub, story 4.1)
Assets/_Game/InputSystem_Actions.cs                  ← No new actions needed
Assets/_Game/InputSystem_Actions.inputactions        ← No new actions needed
Assets/_Game/Scripts/World/TomePickup.cs             ← Refactored in Story 4.6
Assets/_Game/Scripts/AI/TrainerNPC.cs                ← Refactored in Story 4.7
```

**Full call chain (Story 4.2):**
```
Player presses E while looking at TestItem_Potion
  → InteractionSystem.LateUpdate()
  → _input.Player.Interact.WasPressedThisFrame() == true
  → CurrentInteractable.Interact()   ← calls ItemPickup.Interact()
  → InventorySystem.AddItem(_item)   ← item added to list
  → gameObject.SetActive(false)      ← item disappears from world
  → Next Update: raycast misses (GO inactive)
  → CurrentInteractable = null
  → _crosshairImage.color = _defaultColor (white)
```

**Namespaces in use for new files:**
```
Game.Inventory   ← ItemSO, InventorySystem, ItemPickup
Game.World       ← IInteractable (existing, consumed by ItemPickup)
Game.Core        ← GameLog (consumed by InventorySystem, ItemPickup)
```

### References

- Epic 4 story 2 scope: [Source: _bmad-output/epics.md#Epic 4: Inventory, Items & Interaction]
- Architecture — `Scripts/Inventory/ItemPickup.cs` and `InventorySystem.cs`: [Source: _bmad-output/game-architecture.md#Project File Structure]
- Architecture — `ScriptableObjects/Items/ItemSO.cs`: [Source: _bmad-output/game-architecture.md#Project File Structure]
- Architecture — cross-system comms via GameEventSO<T>; direct refs within same folder allowed: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Story 4.1 completion — E-key already wired in InteractionSystem.LateUpdate (MUST NOT duplicate): [Source: _bmad-output/implementation-artifacts/4-1-look-at-interaction-system.md#⚠️ Story 4.2 Coordination]
- Story 4.1 completion — "Interactable" layer 8 added, _raycastMask must include it: [Source: _bmad-output/implementation-artifacts/4-1-look-at-interaction-system.md#Completion Notes List]
- project-context.md — FindObjectOfType deprecated, use FindFirstObjectByType: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — NEVER use Debug.Log, use GameLog: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — Only WorldStateManager and SaveSystem are singletons: [Source: _bmad-output/project-context.md#Architecture Patterns]
- project-context.md — Config values in SO, no magic numbers in game logic: [Source: _bmad-output/project-context.md#Architecture Patterns]
- CLAUDE.md — GetComponentInParent required for nested collider lookup (applies to InteractionSystem, already correct): [Source: CLAUDE.md#Enemy Prefab Structure]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

(none)

### Completion Notes List

- Created `ItemSO` ScriptableObject with `itemName`, `description`, `icon`, `isStackable` fields (namespace `Game.Inventory`)
- Created `InventorySystem` MonoBehaviour on Player GO: `AddItem()` with null guard, `Items` as `IReadOnlyList<ItemSO>`, `Count` property; uses `GameLog` throughout; NOT a singleton per architecture rules
- Created `ItemPickup` MonoBehaviour implementing `IInteractable`: `Awake()` null-checks `_item` and `InventorySystem` (disable on missing), `Interact()` adds item then deactivates GO; uses `FindFirstObjectByType<InventorySystem>()` (Unity 6 API)
- Created `Item_TestPotion.asset` (ItemSO): itemName="Health Potion", description="Restores a small amount of health", isStackable=true
- TestScene: `InventorySystem` added to Player GO; `TestItem_Potion` GO created at (0, 0.3, 3) with `ItemPickup` (_item wired), `SphereCollider` (r=0.3), `Rigidbody` (gravity, freeze all rotations), Layer 8 (Interactable)
- 6 new Edit Mode tests in `InventorySystemTests.cs` covering AddItem success/failure and Items list reflection
- All 123 Edit Mode tests pass (0 failures); no compilation errors; no play mode errors

### File List

- `Assets/_Game/ScriptableObjects/Items/ItemSO.cs` (NEW)
- `Assets/_Game/ScriptableObjects/Items/ItemSO.cs.meta` (NEW)
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` (NEW)
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs.meta` (NEW)
- `Assets/_Game/Scripts/Inventory/ItemPickup.cs` (NEW)
- `Assets/_Game/Scripts/Inventory/ItemPickup.cs.meta` (NEW)
- `Assets/_Game/Data/Items/Item_TestPotion.asset` (NEW)
- `Assets/_Game/Data/Items/Item_TestPotion.asset.meta` (NEW)
- `Assets/Tests/EditMode/InventorySystemTests.cs` (NEW)
- `Assets/Tests/EditMode/InventorySystemTests.cs.meta` (NEW)
- `Assets/_Game/Scenes/TestScene.unity` (MODIFIED — InventorySystem on Player + TestItem_Potion GO)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (MODIFIED — 4-2-item-pickup: review)

## Change Log

- 2026-03-13: Implemented story 4.2 — ItemSO data class, InventorySystem MonoBehaviour, ItemPickup IInteractable, Item_TestPotion asset, TestScene wiring, 6 Edit Mode tests. 123/123 tests pass.
