# Camera — CameraController + Cinemachine 3.x OTS rig

> Read when touching `CameraController`, `CameraRigMath` or the `CinemachineCamera` setup on the Player prefab.

---

## Cinemachine 3.x — Over-the-Shoulder Setup

The project uses `com.unity.cinemachine` **6.6.0** (`Packages/manifest.json`) — the Cinemachine 3.x API, namespace `Unity.Cinemachine`.
Assembly reference in `Game.asmdef`: `"Unity.Cinemachine"`.
Components are sibling MonoBehaviours on the `CinemachineCamera` GameObject — not nested pipeline stages.

Working OTS configuration (spec-third-person-camera-spring-arm):
- **Body:** `CinemachineThirdPersonFollow` — a real orbit + spring arm around `CameraTarget` (pitch+yaw):
  shoulder = target + yaw-heading × `ShoulderOffset (0.3, 0.5, 0)`, camera = shoulder − targetForward ×
  `CameraDistance 4.5`, `CameraSide 1`, `VerticalArmLength 0`, `Damping (0.1, 0.25, 0.15)`.
  - **Collision = its `AvoidObstacles` only:** `CollisionFilter` = **Default + Interactable** (257 — closed doors and
    containers have solid Interactable colliders), `IgnoreTag Player`, radius 0.2, `DampingIntoCollision 0` (instant
    pull-in, never clips), `DampingFromCollision 0.5` (slow ease-out). Characters, Harvestable, Water never move the
    camera. Small props still block, but only when they sit on the pivot→camera arm (by design).
  - The orbit puts the camera metres behind the player, so anything that measures a distance along the camera ray
    must measure it from the player (`InteractionSystem._reachOrigin`, `Scripts/World/CLAUDE.md`).
  - **Never add `CinemachineDeoccluder` / `CinemachineDecollider`.** The Deoccluder zoomed in on any one-frame
    occluder (NPCs, pickups); the Decollider's Terrain Resolution casts *down from 10 m above the camera* and lifts it
    onto any Default collider (wall tops, roofs) — the old "camera slides upward near walls" bug.
  - Don't fake an orbit with pitch-driven `FollowOffset` changes (the old `CinemachineFollow` +
    `LockToTargetWithWorldUp` setup did, with `_lookDownHeightRise` / `_lookDownZoomIn`) — it felt like the camera
    sliding away from the player indoors.
- **`CameraController` rig writes** (every `LateUpdate`, pure rules in `CameraRigMath` + `CameraRigMathTests`):
  bow aim lerps shoulder / distance to `CombatConfigSO.aimFollowOffset` (x, y = shoulder, −z = distance, floored at
  `MinArmDistance`); then the distance × `RigDistanceScale` (look-up shortening: 1 at level / looking down →
  `_lookUpDistanceScale` 0.5 at `_pitchMin`, faded out by the aim blend so the short aim arm isn't halved again).
  While swimming the upward pitch limit eases to `_swimPitchMin` (−5° ≈ 0.25 m above the surface; Water isn't a
  collider, so this limit is the only thing keeping the camera dry) at `_swimPitchEaseSpeed`, pushing `_pitch` up with
  it every frame (menus open too). Base shoulder / distance are read from the component in `Awake` — tune them on the
  prefab; play-mode edits on the component are overwritten each frame.
- **Teleports:** `CameraController.DetectWarp` calls `CinemachineCore.OnTargetObjectWarped` when `CameraTarget` jumps
  more than `_warpDistance` (3 m) in a frame — otherwise the rig damping sweeps the camera across the map after a
  load / spawn / story teleport. Teleport code doesn't need to notify the camera.
- **Aim:** `CinemachineRotateWithFollowTarget` — inherits `CameraTarget` world rotation directly
  (`CinemachineSameAsFollowTarget` is **deprecated** in the 3.x API — do not use it)
- **Do NOT use** `CinemachineRotationComposer` for OTS — it aim-corrects toward a world point
  and cancels the vertical pitch driven by `CameraController`
- **Do NOT add** `CinemachineInputAxisController` or `CinemachinePanTilt` — dual input causes
  rotation fighting with `CameraController`

`CameraController.cs` owns all mouse input and writes to `CameraTarget.rotation`.
Cinemachine reads `CameraTarget` passively — it never takes direct input.

---

## Angle math (CameraController)

- Normalize accumulated yaw with `_yaw %= 360f`; read pitch from `eulerAngles.x` through `Mathf.DeltaAngle(0f, x)` before clamping (both already in `CameraController`).

---

## Cursor Lock in CameraController

Escape unlocks via `_input.UI.Cancel.WasPressedThisFrame()`, left-click re-locks via
`_input.UI.Click.WasPressedThisFrame()` — the Player map has no Cancel action, so `CameraController` enables /
disables the **UI** map alongside the Player map. Full action list: `Assets/_Game/CLAUDE.md`.
