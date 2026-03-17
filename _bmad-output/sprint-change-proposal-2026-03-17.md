# Sprint Change Proposal — Usable Item System
**Date:** 2026-03-17
**Workflow:** correct-course
**Scope:** Minor — direct implementation by development team

---

## Section 1: Issue Summary

**Problem Statement:**
The current `ItemSO` is a flat data class with no extension point for "usable" behaviour. Tomes are the first item type that should be usable from inventory, but today skill learning only works via direct world interaction (`TomePickup.cs`). As more consumable/skill item types are planned in Epics 7 and 8, a clean `UsableItemSO` abstraction must be established now before item diversity grows.

**Discovery Context:**
Identified by Valentin during Epic 4 wrap-up, after story 4.8 (inventory context menu) completed. The right-click context menu and inventory framework are in place — this is the right moment to extend the item hierarchy before new epics begin.

**Current Limitations:**
- `ItemSO` has no virtual or abstract methods; no `OnUse()` hook exists
- Context menu `ShowContextMenu()` wires only a single "Drop" button via `GetComponentInChildren<Button>()` — not extensible to multiple buttons without rework
- No "Use" button in the inventory context menu
- Tomes in inventory cannot be used (only droppable); skill learning requires finding the world object

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 4 (in-progress):** Add story 4.9 `usable-item-system`. All other Epic 4 stories remain done.
- **Epic 7 (backlog):** `UsableItemSO` is a direct prerequisite for consumable items (potions, scrolls). No changes to Epic 7 stories needed — this proposal creates the foundation they will build on.
- **Epic 8 (backlog):** Crafted items will likely extend `UsableItemSO`. No changes needed now.

### Story Impact
| Story | Status | Impact |
|-------|--------|--------|
| 4.5 `item-scriptable-object` | done | `ItemSO` modified: adds no fields, remains fully backward-compatible |
| 4.8 `inventory-context-menu-and-detail-panel` | done | `InventoryUI.ShowContextMenu()` and `InventoryContextMenu.prefab` require targeted changes |
| **4.9 `usable-item-system`** | **NEW** | New story to implement this proposal |

### Artifact Conflicts
| Artifact | Conflict | Action |
|----------|----------|--------|
| `ItemSO.cs` | None — base class unchanged | No action |
| `InventoryUI.cs` | `ShowContextMenu()` wires 1 button by type; needs named-button lookup + Use wiring | Update |
| `InventoryContextMenu.prefab` | Has only "Drop" button; needs "Use" button added | Update |
| `epics.md` | Epic 4 acceptance criteria doesn't mention usable items | Minor update |
| `sprint-status.yaml` | Story 4.9 missing | Add entry |

### Technical Impact
- `UsableItemSO.OnUse(GameObject user)` pattern: SOs cannot hold scene MonoBehaviour refs, so `user` is passed in at call time. `SkillItemSO` calls `user.GetComponent<PlayerSkills>()` — consistent with existing project pragmatism (`ItemPickup` uses `FindFirstObjectByType<InventorySystem>()`).
- `TomePickup.cs` world behaviour is **unchanged** — the tome can still be learned directly from the world (pressing E). The inventory Use path is additive.

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** ✅ Selected

Add story 4.9 to Epic 4. No rollback or MVP scope change required.

**Rationale:**
- All infrastructure (inventory, context menu, ItemSO) already exists; this is a targeted extension
- The `UsableItemSO` abstraction is a clean, low-risk hierarchy that pays dividends across Epics 7 and 8
- Effort is low (3–4 scripts, 1 prefab change, 1 data asset)
- Risk is low — additive only, no existing behaviour removed

**Effort:** Low | **Risk:** Low | **Timeline Impact:** +1 story in Epic 4

---

## Section 4: Detailed Change Proposals

### 4.1 — New File: `UsableItemSO.cs`

**Path:** `Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs`

```
OLD: (does not exist)

NEW:
using UnityEngine;

namespace Game.Inventory
{
    public abstract class UsableItemSO : ItemSO
    {
        [Tooltip("If true, the item is removed from inventory after use.")]
        public bool consumable;

        /// <summary>Called when the player uses this item from the inventory context menu.</summary>
        /// <param name="user">The player GameObject — use GetComponent to access player systems.</param>
        public abstract void OnUse(GameObject user);
    }
}
```

