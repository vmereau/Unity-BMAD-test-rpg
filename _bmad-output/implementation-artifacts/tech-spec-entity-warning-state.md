---
title: 'Entity Warning State (Detection-to-Engage Buffer)'
slug: 'entity-warning-state'
created: '2026-05-25'
status: 'Completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C# / .NET Standard 2.1', 'NavMeshAgent', 'Animator', 'URP 17.x']
files_to_modify: ['Assets/_Game/ScriptableObjects/Entities/Entity.cs', 'Assets/_Game/Scripts/AI/EntityBrain.cs', 'Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs', 'Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs', 'Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs', 'Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs', 'Assets/_Game/Art/Characters/Monsters/EntityBase.controller', 'Assets/_Game/Scripts/Core/Animations/CLAUDE.md']
code_patterns: ['enum-driven switch FSM', 'config-SO for tunables (no magic numbers)', 'FormerlySerializedAs on renamed serialized fields', 'AIAnimationDriver polymorphic seam (bridge=param writes, driver=lifecycle)', 'GameLog + per-class TAG', '#if UNITY_EDITOR OnValidate with GameLog.Warn']
test_patterns: ['no game-code tests currently exist', 'manual play-mode verification', 'optional EditMode SO-validation test (no infra set up yet)']
---

# Tech-Spec: Entity Warning State (Detection-to-Engage Buffer)

**Created:** 2026-05-25

## Overview

### Problem Statement

Hostile entities currently engage the player the instant the player enters `DetectionRange`
(`EntityBrain.IsPlayerInDetectionRange()` → `TransitionToEngaging()` from Idle/Patrol). There is
no telegraph, no "you've been spotted" beat, and no chance for the player to retreat before combat
starts. The detection model is binary: out-of-range or engaging.

### Solution

Insert a `Warning` state between detection and engagement. When the player is in the band between
`WarningRange` and `DetectionRange`, the entity stops, faces the player, and holds a warning
animation while a configurable timer counts down. The entity escalates to `Engaging` when the
timer elapses **or** when the player crosses below `WarningRange`; it cancels the warning and
returns to its prior Idle/Patrol state if the player leaves `DetectionRange`. A per-entity
`_engageImmediately` toggle in `EntityBrain`'s Behavior section bypasses the warning entirely
(legacy instant-engage behavior).

### Scope

**In Scope:**

- `Entity.cs` SO: add `_warningRange` and `_warningTimer` fields + getters, with `OnValidate`
  clamping `_warningRange` below `_detectionRange`.
- `EntityBrain.cs`: add `Warning` state to the enum; add `[SerializeField] bool _engageImmediately`
  to the Behavior header; implement detection→warning→engage decision logic; implement manual
  "face the player" rotation while warning; implement the warning timer; cancel-and-return on
  detection-range exit.
- Animation seam: add `SetWarning(bool)` to `AIAnimationDriver` (virtual). `MonsterAnimationDriver`
  forwards to `MonsterAnimationBridge`, which writes a new `IsWarning` bool animator parameter.
  `HumanoidAIAnimationDriver` warn-log stubs `SetWarning` (consistent with its existing combat-trigger stubs).

**Out of Scope:**

- Sound cues / VO for the warning (explicitly "more will come later").
- Humanoid warning **animation** implementation (humanoid AI combat epic not yet built — stub only).
- Authoring the actual warning animation clip and wiring the monster animator controller's
  `IsWarning` state/transitions (flagged as Editor follow-up; the bridge writes the parameter).
- HUD/UI "you are detected" indicator beyond the animation telegraph.
- Changes to the Player animation path (`PlayerAnimationDriver`) — Player is not AI-driven.

## Context for Development

### Codebase Patterns

- **Enum-driven switch state machine** in `EntityBrain.Update()` — no external FSM library
  (per `project-context.md`). Add `Warning` to `private enum EntityState`.
- **All tunable values live in the Entity SO** — never hardcode ranges/timers in `EntityBrain`
  (config-SO rule). Warning range + timer go on `Entity`.
- **Animation polymorphism via `AIAnimationDriver`** — `EntityBrain`/`EntityHealth` reference only
  the abstract `AIAnimationDriver`; concrete driver/bridge bindings happen in the prefab inspector.
  AI code must NOT reference concrete bridge/driver types (`Scripts/Core/Animations/CLAUDE.md`).
