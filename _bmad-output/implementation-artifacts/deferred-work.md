
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
- **PlayerAnimationDriver NRE at startup** — `EquipmentVisuals.OnEnable → PlayerCombat.HandleVisualsRefreshed →
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

- **Arrival sequence** — escorted arrival, the public beating at the center stage, escort to the tent, Tent Mate briefing.
- **Warden's errand tour** — report to each work site manager; tools issued as Company property.
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
- **`PlayerAnimationDriver.SetInCombat` null reference at startup** — `EquipmentVisuals.OnEnable` → `Refresh` → `PlayerCombat.HandleVisualsRefreshed` reaches `_humanoidBridge` before it is set, once per play session. Seen in the blockout's play test, outside the code it changed.
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
- **`NullReferenceException` at `PlayerAnimationDriver.cs:85` on entering play mode.** Reached from `EquipmentVisuals.OnEnable` → `Refresh` → `PlayerCombat.HandleVisualsRefreshed` → `PlayerStateManager.SetInCombat`. It looks like an init-order issue; none of those files were touched by this spec.
- **`Waterfall_Placeholder` reads grey above the water line.** The `Water` material shows only on the lowest part of the box. It is a placeholder and unchanged by this spec.
