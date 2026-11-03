---
title: 'Full-body attacks with attack steps and wind-up steering'
type: 'refactor'
created: '2026-11-03'
baseline_commit: 'dc11194edcb19c2239c5bf8961b289445bd36178'
status: 'done'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Humanoid_Template` plays attacks on an upper-body-masked `Attack` layer at constant weight 1 (with twin copies of every locomotion/sneak/jump/dodge state), so hip-driven swings break; the 2H fix (synced `FullBody` layer + `ClipEventDeduper` + `IsFullBodyAttack`) roots the player in place, which feels bad.

**Approach:** Every melee attack (all weapon families, Player and NPCs) plays full-body on the Base Layer; the masked layer becomes `UpperBody`, weight driven 0↔1 and used only for combat-idle carry pose, block, bow aim, draw/sheathe. The player moves during attacks through a scripted forward step per attack (the Mixamo clips are in place) and can steer from attack start until `HitboxEnable` (commit point).

## Boundaries & Constraints

**Always:** Animator writes only through `HumanoidAnimationBridge`. Keep the clip-event + SMB pipeline, `PlayerAttackTracker`, combo rules (`interruptionSource = None` on combo transitions) and the existing instant cancels (block / jump / fall / dodge cancel a swing at any moment). Change clip import settings only via `ModelImporter.clipAnimations`. Edit the controller with the `AnimatorController` API (MCP `execute_code`), not raw YAML. Stagger / knockdown / death interrupt attacks on the Base Layer for Player and NPCs.

**Ask First:** Any change to NPC movement (NavMeshAgent) beyond animator weights.

**Never:** No Animator root motion (`applyRootMotion` stays off, clips stay baked); no NPC attack step. No new clip events. No Animation Rigging / grip IK work (`TwoHandGripConstraint` stays). No new animation assets. No change to bow aim logic beyond its layer name.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 1H / 2H / unarmed swing | Attack press in combat, move input held | Full-body clip; player pushed forward along its facing by the step's distance during the step window; rotates toward camera-relative input until `HitboxEnable`, then locked | Gravity still applied |
| Step distance 0 | Archetype / config step distance 0 | Swing stays rooted (off switch) | N/A |
| Lock-on swing | Locked target | Faces target as today; attack step still applied | N/A |
| Swing cancelled | Block / dodge / jump mid-swing | Base leaves the attack state; attack step stops the same frame `IsAttacking` clears | N/A |
| Stagger / knockdown / death mid-swing | Reaction trigger | Base reaction state wins; UpperBody weight 0 | Existing SMB exit cleanup |
| Walk in combat, no swing | IsInCombat | UpperBody weight 1 (combat idle carry) over Base locomotion | N/A |
| Out of combat / dodge / airborne / incapacitated | — | UpperBody weight 0, full Base pose | N/A |
| NPC swing | Brain attacks | Full-body on Base, NPC stays in place (agent stopped, `FaceTarget` unchanged) | N/A |

</frozen-after-approval>

## Code Map

- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- layers to restructure (Attack → UpperBody, delete FullBody)
- `Art/Characters/Humanoids/Animations/Combat/Unarmed/Uppercut Jab.fbx` -- stray `loopTime: 1`
- `ScriptableObjects/Config/CombatConfigSO.cs` -- default + unarmed attack steps
- `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- `SetFullBodyWeight` → `SetUpperBodyWeight`
- `Scripts/Core/Animations/HumanoidAIAnimationDriver.cs` -- NPC weight drive
- `Scripts/Player/PlayerAnimationDriver.cs` -- `StepFullBodyWeight` → upper-body weight step
- `Scripts/Player/PlayerStateManager.cs` -- `IsFullBodyAttack`, `EvaluateCanMove`, new steer flag
- `Scripts/Player/PlayerController.cs:119-187` -- input zeroing, rotation branches, `Move`
- `Scripts/Combat/PlayerCombat.cs:397-401, OnHitboxEnable, OnAttackStateEntered` -- full-body flag, commit point
- `Scripts/Combat/ClipEventDeduper.cs` + both event receivers -- delete dedupe
- `ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs`, `WeaponSO.cs`, `Archetype_TwoHanded.asset`, `Config/PlayerConfigSO.cs` -- full-body fields
- `Tests/EditMode/` -- `HumanoidTemplateControllerTests`, `ClipEventDeduperTests`, `PlayerAnimationDriverTests`, `PlayerStateManagerTests`, `WeaponArchetypeTests`

