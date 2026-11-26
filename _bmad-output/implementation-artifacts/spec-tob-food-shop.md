---
title: 'Move food and recipe stock from the Storekeeper to a Tob shop'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: 'eabd3881'
context:
  - '{project-root}/.claude/commands/NPC/shop-dialogue.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Storekeeper sells the food and the written recipes. Food is Tob's area as the cooking tutor, and players should buy it from him once they have met him through his first quest.

**Approach:** Remove the food items and the two recipe items from the Storekeeper's scene stock and give them to Tob at the same counts. Add a shop topic to Tob, "What do you sell?", that unlocks when `WorkersRation` is completed. Update the quest specs and docs that still name the Storekeeper as the seller.

## Boundaries & Constraints

**Always:** Follow `/NPC:shop-dialogue`: `Dialogues/Shop/Start_KitchenHand_Shop` → `Shop_KitchenHand`, `Memories/Mem_KitchenHand_Shop`, and stock through `NpcShopEditor.SetStock` (scene clean, not in Play mode). The topic `dialogueFact` is null, so the shop is repeatable. Move the counts 1:1: Apple 10, Bread 10, Brown Mushroom 10, Trout 5, Onion 5, Salt Pork 5, Item_Recipe_AppleMushroomSandwich 2, Item_Recipe_AppleTroutSandwich 1. Shop line: "Whatever the garden and the store spare me. Fresh today, gone tomorrow — and the recipes are the ones I've worn out myself."

**Ask First:** Any runtime code change. Moving any item not listed above. Editing an implemented quest's assets.

**Never:** Touch the Storekeeper's non-food stock (gear, arrows, torch, Health Potion) or his shop topic. Add per-item conditions, restocking or price changes. Create a new fact (reuse `QuestFact_WorkersRation_Completed`).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Before quest done | WorkersRation not completed | Tob has no "What do you sell?" topic | — |
| Quest done | `QuestFact_WorkersRation_Completed` true | Topic shown; picking it plays the shop line, then opens trade on Tob's inventory (food + recipes) | — |
| Storekeeper | any time | Trade shows gear, arrows, torch, potion; no food or recipes | — |
| Stock edit refused | scene dirty / play mode / several instances | Stop and report | `SetStock` Error result |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Editor/NPCs/NpcShopEditor.cs` -- `SetStock` / `DescribeStock` on the scene instance's `InventorySystem._startingItems`
- `Assets/_Game/Scenes/StartingTown.unity` -- `Storekeeper` and `KitchenHand` instances (stock overrides)
- `Assets/_Game/Data/NPCs/KitchenHand/` -- Tob's dialogues / memories; `NPC_KitchenHand.asset` memories list (auto-sync)
- `Assets/_Game/Data/Facts/QuestFact_WorkersRation_Completed.asset` -- unlock condition
- `Assets/_Game/Data/NPCs/Storekeeper/Dialogues/Shop_Storekeeper.asset` -- reference shop node (`ShopDialogueNode`)
- `docs/World/Quests/WorkersSandwich.md`, `CooksExperiment.md`, `docs/World/NPCs/npc-020-tob.md` -- text naming the Storekeeper as seller
- `Assets/_Game/Scripts/Crafting/CLAUDE.md` -- recipe items / Trout "sold by the Storekeeper"

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Data/NPCs/KitchenHand/Dialogues/Shop/Shop_KitchenHand.asset` -- create `ShopDialogueNode` with the shop line, `nextNode` null -- trade node
- [x] `Assets/_Game/Data/NPCs/KitchenHand/Dialogues/Shop/Start_KitchenHand_Shop.asset` -- create `StartDialogueNode` text "What do you sell?", `nextNode` → `Shop_KitchenHand`, `dialogueFact` null -- topic button
- [x] `Assets/_Game/Data/NPCs/KitchenHand/Memories/Mem_KitchenHand_Shop.asset` -- `effects.startdialog` → start node, unlock [`QuestFact_WorkersRation_Completed`], no invalidation; check `NPC_KitchenHand.memories` contains it -- visibility
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- `SetStock` to 0 for the 8 items on Storekeeper, and to their counts on KitchenHand -- move the stock
- [x] `docs/World/Quests/WorkersSandwich.md` -- description, prerequisite 2, step 1 description, offer line → Tob sells onion and pork (e.g. "I sell the onion and the pork, once you've had your soup.") -- spec matches the world
- [x] `docs/World/Quests/CooksExperiment.md` -- line 130 → Tob sells written recipes ("I sell a few written recipes too, if you'd rather read than burn."); line 161 "Storekeeper recipe items" → Tob's -- same
- [x] `docs/World/NPCs/npc-020-tob.md`, `Assets/_Game/Scripts/Crafting/CLAUDE.md` -- replace Storekeeper-as-seller mentions with Tob; add a shop row to Tob's topic table if present -- docs
- [x] `Assets/_Game/Data/NPCs/KitchenHand/CLAUDE.md` -- Quest role / Asset Reference: add the `Shop` topic, `Mem_KitchenHand_Shop`, its unlock and the stock -- NPC sheet

