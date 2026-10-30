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
  (below), not by typing values blind.
- **Null archetype** → prefab poses are used unchanged and the weapon sheathes on `UndrawnWeaponSocket` (legacy path).
- **Drawn hand:** `drawnHand = Left` → `LeftWeaponSocket` under `mixamorig:LeftHand` (mirror of the right socket offset).
  Left asked for but `_leftWeaponSocket` unassigned → right hand + one warning (`ResolveDrawnSocket`).
- **Back socket:** `BackWeaponSocket` under `mixamorig:Spine2` (placement `(0, 0.05, -0.15)` is a first guess;
  tune it when the first Back weapon exists). If the archetype asks for Back but the socket is unassigned,
  `EquipmentVisuals` warns once and falls back to the hip.
- Current archetypes: `Archetype_OneHandedSword`, `Archetype_OneHandedAxe` (the axe reuses `Sword_AnimatorOverride`),
  `Archetype_Bow` (**Left** hand; Back socket; drawn pose derived from `Aim Overdraw` at 50 % — belly (+Z) along the
  right-hand → left-hand line, limbs (+Y) along UpperChest up — sheathed pose a guess, tune in play mode;
  `Bow_AnimatorOverride` swaps only the draw / sheathe clips), `Archetype_TwoHanded` (Right hand, **Back** socket,
  `TwoHanded_AnimatorOverride`, 2 combo steps, stance 45 + knockdown, `attackStaminaMultiplier` 2; drawn pose = edge
  reference + fallback (two-hand grip below); sheathed pose = hilt above the right shoulder, blade down-left).
- **Two-hand grip (`twoHandedGrip`, spec `spec-two-hand-grip-constraint.md`):** the Mixamo 2H clips don't keep a constant
  hand relationship (hand line drifts ~50° within one swing; `2hand blocking` holds the other way round), so no rigid
  `drawnPose` fits. `TwoHandGripConstraint` (Player root, `[DefaultExecutionOrder(250)]`, after `BowAimCorrection` 200)
  re-aims `Drawn` every frame while drawn: grip on `DrawnSocket`, blade (+Y) = left socket → right socket line, edge (+Z) =
  `drawnPose`'s edge projected ⊥ blade then rolled `gripRoll`°, moved `gripSlide` m along the blade (`TwoHandGripMath`,
  tested). The result is **blended** with the rigid `drawnPose` by `GripWeight`: 1 for hands 0.10–0.40 m apart (measured
  clips: 0.19–0.35), easing to 0 as the left hand lets go (0.55 m — sheathe, knockdown, death) or the solve degenerates
  (hands < 5 cm, edge reference near the blade), so the hand-off never pops. `drawnPose` matters for its **edge
  direction** and as that rigid pose. No arm IK: the clips place the hands. `drawnHand = Left` + `twoHandedGrip` is
  unsupported (warns once). `WeaponHitbox` runs at order **300** so its sweep sees the re-aimed blade.
- **Flamberge** (`TwoHanded/Flamberge/`): `Drawn` box trimmed to the blade (centre `(0, 0.91, 0)`, size `(0.03, 1.32, 0.08)`).
- **Bows** (`Bow/HuntingBow/`): `Drawn` has **no** `WeaponHitbox` / shape collider — `PlayerCombat` never melee-swings a `BowSO`.
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

**Always verify grip and edge direction afterwards** with the Weapon Pose Tuner. The fitted hitbox
covers the whole mesh — shrink it by hand for head-only hitboxes (e.g. the Axe keeps a head-only box).

---

## Weapon Pose Tuner (`Tools/Combat/Weapon Pose Tuner`)

`Editor/WeaponPoseTunerWindow.cs`, edit mode only. Select the Player (scene or Prefab Mode) → **Use Selection**, pick a
`WeaponSO`, mode **Drawn / Sheathed**, a clip (the weapon's override clips, or any clip in **Clip Override**), scrub frames.
The `_Visual` is previewed (`HideFlags.DontSave`) on the socket `EquipmentVisuals` would use, with the archetype poses; in
Drawn mode the `Drawn` hitbox box is drawn in orange.

- **Select Pose Node** → move / rotate `Drawn` / `Sheathed` → **Save Pose to Archetype** (whole family; saved in socket
  space from world, so moving the preview root is harmless; Undo-able).
- **Select Grip (Mesh)** → **Save Grip to Weapon** writes that local pose to **both** `Drawn/Mesh` and `Sheathed/Mesh` of
  the `_Visual` prefab (saved in place, GUID kept; **not Undo-able**, refused while that prefab is open in Prefab Mode).
  **Revert** rebuilds the preview from the assets.
- Save Pose is disabled while the preview sits on a fallback socket (Left / Back asked but unassigned). The sampled pose
  is stopped and the rig restored before any scene / prefab save, then re-sampled. Edit mode only.
- **Two-hand grip archetype, Drawn mode:** the preview is re-solved like the runtime (after each sample and every
  repaint); Save Pose / Select Pose Node are disabled — edit `gripRoll` / `gripSlide` on the archetype and watch the
  preview. Grip (Mesh) and the Sheathed pose stay tunable as usual.
- **After tuning in the Player's Prefab Mode, discard the stage changes** — only the archetype / weapon prefab are meant to
  change. Saving the Player stage writes Cinemachine's edit-mode camera position and UI layout values into `Player.prefab`.

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
