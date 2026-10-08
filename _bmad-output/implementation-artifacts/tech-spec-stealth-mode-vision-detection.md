---
title: 'Stealth Mode & Vision-Cone Detection'
slug: 'stealth-mode-vision-detection'
created: '2026-10-08'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'Unity Input System (InputSystem_Actions)', 'Mecanim humanoid (Humanoid_Template.controller)', 'NavMeshAgent', 'PhysX raycasts', 'NUnit EditMode tests']
files_to_modify: ['Assets/_Game/Scripts/Player/PlayerController.cs', 'Assets/_Game/Scripts/Player/PlayerStateManager.cs', 'Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs', 'Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs', 'Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs', 'Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs', 'Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs', 'Assets/_Game/ScriptableObjects/Entities/Entity.cs', 'Assets/_Game/Scripts/AI/EntityBrain.cs', 'Assets/_Game/Scripts/AI/FactionMember.cs', 'Assets/_Game/Scripts/AI/TargetRegistry.cs', 'Assets/_Game/Scripts/Combat/PlayerCombat.cs', 'Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller', 'Assets/_Game/Art/Characters/Humanoids/Animations/Sneaking/*.fbx.meta', 'Assets/_Game/Prefabs/Player/Player.prefab', 'NEW Assets/_Game/Scripts/Stealth/IStealthTarget.cs', 'NEW Assets/_Game/Scripts/Stealth/StealthDetection.cs', 'NEW Assets/_Game/Scripts/Combat/ISneakAttackTarget.cs', 'NEW Assets/_Game/Scripts/Player/PlayerSneak.cs', 'NEW Assets/_Game/Scripts/AI/EntityPerception.cs', 'NEW Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs', 'NEW Assets/_Game/Data/Config/StealthConfig.asset', 'NEW Assets/Tests/EditMode/StealthDetectionTests.cs', 'Assets/_Game/InputSystem_Actions.inputactions', 'Assets/_Game/InputSystem_Actions.cs', 'Assets/_Game/Prefabs/Entities/Entity_base.prefab', 'Assets/_Game/Scenes/Core.unity', 'NEW Assets/_Game/Scripts/Stealth/StealthDebugGeometry.cs', 'NEW Assets/_Game/Scripts/Stealth/StealthDebug.cs', 'NEW Assets/_Game/Editor/StealthDebugMenu.cs', 'NEW Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs', 'NEW Assets/_Game/Data/Debug/M_DebugLines.mat', 'NEW Assets/_Game/Prefabs/UI/Debug/StealthDebugLabel.prefab', 'NEW Assets/Tests/EditMode/StealthDebugGeometryTests.cs']
code_patterns: ['PlayerStateManager single gate + SetX setters', 'Animator writes only via HumanoidAnimationBridge (player: PlayerAnimationDriver wrappers)', 'Enum switch state machine in EntityBrain', 'Cross-system interface polled via GetComponent (ICombatStateProvider precedent)', 'Config SOs for all tunables; per-entity tuning on Entity SO', 'GameLog + TAG, no Debug.Log', 'Non-alloc physics, cached refs, no per-frame allocations', 'OnDisable null-guard for _input', 'Pure static math classes tested in EditMode (HitSweepTracker precedent)']
test_patterns: ['NUnit EditMode in Assets/Tests/EditMode (Tests.EditMode asmdef references Game)', 'Pure formula tests; brain decisions mirrored in plain C# (EntityWarningStateTests)', 'Class naming [SystemName]Tests']
---

# Tech-Spec: Stealth Mode & Vision-Cone Detection

**Created:** 2026-10-08

## Overview

### Problem Statement

The player has no way to sneak, and hostile entities detect the player through a 360° distance check
(`TargetRegistry.FindClosestHostile` within `Entity.DetectionRange`) that ignores facing and line of
sight. Entities "see" through walls and from behind, so the GDD stealth pillar (Mechanic #3:
visibility-cone detection, crouching reduces detection) is missing and there is no way to avoid,
bypass or ambush a fight.

### Solution

Add a toggled **sneak stance** for the player (slower movement, Mixamo crouch locomotion) and replace
hostile entities' radius acquisition of the player with a per-entity **awareness meter** fed by a
**vision cone + line-of-sight raycast + small 360° proximity radius**. Fill rate scales with distance,
how central the player is in the cone, and whether the player is sneaking. A full meter hands off to
the existing Warning → Engaging flow. Engaged entities that lose sight of the player move to the
**last seen position** and enter a basic **Searching** state. Hitting an unaware entity deals
**sneak-attack** bonus damage.

### Scope

**In Scope:**

1. **Sneak stance (player)**
   - `Crouch` action (**C** — already bound, currently unused) **toggles** sneak.
     `CharacterStatsToggle` is also on C today (opens the stats tab) → it moves to **P**.
   - New `sneakSpeed` in `PlayerConfigSO`; `PlayerStateManager.IsSneaking`.
   - **Running exits sneak instantly**: pressing Sprint while moving and sneaking clears `IsSneaking`
     immediately and plays the `sneak to sprint` clip, which flows into `Running`.
   - **Attacking and blocking do NOT cancel sneak.** Sneak ends only on C (again), starting to run,
     dodge, jump, or death.
   - Enter/exit play `stand to sneak` / `sneak to stand`.
2. **Animation** — sneak locomotion in `Humanoid_Template` Base Layer, driven by a new `IsSneaking`
   animator parameter (written only via `HumanoidAnimationBridge`). Clips in
   `Art/Characters/Humanoids/Animations/Sneaking/`: `sneak idle`, `sneak walk`, `sneak left`,
   `sneak right`, `stand to sneak`, `sneak to stand`, `sneak to sprint`. Backward sneaking uses
   `sneak walk` at speed −1 (no dedicated clip).
3. **Attacking while sneaking** — attacks run on the upper-body-masked `Attack` layer, so the legs keep
   the sneak pose from the Base layer. Sneak persists through the combo.
4. **Detection data** — new fields on the `Entity` SO: view angle, sight range, proximity radius,
   awareness fill/drain rates, sneak multipliers, lose-sight timeout, search duration; LOS layer mask.
5. **Awareness meter** on hostile entities vs. the player — replaces radius acquisition in Idle/Patrol;
   full awareness → existing Warning/Engaging logic.
6. **Lose the player by breaking LOS** — while Warning/Engaging/Attacking, losing LOS for N seconds →
   move to last seen position → new **Searching** state (short wait/look-around placeholder, refined in
   a later spec) → back to Idle/Patrol. Re-sighting during search re-engages.
7. **Sneak attack** — a player hit on an entity that is unaware (not detected/engaged) is multiplied by a
   configurable sneak-attack damage multiplier.
8. **Detection debug views**
   - **Gizmos** (Scene view, or Game view with Gizmos on): cone at standing and sneaking range, proximity
     circles, LOS line (green clear / red blocked), last seen marker, and a label with brain state,
     awareness %, visibility, fill rate/s, distance and time since seen. Shown for the selected entity, or
     for all entities via `Tools/Stealth/Show Detection Gizmos`.
   - **In-game overlay** (editor + development builds only), toggled with **F3**: the same cone/LOS lines
     drawn at runtime, a floating label above each nearby entity, and a corner panel (player sneaking,
     visibility height, highest awareness on the player and which entity).

**Out of Scope:**

- Neutral NPC reactions to a sneaking player (deferred to the stealing/pickpocket spec).
- Stealing, pickpocketing, containers ownership.
- Sound/noise detection; light/shadow visibility.
- Player-facing HUD stealth indicator, per-entity `?`/`!` markers (the F3 overlay is a dev tool only).
- Stealth skill / progression tie-in.
- Smart search behaviour (search patterns, calling allies) — Searching is a placeholder.
- NPC-vs-NPC detection (non-player targets keep the current radius check).
- Dedicated sneak-attack animations (takedowns).

## Context for Development

### Codebase Patterns

**Player**
- `PlayerController.ApplyMovement()` uses `_config.runSpeed` when `_input.Player.Sprint.IsPressed()`, else
  `_config.walkSpeed` (`PlayerConfigSO`: walk 3, run 6). Returns early while `IsDodging`. `ApplyJump()`
  gates on `_stateManager.CanJump()` and calls `NotifyJumpStarted()`.
