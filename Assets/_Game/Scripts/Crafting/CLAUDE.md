# CLAUDE.md — Assets/_Game/Scripts/Crafting

> Loaded when Claude accesses files in this folder. Recipes and crafting stations (cooking, smithing). Namespace: `Game.Crafting`.
> The cooking / smithing screen (`CookingUI` & co.) lives in `Scripts/UI/Crafting/` (`Game.UI`) — documented in
> `Scripts/UI/Crafting/CLAUDE.md`; the station prefab contract in `Prefabs/CLAUDE.md`.

---

## What's here

| File | Role |
|------|------|
| `RecipeSO` (`ScriptableObjects/Crafting/`) | 1–3 `ingredients` (order-free; repeat an item to need several) → one `output`, plus `knownFact` (`RecipeFact`). Assets in `Data/Recipes/` (`Recipe_<Output>`), facts in `Data/Facts/` (`RecipeFact_<Output>`). |
| `CookingRules` | **Static, pure.** `FindRecipe(recipes, slots)` (exact multiset match, empty slots / null / incomplete recipes skipped), `HasIngredients(needs, countOf)` (duplicates counted), `EvaluateExperiment(slots, countOf)` → `ExperimentState` `Empty` / `NotEnough` / `Ready`. Tested by `CookingRulesTests`. |
| `CraftingKind` | `Cooking` / `Smithing` — the station's mode. `CookingRules.IsMaterialFor(kind, item)`: cooking → `isIngredient`, smithing → `isCraftingMaterial` (grid filter + station warnings). |
| `CookingStation` | `IInteractable` on the station root (cooking **and** smithing — the `Cooking*` names are kept so `m_Script` refs survive): prompt, name tag, `_recipes` (`RecipeSO[]` = what can be crafted here; `Awake` warns on incomplete recipes / missing `knownFact` / a material without the kind's flag), `_kind`, `_requiredSkill` (`SkillFact`, null = anyone). `Interact()` raises `OnCookingStationOpenRequested` (`GameEventSO_CookingStationOpenRequest { recipes, stationName, kind, requiredSkill }`). Behaviour only — the look is the prefab's `Model` child. |
| `CookingSystem` | Player-side listener on `Player.prefab` (next to `ContainerSystem`) → `CookingUI.Open(recipes, name, kind)`. Station gates live here: a `requiredSkill` the player lacks (`WorldStateManager.PlayerHasSkill`, no Core → refused) raises `OnCraftingRefused` ("You need the Smithing skill", `FormatSkillRequired`) and the screen stays closed. |

---

## Known recipes = stored facts

- Knowing a recipe is the stored `RecipeFact` (`Recipe.Known.{recipeId}`) in `WorldStateManager` — no player-side list,
  no recipe catalog, no `SaveData` field: facts are already saved / restored, and quest parts, memory conditions and dialogue
  read it like any `Fact` (`PlayerRewardSO` does not — its `RewardFactType` has no Recipe entry yet). Write it only through `WorldStateManager.SetRecipeKnown`; read with `IsRecipeKnown`.
- Setters: the Experiment tab's first successful cook of a recipe (Quest Explorer shows the recipe as the fact's
  `[Recipe]` setter) and **recipe items** (`RecipeItemSO`, e.g. `Item_Recipe_AppleMushroomSandwich` and
  `Item_Recipe_AppleTroutSandwich`, both sold by the Storekeeper): `OnUse` sets the fact, raises `OnRecipeLearned` (toast "Recipe learned: …") and is consumed; it is
  rejected (`CanUse()` false → Use disabled, item kept, only sellable) once the recipe is known. Quest Explorer
  doesn't list recipe items as setters yet (the recipe is enough to avoid "never set").
- `RecipeFact._recipeId` is the save key — never rename it once shipped. It also keys the craft count.

---

## Craft count (saved, not a fact)

- `WorldStateManager.AddRecipeCraft` / `GetRecipeCraftCount` (keyed by `RecipeFact.ToString()`), a separate int store
  so facts stay bool-only. Saved as the optional `SaveData.recipeCrafts` (`CaptureRecipeCrafts` / `RestoreRecipeCrafts`;
  null in older saves → all 0, no version bump). No event.
- Shown on the recipe card: `Scripts/UI/Crafting/CLAUDE.md` → Craft count.

---

## Smithing (anvil)

- `SmithingStation_Anvil` (`Prefabs/World/SmithingStations/`, by Edric in `StartingTown`): `CookingStation` with
  `_kind = Smithing`, `_requiredSkill = SkillFact_Smithing_Learned`, prompt "Forge", name "Anvil".
- Smithing screen mode (Recipes tab only, Forge button / count): `Scripts/UI/Crafting/CLAUDE.md`.
- Recipes are learned only from recipe items (`Item_Recipe_ShortIronSword`, given once by Edric after Smithing is
  learned — `Data/NPCs/BlackSmith/CLAUDE.md`). Ingots: Edric's shop.
- **Crafted bonus = a forged asset** (`Weapon_<Name>_Forged`), not instance data — owned by `ScriptableObjects/Items/CLAUDE.md` → Forged weapons.

---

## First-meal bonus

A cooked food can grant a one-time permanent bonus (e.g. Apple Mushroom Sandwich: +5 Max HP; Apple Trout Sandwich — Bread + Apple + Trout, Trout sold by the Storekeeper: +1 Strength) the first time it is eaten — data on
the food item, not on the recipe, so a bought / looted copy counts too. Details: `ScriptableObjects/Items/CLAUDE.md`
(`PotionItemSO`); claim key in `Data/Facts/CLAUDE.md`.

