# Story 7.8: Equipment Visual Prefab Support

Status: done

## Story

As a player,
I want equipped weapons and gear to display their real 3D mesh on the player model instead of colored primitive shapes,
so that equipping items feels visually authentic as real art assets are added to the project.

## Acceptance Criteria

### AC 1 — `EquipableItemSO.cs` — `equipVisualPrefab` field

Add the following field to `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs`, above the existing `[Header("Stat Bonuses")]`:

```csharp
[Header("Visuals")]
public GameObject equipVisualPrefab; // Prefab instantiated on socket when equipped. Null = placeholder primitive.
```

- Field is on `EquipableItemSO` (not `WeaponSO`) — all equippables (weapon, helmet, armor) can benefit
- `null` means placeholder fallback remains — no existing `.asset` file breaks

---

### AC 2 — `WeaponSO.cs` — `animatorOverrideController` field

Add the following field to `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs`, after the existing `[Header("Combat")]` section:

```csharp
[Header("Animation")]
public AnimatorOverrideController animatorOverrideController; // Optional. Applied to player Animator on equip. Null = keep default controller.
```

- `null` means no override (default `PlayerAnimatorController` stays active)
- Applied to the player's `Animator.runtimeAnimatorController` when the weapon is equipped

---

### AC 3 — `EquipmentVisuals.cs` — Prefab instantiation, animator override, `ActiveWeaponGO`

Modify `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` with the following changes:

**3a — New serialized fields** (add after `_armorPlaceholderMaterial`):

```csharp
[SerializeField] private Animator _animator;
[SerializeField] private RuntimeAnimatorController _defaultAnimatorController;
```

**3b — `ActiveWeaponGO` property** (add after `private Material _originalBodyMaterial;`):

```csharp
/// <summary>The currently instantiated weapon visual GO. Exposed for PlayerCombat (story 7-9) to locate WeaponHitbox.</summary>
public GameObject ActiveWeaponGO => _weaponVisual;
```

**3c — `Awake()` — null-guards for new fields** (add at end of existing Awake, after `_originalBodyMaterial` cache):

```csharp
if (_animator == null)
    GameLog.Warn(TAG, "EquipmentVisuals: _animator not assigned — animator override will be skipped");
if (_defaultAnimatorController == null)
    GameLog.Warn(TAG, "EquipmentVisuals: _defaultAnimatorController not assigned — override restore will set controller to null");
```

**3d — `RefreshWeapon()` — destroy path** (update the first two lines):

```csharp
if (_weaponVisual != null)
    Destroy(_weaponVisual);
_weaponVisual = null;
ApplyAnimatorOverride(null); // Restore default controller when weapon removed
```

**3e — `RefreshWeapon()` — attach path** (replace the single `CreatePlaceholder` call and its Log):

```csharp
if (weapon.equipVisualPrefab != null)
{
    _weaponVisual = Object.Instantiate(weapon.equipVisualPrefab, _weaponSocket);
    _weaponVisual.transform.localPosition = Vector3.zero;
    _weaponVisual.transform.localRotation = Quaternion.identity;
    GameLog.Info(TAG, $"Weapon visual attached (prefab: {weapon.equipVisualPrefab.name})");
}
else
{
    _weaponVisual = CreatePlaceholder(PrimitiveType.Cube, _weaponSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow);
    GameLog.Info(TAG, "Weapon visual attached (placeholder)");
}
ApplyAnimatorOverride((weapon as WeaponSO)?.animatorOverrideController);
```

**3f — `RefreshHelmet()` — attach path** (replace the single `CreatePlaceholder` call and its Log):

```csharp
if (helmet.equipVisualPrefab != null)
{
    _helmetVisual = Object.Instantiate(helmet.equipVisualPrefab, _helmetSocket);
    _helmetVisual.transform.localPosition = Vector3.zero;
    _helmetVisual.transform.localRotation = Quaternion.identity;
    GameLog.Info(TAG, $"Helmet visual attached (prefab: {helmet.equipVisualPrefab.name})");
}
else
{
    _helmetVisual = CreatePlaceholder(PrimitiveType.Sphere, _helmetSocket, new Vector3(0.28f, 0.28f, 0.28f), Color.cyan);
    GameLog.Info(TAG, "Helmet visual attached (placeholder)");
}
```

