# Story 5.1: Starting Town Exploration

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a player,
I want to explore a starting town with NPC characters present,
so that I feel immersed in a living world from the first moments of the game.

## Acceptance Criteria

1. **`StartingTown.unity`** scene created at `Assets/_Game/Scenes/StartingTown.unity`:
   - Loadable additively alongside `Core.unity` (both in Build Settings)
   - Contains a ground/terrain for the town area (at minimum a flat ground plane with a box collider)
   - Contains a Directional Light for ambient daylight (sun direction roughly 45° elevation)
   - Contains 3–5 placeholder building structures (Unity primitive cubes/blocks with colliders) arranged as a simple town layout
   - Contains an empty `PlayerSpawnPoint` GameObject at the desired player start position

2. **`SceneLoader.cs`** created at `Assets/_Game/Scripts/Core/SceneLoader.cs`:
   - Attached to the `SceneLoader` stub GameObject in `Core.unity`
   - Namespace: `Game.Core`
   - On `Start()`: checks if StartingTown scene is not yet loaded → calls `LoadRegion("StartingTown")`
   - `public void LoadRegion(string sceneName)` — loads additively via `SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive)` as a coroutine
   - `public void UnloadRegion(string sceneName)` — unloads via `SceneManager.UnloadSceneAsync(sceneName)` as a coroutine (stub for future use)
   - Null-guard: if `sceneName` is empty, log `GameLog.Error` and return
   - TAG: `[Scene]`

3. **`NPCState` enum and `NPCDataSO.cs`** created:
   - `NPCState` enum at `Assets/_Game/ScriptableObjects/NPC/NPCState.cs` — namespace `Game.NPC` — values: `Working`, `Sleeping`, `Patrolling`, `AtTavern`
   - `NPCDataSO.cs` at `Assets/_Game/ScriptableObjects/NPC/NPCDataSO.cs` — namespace `Game.NPC`
   - `[CreateAssetMenu(menuName = "Game/NPC/NPC Data", fileName = "NPC_")]`
   - Fields: `public string npcName`, `public NPCState dayState = NPCState.Working`, `public NPCState nightState = NPCState.Sleeping`, `public float walkSpeed = 2f`

4. **`NPCPresence.cs`** created at `Assets/_Game/Scripts/AI/NPCPresence.cs`:
   - Namespace: `Game.AI`
   - `[SerializeField] private NPCDataSO _data`
   - Implements `IInteractable` (from `Game.World`)
   - `string IInteractable.InteractPrompt => _data != null ? _data.npcName : "NPC"`
   - `void IInteractable.Interact(GameObject interactor)` → logs `GameLog.Info(TAG, $"{_data.npcName} is busy.")` (placeholder until Epic 6 dialogue)
   - `Awake()` null-guard: if `_data == null` → `GameLog.Error(TAG, ...)` + `enabled = false`
   - TAG: `[NPC]`

5. **5 NPC data assets** created at `Assets/_Game/Data/NPCs/`:
   - `NPC_Innkeeper.asset` — `npcName = "Innkeeper"`, dayState = Working, nightState = AtTavern
   - `NPC_Blacksmith.asset` — `npcName = "Blacksmith"`, dayState = Working, nightState = Sleeping
   - `NPC_Merchant.asset` — `npcName = "Merchant"`, dayState = Working, nightState = Sleeping
   - `NPC_Guard.asset` — `npcName = "Guard"`, dayState = Patrolling, nightState = Patrolling
   - `NPC_Elder.asset` — `npcName = "Elder"`, dayState = Working, nightState = Sleeping

6. **`NPC_Base.prefab`** created at `Assets/_Game/Prefabs/NPCs/NPC_Base.prefab`:
   - Root GO: Layer 8 (Interactable), tag = Untagged
   - Components on root: `NPCPresence`, `PersistentID`, `CapsuleCollider` (height 2.0, radius 0.4, not trigger)
   - Child GO named `Visual`: Unity Capsule mesh renderer with URP Lit material (gray placeholder)
   - `_data` field of `NPCPresence` left unassigned in the prefab (assigned per instance in scene)

