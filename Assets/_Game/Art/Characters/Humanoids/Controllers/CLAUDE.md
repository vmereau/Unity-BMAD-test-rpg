# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Controllers

> Loaded when Claude accesses `Humanoid_Template.controller`. Covers its layers, parameters, SMB wiring
> and transition rules. Shared pipeline contract: `.claude/rules/attack-pipeline.md`.

---

## Humanoid_Template.controller

Used by **the Player** (`Player.prefab` → Animator) **and humanoid NPCs**. Weapon families swap clips via
`AnimatorOverrideController`s (`../Overrides/Humanoid_Base`, `Combat/one handed/Sword_AnimatorOverride`,
`Combat/two handed/TwoHanded_AnimatorOverride`, chosen by `WeaponSO.ResolvedAnimatorOverride`). Both melee overrides
replace the weapon block (`Unarmed Block Guard` → `Sword Braced Guard` / `2hand blocking`; the shield guard `Block Idle`
plays from the base, the two-handed override maps it to `2hand blocking` too); the two-handed one also replaces
`unarmed stagger 1/2`. The bow has no block entry (right click aims).

- **Layers** (spec-full-body-attacks-root-motion): **`Base Layer`** holds locomotion (`LockOn Locomotion` 2D blend
  tree, sneak states) **and every full-body action** — `Attack_1/2/3_State` (AnyState → `Attack_1`), jump/fall/land,
  dodge, `Stagger_1/2`, `KnockedDown → GettingUp`, `Death`, `GetHit`. **`UpperBody`** (index 1, `UpperBodyMask`, Override,
  default weight **0**) holds only `LockOn Locomotion` (default), `CombatIdle` (the weapon carry pose, IsInCombat),
  AnyState → `Block_State` / `Draw_State` / `Sheathe_State`, and the bow aim states. No twins: **a new Base state needs
  nothing on UpperBody** — tag it instead (below).
- **UpperBody weight** is not authored: `PlayerAnimationDriver` / `HumanoidAIAnimationDriver` step it every frame with
  `UpperBodyWeight` (`Scripts/Core/Animations/`) from the Animator's **state tags**, read on each layer's destination
  state (next state during a transition): Base tag `Attack` or `FullBody` → 0; else UpperBody tag `UpperBodyAction` **or** the
  driver's in-combat flag (the `IsInCombat` bool param; `CombatIdle` itself has no tag) → 1; else 0 (blend `PlayerConfigSO.upperBodyBlendTime` / the NPC driver's `_upperBodyBlendTime`, 0.15 s).
  Tags: `Attack` = the three attack states; `FullBody` = JumpRise, Falling, Dodging, Dodge back, Stagger_1/2,
  KnockedDown, GettingUp, Death, Dead, Unconscious, Sleeping, Swim Locomotion, PickUp, Execute (not Landing: a block / draw
  pressed on landing must show); `UpperBodyAction` = Block, Draw, Sheathe, Drink, BowDrawArrow/Aim/Fire. **A new
  full-body Base state must get the `FullBody` tag** or the carry pose plays over it in combat.
- **Hand layers** (spec-hand-pose-layer): **`LeftHand`** (index 2) / **`RightHand`** (index 3), Override, weight **1**,
  finger-only masks (`Animations/HandPoses/HandMask_Left/Right`). They always replace the clips' fingers: one state per
  `HandPose` (`Relaxed` default, `Fist`, `OneHandGrip`, `TwoHandGrip`, `BowGrip`, `ShieldHold`, `BowCarry`), AnyState → state on the int
  `LeftHandPose` / `RightHandPose` == pose (0.12 s, no self-transition). **Built by `Tools/Characters/Build Hand Poses`**
  (`Editor/Characters/HandPoseBuilder`, values in code): never hand-edit these layers or the clips. Driven by `HandPoseDriver`
  (`Scripts/Core/Animations/CLAUDE.md`). Pose clips carry no events. A rebuild keeps each hand layer at its index; a new layer goes after them
  (update the indices in `HumanoidTemplateControllerTests`).