**Rationale:** Abstract base keeps `ItemSO` untouched and backward-compatible. `consumable` flag lives here so all usable items opt in to self-removal.

---

### 4.2 — New File: `SkillItemSO.cs`

**Path:** `Assets/_Game/ScriptableObjects/Items/SkillItemSO.cs`

```
OLD: (does not exist)

NEW:
using Game.Core;
using Game.Progression;
using UnityEngine;

namespace Game.Inventory
{
    [CreateAssetMenu(menuName = "Items/Skill Item", fileName = "Item_")]
    public class SkillItemSO : UsableItemSO
    {
        private const string TAG = "[SkillItemSO]";

        [SerializeField] private SkillSO _skill;

        public override void OnUse(GameObject user)
        {
            if (_skill == null)
            {
                GameLog.Warn(TAG, "OnUse: _skill not assigned.");
                return;
            }
            var playerSkills = user.GetComponent<PlayerSkills>();
            if (playerSkills == null)
            {
                GameLog.Warn(TAG, $"OnUse: no PlayerSkills on {user.name}");
                return;
            }
            playerSkills.LearnSkill(_skill);
        }
    }
}
```

**Rationale:** Mirrors `TomePickup.Interact()` logic exactly. Reuses `PlayerSkills.LearnSkill()` which already handles LP validation, LP deduction, and "already learned" guard.

---

### 4.3 — Modified: `InventoryUI.cs` — `ShowContextMenu()`

**Path:** `Assets/_Game/Scripts/UI/InventoryUI.cs`

**OLD (lines 174–176):**
```csharp
// Wire drop item button at runtime
var btn = _activeContextMenu.GetComponentInChildren<Button>();
btn.onClick.AddListener(() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); });
```

**NEW:**
```csharp
var item = _inventorySystem.Items[slotIndex];

// Wire drop button
var dropBtn = _activeContextMenu.transform.Find("DropButton").GetComponent<Button>();
dropBtn.onClick.AddListener(() => { DropItem(_contextMenuSlotIndex); HideContextMenu(); });

// Use button — always present; enabled only for UsableItemSO
var useBtn = _activeContextMenu.transform.Find("UseButton").GetComponent<Button>();
if (item is UsableItemSO usable)
{
    useBtn.interactable = true;
    useBtn.onClick.AddListener(() => { UseItem(_contextMenuSlotIndex); HideContextMenu(); });
}
else
{
    useBtn.interactable = false;
}
```

**Rationale:** Named lookup (`Find("DropButton")` / `Find("UseButton")`) replaces fragile `GetComponentInChildren` order dependency. "Use" always rendered — disabled state communicates item is not usable rather than hiding the option.

---

### 4.4 — New Method: `InventoryUI.UseItem()`

**Path:** `Assets/_Game/Scripts/UI/InventoryUI.cs`

**OLD:** (method does not exist)

**NEW** (add after `DropItem()`):**
```csharp
public void UseItem(int slotIndex)
{
    if (slotIndex < 0 || slotIndex >= _inventorySystem.Count)
    {
        GameLog.Warn(TAG, $"Use skipped: slot {slotIndex} out of range");
        return;
    }
    var item = _inventorySystem.Items[slotIndex];
    if (item is not UsableItemSO usable)
    {
        GameLog.Warn(TAG, $"Use skipped: {item.itemName} is not UsableItemSO");
        return;
    }

    usable.OnUse(_playerTransform.gameObject);

    if (usable.consumable)
    {
        _inventorySystem.RemoveItem(slotIndex);
        RefreshSlots();
        GameLog.Info(TAG, $"Consumed: {item.itemName}");
    }
}
```

**Rationale:** Single responsibility — all "use from inventory" logic centralised here. Consumption happens post-`OnUse` so the item effect fires even if removal fails.

---

### 4.5 — Modified: `InventoryContextMenu.prefab`

**Path:** `Assets/_Game/Prefabs/UI/Inventory/InventoryContextMenu.prefab`

```
OLD:
└── InventoryContextMenu
    └── [Button] (unnamed or "DropButton")

NEW:
└── InventoryContextMenu
    ├── [Button] "DropButton"   — label "Drop"   — always interactable
    └── [Button] "UseButton"    — label "Use"     — default interactable = false (grayed)
```

**Rationale:** Button GameObjects named exactly to match `Find()` calls in `ShowContextMenu()`. "Use" defaults to `interactable = false` in prefab so it's always grayed until explicitly enabled at runtime.

