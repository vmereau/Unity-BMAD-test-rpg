# CLAUDE.md — Assets/_Game/Prefabs

> Loaded when Claude accesses files in this folder. Cross-cutting layer rules; each prefab family has
> its own CLAUDE.md.

---

## Sub-folder Index

| Folder | Covers |
|--------|--------|
| `Player/` | Player prefab hierarchy, camera/audio ownership, sockets, hurtbox |
| `UI/` | `UICanvas` hierarchy, EventSystem placement, nested-panel wiring |
| `Entities/` | `Entity_base`, `NPC_base Variant`, variant-editing gotchas |
| `Entities/Monsters/` | Monster hierarchy, hit-detection contract, monster attack recipe |
| `Items/` | World-item (drop / pickup) prefab requirements |
| `Items/Weapons/` | `_World` / `_Visual`, `Drawn` / `Sheathed`, normalized frame, archetypes, Weapon Creator |

---

## Layer Rules

| Layer | Used for | Who queries it |
|-------|----------|----------------|
| **Characters (6)** | Root of every entity | `LockOnSystem` (`m_Bits: 64`) |
| **CharacterHitbox (7)** | Hurtboxes: trigger collider on a child (`Hitbox` / `HitBox`) | `WeaponHitbox` sweeps (`QueryTriggerInteraction.Collide`) |
| **Interactable (8)** | World item roots, entity `InteractionCollider` children, containers/doors | `InteractionSystem` raycast |
| Default (0) | Attacker-side `WeaponHitbox` GOs, weapon `_Visual` prefabs | — (explicit-mask queries) |

- Hurtboxes and interaction colliders are **triggers** so they never block movement or ragdolls.
- `WeaponHitbox` GOs need no Rigidbody, and anything under a ragdoll Animator must not have one.
