# Sprint Change Proposal — 2026-03-20

**Project:** Unity-BMAD-test-rpg
**Author:** Valentin (via Correct Course workflow)
**Date:** 2026-03-20
**Scope Classification:** Minor — direct implementation by development team

---

## Section 1: Issue Summary

### Problem Statement

Story 7-1 (Equipment Slots, status: done) and story 7-2 (Double-Click Primary Action, status: in-progress) require three refinements identified during review:

1. **Equipment slot single-click has no UX action.** `EquipmentSlotUI.OnButtonClicked()` only primes a double-click timer — a single click on an occupied slot does nothing. This is inconsistent with `InventoryPanel`, where single-click selects an item and displays its details in `ItemDetailPanel`.

2. **No shared equippable abstraction.** `WeaponSO` and `ArmorSO` both extend `ItemSO` directly. All equippability checks (`EquipmentSystem.IsEquippable()`, `PrimaryAction()` dispatch, future stories 7-3/7-4) repeat `item is WeaponSO || item is ArmorSO`. Adding a third equippable type in future would require touching every check site.

3. **Missing "Equip" action buttons.** `ItemDetailPanel`'s ActionWrapper has no "Equip" button. The `InventoryContextMenu` condition already delegates to `IsEquippable()` but no separate equip button exists in the detail panel, leaving no way to equip from the detail view.

### Discovery Context

Identified during post-implementation review of story 7-1 and while story 7-2 tasks 1–3 were already complete (task 4 play-mode validation still pending).

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 7** (in-progress): No stories invalidated. Two stories require updates:
  - **7-1** (done → reopen to in-progress): Add `EquipableItemSO`, update `WeaponSO`/`ArmorSO`, fix single-click, add Equip button to `ItemDetailPanel`
  - **7-2** (in-progress): Update `PrimaryAction()` type dispatch; add one test
- **Epics 8–9** (backlog): Unaffected
- **Future stories 7-3, 7-4**: Will benefit — `EquipableItemSO` abstraction reduces future type-check sprawl

### Story Impact
| Story | Current Status | Action Required |
|-------|---------------|-----------------|
| 7-1 | done | Reopen → in-progress; apply all 9 proposals |
| 7-2 | in-progress | Update AC 1 type dispatch; add 1 test; update Dev Notes |
| 7-3+ | backlog | No immediate change; use `EquipableItemSO` when authored |

### Artifact Conflicts
- **Story files:** 7-1 and 7-2 need edits (detailed in Section 4)
- **Sprint status:** `7-1-equipment-slots` reverts from `done` → `in-progress`
- **Architecture doc:** No conflict; `EquipableItemSO` fits existing item hierarchy pattern
- **GDD / PRD:** No conflict; this is a UX and architecture refinement

### Technical Impact
- New file: `EquipableItemSO.cs` (abstract SO, ~15 lines)
- Modified files: `WeaponSO.cs`, `ArmorSO.cs`, `EquipmentSystem.cs`, `EquipmentSlotUI.cs`, `EquipmentUI.cs`, `ItemDetailPanelUI.cs`, `InventoryUI.cs` (context menu condition inherits automatically)
- Test additions: 1 new test in `InventoryPrimaryActionTests.cs`
- Scene/prefab wiring: `EquipmentUI._itemDetailPanel` and `ItemDetailPanelUI._equipButton`, `_equipmentSystem`, `_inventorySystem` references need Inspector wiring

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** (selected)

Modify stories 7-1 and 7-2 in-place. No rollback, no MVP scope change.

- **Effort:** Low — `EquipableItemSO` is ~15 lines; remaining changes are targeted edits to existing methods
- **Risk:** Low — changes are additive (new abstract class, new button, new single-click path); existing double-click unequip and context menu equip paths are unaffected
- **Timeline impact:** Minimal — story 7-2 task 4 (play-mode validation) hasn't been done yet, so implementation can happen before that validation step; only one story needs reopening

**Rationale over alternatives:**
- Rollback not needed — the implemented code is correct, just needs targeted additions
- MVP review not needed — this is a UX improvement and architecture hygiene fix, not a scope change

---

## Section 4: Detailed Change Proposals

### 4.1 — Story 7-1, AC 1: Add `EquipableItemSO` abstract class

