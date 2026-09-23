# CLAUDE.md — Assets/_Game/Prefabs/Entities/Monsters

> Loaded when Claude accesses files in this folder. Covers monster prefab structure requirements for physics-based hit detection.

---

## Monster Prefab Structure

### Current Monster: Monster_DarknessSpider

```
Monster_DarknessSpider Variant.prefab  (variant of Assets/_Game/Prefabs/Entities/Entity_base.prefab)
├── NavMeshAgent, EntityBrain, EntityHealth, PersistentID, EntityPresence, InventorySystem  ← ROOT (Layer 6 Characters)
├── Visual                ← model
├── InteractionCollider   ← trigger, Layer 8 Interactable (inherited from Entity_base)
├── EntityUICanvas        ← name / HP world-space UI (inherited from Entity_base)
└── HitBox                ← non-trigger collider, Layer 7 CharacterHitbox — the weapon hit surface
```

The collider on the `HitBox` child is the surface that weapon trigger colliders interact with.

**Consequence for hit detection:** the collider found is on a child. Use `GetComponentInParent<EntityHealth>()` — NOT `TryGetComponent` — to walk up to the root. `TryGetComponent` only looks at the collider's own GameObject and will always miss.

---

## Physics Requirements for WeaponHitbox Detection

`WeaponHitbox.OnTriggerEnter` fires when the weapon's trigger collider overlaps an entity collider. Unity physics requires **at least one `Rigidbody`** in the collision pair for `OnTriggerEnter` to fire.

| Weapon has Rigidbody? | Entity has Rigidbody? | OnTriggerEnter fires? |
|---|---|---|
| Yes (kinematic) | No (static) | **YES** |
| No (static) | No (static) | NO |
| No (static) | Yes | YES |
| Yes | Yes | YES |

**Rule:** Entity colliders do **not** need a `Rigidbody` — the kinematic `Rigidbody` on the weapon prefab root satisfies the requirement.

**Do NOT add a Rigidbody to entity prefabs** just to make hit detection work — the weapon prefab is responsible for providing the Rigidbody.

---

## Weapon Prefab Requirements (set on weapon, not target)

Every weapon visual prefab that uses `WeaponHitbox` must have:
1. **Kinematic `Rigidbody` on the prefab root** (`isKinematic=true`, `useGravity=false`) — required for trigger detection
2. **`WeaponHitbox` component on the mesh child GO** (e.g. `SM_Sword_1`), not the root
3. **BoxCollider (or other collider) on the mesh child GO** with `isTrigger=true`

See `Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab` as the reference implementation.

---

## EntityHealth Lookup

`WeaponHitbox.OnTriggerEnter` uses `other.GetComponentInParent<EntityHealth>()` to find the health component. This traverses up from the collided collider's GO. **EntityHealth must be reachable via `GetComponentInParent` from the collider GO** — placing it on the root or any ancestor of the `HitBox` child works.

If EntityHealth is placed on a sibling or unrelated GO, hits will silently be ignored.

---

## Adding New Monster Types

Checklist when creating a new monster prefab (as a variant of `Entity_base`):

- [ ] Collider on a child GO that represents the body (CapsuleCollider recommended)
- [ ] Collider is **non-trigger** (trigger colliders won't receive `OnTriggerEnter` from weapon triggers in the same layer)
- [ ] `EntityHealth` component is on the root GO or any ancestor of the collider GO
- [ ] Hit collider child is on the **CharacterHitbox (Layer 7)** layer; root stays on **Characters (Layer 6)**
- [ ] No `Rigidbody` needed — weapon provides it
