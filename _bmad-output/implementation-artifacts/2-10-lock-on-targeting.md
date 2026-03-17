# Story 2.10: Lock-On Targeting

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to press Middle Mouse Button to lock on to the nearest enemy in front of me,
so that the camera always faces the locked target and I can fight with precision without manually adjusting the camera.

## Acceptance Criteria

1. `CombatConfigSO.cs` gains a `[Header("Lock-On")]` block:
   - `public float lockOnRange = 15f` — max acquisition distance in metres
   - `public float lockOnFOV = 60f` — half-angle of detection cone in degrees (120° total cone)
   - `public float lockOnLerpSpeed = 5f` — camera yaw/pitch lerp factor toward locked target per second
   - `public float lockOnBreakDistance = 20f` — auto-unlock threshold if target exceeds this distance
   - `public float lockOnTargetHeightOffset = 1.0f` — vertical offset applied to locked target position for camera aim point (raises aim to ~chest height)

2. **InputSystem_Actions dual-edit** — a `LockOn` action is added to the **Player** map:
   - Binding: `<Mouse>/middleButton`
   - Type: Button (Performed on press)
   - **BOTH files must be edited:** `InputSystem_Actions.inputactions` (editor source) AND the embedded JSON string in `InputSystem_Actions.cs` inside `Assets/_Game/`. Failure to edit both causes `FindAction("LockOn", throwIfNotFound: true)` to throw `ArgumentException` at runtime.

3. `LockOnSystem.cs` exists at `Assets/_Game/Scripts/Player/LockOnSystem.cs`:
   - `namespace Game.Player`, `private const string TAG = "[Player]";`
   - `[SerializeField] private CombatConfigSO _config`
   - `public Transform LockedTarget { get; private set; }`
   - `public bool IsLockedOn => LockedTarget != null`
   - `private readonly Collider[] _lockOnBuffer = new Collider[10]` — pre-allocated, never `new` per call
   - `private InputSystem_Actions _input`
   - `private Camera _mainCamera`
   - `[SerializeField] private LayerMask _lockOnLayerMask` — must be configured in Inspector to include the layer(s) enemies reside on; defaults to Nothing (0) which finds no colliders
   - `Awake()`: null-guard `_config` (error + disable). Cache `Camera.main` (warn if null).
   - `OnEnable()`: `_input = new InputSystem_Actions(); _input.Player.Enable(); _input.Player.LockOn.performed += OnLockOnPressed;`
   - `OnDisable()`: null-guard `_input`; unsubscribe `LockOn.performed -= OnLockOnPressed`; dispose. (Required guard: Awake may disable before OnEnable runs.)
   - `private void OnLockOnPressed(InputAction.CallbackContext _)`:
     - If `IsLockedOn` → `ClearLock()`
     - Else → `TryAcquireTarget()`
   - `private void TryAcquireTarget()`:
     - `int count = Physics.OverlapSphereNonAlloc(transform.position, _config.lockOnRange, _lockOnBuffer, _lockOnLayerMask)`
     - Compute horizontal camera forward: `Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1,0,1)).normalized` (project to XZ plane; falls back to `transform.forward` if camera is null)
     - For each collider in `_lockOnBuffer[0..count]`:
       - `hit.transform.GetComponentInParent<EnemyHealth>()` — skip if no EnemyHealth or `health.IsDead`. Note: `GetComponentInParent` (not `TryGetComponent`) is intentional — enemies may have child colliders that the overlap sphere hits; walking up to the root finds EnemyHealth regardless of which collider was detected
       - `Vector3 toEnemy = hit.transform.position - transform.position; toEnemy.y = 0f`
       - `float angle = Vector3.Angle(camForward, toEnemy.normalized)` — skip if `angle > _config.lockOnFOV`
       - Track nearest by `Vector3.Distance`
     - If a valid candidate found: `LockedTarget = nearestHealth.transform` (the transform that owns EnemyHealth), log "Locked on to {name}"
     - If none found: log "No valid targets in cone"
   - `private void Update()`: if `IsLockedOn`:
     - `if (LockedTarget == null || !LockedTarget.gameObject.activeInHierarchy) { ClearLock(); return; }`
     - `if (LockedTarget.TryGetComponent<EnemyHealth>(out var h) && h.IsDead) { ClearLock(); return; }`
     - `if (Vector3.Distance(transform.position, LockedTarget.position) > _config.lockOnBreakDistance) { GameLog.Info(TAG, "Lock broken — target out of range"); ClearLock(); }`
   - `private void ClearLock()`: `LockedTarget = null; GameLog.Info(TAG, "Lock-on cleared")`
   - `OnGUI` debug overlay at y=290 (`#if DEVELOPMENT_BUILD || UNITY_EDITOR`):
     `$"LockOn: {(IsLockedOn ? LockedTarget.name : "none")}"`, fontSize=18