**3g — New private helper** (add after `RefreshBody()`):

```csharp
private void ApplyAnimatorOverride(AnimatorOverrideController overrideController)
{
    if (_animator == null) return;
    _animator.runtimeAnimatorController = overrideController != null
        ? overrideController
        : _defaultAnimatorController;
}
```

---

### AC 4 — Player prefab Inspector wiring

Wire the two new serialized fields on the `EquipmentVisuals` component on the Player prefab root:

- `_animator` → the `Animator` component on the **Player prefab root** (already present; `PlayerAnimatorController` is assigned to it)
- `_defaultAnimatorController` → `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller`

**Also update `WireEquipmentVisuals.cs`** (`Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs`) to wire these two new fields programmatically, so `Game/Dev/Wire EquipmentVisuals on Player Prefab` stays up to date.

---

### AC 5 — `Weapon_TestSword.asset` — assign `equipVisualPrefab`

In the Inspector for `Assets/_Game/Data/Items/Weapon_TestSword.asset`:
- `equipVisualPrefab` → `Assets/_Game/Prefabs/Items/Weapons/Swords/Sword base.prefab`
- `animatorOverrideController` → leave **null** (test sword uses default PlayerAnimatorController)

Do **not** assign `equipVisualPrefab` on `Armor_TestHelmet.asset` or `Armor_TestArmor.asset` — their placeholder visuals remain correct for this story.

---

### AC 6 — Play Mode validation

- Equipping `Weapon_TestSword` → `Sword base.prefab` mesh appears attached to the player's right hand and moves with animations (not the yellow cube)
- Unequipping the sword → `Sword base.prefab` instance is destroyed and the default `PlayerAnimatorController` is restored
- Equipping a weapon with null `equipVisualPrefab` → yellow cube placeholder still appears (backward-compatible path)
- Equipping `Armor_TestHelmet` → cyan sphere placeholder still appears on head (prefab path not triggered)
- Equipping `Armor_TestArmor` → steel-gray body material swap still works (body slot unaffected)
- No null-ref errors on startup from `EquipmentVisuals` (`_animator` and `_defaultAnimatorController` warn only, not error)
- `ActiveWeaponGO` returns the sword GO while equipped, null while unequipped (verify in debugger or log)

## Tasks / Subtasks

- [x] Task 1: Add `equipVisualPrefab` to `EquipableItemSO.cs` (AC: 1)
  - [x] 1.1 Add `[Header("Visuals")]` + `public GameObject equipVisualPrefab;` field above stat bonus header
  - [x] 1.2 Verify compilation is clean (`read_console` after save)

- [x] Task 2: Add `animatorOverrideController` to `WeaponSO.cs` (AC: 2)
  - [x] 2.1 Add `[Header("Animation")]` + `public AnimatorOverrideController animatorOverrideController;` field after Combat header
  - [x] 2.2 Verify compilation is clean

- [x] Task 3: Update `EquipmentVisuals.cs` (AC: 3)
  - [x] 3.1 Add `_animator` and `_defaultAnimatorController` serialized fields
  - [x] 3.2 Add `ActiveWeaponGO` property
  - [x] 3.3 Add null-guards for new fields in `Awake()`
  - [x] 3.4 Update `RefreshWeapon()` destroy path — call `ApplyAnimatorOverride(null)` before nulling `_weaponVisual`
  - [x] 3.5 Update `RefreshWeapon()` attach path — prefab branch + placeholder fallback + `ApplyAnimatorOverride`
  - [x] 3.6 Update `RefreshHelmet()` attach path — prefab branch + placeholder fallback
  - [x] 3.7 Add `ApplyAnimatorOverride()` private helper
  - [x] 3.8 Verify compilation clean; no existing behavior changed for null-prefab items

