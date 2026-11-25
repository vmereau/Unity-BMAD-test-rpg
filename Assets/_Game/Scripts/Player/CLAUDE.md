# CLAUDE.md — Assets/_Game/Scripts/Player

> Loaded when Claude accesses files in this folder. Covers PlayerStateManager gating, PlayerAnimationDriver,
> PlayerController velocity and LockOnSystem.

## Topic files

| File | Read when |
|------|-----------|
| `PLAYER_STATES.md` | Touching `PlayerPotionDrinker`, `PlayerSneak`, `PlayerSwimming` / `SwimRules`, `PlayerMovementStamina` / `PlayerFallDamage` / `MovementStaminaRules`, `PlayerStance`, or the unconscious flow in `PlayerHealth` |
| `CAMERA.md` | Touching `CameraController`, `CameraRigMath` or the `CinemachineCamera` setup on the Player prefab |

---

## Player Action Gating — PlayerStateManager (Single Gate Pattern)

`PlayerStateManager.cs` is the **single source of truth** for all player action permissions. Before performing any player action, always call the corresponding `Can*` query:

| Action | Gate method | Written by |
|--------|-------------|------------|
| Attack | `CanAttack()` | `PlayerCombat.SetAttacking()` |
| Block  | `CanBlock()`  | `PlayerCombat.SetBlocking()` |
| Dodge  | `CanDodge()`  | `DodgeController.SetDodging()` |
| Jump   | `CanJump()`   | — (read-only gate) |
| Move   | `CanMove()`   | — (read-only gate; false during any attack — swings move through `PlayerAttackStep`, or `ActionRootMotion` for a clip that carries its own travel: `Scripts/Combat/PLAYER_COMBO.md`) |
| Attack steering | `CanSteerAttack()` / `IsAttackCommitted` | `PlayerCombat` → `SetAttackCommitted(bool)` (true at `HitboxEnable`, false on attack-state enter); `PlayerController` turns at `attackSteerSpeed` while it's true (`Scripts/Combat/PLAYER_COMBO.md`) |
| InCombat | `IsInCombat` | `PlayerCombat.SetInCombat()` via `PlayerStateManager.SetInCombat()` |
| Aggroed | `IsAggroed` (read-only, cached per frame) | — computed: `EntityBrain.IsAnyFighting(player FactionMember)` = any active brain in Warning / Engaging / Attacking / Searching on the player, any faction. **Not** `IsInCombat` (drawn weapon) |
| Draw / sheathe (R) | `CanSwitchWeapon()` / `IsSwitchingWeapon` | `PlayerCombat` → `BeginWeaponSwitch(draw, speed)` / `EndWeaponSwitch()` (flow: `Scripts/Combat/CLAUDE.md`) |
| Equip from the action bar | `CanChangeEquipment()` — not dead, attacking, blocking, aiming, dodging, airborne, switching or drinking; **not** `IsBusy` (the cursor is unlocked when clicking a bar slot) | — (read-only gate; `ActionBarSystem`, `PlayerCombat` swap) |
| Bow aim | `CanAim()` / `IsAiming` | `PlayerCombat` → `SetAiming(bool)` / `PlayBowFire()`. R is blocked while aiming (`PLAYER_STATES.md` → Bow aim) |
| Sneak  | `CanSneak()` / `IsSneaking` | `PlayerSneak` → `SetSneaking(bool)` / `ExitSneakToSprint()` |
| Swim   | `IsSwimming` (blocks attack, block, aim, R, dodge, jump, sneak) | `PlayerSwimming` → `SetSwimming(bool)` |
| Drink potion | `CanDrink()` / `IsDrinking` (not part of `IsBusy`) | `PlayerPotionDrinker` → `SetDrinking(bool)` (`PLAYER_STATES.md` → Drinking) |
| Execute (F) | `CanExecute()` / `IsExecuting` (part of `IsBusy`); `CanContinueExecute` keeps a running stomp alive (ignores `IsExecuting`) | `PlayerExecution` → `SetExecuting(bool)` (exits sneak; flow: `Scripts/Combat/EXECUTE.md`) |

