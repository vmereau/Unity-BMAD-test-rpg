# Story 3.5: Tome Skill Learning

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to find tomes in the world and read them to learn skills using learning points,
so that I can permanently expand my character's capabilities through exploration and deliberate LP investment.

## Acceptance Criteria

1. **`SkillSO.cs`** exists at `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs`:
   - `namespace Game.Progression`, `CreateAssetMenu(menuName = "Game/Skills/Skill", fileName = "Skill_")`
   - `public string skillId` — unique identifier (e.g. `"power_strike"`)
   - `public string displayName` — human-readable label (e.g. `"Power Strike"`)
   - `[TextArea] public string description` — description of what the skill does/will do
   - `public int lpCost` — LP required to learn this skill (must be > 0)
   - Note: The actual gameplay effect of each skill is implemented in Story 3.6 (stat-combat effects). This SO is the data container only.

2. **`PlayerSkills.cs`** exists at `Assets/_Game/Scripts/Progression/PlayerSkills.cs`:
   - `namespace Game.Progression`, `private const string TAG = "[Progression]";`
   - `[SerializeField] private LearningPointSystem _lpSystem`
   - `[SerializeField] private GameEventSO_String _onSkillLearned` — raised with `skillId` when a skill is learned
   - `private readonly HashSet<string> _learnedSkills = new HashSet<string>()`
   - `Awake()`: null-guard `_lpSystem` (error + `enabled = false`)
   - `public bool HasSkill(string skillId)` — returns true if the skill is in `_learnedSkills`
   - `public bool LearnSkill(SkillSO skill)`:
     - Returns false if `skill == null` (error log)
     - Returns false if `HasSkill(skill.skillId)` (warn: "Already learned")
     - Returns false if `!_lpSystem.TrySpendLP(skill.lpCost)` (warn: "Insufficient LP")
     - On success: `_learnedSkills.Add(skill.skillId)`, raise `_onSkillLearned` with `skill.skillId`, `GameLog.Info` with skill name, return true
   - `OnGUI` debug overlay at y=390: `$"Skills: {_learnedSkills.Count} learned"`, `fontSize = 18`, cached `_guiStyle`

