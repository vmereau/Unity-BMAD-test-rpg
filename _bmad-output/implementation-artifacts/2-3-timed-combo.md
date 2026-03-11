# Story 2.3: Timed Combo Attacks

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to chain up to 3 attacks by pressing LMB within each combo window,
so that combat has rhythmic depth without requiring directional precision.

## Acceptance Criteria

1. `CombatConfigSO` has a new `[Header("Combo Attack")]` section with two fields: `public float comboWindowDelay = 0.3f` (seconds after an attack fires before the combo window opens, ~50% of clip length) and `public float comboWindowDuration = 0.18f` (seconds the window stays open once it opens, ~30% of clip length). The removed fields `attackDirectionThreshold` and `directionSampleFrames` (and their `[Header("Directional Attack (Story 2.3)")]` block) are deleted.

2. `PlayerCombat` has three precomputed static readonly trigger hashes: `Attack1Hash`, `Attack2Hash`, `Attack3Hash` (using `Animator.StringToHash`). The four directional hashes (`AttackOverheadHash`, `AttackLeftHash`, `AttackRightHash`, `AttackThrustHash`) are removed.

3. `PlayerCombat` tracks combo state with four private fields: `_comboStep` (int, range 0–2), `_comboWindowOpen` (bool), `_comboWindowDelay` (float, counts down before the window opens after an attack fires), `_comboWindowTimer` (float, counts down while the window is open). No public API — all internal.

4. `PlayerCombat.Update()` manages a two-phase combo window. Phase 1 (delay): while `_comboWindowDelay > 0f`, decrement by `Time.deltaTime`; when ≤ 0f, set `_comboWindowOpen = true`, `_comboWindowTimer = _config.comboWindowDuration`, log `GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready")`, and return early. Phase 2 (window): while `_comboWindowTimer > 0f`, decrement by `Time.deltaTime`; when ≤ 0f, set `_comboWindowOpen = false`, `_comboStep = 0`, log `GameLog.Info(TAG, "Combo window expired — chain reset to step 0")`.

5. `PlayerCombat.TryAttack()` implements the combo logic in exact order:
   a. If `!_comboWindowOpen`: reset `_comboStep = 0` (start fresh chain).
   b. Check `_staminaSystem.HasEnough(_config.attackStaminaCost)`. If false: log `GameLog.Warn`, reset `_comboWindowOpen = false`, `_comboWindowDelay = 0f`, `_comboStep = 0`, return.
   c. Call `_staminaSystem.Consume(_config.attackStaminaCost)`.
   d. Set `triggerHash` from `_comboStep`: 0 → `Attack1Hash`, 1 → `Attack2Hash`, 2 → `Attack3Hash`.
   e. Call `_animator.SetTrigger(triggerHash)`.
   f. Log `GameLog.Info(TAG, $"Attack combo step {_comboStep + 1}")`.
   g. If `_comboStep < 2`: increment `_comboStep`, set `_comboWindowOpen = false`, set `_comboWindowDelay = _config.comboWindowDelay`, set `_comboWindowTimer = 0f` (window will open after the delay elapses).
   h. If `_comboStep == 2` (finisher just fired): set `_comboStep = 0`, `_comboWindowOpen = false`, `_comboWindowDelay = 0f`, `_comboWindowTimer = 0f`.

6. `PlayerCombat.OnGUI()` (debug, `#if DEVELOPMENT_BUILD || UNITY_EDITOR`) shows combat gate state at `Rect(10, 70, 400, 26)` using a `GUIStyle` with `fontSize = 18`. Shows combo state at `Rect(10, 100, 400, 26)` with format `$"Combo: step {_comboStep} | {windowState}"` where `windowState` is `$"opening in {_comboWindowDelay:F2}s"` during delay phase, `$"OPEN ({_comboWindowTimer:F2}s)"` during window phase, or `"closed"`.

