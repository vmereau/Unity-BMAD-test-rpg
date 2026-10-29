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
| `Harvestables/` | Fixed world resources (plants…) — contract below |
| `World/CookingStations/` | Cooking stations (multi-look) — contract below |
| `World/SmithingStations/` | Smithing stations — variants of `CookingStation_Base` (contract below) |
| `World/Beds/` | Beds (multi-look) — contract below |

---

## Layer Rules

| Layer | Used for | Who queries it |
|-------|----------|----------------|
| **Characters (6)** | Root of every entity | `LockOnSystem` (`m_Bits: 64`) |
| **CharacterHitbox (7)** | Hurtboxes: trigger collider on a child (`Hitbox` / `HitBox`) | `WeaponHitbox` sweeps (`QueryTriggerInteraction.Collide`) |
| **Interactable (8)** | World item roots, entity `InteractionCollider` children, containers/doors | `InteractionSystem` raycast |
| **Harvestable (9)** | Harvestable roots (plants, …) | `InteractionSystem` raycast (Player `_raycastMask` = 768: layers 8 + 9) |
| Default (0) | Attacker-side `WeaponHitbox` GOs, weapon `_Visual` prefabs | — (explicit-mask queries) |

- Hurtboxes and interaction colliders are **triggers** so they never block movement or ragdolls.
- `WeaponHitbox` GOs need no Rigidbody, and anything under a ragdoll Animator must not have one.

---

## Harvestables (`Harvestables/`)

- New type = **prefab variant of `Harvestable_Base`** (root: layer Harvestable, `Harvestable`, trigger
  `BoxCollider`, `InteractionHighlight`, `SaveableObject` with empty id) + the model as a nested child, collider
  refit to the model, `Harvestable._item` set. No Rigidbody — plants hold their place.
- The yield `ItemSO.worldItemPrefab` is a **separate** drop prefab in `Items/` (variant of `Base Item`, Rigidbody,
  layer 8) — never the harvestable itself. `Item_HealingPlant` is a placeholder (scaled carrot mesh).
- Scene instances need a save ID — re-run `Tools/Save/Validate Save IDs` after placing **or duplicating**
  (Ctrl+D copies the ID; harvesting one would remove both on load); add `Ownership` for a garden plant (→ "Steal").
- `Tools/Items/Generate Missing Icons` renders a yield item's icon from its harvestable prefab (real model), not
  from the placeholder drop.

## Cooking stations (`World/CookingStations/`)

- New look = **prefab variant of `CookingStation_Base`** (root: layer Interactable (8), `CookingStation`, trigger
  `BoxCollider`, `InteractionHighlight`; empty `Model` child) with the model nested under `Model` and the root
  collider refit to it. The model keeps its own solid collider (Default layer) for physical blocking.
- `_recipes` is set on the base, so every look inherits it; override it on a variant only for a different station
  type. Stations are stateless — no `SaveableObject` / save ID.
- `CookingStation_Fireplace` = `SM_Fireplace2` (Hivemind, URP-compatible materials).
- Smithing: `SmithingStation_Anvil` (`World/SmithingStations/`) = variant of `CookingStation_Base` overriding `_kind`
  Smithing, `_requiredSkill`, prompt / name and `_recipes`; `SM_Anvil` (Hivemind BanditVillage) lifted so its base sits
  at y 0 (pivot is mid-mesh). Behaviour: `Scripts/Crafting/CLAUDE.md`.

## Beds (`World/Beds/`)

- Same multi-look contract as cooking stations: new look = **prefab variant of `Bed_Base`** (root: layer Interactable (8),
  `Bed`, trigger `BoxCollider`, `InteractionHighlight`; empty `Model` child — never put a model on the base) with the
  model prefab nested under `Model` and the root collider refit to its bounds. The model keeps its own solid colliders.
- `_onSleepRequested` (`OnBedSleepRequested`) is set on the base, so every look inherits it. Beds are stateless — no
  `SaveableObject` / save ID.
- `Bed_WoodBed_01a` = HouseForge `SM_WoodBed_01a` (in the `HDRP (Default)` folder, but `S_MasterORM` has a URP target;
  long axis = local X). Instance `Bed` in StartingTown next to the fireplace.
