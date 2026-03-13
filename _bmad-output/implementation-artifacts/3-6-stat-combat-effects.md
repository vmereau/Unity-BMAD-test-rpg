# Story 3.6: Stat-Combat Effects

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want my stats to affect combat outcomes (Strength → damage, Endurance → stamina pool) and my learned skills to provide tangible effects,
so that investing learning points and gold at trainers produces meaningful improvements in combat capability and validates the entire progression system.

## Acceptance Criteria

1. **`ProgressionConfigSO.cs`** updated with three new fields under a new header:
   - `[Header("Stat Combat Multipliers — Story 3.6")]`
   - `public float damagePerStrength = 2f` — flat damage added per Strength point above base
   - `public float staminaPerEndurance = 5f` — flat stamina pool added per Endurance point above base
   - `public float powerStrikeDamageBonus = 10f` — flat damage bonus applied when "power_strike" skill is learned

2. **`StaminaSystem.cs`** updated to make `MaxStamina` stat-aware:
   - Add `[SerializeField] private PlayerStats _playerStats` (optional — null warn, non-fatal)
   - Add `[SerializeField] private ProgressionConfigSO _progressionConfig` (optional — null warn, non-fatal)
   - In `Awake()`: warn (not error) if either is null: `GameLog.Warn(TAG, "PlayerStats/ProgressionConfigSO not assigned — Endurance stamina bonus inactive")`
   - `MaxStamina` property becomes computed:
     ```csharp
     public float MaxStamina
     {
         get
         {
             float bonus = (_playerStats != null && _progressionConfig != null)
                 ? (_playerStats.Endurance - _progressionConfig.baseEndurance) * _progressionConfig.staminaPerEndurance
                 : 0f;
             return _config.baseStaminaPool + bonus;
         }
     }
     ```
   - In `Update()`: replace the hardcoded `_config.baseStaminaPool` regen cap with `MaxStamina`
   - In `OnGUI` overlay: replace `_config.baseStaminaPool` display denominator with `MaxStamina` (so it shows `80 / 105` not `80 / 100` after Endurance upgrade)

3. **`PlayerCombat.cs`** updated to make attack damage stat- and skill-aware:
   - Add `[SerializeField] private PlayerStats _playerStats` (optional — null warn, non-fatal)
   - Add `[SerializeField] private ProgressionConfigSO _progressionConfig` (optional — null warn, non-fatal)
   - Add `[SerializeField] private PlayerSkills _playerSkills` (optional — null warn, non-fatal)
   - In `Awake()`: warn (not error) if any of the three are null (using one `GameLog.Warn` call per missing ref)
   - Add private helper `ComputeEffectiveDamage()`:
     ```csharp
     private float ComputeEffectiveDamage()
     {
         float damage = _config.attackDamage;
         if (_playerStats != null && _progressionConfig != null)
             damage += (_playerStats.Strength - _progressionConfig.baseStrength)
                       * _progressionConfig.damagePerStrength;
         if (_playerSkills != null && _progressionConfig != null
             && _playerSkills.HasSkill("power_strike"))
             damage += _progressionConfig.powerStrikeDamageBonus;
         return damage;
     }
     ```
   - In `ExecuteHitDetection()`: replace `_config.attackDamage` with `ComputeEffectiveDamage()`
   - Update OnGUI overlay to show effective damage: add a line `$"DMG: {ComputeEffectiveDamage():F1}"` at y=410 (below existing labels)

4. **`ProgressionConfig.asset`** (`Assets/_Game/Data/Config/ProgressionConfig.asset`): assign the three new fields in Inspector — `damagePerStrength = 2`, `staminaPerEndurance = 5`, `powerStrikeDamageBonus = 10`

5. **Scene wiring in `TestScene.unity`**:
   - `StaminaSystem` component on **Player** GO: assign `_playerStats` → `PlayerStats` component on Player GO; assign `_progressionConfig` → `ProgressionConfig.asset`
   - `PlayerCombat` component on **Player** GO: assign `_playerStats` → `PlayerStats` on Player GO; assign `_progressionConfig` → `ProgressionConfig.asset`; assign `_playerSkills` → `PlayerSkills` on ProgressionSystem GO

