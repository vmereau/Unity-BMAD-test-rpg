---
title: 'Lock-on accepts non-hostile NPCs fighting the player'
type: 'bugfix'
created: '2026-11-18'
status: 'done'
baseline_commit: 'b94224c4'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `LockOnSystem` only accepts entities whose faction is hostile to the player's. A neutral NPC that enters a fight with the player (brawl, trespass escalation, toll fight, defending a member, being attacked by the player) cannot be locked on, while faction-hostile bandits can. `Scripts/AI/CLAUDE.md` records this as a known gap.

**Approach:** A candidate is lockable when its faction is hostile to the player **or** its `EntityBrain` is currently fighting the player (`EntityBrain.IsFighting(playerMember)`). The rule is extracted as a pure static method so it can be unit-tested.

## Boundaries & Constraints

**Always:** Keep one candidate per `EntityHealth`, skip dead entities, and keep the cone, range and nearest-pick logic unchanged. Read the player's `FactionMember` at query time, as today.

**Ask First:** Making a neutral NPC lockable when it is *not* fighting the player (for example, to start a fight).

**Never:** Change AI combat / aggro logic (read-only queries on `EntityBrain` are fine). Never break an existing lock merely because the NPC leaves combat. Break conditions are the existing ones (death, range, swimming, inactive) plus the target or the player being unconscious (human decision [U], review loop 1).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Hostile faction | Bandit in cone, not in combat | Lockable (unchanged) | N/A |
| Neutral fighting player | Neutral NPC, brain `IsFighting(player)` | Lockable | N/A |
| Opening hit staggered it | Neutral NPC staggered / knocked down by the player's first hit, engages after recovery | Lockable | N/A |
| Locked target knocked out | Locked neutral NPC goes unconscious | Lock cleared | N/A |
| Player knocked out | Player unconscious while locked | Lock cleared | N/A |
| Neutral idle | Neutral NPC, not in combat | Not lockable | N/A |
| Neutral fighting someone else | Neutral NPC fighting another NPC | Not lockable | N/A |
| No brain | Non-hostile entity without `EntityBrain` | Not lockable | null-safe |
| Player has no FactionMember | — | Nothing lockable (unchanged) | existing warning |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- `IsHostileTarget` filter used by `TryAcquireTarget`
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- public `IsFighting(FactionMember)` = `IsInCombat && _currentTarget == member`
- `Assets/Tests/EditMode/**/LockOnTests.cs` -- existing pure tests for the cone selection
- `Assets/_Game/Scripts/Player/CLAUDE.md`, `Assets/_Game/Scripts/AI/CLAUDE.md` -- lock-on docs + "known gap" note

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- replace `IsHostileTarget` with `IsLockableTarget(health)`, which resolves the target `FactionMember` and `EntityBrain` and calls a public static `IsLockable(bool factionHostile, bool fightingPlayer) => factionHostile || fightingPlayer`. Return false when the player member is null. -- adds the fighting-player case
- [x] `Assets/_Game/Scripts/AI/EntityBrain.cs` -- add read-only `IsFightingOrEngaging(FactionMember)` = `IsFighting(member) || (_engageAfterRecovery && _currentTarget == member)`; `LockOnSystem` uses it -- lock the NPC staggered by the opening hit
- [x] `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- `Update` clears the lock when the target `EntityHealth.IsUnconscious` or `PlayerStateManager.IsUnconscious` -- no lock on a knocked-out body / while knocked out
- [x] `Assets/Tests/EditMode/**/LockOnTests.cs` -- add cases for `IsLockable` (the four true/false combinations) -- covers the matrix rule
- [x] `Assets/_Game/Scripts/Player/CLAUDE.md` -- add the lockability rule to the LockOnSystem section; `Assets/_Game/Scripts/AI/CLAUDE.md` -- remove the "known gap" sentence -- keeps the docs accurate

**Acceptance Criteria:**
- Given a neutral NPC that has started a brawl with the player, when the player presses Lock On while aiming at it, then the lock acquires it.
- Given a neutral NPC fighting another NPC near the player, when the player presses Lock On, then that NPC is not selected.

## Verification

**Commands:**
- Unity EditMode tests `LockOnTests` -- expected: all pass
- `read_console` errors after compile -- expected: none

**Manual checks (if no CLI):**
- In play mode, provoke a neutral NPC (hit it), then press middle mouse: the lock takes it.

## Suggested Review Order

**Who can be locked**

- Entry point: faction-hostile short-circuit, else ask the brain if it fights the player.
  [`LockOnSystem.cs:132`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L132)

- Read-only query also covering a pending engage after a stagger from the opening hit.
  [`EntityBrain.cs:72`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L72)

- Pure rule extracted for EditMode tests.
  [`LockOnSystem.cs:146`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L146)

**When the lock breaks**

- Knocked-out target releases the lock (neutral fights end in knockouts, not deaths).
  [`LockOnSystem.cs:199`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L199)

- Knocked-out player releases the lock.
  [`LockOnSystem.cs:205`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L205)

**Peripherals**

- Truth-table tests for the lockability rule.
  [`LockOnTests.cs:135`](../../Assets/Tests/EditMode/LockOnTests.cs#L135)

- Docs: lockability + break conditions.
  [`CLAUDE.md:236`](../../Assets/_Game/Scripts/Player/CLAUDE.md#L236)
