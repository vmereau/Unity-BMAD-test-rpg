# CLAUDE.md — Assets/_Game/Art/Characters/Humanoids/Controllers

> Loaded when Claude accesses `Humanoid_Template.controller`. Covers its layers, parameters, SMB wiring
> and transition rules. Shared pipeline contract: `.claude/rules/attack-pipeline.md`.

---

## Humanoid_Template.controller

Used by **the Player** (`Player.prefab` → Animator) **and humanoid NPCs**. Weapon families swap clips via
`AnimatorOverrideController`s (`../Overrides/Humanoid_Base`, `Sword_AnimatorOverride`, chosen by
`WeaponSO.ResolvedAnimatorOverride`).

- **Layers:** `Base Layer` (locomotion `LockOn Locomotion` 2D blend tree, jump/fall/land, dodge, block,
  `GetHit`, `Death`) and the upper-body-masked **`Attack`** layer (`CombatIdle`, `Attack_1/2/3_State`),
  weighted by `IsInCombat`.
- **Parameters:** `VelocityX`, `VelocityZ`, `IsGrounded`, `IsRising`, `IsBlocking`, `IsDodging`,
  `IsDodgingBackwards`, `IsInCombat`, `Attack_1/2/3`, `GetHit`, `Death`. There is **no** `Speed` or
  `IsLockedOn` — don't add them.
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

- **Combo transitions must not be interruptible by the source state's exit** — `Attack_2 → Attack_3` uses
  `interruptionSource = None`. With `Source`, the Uppercut's (1.33 s) 0.42 s blend to Attack_3, requested at
  ComboWindowOpen (0.63), was cancelled by `Attack_2 → CombatIdle` at exit time 0.89, so step 3 never
  played. When adding/retiming combo clips, check `requestTime + blendDuration < exitTime` (normalized),
  or keep the combo transition at `None`.
- **Hits don't interrupt punches:** `GetHit` / `Death` are AnyState transitions on the **Base** layer while
  attacks live on the masked `Attack` layer — an NPC keeps punching while hit. Death stays safe via
  `EntityBrain.TransitionToDead → EndAttack` + the ragdoll.
