---
title: 'Execute stomp — kill an unconscious NPC'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: 'f3730ee'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/.claude/rules/attack-pipeline.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A brawl loser lies unconscious and then always gets up: the player can never finish off a downed humanoid.

**Approach:** In combat (any drawn weapon, bow included), standing at an unconscious NPC, **hold F** to play the `Execute Stomp` clip: the player aligns on the body, stomps, and the NPC dies when the foot lands (animation event, frame 22 of 56). Releasing F before the impact cancels — the NPC stays unconscious. The HUD prompt card shows an `[F] Execute` row while a target is available.

## Boundaries & Constraints

**Always:** Only unconscious NPCs (`EntityHealth.IsUnconscious`, not getting up, not dead) can be executed. Per-NPC opt-out `NPCEntity.CanBeExecuted` (default true, nobody opted out yet). An execution is a normal death: `Die()` path, `RegisterDeath` → kill XP, `KilledFact`, lootable corpse. The kill comes only from the clip event, never a timer. While executing the player can't move, attack, block, aim, dodge, jump, switch weapon or interact. Animator writes only through bridges / drivers. `Game.Combat` stays AI-free (interface seam). Tunables in `CombatConfigSO`.

**Ask First:** Changing `Die()` behaviour for non-execute deaths; any change to who gets kill XP.

**Never:** Executing conscious, stance-knocked-down, hostile-standing or monster entities; NPCs executing anyone; crime / witness / reputation reactions; sound or VFX; lock-on targeting; saving a mid-execute state.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Execute | Weapon drawn, unconscious NPC in range + cone, hold F to impact | Player aligns, stomps; NPC dies at impact (ragdoll), kill XP; clip finishes | N/A |
| Cancel | F released before impact | Clip blends out, NPC still unconscious, player free | N/A |
| Release after impact | F released after the kill | Nothing (clip finishes) | N/A |
| Not in combat | Weapon sheathed | F does nothing, no `[F]` row | N/A |
| Target wakes | NPC starts getting up before impact | Execute cancelled | N/A |
| Player hit / incapacitated / dead / dialogue | During the stomp, before impact | Execute cancelled | N/A |
| Protected NPC | `CanBeExecuted` false | Not a target, no `[F]` row | N/A |
| Lost events | Animator never delivers the end event | Watchdog ends the execute after clip length + 0.5 s | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Scripts/AI/EntityHealth.cs` -- `IsUnconscious`, `IsGettingUp`, private `Die()` (death clip; ragdoll via `AIAnimationDriver.EnableRagdoll`)
- `ScriptableObjects/Entities/NPC/NPCEntity.cs` -- per-NPC data (`XpOnKnockout`)
- `Scripts/Player/PlayerStateManager.cs` -- `IsBusy`, `Can*` gates, pure `Evaluate*` helpers
- `Scripts/Player/PlayerAnimationDriver.cs` → `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- player Animator writes
- `Scripts/Combat/AnimationEventReceiver.cs` -- clip events on the Player root
- `Scripts/Combat/PlayerCombat.cs` -- input subscription pattern (`_input.Player.*`)
- `InputSystem_Actions.inputactions` + `InputSystem_Actions.cs` -- dual-file contract (`Assets/_Game/CLAUDE.md`); F is free
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- Base Layer full-body states, `FullBody` tag
- `Scripts/UI/HUD/InteractionPromptUI.cs`, `Prefabs/UI/InteractionPrompt.prefab` -- card: `KeyBadge` + `Texts`
- Clip (measured): 56 frames @ 30 fps = 1.867 s, Humanoid; right foot lands at frame 22 (normalized 0.393), rests until 28; landing point ≈ (−0.45, 0, 0.88) m in player-local space

## Tasks & Acceptance

