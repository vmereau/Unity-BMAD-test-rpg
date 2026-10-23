---
title: 'Cooking v4: one-time permanent stat bonus on first meal'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: '2b6393e'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Crafting/CLAUDE.md'
  - '{project-root}/Assets/_Game/ScriptableObjects/Items/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Inventory/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Cooked meals only heal, so cooking a recipe gives no lasting payoff.

**Approach:** A food (`PotionItemSO`) can list permanent stat bonuses. The first time the player eats one, they get the bonuses permanently and see a toast. The claim is stored as a saved world-state key for that item. The item detail shows the bonus, grayed out once it has been gained. To start, Apple Mushroom Sandwich gives +5 Max HP.

## Boundaries & Constraints

**Always:**
- Bonus stats: `MaxHealth`, `Strength`, `Dexterity`, `Endurance`, `Intelligence`, and a meal can list several. The four base stats go through `PlayerStats.UpgradeStat`, so they are already saved. MaxHealth raises both the max and the current HP by the amount, on top of the normal heal.
- Claim key: `Meal.BonusClaimed.{itemId}`, stored in `WorldStateManager._worldFacts` (so it is saved) through a typed API only (`ClaimMealBonus(ItemSO)` / `IsMealBonusClaimed(ItemSO)`). It raises no event and is not a quest / memory fact, the same as the dialogue Seen markers.
- First eat: heal, apply each bonus, claim, then raise the new `GameEventSO_String` `OnMealBonusGained` with the bonus text. The toast reads "Permanent bonus: +5 Max HP". Later eats only heal. Eating stays possible after the claim.
- Bonus text is shared by the toast and the detail row (pure helper), e.g. "+5 Max HP", "+5 Max HP, +1 Strength".
- Detail row (every host, via `ItemDetailFormatter`, which stays pure because the claim check is injected): label "First meal", value = bonus text. Unclaimed → Positive. Claimed → value "<text> (gained)" in a new `StatPolarity.Muted` (gray `_mutedColor` on `ItemDetailPanelUI`). No check injected (Core missing) → Positive.
- `PlayerHealth.MaxHealth` = `baseHealth` + saved bonus. `Heal`, `RestoreHealth` and the HUD bar use `MaxHealth`. The bonus is saved as the optional `PlayerSaveData.bonusMaxHealth` (int, missing → 0, no version bump) and restored before health.
- Content: `Item_AppleMushroomSandwich` → `MaxHealth +5`, `OnMealBonusGained` wired on the item and on `NotificationToastUI`.

**Ask First:** Changing existing save fields, bumping the save version, or changing `OnPlayerHealthChanged`'s payload.

**Never:** Temporary buffs, bonuses on non-food items, a new Fact SO type, new UI sprites, prefab layout changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First meal | Sandwich eaten, unclaimed, HP 80/100 | Heal, then max 105 and current +5, claimed, toast "Permanent bonus: +5 Max HP" | N/A |
| Second meal | Claimed | Heal only, cap 105, no toast | N/A |
| Detail | Claimed / unclaimed | "First meal: +5 Max HP (gained)" gray / "+5 Max HP" green | N/A |
| Save / load | Claimed, max 105, save, load | Max 105, claimed row, no second bonus | N/A |
| Old save | No `bonusMaxHealth` | Max = base | N/A |
| No Core | WSM missing | Heals, no bonus, item consumed | `GameLog.Warn` |
| Dead | Player dead | Rejected, nothing claimed | N/A |
| Bad entry | amount ≤ 0 | Entry skipped | `GameLog.Warn` in `OnValidate` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- food `OnUse` (heal), gets the bonuses
- `Assets/_Game/Scripts/Player/PlayerHealth.cs`, `PlayerStats.cs` -- max HP from config; `UpgradeStat`
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs`, `Scripts/Core/Save/SaveData.cs` -- player capture / restore
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `MarkDialogueSeen` pattern for unevented saved keys
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs`, `ItemStatLine.cs`, `ItemDetailPanelUI.cs` -- rows, polarity, colors
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs`, `HealthBarUI.cs` -- toast channels, bar max from config

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/MealBonus.cs` (new) -- `MealBonusStat` enum, `[Serializable] MealBonus { stat, amount }`, static `MealBonusText.Format(IReadOnlyList<MealBonus>)` -- data + shared text
- [x] `ScriptableObjects/Items/PotionItemSO.cs` -- `_permanentBonuses`, `_onMealBonusGained`; `HasPermanentBonus`; apply-once in `OnUse`; `OnValidate` (call base) -- the feature
- [x] `Scripts/Core/State/WorldStateManager.cs` (+ `WorldFactPrefix.Meal`) -- `ClaimMealBonus` / `IsMealBonusClaimed` -- saved flag
- [x] `Scripts/Player/PlayerHealth.cs` -- `_bonusMaxHealth`, `AddMaxHealthBonus(int)` (max + current, raises health event), `BonusMaxHealth`, `RestoreMaxHealthBonus(int)`; `MaxHealth` everywhere -- stat
- [x] `Scripts/Core/Save/SaveData.cs`, `Scripts/Player/PlayerSaveAdapter.cs` -- `bonusMaxHealth` capture / restore before health -- persistence
- [x] `Scripts/UI/HUD/HealthBarUI.cs` + Player prefab -- optional `PlayerHealth` ref for the max (config fallback) -- bar fills correctly over 100
- [x] `Scripts/UI/Inventory/ItemStatLine.cs`, `ItemDetailFormatter.cs`, `ItemDetailPanelUI.cs` -- `Muted`, "First meal" row via injected `Func<ItemSO,bool>`, `_mutedColor` -- detail
- [x] `Scripts/UI/HUD/NotificationToastUI.cs` + `Data/Events/OnMealBonusGained.asset` wired on the HUD -- toast
- [x] `Data/Items/Item_AppleMushroomSandwich.asset` -- MaxHealth +5, event -- content
- [x] `Assets/Tests/EditMode/` -- formatter rows, `MealBonusText`, first/second eat, old-save JSON -- matrix
- [x] `ScriptableObjects/Items/CLAUDE.md`, `Scripts/Crafting/CLAUDE.md`, `Data/Facts/CLAUDE.md` (claim key next to the Seen markers) -- docs