- **Bridge = pure animator-parameter writes; Driver = lifecycle/smoothing.** A new animator
  parameter is written by the bridge; the driver exposes the polymorphic method.
- **`GameLog` only**, every class has `private const string TAG`. `Info`/`Warn` stripped in Release.
- **`FormerlySerializedAs`** is used liberally on `Entity` serialized fields to preserve asset data
  across renames — follow the existing convention when adding fields.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Base SO — add warning range + timer fields/getters + OnValidate clamp |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | State machine — add Warning state, toggle, facing, timer |
| `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs` | Polymorphic seam — add `virtual void SetWarning(bool)` |
| `Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs` | Override `SetWarning` → bridge |
| `Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs` | Add `IsWarning` bool param write |
| `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs` | Warn-log stub `SetWarning` |
| `Assets/_Game/Art/Characters/Monsters/EntityBase.controller` | Monster animator — add `IsWarning` bool param + warning state (Editor follow-up) |
| `Assets/_Game/Scripts/Core/Animations/CLAUDE.md` | Seam rules — update checklist for new param |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | Reference only — death path calls `TriggerDeath`; confirms warning must clear on death |
| `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs` | Reference only — canonical `#if UNITY_EDITOR` OnValidate + GameLog.Warn pattern to mirror |
| `Assets/_Game/ScriptableObjects/Entities/Monsters/MonsterEntity.cs` | Reference only — subclass of `Entity`, inherits new fields automatically |

### Technical Decisions

- **Warning animation = sustained `IsWarning` bool**, not a one-shot trigger — the warning pose
  must hold for the multi-second timer and exit cleanly. (User-confirmed.)
- **Humanoid warning = stub** (warn-log no-op on `HumanoidAIAnimationDriver`), monster fully wired.
  The detection/timer/facing **logic** runs identically for both; only the humanoid animation is
  deferred. (User-confirmed, consistent with existing humanoid combat-trigger stubs.)
- **Player leaves detection range during Warning → cancel & return** to prior Idle/Patrol state,
  reset warning timer. (User-confirmed.)
- **Facing the player while warning** must be manual: the agent is stopped in Warning, so
  `NavMeshAgent` auto-rotation does not apply. Use `Quaternion.RotateTowards` toward
  `LookRotation` of the horizontal direction to the player, at a configurable turn speed.
- **`IsWarning` must be cleared on every exit from Warning** — including death. `EntityHealth.Die()`
  → brain `Update()` detects `IsDead` → `TransitionToDead()`. If the entity dies mid-warning the
  bool would stick, so `SetWarning(false)` belongs in `TransitionToEngaging`, `DisengageFromCombat`,
  and `TransitionToDead` (and any other warning-exit path).
- **Animator param ordering is safe**: `Animator.SetBool("IsWarning", …)` on a controller that
  lacks the param is a silent no-op in Unity (editor-only warning, no exception). So the C# can be
  committed before `EntityBase.controller` is wired — the warning logic runs, the animation simply
  doesn't play until the param + state exist. Adding the param/state is an **Editor follow-up**, not
  a code task.
- **`OnValidate` clamp pattern** (mirroring `QuestFact.cs`): guard with `#if UNITY_EDITOR`, clamp
  `_warningRange` to `(0, _detectionRange)` and warn via `GameLog.Warn(TAG, …)` when out of range.
  `Entity` needs a `TAG` const added (it currently has none).
- **Both subclasses (`MonsterEntity`, `NPCEntity`) inherit the new fields for free** — fields go on
  the `Entity` base, no subclass edits required. This satisfies "both humanoid and monster can have
  a warning distance."

## Implementation Plan

> Order matters: SO data first (Task 1), then the animation seam (Tasks 2–4) so `EntityBrain` can
> compile against `SetWarning`, then the brain logic (Task 5), then docs + Editor wiring (Tasks 6–7).

### Tasks