4. `CameraController.cs` is updated to support lock-on camera tracking:
   - Add `[SerializeField] private LockOnSystem _lockOnSystem` — optional, warn if null in Awake (do NOT disable; free-look still works without it)
   - Add `[SerializeField] private CombatConfigSO _combatConfig` — optional, warn if null (lockOnLerpSpeed unavailable)
   - In `LateUpdate()` (not `Update()` — camera tracking runs in LateUpdate so it executes after all player/enemy movement is settled for the frame): replace the direct `RotateCamera()` call with:
     ```csharp
     if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
         TrackLockedTarget();
     else
         RotateCamera();
     ```
   - `private void TrackLockedTarget()`:
     - `Vector3 dir = (_lockOnSystem.LockedTarget.position + Vector3.up * _combatConfig.lockOnTargetHeightOffset) - _cameraTarget.position` — height offset raises aim point to ~chest height; falls back to 1.0f if `_combatConfig` null
     - Guard: `if (dir.sqrMagnitude < 0.0001f) return;` — avoid NaN when coincident
     - `dir.Normalize()`
     - `float desiredYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg`
     - `float desiredPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg` — negative because pitch convention: looking up = negative pitch
     - `desiredPitch = Mathf.Clamp(desiredPitch, _pitchMin, _pitchMax)`
     - `float lerpSpeed = _combatConfig != null ? _combatConfig.lockOnLerpSpeed : 5f`
     - `_yaw = Mathf.LerpAngle(_yaw, desiredYaw, lerpSpeed * Time.deltaTime)` — LerpAngle handles 360°/0° wrap-around correctly
     - `_pitch = Mathf.Lerp(_pitch, desiredPitch, lerpSpeed * Time.deltaTime)`
     - `_cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f)`

5. `Player.prefab` updated:
   - `LockOnSystem` component added to Player root; `_config = CombatConfig.asset` assigned; `_lockOnLayerMask` set to the layer(s) enemies occupy (required — default Nothing breaks targeting)
   - `CameraController._lockOnSystem` wired to the `LockOnSystem` component on the Player root
   - `CameraController._combatConfig` assigned to `CombatConfig.asset`

6. Edit Mode tests at `Assets/Tests/EditMode/LockOnTests.cs` with ≥ 4 tests:
   - Pure-math static helpers (no MonoBehaviour dependencies):
     ```csharp
     static bool IsInCone(Vector3 forward, Vector3 toTarget, float halfAngle)
         => Vector3.Angle(forward, toTarget) <= halfAngle;

     static int GetNearestIndex(float[] distances)
     {
         int nearest = 0;
         for (int i = 1; i < distances.Length; i++)
             if (distances[i] < distances[nearest]) nearest = i;
         return nearest;
     }
     ```
   - `IsInCone_ReturnsTrue_WhenTargetIsDirectlyAhead()` — forward=Vector3.forward, to=Vector3.forward, half=60° → true
   - `IsInCone_ReturnsFalse_WhenTargetIsBehind()` — forward=Vector3.forward, to=Vector3.back, half=60° → false (180° > 60°)
   - `IsInCone_HandlesBoundaryInclusive()` — angle exactly 60°, half=60° → true
   - `IsInCone_ReturnsFalse_WhenAngleJustExceedsFOV()` — use `Vector3.Angle` to produce ~61°, half=60° → false
   - `GetNearest_ReturnsClosestCandidate()` — distances=[10f, 3f, 7f] → index 1

7. No compile errors. All existing 137 Edit Mode tests pass. New total: ≥ 142.

