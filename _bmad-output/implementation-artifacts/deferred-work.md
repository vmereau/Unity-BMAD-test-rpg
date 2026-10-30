
## From spec-items-create-command (2026-10-22)

- **Thin world items are hard to focus/pick up** — `ItemAssetBuilder` fits the BoxCollider exactly to the renderer
  bounds (lockpick: 0.15 × 0.019 × 0.045 m). Consider a minimum collider thickness (or a separate trigger/interaction
  collider) for thin items; tune after a play test. Also applies to `WeaponCreatorWindow` world prefabs, which
  additionally lack `InteractionHighlight` (pre-existing).

- **`NpcShopEditor` scene path untested** (from spec-npc-shop-dialogue-command review) — only the pure
  `ApplyStockChange` is covered. `SetStock` / `FindInstances` / dirty-scene + Play-mode refusal / `SerializedProperty`
  write-back have no automated test; would need a temp scene with an `Entity_base` instance in an EditMode test.

## From spec-lockpick-consumable-requirement (2026-10-22)

- ~~**Action-bar HUD not refreshed after a non-UI inventory change**~~ — resolved by spec-ui-state-hygiene (2026-10-28).
- ~~**Failed-lock toast spam**~~ — resolved by spec-ui-state-hygiene (2026-10-28).
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

- ~~**Character tabs open over world screens**~~ — resolved by spec-ui-state-hygiene (2026-10-28).
- ~~**`ItemSlotUI` drag ghost leaks when its panel closes mid-drag**~~ — resolved by spec-ui-state-hygiene (2026-10-28).

## From spec-cooking-experiment-and-recipes (2026-10-23)

- `CookingUI.RefreshRecipes` destroys / re-instantiates every recipe row on each refresh (incl. hotkey presses) — fine at 2 recipes, pool rows if the list grows.
- Quest Explorer counts a `RecipeSO` as its fact's setter even when no `CookingStation` lists it — a quest gated on it could never progress without a validator warning.
- Two station recipes with the same ingredient multiset: the first always wins, the second can never be discovered; no warning yet.
- `PlayerRewardSO.RewardFactType` has no Recipe entry — recipe discovery can't trigger a reward yet.
- `Item_AppleMushroomSandwich` reuses the bread model (`SM_FoodKitBread_01a`); the 5 new foods have no icons (gray placeholders).

## From spec-recipe-items-and-craft-count (2026-10-23 review)

- **Recipe item for a recipe no station lists** — `RecipeItemSO` sets the fact even when no `CookingStation._recipes` contains its `RecipeSO`; the player pays for a recipe that never shows in any Recipes tab. Add a validator (Quest Explorer or `ItemCatalog` sync) that every recipe-item recipe is cooked somewhere.
- ~~**Inventory selection after the last unit is used**~~ — resolved by spec-ui-state-hygiene (2026-10-28).

## From spec-meal-permanent-stat-bonus (2026-10-23 review)

- **Meal claim vs max-HP bonus restore in two load phases** — `SaveSystem.BeginLoad` restores facts (incl. `Meal.BonusClaimed.*`) before the region reload, `PlayerSaveAdapter` restores `bonusMaxHealth` after. If the reload fails (`FailLoad`), facts are already replaced while the player keeps its bonus → the meal can be claimed again. Pre-existing atomicity gap of facts vs player state; restore facts on `FailLoad` or apply both together.
- ~~**Detail panel stale after an action-bar use while the inventory is open**~~ — resolved by spec-ui-state-hygiene (2026-10-28).

## From spec-quest-item-rewards-trout-recipe (2026-10-24 review)

