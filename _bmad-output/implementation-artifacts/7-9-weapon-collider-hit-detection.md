# Story 7.9: Weapon Collider Hit Detection

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want attacks with an equipped weapon to hit only enemies within the weapon's actual physical reach (based on its trigger collider shape),
so that combat feels physically accurate and different weapon types have meaningful spatial differences.

## Acceptance Criteria

1. Given a sword is equipped, when the player performs an attack, enemies are only hit if they fall within the weapon prefab's trigger collider region — not a generic sphere around the player origin
2. Given no weapon is equipped (unarmed), when the player attacks, the existing `Physics.OverlapSphereNonAlloc` fallback still detects and damages enemies
3. Given a weapon prefab has no `WeaponHitbox` component (e.g., placeholder cube), the system silently falls back to sphere overlap — backward compatible
4. Given a weapon is equipped while the player is alive/active, the hitbox is bound immediately on `OnEquipmentChanged` — no stale hitbox references persist between weapon swaps
5. Given a weapon is unequipped, the old hitbox is unsubscribed and its colliders disabled so they cannot trigger unexpected hits
6. Given the player swaps from one weapon to another, the old hitbox is fully unbound before the new hitbox is bound — no double-hit events
7. No regressions on existing done stories (7-1 through 7-4, 7-8): inventory, stat effects, equipment visuals, and animator override must still function

## Tasks / Subtasks

- [x] Task 1: Create `WeaponHitbox.cs` component (AC: 1, 2, 3)
  - [x] Create `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` in namespace `Game.Combat`
  - [x] In `Awake()`: call `GetComponentsInChildren<Collider>(includeInactive: true)` to collect all child colliders; immediately call `Disable()` so hitbox starts dormant
  - [x] Expose `public event System.Action<EnemyHealth> OnEnemyHit;`
  - [x] Add `public void Enable()`: iterate `_colliders`, set each `enabled = true`
  - [x] Add `public void Disable()`: iterate `_colliders`, set each `enabled = false`
  - [x] Implement `OnTriggerEnter(Collider other)`: call `other.GetComponentInParent<EnemyHealth>()`, if non-null and not dead → raise `OnEnemyHit?.Invoke(health)`

- [x] Task 2: Add `WeaponHitbox` to `SwordBase_Visual.prefab` (AC: 1)
  - [x] Open `Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab`
  - [x] On `SM_Sword_1` child: ensure `BoxCollider` is present and set `isTrigger = true`; size/center the collider to match the sword blade geometry
  - [x] Add `WeaponHitbox` component to `SM_Sword_1` (same GO as the trigger collider)
  - [x] Verify root `SwordBase_Visual` GO has no colliders (physics-clean root)

- [x] Task 3: Update `PlayerCombat.cs` — hitbox binding and attack routing (AC: 1, 2, 3, 4, 5, 6)
  - [x] Add `[SerializeField] private EquipmentVisuals _equipmentVisuals;`
  - [x] Add `[SerializeField] private GameEventSO_Void _onVisualsRefreshed;` (bound to `OnVisualsRefreshed.asset` — see Dev Notes; not `_onEquipmentChanged`, to avoid the GameEventSO reverse-iteration race)
  - [x] Add `private WeaponHitbox _activeHitbox;`
  - [x] In `OnEnable()`: subscribe `_onVisualsRefreshed?.AddListener(HandleVisualsRefreshed)` — SO raised at end of `Refresh()` so `ActiveWeaponGO` is valid when callback fires
  - [x] In `OnDisable()`: `_onVisualsRefreshed?.RemoveListener(HandleVisualsRefreshed)`; call `UnbindWeaponHitbox()` unconditionally
  - [x] Implement `private void HandleVisualsRefreshed(bool _)`: call `UnbindWeaponHitbox()`; if `_equipmentVisuals == null` return; get `_equipmentVisuals.ActiveWeaponGO`; if null return; `GetComponentInChildren<WeaponHitbox>()`; if non-null subscribe `_activeHitbox.OnEnemyHit += OnWeaponHit`; else `GameLog.Warn` sphere-fallback notice
  - [x] Implement `private void UnbindWeaponHitbox()`: if `_activeHitbox != null` → `_activeHitbox.Disable()` → `_activeHitbox.OnEnemyHit -= OnWeaponHit` → `_activeHitbox = null`
  - [x] Implement `private void OnWeaponHit(EnemyHealth health)`: call `health.TakeDamage(ComputeEffectiveDamage())`; `GameLog.Info(TAG, $"Weapon hit: {health.gameObject.name}")`
  - [x] In `TryAttack()` at hit-detection point: replace / branch the existing `ExecuteHitDetection()` call → if `_activeHitbox != null` call `_activeHitbox.Enable()`; else call `ExecuteHitDetection()` (unarmed sphere fallback)
  - [x] In ALL combo-end/reset paths (finisher completion, combo window expiry, stamina denied block): add `_activeHitbox?.Disable()`

