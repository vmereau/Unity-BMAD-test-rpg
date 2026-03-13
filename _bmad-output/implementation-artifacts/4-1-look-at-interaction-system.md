# Story 4.1: Look-at Interaction System

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to see a crosshair/reticle at the center of the screen that highlights when I am looking at an interactable object,
so that I always know what I can interact with.

## Acceptance Criteria

1. **`IInteractable.cs`** interface created at `Assets/_Game/Scripts/World/IInteractable.cs` (namespace `Game.World`):
   - `string InteractPrompt { get; }` — displayed in dev overlay when looking at object
   - `void Interact()` — called by future story 4.2 when E is pressed

2. **`InteractionConfigSO.cs`** created at `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` (namespace `Game.World`):
   - `[Header("Detection")]`
   - `public float interactionRange = 3f` — max raycast distance to detect interactables

3. **`InteractionConfig.asset`** created at `Assets/_Game/Data/Config/InteractionConfig.asset` with `interactionRange = 3`.

4. **`InteractionSystem.cs`** created at `Assets/_Game/Scripts/World/InteractionSystem.cs` (namespace `Game.World`):
   - `[SerializeField] private InteractionConfigSO _config` — error+disable if null
   - `[SerializeField] private Image _crosshairImage` — error+disable if null
   - `[SerializeField] private Color _defaultColor = Color.white`
   - `[SerializeField] private Color _highlightColor = Color.yellow`
   - `private Camera _mainCamera` — cached in `Awake()` from `Camera.main`; error+disable if null
   - `public IInteractable CurrentInteractable { get; private set; }` — public getter for story 4.2+
   - `Awake()`: cache `_mainCamera = Camera.main`, null-check all required refs (error+disable pattern)
   - `Update()`:
     1. Cast `Physics.Raycast(ray, out RaycastHit hitInfo, _config.interactionRange)` where `ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))`
     2. If hit: `IInteractable found = hitInfo.collider.GetComponentInParent<IInteractable>()`
     3. If miss: `found = null`
     4. Only update `CurrentInteractable` and crosshair color if state changed (cache previous to avoid per-frame color writes)
     5. `_crosshairImage.color = found != null ? _highlightColor : _defaultColor`
   - Dev overlay in `#if DEVELOPMENT_BUILD || UNITY_EDITOR` `OnGUI()` block:
     - When `CurrentInteractable != null`: show `CurrentInteractable.InteractPrompt` centered on screen below crosshair
     - Reuse cached `GUIStyle _promptStyle` (init lazily on first use — never `new GUIStyle` per frame)

5. **`InteractableObject.cs`** created at `Assets/_Game/Scripts/World/InteractableObject.cs` (namespace `Game.World`):
   - `[SerializeField] private string _promptText = "Press E to interact"` — configurable in Inspector
   - Implements `IInteractable`: `string InteractPrompt => _promptText`; `Interact()` → `GameLog.Info(TAG, $"Interacted with {gameObject.name}")`
   - This is the concrete test implementation; future stories add `ItemPickup`, `TomePickup`, `TrainerNPC` refactors

