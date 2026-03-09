# Story 2.6: Player State Manager

Status: done

## Story

As a developer,
I want a PlayerStateManager that centralizes player state (airborne, blocking, attacking, dodging),
so that combat actions are correctly gated — including preventing all combat while jumping or falling.

## Acceptance Criteria

1. A new `PlayerStateManager` class exists at `Assets/_Game/Scripts/Combat/PlayerStateManager.cs` with:
   - `namespace Game.Combat`
   - `private const string TAG = "[Combat]"`
   - Private cached field: `private CharacterController _characterController`
   - `Awake()`: GetComponent<CharacterController>(), null guard (log error + `enabled = false; return;` if missing)
   - **Read-only state properties:**
     - `public bool IsAirborne => _characterController != null && !_characterController.isGrounded;`
     - `public bool IsBlocking { get; private set; }` (defaults false)
     - `public bool IsAttacking { get; private set; }` (defaults false)
     - `public bool IsDodging { get; private set; }` (defaults false — reserved for Story 2.7)
   - **Additional cached fields** (for animator side-effects):
     - `private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking")`
     - `private Animator _animator` — cached in `Awake()` with null guard
   - **State setter methods** (called by `PlayerCombat`):
     - `public void SetBlocking(bool value)` → sets `IsBlocking = value` and calls `_animator.SetBool(IsBlockingHash, value)`
     - `public void SetAttacking(bool value, int triggerHash = 0)` → sets `IsAttacking = value`; if `value == true && triggerHash != 0`, also calls `_animator.SetTrigger(triggerHash)`
     - `public void SetDodging(bool value)` → `IsDodging = value;` (reserved stub for Story 2.7)
   - No Update(), no event subscriptions beyond state + animator coupling.

2. `PlayerCombat` is updated to use `PlayerStateManager`:
   - Private field `_isBlocking` is **removed**; replaced by reads/writes through `_stateManager`.
   - `private PlayerStateManager _stateManager;` added to `PlayerCombat` fields.
   - `private Animator _animator` field and its `GetComponent` null guard **removed** from `PlayerCombat` — animator is now driven exclusively through `PlayerStateManager` setters.
   - `IsBlockingHash` constant **removed** from `PlayerCombat` — moved to `PlayerStateManager`.
   - In `Awake()`: `_stateManager = GetComponent<PlayerStateManager>();` with null guard (`GameLog.Error` + disable) after the `_staminaSystem` null guard.
   - All `_isBlocking = true` → `_stateManager.SetBlocking(true)`
   - All `_isBlocking = false` → `_stateManager.SetBlocking(false)`
   - All reads of `_isBlocking` → `_stateManager.IsBlocking`

3. `PlayerCombat.TryAttack()` gains an airborne gate (FIRST guard, before the blocking guard):
   ```csharp
   if (_stateManager.IsAirborne)
   {
       GameLog.Warn(TAG, "Cannot attack while airborne");
       return;
   }
   ```

4. `PlayerCombat.OnBlockStarted()` gains an airborne gate (FIRST guard, before the stamina check):
   ```csharp
   if (_stateManager.IsAirborne)
   {
       GameLog.Warn(TAG, "Cannot block while airborne");
       return;
   }
   ```

5. `PlayerCombat` tracks attacking state through `_stateManager`:
   - In `TryAttack()`, replace `_animator.SetTrigger(triggerHash)` + `_stateManager.SetAttacking(true)` with a single call: `_stateManager.SetAttacking(true, triggerHash)` — fires the animator trigger and sets state atomically
   - In `TryAttack()`, stamina-fail reset path (before returning): call `_stateManager.SetAttacking(false)`
   - In `TryAttack()`, finisher-fired path (when `_comboStep >= 2` resets): call `_stateManager.SetAttacking(false)`
   - In `Update()`, when combo window expires (`_comboWindowTimer <= 0f` branch): call `_stateManager.SetAttacking(false)`
   - In `OnBlockStarted()`, after combo reset: call `_stateManager.SetAttacking(false)`
   - **Invariant:** `IsAttacking` is `true` throughout the full combo chain (including delay and window phases between hits); `false` only when the chain fully terminates.

6. `PlayerCombat.OnGUI()` debug overlay gets a new fourth line at `Rect(10, 160, 500, 26)`, `fontSize = 18`:
   ```csharp
   GUI.Label(new Rect(10, 160, 500, 26),
       $"State: Airborne:{_stateManager.IsAirborne} | Blocking:{_stateManager.IsBlocking} | Attacking:{_stateManager.IsAttacking}",
       style);
   ```

