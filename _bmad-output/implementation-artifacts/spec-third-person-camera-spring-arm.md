---
title: 'Third-person camera: orbiting spring arm with environment-only collision'
type: 'bugfix'
created: '2026-11-15'
status: 'done'
baseline_commit: 'bfa1127'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The OTS camera feels jumpy: it snaps in on NPCs and small props, slides upward after standing near walls, and indoors looking up/down slides the camera around instead of orbiting. Causes: `CinemachineDeoccluder` reacts to any one-frame occluder (incl. Interactable layer), `CinemachineDecollider` Terrain Resolution pushes the camera on top of any Default-layer collider within 10 m above it, and `CinemachineFollow` (`LockToTargetWithWorldUp`) does not orbit, so `CameraController` fakes it with large pitch-driven offsets (`_lookDownHeightRise 3`, `_lookDownZoomIn 3`).

**Approach:** Replace Follow + Deoccluder + Decollider with `CinemachineThirdPersonFollow` (true orbit around a pivot, built-in sphere-cast spring arm). Only the Default layer blocks; pull-in is instant, ease-out is slow. `CameraController` drops the fake offsets, shortens the arm when looking up, eases look-up to a limit while swimming, and blends bow aim onto shoulder/distance.

## Boundaries & Constraints

**Always:** `CameraController` stays the only input owner and writes only `CameraTarget.rotation` + rig parameters. Resting pose at level pitch matches today's (pivot 1.6 m + shoulder (0.3, 0.5), distance 4.5). Pure math lives in a static class with EditMode tests. Collision mask = Default + Interactable (closed doors / containers; user decision 2026-11-15), `IgnoreTag` Player. Small props keep blocking (user decision).

**Ask First:** Adding a new physics layer; changing `PlayerController` / `InteractionSystem` / `BowAimCorrection` logic.

**Never:** Player model fading, a `CameraIgnore` layer, camera-shake or lock-on behaviour changes, new input actions.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Level look | pitch 0 | arm distance = base (4.5) | N/A |
| Full look-up | pitch = `_pitchMin` | distance = base × `_lookUpDistanceScale` (0.5), linear in between | N/A |
| Look down | pitch > 0 | distance = base (orbit raises camera naturally) | N/A |
| Swimming look-up | swimming, pitch −40 | pitch eases (not snaps) to ≥ `_swimPitchMin` (−5); input can't go below it | N/A |
| Bow aim | aim blend 0→1 | shoulder/distance lerp to `aimFollowOffset` (x,y → shoulder, −z → distance); FOV as today | `_combatConfig` null → no aim offset |
| Obstacle | wall between pivot and camera | camera snaps in instantly, eases out ~0.5 s when clear | N/A |
| NPC in line | Characters collider between | no camera movement | N/A |
| Floor pickup | looking down at an item at the feet | item focused (reach measured from the player, not the camera) | `_reachOrigin` null → camera origin + warning |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Player/CameraController.cs` -- input → `CameraTarget` rotation; today's fake pitch offsets + aim blend on `CinemachineFollow`
- `Assets/_Game/Prefabs/Player/Player.prefab` -- `Virtual Camera` GO: CinemachineCamera, Follow, RotateWithFollowTarget, Deoccluder, Decollider; CameraController serialized fields
- `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- `aimFollowOffset` (0.6, 0.3, −1.8) tooltip references CinemachineFollow
- `Assets/_Game/Scripts/Combat/BowAimCorrection.cs` -- reads camera pose after brain (order 200); unchanged
- `Assets/_Game/Scripts/World/InteractionSystem.cs`, `InteractionFocus.cs` -- screen-centre ray from `Camera.main`; reach now measured from `_reachOrigin` (CameraTarget)
- `Assets/_Game/Scripts/Player/CLAUDE.md`, `Assets/_Game/Prefabs/Player/CLAUDE.md` -- document the camera rig (current "Do NOT use ThirdPersonFollow" note is wrong)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/Player/CameraRigMath.cs` -- new pure static `Game.Player.CameraRigMath`: `LookUpDistanceScale(pitch, pitchMin, minScale)`, `EffectivePitchMin(pitchMin, swimming, swimPitchMin)`, `BlendRig(baseShoulder, baseDistance, aimOffset, blend, out shoulder, out distance)` -- testable rules
- [x] `Assets/Tests/EditMode/CameraRigMathTests.cs` -- cover every matrix row except collision
- [x] `Assets/_Game/Scripts/Player/CameraController.cs` -- replace `_cinemachineFollow` with `CinemachineThirdPersonFollow _thirdPersonFollow`; cache base `ShoulderOffset` / `CameraDistance` in Awake; remove `_lookDownHeightRise`, `_lookDownZoomIn`, CameraTarget height reset; add `_lookUpDistanceScale` (0.5), `_swimPitchMin` (−10), `_swimPitchEaseSpeed` (deg/s, 90); apply rig every LateUpdate (also while cursor unlocked, as today)
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` (via MCP / PrefabUtility, not YAML) -- remove CinemachineFollow, Deoccluder, Decollider; add ThirdPersonFollow: ShoulderOffset (0.3, 0.5, 0), VerticalArmLength 0, CameraSide 1, CameraDistance 4.5, Damping (0.1, 0.25, 0.15), AvoidObstacles on, filter Default, IgnoreTag Player, radius 0.2, into 0, from 0.5; wire `_thirdPersonFollow`
- [x] `Assets/_Game/ScriptableObjects/Config/CombatConfigSO.cs` -- tooltip: x,y = shoulder offset, −z = arm distance
- [x] `Assets/_Game/Scripts/Player/CLAUDE.md`, `Assets/_Game/Prefabs/Player/CLAUDE.md` -- rewrite the Cinemachine OTS section (rig, collision rules, why Decollider/Deoccluder are banned)

