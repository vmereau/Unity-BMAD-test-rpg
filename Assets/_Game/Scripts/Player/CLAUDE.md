# CLAUDE.md — Assets/_Game/Scripts/Player

> Loaded when Claude accesses files in this folder. Covers CameraController, PlayerController, PlayerAnimationDriver, PlayerStateManager, and related systems.

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

### IsAirborne is Coyote-Smoothed, Not Raw `!isGrounded`

`PlayerStateManager.IsAirborne` is **not** `!_characterController.isGrounded` — it includes a coyote-time grace window (`_coyoteTime`, `[SerializeField]`, default `0.1s`). The property returns `false` for `_coyoteTime` seconds after `isGrounded` last returned `true`, so single-frame ungroundings on slope crests, step edges, and small ledges do not flip the player into the airborne state.

Consequences:
- **`PlayerAnimationDriver.Update()` must drive the animator `IsGrounded` bool via `!_stateManager.IsAirborne`** — never query `_characterController.isGrounded` directly for animation. Doing so reintroduces the falling-clip flicker on slopes and breaks the single-source-of-truth contract.
- **`PlayerController.ApplyGravity()` intentionally still uses raw `_characterController.isGrounded`** — gravity must engage on the actual physical ungrounding, otherwise real falls feel floaty for ~100 ms. Only the *gameplay* airborne concept (action gating + animation) gets the grace window; physics stays raw.
- **Active jumps must call `_stateManager.NotifyJumpStarted()`** — coyote time should only forgive *passive* ungroundings (slopes, ledges). When the player jumps on purpose, `PlayerController.ApplyJump()` calls `NotifyJumpStarted()` right after setting `_verticalVelocity = jumpForce`, which expires the coyote window so `IsAirborne` becomes true on the very next frame. Without this call, the rising/fall animation is delayed by the full `_coyoteTime` window. Any future system that programmatically launches the player upward (knockback, ability, etc.) should do the same.
- **Coyote-jump is still enabled** — `CanJump()` is evaluated *before* `NotifyJumpStarted()` is called, so the player can still jump for `_coyoteTime` seconds after walking off a ledge. If you ever want strict no-coyote-jump, change `CanJump()` alone to query raw `isGrounded`.
- Tuning: bump `_coyoteTime` to `0.15`–`0.3s` if flicker persists on a specific slope; lower to `0.05`–`0.08s` if mid-jump actions become possible due to the grace.

---

## PlayerAnimationDriver — Locomotion & Combat Animation API

`PlayerAnimationDriver` is the **player-specific driver** for `HumanoidAnimationBridge` (`Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs`). `HumanoidAnimationBridge` owns every `Animator.Set*` call for humanoids (Player and, eventually, humanoid NPCs); `PlayerAnimationDriver` reads `CharacterController` velocity and `PlayerStateManager.IsAirborne` and forwards normalized values into it. `PlayerStateManager` calls `PlayerAnimationDriver`'s combat methods — no other class should reach into `HumanoidAnimationBridge` or `Animator` directly for the player.

`[RequireComponent(typeof(HumanoidAnimationBridge))]` guarantees the sibling component exists on the Player prefab.

### Locomotion — Unified 2D Blend Tree

The `PlayerAnimatorController` uses a **single `LockOn Locomotion` state** (2D Freeform Cartesian blend tree) for all movement — there is no separate 1D free-locomotion state. `PlayerAnimationDriver.Update()` always computes `VelocityX` / `VelocityZ` from local-space velocity, then forwards them through `HumanoidAnimationBridge.SetMovement(x, z)`:

```csharp
Vector3 worldHoriz = new Vector3(velocity.x, 0f, velocity.z);
Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
normX = Mathf.Clamp(localVelocity.x / runSpeed, -1f, 1f);
normZ = Mathf.Clamp(localVelocity.z / runSpeed, -1f, 1f);
_humanoidBridge.SetMovement(normX, normZ);
```

- **Free movement:** character faces movement direction → `localVelocity.z ≈ speed`, `x ≈ 0` → forward clips play
- **Lock-on:** character faces target → strafing produces non-zero `x` → strafe clips play
- Blend positions: ±0.5 = walk speed, ±1.0 = run speed (normalized against `PlayerConfigSO.runSpeed`)
- `Speed` and `IsLockedOn` animator parameters **do not exist** — do not add them
- `IsGrounded` / `IsRising` are forwarded via `HumanoidAnimationBridge.SetGrounded()` / `SetRising()`