- **Off-hand arm layer** (spec-sword-shield-take-combo): **`LeftArm`** (index 4), Override, default weight **0**, mask
  `Animations/OffhandPoses/LeftArmMask` (left arm only, no fingers). One state, `TorchHold` (`Torch Hold.anim`, constant
  muscles). `HandPoseDriver` steps the weight to 1 while the off-hand item's `offhandArmPose` isn't None, 0 in `Attack` /
  `FullBody` states, `UpperBodyAction` states and while down: actions play the clip's own left arm (attacks swing it clear of the blade). **Built by `Tools/Characters/Build Offhand Arm Layer`**
  (`Editor/Characters/OffhandArmLayerBuilder`, muscle values in code): never hand-edit it. A second pose = a new state +
  an int parameter.
- **Attacks are full-body.** Every attack state exits to `LockOn Locomotion` on `IsBlocking` (listed first — block-cancel),
  then the recovery cancels (`AttackCancel` bool, player-only — NPCs never set it, spec-attack-recovery-cancel):
  `→ Attack_1_State` on `AttackCancel` + `Attack_1` (0.15 s, restart; self-transition on Attack_1), `→ LockOn Locomotion`
  on `AttackCancel` (0.2 s), then the combo / exit-time transitions. AnyState → `Attack_1` also requires `AttackCancel` false
  (AnyState is evaluated first — it would otherwise shadow the 0.15 s restart from Attack_2/3). Attack clips stay root-baked (`applyRootMotion` off everywhere): the Mixamo clips
  are in place (net root travel 0), so the player's forward step is scripted (`PlayerAttackStep`, `Scripts/Combat/PLAYER_COMBO.md`). Exception: the three base
  attack clips (`lead jab step`, `hook step`, `uppercut step`) keep root XZ unbaked and the player reads their travel
  through `ActionRootMotion` (`PLAYER_COMBO.md` → Action root motion). The attack transitions are shared by every weapon
  override: retiming one for the unarmed clips retimes the sword and two-handed combos too. Re-keying the weapon overrides after a base clip change: *Base attack
  clips and override keys* below.
- **Parameters:** `VelocityX`, `VelocityZ`, `IsGrounded`, `IsRising`, `IsBlocking`, `IsDodging`,
  `IsDodgingBackwards`, `IsInCombat`, `Attack_1/2/3`, `Attack` (trigger — unused by humanoid code; only
  `MonsterAnimationBridge` sets an `Attack` trigger, on monster controllers), `GetHit`, `Death`, `IsSneaking`, `SneakToSprint`,
  `DrawWeapon`, `SheatheWeapon` (triggers), `WeaponSwitchSpeed` (float, default 1.5 — written from `CombatConfigSO`),
  `IsAiming` (bool), `BowFire` (trigger), `Stagger` / `KnockDown` (triggers), `StaggerIndex` (int 0/1), `IsSwimming` (bool),
  `IsUnconscious` (bool), `PickUp` (trigger), `IsExecuting` (bool, player only), `AttackCancel` (bool, player only),
  `IsDrinking` (bool, player + NPCs), `IsSleeping` (bool, NPCs only — routine Sleep), `ShieldInHand` (float 0 / 1, the
  `Block_State` blend — written only on a raise, see below), `LeftHandPose` / `RightHandPose` (int `HandPose`, hand layers).
  There is **no** `Speed` or `IsLockedOn` — don't add them. NPCs never set `IsSneaking` / `SneakToSprint`,
  so every sneak path is player-only.

### Draw / sheathe states (UpperBody layer, player only)

AnyState → `Draw_State` (`DrawWeapon`) / `Sheathe_State` (`SheatheWeapon`), 0.1 s, no self-transition; speed multiplier
`WeaponSwitchSpeed`; each carries `SMB_WeaponSwitchState` (`drawState` true / false). `Draw_State → CombatIdle` and
`Sheathe_State → LockOn Locomotion`, exit time 0.85. Attacks unlock at the `WeaponSwap` event, before the clip ends: the
Base AnyState → `Attack_1` takes the swing and the UpperBody weight drops to 0 (the draw tail is hidden).
AnyState transitions are evaluated before `LockOn Locomotion → CombatIdle (IsInCombat)`, so the draw wins the frame
`IsInCombat` flips. Base clips = `Draw unarmed` / `sheath unarmed` (spec-unarmed-combo-rework); `Sword_AnimatorOverride`
maps them to `draw sword` / `sheath sword`; `Bow_AnimatorOverride`
(→ `Archetype_Bow`) maps them to `Draw Bow` / `Sheathe Bow`; `TwoHanded_AnimatorOverride` to the baked `2hand draw` /
`2hand sheath` `.anim` (two-part vendor draws are merged, never given a second state — `../Animations/Combat/CLAUDE.md`). NPCs never set the triggers.