**Acceptance Criteria:**
- Given the player stands with their back to a wall then walks away, when the camera clears the wall, then it eases back out horizontally and never rises above its orbit position.
- Given an NPC or pickup passes between player and camera, when it crosses, then the camera distance doesn't change.
- Given the player indoors looks down, when pitch increases, then the camera orbits up over the shoulder and the player stays framed.
- Given the project compiles, when EditMode tests run, then all tests pass with no new console errors.

## Spec Change Log

- **2026-11-15, review loop 1 (human-approved).** Findings: (1) with a real orbit the camera sits 4.5 m behind the player, so the camera-origin 6 m interaction range could never reach floor items; (2) `Door Base` has a solid collider on Interactable, so excluding Interactable let the camera pass through closed doors; (3) a -10 deg swim limit left the camera ~8 cm underwater; (4) teleports swept the damped rig across the map; (5) aim distance compounded with look-up shortening. Amended: collision mask Default + Interactable; `InteractionSystem` reach starts at the ray point closest to `CameraTarget` (`interactionRange` 3); `_swimPitchMin` -5, ease 180 deg/s, pitch pushed with the floor every frame; `DetectWarp` -> `CinemachineCore.OnTargetObjectWarped` above `_warpDistance` 3 m; `RigDistanceScale` fades look-up shortening by aim blend, `MinArmDistance` 0.5. KEEP: ThirdPersonFollow rig, no Deoccluder / Decollider, pure `CameraRigMath` + tests.

## Design Notes

ThirdPersonFollow rig: root = target pos; shoulder = root + yaw-heading × ShoulderOffset; hand = shoulder + targetRot × up × VerticalArmLength; camera = hand − targetForward × distance, with sphere-cast collision root→hand→camera. Full pitch+yaw on `CameraTarget` therefore orbits. `RotateWithFollowTarget` stays as rotation control.

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: all pass
- MCP `read_console` -- expected: no compile errors / missing-reference warnings

**Manual checks (if no CLI):**
- Play mode: back against a wall then walk off, walk past NPCs, look up/down inside a tent/building, swim and look up, aim the bow.

## Suggested Review Order

**Camera rig (orbit + spring arm)**

- Entry point: the Virtual Camera now runs ThirdPersonFollow with built-in collision (Default + Interactable).
  [`Player.prefab:1451`](../../Assets/_Game/Prefabs/Player/Player.prefab#L1451)

- Per-frame order: warp check, aim blend, swim pitch floor, rotation, then rig write.
  [`CameraController.cs:105`](../../Assets/_Game/Scripts/Player/CameraController.cs#L105)

- Rig write: aim blend then look-up shortening, faded by aim.
  [`CameraController.cs:179`](../../Assets/_Game/Scripts/Player/CameraController.cs#L179)

- Teleport guard: big CameraTarget jumps reset Cinemachine damping.
  [`CameraController.cs:157`](../../Assets/_Game/Scripts/Player/CameraController.cs#L157)

- Swim limit eases and pushes the pitch every frame, menus open too.
  [`CameraController.cs:168`](../../Assets/_Game/Scripts/Player/CameraController.cs#L168)

**Pure rules**

- Distance scale and aim compounding guard.
  [`CameraRigMath.cs:28`](../../Assets/_Game/Scripts/Player/CameraRigMath.cs#L28)

- Eased pitch floor step.
  [`CameraRigMath.cs:39`](../../Assets/_Game/Scripts/Player/CameraRigMath.cs#L39)

- Aim offset mapping (x,y shoulder, -z distance) with minimum arm.
  [`CameraRigMath.cs:49`](../../Assets/_Game/Scripts/Player/CameraRigMath.cs#L49)

**Interaction reach (boundary crossing)**

- Scan ray advanced to the point nearest the player before the sphere cast.
  [`InteractionSystem.cs:142`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L142)

- Pure projection, clamped so it never starts behind the camera.
  [`InteractionFocus.cs:45`](../../Assets/_Game/Scripts/World/InteractionFocus.cs#L45)

**Peripherals**

- Rig documentation and banned components.
  [`CLAUDE.md:180`](../../Assets/_Game/Scripts/Player/CLAUDE.md#L180)

- Rig math tests.
  [`CameraRigMathTests.cs:8`](../../Assets/Tests/EditMode/CameraRigMathTests.cs#L8)

- Reach tests.
  [`InteractionSystemTests.cs:307`](../../Assets/Tests/EditMode/InteractionSystemTests.cs#L307)
