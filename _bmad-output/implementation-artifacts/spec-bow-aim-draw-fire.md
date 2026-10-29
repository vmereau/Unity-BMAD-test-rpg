---
title: 'Bow aiming: hold to aim, nock / fire animations, OTS zoom'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: '9768ea8bfd3d23e719617fac326f93372aa2c90b'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** With a bow drawn, a left click fires an arrow instantly with no animation and no aiming. There's no way to aim and no shot rhythm.

**Approach:** With a bow drawn, right click (the Block action) is hold-to-aim. It plays `Draw arrow` → looping `Aim Overdraw`, and while held the camera zooms over the shoulder and mouse sensitivity drops. A left click while the arrow is nocked fires through the existing `TryFireBow`. A successful shot plays `Fire arrow`, then `Draw arrow` again. Shooting is locked until the aim pose is reached again.

## Boundaries & Constraints

**Always:**
- Animator writes only go through `PlayerStateManager` → `PlayerAnimationDriver` → `HumanoidAnimationBridge`. New states live on the upper-body `Attack` layer, so legs keep moving. New SMBs are silent no-ops without `AnimationEventReceiver` (`if (receiver != null)`).
- The bow can't block: with a `BowSO` equipped, Block input starts and ends aiming instead.
- Left click with a bow drawn but not aiming → ignored. Firing needs aiming + arrow nocked (aim state entered). The fire gates stay as they are (cooldown → stamina → ammo / "Out of arrows" toast). A refused shot plays nothing and keeps the arrow nocked.
- While aiming: the body faces the camera yaw (+ tunable offset), walk speed max (no sprint, sneak speed still applies), camera follow offset closer / over the shoulder, smaller FOV, sensitivity × multiplier, all blended in and out.
- Aim ends (camera back to normal, no auto-resume while the button is still held) on: right-click release, dodge, airborne, R / weapon switch, busy (menu / dialogue), death, an unblocked hit (`TryReceiveHit` → NotBlocked), equipment visuals refresh, leaving combat, or disable.
- Clip changes go through `ModelImporter.clipAnimations`; controller edits go through editor code, never raw YAML.

**Ask First:** Base-layer changes. Adding an input action. Touching the melee combo flow.

