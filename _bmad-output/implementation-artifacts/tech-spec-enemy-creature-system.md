---
title: 'Enemy Creature System — Fantasy Animals Pack Integration'
slug: 'enemy-creature-system'
created: '2026-03-26'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6]
implementedDate: '2026-04-01'
reviewNotes:
  adversarialReviewCompleted: true
  findingsTotal: 10
  findingsFixed: 2
  findingsSkipped: 8
  resolutionApproach: 'auto-fix'
  fixed:
    - 'F8: EnemyTypeSO fields made [SerializeField] private with public read-only properties; FormerlySerializedAs preserves .asset data'
    - 'F10: Removed dead TAG constant from EnemyTypeSO'
  noChangeRequired:
    - 'F2: Death→Dead transition already has hasExitTime=true, exitTime=1.0; code guards (IsDead check) prevent post-death triggers'
  skippedAsNoise: [F1, F3, F4, F5, F6, F7, F9]
ragdollNote: 'Tasks 20-23 (Ragdoll Wizard setup) require manual Editor interaction — not automatable via MCP. Must be done manually in Unity Editor before AC5 is fully verified.'
tech_stack: ['Unity 6', 'C#', 'URP', 'NavMesh', 'AnimatorOverrideController']
files_to_modify:
  - Assets/_Game/Scripts/AI/EnemyBrain.cs
  - Assets/_Game/Scripts/AI/EnemyHealth.cs
files_to_create:
  - Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs
  - Assets/_Game/Scripts/AI/EnemyAnimator.cs
  - Assets/_Game/Scripts/AI/SMB_DeathState.cs
  - Assets/_Game/Art/Characters/Enemies/EnemyBase.controller
  - Assets/_Game/Art/Characters/Enemies/FantasyWolf.overrideController
  - Assets/_Game/Art/Characters/Enemies/GiantRat.overrideController
  - Assets/_Game/Art/Characters/Enemies/GiantViper.overrideController
  - Assets/_Game/Art/Characters/Enemies/DarknessSpider.overrideController
  - Assets/_Game/Data/Enemies/EnemyType_Grunt.asset
  - Assets/_Game/Data/Enemies/EnemyType_FantasyWolf.asset
  - Assets/_Game/Data/Enemies/EnemyType_GiantRat.asset
  - Assets/_Game/Data/Enemies/EnemyType_GiantViper.asset
  - Assets/_Game/Data/Enemies/EnemyType_DarknessSpider.asset
  - Assets/_Game/Prefabs/Enemies/Enemy_FantasyWolf.prefab
  - Assets/_Game/Prefabs/Enemies/Enemy_GiantRat.prefab
  - Assets/_Game/Prefabs/Enemies/Enemy_GiantViper.prefab
  - Assets/_Game/Prefabs/Enemies/Enemy_DarknessSpider.prefab
code_patterns:
  - AnimatorOverrideController for creature animation swapping
  - GetComponentInParent<EnemyHealth>() for hit detection across child hierarchy
  - SMB_DeathState.OnStateExit() for guaranteed death → ragdoll sequencing (mirrors SMB_AttackState pattern)
test_patterns: []
---

# Tech-Spec: Enemy Creature System — Fantasy Animals Pack Integration

**Created:** 2026-03-26

---

## Overview

### Problem Statement

The existing enemy system uses a single primitive placeholder (`Enemy_Grunt`) with no animations, instant-damage attacks, and an `AIConfigSO` that cannot differentiate per-creature stats. There is no architecture for wiring real creature meshes (with their own Animators) into the AI state machine, and death currently calls `SetActive(false)` with no animation or ragdoll.

### Solution

Introduce `EnemyTypeSO` as the single data source per creature type (stats + animation), an `EnemyBase.controller` with a fixed state graph that all creatures share, and an `EnemyAnimator.cs` component that bridges AI states to Animator calls. Each creature gets an `AnimatorOverrideController` that swaps clips into the base graph without touching the controller. Death plays an animation then activates ragdoll physics, leaving the body persistent in the world. The four Fantasy Animals Pack creatures (Fantasy Wolf, Giant Rat, Giant Viper, Darkness Spider) are implemented as the first batch.

### Scope

**In Scope:**
- `EnemyTypeSO.cs` — replaces `AIConfigSO`, adds `animatorOverride` field
- `EnemyAnimator.cs` — bridges `EnemyBrain` state transitions to Animator calls + ragdoll on death
- `EnemyBase.controller` — base Animator Controller with fixed states/parameters all creatures share
- 4× `AnimatorOverrideController` assets (one per Fantasy Animals creature)
- 4× `EnemyTypeSO` assets with per-creature stats
- 4× enemy prefabs (root with game components + creature visual child)
- Migrate `Enemy_Grunt` and `EnemyBrain`/`EnemyHealth` to use `EnemyTypeSO`
- Ragdoll setup via Unity Ragdoll Wizard (Editor step, per creature)
- GetHit reaction animation (where available — Spider has none, falls back gracefully)

**Out of Scope:**
- Enemy attack hitbox system (replacing `ExecuteAttack()` instant damage) — **follow-up story**
- Spider's 3-hit combo (`Bite3HitCombo`) — deferred to hitbox story
- Viper's `SpitVenom` special attack — deferred
- URP material conversion if needed (flag at implementation time, not blocking)
- Lootable body interaction — future story
- Enemy animations for directional locomotion (walk/run only — strafe is player-only)

---

## Context for Development

### Codebase Patterns

