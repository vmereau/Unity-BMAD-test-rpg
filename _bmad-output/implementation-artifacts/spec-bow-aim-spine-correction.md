---
title: 'Bow aim correction: spine yaw + pitch toward the crosshair'
type: 'feature'
created: '2026-10-29'
status: 'done'
baseline_commit: '9227f31a8b8af16bc3519a9c44c7bb103f03a983'
context:
  - '{project-root}/.claude/rules/attack-pipeline.md'
  - '{project-root}/Assets/_Game/Scripts/Combat/CLAUDE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-bow-aim-draw-fire.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Mixamo archer clips are authored side-on (hips +90°, spine −90°). On the Attack layer the hips come from locomotion, so the torso and bow point ~90° to the left. A fixed body yaw offset only fixes idle, because walking clips change the hips' facing. The aim pose also never follows the camera pitch.

**Approach:** A player-only post-animation pass (LateUpdate, while `IsAiming`, blended in and out on unscaled time) rotates Spine / Chest / UpperChest so a chest-locked "arrow line" points at the crosshair aim point (yaw + pitch). The line is calibrated once from the `Aim Overdraw` pose (hands relative to UpperChest are clip-only). `aimBodyYawOffset` becomes a stance yaw (default 45°) so the side-on archer look stays.

## Boundaries & Constraints

**Always:**
- Bone writes only, after the Animator, on the Player. No Animator parameter writes. NPCs untouched.
- The aim point comes from `PlayerCombat` (same raycast as `FireArrow`), never a second raycast implementation.
- ≈ ⅓ of the delta per bone, per-bone clamp, all tuning in `CombatConfigSO`. The head rides the chest (no extra head look).
- Pure math (yaw/pitch delta, split, clamp) in a static helper covered by EditMode tests. `GameLog` only.
- Revert the uncommitted `keepOriginalOrientation` experiment on `Standing Aim Overdraw` / `Standing Fire Arrow` `.fbx.meta` (back to the committed baseline) before calibrating.

**Ask First:** Adding a package (Animation Rigging). Changing the bow clips, masks or controller. Head look-at.

**Never:** Changing arrow trajectory / fire logic. Hip-fire, crosshair / HUD changes. NPC archers.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Idle aim | Aiming, standing | Bow + chest on crosshair within a few degrees | N/A |
| Moving aim | Walking / strafing any direction | Same, despite locomotion hip turn | N/A |
| Steep pitch | Camera pitch ±60° | Bow follows pitch up to the clamp | N/A |
| Draw / fire clip | `Draw arrow` / `Fire arrow` playing | Same correction (chest-locked line), no swing | N/A |
| Close target | Aim point < `aimCorrectionMinDistance` | Direction uses the ray point at the min distance | N/A |
| Release / interrupt | `IsAiming` → false | Correction blends to 0, no pop | N/A |
| Missing bones / ref | Humanoid bone null or reference ≈ zero | Component disabled | `GameLog.Error` once |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `GetAimPoint(maxDistance)` (private, crosshair ray), `EquippedBow`, `FireArrow`
- `Assets/_Game/Scripts/Player/CameraController.cs` -- `_aimBlend` (MoveTowards, `aimBlendSpeed`, unscaled) pattern to mirror
- `Assets/_Game/Scripts/Player/PlayerController.cs:160` -- aim facing: camera yaw + `aimBodyYawOffset`
- `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs`, `CombatConfigSO.cs` ("Bow Aim" header) -- tuning
- `Art/Characters/Humanoids/models/test base hero/rigged test hero with textures.fbx` -- Spine=`mixamorig:Spine`, Chest=`Spine1`, UpperChest=`Spine2`
- `Prefabs/Player/Player.prefab` -- Animator on root, `Character` child (Humanoid)

## Tasks & Acceptance