**Rules:**
- Never implement action gates inline — always check `PlayerStateManager.Can*()` first.
- State is set via `SetAttacking(bool, int triggerHash)`, `SetBlocking(bool, bool shieldInHand)`, `SetDodging(bool, bool isBackward)`, `SetInCombat(bool)`.
- `IsBusy` is `true` when dead, staggered / knocked down / unconscious (`IsIncapacitated`), held by a story sequence (`IsScriptedHold`, `SetScriptedHold` — camera stays free), the cursor is unlocked or on the frame a dialogue closed (`DialogueEndedThisFrame`) — all `Can*` methods return `false` while busy. `DodgeController` doesn't use `CanDodge()`; it checks `IsIncapacitated` (and `IsExecuting`) itself — so do `InteractionSystem`, `ActionBarSystem.TryUseSlot`, `UIScreenManager.OpenTab` and `PlayerController`'s lock-on rotation: **a new state in `IsBusy` must be added to those five too.**
- `CanAttack()` and `CanBlock()` both require `IsInCombat == true` — pressing R draws/sheathes the weapon. `CanDodge()` is unchanged.
- `CanBlock()`, `CanJump()` and `CanDodge()` allow an attack in progress — block / jump / dodge cancel the swing (`PlayerCombat.CancelAttack()`, see `Scripts/Combat/PLAYER_COMBO.md`).
- `IsInCombat` defaults to `false` — weapon is sheathed on game start.

Per-state detail (Bow aim, Drinking, Sneak, Swimming, aggro movement costs & fall damage, Stance, Unconscious): `PLAYER_STATES.md`.

### IsAirborne is Coyote-Smoothed, Not Raw `!isGrounded`

`PlayerStateManager.IsAirborne` is **not** `!_characterController.isGrounded` — it includes a coyote-time grace window (`_coyoteTime`, `[SerializeField]`, default `0.5s`, also the `Player.prefab` value). The property returns `false` for `_coyoteTime` seconds after `isGrounded` last returned `true`, so single-frame ungroundings on slope crests, step edges, and small ledges do not flip the player into the airborne state.

Consequences:
- **`PlayerAnimationDriver.Update()` must drive the animator `IsGrounded` bool via `!_stateManager.IsAirborne`** — never query `_characterController.isGrounded` directly for animation. Doing so reintroduces the falling-clip flicker on slopes and breaks the single-source-of-truth contract.
- **`PlayerController.ApplyGravity()` intentionally still uses raw `_characterController.isGrounded`** — gravity must engage on the actual physical ungrounding, otherwise real falls feel floaty for ~100 ms. Only the *gameplay* airborne concept (action gating + animation) gets the grace window; physics stays raw.
- **Active jumps must call `_stateManager.NotifyJumpStarted()`** — coyote time should only forgive *passive* ungroundings (slopes, ledges). When the player jumps on purpose, `PlayerController.ApplyJump()` calls `NotifyJumpStarted()` right after setting `_verticalVelocity = jumpForce`, which expires the coyote window so `IsAirborne` becomes true on the very next frame. Without this call, the rising/fall animation is delayed by the full `_coyoteTime` window. Any future system that programmatically launches the player upward (knockback, ability, etc.) should do the same.
- **Coyote-jump is still enabled** — `CanJump()` is evaluated *before* `NotifyJumpStarted()` is called, so the player can still jump for `_coyoteTime` seconds after walking off a ledge. If you ever want strict no-coyote-jump, change `CanJump()` alone to query raw `isGrounded`.
- Tuning: raise `_coyoteTime` if flicker persists on a specific slope; lower it if mid-jump actions or late ledge jumps become possible due to the grace.

---

## PlayerAnimationDriver — Locomotion & Combat Animation API

