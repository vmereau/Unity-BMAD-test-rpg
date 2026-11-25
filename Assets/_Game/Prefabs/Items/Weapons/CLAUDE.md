# CLAUDE.md — Assets/_Game/Prefabs/Items/Weapons

> Loaded when Claude accesses weapon prefab files. Covers the two-prefab convention, Drawn/Sheathed child convention, normalized weapon frame, archetypes, hitbox requirements, and SO wiring.

---

## Topic files

| File | Read when |
|------|-----------|
| `WEAPON_TOOLS.md` | Creating a weapon (`Tools/Items/Weapon Creator`) or tuning grip / archetype poses (`Tools/Combat/Weapon Pose Tuner`) |

---

## Two-Prefab Convention

Each weapon lives in its own folder and ships as **two prefabs**. The Weapon Creator writes `{Family}/{Name}/`
(`WEAPON_TOOLS.md`); the `Swords/SwordBase/` example is a legacy hand-made folder kept for its GUID (see GUID Note):

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

## Drawn / Sheathed Child Convention

`EquipmentVisuals.ApplyCombatVisibility()` looks for children named **exactly** `Drawn` and `Sheathed` on the weapon visual root and toggles their `SetActive` state based on `IsInCombat`.

| State | Root parented to | `Drawn` | `Sheathed` |
|-------|-----------------|---------|------------|
| Drawn (IsInCombat = true) | `WeaponSocket` (right hand) or `LeftWeaponSocket` (archetype `drawnHand = Left`) | active | inactive |
| Sheathed (IsInCombat = false) | `UndrawnWeaponSocket` (hip) or `BackWeaponSocket` (archetype `sheathSocket = Back`) | inactive | active |

**Rules:**
- `Drawn` child: contains the mesh at the grip orientation for the hand + `WeaponHitbox` component + **disabled** `BoxCollider` (sweep shape)
- `Sheathed` child: contains the mesh (or a subset) at the hip scabbard orientation — visuals only, no hitbox/collider
- The root's `localPosition` and `localRotation` are always reset to `(0,0,0)` / `identity` on socket attach — the `Drawn`/`Sheathed` poses come from the weapon's archetype (see below), per-mesh correction lives in the `Mesh` child
- If either child is absent, `ApplyCombatVisibility` silently no-ops — both stay visible (safe fallback for weapons not yet updated or placeholder cubes)
- **Child names are case-sensitive and exact** — `drawn`, `DRAWN`, `DrawnWeapon` will all silently fail

---

## Hitbox Shape Collider

The sweep-shape `BoxCollider` sits on the **`Drawn` child GO** itself (same GO as `WeaponHitbox`) — do NOT place it
on the visual root or on a sub-child. Per-weapon reach tuning = the `WeaponHitbox` `_reachPadding` / `_verticalReach`
fields on `Drawn`. Shape rules (disabled, Box/Sphere/Capsule, no Rigidbody, no layer requirement):
`Scripts/Combat/CLAUDE.md` → WeaponHitbox; lookup via `GetComponentInChildren<WeaponHitbox>(true)`: same file →
PlayerCombat Hitbox Binding.

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
| `SM_Flamberge` | `(0, -0.05, 0)` | `NegY` | `NegX` | 1.90 m; blade y −1.62..−0.30 (±0.03 X), guard −0.20..−0.10 (±0.18 X), handle −0.10..0.20; the right hand sits just under the guard |
| `SM_WeaponBow_01a` | `(0, 0, 0)` | `PosY` | `PosZ` | recurve, limbs ±0.65 Y; "edge" = belly / target side (+Z), string and tips at −Z |

Per-weapon fine-tuning (a slightly different grip) goes into the `Mesh` child, never into `Drawn`/`Sheathed`.

---

## Weapon Archetypes (Grip Alignment)