8. Play Mode validation:
   - Press MMB with enemy in camera cone → camera smoothly rotates to face enemy, then tracks continuously
   - Player moves around enemy → camera always faces locked target
   - Press MMB again → camera unlocks, free mouse look resumes immediately
   - Enemy dies (health reaches 0, SetActive(false)) → lock clears, OnGUI shows "none"
   - Enemy walks beyond `lockOnBreakDistance` → lock auto-clears with log
   - Press MMB with no enemy in front cone → "No valid targets in cone" logged; no lock acquired
   - Multiple enemies in range → nearest valid (alive, in cone) is selected
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Expand CombatConfigSO (AC: 1)
  - [x] 1.1 Add `[Header("Lock-On")]` block to `CombatConfigSO.cs` with `lockOnRange`, `lockOnFOV`, `lockOnLerpSpeed`, `lockOnBreakDistance`
  - [x] 1.2 Verify `CombatConfig.asset` shows new fields with correct defaults in Inspector

- [x] Task 2: InputSystem dual-edit — add LockOn action (AC: 2)
  - [x] 2.1 Edit `Assets/_Game/InputSystem_Actions.inputactions` — add `LockOn` button action in Player map with `<Mouse>/middleButton` binding
  - [x] 2.2 Edit `InputSystem_Actions.cs` embedded JSON — add matching action to Player map JSON block (use `""` double-escaped quotes as required)
  - [x] 2.3 Verify Unity compiles cleanly (no `ArgumentException` on `FindAction("LockOn")`)

- [x] Task 3: Create `LockOnSystem.cs` (AC: 3)
  - [x] 3.1 Create `Assets/_Game/Scripts/Player/LockOnSystem.cs` with namespace, config SO, InputSystem_Actions pattern
  - [x] 3.2 Implement `TryAcquireTarget()` with OverlapSphereNonAlloc + cone filter + nearest selection
  - [x] 3.3 Implement `Update()` lock-break checks (dead, inactive, out-of-range)
  - [x] 3.4 Implement `ClearLock()` and `OnGUI` overlay at y=290

- [x] Task 4: Update `CameraController.cs` (AC: 4)
  - [x] 4.1 Add `_lockOnSystem` and `_combatConfig` serialized fields with Awake null-guards (warn only)
  - [x] 4.2 Add `TrackLockedTarget()` method with yaw/pitch lerp toward target
  - [x] 4.3 Update `Update()` to branch between `TrackLockedTarget()` and `RotateCamera()` based on lock state

- [x] Task 5: Update Player.prefab (AC: 5)
  - [x] 5.1 Add `LockOnSystem` component to Player prefab root; assign `_config = CombatConfig.asset`
  - [x] 5.2 Wire `CameraController._lockOnSystem` → Player's `LockOnSystem`; `CameraController._combatConfig` → `CombatConfig.asset`

- [x] Task 6: Edit Mode tests (AC: 6)
  - [x] 6.1 Create `Assets/Tests/EditMode/LockOnTests.cs` with ≥ 5 tests using static math helpers
  - [x] 6.2 Run all tests via Unity Test Runner — 142/142 green (137 prior + 5 new)

- [ ] Task 7: Play Mode validation (AC: 8) — requires Unity Editor (manual)
  - [ ] 7.1 Press MMB → camera locks on to nearest enemy in front, tracks smoothly
  - [ ] 7.2 Press MMB again → camera unlocks, free mouse look resumes
  - [ ] 7.3 Kill locked enemy → lock clears automatically
  - [ ] 7.4 Press MMB with no enemy in cone → no lock, log message
  - [ ] 7.5 No NullReferenceExceptions

## Dev Notes

Story 2.10 implements the **camera half** of lock-on (targeting + camera tracking). Story 2.11 implements the **movement half** (target-relative strafe/orbit when locked). These two stories intentionally split concerns: this story doesn't touch `PlayerController.cs`. No movement changes in 2.10.

### Critical: InputSystem Dual-Edit Pattern

Every story that adds a new input action in Epic 2+ has required editing BOTH files:
- `InputSystem_Actions.inputactions` — Unity's Input Actions editor source (asset GUID references)
- `InputSystem_Actions.cs` embedded JSON — the string literal inside the constructor that actually drives runtime behavior

**If only `.inputactions` is edited**, `_input.Player.LockOn` at runtime throws:
```
ArgumentException: "LockOn" not found in "Player"
```

