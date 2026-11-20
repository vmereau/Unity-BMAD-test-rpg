# Player States — aim, drinking, sneak, swimming, aggro movement, stance, unconscious

> Read when touching `PlayerSneak`, `PlayerSwimming` / `SwimRules`, `PlayerMovementStamina` / `PlayerFallDamage`,
> `PlayerStance`, `PlayerPotionDrinker`, the player side of bow aim, or the unconscious flow in `PlayerHealth`.
> Gate table and `IsBusy` rules: `CLAUDE.md` in this folder.

---

## Bow aim — player side

While aiming (`IsAiming`): `PlayerController` faces the camera yaw + `PlayerConfigSO.aimStanceYaw` (45° side-on stance)
with no sprint; `Game.Combat.BowAimCorrection` turns the spine onto the crosshair after the Animator
(`Scripts/Combat/BOW.md`); `CameraController` blends `CombatConfigSO.aimFollowOffset` / `aimFieldOfView` /
`aimSensitivityMultiplier` (refs `_stateManager`, `_cinemachineCamera`). R is blocked while aiming.

---

## Drinking — PlayerPotionDrinker

`CanDrink()` / `IsDrinking` blocks attack, block, aim, R, dodge, jump, execute, sprint — walking stays free; inventory open
is allowed (not `IsBusy`). `PlayerPotionDrinker` (Player root, `IPotionDrinker`) → `SetDrinking(bool)`; `PotionItemSO.OnUse`
hands it potions (kind `Potion`), the caller consumes the item on start. Heal at `CombatConfigSO.drinkHealTime`; interrupted
(potion lost before the sip) by `PlayerHealth.DamageRevision` change, incapacitated, dead, swimming, execute / scripted
hold, a load. `DodgeController` checks `IsDrinking` itself; `PlayerAnimationDriver` re-pushes `IsDrinking` every frame.

---

## Sneak — PlayerSneak

`PlayerSneak` (Player root, implements `Game.Stealth.IStealthTarget`) owns the **Crouch (C)** toggle and
reads Sprint/Move: Sprint held while moving → `ExitSneakToSprint()` (clears `IsSneaking`, fires
`SneakToSprint` → `sneak to sprint` clip into running). `CanSneak() = !IsBusy && !IsAirborne && !IsDodging`
— attacking and blocking are allowed and **never** clear sneak. The other exits live in
`PlayerStateManager`: `SetDodging(true)`, `NotifyJumpStarted()`, `SetDead(true)` and `SetInDialogue(true)` (NPC conversations only — pickups, doors and containers keep the stance) clear `IsSneaking`
without a stand-up clip; `PlayerSneak` also clears it when `IsAirborne` (walking off a ledge). `PlayerController` uses `PlayerConfigSO.sneakSpeed` while sneaking.
`PlayerSaveAdapter.Restore` forces `SetSneaking(false)` (sneak is not saved). `VisibilityPoint` = feet +
`StealthConfigSO.standing/sneakingVisibilityHeight` (the AI line-of-sight aim point).

---

## Swimming — PlayerSwimming (spec-water-and-swimming)

`PlayerSwimming` (Player root, `[DefaultExecutionOrder(-10)]`, before `PlayerController`) finds water with one
`OverlapSphereNonAlloc` just above the feet on `_waterMask` (layer `Water`, triggers only); surface = highest
`collider.bounds.max.y`. No water script, no trigger callbacks (those would also fire for child hitboxes). Pure rules:
`SwimRules` (`SwimRulesTests`). Tunables: `PlayerConfigSO` `[Header("Swimming")]`.

- **Depths** = surface above the feet. Keep `swimExitDepth < swimEnterDepth <= swimFloatDepth`, or the float height
  ends the swim at once. The shore needs no code: rising ground pushes the `CharacterController` up past the exit depth.
- **`PlayerController`:** `ApplySwimHeight` replaces jump + gravity while swimming; speed = `swimSpeed` / `swimSprintSpeed`.
- **Sprint:** `IsSwimSprinting` drains `swimSprintStaminaPerSecond`, consumed in whole points (`StaminaSystem.Consume` logs
  and raises the HUD event each call). Empty → normal speed until Sprint is released.
- **Gates:** `!IsSwimming` wraps the instance `Can*` methods; the pure `Evaluate*` signatures stay untouched. `IsAirborne`
  stays false while swimming. `DodgeController` checks `IsSwimming` itself. `LockOnSystem` releases and refuses a lock.
- **Weapon:** `PlayerCombat.SheatheForSwimming` ends attack / aim / block and sheathes instantly, without a clip (the
  swim state is full-body, which hides the UpperBody sheathe).
- `PlayerAnimationDriver` pushes `IsSwimming` **every frame**: `Refresh()` and `Rebind()` reset all parameters.
- A dodge delays the swim until it ends. Death clears `IsSwimming`; the body sinks. Not saved: detection re-runs after a load.
- **Stance reactions:** the driver sends `IsSwimming && !IsIncapacitated`, so a stagger / knockdown clip isn't cut by the
  held bool. `CameraController` limits look-up to `_swimPitchMin` while swimming (the surface is invisible from below).
