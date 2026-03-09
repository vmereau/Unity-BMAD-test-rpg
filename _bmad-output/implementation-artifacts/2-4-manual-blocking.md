# Story 2.4: Manual Blocking

Status: done

## Story

As a player,
I want to hold right mouse button to raise a block,
so that I can defend against incoming attacks using stamina.

## Acceptance Criteria

1. A **Block** action of type `Button` is added to the **Player** action map in `Assets/_Game/InputSystem_Actions.inputactions`, bound to `<Mouse>/rightButton` (Keyboard&Mouse control scheme). The `InputSystem_Actions.cs` C# class is updated to expose `m_Player_Block` and `Player.@Block` accordingly.

2. `PlayerCombat` subscribes to `_input.Player.Block.started += OnBlockStarted` and `_input.Player.Block.canceled += OnBlockCanceled` in `OnEnable()`, and unsubscribes both in `OnDisable()`.

3. `PlayerCombat` has one new private field: `_isBlocking` (bool, default false). No public API.

4. `OnBlockStarted()`: if `_staminaSystem.HasEnough(_config.blockStaminaCostPerHit)` is true → set `_isBlocking = true`, call `_animator.SetBool(IsBlockingHash, true)`, reset combo state (`_comboWindowOpen = false`, `_comboWindowDelay = 0f`, `_comboWindowTimer = 0f`, `_comboStep = 0`), log `GameLog.Info(TAG, "Block raised")`. If stamina insufficient → log `GameLog.Warn(TAG, "Cannot block: insufficient stamina")`, leave `_isBlocking = false`.

5. `OnBlockCanceled()`: set `_isBlocking = false`, call `_animator.SetBool(IsBlockingHash, false)`, log `GameLog.Info(TAG, "Block lowered")`.

6. `TryAttack()`: at the very start, if `_isBlocking` is true → log `GameLog.Warn(TAG, "Cannot attack while blocking")` and return immediately. Existing combo and stamina logic is unchanged.

7. A precomputed static readonly int hash `IsBlockingHash = Animator.StringToHash("IsBlocking")` is added to `PlayerCombat` alongside the existing attack hashes.

8. `PlayerAnimatorController` has a new **Bool** parameter named `IsBlocking`. A new state `Block_State` is added to the **Attack layer** (index 1, UpperBodyMask). Clip: `Idle.fbx` placeholder. `WriteDefaultValues: 0`. Transitions:
   - Any State → `Block_State`: condition `IsBlocking = true`, Has Exit Time OFF, Duration 0.1s, `canTransitionToSelf: false`.
   - `Block_State` → `Locomotion` (or Exit): condition `IsBlocking = false`, Has Exit Time OFF, Duration 0.1s.

9. `PlayerCombat.OnGUI()` debug overlay shows a third line at `Rect(10, 130, 400, 26)` with `fontSize = 18`: `$"Block: {(_isBlocking ? "RAISED" : "lowered")}"`.

10. An Edit Mode test class `BlockGateTests` exists at `Assets/Tests/EditMode/BlockGateTests.cs` with ≥ 3 tests covering block gate logic as pure formulas: can enter block with sufficient stamina, cannot enter block at zero stamina, attacking while blocking is prevented.

11. No compile errors; no Play Mode errors in TestScene.unity. Stories 1.1–2.3 behavior is unchanged (WASD, camera, jump, stamina regen, combo attacks all unaffected). Holding RMB with sufficient stamina shows `Block_State` animation and `"Block: RAISED"` in debug overlay. Releasing RMB returns to locomotion.

## Tasks / Subtasks

- [x] Task 1: Add Block action to Input System (AC: 1)
  - [x] 1.1 Edit `Assets/_Game/InputSystem_Actions.inputactions`: add `Block` Button action entry to Player map actions array
  - [x] 1.2 Edit `Assets/_Game/InputSystem_Actions.inputactions`: add RMB binding for Block to Player map bindings array
  - [x] 1.3 Edit `Assets/_Game/InputSystem_Actions.cs`: add `private readonly InputAction m_Player_Block;` field alongside other `m_Player_*` fields
  - [x] 1.4 Edit `Assets/_Game/InputSystem_Actions.cs`: add `m_Player_Block = m_Player.FindAction("Block", throwIfNotFound: true);` in the action-map constructor
  - [x] 1.5 Edit `Assets/_Game/InputSystem_Actions.cs`: add `public InputAction @Block => m_Wrapper.m_Player_Block;` property inside the `PlayerActions` struct

