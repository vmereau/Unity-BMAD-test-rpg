---
title: 'Kill / knockout XP only with player credit (own kill, friendly kill, assist)'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '8cd00652f28197166129daa832413d0031068ccd'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every entity death pays the player its `XpOnKill`, whoever killed it (a monster killing a worker, a guard killing a bandit far away). Knockout XP is the opposite extreme: only the player's own blow counts, so a knockout the player set up earns nothing.

**Approach:** The victim (`EntityHealth`) remembers whether the player landed damage, and at death / knockout resolves a **player credit** from the killing blow's source: the player, a Friendly-tier NPC (`PlayerRelationship.Of`), or an assist (player damaged it earlier). The kill fact is always recorded; only XP depends on the credit.

## Boundaries & Constraints

**Always:**
- Credit = killer is the player **or** killer is Friendly to the player **or** the player landed damage on this entity (any amount > 0) since it was enabled. Unknown killer (null) → assist only.
- `RegisterKill` always sets the `KilledFact` and raises `OnEntityKilled` (quests, memories, bonus rewards unchanged); the payload carries the credit.
- Base `XpOnKill` (and the kill count) only when credited. Matching `PlayerRewardSO` bonus rewards are paid regardless of credit.
- Knockouts use the same rule; a knockout without credit does **not** call `RegisterKnockout`, so the one-time claim stays free.
- Execute stomp (`Execute(executor)`) uses the executor as the killer.
- The "player damaged" flag resets only in `EntityHealth.OnEnable` (with health). Not saved.

**Ask First:** changing the credit for Neutral-tier NPC kills, or any change to quest / fact behaviour.

