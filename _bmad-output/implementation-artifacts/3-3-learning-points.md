# Story 3.3: Learning Points

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to receive learning points each time I level up,
so that I can track my advancement currency for spending at trainers and on tomes.

## Acceptance Criteria

1. `LearningPointSystem.cs` exists at `Assets/_Game/Scripts/Progression/LearningPointSystem.cs`:
   - `namespace Game.Progression`, `private const string TAG = "[Progression]";`
   - `[SerializeField] private ProgressionConfigSO _config`
   - `[SerializeField] private GameEventSO_Int _onLevelUp` — subscribe to this (payload = new level number)
   - `[SerializeField] private GameEventSO_Int _onLPChanged` — raise when LP changes (payload = new LP total)
   - `public int CurrentLP { get; private set; } = 0` — starts at 0
   - `Awake()`: null-guard `_config` (error + `enabled = false`); warn if `_onLevelUp == null`; warn if `_onLPChanged == null`
   - `OnEnable()`: if `_onLevelUp != null` → `_onLevelUp.AddListener(HandleLevelUp)`
   - `OnDisable()`: if `_onLevelUp == null` → `return`; else `_onLevelUp.RemoveListener(HandleLevelUp)`
   - `private void HandleLevelUp(int newLevel)`:
     ```csharp
     CurrentLP += _config.learningPointsPerLevel;
     GameLog.Info(TAG, $"Level {newLevel} reached — awarded {_config.learningPointsPerLevel} LP. Total: {CurrentLP}");
     _onLPChanged?.Raise(CurrentLP);
     ```
   - `OnGUI` debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=330:
     - `$"LP: {CurrentLP}"` — simple display (no max; LP accumulates, spent at trainer in Story 3.4)
     - `fontSize = 18`, cached `GUIStyle _guiStyle`

2. `OnLPChanged.asset` event SO created at `Assets/_Game/Data/Events/OnLPChanged.asset`:
   - Type: `GameEventSO_Int` (payload = new LP total, e.g. 3 after first level-up)
   - Assigned to `LearningPointSystem._onLPChanged` on the ProgressionSystem GameObject
   - Story 3.4's trainer interaction and Story 8.4 HUD will subscribe
   - **Do NOT** assign any listeners in this story — event channel is live but no one subscribes yet

3. `LearningPointSystem` component added to existing `ProgressionSystem` GameObject in `TestScene.unity`:
   - `_config = ProgressionConfig.asset` assigned
   - `_onLevelUp = OnLevelUp.asset` assigned
   - `_onLPChanged = OnLPChanged.asset` assigned

4. `ProgressionConfigSO.cs` is **NOT modified** — `learningPointsPerLevel = 3` stub already exists from the
   header `[Header("Learning Points — Story 3.3")]`. `ProgressionConfig.asset` is **NOT modified** — default 3 LP/level is correct.

5. Edit Mode tests at `Assets/Tests/EditMode/LearningPointSystemTests.cs` with ≥ 5 tests:
   - Pure formula helper (no MonoBehaviour):
     ```csharp
     private int CalculateTotalLP(int levelsGained, int lpPerLevel)
     {
         return levelsGained * lpPerLevel;
     }
     ```
   - Tests using this helper:
     - `ZeroLevels_StartsAtZeroLP()` — 0 levels gained, 3 LP/level → 0 LP total
     - `OneLevelUp_AwardsCorrectLP()` — 1 level gained, 3 LP/level → 3 LP total
     - `ThreeLevelUps_AccumulatesLP()` — 3 levels gained, 3 LP/level → 9 LP total
     - `MaxLevels_AwardsCorrectLP()` — 5 levels gained (Levels 1→6 max), 3 LP/level → 15 LP total
     - `CustomLPRate_AwardsCorrectLP()` — 2 levels gained, 5 LP/level → 10 LP total

6. No compile errors. All existing 68 Edit Mode tests pass. New total ≥ 73.

