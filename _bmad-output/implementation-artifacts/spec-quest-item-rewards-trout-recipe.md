---
title: 'Quest item rewards: Guard gives the Apple Trout Sandwich recipe'
type: 'feature'
created: '2026-10-24'
status: 'done'
baseline_commit: '92ff3d5'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/Crafting/CLAUDE.md'
  - '{project-root}/Assets/_Game/ScriptableObjects/Items/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/Items/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A `PlayerRewardSO` can grant XP, LP, gold and stats, but no items. Gold from a quest reward also shows no toast. The Guard's spider quest has no memorable reward.

**Approach:** Add item rewards (`ItemSO` + count) to `PlayerRewardSO`. `PlayerRewards` adds them to the player inventory and raises toast channels for reward items ("Received: …") and reward gold ("Gold +N"). Content: a new Trout ingredient, the Apple Trout Sandwich (Bread + Apple + Trout, first meal gives +1 Strength), and its recipe item, which `PlayerReward_SpiderClear_Completed` grants. The Guard's turn-in dialogue gets a separate node announcing the recipe.

## Boundaries & Constraints

**Always:**
- Item rewards are applied after gold in `ApplyRewards`, `count` times through `InventorySystem.AddItem`. Entries with a null item or count ≤ 0 are skipped with a `GameLog.Warn`. If no inventory is assigned, items are skipped with a warning (logged in `Awake`) and no item toast is raised.
- Item toast text: `Received: <itemName>`, or `Received: <itemName> ×N` when N > 1. It shows once per reward entry, only for `PlayerRewards` items (never for pickups, trade or crafting).
- The gold toast `Gold +N` shows only for reward gold (`PlayerRewards`), never for trade / `GoldSystem` changes.
- Trout: `Item_Trout` (`PotionItemSO`), "Trout", ingredient, consumable, heals 10, stack 20, buy 6 / sell 2. The world prefab nests `Hivemind/.../SM_Fish_01a.prefab`. The Merchant stocks 5.
- Sandwich: `Item_AppleTroutSandwich` (`PotionItemSO`), "Apple Trout Sandwich", ingredient, consumable, heals 40, first-meal `_permanentBonuses` = Strength +1, stack 20, buy 18 / sell 8.
- Recipe: `Recipe_AppleTroutSandwich` (Bread, Apple, Trout) + `RecipeFact_AppleTroutSandwich`, added to `CookingStation_Base._recipes`, so it can still be found by experimenting.
- Recipe item: `Item_Recipe_AppleTroutSandwich` (`RecipeItemSO`), "Recipe: Apple Trout Sandwich", buy 60 / sell 15, stack 10. It is unique: no shop or loot source. The world prefab copies the Apple Mushroom recipe prefab.
- `PlayerReward_SpiderClear_Completed` keeps 200 XP and 100 gold and adds 1× `Item_Recipe_AppleTroutSandwich`.
- Dialogue: `Text_Guard_SpiderReward` → new `Text_Guard_SpiderReward_Recipe` (separate node): the Guard hands over his own recipe (trout, apple, good bread) that will make the player stronger.
- Every new item gets an icon prompt for the user (placeholder icon until then).

**Ask First:** Changing quest completion flow / facts; touching `InventorySystem`; save-format changes.

