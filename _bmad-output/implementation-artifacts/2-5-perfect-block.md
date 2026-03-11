# Story 2.5: Perfect Block

Status: done

## Story

As a player,
I want to perform a perfect block in a tight timing window after raising my block,
so that I can negate the stamina cost and stagger the attacker when I time my block precisely.

## Acceptance Criteria

1. `CombatConfigSO` has a new `[Header("Perfect Block")]` section with one field: `public float perfectBlockWindowDuration = 0.25f;` with a tooltip explaining its purpose. No other changes to `CombatConfigSO`.

2. A `HitResult` enum with values `PerfectBlock`, `Blocked`, `NotBlocked` is defined in `Assets/_Game/Scripts/Combat/PlayerCombat.cs` at `namespace Game.Combat` scope — **outside** the `PlayerCombat` class body, before it.

3. `PlayerCombat` has two new private fields: `_isPerfectBlockWindowOpen` (bool, default false) and `_perfectBlockWindowTimer` (float, default 0f).

4. `OnBlockStarted()`: after `_isBlocking = true` and `_animator.SetBool(IsBlockingHash, true)`, set `_isPerfectBlockWindowOpen = true` and `_perfectBlockWindowTimer = _config.perfectBlockWindowDuration`. Log: `GameLog.Info(TAG, $"Perfect block window opened ({_config.perfectBlockWindowDuration:F2}s)")`.

5. `OnBlockCanceled()`: the existing `if (!_isBlocking) return;` guard stays. After setting `_isBlocking = false` and calling `SetBool(IsBlockingHash, false)`, also reset `_isPerfectBlockWindowOpen = false` and `_perfectBlockWindowTimer = 0f`.

6. `Update()` gets a new countdown section (after the existing combo countdown). If `_isPerfectBlockWindowOpen` is true and `_perfectBlockWindowTimer > 0f`, decrement by `Time.deltaTime`. When timer reaches ≤ 0f, set `_isPerfectBlockWindowOpen = false` and log `GameLog.Info(TAG, "Perfect block window closed — regular block mode")`.

7. A new public method `TryReceiveHit(GameObject attacker)` returning `HitResult`:
   - If `!_isBlocking` → log `GameLog.Info(TAG, "Hit received — not blocking")`, return `HitResult.NotBlocked`
   - If `_isPerfectBlockWindowOpen` → set `_isPerfectBlockWindowOpen = false`, `_perfectBlockWindowTimer = 0f`, log `GameLog.Info(TAG, "PERFECT BLOCK — no stamina cost, attacker staggers")`, return `HitResult.PerfectBlock`
   - Otherwise (regular block, window closed) → call `_staminaSystem.Consume(_config.blockStaminaCostPerHit)`:
     - If consumed → log `GameLog.Info(TAG, "Block absorbed hit — stamina consumed")`, return `HitResult.Blocked`
     - If not consumed (stamina depleted) → set `_isBlocking = false`, call `_animator.SetBool(IsBlockingHash, false)`, reset `_isPerfectBlockWindowOpen = false`, log `GameLog.Warn(TAG, "Block broken by hit — stamina depleted")`, return `HitResult.NotBlocked`

8. Debug overlay `OnGUI()`: the Block line at `Rect(10, 130, 400, 26)` is updated to:
   ```csharp
   string pbWindow = _isPerfectBlockWindowOpen ? $"PB: {_perfectBlockWindowTimer:F2}s" : "PB: closed";
   GUI.Label(new Rect(10, 130, 400, 26), $"Block: {(_isBlocking ? "RAISED" : "lowered")} | {pbWindow}", style);
   ```

9. An Edit Mode test class `PerfectBlockTests` exists at `Assets/Tests/EditMode/PerfectBlockTests.cs` with ≥ 5 tests covering:
   - Perfect block returns `PerfectBlock` when blocking and window open (even at zero stamina)
   - Regular block returns `Blocked` when blocking, window closed, and sufficient stamina
   - Block broken returns `NotBlocked` when blocking, window closed, and insufficient stamina
   - Not blocking returns `NotBlocked`
   - Perfect block costs no stamina (window-open hit ignores stamina amount)