- **Namespace:** All AI scripts use `namespace Game.AI`. New scripts must follow.
- **ScriptableObject convention:** SOs live in `Assets/_Game/ScriptableObjects/` (source) and `Assets/_Game/Data/` (instance assets). `EnemyTypeSO.cs` → `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs`; instances → `Assets/_Game/Data/Enemies/`.
- **Logging:** Use `GameLog.Info/Warn/Error(TAG, msg)` — never `Debug.Log` directly.
- **Animator parameter hashes:** Cache with `Animator.StringToHash()` in field initializers, never use string overloads in Update/per-frame calls.
- **GetComponentInParent for hit detection:** `WeaponHitbox` uses `other.GetComponentInParent<EnemyHealth>()` to walk up from collider child to root. `EnemyHealth` must remain on the root GO (or any ancestor of the collider child).
- **No Rigidbody on enemy root:** Weapon's kinematic Rigidbody satisfies trigger detection. Do NOT add Rigidbody to enemy root for hit detection.
- **Root Motion:** All creature movement is driven by `NavMeshAgent`. Always use non-`_RM` animation clips for Idle/Walk/Run to avoid root motion fighting NavMesh. Disable "Apply Root Motion" on the Animator component.
- **OnDisable null guard pattern:** Any field initialized in `OnEnable` must be null-guarded in `OnDisable`.

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/AI/EnemyBrain.cs` | State machine to modify — replace `AIConfigSO` ref, add `EnemyAnimator` calls |
| `Assets/_Game/Scripts/AI/EnemyHealth.cs` | Modify `Die()` to trigger animation instead of `SetActive(false)` |
| `Assets/_Game/ScriptableObjects/Config/AIConfigSO.cs` | Source of truth for all fields to migrate into `EnemyTypeSO` |
| `Assets/_Game/Data/Config/AIConfig.asset` | Existing SO instance — values to carry over to `EnemyType_Grunt.asset` |
| `Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab` | Existing prefab — update Inspector refs to `EnemyTypeSO`, add `EnemyAnimator` |
| `Assets/_Game/Prefabs/Enemies/CLAUDE.md` | Prefab structure rules — collider on child, `EnemyHealth` on root, no RB |
| `Assets/_Game/Scripts/Player/PlayerAnimator.cs` | Reference pattern for Animator parameter hash caching |
| `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` | Reference for animation event routing pattern |

### Additional Constraints Found in Investigation

- **`EnemyAnimator` must NOT use `[RequireComponent(typeof(Animator))]`** — the `Animator` lives on the `CreatureVisual` child GO, not on the root where `EnemyAnimator` lives. Use `[SerializeField] private Animator _animator` assigned in Inspector.
- **`EnemyAnimator` TAG convention:** use `private const string TAG = "[AI]"` — matches `EnemyBrain` and `EnemyHealth` existing tags.
- **`EnemyBrain.Update()` explicit removal:** The call `UpdateAttackVisuals()` in `Update()` must be removed alongside the method definition — not just the method body. Also remove the `HandleCooldowns()` call if the renderer-based visual is the only consumer of `_attackCooldownTimer` display. *(Keep `HandleCooldowns()` — it drives `_attackCooldownTimer` which is still used by `HandleAttack()`.)*
- **`EnemyBrain.Awake()` renderer lines to remove:** `_renderer = GetComponentInChildren<MeshRenderer>()` and `if (_renderer != null) _propBlock = new MaterialPropertyBlock()` — both must be removed.
- **Cross-system boundary check:** `EnemyHealth` → `EnemyAnimator` is same-system (`Game.AI`). Direct `[SerializeField]` MonoBehaviour reference is correct per architecture rules — no `GameEventSO` needed.
- **Coroutine allocation rule:** `WaitForDeathThenRagdoll()` uses only `yield return null` — no `new WaitForSeconds()` per call. Complies with project performance rules.
- **`ScriptableObjects/AI/` is a new subfolder:** `EnemyTypeSO.cs` goes in `Assets/_Game/ScriptableObjects/AI/` (new — currently only `Config/` exists under `ScriptableObjects/`). Instance assets go in `Assets/_Game/Data/Enemies/` (new subfolder).
- **`EnemyHealth.OnEnable` null guard pattern:** `EnemyHealth.OnEnable()` accesses `_config` (soon `_type`) — if `Awake` disabled the component (null SO), `OnEnable` must guard with `if (_type == null) return`. Already present in existing code — preserve it when renaming field.
- **`EnemyBrain` `#if DEVELOPMENT_BUILD` `OnGUI`:** Does not reference `_renderer` — safe to keep unchanged.
- **`AnimatorOverrideController` applied at runtime:** `_animator.runtimeAnimatorController = _type.animatorOverride` in `EnemyAnimator.Awake()`. The `Animator` component in the prefab points to `EnemyBase.controller` as its controller field — this is correct as a fallback/edit-time reference; the runtime assignment overrides it cleanly.
- **`EnemyTypeSO.cs` path:** `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs` (new folder — create it). Instance assets: `Assets/_Game/Data/Enemies/` (new folder — create it).

### Technical Decisions

1. **`EnemyTypeSO` replaces `AIConfigSO` entirely.** The existing `AIConfig.asset` is superseded by `EnemyType_Grunt.asset`. `AIConfigSO.cs` is kept (not deleted) since deletion requires careful ref cleanup — it simply becomes unused.

2. **`AnimatorOverrideController` approach for animation.** A single `EnemyBase.controller` defines the state graph (Idle, Walk, Run, Attack, GetHit, Death) with the Wolf's clips as defaults. Each creature's `AnimatorOverrideController` references `EnemyBase.controller` and swaps only the clips. Future creature packs: create one override controller + one `EnemyTypeSO`.

