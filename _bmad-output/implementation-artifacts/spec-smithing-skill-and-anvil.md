---
title: 'Smithing skill, anvil station and first forged sword'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: 'd440f87d8026c2b3f44235f376e8f48f513de1e7'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Crafting/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/TEACHING.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Crafting exists only as cooking; the GDD's equipment crafting (skill-gated, recipe-gated) has no first loop, and crafted gear has no reason to beat bought gear.

**Approach:** Edric (Blacksmith) teaches a Smithing skill, then hands over a recipe item for a Forged Short Iron Sword; Iron Ingots (a new crafting-material item) are sold by him; a skill-gated anvil station opens the existing cooking screen in a Recipes-only smithing mode that lists only crafting materials. Crafted bonuses = the recipe outputs its own forged `SwordSO` asset with a higher `damageBonus`, linked to its standard counterpart by `WeaponSO.baseWeapon`.

## Boundaries & Constraints

**Always:** Reuse `CookingStation` / `CookingSystem` / `CookingUI` / `RecipeSO` / `RecipeFact` / `RecipeItemSO` (one screen, two kinds) — no duplicated UI prefab. Teaching chain follows `TEACHING.md` (confirm loops back). Edric's lines follow his `CLAUDE.md` (short, gruff, "Aye", no frogs). Player-visible strings that change must have their EditMode tests updated.

**Ask First:** Renaming `Cooking*` classes to `Crafting*`; any change to `InventorySlot`, `SaveData` or item stacking.