10. No compile errors. All existing Edit Mode tests continue to pass. Stories 1.1–2.4 behavior unchanged (WASD, camera, jump, stamina regen, combo attacks, manual blocking all unaffected).

## Tasks / Subtasks

- [x] Task 1: Update CombatConfigSO (AC: 1)
  - [x] 1.1 Add `[Header("Perfect Block")]` after the Combo Attack header block
  - [x] 1.2 Add `[Tooltip("Seconds after raising block during which a hit counts as a perfect block.")] public float perfectBlockWindowDuration = 0.25f;`

- [x] Task 2: Update PlayerCombat.cs (AC: 2, 3, 4, 5, 6, 7, 8)
  - [x] 2.1 Add `HitResult` enum at `namespace Game.Combat` scope, before the `PlayerCombat` class
  - [x] 2.2 Add `_isPerfectBlockWindowOpen` and `_perfectBlockWindowTimer` private fields (alongside `_isBlocking`)
  - [x] 2.3 Update `OnBlockStarted()` to open the perfect block window after raising block
  - [x] 2.4 Update `OnBlockCanceled()` to reset PB window after the existing null guard
  - [x] 2.5 Add PB window countdown section in `Update()` after existing combo countdown
  - [x] 2.6 Add `public HitResult TryReceiveHit(GameObject attacker)` method
  - [x] 2.7 Update `OnGUI()` Block line to show PB window countdown

- [x] Task 3: Edit Mode tests (AC: 9)
  - [x] 3.1 Create `Assets/Tests/EditMode/PerfectBlockTests.cs` with ≥ 5 tests
  - [x] 3.2 Run all tests via Unity Test Runner — all pass (including existing 21+ regression tests)

- [x] Task 4: Play Mode validation (AC: 10) — requires Unity Editor (manual)
  - [ ] 4.1 Enter Play Mode — no console errors
  - [ ] 4.2 Hold RMB immediately — debug overlay shows `"Block: RAISED | PB: 0.25s"` (counting down)
  - [ ] 4.3 Wait for PB window to expire — debug shows `"Block: RAISED | PB: closed"` (still blocking)
  - [ ] 4.4 Release RMB — debug shows `"Block: lowered | PB: closed"`
  - [ ] 4.5 Verify WASD, camera, jump, stamina regen, combo attacks, and regular blocking unchanged

## Dev Notes

Story 2.5 implements the **perfect block timing window** — the foundation for the Gothic-style parry mechanic. Since enemies do not exist until Story 2.7, this story establishes the full `TryReceiveHit()` API contract that enemy code will consume. The stagger effect on the attacker cannot be tested in play mode until Story 2.7 adds enemy AI.

### Key Design Decision: Window-From-Press Timing Model

The perfect block window opens the moment the player **raises the block** (RMB pressed), not at the moment of impact. This matches Gothic-style parry feel:
- Player presses RMB → `OnBlockStarted()` fires → 0.25s perfect block window opens
- If an enemy hit registers within 0.25s → perfect block (no stamina cost, enemy staggers)
- If the enemy hit registers after 0.25s → regular block (stamina consumed)

This requires enemy attack animations to have a small gap between the press-of-attack and the actual hit-frame, giving the player a skill-expressive timing window.

**Tuning note:** `perfectBlockWindowDuration = 0.25f` (250ms) is the starting value. The GDD says "tight enough to reward skill without being frame-perfect" — the 200–300ms range is typical. Tune in `CombatConfig.asset` in the Inspector without code changes.

### `TryReceiveHit()` API Contract for Story 2.7

This method is the interface between `PlayerCombat` and future `EnemyBrain` (Story 2.7). The enemy attack code will look like:

```csharp
// In EnemyBrain / CombatBehaviour (Story 2.7):
var playerCombat = _player.GetComponent<PlayerCombat>();
HitResult result = playerCombat.TryReceiveHit(this.gameObject);
switch (result)
{
    case HitResult.PerfectBlock:
        // Enemy enters Stagger state — implement in EnemyBrain
        // No damage applied to player
        break;
    case HitResult.Blocked:
        // No damage to player — stamina consumed by TryReceiveHit
        break;
    case HitResult.NotBlocked:
        // Apply full attack damage to PlayerHealth (Story 2.8)
        break;
}
```

