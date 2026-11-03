---
title: 'Hostile NPCs — lockable, no interaction prompt'
type: 'feature'
created: '2026-11-03'
status: 'done'
baseline_commit: 'fdae96fd78f288e5ed0013a9f131f973adfc8654'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Hostile humanoids (StartingTown bandits) cannot be locked on — `LockOnSystem` only overlaps Layer 6 (Characters), where humanoid NPCs have no collider (only spiders do). And an alive bandit out of combat still shows the `[E] Talk` prompt and opens dialogue.

**Approach:** Lock-on also overlaps the CharacterHitbox layer (7) that every entity's hurtbox uses, and only accepts alive entities whose faction is hostile to the player's. `NPCPresence` offers no prompt and no interaction while alive and hostile to the player; corpse looting is unchanged. Hostility = either faction lists the other as hostile.

## Boundaries & Constraints

**Always:** Hostility comes from `FactionSO` lists only (no per-instance aggro). One shared pure helper decides "hostile" for both systems. Lock-on keeps one candidate per entity (EntityHealth root), cone/range rules unchanged. Dead hostile NPC with loot → `Loot` prompt still works. Name/HP hover UI unchanged.

**Ask First:** Changing any entity prefab's layers or colliders (not needed by this approach).

**Never:** Neutral / friendly NPCs become lockable. Removing the dialogue graph or memories of hostile NPCs. Per-instance hostility overrides or reputation systems.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Lock bandit | Bandit alive in cone + range | Locked on the bandit root | N/A |
| Lock neutral | Only a Villager/Guard in cone | No target | Info "No valid targets in cone" (existing) |
| Lock spider | Spider in cone | Locked (Monsters hostile to Player) | N/A |
| Player hurtbox | Own Hitbox overlaps (Layer 7) | Ignored (no EntityHealth) | N/A |
| No faction on target | EntityHealth without FactionMember | Not lockable | N/A |
| Hostile NPC prompt | Alive bandit, out of combat, focused | No prompt, `E` does nothing | N/A |
| Hostile NPC corpse | Dead bandit with loot | `Loot` prompt, opens loot | N/A |
| Neutral NPC | Alive villager out of combat | `Talk` prompt, dialogue opens (unchanged) | N/A |
| Missing player faction | Player has no FactionMember / NPC `_playerFaction` unset | Lock-on: no hostility filter possible → no target; NPC: treated non-hostile | One `GameLog.Warn` in Awake |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs` -- `IsHostileTo`; home of the shared static helper
- `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- overlap + candidate filter (`TryAcquireTarget`)
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `LockOnSystem._lockOnLayerMask` m_Bits 64; Player root `FactionMember` override `Faction_Player`
- `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- `InteractPrompt` / `CanInteract` / `Interact`
- `Assets/_Game/Scripts/AI/FactionMember.cs` -- `Faction` (on every entity root)
- `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- wire the new `NPCPresence._playerFaction`
- `Assets/_Game/Scripts/World/InteractionSystem.cs` -- already skips `!CanInteract` for prompt and `E` (no change)
- `Assets/Tests/EditMode/FactionTests.cs`, `LockOnTests.cs` -- existing test homes

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs` -- add `public static bool AreHostile(FactionSO a, FactionSO b)` (null-safe, either direction) -- single hostility rule
- [x] `Assets/_Game/Scripts/Player/LockOnSystem.cs` -- cache the player's `FactionMember` component in Awake (warn if missing) and read `.Faction` at query time — FactionMember resolves it in its own Awake, order not guaranteed; in `TryAcquireTarget` skip candidates whose `GetComponentInParent<FactionMember>()` is null or not `AreHostile` with the player -- hostile-only targets
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` -- `_lockOnLayerMask` = Characters + CharacterHitbox (m_Bits 192) -- humanoids become detectable via their hurtbox
- [x] `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- add `[SerializeField] FactionSO _playerFaction`; resolve own faction from root `FactionMember`; `IsHostileToPlayer` → alive: prompt empty, `CanInteract` false, `Interact` returns with an info log; dead path unchanged -- no dialogue with hostiles
- [x] `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- set `NPCPresence._playerFaction` = `Data/Factions/Faction_Player.asset`
- [x] `Assets/Tests/EditMode/FactionTests.cs` -- `AreHostile`: one-way list, both ways, none, null args
- [x] Docs -- `Prefabs/Player/CLAUDE.md` (Hitbox rule: lock-on now scans 6 + 7, own hurtbox ignored), `Prefabs/Entities/CLAUDE.md` (lock-on via hurtbox, hostile-only), `Scripts/AI/CLAUDE.md` (`NPCPresence` hostile gating)

**Acceptance Criteria:**
- Given the StartingTown bandit in front of the player within lock-on range, when the player presses Lock On, then the camera locks onto the bandit.
- Given a villager and a bandit both in the cone, when the player presses Lock On, then the bandit is chosen even if the villager is closer.
- Given the player looks at an alive bandit out of combat, when in interaction range, then no `[E]` prompt shows and pressing E opens nothing; after killing it, the `Loot` prompt appears.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all pass incl. new `AreHostile` tests
- Unity MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- Play mode in StartingTown: lock on a bandit; villager not lockable; bandit shows no prompt; dead bandit lootable.

## Suggested Review Order

**Hostility rule**

- Single symmetric, null-safe rule shared by lock-on and NPC gating.
  [`FactionSO.cs:24`](../../Assets/_Game/ScriptableObjects/Factions/FactionSO.cs#L24)

**Lock-on**

- Hostile-only candidate filter, inside the one-candidate-per-entity loop.
  [`LockOnSystem.cs:107`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L107)

- Player faction read at query time — FactionMember resolves it in its own Awake.
  [`LockOnSystem.cs:127`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L127)

- Mask now Characters + CharacterHitbox (192): humanoids found via their hurtbox.
  [`LockOnSystem.cs:87`](../../Assets/_Game/Scripts/Player/LockOnSystem.cs#L87)

**NPC interaction**

- Alive & hostile → no prompt, no `E`; corpse loot path untouched.
  [`NPCPresence.cs:31`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L31)

- Defensive block in `Interact` with an info log.
  [`NPCPresence.cs:50`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L50)

**Peripherals**

- `AreHostile` tests: one-way, both, none, nulls.
  [`FactionTests.cs:76`](../../Assets/Tests/EditMode/FactionTests.cs#L76)