- `PlayerStateManager` is the single gate: flags with `private set`, `SetX()` setters that also forward to
  `PlayerAnimationDriver` wrappers, `Can*()` queries. `SetDead(true)` clears action flags.
  `DodgeController` calls `SetDodging(true)` (~line 186).
- `PlayerAnimationDriver.Update()` sends `VelocityX/Z` = local velocity / `runSpeed` (walk ≈ 0.5,
  run = 1.0). Its wrappers forward to `HumanoidAnimationBridge`, which owns every `Animator.Set*`
  (static `StringToHash` fields, one method per parameter).
- Input: `Crouch` (Player map) is bound to `<Keyboard>/c` (plus an unused gamepad binding). No code reads
  it. Every component creates its own `InputSystem_Actions` in `OnEnable` and disposes it in a
  null-guarded `OnDisable`.
- `PlayerSaveAdapter.Restore()` restores state through each system's Restore API.
- The Player prefab has `FactionMember` with `_factionOverride` → the player is a normal `TargetRegistry`
  member.

**AI**
- `EntityBrain`: `enum EntityState { Idle, Patrolling, Warning, Engaging, Attacking, Dead }`, switched in
  `Update`. Idle/Patrol call `TryAcquireTargetThrottled()` (every `_targetScanInterval` = 0.25 s) →
  `TargetRegistry.FindClosestHostile(faction, pos, Entity.DetectionRange)` (360°, no LOS) →
  `RespondToDetectedTarget()` (engage if `_engageImmediately` or inside `WarningRange`, else Warning).
  Warning cancels when dist > `DetectionRange` and engages inside `WarningRange` or when its timer ends.
  Engaging/Attacking disengage when dist > `DisengageRange` → `DisengageFromCombat()` → Idle/Patrol
  (`_disengageState`). `FaceTarget()` turns at `Entity.WarningTurnSpeed`.
- The brain does **not** react to taking damage today: an entity hit from outside `DetectionRange` never
  fights back.
- `Entity` SO (`_Game.ScriptableObjects.Entities`): Detection header (`_detectionRange` 8,
  `_disengageRange` 12, `_warningRange` 5, ...). `DetectionRange <= 0` = passive entity (skip detection
  and validation).
- `EntityHealth` (same GO as the brain) exposes `event Action<float,float> HealthChanged` (same-system
  use is fine).
- `FactionMember` caches `IDamageable` in `Awake`. `TargetRegistry` is a static `HashSet<FactionMember>`.

**Combat**
- Player damage path: `WeaponHitbox.OnHit(IDamageable target, Vector3 hitPoint)` →
  `PlayerCombat.OnWeaponHit` → `TryReceiveHit` → `TakeDamage(ComputeEffectiveDamage())`.
- `CanAttack()` requires `IsInCombat` (weapon drawn with R), `!IsDodging`, `!IsBlocking`, `!IsAirborne`.

**Animator (`Humanoid_Template.controller`, shared by the Player and humanoid NPCs)**
- **Base Layer** (no mask): default `LockOn Locomotion` (2D freeform cartesian on VelocityX/Z).
  AnyState → `JumpRise` / `Falling` / `Dodging` / `Dodge back` / `Death` / `GetHit`. Dodging, Landing and
  GetHit return to `LockOn Locomotion` on exit time.
- **Attack layer** (weight 1, `UpperBodyMask` = Body + Head + arms + fingers) has its **own**
  `LockOn Locomotion` state (default) → `CombatIdle` (IsInCombat) → `Attack_1/2/3_State`, plus AnyState →
  `Block_State` and jump/fall/dodge copies. **This layer always drives the upper body, even out of
  combat**, so it needs its own sneak state or the torso plays standing locomotion over crouched legs.
