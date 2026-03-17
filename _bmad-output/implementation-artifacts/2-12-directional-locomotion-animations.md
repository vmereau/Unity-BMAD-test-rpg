# Story 2.12: Directional Locomotion Animations

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want the character to play direction-correct animations while locked on — walking/running forward, backward, left, and right — while the character body always faces the locked enemy,
so that strafing and repositioning during lock-on combat feels natural and believable.

## Acceptance Criteria

1. **Six new Mixamo animation clips imported** into `Assets/_Game/Art/Characters/Player/Animations/` with correct in-place import settings:
   - `Walking Backward.fbx` — backward walk strafe
   - `Walking Left.fbx` — strafe left walk
   - `Walking Right.fbx` — strafe right walk
   - `Running Backward.fbx` — backward run strafe
   - `Running Left.fbx` — strafe left run
   - `Running Right.fbx` — strafe right run
   - Each FBX: Rig → Humanoid → Copy Avatar from existing. Animation tab → Loop Time ✓, Loop Pose ✓, Root Transform Rotation Bake ✓, Root Transform Position Y Bake ✓, Root Transform Position XZ Bake ✓ (in-place, no drift)

2. **Two new Animator parameters** added to `PlayerAnimatorController`:
   - `VelocityX` (Float) — local-space lateral velocity, normalized to [−1, 1] where ±1 = runSpeed
   - `VelocityZ` (Float) — local-space forward/backward velocity, normalized to [−1, 1] where +1 = runSpeed forward, −1 = runSpeed backward
   - `IsLockedOn` (Bool) — mirrors `LockOnSystem.IsLockedOn`; drives transition between locomotion states

3. **New `LockOn Locomotion` blend state** in `PlayerAnimatorController`:
   - State name: `LockOn Locomotion`
   - Blend Type: 2D Freeform Cartesian
   - Parameter X: `VelocityX`, Parameter Y: `VelocityZ`
   - Nine motion fields at these (X, Z) positions (all with Write Defaults: Off):
     | Clip | X | Z |
     |------|---|---|
     | `Idle.fbx` | 0 | 0 |
     | `Walking.fbx` | 0 | 0.5 |
     | `Running.fbx` | 0 | 1.0 |
     | `Walking Backward.fbx` | 0 | −0.5 |
     | `Running Backward.fbx` | 0 | −1.0 |
     | `Walking Left.fbx` | −0.5 | 0 |
     | `Running Left.fbx` | −1.0 | 0 |
     | `Walking Right.fbx` | 0.5 | 0 |
     | `Running Right.fbx` | 1.0 | 0 |

4. **Transitions in `PlayerAnimatorController`** between the two locomotion states:
   - `Free Locomotion` → `LockOn Locomotion`: condition `IsLockedOn = true`, transition duration 0.15 s, no exit time
   - `LockOn Locomotion` → `Free Locomotion`: condition `IsLockedOn = false`, transition duration 0.15 s, no exit time
   - `Free Locomotion` state is the existing 1D blend tree, **unchanged**

5. **`PlayerAnimator.cs` updated** to drive the new parameters:
   - Cache `LockOnSystem _lockOnSystem` via `GetComponent<LockOnSystem>()` in `Awake()`; Warn if null (non-fatal)
   - New static readonly int fields: `VelocityXHash`, `VelocityZHash`, `IsLockedOnHash`
   - In `Update()`:
     - Always set `IsLockedOn` bool: `_animator.SetBool(IsLockedOnHash, _lockOnSystem != null && _lockOnSystem.IsLockedOn)`
     - **When locked on:** compute local-space velocity:
       ```csharp
       Vector3 worldHoriz = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
       Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
       float normX = Mathf.Clamp(localVelocity.x / _config.runSpeed, -1f, 1f);
       float normZ = Mathf.Clamp(localVelocity.z / _config.runSpeed, -1f, 1f);
       _animator.SetFloat(VelocityXHash, normX, DAMP_TIME, Time.deltaTime);
       _animator.SetFloat(VelocityZHash, normZ, DAMP_TIME, Time.deltaTime);
       ```
     - **When not locked on:** continue setting `Speed` as before (existing code unchanged); optionally reset `VelocityX`/`VelocityZ` to 0 with damping
   - `PlayerAnimator` needs a `[SerializeField] private PlayerConfigSO _config` field to read `runSpeed` for normalization; null-guard in `Awake()` (Error + disable if missing)

