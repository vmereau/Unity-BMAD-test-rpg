---
title: 'Humanoid AI Death, Ragdoll & Hit-React Wiring'
slug: 'humanoid-ai-death-ragdoll'
created: '2026-05-23'
status: 'implementation-complete-pending-manual-steps'
stepsCompleted: [1, 2, 3, 4, 5, 6]
reviewNotes:
  findings: 5
  fixed: 1
  skipped: 4
  approach: auto-fix
  details:
    - F1 (Medium, fixed): new Death/Dead/GetHit states m_WriteDefaultValues set to 0 to match humanoid convention (was 1, copied from monster reference)
    - F2 (Low, skipped): HumanoidAnimationBridge.Animator property — minor DX, cold path only
    - F3 (Low, accepted): public bridge triggers reachable from Player code by discipline only — documented as pre-mortem risk
    - F4 (Low, skipped): YAML m_Name trailing-space cosmetics — Unity normalizes on next save
    - F5 (Info): _ragdollBodies length-0 fallback is the documented graceful path for un-authored humanoid variants
tech_stack:
  - Unity 6000.3.10f1 (URP) — Unity 6.3 LTS
  - C# (Game asmdef)
  - Unity Animator + StateMachineBehaviour
  - Unity Ragdoll Wizard (manual editor step) → Rigidbody + CharacterJoint + Collider per bone
  - Unity NavMesh / NavMeshAgent
files_to_modify:
  - Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs (implement TriggerDeath / TriggerGetHit / EnableRagdoll; add ragdoll cache + components-to-disable-on-death array)
  - Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs (add TriggerDeath / TriggerGetHit + Death/GetHit hash constants)
  - Assets/_Game/Scripts/AI/NPCPresence.cs (cache EntityHealth, early-return Interact() when dead, TODO loot-corpse comment)
  - Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller (add Death/Dead/GetHit AnimatorStates + AnyState transitions + SMB_DeathState behaviour block)
  - Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab (author ragdoll bones as overrides on Character child; wire _componentsToDisableOnDeath array)
code_patterns:
  - MonsterAnimationDriver ragdoll lifecycle (GetComponentsInChildren<Rigidbody>() in Awake → isKinematic=true; on EnableRagdoll set Animator.enabled=false, isKinematic=false, disable listed components)
  - SMB_DeathState.OnStateExit → animator.GetComponentInParent<AIAnimationDriver>().EnableRagdoll()
  - EntityHealth.Die() flow: NavMeshAgent.isStopped=true + PersistentID.RegisterDeath() + driver.TriggerDeath() — already generic, no change
  - HumanoidAnimationBridge owns ALL Animator.Set* writes for humanoids (Player CLAUDE.md rule) — new triggers MUST be added there, not in driver
  - OnDisable null-guard pattern when fields are initialized in OnEnable (CLAUDE.md root)
  - GameLog.{Info,Warn,Error}(TAG, msg) with private const string TAG (project-context.md rule)
