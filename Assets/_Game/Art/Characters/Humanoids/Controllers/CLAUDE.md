# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Controllers

> Loaded when Claude accesses `Humanoid_Template.controller`. Covers its layers, parameters, SMB wiring
> and transition rules. Shared pipeline contract: `.claude/rules/attack-pipeline.md`.

---

## Humanoid_Template.controller

Used by **the Player** (`Player.prefab` → Animator) **and humanoid NPCs**. Weapon families swap clips via
`AnimatorOverrideController`s (`../Overrides/Humanoid_Base`, `Combat/one handed/Sword_AnimatorOverride`,
`Combat/two handed/TwoHanded_AnimatorOverride`, chosen by `WeaponSO.ResolvedAnimatorOverride`). The two-handed override
also replaces `Block Idle` and `unarmed stagger 1/2`.

- **Layers** (spec-full-body-attacks-root-motion): **`Base Layer`** holds locomotion (`LockOn Locomotion` 2D blend
  tree, sneak states) **and every full-body action** — `Attack_1/2/3_State` (AnyState → `Attack_1`), jump/fall/land,
  dodge, `Stagger_1/2`, `KnockedDown → GettingUp`, `Death`, `GetHit`. **`UpperBody`** (index 1, `UpperBodyMask`, Override,
  default weight **0**) holds only `LockOn Locomotion` (default), `CombatIdle` (the weapon carry pose, IsInCombat),
  AnyState → `Block_State` / `Draw_State` / `Sheathe_State`, and the bow aim states. No twins: **a new Base state needs
  nothing on UpperBody** — tag it instead (below).
- **UpperBody weight** is not authored: `PlayerAnimationDriver` / `HumanoidAIAnimationDriver` step it every frame with
  `UpperBodyWeight` (`Scripts/Core/Animations/`) from the Animator's **state tags**, read on each layer's destination
  state (next state during a transition): Base tag `Attack` or `FullBody` → 0; else UpperBody tag `UpperBodyAction` or
  `IsInCombat` → 1; else 0 (blend `PlayerConfigSO.upperBodyBlendTime` / the NPC driver's `_upperBodyBlendTime`, 0.15 s).
  Tags: `Attack` = the three attack states; `FullBody` = JumpRise, Falling, Dodging, Dodge back, Stagger_1/2,
  KnockedDown, GettingUp, Death, Dead (not Landing: a block / draw pressed on landing must show); `UpperBodyAction` = Block, Draw, Sheathe, BowDrawArrow/Aim/Fire. **A new
  full-body Base state must get the `FullBody` tag** or the carry pose plays over it in combat.
- **Attacks are full-body.** Every attack state exits to `LockOn Locomotion` on `IsBlocking` (listed first — block-cancel),
  then the combo / exit-time transitions. Attack clips stay root-baked (`applyRootMotion` off everywhere): the Mixamo clips
  are in place (net root travel 0), so the player's forward step is scripted (`PlayerAttackStep`, `Scripts/Combat/PLAYER_COMBO.md`).
- **Parameters:** `VelocityX`, `VelocityZ`, `IsGrounded`, `IsRising`, `IsBlocking`, `IsDodging`,
  `IsDodgingBackwards`, `IsInCombat`, `Attack_1/2/3`, `GetHit`, `Death`, `IsSneaking`, `SneakToSprint`,
  `DrawWeapon`, `SheatheWeapon` (triggers), `WeaponSwitchSpeed` (float, default 1.5 — written from `CombatConfigSO`),
  `IsAiming` (bool), `BowFire` (trigger), `Stagger` / `KnockDown` (triggers), `StaggerIndex` (int 0/1), `IsSwimming` (bool),
  `IsUnconscious` (bool), `PickUp` (trigger).
  There is **no** `Speed` or `IsLockedOn` — don't add them. NPCs never set `IsSneaking` / `SneakToSprint`,
  so every sneak path is player-only.

### Draw / sheathe states (UpperBody layer, player only)

AnyState → `Draw_State` (`DrawWeapon`) / `Sheathe_State` (`SheatheWeapon`), 0.1 s, no self-transition; speed multiplier
`WeaponSwitchSpeed`; each carries `SMB_WeaponSwitchState` (`drawState` true / false). `Draw_State → CombatIdle` and
`Sheathe_State → LockOn Locomotion`, exit time 0.85. Attacks unlock at the `WeaponSwap` event, before the clip ends: the
Base AnyState → `Attack_1` takes the swing and the UpperBody weight drops to 0 (the draw tail is hidden).
AnyState transitions are evaluated before `LockOn Locomotion → CombatIdle (IsInCombat)`, so the draw wins the frame
`IsInCombat` flips. Base clips = `draw sword` / `sheath sword` (also the unarmed placeholder); `Sword_AnimatorOverride`
lists them explicitly (self-mapped) so an unarmed clip only replaces the base-state motion; `Bow_AnimatorOverride`
(→ `Archetype_Bow`) maps them to `Draw Bow` / `Sheathe Bow`; `TwoHanded_AnimatorOverride` to the baked `2hand draw` /
`2hand sheath` `.anim` (two-part vendor draws are merged, never given a second state — `../Animations/Combat/CLAUDE.md`). NPCs never set the triggers.

### Bow aim states (UpperBody layer, player only)

`CombatIdle → BowDrawArrow_State` (`Draw arrow`, IsAiming, 0.15 s) `→ BowAim_State` (`Aim Overdraw`, looping, exit 0.9)
`→ BowFire_State` (`Fire arrow`, BowFire trigger) `→ BowDrawArrow_State` (exit 0.85, IsAiming). Each of the three states
also has `→ CombatIdle` (IsAiming false, 0.15 s), listed **first**. `BowAim_State` carries `SMB_BowAimState`: its enter =
arrow nocked. The bow clips sit directly in the base controller (not in an override): only a bow sets `IsAiming`, and NPCs
never do. A dodge / jump / fall plays full-body (weight 0) and `PlayerCombat` clears `IsAiming`, which returns the aim states to `CombatIdle`.

### Swim state (Base Layer, player only)

AnyState → `Swim Locomotion` (`IsSwimming`, 0.25 s, no self-transition), listed after Death and the stance reactions, before
`Attack_1`; exit → `LockOn Locomotion` (`IsSwimming` false, 0.25 s). Tag `FullBody`. 1D blend tree on `VelocityZ`:
`swimming idle` (0), `swimming` (0.33 = `swimSpeed / runSpeed`), `swimming` at time scale 1.5 (0.58 = `swimSprintSpeed / runSpeed`);
update the thresholds if the speeds change. Both clips: root rotation / Y / XZ baked into pose, looping. **The idle is upright
but `swimming` is prone** (shoulders 0.9 m against 1.5 m), so `swimming.fbx` has a clip **height offset of −0.6** to share one
waterline. Negative raises the body. Re-measure the shoulders if the clip changes.

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
  `Source` on combo transitions**: with `Source`, the Uppercut's (1.33 s) 0.42 s blend to Attack_3, requested at
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
  `PlayerAnimationDriver` (`Scripts/Player/CLAUDE.md` → Stance).
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
