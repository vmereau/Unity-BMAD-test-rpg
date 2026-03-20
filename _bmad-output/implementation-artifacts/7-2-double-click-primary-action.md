# Story 7-2: Double-Click Primary Action on Inventory Slots

Status: in-progress

## Story

As a player,
I want to double-click an item in my inventory to trigger its primary action automatically,
so that I can equip weapons and armor, or use consumables and tomes, without having to open the context menu every time.

## Acceptance Criteria

### AC 1 — Primary action dispatch in `InventoryUI`

`InventoryUI.cs` extended with a new public method:

```csharp
public void PrimaryAction(int slotIndex)
```

- Bounds-check `slotIndex`; log warn and return if out of range
- Get `ItemSO item = _inventorySystem.Items[slotIndex].Item`; return if null
- Dispatch by type:
  - `item is EquipableItemSO` → `_equipmentSystem.Equip(slotIndex)` then `RefreshSlots()`
  - `item is UsableItemSO` → call existing `UseItem(slotIndex)` (handles `OnUse`, stack decrement, and `RefreshSlots()` internally — do not duplicate)
  - Base `ItemSO` (non-equippable, non-usable) → `GameLog.Info(TAG, $"No primary action for {item.itemName}")` and return
- Depends on `_equipmentSystem` added to `InventoryUI` in story 7-1 (AC 6)

---

### AC 2 — Double-click detection in `ItemSlotUI`

`ItemSlotUI.OnPointerClick` extended to detect double-click via `eventData.clickCount`:

```
Current:
  Left click → SelectSlot

After:
  Left click (clickCount == 1) → SelectSlot
  Left click (clickCount == 2) → PrimaryAction   (if Item != null)
  Right click                  → ShowContextMenu  (unchanged)
```

- `clickCount == 2` check is added **before** the single-click `SelectSlot` call — use an `if/else if` so single-click still fires on the first tap of a double-click (slot selection on first click is correct UX)
- If `Item == null` on double-click → no-op (same guard as right-click)
- No timer or manual double-click logic needed — Unity's `EventSystem` tracks `clickCount` automatically within its internal double-click threshold

---

### AC 3 — Behaviour table

| Item in slot | Double-click result |
|---|---|
| `WeaponSO` (slot empty) | Equipped to Weapon slot |
| `WeaponSO` (slot occupied) | Old weapon returned to inventory, new one equipped (handled by `EquipmentSystem.Equip`) |
| `ArmorSO` | Equipped to appropriate slot (same swap logic) |
| `PotionItemSO` | Healed; stack decremented if last use consumed it |
| `SkillItemSO` | Skill learned; item consumed if successful |
| Base `ItemSO` | No-op (info log) |
| Empty slot | No-op |

---

### AC 4 — Edit Mode tests

**`Assets/Tests/EditMode/InventoryPrimaryActionTests.cs`**:
- `PrimaryAction_WeaponSO_CallsEquip` — slot contains a `WeaponSO`; `PrimaryAction(0)` calls `EquipmentSystem.Equip(0)` (verify via mock/stub or inspector-wired test double)
- `PrimaryAction_EquipableItemSO_CallsEquip` — slot contains an `ArmorSO` (also `EquipableItemSO`); `PrimaryAction(0)` calls `EquipmentSystem.Equip(0)`, verifying dispatch uses `is EquipableItemSO` not `is WeaponSO`
- `PrimaryAction_UsableItemSO_CallsUse` — slot contains a `PotionItemSO`; `PrimaryAction(0)` triggers `OnUse`
- `PrimaryAction_BaseItemSO_IsNoOp` — slot contains base `ItemSO`; `PrimaryAction(0)` does not throw and does not call Equip or Use
- `PrimaryAction_OutOfRange_DoesNotThrow` — `PrimaryAction(-1)` and `PrimaryAction(999)` log warn, no exception

---

### AC 5 — Play Mode validation

- Double-click a weapon in inventory → weapon appears in equipment slot; item removed from inventory grid
- Double-click a weapon when weapon slot is already occupied → old weapon returns to inventory, new one equipped
- Double-click a potion → health restored, stack badge decrements (or slot clears if last)
- Double-click a tome → skill learned (or "already known" feedback), item consumed if applicable
- Double-click a base item (e.g. a key or lore item) → nothing happens, no crash
- Single-click still selects the slot and shows detail panel; context menu still opens on right-click
- No regressions from story 7-1 (context menu Equip/Unequip still functions)

## Tasks / Subtasks

- [ ] Task 1: Add/update `PrimaryAction()` in `InventoryUI.cs` (AC: 1)
  - [x] 1.1 Add `public void PrimaryAction(int slotIndex)` with type-dispatch (WeaponSO/ArmorSO → Equip, UsableItemSO → UseItem, else no-op)
  - [ ] 1.2 Update dispatch: `item is EquipableItemSO` replaces `item is WeaponSO || item is ArmorSO`
  - [ ] 1.3 Verified — compilation clean