Both `PlayerCombat` and `EnemyBrain` are in `namespace Game.Combat`, so direct `GetComponent` is acceptable (same-system communication rule from architecture).

### Block Broken on Hit

When stamina is depleted mid-block and a hit lands:
- `TryReceiveHit()` drops the block internally (`_isBlocking = false`, animator bool cleared)
- Returns `NotBlocked` so the enemy applies full damage
- This is distinct from `OnBlockCanceled` — the block was broken by an incoming hit, not released by the player
- The player's RMB is still held at this point — they must release and re-press to attempt a new block (which requires stamina to have regenerated)

### No New Animation States

This story does NOT add new states to `PlayerAnimatorController.controller`. The `Block_State` added in Story 2.4 continues as the blocking placeholder. Perfect block visual/audio feedback (flash effect, hit-stop, "clang" sound) is deferred to Epic 8 when real assets are available.

### No New Input Actions

No changes to `InputSystem_Actions.inputactions` or `InputSystem_Actions.cs`. Perfect block is detected from game state (`_isBlocking` + `_isPerfectBlockWindowOpen`), not from a new input event.

### What Subsequent Stories Build On This Foundation

| Story | What it adds |
|-------|-------------|
| 2.6 Dodge Roll | New `Dodge` action in `PlayerCombat`; no interaction with perfect block window |
| 2.7 Enemy AI | `EnemyBrain.CombatBehaviour` calls `playerCombat.TryReceiveHit(this.gameObject)`; implements `Stagger` state in `EnemyBrain` when result is `PerfectBlock` |
| 2.8 Health System | `TryReceiveHit` returning `NotBlocked` triggers `PlayerHealth.TakeDamage(enemyDamage)` |
| Epic 8 | Perfect block visual feedback (particle burst), audio ("clang" SFX), optional screen hit-stop |

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ Files in `_Game/Scripts/Combat/` and `_Game/ScriptableObjects/Config/` |
| No magic numbers | ✅ `perfectBlockWindowDuration` from `_config` (CombatConfigSO) |
| GameLog only | ✅ All logging via `GameLog.*` with `[Combat]` TAG |
| Null-guard in Awake | ✅ Existing guards in `PlayerCombat` unchanged |
| No string allocations in hot path | ✅ No per-frame allocations added |
| Event subscription in OnEnable/OnDisable | ✅ No new event subscriptions; existing pattern unchanged |
| OnDisable null guard | ✅ Existing `if (_input == null) return;` guard covers all |
| WriteDefaultValues: false | N/A — no new animator states |
| No SendMessage | ✅ Direct `TryReceiveHit()` method call; same-system |
| Debug tools guarded | ✅ `OnGUI` remains inside `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` |
| Input System actions only | ✅ No new input — detection from game state only |
| `HitResult` enum accessible | ✅ Defined at namespace scope in `PlayerCombat.cs`; `Tests.EditMode.asmdef` references `"Game"` assembly |

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs           ← Add perfectBlockWindowDuration
Assets/_Game/Scripts/Combat/PlayerCombat.cs                       ← Add HitResult enum + PB fields + TryReceiveHit + Update countdown + OnGUI update
```

**Files to CREATE:**
```
Assets/Tests/EditMode/PerfectBlockTests.cs                        ← NEW Edit Mode tests
Assets/Tests/EditMode/PerfectBlockTests.cs.meta                   ← auto-generated by Unity
```

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs         ← Story 2.1 (unchanged)
└── PlayerCombat.cs          ← Story 2.4 + extended (HitResult enum, PB window, TryReceiveHit)
```

**`ScriptableObjects/Config/` after this story:**
```
Assets/_Game/ScriptableObjects/Config/
└── CombatConfigSO.cs        ← Story 2.1 + 2.3 + 2.5 (perfectBlockWindowDuration added)
```