**Execution:**
- [x] Bow `.fbx.meta` files -- `git checkout` both `keepOriginalOrientation` edits -- known baseline before calibration
- [x] `PlayerCombat.cs` -- public `bool TryGetAimPoint(out Vector3 point)`: bow equipped and camera present → `GetAimPoint(bow.maxAimDistance)`; else false -- single source
- [x] `CombatConfigSO.cs` -- "Bow Aim Correction": `aimCorrectionMaxAnglePerBone` 35, `aimCorrectionMinDistance` 2 m, `aimCorrectionEnabled` true -- tuning
- [x] `PlayerConfigSO.cs` + `PlayerController.cs` -- rename `aimBodyYawOffset` → `aimStanceYaw` (`FormerlySerializedAs`), default 45; set `PlayerConfig.asset` to 45 -- stance
- [x] `Scripts/Combat/AimCorrectionMath.cs` (new, static, `.meta` with `MonoImporter`) -- `YawPitchDelta(current, desired)` (signed yaw about up, pitch = elevation difference), `BoneStep(remaining, bonesLeft, maxPerBone)` (share of what's left, clamped), `YawReachFactor(yaw, full, zero)` (fade when the target is behind) -- testable math
- [x] `Scripts/Combat/BowAimCorrection.cs` (new, `.meta`) -- on Player root, `[DefaultExecutionOrder(200)]` (after CinemachineBrain at 100); refs Animator, `PlayerStateManager`, `PlayerCombat`, `CombatConfigSO`; serialized `_arrowLineLocal` (UpperChest-local direction) + `_arrowOriginLocal`; LateUpdate: blend weight toward `IsAiming` at `aimBlendSpeed` (unscaled); restore the animated pose if the Animator didn't rewrite it; weight 0 → return; hold back (1 − weight) of the delta (linear blend); measure world line from UpperChest, delta to aim point, rotate Spine→Chest→UpperChest in world space (yaw about up, then pitch about the aim's right axis), then one residual pass on UpperChest within its clamp; hold last aim point when `TryGetAimPoint` fails -- correction
- [x] Player prefab -- add + wire `BowAimCorrection`; calibrate `_arrowLineLocal` / `_arrowOriginLocal` via editor code sampling `Aim Overdraw` mid-clip on the rig (right hand → left hand, expressed in UpperChest space) -- calibration
- [x] `Tests/EditMode/AimCorrectionMathTests.cs` (new) -- yaw sign / wrap, pitch up/down, split sums to total under clamp, clamp caps each bone, near-vertical / zero inputs, yaw reach fade -- coverage
- [x] Docs: `Scripts/Combat/CLAUDE.md` (Ranged: correction + calibration), `Scripts/Player/CLAUDE.md` (gate table: `aimStanceYaw`), `Prefabs/Player/CLAUDE.md` (component list) -- docs

**Acceptance Criteria:**
- Given the bow aimed, when standing, walking or strafing in any direction, then the bow line and chest point at the crosshair within a few degrees.
- Given the camera pitched steeply up or down while aiming, then the bow follows the pitch (up to the clamps).
- Given the aim released, then the spine returns to the animated pose over ~0.12 s with no pop, and arrows still fly to the crosshair.
- Given an NPC on `Humanoid_Template`, then nothing changes.

## Spec Change Log

- **2026-10-29 review (patch, no loopback).** Three reviewers found no intent or spec gaps. Patches applied:
  - Execution order 100 → 200: CinemachineBrain also runs at 100, so the camera pose could be a frame stale.
  - Animated / written local rotations are cached, so corrections never stack on frames the Animator skips (pause,
    `OnDisable`).
  - Explicit UpperChest residual pass.
  - Linear blend: hold back (1 − w) × delta, instead of w per bone, which front-loaded the blend.
  - Yaw-reach fade from 120° to 160°: the ±180° flip when aiming starts with the body turned away.
  - Normalized degeneracy thresholds.
  - Lazy `Camera.main` with a warning; `TryGetAimPoint` returns false without a camera.
  - Warning when fewer than 3 spine bones are mapped.
  - Null-bones guard after a re-enable.

  Rig check after the patches: residual ≤ 0.5° at 2 m and 0.00° at 30 m over hips −15..−90° and pitch ±60°.

  KEEP: chest-locked calibrated line (user-approved, active through Draw / Fire); re-measure per bone. Rejected: gating on
  the nock state, hitch clamp. Deferred: calibration drift validator.

