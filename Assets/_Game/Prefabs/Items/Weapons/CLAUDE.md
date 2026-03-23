# CLAUDE.md — Assets/_Game/Prefabs/Items/Weapons

> Loaded when Claude accesses weapon prefab files. Covers the two-prefab convention, Drawn/Sheathed child convention, hitbox requirements, and SO wiring.

---

## Two-Prefab Convention

Each weapon lives in its own folder and ships as **two prefabs**:

```
Swords/SwordBase/
├── SwordBase_World.prefab    ← dropped item (Rigidbody, ItemPickup, solid BoxCollider, Layer: Interactable)
└── SwordBase_Visual.prefab   ← equipped visual (kinematic Rigidbody on root, no ItemPickup, Layer: Default)
    ├── Drawn                 ← combat state: WeaponHitbox + trigger collider, positioned for WeaponSocket (hand)
    └── Sheathed              ← sheathed state: visuals only, positioned for UndrawnWeaponSocket (hip)
```

**SO wiring:**
- `ItemSO.worldItemPrefab` → `_World` prefab
- `EquipableItemSO.equipVisualPrefab` → `_Visual` prefab

**Why two prefabs:** `_World` needs physics + interaction; `_Visual` needs neither. Assigning `_World` to `equipVisualPrefab` causes the sword to fall to the floor and be re-pickable when equipped.

---

## Drawn / Sheathed Child Convention (Story 7.12)

`EquipmentVisuals.ApplyCombatVisibility()` looks for children named **exactly** `Drawn` and `Sheathed` on the weapon visual root and toggles their `SetActive` state based on `IsInCombat`.

| State | Root parented to | `Drawn` | `Sheathed` |
|-------|-----------------|---------|------------|
| Drawn (IsInCombat = true) | `WeaponSocket` (hand) | active | inactive |
| Sheathed (IsInCombat = false) | `UndrawnWeaponSocket` (hip) | inactive | active |

**Rules:**
- `Drawn` child: contains the mesh at the grip orientation for the hand + `WeaponHitbox` component + trigger `BoxCollider`
- `Sheathed` child: contains the mesh (or a subset) at the hip scabbard orientation — visuals only, no hitbox/collider
- The root's `localPosition` and `localRotation` are always reset to `(0,0,0)` / `identity` on socket attach — all visual offset must live in the children's local transforms
- If either child is absent, `ApplyCombatVisibility` silently no-ops — both stay visible (safe fallback for weapons not yet updated or placeholder cubes)
- **Child names are case-sensitive and exact** — `drawn`, `DRAWN`, `DrawnWeapon` will all silently fail

---

## Kinematic Rigidbody on Visual Root (Story 7.9)

Required for `WeaponHitbox.OnTriggerEnter` to fire. Unity does not generate trigger events between two static colliders. The weapon trigger on a `CharacterController` child is static; `Enemy_Grunt/Visual`'s CapsuleCollider is also static. Adding `isKinematic=true, useGravity=false` Rigidbody to the `_Visual` root satisfies the requirement without affecting movement.

**Every weapon visual prefab that uses `WeaponHitbox` must have a kinematic Rigidbody on its root.**

---

## Trigger Collider Placement

`BoxCollider` (`isTrigger=true`) goes on the **`Drawn` child GO** (where `WeaponHitbox.cs` also lives). Do NOT place it on the visual root. `GetComponentInChildren<WeaponHitbox>()` from `ActiveWeaponGO` (the root) finds it regardless of depth.

Note: nested prefab children can't be reparented under a stripped transform via YAML — keep the collider as a component on `Drawn`, not in a separate sub-prefab.

---

## Grip Alignment

The `_Visual` root is the grip anchor (placed at the socket with `localPosition = Vector3.zero, localRotation = identity`). Offset the `Drawn` child's `localPosition`/`localRotation` to align the blade correctly in the hand. Offset the `Sheathed` child's `localPosition`/`localRotation` to align it correctly on the hip. Never hardcode per-weapon offsets in `EquipmentVisuals.cs`.

---

## GUID Note — SwordBase_Visual.prefab

The `.meta` GUID for `SwordBase_Visual.prefab` is manually crafted (`d5e6f7a8b9c0d1e2f3a4b5c6d7e8f901`) so it could be typed directly into `Weapon_TestSword.asset` YAML. **Never delete this `.meta` file** — Unity would regenerate a random GUID and silently break the `equipVisualPrefab` reference (weapon shows placeholder cube with no error).

Future weapons should let Unity auto-generate their `.meta` GUID, then copy it into the `.asset` YAML.

---

## Code Review Checklist — Weapon Prefabs

| Severity | Pattern |
|----------|---------|
| HIGH | `_Visual` prefab root missing kinematic Rigidbody — `WeaponHitbox.OnTriggerEnter` will never fire |
| HIGH | `Drawn` or `Sheathed` child named incorrectly (wrong case, extra suffix) — `ApplyCombatVisibility` silently no-ops, both children stay visible simultaneously |
| HIGH | `WeaponHitbox` + trigger collider placed on `Sheathed` child — hitbox would be active on hip socket instead of hand |
| MEDIUM | `Drawn` child absent but `Sheathed` present (or vice versa) — one state will have both children visible |
| MEDIUM | Visual offsets hardcoded in `EquipmentVisuals.cs` instead of baked into `Drawn`/`Sheathed` local transforms |
| LOW | `_World` prefab assigned to `equipVisualPrefab` instead of `_Visual` — weapon falls to floor on equip |