- [x] **Task 1: Add warning config to the Entity base SO**
  - File: `Assets/_Game/ScriptableObjects/Entities/Entity.cs`
  - Action:
    - Add `private const string TAG = "[Entity]";` (the class currently has none).
    - Under the `[Header("Detection")]` block, add three serialized fields:
      - `[SerializeField] private float _warningRange = 5f;` (tooltip: "Inner radius. Player between WarningRange and DetectionRange triggers the warning telegraph. Must be below DetectionRange.")
      - `[SerializeField] private float _warningEngageTime = 3f;` (tooltip: "Seconds the player may linger in the warning band before the entity escalates to Engaging.")
      - `[SerializeField] private float _warningTurnSpeed = 540f;` (tooltip: "Degrees/second the entity rotates to face the player while warning.")
    - Add read-only getters: `public float WarningRange => _warningRange;`, `public float WarningEngageTime => _warningEngageTime;`, `public float WarningTurnSpeed => _warningTurnSpeed;`
    - Add an editor-only validator mirroring `QuestFact.cs`:
      ```csharp
      #if UNITY_EDITOR
      private void OnValidate()
      {
          if (_warningRange < 0f) _warningRange = 0f;
          if (_warningRange >= _detectionRange)
          {
              GameLog.Warn(TAG, $"'{name}': _warningRange ({_warningRange}) must be below _detectionRange ({_detectionRange}) — clamped.");
              _warningRange = Mathf.Max(0f, _detectionRange - 0.5f);
          }
      }
      #endif
      ```
  - Notes: New fields use plain names (no `FormerlySerializedAs`) since they are brand-new. `GameLog` lives in `Game.Core`; add `using Game.Core;` if not already present. Both `MonsterEntity` and `NPCEntity` inherit these fields automatically — no subclass edits.

- [x] **Task 2: Add the `SetWarning` virtual to the polymorphic seam**
  - File: `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs`
  - Action: Add `public virtual void SetWarning(bool active) { }` alongside the existing `TriggerAttack` / `TriggerGetHit` virtuals.
  - Notes: Default no-op base, matching the existing seam style.

- [x] **Task 3: Implement the monster warning animation write**
  - File: `Assets/_Game/Scripts/Core/Animations/MonsterAnimationBridge.cs`
  - Action:
    - Add `private static readonly int IsWarningHash = Animator.StringToHash("IsWarning");`
    - Add `public void SetWarning(bool active) { if (_animator == null) return; _animator.SetBool(IsWarningHash, active); }`
  - File: `Assets/_Game/Scripts/Core/Animations/MonsterAnimationDriver.cs`
  - Action: Add `public override void SetWarning(bool active) => _bridge?.SetWarning(active);`
  - Notes: Bridge stays a pure parameter writer; driver exposes the polymorphic override. `SetBool` on a controller missing the `IsWarning` param is a silent no-op — safe to land before Task 7.

- [x] **Task 4: Stub the humanoid warning animation**
  - File: `Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs`
  - Action: Add `public override void SetWarning(bool active) { if (active) GameLog.Warn(TAG, $"{name}: humanoid AI warning animation not implemented yet"); }`
  - Notes: Consistent with the existing `TriggerAttack` stub. Guarded on `active` so it logs once on enter, not again on the exit (`false`) call — keeps dev logs quiet. Detection/timer/facing logic still runs for humanoids; only the animation is deferred.

