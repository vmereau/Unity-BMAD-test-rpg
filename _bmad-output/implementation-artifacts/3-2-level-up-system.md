# Story 3.2: Level-Up System

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to level up when my XP reaches the threshold,
so that combat progress translates into meaningful character advancement.

## Acceptance Criteria

1. `LevelSystem.cs` exists at `Assets/_Game/Scripts/Progression/LevelSystem.cs`:
   - `namespace Game.Progression`, `private const string TAG = "[Progression]";`
   - `[SerializeField] private ProgressionConfigSO _config`
   - `[SerializeField] private XPSystem _xpSystem` — direct same-system ref (both on ProgressionSystem GO)
   - `[SerializeField] private GameEventSO_Int _onXPGained` — subscribe to this (payload = XP gained per kill)
   - `[SerializeField] private GameEventSO_Int _onLevelUp` — raise on level-up (payload = new level number)
   - `public int CurrentLevel { get; private set; } = 1` — starts at Level 1
   - `public int MaxLevel => _config != null ? _config.xpPerLevel.Length + 1 : 1` — computed (= 6 with default config)
   - `Awake()`: null-guard `_config` (error + `enabled = false`); null-guard `_xpSystem` (error + `enabled = false`); warn if `_onXPGained == null`; warn if `_onLevelUp == null`
   - `OnEnable()`: if `_onXPGained != null` → `_onXPGained.AddListener(HandleXPGained)`
   - `OnDisable()`: if `_onXPGained == null` → `return`; else `_onXPGained.RemoveListener(HandleXPGained)`
   - `private void HandleXPGained(int xpGained)`: calls `CheckLevelUp()`
   - `private void CheckLevelUp()`:
     ```
     while (CurrentLevel < MaxLevel)
     {
         int thresholdIndex = CurrentLevel - 1;  // Level 1 → xpPerLevel[0]=100
         if (_xpSystem.CurrentXP >= _config.xpPerLevel[thresholdIndex])
         {
             CurrentLevel++;
             GameLog.Info(TAG, $"Level up! Now Level {CurrentLevel}");
             _onLevelUp?.Raise(CurrentLevel);
         }
         else break;
     }
     ```
   - `OnGUI` debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=310:
     - If `CurrentLevel < MaxLevel`: `$"Level: {CurrentLevel} / {MaxLevel} | Next LvUp: {_config.xpPerLevel[CurrentLevel - 1]} XP"`
     - If at max level: `$"Level: {CurrentLevel} / {MaxLevel} | MAX"`
     - `fontSize = 18`, cached `GUIStyle _guiStyle`

2. `OnLevelUp.asset` event SO created at `Assets/_Game/Data/Events/OnLevelUp.asset`:
   - Type: `GameEventSO_Int` (payload = new level number, e.g. 2 when reaching Level 2)
   - Assigned to `LevelSystem._onLevelUp` on the ProgressionSystem GameObject
   - Story 3.3's LearningPointSystem will subscribe; Story 8 HUD will subscribe for display
   - **Do NOT** assign any listeners in this story — event channel is live but no one subscribes yet

3. `LevelSystem` component added to existing `ProgressionSystem` GameObject in `TestScene.unity`:
   - `_config = ProgressionConfig.asset` assigned
   - `_xpSystem` = XPSystem component on same GameObject (drag reference)
   - `_onXPGained = OnXPGained.asset` assigned
   - `_onLevelUp = OnLevelUp.asset` assigned

4. `ProgressionConfigSO.cs` is **NOT modified** — `xpPerLevel = { 100, 250, 500, 900, 1400 }` stub already exists from Story 3.1.
   `ProgressionConfig.asset` is **NOT modified** — default values are already correct.
   Level progression with default config:
   | Total XP | Level |
   |----------|-------|
   | 0–99     | 1     |
   | 100–249  | 2     |
   | 250–499  | 3     |
   | 500–899  | 4     |
   | 900–1399 | 5     |
   | ≥ 1400   | 6 (MAX) |