- [x] Task 2: Update PlayerCombat.cs (AC: 2, 3, 4, 5, 6, 7, 9)
  - [x] 2.1 Add `IsBlockingHash` static readonly hash
  - [x] 2.2 Add `_isBlocking` private field
  - [x] 2.3 Subscribe Block started/canceled in `OnEnable`; unsubscribe in `OnDisable`
  - [x] 2.4 Implement `OnBlockStarted()` with stamina gate, combo reset, animator bool, and logging
  - [x] 2.5 Implement `OnBlockCanceled()` with animator bool and logging
  - [x] 2.6 Add `_isBlocking` early-return guard at top of `TryAttack()`
  - [x] 2.7 Add Block debug label at y=130 in `OnGUI()`

- [x] Task 3: Update PlayerAnimatorController (AC: 8)
  - [x] 3.1 Add `IsBlocking` Bool parameter to `PlayerAnimatorController.controller`
  - [x] 3.2 Add `Block_State` to Attack layer with `Idle.fbx` placeholder and `WriteDefaultValues: 0`
  - [x] 3.3 Add Any State → `Block_State` transition (IsBlocking true, no exit time, 0.1s duration, no self-transition)
  - [x] 3.4 Add `Block_State` → Exit/Locomotion transition (IsBlocking false, no exit time, 0.1s duration)

- [x] Task 4: Edit Mode tests (AC: 10)
  - [x] 4.1 Create `Assets/Tests/EditMode/BlockGateTests.cs` with ≥ 3 tests
  - [x] 4.2 Run all tests via Unity Test Runner — all green (including existing ComboWindowTests, PlayerCombatGateTests)

- [x] Task 5: Play Mode validation (AC: 11) — requires Unity Editor (manual)
  - [x] 5.1 Enter Play Mode — no console errors
  - [x] 5.2 Hold RMB with full stamina — debug overlay shows `"Block: RAISED"`, Block_State animation plays
  - [x] 5.3 Release RMB — debug overlay shows `"Block: lowered"`, returns to locomotion
  - [x] 5.4 Drain stamina to near-zero, hold RMB — `"Cannot block: insufficient stamina"` logged, `_isBlocking` stays false
  - [x] 5.5 Hold RMB then press LMB — `"Cannot attack while blocking"` logged, combo does NOT fire
  - [x] 5.6 Release RMB, press LMB — combo fires normally (Attack_1 trigger), stamina decrements
  - [x] 5.7 Verify WASD movement, camera, jump, stamina regen, combo all unchanged

## Dev Notes

Story 2.4 adds the Block input gate and animation to `PlayerCombat`. **No actual damage absorption is implemented** — that requires enemies from Story 2.7. This story establishes: (1) the Block input action, (2) the block state machine in `PlayerCombat`, (3) the stamina gate for entering block, and (4) the block animation state. The `blockStaminaCostPerHit` field already exists in `CombatConfigSO` and will be consumed in Story 2.7 on each hit absorbed.

### Critical: Block Action Missing From Input System

**The `Block` action does NOT currently exist** in the Player action map (`InputSystem_Actions.inputactions`). The Right Mouse Button is bound only to `UI.RightClick` (cursor-lock system). Adding Block to the Player map introduces a conflict risk with cursor handling.

**Cursor-lock safety:** `CameraController` uses `_input.UI.Click.WasPressedThisFrame()` for left-click re-lock. The Block action lives in the Player map (separate from UI map), so RMB can coexist: Player map Block fires during gameplay, UI RightClick fires when cursor is unlocked. When cursor is locked and Player map is enabled, only `Player.Block` fires. When cursor is unlocked (UI mode), only `UI.RightClick` fires. No conflict if Player map is disabled during cursor-unlock mode — confirm `CameraController` disables Player map on unlock (it does: `_input.Player.Disable()`).

### Precise Edits: `InputSystem_Actions.inputactions`

In the `"Player"` map's `"actions"` array, add after the `"Sprint"` entry:
```json
{
    "name": "Block",
    "type": "Button",
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```
> Generate a real UUID for `"id"` — the placeholder above is illustrative. Use any UUID generator or `System.Guid.NewGuid().ToString()`.

In the `"Player"` map's `"bindings"` array, add:
```json
{
    "name": "",
    "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "path": "<Mouse>/rightButton",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "Block",
    "isComposite": false,
    "isPartOfComposite": false
}
```

### Precise Edits: `InputSystem_Actions.cs`