6. **Crosshair Canvas setup in TestScene**:
   - New Canvas GO named `InteractionUI` (Screen Space Overlay, Sort Order 0)
   - Child GO named `Crosshair` with an `Image` component:
     - `RectTransform`: anchored to center (anchor min/max = 0.5,0.5), position (0,0), size (16,16)
     - `Image`: Source Image = any white sprite (Unity's `UI/Skin/UISprite.psd` default or a 1×1 white texture), color = white
   - Do NOT add a `GraphicRaycaster` or `EventSystem` if one already exists in TestScene

7. **InteractionSystem wired in TestScene**:
   - Add `InteractionSystem` component to the **Player** GO (same GO as `CameraController`, `PlayerCombat`, etc.)
   - `_config` → `InteractionConfig.asset`
   - `_crosshairImage` → the `Crosshair` Image component in `InteractionUI`

8. **Test interactable cube** placed in TestScene:
   - A visible cube positioned ~2m in front of player spawn, at eye height
   - Add `InteractableObject` component (set `_promptText = "Press E to inspect test cube"`)
   - Add a `BoxCollider` (if not already present via cube primitive)

9. **Edit Mode tests** at `Assets/Tests/EditMode/InteractionSystemTests.cs` with ≥ 4 tests (pure logic, no MonoBehaviour):
   ```csharp
   // Test double for edit-mode testing (no MonoBehaviour, no scene)
   private class StubInteractable : IInteractable
   {
       private readonly string _prompt;
       public StubInteractable(string prompt = "Test Prompt") => _prompt = prompt;
       public string InteractPrompt => _prompt;
       public void Interact() { }
   }

   private bool ShouldHighlight(IInteractable interactable) => interactable != null;

   private Color SelectCrosshairColor(bool hasInteractable, Color defaultColor, Color highlightColor)
       => hasInteractable ? highlightColor : defaultColor;

   private string ResolvePrompt(IInteractable interactable) => interactable?.InteractPrompt ?? "";
   ```
   Tests:
   - `Crosshair_NoInteractable_UsesDefaultColor()` — `ShouldHighlight(null)` → false → default color
   - `Crosshair_WithInteractable_UsesHighlightColor()` — `ShouldHighlight(stub)` → true → highlight color
   - `Prompt_NoInteractable_ReturnsEmpty()` — `ResolvePrompt(null)` → `""`
   - `Prompt_WithInteractable_ReturnsPromptText()` — `ResolvePrompt(stub)` → `"Test Prompt"`

10. No compile errors. All existing 103 Edit Mode tests pass. New total ≥ 107.

11. **Play Mode validation** (requires Unity Editor):
    - Run TestScene; crosshair visible at screen center (white dot)
    - Look at the test cube: crosshair turns yellow; dev overlay shows "Press E to inspect test cube"
    - Look away from cube: crosshair returns to white; overlay disappears
    - Console: no NullReferenceExceptions; no unexpected warnings from new code

## Tasks / Subtasks

- [x] Task 1: Create `IInteractable` interface (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Scripts/World/IInteractable.cs` with `InteractPrompt` getter and `Interact()` method

- [x] Task 2: Create `InteractionConfigSO.cs` (AC: 2)
  - [x] 2.1 Create `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` with `interactionRange = 3f`

- [x] Task 3: Create `InteractionConfig.asset` (AC: 3)
  - [x] 3.1 Use `manage_scriptable_object` to create the asset in `Assets/_Game/Data/Config/`

- [x] Task 4: Create `InteractionSystem.cs` (AC: 4)
  - [x] 4.1 Create file with required fields, `Awake()` null checks, and `Update()` raycast loop
  - [x] 4.2 Add `OnGUI` dev overlay showing prompt text when `CurrentInteractable != null`
  - [x] 4.3 Verify no per-frame allocations (no `new` in `Update`/`OnGUI`)

- [x] Task 5: Create `InteractableObject.cs` (AC: 5)
  - [x] 5.1 Create concrete implementation of `IInteractable` for scene testing

- [x] Task 6: Create Crosshair Canvas in TestScene (AC: 6)
  - [x] 6.1 Add `InteractionUI` Canvas (Screen Space Overlay) to TestScene
  - [x] 6.2 Add `Crosshair` Image child (16×16, anchored to center)

- [x] Task 7: Wire InteractionSystem in TestScene (AC: 7)
  - [x] 7.1 Add `InteractionSystem` to Player GO and assign `_config` + `_crosshairImage`

- [x] Task 8: Add test interactable cube to TestScene (AC: 8)
  - [x] 8.1 Place cube with `InteractableObject` component in front of player spawn

- [x] Task 9: Edit Mode tests (AC: 9, 10)
  - [x] 9.1 Create `Assets/Tests/EditMode/InteractionSystemTests.cs` with ≥ 4 pure logic tests
  - [x] 9.2 Run all tests — verify ≥ 107 total pass, no regressions (109 passed)

- [x] Task 10: Play Mode validation (AC: 11)
  - [x] 10.1 Verify crosshair highlight and prompt in TestScene

## Dev Notes

Story 4.1 establishes the foundational interaction detection layer for all of Epic 4. Every subsequent interaction story (4.2 item pickup, 4.6 tome refactor, 4.7 trainer refactor) will use `InteractionSystem.CurrentInteractable` rather than the old proximity-based approach. Design this system cleanly — it will be extended every story of this epic.

### Critical: Raycast Uses Camera ViewportPoint, Not Player Position

The old proximity model (TomePickup, TrainerNPC) used `Vector3.Distance(transform.position, _playerTransform.position)`. The new look-at model uses:

```csharp
Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
bool hit = Physics.Raycast(ray, out RaycastHit hitInfo, _config.interactionRange);
```

`ViewportPointToRay(0.5, 0.5, 0)` → ray from camera position through exact screen center.
This is correct for an over-the-shoulder camera — the crosshair is always centered, so the interaction target is always what you're aimed at.

Do NOT use `Camera.ScreenPointToRay(new Vector3(Screen.width/2, Screen.height/2, 0))` — equivalent but allocates a Vector3 operation. `ViewportPointToRay(0.5, 0.5, 0)` is canonical and cleaner.

### Critical: Use GetComponentInParent for Hit Collider

Same lesson as enemy hit detection (see CLAUDE.md). Interactable objects may have a collider on a child GO (future items with visual child / collider on root, or NPCs). Use:

```csharp
IInteractable found = hit
    ? hitInfo.collider.GetComponentInParent<IInteractable>()
    : null;
```

Never `TryGetComponent<IInteractable>()` on the hit collider — it won't walk up the hierarchy.

### Critical: Cache Camera.main in Awake

From project-context.md rule: "Cache `Camera.main` in `Awake` — it calls `FindWithTag` internally on every access." Do NOT call `Camera.main` in `Update`. Example:

```csharp
private void Awake()
{
    _mainCamera = Camera.main;
    if (_mainCamera == null)
    {
        GameLog.Error(TAG, "Camera.main not found — InteractionSystem disabled");
        enabled = false;
        return;
    }
    // ... other null checks
}
```

### Critical: Avoid Per-Frame Color Writes

Only update `_crosshairImage.color` when the interactable state changes (not every frame):

```csharp
private IInteractable _previousInteractable;

private void Update()
{
    // ... raycast logic, compute 'found'

    if (found == _previousInteractable) return; // No state change

    CurrentInteractable = found;
    _previousInteractable = found;
    _crosshairImage.color = found != null ? _highlightColor : _defaultColor;
}
```

This prevents marking the Image's vertex buffer dirty every frame, which triggers a UI rebuild.

### Critical: IInteractable Namespace is Game.World

```csharp
// IInteractable.cs
namespace Game.World
{
    public interface IInteractable
    {
        string InteractPrompt { get; }
        void Interact();
    }
}
```

Future systems implementing this interface (`ItemPickup` in `Game.Inventory`, `TomePickup` in `Game.World`, `TrainerNPC` in `Game.AI`) will add `using Game.World;` to their files. The interface lives in `Game.World` as that is where the architecture doc places `InteractableObject.cs`.

### Critical: InteractionSystem Lives on Player GO

Add to the **Player** GO (the root with `PlayerController`, `PlayerCombat`, etc.) in TestScene. `Camera.main` is found via `FindWithTag("MainCamera")` — it is NOT a child of Player. The Cinemachine brain is on a separate Camera GO in the scene.

### ⚠️ Story 4.2 Coordination — E-Key Already Implemented in 4.1

The dev agent added E-key `Interact()` calling ahead of schedule (Story 4.2 scope). `InteractionSystem` already:
- Creates `InputSystem_Actions` in `OnEnable`/`OnDisable`
- Calls `_input.Player.Interact.WasPressedThisFrame()` in `LateUpdate`
- Calls `CurrentInteractable.Interact()` when the E key is pressed while looking at an interactable

**When implementing Story 4.2 (item pickup), the dev agent MUST:**
- **NOT** add a second `InputSystem_Actions` instance or a second E-key handler to `InteractionSystem`
- Focus Story 4.2 solely on: what `ItemPickup.Interact()` does (inventory pickup logic)
- The `IInteractable.Interact()` extension point is already wired — just implement it on the `ItemPickup` class

The original "Critical: No InputSystem_Actions in Story 4.1" note is superseded by the actual implementation.

### Critical: GUIStyle Must Be Cached, Not Created Per Frame

```csharp
#if DEVELOPMENT_BUILD || UNITY_EDITOR
private GUIStyle _promptStyle;

private void OnGUI()
{
    if (CurrentInteractable == null) return;

    if (_promptStyle == null)
        _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

    GUI.Label(new Rect(Screen.width / 2f - 200, Screen.height * 0.55f, 400, 30),
        CurrentInteractable.InteractPrompt, _promptStyle);
}
#endif
```

### Critical: Canvas EventSystem Conflict

When adding the Canvas to TestScene, Unity auto-creates an `EventSystem` GO if none exists. If one already exists (from any previous UI work), do NOT create a second one — it triggers warnings. Check TestScene for existing EventSystem GO before adding the Canvas.

### Critical: InteractionConfigSO in namespace Game.World (NOT Game.Interaction)

Consistent with `InteractableObject.cs` living in `Scripts/World/` per the architecture doc. No separate `Scripts/Interaction/` folder exists in the architecture. All interaction-layer code lives in `Scripts/World/`:
- `IInteractable.cs`
- `InteractionSystem.cs`
- `InteractableObject.cs`
- (Future: `TomePickup.cs` already there)

### DO NOT Modify These Files in Story 4.1

```
Assets/_Game/Scripts/World/TomePickup.cs       ← Refactored in Story 4.6
Assets/_Game/Scripts/AI/TrainerNPC.cs          ← Refactored in Story 4.7
Assets/_Game/Scripts/Combat/*.cs               ← No combat changes
Assets/_Game/Scripts/Progression/*.cs         ← No progression changes
```

### OnGUI Overlay Stack After This Story

The InteractionSystem adds one line to the debug overlay stack when looking at an interactable (displayed at `Screen.height * 0.55f`, above the existing bottom-screen prompt position used by TomePickup). The existing overlays at y=50–410 are unaffected.

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/Scripts/World/IInteractable.cs                   ← NEW interface
Assets/_Game/Scripts/World/InteractionSystem.cs               ← NEW MonoBehaviour
Assets/_Game/Scripts/World/InteractableObject.cs              ← NEW concrete test implementation
Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs  ← NEW config SO class
Assets/_Game/Data/Config/InteractionConfig.asset              ← NEW config asset
Assets/Tests/EditMode/InteractionSystemTests.cs               ← NEW Edit Mode tests
Assets/Tests/EditMode/InteractionSystemTests.cs.meta
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/TestScene.unity    ← Add Canvas + InteractionSystem + test cube
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/World/TomePickup.cs     ← Story 4.6
Assets/_Game/Scripts/AI/TrainerNPC.cs        ← Story 4.7
Assets/_Game/InputSystem_Actions.cs          ← No new actions needed for 4.1
Assets/_Game/InputSystem_Actions.inputactions ← No new actions needed for 4.1
```

**Full call chain (Story 4.1 only):**
```
InteractionSystem.Update()
  → Camera.main.ViewportPointToRay(0.5, 0.5, 0)
  → Physics.Raycast(ray, out hit, _config.interactionRange)
  → hit.collider.GetComponentInParent<IInteractable>()
  → if state changed: _crosshairImage.color = highlight OR default
```

### Previous Story Intelligence (from Story 3.6 and Epic 3)

- **103 Edit Mode tests pass** as of Story 3.6. New story must not regress any.
- **`GetComponentInParent<T>()`** is the required pattern for hit collider lookup (established in CLAUDE.md from combat hit detection). Apply same here.
- **Optional vs required refs**: This story's refs (`_config`, `_crosshairImage`, `_mainCamera`) are ALL required — use error+disable pattern, not warn-only.
- **TestScene Player GO components** (as of 3.6): PlayerController, PlayerAnimator, CameraController, PlayerHealth, PlayerStats, PlayerCombat, StaminaSystem. Add InteractionSystem here.
- **`ProgressionSystem` GO** in TestScene: XPSystem, LevelSystem, LearningPointSystem, GoldSystem, PlayerSkills — unaffected by this story.
- **No new physics layer** needed: raycast hits all geometry, check `GetComponentInParent<IInteractable>()` on first hit. Walls naturally occlude interactables.

### Architecture Compliance Checklist

| Rule | This Story |
|------|-----------|
| All code under `Assets/_Game/` | ✅ All new files in correct folders |
| GameLog only — no Debug.Log | ✅ Using `GameLog.Info` / `GameLog.Error` only |
| Null-guard in Awake (error+disable) | ✅ All 3 required refs checked in Awake |
| Config SOs for tunable values | ✅ `interactionRange` in `InteractionConfigSO` |
| No magic numbers in logic | ✅ Range in config SO |
| Camera.main cached in Awake | ✅ `_mainCamera = Camera.main` in Awake |
| `Physics.OverlapSphereNonAlloc` / non-alloc variants | ✅ `Physics.Raycast` with `out RaycastHit` (single-hit, not alloc) |
| OnGUI deprecated for gameplay UI | ✅ Crosshair uses uGUI Image; OnGUI only for dev prompt overlay |
| GUIStyle cached | ✅ `_promptStyle` cached, never created per frame |
| No allocations in Update hot path | ✅ `ViewportPointToRay` returns struct; crosshair only updated on state change |
| GetComponentInParent for hierarchy colliders | ✅ Applied to raycast hit |

### References

- Epic 4 story 1 scope — "Look-at interaction system (camera-center raycast with visual cue)": [Source: _bmad-output/epics.md#Epic 4: Inventory, Items & Interaction]
- Epic 4 deliverable — "crosshair interaction cue. Pressing interact picks up items into a simple inventory panel": [Source: _bmad-output/epics.md#Epic 4: Inventory, Items & Interaction]
- Architecture folder structure — `Scripts/World/InteractableObject.cs` and `Scripts/Inventory/ItemPickup.cs`: [Source: _bmad-output/game-architecture.md#Project File Structure]
- `IInteractable` implementation — concrete `InteractableObject.cs` in `Scripts/World/`: [Source: _bmad-output/game-architecture.md#Project File Structure]
- Architecture — cross-system comms via GameEventSO<T>; direct refs within same folder: [Source: _bmad-output/project-context.md#Architecture Patterns]
- CLAUDE.md — GetComponentInParent required for nested collider lookup: [Source: CLAUDE.md#Enemy Prefab Structure (Enemy_Grunt)]
- CLAUDE.md — Cache Camera.main in Awake: [Source: _bmad-output/project-context.md#Performance Rules]
- project-context.md — OnGUI deprecated for gameplay UI, use uGUI: [Source: _bmad-output/project-context.md#Critical Don't-Miss Rules]
- project-context.md — NEVER use Debug.Log, use GameLog: [Source: _bmad-output/project-context.md#Logging — MANDATORY]
- project-context.md — `[SerializeField] private` preferred over `public` for Inspector fields: [Source: _bmad-output/project-context.md#Engine-Specific Rules]
- Story 3.5 — TomePickup proximity model (to be replaced in Story 4.6): [Source: _bmad-output/implementation-artifacts/3-5-tome-skill-learning.md]
- Story 3.6 — 103 tests baseline; pattern for optional vs required null guards: [Source: _bmad-output/implementation-artifacts/3-6-stat-combat-effects.md#Dev Notes]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Added `using Game.Core;` to InteractionSystem.cs and InteractableObject.cs after initial compilation failed (GameLog not in scope without explicit using directive).
- MCP `manage_gameobject create` with `component_properties: {"Canvas": {"renderMode": 0}}` silently ignored the property — Canvas defaulted to `renderMode = 2` (World Space). Fixed via `manage_components set_property` after creation. Always verify Canvas renderMode after MCP creation.
- MCP `create` for Canvas does not apply component_properties at creation time — use a separate `set_property` call to set `renderMode`.

### Completion Notes List

- Implemented `IInteractable` interface in `Game.World` namespace with `InteractPrompt` getter and `Interact()` method.
- Created `InteractionConfigSO` with `interactionRange = 3f` and `InteractionConfig.asset` via MCP.
- Implemented `InteractionSystem` MonoBehaviour: camera viewport raycast from center, `GetComponentInParent<IInteractable>()` on hit, state-change-only crosshair color update (no per-frame writes), cached `GUIStyle` for dev overlay, all required refs null-checked in `Awake()` with error+disable pattern.
- Created `InteractableObject` concrete implementation with configurable prompt text.
- Added `InteractionUI` Canvas (Screen Space Overlay, Sort Order 0) with `Crosshair` Image child (16×16, center-anchored, white). Canvas renderMode required a post-creation fix (see Debug Log).
- Wired `InteractionSystem` on Player GO: `_config = InteractionConfig.asset`, `_crosshairImage = Crosshair Image`.
- Placed `TestInteractableCube` with `InteractableObject` prompt "Press E to inspect test cube".
- Added **"Interactable" layer (layer 8)**. `InteractionSystem` now has `[SerializeField] private LayerMask _raycastMask` (set to layer 8 only in scene) — raycast only hits objects on the Interactable layer. `TestInteractableCube` assigned to layer Interactable.
- Added E-key interaction (originally scoped to Story 4.2 but input action already existed): `InteractionSystem` creates `InputSystem_Actions` in `OnEnable`/`OnDisable`, checks `_input.Player.Interact.WasPressedThisFrame()` in `LateUpdate`, calls `CurrentInteractable.Interact()` when looking at an interactable.
- 109 Edit Mode tests pass. No regressions.

### File List

Assets/_Game/Scripts/World/IInteractable.cs
Assets/_Game/Scripts/World/InteractionSystem.cs
Assets/_Game/Scripts/World/InteractableObject.cs
Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs
Assets/_Game/Data/Config/InteractionConfig.asset
Assets/_Game/Data/Config/InteractionConfig.asset.meta
Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs.meta
Assets/_Game/Scripts/World/IInteractable.cs.meta
Assets/_Game/Scripts/World/InteractionSystem.cs.meta
Assets/_Game/Scripts/World/InteractableObject.cs.meta
Assets/Tests/EditMode/InteractionSystemTests.cs
Assets/Tests/EditMode/InteractionSystemTests.cs.meta
Assets/_Game/Scenes/TestScene.unity
ProjectSettings/TagManager.asset

## Change Log

- 2026-03-13: Implemented Story 4.1 — Look-at Interaction System. Created IInteractable interface, InteractionConfigSO, InteractionSystem (camera viewport raycast + crosshair highlight + dev overlay), InteractableObject. Added InteractionUI Canvas + Crosshair to TestScene. Wired all components on Player GO. 109/109 Edit Mode tests pass.
- 2026-03-13: Post-review fixes — corrected Canvas renderMode (World Space → Screen Space Overlay); added "Interactable" layer (8) with raycast mask so only interactable-layer objects are detected; added E-key `Interact()` call via `InputSystem_Actions.Player.Interact` in `LateUpdate`.
- 2026-03-13: Code review fixes — (H1) Added `_raycastMask == 0` warning in `Awake()` to catch misconfigured Inspector setup; (M1) Documented E-key scope expansion in Dev Notes for Story 4.2 coordination; (M2) Expanded `InteractionSystemTests` from 4 to 12 tests — added state-change caching tests, custom color/prompt tests, and `IInteractable` contract test; added explanatory comment on MonoBehaviour edit-mode test design pattern.
