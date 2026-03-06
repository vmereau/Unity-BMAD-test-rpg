# Story 2.2: Zero Stamina Gate

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to be prevented from attacking, blocking, or dodging when I have no stamina,
so that stamina management is a meaningful tactical constraint during combat.

## Acceptance Criteria

1. A `PlayerCombat` MonoBehaviour exists at `Assets/_Game/Scripts/Combat/PlayerCombat.cs` in namespace `Game.Combat`.
2. `PlayerCombat.Awake()` acquires a reference to `StaminaSystem` via `GetComponent<StaminaSystem>()` and to `CombatConfigSO` via `[SerializeField]`; if either is missing, the component logs an error and disables itself (null-guard pattern).
3. `PlayerCombat.Awake()` also creates an `InputSystem_Actions` instance (same pattern as other combat components).
4. Input is wired in `OnEnable` / unwired in `OnDisable` with null guard: subscribes to `_input.Player.Attack.started` in `OnEnable`, unsubscribes and disposes in `OnDisable`.
5. When `Attack` input fires (`LMB`) and `_staminaSystem.HasEnough(_config.attackStaminaCost)` returns **false**, no action is taken and `GameLog.Warn` emits `"[Combat] Cannot attack: insufficient stamina"`.
6. When `Attack` input fires and stamina is sufficient, `_staminaSystem.Consume(_config.attackStaminaCost)` is called (returns true) and `GameLog.Info` emits `"[Combat] Attack action (placeholder — directional logic added in Story 2.3)"`.
7. A development-only debug display (position: `Rect(10, 70, 300, 20)`) shows `"Combat: [Ready]"` when `HasEnough(attackStaminaCost)` is true, and `"Combat: [STAMINA EMPTY]"` when false — visible in Development builds and Editor only.
8. `PlayerCombat` is attached to the Player prefab root alongside `StaminaSystem`, with `CombatConfig.asset` wired to its `_config` field in the Inspector.
9. After draining stamina to zero by pressing LMB repeatedly, further LMB presses produce no stamina change and log the "Cannot attack" warning; as stamina regenerates (wait ≥ 1.5s regen delay), attacks become possible again.
10. An Edit Mode test class `PlayerCombatGateTests` exists at `Assets/Tests/EditMode/PlayerCombatGateTests.cs` with ≥ 3 tests covering: gate allows when sufficient, gate blocks when insufficient, gate blocks at exactly zero stamina.
11. No compile errors; no Play Mode errors in TestScene.unity; all Stories 1.1–2.1 behavior is unchanged.

## Tasks / Subtasks