**Never:** Hip-fire. A crosshair / HUD change. NPC archers. Arrow-in-hand visuals. Draw-strength / charge mechanics.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Start aim | Bow drawn, RMB down | `Draw arrow` → `Aim Overdraw` loop, camera zoom | N/A |
| Fire | Aiming, nocked, arrow + stamina | Arrow fires (existing flow), `Fire arrow` → `Draw arrow` → aim; nock cleared until aim re-entered | N/A |
| Click while nocking | Aiming, in draw / fire clip | Ignored, nothing spent | N/A |
| Click no ammo | Aiming, nocked, 0 arrows | "Out of arrows" toast, stays nocked, no clip | Info log |
| Click not aiming | Bow drawn, RMB up | Ignored | N/A |
| Release | RMB up at any point | Back to `CombatIdle`, camera restores | N/A |
| Interrupt | Dodge / jump / R / hit / menu while aiming | Aim ends; still-held RMB doesn't resume | N/A |
| Sword | Sword drawn, RMB | Blocks as today | N/A |
| Lost nock callback | Aim state enter never arrives | Nock forced after `bowNockTimeout` | `GameLog.Warn` |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- Block / Attack input, `TryAttack` bow branch, `TryFireBow`, `TryReceiveHit`, `HandleVisualsRefreshed`, `Update`
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- flags, `Can*` gates, `EvaluateCanSwitchWeapon`, `SetDead`
- `Assets/_Game/Scripts/Player/PlayerAnimationDriver.cs`, `Scripts/Core/Animations/HumanoidAnimationBridge.cs` -- animator params
- `Assets/_Game/Scripts/Combat/AnimationEventReceiver.cs`, `SMB_WeaponSwitchState.cs` -- receiver + SMB model
- `Assets/_Game/Scripts/Player/CameraController.cs` -- look, `UpdateCameraTargetHeight` writes `FollowOffset`; Cinemachine lens FOV 40 on the Player prefab
- `Assets/_Game/Scripts/Player/PlayerController.cs` -- `ApplyMovement` free-move rotation / speed
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs`, `PlayerConfigSO.cs` -- tuning
- `Art/Characters/Humanoids/Controllers/Humanoid_Template.controller` -- `Attack` layer, `CombatIdle`
- `Art/Characters/Humanoids/Animations/Combat/Bow/` -- `Draw arrow`, `Aim Overdraw`, `Fire arrow` clips

## Tasks & Acceptance

**Execution:**
- [x] `CombatConfigSO.cs` -- "Bow Aim" header: `aimFieldOfView` 30, `aimFollowOffset` (0.6, 0.3, −1.8), `aimSensitivityMultiplier` 0.5, `aimBlendSpeed` 8, `bowNockTimeout` 2.5 s (> Draw arrow + Fire arrow ≈ 1.73 s) -- tuning
- [x] `PlayerConfigSO.cs` -- `aimBodyYawOffset` 0 (degrees) -- corrects the Mixamo pose facing in play
- [x] `HumanoidAnimationBridge.cs` + `PlayerAnimationDriver.cs` -- `SetAiming(bool)` (`IsAiming`), `PlayBowFire()` / `ResetBowFireTrigger()` (`BowFire`) -- bridge-only writes
- [x] `PlayerStateManager.cs` -- `IsAiming`, `SetAiming(bool)` (clears `BowFire` on false); `CanAim()` + pure `EvaluateCanAim(busy, airborne, dodging, attacking, blocking, switching, inCombat)`; `EvaluateCanSwitchWeapon` gains `isAiming` (R calls `StopAim` first, so R ends the aim and then switches); `SetDead(true)` clears aiming -- single owner
- [x] `PlayerCombat.cs` -- Block started / canceled route to `StartAim` / `StopAim` when a `BowSO` is equipped. `StartAim` gated by `CanAim()`, resets `_arrowNocked` + nock timer. Bow attack: require `IsAiming && _arrowNocked`; `TryFireBow` returns bool; success → `_arrowNocked = false`, `PlayBowFire()`. `OnBowAimEntered()` sets nocked. A watchdog-forced nock fires without `PlayBowFire` (layer not in `BowAim_State` — the trigger would latch). `Update`: stop the aim on any interrupt condition, nock watchdog. `StopAim` in `TryReceiveHit` (NotBlocked), `HandleVisualsRefreshed`, `OnDisable` -- flow
- [x] `AnimationEventReceiver.cs` + new `Scripts/Combat/SMB_BowAimState.cs` (`.meta` with `MonoImporter`) -- `OnStateEnter` → `NotifyBowAimEntered()` -- nock signal
- [x] `Humanoid_Template.controller` (editor code) -- params `IsAiming` (bool), `BowFire` (trigger). Attack-layer states `BowDrawArrow_State` (`Draw arrow`), `BowAim_State` (`Aim Overdraw`, + `SMB_BowAimState`), `BowFire_State` (`Fire arrow`). `CombatIdle → BowDrawArrow` (IsAiming); `BowDrawArrow → BowAim` (exit time); `BowAim → BowFire` (BowFire); `BowFire → BowDrawArrow` (exit time, IsAiming); each of the three → `CombatIdle` (IsAiming false, ~0.15 s) -- upper body only
- [x] `Standing Aim Overdraw.fbx` importer -- `loopTime` true -- loop the hold
- [x] `PlayerController.cs` -- free-move branch while `IsAiming`: face camera yaw + `aimBodyYawOffset`, speed = walk (sneak still lower) -- strafing aim
- [x] `CameraController.cs` -- `[SerializeField] PlayerStateManager`, `CinemachineCamera`; `_aimBlend` toward `IsAiming` at `aimBlendSpeed`; lerp `FollowOffset` (after look-down math), lens FOV and sensitivity; wire on the Player prefab -- zoom
- [x] `Tests/EditMode/BowAimGateTests.cs` (new) + `WeaponSwitchGateTests.cs` -- `EvaluateCanAim` per gate, switch blocked while aiming
- [x] Docs: `Scripts/Combat/CLAUDE.md` (Ranged section: aim flow), `Controllers/CLAUDE.md` (params / states), `Animations/Combat/CLAUDE.md` (Bow folder clips), `Scripts/Player/CLAUDE.md` (gate table + aim camera)

**Acceptance Criteria:**
- Given the bow drawn, when RMB is held while walking, then the legs walk, the upper body plays draw-arrow → aim, the character faces the camera direction, and the view zooms over the shoulder with slower mouse look.
- Given aiming and nocked, when LMB is clicked twice quickly, then only one arrow is spent and the second click is ignored until the aim pose returns.
- Given an NPC using `Humanoid_Template`, when it fights, then nothing changes (no aim states, no errors).

## Spec Change Log

- **2026-10-29 review (patch, no loopback).** Acceptance audit: the task said "switch blocked while aiming" but the frozen
  rule says R *ends* the aim — `OnDrawWeaponStarted` now calls `StopAim()` before `CanSwitchWeapon()` (gate keeps
  `isAiming` as a safety net). `bowNockTimeout` 2 → 2.5 s (must exceed the fire + re-nock cycle). Review patches: forced
  nock never sets `BowFire`; `EvaluateCanAim` excludes a still-raised block (and Block release lowers it after a stop);
  aim camera blends on unscaled time (pause menus) and leaves the lens alone at rest; Sprint while aiming no longer drops
  sneak; bridge null checks explicit. KEEP: nock = `BowAim_State` enter, aim stop centralised in `UpdateBowAim`.

## Design Notes

Nock = `BowAim_State` OnStateEnter (fires at the start of the `BowDrawArrow → BowAim` blend, near the end of the clip). That's the only "ready" signal, with a timeout fallback. The arrow spawns on the click, the same frame `Fire arrow` starts (no release event). The camera blend is applied after the existing look-down offset so both compose. Lock-on keeps its own facing; aiming doesn't change it.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- all green, incl. `BowAimGateTests`, `WeaponSwitchGateTests`
- `read_console` (errors) after recompile -- none

**Manual checks (if no CLI):**
- Play: draw bow, aim, fire 3 arrows, spam-click, release mid-draw, dodge while aiming, aim with 0 arrows, sword RMB still blocks.

## Suggested Review Order

**Aim / fire flow (entry point)**

- Bow attack: no hip-fire, needs aim + nock; forced nock skips the release trigger
  [`PlayerCombat.cs:309`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L309)

- Right click routes to aim when a bow is equipped — the bow can't block
  [`PlayerCombat.cs:256`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L256)

- Start / stop pair; stop is idempotent and called from every interrupt
  [`PlayerCombat.cs:581`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L581)

- Single per-frame interrupt check plus nock watchdog
  [`PlayerCombat.cs:601`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L601)

- Nock signal from the aim state; stale enters ignored
  [`PlayerCombat.cs:746`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L746)

- Existing gates unchanged, now report success so only real shots replay the cycle
  [`PlayerCombat.cs:536`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L536)

- R and landed hits end the aim explicitly
  [`PlayerCombat.cs:246`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L246)
  [`PlayerCombat.cs:816`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L816)

**State & animator**

- Aim gate excludes block / dodge / air / switch; switch gate excludes aim
  [`PlayerStateManager.cs:299`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L299)
  [`PlayerStateManager.cs:287`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L287)

- IsAiming owner; ending clears a pending BowFire
  [`PlayerStateManager.cs:186`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L186)

- Bridge-only parameter writes
  [`HumanoidAnimationBridge.cs:111`](../../Assets/_Game/Scripts/Core/Animations/HumanoidAnimationBridge.cs#L111)

- Aim-state SMB, silent on NPCs
  [`SMB_BowAimState.cs:14`](../../Assets/_Game/Scripts/Combat/SMB_BowAimState.cs#L14)

- Attack-layer bow states (draw arrow → aim loop → fire)
  [`Humanoid_Template.controller:887`](../../Assets/_Game/Art/Characters/Humanoids/Controllers/Humanoid_Template.controller#L887)

**Camera & movement**

- Unscaled blend; lens untouched at rest
  [`CameraController.cs:194`](../../Assets/_Game/Scripts/Player/CameraController.cs#L194)

- Aim offset composed on top of the look-down offset
  [`CameraController.cs:177`](../../Assets/_Game/Scripts/Player/CameraController.cs#L177)

- Sensitivity scaled by the blend
  [`CameraController.cs:113`](../../Assets/_Game/Scripts/Player/CameraController.cs#L113)

- Body faces camera yaw + offset while aiming, no sprint
  [`PlayerController.cs:160`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L160)

- Shift while aiming no longer drops sneak
  [`PlayerSneak.cs:86`](../../Assets/_Game/Scripts/Player/PlayerSneak.cs#L86)

**Peripherals**

- Tuning values
  [`CombatConfigSO.cs:64`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L64)

- Gate tests
  [`BowAimGateTests.cs:7`](../../Assets/Tests/EditMode/BowAimGateTests.cs#L7)
