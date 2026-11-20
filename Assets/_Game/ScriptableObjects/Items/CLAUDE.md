# CLAUDE.md — Assets/_Game/ScriptableObjects/Items

> Loaded when Claude accesses files in this folder. Covers the Item SO hierarchy, field tables, `Resolved*` rules, forged
> weapons and save IDs. Prefab requirements / drop: `Prefabs/Items/CLAUDE.md`; runtime systems: `Scripts/Inventory/CLAUDE.md`.
> **New generic item from a model folder:** `/items:create <folder> [Name]` (`Editor/Items/ItemAssetBuilder`) —
> creates `Data/Items/Item_<Name>.asset`, `Prefabs/Items/Item_<Name>.prefab`, `Art/Items/<Name>/` (FBX, maps,
> `M_<Name>.mat`) and the icon, then syncs `ItemCatalog`. Weapons use the Weapon Creator instead.

---

## Topic files

| File | Read when |
|------|-----------|
| `ADDING_ITEM_TYPES.md` | Adding a new `ItemSO` subclass (usable or equippable) |

Gold coins (`GoldItemSO`, `Item_Gold`): `Scripts/Inventory/CLAUDE.md` → Gold loot.

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
    ├── ArmorSO                     (Helmet/Armor/Necklace slots; a ring asset uses `slot = Ring1` and equips to Ring1 or Ring2 — `EquipmentRules`)
    └── ShieldSO                    (occupies Offhand; blockStaminaCost, perfectBlockWindow, heldPose / backPose — armor = defenseBonus)

WeaponArchetypeSO                   (weapon family preset — Weapons/WeaponArchetypeSO.cs)
  + WeaponPose (struct), WeaponSheathSocket (Hip/Back), WeaponHand (Right/Left), SignedAxis, WeaponGripMath (static) — all in Weapons/
