# CLAUDE.md — Assets/_Game/Prefabs/Player

> Loaded when Claude accesses the Player prefab. Covers its hierarchy and wiring rules.
> Camera behaviour (Cinemachine OTS): `Scripts/Player/CAMERA.md`; player scripts: `Scripts/Player/CLAUDE.md`.

---

## Player Prefab Structure

The Player prefab is **fully self-contained** — it owns its camera, UI and audio listener. Drop it into
any scene without external wiring.

```
Player.prefab
├── CharacterController  (Height 1.7, Center Y 0.9) — no Rigidbody
├── Animator             (Apply Root Motion OFF; Controller: Humanoid_Template — shared with humanoid NPCs)
├── Root scripts (non-exhaustive — wiring-relevant components only):
│   PlayerController, HumanoidAnimationBridge, PlayerAnimationDriver, CameraController,
│   PlayerStateManager, PlayerCombat, DodgeController, StaminaSystem, PlayerHealth, PlayerStats,
│   PlayerSkills, InteractionSystem, InventorySystem, ActionBarSystem, EquipmentSystem, XPSystem,
│   LevelSystem, LearningPointSystem, GoldSystem (_startingGold 0: every origin starts broke), LockOnSystem,
│   EquipmentVisuals, DialogueSystem, BowAimCorrection (calibrated arrow line — Scripts/Combat/BOW.md),
│   TwoHandGripConstraint (order 250 — re-aims a drawn two-handed weapon through both hands, Scripts/Inventory/CLAUDE.md → TwoHandGripConstraint),
│   FootstepEmitter (distance-based steps, gated by PlayerStateManager as IFootstepGate — Scripts/Audio/CLAUDE.md)
│   Also on the root: ActionRootMotion, AnimationEventReceiver, PlayerAttackStep, PlayerExecution, PlayerFallDamage,
│   PlayerMovementStamina, PlayerPotionDrinker, PlayerRewards, PlayerSaveAdapter, PlayerSneak, PlayerStance,
│   PlayerSwimming, SleepSystem, StatusEffectHost, StolenGoodsTracker, ContainerSystem, CookingSystem, DoorSystem,
│   FactionMember
├── UICanvas             (nested prefab Prefabs/UI/UICanvas.prefab — see Prefabs/UI/CLAUDE.md)
├── CameraTarget         (pure Transform pivot, local Y 1.6 — Cinemachine Follow/LookAt target)
├── Virtual Camera       (CinemachineCamera + CinemachineThirdPersonFollow + CinemachineRotateWithFollowTarget — no Deoccluder / Decollider, see Scripts/Player/CAMERA.md)
├── Camera               (Camera + CinemachineBrain + AudioListener + UniversalAdditionalCameraData)
├── Character            (nested Mixamo FBX prefab, Humanoid rig)
│   ├── base_hero                     (Body SMR below the neck — mesh `base_hero_body_baked`; hidden while armor is worn)
│   ├── Head                          (added SMR, `base_hero_head_baked`, same canonical bones / skin material; includes inset, closed neck below the garment — always visible)
│   ├── …/RightHand/WeaponSocket      (right hand — drawn weapon, default)
│   │   └── UnarmedHitbox             (Layer 0, inactive; disabled SphereCollider r 0.25 + WeaponHitbox)
│   ├── …/LeftHand/LeftWeaponSocket   (left hand — drawn bow, archetype `drawnHand = Left`; second hand of the two-hand grip)
│   │   └── UnarmedHitbox_L           (same as UnarmedHitbox — left fist: lead jab, uppercut)
│   ├── …/mixamorig:Head/HelmetSocket  (helmet visual)
│   ├── mixamorig:Hips/UndrawnWeaponSocket   (hip sheath, position (0,0,0) — tune by hand)
│   └── mixamorig:Spine2/BackWeaponSocket    (back sheath, (0, 0.05, -0.15) first guess)
└── Hitbox               (Layer 7 CharacterHitbox, trigger CapsuleCollider r 0.3 h 1.7 center y 0.9 — hurtbox)
```

---

## Rules

- Lootable gold: root `InventorySystem._collectGoldAsCurrency` is enabled, and both its `_onGoldLootRequested` and the root `GoldSystem._onGoldLootRequested` reference `OnGoldLootRequested`. Keep this wiring player-only; NPC inventories must retain gold as loot.
- **Do not add a second Camera or AudioListener** to a scene that uses this prefab — the `Camera` child owns
  both (plus the `CinemachineBrain`).
- `DialogueSystem` lives on the **Player root** (not on UICanvas or a scene GO). Its `_dialogueUI` and
  `DialogueUI._dialogueSystem` are cross-wired via nested-prefab overrides on this prefab.
- `EquipmentVisuals` socket refs: re-wire with `Game/Dev/Wire EquipmentVisuals on Player Prefab` (creates
  `LeftWeaponSocket` / `BackWeaponSocket` if missing; wires `_bodyRenderer` to `base_hero` and `_skeletonRoot` to `Character/mixamorig:Hips`).
- `Hitbox` is hit by AI `WeaponHitbox` sweeps. The player's own sweeps skip it (`SetOwner`), `LockOnSystem`
  (scans Layers 6 + 7) ignores it (no `EntityHealth`) and the CharacterController ignores triggers — so it has no side effects.
- `UnarmedHitbox` / `UnarmedHitbox_L` are toggled by `PlayerCombat` (active only while bound; `_unarmedHitbox` /
  `_unarmedHitboxLeft`). The hand used per combo step is data: `CombatConfigSO.unarmedAttacks`.
- Camera-relative movement uses `Camera.main`, cached in `Awake` as `_mainCamera`.
- `PlayerAnimationDriver` only reads `CharacterController.velocity` — it never writes movement state.
