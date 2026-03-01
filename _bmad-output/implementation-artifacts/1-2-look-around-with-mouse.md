# Story 1.2: Look Around with Mouse

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to look around with the mouse,
so that the camera follows my aim and movement direction is relative to where I face.

## Acceptance Criteria

1. Moving the mouse rotates the camera horizontally (yaw) around the player.
2. Moving the mouse vertically pitches the camera up/down, clamped to a designer-configurable range (e.g. -70° to +70°).
3. The player character's body rotates to match the camera's horizontal facing when moving.
4. The mouse cursor is locked and hidden during gameplay; pressing Escape unlocks it.
5. Camera follows the player at an over-the-shoulder offset (not centered behind — offset slightly right).
6. The basic Cinemachine follow camera from Story 1.1 is replaced/upgraded to the full over-the-shoulder setup.
7. WASD movement direction remains camera-relative (already implemented in Story 1.1 — must not regress).
8. No console errors on entering Play Mode.

## Tasks / Subtasks

- [x] Task 1: Create `CameraController.cs` (AC: 1, 2, 4)
  - [x] 1.1 Create `Assets/_Game/Scripts/Player/CameraController.cs` as a MonoBehaviour
  - [x] 1.2 Declare `private const string TAG = "[Camera]";`
  - [x] 1.3 Declare `[SerializeField] private Transform _cameraTarget;` — the pivot transform the Cinemachine camera follows
  - [x] 1.4 Declare inspector-configurable `[SerializeField] private float _mouseSensitivity = 1f;`, `[SerializeField] private float _pitchMin = -70f;`, `[SerializeField] private float _pitchMax = 70f;`
  - [x] 1.5 Subscribe `_input.Player.Look` callback in `OnEnable`; unsubscribe in `OnDisable`; dispose input in `OnDisable`
  - [x] 1.6 In `Awake()`, null-guard `_cameraTarget`; log error and `return` if missing
  - [x] 1.7 In `Update()`, read Look delta, accumulate `_yaw` and `_pitch` (clamp pitch to [_pitchMin, _pitchMax]), apply rotation to `_cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0)`
  - [x] 1.8 In `Awake()`, lock and hide cursor: `Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;`
  - [x] 1.9 In `Update()`, listen for Escape key via Input System Cancel action → toggle cursor lock (unlock on Escape, re-lock on click)
  - [x] 1.10 Use `GameLog` for all logging; never `Debug.Log`

- [x] Task 2: Upgrade Cinemachine to over-the-shoulder setup (AC: 5, 6)
  - [x] 2.1 In `TestScene.unity`, select the existing `CinemachineVirtualCamera` (or `CinemachineCamera` in Cinemachine 3)
  - [x] 2.2 Set the camera's **Follow** target to `Player/CameraTarget` (the pivot child transform at eye level, Y ≈ 1.6)
  - [x] 2.3 Set the camera's **LookAt** target to the same `CameraTarget`
  - [x] 2.4 Configure **Body** to `Transposer` (Cinemachine 2) or `CinemachineFollow` (Cinemachine 3) with offset: `X = 0.5` (right shoulder), `Y = 0.3`, `Z = -3.5` (adjust in play mode for feel)
  - [x] 2.5 Configure **Aim** to `CinemachineSameAsFollowTarget` — directly inherits CameraTarget rotation so pitch works correctly (CinemachineRotationComposer was cancelling vertical movement)
  - [x] 2.6 Remove or disable any `CinemachineInputProvider` or `CinemachinePOV` components from the camera — input is driven exclusively by `CameraController.cs`
  - [x] 2.7 Assign `CameraController._cameraTarget` in Inspector to `Player/CameraTarget` transform

- [x] Task 3: Update `PlayerController.cs` for character rotation (AC: 3, 7)
  - [x] 3.1 Open `Assets/_Game/Scripts/Player/PlayerController.cs` (from Story 1.1)
  - [x] 3.2 Add character body rotation: when `moveInput.magnitude > 0.1f`, smoothly rotate `transform.forward` toward the world-space move direction using `Quaternion.Slerp` or `RotateTowards`
  - [x] 3.3 Expose `[SerializeField] private float _rotationSpeed = 10f;` — do NOT hardcode
  - [x] 3.4 Confirm the camera-relative movement vector computation is unchanged (regression check)
  - [x] 3.5 Confirm that `_camera` is still cached in `Awake()` — do NOT call `Camera.main` in `Update()`

