---
title: 'Knockout gold looting — the winning NPC robs the knocked-out player'
type: 'feature'
created: '2026-11-12'
status: 'done'
baseline_commit: 'defd66f2ea4e736466ef053392f65d045ccd5bfe'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When an NPC wins a brawl against the player, it just walks back to its routine. Losing a fight costs the player nothing.

**Approach:** The NPC whose blow knocked the player out walks to the body and taunts the player in a speech bubble. It plays the `picking up` clip over the body and takes 15 + a random 10–20 % of the player's gold, capped at what the player has. Then it says one more line: a thank-you when it got the full amount, a jab at how poor the player is otherwise. The gold goes into the NPC's inventory, so the player can loot it back by knocking out or killing that NPC.

## Boundaries & Constraints

**Always:** Only the NPC that delivered the knockout blow loots, and only when the victim is the player. The gold is taken only if the player is still lying unconscious at the grab moment. If the player starts getting up before then (during the walk or the clip), the looting is cancelled and the NPC returns to its routine. A player with less than 15 gold loses all of it, and the NPC uses the "poor" line. With 0 gold, nothing is taken and the NPC still uses the "poor" line. All tunables and bark sets go in `CombatConfigSO`. Cross-system access to gold goes through a `GameEventSO` request channel only.

**Ask First:** Changes to the `IDamageable` interface.

**Never:** NPC-vs-NPC looting, taking items other than gold, crime / reputation / theft flagging, saving the looting state, looting by bystanders or other brawlers.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Rich player | Player has 200 gold, KO'd by a neutral NPC | Taunt bubble → walk → pickup clip → 35–55 gold moves to the NPC inventory → thank-you bubble → routine | N/A |
| Poor player | Player has 8 gold | All 8 taken, "poor" bubble | N/A |
| Broke player | Player has 0 gold | Nothing taken, "poor" bubble | N/A |
| Gets up first | Player starts getting up before the grab moment | No gold taken, no second bubble, NPC returns to its routine | N/A |
| Loot back | Player knocks out / kills the robber | The gold stack is in its loot screen; taking it credits `GoldSystem` | Existing gold-loot pipeline |
| Robber attacked | Another entity hits the NPC while it loots | Normal damage reaction (looting ends) | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityMeleeAttacker.cs` `HandleHit` -- the blow that knocks the target out (conscious before `TakeDamage`, unconscious after)
- `Scripts/AI/EntityBrain.cs` -- `HandleAttack` / `HandleEngage` `!HasValidTarget()` branch (the winner disengages today), `HandleHealthChanged` state list, `RaiseSpeechBubble`, `ResumeNonCombat`
- `Scripts/Player/PlayerHealth.cs` -- `_gettingUp` (private today), `IsUnconscious`
- `Scripts/Economy/GoldSystem.cs` -- player balance; `GoldLootRequest` (`Scripts/Core/`) is the acknowledged-request pattern to copy
- `Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `HumanoidAnimationBridge.cs` -- animator writes
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`, `Animations/picking up.fbx` (untracked, Humanoid import)
- `ScriptableObjects/Config/CombatConfigSO.cs`; `Data/NPCs/Barks/`; `Data/Items/Item_Gold.asset`

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Economy/GoldTheftRules.cs` (new, pure) -- `Amount(gold, baseAmount, fraction) = min(gold, base + RoundToInt(gold × fraction))`, ≥ 0 -- testable rule
- [x] `Scripts/Core/GoldTheftRequest.cs` + `Scripts/Core/Events/GameEventSO_GoldTheftRequest.cs` (separate file) + `Data/Events/OnGoldTheftRequested.asset` -- request {Victim, BaseAmount, Fraction}, result {Handled, GoldBefore, Taken}
- [x] `GoldSystem.cs` -- listen; when `Victim == gameObject` and not yet handled: compute with `GoldTheftRules`, deduct, raise `OnGoldChanged`, fill the result. Wire it on `Player.prefab`
- [x] `Scripts/Combat/IKnockoutRecovery.cs` (new) -- `bool IsGettingUp`; `PlayerHealth` implements it (`_gettingUp`) -- "still lying" without changing `IDamageable`
- [x] `CombatConfigSO.cs` -- `[Header("Knockout loot")]`: `knockoutLootBaseGold` 15, fraction min / max 0.10 / 0.20, `knockoutLootReachDistance`, `knockoutLootGrabTime`, `knockoutLootClipDuration` (measured clip length), `knockoutLootGold` (`Item_Gold`), `knockoutLootTauntBarks` / `ThanksBarks` / `PoorBarks` (`BarkSetSO`); fill `CombatConfig.asset`
- [x] `Data/NPCs/Barks/Barks_KnockoutTaunt|Thanks|Poor.asset` -- taunt: "Let's see what you have.", "I'm gonna help myself to your stuff."; thanks: "Nice, that will come in handy. Thank you."; and 2–3 more lines per set in the same tone
- [x] `EntityHealth.cs` -- public `CombatConfig` getter (the brain reads its tunables from there)
- [x] `EntityMeleeAttacker.cs` -- record `KnockedOutVictim` (the `FactionMember` of a target that went conscious → unconscious from this hit); cleared by `EndAttack` consumers through a `TakeKnockedOutVictim()` method
- [x] `EntityBrain.cs` -- new `Looting` state: on the `!HasValidTarget()` branch, if the KO'd victim is the current target and `IsPlayer`, enter Looting instead of disengaging. Enter: combat off, `BaseSpeed`, taunt bubble, walk to the player. Arrival (flat ≤ reach) → stop, face, `TriggerPickUp`. At `grabTime`, if the player is still lying, raise the theft request, add `Taken` × `Item_Gold` to its own `InventorySystem`, then the thanks bubble if `GoldBefore ≥ base`, else poor. At `clipDuration` → `ResumeNonCombat`. Each tick before the grab: player dead / not unconscious / `IsGettingUp` → cancel → `ResumeNonCombat`. Add `Looting` to `HandleHealthChanged`'s reactive states. Wire `_onGoldTheftRequested` on `Entity_base` (and on unpacked scene NPCs like Brann)
- [x] `AIAnimationDriver` (virtual no-op `TriggerPickUp`), `HumanoidAIAnimationDriver`, `HumanoidAnimationBridge` (`PickUp` trigger) -- animator writes stay in the bridge
- [x] `picking up.fbx` import (Humanoid, avatar like the other Humanoids clips, no loop) + `Humanoid_Template.controller` -- `PickUp` trigger, Base Layer `PickUp` state entered from `LockOn Locomotion` only (changed in review: an AnyState entry needs exclusions; the brain resets the trigger on every looting exit instead), exit to locomotion. Through the `AnimatorController` API via MCP, not YAML
- [x] `Tests/EditMode/GoldTheftRulesTests.cs` -- 200 → 35..55, 8 → 8, 0 → 0, exactly 15, rounding
- [x] Docs -- `Scripts/AI/CLAUDE.md` (Unconscious → Looting), `Controllers/CLAUDE.md` (new param / state), `Scripts/UI/HUD/CLAUDE.md` (new bubble source)

