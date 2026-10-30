# CLAUDE.md — Assets/_Game/ScriptableObjects/Items

> Loaded when Claude accesses files in this folder. Covers the Item SO hierarchy, how to create new item types, and integration points.
> **New generic item from a model folder:** `/items:create <folder> [Name]` (`Editor/Items/ItemAssetBuilder`) —
> creates `Data/Items/Item_<Name>.asset`, `Prefabs/Items/Item_<Name>.prefab`, `Art/Items/<Name>/` (FBX, maps,
> `M_<Name>.mat`) and the icon, then syncs `ItemCatalog`. Weapons use the Weapon Creator instead.

---

## Class Hierarchy

```
ItemSO                              (base — any item in the inventory)
├── UsableItemSO  (abstract)        (items that can be "used" from the context menu)
│   ├── SkillItemSO                 (teaches a SkillSO to the player on use)
│   ├── RecipeItemSO                (teaches a cooking RecipeSO on use — sets its RecipeFact)
│   └── PotionItemSO                (restores player health when used; stackable; optional first-meal permanent bonus)
└── EquipableItemSO  (abstract)     (items that can be equipped to a slot)
    ├── WeaponSO  (abstract)        (occupies the Weapon slot; references a WeaponArchetypeSO)
    │   ├── SwordSO                 (Weapons/SwordSO.cs — every melee weapon)
    │   └── BowSO                   (Weapons/BowSO.cs — ranged: ammoItem, projectilePrefab, speed, cooldown; see Scripts/Combat/CLAUDE.md)
    └── ArmorSO                     (occupies Helmet/Armor/Ring1/Necklace slots)

WeaponArchetypeSO                   (weapon family preset — Weapons/WeaponArchetypeSO.cs)
  + WeaponPose (struct), WeaponSheathSocket (Hip/Back), WeaponHand (Right/Left), SignedAxis, WeaponGripMath (static) — all in Weapons/
```

All types live in namespace `Game.Inventory`.

**`WeaponSO` is abstract** (Story 7.10) — `ScriptableObject.CreateInstance<WeaponSO>()` will return null. Always instantiate a concrete subclass (e.g. `SwordSO`).

**New weapon family = new `WeaponArchetypeSO` asset** in `Data/Items/Weapons/Archetypes/` (drawn hand, grip poses, Hip/Back
sheath socket, animator override, default combo steps) — no code. Add a new `XxxSO : WeaponSO` class only when
the family needs new **fields**. `SwordSO` is kept (renaming would break asset `m_Script` refs) even though the
class name no longer determines the family. See `Prefabs/Items/Weapons/CLAUDE.md` for the normalized mesh frame.

**Forged weapons (crafted bonus):** an inventory slot is `(ItemSO, Count)` and saves store `itemId`, so a copy can't carry
"was crafted". A crafted weapon is its **own asset** `Weapon_<Name>_Forged` with better stats, its `WeaponSO.baseWeapon` →
the standard `Weapon_<Name>` (bought / looted). Both keep the **same `itemName`** — `IsForged` → detail category
"Weapon · Forged" tells them apart, and the bonus is simply the higher Damage value (no extra comparison row). The
smithing recipe outputs the forged one. Both share the `_Visual`
prefab; each has its own `_World` variant (its `ItemPickup._item`). Example: `Weapon_ShortIronSword` (10) /
`Weapon_ShortIronSword_Forged` (14), `Prefabs/Items/Weapons/Swords/ShortIronSword/`.

