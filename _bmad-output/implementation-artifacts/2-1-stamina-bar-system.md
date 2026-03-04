# Story 2.1: Stamina Bar System

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want a stamina bar that depletes when I attack, block, or dodge,
so that combat actions have a meaningful resource cost that requires tactical management.

## Acceptance Criteria

1. A `CombatConfigSO` ScriptableObject class exists at `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` with stamina parameters: `baseStaminaPool`, `attackStaminaCost`, `blockStaminaCostPerHit`, `dodgeStaminaCost`, `staminaRegenRate`, `staminaRegenDelay`.
2. A `CombatConfig.asset` instance exists at `Assets/_Game/Data/Config/CombatConfig.asset` with sensible defaults (pool: 100, attack cost: 20, block cost: 15, dodge cost: 25, regen rate: 20/s, regen delay: 1.5s).
3. A `StaminaSystem` MonoBehaviour exists at `Assets/_Game/Scripts/Combat/StaminaSystem.cs` with: `CurrentStamina` (float), `MaxStamina` (float), `Consume(float amount)` → bool (true if consumed, false if insufficient), `HasEnough(float amount)` → bool, and auto-regen logic.
4. `StaminaSystem` is attached to the Player prefab root with `CombatConfig.asset` wired in the Inspector.
5. Stamina cannot drop below 0 or exceed `MaxStamina`.
6. Stamina regenerates automatically at `staminaRegenRate` units/second after `staminaRegenDelay` seconds have elapsed since the last `Consume()` call.
7. A development-only debug display shows `"Stamina: {current:F0} / {max:F0}"` in Play Mode — visible in Development builds and Editor only.
8. An Edit Mode test class `StaminaSystemTests` exists at `Assets/Tests/EditMode/StaminaSystemTests.cs` verifying: consume reduces stamina, `Consume` returns false when insufficient, stamina cannot go negative, regen formula does not exceed max.
9. No compile errors; no Play Mode errors in TestScene.unity; Stories 1.1–1.5 behavior is unchanged.

## Tasks / Subtasks

- [x] Task 1: Create CombatConfigSO (AC: 1, 2)
  - [x] 1.1 Create `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` — see Dev Notes for complete implementation
  - [x] 1.2 Create `CombatConfig.asset` at `Assets/_Game/Data/Config/CombatConfig.asset` with default values (pool: 100, attack: 20, block: 15, dodge: 25, regen rate: 20, regen delay: 1.5)
  - [x] 1.3 Verify no compile errors after CombatConfigSO.cs creation

- [x] Task 2: Create StaminaSystem (AC: 3, 5, 6)
  - [x] 2.1 Create `Assets/_Game/Scripts/Combat/StaminaSystem.cs` — see Dev Notes for complete implementation
  - [x] 2.2 Verify `Consume()`, `HasEnough()`, `CurrentStamina`, `MaxStamina` all present and correct
  - [x] 2.3 Verify regen cooldown resets on `Consume()`, regen resumes after `staminaRegenDelay` seconds

- [x] Task 3: Attach StaminaSystem to Player prefab (AC: 4)
  - [x] 3.1 Open `Assets/_Game/Prefabs/Player/Player.prefab`
  - [x] 3.2 Add `StaminaSystem` component to the Player root GameObject
  - [x] 3.3 Wire `CombatConfig.asset` to the `_config` field in the Inspector

- [x] Task 4: Validate debug display (AC: 7)
  - [x] 4.1 Enter Play Mode in TestScene.unity — verify Stamina label appears at screen position (10, 50)
  - [x] 4.2 Confirm display reads "Stamina: 100 / 100" at startup

- [x] Task 5: Edit Mode tests (AC: 8)
  - [x] 5.1 Create `Assets/Tests/EditMode/StaminaSystemTests.cs` — see Dev Notes for test cases
  - [x] 5.2 Run tests via Unity Test Runner (Window → General → Test Runner → EditMode)
  - [x] 5.3 All 4 tests pass (green)

- [x] Task 6: Validate regression (AC: 9)
  - [x] 6.1 Enter Play Mode in TestScene.unity — no console errors
  - [x] 6.2 Verify player movement (WASD), camera (mouse look), and animations (idle/walk/run) still work
  - [x] 6.3 Check Unity Console for any new assembly or compilation errors

