# CLAUDE.md — Assets/_Game/Scripts/Inventory

> Loaded when Claude accesses files in this folder. Inventory, equipment, action bar,
> and world pickups. Namespace: `Game.Inventory`.

---

## What's here

| File | Role |
|------|------|
| `InventorySystem` | Stacked inventory. Backed by an immutable `InventorySlot` struct (item + count) — mutate by replacing the slot in the list, never in place. |
| `EquipmentSystem` | Equipped items per `EquipmentSlot`. `GetEquipped(slot)` is the query other systems use (e.g. `PlayerCombat` casts the Weapon slot to `WeaponSO`). |
| `EquipmentVisuals` | Spawns/reparents equipped-item visuals on the player rig. Raises `OnVisualsRefreshed` SO at the **end** of `Refresh()`; owns combat-state socket swap via `SetCombatState()`. |
| `ActionBarSystem` | Action-bar slot bindings (`ActionBarSlotData`). |
| `ItemPickup` | `IInteractable` world item — adds to inventory on interact. |

---

## Rules

- **Cross-system events use `GameEventSO<T>` only** — no plain C# events across `Game.Inventory` → `Game.Combat`/UI boundaries (project-context.md).
- `EquipmentVisuals` raises `OnVisualsRefreshed` **after** `_weaponVisual` is assigned, so `ActiveWeaponGO` is valid in listeners. `PlayerCombat` binds its hitbox off this SO, NOT `OnEquipmentChanged` (component-order race) — details in `Scripts/Combat/CLAUDE.md`.
- `GameEventSO<T>.Raise()` iterates listeners **last-added-first** — never rely on listener ordering for correctness.
