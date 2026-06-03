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
├── HumanoidAnimationBridge.cs  (Game.Animations — owns all Animator.Set* calls)
├── PlayerAnimationDriver.cs    (reads CharacterController velocity, drives HumanoidAnimationBridge)
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
- `PlayerAnimationDriver` reads `CharacterController.velocity` passively for movement — never writes to movement state

---

## Enemy Prefab Structure

> See `Assets/_Game/Prefabs/Enemies/CLAUDE.md` for full enemy prefab hierarchy, physics hit detection requirements, and checklist for adding new enemy types.

---

## Prefab Layer Rules

| Prefab type | Required layer | Why |
|-------------|---------------|-----|
| World item pickup | **Interactable (Layer 8)** on root | Root has the collider; `InteractionSystem` raycasts only against Layer 8 |
| **Any entity (`Entity_base`)** | **Characters (Layer 6)** on root + **Interactable (Layer 8)** on `InteractionCollider` child | The interactable surface now lives on the **base** prefab: `Entity_base` carries `EntityPresence` (`Game.World.IInteractable`) on the root plus a Layer-8 `InteractionCollider` child (trigger CapsuleCollider, Radius 0.5 / Height 2 / Center Y 1). **Every** entity (NPC and monster) is therefore interactable + UI-visible by default. Root stays Layer 6 so `LockOnSystem._lockOnLayerMask (m_Bits:64)` detects it; `GetComponentInParent<IInteractable>()` climbs from the Layer-8 child to `EntityPresence` (or its subclass) on the root. |
| NPC (`Entity_base` variant) | inherits the above | NPCs add `NPCPresence : EntityPresence` for dialogue — see the variant gotcha below (the inherited base `EntityPresence` + collider must be removed). |
| Monster (`Entity_base` variant) | inherits the above | Monsters inherit `EntityPresence` + the Layer-8 collider unchanged → name/HP UI on hover, no `[E]` prompt (`CanInteract == false`, `Interact()` no-op). A future loot spec will subclass/extend `EntityPresence` for corpses. |

**Subclass-on-a-variant gotcha (base concrete component + subclass variant):** when a variant needs to *replace* an inherited base component with its own subclass (here `NPCPresence : EntityPresence`), Unity cannot re-type the inherited component. The variant must:
- add the inherited base component's `fileID` to `m_RemovedComponents` (so the root keeps exactly **one** `IInteractable` — the subclass), and
- if the base also owns a shared child the subclass needs its own copy of, add the inherited child GameObject's `fileID` to `m_RemovedGameObjects` and keep the variant's own added child.

`NPC_base Variant` does both: it removes the inherited base `EntityPresence` + base `InteractionCollider`, and keeps its own added `NPCPresence` + hip-pinned `InteractionCollider` (referenced by `HumanoidAIAnimationDriver._transformsToPinToHips` — do **not** delete it).

**`InventorySystem` lives on the `Entity_base` root** (empty `_startingItems`, enabled). Every entity — NPC and monster — therefore inherits exactly one `InventorySystem`. The NPC variant **no longer adds its own** (it inherits the base one); monsters inherit an empty one as groundwork for future looting. The shopkeeper's stock is a **scene-instance `_startingItems` override** on the inherited component, not a prefab edit. `GoldSystem` is **not** on the base — it stays an added component on `NPC_base Variant` only (`_startingGold: 500`).

**Migration gotcha — moving a variant-added component down to the base:** when you relocate a component from a variant's `m_AddedComponents` onto the shared base, you must (a) delete the variant's added copy (otherwise the root ends up with two and `GetComponent<T>()` may resolve the wrong one), and (b) **retarget any scene `m_Modifications`** that referenced the old added-component `fileID`. The inherited base component gets a **fresh Unity-generated variant-local stripped `fileID`** (e.g. the moved `InventorySystem` became `76150843049530146` on `NPC_base Variant`, *not* the base `fileID` `7887146153611111599` and *not* the old added `fileID` `-8669163291337827286`) — so the only reliable way to re-author the override is to **set the value through the Editor on the scene instance** and let Unity write the correct target. Removing the added component first orphans the scene override (the Editor drops it on reload), so capture the data, then restore it on the inherited component. A hand-edited fileID will silently break the override.

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