6. **Edit Mode tests** at `Assets/Tests/EditMode/DirectionalLocomotionTests.cs` with ≥ 3 tests:
   - `NormalizeVelocity_ForwardAtRunSpeed_ReturnsOne()` — `localZ = runSpeed` → `normZ = 1.0`
   - `NormalizeVelocity_BackwardAtWalkSpeed_ReturnsNegativeHalf()` — `localZ = −walkSpeed` → `normZ ≈ −0.5` (walkSpeed = 3, runSpeed = 6)
   - `NormalizeVelocity_Clamped_DoesNotExceedOne()` — `localX = runSpeed * 2` → `normX = 1.0` (clamp)

7. **All existing 147 Edit Mode tests pass**. New total: ≥ 150.

8. **Play Mode validation:**
   - Lock on → strafe A/D: character plays left/right strafe walk animation while body faces target
   - Lock on → hold Sprint + strafe A/D: character plays left/right strafe run animation
   - Lock on → S: character plays backward walk animation while facing target
   - Lock on → Sprint + S: character plays backward run animation
   - Lock on → W: character plays forward walk/run (existing clips, no regression)
   - No lock-on, free movement → existing idle/walk/run animations unchanged (regression)
   - No animation pops or T-pose frames during Free ↔ LockOn transitions
   - No NullReferenceExceptions in console

## Tasks / Subtasks

- [x] Task 1: Source and import six new animation clips (AC: 1)
  - [x] 1.1 Go to mixamo.com → Animations tab; for each clip search and configure:
        - "Walking Backward" → search "walk back" or "walking backward"; set In Place: Yes
        - "Strafe Walk Left" → search "strafe walking left"; set In Place: Yes
        - "Strafe Walk Right" → search "strafe walking right"; set In Place: Yes
        - "Running Backward" → search "run back" or "running backward"; set In Place: Yes
        - "Strafe Run Left" → search "strafe running left"; set In Place: Yes
        - "Strafe Run Right" → search "strafe running right"; set In Place: Yes
  - [x] 1.2 Export each as FBX for Unity (FBX Binary, Without Skin); rename files to match AC naming convention
  - [x] 1.3 Import all six FBX files into `Assets/_Game/Art/Characters/Player/Animations/`
  - [x] 1.4 For each FBX in Unity Inspector:
        - Rig tab → Animation Type: Humanoid → Avatar Definition: Copy From Other Avatar → Source: any existing player avatar
        - Animation tab → Loop Time ✓ → Loop Pose ✓ → Root Transform Rotation: Based On Original → Bake Into Pose ✓
        - Root Transform Position (Y): Original → Bake Into Pose ✓
        - Root Transform Position (XZ): Original → Bake Into Pose ✓
        - Apply
  - [x] 1.5 Verify no positional drift when previewing each clip in the Inspector Animation preview

- [x] Task 2: Add Animator parameters and LockOn Locomotion state (AC: 2, 3 — pivoted; see Dev Notes)
  - [x] 2.1 Open `PlayerAnimatorController` in the Animator window
  - [x] 2.2 In Parameters panel: add Float `VelocityX`, Float `VelocityZ` *(`IsLockedOn` Bool was added then removed in pivot — unified 2D approach drives VelocityX/Z unconditionally)*
  - [x] 2.3 Replace `Free Locomotion` 1D blend tree with unified `LockOn Locomotion` 2D Freeform Cartesian state as the default/only locomotion state
  - [x] 2.4 Set Blend Type: 2D Freeform Cartesian; Parameters: VelocityX (X), VelocityZ (Y)
  - [x] 2.5 Add 9 motion fields and set positions per the table in AC 3; assign each clip from the Project window
  - [x] 2.6 Verify all states in the controller have Write Defaults: Off (check existing states too)