`PlayerAnimationDriver` is the **player-specific driver** for `HumanoidAnimationBridge` (`Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs`). `HumanoidAnimationBridge` owns every `Animator.Set*` call for humanoids (Player and, eventually, humanoid NPCs); `PlayerAnimationDriver` reads `CharacterController` velocity and `PlayerStateManager.IsAirborne` and forwards normalized values into it. `PlayerStateManager` calls `PlayerAnimationDriver`'s combat methods — no other class should reach into `HumanoidAnimationBridge` or `Animator` directly for the player.

`[RequireComponent(typeof(HumanoidAnimationBridge))]` guarantees the sibling component exists on the Player prefab.
The bridge is resolved **lazily** (`Bridge` property): `EquipmentVisuals.OnEnable → PlayerCombat → PlayerStateManager.SetInCombat`
can call the driver before its `Awake` — never read `_humanoidBridge` directly.

### Locomotion — Unified 2D Blend Tree

The Player's controller (`Humanoid_Template`, shared with humanoid NPCs — see `Art/Characters/Humanoids/Controllers/CLAUDE.md`) uses a **single `LockOn Locomotion` state** (2D Freeform Cartesian blend tree) for all movement — there is no separate 1D free-locomotion state. `PlayerAnimationDriver.Update()` always computes `VelocityX` / `VelocityZ` = the Y-stripped velocity in local space ÷ `PlayerConfigSO.runSpeed`, clamped to ±1, and forwards them through `HumanoidAnimationBridge.SetMovement(x, z)`.

- **Free movement:** character faces movement direction → `localVelocity.z ≈ speed`, `x ≈ 0` → forward clips play
- **Lock-on:** character faces target → strafing produces non-zero `x` → strafe clips play
- Blend positions: ±0.5 = walk speed, ±1.0 = run speed (normalized against `PlayerConfigSO.runSpeed`)
- `Speed` and `IsLockedOn` animator parameters **do not exist** — do not add them
- `IsGrounded` / `IsRising` are forwarded via `HumanoidAnimationBridge.SetGrounded()` / `SetRising()`

### Combat Animation API

`PlayerAnimationDriver` exposes player-facing wrappers that delegate to `HumanoidAnimationBridge`:

| `PlayerAnimationDriver` method | Forwards to | Animator effect |
|--------------------------------|-------------|-----------------|
| `SetBlocking(bool, bool shieldInHand)` | `HumanoidAnimationBridge.SetBlocking` | Sets `IsBlocking` bool; on a raise also the `ShieldInHand` float (block clip) |
| `PlayAttack(int triggerHash)` | `HumanoidAnimationBridge.PlayAttack` | Fires the given attack trigger |
| `ResetAttackTriggers()` | `HumanoidAnimationBridge.ResetAttackTriggers` | Clears `Attack_1/2/3` — called by `PlayerStateManager.SetAttacking(false)` so an untaken trigger can't stay latched |
| `PlayDodge(bool isBackward)` | `HumanoidAnimationBridge.PlayDodge` | Fires `IsDodging` or `IsDodgingBackwards` trigger |
| `Update` (UpperBody weight) | `HumanoidAnimationBridge.SetUpperBodyWeight` | `UpperBodyWeight.Target(IsBaseFullBody, IsUpperBodyAction, IsInCombat)` stepped over `PlayerConfigSO.upperBodyBlendTime` — same rule as NPCs (`Scripts/Core/Animations/CLAUDE.md`) |
| `SetInCombat(bool)` | `HumanoidAnimationBridge.SetInCombat` | Sets `IsInCombat` bool (UpperBody layer: `LockOn Locomotion` ⇄ `CombatIdle`; in combat the drivers fade the layer to 1 — the weapon carry pose) |
| `PlayWeaponSwitch(bool draw, float speed)` | `HumanoidAnimationBridge.PlayWeaponSwitch` | Sets `WeaponSwitchSpeed`, fires `DrawWeapon` / `SheatheWeapon` (clears the opposite one) |
| `ResetWeaponSwitchTriggers()` | `HumanoidAnimationBridge.ResetWeaponSwitchTriggers` | Clears both — called by `PlayerStateManager.EndWeaponSwitch()` |
| `SetSneaking(bool)` | `HumanoidAnimationBridge.SetSneaking` | Sets `IsSneaking` bool (entering also resets `SneakToSprint`) |
| `PlaySneakToSprint()` | `SetSneaking(false)` + `TriggerSneakToSprint` | Leaves sneak through the `sneak to sprint` clip |

