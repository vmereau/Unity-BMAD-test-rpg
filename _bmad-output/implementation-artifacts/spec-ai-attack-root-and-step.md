---
title: 'AI attacks root the entity and step forward like the player'
type: 'feature'
created: '2026-11-17'
status: 'done'
baseline_commit: '1d8b1c9'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/PLAYER_COMBO.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An attacking entity keeps walking: `EntityBrain.HandleAttack` returns to Engaging (agent moving) when the target leaves `AttackRange` mid-swing, faces the target for the whole swing, and humanoid NPCs never make the forward step the player makes on each attack.

**Approach:** Apply the player's swing rules to every entity: rooted while an attack state plays (the swing and its combo finish, then the brain chases), turning toward the target only during the wind-up (committed once a hit window opens), and — for humanoids — the same per-attack forward `AttackStep` as the player (weapon archetype steps or `CombatConfigSO.unarmedAttacks`), resolved by one shared function.

## Boundaries & Constraints

**Always:**
- One step resolver used by both `PlayerCombat` and the AI: weapon → `WeaponSO.ResolveAttackStep(index, defaultAttackStep)`, fists → `unarmedAttacks[index − 1].step`, else `defaultAttackStep`. Player behaviour unchanged.
- AI step = constant-speed displacement along the entity's forward through `NavMeshAgent.Move` (stays on the navmesh), timed in seconds since the attack state entered (`AttackStep.DisplacementBetween`). A combo state enter replaces the running step.
- Step stops when the attack ends (`!IsAttacking`), the agent is disabled / off-mesh, or the root would get closer than a min distance to the attack target (no walking into the victim).
- Rooting/commit apply to all entities (monsters too); monsters get no step (`EntityBase.controller` Attack index 0 = no step).
- A dead / invalid target still ends the attack immediately (existing path).
- SMBs stay silent no-ops when their receiver is absent (`if (x != null)`, never `?.`).

**Ask First:** Changing `AttackRange`, `AttackCooldown`, step values, or any clip / clip event.

**Never:** Root motion on NPC animators; recovery-cancel for AI; touching `epics.md` / `sprint-status.yaml`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unarmed NPC combo | NPC in range, 3-hit combo | Rooted; steps 0.30 / 0.35 / 0.50 m in each step window | N/A |
| Armed NPC | Wielding a sword/axe/2H | Step from that weapon's archetype `attackSteps` (else `defaultAttackStep`) | N/A |
| Target backs off mid-swing | dist > AttackRange during an attack state | Swing (and remaining combo) finishes in place, then Engaging | N/A |
| Target sidesteps after commit | HitboxEnable already fired | NPC no longer turns; can miss | N/A |
| Target hugging the NPC | dist ≤ min step distance | No forward displacement | N/A |
| Monster | Spider / wolf attack | Rooted + wind-up-only facing, no step | No `EntityAttackStep` → none |
| Target dies mid-swing | target invalid | Attack ends (unchanged) | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/Player/PlayerAttackStep.cs` -- player step mover (reference behaviour)
- `Scripts/Combat/PlayerCombat.cs:1002` -- `ResolveAttackStep` (to route through the shared resolver)
- `ScriptableObjects/Config/CombatConfigSO.cs` -- `defaultAttackStep`, `unarmedAttacks`
- `ScriptableObjects/Items/Weapons/AttackStep.cs` -- step data + `DisplacementBetween`
- `Scripts/AI/SMB_EntityAttackState.cs` / `EntityAnimationEventReceiver.cs` -- AI attack-state enter/exit routing (no index today)
- `Scripts/AI/EntityMeleeAttacker.cs` -- attack bracket, windows, `WieldedWeapon`
- `Scripts/AI/EntityBrain.cs:692` -- `HandleAttack` (range exit before in-swing guard, `FaceTarget` every frame); `ExecuteAttack` ~2056
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- 3 `SMB_EntityAttackState` instances on Attack_1/2/3_State
- `Prefabs/Entities/NPC_base Variant.prefab` -- humanoid NPC root (gets the step component)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- add `ResolveAttackStep(WeaponSO weapon, int attackIndex)` (rule above); `PlayerCombat.ResolveAttackStep` calls it -- one source of truth
- [x] `Scripts/AI/SMB_EntityAttackState.cs` + `EntityAnimationEventReceiver.cs` -- serialized `attackIndex` (0 = no step) passed through `NotifyAttackEntered(int)` -- AI needs the combo index
- [x] `Humanoid_Template.controller` -- set `attackIndex` 1/2/3 on the entity SMB of Attack_1/2/3_State -- override controllers inherit it
- [x] `Scripts/AI/EntityMeleeAttacker.cs` -- `NotifyAttackStateEntered(int)` clears `IsAttackCommitted` and starts the optional same-GO `EntityAttackStep` with the resolved step; `OpenWindow` sets `IsAttackCommitted`; `BeginAttack(damage, hits, target)` stores the step target -- commit + step trigger
- [x] `Scripts/AI/EntityAttackStep.cs` (new, `Game.AI`) -- `[SerializeField] CombatConfigSO`, `_minTargetDistance` (0.8 m); `Begin(index)` resolves via `WieldedWeapon`; `Update` moves with `NavMeshAgent.Move` under the stop rules -- humanoid step
- [x] `Scripts/AI/EntityBrain.cs` -- `HandleAttack`: right after the valid-target check, while `IsInAttackState` stay rooted (agent stopped), `FaceTarget()` only if `!IsAttackCommitted`, return; pass the target to `BeginAttack` -- rooting + commit
- [x] `Prefabs/Entities/NPC_base Variant.prefab` -- add `EntityAttackStep` on the root, wire the `CombatConfig` asset -- all humanoid NPCs
- [x] `Tests/EditMode` -- `CombatConfigSO.ResolveAttackStep` cases (unarmed index 1..3 / out of range, weapon with / without archetype steps) -- regression on the shared rule
- [x] `Scripts/AI/CLAUDE.md`, `.claude/rules/attack-pipeline.md` -- document rooting, commit, `EntityAttackStep`, SMB index

**Acceptance Criteria:**
- Given an unarmed NPC fighting the player, when it attacks, then its root does not translate except the configured forward step of each attack state, and it never walks until the last attack state exits.
- Given a monster in Attacking, when the player retreats mid-swing, then the monster finishes the swing in place before chasing.
- Given any entity mid-swing, when its hit window opens, then its yaw stops following the target until the next attack state enters.
- Given the player attacks, then their steps are identical to before (same values, same timing).

## Design Notes

Commit flag lives on `EntityMeleeAttacker` (it already receives `HitboxEnable` and state enters), so the brain only reads `IsInAttackState` / `IsAttackCommitted`. `NavMeshAgent.Move` works while `isStopped` and leaves `agent.velocity` at 0, so locomotion params stay idle under the full-body attack clip.

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`) -- expected: all pass, incl. new resolver tests
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play StartingTown, provoke an unarmed NPC: log root displacement per attack state ≈ configured step; retreat mid-combo → NPC finishes rooted.