### Bow aim states (UpperBody layer — player and NPC archers)

`CombatIdle → BowDrawArrow_State` (`Bow Draw Arrow.anim`, IsAiming, 0.15 s) `→ BowAim_State` (`Bow Aim Overdraw.anim`,
looping, exit 0.9) `→ BowFire_State` (`Bow Fire Arrow.anim`, BowFire trigger) `→ BowDrawArrow_State` (exit 0.85,
IsAiming). The three clips are left-wrist bakes of the Mixamo FBX (`Tools/Characters/Bake Bow Wrist Clips`,
`Combat/BAKE_RECIPES.md` → Bow wrist): re-bake, don't hand-edit. Each of the three states
also has `→ CombatIdle` (IsAiming false, 0.15 s), listed **first**. `BowAim_State` carries `SMB_BowAimState`: its enter =
arrow nocked (the Animator GO's `IBowNockListener`). The bow clips sit directly in the base controller (not in an override):
only a bow sets `IsAiming` — the player, and NPC archers through `AIAnimationDriver.SetAiming` / `TriggerBowFire`
(`EntityArcher`, spec-ai-ranged-combat-style). Because the IsAiming-false exit is listed first, `IsAiming` must stay true for a
moment after `BowFire` or the release clip never plays (`CombatConfigSO.npcBowReleaseHold`). A dodge / jump / fall plays full-body (weight 0) and `PlayerCombat` clears `IsAiming`, which returns the aim states to `CombatIdle`.

### Swim state (Base Layer, player only)

AnyState → `Swim Locomotion` (`IsSwimming`, 0.25 s, no self-transition), listed after Death and the stance reactions, before
`Attack_1`; exit → `LockOn Locomotion` (`IsSwimming` false, 0.25 s). Tag `FullBody`. 1D blend tree on `VelocityZ`:
`swimming idle` (0), `swimming` (0.33 = `swimSpeed / runSpeed`), `swimming` at time scale 1.5 (0.58 = `swimSprintSpeed / runSpeed`);
update the thresholds if the speeds change. Both clips: root rotation / Y / XZ baked into pose, looping. **The idle is upright
but `swimming` is prone** (shoulders 0.9 m against 1.5 m), so `swimming.fbx` has a clip **height offset of −0.6** to share one
waterline. Negative raises the body. Re-measure the shoulders if the clip changes.

### Sleeping state (Base Layer, NPCs only — spec-npc-routine-sleep)

AnyState → `Sleeping` (`IsSleeping`, 0.4 s, no self-transition), listed right after `Swim Locomotion` (Death / knockdown /
stagger win); exit → `LockOn Locomotion` (`IsSleeping` false, 0.4 s — no get-up clip). Tag `FullBody`. Clip `sleeping.fbx`
(Mixamo): looping, root rotation / Y / XZ baked, **based on original**. The source lies on a raised surface: the hips sit
~1.1 m above the root, so the bed's `NpcSleepAnchor` sits ~1 m **below** the mattress (Hobb's: local y −0.48) instead of
a clip height offset. The body's head points along the root's −Z.

### Sneak states (Base Layer)

`LockOn Locomotion` → `Stand To Sneak` → `Sneak Locomotion` (2D freeform blend tree on VelocityX/Z:
idle (0,0), walk (0,±0.25 — backward = `sneak walk` at time scale −1), left (−0.25,0), right (0.25,0);
0.25 = `sneakSpeed / runSpeed`) → `Sneak To Stand` / `Sneak To Sprint` → `LockOn Locomotion`. In combat the UpperBody
`CombatIdle` carry pose plays over them. The blend tree is a sub-asset owned by its state — deleting a state that
references a shared blend tree deletes it for every state (rebuilt once this way; check `motion` after removing states).

- **Order matters:** `SneakToSprint` transitions are listed **before** `IsSneaking == false` ones — both
  conditions are true on the same frame and the trigger must win.
- Transition clips get four movement early-outs (`|VelocityX|` or `|VelocityZ|` > 0.05) so the player never
  slides in a static pose. `Sneak To Stand → Stand To Sneak` (IsSneaking) handles a fast re-toggle.
- Sneak clips (`Animations/Sneaking/`): root rotation / Y / XZ baked into pose (like `Walking.fbx`), the four
  locomotion clips loop. Change them through `ModelImporter.clipAnimations`, never raw `.meta` YAML.
