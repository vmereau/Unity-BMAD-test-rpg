# Story 2.8: Enemy AI — Patrol & Engage

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want enemies to patrol waypoints when calm and chase me when I enter their detection range,
so that the world feels alive and combat encounters begin naturally.

## Acceptance Criteria

1. `AIConfigSO.cs` exists at `Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs`:
   - `namespace Game.AI` (note: AI system lives in its own namespace consistent with `Scripts/AI/` folder)
   - `[CreateAssetMenu(menuName = "Config/AI", fileName = "AIConfig")]`
   - `[Header("Movement")]` block: `public float patrolSpeed = 2f`, `public float engageSpeed = 4f`
   - `[Header("Detection")]` block: `public float detectionRange = 8f`, `public float disengageRange = 12f`
   - `[Header("Patrol")]` block: `public float waypointArrivalThreshold = 0.5f`, `public float patrolWaitTime = 2f`
   - Asset instance: `Assets/_Game/Data/Config/AIConfig.asset`

2. A minimal `WorldStateManager.cs` exists at `Assets/_Game/Scripts/Core/WorldStateManager.cs`:
   - `namespace Game.Core`, `private const string TAG = "[WorldState]";`
   - Singleton: `public static WorldStateManager Instance { get; private set; }` with `DontDestroyOnLoad`
   - Duplicate guard: if `Instance != null && Instance != this` → `GameLog.Warn` + `Destroy(gameObject)`
   - `private HashSet<string> _killedEntities = new HashSet<string>()`
   - `public bool IsKilled(string guid)`: return false if null/empty (log warning), else `_killedEntities.Contains(guid)`
   - `public void RegisterKill(string guid)`: log warning if null/empty, else `_killedEntities.Add(guid)`
   - **Scope note:** This is a minimal stub for Stories 2.8–2.9. Save/Load and event wiring come in Epic 8.
   - Component added to the `WorldStateManager` stub GameObject in `Core.unity`

3. `PersistentID.cs` exists at `Assets/_Game/Scripts/World/PersistentID.cs`:
   - `namespace Game.World`, `private const string TAG = "[WorldState]";`
   - `[SerializeField] private string _guid` — GUID string, assigned in Editor per entity
   - `[SerializeField] private GameEventSO_String _onEntityKilled` — optional (warn if null when RegisterDeath called)
   - On `Awake()`:
     - If `_guid` is null or empty → `GameLog.Error` + return (entity stays active; designer must fix)
     - If `WorldStateManager.Instance != null && WorldStateManager.Instance.IsKilled(_guid)` → `gameObject.SetActive(false)` (silent, per architecture)
     - If `WorldStateManager.Instance == null` → `GameLog.Warn` (WorldStateManager not found — PersistentID check skipped)
   - `public void RegisterDeath()`: calls `WorldStateManager.Instance?.RegisterKill(_guid)`, raises `_onEntityKilled?.Raise(_guid)` if assigned
   - `[ContextMenu("Generate GUID")]` editor utility: `_guid = System.Guid.NewGuid().ToString(); UnityEditor.EditorUtility.SetDirty(this);`

