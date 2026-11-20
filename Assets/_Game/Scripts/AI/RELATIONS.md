# Relations — player relationship tiers, mutual defense

> Read when touching `PlayerRelationship`, `FactionSO.Defends` / `AidGroupSO`, or the `EntityBrain` join-a-fight scans
> (`TryJoinNearbyFight` → `TryAssistPlayer` / `TryDefendMember`), friendly grace and relationship barks.
> Specs: `spec-player-relationship-tiers`, `spec-faction-mutual-defense`.

---

## Player relationship

- **Tier** = `PlayerRelationship.Of(FactionMember)` — the one query other systems (dialogue, …) read; live, never cache it.
  Hostile = `FactionSO.AreHostile` with the player's faction (**faction-only**). Otherwise an active `NPCEntity` override
  (`_playerRelationship` Neutral / Friendly, active while **any** `_relationshipConditions` fact is true; empty = always; no
  `WorldStateManager` = inactive) wins; otherwise Friendly when either faction lists the other in `_alliedFactions`, else
  Neutral. No player → Neutral. Only Tam has an override today (Friendly once fed — `Data/NPCs/BeatenWorker/CLAUDE.md`).
- Tunables + barks: `CombatConfigSO` "Player relationship" (`friendlyAssistRange` 12, `friendlyGraceDuration` 8,
  `friendlyWarnLockout` 2, `relationshipBarkCooldown` 30, `Barks_HostileSight` /
  `_FriendlyAssist` / `_FriendlyWarn` / `_FriendlyDisappointed`), read through `EntityHealth.CombatConfig`. No config →
  none of the reactions below. Bubbles use the theft bubble duration / priority; each set keeps its own no-repeat index;
  missing line / channel → silent, one warning (`RaiseRelationshipBark`).
- **Hostile:** `RespondToDetectedTarget` on the player → one `hostileSightBarks` bubble per cooldown (NPCs only, never
  monsters). Hit-triggered engages don't bark.
- **Friendly assist:** `TryAssistPlayer` (throttled by `_targetScanInterval`) runs from Idle / Patrolling (end of
  `TryDetectFromNonCombat`), Suspicious and Watching. Non-passive, has an attacker, player live and conscious → closest
  brain in the static `EntityBrain._active` list (OnEnable / OnDisable, reset on `SubsystemRegistration`) with
  `IsFighting(player)` that it **can fight**: live, conscious, not its own / an allied faction, faction-hostile (one-way
  `IsHostileTo`, same rule as the melee filter) or a knockout pair, and **not Friendly** to the player (friendlies never
  join against a friendly). Range = `min(friendlyAssistRange, 0.9 × DisengageRange)` — at 12 / 12 it would engage and
  drop the fight at once, then re-assist every scan. Faction-hostile → plain engage; knockout pair → aggro target
  (brawl). `friendlyAssistBarks` once per cooldown, perception reset.
- **Friendly grace:** `HandleHealthChanged`, before the passive early return: the player hits a Friendly NPC **not in
  combat** → `TryFriendlyWarn` — outside the window: `friendlyWarnBarks` bubble + window opens, no reaction; within
  `friendlyWarnLockout` of the warning: absorbed (the rest of the same combo must not start the brawl); later inside the
  window: window closes, the normal damage reaction runs (brawl). A passive friendly (Tam) keeps the window open — one
  bubble per window, never fights back. Grace resets in `OnEnable`; runtime only.
- **Friendly knockout winner:** `TryStartLooting` → Friendly → `friendlyDisappointedBarks` bubble, no loot,
  `DisengageFromCombat` there (returns true, so the caller doesn't log "target lost").
- Out of scope / later: bystanders watching / commenting a fight, hostile execute (hostile blows already kill), individual
  Hostile overrides, a tier dialogue condition.

---

## Mutual defense

- **Rule** = pure `FactionSO.Defends(self, selfGroup, other, otherGroup)` (`FactionTests`): a shared non-null
  `AidGroupSO` (`Entity._aidGroup`, faction-independent — e.g. a hidden crew who are workers on paper), **or** the
  defender's faction has `_membersDefendEachOther` and the other is the same faction or allied (either direction). The
  **defender's** flag decides. Flags: Company / Bandits / Monsters on, Neutral (workers) / Player off.
- **One faction per entity** stays the rule — sub-groups go through `AidGroupSO`, never multi-faction membership.
- **Scan:** `TryJoinNearbyFight` = `TryAssistPlayer() || TryDefendMember()` at the same three call sites (Idle /
  Patrolling, Suspicious, Watching); each has its own throttle timer. `TryDefendMember` → non-passive → closest
  opponent of an `_active` brain in **Engaging / Attacking** (a Warning also sets `IsInCombat` — the warned target keeps
  its grace) with a live, conscious `_currentTarget` (**whoever started the fight** — a trespass brawl counts) and is
  defended. A defender without a faction still defends its aid group (brawl only). First scan staggered in `OnEnable`. Range measured to the **opponent**:
  `min(memberDefendRange, 0.9 × DisengageRange)`.
- **Skips:** an opponent the defender also defends; the player when the defender is Friendly to the player (it stays out
  of a fight between its colleague and the player — the player is never on the defended side, that's the friendly
  assist's job); opponents neither faction-hostile nor a knockout pair.
- **Join:** perception reset (+ `ForceAware` for a stealth opponent), hostile → plain engage, else aggro target (brawl);
  fists when the defended colleague's brawl is a fistfight (`NpcArmament.FistsRequested`).
  `memberDefendBarks` (`Barks_MemberDefend`) once per `relationshipBarkCooldown`, `NPCEntity` only — monsters stay silent.
- Runtime only; no event-based call for help.