- Known: after `GetHit` or an attack the Base layer returns to `LockOn Locomotion` and replays `stand to sneak`.
- Animator writes go only through `HumanoidAnimationBridge` (`Scripts/Core/Animations/CLAUDE.md`).

### Base attack clips and override keys (spec-unarmed-combo-rework)

Base motions: `Attack_1_State` = `lead jab step`, `Attack_2_State` = `hook step`, `Attack_3_State` = `uppercut step` (three
distinct clips). Overrides are keyed on the **base clip**, so every weapon override must map all three, or that state plays the
unarmed punch with a weapon in hand: sword `lead jab step` → `sword shield cut`, `hook step` → `sword shield rising cut`,
`uppercut step` → `sword shield overhead` (axe override: `SwordAttackLeft` / `SwordAttackThrust` / `SwordAttackLeft`); two-handed `lead jab step` / `uppercut step` → `2hand attack refined`, `hook step` →
`2hand attack 2 refined`.
**Changing a base state's motion orphans the override entries keyed on the old clip** — re-key them in the same change
and delete the stale `m_Clips` entries (`SerializedObject`), or the old clip stays referenced (or re-key in place: set
each entry's `m_OriginalClip` to the new base clip, which leaves no stale entry). Same for `CombatIdle`
(= `Unarmed Idle Guard.anim`; sword → `sword shield idle`, axe → `SwordIdle`, two-handed → `2hand idle`, bow → `Bow Idle.anim`, the
yaw-compensated bake of the Mixamo idle, played with the bow's carry seat — spec-bow-idle-compensated-bake) and `Block_State` (= `Unarmed Block
Guard.anim`, the unarmed high guard; sword → `Sword Braced Guard`, two-handed → `2hand blocking`). The player and styled
NPCs (`EntityGuard` → `AIAnimationDriver.SetBlocking`, spec-ai-npc-blocking) set `IsBlocking`.

**`Block_State` is a 1D blend tree on `ShieldInHand`** (spec-one-hand-braced-block): `Unarmed Block Guard` @0 = the
weapon's own guard (each override maps it), `Block Idle` @1 = the shield guard (sword + shield in hand). The shield is an
offhand that changes the block clip, not a separate override controller (a controller swap resets every parameter). Written
only through `HumanoidAnimationBridge.SetBlocking(value, shieldInHand)`, which sets the float **only when raising**: the exit
blend must keep the clip it raised with. Player: `PlayerCombat` passes `_blockShield != null` (raise + re-push after a
controller reset); NPCs: `EntityGuard` passes `NpcArmament.ShieldInHand` (raise + per-tick re-send). A new weapon
family's override maps `Unarmed Block Guard` to its guard and leaves `Block Idle` unless it can never hold a shield
(`TwoHanded_AnimatorOverride` maps both). Tests: `HumanoidTemplateControllerTests` (blend tree, overrides).

**Replacing an FBX in place can leave a state playing no clip.** The editor's compiled controller keeps the destroyed
old clip: `state.motion` and the YAML look right, but at runtime the state reports `length` 1.00 and an empty
`GetCurrentAnimatorClipInfo`, and the clip is missing from `ctrl.animationClips`. Reimporting the FBX or the controller
doesn't fix it. Reassigning `state.motion` (null → clip) or restarting the editor does.

---

## SMB Wiring

`Attack_1/2/3_State` each carry **`SMB_AttackState`** (player; `attackIndex` = 1/2/3, logging only —
default 0 is harmless) **and `SMB_EntityAttackState`** (AI). SMBs are `!u!114` blocks appended to the
`.controller` YAML and referenced from the state's `m_StateMachineBehaviours`
(`SMB_AttackState` script GUID `be56e776511a4e3458f4511c065cbeac`). Missing SMB on an attack state = no
interrupt cleanup (hitbox and `IsAttacking` stay on).

---

## Transition Rules

- **Attack transitions use `interruptionSource = Destination`** (every Base attack-state transition + AnyState →
  `Attack_1`; tested). **Never `None` on the Base Layer**: a non-interruptible transition blocks AnyState too, so a dodge,
  stagger or death waited out the whole 0.42 s combo blend (measured with a manual `Animator.Update` loop). **Never
  `Source` on combo transitions**: with `Source`, the old step-2 Uppercut's (1.33 s) 0.42 s blend to Attack_3, requested at
  ComboWindowOpen (0.63), was cancelled by `Attack_2 → CombatIdle` (now `→ LockOn Locomotion`) at exit time 0.89, so step 3 never
  played. When adding/retiming combo clips, check `requestTime + blendDuration < exitTime` (normalized),
  or keep the combo transition at `Destination`.
- **Plain hits don't interrupt attacks:** `GetHit` (no clip yet) is reached from `LockOn Locomotion` only, never from
  AnyState, and `HumanoidAnimationBridge.TriggerGetHit` skips while Base is in a full-body state (a latched trigger would
  fire once the swing ends). Death stays safe via `EntityBrain.TransitionToDead → EndAttack` + the ragdoll.
- **Stance reactions and death interrupt attacks (AI + Player):** AnyState → `Death`, `KnockedDown` → `GettingUp`
  (`KnockDown`), `Stagger_1/2` (`Stagger` + `StaggerIndex` 0/1), all listed before AnyState → `Attack_1`; exit →
  `LockOn Locomotion`. Clips: `Combat/Unarmed/stagger 1/2.fbx` (placeholder for every weapon), `Combat/knocked
  down.fbx` → `Combat/getting up.fbx` — Humanoid, root rotation / Y / XZ baked into pose (NPC Animators have
  `applyRootMotion` off; so does the Player). The Player sets them through `PlayerStance` → `PlayerStateManager` →
  `PlayerAnimationDriver` (`Scripts/Player/PLAYER_STATES.md` → Stance).
- **Unconscious** (spec-unconscious-knockout): AnyState → `Unconscious` (`KnockDown` + `IsUnconscious`, listed just before
  AnyState → `KnockedDown`, which now requires `IsUnconscious == false`). `Unconscious` plays the same `Knocked Down` clip
  (non-looping → holds the last, on-the-ground frame) and goes to `GettingUp` on `IsUnconscious == false` with **no exit
  time** (an exit-time transition with a condition is unreliable past the end of a non-looping clip). Tag `FullBody`. Written
  only through `HumanoidAnimationBridge.SetUnconscious(bool)` (false also drops an unconsumed `KnockDown`).
- **PickUp** (spec-knockout-gold-looting): Base Layer `LockOn Locomotion → PickUp` on the `PickUp` trigger (0.25 s), `PickUp →
  LockOn Locomotion` at exit time 0.9 (0.4 s). Tag `FullBody`. Clip `Animations/picking up.fbx` trimmed to frames 15–165
  (5 s; lowest point / grab at 1.75 s), root rotation / Y / XZ baked into pose. Timings mirrored in `CombatConfigSO`
  `knockoutLootGrabTime` / `knockoutLootClipDuration` — re-measure both if the trim changes. Written only through
  `HumanoidAnimationBridge.PlayPickUp()` (NPC knockout loot; the player doesn't use it yet).
- **Drink** (spec-potion-drink-animation, player + NPCs): **UpperBody** `Drink_State` (tag `UpperBodyAction`, speed 1.5),
  AnyState → it on `IsDrinking` (0.2 s, no self-transition); exit on `IsDrinking == false` → `LockOn Locomotion` /
  `CombatIdle` by `IsInCombat` (0.25 s, like `Block_State`). Legs keep walking. Clip `Animations/drink potion.fbx` trimmed to
  frames 57–177 (4 s, **left hand** drinks — the sword hand stays free), root rotation / Y / XZ baked. Timings mirrored in
  `CombatConfigSO` `drinkHealTime` 1.55 s (head tilted back) / `drinkDuration` 2.5 s (arm lowered) at speed 1.5 —
  re-measure both if the trim or the state speed changes. Written only through `HumanoidAnimationBridge.SetDrinking`.
- **Execute** (spec-execute-unconscious-npc, player only): AnyState → `Execute` on `IsExecuting` (0.15 s, no self-transition),
  listed after `Swim Locomotion`, just before AnyState → `Attack_1`; `Execute → LockOn Locomotion` on `IsExecuting == false`,
  no exit time, 0.2 s (cancel and normal end — `PlayerExecution` clears the bool at the `ExecuteEnd` event). Clip
  `Combat/execute stomp.fbx` (`Execute Stomp`), tag `FullBody`. Written only through `HumanoidAnimationBridge.SetExecuting`.
  NPCs never set it (their receiver has no `ExecuteImpact`).
