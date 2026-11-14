---
title: 'Area 1 — the Warden''s errand: notices to the four work-site managers'
type: 'feature'
created: '2026-11-14'
baseline_commit: '6b64537'
status: 'done'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md', '{project-root}/Assets/_Game/Data/Quests/CLAUDE.md', '{project-root}/docs/World/Areas/area-001-company-encampment.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When the arrival sequence (event-001) ends, the player has nothing to do. No work site has a manager, so the camp's work, and the quests that will come from it, have no way in. Voss's only quest, Find Herbalist, is a test leftover that can never finish.

**Approach:** A quest, `WardensErrand`, starts on its own when `WorldFact_Arrival_Done` is set, and Hobb's arrival briefing now points the player to Voss. Voss hands over four notice items, one for each new named manager, at the quarry, the wood camp, the hunting camp and the farm. The notices use a new Quest Item category. Handing one over in dialogue uses it up, and delivering them makes the player meet each manager. Once all four are delivered, the player reports back to Voss and chooses where they will work, which completes the quest. Find Herbalist is deleted.

## Boundaries & Constraints

**Always:** Every bit of state is a fact, so save / load needs no new code. Quest items (`ItemSO.isQuestItem`) can't be sold, dropped, put into a container or bound to the action bar. Their category reads "Quest Item" and they sit in the Misc tab. The player's inventory has no slot limit, so the notices can't be lost. A dialogue choice can require an item: the row is disabled while the player lacks it, and the item is removed when the choice is picked. Each manager's delivery topic appears only after Voss assigns the errand, and plays only once. The work-site choice is one `DialogueFact` per site, for later specs to read. The errand is optional: no timer, no penalty. Each new NPC gets the full folder (identity `CLAUDE.md`, `NPC_*`, `KilledFact`, `WitnessProfile_Humanoid`), comes from `NPC_base Variant`, stands at its site on a routine, and uses an existing head variant and skin tone.

**Ask First:** Any C# change beyond the quest-item flag and its guards, the choice's required item, and the deletions. Any change to `ArrivalSequence` behaviour, `Core.unity`, the faction matrix, or a manager getting a weapon loadout.

**Never:** Manager quests, the work shifts themselves, wages or debt, the stripped beat, portrait faces (those go through `/characters:portrait-face` later), new animations, terrain or layout changes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Start | Briefing closes, `Arrival_Done` set | Quest starts with the log line "I've just arrived. I should report to Warden Voss for work." Hobb's new third line names Voss | N/A |
| Dev Skip Arrival | Fact set by the dev skip | Quest is started too, the same way | N/A |
| Assign | Player picks Voss's "I'm here for work." topic | Assigned fact set; four notices added with a "Received" toast; the step lists the four managers | N/A |
| Manager before Voss | Player talks to a manager before the assignment | No delivery topic; only the repeatable "What's the work here?" topic | N/A |
| Deliver | Player picks the "Hand over the Warden's notice." choice | That notice is removed, the site's delivered fact is set and the topic is gone; the work topic stays | N/A |
| Notice missing | Delivery choice while that notice isn't in the inventory | Row disabled, with the item named in red | Pick refused |
| Sell / drop / store a notice | Trade, drop, container put or action-bar bind | Refused; no sell or put button, no drop option | Toast or log, no item loss |
| All delivered | All four delivered facts set | Voss's "The notices are delivered." topic appears | N/A |
| Choose | Player picks a site in Voss's choice | That site's work fact is set, the quest completes, 100 XP is granted, and the topic is gone | N/A |
| Save / load | Saved at any stage | The same stage is restored | N/A |

</frozen-after-approval>

## Code Map

