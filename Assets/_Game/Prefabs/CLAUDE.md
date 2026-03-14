# CLAUDE.md — Assets/_Game/Prefabs

> Loaded when Claude accesses files in this folder. Contains all prefab structure rules and gotchas.

---

## Player Prefab Structure

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF; Controller: PlayerAnimatorController)
├── PlayerController.cs
├── PlayerAnimator.cs
├── PlayerStateManager.cs
├── PlayerCombat.cs
├── DodgeController.cs
├── StaminaSystem.cs
├── CameraTarget         (child, local Y = 1.6 — pure Transform pivot, no components)
└── Character            (child — nested Mixamo FBX prefab, Humanoid rig)
```

- `CameraController` is wired **in `TestScene.unity`** as a scene component — **not** in the prefab (known drift from intended architecture; action item exists to move it)
- Cinemachine `Follow` and `LookAt` → both point to `CameraTarget`
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
