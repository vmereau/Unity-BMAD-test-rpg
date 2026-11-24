---
title: 'Unshielded blocking (fists, one- and two-handed weapons)'
type: 'feature'
created: '2026-11-24'
status: 'done'
baseline_commit: 'd03359d24679a442cb33ab0e7c7852ed5d612482'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Since `spec-shield-offhand-blocking`, the player and NPCs can only block with a shield in hand, so fists, two-handers and a one-hander without a shield can't guard at all, although their block clips (`Unarmed Block Guard`, `2hand blocking`, `Block Idle`) are still wired.

**Approach:** Block without a shield again in any drawn melee mode (fists, one-hander, two-hander), with weaker values than a shield: player 25 stamina per absorbed hit and a 0.2 s perfect-block window (from `CombatConfigSO`); NPCs guard with half their style's `perfectBlockChance` and no shield armor. A held shield keeps its own values.

## Boundaries & Constraints

**Always:** Shield in hand → shield values exactly as today. No shield → unshielded values. Bow equipped → right click stays aim, never block (player); NPC in ranged mode never guards. Values are snapshotted at the raise; an equipment change that changes the block source (shield gained / lost / swapped, bow equipped) lowers a raised block. Existing animator wiring plays the block (no controller / override changes).

**Ask First:** New animator states, parameters or override entries; changing the shield values; changing NPC guard rules beyond the shield gate and the halved perfect-block chance.

**Never:** New block clips; block-angle rules for the player; stamina for NPCs; touching `EquipmentRules` slot logic.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fists block | Unarmed, in combat, RMB | Block raised, PB window 0.2 s, 25 stamina per absorbed hit, high-guard clip | N/A |
| 2H block | Flamberge drawn, RMB | Same values, `2hand blocking` clip | N/A |
| 1H no shield | Sword drawn, no offhand, RMB | Same values, `Block Idle` clip | N/A |
| Shield | Sword + Kite drawn, RMB | 12 stamina, 0.2 s (unchanged) | N/A |
| Low stamina | < 25 stamina, no shield, RMB | Block refused | `GameLog.Warn` |
| Bow | Bow equipped, RMB | Aim, no block | N/A |
| Shield equipped mid-block | Unshielded block raised, shield equipped | Block lowered; next press uses shield values | `GameLog.Info` |
| NPC fists / 2H | Humanoid NPC with `NpcArmament`, fists or two-hander, tactics active | Guards; perfect block at half `perfectBlockChance`; no shield armor | N/A |

</frozen-after-approval>

## Code Map

- `ScriptableObjects/Config/CombatConfigSO.cs` (+ `CombatConfig.asset`) -- stamina section; add the unshielded block values.
- `Scripts/Combat/PlayerCombat.cs` -- `OnBlockStarted` (~315), `LowerBlock`, `HeldShield`, `HandleVisualsRefreshed` (~590), `TryReceiveHit` (~1207); `_blockShield` snapshot.
- `Scripts/AI/EntityGuard.cs` -- `CanGuard` (~119) shield gate; `ResolveHit` (~103) perfect-block chance.
- `Scripts/AI/CombatBlockRules.cs` -- pure rules; add the unshielded perfect-chance helper.
- `Scripts/AI/NpcArmament.cs` -- `ShieldInHand`, `IsRanged`, `IsSwitchingWeapon`.
- `Assets/Tests/EditMode/PerfectBlockTests.cs`, `CombatBlockRulesTests.cs` -- existing block tests.
- Docs: `Scripts/Combat/CLAUDE.md` (*Blocking — shield only*), `Scripts/AI/COMBAT.md` (~94).

## Tasks & Acceptance

