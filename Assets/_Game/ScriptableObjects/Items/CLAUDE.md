# CLAUDE.md — Assets/_Game/ScriptableObjects/Items

> Loaded when Claude accesses files in this folder. Covers the Item SO hierarchy, how to create new item types, and integration points.

---

## Class Hierarchy

```
ItemSO                              (base — any item in the inventory)
├── UsableItemSO  (abstract)        (items that can be "used" from the context menu)
│   ├── SkillItemSO                 (teaches a SkillSO to the player on use)
│   └── PotionItemSO                (restores player health when used; stackable)
└── EquipableItemSO  (abstract)     (items that can be equipped to a slot)
    ├── WeaponSO  (abstract)        (occupies the Weapon slot; references a WeaponArchetypeSO)
    │   └── SwordSO                 (Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs — used for all current weapons)
    └── ArmorSO                     (occupies Helmet/Armor/Ring1/Necklace slots)

WeaponArchetypeSO                   (weapon family preset — Weapons/WeaponArchetypeSO.cs)
  + WeaponPose (struct), WeaponSheathSocket (Hip/Back), SignedAxis, WeaponGripMath (static) — all in Weapons/
```

All types live in namespace `Game.Inventory`.

**`WeaponSO` is abstract** (Story 7.10) — `ScriptableObject.CreateInstance<WeaponSO>()` will return null. Always instantiate a concrete subclass (e.g. `SwordSO`).

**New weapon family = new `WeaponArchetypeSO` asset** in `Data/Items/Weapons/Archetypes/` (grip poses, Hip/Back
sheath socket, animator override, default combo steps) — no code. Add a new `XxxSO : WeaponSO` class only when
the family needs new **fields**. `SwordSO` is kept (renaming would break asset `m_Script` refs) even though the
class name no longer determines the family. See `Prefabs/Items/Weapons/CLAUDE.md` for the normalized mesh frame.

**Weapon-level overrides win over the archetype:**
- `ResolvedComboSteps` = `comboSteps` if > 0, else `archetype.defaultComboSteps` (min 1), else `WeaponSO.DEFAULT_COMBO_STEPS` (2).
- `ResolvedAnimatorOverride` = `animatorOverrideController` if set, else `archetype.animatorOverrideController`, else null (default controller).
- Always read the `Resolved*` properties in runtime code — never the raw fields.

`EquipableItemSO` defines `public abstract bool CanEquip()` — always `true` in current stories; future stories override for conditional equipping (stat gates, quest requirements). All equippability type-checks use `item is EquipableItemSO` — never `item is WeaponSO || item is ArmorSO`.

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

Keep `description` as flavour text; numeric effects are shown by the detail panel's stat rows (`ItemDetailFormatter`) — add a row there when a new item type gains a numeric field.

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

### Usable item (context menu "Use")
1. Extend `UsableItemSO` (abstract), override `OnUse(GameObject user)`.
2. Add `[CreateAssetMenu]`.
3. If it has numeric effects, add its rows (and category) in `ItemDetailFormatter` + `ItemDetailFormatterTests`. The Consumable/Reusable tag row is automatic.
4. The context menu **Use** button enables automatically (checks `item is UsableItemSO`).

### Equippable item (wearable gear)
1. Extend `EquipableItemSO` (abstract), override `CanEquip() => true`.
2. Add `[CreateAssetMenu]`.
3. Define which `EquipmentSlot` the item targets (either hardcoded like `WeaponSO`, or via a `slot` field like `ArmorSO`).
4. Add a new `case` in `EquipmentSystem.Equip()` for slot resolution, plus an `else` warn for unknown types.
5. Stat bonus rows are automatic for any `EquipableItemSO`; add type-specific rows and a category in `ItemDetailFormatter` (concrete types before abstract bases in its `switch`) + tests. No prefab change needed.
6. The **Equip/Unequip** detail button is handled by `InventoryDetailActions` for any `EquipableItemSO`.
7. The context menu **Equip** button in `InventoryUI.ShowContextMenu()` also appears automatically — it checks `_equipmentSystem.IsEquippable(item) && !_equipmentSystem.IsEquipped(item)`, which resolves to true for any `EquipableItemSO` not yet equipped. There is **no Unequip path in the context menu** — unequip is only via double-click on the equipment slot or the Unequip detail button.

### Item detail panel
`ItemDetailPanelUI` (`Scripts/UI/Inventory/`) has no per-type sections: it paints a category line, data-driven stat rows,
the description and a price, all computed by `ItemDetailFormatter`. Action buttons live in the per-host actions prefabs
(`InventoryDetailActions`, `TradeDetailActions`, `ContainerDetailActions`). See `Scripts/UI/Inventory/CLAUDE.md`.

---

## Runtime Integration

| System | File | Role |
|---|---|---|
| `InventorySystem` | `Scripts/Inventory/InventorySystem.cs` | Holds `List<InventorySlot>` at runtime; `AddItem` (stacks stackable items), `RemoveItem` (removes whole slot), `DecrementStack` (removes one unit), `MoveItem` |
| `ItemPickup` | `Scripts/Inventory/ItemPickup.cs` | World interactable; calls `InventorySystem.AddItem(_item)` and destroys itself |
| `InventoryUI` | `Scripts/UI/Inventory/InventoryUI.cs` | Reads `InventorySystem.Items`; calls `UseItem` / `DropItem` |
| `ItemDetailPanelUI` | `Scripts/UI/Inventory/ItemDetailPanelUI.cs` | Paints an `ItemSO` (category, stat rows, description, price) from `ItemDetailFormatter` |

---

## Drop Behaviour

**Every item that should be droppable must have `worldItemPrefab` assigned** — otherwise the drop is
skipped (`GameLog` warning). Prefab requirements and drop physics: `Prefabs/Items/CLAUDE.md`.

---

## Code Review Checklist — Items

| Severity | Pattern |
|---|---|
| HIGH | New `UsableItemSO` subclass returns `true` from `OnUse` when the use actually failed — item will be consumed incorrectly |
| HIGH | `worldItemPrefab` left unassigned on a droppable item — drop silently no-ops |
| MEDIUM | New item type with numeric fields but no category/stat rows in `ItemDetailFormatter` (+ `ItemDetailFormatterTests`) — detail panel shows only the generic rows. `ItemDetailPanelUI` has no per-type code; never add cases there |
| MEDIUM | `OnUse` calls `GetComponent` on `user` without a null guard — logs no error if component is missing |
| MEDIUM | New stackable item type (`maxStacks > 1`) without verifying `worldItemPrefab` has `ItemPickup` + `Rigidbody` — dropped items must be re-pickable |