### Combat Animation API

`PlayerAnimationDriver` exposes player-facing wrappers that delegate to `HumanoidAnimationBridge`:

| `PlayerAnimationDriver` method | Forwards to | Animator effect |
|--------------------------------|-------------|-----------------|
| `SetBlocking(bool)` | `HumanoidAnimationBridge.SetBlocking` | Sets `IsBlocking` bool |
| `PlayAttack(int triggerHash)` | `HumanoidAnimationBridge.PlayAttack` | Fires the given attack trigger |
| `PlayDodge(bool isBackward)` | `HumanoidAnimationBridge.PlayDodge` | Fires `IsDodging` or `IsDodgingBackwards` trigger |
| `SetInCombat(bool)` | `HumanoidAnimationBridge.SetInCombat` | Sets `IsInCombat` bool (parameter exists from 7.12; layer weight added in 7.13) |

**Consequence:** When adding new player animations, add a public method to `HumanoidAnimationBridge` (the actual Animator owner), expose a player-facing wrapper on `PlayerAnimationDriver`, and call it from `PlayerStateManager`. Never add `Animator.SetTrigger/SetBool` calls outside `HumanoidAnimationBridge`.

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

The project uses **Cinemachine 3.1.6** (`com.unity.cinemachine`, namespace `Unity.Cinemachine`).
Assembly reference in `Game.asmdef`: `"Unity.Cinemachine"`.
Components are sibling MonoBehaviours on the `CinemachineCamera` GameObject — not nested pipeline stages.

Working OTS configuration:
- **Body:** `CinemachineFollow` — `FollowOffset (0.5, 0.3, −3.5)`, `TrackerSettings.BindingMode = LockToTargetWithWorldUp`
  - **`LockToTargetWithWorldUp` is required** — only yaw is applied to the offset; pitch is handled by the aim component. Using `LockToTarget` (full rotation) causes the camera to rise steeply when pitching down, breaking the interaction raycast. Using `WorldSpace` breaks orbiting entirely.
  - **Do NOT use `CinemachineThirdPersonFollow`** — it expects a yaw-only tracking target (character body); `CameraTarget` carries pitch+yaw, which causes the camera to spin on itself instead of orbiting.
- **Aim:** `CinemachineRotateWithFollowTarget` — inherits `CameraTarget` world rotation directly
  (`CinemachineSameAsFollowTarget` is **deprecated** in 3.1.6 — do not use it)
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
| HIGH | `Animator.SetTrigger/SetBool` for humanoid player animations called outside `HumanoidAnimationBridge` — all animator calls for the player must go through `HumanoidAnimationBridge` (owner) via `PlayerAnimationDriver`'s public methods (`SetBlocking`, `PlayAttack`, `PlayDodge`, `SetInCombat`) |
| HIGH | `CanAttack()` or `CanBlock()` returns true when weapon is sheathed — both gates require `IsInCombat == true` since Story 7.12; test scenarios must press R before attacking |
| HIGH | `PlayerAnimationDriver` reads `_characterController.isGrounded` directly for the animator `IsGrounded` bool — must read `!_stateManager.IsAirborne` so the coyote-time grace applies; raw `isGrounded` causes falling-clip flicker on slopes/ledges |
| MEDIUM | Action-gating code uses `!_characterController.isGrounded` instead of `_stateManager.IsAirborne` — bypasses the coyote window and reintroduces single-frame action cancellation on slope crests |
| MEDIUM | Code sets `_verticalVelocity` to a positive value (jump, launcher, knockback) without calling `_stateManager.NotifyJumpStarted()` — the coyote window will swallow the upward motion and delay the rising animation by `_coyoteTime` seconds |
| HIGH | `Speed` or `IsLockedOn` animator parameters added — these do not exist in `PlayerAnimatorController`; locomotion uses `VelocityX`/`VelocityZ` only |
| MEDIUM | `CharacterController.velocity.magnitude` used for animation — Y component inflates value; strip Y before normalizing |
| MEDIUM | Accumulated angle (`_yaw`, `_angle`) without `% 360f` modulo |
| MEDIUM | `eulerAngles` used as signed source without `Mathf.DeltaAngle` conversion |
| MEDIUM | `Keyboard.current` / `Mouse.current` used instead of `InputSystem_Actions` action map |
