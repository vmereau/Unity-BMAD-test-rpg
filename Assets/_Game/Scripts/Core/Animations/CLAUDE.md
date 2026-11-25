# CLAUDE.md — Assets/_Game/Scripts/Core/Animations

> Loaded when Claude accesses files in this folder. Covers the AI animation polymorphism
> hierarchy and the Brain → Driver → Bridge contract. Animator-controller authoring (SMBs, combo
> transitions, layers): `Art/Characters/Humanoids/Controllers/CLAUDE.md` and
> `.claude/rules/attack-pipeline.md`.

---

## Architecture

```
EntityBrain ─┐                                       ┌─ MonsterAnimationBridge   (pure parameter writes)
             ├─→ AIAnimationDriver ─┬─ MonsterAnimationDriver  ───→ ┤
EntityHealth ┘                      │                              └─ Animator (monster controller)
                                    │
                                    └─ HumanoidAIAnimationDriver ─→ HumanoidAnimationBridge (pure parameter writes)
                                                                                   │
                                                                                   └─→ Animator (Humanoid_Template)
```

`AIAnimationDriver` is the polymorphic seam. `EntityBrain` and `EntityHealth` serialize a
`[SerializeField] AIAnimationDriver _animationDriver` — Unity does not serialize C# interface
fields, so the seam is an abstract MonoBehaviour.

**The Player does NOT flow through this hierarchy.** Player uses
`PlayerAnimationDriver → HumanoidAnimationBridge` directly because the Player is not AI-driven.
Do not put the Player on `AIAnimationDriver`.

---

## Bridge vs Driver

| Layer | Responsibility |
|-------|----------------|
| **Bridge** (`MonsterAnimationBridge`, `HumanoidAnimationBridge`) | Pure animator-parameter wrapper. One method per animator parameter. No lifecycle, no smoothing, no SO logic. |
| **Driver** (`MonsterAnimationDriver`, `HumanoidAIAnimationDriver`, `PlayerAnimationDriver`) | Owns lifecycle (ragdoll, AnimatorOverride application, death-component-disable), smoothing, velocity → animator-parameter math. Calls into the bridge. |

Symmetric on both sides: bridge + driver.

---

## Rules

- **AI code never references concrete bridge or driver types.** `EntityBrain`, `EntityHealth`,
  `SMB_DeathState` must reference `AIAnimationDriver` only. Concrete bindings happen in the
  prefab inspector.
- **One Animator per entity, owned by its matching bridge.** The monster bridge owns the
  monster controller's Animator; the humanoid bridge owns `Humanoid_Template`'s Animator.
  Never write to an Animator from outside its bridge.
- **`MonsterAnimationDriver` script GUID is `bc1ff05bbb035a34cb7a7f54f833aa88`** — monster
  prefabs reference it. Do not regenerate the `.meta` or every monster prefab loses its driver reference;
  a rename moves file + `.meta` in lockstep.
- **`HumanoidAIAnimationDriver._runSpeed` must match the variant's `Entity.EngageSpeed`** —
  the 2D blend tree normalizes against `_runSpeed`. If a humanoid type uses
  `EngageSpeed = 6` but the driver's `_runSpeed` stays `4`, the blend tree saturates at 1.0
  before the agent reaches full speed.
- **Combo seam:** `AIAnimationDriver.MaxComboSteps` (default 1) + `TriggerComboStep(int step)`
  (steps 2..Max; step 1 is `TriggerAttack`). `MonsterAnimationDriver` inherits 1 / no-op.
  `HumanoidAIAnimationDriver`: `MaxComboSteps = 3`; `TriggerAttack` calls
  `HumanoidAnimationBridge.ResetAttackTriggers()` then sets `Attack_1` (a stale `Attack_2/3` would
  otherwise auto-chain the next attack); `TriggerComboStep(2|3)` sets `Attack_2|Attack_3`.
  `CancelAttack()` (called by `EntityMeleeAttacker.EndAttack`) resets the attack triggers — an
  `Attack_1` set before the Base Layer took it (disengage during the `IsInCombat`
  blend) would otherwise fire as a damage-less punch on the next combat entry. Monster: no-op.
- **`SetWarning(bool)` is a held bool, not a trigger** (the telegraph holds for the warning timer):
  `MonsterAnimationDriver.SetWarning` → `MonsterAnimationBridge` `IsWarning` bool; `HumanoidAIAnimationDriver.SetWarning`
  is an empty no-op — the humanoid warning pose comes from `SetInCombat(true)`.
- **Stance seam:** `TriggerStagger(int variant)` / `TriggerKnockDown()` default to `TriggerGetHit()` (monsters: GetHit
  fallback); `ClearStanceReaction()` default no-op. Humanoid → `HumanoidAnimationBridge.PlayStagger` / `PlayKnockDown` /
  `ResetStanceTriggers`. Like `ResetAttackTriggers`, these bridge methods write several parameters on purpose: one hit
  sets `GetHit` then `Stagger` in the same frame, and the pending `GetHit` must be cleared in the same write or it cuts
  the stagger clip.
