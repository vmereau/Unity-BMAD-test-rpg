---
title: 'Unconscious state — non-lethal humanoid knockouts, recovery and loot'
type: 'feature'
created: '2026-11-12'
status: 'done'
baseline_commit: '767cdfb34a1c2490710529fbcb7a5f9e2b0f59d8'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every 0-HP hit kills, so the game cannot have Gothic-style brawls with neutral humanoids (a thief caught in the act, arena fights). Also, neutral NPCs never fight back when the player hits them.

**Approach:** When the attacker and the victim are both humanoids (the player or an `NPCEntity`) and their factions are not hostile, a hit that would kill knocks the victim out instead. The victim lies on the ground (knockdown clip held on its last frame), regains health up to 15 % over a few seconds, then gets up. An NPC hit by a non-hostile attacker fights back, with that attacker as its aggro target. An unconscious NPC can be looted like a corpse.

## Boundaries & Constraints

**Always:** Monster hits, hostile-faction hits and hits from an unknown source stay lethal, including on an unconscious target, so a wolf still kills a downed player and ends the game. A knockout never registers a death, a kill or XP. Unconscious counts as incapacitated: no actions, no dialogue, no stance damage, brain paused. All tunables go in `CombatConfigSO` and are shared by the player and NPCs. NPCs that get up return to their routine; they do not re-attack.

**Ask First:** Any change to the `IDamageable` callers beyond the two hit resolvers and the test fakes.

**Never:** Finishing moves (killing a downed humanoid), the winner taking the loser's items, crime or reputation consequences, theft flagging on loot, bystander reactions, saving the unconscious state, passive NPCs (DetectionRange 0) fighting back.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior |
|----------|--------------|---------------------------|
| Neutral brawl | Player hits a neutral NPC to 0 HP | NPC unconscious, HP 0 → 15 % over `unconsciousDuration`, gets up, returns to its routine; it can be looted while down |
| Player loses | Neutral NPC (aggro on the player) brings the player to 0 | Player unconscious (input locked, no death screen); the NPC disengages to its routine; the player gets up with 15 % HP |
| Monster | Wolf hits the player (conscious or unconscious) to 0 | Death + game over, as today |
| Hostile humanoid | Player vs bandit | Lethal both ways (unchanged) |
| Downed, humanoid hit | Non-hostile humanoid hits an unconscious target | Ignored (no damage, no stance) |
| Dead humanoid | Corpse with items | Lootable (unchanged) |
| Load | Save loaded while the player is unconscious | The unconscious state is cleared and the Animator reset; HP comes from the save |

</frozen-after-approval>

## Code Map