## Tasks & Acceptance

**Execution:**
- [x] `Humanoid_Template.controller` -- Base Layer: add `Attack_1/2/3_State` (both SMBs, same clips/triggers/combo transitions, exits to `LockOn Locomotion`, plus `IsBlocking` exits); remove `FullBody` layer; rename `Attack` → `UpperBody`, default weight 0, delete its attack, jump/fall/dodge, stagger/knockdown twins and out-of-combat locomotion/sneak twins; keep CombatIdle, Block, Draw/Sheathe, Bow states. Override controllers keep resolving (same clip keys) -- single attack location
- [x] `Uppercut Jab.fbx` importer -- loopTime off -- stray loop
- [x] `AttackStep` (serializable: `distance` m, `startTime` s, `duration` s) -- `WeaponArchetypeSO.attackSteps[]` (index = Attack_N − 1, missing → `CombatConfigSO.defaultAttackStep`), `CombatConfigSO.unarmedAttackSteps[]`; pure `AttackStepMath.Displacement(step, t0, t1)` -- tunable lunge per family
- [x] `Scripts/Player/PlayerAttackStep.cs` (new, Player root, add to `Player.prefab`) -- on attack-state enter (from `PlayerCombat`) start the step's timer; each frame while `IsAttacking`, `CharacterController.Move(forward × displacement)`; stop on attack end / cancel -- movement during swings
- [x] `PlayerStateManager` / `PlayerController` -- `CanMove` false during any attack; steer toward camera-relative input while attacking and not committed (`attackSteerSpeed` in `PlayerConfigSO`); `PlayerCombat` sets committed on `HitboxEnable`, clears on attack-state enter and attack end -- wind-up steering
- [x] Bridge + both drivers -- pure `UpperBodyWeight.Evaluate(...)` (Core/Animations) blended over `upperBodyBlendTime`; player: block/aim/switch → 1, attack/dodge/airborne/incapacitated/dead → 0, else IsInCombat; NPC: same with its known flags -- weight drive
- [x] Remove `FullBody` code paths: `IsFullBodyAttack`, `fullBodyAttacks`, `ResolvedFullBodyAttacks`, `fullBodyAttackBlendTime`, `ClipEventDeduper` + tests -- dead workaround
- [x] `Tests/EditMode/` -- update controller tests (attacks on Base, UpperBody masked, weight 0, no FullBody); tests for `UpperBodyWeight.Evaluate`, steer evaluation, `EvaluateCanMove`
- [x] Docs -- `Controllers/CLAUDE.md`, `Animations/Combat/CLAUDE.md`, `Core/Animations/CLAUDE.md`, `Player/CLAUDE.md`, `Scripts/Combat/PLAYER_COMBO.md`, `.claude/rules/attack-pipeline.md`; `gdd.md:304` → instant cancels (block/dodge/jump) are allowed, pending playtest; prune resolved `deferred-work.md` items

**Acceptance Criteria:**
- Given any equipped weapon family, when the player attacks, then hips/legs follow the clip and the blade matches the clip (no 2H 30–112° offset).
- Given the controller, when inspected, then there is no `FullBody` layer and no locomotion/sneak/jump/dodge twin on `UpperBody`.
- Given an NPC mid-attack, when staggered or killed, then the reaction plays immediately and the NPC does not drift from its NavMeshAgent position.
- Given EditMode tests, when run, then all pass.

## Spec Change Log