- [x] Task 4: Wire Player prefab (AC: 4)
  - [x] 4.1 Update `WireEquipmentVisuals.cs` editor utility to set `_animator` and `_defaultAnimatorController`
  - [x] 4.2 Run `Game/Dev/Wire EquipmentVisuals on Player Prefab` to apply wiring
  - [x] 4.3 Verify no missing references in Player prefab Inspector for `EquipmentVisuals`

- [x] Task 5: Assign `equipVisualPrefab` on `Weapon_TestSword.asset` (AC: 5)
  - [x] 5.1 Assign `Assets/_Game/Prefabs/Items/Weapons/Swords/Sword base.prefab` to `equipVisualPrefab` on `Weapon_TestSword.asset`

- [ ] Task 6: Play Mode validation (AC: 6)
  - [ ] 6.1 Manual validation per AC 6 checklist

## Dev Notes

### `ApplyAnimatorOverride(null)` Must Fire on Weapon Remove — Not Optional

`RefreshWeapon()` starts by destroying `_weaponVisual` and resetting it to null **every time** (even if nothing was equipped). The `ApplyAnimatorOverride(null)` call must be placed immediately after `_weaponVisual = null`, **before** the early-return null-check:

```csharp
private void RefreshWeapon()
{
    if (_weaponVisual != null)
        Destroy(_weaponVisual);
    _weaponVisual = null;
    ApplyAnimatorOverride(null); // ← BEFORE the early-return below

    var weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon);
    if (weapon == null || _weaponSocket == null) return;
    // ... attach path
}
```

If the call is placed after the early-return, unequipping a weapon (which calls `RefreshWeapon()` and returns early when `weapon == null`) will **never restore the default controller** — the override animation plays forever.

---

### `_defaultAnimatorController` Null → Animator Breaks

`ApplyAnimatorOverride(null)` sets `_animator.runtimeAnimatorController = _defaultAnimatorController`. If `_defaultAnimatorController` is null, this sets the controller to null → Animator has no clips → all parameters are gone → character freezes on the current animation frame.

**Always** assign `PlayerAnimatorController.controller` to `_defaultAnimatorController` in Inspector (AC 4). The WireEquipmentVisuals editor utility handles this automatically.

---

### Prefab Instantiation — Do NOT Destroy Colliders

`CreatePlaceholder()` destroys colliders on primitives because they interfere with physics. **Do not apply this pattern to instantiated prefabs.** `Sword base.prefab` may not have colliders now, but story 7-9 will add a `HitboxRoot` trigger child with a trigger collider shaped to the sword. Destroying colliders on prefab instances here would break story 7-9.

```csharp
// Only CreatePlaceholder() destroys colliders — NOT the prefab instantiation path
_weaponVisual = Object.Instantiate(weapon.equipVisualPrefab, _weaponSocket);
// No Object.Destroy(GetComponent<Collider>()) here!
```

---

### `(weapon as WeaponSO)?` Cast in `ApplyAnimatorOverride` Call

`_equipmentSystem.GetEquipped(EquipmentSlot.Weapon)` returns `ItemSO`. `equipVisualPrefab` is on `EquipableItemSO`, so `weapon.equipVisualPrefab` works without a cast. But `animatorOverrideController` is on `WeaponSO` only — use:

```csharp
ApplyAnimatorOverride((weapon as WeaponSO)?.animatorOverrideController);
```

The `as` cast is null-safe — if `weapon` is somehow not a `WeaponSO`, this passes `null` (no override applied). In practice this can't happen because `EquipmentSystem.Equip()` only puts `WeaponSO` instances into `EquipmentSlot.Weapon`.

---

### `ActiveWeaponGO` Property — For Story 7-9 Only

```csharp
public GameObject ActiveWeaponGO => _weaponVisual;
```

This property is exposed **exclusively** for `PlayerCombat` (story 7-9) to call `_equipmentVisuals.ActiveWeaponGO.GetComponentInChildren<WeaponHitbox>()`. Do not write any hitbox logic in this story. The property simply exposes the already-tracked `_weaponVisual` field.

`ActiveWeaponGO` returns:
- The instantiated `Sword base.prefab` GO when armed
- The placeholder cube GO when a weapon without `equipVisualPrefab` is equipped
- `null` when no weapon is equipped

