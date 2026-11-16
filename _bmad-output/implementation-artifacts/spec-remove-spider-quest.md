---
title: 'Remove the obsolete spider quest'
type: 'chore'
created: '2026-11-16'
status: 'done'
baseline_commit: '4edfee1'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `SpiderClear` ("Spider Infestation", Guard Harl) was a test quest and is now obsolete. Its quest asset, facts, reward and Harl's three spider topics still ship and appear in the quest log. Its reward is the only source of `Item_Recipe_AppleTroutSandwich`.

**Approach:** Delete the quest and everything only it uses: quest folder, quest/dialogue facts, the player reward, Harl's spider dialogues and memories. Remove its entries from the managers and the quest log, and its mentions in the docs. Keep the spiders at the cave and their kill facts. Make the trout recipe buyable from the Storekeeper, alongside the mushroom recipe.

## Boundaries & Constraints

**Always:** Delete each `.meta` file with its asset. After deleting, leave no null or missing entries in `QuestEventsManager._quests`, `QuestLogUI._allQuests`, `Player.prefab` `PlayerRewards._rewards` or Harl's `NPCEntity.memories`. Change shop stock only through `NpcShopEditor.SetStock`, and only with StartingTown saved and not in Play mode.

**Ask First:** Any spider-related asset whose references reach beyond the spider quest (a fact or dialogue node that another quest, memory or scene object reads).

**Never:** Remove or move the cave spiders, `KilledFact_Monster_DarknessSpider*`, `EnemyType_DarknessSpider` or spider loot items. Change Harl's toll dialogue, including the "Can't promise the same about the spiders" line. Change the trout recipe's `RecipeSO` / `RecipeFact` or rename any save key.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/Quests/Spiders/` -- quest + `QuestFact_spiders_killed`
- `Assets/_Game/Data/Facts/{DialogueFact_RacketGuard_SpiderQuestAccepted,DialogueFact_RacketGuard_SpiderQuestTurnIn,QuestFact_SpiderClear_Completed}.asset` -- quest-only facts
- `Assets/_Game/Data/Rewards/PlayerReward_SpiderClear_Completed.asset` -- trout recipe reward (auto-synced into `Player.prefab`)
- `Assets/_Game/Data/NPCs/RacketGuard/Dialogues/{SpiderOffer,SpiderActive,SpiderReward}/`, `Memories/Spiders/` -- Harl's quest dialogue; `NPCMemoriesAutoSync` rebuilds his memory list
- `Assets/_Game/Prefabs/QuestEventsManager.prefab` (auto-synced), `Prefabs/UI/QuestLog/QuestLogUI.prefab` (manual `_allQuests`)
- `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- `SetStock(npc, item, count)` writes the scene `InventorySystem` stock
- Docs: `docs/World/Quests/SpiderClear.md`, `docs/World/world-index.md`, `Data/Quests/CLAUDE.md`, `Scripts/Crafting/CLAUDE.md`, `Data/NPCs/RacketGuard/CLAUDE.md`, `docs/World/Quests/QUEST_SPEC_TEMPLATE.md` (example name only), `deferred-work.md` (spider quest text entry)

## Tasks & Acceptance

**Execution:**
- [x] Unity -- Run `quest_report` on every fact and memory to delete. Confirm that only SpiderClear / Harl's spider memories read or set them.
- [x] Assets above -- delete them (`AssetDatabase.DeleteAsset`, so metas go too). Then run the quest sync, `Game/Dev/Sync All NPC Memories` and the reward sync. Remove the quest from `QuestLogUI._allQuests` and strip null entries from the lists.
- [x] `StartingTown.unity` Storekeeper -- `NpcShopEditor.SetStock(Storekeeper, Item_Recipe_AppleTroutSandwich, 1)`, then save the scene -- the recipe gets a source again.
- [x] Docs -- delete `SpiderClear.md` and its `world-index.md` row. Update the reward example in `Data/Quests/CLAUDE.md` and the trout recipe source in `Crafting/CLAUDE.md` (now the Storekeeper). Remove "He gives the spider job" from Harl's CLAUDE.md. Rename the template's example fact to a neutral one. Strike the "Spider quest tracks 2 spiders" entry in `deferred-work.md` as resolved by this spec.

**Acceptance Criteria:**
- Given the project after the change, when `quest_report audit` runs, then SpiderClear is absent and no new issue references a missing quest, fact or memory.
- Given a new game, when the player talks to Harl, then no spider topic is offered, and the toll still works.
- Given the Storekeeper's shop, when the player opens it, then `Item_Recipe_AppleTroutSandwich` is listed at its buy value.
- Given the cave spiders, when one is killed, then its `KilledFact` is still recorded.
- Given the repo, when grepping for `SpiderClear`, `SpiderQuest`, `SpiderOffer`, `SpiderReward`, `SpiderActive` under `Assets/` and `docs/`, then nothing matches except history in `_bmad-output/`.

## Verification

**Commands:**
- `quest_report audit` -- expected: no SpiderClear, no missing references
- Unity EditMode tests -- expected: same pass count as before (the tests use their own in-memory spider fixtures)

**Manual checks (if no CLI):**
- `read_console` after the deletes and syncs: no missing-reference errors. The Storekeeper's `InventorySystem` lists the trout recipe.

## Suggested Review Order

**Removing the quest from the lists that load it**

- Quest manager now lists only HiddenStash and WardensErrand (auto-synced).
  [`QuestEventsManager.prefab:47`](../../Assets/_Game/Prefabs/QuestEventsManager.prefab#L47)

- Quest log list: null entry stripped by hand (no auto-sync).
  [`QuestLogUI.prefab:1452`](../../Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab#L1452)

- Spider reward removed. Two rewards moved (sync churn). The dropped `_playerSkills` key on `PlayerCombat` is stale.
  [`Player.prefab:719`](../../Assets/_Game/Prefabs/Player/Player.prefab#L719)

- Harl keeps only his two toll memories.
  [`NPC_RacketGuard.asset:59`](../../Assets/_Game/Data/NPCs/RacketGuard/NPC_RacketGuard.asset#L59)

**Trout recipe gets a new source**

- Storekeeper stock adds `Item_Recipe_AppleTroutSandwich` ×1 via `NpcShopEditor.SetStock`.
  [`StartingTown.unity:424973`](../../Assets/_Game/Scenes/StartingTown.unity#L424973)

**Docs and examples**

- Recipe-item source now the Storekeeper.
  [`Crafting/CLAUDE.md:27`](../../Assets/_Game/Scripts/Crafting/CLAUDE.md#L27)

- Item-reward example now the blacksmith's sword recipe.
  [`Quests/CLAUDE.md:43`](../../Assets/_Game/Data/Quests/CLAUDE.md#L43)

- Harl's background no longer gives a spider job.
  [`RacketGuard/CLAUDE.md:49`](../../Assets/_Game/Data/NPCs/RacketGuard/CLAUDE.md#L49)

- Template example fact renamed to a real one.
  [`QUEST_SPEC_TEMPLATE.md:38`](../../docs/World/Quests/QUEST_SPEC_TEMPLATE.md#L38)

- Review findings deferred: Harl's empty topic list, old-save shop stock.
  [`deferred-work.md:820`](deferred-work.md#L820)