**Never:** Per-copy item data (quality, crafted flag in slots/saves); an Experiment tab for smithing; new dialogue node types or teach-choice reward fields.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Anvil, no skill | Interact anvil without Smithing | Screen stays closed; toast "You need the Smithing skill" | N/A |
| Anvil, skill, no recipe | Smithing learned, recipe unknown | Smithing screen opens on Recipes, no Experiment tab, "No known recipes" | N/A |
| Forge | Recipe known, 2 Iron Ingots | Forge consumes 2 ingots → Forged Short Iron Sword; "Forged once" | Add fails → ingots refunded (existing `TryCraft`) |
| Grid filter | Smithing screen, inventory has apples + ingots | Grid lists ingots only; cooking screen lists apples only | N/A |
| Station without required skill set | `_requiredSkill` null (fireplace) | Opens as today | N/A |
| Detail panel | Forged sword selected | Category "Weapon · Forged"; row "Forged" "+4 over Short Iron Sword" | `baseWeapon` null → no row, category "Weapon" |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Crafting/CookingStation.cs` -- station → open request; per-recipe validity warnings
- `Assets/_Game/Scripts/Crafting/CookingSystem.cs` -- player-side listener; documented home for skill gates
- `Assets/_Game/Scripts/Crafting/CookingRules.cs` -- pure rules (+ `CookingRulesTests`)
- `Assets/_Game/ScriptableObjects/Events/GameEventSO_CookingStationOpenRequest.cs` -- request payload
- `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs` -- tabs, grid filter, craft count text, Cook button
- `Assets/_Game/ScriptableObjects/Items/ItemSO.cs`, `WeaponSO.cs` -- item flags / weapon fields
- `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` -- category + stat rows (+ tests)
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- string toast channels
- `Assets/_Game/Data/NPCs/Innkeeper/Teachings/Lockpicking/`, `Memories/` -- teaching + gate-memory pattern to mirror
- `Assets/_Game/Data/NPCs/BlackSmith/Dialogues/FrogsInsisted/` -- Start node `dialogueFact` → `PlayerRewardSO` pattern
- `Assets/_Game/Prefabs/World/CookingStations/CookingStation_Base.prefab` -- station base to variant from

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Crafting/CraftingKind.cs` -- new enum `Cooking`, `Smithing` -- station / screen mode
- [x] `Scripts/Crafting/CookingRules.cs` -- add `IsMaterialFor(CraftingKind, ItemSO)` (Cooking → `isIngredient`, Smithing → `isCraftingMaterial`) -- one filter rule for grid, station warnings and slots
- [x] `ScriptableObjects/Items/ItemSO.cs` -- add `isCraftingMaterial` bool (tooltip) -- smithing grid filter
- [x] `ScriptableObjects/Items/WeaponSO.cs` -- add `baseWeapon` (`WeaponSO`, "standard counterpart of a forged weapon; null = not forged") -- forged link
- [x] `ScriptableObjects/Events/GameEventSO_CookingStationOpenRequest.cs` -- add `kind`, `requiredSkill` (`SkillFact`, checked via `WorldStateManager.PlayerHasSkill` — no direct `PlayerSkills` reference across systems) to the payload
- [x] `Scripts/Crafting/CookingStation.cs` -- add `_kind`, `_requiredSkill`; raise them; ingredient-flag warning uses `IsMaterialFor`
- [x] `Scripts/Crafting/CookingSystem.cs` -- if `requiredSkill` set and `WorldStateManager.PlayerHasSkill` is false → raise `_onCraftingRefused` ("You need the {displayName} skill"), don't open; pass `kind` to `CookingUI.Open`
- [x] `Scripts/UI/Crafting/CookingUI.cs` -- `Open(recipes, name, kind)`; Smithing hides Experiment tab button, opens on Recipes, rejects experiment input; grid filters by `IsMaterialFor`; Cook button label "Cook"/"Forge" (`_recipeCookLabel`); `FormatCraftCount(count, kind)` → "Never forged / Forged once / Forged N times" -- one screen, two modes
- [x] `Scripts/UI/HUD/NotificationToastUI.cs` + `Data/Events/OnCraftingRefused.asset` -- new string toast channel, wired in `Player.prefab` UICanvas and `CookingSystem`
- [x] `Scripts/UI/Inventory/ItemDetailFormatter.cs` -- `isCraftingMaterial` → category "Crafting Material"; forged weapon → "Weapon · Forged" + row "Forged" "+N over <base name>" (damage delta, omitted ≤ 0)
- [x] `Assets/Tests/EditMode/` -- tests for `IsMaterialFor`, both `FormatCraftCount` kinds, new formatter category / forged row; update callers of old `FormatCraftCount(int)`
- [x] `Data/Items/Item_IronIngot.asset` + `Prefabs/Items/Item_IronIngot.prefab` -- `isCraftingMaterial`, maxStacks 20, buy 15 / sell 5; placeholder grey box prefab (Base Item variant), icon via `Tools/Items/Generate Missing Icons`
- [x] `Data/Items/Weapons/Swords/Weapon_ShortIronSword.asset`, `Weapon_ShortIronSword_Forged.asset` -- `SwordSO`, `Archetype_OneHandedSword`, SwordBase `_World` / `_Visual`; damage 10 / 14, buy 60 / 90; forged `baseWeapon` = standard
- [x] `Data/Recipes/Recipe_ShortIronSword.asset`, `Data/Facts/RecipeFact_ShortIronSword.asset`, `Data/Items/Item_Recipe_ShortIronSword.asset` -- 2× Iron Ingot → forged sword; consumable recipe item
- [x] `Data/Skills/Smithing/Skill_Smithing.asset` (+ `SkillCatalog`), `Data/Player/SkillFact_Smithing_Learned.asset` -- 3 LP; effect "Forge weapons at an anvil from known recipes."
- [x] `Data/NPCs/BlackSmith/Teachings/Smithing/` + `Memories/Mem_Blacksmith_Smithing.asset` -- Start "Teach me smithing" → Text → TeachChoice (Smithing, 80 gold) → Confirm (loops back); topic invalidated once learned
- [x] `Data/NPCs/BlackSmith/Dialogues/SmithingRecipe/` + memory -- one-shot topic unlocked by `SkillFact_Smithing_Learned`; Start `dialogueFact` → `PlayerReward_blacksmith_smithing_recipe` (`Item_Recipe_ShortIronSword` ×1); invalidated by its fact
- [x] Edric shop (`NPC:shop-dialogue` flow) -- "I need iron", unlocked by the skill fact; stock Iron Ingot ×10, Short Iron Sword ×1
- [x] `Prefabs/World/SmithingStations/SmithingStation_Anvil.prefab` -- variant of `CookingStation_Base`, `SM_Anvil` under `Model`, collider refit; `_kind` Smithing, `_requiredSkill` Smithing, prompt "Forge", name "Anvil", recipes [`Recipe_ShortIronSword`]; place in `StartingTown.unity` by Edric's forge
- [x] `Scripts/Crafting/CLAUDE.md`, `Prefabs/CLAUDE.md`, `ScriptableObjects/Items/CLAUDE.md`, `Data/NPCs/BlackSmith/CLAUDE.md` -- document smithing kind, anvil, forged-weapon convention, Edric's new topics