`WeaponSO.archetype` → `WeaponArchetypeSO` (`Data/Items/Weapons/Archetypes/`) holds the `drawnHand` (Right / Left),
the `drawnPose` (in that hand socket's space) / `sheathedPose`, the `sheathSocket` (Hip / Back), the family's animator override and default combo steps.
`EquipmentVisuals.RefreshWeapon` instantiates the `_Visual` prefab and calls
`ApplyArchetypePoses(visual, archetype)`, which overwrites `Drawn` / `Sheathed` local transforms at runtime.

- **Once a weapon has an archetype, editing its prefab's `Drawn`/`Sheathed` transforms has no in-game effect.**
  Tune the archetype (affects the whole family) or the `Mesh` child (this weapon only) — with the **Weapon Pose Tuner**
  (`WEAPON_TOOLS.md`), not by typing values blind.
- **Null archetype** → prefab poses are used unchanged and the weapon sheathes on `UndrawnWeaponSocket` (legacy path).
- **Drawn hand:** `drawnHand = Left` → `LeftWeaponSocket` under `mixamorig:LeftHand` (mirror of the right socket offset).
  Left asked for but `_leftWeaponSocket` unassigned → right hand + one warning (`ResolveDrawnSocket`).
- **Back socket:** `BackWeaponSocket` under `mixamorig:Spine2` (placement `(0, 0.05, -0.15)` is a first guess;
  tune it when the first Back weapon exists). If the archetype asks for Back but the socket is unassigned,
  `EquipmentVisuals` warns once and falls back to the hip.
- Current archetypes: `Archetype_OneHandedSword`, `Archetype_OneHandedAxe` (the axe reuses `Sword_AnimatorOverride`),
  `Archetype_Bow` (**Left** hand; Back socket; `Bow_AnimatorOverride` swaps the draw / sheathe clips and the combat idle; specs
  `spec-bow-hand-grip.md`, `spec-bow-idle-compensated-bake.md`). **Drawn pose = grip, not aim:** the handle (Drawn y ±0.075, z 0.065–0.107) lies across the
  left palm at 30° toward the fingers, its string-side face on the palm (handle centre 4.6 cm out along the palm normal), and
  `BowGrip` fingers close over it. The bow's aim orientation comes from the wrist offsets in the baked bow clips
  (`BowClipWristBaker`), so **changing `drawnPose` means re-fitting those offsets**. **Sheathed pose = the in-hand pose at
  the Draw Bow `WeaponSwap` frame** (0.30), expressed in `BackWeaponSocket` space: the grab is seamless and the bow hangs
  along the spine. Changing `drawnPose` or the draw clip means re-deriving it (`inverse(Back) × LeftWeaponSocket ×
  drawnPose` at that frame). Sheathe Bow releases at 0.325, where the palm is closest (2 cm; the bow turns ~49° there).
  Both poses were derived on `Humanoid_BaseHero` (v2 body, same sockets as the Player): a body with other proportions
  gets a near-seamless grab, not an exact one. **Carry pose (`hasCarryPose` + `carryPose`) = the idle seat:** while the
  combat idle plays (`Bow Idle.anim`), the Drawn child sits at `carryPose` = **`drawnPose` rolled −95° about the handle
  axis** (Drawn-space line x 0, z 0.0855, along Y), with `BowCarry` fingers. The handle still crosses the palm; the roll
  moves the string from 17 cm outboard of the forearm to between the forearm and the hip (the idle's wrist is turned ~90°
  from the aim clips'). `carryPivot` (0, 0, 0.0855) = the handle centre: `EquipmentVisuals.BlendPose` turns
  the bow about it, so the handle stays in the palm during the 0.15 s seat change. `HandPoseCapture bowidle` reports the
  seat over Idle / Walking / Running legs at 10 / 50 / 90 % (string gap and side, deepest bow vertex inside the body). Aim / nock / fire / draw / sheathe use `drawnPose` (grab and release frames unchanged); `HandPoseDriver`
  blends between them in 0.15 s (`Scripts/Core/Animations/CLAUDE.md`). Changing `drawnPose` means re-deriving
  `carryPose` from it; changing `carryPose` means re-tuning `BowCarry` (`HandPoseCapture bowidle`) and re-checking the body
  clearance over Idle / Walking / Running legs.
- `Archetype_TwoHanded` (Right hand, **Back** socket,
  `TwoHanded_AnimatorOverride`, 2 combo steps, stance 45 + knockdown, `attackStaminaMultiplier` 2; drawn pose = edge
  reference + fallback (two-hand grip below); sheathed pose = hilt above the right shoulder, blade down-left).
- **Two-hand grip (`twoHandedGrip`, spec `spec-two-hand-grip-constraint.md`):** `Drawn` is re-aimed every frame by `TwoHandGripConstraint` — algorithm in `Scripts/Inventory/CLAUDE.md` → TwoHandGripConstraint.
- **Flamberge** (`TwoHanded/Flamberge/`): `Drawn` box trimmed to the blade (centre `(0, 0.91, 0)`, size `(0.03, 1.32, 0.08)`).
- **Bows** (`Bow/HuntingBow/`): `Drawn` has **no** `WeaponHitbox` / shape collider — `PlayerCombat` never melee-swings a `BowSO`.
- Never hardcode per-weapon offsets in `EquipmentVisuals.cs`.

---

## GUID Note — SwordBase_Visual.prefab

The `.meta` GUID for `SwordBase_Visual.prefab` is manually crafted (`d5e6f7a8b9c0d1e2f3a4b5c6d7e8f901`) and
`Weapon_TestSword`, `Weapon_ShortIronSword` and `Weapon_ShortIronSword_Forged` (`equipVisualPrefab`) reference its root GO by fileID `100000000`. **Never delete this `.meta`
file or re-create the prefab** — edit it in place (`PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` on
the same path). Otherwise the reference silently breaks (weapon shows the placeholder cube with no error).

New weapons: use the Weapon Creator — it wires the SO references itself (no YAML editing).

---

## Code Review Checklist — Weapon Prefabs

| Severity | Pattern |
|----------|---------|
| MEDIUM | `_Visual` root still carries a (legacy) Rigidbody, or the `Drawn` shape `BoxCollider` is left enabled — both are dead physics participants |
| HIGH | `Drawn` or `Sheathed` child named incorrectly (wrong case, extra suffix) — `ApplyCombatVisibility` silently no-ops, both children stay visible simultaneously |
| HIGH | `WeaponHitbox` + trigger collider placed on `Sheathed` child — hitbox would be active on hip socket instead of hand |
| HIGH | `Mesh` child not normalized (weapon uses an archetype but the mesh pivot/axes are raw art-pack) — weapon sits offset/rotated in hand and on hip |
| HIGH | Extra collider (e.g. art-pack `MeshCollider`) left under `Drawn`/`Sheathed` — it blocks the CharacterController; only the disabled shape collider on `Drawn` is allowed |
| MEDIUM | `Drawn` child absent but `Sheathed` present (or vice versa) — one state will have both children visible |
| MEDIUM | Hitbox `BoxCollider` left at the default 1×1×1 — hits register far outside the blade |
| MEDIUM | Visual offsets hardcoded in `EquipmentVisuals.cs` instead of the archetype poses / `Mesh` child |
| MEDIUM | Per-weapon pose tweak made on `Drawn`/`Sheathed` of a weapon that has an archetype — overwritten at runtime, no effect |
| LOW | `_World` prefab assigned to `equipVisualPrefab` instead of `_Visual` — weapon falls to floor on equip |