**Never:** `generate_image`; item rewards for `KilledFact` base XP; selling the Guard's recipe item; committing `LiberationSans SDF - Fallback.asset`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Turn in | Spider quest completed via Guard dialogue | +200 XP, +100 gold, recipe item in inventory; toasts "Experience +200", "Gold +100", "Received: Recipe: Apple Trout Sandwich" | N/A |
| Stack reward | Item entry count 3 | 3 units added, one toast "Received: X ×3" | N/A |
| Bad entry | Item null or count 0 | Entry skipped, other rewards still applied | `GameLog.Warn` |
| No inventory | `_inventory` unassigned | XP/gold applied, items skipped | `GameLog.Warn` in Awake |
| Recipe known already | Discovered by experiment before turn-in | Item still given; Use disabled ("Already known") | N/A |
| Eat sandwich first time | — | +40 HP, +1 Strength, toast "Permanent bonus: +1 Strength" | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs` -- reward data (add `ItemReward` list)
- `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` -- `ApplyRewards`; Player.prefab component
- `Assets/_Game/Scripts/Editor/PlayerRewardSOEditor.cs` -- custom inspector (draw new list)
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- toast channels / formatters
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `PlayerRewards` + `InventorySystem` + nested `UICanvas` toast wiring
- `Assets/_Game/Data/Events/` -- `GameEventSO_*` channel assets
- `Assets/_Game/Data/Items/`, `Data/Recipes/`, `Data/Facts/`, `Prefabs/Items/` -- content, mirrors the Apple Mushroom set
- `Assets/_Game/Prefabs/World/CookingStations/CookingStation_Base.prefab` -- `_recipes`
- `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- `SetStock` (Merchant, StartingTown instance)
- `Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderReward/` -- turn-in nodes
- `Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderOffer/Choice_Guard_SpiderOffer.asset` -- second turn-in path ("I've already killed the spiders" sets the same turn-in fact); now continues to `Text_Guard_SpiderReward` so it also announces the recipe

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Rewards/PlayerRewardSO.cs` -- add `[Serializable] struct ItemReward { ItemSO item; int count; }`, `_itemRewards` list + `ItemRewards` getter; static `FormatItemReceived(string name, int count)` -- data + pure toast text
- [x] `Scripts/Editor/PlayerRewardSOEditor.cs` -- draw `_itemRewards` -- editable in inspector
- [x] `Scripts/Player/Progression/PlayerRewards.cs` -- optional `InventorySystem _inventory`, `GameEventSO_String _onRewardItemReceived`, `GameEventSO_Int _onRewardGoldGained`; apply items + raise channels -- behaviour
- [x] `Scripts/UI/HUD/NotificationToastUI.cs` -- subscribe `_onRewardGoldGained` (`FormatGold` → "Gold +N") and `_onRewardItemReceived` (verbatim) -- toasts
- [x] `Data/Events/OnRewardItemReceived.asset`, `OnRewardGoldGained.asset` + wiring in `Player.prefab` -- channels
- [x] Content: `Item_Trout`, `Item_AppleTroutSandwich`, `Item_Recipe_AppleTroutSandwich` (+ world prefabs), `Recipe_AppleTroutSandwich`, `RecipeFact_AppleTroutSandwich`; station `_recipes`; sync `ItemCatalog`; Merchant Trout ×5; reward asset item entry -- content
- [x] Guard dialogue: reword `Text_Guard_SpiderReward` (coin), new `Text_Guard_SpiderReward_Recipe` linked after it -- narrative
- [x] `Assets/Tests/EditMode/` -- tests for `FormatItemReceived`, `FormatGold`, and skipping bad entries where pure -- matrix rows
- [x] Docs: `Scripts/Player/Progression/CLAUDE.md`, `Scripts/UI/HUD/CLAUDE.md`, `Data/Quests/CLAUDE.md`, `Scripts/Crafting/CLAUDE.md` -- item rewards, new toasts, new recipe

**Acceptance Criteria:**
- Given the Quest Explorer / `quest_report` for Quest_SpiderClear, when validated, then there are no new errors and `RecipeFact_AppleTroutSandwich` is not reported as never set.
- Given Play mode, when the Guard's turn-in plays, then the player reads the recipe node before the dialogue ends and the inventory shows the recipe item.
- Given the console, after compile, then there are no errors and EditMode tests pass.

## Design Notes

The item toast reports the units actually added. `InventorySystem.AddItem` has no capacity limit, so this equals `count` today. The review deferred three pre-existing issues to `deferred-work.md`: dialogue rewards re-fire, the 2-vs-5 spider mismatch, and a load-time reseed race. `PlayerRewardSOEditor` warns when items are put on a Dialogue reward.


The item toast is pre-formatted in `Game.Progression` (`PlayerRewardSO.FormatItemReceived`) and shown verbatim, like `OnLockpickFailed`. That keeps Progression from depending on UI and lets a single string channel carry both name and count. Gold uses an int channel, formatted by the HUD like XP.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass
- Unity MCP `quest_report` (Quest_SpiderClear) -- expected: no new issues

**Manual checks:**
- Play StartingTown: kill the spiders, turn in to the Guard, check the 3 toasts + the recipe node, learn the recipe, buy Trout, cook and eat the sandwich (+1 Strength toast).

## Suggested Review Order

**Item rewards**

- Entry point: items added after gold, one toast per entry with the units added.
  [`PlayerRewards.cs:180`](../../Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs#L180)

- Gold toast raised only from the reward path, items skipped without an inventory.
  [`PlayerRewards.cs:164`](../../Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs#L164)

- Data: `ItemReward { item, count }` and pure toast text (×N only when N > 1).
  [`PlayerRewardSO.cs:20`](../../Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs#L20)
  [`PlayerRewardSO.cs:53`](../../Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs#L53)

- Inspector warns: Dialogue rewards re-fire on every replay (item farming).
  [`PlayerRewardSOEditor.cs:39`](../../Assets/_Game/Scripts/Editor/PlayerRewardSOEditor.cs#L39)

**HUD toasts**

- Item text shown verbatim; gold formatted by the HUD, like XP.
  [`NotificationToastUI.cs:102`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L102)
  [`NotificationToastUI.cs:131`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L131)

**Spider quest content**

- Reward grants the Guard's recipe item.
  [`PlayerReward_SpiderClear_Completed.asset:23`](../../Assets/_Game/Data/Rewards/PlayerReward_SpiderClear_Completed.asset#L23)

- Turn-in: coin line → new recipe node; the offer's "already killed" path joins it.
  [`Text_Guard_SpiderReward.asset:16`](../../Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderReward/Text_Guard_SpiderReward.asset#L16)
  [`Choice_Guard_SpiderOffer.asset:29`](../../Assets/_Game/Data/NPCs/NPC_Guard/Dialogues/SpiderOffer/Choice_Guard_SpiderOffer.asset#L29)

- Recipe (Bread + Apple + Trout) and the sandwich's +1 Strength first-meal bonus.
  [`Recipe_AppleTroutSandwich.asset:15`](../../Assets/_Game/Data/Recipes/Recipe_AppleTroutSandwich.asset#L15)
  [`Item_AppleTroutSandwich.asset:26`](../../Assets/_Game/Data/Items/Item_AppleTroutSandwich.asset#L26)

**Peripherals**

- Tests: stack count, bad entries, no inventory, gold toast.
  [`PlayerRewardItemTests.cs:78`](../../Assets/Tests/EditMode/PlayerRewardItemTests.cs#L78)
  [`PlayerRewardItemTests.cs:110`](../../Assets/Tests/EditMode/PlayerRewardItemTests.cs#L110)
