# Story 2.3: Directional Attacks

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to attack in the direction I move my mouse,
so that combat feels intentional and weighty rather than a simple button press.

## Acceptance Criteria

1. An `AttackDirection` enum (`Overhead`, `Left`, `Right`, `Thrust`) exists at `Assets/_Game/Scripts/Combat/AttackDirection.cs` in namespace `Game.Combat`.
2. A `DirectionalAttackSampler` MonoBehaviour exists at `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs` in namespace `Game.Combat`. Its `Awake()` null-guards `_config` (SerializeField) with the standard log + `enabled = false` pattern.
3. `DirectionalAttackSampler` maintains a `Queue<Vector2> _deltaBuffer`. `RecordDelta(Vector2 delta)` enqueues the delta and dequeues from the front if count exceeds `_config.directionSampleFrames`. `Resolve()` averages all queued values, then clears the buffer.
4. `Resolve()` applies the architecture-specified logic:
   - buffer empty OR `avg.magnitude < _config.attackDirectionThreshold` → `Overhead`
   - `|avg.x| > |avg.y|` and `avg.x < 0` → `Left`
   - `|avg.x| > |avg.y|` and `avg.x > 0` → `Right`
   - `|avg.y| > |avg.x|` and `avg.y > 0` → `Overhead`
   - `|avg.y| > |avg.x|` and `avg.y < 0` → `Thrust`
5. `PlayerCombat.Awake()` additionally acquires `DirectionalAttackSampler` via `GetComponent` and `Animator` via `GetComponent` with null-guards (log error + `enabled = false` if missing).
6. `PlayerCombat.Update()` calls `_sampler.RecordDelta(_input.Player.Look.ReadValue<Vector2>())` each frame, guarded by `if (_input == null || _sampler == null) return`.
7. `PlayerCombat.TryAttack()` replaces the Story 2.2 placeholder log with: call `_sampler.Resolve()` → check stamina via `HasEnough` → call `_staminaSystem.Consume(attackStaminaCost)` → call `_animator.SetTrigger(triggerHash)` (integer hash, not string — string allocation per attack violates performance rules) → log `GameLog.Info(TAG, $"Attack: {direction}")`. Trigger hashes are precomputed as static readonly fields at class init.
8. `PlayerAnimatorController` has 4 new Trigger-type parameters: `Attack_Overhead`, `Attack_Left`, `Attack_Right`, `Attack_Thrust`.
9. `PlayerAnimatorController` has 4 new animation states (one per trigger), each reachable via an Any State transition on its respective trigger. Each state transitions back to the Locomotion state on Exit Time (1.0). Placeholder: reuse the existing `Idle.fbx` clip for all 4 states — animation authenticity is out of scope for this story.
10. `DirectionalAttackSampler` is attached to the Player prefab root alongside `PlayerCombat`, with `CombatConfig.asset` wired to its `_config` field.
11. Development-only debug display (via `OnGUI` in `PlayerCombat`) shows the last resolved direction below the existing combat label at `Rect(10, 90, 300, 20)`: `"Last Attack: {direction}"`.
12. An Edit Mode test class `DirectionalAttackSamplerTests` exists at `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs` with ≥ 4 tests calling `DirectionalAttackSampler` directly, covering: empty buffer → Overhead, right-dominant delta → Right, left-dominant delta → Left, downward delta → Thrust, and buffer clear after Resolve.
13. No compile errors; no Play Mode errors in TestScene.unity; Stories 1.1–2.2 behavior is unchanged.

## Tasks / Subtasks

