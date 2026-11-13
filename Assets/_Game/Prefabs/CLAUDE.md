# CLAUDE.md — Assets/_Game/Prefabs

> Loaded when Claude accesses files in this folder. Cross-cutting layer rules; each prefab family has
> its own CLAUDE.md.

---

## Sub-folder Index

| Folder | Covers |
|--------|--------|
| `Player/` | Player prefab hierarchy, camera/audio ownership, sockets, hurtbox |
| `UI/` | `UICanvas` hierarchy, EventSystem placement, nested-panel wiring |
| `Characters/` | Humanoid model prefabs (`Humanoid_BaseHero` — NPC body), documented in `Entities/CLAUDE.md` |
| `Entities/` | `Entity_base`, `NPC_base Variant`, variant-editing gotchas |
| `Entities/Monsters/` | Monster hierarchy, hit-detection contract, monster attack recipe |
| `Items/` | World-item (drop / pickup) prefab requirements |
| `Items/Weapons/` | `_World` / `_Visual`, `Drawn` / `Sheathed`, normalized frame, archetypes, Weapon Creator |
| `Harvestables/` | Fixed world resources (plants…) — contract below |
| `World/CookingStations/` | Cooking stations (multi-look) — contract below |
| `World/SmithingStations/` | Smithing stations — variants of `CookingStation_Base` (contract below) |
| `World/Beds/` | Beds (multi-look) — contract below |
| `World/Water_Volume` | Lakes, rivers: swimmable water — contract below |
| `World/Tent_Graybox` | Placeholder worker tent (BanditVillage tarp, sacks, log). All camp tents are instances, so real art is one prefab swap |

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

## Water volumes (`World/Water_Volume`)

Root: layer `Water`, trigger `BoxCollider` (1×1×1), `NavMeshModifierVolume` (Not Walkable). Child `Surface`: quad at the
top face, `Game/Water` material, `NavMeshModifier` ignore. **Resize with the root scale only.** The top of the box is the
swim surface, so it must match the visible water. The box must reach below the basin floor and cover the whole basin:
leaving its sides ends the swim at once and the player falls.

New lake recipe:
1. Dig the basin below the top of the box. In `StartingTown` the valley water is one volume with its top at y = −2, so add a new
   lake to `layout.json` and `Area1TerrainBuilder` instead of hand-digging: the next build overwrites hand edits (`Scenes/CLAUDE.md`).
2. Keep one shoreline at a walkable slope (`CharacterController.slopeLimit` 45°): there is no climb-out animation, so a
   steep bank traps the player.
3. Rebake the NavMesh. Scene bakes collect render meshes: the prefab keeps the surface out and marks the box unwalkable, but
   only after a rebake.

`StartingTown` has one instance scaled to the whole valley, surface y −2, with its Not Walkable volume split around the ford.

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
  long axis = local X). Instance `Bed` in StartingTown beside the player's tent.