- Water surface and basin recipe: `Prefabs/CLAUDE.md` → Water volumes.

---

## Aggro movement costs & fall damage (spec-movement-stamina-fall-damage)

- `PlayerMovementStamina` (Player root, `[DefaultExecutionOrder(-5)]`: after `PlayerSwimming`, before `PlayerController`):
  while `IsAggroed`, running (Sprint + move, `CanMove`, not swimming / sneaking / aiming / drinking / dodging) drains
  `SprintStaminaPerSecond` in whole points (`MovementStaminaRules.DrainStep`, same pattern as swim sprint); short →
  `IsRunExhausted` → `PlayerController` drops to walk speed until Sprint is released. `TryPayJump()` (called by
  `ApplyJump` after `CanJump` + input) charges `JumpStaminaCost` while aggroed; short → no jump, nothing spent. Out of
  aggro both are free. Base values `PlayerConfigSO` `[Header("Aggro")]`; **modifiers go through
  `SprintDrainMultiplier` / `JumpCostMultiplier`** (equipment / skills / stats) — never edit the config at runtime.
- `PlayerFallDamage` (Player root, `LateUpdate`): fall = highest y since `CharacterController.isGrounded` was last true
  − landing y; `MovementStaminaRules.FallDamage` (`[Header("Fall Damage")]` `fallSafeHeight` 11, `fallDamagePerMetre`
  4) → `PlayerHealth.TakeUnmitigatedDamage` (no Defense, never a knockout, hurt sound, can kill). Any grounded frame ends
  the fall — a CC counts steep terrain contacts as ground, so sliding down a face is several short falls (the goat-track
  drop's ~10 m + ~7 m steps deal nothing). Swimming, death, unconscious, a one-frame move > 4 m **sideways or upward**
  (never downward: a hitch mid-fall must not cancel it) and `ResetFall()` (called by `PlayerController.ResetVerticalVelocity`,
  i.e. a load teleport) cancel it. A landing in swimmable water (`PlayerSwimming.IsInSwimmableWater` — a fast fall can hit a
  shallow pool's floor before swim mode starts) deals nothing. Landings ≥ 2 m log their height (tuning).
- The run debt is kept between runs (tapping Sprint never runs free); exhaustion clears on Sprint release or aggro end.
  `PlayerSneak.IsSprintingWhileMoving` ignores an exhausted Shift (walk speed, so sneak isn't dropped).

---

## Stance — PlayerStance (spec-player-stance-stagger-knockdown)

`PlayerStance` (Player root, `Game.Combat.IStanceTarget`) runs the shared `StanceMeter` with the `CombatConfigSO`
`[Header("Player Stance")]` settings — same rules as `EntityStance` (`Scripts/AI/COMBAT.md` → Stance). AI hits call
`ApplyStanceDamage` (landed) / `ApplyBlockedStanceDamage` (regular block: × `blockStanceMultiplier`, never knockdown);
perfect block / dodge never reach it. Hyper-armor: `playerAttackStanceResistance` while `IsAttacking`. A reaction →
`PlayerCombat.InterruptForStance()` (attack + combo ended, block lowered, PB window closed, aim stopped) then
`PlayerStateManager.BeginStagger(variant)` / `BeginKnockDown()` (flag + exits sneak + `PlayerAnimationDriver.PlayStagger`
/ `PlayKnockDown`); recovery → `EndStanceReaction()`. Health damage still lands while down. `SetDead(true)` clears the
flags (and the meter refills while dead); `PlayerSaveAdapter.Restore` calls `ResetStance()` (a load mid-reaction rebinds
the Animator). Systems outside the `Can*` gates check `IsIncapacitated` themselves: `DodgeController`, `InteractionSystem`
(no focus / E), `ActionBarSystem.TryUseSlot`, `UIScreenManager.OpenTab` (an equip mid-reaction would swap the controller),
`PlayerController` lock-on rotation. Hidden — no UI, not saved.

---

## Unconscious — PlayerHealth (spec-unconscious-knockout)

A 0-HP blow from a non-hostile humanoid knocks the player out (rule in `Scripts/AI/KNOCKOUT.md` → Unconscious): no
`_onPlayerDied`, no death screen. `PlayerHealth.KnockOut` → `PlayerCombat.InterruptForStance()` +
`PlayerStateManager.SetUnconscious(true)` (`IsUnconscious` is part of `IsIncapacitated` → every gate closes; clears
block / attack / aim / sneak / stance flags; held knocked down clip) and raises `OnPlayerKnockedOut` (`GameEventSO_Void`):
`UIScreenManager`, `ContainerUI`, `CookingUI`, `SleepUI`, `NPCTradeUI` and `DialogueSystem` close like on a death. `PlayerHealth.Update` regains HP (HUD event each
tick), then `BeginGetUp()` (get-up clip), then `SetUnconscious(false)`. Monster / hostile hits still kill a downed player
(`SetDead` clears the flag). Not saved: `RestoreHealth` clears it with `resetAnimator: true`. `PlayerStance` refills and
ignores stance while unconscious.