## Dev Notes

This story establishes the stamina system **foundation** for all of Epic 2. Subsequent stories call
`StaminaSystem.Consume()` and `HasEnough()` when implementing their specific actions. Nothing in
this story wires input to consumption — that happens in stories 2.3 (attacks), 2.4 (blocking),
and 2.6 (dodge roll).

### Precise Scope Boundaries

**In scope:**
- `CombatConfigSO.cs` and `CombatConfig.asset`
- `StaminaSystem.cs` — full stamina API (consume, query, auto-regen)
- Player prefab: add `StaminaSystem` component
- Development debug display (`OnGUI`, Editor/Dev builds only)
- Edit Mode tests for stamina formula math

**Explicitly OUT of scope — do NOT implement:**
- Wiring attack / block / dodge input to `Consume()` — stories 2.3, 2.4, 2.6
- Adding Block or Dodge input actions to `InputSystem_Actions.inputactions` — stories 2.4 and 2.6
- HUD stamina bar (health bar, UI canvas) — Epic 8 story 4
- `PlayerCombat.cs` — story 2.3
- `WorldStateManager.cs` script implementation — stub in Core.unity; scripted in Epic 2 stories 2.3+
- `OnStaminaChanged` GameEventSO channel — not needed until UI integration in Epic 8

### Critical Architecture Rules

**Assembly & Namespace:**
- `CombatConfigSO.cs` → namespace `Game.Combat` (compiles into `Game` assembly via `Game.asmdef`)
- `StaminaSystem.cs` → namespace `Game.Combat` (same assembly)
- `StaminaSystemTests.cs` → unnamespaced or `namespace Game.Tests` (in `Tests.EditMode` assembly which references `Game`)

**Config SO — MANDATORY (no magic numbers):**
ALL tunable stamina values must live in `CombatConfigSO`. Never hardcode stamina amounts directly in `StaminaSystem.cs`.

**Logging — MANDATORY:**
```csharp
private const string TAG = "[Combat]";
// GameLog.Info / GameLog.Warn / GameLog.Error ONLY — never Debug.Log
```

**Null-guard in Awake — MANDATORY pattern:**
```csharp
private void Awake()
{
    if (_config == null)
    {
        GameLog.Error(TAG, "CombatConfigSO not assigned — StaminaSystem disabled");
        enabled = false;
        return;
    }
    _currentStamina = _config.baseStaminaPool;
}
```

**OnDisable null guard:** `StaminaSystem` does not initialize `_input` in `OnEnable`, so the
`if (_input == null) return;` guard pattern (from CLAUDE.md) is not required here. If a future
modification adds event subscriptions in `OnEnable`, apply the guard then.

**Cross-system rule:** `StaminaSystem` is in `Scripts/Combat/`. Future `PlayerCombat.cs` (story 2.3,
same folder) will call it via direct MonoBehaviour reference — this is correct per architecture rules
(same-system comms use direct references; cross-system uses `GameEventSO<T>` channels only).

**No `GameEventSO<float>` for stamina yet:** The architecture defines a `[Combat]` system boundary.
Stamina changes are internal to that system in Epic 2. A `OnStaminaChanged` event channel is only
needed when the HUD needs to react (Epic 8). Do NOT add it now.

### CombatConfigSO — Complete Implementation

```csharp
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Balancing values for the combat system. Assign CombatConfig.asset in Inspector.
    /// All tunable gameplay values live here — never hardcode in combat scripts.
    /// </summary>
    [CreateAssetMenu(menuName = "Config/Combat", fileName = "CombatConfig")]
    public class CombatConfigSO : ScriptableObject
    {
        [Header("Stamina Pool")]
        public float baseStaminaPool = 100f;

        [Header("Stamina Costs")]
        public float attackStaminaCost = 20f;
        public float blockStaminaCostPerHit = 15f;
        public float dodgeStaminaCost = 25f;

        [Header("Stamina Recovery")]
        [Tooltip("Stamina units recovered per second after the regen delay elapses.")]
        public float staminaRegenRate = 20f;
        [Tooltip("Seconds after last Consume() call before regen begins.")]
        public float staminaRegenDelay = 1.5f;

        [Header("Directional Attack (Story 2.3)")]
        [Tooltip("Average mouse delta magnitude below which attack direction defaults to Overhead.")]
        public float attackDirectionThreshold = 0.3f;
        [Tooltip("Number of frames to average mouse delta over for direction resolution.")]
        public int directionSampleFrames = 5;
    }
}
```