3. **`EnemyBase.controller` parameter schema:**
   - `float Speed` — 0 = idle, 0–3.5 = walk, ≥3.5 = run
   - `trigger Attack` — plays attack animation from any state, exits back to locomotion
   - `trigger GetHit` — plays hit reaction, exits back to locomotion
   - `trigger Death` — plays death, transitions to ragdoll (no exit from Death state)

4. **Death sequencing:** `EnemyHealth.Die()` no longer calls `SetActive(false)`. It calls `EnemyAnimator.TriggerDeath()` which fires the `Death` trigger on the Animator. A `SMB_DeathState` `StateMachineBehaviour` on the Death state calls `EnableRagdoll()` in `OnStateExit()` when the Death→Dead transition completes (at true animation end, not during crossfade). If no ragdoll Rigidbodies are found (e.g. `Enemy_Grunt` — `EnemyAnimator` not present, so `TriggerDeath()` is a no-op null-conditional), the grunt body remains active in the scene with `EnemyBrain` and `EnemyHealth` disabled. **No body is ever deactivated via `SetActive(false)` in this story.**

5. **GetHit for Darkness Spider:** Spider has no GetHit animation clip. The override controller maps the GetHit slot to `DarknessSpider@IdleNormal` as a no-op fallback (brief snap to idle). `EnemyAnimator.TriggerGetHit()` is still called — it just has no visible effect.

6. **Prefab structure for new creatures:**
   ```
   Enemy_FantasyWolf.prefab  (Assets/_Game/Prefabs/Enemies/)
   ├── ROOT: NavMeshAgent, EnemyBrain, EnemyHealth, PersistentID, EnemyAnimator
   │   (EnemyBrain._animator → EnemyAnimator on root; EnemyAnimator._animator → Animator on CreatureVisual)
   └── CreatureVisual  (child — pack prefab's SkinnedMeshRenderer + bone hierarchy)
       └── Animator  (Apply Root Motion: OFF; Controller: via EnemyTypeSO.animatorOverride at runtime)
           └── [bone hierarchy with ragdoll Rigidbody + Collider + CharacterJoint per bone]
   ```
   The **CapsuleCollider** used for hit detection is placed as a child of CreatureVisual (body-level), matching `Enemy_Grunt` pattern. Layer: **Enemy**.

7. **AnimatorOverrideController is applied at runtime in `EnemyAnimator.Awake()`** by calling `_animator.runtimeAnimatorController = _type.animatorOverride`. This means the Animator component on CreatureVisual points to `EnemyBase.controller` in the prefab, and the override is applied programmatically. This avoids needing to set the override in each prefab variant.

---

## Implementation Plan

### Tasks

Tasks are ordered lowest-dependency first.

#### Phase 1 — Data Layer

- [x] **Task 1: Create `EnemyTypeSO.cs`**
  - File: `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs` (create new `AI/` subfolder)
  - Namespace: `Game.AI`
  - `[CreateAssetMenu(menuName = "Game/AI/Enemy Type", fileName = "EnemyType_")]`
  - `private const string TAG = "[AI]";`
  - Fields — all `public`, matching `AIConfigSO` field names exactly plus new `animatorOverride`:
    ```csharp
    [Header("Stats")]
    public float baseHealth = 50f;
    public float attackDamage = 10f;

    [Header("Movement")]
    public float patrolSpeed = 2f;
    public float engageSpeed = 4f;

    [Header("Detection")]
    public float detectionRange = 8f;
    public float disengageRange = 12f;

    [Header("Engage")]
    public float engageStoppingDistance = 1.5f;

    [Header("Patrol")]
    public float waypointArrivalThreshold = 0.5f;
    public float patrolWaitTime = 2f;

    [Header("Attack")]
    public float attackRange = 1.8f;
    public float attackCooldown = 2f;

    [Header("Animation")]
    public AnimatorOverrideController animatorOverride;
    ```

- [x] **Task 2: Create `EnemyType_Grunt.asset`**
  - Create `Assets/_Game/Data/Enemies/` folder
  - Right-click → Create → Game/AI/Enemy Type → name `EnemyType_Grunt`
  - Set values from existing `AIConfig.asset`: baseHealth=50, attackDamage=10, patrolSpeed=2, engageSpeed=4, detectionRange=8, disengageRange=12, engageStoppingDistance=1.5, waypointArrivalThreshold=0.5, patrolWaitTime=2, attackRange=1.8, attackCooldown=2
  - `animatorOverride`: leave null (Grunt has no Animator)