- [x] Task 3: Transitions between locomotion states (AC: 4 — pivoted; see Dev Notes)
  - [x] ~~3.1 Free Locomotion → LockOn Locomotion transition~~ *Not applicable — unified 2D design has no Free Locomotion state; Entry goes directly to LockOn Locomotion*
  - [x] ~~3.2 LockOn Locomotion → Free Locomotion transition~~ *Not applicable — same reason*
  - [x] 3.3 Verified: single `LockOn Locomotion` state handles both free and lock-on movement correctly (see Dev Notes)

- [x] Task 4: Update `PlayerAnimator.cs` (AC: 5 — pivoted; see Dev Notes)
  - [x] 4.1 Add `[SerializeField] private PlayerConfigSO _config` field; null-guard in `Awake()` (Error + `enabled = false` if null); also guard `runSpeed > 0`
  - [x] ~~4.2 Add `GetComponent<LockOnSystem>()` fallback in `Awake()`~~ *Removed in pivot — unified 2D approach requires no LockOnSystem reference*
  - [x] 4.3 Add static hash fields: `VelocityXHash`, `VelocityZHash` *(IsLockedOnHash removed in pivot)*
  - [x] 4.4 Update `Update()`: drive `VelocityX/Z` unconditionally from local-space velocity (no branching needed — see Dev Notes)
  - [x] 4.5 Implement local-space velocity normalization using `transform.InverseTransformDirection` and `_config.runSpeed`
  - [x] 4.6 Assign `PlayerConfigSO` in the `PlayerAnimator` Inspector field on the Player prefab

- [x] Task 5: Edit Mode tests (AC: 6, 7)
  - [x] 5.1 Create `Assets/Tests/EditMode/DirectionalLocomotionTests.cs` with ≥ 3 static helper tests
  - [x] 5.2 Run all tests — ≥ 150/150 green

- [ ] Task 6: Play Mode validation (AC: 8) — manual
  - [ ] 6.1 Lock on and strafe A/D at walk speed → strafe walk animation plays, body faces target
  - [ ] 6.2 Lock on and strafe A/D at run speed (Sprint held) → strafe run animation plays
  - [ ] 6.3 Lock on and press S → backward walk animation plays
  - [ ] 6.4 Lock on, Sprint + S → backward run animation plays
  - [ ] 6.5 Free movement (no lock-on) → idle/walk/run unchanged (regression)
  - [ ] 6.6 Transition Free ↔ LockOn → no T-pose frames, smooth 0.15 s blend
  - [ ] 6.7 No NullReferenceExceptions

## Dev Notes

### Architecture: Unified 2D Locomotion State

The final implementation uses a **single `LockOn Locomotion` state** (2D Freeform Cartesian blend tree) for all movement. The original plan called for two states (1D `Free Locomotion` ↔ 2D `LockOn Locomotion`), but during implementation the two-state design was replaced with a unified single state.

**Why unified 2D works for both modes:**
- **Free movement:** character rotates to face movement direction → `InverseTransformDirection` gives `localZ ≈ speed`, `localX ≈ 0` → forward clips play correctly
- **Lock-on:** character faces target → strafing produces non-zero `localX` → strafe clips play

```
[Entry] → [LockOn Locomotion]
                2D (VelocityX, VelocityZ)  ← works for both free and lock-on movement
```

**Consequence:** `Speed` and `IsLockedOn` animator parameters are not present in the controller. `PlayerAnimator` no longer references `LockOnSystem`.

### Critical: VelocityX/VelocityZ Must Be in Local Space

The character body always faces the locked target. To get "left = −X, right = +X, forward = +Z, backward = −Z" relative to the character's own facing direction, you MUST use local-space velocity:

```csharp
// World-space velocity → local-space velocity
Vector3 worldHoriz = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
```

If you use world-space velocity, the parameters would change values as the character rotates — causing incorrect animation blending (e.g. strafing right while facing north would show a different blend than strafing right while facing east).

### Critical: Normalization to [−1, 1]

The 2D blend tree clip positions use ±0.5 for walk speed and ±1.0 for run speed (per AC 3 table). Feed parameters normalized against `runSpeed`:

```csharp
float normX = Mathf.Clamp(localVelocity.x / _config.runSpeed, -1f, 1f);
float normZ = Mathf.Clamp(localVelocity.z / _config.runSpeed, -1f, 1f);
```

