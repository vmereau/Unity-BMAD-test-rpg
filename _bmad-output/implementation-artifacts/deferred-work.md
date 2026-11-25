
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
- ~~**Spider quest tracks 2 spiders, text says "All five"**~~ — resolved by spec-remove-spider-quest (2026-11-16): the test quest was removed.
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
- [Resolved in `c91fb90`: `PlayerAnimationDriver.Bridge` resolves lazily] **Pre-existing NRE at play start** — `PlayerAnimationDriver.SetInCombat` (line 74) via `EquipmentVisuals.OnEnable` →
  `PlayerCombat.HandleVisualsRefreshed`, seen while testing stance (path not touched by it).

## Player stance follow-ups (from spec-player-stance-stagger-knockdown, 2026-10-30)

- **Stance UI** — kept hidden on purpose (feel over numbers); revisit if playtests show players can't read it.
- **Guard break** — a regular block only takes `blockStanceMultiplier` of stance and never knocks down; a dedicated
  guard-break reaction (block broken at 0 stance) is a separate design.
- **NPC weapon visuals** — `NPCEntity._weapon` is data only (stance + knockdown flag). Visual, hitbox, damage and
  weapon-specific clips come with real NPC equipment.
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

## From spec-two-hand-grip-constraint (2026-10-30 review)

- **`2hand sheath.anim` wrist flip at the merge seam** — the right hand rotates up to ~174°/frame around frames 21–25 (just before the WeaponSwap at 0.78 s), so the rigid weapon spins for ~2 frames before going to the back. Re-bake with a longer gap / proper rotation blend at the seam, or move WeaponSwap a few frames earlier.
- **NPC two-handers** would get no grip constraint (Player-only component) — only relevant if NPCs ever wield `twoHandedGrip` archetypes through `EquipmentVisuals`.

## From spec-two-handed-full-body-attacks (2026-10-30 review)

- **`IsMaxCombo` still reads the `_currentWeaponSO` cache** (startup race already listed above) — the attack step now
  reads `EquipmentSystem`, combo steps / stamina / stance don't yet.


## From spec-full-body-attacks-root-motion (2026-11-03 review)

- **Recovery-tail lock** — every attack now has no move input until its exit transition (0.81–0.90 normalized; the 1H
  overhead clip is 3.17 s), and steering ends at `HitboxEnable`. Instant cancels (block / dodge / jump) are the only way
  out. Playtest; if it feels sticky, add a recovery window (move / steer again after `HitboxDisable`, or a cancel-into-move
  clip event).
- **Watchdog roots the player** — a trigger the Animator never takes now keeps `CanMove()` false for the full
  `attackStallTimeout` (1.5 s) for every weapon, not only 2H (pre-existing watchdog, wider impact now).
- **Attack step is time-based** — `AttackStep` runs on seconds since the state entered, not clip time: an Animator speed
  change (future attack-speed stat, hit-stop) would desync the lunge from the swing. Switch to normalized time if speeds vary.
- [Resolved in `c91fb90`: `PlayerAnimationDriver.Bridge` resolves lazily] **PlayerAnimationDriver NRE at startup** — `EquipmentVisuals.OnEnable → PlayerCombat.HandleVisualsRefreshed →
  PlayerStateManager.SetInCombat → PlayerAnimationDriver.SetInCombat` runs before the driver's `Awake` cached the bridge
  (seen on entering Play mode; code unchanged since a8a6bc5). Guard `_humanoidBridge` or resolve it lazily.

## From spec-meshy-2h-motion-prompts (2026-11-03 playtest)

- **Meshy text-to-motion for 2H attacks — retry later** — rejected because Meshy animates without a weapon: the
  left → right hand line read by `TwoHandGripConstraint` swings behind the body (attack 1 cut through the player) and the
  hands cross in the chop. Verdict, hand-order rule, mirror check and logged clips: `Combat/MESHY_PROMPTS.md`; the v1
  clips are kept untracked in `two handed/meshy test/`. Retry when Meshy can take a prop or a reference clip.
- **Blender bridge: sword path + two-hand IK bake** — repair weaponless clips (Meshy or other) in Blender via the
  Unity MCP `blender_bridge` (`run_python`, `screenshot`, `import_model`): attach a sword object to the rig, keyframe its
  path (guard → wind-up → cut → recovery), constrain both hands to the handle with IK, bake, re-import as Humanoid.
  Gives the grip constraint a consistent hand line. Needs the BlenderMCP addon installed first; validate on one attack
  with the Player-rig blade-tip sampling (PlayableGraph, `MESHY_PROMPTS.md`) before planning a full set. Own spec.
  - **Update 2026-11-03 (spec-blender-2h-attack1-horizontal-slash):** the Blender pipeline works through the Claude Code
    Blender MCP (`mcp__blender__*`; the Unity `blender_bridge` is not set up). Validated on a Mixamo clip, not a Meshy one:
    left-hand IK only, driven by Unity-measured blade pitch → `2hand attack refined.fbx` (Attack_1). Recipe:
    `Combat/CLAUDE.md` → Blender edit recipe. The full sword-path rebuild for weaponless clips stays open.

## From spec-blender-2h-attack1-horizontal-slash (2026-11-03)

- **2H attack 1 recovery tail** — after the hit (0.58 → 1.0, ~0.77 s) the follow-through swings behind the right side and
  back; trimming or speeding it would make the attack feel lighter. Changes the clip length → re-tune events / `attackSteps`.
- **2H attack 1 strike entry** — the refined clip drops the blade from overhead to horizontal in ~40 ms (tip ~39 m/s vs 24
  m/s originally). Accepted in playtest; if it reads as a jerk, start the tilt during the overhead wind-up (before 0.38).
- ~~**2H attack 2 (finisher) blade check**~~ — **done 2026-11-03** (spec-blender-2h-attack2-diagonal-finisher): the
  swing was a flat descending sweep; `2hand attack 2 refined.fbx` raises the blade and tilts the cut to a 42° diagonal.
- **`WeaponHitbox` sub-steps ignore rotation** (review finding) — `WeaponHitbox.cs` ~107 sizes the sweep sub-steps from
  the hitbox centre's travel only; a fast rotation about the grip (refined 2H attack 1 entry, tip ~39 m/s) moves the tip far
  while the centre barely moves, so a small hurtbox could be skipped at low frame rates. Pre-existing; hits were fine in playtest.
- ~~**Commit the Blender edit script as a tool**~~ — **done 2026-11-03**: `Tools/Blender/twohand_blade_line.py` + `configs/2hand_attack_1.json`

## From spec-blender-2h-attack2-diagonal-finisher (2026-11-03)

- **2H finisher backswing as fast as the strike** — in `2hand attack 2` the backswing (0.08–0.26) peaks at 20 m/s tip speed,
  the strike at 21 m/s (refined: 23), so the finisher doesn't read as heavy. Timing, not the blade line: slow the backswing
  / hold the raise (body + both arms) — changes event times and maybe the clip length → re-tune `attackSteps`.
- **2H finisher impact height** — when the blade points forward (0.55) the hands are at 0.88 m, so the tip crosses the
  centre at ~0.7 m (thigh). Raising it needs the hands kept higher through the cut (right arm / spine edit, out of the
  left-arm-only recipe).
- **Commit the full Unity blade check** (review finding) — the README snippet only gives pitch / hand distance; the
  acceptance numbers (yaw, tip, speed vs original, blade clearances, body-bone drift) came from an extended `execute_code`
  run that isn't in the repo. Add it to `Tools/Blender/` (or an editor menu) so the check can be repeated.

## 2026-11-03 — from spec-hostile-npc-lockon-and-prompt review

- Humanoid NPCs' `EntityStance._animationDriver` is null: the base `Entity_base` reference points at the base animation driver that `NPC_base Variant` removes (fileID 3299999205233303053). `EntityStance.Awake` falls back to `GetComponent`; verify stagger / knockdown animations on humanoids and wire it to `HumanoidAIAnimationDriver` explicitly if needed.

## 2026-11-03 — from spec-music-system-exploration-track review

- **Music starts with Core, not with the first region** — the 3 s fade can partly happen during the boot loading overlay. Start (or restart) the fade on the first `OnLoadFinished` if it's noticeable.
- **Linear volume slider** — `AudioSource.volume` is amplitude, so most audible change sits near 0. Use a perceptual curve (or dB via an `AudioMixer`) when SFX / master volume arrive.
- **Options slider has navigation None** — like the rest of the Game Menu, there's no keyboard / gamepad navigation. Revisit with controller support.
- **Core.unity UICanvas override values re-serialized** — saving Core rewrote ~15 existing RectTransform override values on layout-driven Player UI children (0 → computed). No runtime effect (layout groups drive them), but those stale overrides could be cleaned up.

## 2026-11-03 — from spec-sfx-system-combat-footsteps review