7. **5 NPC instances placed in `StartingTown.unity`**:
   - Each is a variant or instance of `NPC_Base.prefab`
   - Each has its `NPCPresence._data` assigned to its respective `NPCDataSO` asset
   - Each has a unique `PersistentID` GUID following the naming pattern `StartingTown_NPC_[Name]`:
     - `StartingTown_NPC_Innkeeper`, `StartingTown_NPC_Blacksmith`, `StartingTown_NPC_Merchant`, `StartingTown_NPC_Guard`, `StartingTown_NPC_Elder`
   - NPCs are spread across the town area at reasonable distances from each other

8. **Player loads into the StartingTown and can walk freely**:
   - Player spawns at (or near) `PlayerSpawnPoint`
   - Player does not clip through the ground or NPC colliders
   - Navigation throughout the town is unobstructed (no invisible collision walls)

9. **Interaction prompt shows NPC name when looking at an NPC**:
   - `InteractionSystem` raycasts against Layer 8 correctly detects NPCs
   - Looking at an NPC shows its name as the interaction prompt
   - Pressing E near an NPC logs a placeholder line in GameLog (no crash, no error)

10. **No regressions**:
    - `TestScene.unity` still works (player controller, combat, inventory all functional)
    - `Core.unity` managers still load correctly
    - All Edit Mode tests pass (currently 137 tests)

## Tasks / Subtasks

- [ ] Task 1: Create `NPCState` enum and `NPCDataSO.cs` (AC: 3)
  - [ ] 1.1 Create `Assets/_Game/ScriptableObjects/NPC/NPCState.cs` with enum values: Working, Sleeping, Patrolling, AtTavern
  - [ ] 1.2 Create `Assets/_Game/ScriptableObjects/NPC/NPCDataSO.cs` with npcName, dayState, nightState, walkSpeed fields
  - [ ] 1.3 Confirm compilation with no errors

- [ ] Task 2: Create 5 NPC data assets (AC: 5)
  - [ ] 2.1 Create `Assets/_Game/Data/NPCs/NPC_Innkeeper.asset` (NPCDataSO)
  - [ ] 2.2 Create `Assets/_Game/Data/NPCs/NPC_Blacksmith.asset`
  - [ ] 2.3 Create `Assets/_Game/Data/NPCs/NPC_Merchant.asset`
  - [ ] 2.4 Create `Assets/_Game/Data/NPCs/NPC_Guard.asset` (dayState = Patrolling, nightState = Patrolling)
  - [ ] 2.5 Create `Assets/_Game/Data/NPCs/NPC_Elder.asset`

- [ ] Task 3: Create `NPCPresence.cs` (AC: 4)
  - [ ] 3.1 Create `Assets/_Game/Scripts/AI/NPCPresence.cs` implementing IInteractable
  - [ ] 3.2 Add Awake null-guard for `_data`, InteractPrompt property, Interact() placeholder
  - [ ] 3.3 Confirm compilation with no errors

- [ ] Task 4: Create `NPC_Base.prefab` (AC: 6)
  - [ ] 4.1 Create prefab at `Assets/_Game/Prefabs/NPCs/NPC_Base.prefab`
  - [ ] 4.2 Set root GO Layer to 8 (Interactable)
  - [ ] 4.3 Add `NPCPresence` component (leave `_data` unassigned)
  - [ ] 4.4 Add `PersistentID` component
  - [ ] 4.5 Add `CapsuleCollider` (height 2.0, radius 0.4)
  - [ ] 4.6 Add child `Visual` GO with Capsule mesh + URP Lit material

- [ ] Task 5: Create `SceneLoader.cs` (AC: 2)
  - [ ] 5.1 Create `Assets/_Game/Scripts/Core/SceneLoader.cs`
  - [ ] 5.2 Implement `LoadRegion()` and `UnloadRegion()` as coroutines
  - [ ] 5.3 Implement auto-load of StartingTown in Start()
  - [ ] 5.4 Attach script to `SceneLoader` stub in `Core.unity`
  - [ ] 5.5 Add `StartingTown` and `Core` to Build Settings (if not already added)

