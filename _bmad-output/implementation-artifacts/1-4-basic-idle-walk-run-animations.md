# Story 1.4: Basic Idle/Walk/Run Animations

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to see basic idle, walk, and run animations on my character,
so that movement feels alive and responsive.

## Acceptance Criteria

1. When the player is standing still, the idle animation plays.
2. When the player is walking (no Sprint held), the walk animation plays.
3. When the player is running (Sprint held), the run animation plays.
4. Transitions between idle, walk, and run are smooth (no sudden pop).
5. Animation state correctly reflects current movement — stopping immediately returns to idle.
6. Walk speed and run speed remain `_config.walkSpeed` / `_config.runSpeed` (regression — Story 1.3 values unchanged).
7. WASD camera-relative movement and body rotation remain functional (regression — Stories 1.1/1.2 unchanged).
8. No console errors on entering Play Mode.

## Tasks / Subtasks

- [ ] Task 1: Source animation clips (AC: 1, 2, 3)
  - [ ] 1.1 Source 3 animation clips: idle, walk, run — Mixamo (free) recommended
        Go to mixamo.com → Animations tab → search each; set "In Place" = Yes, "Trim" as needed
        Recommended searches: "Idle" / "Walking" / "Running"
  - [ ] 1.2 Export each as FBX for Unity (FBX Binary, Without Skin for anim-only re-downloads)
  - [ ] 1.3 Import FBX files into `Assets/_Game/Art/Characters/Player/Animations/`
  - [ ] 1.4 In Unity Import Settings for each FBX:
        Rig tab → Animation Type: Humanoid → Create Avatar (first import), Copy From (subsequent)
        Animation tab → Loop Time: ✓, Loop Pose: ✓, Root Transform Rotation: Bake Into Pose: ✓
        Root Transform Position (Y): Bake Into Pose: ✓, Root Transform Position (XZ): Bake Into Pose: ✓
        This ensures in-place animation with no positional drift

- [ ] Task 2: Create AnimatorController (AC: 1–5)
  - [ ] 2.1 Create `PlayerAnimatorController.controller` in `Assets/_Game/Art/Characters/Player/Animations/`
        Right-click in Project → Create → Animator Controller
  - [ ] 2.2 Open in Animator window; add float parameter named exactly `"Speed"`
  - [ ] 2.3 Right-click in graph → Create State → From New Blend Tree; name it "Locomotion"
        Set as Default State (right-click → Set as Layer Default State)
  - [ ] 2.4 Double-click the Locomotion blend tree; set:
        Blend Type: 1D, Parameter: Speed, Automate Thresholds: OFF
  - [ ] 2.5 Add 3 motion fields and set manual thresholds:
        - Threshold 0.0 → `player_idle` clip
        - Threshold 3.5 → `player_walk` clip  (just below walkSpeed=3f for clean start)
        - Threshold 6.5 → `player_run` clip   (just above runSpeed=6f for clean transition)
  - [ ] 2.6 Verify no other states exist — the blend tree alone handles all 3 animation states

- [ ] Task 3: Create PlayerAnimator.cs (AC: 1–5)
  - [ ] 3.1 Create `Assets/_Game/Scripts/Player/PlayerAnimator.cs` with content from Dev Notes below
  - [ ] 3.2 Confirm `[RequireComponent(typeof(Animator))]` attribute is on the class
  - [ ] 3.3 Confirm `SpeedHash` is a `private static readonly int` (not a string in Update)
  - [ ] 3.4 Confirm `Awake()` null-checks both `_animator` and `_characterController`; sets `enabled = false` if missing
  - [ ] 3.5 Confirm `Update()` uses `SetFloat(SpeedHash, speed, DAMP_TIME, Time.deltaTime)` for smooth blending

- [ ] Task 4: Wire up Player prefab (AC: 1–8)
  - [ ] 4.1 Open `Assets/_Game/Prefabs/Player/Player.prefab`
  - [ ] 4.2 Add `Animator` component to the root Player GameObject (or character mesh child if a mesh is present)
  - [ ] 4.3 Assign `PlayerAnimatorController` as the Controller in the Animator component
  - [ ] 4.4 **CRITICAL:** Set `Apply Root Motion` = OFF on the Animator component
        (PlayerController owns position via CharacterController.Move — root motion would fight it)
  - [ ] 4.5 Add `PlayerAnimator` component to the same GameObject that has the Animator
  - [ ] 4.6 Confirm `PlayerAnimator` and `Animator` are on the same GameObject so GetComponent works