- **Arrows reuse melee cues** — `PlayerCombat.ResolveHit` plays `hitFlesh` / `block` / `perfectBlock` (sword sounds) for arrow hits too, and arrows hitting terrain or walls make no sound. Add arrow impact cues (bow `WeaponSO` or projectile data).
- **Monsters share humanoid combat cues** — the spider's bite plays the sword-on-flesh cue, and blocking it plays a steel clang. Add a per-entity `CombatAudioSO` override (e.g. on `EntitySO`).
- **Cooldown is per cue, not per emitter** — two hits on the same cue within its cooldown (50 ms) drop the second one in group fights. Key the cooldown by cue + emitter if it's noticeable.
- **No voice priority** — once the pool is full, the earliest-started voice is stolen, whatever its importance (a perfect block could be cut by a footstep). Add a priority field to `SoundCueSO` if it's audible.
- **SFX tails keep playing over pause** — nothing pauses the SFX pool when the Game Menu or death screen sets `timeScale = 0`. Consider `AudioListener.pause` (music already ignores it).
- **Sneak footsteps** — sneaking (1.5 m/s) plays normal walk steps. Consider a quieter sneak cue or volume scale (pure audio; stealth doesn't model sound).
- **AI swing whooshes** — enemies have no swing sound. Needs a per-attack cue that avoids sword whooshes on creatures.
- **Block sounds by blocking surface** (design agreed with Valentin) — `Block` / `PerfectBlock` cues assume steel on steel, but blocks can be made with bare arms today and with shields later (wood / metal). Plan: an `ImpactSurfaceSO` (Metal, Wood, Body, …) holding its own `block` / `perfectBlock` cues. `WeaponArchetypeSO.blockSurface` (sword / axe / two-handed → Metal), a future `ShieldSO` → Wood or Metal, unarmed → Body via `CombatAudioSO`. Move the block sound to the defender (`PlayerCombat.TryReceiveHit` knows what blocked) instead of `EntityMeleeAttacker.HandleHit`. No attacker × defender matrix — the blocking surface sets the sound. Generate wood (thunk / crack) and body (forearm thud / deflect) sets. Do it as its own quick-dev spec. Until then the Block / PerfectBlock cues have no clips (silent).
- **Unarmed swing is a stopgap** — `Cue_UnarmedSwing` reuses the 4 `SwingLight` clips at pitch 1.3–1.5 and volume 0.5. The ElevenLabs "punch swing" prompt gave impact sounds. Retry with "very short quick air whoosh of an arm swinging, light cloth sleeve flutter, no impact, no hit, no voice" (0.3–0.4 s, prompt influence ~0.7–0.8).

## 2026-11-04 — Area 1 vertical slice (split from spec-encampment-camp-and-cast)

Each ships as its own quick-dev spec, on top of the camp layout and cast:

- **Arrival sequence** — split 2026-11-12. Part 1 (gate spawn, escort to the stage, Warden speech, beating, Tam gating,
  dev skip) is `spec-area-1-arrival-escort-beating`; part 2 (escort to the hut, Hobb's briefing) is
  `spec-area-1-arrival-hut-briefing` (done 2026-11-13). Still open: the "stripped" beat, redesigned 2026-11-16 as player origins (`docs/World/player-origins.md`).
- ~~**Warden's errand tour**~~ — shipped as `spec-area-1-wardens-errand` (2026-11-15): four named managers, quest-item
  notices, work-site choice. Tools issued as Company property are still open (with the work shifts).
- **First work site** — Stone Quarry recommended (hidden find, first contact with the ruins mystery).
- **Racket guard encounter** — exit-tax interception, pay / refuse / attack-on-sight branches.

## From spec-encampment-camp-and-cast (2026-11-04)

- **River and docks** — the terrain has no river, though Area 1 relies on it (docks, illegal fishing, the hunter's fire across
  the water, Harl's trout line). Needs terrain work and a water shader.
- **Camp boundary** — only the gate and short fence runs block anything; open slopes let the player walk round both the inner
  fence and the civilization gate. Close it with a real outer fence, or terrain, once the area's shape is settled.
- **Outcrop shack** — the `SM_AssembledPlanksA_a1*` / `SM_wallDoor2m_a1_1` planks at the cliff foot (−1, −44) were left in
  place: purpose unknown, sits at y −1. Decide whether it stays.
- **Find Herbalist can't finish** (pre-existing, Quest Explorer V3 / V7) — `WorldFact_herbalist_forest_camp_found` has no setter
  and the quest has no completed part. It starts and logs only.
- **Tam is present before the beating** — `docs/World/NPCs/npc-002` gates him behind event-001; the arrival spec adds that.
- **Company flags are stand-ins** — `SKL_Flag_03` scaled 1.5×, with no Company emblem.
- **Terrain tree prefabs missing** — "Tree prefab at index 0–2 is missing" logged on load (pre-existing).
- **Colliders on `PF_PrimitiveHouse01`** — negative-scale BoxCollider warnings each play session (pre-existing, pack prefab).
- **`TheftPursuitRegistryTests` fail on a second run in one Unity session** (pre-existing) — the static `_claimed` set only resets on play-mode enter and the tests reuse fixed ids, so re-running EditMode tests without a domain reload fails 3 tests. Add a test-only reset or per-run unique ids.
- **Enemies stay engaged at the shore** (spec-water-and-swimming) — `EntityBrain.HandleEngage` keeps a hostile Engaging when a swimmer is unreachable, so water is a free escape. Add an unreachable timer, then search or disengage.
- **Water ripple precision** (spec-water-and-swimming) — `Game/Water` feeds unwrapped `_Time.y` into the hash; noise may coarsen after about two hours. Wrap the offset or use a precision-safe hash.
- **Water shader lighting** (spec-water-and-swimming) — no main-light shadows on the sun glint (shows under cliffs and trees), and see-through water is fogged twice. Cosmetic.

## From spec-area-1-zone-planner (2026-11-04)

- **Area 1 blockout** — split from the zone planner: build every feature in the approved `docs/World/Areas/area-001/layout.json`
  and its `notes`: terrain, river, lake, roads, fences, forest, the arrival gate and removal of the Civilization gate, work sites,
  both ruins, hollow, cave and the stone fort; move the spiders and bandits; then rebake the NavMesh and check sightlines and walk times. Whole-terrain sculpting is pre-approved by Valentin (2026-11-04).
- **Magenta tree in `StartingTown`** (resolved, see the blockout-terrain entries below) — a large tree renders magenta in the Scene view. A scan of scene renderer materials found nothing
  broken, and all 3 terrain tree prototypes report no prefab. Cause not confirmed; check them before the blockout scatters forest.

## From spec-area-1-blockout-terrain (2026-11-04)

- **Area 1 blockout: sites and structures** — split from the blockout at Valentin's request; depends on `spec-area-1-blockout-terrain` and `spec-area-1-blockout-camp-edge`.
  Add a blockout root under `Area1_Blockout` per site: docks, hunter's fire with a working night light, quarry with the hidden sealed crack, Ruins 2, ruins on the ridge's south lip (towers 14–18 m, so they clear the 5 m palisade from the tents), wood camp, hunting grounds, tutorial cave (rock mound over a graybox chamber) and a stone fort: curtain wall on the outcrop rim plus a tower on a free corner, no gameplay objects moved.
  Approved 2026-11-04: Fight Club Hollow moves to about (32, 30), on flat ground with the crater rim as its back wall and a rock-mesh fold against the north edge of the Mafia tents. `Area1TerrainBuilder` gets cliffs on the quarry's south and east sides, a cliff ring around Ruins 2, and a narrow open-air cleft between them at about (255, −125), closed by a planks-and-rubble blocker quest-002 can remove. After the re-run, recheck the waterfall and rebake.
  Move the bandits off the arrival road and the spiders into or beside the cave. Verify: hollow hidden from the fort, hunter's fire visible at night from the docks and tents, ruins visible from the tents, Ruins 2 unreachable with the blocker in place and reachable without it, NavMesh walk times between all sites against the first spec's numbers, enemies on the NavMesh and off the roads, eye-level screenshots against the artworks.
  **Split 2026-11-05:** `spec-area-1-blockout-quarry-terrain` takes the quarry cliffs, Ruins 2 ring, cleft, blocker and the tree clearings at the cave and hunting camp. Everything else above stays here for the next spec, with these decisions from Valentin:
  - The ford stays: the bridge is the main crossing, and the wade lanes at x 31–32.4 and x 37.5–39 stay open (NavMesh and a 0.4 m capsule pass; not play-tested).
  - The Company house area will be reworked: the curtain wall merges into the Company cliff and the `House`, with old stone structures merged with recent wood buildings on the hill. No wall or plinth around the `House`, which overhangs the rim at about z −45 (floor y 10, ground y 3). Leave a gate gap at the ramp top (x 13–23, z −28..−34), and treat the `SM_Ruin_*` pieces there as wall.
  - The hollow keeps the bench, stone, fir and log at about (31, 30). Its floor is about 10 × 8 m once the fold stands. Update `hollow` in `layout.json` to (32, 30).
  - Planned defaults: tower on the north-west corner at about (1, −31); ruins towers around x 118–140, where the sight corridor crosses the lip; cave built on the ground with no terrain holes; bandit patrol moves to about (−110, 30); check the fire from (11, 35) and the docks, because deeper tents can't see it.
  - Findings: the `CampDock` deck isn't connected to the NavMesh (paths stop in the shallows at about (4.7, 66.4)). The barricade snag probably comes from box colliders wider than the meshes the bake sees; fix with a Not Walkable volume without moving them. `SM_Floor_A_01 (8)` and `SM_bigRockA_a1 (18)` sit at scene root, not under `CampBridge`. The bridge has no rails, and the docks no boat or mooring. Check the spiders' `_waypoints` before moving them.
- **`Tree prefab at index N is missing` on every terrain build** — `StartingTownTerrain_Source.asset` still lists the old tree prototypes, and loading it logs three errors. The live terrain's prototypes are intact. Clear the source asset's `treePrototypes`.
- **Hand-built bridge and barricades need a recheck** (resolved in spec-area-1-blockout-camp-sites) — `Area1_Blockout/CampBridge`, `CampDock`, `CompanyCliff` and the root `SM_Barricade_*` were added by hand on 2026-11-05. The bridge's beams and `SM_bigRockA_a1 (18)` block wading through the ford, so the bridge is now the crossing. A NavMesh corner at (28.5, 55.4) runs into a barricade's collider. Decide whether the bridge replaces the ford, and rebake after any change.
- **Set `WorldFact_QuarryPassageOpened` from quest-002** -- `SetWorldEvent` still has no callers, so the cleft stays sealed until quest-002 is designed. Tabled pending the ruins lore.
- **The player can jump up any steep slope** -- `PlayerController` has no slope slide and `CanJump` passes on steep ground, so holding jump climbs cliffs. A capsule with the real jump values reached y 153 on the east mountain wall, which walking can't climb. Ruins 2 has an invisible `RuinsRing` for this; the mountain ring and the `PassBlocker` flanks don't. Slide on slopes over the limit, or refuse jumps there.
- **`fence-break` isn't checked against the fence** — moved off `outer-fence-3` or within 4 m of a corner, the gap goes missing or moves while `BrokenStretch` stays. The current position is fine.
- **Remove `outcrop-edge` when the fort wall exists** (done in spec-area-1-blockout-camp-sites) — an invisible stopgap in `Area1_Blockout/CampEdge`; the sites spec's curtain wall replaces it.
- **Magenta tree: cause found and fixed** — `SM_MountainTree_*` and `SM_LeafTree_*` had HDRP materials (`InternalErrorShader`). Valentin reimported the packs as URP on 2026-11-04 and they now render. The forest uses beech instead; see the spec.
- **Recorder sample blocks all compilation after a pack reimport** — `Assets/Hivemind/Defaults/Samples/Recorder/3.0.3/Movie Recorder Sample/MovieRecorderExample.cs` needs `com.unity.recorder`, which the project lacks. It is wrapped in `#if false` locally; `Assets/Hivemind` is gitignored, so a fresh pack import brings the error back. Delete the sample, or install the package.
- [Resolved in `c91fb90`: `PlayerAnimationDriver.Bridge` resolves lazily] **`PlayerAnimationDriver.SetInCombat` null reference at startup** — `EquipmentVisuals.OnEnable` → `Refresh` → `PlayerCombat.HandleVisualsRefreshed` reaches `_humanoidBridge` before it is set, once per play session. Seen in the blockout's play test, outside the code it changed.
- **29 camp ferns and trees left the scene before the blockout** — root-level `SM_EagleFern_*`, `SM_EuropeanBeech_*`, `SM_SilverFir_*` instances were already gone from the unsaved editor state and were saved as Valentin chose. They are in `ffdd42e` if wanted back.
- **Forest is all beech** — `SM_SilverFir_*` prefabs exist and would suit the art's dark conifers better. Check they work as terrain trees and add them to `TreePrefabs`.
- **`Application.runInBackground` is off in the Editor** — play mode doesn't tick while Unity is unfocused, so MCP play tests sit at frame 2 unless a test sets it to true first.
- **Reimport side effects to sort out** — untracked `Assets/DemoLevelSettings.lighting`, `Assets/Medieval_VillageSettings.lighting` (and metas) appeared with the URP reimport; they are not part of the blockout commits.

## From spec-area-1-blockout-camp-sites (2026-11-05)

- **[Done 2026-11-05, `spec-area-1-blockout-remote-sites.md`] Area 1 blockout: remote sites** -- split from the camp-side spec at Valentin's request; depends on it. Extend `Area1SitesBuilder` with one root per site: ruins on the ridge's south lip (towers 14–18 m around x 118–140; the sight corridor crosses the lip at about x 127, and the palisade hides everything below about y 38), quarry and Ruins 2 dressing on the existing pads (keep the cleft blocker and `RuinsRing` working), wood camp on its pad, hunting camp, and the tutorial cave (rock mound over a graybox chamber at (85, −45), on the ground, no terrain holes).
  Decided 2026-11-05: **move `hunting-camp` to about (32, 222) in `layout.json`**, because (32, 214) is the steep end of the raised trail (25–42°); stay within about 14 m of it, inside the tree clearing, with no terrain change. **Wire all five bandit waypoints** into `EntityBrain._waypoints` (only 1 and 2 are wired today). Move the `Bandit` root and waypoints to about (−110, 30), snapping each to the ground, which is about 3 m higher there. The spiders have no `_waypoints`, so only the three spiders move into or beside the cave.
  Finish: rebake, walk times between all sites against the first spec's numbers, spiders and bandit on the NavMesh and off roads, eye-level screenshots against the artworks, refresh `terrain-map.png` and republish the planner artifact.

## Deferred from: review of spec-area-1-blockout-remote-sites (2026-11-05)

- **Graybox box tops bake as NavMesh islands.** Confirmed on `Ruins/Tower_West`, the arch `Lintel` and the scaffold decks: each samples as NavMesh, with a partial path from the camp. The player can jump up there, and `EntityBrain` chase calls `SetDestination` with no path-status check, so a chasing agent would likely stall at the nearest edge. Consider Not Walkable volumes when art replaces these roots.
- **`Waterfall_Placeholder` reads grey above the water line.** The `Water` material shows only on the lowest part of the box. It is a placeholder and unchanged by this spec.

## From spec-area-1-blockout-camp-interior (2026-11-05)

- **[Done 2026-11-05, `spec-area-1-company-compound-road.md`] Paint the camp paths.** The terrain tool now paints them. The Company Road's cobbles take the middle of the inner gate, so the art's deepest mud shows only at its sides.
- **[Done 2026-11-05, `spec-area-1-company-compound-road.md`] West bank route to the docks.** Closed by the compound fence and gate.
- **Restrict the dock gate.** `DockGate` stands open. Locking it, or a guard at night, is gameplay for a later spec, along with the guards at the inner gate and warehouse.

## From spec-area-1-dock-guard-post (2026-11-06)

- **Illegal fishing spot.** Moved from the docks to the far side of the lake, in the design doc only. The spot, its route and how workers slip out at night still need designing.
- **Guard post gameplay.** Guards, door leaves, the lockup and tally shed interactions, the alarm bell, and a possible back way into the compound.

## From spec-area-1-lakeside-rework (2026-11-06)

- **Guard the terrain capture against pit changes.** Capturing after `fight-pit` or `FightPit.Offset` changes stores the old pit as a hand edit, and the next build cancels the new one. `Assets/_Game/Scenes/CLAUDE.md` warns about it. A pit signature in `terrain-sculpt.bin`, checked by `CaptureSculpt`, would stop it.
- **Update the hosted zone planner.** The hosted planner artifact still holds the old layout with `hollow` and none of the lakeside features. Anything exported from it would fail the builders until it is republished.
- **Floating log at (31, 43.5).** `SM_Outskirts_CuttedWoodTrunk` stands about 1.6 m over the ground since the lakeside was flattened. Its collider catches a player walking the slope below it, and the night path runs close by.
- **Close the shore south of the pit, if wanted.** Hand-placed rocks replaced `lake-fence`, but the shore is still walkable past them at about (68, 14), round to the quarry opening. Valentin will decide later.
- **Detailed fight pit.** Valentin will design it later. Its sides should hide the floor from the tower top, which sees the north half of the floor now that the rock screen is gone.

## From spec-area-1-fort-hill-limits (2026-11-06)

- **Fort interior.** Valentin will design it. `House` and the big building stand open in the bailey, and the fort rectangle has no trees.
- **Moving the ramp keeps the old cut.** `Tools/World/Grade Area 1 Fort Ramp` only reshapes ground around the current line, so a moved line leaves the old cut in the sculpt until it's smoothed by hand. Storing the pre-grade heights would let the tool regrade cleanly.
- **Low stub panel.** The `outer-fence-stub` panel near (-1.9, -52.1) clears the cliff beside it by 1.23 m, under the 1.27 m jump. It only joins the ramp and the camp, which the open `RampGate` joins anyway. Raise it when `RampGate` is closed by gameplay.
- **Panels only check height along their length.** `BuildPalisade` sets a panel's top from the ground under it, so a cliff beside a panel isn't counted. That is what leaves the stub panel low.
- **Fort entrances have no gameplay.** Both gates stand open with no guards. Who may use the cart gate, and when, is for a later spec.

## From spec-area-1-hand-edit-capture (2026-11-07)

- **[Done 2026-11-07, `spec-area-1-timberworks.md`] The chamber's terrain holes come back.** The 6 holes near (-4, -46) were removed by hand from the live terrain, but `StartingTownTerrain_Source.asset` still has them and `Build Area 1 Terrain` copies them back without warning. Clear them in the source asset, or stop copying them.
- **[Done 2026-11-07, `spec-area-1-timberworks.md`] Stale `Barricades/NotWalkable_*`.** The volumes still match the old barricade positions, including the deleted `SM_Barricade_Var5 (2)`, until Sites runs. Capture hand edits first, then run Sites and rebake.

## From spec-area-1-timberworks (2026-11-07)

- [Done 2026-11-07, spec-area-1-garrison-quarter] **Garrison quarter (next spec, agreed with Valentin).** The ground between the fort road and the south-west palisade, now flat to about x −40 at z −30 and x −35 at z −40. Barracks for rank-and-file guards with a locked armoury, a shared Company house for lower employees (Debt Manager, a new steward under Voss), an alehouse restricted to guards and artisans at the start, a drill yard with pells, a grain store on stilts, and workers' graves by the bank. Keep space for kennels and a barber-surgeon. Voss stays in the fort, out of reach early on. Workers eat at the canteen. The docks and warehouse hold the early theft targets; the armoury, grain store and fort come later.
- **Traveling merchant's wagon stand.** Waits for the closed pass to have a story answer.
- [Done 2026-11-07, spec-area-1-garrison-quarter] **Palisade panels 012-015 stand below the bank.** The hand flatten cut the ground under the line at about (-40..-46, -34..-27), and their tops (y 6.4-8.6) are now at or under the ground just outside (up to y 7.8). A player on the hill can step over and drop into the camp. It's one-way: from inside it's still a 7 m wall. Raising the panels or moving `outer-fence` onto the bank top fixes it, and `BuildPalisade` only checks height along a panel, as noted above. The same bank's panels 021 and 011 keep only 1.2-1.4 m.
- **Spots left from the removed houses.** `Spot_Storekeeper_Night` stands on the commute at (-12.5, 3), and the Cook's spot dates from the old canteen house.
- **Charcoal smoke.** The clamp is a shape only. Smoke that reads from the tents is for the art pass.

## From spec-area-1-garrison-quarter (2026-11-07)

- **Kennels and barber-surgeon.** `reserve-kennels` (−40.5, −27.5) and `reserve-barber` (−32.7, −39.7) are empty plots in the layout. The barber's is only 3.2 × 4 m.
- **Grave mounds on the gullied bank.** The flatten left gullies on the low bank, so some mounds overhang. Smooth that ground when the graves get art.
- **Brew shed.** Offered and left out: a shed and barrel yard behind The Short Measure.

## From spec-area-1-grain-fields (2026-11-08)

- **Farm manager NPC and the field gang.** The reeve, the gang roster, the morning count at the tally post and the walk out through the fields gate need NPCs and routines.
- [Done 2026-11-08, spec-area-1-grain-fields] **Fields gate is hand-added.** Now `fields-gate` in the layout, built by Sites under `RampGate/FieldsGate`; `ramp-rail` is back to full length, and the terrain run painted the new `path-farm`.
- **Barn theft.** The tithe barn is closed with no door to open; the worker path's "secure food reserves" quest opens it.
- **Oxen routines.** The farm and Stoneworks oxen are boxes. The mill ox walking its ring, oxen hauling to the quarry and wood camp, and breeding are later work.
- **Harvest and crops.** Crops are static rows: no growth, harvest, seasons or pickable produce. Tall wheat could later give stealth cover.
- **Camp hand paint isn't in the source terrain.** Every terrain run repaints the camp ground from `StartingTownTerrain_Source.asset`; this spec restored 5763 hand-painted samples afterwards. Copy the live camp paint into the source asset, or have the tool keep the live alphamap inside the camp.
- **Wattle fences in the bake.** The 0.12 m wattle pieces may be too thin for a bake, so agents could path through them; the gaps are the intended way in.

## From spec-area-1-tent-camp (2026-11-08)

- **Warden and event-001 at the new square.** The Warden still stands at (−3, 12) by the inner gate. The arrival beating, the escort along the lane and the warden's announcements should move to the stage at (1.5, 25).
- **Gang house stash.** Where npc-003 keeps it (floor cache, back door onto the deck, or a separate shed) and how quest-001 gets in are undecided; the gang house is unchanged.
- **Black market operator and stock.** The tent and counter exist; who stands there, opening hours and the trade UI don't.
- **Lakeside use and the field gang's beds.** The lakeside stays open. Where the field gang sleeps is undecided.
- **Tent camp NPCs.** The zones have no residents beyond the tent mate and the beaten worker: card players, Wretches, gang members and guards on the lanes are later work.
- **Company emblem on the flags.** The flags are plain dark red until the emblem is chosen.
- **Warehouse camp ramp's tip is in the NavMesh.** The bake links the low foot of `Warehouse/Ramp_Camp` (x 15.3–18.8, z 34.8) to the ground at its sides, so a path east from the Player's Zone cuts over the corner where the player's controller can't step. The player walks round the foot. A Not Walkable volume on the ramp's sides, or a skirt, would fix the path.
- **Company Road past the player's tent.** `company-road` runs (17, 34.8) → (10, 37) → (8, 33), so its cobble paint passes under the player's tent and campfire. Reroute it round the Player's Zone when the road gets art.

## From spec-restricted-areas-trespass (2026-11-09)

- **Grace across overlapping areas.** Grace is per `RestrictedArea`; a point inside two overlapping areas with shared owners is re-detected through the second area right after a scold. Make grace per owner / per trespasser, or share it across areas containing the point.
- **Lock-on vs aggro owners.** `LockOnSystem` only targets hostile factions, so the player can't lock on to an instant-aggression owner attacking them.
- **Humanoid unarmed reach.** NPC `AttackRange` 1.5 m stops the attacker just beyond the unarmed hitbox's reach: in play testing the punches whiffed on a standing player at 1.48 m and landed at 0.9 m. Lower `AttackRange` / `EngageStoppingDistance` or extend the unarmed hitbox (affects every humanoid NPC).
- **Trespass escalation.** `TrespassersSpotted` (runtime) is the hook for repeat-offence escalation, bounty or hostility; nothing reads it yet.

## From spec-npc-routine-patrol-activity (2026-11-09)

- **Monster patrols.** `EntityBrain` no longer has waypoints and `NpcRoutine` is NPC-only, so monsters can only idle / wander. `TestScene.unity` still holds 4 orphaned `_waypoints` prefab overrides (size 3, incl. `CreatureWaypoints`) on instances that used to patrol; give them a patrol source (routine or a monster-side route) or clean the overrides.
- **Routine brain behaviour has no automated tests.** Only the pure helpers (`FindBlock`, `FindAllDayBlock`, `NearestIndex`) are tested; route identity, snap advance, deferred nearest pick and the no-clock path were verified in play mode only.

## From spec-npc-hearing-movement-noise (2026-11-09 review)

- Trespass / theft pursuit uses its own LOS timer (`_pursuitHasLos`) and ignores hearing: an owner who reached full awareness by hearing a trespasser behind a fence may give up the chase after `theftChaseLoseSightTime` without ever seeing them.
- Hearing (like vision) uses flat XZ distance: a player walking on a floor / deck directly above or below an entity is heard through it (radius only halved by the blocked LOS).
- Player noise comes from any position change: knockback, being pushed, or a future moving platform counts as Walk / Run noise. Classify from the controller's intended velocity if that becomes a problem.
- Searching walks to the destination set at `TransitionToSearching`; a target heard mid-search doesn't redirect it (it re-engages once the meter refills).
## Observed during Hidden Stash verification (2026-11-10)

- [Resolved in `c91fb90`: `PlayerAnimationDriver.Bridge` resolves lazily] Player startup raised `NullReferenceException` in `PlayerAnimationDriver.SetInCombat:85`, via `PlayerStateManager.SetInCombat`, `PlayerCombat.HandleVisualsRefreshed`, and `EquipmentVisuals.OnEnable`. These files are unchanged by the quest work. Baseline reproduction was not separately performed; investigate the startup subscription/initialization order before attributing a cause.

## Area 1 arena navigation / rock collider agreement (2026-11-12)

During mafia-zone traversal checks, the direct baked-corner route to the existing arena opening snagged at approximately (45.82, 1.89, 31.5) against an unchanged rim-rock collider. The surface bakes render meshes, so its route can be tighter than the rock collider permits. Walking around via (43,34.8), (52.5,36), then (52.5,34) succeeds. Investigate collider/nav geometry agreement in a separate arena pass; preserve the hand-owned rocks and existing entrance when doing so.


## From spec-unconscious-knockout (2026-11-12 review)

- Loot of an unconscious NPC is free and reads the same `InventorySystem` as shop stock: punch a merchant down, take the stock, repeat. Needs a theft / crime rule (raise `OnTheftCommitted` for loot taken from a living NPC) or a shop-stock exclusion — user chose "free for now".
- [Resolved in `c91fb90`: `OnPlayerKnockedOut` closes the screens and dialogue; the action bar and screen toggles ignore input while incapacitated] Screens are not closed on a player knockout: a loot / trade / container / inventory screen or dialogue left open stays usable while down, and an equip during the knockout reassigns the Animator controller (resets `IsUnconscious` → the get-up clip plays early while input stays locked). (The loot screen of an NPC that gets up now closes — `OnLootSourceLost`, 2026-11-12.) Fix together with a knockout / got-up event channel that the screens listen to (like `_onPlayerDied`).
- Knocked out while swimming: the held knockdown plays at the surface under swim physics; `SetUnconscious` doesn't touch `IsSwimming`.
- A re-enabled NPC (component toggle, respawn tool) mid-knockout without an `NpcRoutine` keeps its agent stopped (brain forced to Idle with `isStopped`).

## From spec-knockout-gold-looting (2026-11-12 review)

- **Robber bends over the player's root, not the body.** The knockdown clip bakes its root motion into the pose, so the lying body can sit up to ~1.5 m from the player root the robber walks to. Play-test; if the pickup misses the body, measure reach to the hips bone (or a `LootAnchor`) instead.
- **Looting reacts only to hits and radius-scan hostiles.** Unknown-source damage (trap, fall) leaves the robber kneeling; an `EndLooting` on any damage would be simpler if it shows up.

## From spec-racket-guard-exit-toll (2026-11-12 review)

- [Resolved 2026-11-13, `spec-npc-dialogue-close-request.md`] **Toll dialogue left open when the encounter aborts mid-talk.** If Harl is hit or spots a hostile while the forced toll topic is open, the gate ends the encounter but the panel stays up. A pay / defer pick still applies (answers go to the last gate that opened the dialogue). A refusal shows the "lumps" line with no brawl. Closing the dialogue from AI needs a new request channel (`OnDialogueCloseRequested`).
- [Resolved 2026-11-13, `spec-npc-dialogue-close-request.md`] **Misleading log on topics without a `dialogueFact`** (pre-existing). `DialogueSystem.NotifyTopicCompleted` logs "WorldStateManager unavailable" for every repeatable topic (`dialogueFact` null), including every forced toll topic. Split the null-fact case from the missing-manager case.
- **Paid choice row is evaluated at render.** If gold drops while the choice node is shown, the row stays enabled and does nothing (`SelectChoice` returns false), with no feedback. Re-render on `OnGoldChanged`, or show a toast.
- **Run-past not covered in Play mode.** In the smoke test Harl reached the player before the scripted teleport out of the zone, so the "walked past → brawl" branch is verified by code only.

## From spec-area-1-arrival-escort-beating (2026-11-13)

- **Escort guards wear no Company armor.** NPCs have no equipment visuals (only the player's `EquipmentVisuals` binds
  `CompanyGruntArmor_Visual`, fitted to the player rig). An NPC outfit component (instantiate a `SkinnedBodyPart`, bind with
  `SkinnedMeshRebinder`, hide the body) would dress the guards, after checking the armor binds to the `NPC_base Variant` rig.
- **No camera direction at the stage.** The player is held but the camera stays wherever it was, so the beating can happen
  behind them. A soft look-at (or a camera nudge toward the stage when the hold starts) would frame it.
- **Dragged body pops.** A player knocked out on the escort is teleported to the watch spot while lying down, in view of the
  camera. A fade (none exists yet) would hide it.
- **Water is a free escape from the escort.** A straying player who swims makes the chasing guard give up (same gap as
  "Enemies stay engaged at the shore"); the stray rules then warn and send a guard again, forever.
- **Humanoid unarmed reach** (see spec-restricted-areas-trespass) — only the Warden got tighter ranges (0.9 / 1.1) for the
  beating; every other humanoid still stops at 1.3 m and can whiff.

## Deferred from: review of spec-area-1-arrival-escort-beating (2026-11-13)

- **Save block isn't airtight at region entry.** `SaveSystem` can autosave in the first frames after a region load, before
  `ArrivalSequence.TryStart` (3-frame settle) calls `BlockSaving`. Harmless today (the fact is unset, so the arrival replays
  on load), but a sequence that changes state in its first frames would need the block earlier (e.g. in `Awake`).
- **Knocking the Warden or Tam out before the stage consumes the event.** Emergent and accepted for now: a downed Warden
  skips the speech and the beating; a downed Tam skips the beating and the closing line.

## Deferred from: review of spec-area-1-arrival-hut-briefing (2026-11-13)

- [Resolved 2026-11-13, `spec-npc-dialogue-close-request.md`] **Region unload during Hobb's briefing leaves the dialogue open.** `ArrivalSequence.OnDisable` → `Release` resumes routines
  and unblocks saving but can't close the Core-owned dialogue; the next load replays the arrival. Same gap as the Warden's
  speech in part 1, but the topic list can stay open indefinitely. Needs a dialogue-close channel the sequence can raise.
- [Resolved 2026-11-13, `spec-npc-dialogue-close-request.md`] **`DialogueSystem.NotifyTopicCompleted` warns "WorldStateManager unavailable" for any topic without a `dialogueFact`**
  (seen on Hobb's briefing; the WSM is there). Misleading log, pre-existing.

## Deferred from: review of spec-npc-dialogue-close-request (2026-11-13)

- **Trade window survives its NPC.** A shop node closes the dialogue before `NPCTradeUI.Open`, so a close request (encounter
  abort, `ArrivalSequence` region unload after Hobb's topic list) can't reach an open trade window. After a region unload the
  window keeps a destroyed `_npcInventory` and a refresh / buy can throw `MissingReferenceException`. Have the trade UI close
  itself when its inventory is Unity-null, or track the trade's NPC for close requests too.
- **Player-opened Talk on an unloading NPC.** Only `TollGate` and `ArrivalSequence` raise close requests; a normal
  conversation open when the region unloads stays up with a destroyed context. `NPCPresence.OnDisable` calling
  `RequestDialogueClose()` would cover every NPC.

## From spec-npc-base-hero-model (2026-11-13 split — goal B, next spec)

- **NPC equipment: armor + weapon, armed / fists combat, loot rule.** Answers already given by Valentin:
  - Guards = Company faction: CompanyGuard_ArrivalLeft / Right, DockGuard, WarehouseGuard, **plus Harl (RacketGuard) and
    the Warden, moved to Faction_Company** (check hostility side effects: Warden beating Tam, toll brawl). All get
    `Armor_CompanyGrunt` + `Weapon_ShortIronSword`; other NPCs nothing.
  - Reuse `EquipmentSystem` + `EquipmentVisuals` on `NPC_base Variant` with the player-only channels
    (`OnEquipmentChanged`, `OnVisualsRefreshed`) left unwired; refresh directly.
  - Fight style: weapon is the general case (every fight / brawl); fists only on explicit request (arrival sequence's
    Warden beating Tam now, arena later). Clip events open "" = all hitboxes → only one of Unarmed / Weapon registered.
  - NPCs play draw / sheathe clips; the visual moves on `WeaponSwap` (add it to `EntityAnimationEventReceiver`).
  - Knocked out / killed: the weapon moves into the NPC inventory (lootable); armor stays equipped (not lootable).
    Wakes with it still there → re-equips; looted → fists.
  - Armed damage = `AttackDamage + weapon.damageBonus`; stance / knockdown from the wielded weapon (fists never KD) —
    replaces the data-only `NPCEntity._weapon`.
  - Known gaps: NPC equipment not saved (save mid-knockout can duplicate the sword); Brann is unpacked (no model swap).

## From spec-npc-base-hero-model (2026-11-13 review)

- **NPC Animator culling `CullUpdateTransforms` (pre-existing, kept by the model swap)** — off-screen NPCs don't pose
  bones, so `UnarmedHitbox` / weapon sweeps and hip-pinned colliders use a frozen pose: off-camera punches (NPC hitting
  the player from behind, the event-001 beating out of view) can whiff. Player and `Entity_base` use AlwaysAnimate.
  Confirmed in play: a knocked-out NPC off screen still read lying-down hips after getting up. Consider AlwaysAnimate
  on `Humanoid_BaseHero` (perf cost: every NPC animates) or only while in combat.
- ~~**Brann is unpacked**~~ — **resolved 2026-11-13:** re-instanced from `NPC_base Variant` (base-hero model, NpcArmament,
  `Hitbox`). No scene NPC uses the `Idle.fbx` dummy any more.

## From spec-npc-equipment (2026-11-13)

- ~~**NPC equipment is not saved.** The loadout is re-granted on every region load; the NPC inventory is saved. A save made
  while a guard lies knocked out (sword moved into his inventory) restores the sword in the inventory **and** a fresh one
  equipped → duplicate. A looted sword comes back on reload. A save-restored corpse may get the same duplicate if the
  corpse restore lands after `NpcArmament.Start`. Fix: save `EquipmentSystem.Equipped` per NPC (`SaveableObject`).~~
  Resolved 2026-11-16 by `spec-npc-equipment-save.md`.
- ~~**Brann** unpacked~~ — resolved: re-instanced from `NPC_base Variant` (no loadout; he's not a guard).
- **Off-screen Animator culling** (`CullUpdateTransforms`, see the spec-npc-base-hero-model review entry) also affects the
  sword sweep of an off-camera guard.
- **Fists last the whole fight:** if the player hits the Warden during the Tam beating, the retaliation keeps fists (the
  request ends only when combat ends).
- **Draw / sheathe play-mode loop not verified by the agent** (editor unfocused → no frames); verified: loadouts, hitbox /
  controller per mode, draw started on combat entry.
- **Revive after death (review):** `EntityHealth` can revive (`IsDead = false`), but nothing calls `NpcArmament.OnWokeUp`
  then — a respawned guard keeps its sword in the inventory and fights with fists. Hook the revive path if respawn ships.
- **Head variants — posed neck checks are not an automated test (spec-npc-head-variants review):** AC2 (no seam through
  idle / walk / attack / dodge, bare and Company armor) is verified by the MCP script `Tools/Blender/verify_head_variants.cs`
  only. Promote it to an EditMode test if head variants multiply.

## From spec-npc-skin-tones (2026-11-14, split at clarify)

- [Resolved 2026-11-14, `spec-npc-faces-second-batch.md`] **Second face batch on the toned skins** (agreed with Valentin): a head-variant spec on top of the skin-tone system.
  Tam (Fair, red hair + stubble, freckles, black eye, eye bags), Brann (Olive, dark brown hair, fuller cheeks, neat short
  beard), Storekeeper (Deep, short black hair greying at the temples, short beard, narrow face), Harl (Weathered,
  dirty-blonde cropped hair, stubble, brow scar), DockGuard (Fair, ash-blonde, clean-shaven, younger angular face).
  Blonde risk: the hair recolour caps luminance at skin level (halo guard), so try relaxing it with edge blending in the
  mockups, with darker blonde as the fallback. Each new head needs its own skin mask written by `make_head_variant.py`
  from its paint layers, so the hair doesn't take the tone.
- **`CompanyGruntArmorBuilder` recreates `M_CompanyGrunt` as URP Lit** (spec-npc-skin-tones review): only when the
  material is missing. It would lose `SG_SkinLit` and the hand mask, so toned guards' hands stay base-hero colour (the
  EditMode test catches it). Create it with `SG_SkinLit`, `T_SkinMask_CompanyGrunt` and its ref luminance instead.
- **`make_skin_masks.py` reads only the first UV mesh of an outfit FBX** (review): an outfit split into several meshes
  (separate gloves) would get a partial mask. Bake every mesh of the import into the same mask.

## From spec-npc-portrait-faces (2026-11-14)

- **Portrait faces for the other 9 NPCs** (Cook, Blacksmith, Brann, Harl, DockGuard, GuardLeft, GuardRight,
  WarehouseGuard, Bandit): same recipe as the pilot (Humanoids/CLAUDE.md → NPC portrait faces). Each needs 2 user-made
  portraits. Branded guards keep their `decal` layer (it is painted on the neck, outside the projection).
- **Portrait warp leaves ears / temples unanchored (review):** pins cover only the back of the skull and crown, so the RBF
  interpolates the ears and temples (oval landmarks 234 / 454 are ray-cast at a grazing angle). Fine on the pilot (max
  reshape 11 mm); add ear-ring pins if a portrait drives a large width change.
- **StartingTown play-mode pass for the portrait pilot** (AC1 of spec-npc-portrait-faces): heads are baked in place and the
  runtime is unchanged, but no in-game capture was taken.
- **Ears and the back of the head stay painted:** the side projection stops in front of the ear. A portrait with
  distinctive ears or a long hairstyle won't carry them.

## From spec-area-1-wardens-errand (2026-11-15)

- **Stripped beat** — redesigned 2026-11-16 as player origins (stat / skill bonus kept, promised belongings taken, denial
  topic; Wretch has none): `docs/World/player-origins.md`. Next spec once its open questions are answered.
- **Manager quests** — Dace (sealed south-face crack, ties to quest-002 / npc-009), Holl (trees falling against the cut),
  Ash (snares taken whole), Pell (tithe barn short every week). Each manager only plants the hook today.
- ~~**Work shifts** — the `DialogueFact_Warden_Errand_Work*` choice is recorded but nothing reads it yet.~~ — resolved by
  spec-wardens-errand-no-work-choice (2026-11-16): no work-site choice by design, every site is open.
- **Portrait faces for the managers** — `HeadAppearance_QuarryOverseer` / `_TimberBoss` / `_LeadHunter` / `_FarmReeve`
  reuse the Bandit / GuardRight / DockGuard / WarehouseGuard meshes (lookalikes) until `/characters:portrait-face`.
- **Placeholder notice icon** — the four notices reuse the trout-recipe icon; needs an `items:icon-prompt` icon.
- **Managers have no night routine** — one all-day Stand block; they stand at their site through the night.
- **No inventory tab for quest items** — notices sit in Misc; add a Quest filter tab if quest items multiply.

### Deferred from: review of spec-area-1-wardens-errand (2026-11-15)

- **Quest items are guarded in the UI, not in `InventorySystem`** — `InventoryUI.UseItem` (double-click / Use) would consume
  a usable quest item, `Equip` would take an equippable one out of the inventory (then a `requiredItem` choice reads it as
  missing), and lockpick / ammo `TryConsume` callers ignore the flag. Cooking / smithing now skip quest items and `ItemSO`
  warns on these flag combinations; move the rule into `InventorySystem` if a usable or equippable quest item is ever made.
- **`TeachChoiceOption` inherits `requiredItem`** but teach choices ignore it (drawer hides it); a value set by script is
  silently unused. Hide it or honour it.
- **`requiredItem` = `Item_Gold` can never be held** (the player inventory turns coins into `GoldSystem`); use `goldCost`.
  A validator could flag it.
- **Gold paid before the item is taken** — safe today (`TryConsume` can't fail after `CountOf` in the same frame), but a
  refund on failure would make `SelectChoice` atomic.
- **Quest Explorer V3 on `WorldFact_Arrival_Done`** — the index doesn't list `ArrivalSequenceSO.completedFact` as a setter,
  so `quest_report audit` reports "never set" for WardensErrand's start. Editor-tool fix pending Valentin's go-ahead.
- **Herbalist placeholder text in the quest-log prefabs** — `QuestLogUI.prefab` / `QuestPartPrefab.prefab` still hold the old
  design-time TMP strings (overwritten at runtime).

## From spec-dialogue-give-item-tam-food (2026-11-15)

- **Old dev saves** that completed HiddenStash through `Reported` now show it in progress (completion moved to FedFood / FedMeal).
- **Give window has no keyboard selection** (mouse only); Escape cancels. Add arrow-key / number navigation if controller support comes.
- **Meal ending payoff**: `DialogueFact_HiddenStash_FedMeal` is set but nothing reads it yet (planned opportunity with Tam).

## From spec-bulk-loot-and-trade (2026-11-15)

- **Free items can't be traded** (pre-existing): `GoldSystem.TrySpend(0)` returns false, so a `buyValue` / `sellValue` of 0
  makes Buy / Sell silently do nothing while the button stays enabled. Bulk now shows ×1 for them.
- **Trade pays before the item lands** (pre-existing): `NPCTradeUI.TradeUnits` ignores `AddItem`'s result, so buying a
  `GoldItemSO` whose currency collection is rejected loses item and gold. Check / refund per unit.
- **Context-menu batch is fixed when the menu opens** (by design): toggling Shift while the trade / container menu is
  open doesn't repaint or change its quantity; the detail buttons recompute on click.
- **`EntityHealth` dereferences `_persistentID` before its null check** (pre-existing, found in
  spec-knockout-xp-reward review): `Awake` / `OnEnable` / `TakeDamage` read `_persistentID.Entity` first, so an entity
  without a `PersistentID` throws in `Awake` instead of hitting the intended warning.

## From spec-execute-unconscious-npc (2026-11-16)

- **`EntityHealth.RestoreAsCorpse` comment is wrong** (pre-existing): it says the brain switches to Dead on its next tick, but
  `EnableRagdoll` disables `EntityBrain` first, so `_state` never reaches Dead. Harmless today (knockout / load already did the
  cleanup); the execute death path has the same shape. Fix the comment or drive `TransitionToDead` explicitly.
- **Execute starts only on a fresh F press** — holding F while walking up to a body doesn't start the stomp. Poll
  `Execute.IsPressed()` in `FindTarget` if playtests want hold-to-start.


## From spec-unarmed-idle-posture-fix (2026-11-16)

- ~~**A drawn bow plays the unarmed fist guard**~~ — done 2026-11-25 (spec-bow-idle-compensated-bake): `Bow Idle.anim`
  with an idle-only carry seat.

## From spec-unarmed-high-guard-block (2026-11-16)

- **Strafing turns the upper-body guard sideways** (pre-existing, every UpperBody pose): `UpperBodyMask` takes the hips
  from the strafe clips, whose hips yaw ~72–92°, so the shoulders (and the guard / block) face sideways while strafing.
  Measured identical with the old `Block Idle` and `Unarmed Idle Guard`. Needs a spine counter-twist or a mask change.
- ~~**The unarmed combat idle looks 36° down**~~ — done 2026-11-16: Neck / Head Nod +0.36 / +0.18 baked into
  `Unarmed Idle Guard.anim` (now ~14° down).

## From spec-start-main-menu (2026-11-16)

- **Quit to main menu** from the Esc Game Menu — needs a `Core` teardown first: `SaveSystem` and `WorldStateManager` are
  `DontDestroyOnLoad`, so loading `MainMenu` (Single) would leave them alive with references into the destroyed `Core`.
- **Failed in-game load leaves no region** (pre-existing) — `ReloadRegion` unloads the current region before loading the
  save's, so when an F5/F9 / Load Game region load fails, `FailLoad` "keeps the player's state" in an empty world with the
  save's facts already applied. The start-menu load now falls back to a clean New Game; the in-game path doesn't.
- **Continue session has no new-game snapshot** — Restart on the death screen needs one; it's only offered with no valid
  save, so it can only bite if every save is deleted mid-session (then Restart logs an error and does nothing).

## From spec-player-origins (2026-11-16)

- **New-game snapshot lost if a load pre-empts the startup region** (`SaveSystem.HandleRegionLoaded`): an F9 QuickLoad
  in Core's first frames sets `IsLoading` before the startup `RegionLoaded`, so `_newGameSnapshot` is never built (Restart
  then logs an error). Pre-existing; the picked origin id is now consumed either way.

## From spec-origin-artwork (2026-11-16)

- Origin artwork stays resident for the whole session: `SaveSystem` in Core holds `OriginCatalogSO` → every
  `OriginSO._artwork` sprite (3 × 896×1200 HQ, about 3 MB). Fine at three origins; with a bigger roster, move the
  artwork to a MainMenu-only lookup or an Addressables / soft reference.

## From spec-play-scenarios (2026-11-16)

- **PlayMode tests vs play scenarios:** `EditorSceneManager.playModeStartScene` (set by the `Game/Play Scenario` toolbar) may hijack a PlayMode test run into MainMenu / Core. No PlayMode tests exist yet; when the first is added, clear the start scene around test runs (Test Framework `TestRunnerApi` callbacks) or exempt them in `PlayScenarios.ApplyOnPlay`.
- **Dirty scenes with a forced start scene:** with Arrival / After Arrival, Play loads Core and StartingTown from disk; unconfirmed whether Unity prompts to save modified open scenes first. If edits are lost, add `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()` on `ExitingEditMode` in `PlayScenarioToolbar`.

## From spec-tam-tired-friendly-tone (2026-11-16)

- **Tam friendship after feeding** (user-deferred, own spec): feeding Tam (`DialogueFact_HiddenStash_FedFood` /
  `_FedMeal`) should make him a friend and change how he speaks to the player afterwards (new friend topic and/or
  friend variants of existing topics, gated on the Fed facts). To design: what changes, and whether a cooked meal
  makes him warmer than bread or an apple.

## From spec-level-up-max-health (2026-11-16)

- **Pre-change saves miss per-level HP:** a save made before this feature at level N loads with `bonusMaxHealth`
  holding only first-meal bonuses, so that character permanently lacks (N-1) × `healthPerLevel` max HP. No
  migration and `SAVE_FORMAT_VERSION` was not bumped. A top-up on load would need the meal and level bonuses tracked
  separately. Only matters if saves exist outside development.

## From spec-player-relationship-tiers (2026-11-16)

- **Friendlies assist against lawful pursuers:** `TryAssistPlayer` joins against *anyone* fighting the player (as specified),
  including a guard brawling the player over a theft / trespass or a neutral the player attacked first. Decide whether the
  player's own crime (who started it, why) should keep friendlies out of the fight.
- **Assist without perception:** the assist scan is pure distance (`friendlyAssistRange` from the friendly), no LOS /
  hearing — a friendly behind a wall joins a fight it can't see. Gate on `EntityPerception` sight or hearing if it reads wrong.
- **Tam friendship after feeding** (entry above): Tam is now relationship-Friendly once fed (`PlayerRelationship.Of`);
  the dialogue / tone side is still open and can read the tier.

## From spec-faction-mutual-defense (2026-11-16)

- **Friendly assist counts a Warning as a fight:** `IsFighting` uses `IsInCombat`, which `TransitionToWarning` also sets, so
  `TryAssistPlayer` can join against an NPC that is only warning the player. The defend scan now requires Engaging /
  Attacking; apply the same filter to the assist if it reads wrong.
- **Join scans have no perception gate:** `TryDefendMember`, like the assist, is pure distance to the opponent (no LOS /
  hearing) — an NPC behind a wall or on another floor joins. Gate both on `EntityPerception` if it reads wrong.
- **No cap on joiners:** defenders chain (each joiner is a fighting member for the next scan) and two flagged factions
  (Company vs Bandits) pull in every member near the fight. Add a max-defenders-per-opponent if fights balloon.
- **One-way hostility in join scans:** `FindPlayerOpponent` / `FindMemberOpponent` use the defender's `IsHostileTo`, so an
  asymmetric hostile list (B lists A, A doesn't list B) makes A ignore B attacking its colleague. Current faction data is
  symmetric.
- **`TransitionToEngaging` has no `isOnNavMesh` guard** (assist, defend and detection paths all reach it); an off-mesh
  NPC logs agent errors when it joins.

## From spec-stat-scaling-balance (2026-11-16 review)

- ~~**NPC-on-NPC kills pay the player XP**~~ **Resolved by spec-kill-xp-credit-assist (2026-11-16).** (pre-existing, amplified) — `PlayerRewards` pays `Entity.XpOnKill` on every `OnEntityKilled`, whoever landed the kill. With level-derived XP (level × 10) and faction mutual defense, guards killing a bandit or wolves hand the player 60–100 XP for free. Record the killer in `EntityHealth.Die` and pay kill XP only for player kills.
- **Pre-rebalance saves keep base stats 5** — `PlayerStats.RestoreBaseStats` restores the saved STR/DEX/END/INT, so an old save sits 5 below the new base (10). Bump the save version or clamp restored base stats to the config base if old saves must keep working.

## From spec-kill-xp-credit-assist review (2026-11-16)

- `EntityHealth.TakeDamage` / `OnEnable` dereference `_persistentID.Entity` without a null guard (the `Awake` check reads `_persistentID.Entity` before testing `_persistentID == null`) — an entity without a `PersistentID` throws instead of logging. Pre-existing.
- Kill-credit tuning ideas, not requested: Friendly credit has no distance / presence limit (dormant today — no faction alliances, Tam is passive); assist never expires within a region visit; stance-only hits (stagger / knockdown with no HP loss) give no assist.

## From spec-remove-spider-quest (2026-11-16 review)

- **Harl has no talk topic of his own.** With the spider topics gone, his only memories are `Mem_RacketGuard_TollCanDefer`
  (a choice gate) and `Mem_RacketGuard_TollBeaten` (after losing a fight), so Talk opens an empty topic list until he is
  beaten. Give him a greeting / small-talk topic in his voice (`/NPC:dialogue`).
- **Old saves keep the old Storekeeper stock** (same as the smithing entry above): `SaveableObject` restores the saved
  inventory, so a save made before this change never offers `Item_Recipe_AppleTroutSandwich`, and the spider quest that
  gave it is gone. Dev saves only; no `SAVE_FORMAT_VERSION` bump.

## From spec-npc-equipment-save (2026-11-16 review)
- **Saved NPC loadout is authoritative over content changes:** a saved slot whose itemId no longer resolves comes back
  empty (warning only), and an item added to `NPCEntity.StartingEquipment` after the save never appears. Fall back to the
  authored item per unresolved slot if saves must survive content updates (dev saves only today).
- **`PersistentID` vs `NpcArmament` `Start` order is undefined** (pre-existing): when `NpcArmament.Start` runs before a killed
  NPC is hidden, the save restore's `SetActive(true)` → `EntityHealth.OnEnable` (`IsDead = false`) → `NpcArmament.OnEnable`
  rebuilds an "up" NPC (controller swap) just before `RestoreAsCorpse`. Probably invisible under the ragdoll; a
  `DefaultExecutionOrder` on `PersistentID` would make it deterministic.
- **Re-equip matches the item type, not the dropped instance** (pre-existing in `OnWokeUp`, now also on restore): an NPC
  whose shop stock holds the same `WeaponSO` it wields picks a stock copy up after its own sword was looted.

## From spec-theft-return-or-fight (2026-11-17)

- **Test gaps:** no EditMode coverage for the player-side `StolenGoodsRequest` handlers (`InventorySystem` per-item clamp
  / take loop, `GoldSystem` clamp + `OnGoldChanged`), `StolenGoodsTracker` recording (container session, gold as amount,
  harvest count) or the `DialogueSystem` opening-line branch. Needs reflection or a small refactor into pure helpers.
- **Stolen goods are matched by item type, not by unit:** a thief who ate the stolen bread but owns other bread (or holds
  gold from elsewhere) gives up their own units. Also, putting an item into a stolen container and taking it back in the
  same session counts it as stolen.

## From spec-trespass-repeat-offence-fight review (2026-11-17)

- Trespass warning reset is polled at combat exit (`EntityBrain.EndTrespassFight`): if the owner leaves the fight (knocked out by the player, disengage range) and a colleague who joined via mutual defence later knocks the player out, the area's warnings are not reset. Fix idea: reset from the player-knockout event while an escalation fight is open for the area.
- `RestrictedArea` gizmo turns red on the raw warning count, not the expiry-filtered one (debug only).

## From spec-attack-recovery-cancel (2026-11-17, split at clarify)

- **Unarmed attack foot step (Blender):** the three unarmed clips (`lead jab`, `Hook`, `Uppercut`) are in place while
  `PlayerAttackStep` moves the capsule 0.30 / 0.35 / 0.50 m, so the feet slide. Edit the clips in Blender (existing
  `Tools/Blender/` IK + bake pipeline): during each `AttackStep` window the planted foot slides back in clip space by
  exactly the scripted displacement (stays world-fixed), the lead foot does a push-step and the rear foot drags after.
  Keep timings / events. Look-test the jab first. User chose Blender over new clips (2026-11-17). **Done in spec-unarmed-attack-foot-step.**
- **Recovery cancel follow-ups (spec-attack-recovery-cancel review):** (1) held move input cancels the recovery on the very
  frame the combo window closes — intended (user chose window close), revisit if playtests want a punishable tail (require a
  fresh push past the threshold). (2) No EditMode coverage for `PlayerCombat`'s recovery wiring (move threshold, `AttackCancel`
  clear on enter, blend-out press path) — only the tracker is tested; extract a pure decision helper if it regresses.

## From spec-unarmed-attack-foot-step (2026-11-17)

- **NPCs: same scripted step as the player (future spec, user decision).** NPCs share `Humanoid_Template` and now play the
  stepped unarmed clips in place: the feet step on the spot (rear foot slides back ~30 cm and returns). Give NPC melee the
  player's `AttackStep` displacement (and attack flow) so the clips read as intended. Stopgap if needed: point
  `Humanoid_Base.overrideController`'s three unarmed attacks at `Unarmed/old/`.
- **Entry / combo blend slides:** the 0.26 s CombatIdle → Attack_1 blend goes from the square locomotion `Idle` stance to the
  bladed jab stance (~22 cm rear-foot slide inside the step window), and the 0.42 s combo blends contain most of the Hook /
  Uppercut steps (mixed pose slides 5–20 cm, Hook tail up to ~60 cm while the Uppercut steps). Options: a bladed combat idle
  stance, shorter unarmed blends, or start the steps after the blend (step timing change). Anchoring the clips on the idle
  stance in Blender was tried and fails (Player leg reach / foot yaw).
- **Jab lead foot creep:** ~5 cm at 1.13–1.30 s (lead knee at full extension while the source body pulls back).
- **Upper body shift:** editing the legs moves Unity's centre-of-mass root, so the whole upper body (pose unchanged) shifts
  1.8–3.0 cm in the hit windows; re-check fist hitbox reach if playtests notice it.
- **Review follow-ups (tooling, low):** (1) `configs/unarmed/*.json` duplicate `CombatConfigSO.unarmedAttacks` with no
  cross-check: retuning a step invalidates the clip edit silently (re-run the tool after any step change; a test or an
  editor check could compare them). (2) `Unarmed/old/` clips share clip names / internal ids with the live ones: a future
  name-based lookup (`FindAssets("Hook t:AnimationClip")`, MCP find by name) becomes ambiguous. (3) The Uppercut's
  stored corrections are large (up to ~0.36 m at f6–10, reach-clamped): a re-sample or IK change can make the loop
  diverge from there — reset that range first. (4) The leg-reach clamp also applies to un-edited frames inside a bake
  range (no effect on the shipped clips: every baked frame carries a correction).

## From spec-ai-combat-tactics-foundation (2026-11-17) — combat AI roadmap (split, user decisions)

Built on the `CombatStyleSO` + `EntityCombatTactics` utility layer of that spec; each is its own quick-dev spec, in order:

- ~~**NPC blocking.**~~ Done — spec-ai-npc-blocking (2026-11-17). Original note: `EntityHealth.TryReceiveHit` always returns `NotBlocked`. React to the attacker's wind-up (player
  `PlayerStateManager.IsAttacking` / `IsAttackCommitted`, NPC `EntityMeleeAttacker.IsInSwing`) after a profile reaction
  delay, with a block chance; front-facing check; set `IsBlocking` (`HumanoidAnimationBridge.SetBlocking`, `Block_State`
  already in `Humanoid_Template`). Stance is the guard budget: blocked hits drain stance, guard break → stagger. Profile
  perfect-block chance. Company guards: **competent but beatable** (~30–40 % blocks, rare perfect blocks, clear openings).
- ~~**Potion use + drink animation (player and humanoid NPCs).**~~ Done — spec-potion-drink-animation (2026-11-17). Original note: Use `Art/Characters/Humanoids/Animations/drink potion.fbx`
  (imported by the user, untracked as of 2026-11-17) for the player's potion use **and** NPCs. `PotionItemSO.OnUse` only
  accepts `PlayerHealth` → add an NPC path (`EntityHealth.Heal`). Tactic: back off, then drink below a profile health
  threshold. Give each of the six `Faction_Company` guards (CompanyGuard_ArrivalLeft / Right, DockGuard, WarehouseGuard,
  Harl, Warden) one `Item_Health_Potion`; an undrunk potion stays lootable.
- ~~**Ranged style.**~~ Done — spec-ai-ranged-combat-style (2026-11-17): the bandit (Hunting Bow + Axe, 12 arrows).
- **Ideas noted:** punish the player's whiffed recovery; delayed swings / feints against rhythm parries; skill-scaled weights.

### Review follow-ups (spec-ai-combat-tactics-foundation, 2026-11-17)

- **Passive-target proxy:** tactics skip targets with `DetectionRange <= 0` ("never fights back"). A future brawl target
  that has `DetectionRange 0` but does fight back would get the plain chase. Replace with an explicit flag if that happens.
- **Too-close attackers:** `Decide` checks the attack roll before BackOff, so an entity inside `AttackRange × backOffFraction`
  that rolls an attack closes in further instead of stepping out (spec matrix "Too close" row allows it). Revisit if
  playtests show guards hugging the player.
- **Token cap without CombatConfig:** an entity whose `EntityHealth` has no `CombatConfigSO` is uncapped (one warning).

### From spec-ai-npc-blocking (2026-11-17)

- **Guard vs arrows:** `EntityGuard.ResolveHit` doesn't know the projectile — a raised guard blocks an arrow shot from within
  `guardThreatRange` in its front arc (distant shots always land). Add a projectile flag to `TryReceiveHit` if NPC archers or a
  shield system make it matter.
- **Guard token hold (review):** an NPC in Attacking with a token waits while its guard is up (`HandleAttack` doesn't tick
  tactics, so `attackCommitTimeout` doesn't run). Fine at block 0.4 / 0.25 (failed reads lower the guard); revisit if a
  high-`blockChance` style starves other attackers.

### From spec-ai-ranged-combat-style (2026-11-17)

- **Guard vs arrows (still open):** `EntityGuard.ResolveHit` can't tell an arrow from a swing; a raised guard blocks a point-blank
  arrow in its front arc. With NPC archers shooting other NPCs it can matter (guards fighting the bandit) — add a projectile flag
  to `TryReceiveHit` if it shows.
- **Weapon after a fight:** an archer keeps the weapon it held when combat ended — the next fight can start by drawing the bow,
  then switching to the axe (two clips) if the target is close. Consider reverting to the melee weapon on combat exit.
- **Player block vs arrows (review):** `PlayerCombat.TryReceiveHit` has no front-arc / range check, so a raised block absorbs
  NPC arrows from any direction (also from behind), and a perfect block can open on an arrow. Pre-existing for melee; NPC
  archers make it reachable. Add an arc check (or a projectile path) to the player's block.
- **Archers on bumpy terrain:** a small rise between archer and target cuts perception LOS; the brain falls back to the
  last-seen chase and walks in (seen in StartingTown around the bandit's patrol). An archer could hold its ring / reposition
  sideways instead of closing in while sight is briefly lost.
- **No player-side arrow tell other than the animation:** no sound on the NPC draw / release; add bow SFX cues for readability.
- **Archers vs monsters / NPCs:** ranged fire works against any faction-hostile target, but only the player was play-tested.

### From spec-potion-drink-animation (2026-11-17)

- **Potion prop in hand:** the drink clip uses the left hand with nothing in it (a drawn sword stays in the right hand). Add a
  potion visual on a left-hand socket, shown for the drink only.
- **NPCs drinking outside combat:** a hurt NPC never drinks after a fight (the brain ends any drink outside Engaging /
  Attacking). A post-fight "patch up" behaviour would need its own trigger.
- **Brawl sight flicker:** in a scripted brawl (`StartBrawl` on the non-hostile player) the tactics drop to the plain
  last-seen chase for short spells (`chaseLastSeen`, seen as `Engaging` without a tactic in `DebugStateName`). A drink now
  survives it; check whether perception of a non-hostile brawl target loses sight too easily.
- **Bandit:** has drink values (threshold 0.25) but no potion by design — give it one if fights feel too short.
- **Clip / timer drift on a controller swap (review):** equipping a weapon from the open inventory mid-drink (player) or an
  `NpcArmament` swap restarts `Drink_State` from frame 0 (parameters reset, `IsDrinking` re-pushed) while `DrinkTimeline`
  keeps running — the heal can land before the bottle reaches the mouth. Interrupt or re-sync on `OnVisualsRefreshed` if it
  shows in play.
- **Save mid-drink is blocked (review):** `PlayerPotionDrinker` holds `SaveSystem.BlockSaving` for the drink (≈ 2.5 s); an
  autosave that lands then is refused. Revisit if autosaves start failing visibly.

### From spec-shield-offhand-blocking (2026-11-17)

- **Shield block / idle clips:** the block reuses the sword's `Block Idle` (left forearm raised forward; since
  spec-one-hand-braced-block it plays only with a shield in hand, the sword alone has `Sword Braced Guard`). `ShieldSO.heldPose` is
  solved for that block pose, so in combat idle / locomotion (hand hanging) the shield sits flat at the hip — the two hand
  orientations are 118° apart, no rigid grip fits both. Needs a shield guard idle + block clip (UpperBody override for
  one-hand + shield), then re-solve `heldPose`.
- **Shield bash / shield-specific perfect-block reaction:** out of scope.
- **NPC shield values:** NPCs ignore `blockStaminaCost` / `perfectBlockWindow` (they roll the style's `perfectBlockChance`
  and spend stance). Scale the parry chance by the shield if shields should feel different on NPCs.
- **Bandit back clutter:** a bandit carrying a bow has no bow visual while melee; if a carried-bow visual is added later it
  shares `BackWeaponSocket` with the shield.
- **Shield poses tuning:** `backPose` (on `BackWeaponSocket`, from the bind pose) and `heldPose` were computed offscreen, not
  tuned in play — check the Kite's lower tip against the hips while running.
- **Shield loot depends on the weapon in hand (review):** an NPC downed while holding its bow has its shield stowed in the
  (lootable) inventory; downed with the melee weapon, the shield stays equipped (not lootable, like armor). Pick one rule.
- **Restore re-grants a looted starting shield (review):** `NpcArmament.GrantMissingStartingShield` (pre-shield save compat)
  can't tell "save predates shields" from "player looted the stowed shield" — loot, save, load gives the NPC a fresh one.
  Save a stowed-shield / granted flag if it matters.
- **Left-handed one-hander + shield (review):** `ShieldSO.heldPose` is solved for the left hand; a future left-hand melee
  archetype would put the shield in the right hand with the left-hand pose. Add a right-hand pose then.
- **No PlayerCombat block tests (review):** "no shield → refused", cost / window from the shield and the mid-block lower are
  verified in play only; the pure rules (`EquipmentRules`, `EquipmentVisuals.ShouldHoldShield`) are tested.

## From spec-sealed-passage-door-route (2026-11-18, split at clarify)

- ~~**SealedPassage goat-track route (B2, next spec, user decision).** Third way into the Ruins 2 ring for quest-002
  (`docs/World/Quests/SealedPassage.md`): a steep but walkable track from the forest edge south-east of the quarry,
  over the ring's rim and down into it on foot (no rope), a gap in the invisible `Quarry/RuinsRing` colliders where it
  crosses, and a small group of existing hostile creatures along it ("the wilderness is dangerous"). Reuses B1's
  ring-entry `WorldFactTrigger`. Check reachability against surface slope (Scenes/CLAUDE.md: the player can jump up
  steep slopes) and keep the ring closed everywhere else.~~ **Done 2026-11-18** (`spec-sealed-passage-goat-track`).

## From spec-sealed-passage-door-route (2026-11-18, review)

- **A failed quest can flip to completed (review).** `QuestSO.IsFailed` now returns false once `IsCompleted` (completion
  wins, per spec). For a quest whose completion fact can still be set after a fail fact, `QuestEventsManager` would
  raise Failed and then Completed, grant both reward sets, and flip `QuestFact(IsFailed)` readers back. Not reachable in
  SealedPassage (a dead Bren can't take the report). Snapshot the first terminal state if a quest ever needs it.
- **The owned cleft door and theft (review).** The door's `Ownership` (Dace) makes every closed-door use a theft while
  Dace lives, including opening it with his own key, and ~~from the inside focusing the leaf instead of the small
  `InsideLatch` also counts (and spends a pick). The inside case only arises via the goat track (B2): decide there
  whether the inside face should skip ownership or the latch collider should be larger.~~ **Inside case done
  2026-11-18** (`spec-sealed-passage-goat-track`): the leaf defers to the latch while it is usable. Opening from outside
  with Dace's key still counts as a theft (kept: the key is his).
- ~~**Quest facts set before the quest starts (review).** `RingEntryTrigger` and Odo's `ContainerTakeFact` fire whether or
  not Bren's quest was accepted, so a player who explores first gets step 0 done on accept and Bren's report topic
  unlocks off `TokenTaken` without the quest started. Decide if the report memory should also require
  `QuestFact_SealedPassage_Started`.~~ **Done 2026-11-18** (`spec-sealed-passage-goat-track`): it does. Step 0 can
  still complete on accept for an explorer (intended: the quest log catches up).
- **Closed-scene setter scan misses prefab instances (review, pre-existing).** `QuestIndexCollector` only reads top-level
  `--- !u!114` blocks, so a `ContainerTakeFact` / `WorldFactTrigger` inside a prefab instance of a closed scene stores
  `_fact` in `m_Modifications` and reads as "never set" (false V3).
- **Odo's belongings are food (review).** `OdoBundle` holds `Item_Bread` and `Item_BrownMushroom` because no worthless
  item exists yet; swap for real junk (a worn tally string, a broken comb) when one is added.

## From spec-sealed-passage-goat-track (2026-11-18, review)

- **Ring gap follows the layout line only (review).** `BuildRuinsRing` opens the gap wherever `path-goat-track` crosses
  the loop, with no check that the terrain was graded there (edit the line + rerun Sites without regrading and capturing
  → a full-height hole over raw cliff). `TrySplitAtCrossing` also handles one crossing per wall: a second crossing of the
  same wall, a crossing within ~2.5 m of a ring corner (the neighbouring wall isn't opened), or a track running along a
  wall stay walled; the "crosses N times" warning counts split walls, not crossings. Fine for today's line (one square-ish
  crossing mid-wall, verified in play); harden if the track ever moves.

## From spec-sealed-passage-report-rework (2026-11-18, review)

- ~~**Odo's tally token stays in the bag forever after the lie or "nothing" ending (review).**~~ Resolved by
  `spec-sealed-passage-odo-ring-drop`: the token is now `Armor_Odo_Ring`, a normal item (wear, sell, drop). `Item_Odo_TallyToken` is a quest
  item (`CanPlayerPart` blocks drop / sell / give), and `Mem_QuarryWorker_Report` closes on every ending, so a player who
  lies or keeps it carries it for good. Either let quest items become normal items once their quest completes, or give
  the token a later use (a shrine, the fort beat that exposes the lie).
- **Closed-scene setter rows look dead in the Quest Explorer (review, pre-existing).** `QuestDetailView` only exempts
  `FactLinkSource.ClosedScene` from the "object no longer loaded" check, so `ContainerTake` / `WorldFactTrigger` /
  `DoorLatch` setters read from a closed scene show as missing, with no "Open scene & select" button.
- **Closed-scene WorldFact setter scan sits behind a KilledFact early return (review, pre-existing).**
  `QuestIndexCollector.CollectClosedScenes` returns when no `KilledFact` exists, before the setter scan. Harmless today.
- **Kind-lie payoff (design).** `DialogueFact_QuarryWorker_ReportLied` has no world consumer yet; a later Company / fort
  beat (e.g. Dace or a fort clerk naming Odo) can expose it.

## From spec-sealed-passage-odo-ring-drop (2026-11-18, review)

- **`TheftPursuitRegistryTests` fail on a second EditMode run in the same domain (pre-existing).** `TheftPursuitRegistry`
  is a static set cleared only on play (`SubsystemRegistration`); the tests claim fixed ids and never clear it, so a rerun
  without a domain reload fails 3 tests. Add a test-only reset (or `[SetUp]` clear) to the registry.
- **Goat-track drop check is a sampled simulation (design note).** `Tools/World/Check Goat Track Drop` drives a
  `CharacterController` from a 2 m grid of pit starts in 12 directions, then three turns towards the ledge. It is evidence,
  not proof: a player steering differently (zig-zags, sprint, crouch-jumps) is not covered. Walk the drop and try to climb
  back in play mode after any change to the track, the pit or `DropBlockers`.

## From spec-movement-stamina-fall-damage (2026-11-19, review)

- **A refused jump gives no feedback (UX).** Aggroed with less than `aggroJumpStaminaCost`, Jump does nothing but log.
  Add a "stamina short" HUD pulse / sound, shared with refused dodges, attacks and blocks.
- **Scripted / spawn teleports don't call `PlayerController.ResetVerticalVelocity()` (pre-existing).**
  `ArrivalSequence.TeleportPlayer` and `SceneLoader`'s spawn move the transform only. Fall damage relies on its
  > 4 m sideways / upward heuristic for them; a short airborne teleport would keep the old apex and fall speed.
- **Fall damage is avoidable by hugging a face (design).** Any grounded frame (a CC counts steep contacts) ends the
  fall, so sliding down any cliff is free. Intended for the goat track; revisit if cliffs need to hurt.

## From spec-npc-voice-pass (2026-11-19, review)

- **Shared barks still use the old clipped register.** `Barks_*` were out of scope (shouted one-liners); a few read
  oddly ("I hope to never see you again here.", "You there?! Die!"). Give them a light pass of their own.

## From spec-area-1-home-yard-beauty-corner (2026-11-19)

- **Camp kit rollout.** The player's and Hobb's huts are the only two with art; the other 73 huts and 26 tarp bays are
  still graybox, and show in every wide view of the home yard. Swap them to `WorkerHut_Home` / `WorkerHut_Long`
  (rules in `Art/Environment/CLAUDE.md` → *Camp Kit*), cluster by cluster. Many graybox huts interpenetrate their
  neighbours, so a rollout needs a spacing pass, not a one-to-one swap.
- **Meshy pass on the custom pieces.** `Bed_WorkerPallet` and the door flap are Blender models with tiling kit
  textures. Regenerate the bed with Meshy from `Art/Environment/Camp/Concepts/worker bed.png` (target look: `H4 worker
  bed.png`), then replace the mesh under `WorkerBed_Model.prefab`.
- **The player's hut is 3 × 3 m, not the 3 × 4 m of the H2 concept.** The ground rises about 1 m within 2 m of its back
  wall. A longer hut needs the bank behind it flattened in `Area1TerrainBuilder` / the layout, or the hut moved.
- **Rear opening looks at the bank.** The window of the player's hut faces the rising ground, not the hunter fire
  through `BrokenStretch`. Same as the graybox; revisit with the terrain change above.
- ~~**Tarp bays are untouched.** `worker-tarp-north (1)` next to the two huts keeps its graybox canvas and bedrolls; the
  kit's `SK_Roof_Tarp_AsStaticMesh` is a candidate.~~ Resolved by `spec-area-1-home-yard-pass-2` (lean-to and
  `TarpBayEast`, generated tarps; the other 24 graybox bays are still part of the kit rollout).
- **Hut interiors look near black in edit mode only.** The editor shows skybox ambient; in Play the trilight ambient
  from `DayNightConfig` makes the room readable. Judge interiors in Play. An edit-mode ambient preview would help.
- **Kit fire particles read as thin smoke.** `SM_FireCamp_01b` nests the kit's fire and smoke particles, which show as
  pale smoke in URP and run all day. The visible flame is a night-only glow quad. A proper fire VFX is a later pass.
- **Candle flame pops on.** `PS_CandleFlame`'s material has no `_BaseColor`, so `ClockNightLight` cannot fade it: it
  appears at once at dusk while the light fades in.
- **`HutLane_R_1` is under the stage floor (pre-existing).** `ArrivalSequence/HutLanes/HutLane_R_1` at (−3.29, 26.26)
  has no NavMesh within 1 m: the nearest is the stage deck 1.07 m above it. Found while checking the lanes after the
  rebake; not caused by this spec. Move the point off the stage or check that the right-hand guard still walks it.
- **`worker-hut-03` stands on the left escort lane (pre-existing).** Between `HutLane_L_3` (5.03, 32.78) and `L_4`
  (3.82, 38.13) the NavMesh path is 8.4 m against 5.5 m straight, because the graybox hut at about (4.3, 36) is on the
  line. The guard walks round it. Move the lane points or the hut during the kit rollout.
- **No boots, wall pegs or playing cards in the kit.** The interior uses a plank as a peg rail and a flat block as the
  cards. Small custom props for a later pass.

## From spec-hobb-hut-ownership (2026-11-19)

- **NPCs can't sit.** `NpcActivity` has only Stand and Patrol, and there is no sitting clip. Hobb stands beside his
  stool at night (`Spot_TentMate_Night`). Needs a `Sit` activity (seat spot + facing), a Mixamo sit-down / seated idle /
  stand-up set in `Humanoid_Template`, and the brain to stand up before any reaction (trespass, theft, combat, dialogue).
- **Other owned huts.** Only `worker-mate` has its `RestrictedArea` switched on. Owners for the rest come with the
  kit rollout and the camp's NPC roster.
- **Talk openers aren't tied to an incident.** Any own-goods chase spends every `cancelWhenWitnessed` opener of that NPC
  whose condition is true. Fine with Hobb's single opener; scope openers to a container / incident once an NPC has two.
- **No check that owned huts stay owned.** Hobb's ownership is two scene overrides on the generated
  `worker-mate/RestrictedArea`; a builder rename would orphan them silently. A validator listing active, owned hut
  areas (or a rebuild-time check) would catch it.
- **Opener timing edge.** A witnessed chase that times out while the player is still in the container UI ends before
  the take, so Hobb still says "someone's been through my things" later (accepted in spec-hobb-hut-ownership).

## From spec-timed-dialogue-facts (2026-11-19)

- **V18 doesn't see scene talk openers.** The validator checks `ElapsedFact`s in quest parts and memory conditions only.
  An opener (`NPCPresence._talkOpeners`) with a bad `ElapsedFact` (no source, computed source) only logs at runtime.
  Index scene opener conditions in Quest Explorer to cover them.
- **Quest Explorer readers stop at the `ElapsedFact`.** A stored fact read only through an `ElapsedFact` lists no
  readers (e.g. `WorldFact_Hobb_BelongingsRummaged`). Resolve `ElapsedFact.Source` as a reader in `QuestReferenceIndex`.
- **No "source never set" check for an `ElapsedFact`.** V3 skips computed facts, so an `ElapsedFact` over a stored fact
  nothing sets stays always false with no issue. Extend V3 (or V18) to the source's setters.
- **A witnessed theft of any of Hobb's goods spends the pending box opener.** The 2 h delay widens the window in which
  catching the player stealing something else cancels it (the opener isn't tied to an incident; see above).

## From quest design TitheBarnShortfall (2026-11-19, `docs/World/Quests/TitheBarnShortfall.md`)

- **Prerequisites before `/quests:implement TitheBarnShortfall`** (each its own quick-dev spec): ~~P1 HUD time of
  day; P2 sleep / wait to a chosen hour~~ (done 2026-11-19, `spec-hud-clock-and-sleep-hour-picker`; beds only); ~~P3 hour- and NPC-gated
  `WorldFactTrigger`~~ (done 2026-11-19, `spec-gated-world-fact-trigger`); ~~P4 `FactRemovedObject` driven by any `Fact` (incl. `ElapsedFact`) for NPC removal~~ (done 2026-11-19,
  `spec-fact-driven-npc-removal`).
- **Gang revenge when Venn takes the fall (follow-up event / quest, to design).** Reporting Venn cuts the gang's
  grain supply. Gang members from the Mafia Zone should jump the player at night some time after Venn is gone
  (a beating + robbery through the knockout / loot flow rather than a death fight). Brann knows ("you cost us a
  supplier") and may offer a way to settle the debt, which is a second way into the mafia route. Worse after a
  double-cross (`DialogueFact_BarnGuard_DealStruck` + `DialogueFact_FarmReeve_Shortfall_ReportedGuard`).

## From spec-hud-clock-and-sleep-hour-picker (2026-11-19)

- **Final clock dial art.** `UICanvas/Game/Clock` uses placeholder built-in sprites (`UISprite` panel, `Knob` sun / moon,
  a 2 px horizon line). Make a Gothic dial (frame, sun, moon) with `/ui:create` and swap the sprites; `HudClockUI` only
  moves the icons' `anchoredPosition` and toggles them, so any sprites work.
- **Full heal on any sleep length (pre-existing, easier to reach now).** `SleepSystem.ApplySleep` heals to max whatever the
  duration, and the hour picker lets the player pick the next hour (label "under 1 h"), so a few-seconds sleep is a free
  full heal. Scale the heal by hours slept or require a minimum sleep if it matters for balance.

## From spec-gated-world-fact-trigger (2026-11-19)

- **Quest Explorer can't see a blocked gated trigger.** It indexes `WorldFactTrigger._fact` only. A validator rule could flag
  an `InRoutineBlock` `_routineBlockLabel` that matches no `NpcRoutineBlock.label` on that NPC, or a `_requiredNpc` that is
  a prefab asset / in another scene. Today only a runtime `Awake` warning reports the label mismatch.
- **No MonoBehaviour-level tests for `WorldFactTrigger`** (Awake validation, destroyed / inactive NPC). The decision is
  covered by `WorldFactTriggerRulesTests`; a PlayMode test would cover the wiring.

## From spec-fact-driven-npc-removal (2026-11-19)

- **Quest Explorer closed-scene scan misses prefab-instance overrides.** `QuestIndexCollector.CollectClosedScenes` only
  reads `--- !u!114` blocks. A `FactRemovedObject` / `WorldFactTrigger` / `ContainerTakeFact` / `DoorLatch` whose fact
  is a `PrefabInstance` modification (`propertyPath: _fact`) is indexed only while its scene is loaded. Scan the
  modifications too if such prefabs appear (documented in `Editor/QuestExplorer/CLAUDE.md`).

## From spec-tithe-barn-proof-and-leads (2026-11-19)

- **Theft return confiscates quest items.** `EntityBrain.TakeGoods` / `InventorySystem.HandleStolenGoodsRequested` don't
  skip `isQuestItem`, so a witness who catches the player stealing `Item_BrannsTally` takes it into **their own**
  inventory (`RecoverRefusedGoods` does the same after a knockout). `WorldFact_GangStash_TallyTaken` stays set, so the
  Tam / Aldric leads close and the journal says the player has the proof. Fix in code: exclude quest items from
  theft-return (or return them to the source container). Recovery today: loot the catcher.
  - **Returnable quest items: split the tally fact in two (user decision, 2026-11-25).** Replace the one-way
    `WorldFact_GangStash_TallyTaken` with (1) a new **computed** fact type (e.g. `HasItemFact { ItemSO item; int count }`,
    evaluated live in `WorldStateManager.GetFact` against the player's inventory, never stored or saved) for "the player
    carries the tally", and (2) a **permanent stored** fact set when the tally is handed to Pell (the `requiredItem`
    choice). Readers then use "carries it OR handed it over": Tam / Aldric leads (`Mem_BeatenWorker_BarnTally`,
    `Mem_Cook_Tally` invalidations), `Mem_FarmReeve_GateCaught` and the quest step "Something with his name on it".
    Needed with it: `WorldStateManager` raises a fact change when the player's inventory changes (quests / talk openers
    are event-driven); a stolen quest item goes back to its **source container** (the ledger must record it,
    `StolenGoodsTracker` knows it at the take), not into the catcher's bag, so the reopened leads point to the right
    place. Then quest items no longer need to be kept out of theft-return.
- **Saves predating a container's new starting item lose it.** `SaveableObject.Restore` replaces the inventory with the
  saved slots, so a save made before `BrannsStash` got the tally never has it. Dev-only today; matters once saves ship.
- **Tally taken before the quest starts** (HiddenStash Take All) is a quest item the player can't part with, and stays in
  the bag forever if the quest ends on Pim. Cosmetic.

## From spec-barn-guard-night-gate (2026-11-19)

- **`QuestEventsManager` raises step completion before a quest starts.** `EvaluateQuest` checks `IsStepCompleted` with no
  `IsStarted` guard, so a step whose facts turn true early raises `OnQuestStepCompleted` and pays its `PlayerRewardSO`
  with no quest in the log. Guard step events on `isStarted` (and fire the already-true steps when the quest starts).
- **Quest Explorer doesn't index `WorldFactTrigger._requiredFact` as a reader** (`QuestIndexCollector` reads `_fact` only),
  so `BarnDoorTrigger`'s dependency on `QuestFact_TitheBarnShortfall_Started` is invisible to `quest_report`.
- **No validator rule for `HourWindowFact` misuse.** It raises no change event, so in a quest part / talk opener /
  `FactRemovedObject` it would silently never update. A V-rule like V18 (ElapsedFact) would catch it.

## From spec-action-root-motion-jab-trial (2026-11-23)

- ~~NPC unarmed jab not checked in play.~~ Checked in spec-action-root-motion-unarmed-combo (three-hit NPC combo, see
  that spec's notes). Still open: the reach margin at the 1.3 m stopping distance in a real fight, and AI timing tuned to
  the old clips.
- **Ledge stop and lock-on untested for `ActionRootMotion`.** Dodge / block / jump cancels were not run one by one either.
- **Trailing foot of `lead jab step` drags 5–7 cm above the ground** for most of the clip (Meshy source). Needs a foot
  plan like `unarmed_foot_step.py`, or a regenerated source.
- ~~Chaining jab → Hook early drops the rest of the jab's travel.~~ Resolved in spec-action-root-motion-unarmed-combo:
  the travel carries on through clip-driven attack → attack crossfades.
- **A state with unbaked root XZ still blending out adds its travel to the jab.** Blends from a scripted attack are
  skipped; `Landing` (unbaked) is not.
- **A weapon without an animator override plays the base `lead jab step` with the weapon's scripted step**: the clip's
  travel is dropped and the capsule slides by `defaultAttackStep` under stepping feet.
- **`AttackStep.distance` and the clip's travel are two values to keep in sync for NPCs**, and a `rootMotionScale` on an
  in-place or XZ-baked clip roots the player. No validation; a body swap with another avatar scale also changes the travel.
- **`meshy_motion_fit.py` keys rotations only** (plus pelvis location). Its input guards (no armature, no animation,
  `speed` ≤ 0, reversed frame range, bad `fade`) were added in spec-action-root-motion-unarmed-combo.

## From spec-action-root-motion-unarmed-combo (2026-11-24)

- **The hook's lean moves the capsule 6 cm sideways and back** (Unity roots the clip on the centre of mass). Chained
  early the combo ends 3 cm left of the line, chained late 6 cm right (the 8° travel yaw overshoots there). A root that
  follows the feet rather than the centre of mass needs another root source than the Humanoid default.
- **Travel of a step depends on the press time** (hook 0.33–0.40 m, uppercut 0.45–0.52 m): the 0.42 s combo blend
  weights each clip's first frames down. The blend duration is shared with the sword / two-handed combos, so it was
  left alone; a per-weapon transition (or a shorter blend) would make the unarmed steps constant.
- **Late Hook → Uppercut press: the root backs up at 0.2 m/s for a few frames** before the lunge (the hook's settle
  back, already scaled to 40 %), and the rear foot slides 3 cm through that blend.
- **At Animator speed 0.5 the planted-foot slide reaches 4–5 cm through the crossfades** (the blends scale with the Animator speed, so the poses are the same: the measure counts a foot as planted below
  0.3 m/s, and slowed steps fall under that threshold. Read the full-speed slide numbers as a lower bound).
- **NPC foot slide not measured directly.** Their scripted path stays within 7 cm of the player's clip-driven path
  over a three-hit combo (TentMate, flat ground), so up to that much slide under the feet is expected. No real fight
  was run: reach against the 1.3 m stopping distance is still to check (fist 0.59–0.62 m ahead of the root for the three clips).
- **Uppercut's rear foot ends 6 cm short of the jab's start stance** (leg at full reach on its last 3 frames) and the
  jab's trailing-foot hover is unchanged.
- **Ledge stop and lock-on still untested** for the two new steps (same code path as the jab; cancel was run on both).
- **The old `Hook.fbx` / `Uppercut.fbx` / `lead jab.fbx` and their `unarmed_foot_step.py` configs are now dead weight**
  kept for a revert: delete them once the clip-driven combo is accepted.
- **Not run in play:** a sword / two-handed swing after the change (covered by the override-key test only), dodge /
  block / jump cancels one by one on the new steps, and a scripted attack blending into a clip-driven one (no such pair
  exists in the shipped data; covered by the pure `AppliesDuringBlend` test).
- **No automated test of `ActionRootMotion.Begin`'s chain decision** (which attack was entered last): a PlayMode test
  asserting travel inside the restart blend and after a cancel + fresh press would pin it. `CancelAttack` must not
  be made to reset that memory.
- **`meshy_motion_fit.py`**: a `speed` that does not divide the frame range drops the tail of the source (uppercut: last
  source frame used 59.6 of 61, reported as `last_source_frame`); `backtrack_scale` also acts on a lean back before the
  lunge; `facing_offset_deg` is implemented but no config uses it.
- **`action_root_motion_analyse.py` tells a chain crossfade from the idle → jab blend by time** (0.3 s after the first
  press): log the current state as its own column to make that exact.
- **One unexplained outlier**: the first single jab of one play session travelled 0.445 m; three later ones 0.397–0.404 m
  on the same clip and code.

## From spec-npc-meshy-heads (2026-11-24)

- **Eyes are untoned through the colour rule only** (`force_eyes=False` on Meshy heads): a light-brown / hazel iris close
  to the skin's chroma could get toned. If a Meshy NPC has light eyes, force skin 0 inside a smaller eyeball core
  (base eye ellipsoids x ~0.6) while keeping the lids toneable.
- **Shrinkwrap ignores inner geometry**: every moving vertex is cast along ± its normal, so an eyeball back, inner mouth
  or teeth (if the canonical head has them) could be pulled onto the outer Meshy skin. Not visible on StorekeeperMeshy;
  check before a head with an open mouth.
- **Back of the skull is shrinkwrapped too** (spec planned a fade to 0): the Meshy crown sits ~20 mm lower than the base,
  so the crown moves down up to `max_mm`. Accepted on the Storekeeper sheet; check helmets still sit right on him.
- **Ear placement is measured by hand** (side overlay, config `deforms` + `meshy.exclude`); automate it (ear landmark on
  the side render) before a batch of Meshy heads.
- **`_project_meshy` runs three Python-level KDTree / BVH queries per head texel** (~30 s per run): batch / vectorise
  if a larger texture or many heads make it slow.

### From spec-one-hand-braced-block (2026-11-24)

- **One-handed axe plays the braced guard:** `Archetype_OneHandedAxe` shares `Sword_AnimatorOverride`, so an axe without a
  shield blocks with `Sword Braced Guard` (left palm at 0.42 m along the haft, fitted for the sword's blade). Give the axe
  its own override / guard if it reads wrong.
- **Braced guard checked on the Player only:** fitted and measured over Idle / Walking / Running on `Player.prefab`. NPC
  bodies, lock-on strafe and sneak bases (spine bend) were not measured — palm contact and head clearance may drift there.
- **`Sword Braced Guard.anim` is 3.4 MB** (every source curve kept, dense keys, uncompressed). Strip the constant curves to
  two keys or enable compression if repo size matters.

## From spec-base-body-regen (2026-11-24) — split goal

- ~~**Hand-pose layer (spec 2)**~~ — done in spec-hand-pose-layer.
- **More hand poses:** tool / activity grips (smithing hammer, cooking, carrying…) after the base set ships.
- `CompanyGruntArmorBuilder.Build()` copies the leather armor's stats / prices onto `Armor_CompanyGrunt` on every rebuild
  (it reset defense 8 → 15 during the v2 hand graft; reverted by hand). Make the item stats builder-independent.
- Base hero v2: faint sawtooth where the neck band switches atlas (trapezius, 1.53 m) and a few small marks along the
  seam in some poses (`Temp/HeadVariants` sheets). Cosmetic.
- spec-base-body-regen review (deferred, robustness of tools that work today):
  - `SkinnedNeckBaker.BakeLeather`: the rollback restores the meshes but not `LeatherArmor_Visual.prefab` (saved by
    `OutfitHandGraft.ApplyToVisual` inside the same try).
  - `fit_base_body.py`: `run()` has no try/finally (a failure leaves the window on `_BodyV2Fit`, maybe in edit mode);
    `_new_scene` removes objects but not their datablocks (re-runs accumulate meshes / images, names get `.001`);
    `finger_joints` reads `fingers[0]` before the count check and labels fingers by width rank (a long thin thumb track
    could displace the pinky); `deform` has hard mask edges at z 1.15 / x 0; `neck_zip` keeps floating islands;
    `bake_neck_band` / `make_skin_masks._body` assume square textures that are a multiple of `bake_px`.
  - `BaseHeroV2Applier`: `SKIN_REF_LUMINANCE` is copied by hand from `make_skin_masks.py`; no rollback if the second
    prefab save fails after the first (both are validated first now).

## From spec-hand-pose-layer (2026-11-24)

- ~~**Bow wrist:**~~ resolved by spec-bow-hand-grip (handle across the palm + baked wrist offsets).
- **Right-hand string pose:** the bow's right hand stays `Relaxed` through draw / aim / release; a string-hook pose (and
  per-state switching on the UpperBody bow states) is later work.
- **Finger skin at the PIP crease:** in the fist and grips the v2 hand shows dark folds / pinched tips at the PIP joints
  (weights from `fit_base_body.py`, not the pose). Retune the finger weight ramp if it reads badly in game.
- Fist / grips leave up to ~5 mm of finger skin compressed against the palm (`HandPoseCapture` "inside palm"): contact,
  not pass-through. PIP is capped at 81° by the avatar muscle range (limits left unchanged by design).
- Two-hander: both hands keep `TwoHandGrip` while the weapon is drawn, even when the left hand lets go of the handle
  (sheathe before WeaponSwap, knockdown; `TwoHandGripConstraint` treats > 0.55 m as let go). Could feed the constraint's
  grip weight into the left-hand pose.

## From spec-bow-hand-grip (2026-11-24)

- **Right-hand string pose** (extends the spec-hand-pose-layer item above; the user chose to defer it again): the bow mesh is static (straight string, no bone), so
  at full draw the right hand is far from the string. Needs a bendable string (bone or shader) plus a string-hook finger
  pose on the UpperBody bow states.
- **Sheathe Bow release:** the palm is 2 cm from the back grip at the release frame (0.325), but the bow turns ~49° there
  (the sheathe clip doesn't bring the hand back the way the draw takes it). A Blender wrist edit of `Standing Disarm
  Bow` around 0.30–0.35 would make it seamless.
- **Bow on the back at rest:** the seamless sheathed pose hangs the bow nearly vertical along the spine, the upper tip
  above the head. A diagonal carry would need a Draw Bow arm edit.
- ~~**Combat idle with the bow**~~ — done 2026-11-25 (spec-bow-idle-compensated-bake).

## From spec-bow-idle-compensated-bake (2026-11-25)

- **Bow idle legs:** the bow idle is upper-body only; at standstill the legs play the normal `Idle`, not the Mixamo
  archer stance. A bow locomotion set (stance idle + walks) on the Base layer would complete it.
- **Forward lean / right arm:** the baked idle keeps the Mixamo clip's forward lean and its right arm held away from the
  body. Both can be reduced with more offsets in `BowIdleBaker` if they read wrong in play.
- **Seat change is a 95° roll in the hand** (carry ↔ aiming seat, 0.15 s). A weapon bone keyed in the clips, or a
  curve-driven weight, would follow the arm blend exactly instead of a timer.

## From spec-quest-events-start-guard (2026-11-25, review)

- **First fact change before `Start` is lost (pre-existing).** `QuestEventsManager` subscribes in `OnEnable` but seeds in
  `Start`; a fact set by another component's `Awake` / `Start` in between finds no snapshot and is seeded silently, then
  `Start` reseeds over it (e.g. a story sequence starting a quest on scene start raises no Started). Seed in
  `OnEnable` / `Awake` instead.
- **Reset / failed-load branch may skip the quest reseed (pre-existing, verify).** `SaveSystem`'s branch that calls
  `RestoreFacts(null)` + `LoadStartupRegion()` was not seen calling `_questEvents.ReseedState()` (`FinishLoad` does).
  The old run's started / completed snapshot would then swallow the next run's Started.
- **Failed then completed pays both (pre-existing).** `QuestSO.IsFailed` turns false once `IsCompleted` is true, so a
  quest can raise Failed, later Completed, and `PlayerRewards` pays both.

## From spec-sword-three-hit-combo (2026-11-25, abandoned)

The Meshy three-hit sword combo was rejected on look and reverted; sword and axe are unchanged. Kept for another attempt:
`meshy_motion_fit.py` options (`blade_aim`, `time_map`, `upper_body`, `hand_shift`, `pelvis_drop_m`),
`Tools/Blender/configs/sword/`, the three sources, `sword_blade_measure.cs`, `sword_combo_play_measure.cs`.

- **Facts found on the way that still hold:** NPCs roll 1–3 hits from their Entity SO whatever the weapon's `comboSteps`
  (an axe NPC plays a third hit); a held shield only stays upright on a forearm bent forward as in `SwordIdle` and tips
  flat on a lowered arm, in the Mixamo sword clips too; `SwordIdle` holds the kite shield 5 cm from the blade;
  `rootMotionScale` is on the archetype while the clips come from an override a weapon can replace.
- **A clip re-imported during play mode is not picked up by the running Animator**: leave play mode after a re-fit.
- **"Every player attack clip carries all four events"** (`Combat/CLAUDE.md` and its checklist) is wrong for finishers:
  `uppercut step` and `2hand attack 2 refined` carry three (no `ComboWindowOpen`).
- **Play harness:** `sword_combo_play_measure.cs` duplicates most of `action_root_motion_play_measure.cs`; fold the
  unarmed runs into it (no weapon) and drop the older file.

## From spec-sword-shield-take-combo (2026-11-25)

- **Attack exit leg snap.** In combat the legs come from the Base layer's `LockOn Locomotion` (generic `Idle`), not from
  `sword and shield idle` (UpperBody mask only), so every sword attack's exit blends the pack's bladed stance into the
  parallel-feet idle legs in 0.25–0.37 s. A sword combat-idle leg pose (override `Idle` only while drawn, or a Base-layer
  combat idle) would end the swings in their own stance.
- ~~**The rest of the Mixamo Sword and Shield Pack**~~ — block idle and locomotion done (spec-sword-shield-pack-locomotion);
  the attack exit leg snap above is gone for the sword (the combat locomotion idle is `sword shield idle`). Leftovers below.
- ~~**Torch item.**~~ Done (spec-torch-offhand-item); leftovers in its section below.
- **Sword NPC travel** not measured in play (`attackSteps` 0.52 / 0.39 / 0.78 m copied from the player's buffered chain).
- **`sword_combo_play_measure.cs` teleport home** left the player unable to attack (or falling through the world when the
  start was captured before the arrival skip): runs 2+ were refused. This session turned the player 180° between runs
  instead; fix the harness (or teleport + wait for `isGrounded` and re-lock the cursor).
- **Review (deferred):** ~~`EquipmentVisuals.OffhandArmPose` reads the slot only~~ (gated on `TorchInHand`,
  spec-torch-offhand-item). A buffered combo press still fires if the player starts moving before the window opens (the move
  cancel only arms after `ComboWindowClose`); drop the buffer on move input if that feels wrong. `SetDead` /
  `SetUnconscious` end the attack without `ResetAttackCombo` (buffer kept, harmless: cleared on the next state enter).
  `OffhandArmLayerBuilder` run before `HandPoseBuilder` on a fresh controller would put `LeftArm` before the hand layers.
- **Review (deferred, spec-action-bar-equip-swap):** the bar's swap request is fire-and-forget — if `PlayerCombat` is
  disabled or refuses (re-check), nothing equips yet `OnActionBarUsed` was raised (harmless: refresh only). Use an
  acknowledged request (`GoldLootRequest` pattern) with a direct-equip fallback if a silent no-op is ever seen in play.

## From spec-sword-shield-pack-locomotion (2026-11-25)

- **Pack clips still untracked** in `Assets/mixamo packs/Sword and Shield Pack/`: `turn` / `turn (2)` (turn in place ~100°
  R / L from the idle) and `180 turn` / `(2)` (walk / run pivots) need a turn-in-place or pivot state (the player rotates
  in code today); `block` (raise 0.57 s, steps 0.26 m) / `block (2)` (lower 0.67 s) were declined (the visual guard would lag
  the gameplay block); crouch block / crouch idle / crouching, impacts, death, kick, jump, casting, power up, sheath sword 1 / 2.
- **Speed match.** The pack walk is 1.5 m/s native, played at walkSpeed 3 m/s (blend 0.5); run 4.2 → 6. Approved on look
  with time scale 1; raise the combat tree's time scales (builder copies the free tree's) if foot slide shows up later.
- **Sword NPC combat locomotion** checked only in EditMode (tests) and by the player path: watch one guard with a sword
  walk / strafe in combat. An NPC without a `HandPoseDriver` keeps the carry pose over the pack legs.
- **Axe / two-handed / bow / unarmed combat stance**: the combat tree is ready; mapping their `Combat *` keys to a stance
  loop of their own would end their attacks in their own legs too.
- **Review (deferred):** no test covers the runtime composition (`HandPoseDriver` writing the flag, the drivers stepping
  `CombatLocomotion`); checked in play only. `SetCombatLocomotion` does not check the parameter exists (fine while every
  humanoid uses Humanoid_Template). The flag reaches the drivers through two components' `Update`s with no execution
  order: one frame late on an equip change.

## From spec-torch-offhand-item (2026-11-25)

- ~~**NPC torches**~~ Done (spec-npc-night-torch). NPCs never carried one (`NpcArmament` handles shields only; `HandPoseDriver` would already apply
  `TorchHold` from `EquipmentVisuals`). Night patrols / guards with torches need the armament grant + a douse rule.
- **Stowed torch shown on the hip** — hidden today (user decision). `TorchSO.stowPose` is solved (back-left of the belt,
  head up and back) and `Torch_Visual/Sheathed` exists: drop the `SetActive(held)` in `EquipmentVisuals.PlaceTorch`.
- **Blade touch** — the torch comes within 3.6 cm (axis to axis) of the short sword for ~0.06 s in the first sword-and-shield
  cut (clip's own left arm). Accepted, not tuned.
- **Torch in the action bar while drawn** equips at once (no sheathe → equip → draw swap like weapons / shields): over a
  held shield, the shield vanishes and the torch appears in the same frame. Route `TorchSO` through `OnEquipSwapRequested`
  when an offhand item is held (`ActionBarSystem.cs` ~261) if it looks wrong.
- **Stow timing** — the torch hides / reappears at the two-hander / bow draw clip's WeaponSwap event (`_isInCombat`), so it
  stays in the left hand for the first part of the draw. Hide it at `BeginWeaponSwitch` if that reads badly.
- ~~**NPC torch handling**~~ Done (spec-npc-night-torch: grant at night only, unequipped on down; no douse needed — NPCs
  never swim). Was: `NpcArmament.GrantItem` would equip a corpse's torch, `OnDowned` leaves it
  equipped and lit (not lootable), and NPCs never get `SetTorchDoused`.
- **Grip seat** — `heldPose` was solved on the idle Torch Hold frame; walking / running keep the same arm pose, but no
  per-clip check was done. Re-solve if `Torch Hold.anim` is re-fitted.
- **Light budget** — one soft-shadowed point light (6 cube faces) follows the player; drop `_castShadows` if frame time suffers.

## From spec-area-1-home-yard-pass-2 (2026-11-25)

- **Home yard leans on graybox neighbours.** The lean-to's tarp is tied to graybox hut-02's north wall (Hobb-frame line
  `x = 2.81 + 0.5 z`), and `TarpBayEast` was moved 1.26 m west to clear graybox hut-07. When the kit rollout rebuilds or
  moves hut-02 / hut-07, update the tie points in `HomeYardDresser.DressLeanTo` and recheck the bay's east side.
- **Older yard props sit on the escort lanes.** The dresser's lane check lists the first pass's card crate and log seats
  (0–0.12 m from the left lane, segments 3–4), the trunk (0.6 m) and the doorstep sacks and bucket (0.36–0.53 m).
  The escort walks round them (paths as before), but the card game is the cause of the L3→4 detour together with
  hut-03; move it when that lane is fixed.
- **Floor beds read flat at a distance.** The Blender bed is low-contrast under the kit textures (mattress, blanket).
  The Meshy pass on `Camp/Concepts/floor bed.png` (or a hand-painted texture) is the next step, as for the worker bed.
- **Tarp underside is dark by day.** Two-sided canvas with the URP Lit material: the inside of the bay sits in its own
  shadow. A light transmission / double-sided GI tweak would lift it; judge in Play first.