- [x] Task 4: Wire `Player.prefab` serialized references (AC: 1, 4)
  - [x] Open `Assets/_Game/Prefabs/Player/Player.prefab`
  - [x] On the `PlayerCombat` component, assign `_equipmentVisuals` → the `EquipmentVisuals` component already on the Player prefab
  - [x] On the `PlayerCombat` component, assign `_onVisualsRefreshed` → `Assets/_Game/Data/Events/OnVisualsRefreshed.asset`
  - [x] On the `EquipmentVisuals` component, assign `_onVisualsRefreshed` → `Assets/_Game/Data/Events/OnVisualsRefreshed.asset`

- [x] Task 5: Play-mode validation (AC: 1–7)
  - [x] Equip sword → swing at enemy in close range → enemy takes damage
  - [x] Equip sword → swing at enemy out of sword reach → no damage registered
  - [x] Unequip sword → swing at enemy → sphere fallback activates, damage dealt
  - [x] Swap weapons mid-combat → no double-hit, correct hitbox bound after swap
  - [x] Open Console → no errors or warnings introduced by this story
  - [x] Confirm inventory panel, stat bonuses (7-3), equipment visuals (7-4/7-8) still work

## Dev Notes

### Architecture Overview

This story wires the weapon prefab physics (set up in 7-8) into the attack pipeline.

```
EquipmentVisuals.Refresh() [called by OnEquipmentChanged event or OnEnable]
  → instantiates weapon visual prefab
  → fires EquipmentVisuals.OnVisualsRefreshed (C# event)
    → PlayerCombat.HandleVisualsRefreshed()
      → EquipmentVisuals.ActiveWeaponGO (property exposed in 7-8)
        → GetComponentInChildren<WeaponHitbox>()
          → subscribe to WeaponHitbox.OnEnemyHit
            → PlayerCombat.OnWeaponHit(EnemyHealth)
              → health.TakeDamage(ComputeEffectiveDamage())
```

During an attack, `TryAttack()` calls `_activeHitbox.Enable()` which activates the trigger
collider(s) on the weapon prefab child. Unity physics fires `OnTriggerEnter` on `WeaponHitbox`
when the collider overlaps an enemy collider. Deduplication within a single swing is implicit:
after the combo step resolves, all combo-end paths call `_activeHitbox?.Disable()`, preventing
further triggers until the next attack.

