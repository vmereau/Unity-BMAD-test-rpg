---
title: 'Player relationship tiers (Hostile / Neutral / Friendly)'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: 'd3e758a'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPCs only know "hostile or not" towards the player. There is no Friendly tier, no shared query other systems (dialogue) can read, and hostiles attack in silence.

**Approach:** Add a readable tier `PlayerRelationship.Of(npc)` → Hostile / Neutral / Friendly. It is derived from the faction (hostile list → Hostile, allied with `Faction_Player` → Friendly, else Neutral), with an optional per-NPC Neutral / Friendly override gated by facts. The brain reacts to it: hostiles bark on sight; friendlies help the player, warn before brawling him, and never loot him.

## Boundaries & Constraints

**Always:**
- Hostile is faction-only (`FactionSO.AreHostile` with the player's faction). An NPC override can only pick Neutral or Friendly, and only when its faction is not hostile.
- Override conditions: any listed `Fact` true through `WorldStateManager.GetFact` (empty list = always; no manager = false). Read live, never cached.
- Hostile on sight: an `NPCEntity` (never monsters) that reacts to the player (Warning / Engaging) raises one `hostileSightBarks` bubble, at most once per `hostileSightBarkCooldown`.
- Friendly assist: a Friendly, non-passive NPC that is Idle / Patrolling / Suspicious / Watching scans (throttled) for a live brain fighting the player within `friendlyAssistRange`. Opponents whose tier is Friendly are skipped. It joins with one `friendlyAssistBarks` bubble. A faction-hostile opponent → normal engage; otherwise → aggro target (brawl, knockouts only).
- Friendly grace: a Friendly NPC not in combat that the player hits → `friendlyWarnBarks` bubble, no retaliation, and a grace window opens (`friendlyGraceDuration`). A player hit inside the window → the existing damage reaction (brawl; passive NPCs still never fight back). A hit after the window has expired → a new warning.
- Player knocked out by a Friendly NPC's blow → no looting: a `friendlyDisappointedBarks` bubble, then disengage to the routine.
- Tam (`NPC_BeatenWorker`): override Friendly when `DialogueFact_HiddenStash_FedFood` or `_FedMeal` is set. Tam stays passive, so for him only the warn bubble applies.
- Runtime only (grace and cooldowns are not saved). Missing bark set / channel → silent, with one warning.

**Ask First:** Making any faction other than Tam Friendly, or changing any faction's hostile list.

**Never:** Bystander watching / commenting (later spec), hostile execute (hostile blows are already lethal), individual Hostile overrides, a dialogue-condition fact type for tiers, saving tier state.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tier resolve | faction hostile + override Friendly | Hostile | N/A |
| Tier resolve | not hostile, override Friendly, no condition true | faction tier (Neutral / Friendly) | N/A |
| Tier resolve | not hostile, faction allied either way | Friendly | N/A |
| No player | `TargetRegistry.Player` null | Neutral | N/A |
| Friendly assist | Neutral NPC brawling the player, Friendly guard 10 m away | guard barks, brawls the Neutral | N/A |
| Friendly vs friendly | player brawling Friendly A (after grace), Friendly B nearby | B does not join | N/A |
| Grace | 1st hit on Friendly, 2nd hit 5 s later | bubble, then brawl | N/A |
| Grace expired | 2nd hit 10 s later (window 8 s) | warns again, no brawl | N/A |
| Disappointed | Friendly's blow knocks the player out | disappointed bubble, no gold taken | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- state machine; `RespondToDetectedTarget`, `HandleHealthChanged`, `TryStartLooting`, `RaiseSpeechBubble`, `TryAcquireTargetThrottled`
- `Assets/_Game/Scripts/AI/FactionMember.cs` / `TargetRegistry.cs` -- faction resolution, `TargetRegistry.Player`
- `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs` -- `AreHostile`, `IsAlliedWith`
- `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` -- per-NPC data
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` + `Data/Config/CombatConfig.asset` -- knockout-loot barks pattern
- `Assets/_Game/Data/NPCs/Barks/` -- `BarkSetSO` assets

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/AI/PlayerRelationship.cs` -- new: `enum RelationshipTier`, pure `Resolve(bool hostile, bool allied, PlayerRelationshipOverride ov, bool conditionMet)` + `Of(FactionMember npc)` -- the single readable source
- [x] `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` -- `enum PlayerRelationshipOverride { Faction, Neutral, Friendly }`, `_playerRelationship`, `List<Fact> _relationshipConditions` + getters -- the per-NPC override
- [x] `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- `[Header("Player relationship")]`: `friendlyAssistRange` 12, `friendlyGraceDuration` 8, `hostileSightBarkCooldown` 30, the four `BarkSetSO`s
- [x] `Assets/_Game/Scripts/AI/EntityBrain.cs` -- static `Active` list (OnEnable / OnDisable, reset on SubsystemRegistration); hostile sight bark; friendly assist scan in the non-combat handlers; grace in `HandleHealthChanged` (before the passive early return, only when not in combat); Friendly branch at the top of `TryStartLooting`
- [x] `Assets/_Game/Data/NPCs/Barks/` -- `Barks_HostileSight` ("You there?! Die!"), `Barks_FriendlyAssist` ("A fight! I'm with you!"), `Barks_FriendlyWarn` ("What are you doing?!"), `Barks_FriendlyDisappointed`, 2–3 lines each; wire them in `CombatConfig.asset`
- [x] `Assets/_Game/Data/NPCs/BeatenWorker/NPC_BeatenWorker.asset` -- override Friendly, conditions FedFood + FedMeal
- [x] `Assets/Tests/EditMode/PlayerRelationshipTests.cs` -- `Resolve` matrix rows
- [x] `Assets/_Game/Scripts/AI/CLAUDE.md`, `Data/NPCs/BeatenWorker/CLAUDE.md` -- document the tiers, the hooks and Tam's gate

**Acceptance Criteria:**
- Given a bandit spots the player, when it enters Warning / Engaging, then it shows a hostile bark once per cooldown, and monsters never bark.
- Given Tam was fed, when another system calls `PlayerRelationship.Of(Tam)`, then it returns Friendly; before feeding, Neutral.
- Given EditMode tests run, then all pass, including `PlayerRelationshipTests`.

## Design Notes

The assist scan iterates `EntityBrain.Active` and asks `brain.IsFighting(TargetRegistry.Player)`. This is allocation-free, and it reuses the existing public query instead of keeping a separate opponent set in sync. The assister's aggro target is the opponent brain's own `FactionMember`.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play: hit Tam once when he's fed → warn bubble; hit a neutral worker near a temporarily Friendly-overridden guard → the guard joins.

## Suggested Review Order

**Tier model**

- Entry point: the one query other systems read; hostile is faction-only, overrides gated by facts.
  [`PlayerRelationship.cs:20`](../../Assets/_Game/Scripts/AI/PlayerRelationship.cs#L20)

- Pure rule, matrix-tested.
  [`PlayerRelationship.cs:39`](../../Assets/_Game/Scripts/AI/PlayerRelationship.cs#L39)

- Per-NPC Neutral / Friendly override + any-of fact conditions.
  [`NPCEntity.cs:47`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L47)

**Friendly reactions (EntityBrain)**

- Grace: warn, absorb the rest of the combo, then brawl on a later hit.
  [`EntityBrain.cs:1325`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1325)

- Hook placed before the passive early return, only out of combat.
  [`EntityBrain.cs:1588`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1588)

- Assist: range capped below DisengageRange to avoid an engage / drop loop.
  [`EntityBrain.cs:1347`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1347)

- Opponent filter: fightable, not own / allied faction, never a friendly.
  [`EntityBrain.cs:1378`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1378)

- Assist scan call sites (Idle / Patrol, Suspicious, Watching).
  [`EntityBrain.cs:481`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L481)

- Friendly knockout winner: disappointed, no loot, disengages itself.
  [`EntityBrain.cs:1130`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1130)

**Hostile bark**

- On-sight bark, NPCs only, once per cooldown.
  [`EntityBrain.cs:1311`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1311)

**Peripherals**

- Tunables and bark sets (cooldown renamed with FormerlySerializedAs).
  [`CombatConfigSO.cs:110`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L110)

- Entity accessor needed by the tier query.
  [`FactionMember.cs:110`](../../Assets/_Game/Scripts/AI/FactionMember.cs#L110)

- Tier matrix tests.
  [`PlayerRelationshipTests.cs:1`](../../Assets/Tests/EditMode/PlayerRelationshipTests.cs#L1)
