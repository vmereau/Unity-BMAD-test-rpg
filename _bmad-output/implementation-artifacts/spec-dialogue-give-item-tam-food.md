---
title: 'Give-item window in dialogue, Tam food quest with two endings'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: '766438d5733bc328739d4fd517a44d5bb1cf7b86'
context:
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Inventory/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A dialogue choice can only take one specific item (`requiredItem`). Tam asks for "something to eat", but the player can't hand him food, and his quest (HiddenStash) only ends by reporting the stash theft.

**Approach:** Add an item-category requirement to `ChoiceOption` (`ReadyFood` = Apple / Bread, `Meal` = cooked meals). Picking such a choice opens a trade-style **Give window**: the player's matching items on one side, the selected item in a single give slot (detail panel), and Give / Cancel buttons. Rework HiddenStash: it completes when Tam gets food. Basic food = base ending (50 XP). A cooked meal = a better ending via an extra choice ("Here, I have even better than what you were asking for."), only shown while the player holds a meal (100 XP, its own fact for later content). Reporting the stash becomes a side line worth 25 XP that doesn't complete the quest.

## Boundaries & Constraints

**Always:** The given item moves from the player into the NPC's `InventorySystem` (consumed with a warning if there is none). Quest items never match a category. The choice row shows the category (" (Food)" / " (Meal)"), is red and disabled while nothing matches, and the Give button stays disabled until an item is selected. Cancel / Escape closes only the Give window and returns to the choice node, with nothing given and no fact set. A player knockout closes both windows. Dialogue text has no em-dashes, ≤300 chars per node, and keeps Tam dry. New nodes get their `_id`. Existing `requiredItem` behaviour stays unchanged.

**Ask First:** Changing the Give window layout beyond grid + give slot/detail + two buttons. Making another NPC use the mechanic.