4. `EnemyBrain.cs` exists at `Assets/_Game/Scripts/AI/EnemyBrain.cs`:
   - `namespace Game.AI`, `private const string TAG = "[AI]";`
   - `[RequireComponent(typeof(NavMeshAgent))]`
   - States: `private enum EnemyState { Idle, Patrolling, Engaging }` (Attacking and Dead deferred to Story 2.9)
   - Serialized: `[SerializeField] private AIConfigSO _config`, `[SerializeField] private Transform[] _waypoints`
   - Cached in `Awake()`:
     - `_agent = GetComponent<NavMeshAgent>()` (null-guard: disable on null)
     - `_config` null-guard: log error + disable
     - Player: `var playerObj = GameObject.FindGameObjectWithTag("Player")` → `_player = playerObj?.transform` (warn if null, do NOT disable — enemy can still patrol)
   - Private state: `_state = EnemyState.Idle`, `_currentWaypoint = 0`, `_waitTimer = 0f`
   - `Update()`: switch on `_state` → `HandleIdle()` / `HandlePatrol()` / `HandleEngage()`
   - `HandleIdle()`:
     - If no waypoints (`_waypoints == null || _waypoints.Length == 0`): remain Idle, do nothing
     - Otherwise: call `AdvanceToNextWaypoint()` → transition to `Patrolling`
   - `HandlePatrol()`:
     - Player detection (every frame): if `_player != null` and distance ≤ `_config.detectionRange` → transition to `Engaging`
     - If agent path pending: return early
     - If `_agent.remainingDistance ≤ _config.waypointArrivalThreshold` and not path pending:
       - Decrement `_waitTimer`. If `_waitTimer ≤ 0`: call `AdvanceToNextWaypoint()` → stays `Patrolling`
       - Else: stop agent (isStopped = true) and wait
   - `HandleEngage()`:
     - If `_player == null` → transition to Patrolling
     - Check disengage distance: if > `_config.disengageRange` → transition back to Patrolling, log "Disengaged — player out of range"
     - Else: `_agent.SetDestination(_player.position)` (set every frame to track moving player)
   - `AdvanceToNextWaypoint()`:
     - Advance `_currentWaypoint` index (wrap around with `% _waypoints.Length`)
     - `_agent.isStopped = false`; `_agent.speed = _config.patrolSpeed`
     - `_agent.SetDestination(_waypoints[_currentWaypoint].position)`
     - `_waitTimer = _config.patrolWaitTime`
   - When transitioning to `Engaging`:
     - `_agent.isStopped = false`; `_agent.speed = _config.engageSpeed`
     - `_agent.SetDestination(_player.position)`
     - `GameLog.Info(TAG, "Enemy engaged player")`

