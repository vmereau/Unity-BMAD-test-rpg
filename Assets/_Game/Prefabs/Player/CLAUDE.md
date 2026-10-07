# CLAUDE.md — Assets/_Game/Prefabs/Player

> Loaded when Claude accesses the Player prefab. Covers its hierarchy and wiring rules.
> Camera behaviour (Cinemachine OTS) and player scripts: `Scripts/Player/CLAUDE.md`.

---

## Player Prefab Structure

The Player prefab is **fully self-contained** — it owns its camera, UI and audio listener. Drop it into
any scene without external wiring.

```
Player.prefab
├── CharacterController  (Height 1.8, Center Y 1.0) — no Rigidbody
├── Animator             (Apply Root Motion OFF; Controller: Humanoid_Template — shared with humanoid NPCs)
├── PlayerController, HumanoidAnimationBridge, PlayerAnimationDriver, CameraController,
│   PlayerStateManager, PlayerCombat, DodgeController, StaminaSystem, PlayerHealth, PlayerStats,
│   PlayerSkills, InteractionSystem, InventorySystem, ActionBarSystem, EquipmentSystem, XPSystem,
│   LevelSystem, LearningPointSystem, GoldSystem (_startingGold 500), LockOnSystem,
│   EquipmentVisuals, DialogueSystem
├── UICanvas             (nested prefab Prefabs/UI/UICanvas.prefab — see Prefabs/UI/CLAUDE.md)
├── CameraTarget         (pure Transform pivot, local Y 1.6 — Cinemachine Follow/LookAt target)
├── Virtual Camera       (CinemachineCamera + CinemachineFollow + CinemachineRotateWithFollowTarget)
├── Camera               (Camera + CinemachineBrain + AudioListener + UniversalAdditionalCameraData)
├── Character            (nested Mixamo FBX prefab, Humanoid rig)
│   ├── …/WeaponSocket                (hand — drawn weapon)
│   │   └── UnarmedHitbox             (Layer 0, inactive; disabled SphereCollider r 0.25 + WeaponHitbox)
│   ├── mixamorig:Hips/UndrawnWeaponSocket   (hip sheath, position (0,0,0) — tune by hand)
│   └── mixamorig:Spine2/BackWeaponSocket    (back sheath, (0, 0.05, -0.15) first guess)
└── Hitbox               (Layer 7 CharacterHitbox, trigger CapsuleCollider r 0.3 h 1.7 center y 0.9 — hurtbox)
```

---

## Rules

- **Do not add a second Camera or AudioListener** to a scene that uses this prefab — the `Camera` child owns
  both (plus the `CinemachineBrain`).
- `DialogueSystem` lives on the **Player root** (not on UICanvas or a scene GO). Its `_dialogueUI` and
  `DialogueUI._dialogueSystem` are cross-wired via nested-prefab overrides on this prefab.
- `EquipmentVisuals` socket refs: re-wire with `Game/Dev/Wire EquipmentVisuals on Player Prefab`.
- `Hitbox` is hit by AI `WeaponHitbox` sweeps. The player's own sweeps skip it (`SetOwner`), `LockOnSystem`
  scans Layer 6 only and the CharacterController ignores triggers — so it has no side effects.
- `UnarmedHitbox` is toggled by `PlayerCombat` (active only while bound).
- Camera-relative movement uses `Camera.main`, cached in `Awake` as `_mainCamera`.
- `PlayerAnimationDriver` only reads `CharacterController.velocity` — it never writes movement state.
