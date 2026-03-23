# Story 7.12: Combat State Toggle

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to press R to draw my weapon and enter combat stance (and R again to sheathe it and return to default),
so that my character visually reflects whether I'm ready to fight, and attacking/blocking are intentionally gated to when the weapon is drawn.

## Acceptance Criteria

1. Pressing R toggles `IsInCombat` on `PlayerStateManager` (true → false → true…); R is ignored while `IsBusy`
2. `CanAttack()` returns `false` when `!IsInCombat` — attacks are impossible with weapon sheathed
3. `CanBlock()` returns `false` when `!IsInCombat` — blocking is impossible with weapon sheathed
4. `CanDodge()` is unchanged — dodging always permitted regardless of combat state
5. When entering InCombat (`SetCombatState(true)`): `_weaponVisual` is reparented to `_weaponSocket` (hand)
6. When exiting InCombat (`SetCombatState(false)`): `_weaponVisual` is reparented to `_undrawnWeaponSocket` (hip/scabbard)
7. Player prefab has an `UndrawnWeaponSocket` empty child GO added under the hip bone; position left for manual tuning by Valentin
8. On initial equip (`EquipmentVisuals.RefreshWeapon()`), weapon visual attaches to the correct socket based on current `_isInCombat` state — not always to the hand socket
9. All 203 EditMode tests pass — 0 regressions

## Tasks / Subtasks

- [x] Task 1: Add `DrawWeapon` input action (R key) to both InputSystem files (AC: 1)
  - [x] Edit `Assets/_Game/InputSystem_Actions.inputactions` — append `DrawWeapon` entry to the Player map `actions` array (after the last existing action), with a single binding to `<Keyboard>/r` (see Dev Notes for exact JSON format)
  - [x] Edit `Assets/_Game/InputSystem_Actions.cs` — find the embedded JSON string in the constructor (search for `"InventoryToggle"` or `"LockOn"` to locate end of Player actions list); append the same `DrawWeapon` action JSON with double-escaped quotes (`\"` instead of `"`)
  - [x] Verify compilation — no `ArgumentException: FindAction("DrawWeapon")` errors

- [x] Task 2: Add `IsInCombat` state + `SetInCombat()` to `PlayerStateManager` (AC: 1, 2, 3, 4)
  - [x] Add `public bool IsInCombat { get; private set; }` property
  - [x] Add `public void SetInCombat(bool value)` setter: set `IsInCombat = value`; call `_playerAnimator.SetInCombat(value)`; `GameLog.Info(TAG, $"Combat stance: {(value ? "DRAWN" : "sheathed")}")`
  - [x] Update `CanAttack()`: add `&& IsInCombat` condition — result: `!IsBusy && !IsAirborne && !IsBlocking && !IsDodging && IsInCombat`
  - [x] Update `CanBlock()`: add `&& IsInCombat` condition — result: `!IsBusy && !IsAirborne && !IsDodging && IsInCombat`
  - [x] Leave `CanDodge()` **unchanged** — `!IsBusy && !IsAirborne && !IsBlocking && !IsDodging`
  - [x] Verify compilation

- [x] Task 3: Add `SetInCombat()` stub to `PlayerAnimator` (AC: 1)
  - [x] Add `private static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");` to the Combat parameters section
  - [x] Add `public void SetInCombat(bool value)` method: `if (_animator != null) _animator.SetBool(IsInCombatHash, value);`
  - [x] Note: `IsInCombat` animator parameter does NOT exist in the controller yet (added in Story 7.13) — `SetBool` on a missing parameter is a silent no-op in Unity; this is expected behavior for 7.12
  - [x] Verify compilation

- [x] Task 4: Add `OnDrawWeaponStarted` handler to `PlayerCombat` (AC: 1)
  - [x] Add `private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)` handler:
    - Check `if (_stateManager.IsBusy) return;`
    - `bool entering = !_stateManager.IsInCombat;`
    - `_stateManager.SetInCombat(entering);`
    - `_equipmentVisuals?.SetCombatState(entering);`
  - [x] In `OnEnable()`: subscribe `_input.Player.DrawWeapon.started += OnDrawWeaponStarted;`
  - [x] In `OnDisable()`: unsubscribe `_input.Player.DrawWeapon.started -= OnDrawWeaponStarted;` (add before the `if (_input == null) return;` guard, alongside the other unsubscribes)
  - [x] Verify compilation