> **Why include attack direction fields now?** Architecture defines them in `CombatConfigSO` and
> they must live here. Story 2.3 will reference these fields. Adding them now avoids a later
> refactor — they are unused until 2.3 and that is fine.

### StaminaSystem — Complete Implementation

```csharp
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Manages the player's stamina pool. All combat actions call Consume() before executing.
    /// Stamina regenerates automatically after a configurable delay since the last consumption.
    /// Attach to the Player prefab root alongside PlayerController.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;

        private float _currentStamina;
        private float _regenCooldown;

        /// <summary>Current stamina value in range [0, MaxStamina].</summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>Maximum stamina pool from config.</summary>
        public float MaxStamina => _config != null ? _config.baseStaminaPool : 0f;

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — StaminaSystem disabled");
                enabled = false;
                return;
            }
            _currentStamina = _config.baseStaminaPool;
            GameLog.Info(TAG, $"StaminaSystem initialized. Pool: {_config.baseStaminaPool}");
        }

        private void Update()
        {
            if (_regenCooldown > 0f)
            {
                _regenCooldown -= Time.deltaTime;
                return;
            }

            if (_currentStamina < _config.baseStaminaPool)
            {
                _currentStamina = Mathf.Min(
                    _currentStamina + _config.staminaRegenRate * Time.deltaTime,
                    _config.baseStaminaPool);
            }
        }

        /// <summary>
        /// Attempts to consume <paramref name="amount"/> stamina.
        /// Returns true if stamina was consumed; false if insufficient (stamina unchanged).
        /// Resets the regen cooldown on successful consumption.
        /// </summary>
        public bool Consume(float amount)
        {
            if (amount <= 0f) return true;

            if (_currentStamina < amount)
            {
                GameLog.Warn(TAG, $"Insufficient stamina: needed {amount}, had {_currentStamina:F1}");
                return false;
            }

            _currentStamina -= amount;
            _currentStamina = Mathf.Max(_currentStamina, 0f);  // safety clamp
            _regenCooldown = _config.staminaRegenDelay;
            GameLog.Info(TAG, $"Stamina consumed: -{amount}. Remaining: {_currentStamina:F1}");
            return true;
        }

        /// <summary>Returns true if the player has at least <paramref name="amount"/> stamina.</summary>
        public bool HasEnough(float amount) => _currentStamina >= amount;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null) return;
            GUI.Label(new Rect(10, 50, 300, 20),
                $"Stamina: {_currentStamina:F0} / {_config.baseStaminaPool:F0}");
        }
#endif
    }
}
```

**Design notes:**
- `Consume()` returns `bool` so callers (PlayerCombat, DodgeController) can branch on success/fail.
- The `Mathf.Max(..., 0f)` safety clamp after subtraction is a belt-and-suspenders guard; the
  `_currentStamina < amount` check above should prevent negative values, but floating-point
  subtraction can produce tiny negatives (e.g. `-0.0001f`).
- `OnGUI` is inside `#if DEVELOPMENT_BUILD || UNITY_EDITOR` to guarantee zero overhead in
  release builds (compiler strips the block entirely). This matches the architecture mandate:
  "Debug tools: active in Development Builds only; zero overhead in Release builds."
- Regen cooldown is reset only on successful `Consume()` — failed attempts (insufficient stamina)
  do NOT reset the cooldown, allowing recovery to continue uninterrupted.

### Edit Mode Tests — StaminaSystemTests.cs

`StaminaSystem` is a MonoBehaviour so we cannot directly instantiate it in Edit Mode without a scene.
The tests below validate the **pure mathematical formulas** used by `StaminaSystem`, which is the
correct scope per project testing rules ("Edit Mode tests for pure logic only").

