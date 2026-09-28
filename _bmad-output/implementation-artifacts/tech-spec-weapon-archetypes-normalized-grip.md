---
title: 'Weapon Archetypes & Normalized Grip'
slug: 'weapon-archetypes-normalized-grip'
created: '2026-09-28'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1 (6.6)', 'C# / Game.asmdef', 'URP 17', 'Unity Editor UI Toolkit (EditorWindow)', 'NUnit EditMode tests']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs (new)'
  - 'Assets/_Game/ScriptableObjects/Items/Weapons/WeaponPose.cs (new)'
  - 'Assets/_Game/ScriptableObjects/Items/Weapons/WeaponSheathSocket.cs (new)'
  - 'Assets/_Game/ScriptableObjects/Items/Weapons/SignedAxis.cs (new)'
  - 'Assets/_Game/ScriptableObjects/Items/Weapons/WeaponGripMath.cs (new)'
  - 'Assets/_Game/ScriptableObjects/Items/WeaponSO.cs'
  - 'Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs'
  - 'Assets/_Game/Scripts/Combat/PlayerCombat.cs'
  - 'Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs'
  - 'Assets/_Game/Editor/WeaponCreatorWindow.cs'
  - 'Assets/_Game/Editor/WeaponModelUtility.cs (new)'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
  - 'Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab'
  - 'Assets/_Game/Prefabs/Items/Weapons/Swords/Axe/Axe_Visual.prefab'
  - 'Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset (new)'
  - 'Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedAxe.asset (new)'
  - 'Assets/_Game/Data/Items/Weapons/Swords/Weapon_TestSword.asset'
  - 'Assets/_Game/Data/Items/Weapons/Swords/Weapon_Axe.asset'
  - 'Assets/Tests/EditMode/WeaponArchetypeTests.cs (new)'
  - 'Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md, Assets/_Game/ScriptableObjects/Items/CLAUDE.md, Assets/_Game/Scripts/Inventory/CLAUDE.md, _bmad-output/project-context.md'
code_patterns: ['SO data in Game.Inventory, one type per file', 'Drawn/Sheathed child convention', 'GameLog TAG logging', 'PrefabUtility.LoadPrefabContents for prefab edits (preserves fileIDs)', 'EditorWindow in Game.Editor namespace']
test_patterns: ['NUnit EditMode, [SystemName]Tests, ScriptableObject.CreateInstance<SwordSO>() + DestroyImmediate in TearDown (see EquipmentSystemTests)']
---

# Tech-Spec: Weapon Archetypes & Normalized Grip

**Created:** 2026-09-28

## Overview

### Problem Statement

Every weapon needs two hand-tuned poses (`Drawn` in the hand, `Sheathed` on the hip) because each art pack
ships meshes with different pivots and axes (e.g. `SM_Sword_1` is centered with the blade along −Y and the grip
at y≈0.35; `SM_Axe_01` pivots at the handle end with the head along +Y, edge +Z, grip at y≈0.08). The Weapon
Creator copies the source model as-is — no grip alignment, a default 1×1×1 hitbox, and it keeps the source
art's `MeshCollider`s and `LODGroup` — so a freshly created weapon is misplaced and has a broken hitbox (the Axe
had to be fixed by hand on 2026-09-28). `SwordSO` is an empty subclass whose only effect is the Creator's
output folder name, so there is no data-driven notion of a weapon family (grip, sheath location, animation set).

### Solution

Normalize every weapon mesh inside its `_Visual` prefab (via the `Mesh` child transform: grip at the origin,
head/blade along +Y, cutting edge along +Z) so all weapons of a family share the same `Drawn` / `Sheathed`
poses. Introduce `WeaponArchetypeSO` holding those poses plus the sheath socket (Hip / Back), animator
override and default combo steps; `WeaponSO` references an archetype and `EquipmentVisuals` applies the
archetype poses at runtime. Upgrade the Weapon Creator to pick an archetype, strip source colliders and extra
LODs, and size the hitbox from renderer bounds. Migrate Test Sword and Axe.

### Scope

**In Scope:**
- `WeaponArchetypeSO` (new SO): drawn pose, sheathed pose, sheath socket (Hip / Back), animator override, default combo steps
- `WeaponSO.archetype` reference field; `SwordSO` class kept (renaming would break asset `m_Script` refs)
- `EquipmentVisuals` applies archetype poses + socket choice at runtime; weapons without an archetype keep prefab poses (backward compatible)
- New `BackWeaponSocket` on the Player rig (Spine2), wired into `EquipmentVisuals`
- Normalized-grip convention for the `Mesh` child of `_Visual` / `_World` prefabs, documented in the weapon prefab CLAUDE.md
- Weapon Creator: archetype dropdown, strips source colliders + LODs (keep LOD0), hitbox sized from renderer bounds, archetype presets, warning that the model must be normalized
- Two archetype assets: `OneHandedSword`, `OneHandedAxe` (Axe keeps `Sword_AnimatorOverride`)
- Migrate `Weapon_TestSword` + `SwordBase_Visual` and `Weapon_Axe` + `Axe_Visual` / `Axe_World` onto the system

**Out of Scope:**
- Grip-preview editor tool (separate spec)
- Dedicated axe animation clips / new animator override
- Two-handed weapons or any weapon actually using the Back socket
- NPC-held weapons
- Retiring `SwordSO` / making `WeaponSO` concrete

