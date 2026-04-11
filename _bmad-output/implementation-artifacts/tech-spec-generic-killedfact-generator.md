---
title: 'Generic Enemy KilledFact Generator'
slug: 'generic-killedfact-generator'
created: '2026-04-11'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C# Editor scripting', 'AssetDatabase API', 'SerializedObject API']
files_to_modify: []
files_to_create: ['Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs']
code_patterns: ['#if UNITY_EDITOR guard', 'Game.Editor namespace', 'MenuItem attribute', 'SerializedObject + FindProperty + ApplyModifiedProperties', 'ScriptableObject.CreateInstance + AssetDatabase.CreateAsset']
test_patterns: ['no automated tests — Editor tooling only; verified by running the menu item and inspecting the scene']
---

# Tech-Spec: Generic Enemy KilledFact Generator

**Created:** 2026-04-11

## Overview

### Problem Statement

Generic scene-placed enemies carry a `PersistentID` component but have no `KilledFact` SO assigned.
`PersistentID.Awake()` logs an error and returns early in that case — `WorldStateManager` never registers
the kill, so the enemy reappears on scene reload as if it was never killed.
Manually creating KilledFact assets per enemy is tedious and error-prone (proven by the 5 duplicate
`KilledFact_spider_1` assets already sitting in `Assets/_Game/Data/Enemies/Starting_town/generic/`
without being wired back to any component).

### Solution

A single Editor menu item (`Game/World/Generate Missing KilledFacts`) that:
1. Finds every `PersistentID` in the active scene whose `_killedFact` field is null.
2. For each, creates a `KilledFact` SO asset with a freshly generated GUID at
   `Assets/_Game/Data/Enemies/{SceneName}/Generic/KilledFact_{GOName}.asset`.
3. If an asset already exists at that path, reuses it (no new GUID) to avoid further duplicates.
4. Assigns the asset back to the component via `SerializedObject` + `FindProperty("_killedFact")`.
5. Marks the scene dirty and saves assets.
6. Logs a clear summary: N created, M reused, K skipped.

### Scope

**In Scope:**
- New file: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
- Scans active (loaded) scene only — no multi-scene batch, no prefab assets
- Creates `KilledFact` assets in `Assets/_Game/Data/Enemies/{SceneName}/Generic/`
- Folder created automatically if it does not exist
- Reuse-not-duplicate logic: if `KilledFact_{GOName}.asset` already exists → load and assign, don't create
- Auto-assigns via `SerializedObject` (private `[SerializeField]` field)
- Marks scene dirty so Unity prompts to save

**Out of Scope:**
- Cleanup of the 5 existing duplicate assets in `Starting_town/generic/` (manual task)
- Runtime-spawned enemies (no stable scene identity)
- Scanning prefab assets in the Project window
- Any runtime changes to `PersistentID.cs`
- Automated test — tool is verified manually by running and checking Inspector

---

## Context for Development

### Codebase Patterns

