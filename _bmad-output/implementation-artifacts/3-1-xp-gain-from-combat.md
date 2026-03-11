# Story 3.1: XP Gain from Combat

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to gain XP from defeating enemies,
so that combat has a meaningful long-term reward beyond the immediate victory.

## Acceptance Criteria

1. `ProgressionConfigSO.cs` exists at `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs`:
   - `namespace Game.Progression`
   - `[CreateAssetMenu(menuName = "Config/Progression", fileName = "ProgressionConfig")]`
   - `[Header("XP")] public int xpPerKill = 50` — flat XP awarded per enemy kill
   - `[Header("Level Thresholds — Story 3.2")] public int[] xpPerLevel = { 100, 250, 500, 900, 1400 }` — XP needed to reach each level (stub; consumed by Story 3.2's LevelSystem)
   - `[Header("Learning Points — Story 3.3")] public int learningPointsPerLevel = 3` — LP awarded on level-up (stub; consumed by Story 3.3)

2. `ProgressionConfig.asset` exists at `Assets/_Game/Data/Config/ProgressionConfig.asset`:
   - Type: `ProgressionConfigSO`
   - Inspector shows all fields with default values from AC 1

3. `XPSystem.cs` exists at `Assets/_Game/Scripts/Progression/XPSystem.cs`:
   - `namespace Game.Progression`, `private const string TAG = "[Progression]";`
   - `[SerializeField] private ProgressionConfigSO _config`
   - `[SerializeField] private GameEventSO_String _onEntityKilled` — wired to `OnEntityKilled.asset`
   - `[SerializeField] private GameEventSO_Int _onXPGained` — optional, warn if unassigned (Story 3.2 LevelSystem subscribes here)
   - `public int CurrentXP { get; private set; }`
   - `public int TotalKills { get; private set; }`
   - `Awake()`: null-guard `_config` (error + `enabled = false` if missing); warn if `_onEntityKilled == null`
   - `OnEnable()`: if `_onEntityKilled != null` → `_onEntityKilled.AddListener(HandleEntityKilled)`
   - `OnDisable()`: if `_onEntityKilled != null` → `_onEntityKilled.RemoveListener(HandleEntityKilled)`
   - `private void HandleEntityKilled(string guid)`:
     - `TotalKills++`
     - `int xpGained = _config.xpPerKill`
     - `CurrentXP += xpGained`
     - `GameLog.Info(TAG, $"XP gained: +{xpGained} (kill #{TotalKills}) — Total XP: {CurrentXP}")`
     - `_onXPGained?.Raise(xpGained)` — signal for Story 3.2 LevelSystem
   - `OnGUI` debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=290:
     `$"XP: {CurrentXP} | Kills: {TotalKills}"` — `fontSize = 18`, cached `GUIStyle`

4. `OnXPGained.asset` event SO created at `Assets/_Game/Data/Events/OnXPGained.asset`:
   - Type: `GameEventSO_Int` (payload = XP amount gained per kill, e.g. 50)
   - Assigned to `XPSystem._onXPGained` on the ProgressionSystem GameObject
   - Story 3.2's LevelSystem will subscribe; Story 8 UI will subscribe for HUD display
   - **Do NOT** assign any listeners in this story — event channel is live but no one subscribes yet

5. `ProgressionSystem` GameObject exists in `TestScene.unity`:
   - Has `XPSystem` component attached
   - `_config = ProgressionConfig.asset` assigned
   - `_onEntityKilled = OnEntityKilled.asset` assigned
   - `_onXPGained = OnXPGained.asset` assigned
   - Note: Final architecture places this in `Core.unity` (always-loaded scene). TestScene placement is the prototype equivalent.

6. Edit Mode tests at `Assets/Tests/EditMode/XPSystemTests.cs` with ≥ 4 tests:
   - Pure formula helpers (no MonoBehaviour):
     - `int CalculateXPForKills(int killCount, int xpPerKill) => killCount * xpPerKill`
     - `int AccumulateXP(int currentXP, int xpGained) => currentXP + xpGained`
   - Tests:
     - `SingleKill_AwardsCorrectXP()` — 1 kill × 50 xpPerKill → 50
     - `MultipleKills_AccumulatesXP()` — 3 kills × 50 each → accumulated total 150
     - `ZeroKills_AwardsZeroXP()` — 0 kills → 0
     - `DifferentConfig_AwardsCorrectXP()` — 1 kill × 100 xpPerKill → 100

7. No compile errors. All existing 57 Edit Mode tests pass. New total ≥ 61.

8. Play Mode validation:
   - Kill one enemy → `OnXPGained` fires → debug overlay shows `XP: 50 | Kills: 1`
   - Kill second enemy → `XP: 100 | Kills: 2`
   - No NullReferenceExceptions in console
   - `OnEntityKilled` still fires correctly for WorldStateManager (PersistentID kill registration unaffected)

## Tasks / Subtasks

- [x] Task 1: Create `ProgressionConfigSO.cs` and config asset (AC: 1, 2)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` — xpPerKill, xpPerLevel stub, learningPointsPerLevel stub
  - [x] 1.2 Create `Assets/_Game/Data/Config/ProgressionConfig.asset` from the new SO menu

- [x] Task 2: Create `XPSystem.cs` (AC: 3)
  - [x] 2.1 Create `Assets/_Game/Scripts/Progression/XPSystem.cs`
  - [x] 2.2 Implement `HandleEntityKilled(string guid)` — increment kills, accumulate XP, raise OnXPGained
  - [x] 2.3 Add OnGUI debug overlay at y=290 with cached GUIStyle
  - [x] 2.4 Verify null-guard, OnEnable/OnDisable subscription pattern

- [x] Task 3: Create `OnXPGained.asset` event (AC: 4)
  - [x] 3.1 Create `Assets/_Game/Data/Events/OnXPGained.asset` (GameEventSO_Int)

- [x] Task 4: Wire ProgressionSystem in TestScene (AC: 5)
  - [x] 4.1 Create `ProgressionSystem` GameObject in TestScene.unity
  - [x] 4.2 Assign XPSystem component; wire _config, _onEntityKilled, _onXPGained

- [x] Task 5: Edit Mode tests (AC: 6)
  - [x] 5.1 Create `Assets/Tests/EditMode/XPSystemTests.cs` with ≥ 4 tests
  - [x] 5.2 Run all tests — verify all 57 prior tests still pass + 4 new = ≥ 61 total ✅ (61/61 passed)

- [ ] Task 6: Play Mode validation (AC: 8) — requires Unity Editor
  - [ ] 6.1 Kill one enemy → OnGUI shows XP: 50 | Kills: 1
  - [ ] 6.2 Kill second enemy → XP: 100 | Kills: 2
  - [ ] 6.3 No null exceptions; WorldStateManager kill registration still works

## Dev Notes

Story 3.1 opens Epic 3 by establishing the XP infrastructure that all subsequent progression stories build on. The XP gain hook itself is simple — `OnEntityKilled.asset` is already live from Story 2.9. The main work is creating the `ProgressionConfigSO` foundation and `XPSystem` component.

### Critical: Event Subscription Pattern

`XPSystem` subscribes to `OnEntityKilled` in **`OnEnable`/`OnDisable`** — NEVER in `Start` or `Awake`. This is a non-negotiable project rule.

```csharp
private void OnEnable()
{
    if (_onEntityKilled != null)
        _onEntityKilled.AddListener(HandleEntityKilled);
}

private void OnDisable()
{
    if (_onEntityKilled != null)
        _onEntityKilled.RemoveListener(HandleEntityKilled);
}
```

The OnDisable null guard is required because `Awake` may disable the component before `OnEnable` runs (if null-guard disables the component: `enabled = false`). See CLAUDE.md "Unity Lifecycle Gotcha: OnDisable Before OnEnable".

### Critical: OnEntityKilled Payload is a GUID String

`OnEntityKilled.asset` is a `GameEventSO_String` where the payload is the entity's GUID (e.g. `"TestScene_Enemy_Grunt01"`). For XP award purposes, Story 3.1 **ignores the GUID** and awards flat `xpPerKill` for every kill signal. Future stories may use the GUID to look up enemy type for variable XP; for now, flat XP is the design.

```csharp
private void HandleEntityKilled(string guid)
{
    // guid is the PersistentID GUID — ignored for flat-XP award in Story 3.1
    TotalKills++;
    int xpGained = _config.xpPerKill;
    CurrentXP += xpGained;
    GameLog.Info(TAG, $"XP gained: +{xpGained} (kill #{TotalKills}) — Total XP: {CurrentXP}");
    _onXPGained?.Raise(xpGained);
}
```

### Critical: ProgressionConfigSO is Epic 3's Foundation

This SO is created in Story 3.1 and extended by subsequent stories:
- Story 3.1: `xpPerKill` (used now)
- Story 3.2: `xpPerLevel[]` (used in 3.2; stub values provided now)
- Story 3.3: `learningPointsPerLevel` (used in 3.3; stub value provided now)
- Story 3.6: stat multipliers (will be added in 3.6)

Add all stub fields from the start with clear `[Header]` annotations so the inspector communicates which story each block belongs to.

```csharp
[CreateAssetMenu(menuName = "Config/Progression", fileName = "ProgressionConfig")]
public class ProgressionConfigSO : ScriptableObject
{
    [Header("XP — Story 3.1")]
    public int xpPerKill = 50;

    [Header("Level Thresholds — Story 3.2")]
    [Tooltip("XP required to reach level index+1. Level 1=100, Level 2=250, etc.")]
    public int[] xpPerLevel = { 100, 250, 500, 900, 1400 };

    [Header("Learning Points — Story 3.3")]
    [Tooltip("Learning points awarded each time the player levels up.")]
    public int learningPointsPerLevel = 3;
}
```

### Critical: OnXPGained Event — Story 3.2 Integration Point

`_onXPGained.Raise(xpGained)` in `HandleEntityKilled()` is the hook for Story 3.2's LevelSystem. When LevelSystem is implemented in 3.2, it will:
1. Subscribe to `OnXPGained` in OnEnable
2. Call `XPSystem.Instance.CurrentXP` OR track accumulated XP from the event payload
3. Check against `ProgressionConfigSO.xpPerLevel[]` thresholds
4. Fire `OnLevelUp` event if threshold crossed

For Story 3.1, `_onXPGained` simply raises to no listeners (event channel is live but silent). This is normal — event channels have no listeners until the subscriber story is implemented.

### Critical: No XPSystem Singleton

Per architecture rules, only `WorldStateManager` and `SaveSystem` are singletons. `XPSystem` is a regular MonoBehaviour in the scene. LevelSystem (Story 3.2) will reference `XPSystem` either:
- **Via the `OnXPGained` event channel** (preferred — decoupled)
- **Via direct MonoBehaviour reference** if both are on the same GameObject (same-system comms are acceptable)

Recommended: put `XPSystem`, `LevelSystem`, `LearningPointSystem`, and `StatSystem` on the **same `ProgressionSystem` GameObject** in Core.unity. They are all in `Scripts/Progression/` — same-system direct references are fine.

### Critical: GameEventSO_Int Exists

`GameEventSO_Int` is already defined in `GameEventSO.cs`:
```csharp
[CreateAssetMenu(menuName = "Game/Events/Int Event", fileName = "NewIntEvent")]
public class GameEventSO_Int : GameEventSO<int> { }
```
Use this for `OnXPGained.asset`. **No new type needed.**

### Critical: GAP — Enemy Layer Still Missing

Story 2.9 noted: "No enemy-specific layer exists (deferred to Epic 3). `PlayerCombat.ExecuteHitDetection()` uses `Physics.DefaultRaycastLayers` mask — may catch unintended colliders."

While this story does not add physics layers (XP gain doesn't require it), be aware: if the developer wishes to address this debt, the steps are:
1. Add "Enemy" layer in Project Settings → Physics Layers
2. Assign `Enemy` layer to `Enemy_Grunt.prefab`
3. Update `PlayerCombat.ExecuteHitDetection()` to use `LayerMask.GetMask("Enemy")` instead of default layers

This is NOT a blocking requirement for Story 3.1 — defer to Story 3.6 or a dedicated cleanup story.

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
[y=290]  XP: 50 | Kills: 1                                                  ← XPSystem (NEW)
```

Always cache `GUIStyle` per-component to avoid heap allocation in OnGUI (Code Review pattern from Story 2.9 fixes):
```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private GUIStyle _guiStyle;

private void OnGUI()
{
    if (_guiStyle == null) _guiStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
    GUI.Label(new Rect(10, 290, 400, 26), $"XP: {CurrentXP} | Kills: {TotalKills}", _guiStyle);
}
#endif
```

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `Scripts/Progression/`, `ScriptableObjects/Config/`, `Data/Config/`, `Data/Events/` |
| GameLog only — no Debug.Log | ✅ Use `GameLog.Info(TAG, ...)` with `TAG = "[Progression]"` |
| Null-guard in Awake | ✅ `_config` null-guarded; error + `enabled = false` |
| Event subscribe in OnEnable/OnDisable | ✅ With null guard on `_onEntityKilled` |
| Config SOs for tunable values | ✅ `ProgressionConfigSO` with xpPerKill |
| No cross-system direct references | ✅ XPSystem subscribes to OnEntityKilled event SO — never references EnemyHealth/EnemyBrain |
| No singleton (XPSystem) | ✅ Regular MonoBehaviour on ProgressionSystem GameObject |
| No magic numbers | ✅ `xpPerKill` in config SO |
| GUIStyle cached | ✅ Per-component `_guiStyle` initialized on first `OnGUI` call |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs      ← NEW config SO class
Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs.meta
Assets/_Game/Scripts/Progression/XPSystem.cs                      ← NEW XP tracking component
Assets/_Game/Scripts/Progression/XPSystem.cs.meta
Assets/_Game/Data/Config/ProgressionConfig.asset                  ← NEW config SO instance
Assets/_Game/Data/Config/ProgressionConfig.asset.meta
Assets/_Game/Data/Events/OnXPGained.asset                         ← NEW event SO (GameEventSO_Int)
Assets/_Game/Data/Events/OnXPGained.asset.meta
Assets/Tests/EditMode/XPSystemTests.cs                            ← NEW Edit Mode tests
Assets/Tests/EditMode/XPSystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity         ← Add ProgressionSystem GameObject with XPSystem
```

**Scripts/Progression/ after this story:**
```
Assets/_Game/Scripts/Progression/
└── XPSystem.cs    ← NEW Story 3.1
```
(LevelSystem.cs, LearningPointSystem.cs, StatSystem.cs: added in Stories 3.2, 3.3, 3.6)

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset   ← Existing Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset     ← Existing Story 2.9 (GameEventSO_Void)
└── OnXPGained.asset       ← NEW Story 3.1 (GameEventSO_Int)
```

**ScriptableObjects/Config/ after this story:**
```
Assets/_Game/ScriptableObjects/Config/
├── AIConfigSO.cs           ← Existing
├── CombatConfigSO.cs       ← Existing
├── PlayerConfigSO.cs       ← Existing
└── ProgressionConfigSO.cs  ← NEW Story 3.1
```

### References

- Epic 3 story 1 ("As a player, I gain XP from defeating enemies"): [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- Epic 3 scope ("Base stats, XP gain, level-up system, learning points..."): [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- Architecture — ProgressionConfigSO fields (XP thresholds, LP per level, stat multipliers): [Source: _bmad-output/game-architecture.md#Configuration Management]
- Architecture — Progression scripts location: [Source: _bmad-output/game-architecture.md#Directory Structure] → `Scripts/Progression/XPSystem.cs`
- Architecture — OnLevelUp event (GameEventSO_Int, int newLevel): [Source: _bmad-output/game-architecture.md#Event System] — raised by ProgressionSystem, heard by UI/PlayerStats
- Architecture — Cross-system via GameEventSO channels only: [Source: _bmad-output/game-architecture.md#Architectural Boundaries]
- Architecture — Only WorldStateManager/SaveSystem are singletons: [Source: _bmad-output/game-architecture.md#Data Access]
- Story 2.9 — OnEntityKilled.asset (GameEventSO_String) live and wired to PersistentID: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#AC 7]
- Story 2.9 — Enemy layer deferred to Epic 3: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Critical: Player Attack Hit Detection]
- Story 2.9 — GUIStyle heap allocation fix (cache per component): [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Change Log]
- Epic 2 Retro — Create ProgressionConfigSO before Story 3.1: [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-03-11.md#Preparation needed before Epic 3]
- Epic 2 Retro — OnEntityKilled event dependency available: [Source: _bmad-output/implementation-artifacts/epic-2-retro-2026-03-11.md#Dependencies on Epic 2 work]
- GameEventSO_Int exists in GameEventSO.cs: [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO.cs]
- OnEntityKilled.asset at Assets/_Game/Data/Events/OnEntityKilled.asset: [Source: git file list]
- OnEnable/OnDisable subscription rule: [Source: _bmad-output/project-context.md#Architecture Patterns]
- CLAUDE.md — OnDisable null guard when Awake may disable component: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- project-context.md — NEVER use Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — All tunable values in config SOs: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — Testing: test pure logic formulas (XP calc), not MonoBehaviour lifecycle: [Source: _bmad-output/project-context.md#Testing Rules]
- Test count after 2.9: 57 tests: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Tasks]
- OnGUI overlay y-positions confirmed: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#OnGUI Debug Overlay Stack]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None.

### Completion Notes List

- Created `ProgressionConfigSO.cs` in `Game.Progression` namespace with xpPerKill=50, xpPerLevel stubs, learningPointsPerLevel stub — foundation for Epic 3 stories 3.2 and 3.3
- Created `ProgressionConfig.asset` SO instance at `Assets/_Game/Data/Config/ProgressionConfig.asset` with default values
- Created `XPSystem.cs`: subscribes to `OnEntityKilled` (GameEventSO_String) in OnEnable/OnDisable with null guards, awards flat xpPerKill per kill, raises `_onXPGained` (GameEventSO_Int), logs via `GameLog.Info`, OnGUI overlay at y=290 with cached GUIStyle
- Created `OnXPGained.asset` (GameEventSO_Int) at `Assets/_Game/Data/Events/OnXPGained.asset` — live event channel for Story 3.2 LevelSystem to subscribe
- Created `ProgressionSystem` GameObject in TestScene.unity with XPSystem component; all three references wired: _config, _onEntityKilled, _onXPGained
- 4 new Edit Mode tests added (`XPSystemTests.cs`); all 61 tests pass (57 prior + 4 new)
- Task 6 (Play Mode validation) requires manual in-Editor testing by Valentin
- **Post-story bugfix:** `PlayerCombat.ExecuteHitDetection()` was using `TryGetComponent<EnemyHealth>()` on the hit collider's GameObject. Since `Enemy_Grunt` has its `CapsuleCollider` on the `Visual` child but `EnemyHealth` on the root, no damage was ever applying. Fixed by switching to `GetComponentInParent<EnemyHealth>()`.
- **Post-story addition:** Added `EnemyRespawner.cs` (Game.DevTools namespace) on `ProgressionSystem` for testing purposes — monitors enemy GameObjects, re-enables them after a configurable delay (default 5s). `EnemyHealth.OnEnable()` added to reset `IsDead` and `CurrentHealth` on reactivation. Superseded by Story 4-5.

### File List

- `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` (new)
- `Assets/_Game/Scripts/Progression/XPSystem.cs` (new)
- `Assets/Tests/EditMode/XPSystemTests.cs` (new)
- `Assets/_Game/Data/Config/ProgressionConfig.asset` (new)
- `Assets/_Game/Data/Events/OnXPGained.asset` (new)
- `Assets/_Game/Scripts/Debug/EnemyRespawner.cs` (new — test scaffolding, Game.DevTools namespace)
- `Assets/_Game/Scripts/AI/EnemyHealth.cs` (modified — added OnEnable reset for respawn support)
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` (modified — ExecuteHitDetection uses GetComponentInParent)
- `Assets/_Game/Scenes/TestScene.unity` (modified — ProgressionSystem + EnemyRespawner added)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — status → review)
- `_bmad-output/implementation-artifacts/3-1-xp-gain-from-combat.md` (modified — tasks, status, records)
- `CLAUDE.md` (modified — added EnemyRespawner test scaffolding pattern and Game.DevTools namespace rule)
