# CLAUDE.md — Unity-BMAD-test-rpg

> Read this at the start of every session. It orients you to the project and records
> patterns learned during development. Coding rules live in `_bmad-output/project-context.md`.

---

## Project Identity

- **Engine:** Unity 6000.3.10f1 (Unity 6.3 LTS)
- **Render Pipeline:** URP 17.x (`Assets/Settings/PC_RPAsset`, `PC_Renderer`)
- **Input:** Unity Input System — generated class `InputSystem_Actions` at `Assets/_Game/InputSystem_Actions.cs`; legacy input disabled
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
| **Game assembly definition** | `Assets/_Game/Game.asmdef` (refs: `Unity.InputSystem`) |
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

### Assembly Setup (As of Story 1.5)

`Assets/_Game/Game.asmdef` exists with:
- `"name": "Game"`, `"autoReferenced": true`
- `"references": ["Unity.InputSystem"]`

All scripts under `Assets/_Game/` compile into the **`Game` assembly** (not `Assembly-CSharp`).
`InputSystem_Actions.cs` was moved from `Assets/` root into `Assets/_Game/` so it compiles into `Game`.

**Consequence:** Any future auto-generated file placed at `Assets/` root will be in `Assembly-CSharp` and invisible to `Game` scripts — always move such files inside `Assets/_Game/`.

`Tests.EditMode.asmdef` explicitly references `"Game"` in its references array.

### Enemy Prefab Structure (Enemy_Grunt)

```
Enemy_Grunt.prefab  (Assets/_Game/Prefabs/Enemies/)
├── NavMeshAgent, EnemyBrain, PersistentID, EnemyHealth   ← all on ROOT
└── Visual  (child)
    └── MeshFilter, CapsuleCollider, MeshRenderer         ← collider is on CHILD
```

**Consequence for hit detection:** `Physics.OverlapSphereNonAlloc` returns the `CapsuleCollider` on `Visual`. Use `GetComponentInParent<EnemyHealth>()` — NOT `TryGetComponent` — to walk up to the root. `TryGetComponent` only looks at the collider's own GameObject and will always miss.

### Test Scaffolding — EnemyRespawner (Story 3.1)

`Assets/_Game/Scripts/Debug/EnemyRespawner.cs` (namespace `Game.DevTools`) is attached to `ProgressionSystem` in TestScene. It re-enables dead enemies after a configurable delay (default 5s). `EnemyHealth.OnEnable()` resets `IsDead` and `CurrentHealth` on reactivation. **This is test scaffolding — superseded by Story 4-5 (no-enemy-respawn design).**

`Game.DevTools` is the correct namespace for test/debug utilities. `Game.Debug` is **banned** — it shadows `UnityEngine.Debug` globally and breaks `Debug.Log`, `Debug.DrawLine`, etc. across the entire codebase.

### Core.unity Scene Stubs (Complete as of Story 1.5)

All 7 manager stub GameObjects are in `Assets/_Game/Scenes/Core.unity`:
`WorldStateManager`, `GameEventBus`, `SaveSystem`, `SceneLoader`, `DayNightController`, `AudioManager`, `UI`

These are empty GameObjects only — no scripts yet. Scripts are added per-epic.

### Unity MCP Tool Quirks

- **`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
- **`manage_asset(action="move")`** is unreliable — partial moves have been observed. Fallback: `Bash mv` + `refresh_unity(mode="force")`.
- **`manage_animation(controller_add_transition)`** sets wrong `conditionMode` for bools: uses `3` (Equals) instead of `2` (IfNot/false) or `1` (If/true). Always verify and fix via direct YAML edits.
- **`manage_gameobject(create)` ignores `component_properties` for Canvas `renderMode`** — Canvas always defaults to `renderMode = 2` (World Space). After creating a Canvas GO, always follow up with `manage_components set_property renderMode 0` to set Screen Space Overlay.
- **AnimatorController YAML-only transitions** may not be visible in Unity's Animator tab. When rewriting `.controller` files entirely via `Write`, transitions defined only in YAML may need to be manually re-created in the Unity Animator window. Prefer using MCP tools for individual transitions, then fix known bugs in YAML.

### Animator Controller Best Practices (Story 1.6)

- **Always set `WriteDefaultValues: false`** on all animator states. With `true`, states write T-pose defaults for bones they don't animate, causing pose corruption during transitions.
- **Smooth transition durations:** Use 0.1–0.2s crossfade for most transitions. Instant (0s) transitions look snappy/jarring.
- **IsRising detection:** Use `velocity.y > 0.1f` (not `> 0f`) to avoid float noise triggering false positives at rest.

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

### InputSystem_Actions.cs Embeds the Full JSON (Critical)

`InputSystem_Actions.cs` is **not** just a wrapper that reads from `InputSystem_Actions.inputactions` at runtime — it **embeds the entire action map JSON as a string literal** inside the constructor. The `.inputactions` file is only used by Unity's Input Actions editor UI.

**Consequence:** When adding new actions (e.g. Block), you MUST edit **both** files:
1. `InputSystem_Actions.inputactions` — for Unity's editor and future regeneration
2. `InputSystem_Actions.cs` embedded JSON (uses `""` double-escaped quotes) — this is what actually runs

If you only edit `.inputactions`, the runtime `FindAction("Block", throwIfNotFound: true)` will throw `ArgumentException` because the embedded JSON doesn't have the new action.

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
| MEDIUM | `WriteDefaultValues: true` on animator states — causes T-pose bleed; always use `false` |
| MEDIUM | AnimatorController `.controller` file fully rewritten via `Write` tool — transitions may not be visible in Unity Animator; prefer incremental MCP edits + YAML fixes |
| HIGH | `TryGetComponent<EnemyHealth>()` on a hit collider — `Enemy_Grunt` has `CapsuleCollider` on the `Visual` child but `EnemyHealth` on the root; always use `GetComponentInParent<EnemyHealth>()` in hit detection |
| HIGH | Namespace `Game.Debug` conflicts with `UnityEngine.Debug` — any file resolves bare `Debug` to `Game.Debug` instead of the Unity class; use `Game.DevTools` for test/debug utilities |