- [ ] Task 5: Validate in Play Mode (AC: 1–8)
  - [ ] 5.1 Enter Play Mode in `TestScene.unity`
  - [ ] 5.2 Standing still → idle animation plays
  - [ ] 5.3 WASD pressed, no Sprint → walk animation plays
  - [ ] 5.4 WASD + Sprint held → run animation plays
  - [ ] 5.5 Releasing Sprint mid-movement smoothly blends back to walk
  - [ ] 5.6 Stopping movement smoothly blends back to idle
  - [ ] 5.7 Regression: WASD movement still camera-relative; body rotates to face movement direction
  - [ ] 5.8 No console errors; Animator state machine visible in Animator window during Play Mode

## Dev Notes

### What Changes — Precise Scope

**New files:**
- `Assets/_Game/Scripts/Player/PlayerAnimator.cs` — new MonoBehaviour, animation driver
- `Assets/_Game/Art/Characters/Player/Animations/PlayerAnimatorController.controller` — new AnimatorController
- `Assets/_Game/Art/Characters/Player/Animations/` — animation FBX/clip assets (player_idle, player_walk, player_run)

**Modified files:**
- `Assets/_Game/Prefabs/Player/Player.prefab` — add `Animator` + `PlayerAnimator` components

**Unchanged (DO NOT TOUCH):**
- `Assets/_Game/Scripts/Player/PlayerController.cs` — no changes needed
- `Assets/_Game/Scripts/Player/CameraController.cs` — no changes needed
- `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs` — no changes needed
- `Assets/_Game/Data/Config/PlayerConfig.asset` — no changes needed

### PlayerAnimator.cs — Complete Implementation

```csharp
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Drives the Player Animator from CharacterController velocity.
    /// Reads horizontal speed and updates the "Speed" blend tree parameter.
    /// PlayerController owns movement; this component owns animation only.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimator : MonoBehaviour
    {
        private const string TAG = "[Player]";
        private const float DAMP_TIME = 0.1f;

        // Hash the parameter name once at class init — never use string in hot path
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private Animator _animator;
        private CharacterController _characterController;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _characterController = GetComponent<CharacterController>();

            if (_animator == null)
            {
                GameLog.Error(TAG, "Animator component not found — PlayerAnimator disabled.");
                enabled = false;
                return;
            }
            if (_characterController == null)
            {
                GameLog.Error(TAG, "CharacterController not found — PlayerAnimator cannot read speed.");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            float speed = _characterController.velocity.magnitude;
            _animator.SetFloat(SpeedHash, speed, DAMP_TIME, Time.deltaTime);
        }
    }
}
```

**Notes on this implementation:**
- `DAMP_TIME = 0.1f` gives smooth blend transitions. Increase to 0.15f if transitions feel too snappy; decrease to 0.05f if too sluggish.
- `_characterController.velocity.magnitude` reflects actual ground movement — it returns ~0 when blocked by a wall, preventing the "running in place" issue that raw input magnitude would cause.
- No `OnDisable` null guard needed — this component has no `_input` initialized in `OnEnable`. The `enabled = false` pattern in `Awake` handles the startup failure case cleanly.

### AnimatorController Blend Tree — Setup Detail

```
PlayerAnimatorController.controller
└── Base Layer
    └── [Default State] Locomotion  (Blend Tree, 1D)
        Parameter: Speed (float)
        Automate Thresholds: OFF
        ┌─────────────┬──────────────┐
        │  Threshold  │   Motion     │
        ├─────────────┼──────────────┤
        │     0.0     │  player_idle │
        │     3.5     │  player_walk │
        │     6.5     │  player_run  │
        └─────────────┴──────────────┘
```

**Why these thresholds?**
- `walkSpeed = 3f` → threshold at 3.5 ensures the walk animation starts blending in before max walk speed
- `runSpeed = 6f` → threshold at 6.5 ensures run blend completes at sprint speed
- The blend tree linearly interpolates between clips — at speed=3f the player is 100% walk blend; at speed=5f they are partway between walk and run

**If speeds are changed** in `PlayerConfigSO` during design, update these thresholds proportionally.

### Velocity Magnitude vs. Horizontal Speed

`_characterController.velocity.magnitude` includes the Y component (vertical velocity). When grounded, `_verticalVelocity = GROUNDED_VELOCITY = -2f`, which adds a small constant to the magnitude (~`sqrt(walkSpeed² + 2²)`). For `walkSpeed=3f`: actual magnitude ≈ 3.6f instead of 3.0f.

**Recommended fix** if this causes animation issues during testing:
```csharp
// In Update() — use horizontal speed only
Vector3 horizontalVelocity = new Vector3(
    _characterController.velocity.x, 0f, _characterController.velocity.z);
float speed = horizontalVelocity.magnitude;
```
Start with `velocity.magnitude` first and switch if blend feels off. Adjust thresholds if needed.

### Mixamo Free Animation Workflow