**Execution:**
- [x] `Art/.../Animations/Execute Stomp.fbx` -- move to `Animations/Combat/execute stomp.fbx` (+ .meta); clip `Execute Stomp`, root rotation / Y / XZ baked, events `ExecuteImpact` @ 0.393 and `ExecuteEnd` @ 0.95 (via `ModelImporter.clipAnimations`)
- [x] `Scripts/Combat/IExecutable.cs` (new) -- `CanBeExecuted`, `Vector3 ExecutePoint`, `bool Execute(GameObject executor)`
- [x] `NPCEntity.cs` -- `[SerializeField] bool _canBeExecuted = true` + getter
- [x] `EntityHealth.cs` -- implement `IExecutable`: point = chest bone (fallback hips / root); `Execute` re-checks, then `Die(ragdollNow: true)` (ragdoll instead of the death clip; normal deaths unchanged)
- [x] `Scripts/Combat/ExecuteMath.cs` (new, pure) -- `IsCandidate(range, cone)`, `AlignStance(playerPos, targetPoint, footOffset) → (pos, yaw)` so the landing point hits the target
- [x] `CombatConfigSO.cs` -- `[Header("Execute")]` range 1.8, cone half-angle 70, foot offset, align duration 0.2, clip duration 1.867 (changed in implementation: the 0.2 s cancel blend is the controller transition's duration, not a config value)
- [x] `Scripts/Combat/PlayerExecution.cs` (new, Player root) -- target scan (CharacterHitbox overlap → `IExecutable`, nearest in cone), `CurrentTarget`; F started → gate → align (CharacterController) → `SetExecuting(true)`; F canceled / invalid target / player can't continue before impact → cancel; `OnExecuteImpact` → `target.Execute`; `OnExecuteEnd` / watchdog → finish
- [x] `PlayerStateManager.cs` -- `IsExecuting` (in `IsBusy`), `SetExecuting(bool)`, `EvaluateCanExecute(...)` (in combat, not busy / airborne / swimming / attacking / blocking / aiming / dodging / switching)
- [x] `PlayerAnimationDriver.cs`, `HumanoidAnimationBridge.cs` -- `SetExecuting(bool)` → `IsExecuting` bool
- [x] `AnimationEventReceiver.cs` -- `ExecuteImpact()`, `ExecuteEnd()`
- [x] `Humanoid_Template.controller` (AnimatorController API via MCP) -- `IsExecuting` bool; AnyState → `Execute` (after Death / stance / Unconscious, before `Attack_1`, no self-transition, 0.15 s); `Execute → LockOn Locomotion` on `IsExecuting == false` (no exit time, cancel blend); tag `FullBody`
- [x] Input -- `Player/Execute` button on `<Keyboard>/f` in both input files
- [x] `InteractionPromptUI.cs` + `InteractionPrompt.prefab` -- `ExecuteRow` (key `F`, verb `Execute`); polls `_playerExecution.CurrentTarget`; shown when a target exists and the focus is none or the same entity; anchored on the focus, else on the target; wire on `Player.prefab` / `UICanvas`
- [x] `Assets/Tests/EditMode/ExecuteTests.cs` -- `ExecuteMath` (candidate cone/range, alignment hits the point), `EvaluateCanExecute` truth table
- [x] `DodgeController.cs`, `InteractionSystem.cs`, `ActionBarSystem.TryUseSlot`, `UIScreenManager.OpenTab`, `PlayerController` lock-on rotation -- also refuse while `IsExecuting` (added in implementation: they gate on `IsIncapacitated`, not `IsBusy`)
- [x] Docs -- `Scripts/Combat/CLAUDE.md`, `Scripts/Player/CLAUDE.md`, `Controllers/CLAUDE.md`, `Combat/CLAUDE.md` (clip timings), `Scripts/UI/HUD/CLAUDE.md`, `Data/NPCs/CLAUDE.md`

**Acceptance Criteria:**
- Given a knocked-out villager and a drawn sword, when the player looks at it, then the card reads `[E] Loot` and `[F] Execute`.
- Given the player holds F, when the foot lands, then the villager ragdolls dead, a kill XP toast shows, and the corpse is lootable.
- Given the player holds F, when F is released at the lift-off, then the villager still lies unconscious and later gets up.

## Design Notes

Alignment keeps the approach direction: yaw = bearing(player → target) − bearing(footOffset), pos = target − R(yaw)·footOffset, moved over 0.2 s with the CharacterController (walls respected). If the foot misses visually in play, retune `footOffset`, not the math. The `[F]` row polls `PlayerExecution` like `AimSpreadUI` polls `PlayerCombat` (continuous state, no new event type).

**Review patches (2026-11-16).** Focus wins: a focused executable body is the target, a focus on anything else = no target, so the `[F]` row and F always agree (two bodies side by side). Cancel also on any damage (`PlayerHealth.DamageRevision`), while loading, airborne or swimming, re-checked inside the impact event. Access check skips the body's own ragdoll colliders (solid Default bones — a plain linecast rejected every body); the CharacterController ignores the body's colliders during the stomp so the slide reaches the stance. A foot miss at impact only warns (the kill stays event-driven). The execute death keeps the held pose (no get-up push) before the ragdoll.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green incl. `ExecuteTests`
- `read_console` after compile -- expected: no errors

**Manual checks:**
- Play mode in StartingTown: knock out a villager, execute it, cancel an execute, try sheathed (nothing), check the `[F]` row placement

## Suggested Review Order

**Execute flow (input → kill)**

- Entry point: F re-scans, computes the stance, enters the stomp.
  [`PlayerExecution.cs:136`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L136)

- The kill happens only here, from the clip event, after re-checking every cancel rule.
  [`PlayerExecution.cs:166`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L166)

- One list of cancel reasons: interrupted, any damage, loading, target waking.
  [`PlayerExecution.cs:199`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L199)

- Event receiver routes the clip's ExecuteImpact / ExecuteEnd.
  [`AnimationEventReceiver.cs:57`](../../Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs#L57)

**Target selection and alignment**

- Focus decides, else nearest body in range / cone / access.
  [`PlayerExecution.cs:233`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L233)

- Line of sight ignores the body's own solid ragdoll bones.
  [`PlayerExecution.cs:289`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L289)

- Pure math: stance that lands the measured foot on the chest.
  [`ExecuteMath.cs:31`](../../Assets/_Game/Scripts/Combat/ExecuteMath.cs#L31)

- Slide through the body's limbs, restored when the stomp ends.
  [`PlayerExecution.cs:310`](../../Assets/_Game/Scripts/Combat/PlayerExecution.cs#L310)

**Victim side (AI, via the IExecutable seam)**

- Executable = lying, not getting up, NPC opt-out flag.
  [`EntityHealth.cs:67`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L67)

- Normal death (kill XP, fact, loot) but straight to ragdoll.
  [`EntityHealth.cs:247`](../../Assets/_Game/Scripts/AI/EntityHealth.cs#L247)

**Player state gates**

- IsExecuting joins IsBusy; CanContinueExecute keeps a running stomp alive.
  [`PlayerStateManager.cs:69`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L69)

- Start gate (pure, tested).
  [`PlayerStateManager.cs:436`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L436)

- Systems gating on IsIncapacitated also refuse while executing.
  [`DodgeController.cs:126`](../../Assets/_Game/Scripts/Combat/DodgeController.cs#L126)

- Damage counter: a same-frame heal can't hide a hit.
  [`PlayerHealth.cs:153`](../../Assets/_Game/Scripts/Player/PlayerHealth.cs#L153)

- Animator bool re-pushed every frame (controller swaps reset parameters).
  [`PlayerAnimationDriver.cs:78`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L78)

**HUD prompt**

- `[F] Execute` row polls the target; anchors on the chest without focus.
  [`InteractionPromptUI.cs:165`](../../Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs#L165)

**Peripherals**

- Execute tunables (range, cone, foot offset, timings, access).
  [`CombatConfigSO.cs:109`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L109)

- Per-NPC protection flag.
  [`NPCEntity.cs:57`](../../Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs#L57)

- Cone / range / alignment / gate tests.
  [`ExecuteTests.cs:7`](../../Assets/Tests/EditMode/ExecuteTests.cs#L7)

