# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Controllers

> Loaded when Claude accesses `Humanoid_Template.controller`. Covers its layers, parameters, SMB wiring
> and transition rules. Shared pipeline contract: `.claude/rules/attack-pipeline.md`.

---

## Humanoid_Template.controller

Used by **the Player** (`Player.prefab` → Animator) **and humanoid NPCs**. Weapon families swap clips via
`AnimatorOverrideController`s (`../Overrides/Humanoid_Base`, `Sword_AnimatorOverride`, chosen by
`WeaponSO.ResolvedAnimatorOverride`).

- **Layers:** `Base Layer` (locomotion `LockOn Locomotion` 2D blend tree, sneak states, jump/fall/land,
  dodge, `GetHit`, `Death`) and the upper-body-masked **`Attack`** layer at a **constant weight 1** with its
  **own** `LockOn Locomotion` (default) → `CombatIdle` (IsInCombat) → `Attack_1/2/3_State`, AnyState →
  `Block_State`, plus jump/fall/dodge copies. The Attack layer always drives the upper body, even out of
  combat — **any new Base locomotion state needs an Attack-layer twin**, or the torso plays standing
  locomotion over the new legs.
- **Parameters:** `VelocityX`, `VelocityZ`, `IsGrounded`, `IsRising`, `IsBlocking`, `IsDodging`,
  `IsDodgingBackwards`, `IsInCombat`, `Attack_1/2/3`, `GetHit`, `Death`, `IsSneaking`, `SneakToSprint`,
  `DrawWeapon`, `SheatheWeapon` (triggers), `WeaponSwitchSpeed` (float, default 1.5 — written from `CombatConfigSO`),
  `IsAiming` (bool), `BowFire` (trigger).
  There is **no** `Speed` or `IsLockedOn` — don't add them. NPCs never set `IsSneaking` / `SneakToSprint`,
  so every sneak path is player-only.

### Draw / sheathe states (Attack layer, player only)

AnyState → `Draw_State` (`DrawWeapon`) / `Sheathe_State` (`SheatheWeapon`), 0.1 s, no self-transition; speed multiplier
`WeaponSwitchSpeed`; each carries `SMB_WeaponSwitchState` (`drawState` true / false). `Draw_State → CombatIdle` and
`Sheathe_State → LockOn Locomotion` (IsSneaking false) / `→ Sneak Locomotion` (IsSneaking true), exit time 0.85.
`Draw_State → Attack_1_State` (Attack_1, interruption Source) — attacks unlock at the `WeaponSwap` event, before the clip ends.
AnyState transitions are evaluated before `LockOn Locomotion → CombatIdle (IsInCombat)`, so the draw wins the frame
`IsInCombat` flips. Base clips = `draw sword` / `sheath sword` (also the unarmed placeholder); `Sword_AnimatorOverride`
lists them explicitly (self-mapped) so an unarmed clip only replaces the base-state motion; `Bow_AnimatorOverride`
(→ `Archetype_Bow`) maps them to `Draw Bow` / `Sheathe Bow`. NPCs never set the triggers.

### Bow aim states (Attack layer, player only)

`CombatIdle → BowDrawArrow_State` (`Draw arrow`, IsAiming, 0.15 s) `→ BowAim_State` (`Aim Overdraw`, looping, exit 0.9)
`→ BowFire_State` (`Fire arrow`, BowFire trigger) `→ BowDrawArrow_State` (exit 0.85, IsAiming). Each of the three states
also has `→ CombatIdle` (IsAiming false, 0.15 s), listed **first**. `BowAim_State` carries `SMB_BowAimState`: its enter =
arrow nocked. The bow clips sit directly in the base controller (not in an override): only a bow sets `IsAiming`, and NPCs
never do. The AnyState dodge / jump / fall transitions pull the layer out; `PlayerCombat` then clears `IsAiming`.

### Sneak states (both layers)

`LockOn Locomotion` → `Stand To Sneak` → `Sneak Locomotion` (2D freeform blend tree on VelocityX/Z:
idle (0,0), walk (0,±0.25 — backward = `sneak walk` at time scale −1), left (−0.25,0), right (0.25,0);
0.25 = `sneakSpeed / runSpeed`) → `Sneak To Stand` / `Sneak To Sprint` → `LockOn Locomotion`. The same four
states + transitions exist on the Attack layer (sharing the blend tree), plus `Sneak Locomotion ⇄
CombatIdle` (IsInCombat) and `Block_State → Sneak Locomotion` (IsBlocking false + IsSneaking);
`CombatIdle/Block_State → LockOn Locomotion` require `IsSneaking == false`.

- **Order matters:** `SneakToSprint` transitions are listed **before** `IsSneaking == false` ones — both
  conditions are true on the same frame and the trigger must win.
- Transition clips get four movement early-outs (`|VelocityX|` or `|VelocityZ|` > 0.05) so the player never
  slides in a static pose. `Sneak To Stand → Stand To Sneak` (IsSneaking) handles a fast re-toggle.
- Sneak clips (`Animations/Sneaking/`): root rotation / Y / XZ baked into pose (like `Walking.fbx`), the four
  locomotion clips loop. Change them through `ModelImporter.clipAnimations`, never raw `.meta` YAML.
- Known: after `GetHit` the Base layer returns to `LockOn Locomotion` and replays `stand to sneak`.
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

- **Combo transitions must not be interruptible by the source state's exit** — `Attack_1 → Attack_2` and
  `Attack_2 → Attack_3` both use `interruptionSource = None`. With `Source`, the Uppercut's (1.33 s) 0.42 s blend to Attack_3, requested at
  ComboWindowOpen (0.63), was cancelled by `Attack_2 → CombatIdle` at exit time 0.89, so step 3 never
  played. When adding/retiming combo clips, check `requestTime + blendDuration < exitTime` (normalized),
  or keep the combo transition at `None`.
- **Hits don't interrupt punches:** `GetHit` / `Death` are AnyState transitions on the **Base** layer while
  attacks live on the masked `Attack` layer — an NPC keeps punching while hit. Death stays safe via
  `EntityBrain.TransitionToDead → EndAttack` + the ragdoll.
