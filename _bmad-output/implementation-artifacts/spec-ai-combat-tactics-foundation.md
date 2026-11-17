---
title: 'AI combat tactics foundation — combat styles, circling, attack tokens'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '6786a1d'
context:
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In combat an NPC only runs straight at its target and swings whenever the cooldown allows; several guards pile in and swing at once. It reads as mindless and leaves no room for the planned blocking, potion and ranged behaviours.

**Approach:** Add a data-driven tactics layer: a `CombatStyleSO` profile on the `Entity` SO and an `EntityCombatTactics` component that, while the brain is Engaging / Attacking, periodically picks one action — **Approach**, **Circle** (strafe around the target at a ring distance, facing it), **Attack** (close in and swing, only while holding an attack token), **BackOff** (step out when too close). A per-target attack-token registry caps simultaneous attackers; the others circle and wait. Company guards ("competent but beatable") and the bandit get styles; entities without a style keep today's behaviour.

## Boundaries & Constraints

**Always:**
- `EntityBrain` keeps its states; the tactics component only drives movement / facing and grants attacks inside Engaging / Attacking. No style (or no component) → today's chase-and-swing, unchanged (monsters).
- Decision rules are pure (`CombatTacticsRules`, no Unity objects) and EditMode-tested; randomness is injected (a `[0,1)` roll parameter).
- Ring radius = `Entity.AttackRange + style.ringMargin` (follows the weapon / future bow range).
- While circling / backing off: `NavMeshAgent.updateRotation = false` and the entity faces the target at `WarningTurnSpeed`; restored to `true` whenever tactics stop (leaving Engaging / Attacking, death, knockout, stagger, disable). Strafe animation comes free from `DriveLocomotion` (local `VelocityX/Z` → `LockOn Locomotion`).
- Circle destinations are `NavMesh.SamplePosition`-validated; no valid point → flip direction once, else Approach.
- Tokens: one registry keyed by target, capacity `CombatConfigSO.maxAttackersPerTarget` (default 2). Taken when Attack is chosen, released when the swing ends, on target change, leaving combat, death, knockout, stance reaction, disable. Never leaks (static reset on `SubsystemRegistration`).
- The mid-swing rooting / commit rules (spec-ai-attack-root-and-step) are untouched: tactics never move an entity while `IsInSwing`.
- All tunables live on the SO; no magic numbers in code.

**Ask First:** Changing any existing `Entity` / `NPCEntity` value other than assigning `_combatStyle`; adding animator states / parameters.

