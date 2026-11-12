---
title: 'Racket guard exit toll — Harl collects a one-time protection fee at two gates'
type: 'feature'
created: '2026-11-12'
status: 'done'
baseline_commit: '087bf1576a218d92410959bf281dfdf575251e88'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md', '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md', '{project-root}/Assets/_Game/Data/NPCs/MEMORIES.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Harl (npc-007, `RacketGuard`) only hands out the spider job. The camp has no extortion: the player leaves the camp freely.

**Approach:** Harl works two toll gates: the quarry opening (tally post) in the first half of the day and the wilderness opening in the second. When an unpaid player enters the gate zone, he walks up and forces a toll dialogue. The player can pay a one-time protection fee, which counts for that gate only. They can say they can't pay, which works once for both gates combined. Or they can refuse (Escape or walking past counts as refusing), which starts a non-lethal brawl. If Harl loses a fight, he never collects again. If the player loses, Harl robs them (existing looting) and that gate counts as paid. Also in scope: a player knockout closes every open screen, and the `PlayerAnimationDriver.SetInCombat` NRE at startup is fixed.

## Boundaries & Constraints

**Always:** The toll state is saved through `WorldFact`s set by `WorldStateManager.SetWorldEvent`: one paid fact per gate, one shared "deferred" fact and one shared "Harl beaten" fact. The dialogue zone covers the whole gate: its width plus a few metres on each side. A gate collects only during its hours, while Harl is on duty near it (routine at the post, not in combat, conscious), and only when neither its paid fact nor the beaten fact is set. After a dialogue that doesn't end in a fight, the gate re-arms only once the player has left its zone. If the player has paid at the other gate, Harl uses a mocking opener ("already paid over there? that was *that* gate") before asking again. The fee is paid to Harl: the coins go into his inventory, so they can be looted back. Dialogue changes stay generic: a gold cost and a signal on a choice, and an NPC-forced topic. All tunables live on data assets.

**Ask First:** Changes to `IDamageable`, and any change to how the existing `Looting` flow takes gold.

