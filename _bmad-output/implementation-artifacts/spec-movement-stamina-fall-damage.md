---
title: 'Movement costs: aggro run drain, aggro jump cost, fall damage'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '852c02e48c75a6f222ac1f2f80a2afd9221c4334'
context:
  - '{project-root}/Assets/_Game/Scripts/Player/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/AI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Running and jumping are free even while enemies fight the player, so stamina never matters when
fleeing, and the player takes no damage from any fall.

**Approach:** A player flag `IsAggroed` (any live enemy, hostile or neutral, is fighting the player) gates two stamina
costs: running drains stamina per second (modifiable rate, same exhaust rule as swim sprint) and each jump costs a
flat amount (refused when short). Separately, landing after a free fall above a safe height deals damage that ignores
Defense.

## Boundaries & Constraints

**Always:** `IsAggroed` on `PlayerStateManager`, read like `IsInCombat`; "aggroed" = some active `EntityBrain`
`IsFighting(player member)` (Warning, Engaging, Attacking, Searching on the player — any faction). Drain rate and jump
cost are read through public properties with a runtime multiplier (equipment / skills / stats hook later), base values
in `PlayerConfigSO`. Defaults: run drain 8/s, jump 15, safe fall 11 m, 4 HP per metre above. Fall height = highest
point since last grounded − landing height; any grounded frame (incl. steep terrain contacts) ends the fall. Pure rules
in a static helper with EditMode tests. Null-guard every optional dependency (warn once in `Awake`).

**Ask First:** Changing the default numbers; adding HUD feedback; making drawn-weapon stance (`IsInCombat`) cost
stamina.

**Never:** Stamina cost outside aggro; fall damage while swimming, on a water landing, or after a teleport / load;
stagger or knockdown from a fall; changes to dodge or attack costs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Run, aggroed | Sprint + move, a brain fighting the player | Run speed, −8 stamina/s (whole points) | N/A |
| Run, exhausted | Stamina below next whole point | Walk speed until Sprint released and pressed again | N/A |
| Run, not aggroed | No brain fighting the player | Run speed, no drain | N/A |
| Jump, aggroed | 15+ stamina | Jumps, −15 | N/A |
| Jump, aggroed, short | < 15 stamina | No jump, nothing spent | N/A |
| Jump, not aggroed | Any stamina | Jumps free | N/A |
| Short fall | Fall ≤ 11 m (goat-track steps ~10 m / ~7 m) | No damage | N/A |
| Long fall | 16 m fall | 20 damage, Defense ignored, hurt sound | N/A |
| Water landing | Fall ends in swim | No damage | N/A |
| Teleport | Load / respawn mid-air | Fall tracking reset, no damage | N/A |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityBrain.cs` -- static `_active` brain list, `IsFighting(member)`; add static `IsAnyFighting(member)`
- `Scripts/Player/PlayerStateManager.cs` -- state flags + `Can*` gates; add `IsAggroed`
- `Scripts/Player/PlayerController.cs` -- `ApplyJump`, sprint speed choice, `ResetVerticalVelocity` (teleports)
- `Scripts/Player/PlayerSwimming.cs` -- `UpdateSprint` whole-point debt / exhaust pattern to mirror
- `Scripts/Combat/StaminaSystem.cs` -- `HasEnough` / `Consume`
- `Scripts/Player/PlayerHealth.cs` -- `TakeDamage` (Defense mitigation, hurt sfx, death)
- `ScriptableObjects/Config/PlayerConfigSO.cs`, `Data/Config/PlayerConfig.asset` -- movement tuning
- `Prefabs/Player/Player.prefab` -- player root components

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/AI/EntityBrain.cs` -- `public static bool IsAnyFighting(FactionMember member)` over `_active` -- one query for the player
- [x] `Scripts/Player/PlayerStateManager.cs` -- `IsAggroed` (same-GO `FactionMember` resolved in `Awake`, value cached per frame) -- the readable flag
- [x] `ScriptableObjects/Config/PlayerConfigSO.cs` + `PlayerConfig.asset` -- `[Header("Aggro")] aggroRunStaminaPerSecond 8, aggroJumpStaminaCost 15`; `[Header("Fall Damage")] fallSafeHeight 11, fallDamagePerMetre 4`
- [x] `Scripts/Player/MovementStaminaRules.cs` (new) -- pure `FallDamage(height, safe, perMetre)` and whole-point drain step (debt in → consume amount / exhausted out)
- [x] `Scripts/Player/PlayerMovementStamina.cs` (new) -- `SprintStaminaPerSecond`, `JumpStaminaCost` (base × `SprintDrainMultiplier` / `JumpCostMultiplier`, default 1, clamped ≥ 0); per-frame drain while aggroed and actually running; `IsRunExhausted`; `TryPayJump()` (free when not aggroed)
- [x] `Scripts/Player/PlayerController.cs` -- run speed only when not `IsRunExhausted`; jump calls `TryPayJump()` after the state gate; `ResetVerticalVelocity` also resets fall tracking
- [x] `Scripts/Player/PlayerHealth.cs` -- `TakeUnmitigatedDamage(float)` sharing the hurt / death path, no Defense
- [x] `Scripts/Player/PlayerFallDamage.cs` (new) -- after movement, tracks apex while airborne, on landing applies rule damage + logs height; resets on swim and on `ResetFall()`
- [x] `Prefabs/Player/Player.prefab` -- add `PlayerMovementStamina`, `PlayerFallDamage`, wire config / stamina / health
- [x] `Tests/EditMode/MovementStaminaRulesTests.cs` -- matrix rows for fall damage, drain step, exhaust
- [x] `Scripts/Player/CLAUDE.md`, `Scripts/AI/CLAUDE.md` -- document `IsAggroed`, movement costs, fall damage

