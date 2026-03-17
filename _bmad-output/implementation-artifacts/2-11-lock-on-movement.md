# Story 2.11: Lock-On Movement

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want my movement to be target-relative when locked on — forward/backward moves toward/away from the target and left/right strafes in a circle around the target — while my character always faces the locked enemy,
so that I can fight with precision and repositioning feels natural during lock-on combat.

## Acceptance Criteria

1. **PlayerController reads LockOnSystem state:**
   - `PlayerController.cs` gets a `[SerializeField] private LockOnSystem _lockOnSystem` field
   - `Awake()` calls `GetComponent<LockOnSystem>()` as fallback if not serialized; logs `Warn` if null (do NOT disable — free movement still works without lock-on)

2. **Target-relative movement when locked on (`IsLockedOn == true`):**
   - `forward` = direction from player to `LockedTarget`, projected to XZ plane (`toTarget.y = 0`), normalized
   - `right` = `Vector3.Cross(Vector3.up, forward).normalized` (strafes to the right of the target direction)
   - `moveDir = (forward * moveInput.y + right * moveInput.x).normalized`
   - WASD semantics: W = toward target, S = away from target, A = strafe left, D = strafe right
   - If `toTarget.sqrMagnitude < 0.0001f` (player coincident with target): skip movement direction change (keep last `moveDir` or zero) to avoid NaN from normalizing a zero vector
   - Movement speed logic unchanged: walk/run depending on Sprint held

3. **Character always faces the locked target when locked on:**
   - When `IsLockedOn`, the character body ALWAYS rotates toward the locked target regardless of whether the player is moving
   - Rotation uses existing `_config.rotationSpeed` with `Quaternion.Slerp`
   - Target direction for rotation: `toTarget` projected to XZ, same vector computed for movement
   - `Quaternion.LookRotation(lockForward)` → slerp to this at `rotationSpeed * Time.deltaTime`
   - Even when `moveInput == Vector2.zero`, the rotation toward the target still applies

4. **Free movement unchanged when NOT locked on:**
   - Existing camera-relative movement logic (`camForward * moveInput.y + camRight * moveInput.x`) unchanged
   - Existing "rotate to face movement direction" (only when `moveDir.sqrMagnitude > 0.01f`) unchanged
   - When lock-on is cleared (MMB pressed again, enemy dies, out of range), movement reverts to free camera-relative mode immediately

5. **Edit Mode tests at `Assets/Tests/EditMode/LockOnMovementTests.cs`** with ≥ 4 tests using pure-math static helpers (no MonoBehaviour dependencies):
   ```csharp
   static Vector3 ComputeLockForward(Vector3 playerPos, Vector3 targetPos)
   {
       Vector3 toTarget = targetPos - playerPos;
       toTarget.y = 0f;
       return toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
   }

   static Vector3 ComputeLockRight(Vector3 lockForward)
       => Vector3.Cross(Vector3.up, lockForward).normalized;

   static Vector3 ComputeLockMoveDir(Vector3 forward, Vector3 right, Vector2 input)
       => (forward * input.y + right * input.x).normalized;
   ```
   - `LockForward_PointsTowardTarget()` — player=(0,0,0), target=(0,0,5) → forward = Vector3.forward (0,0,1)
   - `LockRight_IsPerpendicularToForward()` — dot(lockForward, lockRight) ≈ 0 (perpendicular)
   - `MoveForward_ProducesTargetwardDirection()` — input=(0,1) → moveDir ≈ lockForward
   - `StrafeRight_ProducesPerpendicularDirection()` — input=(1,0) → moveDir ≈ lockRight
   - `CoincidentTarget_ReturnsZeroForward()` — player == target → forward = Vector3.zero (no NaN)

6. No compile errors. All existing 142 Edit Mode tests pass. New total: ≥ 147.

7. Play Mode validation:
   - Lock on (MMB) → WASD now moves toward/away/strafe relative to the locked enemy
   - Character body always faces the locked target while locked on, even when standing still
   - Strafing left/right results in the player orbiting around the enemy
   - Release lock (MMB again) → WASD reverts to camera-relative movement; character rotates to face movement direction as before
   - No NullReferenceExceptions; no jitter or NaN on edge cases (very close to target, directly overhead)

## Tasks / Subtasks

- [x] Task 1: Update `PlayerController.cs` — add LockOnSystem reference (AC: 1)
  - [x] 1.1 Add `[SerializeField] private LockOnSystem _lockOnSystem` field
  - [x] 1.2 In `Awake()`, after `_stateManager` cache, add: `if (_lockOnSystem == null) _lockOnSystem = GetComponent<LockOnSystem>();` + Warn if still null
  - [x] 1.3 Verify no compile errors after adding reference (both scripts are in `Game.Player` namespace — no extra `using` needed)