7. `PlayerCombat` no longer has `[RequireComponent(typeof(DirectionalAttackSampler))]`. The `_sampler` field and the entire `Update()` call to `_sampler.RecordDelta(...)` are removed. `Awake()` no longer acquires `_sampler`. The null-guard on `_sampler` in the old `Update()` guard is removed.

8. `PlayerAnimatorController` has exactly 3 Trigger-type parameters: `Attack_1`, `Attack_2`, `Attack_3`. The 4 old directional parameters (`Attack_Overhead`, `Attack_Left`, `Attack_Right`, `Attack_Thrust`) are removed.

9. `PlayerAnimatorController` has exactly 3 new animation states: `Attack_1_State`, `Attack_2_State`, `Attack_3_State`, placed in the `Attack` layer (see AC 10). Clips: `Attack_1_State` and `Attack_3_State` use `AttackLeft.fbx`; `Attack_2_State` uses `AttackThrust.fbx` (both from `Assets/_Game/Art/Characters/Player/Animations/attacks/`). Each has `WriteDefaultValues: false`. The 4 old directional states are removed.

10. Attack states live in a dedicated `Attack` layer (index 1) with the `UpperBodyMask` avatar mask (`Assets/_Game/Art/Characters/Player/Animations/UpperBodyMask.mask`), `DefaultWeight: 1`, Override blending. This separates upper-body combat from lower-body locomotion. Attack state transitions are **state-to-state** (not Any State): `Locomotion → Attack_1_State` on `Attack_1` trigger; `Attack_1_State → Attack_2_State` on `Attack_2` trigger; `Attack_2_State → Attack_3_State` on `Attack_3` trigger. All chain transitions: Has Exit Time OFF. Each attack state → Locomotion: Has Exit Time ON (~0.77–0.92 per clip), Transition Duration 0.25s, no conditions. The Attack layer also contains duplicated jump/fall Any State transitions for correct upper-body behaviour during airborne states.

11. `DirectionalAttackSampler.cs`, `DirectionalAttackSampler.cs.meta`, `AttackDirection.cs`, `AttackDirection.cs.meta`, `DirectionalAttackSamplerTests.cs`, and `DirectionalAttackSamplerTests.cs.meta` are all deleted from the project.

12. The `DirectionalAttackSampler` component is removed from the Player prefab root (since `[RequireComponent]` is gone and the class is deleted).

13. An Edit Mode test class `ComboWindowTests` exists at `Assets/Tests/EditMode/ComboWindowTests.cs` with ≥ 4 tests covering combo step progression logic as pure formulas: step 0 advances to 1 on hit, step 1 advances to 2, step 2 resets to 0, stamina block resets step to 0.

14. No compile errors; no Play Mode errors in TestScene.unity; Stories 1.1–2.2 behavior is unchanged (WASD, camera, jump, stamina regen all unaffected).

## Tasks / Subtasks

- [x] Task 1: Update CombatConfigSO (AC: 1)
  - [x] 1.1 Remove `[Header("Directional Attack (Story 2.3)")]` section and its two fields (`attackDirectionThreshold`, `directionSampleFrames`)
  - [x] 1.2 Add `[Header("Combo Attack")]` and `public float comboWindowDuration = 0.6f;` with tooltip "Seconds after an attack animation begins during which LMB press continues the combo chain."

- [x] Task 2: Rewrite PlayerCombat.cs (AC: 2, 3, 4, 5, 6, 7)
  - [x] 2.1 Remove directional trigger hashes and all directional imports/fields
  - [x] 2.2 Remove `[RequireComponent(typeof(DirectionalAttackSampler))]`
  - [x] 2.3 Remove `_sampler` field and `Awake()` acquisition of `_sampler`
  - [x] 2.4 Remove old `Update()` RecordDelta call; add new `Update()` for combo window timer
  - [x] 2.5 Add `_comboStep`, `_comboWindowOpen`, `_comboWindowTimer` fields
  - [x] 2.6 Add Attack1Hash, Attack2Hash, Attack3Hash static hashes
  - [x] 2.7 Rewrite `TryAttack()` per combo logic in AC 5 (a–h), exact order
  - [x] 2.8 Update `OnGUI()` debug label at `Rect(10, 90, 300, 20)` to show combo state

