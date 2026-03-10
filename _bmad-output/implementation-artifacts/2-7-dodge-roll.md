# Story 2.7: Dodge Roll

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to dodge roll in any direction,
so that I can evade enemy attacks with i-frame immunity during the roll.

## Acceptance Criteria

1. A `Dodge` input action exists in `InputSystem_Actions`:
   - **Keyboard binding:** `<Keyboard>/leftCtrl` (Left Ctrl)
   - **Note on GDD conflict:** GDD says "Space + WASD direction" but Space is already Jump (Story 1.6); Left Ctrl is used to avoid conflict. Key rebinding is Epic 8 scope.
   - Added to **both** `InputSystem_Actions.inputactions` (for editor UI) AND embedded JSON in `InputSystem_Actions.cs` (runtime — see CLAUDE.md critical note)
   - Action type: `Button`, expectedControlType: `Button`

2. `CombatConfigSO` gains two new config fields in a `[Header("Dodge")]` block:
   - `public float dodgeDuration = 0.5f` — seconds the roll lasts
   - `public float dodgeSpeed = 8f` — units/sec horizontal speed during roll
   - `dodgeStaminaCost = 25f` **already exists** — do NOT add again

3. A new `DodgeController.cs` exists at `Assets/_Game/Scripts/Combat/DodgeController.cs`:
   - `namespace Game.Combat`
   - `private const string TAG = "[Combat]";`
   - `[RequireComponent(typeof(CharacterController))]`
   - `[RequireComponent(typeof(StaminaSystem))]`
   - `[RequireComponent(typeof(PlayerStateManager))]`
   - `[SerializeField] private CombatConfigSO _config`
   - Cached fields in `Awake()` with null guards:
     - `_characterController` = GetComponent<CharacterController>()
     - `_staminaSystem` = GetComponent<StaminaSystem>()
     - `_stateManager` = GetComponent<PlayerStateManager>()
     - `_mainCamera` = Camera.main (warn-only, not disable)
     - `_input` = new InputSystem_Actions()
   - Private state: `_dodgeDirection` (Vector3), `_dodgeTimer` (float), `_dodgeVerticalVelocity` (float)
   - **OnEnable():** null guard → enable Player map → subscribe `_input.Player.Dodge.started += OnDodgeStarted`
   - **OnDisable():** `if (_input == null) return;` → unsubscribe → Disable → Dispose → null
   - **Update():** if `_stateManager.IsDodging` → tick timer, apply CharacterController.Move(), stop when timer ≤ 0
   - **OnDodgeStarted():** gate checks → consume stamina → compute direction → SetDodging(true)

4. Gate order in `OnDodgeStarted()` (all gates before stamina consumption):
   ```
   1. IsAirborne → "Cannot dodge while airborne"
   2. IsBlocking → "Cannot dodge while blocking"
   3. IsDodging  → "Cannot dodge: already dodging"
   4. HasEnough(dodgeStaminaCost) → "Cannot dodge: insufficient stamina"
   5. Consume(dodgeStaminaCost) → on false: log Error + return
   6. Compute direction
   7. If IsAttacking → SetAttacking(false) to cancel the combo (attack-cancel dodge)
   8. _stateManager.SetDodging(true)
   ```
   **Note:** Attacking is NOT a gate — dodge cancels attacks (attack-cancel mechanic).

5. Dodge direction computation:
   - Read `_input.Player.Move.ReadValue<Vector2>()` at dodge moment
   - If magnitude ≥ 0.1f: convert to camera-relative world direction (same pattern as PlayerController):
     ```csharp
     Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
     Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
     _dodgeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
     ```
   - If magnitude < 0.1f (no input): `_dodgeDirection = -transform.forward` (backward roll)
   - If `_mainCamera == null`: fallback to `new Vector3(moveInput.x, 0f, moveInput.y).normalized` or `Vector3.back`

6. Dodge movement (called each frame while `_stateManager.IsDodging`):
   ```csharp
   _dodgeTimer -= Time.deltaTime;
   _dodgeVerticalVelocity += -9.81f * Time.deltaTime;  // gravity accumulates
   Vector3 velocity = _dodgeDirection * _config.dodgeSpeed + Vector3.up * _dodgeVerticalVelocity;
   _characterController.Move(velocity * Time.deltaTime);
   if (_dodgeTimer <= 0f)
   {
       _stateManager.SetDodging(false);
       _dodgeVerticalVelocity = 0f;
       GameLog.Info(TAG, "Dodge complete");
   }
   ```
   - On dodge start: `_dodgeTimer = _config.dodgeDuration; _dodgeVerticalVelocity = -2f;` (match GROUNDED_VELOCITY constant from PlayerController)

