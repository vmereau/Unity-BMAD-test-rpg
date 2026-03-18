# Sprint Change Proposal — 2026-03-18

**Project:** Unity-BMAD-test-rpg
**Date:** 2026-03-18
**Scope Classification:** Minor — direct implementation by development team
**Status:** Approved

---

## Section 1: Issue Summary

### Problem Statement

After completing story 4.9 (Usable Item System), two closely related features are needed to continue the inventory system:

1. **Health Potion** — a consumable item that restores player HP. `PlayerHealth` currently has no `Heal()` method and no `UsableItemSO` subtype exists for stat-affecting consumables.
2. **Stacking System** — `ItemSO.isStackable` is a plain `bool` field that has never been enforced by `InventorySystem` (noted as "Reserved" in CLAUDE.md). The intent was always to implement real stacking; this change delivers it:
   - `isStackable` bool replaced by `maxStacks: int` with `IsStackable` as a computed property
   - `InventorySystem` tracks stack counts (multiple potions occupy one slot)
   - `ItemSlotUI` displays a stack count badge when count > 1
   - `UseItem` and `DropItem` consume/drop one unit at a time for stackable items

### Discovery Context

Identified as a natural follow-on after story 4.9 was completed on 2026-03-17. No bug — this is an incoming feature request to extend the item/inventory system before starting Epic 5.

### Evidence

- `ItemSO.cs` line 11: `public bool isStackable;` — no computed logic, no runtime enforcement
- `InventorySystem.cs` line 11: `private readonly List<ItemSO> _items` — flat list, no count tracking
- `PlayerHealth.cs` — only `TakeDamage()` exists; no `Heal()` method
- `UsableItemSO.cs` — abstract base exists; `PotionItemSO` is a natural second subtype alongside `SkillItemSO`
- `Assets/_Game/ScriptableObjects/Items/CLAUDE.md`: `isStackable | bool | Reserved — not yet enforced by InventorySystem`

---

## Section 2: Impact Analysis

### Epic Impact

| Epic | Impact |
|------|--------|
| **Epic 4** (Inventory, Items & Interaction) — `in-progress` | Add story **4.10**. All other stories remain `done`. Retrospective still `optional`. |
| **Epic 7** (Equipment & Economy) — `backlog` | `InventorySystem.Items` type changes from `IReadOnlyList<ItemSO>` to `IReadOnlyList<InventorySlot>`. Stories 7.3 (shop) and 7.4 (looting) must account for this when implemented. No immediate changes needed. |
| **Epic 8** (Crafting) — `backlog` | Craftable consumables will naturally use the stack system. No changes now. |
| All others | No impact. |

### Story Impact

| Story | Impact |
|-------|--------|
| 4.9 — Usable Item System | Completed; no changes. `UseItem()` logic extended for stacking but existing `SkillItemSO` path is unaffected (tomes are non-stackable). |
| **4.10 — Potion & Stacking System** | **NEW story** (see Section 4). |
| Future 7.x shop/loot stories | Must use `InventorySlot` API — document in those story files. |

### Artifact Conflicts

| Artifact | Change Required |
|----------|----------------|
| `ItemSO.cs` | `isStackable: bool` removed; `maxStacks: int` + `IsStackable` computed property added |
| `InventorySystem.cs` | `InventorySlot` struct introduced; `List<ItemSO>` → `List<InventorySlot>`; `DecrementStack()` added |
| `InventoryUI.cs` | `DropItem`, `UseItem`, `RefreshSlots`, `ShowContextMenu`, `SelectSlot` updated for new slot type |
| `ItemSlotUI.cs` | `Bind()` gains `stackCount` parameter; stack badge logic added |
| `ItemSlotUI.prefab` | New `StackCountText` TMP child (upper-left, hidden when count ≤ 1) |
| `ItemDetailPanelUI.cs` | `case PotionItemSO` added to `switch` block |
| `PlayerHealth.cs` | `Heal(float amount)` public method added |
| `PotionItemSO.cs` | **NEW** concrete `UsableItemSO` subclass |
| `Item_HealthPotion.asset` | **NEW** data asset |
| `HealthPotion.prefab` | **NEW** world item prefab |
| CLAUDE.md — `Items/` folder | Hierarchy, `isStackable → maxStacks` migration note |
| CLAUDE.md — `Scripts/UI/` folder | Stack badge pattern note |
| `epics.md` | Story 4.10 added to Epic 4 |
| `sprint-status.yaml` | Story 4.10 added |