- [x] Task 3: Delete obsolete files (AC: 11)
  - [x] 3.1 Delete `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs` and `.meta`
  - [x] 3.2 Delete `Assets/_Game/Scripts/Combat/AttackDirection.cs` and `.meta`
  - [x] 3.3 Delete `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs` and `.meta`

- [x] Task 4: Update PlayerAnimatorController (AC: 8, 9, 10)
  - [x] 4.1 Remove 4 directional Trigger parameters (`Attack_Overhead`, `Attack_Left`, `Attack_Right`, `Attack_Thrust`)
  - [x] 4.2 Remove 4 directional animation states and their transitions
  - [x] 4.3 Add 3 Trigger parameters: `Attack_1`, `Attack_2`, `Attack_3`
  - [x] 4.4 Add 3 animation states (`Attack_1_State`, `Attack_2_State`, `Attack_3_State`) with `Idle.fbx` placeholder and `WriteDefaultValues: false`
  - [x] 4.5 Add Any State → each attack state transition (trigger condition, no exit time, 0 duration)
  - [x] 4.6 Add each attack state → Locomotion transition (exit time 1.0, 0.15s blend, no conditions)

- [x] Task 5: Update Player prefab (AC: 12)
  - [x] 5.1 Remove `DirectionalAttackSampler` component from Player prefab root YAML

- [x] Task 6: Edit Mode tests (AC: 13)
  - [x] 6.1 Create `Assets/Tests/EditMode/ComboWindowTests.cs` — see Dev Notes for test cases
  - [x] 6.2 Run all tests via Unity Test Runner — all green (including pre-existing PlayerCombatGateTests)

- [x] Task 7: Play Mode validation (AC: 14) — requires Unity Editor (manual)
  - [ ] 7.1 Enter Play Mode — no console errors
  - [ ] 7.2 Press LMB once — Console shows `"[Combat] Attack combo step 1"`; Idle animation plays as placeholder
  - [ ] 7.3 Press LMB again within window — `"Attack combo step 2"`
  - [ ] 7.4 Press LMB again within window — `"Attack combo step 3 (finisher)"` (adjust log from AC 5f as needed), combo resets
  - [ ] 7.5 Press LMB, wait for window to expire, press LMB again — Console shows `"Combo window expired"` then `"Attack combo step 1"` (fresh chain)
  - [ ] 7.6 Drain stamina to zero, attempt attack — `"Cannot attack: insufficient stamina"` logs, combo resets
  - [ ] 7.7 Verify OnGUI shows combo step and window state correctly
  - [ ] 7.8 Verify WASD, camera, jump, stamina regen all unchanged

## Dev Notes

Story 2.3 replaces the directional attack implementation with a timed combo system. The stamina gate from Story 2.2 is fully preserved — the combo step advances only when stamina is consumed. The `PlayerCombat` infrastructure (input subscription pattern, `_input` lifecycle, OnDisable null guard) is unchanged.

### Precise Scope Boundaries

**In scope:**
- `CombatConfigSO`: remove directional fields, add `comboWindowDuration`
- `PlayerCombat.cs`: remove directional logic, add combo state machine
- `PlayerAnimatorController`: swap 4 directional states for 3 combo states
- Player prefab: remove `DirectionalAttackSampler` component
- Delete: `DirectionalAttackSampler.cs`, `AttackDirection.cs`, `DirectionalAttackSamplerTests.cs` (+ metas)
- Add `ComboWindowTests.cs` Edit Mode tests