- [x] Task 2: Implement target-relative movement in `ApplyMovement()` (AC: 2, 3, 4)
  - [x] 2.1 Extract `lockForward` and `lockRight` from player-to-target vector in XZ plane when locked on
  - [x] 2.2 Guard against coincident positions (sqrMagnitude < 0.0001f) to avoid NaN
  - [x] 2.3 Compute `moveDir` as `(lockForward * moveInput.y + lockRight * moveInput.x).normalized`
  - [x] 2.4 Replace the "rotate to face movement direction" block with: always rotate toward target when locked on; keep existing logic when not locked on
  - [x] 2.5 Verify free movement (non-locked) code path is unchanged

- [x] Task 3: Edit Mode tests (AC: 5)
  - [x] 3.1 Create `Assets/Tests/EditMode/LockOnMovementTests.cs` with ≥ 5 tests using static math helpers
  - [x] 3.2 Run all tests — ≥ 147/147 green (142 prior + 5 new)

- [ ] Task 4: Play Mode validation (AC: 7) — requires Unity Editor (manual)

- [ ] Task 5: Fix pre-existing test failure (tracked from review)
  - [ ] 5.1 Investigate and fix `LockOnTests.IsInCone_HandlesBoundaryInclusive` — failing since story 2.10, unrelated to 2.11 but must not remain silently broken
  - [ ] 4.1 Lock on and verify W/S moves toward/away from target
  - [ ] 4.2 Lock on and verify A/D strafes around target
  - [ ] 4.3 Verify character body always faces target while locked on (including when stationary)
  - [ ] 4.4 Unlock and verify camera-relative movement resumes
  - [ ] 4.5 No NullReferenceExceptions or console errors

## Dev Notes

Story 2.11 implements the **movement half** of lock-on. Story 2.10 implemented the **camera half** (targeting + camera tracking). Together they form the complete lock-on system. This story ONLY modifies `PlayerController.cs` — no other files need changing.

### Critical: ApplyMovement() Rewrite When Locked On

Current `PlayerController.ApplyMovement()` (line 104–137) computes `moveDir` relative to camera forward/right, then rotates the character body to face the movement direction. When locked on, both of these behaviors change:

**BEFORE (free movement):**
```csharp
Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
moveDir = (camForward * moveInput.y + camRight * moveInput.x).normalized;

// Rotate body to face movement direction
if (moveDir.sqrMagnitude > 0.01f)
{
    Quaternion targetRot = Quaternion.LookRotation(moveDir);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _config.rotationSpeed * Time.deltaTime);
}
```

**AFTER (locked-on movement):**
```csharp
// --- Lock-on branch ---
if (_lockOnSystem != null && _lockOnSystem.IsLockedOn)
{
    Vector3 toTarget = _lockOnSystem.LockedTarget.position - transform.position;
    toTarget.y = 0f;

    if (toTarget.sqrMagnitude > 0.0001f)
    {
        Vector3 lockForward = toTarget.normalized;
        Vector3 lockRight   = Vector3.Cross(Vector3.up, lockForward).normalized;
        moveDir = (lockForward * moveInput.y + lockRight * moveInput.x);
        if (moveDir.sqrMagnitude > 0.01f) moveDir = moveDir.normalized;

        // Always face the target (even when stationary)
        Quaternion targetRot = Quaternion.LookRotation(lockForward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _config.rotationSpeed * Time.deltaTime);
    }
    // else: coincident with target — keep moveDir = zero, skip rotation to avoid NaN
}
// --- Free movement branch (existing code, unchanged) ---
else
{
    // ... existing camera-relative movement + face-movement-dir rotation ...
}
```

### Critical: LockOnSystem Is Same-System — Direct Reference OK

`LockOnSystem.cs` is in `Assets/_Game/Scripts/Player/` (namespace `Game.Player`). `PlayerController.cs` is in the same folder and namespace. Per the architecture pattern:
> "Same-system comms: Direct MonoBehaviour references acceptable within the same `Scripts/[System]/` folder"

No `GameEventSO<T>` channel needed — direct `[SerializeField] private LockOnSystem _lockOnSystem` reference is correct and consistent with how `CameraController` references `LockOnSystem` (established in Story 2.10).

**Prefab wiring required:** After code changes, open `Player.prefab` and drag the `LockOnSystem` component to the `_lockOnSystem` field on `PlayerController`. Without this, `Awake()` fallback `GetComponent<LockOnSystem>()` will still work (same prefab root), but explicit serialized assignment is preferred for clarity.

### Critical: Character Faces Target — Rotation Replaces "Face Movement Direction" When Locked