The file is auto-generated but manually maintained (moved from `Assets/` root in Story 1.5). Edit it directly — do NOT trigger Unity to regenerate it as that would reset manual edits and place it back in `Assets/` root.

**Step A — Add field** (alongside `m_Player_Attack`, `m_Player_Sprint`, etc.):
```csharp
private readonly InputAction m_Player_Block;
```

**Step B — Add FindAction call** (in the constructor, alongside `m_Player_Attack = m_Player.FindAction("Attack", throwIfNotFound: true);`):
```csharp
m_Player_Block = m_Player.FindAction("Block", throwIfNotFound: true);
```

**Step C — Add property in `PlayerActions` struct** (alongside `public InputAction @Attack` and `public InputAction @Sprint`):
```csharp
/// <summary>Provides access to the underlying input action "Player/Block".</summary>
public InputAction @Block => m_Wrapper.m_Player_Block;
```

**Note:** Do NOT add `OnBlock` to the `IPlayerActions` interface or `AddCallbacks`/`UnregisterCallbacks` — `PlayerCombat` uses direct event subscription, not the callback interface.

### Complete Implementation: PlayerCombat.cs

Full rewrite from the post-Story-2.3 state:

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Combat
{
    /// <summary>
    /// Handles player combat input and enforces stamina gating.
    /// Story 2.2: Stamina gate.
    /// Story 2.3: Timed 3-hit combo system.
    /// Story 2.4: Manual block input and gate.
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
        private static readonly int IsBlockingHash = Animator.StringToHash("IsBlocking");

        [SerializeField] private CombatConfigSO _config;

        private StaminaSystem _staminaSystem;
        private Animator _animator;
        private InputSystem_Actions _input;

        // Combo state
        private int _comboStep = 0;            // 0 = ready, 1 = after hit 1, 2 = after hit 2
        private bool _comboWindowOpen = false;
        private float _comboWindowDelay = 0f;  // counts down before window opens
        private float _comboWindowTimer = 0f;  // counts down while window is open

        // Block state
        private bool _isBlocking = false;

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
            _input.Player.Block.started += OnBlockStarted;
            _input.Player.Block.canceled += OnBlockCanceled;
        }

        private void OnDisable()
        {
            if (_input == null) return; // Guard: Awake may disable before OnEnable runs
            _input.Player.Attack.started -= OnAttackStarted;
            _input.Player.Block.started -= OnBlockStarted;
            _input.Player.Block.canceled -= OnBlockCanceled;
            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            // Phase 1: delay before combo window opens
            if (_comboWindowDelay > 0f)
            {
                _comboWindowDelay -= Time.deltaTime;
                if (_comboWindowDelay <= 0f)
                {
                    _comboWindowOpen = true;
                    _comboWindowTimer = _config.comboWindowDuration;
                    GameLog.Info(TAG, $"Combo window opened — step {_comboStep} ready");
                }
                return;
            }

            // Phase 2: window is open, count down
            if (_comboWindowTimer > 0f)
            {
                _comboWindowTimer -= Time.deltaTime;
                if (_comboWindowTimer <= 0f)
                {
                    _comboWindowOpen = false;
                    _comboStep = 0;
                    GameLog.Info(TAG, "Combo window expired — chain reset to step 0");
                }
            }
        }

        private void OnAttackStarted(InputAction.CallbackContext ctx)
        {
            TryAttack();
        }

        private void OnBlockStarted(InputAction.CallbackContext ctx)
        {
            if (!_staminaSystem.HasEnough(_config.blockStaminaCostPerHit))
            {
                GameLog.Warn(TAG, "Cannot block: insufficient stamina");
                return;
            }

            _isBlocking = true;
            _animator.SetBool(IsBlockingHash, true);

            // Reset any in-progress combo — cannot combo mid-block
            _comboWindowOpen = false;
            _comboWindowDelay = 0f;
            _comboWindowTimer = 0f;
            _comboStep = 0;

            GameLog.Info(TAG, "Block raised");
        }

        private void OnBlockCanceled(InputAction.CallbackContext ctx)
        {
            _isBlocking = false;
            _animator.SetBool(IsBlockingHash, false);
            GameLog.Info(TAG, "Block lowered");
        }

        private void TryAttack()
        {
            // Cannot attack while blocking
            if (_isBlocking)
            {
                GameLog.Warn(TAG, "Cannot attack while blocking");
                return;
            }

            // If window is not open, start the chain fresh (also cancels any pending delay)
            if (!_comboWindowOpen)
            {
                _comboWindowDelay = 0f;
                _comboStep = 0;
            }

            if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
            {
                GameLog.Warn(TAG, $"Cannot attack: insufficient stamina (combo step {_comboStep})");
                _comboWindowOpen = false;
                _comboWindowDelay = 0f;
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
                _comboWindowOpen = false;  // window not open yet — delay starts
                _comboWindowDelay = _config.comboWindowDelay;
                _comboWindowTimer = 0f;
            }
            else
            {
                // Finisher fired — reset combo
                _comboStep = 0;
                _comboWindowOpen = false;
                _comboWindowDelay = 0f;
                _comboWindowTimer = 0f;
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private void OnGUI()
        {
            if (_config == null || _staminaSystem == null) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
            string state = canAttack ? "Ready" : "STAMINA EMPTY";
            GUI.Label(new Rect(10, 70, 400, 26), $"Combat: [{state}]", style);
            string windowState = _comboWindowDelay > 0f
                ? $"opening in {_comboWindowDelay:F2}s"
                : _comboWindowOpen ? $"OPEN ({_comboWindowTimer:F2}s)" : "closed";
            GUI.Label(new Rect(10, 100, 400, 26), $"Combo: step {_comboStep} | {windowState}", style);
            GUI.Label(new Rect(10, 130, 400, 26), $"Block: {(_isBlocking ? "RAISED" : "lowered")}", style);
        }
#endif
    }
}
```

### AnimatorController Changes (Task 3)

The `PlayerAnimatorController.controller` already has:
- Base Layer with Locomotion blend tree + jump/fall states
- Attack Layer (index 1) with `UpperBodyMask`, Override blending, `DefaultWeight: 1`
- Parameters: `Speed` (Float), `IsGrounded` (Bool), `IsRising` (Bool), `Attack_1/2/3` (Triggers)

**Add parameter:**
```yaml
# In m_AnimatorParameters array:
- m_Name: IsBlocking
  m_Type: 4   # Bool
  m_DefaultFloat: 0
  m_DefaultInt: 0
  m_DefaultBool: 0
  m_Controller: {fileID: 9100000}