## Design Notes

Directions compose the same whatever the pivot, so rotating three bones by ⅓ of the delta (world space, parent first) turns the chest-locked line by the full delta. The hand offset moves slightly with the spine, so a small residual stays; the UpperChest residual pass fixes it. The line is stored chest-local because the bow clips alone set the hands relative to the chest. A live hand measurement would swing the spine during `Draw arrow` / `Fire arrow`.

Pitch about the line's horizontal right axis after the yaw step (`Cross(up, flat)`), not the bone's local axis, so pitch stays correct whatever the hips do.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- all green incl. `AimCorrectionMathTests`
- `read_console` (errors) after recompile -- none

**Manual checks (if no CLI):**
- Play: aim idle / walk 8 directions / pitch ±60°, fire, release; compare bow line to crosshair (gizmo ray from the calibrated line while selected).

## Suggested Review Order

**Correction loop (entry point)**

- Per-frame flow: restore → blend → aim point → per-bone split → residual pass
  [`BowAimCorrection.cs:95`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L95)

- Linear blend by holding back (1 − w) of the delta; residual pass for the moving hand
  [`BowAimCorrection.cs:127`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L127)

- Yaw about world up, then pitch about the line's horizontal right axis
  [`BowAimCorrection.cs:162`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L162)

- Runs after CinemachineBrain (100) so the crosshair ray is this frame's
  [`BowAimCorrection.cs:13`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L13)

**Robustness**

- Never stacks when the Animator skipped a frame (pause / culled)
  [`BowAimCorrection.cs:141`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L141)

- Close walls pushed out along the crosshair ray so the spine doesn't whip
  [`BowAimCorrection.cs:176`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L176)

- Disable restores the animated pose and resets the blend
  [`BowAimCorrection.cs:88`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L88)

**Math**

- Normalized yaw / pitch delta; yaw undefined near vertical → 0
  [`AimCorrectionMath.cs:19`](../../Assets/_Game/Scripts/Combat/AimCorrectionMath.cs#L19)

- Per-bone share of what's left, clamped per axis
  [`AimCorrectionMath.cs:41`](../../Assets/_Game/Scripts/Combat/AimCorrectionMath.cs#L41)

- Fade when the target is behind the line (±180° sign flip)
  [`AimCorrectionMath.cs:54`](../../Assets/_Game/Scripts/Combat/AimCorrectionMath.cs#L54)

**Aim source & stance**

- Single aim-point source shared with the shot; false without bow or camera
  [`PlayerCombat.cs:650`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L650)

- Stance yaw (45°) replaces the old full-body offset
  [`PlayerController.cs:165`](../../Assets/_Game/Scripts/Player/PlayerController.cs#L165)
  [`PlayerConfigSO.cs:21`](../../Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs#L21)

**Peripherals**

- Calibrated chest-local arrow line (values on the Player prefab)
  [`BowAimCorrection.cs:32`](../../Assets/_Game/Scripts/Combat/BowAimCorrection.cs#L32)

- Tuning
  [`CombatConfigSO.cs:77`](../../Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs#L77)

- Math tests
  [`AimCorrectionMathTests.cs:8`](../../Assets/Tests/EditMode/AimCorrectionMathTests.cs#L8)

- Docs: correction, calibration recipe, execution order
  [`CLAUDE.md:126`](../../Assets/_Game/Scripts/Combat/CLAUDE.md#L126)
