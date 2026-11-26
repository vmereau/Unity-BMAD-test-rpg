---
title: 'Cooking quest prerequisites for Tob''s tutorial chain'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: '45711235'
context:
  - '{project-root}/docs/World/Quests/WorkersRation.md'
  - '{project-root}/docs/World/Quests/WorkersSandwich.md'
  - '{project-root}/docs/World/Quests/CooksExperiment.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tob's quest chain (WorkersRation → WorkersSandwich → CooksExperiment) needs code that doesn't exist yet. Nothing can test whether a meal has been eaten or a recipe cooked. Nothing records the first meal found by experimenting. No dialogue topic can hand out an item a limited number of times per day.

**Approach:** Add two computed facts, `MealEatenFact` and `CraftedFact`. Both raise `OnFactChanged` when they first become true. Have CookingUI set a serialized `WorldFact` on the first meal it discovers. Add a generic daily-ration topic node: its count is stored in WorldStateManager, saved, and reset at the end of the NPC's routine Sleep block. Quest data is out of scope (`/quests:implement`).

## Boundaries & Constraints

**Always:** Build fact keys only inside WorldStateManager / the fact `ToString()`. Use `GameLog` with a TAG. The ration count is generic: per topic node, with any item and any count. New `SaveData` fields are optional, so older saves load with no version bump. Put the pure logic in static rules classes with EditMode tests.

**Ask First:** Changing `DialogueUI` beyond the topic label. Bumping the save version. Editing any scene.

