---
title: 'Player draw / sheathe weapon animations'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: 'ec40f6185b9055fa7430cc83456c55a014dfdd1e'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Pressing R toggles combat instantly: the weapon visual jumps between hand and hip/back socket with no animation.

**Approach:** R plays a quick, upper-body draw or sheathe clip (sword clips by default, bow clips through a new bow override). An animation event moves the weapon visual at the right frame, and an SMB exit is the safety net. Unarmed goes through the same flow: it plays the default (sword) clips for now with no visual swap, until a dedicated unarmed clip exists.

## Boundaries & Constraints

**Always:** Animator writes go only through `PlayerStateManager` → `PlayerAnimationDriver` → `HumanoidAnimationBridge`. The new SMB is a silent no-op without `AnimationEventReceiver` (`if (receiver != null)`, never `?.`). Clip changes go through `ModelImporter.clipAnimations`, never raw `.meta` YAML. Legs keep moving during the clip (masked `Attack` layer only).

**Ask First:** Any change to the Base layer, or adding draw/sheathe to NPCs.

**Never:** NPC draw animations. Attack-cancels-draw. Saving the drawn stance. Hardcoded per-weapon logic in code (clips come from override controllers).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Draw | Sheathed, R | `IsInCombat` true, draw clip plays, weapon moves to hand at `WeaponSwap` event, `CombatIdle` after | N/A |
| Sheathe | Drawn, R | `IsInCombat` false, sheathe clip plays, weapon moves to sheath socket at event | N/A |
| Locked during switch | Switch in flight, R / Attack / Block | Ignored | N/A |
| Gate | Busy, attacking, blocking, dodging or airborne, R | Ignored | N/A |
| Interrupted | Dodge/jump before the event | SMB exit of the matching state completes the swap | N/A |
| Re-toggle in blend | R after swap, previous state still blending out | Old state's exit does NOT complete the new switch (direction must match) | N/A |
| Lost callbacks | No event / exit within `weaponSwitchTimeout` | Swap forced, warning logged | `GameLog.Warn` |
| Equip change / load / death mid-switch | Visuals refresh, save load, `SetDead` | Switch ends cleanly; visual matches `IsInCombat` (load → sheathed) | N/A |
| Unarmed | No weapon, R | Default clip plays, state toggles, no visual move | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `OnDrawWeaponStarted` (instant toggle today), `HandleVisualsRefreshed`, `Update` watchdog
- `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs` -- clip event + SMB routing to `PlayerCombat`
- `Assets/_Game/Scripts/Combat/SMB_AttackState.cs` -- model for the new SMB
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- flags, `CanX()` gates, `SetDead`
- `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs`, `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- animator params
- `Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs` -- forces sheathed on load
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- tuning
- `Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `Attack` layer (UpperBodyMask), AnyState → Block/Jump/Fall/Dodge
- `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/{Sword,Bow}/` -- `draw sword`, `sheath sword`, `Draw Bow`, `Sheathe Bow` clips; `Sword_AnimatorOverride`
- `Assets/_Game/Data/Items/Weapons/Archetypes/Archetype_Bow.asset` -- override currently null

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Config/CombatConfigSO.cs` -- add `weaponSwitchAnimSpeed` (1.5) and `weaponSwitchTimeout` (2 s) under a "Draw / Sheathe" header -- tunable without touching the controller
- [x] `HumanoidAnimationBridge.cs` + `PlayerAnimationDriver.cs` -- `PlayWeaponSwitch(bool draw, float speed)` (sets `WeaponSwitchSpeed`, fires `DrawWeapon`/`SheatheWeapon`) and `ResetWeaponSwitchTriggers()` -- bridge-only animator writes
- [x] `PlayerStateManager.cs` -- `IsSwitchingWeapon`, `BeginWeaponSwitch(bool draw, float speed)`, `EndWeaponSwitch()`; `CanSwitchWeapon()` + pure `EvaluateCanSwitchWeapon(busy, attacking, blocking, dodging, airborne, switching)`; `CanAttack`/`CanBlock` require `!IsSwitchingWeapon`; `SetDead(true)` keeps the flag (IsBusy blocks R; watchdog/load still fix the visual) -- single owner of state
- [x] `PlayerCombat.cs` -- R: gate on `CanSwitchWeapon()`, `SetInCombat(entering)`, `BeginWeaponSwitch`, store `_pendingDraw` + start time. `CompleteWeaponSwitch()` (no-op unless switching): `SetCombatState(IsInCombat)` + `EndWeaponSwitch()`. Called from `OnWeaponSwapEvent()`, `OnWeaponSwitchStateExited(bool drawState)` (only if `drawState == _pendingDraw`), the `Update` timeout (warn), `HandleVisualsRefreshed`, and `OnDisable` -- event times the swap, the other paths guarantee it
- [x] `AnimationEventReceiver.cs` -- `WeaponSwap()` (clip event) and `NotifyWeaponSwitchExited(bool drawState)` -- routing
- [x] `Scripts/Combat/SMB_WeaponSwitchState.cs` (new, with `.meta` including a `MonoImporter` block) -- `[SerializeField] bool drawState`; `OnStateExit` → receiver; receiver cached as in `SMB_AttackState`
- [x] `PlayerSaveAdapter.cs` -- when `IsInCombat || IsSwitchingWeapon`: `EndWeaponSwitch()`, `SetInCombat(false)`, `SetCombatState(false)`
- [x] `Humanoid_Template.controller` (via editor code) -- `Attack` layer: triggers `DrawWeapon`, `SheatheWeapon`, float `WeaponSwitchSpeed` (default 1.5). States `Draw_State` (`draw sword`) and `Sheathe_State` (`sheath sword`) with speed param `WeaponSwitchSpeed` and `SMB_WeaponSwitchState` (draw true/false). AnyState → each on its trigger (~0.1 s blend, no self-transition). `Draw_State → CombatIdle` on exit time. `Sheathe_State → LockOn Locomotion` (IsSneaking false) / `→ Sneak Locomotion` (IsSneaking true), on exit time -- upper body only
- [x] FBX importers for the 4 clips -- add `WeaponSwap` event: draw sword 0.40, sheath sword 0.65, Draw Bow 0.45, Sheathe Bow 0.65 (normalized; first guesses, tuned in play) -- swap timing
- [x] `Combat/Bow/Bow_AnimatorOverride.overrideController` (new) -- base `Humanoid_Template`; override `draw sword`→`Draw Bow`, `sheath sword`→`Sheathe Bow`; assign to `Archetype_Bow.animatorOverrideController`. Add explicit `draw sword`/`sheath sword` self-entries to `Sword_AnimatorOverride` -- a future unarmed clip then only replaces the base-controller clip
- [x] `Tests/EditMode/WeaponSwitchGateTests.cs` (new) -- cover `EvaluateCanSwitchWeapon` for each gate
- [x] Docs: `Scripts/Combat/CLAUDE.md` (Draw/Sheathe section), `Controllers/CLAUDE.md` (params/states), `Animations/Combat/CLAUDE.md` (new folder layout + `WeaponSwap` timings), `PLAYER_COMBO.md` routing table

**Acceptance Criteria:**
- Given a sword equipped and sheathed, when R is pressed while running, then the legs keep running, the draw clip plays at `weaponSwitchAnimSpeed`, and the sword appears in the hand mid-clip.
- Given the bow equipped, when R is pressed, then the bow clips play (not the sword ones), and the bow moves between back socket and hand.
- Given a switch completed, when R is pressed again quickly, then the reverse clip plays and its own swap happens at its own event.
- Given an NPC using `Humanoid_Template`, when it enters combat, then nothing changes (no draw state, no errors).

## Design Notes

`IsInCombat` flips at R (keeps the animator bool consistent). Only the visual waits for the event. AnyState transitions are evaluated before `LockOn Locomotion → CombatIdle (IsInCombat)`, so `Draw_State` wins on the same frame. R is gated on blocking/airborne/dodging because their AnyState conditions are persistent bools and would immediately pull the layer out of the draw state.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- all green, incl. `WeaponSwitchGateTests`
- `read_console` (errors) after recompile -- none

**Manual checks (if no CLI):**
- Play mode: draw/sheathe sword, bow, unarmed. Dodge mid-draw. Save/load mid-sheathe. Equip a different weapon while drawn.

## Suggested Review Order

**Switch flow (R → clip → swap)**

- Entry point: R flips the stance now and starts the clip; the visual waits.
  [`PlayerCombat.cs:236`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L236)

- Single completion point — releases the lock, then moves the visual to match IsInCombat.
  [`PlayerCombat.cs:688`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L688)

- State-exit net filtered by direction so a quick re-press isn't finished early.
  [`PlayerCombat.cs:678`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L678)

- Same-frame dodge/jump completion + speed-scaled watchdog.
  [`PlayerCombat.cs:207`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L207)

- Equip change mid-clip completes the switch and re-pushes IsInCombat after the controller reset.
  [`PlayerCombat.cs:411`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L411)

**State & gating**

- Switch lock owner; trigger fired through the driver/bridge.
  [`PlayerStateManager.cs:166`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L166)

- R gate excludes held-bool AnyState states (block/dodge/airborne).
  [`PlayerStateManager.cs:263`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L263)

- Attack/Block locked while switching.
  [`PlayerStateManager.cs:246`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L246)

- Load forces sheathed even with a sheathe in flight.
  [`PlayerSaveAdapter.cs:158`](../../Assets/_Game/Scripts/Player/PlayerSaveAdapter.cs#L158)

**Animator plumbing**

- Speed param + trigger, opposite trigger cleared.
  [`HumanoidAnimationBridge.cs:92`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L92)

- Event and SMB routing.
  [`AnimationEventReceiver.cs:44`](../../Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs#L44)
  [`SMB_WeaponSwitchState.cs:17`](../../Assets/_Game/Scripts/Combat/SMB_WeaponSwitchState.cs#L17)

- Draw_State / Sheathe_State on the Attack layer (+ AnyState triggers, Draw→Attack_1).
  [`Humanoid_Template.controller:361`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L361)

**Peripherals**

- Tuning values.
  [`CombatConfigSO.cs:58`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L58)

- Tests: gate + load mid-sheathe.
  [`WeaponSwitchGateTests.cs:1`](../../Assets/Tests/EditMode/WeaponSwitchGateTests.cs#L1)
  [`PlayerSaveAdapterTests.cs:205`](../../Assets/Tests/EditMode/PlayerSaveAdapterTests.cs#L205)