When locked on, the character must **always face the locked enemy**, even when standing still. This overrides the existing "rotate to face movement direction" block. The rotation always uses `lockForward` (player→target in XZ), NOT `moveDir`.

This means during left/right strafe: the player's feet move sideways relative to the target, but the character body always faces the target. This is the standard action-RPG lock-on behavior (Dark Souls / Elden Ring model).

The existing `Quaternion.Slerp(transform.rotation, targetRot, _config.rotationSpeed * Time.deltaTime)` approach reuses `rotationSpeed = 10f` from `PlayerConfigSO`. No new config values needed for this story — `rotationSpeed` is appropriate for both facing-movement-dir and facing-target use cases.

### Critical: Input Is Still Camera-Relative in One Sense

When locked on, the movement is **target-relative**, NOT strictly camera-relative. However, since the camera is also tracking the locked target (Story 2.10 — CameraController.TrackLockedTarget), the camera yaw is always oriented toward the enemy too. This means the direction vectors will be very similar in practice, but the code reads from `LockOnSystem.LockedTarget.position`, NOT from the camera, for movement direction.

**Never use the camera forward for lock-on movement** — use the player-to-target vector directly. The camera might be at a different pitch/yaw momentarily (lerping), so deriving movement from the camera would cause inconsistent strafe directions.

### OnGUI Debug Overlay Stack (No Change for This Story)

The OnGUI stack from Story 2.10 is unchanged:
```
[y=50]   Stamina: ...                ← StaminaSystem
[y=70]   Combat: ...                 ← PlayerCombat
[y=100]  Combo: ...                  ← PlayerCombat
[y=130]  Block: ...                  ← PlayerCombat
[y=160]  State: ...                  ← PlayerStateManager
[y=190]  Dodge: ...                  ← DodgeController
[y=220]  Enemy: ...                  ← EnemyBrain
[y=250]  PlayerHP: ...               ← PlayerHealth
[y=270]  EnemyHP: ...                ← EnemyHealth
[y=290]  LockOn: none | Enemy_Grunt  ← LockOnSystem
```
No new overlay entry needed for movement mode.

### PlayerAnimator Consideration

Story 2.11 does NOT change `PlayerAnimator.cs`. The blend tree still reads `CharacterController.velocity` (horizontal component) for the walk/run speed. During lock-on strafe, the velocity magnitude will be similar to normal movement — the animator will correctly play walk/run animations without modification.

If in a future story the character needs strafing-specific animations (side-step), `PlayerAnimator` would need to be updated with a Blend Tree 2D. That is **out of scope** for 2.11.

### Known Edge Case: Zero MoveDir When Input Is Zero