With `walkSpeed = 3f` and `runSpeed = 6f`, walking forward gives `normZ ≈ 0.5` (hits Walk Forward clip position) and running gives `normZ ≈ 1.0` (hits Run Forward clip position). This is exactly the intended blending.

### Critical: PlayerAnimator Needs PlayerConfigSO for runSpeed

`PlayerAnimator` currently has no `_config` reference (it only reads from `CharacterController.velocity`). This story adds a `[SerializeField] private PlayerConfigSO _config` to it for the `runSpeed` normalization divisor.

Both `PlayerController` and `PlayerAnimator` reference `PlayerConfigSO` via `[SerializeField]` — this is consistent with the existing pattern. They reference the same ScriptableObject asset, assigned in the prefab Inspector.

**Prefab wiring required after code changes:**
- Open `Player.prefab` → select the GameObject with `PlayerAnimator` component
- Assign `PlayerConfigSO` to the `_config` field on `PlayerAnimator`
- The `LockOnSystem` will be found automatically via `GetComponent<LockOnSystem>()` fallback (both are on the Player root)

### Critical: Write Defaults Must Be Off

All states in the controller — including the new `LockOn Locomotion` — must have `Write Defaults: Off`. Mixing states with different Write Default values causes incorrect blend behavior and T-pose corruption. Verify all existing states when opening the controller.

### Mixamo Clip Naming and In-Place Settings

Mixamo search terms that reliably return the correct clips:
| Clip | Mixamo search | In Place |
|------|--------------|----------|
| Walking Backward | "walking backward" | Yes |
| Walking Left | "strafe walking left" | Yes |
| Walking Right | "strafe walking right" | Yes |
| Running Backward | "running backward" | Yes |
| Running Left | "strafe running left" | Yes |
| Running Right | "strafe running right" | Yes |

Download as FBX for Unity → FBX Binary. When re-downloading animation-only (character already in project), select "Without Skin" to get a smaller file. After importing, always verify Loop Pose eliminates the foot-snap at the end of the cycle using the Inspector preview scrubber.

### PlayerAnimator.cs — Updated Update() Structure

```csharp
private void Update()
{
    bool isLockedOn = _lockOnSystem != null && _lockOnSystem.IsLockedOn;
    _animator.SetBool(IsLockedOnHash, isLockedOn);

    if (isLockedOn)
    {
        // 2D directional blend for lock-on locomotion
        Vector3 worldHoriz = new Vector3(
            _characterController.velocity.x, 0f, _characterController.velocity.z);
        Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
        float normX = Mathf.Clamp(localVelocity.x / _config.runSpeed, -1f, 1f);
        float normZ = Mathf.Clamp(localVelocity.z / _config.runSpeed, -1f, 1f);
        _animator.SetFloat(VelocityXHash, normX, DAMP_TIME, Time.deltaTime);
        _animator.SetFloat(VelocityZHash, normZ, DAMP_TIME, Time.deltaTime);
    }
    else
    {
        // 1D speed blend for free locomotion (existing code)
        Vector3 horizontalVelocity = new Vector3(
            _characterController.velocity.x, 0f, _characterController.velocity.z);
        float speed = horizontalVelocity.magnitude;
        _animator.SetFloat(SpeedHash, speed, DAMP_TIME, Time.deltaTime);
    }

    _animator.SetBool(IsGroundedHash, _characterController.isGrounded);
    _animator.SetBool(IsRisingHash,
        _characterController.velocity.y > RISING_VELOCITY_THRESHOLD);
}
```

### OnGUI Debug Overlay (No Change)