5. `EnemyBrain.cs` OnGUI debug overlay (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=220:
   ```csharp
   string distStr = _player != null
       ? $"{Vector3.Distance(transform.position, _player.position):F1}m"
       : "?";
   GUI.Label(new Rect(10, 220, 500, 26),
       $"Enemy: {_state} | PlayerDist: {distStr} | DetectRange: {_config.detectionRange}m",
       style);
   ```
   Uses `new GUIStyle(GUI.skin.label) { fontSize = 18 }` — consistent with existing overlay pattern.
   **Note:** With multiple enemies in scene, each draws its own label at y=220 (overlapping). Acceptable for single-enemy prototype test.

6. Enemy prefab `Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab`:
   - Root GameObject `Enemy_Grunt` with:
     - `NavMeshAgent` component: Speed = 2 (set at runtime by EnemyBrain), Stopping Distance = 1.5, Angular Speed = 200
     - `EnemyBrain` component: `_config` = `AIConfig.asset`
     - `PersistentID` component: `_guid` = a real UUID (generated via Context Menu after placement)
   - Child GameObject `Visual`: a `Capsule` mesh (placeholder, 1.8m tall), centered at Y=0.9
   - Enemy layer: assign to `Default` for now (Enemy layer created in Epic 3 when stats are wired)
   - Tag: leave as `Untagged`

7. TestScene setup (manual editor operations — document clearly in story):
   - NavMesh baked: add `NavMeshSurface` component to the floor/ground plane → Bake via Inspector
   - One `Enemy_Grunt` prefab placed in scene
   - Three empty GameObjects named `Waypoint_0`, `Waypoint_1`, `Waypoint_2` placed in a patrol triangle
   - `EnemyBrain._waypoints` array wired to the three waypoint transforms in Inspector

8. Edit Mode tests `Assets/Tests/EditMode/EnemyBrainStateTests.cs` with ≥ 4 tests:
   - Helper: `bool ShouldEngage(float distToPlayer, float detectionRange)` → `distToPlayer <= detectionRange`
   - Helper: `bool ShouldDisengage(float distToPlayer, float disengageRange)` → `distToPlayer > disengageRange`
   - `ShouldEngage_ReturnsTrue_WhenWithinRange()` — 5m player, 8m range → true
   - `ShouldEngage_ReturnsFalse_WhenOutOfRange()` — 10m player, 8m range → false
   - `ShouldDisengage_ReturnsTrue_WhenTooFar()` — 15m player, 12m range → true
   - `ShouldDisengage_ReturnsFalse_WhenWithinRange()` — 10m player, 12m range → false

9. No compile errors. All existing 41 Edit Mode tests pass. New total: ≥ 45.

10. Play Mode validation:
    - Enemy patrols between waypoints, waiting briefly at each
    - Enemy detects player at ≤ 8m and chases (transitions to Engaging)
    - Enemy stops chasing at > 12m and returns to patrol (transitions back to Patrolling)
    - WorldStateManager persists across scene reloads (DontDestroyOnLoad)
    - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Create `AIConfigSO.cs` + asset (AC: 1)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs`
  - [x] 1.2 Create `Assets/_Game/Data/Config/AIConfig.asset` and populate default values

- [x] Task 2: Create `WorldStateManager.cs` + wire to Core scene (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Scripts/Core/WorldStateManager.cs` (minimal stub)
  - [x] 2.2 Add `WorldStateManager.cs` component to the existing `WorldStateManager` stub GameObject in `Core.unity`

- [x] Task 3: Create `PersistentID.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/World/PersistentID.cs`

- [x] Task 4: Create `EnemyBrain.cs` (AC: 4–5)
  - [x] 4.1 Create `Assets/_Game/Scripts/AI/EnemyBrain.cs` with Idle/Patrolling/Engaging state machine
  - [x] 4.2 Implement `HandlePatrol()` with arrival check, wait timer, waypoint advance
  - [x] 4.3 Implement `HandleEngage()` with disengage check and continuous destination update
  - [x] 4.4 Add `OnGUI` debug overlay at y=220

- [x] Task 5: Create Enemy_Grunt prefab (AC: 6)
  - [x] 5.1 Create `Assets/_Game/Prefabs/Enemies/` folder if it doesn't exist
  - [x] 5.2 Create `Enemy_Grunt.prefab` with NavMeshAgent, EnemyBrain, PersistentID, Capsule visual
  - [x] 5.3 Assign `AIConfig.asset` to EnemyBrain `_config` field

- [x] Task 6: TestScene setup — editor operations (AC: 7)
  - [x] 6.1 Add `NavMeshSurface` to the scene ground plane (AI Navigation package)
  - [x] 6.2 Bake NavMesh (click Bake in NavMeshSurface inspector)
  - [x] 6.3 Place `Enemy_Grunt` prefab in scene
  - [x] 6.4 Create 3 waypoint GameObjects, assign to EnemyBrain `_waypoints` array
  - [x] 6.5 Generate GUID for PersistentID via Context Menu → "Generate GUID"
  - [x] 6.6 Verify `WorldStateManager` stub in `Core.unity` has `WorldStateManager.cs` component attached

- [x] Task 7: Edit Mode tests (AC: 8)
  - [x] 7.1 Create `Assets/Tests/EditMode/EnemyBrainStateTests.cs` with ≥ 4 tests
  - [x] 7.2 Run all tests via Unity Test Runner — all 45+ green

- [x] Task 8: Play Mode validation (AC: 10) — requires Unity Editor (manual)
  - [ ] 8.1 Enemy patrols between waypoints with brief wait at each
  - [ ] 8.2 Walk player within 8m → enemy transitions to Engaging and chases
  - [ ] 8.3 OnGUI shows correct state and player distance
  - [ ] 8.4 Walk player beyond 12m → enemy stops chasing and returns to patrol
  - [ ] 8.5 No NullReferenceExceptions in console

## Dev Notes

Story 2.8 introduces the first enemy AI: a NavMesh-driven state machine with patrol and engagement behaviors. It also creates two foundational infrastructure scripts (`WorldStateManager` stub and `PersistentID`) required by the architecture for all future world entities. Story 2.9 (health system) will expand on these by adding enemy health, hit detection, and death registration.

### Critical: NavMesh Setup (AI Navigation Package 2.0.10)

The project already has `com.unity.ai.navigation` v2.0.10 installed (confirmed in `Packages/manifest.json`). This package is the standalone replacement for Unity's legacy NavigationMesh window.

**To bake a NavMesh in the test scene:**
1. Select the floor/ground plane GameObject in the scene
2. Add Component → `NavMeshSurface` (found under `AI Navigation`)
3. In the `NavMeshSurface` inspector: set `Agent Type` = Humanoid (default), `Collect Objects` = All Game Objects
4. Click **Bake** — a blue navmesh overlay should appear on the walkable floor

**NavMeshAgent on Enemy prefab:**
- `using UnityEngine.AI;` — NavMeshAgent lives in `UnityEngine.AI` namespace (built-in, not from the new package)
- `NavMeshAgent` must be on the same GameObject as `EnemyBrain`
- Set Stopping Distance = 1.5 so the agent stops before walking into the player
- `NavMeshAgent.isStopped = true/false` pauses/resumes movement
- `NavMeshAgent.SetDestination(Vector3)` — schedules an async pathfinding calculation; do NOT check `remainingDistance` on the same frame as `SetDestination`

**NavMesh agent path pending check:**
```csharp
// CORRECT pattern for arrival check — avoids false positives on first frame after SetDestination
if (!_agent.pathPending && _agent.remainingDistance <= _config.waypointArrivalThreshold)
{
    // arrived
}
```

### WorldStateManager Minimal Stub Pattern

```csharp
using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Central runtime state manager. Story 2.8: Minimal stub (singleton + kill tracking).
    /// Story 2.9 adds: OnEntityKilled event wiring.
    /// Epic 8: Save/Load, Steam Cloud sync.
    /// Attach to the WorldStateManager GameObject in Core.unity.
    /// </summary>
    public class WorldStateManager : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        public static WorldStateManager Instance { get; private set; }

        private readonly HashSet<string> _killedEntities = new HashSet<string>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                GameLog.Warn(TAG, "Duplicate WorldStateManager detected — destroying new instance");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool IsKilled(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "IsKilled called with null or empty GUID");
                return false;
            }
            return _killedEntities.Contains(guid);
        }

        public void RegisterKill(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                GameLog.Warn(TAG, "RegisterKill called with null or empty GUID");
                return;
            }
            _killedEntities.Add(guid);
            GameLog.Info(TAG, $"Entity killed: {guid}");
        }
    }
}
```

### PersistentID.cs Implementation

```csharp
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Marks a world entity as permanently tracked by WorldStateManager.
    /// On Awake: checks if this entity was previously killed and deactivates silently if so.
    /// Call RegisterDeath() when the entity is killed — before playing death effects.
    /// Every world enemy, NPC, and container MUST have this component with a unique GUID.
    /// Story 2.8: Initial implementation.
    /// </summary>
    public class PersistentID : MonoBehaviour
    {
        private const string TAG = "[WorldState]";

        [SerializeField] private string _guid;
        [SerializeField] private GameEventSO_String _onEntityKilled;

        private void Awake()
        {
            if (string.IsNullOrEmpty(_guid))
            {
                GameLog.Error(TAG, $"PersistentID on {gameObject.name} has no GUID — entity will not be tracked");
                return;
            }

            if (WorldStateManager.Instance == null)
            {
                GameLog.Warn(TAG, $"WorldStateManager not found — PersistentID check skipped for {gameObject.name}");
                return;
            }

            if (WorldStateManager.Instance.IsKilled(_guid))
            {
                gameObject.SetActive(false); // Silent — no events, no logging
            }
        }

        /// <summary>
        /// Call when this entity is killed. Registers the kill in WorldStateManager
        /// and raises the OnEntityKilled event channel if assigned.
        /// </summary>
        public void RegisterDeath()
        {
            WorldStateManager.Instance?.RegisterKill(_guid);

            if (_onEntityKilled != null)
                _onEntityKilled.Raise(_guid);
            else
                GameLog.Warn(TAG, $"OnEntityKilled event not assigned on {gameObject.name} — kill not broadcast");
        }

#if UNITY_EDITOR
        [ContextMenu("Generate GUID")]
        private void GenerateGUID()
        {
            _guid = System.Guid.NewGuid().ToString();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
```

### EnemyBrain.cs Complete Implementation

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Enemy state machine: Idle → Patrolling → Engaging.
    /// Patrol: cycles between waypoints, waits at each.
    /// Engage: chases player via NavMesh when within detectionRange; disengages if > disengageRange.
    /// Requires NavMeshAgent on same GameObject. AIConfigSO drives all tunable values.
    /// Story 2.8: Initial implementation (patrol + engage only; attack/health in Story 2.9).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyBrain : MonoBehaviour
    {
        private const string TAG = "[AI]";

        private enum EnemyState { Idle, Patrolling, Engaging }

        [SerializeField] private AIConfigSO _config;
        [SerializeField] private Transform[] _waypoints;

        private NavMeshAgent _agent;
        private Transform _player;
        private EnemyState _state = EnemyState.Idle;
        private int _currentWaypoint;
        private float _waitTimer;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                GameLog.Error(TAG, "NavMeshAgent not found — EnemyBrain disabled");
                enabled = false;
                return;
            }

            if (_config == null)
            {
                GameLog.Error(TAG, "AIConfigSO not assigned — EnemyBrain disabled");
                enabled = false;
                return;
            }

            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null)
                GameLog.Warn(TAG, "Player not found (tag 'Player') — enemy cannot engage");
            else
                _player = playerObj.transform;
        }

        private void Start()
        {
            if (_waypoints == null || _waypoints.Length == 0)
            {
                GameLog.Warn(TAG, $"{gameObject.name}: No waypoints assigned — remaining Idle");
                _state = EnemyState.Idle;
                return;
            }
            // Initialize to last index so the first AdvanceToNextWaypoint() lands at index 0.
            // Without this, AdvanceToNextWaypoint() would do (0+1)%N = 1, skipping waypoint 0.
            _currentWaypoint = _waypoints.Length - 1;
            AdvanceToNextWaypoint();
            _state = EnemyState.Patrolling;
        }

        private void Update()
        {
            switch (_state)
            {
                case EnemyState.Idle:      HandleIdle();    break;
                case EnemyState.Patrolling: HandlePatrol(); break;
                case EnemyState.Engaging:  HandleEngage();  break;
            }
        }

        private void HandleIdle()
        {
            // Nothing to do — enemy stays put. Will only move if waypoints are later assigned.
        }

        private void HandlePatrol()
        {
            // Check for player detection
            if (_player != null && Vector3.Distance(transform.position, _player.position) <= _config.detectionRange)
            {
                TransitionToEngaging();
                return;
            }

            // Wait for path to compute
            if (_agent.pathPending) return;

            // Check arrival at waypoint
            if (_agent.remainingDistance <= _config.waypointArrivalThreshold)
            {
                _agent.isStopped = true;
                _waitTimer -= Time.deltaTime;
                if (_waitTimer <= 0f)
                {
                    AdvanceToNextWaypoint();
                }
            }
        }

        private void HandleEngage()
        {
            if (_player == null)
            {
                GameLog.Warn(TAG, "Player lost — returning to patrol");
                TransitionToPatrol();
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, _player.position);
            if (distToPlayer > _config.disengageRange)
            {
                GameLog.Info(TAG, "Disengaged — player out of range");
                TransitionToPatrol();
                return;
            }

            _agent.SetDestination(_player.position);
        }

        private void AdvanceToNextWaypoint()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;

            _currentWaypoint = (_currentWaypoint + 1) % _waypoints.Length;
            _agent.isStopped = false;
            _agent.speed = _config.patrolSpeed;
            _agent.SetDestination(_waypoints[_currentWaypoint].position);
            _waitTimer = _config.patrolWaitTime;
        }

        private void TransitionToEngaging()
        {
            _state = EnemyState.Engaging;
            _agent.isStopped = false;
            _agent.speed = _config.engageSpeed;
            _agent.SetDestination(_player.position);
            GameLog.Info(TAG, $"{gameObject.name} engaged player");
        }

        private void TransitionToPatrol()
        {
            _state = EnemyState.Patrolling;
            AdvanceToNextWaypoint();
            GameLog.Info(TAG, $"{gameObject.name} returned to patrol");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            string distStr = _player != null
                ? $"{Vector3.Distance(transform.position, _player.position):F1}m"
                : "?";
            GUI.Label(new Rect(10, 220, 500, 26),
                $"Enemy: {_state} | PlayerDist:{distStr} | DetectRange:{_config.detectionRange}m",
                style);
        }