- [ ] Task 6: Create `StartingTown.unity` scene (AC: 1)
  - [ ] 6.1 Create scene at `Assets/_Game/Scenes/StartingTown.unity`
  - [ ] 6.2 Add ground plane (Plane or Cube, scale appropriately) with Collider
  - [ ] 6.3 Add Directional Light (sun at ~45° elevation, warm white)
  - [ ] 6.4 Add 3–5 building proxies (stretched cubes) arranged as simple town outline
  - [ ] 6.5 Add `PlayerSpawnPoint` empty GO at a walkable location in the town center
  - [ ] 6.6 Add `StartingTown` to Build Settings scene list

- [ ] Task 7: Place 5 NPC instances in StartingTown (AC: 7)
  - [ ] 7.1 Instantiate 5 NPC GameObjects from `NPC_Base.prefab`
  - [ ] 7.2 Assign `NPCPresence._data` for each NPC to its respective data asset
  - [ ] 7.3 Assign unique PersistentID GUIDs per naming convention
  - [ ] 7.4 Position NPCs at distinct, spread-out locations in the town

- [ ] Task 8: Player spawn and validation (AC: 8, 9, 10)
  - [ ] 8.1 Configure player to spawn at `PlayerSpawnPoint` position on scene load
  - [ ] 8.2 Walk through town — verify no collision issues or invisible walls
  - [ ] 8.3 Look at each NPC — verify interaction prompt shows NPC name
  - [ ] 8.4 Press E near NPC — verify GameLog shows placeholder line, no errors
  - [ ] 8.5 Reload TestScene — confirm no regressions to player/combat/inventory
  - [ ] 8.6 Run all Edit Mode tests — confirm 137/137 pass

## Dev Notes

### Architecture Overview

Epic 5 is the World & Exploration epic. It builds the physical game world that all other systems inhabit. Story 5.1 creates the **minimum viable starting town**: a walkable scene with named NPC characters who can be looked at but not yet talked to (dialogue is Epic 6). Subsequent stories add schedules (5.4), day/night cycle (5.6), and enemies/dungeon (5.2, 5.3).

### CRITICAL: Scene Loading Architecture

**How Unity scenes work in this project:**
- `Core.unity` is ALWAYS loaded first — it hosts all singletons (`WorldStateManager`, `SceneLoader`, `GameEventBus`, etc.)
- Region scenes (`StartingTown`, `Wilderness`, `Dungeon`) load **additively** alongside Core
- `SceneLoader.cs` is attached to the `SceneLoader` stub GameObject in `Core.unity`
- The stub GO already exists in `Core.unity` — just attach the script, do NOT create a new GO

**SceneLoader auto-boot pattern:**
```csharp
private void Start()
{
    // Check if region already loaded (prevents double-load in Editor when entering play mode with StartingTown open)
    if (!SceneManager.GetSceneByName("StartingTown").isLoaded)
        StartCoroutine(LoadRegionCoroutine("StartingTown"));
}
```

**Build Settings requirement:**
- Both `Core.unity` (index 0) and `StartingTown.unity` (index 1) must be in File → Build Settings → Scenes In Build
- Without this, `LoadSceneAsync("StartingTown")` throws at runtime ("Scene could not be loaded because it has not been added to the build settings")

### CRITICAL: NPC Layer Requirement

`InteractionSystem` raycasts **only against Layer 8 (Interactable)**. NPCs must be on Layer 8 or they will never register an interaction prompt.

- Set the NPC root GameObject layer to 8 in the prefab
- Verify the layer is actually named "Interactable" in Project Settings → Physics → Layer Collision Matrix
- The `PersistentID` pattern (for future NPC death tracking) uses GUID strings — assign them now for all 5 NPCs

### CRITICAL: IInteractable Contract

`InteractionSystem` uses `GetComponentInParent<IInteractable>()` on whatever collider is hit. Therefore:
- The `CapsuleCollider` can be on the root GO (Layer 8) and `NPCPresence` (implementing IInteractable) is also on root — this is correct
- Do NOT put the collider only on a child GO without also putting it on Layer 8

`IInteractable` interface is in `Assets/_Game/Scripts/World/IInteractable.cs`, namespace `Game.World`:
```csharp
public interface IInteractable
{
    string InteractPrompt { get; }
    void Interact(GameObject interactor);
}
```