| Pattern | Detail |
|---------|--------|
| Editor script guard | Entire file wrapped in `#if UNITY_EDITOR … #endif` (no separate Editor asmdef) |
| Namespace | `Game.Editor` — matches `WireEquipmentVisuals.cs` |
| Menu path convention | `Game/Dev/…` exists; new tool goes under `Game/World/…` (world state domain) |
| Setting private `[SerializeField]` | `var so = new SerializedObject(component); so.FindProperty("_fieldName").objectReferenceValue = asset; so.ApplyModifiedProperties();` |
| KilledFact creation | `ScriptableObject.CreateInstance<KilledFact>().Init(System.Guid.NewGuid().ToString())` then `AssetDatabase.CreateAsset(asset, path)` |
| Folder creation | `System.IO.Directory.CreateDirectory(absolutePath)` then `AssetDatabase.Refresh()` before `CreateAsset` |
| Scene name | `UnityEngine.SceneManagement.SceneManager.GetActiveScene().name` — e.g. `StartingTown` |
| Marking scene dirty | `UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene)` |
| Logging | `GameLog.Info / Warn / Error` with a `[GenerateKilledFacts]` tag |

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/World/PersistentID.cs` | Component to scan; `_killedFact` is the private `[SerializeField]` to assign |
| `Assets/_Game/Scripts/Core/State/Facts/KilledFact.cs` | SO to instantiate; `Init(string guid)` sets Prefix + _guid |
| `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` | Exact Editor script pattern to follow |
| `Assets/_Game/Data/Enemies/` | Root of target asset folder tree |

### Technical Decisions

1. **Menu path**: `Game/World/Generate Missing KilledFacts` — not under `Game/Dev/` because this is a
   production asset-generation tool, not debug scaffolding.
2. **Scene name as folder name**: Use `activeScene.name` verbatim (e.g. `StartingTown`). The existing
   `Starting_town` folder is a legacy manual artefact and a separate cleanup task.
3. **Asset file name**: `KilledFact_{go.name}.asset`. If the GO name contains characters invalid in file
   paths (`:`, `/`, `\`, `?`, `*`), sanitize by replacing them with `_`.
4. **Reuse logic**: `AssetDatabase.LoadAssetAtPath<KilledFact>(path)` before creating. If non-null → reuse.
   This prevents the duplicate problem.
5. **GUID generation**: `System.Guid.NewGuid().ToString()` — same as KilledFact's own `[ContextMenu]` helper.
6. **Save flow**: `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()` at the end of the run, then
   `EditorSceneManager.MarkSceneDirty(activeScene)` so Unity prompts the user to save the scene.
7. **`KilledFact.Init()` requires `OnEnable` to have run**: `Init()` only sets `_guid`; `OnEnable` sets
   `Prefix`. Since `OnEnable` fires on `CreateInstance`, this is fine.

---

## Implementation Plan

### Tasks

- [ ] Task 1: Create `GenerateGenericKilledFacts.cs` Editor script
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: Create new file. Wrap entirely in `#if UNITY_EDITOR … #endif`. Use `namespace Game.Editor`. Add `[MenuItem("Game/World/Generate Missing KilledFacts")]` on a `public static void Run()` method.
  - Notes: Follow the exact structure of `WireEquipmentVisuals.cs` — same guard, same namespace, same static class pattern. No MonoBehaviour.

- [ ] Task 2: Implement scene scan
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: Inside `Run()`, call `Object.FindObjectsOfType<PersistentID>()` to get all PersistentID instances in the active scene. Use `UnityEngine.SceneManagement.SceneManager.GetActiveScene()` to get the active scene name.
  - Notes: `FindObjectsOfType` includes inactive objects — pass `true` as parameter so deactivated enemies are also processed.

- [ ] Task 3: Implement per-component asset creation logic
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: For each `PersistentID` found, use `SerializedObject` to check if `_killedFact` is already assigned. If non-null → skip (increment skipped counter). If null → proceed to asset creation.
    - Build asset path: `$"Assets/_Game/Data/Enemies/{sceneName}/Generic/KilledFact_{sanitizedName}.asset"` where `sanitizedName` replaces `/ \ : * ? " < > |` with `_`.
    - Check if asset exists: `AssetDatabase.LoadAssetAtPath<KilledFact>(assetPath)`.
    - If exists → reuse (increment reused counter). If not → `ScriptableObject.CreateInstance<KilledFact>().Init(System.Guid.NewGuid().ToString())`, then `AssetDatabase.CreateAsset(fact, assetPath)` (increment created counter).
    - Assign back: `so.FindProperty("_killedFact").objectReferenceValue = fact; so.ApplyModifiedProperties();`
  - Notes: `KilledFact.Init(string guid)` sets both `_guid` and `Prefix` — call it before `CreateAsset`. `OnEnable` also sets Prefix, which fires on `CreateInstance`.