- [x] **Task 5: Add the Warning state + toggle + facing + timer to EntityBrain**
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    - Add `Warning` to the enum: `private enum EntityState { Idle, Patrolling, Warning, Engaging, Attacking, Dead }`
    - Under `[Header("Behavior")]`, after `_canEngagePlayer`, add:
      `[Tooltip("Skip the warning telegraph and engage the instant the player is detected.")] [SerializeField] private bool _engageImmediately = false;`
    - Add runtime field `private float _warningTimer;`
    - Add a detection-response helper and call it from `HandleIdle` and `HandlePatrol` in place of the current direct `TransitionToEngaging()`:
      ```csharp
      private void RespondToDetectedPlayer()
      {
          if (_engageImmediately) { TransitionToEngaging(); return; }
          float dist = Vector3.Distance(transform.position, _player.position);
          if (dist <= _persistentID.Entity.WarningRange) TransitionToEngaging();
          else TransitionToWarning();
      }
      ```
    - In `HandleIdle` and `HandlePatrol`, change `if (_canEngagePlayer && IsPlayerInDetectionRange()) { TransitionToEngaging(); return; }` to call `RespondToDetectedPlayer();` instead.
    - Add the `Warning` case to the `Update()` switch: `case EntityState.Warning: HandleWarning(); break;`
    - Implement `HandleWarning()`:
      ```csharp
      private void HandleWarning()
      {
          if (_player == null) { CancelWarning(); return; }
          float dist = Vector3.Distance(transform.position, _player.position);
          if (dist > _persistentID.Entity.DetectionRange) { CancelWarning(); return; }   // player escaped
          if (dist <= _persistentID.Entity.WarningRange) { TransitionToEngaging(); return; } // crossed inner ring
          FacePlayer();
          _warningTimer -= Time.deltaTime;
          if (_warningTimer <= 0f) TransitionToEngaging();
      }
      ```
    - Implement `FacePlayer()` (Y-only rotation, manual since the agent is stopped):
      ```csharp
      private void FacePlayer()
      {
          Vector3 dir = _player.position - transform.position;
          dir.y = 0f;
          if (dir.sqrMagnitude < 0.0001f) return;
          Quaternion target = Quaternion.LookRotation(dir);
          float maxDeg = _persistentID.Entity.WarningTurnSpeed * Time.deltaTime;
          transform.rotation = Quaternion.RotateTowards(transform.rotation, target, maxDeg);
      }
      ```
    - Add `TransitionToWarning()`:
      ```csharp
      private void TransitionToWarning()
      {
          if (_state == EntityState.Idle || _state == EntityState.Patrolling)
              _disengageState = _state;
          _state = EntityState.Warning;
          _agent.isStopped = true;
          _warningTimer = _persistentID.Entity.WarningEngageTime;
          _animationDriver?.SetWarning(true);
          GameLog.Info(TAG, $"{gameObject.name} detected player — warning");
      }
      ```
    - Add `CancelWarning()` (player left detection range during warning → return to prior state):
      ```csharp
      private void CancelWarning()
      {
          _animationDriver?.SetWarning(false);
          GameLog.Info(TAG, $"{gameObject.name} lost player during warning — standing down");
          if (_disengageState == EntityState.Idle) TransitionToIdle(_idleOrigin);
          else TransitionToPatrol();
      }
      ```
    - In `TransitionToEngaging()` and `TransitionToDead()`, add `_animationDriver?.SetWarning(false);` as the first line so the warning pose clears on every exit from Warning (timer-elapse, cross-inner-ring, and death-while-warning).
  - Notes: `_agent.isStopped = true` during Warning means the agent does NOT auto-rotate — `FacePlayer()` is required. Warning is only entered from Idle/Patrol (first contact); Attacking→Engaging re-entry never re-warns. `_engageImmediately = true` reproduces the exact pre-change behavior.

- [x] **Task 6: Update the animation seam documentation**
  - File: `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`
  - Action: Document the new `SetWarning(bool)` seam method (held bool, not a trigger), the monster `IsWarning` bool param, and that the humanoid driver stubs it (warn-log on `active`). Add a checklist row: "MEDIUM — `IsWarning` animator param missing on a monster controller using warning detection; `SetBool` no-ops silently so the telegraph never plays."
  - Notes: Keeps the folder doc authoritative for the next agent.

