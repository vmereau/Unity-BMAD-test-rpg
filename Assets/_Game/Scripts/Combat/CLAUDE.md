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
3. Finisher executed (last hit in combo)
4. `Update` — attack window timer expires
5. `OnBlockStarted` — block interrupts combo

Missing any of these paths leaves the hitbox enabled = phantom hits persist after the attack animation ends.

### Unarmed Fallback

When `_activeHitbox == null` (no weapon equipped, or weapon prefab has no WeaponHitbox), `PlayerCombat` falls back to `ExecuteHitDetection()` — a sphere overlap cast from the player's position. This covers fist attacks and any weapon without a configured hitbox.

---

## Code Review Checklist — Combat Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `_activeHitbox` not disabled on all combo-end paths — phantom hits will persist between attacks |
| HIGH | Subscribing to `_onEquipmentChanged` directly in `PlayerCombat` — `ActiveWeaponGO` is null due to GameEventSO reverse-iteration order; use `_onVisualsRefreshed` SO raised at the end of `Refresh()` |
| HIGH | Using a plain C# event across `Game.Inventory` → `Game.Combat` boundary — architecture mandates `GameEventSO<T>` for all cross-system events |
| MEDIUM | `WeaponHitbox` placed on the weapon prefab root instead of the mesh child — root has the kinematic Rigidbody; collider must be on a child so `OnTriggerEnter` resolves the correct GameObject |
| HIGH | Weapon visual prefab missing a kinematic `Rigidbody` on its root — static trigger + static collider = `OnTriggerEnter` never fires (see Prefabs/Enemies/CLAUDE.md) |