- [x] Task 1: Create AttackDirection enum (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Scripts/Combat/AttackDirection.cs` — `namespace Game.Combat { public enum AttackDirection { Overhead, Left, Right, Thrust } }`

- [x] Task 2: Create DirectionalAttackSampler MonoBehaviour (AC: 2, 3, 4)
  - [x] 2.1 Create `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs` — see Dev Notes for complete implementation
  - [x] 2.2 Verify null-guard on `_config` in Awake (log error + `enabled = false`)
  - [x] 2.3 Verify `RecordDelta()` enqueues and dequeues correctly at capacity cap
  - [x] 2.4 Verify `Resolve()` logic matches architecture spec exactly (5 cases in AC4)
  - [x] 2.5 Verify `Resolve()` clears `_deltaBuffer` after computing direction

- [x] Task 3: Update PlayerCombat (AC: 5, 6, 7, 11)
  - [x] 3.1 In `Awake()`: acquire `_sampler = GetComponent<DirectionalAttackSampler>()` and `_animator = GetComponent<Animator>()` with null-guards
  - [x] 3.2 Add `Update()` method calling `_sampler.RecordDelta(_input.Player.Look.ReadValue<Vector2>())` with guard
  - [x] 3.3 Replace `TryAttack()` placeholder with directional resolve → consume → SetTrigger flow
  - [x] 3.4 Update `OnGUI` to also display last attack direction at `Rect(10, 90, 300, 20)`

- [x] Task 4: Update PlayerAnimatorController (AC: 8, 9)
  - [x] 4.1 Add 4 Trigger parameters: `Attack_Overhead`, `Attack_Left`, `Attack_Right`, `Attack_Thrust`
  - [x] 4.2 Add 4 animation states using `Idle.fbx` as placeholder clip
  - [x] 4.3 Add Any State → each attack state transition on its respective trigger (Has Exit Time OFF, Transition Duration 0)
  - [x] 4.4 Add each attack state → Locomotion transition on Exit Time (1.0, no condition)

- [x] Task 5: Attach DirectionalAttackSampler to Player prefab (AC: 10)
  - [x] 5.1 Add `DirectionalAttackSampler` component to Player prefab root
  - [x] 5.2 Wire `CombatConfig.asset` to `_config` field

- [x] Task 6: Edit Mode tests (AC: 12)
  - [x] 6.1 Create `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs` — see Dev Notes for test cases
  - [x] 6.2 Run via Unity Test Runner — all tests green

- [x] Task 7: Play Mode validation (AC: 13)
  - [x] 7.1 Enter Play Mode — no errors on startup
  - [x] 7.2 Move mouse rightward and press LMB — Console shows `"[Combat] Attack: Right"`
  - [x] 7.3 Move mouse leftward and press LMB — Console shows `"[Combat] Attack: Left"`
  - [x] 7.4 Move mouse downward and press LMB — Console shows `"[Combat] Attack: Thrust"`
  - [x] 7.5 Hold mouse still and press LMB — Console shows `"[Combat] Attack: Overhead"`
  - [x] 7.6 Drain stamina to zero, press LMB — "Cannot attack: insufficient stamina" appears (2.2 gate still works)
  - [x] 7.7 Verify OnGUI shows `"Last Attack: [direction]"` after each attack
  - [x] 7.8 Verify WASD movement, camera, jump, stamina regen all unchanged

## Dev Notes

Story 2.3 implements the **directional attack resolution** layer that Story 2.2 deferred. The stamina gate from Story 2.2 is preserved — direction is resolved BEFORE stamina is checked (direction resolution is free; failing the gate discards the resolved direction). The resolver also clears its buffer after each call, whether the attack succeeds or is gated.

### Precise Scope Boundaries

**In scope:**
- `AttackDirection` enum (new file)
- `DirectionalAttackSampler` MonoBehaviour (new)
- `PlayerCombat` updated: new Awake refs, new Update, new TryAttack body, updated OnGUI
- `PlayerAnimatorController`: 4 new triggers, 4 placeholder states
- Player prefab: `DirectionalAttackSampler` component + `CombatConfig.asset` wired
- Edit Mode tests for `DirectionalAttackSampler.Resolve()` logic
- Development debug display: last resolved direction

**Explicitly OUT of scope — do NOT implement:**
- Real Mixamo attack animations (FBX import deferred — placeholder Idle clip is correct)
- Hit detection or damage — Story 2.3 scope ends at SetTrigger + Consume
- Hitbox activation timing — tied to actual animation events (later story)
- Block input/gate — Story 2.4
- Dodge roll — Story 2.6
- Attack animation events (sound, VFX, hitbox windows) — Epic 8 / later stories
- Stagger or hit reactions on enemies — Story 2.7 (enemy AI)

### Critical Architecture Rules

**Buffer management — MANDATORY (project-context.md gotcha):**
> "DirectionalAttackSampler buffer must be cleared between attacks"

`Resolve()` MUST call `_deltaBuffer.Clear()` BEFORE returning. If the buffer is not cleared, stale mouse movement from a previous attack bleeds into the next direction resolution, making subsequent attacks feel wrong (e.g., the player pauses, then attacks straight down, but the buffer still has old "right" deltas from two frames ago).

**Direction resolution order — MANDATORY:**
```
In TryAttack():
1. Resolve direction (always — even if gate will fail)
2. Check stamina gate via HasEnough()
3. If gate fails → log warning, return (direction discarded — buffer already cleared by Resolve())
4. Consume stamina
5. SetTrigger on Animator
6. Log info
```

The architecture data flow shows Resolve() BEFORE the stamina check. If you check stamina first, you skip Resolve() when the gate fails, meaning the buffer is NOT cleared — and the stale delta bleeds into the next attack after regen.

**RecordDelta guard — MANDATORY:**
PlayerCombat's `Update()` must guard on `_input == null`:
```csharp
private void Update()
{
    if (_input == null || _sampler == null) return;
    _sampler.RecordDelta(_input.Player.Look.ReadValue<Vector2>());
}
```

After our Story 2.2 code review fix, `_input` is set to null in `OnDisable()`. On re-enable, `_input` is recreated in `OnEnable()`. Between disable and re-enable, `Update()` runs zero frames (disabled components don't call Update), so this guard is technically belt-and-suspenders — but it's still required for defensive correctness.

**Animator reference — PlayerCombat gets it:**
`PlayerCombat` acquires `Animator` via `GetComponent<Animator>()` in `Awake()`. This is the same `Animator` that `PlayerAnimator` drives. Both can coexist writing different parameters (PlayerAnimator writes Speed/IsGrounded/IsRising; PlayerCombat writes Attack_* triggers). Unity supports multiple scripts writing to the same Animator.

**AnimatorController parameter hashing — MANDATORY:**
Per `PlayerAnimator.cs` pattern, hash trigger names once at class init:
```csharp
private static readonly int AttackOverheadHash = Animator.StringToHash("Attack_Overhead");
private static readonly int AttackLeftHash      = Animator.StringToHash("Attack_Left");
private static readonly int AttackRightHash     = Animator.StringToHash("Attack_Right");
private static readonly int AttackThrustHash    = Animator.StringToHash("Attack_Thrust");
```

Then in `TryAttack()`:
```csharp
int triggerHash = direction switch
{
    AttackDirection.Left    => AttackLeftHash,
    AttackDirection.Right   => AttackRightHash,
    AttackDirection.Thrust  => AttackThrustHash,
    _                       => AttackOverheadHash,
};
_animator.SetTrigger(triggerHash);
```

**Do NOT use string-based SetTrigger** (e.g., `SetTrigger("Attack_" + direction.ToString())`) — violates performance rules (string allocation per attack).

### Complete Implementation: AttackDirection.cs

```csharp
namespace Game.Combat
{
    /// <summary>Attack direction resolved from mouse delta at moment of input.</summary>
    public enum AttackDirection { Overhead, Left, Right, Thrust }
}
```

### Complete Implementation: DirectionalAttackSampler.cs

```csharp
using Game.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Maintains a rolling buffer of mouse delta values and resolves them into
    /// one of four attack directions at the moment of LMB input.
    /// Call RecordDelta() every Update from PlayerCombat.
    /// Call Resolve() when LMB is pressed — clears buffer after resolution.
    /// </summary>
    public class DirectionalAttackSampler : MonoBehaviour
    {
        private const string TAG = "[Combat]";

        [SerializeField] private CombatConfigSO _config;

        private readonly Queue<Vector2> _deltaBuffer = new Queue<Vector2>();

        private void Awake()
        {
            if (_config == null)
            {
                GameLog.Error(TAG, "CombatConfigSO not assigned — DirectionalAttackSampler disabled");
                enabled = false;
            }
        }

        /// <summary>
        /// Enqueue the current frame's mouse delta. Called every Update from PlayerCombat.
        /// Maintains a rolling window of _config.directionSampleFrames entries.
        /// </summary>
        public void RecordDelta(Vector2 delta)
        {
            _deltaBuffer.Enqueue(delta);
            while (_deltaBuffer.Count > _config.directionSampleFrames)
                _deltaBuffer.Dequeue();
        }

        /// <summary>
        /// Average the buffered deltas, clear the buffer, and return the resolved direction.
        /// Returns Overhead when buffer is empty or mouse was below the threshold.
        /// </summary>
        public AttackDirection Resolve()
        {
            if (_deltaBuffer.Count == 0)
            {
                GameLog.Info(TAG, "Direction defaulted to Overhead (buffer empty)");
                return AttackDirection.Overhead;
            }

            Vector2 avg = Vector2.zero;
            foreach (var d in _deltaBuffer) avg += d;
            avg /= _deltaBuffer.Count;

            _deltaBuffer.Clear(); // CRITICAL: clear before returning

            if (avg.magnitude < _config.attackDirectionThreshold)
            {
                GameLog.Info(TAG, "Direction defaulted to Overhead (below threshold)");
                return AttackDirection.Overhead;
            }

            if (Mathf.Abs(avg.x) > Mathf.Abs(avg.y))
                return avg.x < 0 ? AttackDirection.Left : AttackDirection.Right;

            return avg.y > 0 ? AttackDirection.Overhead : AttackDirection.Thrust;
        }
    }
}
```

### PlayerCombat.cs — Diff Summary (changes from Story 2.2)

**New fields:**
```csharp
private DirectionalAttackSampler _sampler;
private Animator _animator;
private AttackDirection _lastAttackDirection; // for debug display