**Acceptance Criteria:**
- Given an enemy fighting the player, when the player sprints for 5 s from full, then stamina drops ≈40 and the HUD bar follows.
- Given a dead or disengaged enemy, when its brain leaves combat, then `IsAggroed` becomes false the same frame.
- Given the goat-track drop, when the player walks off both steps, then no fall damage is logged; a jump off the ring rim to the pad (≥ 16 m) deals damage.
- EditMode tests green; console free of new errors after compile.

## Design Notes

`IsAggroed` stays separate from `IsInCombat` (drawn weapon, drives animator); unarmed fleeing must still cost. Fall
damage bypasses Defense so armour never makes long drops harmless.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- all pass
- MCP `read_console` errors after refresh -- none new

**Manual checks:**
- Play mode: sprint near a hostile, jump with < 15 stamina, walk the goat-track drop, jump from the rim.

## Suggested Review Order

**Aggro flag**

- Entry point: one read-only flag, any faction, independent of the drawn-weapon IsInCombat.
  [`PlayerStateManager.cs:131`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L131)

- Static query over the existing active-brain list, same IsFighting rule lock-on uses.
  [`EntityBrain.cs:72`](../../Assets/_Game/Scripts/AI/EntityBrain.cs#L72)

**Run drain and jump cost**

- Modifiable rates: base config × runtime multiplier (equipment / skills / stats hook).
  [`PlayerMovementStamina.cs:37`](../../Assets/_Game/Scripts/Player/PlayerMovementStamina.cs#L37)

- Per-frame whole-point drain; debt kept between runs so tapping Sprint never runs free.
  [`PlayerMovementStamina.cs:77`](../../Assets/_Game/Scripts/Player/PlayerMovementStamina.cs#L77)

- Jump paid after the state gate; refused jump spends nothing.
  [`PlayerMovementStamina.cs:109`](../../Assets/_Game/Scripts/Player/PlayerMovementStamina.cs#L109)

- Controller hooks: refused jump, exhausted run falls back to walk speed.
  [`PlayerController.cs:126`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L126)
  [`PlayerController.cs:234`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L234)

- Exhausted Shift no longer counts as a sprint, so sneak stays.
  [`PlayerSneak.cs:125`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L125)

**Fall damage**

- Apex-to-landing tracking; teleport check ignores downward motion so hitches never cancel a fall.
  [`PlayerFallDamage.cs:58`](../../Assets/_Game/Scripts/Player/PlayerFallDamage.cs#L58)

- Water-landing guard for fast falls into shallow pools.
  [`PlayerSwimming.cs:118`](../../Assets/_Game/Scripts/Player/PlayerSwimming.cs#L118)

- Damage path without Defense, shared hurt / death handling.
  [`PlayerHealth.cs:191`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L191)

- Load teleport resets the fall.
  [`PlayerController.cs:33`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L33)

**Rules, config, tests**

- Pure fall / drain math.
  [`MovementStaminaRules.cs:11`](../../Assets/_Game/Scripts/Player/MovementStaminaRules.cs#L11)

- New tunables (8/s, 15, 11 m, 4 HP/m).
  [`PlayerConfigSO.cs:42`](../../Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs#L42)

- Matrix rows as EditMode tests.
  [`MovementStaminaRulesTests.cs:1`](../../Assets/Tests/EditMode/MovementStaminaRulesTests.cs#L1)