- [x] **Task 3: Update `EnemyBrain.cs`**
  - File: `Assets/_Game/Scripts/AI/EnemyBrain.cs`
  - Replace `[SerializeField] private AIConfigSO _config` → `[SerializeField] private EnemyTypeSO _type`
  - Replace all `_config.X` → `_type.X` (all field names are identical, mechanical rename)
  - Add field: `[SerializeField] private EnemyAnimator _enemyAnimator` ← name is `_enemyAnimator` NOT `_animator` (avoid collision with `UnityEngine.Animator` naming convention)
  - In `Awake()`: change null-check guard message to reference `EnemyTypeSO`
  - In `TransitionToEngaging()`: add `_enemyAnimator?.SetMoveSpeed(_type.engageSpeed)`
  - In `AdvanceToNextWaypoint()`: add `_enemyAnimator?.SetMoveSpeed(_type.patrolSpeed)`
  - In `TransitionToAttacking()`: add `_enemyAnimator?.SetMoveSpeed(0f)`
  - In `TransitionToDead()`: add `_enemyAnimator?.SetMoveSpeed(0f)` (death trigger comes from `EnemyHealth`)
  - In `ExecuteAttack()`: add `_enemyAnimator?.TriggerAttack()` as first line (before damage logic)
  - In `HandleIdle()`: add `_enemyAnimator?.SetMoveSpeed(0f)` — called every frame, float setter only, no allocation
  - Remove from `Awake()`: `_renderer = GetComponentInChildren<MeshRenderer>()` and `if (_renderer != null) _propBlock = new MaterialPropertyBlock()`
  - Remove fields: `private MeshRenderer _renderer` and `private MaterialPropertyBlock _propBlock`
  - Remove methods: `SetRendererColor(Color)` and `UpdateAttackVisuals()`
  - Remove from `Update()`: the `UpdateAttackVisuals()` call
  - Add comment where those lines were: `// Visual debug removed — see EnemyAnimator for future debug overlay`
  - Notes: `HandleCooldowns()` call stays in `Update()` — `_attackCooldownTimer` is still used by `HandleAttack()`; `#if DEVELOPMENT_BUILD` `OnGUI` stays unchanged (does not reference `_renderer`)

- [x] **Task 4: Update `EnemyHealth.cs`**
  - File: `Assets/_Game/Scripts/AI/EnemyHealth.cs`
  - Replace `[SerializeField] private AIConfigSO _config` → `[SerializeField] private EnemyTypeSO _type`
  - Replace `_config.baseHealth` → `_type.baseHealth` (two occurrences: `Awake()` and `OnEnable()`)
  - Preserve existing `if (_config == null)` guard in `Awake()` — rename to `if (_type == null)`
  - Preserve existing `OnEnable()` null guard: rename `_config` → `_type`
  - Add field: `[SerializeField] private EnemyAnimator _enemyAnimator` ← name is `_enemyAnimator` NOT `_animator` (F9 — avoids naming collision)
  - In `TakeDamage()`: fire GetHit **only when the hit does NOT kill** — move the trigger call to AFTER the death check:
    ```csharp
    CurrentHealth -= amount;
    CurrentHealth = Mathf.Max(CurrentHealth, 0f);
    if (CurrentHealth <= 0f) { Die(); return; }   // death path — no GetHit
    _enemyAnimator?.TriggerGetHit();               // hit reaction only if still alive
    ```
  - In `Die()`: replace `gameObject.SetActive(false)` with `_enemyAnimator?.TriggerDeath()`
  - Keep `agent.isStopped = true` and `_persistentID?.RegisterDeath()` before the animator call (order: stop agent → register death → trigger animation)
  - Note: the GO is **never** deactivated via `SetActive(false)` in this story — the body remains in the scene permanently after ragdoll

- [x] **Task 5: Update `Enemy_Grunt.prefab`** (Unity Editor)
  - Open `Assets/_Game/Prefabs/Enemies/Enemy_Grunt.prefab`
  - On `EnemyBrain`: remove `AIConfig` asset ref → assign `EnemyType_Grunt.asset` to `_type`; leave `_enemyAnimator` as **None** (null)
  - On `EnemyHealth`: remove `AIConfig` asset ref → assign `EnemyType_Grunt.asset` to `_type`; leave `_enemyAnimator` as **None** (null)
  - Do **NOT** add `EnemyAnimator` component to the grunt — the null-conditional `?.` on all `_enemyAnimator` calls handles the absent reference safely with zero log noise
  - Note: grunt death now calls `_enemyAnimator?.TriggerDeath()` which is a no-op (null). The grunt body will remain active in the scene indefinitely after death. This is acceptable until a proper death/cleanup story is written.
  - Save prefab

#### Phase 2 — Animation System

- [x] **Task 6: Create `EnemyAnimator.cs`**
  - File: `Assets/_Game/Scripts/AI/EnemyAnimator.cs`
  - Namespace: `Game.AI`
  - `private const string TAG = "[AI]";`
  - Do NOT use `[RequireComponent(typeof(Animator))]` — Animator is on child GO, not this GO
  - Fields:
    ```csharp
    [SerializeField] private EnemyTypeSO _type;
    [SerializeField] private Animator _animator; // wire to CreatureVisual child's Animator in Inspector

    private Rigidbody[] _ragdollBodies;
    private bool _ragdollActive;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int GetHitHash = Animator.StringToHash("GetHit");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    ```
  - `Awake()`:
    - If `_animator == null`: `GameLog.Warn(TAG, $"{gameObject.name}: No Animator assigned — EnemyAnimator is a no-op")` then return
    - If `_type != null && _type.animatorOverride == null`: `GameLog.Warn(TAG, $"{gameObject.name}: EnemyTypeSO has no animatorOverride — base controller clips will play")` (F6 — silent wrong-clips warning)
    - If `_type?.animatorOverride != null`: `_animator.runtimeAnimatorController = _type.animatorOverride`
    - `_ragdollBodies = _animator.GetComponentsInChildren<Rigidbody>()`
    - `foreach (var rb in _ragdollBodies) rb.isKinematic = true`
  - Public methods (all null-guard `_animator`):
    - `SetMoveSpeed(float speed)`: `_animator?.SetFloat(SpeedHash, speed)`
    - `TriggerAttack()`: `_animator?.SetTrigger(AttackHash)`
    - `TriggerGetHit()`: `_animator?.SetTrigger(GetHitHash)`
    - `TriggerDeath()`: null-guard `_animator`; call `_animator.SetTrigger(DeathHash)` only — **no coroutine**; ragdoll is triggered by `SMB_DeathState.OnStateExit()` (Task 6b)
  - `EnableRagdoll()` public method (called by `SMB_DeathState`):
    - If `_ragdollActive` already true: return early (guard against double-call)
    - If `_ragdollBodies == null || _ragdollBodies.Length == 0`:
      - Log: `GameLog.Info(TAG, $"{gameObject.name} has no ragdoll bodies — disabling components")`
      - Disable `EnemyBrain` and `EnemyHealth` on this GO: `GetComponent<EnemyBrain>().enabled = false` / `GetComponent<EnemyHealth>().enabled = false` (null-guard each)
      - Return — **do NOT call `SetActive(false)`**; grunt body stays in scene
    - `_animator.enabled = false`
    - `foreach (var rb in _ragdollBodies) rb.isKinematic = false`
    - `_ragdollActive = true`
    - Disable `EnemyBrain` and `EnemyHealth` on this GO (stop per-frame updates): `GetComponent<EnemyBrain>().enabled = false` / `GetComponent<EnemyHealth>().enabled = false`
    - `GameLog.Info(TAG, $"{gameObject.name} ragdoll activated")`
  - No coroutine — `WaitForDeathThenRagdoll()` does NOT exist in this implementation