**File:** `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` (new)
**Files modified:** `WeaponSO.cs`, `ArmorSO.cs`

OLD:
```
WeaponSO extends ItemSO
ArmorSO extends ItemSO
```

NEW:
```
EquipableItemSO (abstract) extends ItemSO
  - public abstract bool CanEquip()  ← placeholder, always true in this story

WeaponSO extends EquipableItemSO
  - public override bool CanEquip() => true

ArmorSO extends EquipableItemSO
  - public override bool CanEquip() => true
```

Rationale: Single equippable abstraction eliminates type-check sprawl across the codebase.

---

### 4.2 — Story 7-1, AC 2: Update `EquipmentSystem` to use `EquipableItemSO`

**File:** `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs`

OLD:
```csharp
public bool IsEquippable(ItemSO item) => item is WeaponSO || item is ArmorSO;
// Equip() guard: checks WeaponSO then ArmorSO, else logs warn
```

NEW:
```csharp
public bool IsEquippable(ItemSO item) => item is EquipableItemSO;
// Equip() guard: if (item is not EquipableItemSO) → log warn and return
// Slot resolution still branches WeaponSO / ArmorSO concretely (unchanged)
```

Rationale: `IsEquippable()` now covers any future `EquipableItemSO` subclass automatically.

---

### 4.3 — Story 7-1, AC 4: `EquipmentSlotUI` single-click + `EquipmentUI` wiring

**Files:** `EquipmentSlotUI.cs`, `EquipmentUI.cs`

OLD (`EquipmentSlotUI.OnButtonClicked`):
```csharp
private void OnButtonClicked() {
    if (Time.unscaledTime - _lastClickTime <= DoubleClickThreshold)
        _equipmentUI.OnSlotDoubleClicked(Slot);
    _lastClickTime = Time.unscaledTime;
}
```

NEW (`EquipmentSlotUI`):
```csharp
private ItemSO _currentItem;  // stored in Bind()

private void OnButtonClicked() {
    if (Time.unscaledTime - _lastClickTime <= DoubleClickThreshold)
        _equipmentUI.OnSlotDoubleClicked(Slot);
    else if (_currentItem != null)
        _equipmentUI.OnSlotClicked(Slot, _currentItem);
    _lastClickTime = Time.unscaledTime;
}
```

OLD (`EquipmentUI`):
```csharp
// No single-click handler
public void OnSlotDoubleClicked(EquipmentSlot slot) => _equipmentSystem.Unequip(slot);
```

NEW (`EquipmentUI`):
```csharp
[SerializeField] private ItemDetailPanelUI _itemDetailPanel;

public void OnSlotDoubleClicked(EquipmentSlot slot) => _equipmentSystem.Unequip(slot);  // unchanged
public void OnSlotClicked(EquipmentSlot slot, ItemSO item) => _itemDetailPanel?.Show(item);
```

Rationale: Single-click on equipped item shows details in `ItemDetailPanel`, matching `InventoryPanel` UX.

---

### 4.4 — Story 7-1, AC 7: `ItemDetailPanelUI` "Equip" button

**File:** `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`

OLD:
```
ActionWrapper has no "Equip" button.
Show() handles WeaponSO and ArmorSO type labels only.
```

NEW:
```csharp
[SerializeField] private Button _equipButton;
[SerializeField] private EquipmentSystem _equipmentSystem;
[SerializeField] private InventorySystem _inventorySystem;  // if not already present

// In Show(ItemSO item), after type switch:
if (_equipButton != null) {
    _equipButton.gameObject.SetActive(true);
    _equipButton.interactable = item is EquipableItemSO;
    _equipButton.onClick.RemoveAllListeners();
    _equipButton.onClick.AddListener(() => OnEquipClicked(item));
}

private void OnEquipClicked(ItemSO item) {
    int index = _inventorySystem.Items.FindIndex(s => s.Item == item);
    if (index >= 0) _equipmentSystem.Equip(index);
    else GameLog.Warn(TAG, "Item not found in inventory for equip");
}
```

Prefab: Add "Equip" Button to `ItemDetailPanel` ActionWrapper. Wire `_equipButton`, `_equipmentSystem`, `_inventorySystem` in scene Inspector.

Rationale: Equip action accessible from detail view; button disabled (not hidden) for non-equippable items, consistent with other ActionWrapper buttons.

