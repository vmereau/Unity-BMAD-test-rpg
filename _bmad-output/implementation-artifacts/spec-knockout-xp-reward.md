---
title: 'XP reward for knocking out an NPC'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: '49defb2'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Since spec-unconscious-knockout, beating a neutral humanoid NPC in a brawl ends in a knockout (no death), so winning the fight grants no experience at all.

**Approach:** The first time the **player's** blow knocks a given NPC out, grant a one-time knockout XP (`NPCEntity.XpOnKnockout`, separate from `XpOnKill`, same default 25). The claim is recorded per NPC and saved, so later knockouts of the same NPC give nothing. Kill XP is unchanged and still paid on top if that NPC later dies.

## Boundaries & Constraints

**Always:** Only a knockout caused by the player (`FactionMember.Of(attacker).IsPlayer`) pays; NPC-vs-NPC knockouts and the player being knocked out pay nothing. One claim per NPC identity (its `KilledFact`), stored in `WorldStateManager._worldFacts` like meal bonus claims (saved, raises no fact event, not a quest / memory condition). Knockout XP must not count as a kill (`XPSystem.TotalKills`). Route through the same shape as kills: `EntityHealth` → `PersistentID` → `WorldStateManager` → event channel → `PlayerRewards`.

**Ask First:** Changing any NPC's knockout value away from its kill value; touching `PlayerRewardSO` matching.

**Never:** An execution / finishing-blow mechanic on downed NPCs (separate spec). Changing who gets kill XP. Granting XP for the stance knockdown (the 4.6 s fall). New per-NPC fact assets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First knockout | Player knocks out Harl, never claimed | +`XpOnKnockout` XP, claim recorded, kill count unchanged | N/A |
| Repeat knockout | Harl knocked out again (same or after save/load) | No XP | N/A |
| NPC knocks NPC out | Warden knocks out Tam | No XP, no claim | N/A |
| Kill after knockout | Claimed NPC later dies | Normal kill XP (unchanged path) | N/A |
| Missing identity | NPC with no `KilledFact` on `PersistentID` | No XP | Existing `PersistentID` error path; one warning |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/EntityHealth.cs` -- `TakeDamage(amount, attacker)` decides knockout; private `KnockOut()` has no attacker yet
- `Assets/_Game/Scripts/World/PersistentID.cs` -- `RegisterDeath()` → `WorldStateManager.RegisterKill`; add sibling
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `RegisterKill`, meal claim pattern (`ClaimMealBonus`, `MealBonusKey`), `_worldFacts` saved via `CaptureFacts`
- `Assets/_Game/Scripts/Core/State/WorldFactPrefix.cs` -- key prefixes
- `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` -- add knockout XP field
- `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` -- `HandleEntityKilled` grants `XpOnKill`
- `Assets/_Game/Scripts/Player/Progression/XPSystem.cs` -- `GiveExperience` increments `TotalKills`
- `Assets/_Game/Data/Events/OnEntityKilled.asset` -- existing `GameEventSO_EntityKilled`; Core.unity (`WorldStateManager`) and `Prefabs/Player/Player.prefab` (`PlayerRewards`) reference it

## Tasks & Acceptance

**Execution:**
- [x] `NPCEntity.cs` -- add `[Header("Rewards")]`-adjacent `[SerializeField, Min(0)] int _xpOnKnockout = 25` + `XpOnKnockout` -- humanoid-only, tunable apart from kill XP
- [x] `WorldFactPrefix.cs` -- add `Knockout`
- [x] `WorldStateManager.cs` -- add `[SerializeField] GameEventSO_EntityKilled _onEntityKnockedOut` and `RegisterKnockout(KilledFact, Entity)`: null fact → warn + return; already claimed (`Knockout.XpClaimed.{fact}`) → return; else store claim and raise; plus `IsKnockoutClaimed(KilledFact)` -- dedupe lives with the saved store
- [x] `PersistentID.cs` -- `RegisterKnockout()` mirroring `RegisterDeath()`
- [x] `EntityHealth.cs` -- pass `attacker` to `KnockOut`; when `FactionMember.Of(attacker)` is the player, call `_persistentID?.RegisterKnockout()`
- [x] `XPSystem.cs` -- `GiveExperience(int amount, bool countAsKill = true)`; only kills bump `TotalKills`
- [x] `PlayerRewards.cs` -- subscribe `_onEntityKnockedOut`, grant `(e.entity as NPCEntity)?.XpOnKnockout` with `countAsKill: false`; warn in `Awake` when unassigned
- [x] `Data/Events/OnEntityKnockedOut.asset` (+ .meta) -- new `GameEventSO_EntityKilled`; wire on `WorldStateManager` in `Core.unity` and `PlayerRewards` in `Player.prefab`
- [x] `Assets/Tests/EditMode/KnockoutXpTests.cs` -- `RegisterKnockout` raises once per fact, survives `CaptureFacts`/`RestoreFacts`, null fact ignored; `PlayerRewards` grants knockout XP without bumping `TotalKills`
- [x] `Scripts/AI/CLAUDE.md`, `Scripts/Player/Progression/CLAUDE.md`, `Data/Facts/CLAUDE.md` -- replace "No `RegisterDeath`, no kill, no XP" with the knockout XP rule; event chain; claim key row

**Acceptance Criteria:**
- Given a fresh game, when the player knocks out a neutral NPC, then the XP bar rises by that NPC's `XpOnKnockout` and the log shows the XP gain.
- Given that claim, when the game is saved, reloaded and the NPC knocked out again, then no XP is granted.

## Design Notes

Payload reuses `EntityKilled` (entity + identity fact) — no new struct; the channel name carries the meaning. The claim is keyed by the NPC's `KilledFact` because it is already its unique, saved identity.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass, incl. `KnockoutXpTests`

**Manual checks:**
- Play mode: brawl a neutral NPC to knockout → +25 XP once; repeat → none. `read_console` shows no new errors/warnings.

## Suggested Review Order

**Trigger — player knockout only**

- Entry point: a 0-HP knockout by the player registers the reward.
  [`EntityHealth.cs:215`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L215)

- Mirrors `RegisterDeath`; missing identity warns and pays nothing.
  [`PersistentID.cs:71`](../../Assets/_Game/Scripts/World/PersistentID.cs#L71)

**One-time claim (saved)**

- First knockout per `KilledFact` claims and raises; unwired channel keeps the claim free.
  [`WorldStateManager.cs:68`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L68)

- Claim key lives in `_worldFacts` like meal claims — saved, no fact event.
  [`WorldStateManager.cs:82`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L82)

**Reward payout**

- Grants `XpOnKnockout` without counting a kill.
  [`PlayerRewards.cs:111`](../../Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs#L111)

- `countAsKill` flag keeps `TotalKills` honest; default unchanged for kills.
  [`XPSystem.cs:31`](../../Assets/_Game/Scripts/Player/Progression/XPSystem.cs#L31)

- Separate tunable, default 25 = current kill XP.
  [`NPCEntity.cs:51`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L51)

**Wiring & tests**

- New `OnEntityKnockedOut` channel on the manager…
  [`Core.unity:240`](../../Assets/_Game/Scenes/Core.unity#L240)

- …and on the player's rewards.
  [`Player.prefab:628`](../../Assets/_Game/Prefabs/Player/Player.prefab#L628)

- Claim once, save round-trip, not a kill, no kill count.
  [`KnockoutXpTests.cs:58`](../../Assets/Tests/EditMode/KnockoutXpTests.cs#L58)