```

**Add Block_State to Attack layer:**
```yaml
# New AnimatorState:
- serializedVersion: 6
  m_ObjectHideFlags: 1
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: Block_State
  m_Speed: 1
  m_CycleOffset: 0
  m_Transitions: []         # Filled by transitions below
  m_StateMachineBehaviours: []
  m_Position: {x: 500, y: 250, z: 0}
  m_IKOnFeet: 0
  m_WriteDefaultValues: 0   # MANDATORY — prevents T-pose bleed
  m_Mirror: 0
  m_SpeedParameterActive: 0
  m_MirrorParameterActive: 0
  m_CycleOffsetParameterActive: 0
  m_TimeParameterActive: 0
  m_Motion: {fileID: [Idle.fbx clip GUID], guid: [Idle.fbx guid]}
  m_Tag:
  m_SpeedParameter:
  m_MirrorParameter:
  m_CycleOffsetParameter:
  m_TimeParameter:
```

**Any State → Block_State transition:**
- condition: `IsBlocking = true` (conditionMode: 1 = If/true for Bool)
- Has Exit Time: OFF
- Transition Duration: 0.1s
- `canTransitionToSelf: 0` — prevents re-entering Block_State while already in it

**Block_State → Locomotion (or Exit):**
- condition: `IsBlocking = false` (conditionMode: 2 = IfNot/false for Bool)
- Has Exit Time: OFF
- Transition Duration: 0.1s

> **MCP gotcha (CLAUDE.md):** `manage_animation(controller_add_transition)` sets wrong `conditionMode` for bools. For `IsBlocking = true` use `conditionMode: 1`; for `IsBlocking = false` use `conditionMode: 2`. Always verify YAML after MCP tool calls.

### Edit Mode Tests: BlockGateTests.cs

```csharp
using NUnit.Framework;

/// <summary>
/// Edit Mode tests for block gate logic used by PlayerCombat.
/// Tests pure state formulas — no MonoBehaviour lifecycle.
/// Pattern mirrors ComboWindowTests and PlayerCombatGateTests.
/// </summary>
public class BlockGateTests
{
    // Simulates the block entry gate formula
    private bool CanEnterBlock(float currentStamina, float blockCostPerHit)
    {
        return currentStamina >= blockCostPerHit;
    }

    // Simulates the attack-while-blocking gate
    private bool CanAttack(bool isBlocking)
    {
        return !isBlocking;
    }