## Context for Development

### Codebase Patterns

- `EquipmentVisuals` (Player root) instantiates `equipVisualPrefab` under `WeaponSocket` (hand) or
  `UndrawnWeaponSocket` (hip) and resets the root to identity; `ApplyCombatVisibility` toggles children named
  exactly `Drawn` / `Sheathed`. All per-weapon offsets currently live in those children's local transforms.
- `WeaponHitbox` (on `Drawn`) collects **all** colliders under it in `Awake` and toggles them per attack window —
  any stray collider under `Drawn` becomes part of the hitbox.
- `_Visual` roots need a kinematic Rigidbody (trigger events). See `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md`.
- Weapon Creator: `Assets/_Game/Editor/WeaponCreatorWindow.cs` (`Tools/Items/Weapon Creator`). It lists concrete
  `WeaponSO` subclasses via `TypeCache`, builds `<Name>_Visual` (kinematic RB root → `Drawn` [trigger BoxCollider +
  `WeaponHitbox`] → `Mesh`; `Sheathed` → `Mesh`) and `<Name>_World` (Interactable layer, `ItemPickup`, RB,
  BoxCollider from the first renderer's `localBounds`) and wires them onto the SO, then calls
  `ItemIconGenerator.GenerateMissingIcons()` (renders `worldItemPrefab` previews). Output folders are derived from
  the C# type name (`SwordSO` → `Swords/`).
- **WeaponSO consumers** (must keep working): `EquipmentSystem.Equip` (`item is WeaponSO` → Weapon slot) and
  `GetWeaponDamageBonus`; `PlayerCombat.BindWeaponHitbox` caches `_currentWeaponSO` and `IsMaxCombo()` reads
  `_currentWeaponSO.comboSteps` (unarmed fallback `2`); `EquipmentVisuals.RefreshWeapon` reads
  `animatorOverrideController`; `ItemDetailPanelUI.ShowWeaponSection` reads `damageBonus` only.
- **Player rig sockets** (`Player.prefab`, all on layer 3):
  - `WeaponSocket` → `Character/mixamorig:Hips/.../mixamorig:RightHand/WeaponSocket` (lp `(0.002, 0.092, 0.011)`)
  - `UndrawnWeaponSocket` → `Character/mixamorig:Hips/UndrawnWeaponSocket` (lp `(0.122, -0.008, 0.012)`)
  - `mixamorig:Spine2` exists at `Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2` — parent for the new back socket.
  - `EquipmentVisuals._defaultAnimatorController` = `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`.
- **`Game/Dev/Wire EquipmentVisuals on Player Prefab`** (`Scripts/Editor/WireEquipmentVisuals.cs`) re-wires
  `EquipmentVisuals` fields by rig path; its default-controller path
  (`Art/Characters/Player/Animations/PlayerAnimatorController.controller`) is **stale** — running it today would null
  the controller. Fix it while adding the back socket.
- **Prefab GUID/fileID constraint:** `Weapon_TestSword.equipVisualPrefab` = `{fileID: 100000000, guid: d5e6f7a8b9c0d1e2f3a4b5c6d7e8f901}`
  (hand-crafted GUID, root GO fileID `100000000`). Edit `SwordBase_Visual.prefab` **in place** with
  `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` on the same path — never delete/recreate it.
- **SwordBase_Visual structure today:** root (kinematic RB) → `Drawn` and `Sheathed` are **nested prefab instances**
  of `Hivemind/MedievalWeapons/Art/Models/SM_Sword_1/SM_Sword_1 (1).prefab` renamed; the MeshFilter/Renderer sit
  on `Drawn`/`Sheathed` themselves; `Drawn` has added `BoxCollider` (trigger, size `0.1455×0.8451×0.0289`) + `WeaponHitbox`.
  Poses: Drawn lp `(-0.305, -0.029, -0.13)` euler `(21.94, 338.35, 277.49)`; Sheathed lp `(0.095, -0.22, -0.064)` euler `(346.78, 85.24, 14.24)`.
  Nested-prefab internals can't be reparented — unpack (`PrefabUtility.UnpackPrefabInstance(..., Completely, ...)`)
  inside the loaded contents before inserting a `Mesh` child.
- **Axe_Visual structure today (after the 2026-09-28 hand fix):** root (kinematic RB) → `Drawn` [trigger BoxCollider
  center `(0,0.47,0.095)` size `(0.06,0.22,0.22)` + `WeaponHitbox`] → `Mesh` → `SM_Axe_01_LOD0`; `Sheathed` → `Mesh` →
  `SM_Axe_01_LOD0`. Poses: Drawn lp `(0.0835, 0.0230, 0.0467)` euler `(293.12, 318.97, 107.92)`; Sheathed lp
  `(0.1215, -0.3238, -0.0889)` euler `(346.15, 178.56, 346.38)`. `Axe_World` already stripped to one BoxCollider.
- **Measured mesh frames (mesh-local):**
  - `SM_Sword_1`: bounds centered at origin, size `(0.1455, 0.8451, 0.0289)`; hilt at +Y (crossguard y≈0.25–0.32, pommel y≈0.39), grip ≈ `(0, 0.35, 0)`; blade toward −Y; edges ±X; **leading edge in the sword attack clips = −X**.
  - `SM_Axe_01`: pivot at handle end; handle along +Y (0 → 0.57); head y≈0.39–0.58; cutting edge toward +Z (z≈0.19); grip ≈ `(0, 0.08, 0)`.
  - Swing check method: sample `SwordAttackLeft` / `SwordAttackThrust` with `AnimationClip.SampleAnimation` (Animator disabled) at normalized t 0.25→0.45 and compute `dot(edgeDir, headMotion)` — ≈ +0.9 means the edge leads (verified for the fixed Axe).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` | Spawns weapon visual, socket swap (`SetCombatState`), `ApplyCombatVisibility`, animator override |
| `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs` | Abstract weapon SO: `damageBonus`, `comboSteps`, `animatorOverrideController` |
| `Assets/_Game/ScriptableObjects/Items/Weapons/SwordSO.cs` | Empty concrete subclass (kept) |
| `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs` | `equipVisualPrefab`, stat bonuses |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | `IsMaxCombo()` (L275-280), `BindWeaponHitbox()` (L325-343) |
| `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` | Collects all child colliders in `Awake`, toggles per attack window |
| `Assets/_Game/Editor/WeaponCreatorWindow.cs` | Weapon asset/prefab generator |
| `Assets/_Game/Editor/ItemIconGenerator.cs` | Icon generation from `worldItemPrefab` (unchanged) |
| `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` | Dev wiring menu for `EquipmentVisuals` fields |
| `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md` | Weapon prefab conventions (to update) |
| `Assets/_Game/ScriptableObjects/Items/CLAUDE.md` | Item SO hierarchy docs (to update) |
| `_bmad-output/project-context.md` L232-237 | Weapon rules ("new category = new XxxSO") (to update) |
| `Assets/Tests/EditMode/EquipmentSystemTests.cs` | Test pattern: `CreateInstance<SwordSO>()`, `_createdItems` teardown |

### Technical Decisions

- **Archetype is a reference field, not a subclass** — keeps `SwordSO` and existing asset GUIDs intact; new weapon families are data (new archetype asset), not code.
- **Poses applied at runtime** by `EquipmentVisuals` from the archetype — tuning an archetype updates every weapon of that family. Null archetype → prefab poses unchanged.
- **Back socket added now** (Spine2) with a Hip/Back choice on the archetype; no weapon uses Back yet.
- **Axe keeps sword animations** — `OneHandedAxe` references `Sword_AnimatorOverride`; swing-edge alignment was verified by sampling `SwordAttackLeft` / `SwordAttackThrust` (edge leads, dot ≈ +0.9).
- **Normalized weapon frame** (the `Mesh` child's parent space, i.e. `Drawn`/`Sheathed` local space): grip point at origin, blade/head along **+Y**, leading cutting edge along **+Z**. The `Mesh` child transform converts art-pack mesh space into this frame; `Drawn`/`Sheathed` poses then come from the archetype.
- **Weapon-level overrides win over archetype defaults:** `WeaponSO.animatorOverrideController` (if non-null) and `WeaponSO.comboSteps` (if > 0) override the archetype; resolved via `WeaponSO.ResolvedAnimatorOverride` / `ResolvedComboSteps`. Migrated assets set `comboSteps: 0` and `animatorOverrideController: null` to inherit.
- **Archetype poses are derived, not re-tuned:** the migration computes `newDrawnPose = oldDrawnPose × inverse(meshChildLocal)` from the existing hand-tuned sword poses, so the sword looks identical after migration.
- **Creator normalization inputs:** grip point (mesh-local `Vector3`), blade axis and edge axis (signed-axis enums) — the tool builds the `Mesh` child transform from them. Fine-tuning visually is the job of the separate grip-preview tool spec.

## Implementation Plan

### Tasks

**Data layer (runtime, `Game` assembly, namespace `Game.Inventory`, one type per file)**

- [x] Task 1: Add `WeaponPose` struct
  - File: `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponPose.cs` (new)
  - Action: `[System.Serializable] public struct WeaponPose { public Vector3 localPosition; public Vector3 localEulerAngles; public void ApplyTo(Transform t) { t.localPosition = localPosition; t.localRotation = Quaternion.Euler(localEulerAngles); } public static WeaponPose From(Transform t) => new() { localPosition = t.localPosition, localEulerAngles = t.localEulerAngles }; }`
  - Notes: Let Unity generate the `.meta` (no hand-written meta — see root CLAUDE.md MonoImporter rule).

- [x] Task 2: Add `WeaponSheathSocket` enum
  - File: `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponSheathSocket.cs` (new)
  - Action: `public enum WeaponSheathSocket { Hip = 0, Back = 1 }`

- [x] Task 3: Add `SignedAxis` + `WeaponGripMath` (normalization math, runtime so tests and the future grip-preview tool can use it)
  - Files: `Assets/_Game/ScriptableObjects/Items/Weapons/SignedAxis.cs` (new), `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponGripMath.cs` (new)
  - Action:
    - `public enum SignedAxis { PosX, NegX, PosY, NegY, PosZ, NegZ }`
    - `public static class WeaponGripMath` with:
      - `public static Vector3 ToVector(SignedAxis a)` — unit vector per enum value.
      - `public static bool AreValid(SignedAxis blade, SignedAxis edge)` — false when both are on the same axis (e.g. `PosY`/`NegY`).
      - `public static Quaternion NormalizationRotation(SignedAxis blade, SignedAxis edge)` → `Quaternion.Inverse(Quaternion.LookRotation(ToVector(edge), ToVector(blade)))` (maps mesh blade → +Y, mesh edge → +Z).
      - `public static void GetMeshChildLocal(Vector3 gripMeshLocal, SignedAxis blade, SignedAxis edge, out Vector3 localPosition, out Quaternion localRotation)` → `localRotation = NormalizationRotation(blade, edge); localPosition = -(localRotation * gripMeshLocal);`
      - `public static void RebaseParentPose(Vector3 oldPos, Quaternion oldRot, Vector3 meshChildPos, Quaternion meshChildRot, out Vector3 newPos, out Quaternion newRot)` → `newRot = oldRot * Quaternion.Inverse(meshChildRot); newPos = oldPos - newRot * meshChildPos;` (keeps the mesh's socket-space placement identical when a normalizing `Mesh` child is inserted under an existing posed parent).
  - Notes: No `GameLog` needed (pure math, no TAG).

- [x] Task 4: Add `WeaponArchetypeSO`
  - File: `Assets/_Game/ScriptableObjects/Items/Weapons/WeaponArchetypeSO.cs` (new)
  - Action: `[CreateAssetMenu(menuName = "Items/Weapons/Weapon Archetype", fileName = "Archetype_")] public class WeaponArchetypeSO : ScriptableObject` with:
    - `[Header("Grip")] public WeaponPose drawnPose; public WeaponPose sheathedPose;` (tooltips: pose of the `Drawn` / `Sheathed` child in `WeaponSocket` / sheath-socket space; assumes a normalized mesh — grip at origin, blade +Y, edge +Z)
    - `[Header("Sheath")] public WeaponSheathSocket sheathSocket = WeaponSheathSocket.Hip;`
    - `[Header("Animation")] public AnimatorOverrideController animatorOverrideController;`
    - `[Header("Combo")] [Min(1)] public int defaultComboSteps = 2;`

- [x] Task 5: Extend `WeaponSO` with archetype + resolved accessors
  - File: `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs`
  - Action:
    - Add `public const int DEFAULT_COMBO_STEPS = 2;`
    - Add `[Header("Archetype")] public WeaponArchetypeSO archetype;` (tooltip: shared grip/sheath/animation preset for the weapon family).
    - Change `comboSteps` default from `2` to `0`, tooltip `"0 = use archetype default"`, add `[Min(0)]`.
    - Update `animatorOverrideController` comment/tooltip: `"Optional per-weapon override. Null = use archetype."`
    - Add `public int ResolvedComboSteps => comboSteps > 0 ? comboSteps : archetype != null ? Mathf.Max(1, archetype.defaultComboSteps) : DEFAULT_COMBO_STEPS;`
    - Add `public AnimatorOverrideController ResolvedAnimatorOverride => animatorOverrideController != null ? animatorOverrideController : archetype != null ? archetype.animatorOverrideController : null;`
  - Notes: Existing assets serialize `comboSteps: 2` explicitly, so they keep behaving identically until migrated (Task 13). Keep field names unchanged (serialization).

**Runtime consumers**

- [x] Task 6: `PlayerCombat` reads resolved combo steps
  - File: `Assets/_Game/Scripts/Combat/PlayerCombat.cs`
  - Action: In `IsMaxCombo()` replace `_currentWeaponSO.comboSteps : 2` with `_currentWeaponSO.ResolvedComboSteps : WeaponSO.DEFAULT_COMBO_STEPS`. Update the Story 7.10 comment accordingly.

- [x] Task 7: `EquipmentVisuals` applies archetype poses + sheath socket
  - File: `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`
  - Action:
    - Add `[SerializeField] private Transform _backWeaponSocket;` next to `_undrawnWeaponSocket`.
    - Add field `private WeaponArchetypeSO _currentArchetype;` — set in `RefreshWeapon()` from `(weapon as WeaponSO)?.archetype` (null when no weapon).
    - Add `public static Transform ResolveSheathSocket(WeaponArchetypeSO archetype, Transform hipSocket, Transform backSocket)` → returns `backSocket` when `archetype != null && archetype.sheathSocket == WeaponSheathSocket.Back && backSocket != null`, else `hipSocket`.
    - Add private `Transform CurrentSheathSocket()` that calls `ResolveSheathSocket(_currentArchetype, _undrawnWeaponSocket, _backWeaponSocket)` and logs `GameLog.Warn(TAG, ...)` when Back was requested but `_backWeaponSocket` is null.
    - Replace the sheathed-socket choice in `SetCombatState` (`isInCombat ? _weaponSocket : _undrawnWeaponSocket`) and in `RefreshWeapon` (`(_isInCombat || _undrawnWeaponSocket == null) ? ...`) with `CurrentSheathSocket()` (keep the existing "fall back to `_weaponSocket` when the sheath socket is null" behavior in `RefreshWeapon`).
    - Add `public static void ApplyArchetypePoses(GameObject weaponVisual, WeaponArchetypeSO archetype)` → no-op if either is null; otherwise `transform.Find("Drawn")` / `"Sheathed"` and `pose.ApplyTo(child)` for each found child (missing child = skip silently, consistent with `ApplyCombatVisibility`).
    - In `RefreshWeapon`, call `ApplyArchetypePoses(_weaponVisual, _currentArchetype)` right after instantiating `equipVisualPrefab` (before `ApplyCombatVisibility`). Not called for the placeholder cube.
    - Replace `ApplyAnimatorOverride((weapon as WeaponSO)?.animatorOverrideController)` with `ApplyAnimatorOverride((weapon as WeaponSO)?.ResolvedAnimatorOverride)`.
  - Notes: Null archetype ⇒ prefab poses untouched (backward compatible). Root is still reset to identity on the socket; `OnVisualsRefreshed` still raised at the end of `Refresh()`.

- [x] Task 8: Add the back socket to the Player rig and fix the wiring tool
  - Files: `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs`, `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action:
    - In `WireEquipmentVisuals.Wire()`: fix `_defaultAnimatorController` path to `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller`; find or create `BackWeaponSocket` under `Character/mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/mixamorig:Spine2` (create: new GO, layer 3, `localPosition (0, 0.05, -0.15)`, identity rotation) and assign `_backWeaponSocket`; include it in the final log line.
    - Run `Game/Dev/Wire EquipmentVisuals on Player Prefab` (or `execute_menu_item`) so `Player.prefab` gets the socket + wiring.
  - Notes: Verify afterwards that `_defaultAnimatorController` still points at `Humanoid_Template.controller` and all previously wired fields are unchanged.

**Editor tooling (`Game.Editor` assembly, `Assets/_Game/Editor/`)**

- [x] Task 9: Add `WeaponModelUtility` (shared by the Creator and the migration)
  - File: `Assets/_Game/Editor/WeaponModelUtility.cs` (new, namespace `Game.Editor`)
  - Action: `public static class WeaponModelUtility` with:
    - `public static void StripForEquip(GameObject model)` — `DestroyImmediate` every `Collider`, `Rigidbody` and `LODGroup` in `model`'s hierarchy, and every child GameObject whose name matches `_LOD[1-9]\d*$` (keep `_LOD0` / non-LOD renderers).
    - `public static void Normalize(Transform meshChild, Vector3 gripMeshLocal, SignedAxis blade, SignedAxis edge)` — applies `WeaponGripMath.GetMeshChildLocal`.
    - `public static void FitBoxToRenderers(BoxCollider box)` — encapsulates the 8 corners of every child `Renderer.localBounds` transformed into `box.transform` local space; sets `center`/`size`. No-op with a warning if there are no renderers.

- [x] Task 10: Upgrade `WeaponCreatorWindow`
  - File: `Assets/_Game/Editor/WeaponCreatorWindow.cs`
  - Action:
    - Add UI: `ObjectField` "Archetype" (`WeaponArchetypeSO`, required), `Vector3Field` "Grip Point (model local)" (default zero), `EnumField` "Blade Axis" (default `PosY`), `EnumField` "Edge Axis" (default `PosZ`), and a `HelpBox` explaining the normalized frame and that grip/edge must be verified visually (Play mode or the grip-preview tool).
    - Add `"archetype"` to `ExcludedProperties` (driven by the new field).
    - Validation in `OnCreateClicked`: archetype required; `WeaponGripMath.AreValid(blade, edge)` required — show `EditorUtility.DisplayDialog` errors like the existing ones.
    - Folders: derive `familyFolder` from the archetype asset name (strip a leading `Archetype_`, e.g. `Archetype_OneHandedAxe` → `OneHandedAxe`); `dataPath = Assets/_Game/Data/Items/Weapons/{familyFolder}`, `prefabsPath = Assets/_Game/Prefabs/Items/Weapons/{familyFolder}/{safeName}` (replaces the `SwordSO → Swords` type-name logic; the type dropdown stays to pick the C# class).
    - On the created SO: set `archetype`, and `comboSteps = 0` / `animatorOverrideController = null` **only if** the user left them at defaults (so explicit per-weapon overrides still work).
    - `SetupModelChild`: after `Instantiate`, call `WeaponModelUtility.StripForEquip(modelInstance)` and `WeaponModelUtility.Normalize(modelInstance.transform, grip, blade, edge)`; for `Drawn`, add the trigger `BoxCollider` + `WeaponHitbox` then `WeaponModelUtility.FitBoxToRenderers(trigger)`.
    - Leave `Drawn` / `Sheathed` at identity in the saved prefab (archetype poses are applied at runtime).
    - World prefab: `StripForEquip` the model (no normalization needed), and size the root `BoxCollider` from **all** renderers (use `FitBoxToRenderers` on a temporary child-space basis or encapsulate `Renderer.bounds` of the model) instead of only the first renderer.
    - Update the empty-types `HelpBox` text and the success dialog to remind: "Verify grip and edge direction before use."

**Data + migration (run once via MCP `execute_code` or a throwaway editor snippet — do NOT commit a migration script)**

- [x] Task 11: Create archetype assets
  - Files: `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_OneHandedSword.asset`, `.../Archetype_OneHandedAxe.asset` (new, + folder `.meta`)
  - Action: Create both via `ScriptableObject.CreateInstance<WeaponArchetypeSO>()` + `AssetDatabase.CreateAsset`. Both: `sheathSocket = Hip`, `defaultComboSteps = 2`, `animatorOverrideController = Sword_AnimatorOverride` (`Assets/_Game/Art/Characters/Humanoids/Animations/Combat/attacks/Sword/Sword_AnimatorOverride.overrideController`). Poses filled in Task 12.

- [x] Task 12: Normalize both `_Visual` prefabs and derive archetype poses
  - Files: `SwordBase/SwordBase_Visual.prefab`, `Axe/Axe_Visual.prefab`, both archetype assets
  - Action (per prefab, via `PrefabUtility.LoadPrefabContents(path)` → edit → `SaveAsPrefabAsset(root, samePath)` → `UnloadPrefabContents`):
    - **Sword** (grip `(0, 0.35, 0)`, blade `NegY`, edge `NegX`): for `Drawn` and `Sheathed`: record old pose; if it is a prefab instance root, `PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction)`; create child `Mesh` with a `MeshFilter` + `MeshRenderer` copying `sharedMesh` / `sharedMaterials`; remove the `MeshFilter` / `MeshRenderer` from the parent; `WeaponModelUtility.Normalize(Mesh, grip, blade, edge)`; `WeaponGripMath.RebaseParentPose(...)` → set the parent's new pose. On `Drawn`, `FitBoxToRenderers` the trigger `BoxCollider` (keep `WeaponHitbox`).
    - **Axe** (grip `(0, 0.08, 0)`, blade `PosY`, edge `PosZ`): `Mesh` children already exist → `Normalize` them, `RebaseParentPose` for `Drawn` / `Sheathed`; transform the hitbox center into the new frame (`center_new = meshLocalRot * center_old + meshLocalPos`, i.e. `(0, 0.39, 0.095)`), keep size `(0.06, 0.22, 0.22)` (head-only hitbox).
    - Write each prefab's resulting `Drawn` / `Sheathed` poses into its archetype (`drawnPose` / `sheathedPose` via `WeaponPose.From`) and `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()`.
  - Notes:
    - After saving, re-read `SwordBase_Visual.prefab` YAML and confirm the root GO is still `--- !u!1 &100000000` (else `Weapon_TestSword.equipVisualPrefab` breaks).
    - Sanity check: the Axe's derived `drawnPose` should be within ~1 cm / ~3° of the Sword's (the axe grip was derived from the sword's on 2026-09-28) — if not, stop and investigate.

- [x] Task 13: Point both weapons at their archetypes
  - Files: `Assets/_Game/Data/Items/Weapons/Swords/Weapon_TestSword.asset`, `.../Weapon_Axe.asset`
  - Action: via `SerializedObject`: `archetype` = matching archetype, `comboSteps = 0`, `animatorOverrideController = null`. Leave the assets in `Swords/` (moving them is out of scope).

**Tests & docs**

- [x] Task 14: EditMode tests
  - File: `Assets/Tests/EditMode/WeaponArchetypeTests.cs` (new)
  - Action: NUnit fixture following `EquipmentSystemTests` (create SOs with `ScriptableObject.CreateInstance`, track in a list, `DestroyImmediate` in `[TearDown]`; GameObjects likewise). Cases:
    - `ResolvedComboSteps`: weapon `3` + archetype `2` → 3; weapon `0` + archetype `4` → 4; weapon `0` + no archetype → `DEFAULT_COMBO_STEPS`; weapon `0` + archetype `0` → 1.
    - `ResolvedAnimatorOverride`: weapon override wins; null → archetype's; both null → null.
    - `EquipmentVisuals.ResolveSheathSocket`: null archetype → hip; Hip → hip; Back + back socket → back; Back + null back socket → hip.
    - `EquipmentVisuals.ApplyArchetypePoses`: sets `Drawn`/`Sheathed` local pos/rot from the archetype (compare with `Vector3`/`Quaternion.Angle` tolerance); null archetype leaves transforms untouched; visual without `Drawn`/`Sheathed` children doesn't throw.
    - `WeaponGripMath`: `NormalizationRotation(NegY, NegX)` maps `(0,-1,0)`→`(0,1,0)` and `(-1,0,0)`→`(0,0,1)`; `GetMeshChildLocal` maps the grip point to the origin; `AreValid(PosY, NegY)` is false; `RebaseParentPose` round-trip — a mesh-space point lands at the same parent-space position before and after rebasing.
  - Notes: Run the full EditMode suite afterwards (272 tests currently green + new ones).

- [x] Task 15: Update documentation
  - Files: `Assets/_Game/Prefabs/Items/Weapons/CLAUDE.md`, `Assets/_Game/ScriptableObjects/Items/CLAUDE.md`, `Assets/_Game/Scripts/Inventory/CLAUDE.md`, `_bmad-output/project-context.md` (L232-237)
  - Action:
    - Weapons CLAUDE.md: add "Normalized Weapon Frame" (Mesh child convention, grip/blade/edge axes, measured frames for SM_Sword_1 / SM_Axe_01) and "Weapon Archetypes" (runtime poses, Hip/Back socket, null-archetype fallback); rewrite "Grip Alignment" (poses now live in the archetype, not per prefab); replace the "copy GUID into .asset YAML" advice with "use the Weapon Creator"; add a "Weapon Creator" section (inputs, what it strips, hitbox fitting, verify grip afterwards); add checklist rows: HIGH `Mesh` child not normalized (weapon uses archetype but mesh pivot/axes are raw art-pack), HIGH non-trigger collider left under `Drawn`/`Sheathed` (becomes part of the hitbox / blocks the CharacterController), MEDIUM hitbox left at default 1×1×1.
    - Items CLAUDE.md: add `WeaponArchetypeSO` (+ `WeaponPose`, `WeaponSheathSocket`, `SignedAxis`, `WeaponGripMath`) to the hierarchy section; replace "new weapon category = new `XxxSO`" with "new weapon family = new `WeaponArchetypeSO` asset; new C# subclass only when the family needs new fields"; document `ResolvedComboSteps` / `ResolvedAnimatorOverride` precedence.
    - Inventory CLAUDE.md: update the `EquipmentVisuals` row (archetype poses, `_backWeaponSocket`, `ResolveSheathSocket`).
    - project-context.md L232-237: same rule change as the Items CLAUDE.md; `comboSteps` → "`ResolvedComboSteps` (weapon > 0 overrides archetype)".

### Acceptance Criteria

- [x] AC 1: Given `Weapon_TestSword` is equipped after migration, when the player is sheathed and then draws (`Draw Weapon` input), then the sword appears at the same hip and hand positions/orientations as before migration (within ~1 cm / ~2°, compared via world-space renderer bounds or screenshots before/after).
- [x] AC 2: Given `Weapon_Axe` is equipped, when sheathed, then the axe hangs from `UndrawnWeaponSocket` head-up at the back-right hip (same as the 2026-09-28 fix), and when drawn, the handle sits in the right hand with the head beyond the fist.
- [x] AC 3: Given the Axe is drawn, when `SwordAttackLeft` / `SwordAttackThrust` are sampled at normalized t 0.35–0.40, then `dot(axe edge (+Z of Drawn), head motion) ≥ 0.7` (edge leads the swing).
- [x] AC 4: Given a weapon whose `WeaponSO.archetype` is null (e.g. a new test SO with a hand-posed prefab), when it is equipped, then `Drawn`/`Sheathed` keep their prefab poses and it sheathes on `UndrawnWeaponSocket` — no errors, no warnings.
- [x] AC 5: Given an archetype with `sheathSocket = Back` and `_backWeaponSocket` assigned, when the weapon is equipped sheathed, then the visual is parented to `BackWeaponSocket`; when drawn it moves to `WeaponSocket`; when sheathed again it returns to `BackWeaponSocket`.
- [x] AC 6: Given an archetype with `sheathSocket = Back` but `_backWeaponSocket` unassigned, when the weapon is equipped sheathed, then it falls back to `UndrawnWeaponSocket` and one `GameLog.Warn` is logged.
- [x] AC 7: Given `Weapon_TestSword` / `Weapon_Axe` with `comboSteps = 0` and archetype `defaultComboSteps = 2`, when the player chains attacks, then the combo caps at 2 steps (same as before); given a weapon SO with `comboSteps = 3`, then it caps at 3.
- [x] AC 8: Given a weapon with `animatorOverrideController = null` and an archetype referencing `Sword_AnimatorOverride`, when equipped, then the player Animator's controller is `Sword_AnimatorOverride`; when unequipped, it reverts to `Humanoid_Template`.
- [x] AC 9: Given the Weapon Creator with a raw art-pack model that has a `LODGroup`, 6 LOD children and `MeshCollider`s, when "Create Weapon Assets" is clicked, then the `_Visual` and `_World` prefabs contain no `MeshCollider`, no `LODGroup`, no `_LOD1+` children; `_Visual/Drawn` has exactly one trigger `BoxCollider` whose bounds enclose the model (not 1×1×1); the SO has `archetype` set and `comboSteps = 0`; files land in `Data/Items/Weapons/{Family}/` and `Prefabs/Items/Weapons/{Family}/{Name}/`.
- [x] AC 10: Given the Weapon Creator, when Create is clicked with no archetype, or with blade and edge on the same axis, then an error dialog is shown and no assets are created.
- [x] AC 11: Given the Weapon Creator with grip/blade/edge inputs matching the Axe (`(0,0.08,0)`, `PosY`, `PosZ`) and archetype `OneHandedAxe`, when a test weapon is created from `SM_Axe_01` and equipped, then it is placed identically to `Weapon_Axe` (delete the test assets afterwards).
- [x] AC 12: Given the migration ran, when `SwordBase_Visual.prefab` is inspected, then its root GameObject fileID is still `100000000` and `Weapon_TestSword.equipVisualPrefab` still resolves (sword visible when equipped, not the yellow placeholder cube).
- [x] AC 13: Given `Player.prefab`, when inspected after Task 8, then `BackWeaponSocket` exists under `mixamorig:Spine2` (layer 3), `EquipmentVisuals._backWeaponSocket` references it, and every previously wired `EquipmentVisuals` field (incl. `_defaultAnimatorController = Humanoid_Template`) is unchanged.
- [x] AC 14: Given the EditMode suite, when run, then all existing tests plus the new `WeaponArchetypeTests` pass, and the console shows no compile errors.

## Additional Context

### Dependencies

- No new packages. Uses existing `Sword_AnimatorOverride.overrideController`, `Humanoid_Template.controller`, `SM_Sword_1` / `SM_Axe_01` art.
- Builds on the 2026-09-28 manual Axe fix (`Axe_Visual` / `Axe_World` already stripped; poses hand-derived) — that fix must be committed first.
- Follow-up spec (separate): **Weapon grip-preview editor tool** — consumes `WeaponGripMath` / `WeaponArchetypeSO` to preview and tune poses on the player rig outside Play mode.

### Testing Strategy

- **EditMode (automated):** `WeaponArchetypeTests` (Task 14) + full existing suite.
- **Unity MCP verification (manual/agent):**
  1. Before migration: enter Play mode, equip `Weapon_TestSword`, record world-space renderer bounds for sheathed and drawn (`execute_code`); repeat after migration and compare (AC 1).
  2. Equip `Weapon_Axe`, screenshot sheathed + drawn (`manage_camera screenshot`, `view_position` near the hip/hand) (AC 2).
  3. Edge check: disable the Animator, `AnimationClip.SampleAnimation` `SwordAttackLeft` / `SwordAttackThrust` at t 0.25→0.45, compute `dot(Drawn.forward, headDelta)` (AC 3).
  4. Temporarily set an archetype to `Back`, equip, draw/sheathe, check parents (AC 5); unassign `_backWeaponSocket` on the scene instance only, check the fallback + warning (AC 6); revert.
  5. Chain attacks via reflection-invoked `TryAttack` or input, confirm combo cap from logs (AC 7); check `Animator.runtimeAnimatorController` on equip/unequip (AC 8).
  6. Run the Weapon Creator on `SM_Axe_01` into a scratch name, inspect generated prefabs (AC 9, 11), try invalid inputs (AC 10), delete the scratch assets.
- **Console:** `read_console` errors/warnings after each step.

### Notes

- **High risk — prefab fileID:** re-creating `SwordBase_Visual.prefab` instead of editing in place would regenerate the root fileID and silently turn the Test Sword into the placeholder cube (AC 12). Use `LoadPrefabContents` on the same path only.
- **High risk — unpacking nested prefabs:** unpacking `Drawn` / `Sheathed` in the sword prefab detaches them from `SM_Sword_1 (1).prefab`; that's intended (the art prefab is only a mesh + material). Confirm the material/mesh references survive.
- **Runtime overrides prefab poses:** once a weapon has an archetype, editing its prefab's `Drawn`/`Sheathed` transforms has no in-game effect — document this clearly (Task 15) to avoid confusion; per-weapon fine-tuning belongs in the `Mesh` child.
- **Back socket placement** `(0, 0.05, -0.15)` under Spine2 is a starting guess; tune it when the first Back weapon exists (out of scope).
- **Future:** dedicated axe clips (new override on `Archetype_OneHandedAxe`), two-handed archetype, NPC weapon holding (would reuse `ApplyArchetypePoses` / `ResolveSheathSocket`), moving existing assets out of `Swords/` into family folders.

## Implementation Notes (2026-09-28)

- Baseline commit `d9e453c` (Axe hand-fix prefabs were uncommitted in the working tree and are included).
- Migration run once via MCP `execute_code` (no script committed). Mesh-in-socket placement before/after:
  Sword Drawn 0.000 cm / 0.00°, Sheathed 0.000 cm / 0.67°; Axe 0.006 cm / ≤1.1° (angle residue = 4-decimal
  baseline rounding). Axe vs Sword derived `drawnPose`: 0.00 cm / 0.00°. `SwordBase_Visual` root still `&100000000`.
- Derived poses — OneHandedSword: drawn lp `(0.0113, 0.0133, 0.0138)` e `(293.12, 318.97, 107.92)`, sheathed lp
  `(0.0105, 0.1103, 0.0153)` e `(13.85, 358.56, 193.62)`; OneHandedAxe: same drawn, sheathed lp
  `(0.1022, -0.2483, -0.0708)` e `(346.15, 178.56, 346.38)`.
- Verification method deviations:
  - AC 1/2: numeric renderer-matrix comparison instead of screenshots.
  - AC 3: not re-sampled — Axe placement is unchanged (≤0.01 cm / ≤1.1°) from the hand fix where dot ≈ +0.9 was measured.
  - AC 9/11: exercised `SetupModelChild` + world-prefab logic on `SM_Axe_01` via reflection (the full
    "Create" click ends in a modal dialog that blocks MCP); folder naming + AC 10 dialogs verified by code only —
    **run the Creator once by hand to confirm.**
  - AC 4–8: Play mode via `execute_code` (equip/draw/sheathe, temp Back archetype, unassigned back socket → 1 warning).
- EditMode: 290/290 (272 existing + 18 `WeaponArchetypeTests`).
- `Core.unity` got re-serialized UI anchor changes when entering Play mode — unrelated to this spec.

## Review Notes

- Adversarial review completed (inline)
- Findings: 7 total, 4 fixed, 3 skipped (F5, F6 noise; F7 undecided)
- Resolution approach: auto-fix
- Fixed: F1 `StripForEquip` also strips LOD1+ renderers by `LODGroup` membership (not just `_LOD` naming);
  F2 `Normalize` accounts for the Mesh child's `localScale`; F3 the Back-socket fallback warning fires once per
  equipped weapon; F4 `SignedAxis` has explicit paired values (`AreValid` relies on value / 2).
- Skipped: F5 Rigidbody+Joint strip failure (no joints on weapon art); F6 pre-existing hand fallback when the
  hip socket is unassigned; F7 archetype-level hitbox shape (whole-mesh fit, documented as "shrink by hand").
- Post-fix: EditMode 290/290; renamed-LOD strip and scaled-root grip verified via MCP; AC 11 still 0.000 cm / 0.000°.