7. `PlayerController` is updated to skip `Update()` logic when dodging:
   - Cache `private PlayerStateManager _stateManager;` in `Awake()` via `GetComponent<PlayerStateManager>()`
   - Null guard: `GameLog.Warn(TAG, "PlayerStateManager not found — dodge gating unavailable");` (warn only, do NOT disable — movement is not blocked by missing DodgeController)
   - In `Update()`: wrap all three calls in `if (_stateManager == null || !_stateManager.IsDodging)` block:
     ```csharp
     if (_stateManager == null || !_stateManager.IsDodging)
     {
         ApplyJump();
         ApplyGravity();
         ApplyMovement();
     }
     ```
   - **Cross-system note:** `Scripts/Player/` → `Scripts/Combat/` dependency via `PlayerStateManager` is intentional; PSM is documented as "read by any system needing combat gate info" (Story 2.6 Dev Notes). Flag for future refactor: move `PlayerStateManager` to `Scripts/Player/` in a future story.

8. `PlayerCombat.cs` — `HitResult` enum gains a `Dodged` value:
   ```csharp
   public enum HitResult { PerfectBlock, Blocked, NotBlocked, Dodged }
   ```
   And `TryReceiveHit()` gains an i-frame check as its **first guard** (before the `!IsBlocking` check):
   ```csharp
   if (_stateManager.IsDodging)
   {
       GameLog.Info(TAG, "Dodge i-frame — hit missed");
       return HitResult.Dodged;
   }
   ```

9. `PlayerStateManager.SetDodging()` is updated to accept an `isBackwardRoll` parameter and fire the appropriate animator trigger:
   - `SetDodging(bool value, bool isBackwardRoll = false)`
   - On `value = true`: fires `IsDodging` trigger (forward roll) or `IsDodgingBackwards` trigger (backward roll)
   - `DodgeController` passes `isBackwardRoll: true` when no directional input was held
   - `CanAttack()` and `CanBlock()` now also gate on `!IsDodging` (cannot attack or block during a roll)

