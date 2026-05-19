---
title: 'AI Humanoid Animation Driver'
slug: 'tech-spec-ai-humanoid-animation-driver'
created: '2026-05-19'
status: 'dev-complete-awaiting-play-test'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13]
tech_stack:
  - Unity 6000.3.10f1
  - NavMeshAgent
  - Animator (2D Freeform Cartesian blend tree)
files_to_modify:
  - Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs (RENAME → MonsterAnimationDriver.cs)
  - Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs (NEW)
  - Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs (NEW)
  - Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs (NEW)
  - Assets/_Game/Scripts/AI/EntityBrain.cs
  - Assets/_Game/Scripts/AI/EntityHealth.cs
  - Assets/_Game/Scripts/AI/SMB_DeathState.cs
  - Assets/_Game/Prefabs/Entities/Entity_base.prefab
  - Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab
code_patterns:
  - Abstract MonoBehaviour base class for polymorphic component reference
  - SerializeField type widening preserves existing component references in YAML
  - Driver pattern: brain owns state machine, driver owns animator parameter math
test_patterns:
  - Existing EditMode test suite (203 tests) must remain green — no regression on monsters
---

# Tech-Spec: AI Humanoid Animation Driver

**Created:** 2026-05-19

## Overview

### Problem Statement

`EntityBrain` (the NavMeshAgent-driven AI state machine) currently only animates monster
entities through `EntityAnimationBridge` — a single `Speed` float drives a 1D locomotion
blend tree, and a single `Attack` trigger fires combat anims.

Humanoid entities (NPCs today, humanoid enemies later — goblins, skeletons) need a
different animator contract:

| Surface | Monster | Humanoid |
|---------|---------|----------|
| Locomotion | `Speed` (1D, raw m/s) | `VelocityX` + `VelocityZ` (2D, normalized to local space) + `IsGrounded` + `IsRising` |
| Attack | `Attack` trigger | combo trigger hashes |
| GetHit / Death | `GetHit` / `Death` triggers + ragdoll | future — out of scope for this spec |

The `Player` already drives the humanoid animator through `HumanoidAnimationBridge` via
`PlayerAnimationDriver` (which reads `CharacterController.velocity`). For AI humanoids the
velocity source is `NavMeshAgent.velocity` and the gating is the brain's state machine,
not `PlayerStateManager` — so a parallel driver is needed.

The state machine itself (Idle → Patrol → Engage → Attack → Dead) is identical between
monsters and humanoids; only the animator-parameter mapping differs.

### Solution

Introduce a thin `AIAnimationDriver` abstract MonoBehaviour. `EntityBrain` and
`EntityHealth` reference the abstract type, so both monster and humanoid concrete drivers
plug into the same brain. **Both sides are symmetric: pure bridge + driver.**

