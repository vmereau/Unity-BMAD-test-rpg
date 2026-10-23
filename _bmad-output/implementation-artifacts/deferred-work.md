
## From spec-items-create-command (2026-10-22)

- **Thin world items are hard to focus/pick up** — `ItemAssetBuilder` fits the BoxCollider exactly to the renderer
  bounds (lockpick: 0.15 × 0.019 × 0.045 m). Consider a minimum collider thickness (or a separate trigger/interaction
  collider) for thin items; tune after a play test. Also applies to `WeaponCreatorWindow` world prefabs, which
  additionally lack `InteractionHighlight` (pre-existing).

- **`NpcShopEditor` scene path untested** (from spec-npc-shop-dialogue-command review) — only the pure
  `ApplyStockChange` is covered. `SetStock` / `FindInstances` / dirty-scene + Play-mode refusal / `SerializedProperty`
  write-back have no automated test; would need a temp scene with an `Entity_base` instance in an EditMode test.

## From spec-lockpick-consumable-requirement (2026-10-22)

- **Action-bar HUD not refreshed after a non-UI inventory change** — `InventorySystem` raises no change event, so
  consuming a lockpick (`DoorSystem` / `ContainerSystem`) leaves `ActionBarUI` showing a stale stack count / stale
  slot until the next `OnActionBarUsed`. Hotkey use is safe (`HandleHotkeyPressed` now re-validates indices); a proper
  fix is an inventory-changed `GameEventSO` that `ActionBarUI` listens to.
- **Failed-lock toast spam** — each E press on a locked door raises a new "You need a lockpick" / skill toast; five
  presses fill the toast stack and evict XP / save toasts. Consider de-duplicating identical consecutive toasts in
  `NotificationToastUI`.
- **Quest log "new entry" marker** (from `spec-quest-log-info-panel-redesign`, 2026-10-23) — flag a quest row when
  a part / step became active since the player last viewed it. Needs a persisted per-quest "last seen" snapshot (save
  system) — out of scope for the redesign.