**Consequence:** When adding new player animations, add a public method to `HumanoidAnimationBridge` (the actual Animator owner), expose a player-facing wrapper on `PlayerAnimationDriver`, and call it from `PlayerStateManager`. Never add `Animator.SetTrigger/SetBool` calls outside `HumanoidAnimationBridge`.

---

## CharacterController.velocity Includes Y Component

`PlayerController` always applies `GROUNDED_VELOCITY = -2` while grounded, so `CharacterController.velocity` is never zero —
strip Y before feeding it into blend trees or speed checks.

---


## Camera

`CameraController`, Cinemachine OTS rig, look-up / swim / aim rig writes, teleport warp and cursor lock: `CAMERA.md`.

---


## LockOnSystem — Target Acquisition

`OverlapSphereNonAlloc` on `_lockOnLayerMask` (layer 6 Characters, triggers included) into a **64-slot** buffer —
one entity contributes many colliders there (spider ragdoll bones ≈ 13), and a 10-slot buffer truncated the
aimed target out. Hits collapse to **one candidate per `EntityHealth`** (dead skipped), measured at the health
transform; `SelectNearestInCone` (pure, `LockOnTests`) picks the nearest within `lockOnFOV` (**half-angle**) and
re-checks `lockOnRange` against the root. A full buffer logs a warning.
**Lockable** (`IsLockable`, pure): faction-hostile to the player **or** `EntityBrain.IsFightingOrEngaging(player)`
(fighting it, or staggered by its opening hit and engaging after recovery) — so a neutral NPC in a brawl / trespass /
toll / defend fight with the player can be locked. Leaving combat never breaks an existing lock; the breaks are death,
target **or** player unconscious (neutral fights end in knockouts), `lockOnBreakDistance`, swimming, inactive.

---

## Code Review Checklist — Player Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | Player action performed without checking `PlayerStateManager.Can*()` — always gate Attack/Block/Dodge/Jump/Move through `PlayerStateManager` |
| HIGH | `Animator.SetTrigger/SetBool` for humanoid player animations called outside `HumanoidAnimationBridge` — all animator calls for the player must go through `HumanoidAnimationBridge` (owner) via `PlayerAnimationDriver`'s public methods (`SetBlocking`, `PlayAttack`, `PlayDodge`, `SetInCombat`) |
| HIGH | `CanAttack()` or `CanBlock()` returns true when weapon is sheathed — both gates require `IsInCombat == true`; test scenarios must press R before attacking |
| HIGH | `PlayerAnimationDriver` reads `_characterController.isGrounded` directly for the animator `IsGrounded` bool — must read `!_stateManager.IsAirborne` so the coyote-time grace applies; raw `isGrounded` causes falling-clip flicker on slopes/ledges |
| HIGH | `Speed` or `IsLockedOn` animator parameters added — these do not exist in `Humanoid_Template`; locomotion uses `VelocityX`/`VelocityZ` only |
| MEDIUM | Action-gating code uses `!_characterController.isGrounded` instead of `_stateManager.IsAirborne` — bypasses the coyote window and reintroduces single-frame action cancellation on slope crests |
| MEDIUM | Code sets `_verticalVelocity` to a positive value (jump, launcher, knockback) without calling `_stateManager.NotifyJumpStarted()` — the coyote window will swallow the upward motion and delay the rising animation by `_coyoteTime` seconds |
| MEDIUM | `CharacterController.velocity.magnitude` used for animation — Y component inflates value; strip Y before normalizing |
| MEDIUM | Accumulated angle (`_yaw`, `_angle`) without `% 360f` modulo |
| MEDIUM | `eulerAngles` used as signed source without `Mathf.DeltaAngle` conversion |