- **Dialogue-type rewards re-fire on every node replay** (pre-existing; worse with item rewards) — `WorldStateManager.SetFact` raises `OnDialoguePlayed` even when the fact is already true, so `PlayerRewards.HandleDialoguePlayed` re-applies XP / gold / items. Only an inspector warning guards item rewards for now; apply Dialogue rewards on the false→true transition (or a claim key).
- **Spider quest tracks 2 spiders, text says "All five"** (pre-existing, Quest Explorer V13) — `Mem_Guard_Spiders_Already_Killed` / step 1 use two `KilledFact`s; the "I've already killed the spiders" path completes the quest without it being started. Align facts and text.
- **Quest reward race during load** — `RestoreFacts` (`BeginLoad`) runs before `QuestEventsManager.ReseedState` (`FinishLoad`); a fact change during the async region reload could fire `OnQuestCompleted` and its rewards / toasts. Reseed right after `RestoreFacts` or ignore fact changes while loading.

## From spec-game-clock-and-bed-sleep (2026-10-26)

Follow-up features built on `GameClock` (split out of the time-tracking request):

- **Status effects** — shipped as `spec-status-effects-food-regen` (2026-10-26); follow-ups in its section below.
- **NPC routines** — shipped as `spec-npc-routines-day-night` (2026-10-27); follow-ups in its section below.
- ~~**Day/night lighting**~~ — shipped as `spec-day-night-cycle` (2026-10-26).
- **Wait anywhere** — explicitly not wanted for now (beds only); revisit if needed.
- ~~`SleepUI` tabs-over-world-screen gap~~ — resolved by spec-ui-state-hygiene (2026-10-28).

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
- ~~**Food still reads "Potion"**~~ — resolved by spec-cooking-ingredient-categories (Ingredient / Meal / Potion).
- **Failed load leaves restored state** — same `FailLoad` gap as the clock: effects aren't touched by `BeginLoad`, but
  the clock is, so a failed load can make current effects catch up / pay against the restored time.
- **No host-level tests** (from review) — `StatusEffectHost` Capture → Restore round-trip, invalid / unknown entry
  rejection, clear on death and the `IsLoading` skip are covered by a manual play smoke test only; an EditMode test with
  a stub catalog + clock would lock them in.

## From spec-cooking-ingredient-categories (2026-10-26)

- **Short-slot cue lost** — Experiment slots no longer show a count; when the same item fills more slots than the
  player owns, nothing marks which slot is short (only "Not enough ingredients"). Tint the short slot instead.
- **Recipe input without `isIngredient`** — hidden from the cooking grid but still cookable from the Recipes tab;
  `CookingStation` only warns. Consider a validator error (Quest Explorer-style) for such recipes.
- **Stale grid child on same-frame refresh** — `RefreshGrid` uses `Destroy` (end of frame), so a hotkey refresh and a
  double-click in one frame can resolve an old slot (pre-existing with the old `GetChild`). Unparent before destroy.

## From spec-day-night-cycle (2026-10-26)

- **Region scene played alone has no light** — StartingTown's directional light moved to Core; nothing bootstraps
  Core when a region is played on its own. Add an editor play-mode bootstrap (or an editor-only preview light).
- **Editor vs build sky differ** — builds keep Core active (built-in Default-Skybox); the editor often runs with
  StartingTown active (`SkyboxLite`). Pick the intended skybox and set it on Core (or `SetActiveScene(region)`).
- **Reflection cubemap not re-rendered** — night only scales `reflectionIntensity`; a `DynamicGI.UpdateEnvironment`
  on daylight steps (or a custom night cubemap) would tint reflections properly.
- **No day/night event** — `OnDayNightChanged` (`GameEventSO_Bool`) for NPC schedules / torches when they arrive.

## From spec-npc-routines-day-night (2026-10-27)