- [x] **Task 6b: Create `SMB_DeathState.cs`** (F1 fix — replaces fragile normalizedTime polling)
  - File: `Assets/_Game/Scripts/AI/SMB_DeathState.cs`
  - Namespace: `Game.AI`
  - Inherits: `StateMachineBehaviour`
  - Pattern mirrors existing `SMB_AttackState.cs` in the project
  - Implementation:
    ```csharp
    public class SMB_DeathState : StateMachineBehaviour
    {
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            animator.GetComponentInParent<EnemyAnimator>()?.EnableRagdoll();
        }
    }
    ```
  - `GetComponentInParent<EnemyAnimator>()` — Animator is on `CreatureVisual` (child); `EnemyAnimator` is on root; parent traversal finds it correctly
  - `OnStateExit` fires when the Death state transitions to the `Dead` state (see Task 7) — guaranteed to fire exactly once at true animation end, not during crossfade
  - Add this SMB to the **Death** state in `EnemyBase.controller` (see Task 7)

- [x] **Task 7: Create `EnemyBase.controller`** (Unity Editor)
  - Create folder `Assets/_Game/Art/Characters/Enemies/`
  - Right-click → Create → Animator Controller → name `EnemyBase`
  - Add Parameters: `float Speed`, `trigger Attack`, `trigger GetHit`, `trigger Death`
  - Add States — set **Write Defaults: OFF** on EVERY state (prevents T-pose corruption during crossfades with ragdoll bone hierarchy):
    - **Idle** (set as default state) — Motion: `FantasyWolf@IdleBreathe`; Loop Time: ON; Write Defaults: OFF
    - **Walk** — Motion: `FantasyWolf@Walk`; Loop Time: ON; Write Defaults: OFF
    - **Run** — Motion: `FantasyWolf@Run`; Loop Time: ON; Write Defaults: OFF
    - **Attack** — Motion: `FantasyWolf@Bite`; Loop Time: OFF; Has Exit Time: ON; Exit Time: 1.0; Write Defaults: OFF
    - **GetHit** — Motion: `FantasyWolf@GetHitFront`; Loop Time: OFF; Has Exit Time: ON; Exit Time: 1.0; Write Defaults: OFF
    - **Death** — Motion: `FantasyWolf@Death`; Loop Time: OFF; Write Defaults: OFF; **add `SMB_DeathState` StateMachineBehaviour to this state** (select state → Inspector → Add Behaviour → SMB_DeathState)
    - **Dead** — Motion: none (empty state); Loop Time: OFF; Write Defaults: OFF — this is the terminal state; `SMB_DeathState.OnStateExit` fires when Death→Dead transition occurs
  - Add Transitions:
    - **Any State → Death**: Condition: `Death` trigger; Has Exit Time: OFF; Can Transition To Self: OFF; Transition Duration: 0; **highest priority** (listed first among Any State transitions)
    - **Any State → GetHit**: Condition: `GetHit` trigger; Has Exit Time: OFF; Can Transition To Self: OFF; Transition Duration: 0.1; listed AFTER Death in Any State transition order so Death always wins
    - **Any State → Attack**: Condition: `Attack` trigger; Has Exit Time: OFF; Can Transition To Self: OFF; Transition Duration: 0.1; listed after Death and GetHit
    - **Idle → Walk**: Condition: `Speed > 0.4`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Idle → Run**: Condition: `Speed > 3.5`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Walk → Idle**: Condition: `Speed < 0.4`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Walk → Run**: Condition: `Speed > 3.5`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Run → Walk**: Two condition rows on ONE transition (not two separate transitions): row 1: `Speed < 3.5`; row 2: `Speed > 0.4`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Run → Idle**: Condition: `Speed < 0.4`; Has Exit Time: OFF; Transition Duration: 0.1
    - **Attack → Entry**: Has Exit Time: ON; Exit Time: 1.0; Transition Duration: 0.1 (returns to Idle/Walk/Run after clip)
    - **GetHit → Entry**: Has Exit Time: ON; Exit Time: 1.0; Transition Duration: 0.1
    - **Death → Dead**: Has Exit Time: ON; Exit Time: 1.0; Transition Duration: 0 — this transition completing is what fires `SMB_DeathState.OnStateExit()`