**Never:** A per-day toll, crime or reputation effects, other NPCs collecting, Harl chasing the player outside his duty radius, saving the encounter's in-progress state, new animations.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior |
|----------|--------------|---------------------------|
| First pay | Player has 30 gold and enters the quarry gate at 09:00 | Harl walks up and opens the toll topic. "Pay [10 gold]" → player 20, Harl +10 coins, quarry paid. Passing again: no stop |
| Other gate | Quarry paid, wilderness gate at 16:00 | Mocking opener, then the same choices. Paying sets only wilderness paid |
| Can't pay | Not deferred | "I don't have it" → warning line, deferred set, player passes. The gate re-arms after the player leaves the zone |
| Can't pay again | Deferred set, gold < fee | Pay is shown disabled. "I don't have it" is hidden. Options: refuse |
| Refuse / Escape | Any unpaid state | Harl brawls the player (aggro, non-lethal) |
| Player loses | Harl knocks the player out | Existing robbery. That gate's paid fact is set |
| Player wins | Harl knocked out by the player (toll fight or any brawl) | Beaten fact set. No gate ever collects again. Grudging "toll" topic in his normal dialogue |
| Player flees | Harl disengages | No fact. The gate re-arms after the player leaves the zone |
| Off hours / night | Quarry at 15:00, or any gate at 22:00 | No stop |
| Knocked out with a screen open | Inventory / loot / trade / dialogue / cooking / sleep open | Each closes when the knockout starts |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityBrain.cs` -- states, `RespondToTrespasser` instant-aggression engage (brawl pattern), `HandleConfronting` (cursor-lock dialogue watch), `ResumeNonCombat`, `Looting`
- `Scripts/AI/NPC/NPCPresence.cs` -- builds `NPCDialogueRequestData` (memories / graph / inventory)
- `Scripts/World/DialogueSystem.cs`, `Scripts/UI/Dialogue/DialogueUI.cs` -- forced line path, `SelectChoice`, choice buttons
- `ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` (`ChoiceOption`) -- the choice data
- `Scripts/World/RestrictedArea.cs` / `RestrictedAreaRules.cs` -- box-zone containment and hour-window pattern to reuse
- `Scripts/AI/NPC/NpcRoutine.cs` -- Harl's duty schedule (he has no routine today)
- `Scripts/Player/PlayerHealth.cs` -- knockout start; `_onPlayerDied` listeners: `UIScreenManager`, `ContainerUI`, `CookingUI`, `SleepUI`
- `Scripts/Player/PlayerAnimationDriver.cs` -- `_humanoidBridge` is cached in `Awake`, so it is null when `SetInCombat` is called first
- `Data/NPCs/RacketGuard/` -- identity, dialogues, memories. Scene: `RacketGuard` at (5.5, 6.5), tally post (52.5, 2.5), wilderness trail start (33, 51.7)

## Tasks & Acceptance

**Execution:**
- [x] `ChoiceDialogueNode.cs` -- `ChoiceOption.goldCost` (int) and `signal` (new `DialogueSignalSO`, menu `Game/Dialogue/Signal`) -- generic consequence hooks
- [x] `ScriptableObjects/Events/GameEventSO_DialogueSignal.cs` (own file, next to the other event types) + `Data/Events/OnDialogueSignal.asset` -- payload {signal, npcName}
- [x] `DialogueSystem.cs` -- `NPCDialogueRequestData.forcedStartNode`: open with the graph context and `StartTopic` it directly. `SelectChoice`: if `goldCost` > 0, `TrySpend` (stop on failure) and add the coins to `_currentNPCInventory` (`_goldItem` = `Item_Gold`). Then raise the signal, then advance. Add a `CloseForKnockout` listener
- [x] `DialogueUI.cs` -- a choice with `goldCost` shows " (Ng)" (teach-row format) and is disabled when unaffordable (`DialogueSystem.CanAffordGold`)
- [x] `NPCPresence.cs` -- `RequestForcedTopic(StartDialogueNode)` -- same request plus `forcedStartNode`
- [x] `EntityBrain.cs` -- `Intercepting` state: `BeginIntercept(target, talkDistance, timeout)` walks at `EngageSpeed` → within talk distance with the cursor locked → stops and faces the target, `InterceptReached`. `EndIntercept()` → `ResumeNonCombat`. Abort (`InterceptAborted`) on a hit, a hostile spotted, its own knockout, the target unconscious, or the target out of reach or time. Also `StartBrawl(FactionMember)` (aggro + `ForceAware` + Engaging, factored out of instant-aggression trespass) `IsFighting(FactionMember)` and `WasHitThisFrameBy(FactionMember)`. Intercepting is reactive to hits and stores routine requests
- [x] `ScriptableObjects/Entities/NPC/TollRacketSO.cs` (new; SO definitions live under `ScriptableObjects/`) -- no fee field (single source: the pay choice's `goldCost`); deferred / beaten `WorldFact`s, first and other-gate-paid start nodes, pay / defer / refuse signals, talk distance, duty radius, intercept timeout
- [x] `Scripts/AI/NPC/TollGate.cs` (new) + pure `TollGateRules.cs` -- static active list. Box zone (child `BoxCollider`s, on-demand containment). Hours window, collector brain, post transform, paid fact. Flow: armed → player in zone and `CanCollect` → `BeginIntercept` → reached → `RequestForcedTopic` (other-gate opener when any sibling gate with the same racket is paid) → dialogue closed → outcome from the last signal (pay → paid, defer → deferred, refuse or none → `StartBrawl`) → watch the brawl: player unconscious from Harl → paid; Harl unconscious while targeting the player → beaten; Harl out of combat → disarmed until zone exit. Listens to `EntityHealth.KnockedOut` outside toll fights too (beaten when targeting the player)
- [x] Harl data -- `Data/NPCs/RacketGuard/Dialogues/Toll/` (opener, other-gate opener, shared choice node, pay / defer / refuse replies), `Memories/Toll/Mem_RacketGuard_TollCanDefer` (invalidated by deferred, gates the defer choice), `Mem_RacketGuard_TollBeaten` + grudging topic, facts `WorldFact_Toll_QuarryPaid|WildernessPaid|Deferred|HarlBeaten`, signals, `TollRacket_Harl.asset`. Lines in Harl's voice ("new meat"; `CLAUDE.md` identity)
- [x] `StartingTown.unity` -- `TollGates/TollGate_Quarry` (06–14) and `TollGate_Wilderness` (14–21), each zone spanning its gate. Harl's `NpcRoutine`: 06–14 quarry post, 14–21 wilderness post, 21–06 garrison quarter spot. Wire `_onDialogueSignal`, `_goldItem`, `_onPlayerKnockedOut`
- [x] `PlayerHealth.cs` + `Data/Events/OnPlayerKnockedOut.asset` (`GameEventSO_Void`) -- raised when the knockout starts. `UIScreenManager`, `ContainerUI`, `NPCTradeUI`, `CookingUI`, `SleepUI` and `DialogueSystem` close on it, through the same paths as `_onPlayerDied`
- [x] `PlayerAnimationDriver.cs` -- resolve `_humanoidBridge` lazily (a property) -- fixes the startup NRE
- [x] `Tests/EditMode/TollGateRulesTests.cs` -- `CanCollect` truth table (hours, paid, beaten, armed, on duty) and opener choice
- [x] Docs -- `Scripts/AI/CLAUDE.md` (Intercepting, TollGate), `Data/NPCs/DIALOGUE.md` (goldCost / signal / forced topic), `Data/Facts/CLAUDE.md` (first `SetWorldEvent` caller), `RacketGuard/CLAUDE.md`, `docs/World/world-index.md`

**Acceptance Criteria:**
- Given the quarry gate is paid, when the player crosses it at any hour, then Harl never stops them there.
- Given Harl was knocked out by the player, when the player enters either gate during its hours, then Harl stays at his post.
- Given the player is knocked out with the inventory open, then the inventory closes and no equip is possible until they get up.
- Given Play mode starts, then no `NullReferenceException` from `PlayerAnimationDriver` is logged.

## Design Notes

The gate owns the encounter, and the brain only moves and fights (`Intercepting`, `StartBrawl`). The gate polls the brain and the health components in the same assembly; no cross-system C# events. The dialogue reports outcomes through generic `DialogueSignalSO`s, not dialogue facts: dialogue facts are permanent and re-raise on replay. The forced toll topics are not in Harl's normal topic list, so Quest Explorer V4 may flag them as unattributed. That is expected; note it in his `CLAUDE.md`.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green, including `TollGateRulesTests`
- `read_console` after compile and on entering Play mode -- expected: no errors, no `SetInCombat` NRE

**Manual checks:**
- Play mode in StartingTown: run through each row of the I/O matrix with `GameClock` set to 09:00 / 16:00 / 22:00 and gold set through the dev tools.

## Suggested Review Order

**Gate encounter (entry point)**

- The gate stops an unpaid player only when in hours, armed and the collector is on duty.
  [`TollGate.cs:121`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L121)

- Walking past mid-approach = refusing → brawl; other aborts stay armed.
  [`TollGate.cs:150`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L150)

- Dialogue confirmed open synchronously (cursor), never mistaken for a refusal.
  [`TollGate.cs:165`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L165)

- Pay / defer applied on the pick, so an interrupted talk can't lose a payment.
  [`TollGate.cs:270`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L270)

- Loss pays this gate only if the collector was the opponent.
  [`TollGate.cs:234`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L234)

- Beaten = knocked out by the player's own blow, in any fight.
  [`TollGate.cs:290`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L290)

**Brain: intercept and brawl**

- Non-combat walk-up that waits for a locked cursor, then faces the player.
  [`EntityBrain.cs:1266`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1266)

- Status reads Aborted whenever the state left Intercepting another way.
  [`EntityBrain.cs:58`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L58)

- Brawl reuses the instant-aggression aggro path; safe mid-stagger.
  [`EntityBrain.cs:1313`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1313)

- Hostiles, lost target, timeout and unreachable paths end the intercept.
  [`EntityBrain.cs:1338`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1338)

- Knockout attribution from NotifyHitBy, kept for the frame.
  [`EntityBrain.cs:72`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L72)

**Generic dialogue additions**

- NPC-initiated topic: straight into the chain with full graph context.
  [`DialogueSystem.cs:107`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L107)

- Paid choice: gold to the NPC's inventory, then the signal, then advance.
  [`DialogueSystem.cs:250`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L250)

- Forced-topic UI mode: Escape anywhere, chain end closes.
  [`DialogueUI.cs:230`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L230)

- Unaffordable paid rows are disabled with the cost in red.
  [`DialogueUI.cs:490`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L490)

- Choice data: goldCost (shared with teach choices) and signal.
  [`ChoiceDialogueNode.cs:20`](../../Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs#L20)

- Presence builds the forced request like a normal talk.
  [`NPCPresence.cs:88`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L88)

**Knockout closes screens / startup NRE**

- Knockout broadcasts OnPlayerKnockedOut; screens reuse their death-close handler.
  [`PlayerHealth.cs:171`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L171)

- Example listener.
  [`UIScreenManager.cs:59`](../../Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs#L59)

- Lazy bridge fixes SetInCombat being called before Awake.
  [`PlayerAnimationDriver.cs:24`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L24)

**Peripherals**

- Racket data shared by both gates (facts, openers, signals, tunables).
  [`TollRacketSO.cs:13`](../../Assets/_Game/ScriptableObjects/Entities/NPC/TollRacketSO.cs#L13)

- Pure rules behind the gate.
  [`TollGateRules.cs:24`](../../Assets/_Game/Scripts/AI/NPC/TollGateRules.cs#L24)

- Truth tables for collect, hours, opener, answer and brawl.
  [`TollGateRulesTests.cs:1`](../../Assets/Tests/EditMode/TollGateRulesTests.cs#L1)