#endif
    }
}
```

### Edit Mode Test Pattern

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for EnemyBrain state transition logic.
/// Pure formula simulation — no MonoBehaviour lifecycle, no NavMesh.
/// Pattern mirrors DodgeGateTests, BlockGateTests.
/// </summary>
public class EnemyBrainStateTests
{
    private bool ShouldEngage(float distToPlayer, float detectionRange)
        => distToPlayer <= detectionRange;

    private bool ShouldDisengage(float distToPlayer, float disengageRange)
        => distToPlayer > disengageRange;

    [Test]
    public void ShouldEngage_ReturnsTrue_WhenWithinRange()
        => Assert.That(ShouldEngage(5f, 8f), Is.True);

    [Test]
    public void ShouldEngage_ReturnsFalse_WhenOutOfRange()
        => Assert.That(ShouldEngage(10f, 8f), Is.False);

    [Test]
    public void ShouldEngage_ReturnsTrue_WhenExactlyAtRange()
        => Assert.That(ShouldEngage(8f, 8f), Is.True);

    [Test]
    public void ShouldDisengage_ReturnsTrue_WhenTooFar()
        => Assert.That(ShouldDisengage(15f, 12f), Is.True);

    [Test]
    public void ShouldDisengage_ReturnsFalse_WhenWithinRange()
        => Assert.That(ShouldDisengage(10f, 12f), Is.False);

    [Test]
    public void ShouldDisengage_ReturnsFalse_WhenExactlyAtRange()
        => Assert.That(ShouldDisengage(12f, 12f), Is.False);
}
```