**Never:** Create quest / dialogue / memory / item assets (`/quests:implement`, `/items:create`). The only exception is `WorldFact_Cooking_MealDiscovered`, wired on `CookingUI.prefab`. Never reset the ration at midnight. Never make a computed fact writable.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Meal eaten first time | Meal with a bonus eaten | `ClaimMealBonus` raises `OnFactChanged("Meal.Eaten.{itemId}", true)`; `MealEatenFact` true | — |
| Meal without bonus | `_meal` has no bonus / not a Meal | never true | `OnValidate` warning + validator V19 |
| First craft | count 0 → 1 | `OnFactChanged("Crafted.{recipeId}", true)` once; later crafts raise nothing | null recipe → false, V19 |
| Meal discovered | Experiment learns a recipe whose output is `PotionItemSO.Kind == Meal` | WorldFact set (once) | null field → one warning in `Awake` |
| Not a discovery | known recipe, failed dish, potion output, recipe item | fact unchanged | — |
| Ration given | awake, not in Sleep block, N > 0 left | item added, count −1, normal chain plays | inventory refuses the item → nothing counted, warning, back to the topic list |
| None left | 0 left | `noneLeftLine` shown, nothing given | — |
| Woken | NPC's current routine activity = Sleep | `notNowLine` shown, nothing given | no `NpcRoutine` → never refused for this |
| Reset | clock passes the end hour of the NPC's Sleep block (live, sleep skip, load) | count back to per-day | no Sleep block → reset hour 0 + one warning; no clock → never resets |
| Save / load | some taken | remaining restored | missing field → all full |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- fact store; `ClaimMealBonus`, `AddRecipeCraft`, `GetFact` switch, capture/restore
- `Assets/_Game/Scripts/Core/State/WorldFactPrefix.cs` -- key prefixes
- `Assets/_Game/ScriptableObjects/Facts/` -- fact types (`RecipeFact`, `HourWindowFact` pattern)
- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- `Kind`, `HasPermanentBonus`
- `Assets/_Game/Scripts/UI/Crafting/CookingUI.cs` -- `CookExperiment` (learned branch)
- `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs` -- topic base
- `Assets/_Game/Scripts/World/DialogueSystem.cs` -- `StartTopic`, `_currentRequester`, runtime line node
- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- `AddStartNodeButton` label
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs`, `NpcRoutineBlock.cs` -- `CurrentActivity`, blocks (Sleep end hour)
- `Assets/_Game/Scripts/Core/Save/SaveData.cs`, `SaveSystem.cs` -- optional `recipeCrafts` pattern
- `Assets/_Game/Editor/QuestExplorer/` -- `QuestIndexCollector`, `QuestReferenceIndex.MapRecipeSetters`, `QuestIndexTypes.FactLinkSource`, `QuestDetailView` tags/type names, `QuestValidator`

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Facts/MealEatenFact.cs` -- `_meal` (`PotionItemSO`), key `Meal.Eaten.{itemId}`, `OnValidate` warns when meal missing / no bonus -- computed fact
- [x] `ScriptableObjects/Facts/CraftedFact.cs` -- `_recipe` (`RecipeFact`), key `Crafted.{recipeId}` (add `RecipeFact.RecipeId`) -- computed fact
- [x] `WorldFactPrefix.cs` -- add `Crafted`, `Ration`
- [x] `WorldStateManager.cs` -- `GetFact` cases; `ClaimMealBonus` raises the MealEatenFact key event on the first claim; `AddRecipeCraft` raises the Crafted key on 0 → 1; ration store (`GetRationTaken` / `TakeRation` keyed by node id, period-based) + `CaptureDailyRations` / `RestoreDailyRations` -- one owner for keys and events
- [x] `Scripts/Core/State/DailyRationRules.cs` -- pure `Period(now, wakeHour)`, `Remaining(perDay, storedPeriod, taken, currentPeriod)`, `Label(text, remaining)` -- testable core
- [x] `SaveData.cs` / `SaveSystem.cs` -- optional `Dictionary<string, DailyRationSaveData> dailyRations` captured / restored / cleared like `recipeCrafts`
- [x] `ScriptableObjects/Dialogue/DailyRationDialogueNode.cs` -- `: StartDialogueNode`; `item`, `perDay` (≥1, default 2), `noneLeftLine`, `notNowLine`; `OnValidate` warnings -- the topic
- [x] `NpcRoutine.cs` / `NpcRoutineBlock.cs` -- pure `NpcRoutineSchedule.FindSleepEndHour(blocks)` + `NpcRoutine.TryGetWakeHour(out int)` -- reset hour
- [x] `DialogueSystem.cs` -- `TopicLabel(node)` (ration → "(N left)"); `StartTopic` ration branch: refuse (not now / none left / item refused) via the runtime line node ending on the topic list, else give one to the player inventory, take one, continue the chain -- runtime
- [x] `DialogueUI.cs` -- topic button text via `_dialogueSystem.TopicLabel(node)` -- label
- [x] `CookingUI.cs` -- `[SerializeField] WorldFact _mealDiscoveredFact`; set when `learned` and the output is a `Meal`; pure `CookingRules.IsMealDiscovery(learned, output)` -- discovery fact
- [x] `Data/Facts/WorldFact_Cooking_MealDiscovered.asset` + wire on `Prefabs/UI/Cooking/CookingUI.prefab` -- approved exception
- [x] Quest Explorer -- setters: MealEatenFact → its meal (`[Meal]`), CraftedFact → the `RecipeSO` whose `knownFact` matches (`[Recipe]`), CookingUI prefab field → `[Cooking]`; type names; validator **V19** (meal missing / no bonus, recipe missing) on quest parts and memory conditions -- tooling
- [x] `Tests/EditMode/` -- `DailyRationRulesTests`, `MealCraftFactTests` (WSM events / GetFact), `CookingRules.IsMealDiscovery` cases, `FindSleepEndHour`, V19 problem text -- pure parts
- [x] Docs -- `Data/Facts/CLAUDE.md` (two types, Ration store), `Scripts/Crafting/CLAUDE.md` + `Scripts/UI/Crafting/CLAUDE.md` (events, discovery fact), `Scripts/World/DIALOGUE_SYSTEM.md` (ration topic), `Scripts/AI/NPC/CLAUDE.md` (wake hour), `Editor/QuestExplorer/CLAUDE.md` (V19, setters), quest docs' Prerequisites ticked -- knowledge