- [x] Task 4: Validate in play mode (AC: 1–8)
  - [x] 4.1 Enter Play Mode in `TestScene.unity`
  - [x] 4.2 Confirm mouse left/right rotates camera yaw; mouse up/down pitches camera with visible clamp
  - [x] 4.3 Confirm player body rotates when moving (faces movement direction)
  - [x] 4.4 Confirm WASD movement is still camera-relative (walk toward camera = S key, etc.)
  - [x] 4.5 Confirm cursor is locked on Play Mode start; Escape unlocks; left-click re-locks
  - [x] 4.6 Confirm no console errors (use GameLog.Error only — no raw Debug.LogError)
  - [x] 4.7 Confirm over-the-shoulder offset: player is framed to the left, right shoulder visible
  - [x] 4.8 Confirm FPS remains above 60 in the Stats window

## Dev Notes

### Camera Architecture

The camera system in this project separates concerns:
- **`CameraController.cs`** (`Scripts/Player/`) — owns mouse input, accumulates yaw/pitch state, drives `CameraTarget` rotation
- **`CinemachineCamera` / `CinemachineVirtualCamera`** — reads the `CameraTarget` transform to position/orient itself; does NOT take direct input
- **`CameraTarget`** — an empty child Transform of the Player at eye height (~Y 1.6); acts as the camera's "view pivot"

This decoupling means Cinemachine handles smoothing/damping but CameraController fully controls the view direction. Do NOT add Cinemachine's own POV or input provider — dual input causes rotation fighting.

### Implementation Pattern for `CameraController.cs`

```csharp
// Assets/_Game/Scripts/Player/CameraController.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    private const string TAG = "[Camera]";

    [SerializeField] private Transform _cameraTarget;
    [SerializeField] private float _mouseSensitivity = 1f;
    [SerializeField] private float _pitchMin = -70f;
    [SerializeField] private float _pitchMax = 70f;

    private InputSystem_Actions _input;
    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        if (_cameraTarget == null)
        {
            GameLog.Error(TAG, "CameraTarget not assigned — CameraController disabled");
            enabled = false;
            return;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize yaw/pitch from current target rotation to avoid snap on first frame
        _yaw   = _cameraTarget.eulerAngles.y;
        _pitch = _cameraTarget.eulerAngles.x;
    }

    private void OnEnable()
    {
        _input = new InputSystem_Actions();
        _input.Player.Enable();
    }

    private void OnDisable()
    {
        _input.Player.Disable();
        _input.Dispose();
    }

    private void Update()
    {
        // Cursor unlock/re-lock
        if (_input.Player.Cancel.WasPressedThisFrame())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Only rotate camera when cursor is locked
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 lookDelta = _input.Player.Look.ReadValue<Vector2>();
        _yaw   += lookDelta.x * _mouseSensitivity;
        _pitch -= lookDelta.y * _mouseSensitivity; // inverted: mouse up = look up
        _pitch = Mathf.Clamp(_pitch, _pitchMin, _pitchMax);

        _cameraTarget.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
```

### Player Body Rotation (PlayerController.cs change)

Add to `PlayerController.Update()`, AFTER computing `moveDir`:

```csharp
// Rotate body to face movement direction
if (moveDir.sqrMagnitude > 0.01f)
{
    Quaternion targetRot = Quaternion.LookRotation(moveDir);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
        _rotationSpeed * Time.deltaTime);
}
```

Keep existing `_moveSpeed` field pattern — add `[SerializeField] private float _rotationSpeed = 10f;` alongside it.

### Cinemachine Version Clarification

Unity 6.3 ships with **Cinemachine 3.x**. The API differs from Cinemachine 2.x:

| Cinemachine 2.x | Cinemachine 3.x |
|---|---|
| `CinemachineVirtualCamera` | `CinemachineCamera` |
| Body: `Transposer` | Body: `CinemachineFollow` |
| Aim: `Composer` | Aim: `CinemachineRotationComposer` |
| `CinemachinePOV` | `CinemachinePanTilt` (avoid — use CameraController instead) |
| `CinemachineFreeLook` | `CinemachineCamera` + `CinemachineOrbitalFollow` |

**Check which version is installed:** `Window → Package Manager → Cinemachine` shows the version. Story 1.1 set up a `CinemachineVirtualCamera` (Cinemachine 2.x style) — check if it was auto-upgraded or if 3.x is active.

If **Cinemachine 3.x** is installed:
- The component on the camera GameObject is `CinemachineCamera`
- Body component: add `CinemachineFollow` with `Follow Offset = (0.5, 0.3, -3.5)`
- Aim component: add `CinemachineRotationComposer` with `Screen Position = (0.65, 0.5)`
- **Do NOT** add `CinemachineInputAxisController` — input is owned by `CameraController.cs`