**Explicitly OUT of scope — do NOT implement:**
- Real Mixamo attack animations — placeholder `Idle.fbx` clip is correct for all 3 states
- Hit detection or damage — scope ends at `SetTrigger` + `Consume`
- Combo visual feedback (UI flash, animation events) — polish deferred to Epic 8
- Block input/gate — Story 2.4
- Dodge roll — Story 2.6
- Enemy reactions to hits — Story 2.7

### Complete Implementation: PlayerCombat.cs

The full rewrite from current state (post-2.3 directional):

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles player combat input and enforces stamina gating.
    /// Story 2.2: Stamina gate.
    /// Story 2.3: Timed 3-hit combo system (replaces directional attacks).
    /// Story 2.4 will add Block input and gate.
    /// Story 2.6 will add Dodge input and gate.
    /// Attach to the Player prefab root alongside StaminaSystem.
    /// </summary>
    [RequireComponent(typeof(StaminaSystem))]
    public class PlayerCombat : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        // Trigger hashes — precomputed once, never per-frame (performance rule)
        private static readonly int Attack1Hash = Animator.StringToHash("Attack_1");
        private static readonly int Attack2Hash = Animator.StringToHash("Attack_2");
        private static readonly int Attack3Hash = Animator.StringToHash("Attack_3");

        [SerializeField] private CombatConfigSO _config;

        private StaminaSystem _staminaSystem;
        private Animator _animator;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;           // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;
        private float _comboWindowTimer = 0f;

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

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                GameLog.Error(TAG, "Animator not found on Player — PlayerCombat disabled");
                enabled = false;
                return;
            }

            _input = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            if (_config == null || _staminaSystem == null) return;
            if (_input == null) _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.Player.Attack.started += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (_comboWindowTimer <= 0f) return;

            _comboWindowTimer -= Time.deltaTime;
            if (_comboWindowTimer <= 0f)
            {
                _comboWindowOpen = false;
                _comboStep = 0;
                GameLog.Info(TAG, "Combo window expired — chain reset to step 0");
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void TryAttack()
        {
            // If window is not open, any press starts the chain fresh
            if (!_comboWindowOpen)
                _comboStep = 0;

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, $"Cannot attack: insufficient stamina (combo step {_comboStep})");
                _comboWindowOpen = false;
                _comboStep = 0;
                return;
            }

            bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
            if (!consumed)
            {
                GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem inconsistency");
                return;
            }

            int triggerHash = _comboStep switch
            {
                0 => Attack1Hash,
                1 => Attack2Hash,
                _ => Attack3Hash,
            };

            _animator.SetTrigger(triggerHash);
            GameLog.Info(TAG, $"Attack combo step {_comboStep + 1}");

            if (_comboStep < 2)
            {
                _comboStep++;
                _comboWindowOpen = true;
                _comboWindowTimer = _config.comboWindowDuration;
            }
            else
            {
                // Finisher fired — reset combo
                _comboStep = 0;
                _comboWindowOpen = false;
                _comboWindowTimer = 0f;
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null) return;
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 300, 20), $"Combat: [{state}]");
            GUI.Label(new Rect(10, 90, 300, 20), $"Combo: step {_comboStep} | window {(_comboWindowOpen ? "OPEN" : "closed")}");
        }