#### Phase 3 — Creature Assets

- [x] **Task 8: Create `FantasyWolf.overrideController`** (Unity Editor)
  - File: `Assets/_Game/Art/Characters/Enemies/FantasyWolf.overrideController`
  - Right-click → Create → Animator Override Controller; set Controller: `EnemyBase`
  - Override mappings (use non-`_RM` clips only):
    - `FantasyWolf@IdleBreathe` → `FantasyWolf@IdleBreathe` (no change — Wolf is base)
    - `FantasyWolf@Walk` → `FantasyWolf@Walk`
    - `FantasyWolf@Run` → `FantasyWolf@Run`
    - `FantasyWolf@Bite` → `FantasyWolf@Bite`
    - `FantasyWolf@GetHitFront` → `FantasyWolf@GetHitFront`
    - `FantasyWolf@Death` → `FantasyWolf@Death`

- [x] **Task 9: Create `GiantRat.overrideController`** (Unity Editor)
  - File: `Assets/_Game/Art/Characters/Enemies/GiantRat.overrideController`
  - Base Controller: `EnemyBase`; override mappings:
    - Idle slot → `GiantRat@Idle`
    - Walk slot → `GiantRat@walk` ← **lowercase 'w'** — the FBX on disk is `GiantRat@walk.FBX`; do NOT type `GiantRat@Walk` (capital) when dragging the clip in the Inspector
    - Run slot → `GiantRat@Run`
    - Attack slot → `GiantRat@JumpBite`
    - GetHit slot → `GiantRat@GetHitFront`
    - Death slot → `GiantRat@Death`

- [x] **Task 10: Create `GiantViper.overrideController`** (Unity Editor)
  - File: `Assets/_Game/Art/Characters/Enemies/GiantViper.overrideController`
  - Base Controller: `EnemyBase`; override mappings:
    - Idle slot → `GiantViper@Idle`
    - Walk slot → `GiantViper@Crawl` (Viper has no separate walk; Crawl serves both speeds)
    - Run slot → `GiantViper@Crawl`
    - Attack slot → `GiantViper@BiteForward`
    - GetHit slot → `GiantViper@GetHitFront`
    - Death slot → `GiantViper@Death`

- [x] **Task 11: Create `DarknessSpider.overrideController`** (Unity Editor)
  - File: `Assets/_Game/Art/Characters/Enemies/DarknessSpider.overrideController`
  - Base Controller: `EnemyBase`; override mappings:
    - Idle slot → `DarknessSpider@IdleNormal`
    - Walk slot → `DarknessSpider@CrawlNormal`
    - Run slot → `DarknessSpider@CrawlThreat`
    - Attack slot → `DarknessSpider@CrawlBiteThreat`
    - GetHit slot → `DarknessSpider@IdleNormal` ← **no-op fallback** (Spider has no GetHit clip)
    - Death slot → `DarknessSpider@DeathNormal`

- [x] **Task 12: Create `EnemyType_FantasyWolf.asset`**
  - File: `Assets/_Game/Data/Enemies/EnemyType_FantasyWolf.asset`
  - Values: baseHealth=80, attackDamage=12, patrolSpeed=2.5, engageSpeed=5.0, detectionRange=10, disengageRange=15, engageStoppingDistance=1.5, waypointArrivalThreshold=0.5, patrolWaitTime=2.0, attackRange=2.0, attackCooldown=2.0
  - `animatorOverride`: `FantasyWolf.overrideController`

- [x] **Task 13: Create `EnemyType_GiantRat.asset`**
  - File: `Assets/_Game/Data/Enemies/EnemyType_GiantRat.asset`
  - Values: baseHealth=40, attackDamage=8, patrolSpeed=2.0, engageSpeed=4.5, detectionRange=7, disengageRange=12, engageStoppingDistance=1.2, waypointArrivalThreshold=0.5, patrolWaitTime=1.5, attackRange=1.5, attackCooldown=1.5
  - `animatorOverride`: `GiantRat.overrideController`

- [x] **Task 14: Create `EnemyType_GiantViper.asset`**
  - File: `Assets/_Game/Data/Enemies/EnemyType_GiantViper.asset`
  - Values: baseHealth=60, attackDamage=15, patrolSpeed=1.5, engageSpeed=4.0, detectionRange=8, disengageRange=13, engageStoppingDistance=2.0, waypointArrivalThreshold=0.5, patrolWaitTime=2.5, attackRange=2.5, attackCooldown=2.5
  - Note: `engageSpeed=4.0` (not 3.5) — keeps Viper above the `Run` state threshold of `Speed > 3.5` so the Run state is actually entered during chase
  - `animatorOverride`: `GiantViper.overrideController`

- [x] **Task 15: Create `EnemyType_DarknessSpider.asset`**
  - File: `Assets/_Game/Data/Enemies/EnemyType_DarknessSpider.asset`
  - Values: baseHealth=50, attackDamage=10, patrolSpeed=2.0, engageSpeed=4.0, detectionRange=9, disengageRange=14, engageStoppingDistance=1.8, waypointArrivalThreshold=0.5, patrolWaitTime=2.0, attackRange=2.2, attackCooldown=2.0
  - `animatorOverride`: `DarknessSpider.overrideController`