- [x] **Task 7 (Editor follow-up, not C#): Wire the monster animator controller** _(wired via Unity MCP: `IsWarning` bool param + `Warning` state + Idle/Walk/Run→Warning and Warning→Idle transitions. Placeholder motion = `IdleBreathe`; swap in a dedicated warning clip when the art asset exists.)_
  - File: `Assets/_Game/Art/Characters/Monsters/EntityBase.controller`
  - Action (in the Unity Animator window, or via Unity MCP): add a `bool` parameter named exactly `IsWarning`; add a `Warning` state with the warning clip; add transitions Idle/Walk/Run → Warning (`IsWarning == true`) and Warning → Idle (`IsWarning == false`).
  - Notes: This is the only step that requires the Unity Editor. Until it's done the warning **logic** runs (entity stops, faces player, escalates correctly) but no warning pose plays. The warning clip itself is an art/design asset; if none exists yet, a placeholder/idle-variant is acceptable to validate the wiring.

### Acceptance Criteria

- [ ] **AC1 (warning entry):** Given a monster with `_engageImmediately = false`, `WarningRange = 5`, `DetectionRange = 8`, and the player at distance 7, when the player enters detection range from Idle/Patrol, then the entity stops, rotates to face the player, sets `IsWarning = true`, and logs "detected player — warning".
- [ ] **AC2 (timer escalation):** Given the entity is in Warning and the player stays in the 5–8 band, when `WarningEngageTime` seconds elapse, then the entity transitions to Engaging and `IsWarning` is set false.
- [ ] **AC3 (cross inner ring):** Given the entity is in Warning, when the player moves to distance ≤ `WarningRange` (≤ 5), then the entity transitions to Engaging immediately (without waiting for the timer) and `IsWarning` is set false.
- [ ] **AC4 (immediate-engage toggle):** Given a monster with `_engageImmediately = true`, when the player enters detection range, then the entity transitions straight to Engaging with no Warning state and `SetWarning` is never called true.
- [ ] **AC5 (warning cancel):** Given the entity is in Warning, when the player moves beyond `DetectionRange`, then the entity sets `IsWarning = false`, logs "standing down", and returns to its prior Idle or Patrol state (whichever it came from), with the warning timer discarded.
- [ ] **AC6 (death during warning):** Given the entity is in Warning, when it takes lethal damage, then `IsWarning` is cleared (via `TransitionToDead`) before the death animation plays — no stuck warning pose.
- [ ] **AC7 (SO validation):** Given the `Entity` inspector, when a designer sets `_warningRange ≥ _detectionRange`, then `OnValidate` clamps `_warningRange` below `_detectionRange` and logs a warning.
- [ ] **AC8 (humanoid graceful stub):** Given a humanoid AI entity (`HumanoidAIAnimationDriver`) with `_engageImmediately = false`, when it enters Warning, then the detection/timer/facing logic runs normally and `SetWarning(true)` warn-logs "not implemented yet" exactly once with no NullReferenceException; `SetWarning(false)` on exit logs nothing.
- [ ] **AC9 (facing is upright):** Given the entity is warning a player standing above/below it, when it rotates to face, then it rotates only around the Y axis (no pitch/roll tilt).
- [ ] **AC10 (monster animation plays after wiring):** Given `EntityBase.controller` has the `IsWarning` bool param and Warning state, when the entity enters Warning, then the warning clip plays and exits cleanly when `IsWarning` returns to false.

## Additional Context

### Dependencies

- **No new packages.** Uses existing `UnityEngine.AI.NavMeshAgent`, `Animator`, `GameLog`.
- **Task 7 (animator wiring)** is a prerequisite for the *visual* telegraph but NOT for the *logic* — Tasks 1–6 are fully functional and testable on their own (entity stops, faces, escalates).
- **Warning clip asset:** an actual warning animation clip is an art/design deliverable; the wiring can be validated with a placeholder.

### Testing Strategy

- **No automated game-code test infrastructure exists** in the project (only Unity package-cache tests). Primary verification is manual play-mode.
- **Manual play-mode steps:**
  1. On a monster prefab/variant, set `WarningRange = 5`, `DetectionRange = 8`, `WarningEngageTime = 3`, `_engageImmediately = false`. Enter Play.
  2. Walk the player to ~7m: confirm the entity stops, turns to face you, logs "warning", and (after Task 7) plays the warning clip. → AC1
  3. Stand still 3s: confirm it engages. → AC2
  4. Repeat, but step inside 5m before the timer: confirm immediate engage. → AC3
  5. Repeat, but run back beyond 8m during warning: confirm it stands down and resumes patrol/idle. → AC5
  6. Repeat, kill it mid-warning: confirm death plays with no lingering warning pose. → AC6
  7. Toggle `_engageImmediately = true`: confirm instant engage, no warning. → AC4
  8. Repeat steps 1–2 on a humanoid AI entity: confirm logic runs and console shows a single "not implemented yet" warn, no NRE. → AC8
- **Optional EditMode test** (only if test infra is later added): assert `Entity.OnValidate` clamps `_warningRange` below `_detectionRange` — pure SO-data validation, an approved EditMode target per `project-context.md`.

### Notes

**Pre-mortem / risk register:**

- **Stuck warning pose on death** — the highest-risk bug. Mitigated by clearing `SetWarning(false)` in *all three* exit transitions (`TransitionToEngaging`, `CancelWarning`, `TransitionToDead`), not just disengage.
- **Param-name mismatch** — the bridge hashes the string `"IsWarning"`; the controller param must match exactly (case-sensitive). A typo yields a silent no-op (no telegraph, no error). Double-check during Task 7.
- **Empty warning band misconfig** — if `WarningRange ≥ DetectionRange` at runtime, the band is empty and the entity always engages immediately (safe degradation). `OnValidate` prevents this in the editor.
- **Facing tilt** — using a full `LookRotation` without zeroing `dir.y` would pitch the model when the player is on a slope/stairs. `FacePlayer()` zeros `dir.y`.
- **Log spam** — entities that repeatedly enter/exit warning could flood `Info` logs; acceptable since `Info`/`Warn` are stripped in Release builds.

**Future considerations (out of scope, noted for continuity):**

- Sound/VO cue on warning entry (the user explicitly flagged "more will come later like sounds").
- Real humanoid warning animation (replace the Task 4 stub when the humanoid AI combat epic lands).
- Optional HUD "you've been spotted" indicator tied to the same Warning transition.
- Per-state warning re-trigger (currently combat re-entry never re-warns — revisit if a "lost then re-spotted" telegraph is desired).

## Review Notes

- **Adversarial review completed.** Findings: 12 total, 4 fixed, 8 acknowledged (by-design / future / noise).
- **Resolution approach:** auto-fix real findings.
- **Fixed:**
  - **F3 (death-pose robustness):** `EntityHealth.Die()` now calls `_animationDriver?.SetWarning(false)` directly, guaranteeing the warning bool clears at the moment of death regardless of `EntityBrain.Update()` ordering (belt-and-suspenders with `TransitionToDead`).
  - **F7 (runtime misconfig):** `EntityBrain.Start()` warns once if `WarningRange >= DetectionRange` (empty band → silent instant-engage) — `OnValidate` only runs in-editor, so un-migrated SOs were degrading silently.
  - **F10 (log spam):** `HumanoidAIAnimationDriver.SetWarning` warn-logs "not implemented yet" once per instance instead of on every warning entry.
  - **F11 (test coverage):** added `Assets/Tests/EditMode/EntityWarningStateTests.cs` (15 tests) covering the `OnValidate` clamp (AC7) and the warning decision logic (AC1–AC5) via the project's pure-formula simulation pattern. All pass.
- **Acknowledged, not changed:**
  - **F1 (default behavior):** `_engageImmediately = false` is the spec's intended default — warning is the new default detection behavior. **Migration note:** existing enemies that should keep instant-engage must have `_engageImmediately = true` set on their `EntityBrain`.
  - **F2 (placeholder motion):** `Warning` state uses the `IdleBreathe` placeholder; a dedicated warning clip is a pending art deliverable (already noted in Task 7).
  - **F4 (no hysteresis):** boundary flapping at exactly `DetectionRange`; adding a deadband would contradict AC5 and needs a new tunable — deferred.
  - **F5 (damage during warning doesn't escalate), F6 (rotation vs agent), F8 (squared-distance micro-opt — would break the codebase's consistent `Vector3.Distance` usage), F9, F12:** undecided/noise, left as-is.
- **Tests:** full EditMode suite = 250 tests, 248 pass. The 15 new warning tests pass. The 2 failures (`InventorySystemTests.ItemPickup_Configure_SetsInteractPrompt`, `WorldStateManagerFactsTests.SetWorldEvent_RaisesEvent_WithCorrectPayload`) are pre-existing and in unrelated systems (no overlap with the changed files).
- **Spec correction:** the frontmatter `test_patterns` claim "no game-code tests currently exist" is inaccurate — the project has an active 27-file EditMode suite (`Assets/Tests/EditMode/`), including `EnemyBrainStateTests`.
