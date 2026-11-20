# Adding Item Types — new `ItemSO` subclasses

> Read when adding a new item class (usable or equippable) under `ScriptableObjects/Items/`. Class hierarchy, field
> tables and `Resolved*` rules: `ScriptableObjects/Items/CLAUDE.md`. Generic items from a model folder: `/items:create`.

---

## Usable item (context menu "Use")

1. Extend `UsableItemSO` (abstract), override `OnUse(GameObject user)`.
2. Add `[CreateAssetMenu]`.
3. If it has numeric effects, add its rows (and category) in `ItemDetailFormatter` + `ItemDetailFormatterTests`. The Consumable/Reusable tag row is automatic.
4. The context menu **Use** button and the detail **Use** button appear automatically (`item is UsableItemSO`);
   they are interactable only while `CanUse()` is true (virtual, default true — override it when a use would surely be
   rejected, e.g. `RecipeItemSO` when known). `OnUse` must still reject by itself: action bar / double-click skip `CanUse`.

---

## Equippable item (wearable gear)

1. Extend `EquipableItemSO` (abstract), override `CanEquip() => true`.
2. Add `[CreateAssetMenu]`.
3. Define which `EquipmentSlot` the item targets (either hardcoded like `WeaponSO`, or via a `slot` field like `ArmorSO`).
4. Add the slot resolution to `EquipmentRules.TryGetTargetSlot` **and** `FitsSlot` (`Scripts/Inventory/EquipmentRules.cs`, tested) — never in `EquipmentSystem` itself. A weapon that needs both hands overrides `WeaponSO.OccupiesOffhand`.
5. Stat bonus rows are automatic for any `EquipableItemSO`; add type-specific rows and a category in `ItemDetailFormatter` (concrete types before abstract bases in its `switch`) + tests. No prefab change needed.
6. The **Equip** / **Unequip** buttons (detail panel + context menu) appear automatically for any `EquipableItemSO` — see `Scripts/UI/Inventory/CLAUDE.md`.

---

## Item detail panel

`ItemDetailPanelUI` has no per-type code — new rows / categories go in `ItemDetailFormatter` (steps above). Details:
`Scripts/UI/Inventory/CLAUDE.md`.
