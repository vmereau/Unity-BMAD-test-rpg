# CLAUDE.md — Assets/_Game/Scripts/Player

> Loaded when Claude accesses files in this folder. Covers CameraController, PlayerController, PlayerAnimator, PlayerStateManager, and related systems.

---

## Player Action Gating — PlayerStateManager (Single Gate Pattern)

`PlayerStateManager.cs` is the **single source of truth** for all player action permissions. Before performing any player action, always call the corresponding `Can*` query:

| Action | Gate method | Written by |
|--------|-------------|------------|
| Attack | `CanAttack()` | `PlayerCombat.SetAttacking()` |
| Block  | `CanBlock()`  | `PlayerCombat.SetBlocking()` |
| Dodge  | `CanDodge()`  | `DodgeController.SetDodging()` |
| Jump   | `CanJump()`   | — (read-only gate) |
| Move   | `CanMove()`   | — (read-only gate) |

**Rules:**
- Never implement action gates inline — always check `PlayerStateManager.Can*()` first.
- State is set via `SetAttacking(bool, int triggerHash)`, `SetBlocking(bool)`, `SetDodging(bool, bool isBackward)`.
- `IsBusy` is `true` when the cursor is unlocked — all `Can*` methods return `false` while busy.

---

## PlayerAnimator — Combat Animation API

`PlayerAnimator` owns **all** Animator calls (movement and combat). `PlayerStateManager` delegates animation side-effects to it — no other class should call the Animator directly for player animations.

| Method | Animator effect |
|--------|----------------|
| `SetBlocking(bool)` | Sets `IsBlocking` bool |
| `PlayAttack(int triggerHash)` | Fires the given attack trigger |
| `PlayDodge(bool isBackward)` | Fires `IsDodging` or `IsDodgingBackwards` trigger |

**Consequence:** When adding new player animations, add a public method to `PlayerAnimator` and call it from `PlayerStateManager` — never add `Animator.SetTrigger/SetBool` calls elsewhere.

---

## CharacterController.velocity Includes Y Component

`CharacterController.velocity.magnitude` is **never 0 when grounded** because `PlayerController` constantly applies `GROUNDED_VELOCITY = -2f` to keep the character snapped to the ground. Effects:
- At rest: magnitude ≈ 2.0f (not 0) — blend tree never fully reaches idle threshold
- At `walkSpeed=3f`: magnitude ≈ 3.6f (not 3f) — blend starts crossing walk→run threshold early
- At `runSpeed=6f`: magnitude ≈ 6.3f — may never reach the run threshold if set above 6.3f

**Always use horizontal speed for animation blend trees:**

```csharp
// In PlayerAnimator.Update() — correct pattern
Vector3 horizontalVelocity = new Vector3(
    _characterController.velocity.x, 0f, _characterController.velocity.z);
float speed = horizontalVelocity.magnitude;
```

---

## Float Accumulation & Euler Angle Quirks (CameraController)

- **Unbounded yaw:** accumulated `_yaw` must be normalized: `_yaw %= 360f;` — without this,
  float precision degrades after extended play (thousands of degrees → sub-degree jitter).
- **Pitch from `eulerAngles`:** Unity returns `eulerAngles.x` in [0, 360]. A −10° pitch is
  stored as 350°. Use `Mathf.DeltaAngle(0f, eulerAngles.x)` to get a proper signed value
  before clamping, or the pitch will snap to `_pitchMax` on the first frame.

---

## Cinemachine 3.x — Over-the-Shoulder Setup

The project ships with **Cinemachine 3.x** (`CinemachineCamera` component, not `CinemachineVirtualCamera`).

Working OTS configuration:
- **Body:** `CinemachineFollow` with offset `(0.5, 0.3, −3.5)`
- **Aim:** `CinemachineSameAsFollowTarget` — inherits `CameraTarget` world rotation directly
- **Do NOT use** `CinemachineRotationComposer` for OTS — it aim-corrects toward a world point
  and cancels the vertical pitch driven by `CameraController`
- **Do NOT add** `CinemachineInputAxisController` or `CinemachinePanTilt` — dual input causes
  rotation fighting with `CameraController`

`CameraController.cs` owns all mouse input and writes to `CameraTarget.rotation`.
Cinemachine reads `CameraTarget` passively — it never takes direct input.

---

## Unity Input System — Action Map Layout

The project's `InputSystem_Actions` action maps:

- **Player map:** Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint, **InventoryToggle** — **no Cancel action**
- **UI map:** Navigate, Submit, **Cancel** (Escape), **Click** (left mouse), Point, RightClick, MiddleClick, ScrollWheel

Consequences for cursor lock handling in `CameraController`:
- Escape unlock → `_input.UI.Cancel.WasPressedThisFrame()`
- Left-click re-lock → `_input.UI.Click.WasPressedThisFrame()`
- Must call `_input.UI.Enable()` / `_input.UI.Disable()` alongside the Player map

---

## Code Review Checklist — Player Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | Player action performed without checking `PlayerStateManager.Can*()` — always gate Attack/Block/Dodge/Jump/Move through `PlayerStateManager` |
| HIGH | `Animator.SetTrigger/SetBool` for player combat animations called outside `PlayerAnimator` — all combat animator calls must go through `PlayerAnimator.SetBlocking()`, `PlayAttack()`, `PlayDodge()` |
| MEDIUM | `CharacterController.velocity.magnitude` used for animation speed — Y component inflates value; use `new Vector3(v.x, 0, v.z).magnitude` |
| MEDIUM | Accumulated angle (`_yaw`, `_angle`) without `% 360f` modulo |
| MEDIUM | `eulerAngles` used as signed source without `Mathf.DeltaAngle` conversion |
| MEDIUM | `Keyboard.current` / `Mouse.current` used instead of `InputSystem_Actions` action map |