- 2026-11-03, Ask-First trigger (pre-implementation): every Mixamo attack clip has net root XZ travel 0 (max body offset 0.06–0.63 m, returns to start), so clip root motion gives no movement. Human chose option A: scripted per-attack forward step (`AttackStep`) instead of `OnAnimatorMove` root motion; clips stay baked. KEEP: steering until `HitboxEnable`, full-body Base attacks.
- 2026-11-03, review (patches, no loopback): (1) the frozen "interruptionSource = None on combo transitions" rule froze the whole body once attacks moved to Base — measured with a manual `Animator.Update` loop, a dodge waited ~0.4 s through the combo blend. All Base attack transitions + AnyState → Attack_1 now use `Destination`: the dodge interrupts at once and the combo still completes (the original intent — the source's exit can't cut the combo — is kept). (2) Death / stagger / knockdown clear latched attack triggers (Base AnyState → Attack_1 could leave them). (3) `TriggerGetHit` only while Base is in LockOn Locomotion. (4) `Landing` untagged (block / draw on landing shows). (5) A stance reaction mid draw / sheathe completes the swap. (6) `PlayerAttackStep` runs before `PlayerController` (its gravity Move stays last → isGrounded), stops on busy / airborne / disabled controller, and stops at a ledge (ground probe). (7) Orphan `FullBody` state machine removed. Naming: built as `AttackStep.DisplacementBetween` / `Resolve` and `UpperBodyWeight.Target` + `Step`; the player weight reads Animator state tags (shared with NPCs) instead of PlayerStateManager flags, full-body states win over upper actions (dodge mid-draw must hide the draw).

## Design Notes

`GetHit` has no clip: an AnyState → `GetHit` on Base would now cut attacks into a motionless state. Keep plain hits non-interrupting: route `GetHit` from locomotion states only (or exclude attack states), matching today's behavior.

Steering commits on the existing `HitboxEnable` event so no clip needs re-authoring; committed = rotation locked, root motion continues.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all green
- MCP `read_console` after refresh -- expected: no compile errors / missing-motion warnings

**Manual checks (if no CLI):**
- Play mode: swing with each family while holding a direction — character steps and turns during wind-up, locks at hit; walk in combat keeps the weapon carry pose; draw/sheathe while walking still upper-body.

## Suggested Review Order

**Layer layout (the core of the change)**

- Controller layout, tags and the Destination interruption rule — read this before the code.
  [`Controllers/CLAUDE.md:15`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/CLAUDE.md#L15)

- One pure rule decides the UpperBody weight from tags + IsInCombat.
  [`UpperBodyWeight.cs:18`](../../Assets/_Game/Scripts/Core/Animations/UpperBodyWeight.cs#L18)

- Bridge reads destination-state tags; drivers own the smoothing.
  [`HumanoidAnimationBridge.cs:66`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L66)

- Player and NPC drivers step the same rule.
  [`PlayerAnimationDriver.cs:72`](../../Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs#L72)
  [`HumanoidAIAnimationDriver.cs:71`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAIAnimationDriver.cs#L71)

- Base AnyState → Attack_1: reactions and death now clear latched attack triggers; GetHit only from locomotion.
  [`HumanoidAnimationBridge.cs:177`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L177)

**Moving during a swing**

- Scripted forward step: in-place Mixamo clips, ledge probe, runs before PlayerController.
  [`PlayerAttackStep.cs:48`](../../Assets/_Game/Scripts/Player/PlayerAttackStep.cs#L48)

- Step math: constant speed over a seconds window.
  [`AttackStep.cs:33`](../../Assets/_Game/ScriptableObjects/Items/Weapons/AttackStep.cs#L33)

- Step starts on attack-state enter; archetype → unarmed list → config default.
  [`PlayerCombat.cs:863`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L863)

- HitboxEnable commits the swing (steering ends).
  [`PlayerCombat.cs:839`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L839)

- Wind-up steering from raw input while move input is zeroed.
  [`PlayerController.cs:179`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L179)

- Gates: no move during any attack; steer until committed.
  [`PlayerStateManager.cs:396`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L396)

**Peripherals**

- Tunables: default / unarmed steps, steer speed, blend time.
  [`CombatConfigSO.cs:87`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L87)
  [`PlayerConfigSO.cs:28`](../../Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs#L28)

- Controller structure tests (layers, tags, Destination interruption, motions).
  [`HumanoidTemplateControllerTests.cs:92`](../../Assets/Tests/EditMode/HumanoidTemplateControllerTests.cs#L92)