**Acceptance Criteria:**
- Given the inventory detail shows the sandwich, when it is eaten for the first time, then the row turns gray "(gained)" with no reselect.
- Given max HP is 105, when the Character screen opens, then it shows "HP: x / 105".

## Design Notes

The bonus lives on the food item, not the recipe, so a meal bought or looted gives the same first-meal bonus. The claim is a stored key, not a Fact SO (per the user's choice), so no asset is needed per meal. The max-HP bonus is saved on its own. Deriving it from the claims would need a catalog walk inside `PlayerHealth`. Base stats need no new save field.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass

**Manual checks:**
- Play StartingTown: cook and eat a sandwich (toast, bar, detail turns gray), eat a second one, then save and load.

## Suggested Review Order

**First-meal bonus**

- Entry point: heal, then apply-once bonus; every target checked before claiming.
  [`PotionItemSO.cs:60`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L60)

- Review fix: empty itemId would re-grant forever — bail before applying.
  [`PotionItemSO.cs:70`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L70)

- Review fix: no config / no PlayerStats → nothing applied, nothing claimed.
  [`PotionItemSO.cs:78`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L78)

- Claim key stored with facts, no event (like Seen markers).
  [`WorldStateManager.cs:168`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L168)

- Data + shared text for toast and detail row.
  [`MealBonus.cs:22`](../../Assets/_Game/ScriptableObjects/Items/MealBonus.cs#L22)

**Max health**

- Max = config base + saved bonus; Heal / RestoreHealth clamp to it.
  [`PlayerHealth.cs:37`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L37)

- Bonus raises max and current together.
  [`PlayerHealth.cs:117`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L117)

- Restored before health so it isn't clamped to the base.
  [`PlayerSaveAdapter.cs:144`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L144)

- Optional save field — older saves → 0, no version bump.
  [`SaveData.cs:56`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L56)

- HUD bar reads PlayerHealth max (Player.prefab override), clamped.
  [`HealthBarUI.cs:59`](../../Assets/_Game/Scripts/UI/HUD/HealthBarUI.cs#L59)

**Detail row + toast**

- "First meal" row, Muted "(gained)" once claimed; claim check injected.
  [`ItemDetailFormatter.cs:142`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L142)

- Panel injects the WorldStateManager check; gray for Muted.
  [`ItemDetailPanelUI.cs:123`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs#L123)

- Review fix: reusable items repaint the detail after use too.
  [`InventoryUI.cs:143`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L143)

- Toast "Permanent bonus: …" on the new `OnMealBonusGained` channel.
  [`NotificationToastUI.cs:119`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L119)

**Peripherals**

- Tests: first / second meal, guards, save fallback, rows, toast text.
  [`MealBonusTests.cs:105`](../../Assets/Tests/EditMode/MealBonusTests.cs#L105)