- [x] Task 1: Create PlayerCombat MonoBehaviour (AC: 1, 2, 3, 4, 5, 6, 7)
  - [x] 1.1 Create `Assets/_Game/Scripts/Combat/PlayerCombat.cs` — see Dev Notes for complete implementation
  - [x] 1.2 Verify `namespace Game.Combat`, correct `[SerializeField] private CombatConfigSO _config`
  - [x] 1.3 Verify null-guard Awake pattern: log error + `enabled = false` if `_config` or `_staminaSystem` missing
  - [x] 1.4 Verify `OnEnable` / `OnDisable` subscribe/unsubscribe with null guard for `_input`
  - [x] 1.5 Verify `TryAttack()` calls `HasEnough()` then `Consume()`, logs correct messages
  - [x] 1.6 Verify `OnGUI` debug display at `Rect(10, 70, 300, 20)` inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR`

- [x] Task 2: Attach PlayerCombat to Player prefab (AC: 8)
  - [x] 2.1 Open `Assets/_Game/Prefabs/Player/Player.prefab`
  - [x] 2.2 Add `PlayerCombat` component to the Player root GameObject (same root as `StaminaSystem`)
  - [x] 2.3 Wire `CombatConfig.asset` to the `_config` field in the Inspector

- [x] Task 3: Edit Mode tests (AC: 10)
  - [x] 3.1 Create `Assets/Tests/EditMode/PlayerCombatGateTests.cs` — see Dev Notes for test cases
  - [x] 3.2 Run tests via Unity Test Runner (Window → General → Test Runner → EditMode)
  - [x] 3.3 All ≥ 3 tests pass (green)

- [x] Task 4: Play Mode validation (AC: 9, 11)
  - [x] 4.1 Enter Play Mode in TestScene.unity — no console errors on startup
  - [x] 4.2 Confirm `"Combat: [Ready]"` debug label appears below the Stamina label
  - [x] 4.3 Press LMB repeatedly until stamina reaches 0 — `"Combat: [STAMINA EMPTY]"` label appears; further LMB presses log "Cannot attack"
  - [x] 4.4 Wait ~1.5s idle — stamina regenerates, label returns to `"Combat: [Ready]"`, LMB works again
  - [x] 4.5 Verify player movement (WASD), camera (mouse look), and animations (idle/walk/run/jump) still work
  - [x] 4.6 Check Unity Console for zero new errors or warnings

## Dev Notes

Story 2.2 establishes the **stamina gate enforcement layer** for Epic 2. It creates `PlayerCombat.cs`
as the root combat component that all subsequent combat stories (2.3–2.6) build upon. This story
proves the gate concept works using the existing `Attack` input action (LMB).

**Block and Dodge gates are NOT in scope here:** Block input (`RMB`) is added in Story 2.4;
Dodge input (`Space` / `Shift+direction`) is added in Story 2.6. Those stories will add the gate
check to their respective input handlers inside `PlayerCombat`.

### Precise Scope Boundaries

**In scope:**
- `PlayerCombat.cs` — full MonoBehaviour skeleton with Attack gate (LMB only)
- Player prefab: add `PlayerCombat` component + wire `CombatConfig.asset`
- Development debug display showing gate state
- Edit Mode tests for gate logic formulas

**Explicitly OUT of scope — do NOT implement:**
- Directional attack logic (rolling mouse delta buffer, direction resolution) — Story 2.3
- Hit detection, damage numbers, hit reactions — Story 2.3
- Block input + block gate — Story 2.4
- Dodge input + dodge gate — Story 2.6
- `WorldStateManager` script implementation — stub in Core.unity; scripted in 2.3+
- Health system — Story 2.8
- Any UI changes (HUD, health bar, stamina bar) — Epic 8

### Critical Architecture Rules

**Assembly & Namespace:**
- `PlayerCombat.cs` → namespace `Game.Combat` (compiles into `Game` assembly via `Game.asmdef`)
- Tests → `Assets/Tests/EditMode/PlayerCombatGateTests.cs` (in `Tests.EditMode` assembly, already references `Game`)

**Input Pattern — MANDATORY (matches existing components):**

Each combat component creates its own `InputSystem_Actions` instance in `Awake`. Subscribe in
`OnEnable`, unsubscribe + dispose in `OnDisable`. This is the established pattern from
`PlayerController` and `CameraController`.

**Key CLAUDE.md gotcha — OnDisable null guard (CRITICAL):**

`Awake()` creates `_input = new InputSystem_Actions()` only if the null-guards for `_config` and
`_staminaSystem` pass. If either null-guard fires, `enabled = false` is set BEFORE `_input` is
initialized, meaning `_input` is null when Unity calls `OnDisable()`. The null guard is mandatory:

```csharp
private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.Player.Attack.started -= OnAttackStarted;
    _input.Player.Disable();
    _input.Dispose();
}
```

**Logging — MANDATORY:**
```csharp
private const string TAG = "[Combat]";
// GameLog.Info / GameLog.Warn / GameLog.Error ONLY — never Debug.Log
```

**Config SO — MANDATORY (no magic numbers):**
Read `attackStaminaCost` from `_config`. Never hardcode stamina cost values.

**Direct component reference — correct pattern:**
`PlayerCombat` and `StaminaSystem` share the same GameObject (Player root), same system boundary
(`Scripts/Combat/`). Direct `GetComponent<StaminaSystem>()` in `Awake` is the right pattern per
architecture rules (same-system comms use direct references; cross-system uses `GameEventSO<T>`
channels only).

### PlayerCombat — Complete Implementation

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles player combat input and enforces stamina gating.
    /// Story 2.2: Attack gate with placeholder attack action.
    /// Story 2.3 will add directional attack logic.
    /// Story 2.4 will add Block input and gate.
    /// Story 2.6 will add Dodge input and gate.
    /// Attach to the Player prefab root alongside StaminaSystem.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;

        private StaminaSystem _staminaSystem;
        private InputSystem_Actions _input;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _staminaSystem = GetComponent<StaminaSystem>();
            if (_staminaSystem == null)
            {
                GameLog.Error(TAG, "StaminaSystem not found on Player — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Enable();
            _input.Player.Attack.started += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Disable();
            _input.Dispose();
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void TryAttack()
        {
            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, "Cannot attack: insufficient stamina");
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
            if (consumed)
            {
                // Placeholder: Story 2.3 replaces this with directional attack logic
                GameLog.Info(TAG, "Attack action (placeholder — directional logic added in Story 2.3)");
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null) return;
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 300, 20), $"Combat: [{state}]");
        }
#endif
    }
}
```

