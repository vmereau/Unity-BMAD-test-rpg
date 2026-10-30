---
title: 'AI stance — stagger and knockdown'
type: 'feature'
created: '2026-10-30'
status: 'done'
baseline_commit: 'e0163c5e8747a355cdcc54da90a25daf1eeaf352'
context: ['{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Player hits never interrupt AI: `GetHit` plays on the Base layer while attacks keep running on the
`Attack` layer, and the GDD's "perfect block staggers the attacker" is only a log line. Combat has no weight.

**Approach:** Every AI entity gets a hidden, regenerating **stance** meter. Player hits (and perfect blocks) deal
stance damage. Crossing a threshold **staggers** (interrupts the attack, random Stagger 1/2). Reaching 0
**knocks down** (`knocked down` → `getting up`, animation-based), then stance refills. Stance damage comes from the
weapon archetype (optional per-weapon override), Strength, combo finisher and Power Strike. Only weapons flagged
"can knock down" may push stance below the stagger threshold.

## Boundaries & Constraints

**Always:**
- Stagger fires once per drain (stance goes from above the threshold to at or below it). It re-arms only after
  regen brings stance back above the threshold. A hit that reaches 0 → knockdown only (no stagger first).
- A non-knockdown hit (weapon flag off, unarmed, bow, perfect block) can never take stance below the threshold;
  if stance is already at/below it, the hit deals no stance damage.
- Regen starts after `stanceRegenDelay` without stance damage; stance refills to max on getting up.
- Hyper-armor: stance damage × (1 − `attackStanceResistance`) while the entity is in an attack state.
- While staggered/knocked down: attack ended (`EndAttack`), agent stopped, brain logic paused, no stance damage
  while knocked down, `GetHit` suppressed (health damage still applies — "free damage"). Death overrides all.
- Pure math in a static class with EditMode tests. Animator writes only through bridges; AI code references
  `AIAnimationDriver` only. `Game.Combat` stays AI-free (interface seam like `ISneakAttackTarget`).

**Ask First:** changing the hit order in `PlayerCombat.ResolveHit` beyond inserting the stance call; touching
`PlayerHealth` / player stagger.

**Never:** player stance or player stagger; physics ragdoll knockdown; armor weight / stance bonus; bonus damage
on knocked-down targets; a two-handed archetype asset; NPC-vs-NPC stance damage; any stance UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Stagger | max 100, thr 50%, stance 60, hit 20, no-KD | stance 50 → stagger | N/A |
| Heavy KD | stance 60, hit 70, KD weapon | stance 0 → knockdown, no stagger | N/A |
| Dagger floor | stance 40 (already below thr), hit 30, no-KD | stance 40, no reaction | N/A |
| Perfect block | stance 100, pb dmg 80 | stance 50 → stagger | N/A |
| Hyper-armor | in attack state, resistance 0.5, hit 20 | 10 stance damage | N/A |
| Lethal hit | health → 0 | death only, no stance reaction | N/A |
| Monster | no stagger clips | `GetHit` fallback + timed lock | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `ResolveHit` (shared melee + arrow), `ComputeEffectiveDamage`, `IsMaxCombo`
- `Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs` -- `HandleHit` `PerfectBlock` case (attacker = self)
- `Assets/_Game/Scripts/AI/EntityHealth.cs` -- `TakeDamage` → `TriggerGetHit`
- `Assets/_Game/Scripts/AI/EntityBrain.cs` -- `Update` state switch, `HandleHealthChanged` (engages attacker)
- `Assets/_Game/ScriptableObjects/Entities/Entity.cs` -- per-entity tuning
- `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs`, `../WeaponSO.cs` -- archetype + override pattern (`ResolvedComboSteps`)
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- player-side tuning
- `Assets/_Game/Scripts/Core/Animations/` -- `AIAnimationDriver`, humanoid/monster driver + bridge
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- shared player/NPC controller

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Combat/StanceMath.cs` -- pure `Apply(current, max, thresholdFraction, damage, canKnockDown, armed) → (newStance, StanceReaction None/Stagger/KnockDown)` + `Regen` -- testable rules
- [x] `Assets/Tests/EditMode/StanceMathTests.cs` -- cover the I/O matrix rows (math-level) + re-arm after regen
- [x] `Assets/_Game/Scripts/Combat/IStanceTarget.cs` -- `ApplyStanceDamage(float amount, bool canKnockDown)` -- seam for `PlayerCombat`
- [x] `WeaponArchetypeSO.cs` / `WeaponSO.cs` -- archetype `stanceDamage`, `canKnockDown`; weapon `stanceDamage` (0 = archetype) + `WeaponSO.KnockDownOverride {Archetype, Yes, No}`; `ResolvedStanceDamage` / `ResolvedCanKnockDown`
- [x] `CombatConfigSO.cs` -- `unarmedStanceDamage`, `stancePerStrength`, `finisherStanceMultiplier`, `powerStrikeStanceBonus`
- [x] `Entity.cs` -- `[Header("Stance")]` `_maxStance`, `_staggerThreshold` (0..1), `_stanceRegenDelay`, `_stanceRegenRate`, `_attackStanceResistance` (0..1), `_perfectBlockStanceDamage`, `_staggerDuration`, `_knockdownDuration` + getters
- [x] `Assets/_Game/Scripts/AI/EntityStance.cs` -- `IStanceTarget` on entity root: meter, regen, reaction timers, `IsIncapacitated` / `IsKnockedDown`, same-GO `event Action Staggered / KnockedDown / Recovered`; drives `AIAnimationDriver`; ignores dead; info log per stance hit
- [x] `PlayerCombat.cs` -- after `TakeDamage` in `ResolveHit`, if target alive and `IStanceTarget`: stance = (weapon or unarmed) + Strength bonus, × finisher on max combo step, + Power Strike; arrows use the bow's resolved values snapshotted at fire time, never finisher
- [x] `EntityMeleeAttacker.cs` -- `PerfectBlock` → own `EntityStance.ApplyStanceDamage(Entity.PerfectBlockStanceDamage, false)`
- [x] `EntityHealth.cs` -- skip `TriggerGetHit` while `EntityStance.IsIncapacitated`
- [x] `EntityBrain.cs` -- on `Staggered`/`KnockedDown`: `EndAttack`, stop agent; `Update` skips state handling while incapacitated; on `Recovered`: resume agent (current state continues)
- [x] `AIAnimationDriver` + humanoid driver/bridge -- `TriggerStagger(int variant)`, `TriggerKnockDown()`; params `Stagger` (trigger), `StaggerIndex` (int), `KnockDown` (trigger); monster driver → `GetHit` fallback
- [x] `Humanoid_Template.controller` -- AnyState → `Stagger_1/2_State`, `KnockedDown_State` → `GettingUp_State` on **both** Base and Attack layers (no self-transition), exits back to locomotion / `CombatIdle`; listed before `GetHit`
- [x] `knocked down.fbx`, `getting up.fbx`, `Unarmed/stagger 1/2.fbx` -- verify Humanoid rig (set by Valentin), loop off, root motion baked into pose; fix via `ModelImporter` only if needed
- [x] Data -- archetype values (sword 25 KD off, axe 35 KD on, bow 10 KD off); bandit + monster `Entity` assets get stance defaults
- [x] StartingTown scene (test setup) -- existing bandit: `NPC_Bandit` HP 1000 (as requested; revert noted in deferred-work); 3 extra `bandit (stance test N)` instances, each with its own `StanceTest/NPC_Bandit_StanceTestN` (HP 1000) + `KilledFact_bandit_stanceTestN` -- lets stagger/knockdown be tested without killing
- [x] Docs -- `Scripts/AI/CLAUDE.md`, `Scripts/Combat/CLAUDE.md`, controller CLAUDE.md, `deferred-work.md` (player stance, armor stance, two-handed archetype, ragdoll knockdown, knocked-down bonus skill)

**Acceptance Criteria:**
- Given the bandit mid-combo, when the player's hits drop stance past the threshold, then the punch stops, no further hit lands from it, and a stagger clip (random 1/2) plays before it resumes attacking.
- Given an axe (KD on), when stance reaches 0, then `knocked down` → `getting up` play, the bandit takes health damage without `GetHit`, and gets up at full stance.
- Given a sword (KD off), when the player keeps hitting a staggered bandit, then it is never knocked down.
- Given a perfect block of the bandit, then it staggers at most (never knocked down).
- Given the player (shared controller), when playing normally, then no stance state is ever entered.

## Design Notes

Hit order in `ResolveHit`: `NotifyHitBy` → `TakeDamage` → stance. `HandleHealthChanged` acquires the attacker
first; the stance reaction then pauses the brain without losing the target. `Entity_base` gets `EntityStance`
once → every variant inherits it.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green incl. `StanceMathTests`
- `read_console` -- expected: no compile errors

**Manual checks:**
- Play StartingTown: stagger/knockdown the bandit with sword vs axe; perfect-block it; the spider shows the `GetHit` fallback.

## Suggested Review Order

**Stance rules (pure)**

- Entry point: threshold crossing, no-knockdown floor, knockdown — the whole mechanic in one function.
  [`StanceMath.cs:29`](../../Assets/_Game/Scripts/Combat/StanceMath.cs#L29)

- Player melee formula: weapon/unarmed base + Strength, finisher multiplier, Power Strike.
  [`StanceMath.cs:52`](../../Assets/_Game/Scripts/Combat/StanceMath.cs#L52)

**Stance meter (EntityStance)**

- Hyper-armor, absorbed / floored hits skip the regen reset, then reaction dispatch.
  [`EntityStance.cs:97`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L97)

- Perfect block bypasses hyper-armor (attacker is always mid-attack).
  [`EntityStance.cs:95`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L95)

- Reaction timer; no regen while incapacitated (prevents stun-lock).
  [`EntityStance.cs:66`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L66)

- Recovery refills after knockdown and clears unconsumed animator triggers.
  [`EntityStance.cs:139`](../../Assets/_Game/Scripts/AI/EntityStance.cs#L139)

**Stance sources**

- Stance applied after `TakeDamage` so the damage reaction acquires the attacker first.
  [`PlayerCombat.cs:539`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L539)

- Melee stance damage resolution from the equipped weapon / config.
  [`PlayerCombat.cs:893`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L893)

- Arrows snapshot the bow's resolved stance values at fire time.
  [`PlayerCombat.cs:697`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L697)

- Perfect block hits the attacker's own stance.
  [`EntityMeleeAttacker.cs:230`](../../Assets/_Game/Scripts/AI/EntityMeleeAttacker.cs#L230)

**Brain pause**

- Update gate: state logic paused, agent held while incapacitated.
  [`EntityBrain.cs:306`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L306)

- Reaction start ends the attack; agent `isStopped` saved once and held.
  [`EntityBrain.cs:934`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L934)

- Recovery restores the agent, then deferred engage / routine walk.
  [`EntityBrain.cs:955`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L955)

- Damage reaction defers Engaging until recovery.
  [`EntityBrain.cs:922`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L922)

- Routine change deferred; theft ignored while incapacitated.
  [`EntityBrain.cs:1038`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L1038)
  [`EntityBrain.cs:699`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L699)

- GetHit suppressed / dialogue blocked while incapacitated.
  [`EntityHealth.cs:88`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L88)
  [`EntityPresence.cs:100`](../../Assets/_Game/Scripts/AI/EntityPresence.cs#L100)

**Animation seam**

- Driver defaults fall back to GetHit (monsters).
  [`AIAnimationDriver.cs:19`](../../Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs#L19)

- Bridge clears the same-hit GetHit before triggering the stagger.
  [`HumanoidAnimationBridge.cs:144`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L144)

- Controller states (Base + Attack layers) — inspect in the Animator window.
  [`Humanoid_Template.controller`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller)

**Data & tests**

- Archetype values + per-weapon overrides.
  [`WeaponSO.cs:46`](../../Assets/_Game/ScriptableObjects/Items/WeaponSO.cs#L46)
  [`WeaponArchetypeSO.cs:29`](../../Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs#L29)

- Per-entity tuning (durations must match clips).
  [`Entity.cs:72`](../../Assets/_Game/ScriptableObjects/Entities/Entity.cs#L72)

- Player-side tuning.
  [`CombatConfigSO.cs:49`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L49)

- Rule tests (I/O matrix rows, re-arm, resistance, formula).
  [`StanceMathTests.cs:8`](../../Assets/Tests/EditMode/StanceMathTests.cs#L8)