**Never:** Blocking, potion use, ranged / weapon-choice actions, feints or recovery punishing (later specs, see `deferred-work.md`); behaviour-tree / GOAP packages; touching `epics.md` / `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Duel | 1 guard vs player, token free | Approaches to the ring, circles `circleTime` (random in range), then closes in and attacks; after the swing returns to circling | N/A |
| Group | 4 guards vs player, capacity 2 | At most 2 in Attack at once; others circle at the ring until a token frees | N/A |
| Too close | Player walks into a circling guard (dist < `AttackRange × backOffFraction`) | BackOff (or Attack if it holds a token and its roll passes) | N/A |
| Far | dist > ring + `approachMargin` | Approach at `EngageSpeed`, `updateRotation` true | N/A |
| Blocked strafe | Circle point off the NavMesh | Flips direction; both fail → Approach | N/A |
| Interrupted | Stagger / knockout / death / disengage while holding a token | Token released, `updateRotation` true | N/A |
| No style | Spider, wolf, unstyled NPC | Exactly today's behaviour | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityBrain.cs:662` -- `HandleEngage` (straight `SetDestination`, Attacking at `AttackRange`); `:693` `HandleAttack` (stands until cooldown); `:2274` `TransitionToEngaging`, `:2286` `TransitionToAttacking`; `SetCombatState` `:80`; `_active` list `:231`; `FaceTarget` `:650`
- `Scripts/AI/EntityMeleeAttacker.cs` -- `IsInSwing`, `IsInAttackState`, `EndAttack` (token release point)
- `Scripts/AI/EntityStance.cs`, `Scripts/AI/EntityHealth.cs` -- stance reaction / knockout / death (release points)
- `ScriptableObjects/Entities/Entity.cs` -- `AttackRange`, `EngageSpeed`, `BaseSpeed`, `WarningTurnSpeed`
- `ScriptableObjects/Config/CombatConfigSO.cs` + `Data/Config/CombatConfig.asset` -- global combat tunables
- `Scripts/Core/Animations/HumanoidAIAnimationDriver.cs:86` -- local-velocity locomotion (strafe already works)
- `Prefabs/Entities/NPC_base Variant.prefab` -- humanoid NPC root
- `Data/NPCs/*/` -- `NPCEntity` assets of the six Company guards and the bandit

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Entities/CombatStyleSO.cs` (new, `Game.AI`) -- `decisionInterval`, `ringMargin`, `approachMargin`, `circleSpeed`, `circleTimeMin/Max`, `directionChangeChance`, `aggression` (0..1 attack weight once circle time elapsed), `backOffFraction`, `backOffSpeed`; `OnValidate` clamps -- the profile
- [x] `ScriptableObjects/Entities/Entity.cs` -- `[Header("Combat AI")] _combatStyle` + `CombatStyle` getter -- per-entity profile, null = legacy
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- `maxAttackersPerTarget` (2) -- token capacity
- [x] `Scripts/AI/CombatTacticsRules.cs` (new, pure) -- `Decide(distance, attackRange, style, circleElapsed, circleTarget, hasToken, canTakeToken, roll) → CombatAction` -- testable decision
- [x] `Scripts/AI/AttackTokenRegistry.cs` (new, static) -- `TryTake(target, holder, capacity)`, `Release(holder)`, `Holds`, reset on `SubsystemRegistration` -- attacker cap
- [x] `Scripts/AI/EntityCombatTactics.cs` (new) -- `Begin(target)` / `End()`, `Tick()` at `decisionInterval` → agent destination / speed / `updateRotation` + facing; `WantsAttack` when holding a token within `AttackRange`; `NotifySwingEnded()`; `CurrentAction` for debug; `OnDisable` → `End()` -- executes decisions
- [x] `Scripts/AI/EntityBrain.cs` -- if a style + component exist: `HandleEngage` delegates movement to tactics and enters Attacking only on `WantsAttack`; `HandleAttack` after the swing (not in swing, `ExecuteAttack` done) → `NotifySwingEnded` + back to Engaging; call `End()` from `SetCombatState(false)`, `TransitionToSearching`, `TransitionToDead`, knockout and stance-reaction handlers, target change; `DebugStateName` appends `/<action>` -- integration
- [x] `Prefabs/Entities/Humanoids/NPC_base Variant.prefab` -- add `EntityCombatTactics` on the root -- every humanoid NPC
- [x] `Data/Entities/CombatStyles/CombatStyle_CompanyGuard.asset`, `CombatStyle_Bandit.asset` (new) -- guard: patient (circle 1.5–3 s, aggression ~0.5); bandit: pushier (circle 0.6–1.5 s, aggression ~0.8); assign to the six Company guards' and the bandit's `NPCEntity` -- first users
- [x] `Tests/EditMode/CombatTacticsRulesTests.cs`, `AttackTokenRegistryTests.cs` -- I/O matrix decisions, capacity, release, reset -- regression
- [x] `Scripts/AI/CLAUDE.md` -- new **Combat tactics** section (+ note that `updateRotation` is now disabled during circling) -- docs

**Acceptance Criteria:**
- Given a Company guard fighting the player in open ground, when observed for 15 s, then it visibly strafes around the player facing them (strafe animation, no moonwalk) between attacks, and its swings still root and step as before.
- Given four hostile styled NPCs on the player, when they reach the ring, then no more than two are in Attacking at any moment (verified with the debug state name / logs).
- Given a spider or wolf, when it fights, then its behaviour is identical to the baseline.
- Given any combat exit (disengage, search, death, knockout, routine return), then the NPC walks its routine with the agent turning along its path (`updateRotation` restored) and holds no token.

## Spec Change Log

## Design Notes

Utility-style selection kept deliberately tiny: one pure `Decide` called every `decisionInterval` (0.3–0.5 s), not per frame, so motion doesn't jitter. Order: too far → Approach; too close and no attack → BackOff; holding a token → Attack; circle time elapsed and `roll < aggression` and a token is available → take it, Attack; else Circle (flip direction on `roll < directionChangeChance`). Later specs add actions (Block, DrinkPotion, Shoot) as new `CombatAction` values with their own conditions in `Decide` and new `CombatStyleSO` weights — the shape that makes skill- and loadout-dependent AI a data change.

Circle point: rotate `(self − target)` flattened by `± circleSpeed × decisionInterval / ringRadius` radians around the target, set its length to the ring radius. The point leads by 2 decision intervals so the agent never arrives and stops between decisions.

Implementation addition: tactics are skipped against a **passive target** (`DetectionRange <= 0`) — the Warden's scripted fist beating of Tam (arrival sequence) keeps the plain chase-and-swing; circling a victim that never fights back reads wrong.

## Verification

**Commands:**
- Unity Test Runner EditMode (`mcp__UnityMCP__run_tests`, mode EditMode) -- expected: all pass, incl. the new tests
- `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play StartingTown, aggro the WarehouseGuard (or the bandit): circling, facing, strafe blend, attack cadence; then a brawl with 2+ Company guards for the token cap; a spider fight for unchanged behaviour.