**Execution:**
- [x] `CombatConfigSO.cs` + `CombatConfig.asset` -- add `unshieldedBlockStaminaCost = 25`, `unshieldedPerfectBlockWindow = 0.2` (tooltips; fix the header comment) -- tunable fallback.
- [x] `PlayerCombat.cs` -- add a pure static `ResolveBlockValues(ShieldSO shield, CombatConfigSO config)` → (cost, window); `OnBlockStarted` drops the shield requirement (bow still aims), snapshots shield (or null) + cost + window; per-hit `Consume` uses the snapshot cost; fail-closed / refresh checks lower the block when `HeldShield() != _blockShield` or a bow is equipped -- player block in every melee mode.
- [x] `CombatBlockRules.cs` + `EntityGuard.cs` -- `CanGuard` requires `NpcArmament.IsReady` (new), not `IsChangingWeapon`, not `IsRanged` (shield no longer required); `ResolveHit` uses `CombatBlockRules.PerfectBlockChance(style.perfectBlockChance, shieldInHand)` (× `CombatStyleSO.unshieldedPerfectBlockMultiplier`, default 0.5, without a shield — a style field, not a const: tunables live in SOs) -- NPC parity.
- [x] `Tests/EditMode/` -- `ResolveBlockValues` (shield vs none) and `PerfectBlockChance` (shield vs none) tests; keep `PerfectBlockTests` green -- regression guard.
- [x] Docs -- rewrite the Combat `CLAUDE.md` blocking section and `COMBAT.md` guard gate for unshielded blocking -- knowledge capture.

**Acceptance Criteria:**
- Given the player unarmed, holding a two-hander, or a one-hander without a shield, when blocking a guard NPC's hit outside the window, then the hit is blocked and 25 stamina is spent (console: "Perfect block window opened (0.20s, unshielded)" at the raise — the F-debug overlay is compiled out).
- Given the change, when EditMode tests run, then all pass and the console shows no new errors.

## Design Notes

The one-hander without a shield plays the sword's `Block Idle` (shield-style pose) through the Sword override — accepted, no new clip. NPC body armor is unchanged; only the shield's `defenseBonus` is missing without one (already the case in `ShieldArmorBonus`).

## Verification

**Commands:**
- `run_tests` EditMode -- expected: all pass.
- `read_console` after compile -- expected: no errors.

**Manual checks:**
- Play StartingTown: block a guard with fists, Flamberge, sword alone, sword + shield; watch the stamina bar and the `[PlayerCombat]` block log lines.

## Suggested Review Order

**Player block (entry point)**

- Shield gate gone: any melee mode raises, values resolved then snapshotted.
  [`PlayerCombat.cs:332`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L332)

- Pure value source: shield's own, else config unshielded 25 / 0.2 s.
  [`PlayerCombat.cs:388`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L388)

- One rule for "block no longer valid": held shield changed or bow equipped.
  [`PlayerCombat.cs:395`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L395)

- Same-source refresh keeps the block; re-push IsBlocking after the controller reset.
  [`PlayerCombat.cs:611`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L611)

- Hit path fails closed on source change; charges the snapshotted cost.
  [`PlayerCombat.cs:1237`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1237)

**NPC guard**

- Guard in any ready melee mode; never ranged or mid weapon change.
  [`EntityGuard.cs:126`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L126)

- Perfect chance scaled by the style multiplier without a shield.
  [`EntityGuard.cs:108`](../../Assets/_Game/Scripts/AI/EntityGuard.cs#L108)

- Pure helper behind it.
  [`CombatBlockRules.cs:120`](../../Assets/_Game/Scripts/AI/CombatBlockRules.cs#L120)

- Fists fight: shield on the back gives no armor; `IsReady` exposed.
  [`NpcArmament.cs:140`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L140)

**Config and tests**

- Player unshielded tunables (asset holds 25 / 0.2).
  [`CombatConfigSO.cs:25`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L25)

- NPC unshielded multiplier, default 0.5 on existing styles.
  [`CombatStyleSO.cs:58`](../../Assets/_Game/ScriptableObjects/Entities/CombatStyleSO.cs#L58)

- Shield vs none value resolution.
  [`PlayerBlockValuesTests.cs:10`](../../Assets/Tests/EditMode/PlayerBlockValuesTests.cs#L10)

- Perfect chance with / without shield.
  [`CombatBlockRulesTests.cs:231`](../../Assets/Tests/EditMode/CombatBlockRulesTests.cs#L231)