```csharp
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the stamina system formulas.
/// Tests pure math logic, not MonoBehaviour lifecycle.
/// </summary>
public class StaminaSystemTests
{
    [Test]
    public void Consume_ReducesStaminaByAmount()
    {
        float pool = 100f;
        float cost = 20f;
        float result = pool - cost;
        Assert.That(result, Is.EqualTo(80f));
    }

    [Test]
    public void Consume_ReturnsFalse_WhenInsufficient()
    {
        float current = 10f;
        float needed = 20f;
        bool hasEnough = current >= needed;
        Assert.That(hasEnough, Is.False);
    }

    [Test]
    public void Stamina_CannotGoBelowZero()
    {
        float current = 5f;
        float cost = 20f;
        float result = Mathf.Max(current - cost, 0f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void Regen_DoesNotExceedMaxPool()
    {
        float current = 95f;
        float max = 100f;
        float regenRate = 20f;
        float deltaTime = 0.5f;  // half second regen = +10 → would be 105 without clamp
        float result = Mathf.Min(current + regenRate * deltaTime, max);
        Assert.That(result, Is.EqualTo(100f));
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
├── StaminaSystem.cs     ← NEW (wired to CombatConfig.asset)
├── CameraTarget         (child, local Y = 1.6)
└── Character            (child — Mixamo FBX)
```

> Note from CLAUDE.md: `CameraController` is wired in `TestScene.unity` as a scene component
> (not in the prefab). Do not move it — this is known drift, not a bug.

### What Stories 2.2–2.6 Will Use From This Story

| Story | What it calls on StaminaSystem |
|-------|-------------------------------|
| 2.2 Zero Stamina Gate | `HasEnough(cost)` before allowing any combat action |
| 2.3 Directional Attacks | `Consume(_combatConfig.attackStaminaCost)` on LMB press |
| 2.4 Manual Blocking | `Consume(_combatConfig.blockStaminaCostPerHit)` when a blocked hit lands |
| 2.5 Perfect Block | Does NOT consume stamina (perfect block negates cost) |
| 2.6 Dodge Roll | `Consume(_combatConfig.dodgeStaminaCost)` on dodge input |

Stories 2.3–2.6 will obtain their `StaminaSystem` reference via:
```csharp
// In PlayerCombat.Awake() (story 2.3):
_staminaSystem = GetComponent<StaminaSystem>();
if (_staminaSystem == null)
{
    GameLog.Error(TAG, "StaminaSystem not found on Player — PlayerCombat disabled");
    enabled = false;
    return;
}
```

This is correct because `PlayerCombat` and `StaminaSystem` are on the same GameObject (Player root)
and in the same `Scripts/Combat/` system boundary — direct `GetComponent` is the right pattern.

### Input System Note

The existing `InputSystem_Actions` Player map already has an `Attack` action (LMB). Block and Dodge
actions do NOT exist yet and will be added to `InputSystem_Actions.inputactions` in stories 2.4 and
2.6 respectively. Story 2.1 does NOT modify the input action asset.

### Project Structure Notes

New files added by this story:

```
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs   ← NEW SO class
Assets/_Game/Data/Config/CombatConfig.asset               ← NEW SO instance
Assets/_Game/Scripts/Combat/StaminaSystem.cs              ← NEW MonoBehaviour
Assets/Tests/EditMode/StaminaSystemTests.cs               ← NEW Edit Mode tests
```

All files are within the `Game` assembly (`Game.asmdef`), except tests which are in `Tests.EditMode.asmdef` (already references `Game`). No assembly definition changes needed.

**`Scripts/Combat/` is currently empty** — `StaminaSystem.cs` is the first file written into it.
The architecture lists many future Combat scripts (`PlayerCombat`, `DirectionalAttackSampler`,
`HitDetection`, `PerfectBlockHandler`, `DodgeController`) — those come in stories 2.3–2.6.

### Architecture Compliance