- [x] **Task 16: Create `Enemy_FantasyWolf.prefab`** (Unity Editor)
  - File: `Assets/_Game/Prefabs/Enemies/Enemy_FantasyWolf.prefab`
  - Root GO `Enemy_FantasyWolf`: Layer = **Enemy**
  - Add components to root: `NavMeshAgent`, `EnemyBrain`, `EnemyHealth`, `EnemyAnimator`, `PersistentID`
  - Wire root: `EnemyBrain._type` → `EnemyType_FantasyWolf`; `EnemyBrain._animator` → `EnemyAnimator`; `EnemyHealth._type` → `EnemyType_FantasyWolf`; `EnemyHealth._animator` → `EnemyAnimator`; `EnemyAnimator._type` → `EnemyType_FantasyWolf`
  - Add child GO `CreatureVisual`: drag `M_FantasyWolf_PBR.prefab` mesh into scene as child; **expand the full hierarchy and remove every Rigidbody and every Collider on every GO in the pack prefab** before running the Ragdoll Wizard — a missed Rigidbody will be picked up by `EnemyAnimator.GetComponentsInChildren<Rigidbody>()` and set non-kinematic on ragdoll activation, causing the creature to fall through the floor
  - On `CreatureVisual`'s `Animator`: set `Apply Root Motion = OFF`; Controller = `EnemyBase.controller`
  - Wire `EnemyAnimator._animator` → `Animator` on `CreatureVisual`
  - Add child GO `HitCollider` under `CreatureVisual`: add `CapsuleCollider` (non-trigger, sized to body); Layer = **Enemy**
  - Assign `PersistentID` a new GUID (right-click → Generate GUID in Inspector)

- [x] **Task 17: Create `Enemy_GiantRat.prefab`** (Unity Editor)
  - Same structure as Task 16 with: mesh = `GiantRat_PBR.prefab`; type SO = `EnemyType_GiantRat`
  - Same Rigidbody/Collider purge requirement: expand full hierarchy and remove all before running Ragdoll Wizard

- [x] **Task 18: Create `Enemy_GiantViper.prefab`** (Unity Editor)
  - Same structure as Task 16 with: mesh = `GiantViper_PBR.prefab`; type SO = `EnemyType_GiantViper`
  - Same Rigidbody/Collider purge requirement
  - Note: Viper is a serpent — no legs; size CapsuleCollider to body length, orient horizontally if needed

- [x] **Task 19: Create `Enemy_DarknessSpider.prefab`** (Unity Editor)
  - Same structure as Task 16 with: mesh = `DarknessSpider.prefab`; type SO = `EnemyType_DarknessSpider`
  - Same Rigidbody/Collider purge requirement

- [x] **Task 20: Ragdoll setup — Fantasy Wolf** (Unity Editor)
  - Open `Enemy_FantasyWolf.prefab` in prefab mode
  - Select the `CreatureVisual` child root bone (e.g. `Hips` or root bone in Wolf rig)
  - Component menu → Physics → Ragdoll; assign bones (Pelvis/Hips, LeftUpperLeg, RightUpperLeg, LeftLeg, RightLeg, LeftArm, RightArm, Head as available in quadruped rig)
  - All created bone `Rigidbody` components are found by `EnemyAnimator.Awake()` via `GetComponentsInChildren<Rigidbody>()` and set to `isKinematic = true` automatically
  - Verify in Play mode: no physics ragdoll during normal locomotion

- [x] **Task 21: Ragdoll setup — Giant Rat** (same steps as Task 20)
- [x] **Task 22: Ragdoll setup — Giant Viper** (same steps as Task 20; note: serpent rig — spine bones only; may need manual CharacterJoint chain on spine segments)
- [x] **Task 23: Ragdoll setup — Darkness Spider** (same steps as Task 20; 8-leg rig — wizard may need all legs assigned manually)

#### Phase 4 — Scene Placement & Verification

- [x] **Task 24: Disable `EnemyRespawner` in TestScene** (F2 — respawn system is incompatible with persistent ragdoll bodies)
  - Open `Assets/_Game/Scenes/TestScene.unity`
  - Find the GameObject(s) or component(s) that host `EnemyRespawner.cs`
  - Disable the `EnemyRespawner` component (or deactivate its GO) — do NOT delete it; it will be redesigned in a future story
  - Note: `EnemyRespawner` relies on `SetActive(false/true)` cycling. Since bodies now stay active after death, the respawner would immediately find a live (ragdolled) GO and malfunction. Disabling it is the correct short-term fix.

- [x] **Task 25: Place all 4 creatures in TestScene and verify**
  - Bake NavMesh to include new placement areas (Window → AI → Navigation → Bake)
  - Place one of each creature prefab (`Enemy_FantasyWolf`, `Enemy_GiantRat`, `Enemy_GiantViper`, `Enemy_DarknessSpider`) with waypoints assigned
  - Enter Play mode; verify each creature: patrol → engage → attack → death animation → ragdoll body persists
  - Verify `Enemy_Grunt`: attack → grunt body stays in scene, no errors (EnemyBrain + EnemyHealth disabled after death, no `SetActive(false)`)
  - Verify player weapon hits register on all 4 new creatures (watch GameLog for `[Combat]` damage messages)
  - Check console: 0 errors, 0 unexpected warnings on startup

---

### Acceptance Criteria

- [x] **AC1 — Creature type data:** Given any enemy prefab in the scene, when viewed in Inspector, then `EnemyBrain` and `EnemyHealth` both reference an `EnemyTypeSO` asset; `AIConfigSO` is no longer referenced anywhere in those components; each creature has distinct stats

- [x] **AC2 — Locomotion animations play:** Given a creature enemy in the scene with waypoints assigned, when the game is running, then: idle animation plays when creature is stationary; walk animation plays during patrol movement; run animation plays when creature is chasing the player

