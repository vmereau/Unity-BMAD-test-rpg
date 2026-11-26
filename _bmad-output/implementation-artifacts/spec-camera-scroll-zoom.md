---
title: 'Mouse-wheel camera zoom between a close over-the-shoulder view and a slightly farther arm'
type: 'feature'
created: '2026-11-27'
status: 'done'
baseline_commit: '621c51bd'
context:
  - '{project-root}/Assets/_Game/Scripts/Player/CAMERA.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The third-person camera arm is fixed at 4.5 m (`CinemachineThirdPersonFollow.CameraDistance` on the Player prefab), so the player can't bring the view close for indoor or detailed moments, or pull it back a bit to see more.

**Approach:** `CameraController` reads the mouse wheel and moves a zoom distance in steps between a close over-the-shoulder minimum (1.5 m) and a maximum a little beyond the default (6 m), easing the arm to it. The zoomed distance replaces the prefab default as the base for every existing rig write (bow aim blend, look-up shortening); Cinemachine's `AvoidObstacles` still pulls the arm in.

## Boundaries & Constraints

**Always:**
- The wheel is read through `_input.UI.ScrollWheel` (already bound to `<Mouse>/scroll`; `CameraController` already enables the UI map). No new input action.
- Wheel forward (positive y) = closer; one notch = one step (0.5 m). The per-frame wheel value is clamped to ±1 step, so the result is the same whether Input System reports ±1 or ±120 per notch.
- Zoom only reacts while `CursorManager.IsLocked` (menus, dialogue and container screens keep the wheel for scrolling) and while not aiming the bow (`_aimBlend > 0` → ignore the wheel; the target is kept).
- The arm eases to the target (frame-rate independent, unscaled time like the aim blend) — never snaps.
- Starts at the prefab `CameraDistance` (4.5 m), clamped into [min, max]. Min, max, step and ease speed are `[SerializeField]`s on `CameraController` with those defaults; min is floored at `CameraRigMath.MinArmDistance`.
- Bow aim blends from the zoomed distance to the aim arm; the look-up shortening multiplies the zoomed distance (so 1.5 m × 0.5 at full look-up = 0.75 m, still ≥ `MinArmDistance`).
- Pure rules (wheel → new target, ease) live in `CameraRigMath` with EditMode tests.

**Ask First:**
- Changing the shoulder offset with the zoom (e.g. a wider shoulder when close) or saving the zoom level in settings / saves.

**Never:**
- No `CinemachineDeoccluder` / `CinemachineDecollider`, no `CinemachineInputAxisController`, no `Mouse.current` reads.
- Don't change the prefab's default `CameraDistance` or the `AvoidObstacles` settings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Zoom in | target 4.5, wheel +1 | target 4.0, arm eases to it | N/A |
| Zoom out | target 4.5, wheel −1 | target 5.0 | N/A |
| At min | target 1.5, wheel +1 | stays 1.5 | clamp |
| At max | target 6.0, wheel −1 | stays 6.0 | clamp |
| Platform range | target 4.5, wheel +120 | target 4.0 (one step) | clamp to ±1 |
| No wheel | wheel 0 | target unchanged | N/A |
| Menu open | cursor unlocked, wheel +1 | target unchanged | gate |
| Aiming | `_aimBlend > 0`, wheel +1 | target unchanged; arm follows aim blend | gate |
| Bad config | min > max on the component | warn once in `Awake`, swap so min ≤ max | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Player/CameraController.cs` -- owns all camera input; `ApplyRig` writes `ShoulderOffset` / `CameraDistance` every `LateUpdate` from `_defaultCameraDistance`
- `Assets/_Game/Scripts/Player/CameraRigMath.cs` -- pure rig rules (`BlendRig`, `RigDistanceScale`, `MinArmDistance`)
- `Assets/Tests/EditMode/CameraRigMathTests.cs` -- existing rig rule tests
- `Assets/_Game/Scripts/Player/CAMERA.md` -- camera rig doc (rig writes paragraph)
- `Assets/_Game/InputSystem_Actions.cs` -- `UI.ScrollWheel` (Vector2, `<Mouse>/scroll`), read only

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Player/CameraRigMath.cs` -- add `StepZoomTarget(target, wheelY, step, min, max)` (clamp wheel to ±1, positive = closer, clamp result) and `EaseZoom(current, target, speed, dt)` (exponential, snaps when within 0.001) -- testable rules
- [x] `Assets/_Game/Scripts/Player/CameraController.cs` -- serialized `_zoomMinDistance 1.5`, `_zoomMaxDistance 6`, `_zoomStep 0.5`, `_zoomEaseSpeed 10`; `_zoomTarget` / `_zoomDistance` initialised in `Awake` from the clamped default; `UpdateZoom()` in `LateUpdate` before `ApplyRig` (gates above); `ApplyRig` uses `_zoomDistance` as the base distance -- the feature
- [x] `Assets/Tests/EditMode/CameraRigMathTests.cs` -- cases for every matrix row of the two new methods (in, out, min, max, ±120, zero, ease converges / never overshoots) -- I/O coverage
- [x] `Assets/_Game/Scripts/Player/CAMERA.md` -- document the zoom in the rig writes paragraph (input, gates, base for aim / look-up, swim note) -- doc