| Rule | How This Story Complies |
|------|------------------------|
| All custom code under `Assets/_Game/` | ✅ All new scripts in `_Game/ScriptableObjects/Config/` and `_Game/Scripts/Combat/` |
| No magic numbers — config SOs only | ✅ All values in `CombatConfigSO`; no literals in `StaminaSystem` |
| GameLog only — no Debug.Log | ✅ All logging via `GameLog.Info/Warn/Error` with `[Combat]` TAG |
| Null-guard `[SerializeField]` refs in Awake | ✅ `_config` null-checked, component disabled if missing |
| Event subscription in OnEnable/OnDisable | ✅ No events subscribed in this story — N/A |
| Config SO naming convention | ✅ `CombatConfigSO.cs`, instance `CombatConfig.asset` |
| Script naming: PascalCase | ✅ `CombatConfigSO`, `StaminaSystem` |
| Private fields: `_camelCase` | ✅ `_config`, `_currentStamina`, `_regenCooldown` |
| No `Resources.Load()` | ✅ Direct Inspector reference on Player prefab |
| Debug tools in Development builds only | ✅ `OnGUI` wrapped in `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` |

### References

- `CombatConfigSO` definition (fields and SO naming): [Source: game-architecture.md#Configuration Management]
- `StaminaSystem.cs` script location: [Source: game-architecture.md#System Location Mapping]
- Stamina mechanic design: [Source: gdd.md#Combat (Real-Time, Gothic-Style)]
- Epic 2 scope and stamina story: [Source: epics.md#Epic 2: Combat System]
- Debug overlay architecture: [Source: game-architecture.md#Debug & Development Tools]
- Null-guard Awake error pattern: [Source: game-architecture.md#Error Handling]
- OnDisable null guard: [Source: CLAUDE.md#Unity Lifecycle Gotcha: OnDisable Before OnEnable]
- `GameLog` mandatory usage: [Source: project-context.md#Logging — MANDATORY]
- No magic numbers in game logic: [Source: project-context.md#Config & Data Anti-Patterns]
- Edit Mode test scope: [Source: project-context.md#Testing Rules]
- `Game.asmdef` reference: [Source: CLAUDE.md#Assembly Setup (As of Story 1.5)]
- Cross-system vs same-system comms: [Source: game-architecture.md#Standard Patterns — Component Communication]
- Directional attack config fields: [Source: game-architecture.md#Novel Pattern 1: Directional Attack Pattern]
- Previous story learnings: [Source: _bmad-output/implementation-artifacts/1-5-unity-project-bmad-structure.md#Dev Agent Record]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- No blockers encountered. All tasks completed in single session.

### Completion Notes List

- Created `CombatConfigSO.cs` with all required stamina parameters plus directional attack config fields for story 2.3.
- Created `StaminaSystem.cs` with `Consume()`, `HasEnough()`, `CurrentStamina`, `MaxStamina`, auto-regen, `OnGUI` debug overlay.
- Created `CombatConfig.asset` with defaults: pool=100, attack=20, block=15, dodge=25, regen_rate=20, regen_delay=1.5.
- Added `StaminaSystem` component to `Player.prefab` root, `_config` wired to `CombatConfig.asset` (verified in YAML).
- Debug display confirmed: "Stamina: 100 / 100" visible at top-left in Play Mode (Editor build).
- 4/4 Edit Mode tests pass. Zero console errors in Play Mode. All AC 1–9 satisfied.

### File List

- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` (new)
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs.meta` (new — Unity auto-generated)
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs` (new)
- `Assets/_Game/Scripts/Combat/StaminaSystem.cs.meta` (new — Unity auto-generated)
- `Assets/_Game/Data/Config/CombatConfig.asset` (new)
- `Assets/_Game/Data/Config/CombatConfig.asset.meta` (new — Unity auto-generated)
- `Assets/Tests/EditMode/StaminaSystemTests.cs` (new)
- `Assets/Tests/EditMode/StaminaSystemTests.cs.meta` (new — Unity auto-generated)
- `Assets/_Game/Prefabs/Player/Player.prefab` (modified — StaminaSystem component added)
- `_bmad-output/implementation-artifacts/2-1-stamina-bar-system.md` (this file)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (status updated)

## Change Log

- 2026-03-04: Story 2.1 implemented — CombatConfigSO, StaminaSystem, CombatConfig.asset created; StaminaSystem added to Player prefab; 4/4 Edit Mode tests passing.
- 2026-03-04: Code review fixes — added null guard to `StaminaSystem.Update()` for consistency with `MaxStamina` property; rewrote `StaminaSystemTests` to call actual `StaminaSystem.Consume()` API via reflection-injected MonoBehaviour instance (tests 1–3 now catch real bugs); added missing `.meta` files to File List.
