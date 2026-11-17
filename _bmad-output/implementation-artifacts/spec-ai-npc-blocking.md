---
title: 'NPC blocking — reactive guard, stance budget, rare parry'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '7050755'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `EntityHealth.TryReceiveHit` always returns `NotBlocked`: styled humanoid NPCs (Company guards, bandit) never defend, so every fight is a pure damage race.

**Approach:** A parallel defensive component `EntityGuard`, ticked by the brain while tactics drive the fight. It reads the attacker's wind-up, rolls a per-swing block chance from `CombatStyleSO`, raises the guard (`IsBlocking`) after a reaction delay if the attacker is in its front arc, and resolves hits as Blocked / PerfectBlock. Blocked hits drain stance (the guard budget); a guard break staggers and keeps the guard down until stance regenerates.

## Boundaries & Constraints

**Always:**
- Opt-in by data: `CombatStyleSO.blockChance` 0 (the field default) = never blocks; no style / no tactics = never blocks (monsters).
- Threat = the brain's current target winding up a melee swing, via a new `Game.Combat.IMeleeThreat` (`IsMeleeAttacking`, `IsMeleeCommitted`) on `PlayerCombat` (melee only, not the bow) and `EntityMeleeAttacker` (`IsInSwing` / `IsAttackCommitted`). One roll per swing step (wind-up rising edge: attacking and not committed; both re-open per combo step).
- Guard is never up while: own `IsInSwing`, staggered / knocked down, unconscious, dead, switching weapon, or **guard broken** (stance ≤ stagger threshold). Released through the brain's single release point (`EndTactics`), on disable, and when the threat ends + `guardHoldTime`.
- The brain never starts a swing while the guard is up (`IsBlocking` cancels attack states in `Humanoid_Template`).
- A raised guard blocks only hits whose attacker is in the front arc (`guardArcAngle`, half-angle); others land normally.
- Decision logic pure (`CombatBlockRules` + `GuardPlan`, no Unity objects), randomness injected, EditMode-tested.
- All tunables on `CombatStyleSO` / `CombatConfigSO`; no magic numbers.

**Ask First:** Adding animator states / parameters; changing any existing `Entity` / `NPCEntity` value.

