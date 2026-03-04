# Story 1.6: Jump

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to jump,
so that I can traverse obstacles and navigate the environment vertically.

## Acceptance Criteria

1. `PlayerConfigSO` at `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs` has a `jumpForce` float field (default `5f`) under a `[Header("Jump")]` group.
2. `PlayerController` reads `_input.Player.Jump.WasPressedThisFrame()` each frame; when the player is grounded, sets `_verticalVelocity = _config.jumpForce` — launching the character upward.
3. Jump cannot be triggered mid-air: the grounded gate (`_characterController.isGrounded`) prevents double-jumping.
4. Gravity continues to decelerate and then accelerate the character downward during the airborne phase via the existing `ApplyGravity()` logic (no changes to gravity formula required).
5. `PlayerAnimator` sets hash-cached `IsGrounded` and `IsRising` bool Animator parameters each frame based on `_characterController.isGrounded` and `_characterController.velocity.y > 0.1f`.
6. `PlayerAnimatorController` has `IsGrounded` (default: `true`) and `IsRising` bool parameters, plus 4 states: `Locomotion` (blend tree), `JumpRise`, `Falling`, `Landing` — with Mixamo Humanoid animation clips for each airborne phase.
7. Animator transitions: `AnyState → JumpRise` (IsGrounded=false, IsRising=true), `AnyState → Falling` (IsGrounded=false, IsRising=false), `JumpRise → Falling` (IsRising=false), `Falling → Landing` (IsGrounded=true), `Landing → Locomotion` (exit time). All transitions use smooth crossfade durations (0.1–0.2s). All states use `WriteDefaultValues: false`.
8. An Edit Mode test class `PlayerJumpTests` at `Assets/Tests/EditMode/PlayerJumpTests.cs` verifies: jump sets positive vertical velocity, gravity reduces upward velocity over time, grounded gate prevents mid-air jump.
9. No compile errors; no Play Mode errors in TestScene.unity; Stories 1.1–1.5 behavior (WASD, camera, idle/walk/run animations) is unchanged.

## Tasks / Subtasks

- [x] Task 1: Add `jumpForce` to `PlayerConfigSO` (AC: 1)
  - [x] 1.1 Open `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs`
  - [x] 1.2 Add `[Header("Jump")] [SerializeField] public float jumpForce = 5f;` after the existing speed fields
  - [x] 1.3 Verify no compile errors after save

- [x] Task 2: Add jump logic to `PlayerController` (AC: 2, 3, 4)
  - [x] 2.1 Add `private void ApplyJump()` method — see Dev Notes for complete implementation
  - [x] 2.2 In `Update()`, call `ApplyJump()` **before** `ApplyGravity()` — order is critical (see Dev Notes)
  - [x] 2.3 Verify: pressing Space while grounded launches player upward; pressing Space mid-air does nothing

- [x] Task 3: Add `IsGrounded` and `IsRising` parameters to `PlayerAnimator` (AC: 5)
  - [x] 3.1 Add `IsGroundedHash` and `IsRisingHash` static fields to `PlayerAnimator`
  - [x] 3.2 In `Update()`, add `SetBool(IsGroundedHash, isGrounded)` and `SetBool(IsRisingHash, velocity.y > 0.1f)`
  - [x] 3.3 Verify no compile errors

- [x] Task 4: Import Mixamo jump/fall/land animations and update `PlayerAnimatorController` (AC: 6, 7)
  - [x] 4.1 Download 3 Mixamo FBX clips: "Jumping Up", "Falling Idle", "Landing" (No Skin, In Place)
  - [x] 4.2 Place FBX files at `Assets/_Game/Art/Characters/Player/Animations/`
  - [x] 4.3 Configure FBX import: Humanoid rig (animationType=3), avatarSetup=1, animationCompression=3, clipAnimations with loop on Falling Idle only
  - [x] 4.4 Add `IsGrounded` (Bool, default true) and `IsRising` (Bool) parameters to controller
  - [x] 4.5 Create 3 new states: `JumpRise` (Jumping Up clip), `Falling` (Falling Idle clip), `Landing` (Landing clip)
  - [x] 4.6 Add transitions: AnyState→JumpRise (IsGrounded=F, IsRising=T, 0.1s), AnyState→Falling (IsGrounded=F, IsRising=F, 0.15s), JumpRise→Falling (IsRising=F, 0.15s), Falling→Landing (IsGrounded=T, 0.1s), Landing→Locomotion (exit time 80%, 0.2s)
  - [x] 4.7 Set `WriteDefaultValues: false` on all states to prevent T-pose bleed
  - [x] 4.8 Landing→Locomotion transition manually created in Unity Animator tab (YAML-only transition was not visible to Unity)