- **UpperBody layer weight: drivers step it, the bridge reads tags.** `HumanoidAnimationBridge` exposes
  `IsBaseFullBody` / `IsUpperBodyAction` (destination-state tags, `Controllers/CLAUDE.md`) and `SetUpperBodyWeight`;
  `PlayerAnimationDriver` and `HumanoidAIAnimationDriver` each call `UpperBodyWeight.Target` + `Step` in `Update` with
  their own `IsInCombat` (NPC: cached in `SetInCombat`). Same rule for Player and NPCs — never write the weight elsewhere.
  Same `Update` steps the `CombatLocomotion` float (`SetCombatLocomotion`, Base free / combat locomotion). The archetype's
  `fullBodyCombatLocomotion` reaches the drivers through `HumanoidAnimationBridge.FullBodyCombatLocomotion`, written every
  frame by `HandPoseDriver` (the one component that reads `EquipmentVisuals`); `IsFullBodyCombatLocomotion` adds "Base heads
  into `LockOn Locomotion`" (state name, not a tag). Without a `HandPoseDriver` the flag stays false (carry pose as before).
  `HumanoidAnimationBridge.TriggerGetHit` is skipped while `IsBaseFullBody` (GetHit has no clip and must not latch).
- **Hand poses: one driver for the Player and NPCs** (spec-hand-pose-layer). `HandPoseDriver` (on `Player` and `NPC_base
  Variant`, beside the bridge and `EquipmentVisuals`) runs the pure `HandPoseRule` every frame and writes the result with
  `HumanoidAnimationBridge.SetHandPoses` (ints, written on change). Inputs: weapon in hand = `EquipmentVisuals.IsDrawn &&
  ActiveWeaponGO != null`, the archetype's `drawnHand` / `twoHandedGrip`, `ShieldInHand`, and "fighting" = the Animator's
  `IsInCombat` (`bridge.IsInCombat`), not `IsDrawn`: NPC brawls fight with the weapon sheathed. Empty hands stay relaxed
  while `bridge.IsSwitchingWeapon` (UpperBody `Draw_State` / `Sheathe_State`: `IsInCombat` flips at the press, the weapon
  arrives at WeaponSwap) and `bridge.IsBaseDown` (KnockedDown / Unconscious / Death / Dead / GettingUp). `HandPose` values are the
  Animator ints — append new poses, never renumber (Relaxed = 0 = the default after a controller swap).
  **Off-hand arm (spec-sword-shield-take-combo):** the same driver reads `EquipmentVisuals.OffhandArmPose` (the Offhand
  item's `offhandArmPose`) and steps the `LeftArm` layer weight with the pure `OffhandArmRule` (0.15 s): 1 for a pose in
  idle / locomotion, 0 while `IsBaseFullBody` (attacks + `FullBody` states), `IsUpperBodyAction` (block, draw / sheathe,
  drink, bow aim) or down — every action plays the clip's own left arm (a fixed torch pose came within 2–6 cm of the sword). `TorchHold` also forces the left fingers to `Fist`, attacks
  included. Bridge: `SetOffhandArmWeight` (layer looked up by name, cached). NPC off-hand items have no pose today.
  **Bow carry seat (spec-bow-idle-compensated-bake):** for a left-hand, rigid archetype with `hasCarryPose`
  (`EquipmentVisuals.UsesCarrySeat`), `HandPoseRule.CarrySeat` is true only in the combat idle: bow in hand, UpperBody heading
  into `CombatIdle` (`bridge.IsUpperBodyCombatIdle`) and the Base Layer not full-body (`IsBaseFullBody`). It picks
  `BowCarry` fingers, and the driver eases a weight 0 → 1 in 0.15 s (scaled time) into `EquipmentVisuals.SetCarryWeight`
  (Drawn child: `drawnPose` → `carryPose`). Aim, draw / sheathe, drink, dodge, stagger, knockdown and death keep `drawnPose`.
  A new weapon visual, or a weapon appearing in the hand without a draw clip (NPCs), snaps to the target;
  `RefreshWeapon` keeps the weight when it rebuilds the same family. Same component on the Player and NPCs.
- **NPC Animators never apply root motion.** `HumanoidAIAnimationDriver.Awake` forces `applyRootMotion = false` (warns,
  before any early return; it uses `HumanoidAnimationBridge.Animator` — drivers do lifecycle work on the bridge's Animator,
  only animator *parameters* are bridge-only):
  attacks play full-body on the Base Layer, so root motion would move the `Character` child away from the agent root
  (lock-on, colliders and hitbox owners stay at the root). NPC swings still step forward: `EntityAttackStep`
  (`Scripts/AI`, on `NPC_base Variant`; monsters have none) moves the root via `NavMeshAgent.Move` — see
  `.claude/rules/attack-pipeline.md`.
- **NavMeshAgent humanoid AI is always grounded** — `HumanoidAIAnimationDriver.DriveLocomotion`
  hard-codes `IsGrounded = true`, `IsRising = false`. Revisit if AI ever leaves the navmesh.

---

## Code Review Checklist — Animations

| Severity | Pattern |
|----------|---------|
| HIGH | `EntityBrain`, `EntityHealth`, or `SMB_DeathState` referencing a concrete driver/bridge type instead of `AIAnimationDriver` |
| HIGH | `Animator.Set*` call from outside the matching bridge component |
| HIGH | Player code routed through `AIAnimationDriver` — Player is not AI |
| HIGH | New monster base controller whose attack state lacks `SMB_EntityAttackState` — an interrupted attack leaves the hit window open (phantom bites) |
| MEDIUM | `HumanoidAIAnimationDriver._runSpeed` left at default `4f` on a variant whose `Entity.EngageSpeed > 4` |
| MEDIUM | New monster `Trigger*` parameter added to `MonsterAnimationBridge` but not exposed via `AIAnimationDriver` virtual method — humanoid driver can't no-op-stub it |
| MEDIUM | `IsWarning` animator param missing on a monster controller using warning detection — `SetBool` no-ops silently, so the warning telegraph never plays (no error, no clip). Verify the param name matches exactly (case-sensitive). |
