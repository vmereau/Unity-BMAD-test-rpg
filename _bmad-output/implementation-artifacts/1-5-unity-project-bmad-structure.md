# Story 1.5: Unity Project BMAD Structure

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want the Unity project to be fully structured according to the BMAD workflow architecture,
so that all future stories have consistent folder locations, assembly references, and foundational C# infrastructure to build on.

## Acceptance Criteria

1. All folder paths from the architecture doc exist under `Assets/_Game/` — including Art/Environment, Art/UI, Art/VFX, Audio/Music, Audio/SFX.
2. A `Game.asmdef` assembly definition exists at `Assets/_Game/` — all scripts under `_Game/` compile into the `Game` assembly.
3. `Tests/EditMode/Tests.EditMode.asmdef` references the `Game` assembly so edit-mode tests can import and test game logic.
4. `Assets/_Game/Scripts/Core/GameConstants.cs` exists with the `GameConstants` static class and an empty `Game.Core` namespace wrapper.
5. `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` exists with a correct, functional generic typed event SO class (`GameEventSO<T>`) — this is the foundational cross-system communication mechanism for all future epics.
6. `Core.unity` scene contains named empty GameObjects serving as stubs for global managers: `WorldStateManager`, `GameEventBus`, `SaveSystem`, `SceneLoader`, `DayNightController`, `AudioManager`, and a `UI` root.
7. No console errors in Play Mode or on entering Play Mode in `TestScene.unity`.
8. Existing Stories 1.1–1.4 behavior is unchanged — all regressions pass (movement, camera, animations still work).

## Tasks / Subtasks

- [x] Task 1: Complete folder structure (AC: 1)
  - [x] 1.1 Create `Assets/_Game/Art/Environment/` — add `.gitkeep` placeholder
  - [x] 1.2 Create `Assets/_Game/Art/UI/` — add `.gitkeep` placeholder
  - [x] 1.3 Create `Assets/_Game/Art/VFX/` — add `.gitkeep` placeholder
  - [x] 1.4 Create `Assets/_Game/Audio/Music/` — add `.gitkeep` placeholder
  - [x] 1.5 Create `Assets/_Game/Audio/SFX/` — add `.gitkeep` placeholder
  - [x] 1.6 Verify Unity creates `.meta` files for all new directories (refresh Asset Database)

- [x] Task 2: Create Game assembly definition (AC: 2)
  - [x] 2.1 Create `Assets/_Game/Game.asmdef` with name `"Game"`, `autoReferenced: true`
  - [x] 2.2 Confirm all existing scripts compile without errors after adding the asmdef
        (Scripts now live in the `Game` assembly instead of the default Assembly-CSharp)

- [x] Task 3: Update EditMode test assembly reference (AC: 3)
  - [x] 3.1 Edit `Assets/Tests/EditMode/Tests.EditMode.asmdef` — add `"Game"` to the `references` array
  - [x] 3.2 Confirm no compilation errors

- [x] Task 4: Create GameConstants.cs (AC: 4)
  - [x] 4.1 Create `Assets/_Game/Scripts/Core/GameConstants.cs`
        See Dev Notes for full implementation — includes `SAVE_FILE_NAME`, `CRASH_LOG_FILE_NAME`, `MAX_EQUIPMENT_SLOTS`

- [x] Task 5: Create GameEventSO.cs (AC: 5) ⚡ CRITICAL
  - [x] 5.1 Create `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs`
        See Dev Notes for full implementation — generic typed SO event channel
  - [x] 5.2 Verify the script compiles without errors
  - [x] 5.3 Confirm `[CreateAssetMenu]` attribute is present so event assets can be created in the Unity Editor

- [x] Task 6: Set up Core.unity scene stubs (AC: 6)
  - [x] 6.1 Open `Assets/_Game/Scenes/Core.unity` in Unity Editor
  - [x] 6.2 Create empty GameObjects with exact names: `WorldStateManager`, `GameEventBus`, `SaveSystem`,
        `SceneLoader`, `DayNightController`, `AudioManager`, `UI`
  - [x] 6.3 Save the scene