### Technical Impact

- **`InventorySystem.Items` API type change** — only one consumer (`InventoryUI.cs`); contained.
- **`isStackable` field removed from `ItemSO`** — existing assets (`Item_Tome_PowerStrike.asset`) have `isStackable` in YAML; Unity silently ignores removed fields on next load. New `maxStacks` defaults to `0` in YAML → `IsStackable = false` ✅. No manual asset migration needed.
- **No breaking change to `SkillItemSO`** or any non-stackable item behavior.
- **No changes to test infrastructure** — new tests extend existing `InventorySystemTests.cs` and `HealthSystemTests.cs`.

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** (selected)

Add a single new story (4.10) to Epic 4. All changes are additive or internally scoped to the inventory subsystem. No rollbacks needed. No MVP reduction needed.

| Dimension | Assessment |
|-----------|-----------|
| Effort | **Medium** — ~8 files touched, all well-understood from prior stories |
| Risk | **Low** — additive changes; `InventoryUI` is the only cross-system ripple point |
| Timeline | No impact on Epic 5 start; story 4.10 can be implemented before or after 5.1 |
| Maintainability | `InventorySlot` struct is the correct long-term data model; deferring it would create more debt for Epic 7 |

**Rationale for not deferring:** `maxStacks` and `InventorySlot` are foundational to Epic 7 (shop sells stackable items, looting produces stacks of arrows/potions). Implementing now, while the inventory system is actively being extended, costs less than retrofitting later across multiple Epic 7 stories.

---

## Section 4: Detailed Change Proposals

### A — `ItemSO.cs` — replace `isStackable` with `maxStacks`

**File:** `Assets/_Game/ScriptableObjects/Items/ItemSO.cs`

```
OLD:
public bool isStackable;

NEW:
public int maxStacks = 1;
public bool IsStackable => maxStacks > 1;
```

Rationale: `maxStacks` replaces the unenforced placeholder bool. `IsStackable` is computed — both `0` and `1` mean non-stackable, so existing assets with unset `maxStacks` (defaults to `0` in serialization) are correctly non-stackable without any asset migration.

---

### B — `InventorySystem.cs` — `InventorySlot` struct + stacking logic

**File:** `Assets/_Game/Scripts/Inventory/InventorySystem.cs`

```
OLD:
  private readonly List<ItemSO> _items = new List<ItemSO>();
  public IReadOnlyList<ItemSO> Items => _items;
  AddItem(ItemSO item)         → always appends new entry
  RemoveItem(int index)        → removes entire slot

NEW:
  // New struct (inside namespace Game.Inventory):
  public readonly struct InventorySlot
  {
      public readonly ItemSO Item;
      public readonly int Count;
      public InventorySlot(ItemSO item, int count) { Item = item; Count = count; }
  }

  private readonly List<InventorySlot> _slots = new List<InventorySlot>();
  public IReadOnlyList<InventorySlot> Items => _slots;

  AddItem(ItemSO item)
    → if item.IsStackable: find existing slot for same item with Count < item.maxStacks
       → increment that slot's count; else append InventorySlot(item, 1)
    → if not stackable: always append InventorySlot(item, 1)

  RemoveItem(int index)        → unchanged: removes entire slot

  DecrementStack(int index)    → NEW
    → if Count > 1: replace slot with Count-1
    → if Count == 1: remove slot entirely
    → returns ItemSO that was decremented
```

Rationale: `InventorySlot` is the correct long-term model. `DecrementStack` gives "one at a time" semantics for UseItem and DropItem on stackable items. `RemoveItem` retains "clear entire slot" semantics for future use (Epic 7 sell-all, loot entire stack).

---

### C — `PlayerHealth.cs` — add `Heal()`

**File:** `Assets/_Game/Scripts/Player/PlayerHealth.cs`

```
OLD:
  public void TakeDamage(float amount)  ← only way to change health

NEW (add after TakeDamage):
  public void Heal(float amount)
  {
      if (IsDead) return;
      CurrentHealth = Mathf.Min(CurrentHealth + amount, _config.baseHealth);
      GameLog.Info(TAG, $"Player healed {amount} HP — HP: {CurrentHealth:F0}/{_config.baseHealth:F0}");
  }
```