## Suggested Review Order

**Rooting + wind-up steering (all entities)**

- Entry point: mid-swing branch returns before range / sight checks — swing finishes rooted.
  [`EntityBrain.cs:702`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L702)

- Rooting predicate: in-state or trigger pending, capped by a 4 s safety timeout.
  [`EntityMeleeAttacker.cs:79`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L79)

- Commit only when a hitbox really opens; state enter re-opens steering.
  [`EntityMeleeAttacker.cs:268`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L268)

- Cancelled swing still blending out: no new attack (old guard kept).
  [`EntityBrain.cs:732`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L732)

**Forward step (humanoids)**

- Shared resolver used by player and AI — one rule for steps.
  [`CombatConfigSO.cs:192`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L192)

- State enter starts the step with the SMB's combo index.
  [`EntityMeleeAttacker.cs:244`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L244)

- Step mover: NavMeshAgent.Move, stops with the attack.
  [`EntityAttackStep.cs:70`](../../Assets/_Game/Scripts/AI/EntityAttackStep.cs#L70)

- Body-radius stand-off so the step never walks into the victim.
  [`EntityAttackStep.cs:91`](../../Assets/_Game/Scripts/AI/EntityAttackStep.cs#L91)

- Target handed to the attacker for the stand-off.
  [`EntityBrain.cs:2073`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L2073)

**Plumbing**

- SMB carries attackIndex (1/2/3 on Humanoid_Template, 0 on monsters).
  [`SMB_EntityAttackState.cs:17`](../../Assets/_Game/Scripts/AI/SMB_EntityAttackState.cs#L17)

- Player now routes through the shared resolver (same values).
  [`PlayerCombat.cs:1002`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1002)

**Tests**

- Resolver cases.
  [`CombatConfigAttackStepTests.cs:1`](../../Assets/Tests/EditMode/CombatConfigAttackStepTests.cs#L1)

- Commit flag cases.
  [`EntityMeleeAttackerTests.cs:187`](../../Assets/Tests/EditMode/EntityMeleeAttackerTests.cs#L187)