**Never:** Potion use, ranged style, feints, punishing the player's recovery (later specs); a guard against arrows (out of scope — note only); touching `epics.md` / `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Read the swing | Player winds up in front, roll < blockChance | Guard up after reaction delay; hit → `Blocked`, no damage, stance drained × `guardStanceMultiplier` | N/A |
| Too slow | Reaction delay > the swing's hit time | First hit lands; guard up for the rest of the swing | N/A |
| Failed read | Roll ≥ blockChance on a new combo step while guarded | Guard lowered — the hit lands | N/A |
| Parry | Blocked hit, perfect roll < perfectBlockChance | `PerfectBlock`; player stance takes `parriedStanceDamage` (skips attack resistance) | N/A |
| Guard break | Blocked hits cross the stagger threshold | Stagger; no guard until stance regens above the threshold | N/A |
| Flank | Hit from outside the front arc while guarded | `NotBlocked` | N/A |
| Own swing | Threat starts while the NPC swings / staggers | No reaction to that swing step | N/A |
| Unstyled | Spider, wolf, unstyled NPC | Never blocks | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityHealth.cs:211` -- `TryReceiveHit` stub → delegate to `EntityGuard`
- `Scripts/AI/EntityBrain.cs:441` -- `Update` (release point `:491`), `IsAttackReady` `:754`, `EndTactics` `:772`, `HandleAttack` → `ExecuteAttack` `:840`, `DebugStateName` `:78`
- `Scripts/AI/EntityStance.cs` -- `ApplyBlockedStanceDamage` (no multiplier today), threshold from Entity SO
- `Scripts/AI/EntityMeleeAttacker.cs:79` -- `IsInSwing`, `IsAttackCommitted`; `HandleHit` already applies blocked stance / perfect-block stance to itself
- `Scripts/Combat/PlayerCombat.cs:686` -- `ResolveHit` returns on any non-`NotBlocked` without stance; `TryReceiveHit` `:1133` (player's own block, reference)
- `Scripts/Player/PlayerStance.cs` -- `Apply` (attack resistance)
- `Scripts/Core/Animations/AIAnimationDriver.cs`, `HumanoidAIAnimationDriver.cs`, `HumanoidAnimationBridge.cs:107` -- `SetBlocking`
- `Data/Entities/CombatStyles/CombatStyle_CompanyGuard.asset`, `CombatStyle_Bandit.asset`; `Data/Config/CombatConfig.asset`

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Combat/IMeleeThreat.cs` (new) -- interface; implement on `PlayerCombat` (`IsAttacking` && no bow) and `EntityMeleeAttacker` -- attacker-agnostic wind-up read
- [x] `ScriptableObjects/Entities/CombatStyleSO.cs` -- `[Header("Blocking")]` `blockChance` (0), `perfectBlockChance`, `reactionDelayMin/Max`, `guardHoldTime`, `guardArcAngle`, `guardThreatRange`, `guardStanceMultiplier`; clamp max ≥ min -- profile
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- `parriedStanceDamage` (60) -- player stance on an NPC parry
- [x] `Scripts/AI/CombatBlockRules.cs` (new, pure) -- `GuardPlan.Tick(input, dt, blockRoll, delayRoll) → raised` (Down / Reacting / Up / Lingering), `IsInFrontArc`, `ResolveBlock(raised, inArc, perfectRoll, chance) → HitResult`, `IsGuardBroken(current, max, threshold)` -- testable decisions
- [x] `Scripts/AI/EntityGuard.cs` (new) -- `Tick(target)`, `End()`, `IsRaised`, `ResolveHit(attacker)`; caches the target's `IMeleeThreat`; drives `AIAnimationDriver.SetBlocking` on change; info logs on raise / block; `OnDisable → End` -- executes the plan
- [x] `Scripts/AI/EntityHealth.cs` -- `TryReceiveHit` → guard (dead / unconscious / no guard → `NotBlocked`) -- hit gate
- [x] `Scripts/AI/EntityBrain.cs` -- resolve `_guard`; tick it after the state switch while tactics are active; `EndTactics` also ends it; `IsAttackReady` and the `HandleAttack` swing wait while raised; `DebugStateName` `…/Guard` while raised -- integration
- [x] `Scripts/AI/EntityStance.cs` -- blocked stance × `guardStanceMultiplier`; `IsGuardBroken` -- guard budget
- [x] `Scripts/Combat/PlayerCombat.cs` -- `ResolveHit`: `Blocked` → target `ApplyBlockedStanceDamage(stance)`; `PerfectBlock` → own `PlayerStance.ApplyParriedStanceDamage(parriedStanceDamage)` -- block costs the blocker, parry punishes
- [x] `Scripts/Player/PlayerStance.cs` -- `ApplyParriedStanceDamage` (no knockdown, no attack resistance) -- parry
- [x] `Scripts/Core/Animations/AIAnimationDriver.cs` + `HumanoidAIAnimationDriver.cs` -- virtual `SetBlocking` → bridge -- animation
- [x] `Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- add `EntityGuard` on the root -- every humanoid NPC
- [x] Style assets -- guard: block 0.4, perfect 0.08, reaction 0.15–0.4 s, hold 0.35 s, arc 60°, range 3.5 m, stance × 0.7; bandit: block 0.25, perfect 0.03, reaction 0.2–0.45 s, hold 0.25 s, arc 50°, range 3.5 m, stance × 0.9 -- tuning
- [x] `Tests/EditMode/CombatBlockRulesTests.cs` -- every matrix row at rule level + arc + resolve + guard broken -- regression
- [x] `Scripts/AI/CLAUDE.md` (new **Blocking** section, stance notes), `Art/.../Controllers/CLAUDE.md` (NPCs now set `IsBlocking`), `deferred-work.md` (arrow-guard note) -- docs

**Acceptance Criteria:**
- Given a Company guard dueling the player, when the player attacks repeatedly for 30 s, then roughly a third of the hits are blocked (block anim + block sound, logs), parries are rare, and sustained combos break the guard into a stagger followed by a window with no blocking.
- Given a guard holding its guard, when the player stops attacking, then the guard lowers shortly after and the guard can swing again.
- Given a spider or wolf, when it fights, then it never blocks.

## Design Notes

A parallel check rather than a new `CombatAction`: the guard is upper-body only (`Block_State` on `UpperBody`), so the NPC keeps circling / backing off while guarding; movement decisions stay untouched. Per-step rolls plus the stance budget give two openings: a failed read mid-combo, and the guard-broken window (stance ≤ threshold also prevents the floored-hit rule from making a broken guard absorb blocked hits for free). Rough tuning: sword hit ≈ 0.3 s after wind-up, so a 0.15–0.4 s reaction catches the first hit ~70 % of reads → ≈ 30–35 % of hits blocked at 0.4.

## Verification

**Commands:**
- Unity Test Runner EditMode (`mcp__UnityMCP__run_tests`) -- expected: all pass incl. `CombatBlockRulesTests`
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play StartingTown vs the WarehouseGuard and the bandit: block anim + sound, parry stagger on the player, guard break, debug `/Guard`; a spider never blocks. Verify `Block_State`'s AnyState transition fires for an NPC (only the player set `IsBlocking` until now).

## Suggested Review Order

**Guard component (entry point)**

- Per-frame read of the target's swing → pure plan → animator; range + arc gate the read.
  [`EntityGuard.cs:51`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L51)

- Hit outcome: raised guard, attacker in arc and reach — distant arrows never blocked.
  [`EntityGuard.cs:99`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L99)

- Never guard mid-swing, staggered, down, switching weapon, or with a broken guard.
  [`EntityGuard.cs:114`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L114)

- Raised guard re-sent each tick: controller swaps reset animator params.
  [`EntityGuard.cs:83`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L83)

**Pure rules**

- Guard state machine: one roll per swing step on the wind-up edge.
  [`CombatBlockRules.cs:38`](../../Assets/_Game/Scripts/AI/CombatBlockRules.cs#L38)

- Up only while the threat stays active, in front and in range.
  [`CombatBlockRules.cs:67`](../../Assets/_Game/Scripts/AI/CombatBlockRules.cs#L67)

- Block / perfect-block resolution and guard-break threshold (shared StanceMath).
  [`CombatBlockRules.cs:110`](../../Assets/_Game/Scripts/AI/CombatBlockRules.cs#L110)

**Brain integration**

- Guard ticks only while tactics drive Engaging / Attacking.
  [`EntityBrain.cs:502`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L502)

- Single release point: ending tactics drops the guard.
  [`EntityBrain.cs:790`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L790)

- No swing while guarding — IsBlocking would block-cancel the attack state.
  [`EntityBrain.cs:854`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L854)
  [`EntityBrain.cs:771`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L771)

**Hit pipeline and stance budget**

- The IDamageable stub now delegates to the guard.
  [`EntityHealth.cs:216`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L216)

- Blocked stance × style multiplier; guard broken at/below the stagger threshold.
  [`EntityStance.cs:96`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L96)

- Player hits: a block costs the NPC stance, a parry costs the player's.
  [`PlayerCombat.cs:700`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L700)

- Parry stance skips the player's attack hyper-armor.
  [`PlayerStance.cs:90`](../../Assets/_Game/Scripts/Player/PlayerStance.cs#L90)

**Threat interface**

- Player melee swing (never the bow) readable by NPC guards.
  [`PlayerCombat.cs:773`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L773)

- NPC attackers expose IsInSwing / IsAttackCommitted.
  [`EntityMeleeAttacker.cs:84`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L84)

**Peripherals**

- Style tunables and the parry config value.
  [`CombatStyleSO.cs:52`](../../Assets/_Game/ScriptableObjects/Entities/CombatStyleSO.cs#L52)
  [`CombatConfigSO.cs:79`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L79)

- Animation hook (monsters no-op).
  [`AIAnimationDriver.cs:33`](../../Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs#L33)

- Rule tests and folder docs.
  [`CombatBlockRulesTests.cs:1`](../../Assets/Tests/EditMode/CombatBlockRulesTests.cs#L1)
  [`CLAUDE.md:324`](../../Assets/_Game/Scripts/AI/CLAUDE.md#L324)