private static readonly int AttackOverheadHash = Animator.StringToHash("Attack_Overhead");
private static readonly int AttackLeftHash      = Animator.StringToHash("Attack_Left");
private static readonly int AttackRightHash     = Animator.StringToHash("Attack_Right");
private static readonly int AttackThrustHash    = Animator.StringToHash("Attack_Thrust");
```

**Updated Awake() — add after _staminaSystem acquisition:**
```csharp
_sampler = GetComponent<DirectionalAttackSampler>();
if (_sampler == null)
{
    GameLog.Error(TAG, "DirectionalAttackSampler not found on Player — PlayerCombat disabled");
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
```

**New Update():**
```csharp
private void Update()
{
    if (_input == null || _sampler == null) return;
    _sampler.RecordDelta(_input.Player.Look.ReadValue<Vector2>());
}
```

**Replaced TryAttack():**
```csharp
private void TryAttack()
{
    // Resolve direction first (always — clears buffer even if gate fails)
    AttackDirection direction = _sampler.Resolve();

    if (!_staminaSystem.HasEnough(_config.attackStaminaCost))
    {
        GameLog.Warn(TAG, "Cannot attack: insufficient stamina");
        return;
    }

    bool consumed = _staminaSystem.Consume(_config.attackStaminaCost);
    if (consumed)
    {
        _lastAttackDirection = direction;
        int triggerHash = direction switch
        {
            AttackDirection.Left   => AttackLeftHash,
            AttackDirection.Right  => AttackRightHash,
            AttackDirection.Thrust => AttackThrustHash,
            _                      => AttackOverheadHash,
        };
        _animator.SetTrigger(triggerHash);
        GameLog.Info(TAG, $"Attack: {direction}");
    }
    else
    {
        GameLog.Error(TAG, "Consume() returned false after HasEnough() passed — StaminaSystem state inconsistency");
    }
}
```

**Updated OnGUI() — add direction label:**
```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private void OnGUI()
{
    if (_config == null || _staminaSystem == null) return;
    bool canAttack = _staminaSystem.HasEnough(_config.attackStaminaCost);
    string state = canAttack ? "Ready" : "STAMINA EMPTY";
    GUI.Label(new Rect(10, 70, 300, 20), $"Combat: [{state}]");
    GUI.Label(new Rect(10, 90, 300, 20), $"Last Attack: {_lastAttackDirection}");
}
#endif
```

### AnimatorController Setup (Task 4)

**Preferred method:** Use the MCP tools to add parameters and states to `PlayerAnimatorController.controller`, then verify via YAML if needed.

**4 new Trigger parameters to add:**
```
Attack_Overhead  (type: Trigger)
Attack_Left      (type: Trigger)
Attack_Right     (type: Trigger)
Attack_Thrust    (type: Trigger)
```

**4 new animation states:**
- `Attack_Overhead_State` — clip: `Idle.fbx` (placeholder), Motion: `Assets/_Game/Art/Characters/Player/Animations/Idle.fbx`
- `Attack_Left_State` — same clip
- `Attack_Right_State` — same clip
- `Attack_Thrust_State` — same clip

**Transitions (from Any State → attack state):**
- Trigger: respective trigger parameter
- Has Exit Time: OFF
- Transition Duration: 0s
- WriteDefaultValues: false on all new states (CRITICAL per CLAUDE.md)

**Transitions (attack state → Locomotion):**
- Has Exit Time: ON
- Exit Time: 1.0
- No conditions
- Transition Duration: 0.15s (smooth blend back)

**CLAUDE.md Gotcha — WriteDefaultValues:**
Set `WriteDefaultValues: 0` on ALL four new attack states. If left as `true`, the attack states will write T-pose defaults for bones they don't animate, causing visual corruption during the transition back to Locomotion.

**CLAUDE.md Gotcha — AnimatorController transitions via MCP:**
The `manage_animation` tool's `conditionMode` for triggers uses `1` (If/true). Verify YAML after adding transitions. If using direct YAML edits, use `conditionMode: 1` for Trigger conditions.

### Edit Mode Tests: DirectionalAttackSamplerTests.cs

```csharp
using System.Reflection;
using Game.Combat;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for DirectionalAttackSampler direction resolution logic.
/// Uses reflection to set _config.attackDirectionThreshold and directionSampleFrames.
/// </summary>
public class DirectionalAttackSamplerTests
{
    private GameObject _go;
    private DirectionalAttackSampler _sampler;
    private CombatConfigSO _config;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestSampler");
        _config = ScriptableObject.CreateInstance<CombatConfigSO>();
        _config.attackDirectionThreshold = 0.3f;
        _config.directionSampleFrames = 5;

        _sampler = _go.AddComponent<DirectionalAttackSampler>();
        typeof(DirectionalAttackSampler)
            .GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_sampler, _config);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        Object.DestroyImmediate(_config);
    }

    [Test]
    public void Resolve_ReturnsOverhead_WhenBufferEmpty()
    {
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Overhead));
    }

    [Test]
    public void Resolve_ReturnsRight_WhenMouseMovedRight()
    {
        _sampler.RecordDelta(new Vector2(1f, 0f));
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Right));
    }

    [Test]
    public void Resolve_ReturnsLeft_WhenMouseMovedLeft()
    {
        _sampler.RecordDelta(new Vector2(-1f, 0f));
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Left));
    }

    [Test]
    public void Resolve_ReturnsThrust_WhenMouseMovedDown()
    {
        _sampler.RecordDelta(new Vector2(0f, -1f));
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Thrust));
    }

    [Test]
    public void Resolve_ReturnsOverhead_WhenBelowThreshold()
    {
        _sampler.RecordDelta(new Vector2(0.1f, 0f)); // below 0.3 threshold
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Overhead));
    }

    [Test]
    public void Resolve_ClearsBuffer_AfterCall()
    {
        _sampler.RecordDelta(new Vector2(1f, 0f));
        _sampler.Resolve(); // consumes the delta
        // Buffer now empty → second Resolve returns Overhead
        Assert.That(_sampler.Resolve(), Is.EqualTo(AttackDirection.Overhead));
    }
}
```

### Player Prefab Structure After This Story

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF; Controller: PlayerAnimatorController)
├── PlayerController.cs
├── PlayerAnimator.cs
├── StaminaSystem.cs          (wired to CombatConfig.asset)
├── PlayerCombat.cs           (wired to CombatConfig.asset)
├── DirectionalAttackSampler.cs  ← NEW (wired to CombatConfig.asset)
├── CameraTarget             (child, local Y = 1.6)
└── Character                (child — Mixamo FBX)
```