```

All types live in namespace `Game.Inventory`.

**`WeaponSO` is abstract** — `ScriptableObject.CreateInstance<WeaponSO>()` will return null. Always instantiate a concrete subclass (e.g. `SwordSO`).

**New weapon family = new `WeaponArchetypeSO` asset** in `Data/Items/Weapons/Archetypes/` (drawn hand, grip poses, Hip/Back
sheath socket, animator override, default combo steps) — no code. Add a new `XxxSO : WeaponSO` class only when
the family needs new **fields**. `SwordSO` is kept (renaming would break asset `m_Script` refs) even though the
class name no longer determines the family. See `Prefabs/Items/Weapons/CLAUDE.md` for the normalized mesh frame.

**Forged weapons (crafted bonus):** an inventory slot is `(ItemSO, Count)` and saves store `itemId`, so a copy can't carry
"was crafted". A crafted weapon is its **own asset** `Weapon_<Name>_Forged` with better stats, its `WeaponSO.baseWeapon` →
the standard `Weapon_<Name>` (bought / looted). Both keep the **same `itemName`** — `IsForged` → detail category
"Weapon · Forged" tells them apart, and the bonus is simply the higher Damage value (no extra comparison row). The
smithing recipe outputs the forged one. Both share the `_Visual`
prefab; each has its own `_World` variant (its `ItemPickup._item`). Example: `Weapon_ShortIronSword` (12) /
`Weapon_ShortIronSword_Forged` (16), `Prefabs/Items/Weapons/Swords/ShortIronSword/`.

**`WeaponSO` fields** (`WeaponSO.cs`): `archetype`, `damageBonus` (base damage; raw hit = it + the wielder's
`scalingStat`, 1:1), `scalingStat` (Strength for melee, Dexterity for bows), `baseWeapon` (forged → standard
counterpart), `comboSteps`, `animatorOverrideController`, `stanceDamage` (0 = archetype), `knockDown`
(`KnockDownOverride`: Archetype / Yes / No).

**Weapon-level overrides win over the archetype:**
- `ResolvedComboSteps` = `comboSteps` if > 0, else `archetype.defaultComboSteps` (min 1), else `WeaponSO.DEFAULT_COMBO_STEPS` (2).
- `ResolvedAnimatorOverride` = `animatorOverrideController` if set, else `archetype.animatorOverrideController`, else null (default controller).
- `ResolvedAttackStaminaMultiplier` = `archetype.attackStaminaMultiplier` (default 1), else 1 — archetype-only, no weapon override.
  `PlayerCombat` multiplies `CombatConfigSO.attackStaminaCost` by it for melee swings (bow shots keep the flat cost).
- `ResolveAttackStep(attackIndex, fallback)` = `archetype.attackSteps[attackIndex − 1]` (`AttackStep`: forward metres +
  start / duration seconds), else `fallback` (`CombatConfigSO.defaultAttackStep`) — archetype-only. Player swing step,
  see `Scripts/Combat/PLAYER_COMBO.md`.
- Always read the `Resolved*` properties in runtime code — never the raw fields.

`EquipableItemSO` defines `public abstract bool CanEquip()` — always `true` in current stories; future stories override for conditional equipping (stat gates, quest requirements). All equippability type-checks use `item is EquipableItemSO` — never `item is WeaponSO || item is ArmorSO`.

---

## ItemSO (base)

`Assets/_Game/ScriptableObjects/Items/ItemSO.cs`

| Field | Type | Purpose |
|---|---|---|
| `itemId` | `string` | Stable save ID — auto-generated, never edit. Clear it on a duplicated asset so it regenerates |
| `itemName` | `string` | Display name in UI |
| `description` | `string` | Shown in detail panel |
| `icon` | `Sprite` | Inventory slot + detail panel icon |
| `maxStacks` | `int` | Max units per inventory slot (default 1 = non-stackable) |
| `IsStackable` | `bool` (computed) | `true` when `maxStacks > 1`; drives stacking logic in `InventorySystem` |
| `worldItemPrefab` | `GameObject` | Prefab spawned when the item is dropped |
| `buyValue` | `int` | Price the player pays a trader (default 1) |
| `sellValue` | `int` | Price a trader pays the player (default 1) |
| `isCraftingMaterial` | `bool` | Smithing material (`Item_IronIngot`): the only items an anvil's grid lists. Detail category **Crafting Material** (after the ingredient rule) |
| `isQuestItem` | `bool` | Quest item: detail category **Quest Item** (wins over every other rule), Misc filter tab. It can't be sold (`NPCTradeUI.SellItem`), dropped (`InventoryUI.DropItem`), put into a container (`ContainerUI.PutItem`) or bound to the action bar (`ActionBarSystem.Assign`); the matching buttons are hidden. It only leaves the player through a dialogue choice's `requiredItem`. One rule: `ItemSO.CanPlayerPart`. Example: `Data/Items/Quest/Item_Notice_*` (Warden's errand), `sellValue` 0, no world prefab |
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
| `_readyToEat` / `IsReadyToEat` | `bool` | Eaten as is (`Item_Apple`, `Item_Bread`): matches `GiveItemCategory.ReadyFood`, the food a dialogue Give choice accepts. Leave off raw fish, mushrooms, herbs and meals (`OnValidate` warns on Meal + readyToEat). Category rules: `GiveItemCategory.cs` (`GiveItemRules.Matches` / `Label`, `GiveItemRulesTests`) |
| `HealAmount` (property) | `float` | Public read accessor |

**Drink (spec-potion-drink-animation):** kind `Potion` **and not `isIngredient`** (`IsDrunk` — raw food is kind Potion too) used by an `IPotionDrinker` (the player's
`PlayerPotionDrinker`) → `TryBeginDrink` (true = started, the caller consumes the item now; the heal lands at the sip via
`ApplyHeal(GameObject)`). NPCs heal through `ApplyHeal(EntityHealth)` (instant full amount, no meal bonus). Meals / raw food
(and a user without a drinker) keep the path below.

`ApplyHeal` (and `OnUse` without a drinker) heals `_healAmount`: **instantly** (potions, `Item_HealingPlant`) or, when `_overTimeEffect` + `_durationMinutes`
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

## ShieldSO (concrete) — spec-shield-offhand-blocking

`Assets/_Game/ScriptableObjects/Items/ShieldSO.cs`, menu **Items → Shield**. Data: `Data/Items/Shields/Shield_<Name>.asset`;
prefabs `Prefabs/Items/Shields/<Name>/` (`_World` / `_Visual` hierarchy, `Mesh` normalisation: `Prefabs/Items/CLAUDE.md` → Shield prefabs).

| Field | Purpose |
|---|---|
| `defenseBonus` (inherited) | Armor — player `Defense`, NPC armor (offhand only) |
| `blockStaminaCost` | Player stamina per absorbed hit (and the minimum to raise the block) |
| `perfectBlockWindow` | Player perfect-block window (s) |
| `heldPose` | `Drawn` pose in the offhand socket — solved so the shield faces forward, world-up, in the in-game block (sword override, `IsBlocking`, UpperBody weight 1, evaluated with `Animator.Update` — **not** `clip.SampleAnimation`: `Block Idle` turns the hips ~90°, the masked layer doesn't) |
| `backPose` | `Sheathed` pose on `BackWeaponSocket` (tuned offsets: `Prefabs/Items/CLAUDE.md` → Shield prefabs) |

Detail panel: category "Shield", rows Block Stamina / Perfect Block, then Defense. Shields: `Shield_RoundWoodShield`
(+3, 20, 0.35 s, 40 g) and `Shield_KiteWoodShield` (+6, 12, 0.2 s, 60 g); both sold by the Storekeeper.

---

## Save IDs — `itemId` + `ItemCatalog`

- `ItemSO.itemId` is the stable save ID (GUID, auto-filled in `OnValidate`) — **never edit it**. Subclasses that
  need `OnValidate` must `override` and call `base.OnValidate()`.
- `Data/Items/ItemCatalog.asset` (`ItemCatalogSO`, `FindById`) lists every item; `Editor/Save/ItemCatalogAutoSync`
  keeps it in sync on item import / move / delete (or `Tools/Save/Sync Item Catalog`). A duplicated item asset keeps
  the original's `itemId` → the sync logs an error naming both paths: clear the copy's `itemId` so it regenerates.

---

## Code Review Checklist — Items

| Severity | Pattern |
|---|---|
| HIGH | New `UsableItemSO` subclass returns `true` from `OnUse` when the use actually failed — item will be consumed incorrectly |
| HIGH | `worldItemPrefab` left unassigned on a droppable item — `InventoryUI.DropItem` refuses the drop (`GameLog.Warn`, item kept) |
| MEDIUM | New item type with numeric fields but no category/stat rows in `ItemDetailFormatter` (+ `ItemDetailFormatterTests`) — detail panel shows only the generic rows. `ItemDetailPanelUI` has no per-type code; never add cases there |
| MEDIUM | `OnUse` calls `GetComponent` on `user` without a null guard — logs no error if component is missing |
| MEDIUM | New stackable item type (`maxStacks > 1`) without verifying `worldItemPrefab` has `ItemPickup` + `Rigidbody` — dropped items must be re-pickable |