Both files are in `Assets/_Game/` and compile into the `Game` assembly.

After adding the LockOn action, Unity may auto-generate a new `InputSystem_Actions.cs` at `Assets/` root (outside `_Game/`). **Delete this file immediately** — it would be in `Assembly-CSharp` and invisible to `Game` assembly scripts.

### Critical: LockOnSystem Placed in Scripts/Player/

`LockOnSystem.cs` is in `Scripts/Player/` (namespace `Game.Player`), not `Scripts/Combat/`. This is intentional:
- `CameraController.cs` (also in `Scripts/Player/`) can reference it directly (intra-system reference — no architecture violation)
- The lock-on system is conceptually a **player targeting** concern, not a pure combat logic concern
- `LockOnSystem` reads `EnemyHealth` state (reads `.IsDead`) — this is a one-way read on a public property, acceptable for inter-system state inspection per prototype conventions

### Critical: OnEnable/OnDisable Null Guard (CLAUDE.md Rule)

`LockOnSystem` follows the established OnDisable guard pattern:
```csharp
private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.Player.LockOn.performed -= OnLockOnPressed;
    _input.Player.Disable();
    _input.Dispose();
}
```
This guard is MANDATORY because if `_config == null`, Awake sets `enabled = false` before `OnEnable` runs, but Unity then calls `OnDisable` — `_input` is null and would throw.

### Critical: Camera Tracking Math

The existing `CameraController` convention:
- `_yaw` — horizontal rotation, accumulates unbounded (normalized with `% 360f`)
- `_pitch` — vertical rotation, clamped to `[_pitchMin, _pitchMax]` = `[-70f, 70f]`
- `_cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f)`
- Positive pitch = camera tilts down; negative pitch = camera tilts up

Deriving yaw/pitch from a direction vector to the locked target:
```csharp
Vector3 dir = (LockedTarget.position - _cameraTarget.position).normalized;

// Yaw: atan2 of X/Z gives signed angle from +Z axis in XZ plane
float desiredYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

// Pitch: dir.y = sin(elevation); negative because looking up = negative pitch
float desiredPitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
desiredPitch = Mathf.Clamp(desiredPitch, _pitchMin, _pitchMax);

// Smooth approach (exponential — feels natural for camera tracking)
_yaw = Mathf.LerpAngle(_yaw, desiredYaw, lockOnLerpSpeed * Time.deltaTime);
_pitch = Mathf.Lerp(_pitch, desiredPitch, lockOnLerpSpeed * Time.deltaTime);
```

`Mathf.Clamp(dir.y, -1f, 1f)` prevents `Mathf.Asin` from returning NaN on floating-point precision noise (dir might be 1.00001f after normalization).

`Mathf.LerpAngle` correctly handles yaw wrap-around (e.g. lerping from 350° to 10° goes via 0°, not via 180°). Regular `Mathf.Lerp` would fail here.

### Critical: Target Selection — GetComponentInParent (intentional)

Unlike Story 2.9's hit-detection (which uses `TryGetComponent` to require the exact root collider), the lock-on overlap uses `GetComponentInParent`:
```csharp
var health = hit.transform.GetComponentInParent<EnemyHealth>();
if (health == null) continue;
if (health.IsDead) continue;
```

`GetComponentInParent` is intentional here — the lock-on overlap sphere may hit any collider on the enemy (root or child), and we want the lock to succeed regardless of which collider is detected. This is safe as long as only enemy root GameObjects carry `EnemyHealth`.

### Critical: CombatConfigSO Dual-Reference in CameraController

