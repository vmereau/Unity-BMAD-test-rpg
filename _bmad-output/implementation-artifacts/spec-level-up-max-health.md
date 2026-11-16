---
title: 'Level-up grants a fixed max-health bonus'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '120f4983565c8238b74cdb33ce1cbdda0be2b6eb'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A level-up only awards Learning Points; the player's health pool never grows with level, so leveling feels weightless defensively.

**Approach:** Each level-up permanently raises the player's max health by a fixed, tunable amount (default 10) and heals current health by the same amount, reusing the existing saved max-health bonus (`PlayerHealth.AddMaxHealthBonus` → `bonusMaxHealth` in the save).

## Boundaries & Constraints

**Always:** The amount lives in `ProgressionConfigSO` (`healthPerLevel`, default 10) — never hardcoded. Subscribe to `OnLevelUp` in `OnEnable`, unsubscribe in `OnDisable` with the null guard. One bonus per level gained (a multi-level XP jump raises `OnLevelUp` once per level, so it stacks naturally). Persistence goes through the existing `bonusMaxHealth` save field — no new save data.

**Ask First:** Changing the bonus to scale with level or Endurance; showing a HUD toast for the gain.

**Never:** Re-grant the bonus on save load (restore paths never raise `OnLevelUp`). Touch `LearningPointSystem` LP logic. Add a new save field.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Single level-up | HP 60/100, healthPerLevel 10 | MaxHealth 110, HP 70, `OnPlayerHealthChanged` raised | N/A |
| Multi-level jump | 2 `OnLevelUp` raises | MaxHealth +20, HP +20 | N/A |
| Zero / negative config | healthPerLevel ≤ 0 | No change, no warning spam | Silently skipped |
| Config missing | `_progressionConfig` null | No change | One warning in `Awake` |
| Save/load | Leveled twice then load | MaxHealth = base + saved bonus, not doubled | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` -- progression tuning; add `healthPerLevel`
- `Assets/_Game/Data/Config/ProgressionConfig.asset` -- live config asset
- `Assets/_Game/Scripts/Player/PlayerHealth.cs` -- owns `MaxHealth = baseHealth + _bonusMaxHealth`, `AddMaxHealthBonus`
- `Assets/_Game/Scripts/Player/Progression/LevelSystem.cs` -- raises `OnLevelUp` once per level gained
- `Assets/_Game/Data/Events/OnLevelUp.asset` -- `GameEventSO_Int` channel
- `Assets/_Game/Prefabs/Player/Player.prefab` -- hosts `PlayerHealth` (needs the two new refs wired)
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` -- already saves/restores `BonusMaxHealth`
- `Assets/Tests/EditMode/PlayerRestoreTests.cs` -- reflection-wired `PlayerHealth` test pattern

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` -- add `[Header("Health — level-up")] public int healthPerLevel = 10;` with tooltip -- tunable value
- [x] `Assets/_Game/Data/Config/ProgressionConfig.asset` -- add `healthPerLevel: 10` -- explicit serialized value
- [x] `Assets/_Game/Scripts/Player/PlayerHealth.cs` -- add `[SerializeField] ProgressionConfigSO _progressionConfig` and `[SerializeField] GameEventSO_Int _onLevelUp`; warn in `Awake` if either is null; subscribe/unsubscribe in `OnEnable`/`OnDisable`; private `HandleLevelUp(int level)` calls `AddMaxHealthBonus(healthPerLevel)` when > 0 and logs the level -- level-up health gain via the existing saved bonus
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` -- wire `_progressionConfig` → `ProgressionConfig.asset`, `_onLevelUp` → `OnLevelUp.asset` on `PlayerHealth` -- runtime wiring
- [x] `Assets/Tests/EditMode/LevelUpHealthTests.cs` -- tests for single level-up, two level-ups, healthPerLevel 0 (invoke `HandleLevelUp` via reflection, as in `PlayerRestoreTests`) -- cover the matrix
- [x] `Assets/_Game/Scripts/Player/Progression/CLAUDE.md` -- add `OnLevelUp → PlayerHealth (+healthPerLevel max HP)` to the event chain -- keep docs accurate

**Acceptance Criteria:**
- Given the game is running with the Player prefab, when the player levels up, then the character screen HP shows max +10 and current +10, and the HUD health bar updates.
- Given the player leveled up and saved, when the save is loaded, then max health equals base + saved bonus (no extra bonus from the load).

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all tests pass, including `LevelUpHealthTests`
- Unity MCP `read_console` -- expected: no compile errors; no `PlayerHealth` missing-ref warnings in Play mode

## Suggested Review Order

**Level-up → health**

- Entry point: one bonus per OnLevelUp raise, reusing the saved max-health bonus.
  [`PlayerHealth.cs:119`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L119)

- Subscription lifecycle with the OnDisable null guard.
  [`PlayerHealth.cs:106`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L106)

- Existing bonus path: +max, +current, raises the HUD event, ignored when dead.
  [`PlayerHealth.cs:234`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L234)

- New serialized refs and the missing-wiring warning.
  [`PlayerHealth.cs:45`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L45)

**Config & wiring**

- Tunable amount, default 10.
  [`ProgressionConfigSO.cs:22`](../../Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs#L22)

- Live asset value.
  [`ProgressionConfig.asset:18`](../../Assets/_Game/Data/Config/ProgressionConfig.asset#L18)

- Player prefab refs to ProgressionConfig and OnLevelUp.
  [`Player.prefab:612`](../../Assets/_Game/Prefabs/Player/Player.prefab#L612)

**Peripherals**

- Tests: single, stacked, subscription, zero config, missing config.
  [`LevelUpHealthTests.cs:26`](../../Assets/Tests/EditMode/LevelUpHealthTests.cs#L26)

- Event-chain doc.
  [`CLAUDE.md:16`](../../Assets/_Game/Scripts/Player/Progression/CLAUDE.md#L16)
