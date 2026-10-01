# CLAUDE.md — Assets/_Game/Prefabs/Items/Weapons

> Loaded when Claude accesses weapon prefab files. Covers the two-prefab convention, Drawn/Sheathed child convention, normalized weapon frame, archetypes, the Weapon Creator, hitbox requirements, and SO wiring.

---

## Two-Prefab Convention

Each weapon lives in its own folder and ships as **two prefabs**:

```
Swords/SwordBase/
├── SwordBase_World.prefab    ← dropped item (Rigidbody, ItemPickup, solid BoxCollider, Layer: Interactable)
└── SwordBase_Visual.prefab   ← equipped visual (no Rigidbody, no ItemPickup, Layer: Default)
    ├── Drawn                 ← combat state: WeaponHitbox + DISABLED BoxCollider (shape only), posed for WeaponSocket (hand)
    │   └── Mesh              ← normalizing transform (grip at origin, blade +Y, edge +Z) → renderer(s)
    └── Sheathed              ← sheathed state: visuals only, posed for the sheath socket (hip / back)
        └── Mesh
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
| Sheathed (IsInCombat = false) | `UndrawnWeaponSocket` (hip) or `BackWeaponSocket` (archetype `sheathSocket = Back`) | inactive | active |

**Rules:**
- `Drawn` child: contains the mesh at the grip orientation for the hand + `WeaponHitbox` component + **disabled** `BoxCollider` (sweep shape)
- `Sheathed` child: contains the mesh (or a subset) at the hip scabbard orientation — visuals only, no hitbox/collider
- The root's `localPosition` and `localRotation` are always reset to `(0,0,0)` / `identity` on socket attach — the `Drawn`/`Sheathed` poses come from the weapon's archetype (see below), per-mesh correction lives in the `Mesh` child
- If either child is absent, `ApplyCombatVisibility` silently no-ops — both stay visible (safe fallback for weapons not yet updated or placeholder cubes)
- **Child names are case-sensitive and exact** — `drawn`, `DRAWN`, `DrawnWeapon` will all silently fail

---

## Hitbox Shape Collider

`WeaponHitbox` sweeps with physics queries (see `Scripts/Combat/CLAUDE.md`) — **no Rigidbody** on the
`_Visual` root and no layer requirement. The `BoxCollider` on the **`Drawn` child GO** (same GO as
`WeaponHitbox`) is only the sweep **shape**: keep it **disabled** (`WeaponHitbox.Awake` also disables it).
Box/Sphere/Capsule only. Do NOT place it on the visual root or on a sub-child. `GetComponentInChildren<WeaponHitbox>(true)`
from `ActiveWeaponGO` (the root) finds it regardless of depth. Per-weapon reach tuning = the
`WeaponHitbox` `_reachPadding` / `_verticalReach` fields on `Drawn`.

Note: nested prefab children can't be reparented under a stripped transform via YAML — keep the collider as a component on `Drawn`, not in a separate sub-prefab.

---

## Normalized Weapon Frame

Every weapon mesh is normalized by the **`Mesh` child** of `Drawn` / `Sheathed` (and nothing else). In the
`Mesh` child's parent space (= `Drawn`/`Sheathed` local space):

- **grip point** at the origin,
- **blade / head** along **+Y**,
- **leading cutting edge** along **+Z**.

The `Mesh` child's local transform converts art-pack mesh space into this frame
(`WeaponGripMath.GetMeshChildLocal(grip, bladeAxis, edgeAxis)`); `WeaponModelUtility.Normalize` applies it.
Because every mesh of a family shares the frame, one pair of `Drawn`/`Sheathed` poses fits them all.

Measured art-pack frames (mesh-local):

| Mesh | Grip | Blade axis | Edge axis | Notes |
|------|------|-----------|-----------|-------|
| `SM_Sword_1` | `(0, 0.35, 0)` | `NegY` | `NegX` | bounds centred at origin, size `0.1455×0.8451×0.0289`; leading edge in the sword clips = −X |
| `SM_Axe_01` | `(0, 0.08, 0)` | `PosY` | `PosZ` | pivot at the handle end; head y≈0.39–0.58, edge z≈0.19 |

Per-weapon fine-tuning (a slightly different grip) goes into the `Mesh` child, never into `Drawn`/`Sheathed`.

---

## Weapon Archetypes (Grip Alignment)

`WeaponSO.archetype` → `WeaponArchetypeSO` (`Data/Items/Weapons/Archetypes/`) holds the `drawnPose` /
`sheathedPose`, the `sheathSocket` (Hip / Back), the family's animator override and default combo steps.
`EquipmentVisuals.RefreshWeapon` instantiates the `_Visual` prefab and calls
`ApplyArchetypePoses(visual, archetype)`, which overwrites `Drawn` / `Sheathed` local transforms at runtime.

- **Once a weapon has an archetype, editing its prefab's `Drawn`/`Sheathed` transforms has no in-game effect.**
  Tune the archetype (affects the whole family) or the `Mesh` child (this weapon only).
- **Null archetype** → prefab poses are used unchanged and the weapon sheathes on `UndrawnWeaponSocket` (legacy path).
- **Back socket:** `BackWeaponSocket` under `mixamorig:Spine2` (placement `(0, 0.05, -0.15)` is a first guess;
  tune it when the first Back weapon exists). If the archetype asks for Back but the socket is unassigned,
  `EquipmentVisuals` warns once and falls back to the hip.
- Current archetypes: `Archetype_OneHandedSword`, `Archetype_OneHandedAxe` (the axe reuses `Sword_AnimatorOverride`).
- Never hardcode per-weapon offsets in `EquipmentVisuals.cs`.

---

## Weapon Creator (`Tools/Items/Weapon Creator`)

Use it for every new weapon instead of hand-building prefabs. Inputs: name, C# type, base model, **archetype**
(required), **grip point** (model local), **blade axis**, **edge axis** (must be on different axes). It:

- strips every `Collider`, `Rigidbody`, `LODGroup` and `_LOD1+` child from the model (keeps LOD0),
- builds `_Visual` (no Rigidbody; root → `Drawn` [**disabled** `BoxCollider` fitted to the renderers + `WeaponHitbox`]
  → `Mesh`; `Sheathed` → `Mesh`), both `Mesh` children normalized, `Drawn`/`Sheathed` left at identity,
- builds `_World` (un-normalized model, root `BoxCollider` fitted to **all** renderers),
- writes `Data/Items/Weapons/{Family}/Weapon_{Name}.asset` and `Prefabs/Items/Weapons/{Family}/{Name}/`
  (`{Family}` = archetype name without `Archetype_`), sets `archetype`, leaves `comboSteps = 0` /
  `animatorOverrideController = null` (inherit) unless edited, and generates the icon.

**Always verify grip and edge direction afterwards** (Play mode or the grip-preview tool). The fitted hitbox
covers the whole mesh — shrink it by hand for head-only hitboxes (e.g. the Axe keeps a head-only box).

---

## GUID Note — SwordBase_Visual.prefab

The `.meta` GUID for `SwordBase_Visual.prefab` is manually crafted (`d5e6f7a8b9c0d1e2f3a4b5c6d7e8f901`) and
`Weapon_TestSword.equipVisualPrefab` references its root GO by fileID `100000000`. **Never delete this `.meta`
file or re-create the prefab** — edit it in place (`PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` on
the same path). Otherwise the reference silently breaks (weapon shows the placeholder cube with no error).

New weapons: use the Weapon Creator — it wires the SO references itself (no YAML editing).

---

## Code Review Checklist — Weapon Prefabs

| Severity | Pattern |
|----------|---------|
| MEDIUM | `_Visual` root still carries a (legacy) Rigidbody, or the `Drawn` shape `BoxCollider` is left enabled — both are dead physics participants since the sweep rework |
| HIGH | `Drawn` or `Sheathed` child named incorrectly (wrong case, extra suffix) — `ApplyCombatVisibility` silently no-ops, both children stay visible simultaneously |
| HIGH | `WeaponHitbox` + trigger collider placed on `Sheathed` child — hitbox would be active on hip socket instead of hand |
| HIGH | `Mesh` child not normalized (weapon uses an archetype but the mesh pivot/axes are raw art-pack) — weapon sits offset/rotated in hand and on hip |
| HIGH | Extra collider (e.g. art-pack `MeshCollider`) left under `Drawn`/`Sheathed` — it blocks the CharacterController; only the disabled shape collider on `Drawn` is allowed |
| MEDIUM | `Drawn` child absent but `Sheathed` present (or vice versa) — one state will have both children visible |
| MEDIUM | Hitbox `BoxCollider` left at the default 1×1×1 — hits register far outside the blade |
| MEDIUM | Visual offsets hardcoded in `EquipmentVisuals.cs` instead of the archetype poses / `Mesh` child |
| MEDIUM | Per-weapon pose tweak made on `Drawn`/`Sheathed` of a weapon that has an archetype — overwritten at runtime, no effect |
| LOW | `_World` prefab assigned to `equipVisualPrefab` instead of `_Visual` — weapon falls to floor on equip |
