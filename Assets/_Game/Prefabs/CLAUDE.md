# CLAUDE.md — Assets/_Game/Prefabs

> Loaded when Claude accesses files in this folder. Contains all prefab structure rules and gotchas.

---

## Player Prefab Structure

The Player prefab is **fully self-contained** — it owns its own camera, UI, and audio listener. Place it in any scene without external wiring.

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF; Controller: PlayerAnimatorController)
├── PlayerController.cs
├── PlayerAnimator.cs
├── CameraController.cs
├── PlayerStateManager.cs
├── PlayerCombat.cs
├── DodgeController.cs
├── StaminaSystem.cs
├── PlayerHealth.cs
├── PlayerStats.cs
├── PlayerSkills.cs
├── InteractionSystem.cs
├── InventorySystem.cs
├── ActionBarSystem.cs
├── EquipmentSystem.cs
├── XPSystem.cs
├── LevelSystem.cs
├── LearningPointSystem.cs
├── GoldSystem.cs        (_startingGold: 500)
├── LockOnSystem.cs
├── EquipmentVisuals.cs  (story 7-4; wire via Game/Dev/Wire EquipmentVisuals on Player Prefab)
├── DialogueSystem.cs    (story 6-1; _dialogueUI → DialogueUI inside UICanvas.prefab/DialoguePanel)
├── UICanvas             (child — nested prefab: Assets/_Game/Prefabs/UI/UICanvas.prefab)
│   ├── Canvas + GraphicRaycaster + UIScreenManager  (no CanvasScaler — removed story 6-1)
│   ├── EventSystem      (child of UICanvas with plain Transform — EventSystem + InputSystemUIInputModule)
│   ├── Crosshair        (Image)
│   ├── ActionBar        (ActionBarUI — nested within UICanvas.prefab)
│   │   └── 6x ActionBarSlot  (ActionBarSlotUI, Icon, StackCountText, KeyLabel)
│   ├── DialoguePanel    (nested prefab: Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab)
│   └── Menus            (container GO — tab-based screen panels)
│       ├── TabBar            (TabBarUI — nested within UICanvas.prefab)
│       ├── InventoryUI       (inactive by default)
│       ├── QuestLogUI        (inactive by default)
│       ├── CharacterStatsUI  (inactive by default)
│       └── OptionsUI         (inactive by default)
├── CameraTarget         (child — pure Transform pivot, local Y = 1.6; Cinemachine Follow/LookAt target)
├── Virtual Camera       (child — CinemachineVirtualCamera; Follow/LookAt → CameraTarget)
│   └── cm               (CinemachinePipeline, CinemachineTransposer, CinemachineSameAsFollowTarget)
├── Camera               (child — Camera + CinemachineBrain + AudioListener + UniversalAdditionalCameraData)
└── Character            (child — nested Mixamo FBX prefab: Idle.fbx, Humanoid rig)
```

- The prefab is **self-contained**: drop it into any scene and it works without scene-level camera or UI wiring
- `CameraController` is on the Player root (previously was a scene-level component in TestScene — drift resolved)
- `Camera` child owns the `CinemachineBrain` and `AudioListener` — do NOT add a second Camera or AudioListener to any scene that uses this prefab
- Cinemachine `Follow` and `LookAt` on `Virtual Camera` both point to `CameraTarget` (local Y = 1.6)
- `UICanvas` is a **nested prefab** (`UICanvas.prefab`) — edit `UICanvas.prefab` directly for layout changes; overrides live on the Player prefab
- `UICanvas.prefab` has `EventSystem` as a **child of UICanvas** (plain `Transform`, not `RectTransform` — non-UI GO parented to a Canvas). Prefab assets must have a single root; adding EventSystem as a second root breaks Prefab Mode. The EventSystem is required for all button pointer events; it lives here so it's always present with the Player
- `DialoguePanel.prefab` is nested inside `UICanvas.prefab` as a `PrefabInstance`; `DialogueUI._dialogueSystem` and `DialogueSystem._dialogueUI` are cross-wired via Player.prefab nested-prefab overrides — do NOT try to wire them inside UICanvas.prefab alone
- `DialogueSystem` is on the **Player root** (not UICanvas, not a separate scene GO)
- No Rigidbody on player — `CharacterController` only
- Camera-relative movement uses `Camera.main` cached in `Awake()` as `_mainCamera`
- `PlayerAnimator` reads `CharacterController.velocity` passively for movement — never writes to movement state

---

## Enemy Prefab Structure

> See `Assets/_Game/Prefabs/Enemies/CLAUDE.md` for full enemy prefab hierarchy, physics hit detection requirements, and checklist for adding new enemy types.

---

## Prefab Layer Rules

| Prefab type | Required layer | Why |
|-------------|---------------|-----|
| World item pickup | **Interactable (Layer 8)** on root | Root has the collider; `InteractionSystem` raycasts only against Layer 8 |
| NPC (Entity_base variant) | **Characters (Layer 6)** on root + **Interactable (Layer 8)** on `InteractionCollider` child | Root must be Layer 6 so `LockOnSystem._lockOnLayerMask (m_Bits:64)` can detect them; a dedicated `InteractionCollider` child GO on Layer 8 carries the trigger CapsuleCollider for `InteractionSystem`. `GetComponentInParent<IInteractable>()` traverses up from the child to find `NPCPresence` on the root. |
| Enemy (Entity_base variant) | **Characters (Layer 6)** on root | Lock-on only; enemies are not interactable, so no Layer 8 child needed |

**NPC two-collider pattern** (do NOT collapse into one):
- `Hitbox` child (Layer 7 — CharacterHitbox): non-trigger CapsuleCollider, used by `WeaponHitbox` for combat damage detection
- `InteractionCollider` child (Layer 8 — Interactable): trigger CapsuleCollider (Radius: 0.5, Height: 2.0, Center Y: 1.0), used by `InteractionSystem` for dialogue detection

---

## Weapon Prefabs

> See `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` for the full weapon prefab spec: two-prefab convention (`_World` / `_Visual`), Drawn/Sheathed child convention, kinematic Rigidbody requirement, trigger collider placement, grip alignment, and GUID notes.

---

## World Item Prefab Rules (ItemSO.worldItemPrefab)

Prefabs assigned to `ItemSO.worldItemPrefab` (used for dropped items) **must** have:
- A **Rigidbody** component — `InventoryUI.DropItem()` calls `AddForce` immediately after `Instantiate`; no Rigidbody causes a `NullReferenceException`
- **Layer: Interactable (Layer 8)** — so the player can pick it back up via `InteractionSystem`
- **ItemPickup.cs** component with `_item` pre-assigned in the prefab

**Drop physics pattern:** `DropItem()` in `InventoryUI` instantiates the prefab, then applies impulse:
```csharp
go.GetComponent<Rigidbody>().AddForce(forward * 2f + Vector3.up * 1f, ForceMode.Impulse);
```