- `Scripts/Combat/IDamageable.cs` -- the hit contract; gains an attacker-aware `TakeDamage` and `IsUnconscious`
- `Scripts/Combat/PlayerCombat.cs` `ResolveHit` (melee + arrows) and `Scripts/AI/EntityMeleeAttacker.cs` `HandleHit` -- the two damage call sites
- `Scripts/AI/EntityHealth.cs`, `Scripts/Player/PlayerHealth.cs` -- where HP reaches 0
- `Scripts/AI/FactionMember.cs` -- `IsPlayer`, and the `PersistentID.Entity` that tells whether an entity is humanoid
- `Scripts/AI/EntityBrain.cs` -- `HandleHealthChanged` (fight back), `HasValidTarget`, `Update` pause, `DisengageFromCombat`
- `Scripts/AI/EntityStance.cs`, `Scripts/Player/PlayerStance.cs` -- treat unconscious like dead for stance
- `Scripts/AI/EntityPresence.cs`, `Scripts/AI/NPC/NPCPresence.cs` -- loot gating
- `Scripts/Player/PlayerStateManager.cs` (`IsIncapacitated` gates everything), `PlayerAnimationDriver.cs`, `PlayerSaveAdapter.cs`
- `Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `HumanoidAnimationBridge.cs` -- animator writes
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `KnockedDown → GettingUp`

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Combat/KnockoutRules.cs` (new, pure) -- `IsNonLethal(attackerHumanoid, victimHumanoid, hostile)`, `RecoveryHealth(max, fraction, elapsed, duration)` (linear, clamped) -- shared, testable rules
- [x] `Scripts/Combat/IDamageable.cs` -- add `bool IsUnconscious { get; }` and `void TakeDamage(float amount, GameObject attacker)`; the existing `TakeDamage(float)` stays lethal. Update the fakes in `Tests/EditMode` -- so the victim can tell who hit it
- [x] `PlayerCombat.ResolveHit`, `EntityMeleeAttacker.HandleHit` -- call the attacker overload; skip stance damage when the target `IsUnconscious`
- [x] `Scripts/AI/FactionMember.cs` -- `IsHumanoid => IsPlayer || PersistentID.Entity is NPCEntity`; static `IsKnockoutPair(FactionMember attacker, FactionMember victim)` = both humanoid and not `FactionSO.AreHostile` (either side null → false)
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- `[Header("Unconscious")]` `unconsciousDuration` 8, `unconsciousRecoverHealthFraction` 0.15, `getUpDuration` (= the `getting up` clip length, measured)
- [x] `Scripts/AI/EntityHealth.cs` -- `_combatConfig` field (wire it on `Entity_base.prefab` and on `NPC_base Variant` if it overrides the component). At 0 HP with a knockout pair: `IsUnconscious`, `AIAnimationDriver.SetUnconscious(true)`, HP regains with `RecoveryHealth`, the animator bool is cleared after `unconsciousDuration`, `IsUnconscious` ends after `getUpDuration`. Same-GO events `KnockedOut` / `RegainedConsciousness`. A humanoid hit while down is ignored; a lethal hit → `Die()`. `OnEnable` resets the state
- [x] `Scripts/Player/PlayerHealth.cs` -- same flow: `PlayerCombat.InterruptForStance()`, `PlayerStateManager.SetUnconscious(bool, resetAnimator)`, no `_onPlayerDied`, health event raised every tick. `RestoreHealth` clears the state with `resetAnimator: true`
- [x] `Scripts/Player/PlayerStateManager.cs`, `PlayerAnimationDriver.cs` -- `IsUnconscious` (part of `IsIncapacitated`; clears block, attack, aim, sneak and stance flags); `SetUnconscious` forwards to the driver and the bridge
- [x] `AIAnimationDriver` (virtual no-op), `HumanoidAIAnimationDriver`, `HumanoidAnimationBridge` -- `SetUnconscious(bool)`: sets the `IsUnconscious` bool and on `true` calls `PlayKnockDown()`
- [x] `Humanoid_Template.controller` -- add the `IsUnconscious` bool and a separate `Unconscious` state (same `Knocked Down` clip, non-looping → its last frame holds), entered from AnyState on `KnockDown` + `IsUnconscious` and leaving for `GettingUp` on `IsUnconscious == false` without exit time; the stance AnyState → `KnockedDown` gets `IsUnconscious == false`. Done with the `AnimatorController` API through MCP, not YAML. (Changed in implementation: the existing `KnockedDown → GettingUp` has exit time 0.95, and gating it with a condition is unreliable past the end of a non-looping clip.)
- [x] `EntityStance.cs`, `PlayerStance.cs` -- while unconscious: clear or refill like dead, ignore `Apply`; never call `EndStanceReaction` -- no stance reaction on top of the knockout
- [x] `Scripts/AI/EntityBrain.cs` -- `KnockedOut` → `DisengageFromCombat` logic and a held agent; `Update` paused while `IsUnconscious`; `RegainedConsciousness` → `ResumeNonCombat()`. `HandleHealthChanged`: a live, conscious, known attacker that is not hostile, when this entity is humanoid and has `DetectionRange > 0` → `SetAggroTarget`, `ForceAware` (stealth target), engage. `HasValidTarget` is false when the target `IsUnconscious` and this brain is a knockout pair with it
- [x] `EntityPresence.cs`, `NPCPresence.cs` -- `IsLootable = (IsDead || IsUnconscious) && HasLoot`; `NPCPresence.Interact` loots when dead or unconscious; prompt "Loot"
- [x] `Tests/EditMode/KnockoutRulesTests.cs` -- the lethality truth table and the recovery-curve clamps
- [x] Docs -- `Scripts/AI/CLAUDE.md`, `Scripts/Player/CLAUDE.md`, `Controllers/CLAUDE.md` (new param)