6. **Edit Mode tests** at `Assets/Tests/EditMode/StatCombatEffectTests.cs` with ≥ 6 tests (pure logic, no MonoBehaviour):
   ```csharp
   private float ComputeDamage(float baseDamage, int strength, int baseStrength,
       float damagePerStrength, bool hasPowerStrike, float psBonus)
   {
       float d = baseDamage + (strength - baseStrength) * damagePerStrength;
       if (hasPowerStrike) d += psBonus;
       return d;
   }

   private float ComputeMaxStamina(float basePool, int endurance, int baseEndurance,
       float staminaPerEndurance)
       => basePool + (endurance - baseEndurance) * staminaPerEndurance;
   ```
   Tests:
   - `Damage_BaseStrength_NoBonusApplied()` — STR=5 base=5 → damage = baseDamage
   - `Damage_OnePointAboveBase_AddsCorrectBonus()` — STR=6 base=5 dps=2 → baseDamage + 2
   - `Damage_PowerStrike_AddsBonus()` — STR=5 hasPowerStrike=true psBonus=10 → baseDamage + 10
   - `Damage_StrengthPlusSkill_StacksCorrectly()` — STR=7 base=5 dps=2 psBonus=10 → baseDamage + 4 + 10
   - `Stamina_BaseEndurance_NoBonusApplied()` — END=5 base=5 → pool = basePool
   - `Stamina_OnePointAboveBase_AddsCorrectBonus()` — END=6 base=5 spe=5 → basePool + 5
   - `Stamina_MultipleEndurancePoints_ScalesLinearly()` — END=8 base=5 spe=5 → basePool + 15

7. No compile errors. All existing ≥ 96 Edit Mode tests pass. New total ≥ 103.

8. **Play Mode validation** (requires Unity Editor):
   - Start TestScene at default stats (STR=5, END=5) — attack enemy → deals 25 damage (`_config.attackDamage`, no bonus)
   - Upgrade Strength +1 at Trainer (cost: LP + gold) → attack enemy → deals 27 damage (25 + 2)
   - Read Power Strike tome → attack enemy → deals 37 damage (25 + 2 + 10)
   - Upgrade Endurance +1 at Trainer → stamina overlay shows `/105` max (was `/100`); regen caps at 105
   - Overlay at y=410 shows `DMG: 37.0` (or appropriate value for current stats)
   - Console: no NullReferenceExceptions from new code; existing warning about `_onLPChanged` is pre-existing

## Tasks / Subtasks

- [x] Task 1: Update `ProgressionConfigSO.cs` (AC: 1)
  - [x] 1.1 Add `[Header("Stat Combat Multipliers — Story 3.6")]` and three new float fields

- [x] Task 2: Update `StaminaSystem.cs` (AC: 2)
  - [x] 2.1 Add `[SerializeField] private PlayerStats _playerStats` and `[SerializeField] private ProgressionConfigSO _progressionConfig`
  - [x] 2.2 Add null-warns in `Awake()` (non-fatal)
  - [x] 2.3 Rewrite `MaxStamina` as a computed property using the bonus formula
  - [x] 2.4 Replace `_config.baseStaminaPool` regen cap in `Update()` with `MaxStamina`
  - [x] 2.5 Update `OnGUI` overlay denominator to `MaxStamina`

- [x] Task 3: Update `PlayerCombat.cs` (AC: 3)
  - [x] 3.1 Add three new `[SerializeField]` refs: `_playerStats`, `_progressionConfig`, `_playerSkills`
  - [x] 3.2 Add null-warns in `Awake()` (non-fatal)
  - [x] 3.3 Add `ComputeEffectiveDamage()` private helper method
  - [x] 3.4 Replace `_config.attackDamage` in `ExecuteHitDetection()` with `ComputeEffectiveDamage()`
  - [x] 3.5 Add `DMG: {ComputeEffectiveDamage():F1}` label at y=410 in OnGUI

- [x] Task 4: Update `ProgressionConfig.asset` (AC: 4)
  - [x] 4.1 Set `damagePerStrength = 2`, `staminaPerEndurance = 5`, `powerStrikeDamageBonus = 10`