Implement `NPCPresence.cs` like this:
```csharp
using Game.Core;
using Game.World;
using UnityEngine;

namespace Game.AI
{
    public class NPCPresence : MonoBehaviour, IInteractable
    {
        private const string TAG = "[NPC]";

        [SerializeField] private NPCDataSO _data;

        public string InteractPrompt => _data != null ? _data.npcName : "NPC";

        private void Awake()
        {
            if (_data == null)
            {
                GameLog.Error(TAG, $"NPCPresence on {gameObject.name} has no NPCDataSO assigned");
                enabled = false;
            }
        }

        public void Interact(GameObject interactor)
        {
            GameLog.Info(TAG, $"{_data.npcName} is busy."); // Placeholder — Epic 6 adds dialogue
        }
    }
}
```

### CRITICAL: NPCDataSO Namespace

`NPCDataSO` must be in namespace `Game.NPC` to be consistent with existing `TrainerSO` (already in `Game.NPC`). `NPCState` enum must be in the same namespace so both `NPCDataSO` and the future `NPCScheduler` (story 5.4) can share it without circular dependencies.

### CRITICAL: PersistentID on NPCs

Every NPC in the scene **must** have a `PersistentID` component with a unique GUID. This is required by the architecture for future NPC death → quest closure cascade (Epic 6). Without it, killing an NPC won't propagate correctly later.

GUID naming convention: `Region_Type_Name` → examples:
- `StartingTown_NPC_Innkeeper`
- `StartingTown_NPC_Blacksmith`
- `StartingTown_NPC_Merchant`
- `StartingTown_NPC_Guard`
- `StartingTown_NPC_Elder`

Assign GUIDs in the Unity Inspector, NOT in code. `PersistentID.cs` already exists at `Assets/_Game/Scripts/World/PersistentID.cs` — do NOT create a new one.

### CRITICAL: Do NOT Modify TrainerNPC.cs

`Assets/_Game/Scripts/AI/TrainerNPC.cs` is a different NPC type (implements trainer dialogue in development OnGUI). Leave it completely untouched. `NPCPresence.cs` is a NEW component for generic town NPCs — the two coexist in `Scripts/AI/` without conflict.

### CRITICAL: SceneLoader — Use Coroutines, Not Async/Await

Per project-context.md rules:
- Use `coroutines` for time-based gameplay logic (scene loading = time-based)
- Use `async/await` ONLY for I/O (file save/load, Steam API)

```csharp
public void LoadRegion(string sceneName)
{
    if (string.IsNullOrEmpty(sceneName))
    {
        GameLog.Error(TAG, "LoadRegion called with empty scene name");
        return;
    }
    StartCoroutine(LoadRegionCoroutine(sceneName));
}

private IEnumerator LoadRegionCoroutine(string sceneName)
{
    GameLog.Info(TAG, $"Loading region: {sceneName}");
    var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    yield return op;
    GameLog.Info(TAG, $"Region loaded: {sceneName}");
}
```

### Town Layout Guidance

For the prototype, buildings should be simple placeholder geometry:
- Ground: a scaled-down Unity Plane (e.g., 30x30 units) or several Cubes for terrain
- Buildings: Cubes scaled to roughly 4×4×3 (width×depth×height), arranged in an L-shape or ring around a central plaza
- Minimum 5 walkable open areas between buildings where NPCs stand
- A perimeter boundary (optional: low wall using scaled cubes) to prevent the player from walking off the edge

No NavMesh baking required for this story — NPCs are static. NavMesh will be added in story 5.4 (NPC schedules/pathing).

### Player Spawn Position

The `Player` GameObject lives in `Core.unity` (always loaded). For story 5.1, manually set the Player's transform position to match `PlayerSpawnPoint` in StartingTown, OR implement a simple spawn:

```csharp
// In SceneLoader.cs, after StartingTown loads:
var spawnPoint = GameObject.Find("PlayerSpawnPoint");
if (spawnPoint != null)
{
    var player = FindFirstObjectByType<PlayerController>();
    if (player != null)
        player.transform.position = spawnPoint.transform.position;
}
```

Note: `FindFirstObjectByType` is the Unity 6 replacement for the deprecated `FindObjectOfType`.

### What NOT To Build In This Story