10. `DodgeController` debug overlay (`OnGUI()` inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR`) at y=190:
    ```csharp
    string dodgeState = _stateManager.IsDodging
        ? $"ROLLING {_dodgeTimer:F2}s"
        : "ready";
    GUI.Label(new Rect(10, 190, 500, 26),
        $"Dodge: {dodgeState} | CanDodge:{_staminaSystem.HasEnough(_config.dodgeStaminaCost)}",
        style);
    ```
    Uses `new GUIStyle(GUI.skin.label) { fontSize = 18 }` — consistent with existing overlay pattern.

11. Player prefab (`Assets/_Game/Prefabs/Player/Player.prefab`): `DodgeController` component added to root GameObject. CombatConfigSO must be assigned in Inspector.

12. Edit Mode tests: `DodgeGateTests.cs` at `Assets/Tests/EditMode/DodgeGateTests.cs` with ≥ 6 tests:
    - `CanDodge_ReturnsFalse_WhenAirborne()`
    - `CanDodge_ReturnsTrue_WhenAttacking()` — attacking is **not** a gate; dodge cancels attacks (attack-cancel mechanic)
    - `CanDodge_ReturnsFalse_WhenBlocking()`
    - `CanDodge_ReturnsFalse_WhenAlreadyDodging()`
    - `CanDodge_ReturnsFalse_WhenInsufficientStamina()`
    - `CanDodge_ReturnsTrue_WhenAllConditionsMet()`
    - Test helper signature: `bool CanDodge(bool isAirborne, bool isBlocking, bool isDodging, float stamina, float dodgeCost)` — no `isAttacking` param since attacking is not a gate

13. No compile errors. All existing 35 Edit Mode tests pass. New total: ≥ 41.

14. Play Mode validation: Left Ctrl triggers dodge roll in WASD direction (or backward). Stamina decreases by 25. Cannot be attacked (TryReceiveHit returns Dodged). Cannot attack/block/dodge during roll. Pressing Left Ctrl while jumping/attacking/blocking does nothing. After roll ends (~0.5s), normal movement and combat resume.

## Tasks / Subtasks

- [x] Task 1: Add Dodge input action (AC: 1)
  - [x] 1.1 Edit `InputSystem_Actions.inputactions`: add `"Dodge"` Button action to Player actions array
  - [x] 1.2 Add `<Keyboard>/leftCtrl` binding for Dodge in `.inputactions`
  - [x] 1.3 Edit `InputSystem_Actions.cs` embedded JSON: add same Dodge action + binding (using `""` double-escaped quotes)
  - [x] 1.4 Add `m_Player_Dodge` private field to `InputSystem_Actions`
  - [x] 1.5 Add `FindAction("Dodge", throwIfNotFound: true)` to `Initialize()`
  - [x] 1.6 Add `public InputAction @Dodge => m_Wrapper.m_Player_Dodge;` to `PlayerActions` struct

- [x] Task 2: Extend CombatConfigSO (AC: 2)
  - [x] 2.1 Add `[Header("Dodge")]` section with `dodgeDuration = 0.5f` and `dodgeSpeed = 8f`
  - [x] 2.2 Verify `dodgeStaminaCost = 25f` already exists (do NOT duplicate)

- [x] Task 3: Create DodgeController.cs (AC: 3–6, 10)
  - [x] 3.1 Create `Assets/_Game/Scripts/Combat/DodgeController.cs`
  - [x] 3.2 Implement Awake() with all null guards
  - [x] 3.3 Implement OnEnable() / OnDisable() with null guard pattern
  - [x] 3.4 Implement OnDodgeStarted() with all gate checks, stamina consumption, direction computation, SetDodging(true)
  - [x] 3.5 Implement Update() dodge movement with gravity + timer countdown, SetDodging(false) on expiry
  - [x] 3.6 Add OnGUI() debug overlay at y=190

- [x] Task 4: Update PlayerController.cs (AC: 7)
  - [x] 4.1 Cache `PlayerStateManager _stateManager` in Awake (warn-only null guard)
  - [x] 4.2 Wrap ApplyJump/ApplyGravity/ApplyMovement in IsDodging check

- [x] Task 5: Update PlayerCombat.cs (AC: 8)
  - [x] 5.1 Add `Dodged` to `HitResult` enum
  - [x] 5.2 Add i-frame check as first guard in `TryReceiveHit()`

- [x] Task 6: Update Player prefab (AC: 11)
  - [x] 6.1 Add `DodgeController` component to Player prefab root
  - [x] 6.2 Assign `CombatConfigSO` in DodgeController Inspector slot

- [x] Task 7: Edit Mode tests (AC: 12)
  - [x] 7.1 Create `Assets/Tests/EditMode/DodgeGateTests.cs` with ≥ 6 tests (pure formula pattern)
  - [x] 7.2 Run all tests via Unity Test Runner — all 41+ green

- [ ] Task 8: Play Mode validation (AC: 14) — requires Unity Editor (manual)
  - [ ] 8.1 Enter Play Mode — no compile or runtime errors
  - [ ] 8.2 Press Left Ctrl with WASD held → rolls in that direction, stamina decreases 25
  - [ ] 8.3 Press Left Ctrl with no WASD → backward roll
  - [ ] 8.4 Call TryReceiveHit() during roll in code/test → returns HitResult.Dodged
  - [ ] 8.5 Press Left Ctrl while jumping → nothing (airborne gate)
  - [ ] 8.6 Press Left Ctrl while attacking → dodge executes, attack is cancelled (attack-cancel dodge)
  - [ ] 8.7 Press Left Ctrl while blocking → nothing (blocking gate)
  - [ ] 8.8 Press Left Ctrl during a roll → nothing (already dodging gate)
  - [ ] 8.9 OnGUI shows "Dodge: ROLLING 0.48s" during roll, "Dodge: ready" at rest
  - [ ] 8.10 Existing combat (WASD, camera, jump, stamina, combo, block, PB) unchanged

## Dev Notes

Story 2.7 implements the dodge roll mechanic: directional roll with i-frames and stamina cost. The `PlayerStateManager.SetDodging()` stub added in Story 2.6 is now fully activated. `DodgeController` is a new `Scripts/Combat/` component that owns all dodge logic.

### Critical: InputSystem_Actions Dual-Edit Rule

**YOU MUST EDIT BOTH FILES.** From CLAUDE.md:
> `InputSystem_Actions.cs` embeds the entire action map JSON as a string literal in its constructor. The `.inputactions` file is only used by Unity's editor UI.

Editing only `.inputactions` will NOT add the Dodge action at runtime. `FindAction("Dodge", throwIfNotFound: true)` will throw `ArgumentException`.

**In `.inputactions`** — add to `"actions"` array in the Player map (after Block):
```json
{
    "name": "Dodge",
    "type": "Button",
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```
Generate a real UUID for the id field (do NOT use the placeholder above).

**Add binding** in `"bindings"` array (after the Block binding):
```json
{
    "name": "",
    "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "path": "<Keyboard>/leftCtrl",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "Dodge",
    "isComposite": false,
    "isPartOfComposite": false
}
```

**In `InputSystem_Actions.cs`** — same JSON but using `""` (double-escaped quotes) in the C# string literal. Add:
- After `""name"": ""Block""` block in actions array: add `""name"": ""Dodge""` block
- After the Block binding: add the Dodge binding
- After line ~1176: `m_Player_Dodge = m_Player.FindAction("Dodge", throwIfNotFound: true);`
- After `private readonly InputAction m_Player_Block;`: `private readonly InputAction m_Player_Dodge;`
- In `PlayerActions` struct after `@Block` property: `/// <summary>Provides access to the underlying input action "Player/Dodge".</summary>\n public InputAction @Dodge => m_Wrapper.m_Player_Dodge;`

### Complete DodgeController.cs Implementation

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles dodge roll input, gating, stamina, movement, and i-frame state.
    /// Dodge direction is camera-relative based on Move input at the moment of dodge.
    /// Falls back to backward roll when no WASD input is held.
    /// During dodge, drives CharacterController.Move() directly (PlayerController pauses).
    /// Story 2.7: Initial implementation.
    /// Attach to the Player prefab root alongside PlayerCombat, StaminaSystem, PlayerStateManager.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(StaminaSystem))]
    [RequireComponent(typeof(PlayerStateManager))]
    public class DodgeController : MonoBehaviour
    {
        private const string TAG = "[Combat]";
        private const float GRAVITY = -9.81f;
        private const float GROUNDED_VELOCITY = -2f;

        [SerializeField] private CombatConfigSO _config;

        private CharacterController _characterController;
        private StaminaSystem _staminaSystem;
        private PlayerStateManager _stateManager;
        private Camera _mainCamera;
        private InputSystem_Actions _input;

        private Vector3 _dodgeDirection;
        private float _dodgeTimer;
        private float _dodgeVerticalVelocity;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — DodgeController disabled");
                enabled = false;
                return;
            }

            _characterController = GetComponent<CharacterController>();
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
            {
                GameLog.Error(TAG, "StaminaSystem not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _stateManager = GetComponent<PlayerStateManager>();
            if (_stateManager == null)
            {
                GameLog.Error(TAG, "PlayerStateManager not found — DodgeController disabled");
                enabled = false;
                return;
            }

            _mainCamera = Camera.main;
            if (_mainCamera == null)
                GameLog.Warn(TAG, "Camera.main not found — dodge direction will use world-space fallback");

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Dodge.started += OnDodgeStarted;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Dodge.started -= OnDodgeStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (!_stateManager.IsDodging) return;

            _dodgeTimer -= Time.deltaTime;
            _dodgeVerticalVelocity += GRAVITY * Time.deltaTime;

            Vector3 velocity = _dodgeDirection * _config.dodgeSpeed
                             + Vector3.up * _dodgeVerticalVelocity;
            _characterController.Move(velocity * Time.deltaTime);

            if (_dodgeTimer <= 0f)
            {
                _stateManager.SetDodging(false);
                _dodgeVerticalVelocity = 0f;
                GameLog.Info(TAG, "Dodge roll complete");
            }
        }

        private void OnDodgeStarted(InputAction.CallbackContext ctx)
        {
            // Gate 1: airborne
            if (_stateManager.IsAirborne)
            {
                GameLog.Warn(TAG, "Cannot dodge while airborne");
                return;
            }

            // Gate 2: attacking (mid-combo)
            if (_stateManager.IsAttacking)
            {
                GameLog.Warn(TAG, "Cannot dodge while attacking");
                return;
            }

            // Gate 3: blocking
            if (_stateManager.IsBlocking)
            {
                GameLog.Warn(TAG, "Cannot dodge while blocking");
                return;
            }

            // Gate 4: already dodging
            if (_stateManager.IsDodging)
            {
                GameLog.Warn(TAG, "Cannot dodge: already dodging");
                return;
            }

            // Gate 5: stamina
            if (!_staminaSystem.HasEnough(_config.dodgeStaminaCost))
            {
                GameLog.Warn(TAG, "Cannot dodge: insufficient stamina");
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.dodgeStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                return;
            }

            // Compute camera-relative dodge direction from current Move input
            Vector2 moveInput = _input.Player.Move.ReadValue<Vector2>();
            if (moveInput.magnitude >= 0.1f)
            {
                if (_mainCamera != null)
                {
                    Vector3 camForward = Vector3.Scale(_mainCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized;
                    Vector3 camRight   = Vector3.Scale(_mainCamera.transform.right,   new Vector3(1f, 0f, 1f)).normalized;
                    _dodgeDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
                }
                else
                {
                    _dodgeDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                }
            }
            else
            {
                // No directional input — backward roll
                _dodgeDirection = -transform.forward;
                _dodgeDirection.y = 0f;
                if (_dodgeDirection.sqrMagnitude < 0.001f)
                    _dodgeDirection = Vector3.back; // absolute fallback
                else
                    _dodgeDirection.Normalize();
            }

            _dodgeTimer = _config.dodgeDuration;
            _dodgeVerticalVelocity = GROUNDED_VELOCITY;  // start with ground-snap velocity

            _stateManager.SetDodging(true);
            GameLog.Info(TAG, $"Dodge roll started: dir={_dodgeDirection}, dur={_config.dodgeDuration}s");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null || _stateManager == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            string dodgeState = _stateManager.IsDodging
                ? $"ROLLING {_dodgeTimer:F2}s"
                : "ready";
            GUI.Label(new Rect(10, 190, 500, 26),
                $"Dodge: {dodgeState} | CanDodge:{_staminaSystem.HasEnough(_config.dodgeStaminaCost)}",
                style);
        }
#endif
    }
}
```

### PlayerController.cs — Required Changes

```csharp
// ADD field (after existing _verticalVelocity):
private PlayerStateManager _stateManager;

// IN Awake(), AFTER _mainCamera assignment (warn-only, NOT disable):
_stateManager = GetComponent<PlayerStateManager>();
if (_stateManager == null)
    GameLog.Warn(TAG, "PlayerStateManager not found on Player — dodge gating unavailable");

// REPLACE the Update() body with:
private void Update()
{
    if (_characterController == null || _config == null)
        return;

    // Yield control to DodgeController during dodge
    if (_stateManager != null && _stateManager.IsDodging)
        return;

    ApplyJump();
    ApplyGravity();
    ApplyMovement();
}
```

**Cross-system note:** `PlayerController` (Scripts/Player/) references `PlayerStateManager` (Scripts/Combat/) — a cross-system read-only dependency. This is documented as pragmatic. Future Story: move `PlayerStateManager` to `Scripts/Player/` to eliminate the cross-system reference.

### PlayerCombat.cs — Required Changes

```csharp
// CHANGE the enum (add Dodged):
public enum HitResult { PerfectBlock, Blocked, NotBlocked, Dodged }

// IN TryReceiveHit(), ADD as FIRST guard (before the !IsBlocking check):
if (_stateManager.IsDodging)
{
    GameLog.Info(TAG, "Dodge i-frame — hit missed");
    return HitResult.Dodged;
}
```

### CombatConfigSO.cs — Required Changes

```csharp
// ADD after the existing dodgeStaminaCost field:
[Header("Dodge")]
[Tooltip("Duration in seconds of the dodge roll movement and i-frame window.")]
public float dodgeDuration = 0.5f;
[Tooltip("Horizontal speed in units/sec during the dodge roll.")]
public float dodgeSpeed = 8f;
```

**Warning:** `dodgeStaminaCost = 25f` already exists in the `[Header("Stamina Costs")]` block — do NOT add it again.

### Edit Mode Test Pattern

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for DodgeController gate logic.
/// Pure formula simulation — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests, PerfectBlockTests, PlayerStateManagerTests.
/// </summary>
public class DodgeGateTests
{
    private bool CanDodge(bool isAirborne, bool isAttacking, bool isBlocking,
                          bool isDodging, float stamina, float dodgeCost)
        => !isAirborne && !isAttacking && !isBlocking && !isDodging && stamina >= dodgeCost;

    [Test]
    public void CanDodge_ReturnsFalse_WhenAirborne()
        => Assert.That(CanDodge(true, false, false, false, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenAttacking()
        => Assert.That(CanDodge(false, true, false, false, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenBlocking()
        => Assert.That(CanDodge(false, false, true, false, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenAlreadyDodging()
        => Assert.That(CanDodge(false, false, false, true, 100f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsFalse_WhenInsufficientStamina()
        => Assert.That(CanDodge(false, false, false, false, 24f, 25f), Is.False);

    [Test]
    public void CanDodge_ReturnsTrue_WhenAllConditionsMet()
    {
        Assert.That(CanDodge(false, false, false, false, 25f, 25f), Is.True);
        Assert.That(CanDodge(false, false, false, false, 100f, 25f), Is.True);
    }
}
```

### OnGUI Debug Display Stack (After This Story)

```
[y=50]   Stamina: 80 / 100                                             ← StaminaSystem
[y=70]   Combat: [Ready]                                               ← PlayerCombat stamina gate
[y=100]  Combo: step 0 | closed                                        ← PlayerCombat combo state
[y=130]  Block: lowered | PB: closed                                   ← PlayerCombat block + PB window
[y=160]  State: Airborne:False | Blocking:False | Attacking:False       ← PlayerStateManager
[y=190]  Dodge: ready | CanDodge:True                                  ← DodgeController (NEW)
```

### PlayerStateManager.SetDodging() — Updated Signature

`SetDodging` was extended to fire the appropriate animator trigger on roll start:

```csharp
private static readonly int IsDodgingHash       = Animator.StringToHash("IsDodging");
private static readonly int IsDodgingBackwards   = Animator.StringToHash("IsDodgingBackwards");

/// <summary>Sets dodging state. Fires the forward or backward roll animator trigger on entry.</summary>
public void SetDodging(bool value, bool isBackwardRoll = false)
{
    IsDodging = value;
    if (_animator != null && value)
        _animator.SetTrigger(isBackwardRoll ? IsDodgingBackwards : IsDodgingHash);
}
```

`DodgeController.OnDodgeStarted()` tracks whether input was absent (backward roll) via a local `isBackwardRoll` bool set in the direction-computation block, then passes it to `SetDodging(true, isBackwardRoll)`.

**CanAttack / CanBlock also updated:** Both now gate on `!IsDodging` — attacking or blocking is impossible during the roll window.

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `Scripts/Combat/DodgeController.cs` |
| No magic numbers | ✅ All values from CombatConfigSO; GRAVITY and GROUNDED_VELOCITY are constants matching PlayerController |
| GameLog only | ✅ All logging via `GameLog.Warn/Error/Info(TAG, ...)` with `[Combat]` TAG |
| Null-guard in Awake | ✅ All 4 required refs null-guarded with disable; _mainCamera warn-only |
| OnDisable null guard | ✅ `if (_input == null) return;` guard matches pattern from CLAUDE.md |
| Event subscription in OnEnable/OnDisable | ✅ Dodge.started subscribed in OnEnable, unsubscribed in OnDisable |
| No GetComponent in Update | ✅ All refs cached in Awake |
| Camera.main cached | ✅ Cached in Awake, not called in Update |
| No string allocations in hot path | ✅ Debug-only OnGUI is excluded from hot path |
| Not a singleton | ✅ Lives on Player prefab, accessed via GetComponent |
| Same-system direct refs | ✅ DodgeController → StaminaSystem, PlayerStateManager all in Scripts/Combat/ |
| Cross-system read: PlayerController → PlayerStateManager | ⚠️ Documented pragmatic exception; PlayerController reads IsDodging state (read-only, no circular dependency) |
| InputSystem dual-edit | ✅ Story explicitly requires editing both .inputactions AND embedded .cs JSON |
| WriteDefaultValues: false | N/A — no new animator states in this story |
| Dodge animation triggers | ✅ `IsDodging` / `IsDodgingBackwards` triggers fire via `SetDodging()`; animation clips deferred to Epic 8 |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Combat/DodgeController.cs        ← NEW dodge controller
Assets/_Game/Scripts/Combat/DodgeController.cs.meta  ← auto-generated by Unity
Assets/Tests/EditMode/DodgeGateTests.cs              ← NEW Edit Mode tests (≥6 tests)
Assets/Tests/EditMode/DodgeGateTests.cs.meta         ← auto-generated by Unity
```

**Files to MODIFY:**
```
Assets/_Game/InputSystem_Actions.inputactions        ← Add Dodge action + Left Ctrl binding
Assets/_Game/InputSystem_Actions.cs                  ← Add Dodge to embedded JSON + C# wrappers
Assets/_Game/Scripts/Combat/PlayerCombat.cs          ← Add HitResult.Dodged; i-frame check in TryReceiveHit
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs  ← Add dodgeDuration + dodgeSpeed
Assets/_Game/Scripts/Player/PlayerController.cs      ← Cache PlayerStateManager; skip Update when dodging
Assets/_Game/Prefabs/Player/Player.prefab            ← Add DodgeController component
```

**Scripts/Combat/ after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs        ← Story 2.1 (unchanged)
├── PlayerCombat.cs         ← Stories 2.2–2.6 + i-frame check + Dodged enum value
├── PlayerStateManager.cs   ← Story 2.6 (unchanged — stub already correct)
└── DodgeController.cs      ← NEW Story 2.7
```

**Player prefab root component list after this story:**
```
Player (root)
├── CharacterController      (established Story 1.1)
├── Animator                 (established Story 1.4)
├── PlayerController         (established Story 1.1) ← modified (IsDodging check)
├── PlayerAnimator           (established Story 1.4)
├── StaminaSystem            (established Story 2.1)
├── PlayerCombat             (established Story 2.2) ← modified (Dodged enum + i-frame)
├── PlayerStateManager       (established Story 2.6)
└── DodgeController          ← NEW Story 2.7
```

### Block-Cancel Mechanic (Intentional Design)

`PlayerStateManager.CanBlock()` gates on `!IsAirborne && !IsDodging` only — it does **not** gate on `!IsAttacking`. This means a player can raise a block while mid-combo, which cancels the current attack chain. This is a deliberate block-cancel mechanic mirroring the attack-cancel dodge design. `PlayerCombat.OnBlockStarted()` handles the combo reset when block is raised during an active attack.

### Known Limitations (Prototype Acceptable)

1. **Dodge animation triggers wired, clips pending:** `PlayerStateManager.SetDodging()` fires `IsDodging` (forward) or `IsDodgingBackwards` (backward) animator triggers. The AnimatorController requires matching states and transition rules for these triggers; the actual animation clips require art assets planned for Epic 8. Until clips are imported the character will snap to T-pose during the roll.
2. **No dodge dust/VFX:** Epic 8 scope.
3. **Gravity discontinuity:** DodgeController initializes `_dodgeVerticalVelocity = GROUNDED_VELOCITY` (-2f). If the player was mid-jump when dodge triggers (gated by `IsAirborne` → cannot happen). If the player was grounded, velocity starts at -2f which matches the grounded snap — correct.
4. **Cross-system PlayerController → PlayerStateManager:** Documented above; flag for future PlayerStateManager relocation.

### References

- Epic 2 story 7 ("As a player, I can dodge roll in any direction with i-frames"): [Source: _bmad-output/epics.md#Epic 2: Combat System]
- GDD dodge controls ("Space + WASD direction"): [Source: _bmad-output/gdd.md#Controls and Input]
- GDD i-frame description ("Invincibility frames during roll"): [Source: _bmad-output/gdd.md#Stamina System]
- Architecture DodgeController planned location: [Source: _bmad-output/game-architecture.md#Directory Structure]
- CombatConfigSO dodgeStaminaCost = 25f (already exists): [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- PlayerStateManager.SetDodging() stub (Story 2.6): [Source: Assets/_Game/Scripts/Combat/PlayerStateManager.cs:72]
- Story 2.6 Dev Notes — Story 2.7 prerequisites: [Source: _bmad-output/implementation-artifacts/2-6-player-state-manager.md#Dev Notes]
- CLAUDE.md InputSystem dual-edit rule: [Source: CLAUDE.md#InputSystem_Actions.cs Embeds the Full JSON]
- CLAUDE.md OnDisable null guard pattern: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- CLAUDE.md Camera.main caching rule: [Source: _bmad-output/project-context.md#Performance Rules]
- Camera-relative direction pattern from PlayerController: [Source: Assets/_Game/Scripts/Player/PlayerController.cs:99-103]
- PlayerController GROUNDED_VELOCITY = -2f constant: [Source: Assets/_Game/Scripts/Player/PlayerController.cs:18]
- CharacterController.isGrounded is authoritative grounded check (not velocity.y): [Source: CLAUDE.md#CharacterController.velocity Includes Y Component]
- HitResult enum in PlayerCombat.cs: [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs:8]
- TryReceiveHit() full implementation: [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs:265-295]
- Tests.EditMode.asmdef references "Game": [Source: CLAUDE.md#Assembly Setup]
- Same-system direct reference rule: [Source: _bmad-output/game-architecture.md#Standard Patterns — Component Communication]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

No issues encountered during implementation.

### Completion Notes List

- Task 1 complete: Dodge action added to both `InputSystem_Actions.inputactions` (editor) and embedded JSON in `InputSystem_Actions.cs` (runtime), with `<Keyboard>/leftCtrl` binding. Added `m_Player_Dodge` field, `FindAction` call, and `@Dodge` property.
- Task 2 complete: `CombatConfigSO` gains `dodgeDuration = 0.5f` and `dodgeSpeed = 8f` under `[Header("Dodge")]`. `dodgeStaminaCost` was already present — not duplicated.
- Task 3 complete: `DodgeController.cs` created with all required components: `[RequireComponent]` attributes, null guards in `Awake`, `OnEnable`/`OnDisable` with null guard pattern, `OnDodgeStarted` with all 5 gates + stamina consume + camera-relative direction computation, `Update` with gravity accumulation + timer countdown, `OnGUI` debug overlay at y=190.
- Task 4 complete: `PlayerController` caches `PlayerStateManager` in `Awake` (warn-only), `Update` now returns early if `IsDodging` is true.
- Task 5 complete: `HitResult` enum gains `Dodged` value; `TryReceiveHit` has i-frame check as first guard.
- Task 6 complete: `DodgeController` component added to Player prefab root with `CombatConfig.asset` assigned via Unity MCP.
- Task 7 complete: `DodgeGateTests.cs` created with 6 tests covering all dodge gates. All 41 Edit Mode tests pass (35 existing + 6 new).
- Task 8: Pending manual Play Mode validation by Valentin.

### File List

**Created:**
- `Assets/_Game/Scripts/Combat/DodgeController.cs`
- `Assets/_Game/Scripts/Combat/DodgeController.cs.meta`
- `Assets/Tests/EditMode/DodgeGateTests.cs`
- `Assets/Tests/EditMode/DodgeGateTests.cs.meta`
- `Assets/_Game/Art/Characters/Player/Animations/Dodge back.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/Dodge back.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/dodge roll.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/dodge roll.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/Block Idle.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/Block Idle.fbx.meta`

**Modified:**
- `Assets/_Game/InputSystem_Actions.inputactions`
- `Assets/_Game/InputSystem_Actions.cs`
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs`
- `Assets/_Game/Scripts/Player/PlayerController.cs`
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
- `Assets/_Game/Scripts/Combat/PlayerStateManager.cs`
- `Assets/_Game/Prefabs/Player/Player.prefab`

## Change Log

- 2026-03-10: Story 2.7 implemented — dodge roll with i-frames, stamina cost, camera-relative direction, gate system; 6 new Edit Mode tests added (total: 41).
- 2026-03-10: Refactor — state gate checks moved to `PlayerStateManager.CanAttack/CanBlock/CanDodge/CanJump()`; attacking is not a gate for dodge (attack-cancel mechanic).
- 2026-03-10: Animation triggers added — `PlayerStateManager.SetDodging()` fires `IsDodging` / `IsDodgingBackwards` animator triggers; `DodgeController` passes `isBackwardRoll` flag; `CanAttack()` and `CanBlock()` now also gate on `!IsDodging`.
- 2026-03-10: Code review fixes — deleted duplicate auto-generated `Assets/InputSystem_Actions.cs` at root; per-gate log messages restored in `DodgeController.OnDodgeStarted()`; `_dodgeTimer` reset to 0f on dodge end; `CanBlock()` block-cancel mechanic documented; FBX animation assets added to File List; AC 12 updated to reflect attack-cancel test names; Status set to `review`.