**`CombatConfigSO` complete state after this story:**
```csharp
[Header("Stamina Pool")]
public float baseStaminaPool = 100f;

[Header("Stamina Costs")]
public float attackStaminaCost = 20f;
public float blockStaminaCostPerHit = 15f;
public float dodgeStaminaCost = 25f;

[Header("Stamina Recovery")]
public float staminaRegenRate = 20f;
public float staminaRegenDelay = 1.5f;

[Header("Combo Attack")]
public float comboWindowDelay = 0.3f;
public float comboWindowDuration = 0.18f;

[Header("Perfect Block")]
public float perfectBlockWindowDuration = 0.25f;   // ← NEW
```

### Precise Implementation: PlayerCombat.cs Changes

**At namespace scope, before `PlayerCombat` class:**
```csharp
namespace Game.Combat
{
    /// <summary>Result of a hit attempt against the player.</summary>
    public enum HitResult { PerfectBlock, Blocked, NotBlocked }

    [RequireComponent(typeof(StaminaSystem))]
    public class PlayerCombat : MonoBehaviour
    {
        // ... existing class body
    }
}
```

**New private fields (alongside `_isBlocking`):**
```csharp
// Block state
private bool _isBlocking = false;
private bool _isPerfectBlockWindowOpen = false;
private float _perfectBlockWindowTimer = 0f;
```

**Updated `OnBlockStarted()`:**
```csharp
private void OnBlockStarted(InputAction.CallbackContext ctx)
{
    if (!_staminaSystem.HasEnough(_config.blockStaminaCostPerHit))
    {
        GameLog.Warn(TAG, "Cannot block: insufficient stamina");
        return;
    }

    _isBlocking = true;
    _animator.SetBool(IsBlockingHash, true);

    // Open perfect block window
    _isPerfectBlockWindowOpen = true;
    _perfectBlockWindowTimer = _config.perfectBlockWindowDuration;
    GameLog.Info(TAG, $"Perfect block window opened ({_config.perfectBlockWindowDuration:F2}s)");

    // Reset any in-progress combo — cannot combo mid-block
    _comboWindowOpen = false;
    _comboWindowDelay = 0f;
    _comboWindowTimer = 0f;
    _comboStep = 0;

    GameLog.Info(TAG, "Block raised");
}
```

**Updated `OnBlockCanceled()`:**
```csharp
private void OnBlockCanceled(InputAction.CallbackContext ctx)
{
    if (!_isBlocking) return; // Block was never raised (stamina denied entry)
    _isBlocking = false;
    _animator.SetBool(IsBlockingHash, false);
    _isPerfectBlockWindowOpen = false;
    _perfectBlockWindowTimer = 0f;
    GameLog.Info(TAG, "Block lowered");
}
```

**Updated `Update()` — new section appended after combo countdown:**
```csharp
// Phase 3: Perfect block window countdown
if (_isPerfectBlockWindowOpen && _perfectBlockWindowTimer > 0f)
{
    _perfectBlockWindowTimer -= Time.deltaTime;
    if (_perfectBlockWindowTimer <= 0f)
    {
        _isPerfectBlockWindowOpen = false;
        GameLog.Info(TAG, "Perfect block window closed — regular block mode");
    }
}
```

**New public method:**
```csharp
/// <summary>
/// Called by enemy attack code when a hit connects with the player.
/// Returns PerfectBlock (no stamina cost, stagger attacker), Blocked (stamina consumed),
/// or NotBlocked (apply full damage). Story 2.7 implements the caller.
/// </summary>
public HitResult TryReceiveHit(GameObject attacker)
{
    if (!_isBlocking)
    {
        GameLog.Info(TAG, "Hit received — not blocking");
        return HitResult.NotBlocked;
    }

    if (_isPerfectBlockWindowOpen)
    {
        _isPerfectBlockWindowOpen = false;
        _perfectBlockWindowTimer = 0f;
        GameLog.Info(TAG, "PERFECT BLOCK — no stamina cost, attacker staggers");
        return HitResult.PerfectBlock;
    }

    // Regular block — consume stamina per hit
    bool consumed = _staminaSystem.Consume(_config.blockStaminaCostPerHit);
    if (!consumed)
    {
        // Block broken by hit — stamina exhausted
        _isBlocking = false;
        _animator.SetBool(IsBlockingHash, false);
        _isPerfectBlockWindowOpen = false;
        GameLog.Warn(TAG, "Block broken by hit — stamina depleted");
        return HitResult.NotBlocked;
    }

    GameLog.Info(TAG, "Block absorbed hit — stamina consumed");
    return HitResult.Blocked;
}
```