**Never:** Multi-item or quantity gives. Counter-offers or gold in the Give window. Re-wiring the stash chest, Brann, or `ContainerTakeFact`.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Quest start | StashHint topic, either choice | `DialogueFact_HiddenStash_Asked` set at chain end, quest started, stash topic gone; "no money" also sets Hint |
| Feed, has Apple | Feed topic → "Here, this should help." | Give window lists only Apple / Bread; Give → 1 Apple into Tam's inventory, FedFood set, quest completed, +50 XP |
| Feed, has meal | holds a sandwich | Meal choice visible; Give window lists meals only; FedMeal set, +100 XP, quest completed |
| No meal | holds no meal | Meal choice hidden |
| No food | holds no Apple / Bread | Food row red + disabled; "Not yet." and "I don't have any money for food." still pickable |
| Cancel | Give window open, Escape or Cancel | Window closes, choice node still shown, inventory unchanged |
| Stash report | Hint + Taken | "You were right about Brann's supplies..." topic, +25 XP once, quest not completed |
| Only raw food | Trout, mushroom | Not listed for ReadyFood |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` -- `ChoiceOption` fields
- `Assets/_Game/Scripts/World/DialogueSystem.cs` -- `SelectChoice` payment, `AdvanceToNode` choice filtering, `_tradeUI` pattern
- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs`, `DialogueOptionFormat.cs` -- choice row, cost suffix
- `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs`, `ContainerUI.cs`, `IItemSlotContainer.cs` -- grid / detail / input pattern to copy
- `Assets/_Game/Prefabs/UI/Trade/NPCTradeUI.prefab`, `UICanvas.prefab` -- layout source, nesting point
- `Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs` -- `Kind` (Meal), food flag
- `Assets/_Game/Data/NPCs/BeatenWorker/` -- Tam dialogues and memories
- `Assets/_Game/Data/Quests/HiddenStash/` -- quest + facts; `Data/Rewards/`, `Player.prefab` `PlayerRewards._rewards`

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/GiveItemCategory.cs` -- enum `None, ReadyFood, Meal` (append-only) + static `GiveItemRules.Matches(item, category)` (never quest items; ReadyFood = `PotionItemSO.IsReadyToEat` and not Meal; Meal = `Kind == Meal`) and `Label` ("Food" / "Meal") -- pure, testable rule
- [x] `PotionItemSO.cs` -- `_readyToEat` bool + `IsReadyToEat`; set it on `Item_Apple`, `Item_Bread` -- data-driven food list
- [x] `ChoiceDialogueNode.cs` -- `ChoiceOption.requiredCategory`, `hideWhenMissing` (hides the row when its item / category isn't held); `OnValidate` warns when both `requiredItem` and a category are set (item wins) -- authoring
- [x] `DialogueSystem.cs` -- filter hidden choices; `SelectChoice` on a category choice opens `_giveItemUI` (refuses if nothing matches); `CompleteGiveChoice(choice, node, item)` re-checks the match and holding, moves 1 unit to the NPC inventory, then runs the shared choice tail (seen, fact, signal, advance); `HasChoiceCategory` -- give flow
- [x] `DialogueUI.cs` / `DialogueOptionFormat.cs` -- the category row uses the cost suffix with the category label, disabled while nothing matches -- row state
- [x] `Scripts/UI/Inventory/GiveItemUI.cs` + `Prefabs/UI/Give/GiveItemUI.prefab` (nested in `UICanvas`, wired on `DialogueSystem`) -- title "Give to <NPC>", player grid filtered by category, the detail panel as the give slot, Give (disabled with no selection) / Cancel; Escape and knockout close it; the cursor stays unlocked (the dialogue is still open) -- the window
- [x] Tam assets -- facts `DialogueFact_HiddenStash_Asked` / `_FedFood` / `_FedMeal` in `Data/Quests/HiddenStash/`; `Start_BeatenWorker_StashHint.dialogueFact` = Asked; `Mem_BeatenWorker_StashHint` invalidation [Asked, Mocked]; new `Dialogues/Feed/` + `Mem_BeatenWorker_Feed` (unlock [Asked], invalidation [FedFood, FedMeal, Mocked]): Start "I brought you something to eat." → Choice "Let's see it, then." [0] "Here, this should help." ReadyFood, FedFood → "Hm. It's food. That's more than I had this morning." → "Thanks. Don't tell anyone I said that." [1] "Here, I have even better than what you were asking for." Meal, hideWhenMissing, FedMeal → "A cooked meal. Out here." → "You're a strange one. I won't forget this. Might even have something for you, one day." [2] "Not yet." → "Then stop wasting my time. I'm not getting any fatter." [3] "I don't have any money for food." fact Hint → existing `Text_BeatenWorker_StashHint_2` chain; StashReported start text "You were right about Brann's supplies. They were just lying around, asking to disappear.", memory invalidation adds Mocked; run `Game/Dev/Sync All NPC Memories` -- quest dialogue
- [x] `Quest_HiddenStash.asset` (keep `questId` HiddenStash, asset name and GUID) -- title "Food for Tam"; description: Tam, beaten by the Warden, is hungry and doesn't care where food comes from; bread or an apple would do, something cooked would surprise him; Brann's chest in the gang house is there for the taking if I'm willing to steal. startPart Asked ("Tam is hungry. Bread or an apple would do."); completedParts FedFood ("I gave Tam something to eat.") and FedMeal ("I gave Tam a cooked meal. He looked like he'd remember it."); one step "Optional: Brann's supplies" (description "Brann keeps food in a chest in the gang house by the lake. He leaves for the gang fire at six.") parts [Hint "Tam told me about Brann's chest.", Taken "I helped myself to Brann's supplies."] -- quest reads as feeding Tam, stash as optional theft path
- [x] `Data/Rewards/PlayerReward_HiddenStash_FedFood` (50) / `_FedMeal` (100) / `_Reported` (25), Dialogue fact type, added to `Player.prefab` `PlayerRewards._rewards` -- XP
- [x] `Assets/Tests/EditMode/GiveItemRulesTests.cs` -- matrix rows: Apple / Bread / Trout / meal / quest item per category; cost label with category -- coverage
- [x] Docs -- `DIALOGUE.md` ChoiceOption table, `ScriptableObjects/Items/CLAUDE.md`, `UI/Inventory/CLAUDE.md`, `World/CLAUDE.md` DialogueSystem row, Tam `CLAUDE.md` gating table, `docs/World/Quests/HiddenStash.md`, npc-002; remove the solved HiddenStash entry in `deferred-work.md` -- keep docs true

**Acceptance Criteria:**
- Given the new assets, when running `quest_report audit HiddenStash`, then no new errors versus before.
- Given Play mode with an Apple and a sandwich, when feeding Tam via the meal choice, then the sandwich leaves the player and appears in Tam's `InventorySystem`, and the Apple stays.
- Given EditMode tests, when running them, then all pass.

## Design Notes

The Give window is a new screen, not `NPCTradeUI` with a mode flag: trade logic (prices, gold, two grids) shares nothing with a one-way, one-item give. Copy its structure (InventoryPanel instance + shared `ItemDetailPanelUI`, overrides on the nested root only). The quest has no "bring food" step: a step needs all its parts true, and the two endings are an OR, so the objective lives in startPart's entry and the quest's completedParts.

## Verification

**Commands:**
- `grep -rlE 'u2014|—' Assets/_Game/Data/NPCs --include=*.asset` -- expected: no output
- MCP `run_tests` EditMode -- expected: all pass

**Manual checks:**
- Play: offer help → stash topic → Feed with no food / Apple / meal, Cancel once; check XP toasts and the quest log; the console has no errors.

## Suggested Review Order

**Give flow (dialogue side)**

- Entry point: a category choice defers to the Give window instead of applying.
  [`DialogueSystem.cs:291`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L291)

- Re-checks item, takes it before gold, then shared choice tail.
  [`DialogueSystem.cs:314`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L314)

- Item goes to the NPC; returned to the player if refused.
  [`DialogueSystem.cs:409`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L409)

- `hideWhenMissing` rows filtered before numbering.
  [`DialogueSystem.cs:399`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L399)

- New authoring fields on `ChoiceOption`.
  [`ChoiceDialogueNode.cs:26`](../../Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs#L26)

**Give window**

- Opens on the filtered player grid; no cursor ownership.
  [`GiveItemUI.cs:80`](../../Assets/_Game/Scripts/UI/Inventory/GiveItemUI.cs#L80)

- Selection validates against live inventory; rebuilds on stale indexes.
  [`GiveItemUI.cs:148`](../../Assets/_Game/Scripts/UI/Inventory/GiveItemUI.cs#L148)

- Same-frame Escape guard so cancelling never closes the dialogue.
  [`GiveItemUI.cs:48`](../../Assets/_Game/Scripts/UI/Inventory/GiveItemUI.cs#L48)

- DialogueUI ignores its own Escape and input while the window owns them.
  [`DialogueUI.cs:143`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L143)

**Item categories**

- Pure rule: ready food vs meal, quest items excluded.
  [`GiveItemCategory.cs:20`](../../Assets/_Game/ScriptableObjects/Items/GiveItemCategory.cs#L20)

- Data flag set on Apple and Bread only.
  [`PotionItemSO.cs:35`](../../Assets/_Game/ScriptableObjects/Items/PotionItemSO.cs#L35)

**Tam's quest data**

- Meal answer: Meal category, hidden without a meal.
  [`Choice_BeatenWorker_Feed.asset:28`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Feed/Choice_BeatenWorker_Feed.asset#L28)

- Quest retitled; start Asked, two completion facts, optional stash step.
  [`Quest_HiddenStash.asset:16`](../../Assets/_Game/Data/Quests/HiddenStash/Quest_HiddenStash.asset#L16)

- Either stash answer starts the quest at topic end.
  [`Start_BeatenWorker_StashHint.asset:18`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/StashHint/Start_BeatenWorker_StashHint.asset#L18)

- Feed topic gating; stash report now also needs Asked.
  [`Mem_BeatenWorker_Feed.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_Feed.asset#L15)
  [`Mem_BeatenWorker_StashReported.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_StashReported.asset#L15)

- Meal ending reward (100 XP); food 50, report 25 alongside.
  [`PlayerReward_HiddenStash_FedMeal.asset:19`](../../Assets/_Game/Data/Rewards/PlayerReward_HiddenStash_FedMeal.asset#L19)

**Tests and docs**

- Success path, wrong category, hide filter, real item assets.
  [`GiveItemRulesTests.cs:152`](../../Assets/Tests/EditMode/GiveItemRulesTests.cs#L152)

- Authoring doc for the new choice fields.
  [`DIALOGUE.md:123`](../../Assets/_Game/Data/NPCs/DIALOGUE.md#L123)

- Quest doc endings table.
  [`HiddenStash.md:12`](../../docs/World/Quests/HiddenStash.md#L12)
