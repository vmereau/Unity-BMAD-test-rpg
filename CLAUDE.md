# CLAUDE.md — Unity-BMAD-test-rpg

> Read this at the start of every session. It orients you to the project and records
> patterns learned during development. Coding rules live in `_bmad-output/project-context.md`.

---

## Project Identity

- **Engine:** Unity 6000.3.10f1 (Unity 6.3 LTS)
- **Render Pipeline:** URP 17.x (`Assets/Settings/PC_RPAsset`, `PC_Renderer`)
- **Input:** Unity Input System — generated class `InputSystem_Actions`; legacy input disabled
- **Platform:** PC Windows x64 → Steam distribution
- **Game type:** 3D RPG, third-person over-the-shoulder camera

---

## Key File Locations

| What | Path |
|------|------|
| **Authoritative coding rules (52 rules)** | `_bmad-output/project-context.md` |
| **Game architecture doc** | `_bmad-output/planning-artifacts/` |
| **Sprint status** | `_bmad-output/implementation-artifacts/sprint-status.yaml` |
| **Story files** | `_bmad-output/implementation-artifacts/*.md` |
| **All game source code** | `Assets/_Game/` |
| **Git conventions** | `.claude/rules/git-conventions.md` |

> **Never treat `_bmad/` or `_bmad-output/` as game source code.** They are BMAD
> workflow artifacts. Always exclude them from code reviews and source analysis.

---

## Before Writing Any Game Code

1. Read `_bmad-output/project-context.md` — all 52 rules are mandatory
2. Check `_bmad-output/implementation-artifacts/sprint-status.yaml` for current state
3. If a story file exists for the task, read it fully before implementing

---

## BMAD Workflow Commands

| Command | When to use |
|---------|-------------|
| `/bmad:bmgd:workflows:sprint-status` | See what's in-progress / what's next |
| `/bmad:bmgd:workflows:dev-story` | Implement the current story |
| `/bmad:bmgd:workflows:code-review` | Adversarial review after a story is complete |
| `/bmad:bmgd:workflows:create-story` | Generate next story file from epics |
| `/perso:commit` | Stage, commit, and push changes |
| `/perso:wrap-up` | End of session — update CLAUDE.md with learned patterns |

---

## Learned Patterns & Gotchas

### Unity Input System — Action Map Layout

The project's `InputSystem_Actions` action maps:

- **Player map:** Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint — **no Cancel action**
- **UI map:** Navigate, Submit, **Cancel** (Escape), **Click** (left mouse), Point, RightClick, MiddleClick, ScrollWheel

Consequences for cursor lock handling in `CameraController`:
- Escape unlock → `_input.UI.Cancel.WasPressedThisFrame()`
- Left-click re-lock → `_input.UI.Click.WasPressedThisFrame()`
- Must call `_input.UI.Enable()` / `_input.UI.Disable()` alongside the Player map

### Unity Lifecycle Gotcha: OnDisable Before OnEnable

Unity's first-activation order is `Awake → OnEnable → Start`.
If `Awake()` sets `enabled = false`, Unity calls `OnDisable()` **before** `OnEnable()` has run.
Any field initialized in `OnEnable()` (e.g. `_input`) will be `null` in `OnDisable()`.

**Required pattern whenever `_input` is initialized in `OnEnable()`:**

```csharp
private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.UI.Disable();
    _input.Player.Disable();
    _input.Dispose();
}
```

### CharacterController.velocity Includes Y Component

`CharacterController.velocity.magnitude` is **never 0 when grounded** because `PlayerController` constantly applies `GROUNDED_VELOCITY = -2f` to keep the character snapped to the ground. Effects:
- At rest: magnitude ≈ 2.0f (not 0) — blend tree never fully reaches idle threshold
- At `walkSpeed=3f`: magnitude ≈ 3.6f (not 3f) — blend starts crossing walk→run threshold early
- At `runSpeed=6f`: magnitude ≈ 6.3f — may never reach the run threshold if set above 6.3f

**Always use horizontal speed for animation blend trees:**

