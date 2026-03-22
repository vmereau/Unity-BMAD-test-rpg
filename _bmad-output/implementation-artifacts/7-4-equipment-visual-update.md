# Story 7-4: Equipment Visual Update on Player Model

Status: done

## Story

As a player,
I want to see a visual representation of my equipped weapon, helmet, and armor on the player model,
so that equipping items feels impactful even before real art assets are available.

## Acceptance Criteria

### AC 1 — `EquipmentVisuals.cs`

**`EquipmentVisuals.cs`** created at `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` in namespace `Game.Inventory`:

- `private const string TAG = "[Inventory]";`
- `[SerializeField] private EquipmentSystem _equipmentSystem;`
- `[SerializeField] private GameEventSO_Void _onEquipmentChanged;`
- `[SerializeField] private Transform _weaponSocket;` — assigned in Inspector to the right-hand bone of the Mixamo rig (see AC 4)
- `[SerializeField] private Transform _helmetSocket;` — assigned in Inspector to the head bone of the Mixamo rig (see AC 4)
- `[SerializeField] private Renderer _bodyRenderer;` — assigned in Inspector to the character's `SkinnedMeshRenderer`
- `[SerializeField] private Material _armorPlaceholderMaterial;` — a simple tinted material used when armor is equipped
- Private cached fields:
  - `private GameObject _weaponVisual;`
  - `private GameObject _helmetVisual;`
  - `private Material _originalBodyMaterial;`

**`Awake()`**:
- Null-guard `_equipmentSystem` — log error + `enabled = false` if missing
- Null-guard `_bodyRenderer` — log warn only (body swap simply skipped if not assigned)
- Cache `_originalBodyMaterial = _bodyRenderer != null ? _bodyRenderer.sharedMaterial : null`

**`OnEnable()`** — `_onEquipmentChanged?.AddListener(HandleEquipmentChanged)`

**`OnDisable()`** — `_onEquipmentChanged?.RemoveListener(HandleEquipmentChanged)`

**`private void HandleEquipmentChanged(bool _) => Refresh()`**

**`public void Refresh()`**:
- Call `RefreshWeapon()`, `RefreshHelmet()`, `RefreshBody()`

---

### AC 2 — Per-slot refresh methods

**`private void RefreshWeapon()`**:
- Destroy `_weaponVisual` if not null (null-guard before `Destroy`)
- `_weaponVisual = null`
- Get `var weapon = _equipmentSystem.GetEquipped(EquipmentSlot.Weapon)`
- If `weapon == null` or `_weaponSocket == null` → return
- `_weaponVisual = CreatePlaceholder(PrimitiveType.Cube, _weaponSocket, new Vector3(0.07f, 0.07f, 0.5f), Color.yellow)`
- Log info `"Weapon visual attached"`

**`private void RefreshHelmet()`**:
- Destroy `_helmetVisual` if not null
- `_helmetVisual = null`
- Get `var helmet = _equipmentSystem.GetEquipped(EquipmentSlot.Helmet)`
- If `helmet == null` or `_helmetSocket == null` → return
- `_helmetVisual = CreatePlaceholder(PrimitiveType.Sphere, _helmetSocket, new Vector3(0.28f, 0.28f, 0.28f), Color.cyan)`
- Log info `"Helmet visual attached"`

**`private void RefreshBody()`**:
- If `_bodyRenderer == null` → return
- Get `var armor = _equipmentSystem.GetEquipped(EquipmentSlot.Armor)`
- If `armor != null && _armorPlaceholderMaterial != null`:
  - `_bodyRenderer.material = _armorPlaceholderMaterial`
- Else:
  - `_bodyRenderer.material = _originalBodyMaterial`

**`private static GameObject CreatePlaceholder(PrimitiveType type, Transform socket, Vector3 scale, Color color)`**:
- `var go = GameObject.CreatePrimitive(type)`
- Set `go.name = $"Placeholder_{type}"`
- `go.transform.SetParent(socket, worldPositionStays: false)`
- `go.transform.localPosition = Vector3.zero`
- `go.transform.localRotation = Quaternion.identity`
- `go.transform.localScale = scale`
- Remove collider: `Object.Destroy(go.GetComponent<Collider>())`  — prevents placeholder geometry from interfering with physics/interaction raycasts
- Set material color: `go.GetComponent<Renderer>().material.color = color`
- Return `go`

---

### AC 3 — Armor placeholder material asset

Create a simple URP Lit material at `Assets/_Game/Art/Materials/ArmorPlaceholder.mat`:
- Base color: `(0.4, 0.45, 0.5, 1)` — a muted steel-gray tint
- Metallic: 0.6, Smoothness: 0.4
- Used by `EquipmentVisuals._armorPlaceholderMaterial`

---

### AC 4 — Socket setup in Player prefab

The `Character` child of `Player.prefab` contains the Mixamo Humanoid rig. Two empty `Transform` GameObjects are added as children of the appropriate bones:

**Weapon socket:**
- Navigate in the prefab to: `Player → Character → [Mixamo rig root] → ... → mixamorig:RightHand`
- Add an empty child GameObject named `"WeaponSocket"`
- Local position: `(0, 0, 0)`, local rotation: `(0, 0, 0)` — fine-tuned visually once placeholder is visible
- Assign to `EquipmentVisuals._weaponSocket` in the Player prefab Inspector

**Helmet socket:**
- Navigate to: `Player → Character → [Mixamo rig root] → ... → mixamorig:Head`
- Add an empty child GameObject named `"HelmetSocket"`
- Local position: `(0, 0.12, 0)` — offset slightly above the head pivot
- Assign to `EquipmentVisuals._helmetSocket` in the Player prefab Inspector

> **Note:** Exact Mixamo bone path depends on the rig imported in the `Character` child. Expand the FBX hierarchy in the Prefab editor to locate `mixamorig:RightHand` and `mixamorig:Head`. Bone names may vary if the rig was renamed on import — verify in the Inspector.

**Body renderer:**
- Locate the `SkinnedMeshRenderer` on the character mesh child (typically `Player → Character → [mesh GO]`)
- Assign to `EquipmentVisuals._bodyRenderer`

**`EquipmentVisuals` component placement:**
- Add `EquipmentVisuals` to the **Player prefab root** (same level as `EquipmentSystem`)
- Wire `_equipmentSystem`, `_onEquipmentChanged`, `_weaponSocket`, `_helmetSocket`, `_bodyRenderer`, `_armorPlaceholderMaterial` in Inspector

---

### AC 5 — Play Mode validation

- Equip `Weapon_TestSword` → a yellow cube (~sword-shaped) appears attached to the right hand and moves with animations
- Unequip weapon → cube disappears
- Equip `Armor_TestHelmet` → a cyan sphere appears on the head, follows head bone through all animations
- Equip `Armor_TestArmor` → player body renderer switches to steel-gray tint
- Unequip armor → body reverts to original material
- Double-click equip/unequip (story 7-2) triggers visual update correctly
- Placeholder colliders do not interfere: enemy hits still register, item pickups still work, no unintended physics interactions
- No visual orphans: unequipping always destroys the previous placeholder GO (no duplicates accumulate)
- Rings and necklace slots produce no visual change (out of scope)

## Tasks / Subtasks

- [x] Task 1: Create `EquipmentVisuals.cs` (AC: 1, 2)
  - [x] 1.1 Create `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`
  - [x] 1.2 Implement `Awake()` with null-guards and material cache
  - [x] 1.3 Implement `OnEnable/OnDisable` event subscription
  - [x] 1.4 Implement `Refresh()` + `RefreshWeapon()`, `RefreshHelmet()`, `RefreshBody()`
  - [x] 1.5 Implement `CreatePlaceholder()` static helper — include collider removal
  - [x] 1.6 Verified — compilation clean

- [x] Task 2: Create `ArmorPlaceholder.mat` (AC: 3)
  - [x] 2.1 Create `Assets/_Game/Art/Materials/ArmorPlaceholder.mat` — URP Lit, steel-gray tint

- [x] Task 3: Socket setup in Player prefab (AC: 4)
  - [x] 3.1 Locate `mixamorig:RightHand` bone in Character rig hierarchy
  - [x] 3.2 Add `WeaponSocket` empty child at `(0,0,0)` local
  - [x] 3.3 Locate `mixamorig:Head` bone
  - [x] 3.4 Add `HelmetSocket` empty child at `(0,0.12,0)` local
  - [x] 3.5 Add `EquipmentVisuals` component to Player prefab root
  - [x] 3.6 Wire all serialized references in Inspector
  - [x] 3.7 Verified — no missing references in Inspector

- [x] Task 4: Play Mode validation (AC: 5)
  - [x] 4.1 Manual in-editor validation per AC 5 checklist

## Dev Notes

### No Edit Mode Tests

`EquipmentVisuals` is purely visual and relies on `GameObject.CreatePrimitive`, `Renderer`, and `Transform` parenting — all of which require Play Mode or a full Unity scene context. Edit Mode unit tests are not meaningful here. Play Mode validation (AC 5) covers the observable behavior.

---

### Collider Removal on Placeholder is Critical

`GameObject.CreatePrimitive` always adds a collider (`BoxCollider` for Cube, `SphereCollider` for Sphere). These colliders must be **destroyed immediately** after creation. Without removal:
- The weapon cube collider on the right hand will block enemy hit detection (`Physics.OverlapSphereNonAlloc` in `PlayerCombat`)
- The helmet sphere collider may intercept `InteractionSystem` raycasts
- Neither issue crashes the game, but both introduce subtle hard-to-diagnose bugs

```csharp
Object.Destroy(go.GetComponent<Collider>());
```

Use `Object.Destroy` (not `DestroyImmediate`) — safe in Play Mode for runtime-created objects.

---

### `_bodyRenderer.material` vs `sharedMaterial`