**Acceptance Criteria:**
- Given the edits, when `DescribeStock` runs on both NPCs, then Tob lists exactly the 8 items at the given counts and the Storekeeper lists only the remaining 10.
- Given the new memory, when `quest_report Mem_KitchenHand_Shop` and `quest_report WorkersRation` run, then they report no new Error / Warning.

## Spec Change Log

- 2026-11-26 (implementation): the approved shop line had an em-dash, which `Data/NPCs/CLAUDE.md` / `DIALOGUE.md` forbid in dialogue text. `Shop_KitchenHand` uses "Whatever the garden and the store spare me. Fresh today, gone tomorrow. And the recipes are the ones I've worn out myself." (punctuation only, flagged to the user). Frozen block left as approved.

## Design Notes

Tob's inventory is also his loot, and the stock does not restock. This is the same behaviour the Storekeeper already has.

## Verification

**Manual checks (if no CLI):**
- `read_console` is free of new errors after each asset and stock step.
- `grep -rn "Storekeeper" docs/World/Quests/WorkersSandwich.md docs/World/Quests/CooksExperiment.md` -- no line naming him as the food or recipe seller.

## Suggested Review Order

**Shop topic and visibility**

- Entry point: the memory gates the shop on WorkersRation being completed.
  [`Mem_KitchenHand_Shop.asset:15`](../../Assets/_Game/Data/NPCs/KitchenHand/Memories/Mem_KitchenHand_Shop.asset#L15)

- Repeatable topic button (no dialogueFact), chains to the shop node.
  [`Start_KitchenHand_Shop.asset:15`](../../Assets/_Game/Data/NPCs/KitchenHand/Dialogues/Shop/Start_KitchenHand_Shop.asset#L15)

- Shop line in Tob's voice, em-dash removed per dialogue rules.
  [`Shop_KitchenHand.asset:15`](../../Assets/_Game/Data/NPCs/KitchenHand/Dialogues/Shop/Shop_KitchenHand.asset#L15)

**Stock move (scene overrides)**

- Storekeeper list shrinks 18 → 10; non-food counts unchanged, unused overrides removed.
  [`StartingTown.unity:300891`](../../Assets/_Game/Scenes/StartingTown.unity#L300891)

- Tob's new 8-entry stock (food + two recipe items).
  [`StartingTown.unity:509298`](../../Assets/_Game/Scenes/StartingTown.unity#L509298)

**Quest specs now name Tob**

- Sandwich offer line points the player to Tob's shop.
  [`WorkersSandwich.md:124`](../../docs/World/Quests/WorkersSandwich.md#L124)

- Step description for buying the ingredients.
  [`WorkersSandwich.md:89`](../../docs/World/Quests/WorkersSandwich.md#L89)

- Tob mentions his own written recipes.
  [`CooksExperiment.md:130`](../../docs/World/Quests/CooksExperiment.md#L130)

**Docs**

- NPC sheet Shop row (topic, memory, stock).
  [`CLAUDE.md:70`](../../Assets/_Game/Data/NPCs/KitchenHand/CLAUDE.md#L70)

- Design sheet topic table.
  [`npc-020-tob.md:73`](../../docs/World/NPCs/npc-020-tob.md#L73)

- Crafting doc seller mentions.
  [`Crafting/CLAUDE.md:28`](../../Assets/_Game/Scripts/Crafting/CLAUDE.md#L28)
