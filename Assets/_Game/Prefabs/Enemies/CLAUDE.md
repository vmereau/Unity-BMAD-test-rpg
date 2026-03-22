# CLAUDE.md — Assets/_Game/Prefabs/Enemies

> Loaded when Claude accesses files in this folder. Covers enemy prefab structure requirements for physics-based hit detection.

---

## Enemy Prefab Structure

### Current Enemy: Enemy_Grunt

```
Enemy_Grunt.prefab  (Assets/_Game/Prefabs/Enemies/)
├── NavMeshAgent, EnemyBrain, PersistentID, EnemyHealth   ← all on ROOT
└── Visual  (child)
    └── MeshFilter, CapsuleCollider, MeshRenderer         ← collider is on CHILD
```

The **CapsuleCollider** on the `Visual` child is the hit surface that weapon trigger colliders interact with.

**Consequence for hit detection:** `Physics.OverlapSphereNonAlloc` returns the `CapsuleCollider` on `Visual`. Use `GetComponentInParent<EnemyHealth>()` — NOT `TryGetComponent` — to walk up to the root. `TryGetComponent` only looks at the collider's own GameObject and will always miss.

---

## Physics Requirements for WeaponHitbox Detection

`WeaponHitbox.OnTriggerEnter` fires when the weapon's trigger collider overlaps an enemy collider. Unity physics requires **at least one `Rigidbody`** in the collision pair for `OnTriggerEnter` to fire.

| Weapon has Rigidbody? | Enemy has Rigidbody? | OnTriggerEnter fires? |
|---|---|---|
| Yes (kinematic) | No (static) | **YES** |
| No (static) | No (static) | NO |
| No (static) | Yes | YES |
| Yes | Yes | YES |

**Rule:** Enemy colliders do **not** need a `Rigidbody` — the kinematic `Rigidbody` on the weapon prefab root satisfies the requirement.

**Do NOT add a Rigidbody to enemy prefabs** just to make hit detection work — the weapon prefab is responsible for providing the Rigidbody.

---

## Weapon Prefab Requirements (set on weapon, not enemy)

Every weapon visual prefab that uses `WeaponHitbox` must have:
1. **Kinematic `Rigidbody` on the prefab root** (`isKinematic=true`, `useGravity=false`) — required for trigger detection
2. **`WeaponHitbox` component on the mesh child GO** (e.g. `SM_Sword_1`), not the root
3. **BoxCollider (or other collider) on the mesh child GO** with `isTrigger=true`

See `Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab` as the reference implementation.

---

## EnemyHealth Lookup

`WeaponHitbox.OnTriggerEnter` uses `other.GetComponentInParent<EnemyHealth>()` to find the health component. This traverses up from the collided collider's GO. **EnemyHealth must be reachable via `GetComponentInParent` from the collider GO** — placing it on the root or any ancestor of the `Visual` child works.

If EnemyHealth is placed on a sibling or unrelated GO, hits will silently be ignored.

---

## Adding New Enemy Types

Checklist when creating a new enemy prefab:

- [ ] Collider on a child GO that represents the body (CapsuleCollider recommended)
- [ ] Collider is **non-trigger** (trigger colliders won't receive `OnTriggerEnter` from weapon triggers in the same layer)
- [ ] `EnemyHealth` component is on the root GO or any ancestor of the collider GO
- [ ] Enemy GO (or its collider child) is on the **Enemy** layer
- [ ] No `Rigidbody` needed — weapon provides it