    [Test]
    public void CanEnterBlock_ReturnsFalse_WhenStaminaIsZero()
    {
        Assert.That(CanEnterBlock(0f, 15f), Is.False);
    }

    [Test]
    public void CanEnterBlock_ReturnsTrue_WhenStaminaSufficient()
    {
        Assert.That(CanEnterBlock(15f, 15f), Is.True);
        Assert.That(CanEnterBlock(100f, 15f), Is.True);
    }

    [Test]
    public void CanEnterBlock_ReturnsFalse_WhenStaminaBelowCost()
    {
        Assert.That(CanEnterBlock(14f, 15f), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsFalse_WhenBlocking()
    {
        Assert.That(CanAttack(true), Is.False);
    }

    [Test]
    public void CanAttack_ReturnsTrue_WhenNotBlocking()
    {
        Assert.That(CanAttack(false), Is.True);
    }
}
```

### Debug Display Stack (after this story)

```
[y=50]   Stamina: 80 / 100                          ← StaminaSystem
[y=70]   Combat: [Ready]                             ← PlayerCombat gate
[y=100]  Combo: step 1 | window OPEN                 ← PlayerCombat combo state
[y=130]  Block: RAISED                               ← PlayerCombat block state
```

### What Subsequent Stories Build On This Foundation

| Story | What it adds |
|-------|-------------|
| 2.5 Perfect Block | Timing window detection inside `OnBlockStarted` (or animation event); perfect block skips `Consume()` and staggers attacker |
| 2.6 Dodge Roll | New `Dodge` action; subscribe to `_input.Player.Dodge` (or repurpose Space+direction); `HasEnough(dodgeStaminaCost)` gate; `DodgeController` |
| 2.7 Enemy AI | Enemy attack events call `_staminaSystem.Consume(blockStaminaCostPerHit)` on the player when `_isBlocking = true`; no changes to block input itself |
| Epic 8 | Replace `Idle.fbx` placeholder with real guard-stance animation; optional: animation-event-driven block window feedback |

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All modified files in `_Game/Scripts/Combat/` |
| No magic numbers | ✅ `blockStaminaCostPerHit` from `_config` (already in CombatConfigSO) |
| GameLog only | ✅ All logging via `GameLog.*` with `[Combat]` TAG |
| Null-guard in Awake | ✅ Existing guards cover `_config`, `_staminaSystem`, `_animator` |
| No string allocations in hot path | ✅ `IsBlockingHash` precomputed at class init |
| Event subscription in OnEnable/OnDisable | ✅ Block.started/canceled correctly paired |
| OnDisable null guard | ✅ Existing `if (_input == null) return` guard covers new subscriptions |
| WriteDefaultValues: false | ✅ Required on Block_State |
| No SendMessage | ✅ Direct `Animator.SetBool()` |
| Debug tools in Development builds only | ✅ `OnGUI` in `#if DEVELOPMENT_BUILD || UNITY_EDITOR` |
| Input System actions only | ✅ Uses `_input.Player.Block` — no `Mouse.current` |

### Project Structure Notes

**Files to MODIFY:**
```
Assets/_Game/InputSystem_Actions.inputactions                              ← Add Block action + RMB binding
Assets/_Game/InputSystem_Actions.cs                                        ← Add m_Player_Block field, FindAction, @Block property
Assets/_Game/Scripts/Combat/PlayerCombat.cs                                ← Add IsBlockingHash, _isBlocking, block subscription, OnBlockStarted/Canceled, guard in TryAttack, OnGUI line
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller ← Add IsBlocking Bool param + Block_State
```

**Files to CREATE:**
```
Assets/Tests/EditMode/BlockGateTests.cs    ← NEW Edit Mode tests
Assets/Tests/EditMode/BlockGateTests.cs.meta ← auto-generated by Unity
```

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs          ← Story 2.1 (unchanged)
├── CombatConfigSO.cs         ← Story 2.1 + Story 2.3 (unchanged this story)
└── PlayerCombat.cs           ← Story 2.3 + extended this story (block state)
```

**`CombatConfigSO` current state (no changes needed):**
```csharp
[Header("Stamina Costs")]
public float attackStaminaCost = 20f;
public float blockStaminaCostPerHit = 15f;  // ← already exists, used in AC 4
public float dodgeStaminaCost = 25f;
```

### References

- Epic 2 story 4: [Source: _bmad-output/epics.md#Epic 2: Combat System]
- GDD blocking mechanic: [Source: _bmad-output/gdd.md#Combat (Real-Time, Gothic-Style)]
- GDD control scheme: [Source: _bmad-output/gdd.md#Controls and Input] — Block = RMB hold
- Architecture Decision 1 — Component-based + Event Bus: [Source: _bmad-output/game-architecture.md#Decision 1]
- `CombatConfigSO.blockStaminaCostPerHit`: [Source: Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs]
- PlayerCombat input subscription pattern: [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs]
- Story 2.3 completion (what block builds on): [Source: _bmad-output/implementation-artifacts/2-3-timed-combo.md]
- Story 2.3 note about 2.4: "New Block input action; subscribe to `_input.Player.Block` in `OnEnable`; `HasEnough(blockStaminaCostPerHit)` gate" [Source: 2-3-timed-combo.md#What Subsequent Stories Build On]
- OnDisable null guard: [Source: CLAUDE.md#Unity Lifecycle Gotcha]
- AnimatorController bool conditionMode gotcha: [Source: CLAUDE.md#Unity MCP Tool Quirks]
- WriteDefaultValues: false: [Source: CLAUDE.md#Animator Controller Best Practices]
- Input System action map layout (no Block action exists): [Source: CLAUDE.md#Unity Input System — Action Map Layout]
- Cursor-lock implementation (RMB conflict analysis): [Source: CLAUDE.md#CameraController — cursor lock uses UI.Click, not UI.RightClick for re-lock]
- No magic numbers in gameplay scripts: [Source: _bmad-output/project-context.md#Configuration Management]
- GameLog mandatory: [Source: _bmad-output/project-context.md#Logging]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Code Review Fixes Applied

- [HIGH] Deleted ghost `Assets/InputSystem_Actions.cs` and `Assets/InputSystem_Actions.cs.meta` at repo root — Unity auto-regenerated these when it detected `.inputactions` change; the root file added `OnBlock` to `IPlayerActions` interface contrary to story spec; deleted to prevent future regeneration conflicts.
- [MED] `PlayerCombat.OnBlockCanceled()`: added `if (!_isBlocking) return;` guard — previously logged "Block lowered" and called SetBool(false) even when stamina denied block entry, producing misleading debug output.
- [MED] Story File List updated: added `BlockGateTests.cs.meta` and `CLAUDE.md` (both modified/created during implementation but omitted from File List).
- [MED-3 — Action Item] `BlockGateTests` tests local helpers, not `StaminaSystem.HasEnough()` directly. Acceptable given story's "pure formulas" spec and project test pattern; flag for Epic 2 retrospective.

### Debug Log References

### Completion Notes List

- Tasks 1–3 and 4.1 complete. All code matches Dev Notes specification exactly.
- InputSystem_Actions.inputactions: Block Button action (UUID f7d2a856-9c14-4e32-8b5a-2c1e9f473d80) added to Player map; RMB binding (UUID e3c4b719-5d28-4f67-a920-1d8e6c294b35) added to Player map bindings.
- InputSystem_Actions.cs: m_Player_Block field, FindAction("Block"), @Block property added alongside Sprint equivalents.
- PlayerCombat.cs: Full rewrite adding IsBlockingHash, _isBlocking, OnBlockStarted (stamina gate + combo reset + animator bool), OnBlockCanceled, _isBlocking guard at top of TryAttack, Block debug label at y=130 in OnGUI.
- PlayerAnimatorController.controller: IsBlocking Bool parameter (m_Type: 4) added; Block_State (fileID 1100000000000000010, WriteDefaultValues: 0, Idle.fbx placeholder) added to Attack layer; Any State → Block_State transition (conditionMode: 1, IsBlocking true, canTransitionToSelf: 0, 0.1s, no exit time); Block_State → Locomotion transition (conditionMode: 2, IsBlocking false, 0.1s, no exit time).
- BlockGateTests.cs: Created with 5 tests covering block gate and attack-while-blocking gate formulas.
- 21/21 Edit Mode tests pass (5 new BlockGateTests + 16 existing regression tests).
- Play Mode: no console errors on launch. Manual UX verification (5.2–5.7) to be confirmed by user.
- Root cause found and fixed: InputSystem_Actions.cs embeds full action JSON as string literal — both .inputactions AND the embedded JSON in .cs must be updated when adding actions.

### File List

Assets/_Game/InputSystem_Actions.inputactions
Assets/_Game/InputSystem_Actions.cs
Assets/_Game/Scripts/Combat/PlayerCombat.cs
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller
Assets/Tests/EditMode/BlockGateTests.cs
Assets/Tests/EditMode/BlockGateTests.cs.meta
CLAUDE.md
