# CLAUDE.md — Assets/_Game/Scripts/Crafting

> Loaded when Claude accesses files in this folder. Recipes and crafting stations (cooking, smithing). Namespace: `Game.Crafting`.
> The cooking screen lives in `Scripts/UI/Crafting/` (`Game.UI`); the station prefab contract in `Prefabs/CLAUDE.md`.

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
  `[Recipe]` setter) and **recipe items** (`RecipeItemSO`, e.g. `Item_Recipe_AppleMushroomSandwich` sold by the
  Merchant; `Item_Recipe_AppleTroutSandwich` is unique, the Guard's spider-quest reward via `PlayerRewardSO._itemRewards`): `OnUse` sets the fact, raises `OnRecipeLearned` (toast "Recipe learned: …") and is consumed; it is
  rejected (`CanUse()` false → Use disabled, item kept, only sellable) once the recipe is known. Quest Explorer
  doesn't list recipe items as setters yet (the recipe is enough to avoid "never set").
- `RecipeFact._recipeId` is the save key — never rename it once shipped. It also keys the craft count.

## Craft count (saved, not a fact)

- `WorldStateManager.AddRecipeCraft` / `GetRecipeCraftCount` (keyed by `RecipeFact.ToString()`), a separate int store
  so facts stay bool-only. Saved as the optional `SaveData.recipeCrafts` (`CaptureRecipeCrafts` / `RestoreRecipeCrafts`;
  null in older saves → all 0, no version bump). No event.
- `CookingUI` counts every successful cook of a recipe's output (Recipes tab **or** a matching Experiment cook — not
  the failed dish, not a refunded craft) and shows `FormatCraftCount` ("Never cooked" / "Cooked once" / "Cooked N
  times") in `_recipeCraftCountText` under the recipe detail.

## Smithing (anvil)

- `SmithingStation_Anvil` (`Prefabs/World/SmithingStations/`, by Edric in `StartingTown`): `CookingStation` with
  `_kind = Smithing`, `_requiredSkill = SkillFact_Smithing_Learned`, prompt "Forge", name "Anvil".
- Smithing = **Recipes tab only** (Experiment tab button hidden, experiment input rejected), grid lists
  `isCraftingMaterial` items, button "Forge", count "Never forged / Forged once / Forged N times".
- Recipes are learned only from recipe items (`Item_Recipe_ShortIronSword`, given once by Edric after Smithing is
  learned — `Data/NPCs/BlackSmith/CLAUDE.md`). Ingots: Edric's shop.
- **Crafted bonus = a forged asset**, not instance data: the recipe outputs `Weapon_<Name>_Forged` (same name, higher stats,
  `baseWeapon` → the standard `Weapon_<Name>`; detail category "Weapon · Forged"). Details: `ScriptableObjects/Items/CLAUDE.md`.

## First-meal bonus

A cooked food can grant a one-time permanent bonus (e.g. Apple Mushroom Sandwich: +5 Max HP; Apple Trout Sandwich — Bread + Apple + Trout, Trout sold by the Merchant: +1 Strength) the first time it is eaten — data on
the food item, not on the recipe, so a bought / looted copy counts too. Details: `ScriptableObjects/Items/CLAUDE.md`
(`PotionItemSO`); claim key in `Data/Facts/CLAUDE.md`.

---

## Cooking screen (`Scripts/UI/Crafting/`)

- `CookingUI` (`IScreenPanel`, nested in `UICanvas`, inactive): shared player grid + **Experiment** / **Recipes** tabs
  (cooking opens on Experiment, inactive tab tinted; smithing — see above — has Recipes only). Same lifecycle as `ContainerUI` (Esc / `OnPlayerDied` close, input
  disposed in `OnDestroy`); `SaveSystem` refuses save / load while `IsOpen`.
- **Player grid lists the station's materials only** (both tabs; `CookingRules.IsMaterialFor` — ingredients for cooking, `isCraftingMaterial` for smithing). Each `ItemSlotUI` keeps its real inventory index as `SlotIndex`, so
  grid child position ≠ inventory index — resolve a clicked slot with `FindGridSlot(SlotIndex)`, never `GetChild(index)`.
- **Experiment:** only `ItemSO.isIngredient` items are accepted (others, e.g. a meal dragged from the action bar,
  show "<name> isn't an ingredient"). Cooked meals (`PotionItemSO` `Meal`) are never ingredients. 3
  `CookingIngredientSlotUI` (`_slotIndex` 0–2) **references** (`ItemSO`, like action bar slots), reset on every open. Set by dropping an inventory / action bar slot or double-clicking a grid slot (first empty slot);
  cleared by right-click, double-click or dragging out. Slots show the icon only — no stack count (a cook uses one unit). Cook consumes one unit per filled slot → the matching station recipe's output
  (learning it the first time: "New recipe: …") or `_failedDish` (`Item_ReplenishingStew`). The preview never reveals
  an unknown recipe ("Unknown result"). After a cook, slots whose item ran short clear.
- **Recipes:** station recipes ∩ known, as `CookingRecipeRowUI` rows (`Prefabs/UI/Cooking/CookingRecipeRow.prefab`);
  the detail lists ingredients `have/need` (missing tinted) and Cook needs them all.
- Every cook goes through `TryCraft`: consume all, add the result, refund everything if a step fails; then
  `OnActionBarUsed(REFRESH_ONLY_SLOT)` so the HUD bar and this screen refresh.