If **Cinemachine 2.x** is installed:
- The component is `CinemachineVirtualCamera`
- Body: `Transposer`, Follow Offset `(0.5, 0.3, -3.5)`
- Aim: `Composer`, Screen X = 0.65
- **Do NOT** add `CinemachinePOV` — input is owned by `CameraController.cs`

### Input System: Look Action

The `InputSystem_Actions` generated class already has a `Look` action mapped in the `Player` action map (it's part of the Starter Assets default bindings):
- `_input.Player.Look` → `Vector2` — mouse delta per frame
- Delta is in pixels; multiply by `_mouseSensitivity` to control speed

**Critical:** Do NOT use `Mouse.current.delta.ReadValue()` directly — go through the Input System action so it respects sensitivity scaling and can be rebound.

### Cursor Lock Pattern

- Lock on game start in `Awake()` — do NOT use `Start()` (Start may run after first frame events)
- Unlock on `Cancel` action (mapped to Escape by default in Starter Assets InputSystem_Actions)
- Re-lock on left mouse click while unlocked — this is the standard Unity pattern for dev testing
- In a future story (Epic 8 UI / pause menu), the cursor unlock will be driven by the pause menu opening; for now the Escape toggle is sufficient

### CameraTarget Transform Setup

The `CameraTarget` child on the Player prefab (created in Story 1.1 at Y ≈ 1.6):
- Assign to `CameraController._cameraTarget` in Inspector
- Also assign to Cinemachine **Follow** and **LookAt**
- Its **world rotation** is what CameraController writes to — the Cinemachine camera reads this to orient itself

If the `CameraTarget` was not retained from Story 1.1 setup, create it:
1. Select `Player.prefab` in the Prefab editor
2. Add child empty GameObject → name `CameraTarget`
3. Set local position to `(0, 1.6, 0)` (eye height)
4. Do not add any components — it's a pure transform pivot

### Project Structure Notes

**Files touched in this story:**
- `Assets/_Game/Scripts/Player/CameraController.cs` — **new**
- `Assets/_Game/Scripts/Player/PlayerController.cs` — **modified** (add body rotation)
- `Assets/_Game/Scenes/TestScene.unity` — **modified** (Cinemachine camera reconfigured)
- `Assets/_Game/Prefabs/Player/Player.prefab` — **modified** (assign CameraController references)

**Architecture alignment:**
- `CameraController` lives in `Scripts/Player/` per architecture directory structure [Source: game-architecture.md#Directory Structure]
- No Rigidbody on player (unchanged from Story 1.1) [Source: project-context.md#Architecture Patterns]
- Input subscription in `OnEnable`/`OnDisable` (not `Start`/`Awake`) [Source: project-context.md#Input System]
- `Camera.main` cached in `Awake()` — never in `Update()` [Source: project-context.md#Unity-Specific Hot Path Rules]
- All custom code under `Assets/_Game/` [Source: game-architecture.md#Organization Pattern]

### Architecture Compliance Checklist

| Rule | Applied? |
|---|---|
| `Debug.Log` banned — use `GameLog` | ✅ Required |
| `[SerializeField] private` over `public` for Inspector fields | ✅ Required |
| Input subscription in `OnEnable`/`OnDisable` | ✅ Required |
| No `Input.GetKey()` / `Input.GetAxis()` | ✅ Required |
| `GetComponent` / `Camera.main` cached in `Awake`, never in `Update` | ✅ Required |
| All custom code under `Assets/_Game/` | ✅ Required |
| No Rigidbody on player | ✅ Required (no change) |
| No `Resources.Load()` — banned in Unity 6 | ✅ N/A this story |
| All tunable values in `[SerializeField]` or config SO — no magic numbers | ✅ Required |

### Testing Standards

Per project testing rules (prototype focus):
- **Edit Mode tests NOT required** — `CameraController` is a MonoBehaviour with Unity lifecycle and `Cursor` API dependencies; no pure-logic isolation possible
- **Manual validation** (Task 4) covers all acceptance criteria
- No Play Mode tests required for this story

### Previous Story Intelligence (Story 1.1)

Key patterns established in Story 1.1 that carry forward:
- `InputSystem_Actions` class pattern: `_input = new InputSystem_Actions(); _input.Player.Enable();` in `OnEnable`
- `OnDisable`: `_input.Player.Disable(); _input.Dispose();`
- `private const string TAG = "[Player]";` → use `"[Camera]"` in CameraController
- `CharacterController.Move()` with manual gravity — **do NOT change this in PlayerController**
- `Camera.main` is cached in `Awake()` as `_camera` in PlayerController — CameraController uses its own `_cameraTarget` Transform, not Camera.main
- Story 1.1 completion notes confirm: Player prefab exists at `Assets/_Game/Prefabs/Player/Player.prefab`, `CameraTarget` child at Y=1.6, Cinemachine VirtualCamera set to Follow=Player root, LookAt=CameraTarget

**Story 1.1 tasks deferred to Story 1.2:**
- Full over-the-shoulder camera (Story 1.1 set up a basic follow camera as a stub)
- Mouse look (explicitly out of scope in Story 1.1)

Story 1.1 set up a `CinemachineVirtualCamera` with `Transposer` body (Follow Offset: 0, 2, -5) and `Composer` aim. Story 1.2 should reconfigure this to the OTS offset.

### References

- Camera system location: [Source: game-architecture.md#Directory Structure → `Scripts/Player/CameraController.cs`]
- Input System mandate: [Source: project-context.md#Input System]
- `OnEnable`/`OnDisable` subscription rule: [Source: project-context.md#Architecture Patterns → Event subscription]
- Hot path caching rules: [Source: project-context.md#Unity-Specific Hot Path Rules]
- No magic numbers policy: [Source: project-context.md#Config & Data Anti-Patterns]
- Logging mandate: [Source: project-context.md#Logging — MANDATORY]
- Cinemachine camera rig in Core.unity (for later migration): [Source: project-context.md#Scene Architecture]
- Story 1.1 camera stub: [Source: 1-1-move-character-with-wasd.md#Cinemachine Camera (Stub for Story 1-2)]
- Story 1.1 completion notes: [Source: 1-1-move-character-with-wasd.md#Completion Notes List]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- **Task 1 (CameraController.cs):** Created `Assets/_Game/Scripts/Player/CameraController.cs` in `namespace Game.Player`. Implements mouse look via `InputSystem_Actions.Player.Look` polled in `Update()`. Yaw/pitch accumulated each frame with pitch clamped. Cursor locked in `Awake()`; Cancel action unlocks; left-click re-locks. Null-guard on `_cameraTarget` disables the component (does not throw). GameLog used exclusively.
- **Task 3 (PlayerController.cs):** Added `[SerializeField] private float _rotationSpeed = 10f;` alongside `_moveSpeed`. Added body rotation using `Quaternion.Slerp` when `moveDir.sqrMagnitude > 0.01f`. Camera-relative move vector computation and `_mainCamera` caching in `Awake()` unchanged — regression confirmed by code review.
- **Task 2 (Cinemachine OTS setup):** Configured CinemachineTransposer offset to `(0.5, 0.3, -3.5)`. Replaced CinemachineComposer aim with `CinemachineSameAsFollowTarget` (pitch was being cancelled by aim-at-point behaviour). Follow set to CameraTarget; LookAt cleared. CameraController assigned to Player prefab instance with `_cameraTarget` wired.
- **Task 4 (Play Mode validation):** All ACs confirmed in Unity Editor — yaw/pitch, body rotation, camera-relative WASD, cursor lock/unlock, OTS framing, no console errors, 60+ FPS.

### Code Review Fixes (claude-sonnet-4-6)

- **H1 fixed** — Added `if (_input == null) return;` null guard in `OnDisable()` to prevent NullReferenceException when Awake disables the component before OnEnable initializes `_input`.
- **M1 fixed** — Replaced `Keyboard.current.escapeKey.wasPressedThisFrame` with `_input.UI.Cancel.WasPressedThisFrame()` — now routed through Input System action map (UI/Cancel maps to Escape; rebindable).
- **M2 fixed** — Replaced `Mouse.current.leftButton.wasPressedThisFrame` with `_input.UI.Click.WasPressedThisFrame()` — routed through Input System action map (UI/Click maps to left mouse button). Added `_input.UI.Enable()` / `_input.UI.Disable()` to OnEnable/OnDisable.
- **M3 fixed** — Added `_yaw %= 360f;` after accumulation in `RotateCamera()` to prevent float precision loss over extended sessions.
- **M4 fixed** — Replaced `_pitch = _cameraTarget.eulerAngles.x` with `Mathf.DeltaAngle(0f, _cameraTarget.eulerAngles.x)` to properly convert Unity's [0, 360] euler range to signed [-180, 180], preventing clamp snap on initial frames if CameraTarget has non-zero pitch.

### File List

- `Assets/_Game/Scripts/Player/CameraController.cs` — **new** (modified by code review: H1, M1, M2, M3, M4 fixes)
- `Assets/_Game/Scripts/Player/PlayerController.cs` — **modified** (added `_rotationSpeed` field and body rotation in `ApplyMovement()`)
- `Assets/_Game/Scenes/TestScene.unity` — **modified** (Cinemachine OTS reconfiguration, floor material)
- `Assets/_Game/Art/Materials/Floor_Default.mat` — **new** (URP Lit warm-gray floor material)