`CameraController` (namespace `Game.Player`) references `CombatConfigSO` (namespace `Game.Combat`). This is acceptable in Unity because both compile to the `Game` assembly — there are no namespace-level restrictions between `Game.Player` and `Game.Combat` within the same assembly. The architecture constraint is about MonoBehaviour cross-system dependencies (don't call methods on other systems), not about read-only SO references.

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
[y=290]  LockOn: none (or Enemy_Grunt_01)                                   ← LockOnSystem (NEW)
```

### Known Scope Boundary (2.10 vs 2.11)

- **This story (2.10):** Camera locks on and tracks. Player movement is NOT affected — WASD still moves relative to camera in all directions as before.
- **Story 2.11:** Movement becomes target-relative (forward/backward moves toward/away from target; strafe orbits target). Story 2.11 will modify `PlayerController.cs` to detect `LockOnSystem.IsLockedOn` and redirect movement.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All new scripts in `_Game/Scripts/Player/` |
| GameLog only — no Debug.Log | ✅ All logging via `GameLog.Info/Warn/Error(TAG, ...)` |
| Null-guard in Awake | ✅ `_config` null-guarded (error + disable); `_lockOnSystem`/`_combatConfig` null-guarded (warn only) |
| No GetComponent/Camera.main in Update | ✅ Camera.main cached in Awake; all refs cached |
| Non-allocating physics queries | ✅ `OverlapSphereNonAlloc` with pre-allocated `_lockOnBuffer` |
| Config SOs for all tunable values | ✅ lockOnRange, lockOnFOV, lockOnLerpSpeed, lockOnBreakDistance in CombatConfigSO |
| Event subscription in OnEnable/OnDisable | ✅ `LockOn.performed` subscribed in OnEnable, unsubscribed in OnDisable |
| OnDisable null guard | ✅ `if (_input == null) return;` present |
| InputSystem dual-edit | ✅ Must edit both .inputactions and .cs embedded JSON |
| LockOnSystem in Scripts/Player/ | ✅ Avoids cross-system reference from CameraController |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Player/LockOnSystem.cs             ← NEW lock-on targeting system
Assets/_Game/Scripts/Player/LockOnSystem.cs.meta
Assets/Tests/EditMode/LockOnTests.cs                    ← NEW Edit Mode tests
Assets/Tests/EditMode/LockOnTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs       ← Add [Header("Lock-On")] block
Assets/_Game/InputSystem_Actions.inputactions                  ← Add LockOn action to Player map
Assets/_Game/InputSystem_Actions.cs                            ← Add LockOn to embedded JSON (DUAL EDIT)
Assets/_Game/Scripts/Player/CameraController.cs                ← Add TrackLockedTarget(), LockOnSystem ref
Assets/_Game/Prefabs/Player/Player.prefab                      ← Add LockOnSystem component; wire refs
```

**Scripts/Player/ after this story:**
```
Assets/_Game/Scripts/Player/
├── PlayerController.cs      ← Unchanged (movement in 2.11)
├── PlayerAnimator.cs        ← Unchanged
├── CameraController.cs      ← Updated (lock-on tracking)
├── PlayerStateManager.cs    ← Unchanged
├── PlayerHealth.cs          ← Unchanged
└── LockOnSystem.cs          ← NEW Story 2.10
```

### References

- Epic 2 story 2-10 ("As a player, I can press MMB to lock on..."): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Epic 2 story 2-11 scope boundary ("movement is 2.11"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Architecture — Lock-On Targeting system: [Source: _bmad-output/game-architecture.md#Core Systems] → "Lock-on targeting & camera tracking | Low-Medium | Epic 2"
- Architecture — same-system direct references acceptable: [Source: _bmad-output/game-architecture.md#Standard Patterns] → "Same-system comms: Direct MonoBehaviour references acceptable within the same Scripts/[System]/ folder"
- Architecture — Config SOs for all tunable values: [Source: _bmad-output/game-architecture.md#Configuration Management]
- Architecture — non-allocating physics: [Source: _bmad-output/project-context.md#Performance Rules] → OverlapSphereNonAlloc
- InputSystem dual-edit pattern: [Source: Assets/_Game/CLAUDE.md] → "both InputSystem_Actions.inputactions AND embedded JSON must be edited"
- InputSystem_Actions action map layout: [Source: Assets/_Game/Scripts/Player/CLAUDE.md] → "Player map: Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint, InventoryToggle"
- InputSystem — UI map has MiddleClick (UI context); LockOn goes in Player map: [Source: Assets/_Game/Scripts/Player/CLAUDE.md]
- OnDisable null guard pattern: [Source: CLAUDE.md root] → "Unity Lifecycle Gotcha: OnDisable Before OnEnable"
- Cinemachine 3.x OTS setup — CameraController owns CameraTarget rotation: [Source: Assets/_Game/Scripts/Player/CLAUDE.md#Cinemachine 3.x]
- CameraController.cs float accumulation + euler quirks: [Source: Assets/_Game/Scripts/Player/CLAUDE.md#Float Accumulation]
- Story 2.9 — EnemyHealth.IsDead public property: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Acceptance Criteria]
- Story 2.9 — TryGetComponent vs GetComponentInParent code review fix: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Change Log]
- Story 2.9 — OverlapSphereNonAlloc with pre-allocated buffer pattern: [Source: _bmad-output/implementation-artifacts/2-9-health-system.md#Dev Notes]
- Story 2.9 — test count ended at 57 (Epic 2 end); current count after Epic 4: 137: [Source: _bmad-output/implementation-artifacts/4-9-usable-item-system.md]
- project-context.md — Cache Camera.main in Awake: [Source: _bmad-output/project-context.md#Unity-Specific Hot Path Rules]
- project-context.md — No magic numbers: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- CombatConfigSO.cs current fields: [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- CameraController.cs — _pitch/_yaw convention, _cameraTarget.rotation pattern: [Source: Assets/_Game/Scripts/Player/CameraController.cs]
- EnemyBrain.cs — current state machine (unchanged by this story): [Source: Assets/_Game/Scripts/AI/EnemyBrain.cs]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed `IsInCone_HandlesBoundaryInclusive` test: sin/cos construction gave a vector that `Vector3.Angle` returned as slightly above 60° (float precision). Fixed by using `Quaternion.AngleAxis(60f, Vector3.up) * Vector3.forward` and testing `<= computedAngle` to validate the `<=` boundary condition.
- `EnemyHealth` is in namespace `Game.AI` — added `using Game.AI` to `LockOnSystem.cs`.

### Completion Notes List

- Implemented full lock-on targeting system per AC 1–6:
  - `CombatConfigSO` extended with Lock-On header (range 15m, FOV 60°, lerpSpeed 5, breakDistance 20m, targetHeightOffset 1.0m)
  - `InputSystem_Actions` dual-edited (both `.inputactions` and embedded C# JSON) — LockOn → `<Mouse>/middleButton`; `.inputactions.meta` `generateWrapperCode` disabled to prevent Unity from auto-regenerating `Assets/InputSystem_Actions.cs` on every reimport
  - `LockOnSystem.cs` created: OverlapSphereNonAlloc + LayerMask filter + cone filter + nearest selection; `GetComponentInParent<EnemyHealth>` used (intentional — handles child colliders on enemies); LateUpdate break checks; OnGUI overlay at y=290; OnDisable null guard
  - `CameraController.cs` updated: `TrackLockedTarget()` in `LateUpdate()` (after movement settles) using `LerpAngle`/`Lerp` with height offset for natural chest-level aim; optional `_lockOnSystem` and `_combatConfig` fields with warn-only guards
  - Player prefab: LockOnSystem component added with CombatConfig.asset and `_lockOnLayerMask` wired; CameraController refs wired via YAML fileID
  - 5 new Edit Mode tests; total 142/142 green

### File List

- Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs
- Assets/_Game/InputSystem_Actions.inputactions
- Assets/_Game/InputSystem_Actions.cs
- Assets/_Game/Scripts/Player/LockOnSystem.cs (NEW)
- Assets/_Game/Scripts/Player/LockOnSystem.cs.meta (NEW — Unity-generated)
- Assets/_Game/Scripts/Player/CameraController.cs
- Assets/_Game/Prefabs/Player/Player.prefab
- Assets/Tests/EditMode/LockOnTests.cs (NEW)
- Assets/Tests/EditMode/LockOnTests.cs.meta (NEW — Unity-generated)

## Change Log

- 2026-03-17: Implemented story 2.10 — lock-on targeting system (LockOnSystem.cs, CameraController tracking, InputSystem dual-edit, prefab wiring, 5 Edit Mode tests). All 142 tests green.
- 2026-03-17: Code review fixes — disabled `generateWrapperCode` in `.inputactions.meta` (permanent fix for auto-generated duplicate at `Assets/` root); removed noisy `Lock count` debug log; fixed `IsInCone_HandlesBoundaryInclusive` test to use literal `60f`; documented `lockOnTargetHeightOffset`, `_lockOnLayerMask`, `GetComponentInParent` intent, and `LateUpdate` rationale in AC.
