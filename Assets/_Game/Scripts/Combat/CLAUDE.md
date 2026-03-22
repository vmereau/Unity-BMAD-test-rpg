# CLAUDE.md — Assets/_Game/Scripts/Combat

> Loaded when Claude accesses files in this folder. Covers the WeaponHitbox system and PlayerCombat attack pipeline.

---

## WeaponHitbox System (Story 7.9)

### Overview

`WeaponHitbox` is placed on the **weapon mesh child GO** inside a weapon visual prefab (e.g. `SM_Sword_1` inside `SwordBase_Visual`). It manages trigger collider(s) that represent the weapon's hit zone.

- Colliders start **disabled** (dormant) — enabled only during an active attack frame window
- `Enable()` / `Disable()` are called by `PlayerCombat` at the start/end of each attack window
- `OnTriggerEnter` fires `OnEnemyHit` C# event if `EnemyHealth` is found in the collided object's parent hierarchy

### Binding Pattern — OnVisualsRefreshed GameEventSO

`PlayerCombat` subscribes to `GameEventSO_Void _onVisualsRefreshed` (`Assets/_Game/Data/Events/OnVisualsRefreshed.asset`). `EquipmentVisuals` raises this SO at the very end of `Refresh()`, after `_weaponVisual` is assigned — so `ActiveWeaponGO` is always valid when the callback fires.

Both components hold `[SerializeField] private GameEventSO_Void _onVisualsRefreshed;` wired to the same asset in the Inspector.

```csharp
// PlayerCombat OnEnable
_onVisualsRefreshed?.AddListener(HandleVisualsRefreshed);

// PlayerCombat OnDisable (before _input null guard)
_onVisualsRefreshed?.RemoveListener(HandleVisualsRefreshed);
UnbindWeaponHitbox();

private void HandleVisualsRefreshed(bool _)  // GameEventSO_Void uses Action<bool>
{
    UnbindWeaponHitbox();
    var weaponGO = _equipmentVisuals.ActiveWeaponGO;
    if (weaponGO == null) return;
    _activeHitbox = weaponGO.GetComponentInChildren<WeaponHitbox>();
    if (_activeHitbox != null)
        _activeHitbox.OnEnemyHit += OnWeaponHit;
    else
        GameLog.Warn(TAG, $"WeaponHitbox not found on {weaponGO.name} — using sphere fallback");
}

// EquipmentVisuals.Refresh() — end of method
_onVisualsRefreshed?.Raise(false);
```

**Why not `_onEquipmentChanged`?** `GameEventSO<T>.Raise()` iterates `_listeners` in **reverse** (last-added fires first). `PlayerCombat` (component index 7 on Player GO) subscribes before `EquipmentVisuals` (component index 22), so it would be called BEFORE `EquipmentVisuals.Refresh()` ran — making `ActiveWeaponGO` null. The dedicated `OnVisualsRefreshed` SO is raised from within `Refresh()` after `_weaponVisual` is set, eliminating the race entirely.

**Why not a plain C# event?** Plain `System.Action` events across `Game.Inventory` → `Game.Combat` violate the committed architecture rule: cross-system communication must use typed `GameEventSO<T>` channels only.

### Combo-End Disable Requirement

`_activeHitbox?.Disable()` must be called on **every** path that ends an attack combo:
1. `TryAttack` — stamina deny
2. `TryAttack` — `Consume()` fail
3. Finisher executed (last hit in combo) — via `ResetAttackCombo()`
4. `OnComboWindowClose()` animation event — combo window expired; `ResetAttackCombo()` called
5. `OnBlockStarted` — block interrupts combo; `ResetAttackCombo()` called

Missing any of these paths leaves the hitbox enabled = phantom hits persist after the attack animation ends.

---

## AnimationEventReceiver System (Story 7.10)

### Overview

`AnimationEventReceiver` is a bridge MonoBehaviour on the **Player root GO** (same GO as the `Animator`). Unity's animation event system calls public methods on components on the same GameObject — `AnimationEventReceiver` receives them and routes to `PlayerCombat`.

```
Attack clip → fires "ComboWindowOpen" at ~50% normalized time
  → AnimationEventReceiver.ComboWindowOpen()
    → PlayerCombat.OnComboWindowOpen()
      → _comboWindowOpen = true

Attack clip → fires "ComboWindowClose" at ~90% normalized time
  → AnimationEventReceiver.ComboWindowClose()
    → PlayerCombat.OnComboWindowClose()
      → ResetAttackCombo() (if window was open)
```

### Combo Window Gate in TryAttack()

The old `_comboWindowDelay > 0f` timer guard is replaced by:
```csharp
if (_stateManager.IsAttacking && !_comboWindowOpen)
{
    // Attack input ignored — waiting for combo window (animation event)
    return;
}
```
`IsAttacking` is `true` from `SetAttacking(true)` until `SetAttacking(false)` — this blocks rapid re-triggering of Attack_1 before the combo window opens.