---

### `EquipmentSystem.GetEquipped()` Returns `ItemSO` — Type Confirmed By Slot Logic

`EquipmentSystem.Equip()` enforces that only `WeaponSO` goes into `EquipmentSlot.Weapon` (lines 56–60 of `EquipmentSystem.cs`). So the `weapon` reference from `GetEquipped(EquipmentSlot.Weapon)` is always either `WeaponSO` or `null`. The `as WeaponSO` cast is safe and will never produce an unexpected null from a non-weapon.

---

### WireEquipmentVisuals.cs — Editor Utility Update

Story 7-4 created `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` to programmatically wire all EquipmentVisuals Inspector references on the Player prefab. Add wiring for both new fields:

```csharp
// _animator → Animator component on Player root
var animator = playerRoot.GetComponent<Animator>();
SerializedProperty animatorProp = equipVisualsSerObj.FindProperty("_animator");
animatorProp.objectReferenceValue = animator;

// _defaultAnimatorController → PlayerAnimatorController.controller asset
var defaultController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
    "Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller");
SerializedProperty controllerProp = equipVisualsSerObj.FindProperty("_defaultAnimatorController");
controllerProp.objectReferenceValue = defaultController;
```

---

### No Edit Mode Tests for Visual Components

`EquipmentVisuals` is a runtime-only visual component that requires Play Mode for `Animator`, `Renderer`, and `Transform` parenting. No Edit Mode unit tests. Play Mode validation (AC 6) covers observable behavior. This matches the pattern from story 7-4.

---

### Regression Risk — Placeholder Path Must Remain Intact

The null check `if (weapon.equipVisualPrefab != null)` must be the only branch condition — do not fall through to the `else` if instantiation fails. If `Object.Instantiate` throws (e.g. malformed prefab), the error will surface in the console — do not add a try/catch wrapper; let the error surface clearly.

---

### Project Structure Notes

**Files to MODIFY:**

```
Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs     ← add equipVisualPrefab field
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs            ← add animatorOverrideController field
Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs          ← prefab path + animator override + ActiveWeaponGO
Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs         ← add wiring for _animator + _defaultAnimatorController
Assets/_Game/Prefabs/Player/Player.prefab                   ← wire new EquipmentVisuals refs via editor utility
Assets/_Game/Data/Items/Weapon_TestSword.asset              ← assign equipVisualPrefab = Sword base.prefab
```

**Files NOT to modify:**

```
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs           ← no changes; GetEquipped() API unchanged
Assets/_Game/Scripts/Combat/PlayerCombat.cs                 ← hitbox binding is story 7-9, not this story
Assets/_Game/Data/Items/Armor_TestHelmet.asset              ← leave equipVisualPrefab null (placeholder retained)
Assets/_Game/Data/Items/Armor_TestArmor.asset               ← leave equipVisualPrefab null (body material swap retained)
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs             ← no changes needed
```

### References

