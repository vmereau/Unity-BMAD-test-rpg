---
title: 'Caught stealing — give the goods back or fight for them'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: 'da145ad3f3b6e6242cb1026ef5e9eef061bfe949'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A witness who catches a thief only scolds them; the player keeps everything, so being caught costs nothing.

**Approach:** Record what each theft took (the picked-up item, or every item / gold unit taken from the owned container after it was opened). The catcher's scold line is followed by two choices: **Give it back** moves the stolen goods the player still carries (items + the exact stolen gold) into the catcher's inventory; **Refuse** (or Escape) starts an armed brawl. If the player is knocked out, the NPC that lands the knockout loots as today **and** recovers the refused stolen goods into its own inventory.

## Boundaries & Constraints

**Always:** All thefts a pursuer adopted during one chase add up. Only goods the player still carries are returned / recovered (inventory items, clamped counts; gold clamped to balance). Nothing left → today's scold line only, no choices. The scold bark stays the opening line. Goods go to the catcher (give back) or the knockout winner (refuse), not the owner. Refusal brawl is armed (`StartBrawl(player)`, no fists request). Cross-system access to the player's inventory / gold goes through a `GameEventSO` request channel. Ledger is runtime only.

**Ask First:** Changes to `IDamageable`, the `DialogueUI` state machine beyond the forced opening line, or the knockout gold formula.

**Never:** Marking items as stolen in the inventory / UI, equipped items, saving the ledger, reputation / bounty, owner-specific behaviour, trespass incidents, consequences when the player wins or flees.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Give back item | Stole 1 potion, caught | Scold line → choices → Give back: potion leaves player, enters catcher inventory → catcher resumes | N/A |
| Give back container | Took 2 bread + 12 gold from owned chest | Both bread + exactly 12 gold move to catcher (gold as `Item_Gold` units) | N/A |
| Partly used | Ate 1 of 2 stolen bread | Only 1 bread returned | N/A |
| Nothing left | Opened chest, took nothing / used all | Scold line only (old behaviour) | N/A |
| Several thefts | Stole potion, then opened chest mid-chase | Goods of both incidents offered | N/A |
| Refuse | Picks Refuse or Escape | Catcher brawls the player (armed) | Passive catcher → scold ends, routine |
| Lose brawl | Player KO'd by catcher or a defending guard | Winner loots gold (existing) + stolen goods still carried → its inventory | Got up before grab → nothing |
| Win / flee | Player KOs catcher or escapes | Player keeps goods; refused incident cleared | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/World/InteractionSystem.cs` `RaiseTheft` -- builds `TheftCommittedData`; knows the `Ownership` sibling (`ItemPickup.Item`, container `InventorySystem`)
- `ScriptableObjects/Events/GameEventSO_TheftCommitted.cs` -- theft payload
- `Scripts/UI/Inventory/ContainerUI.cs` `TakeUnit` -- raises `OnContainerItemTaken {sourceInventory, item}` per unit (gold too)
- `Scripts/AI/EntityBrain.cs` -- `HandleTheftCommitted`, `StartPursuitTimers`, `TransitionToConfronting`, `HandleConfronting`, `EndPursuit`, `StartBrawl`, `StealGold` / Looting, `SetCombatState`
- `Scripts/AI/NPC/NPCPresence.cs` `RequestForcedTopic` -- NPC-initiated topic with full graph context
- `Scripts/World/DialogueSystem.cs` -- `forcedLine` / `forcedStartNode` handling, `ApplyChoice` raises `OnDialogueSignal {signal, npcName}`
- `Scripts/Inventory/InventorySystem.cs`, `Scripts/Economy/GoldSystem.cs` -- player handlers (copy `GoldTheftRequest` pattern, `Scripts/Core/`)
- `ScriptableObjects/Config/StealthConfigSO.cs` -- theft tunables home
- `Scripts/AI/NPC/TollGate.cs` -- reference pattern: forced topic → signal → pay / brawl

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Core/StolenGoodsLedger.cs` (new, static, `SubsystemRegistration` reset) -- per theft id: item counts + gold; `Record(id, item, n)`, `RecordGold(id, n)`, `Collect(ids)` (aggregate), `MarkRefused(ids)`, `TakeRefused()` (returns + clears), `Clear(ids)` -- pure, testable bookkeeping
- [x] `GameEventSO_TheftCommitted.cs` + `InteractionSystem.RaiseTheft` -- add `ItemSO item` (pickup) and `InventorySystem sourceInventory` (container) to the payload -- the tracker needs both
- [x] `Scripts/World/StolenGoodsTracker.cs` (new, on the Player prefab) -- listens to `OnTheftCommitted` (records the pickup item; remembers container → theft id) and `OnContainerItemTaken` (unit from the remembered container → `Record`, `GoldItemSO` → `RecordGold(GoldValue)`) -- containers are stolen per unit taken
- [x] `Scripts/Core/StolenGoodsRequest.cs` + `ScriptableObjects/Events/GameEventSO_StolenGoodsRequest.cs` (separate file) + `Data/Events/OnStolenGoodsRequested.asset` -- {Victim, items, gold, Take}; result per part: available / taken counts, handled flags
- [x] `InventorySystem.cs` -- when `Victim == gameObject`: fill available item counts; on `Take` remove them. `GoldSystem.cs` -- same for gold (clamped to balance, `OnGoldChanged`). Wire channel on `Player.prefab`
- [x] `StealthConfigSO.cs` (+ asset) -- `theftReturnDialogue` (`StartDialogueNode`), `theftReturnSignal`, `theftRefuseSignal` (`DialogueSignalSO`)
- [x] `Data/NPCs/Shared/` (new assets) -- `Dialogue_TheftReturn` start → choice node: "[Give it back]" (return signal), "[Refuse]" (refuse signal); `Signal_TheftReturn`, `Signal_TheftRefuse`
- [x] `NPCPresence.cs` + `NPCDialogueRequestData` + `DialogueSystem.cs` -- `RequestForcedTopic(node, openingLine)`: with both a forced line and a start node, show the line first, then continue into the node's chain -- keeps the random scold line
- [x] `EntityBrain.cs` -- pursuer keeps a list of adopted theft ids (non-trespass). At catch: query the player's goods for those ids; none left → old forced line; else forced topic with the scold line. Listen to `OnDialogueSignal` (own `entityName`, Confronting): return → `Take` request → add items + gold (`knockoutLootGold` units) to own inventory, `Clear(ids)`; dialogue closed without return → `MarkRefused(ids)` + `StartBrawl(player)` (passive / fails → `Clear`, routine). Looting grab: also `TakeRefused()` → take request → own inventory. Refused ids are cleared when the catcher's brawl ends with the player conscious. Wire `_onDialogueSignal` + `_onStolenGoodsRequested` on `Entity_base` and unpacked scene NPCs
- [x] `Tests/EditMode/StolenGoodsLedgerTests.cs` -- record / aggregate across ids, gold, refused take-and-clear, reset
- [ ] Docs -- `Scripts/AI/CLAUDE.md` (theft section), `Scripts/World/CLAUDE.md` (tracker, ledger), `Scripts/UI/Dialogue/CLAUDE.md` (opening line + forced topic)