**Acceptance Criteria:**
- Given a quest part on `MealEatenFact_X`, when the player first eats X, then the part completes without a reload.
- Given Tob's ration topic at 2 left, when picked twice, then the label reads "(1 left)" then "(0 left)" and the third pick plays the none-left line.
- Given 0 left at 22:00 and a Sleep block 21 → 5, when the player sleeps until 07:00, then the label reads "(2 left)".
- Given an older save without `dailyRations`, when loaded, then no error and every ration is full.
- All EditMode tests pass; console has no compile errors.

## Design Notes

Period = `floor((now − wakeHour·3600) / 86400)`; the stored entry is `{period, taken}`; remaining = `perDay − taken` when the period matches, else `perDay`. "Woken by the player" = in a Sleep block and awake (a sleeper shows "Wake", not "Talk"). This also refuses while the NPC is walking to bed, which is acceptable. Keyed by node id: one ration node per NPC.

## Verification

**Commands:**
- `run_tests` EditMode -- expected: all pass
- `read_console` Error -- expected: no compile errors

**Manual checks (if no CLI):**
- Quest Explorer → Facts: `WorldFact_Cooking_MealDiscovered` lists the CookingUI setter.

## Suggested Review Order

**Daily ration topic**

- Entry point: refusals (asleep / none left / inventory full) vs give + count.
  [`DialogueSystem.cs:292`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L292)
- Ration day starts at the NPC's wake hour; midnight only as a warned fallback.
  [`DialogueSystem.cs:347`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L347)
- Pure period math: reset survives sleep skips, loads and unloaded NPCs.
  [`DailyRationRules.cs:22`](../../Assets/_Game/Scripts/Core/State/DailyRationRules.cs#L22)
- Store keyed by node id, saved apart from facts.
  [`WorldStateManager.cs:245`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L245)
- Wake hour = longest timed Sleep block, so a nap doesn't reset it.
  [`NpcRoutineBlock.cs:69`](../../Assets/_Game/Scripts/AI/NPC/NpcRoutineBlock.cs#L69)
- Label "(N left)" via the system; never seen / dimmed, fact ignored.
  [`DialogueSystem.cs:264`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L264)
- Node data: item, per day, three configurable lines.
  [`DailyRationDialogueNode.cs:16`](../../Assets/_Game/ScriptableObjects/Dialogue/DailyRationDialogueNode.cs#L16)

**Computed facts with events**

- First claim raises the MealEatenFact key; claims stay non-facts.
  [`WorldStateManager.cs:226`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L226)
- First craft (0 → 1) raises the CraftedFact key.
  [`WorldStateManager.cs:190`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L190)
- Evaluation in GetFact; SetFact refuses both types.
  [`WorldStateManager.cs:115`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L115)
- Fact types + shared "can never be true" problem text.
  [`MealEatenFact.cs:14`](../../Assets/_Game/ScriptableObjects/Facts/MealEatenFact.cs#L14)
  [`CraftedFact.cs:12`](../../Assets/_Game/ScriptableObjects/Facts/CraftedFact.cs#L12)

**Meal discovery**

- Set once when an Experiment cook learns a Meal recipe.
  [`CookingUI.cs:293`](../../Assets/_Game/Scripts/UI/Crafting/CookingUI.cs#L293)
  [`CookingRules.cs:20`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L20)

**Quest Explorer**

- Meal / recipe shown as setters of computed facts.
  [`QuestReferenceIndex.cs:471`](../../Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs#L471)
- CookingUI prefab field indexed as the WorldFact setter.
  [`QuestIndexCollector.cs:77`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L77)
- V19 rule.
  [`QuestValidator.cs:295`](../../Assets/_Game/Editor/QuestExplorer/QuestValidator.cs#L295)

**Peripherals**

- Optional save field (no version bump).
  [`SaveData.cs:123`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L123)
- Tests: `DailyRationRulesTests`, `MealCraftFactTests`, `NpcRoutineScheduleTests`, `QuestValidatorTests` (V19).
  [`MealCraftFactTests.cs`](../../Assets/Tests/EditMode/MealCraftFactTests.cs)