- ❌ NPC dialogue system (Epic 6)
- ❌ NPC schedules / day-night-aware routing (story 5.4)
- ❌ DayNightController.cs (story 5.6)
- ❌ NavMesh baking for NPC movement (story 5.4)
- ❌ QuestSystem or QuestSO (Epic 6)
- ❌ Enemy placement (story 5.2, 5.3)
- ❌ Camera fade transitions between regions (story 5.2 when wilderness transition is needed)
- ❌ WorldStateManager extensions for NPC state tracking (story 5.4+)

### Event System Notes

No new GameEventSO channels are needed for this story. The only cross-system dependency is `InteractionSystem` (existing) detecting `NPCPresence` (new) via `IInteractable`.

Future stories will add:
- `OnNPCDied` event channel (story 5.4 or Epic 6)
- `OnDayNightChanged` event channel (story 5.6)
- `OnRegionTransition` event channel (story 5.2)

### Project Structure Notes

**Files to CREATE:**
```
Assets/_Game/ScriptableObjects/NPC/NPCState.cs
Assets/_Game/ScriptableObjects/NPC/NPCDataSO.cs
Assets/_Game/Scripts/AI/NPCPresence.cs
Assets/_Game/Scripts/Core/SceneLoader.cs
Assets/_Game/Prefabs/NPCs/NPC_Base.prefab
Assets/_Game/Scenes/StartingTown.unity
Assets/_Game/Data/NPCs/NPC_Innkeeper.asset
Assets/_Game/Data/NPCs/NPC_Blacksmith.asset
Assets/_Game/Data/NPCs/NPC_Merchant.asset
Assets/_Game/Data/NPCs/NPC_Guard.asset
Assets/_Game/Data/NPCs/NPC_Elder.asset
```

**Files to MODIFY:**
```
Assets/_Game/Scenes/Core.unity         ← Attach SceneLoader.cs to SceneLoader stub GO
_bmad-output/implementation-artifacts/sprint-status.yaml  ← epic-5 → in-progress, 5-1 → ready-for-dev
```

**Files NOT to modify:**
```
Assets/_Game/Scripts/AI/TrainerNPC.cs          ← Different NPC type; untouched
Assets/_Game/Scripts/World/PersistentID.cs     ← Already exists; no changes needed
Assets/_Game/Scripts/World/IInteractable.cs    ← Already exists; no changes needed
Assets/_Game/Scripts/World/InteractionSystem.cs ← Already exists; no changes needed
Assets/_Game/Scripts/Core/WorldStateManager.cs ← No changes needed for this story
Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs ← Different SO type; untouched
```

**Existing infrastructure to leverage:**
- `IInteractable` + `InteractionSystem` — already raycasts Layer 8 with E-key prompt
- `PersistentID.cs` — already exists, just add to prefab
- `GameLog.cs` — mandatory logging wrapper
- `Core.unity` → `SceneLoader` stub GameObject already exists (attach script to it)
- `Assets/_Game/Data/NPCs/` directory already exists (Trainer_Master.asset is there)

### References

- Epic 5 scope: [Source: _bmad-output/epics.md#Epic 5: World & Exploration]
- Scene loading architecture (additive): [Source: _bmad-output/game-architecture.md#Decision 4: Scene Loading Strategy]
- NPC Schedule Pattern: [Source: _bmad-output/game-architecture.md#Novel Pattern 3: NPC Schedule Pattern]
- Permanent Entity Pattern (PersistentID): [Source: _bmad-output/game-architecture.md#Novel Pattern 2: Permanent Entity Pattern]
- Core.unity + region scene organization: [Source: _bmad-output/project-context.md#Scene Architecture]
- Layer 8 (Interactable) + InteractionSystem: [Source: _bmad-output/project-context.md#Interaction System Patterns]
- NPCDataSO fields: [Source: _bmad-output/game-architecture.md#Novel Pattern 3] — `npcName`, `dayState`, `nightState`, `walpoint`
- TrainerSO pattern (existing NPC SO): [Source: Assets/_Game/ScriptableObjects/NPC/TrainerSO.cs]
- WorldStateManager current state: [Source: Assets/_Game/Scripts/Core/WorldStateManager.cs] — minimal, only kill tracking

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