1. Navigate to [mixamo.com](https://www.mixamo.com)
2. Upload a character or use a default T-pose character
3. Animations tab → Search "Idle" → Select "Idle" (by Adobe) → In Place: N/A → Download
4. Search "Walking" → Select "Walking" → In Place: ✓ → Download
5. Search "Running" → Select "Running" → In Place: ✓ → Download
6. Export settings for each: Format = FBX for Unity, Skin = Without Skin (after first character download)
7. In Unity, after import: select the FBX → Rig tab → Humanoid → Create Avatar

**Alternative:** Unity Asset Store free animation packs (e.g., "Starter Assets — ThirdPerson" package which the architecture doc recommended for Epic 1 foundation — its animation controller may be referenced).

### Regression Prevention — Stories 1.1/1.2/1.3

The following `PlayerController.cs` behaviors are **final** and must not change:
- Camera-relative movement vector (`camForward * moveInput.y + camRight * moveInput.x`)
- Body rotation via `Quaternion.Slerp` toward `moveDir`
- `_config.walkSpeed` / `_config.runSpeed` / `_config.rotationSpeed` from `PlayerConfigSO`
- Sprint detection via `_input.Player.Sprint.IsPressed()`
- `_input` null guard in `OnDisable()`
- `GRAVITY = -9.81f` and `GROUNDED_VELOCITY = -2f` constants

`PlayerAnimator` reads `CharacterController.velocity` as a **passive observer** — it never writes to CharacterController or PlayerController state.

### Previous Story Intelligence

**From Story 1.3 (done):** The `PlayerConfigSO` was extracted from `PlayerController`. Important developer action still pending from Story 1.3:
> Developer must create a `PlayerConfig.asset` instance (right-click in Project → Create → Game → Config → Player Config) and assign it on the Player prefab's `PlayerController` component.

If `PlayerConfig.asset` does not yet exist at `Assets/_Game/Data/Config/PlayerConfig.asset`, create it as part of this story's Task 4 setup before testing.

**Established patterns from Stories 1.1–1.3 to follow:**
- `namespace Game.Player` wrapping
- `private const string TAG = "[Player]";`
- `[RequireComponent(typeof(...))]` attribute
- `GameLog.Error(TAG, msg); enabled = false; return;` for missing refs in `Awake()`
- Cache ALL component refs in `Awake()` — never in `Update()`

### Architecture Compliance

| Rule | Applied |
|---|---|
| `Debug.Log` banned — use `GameLog` | ✅ All logging via `GameLog.Error(TAG, ...)` |
| No `GetComponent` in `Update` | ✅ Both cached in `Awake` |
| GameLog TAG constant per class | ✅ `[Player]` |
| All custom code under `Assets/_Game/` | ✅ Script in `_Game/Scripts/Player/`, art in `_Game/Art/Characters/` |
| No magic numbers in game logic | ✅ `DAMP_TIME` as const; thresholds document relationship to config values |
| `Apply Root Motion` must be OFF | ✅ Documented as critical in Task 4.4 |
| Animator param hash (not string in hot path) | ✅ `Animator.StringToHash()` at class init |
| Null-guard in `Awake` for all serialized refs | ✅ `_animator` and `_characterController` guarded |

### Project Structure Notes

**New directory created:**
- `Assets/_Game/Art/Characters/Player/Animations/` — AnimatorController and animation clips

**Folder rationale:** Architecture doc defines `_Game/Art/Characters/` for character art assets. Player animation assets belong here rather than mixing with script files.

**Player prefab structure after this story:**
```
Player.prefab  (Assets/_Game/Prefabs/Player/)
├── CharacterController
├── PlayerController.cs
├── CameraController.cs
├── Animator  ← NEW (Apply Root Motion: OFF)
├── PlayerAnimator.cs  ← NEW
└── CameraTarget  (child, local Y ≈ 1.6)
```

### References

- Blend trees for movement: [Source: game-architecture.md#Engine & Framework — Animation: "Blend trees for movement"]
- Animation clip naming convention: [Source: game-architecture.md#Naming Conventions — "Animation clips: camelCase_action"]
- `PlayerConfigSO` walkSpeed=3f / runSpeed=6f: [Source: Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs]
- `PlayerController.cs` current implementation: [Source: Assets/_Game/Scripts/Player/PlayerController.cs]
- No `GetComponent` in Update: [Source: project-context.md#Engine-Specific Rules — Unity Lifecycle]
- No magic numbers — config SO: [Source: project-context.md#Config & Data Anti-Patterns]
- Logging mandate: [Source: project-context.md#Logging — MANDATORY]
- Null-guard Awake pattern: [Source: game-architecture.md#Cross-cutting Concerns — Error Handling]
- Player prefab structure: [Source: CLAUDE.md#Player Prefab Structure]
- Story 1.3 learnings: [Source: _bmad-output/implementation-artifacts/1-3-run-and-walk.md]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
