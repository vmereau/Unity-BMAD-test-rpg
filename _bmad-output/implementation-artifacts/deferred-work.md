
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

## From spec-cooking-experiment-and-recipes (2026-10-23)

- `CookingUI.RefreshRecipes` destroys / re-instantiates every recipe row on each refresh (incl. hotkey presses) — fine at 2 recipes, pool rows if the list grows.
- Quest Explorer counts a `RecipeSO` as its fact's setter even when no `CookingStation` lists it — a quest gated on it could never progress without a validator warning.
- Two station recipes with the same ingredient multiset: the first always wins, the second can never be discovered; no warning yet.
- `PlayerRewardSO.RewardFactType` has no Recipe entry — recipe discovery can't trigger a reward yet.
- `Item_AppleMushroomSandwich` reuses the bread model (`SM_FoodKitBread_01a`); the 5 new foods have no icons (gray placeholders).

## From spec-recipe-items-and-craft-count (2026-10-23 review)

- **Recipe item for a recipe no station lists** — `RecipeItemSO` sets the fact even when no `CookingStation._recipes` contains its `RecipeSO`; the player pays for a recipe that never shows in any Recipes tab. Add a validator (Quest Explorer or `ItemCatalog` sync) that every recipe-item recipe is cooked somewhere.
- **Inventory selection after the last unit is used** (pre-existing, `InventoryUI.RefreshSlots`) — using a stack of 1 keeps `_selectedSlotIndex`, so the detail panel jumps to whichever item shifted into that index. Clear or re-target the selection when the selected slot's item changes.

## From spec-meal-permanent-stat-bonus (2026-10-23 review)

- **Meal claim vs max-HP bonus restore in two load phases** — `SaveSystem.BeginLoad` restores facts (incl. `Meal.BonusClaimed.*`) before the region reload, `PlayerSaveAdapter` restores `bonusMaxHealth` after. If the reload fails (`FailLoad`), facts are already replaced while the player keeps its bonus → the meal can be claimed again. Pre-existing atomicity gap of facts vs player state; restore facts on `FailLoad` or apply both together.
- **Detail panel stale after an action-bar use while the inventory is open** (pre-existing for recipe items too) — `ActionBarSystem` use doesn't repaint `InventoryUI`'s detail, so a just-claimed "First meal" row stays green until reselect.

## From spec-quest-item-rewards-trout-recipe (2026-10-24 review)

- **Dialogue-type rewards re-fire on every node replay** (pre-existing; worse with item rewards) — `WorldStateManager.SetFact` raises `OnDialoguePlayed` even when the fact is already true, so `PlayerRewards.HandleDialoguePlayed` re-applies XP / gold / items. Only an inspector warning guards item rewards for now; apply Dialogue rewards on the false→true transition (or a claim key).
- **Spider quest tracks 2 spiders, text says "All five"** (pre-existing, Quest Explorer V13) — `Mem_Guard_Spiders_Already_Killed` / step 1 use two `KilledFact`s; the "I've already killed the spiders" path completes the quest without it being started. Align facts and text.
- **Quest reward race during load** — `RestoreFacts` (`BeginLoad`) runs before `QuestEventsManager.ReseedState` (`FinishLoad`); a fact change during the async region reload could fire `OnQuestCompleted` and its rewards / toasts. Reseed right after `RestoreFacts` or ignore fact changes while loading.

## From spec-game-clock-and-bed-sleep (2026-10-26)

Follow-up features built on `GameClock` (split out of the time-tracking request):

- **Status effects** — shipped as `spec-status-effects-food-regen` (2026-10-26); follow-ups in its section below.
- **NPC routines** — stateless `NpcScheduleSO` blocks (`start → end : activity @ waypoint`, Gothic daily-routine style)
  evaluated from the time of day; NPCs re-evaluate on `OnHourChanged` / `OnTimeSkipped` and snap to their block on region
  load (no off-screen simulation). Gating (asleep trainer can't teach) reads the current activity. Region-scene NPCs can't
  serialize a ref to Core's `GameClock` — they need the time from the events or a new read path (decide then).
  Supersedes the `bool isDay` / `OnDayNightChanged` plan in `game-architecture.md`.
- **Day/night lighting** — sun / ambient driven by `GameClock` time of day (the empty `DayNightController` stub GO in
  Core.unity).
- **Wait anywhere** — explicitly not wanted for now (beds only); revisit if needed.
- `SleepUI` is a world screen like `CookingUI`: the pre-existing "character tabs open over world screens" gap applies
  (I / J / K / P during the sleep panel or fade).

### Review findings deferred (spec-game-clock-and-bed-sleep, 2026-10-26)

- **Hour listeners after a load / skip** — `GameClock.Restore` raises nothing and `SkipTo` raises only the final hour, so
  future hour consumers (routines, lighting) must re-evaluate from `Now` on `OnLoadFinished` / `OnTimeSkipped` instead of
  relying on every `OnHourChanged`; hour-specific triggers inside a skipped span are not delivered. Decide when the first
  listener lands.
- **Failed load leaves the restored clock** — `BeginLoad` restores the time before the region reload and `FailLoad`
  doesn't roll it back (same pre-existing gap as facts / recipe crafts).
- **Sleep hostile gate is distance-only** — no line of sight / awareness: a hostile behind a wall or a passive
  hostile-faction animal within `sleepHostileRadius` blocks the bed. Revisit with real bed placements.
- **World keeps running under the sleep panel and fade** — the player is busy (cursor unlocked) but enemies still act;
  the header time is captured at open and goes stale; a hostile arriving during the 0.5 s fade isn't re-checked at the skip.
  Same pattern as cooking / containers; consider pausing gameplay (or the clock) while `SleepUI.IsOpen`.

## From spec-status-effects-food-regen (2026-10-26)

- **Effect scope not built yet** — stat modifiers, a damage kind (poison: should it bypass Defense?), more stacking
  policies (extend / keep-stronger), effect tags, NPC / enemy hosts (poisoned weapons). Regen potions maybe later;
  magic undecided.
- **`SE_WellFed` has no icon** — HUD shows an amber square; needs an icon prompt (`ui:create` / `items:icon-prompt`).
- **Food still reads "Potion"** in the item detail category — a `Food` label (or a split item class) would read better.
- **Failed load leaves restored state** — same `FailLoad` gap as the clock: effects aren't touched by `BeginLoad`, but
  the clock is, so a failed load can make current effects catch up / pay against the restored time.
- **No host-level tests** (from review) — `StatusEffectHost` Capture → Restore round-trip, invalid / unknown entry
  rejection, clear on death and the `IsLoading` skip are covered by a manual play smoke test only; an EditMode test with
  a stub catalog + clock would lock them in.
