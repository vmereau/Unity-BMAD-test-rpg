# Story 4.10: Potion & Stacking System

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to pick up health potions that stack in my inventory, use them to restore health (showing a stack count badge when I have more than one), and drop them one at a time,
so that consumable items feel like real, manageable resources rather than cluttering one slot per unit.

## Acceptance Criteria

1. **`ItemSO.cs`** updated — `isStackable: bool` replaced by `maxStacks` + `IsStackable`:
   - `public bool isStackable;` removed
   - `public int maxStacks = 1;` added (serialized field; default 1 = non-stackable)
   - `public bool IsStackable => maxStacks > 1;` added (computed property)
   - No existing SO assets need manual migration — Unity silently ignores removed fields; `maxStacks` defaults to `1` on old YAML (Unity uses the code field initializer when the field is absent), meaning `IsStackable = false` (correct for tomes)

2. **`InventorySlot` struct** added to `Assets/_Game/Scripts/Inventory/InventorySystem.cs` (inside `Game.Inventory` namespace, outside the class, same file):
   ```csharp
   public readonly struct InventorySlot
   {
       public readonly ItemSO Item;
       public readonly int Count;
       public InventorySlot(ItemSO item, int count) { Item = item; Count = count; }
   }
   ```

3. **`InventorySystem.cs`** reworked internally to use slots:
   - `private readonly List<ItemSO> _items` → `private readonly List<InventorySlot> _slots`
   - `public IReadOnlyList<ItemSO> Items => _items` → `public IReadOnlyList<InventorySlot> Items => _slots`
   - `public int Count` → returns `_slots.Count`
   - `AddItem(ItemSO item)` stacking logic:
     - If `item == null`: warn + return false (unchanged)
     - If `item.IsStackable`: search `_slots` for first slot where `slot.Item == item && slot.Count < item.maxStacks`; if found, replace that slot with `new InventorySlot(item, slot.Count + 1)` and return true; otherwise fall through to append
     - Append: `_slots.Add(new InventorySlot(item, 1))` and return true
   - `RemoveItem(int index)` unchanged in semantics — removes entire slot, returns `ItemSO` (`slot.Item`)
   - `MoveItem(int from, int to)` adapted to swap `InventorySlot` values (C# tuple swap unchanged)
   - `DecrementStack(int index)` **NEW method**:
     - Bounds-check: warn and return null if out of range
     - If `_slots[index].Count > 1`: replace slot with `new InventorySlot(item, count - 1)`
     - If `_slots[index].Count == 1`: call `_slots.RemoveAt(index)` (full removal)
     - Return the `ItemSO` that was decremented
     - Log via `GameLog.Info(TAG, ...)`

4. **`PlayerHealth.cs`** updated — `Heal(float amount)` method added after `TakeDamage`:
   - `if (IsDead) return;` guard
   - `CurrentHealth = Mathf.Min(CurrentHealth + amount, _config.baseHealth);`
   - `GameLog.Info(TAG, $"Player healed {amount} HP — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");`
   - TAG is `[Combat]` (matches existing pattern in `PlayerHealth.cs`)
   - No event raised for heal (prototype scope)

5. **`PotionItemSO.cs`** created at `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs`:
   - Namespace: `Game.Inventory`
   - `[CreateAssetMenu(menuName = "Items/Potion Item", fileName = "Item_")]`
   - Extends `UsableItemSO`
   - `[SerializeField] private float _healAmount = 30f;`
   - `public float HealAmount => _healAmount;`
   - `OnUse(GameObject user) → bool`:
     - Null guard: `user.GetComponent<PlayerHealth>()` — warn + return false if null
     - Dead guard: `if (health.IsDead) return false;`
     - `health.Heal(_healAmount); return true;`
   - Uses `GameLog.Warn(TAG, ...)` for null guard; TAG constant `"[Inventory]"`

6. **`InventoryUI.cs`** updated — 6 access sites changed from `Items[i]` (ItemSO) to `Items[i].Item`:

   `DropItem(int slotIndex)`:
   ```csharp
   // OLD: var item = _inventorySystem.Items[slotIndex];  (ItemSO)
   //      _inventorySystem.RemoveItem(slotIndex);
   // NEW:
   var slot = _inventorySystem.Items[slotIndex];
   var item = slot.Item;
   _inventorySystem.DecrementStack(slotIndex);   // drops 1 unit
   ```

   `UseItem(int slotIndex)`:
   ```csharp
   // OLD: var item = _inventorySystem.Items[slotIndex];  (ItemSO)
   //      _inventorySystem.RemoveItem(slotIndex);
   // NEW:
   var slot = _inventorySystem.Items[slotIndex];
   var item = slot.Item;
   // ... (existing type check and OnUse unchanged)
   if (used && usable.consumable)
       _inventorySystem.DecrementStack(slotIndex);  // consumes 1 unit
   ```

   `ShowContextMenu(int slotIndex, ...)`:
   ```csharp
   // OLD: var item = _inventorySystem.Items[slotIndex];  (ItemSO)
   // NEW: var item = _inventorySystem.Items[slotIndex].Item;
   ```

   `SelectSlot(int slotIndex)`:
   ```csharp
   // OLD: UpdateDetailPanel(_inventorySystem.Items[slotIndex], slotIndex);
   // NEW: UpdateDetailPanel(_inventorySystem.Items[slotIndex].Item, slotIndex);
   ```

   `RefreshSlots()` (restore-selection block):
   ```csharp
   // OLD: UpdateDetailPanel(items[_selectedSlotIndex], _selectedSlotIndex);
   // NEW: UpdateDetailPanel(items[_selectedSlotIndex].Item, _selectedSlotIndex);
   ```

   `RefreshSlots()` (bind loop):
   ```csharp
   // OLD: slot.Bind(items[i], i);
   // NEW: slotUI.Bind(items[i].Item, i, items[i].Count);
   ```

7. **`ItemSlotUI.cs`** updated:
   - `[SerializeField] private TMP_Text _stackCountText;` field added
   - `Bind(ItemSO item, int index)` signature extended to `Bind(ItemSO item, int index, int stackCount = 1)`
   - Inside `Bind()`, after existing icon/name logic, add:
     ```csharp
     if (_stackCountText != null)
     {
         _stackCountText.text = stackCount.ToString();
         _stackCountText.gameObject.SetActive(stackCount > 1);
     }
     ```

8. **`ItemSlotUI.prefab`** (`Assets/_Game/Prefabs/UI/Inventory/ItemSlotUI.prefab`) updated:
   - New TMP_Text child named `"StackCountText"` added (font size 12, white, bold, upper-left anchor, `SetActive(false)` by default)
   - `ItemSlotUI._stackCountText` serialized field wired to this new child

9. **`ItemDetailPanelUI.cs`** updated — add `PotionItemSO` case to existing `switch` block:
   ```csharp
   case PotionItemSO potionItem:
       ShowUsableSection(potionItem);
       break;
   ```
   (Heal amount shown via `description` field on the asset — no new UI section required)

10. **`Item_Health_Potion.asset`** (`Assets/_Game/Data/Items/Item_Health_Potion.asset`) **updated** to `PotionItemSO` type:
    - Change `m_Script` GUID to reference `PotionItemSO.cs` (get GUID after creating the script)
    - Change `m_EditorClassIdentifier` to `Game::Game.Inventory.PotionItemSO`
    - Remove `isStackable` field (or leave — Unity ignores it)
    - Add `maxStacks: 10`
    - Add `_healAmount: 30`
    - Keep existing `itemName`, `description`, `icon`, and `worldItemPrefab` references unchanged
    - NOTE: Do **not** create a new `Item_HealthPotion.asset` — update the existing asset instead

11. **`TestItem_Health_Potion.prefab`** (`Assets/_Game/Prefabs/Items/TestItem_Health_Potion.prefab`):
    - Verify it has `ItemPickup` component with `_item` → `Item_Health_Potion.asset`
    - Verify it has `Rigidbody` + appropriate collider
    - If `ItemPickup` is missing: add it (same pattern as `Tome_PowerStrike.prefab` in story 4.9)
    - NOTE: Do **not** create a new `HealthPotion.prefab` — use the existing test prefab

12. **Edit Mode tests** added to `Assets/Tests/EditMode/InventorySystemTests.cs`:
    - `AddItem` stacking: two stackable items → one slot with Count = 2
    - `AddItem` stacking: stackable item added beyond `maxStacks` → new separate slot (Count = 1)
    - `AddItem` non-stackable: two identical non-stackable items → two slots (Count = 1 each)
    - `DecrementStack` Count > 1: `Count` decrements by 1, slot remains
    - `DecrementStack` Count == 1: slot is fully removed from inventory
    - `DecrementStack` out-of-range: logs warn, returns null, no crash

13. **Edit Mode tests** added to `Assets/Tests/EditMode/HealthSystemTests.cs`:
    - `Heal`: normal heal increases `CurrentHealth` correctly
    - `Heal`: does not exceed `_config.baseHealth` (no overhealing)
    - `Heal`: dead player (`IsDead = true`) — health unchanged
    - `PotionItemSO.OnUse`: null `PlayerHealth` component → logs warn, returns false
    - `PotionItemSO.OnUse`: dead player → returns false, no heal

14. **Play Mode validation**:
    - Pick up 3 `TestItem_Health_Potion` world items → single inventory slot showing badge "3"
    - Use potion with stack 3 → badge shows "2", player HP increased by 30 (capped at baseHealth)
    - Use potion with stack 1 → slot removed from inventory
    - Drop stacked potion (count > 1) → count decrements, one world item spawns at player's feet
    - Non-stackable item (tome): no badge shown, one slot per item, behavior unchanged
    - `PlayerHealth.Heal()` when at full health → no change (no overhealing)
    - All 137+ Edit Mode tests pass (no regressions)

## Tasks / Subtasks

- [x] Task 1: Update `ItemSO.cs` (AC: 1)
  - [x] 1.1 Replace `public bool isStackable;` with `public int maxStacks = 1;` + `public bool IsStackable => maxStacks > 1;`
  - [x] 1.2 Verify compilation — no errors, no warnings

- [x] Task 2: Update `InventorySystem.cs` (AC: 2, 3)
  - [x] 2.1 Add `InventorySlot` readonly struct before the class definition (within namespace)
  - [x] 2.2 Replace `List<ItemSO> _items` with `List<InventorySlot> _slots`; update `Items` property return type
  - [x] 2.3 Update `Count` property to return `_slots.Count`
  - [x] 2.4 Rewrite `AddItem()` — stacking branch for `IsStackable`, fall-through to append `InventorySlot(item, 1)`
  - [x] 2.5 Update `RemoveItem()` — remove entire slot, return `slot.Item`
  - [x] 2.6 Update `MoveItem()` — tuple swap works the same with struct values
  - [x] 2.7 Add `DecrementStack(int index)` — decrement count or remove slot, return `ItemSO`
  - [x] 2.8 Verify all compilation errors resolved

- [x] Task 3: Add `Heal()` to `PlayerHealth.cs` (AC: 4)
  - [x] 3.1 Add `Heal(float amount)` after `TakeDamage()` with dead guard + `Mathf.Min` cap + log
  - [x] 3.2 Verify compilation

- [x] Task 4: Create `PotionItemSO.cs` (AC: 5)
  - [x] 4.1 Create `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` extending `UsableItemSO`
  - [x] 4.2 Add `[CreateAssetMenu(menuName = "Items/Potion Item")]`
  - [x] 4.3 Implement `OnUse(GameObject user)` — null guard for `PlayerHealth`, dead guard, call `Heal`, return true
  - [x] 4.4 Verify `CreateAssetMenu` appears in editor under `Assets > Create > Items > Potion Item`

- [x] Task 5: Update `InventoryUI.cs` (AC: 6)
  - [x] 5.1 Fix `DropItem()` — add `var slot = ...Items[slotIndex]; var item = slot.Item;`, change `RemoveItem` → `DecrementStack`
  - [x] 5.2 Fix `UseItem()` — same slot pattern, change `RemoveItem` → `DecrementStack`
  - [x] 5.3 Fix `ShowContextMenu()` — `Items[slotIndex].Item` access
  - [x] 5.4 Fix `SelectSlot()` → `UpdateDetailPanel(Items[slotIndex].Item, slotIndex)`
  - [x] 5.5 Fix `RefreshSlots()` restore-selection block — `items[_selectedSlotIndex].Item`
  - [x] 5.6 Fix `RefreshSlots()` bind loop — `slotUI.Bind(items[i].Item, i, items[i].Count)`
  - [x] 5.7 Verify all compilation errors resolved

- [x] Task 6: Update `ItemSlotUI.cs` and prefab (AC: 7, 8)
  - [x] 6.1 Add `[SerializeField] private TMP_Text _stackCountText;` field
  - [x] 6.2 Extend `Bind()` signature to `Bind(ItemSO item, int index, int stackCount = 1)`
  - [x] 6.3 Add badge activation logic inside `Bind()`
  - [x] 6.4 Add `"StackCountText"` TMP child to `ItemSlot.prefab` (upper-left anchor, size 12, white, `SetActive(false)`)
  - [x] 6.5 Wire `_stackCountText` reference in prefab inspector

- [x] Task 7: Update `ItemDetailPanelUI.cs` (AC: 9)
  - [x] 7.1 Add `case PotionItemSO potionItem: ShowUsableSection(potionItem); break;` to switch block

- [x] Task 8: Update `Item_Health_Potion.asset` to `PotionItemSO` type (AC: 10)
  - [x] 8.1 After creating `PotionItemSO.cs`, found its GUID in `PotionItemSO.cs.meta`: `991ec0725183bca4eb0e6f6e4aff6645`
  - [x] 8.2 Edit asset YAML: updated `m_Script` fileID/guid, `m_EditorClassIdentifier`, added `maxStacks: 10`, `_healAmount: 30`, `consumable: 1`
  - [x] 8.3 Verify asset reloads correctly in Unity with proper type and all fields visible

- [x] Task 9: Verify `TestItem_Health_Potion.prefab` (AC: 11)
  - [x] 9.1 Confirmed `ItemPickup` component exists with `_item` → `Item_Health_Potion.asset` (guid `e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0`)
  - [x] 9.2 Confirmed `Rigidbody` + `CapsuleCollider` exist; Layer 8 ✓
  - [x] 9.3 No changes needed — prefab was already correctly configured

- [x] Task 10: Write Edit Mode tests (AC: 12, 13)
  - [x] 10.1 `InventorySystemTests.cs` — 10 new tests: 6 stacking/decrement + `PotionItemSO_OnUse_NoPlayerHealthComponent_ReturnsFalse` + `PotionItemSO_OnUse_DeadPlayer_ReturnsFalse` + 2 `InventoryUI` null-guard tests
  - [x] 10.2 `HealthSystemTests.cs` — 7 new heal formula tests: `ApplyHeal_*` variants + `ApplyHeal_DeadPlayer_HealthUnchanged` + `HealAndDamage_Sequence_CorrectHealth`
  - [ ] 10.3 Run all tests — verify 0 regressions (pending Unity Editor run)

- [ ] Task 11: Play Mode validation (AC: 14)
  - [ ] 11.1 Verify 3 potions → 1 slot with badge "3"
  - [ ] 11.2 Verify use → count decrements + HP restored
  - [ ] 11.3 Verify use last → slot removed
  - [ ] 11.4 Verify drop → count decrements, world item spawns
  - [ ] 11.5 Verify tome still works as non-stackable (no badge)

## Dev Notes

Story 4.10 delivers the stacking infrastructure for all future stackable items (Epic 7: arrows, materials; Epic 8: crafting reagents). The primary change is the `InventorySystem.Items` API type change from `IReadOnlyList<ItemSO>` to `IReadOnlyList<InventorySlot>`. At this time, `InventoryUI` is the only consumer — all 6 access sites are listed in AC 6 above and must all be updated.

---

### CRITICAL: `InventorySlot` Is a Readonly Struct — Mutate via Replacement

`InventorySlot` is a `readonly struct`. You **cannot** modify `Count` in-place. The only correct pattern for incrementing/decrementing is to **replace** the slot in the list:

```csharp
// Increment (inside AddItem stacking branch):
_slots[existingIndex] = new InventorySlot(item, _slots[existingIndex].Count + 1);

// Decrement (inside DecrementStack):
var existing = _slots[index];
if (existing.Count > 1)
    _slots[index] = new InventorySlot(existing.Item, existing.Count - 1);
else
    _slots.RemoveAt(index);
```

---

### CRITICAL: `DecrementStack` vs `RemoveItem` — Semantics

| Method | Semantics | Used by |
|--------|-----------|---------|
| `RemoveItem(int index)` | Removes entire slot regardless of count | Reserved for future "remove all" use (Epic 7 sell-all, loot all) |
| `DecrementStack(int index)` | Removes one unit; slot remains if Count > 1 | `InventoryUI.UseItem()` and `InventoryUI.DropItem()` |

**After this story:** `DropItem` and `UseItem` in `InventoryUI` call `DecrementStack` — they never call `RemoveItem` directly for stackable items. `RemoveItem` is still valid for non-stackable items (Count always = 1, so decrement is identical), but `DecrementStack` is the preferred approach for both paths to keep the code symmetric.

---

### CRITICAL: `InventoryUI.DropItem()` — Spawn After Decrement

The current `DropItem()` calls `RemoveItem` *before* spawning the world item. After this story, the order must be:

```csharp
var slot = _inventorySystem.Items[slotIndex];  // capture BEFORE decrement
var item = slot.Item;
if (item.worldItemPrefab == null) { ... return; }

_inventorySystem.DecrementStack(slotIndex);

var dropPos = _playerTransform.position + ...;
var go = Instantiate(item.worldItemPrefab, dropPos, Quaternion.identity);
go.GetComponent<Rigidbody>().AddForce(...);
RefreshSlots();
```

Capture the slot **before** calling `DecrementStack` — after decrement the index may become invalid if the slot was removed.

---

### CRITICAL: Stacking Identity — Reference Equality for `ItemSO`

The stacking search in `AddItem` uses `slot.Item == item` (reference equality). This works correctly because `ItemSO` assets are ScriptableObjects — two world potions from the same asset share the same `ItemSO` reference. There is no need for a custom `Equals` override.

---

### CRITICAL: `Item_Health_Potion.asset` Already Exists — Update, Don't Create New

`Assets/_Game/Data/Items/Item_Health_Potion.asset` already exists as type `ItemSO` with:
- `itemName`: `"Health Potion"`
- `description`: `"Restores a small amount of health"` (update to `"Restores 30 HP when consumed."`)
- `icon`: `health potion.png` (already assigned — keep it)
- `worldItemPrefab`: `TestItem_Health_Potion.prefab` (GUID `3369ecd03d9c07142ba5e2cbca261f2b`) — keep this reference
- `isStackable: 1` (obsolete field — will be ignored by Unity after ItemSO update)

After creating `PotionItemSO.cs`, update the YAML directly:
1. Copy `PotionItemSO.cs.meta` GUID (e.g. `abcd1234...`)
2. In the asset YAML, change `m_Script: {fileID: 11500000, guid: <ItemSO_guid>, type: 3}` to use the `PotionItemSO` GUID
3. Change `m_EditorClassIdentifier: Game::Game.Inventory.ItemSO` → `Game::Game.Inventory.PotionItemSO`
4. Add `maxStacks: 10` and `_healAmount: 30`
5. Leave `consumable: 1` (inherited from `UsableItemSO`) — add if not present

Do **not** create a parallel `Item_HealthPotion.asset`. The sprint-change-proposal's "NEW" designation assumes the old asset doesn't exist.

---

### CRITICAL: `TestItem_Health_Potion.prefab` May Lack `ItemPickup`

The prefab at `Assets/_Game/Prefabs/Items/TestItem_Health_Potion.prefab` was created as a test asset. Verify it has `ItemPickup` with `_item` → `Item_Health_Potion.asset`. If missing, add it using the same YAML-edit pattern as story 4.9 (which added `ItemPickup` to `Tome_PowerStrike.prefab`):

```yaml
# Add to m_Component list of root GO:
- component: {fileID: <new_component_fileID>}

# New MonoBehaviour section:
--- !u!114 &<new_component_fileID>
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: <ItemPickup_cs_guid>, type: 3}
  _item: {fileID: 11400000, guid: <Item_Health_Potion_asset_guid>, type: 2}
```

---

### CRITICAL: `ItemSlotUI.Bind()` Default Parameter — Backward Compatibility

`Bind(ItemSO item, int index, int stackCount = 1)` — the default `stackCount = 1` means existing callers (none in `InventoryUI.cs` after AC 6 updates, but possible in tests) compile without change. The badge is hidden when `stackCount <= 1`, so non-stackable items are unaffected.

---

### `ItemDetailPanelUI.cs` — Where to Find the Switch Block

`ItemDetailPanelUI.cs` is at `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`. The `switch` block (introduced in story 4.8, extended in story 4.9) currently looks like:

```csharp
switch (item)
{
    case SkillItemSO skillItem:
        ShowUsableSection(skillItem);
        ShowSkillSection(skillItem.Skill);
        break;
    default:
        HideUsableSection();
        HideSkillSection();
        break;
}
```

Add the `PotionItemSO` case **before** `default`:
```csharp
case PotionItemSO potionItem:
    ShowUsableSection(potionItem);
    break;
```

---

### `PlayerHealth.cs` Tag and Config Reference

- The `TAG` constant in `PlayerHealth.cs` is `"[Combat]"` — use this same tag in `Heal()`
- Max health cap uses `_config.baseHealth` (`CombatConfigSO` field) — this is the same config used in `TakeDamage()`
- `Heal()` does **not** set `IsDead = false` — healing a dead player is guarded with early return

---

### Test Infrastructure — Existing Patterns

`InventorySystemTests.cs` currently has 137 passing tests. The test class uses `ScriptableObject.CreateInstance<T>()` to create SO instances in Edit Mode without needing the full Unity runtime.

Pattern for testing `InventorySystem.AddItem` stacking:
```csharp
var item = ScriptableObject.CreateInstance<PotionItemSO>();
// Use reflection or a setter to set maxStacks = 10 (or expose for tests via internal/protected)
// NOTE: ItemSO.maxStacks is a public field — set directly: item.maxStacks = 10;
```

Pattern for testing `PlayerHealth.Heal()`:
```csharp
// CombatConfigSO must be assigned in Awake — create via ScriptableObject.CreateInstance
// then set baseHealth field directly (it's a public field)
```

Check `HealthSystemTests.cs` for the existing `TakeDamage` test setup pattern before adding new tests.

---

### Namespace Requirements

| Class | Namespace |
|-------|-----------|
| `PotionItemSO` | `Game.Inventory` |
| `InventorySlot` struct | `Game.Inventory` (in `InventorySystem.cs`) |
| `PlayerHealth.Heal()` | `Game.Player` (no change, method is in same class) |

---

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Items/ItemSO.cs              ← isStackable→maxStacks+IsStackable
Assets/_Game/Scripts/Inventory/InventorySystem.cs            ← InventorySlot struct + stacking + DecrementStack
Assets/_Game/Scripts/Player/PlayerHealth.cs                  ← Heal() method added
Assets/_Game/Scripts/UI/InventoryUI.cs                       ← 6 access sites updated + DecrementStack calls
Assets/_Game/Scripts/UI/ItemSlotUI.cs                        ← Bind() + _stackCountText
Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs                 ← PotionItemSO case in switch
Assets/_Game/Prefabs/UI/Inventory/ItemSlotUI.prefab          ← StackCountText child + wire reference
Assets/_Game/Data/Items/Item_Health_Potion.asset             ← Re-type to PotionItemSO, add maxStacks+_healAmount
Assets/Tests/EditMode/InventorySystemTests.cs                ← 6 new stacking/decrement tests
Assets/Tests/EditMode/HealthSystemTests.cs                   ← 5 new Heal/PotionItemSO tests
_bmad-output/implementation-artifacts/sprint-status.yaml     ← 4-10 status update
```

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs
```

**Files NOT to modify:**
```
Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs         ← abstract base unchanged
Assets/_Game/ScriptableObjects/Items/SkillItemSO.cs          ← no changes (OnUse return bool already correct)
Assets/_Game/Data/Items/Item_Tome_PowerStrike.asset          ← isStackable field ignored by Unity
Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab← no changes
Assets/_Game/Scripts/Player/PlayerSkills.cs                  ← no changes
Assets/_Game/Scripts/World/InteractionSystem.cs              ← no changes
```

**Files to VERIFY (no expected changes but confirm state):**
```
Assets/_Game/Prefabs/Items/TestItem_Health_Potion.prefab     ← must have ItemPickup + Rigidbody
```

### References

- Sprint Change Proposal: `_bmad-output/sprint-change-proposal-2026-03-18.md` — full rationale, code snippets, and impact analysis
- Story 4.9 — `UsableItemSO`, `SkillItemSO`, `InventoryUI.UseItem()`, `InventoryContextMenu.prefab` pattern: `_bmad-output/implementation-artifacts/4-9-usable-item-system.md`
- Story 4.5 — `ItemSO` base class structure (implemented inline during story 4.4): `_bmad-output/implementation-artifacts/4-5-item-scriptable-object`
- Story 4.3 — `InventorySystem.cs` initial API: `_bmad-output/implementation-artifacts/4-3-inventory-panel.md`
- Story 2.9 — `PlayerHealth.cs` initial implementation and `CombatConfigSO.baseHealth`: `_bmad-output/implementation-artifacts/2-9-health-system.md`
- Architecture — `ItemSO` hierarchy and Inventory system location: `_bmad-output/game-architecture.md` §Project Structure
- `project-context.md` — Logging rules (GameLog mandatory, TAG constant per class), null-guard pattern, namespace conventions

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None — no runtime errors encountered during implementation. All code changes follow established patterns from stories 4.8 and 4.9.

### Completion Notes List

- `ItemSO.cs`: Replaced `isStackable: bool` with `maxStacks: int` (default 1) + computed `IsStackable` property. Old SO assets with `isStackable` field silently ignored by Unity; `maxStacks` defaults to 0 on old YAML (IsStackable = false, correct).
- `InventorySystem.cs`: Full internal rewrite to `List<InventorySlot>`. `InventorySlot` is a `readonly struct` — all mutations use index-replacement pattern (`_slots[i] = new InventorySlot(...)`). `AddItem()` uses reference equality (`slot.Item == item`) for stacking identity — correct for ScriptableObject assets. Added `DecrementStack()` for single-unit consumption.
- `PlayerHealth.cs`: Added `Heal(float amount)` with dead guard and `Mathf.Min` cap against `_config.baseHealth`. Uses existing `[Combat]` TAG. No heal event raised (prototype scope).
- `PotionItemSO.cs`: New class in `Game.Inventory` namespace, extends `UsableItemSO`. `OnUse()` null-guards `PlayerHealth` component and dead state. GUID assigned by Unity: `991ec0725183bca4eb0e6f6e4aff6645`.
- `InventoryUI.cs`: All 6 `Items[i]` access sites updated. `DropItem` and `UseItem` now capture slot before `DecrementStack` call (critical: index may become invalid after slot removal).
- `ItemSlotUI.cs`: `Bind()` extended with optional `stackCount = 1` parameter. Badge hidden when `stackCount <= 1` — non-stackable items unaffected.
- `ItemSlot.prefab` (not `ItemSlotUI.prefab`): Added `StackCountText` TMP child via direct YAML. Upper-left anchor, 28×18px, font size 12, bold, white, inactive by default. `_stackCountText` wired in `ItemSlotUI` component.
- `Item_Health_Potion.asset`: Updated `m_Script` GUID to PotionItemSO, updated `m_EditorClassIdentifier`, added `maxStacks: 10`, `_healAmount: 30`, `consumable: 1`. Existing `icon` and `worldItemPrefab` references preserved.
- `TestItem_Health_Potion.prefab`: Already correctly configured — no changes needed.
- Tests: `InventorySystemTests.cs` updated (existing `.Items[i]` test fixes + 9 new tests). `HealthSystemTests.cs` updated (6 new heal formula tests). Tests not yet run in Unity Editor.

### File List

**Modified:**
- `Assets/_Game/ScriptableObjects/Items/ItemSO.cs`
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs`
- `Assets/_Game/Scripts/Player/PlayerHealth.cs`
- `Assets/_Game/Scripts/UI/InventoryUI.cs`
- `Assets/_Game/Scripts/UI/ItemSlotUI.cs`
- `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`
- `Assets/_Game/Prefabs/UI/Inventory/ItemSlot.prefab`
- `Assets/_Game/Data/Items/Item_Health_Potion.asset`
- `Assets/Tests/EditMode/InventorySystemTests.cs`
- `Assets/Tests/EditMode/HealthSystemTests.cs`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Created:**
- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs`
- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs.meta`

**Verified (no changes):**
- `Assets/_Game/Prefabs/Items/TestItem_Health_Potion.prefab`
