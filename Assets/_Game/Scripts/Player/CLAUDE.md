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
| InCombat | `IsInCombat` | `PlayerCombat.SetInCombat()` via `PlayerStateManager.SetInCombat()` |

**Rules:**
- Never implement action gates inline — always check `PlayerStateManager.Can*()` first.
- State is set via `SetAttacking(bool, int triggerHash)`, `SetBlocking(bool)`, `SetDodging(bool, bool isBackward)`, `SetInCombat(bool)`.
- `IsBusy` is `true` when the cursor is unlocked — all `Can*` methods return `false` while busy.
- `CanAttack()` and `CanBlock()` both require `IsInCombat == true` (Story 7.12) — pressing R draws/sheathes the weapon. `CanDodge()` is unchanged.
- `IsInCombat` defaults to `false` — weapon is sheathed on game start.

---

## PlayerAnimator — Locomotion & Combat Animation API

`PlayerAnimator` owns **all** Animator calls (movement and combat). `PlayerStateManager` delegates animation side-effects to it — no other class should call the Animator directly for player animations.

### Locomotion — Unified 2D Blend Tree

The `PlayerAnimatorController` uses a **single `LockOn Locomotion` state** (2D Freeform Cartesian blend tree) for all movement — there is no separate 1D free-locomotion state. `PlayerAnimator.Update()` always drives `VelocityX` and `VelocityZ` from local-space velocity:

```csharp
Vector3 worldHoriz = new Vector3(velocity.x, 0f, velocity.z);
Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
normX = Mathf.Clamp(localVelocity.x / runSpeed, -1f, 1f);
normZ = Mathf.Clamp(localVelocity.z / runSpeed, -1f, 1f);
```

- **Free movement:** character faces movement direction → `localVelocity.z ≈ speed`, `x ≈ 0` → forward clips play
- **Lock-on:** character faces target → strafing produces non-zero `x` → strafe clips play
- Blend positions: ±0.5 = walk speed, ±1.0 = run speed (normalized against `PlayerConfigSO.runSpeed`)
- `Speed` and `IsLockedOn` animator parameters **do not exist** — do not add them

### Combat Animation API

| Method | Animator effect |
|--------|----------------|
| `SetBlocking(bool)` | Sets `IsBlocking` bool |
| `PlayAttack(int triggerHash)` | Fires the given attack trigger |
| `PlayDodge(bool isBackward)` | Fires `IsDodging` or `IsDodgingBackwards` trigger |
| `SetInCombat(bool)` | Sets `IsInCombat` bool (parameter exists from 7.12; layer weight added in 7.13) |

**Consequence:** When adding new player animations, add a public method to `PlayerAnimator` and call it from `PlayerStateManager` — never add `Animator.SetTrigger/SetBool` calls elsewhere.

---

## CharacterController.velocity Includes Y Component

`CharacterController.velocity.magnitude` is **never 0 when grounded** because `PlayerController` constantly applies `GROUNDED_VELOCITY = -2f` to keep the character snapped to the ground.

**Always strip the Y component before feeding velocity into blend trees:**

```csharp
Vector3 worldHoriz = new Vector3(
    _characterController.velocity.x, 0f, _characterController.velocity.z);
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

- **Player map:** Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint, **InventoryToggle**, **LockOn**, ActionBar1–6, **DrawWeapon** (R key) — **no Cancel action**
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
| HIGH | `Animator.SetTrigger/SetBool` for player combat animations called outside `PlayerAnimator` — all combat animator calls must go through `PlayerAnimator.SetBlocking()`, `PlayAttack()`, `PlayDodge()`, `SetInCombat()` |
| HIGH | `CanAttack()` or `CanBlock()` returns true when weapon is sheathed — both gates require `IsInCombat == true` since Story 7.12; test scenarios must press R before attacking |
| HIGH | `Speed` or `IsLockedOn` animator parameters added — these do not exist in `PlayerAnimatorController`; locomotion uses `VelocityX`/`VelocityZ` only |
| MEDIUM | `CharacterController.velocity.magnitude` used for animation — Y component inflates value; strip Y before normalizing |
| MEDIUM | Accumulated angle (`_yaw`, `_angle`) without `% 360f` modulo |
| MEDIUM | `eulerAngles` used as signed source without `Mathf.DeltaAngle` conversion |
| MEDIUM | `Keyboard.current` / `Mouse.current` used instead of `InputSystem_Actions` action map |