- [x] Task 7: Validate (AC: 7–8)
  - [x] 7.1 Enter Play Mode in `TestScene.unity` — no console errors
  - [x] 7.2 Verify player movement, camera, and animations still work (regressions 1.1–1.4)
  - [x] 7.3 Check Unity Console for any assembly or compilation errors from the asmdef change

## Dev Notes

### What Changes — Precise Scope

**New files:**
- `Assets/_Game/Game.asmdef` — game assembly definition
- `Assets/_Game/Scripts/Core/GameConstants.cs` — project constants
- `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` — event channel SO base class
- `Assets/_Game/Art/Environment/.gitkeep`
- `Assets/_Game/Art/UI/.gitkeep`
- `Assets/_Game/Art/VFX/.gitkeep`
- `Assets/_Game/Audio/Music/.gitkeep`
- `Assets/_Game/Audio/SFX/.gitkeep`

**Modified files:**
- `Assets/Tests/EditMode/Tests.EditMode.asmdef` — add `"Game"` reference
- `Assets/_Game/Scenes/Core.unity` — add manager stub GameObjects

**Unchanged (DO NOT TOUCH):**
- `Assets/_Game/Scripts/Player/PlayerController.cs`
- `Assets/_Game/Scripts/Player/CameraController.cs`
- `Assets/_Game/Scripts/Player/PlayerAnimator.cs`
- `Assets/_Game/Scripts/Core/GameLog.cs`
- `Assets/_Game/ScriptableObjects/Config/PlayerConfigSO.cs`
- `Assets/_Game/Data/Config/PlayerConfig.asset`
- `Assets/_Game/Scenes/TestScene.unity`

---

### Game.asmdef — Content