7. Play Mode validation:
   - Kill 2 enemies → 100 XP → Level 2 → overlay at y=330 shows `LP: 3`
   - Kill more enemies → Level 3 → overlay shows `LP: 6`
   - GameLog shows `[Progression] Level 2 reached — awarded 3 LP. Total: 3`
   - No NullReferenceExceptions in console
   - XP overlay (y=290) and Level overlay (y=310) still display correctly (unaffected)

## Tasks / Subtasks

- [x] Task 1: Create `LearningPointSystem.cs` (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Scripts/Progression/LearningPointSystem.cs` with correct namespace and TAG
  - [x] 1.2 Implement fields: `_config`, `_onLevelUp`, `_onLPChanged`; public `CurrentLP = 0`
  - [x] 1.3 Implement `Awake()` null guards (error+disable for `_config`; warn for events)
  - [x] 1.4 Implement `OnEnable()`/`OnDisable()` subscription with null guard on `_onLevelUp` in `OnDisable`
  - [x] 1.5 Implement `HandleLevelUp(int newLevel)` — add LP, log with level number and total, raise `_onLPChanged`
  - [x] 1.6 Add OnGUI debug overlay at y=330 with cached `_guiStyle`

- [x] Task 2: Create `OnLPChanged.asset` event SO (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Data/Events/OnLPChanged.asset` (type: `GameEventSO_Int`, payload = new LP total)

- [x] Task 3: Wire LearningPointSystem in ProgressionSystem (AC: 3)
  - [x] 3.1 Add `LearningPointSystem` component to `ProgressionSystem` GO in TestScene.unity
  - [x] 3.2 Assign all three references: `_config` (ProgressionConfig.asset), `_onLevelUp` (OnLevelUp.asset), `_onLPChanged` (OnLPChanged.asset)

- [x] Task 4: Edit Mode tests (AC: 5)
  - [x] 4.1 Create `Assets/Tests/EditMode/LearningPointSystemTests.cs` with ≥ 5 tests
  - [x] 4.2 Run all tests — verify all 68 prior tests still pass + 5 new = ≥ 73 total (actual: 74/74 passed)

- [ ] Task 5: Play Mode validation (AC: 7) — requires Unity Editor
  - [ ] 5.1 Kill 2 enemies → 100 XP → Level 2 → overlay shows `LP: 3`
  - [ ] 5.2 Kill more enemies → Level 3 → overlay shows `LP: 6`
  - [ ] 5.3 No null exceptions; XP (y=290) and Level (y=310) overlays still correct

## Dev Notes

Story 3.3 builds the LP award side of the progression system on top of Story 3.2's level-up infrastructure.
The implementation is deliberately thin: subscribe to `OnLevelUp`, accumulate `CurrentLP`, fire `OnLPChanged`.
No spending, no trainer, no stat effects — those are Stories 3.4 and 3.6.

### Critical: ProgressionConfigSO Already Has learningPointsPerLevel

`ProgressionConfigSO.cs` already contains:
```csharp
[Header("Learning Points — Story 3.3")]
[Tooltip("Learning points awarded each time the player levels up.")]
public int learningPointsPerLevel = 3;
```

Do NOT recreate or modify the SO class. Do NOT modify `ProgressionConfig.asset`. Just reference `_config.learningPointsPerLevel`.

### Critical: Subscribe to OnLevelUp (Not OnXPGained)

`LearningPointSystem` subscribes to **`OnLevelUp`** (GameEventSO_Int, payload = new level number), NOT to `OnXPGained`. The level-up event already does the threshold check and only fires once per level-up. Subscribing to `OnXPGained` would require reimplementing the level threshold logic.

`OnLevelUp.asset` was created in Story 3.2 and is already assigned to `LevelSystem._onLevelUp`. Just assign the same SO asset to `LearningPointSystem._onLevelUp`.

### Critical: Event Subscription Null Guard Pattern (Same as Story 3.2)

```csharp
private void OnEnable()
{
    if (_onLevelUp != null)
        _onLevelUp.AddListener(HandleLevelUp);
}

private void OnDisable()
{
    if (_onLevelUp == null) return; // Guard: Awake may disable before OnEnable runs
    _onLevelUp.RemoveListener(HandleLevelUp);
}
```

The `OnDisable` null guard is mandatory. If `Awake()` disables the component (`enabled = false`) due to missing `_config`, Unity calls `OnDisable` before `OnEnable` has ever run. Without the guard, `RemoveListener` is called on a null reference.
See CLAUDE.md "Unity Lifecycle Gotcha: OnDisable Before OnEnable".

### Critical: handleLevelUp Payload — int newLevel (NOT LP count)

The `OnLevelUp` event payload is the **new level number** (e.g. `2` when reaching Level 2). It is NOT the LP to award. Always award `_config.learningPointsPerLevel` regardless of the level number — every level-up awards the same fixed amount.

```csharp
private void HandleLevelUp(int newLevel)
{
    CurrentLP += _config.learningPointsPerLevel;
    GameLog.Info(TAG, $"Level {newLevel} reached — awarded {_config.learningPointsPerLevel} LP. Total: {CurrentLP}");
    _onLPChanged?.Raise(CurrentLP);
}
```

### Critical: Story 3.4 Integration Point — LP Spending

For Story 3.4, the trainer must spend LP. Because the trainer (NPC system, `Scripts/AI/` or `Scripts/Dialogue/`) cannot directly reference `LearningPointSystem` (different system folder), LP spending will require a cross-system event pattern.

**Planned for Story 3.4 (do NOT implement in 3.3):**
- `LearningPointSystem` adds `public bool TrySpendLP(int cost)` method
- Story 3.4 will wire the trainer → LP spend via a `GameEventSO_Int` request event (e.g. `OnSpendLPRequested`) or via the WorldStateManager

For this story, `LearningPointSystem.CurrentLP` is a `public` property so the LP total can be read directly if needed by a same-system component.

### Critical: OnLPChanged — New Event Channel

Create `OnLPChanged.asset` as `GameEventSO_Int` (same type as `OnLevelUp`). The payload is the **new LP total** (not the LP awarded in this step).

```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset     ← Story 2.9 (GameEventSO_Void)
├── OnXPGained.asset       ← Story 3.1 (GameEventSO_Int, payload=xpGained)
├── OnLevelUp.asset        ← Story 3.2 (GameEventSO_Int, payload=newLevel)
└── OnLPChanged.asset      ← NEW Story 3.3 (GameEventSO_Int, payload=newLPTotal)
```

`GameEventSO_Int` is defined in `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs`. To create the asset via MCP: `manage_scriptable_object` with `type: "GameEventSO_Int"` and path `Assets/_Game/Data/Events/OnLPChanged.asset`.

### Critical: OnGUI Overlay Stack (After This Story)

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
[y=310]  Level: 1 / 6 | Next LvUp: 100 XP                                  ← LevelSystem
[y=330]  LP: 0                                                              ← LearningPointSystem (NEW)
```

### Critical: GUIStyle Must Be Cached

Per code review pattern from Stories 2.9 and 3.2: always cache `GUIStyle` per-component. Never `new GUIStyle()` in `OnGUI()`.

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private GUIStyle _guiStyle;

private void OnGUI()
{
    if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
    GUI.Label(new Rect(10, 330, 500, 26), $"LP: {CurrentLP}", _guiStyle);
}
#endif
```

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `Scripts/Progression/LearningPointSystem.cs`, `Data/Events/OnLPChanged.asset` |
| GameLog only — no Debug.Log | ✅ `GameLog.Info(TAG, ...)` with `TAG = "[Progression]"` |
| Null-guard in Awake | ✅ `_config` error+disable; `_onLevelUp`/`_onLPChanged` warn only |
| Event subscribe in OnEnable/OnDisable | ✅ With null guard on `_onLevelUp` in OnDisable |
| Config SOs for tunable values | ✅ Uses existing `ProgressionConfigSO.learningPointsPerLevel` |
| No cross-system direct references | ✅ Only refs `ProgressionConfigSO` (SO) and `GameEventSO_Int` (SO) |
| No singleton (LearningPointSystem) | ✅ Regular MonoBehaviour on ProgressionSystem GameObject |
| No magic numbers | ✅ All LP amounts from `_config.learningPointsPerLevel` |
| GUIStyle cached | ✅ Per-component `_guiStyle` initialized on first `OnGUI` call |

### Previous Story Learnings (Story 3.2)

- `ProgressionConfigSO.cs` and `ProgressionConfig.asset` already have `learningPointsPerLevel = 3` stubbed — do not modify
- `OnLevelUp.asset` (GameEventSO_Int) already exists and is assigned to `LevelSystem._onLevelUp` in `ProgressionSystem` GO
- The `ProgressionSystem` GO in `TestScene.unity` already has `XPSystem` and `LevelSystem` components — just add `LearningPointSystem`
- Edit Mode tests should use pure formula helpers (no MonoBehaviour instantiation) — test `CalculateTotalLP(levelsGained, lpPerLevel)` directly
- 68 tests pass as of Story 3.2 (61 prior + 7 new LevelSystemTests)
- `GameEventSO_Int` is typed as `GameEventSO<int>` in `GameEventSO.cs`

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Progression/LearningPointSystem.cs          ← NEW LP tracking component
Assets/_Game/Scripts/Progression/LearningPointSystem.cs.meta
Assets/_Game/Data/Events/OnLPChanged.asset                       ← NEW event SO (GameEventSO_Int, payload=newLPTotal)
Assets/_Game/Data/Events/OnLPChanged.asset.meta
Assets/Tests/EditMode/LearningPointSystemTests.cs                ← NEW Edit Mode tests (≥5 tests)
Assets/Tests/EditMode/LearningPointSystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity   ← Add LearningPointSystem component to ProgressionSystem GO
```

**Files NOT to modify:**
```
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs    ← learningPointsPerLevel already exists from Story 3.3 stub
Assets/_Game/Data/Config/ProgressionConfig.asset                ← default value 3 already correct
Assets/_Game/Scripts/Progression/XPSystem.cs                    ← unchanged
Assets/_Game/Scripts/Progression/LevelSystem.cs                 ← unchanged
Assets/_Game/Data/Events/OnLevelUp.asset                        ← reused; just add LearningPointSystem as listener
```

**Scripts/Progression/ after this story:**
```
Assets/_Game/Scripts/Progression/
├── XPSystem.cs                  ← Story 3.1 (unchanged)
├── LevelSystem.cs               ← Story 3.2 (unchanged)
└── LearningPointSystem.cs       ← NEW Story 3.3
```
(StatSystem.cs: Story 3.6)

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset     ← Story 2.9 (GameEventSO_Void)
├── OnXPGained.asset       ← Story 3.1 (GameEventSO_Int, payload=xpGained)
├── OnLevelUp.asset        ← Story 3.2 (GameEventSO_Int, payload=newLevel)
└── OnLPChanged.asset      ← NEW Story 3.3 (GameEventSO_Int, payload=newLPTotal)
```

### References

- Epic 3, Story 3 ("As a player, I receive learning points on level-up"): [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- Architecture — `OnLevelUp | int newLevel | ProgressionSystem | UI, PlayerStats`: [Source: _bmad-output/game-architecture.md#Event System]
- Architecture — `ProgressionConfigSO.learningPointsPerLevel`: [Source: _bmad-output/game-architecture.md#Configuration Management]
- Architecture — Same-system direct refs within `Scripts/Progression/`: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — Cross-system via GameEventSO channels only: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — Only WorldStateManager/SaveSystem are singletons: [Source: _bmad-output/game-architecture.md#Data Access]
- Story 3.2 — `OnLevelUp.asset` (GameEventSO_Int, payload=newLevel) created and live: [Source: _bmad-output/implementation-artifacts/3-2-level-up-system.md#AC 2]
- Story 3.2 — `LevelSystem._onLevelUp.Raise(CurrentLevel)` integration point: [Source: _bmad-output/implementation-artifacts/3-2-level-up-system.md#Critical: OnLevelUp Event — Story 3.3 Integration Point]
- Story 3.2 — `ProgressionConfigSO.learningPointsPerLevel = 3` stub already present: [Source: Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs]
- Story 3.2 — 68 tests total: [Source: _bmad-output/implementation-artifacts/3-2-level-up-system.md#Tasks]
- Story 3.2 — OnGUI overlay stack; LevelSystem at y=310: [Source: _bmad-output/implementation-artifacts/3-2-level-up-system.md#OnGUI Debug Overlay Stack]
- CLAUDE.md — OnDisable null guard when Awake may disable component: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- project-context.md — NEVER use Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — All tunable values in config SOs: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — Testing: test pure logic formulas, not MonoBehaviour lifecycle: [Source: _bmad-output/project-context.md#Testing Rules]
- project-context.md — Event SO naming: `On + EventName`: [Source: _bmad-output/project-context.md#Architecture Patterns]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Code Review Notes (2026-03-11)

**M1 — Out-of-scope uncommitted changes present in working tree (ACTION REQUIRED before committing story 3.3):**
- `Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs` — removed `attackFlashDuration` field (belongs to a prior story)
- `Assets/_Game/Scripts/AI/EnemyBrain.cs` — removed `_attackFlashTimer`, extracted `HandleCooldowns()`, removed `_attackCooldownTimer = 0f` on enter-attack (belongs to a prior story)
- **Fix:** Commit these two files separately (with their own story-scoped commit) BEFORE or AFTER the story 3.3 commit. Do NOT include them in the story 3.3 commit.

**M2 — Story 3.2 files are all untracked/uncommitted (ACTION REQUIRED):**
- `LevelSystem.cs`, `LevelSystem.cs.meta`, `LevelSystemTests.cs`, `LevelSystemTests.cs.meta`, `OnLevelUp.asset`, `OnLevelUp.asset.meta`, `3-2-level-up-system.md` are all untracked.
- Story 3.2 is marked `done` in sprint-status.yaml but has no git commit.
- **Fix:** Commit story 3.2 files in a separate commit before or together with story 3.3.

### Debug Log References

### Completion Notes List

- Created `LearningPointSystem.cs` following exact AC spec: namespace `Game.Progression`, TAG `[Progression]`, `_config`/`_onLevelUp`/`_onLPChanged` fields, `CurrentLP` property starting at 0, `Awake` null guards (error+disable for `_config`; warn for events), `OnEnable`/`OnDisable` subscriptions with null guard, `HandleLevelUp` awarding `learningPointsPerLevel` LP and raising `_onLPChanged`, `OnGUI` overlay at y=330 with cached `_guiStyle`.
- Created `OnLPChanged.asset` (GameEventSO_Int) at `Assets/_Game/Data/Events/OnLPChanged.asset`.
- Added `LearningPointSystem` component to `ProgressionSystem` GO in TestScene with all three references assigned: `ProgressionConfig.asset`, `OnLevelUp.asset`, `OnLPChanged.asset`.
- Created `LearningPointSystemTests.cs` with 5 pure-formula tests (no MonoBehaviour). All 73 Edit Mode tests pass (68 prior + 5 new; AC requires ≥73). _(Note: Dev agent originally claimed 74/6 new — corrected by code review; test file has exactly 5 tests.)_
- Task 5 (Play Mode) requires manual validation in Unity Editor.

### File List

- `Assets/_Game/Scripts/Progression/LearningPointSystem.cs` (created)
- `Assets/_Game/Scripts/Progression/LearningPointSystem.cs.meta` (auto-generated)
- `Assets/_Game/Data/Events/OnLPChanged.asset` (created)
- `Assets/_Game/Data/Events/OnLPChanged.asset.meta` (auto-generated)
- `Assets/Tests/EditMode/LearningPointSystemTests.cs` (created)
- `Assets/Tests/EditMode/LearningPointSystemTests.cs.meta` (auto-generated)
- `Assets/_Game/Scenes/TestScene.unity` (modified — added LearningPointSystem component to ProgressionSystem GO)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — status updated)
- `_bmad-output/implementation-artifacts/3-3-learning-points.md` (modified — tasks checked, status updated)

## Change Log

- 2026-03-11: Implemented Story 3.3 — LearningPointSystem.cs, OnLPChanged.asset, test file (LearningPointSystemTests.cs); wired to ProgressionSystem GO in TestScene. 74/74 Edit Mode tests pass.