- Parameters: `IsGrounded, IsRising, Attack_1/2/3, IsBlocking, IsDodging, IsDodgingBackwards, VelocityX,
  VelocityZ, IsInCombat, Attack, GetHit, Death`. No `Speed` / `IsLockedOn` (don't add them).
- Physics layers: Default, TransparentFX, Ignore Raycast, Player, Water, UI, Characters, CharacterHitbox,
  Interactable. Level geometry is on Default.

**Tests**
- EditMode NUnit; `Tests.EditMode.asmdef` references `Game`. Pure static helpers are tested directly
  (`HitSweepTracker.ComputeSubSteps`); brain decisions are mirrored in plain C# (`EntityWarningStateTests`).
  No PlayMode tests in use.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Player/PlayerController.cs` | Speed selection and jump; sneak speed |
| `Assets/_Game/Scripts/Player/PlayerStateManager.cs` | Add `IsSneaking` / `SetSneaking`; clear on dodge, jump, death |
| `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs` | Forward sneak params to the bridge |
| `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` | Restore leaves the player standing |
| `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs` | `IsSneaking` bool + `SneakToSprint` trigger |
| `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs` | `sneakSpeed` |
| `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` | `sneakAttackDamageMultiplier` |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Per-entity perception fields |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | Perception-driven acquisition, Searching state, damage reaction |
| `Assets/_Game/Scripts/AI/FactionMember.cs` | Cache `IStealthTarget` |
| `Assets/_Game/Scripts/AI/TargetRegistry.cs` | Option to skip stealth targets in the radius search |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | Sneak-attack multiplier in `OnWeaponHit` |
| `Assets/_Game/Scripts/Combat/DodgeController.cs` | Dodge exits sneak (through `SetDodging`) |
| `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` | Sneak states on both layers |
| `Assets/_Game/Art/Characters/Humanoids/Animations/UpperBodyMask.mask` | Body-part risk (see Notes) |
| `Assets/_Game/Art/Characters/Humanoids/Animations/Sneaking/*.fbx(.meta)` | Clips + import fixes |
| `Assets/Tests/EditMode/EntityWarningStateTests.cs`, `HitSweepTrackerTests.cs` | Test style |
| `Scripts/Player/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Scripts/Core/Animations/CLAUDE.md`, `Art/Characters/Humanoids/Controllers/CLAUDE.md`, `.claude/rules/attack-pipeline.md` | Folder rules to follow and update |

### Technical Decisions

1. **Sneak toggle** on `Crouch` (C). A new `PlayerSneak` component (`Scripts/Player/`, `Game.Player`) reads
   the input and calls `PlayerStateManager.SetSneaking(bool)`. It also implements `IStealthTarget`.
2. **Exit rules live in `PlayerStateManager`:** `SetDodging(true)`, `NotifyJumpStarted()` and
   `SetDead(true)` clear `IsSneaking` (no transition clip). The running exit is detected in `PlayerSneak`
   (Sprint held + move input ≠ 0) → `ExitSneakToSprint()` clears `IsSneaking` and fires the
   `SneakToSprint` trigger. `PlayerController` uses `sneakSpeed` while `IsSneaking`. Attack and block never
   touch sneak.
3. **Cross-system contracts are interfaces polled with `GetComponent`** (like `ICombatStateProvider`):
   - `Game.Stealth.IStealthTarget` (Player → AI): `bool IsSneaking`, `Vector3 VisibilityPoint` (chest height).
   - `Game.Combat.ISneakAttackTarget` (AI → Combat): `bool IsUnawareOf(GameObject attacker)`.
4. **Perception is a new `EntityPerception` component** (`Scripts/AI/`, `Game.AI`) on hostile entity
   prefabs. It owns awareness (0..1) for **stealth targets only**, the LOS raycast and the last seen
   position. The math lives in a pure static `Game.Stealth.StealthDetection` class (unit-tested). For
   non-stealth targets (NPCs) the brain keeps the radius path, with stealth targets skipped in
   `TargetRegistry`.
5. **Data split:** per-entity values on the `Entity` SO (view angle, proximity radius, awareness fill time,
   lose-sight time, search duration; sight range = the existing `DetectionRange`). Global values in a new
   `StealthConfigSO` (`Data/Config/StealthConfig.asset`): sneak multipliers (sight range, proximity radius,
   fill rate), drain rate, LOS layer mask (Default), eye height, suspicion threshold.
   `sneakAttackDamageMultiplier` goes in `CombatConfigSO`.
6. **Brain changes:** full awareness → the existing `RespondToDetectedTarget()`. A new `Searching` state
   moves to the last seen position, waits/scans for `SearchDuration`, then returns to `_disengageState`.
   In Warning/Engaging/Attacking, losing LOS for `LoseSightTime` → Searching. Taking damage while not
   engaged → awareness = 1 and engage (fixes "hit from range, no reaction"). Suspicion
   (awareness ≥ threshold) in Idle/Patrol: stop and turn toward the last seen point, so the player gets
   feedback without UI.
7. **Animator:** new `IsSneaking` bool and `SneakToSprint` trigger. Base layer: `Stand To Sneak` →
   `Sneak Locomotion` (2D blend tree) → `Sneak To Stand` / `Sneak To Sprint` → `LockOn Locomotion`.
   Attack layer: a `Sneak Locomotion` state alongside its `LockOn Locomotion` (IsSneaking transitions), so
   the upper body crouches when no combat state is active. Blend positions keep the existing normalization
   (velocity / `runSpeed`): sneak walk at `sneakSpeed / runSpeed` (1.5 / 6 = 0.25); backward = `sneak walk`
   at time scale −1. Humanoid NPCs share the controller; `IsSneaking` defaults to false, so they're
   unaffected.
8. **Clip import:** `sneak idle/walk/left/right` → Loop Time + Loop Pose; all 7 clips bake root rotation,
   Y and XZ into the pose (match `Walking.fbx.meta`). Change them through the Unity importer (MCP), not
   raw YAML.
9. Sneak and awareness are runtime-only and not saved. `PlayerSaveAdapter.Restore` forces
   `SetSneaking(false)`.
10. **Key conflict:** `CharacterStatsToggle` (read by `UIScreenManager`) was also bound to C. Decision
    (2026-10-08): sneak keeps C, stats moves to P (free key).
11. **Debug views:** gizmos for the editor (selected entity or all via a Tools menu toggle, mirroring
    `HitboxDebug`) plus a runtime overlay for the Game view and development builds. Gizmos don't render in
    builds, so the overlay draws its own lines with `GL` in URP's `endCameraRendering`, and its labels are
    pooled uGUI TMP texts. It is gated by `Debug.isDebugBuild` (project rule for runtime debug tools),
    lives in `Game.DevTools` / `Core.unity`, and is toggled by a new `ToggleStealthDebug` action (F3).
    Gizmos and overlay share the geometry helper and read the same `EntityPerception` debug properties.

## Implementation Plan

### Tasks

Tasks are grouped in phases; finish and compile each phase before the next. Phase B's mask check (Task 10)
is a gate: resolve it before building the AI phases on top.

#### Phase A — Contracts, config, data

- [x] Task 1: Input actions — free C for sneak, add the debug toggle
  - Files: `Assets/_Game/InputSystem_Actions.inputactions` **and** the embedded JSON in
    `Assets/_Game/InputSystem_Actions.cs` (dual-file contract, `Assets/_Game/CLAUDE.md` — edit both, the `.cs`
    uses `""`-escaped quotes).
  - Action:
    - Rebind `CharacterStatsToggle` from `<Keyboard>/c` to `<Keyboard>/p`.
    - Add Player-map action `ToggleStealthDebug` (Button, new GUID) bound to `<Keyboard>/f3`
      (group `Keyboard&Mouse`); add its generated members to the `.cs` wrapper (`m_Player_ToggleStealthDebug`,
      `PlayerActions.ToggleStealthDebug`, `FindAction` in the constructor, callback interface method
      `OnToggleStealthDebug`) following how `QuickSave` is generated.
  - Check: grep `Assets/_Game` and `Assets/Tests` for UI text or tests that show "C" for stats and update
    them (none found during spec, only docs).
  - Docs: `Assets/_Game/CLAUDE.md` action map list (`CharacterStatsToggle (P)`, `ToggleStealthDebug (F3)`,
    Crouch (C)); `Assets/_Game/Scripts/UI/Screens/CLAUDE.md` (`CharacterStatsToggle (P)`).

- [x] Task 2: Stealth contracts
  - File: NEW `Assets/_Game/Scripts/Stealth/IStealthTarget.cs` (namespace `Game.Stealth`)
  - Action: `public interface IStealthTarget { bool IsSneaking { get; } Vector3 VisibilityPoint { get; } }`
    with XML doc: implemented by the player; polled by AI perception through `FactionMember.StealthTarget`.
  - File: NEW `Assets/_Game/Scripts/Combat/ISneakAttackTarget.cs` (namespace `Game.Combat`)
  - Action: `public interface ISneakAttackTarget { bool IsUnawareOf(GameObject attacker); }` — implemented by
    `EntityBrain`, queried by `PlayerCombat` before applying damage.

- [x] Task 3: `StealthConfigSO`
  - File: NEW `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` (namespace `Game.Stealth`,
    `[CreateAssetMenu(menuName = "Config/Stealth", fileName = "StealthConfig")]`), public fields with
    `[Header]` / `[Tooltip]` like `CombatConfigSO`:
    - Sneak modifiers: `sneakSightRangeMultiplier = 0.6f`, `sneakProximityMultiplier = 0.35f`,
      `sneakFillRateMultiplier = 0.5f`
    - Visibility falloff: `edgeDistanceFactor = 0.2f` (fill factor at the far edge of the cone),
      `peripheralAngleFactor = 0.5f` (fill factor at the cone's side edge)
    - Awareness: `awarenessDrainPerSecond = 0.25f`, `suspicionThreshold = 0.5f`,
      `searchStartAwareness = 0.5f` (awareness kept when an alerted entity loses the player)
    - Sensing: `lineOfSightMask` (LayerMask, default = Default layer), `losCheckInterval = 0.1f`,
      `targetScanInterval = 0.25f`
    - Target points: `standingVisibilityHeight = 1.4f`, `sneakingVisibilityHeight = 0.9f`
    - Behaviour: `suspiciousTurnSpeed = 180f`, `searchTurnSpeed = 90f` (deg/s)
  - File: NEW `Assets/_Game/Data/Config/StealthConfig.asset` — create the instance (MCP
    `manage_scriptable_object`) with the defaults above; set `lineOfSightMask` to `Default`.

- [x] Task 4: Player and combat config fields
  - File: `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs`
  - Action: add `[SerializeField] public float sneakSpeed = 1.5f;` under `runSpeed` with a tooltip
    ("Sneak locomotion speed. Sneak blend-tree positions sit at sneakSpeed / runSpeed — update the
    controller if you change either").
  - File: `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs`
  - Action: new `[Header("Sneak Attack")]` → `public float sneakAttackDamageMultiplier = 2f;` (tooltip:
    multiplier on a hit against an entity that has not detected the player).

- [x] Task 5: Per-entity perception data
  - File: `Assets/_Game/ScriptableObjects/Entities/Entity.cs`
  - Action: new `[Header("Perception")]` block (after Detection) with private serialized fields and
    read-only properties:
    `_viewAngle = 110f` (full cone, degrees), `_eyeHeight = 1.6f`, `_proximityRadius = 2f`,
    `_awarenessFillTime = 1f` (seconds to full awareness at best visibility), `_loseSightTime = 2f`,
    `_searchDuration = 6f`. Tooltips required. Sight range is the existing `DetectionRange` — say so in the
    `_detectionRange` tooltip.
  - `OnValidate` (inside the existing `#if UNITY_EDITOR`): clamp `_viewAngle` to [1, 360], `_eyeHeight` ≥ 0,
    `_proximityRadius` ≥ 0, `_awarenessFillTime` ≥ 0.05, `_loseSightTime` ≥ 0, `_searchDuration` ≥ 0;
    when `_detectionRange > 0` and `_proximityRadius > _detectionRange`, warn and clamp to `_detectionRange`.
  - Notes: existing Entity assets get the initializer defaults automatically. Set the spider asset's
    `_eyeHeight` to ~0.4 (find it under `Data/` with a grep for `Entity` assets whose name contains Spider).

#### Phase B — Player sneak stance and animation

- [x] Task 6: Sneak animator parameters in the bridge
  - File: `Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs`
  - Action: add hashes `IsSneakingHash = "IsSneaking"`, `SneakToSprintHash = "SneakToSprint"`; methods
    `SetSneaking(bool value)` (null-guarded; when `value` is true also `ResetTrigger(SneakToSprintHash)` so a
    stale trigger can't fire) and `TriggerSneakToSprint()`.

- [x] Task 7: Player state
  - File: `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs`
  - Action: wrappers `SetSneaking(bool)` → bridge; `PlaySneakToSprint()` → `_humanoidBridge.SetSneaking(false)`
    then `_humanoidBridge.TriggerSneakToSprint()`.
  - File: `Assets/_Game/Scripts/Player/PlayerStateManager.cs`
  - Action:
    - `public bool IsSneaking { get; private set; }` (doc: written by `PlayerSneak`; cleared by dodge, jump,
      death, sprint).
    - `public bool CanSneak() => !IsBusy && !IsAirborne && !IsDodging;` (attacking/blocking allowed).
    - `public void SetSneaking(bool value)`: return if unchanged; set; `_playerAnimator.SetSneaking(value)`;
      `GameLog.Info(TAG, $"Sneak: {value}")`.
    - `public void ExitSneakToSprint()`: return if `!IsSneaking`; `IsSneaking = false`;
      `_playerAnimator.PlaySneakToSprint()`; log.
    - `SetDodging(true)`: call `SetSneaking(false)` before `PlayDodge`.
    - `NotifyJumpStarted()`: call `SetSneaking(false)`.
    - `SetDead(true)`: add `IsSneaking = false` to the cleared flags and `_playerAnimator.SetSneaking(false)`
      in the animator branch.
    - Update the class summary's "Exposes" list.

- [x] Task 8: `PlayerSneak` component
  - File: NEW `Assets/_Game/Scripts/Player/PlayerSneak.cs` (namespace `Game.Player`, TAG `"[Player]"`,
    `[RequireComponent(typeof(PlayerStateManager))]`, implements `Game.Stealth.IStealthTarget`)
  - Action:
    - Serialized: `StealthConfigSO _stealthConfig`. `Awake`: cache `PlayerStateManager`; missing config →
      `GameLog.Error` + `enabled = false`.
    - `OnEnable`: `_input = new InputSystem_Actions(); _input.Player.Enable();
      _input.Player.Crouch.started += HandleCrouchStarted;`. `OnDisable`: null-guard `_input`, unsubscribe,
      `Disable()`, `Dispose()`.
    - `HandleCrouchStarted`: if `IsSneaking` → `SetSneaking(false)`; else if `CanSneak()` and not
      (Sprint held with move input) → `SetSneaking(true)`.
    - `Update`: if `IsSneaking` && `_input.Player.Sprint.IsPressed()` &&
      `_input.Player.Move.ReadValue<Vector2>().sqrMagnitude > 0.01f` → `_stateManager.ExitSneakToSprint()`.
    - `IStealthTarget`: `IsSneaking => _stateManager.IsSneaking`; `VisibilityPoint => transform.position +
      Vector3.up * (IsSneaking ? sneakingVisibilityHeight : standingVisibilityHeight)`; return
      `transform.position` when the config is missing.
  - Notes: no allocations in `Update`. Input reads are permitted here because this component owns the
    sneak action (same as `PlayerCombat` owns Attack/Block).

- [x] Task 9: Movement speed and save restore
  - File: `Assets/_Game/Scripts/Player/PlayerController.cs`
  - Action: in `ApplyMovement`, `currentSpeed = (_stateManager != null && _stateManager.IsSneaking) ?
    _config.sneakSpeed : (isSprinting ? _config.runSpeed : _config.walkSpeed);`. Update the class summary.
  - File: `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs`
  - Action: in `Restore`, call `_stateManager?.SetSneaking(false)` so a loaded game starts standing.
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action: add `PlayerSneak` to the Player root; assign `StealthConfig.asset`. Use MCP
    (`manage_components`), not raw YAML.

- [x] Task 10: Sneak clips and animator controller
  - Files: `Assets/_Game/Art/Characters/Humanoids/Animations/Sneaking/*.fbx` (import settings only)
  - Action (MCP `execute_code` with `ModelImporter.clipAnimations`, then `SaveAndReimport`):
    - `sneak idle`, `sneak walk`, `sneak left`, `sneak right`: `loopTime = true`, `loopPose = true`.
    - All 7 clips: `lockRootRotation = true`, `lockRootHeightY = true`, `lockRootPositionXZ = true`,
      `keepOriginalOrientation = false`, `keepOriginalPositionY = true`, `keepOriginalPositionXZ = false`
      (same as `Walking.fbx.meta`).
  - File: `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`
  - Action (MCP `execute_code` with `UnityEditor.Animations` API — never raw YAML for this; save with
    `AssetDatabase.SaveAssets()`):
    - Parameters: `IsSneaking` (Bool), `SneakToSprint` (Trigger).
    - Blend tree `Sneak Locomotion` (2D Freeform Cartesian, params `VelocityX`/`VelocityZ`), `s = 0.25`
      (`sneakSpeed / runSpeed`): `sneak idle` (0,0); `sneak walk` (0,s); `sneak walk` with time scale −1
      (0,−s); `sneak left` (−s,0); `sneak right` (s,0). Verify `sneak left` moves the character left; swap
      if not.
    - **Base Layer** new states: `Stand To Sneak` (clip `stand to sneak`), `Sneak Locomotion` (blend tree),
      `Sneak To Stand` (clip `sneak to stand`), `Sneak To Sprint` (clip `sneak to sprint`). Transitions:
      1. `LockOn Locomotion → Stand To Sneak`: `IsSneaking == true`, no exit time, 0.1 s.
      2. `Stand To Sneak → Sneak To Sprint`: `SneakToSprint`, no exit time, 0.1 s (listed first).
      3. `Stand To Sneak → Sneak To Stand`: `IsSneaking == false`, no exit time, 0.15 s.
      4. `Stand To Sneak → Sneak Locomotion`: exit time 0.8, 0.15 s; plus four early-outs with no exit time
         (0.15 s) on movement: `VelocityZ > 0.05`, `VelocityZ < -0.05`, `VelocityX > 0.05`, `VelocityX < -0.05`.
      5. `Sneak Locomotion → Sneak To Sprint`: `SneakToSprint`, no exit time, 0.1 s — **ordered before** 6
         (both fire on the same frame; the trigger must win).
      6. `Sneak Locomotion → Sneak To Stand`: `IsSneaking == false`, no exit time, 0.15 s.
      7. `Sneak To Stand → LockOn Locomotion`: exit time 0.8, 0.15 s; plus the same four movement early-outs.
      8. `Sneak To Stand → Stand To Sneak`: `IsSneaking == true`, no exit time, 0.15 s (fast re-toggle).
      9. `Sneak To Sprint → LockOn Locomotion`: exit time 0.7, 0.2 s.
      Existing AnyState transitions (jump, fall, dodge, GetHit, Death) already interrupt these states.
    - **Attack layer**: mirror the same four states and transitions 1–9 (reuse the same blend tree object
      and clips) so the upper body follows the sneak pose when no combat state runs, and:
      - `Sneak Locomotion → CombatIdle`: `IsInCombat == true`, settings copied from
        `LockOn Locomotion → CombatIdle`.
      - `CombatIdle → Sneak Locomotion`: `IsInCombat == false` AND `IsSneaking == true`; add
        `IsSneaking == false` to the existing `CombatIdle → LockOn Locomotion`.
      - `Block_State → Sneak Locomotion`: `IsBlocking == false` AND `IsSneaking == true`; add
        `IsSneaking == false` to the existing `Block_State → LockOn Locomotion`.
    - **Gate — mask check (play mode):** draw the weapon (R), sneak (C), stand still, then attack. If the
      hips rise or the legs float/stretch, apply the fallback in Notes ("Risk: UpperBodyMask") before
      continuing, and record what was done in the spec.

#### Phase C — Perception

- [x] Task 11: Pure detection math
  - File: NEW `Assets/_Game/Scripts/Stealth/StealthDetection.cs` (namespace `Game.Stealth`,
    `public static class`, no Unity object access, no allocations)
  - Action — public static methods:
    - `float EffectiveSightRange(float sightRange, bool sneaking, float sneakMultiplier)`
    - `float EffectiveProximityRadius(float radius, bool sneaking, float sneakMultiplier)`
    - `float AngleToTarget(Vector3 forward, Vector3 toTarget)` — angle on the XZ plane (y zeroed); returns 0
      when `toTarget` is ~zero.
    - `float ComputeVisibility(float distance, float angleDeg, float sightRange, float viewAngle,
      float proximityRadius, float edgeDistanceFactor, float peripheralAngleFactor)` — returns 0..1:
      `distance <= proximityRadius` → 1 (any angle); `distance > sightRange` or `angleDeg > viewAngle / 2`
      → 0; otherwise `Lerp(1, edgeDistanceFactor, distance / sightRange) *
      Lerp(1, peripheralAngleFactor, angleDeg / (viewAngle / 2))`.
    - `float FillPerSecond(float visibility, float fillTime, bool sneaking, float sneakFillMultiplier)` =
      `visibility / fillTime * (sneaking ? sneakFillMultiplier : 1)`; returns 0 when `fillTime <= 0`.
    - `float StepAwareness(float current, float fillPerSecond, float drainPerSecond, bool visible, float dt)`
      — `visible && fillPerSecond > 0` → `current + fillPerSecond * dt`, else `current - drainPerSecond * dt`;
      clamp 0..1.
  - Notes: line of sight is not part of this class (needs physics). Callers pass `visibility = 0` when LOS
    is blocked.

- [x] Task 12: Stealth-target awareness in faction registry
  - File: `Assets/_Game/Scripts/AI/FactionMember.cs`
  - Action: `public IStealthTarget StealthTarget { get; private set; }` resolved in `Awake` with
    `TryGetComponent` (null for everything except the player).
  - File: `Assets/_Game/Scripts/AI/TargetRegistry.cs`
  - Action: add optional parameter `bool skipStealthTargets = false` to `FindClosestHostile` (skip members
    with `StealthTarget != null` when true). Add
    `FindClosestHostileStealthTarget(FactionSO myFaction, Vector3 origin, float maxRange)` — same filters
    (hostile, alive, in range, deterministic tie-break), only members with `StealthTarget != null`. Share
    the filtering in a private helper to avoid duplicating the loop body.

- [x] Task 13: `EntityPerception` component
  - File: NEW `Assets/_Game/Scripts/AI/EntityPerception.cs` (namespace `Game.AI`, TAG `"[AI]"`)
  - Serialized: `PersistentID _persistentID`, `FactionMember _selfFactionMember`,
    `StealthConfigSO _config` (fallback `GetComponent` for the first two in `Awake`; missing config or
    Entity SO → `GameLog.Error`, `enabled = false`).
  - Public API (read by `EntityBrain`, same system):
    - `bool IsActive` — enabled, config set, `Entity.DetectionRange > 0`.
    - `float Awareness`, `bool IsFullyAware => Awareness >= 1f`, `bool IsSuspicious => Awareness >=
      _config.suspicionThreshold`.
    - `FactionMember Target` (current stealth-target candidate), `bool CanSeeTarget`,
      `Vector3 LastSeenPosition`, `float TimeSinceSeen`.
    - `void Tick(float dt, bool engaged)`; `void ForceAware(FactionMember target)` (Target = target,
      Awareness = 1, LastSeenPosition = target position, TimeSinceSeen = 0); `void SetAwareness(float value)`
      (clamped); `void ResetPerception()` (awareness 0, target null).
  - `Tick` behaviour:
    1. Not `IsActive` → return.
    2. Every `targetScanInterval` (unless engaged with a valid target): `Target =
       TargetRegistry.FindClosestHostileStealthTarget(faction, position, max(DetectionRange,
       DisengageRange))`. A dead/null target → drop it.
    3. Every `losCheckInterval`: compute eye = `transform.position + up * Entity.EyeHeight`, point =
       `Target.StealthTarget.VisibilityPoint`, `hasLOS = !Physics.Raycast(eye, dir, distance,
       _config.lineOfSightMask, QueryTriggerInteraction.Ignore)`. Cache it between checks.
    4. Visibility: if `engaged` → `CanSeeTarget = hasLOS && distance <= Entity.DisengageRange` (360°: an
       entity in a fight tracks you), awareness held at 1. Else → visibility from `StealthDetection`
       (sneak-adjusted sight range and proximity radius, view angle, flat distance), `CanSeeTarget =
       hasLOS && visibility > 0`; `Awareness = StepAwareness(...)`.
    5. `CanSeeTarget` → `LastSeenPosition = Target.Transform.position`, `TimeSinceSeen = 0`; else
       `TimeSinceSeen += dt`.
  - Debug read-outs (read-only properties, updated in `Tick`, used by the debug views):
    `LastVisibility`, `LastFillPerSecond`, `HasLineOfSight`, `DistanceToTarget`, `CurrentSightRange`,
    `CurrentProximityRadius` (sneak-adjusted), `ViewAngle`, `EyePosition`.
  - Static registry for the overlay: `public static IReadOnlyList<EntityPerception> Active` backed by a
    `static readonly List<>`; add in `OnEnable`, remove in `OnDisable`, clear on
    `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` (same as `TargetRegistry`).
  - Gizmos are specified in Task 17.
  - Notes: no allocations; `Physics.Raycast` (single hit) does not allocate.

#### Phase D — Brain integration and sneak attack

- [x] Task 14: `EntityBrain` — perception-driven detection, Suspicious and Searching states
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    - Enum → `{ Idle, Patrolling, Suspicious, Warning, Engaging, Attacking, Searching, Dead }`; add
      cases in the `Update` switch. Update the class summary.
    - Debug: `public string DebugStateName => STATE_NAMES[(int)_state];` with a
      `static readonly string[] STATE_NAMES` built once from the enum (no per-frame `ToString()`).
    - Serialized `EntityPerception _perception` (+ `GetComponent` fallback in `Awake`) and
      `StealthConfigSO _stealthConfig`. Perception is **optional**: when null or `!IsActive`, behaviour is
      exactly today's (radius acquisition of every hostile, no Suspicious/Searching).
    - Each tick in any non-Dead state: `_perception.Tick(Time.deltaTime, engaged: state is Warning,
      Engaging or Attacking)`.
    - **Idle / Patrolling:** if `_perception.IsFullyAware` → `_currentTarget = _perception.Target` →
      `RespondToDetectedTarget()`. Else if `IsSuspicious` → `TransitionToSuspicious()`. Else the existing
      throttled radius acquisition with `skipStealthTargets: true`.
    - **Suspicious** (new): agent stopped; rotate toward `LastSeenPosition` at `suspiciousTurnSpeed`
      (new helper `FacePoint(Vector3, float degPerSec)`; `FaceTarget()` reuses it); fully aware →
      `RespondToDetectedTarget()`; awareness reaches 0 → `ResumeNonCombat()` (back to `_disengageState`,
      resuming the current waypoint rather than skipping it). `TransitionToSuspicious` captures
      `_disengageState` from Idle/Patrolling and does **not** set combat state.
    - **Warning / Engaging / Attacking:** when `_currentTarget` is the perception target and
      `_perception.TimeSinceSeen >= Entity.LoseSightTime` → `TransitionToSearching()`. While not seen (but
      before the timeout), Engaging moves to `LastSeenPosition` instead of the live target position.
      Existing range rules (`DetectionRange` cancel in Warning, `DisengageRange`) are unchanged.
    - **Searching** (new): `TransitionToSearching()` — keep combat state on (alert), `_agent.isStopped =
      false`, speed `EngageSpeed`, stopping distance 0, destination `LastSeenPosition`,
      `_searchTimer = Entity.SearchDuration`, `_perception.SetAwareness(searchStartAwareness)`, clear
      `_currentTarget`, `EndAttack()` on the melee attacker. `HandleSearching()`: fully aware → set
      `_currentTarget` → `TransitionToEngaging()` (no warning); when arrived (`!pathPending` and
      `remainingDistance <= WaypointArrivalThreshold`, or `pathStatus == PathInvalid`) → stop, rotate in
      place at `searchTurnSpeed`, count down `_searchTimer`; at 0 → `SetCombatState(false)` →
      `ResumeNonCombat()`.
    - **Damage reaction:** subscribe to `_entityHealth.HealthChanged` in `OnEnable`, unsubscribe in
      `OnDisable` (null-guarded); track the previous health. On a decrease while Idle, Patrolling,
      Suspicious, Searching or Warning: pick the attacker candidate — `_perception.Target` (or
      `FindClosestHostileStealthTarget` within `DisengageRange` when null); with perception inactive, fall
      back to `FindClosestHostile` within `DisengageRange`. Found → `_perception?.ForceAware(candidate)`,
      `_currentTarget = candidate`, `TransitionToEngaging()`. Ignore increases and dead state.
    - `TransitionToEngaging` / `TransitionToWarning` capture `_disengageState` only from Idle/Patrolling
      (Suspicious/Searching keep the value captured earlier).
    - `DisengageFromCombat` and `TransitionToDead` call `_perception?.ResetPerception()`.
    - Implement `ISneakAttackTarget.IsUnawareOf(GameObject attacker)`: `true` when the state is Idle,
      Patrolling or Suspicious and the perception is not fully aware; `false` otherwise (Searching counts
      as alert). Dead → false.
    - Log every new transition with `GameLog.Info`.

- [x] Task 15: Sneak-attack damage
  - File: `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
  - Action: in `OnWeaponHit`, before `TakeDamage`: `float damage = ComputeEffectiveDamage();` and if
    `target is Component c && c.TryGetComponent(out ISneakAttackTarget sneakTarget) &&
    sneakTarget.IsUnawareOf(gameObject)` → `damage *= _config.sneakAttackDamageMultiplier` and log
    `"Sneak attack x{mult}"`. Then `target.TakeDamage(damage)`.
  - Notes: `TakeDamage` raises `HealthChanged` synchronously, so the brain engages before the next combo hit
    lands — only the opening hit gets the bonus.

- [x] Task 16: Prefab wiring
  - Files: `Assets/_Game/Prefabs/Entities/Entity_base.prefab` (and check variants that remove/override
    root components, see `Prefabs/Entities/CLAUDE.md`)
  - Action: add `EntityPerception` to the `Entity_base` root (wire `_persistentID`, `_selfFactionMember`,
    `_config` = `StealthConfig.asset`); wire `_perception` and `_stealthConfig` on `EntityBrain`. Use MCP
    `manage_prefabs` / `manage_components`. Passive NPCs (`DetectionRange = 0`) keep the component but it
    stays inactive.

#### Phase E — Debug views

- [x] Task 17: Shared debug geometry + detection gizmos
  - File: NEW `Assets/_Game/Scripts/Stealth/StealthDebugGeometry.cs` (namespace `Game.Stealth`, static, pure)
  - Action: `int BuildConeOutline(Vector3 origin, Vector3 forward, float range, float viewAngle,
    int arcSegments, Vector3[] buffer)` — fills `buffer` with line-segment pairs (two side edges + the arc,
    flattened on XZ at `origin.y`; `viewAngle >= 360` → full circle, no side edges) and returns the point
    count. `int BuildCircle(Vector3 center, float radius, int segments, Vector3[] buffer)` the same way.
    Callers own the buffers (no allocation per call). Shared by the gizmos and the runtime overlay.
  - File: NEW `Assets/_Game/Scripts/Stealth/StealthDebug.cs` — `#if UNITY_EDITOR` static class with
    `public static bool DrawAllGizmos;` (mirrors `Combat/HitboxDebug.cs`).
  - File: NEW `Assets/_Game/Editor/StealthDebugMenu.cs` (`Game.Editor` asmdef) — menu item
    `Tools/Stealth/Show Detection Gizmos` toggling `StealthDebug.DrawAllGizmos`, checkmark via
    `Menu.SetChecked`, persisted in `EditorPrefs` (`"Game.StealthDebug.DrawAllGizmos"`), restored with
    `[InitializeOnLoadMethod]` — copy `Editor/HitboxDebugMenu.cs`.
  - File: `Assets/_Game/Scripts/AI/EntityPerception.cs`
  - Action: `OnDrawGizmos` → draw if `StealthDebug.DrawAllGizmos`; `OnDrawGizmosSelected` → draw if not
    already drawn by `OnDrawGizmos`. Shared `DrawDetectionGizmos()` (whole body in `#if UNITY_EDITOR`),
    reusing a static `Vector3[]` buffer:
    - cone at `DetectionRange` (solid colour) and at the sneak sight range (dimmer); in edit mode, use
      the Entity SO values and the transform.
    - proximity circles (standing and sneaking radius).
    - play mode, with a target: line eye → target visibility point, green when `HasLineOfSight`, red
      when blocked; small wire sphere at `LastSeenPosition` while `TimeSinceSeen > 0`.
    - colour of the cone lerps green (0) → yellow (suspicion threshold) → red (1) by `Awareness`.
    - `Handles.Label` above the eye (play mode): `"{DebugStateName}\nAwareness 64%  Vis 0.42
      +0.21/s\nDist 5.3 m  LOS yes  Seen 0.0 s ago\nTarget: sneaking"` — brain read through a cached
      `TryGetComponent<EntityBrain>`.

- [x] Task 18: In-game stealth debug overlay (F3, dev only)
  - File: NEW `Assets/_Game/Scripts/Debug/StealthDebugOverlay.cs` (namespace **`Game.DevTools`** — never
    `Game.Debug`, see `Scripts/Debug/CLAUDE.md`; TAG `"[DevTools]"`)
  - Availability: in `Awake`, if `!Debug.isDebugBuild` → `enabled = false` and deactivate its canvas
    (true in the editor and development builds, false in release). Starts **hidden**.
  - Input: own `InputSystem_Actions`; `Player.ToggleStealthDebug.started` toggles visibility (OnEnable /
    null-guarded OnDisable pattern). Log the new state.
  - Serialized: `Material _lineMaterial` (NEW `Assets/_Game/Data/Debug/M_DebugLines.mat`, shader
    `Hidden/Internal-Colored` — vertex colours, referenced by the material so builds include it),
    `Canvas _canvas` (Screen Space Overlay — set `renderMode` to 0 after creation, MCP quirk in root
    `CLAUDE.md`), `TMP_Text _labelPrefab`, `TMP_Text _summaryText` (top-left panel),
    `float _maxDrawDistance = 40f`, `int _labelPoolSize = 16`.
  - Lines: subscribe to `RenderPipelineManager.endCameraRendering` in `OnEnable` (unsubscribe in
    `OnDisable`); only for `Camera.main` (cached, re-fetched if null) and while visible. For each
    `EntityPerception.Active` with `IsActive` within `_maxDrawDistance` of the camera: `GL.PushMatrix`,
    `_lineMaterial.SetPass(0)`, `GL.Begin(GL.LINES)` — same shapes and colours as the gizmos, built with
    `StealthDebugGeometry` into a reused buffer; `GL.End`, `GL.PopMatrix`.
  - Labels: pre-instantiated pool of `_labelPoolSize` TMP labels under `_canvas`. In `LateUpdate`, for each
    drawn entity: screen position of `EyePosition + up * 0.5` via `WorldToScreenPoint`; hide when behind
    the camera (`z < 0`) or the pool is exhausted. Text = compact form of the gizmo label (state,
    awareness %, LOS, distance), built with a cached `StringBuilder` and `TMP_Text.SetText(StringBuilder)`
    (no per-frame string allocation).
  - Summary panel: player sneaking yes/no, visibility height, and the highest `Awareness` among active
    perceptions with its entity name and state ("Top awareness 64% — Bandit_01 (Suspicious)"). The player's
    `IStealthTarget` comes from any active perception's `Target.StealthTarget`; if none has a target, show
    "no hostile perceiving".
  - Scene setup: GO `StealthDebugOverlay` in `Core.unity` (always-loaded scene) with a child canvas
    (sorting order above the game UI) holding the summary text and the label root. Create via MCP; check
    `Scenes/CLAUDE.md` for the scene-load quirk. Label prefab: NEW
    `Assets/_Game/Prefabs/UI/Debug/StealthDebugLabel.prefab` (TMP text, small font, outline, centered,
    raycast target off).
  - Notes: no allocations per frame (no LINQ, no `FindObjectsByType`, no string concatenation).

#### Phase F — Tests and docs

- [x] Task 19: EditMode tests
  - File: NEW `Assets/Tests/EditMode/StealthDetectionTests.cs` — see Testing Strategy.
  - File: `Assets/Tests/EditMode/TargetRegistryTests.cs` — add a stub `MonoBehaviour, IStealthTarget`
    component; cover `skipStealthTargets` and `FindClosestHostileStealthTarget`.
  - File: `Assets/Tests/EditMode/PlayerStateManagerTests.cs` — mirrored gate formulas for `CanSneak`
    (false when busy, airborne or dodging; true while attacking or blocking).
  - File: NEW `Assets/Tests/EditMode/StealthDebugGeometryTests.cs` — cone outline point count
    (2 edges + arc segments, ×2 points per segment), arc endpoints at `range` and ±half-angle, 360° → closed
    circle without edges, buffer too small → clamps without throwing.

- [x] Task 20: Documentation
  - `Assets/_Game/Scripts/Player/CLAUDE.md`: `IsSneaking` / `CanSneak` / `SetSneaking` /
    `ExitSneakToSprint` in the gating table; `PlayerSneak` role; exit rules; new animation wrappers.
  - `Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md`: new params, sneak states on both
    layers, transition order rule (SneakToSprint before Sneak To Stand), and fix "Attack layer weighted by
    IsInCombat" → constant weight 1 with its own locomotion state; any new Base locomotion state needs an
    Attack-layer twin. Same correction in `Scripts/Player/CLAUDE.md` ("also weights the Attack layer").
  - `Assets/_Game/Scripts/AI/CLAUDE.md`: `EntityPerception` row, new states, damage reaction, sneak-attack
    contract, "perception is optional".
  - NEW `Assets/_Game/Scripts/Stealth/CLAUDE.md`: contracts, `StealthDetection` formula, config fields,
    tuning table, debug views (gizmo menu, F3 overlay); add it to the root `CLAUDE.md` folder index.
  - `Assets/_Game/Scripts/Debug/CLAUDE.md`: `StealthDebugOverlay` (F3, `Debug.isDebugBuild` only, lives in
    `Core.unity`).

### Acceptance Criteria

**Sneak stance**
- [ ] AC 1: Given the player stands on the ground, when C is pressed, then `IsSneaking` becomes true,
  `stand to sneak` plays, and the player moves at `sneakSpeed` (1.5) with sneak locomotion clips in all
  four directions.
- [ ] AC 2: Given the player is sneaking, when C is pressed again, then `IsSneaking` becomes false,
  `sneak to stand` plays, and movement returns to walk speed.
- [ ] AC 3: Given the player is sneaking and moving, when Sprint is held, then sneak ends on that frame,
  `sneak to sprint` plays and blends into `Running`, and the player moves at `runSpeed`.
- [ ] AC 4: Given the player is sneaking and standing still, when Sprint is held without moving, then the
  player keeps sneaking.
- [ ] AC 5: Given the player is sneaking with a weapon drawn, when they attack (full combo) or block, then
  the legs keep the sneak pose, the upper body plays the attack/block, and `IsSneaking` is still true
  afterward.
- [ ] AC 6: Given the player is sneaking, when they dodge or jump, then sneak ends and the dodge/jump plays
  without a stand-up clip first.
- [ ] AC 7: Given the player is airborne, dodging, dead, or has the cursor unlocked (menu/dialogue), when C
  is pressed, then sneak is not entered.
- [ ] AC 8: Given the player dies while sneaking or loads a save, then the player is not sneaking afterward.
- [ ] AC 9: Given a humanoid NPC using `Humanoid_Template`, then its locomotion and combat animations are
  unchanged.

**Detection**
- [ ] AC 10: Given a hostile entity idles facing away, when the standing player walks behind it beyond
  `ProximityRadius`, then its awareness stays 0 and it does not react.
- [ ] AC 11: Given the player stands inside the cone and range with clear line of sight, then awareness
  fills over time — faster when closer and nearer the cone's centre — and the entity goes into Warning or
  Engaging (existing rules) when it reaches 1.
- [ ] AC 12: Given a wall (Default layer) between the entity and the player, when the player is inside the
  cone and range, then awareness does not fill.
- [ ] AC 13: Given the player sneaks at a distance between the sneak sight range (≈ 4.8 m) and
  `DetectionRange` (8 m) inside the cone, then awareness does not fill; standing at the same spot it does.
- [ ] AC 14: Given the player sneaks within the cone, then awareness fills at half the standing rate.
- [ ] AC 15: Given the player stands behind the entity within `ProximityRadius` (2 m), then awareness fills;
  sneaking at 1.5 m behind it does not (sneak proximity ≈ 0.7 m).
- [ ] AC 16: Given awareness rises past the suspicion threshold but not to 1, then the entity stops and turns
  toward the last seen position; when awareness drains to 0, it resumes its idle wander or patrol from the
  same waypoint.
- [ ] AC 17: Given an entity with `DetectionRange = 0` (passive NPC), then it never builds awareness and
  behaves as before.
- [ ] AC 18: Given two hostile NPC factions, then they still detect each other with today's radius check.

**Losing the player and searching**
- [ ] AC 19: Given an entity is engaging the player, when the player breaks line of sight for
  `LoseSightTime` (2 s), then the entity moves to the last seen position, looks around for `SearchDuration`
  (6 s), then returns to its idle/patrol behaviour.
- [ ] AC 20: Given an entity is searching, when it fully detects the player again, then it engages directly
  (no warning telegraph).
- [ ] AC 21: Given the last seen position is unreachable, then the entity searches from where it stands.
- [ ] AC 22: Given an engaged entity keeps line of sight, then the existing `DisengageRange` rule still ends
  the chase.

**Sneak attack and damage reaction**
- [ ] AC 23: Given an entity has not detected the player (Idle, Patrolling or Suspicious), when the player's
  hit lands, then damage = effective damage × `sneakAttackDamageMultiplier` (2) and the log shows the sneak
  attack.
- [ ] AC 24: Given a sneak attack lands as combo hit 1, then hits 2 and 3 deal normal damage.
- [ ] AC 25: Given an entity is Warning, Engaging, Attacking or Searching, then hits deal normal damage.
- [ ] AC 26: Given an idle entity is hit by the player from outside its detection cone/range, then it becomes
  fully aware and engages the player.

**Quality**
- [ ] AC 27: Given the project compiles, then there are no new console errors in play mode and all
  EditMode tests pass, including the new ones.
- [ ] AC 28: Given an entity is selected in the Scene view, then its cones (standing and sneaking range),
  proximity circles and awareness colour are drawn as gizmos, and in play mode the LOS line (green/red),
  last seen marker and the text label (state, awareness %, visibility, fill rate, distance, LOS, time
  since seen) are shown.
- [ ] AC 29: Given `Tools/Stealth/Show Detection Gizmos` is checked, then every entity with an active
  perception draws its gizmos without being selected, and the setting survives an editor restart.

**Keys and dev overlay**
- [ ] AC 30: Given the game is running, when C is pressed, then only sneak toggles (no stats tab); when P is
  pressed, then the character stats tab opens/closes as C did before.
- [ ] AC 31: Given the editor or a development build, when F3 is pressed, then the overlay shows cone/LOS
  lines in the Game view for active perceptions within 40 m, a label above each, and the summary panel;
  pressing F3 again hides everything.
- [ ] AC 32: Given a release (non-development) build, when F3 is pressed, then nothing is shown.
- [ ] AC 33: Given the overlay is visible, then the Profiler shows no per-frame GC allocations from
  `StealthDebugOverlay`, and labels for entities behind the camera are hidden.

## Additional Context

### Dependencies

- Mixamo sneak clips in `Art/Characters/Humanoids/Animations/Sneaking/` (provided 2026-10-08): `sneak idle`,
  `sneak walk`, `sneak left`, `sneak right`, `stand to sneak`, `sneak to stand`, `sneak to sprint`.
- Unity MCP for import settings, the animator controller, the config asset and prefab wiring.
- No new packages. No dependency on other in-progress specs.

### Testing Strategy

**EditMode unit tests — `StealthDetectionTests`:**
- `AngleToTarget`: straight ahead = 0; directly behind = 180; ignores Y; zero vector = 0.
- `ComputeVisibility`: inside proximity at 180° = 1; outside range = 0; outside half-angle = 0; centre at
  distance 0 = 1; far edge on axis = `edgeDistanceFactor`; side edge at distance 0 = `peripheralAngleFactor`;
  monotonically decreasing with distance and with angle; `viewAngle = 360` sees behind.
- `EffectiveSightRange` / `EffectiveProximityRadius`: standing unchanged; sneaking multiplied.
- `FillPerSecond`: standing visibility 1, fillTime 1 → 1/s; sneaking ×0.5; fillTime 0 → 0.
- `StepAwareness`: fills when visible, drains when not, clamps to [0, 1]; visible with fill 0 drains.
- Scenario: sneaking at 6 m with range 8 and multiplier 0.6 → visibility 0; standing → > 0.

**EditMode — registry and gates:** `TargetRegistryTests` (stealth filtering both ways, dead stealth target
skipped); `PlayerStateManagerTests` (mirrored `CanSneak` formula).

**Manual play-mode checklist** (StartingTown or TestScene with a spider and a bandit):
1. Toggle sneak standing still and while moving; check all directions, backward included (AC 1–2).
2. Sneak → hold Shift while moving / standing still (AC 3–4).
3. Draw the weapon while sneaking, full combo, block — check the hips/legs (AC 5; Task 10 gate).
4. Dodge and jump out of sneak (AC 6); press C in the inventory menu (AC 7).
5. Select an entity, watch gizmos; approach from behind standing / sneaking; stand at 6 m in front standing
   vs sneaking; hide behind a wall (AC 10–15, 28).
6. Get half-noticed and back off — the entity turns, then resumes patrol (AC 16).
7. Get engaged, break LOS behind a building — search, then return; get re-spotted during search (AC 19–21).
8. Sneak-attack an idle bandit with a 3-hit combo; read the damage log (AC 23–24); hit an engaged one (AC 25).
9. Hit an idle entity from behind outside its cone — it fights back (AC 26).
10. Watch two hostile NPC factions fight; talk to a passive NPC (AC 17–18); save and load while sneaking (AC 8).
11. Press C (sneak only) and P (stats tab) (AC 30).
12. Toggle `Tools/Stealth/Show Detection Gizmos`; restart the editor and check it stays on (AC 28–29).
13. F3 in play mode: lines, labels, summary; turn the camera so an entity is behind it; Profiler GC Alloc
    column for `StealthDebugOverlay` (AC 31, 33). Make one release build and press F3 (AC 32).

The overlay is also the main tuning tool: use it for steps 5–9 to read awareness and fill rates live.

### Notes

- **Risk: `UpperBodyMask` includes the humanoid Body part.** If Body carries hip height/rotation,
  `CombatIdle` and attack clips on the Attack layer would pull the hips up to standing height over crouched
  legs whenever the weapon is drawn while sneaking. Verify in play mode right after the animator task.
  Fallback: a synced layer `Attack_Sneak` (synced to Attack, mask = Head + arms + fingers only), with
  `HumanoidAnimationBridge.SetSneaking` swapping the two layers' weights; check that animation events and
  SMBs don't fire twice from the synced layer.
- **Risk: shared controller edits.** `Humanoid_Template` drives both the player and humanoid NPCs. Every new
  transition depends on `IsSneaking` / `SneakToSprint`, which NPCs never set, so they stay on today's paths.
  AC 9 covers it.
- **Risk: transition clip length.** If `stand to sneak` / `sneak to stand` feel slow, raise those states'
  speed multipliers. The movement early-outs stop the player sliding in a static pose.
- **Known limitations:** after `GetHit` the Base layer returns to `LockOn Locomotion` and replays
  `stand to sneak` (still sneaking, short re-crouch). Searching entities count as alert, so they take no
  sneak-attack bonus. Detection ignores lighting and noise. Each entity tracks one stealth target (the
  player).
- **Tuning start points:** view angle 110°, sight 8 m (sneaking 4.8 m), proximity 2 m (sneaking 0.7 m),
  fill 1 s at best visibility (≈ 5 s at the far edge standing, 10 s sneaking), drain 0.25/s, suspicion 0.5,
  lose sight 2 s, search 6 s, sneak attack ×2.
- **Future (out of scope):** HUD eye and `?`/`!` markers (read `EntityPerception.Awareness`); neutral NPC
  suspicion and stealing; noise from running or combat; light levels; a stealth skill scaling the sneak
  multipliers; smarter search (several points, alerting allies); takedown animations.

## Implementation Notes (2026-10-08)

- **Task 10 gate — mask check: passed, no fallback.** Play mode, Animator stepped manually: hips 1.03 m
  standing, 0.72–0.73 m in sneak idle, sneak + CombatIdle, sneak + Attack_1 and sneak + Block. The
  `UpperBodyMask` Body part does not lift the hips, so the `Attack_Sneak` synced-layer fallback was not needed.
  The sneak → sprint path (`Sneak To Sprint` → `LockOn Locomotion`, trigger consumed) was checked the same way.
- `sneak left` / `sneak right` direction could not be checked automatically (root motion is baked into the
  pose). Check it in the manual pass (checklist step 1) and swap the two blend positions if they're reversed.
- `ToggleStealthDebug`: the hand-added actions in `InputSystem_Actions.cs` (QuickSave/QuickLoad) have no
  `IPlayerActions` callback entries, so neither does this one (no `OnToggleStealthDebug`).
- `StealthDetection.ComputeVisibility`: a proximity radius of 0 means "no proximity sense". Without this,
  distance 0 ≤ radius 0 counted as "inside proximity".
- `CancelWarning` also resets perception. Otherwise the awareness held at 1 during Warning makes Idle
  re-detect on the next frame, so the entity would flicker between Idle and Warning.
- Damage reaction is skipped for passive entities (`DetectionRange <= 0`), so they behave as before (AC 17).
- `EntityPerception` exposes `SightRange`, `ProximityRadius`, `Brain`, `DebugName`, `TargetIsSneaking`,
  `HasLastSeenPosition` and `Config` in addition to the spec's read-outs, for the overlay and gizmos.
- Label prefab: bold text instead of a TMP outline. Changing the outline at authoring time would edit the
  shared font material.
- Only the spider got a custom `_eyeHeight` (0.4). Rat, viper and wolf keep 1.6, so their LOS sees over low
  cover. Tune them if needed.
- Core.unity was already marked dirty in the editor before this work (zeroed RectTransform overrides on the
  Player instance). Only the `StealthDebugOverlay` objects were kept in the saved scene.
- EditMode: 574/574 passing. The manual play-mode checklist (steps 1–13) is still open.

## Review Notes

- Adversarial review completed (separate subagent, diff-only context).
- Findings: 18 total. 10 fixed (F1–F3, F5–F9, F11, F13, F15); the other 8 (F4, F10, F12, F14, F16–F18)
  were rated undecided or noise and left as they are.
- Resolution approach: auto-fix (real findings only).
- Main fixes: `ISneakAttackTarget.NotifyHitBy(attacker)` is called by every damage source, so the damage
  reaction engages the actual attacker and never a bystander. `StealthConfigSO.damageAlertDuration` (10 s)
  ends repeated sneak-attack bonuses on passive and neutral entities. `suspicionThreshold` is clamped above 0.
  `FactionMember.StealthTarget` reads null while `PlayerSneak` is disabled. Sneak ends when airborne.
  `sneakAttackDamageMultiplier` has a minimum of 1. `CanSneak` tests now exercise the real
  `PlayerStateManager.EvaluateCanSneak`.
- EditMode 574/574 after the fixes. The manual play-mode checklist (steps 1–13) is still open.