- [x] Task 5: Edit Mode tests (AC: 7)
  - [x] 5.1 Create `Assets/Tests/EditMode/PlayerJumpTests.cs` — see Dev Notes for complete implementation
  - [x] 5.2 Run tests via Unity Test Runner (Window → General → Test Runner → EditMode)
  - [x] 5.3 All 3 tests pass (green)

- [x] Task 6: Validate regression (AC: 8)
  - [x] 6.1 Enter Play Mode in TestScene.unity — no console errors
  - [x] 6.2 Verify WASD movement, mouse camera, and idle/walk/run animations still work
  - [x] 6.3 Press Space while running — character leaves ground, falls back, locomotion resumes on landing

## Dev Notes

### Critical Execution Order in PlayerController.Update()

`ApplyJump()` **must be called before `ApplyGravity()`**. Here is why:

On the frame when Jump is pressed, `_characterController.isGrounded` is still `true` (the character hasn't moved yet). If `ApplyGravity()` runs first, it sets `_verticalVelocity = GROUNDED_VELOCITY = -2f`, and then `ApplyJump()` overrides it to `jumpForce` — which works. **But if `ApplyJump()` runs after `ApplyGravity()` AND after `_characterController.Move()` is called (inside `ApplyMovement()`), the frame's movement has already been submitted with the old velocity.** Placing `ApplyJump()` first, before both, guarantees the jump velocity is always included in the same frame's `Move()` call.

```csharp
// Correct order in PlayerController.Update():
private void Update()
{
    if (_characterController == null || _config == null) return;

    ApplyJump();     // 1st — set jump velocity if conditions met
    ApplyGravity();  // 2nd — apply gravity (or hold grounded snap)
    ApplyMovement(); // 3rd — move using current _verticalVelocity
}
```

### ApplyJump() — Complete Implementation

```csharp
private void ApplyJump()
{
    if (_characterController.isGrounded && _input.Player.Jump.WasPressedThisFrame())
    {
        _verticalVelocity = _config.jumpForce;
        GameLog.Info(TAG, $"Jump triggered. Vertical velocity set to {_verticalVelocity}");
    }
}
```

**Why `WasPressedThisFrame()` and not `IsPressed()`:**
`IsPressed()` is true for the entire duration the key is held. Using it would re-apply the jump impulse every frame the key is held while grounded (after landing). `WasPressedThisFrame()` fires only on the first frame of the press, giving a clean single-jump impulse.

### PlayerConfigSO — Change

Current state of `PlayerConfigSO.cs` (3 fields):
```csharp
[SerializeField] public float walkSpeed = 3f;
[SerializeField] public float runSpeed = 6f;
[SerializeField] public float rotationSpeed = 10f;
```

Add after `rotationSpeed`:
```csharp
[Header("Jump")]
[SerializeField] public float jumpForce = 5f;
```

**Tuning note:** `jumpForce = 5f` with `GRAVITY = -9.81f` gives roughly 1.27 m peak height and ~1.0s airtime. For reference: `jumpForce = 7f` gives ~2.5 m / 1.4s. Adjust via Inspector on the `PlayerConfig.asset` — never hardcode.

### PlayerAnimator — Change

`PlayerAnimator` already caches `_characterController` in `Awake()` — no new component lookup needed. Add hashes and `SetBool` calls:

```csharp
// Add near the existing SpeedHash:
private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
private static readonly int IsRisingHash = Animator.StringToHash("IsRising");

// In Update(), after the existing SetFloat:
private void Update()
{
    Vector3 horizontalVelocity = new Vector3(
        _characterController.velocity.x, 0f, _characterController.velocity.z);
    float speed = horizontalVelocity.magnitude;
    _animator.SetFloat(SpeedHash, speed, DAMP_TIME, Time.deltaTime);
    _animator.SetBool(IsGroundedHash, _characterController.isGrounded);
    _animator.SetBool(IsRisingHash, _characterController.velocity.y > 0.1f);
}
```

### Edit Mode Tests — PlayerJumpTests.cs

`PlayerController` is a MonoBehaviour; we test only the pure formulas it uses, not the lifecycle.

```csharp
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for jump physics formulas used by PlayerController.
/// Tests pure math — no MonoBehaviour lifecycle, no scene required.
/// </summary>
public class PlayerJumpTests
{
    [Test]
    public void JumpForce_SetsPositiveVerticalVelocity()
    {
        // Simulates: _verticalVelocity = _config.jumpForce
        float jumpForce = 5f;
        float verticalVelocity = jumpForce;
        Assert.That(verticalVelocity, Is.GreaterThan(0f));
    }

    [Test]
    public void Gravity_ReducesUpwardVelocityEachFrame()
    {
        // Simulates one frame of gravity while airborne (not grounded)
        const float GRAVITY = -9.81f;
        float verticalVelocity = 5f; // just after jump
        float deltaTime = 0.016f;    // ~60fps frame
        verticalVelocity += GRAVITY * deltaTime;
        Assert.That(verticalVelocity, Is.LessThan(5f));
    }

    [Test]
    public void GroundedGate_PreventsJumpWhileAirborne()
    {
        // Simulates: if (_characterController.isGrounded && Jump.WasPressedThisFrame())
        bool isGrounded = false; // mid-air
        bool jumpPressed = true;
        bool shouldJump = isGrounded && jumpPressed;
        Assert.That(shouldJump, Is.False);
    }
}
```

### Animator Controller — Setup Details

The `PlayerAnimatorController` has 3 parameters and 4 states:

**Parameters:**
- `Speed` (Float) → drives the `Locomotion` blend tree (idle/walk/run)
- `IsGrounded` (Bool, default: `true`) → grounded state
- `IsRising` (Bool, default: `false`) → rising vs falling phase of jump

**States:**
- `Locomotion` (default) — Blend tree: Idle (0), Walking (3.5), Running (6) on Speed
- `JumpRise` — Mixamo "Jumping Up" clip (non-looping)
- `Falling` — Mixamo "Falling Idle" clip (looping)
- `Landing` — Mixamo "Landing" clip (non-looping)

**Transitions (all with smooth crossfade):**
- `AnyState → JumpRise`: IsGrounded=false AND IsRising=true, duration 0.1s, CanTransitionToSelf=false
- `AnyState → Falling`: IsGrounded=false AND IsRising=false, duration 0.15s, CanTransitionToSelf=false
- `JumpRise → Falling`: IsRising=false, duration 0.15s
- `Falling → Landing`: IsGrounded=true, duration 0.1s
- `Landing → Locomotion`: exit time 80%, duration 0.2s (no conditions)

**All states:** `WriteDefaultValues: false` (prevents T-pose bleed between states)

### InputSystem_Actions — NO CHANGES NEEDED

The `Jump` action **already exists** in the Player action map (confirmed in `Assets/_Game/InputSystem_Actions.cs`). Do NOT modify `InputSystem_Actions.inputactions`. Bindings are already:
- Keyboard: Space
- Gamepad: South button (A/Cross)

### Architecture Compliance

| Rule | How This Story Complies |
|------|------------------------|
| No magic numbers — config SOs only | ✅ `jumpForce` lives in `PlayerConfigSO`; constants (`GRAVITY`, `GROUNDED_VELOCITY`) defined as `const` in `PlayerController` |
| GameLog only — no Debug.Log | ✅ `GameLog.Info(TAG, ...)` in `ApplyJump()` |
| Null-guard `[SerializeField]` refs in Awake | ✅ `_config` and `_characterController` already null-checked; no new refs added |
| Private fields: `_camelCase` | ✅ No new instance fields — `jumpForce` is a SO field (public per SO convention) |
| Script namespace: `Game.Player` | ✅ All modifications stay in `PlayerController` and `PlayerAnimator` (both already in `namespace Game.Player`) |
| Edit Mode tests: pure logic only | ✅ `PlayerJumpTests` tests math formulas, not MonoBehaviour lifecycle |
| No GetComponent in Update | ✅ All component references cached in Awake |
| Assembly: all code in `Game` asmdef | ✅ `PlayerController`, `PlayerAnimator`, `PlayerConfigSO` all under `Assets/_Game/` |

### What Does NOT Change

- `ApplyMovement()` — no modification; horizontal movement during jump uses same camera-relative code
- `InputSystem_Actions.inputactions` — Jump action is already bound to Space / Gamepad South
- `Tests.EditMode.asmdef` — already references `Game`; no changes needed
- Player prefab root structure — no new components added

### Project Structure Notes

Files changed by this story:

```
MODIFIED  Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs                    ← add jumpForce
MODIFIED  Assets/_Game/Scripts/Player/PlayerController.cs                             ← add ApplyJump(), reorder Update(), fix ApplyGravity() guard
MODIFIED  Assets/_Game/Scripts/Player/PlayerAnimator.cs                               ← add IsGroundedHash, IsRisingHash, SetBool calls
MODIFIED  Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller ← 4 states, 3 params, 5 transitions
NEW       Assets/_Game/Art/Characters/Player/Animations/Jumping Up.fbx                ← Mixamo jump rise (Humanoid)
NEW       Assets/_Game/Art/Characters/Player/Animations/Falling Idle.fbx              ← Mixamo falling (Humanoid, looping)
NEW       Assets/_Game/Art/Characters/Player/Animations/Landing.fbx                   ← Mixamo landing (Humanoid)
NEW       Assets/Tests/EditMode/PlayerJumpTests.cs                                    ← 3 Edit Mode tests
```

All scripts are in the `Game` assembly (`Game.asmdef`). Tests are in `Tests.EditMode.asmdef` (already references `Game`). No assembly definition changes needed.

### References

- `InputSystem_Actions` Player map (Jump action confirmed): `Assets/_Game/InputSystem_Actions.cs`
- `PlayerController` current implementation: `Assets/_Game/Scripts/Player/PlayerController.cs`
- `PlayerAnimator` current implementation: `Assets/_Game/Scripts/Player/PlayerAnimator.cs`
- `PlayerConfigSO` current fields: `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs`
- GROUNDED_VELOCITY gotcha (CharacterController.velocity.y never zero): [Source: CLAUDE.md#CharacterController.velocity Includes Y Component]
- OnDisable null guard pattern: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- GameLog mandatory usage: [Source: project-context.md#Logging — MANDATORY]
- No magic numbers rule: [Source: project-context.md#Config & Data Anti-Patterns]
- Edit Mode test scope (pure logic only): [Source: project-context.md#Testing Rules]
- `WasPressedThisFrame()` vs `IsPressed()`: [Source: Unity Input System docs — Button action reading]
- Hash-cache Animator parameters: [Source: PlayerAnimator.cs existing pattern with `SpeedHash`]
- Jump action already in input asset: [Source: `Assets/_Game/InputSystem_Actions.cs` line 94-175]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

N/A — no runtime issues encountered.

### Completion Notes List

- Task 1: Added `[Header("Jump")] [SerializeField] public float jumpForce = 5f;` to `PlayerConfigSO` after `rotationSpeed`.
- Task 2: Added `ApplyJump()` method to `PlayerController`. Reordered `Update()` to call `ApplyJump()` first, then `ApplyGravity()`, then `ApplyMovement()`. Uses `WasPressedThisFrame()` and `isGrounded` gate to prevent double-jump. `GameLog.Info` used as required. Fixed ApplyGravity grounded guard: added `&& _verticalVelocity <= 0f` to prevent gravity from overwriting jump velocity on the same frame.
- Task 3: Added `IsGroundedHash` and `IsRisingHash` static fields plus `SetBool` calls in `PlayerAnimator.Update()`.
- Task 4: Imported 3 Mixamo FBX animations (Jumping Up, Falling Idle, Landing) as Humanoid rig. Rewrote `PlayerAnimatorController.controller` with 4 states (Locomotion, JumpRise, Falling, Landing), 3 parameters (Speed, IsGrounded, IsRising), and 5 transitions with smooth crossfade durations. Set `WriteDefaultValues: false` on all states. Note: MCP tool had multiple bugs (conditionMode=3 instead of 2 for IfNot bools, wrong transition durations, wrong canTransitionToSelf) — all fixed via direct YAML edits. Landing→Locomotion transition had to be manually created in Unity Animator tab as the YAML-only definition wasn't visible to Unity.
- Task 5: Created `PlayerJumpTests.cs` with 3 Edit Mode tests. All 3 passed (3/3 green).
- Task 6: Play Mode validation done manually. Jump launches character, JumpRise/Falling/Landing animations play with smooth transitions, Locomotion blend tree resumes on landing.

### File List

- `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs` — added `jumpForce` field
- `Assets/_Game/Scripts/Player/PlayerController.cs` — added `ApplyJump()`, reordered `Update()`, fixed `ApplyGravity()` grounded guard
- `Assets/_Game/Scripts/Player/PlayerAnimator.cs` — added `IsGroundedHash`, `IsRisingHash`, two `SetBool` calls
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` — 3 params, 4 states (Locomotion/JumpRise/Falling/Landing), 5 transitions
- `Assets/_Game/Art/Characters/Player/Animations/Jumping Up.fbx` — Mixamo jump rise animation (Humanoid)
- `Assets/_Game/Art/Characters/Player/Animations/Jumping Up.fbx.meta` — Humanoid import settings, clipAnimations
- `Assets/_Game/Art/Characters/Player/Animations/Falling Idle.fbx` — Mixamo falling animation (Humanoid, looping)
- `Assets/_Game/Art/Characters/Player/Animations/Falling Idle.fbx.meta` — Humanoid import settings, clipAnimations
- `Assets/_Game/Art/Characters/Player/Animations/Landing.fbx` — Mixamo landing animation (Humanoid)
- `Assets/_Game/Art/Characters/Player/Animations/Landing.fbx.meta` — Humanoid import settings, clipAnimations
- `Assets/_Game/Art/Characters/Player/Animations/Idle.fbx.meta` — clip renamed from "mixamo.com" to "Idle" (Unity auto-renamed on reimport)
- `Assets/_Game/Art/Characters/Player/Animations/Walking.fbx.meta` — clip renamed from "mixamo.com" to "Walking"
- `Assets/_Game/Art/Characters/Player/Animations/Running.fbx.meta` — clip renamed from "mixamo.com" to "Running"
- `Assets/Tests/EditMode/PlayerJumpTests.cs` — new file: 3 Edit Mode tests
- `Assets/Tests/EditMode/PlayerJumpTests.cs.meta` — Unity auto-generated meta
- `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` — pre-existing: `GameEventSO<T>` made abstract, TAG constant removed (unrelated to this story, committed alongside)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` — updated `1-6-jump` status to `review`

## Change Log

- 2026-03-04: Story implemented — added jump mechanic (PlayerConfigSO, PlayerController, PlayerAnimator, AnimatorController); 3 Edit Mode tests created and passing; status set to `review`.
- 2026-03-04: Expanded with Mixamo jump/fall/land animations — replaced single Airborne placeholder state with 4-state machine (Locomotion, JumpRise, Falling, Landing); added IsRising parameter; fixed ApplyGravity grounded guard bug; set WriteDefaultValues=false on all states; tuned transition durations for smooth blending.
- 2026-03-04: Code review fixes — rewrote PlayerJumpTests with meaningful tests (grounded guard, gravity snap, jump velocity); added `RISING_VELOCITY_THRESHOLD` constant to PlayerAnimator; added namespace `Game.Tests` to test class; documented previously undocumented file changes (Idle/Walking/Running .meta renames, GameEventSO.cs, PlayerJumpTests.cs.meta); fixed int64 overflow fileID in animator controller (Landing→Locomotion transition rebuilt via Unity Animator tab).