**Acceptance Criteria:**
- Given the player is unconscious, when the input is pressed, then no action, interaction or menu tab opens, and the HUD health bar rises to 15 %.
- Given a downed NPC with items, when the player presses E, then the take-only loot screen opens; after it gets up the prompt is "Talk".
- Given a NPC brawl ends with the player down, then no death screen appears and no save is blocked.

## Design Notes

Who decides: the victim. Only the victim's health knows both factions when the hit lands, and `Game.Combat` stays free of AI types. The pure rule is in Combat; the `FactionMember` adapter is in AI. Known gap: a loot screen left open while the NPC gets up stays open. Closing it automatically needs a new event channel, so it is deferred.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green, `KnockoutRulesTests` included
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play mode in StartingTown: brawl a neutral NPC (knockout, loot, get up, routine), lose a brawl, get killed by a wolf (game over)

## Suggested Review Order

**Who decides: knockout vs death**

- Entry point: the victim reads the attacker and picks knockout or death at 0 HP.
  [`EntityHealth.cs:133`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L133)

- The pair rule: both humanoid, factions not hostile, null side or faction = lethal.
  [`FactionMember.cs:67`](../../Assets/_Game/Scripts/AI/FactionMember.cs#L67)

- Pure rule shared with the tests.
  [`KnockoutRules.cs:13`](../../Assets/_Game/Scripts/Combat/KnockoutRules.cs#L13)

- Attacker-aware damage overload on the hit contract.
  [`IDamageable.cs:17`](../../Assets/_Game/Scripts/Combat/IDamageable.cs#L17)

**Unconscious lifecycle (NPC and player)**

- Linear regen, then get-up clip, then RegainedConsciousness.
  [`EntityHealth.cs:93`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L93)

- Player mirror: interrupt combat, lock input through IsIncapacitated, no death event.
  [`PlayerHealth.cs:157`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L157)

- Single gate: unconscious is part of IsIncapacitated, so every Can* closes.
  [`PlayerStateManager.cs:318`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L318)

- Animator: the separate Unconscious state holds the knockdown's last frame.
  [`HumanoidAnimationBridge.cs:236`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L236)

**Brain: brawl, pause, end of the fight**

- A non-hostile humanoid attacker triggers retaliation with the aggro target.
  [`EntityBrain.cs:1040`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1040)

- A knocked-out opponent is no longer a valid target, so the winner returns to its routine.
  [`EntityBrain.cs:423`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L423)

- Knockout ends every flow; waking resumes the routine, never a re-attack.
  [`EntityBrain.cs:1129`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1129)

- Brain paused while down (no perception or state logic).
  [`EntityBrain.cs:314`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L314)

- Pursuit and trespass skip an unconscious player (no scold dialogue or barks on a downed body).
  [`EntityBrain.cs:861`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L861)

**Loot and stance**

- An unconscious body is lootable like a corpse.
  [`EntityPresence.cs:44`](../../Assets/_Game/Scripts/AI/EntityPresence.cs#L44)

- Stance treats unconscious like dead (refill, ignore hits).
  [`EntityStance.cs:80`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L80)

- Resolvers pass the attacker and skip stance on a downed target.
  [`PlayerCombat.cs:598`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L598)

**Peripherals**

- Shared timings for the player and NPCs.
  [`CombatConfigSO.cs:80`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L80)

- Lethality truth table and recovery-curve clamps.
  [`KnockoutRulesTests.cs:11`](../../Assets/Tests/EditMode/KnockoutRulesTests.cs#L11)