- [x] Task 5: Wire scene (AC: 5)
  - [x] 5.1 Assign `_playerStats` and `_progressionConfig` on StaminaSystem in TestScene
  - [x] 5.2 Assign `_playerStats`, `_progressionConfig`, `_playerSkills` on PlayerCombat in TestScene

- [x] Task 6: Edit Mode tests (AC: 6, 7)
  - [x] 6.1 Create `Assets/Tests/EditMode/StatCombatEffectTests.cs` with ≥ 7 pure-logic tests
  - [x] 6.2 Run all tests — verify ≥ 103 total pass (103/103 passed)

- [ ] Task 7: Play Mode validation (AC: 8) — requires Unity Editor
  - [ ] 7.1 Verify base damage, stat-boosted damage, and Power Strike stacked damage
  - [ ] 7.2 Verify stamina pool reflects Endurance upgrade
  - [ ] 7.3 Verify no new NullReferenceExceptions in console

## Dev Notes

Story 3.6 is the final story of Epic 3. It "closes the loop" on the entire progression arc: XP gained from combat (3.1) → level-up (3.2) → learning points (3.3) → stat upgrades at trainer (3.4) → skill learned from tome (3.5) → **those upgrades now visibly affect combat** (3.6). The implementation is deliberate narrow scope: only two stat effects (Strength→damage, Endurance→stamina) and one skill effect (Power Strike). Full skill tree content is deferred post-Epic 3.

### Critical: Optional Refs Pattern (Non-Fatal Null Guards)

Unlike the fatal null-guards used for required dependencies (e.g. `CombatConfigSO` — disables the component), the new `_playerStats`, `_progressionConfig`, `_playerSkills` refs are **optional**. If missing, combat still works at base values. This matches the "graceful degradation" principle: the component must not break when run without progression context.

Pattern:
```csharp
// In Awake() — WARN only, never error+disable:
if (_playerStats == null)
    GameLog.Warn(TAG, "PlayerStats not assigned — Strength damage bonus inactive");
if (_progressionConfig == null)
    GameLog.Warn(TAG, "ProgressionConfigSO not assigned — all stat bonuses inactive");
if (_playerSkills == null)
    GameLog.Warn(TAG, "PlayerSkills not assigned — Power Strike bonus inactive");
```

### Critical: `MaxStamina` Property Must Be Computed Every Call

Do NOT cache the max stamina result in a field. Stats can change mid-session (trainer visit). The `MaxStamina` property must read from `_playerStats.Endurance` live each time it is called. Unity calls `Update()` every frame — the regen cap will always reflect the current stat value without extra event wiring.

```csharp
// Update() — correct pattern after this story:
if (_currentStamina < MaxStamina)  // was: _config.baseStaminaPool
{
    _currentStamina = Mathf.Min(
        _currentStamina + _config.staminaRegenRate * Time.deltaTime,
        MaxStamina);  // was: _config.baseStaminaPool
}
```

### Critical: `ComputeEffectiveDamage()` Called Twice Per Attack

`ComputeEffectiveDamage()` is called both in `ExecuteHitDetection()` (for actual damage) and in `OnGUI()` (for display). Since it's a lightweight float calculation (no allocations, no scene queries), two calls per frame is fine. Do NOT cache its return value or make it a property — it must stay a method to match the "only call when needed" pattern and avoid confusion about recalculation frequency.

### Critical: Cross-System References — Prototype Scope

`StaminaSystem` and `PlayerCombat` are in `namespace Game.Combat` (`Scripts/Combat/`). They will now hold direct refs to:
- `PlayerStats` (`namespace Game.Player`, `Scripts/Player/`) — different system
- `PlayerSkills` (`namespace Game.Progression`, `Scripts/Progression/`) — different system
- `ProgressionConfigSO` (`namespace Game.Progression`) — config SO, acceptable per architecture Decision 7

These are **prototype pragmatism exceptions**, same as TrainerNPC → PlayerStats in Story 3.4. Document as tech debt. For full architecture compliance these would be replaced by querying a `StatBonusProvider` interface or event-driven caching. For Epic 3 scope, direct refs are correct.

Cross-namespace using directives required in both files:
```csharp
using Game.Player;       // for PlayerStats
using Game.Progression;  // for ProgressionConfigSO, PlayerSkills
```

### Critical: `PlayerStats` Is on the Player GO — Same GO as `StaminaSystem` and `PlayerCombat`