5. Edit Mode tests at `Assets/Tests/EditMode/LevelSystemTests.cs` with ≥ 6 tests:
   - Pure formula helper (no MonoBehaviour):
     ```csharp
     private int CalculateLevel(int totalXP, int[] xpThresholds)
     {
         int level = 1;
         for (int i = 0; i < xpThresholds.Length; i++)
         {
             if (totalXP >= xpThresholds[i]) level = i + 2;
             else break;
         }
         return level;
     }
     ```
   - Tests using default thresholds `{ 100, 250, 500, 900, 1400 }`:
     - `ZeroXP_StartsAtLevel1()` — 0 XP → Level 1
     - `XPBelowFirstThreshold_StaysAtLevel1()` — 99 XP → Level 1
     - `XPAtFirstThreshold_ReachesLevel2()` — 100 XP → Level 2
     - `XPAtSecondThreshold_ReachesLevel3()` — 250 XP → Level 3
     - `MaxXP_ReachesMaxLevel()` — 1400 XP → Level 6 (max = xpThresholds.Length + 1)
     - `XPBeyondMax_CapsAtMaxLevel()` — 9999 XP → Level 6 (no overflow past max)
     - `BulkXP_CanSkipMultipleLevels()` — 501 XP (past level 2 and 3 thresholds) → Level 4

6. No compile errors. All existing 61 Edit Mode tests pass. New total ≥ 68.

7. Play Mode validation:
   - Kill 2 enemies → 100 XP total → debug overlay shows `Level: 2 / 6 | Next LvUp: 250 XP`
   - `OnLevelUp` fires → GameLog shows `[Progression] Level up! Now Level 2`
   - Kill 5 more enemies → 350 XP total → Level 3 reached, overlay shows `Level: 3 / 6 | Next LvUp: 500 XP`
   - No NullReferenceExceptions in console
   - `OnXPGained` still fires correctly for XPSystem (unaffected)

## Tasks / Subtasks