**Updated `OnGUI()` Block line:**
```csharp
string pbWindow = _isPerfectBlockWindowOpen ? $"PB: {_perfectBlockWindowTimer:F2}s" : "PB: closed";
GUI.Label(new Rect(10, 130, 400, 26), $"Block: {(_isBlocking ? "RAISED" : "lowered")} | {pbWindow}", style);
```

### Edit Mode Test: PerfectBlockTests.cs

```csharp
using NUnit.Framework;
using Game.Combat;

/// <summary>
/// Edit Mode tests for perfect block logic used by PlayerCombat.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors BlockGateTests and ComboWindowTests.
/// Requires Tests.EditMode.asmdef to reference "Game" assembly (established in Story 1.5).
/// </summary>
public class PerfectBlockTests
{
    // Simulates the TryReceiveHit formula
    private HitResult SimulateHit(bool isBlocking, bool isPerfectWindowOpen,
                                   float stamina, float blockCostPerHit)
    {
        if (!isBlocking) return HitResult.NotBlocked;
        if (isPerfectWindowOpen) return HitResult.PerfectBlock;
        if (stamina >= blockCostPerHit) return HitResult.Blocked;
        return HitResult.NotBlocked; // Block broken by stamina depletion
    }

    [Test]
    public void Hit_ReturnsPerfectBlock_WhenBlockingAndWindowOpen()
    {
        Assert.That(SimulateHit(true, true, 100f, 15f), Is.EqualTo(HitResult.PerfectBlock));
    }

    [Test]
    public void Hit_ReturnsPerfectBlock_EvenAtZeroStamina_WhenWindowOpen()
    {
        // Perfect block costs NO stamina — window open overrides stamina check
        Assert.That(SimulateHit(true, true, 0f, 15f), Is.EqualTo(HitResult.PerfectBlock));
    }

    [Test]
    public void Hit_ReturnsBlocked_WhenBlockingWindowClosedAndHasStamina()
    {
        Assert.That(SimulateHit(true, false, 100f, 15f), Is.EqualTo(HitResult.Blocked));
        Assert.That(SimulateHit(true, false, 15f, 15f), Is.EqualTo(HitResult.Blocked));
    }

    [Test]
    public void Hit_ReturnsNotBlocked_WhenBlockBrokenByStaminaDepletion()
    {
        Assert.That(SimulateHit(true, false, 5f, 15f), Is.EqualTo(HitResult.NotBlocked));
        Assert.That(SimulateHit(true, false, 0f, 15f), Is.EqualTo(HitResult.NotBlocked));
    }

    [Test]
    public void Hit_ReturnsNotBlocked_WhenNotBlocking()
    {
        Assert.That(SimulateHit(false, false, 100f, 15f), Is.EqualTo(HitResult.NotBlocked));
        Assert.That(SimulateHit(false, true, 100f, 15f), Is.EqualTo(HitResult.NotBlocked));
    }
}
```

### Debug Display Stack (after this story)

```
[y=50]   Stamina: 80 / 100                                      ← StaminaSystem.OnGUI
[y=70]   Combat: [Ready]                                        ← PlayerCombat gate
[y=100]  Combo: step 0 | closed                                 ← PlayerCombat combo
[y=130]  Block: RAISED | PB: 0.18s  (or)                       ← PlayerCombat block + PB window
         Block: RAISED | PB: closed (once window expires)
         Block: lowered | PB: closed (when released)
```

### References