---

### 4.5 — Story 7-1, AC 6: Context menu condition (no code change)

The `InventoryUI` context menu condition `_equipmentSystem.IsEquippable(item)` already delegates to `EquipmentSystem.IsEquippable()`. After Proposal 4.2, that method returns `item is EquipableItemSO` — no call-site change needed. Condition automatically widens to cover all `EquipableItemSO` subclasses.

---

### 4.6 — Story 7-1: Task list, Dev Notes, Project Structure

- Reopen tasks 1, 2, 3, 6 with new subtasks (see story file edits in Section 5)
- Add Dev Note: `EquipableItemSO` hierarchy diagram and `CanEquip()` intent
- Add `EquipableItemSO.cs` to **Files to CREATE**
- Add `WeaponSO.cs`, `ArmorSO.cs`, `EquipmentSystem.cs`, `EquipmentSlotUI.cs`, `EquipmentUI.cs`, `ItemDetailPanelUI.cs` to **Files to MODIFY**

---

### 4.7 — Story 7-2, AC 1: `PrimaryAction()` type dispatch

**File:** `Assets/_Game/Scripts/UI/InventoryUI.cs`

OLD:
```csharp
if (item is WeaponSO || item is ArmorSO) → _equipmentSystem.Equip(slotIndex)
```

NEW:
```csharp
if (item is EquipableItemSO) → _equipmentSystem.Equip(slotIndex)
```

---

### 4.8 — Story 7-2, AC 4: Add `PrimaryAction_EquipableItemSO_CallsEquip` test

**File:** `Assets/Tests/EditMode/InventoryPrimaryActionTests.cs`

Add test:
```
PrimaryAction_EquipableItemSO_CallsEquip — slot contains ArmorSO (also EquipableItemSO);
PrimaryAction(0) calls EquipmentSystem.Equip(0), verifying dispatch uses is EquipableItemSO
not is WeaponSO.
```

Existing `PrimaryAction_WeaponSO_CallsEquip` retained.

---

### 4.9 — Story 7-2: Dev Notes, Project Structure, status header

- Add `EquipableItemSO.cs` to **Files NOT to modify**
- Add Dev Note documenting `EquipableItemSO` dependency
- Status header: `ready-for-dev` → `in-progress` (align with sprint-status.yaml)

---

## Section 5: Implementation Handoff

### Change Scope: Minor

All changes are direct implementation tasks. No backlog reorganization or strategic replan needed.

### Sprint Status Updates Required

```yaml
7-1-equipment-slots: done → in-progress
# (reverts to in-progress; re-completes after implementation)
```

### Handoff: Development Team

**Story 7-1 — reopen and implement:**
1. Create `EquipableItemSO.cs` (abstract, `CanEquip()`)
2. Update `WeaponSO.cs` and `ArmorSO.cs` to extend `EquipableItemSO`
3. Update `EquipmentSystem.IsEquippable()` and `Equip()` guard
4. Update `EquipmentSlotUI` — store `_currentItem` in `Bind()`, add single-click path in `OnButtonClicked()`
5. Update `EquipmentUI` — add `_itemDetailPanel` ref, add `OnSlotClicked()`
6. Update `ItemDetailPanelUI` — add `_equipButton`, `_equipmentSystem`, `_inventorySystem`; wire Equip button in `Show()`
7. Wire all new Inspector references in scene
8. Verify compilation + 183 existing tests still pass

**Story 7-2 — patch in-progress implementation:**
1. Update `PrimaryAction()` dispatch: `is EquipableItemSO`
2. Add `PrimaryAction_EquipableItemSO_CallsEquip` test
3. Proceed to task 4 (play-mode validation) once 7-1 changes compile

### Success Criteria

- All existing 187 EditMode tests pass (no regressions)
- New `PrimaryAction_EquipableItemSO_CallsEquip` test passes
- Single-click on occupied equipment slot shows item details in `ItemDetailPanel`
- Double-click on occupied equipment slot still unequips (unchanged)
- "Equip" button visible in `ItemDetailPanel` ActionWrapper; disabled for non-`EquipableItemSO` items
- `EquipmentSystem.IsEquippable()` returns true for `WeaponSO`, `ArmorSO`, and any future `EquipableItemSO` subclass