- [x] Task 2: Extend `ItemSlotUI.OnPointerClick` (AC: 2)
  - [x] 2.1 Add `clickCount == 2` branch to left-click handler → call `_inventoryUI.PrimaryAction(SlotIndex)`
  - [x] 2.2 Verified — compilation clean, single-click selection unaffected

- [ ] Task 3: Write Edit Mode tests (AC: 4)
  - [x] 3.1 Create `Assets/Tests/EditMode/InventoryPrimaryActionTests.cs`
  - [x] 3.2 Implement 4 test methods per AC 4
  - [ ] 3.3 Add `PrimaryAction_EquipableItemSO_CallsEquip` test (ArmorSO as test subject)

- [ ] Task 4: Play Mode validation (AC: 5)
  - [ ] 4.1 Manual in-editor validation per AC 5 checklist

## Dev Notes

### Dependency on Story 7-1

`InventoryUI.PrimaryAction()` calls `_equipmentSystem.Equip(slotIndex)`. The `_equipmentSystem` field is added to `InventoryUI` in story 7-1 AC 6. **This story cannot be fully validated until 7-1 is complete and `EquipmentSystem` compiles.**

### Dependency on EquipableItemSO (Story 7-1 update)

`PrimaryAction()` dispatch uses `item is EquipableItemSO` — introduced when story 7-1 was updated to add the abstract `EquipableItemSO` base class. Do **not** revert to `item is WeaponSO || item is ArmorSO`.

---

### `clickCount` — Unity EventSystem Behaviour

Unity's `EventSystem` increments `clickCount` when consecutive clicks occur within the engine's double-click time threshold (defaults to ~0.3 s, not directly configurable). Both the single-click event (`clickCount == 1`) and the double-click event (`clickCount == 2`) are delivered as separate `OnPointerClick` calls on the same frame sequence — there is no single-click suppression. This means:

1. First tap → `clickCount == 1` → `SelectSlot` fires (slot highlight appears)
2. Second tap within threshold → `clickCount == 2` → `PrimaryAction` fires

This is intentional and correct UX: the slot becomes selected on the first tap and the action fires on the second.

---

### `UseItem` — No Duplication

`InventoryUI.UseItem(int slotIndex)` already handles the full usage path:
- `usable.OnUse(_playerTransform.gameObject)`
- `DecrementStack` if consumed
- `RefreshSlots()`

`PrimaryAction` for `UsableItemSO` must call `UseItem(slotIndex)` directly — do **not** re-implement this logic inline. This ensures both the context menu "Use" and double-click share one code path.

---

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/Scripts/UI/InventoryUI.cs     ← add PrimaryAction() method
Assets/_Game/Scripts/UI/ItemSlotUI.cs      ← extend OnPointerClick with clickCount == 2 branch
```

**Files to CREATE:**
```
Assets/Tests/EditMode/InventoryPrimaryActionTests.cs
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs        ← API used as-is from 7-1
Assets/_Game/Scripts/Inventory/InventorySystem.cs        ← unchanged
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs         ← unchanged
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs          ← unchanged
Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs  ← abstract base, unchanged
```

### References

- Story 7-1 — `EquipmentSystem.Equip()` API, `_equipmentSystem` field on `InventoryUI`
- Story 4.9 — `UsableItemSO.OnUse()`, `InventoryUI.UseItem()` existing implementation
- `Assets/_Game/Scripts/UI/CLAUDE.md` — `IPointerClickHandler` usage, `PointerEventData.InputButton`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Tasks 1–3 complete. Added `InventoryUI.PrimaryAction(int slotIndex)` with type-dispatch: WeaponSO/ArmorSO → `EquipmentSystem.Equip()` + `RefreshSlots()`, UsableItemSO → `UseItem()`, base ItemSO → info log. Extended `ItemSlotUI.OnPointerClick` left-click branch: `clickCount == 2` with non-null item triggers `PrimaryAction`; all other left-clicks fall through to `SelectSlot`. Four EditMode tests pass (187/187 total, zero regressions). Task 4 (play-mode) requires manual in-editor validation.

### File List

- `Assets/_Game/Scripts/UI/InventoryUI.cs` — added `PrimaryAction()` method
- `Assets/_Game/Scripts/UI/ItemSlotUI.cs` — extended `OnPointerClick` with `clickCount == 2` branch
- `Assets/Tests/EditMode/InventoryPrimaryActionTests.cs` — new file, 4 EditMode tests
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — status in-progress
- `_bmad-output/implementation-artifacts/7-2-double-click-primary-action.md` — story updates