### Debug Display Stack (after this story)

```
[y=50]  Stamina: 80 / 100        ← StaminaSystem (Story 2.1)
[y=70]  Combat: [Ready]          ← PlayerCombat (Story 2.2)
[y=90]  Last Attack: Overhead    ← PlayerCombat (this story, Story 2.3)
```

### What Subsequent Stories Build on this Foundation

| Story | What it adds |
|-------|-------------|
| 2.4 Manual Blocking | New Block input action in `InputSystem_Actions.inputactions`; `PlayerCombat.OnEnable` subscribes to `_input.Player.Block`; gate check via `HasEnough(blockCost)` |
| 2.5 Perfect Block | Timing window inside block handler; successful perfect block skips `Consume()` |
| 2.6 Dodge Roll | New Dodge action; `HasEnough(dodgeStaminaCost)`; triggers `DodgeController` |
| 2.7 Enemy AI | Enemy attack logic reads player position; player's `ExecuteAttack`-style system extended for enemy hits |
| 2.3+ | Replace placeholder Idle clips with real Mixamo attack FBXes once imported |

### Architecture Compliance

| Rule | Compliance |
|------|-----------|
| All code under `Assets/_Game/` | ✅ `DirectionalAttackSampler.cs`, `AttackDirection.cs` in `_Game/Scripts/Combat/` |
| No magic numbers | ✅ Threshold and frame count from `_config` |
| GameLog only | ✅ All logging via `GameLog.*` with `[Combat]` TAG |
| Null-guard in Awake | ✅ `_config`, `_sampler`, `_animator` all null-checked |
| Same-system direct reference | ✅ `GetComponent<DirectionalAttackSampler>()` (same GameObject, same system) |
| No string allocations in hot path | ✅ Animator triggers hashed at class init, not per-call |
| Event subscription in OnEnable/OnDisable | ✅ Attack.started subscribed/unsubscribed correctly |
| OnDisable null guard | ✅ Inherited from Story 2.2 fix (`_input = null` after Dispose) |
| Debug tools in Development builds only | ✅ `OnGUI` in `#if DEVELOPMENT_BUILD \|\| UNITY_EDITOR` |
| No SendMessage | ✅ Direct Animator.SetTrigger() |
| WriteDefaultValues: false | ✅ Required on all 4 new attack states |

