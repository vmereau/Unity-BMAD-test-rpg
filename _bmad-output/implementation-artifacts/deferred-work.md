
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

## From spec-harvestable-plants (2026-10-23 review)

- **Interaction cast buffers vs dense plant patches** — `InteractionSystem` sphere casts use 16-entry NonAlloc
  buffers on layers 8 + 9; a dense patch of harvestable triggers in `nameRange` can push NPCs / items out of the
  focus candidates and flicker name tags. Enlarge the buffers or cast layer 9 separately when patches get dense.
- **`ItemIconGenerator` preview wait has no timeout** (pre-existing) — if `AssetPreview` never returns a texture for
  one source, `EditorApplication.update` polls forever and later runs report "Already running" until a domain
  reload. Add a frame cap that skips items whose preview stays null.

## From spec-combo-lock-and-lock-on-fixes (2026-10-23 review)

- **Lock-on collider layout** — ragdoll bone colliders sit on layer 6 (Characters), the lock-on layer. The 64-slot buffer covers ~5 spiders; a dense fight could still truncate. Proper fix: one dedicated lock-on collider per entity, ragdoll bones on another layer.
- **Late combo press consumes stamina** — a press during the uninterruptible blend-out to `CombatIdle` is dropped cleanly but its stamina is spent. Options: retime `ComboWindowClose` to ≤ the exit time (clip events), or refund on a dropped queued step.
- **Stall watchdog vs `Animator.speed`** — `attackStallTimeout` uses scaled time; a future hit-stop / attack-speed modifier (animator speed ≠ 1) could false-trigger it. Scale or skip the check when the player Animator speed changes.
- **Dodge during a combo blend** — both combo transitions use `interruptionSource = None`; verify AnyState → Dodging still cuts in mid-blend (otherwise the roll animation lags the dodge movement by up to the 0.42 s blend).

## From spec-cooking-stations (2026-10-23 review)

- **Character tabs open over world screens** (pre-existing, `ContainerUI` too) — `UIScreenManager.OpenTab` doesn't check
  `ContainerUI` / `CookingUI` `IsOpen`; I / J / K / P opens a tab over them and closing it re-locks the cursor while the
  world screen stays visible (camera turns, player can walk away, the next Esc both closes it and opens the Game Menu).
  Fix: an "any world screen open" gate in `OpenTab` (or close the world screen first).
- **`ItemSlotUI` drag ghost leaks when its panel closes mid-drag** (pre-existing) — `OnEndDrag` isn't delivered to an
  inactive slot, so a ghost on the root canvas stays forever. Add `OnDisable` → `RemoveGhostImage()` in `ItemSlotUI`.