### Scene Note: Core.unity vs TestScene

The `WorldStateManager` stub GameObject already exists in `Core.unity` (created in Story 1.5 as empty GameObject). Task 2.2 requires attaching the new `WorldStateManager.cs` script to that GameObject.

**Core.unity WorldStateManager GameObject location:** This scene cannot be loaded via `manage_scene(action="load")` (known CLAUDE.md limitation — sub-folder scenes). Edit the `.unity` file directly or use `manage_gameobject` with the scene loaded in Unity Editor. Alternatively: work with Unity Editor directly to add the component.

The TestScene used for playtesting is `Assets/_Game/Scenes/TestScene.unity` (or whichever play scene is in use). The NavMesh bake, enemy prefab placement, and waypoints are all done in this play test scene — not in Core.unity.

### Waypoint Initialization: Start() vs Awake()

Note: `AdvanceToNextWaypoint()` is called from `Start()`, not `Awake()`. This is intentional:
- `Awake()` is for self-init (caching components, null-guard)
- `Start()` ensures all scene objects (including waypoint GameObjects) have completed their `Awake()` before the enemy starts moving
- This follows the established `Awake`/`Start` split rule in `project-context.md`

### Player Tag Requirement

`EnemyBrain` uses `GameObject.FindGameObjectWithTag("Player")` to locate the player at startup. Ensure the `Player` prefab root GameObject has its Tag set to **"Player"** in the Unity Inspector.

