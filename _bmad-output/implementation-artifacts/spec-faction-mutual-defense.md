---
title: 'Faction mutual defense + aid groups'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '57cc22e'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** NPCs never help each other. Brawl a guard and the other guards stand by. Workers should behave that way, but guards, bandits and monster packs should not, and there is no way to mark a hidden crew (for example mafia members who are workers on paper) whose members back each other up.

**Approach:** Add a per-faction flag `FactionSO._membersDefendEachOther`, plus an optional per-entity `AidGroupSO` (sub-group, faction-independent). A free, non-passive NPC that sees someone it defends fighting anyone nearby joins the fight against that opponent. One faction per NPC stays the rule.

## Boundaries & Constraints

**Always:**
- Defend rule (pure, tested): A defends B when they share a non-null aid group, OR A's faction has the flag and B's faction is A's faction or allied with it (either direction).
- The player is never someone an NPC defends; helping the player stays the friendly assist's job.
- Trigger: B is in combat with a live, conscious opponent, whoever started it. The defender is non-passive (DetectionRange > 0), in Idle / Patrolling / Suspicious / Watching (the same call sites as `TryAssistPlayer`), and the opponent is within `memberDefendRange`, capped like the assist at `DisengageRange * ASSIST_DISENGAGE_MARGIN`. The scan is throttled and allocation-free over `EntityBrain.Active`.
- Skip an opponent the defender itself defends. Skip the player as opponent when the defender is Friendly to the player (it stays out of a fight between its colleague and the player).
- Opponent faction-hostile to the defender → normal engage. Non-hostile knockout pair → brawl (aggro target). Otherwise skip.
- On joining, an `NPCEntity` raises one `memberDefendBarks` bubble, subject to `relationshipBarkCooldown`. Monsters never bark.
- Flags: Company, Bandits and Monsters `true`; Neutral and Player `false`.

**Ask First:** Creating any aid-group asset or assigning one to an NPC.

**Never:** Multiple factions per NPC, saving defend state, calling for help by event (scan only), changing hostile lists.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Rule | same faction, flag on | defends | N/A |
| Rule | same faction, flag off (workers) | no | N/A |
| Rule | allied faction, own flag on | defends | N/A |
| Rule | flag off, same aid group | defends | N/A |
| Rule | different groups, flag off | no | N/A |
| Rule | null factions / groups | no (unless the group matches) | N/A |
| Guards | player brawls guard A, guard B 8 m away idle | B barks, brawls the player | N/A |
| Workers | player brawls worker, worker 5 m away | no reaction | N/A |
| Friendly colleague | Friendly guard, colleague brawling player | stays out | N/A |
| Hostile | bandit fights guard A, guard B near | B engages the bandit (lethal) | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs` -- hostile / allied lists, `InitForTest`
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- base entity SO (NPCs + monsters), holds the faction
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `TryAssistPlayer` / `FindPlayerOpponent` (L1347–1400) is the template; call sites L481, L593, L802; `IsFighting`, `_active`, `RaiseRelationshipBark`
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` + `Data/Config/CombatConfig.asset` -- relationship tunables and barks
- `Assets/_Game/Data/Factions/*.asset`, `Data/NPCs/Barks/`

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Factions/AidGroupSO.cs` -- new SO in its own file (`Game.Factions`, `CreateAssetMenu "Game/Aid Group"`), `displayName` only -- the sub-group marker
- [x] `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs` -- `_membersDefendEachOther` + getter; static pure `Defends(FactionSO self, AidGroupSO selfGroup, FactionSO other, AidGroupSO otherGroup)`; extend `InitForTest` with an optional flag; update the allied-list tooltip (now also used for defense)
- [x] `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- optional `_aidGroup` under the Faction header + getter
- [x] `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- `memberDefendRange` 12, `memberDefendBarks`
- [x] `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `TryDefendMember()` + `FindMemberOpponent(...)`, called right after `TryAssistPlayer` at its three call sites, with its own throttle timer; join via the same engage / brawl path the assist uses; bark only for `NPCEntity`
- [x] `Assets/_Game/Data/NPCs/Barks/Barks_MemberDefend.asset` -- 2–3 lines ("Hey! Leave him alone!"); wire in `CombatConfig.asset`
- [x] `Assets/_Game/Data/Factions/` -- set the flag on Company, Bandits and Monsters
- [x] `Assets/Tests/EditMode/FactionTests.cs` -- `Defends` rule rows from the matrix
- [x] `Assets/_Game/Scripts/AI/CLAUDE.md` -- document mutual defense, aid groups, and the Friendly-colleague rule

**Acceptance Criteria:**
- Given the edited assets, when Unity compiles, then the console shows no errors and the four faction assets keep their hostile lists.
- Given EditMode tests run, then all pass, including the new `Defends` rows.

## Design Notes

Scan, not event: "fighting anyone" includes fights the member started (a trespass brawl), which a hit event would miss, and it mirrors `TryAssistPlayer`. The range is measured to the opponent, where the defender will go, so a joined fight isn't dropped at once. Chained joins only pull in NPCs near that opponent.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play: brawl a Company guard near another guard → the second joins with a bark; brawl a worker near workers → nobody joins.

## Suggested Review Order

**Defend rule**

- Entry point: the pure rule (shared aid group, or flagged faction + same / allied); the defender's flag decides.
  [`FactionSO.cs:32`](../../Assets/_Game/ScriptableObjects/Factions/FactionSO.cs#L32)

- Per-faction flag (Company / Bandits / Monsters on, workers off).
  [`FactionSO.cs:21`](../../Assets/_Game/ScriptableObjects/Factions/FactionSO.cs#L21)

- Faction-independent sub-group, set on the Entity SO (NPCs and monsters).
  [`Entity.cs:21`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L21)

**Join scan (EntityBrain)**

- Opponent search: only real fights (Engaging / Attacking), range measured to the opponent.
  [`EntityBrain.cs:1422`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1422)

- A Warning sets IsInCombat too — excluded so a warned player keeps the grace.
  [`EntityBrain.cs:1436`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1436)

- Friendly-to-player defender stays out of a colleague-vs-player fight.
  [`EntityBrain.cs:1454`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1454)

- Join: engage or brawl, matching the colleague's fistfight.
  [`EntityBrain.cs:1387`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1387)

- Bark: NPCs only, per-NPC cooldown.
  [`EntityBrain.cs:1409`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1409)

- Assist first, then defend — wired at all three former assist call sites.
  [`EntityBrain.cs:1383`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1383)

- First scan staggered across brains.
  [`EntityBrain.cs:310`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L310)

**Peripherals**

- Fists accessor read by the defender.
  [`NpcArmament.cs:185`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L185)

- Range + bark set tunables.
  [`CombatConfigSO.cs:133`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L133)

- Faction flag data.
  [`Faction_Company.asset:20`](../../Assets/_Game/Data/Factions/Faction_Company.asset#L20)

- Rule matrix tests.
  [`FactionTests.cs:123`](../../Assets/Tests/EditMode/FactionTests.cs#L123)