- [Source: `_bmad-output/sprint-change-proposal-2026-03-21.md`#Section-4] — Detailed change specs for Changes A, B, C; authoritative source for all AC details
- [Source: `_bmad-output/implementation-artifacts/7-4-equipment-visual-update.md`] — Prior story implementing `EquipmentVisuals`, `WeaponSocket`, `HelmetSocket`, `WireEquipmentVisuals.cs`; full prior implementation context
- [Source: `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`] — Current file being modified; read before implementing
- [Source: `Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs`] — Class being modified; current field structure
- [Source: `Assets/_Game/ScriptableObjects/Items/WeaponSO.cs`] — Class being modified; current field structure
- [Source: `Assets/_Game/Scripts/Player/CLAUDE.md`#PlayerAnimator] — PlayerAnimator owns all Animator calls for player anims; `EquipmentVisuals` overriding `runtimeAnimatorController` directly is correct because it's a controller-level swap (not a trigger/bool) and outside PlayerAnimator's purview
- [Source: `Assets/_Game/Prefabs/CLAUDE.md`] — Player prefab structure; `Animator` is on the prefab root alongside `EquipmentVisuals`
- [Source: `_bmad-output/project-context.md`] — `Resources.Load()` banned; all refs via Inspector; `GameLog` for all logging; `[SerializeField] private` for Inspector fields

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Initial compilation failed: `GetEquipped()` returns `ItemSO`, not `EquipableItemSO` — fixed by adding `as EquipableItemSO` cast in both `RefreshWeapon()` and `RefreshHelmet()`.
- `manage_scriptable_object` MCP tool could not set object references via GUID — worked around by editing the `.asset` YAML directly with the correct fileID/guid pair (same root GO fileID as `worldItemPrefab`: `7470631476087896926`).

### Completion Notes List

- Added `equipVisualPrefab` field to `EquipableItemSO` with `[Header("Visuals")]`, above stat bonuses. All existing `.asset` files without the field default to null (placeholder path unchanged).
- Added `animatorOverrideController` field to `WeaponSO` with `[Header("Animation")]`. Null = keep default controller.
- Updated `EquipmentVisuals` with: `_animator` + `_defaultAnimatorController` fields, `ActiveWeaponGO` property, `ApplyAnimatorOverride()` helper, prefab instantiation paths in `RefreshWeapon()` and `RefreshHelmet()`. `ApplyAnimatorOverride(null)` fires in destroy path (before early-return) to guarantee controller restore on unequip.
- Updated `WireEquipmentVisuals.cs` to wire both new fields. Editor utility ran successfully: `animator=True, defaultController=True`.
- `Weapon_TestSword.asset` `equipVisualPrefab` → `SwordBase_Visual.prefab`. `animatorOverrideController` is null (default controller).
- `Sword base.prefab` renamed to `SwordBase_World.prefab` and moved to `Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/`. GUID preserved; `worldItemPrefab` reference on `Weapon_TestSword.asset` remains valid.
- `SwordBase_Visual.prefab` created in same folder: root is grip-point (no physics), `SM_Sword_1` nested prefab child with position/rotation offset baked in for hand alignment, BoxCollider (isTrigger=true) added to `SM_Sword_1` as a component override. Story 7-9 adds `WeaponHitbox.cs` to that same `SM_Sword_1` GO.
- Task 6 (Play Mode validation) is left for manual player validation per AC 6 checklist.

### File List

Assets/_Game/ScriptableObjects/Items/EquipableItemSO.cs
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs
Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs
Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs.meta
Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs
Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs.meta
Assets/_Game/Prefabs/Player/Player.prefab
Assets/_Game/Prefabs/CLAUDE.md
Assets/_Game/Data/Items/Weapon_TestSword.asset
Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_World.prefab
Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_World.prefab.meta
Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab
Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab.meta
Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase.meta
_bmad-output/implementation-artifacts/7-8-equipment-visual-prefab-support.md
_bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-03-21: Implemented story 7.8 — added `equipVisualPrefab` to `EquipableItemSO`, `animatorOverrideController` to `WeaponSO`, updated `EquipmentVisuals` with prefab instantiation, animator override, and `ActiveWeaponGO` property. Wired Player prefab.
- 2026-03-21: Replaced direct `Sword base.prefab` reference with dedicated `SwordBase_Visual.prefab` (mesh-only + isTrigger BoxCollider on `SM_Sword_1`, no Rigidbody/ItemPickup). Renamed `Sword base.prefab` → `SwordBase_World.prefab`, moved both to `Swords/SwordBase/` folder. Baked grip position/rotation offset into `SM_Sword_1` child.
- 2026-03-21: Code review fixes — added `Refresh()` to `OnEnable()` for initial visual sync on scene load; fixed misleading `OnDisable` comment; replaced `Debug.Log*` with `GameLog` in `WireEquipmentVisuals.cs`; renamed `SwordBase_World.prefab` internal GO from "Sword base" to "SwordBase_World"; updated `Assets/_Game/Prefabs/CLAUDE.md` (added `EquipmentVisuals` to Player hierarchy diagram, added GUID warning for `SwordBase_Visual.prefab.meta`); completed story File List with CLAUDE.md and all `.meta` files.