From Story 3.4 dev notes, `PlayerStats` is on the **Player GO** alongside `PlayerController`, `PlayerAnimator`, `PlayerHealth`, `PlayerCombat`, `StaminaSystem`. This means `StaminaSystem` and `PlayerCombat` can fetch `PlayerStats` via `GetComponent<PlayerStats>()` in Awake. However, use `[SerializeField]` inspector wiring (not `GetComponent`) for consistency with the project's explicit-assignment pattern and to keep the dependency visible.

`PlayerSkills` is on the **ProgressionSystem GO** (different scene object), so it MUST be a `[SerializeField]` ref wired in Inspector.

### Critical: Power Strike Magic String Coupling

`PlayerCombat.ComputeEffectiveDamage()` checks `_playerSkills.HasSkill("power_strike")` using the literal skillId string. This couples PlayerCombat to the specific skill asset defined in Story 3.5. For prototype scope with one skill, this is acceptable. Do NOT extract a constant (`SKILL_POWER_STRIKE`) — that over-engineers a prototype feature. Document the coupling in comments:

```csharp
// Prototype: hardcoded skill ID "power_strike" defined in SkillSO (Story 3.5).
// Full system would query skill effects generically via a SkillEffectRegistry.
if (_playerSkills.HasSkill("power_strike"))
    damage += _progressionConfig.powerStrikeDamageBonus;
```

### Critical: `ProgressionConfigSO` Is in `namespace Game.Progression`

`StaminaSystem.cs` currently has no `using Game.Progression` directive. `PlayerCombat.cs` also lacks it. Both files need it added for the new fields. Check existing using blocks before adding — do not duplicate.

### Critical: `baseStrength` and `baseEndurance` Are Fields on `ProgressionConfigSO`

The bonus formulas use `_progressionConfig.baseStrength` (Story 3.4 field) and `_progressionConfig.baseEndurance` (Story 3.4 field). These are `int` values (default 5). The formula `(currentStat - baseStat) * multiplier` correctly returns 0 at base, positive at trained-up. Note: if a trainer ever reduced stats below base (not currently possible), the formula would yield negative damage — that's mathematically correct and not a bug.

### OnGUI Overlay Stack After This Story

```
[y=50]   Stamina: 80 / 105                                                  ← StaminaSystem (MaxStamina now stat-aware)
[y=70]   Combat: [Ready]                                                    ← PlayerCombat
[y=100]  Combo: step 0 | closed                                             ← PlayerCombat
[y=130]  Block: lowered | PB: closed                                        ← PlayerCombat
[y=160]  State: Airborne:False | Blocking:False | Attacking:False           ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                       ← DodgeController
[y=220]  Enemy: Patrolling | PlayerDist:10.2m | ...                         ← EnemyBrain
[y=250]  PlayerHP: 85/100 | Dead:False                                      ← PlayerHealth
[y=270]  EnemyHP: 50/50 | Dead:False                                        ← EnemyHealth
[y=290]  XP: 50 | Kills: 1                                                  ← XPSystem
[y=310]  Level: 1 / 6 | Next LvUp: 100 XP                                  ← LevelSystem
[y=330]  LP: 0                                                              ← LearningPointSystem
[y=350]  STR:6 DEX:5 END:6 MNA:5                                           ← PlayerStats (values after trainer visit)
[y=370]  Gold: 400                                                          ← GoldSystem
[y=390]  Skills: 1 learned                                                  ← PlayerSkills
[y=410]  DMG: 37.0                                                          ← PlayerCombat (NEW — effective damage display)
```

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs   ← Add 3 new float fields
Assets/_Game/Scripts/Combat/StaminaSystem.cs                   ← MaxStamina now dynamic, 2 new SerializeField refs
Assets/_Game/Scripts/Combat/PlayerCombat.cs                    ← ComputeEffectiveDamage(), 3 new SerializeField refs
Assets/_Game/Data/Config/ProgressionConfig.asset               ← Set new field values in Inspector
Assets/_Game/Scenes/TestScene.unity                            ← Wire new refs on StaminaSystem + PlayerCombat
```

**Files to CREATE:**
```
Assets/Tests/EditMode/StatCombatEffectTests.cs                 ← NEW Edit Mode tests (≥7 tests)
Assets/Tests/EditMode/StatCombatEffectTests.cs.meta
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Player/PlayerStats.cs                     ← unchanged; already exposes Strength/Endurance/etc.
Assets/_Game/Scripts/Progression/PlayerSkills.cs               ← unchanged; HasSkill() is the only API needed
Assets/_Game/ScriptableObjects/Skills/SkillSO.cs               ← unchanged
Assets/_Game/Scripts/AI/EnemyHealth.cs                         ← TakeDamage(float) signature unchanged
Assets/_Game/Scripts/Player/PlayerHealth.cs                    ← unchanged
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs        ← unchanged; attackDamage, baseStaminaPool stay as base values
```

**Existing damage call chain:**
```
PlayerCombat.TryAttack()
  → ExecuteHitDetection()
    → Physics.OverlapSphereNonAlloc(transform.position, _config.attackHitRange, _hitBuffer)
    → for each hit: health.TakeDamage(ComputeEffectiveDamage())   ← CHANGED from _config.attackDamage