Rationale: Symmetric to `TakeDamage`. Capped at `_config.baseHealth` (no overhealing). Dead guard is consistent with `TakeDamage`. No event raised for heals (prototype scope).

---

### D — `PotionItemSO.cs` — new concrete type (NEW FILE)

**File:** `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs`

```csharp
[CreateAssetMenu(menuName = "Items/Potion Item", fileName = "Item_")]
public class PotionItemSO : UsableItemSO
{
    [SerializeField] private float _healAmount = 30f;
    public float HealAmount => _healAmount;

    public override bool OnUse(GameObject user)
    {
        var health = user.GetComponent<PlayerHealth>();
        if (health == null) { GameLog.Warn(...); return false; }
        if (health.IsDead) return false;
        health.Heal(_healAmount);
        return true;
    }
}
```

Updated hierarchy:
```
ItemSO
└── UsableItemSO
    ├── SkillItemSO    (teaches a skill)
    └── PotionItemSO   (restores health)  ← NEW
```

---

### E — `InventoryUI.cs` — stacking-aware UseItem, DropItem, RefreshSlots

**File:** `Assets/_Game/Scripts/UI/InventoryUI.cs`

```
DropItem(int slotIndex):
  OLD: var item = _inventorySystem.Items[slotIndex];  (ItemSO)
       _inventorySystem.RemoveItem(slotIndex);
  NEW: var slot = _inventorySystem.Items[slotIndex];  (InventorySlot)
       var item = slot.Item;
       _inventorySystem.DecrementStack(slotIndex);    // drops 1 unit

UseItem(int slotIndex):
  OLD: var item = _inventorySystem.Items[slotIndex];  (ItemSO)
       if (used && usable.consumable)
           _inventorySystem.RemoveItem(slotIndex);
  NEW: var slot = _inventorySystem.Items[slotIndex];  (InventorySlot)
       var item = slot.Item;
       if (used && usable.consumable)
           _inventorySystem.DecrementStack(slotIndex); // consumes 1 unit

RefreshSlots():
  OLD: slot.Bind(items[i], i);              (ItemSO, index)
  NEW: slotUI.Bind(items[i].Item, i, items[i].Count);  (Item, index, stackCount)

ShowContextMenu / SelectSlot / UpdateDetailPanel:
  OLD: _inventorySystem.Items[slotIndex]           → returns ItemSO directly
  NEW: _inventorySystem.Items[slotIndex].Item      → access .Item on InventorySlot
```

---

### F — `ItemSlotUI.cs` + prefab — stack count badge

**File:** `Assets/_Game/Scripts/UI/ItemSlotUI.cs`

```
OLD: public void Bind(ItemSO item, int index)
NEW: [SerializeField] private TMP_Text _stackCountText;

     public void Bind(ItemSO item, int index, int stackCount = 1)
     {
         // ...existing icon + name logic unchanged...
         if (_stackCountText != null)
         {
             _stackCountText.text = stackCount.ToString();
             _stackCountText.gameObject.SetActive(stackCount > 1);
         }
     }
```

**Prefab — `Assets/_Game/Prefabs/UI/Inventory/ItemSlotUI.prefab`:**
- Add child `"StackCountText"` (TMP_Text, font size 12, white, upper-left anchor)
- `SetActive(false)` by default
- Wire `_stackCountText` serialized reference

---

### G — `ItemDetailPanelUI.cs` — add PotionItemSO case

**File:** `Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`

```
OLD switch:
  case SkillItemSO skillItem:
      ShowUsableSection(skillItem);
      ShowSkillSection(skillItem.Skill);
      break;

NEW switch (add case):
  case SkillItemSO skillItem:
      ShowUsableSection(skillItem);
      ShowSkillSection(skillItem.Skill);
      break;
  case PotionItemSO potionItem:
      ShowUsableSection(potionItem);
      break;
```

Rationale: Shows "Consumable" label via existing `_usableSection`. Heal amount shown via `description` field on the asset ("Restores 30 HP") — no new UI section needed.

---

### H — Data assets