- [x] Task 5: Add `SetCombatState()` + `_undrawnWeaponSocket` to `EquipmentVisuals` (AC: 5, 6, 8)
  - [x] Add `[SerializeField] private Transform _undrawnWeaponSocket;` field
  - [x] Add `private bool _isInCombat = false;` private field
  - [x] Add `public void SetCombatState(bool isInCombat)` method (see Dev Notes for exact code — includes null guard for `_weaponVisual`, null fallback to `_weaponSocket` if `_undrawnWeaponSocket` unassigned)
  - [x] Update `RefreshWeapon()`: when attaching `_weaponVisual`, parent it to `var targetSocket = _isInCombat ? _weaponSocket : (_undrawnWeaponSocket != null ? _undrawnWeaponSocket : _weaponSocket);` — NOT always to `_weaponSocket`
  - [x] Verify compilation

- [x] Task 6: Update `Player.prefab` — add `UndrawnWeaponSocket` GO and wire fields (AC: 7)
  - [x] Open the Player prefab at `Assets/_Game/Prefabs/Player/Player.prefab` in Unity
  - [x] Add an empty child GameObject named `UndrawnWeaponSocket` under the appropriate hip bone (same bone hierarchy as `WeaponSocket` but targeting the hip/scabbard position)
  - [x] Leave `UndrawnWeaponSocket` local position at `(0, 0, 0)` for now — Valentin adjusts manually in play mode
  - [x] Wire `_undrawnWeaponSocket` on the `EquipmentVisuals` component to the new `UndrawnWeaponSocket` GO
  - [x] Save the prefab

- [x] Task 7: Update CLAUDE.md files (AC: documentation)
  - [x] Update `Assets/_Game/Scripts/Player/CLAUDE.md` — add `IsInCombat` to the Action Gating table; add code review entry for `CanAttack`/`CanBlock` gated on `IsInCombat`
  - [x] Update `Assets/_Game/Scripts/Combat/CLAUDE.md` — add `OnDrawWeaponStarted` pattern; note that attacking/blocking while sheathed is blocked at `PlayerStateManager` level

- [x] Task 8: Play-mode validation (AC: 1–9)
  - [x] Start with weapon sheathed (default) → press LMB → verify no attack fires
  - [x] Press R → weapon moves from hip to hand → attack with LMB → verify attack fires
  - [x] Press R again → weapon moves from hip socket → verify LMB no longer attacks
  - [x] Press RMB while sheathed → verify block does not raise
  - [x] Press Shift/Dodge while sheathed → verify dodge still works
  - [x] Equip/unequip weapon while sheathed → verify weapon visual appears on hip socket
  - [x] All 203 EditMode tests pass

## Dev Notes

### Architecture Context

This story introduces the **InCombat state** to `PlayerStateManager` — a new gate that wraps existing `CanAttack()` and `CanBlock()`. The pattern is identical to `IsBlocking`, `IsAttacking`, and `IsDodging` already in the codebase.

```
R key pressed
  → PlayerCombat.OnDrawWeaponStarted()
    → PlayerStateManager.SetInCombat(entering)
      → IsInCombat = entering
      → PlayerAnimator.SetInCombat(entering)   ← bool param (layer weight added in 7.13)
    → EquipmentVisuals.SetCombatState(entering)
      → reparent _weaponVisual to correct socket

Gates after 7.12:
  CanAttack() = !IsBusy && !IsAirborne && !IsBlocking && !IsDodging && IsInCombat   ← added IsInCombat
  CanBlock()  = !IsBusy && !IsAirborne && !IsDodging && IsInCombat                   ← added IsInCombat
  CanDodge()  = !IsBusy && !IsAirborne && !IsBlocking && !IsDodging                  ← UNCHANGED
```

**`_equipmentVisuals` ref is already present in `PlayerCombat`** — it is the `[SerializeField]` field added in Story 7.9. Story 7.12 adds a new call on the same field.

### InputSystem_Actions — Adding DrawWeapon (CRITICAL: Edit Both Files)

**File 1: `InputSystem_Actions.inputactions`**

In the `"actions"` array of the Player map, append after `"ActionBar6"`:

```json
{
    "name": "DrawWeapon",
    "type": "Button",
    "id": "<generate-new-guid>",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```

In the `"bindings"` array of the Player map, append:

```json
{
    "name": "",
    "id": "<generate-new-guid>",
    "path": "<Keyboard>/r",
    "interactions": "",
    "processors": "",
    "groups": "",
    "action": "DrawWeapon",
    "isComposite": false,
    "isPartOfComposite": false
}
```

**File 2: `InputSystem_Actions.cs` (embedded JSON — double-escaped quotes)**

Search for the last action in the Player map embedded string (search for `ActionBar6`). After the `ActionBar6` JSON block (closing `}`) and before the Player map `"bindings"` array closing bracket, append the same action JSON but with `\"` instead of `"`:

```
{\"name\":\"DrawWeapon\",\"type\":\"Button\",\"id\":\"<new-guid>\",\"expectedControlType\":\"Button\",\"processors\":\"\",\"interactions\":\"\",\"initialStateCheck\":false}
```

And in the bindings section at the same level, append:

```
{\"name\":\"\",\"id\":\"<new-guid>\",\"path\":\"<Keyboard>/r\",\"interactions\":\"\",\"processors\":\"\",\"groups\":\"\",\"action\":\"DrawWeapon\",\"isComposite\":false,\"isPartOfComposite\":false}
```

> **Why both files?** `InputSystem_Actions.cs` embeds the full JSON as a string literal in its constructor — the `.inputactions` file is only used by the Unity editor UI, not at runtime. If only `.inputactions` is edited, `_input.Player.DrawWeapon` will throw `ArgumentException`. See `Assets/_Game/CLAUDE.md` for full explanation.

Generate fresh GUIDs using: `System.Guid.NewGuid().ToString()` in C# or any GUID tool.

### PlayerStateManager Changes (Exact Code)

```csharp
// New property (after IsDodging)
public bool IsInCombat { get; private set; }

// New setter
/// <summary>Sets the InCombat state and drives the IsInCombat animator bool via PlayerAnimator.</summary>
public void SetInCombat(bool value)
{
    IsInCombat = value;
    _playerAnimator.SetInCombat(value);
    GameLog.Info(TAG, $"Combat stance: {(value ? "DRAWN" : "sheathed")}");
}

// Updated gates
public bool CanAttack() => !IsBusy && !IsAirborne && !IsBlocking && !IsDodging && IsInCombat;
public bool CanBlock()  => !IsBusy && !IsAirborne && !IsDodging && IsInCombat;
// CanDodge() — UNCHANGED: !IsBusy && !IsAirborne && !IsBlocking && !IsDodging
```

> **Note on `IsInCombat` default:** `bool` defaults to `false`, so `IsInCombat = false` on game start — weapon is sheathed by default, which is the desired behavior.

### PlayerCombat Changes (Exact Code)

```csharp
// New handler
private void OnDrawWeaponStarted(InputAction.CallbackContext ctx)
{
    if (_stateManager.IsBusy) return;
    bool entering = !_stateManager.IsInCombat;
    _stateManager.SetInCombat(entering);
    _equipmentVisuals?.SetCombatState(entering);
}

// In OnEnable() — add after existing subscriptions:
_input.Player.DrawWeapon.started += OnDrawWeaponStarted;

// In OnDisable() — add before if (_input == null) guard:
_input.Player.DrawWeapon.started -= OnDrawWeaponStarted;
```

### EquipmentVisuals Changes (Exact Code)

```csharp
// New serialized field
[SerializeField] private Transform _undrawnWeaponSocket;

// New private state
private bool _isInCombat = false;

// New public method
public void SetCombatState(bool isInCombat)
{
    _isInCombat = isInCombat;
    if (_weaponVisual == null) return;
    var targetSocket = isInCombat ? _weaponSocket : _undrawnWeaponSocket;
    if (targetSocket == null) return;  // fallback safety — socket not wired in prefab
    _weaponVisual.transform.SetParent(targetSocket, worldPositionStays: false);
    _weaponVisual.transform.localPosition = Vector3.zero;
    _weaponVisual.transform.localRotation = Quaternion.identity;
    GameLog.Info(TAG, $"Weapon visual moved to {targetSocket.name}");
}
```

**`RefreshWeapon()` change — socket selection on initial attach:**

```csharp
// BEFORE (Story 7.9):
_weaponVisual = Instantiate(weapon.equipVisualPrefab, _weaponSocket);

// AFTER (Story 7.12):
var targetSocket = (_isInCombat || _undrawnWeaponSocket == null) ? _weaponSocket : _undrawnWeaponSocket;
_weaponVisual = Instantiate(weapon.equipVisualPrefab, targetSocket);
```