### comboSteps Query

`PlayerCombat` caches `_currentWeaponSO` in `HandleVisualsRefreshed()`:
```csharp
_currentWeaponSO = _equipmentSystem?.GetEquipped(EquipmentSlot.Weapon) as WeaponSO;
```
`ManageComboStep()` queries it:
```csharp
int maxSteps = _currentWeaponSO != null ? _currentWeaponSO.comboSteps : 3;
if (_comboStep < maxSteps - 1) IncreaseAttackCombo(); else ResetAttackCombo();
```
Unarmed fallback: `maxSteps = 3` (3-hit sphere combo unchanged).

### Animation Events on FBX Clips (Story 7.11)

Events are stored in the FBX `.meta` file under `clipAnimations[].events`. The `time` field is **normalized time** (0.0–1.0). All four events are set on all 3 attack clips:

| Event | Normalized time | Effect |
|---|---|---|
| `HitboxEnable` | `0.25` | Activates weapon collider — hit window opens |
| `HitboxDisable` | `0.50` | Deactivates weapon collider — hit window closes |
| `ComboWindowOpen` | `0.50` | Combo chain input accepted |
| `ComboWindowClose` | `0.90` | Combo chain resets |

`HitboxDisable` and `ComboWindowOpen` fire at the same normalized time (0.5). Unity dispatches them in listed order — both are independent so order is safe.

Attack clips: `AttackLeft.fbx` (Attack_1), `AttackOverhead.fbx` (Attack_2), `AttackThrust.fbx` (Attack_3).

**Timing tuning:** To adjust hit feel, edit `time` values in the `.meta` file. `HitboxEnable` ~0.25 = weapon reaches target zone; `HitboxDisable` ~0.50 = weapon retracts. AttackThrust (thrust) may need `HitboxEnable` as low as 0.15 due to faster connect.

### Hitbox Pipeline — Fully Event-Driven (Story 7.11)

After Story 7.11, `ExecuteAttack()` does **not** call `_activeHitbox.Enable()` directly. The hitbox is entirely driven by animation events:

```
Attack input → ExecuteAttack() → animator trigger → clip plays
  → HitboxEnable at ~25% → AnimationEventReceiver.HitboxEnable()
    → PlayerCombat.OnHitboxEnable() → _activeHitbox?.Enable()
  → HitboxDisable at ~50% → AnimationEventReceiver.HitboxDisable()
    → PlayerCombat.OnHitboxDisable() → _activeHitbox?.Disable()
```

**Finisher fix:** Before Story 7.11, the finisher (Attack_3) had a zero-frame hit window because `_activeHitbox.Enable()` and `_activeHitbox?.Disable()` (via `ResetAttackCombo()`) were called in the same frame. Story 7.11 resolves this by deferring enable to the animation event at ~25%.

### Unarmed Fallback

When `_activeHitbox == null` (no weapon equipped, or weapon prefab has no WeaponHitbox), `PlayerCombat` falls back to `ExecuteHitDetection()` — a sphere overlap cast from the player's position, fired immediately on input frame. This covers fist attacks and any weapon without a configured hitbox.

---

## Code Review Checklist — Combat Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `_activeHitbox` not disabled on all combo-end paths — phantom hits will persist between attacks |
| HIGH | Subscribing to `_onEquipmentChanged` directly in `PlayerCombat` — `ActiveWeaponGO` is null due to GameEventSO reverse-iteration order; use `_onVisualsRefreshed` SO raised at the end of `Refresh()` |
| HIGH | Using a plain C# event across `Game.Inventory` → `Game.Combat` boundary — architecture mandates `GameEventSO<T>` for all cross-system events |
| MEDIUM | `WeaponHitbox` placed on the weapon prefab root instead of the mesh child — root has the kinematic Rigidbody; collider must be on a child so `OnTriggerEnter` resolves the correct GameObject |
| HIGH | Weapon visual prefab missing a kinematic `Rigidbody` on its root — static trigger + static collider = `OnTriggerEnter` never fires (see Prefabs/Enemies/CLAUDE.md) |
| HIGH | `AnimationEventReceiver` function name mismatch — Unity finds receiver methods by exact string match on the same GO as the Animator; typo = silent no-op, not a compile error |
| HIGH | `ScriptableObject.CreateInstance<WeaponSO>()` in new code or tests — `WeaponSO` is abstract (Story 7.10); use a concrete subclass like `SwordSO` |
| MEDIUM | New weapon SO added without a concrete subclass (e.g. inheriting `WeaponSO` directly via `[CreateAssetMenu]`) — `WeaponSO` is abstract and has no `[CreateAssetMenu]`; all new weapon types need a concrete class in `ScriptableObjects/Items/Weapons/` |