**Acceptance Criteria:**
- Given free-look in Play, when the wheel rolls forward several notches, then the camera eases in to a close over-the-shoulder view and stops at 1.5 m.
- Given the camera zoomed in, when the player draws and aims the bow and then releases, then aim works as before and the camera returns to the zoomed distance, not 4.5 m.
- Given the camera at 6 m near a wall, when the player backs into it, then `AvoidObstacles` still pulls the camera in without clipping.
- Given the inventory open, when the wheel scrolls a list, then the camera distance does not change.

## Design Notes

Swimming: the −5° swim pitch limit was tuned for the 4.5 m arm (≈0.25 m above the surface). At 6 m the camera sits ≈0.13 m above the water at that limit — still dry; check it in the play test, and if it dips, cap the zoom while swimming (Ask First).

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode, filter `CameraRigMathTests` -- expected: all pass
- Unity MCP `read_console` (errors) after compile -- expected: none

**Manual checks (if no CLI):**
- Play mode: zoom in/out to both limits, look up / down at each, aim the bow while zoomed, open the inventory and scroll, swim at 6 m looking up.

## Suggested Review Order

**Wheel input and gates**

- Entry point: wheel read only when the cursor is locked and the bow isn't aimed.
  [`CameraController.cs:150`](../../Assets/_Game/Scripts/Player/CameraController.cs#L150)

- One notch per frame, forward = closer, clamped to the limits.
  [`CameraRigMath.cs:49`](../../Assets/_Game/Scripts/Player/CameraRigMath.cs#L49)

- Frame-rate independent ease, no overshoot, 1 mm snap.
  [`CameraRigMath.cs:59`](../../Assets/_Game/Scripts/Player/CameraRigMath.cs#L59)

**Rig composition**

- Zoomed arm replaces the prefab default as base for aim blend and look-up.
  [`CameraController.cs:226`](../../Assets/_Game/Scripts/Player/CameraController.cs#L226)

- Review patch: floor after the look-up multiply so close zoom never enters the head.
  [`CameraController.cs:231`](../../Assets/_Game/Scripts/Player/CameraController.cs#L231)

- Limits validated once, zoom starts at the prefab arm clamped into them.
  [`CameraController.cs:136`](../../Assets/_Game/Scripts/Player/CameraController.cs#L136)

**Config, tests, doc**

- Tunable limits / step / ease speed with defaults 1.5 / 6 / 0.5 / 10.
  [`CameraController.cs:33`](../../Assets/_Game/Scripts/Player/CameraController.cs#L33)

- Matrix rows as test cases, plus ease behaviour.
  [`CameraRigMathTests.cs:115`](../../Assets/Tests/EditMode/CameraRigMathTests.cs#L115)

- Rig-writes paragraph documents the zoom.
  [`CAMERA.md:30`](../../Assets/_Game/Scripts/Player/CAMERA.md#L30)