**Design notes:**
- `TryAttack()` is a private method (not the input callback directly) so it can be called from
  other paths in future stories without re-subscribing input — e.g., Story 2.3 may need to call
  `TryAttack()` from a direction-resolved path.
- `HasEnough()` is checked before `Consume()` — this is belt-and-suspenders since `Consume()`
  already returns false if insufficient, but the explicit check allows early-out logging with
  the correct "Cannot attack" message.
- `OnGUI` dynamically queries `HasEnough()` each frame (cheap float comparison) so the debug
  display accurately reflects the current gate state, including regen recovery.
- `_input.Player.Enable()` in `OnEnable` is safe even though `PlayerController` also calls
  `_input.Player.Enable()` on its own instance — each instance is independent. Unity Input
  System handles multiple active maps of the same type without conflict.

### Edit Mode Tests — PlayerCombatGateTests.cs

`PlayerCombat` is a MonoBehaviour (requires a scene) so tests validate pure gate logic formulas,
consistent with the testing rule: "Edit Mode tests for pure logic only."

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for the stamina gate formula used by PlayerCombat.
/// Tests pure math logic, not MonoBehaviour lifecycle.
/// </summary>
public class PlayerCombatGateTests
{
    [Test]
    public void Gate_AllowsAttack_WhenStaminaSufficient()
    {
        float currentStamina = 50f;
        float attackCost = 20f;
        bool hasEnough = currentStamina >= attackCost;
        Assert.That(hasEnough, Is.True);
    }

    [Test]
    public void Gate_BlocksAttack_WhenStaminaInsufficient()
    {
        float currentStamina = 10f;
        float attackCost = 20f;
        bool hasEnough = currentStamina >= attackCost;
        Assert.That(hasEnough, Is.False);
    }

    [Test]
    public void Gate_BlocksAttack_WhenStaminaExactlyZero()
    {
        float currentStamina = 0f;
        float attackCost = 20f;
        bool hasEnough = currentStamina >= attackCost;
        Assert.That(hasEnough, Is.False);
    }

    [Test]
    public void Gate_AllowsAttack_WhenStaminaExactlyEqualsToCost()
    {
        float currentStamina = 20f;
        float attackCost = 20f;
        bool hasEnough = currentStamina >= attackCost;
        Assert.That(hasEnough, Is.True);
    }
}
```

### Player Prefab Structure After This Story

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF)
├── PlayerController.cs
├── PlayerAnimator.cs
├── StaminaSystem.cs     (wired to CombatConfig.asset — from Story 2.1)
├── PlayerCombat.cs      ← NEW (wired to CombatConfig.asset)
├── CameraTarget         (child, local Y = 1.6)
└── Character            (child — Mixamo FBX)
```

> Note from CLAUDE.md: `CameraController` is wired in `TestScene.unity` as a scene component
> (not in the prefab). Do not move it — this is known drift, not a bug.

### Debug Display Stack (after this story)