- Epic 2 story 5 user story: [Source: _bmad-output/epics.md#Epic 2: Combat System]
- GDD perfect block mechanic: [Source: _bmad-output/gdd.md#Game Mechanics — Combat (Real-Time, Gothic-Style)]
- GDD input feel target: "The perfect block window is tight enough to reward skill without being frame-perfect" [Source: _bmad-output/gdd.md#Input Feel]
- Architecture Decision 1 — Component-based + Event Bus: [Source: _bmad-output/game-architecture.md#Decision 1]
- `PerfectBlockHandler.cs` planned location: [Source: _bmad-output/game-architecture.md#Directory Structure] — This story integrates PB logic into `PlayerCombat` at prototype scale; `PerfectBlockHandler.cs` deferred
- Architecture consistency rule — same-system direct references: [Source: _bmad-output/project-context.md#Architecture Patterns] — `EnemyBrain → PlayerCombat.TryReceiveHit()` is same-system (both `Combat/`); acceptable
- `CombatConfigSO` current state: [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- `PlayerCombat` current state (Story 2.4): [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs]
- `StaminaSystem.Consume()` / `HasEnough()` API: [Source: Assets/_Game/Scripts/Combat/StaminaSystem.cs]
- Story 2.4 block foundation and "What Story 2.5 Adds": [Source: _bmad-output/implementation-artifacts/2-4-manual-blocking.md#What Subsequent Stories Build On]
- `OnBlockCanceled` null guard (`if (!_isBlocking) return`): [Source: _bmad-output/implementation-artifacts/2-4-manual-blocking.md#Code Review Fixes Applied]
- `OnDisable` null guard pattern: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- No magic numbers rule: [Source: _bmad-output/project-context.md#Configuration Management]
- GameLog mandatory: [Source: _bmad-output/project-context.md#Logging]
- `Tests.EditMode.asmdef` references `"Game"`: [Source: CLAUDE.md#Assembly Setup]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- 26/26 Edit Mode tests passed (5 new PerfectBlockTests + 21 regression), 0 compile errors.

### Completion Notes List

- Added `HitResult` enum at namespace scope in `PlayerCombat.cs` (before class body) — accessible to `Tests.EditMode` assembly and future `EnemyBrain`.
- `TryReceiveHit(GameObject)` implements the three-path API: PerfectBlock → no cost; Blocked → stamina consumed; NotBlocked → block either absent or broken by stamina depletion.
- PB window countdown placed after combo countdown in `Update()`. No scheduling conflict: `OnBlockStarted` always resets `_comboWindowDelay = 0f`, so Phase 1's early `return` never fires while PB window is open.
- `CombatConfigSO` extended with `[Header("Perfect Block")]` + `perfectBlockWindowDuration = 0.25f`; tunable at runtime in Inspector without code changes.
- `PerfectBlockTests.cs` covers all 5 required scenarios via pure formula simulation (no MonoBehaviour lifecycle needed).
- Task 4 (Play Mode) is manual; left unchecked per story definition.

### File List

- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` — added perfectBlockWindowDuration field
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` — HitResult enum, PB fields, OnBlockStarted/Canceled updates, Update Phase 3, TryReceiveHit, OnGUI update
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs` — cosmetic OnGUI fix: added GUIStyle for font-size consistency with PlayerCombat overlay
- `Assets/Tests/EditMode/PerfectBlockTests.cs` — NEW: 5 Edit Mode tests
- `Assets/Tests/EditMode/PerfectBlockTests.cs.meta` — auto-generated by Unity

## Change Log

- 2026-03-08: Story 2.5 implemented — perfect block timing window, TryReceiveHit API, PerfectBlockTests (5 tests). All 26 Edit Mode tests pass.
- 2026-03-08: Code review fixes — Phase 3 (PB countdown) moved before Phase 1 early return in Update() so it runs every frame independent of combo state; `_perfectBlockWindowTimer = 0f` added to block-broken path in TryReceiveHit for field consistency; `attacker` param documented with Story 2.7 intent; PerfectBlockTests multi-assertion methods wrapped in Assert.Multiple; StaminaSystem.cs added to File List.