```

**Existing stamina regen chain:**
```
StaminaSystem.Update()
  → if _regenCooldown <= 0 and _currentStamina < MaxStamina  ← CHANGED from _config.baseStaminaPool
    → _currentStamina = Min(_currentStamina + regen, MaxStamina)  ← CHANGED
```

### Previous Story Learnings (Story 3.5)

- **`SkillSO.skillId` is a property getter** (post-code-review change): `public string skillId => _skillId;` — call it as `skill.skillId`, which `PlayerSkills.HasSkill()` uses as the key. `PlayerCombat` checks `HasSkill("power_strike")` against the same HashSet.
- **`PlayerSkills.HasSkill(string)` is confirmed live** at `Scripts/Progression/PlayerSkills.cs:36` — pure HashSet.Contains lookup, safe to call from Update()/per-frame code.
- **`LearningPointSystem.CurrentLP`** is a property getter — accessible from cross-system code if needed.
- **96/96 Edit Mode tests pass** as of Story 3.5 completion. New story must not regress any of them.
- **`ProgressionSystem` GO** in TestScene has: XPSystem, LevelSystem, LearningPointSystem, GoldSystem, PlayerSkills — PlayerSkills is the ref to wire to PlayerCombat._playerSkills.
- **`Player` GO** in TestScene has: PlayerController, PlayerAnimator, CameraController, PlayerHealth, PlayerStats, PlayerCombat, StaminaSystem — all these components coexist on one root GO.

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ Only modifying existing files in correct folders |
| GameLog only — no Debug.Log | ✅ `GameLog.Warn` for optional ref warnings; no new Debug.Log |
| Null-guard in Awake | ✅ All new optional refs get null-warn in Awake |
| Config SOs for tunable values | ✅ `damagePerStrength`, `staminaPerEndurance`, `powerStrikeDamageBonus` in ProgressionConfigSO |
| No magic numbers in logic | ✅ All multipliers in ProgressionConfigSO; string `"power_strike"` documented as prototype coupling |
| Cross-system direct refs — prototype exception | ⚠️ StaminaSystem+PlayerCombat → PlayerStats+PlayerSkills — documented tech debt, same precedent as Story 3.4 |
| `MaxStamina` dynamically computed | ✅ Reads live from `_playerStats.Endurance` each call — no stale cache |
| GUIStyle cached | ✅ Existing `_guiStyle` already cached in both modified components |
| OnGUI #if guard | ✅ New `DMG` label added inside existing `#if DEVELOPMENT_BUILD || UNITY_EDITOR` block |

### References