If the player tag is not set, the enemy will log a warning and remain in patrol-only mode (no engagement). This is the correct graceful degradation.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All scripts in `_Game/Scripts/` |
| AI namespace | ✅ `Game.AI` for `Scripts/AI/`, `Game.World` for `Scripts/World/` |
| No magic numbers | ✅ All values from `AIConfigSO` (patrolSpeed, engageSpeed, detectionRange, etc.) |
| GameLog only | ✅ All logging via `GameLog.Info/Warn/Error(TAG, ...)` |
| Null-guard in Awake | ✅ NavMeshAgent and AIConfigSO null-guarded with disable; player warn-only |
| No GetComponent in Update | ✅ All refs cached in Awake |
| No FindObjectOfType | ✅ Tag-based `FindGameObjectWithTag` used instead of deprecated `FindObjectOfType` |
| Event subscription in OnEnable/OnDisable | N/A — EnemyBrain has no event subscriptions this story |
| Same-system direct refs | ✅ EnemyBrain only references AIConfigSO and NavMeshAgent (same-component) |
| Cross-system comms via GameEventSO | ✅ PersistentID.RegisterDeath() raises `GameEventSO_String` channel |
| WorldStateManager singleton pattern | ✅ With DontDestroyOnLoad + duplicate guard |
| PersistentID on every enemy | ✅ Required on Enemy_Grunt prefab |
| Config SO for all tunable values | ✅ All AI values in AIConfigSO |
| Enemy layer/tag | ⚠️ Enemy layer setup deferred to Story 2.9 when stats/hit detection are wired |

### OnGUI Debug Display Stack (After This Story)

```
[y=50]   Stamina: 80 / 100                                             ← StaminaSystem
[y=70]   Combat: [Ready]                                               ← PlayerCombat stamina gate
[y=100]  Combo: step 0 | closed                                        ← PlayerCombat combo state
[y=130]  Block: lowered | PB: closed                                   ← PlayerCombat block + PB window
[y=160]  State: Airborne:False | Blocking:False | Attacking:False       ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                  ← DodgeController
[y=220]  Enemy: Patrolling | PlayerDist:10.2m | DetectRange:8m         ← EnemyBrain (NEW)
```

### Known Limitations (Prototype Acceptable)