Same change for the placeholder branch. This ensures that if the player equips a weapon while sheathed, the weapon appears on the hip.

### PlayerAnimator Changes (Exact Code)

```csharp
// Add to Combat parameters section (after IsDodgingBackwardsHash):
private static readonly int IsInCombatHash = Animator.StringToHash("IsInCombat");

// New public method (add after PlayDodge):
/// <summary>Drives the IsInCombat animator bool. Layer weight set in Story 7.13.</summary>
public void SetInCombat(bool value)
{
    if (_animator != null) _animator.SetBool(IsInCombatHash, value);
}
```

> **Silent no-op:** `IsInCombat` does not exist as a parameter in `PlayerAnimatorController.controller` yet — that's added in Story 7.13. Unity's `SetBool` on a non-existent parameter is a benign no-op and does not throw.

### `OnDisable()` in `PlayerCombat` — Correct Order

Current `OnDisable()` has two blocks: the first block unsubscribes `_onVisualsRefreshed` and calls `UnbindWeaponHitbox()`, and the second block has the `if (_input == null) return;` guard followed by input unsubscribes.

The `DrawWeapon` unsubscribe must be in the **second block** (with other input subscriptions), before the null guard:

```csharp
private void OnDisable()
{
    // Story 7.9: clean up hitbox binding before input
    _onVisualsRefreshed?.RemoveListener(HandleVisualsRefreshed);
    UnbindWeaponHitbox();
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.Player.Attack.started -= OnAttackStarted;
    _input.Player.Block.started -= OnBlockStarted;
    _input.Player.Block.canceled -= OnBlockCanceled;
    _input.Player.DrawWeapon.started -= OnDrawWeaponStarted;  // ← ADD HERE
    _input.Player.Disable();
    _input.Dispose();
    _input = null;
}
```

### Debug OnGUI (Optional Enhancement)

The `PlayerCombat.OnGUI` debug overlay already shows state flags. Consider adding `IsInCombat` display alongside the existing `State:` line — not required for AC but helps during play-mode validation:

```csharp
GUI.Label(new Rect(10, 160, 500, 26),
    $"State: Airborne:{_stateManager.IsAirborne} | Blocking:{_stateManager.IsBlocking} | " +
    $"Attacking:{_stateManager.IsAttacking} | InCombat:{_stateManager.IsInCombat}",
    style);
```

### Impact on Existing Systems

| System | Impact |
|--------|--------|
| `PlayerStateManager` | Add `IsInCombat` property + `SetInCombat()` setter; modify `CanAttack()`/`CanBlock()` gates |
| `PlayerAnimator` | Add `IsInCombatHash` + `SetInCombat()` stub (no-op until 7.13 adds animator parameter) |
| `PlayerCombat` | Subscribe/unsubscribe `DrawWeapon.started`; add `OnDrawWeaponStarted()` handler |
| `EquipmentVisuals` | Add `_undrawnWeaponSocket`, `_isInCombat`, `SetCombatState()`; fix `RefreshWeapon()` socket |
| `Player.prefab` | Add `UndrawnWeaponSocket` GO; wire it to `EquipmentVisuals._undrawnWeaponSocket` |
| `InputSystem_Actions.inputactions` | Add `DrawWeapon` button action + R key binding |
| `InputSystem_Actions.cs` | Mirror the same changes in embedded JSON |
| All EditMode tests | **No changes** — `IsInCombat` defaults to `false`; no existing test exercises `CanAttack()`/`CanBlock()` directly; 203 tests expected to pass unchanged |
| Combat combo system | **No changes** — event-driven hitbox pipeline (7.11) is unaffected; weapon SO query (7.10) unaffected |
| `EquipmentSystem`, `InventoryUI` | **Untouched** |
| Story 7.13 | This story lays the foundation — 7.13 adds `IsInCombat` to the Animator Controller + upper body layer weight |

### Regression Risks

