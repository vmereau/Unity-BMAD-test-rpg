# CLAUDE.md — Assets/_Game/ScriptableObjects/Items

> Loaded when Claude accesses files in this folder. Covers the Item SO hierarchy, how to create new item types, and integration points.

---

## Class Hierarchy

```
ItemSO                          (base — any item in the inventory)
└── UsableItemSO  (abstract)   (items that can be "used" from the context menu)
    ├── SkillItemSO             (teaches a SkillSO to the player on use)
    └── PotionItemSO            (restores player health when used; stackable)
```

All types live in namespace `Game.Inventory`.

---

## ItemSO (base)

`Assets/_Game/ScriptableObjects/Items/ItemSO.cs`

| Field | Type | Purpose |
|---|---|---|
| `itemName` | `string` | Display name in UI |
| `description` | `string` | Shown in detail panel |
| `icon` | `Sprite` | Inventory slot + detail panel icon |
| `maxStacks` | `int` | Max units per inventory slot (default 1 = non-stackable) |
| `IsStackable` | `bool` (computed) | `true` when `maxStacks > 1`; drives stacking logic in `InventorySystem` |
| `worldItemPrefab` | `GameObject` | Prefab spawned when the item is dropped |

Create via **Assets → Create → Items → Item**.

---

## UsableItemSO (abstract)

`Assets/_Game/ScriptableObjects/Items/UsableItemSO.cs`

Adds to `ItemSO`:

| Field | Type | Purpose |
|---|---|---|
| `consumable` | `bool` | If true, item is removed from inventory after a successful use |

Implementors must override:
```csharp
public abstract bool OnUse(GameObject user);
// Returns true  → use succeeded (consumable items are then removed)
// Returns false → use rejected (item stays in inventory)
```

The `user` parameter is the **player GameObject** — use `GetComponent<T>()` on it to access player systems.

---

## PotionItemSO (concrete)

`Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs`

Adds to `UsableItemSO`:

| Field | Type | Purpose |
|---|---|---|
| `_healAmount` | `float` | HP restored on use (default 30) |
| `HealAmount` (property) | `float` | Public read accessor |

`OnUse` calls `PlayerHealth.Heal(_healAmount)`. Returns false (and keeps the item) if `PlayerHealth` is missing or the player is dead.

Create via **Assets → Create → Items → Potion Item**.

---

## SkillItemSO (concrete)

`Assets/_Game/ScriptableObjects/Items/SkillItemSO.cs`

Adds to `UsableItemSO`:

| Field | Type | Purpose |
|---|---|---|
| `_skill` | `SkillSO` | The skill taught on use |
| `Skill` (property) | `SkillSO` | Public read accessor (used by `ItemDetailPanelUI`) |

`OnUse` calls `PlayerSkills.LearnSkill(_skill)`. Returns false (and keeps the item) if the skill is already learned or `PlayerSkills` is missing.

Create via **Assets → Create → Items → Skill Item**.

---

## Adding a New Item Type

1. Create a new class extending `ItemSO` (or `UsableItemSO` for usable items).
2. Add a `[CreateAssetMenu]` attribute.
3. If it has data the UI should display:
   - Add a public property/accessor for that data.
   - Add a new `case` in `ItemDetailPanelUI.Show()` (`Assets/_Game/Scripts/UI/ItemDetailPanelUI.cs`).
   - Add the optional section fields in the Inspector on the `_detailPanel` GameObject.
4. If it is usable, the context menu **Use** button in `InventoryUI` enables automatically (it checks `item is UsableItemSO`).

---

## Runtime Integration

| System | File | Role |
|---|---|---|
| `InventorySystem` | `Scripts/Inventory/InventorySystem.cs` | Holds `List<InventorySlot>` at runtime; `AddItem` (stacks stackable items), `RemoveItem` (removes whole slot), `DecrementStack` (removes one unit), `MoveItem` |
| `ItemPickup` | `Scripts/Inventory/ItemPickup.cs` | World interactable; calls `InventorySystem.AddItem(_item)` and destroys itself |
| `InventoryUI` | `Scripts/UI/InventoryUI.cs` | Reads `InventorySystem.Items`; calls `UseItem` / `DropItem` |
| `ItemDetailPanelUI` | `Scripts/UI/ItemDetailPanelUI.cs` | Receives an `ItemSO`, dispatches display per type via `switch` pattern match |

---

## Drop Behaviour

`InventoryUI.DropItem` instantiates `item.worldItemPrefab` at the player's position (1.5 m forward, 0.5 m up) and applies a small `Rigidbody` impulse. **Every item that should be droppable must have `worldItemPrefab` assigned** — a missing prefab causes the drop to be silently skipped (a warning is logged via `GameLog`).

---

## Code Review Checklist — Items

| Severity | Pattern |
|---|---|
| HIGH | New `UsableItemSO` subclass returns `true` from `OnUse` when the use actually failed — item will be consumed incorrectly |
| HIGH | `worldItemPrefab` left unassigned on a droppable item — drop silently no-ops |
| MEDIUM | New item type added without a corresponding `case` in `ItemDetailPanelUI.Show()` — detail panel shows only base info |
| MEDIUM | `OnUse` calls `GetComponent` on `user` without a null guard — logs no error if component is missing |
| MEDIUM | New stackable item type (`maxStacks > 1`) without verifying `worldItemPrefab` has `ItemPickup` + `Rigidbody` — dropped items must be re-pickable |