**Acceptance Criteria:**
- Given a neutral NPC knocks the player out, when it reaches the body, then the pickup clip plays and the HUD gold drops when the clip reaches the grab moment.
- Given the robber is then knocked out, when the player loots it, then the stolen gold shows as one stack and taking it restores the HUD gold.
- Given two NPCs brawl the player and one lands the knockout, then only that NPC loots, and the other returns to its routine.

## Design Notes

The attacker knows it delivered the knockout: `IsUnconscious` flips inside its own `TakeDamage` call. That keeps the decision inside `Game.AI`, with no new victim → attacker channel. The grab is timed (`knockoutLootGrabTime`) like the stance durations, not a clip event. The `GoldItemSO` adds one coin per `AddItem` call, so the brain adds the coins in a loop (an NPC inventory has no capacity limit).

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green, including `GoldTheftRulesTests`
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play mode in StartingTown: lose a brawl with 200 gold, then 8, then 0 (check the bubbles and HUD gold); get up before the NPC arrives (cancelled); knock the robber out and loot the gold back.

## Suggested Review Order

**Who loots: the knockout blow**

- Entry point: the attacker sees its own hit flip the target to unconscious.
  [`EntityMeleeAttacker.cs:272`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L272)

- The winner robs instead of disengaging — only for its own knockout of the player.
  [`EntityBrain.cs:1046`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1046)

- Hooked into the existing "target lost" branches of Engaging / Attacking.
  [`EntityBrain.cs:599`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L599)

- Stale knockout records cleared on disengage / own knockout / death / disable.
  [`EntityBrain.cs:1677`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1677)

**Looting state**

- Walk → clip → grab timed from the clip's real start → routine; cancel when the player stirs.
  [`EntityBrain.cs:1097`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1097)

- Partial / invalid paths give up instead of re-pathing for 8 s.
  [`EntityBrain.cs:1153`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1153)

- Single exit hook: drops the latched trigger and blends a playing clip out.
  [`EntityBrain.cs:1202`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1202)

- A hit on the robber fights back, ending the loot.
  [`EntityBrain.cs:1268`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1268)

- "Still lying" = unconscious and not yet in the get-up clip.
  [`PlayerHealth.cs:59`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L59)

**Gold across systems**

- The robber raises a theft request and stores the coins in its own inventory.
  [`EntityBrain.cs:1173`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1173)

- The player's GoldSystem answers the channel; Economy never sees AI types.
  [`GoldSystem.cs:49`](../../Assets/_Game/Scripts/Economy/GoldSystem.cs#L49)

- Pure rule: base + fraction, capped at what's left.
  [`GoldTheftRules.cs:12`](../../Assets/_Game/Scripts/Economy/GoldTheftRules.cs#L12)

- Handled-once result, like GoldLootRequest's accept.
  [`GoldTheftRequest.cs:29`](../../Assets/_Game/Scripts/Core/GoldTheftRequest.cs#L29)

- Bulk add with one log line instead of one per coin.
  [`InventorySystem.cs:88`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L88)

**Animation**

- Pick-up trigger, state query and cancel — all animator writes stay in the bridge.
  [`HumanoidAnimationBridge.cs:249`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L249)

**Peripherals**

- Tunables and bark sets shared by every NPC.
  [`CombatConfigSO.cs:87`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L87)

- The brain reads tunables through its health component.
  [`EntityHealth.cs:44`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L44)

- Amount formula cases: rich, poor, broke, exact base, rounding.
  [`GoldTheftRulesTests.cs:8`](../../Assets/Tests/EditMode/GoldTheftRulesTests.cs#L8)