### Project Structure Notes

New files added by this story:
```
Assets/_Game/Scripts/Combat/AttackDirection.cs                  ← NEW enum
Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs         ← NEW MonoBehaviour
Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs          ← NEW Edit Mode tests
```

Modified files:
```
Assets/_Game/Scripts/Combat/PlayerCombat.cs                     ← Updated (Awake, Update, TryAttack, OnGUI)
Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller ← 4 triggers, 4 states
Assets/_Game/Prefabs/Player/Player.prefab                       ← DirectionalAttackSampler component added
```

`.meta` files generated automatically for all new `.cs` files (do NOT hand-create `.meta` files — let Unity generate them to ensure valid GUID).

**`Scripts/Combat/` after this story:**
```
Assets/_Game/Scripts/Combat/
├── StaminaSystem.cs          ← Story 2.1
├── CombatConfigSO.cs         ← Story 2.1 (directionSampleFrames, attackDirectionThreshold already present)
├── PlayerCombat.cs           ← Story 2.2 + updated this story
├── AttackDirection.cs        ← NEW this story
└── DirectionalAttackSampler.cs ← NEW this story
```

### References

- Epic 2 "directional attacks (mouse-driven)": [Source: epics.md#Epic 2: Combat System]
- GDD combat: "Attack: LMB + mouse direction": [Source: gdd.md#Control Scheme]
- Architecture Decision 5 — Directional Attack Input: [Source: game-architecture.md#Decision 5: Directional Attack Input Sampling]
- Novel Pattern 1 — DirectionalAttackSampler complete example: [Source: game-architecture.md#Novel Pattern 1: Directional Attack Pattern]
- Buffer clear gotcha: [Source: project-context.md#Novel Pattern Gotchas]
- `attackDirectionThreshold`, `directionSampleFrames` already in CombatConfigSO: [Source: implementation-artifacts/2-1-stamina-bar-system.md#CombatConfigSO — Complete Implementation]
- PlayerAnimator pattern (param hashing): [Source: Assets/_Game/Scripts/Player/PlayerAnimator.cs]
- `GetComponent` in Awake only: [Source: project-context.md#Engine-Specific Rules]
- No string allocations in Update: [Source: project-context.md#Performance Rules]
- WriteDefaultValues: false: [Source: CLAUDE.md#Animator Controller Best Practices]
- OnDisable null guard (`_input = null`): [Source: CLAUDE.md#Unity Lifecycle Gotcha + Story 2.2 code review]
- Same-system direct reference: [Source: game-architecture.md#Standard Patterns — Component Communication]
- GameLog mandatory: [Source: project-context.md#Logging]
- Tests.EditMode assembly references Game: [Source: CLAUDE.md#Assembly Setup]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- No compilation errors on first attempt. [RequireComponent] on PlayerCombat auto-triggered Unity to recognize missing DirectionalAttackSampler dependency; component added manually to prefab YAML with _config wired. All 17 EditMode tests green (11 pre-existing + 6 new DirectionalAttackSamplerTests).

### Completion Notes List

- Created `AttackDirection.cs` — simple 4-value enum in `Game.Combat` namespace
- Created `DirectionalAttackSampler.cs` — rolling `Queue<Vector2>` buffer, RecordDelta() maintains cap via directionSampleFrames, Resolve() averages + clears buffer + returns direction per architecture spec
- Updated `PlayerCombat.cs` — added [RequireComponent(typeof(DirectionalAttackSampler))], added _sampler/_animator fields + static trigger hashes, Awake acquires both with null-guards, Update() feeds RecordDelta(), TryAttack() now resolves direction BEFORE stamina gate, uses hashed SetTrigger(), OnGUI shows last attack direction at y=90
- Updated `PlayerAnimatorController.controller` — 4 Trigger parameters (Attack_Overhead/Left/Right/Thrust, m_Type:9), 4 placeholder states using Idle.fbx clip, Any State → attack transitions (no exit time, 0 duration), attack → Locomotion transitions (exit time 0.9, 0.15s blend); WriteDefaultValues:0 on all new states
- Added `DirectionalAttackSampler` component to Player prefab YAML with `_config` wired to `CombatConfig.asset` (guid: 86463aac9796a76438a3015ed78aacb8)
- Created `DirectionalAttackSamplerTests.cs` — 6 tests covering empty buffer, right/left/thrust directions, below-threshold default, and buffer-clear-after-resolve; all pass

### File List

- `Assets/_Game/Scripts/Combat/AttackDirection.cs` (new)
- `Assets/_Game/Scripts/Combat/AttackDirection.cs.meta` (new)
- `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs` (new)
- `Assets/_Game/Scripts/Combat/DirectionalAttackSampler.cs.meta` (new)
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` (modified — directional attack integration)
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` (modified — 4 triggers, 4 states, 8 transitions)
- `Assets/_Game/Prefabs/Player/Player.prefab` (modified — DirectionalAttackSampler component added)
- `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs` (new)
- `Assets/Tests/EditMode/DirectionalAttackSamplerTests.cs.meta` (new)
- `Assets/Tests/EditMode/PlayerCombatGateTests.cs` (new)
- `Assets/Tests/EditMode/PlayerCombatGateTests.cs.meta` (new)
- `_bmad-output/implementation-artifacts/2-3-directional-attacks.md` (new)

## Change Log

- 2026-03-05: Story implemented — `AttackDirection` enum and `DirectionalAttackSampler` MonoBehaviour created; `PlayerCombat` updated with directional resolve + hashed triggers + debug display; `PlayerAnimatorController` updated with 4 trigger params + 4 placeholder states (Idle.fbx); `DirectionalAttackSampler` added to Player prefab with `CombatConfig.asset` wired; 6 Edit Mode tests added (17/17 total passing). Status set to review.
- 2026-03-05: Code review fixes — (M-1) Attack→Locomotion exit time corrected from 0.9 to 1.0 in all 4 transitions; (M-2) `PlayerCombatGateTests.cs` and `.meta` added to File List; (M-3) AC 7 updated to reflect hashed SetTrigger (not string-based); (L-1) `DirectionalAttackSampler.Resolve()` empty-buffer path now calls `Clear()` for invariant consistency. Status set to done.