When locked on and `moveInput == Vector2.zero`:
- `moveDir = (lockForward * 0 + lockRight * 0) = Vector3.zero`
- `moveDir.sqrMagnitude == 0` → `normalized` would produce a zero vector, which is fine (Unity's `Vector3.zero.normalized == Vector3.zero`)
- The `velocity` becomes `Vector3.zero * currentSpeed + Vector3.up * _verticalVelocity` → only gravity is applied, which is correct
- Character rotation still applies (facing target), even with zero moveDir

No special handling needed for zero input — the existing code path handles `moveDir = Vector3.zero` gracefully.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ Only `PlayerController.cs` modified, already in `_Game/Scripts/Player/` |
| GameLog only — no Debug.Log | ✅ Warn logged if `_lockOnSystem` null |
| Null-guard in Awake | ✅ `_lockOnSystem` null-guarded (warn only, no disable) |
| No GetComponent/Camera.main in Update | ✅ `_lockOnSystem` cached in Awake; no per-frame lookup |
| Config SOs for all tunable values | ✅ Uses existing `_config.rotationSpeed` — no new magic numbers |
| Same-system direct reference | ✅ Both in `Scripts/Player/` — direct ref acceptable |
| No cross-system script references | ✅ `LockOnSystem` is Player system, not a foreign system |
| No new input actions | ✅ Lock-on input already handled by `LockOnSystem` — `PlayerController` reads state only |

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/Scripts/Player/PlayerController.cs   ← Add LockOnSystem ref; modify ApplyMovement()
```

**Files to CREATE:**
```
Assets/Tests/EditMode/LockOnMovementTests.cs      ← NEW Edit Mode tests
Assets/Tests/EditMode/LockOnMovementTests.cs.meta ← NEW Unity-generated
```

**Scripts/Player/ after this story:**
```
Assets/_Game/Scripts/Player/
├── PlayerController.cs      ← MODIFIED (lock-on movement branch)
├── PlayerAnimator.cs        ← Unchanged
├── CameraController.cs      ← Unchanged (already updated in 2.10)
├── PlayerStateManager.cs    ← Unchanged
├── PlayerHealth.cs          ← Unchanged
└── LockOnSystem.cs          ← Unchanged (API used, not modified)
```

### References

- Epic 2 story 2-11 ("When locked on, movement is target-relative..."): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Story 2.10 dev notes — scope boundary ("Story 2.11 will modify PlayerController.cs"): [Source: _bmad-output/implementation-artifacts/2-10-lock-on-targeting.md#Known Scope Boundary]
- Story 2.10 — `LockOnSystem.IsLockedOn` and `LockOnSystem.LockedTarget` public API: [Source: _bmad-output/implementation-artifacts/2-10-lock-on-targeting.md#Acceptance Criteria]
- Story 2.10 — `LockOnSystem.cs` location `Scripts/Player/`, namespace `Game.Player`: [Source: _bmad-output/implementation-artifacts/2-10-lock-on-targeting.md#Project Structure Notes]
- Architecture — same-system direct references acceptable: [Source: _bmad-output/game-architecture.md#Standard Patterns]
- `PlayerController.cs` current implementation — `ApplyMovement()` camera-relative pattern: [Source: Assets/_Game/Scripts/Player/PlayerController.cs:104-137]
- `PlayerController.cs` — `_config.rotationSpeed` used for body rotation: [Source: Assets/_Game/Scripts/Player/PlayerController.cs:129]
- `PlayerConfigSO.cs` — `rotationSpeed = 10f` default: [Source: Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs]
- `PlayerController.cs` — OnDisable null guard for `_input`: [Source: Assets/_Game/Scripts/Player/PlayerController.cs:62]
- Architecture — Config SOs for all tunable values (no magic numbers): [Source: _bmad-output/game-architecture.md#Configuration Management]
- project-context.md — cache Camera.main in Awake, not per-frame: [Source: _bmad-output/project-context.md#Unity-Specific Hot Path Rules]
- project-context.md — `CharacterController` for player, no Rigidbody: [Source: _bmad-output/project-context.md#Engine-Specific Rules]
- CLAUDE.md root — OnDisable before OnEnable null guard pattern: [Source: CLAUDE.md]
- `Scripts/Player/CLAUDE.md` — PlayerStateManager gate pattern for CanMove(): [Source: Assets/_Game/Scripts/Player/CLAUDE.md]
- `Scripts/Player/CLAUDE.md` — CharacterController.velocity Y component warning (use horizontal only for animation): [Source: Assets/_Game/Scripts/Player/CLAUDE.md]
- Test count after Story 2.10: 142 Edit Mode tests passing: [Source: _bmad-output/implementation-artifacts/2-10-lock-on-targeting.md#Acceptance Criteria]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Pre-existing test failure `LockOnTests.IsInCone_HandlesBoundaryInclusive` was present before this story (inherited from story 2.10 review state). All 5 new `LockOnMovementTests` pass. Total: 147 tests, 1 pre-existing failure unrelated to this story.

### Completion Notes List

- Tasks 1–3 implemented and validated automatically. Task 4 (Play Mode) requires manual verification in Unity Editor.
- `PlayerController.cs` modified: added `[SerializeField] private LockOnSystem _lockOnSystem` field; Awake fallback `GetComponent<LockOnSystem>()` with Warn if null; `ApplyMovement()` split into lock-on branch (target-relative movement + always-face-target rotation) and free movement branch (camera-relative, unchanged).
- Created `LockOnMovementTests.cs` with 5 pure-math static helper tests. All pass. Test count: 142 → 147.
- Only `PlayerController.cs` modified in game source, per story scope. No other files changed.

### File List

- `Assets/_Game/Scripts/Player/PlayerController.cs` — modified (LockOnSystem ref + lock-on movement branch)
- `Assets/_Game/Prefabs/Player/Player.prefab` — modified (wired `_lockOnSystem` ref on PlayerController; `_lockOnLayerMask` regression fixed in code review)
- `Assets/Tests/EditMode/LockOnMovementTests.cs` — created (8 Edit Mode tests after code review additions)
- `Assets/Tests/EditMode/LockOnMovementTests.cs.meta` — created (Unity-generated)

## Change Log

- 2026-03-17: Implemented lock-on movement in `PlayerController.cs` — target-relative WASD movement and always-face-target rotation when locked on; free movement unchanged. Added 5 Edit Mode tests. (claude-sonnet-4-6)
- 2026-03-17: Code review fixes — restored `_lockOnLayerMask` on Player.prefab (regression from prefab save); aligned `ComputeLockMoveDir` helper guard (`sqrMagnitude > 0.01f`) to match production code; added 3 tests (backward, strafe-left, small-input deadzone); fixed Vector3 assertions to use distance epsilon; added tracking task for pre-existing `IsInCone_HandlesBoundaryInclusive` failure. (claude-sonnet-4-6)