- **Activities** — only `NpcActivity.Stand`; forging / eating / sleeping need animations (driver hook) and spot props.
- **Dialogue / trade gating by activity** — read `NpcRoutine.CurrentActivity` (a sleeping trainer can't teach; "come
  back in the morning"). Today NPCs can be talked to anywhere, any time.
- **Doors** — NPCs can't open doors; only the two door-less village houses are used. `DoorInteractable` houses
  (`House`) need NPCs to open / close (and ignore their own lock) on the way.
- **`OnDayNightChanged`** (`GameEventSO_Bool`) still not built — routines poll the hour; torches / audio may want it.
- **Snap is visible** — after a sleep skip or a load, NPCs in view teleport. Acceptable for now (Gothic does the same).
- **Hour granularity** — blocks are whole hours; minute precision or per-NPC jitter (not all leaving at 21:00 sharp)
  would look more natural.

## From spec-interaction-wall-occlusion (2026-10-28)

- **Initial-overlap bypass:** a sphere-cast hit with distance 0 skips the occlusion probe, so with the camera pushed against a thin wall an interactable within `scanRadius` (0.5 m) on the far side is still usable. Fix: probe toward `collider.ClosestPoint(origin)` / `bounds.center` for distance-0 hits (spec I/O row currently says "visible").
- **Self-exemption through walls:** a candidate whose own Default collider crosses a wall (e.g. `CookingStation_Fireplace` convex hull poking out of the house wall) counts as visible from outside. Playtest; fix with non-convex / trimmed colliders on such props.
- **Convex prop hulls hide contents:** items placed inside a convex-`MeshCollider` prop (shelf, open crate, barrel) are permanently occluded. Audit StartingTown item placements.
- **Camera vs body line of sight:** occlusion is tested from the OTS camera, so items round a doorframe corner the camera sees past are reachable. Consider a second probe from the player's chest.
- **Interactables never occlude:** closed doors (Interactable layer) don't hide loot behind them (Default-only mask chosen 2026-10-28).
- **PlayMode coverage:** no PlayMode test drives `IsOccluded` with real colliders (wall, trigger wall, self collider).

## From spec-ui-state-hygiene (2026-10-28 review)

- **Inventory grid rebuilt several times per action** — `OnPlayerInventoryChanged` fires per mutation, so an equip from the
  open grid rebuilds it 3× (AddItem, RemoveItem, `OnEquipmentChanged`) and a hotkey consume 2× (channel + `OnActionBarUsed`);
  each rebuild destroys / re-instantiates every slot. Coalesce (dirty flag consumed once per frame) or pool slot rows.
- **Listener exception mid-transfer** — `GameEventSO.Raise` has no try/catch and now runs between the two halves of
  `ContainerUI.PutItem` / `NPCTradeUI.SellItem` / `EquipmentSystem.Equip`; a throwing listener (only on misconfigured
  prefabs today) would skip the second half (item lost / duplicated). Isolate listener invokes in `Raise` or raise once
  after the whole operation.
- **Cooking refund vs action-bar binding** — `CookingUI.TryCraft` consumes per ingredient; each consume now re-validates
  the bar, so a failed craft that refunds an action-bar-bound last unit leaves that bar slot cleared. Rare (`HasIngredients`
  is checked first). `CookingUI.NotifyInventoryChanged`'s `REFRESH_ONLY` raise is now partly redundant with the channel.
- **Tab gate trusts the raw cursor state** — `UIScreenManager.OpenTab` ignores I/J/K/P whenever no tab is open and the
  cursor is unlocked; an unlock not owned by a screen (Editor Esc / focus loss, `CameraController` disabled before its
  `Lock()`) would block tabs. Same assumption as `PlayerStateManager.IsBusy` / `_wasGameplayLastFrame`; playtest, and
  switch to an explicit "world screen open" counter if it bites.

## From spec-bow-ranged-weapon (2026-10-29 review)

- Bow launch origin can sit inside a wall when the player hugs it; a sweep starting inside a non-convex MeshCollider / terrain reports nothing → the arrow passes through. Fall back to a chest/camera point when line of sight to the bow is blocked.
- Stuck arrows aren't parented to what they hit — an arrow in a door / moving object floats when it moves.
- In-flight projectiles live in the active scene (Core) and survive a quickload / region change (lifetime ≤ 5 s).

## 2026-10-29 — from spec-player-draw-sheathe-animations review

- **Load (alive) mid-draw leaves the Attack layer in `Draw_State`.** `PlayerSaveAdapter` forces the sheathed stance but
  only the death path rebinds the Animator, so the leftover draw clip finishes (cosmetic), and an R pressed within
  ~0.4 s can latch `DrawWeapon` (AnyState → `Draw_State` has no self-transition) and be completed early by the leftover
  `WeaponSwap` event. Fix idea: play the Attack layer's locomotion state on load (bridge method), or rebind.

## From spec-bow-aim-draw-fire (2026-10-29 review)

- **Release + re-press within one animator update loses the nock** — `StartAim` clears `_arrowNocked`, but the Attack
  layer never leaves `BowAim_State`, so no new enter arrives; shots wait for the `bowNockTimeout` watchdog (2.5 s). Fix:
  track `BowAim_State` enter/exit in the SMB and keep the nock when the layer is still in it.
- **F9 quickload while aiming** — `IsBusy` doesn't cover `SaveSystem.IsLoading` (pre-existing for every action); aim and
  shots stay live during the async reload until `Restore` sheathes. Add `IsLoading` to `IsBusy` or stop the aim on
  `OnLoadStarted`.
- **Lock-on + aim** — the lock-on branch keeps facing the target (no `aimBodyYawOffset`) and `TrackLockedTarget` ignores
  the mouse, so aim sensitivity / pose correction have no effect while locked on. Decide: block lock-on while aiming, or
  apply the offset there.
- **No shot buffering** — a click during `Draw arrow` / `Fire arrow` is dropped (by design for now); consider buffering one
  shot until the nock if play tests feel unresponsive.

## From spec-bow-aim-spine-correction (2026-10-29 review)

- **Calibration drift** — `BowAimCorrection._arrowLineLocal` / `_arrowOriginLocal` are sampled from `Aim Overdraw` on the
  current player rig; a body swap or re-imported / retimed bow clip leaves stale values with no warning. Add an editor
  calibrate button that stores the source clip + avatar and a validator that flags a mismatch.
- **Lock-on + aim** (see the bow-aim entry above) — the correction still aims the chest at the crosshair while locked on,
  but the body keeps facing the target without the stance yaw, so large yaw deltas may hit the clamp.

## From spec-bow-left-hand-socket (2026-10-29)

- **Arrow spawn inside close walls** — `PlayerCombat.GetLaunchOrigin` = `ActiveWeaponGO`, now the left-hand bow grip (~0.85 m ahead of the draw hand). Against a wall the spawn point can already be inside the collider, so the arrow's first `SphereCast` misses it. Consider sweeping from the chest/right hand to the bow on fire, or a backed-off origin.

## From spec-smithing-skill-and-anvil (2026-10-29 review)

- **Reward lost on full inventory** — `PlayerRewards` ignores a failed `AddItem` while the one-shot `DialogueFact` is still set, so a full inventory at "What should I forge first?" loses `Item_Recipe_ShortIronSword` for good (pre-existing for every item reward; the trout recipe has the same risk).
- **Sole recipe source can be sold / dropped** — recipe items are sellable and droppable; selling the smithing recipe unread is recoverable only by buying it back from Edric. Consider quest-item flags (unsellable) or a re-ask fallback.
- **Old saves keep the old NPC stock** — `SaveableObject` restores Edric's saved inventory, so a save made before this change has no ingots / sword.
- **Finite shop stock** — no restock: 10 ingots = 5 forged swords. Needs a restock system before smithing scales.
- **No feedback on a failed Forge** — `CookSelectedRecipe` refunds silently when the output can't be added (full inventory); the Recipes tab has no result text.
- **Smithing screen wording** — recipe detail still says "Ingredients"; the tab bar shows a lone "Recipes" tab; forged / standard swords share the placeholder `sword.png` icon and the recipe item reuses the trout-recipe icon.
- **Prompt doesn't show the skill gate** — the anvil prompt reads "Forge" even without Smithing (refused on interact with a toast).

## From spec-world-screens-over-hud (2026-10-29) — world-screen UX / readability suggestions

Trade, container and crafting screens (and the shared `ItemDetailPanel`):

- **No backdrop** — panels are `#1A1A1A` at 85 % alpha with no full-screen dim, so a bright day scene bleeds through
  the panels and the gaps between them. Add a full-screen dim (black ~55–65 %) behind each world screen; consider panel
  alpha ~92 %.
- **Default Unity buttons** — Buy / Sell / Take / Put / Cook / Craft and the Experiment / Recipes tabs use the white
  `Background` sprite with black 30 pt text: off-style against the dark Gothic panels and `UI_SlotWell`. Make a dark /
  brass 9-slice button (`ui:create`) with light text and clear hover / pressed / disabled states; Buy should look
  disabled when unaffordable.
- **Active crafting tab not obvious** — reuse the brass active tint from the inventory filter tabs.
- **Small / italic secondary text** — category line 16 pt `#A0A0A0` (borderline contrast) and italic descriptions
  16–17 pt grey; aim for ≥ 18 pt and less italic body text at 1080p.
- **Colour coding** — keep gold price `#E6C25C` and green / red stat polarity; apply green / red have / missing to the
  recipe "Requires 2/2" ingredient lines (currently plain grey) and tint the header gold amounts with the price gold.
- **Inventory tab** — the HUD `StatusEffectBar` (y 173–213 @1080p) overlaps the inventory `GoldFooter` (y 148–178)
  when effects are active; move one of them.
- **HUD hidden under attack** (review) — world screens don't pause, so HP loss while looting / crafting is invisible
  until death. Consider closing the world screen (or re-showing the HUD) when the player takes damage.
- **Action-bar hotkeys live behind world screens** (pre-existing) — `ActionBarSystem` hotkeys 1–6 aren't gated on
  open screens, and the bar is now hidden while trading / looting; and the hidden action bar no longer accepts drops
  from the trade / container grids. Gate hotkeys while the cursor is unlocked.

## Stance follow-ups (from spec-ai-stance-stagger-knockdown, 2026-10-30)

- ~~**Player stance**~~ — done in `spec-player-stance-stagger-knockdown` (2026-10-30).
- **Armor stance** — heavy armor raises max stance (`ArmorSO` weight / stance bonus), maybe with a stamina-regen cost.
- **Two-handed archetype** — high stance damage, `canKnockDown` on; needs grip / clips.
- **Ragdoll knockdown** — physics ragdoll + get-up blend in its own spec (today: animation-based fall).
- **Knocked-down payoff** — a skill for bonus damage / a riposte on a knocked-down target.
- **Weapon-specific stagger clips** — `stagger 1/2` (unarmed) play for every weapon; sword / axe variants via overrides.
- **Monster stagger clips** — monsters fall back to `GetHit` + a timed lock.
- **Stance test bandits** — `NPC_Bandit` HP is 1000 (asked for testing; was 50) and `StartingTown/Bandit` has 3
  `bandit (stance test N)` copies, each with its own `Data/NPCs/Bandit/StanceTest/NPC_Bandit_StanceTestN` (HP 1000) +
  `KilledFact_bandit_stanceTestN`. After tuning: restore HP 50, delete the copies and the `StanceTest/` folder.
- **Stance state tests** — `StanceMath` and the shared `StanceMeter` (timers / regen / refill) are unit-tested; the
  brain's hold / restore / deferred-event paths and `PlayerStance` side effects are manual play checks only.
- **Weapon stance 0** — `WeaponSO.stanceDamage = 0` means "use archetype", so a weapon can't be authored with zero stance
  damage on an archetype that has some (use a flag like `KnockDownOverride` if ever needed).
- **Pre-existing NRE at play start** — `PlayerAnimationDriver.SetInCombat` (line 74) via `EquipmentVisuals.OnEnable` →
  `PlayerCombat.HandleVisualsRefreshed`, seen while testing stance (path not touched by it).

## Player stance follow-ups (from spec-player-stance-stagger-knockdown, 2026-10-30)

- **Stance UI** — kept hidden on purpose (feel over numbers); revisit if playtests show players can't read it.
- **Guard break** — a regular block only takes `blockStanceMultiplier` of stance and never knocks down; a dedicated
  guard-break reaction (block broken at 0 stance) is a separate design.
- **NPC weapon visuals** — `NPCEntity._weapon` is data only (stance + knockdown flag). Visual, hitbox, damage and
  weapon-specific clips come with real NPC equipment.
- **Attack layer has no `Death`** — a player (or NPC) dying mid-knockdown keeps the upper-body `KnockedDown 0` →
  `GettingUp 0` twin running over the death pose. Add AnyState → a Death twin on the Attack layer (controller change).
- **Airborne stagger / knockdown** — a reaction while airborne zeroes horizontal movement at once (straight drop) and
  plays the clip mid-fall; consider ignoring stance while `IsAirborne` or delaying the reaction until landing.
- **Forced NPC line while down** — a witness's forced dialogue (`DialogueSystem` forced line) can open over a knockdown
  (cosmetic: the reaction still ends underneath). Witness brains could wait for `!IsIncapacitated`.
- **Inventory already open when staggered** — `UIScreenManager.OpenTab` refuses while incapacitated, but a tab opened
  before the hit still allows equip changes (controller swap cuts the reaction clip).
- **I-frames / downed protection** — health damage still lands while the player is knocked down (asked "yes for now").

## From spec-two-handed-weapons-flamberge (2026-10-30 review)

- **Combo state not reset on weapon swap** — `PlayerCombat.HandleVisualsRefreshed` keeps `_comboStep` / `_comboWindowOpen`; equipping a weapon with fewer combo steps mid-chain (e.g. unarmed step 3 → 2H max 2) lets `IsMaxCombo` (`==`) miss and fire `Attack_3` from CombatIdle. Pre-existing; reset the combo there and use `>=`.
- **`_currentWeaponSO` cache can be null while a weapon is equipped** (after disable/enable, or if the startup visuals refresh precedes `OnEnable`): melee then uses ×1 stamina, unarmed combo steps, and no weapon stance. Pre-existing; resolve from `EquipmentSystem.GetEquipped(Weapon)` like `EquippedBow`.
- **Stamina spent before the Animator accepts the attack trigger** — a press late in a combo window (or during blend-out after a low-stamina reset) can spend stamina with no swing. Pre-existing for all weapons; consider consuming on attack-state enter.
- **No editor tool to bake two-part draw/sheathe clips** — `2hand draw.anim` / `2hand sheath.anim` were baked once from `execute_code` (recipe in `Combat/CLAUDE.md`). Add an `Editor/` baker if more two-part clips arrive.
- **2H knockdown / getting-up / hit / death use the base clips** while holding the 1.3 m blade (clipping). Find 2H variants or accept.

## From spec-weapon-pose-tuner (2026-10-30 review)

- **Hitbox Tuner leaves Humanoid rigs posed** — `AnimationMode.StopAnimationMode` does not revert bones sampled from a Humanoid clip (verified on the Player). `HitboxTunerWindow` has no rest-pose snapshot, so tuning a humanoid NPC leaves it posed (and a save would bake it). Port the snapshot / restore + save hooks from `WeaponPoseTunerWindow`.
- **Weapon Pose Tuner discards unsaved gizmo edits** on mode / weapon / target change without a prompt.
