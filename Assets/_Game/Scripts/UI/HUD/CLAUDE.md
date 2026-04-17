# CLAUDE.md — Assets/_Game/Scripts/UI/HUD

> Always-visible in-game overlay rendered on the HUD Canvas (sortingOrder below menus).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `HealthBarUI` | Horizontal fill bar showing player current/max HP. Subscribes to `GameEventSO_Float _onPlayerHealthChanged`; reads max from `CombatConfigSO`. |
| `StaminaBarUI` | Horizontal fill bar showing player stamina. Subscribes to `GameEventSO_Float _onPlayerStaminaChanged` (value is already a normalized 0–1 ratio — no config reference needed). |
| `ActionBarUI` | Manages 6 `ActionBarSlotUI` children. Subscribes to `GameEventSO_Int _onActionBarUsed`. Requires exactly 6 slots wired in Inspector. |
| `ActionBarSlotUI` | Individual action-bar slot. Supports drag-and-drop between inventory and action bar, hover highlight, key-label display, and stack count. |

---

## Event Channels

- `GameEventSO_Float` — `OnPlayerHealthChanged` (raised by `PlayerHealth`, current HP value)
- `GameEventSO_Float` — `OnPlayerStaminaChanged` (raised by `StaminaSystem`, normalized 0–1)
- `GameEventSO_Int` — `OnActionBarUsed` (raised externally when a slot is activated by key)

---

## ActionBarSlotUI — Drag & Drop

- Implements all five drag interfaces: `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler`, `IPointerClickHandler`.
- Ghost image is parented to the root Canvas and has `raycastTarget = false`.
- `_dropHandled` flag prevents double-destroy when both source `OnEndDrag` and target `OnDrop` fire in the same frame.
- `SlotIndex` (action bar position) and `InventoryIndex` (backing inventory slot, -1 if empty) are set via `Initialize()`.

---

## Gotchas

- `ActionBarUI` sets `enabled = false` in `Awake` if dependencies are missing — any `OnDisable` path that touches `_actionBarSystem` must null-guard first.
- `HealthBarUI` and `StaminaBarUI` both set `enabled = false` in `Awake` if `_fillImage` is null — guard accordingly.