In Play Mode (Development/Editor build), the upper-left corner shows:
```
[line at y=50]  Stamina: 80 / 100         ← from StaminaSystem (Story 2.1)
[line at y=70]  Combat: [Ready]            ← from PlayerCombat (this story, Story 2.2)
```

Ensure the new `OnGUI` label position (`Rect(10, 70, ...)`) does not overlap the existing
Stamina label (`Rect(10, 50, ...)`). The 20px gap is sufficient.

### What Subsequent Stories Build On PlayerCombat

| Story | What it adds to PlayerCombat |
|-------|------------------------------|
| 2.3 Directional Attacks | Replaces placeholder `TryAttack()` log with `DirectionalAttackSampler` resolution + animation trigger |
| 2.4 Manual Blocking | Adds `Block` input action to `InputSystem_Actions.inputactions`; subscribes to `_input.Player.Block` in `OnEnable`; calls `_staminaSystem.HasEnough(blockCost)` gate |
| 2.5 Perfect Block | Adds perfect block timing window inside block handler; successful perfect block skips `Consume()` call |
| 2.6 Dodge Roll | Adds `Dodge` input action; `_staminaSystem.Consume(dodgeStaminaCost)` gate; triggers `DodgeController` |

### Input System Architecture Note

**Story 2.2 uses:** `_input.Player.Attack` (LMB) — **already exists** in `InputSystem_Actions`
**Story 2.4 must add:** `Block` action (RMB) to `InputSystem_Actions.inputactions` asset
**Story 2.6 must add:** `Dodge` action to `InputSystem_Actions.inputactions` asset

Do NOT modify `InputSystem_Actions.inputactions` in this story. The Attack action is already
defined in the Player action map from Epic 1 setup.

### Multiple InputSystem_Actions Instances — Safe Pattern

`PlayerCombat` creates its own `InputSystem_Actions _input` instance (same as `PlayerController`
and `CameraController`). Each component independently manages its instance lifecycle. Unity Input
System allows multiple instances reading the same physical hardware — no conflict occurs because:
- Each instance is enabled/disabled independently in `OnEnable`/`OnDisable`
- They read the same `Attack` action but dispatch independently to each subscriber
- `Dispose()` in `OnDisable` properly releases the instance

This is the established project pattern and is correct.

### Architecture Compliance

| Rule | How This Story Complies |
|------|------------------------|
| All custom code under `Assets/_Game/` | ✅ `PlayerCombat.cs` in `_Game/Scripts/Combat/` |
| No magic numbers — config SOs only | ✅ All costs read from `_config.attackStaminaCost` |
| GameLog only — no Debug.Log | ✅ All logging via `GameLog.Warn/Info/Error` with `[Combat]` TAG |
| Null-guard `[SerializeField]` refs in Awake | ✅ `_config` and `_staminaSystem` both null-checked |
| Event subscription in OnEnable/OnDisable | ✅ `Attack.started` subscribed in `OnEnable`, unsubscribed in `OnDisable` |
| OnDisable null guard (CLAUDE.md pattern) | ✅ `if (_input == null) return;` in `OnDisable` |
| Same-system comms → direct reference | ✅ `GetComponent<StaminaSystem>()` (same GameObject, same system) |
| Debug tools in Development builds only | ✅ `OnGUI` wrapped in `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` |
| Script naming: PascalCase | ✅ `PlayerCombat` |
| Private fields: `_camelCase` | ✅ `_config`, `_staminaSystem`, `_input` |
| No `Resources.Load()` | ✅ Direct Inspector reference |

### Project Structure Notes

New files added by this story:

```
Assets/_Game/Scripts/Combat/PlayerCombat.cs               ← NEW MonoBehaviour
Assets/Tests/EditMode/PlayerCombatGateTests.cs            ← NEW Edit Mode tests
```

Modified files:

```
Assets/_Game/Prefabs/Player/Player.prefab                 ← PlayerCombat component added
```