**Weapon-level overrides win over the archetype:**
- `ResolvedComboSteps` = `comboSteps` if > 0, else `archetype.defaultComboSteps` (min 1), else `WeaponSO.DEFAULT_COMBO_STEPS` (2).
- `ResolvedAnimatorOverride` = `animatorOverrideController` if set, else `archetype.animatorOverrideController`, else null (default controller).
- `ResolvedAttackStaminaMultiplier` = `archetype.attackStaminaMultiplier` (default 1), else 1 — archetype-only, no weapon override.
  `PlayerCombat` multiplies `CombatConfigSO.attackStaminaCost` by it for melee swings (bow shots keep the flat cost).
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
| `isCraftingMaterial` | `bool` | Smithing material (`Item_IronIngot`): the only items an anvil's grid lists. Detail category **Crafting Material** (after the ingredient rule) |
| `isIngredient` | `bool` | Accepted by the cooking Experiment slots and the only items the cooking grid lists (`Scripts/Crafting/CLAUDE.md`). A flag, not a subclass: any item type (raw food `PotionItemSO`, future non-edible ingredients) can be one. Its detail category is **Ingredient**, whatever the class. Never set it on a cooked meal |

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
| `_kind` / `Kind` | `ConsumableKind` | `Potion` (default) or `Meal` (cooked food) — the detail category. Raw food stays `Potion` but is `isIngredient`, so it shows "Ingredient". `OnValidate` warns on `Meal` + `isIngredient` |
| `_healAmount` | `float` | HP restored on use (default 30) |
| `HealAmount` (property) | `float` | Public read accessor |

`OnUse` heals `_healAmount`: **instantly** (potions, `Item_HealingPlant`) or, when `_overTimeEffect` + `_durationMinutes`
(game minutes) are both set (food), **over time** through the player's `StatusEffectHost` (`SE_WellFed`, see
`Scripts/StatusEffects/CLAUDE.md`). No host / clock → instant heal + warning. Returns false (and keeps the item) if
`PlayerHealth` is missing or the player is dead. The detail row reads "+8 over 20 min".

**First-meal bonus** (food only): `_permanentBonuses` (`List<MealBonus>` — `MealBonusStat` MaxHealth / Strength /
Dexterity / Endurance / Intelligence + `amount`, ≤ 0 ignored) and `_onMealBonusGained` (`OnMealBonusGained` toast,
"Permanent bonus: +5 Max HP"). The first eat (after the heal) applies each bonus — MaxHealth →
`PlayerHealth.AddMaxHealthBonus` (max **and** current), stats → `PlayerStats.UpgradeStat` — then
`WorldStateManager.ClaimMealBonus(item)`; later eats only heal. No Core → heal only, nothing claimed. Text:
`MealBonusText.Format` (`MealBonus.cs`), shared by the toast and the detail "First meal" row (grayed "(gained)" once
claimed). Example: `Item_AppleMushroomSandwich` +5 Max HP.

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

## RecipeItemSO (concrete)

`Assets/_Game/ScriptableObjects/Items/RecipeItemSO.cs` — fields `_recipe` (`RecipeSO`, needs `output` + `knownFact`),
`_onRecipeLearned` (`OnRecipeLearned` toast channel). `OnUse` → `WorldStateManager.SetRecipeKnown`; returns false when
the recipe is already known, misconfigured, or Core isn't loaded. Assets: `Data/Items/Item_Recipe_<Output>.asset`,
consumable. Create via **Assets → Create → Items → Recipe Item**. Details: `Scripts/Crafting/CLAUDE.md`.

---

## Adding a New Item Type

### Usable item (context menu "Use")
1. Extend `UsableItemSO` (abstract), override `OnUse(GameObject user)`.
2. Add `[CreateAssetMenu]`.
3. If it has numeric effects, add its rows (and category) in `ItemDetailFormatter` + `ItemDetailFormatterTests`. The Consumable/Reusable tag row is automatic.
4. The context menu **Use** button and the detail **Use** button appear automatically (`item is UsableItemSO`);
   they are interactable only while `CanUse()` is true (virtual, default true — override it when a use would surely be
   rejected, e.g. `RecipeItemSO` when known). `OnUse` must still reject by itself: action bar / double-click skip `CanUse`.

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

## Save IDs — `itemId` + `ItemCatalog`

- `ItemSO.itemId` is the stable save ID (GUID, auto-filled in `OnValidate`) — **never edit it**. Subclasses that
  need `OnValidate` must `override` and call `base.OnValidate()`.
- `Data/Items/ItemCatalog.asset` (`ItemCatalogSO`, `FindById`) lists every item; `Editor/Save/ItemCatalogAutoSync`
  keeps it in sync on item import / move / delete (or `Tools/Save/Sync Item Catalog`). A duplicated item asset keeps
  the original's `itemId` → the sync logs an error naming both paths: clear the copy's `itemId` so it regenerates.

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