- `EntityAnimationBridge` (today a bridge+driver hybrid) is **split** into:
  - `MonsterAnimationBridge` (NEW pure bridge — parameter writes only: `SetMoveSpeed`,
    `TriggerAttack`, `TriggerGetHit`, `TriggerDeath`). Parallels `HumanoidAnimationBridge`.
  - `MonsterAnimationDriver : AIAnimationDriver` (RENAMED FROM `EntityAnimationBridge` —
    keeps `.meta` GUID so the prefab's `m_Script` reference survives). Owns Speed smoothing,
    ragdoll-bone discovery, `Entity.AnimatorOverride` application, and death-component-disable.
    References the bridge via `_bridge` field; delegates parameter writes to it.
- `HumanoidAIAnimationDriver` is new. It inherits `AIAnimationDriver` and delegates to a
  sibling `HumanoidAnimationBridge` (existing pure bridge), doing the navmesh-velocity →
  local-space → normalized conversion that `PlayerAnimationDriver` does for the player.
- Final symmetric shape:

  ```
  EntityBrain ─┐                                       ┌─ MonsterAnimationBridge   (pure parameter writes)
               ├─→ AIAnimationDriver ─┬─ MonsterAnimationDriver  ───→ ┤
  EntityHealth ┘                      │                              └─ Animator (monster controller)
                                      │
                                      └─ HumanoidAIAnimationDriver ─→ HumanoidAnimationBridge (pure parameter writes)
                                                                                     │
                                                                                     └─→ Animator (Humanoid_Template)
  ```

  The Player uses `PlayerAnimationDriver → HumanoidAnimationBridge` and does **not** flow
  through `AIAnimationDriver` (Player is not AI).

- One brain, two drivers. **No `MonsterBrain` / `HumanoidBrain` split today** — defer until
  humanoid combat actually diverges.

### Scope

**In Scope:**
- `AIAnimationDriver` abstract base
- Split `EntityAnimationBridge` into `MonsterAnimationBridge` (pure) + `MonsterAnimationDriver`
  (lifecycle + smoothing). Rename via file mv preserves the `.meta` GUID so the prefab's
  `m_Script` reference auto-migrates from `EntityAnimationBridge` to `MonsterAnimationDriver`.
- `HumanoidAIAnimationDriver` concrete class — drive locomotion only (idle + walk + run)
- `EntityBrain` / `EntityHealth` / `SMB_DeathState` reference the abstract base
- `Entity_base.prefab` re-wired: `MonsterAnimationBridge` component added; driver's old
  `_animator` field cleared; driver's new `_bridge` field wired to the bridge
- `NPC_base Variant.prefab` reconfigured to use `HumanoidAIAnimationDriver` (variant override)
- AC for a humanoid NPC visibly idling and wandering in a scene
- AC for monster regression — the spider and any future monster still patrol/engage/attack/die/ragdoll

**Out of Scope:**
- Humanoid AI attacks, blocks, dodges, get-hit, death animations, ragdoll (Trigger* methods
  stub as no-op + warning on `HumanoidAIAnimationDriver` — implemented in a later story)
- Splitting `EntityBrain` into `MonsterBrain` / `HumanoidBrain` (defer until humanoid
  combat behaviors diverge — block, dodge, weapon draw)
- Adding humanoid AI enemies (goblin/skeleton prefabs are a separate epic)
- Damageable NPCs — `EntityHealth` is inherited on the NPC variant but no damage flow
  reaches NPCs today. Get-hit/death will warn-log via the humanoid driver stubs if anything
  ever calls them.

## Context for Development

### Codebase Patterns

**Abstract MonoBehaviour for polymorphic SerializeField reference** — Unity does not
serialize C# interface fields in the inspector. The idiomatic workaround is an abstract
MonoBehaviour base class that all concrete components inherit. The brain serializes
`[SerializeField] AIAnimationDriver _animationDriver;` and the inspector accepts any
component that derives from it.

**Field-type widening preserves prefab references** — Changing a `[SerializeField]` field's
declared type from a derived class (e.g. the old `EntityAnimationBridge`) to its base
(`AIAnimationDriver`) does **not** break the YAML reference, because Unity serializes the
referenced component by instance (fileID), not by declared field type. Smoke-test on
`Entity_base.prefab` after the field-type swap. (On the `NPC_base Variant`, the inherited
reference *will* become Missing — but that's intentional: the variant task removes the
inherited driver and replaces the reference with `HumanoidAIAnimationDriver`.)

**Class rename via file `mv` preserves prefab `m_Script` binding** — Unity binds a
MonoBehaviour to a script by the `.meta` GUID, NOT by class name. So
`mv EntityAnimationBridge.cs MonsterAnimationDriver.cs` (file + .meta in lockstep) keeps
the GUID intact and the prefab YAML's `m_Script: {fileID: 11500000, guid: bc1ff05bbb035a34cb7a7f54f833aa88}`
continues to resolve. The `m_EditorClassIdentifier` (`Game::Game.Animations.EntityAnimationBridge`)
will be stale until Unity re-saves the prefab, but it's informational only. Use plain Bash
`mv` (NOT `manage_asset(action="move")` — that tool is documented as unreliable in
project CLAUDE.md). Follow with `refresh_unity(mode="force")`.

**New `[SerializeField]` fields on a renamed class do NOT auto-populate on prefabs** —
After the rename, `MonsterAnimationDriver` has a new `_bridge` field. On `Entity_base.prefab`
this field will be `None` and must be wired manually. The old `_animator` field reference
becomes orphaned (the field no longer exists on the class) — Unity silently drops
orphaned serialized data on next save.

**`PlayerAnimationDriver` pattern** (`Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs`)
is the prior art for the humanoid math:
```csharp
Vector3 worldHoriz = new Vector3(velocity.x, 0f, velocity.z);
Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
float normX = Mathf.Clamp(localVelocity.x / _runSpeed, -1f, 1f);
float normZ = Mathf.Clamp(localVelocity.z / _runSpeed, -1f, 1f);
_humanoidBridge.SetMovement(normX, normZ);
```
The AI driver does the same but reads `_agent.velocity` instead of `CharacterController.velocity`,
and hard-codes `IsGrounded = true` / `IsRising = false` (navmesh agents stay on the navmesh).

**Existing brain locomotion smoothing** (`EntityBrain.HandleMovementAnimation`):
```csharp
float target = _agent.velocity.magnitude;
_smoothedAnimSpeed = Mathf.Lerp(_smoothedAnimSpeed, target, Time.deltaTime * 10f);
_animationBridge?.SetMoveSpeed(_smoothedAnimSpeed);
```
This smoothing is monster-specific (the 1D `Speed` parameter is unnormalized raw m/s).
Move it into `EntityAnimationBridge.DriveLocomotion` so the brain only calls
`_animationDriver?.DriveLocomotion(_agent)` and the per-driver math stays encapsulated.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs` | Animator vocabulary for humanoid (Player + AI). DO NOT modify in this spec. |
| `Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs` | RENAMED via `mv` → `MonsterAnimationDriver.cs`. The renamed class is also slimmed down — `_animator`/`SetMoveSpeed`/`Trigger*` move into the new `MonsterAnimationBridge`. |
| `Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs.meta` | Renamed in lockstep with the .cs via `mv` to preserve GUID `bc1ff05bbb035a34cb7a7f54f833aa88`. |
| `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs` | Reference implementation for the local-space → normalized velocity math. |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | Brain — swap `_animationBridge` → `_animationDriver`, delete `HandleMovementAnimation`. |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | Same field swap. |
| `Assets/_Game/Scripts/AI/SMB_DeathState.cs` | Update `GetComponentInParent` type to `AIAnimationDriver`. |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | Existing monster prefab — add `MonsterAnimationBridge` component, re-wire driver's new `_bridge` field. |
| `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` | Already inherits Brain/Health/NavMeshAgent + adds `HumanoidAnimationBridge`. Remove inherited driver, add `HumanoidAIAnimationDriver`, override brain/health field bindings. |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | Variant of `Entity_base` — inherits the new bridge automatically; verify no overrides break. |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | `BaseSpeed` / `EngageSpeed` / `AnimatorOverride` fields. |
| `Assets/_Game/Scripts/Player/CLAUDE.md` | Documents the humanoid animator contract (`VelocityX`/`VelocityZ`, no `Speed`/`IsLockedOn`). |

### Technical Decisions

1. **Abstract `MonoBehaviour` not interface.** Unity inspector requirement — see Codebase
   Patterns. The brain field is `[SerializeField] private AIAnimationDriver _animationDriver`.

2. **`EntityAnimationBridge` is split into `MonsterAnimationBridge` + `MonsterAnimationDriver`.**
   Achieves symmetry with the humanoid side (driver + pure bridge on both). The rename is
   done by file `mv` (preserves the `.meta` GUID `bc1ff05bbb035a34cb7a7f54f833aa88`), so
   the prefab's `m_Script` reference resolves to the renamed class automatically. The new
   `MonsterAnimationBridge` is a separate file with its own GUID and must be added to
   `Entity_base.prefab` manually (only one prefab — `Monster_DarknessSpider Variant` and
   `NPC_base Variant` inherit). Driver's old `_animator` field is dropped; new `_bridge`
   field replaces it and must be wired by hand on `Entity_base.prefab`.

3. **Speed smoothing moves from `EntityBrain` to `EntityAnimationBridge.DriveLocomotion`.**
   Per-driver math belongs in the driver. `EntityBrain.HandleMovementAnimation` and the
   `_smoothedAnimSpeed` field get deleted. The brain calls
   `_animationDriver?.DriveLocomotion(_agent)` unconditionally in `Update` and the driver
   decides what to do with the agent's velocity.

4. **`HumanoidAIAnimationDriver._runSpeed` is serialized, not pulled from Entity SO.**
   Keeps the driver loosely coupled. Default `4f` matches `Entity.EngageSpeed` default.
   Set per-prefab if a humanoid type uses a different agent speed cap. (Future story may
   add `Entity.AnimationRunSpeed` and have the driver read from it — out of scope today.)

5. **Combat triggers on `HumanoidAIAnimationDriver` are no-op stubs.** Calling
   `TriggerAttack` / `TriggerGetHit` / `TriggerDeath` / `EnableRagdoll` on a humanoid AI
   driver logs a warning and returns. This is intentional — humanoid AI combat is a
   separate epic. Stubs prevent NRE while letting `EntityHealth` / `EntityBrain` still
   compile against the abstract base.

6. **`IsGrounded = true`, `IsRising = false` always for AI humanoids.** NavMeshAgents
   don't leave the navmesh. If we add jumping AI later we'll revisit. For now, hardcoded.

7. **No new EditMode tests in this spec.** The abstraction is a wiring refactor —
   correctness comes from the existing 203 tests staying green plus the visual play-mode
   AC (humanoid NPC patrols + plays walk clip).

## Implementation Plan

### Tasks

> Order is dependency-first: base class → concrete drivers → consumers → prefab wiring → validation.

- [x] **Task 1: Create `AIAnimationDriver` abstract base** (AC: 1)
  - [ ] 1.1 Create `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`
  - [ ] 1.2 Namespace `Game.Animations`. `public abstract class AIAnimationDriver : MonoBehaviour`.
  - [ ] 1.3 Declare virtual methods (default no-op bodies):
    - `public virtual void DriveLocomotion(UnityEngine.AI.NavMeshAgent agent) { }`
    - `public virtual void TriggerAttack() { }`
    - `public virtual void TriggerGetHit() { }`
    - `public virtual void TriggerDeath() { }`
    - `public virtual void EnableRagdoll() { }`
  - [ ] 1.4 XML comment explaining: "Polymorphic seam between `EntityBrain` / `EntityHealth` and the entity's animator. Concrete subclasses: `EntityAnimationBridge` (monsters), `HumanoidAIAnimationDriver` (humanoid AI). The Player uses `PlayerAnimationDriver` instead — Player is not AI-driven and does not flow through this hierarchy."
  - [ ] 1.5 `read_console` — no compilation errors.

- [x] **Task 2: Create `MonsterAnimationBridge` (pure parameter wrapper)** (AC: 2)
  - [ ] 2.1 Create `Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs`.
  - [ ] 2.2 Namespace `Game.Animations`. `public class MonsterAnimationBridge : MonoBehaviour`.
  - [ ] 2.3 Mirror the surface of `HumanoidAnimationBridge` — pure parameter writes, no lifecycle:
    ```csharp
    private static readonly int SpeedHash  = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int GetHitHash = Animator.StringToHash("GetHit");
    private static readonly int DeathHash  = Animator.StringToHash("Death");

    [SerializeField] private Animator _animator;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    public Animator Animator => _animator;       // exposed so driver can read for ragdoll bone discovery

    public void SetMoveSpeed(float speed) { if (_animator != null) _animator.SetFloat(SpeedHash, speed); }
    public void TriggerAttack()  => _animator?.SetTrigger(AttackHash);
    public void TriggerGetHit()  => _animator?.SetTrigger(GetHitHash);
    public void TriggerDeath()   => _animator?.SetTrigger(DeathHash);
    ```
  - [ ] 2.4 XML class comment: "Pure animator-parameter wrapper for monster entities — 1D `Speed` locomotion + Attack/GetHit/Death triggers. Mirrors `HumanoidAnimationBridge`. Lifecycle (ragdoll, AnimatorOverride, death-component-disable) lives on `MonsterAnimationDriver`, not here."
  - [ ] 2.5 `read_console` — no compilation errors.

- [x] **Task 3: Rename `EntityAnimationBridge.cs` → `MonsterAnimationDriver.cs` and slim it down** (AC: 3, 8)
  - [ ] 3.1 **File `mv`** to preserve the `.meta` GUID — keeps `Entity_base.prefab`'s `m_Script` binding intact. Use Bash (NOT `manage_asset(action="move")`):
    ```bash
    mv "Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs"      "Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs"
    mv "Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs.meta" "Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs.meta"
    ```
    Then call `refresh_unity(mode="force")`.
  - [ ] 3.2 Edit the renamed file:
    - Change class signature to `public class MonsterAnimationDriver : AIAnimationDriver`
    - Update XML class comment: "Monster concrete `AIAnimationDriver`. Owns Speed smoothing, ragdoll-bone discovery, `Entity.AnimatorOverride` application, and death-component-disable lifecycle. Delegates animator parameter writes to `MonsterAnimationBridge`. Humanoid AI uses `HumanoidAIAnimationDriver` instead — its `Speed` value is in raw units and would saturate the humanoid 2D blend tree."
  - [ ] 3.3 **Remove** the following members from the renamed class (they live on `MonsterAnimationBridge` now):
    - `[SerializeField] private Animator _animator` field
    - `SpeedHash`, `AttackHash`, `GetHitHash`, `DeathHash` static readonly fields
    - The `SetMoveSpeed(float)`, `TriggerAttack`, `TriggerGetHit`, `TriggerDeath` methods
    - The `if (_animator == null) _animator = GetComponentInChildren<Animator>();` line from `Awake`
  - [ ] 3.4 **Add** the bridge reference field:
    ```csharp
    [SerializeField] private MonsterAnimationBridge _bridge;
    private const float SPEED_SMOOTHING_RATE = 10f;
    private float _smoothedAnimSpeed;
    ```
  - [ ] 3.5 Rewrite `Awake` — bridge resolution + existing lifecycle:
    ```csharp
    private void Awake()
    {
        if (_bridge == null) _bridge = GetComponent<MonsterAnimationBridge>();
        if (_bridge == null)
        {
            GameLog.Warn(TAG, $"{gameObject.name}: No MonsterAnimationBridge assigned — MonsterAnimationDriver is a no-op");
            return;
        }

        if (_persistentID != null && _persistentID.Entity != null && _persistentID.Entity.AnimatorOverride != null && _bridge.Animator != null)
            _bridge.Animator.runtimeAnimatorController = _persistentID.Entity.AnimatorOverride;

        if (_bridge.Animator != null)
        {
            _ragdollBodies = _bridge.Animator.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in _ragdollBodies) rb.isKinematic = true;
        }
    }
    ```
  - [ ] 3.6 Override `DriveLocomotion` (Speed smoothing moves here from `EntityBrain`):
    ```csharp
    public override void DriveLocomotion(NavMeshAgent agent)
    {
        if (_bridge == null || agent == null) return;
        float target = agent.velocity.magnitude;
        _smoothedAnimSpeed = Mathf.Lerp(_smoothedAnimSpeed, target, Time.deltaTime * SPEED_SMOOTHING_RATE);
        _bridge.SetMoveSpeed(_smoothedAnimSpeed);
    }
    ```
  - [ ] 3.7 Override the three trigger methods to delegate to the bridge:
    ```csharp
    public override void TriggerAttack() => _bridge?.TriggerAttack();
    public override void TriggerGetHit() => _bridge?.TriggerGetHit();
    public override void TriggerDeath()  => _bridge?.TriggerDeath();
    ```
  - [ ] 3.8 Override `EnableRagdoll` — same logic as before but reading `_bridge.Animator` instead of `_animator`:
    ```csharp
    public override void EnableRagdoll()
    {
        if (_ragdollActive) return;

        if (_ragdollBodies == null || _ragdollBodies.Length == 0)
        {
            DisableDeathComponents();
            return;
        }

        if (_bridge != null && _bridge.Animator != null) _bridge.Animator.enabled = false;
        foreach (var rb in _ragdollBodies) rb.isKinematic = false;

        _ragdollActive = true;
        DisableDeathComponents();
    }
    ```
  - [ ] 3.9 Keep `_persistentID`, `_componentsToDisableOnDeath`, `_ragdollBodies`, `_ragdollActive`, `DisableDeathComponents()`, and `TAG` unchanged.
  - [ ] 3.10 `read_console` — no compilation errors.

- [x] **Task 4: Create `HumanoidAIAnimationDriver`** (AC: 4, 5)
  - [ ] 4.1 Create `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs`.
  - [ ] 4.2 Namespace `Game.Animations`. `public class HumanoidAIAnimationDriver : AIAnimationDriver`.
  - [ ] 4.3 Add `[RequireComponent(typeof(HumanoidAnimationBridge))]`.
  - [ ] 4.4 Fields:
    ```csharp
    private const string TAG = "[AI]";
    [SerializeField] private HumanoidAnimationBridge _bridge;
    [Tooltip("Velocity at which the humanoid 2D blend tree shows the run clip (normalized = ±1.0). Set to match the entity's NavMeshAgent peak speed. Default 4f matches Entity.EngageSpeed default.")]
    [SerializeField] private float _runSpeed = 4f;
    ```
  - [ ] 4.5 `Awake()` resolves bridge: `if (_bridge == null) _bridge = GetComponent<HumanoidAnimationBridge>();` then null-guard with `GameLog.Warn` and `enabled = false` if `_runSpeed <= 0f`.
  - [ ] 4.6 Override `DriveLocomotion` (mirrors `PlayerAnimationDriver` math):
    ```csharp
    public override void DriveLocomotion(NavMeshAgent agent)
    {
        if (_bridge == null || agent == null) return;
        Vector3 worldHoriz = new Vector3(agent.velocity.x, 0f, agent.velocity.z);
        Vector3 localVelocity = transform.InverseTransformDirection(worldHoriz);
        float normX = Mathf.Clamp(localVelocity.x / _runSpeed, -1f, 1f);
        float normZ = Mathf.Clamp(localVelocity.z / _runSpeed, -1f, 1f);
        _bridge.SetMovement(normX, normZ);
        _bridge.SetGrounded(true);
        _bridge.SetRising(false);
    }
    ```
  - [ ] 4.7 Leave `TriggerAttack` / `TriggerGetHit` / `TriggerDeath` / `EnableRagdoll` as stubs that log once:
    ```csharp
    public override void TriggerAttack() => GameLog.Warn(TAG, $"{name}: humanoid AI attack not implemented yet");
    // etc — copy pattern for the other three.
    ```
  - [ ] 4.8 `read_console` — no compilation errors.

- [x] **Task 5: Update `EntityBrain` to consume the abstract driver** (AC: 6, 7)
  - [ ] 5.1 Change serialized field:
    - Remove: `[FormerlySerializedAs("_entityAnimator")] [SerializeField] private EntityAnimationBridge _animationBridge;`
    - Add: `[FormerlySerializedAs("_animationBridge")] [FormerlySerializedAs("_entityAnimator")] [SerializeField] private AIAnimationDriver _animationDriver;`
  - [ ] 5.2 Delete `private float _smoothedAnimSpeed;` field.
  - [ ] 5.3 Delete the entire `HandleMovementAnimation()` method.
  - [ ] 5.4 In `Update()`, replace the `HandleMovementAnimation();` call with:
    `if (_animationDriver != null) _animationDriver.DriveLocomotion(_agent);`
  - [ ] 5.5 Replace `_animationBridge?.TriggerAttack();` (in `ExecuteAttack`) with `_animationDriver?.TriggerAttack();`.
  - [ ] 5.6 `read_console` — no compilation errors.

- [x] **Task 6: Update `EntityHealth` to consume the abstract driver** (AC: 6)
  - [ ] 6.1 Change serialized field:
    - Remove: `[FormerlySerializedAs("_entityAnimator")] [SerializeField] private EntityAnimationBridge _animationBridge;`
    - Add: `[FormerlySerializedAs("_animationBridge")] [FormerlySerializedAs("_entityAnimator")] [SerializeField] private AIAnimationDriver _animationDriver;`
  - [ ] 6.2 Replace `_animationBridge?.TriggerGetHit();` → `_animationDriver?.TriggerGetHit();`.
  - [ ] 6.3 Replace `_animationBridge?.TriggerDeath();` → `_animationDriver?.TriggerDeath();`.
  - [ ] 6.4 `read_console` — no compilation errors.

- [x] **Task 7: Update `SMB_DeathState` to dispatch via abstract base** (AC: 6)
  - [ ] 7.1 In `OnStateExit`, change `GetComponentInParent<EntityAnimationBridge>()` → `GetComponentInParent<AIAnimationDriver>()`.
  - [ ] 7.2 Update the XML class comment — replace `EntityAnimationBridge` with `AIAnimationDriver` in the description.
  - [ ] 7.3 `read_console` — no compilation errors.

- [x] **Task 8: Migrate `Entity_base.prefab` to driver+bridge split** (AC: 8)
  - [ ] 8.1 Open `Assets/_Game/Prefabs/Entities/Entity_base.prefab` in Prefab Mode.
  - [ ] 8.2 Confirm the existing component at `m_Script: guid: bc1ff05bbb035a34cb7a7f54f833aa88` now shows as `MonsterAnimationDriver` in the inspector (the rename in Task 3 should auto-resolve). If it shows as Missing, force a reimport via `refresh_unity(mode="force")` and re-open.
  - [ ] 8.3 The driver's old `_animator` field is gone after Task 3 — verify Unity silently dropped the orphaned value (`m_Animator: {fileID: 6461208964627829605}` will be stripped on next save).
  - [ ] 8.4 Note the Animator fileID (`6461208964627829605` from current YAML, on the `Character` child) — you'll re-wire it to the bridge below.
  - [ ] 8.5 Add `MonsterAnimationBridge` component on the prefab root.
  - [ ] 8.6 Wire `MonsterAnimationBridge._animator` → the Animator on the Character child (fileID `6461208964627829605`).
  - [ ] 8.7 Wire `MonsterAnimationDriver._bridge` → the new `MonsterAnimationBridge` sibling component (drag-drop in inspector).
  - [ ] 8.8 Confirm `EntityBrain._animationDriver` and `EntityHealth._animationDriver` both still reference the `MonsterAnimationDriver` component (preserved by fileID + type widening via `[FormerlySerializedAs]`). If Missing, re-assign manually.
  - [ ] 8.9 Save the prefab via Editor.

- [x] **Task 9: Verify `Monster_DarknessSpider Variant.prefab` inherits cleanly** (AC: 10)
  - [ ] 9.1 Open `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` in Prefab Mode.
  - [ ] 9.2 Confirm `MonsterAnimationDriver` and `MonsterAnimationBridge` are both present (inherited from base).
  - [ ] 9.3 Confirm `MonsterAnimationDriver._bridge` resolves to the inherited bridge (no Missing reference).
  - [ ] 9.4 Confirm `MonsterAnimationBridge._animator` resolves to the variant's Animator. If the variant overrides the Character child, the bridge may need a per-variant override on `_animator` — check and re-wire if Missing.
  - [ ] 9.5 No code change needed here; this task is verification only.

- [x] **Task 10: Reconfigure `NPC_base Variant` to use the humanoid driver** (AC: 9)

  > Context: `NPC_base Variant.prefab` is a prefab variant of `Entity_base.prefab`, so it
  > already inherits `Transform`, `PersistentID`, `EntityHealth`, `EntityBrain`,
  > `NavMeshAgent` (from the base, fileID `3257318164010893370`), `MonsterAnimationDriver`
  > (post-Task-3 rename) and `MonsterAnimationBridge` (added on the base in Task 8). It
  > additionally adds `HumanoidAnimationBridge` and the NPC components (`NPCPresence`,
  > `NPCMemoryComponent`, `NPCDialogueGraphComponent`, `InventorySystem`, `GoldSystem`).
  > The work in this task is to **remove the inherited monster driver+bridge, add the
  > humanoid driver, and override the brain/health field bindings** — not to add
  > brain/health (already present).

  - [ ] 10.1 Open `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` in Prefab Mode (do NOT raw-YAML edit — `refresh_unity(mode="force")` would discard raw YAML changes per project CLAUDE.md).
  - [ ] 10.2 **Remove** the inherited `MonsterAnimationDriver` component on the variant (right-click → `Remove Component`; Unity writes this as `m_RemovedComponents` in the variant YAML). Required — `MonsterAnimationDriver.Awake()` would otherwise call `GetComponentsInChildren<Rigidbody>()` on the humanoid rig (forcing kinematics) and apply `Entity.AnimatorOverride` to the humanoid animator (the override expects a monster controller).
  - [ ] 10.3 **Remove** the inherited `MonsterAnimationBridge` component on the variant. Its `_animator` auto-resolves via `GetComponentInChildren<Animator>()` which would grab the humanoid animator inappropriately, and nothing on a humanoid drives the monster bridge's parameters.
  - [ ] 10.4 Add `HumanoidAIAnimationDriver` component on the variant root.
  - [ ] 10.5 Drag the existing sibling `HumanoidAnimationBridge` (fileID `3168899758345796514` in current YAML) into the driver's `_bridge` field.
  - [ ] 10.6 Set `_runSpeed = 4f` on the driver (matches `Entity.EngageSpeed` default; tune per-NPC variant if needed).
  - [ ] 10.7 On the inherited `EntityBrain` component, **override** `_animationDriver` to point at the new `HumanoidAIAnimationDriver` (after Task 10.2 the inherited reference is Missing). Captured as `propertyPath: _animationDriver` modification in variant YAML.
  - [ ] 10.8 On the inherited `EntityBrain` component, **override** `_canEngagePlayer = false`.
  - [ ] 10.9 On the inherited `EntityHealth` component, **override** `_animationDriver` to point at the new `HumanoidAIAnimationDriver`. `TriggerGetHit` / `TriggerDeath` are stubs — calls warn-log but won't NRE.
  - [ ] 10.10 Confirm `PersistentID.Entity` references a humanoid `Entity` SO. If `Assets/_Game/Data/Entities/` doesn't have one, create `Entity_HumanoidNPC.asset` (defaults: `_baseSpeed = 2`, `_engageSpeed = 4`, `_detectionRange = 0`, `_baseHealth = 100`, leave `_animatorOverride` null).
  - [ ] 10.11 Save the prefab via the Editor.

- [x] **Task 11: Variant override smoke test** (AC: 9)
  - [ ] 11.1 Reopen `NPC_base Variant.prefab` after a domain reload (`isCompiling: false`). Confirm the variant's component list shows: inherited (Transform, PersistentID, EntityHealth, EntityBrain, NavMeshAgent), added (NPCPresence, NPCMemoryComponent, NPCDialogueGraphComponent, InventorySystem, GoldSystem, HumanoidAnimationBridge), the **new** `HumanoidAIAnimationDriver`, and **neither** `MonsterAnimationDriver` nor `MonsterAnimationBridge`.
  - [ ] 11.2 Confirm `EntityBrain._animationDriver` and `EntityHealth._animationDriver` both show the `HumanoidAIAnimationDriver` reference (not "Missing", not "None").
  - [ ] 11.3 Confirm `EntityBrain._canEngagePlayer` is unchecked.
  - [ ] 11.4 `read_console` — no errors or warnings about missing references on the variant.

- [x] **Task 12: Play-mode validation** (AC: 9, 10, 11)
  - [ ] 12.1 Place an `NPC_base Variant` instance into a navmesh-baked test scene (`Assets/_Game/Scenes/CombatTestScene.unity` or `StartingTown` if NavMesh baked).
  - [ ] 12.2 Enter play mode. Confirm:
    - NPC stands still (idle clip plays — wired in `Humanoid_Template`).
    - No console errors / warnings from `HumanoidAIAnimationDriver.Awake()` or `EntityBrain.Awake()`.
  - [ ] 12.3 With the NPC selected, open the Animator window. Confirm `VelocityX`, `VelocityZ` are 0 and `IsGrounded` is `true`.
  - [ ] 12.4 (Optional) Assign 2–3 test waypoint Transforms to the NPC instance's `EntityBrain._waypoints` to confirm patrol works — 2D blend tree should show walking clip during motion, idle at each waypoint pause.
  - [ ] 12.5 Spawn a `Monster_DarknessSpider Variant` (or existing enemy) in a scene. Confirm patrol → engage → attack → get-hit → death → ragdoll all execute identically to before this spec. This is the critical monster-regression check.
  - [ ] 12.6 Run EditMode tests (`mcp__UnityMCP__run_tests`). All 203 pass.

- [x] **Task 13: Documentation** (AC: 12)
  - [ ] 13.1 Update `CLAUDE.md` (project root) — under "Learned Patterns & Gotchas" / "Animator, Camera & Player Script Rules", add a bullet: "AI animation polymorphism via `AIAnimationDriver` base class → see `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`."
  - [ ] 13.2 Create `Assets/_Game/Scripts/Core/Animations/CLAUDE.md` (or extend if exists) with:
    - Diagram: `EntityBrain/EntityHealth → AIAnimationDriver → {MonsterAnimationDriver → MonsterAnimationBridge | HumanoidAIAnimationDriver → HumanoidAnimationBridge} → Animator`
    - Rule: bridges are pure parameter wrappers (one method per animator parameter); drivers own lifecycle, smoothing, AnimatorOverride application, ragdoll. Symmetric on both sides.
    - Rule: never reference concrete bridge or driver types in AI code (`EntityBrain`, `EntityHealth`, `SMB_DeathState`) — always go through `AIAnimationDriver`.
    - Rule: Player goes through `PlayerAnimationDriver → HumanoidAnimationBridge`, NOT `AIAnimationDriver`. The Player is not AI; do not put it on the AI driver hierarchy.
    - Note: humanoid AI combat triggers are stubs — implement when the humanoid AI combat story is created.
    - Note: the `MonsterAnimationDriver` script GUID is `bc1ff05bbb035a34cb7a7f54f833aa88` (preserved from the original `EntityAnimationBridge.cs`) — do not regenerate the .meta or every monster prefab loses its driver reference.

### Acceptance Criteria

1. **Given** a fresh checkout, **when** the project compiles, **then** an
   `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs` exists declaring an
   `abstract class AIAnimationDriver : MonoBehaviour` in namespace `Game.Animations` with
   five virtual no-op methods: `DriveLocomotion(NavMeshAgent)`, `TriggerAttack`,
   `TriggerGetHit`, `TriggerDeath`, `EnableRagdoll`.

2. **Given** the split is complete, **when** the source tree is read, **then**:
   - `Assets/_Game/Scripts/Core/Animations/EntityAnimationBridge.cs` no longer exists.
   - `Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs` exists declaring a
     `class MonsterAnimationBridge : MonoBehaviour` with `[SerializeField] Animator _animator`,
     public `Animator Animator` accessor, and pure-parameter methods `SetMoveSpeed(float)`,
     `TriggerAttack`, `TriggerGetHit`, `TriggerDeath`. No ragdoll, override, or smoothing
     logic in this class.
   - `Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs` exists declaring a
     `class MonsterAnimationDriver : AIAnimationDriver` with `[SerializeField] MonsterAnimationBridge _bridge`,
     ragdoll/AnimatorOverride/death-component-disable logic, smoothing state, and the
     five `AIAnimationDriver` overrides.
   - The `MonsterAnimationDriver.cs.meta` GUID is `bc1ff05bbb035a34cb7a7f54f833aa88` —
     unchanged from the original `EntityAnimationBridge.cs.meta`.

3. **Given** the refactor is complete, **when** an enemy plays a frame in scene, **then**
   `MonsterAnimationDriver.DriveLocomotion` smooths `agent.velocity.magnitude` and forwards
   the smoothed value to `MonsterAnimationBridge.SetMoveSpeed` (matching the prior
   `EntityBrain.HandleMovementAnimation` behavior). `EntityBrain` no longer contains a
   `_smoothedAnimSpeed` field or `HandleMovementAnimation` method.

4. **Given** the refactor is complete, **when** a humanoid NPC is in scene, **then**
   `HumanoidAIAnimationDriver` exists at
   `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs`, inherits
   `AIAnimationDriver`, has `[RequireComponent(typeof(HumanoidAnimationBridge))]`, and its
   `DriveLocomotion` writes `VelocityX`/`VelocityZ`/`IsGrounded=true`/`IsRising=false`
   to the bridge from `agent.velocity` normalized in local space against `_runSpeed`.

5. **Given** a humanoid AI driver, **when** any of its `Trigger*` or `EnableRagdoll`
   methods is called, **then** it logs a one-shot `GameLog.Warn` and does nothing else
   (no NRE, no animator interaction) — explicitly out of scope for this spec.

6. **Given** `EntityBrain`, `EntityHealth`, and `SMB_DeathState` after the refactor, **when**
   their source is read, **then** they reference `AIAnimationDriver` and contain no
   reference to any concrete bridge or driver type (`MonsterAnimationDriver`,
   `MonsterAnimationBridge`, `HumanoidAnimationBridge`, `HumanoidAIAnimationDriver`,
   or the deleted `EntityAnimationBridge`).

7. **Given** an enemy after the split, **when** play mode starts, **then**
   patrol/engage/attack/get-hit/death/ragdoll work identically to before this spec —
   full monster behavior regression-free. (Validated via `Monster_DarknessSpider Variant`
   in Task 12.5.)

8. **Given** `Entity_base.prefab` after Task 8, **when** opened in the editor, **then**:
   - The component formerly known as `EntityAnimationBridge` is now identified as
     `MonsterAnimationDriver` (same fileID `3299999205233303053`, same script GUID).
   - A new `MonsterAnimationBridge` component exists on the prefab root with `_animator`
     wired to the Character child's Animator (fileID `6461208964627829605`).
   - `MonsterAnimationDriver._bridge` resolves to the new `MonsterAnimationBridge` sibling.
   - `EntityBrain._animationDriver` and `EntityHealth._animationDriver` both resolve to
     the `MonsterAnimationDriver` (preserved by fileID + `[FormerlySerializedAs("_animationBridge")]`).
   - No "Missing reference" warnings in the console.

9. **Given** `NPC_base Variant.prefab` after Tasks 10–11, **when** opened in Prefab Mode,
   **then** (a) its component list contains `HumanoidAIAnimationDriver` and does NOT
   contain `MonsterAnimationDriver` nor `MonsterAnimationBridge` (both removed via
   `m_RemovedComponents` against the base); (b) inherited `EntityBrain._animationDriver`
   and `EntityHealth._animationDriver` both reference the new `HumanoidAIAnimationDriver`;
   (c) `EntityBrain._canEngagePlayer` is `false`; (d) `PersistentID.Entity` references a
   humanoid Entity SO. **When** an instance is dropped into a navmesh-baked scene and
   play mode is entered, **then** the NPC visibly idles in place (idle clip from
   `Humanoid_Template` plays) with no console errors. Adding waypoints causes the NPC to
   walk between them with the 2D blend tree's walk clip playing during motion, returning
   to idle when stationary.

10. **Given** the existing monster prefab in `CombatTestScene` (or equivalent), **when**
    the player engages it, **then** patrol → engage → attack → death → ragdoll all execute
    with no regression versus the current behavior.

11. **Given** the refactor is complete, **when** `mcp__UnityMCP__run_tests` runs the
    EditMode suite, **then** all 203 tests pass — 0 regressions.

12. **Given** the refactor is complete, **when** a future contributor reads
    `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`, **then** they see the
    Brain → Driver → Bridge contract documented, with the "no concrete bridge types in
    AI code" rule explicit, and the project-root `CLAUDE.md` cross-links to it.

## Additional Context

### Dependencies

- No new packages.
- No new ScriptableObjects required (a humanoid test `Entity` SO may need to be created in
  Task 10.10 if one doesn't exist — use existing defaults).
- No changes to `HumanoidAnimationBridge`, `PlayerAnimationDriver`, `PlayerStateManager`,
  `Humanoid_Template.controller`, or any animation clip.

### Testing Strategy

- **EditMode tests**: existing 203 tests must pass. No new unit tests in this spec — the
  refactor is wiring-level. Driver math is too thin to unit-test usefully (one
  `Mathf.Clamp` + `InverseTransformDirection`; equivalent player code is not unit-tested
  either).
- **Play-mode smoke tests** (manual, in Task 12):
  - Humanoid NPC idles in place — no errors.
  - Humanoid NPC patrols between waypoints — walk clip plays.
  - `Monster_DarknessSpider Variant` patrol/engage/attack/death/ragdoll — no regression.
- **Prefab serialization smoke tests** (Tasks 8 and 11): two highest-risk steps —
  1. `Entity_base.prefab`: after the `mv` rename, the existing fileID `3299999205233303053`
     must continue to resolve to a valid script (now `MonsterAnimationDriver`). The new
     `MonsterAnimationBridge` must be added manually and wired into the driver's `_bridge`
     field; this is the only mandatory manual wiring on the monster side.
  2. `NPC_base Variant.prefab`: removing the inherited `MonsterAnimationDriver` and
     `MonsterAnimationBridge` via `m_RemovedComponents` is critical — leaving either in
     place causes runtime damage (kinematic ragdoll on humanoid rig, animator override
     mismatch).
  If anything goes Missing that shouldn't, manual re-assign and capture the gotcha as a
  CLAUDE.md candidate.

### Notes

- **`HumanoidAnimationBridge` is shared between Player and humanoid AI** by design — the
  bridge is pure animator-parameter vocabulary, it doesn't care whether the inputs come
  from a `CharacterController` or a `NavMeshAgent`. The two callers happen to be
  `PlayerAnimationDriver` and `HumanoidAIAnimationDriver`. Do not add AI-specific logic
  to the bridge.
- **Future story (humanoid AI combat) will:**
  - Add concrete implementations to the four `Trigger*` stubs on `HumanoidAIAnimationDriver`
    (probably via passing a trigger hash and forwarding to `HumanoidAnimationBridge.PlayAttack`).
  - Likely subclass `EntityBrain → HumanoidBrain` to override `ExecuteAttack` for combo
    selection and add Block/Dodge state branches. Until that happens, do not split.
  - Add ragdoll bones to humanoid character prefabs and implement `EnableRagdoll` on the
    humanoid driver (mirroring `MonsterAnimationDriver.EnableRagdoll`).
- **`Entity` SO `BaseSpeed`/`EngageSpeed`**: today the NavMeshAgent's `speed` is written by
  `EntityBrain.AdvanceToNextWaypoint` (`_agent.speed = _persistentID.Entity.BaseSpeed`) and
  `TransitionToEngaging` (`_engageSpeed`). So `_runSpeed` on the humanoid driver matches
  what the agent will actually reach. If a humanoid type has `EngageSpeed = 6` you must
  set the driver's `_runSpeed = 6` on that variant, or the blend tree saturates at 1.0
  before the agent reaches full speed.
- **Symmetric architecture, asymmetric risk**: both sides now have a clean driver + pure
  bridge split, but the migration paths differ. The humanoid side is greenfield — new
  driver, new wiring, no legacy. The monster side is a class split that has to preserve
  `Entity_base.prefab`'s existing `m_Script` reference (achieved via file `mv` keeping the
  `.meta` GUID) and manually re-wire the new `_bridge` field. Re-wiring is one-time work
  on one prefab — variants inherit. Test plan in Task 12.5 (spider regression) is the
  gate.
- **`Monster_DarknessSpider Variant.prefab` is the regression canary**. Currently the only
  monster variant in the project. If it patrols / engages / attacks / dies / ragdolls
  identically after the split, the refactor is correct. If any of those break, the bridge
  wiring on `Entity_base.prefab` is wrong (Task 8) or the inherited reference on the
  variant got Missing-ified (Task 9 catches this).