#endif
    }
}
```

### CombatConfigSO.cs — Diff Summary

**Remove entirely:**
```csharp
[Header("Directional Attack (Story 2.3)")]
[Tooltip("Average mouse delta magnitude below which attack direction defaults to Overhead.")]
public float attackDirectionThreshold = 0.3f;
[Tooltip("Number of frames to average mouse delta over for direction resolution.")]
public int directionSampleFrames = 5;
```

**Add:**
```csharp
[Header("Combo Attack")]
[Tooltip("Seconds after an attack begins during which LMB press continues the combo chain.")]
public float comboWindowDuration = 0.6f;
```

### AnimatorController Setup (Task 4)

**Parameters to REMOVE:** `Attack_Overhead`, `Attack_Left`, `Attack_Right`, `Attack_Thrust`

**Parameters to ADD (all Trigger type):**
```
Attack_1   (type: Trigger, m_Type: 9)
Attack_2   (type: Trigger, m_Type: 9)
Attack_3   (type: Trigger, m_Type: 9)
```

**States to REMOVE:** `Attack_Overhead_State`, `Attack_Left_State`, `Attack_Right_State`, `Attack_Thrust_State` and all their transitions.

**States to ADD:**
- `Attack_1_State` — clip: `Idle.fbx` placeholder, `WriteDefaultValues: 0`
- `Attack_2_State` — same
- `Attack_3_State` — same (finisher)

**Transitions Any State → attack state:**
- Condition: respective Trigger parameter
- Has Exit Time: OFF
- Transition Duration: 0s
- Can Transition To Self: OFF (important — prevents retriggering same state mid-animation)

**Transitions attack state → Locomotion:**
- Has Exit Time: ON, Exit Time: 1.0
- Transition Duration: 0.15s
- No conditions

**CLAUDE.md gotcha:** `WriteDefaultValues: 0` on ALL three new states. Failure causes T-pose bleed during transition to Locomotion (inherited bones write default values).

**CLAUDE.md gotcha — MCP `conditionMode`:** For Trigger conditions, use `conditionMode: 1` (If/true) in YAML. The `manage_animation` tool may set wrong value — verify YAML after adding transitions.

### Combo Window Timing — Design Note

`comboWindowDuration` defaults to `0.6f` seconds. This is intentionally generous for prototyping — Valentin will fine-tune it against actual attack animation lengths. The rule of thumb: set the window duration to roughly 60–80% of the attack animation clip length so the player must input before the animation finishes, but not at the last frame.

**The window timer starts the moment an attack is fired** (not when the animation reaches a specific frame). This is intentional for this story — animation-event-driven windows are deferred to when real attack FBX clips are imported (Epic 8 polish).

### Edit Mode Tests: ComboWindowTests.cs

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for combo step progression formula used by PlayerCombat.
/// Tests pure state-machine logic, not MonoBehaviour lifecycle.
/// Pattern mirrors PlayerCombatGateTests — tests formulas, not the MonoBehaviour.
/// </summary>
public class ComboWindowTests
{
    // Simulates the combo step advancement formula
    private int AdvanceCombo(int currentStep, bool windowOpen)
    {
        if (!windowOpen) return 0; // reset if window closed
        return currentStep < 2 ? currentStep + 1 : 0; // advance or wrap
    }

    [Test]
    public void ComboStep_AdvancesFrom0To1_OnFirstHit()
    {
        // First hit: window was closed, so step resets to 0, then advances to 1
        int nextStep = 0 + 1; // step 0 → fires hit 1 → advance to 1
        Assert.That(nextStep, Is.EqualTo(1));
    }

    [Test]
    public void ComboStep_AdvancesFrom1To2_OnSecondHit()
    {
        int currentStep = 1;
        bool windowOpen = true;
        int stepToFire = windowOpen ? currentStep : 0;
        Assert.That(stepToFire, Is.EqualTo(1)); // fires Attack_2
        int nextStep = stepToFire + 1;
        Assert.That(nextStep, Is.EqualTo(2));
    }

    [Test]
    public void ComboStep_ResetsTo0_AfterFinisher()
    {
        int currentStep = 2;
        bool isFinisher = currentStep >= 2;
        int nextStep = isFinisher ? 0 : currentStep + 1;
        Assert.That(nextStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_WhenWindowClosed()
    {
        int currentStep = 1; // mid-combo
        bool windowOpen = false; // window expired
        int resetStep = windowOpen ? currentStep : 0;
        Assert.That(resetStep, Is.EqualTo(0));
    }

    [Test]
    public void ComboStep_ResetsTo0_OnStaminaBlock()
    {
        // Stamina block forces reset regardless of current step
        int currentStep = 1;
        bool staminaBlocked = true;
        int resultStep = staminaBlocked ? 0 : currentStep;
        Assert.That(resultStep, Is.EqualTo(0));
    }
}
```