1. **No attack state:** Story 2.8 enemy can detect and chase, but cannot deal damage. Story 2.9 adds `Attacking` state + `EnemyHealth.cs` + hit detection.
2. **Single enemy overlay:** If multiple enemies are in scene, their `OnGUI` labels overlap at y=220. Acceptable for single-enemy test — proper debug UI is Epic 8 scope.
3. **Tag-based player find:** `FindGameObjectWithTag("Player")` is a scene search in Awake. Fine for prototype. Future: inject player reference via a `PlayerRegistry` SO or GameEventSO channel.
4. **No enemy animations:** Placeholder capsule only. Actual enemy FBX, idle/walk/run/attack animations come in Epic 8.
5. **NavMesh manual bake:** Developer must bake NavMesh in editor. No runtime baking.

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs          ← NEW AI config SO class
Assets/_Game/Data/Config/AIConfig.asset                      ← NEW AI config asset
Assets/_Game/Scripts/Core/WorldStateManager.cs               ← NEW minimal stub
Assets/_Game/Scripts/World/PersistentID.cs                   ← NEW persistent entity marker
Assets/_Game/Scripts/AI/EnemyBrain.cs                        ← NEW enemy state machine
Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab              ← NEW enemy prefab
Assets/Tests/EditMode/EnemyBrainStateTests.cs                ← NEW Edit Mode tests
+ .meta files for all of the above (Unity auto-generates)
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/Core.unity           ← Add WorldStateManager.cs to existing stub GameObject
Assets/_Game/Scenes/TestScene.unity      ← Add NavMeshSurface + NavMesh bake + enemy prefab + waypoints
```

**Scripts/AI/ after this story:**
```
Assets/_Game/Scripts/AI/
└── EnemyBrain.cs                        ← NEW Story 2.8
```

**Scripts/World/ after this story:**
```
Assets/_Game/Scripts/World/
└── PersistentID.cs                      ← NEW Story 2.8
```

**Scripts/Core/ after this story:**
```
Assets/_Game/Scripts/Core/
├── GameLog.cs                           ← Story 1.x (unchanged)
├── GameConstants.cs                     ← Story 1.x (unchanged)
└── WorldStateManager.cs                 ← NEW Story 2.8 (minimal stub)
```

### References

- Epic 2 story 8 ("As a player, enemies patrol and engage me when I enter their range"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Epic 2 scope ("basic enemy AI (patrol, engage, attack)"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Architecture — Enemy AI components: [Source: _bmad-output/game-architecture.md#Directory Structure] → `Scripts/AI/EnemyBrain.cs`, `PatrolBehaviour.cs`, `CombatBehaviour.cs`, `EnemyHealth.cs`
- Architecture — AIConfigSO: [Source: _bmad-output/game-architecture.md#Configuration Management] → `AIConfigSO` for "patrol speed, engagement range, attack intervals"
- Architecture — Permanent Entity Pattern: [Source: _bmad-output/game-architecture.md#Novel Pattern 2: Permanent Entity Pattern]
- Architecture — WorldStateManager Decision 2: [Source: _bmad-output/game-architecture.md#Decision 2: World State Persistence]
- Architecture — State machine pattern: [Source: _bmad-output/game-architecture.md#State Transitions] → `EnemyState { Idle, Patrolling, Engaging, Attacking, Dead }`
- Architecture — GameEventSO cross-system comms: [Source: _bmad-output/game-architecture.md#Event System]
- Architecture — NavMesh for AI: [Source: _bmad-output/game-architecture.md#Engine-Provided Architecture] → "Unity NavMesh — Enemy patrol and engagement pathfinding"
- Project-context: No magic numbers rule: [Source: _bmad-output/project-context.md#Config & Data Anti-Patterns]
- Project-context: `FindFirstObjectByType` vs deprecated `FindObjectOfType`: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- Project-context: Logging rules: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- Project-context: Awake/Start split rule: [Source: _bmad-output/project-context.md#Engine-Specific Rules]
- Core.unity sub-folder load limitation: [Source: CLAUDE.md#Unity MCP Tool Quirks]
- AI Navigation package v2.0.10 installed: [Source: Packages/manifest.json]
- `com.unity.modules.ai` built-in (NavMeshAgent): [Source: Packages/manifest.json]
- GameEventSO_String (existing): [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO.cs]
- Story 2.7 File List (for audit reference): [Source: _bmad-output/implementation-artifacts/2-7-dodge-roll.md#File List]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- NavMeshBaker.cs temporarily placed at `Assets/Editor/NavMeshBaker.cs` (outside `Game.asmdef` scope) to access `Unity.AI.Navigation` assembly — bake executed via `Tools/Bake NavMesh Surfaces` menu item, scene auto-saved.
- Player tag was "Untagged" in TestScene — corrected to "Player" via MCP so `FindGameObjectWithTag("Player")` resolves correctly.
- `Scripts/AI/`, `Scripts/World/` folders already existed as empty stubs from Story 1.5.
- Task 6.2 NavMesh baked: 1 surface on `Floor` GameObject in TestScene.

### Completion Notes List

- **Task 1**: `AIConfigSO.cs` created at `Assets/_Game/ScriptableObjects/Config/` with `Game.AI` namespace. `AIConfig.asset` created at `Assets/_Game/Data/Config/` via MCP ScriptableObject manager with default values (patrolSpeed=2, engageSpeed=4, detectionRange=8, disengageRange=12, waypointArrivalThreshold=0.5, patrolWaitTime=2).
- **Task 2**: `WorldStateManager.cs` (singleton + kill tracking stub) created at `Assets/_Game/Scripts/Core/`. Component wired to `WorldStateManager` stub GameObject in `Core.unity` via direct YAML edit (sub-folder scene limitation).
- **Task 3**: `PersistentID.cs` created at `Assets/_Game/Scripts/World/` with GUID assignment, kill-check on Awake, RegisterDeath() method, and editor Context Menu for GUID generation.
- **Task 4**: `EnemyBrain.cs` created at `Assets/_Game/Scripts/AI/` — full Idle/Patrolling/Engaging state machine with NavMeshAgent integration, patrol waypoint cycling, detection/disengage logic, OnGUI debug overlay at y=220.
- **Task 5**: `Enemy_Grunt.prefab` created at `Assets/_Game/Prefabs/Enemies/` with NavMeshAgent (stoppingDistance=1.5, angularSpeed=200), EnemyBrain (_config=AIConfig.asset), PersistentID (GUID set), and `Visual` Capsule child at Y=0.9.
- **Task 6**: TestScene setup complete — NavMeshSurface added to Floor and baked; Enemy_Grunt placed (linked prefab instance); Waypoint_0 (5,0,5), Waypoint_1 (-5,0,5), Waypoint_2 (0,0,-5) created and wired to `_waypoints`; PersistentID GUID set to `a3f7e291-84b2-4c6d-9e15-3d8f2a1b0c5e`; Player tag corrected to "Player".
- **Task 7**: `EnemyBrainStateTests.cs` created with 6 tests (> AC minimum of 4). All 47 Edit Mode tests pass (41 prior + 6 new).
- **Task 8**: Play Mode validation — requires manual in-editor test (cannot run Play Mode AI/NavMesh behaviour via automated tests).

### File List

Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs
Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs.meta
Assets/_Game/Data/Config/AIConfig.asset
Assets/_Game/Data/Config/AIConfig.asset.meta
Assets/_Game/Scripts/Core/WorldStateManager.cs
Assets/_Game/Scripts/Core/WorldStateManager.cs.meta
Assets/_Game/Scripts/World/PersistentID.cs
Assets/_Game/Scripts/World/PersistentID.cs.meta
Assets/_Game/Scripts/AI/EnemyBrain.cs
Assets/_Game/Scripts/AI/EnemyBrain.cs.meta
Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab
Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab.meta
Assets/Tests/EditMode/EnemyBrainStateTests.cs
Assets/Tests/EditMode/EnemyBrainStateTests.cs.meta
Assets/_Game/Scenes/Core.unity (WorldStateManager.cs component added to WorldStateManager GameObject)
Assets/_Game/Scenes/TestScene.unity (NavMeshSurface + NavMesh bake + Enemy_Grunt + waypoints + Player tag)
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller (Dodge back AnimatorState + WriteDefaultValues fix — Story 2.7 leftover committed here)

## Change Log

- Story 2.8 implemented: AIConfigSO, WorldStateManager stub, PersistentID, EnemyBrain state machine, Enemy_Grunt prefab, NavMesh bake, patrol waypoints, 6 Edit Mode tests (Date: 2026-03-10)
- Code review fixes (Date: 2026-03-10): added WorldStateManager.OnDestroy singleton cleanup; extracted engageStoppingDistance to AIConfigSO; fixed HandlePatrol isStopped ordering; removed NavMeshBaker.cs + orphaned Editor.meta; documented PlayerAnimatorController.controller in File List
- Task 8 (Play Mode validation) pending manual verification by Valentin