```csharp
// In PlayerAnimator.Update() — correct pattern
Vector3 horizontalVelocity = new Vector3(
    _characterController.velocity.x, 0f, _characterController.velocity.z);
float speed = horizontalVelocity.magnitude;
```

### Float Accumulation & Euler Angle Quirks

- **Unbounded yaw:** accumulated `_yaw` must be normalized: `_yaw %= 360f;` — without this,
  float precision degrades after extended play (thousands of degrees → sub-degree jitter).
- **Pitch from `eulerAngles`:** Unity returns `eulerAngles.x` in [0, 360]. A −10° pitch is
  stored as 350°. Use `Mathf.DeltaAngle(0f, eulerAngles.x)` to get a proper signed value
  before clamping, or the pitch will snap to `_pitchMax` on the first frame.

### Cinemachine 3.x — Over-the-Shoulder Setup (Story 1.2)

The project ships with **Cinemachine 3.x** (`CinemachineCamera` component, not `CinemachineVirtualCamera`).

Working OTS configuration:
- **Body:** `CinemachineFollow` with offset `(0.5, 0.3, −3.5)`
- **Aim:** `CinemachineSameAsFollowTarget` — inherits `CameraTarget` world rotation directly
- **Do NOT use** `CinemachineRotationComposer` for OTS — it aim-corrects toward a world point
  and cancels the vertical pitch driven by `CameraController`
- **Do NOT add** `CinemachineInputAxisController` or `CinemachinePanTilt` — dual input causes
  rotation fighting with `CameraController`

`CameraController.cs` owns all mouse input and writes to `CameraTarget.rotation`.
Cinemachine reads `CameraTarget` passively — it never takes direct input.

### Player Prefab Structure

```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController  (Height: 1.8, Center Y: 1.0)
├── Animator             (Apply Root Motion: OFF; Controller: PlayerAnimatorController)
├── PlayerController.cs
├── PlayerAnimator.cs
├── CameraTarget         (child, local Y = 1.6 — pure Transform pivot, no components)
└── Character            (child — nested Mixamo FBX prefab, Humanoid rig)
```

- `CameraController` is wired **in `TestScene.unity`** as a scene component — **not** in the prefab (known drift from intended architecture; action item exists to move it)
- Cinemachine `Follow` and `LookAt` → both point to `CameraTarget`
- No Rigidbody on player — `CharacterController` only
- Camera-relative movement uses `Camera.main` cached in `Awake()` as `_mainCamera`
- `PlayerAnimator` reads `CharacterController.velocity` passively — never writes to movement state

---

## Code Review Checklist (Patterns Found in Practice)

High-signal issues to always check in Unity MonoBehaviour reviews:

| Severity | Pattern |
|----------|---------|
| HIGH | `OnDisable` calls fields initialized in `OnEnable` without null guard |
| HIGH | `enabled = false` set in `Awake` without OnDisable null guard |
| MEDIUM | `Keyboard.current` / `Mouse.current` used instead of `InputSystem_Actions` action map |
| MEDIUM | Accumulated angle (`_yaw`, `_angle`) without `% 360f` modulo |
| MEDIUM | `eulerAngles` used as signed source without `Mathf.DeltaAngle` conversion |
| MEDIUM | `GetComponent` or `Camera.main` called in `Update` instead of cached in `Awake` |
| MEDIUM | `CharacterController.velocity.magnitude` used for animation speed (Y component inflates value; use `new Vector3(v.x,0,v.z).magnitude`) |
| MEDIUM | `.meta` file manually created and missing `MonoImporter` block — Unity may regenerate with new GUID on reimport, breaking prefab script references |
| LOW | `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` used directly (use `GameLog`) |
| LOW | Magic numbers in gameplay logic (use `[SerializeField]` or config SO) |
| LOW | Story File List missing Unity Editor-generated assets (FBX, AnimatorController, .meta files) — always audit art asset directories when story covers animation/import work |
| HIGH | Auto-generated files (e.g. `InputSystem_Actions.cs`) left in `Assets/` root after adding a named asmdef — named assemblies can't see `Assembly-CSharp`; move them inside the asmdef folder |