3. **`TomePickup.cs`** exists at `Assets/_Game/Scripts/World/TomePickup.cs`:
   - `namespace Game.World`, `private const string TAG = "[World]";`
   - `[SerializeField] private SkillSO _skill`
   - `[SerializeField] private PlayerSkills _playerSkills`
   - `[SerializeField] private Transform _playerTransform`
   - `[SerializeField] private float _interactionRadius = 2f`
   - `private PersistentID _persistentID` — fetched via `GetComponent<PersistentID>()` in `Awake`
   - `private InputSystem_Actions _input` — created in `Awake`, enabled/disabled in `OnEnable`/`OnDisable`
   - `Awake()`: null-guard `_skill`, `_playerSkills`, `_playerTransform` (error + `enabled = false`); null-warn for `_persistentID` (non-fatal — tome just won't persist); init `_input`
   - `OnEnable()` / `OnDisable()`: `if (_input == null) return;` guard; `_input.Player.Enable()` / `_input.Player.Disable(); _input.Dispose(); _input = null;`
   - `Update()`: if `Vector3.Distance(transform.position, _playerTransform.position) <= _interactionRadius` and `_input.Player.Interact.WasPressedThisFrame()`:
     - Attempt `_playerSkills.LearnSkill(_skill)`
     - If true: `_persistentID?.RegisterDeath()`, `gameObject.SetActive(false)` (tome consumed)
     - If false (not enough LP or already learned): log reason (already surfaced in PlayerSkills); do nothing further
   - `OnGUI` (DEVELOPMENT_BUILD || UNITY_EDITOR): if within range, show `$"Press E to read: {_skill?.displayName ?? "Tome"}"` as centered screen-space label

4. **`Skill_PowerStrike.asset`** exists at `Assets/_Game/Data/Skills/Skill_PowerStrike.asset`:
   - Type: `SkillSO`
   - `skillId = "power_strike"`
   - `displayName = "Power Strike"`
   - `description = "A powerful melee technique. Increases melee damage output. (Effect implemented in Story 3.6)"`
   - `lpCost = 2`

5. **`OnSkillLearned.asset`** created at `Assets/_Game/Data/Events/OnSkillLearned.asset` (type: `GameEventSO_String`)

6. **Scene wiring in `TestScene.unity`**:
   - `PlayerSkills` component added to **ProgressionSystem GO** with:
     - `_lpSystem` → LearningPointSystem on ProgressionSystem GO
     - `_onSkillLearned` → OnSkillLearned.asset
   - New **`Tome_PowerStrike` GO** placed in TestScene (accessible location, near Trainer_Master or player start, clear of enemy patrol):
     - `TomePickup` component with:
       - `_skill = Skill_PowerStrike.asset`
       - `_playerSkills` → PlayerSkills on ProgressionSystem GO
       - `_playerTransform` → Player GO transform
       - `_interactionRadius = 2f`
     - `PersistentID` component with a generated GUID; `_onEntityKilled = OnEntityKilled.asset`
     - `SphereCollider` trigger (radius 0.5) — visual indicator only; not used by code

7. **Edit Mode tests** at `Assets/Tests/EditMode/SkillLearningTests.cs` with ≥ 6 tests:
   - Pure logic helpers (no MonoBehaviour):
     ```csharp
     private bool CanLearnSkill(bool alreadyLearned, bool hasEnoughLP)
         => !alreadyLearned && hasEnoughLP;

     private bool IsSkillKnown(HashSet<string> known, string id)
         => known.Contains(id);

     private HashSet<string> SimulateLearnSkill(HashSet<string> known, string id)
     {
         known.Add(id);
         return known;
     }

     private int SimulateLPAfterLearn(int currentLP, int cost)
         => currentLP - cost;
     ```
   - Tests:
     - `CanLearn_WhenNewSkillAndSufficientLP_ReturnsTrue()` — alreadyLearned=false, hasLP=true → true
     - `CannotLearn_WhenAlreadyLearned_ReturnsFalse()` — alreadyLearned=true, hasLP=true → false
     - `CannotLearn_WhenInsufficientLP_ReturnsFalse()` — alreadyLearned=false, hasLP=false → false
     - `CannotLearn_WhenAlreadyLearnedAndInsufficientLP_ReturnsFalse()` — alreadyLearned=true, hasLP=false → false
     - `SkillTracking_AfterLearn_ContainsSkillId()` — known={}, learn "power_strike" → known contains "power_strike"
     - `SkillTracking_MultipleDistinctSkills_AllTracked()` — learn "power_strike" + "stealth_walk" → both in set
     - `LPCost_DeductedCorrectly_AfterLearn()` — LP=5, cost=2 → remaining LP=3
     - `CannotLearn_ExactLP_SameAsCost_PassesLPCheck()` — LP=2, cost=2 → hasEnoughLP=true (edge case)

8. No compile errors. All existing ≥ 87 Edit Mode tests pass. New total ≥ 95.

9. **Play Mode validation**:
   - Start TestScene; walk to `Tome_PowerStrike` GO → `"Press E to read: Power Strike"` prompt appears
   - Press E → skill learned: Skills overlay at y=390 changes from "0 learned" to "1 learned"; LP decreases by 2
   - Approach tome again (still visible if not deactivated) — wait, it should have been deactivated. Cannot re-approach
   - Reload scene (play mode restart) → Tome_PowerStrike GO inactive immediately (WorldStateManager tracks kill in-session; note: cross-session persistence requires Epic 8 Save System)
   - Approach tome with LP=0 → pressing E does nothing (logged as "Insufficient LP")
   - Console: `[World] Tome consumed: Power Strike` or similar; no NullReferenceExceptions

## Tasks / Subtasks

- [x] Task 1: Create `SkillSO.cs` (AC: 1)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Skills/` folder
  - [x] 1.2 Create `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` with all fields and CreateAssetMenu

- [x] Task 2: Create `PlayerSkills.cs` (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Scripts/Progression/PlayerSkills.cs` — namespace, TAG, fields
  - [x] 2.2 Implement `Awake` null-guard for `_lpSystem`
  - [x] 2.3 Implement `HasSkill(string)` and `LearnSkill(SkillSO)` with all guards and logging
  - [x] 2.4 Add `OnGUI` overlay at y=390 with cached `_guiStyle`

- [x] Task 3: Create `TomePickup.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/World/TomePickup.cs` — namespace, TAG, fields
  - [x] 3.2 Implement `Awake` null-guards + `PersistentID` fetch + `InputSystem_Actions` init
  - [x] 3.3 Implement `OnEnable`/`OnDisable` with null guards (CLAUDE.md lifecycle gotcha)
  - [x] 3.4 Implement `Update` proximity check + `Interact.WasPressedThisFrame()` + `LearnSkill` call
  - [x] 3.5 Implement `OnGUI` proximity prompt

- [x] Task 4: Create `Skills/` data folder and `Skill_PowerStrike.asset` (AC: 4)
  - [x] 4.1 Create `Assets/_Game/Data/Skills/` folder
  - [x] 4.2 Create `Assets/_Game/Data/Skills/Skill_PowerStrike.asset` with correct values

- [x] Task 5: Create `OnSkillLearned.asset` event SO (AC: 5)
  - [x] 5.1 Create `Assets/_Game/Data/Events/OnSkillLearned.asset` (type: GameEventSO_String)

- [x] Task 6: Wire scene (AC: 6)
  - [x] 6.1 Add `PlayerSkills` component to ProgressionSystem GO — assign `_lpSystem`, `_onSkillLearned`
  - [x] 6.2 Create `Tome_PowerStrike` GO in TestScene — add `TomePickup`, `PersistentID` (generate GUID), `SphereCollider`
  - [x] 6.3 Assign all TomePickup inspector refs; assign `_onEntityKilled = OnEntityKilled.asset` on PersistentID

- [x] Task 7: Edit Mode tests (AC: 7, 8)
  - [x] 7.1 Create `Assets/Tests/EditMode/SkillLearningTests.cs` with ≥ 8 pure-logic tests
  - [x] 7.2 Run all tests — 96 total pass (≥ 95 required ✅)

### Review Follow-ups (AI)
- [ ] [AI-Review][LOW] Add Awake warning when `_onSkillLearned` is null in `PlayerSkills.cs:25` (consistent with other Progression systems)
- [ ] [AI-Review][LOW] Add `Assets/_Game/Data/Skills.meta` and `Assets/_Game/ScriptableObjects/Skills.meta` to File List below

- [x] Task 8: Play Mode validation (AC: 9) — requires Unity Editor
  - [x] 8.1 Walk to tome, verify prompt appears
  - [x] 8.2 Press E, verify skill count overlay increments and LP decreases by 2
  - [x] 8.3 Verify tome GO deactivates after successful read
  - [x] 8.4 Verify no crash when approaching with LP=0

## Dev Notes

Story 3.5 introduces the tome interaction system and skill data infrastructure. This is deliberately prototype-scoped: skills are *learned* (tracked in `PlayerSkills._learnedSkills`) but their gameplay effects are implemented in Story 3.6 (stat-combat effects). The pattern mirrors TrainerNPC from 3.4: prototype direct references, OnGUI feedback, proximity+E interaction.

### Critical: `SkillSO.cs` in ScriptableObjects/Skills/ — Folder Must Be Created

The architecture specifies `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` and `Assets/_Game/Data/Skills/` — neither folder exists yet. Create both folders (and their `.meta` files) before creating assets.

```
Assets/_Game/ScriptableObjects/Skills/           ← CREATE this folder
Assets/_Game/ScriptableObjects/Skills/SkillSO.cs ← NEW
Assets/_Game/Data/Skills/                        ← CREATE this folder
Assets/_Game/Data/Skills/Skill_PowerStrike.asset ← NEW
```

Use MCP `manage_asset(action="create_folder")` or `manage_script` tooling. Without the `.meta` for the folder, Unity may regenerate GUIDs and break asset references.

### Critical: PlayerSkills in Scripts/Progression/ — Same-System Direct Ref to LearningPointSystem

`PlayerSkills.cs` is in `Scripts/Progression/` and holds a direct serialized ref to `LearningPointSystem` (also `Scripts/Progression/`). This is **acceptable per the architecture**:

> "Same-system comms: Direct MonoBehaviour references acceptable within the same `Scripts/[System]/` folder"

So `PlayerSkills._lpSystem` is NOT a prototype exception — it's the correct architecture pattern.

The cross-system exception is `TomePickup.cs` (in `Scripts/World/`) holding a direct ref to `PlayerSkills` (in `Scripts/Progression/`). This is the same prototype pragmatism as `TrainerNPC` in Story 3.4. It will be superseded in Epic 6 (Inventory/loot system will handle item-type pickups properly).

### Critical: TomePickup in Scripts/World/ — NOT Scripts/AI/

Unlike `TrainerNPC.cs` (AI-driven NPC), the tome is a passive world object. It belongs in `Scripts/World/` (same as `PersistentID.cs`) with namespace `Game.World`. Do NOT put it in `Scripts/AI/`.

### Critical: InputSystem_Actions Pattern in TomePickup

Same lifecycle as `TrainerNPC.cs` from Story 3.4. Must create its own instance and null it after Dispose to prevent `ObjectDisposedException` on re-enable:

```csharp
private InputSystem_Actions _input;

private void Awake()
{
    // ... null guards ...
    _persistentID = GetComponent<PersistentID>();
    _input = new InputSystem_Actions();
}

private void OnEnable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable
    _input.Player.Enable();
}

private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable
    _input.Player.Disable();
    _input.Dispose();
    _input = null; // Null after Dispose (code-review fix pattern from 3.4)
}
```

### Critical: Tome Persistence Scope — In-Session Only (Epic 8 Deferred)

`WorldStateManager.Instance.RegisterKill(guid)` via `PersistentID.RegisterDeath()` tracks the tome as consumed **in-session only**. The WorldStateManager does not persist to disk yet (Save/Load is Epic 8). This means:

- Within a single play session: tome consumed → stays gone on scene reload (WorldStateManager is DontDestroyOnLoad)
- After application restart: tome re-appears at original position

This is expected prototype behavior. Document this limitation in play mode validation notes.

### Critical: `_persistentID` May Be Null — Non-Fatal

If the Tome GO is missing a PersistentID component, `TomePickup` should warn but NOT disable. The tome can still function for learning — it just won't be tracked by WorldStateManager. Guard null before calling:

```csharp
_persistentID?.RegisterDeath();
gameObject.SetActive(false); // Always disable after successful learn
```

### Critical: PersistentID Requires OnEntityKilled.asset

`PersistentID.RegisterDeath()` will warn if `_onEntityKilled` is not assigned. Assign `Assets/_Game/Data/Events/OnEntityKilled.asset` (created in Story 2.9, `GameEventSO_String`) to the `Tome_PowerStrike` GO's PersistentID component to suppress the warning.

### Critical: SkillSO `skillId` vs `name` — Use `skillId` for Lookup

`SkillSO.skillId` is the authoritative unique key, not `SkillSO.name` (which is the asset filename). Always use `skill.skillId` in `PlayerSkills._learnedSkills` HashSet and event payloads. This prevents coupling skill tracking to asset names.

### Critical: PlayerSkills.LearnSkill Must Be Atomic (LP + Registration)

The LP is spent via `_lpSystem.TrySpendLP(skill.lpCost)` BEFORE adding to `_learnedSkills`. TrySpendLP returns false if insufficient — ensuring no LP is deducted if learning fails. The order matters:

```csharp
public bool LearnSkill(SkillSO skill)
{
    if (skill == null) { GameLog.Error(TAG, "LearnSkill called with null skill"); return false; }
    if (HasSkill(skill.skillId))
    {
        GameLog.Warn(TAG, $"Skill already learned: {skill.displayName}");
        return false;
    }
    // TrySpendLP is atomic — only deducts if sufficient LP exists
    if (!_lpSystem.TrySpendLP(skill.lpCost))
    {
        GameLog.Warn(TAG, $"Insufficient LP to learn {skill.displayName} (cost: {skill.lpCost}, current: {_lpSystem.CurrentLP})");
        return false;
    }
    _learnedSkills.Add(skill.skillId);
    _onSkillLearned?.Raise(skill.skillId);
    GameLog.Info(TAG, $"Skill learned: {skill.displayName} (id: {skill.skillId}). Total skills: {_learnedSkills.Count}");
    return true;
}
```

### Critical: TomePickup OnGUI Prompt — DEVELOPMENT_BUILD || UNITY_EDITOR Guard

Same as TrainerNPC: proximity prompt in OnGUI must be wrapped in `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. Use the existing `_guiStyle` caching pattern:

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private GUIStyle _promptStyle;

private void OnGUI()
{
    if (_playerTransform == null || _skill == null) return;
    if (Vector3.Distance(transform.position, _playerTransform.position) > _interactionRadius) return;
    if (_promptStyle == null)
        _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
    GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.65f, 400, 30),
        $"Press E to read: {_skill.displayName}", _promptStyle);
}
#endif
```

### Critical: Test Pattern — Pure Logic Helpers Only

Same pattern as `TrainerTransactionTests.cs` from Story 3.4. Tests must NOT instantiate MonoBehaviours. Test only the pure decision logic:

- `CanLearnSkill(bool alreadyLearned, bool hasEnoughLP)`
- `IsSkillKnown(HashSet<string> known, string id)`
- `SimulateLearnSkill(HashSet<string> known, string id)`
- `SimulateLPAfterLearn(int currentLP, int cost)`

### OnGUI Overlay Stack After This Story

```
[y=50]   Stamina: 80 / 100                                                  ← StaminaSystem
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
[y=350]  STR:5 DEX:5 END:5 MNA:5                                           ← PlayerStats
[y=370]  Gold: 500                                                          ← GoldSystem
[y=390]  Skills: 0 learned                                                  ← PlayerSkills (NEW)
```

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Skills/                          ← NEW folder
Assets/_Game/ScriptableObjects/Skills/SkillSO.cs               ← NEW skill data SO
Assets/_Game/ScriptableObjects/Skills/SkillSO.cs.meta
Assets/_Game/Scripts/Progression/PlayerSkills.cs               ← NEW skill tracker component
Assets/_Game/Scripts/Progression/PlayerSkills.cs.meta
Assets/_Game/Scripts/World/TomePickup.cs                        ← NEW world tome interactable
Assets/_Game/Scripts/World/TomePickup.cs.meta
Assets/_Game/Data/Skills/                                       ← NEW folder
Assets/_Game/Data/Skills/Skill_PowerStrike.asset               ← NEW skill data instance
Assets/_Game/Data/Skills/Skill_PowerStrike.asset.meta
Assets/_Game/Data/Events/OnSkillLearned.asset                  ← NEW event SO (GameEventSO_String)
Assets/_Game/Data/Events/OnSkillLearned.asset.meta
Assets/Tests/EditMode/SkillLearningTests.cs                    ← NEW Edit Mode tests (≥8 tests)
Assets/Tests/EditMode/SkillLearningTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity                            ← Add PlayerSkills to ProgressionSystem GO,
                                                                  add Tome_PowerStrike GO
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Progression/LearningPointSystem.cs        ← unchanged; TrySpendLP already added in 3.4
Assets/_Game/Scripts/Progression/XPSystem.cs                   ← unchanged
Assets/_Game/Scripts/Progression/LevelSystem.cs                ← unchanged
Assets/_Game/Scripts/Player/PlayerStats.cs                     ← unchanged
Assets/_Game/Scripts/Economy/GoldSystem.cs                     ← unchanged
Assets/_Game/Scripts/AI/TrainerNPC.cs                          ← unchanged
Assets/_Game/Data/Config/ProgressionConfig.asset               ← unchanged
```

**Scripts/Progression/ after this story:**
```
Assets/_Game/Scripts/Progression/
├── XPSystem.cs               ← Story 3.1 (unchanged)
├── LevelSystem.cs            ← Story 3.2 (unchanged)
├── LearningPointSystem.cs    ← Story 3.3 + TrySpendLP from 3.4 (unchanged)
└── PlayerSkills.cs           ← NEW Story 3.5
```

**Scripts/World/ after this story:**
```
Assets/_Game/Scripts/World/
├── PersistentID.cs           ← Story 2.8 (unchanged)
└── TomePickup.cs             ← NEW Story 3.5
```

**Data/Events/ after this story:**
```
Assets/_Game/Data/Events/
├── OnEntityKilled.asset      ← Story 2.9 (GameEventSO_String)
├── OnPlayerDied.asset        ← Story 2.9 (GameEventSO_Void)
├── OnXPGained.asset          ← Story 3.1 (GameEventSO_Int)
├── OnLevelUp.asset           ← Story 3.2 (GameEventSO_Int)
├── OnLPChanged.asset         ← Story 3.3 (GameEventSO_Int)
├── OnGoldChanged.asset       ← Story 3.4 (GameEventSO_Int)
├── OnStatsChanged.asset      ← Story 3.4 (GameEventSO_Void)
└── OnSkillLearned.asset      ← NEW Story 3.5 (GameEventSO_String, payload=skillId)
```

### Previous Story Learnings (Story 3.4)

- **`LearningPointSystem.TrySpendLP` is confirmed live** at `Assets/_Game/Scripts/Progression/LearningPointSystem.cs`. Returns false if cost <= 0 OR CurrentLP < cost. `PlayerSkills.LearnSkill` depends on this.
- **`PlayerStats.cs`** is confirmed at `Assets/_Game/Scripts/Player/PlayerStats.cs` in `namespace Game.Player`.
- **`ProgressionSystem` GO** in TestScene has: XPSystem, LevelSystem, LearningPointSystem, GoldSystem. Add **PlayerSkills** here too.
- **`Player` GO** in TestScene has: PlayerController, PlayerAnimator, CameraController, PlayerHealth, PlayerStats.
- **Test count after Story 3.4**: ≥ 87 tests (73 from Stories 1-3.3 + 14 from 3.4 story + code review).
- **OnDisable null guard pattern for InputSystem_Actions** is mandatory — see CLAUDE.md "Unity Lifecycle Gotcha". Also null the `_input` field after `Dispose()` to prevent `ObjectDisposedException` on re-enable (code review finding from 3.4).
- **SphereCollider visual on world objects** — Story 3.4 added a SphereCollider to Trainer_Master GO as visual feedback only. Apply same pattern to Tome_PowerStrike GO (trigger, radius 0.5 for a tome-sized object).
- **MCP quirks for scene wiring**: `_onEntityKilled` may not be assignable at GO creation time — may need a second MCP call to set component properties after GO exists.

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All new files in correct folders |
| GameLog only — no Debug.Log | ✅ `GameLog.Info/Warn/Error` with `[Progression]` and `[World]` TAGs |
| Null-guard in Awake | ✅ All new components null-guard required refs |
| Config SOs for tunable values | ✅ `lpCost` lives in `SkillSO` data asset — not hardcoded |
| Cross-system direct refs — prototype exception | ⚠️ `TomePickup` (Game.World) → `PlayerSkills` (Game.Progression) — documented tech debt |
| Same-system direct refs — acceptable | ✅ `PlayerSkills` → `LearningPointSystem` both in `Scripts/Progression/` |
| Event channels for cross-system signals | ✅ `OnSkillLearned.asset` (GameEventSO_String) for downstream listeners |
| PersistentID on world entities | ✅ `Tome_PowerStrike` GO has PersistentID with GUID |
| No magic numbers | ✅ `lpCost` in SkillSO asset; `_interactionRadius` is `[SerializeField]` |
| GUIStyle cached | ✅ Per-component `_guiStyle`/`_promptStyle` initialized on first `OnGUI` call |
| OnDisable null guard for InputSystem | ✅ `TomePickup`: `if (_input == null) return;` + null after Dispose |
| SkillSO in correct SO folder | ✅ `ScriptableObjects/Skills/` per architecture spec |

### References

- Epic 3 scope — "tomes (spend LP to learn skills)": [Source: _bmad-output/epics.md#Epic 3: Progression & Stats]
- Epic 3 Story 5 — "As a player, I can read a tome to learn a skill using learning points": [Source: _bmad-output/epics.md#Stories]
- GDD — Progression: "Spent at Trainers (LP + gold) or Tomes (LP) → Stats increase / Skills unlocked": [Source: _bmad-output/gdd.md#Progression Flow]
- GDD — Core Pillars: "Learning points spent deliberately through trainers and tomes": [Source: _bmad-output/gdd.md#Core Pillars]
- Architecture — Skills: `Scripts/Progression/` | `ScriptableObjects/Skills/SkillSO.cs` | `Data/Skills/`: [Source: _bmad-output/game-architecture.md#System Location Mapping]
- Architecture — Same-system comms: Direct MonoBehaviour refs acceptable within same Scripts/[System]/ folder: [Source: _bmad-output/project-context.md#Architecture Patterns]
- Architecture — Cross-system: GameEventSO<T> only: [Source: _bmad-output/project-context.md#Architecture Patterns]
- Architecture — SO naming: PascalCase + SO suffix, assets PascalCase_Description: [Source: _bmad-output/game-architecture.md#Naming Conventions]
- Story 3.4 — `LearningPointSystem.TrySpendLP` live, returns false if insufficient: [Source: _bmad-output/implementation-artifacts/3-4-trainer-stat-upgrade.md#Acceptance Criteria]
- Story 3.4 — `InputSystem_Actions` null-after-Dispose pattern (code review fix): [Source: _bmad-output/implementation-artifacts/3-4-trainer-stat-upgrade.md#Change Log]
- Story 3.4 — test count ≥ 87 after code review: [Source: _bmad-output/implementation-artifacts/3-4-trainer-stat-upgrade.md#Change Log]
- Story 2.8 — `PersistentID.RegisterDeath()` + `WorldStateManager.RegisterKill`: [Source: Assets/_Game/Scripts/World/PersistentID.cs]
- CLAUDE.md — OnDisable before OnEnable lifecycle gotcha: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- CLAUDE.md — InputSystem_Actions embeds full JSON (no new actions needed for this story): [Source: CLAUDE.md#InputSystem_Actions.cs Embeds the Full JSON]
- project-context.md — `Resources.Load()` BANNED — use serialized Inspector refs: [Source: _bmad-output/project-context.md#Asset Loading]
- project-context.md — NEVER use Debug.Log: [Source: _bmad-output/project-context.md#Logging — MANDATORY]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Play mode startup: 2 expected WorldStateManager-not-found warnings (pre-existing; Core.unity not loaded in TestScene). Pre-existing `_onLPChanged` null warning. No NullReferenceExceptions from new code.
- 96/96 Edit Mode tests pass (prior 88 + 8 new).

### Completion Notes List

- Created `SkillSO.cs` in `ScriptableObjects/Skills/` — data container for learnable skills (skillId, displayName, description, lpCost). Gameplay effects deferred to Story 3.6.
- Created `PlayerSkills.cs` in `Scripts/Progression/` — tracks learned skills HashSet, `HasSkill`/`LearnSkill` methods, OnGUI overlay at y=390.
- Created `TomePickup.cs` in `Scripts/World/` — proximity + E interaction, InputSystem lifecycle pattern (null after Dispose), `_persistentID` non-fatal null guard.
- Created `Skill_PowerStrike.asset` (skillId="power_strike", lpCost=2) and `OnSkillLearned.asset` (GameEventSO_String).
- Wired `PlayerSkills` on ProgressionSystem GO (lpSystem + onSkillLearned refs). Created `Tome_PowerStrike` GO at (5, 0.5, 3) with TomePickup, PersistentID (GUID assigned), SphereCollider (trigger, radius=0.5).
- All patterns follow Story 3.4 precedents: InputSystem null-after-Dispose, OnDisable null guard, `#if DEVELOPMENT_BUILD || UNITY_EDITOR` OnGUI guards.

### File List

Assets/_Game/ScriptableObjects/Skills/SkillSO.cs
Assets/_Game/ScriptableObjects/Skills/SkillSO.cs.meta
Assets/_Game/ScriptableObjects/Skills.meta
Assets/_Game/Scripts/Progression/PlayerSkills.cs
Assets/_Game/Scripts/Progression/PlayerSkills.cs.meta
Assets/_Game/Scripts/World/TomePickup.cs
Assets/_Game/Scripts/World/TomePickup.cs.meta
Assets/_Game/Data/Skills/Skill_PowerStrike.asset
Assets/_Game/Data/Skills/Skill_PowerStrike.asset.meta
Assets/_Game/Data/Skills.meta
Assets/_Game/Data/Events/OnSkillLearned.asset
Assets/_Game/Data/Events/OnSkillLearned.asset.meta
Assets/Tests/EditMode/SkillLearningTests.cs
Assets/Tests/EditMode/SkillLearningTests.cs.meta
Assets/_Game/Scenes/TestScene.unity
_bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-03-13: Story 3.5 implemented — SkillSO, PlayerSkills, TomePickup scripts; Skill_PowerStrike.asset + OnSkillLearned.asset; Tome_PowerStrike GO wired in TestScene; 8 Edit Mode tests (96 total pass)
- 2026-03-13: Code review fixes — M1: SkillSO fields → [SerializeField] private + property getters + [Min(1)] on lpCost; M2: TomePickup.OnEnable recreates _input after disposal; M3: renamed misleading test `CannotLearn_ExactLP_...` → `CanLearn_ExactLP_...`; File List updated with folder .meta files