**Never:** no new event channel or cross-system `event Action`; no damage-share threshold or time window; no change to `PlayerHealth`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Own kill | Player's blow kills a spider | Fact set, XP paid, kill counted | N/A |
| Friendly kill | Tam (Friendly) kills a rat the player never hit | XP paid | N/A |
| Assist | Player hits a bandit, a guard (Neutral) kills it | XP paid | N/A |
| No credit | A spider kills a worker the player never hit | Fact set, no XP, bonus rewards still paid | N/A |
| Unknown source | `TakeDamage(amount)` kills, player never hit | No XP | N/A |
| Knockout, assist | Player hits Harl, a guard brawl knocks him out | Knockout XP once (claim made) | N/A |
| Knockout, no credit | Two NPCs brawl, player never involved | No claim, no XP; a later credited knockout still pays | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/EntityHealth.cs` -- victim: `TakeDamage(amount, attacker)`, `KnockOut`, `Execute`, `Die`; calls `PersistentID`
- `Assets/_Game/Scripts/AI/PlayerRelationship.cs` -- `Of(FactionMember)` → Friendly tier
- `Assets/_Game/Scripts/AI/FactionMember.cs` -- `Of(GameObject)`, `IsPlayer`
- `Assets/_Game/Scripts/World/PersistentID.cs` -- `RegisterDeath()` / `RegisterKnockout()` → `WorldStateManager`
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `RegisterKill` raises `OnEntityKilled`
- `Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs` -- kill event payload (SO refs only)
- `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` -- `HandleEntityKilled` pays XP + bonus rewards
- `Assets/Tests/EditMode/KnockoutXpTests.cs`, `WorldStateManagerFactsTests.cs` -- existing reward / kill tests

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/AI/KillCredit.cs` -- new static `Game.AI.KillCredit`: pure `Evaluate(bool killerIsPlayer, bool killerIsFriendly, bool playerAssisted)` → `KillCreditKind` enum (None / Player / Friendly / Assist, first match in that order) and `Resolve(GameObject killer, bool playerAssisted)` using `FactionMember.Of` + `PlayerRelationship.Of` -- one testable rule for kills and knockouts
- [x] `Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs` -- add `bool playerCredited` (ctor param, default false) -- payload carries the credit
- [x] `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `RegisterKill(fact, entity = null, bool playerCredited = false)` passes it into the payload -- fact still always set
- [x] `Assets/_Game/Scripts/World/PersistentID.cs` -- `RegisterDeath(bool playerCredited)`; doc update -- forward the credit
- [x] `Assets/_Game/Scripts/AI/EntityHealth.cs` -- `_playerDamaged` set in `TakeDamage` when the attacker `IsPlayer` and effective damage > 0, cleared in `OnEnable`; `Die(GameObject killer, …)` resolves credit and calls `RegisterDeath(credited)`; `KnockOut` calls `RegisterKnockout()` only when credited; `Execute` passes the executor; log the credit kind -- victim decides, like knockouts
- [x] `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` -- `HandleEntityKilled`: base XP only if `e.playerCredited`; bonus loop unchanged -- per user decision
- [x] `Assets/Tests/EditMode/KillCreditTests.cs` -- `Evaluate` truth table; `RegisterKill` payload carries the flag; `PlayerRewards` pays XP only when credited and pays a matching bonus `PlayerRewardSO` either way; uncredited knockout covered via `Resolve(null, false) == None` (EntityHealth needs PersistentID + Entity + config + two FactionMembers, too heavy for EditMode) -- matrix coverage
- [x] `Assets/_Game/Scripts/Player/Progression/CLAUDE.md`, `Assets/_Game/Scripts/AI/CLAUDE.md` -- document the credit rule in the event chain / Unconscious section

**Acceptance Criteria:**
- Given the Player prefab and a killed entity with no credit, when `OnEntityKilled` fires, then `XPSystem.CurrentXP` and `TotalKills` are unchanged and the `KilledFact` is set.
- Given an existing quest step on a kill fact, when a non-credited kill happens, then the step still completes.
- Given all EditMode tests, when run, then they pass (including existing `KnockoutXpTests`).

## Design Notes

Credit is resolved at death time from the **killing blow's** `GameObject` (the `attacker` already passed to `TakeDamage`), not the last hit by anyone — so the order is: own kill → friendly → assist. Friendly is read live via `PlayerRelationship.Of`, so Tam counts only once his Friendly override is active.

```csharp
if (CurrentHealth <= 0f) { if (knockout) KnockOut(attacker); else Die(attacker); return; }
// Die: var kind = KillCredit.Resolve(killer, _playerDamaged); _persistentID?.RegisterDeath(kind != KillCreditKind.None);
```

## Verification

**Commands:**
- `mcp__UnityMCP__run_tests` (EditMode) -- expected: all pass
- `mcp__UnityMCP__read_console` (Error) -- expected: no compile errors

## Suggested Review Order

**Credit rule**

- Entry point: one pure rule, first match player → Friendly → assist.
  [`KillCredit.cs:25`](../../Assets/_Game/Scripts/AI/KillCredit.cs#L25)

- Resolves the killing blow's GameObject to the player / a Friendly tier.
  [`KillCredit.cs:16`](../../Assets/_Game/Scripts/AI/KillCredit.cs#L16)

**Victim decides (EntityHealth)**

- Assist flag: only landed player damage; resolved through the same `FactionMember.Of` as knockouts.
  [`EntityHealth.cs:181`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L181)

- Death: credit resolved from the killer, forwarded to the kill registration.
  [`EntityHealth.cs:282`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L282)

- Knockout: uncredited ones skip `RegisterKnockout`, keeping the one-time claim free.
  [`EntityHealth.cs:250`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L250)

- Execute stomp passes the executor as killer.
  [`EntityHealth.cs:265`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L265)

- Flag reset with health on (re-)enable only — per user decision.
  [`EntityHealth.cs:126`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L126)

**Kill event & rewards**

- Fact always recorded; credit rides on the payload (default false).
  [`WorldStateManager.cs:60`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L60)

- Base kill XP + kill count gated by credit.
  [`PlayerRewards.cs:97`](../../Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs#L97)

- Bonus rewards always paid, never count the kill (review patch).
  [`PlayerRewards.cs:106`](../../Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs#L106)

- Payload field; knockout channel always raised credited.
  [`EntityKilled.cs:21`](../../Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs#L21)
  [`WorldStateManager.cs:80`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L80)

- Forwarding signature (no default — forces callers to decide).
  [`PersistentID.cs:57`](../../Assets/_Game/Scripts/World/PersistentID.cs#L57)

**Tests**

- Truth table, payload flag, credited / uncredited XP, bonus without kill count.
  [`KillCreditTests.cs:1`](../../Assets/Tests/EditMode/KillCreditTests.cs#L1)

- Reflection call updated for the new `ApplyRewards` parameter.
  [`PlayerRewardItemTests.cs:147`](../../Assets/Tests/EditMode/PlayerRewardItemTests.cs#L147)