- `Data/NPCs/Warden/` -- Voss's identity, memories and dialogues; `Dialogues/FindHerbalist` + `Mem_Warden_HerbalistQuest` are deleted
- `Data/Quests/find herbalist/` -- quest, `QuestFact`, `WorldFact` and `PlayerReward` to delete; also referenced by `Prefabs/UI/QuestLog/QuestLogUI.prefab` (`_allQuests`) and the `QuestEventsManager` auto-sync
- `Data/NPCs/TentMate/Dialogues/Arrival/` -- Hobb's two-line briefing chain, wired into `Data/World/ArrivalSequence_Event001`
- `Data/Quests/HiddenStash/` -- a quest to copy the layout from
- `Data/Rewards/PlayerReward_SpiderClear_Completed.asset` -- example of a quest-completed reward (200 XP)
- `Prefabs/Entities/NPC_base Variant` -- base for the new NPCs; Brann is a recent instance to copy (routine, `PersistentID`, head variant)
- `Scenes/StartingTown.unity` -- sites: `Area1_Blockout/Quarry` (≈226, −96), `WoodCamp` (≈−224, −8), `HuntingCamp` (≈32, 222), the `Farm` manager's house door (≈−22, −62)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Items/ItemSO.cs`, `Scripts/UI/Inventory/ItemDetailFormatter.cs`, `ItemFilter.cs` (Misc), `NPCTradeUI`, `InventoryUI` / `InventoryDetailActions` (drop), `ContainerUI` (put), `ActionBarSystem` (bind) -- `isQuestItem` flag, "Quest Item" category, and the guards -- a quest item can't leave the player except through dialogue
- [x] `ScriptableObjects/Dialogue/ChoiceDialogueNode.cs`, `Scripts/World/DialogueSystem.cs` (+ `_playerInventory` wired on `Player.prefab`), `Scripts/UI/Dialogue/DialogueUI.cs` + `DialogueOptionFormat.cs` -- `ChoiceOption.requiredItem` (ItemSO, checked before the gold is paid, removed after it); the row is disabled, with the item name in red, while it's missing -- same pattern as `goldCost`
- [x] `Tests/EditMode/` -- category / filter for a quest item; the required-item label and refusal; trade / drop / put guards on the pure helpers
- [x] `Data/Items/Quest/Item_Notice_{Quarry|Timber|Hunting|Farm}` -- "Notice for Overseer Dace" etc.: `isQuestItem`, `sellValue` 0, a shared placeholder icon, no world prefab; plus `PlayerReward_WardensErrand_Assigned` (Dialogue type, the assign fact, four `_itemRewards`) -- what Voss hands over
- [x] `Data/Quests/find herbalist/`, `Warden/Dialogues/FindHerbalist/`, `Warden/Memories/Mem_Warden_HerbalistQuest`, `QuestLogUI.prefab` -- delete the quest and every asset only it uses, take it out of `_allQuests`, re-sync `QuestEventsManager`, and update `Data/Quests/CLAUDE.md` -- user decision: it was a test run
- [x] `Data/NPCs/QuarryOverseer/`, `TimberBoss/`, `LeadHunter/`, `FarmReeve/` -- for each: identity `CLAUDE.md`, `NPC_<Name>`, `KilledFact`, a `Notice` topic (unlocked by the assigned fact, invalidated by the site's delivered fact). It opens with a greeting line, then a choice "Hand over the Warden's notice." that requires and uses up that site's notice and sets the delivered fact, followed by 1–2 lines hinting at the site's trouble, and a `Work` topic (repeatable, always available, so the matrix's "Manager before Voss" row holds). The site hook is planted, not started. Cast: Overseer Dace (Company, quarry: workers frightened of the sealed crack), Gorm Holl (worker gang boss, wood camp: trees that come down wrong), Corvin Ash (lead hunter, npc-010, Company in name only, speaks to the player as an equal: something has been taking the snares), Reeve Odo Pell (Company, farm: tithe counts that never add up) -- each manager is a way into later quests
- [x] `Data/NPCs/Warden/Dialogues/Errand/` + memories -- `Assign`: "I'm here for work.", unlocked by `Arrival_Done`, sets `DialogueFact_Warden_Errand_Assigned`, invalidated by it; Voss names the four managers and their sites. `Report`: "The notices are delivered.", unlocked by `QuestFact_WardensErrand_Step0` (all delivered), invalidated by `QuestFact_WardensErrand_Completed`; a choice node with four options, each setting `DialogueFact_Warden_Errand_Work{Quarry|Timber|Hunting|Farm}`, followed by a short Voss line per site
- [x] `Data/Quests/WardensErrand/` -- `Quest_WardensErrand`. Start: `Arrival_Done`. Step 0 "Deliver the Warden's notices": the assigned fact plus four delivered facts, each with a log entry. Step 1 "Report back to Voss": `QuestFact` step 0 completed. Completed parts: the four work facts, each with an entry ("I'll work at the quarry." …). `QuestFact`s. `PlayerReward` 100 XP on completion. Register in `QuestLogUI._allQuests` -- the quest log follows the errand
- [x] `Data/NPCs/TentMate/Dialogues/Arrival/` -- add a third text node: "Come first light, see Voss at the centre. He hands out the work. Don't make him come looking for you." -- user decision: both Hobb and the automatic start
- [x] `Scenes/StartingTown.unity` -- four `NPC_base Variant` instances, each at its site, facing its approach. Each gets an all-day Stand routine block at that spot (night spot optional), a `PersistentID` with its own `KilledFact`, `NPCEntity`, a skin tone and its own `HeadAppearance_<NPC>` copied from an existing head (Bandit / GuardRight / DockGuard / WarehouseGuard), under the new hand-owned root `WorkSiteManagers` -- they stand where the player arrives
- [x] Docs -- `docs/World/world-index.md` (four NPCs, the quest, Find Herbalist removed), `docs/World/Quests/WardensErrand.md`, the area-001 doc (named managers), `Data/Facts/CLAUDE.md` if the setters list changes, `deferred-work.md` (errand shipped; manager quests, work shifts and portrait faces still open)

**Acceptance Criteria:**
- Given a new game, when the arrival ends, then the quest log shows Wardens Errand with the "report to Voss" line, and Hobb's briefing has three lines.
- Given the quest is complete, when `/quests:audit` (MCP `quest_report audit`) runs, then WardensErrand reports no errors and no Find Herbalist asset remains.
- Given each manager, when the player walks there from the camp along the roads, then the manager is reachable on the NavMesh and can be talked to.
- Given EditMode tests, when they run, then all pass.
- 2026-11-15, review (3 reviewers, patches only, no loopback): step 1 gained a second part (`QuestFact_WardensErrand_Completed`), because with only "step 0 done" it showed as complete the moment it appeared. Voss's third assign line no longer threatens "Lose one and the paper goes on your debt" (impossible loss, out-of-scope debt). Cooking / smithing skip quest items (`CookingRules.IsMaterialFor`), and `ItemSO.OnValidate` warns when `isQuestItem` is combined with an ingredient / material / usable / equipable item. Tests added: `SelectChoice` refusal, the action-bar positive control, the crafting skip. Docs: npc range, the `WorldFact_Arrival_Done` readers, the QuestSO tooltip. Deferred: non-UI consume paths, the teach-choice field, the gold-item authoring trap, the refund, the Quest Explorer setter gap, and the prefab placeholder strings. KEEP: the required-item check before gold, consume after; the hand-over as a choice fact (Escape-safe).

## Design Notes

Step 0 includes the assigned fact, so it shows in the log as soon as Voss gives the errand: a `QuestStep` is active when any of its parts is true, and done when all of them are. The Report topic and step 1 then read the step-0 `QuestFact`, so they need no "all delivered" fact of their own. Check the `QuestFact` step state's meaning (step completed vs active) in `QuestFact.cs` before wiring it. The notices come from a Dialogue-type `PlayerRewardSO` on the one-shot assign fact. Dialogue rewards fire again whenever a node replays (a known deferred gap), but this topic never replays, so that's acceptable here. The delivery is a choice and not the topic's own `dialogueFact`, because a choice can require the item, and closing the panel early with Escape must not count as delivered. User change at approval (2026-11-14): physical quest-item notices instead of spoken ones. Managers stand outdoors on a plain Stand block. Their activities and work hours come later with the work shifts.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- MCP `quest_report` `audit` and `WardensErrand` -- expected: no errors, every fact has a setter
- `read_console` after compile and in Play -- expected: no errors

**Manual checks:**
- Play: Dev Skip Arrival, then talk to Voss, deliver to all four in any order (save and load once in the middle), report back and pick a site. Check that the quest completes with 100 XP and that every topic disappears as it should.

## Suggested Review Order

**Handing an item over in dialogue**

- Entry point: the item is checked before the gold, then consumed after the payment.
  [`DialogueSystem.cs:287`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L287)

- Held check and the plain one-unit consume (not given to the NPC, so it can't be looted back).
  [`DialogueSystem.cs:322`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L322)

- The row is disabled and dimmed while the item is missing, like an unaffordable gold cost.
  [`DialogueUI.cs:493`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L493)

- One suffix for gold and the item, each part red when unpayable.
  [`DialogueOptionFormat.cs:77`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueOptionFormat.cs#L77)

- New authored field on choices.
  [`ChoiceDialogueNode.cs:23`](../../Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs#L23)

**Quest items stay with the player**

- The flag and the single rule every guard uses.
  [`ItemSO.cs:26`](../../Assets/_Game/ScriptableObjects/Items/ItemSO.cs#L26)

- Sell refused at the action itself, so drag, double-click and buttons are all covered.
  [`NPCTradeUI.cs:283`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L283)

- Container put refused the same way.
  [`ContainerUI.cs:172`](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L172)

- Drop refused.
  [`InventoryUI.cs:146`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L146)

- Action bar refuses a quest item.
  [`ActionBarSystem.cs:98`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L98)

- Cooking and smithing never list a quest item.
  [`CookingRules.cs:21`](../../Assets/_Game/Scripts/Crafting/CookingRules.cs#L21)

- Author-time warning for risky flag combinations.
  [`ItemSO.cs:35`](../../Assets/_Game/ScriptableObjects/Items/ItemSO.cs#L35)

**Presentation**

- "Quest Item" category wins over every other rule.
  [`ItemDetailFormatter.cs:61`](../../Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs#L61)

- Quest items sit in the Misc tab.
  [`ItemFilter.cs:30`](../../Assets/_Game/Scripts/UI/Inventory/ItemFilter.cs#L30)

- The sell button is hidden for quest items (same pattern for put and drop).
  [`TradeDetailActions.cs:55`](../../Assets/_Game/Scripts/UI/Inventory/TradeDetailActions.cs#L55)

**Content (data)**

- Quest: start on arrival, four deliveries, report, four work choices.
  [`Quest_WardensErrand.asset`](../../Assets/_Game/Data/Quests/WardensErrand/Quest_WardensErrand.asset)

- Flow, facts and the verification route.
  [`WardensErrand.md`](../../docs/World/Quests/WardensErrand.md)

**Peripherals**

- Tests: category, filter, part rule, crafting skip, action bar, choice refusal, labels.
  [`QuestItemTests.cs:93`](../../Assets/Tests/EditMode/QuestItemTests.cs#L93)
