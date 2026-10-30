---
title: 'Player stance — stagger and knockdown, AI stance damage'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: '78ddb8651889b1ffba6e7804c863704e9179dc67'
context: ['{project-root}/.claude/rules/attack-pipeline.md', '{project-root}/_bmad-output/implementation-artifacts/spec-ai-stance-stagger-knockdown.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Only AI has stance (spec-ai-stance-stagger-knockdown). AI hits never stagger the player, so trading
blows with a bandit or a wolf has no weight on the player side.

**Approach:** The player gets the same hidden, regenerating stance meter and the same rules (threshold stagger,
0 = knockdown, refill on getting up), sharing one meter class with `EntityStance`. AI melee hits now deal stance
damage to any stance target. Humanoid NPCs work like the player: `Entity` base + an optional data-only `WeaponSO`
on `NPCEntity` (stance + knockdown flag), × finisher on the last combo hit. Monsters deal a fixed per-attack value
with a fixed knockdown flag.

## Boundaries & Constraints

**Always:**
- Same `StanceMath` rules as AI (once-per-drain stagger, no-KD floor, hyper-armor while attacking).
- Regular block absorbs a hit → stance damage × `blockStanceMultiplier`, never knockdown. Perfect block / dodge → none.
- Player stagger / knockdown: attack + combo cancelled, block lowered, aim stopped, sneak exited; movement and
  every action locked (`IsBusy`) for the config duration. Health damage still applies while down. Death overrides;
  revive / load resets stance to full with no reaction.
- AI → any `IStanceTarget` (player or NPC), applied after `TakeDamage` only if the target is alive.
- Player stance config lives in `CombatConfigSO`; stance is hidden and not saved.
- Animator only through `PlayerAnimationDriver` → `HumanoidAnimationBridge` (existing `PlayStagger` / `PlayKnockDown`).

**Ask First:** changing `PlayerHealth.TakeDamage` / death flow beyond the reset hook; changing controller transitions.

**Never:** stance UI; guard break; i-frames while down; NPC weapon visuals / hitboxes / damage from the `WeaponSO`;
per-attack monster tables (monsters have one attack); player `GetHit` reaction on non-stance hits.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Bandit hits player | stance 60/100, thr 50%, NPC base 15, no weapon | 45 → player staggers, attack cancelled | N/A |
| Unarmed floor | player stance 50, NPC no weapon | no stance change (can't KD) | N/A |
| Armed NPC | base 15 + axe 35 (KD on), finisher ×1.5, stance 60 | 75 → 0 → knockdown | N/A |
| Blocked | stance 100, hit 40, multiplier 0.5 | 80, no reaction | N/A |
| Wolf KD | wolf 25 KD on, player stance 20 | 0 → knockdown | N/A |
| Guard vs bandit | guard hit lands on bandit | bandit `EntityStance` drains (hyper-armor applies) | N/A |
| Lethal | player health → 0 | death only | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/StanceMath.cs` -- pure rules; add humanoid formula
- `Assets/_Game/Scripts/AI/EntityStance.cs` -- AI meter; refactor onto the shared meter
- `Assets/_Game/Scripts/Combat/IStanceTarget.cs` -- seam used by `PlayerCombat` and now `EntityMeleeAttacker`
- `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs` -- `HandleHit` (combo step via `_combo`)
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs`, `NPC/NPCEntity.cs`, `Monsters/MonsterEntity.cs` -- per-entity data
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `TryReceiveHit`, `CancelAttack`, block / aim lowering
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `IsBusy` gate; `PlayerAnimationDriver.cs` -- bridge calls
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` -- load / revive
- `Assets/_Game/Prefabs/Player/` -- player prefab gets `PlayerStance`

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Combat/StanceMeter.cs` -- plain C# meter (current, staggered / knocked down, regen delay, reaction timer): `Apply(amount, canKnockDown, settings) → StanceReaction`, `Tick(dt, settings) → recovered`, `Reset` -- one implementation for AI + player
- [x] `Scripts/AI/EntityStance.cs` -- use `StanceMeter`; behaviour and public API unchanged; implement new interface member
- [x] `IStanceTarget.cs` -- add `ApplyBlockedStanceDamage(float amount)` (AI: same as a no-KD hit)
- [x] `StanceMath.cs` -- `ComputeHumanoidStanceDamage(base, weaponStance, isFinisher, finisherMultiplier)`
- [x] `Entity.cs` -- `_attackStanceDamage`, `_attackCanKnockDown`; virtual `ResolveAttackStance(int step, int hits, out bool canKnockDown)` (monster: fixed)
- [x] `NPCEntity.cs` -- `[Header("Combat")]` `_weapon` (`WeaponSO`, data only), `_finisherStanceMultiplier` (1.5); override → humanoid formula, KD = weapon's resolved flag (none → false), finisher when `hits > 1 && step == hits`
- [x] `EntityMeleeAttacker.cs` -- NotBlocked: after `TakeDamage`, alive target's `IStanceTarget.ApplyStanceDamage`; Blocked: `ApplyBlockedStanceDamage`
- [x] `CombatConfigSO.cs` -- `[Header("Player Stance")]` max, threshold, regen delay / rate, attack resistance, `blockStanceMultiplier` (0.5), stagger (1.1 s) / knockdown (4.6 s) durations
- [x] `Scripts/Player/PlayerStance.cs` -- `IStanceTarget` on Player root: meter + hyper-armor while `IsAttacking`, reaction → `PlayerCombat.InterruptForStance()`, `PlayerStateManager.BeginStagger / BeginKnockDown / EndStanceReaction` (state manager drives the animation driver, per the player gating pattern); ignores dead; `ResetStance()`; info log per hit
- [x] `PlayerCombat.cs` -- public `InterruptForStance()` (cancel attack, lower block, close PB window, stop aim)
- [x] `PlayerStateManager.cs` -- `IsStaggered` / `IsKnockedDown` / `IsIncapacitated` in `IsBusy`; `SetStanceReaction` exits sneak; cleared by `SetDead`
- [x] `PlayerAnimationDriver.cs` -- `PlayStagger(int)`, `PlayKnockDown()`, `ClearStanceReaction()`
- [x] `PlayerSaveAdapter.cs` -- `PlayerStance.ResetStance()` on load / revive
- [x] Player prefab -- add + wire `PlayerStance`
- [x] Data -- NPCs `_attackStanceDamage` 15; monsters: rat 10, viper 15, spider 20, wolf 25 KD, grunt 30 KD
- [x] Tests -- `StanceMeterTests` (stagger, re-arm after regen, knockdown refill, no damage while down, blocked never KD), humanoid formula in `StanceMathTests`
- [x] Docs -- `Scripts/Player/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Scripts/Combat/CLAUDE.md`, `deferred-work.md` (remove "Player stance", add stance UI / guard break / NPC weapon visuals)

**Acceptance Criteria:**
- Given a bandit combo on an unblocking player, when stance crosses the threshold, then the player's attack stops, a stagger clip plays and input is locked until it ends.
- Given a wolf (KD on) draining the player to 0, then `knocked down` → `getting up` play, the wolf can still deal health damage, and the player gets up at full stance.
- Given the player dies while knocked down, then the death animation plays and loading a save restores full stance with control.
- Given a guard fighting a bandit, then the bandit can be staggered by the guard's hits.

## Design Notes

`StanceMeter` holds state only; settings come from `Entity` (AI) or `CombatConfigSO` (player) through a small
readonly struct, so both owners share the exact timer / regen / refill logic and the tests cover both.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green incl. `StanceMeterTests`, `StanceMathTests`
- `read_console` -- expected: no compile errors

**Manual checks:**
- Play StartingTown: get staggered by a stance-test bandit, knocked down by a wolf, block-absorb hits, watch a guard stagger a bandit.

## Suggested Review Order

**Shared stance meter**

- Entry point: one meter (drain, regen delay, reaction timer, knockdown refill) for AI and player.
  [`StanceMeter.cs:61`](../../Assets/_Game/Scripts/Combat/StanceMeter.cs#L61)

- Recovery: knockdown refills, regen timer reset; no regen while reacting.
  [`StanceMeter.cs:88`](../../Assets/_Game/Scripts/Combat/StanceMeter.cs#L88)

- `EntityStance` now delegates to the meter; hyper-armor, log and events unchanged.
  [`EntityStance.cs:106`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L106)

**AI → stance targets**

- Landed hits apply stance after health (lethal skipped); blocked hits use the block path.
  [`EntityMeleeAttacker.cs:249`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L249)
  [`EntityMeleeAttacker.cs:235`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L235)

- Amount from the attacker's Entity at the current combo step.
  [`EntityMeleeAttacker.cs:256`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L256)

- Monsters: fixed stance + knockdown flag.
  [`Entity.cs:146`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L146)

- Humanoids: base + data-only weapon, finisher on the last combo hit, weapon decides knockdown.
  [`NPCEntity.cs:42`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L42)

**Player stance**

- Block multiplier, hyper-armor, then reaction → interrupt + state manager.
  [`PlayerStance.cs:96`](../../Assets/_Game/Scripts/Player/PlayerStance.cs#L96)

- Death refills; disable mid-reaction unlocks; load resets.
  [`PlayerStance.cs:59`](../../Assets/_Game/Scripts/Player/PlayerStance.cs#L59)
  [`PlayerStance.cs:75`](../../Assets/_Game/Scripts/Player/PlayerStance.cs#L75)
  [`PlayerStance.cs:87`](../../Assets/_Game/Scripts/Player/PlayerStance.cs#L87)

- Interrupt: attack/combo ended, block + PB window closed, aim stopped.
  [`PlayerCombat.cs:446`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L446)

**Player lock**

- `IsIncapacitated` folded into `IsBusy` → every `Can*` gate and movement.
  [`PlayerStateManager.cs:69`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L69)

- Reaction setters drive the animation driver (exit sneak); end clears triggers / rebinds on load.
  [`PlayerStateManager.cs:243`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L243)
  [`PlayerStateManager.cs:265`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L265)

- Death clears untaken stance triggers before Death.
  [`PlayerStateManager.cs:307`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L307)

- Paths outside the `Can*` gates: dodge, interaction, action bar, tabs, lock-on rotation.
  [`DodgeController.cs:126`](../../Assets/_Game/Scripts/Combat/DodgeController.cs#L126)
  [`InteractionSystem.cs:35`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L35)
  [`ActionBarSystem.cs:175`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L175)
  [`UIScreenManager.cs:107`](../../Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs#L107)
  [`PlayerController.cs:132`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L132)

- Load resets stance after the revive.
  [`PlayerSaveAdapter.cs:157`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L157)

**Config, math & tests**

- Player stance tuning.
  [`CombatConfigSO.cs:61`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L61)

- Humanoid formula + block multiplier.
  [`StanceMath.cs:64`](../../Assets/_Game/Scripts/Combat/StanceMath.cs#L64)

- Meter tests (stagger, re-arm, knockdown refill, floor, regen delay).
  [`StanceMeterTests.cs:8`](../../Assets/Tests/EditMode/StanceMeterTests.cs#L8)