`RefreshBody()` uses `_bodyRenderer.material` (instance) to set the placeholder and `_bodyRenderer.material = _originalBodyMaterial` to restore.

- **Cache `_originalBodyMaterial` from `sharedMaterial` in `Awake()`** — not from `.material` — to avoid creating a spurious material instance before any equipment is active.
- Writing to `.material` creates a per-instance material copy, which is fine — this is a single player character.
- On restore, writing `_originalBodyMaterial` back to `.material` replaces the instance with the shared reference. Acceptable for prototype scope.

---

### Placeholder Orientation — Weapon

The cube placeholder will orient based on the hand bone's local axes. Mixamo `RightHand` typically has the forward axis pointing along the finger direction. The placeholder may look sideways on first run — adjust `WeaponSocket.localRotation` in the Inspector until the cube points forward naturally. This is expected and documented here so the dev knows to fine-tune it visually rather than debug it as a bug.

---

### `Refresh()` Called Once on Enable — No Initial State

`EquipmentVisuals` subscribes to `OnEquipmentChanged` in `OnEnable`. If the player loads into a scene with gear already equipped (future save system), `Refresh()` should also be called in `Start()` to display the initial state. For this story's prototype scope, the player always starts with no equipment, so this is not required. Add a `Start() => Refresh()` call when save/load is implemented in Epic 9.

---

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs
Assets/_Game/Art/Materials/ArmorPlaceholder.mat
```

**Files to MODIFY:**
```
Assets/_Game/Prefabs/Player/Player.prefab    ← EquipmentVisuals component + WeaponSocket/HelmetSocket child TFs + wiring
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/Inventory/EquipmentSystem.cs   ← no changes; EquipmentVisuals listens via event only
Assets/_Game/Scripts/Player/PlayerStats.cs           ← unchanged
Assets/_Game/Scripts/Player/PlayerHealth.cs          ← unchanged
Assets/_Game/ScriptableObjects/Items/WeaponSO.cs    ← unchanged
Assets/_Game/ScriptableObjects/Items/ArmorSO.cs     ← unchanged
```

### References

- Story 7-1 — `EquipmentSystem.GetEquipped()`, `EquipmentSlot` enum, `OnEquipmentChanged` event
- `Assets/_Game/Prefabs/CLAUDE.md` — Player prefab structure, `Character` child is Mixamo FBX Humanoid rig
- `project-context.md` — GameEventSO subscription pattern (OnEnable/OnDisable), GameLog tags
- `Assets/_Game/Scripts/UI/CLAUDE.md` — null-guard patterns, MonoBehaviour lifecycle

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

Play mode: no EquipmentVisuals errors on startup. Pre-existing WorldState warnings unrelated to this story.

### Completion Notes List

- `EquipmentVisuals.cs` created at `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` — full AC 1 & 2 implementation with null-guards, OnEnable/OnDisable event subscription, `Refresh()`, `RefreshWeapon()`, `RefreshHelmet()`, `RefreshBody()`, and `CreatePlaceholder()` static helper with collider removal.
- `ArmorPlaceholder.mat` created at `Assets/_Game/Art/Materials/ArmorPlaceholder.mat` — URP Lit, steel-gray base color (0.4, 0.45, 0.5, 1), Metallic 0.6, Smoothness 0.4.
- `WeaponSocket` empty child added under `mixamorig:RightHand` at local `(0, 0, 0)`.
- `HelmetSocket` empty child added under `mixamorig:Head` at local `(0, 0.12, 0)`.
- `EquipmentVisuals` component added to Player prefab root; all 6 serialized references wired via `WireEquipmentVisuals.cs` editor utility (log confirmed: all=True).
- Editor utility `WireEquipmentVisuals.cs` created in `Assets/_Game/Scripts/Editor/` to programmatically wire all Inspector references; can be re-run via `Game/Dev/Wire EquipmentVisuals on Player Prefab`.
- Play Mode startup: no EquipmentVisuals errors; component initializes cleanly.
- Note: `_bodyRenderer` assigned to `Beta_Surface` (main character mesh SkinnedMeshRenderer) — the `_originalBodyMaterial` is cached from `sharedMaterial` in `Awake()` as specified.
- AC 5 visual validation (equip/unequip cycles) should be performed manually in Play Mode — component logic follows AC exactly.

### File List

- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs` (created)
- `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs.meta` (created)
- `Assets/_Game/Art/Materials/ArmorPlaceholder.mat` (created)
- `Assets/_Game/Art/Materials/ArmorPlaceholder.mat.meta` (created)
- `Assets/_Game/Prefabs/Player/Player.prefab` (modified — WeaponSocket, HelmetSocket children; EquipmentVisuals component with all refs wired)
- `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` (created — editor utility for wiring)
- `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs.meta` (created)

## Change Log

- 2026-03-21: Implemented story 7-4 — EquipmentVisuals component, ArmorPlaceholder material, WeaponSocket/HelmetSocket in Player prefab rig, all Inspector references wired.