## Suggested Review Order

**Brain ↔ tactics handoff**

- Entry point: styled fighters tick tactics with the brain's 3D distance; swing only with a token.
  [`EntityBrain.cs:709`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L709)

- After a tactical swing: token back, Engaging resumes with plain agent settings.
  [`EntityBrain.cs:797`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L797)

- No token in Attacking (stagger recovery, retarget) → back to Engaging, keeps the cap.
  [`EntityBrain.cs:807`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L807)

- Single release point: any state outside Engaging / Attacking ends tactics.
  [`EntityBrain.cs:490`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L490)

- Passive targets (Tam) keep the plain chase-and-swing — implementation addition.
  [`EntityBrain.cs:742`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L742)

**Tactics component**

- Per-frame tick: decisions at the style interval, commit timeout frees stuck tokens.
  [`EntityCombatTactics.cs:76`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L76)

- Movement per action: Approach rotates with the path, others strafe facing the target.
  [`EntityCombatTactics.cs:185`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L185)

- Idempotent end: token released, agent rotation restored.
  [`EntityCombatTactics.cs:126`](../../Assets/_Game/Scripts/AI/EntityCombatTactics.cs#L126)

**Pure rules**

- Decision priority: token → approach → aggression roll → back off → circle.
  [`CombatTacticsRules.cs:31`](../../Assets/_Game/Scripts/AI/CombatTacticsRules.cs#L31)

- Token cap per target, one token per holder.
  [`AttackTokenRegistry.cs:35`](../../Assets/_Game/Scripts/AI/AttackTokenRegistry.cs#L35)

**Data and config**

- Profile with every tunable (spacing, circling, aggression, commit timeout).
  [`CombatStyleSO.cs:12`](../../Assets/_Game/ScriptableObjects/Entities/CombatStyleSO.cs#L12)

- Opt-in per entity; null keeps monsters unchanged.
  [`Entity.cs:114`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L114)

- Global attacker cap.
  [`CombatConfigSO.cs:142`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L142)

**Peripherals**

- Decision and registry tests.
  [`CombatTacticsRulesTests.cs:1`](../../Assets/Tests/EditMode/CombatTacticsRulesTests.cs#L1)
  [`AttackTokenRegistryTests.cs:1`](../../Assets/Tests/EditMode/AttackTokenRegistryTests.cs#L1)

- Folder docs: Combat tactics section.
  [`CLAUDE.md:291`](../../Assets/_Game/Scripts/AI/CLAUDE.md#L291)