**`Assets/_Game/Data/Items/Item_HealthPotion.asset`** (NEW)
- Type: `PotionItemSO`
- `itemName`: `"Health Potion"`
- `description`: `"Restores 30 HP when consumed."`
- `consumable`: `true`
- `maxStacks`: `10`
- `_healAmount`: `30`
- `worldItemPrefab`: `HealthPotion.prefab`

**`Assets/_Game/Prefabs/Items/Potions/HealthPotion.prefab`** (NEW)
- Root with `ItemPickup` component (`_item` → `Item_HealthPotion.asset`)
- `Rigidbody` + `SphereCollider`
- Placeholder mesh (Unity primitive sphere, red tint) until art asset available

---

## Section 5: Implementation Handoff

### Scope Classification: **Minor**

All changes are contained within the inventory subsystem and its direct consumers. No new epics, no backlog reorganization, no architectural pivot.

### New Story to Create

**Story 4.10 — Potion & Stacking System**

Suggested story statement:
> As a player, I can pick up health potions that stack in my inventory, use them to restore health (showing a stack count badge when I have more than one), and drop them one at a time.

**Story file location:** `_bmad-output/implementation-artifacts/4-10-potion-stacking-system.md`

### Files to CREATE

```
Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs
Assets/_Game/Data/Items/Item_HealthPotion.asset
Assets/_Game/Prefabs/Items/Potions/HealthPotion.prefab
_bmad-output/implementation-artifacts/4-10-potion-stacking-system.md
```

### Files to MODIFY

```
Assets/_Game/ScriptableObjects/Items/ItemSO.cs              ← isStackable→maxStacks+IsStackable
Assets/_Game/Scripts/Inventory/InventorySystem.cs            ← InventorySlot struct + DecrementStack
Assets/_Game/Scripts/Player/PlayerHealth.cs                  ← Heal() method
Assets/_Game/Scripts/UI/InventoryUI.cs                       ← stacking-aware UseItem/DropItem/Refresh
Assets/_Game/Scripts/UI/ItemSlotUI.cs                        ← Bind() + _stackCountText
Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs                 ← PotionItemSO case
Assets/_Game/Prefabs/UI/Inventory/ItemSlotUI.prefab          ← StackCountText child
Assets/Tests/EditMode/InventorySystemTests.cs                ← stacking tests
Assets/Tests/EditMode/HealthSystemTests.cs                   ← Heal() tests
Assets/_Game/ScriptableObjects/Items/CLAUDE.md               ← hierarchy + maxStacks note
_bmad-output/epics.md                                        ← story 4.10 added
_bmad-output/implementation-artifacts/sprint-status.yaml     ← 4-10 added
```

### Files NOT to Modify

```
Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs         ← abstract base unchanged
Assets/_Game/ScriptableObjects/Items/SkillItemSO.cs          ← no changes needed
Assets/_Game/Data/Items/Item_Tome_PowerStrike.asset          ← isStackable field ignored by Unity
Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab← no changes
```

### Handoff Recipients

| Role | Responsibility |
|------|---------------|
| **Scrum Master** | Create story file `4-10-potion-stacking-system.md` using `/bmad:bmgd:workflows:create-story`, update `epics.md` and `sprint-status.yaml` |
| **Dev Agent** | Implement story 4.10 following this proposal |
| **Dev Agent (post-impl)** | Run `/bmad:bmgd:workflows:code-review` on story 4.10 |

### Success Criteria

- [ ] `PotionItemSO` can be created via `Assets → Create → Items → Potion Item`
- [ ] Picking up 3 health potions creates 1 slot showing "3" badge (not 3 slots)
- [ ] Using a potion with 3 stacked: count becomes 2, player health increases by 30 HP
- [ ] Using the last potion (count = 1): slot is removed from inventory
- [ ] Dropping a stacked potion drops 1 unit and spawns a world item; slot count decrements
- [ ] Non-stackable items (tomes) behave exactly as before — no badge, no stacking
- [ ] `PlayerHealth.Heal()` does not overheal beyond `baseHealth`
- [ ] All existing Edit Mode tests pass (no regressions)
- [ ] New Edit Mode tests added for: `PotionItemSO.OnUse`, `PlayerHealth.Heal`, stacking in `InventorySystem.AddItem`, `DecrementStack`

---

*Generated by Correct Course workflow — 2026-03-18*