**Key design decision — `EquipmentVisuals.OnVisualsRefreshed` instead of `OnEquipmentChanged`:**
`PlayerCombat` subscribes to `EquipmentVisuals.OnVisualsRefreshed` (a C# event on the component)
rather than directly to the `OnEquipmentChanged` GameEventSO. This guarantees `ActiveWeaponGO`
is already set when the hitbox bind runs. Subscribing directly to `OnEquipmentChanged` caused
a race: `PlayerCombat.OnEnable` fires before `EquipmentVisuals.OnEnable` (component order on
the same GO), and `GameEventSO.Raise()` iterates listeners in reverse-add order — so ordering
depended on timing of subscriptions rather than logic. The C# event approach eliminates that
entirely.

### Critical Code Patterns

#### WeaponHitbox.cs:

```csharp
namespace Game.Combat
{
    public class WeaponHitbox : MonoBehaviour
    {
        public event System.Action<EnemyHealth> OnEnemyHit;
        private Collider[] _colliders;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
            Disable();
        }

        public void Enable()  { foreach (var c in _colliders) c.enabled = true; }
        public void Disable() { foreach (var c in _colliders) c.enabled = false; }

        private void OnTriggerEnter(Collider other)
        {
            var health = other.GetComponentInParent<EnemyHealth>();
            if (health != null && !health.IsDead)
                OnEnemyHit?.Invoke(health);
        }
    }
}
```

#### PlayerCombat.cs — actual implemented members:

```csharp
// Serialized field (no GameEventSO_Void needed — binding via EquipmentVisuals.OnVisualsRefreshed)
[SerializeField] private EquipmentVisuals _equipmentVisuals;

// Runtime field
private WeaponHitbox _activeHitbox;

// OnEnable — subscribe to C# event on EquipmentVisuals (fires after Refresh() completes)
if (_equipmentVisuals != null) _equipmentVisuals.OnVisualsRefreshed += HandleVisualsRefreshed;

// OnDisable
if (_equipmentVisuals != null) _equipmentVisuals.OnVisualsRefreshed -= HandleVisualsRefreshed;
UnbindWeaponHitbox();

// In TryAttack()
if (_activeHitbox != null)
    _activeHitbox.Enable();
else
    ExecuteHitDetection(); // Unarmed sphere fallback

// In ALL combo-end paths
_activeHitbox?.Disable();
```

#### New private methods in PlayerCombat.cs:

```csharp
private void HandleVisualsRefreshed()
{
    UnbindWeaponHitbox();
    if (_equipmentVisuals == null) return;
    var weaponGO = _equipmentVisuals.ActiveWeaponGO;
    if (weaponGO == null) return; // Unarmed
    _activeHitbox = weaponGO.GetComponentInChildren<WeaponHitbox>();
    if (_activeHitbox != null)
        _activeHitbox.OnEnemyHit += OnWeaponHit;
}

private void UnbindWeaponHitbox()
{
    if (_activeHitbox == null) return;
    _activeHitbox.Disable();
    _activeHitbox.OnEnemyHit -= OnWeaponHit;
    _activeHitbox = null;
}

private void OnWeaponHit(EnemyHealth health)
{
    health.TakeDamage(ComputeEffectiveDamage());
    GameLog.Info(TAG, $"Weapon hit: {health.gameObject.name}");
}
```

#### EquipmentVisuals.cs additions (story 7-9):

```csharp
// New C# event — fired at the end of every Refresh()
public event System.Action OnVisualsRefreshed;

// Added to end of Refresh():
OnVisualsRefreshed?.Invoke();
```

### Key Integration Points from Story 7-8

- `EquipmentVisuals.ActiveWeaponGO` — exposes the currently instantiated weapon prefab GO.
  Found in `Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs`
- `EquipmentVisuals.OnVisualsRefreshed` — C# event added in 7-9. Subscribe here, NOT to
  `OnEquipmentChanged`, to guarantee `ActiveWeaponGO` is valid when the callback fires.
- `SwordBase_Visual.prefab` hierarchy:
  ```
  SwordBase_Visual (root — kinematic Rigidbody, no colliders)
  └── SM_Sword_1 (child — mesh + BoxCollider isTrigger=true + WeaponHitbox)
  ```

### Weapon Prefab Requirements (for current and future weapons)

1. **Root GO**: kinematic `Rigidbody` (`isKinematic=true`, `useGravity=false`) — **required** for
   `OnTriggerEnter` to fire against enemies that have no Rigidbody (static colliders). Without
   it, the trigger is treated as a static trigger and will NOT detect static enemy colliders.
2. **Root GO**: NO physics colliders (grip/attachment point only)
3. **Child GO containing mesh**: `BoxCollider` (`isTrigger=true`) shaped to weapon blade
4. **Child GO**: `WeaponHitbox` component on same GO as the trigger collider
5. If no `WeaponHitbox` exists on the hierarchy → sphere fallback auto-activates (no crash)

### Physics Gotcha — Kinematic Rigidbody Required on Weapon

Unity's trigger system requires at least one `Rigidbody` in an `OnTriggerEnter` pair. The weapon
is a child of the player's weapon socket (whose root has a `CharacterController`, not a
`Rigidbody`). Enemy colliders are static (no Rigidbody). Without a kinematic Rigidbody on the
weapon prefab, `OnTriggerEnter` will fire against the player's own CharacterController capsule
but NOT against enemy colliders.

### Combo End Paths in PlayerCombat.cs

ALL locations where the combo resets need `_activeHitbox?.Disable()`:
- Finisher (3rd hit) completes
- Combo window timeout in `Update()`
- Stamina denied in `TryAttack()`
- `OnBlockStarted()` combo interrupt
- `OnDisable()` cleanup (via `UnbindWeaponHitbox()`)

Missing even one causes the hitbox to stay enabled between attacks, producing phantom hits.

### Unarmed Fallback Preservation

`ExecuteHitDetection()` (sphere overlap) must be preserved unchanged. It is called when
`_activeHitbox == null` (unarmed, or weapon prefab has no `WeaponHitbox` component).

### Player.prefab Wiring

`PlayerCombat` requires only one serialized reference for hitbox:
- `_equipmentVisuals` → `EquipmentVisuals` component on the same Player root GO
- `_onEquipmentChanged` is **not** needed (removed — binding goes through `OnVisualsRefreshed`)

### Project Structure Notes

- `WeaponHitbox.cs` → `Assets/_Game/Scripts/Combat/` (same folder as `PlayerCombat.cs`, within
  `Game.Combat` namespace — same-system direct reference is acceptable per architecture rules)