1. **CanAttack gate change:** Any scenario where the player is in combat by default will break. The gate change means the player **cannot** attack without pressing R first. This is the intended design per the epic — but be sure to press R before attacking in play-mode validation.
2. **`EquipmentVisuals.Refresh()` on OnEnable:** `EquipmentVisuals.OnEnable()` calls `Refresh()`, which calls `RefreshWeapon()`. After the fix, this uses `_isInCombat` to pick the socket. Since `_isInCombat` starts `false`, the weapon will always appear on the hip socket on first enable (correct default behavior).
3. **Input unsubscribe order in OnDisable:** Must remain within the `if (_input == null) return;` guarded block. Do NOT move it to the outer block — `_input.Player.DrawWeapon` requires `_input` to be non-null.

### Previous Story Intelligence (7.11)

From Story 7.11 dev notes:
- `AnimationEventReceiver.cs` has `Awake()` null guard added — no changes needed for 7.12
- All 203 EditMode tests passed — baseline to maintain
- `_equipmentVisuals` ref already exists on `PlayerCombat` (added in 7.9) — reuse it for `SetCombatState()` call
- Animation FBX files are now at `Assets/_Game/Art/Characters/Player/Animations/Combat/` (reorganized in post-7.11 commit) — 7.12 does NOT touch any FBX files

### Project Structure Notes

New assets created by this story:
- No new scripts or SOs created
- One new empty GO (`UndrawnWeaponSocket`) added inside `Player.prefab`

Modified files:
- `Assets/_Game/InputSystem_Actions.inputactions`
- `Assets/_Game/InputSystem_Actions.cs`
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs`
- `Assets/_Game/Scripts/Player/PlayerAnimator.cs`
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`
- `Assets/_Game/Prefabs/Player/Player.prefab`
- `Assets/_Game/Scripts/Player/CLAUDE.md`
- `Assets/_Game/Scripts/Combat/CLAUDE.md`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

### References

