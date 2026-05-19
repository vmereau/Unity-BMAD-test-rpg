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
| **Authoritative coding rules (57 rules)** | `_bmad-output/project-context.md` |
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

1. Read `_bmad-output/project-context.md` — all 57 rules are mandatory
2. Check `_bmad-output/implementation-artifacts/sprint-status.yaml` for current state
3. If a story file exists for the task, read it fully before implementing

---

## During a Session — Watch for CLAUDE.md Updates

Throughout any session, actively watch for new patterns, gotchas, or rules worth preserving.
**When you spot one, immediately tell the user** before moving on. Format:

```
> [CLAUDE.md candidate] <root | Assets/_Game/... folder>
> Pattern: <one-line description>
> Suggested addition: <brief content or note>
```

Triggers to watch for:

- A Unity MCP tool behaves unexpectedly or requires a workaround
- A Unity lifecycle, serialization, or rendering edge case causes a bug or forces a code pattern
- A naming convention, namespace rule, or layer/prefab constraint is clarified or discovered
- A folder-specific CLAUDE.md is missing a rule that was applied during the session
- A code review surfaces a recurring issue not yet in the checklist
- A new system (script, prefab, SO, scene) is introduced that other agents need to know about

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

### Assembly, Input & Scene Rules

> - Assembly setup + `InputSystem_Actions` dual-file contract → `Assets/_Game/CLAUDE.md`
> - Scene stubs + MCP scene-load quirk → `Assets/_Game/Scenes/CLAUDE.md`
> - Debug namespace rules + EnemyRespawner scaffolding → `Assets/_Game/Scripts/Debug/CLAUDE.md`

### Unity MCP Tool Quirks

- **`manage_asset(action="move")`** is unreliable — partial moves have been observed. Fallback: `Bash mv` + `refresh_unity(mode="force")`.
- **`manage_gameobject(create)` ignores `component_properties` for Canvas `renderMode`** — Canvas always defaults to `renderMode = 2` (World Space). After creating a Canvas GO, always follow up with `manage_components set_property renderMode 0` to set Screen Space Overlay.
- **`refresh_unity(mode="force")` after direct YAML edits destroys the edits** — Unity reimports from cached in-memory state, discarding disk changes. After YAML-editing a `.prefab` file directly, always use `refresh_unity(mode="if_dirty")`. Never use `force` after a raw YAML edit.
- Animation and scene-specific MCP quirks → `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md` and `Assets/_Game/Scenes/CLAUDE.md`.

### Animator, Camera & Player Script Rules

> - Animator Controller best practices + MCP animation quirks → `Assets/_Game/Art/Characters/Humanoids/Animations/Combat/CLAUDE.md`
> - Cinemachine OTS setup, float/euler quirks, Input System action map, CharacterController velocity Y, PlayerStateManager gate pattern, PlayerAnimationDriver / HumanoidAnimationBridge API → `Assets/_Game/Scripts/Player/CLAUDE.md`
> - AI animation polymorphism via `AIAnimationDriver` base class (Brain/Health → Driver → Bridge contract for monster + humanoid AI) → `Assets/_Game/Scripts/Core/Animations/CLAUDE.md`

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

### Prefab Structure & Layer Rules

> See `Assets/_Game/Prefabs/CLAUDE.md` for full prefab hierarchies (Player, Enemy_Grunt) and layer requirements.

---

## Code Review Checklist (Patterns Found in Practice)

High-signal issues to always check in Unity MonoBehaviour reviews:

| Severity | Pattern |
|----------|---------|
| HIGH | `OnDisable` calls fields initialized in `OnEnable` without null guard |
| HIGH | `enabled = false` set in `Awake` without OnDisable null guard |
| MEDIUM | `GetComponent` or `Camera.main` called in `Update` instead of cached in `Awake` |
| MEDIUM | `.meta` file manually created and missing `MonoImporter` block — Unity may regenerate with new GUID on reimport, breaking prefab script references |
| LOW | `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` used directly (use `GameLog`) |
| LOW | Magic numbers in gameplay logic (use `[SerializeField]` or config SO) |
| LOW | Story File List missing Unity Editor-generated assets (FBX, AnimatorController, .meta files) — always audit art asset directories when story covers animation/import work |
| HIGH | `Cursor.lockState`, `Cursor.visible`, or `CursorLockMode` used directly outside `CursorManager.cs` — all cursor state changes must go through `CursorManager.Lock()` / `CursorManager.Unlock()` / `CursorManager.IsLocked` (`Assets/_Game/Scripts/Core/CursorManager.cs`) |
| HIGH | Namespace `Game.Debug` — use `Game.DevTools`; see `Assets/_Game/Scripts/Debug/CLAUDE.md` |
| HIGH | Prefab structure or layer misconfigured — see `Assets/_Game/Prefabs/CLAUDE.md` |
| HIGH | Assembly / InputSystem / Player / Animator rules — see folder-specific CLAUDE.md files |
| MEDIUM | Public method on MonoBehaviour dereferences a `[SerializeField]` dependency without a null guard — `Awake` setting `enabled = false` does NOT block external callers from reaching public methods; add `if (_dep == null) return;` at the top of every public method that uses a serialized dependency |
| LOW | `private const string TAG` declared in a class that has no `GameLog.*` calls — dead code, remove it |
| LOW | `[SerializeField]` field declared but never read or written in code — remove unless a future story explicitly needs it |
| LOW | `System.Enum.GetValues(typeof(T))` inside a button-click or event handler — allocates a new array on every call; cache as `static readonly T[]` at class level |
| LOW | `Transform.Find("ChildName")` for context menu button lookup with no warn/error when null — fails silently if prefab child is renamed; log a warning when the result is null and the feature is expected |
| LOW | Story File List missing Unity-generated `{SceneName}Settings.lighting` asset — auto-created at `Assets/` root when any scene's lighting settings are modified in the Editor (no bake required); always check `Assets/*.lighting` after scene work |