- [ ] Task 4: Implement folder creation
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: Before the per-component loop, compute `folderPath = $"Assets/_Game/Data/Enemies/{sceneName}/Generic"`. Use `AssetDatabase.IsValidFolder(folderPath)` to check existence. If not valid, call `AssetDatabase.GUIDFromAssetPath` won't help — instead use `System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + folderPath)` then `AssetDatabase.Refresh()` to register it.
  - Notes: `AssetDatabase.CreateFolder` can also be used recursively. The `Application.dataPath + "/../"` pattern converts to the project root so `System.IO.Directory.CreateDirectory` works. Alternatively use `AssetDatabase.CreateFolder` for each missing level.

- [ ] Task 5: Implement save and summary log
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: After the loop, call `AssetDatabase.SaveAssets()` and `AssetDatabase.Refresh()`. Mark the scene dirty: `EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene())`. Log a summary with `GameLog.Info(TAG, $"Done — {created} created, {reused} reused, {skipped} skipped (already assigned).")`.
  - Notes: If `created == 0 && reused == 0`, also log a `GameLog.Info` that no unassigned PersistentIDs were found, so the user knows the tool ran successfully and there was nothing to do.

### Acceptance Criteria

- [ ] AC 1: Given a scene with a `PersistentID` component that has no `KilledFact` assigned, when the user runs `Game/World/Generate Missing KilledFacts`, then a `KilledFact` SO asset is created at `Assets/_Game/Data/Enemies/{SceneName}/Generic/KilledFact_{GOName}.asset` with a non-empty GUID, and the `PersistentID` component's `_killedFact` field shows the assigned asset in the Inspector.

- [ ] AC 2: Given the tool was already run once and KilledFacts are assigned, when the user runs the tool a second time, then the console logs "0 created, N reused, M skipped" — no new asset files are created and no duplicate names appear in the folder.

- [ ] AC 3: Given a `PersistentID` that already has a `KilledFact` assigned (a narrative enemy), when the tool runs, then that component is skipped and its existing assignment is unchanged.

- [ ] AC 4: Given the target folder `Assets/_Game/Data/Enemies/{SceneName}/Generic/` does not yet exist, when the tool runs, then the folder is auto-created, the asset is saved inside it, and no error is thrown.

- [ ] AC 5: Given a GameObject name containing invalid file-path characters (e.g. `Spider (1)`), when the tool creates the asset, then the filename is sanitized (e.g. `KilledFact_Spider _1_.asset`) and the asset is saved without error.

- [ ] AC 6: Given the tool has run and the scene is in Play mode, when an enemy with a newly assigned `KilledFact` is killed and the scene is reloaded, then the enemy does not reappear (WorldStateManager correctly tracks the kill via the generated fact).

---

## Additional Context

### Dependencies

- No new package dependencies — pure Editor scripting with Unity built-ins.
- `KilledFact` and `PersistentID` must be compiled without errors before the tool runs.

### Testing Strategy

Manual verification:
1. Open `StartingTown` scene (or any scene with generic enemies).
2. Ensure at least one `PersistentID` in the scene has no `KilledFact` assigned.
3. Run `Game/World/Generate Missing KilledFacts`.
4. Verify: new `.asset` files appear in `Assets/_Game/Data/Enemies/{SceneName}/Generic/`.
5. Verify: `PersistentID` component in the Inspector now shows the assigned KilledFact.
6. Verify: running the tool again logs "0 created, N reused" with no duplicates.
7. Enter Play mode — no `[WorldState]` error logs for those enemies.

### Notes

- The 5 existing `KilledFact_spider_1` duplicates in `Starting_town/generic/` need manual cleanup.
  Keep one, delete the rest, then run the tool to assign the surviving asset. Or delete all 5 and
  let the tool regenerate clean ones.
- Story 5-5 (`no-enemy-respawn`) in `sprint-status.yaml` is the consumer of this tooling.
  Once generic enemies have KilledFacts, `PersistentID.Awake()` will correctly deactivate them
  after scene reload — no-respawn works without further code changes.