- All assets under `Assets/_Game/` per project folder root rule
- No changes needed to `Game.asmdef` — `Game.Combat` is already in the same assembly

### References

- [Source: _bmad-output/game-architecture.md#Combat System] — combat architecture, hit detection, event pattern
- [Source: _bmad-output/implementation-artifacts/7-8-equipment-visual-update.md] — `ActiveWeaponGO` property, weapon prefab hierarchy, `EquipmentVisuals` changes
- [Source: Assets/_Game/Scripts/Combat/PlayerCombat.cs] — current `TryAttack()`, `ExecuteHitDetection()`, `ComputeEffectiveDamage()`, combo state machine
- [Source: Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs] — `ActiveWeaponGO` property, `_onEquipmentChanged` event subscription pattern
- [Source: Assets/_Game/Scripts/Inventory/EquipmentSystem.cs] — confirms `_onEquipmentChanged.Raise()` called in `Equip()` and `Unequip()`
- [Source: Assets/_Game/Data/Events/OnEquipmentChanged.asset] — event SO, already exists, `GameEventSO_Void` type
- [Source: _bmad-output/project-context.md#Architecture Patterns] — cross-system comms via GameEventSO only, event subscribe in OnEnable/OnDisable
- [Source: _bmad-output/sprint-change-proposal-2026-03-21.md] — full specification and rationale for this story

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Implemented `WeaponHitbox.cs` — new MonoBehaviour in `Game.Combat` namespace; colliders start disabled in `Awake()`, enabled only during attack window via `Enable()`/`Disable()`; `OnTriggerEnter` fires `OnEnemyHit` event when a live enemy is hit
- Updated `SwordBase_Visual.prefab`: `SM_Sword_1` child BoxCollider set to `isTrigger=true` (size 0.07×0.07×0.8, center offset 0.35 along Z); `WeaponHitbox` component added to same GO; root has no colliders
- Updated `PlayerCombat.cs`: added `_equipmentVisuals` + `_onVisualsRefreshed` serialized fields; `_activeHitbox` private field; `HandleVisualsRefreshed(bool _)`/`UnbindWeaponHitbox`/`OnWeaponHit` private methods; `OnEnable` subscribes to `_onVisualsRefreshed` SO; `OnDisable` unsubscribes and calls `UnbindWeaponHitbox()` before `_input` null guard; `TryAttack()` routes to hitbox or sphere fallback; `_activeHitbox?.Disable()` added to all 4 combo-end paths (finisher, window expiry, stamina denied, OnDisable)
- Updated `EquipmentVisuals.cs`: added `ActiveWeaponGO` property and `_onVisualsRefreshed` SO field; `Refresh()` calls `_onVisualsRefreshed?.Raise(false)` at end — subscribers see a valid `ActiveWeaponGO`
- Created `OnVisualsRefreshed.asset` (`GameEventSO_Void`) — cross-system event channel replacing the plain C# event (architecture compliance)
- Wired `Player.prefab`: `PlayerCombat._equipmentVisuals` → `EquipmentVisuals` component; `PlayerCombat._onVisualsRefreshed` + `EquipmentVisuals._onVisualsRefreshed` → `OnVisualsRefreshed.asset` (guid `2360a371ed2298b42981d2b6ed275384`)
- All 199 existing EditMode tests pass — no regressions

### File List

- Assets/_Game/Scripts/Combat/WeaponHitbox.cs (new)
- Assets/_Game/Scripts/Combat/PlayerCombat.cs (modified)
- Assets/_Game/Scripts/Inventory/EquipmentVisuals.cs (modified)
- Assets/_Game/Prefabs/Items/Weapons/Swords/SwordBase/SwordBase_Visual.prefab (modified)
- Assets/_Game/Prefabs/Player/Player.prefab (modified)
- Assets/_Game/Data/Events/OnVisualsRefreshed.asset (new)
- Assets/_Game/Scenes/TestScene.unity (modified — play-test residue from Task 5 validation)
- _bmad-output/implementation-artifacts/sprint-status.yaml (modified)

## Change Log

- 2026-03-21: Implemented weapon collider hit detection (Story 7.9) — WeaponHitbox component, SwordBase_Visual prefab update, PlayerCombat hitbox routing, Player.prefab wiring. All 199 tests pass.
- 2026-03-22: Code review fixes — replaced cross-system C# event with `OnVisualsRefreshed` GameEventSO_Void (architecture compliance); removed dead TAG constant from WeaponHitbox; added sphere-fallback warning log; corrected task documentation and completion notes.