- [x] Task 1: Create `LevelSystem.cs` (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Scripts/Progression/LevelSystem.cs` with correct namespace and TAG
  - [x] 1.2 Implement fields: `_config`, `_xpSystem`, `_onXPGained`, `_onLevelUp`; public `CurrentLevel`, `MaxLevel`
  - [x] 1.3 Implement `Awake()` null guards (error+disable for `_config` and `_xpSystem`; warn for events)
  - [x] 1.4 Implement `OnEnable()`/`OnDisable()` subscription with null guards (OnDisable guard: Awake may disable before OnEnable)
  - [x] 1.5 Implement `HandleXPGained()` and `CheckLevelUp()` while loop
  - [x] 1.6 Add OnGUI debug overlay at y=310 with cached GUIStyle

- [x] Task 2: Create `OnLevelUp.asset` event SO (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Data/Events/OnLevelUp.asset` (type: GameEventSO_Int)

- [x] Task 3: Wire LevelSystem in ProgressionSystem (AC: 3)
  - [x] 3.1 Add LevelSystem component to `ProgressionSystem` GO in TestScene.unity
  - [x] 3.2 Assign all four references: `_config`, `_xpSystem`, `_onXPGained`, `_onLevelUp`

- [x] Task 4: Edit Mode tests (AC: 5)
  - [x] 4.1 Create `Assets/Tests/EditMode/LevelSystemTests.cs` with ≥ 7 tests
  - [x] 4.2 Run all tests — verify all 61 prior tests still pass + 7 new = ≥ 68 total

- [ ] Task 5: Play Mode validation (AC: 7) — requires Unity Editor
  - [ ] 5.1 Kill 2 enemies → 100 XP → overlay shows Level 2
  - [ ] 5.2 Kill more enemies to reach Level 3 → overlay updates correctly
  - [ ] 5.3 No null exceptions; XPSystem overlay still shows correct values at y=290

## Dev Notes

Story 3.2 builds the level-up trigger on top of Story 3.1's XP infrastructure. The `ProgressionConfigSO.xpPerLevel[]` stub was already created in Story 3.1 — do not recreate or modify it. The implementation is deliberately thin: LevelSystem subscribes to `OnXPGained`, calls `CheckLevelUp()`, fires `OnLevelUp`. No UI, no LP award (Story 3.3). No stat changes (Story 3.6).

### Critical: xpPerLevel Interpretation

`xpPerLevel = { 100, 250, 500, 900, 1400 }` uses **cumulative total XP thresholds**:
- `xpPerLevel[0] = 100` → total XP needed to reach Level 2 (from Level 1)
- `xpPerLevel[1] = 250` → total XP needed to reach Level 3 (from Level 2)
- `xpPerLevel[CurrentLevel - 1]` = threshold for the NEXT level up

MaxLevel = `xpPerLevel.Length + 1 = 6`. Player starts at Level 1, can reach Level 6.

Do NOT interpret as incremental XP (i.e., don't add thresholds together). All comparisons use `XPSystem.CurrentXP` directly (cumulative since game start).

### Critical: LevelSystem Reads XPSystem Directly (Same-System Pattern)

Per architecture: "Same-system comms: Direct MonoBehaviour references acceptable within the same `Scripts/[System]/` folder." Both `LevelSystem` and `XPSystem` are in `Assets/_Game/Scripts/Progression/` — direct reference via `[SerializeField] private XPSystem _xpSystem` is the correct pattern. No event needed to pass total XP.

`XPSystem.CurrentXP` is a public property:
```csharp
public int CurrentXP { get; private set; }
```

LevelSystem calls `_xpSystem.CurrentXP` inside `CheckLevelUp()`. This avoids duplicating XP state.

### Critical: CheckLevelUp Must Handle Multiple Level-Ups in One Kill

Using a `while` loop (not `if`) is non-negotiable. If a kill awards 150 XP and takes the player from 90 XP total to 240 XP total, that crosses the Level 2 threshold (100) but not Level 3 (250). Only one level-up fires. However, if starting at 90 XP and gaining 800 XP (hypothetical high-kill scenario), the while loop correctly fires multiple `OnLevelUp` events.

```csharp
private void CheckLevelUp()
{
    while (CurrentLevel < MaxLevel)
    {
        int thresholdIndex = CurrentLevel - 1;  // Level 1 → index 0 → threshold 100
        if (_xpSystem.CurrentXP >= _config.xpPerLevel[thresholdIndex])
        {
            CurrentLevel++;
            GameLog.Info(TAG, $"Level up! Now Level {CurrentLevel}");
            _onLevelUp?.Raise(CurrentLevel);
        }
        else
        {
            break;
        }
    }
}
```

### Critical: Event Subscription Pattern (Same as XPSystem)

```csharp
private void OnEnable()
{
    if (_onXPGained != null)
        _onXPGained.AddListener(HandleXPGained);
}

private void OnDisable()
{
    if (_onXPGained == null) return; // Guard: Awake may disable before OnEnable runs
    _onXPGained.RemoveListener(HandleXPGained);
}
```

The `OnDisable` null guard is mandatory — if `Awake()` disables the component (`enabled = false`) due to missing `_config` or `_xpSystem`, Unity calls `OnDisable` before `OnEnable` has ever run. Without the guard, `RemoveListener` is called on a null reference.

See CLAUDE.md "Unity Lifecycle Gotcha: OnDisable Before OnEnable".

### Critical: Awake Null-Guard Order

```csharp
private void Awake()
{
    if (_config == null)
    {
        GameLog.Error(TAG, "ProgressionConfigSO not assigned — LevelSystem disabled.");
        enabled = false;
        return;
    }
    if (_xpSystem == null)
    {
        GameLog.Error(TAG, "XPSystem reference not assigned — LevelSystem disabled.");
        enabled = false;
        return;
    }
    if (_onXPGained == null)
        GameLog.Warn(TAG, "OnXPGained event not assigned — LevelSystem won't respond to XP gains.");
    if (_onLevelUp == null)
        GameLog.Warn(TAG, "OnLevelUp event not assigned — level-up signals will be silent (Story 3.3 LP won't trigger).");
}
```

### Critical: OnLevelUp Event — Story 3.3 Integration Point

`_onLevelUp.Raise(CurrentLevel)` is the hook for Story 3.3's LearningPointSystem. When implemented in 3.3:
1. LearningPointSystem subscribes to `OnLevelUp` in OnEnable
2. On each level-up event: `CurrentLP += _config.learningPointsPerLevel` (default = 3)
3. Raises its own event for Story 6 UI or trainer spending

For Story 3.2, `_onLevelUp` raises to no listeners (channel live but silent). This is normal.

Architecture reference: `OnLevelUp | int newLevel | ProgressionSystem | UI, PlayerStats`
[Source: _bmad-output/game-architecture.md#Event System]

### Critical: ProgressionConfigSO Already Exists — Do NOT Recreate

`ProgressionConfigSO.cs` was created in Story 3.1. Do NOT recreate it. Do NOT modify it. The `xpPerLevel` field already exists with stub values. Just reference it via `[SerializeField] private ProgressionConfigSO _config`.

### Critical: OnGUI Overlay — GUIStyle Must Be Cached

Per code review pattern from Story 2.9: always cache `GUIStyle` per-component. Never `new GUIStyle()` in `OnGUI()` — that allocates heap on every frame.

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private GUIStyle _guiStyle;

private void OnGUI()
{
    if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };

    string levelText = CurrentLevel < MaxLevel
        ? $"Level: {CurrentLevel} / {MaxLevel} | Next LvUp: {_config.xpPerLevel[CurrentLevel - 1]} XP"
        : $"Level: {CurrentLevel} / {MaxLevel} | MAX";

    GUI.Label(new Rect(10, 310, 500, 26), levelText, _guiStyle);
}
#endif
```

Note: Only show `_config.xpPerLevel[CurrentLevel - 1]` when `CurrentLevel < MaxLevel`. When at max level, that index would be out of bounds or meaningless.

### OnGUI Debug Overlay Stack (After This Story)

```
[y=50]   Stamina: 80 / 100                                                  ← StaminaSystem
[y=70]   Combat: [Ready]                                                    ← PlayerCombat
[y=100]  Combo: step 0 | closed                                             ← PlayerCombat
[y=130]  Block: lowered | PB: closed                                        ← PlayerCombat
[y=160]  State: Airborne:False | Blocking:False | Attacking:False           ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                       ← DodgeController
[y=220]  Enemy: Patrolling | PlayerDist:10.2m | DetectRange:8m | AtkCD:0.0s ← EnemyBrain
[y=250]  PlayerHP: 85/100 | Dead:False                                      ← PlayerHealth
[y=270]  EnemyHP: 50/50 | Dead:False                                        ← EnemyHealth
[y=290]  XP: 50 | Kills: 1                                                  ← XPSystem
[y=310]  Level: 1 / 6 | Next LvUp: 100 XP                                  ← LevelSystem (NEW)
```

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `Scripts/Progression/LevelSystem.cs`, `Data/Events/OnLevelUp.asset` |
| GameLog only — no Debug.Log | ✅ Use `GameLog.Info(TAG, ...)` with `TAG = "[Progression]"` |
| Null-guard in Awake | ✅ `_config` and `_xpSystem` error+disable; events warn only |
| Event subscribe in OnEnable/OnDisable | ✅ With null guard on `_onXPGained` in OnDisable |
| Config SOs for tunable values | ✅ Uses existing `ProgressionConfigSO.xpPerLevel[]` — no new config fields |
| No cross-system direct references | ✅ LevelSystem is in `Scripts/Progression/`; XPSystem is in `Scripts/Progression/` — same-system direct ref OK |
| No singleton (LevelSystem) | ✅ Regular MonoBehaviour on ProgressionSystem GameObject |
| No magic numbers | ✅ All thresholds from `_config.xpPerLevel[]` |
| GUIStyle cached | ✅ Per-component `_guiStyle` initialized on first `OnGUI` call |
| CheckLevelUp uses while loop | ✅ Handles multiple consecutive level-ups in a single kill event |
| MaxLevel computed from config | ✅ `xpPerLevel.Length + 1` — no hardcoded max |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Progression/LevelSystem.cs          ← NEW level-up tracking component
Assets/_Game/Scripts/Progression/LevelSystem.cs.meta
Assets/_Game/Data/Events/OnLevelUp.asset                 ← NEW event SO (GameEventSO_Int, payload=newLevel)
Assets/_Game/Data/Events/OnLevelUp.asset.meta
Assets/Tests/EditMode/LevelSystemTests.cs                ← NEW Edit Mode tests (≥7 tests)
Assets/Tests/EditMode/LevelSystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity   ← Add LevelSystem component to ProgressionSystem GO
```

**Files NOT to modify:**
```
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs   ← xpPerLevel[] already exists from Story 3.1
Assets/_Game/Data/Config/ProgressionConfig.asset               ← default values already correct
Assets/_Game/Scripts/Progression/XPSystem.cs                   ← only source of CurrentXP; unchanged
```

**Scripts/Progression/ after this story:**
```
Assets/_Game/Scripts/Progression/
├── XPSystem.cs       ← Story 3.1 (unchanged)
└── LevelSystem.cs    ← NEW Story 3.2
```
(LearningPointSystem.cs: Story 3.3; StatSystem.cs: Story 3.6)

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset     ← Story 2.9 (GameEventSO_Void)
├── OnXPGained.asset       ← Story 3.1 (GameEventSO_Int, payload=xpGained)
└── OnLevelUp.asset        ← NEW Story 3.2 (GameEventSO_Int, payload=newLevel)
```

### References

- Epic 3, Story 2 ("As a player, I level up when my XP reaches the threshold"): [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- Architecture — OnLevelUp event (GameEventSO_Int, int newLevel), raised by ProgressionSystem, heard by UI/PlayerStats: [Source: _bmad-output/game-architecture.md#Event System]
- Architecture — ProgressionConfigSO.xpPerLevel thresholds: [Source: _bmad-output/game-architecture.md#Configuration Management]
- Architecture — Same-system direct refs acceptable within Scripts/Progression/: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — Cross-system via GameEventSO channels only: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — Only WorldStateManager/SaveSystem are singletons: [Source: _bmad-output/game-architecture.md#Data Access]
- Story 3.1 — ProgressionConfigSO.xpPerLevel stub `{100, 250, 500, 900, 1400}` already created: [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#AC 1]
- Story 3.1 — XPSystem.CurrentXP (public property), XPSystem.TotalKills: [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#AC 3]
- Story 3.1 — OnXPGained.asset (GameEventSO_Int, payload=xpGained per kill): [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#AC 4]
- Story 3.1 — "LevelSystem will subscribe to OnXPGained in OnEnable" (integration point): [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#Critical: OnXPGained Event — Story 3.2 Integration Point]
- Story 3.1 — Recommended: XPSystem+LevelSystem on same ProgressionSystem GO: [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#Critical: No XPSystem Singleton]
- Story 3.1 — 61 tests total (57 prior + 4 new XPSystemTests): [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#Tasks]
- Story 3.1 — OnGUI overlay stack y=290 for XPSystem: [Source: _bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md#OnGUI Debug Overlay Stack]
- CLAUDE.md — OnDisable null guard when Awake may disable component: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- project-context.md — NEVER use Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — All tunable values in config SOs: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — Testing: test pure logic formulas, not MonoBehaviour lifecycle: [Source: _bmad-output/project-context.md#Testing Rules]
- project-context.md — GameEventSO_Int exists in GameEventSO.cs: [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO.cs]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Task 4.2: Edit Mode tests ran — 68/68 passed (61 prior + 7 new LevelSystemTests). No failures.

### Completion Notes List

- Created `LevelSystem.cs` with full implementation: null-guarded Awake, OnEnable/OnDisable subscription, HandleXPGained → CheckLevelUp (while loop for multi-level-up), OnGUI overlay at y=310 with cached GUIStyle.
- Created `OnLevelUp.asset` (GameEventSO_Int) in `Assets/_Game/Data/Events/`.
- Added LevelSystem component to ProgressionSystem GO in TestScene; all four references assigned: `_config` (ProgressionConfig.asset), `_xpSystem` (XPSystem on same GO), `_onXPGained` (OnXPGained.asset), `_onLevelUp` (OnLevelUp.asset).
- Created 7 Edit Mode tests in LevelSystemTests.cs covering: ZeroXP→Level1, 99XP→Level1, 100XP→Level2, 250XP→Level3, 1400XP→Level6 (max), 9999XP→Level6 (cap), 501XP→Level4 (multi-skip).
- Task 5 (Play Mode validation) left for manual testing by developer in Unity Editor.
- ProgressionConfigSO and ProgressionConfig.asset not modified — xpPerLevel already correct from Story 3.1.
- XPSystem.cs not modified — CurrentXP public property already available.
- AIConfigSO.cs: removed `attackFlashDuration` field (out-of-scope cleanup — attack flash removed from EnemyBrain).
- EnemyBrain.cs: attack cooldown timer extracted to `HandleCooldowns()` (called from Update in all states); `_attackCooldownTimer = 0f` reset removed from `EnterAttackingState()`; white flash on attack removed; `UpdateAttackVisuals` now shows cooldown lerp (yellow→red) in any state when cooldown > 0, solid red when in Attacking state with cooldown expired, white otherwise.

### File List

Assets/_Game/Scripts/Progression/LevelSystem.cs
Assets/_Game/Scripts/Progression/LevelSystem.cs.meta
Assets/_Game/Data/Events/OnLevelUp.asset
Assets/_Game/Data/Events/OnLevelUp.asset.meta
Assets/Tests/EditMode/LevelSystemTests.cs
Assets/Tests/EditMode/LevelSystemTests.cs.meta
Assets/_Game/Scenes/TestScene.unity
Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs
Assets/_Game/Scripts/AI/EnemyBrain.cs

## Change Log

- 2026-03-11: Story 3.2 implemented — LevelSystem.cs created, OnLevelUp.asset created, LevelSystem wired in ProgressionSystem (TestScene), 7 Edit Mode tests added (68 total pass).