All files are within the `Game` assembly (`Game.asmdef`), except tests which are in
`Tests.EditMode.asmdef` (already references `Game`). No assembly definition changes needed.

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs    ← from Story 2.1
└── PlayerCombat.cs     ← NEW (this story)
```

### References

- Gate story requirement: [Source: epics.md#Epic 2: Combat System — Story 2]
- GDD combat stamina mechanic: [Source: gdd.md#Combat (Real-Time, Gothic-Style)]
- `StaminaSystem.HasEnough()` and `Consume()` API: [Source: implementation-artifacts/2-1-stamina-bar-system.md#StaminaSystem — Complete Implementation]
- `CombatConfigSO.attackStaminaCost` field: [Source: implementation-artifacts/2-1-stamina-bar-system.md#CombatConfigSO — Complete Implementation]
- PlayerCombat script location: [Source: game-architecture.md#System Location Mapping]
- OnDisable null guard pattern: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- `GameLog` mandatory usage: [Source: project-context.md#Logging — MANDATORY]
- No magic numbers in game logic: [Source: project-context.md#Config & Data Anti-Patterns]
- Event subscription in OnEnable/OnDisable: [Source: project-context.md#MonoBehaviour Lifecycle Rules]
- Same-system direct reference: [Source: game-architecture.md#Standard Patterns — Component Communication]
- `Game.asmdef` assembly reference: [Source: CLAUDE.md#Assembly Setup (As of Story 1.5)]
- Edit Mode test scope: [Source: project-context.md#Testing Rules]
- Debug tools in Development builds only: [Source: game-architecture.md#Debug & Development Tools]
- Debug display Stamina at y=50: [Source: implementation-artifacts/2-1-stamina-bar-system.md#StaminaSystem — Complete Implementation → OnGUI]
- PlayerCombat reference for future story: [Source: implementation-artifacts/2-1-stamina-bar-system.md#What Stories 2.2–2.6 Will Use From This Story]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- No issues encountered. Compilation clean on first attempt.

### Completion Notes List

- Created `PlayerCombat.cs` in `Assets/_Game/Scripts/Combat/` with full stamina gate enforcement
- `Awake` null-guards both `_config` (SerializeField) and `_staminaSystem` (GetComponent); either missing disables the component and logs an error
- `OnEnable`/`OnDisable` null guard on `_input` per CLAUDE.md pattern (Awake may disable before OnEnable runs)
- `TryAttack()` checks `HasEnough()` before `Consume()`; logs "Cannot attack: insufficient stamina" on gate block
- `OnGUI` debug display at `Rect(10, 70, ...)` shows `[Ready]` / `[STAMINA EMPTY]` — wrapped in `#if DEVELOPMENT_BUILD || UNITY_EDITOR`
- `PlayerCombat` added to Player prefab root alongside `StaminaSystem`; `CombatConfig.asset` wired to `_config`
- 4 Edit Mode tests created and passing (all green); full suite 11/11 passing, zero regressions
- Play Mode entry: no console errors or warnings; StaminaSystem initialized at pool=100

### File List

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` (new)
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs.meta` (new)
- `Assets/Tests/EditMode/PlayerCombatGateTests.cs` (new)
- `Assets/Tests/EditMode/PlayerCombatGateTests.cs.meta` (new)
- `Assets/_Game/Prefabs/Player/Player.prefab` (modified — PlayerCombat component added)
- `.gitignore` (modified — added Assets/Screenshots/ exclusion)
- `_bmad-output/implementation-artifacts/2-2-zero-stamina-gate.md` (new)

## Change Log

- 2026-03-04: Story implemented — `PlayerCombat.cs` created with stamina gate enforcement; `PlayerCombatGateTests.cs` created with 4 passing Edit Mode tests; `PlayerCombat` component added to Player prefab with `CombatConfig.asset` wired. Status set to review.
- 2026-03-04: Code review fixes applied — (M1) `_input = null` after `Dispose()` to prevent use-after-dispose on re-enable; (M2) tests rewritten to call `StaminaSystem.HasEnough()` directly via reflection rather than inline formula; (M3) `Assets/Screenshots/` added to `.gitignore`; (L1) `GameLog.Error` added for unexpected `Consume()` false return; (L2) `[RequireComponent(typeof(StaminaSystem))]` added to `PlayerCombat`. Status set to done.