**Acceptance Criteria:**
- Given the player stole the Blacksmith's potion and is caught, when the dialogue opens, then a scold line shows, then the two choices.
- Given the player picks Give it back, when the dialogue closes, then the potion is in the catcher's inventory and the catcher walks back to its routine.
- Given the player refuses and gets knocked out, when the winner reaches the body, then the HUD gold drops (stolen gold + the usual cut) and the stolen items appear in the winner's loot.
- Given a normal Talk, a trespass scold or the toll dialogue, when used, then behaviour is unchanged.

## Design Notes

The ledger lives in `Game.Core` so World records and AI reads without either knowing the other. The query-before-dialogue decides scold-only vs choices; the take happens on the pick (like the toll), so an interrupted dialogue never takes goods without the choice. Refusal = no return signal by the time the dialogue closes (same rule as the toll's none / refuse).

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green incl. `StolenGoodsLedgerTests`
- `read_console` after compile -- expected: no errors

**Manual checks:**
- StartingTown: steal the owned potion → give back; open the Merchant's chest, take items + gold → refuse → lose the brawl → loot the winner; take nothing from the chest → scold only; refuse then win the brawl.

## Suggested Review Order

**Catch: give-back offer**

- Entry point: at the catch, the scold opens the return topic when the thief still carries goods.
  [`EntityBrain.cs:1168`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1168)

- Falls back to the old lone scold line when nothing is offered.
  [`EntityBrain.cs:1120`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1120)

- Every theft adopted during one chase is kept, so they add up.
  [`EntityBrain.cs:948`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L948)

- Give-back choice moves the goods into the catcher's inventory; works even if it was hit.
  [`EntityBrain.cs:1202`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1202)

**Refusal and knockout recovery**

- Closing without giving back = refusal: armed brawl, incidents marked refused.
  [`EntityBrain.cs:1223`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1223)

- Dialogue closed while the offer stood triggers the refusal.
  [`EntityBrain.cs:1145`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1145)

- Brawl won / fled clears the refusal; a downed player leaves it for the winner.
  [`EntityBrain.cs:1254`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1254)

- Knockout winner recovers refused goods after the usual gold cut.
  [`EntityBrain.cs:1422`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1422)

- Any looting exit settles refusals, so none linger to a later knockout.
  [`EntityBrain.cs:1485`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1485)

- Take request, then store items + gold coins in the NPC inventory.
  [`EntityBrain.cs:1280`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1280)

**What was stolen**

- Static ledger keyed by theft id; World writes, AI reads.
  [`StolenGoodsLedger.cs:80`](../../Assets/_Game/Scripts/Core/StolenGoodsLedger.cs#L80)

- Pickup / harvest item and count, or the container's inventory, in the theft payload.
  [`InteractionSystem.cs:295`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L295)

- Player tracker: records units taken from the stolen container; gold as an amount.
  [`StolenGoodsTracker.cs:53`](../../Assets/_Game/Scripts/World/StolenGoodsTracker.cs#L53)

- Container session ends on any other open; a save load forgets everything.
  [`StolenGoodsTracker.cs:67`](../../Assets/_Game/Scripts/World/StolenGoodsTracker.cs#L67)

**Player side of the request**

- Query / take clamped to what is still carried, per item.
  [`InventorySystem.cs:53`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L53)

- Exact stolen gold, clamped to the balance.
  [`GoldSystem.cs:53`](../../Assets/_Game/Scripts/Economy/GoldSystem.cs#L53)

- Request object: one handler per part, results clamped and handled once.
  [`StolenGoodsRequest.cs:12`](../../Assets/_Game/Scripts/Core/StolenGoodsRequest.cs#L12)

**Dialogue: scold line before choices**

- Forced line + start node: line shown first via a reused runtime text node.
  [`DialogueSystem.cs:165`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L165)

- New overload opening a forced topic with an opening line.
  [`NPCPresence.cs:122`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L122)

**Peripherals**

- Shared topic + signals referenced from the stealth config.
  [`StealthConfigSO.cs:100`](../../Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs#L100)

- Ledger and request tests.
  [`StolenGoodsLedgerTests.cs:1`](../../Assets/Tests/EditMode/StolenGoodsLedgerTests.cs#L1)
