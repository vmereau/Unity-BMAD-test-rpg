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
├── UICanvas             (child — nested prefab: Assets/_Game/Prefabs/UI/UICanvas.prefab)
│   ├── Canvas + CanvasScaler + GraphicRaycaster + InventoryUI
│   ├── Crosshair        (Image)
│   ├── ActionBar        (ActionBarUI — nested within UICanvas.prefab)
│   │   └── 6x ActionBarSlot  (ActionBarSlotUI, Icon, StackCountText, KeyLabel)
│   └── InventoryUI      (container GO, no components)
│       ├── EquipmentPanel    (inactive by default; EquipmentUI — nested within UICanvas.prefab)
│       │   └── SlotWeapon/Helmet/Armor/Ring1/Ring2/Necklace  (EquipmentSlotUI)
│       ├── InventoryPanel    (inactive by default; GridLayoutGroup — nested within UICanvas.prefab)
│       └── ItemDetailPanel   (inactive by default; ItemDetailPanelUI — nested within UICanvas.prefab)
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
- No Rigidbody on player — `CharacterController` only
- Camera-relative movement uses `Camera.main` cached in `Awake()` as `_mainCamera`
- `PlayerAnimator` reads `CharacterController.velocity` passively for movement — never writes to movement state

---

## Enemy Prefab Structure (Enemy_Grunt)

```
Enemy_Grunt.prefab  (Assets/_Game/Prefabs/Enemies/)
├── NavMeshAgent, EnemyBrain, PersistentID, EnemyHealth   ← all on ROOT
└── Visual  (child)
    └── MeshFilter, CapsuleCollider, MeshRenderer         ← collider is on CHILD
```

**Consequence for hit detection:** `Physics.OverlapSphereNonAlloc` returns the `CapsuleCollider` on `Visual`. Use `GetComponentInParent<EnemyHealth>()` — NOT `TryGetComponent` — to walk up to the root. `TryGetComponent` only looks at the collider's own GameObject and will always miss.

---

## Prefab Layer Rules

| Prefab type | Required layer | Why |
|-------------|---------------|-----|
| Interactable (pickup, NPC, etc.) | **Interactable (Layer 8)** | `InteractionSystem` raycasts only against Layer 8 — prefab is invisible to interaction without it |

Always set the layer on the **root** GameObject of the prefab, not just a child.

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