test_patterns:
  - Manual play-mode verification only (no Edit/PlayMode test infra for AI death; project's existing AI tests are limited)
---

# Tech-Spec: Humanoid AI Death, Ragdoll & Hit-React Wiring

**Created:** 2026-05-23

## Overview

### Problem Statement

Humanoid AI entities (the current `NPC_base Variant` prefab and any future humanoid enemy)
do not visibly die when `EntityHealth.CurrentHealth` reaches zero. `HumanoidAIAnimationDriver.
TriggerDeath`, `EnableRagdoll`, and `TriggerGetHit` are all warn-log no-op stubs (see
`Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs:52-55`). Consequences:

- A 0-HP humanoid stays in idle/locomotion pose with the NavMeshAgent stopped — no death anim, no ragdoll.
- Every non-lethal hit spam-warns `"humanoid AI get-hit not implemented yet"` via `EntityHealth.TakeDamage` → `_animationDriver?.TriggerGetHit()` (`EntityHealth.cs:71`).
- The `NPCPresence` interaction on a corpse still works — "Talk" prompt, nametag, and `Interact()` raise the dialogue event even though the NPC is dead.
- The `Humanoid_Template.controller` declares `Death` / `GetHit` trigger parameters (`lines 381, 387`) but has no AnimatorStates to consume them, so the triggers are dropped.
- The humanoid `Character` (Mixamo Idle.fbx nested prefab) has no ragdoll authoring — no per-bone Rigidbody / CapsuleCollider / CharacterJoint — so even a wired `EnableRagdoll()` would find zero `Rigidbody` children and no-op.

### Solution

Mirror the existing monster death pipeline for humanoids:

1. **Author ragdoll bones** on the humanoid `Character` prefab via Unity's built-in Ragdoll Wizard (one-time editor step), so `GetComponentsInChildren<Rigidbody>()` discovers a full skeleton.
2. **Extend `Humanoid_Template.controller`** with three new AnimatorStates: `GetHit` (transient, no motion), `Death` (transient, no motion, short hold), and `Dead` (no motion, terminal). Add `Any State → Death` and `Any State → GetHit` transitions on their respective triggers, plus `Death → Dead` exit-time transition. Attach `SMB_DeathState` to the `Death` state.
3. **Implement `HumanoidAIAnimationDriver.TriggerDeath` / `TriggerGetHit` / `EnableRagdoll`** by copying `MonsterAnimationDriver`'s lifecycle pattern (kinematic toggle, `Animator.enabled = false`, NavMeshAgent disable, configurable `_componentsToDisableOnDeath` array). Delegate the parameter writes to `HumanoidAnimationBridge.SetTrigger`.
4. **Add `Death` / `GetHit` trigger setters** to `HumanoidAnimationBridge` (it currently only exposes `PlayAttack(int hash)` and `PlayDodge`; no death/hit-react setters).
5. **Gate `NPCPresence.Interact()`** with an `EntityHealth.IsDead` check (early return + log). Keep `InteractPrompt` and `NameTag` unchanged so the corpse stays highlightable for a future loot-the-corpse interaction; add a TODO comment in `NPCPresence.cs` documenting the planned loot-unlock work.
6. **Wire the NPC prefab**: assign `EntityBrain`, `EntityHealth`, `NPCPresence`, `NavMeshAgent` (and `Capsule Collider` if needed) into the new `HumanoidAIAnimationDriver._componentsToDisableOnDeath` array so they freeze on ragdoll.

### Scope

**In Scope:**

- Implement `HumanoidAIAnimationDriver.TriggerDeath`, `EnableRagdoll`, `TriggerGetHit` (no longer stubs).
- Add `_componentsToDisableOnDeath`, `_persistentID` (optional), and ragdoll-bone caching `[SerializeField]` fields to `HumanoidAIAnimationDriver`, mirroring `MonsterAnimationDriver`.
- Extend `HumanoidAnimationBridge` with `TriggerGetHit()` and `TriggerDeath()` methods + `GetHit` / `Death` hash constants.
- Add `GetHit`, `Death`, `Dead` AnimatorStates to `Humanoid_Template.controller` with the proper transitions and `SMB_DeathState` attachment on `Death`.
- Author humanoid ragdoll bones on the `Character` child prefab via Unity's Ragdoll Wizard (one-time editor step, documented in the spec).
- `NPCPresence.Interact()` early-returns when `EntityHealth.IsDead`, with a `// TODO loot-corpse` comment.
- Wire the NPC_base Variant prefab `_componentsToDisableOnDeath` array.
- Manual play-mode verification: kill an NPC with player attacks, confirm ragdoll + interaction lockout.

**Out of Scope:**

- Loot-the-corpse interaction (deferred — TODO comment in `NPCPresence` flags it).
- Importing a real Mixamo death FBX clip (current spec uses no-motion `Death` state).
- Importing a Mixamo `GetHit` clip (current spec uses no-motion `GetHit` state — animation just plays existing motion through the transient state).
- Adding `IInteractable.CanInteract` or any `InteractionSystem` filtering changes (corpses stay selectable so future loot UX works).
- Humanoid enemy AI variants (only `NPC_base Variant` is in scope; future humanoid enemies reuse the same Animator + driver and need their own prefab work).
- Automated tests (no test infra for AI death exists yet; the project has only `Assets/Tests/EditMode/InteractionSystemTests.cs`-style edit-mode tests, not play-mode death verification).

## Context for Development

### Codebase Patterns

**1. Brain → Driver → Bridge architecture** (`Assets/_Game/Scripts/Core/Animations/CLAUDE.md`)

```
EntityBrain ─┐                                       ┌─ HumanoidAnimationBridge (pure parameter writes)
             ├─→ AIAnimationDriver ─→ HumanoidAIAnimationDriver ──→ ┤
EntityHealth ┘                                                       └─→ Animator (Humanoid_Template / Humanoid_Base override)
```

- `AIAnimationDriver` is the polymorphic seam — `EntityBrain` / `EntityHealth` / `SMB_DeathState` reference it abstractly.
- Bridges contain ZERO lifecycle logic — only `Animator.Set*` calls. All ragdoll/lifecycle owned by the driver.
- One Animator per entity, owned by its matching bridge. Never write to an Animator from outside its bridge (HARD project rule).

**2. Monster ragdoll lifecycle** (reference implementation — `MonsterAnimationDriver.cs:28-87`)

```csharp
// Awake:
_ragdollBodies = _bridge.Animator.GetComponentsInChildren<Rigidbody>();
foreach (var rb in _ragdollBodies) rb.isKinematic = true;  // start kinematic, animator drives bones

// EnableRagdoll (called by SMB_DeathState.OnStateExit):
if (_ragdollActive) return;
if (_ragdollBodies == null || _ragdollBodies.Length == 0) { DisableDeathComponents(); return; }  // graceful fallback
_bridge.Animator.enabled = false;                          // stop animator-driven pose
foreach (var rb in _ragdollBodies) rb.isKinematic = false; // bones go physical
_ragdollActive = true;
DisableDeathComponents();                                  // disable Brain, Health, etc.
```

**3. SMB_DeathState contract** (`Assets/_Game/Scripts/AI/SMB_DeathState.cs`)

```csharp
public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
{
    animator.GetComponentInParent<AIAnimationDriver>()?.EnableRagdoll();
}
```

The Animator lives on the Character child; `GetComponentInParent` walks up to the root where `HumanoidAIAnimationDriver` lives. Already correct for humanoid layout.

**4. EntityHealth.Die() flow** (`EntityHealth.cs:74-88`) — already generic, no change required:

```csharp
IsDead = true;
if (TryGetComponent<NavMeshAgent>(out var agent)) agent.isStopped = true;
_persistentID?.RegisterDeath();
_animationDriver?.TriggerDeath();  // → HumanoidAIAnimationDriver.TriggerDeath (currently stub)
```

**5. HumanoidAnimationBridge ownership rule** (`Assets/_Game/Scripts/Player/CLAUDE.md`)

> "Never add `Animator.SetTrigger/SetBool` calls outside `HumanoidAnimationBridge`. When adding new animations, add a public method to `HumanoidAnimationBridge` (the actual Animator owner)."

Consequence: `HumanoidAIAnimationDriver.TriggerDeath` MUST delegate to a new `HumanoidAnimationBridge.TriggerDeath()` method, NOT call `Animator.SetTrigger("Death")` directly.

**6. Animator controller layout — what already exists in `Humanoid_Template.controller`:**

- Parameters: `Death` (trigger, line 387), `GetHit` (trigger, line 381), `Attack` (trigger, line 375), `Attack_1/2/3`, `IsBlocking`, `IsDodging`, `IsDodgingBackwards`, `IsGrounded`, `IsRising`, `IsInCombat`, `VelocityX`, `VelocityZ` — **Death and GetHit trigger params already declared but never consumed.**
- States: Attack_1_State, Attack_2_State, Attack_3_State, Block_State, LockOn Locomotion (2D blend tree), Dodge back, Dodging, JumpRise, Landing, CombatIdle, Falling — **no Death, Dead, or GetHit state exists.**
- Layers: `Base Layer` (locomotion + jump + falling), `Attack` (upper-body mask) — Death state must live on `Base Layer` so ragdoll takes the whole skeleton.

**7. Reference YAML pattern** (from `Assets/_Game/Art/Characters/Monsters/EntityBase.controller`):

- `SMB_DeathState` inline `!u!114` MonoBehaviour block at line 195-206 — referenced from `Death` state's `m_StateMachineBehaviours: [{fileID: -3519396821137573682}]`.
- `Any State → Death` transition (`!u!1101 &6919870562621114799`) — `m_Conditions: [{m_ConditionMode: 1, m_ConditionEvent: Death, m_EventTreshold: 0}]`, `m_DstState: {fileID: <Death state fileID>}`.
- `Death → Dead` transition (`!u!1101 &7389647503240205148`) — `m_Conditions: []`, `m_HasExitTime: 1`, `m_ExitTime: 1`.
- `Dead` state (`!u!1102 &4004235245111609321`) — `m_Motion: {fileID: 0}` (no clip), `m_Transitions: []` (terminal).
- `Any State → GetHit` transition (`!u!1101 &-5148708298418687873`) — same pattern as Death.
- `GetHit` state — `m_Motion: 0`, transitions back to default via `m_HasExitTime: 1, m_ExitTime: 1`.
- SMB_DeathState script GUID: `3b2a83dac7734854db721822151a6dca`.

**8. Character source prefab** (`guid: 1cd071724417064469dd6b24aac9246f`):

Both Player.prefab and NPC_base Variant.prefab nest the Mixamo `Idle.fbx` as their `Character` child via `PrefabInstance`. The FBX is **read-only** — ragdoll Rigidbody/Collider/CharacterJoint components are added as **prefab-instance overrides on the Character GO inside NPC_base Variant.prefab only**. Player.prefab's `Character` keeps `m_AddedComponents: []` (verified) — Player ragdoll is a separate future story.

**9. NPC variant prefab map** (`Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`):

| FileID | Component | Where assigned |
|--------|-----------|----------------|
| `2202085857188219662` | NPC root GameObject (inherits from Entity_base) | — |
| `3766970546440902038` | `HumanoidAIAnimationDriver` | `_bridge: 3168899758345796514`, `_runSpeed: 4` |
| `3168899758345796514` | `HumanoidAnimationBridge` | `_animator: 1563932590321401957` (Character child) |
| `2441576283753789042` | `NPCPresence` | `_persistentID: 8935889491633091201` |
| `8935889491633091201` | `PersistentID` (inherited stripped ref) | — |
| `6892060459904109189` | `EntityBrain` (inherited, `_animationDriver` overridden → driver) | — |
| `-6003750127742918704` | `EntityHealth` (inherited, `_animationDriver` overridden → driver) | — |
| `3257318164010893370` | `NavMeshAgent` (inherited) | — |
| `1563932590321401957` | `Animator` (stripped, on Character child) | `m_Controller` overridden → `Humanoid_Base.overrideController` |

Removed-from-base in NPC variant: MonsterAnimationDriver (`3299999205233303053`), MonsterAnimationBridge (`7557825151662695134`), `Visual` child (`8934368094500991404`).

**10. Player-Animator safety** — Why the new Animator states don't break the Player:

- Player Animator uses `Humanoid_Template.controller` directly (Player.prefab line 164); NPC uses `Humanoid_Base.overrideController` which wraps the same template.
- New `Death` / `GetHit` triggers ARE visible to Player's Animator, but Player code never calls `bridge.TriggerDeath()` or `bridge.TriggerGetHit()` — `PlayerAnimationDriver` does not expose these wrappers, and `PlayerHealth` (not `EntityHealth`) does not call `TriggerDeath`. Verified by grep: only `EntityHealth.cs` invokes `TriggerDeath/TriggerGetHit` on the driver.
- `SMB_DeathState` only attaches to the `Death` state — Player never enters it.
- **Required guard:** When adding `HumanoidAnimationBridge.TriggerDeath()` / `TriggerGetHit()`, do NOT expose corresponding wrappers on `PlayerAnimationDriver`. This keeps Player code physically unable to fire the new triggers.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs` | Reference implementation for humanoid driver — copy ragdoll lifecycle pattern verbatim |
| `Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs` | Reference for trigger hash + `Animator.SetTrigger` pattern (`DeathHash`, `GetHitHash`, `TriggerDeath()`, `TriggerGetHit()`) |
| `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs` | Target — stubs to replace |
| `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs` | Target — add Death/GetHit hash + setters |
| `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs` | Abstract seam — no change, but confirms virtual signatures of `TriggerDeath`/`TriggerGetHit`/`EnableRagdoll` |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | No change — already calls driver.TriggerDeath/TriggerGetHit |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | No change — generic; will be one of the components disabled on death |
| `Assets/_Game/Scripts/AI/SMB_DeathState.cs` | No change — already uses `GetComponentInParent<AIAnimationDriver>()` |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Target — add `IsDead` gate to `Interact()` + TODO comment |
| `Assets/_Game/Scripts/World/IInteractable.cs` | No change — interaction gating handled internally in NPCPresence |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | No change — corpse remains selectable for future loot |
| `Assets/_Game/Art/Characters/Monsters/EntityBase.controller` | Reference Animator YAML for `Death`/`Dead`/`GetHit` states + `SMB_DeathState` block + AnyState transitions (lines 84-87, 195-206, 444-490, 6919870562621114799 transition) |
| `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` | Target — add 3 AnimatorStates + SMB block + 2 AnyState transitions |
| `Assets/_Game/Art/Characters/Humanoids/Overrides/Humanoid_Base.overrideController` | No change — inherits new states automatically |
| `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` | Target — author ragdoll bones (overrides on Character child) + wire `_componentsToDisableOnDeath` on `HumanoidAIAnimationDriver` |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | Reference — `MonsterAnimationDriver._componentsToDisableOnDeath: [EntityHealth, EntityBrain]` (line 70-71) |
| `Assets/_Game/Prefabs/Player/Player.prefab` | NO change — confirmed `m_AddedComponents: []` on Character child; Player ragdoll is a future story |
| `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs` | NO change — must NOT expose wrappers for new bridge methods (Player-safety guard) |
| `Assets/_Game/CLAUDE.md`, `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`, `Assets/_Game/Scripts/Player/CLAUDE.md`, `Assets/_Game/Prefabs/CLAUDE.md` | Authoritative rules — read before implementing |

### Technical Decisions

- **Manual Ragdoll Wizard** chosen over runtime ragdoll-bone construction or YAML-authoring (per user direction). Setup is one-time and committed to the NPC variant prefab as added components on the Character child's bone GOs. Driver code mirrors `MonsterAnimationDriver` exactly — no humanoid-specific ragdoll discovery.
- **No-motion Death state** chosen over importing a Mixamo death FBX (per user direction). Death state has `m_Motion: 0` and `m_HasExitTime: 1` with a short `m_ExitTime` (~0.1s). Easy to upgrade later by assigning a Motion clip.
- **Soft interaction gate** chosen over hiding crosshair/nametag (per user direction): the corpse stays a valid selection target so a future loot interaction can unlock without re-introducing detection logic. The `IsDead` early-return is internal to `NPCPresence.Interact()` only. A TODO comment flags the planned loot-interaction unlock (aligns with backlog story `7-6-looting-system`).
- **Implement `TriggerGetHit` now** to remove warn-log spam during humanoid combat (per user direction). `GetHit` state is also no-motion + short exit time (placeholder for future hit-react clip import).
- **`_componentsToDisableOnDeath` array** on NPC variant must include `EntityBrain`, `EntityHealth`, and `NavMeshAgent` — NOT `NPCPresence` (kept alive for the soft interaction gate + future loot UX) and NOT `NPCMemoryComponent`/`NPCDialogueGraphComponent`/`InventorySystem`/`GoldSystem` (the dead NPC's state must remain queryable for future loot + save systems).
- **Field type for `_componentsToDisableOnDeath`**: `Behaviour[]` (matches `MonsterAnimationDriver`'s typing as `MonoBehaviour[]` — but `NavMeshAgent` is a `Behaviour` not `MonoBehaviour`, so the humanoid driver MUST type the field as `Behaviour[]` to accept the agent. Confirmed: spider variant inherits `MonoBehaviour[]` from base and only contains `EntityHealth`+`EntityBrain`; humanoid needs the broader type.)
- **Player safety guard**: do NOT expose `TriggerDeath()` / `TriggerGetHit()` wrappers on `PlayerAnimationDriver`. The bridge owns the SetTrigger call (per HumanoidAnimationBridge ownership rule), but Player has no caller path — verified by grep.
- **Animator YAML edit risk**: `Humanoid_Template.controller` will be edited as raw YAML (Edit tool). `Assets/_Game/CLAUDE.md` warns: after a raw YAML edit, never use `refresh_unity(mode="force")` — use `if_dirty` only, or Unity reimports from cached state and destroys the edit.
- **PersistentID on HumanoidAIAnimationDriver**: NOT needed. `MonsterAnimationDriver` uses `_persistentID.Entity.AnimatorOverride` to swap the controller at runtime, but the NPC variant already overrides the Character Animator's `m_Controller` to `Humanoid_Base.overrideController` directly in the prefab — no runtime override needed.

## Implementation Plan

### Tasks

Ordered by dependency: lowest-level animator-write primitives → driver lifecycle → controller asset → ragdoll authoring → prefab wiring → interaction gate → verification.

- [x] **Task 1: Add Death + GetHit triggers to `HumanoidAnimationBridge`**
  - File: `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs`
  - Action: Add two `static readonly int` hash constants (`DeathHash = Animator.StringToHash("Death")`, `GetHitHash = Animator.StringToHash("GetHit")`) and two public methods: `public void TriggerDeath() => _animator?.SetTrigger(DeathHash);` and `public void TriggerGetHit() => _animator?.SetTrigger(GetHitHash);`. Mirror the formatting of `MonsterAnimationBridge.cs:15-16, 34-35`.
  - Notes: Do NOT expose these on `PlayerAnimationDriver` — Player must remain physically unable to fire them. This satisfies the "HumanoidAnimationBridge owns all SetTrigger calls" rule (Player CLAUDE.md) without giving Player a caller path.

- [x] **Task 2: Implement `HumanoidAIAnimationDriver.TriggerDeath` / `TriggerGetHit` / `EnableRagdoll`**
  - File: `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs`
  - Action:
    1. Add `[SerializeField] private Behaviour[] _componentsToDisableOnDeath;` (use `Behaviour[]`, NOT `MonoBehaviour[]`, so `NavMeshAgent` is assignable — see Technical Decisions).
    2. Add private fields: `private Rigidbody[] _ragdollBodies;` and `private bool _ragdollActive;`.
    3. In `Awake()`, after the existing `_bridge` / `_runSpeed` validation: cache ragdoll bodies via `_ragdollBodies = _bridge.GetComponentsInChildren<Rigidbody>();` then `foreach (var rb in _ragdollBodies) rb.isKinematic = true;`. Guard against `_bridge == null`.
    4. Replace `TriggerDeath` stub: `public override void TriggerDeath() => _bridge?.TriggerDeath();`
    5. Replace `TriggerGetHit` stub: `public override void TriggerGetHit() => _bridge?.TriggerGetHit();`
    6. Replace `EnableRagdoll` stub with the monster pattern:
       ```csharp
       public override void EnableRagdoll()
       {
           if (_ragdollActive) return;
           if (_ragdollBodies == null || _ragdollBodies.Length == 0)
           {
               GameLog.Warn(TAG, $"{name}: no ragdoll bodies cached — disabling components only");
               DisableDeathComponents();
               return;
           }
           if (_bridge != null && _bridge.GetComponentInChildren<Animator>() is var anim && anim != null)
               anim.enabled = false;
           foreach (var rb in _ragdollBodies) rb.isKinematic = false;
           _ragdollActive = true;
           DisableDeathComponents();
       }

       private void DisableDeathComponents()
       {
           if (_componentsToDisableOnDeath == null) return;
           foreach (var component in _componentsToDisableOnDeath)
               if (component != null) component.enabled = false;
       }
       ```
    7. Leave `TriggerAttack` as the warn-stub for now (humanoid AI attack is out of scope per CLAUDE.md and this spec).
  - Notes: Use `GameLog.Warn(TAG, …)` — `TAG = "[AI]"` already declared at the top of the file. Do NOT call `Debug.Log*`.

- [x] **Task 3: Add Death / Dead / GetHit AnimatorStates to `Humanoid_Template.controller`**
  - File: `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`
  - Action: Edit the controller YAML directly. Use `Edit` tool, NOT `manage_asset`. Add:
    1. **Three `!u!1102 AnimatorState` blocks** on the `Base Layer` state machine:
       - `Death` state: `m_Name: Death`, `m_Motion: {fileID: 0}`, `m_StateMachineBehaviours: [{fileID: <SMB_fileID>}]`, one transition out to Dead (HasExitTime=1, ExitTime=0.1, no conditions).
       - `Dead` state: `m_Name: Dead`, `m_Motion: {fileID: 0}`, `m_Transitions: []`, no SMBs (terminal).
       - `GetHit` state: `m_Name: GetHit`, `m_Motion: {fileID: 0}`, one transition back to default state (Idle / LockOn Locomotion) with HasExitTime=1, ExitTime=1.
    2. **One `!u!114 MonoBehaviour` block** for `SMB_DeathState`: script GUID `3b2a83dac7734854db721822151a6dca`, `m_GameObject: {fileID: 0}`, `m_EditorClassIdentifier: Game::Game.AI.SMB_DeathState`. Referenced from the Death state's `m_StateMachineBehaviours` array.
    3. **Two new `!u!1101 AnimatorStateTransition` blocks** (AnyState transitions):
       - `Any State → Death`: condition `{m_ConditionMode: 1, m_ConditionEvent: Death, m_EventTreshold: 0}`, transition duration 0.25, HasExitTime=0.
       - `Any State → GetHit`: condition `{m_ConditionMode: 1, m_ConditionEvent: GetHit, m_EventTreshold: 0}`, transition duration 0.25, HasExitTime=0.
    4. **Add the 3 new state fileIDs to `m_ChildStates`** of the `Base Layer` AnimatorStateMachine.
    5. **Add the 2 new AnyState transition fileIDs to `m_AnyStateTransitions`** of the `Base Layer` AnimatorStateMachine.
  - Notes:
    - **Reference exact YAML structure** from `Assets/_Game/Art/Characters/Monsters/EntityBase.controller`:
      - SMB block: lines 195-206
      - Death state: lines 463-490
      - Dead state: lines 437-462
      - GetHit state: lines 168-194
      - Any State → Death transition: `&6919870562621114799` (lines 516-540)
      - Any State → GetHit transition: `&-5148708298418687873` (lines 143-167)
      - Death → Dead transition: `&7389647503240205148` (lines 568-589)
      - `m_AnyStateTransitions` array: lines 84-87
    - **After saving**, run Unity refresh with `mode="if_dirty"` ONLY — never `mode="force"` (CLAUDE.md root warns this destroys raw YAML edits).
    - Generate stable, unique fileIDs for the new blocks. Use large positive or negative numbers that don't collide with existing IDs in the file.
    - The GetHit state should transition back to the Locomotion default — use the existing default state's fileID in `m_DstState`.

- [ ] **Task 4: Author ragdoll bones on the NPC's Character child (Unity Ragdoll Wizard)** — **PENDING USER ACTION (requires Unity Editor / MCP offline)**
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` (Character GO is the nested `Idle.fbx` instance, fileID `8882236327865726265`)
  - Action: In Unity Editor — open `NPC_base Variant.prefab` in Prefab Mode. Select the `Character` child. Use `GameObject → 3D Object → Ragdoll…` wizard, assign bones from the Mixamo humanoid rig:
    - Pelvis → `mixamorig:Hips`
    - L/R Hips → `mixamorig:LeftUpLeg` / `mixamorig:RightUpLeg`
    - L/R Knee → `mixamorig:LeftLeg` / `mixamorig:RightLeg`
    - L/R Foot → `mixamorig:LeftFoot` / `mixamorig:RightFoot`
    - L/R Arm → `mixamorig:LeftArm` / `mixamorig:RightArm`
    - L/R Elbow → `mixamorig:LeftForeArm` / `mixamorig:RightForeArm`
    - L/R Hand → `mixamorig:LeftHand` / `mixamorig:RightHand`
    - Middle Spine → `mixamorig:Spine1` (or `Spine`)
    - Head → `mixamorig:Head`
    - Total Mass: 80, Strength: 0 (defaults are fine).
  - Notes:
    - Ragdoll Wizard adds `Rigidbody` + `CapsuleCollider`/`BoxCollider`/`SphereCollider` + `CharacterJoint` to each bone GO as **prefab-instance overrides on the NPC variant**. The base `Idle.fbx` is read-only.
    - **Do NOT run the wizard on the Player's Character child** — Player ragdoll is a separate future story (`Assets/_Game/Prefabs/Player/Player.prefab` `m_AddedComponents: []` on Character must stay empty).
    - After authoring, save the prefab and verify the override count is sane (15+ added GOs/components on the Character via `Apply All` view — should all be ragdoll additions, no accidental edits to bone Transforms).

- [x] **Task 5: Wire `_componentsToDisableOnDeath` on the NPC variant prefab**
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`
  - Action: In the Inspector for the `HumanoidAIAnimationDriver` component on the NPC root (fileID `3766970546440902038`), assign the new `_componentsToDisableOnDeath` array with 3 entries:
    1. `EntityBrain` (inherited from Entity_base, fileID `6892060459904109189`)
    2. `EntityHealth` (inherited from Entity_base, fileID `-6003750127742918704`)
    3. `NavMeshAgent` (inherited from Entity_base, fileID `3257318164010893370`)
  - Notes: Do NOT add `NPCPresence`, `NPCMemoryComponent`, `NPCDialogueGraphComponent`, `InventorySystem`, or `GoldSystem` — these must stay enabled for the corpse to remain queryable (future loot UX, save system). Save the prefab.

- [x] **Task 6: Gate `NPCPresence.Interact()` on `EntityHealth.IsDead`**
  - File: `Assets/_Game/Scripts/AI/NPCPresence.cs`
  - Action:
    1. Add `using Game.AI;` if not already present (it's in the same `Game.AI` namespace — already implicit).
    2. Add `private EntityHealth _entityHealth;` field.
    3. In `Awake()`, after the `_data == null` check, cache: `_entityHealth = GetComponent<EntityHealth>();` (it's optional — neutral NPCs may not have it; do NOT disable the component if missing).
    4. At the top of `Interact()`, after `if (_data == null) return;`, add:
       ```csharp
       if (_entityHealth != null && _entityHealth.IsDead)
       {
           // TODO: replace this early-return with a loot-corpse interaction unlock
           //       once the looting-system story (sprint backlog 7-6) lands. The
           //       corpse intentionally remains a valid IInteractable target so
           //       the future loot UX can plug in without re-introducing detection
           //       logic. Crosshair highlight + NameTag are unchanged on purpose.
           GameLog.Info(TAG, $"{gameObject.name} is dead — dialogue interaction blocked");
           return;
       }
       ```
  - Notes: Do NOT modify `InteractPrompt` or `NameTag` — both must continue returning their live values so the corpse stays selectable. Do NOT add any change to `InteractionSystem.cs` or `IInteractable.cs`.

- [ ] **Task 7: Manual play-mode verification** — **PENDING USER ACTION (requires Unity Editor)**
  - File: `Assets/_Game/Scenes/StartingTown.unity` (existing NPC instance)
  - Action: Per the Testing Strategy section below. Run through all 6 manual test steps and confirm each AC.
  - Notes: There is no automated AI-death test infrastructure in this project (`Assets/Tests/EditMode/` only covers `InteractionSystemTests`, not death). Manual verification is the only test gate for this spec.

### Acceptance Criteria

- [ ] **AC 1 (Happy path — Death + Ragdoll):** *Given* an NPC instance is alive in `StartingTown.unity` and the player draws a sword and attacks it, *when* `EntityHealth.CurrentHealth` reaches 0, *then* within ~0.5s the NPC's `Character` skeleton collapses into ragdoll (per-bone Rigidbodies become non-kinematic), the `Character` Animator is disabled, and `EntityBrain` / `EntityHealth` / `NavMeshAgent` are all disabled. The body remains visible in the scene and does NOT call `SetActive(false)`.
- [ ] **AC 2 (Interaction gated on death):** *Given* a dead NPC corpse, *when* the player aims the crosshair at it and presses Interact (E), *then* `GameLog.Info` logs `"<name> is dead — dialogue interaction blocked"` and `_onDialogueRequested.Raise()` is NOT called (no dialogue UI opens).
- [ ] **AC 3 (Corpse stays selectable for future loot):** *Given* a dead NPC corpse, *when* the player looks at it within interaction range, *then* the crosshair still highlights (yellow), the NameTag still displays the NPC's name above the corpse, and `InteractionSystem.CurrentInteractable` still returns the `NPCPresence` component. (Verifies the future loot interaction can plug into the existing detection without code change.)
- [ ] **AC 4 (GetHit removes warn-spam):** *Given* an alive NPC with full HP, *when* the player lands a single non-lethal hit, *then* no `"humanoid AI get-hit not implemented yet"` warn log fires, the GetHit trigger fires on the Character Animator, the NPC briefly enters the `GetHit` state and exits back to locomotion within ~0.25s, and the NPC can resume engagement/attack normally.
- [ ] **AC 5 (Player Animator unaffected):** *Given* the Player has health > 0, *when* the player takes damage from any source, *then* the Player Animator does NOT enter `Death`, `Dead`, or `GetHit` states; `PlayerAnimationDriver` remains the only driver feeding the Player Animator; and grepping the codebase for `bridge.TriggerDeath` / `bridge.TriggerGetHit` returns only matches inside `HumanoidAIAnimationDriver.cs`. (Player safety guard.)
- [ ] **AC 6 (Controller-asset shape):** *Given* `Humanoid_Template.controller` is opened in the Unity Animator window, *when* inspecting the `Base Layer` state machine, *then* three new states exist (`Death`, `Dead`, `GetHit`), the `Death` state has exactly one `SMB_DeathState` `StateMachineBehaviour` attached, and two new `Any State` transitions exist — one on the `Death` trigger to the `Death` state, one on the `GetHit` trigger to the `GetHit` state. Both pre-existing controllers in use (`Humanoid_Template` directly and `Humanoid_Base.overrideController`) reflect these states.
- [ ] **AC 7 (NPC ragdoll authoring scope):** *Given* the `NPC_base Variant.prefab` is opened in Prefab Mode, *when* inspecting the `Character` child, *then* the bone GameObjects (Hips, Spine, Head, L/R UpLeg, L/R Leg, L/R Foot, L/R Arm, L/R ForeArm, L/R Hand) each have a `Rigidbody`, a Collider, and the non-pelvis bones each have a `CharacterJoint`. The Player prefab's `Character` child still has `m_AddedComponents: []` — Player is untouched.
- [ ] **AC 8 (`_componentsToDisableOnDeath` wiring):** *Given* the NPC variant prefab, *when* inspecting `HumanoidAIAnimationDriver` in the Inspector, *then* `_componentsToDisableOnDeath` contains exactly 3 references: `EntityBrain`, `EntityHealth`, `NavMeshAgent` — and does NOT contain `NPCPresence`, `NPCMemoryComponent`, `NPCDialogueGraphComponent`, `InventorySystem`, or `GoldSystem`.
- [ ] **AC 9 (Loot-corpse TODO marker present):** *Given* `NPCPresence.cs` on disk, *when* searching for `TODO`, *then* the comment block in `Interact()` explicitly mentions "loot" and references the future looting-system story so it surfaces when story 7-6 starts.

## Additional Context

### Dependencies

- **Code dependencies:** `MonsterAnimationDriver.cs` (reference for ragdoll lifecycle — Task 2), `MonsterAnimationBridge.cs` (reference for trigger hash pattern — Task 1), `EntityBase.controller` (reference YAML for Death/Dead/GetHit states + SMB — Task 3), `Entity_base.prefab` (reference for `_componentsToDisableOnDeath` typing — Task 5).
- **External library dependencies:** None — Unity built-in `Rigidbody`, `CharacterJoint`, `NavMeshAgent`, `Animator`, `StateMachineBehaviour`. No new packages.
- **Asset dependencies:** None — no new FBX, no new ScriptableObject. Ragdoll authoring uses Unity's built-in `GameObject → 3D Object → Ragdoll…` wizard.
- **Story dependencies:** Builds on existing `tech-spec-ai-humanoid-animation-driver.md` (which created the warn-log stubs). No blocking dependency on any in-progress story per `sprint-status.yaml` (Epic 5 / 6 / 7 in-progress stories do not touch this code path).
- **Sister-spec coordination:** None. This spec is self-contained; future story `7-6-looting-system` (backlog) will consume the corpse-stays-selectable behavior introduced here.

### Testing Strategy

**Automated tests:** None. The project's existing test infrastructure (`Assets/Tests/EditMode/InteractionSystemTests.cs`) does not cover AI death paths, and there is no PlayMode test rig for NavMesh / Animator / Ragdoll. Adding test infrastructure for this is explicitly out of scope.

**Manual play-mode verification (mandatory before marking the story done):**

1. **Open scene:** `Assets/_Game/Scenes/StartingTown.unity`. Confirm at least one NPC instance is present and reachable.
2. **Sanity baseline:** Press Play. Confirm the NPC walks/idles normally and does not log any warnings related to humanoid animation. *(Validates Task 2 didn't break the existing locomotion path.)*
3. **GetHit test (AC 4):** Draw the sword (R), close to the NPC, land one non-lethal hit. Verify: no warn log; NPC briefly plays the `GetHit` transient state then resumes locomotion; the NPC can continue to engage/attack the player. (If `EngagePlayer` is enabled on the NPC.)
4. **Death test (AC 1):** Continue attacking until `EntityHealth.CurrentHealth` reaches 0. Verify: `EntityHealth.Die()` info log fires; the `Death` AnimatorState is briefly entered; within ~0.5s the body collapses into physics (head/limbs go limp); the body does NOT disappear; no warn log for "humanoid AI death not implemented yet" or "humanoid AI ragdoll not implemented yet".
5. **Interaction gate test (AC 2 + AC 3):** Walk up to the corpse. Verify: crosshair turns yellow (corpse is still detected); NameTag still shows above the corpse; pressing E logs `"<NPC name> is dead — dialogue interaction blocked"` and does NOT open the dialogue UI.
6. **Player-safety test (AC 5):** Take damage as the Player (have an NPC with `_canEngagePlayer = true` attack you, or trigger any damage source). Verify: Player Animator never enters `Death`/`Dead`/`GetHit` states; Player movement and animations remain unaffected.
7. **Controller-shape test (AC 6):** In the Editor, open `Humanoid_Template.controller` in the Animator window. Visually verify Death, Dead, GetHit states exist; SMB_DeathState is attached to Death; AnyState transitions on Death and GetHit triggers exist.
8. **Component-disable check (AC 8):** Pause play mode right after the corpse forms. In the hierarchy, inspect the NPC root: confirm `EntityBrain.enabled`, `EntityHealth.enabled`, `NavMeshAgent.enabled` are all `false`. Confirm `NPCPresence.enabled` is `true`.

**Static / source-level checks:**

- **Grep guard (AC 5):** Run `Grep "_bridge.TriggerDeath\|_bridge.TriggerGetHit" path=Assets/_Game/Scripts`. The ONLY matches should be inside `HumanoidAIAnimationDriver.cs` and `MonsterAnimationDriver.cs`. No match inside `Assets/_Game/Scripts/Player/`.
- **Player prefab guard (AC 7):** `Grep "m_AddedComponents: \[\]" path=Assets/_Game/Prefabs/Player/Player.prefab` — confirm both occurrences on the Character PrefabInstance still show empty arrays (no accidental Ragdoll Wizard run on Player).

### Notes

**High-risk items / pre-mortem analysis:**

1. **Shared controller — Player blast radius.** Adding `Any State → Death/GetHit` transitions to `Humanoid_Template.controller` is visible to BOTH Player and NPC Animators. Mitigation: the new triggers are only set via `HumanoidAnimationBridge.TriggerDeath/TriggerGetHit` (Task 1), and these methods are NOT exposed on `PlayerAnimationDriver` (deliberate omission). Verified by code path: `EntityHealth.TakeDamage` → `_animationDriver?.TriggerGetHit/TriggerDeath` — Player has `PlayerHealth` instead of `EntityHealth`, so the path is unreachable from the Player side. **If a future story adds a Player death animation, it must NOT add `TriggerDeath` to `PlayerAnimationDriver` — Player death needs its own dedicated trigger name to avoid colliding with this AI-only path.**
2. **Raw YAML edit on `Humanoid_Template.controller`.** The CLAUDE.md root explicitly warns: after a raw YAML edit on a controller, **never** use `refresh_unity(mode="force")` — Unity will reimport from cached state and destroy the edits. Use `mode="if_dirty"` only. If MCP isn't being used, just save the file and let Unity auto-reimport on focus.
3. **`Behaviour[]` vs `MonoBehaviour[]` field type.** `MonsterAnimationDriver._componentsToDisableOnDeath` is typed `MonoBehaviour[]` because monsters only need to disable `EntityHealth`/`EntityBrain` (both MonoBehaviours). Humanoid needs to also disable `NavMeshAgent`, which inherits from `Behaviour` (not `MonoBehaviour`). The humanoid driver MUST use `Behaviour[]` or the Inspector will reject the `NavMeshAgent` drag-drop. Both `MonoBehaviour` and `NavMeshAgent` have `.enabled` (inherited from `Behaviour`), so the existing `component.enabled = false` line works unchanged.
4. **Ragdoll wizard inputs are bone-name-sensitive.** Mixamo rigs prefix bones with `mixamorig:` (e.g. `mixamorig:Hips`). If the dev runs the wizard and the bone-drag-targets are wrong, the wizard silently produces a broken ragdoll (joints anchored to wrong bones). After authoring, smoke-test by killing an NPC in PlayMode — if the ragdoll looks broken (bones spinning, separating), re-run the wizard.
5. **GetHit transient interrupts locomotion.** The `Any State → GetHit` transition fires regardless of current state. The dev must verify the GetHit state's `m_HasExitTime: 1, m_ExitTime: 1` plus a `m_CanTransitionToSelf: 1` setting matches the monster's pattern, so back-to-back hits don't deadlock. (Monster uses `m_CanTransitionToSelf: 1` on the same trigger — see `EntityBase.controller:-5148708298418687873`.)
6. **NavMeshAgent disable timing.** `EntityHealth.Die()` sets `agent.isStopped = true` BEFORE `_animationDriver.TriggerDeath()` fires. Then the Death anim plays (~0.1s), then `SMB_DeathState.OnStateExit` → `EnableRagdoll()` → `_componentsToDisableOnDeath` sets `agent.enabled = false`. In the gap (~0.1s), the agent is stopped but enabled. This is OK because nothing in the gap moves the ragdoll bones — the Animator is still driving the skeleton kinematically. No teleport risk.

**Known limitations:**

- The Death state has no motion clip — the corpse pops directly into ragdoll without a falling animation. Acceptable for v1 per user direction. Future polish story: import a Mixamo death FBX, assign to the Death state's `m_Motion`.
- The GetHit state has no motion clip — the existing locomotion clip continues to play through the brief GetHit window. The hit feedback is currently invisible. Acceptable for v1 to remove warn-spam; future story can import a Mixamo hit-react clip.
- Single NPC type only (`NPC_base Variant`). Any future humanoid enemy variant must independently run the Ragdoll Wizard on its own Character child override and wire its own `_componentsToDisableOnDeath` — none of this is inherited automatically.

**Future considerations (explicitly out of scope, captured for traceability):**

- Loot-the-corpse interaction unlock (sprint backlog `7-6-looting-system`) — `NPCPresence.Interact()` TODO comment flags the planned replacement.
- Player ragdoll on death (currently `gameObject.SetActive(false)`).
- Real Mixamo death + hit-react FBX import.
- `IInteractable.CanInteract` contract if future requirements diverge (currently soft-gated inside `NPCPresence` is sufficient).
- Audio: death thud / hit grunt SFX integration with the new Death/GetHit triggers.

### CLAUDE.md Candidates (for `/perso:wrap-up` to consider)

After this story is implemented, the following patterns are worth promoting to a folder CLAUDE.md so future stories don't re-derive them:

- `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`: update the "Humanoid AI combat triggers are stubs" bullet to reflect that `TriggerDeath` / `TriggerGetHit` / `EnableRagdoll` are now implemented; only `TriggerAttack` remains a stub. Mention the `Behaviour[]` field-type requirement for humanoid `_componentsToDisableOnDeath` vs `MonoBehaviour[]` for monsters.
- `Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md` (NEW): document that the controller is shared with the Player; new AnyState transitions MUST only fire via triggers that Player code cannot reach (HumanoidAnimationBridge methods not exposed on PlayerAnimationDriver).
- `Assets/_Game/Prefabs/Entities/Humanoids/CLAUDE.md` (NEW): document the Ragdoll Wizard authoring step + Mixamo bone-prefix gotcha + "do NOT run on Player.prefab Character" warning.
