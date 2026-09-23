# CLAUDE.md — Unity-BMAD-test-rpg

> Read this at the start of every session. It orients you to the project and records
> patterns learned during development. Coding rules live in `_bmad-output/project-context.md`.

---

## Project Identity

- **Engine:** Unity 6000.6.2f1 (Unity 6.6)
- **Render Pipeline:** URP 17.x (`Assets/Settings/PC_RPAsset`, `PC_Renderer`)
- **Input:** Unity Input System — generated class `InputSystem_Actions` at `Assets/_Game/InputSystem_Actions.cs`; legacy input disabled
- **Camera:** Cinemachine 3.x, third-person over-the-shoulder
- **Platform:** PC Windows x64 → Steam distribution
- **Game type:** 3D RPG

---

## Key File Locations

| What | Path |
|------|------|
| **Authoritative coding rules** | `_bmad-output/project-context.md` |
| **GDD / architecture / narrative / epics** | `_bmad-output/gdd.md`, `game-architecture.md`, `narrative-design.md`, `epics.md` |
| **Sprint status** | `_bmad-output/implementation-artifacts/sprint-status.yaml` |
| **Story files + tech specs** | `_bmad-output/implementation-artifacts/*.md` |
| **Superseded proposals / archived specs** | `_bmad-output/archive/` |
| **All game source code** | `Assets/_Game/` |
| **Game assembly definition** | `Assets/_Game/Game.asmdef` |
| **Git conventions** | `.claude/rules/git-conventions.md` |

> **Never treat `_bmad/` or `_bmad-output/` as game source code.** They are BMAD
> workflow artifacts. Always exclude them from code reviews and source analysis.

---

## Before Writing Any Game Code

1. Read `_bmad-output/project-context.md` — its rules are mandatory
2. Check `sprint-status.yaml` for current state
3. If a story file or tech spec exists for the task, read it fully before implementing
4. Read the folder `CLAUDE.md` for every folder you touch (index below)

---

## Development Workflow

Epics 1–4 were delivered as BMAD stories. Since mid-April 2026, work is done as **quick tech
specs** (`tech-spec-*.md` in `implementation-artifacts/`). When a spec ships a feature that maps
to a sprint story, update `sprint-status.yaml` in the same session, and set the spec's
`status:` to `completed` — otherwise tracking drifts.

| Skill / command | When to use |
|-----------------|-------------|
| `gds-sprint-status` | See what's in-progress / what's next |
| `gds-quick-spec` → `gds-quick-dev` | Spec and implement a feature (current default flow) |
| `gds-create-story` → `gds-dev-story` | Full story flow from `epics.md` |
| `gds-code-review` | Adversarial review after a feature is complete |
| `gds-correct-course` | Re-plan when scope drifts from `epics.md` |
| `NPC:create`, `NPC:dialogue`, `NPC:teach-dialogue` | NPC data + dialogue authoring |
| `quests:design` → `quests:implement` | Quest spec, then Unity assets |
| `perso:commit` | Stage, commit, and push changes |
| `perso:wrap-up` | End of session — update CLAUDE.md with learned patterns |

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

Put each pattern in the **most specific folder CLAUDE.md** that owns it; keep only cross-cutting
rules here. Don't duplicate a rule that already lives in `project-context.md` or a folder file.

---

## Folder CLAUDE.md Index