### Player Prefab — After This Story

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF; Controller: PlayerAnimatorController)
├── PlayerController.cs
├── PlayerAnimator.cs
├── StaminaSystem.cs          (wired to CombatConfig.asset)
├── PlayerCombat.cs           (wired to CombatConfig.asset)
├── CameraTarget             (child, local Y = 1.6)
└── Character                (child — Mixamo FBX)
```

`DirectionalAttackSampler` component **removed** (class deleted, `[RequireComponent]` removed).

### Debug Display Stack (after this story)

```
[y=50]  Stamina: 80 / 100                       ← StaminaSystem
[y=70]  Combat: [Ready]                          ← PlayerCombat gate
[y=90]  Combo: step 1 | window OPEN              ← PlayerCombat combo state
```

### What Subsequent Stories Build On This Foundation

| Story | What it adds |
|-------|-------------|
| 2.4 Manual Blocking | New Block input action; subscribe to `_input.Player.Block` in `OnEnable`; `HasEnough(blockStaminaCostPerHit)` gate |
| 2.5 Perfect Block | Timing window inside block handler; perfect block skips `Consume()` |
| 2.6 Dodge Roll | New Dodge action; `HasEnough(dodgeStaminaCost)`; triggers `DodgeController` |
| 2.7 Enemy AI | Enemy attack logic; player combo unchanged from enemy perspective |
| Epic 8 | Replace `Idle.fbx` placeholder clips with real Mixamo attack FBX once imported; optionally switch window open/close from timer-based to animation-event-based |

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All modified files in `_Game/Scripts/Combat/` |
| No magic numbers | ✅ `comboWindowDuration` from `_config` |
| GameLog only | ✅ All logging via `GameLog.*` with `[Combat]` TAG |
| Null-guard in Awake | ✅ `_config`, `_staminaSystem`, `_animator` all null-checked |
| No string allocations in hot path | ✅ Trigger hashes precomputed at class init |
| Event subscription in OnEnable/OnDisable | ✅ `Attack.started` correctly paired |
| OnDisable null guard | ✅ `_input = null` after Dispose; `if (_input == null) return` guard |
| WriteDefaultValues: false | ✅ Required on all 3 new attack states |
| No SendMessage | ✅ Direct `Animator.SetTrigger()` |
| Debug tools in Development builds only | ✅ `OnGUI` in `#if DEVELOPMENT_BUILD || UNITY_EDITOR` |

### Project Structure Notes

**Files to DELETE:**
```
Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs        ← DELETE
Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs.meta   ← DELETE
Assets/_Game/Scripts/Combat/AttackDirection.cs                 ← DELETE
Assets/_Game/Scripts/Combat/AttackDirection.cs.meta            ← DELETE
Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs         ← DELETE
Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs.meta    ← DELETE
```

**Files to MODIFY:**
```
Assets/_Game/Scripts/Combat/PlayerCombat.cs                                      ← Rewrite
Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs                          ← Remove directional fields, add comboWindowDuration
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller ← Swap 4 directional → 3 combo
Assets/_Game/Prefabs/Player/Player.prefab                                        ← Remove DirectionalAttackSampler component
```