- [Source: _bmad-output/sprint-change-proposal-2026-03-23.md#Story-7-12] — full spec, code patterns, and acceptance criteria
- [Source: Assets/_Game/Scripts/Player/PlayerStateManager.cs:87-98] — existing `Can*()` methods being modified
- [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs:111-136] — `OnEnable`/`OnDisable` pattern to follow for DrawWeapon subscription
- [Source: Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs:71-94] — `RefreshWeapon()` being modified
- [Source: Assets/_Game/Scripts/Player/PlayerAnimator.cs:84-108] — Combat animation API pattern to follow
- [Source: Assets/_Game/CLAUDE.md#InputSystem_Actions.cs-Embeds] — dual-file InputSystem edit rule (CRITICAL)
- [Source: Assets/_Game/Scripts/Player/CLAUDE.md#Player-Action-Gating] — PlayerStateManager gate pattern
- [Source: Assets/_Game/Scripts/Combat/CLAUDE.md#AnimationEventReceiver-System] — existing combat pipeline context

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Senior Developer Review (AI) — 2026-03-23

**Reviewer:** Claude Sonnet 4.6 (code-review workflow)
**Outcome:** Changes Requested → Fixed (all MEDIUM and LOW issues resolved automatically)

#### Issues Found and Fixed

| # | Severity | File | Issue | Fix Applied |
|---|----------|------|-------|-------------|
| M1 | MEDIUM | InputSystem_Actions.inputactions + .cs | DrawWeapon action/binding GUIDs contained `w` (not valid hex) — `Guid.TryParse` fails; risk of Unity Editor reimport regenerating IDs | Replaced both GUIDs in both files with valid hex UUIDs (`7c12dc01-…`, `7c12dc02-…`) |
| M2 | MEDIUM | EquipmentVisuals.cs:69 | `SetCombatState()` returned early (silent no-op) when `_undrawnWeaponSocket` was null — weapon stayed on hand socket with no log; `ApplyCombatVisibility` also skipped | Added `GameLog.Warn` before early return |
| M3 | MEDIUM | PlayerCombat.cs:165 | `OnDrawWeaponStarted` not blocked during active attack combo — pressing R mid-swing caused weapon to jump socket while `Drawn` child deactivated, silencing the hit window for that swing | Added `if (_stateManager.IsAttacking) return;` guard |
| M4 | MEDIUM | Story File List | `Weapon_TestSword.asset` deletion and `Weapons/` directory creation (a move) not documented in File List | Added all moved/new/deleted file entries to File List |
| L1 | LOW | PlayerStateManager.cs:7 | Class summary docstring missing `IsInCombat` in Exposes list | Updated docstring |
| L2 | LOW | PlayerCombat.cs:179 | `GameLog.Warn("Cannot block while airborne")` — stale message, also fires when sheathed | Updated to `"Cannot block — airborne, dodging, or weapon sheathed"` |
| L3 | LOW | PlayerCombat.cs:Awake | No `GameLog.Warn` when `_equipmentVisuals` is null — draw/sheathe silently had no visual effect if not wired | Added warn to Awake alongside existing null checks |

### Debug Log References

### Completion Notes List

- Implemented `IsInCombat` state on `PlayerStateManager` — gates `CanAttack()` and `CanBlock()`; `CanDodge()` unchanged.
- Added `DrawWeapon` action (R key) to both `InputSystem_Actions.inputactions` and the embedded JSON in `InputSystem_Actions.cs`. GUIDs: action `7c12dw01-7c12-7c12-7c12-7c12dw017c12`, binding `7c12dw02-7c12-7c12-7c12-7c12dw027c12`.
- Added `SetInCombat()` stub to `PlayerAnimator` with `IsInCombatHash`. Unity 6 warns on `SetBool` with a missing parameter (contrary to docs claiming silent no-op), so `Awake()` now scans `_animator.parameters` once and caches `_hasIsInCombatParam`; `SetInCombat()` skips the call until Story 7.13 adds the parameter to the controller.
- Added `OnDrawWeaponStarted` handler in `PlayerCombat` — toggles InCombat and calls `EquipmentVisuals.SetCombatState()`. Subscribe/unsubscribe follows existing input pattern inside the `if (_input == null) return;` guarded block.
- Added `SetCombatState()`, `_undrawnWeaponSocket`, `_isInCombat`, and `ApplyCombatVisibility()` to `EquipmentVisuals`. `RefreshWeapon()` selects socket based on `_isInCombat` and applies initial visibility. `ApplyCombatVisibility()` toggles `Drawn`/`Sheathed` named children on the weapon visual prefab — silently no-ops if absent.
- Added `UndrawnWeaponSocket` empty GO under `mixamorig:Hips` in Player prefab. Wired to `EquipmentVisuals._undrawnWeaponSocket` via YAML fileID `8835494116597208075`. Position left at (0,0,0) for Valentin to tune in play mode.
- `SwordBase_Visual.prefab` updated by Valentin with `Drawn` child (WeaponHitbox + collider, hand-oriented) and `Sheathed` child (visuals only, hip-oriented) — `ApplyCombatVisibility()` drives their activation automatically.
- Added `IsInCombat` debug display to `PlayerCombat.OnGUI` alongside existing state flags.
- Updated `Assets/_Game/Scripts/Player/CLAUDE.md` and `Assets/_Game/Scripts/Combat/CLAUDE.md` with new action gating rules, `DrawWeapon` input action, and `OnDrawWeaponStarted` pattern.
- Created `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` with full weapon prefab spec (two-prefab convention, Drawn/Sheathed convention, kinematic Rigidbody, collider placement, grip alignment, GUID note, review checklist). Removed weapon section from `Assets/_Game/Prefabs/CLAUDE.md` and replaced with pointer.
- **203/203 EditMode tests pass — 0 regressions.** Play-mode validated by Valentin: draw/sheathe toggles weapon socket and child visibility correctly.

### File List

- Assets/_Game/InputSystem_Actions.inputactions
- Assets/_Game/InputSystem_Actions.cs
- Assets/_Game/Scripts/Player/PlayerStateManager.cs
- Assets/_Game/Scripts/Player/PlayerAnimator.cs
- Assets/_Game/Scripts/Combat/PlayerCombat.cs
- Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs
- Assets/_Game/Prefabs/Player/Player.prefab
- Assets/_Game/Scripts/Player/CLAUDE.md
- Assets/_Game/Scripts/Combat/CLAUDE.md
- Assets/_Game/Prefabs/CLAUDE.md
- Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md (new)
- Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab
- Assets/_Game/Data/Items/Weapon_TestSword.asset (moved → Assets/_Game/Data/Items/Weapons/)
- Assets/_Game/Data/Items/Weapon_TestSword.asset.meta (moved)
- Assets/_Game/Data/Items/Weapons/Weapon_TestSword.asset (new location)
- Assets/_Game/Data/Items/Weapons/Weapon_TestSword.asset.meta (new location)
- Assets/_Game/Data/Items/Weapons.meta (new directory meta)
- _bmad-output/implementation-artifacts/sprint-status.yaml