```json
{
    "name": "Game",
    "references": [
        "Unity.InputSystem"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Why `autoReferenced: true`:** All assembly definitions in the project that don't specify a references list will be able to use this assembly automatically. The test assemblies still need explicit references since they use `overrideReferences: true`.

**Critical:** After creating `Game.asmdef`, Unity will recompile. If any existing script has a namespace or class issue it was silently ignoring before, it will now surface. Watch the console carefully.

---

### Updated Tests.EditMode.asmdef — Content

```json
{
    "name": "Tests.EditMode",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Game"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

---

### GameConstants.cs — Complete Implementation

```csharp
namespace Game.Core
{
    /// <summary>
    /// Project-wide compile-time constants.
    /// Tunable gameplay values belong in config SOs, not here.
    /// This is for structural constants that never change based on design.
    /// </summary>
    public static class GameConstants
    {
        // --- Save / Persistence ---
        public const string SAVE_FILE_NAME = "savegame.json";
        public const string CRASH_LOG_FILE_NAME = "crash_log.txt";

        // --- Equipment ---
        public const int MAX_EQUIPMENT_RING_SLOTS = 2;
        public const int MAX_EQUIPMENT_SLOTS = 9; // head, chest, legs, boots, gloves, weapon, shield, ring×2, necklace

        // --- Progression ---
        public const int MAX_CHARACTER_LEVEL = 50;

        // --- World ---
        public const string CORE_SCENE_NAME = "Core";
        public const string STARTING_TOWN_SCENE_NAME = "StartingTown";
        public const string WILDERNESS_SCENE_NAME = "Wilderness";
        public const string DUNGEON_SCENE_NAME = "Dungeon";
        public const string MAIN_MENU_SCENE_NAME = "MainMenu";
    }
}
```

---

### GameEventSO.cs — Complete Implementation

This is the most important file in this story. Every future cross-system event channel in the game (OnEntityKilled, OnQuestStateChanged, OnActAdvanced, etc.) is an instance of this class.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Typed ScriptableObject event channel for decoupled cross-system communication.
    ///
    /// USAGE — Raising an event:
    ///   [SerializeField] private GameEventSO<string> _onEntityKilled;
    ///   _onEntityKilled.Raise(persistentID);
    ///
    /// USAGE — Listening to an event (always OnEnable/OnDisable):
    ///   private void OnEnable() => _onEntityKilled.AddListener(HandleEntityKilled);
    ///   private void OnDisable() => _onEntityKilled.RemoveListener(HandleEntityKilled);
    ///   private void HandleEntityKilled(string id) { ... }
    ///
    /// To create a concrete event asset: right-click in Project → Create → Game/Events → [EventName]
    /// Each event type needs its own CreateAssetMenu subclass (see GameEventSO_String example below).
    /// </summary>
    public abstract class GameEventSOBase : ScriptableObject { }

    public class GameEventSO<T> : GameEventSOBase
    {
        private const string TAG = "[Event]";

        private readonly List<Action<T>> _listeners = new List<Action<T>>();

        public void Raise(T payload)
        {
            // Iterate in reverse so listeners can unsubscribe safely during dispatch
            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                _listeners[i]?.Invoke(payload);
            }
        }

        public void AddListener(Action<T> listener)
        {
            if (!_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public void RemoveListener(Action<T> listener)
        {
            _listeners.Remove(listener);
        }
    }

    // --- Concrete event types (add one per payload type needed) ---

    /// <summary>String event channel — used for OnEntityKilled, OnNPCDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/String Event", fileName = "NewStringEvent")]
    public class GameEventSO_String : GameEventSO<string> { }

    /// <summary>Int event channel — used for OnLevelUp, OnActAdvanced, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Int Event", fileName = "NewIntEvent")]
    public class GameEventSO_Int : GameEventSO<int> { }

    /// <summary>Bool event channel — used for OnDayNightChanged, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Bool Event", fileName = "NewBoolEvent")]
    public class GameEventSO_Bool : GameEventSO<bool> { }

    /// <summary>Void/signal event channel — used for OnPlayerDied, etc.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Void Event", fileName = "NewVoidEvent")]
    public class GameEventSO_Void : GameEventSO<bool> { }
}
```

**Design notes:**
- `GameEventSO<T>` is abstract — you never create a raw `GameEventSO<T>` asset directly. You always create a concrete subclass asset (e.g. `GameEventSO_String`) via the `CreateAssetMenu`.
- The abstract `GameEventSOBase` class exists so future editor tools can `FindAssets<GameEventSOBase>` to list all event channels in the project.
- `OnPlayerDied` can use `GameEventSO_Void` where the bool payload is unused (convention: always pass `true`).
- **Do NOT** make `GameEventSO<T>` inherit from `ScriptableObject` directly with the generic parameter — Unity cannot serialize generic ScriptableObjects. The concrete subclass pattern is the correct Unity approach.

---

### Folder Structure Delta

Architecture spec requires these folders that don't yet exist:

```
Assets/_Game/Art/
├── Environment/   ← MISSING
├── UI/            ← MISSING
└── VFX/           ← MISSING

Assets/_Game/Audio/
├── Music/         ← MISSING
└── SFX/           ← MISSING
```

Already present (created during Stories 1.1–1.4 or at project init):
```
Assets/_Game/Art/Characters/ ✅
Assets/_Game/Art/Materials/  ✅
Assets/_Game/Data/Config/    ✅
Assets/_Game/Data/Events/    ✅
Assets/_Game/Data/Items/     ✅
Assets/_Game/Data/NPCs/      ✅
Assets/_Game/Data/Quests/    ✅
Assets/_Game/Data/Skills/    ✅
Assets/_Game/Prefabs/Enemies/ ✅
Assets/_Game/Prefabs/NPCs/   ✅
Assets/_Game/Prefabs/Player/ ✅
Assets/_Game/Prefabs/UI/     ✅
Assets/_Game/Prefabs/VFX/    ✅
Assets/_Game/Prefabs/World/  ✅
Assets/_Game/Scripts/Core/   ✅
Assets/_Game/Scripts/AI/     ✅
Assets/_Game/Scripts/Audio/  ✅
Assets/_Game/Scripts/Combat/ ✅
... (all 15 script subsystems present) ✅
Assets/_Game/ScriptableObjects/Config/ ✅
Assets/_Game/ScriptableObjects/Events/ ✅ (folder, but GameEventSO.cs missing)
Assets/_Game/ScriptableObjects/Items/  ✅
Assets/_Game/ScriptableObjects/NPC/    ✅
Assets/_Game/ScriptableObjects/Quest/  ✅
Assets/_Game/ScriptableObjects/Skills/ ✅
Assets/Tests/EditMode/ ✅
Assets/Tests/PlayMode/ ✅
Assets/ThirdParty/     ✅
```

**Note:** `Assets/_Game/ScriptableObjects/Skills/` directory exists but per the architecture spec the class name is `SkillSO.cs` — do NOT create it in this story; it belongs to Epic 3.

---

### Core.unity Scene — Stub Setup

Per architecture Decision 4, `Core.unity` must always be loaded and contains all global managers. For this story, create named **empty GameObjects** as placeholders — no scripts attached yet. The scripts will be implemented in their respective epics.

Required stub GameObjects:
- `WorldStateManager` — Epic 2 (permanent kill tracking)
- `GameEventBus` — Epic 2 (but the event SO pattern is now available via GameEventSO.cs)
- `SaveSystem` — Epic 8
- `SceneLoader` — Epic 4
- `DayNightController` — Epic 4
- `AudioManager` — Epic 8
- `UI` — Epic 8 (root canvas)

**Note:** `Core.unity` should NOT be in the Build Settings' scene list yet as an additive scene since the region scenes don't exist — the current development uses `TestScene.unity` only. However, the scene should be buildable and contain the stubs.

---

### Assembly Definition Impact on Existing Scripts

**IMPORTANT:** After adding `Game.asmdef`, all scripts under `Assets/_Game/` move from the default `Assembly-CSharp` to the `Game` assembly. This means:

1. `InputSystem_Actions.cs` (at `Assets/InputSystem_Actions.cs`) remains in `Assembly-CSharp` — the `Game` assembly can still reference it because `autoReferenced: true` in `Game.asmdef` allows both assemblies to coexist without explicit cross-reference issues. `InputSystem_Actions` is in `Assembly-CSharp` which `Game` implicitly sees.

2. **If compilation errors appear** after adding `Game.asmdef`, the most common cause is that `InputSystem_Actions` is now in a separate assembly from `Game` and requires an explicit assembly reference. Fix: add `"Unity.InputSystem"` or `"Assembly-CSharp"` to `Game.asmdef`'s references if needed.

3. **Correct approach if `InputSystem_Actions` reference fails:** Add `"com.unity.inputsystem"` to `Game.asmdef` references, not `"Assembly-CSharp"` (referencing Assembly-CSharp from a named assembly is an anti-pattern).

---

### Architecture Compliance

| Rule | Applied |
|---|---|
| All custom code under `Assets/_Game/` | ✅ `Game.asmdef` at `_Game/` root covers all sub-scripts |
| `GameEventSO<T>` typed SO channels for cross-system communication | ✅ Implemented in this story |
| `GameLog` wrapper — never `Debug.Log` | ✅ `GameConstants.cs` has no logging (constants only) |
| No magic numbers in game logic | ✅ Constants in `GameConstants.cs` |
| `ScriptableObjects/Events/` for event SO types | ✅ `GameEventSO.cs` placed in correct folder |
| Tests can reference `Game` assembly | ✅ `Tests.EditMode.asmdef` updated |

### Project Structure Notes

After this story the project matches the architecture spec exactly. Key path alignments:

- Event SO assets: `Assets/_Game/Data/Events/` (instances go here, class definition is in `ScriptableObjects/Events/`)
- Future event asset creation: right-click in `Data/Events/` → Create → Game/Events → [String/Int/Bool/Void Event]
- The `Scripts/Core/` folder now has: `GameLog.cs` ✅, `GameConstants.cs` ← NEW
- The `ScriptableObjects/Events/` folder now has: `GameEventSO.cs` ← NEW

### References

- Architecture directory structure: [Source: game-architecture.md#Directory Structure]
- Assembly definition requirement for tests: [Source: project-context.md#Testing Rules — Test Organization]
- `GameEventSO<T>` pattern: [Source: game-architecture.md#Event System]
- `GameEventSO<T>` usage example: [Source: game-architecture.md#Consistency Rules]
- Cross-system event channels: [Source: game-architecture.md#Decision 1: Game Architecture Pattern]
- Core scene contents: [Source: game-architecture.md#Decision 4: Scene Loading Strategy]
- `GameConstants` as static class: [Source: game-architecture.md#Configuration Management]
- No magic numbers rule: [Source: project-context.md#Config & Data Anti-Patterns]
- `autoReferenced` asmdef pattern: [Source: project-context.md#Code Organization Rules]
- Previous story learnings: [Source: _bmad-output/implementation-artifacts/1-4-basic-idle-walk-run-animations.md]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Task 2.2: After adding `Game.asmdef`, `InputSystem_Actions.cs` could not be resolved because it was in `Assembly-CSharp` (root Assets/). Named assemblies cannot reference Assembly-CSharp. Fix: moved `InputSystem_Actions.cs` and `InputSystem_Actions.inputactions` into `Assets/_Game/` so they compile into the `Game` assembly. Added `"Unity.InputSystem"` to `Game.asmdef` references for the `using UnityEngine.InputSystem` namespace.

### Completion Notes List

- All 7 tasks and all subtasks completed and validated
- `Game.asmdef` created with `autoReferenced: true` and `Unity.InputSystem` reference
- `InputSystem_Actions.cs` and `.inputactions` moved from root `Assets/` into `Assets/_Game/` to resolve cross-assembly reference issue
- `GameConstants.cs` and `GameEventSO.cs` created per spec
- `Tests.EditMode.asmdef` updated with `"Game"` reference
- Art/Audio subdirectories created with `.gitkeep` placeholders
- `Core.unity` scene updated: added `SceneLoader` stub, renamed `UI Canvas` → `UI`
- Zero compile errors and zero Play Mode errors confirmed

### File List

- `Assets/_Game/Game.asmdef` — NEW: Game assembly definition
- `Assets/_Game/Game.asmdef.meta` — NEW: generated by Unity
- `Assets/_Game/InputSystem_Actions.cs` — MOVED from `Assets/InputSystem_Actions.cs`
- `Assets/_Game/InputSystem_Actions.cs.meta` — MOVED from `Assets/InputSystem_Actions.cs.meta`
- `Assets/_Game/InputSystem_Actions.inputactions` — MOVED from `Assets/InputSystem_Actions.inputactions`
- `Assets/_Game/InputSystem_Actions.inputactions.meta` — MOVED from `Assets/InputSystem_Actions.inputactions.meta`
- `Assets/_Game/Scripts/Core/GameConstants.cs` — NEW
- `Assets/_Game/Scripts/Core/GameConstants.cs.meta` — NEW: generated by Unity
- `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` — NEW
- `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs.meta` — NEW: generated by Unity
- `Assets/_Game/Art/Environment/.gitkeep` — NEW
- `Assets/_Game/Art/Environment.meta` — NEW: generated by Unity
- `Assets/_Game/Art/UI/.gitkeep` — NEW
- `Assets/_Game/Art/UI.meta` — NEW: generated by Unity
- `Assets/_Game/Art/VFX/.gitkeep` — NEW
- `Assets/_Game/Art/VFX.meta` — NEW: generated by Unity
- `Assets/_Game/Audio/Music/.gitkeep` — NEW
- `Assets/_Game/Audio/Music.meta` — NEW: generated by Unity
- `Assets/_Game/Audio/SFX/.gitkeep` — NEW
- `Assets/_Game/Audio/SFX.meta` — NEW: generated by Unity
- `Assets/Tests/EditMode/Tests.EditMode.asmdef` — MODIFIED: added `"Game"` reference
- `Assets/_Game/Scenes/Core.unity` — MODIFIED: renamed `UI Canvas` → `UI`, added `SceneLoader` stub

## Change Log

- 2026-03-03: Story implemented — created `Game.asmdef`, `GameConstants.cs`, `GameEventSO.cs`; moved `InputSystem_Actions` into `_Game/`; added folder stubs; updated `Core.unity` with all 7 manager stubs; `Tests.EditMode.asmdef` updated with `Game` reference. Zero compile/runtime errors.
- 2026-03-04: Code review fixes — added MonoImporter block to `GameConstants.cs.meta` and `GameEventSO.cs.meta`; marked `GameEventSO<T>` as `abstract`; removed unused `TAG` constant from `GameEventSO.cs`; corrected `Game.asmdef` dev notes references and removed false `.gitkeep.meta` entries from File List.