7. The `Player.prefab` at `Assets/_Game/Prefabs/Player/Player.prefab` has a `PlayerStateManager` component added to its root GameObject (same root as `PlayerCombat`, `StaminaSystem`, `Animator`, `CharacterController`). This is required before entering Play Mode.

8. An Edit Mode test class `PlayerStateManagerTests` exists at `Assets/Tests/EditMode/PlayerStateManagerTests.cs` with ≥ 5 tests covering the gating logic as pure formulas:
   - Cannot attack while airborne
   - Cannot block while airborne
   - Can attack when grounded and not blocking
   - Can block when grounded and has stamina
   - Cannot attack while blocking (existing behavior, now tested through state manager lens)

9. No compile errors. All existing Edit Mode tests (26+) continue to pass. In Play Mode: jumping prevents attack/block input from firing (debug log shows "Cannot attack while airborne" / "Cannot block while airborne"); landing restores normal combat. All Stories 1.1–2.5 behaviors unchanged (WASD, camera, stamina regen, combo attacks, manual blocking, perfect block, TryReceiveHit).

## Tasks / Subtasks

- [x] Task 1: Create PlayerStateManager (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Scripts/Combat/PlayerStateManager.cs` with namespace, TAG, CharacterController field
  - [x] 1.2 Implement `Awake()` with GetComponent + null guard
  - [x] 1.3 Add `IsAirborne` computed property from `CharacterController.isGrounded`
  - [x] 1.4 Add `IsBlocking`, `IsAttacking`, `IsDodging` auto-properties with private setters
  - [x] 1.5 Add `SetBlocking()`, `SetAttacking()`, `SetDodging()` public methods
  - [x] 1.6 Create `Assets/_Game/Scripts/Combat/PlayerStateManager.cs.meta` (Unity auto-generates)

- [x] Task 2: Update PlayerCombat.cs (AC: 2, 3, 4, 5, 6)
  - [x] 2.1 Remove `_isBlocking` field; add `_stateManager` field
  - [x] 2.2 Add `_stateManager = GetComponent<PlayerStateManager>()` with null guard in `Awake()`
  - [x] 2.3 Replace all `_isBlocking = true/false` with `_stateManager.SetBlocking(true/false)`
  - [x] 2.4 Replace all `_isBlocking` reads with `_stateManager.IsBlocking`
  - [x] 2.5 Add airborne gate at top of `TryAttack()` (before blocking gate)
  - [x] 2.6 Add airborne gate at top of `OnBlockStarted()` (before stamina check)
  - [x] 2.7 Add `_stateManager.SetAttacking(true)` after `_animator.SetTrigger()` in `TryAttack()`
  - [x] 2.8 Add `_stateManager.SetAttacking(false)` to all 4 combo-reset paths
  - [x] 2.9 Update `OnGUI()` to add State line at y=160

- [x] Task 3: Update Player prefab (AC: 7)
  - [x] 3.1 Add `PlayerStateManager` component to Player.prefab root GameObject
  - [x] 3.2 Verify `CharacterController` is on the same root (it is — established Story 1.1)

- [x] Task 4: Edit Mode tests (AC: 8)
  - [x] 4.1 Create `Assets/Tests/EditMode/PlayerStateManagerTests.cs` with ≥ 5 tests
  - [x] 4.2 Run all tests via Unity Test Runner — all green (including existing 26 regression tests)

- [ ] Task 5: Play Mode validation (AC: 9) — requires Unity Editor (manual)
  - [ ] 5.1 Enter Play Mode — no console errors
  - [ ] 5.2 Press Space to jump, press LMB mid-air → `"Cannot attack while airborne"` logged; attack does NOT fire
  - [ ] 5.3 Press Space to jump, hold RMB mid-air → `"Cannot block while airborne"` logged; block does NOT raise
  - [ ] 5.4 Land, press LMB → combo fires normally; hold RMB → block raises normally
  - [ ] 5.5 Debug overlay shows 4th line: `"State: Airborne:False | Blocking:False | Attacking:False"`
  - [ ] 5.6 Mid-combo, debug shows `Attacking:True`; after window expires, shows `Attacking:False`
  - [ ] 5.7 Verify WASD, camera, jump, stamina regen, 3-hit combo, block, perfect block all unchanged

## Dev Notes

Story 2.6 is a **developer story** — no player-visible features. The deliverable is an architectural refactor that: (1) creates a centralized `PlayerStateManager` that exposes read-only state to other systems, and (2) gates all combat actions against `IsAirborne` so jumping/falling correctly prevents attacking and blocking.

### Critical Design Decisions

#### PlayerStateManager Location: `Scripts/Combat/`

`PlayerStateManager` lives alongside `PlayerCombat` and `StaminaSystem` in `Scripts/Combat/`. This is intentional because:
- Its primary purpose is gating **combat** actions
- `PlayerCombat` accesses it via `GetComponent<PlayerStateManager>()` (both on same Player prefab root) — this is same-system direct reference, acceptable within `Scripts/Combat/`
- It is NOT a singleton — it lives on the Player prefab, not in `Core.unity`

Placing it in `Scripts/Player/` would make `PlayerCombat → PlayerStateManager` a cross-system reference requiring event channels — over-engineering for a player-specific state aggregator.

#### `_isBlocking` Removal from `PlayerCombat`

The existing `_isBlocking` private field in `PlayerCombat` is **fully removed** and replaced by `_stateManager.IsBlocking`. All existing read/write sites are mechanical replacements:

| Was | Becomes |
|-----|---------|
| `_isBlocking = true` | `_stateManager.SetBlocking(true)` |
| `_isBlocking = false` | `_stateManager.SetBlocking(false)` |
| `if (_isBlocking)` | `if (_stateManager.IsBlocking)` |
| `if (!_isBlocking) return;` | `if (!_stateManager.IsBlocking) return;` |
| `_isBlocking ? "RAISED" : "lowered"` | `_stateManager.IsBlocking ? "RAISED" : "lowered"` |

The `_isPerfectBlockWindowOpen` and `_perfectBlockWindowTimer` fields **are NOT moved** — they remain in `PlayerCombat` as internal PB timing state (they're combat-mechanic state, not player-movement state).

#### IsAttacking Semantics

`IsAttacking` is `true` from the moment an attack trigger fires until the combo chain fully terminates:
- **Enters true:** `_animator.SetTrigger(triggerHash)` in `TryAttack()`
- **Returns to false:**
  1. Stamina fail in `TryAttack()` (chain aborted before trigger)
  2. Finisher (step 3) fires — chain complete
  3. Combo window timer expires in `Update()` — player stopped chaining
  4. `OnBlockStarted()` resets the combo — combat mode switched

This means `IsAttacking` remains `true` during the delay phase and window-open phase between combo hits (correct — player is mid-combo). Story 2.7 will use `IsAttacking` to prevent dodge roll during active combo.

#### IsDodging: Stub Only

`IsDodging` and `SetDodging()` are stubs with no callers this story. They exist so Story 2.7 (Dodge Roll) can wire in without modifying `PlayerStateManager`'s API. No tests required for the stub.

#### Airborne Gate Ordering

The guard order in `TryAttack()` must be:
1. **Airborne** check (new — highest priority; airborne cancels everything)
2. **Combo delay** check (existing — prevents duplicate combo input)
3. **Blocking** check (existing — cannot attack while blocking)
4. **Stamina** check (existing)

The guard order in `OnBlockStarted()` must be:
1. **Airborne** check (new)
2. **Stamina** check (existing)

### `PlayerCombat.cs` — Complete Updated State After This Story

After the refactor, the relevant fields and method bodies look like:

```csharp
// Fields — REMOVED: _isBlocking. ADDED: _stateManager
private PlayerStateManager _stateManager;
private bool _isPerfectBlockWindowOpen = false;  // still owned by PlayerCombat
private float _perfectBlockWindowTimer = 0f;     // still owned by PlayerCombat

// Awake() — new addition after _animator null guard
_stateManager = GetComponent<PlayerStateManager>();
if (_stateManager == null)
{
    GameLog.Error(TAG, "PlayerStateManager not found on Player — PlayerCombat disabled");
    enabled = false;
    return;
}

// TryAttack() — new FIRST guard
if (_stateManager.IsAirborne)
{
    GameLog.Warn(TAG, "Cannot attack while airborne");
    return;
}
// SECOND guard (unchanged from Story 2.5 except was inline _isBlocking):
if (_stateManager.IsBlocking)  // was: if (_isBlocking)
{
    GameLog.Warn(TAG, "Cannot attack while blocking");
    return;
}

// After _animator.SetTrigger(triggerHash):
_stateManager.SetAttacking(true);

// In stamina-fail path (before return):
_stateManager.SetAttacking(false);

// In finisher path (after combo resets):
_stateManager.SetAttacking(false);

// Update() — combo window expired branch:
_stateManager.SetAttacking(false);

// OnBlockStarted() — new FIRST guard:
if (_stateManager.IsAirborne)
{
    GameLog.Warn(TAG, "Cannot block while airborne");
    return;
}

// OnBlockStarted() — after _animator.SetBool:
_stateManager.SetBlocking(true);  // was: _isBlocking = true

// OnBlockCanceled() — existing guard now uses stateManager:
if (!_stateManager.IsBlocking) return;  // was: if (!_isBlocking)
_stateManager.SetBlocking(false);       // was: _isBlocking = false
_stateManager.SetAttacking(false);      // combo was reset in OnBlockStarted; now ensure attacking cleared too

// TryReceiveHit() — block-broken path:
_stateManager.SetBlocking(false);      // was: _isBlocking = false
```

### OnGUI Debug Display Stack (After This Story)

```
[y=50]   Stamina: 80 / 100                                        ← StaminaSystem
[y=70]   Combat: [Ready]                                          ← PlayerCombat stamina gate
[y=100]  Combo: step 0 | closed                                   ← PlayerCombat combo state
[y=130]  Block: RAISED | PB: closed                               ← PlayerCombat block + PB window
[y=160]  State: Airborne:False | Blocking:True | Attacking:False  ← PlayerStateManager (NEW)
```

### Edit Mode Test Pattern

Tests follow the established pattern — pure formula simulation, no MonoBehaviour lifecycle:

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for PlayerStateManager gating logic.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests and PerfectBlockTests.
/// Requires Tests.EditMode.asmdef to reference "Game" assembly (established Story 1.5).
/// </summary>
public class PlayerStateManagerTests
{
    private bool CanAttack(bool isAirborne, bool isBlocking) =>
        !isAirborne && !isBlocking;

    private bool CanBlock(bool isAirborne, float stamina, float blockCost) =>
        !isAirborne && stamina >= blockCost;

    [Test]
    public void CanAttack_ReturnsFalse_WhenAirborne()
    {
        Assert.That(CanAttack(true, false), Is.False);
    }

    [Test]
    public void CanBlock_ReturnsFalse_WhenAirborne()
    {
        Assert.That(CanBlock(true, 100f, 15f), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsTrue_WhenGroundedAndNotBlocking()
    {
        Assert.That(CanAttack(false, false), Is.True);
    }

    [Test]
    public void CanBlock_ReturnsTrue_WhenGroundedAndHasStamina()
    {
        Assert.That(CanBlock(false, 15f, 15f), Is.True);
        Assert.That(CanBlock(false, 100f, 15f), Is.True);
    }

    [Test]
    public void CanAttack_ReturnsFalse_WhenBlocking()
    {
        Assert.That(CanAttack(false, true), Is.False);
        // Airborne + blocking: still false
        Assert.That(CanAttack(true, true), Is.False);
    }
}
```

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `Scripts/Combat/PlayerStateManager.cs` |
| No magic numbers | ✅ No new config values; all reads are booleans from engine state |
| GameLog only | ✅ All new logging via `GameLog.Warn(TAG, ...)` with `[Combat]` TAG |
| Null-guard in Awake | ✅ CharacterController null guard in `PlayerStateManager.Awake()`; `_stateManager` null guard added to `PlayerCombat.Awake()` |
| No string allocations in hot path | ✅ `IsAirborne` computes from bool; no per-frame heap allocation |
| No cross-system direct refs | ✅ `PlayerCombat` → `PlayerStateManager` is same-system (both `Scripts/Combat/`); `PlayerStateManager` → `CharacterController` is engine-component access, always acceptable |
| Event subscription in OnEnable/OnDisable | ✅ No new event subscriptions — state manager is passive |
| OnDisable null guard | ✅ Existing `if (_input == null) return;` guard unchanged; `_stateManager` not used in OnDisable |
| WriteDefaultValues: false | N/A — no new animator states |
| No SendMessage | ✅ Direct property reads and method calls |
| Debug tools guarded | ✅ New `OnGUI` line inside existing `#if DEVELOPMENT_BUILD || UNITY_EDITOR` block |
| Not a singleton | ✅ `PlayerStateManager` lives on Player prefab, accessed via `GetComponent` |
| `Tests.EditMode.asmdef` references `"Game"` | ✅ Already established Story 1.5; no changes needed |

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Combat/PlayerStateManager.cs        ← NEW centralized state aggregator
Assets/_Game/Scripts/Combat/PlayerStateManager.cs.meta  ← auto-generated by Unity
Assets/Tests/EditMode/PlayerStateManagerTests.cs         ← NEW Edit Mode tests (≥5 tests)
Assets/Tests/EditMode/PlayerStateManagerTests.cs.meta    ← auto-generated by Unity
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/Combat/PlayerCombat.cs              ← Remove _isBlocking, add _stateManager, airborne gates, attacking state tracking, OnGUI line
Assets/_Game/Prefabs/Player/Player.prefab                ← Add PlayerStateManager component to root
```

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs         ← Story 2.1 (unchanged)
├── PlayerCombat.cs          ← Story 2.5 + refactored (PlayerStateManager integration, airborne gates)
└── PlayerStateManager.cs    ← NEW (airborne + blocking + attacking + dodging state aggregator)
```

**Player prefab root component list after this story:**
```
Player (root)
├── CharacterController      (established Story 1.1)
├── Animator                 (established Story 1.4)
├── PlayerController         (established Story 1.1)
├── PlayerAnimator           (established Story 1.4)
├── StaminaSystem            (established Story 2.1)
├── PlayerCombat             (established Story 2.2) ← modified this story
└── PlayerStateManager       ← NEW this story
```

**What Subsequent Stories Build On This Foundation:**

| Story | What it adds |
|-------|-------------|
| 2.7 Dodge Roll | New `Dodge` input action; calls `_stateManager.SetDodging(true/false)`; gates on `!_stateManager.IsAirborne && !_stateManager.IsAttacking` |
| 2.8 Enemy AI | `EnemyBrain` references `PlayerCombat.TryReceiveHit()` — `_stateManager.IsBlocking` is read inside that method via `_stateManager` |
| 2.9 Health System | `PlayerHealth.TakeDamage()` may query `_stateManager.IsDodging` for i-frame immunity |
| Epic 3 | `PlayerStats` progression system may need to read `IsAirborne` for stat-driven effects |

### Precise Prefab Edit: Player.prefab

The Player prefab YAML at `Assets/_Game/Prefabs/Player/Player.prefab` needs a new MonoBehaviour component added to the root GameObject. After adding via Unity Editor or MCP:
- Component: `PlayerStateManager`
- Script GUID: will be auto-assigned by Unity when the `.cs` file is created
- No serialized fields exposed (no Inspector configuration needed)

If adding via Unity MCP tool:
```
manage_gameobject(action="add_component", target="Player", component_name="PlayerStateManager")
```
Then `refresh_unity()`.

### References

- Epic 2 story 6 user story: [Source: _bmad-output/epics.md#Epic 2: Combat System]
- Architecture — Scripts/Combat folder location: [Source: _bmad-output/game-architecture.md#Directory Structure]
- Architecture — same-system direct references rule: [Source: _bmad-output/game-architecture.md#Standard Patterns — Component Communication]
- Singletons: only WorldStateManager and SaveSystem: [Source: _bmad-output/project-context.md#Architecture Patterns]
- No magic numbers rule: [Source: _bmad-output/project-context.md#Configuration Management]
- GameLog mandatory: [Source: _bmad-output/project-context.md#Logging]
- `OnDisable` null guard pattern: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- `Tests.EditMode.asmdef` references `"Game"`: [Source: CLAUDE.md#Assembly Setup]
- `CharacterController.isGrounded` is the authoritative grounded check: [Source: Assets/_Game/Scripts/Player/PlayerController.cs]
- Story 2.5 TryReceiveHit API (uses _isBlocking → now _stateManager.IsBlocking): [Source: _bmad-output/implementation-artifacts/2-5-perfect-block.md]
- `_isBlocking` field and all its sites in PlayerCombat: [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs]
- GROUNDED_VELOCITY constant causing CharacterController.velocity.y != 0 when grounded: [Source: CLAUDE.md#CharacterController.velocity Includes Y Component] — use `isGrounded` not velocity.y for airborne check
- `CombatConfigSO.dodgeStaminaCost = 25f` already exists (for Story 2.7): [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- Player prefab structure: [Source: CLAUDE.md#Player Prefab Structure]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- No significant debug issues encountered. Unity auto-generated PlayerStateManager.cs.meta on refresh.
- Unity auto-applied [RequireComponent(typeof(PlayerStateManager))] on first compile, but the prefab YAML still required manual component block insertion.

### Completion Notes List

- Created `PlayerStateManager.cs`: state aggregator with IsAirborne (engine-derived from CharacterController.isGrounded), IsBlocking, IsAttacking (PlayerCombat-managed), IsDodging (stub). Setters drive animator side-effects: `SetBlocking` calls `SetBool(IsBlockingHash)`, `SetAttacking(bool, int triggerHash=0)` optionally calls `SetTrigger(triggerHash)`.
- Refactored `PlayerCombat.cs`: removed `_isBlocking`, `_animator`, and `IsBlockingHash` — all animator calls now go through `PlayerStateManager` setters. Added airborne gates in `TryAttack()` and `OnBlockStarted()`. Attack trigger + attacking state set atomically via `SetAttacking(true, triggerHash)`. `SetAttacking(false)` in 3 combo-reset paths (stamina fail, finisher, window expiry) plus `OnBlockStarted` combo reset. Removed redundant `SetAttacking(false)` from `OnBlockCanceled` — `IsAttacking` is already false when a block is active. Added `[RequireComponent(typeof(PlayerStateManager))]`.
- Added 4th debug overlay line at y=160 showing live Airborne/Blocking/Attacking state.
- Player.prefab: manually added PlayerStateManager MonoBehaviour component block to root (fileID 7419823056781234567, GUID d260a03781c19414d8aed623aad8fe6c).
- 31/31 Edit Mode tests pass (5 new PlayerStateManagerTests + 26 regression). Zero compile errors.

### File List

- `Assets/_Game/Scripts/Combat/PlayerStateManager.cs` (NEW)
- `Assets/_Game/Scripts/Combat/PlayerStateManager.cs.meta` (auto-generated by Unity)
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` (MODIFIED)
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs` (MODIFIED — OnGUI fontSize=18 to match PlayerCombat style)
- `Assets/_Game/Prefabs/Player/Player.prefab` (MODIFIED — PlayerStateManager component added)
- `Assets/Tests/EditMode/PlayerStateManagerTests.cs` (NEW)
- `Assets/Tests/EditMode/PlayerStateManagerTests.cs.meta` (auto-generated by Unity)

_Note: `InputSystem_Actions.cs`, `InputSystem_Actions.inputactions`, `PlayerAnimatorController.controller`, and `CombatConfigSO.cs` appear in the working tree but belong to Stories 2.4/2.5 (uncommitted since those sessions)._

## Change Log

- 2026-03-09: Story 2.6 implemented — created PlayerStateManager, refactored PlayerCombat (_isBlocking → _stateManager), added airborne gates, attacking state tracking, debug overlay 4th line, Player prefab component, 5 new Edit Mode tests (31 total passing)
- 2026-03-09: Post-story refactor — animator side-effects moved into PlayerStateManager setters (SetBlocking drives SetBool, SetAttacking optionally fires SetTrigger); _animator field and IsBlockingHash removed from PlayerCombat; redundant SetAttacking(false) removed from OnBlockCanceled
- 2026-03-10: Code review fixes — (H1) null-guard _animator in SetBlocking/SetAttacking; (H1/L1) added [RequireComponent(typeof(CharacterController))] to PlayerStateManager; (M2) SetAttacking(false) added to Consume() fail-path in TryAttack(); (L3) OnEnable guard extended to include _stateManager; (M3) added 4 IsAttacking/CanDodge tests (35 total); (M1) File List updated with StaminaSystem.cs

## Senior Developer Review (AI) — 2026-03-10

**Outcome:** Issues Found → Fixed (7 issues: 1 High, 3 Medium, 3 Low)

**Fixed:**
- [H1] NRE in PlayerStateManager setters when _animator null — added `if (_animator != null)` guards in SetBlocking/SetAttacking
- [L1] Added `[RequireComponent(typeof(CharacterController))]` to PlayerStateManager — prevents configuration errors at prefab authoring time
- [M2] SetAttacking(false) missing from Consume() fail-path in TryAttack() — fixed; prevents IsAttacking stuck true if StaminaSystem inconsistency occurs
- [L3] OnEnable() guard in PlayerCombat extended to include `_stateManager == null` check
- [M3] Added 4 new tests: CanDodge formula (Story 2.7 prerequisite), IsBlocking/IsAttacking mutual-exclusion invariant (35 total)
- [M1] StaminaSystem.cs (OnGUI fontSize fix) added to File List; 2.4/2.5 carry-over files noted

**Not fixed (LOW/deferred):**
- [L2] Per-frame `new GUIStyle()` in dev OnGUI() — dev-only, acceptable; defer to a cleanup pass