| Area | File (under `Assets/_Game/`) | Covers |
|------|------------------------------|--------|
| Assembly & input | `CLAUDE.md` | `Game.asmdef`, `InputSystem_Actions` dual-file contract |
| Scenes | `Scenes/CLAUDE.md` | Core.unity managers, MCP scene-load quirk |
| Core | `Scripts/Core/CLAUDE.md` | `CursorManager`, `GameLog`, `GameConstants`, `WorldStateManager` / world facts |
| Animation drivers | `Scripts/Core/Animations/CLAUDE.md` | `AIAnimationDriver` polymorphism (Brain/Health → Driver → Bridge) |
| AI | `Scripts/AI/CLAUDE.md` | Entity brains, health, factions, NPC presence/memory |
| Combat | `Scripts/Combat/CLAUDE.md` | `WeaponHitbox`, animation events, combo guard, draw/sheathe combat state |
| Player | `Scripts/Player/CLAUDE.md` | Cinemachine OTS setup, input map, `PlayerStateManager`, animation driver |
| Progression | `Scripts/Player/Progression/CLAUDE.md` | XP / level / LP / skills event chain |
| Inventory | `Scripts/Inventory/CLAUDE.md` | Inventory, equipment, action bar, pickups |
| World | `Scripts/World/CLAUDE.md` | Interaction, dialogue, containers, doors/locks, `PersistentID` |
| Dev tools | `Scripts/Debug/CLAUDE.md` | `Game.DevTools` namespace rule, respawn scaffolding |
| UI | `Scripts/UI/CLAUDE.md` (+ `Dialogue/`, `HUD/`, `Inventory/`, `Quest/`, `Screens/`) | Canvas setup, cursor, input, layout; HUD (incl. toasts), screens, dialogue, quest UI |
| Prefabs | `Prefabs/CLAUDE.md` | Player / Entity hierarchies, layer rules, world-item rules |
| Monster prefabs | `Prefabs/Entities/Monsters/CLAUDE.md` | Monster hierarchy, hit-detection physics |
| Weapon prefabs | `Prefabs/Items/Weapons/CLAUDE.md` | `_World` / `_Visual` convention, sockets, grip |
| Items data | `ScriptableObjects/Items/CLAUDE.md` | `ItemSO` family |
| NPC data | `Data/NPCs/CLAUDE.md` (+ per-NPC folders) | NPC data SOs, memories, dialogue |
| Quest / skill data | `Data/Quests/CLAUDE.md`, `Data/Skills/CLAUDE.md` | Quest and skill assets |
| Combat animations | `Art/Characters/Humanoids/Animations/Combat/CLAUDE.md` | Animator Controller practices, MCP animation quirks |

---

## Unity MCP Tool Quirks

- **`manage_asset(action="move")`** is unreliable — partial moves have been observed. Fallback: `Bash mv` (move the `.meta` too) + `refresh_unity(mode="force")`.
- **`manage_gameobject(create)` ignores `component_properties` for Canvas `renderMode`** — Canvas always defaults to `renderMode = 2` (World Space). After creating a Canvas GO, always follow up with `manage_components set_property renderMode 0` to set Screen Space Overlay.
- **`refresh_unity(mode="force")` after direct YAML edits destroys the edits** — Unity reimports from cached in-memory state, discarding disk changes. After YAML-editing a `.prefab` file directly, always use `refresh_unity(mode="if_dirty")`. Never use `force` after a raw YAML edit.
- Scene-loading and animation quirks live in `Scenes/CLAUDE.md` and the Combat animations CLAUDE.md.

---

## Unity Lifecycle Gotcha: OnDisable Before OnEnable

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

---

## Code Review Checklist (Patterns Found in Practice)

Cross-cutting issues to check in every Unity MonoBehaviour review. Folder-specific checklists
(namespace, cursor, logging, prefab/layer, input, animator) live in the folder CLAUDE.md files
and `project-context.md` — apply those too.

| Severity | Pattern |
|----------|---------|
| HIGH | `OnDisable` uses fields initialized in `OnEnable` without a null guard (see lifecycle gotcha above) |
| MEDIUM | Public method on MonoBehaviour dereferences a `[SerializeField]` dependency without a null guard — `Awake` setting `enabled = false` does NOT block external callers from reaching public methods; add `if (_dep == null) return;` at the top of every public method that uses a serialized dependency |
| MEDIUM | `.meta` file manually created and missing `MonoImporter` block — Unity may regenerate with new GUID on reimport, breaking prefab script references |
| LOW | `private const string TAG` declared in a class that has no `GameLog.*` calls — dead code, remove it |
| LOW | `[SerializeField]` field declared but never read or written in code — remove unless a future story explicitly needs it |
| LOW | `System.Enum.GetValues(typeof(T))` inside a button-click or event handler — allocates a new array on every call; cache as `static readonly T[]` at class level |
| LOW | `Transform.Find("ChildName")` with no warn/error when null — fails silently if a prefab child is renamed; log a warning when the result is null and the feature is expected |
| LOW | Story/spec File List missing Unity-generated assets — FBX, AnimatorController, `.meta` files, and `Assets/*.lighting` (auto-created when any scene's lighting settings change) |