- Epic 3 story 6 scope — "my stats affect combat outcomes (Strength → damage, Endurance → stamina pool)": [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- `PlayerCombat.ExecuteHitDetection()` — current hardcoded `_config.attackDamage` call: [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs:277]
- `StaminaSystem.MaxStamina` — currently returns `_config.baseStaminaPool` (static): [Source: Assets/_Game/Scripts/Combat/StaminaSystem.cs:24]
- `StaminaSystem.Update()` — regen cap at `_config.baseStaminaPool`: [Source: Assets/_Game/Scripts/Combat/StaminaSystem.cs:48-53]
- `CombatConfigSO.attackDamage = 25f`, `baseStaminaPool = 100f`: [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs:47,14]
- `ProgressionConfigSO.baseStrength = 5`, `baseEndurance = 5`: [Source: Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs:27,29]
- `PlayerStats.Strength`, `PlayerStats.Endurance` (public properties): [Source: Assets/_Game/Scripts/Player/PlayerStats.cs:21-24]
- `PlayerSkills.HasSkill(string)` (HashSet.Contains): [Source: Assets/_Game/Scripts/Progression/PlayerSkills.cs:36]
- `SkillSO.skillId` property getter: [Source: Assets/_Game/ScriptableObjects/Skills/SkillSO.cs:17]
- Architecture — same-system comms: direct refs acceptable within same Scripts/[System]/ folder: [Source: _bmad-output/project-context.md#Architecture Patterns]
- Architecture — cross-system refs: prototype exception pattern established in Story 3.4: [Source: _bmad-output/implementation-artifacts/3-4-trainer-stat-upgrade.md#Dev Notes]
- Architecture — config SOs for all tunable values: [Source: _bmad-output/game-architecture.md#Decision 4]
- Architecture — all tunable values in per-system config SO: [Source: _bmad-output/project-context.md#Config SOs]
- project-context.md — NEVER use Debug.Log, use GameLog: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- CLAUDE.md — Enemy_Grunt structure: CapsuleCollider on Visual child, EnemyHealth on root; use GetComponentInParent: [Source: CLAUDE.md#Enemy Prefab Structure (Enemy_Grunt)]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

_None_

### Completion Notes List

- Implemented `ProgressionConfigSO` 3 new float fields: `damagePerStrength=2`, `staminaPerEndurance=5`, `powerStrikeDamageBonus=10`.
- `StaminaSystem.MaxStamina` is now a computed property reading live from `_playerStats.Endurance`; regen cap and OnGUI overlay both use it dynamically.
- `PlayerCombat.ComputeEffectiveDamage()` accumulates base damage + Strength bonus + Power Strike skill bonus; called in `ExecuteHitDetection()` and `OnGUI`.
- Scene wired: StaminaSystem and PlayerCombat on Player GO both receive `_playerStats` (PlayerStats on Player) and `_progressionConfig` (ProgressionConfig.asset); PlayerCombat also receives `_playerSkills` (PlayerSkills on ProgressionSystem GO).
- 7 new pure-logic Edit Mode tests in `StatCombatEffectTests.cs`. All 103/103 tests pass — no regressions.
- Task 7 (Play Mode validation) requires manual Unity Editor verification.
- **Code review fixes (2026-03-13):** (1) `StaminaSystem.OnGUI` — added cached `_guiStyle` field to fix per-frame GC allocation. (2) `StaminaSystem.Awake()` — init `_currentStamina = MaxStamina` instead of `baseStaminaPool` for save/load consistency. (3) `StaminaSystem.Update()` — cache `MaxStamina` to single call per regen tick. (4) `MaxStamina` getter — `Mathf.Max(0f, bonus)` clamp prevents future debuff system from reducing pool below base. (5) `ComputeEffectiveDamage()` — same `Mathf.Max(0f, ...)` clamp on Strength bonus. (6) Added TODO(Epic4-tech-debt) comments on cross-system refs in both files. (7) Added 2 below-base stat edge-case tests (total: 9 tests in `StatCombatEffectTests.cs`).

### File List

- `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` — modified (3 new fields)
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs` — modified (MaxStamina computed, 2 new SerializeField refs, OnGUI updated)
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` — modified (ComputeEffectiveDamage(), 3 new SerializeField refs, OnGUI label at y=410)
- `Assets/_Game/Data/Config/ProgressionConfig.asset` — modified (new field values set)
- `Assets/_Game/Scenes/TestScene.unity` — modified (scene refs wired)
- `Assets/Tests/EditMode/StatCombatEffectTests.cs` — created (7 pure-logic tests)
- `Assets/Tests/EditMode/StatCombatEffectTests.cs.meta` — created by Unity

### Change Log

- 2026-03-13: Story 3.6 implemented — Strength→damage and Endurance→stamina stat effects, Power Strike skill damage bonus. 7 new Edit Mode tests. 103/103 pass.