---

### 4.6 — New Data Asset: `Item_Tome_PowerStrike.asset`

**Path:** `Assets/_Game/Data/Items/Item_Tome_PowerStrike.asset`

```
OLD: (does not exist — tome was world-only via TomePickup)

NEW: SkillItemSO asset
  itemName:        "Tome of Power Strike"
  description:     "A worn tome that teaches the Power Strike technique."
  icon:            [assign tome icon sprite]
  isStackable:     false
  worldItemPrefab: Tome_PowerStrike.prefab
  consumable:      true
  _skill:          PowerStrike (SkillSO asset)
```

**Rationale:** `consumable = true` — learning a skill from a tome consumes it, matching world-tome behaviour. `isStackable = false` — tomes are unique.

---

### 4.7 — Update: `Tome_PowerStrike.prefab` ItemPickup reference

**Path:** `Assets/_Game/Prefabs/Items/Tomes/Tome_PowerStrike.prefab`

```
OLD: ItemPickup._item → (ItemSO asset, if set — may be unset pre-story)

NEW: ItemPickup._item → Item_Tome_PowerStrike.asset (SkillItemSO)
```

**Rationale:** Picking up the world tome now puts a `SkillItemSO` in inventory instead of a generic item. The `TomePickup` component on the same prefab remains — direct-read world interaction still works alongside inventory use.

---

### 4.8 — Update: `sprint-status.yaml`

```
OLD:
  epic-4: in-progress
  ...
  4-7-trainer-look-at-interaction: done
  epic-4-retrospective: optional

NEW:
  epic-4: in-progress
  ...
  4-7-trainer-look-at-interaction: done
  4-9-usable-item-system: ready-for-dev
  epic-4-retrospective: optional
```

---

### 4.9 — Update: `epics.md` — Epic 4 Acceptance Criteria

**Section:** Epic 4, Acceptance Criteria

**OLD (last bullet):**
```
- As a player, I can right-click an inventory slot to open a context menu with
  actions (starting with "Drop Item"), and left-click a slot to select it and
  view item details in a panel
```

**NEW (append bullet):**
```
- As a player, I can right-click a usable item (e.g. a tome) in my inventory
  and select "Use" to trigger its effect; non-usable items show "Use" grayed out
- As a player, using a consumable item removes it from my inventory after use
```

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — direct implementation by development team.

### Story 4.9: `usable-item-system`

**Tasks for Dev:**
1. Create `UsableItemSO.cs` (abstract base with `consumable` + `OnUse(GameObject)`)
2. Create `SkillItemSO.cs` (implements `OnUse` via `PlayerSkills.LearnSkill`)
3. Update `InventoryContextMenu.prefab` — add "UseButton", rename existing to "DropButton", set UseButton `interactable = false` default
4. Update `InventoryUI.ShowContextMenu()` — named button lookup + Use button wiring
5. Add `UseItem()` method to `InventoryUI`
6. Create `Item_Tome_PowerStrike.asset` (`SkillItemSO`, `consumable = true`, refs `PowerStrike` skill + `Tome_PowerStrike` prefab)
7. Update `Tome_PowerStrike.prefab` `ItemPickup._item` → `Item_Tome_PowerStrike.asset`
8. Update `sprint-status.yaml` and `epics.md`

**Dependencies:**
- `SkillSO` for PowerStrike must exist at `Assets/_Game/Data/Skills/` (confirmed: exists from story 3.5)
- `PlayerSkills` must be on the Player GameObject root (confirmed: exists from story 3.3)
- `_playerTransform` must be assigned in `InventoryUI` inspector (confirmed: already a serialized field)

**Success Criteria:**
- [ ] Right-clicking any item shows context menu with "Use" (grayed) and "Drop"
- [ ] Right-clicking `Item_Tome_PowerStrike` shows "Use" enabled
- [ ] Clicking "Use" on the tome calls `PlayerSkills.LearnSkill(PowerStrike)`
- [ ] If player has sufficient LP → skill learned, tome removed from inventory
- [ ] If player lacks LP → `PlayerSkills.LearnSkill` returns false, tome stays in inventory
- [ ] If player already knows skill → no effect, tome stays in inventory
- [ ] Dropping the tome still works as before
- [ ] `TomePickup` world interaction still works (unchanged)
- [ ] New `SkillItemSO` assets can be created via `Assets > Create > Items > Skill Item`