**Acceptance Criteria:**
- Given a new game, when the player talks to Edric, then "Teach me smithing" is offered and learning costs 3 LP + 80 gold.
- Given Smithing learned, when the player re-opens Edric's dialogue, then the recipe topic gives `Item_Recipe_ShortIronSword` once, and the iron shop topic is offered.
- Given the fireplace, when opened, then cooking behaves exactly as before (Experiment tab, ingredients, "Cooked N times").
- Given Quest Explorer, when validating, then no new errors on Edric's memories / facts.

## Design Notes

Forged vs standard is two assets, not instance data: `InventorySlot` is `(ItemSO, Count)` and saves store `itemId`, so a per-copy flag would touch inventory, saves, equipment, trade, containers and drops. A forged asset needs zero system changes; `baseWeapon` only feeds the detail-panel comparison. Future forged weapons: create the standard + `_Forged` pair, recipe outputs the forged one.

The recipe gift uses a follow-up topic because `TeachChoiceOption.confirmNextNode` text nodes can't set facts (only Start / Choice nodes carry `dialogueFact`).

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- all green
- Unity MCP `read_console` after compile -- no errors
- Unity MCP `quest_report` -- no new issues for Edric

**Manual checks:**
- Play StartingTown: learn Smithing, get recipe, buy 2 ingots, use recipe item, forge at anvil; detail panel shows forged row.

## Suggested Review Order

**Smithing mode on the shared crafting screen**

- Entry point: one screen, mode chosen per open; cooking stays the default.
  [`CookingUI.cs:147`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L147)

- Smithing hides the Experiment tab, sets the Forge label, opens on Recipes.
  [`CookingUI.cs:167`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L167)

- Single guard used by tab switching, slot input and experiment cook.
  [`CookingUI.cs:191`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L191)

- Grid lists only the station's materials.
  [`CookingUI.cs:381`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L381)

- One material rule shared by grid, station warnings and tests.
  [`CookingRules.cs:20`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L20)

- "Forged N times" vs "Cooked N times".
  [`CookingUI.cs:453`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L453)

**Station + skill gate**

- Gate lives player-side; checked through WorldStateManager, no cross-system ref.
  [`CookingSystem.cs:46`](../../Assets/_Game/Scripts/Crafting/CookingSystem.cs#L46)

- Station carries kind + required SkillFact; defaults keep the fireplace unchanged.
  [`CookingStation.cs:20`](../../Assets/_Game/Scripts/Crafting/CookingStation.cs#L20)

- Payload extension of the existing open-request event.
  [`GameEventSO_CookingStationOpenRequest.cs:11`](../../Assets/_Game/ScriptableObjects/Events/GameEventSO_CookingStationOpenRequest.cs#L11)

- Refusal toast channel.
  [`NotificationToastUI.cs:114`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L114)

**Forged weapons (crafted bonus)**

- Forged = own asset linked to its standard counterpart; no instance data.
  [`WeaponSO.cs:18`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L18)

- Detail row "+4 over Short Iron Sword" from the damage delta.
  [`ItemDetailFormatter.cs:92`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L92)

- New material flag and its detail category.
  [`ItemSO.cs:21`](../../Assets/_Game/ScriptableObjects/Items/ItemSO.cs#L21)
  [`ItemDetailFormatter.cs:62`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L62)

**Data (Edric, items, anvil)**

- Teaching chain, recipe gift, shop and anvil are indexed in Edric's sheet.
  [`BlackSmith/CLAUDE.md`](../../Assets/_Game/Data/NPCs/BlackSmith/CLAUDE.md)

- Smithing loop documentation.
  [`Crafting/CLAUDE.md`](../../Assets/_Game/Scripts/Crafting/CLAUDE.md)

**Tests**

- Material rule, forged row, smithing count text.
  [`CookingRulesTests.cs:142`](../../Assets/Tests/EditMode/CookingRulesTests.cs#L142)
  [`ItemDetailFormatterTests.cs:187`](../../Assets/Tests/EditMode/ItemDetailFormatterTests.cs#L187)
  [`RecipeItemTests.cs:187`](../../Assets/Tests/EditMode/RecipeItemTests.cs#L187)