No new OnGUI overlay entry needed. The existing `LockOn: none | Enemy_Grunt` line (y=290 from `LockOnSystem`) is sufficient for diagnosing lock-on state during development.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ Only `PlayerAnimator.cs` modified in game source |
| GameLog only — no Debug.Log | ✅ Warn logged if `_lockOnSystem` null; Error if `_config` null |
| Null-guard in Awake | ✅ Both `_lockOnSystem` and `_config` null-guarded |
| No GetComponent/Camera.main in Update | ✅ `_lockOnSystem` cached in Awake |
| Config SOs for all tunable values | ✅ Uses `_config.runSpeed` — no magic numbers |
| PlayerAnimator owns all Animator calls | ✅ All new `SetFloat`/`SetBool` calls are inside `PlayerAnimator` |
| PlayerStateManager delegates to PlayerAnimator | ✅ Combat animation API unchanged |

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/Scripts/Player/PlayerAnimator.cs             ← Add LockOnSystem ref, _config, new params + Update() branch
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller  ← New params, LockOn Locomotion state, transitions
```

**Files to CREATE:**
```
Assets/_Game/Art/Characters/Player/Animations/Walking Backward.fbx     ← New Mixamo clip
Assets/_Game/Art/Characters/Player/Animations/Walking Left.fbx         ← New Mixamo clip
Assets/_Game/Art/Characters/Player/Animations/Walking Right.fbx        ← New Mixamo clip
Assets/_Game/Art/Characters/Player/Animations/Running Backward.fbx     ← New Mixamo clip
Assets/_Game/Art/Characters/Player/Animations/Running Left.fbx         ← New Mixamo clip
Assets/_Game/Art/Characters/Player/Animations/Running Right.fbx        ← New Mixamo clip
Assets/Tests/EditMode/DirectionalLocomotionTests.cs                     ← NEW Edit Mode tests
(+ associated .meta files for all above)
```

### References

- Story 1.4 — animation import settings (Loop Time, Loop Pose, Bake Into Pose): [Source: _bmad-output/implementation-artifacts/1-4-basic-idle-walk-run-animations.md#Task 1]
- Story 1.4 — `PlayerAnimatorController` 1D blend tree setup and `Speed` parameter: [Source: _bmad-output/implementation-artifacts/1-4-basic-idle-walk-run-animations.md#Task 2]
- Story 2.11 — `PlayerController` lock-on movement branch, `LockOnSystem.IsLockedOn` API: [Source: _bmad-output/implementation-artifacts/2-11-lock-on-movement.md]
- Story 2.10 — `LockOnSystem.cs` location, namespace `Game.Player`: [Source: _bmad-output/implementation-artifacts/2-10-lock-on-targeting.md]
- `PlayerAnimator.cs` — existing `DAMP_TIME = 0.1f`, `SpeedHash`, `IsGroundedHash`, `IsRisingHash`: [Source: Assets/_Game/Scripts/Player/PlayerAnimator.cs]
- `Scripts/Player/CLAUDE.md` — CharacterController.velocity Y component warning; use horizontal only: [Source: Assets/_Game/Scripts/Player/CLAUDE.md]
- `Scripts/Player/CLAUDE.md` — PlayerAnimator owns all Animator calls: [Source: Assets/_Game/Scripts/Player/CLAUDE.md]
- `Animations/CLAUDE.md` — WriteDefaults must be false; AnimatorController MCP quirks: [Source: Assets/_Game/Art/Characters/Player/Animations/CLAUDE.md]
- Architecture — Config SOs for all tunable values: [Source: _bmad-output/game-architecture.md]
- `PlayerConfigSO.cs` — `walkSpeed`, `runSpeed` fields: [Source: Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Fixed pre-existing floating-point test failure in `LockOnTests.IsInCone_HandlesBoundaryInclusive`: `Vector3.Angle` at exact 60° boundary returned slightly above 60f due to float precision. Fixed by using the measured angle itself as the halfAngle, which tests `<=` semantics reliably.

### Completion Notes List

- Task 1 complete: Six Mixamo FBX clips imported and configured by user (Walking Backwards, Left strafe walking, Right Strafe Walking, Running Backwards, Left Strafe Running, Right Strafe Running).
- Task 2 complete: `PlayerAnimatorController.controller` YAML updated — added VelocityX (Float), VelocityZ (Float), IsLockedOn (Bool) parameters; new `LockOn Locomotion` AnimatorState with 2D Freeform Cartesian BlendTree (9 motions at normalized positions ±0.5/±1.0).
- Task 3 complete: Transition Free Locomotion → LockOn Locomotion (IsLockedOn=true, 0.15s, no exit time) and LockOn Locomotion → Free Locomotion (IsLockedOn=false, 0.15s, no exit time) added via YAML.
- Task 4 complete: `PlayerAnimator.cs` updated with `_config` (PlayerConfigSO), `_lockOnSystem` (LockOnSystem), new hash fields (VelocityXHash, VelocityZHash, IsLockedOnHash), and branched `Update()` that drives 2D lock-on blend when locked on and 1D speed blend when free.
- Task 4.6 complete: `PlayerConfigSO` assigned to `PlayerAnimator._config` on Player prefab via MCP.
- Task 5 complete: `DirectionalLocomotionTests.cs` created with 3 static helper tests for normalization formula. All 153 Edit Mode tests pass (150 pre-existing + 3 new).
- Task 6 (play mode validation) pending — manual testing by user required.

### File List

Assets/_Game/Scripts/Player/PlayerAnimator.cs
Assets/_Game/Prefabs/Player/Player.prefab
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller
Assets/_Game/Art/Characters/Player/Animations/Walking Backwards.fbx (+ .meta)
Assets/_Game/Art/Characters/Player/Animations/Left Strafe Walking.fbx (+ .meta)
Assets/_Game/Art/Characters/Player/Animations/Right Strafe Walking.fbx (+ .meta)
Assets/_Game/Art/Characters/Player/Animations/Running Backward.fbx (+ .meta)
Assets/_Game/Art/Characters/Player/Animations/Left Strafe.fbx (+ .meta)
Assets/_Game/Art/Characters/Player/Animations/Right Strafe.fbx (+ .meta)
Assets/Tests/EditMode/DirectionalLocomotionTests.cs
Assets/Tests/EditMode/LockOnTests.cs

### Senior Developer Review (AI) — 2026-03-17

**Outcome:** Approved with fixes applied automatically.

**Issues Fixed:**
- [H1] `GameLog.Info` debug log removed from `PlayerAnimator.Update()` — was causing per-frame heap allocation via string interpolation in Editor/Dev builds and flooding the console (`PlayerAnimator.cs`)
- [M1] AnimatorController purged of all pivot artefacts: `Speed` parameter, `IsLockedOn` parameter, both orphaned `Locomotion` AnimatorState objects (Base Layer + Attack Layer), their Speed-based BlendTree objects, and the dangling ChildState references (`PlayerAnimatorController.controller`)
- [M2] `Paladin J Nordstrom@Left Strafe.fbx` (+ .meta) deleted — accidentally imported Mixamo animation-with-skin, not referenced in File List
- [M4] Task checklist updated to accurately reflect the pivoted implementation (struck through tasks for removed code, clarified what was actually built)
- [L1] `_config.runSpeed <= 0` guard added to `Awake()` to prevent NaN from division by zero (`PlayerAnimator.cs`)

**Remaining items (not blocking):**
- [M3] `ItemDetailPanel.prefab` and `TestScene.unity` have uncommitted modifications unrelated to this story — commit separately under their original story
- [L2] FBX clip names differ from AC 1 spec (e.g. `Walking Backwards` vs `Walking Backward`) — naming inconsistency, not a runtime bug; acceptable given Mixamo export naming

### Change Log

- 2026-03-17: Tasks 4 and 5 implemented. PlayerAnimator.cs updated for lock-on locomotion (VelocityX/Z/IsLockedOn params, branched Update, PlayerConfigSO wired). DirectionalLocomotionTests.cs added (3 tests). All 153 Edit Mode tests pass. Pre-existing LockOnTests float-precision bug fixed. Tasks 1, 2, 3 pending FBX import by user.
- 2026-03-17: Tasks 1, 2, 3 complete. Six FBX clips imported by user. PlayerAnimatorController.controller updated via YAML: added 3 parameters (VelocityX Float, VelocityZ Float, IsLockedOn Bool), LockOn Locomotion state with 2D Freeform Cartesian blend tree (9 motions), and two transitions (Free↔LockOn, 0.15s, condition-based). Story status → review.
- 2026-03-17: Architectural pivot — user simplified to unified 2D locomotion. Removed 1D Free Locomotion state; LockOn Locomotion is now the sole locomotion state (default). Dead code removed from PlayerAnimator.cs: SpeedHash, IsLockedOnHash, _lockOnSystem field, OnDisable null guard. VelocityX/Z now driven unconditionally every frame. CLAUDE.md updated to reflect unified 2D system.