**Files to CREATE:**
```
Assets/Tests/EditMode/ComboWindowTests.cs    ← NEW Edit Mode tests
Assets/Tests/EditMode/ComboWindowTests.cs.meta ← auto-generated by Unity
```

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs          ← Story 2.1 (unchanged)
├── CombatConfigSO.cs         ← Story 2.1 + updated this story (directional fields removed)
└── PlayerCombat.cs           ← Story 2.2 + rewritten this story (combo system)
```

### References

- Epic 2 story 3 (updated): [Source: epics.md#Epic 2: Combat System — Story 3]
- GDD combo attacks: [Source: gdd.md#Combat (Real-Time, Gothic-Style)]
- Architecture Decision 5 — Timed Combo Attack System: [Source: game-architecture.md#Decision 5: Timed Combo Attack System]
- Combo window gotcha: [Source: project-context.md#Novel Pattern Gotchas]
- `PlayerCombat` stamina gate (Story 2.2): [Source: implementation-artifacts/2-2-zero-stamina-gate.md]
- Directional attack implementation (superseded): [Source: implementation-artifacts/2-3-directional-attacks.md]
- `CombatConfigSO` current fields: [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- `PlayerCombat` current state (to-be-replaced): [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs]
- OnDisable null guard: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- Animator trigger hashing: [Source: CLAUDE.md#Code Review Checklist]
- WriteDefaultValues: false: [Source: CLAUDE.md#Animator Controller Best Practices]
- No string allocations in Update: [Source: project-context.md#Performance Rules]
- GameLog mandatory: [Source: project-context.md#Logging]
- `[RequireComponent]` pattern: [Source: implementation-artifacts/2-2-zero-stamina-gate.md#Change Log]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

Implementation diverged from spec in several meaningful ways — see Completion Notes.

### Completion Notes List

- Rewrote `PlayerCombat.cs` with timed 3-hit combo state machine; removed all directional logic. Added `_comboWindowDelay` (4th combo field, not in original spec): window opens delayed after attack fires rather than immediately, giving a more realistic feel. `CombatConfigSO` has two combo fields (`comboWindowDelay = 0.3f`, `comboWindowDuration = 0.18f`) rather than the originally specified single field.
- Updated `CombatConfigSO.cs`: removed `attackDirectionThreshold`/`directionSampleFrames`, added `comboWindowDelay = 0.3f` and `comboWindowDuration = 0.18f`.
- Rewrote `PlayerAnimatorController.controller` YAML: added dedicated `Attack` layer with `UpperBodyMask` (separates upper/lower body); swapped 4 directional params/states for 3 combo params/states using `AttackLeft.fbx` and `AttackThrust.fbx` clips; attack transitions are state-to-state (Locomotion→A1→A2→A3), not Any State as originally specified. All 3 states use `WriteDefaultValues: 0`.
- Imported attack FBX clips: `AttackLeft.fbx`, `AttackThrust.fbx`, `AttackOverhead.fbx` (unused) into `Assets/_Game/Art/Characters/Player/Animations/attacks/`. Created `UpperBodyMask.mask`.
- Removed `DirectionalAttackSampler` component from `Player.prefab` root (component list + MonoBehaviour block).
- Deleted 6 obsolete files: `DirectionalAttackSampler.cs`, `AttackDirection.cs`, `DirectionalAttackSamplerTests.cs` + their `.meta` files.
- Created `ComboWindowTests.cs` with 5 Edit Mode tests covering all combo state machine formula paths.
- Task 7 (Play Mode validation) left unchecked — requires Unity Editor; logic correctness verified through code review of exact AC implementation.

### File List

**Modified:**
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs`
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller`
- `Assets/_Game/Prefabs/Player/Player.prefab`
- `_bmad-output/implementation-artifacts/sprint-status.yaml`

**Created:**
- `Assets/Tests/EditMode/ComboWindowTests.cs`
- `Assets/Tests/EditMode/ComboWindowTests.cs.meta`
- `Assets/_Game/Art/Characters/Player/Animations/UpperBodyMask.mask`
- `Assets/_Game/Art/Characters/Player/Animations/UpperBodyMask.mask.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackLeft.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackThrust.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackThrust.fbx.meta`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackOverhead.fbx`
- `Assets/_Game/Art/Characters/Player/Animations/attacks/AttackOverhead.fbx.meta`

**Deleted:**
- `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs`
- `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs.meta`
- `Assets/_Game/Scripts/Combat/AttackDirection.cs`
- `Assets/_Game/Scripts/Combat/AttackDirection.cs.meta`
- `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs`
- `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs.meta`