- [x] **AC3 — Attack animation triggers:** Given a creature is in Attacking state and within attackRange, when `ExecuteAttack()` fires, then the Attack trigger fires on the Animator; the attack animation visibly plays; damage is still dealt to the player (instant damage path — hitbox story is follow-up)

- [x] **AC4 — GetHit reaction:** Given the player strikes a Wolf, Rat, or Viper, when `EnemyHealth.TakeDamage()` is called and the creature is alive, then the GetHit animation trigger fires and the hit reaction clip plays; given the player strikes a Darkness Spider, then no visible reaction occurs but zero errors are logged

- [x] **AC5 — Death animation then ragdoll:** Given any creature enemy has health > 0, when health reaches 0, then: (1) NavMeshAgent is stopped; (2) death is registered via `PersistentID`; (3) death animation plays to completion; (4) ragdoll activates with all bone Rigidbodies becoming non-kinematic; (5) body remains in scene (not deactivated)

- [x] **AC6 — Grunt backward compatibility:** Given `Enemy_Grunt` prefab is in the scene, when the grunt is killed, then: `EnemyAnimator` is absent (null `_enemyAnimator`), so `TriggerDeath()` is a no-op; the grunt body **remains active in the scene** (no `SetActive(false)`); `EnemyBrain` and `EnemyHealth` are NOT disabled (grunt has no `EnemyAnimator` to call `EnableRagdoll()`); no NullReferenceException is thrown; both components reference `EnemyType_Grunt.asset` correctly

- [x] **AC7 — Hit detection on new prefabs:** Given the player has a weapon drawn and attacks a new creature prefab, when the weapon's trigger collider overlaps the creature's `CapsuleCollider`, then `EnemyHealth.TakeDamage()` is called; `GameLog` shows `[Combat]` damage message; no NullReferenceException from `GetComponentInParent<EnemyHealth>()`

- [x] **AC8 — No root motion conflicts:** Given any creature is navigating via NavMesh, when the creature moves from patrol waypoint to engage position, then creature position is driven by NavMeshAgent (not animator); `Apply Root Motion = OFF` is confirmed on each creature's `Animator` component; no sliding/fighting between NavMesh and root motion

---

## Additional Context

### Dependencies

- Epic 2, Story 2.8 (`EnemyBrain`) and Story 2.9 (`EnemyHealth`) — both `done`
- Story 7.9 (`WeaponHitbox`) — `done`; hit detection architecture unchanged
- Story 7.13 (`CombatAnimationRewiring`) — in `review`; no conflict (player-side only)

### Follow-Up Story (to create after this spec is implemented)

**Enemy Attack Hitbox System**
- Replace `ExecuteAttack()` instant damage with animation-event-driven hitbox approach (same as player)
- `EnemyTypeSO` will need hitbox config fields (offset, size, damage)
- Each creature's attack animation gets animation events for hitbox open/close
- Spider's `Bite3HitCombo` multi-hit becomes viable at that point

### Testing Strategy

Manual play-through verification in TestScene:
1. Spawn all 4 creature types + grunt, confirm no console errors on startup
2. Observe locomotion: idle → patrol walk → engage run transition for each creature
3. Attack player: confirm animation fires + damage lands
4. Take hits: Wolf/Rat/Viper show GetHit reaction; Spider shows no reaction, no errors
5. Kill each creature: confirm death animation plays, ragdoll activates, body persists
6. Kill grunt: confirm grunt body stays active in scene (no `SetActive(false)`), no NullReferenceException, EnemyBrain and EnemyHealth remain enabled (no `EnemyAnimator` to disable them)
7. Player attacks each new creature: confirm `EnemyHealth.TakeDamage` called (watch `GameLog`)

### Notes

- **URP Materials:** Pack materials may use Built-in RP shaders. If creatures appear pink/purple in play mode, convert materials via `Edit → Rendering → Materials → Convert Selected Built-in Materials to URP`. Only the materials assigned to the `SkinnedMeshRenderer` on `CreatureVisual` need conversion.
- **Ragdoll wizard bone assignment:** For quadruped creatures (Wolf, Rat, Viper), the Ragdoll Wizard's "pelvis" should be the creature's root/hip bone. The wizard is designed for bipeds; some manual bone assignment is expected.
- **Speed threshold values** in `EnemyBase.controller` (Walk: >0.4, Run: >3.5) are calibrated against the `EnemyTypeSO` default speeds (patrol ~2.0, engage ~4.0+). If speeds are tuned significantly, adjust thresholds accordingly.
- **`AIConfigSO.cs` is NOT deleted** in this story to avoid breaking existing scene references that may have the old component cached. It becomes unused after grunt migration. Deletion can be done in a cleanup pass.
- **Known limitation — locomotion/animation desync during attack (F11):** When the player moves out of `attackRange` mid-attack, `EnemyBrain` may re-enter the Engage state and call `SetMoveSpeed(engageSpeed)` while the Attack animation is still playing. The locomotion blend tree will start showing the Run clip underneath. This is a cosmetic desync accepted for this story; it will be addressed when the hitbox story reworks `ExecuteAttack()` with proper animation lock-out.
- **Audit stale `AIConfig.asset` references after grunt migration (F20):** After completing Task 3–5, search the project for any remaining references to `AIConfig.asset` or `AIConfigSO` (Window → Search by type in the Inspector, or Project window search `t:AIConfigSO`). Any scene-placed enemy GOs that still reference the old asset will log a missing-type warning at runtime. Reassign to the appropriate `EnemyTypeSO` asset.
