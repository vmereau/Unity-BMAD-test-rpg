---
title: 'GameWorldState + NPC Memory System'
slug: 'game-world-state-npc-memory'
created: '2026-04-03'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5]
tech_stack: ['Unity 6000.3.10f1', 'C#', 'ScriptableObjects', 'GameEventSO<T>']
files_to_modify:
  - Assets/_Game/Scripts/Core/WorldStateManager.cs
files_to_create:
  - Assets/_Game/ScriptableObjects/Events/WorldFactData.cs
  - Assets/_Game/ScriptableObjects/Events/GameEventSO_WorldFact.cs
  - Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs
  - Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs
  - Assets/_Game/Scripts/AI/NPCMemoryComponent.cs
  - Tests/EditMode/WorldStateManagerFactsTests.cs
  - Tests/EditMode/TopicUnlockEvaluatorTests.cs
code_patterns:
  - 'GameEventSO<T> pattern for cross-system events'
  - 'WorldStateManager singleton as runtime truth source'
  - 'ScriptableObject with runtime logic methods (IsUnlocked / IsInvalidated)'
  - 'PersistentID GUID naming: Region_Type_Name'
test_patterns:
  - 'Edit Mode only — no MonoBehaviour, no scene'
  - 'WorldStateManager.Instance mock via direct field injection in tests'
---

# Tech-Spec: GameWorldState + NPC Memory System

**Created:** 2026-04-03

---

## Overview

### Problem Statement

`WorldStateManager` currently tracks only killed entities. The NPC memory system from the brainstorm requires a unified world fact store so that quest outcomes, NPC deaths, and world events can all be queried by `NPCMemoryEntrySO` to determine which memory effects are currently active.

### Solution

Extend `WorldStateManager` with a flat `Dictionary<string, bool>` world fact store. Add a typed event channel (`GameEventSO_WorldFact`) that broadcasts every fact change. Introduce `TopicUnlockEvaluator` (pure static logic), `NPCMemoryEntrySO` (data + conditions + effects), and `NPCMemoryComponent` (MonoBehaviour that holds memories and exposes active ones).

### Scope

**In Scope:**
- `WorldStateManager` world facts extension (SetFact / GetFact / SetQuestStep)
- Auto-fact on RegisterKill: `"Killed.{guid}"` written automatically
- `WorldFactData` struct + `GameEventSO_WorldFact` event type
- `TopicUnlockEvaluator` static class (AllTrue / AnyTrue)
- `NPCMemoryEntrySO` ScriptableObject: conditions + four effect types
- `NPCMemoryComponent` MonoBehaviour: holds memories, exposes active set, reacts to fact changes
- Edit Mode unit tests for WorldStateManager facts and TopicUnlockEvaluator
- Save-data shape struct (defined, NOT wired to SaveSystem — Epic 8)

**Out of Scope:**
- SaveSystem implementation (Epic 8)
- Dialogue system integration (future story)
- Shop system integration (future story)
- Routine override execution (future story)
- Quest system (future story — SetQuestStep is the write API; Quest SO authoring is separate)
- Reputation / faction aggregation layer (future Epic)
- NPC gossip or memory transfer between NPCs

---

## Context for Development

### Codebase Patterns

- **Event channels:** `GameEventSO<T>` in `Assets/_Game/ScriptableObjects/Events/`. To add a new type, create a struct + a concrete subclass following `GameEventSO_String` as the model. Both files go in the same folder.
- **WorldStateManager:** Singleton on `DontDestroyOnLoad` GameObject in `Core.unity`. Only singleton allowed alongside `SaveSystem`. Never cache WSM reference in other MonoBehaviours — always call `WorldStateManager.Instance`.
- **Cross-system events:** Always subscribe `OnEnable` / unsubscribe `OnDisable`. Never subscribe in `Start` or `Awake`.
- **SO with runtime methods:** Pattern is used by other SOs (e.g. `ItemSO`). `NPCMemoryEntrySO.IsUnlocked()` calling `WorldStateManager.Instance` is acceptable — mirrors `PersistentID.Awake()` pattern.
- **PersistentID GUID convention:** `Region_Type_Name` (e.g. `StartingTown_NPC_Guard`). The auto-generated Killed key becomes `"Killed.StartingTown_NPC_Guard"`.
- **Logging:** Always use `GameLog.Info/Warn/Error(TAG, msg)` — never `Debug.Log` directly.
- **Namespace:** `Game.Core` for WorldStateManager and event types. `Game.World` for TopicUnlockEvaluator. `Game.NPC` for NPCMemoryEntrySO. `Game.AI` for NPCMemoryComponent.
- **No magic strings in call sites:** Key format is assembled by `WorldStateManager` methods — callers never construct key strings manually.

### Key Format Convention

| Fact type | Format | Example |
|-----------|--------|---------|
| Quest step | `Quest.{QuestId}.{step_key}` | `Quest.Mill.monster_killed` |
| Entity killed | `Killed.{guid}` | `Killed.StartingTown_NPC_Guard` |
| World event | `World.{event_key}` | `World.starting_town_mill_cleared` |

- `{QuestId}`: PascalCase quest identifier (e.g. `Mill`, `HerbalistSearch`)
- `{step_key}`: snake_case step within the quest (e.g. `monster_killed`, `npc_talked`)
- `{guid}`: exact string from `PersistentID._guid` field (e.g. `StartingTown_NPC_Guard`)
- `{event_key}`: snake_case world event (e.g. `starting_town_mill_cleared`)

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/Core/WorldStateManager.cs` | Existing stub — extend in Story A |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Generic base — `GameEventSO<T>` |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_String.cs` | Model for new concrete event type |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Shows RegisterDeath → WorldStateManager.RegisterKill flow |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | NPC MonoBehaviour — NPCMemoryComponent attaches alongside this |
| `Assets/_Game/ScriptableObjects/NPC/NPCDataSO.cs` | NPC data SO — NPCMemoryEntrySO is a separate SO, not an extension |
| `_bmad-output/brainstorming-npc-memory-system-2026-04-03.md` | Design rationale — read for effect types and invalidation rules |
| `_bmad-output/project-context.md` | All 57 mandatory coding rules |

### Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Fact storage shape | Flat `Dictionary<string, bool>` | Simple, uniform — all fact types use the same query path |
| Kill auto-fact | `RegisterKill()` auto-sets `"Killed.{guid}"` | Memory entries can reference deaths as uniform string conditions, no special-casing |
| Quest API | `SetQuestStep(questId, stepKey, value)` formats key internally | Callers never construct key strings; format is centralized |
| Event payload | `WorldFactData` struct (key + value) | Listeners receive both pieces; no secondary lookup needed |
| `IsUnlocked()` placement | Method on `NPCMemoryEntrySO` | Keeps evaluation co-located with data; delegates logic to `TopicUnlockEvaluator` |
| Invalidation rule | Any one invalidation condition = memory closed | Matches brainstorm: "invalidation always supersedes unlock" |
| Save-data shape | Defined as `WorldStateSaveData` struct, not wired | Enables Epic 8 save integration without rework |

---

## Implementation Plan

### Story A — WorldStateManager: World Facts Extension

**Goal:** Add the flat world fact store and the event channel. This is the foundation everything else builds on.

#### Tasks

**A1. Create `WorldFactData` struct**
- File: `Assets/_Game/ScriptableObjects/Events/WorldFactData.cs`
- Namespace: `Game.Core`
- Content:
```csharp
[System.Serializable]
public struct WorldFactData
{
    public string key;
    public bool value;
    public WorldFactData(string key, bool value) { this.key = key; this.value = value; }
}
```

**A2. Create `GameEventSO_WorldFact`**
- File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_WorldFact.cs`
- Namespace: `Game.Core`
- Content:
```csharp
[CreateAssetMenu(menuName = "Game/Events/WorldFact Event", fileName = "NewWorldFactEvent")]
public class GameEventSO_WorldFact : GameEventSO<WorldFactData> { }
```
- Create asset instance: `Assets/_Game/Data/Events/OnWorldFactChanged.asset`

**A3. Extend `WorldStateManager`**
- Add `[SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;` (assign `OnWorldFactChanged.asset` in Inspector)
- Add `private readonly Dictionary<string, bool> _worldFacts = new Dictionary<string, bool>();`
- Add `SetFact(string key, bool value)`:
  - Sets `_worldFacts[key] = value`
  - Raises `_onWorldFactChanged` with `new WorldFactData(key, value)`
  - Logs: `GameLog.Info(TAG, $"World fact set: {key} = {value}")`
- Add `GetFact(string key) : bool`:
  - Returns `_worldFacts.TryGetValue(key, out var v) && v`
  - No logging (hot path — called per memory entry per evaluation)
- Extend `RegisterKill(string guid)` — after existing `_killedEntities.Add(guid)`:
  - Call `SetFact($"Killed.{guid}", true)` — auto-broadcasts via event
- Add `SetQuestStep(string questId, string stepKey, bool value)`:
  - Formats key: `$"Quest.{questId}.{stepKey}"`
  - Calls `SetFact(formattedKey, value)`
- Add `WorldStateSaveData GetSaveData()` (save shape, not wired):
```csharp
public WorldStateSaveData GetSaveData() => new WorldStateSaveData
{
    killedEntities = new List<string>(_killedEntities),
    worldFacts = new Dictionary<string, bool>(_worldFacts)
};
```
- Add `WorldStateSaveData` struct (same file or nested):
```csharp
[System.Serializable]
public struct WorldStateSaveData
{
    public List<string> killedEntities;
    public Dictionary<string, bool> worldFacts;
}
```

**A4. Edit Mode tests — `Tests/EditMode/WorldStateManagerFactsTests.cs`**

Tests (all Edit Mode, no scene):
- `SetFact_StoresBoolValue` — SetFact("World.test", true) → GetFact returns true
- `GetFact_MissingKey_ReturnsFalse` — GetFact("World.nonexistent") → false
- `SetFact_Overwrite_UpdatesValue` — set true, then set false → false
- `SetQuestStep_FormatsKey` — SetQuestStep("Mill","monster_killed",true) → GetFact("Quest.Mill.monster_killed") == true
- `RegisterKill_AutoSetsKilledFact` — RegisterKill("StartingTown_NPC_Guard") → GetFact("Killed.StartingTown_NPC_Guard") == true AND IsKilled("StartingTown_NPC_Guard") == true
- `SetFact_RaisesEvent_WithCorrectPayload` — listener receives WorldFactData with matching key+value

> **Note on testing WSM:** `WorldStateManager` is a MonoBehaviour singleton. For Edit Mode tests, instantiate it directly with `new GameObject().AddComponent<WorldStateManager>()` and assign `Instance` via reflection, or extract the fact dictionary logic into a pure `WorldFactStore` sub-class. **Recommended:** keep the test simple — use `AddComponent` in `[SetUp]` and destroy in `[TearDown]`.

---

### Story B — TopicUnlockEvaluator + NPCMemoryEntrySO

**Goal:** Pure evaluation logic + the ScriptableObject schema for memory entries.
Depends on Story A being complete (`WorldStateManager.GetFact()` must exist).

#### Tasks

**B1. Create `TopicUnlockEvaluator`**
- File: `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs`
- Namespace: `Game.World`
- Static class (no MonoBehaviour, no instance)
```csharp
public static class TopicUnlockEvaluator
{
    // Returns true if ALL keys are set to true in WorldStateManager.
    // Empty array = trivially unlocked (always true).
    public static bool AllTrue(string[] keys)
    {
        if (keys == null || keys.Length == 0) return true;
        var wsm = WorldStateManager.Instance;
        if (wsm == null)
        {
            GameLog.Warn("[TopicUnlock]", "WorldStateManager not available — conditions evaluated as false");
            return false;
        }
        foreach (var key in keys)
            if (!wsm.GetFact(key)) return false;
        return true;
    }

    // Returns true if ANY key is set to true in WorldStateManager.
    // Empty array = not invalidated (returns false).
    public static bool AnyTrue(string[] keys)
    {
        if (keys == null || keys.Length == 0) return false;
        var wsm = WorldStateManager.Instance;
        if (wsm == null) return false;
        foreach (var key in keys)
            if (wsm.GetFact(key)) return true;
        return false;
    }
}
```

**B2. Create `NPCMemoryEffects` (nested class inside NPCMemoryEntrySO)**
```csharp
[System.Serializable]
public class NPCMemoryEffects
{
    [Header("Dialogue")]
    [Tooltip("Dialogue lines available while this memory is active. Consumed by DialogueSystem (future).")]
    public string[] dialogueLines;

    [Header("Shop")]
    [Range(-1f, 1f)]
    [Tooltip("Price modifier. 0 = no effect. -0.1 = 10% discount. Consumed by ShopSystem (future).")]
    public float shopPriceModifier;

    [Tooltip("One-shot line played first time shop is opened while memory is active. Set '' to skip.")]
    public string shopRevealDialogueLine;

    [Header("Routine")]
    [Tooltip("Routine override while this memory is active. None = no change. Consumed by NPCScheduler (future).")]
    public NPCState routineOverride = NPCState.Working; // NPCState.None added in this story

    [Tooltip("If true, routineOverride is applied. If false, NPC keeps default schedule.")]
    public bool overrideRoutine;

    [Header("Quest")]
    [Tooltip("Dialogue key that initiates or references a quest. Empty = no quest effect.")]
    public string questDialogueKey;
}
```

> **Note:** Add `None = 0` to `NPCState` enum (`Assets/_Game/ScriptableObjects/NPC/NPCState.cs`) so `overrideRoutine` can default to "no override". Shift existing values if needed — check usages in `NPCDataSO` and `NPCPresence`.

**B3. Create `NPCMemoryEntrySO`**
- File: `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs`
- Namespace: `Game.NPC`
```csharp
[CreateAssetMenu(menuName = "Game/NPC/Memory Entry", fileName = "Mem_")]
public class NPCMemoryEntrySO : ScriptableObject
{
    private const string TAG = "[NPCMemory]";

    [Header("Identity")]
    [Tooltip("Unique ID for this memory — used in logs and save data.")]
    public string memoryId;

    [Header("Conditions")]
    [Tooltip("ALL of these world fact keys must be true for this memory to be active.")]
    public string[] unlockConditions;

    [Tooltip("If ANY of these world fact keys is true, this memory is permanently closed.")]
    public string[] invalidationConditions;

    [Header("Effects")]
    public NPCMemoryEffects effects;

    /// <summary>Returns true when all unlock conditions are met in WorldStateManager.</summary>
    public bool IsUnlocked() => TopicUnlockEvaluator.AllTrue(unlockConditions);

    /// <summary>Returns true when any invalidation condition is met. Invalidation supersedes unlock.</summary>
    public bool IsInvalidated() => TopicUnlockEvaluator.AnyTrue(invalidationConditions);

    /// <summary>Convenience: unlocked AND not invalidated.</summary>
    public bool IsActive() => IsUnlocked() && !IsInvalidated();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(memoryId))
            GameLog.Warn(TAG, $"NPCMemoryEntrySO '{name}' has no memoryId set");
    }
#endif
}
```

**B4. Edit Mode tests — `Tests/EditMode/TopicUnlockEvaluatorTests.cs`**

Tests:
- `AllTrue_EmptyArray_ReturnsTrue` — `AllTrue(new string[0])` → true (trivially unlocked)
- `AllTrue_AllFactsTrue_ReturnsTrue` — set "Quest.Mill.a" + "World.b" true → AllTrue returns true
- `AllTrue_OneFactFalse_ReturnsFalse` — set only first fact true → AllTrue false
- `AnyTrue_EmptyArray_ReturnsFalse` — `AnyTrue(new string[0])` → false (not invalidated)
- `AnyTrue_OneFact_ReturnsTrue` — one fact set true → AnyTrue true
- `AnyTrue_NoFactsTrue_ReturnsFalse` — no facts set → false
- `IsActive_UnlockedNotInvalidated_ReturnsTrue` — SO with unlock met, invalidation not met → IsActive true
- `IsActive_Invalidated_ReturnsFalse` — both unlock + invalidation met → IsActive false (invalidation wins)

---

### Story C — NPCMemoryComponent

**Goal:** MonoBehaviour that holds an NPC's memory entries and exposes the currently active set to downstream systems.
Depends on Stories A and B.

#### Tasks

**C1. Create `NPCMemoryComponent`**
- File: `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`
- Namespace: `Game.AI`
- Attach alongside `NPCPresence` on NPC prefabs in `Assets/_Game/Prefabs/NPCs/`

```csharp
public class NPCMemoryComponent : MonoBehaviour
{
    private const string TAG = "[NPCMemory]";

    [SerializeField] private List<NPCMemoryEntrySO> _memories;
    [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;  // OnWorldFactChanged.asset

    private void OnEnable()
    {
        if (_onWorldFactChanged == null)
        {
            GameLog.Warn(TAG, $"OnWorldFactChanged not assigned on {gameObject.name} — memories won't react to world changes");
            return;
        }
        _onWorldFactChanged.AddListener(HandleWorldFactChanged);
    }

    private void OnDisable()
    {
        if (_onWorldFactChanged == null) return;
        _onWorldFactChanged.RemoveListener(HandleWorldFactChanged);
    }

    /// <summary>
    /// Returns all memory entries where IsUnlocked() && !IsInvalidated().
    /// Evaluated on demand — callers (dialogue, shop, quest) call this when they open.
    /// Not cached: world state may change between calls.
    /// </summary>
    public NPCMemoryEntrySO[] GetActiveMemories()
    {
        if (_memories == null || _memories.Count == 0) return System.Array.Empty<NPCMemoryEntrySO>();

        var result = new List<NPCMemoryEntrySO>(_memories.Count);
        foreach (var memory in _memories)
        {
            if (memory == null) continue;
            if (memory.IsActive()) result.Add(memory);
        }
        return result.ToArray();
    }

    private void HandleWorldFactChanged(WorldFactData data)
    {
        // Future: raise a local OnMemoriesChanged event for UI / dialogue pre-evaluation.
        // For now, no-op — GetActiveMemories() is always evaluated on demand.
        GameLog.Info(TAG, $"World fact changed: {data.key} = {data.value} — memories will re-evaluate on next query");
    }
}
```

**C2. Create event asset**
- `Assets/_Game/Data/Events/OnWorldFactChanged.asset` — `GameEventSO_WorldFact` instance
  (already specified in Story A — verify it's created, assign to NPCMemoryComponent in Inspector)

**C3. Prefab updates (Editor work)**
- Add `NPCMemoryComponent` to existing NPC prefabs in `Assets/_Game/Prefabs/NPCs/` that will use the system
- Assign `OnWorldFactChanged.asset` to the `_onWorldFactChanged` field
- Leave `_memories` list empty for now — populated per NPC when memory SOs are authored

---

## Acceptance Criteria

### Story A — WorldStateManager Facts

**Given** WorldStateManager is initialized,
**When** `SetFact("World.mill_cleared", true)` is called,
**Then** `GetFact("World.mill_cleared")` returns `true` and `OnWorldFactChanged` fires with key `"World.mill_cleared"` and value `true`.

**Given** a key has never been set,
**When** `GetFact("World.nonexistent")` is called,
**Then** it returns `false` (no exception, no log).

**Given** an entity with GUID `"StartingTown_NPC_Guard"`,
**When** `RegisterKill("StartingTown_NPC_Guard")` is called,
**Then** `IsKilled("StartingTown_NPC_Guard")` returns `true` AND `GetFact("Killed.StartingTown_NPC_Guard")` returns `true`.

**Given** `SetQuestStep("Mill", "monster_killed", true)` is called,
**Then** `GetFact("Quest.Mill.monster_killed")` returns `true`.

### Story B — TopicUnlockEvaluator + NPCMemoryEntrySO

**Given** a `NPCMemoryEntrySO` with `unlockConditions: ["Quest.Mill.monster_killed"]`,
**When** `SetQuestStep("Mill", "monster_killed", true)` has been called,
**Then** `memory.IsUnlocked()` returns `true`.

**Given** a memory with both unlock and invalidation conditions met simultaneously,
**When** `memory.IsActive()` is called,
**Then** it returns `false` (invalidation supersedes unlock).

**Given** a memory with no unlock conditions (empty array),
**Then** `IsUnlocked()` returns `true` (trivially unlocked — always available).

**Given** a memory with no invalidation conditions (empty array),
**Then** `IsInvalidated()` returns `false` (never closed by default).

### Story C — NPCMemoryComponent

**Given** an NPC has `NPCMemoryComponent` with 3 memory entries (2 active, 1 invalidated),
**When** `GetActiveMemories()` is called,
**Then** it returns exactly 2 entries (the two where `IsActive()` is true).

**Given** `NPCMemoryComponent` is enabled and `OnWorldFactChanged` is assigned,
**When** a world fact changes,
**Then** `HandleWorldFactChanged` is called (listener receives the event).

**Given** `OnWorldFactChanged` is not assigned in Inspector,
**When** `OnEnable()` fires,
**Then** a `GameLog.Warn` is emitted and no null exception occurs.

---

## Additional Context

### Dependencies

- Story B depends on Story A (`WorldStateManager.GetFact()` must exist)
- Story C depends on Stories A + B (needs both the event type and the SO)
- No external packages required — all patterns use existing project infrastructure
- `NPCState` enum needs a `None = 0` value added before Story B (check existing enum values for conflicts)

### Testing Strategy

- **Story A:** Edit Mode tests only — `WorldStateManagerFactsTests.cs`. Use `new GameObject().AddComponent<WorldStateManager>()` in `[SetUp]`, destroy in `[TearDown]`. Use reflection to set `Instance` field if singleton guard blocks test instantiation.
- **Story B:** Edit Mode tests only — `TopicUnlockEvaluatorTests.cs`. Requires a live `WorldStateManager` instance (same setup as Story A tests). Tests `AllTrue`/`AnyTrue` and `IsActive()` combinations.
- **Story C:** No automated tests — `NPCMemoryComponent` is a thin adapter. Verify manually: add a memory entry to an NPC in the scene, set the unlock condition via a debug tool or Inspector hack, confirm `GetActiveMemories()` returns it.

### Notes

- **Save-forward design:** `WorldStateSaveData` struct is defined in Story A but not wired. When Epic 8 implements `SaveSystem`, it calls `WorldStateManager.GetSaveData()` for serialization and a corresponding `LoadSaveData(WorldStateSaveData)` method (not authored yet — add in Epic 8 story).
- **Shop reveal tracking:** The `shopRevealDialogueLine` one-shot reveal is scoped out of this spec. When the ShopSystem is built, it reads `effects.shopRevealDialogueLine` and writes a world fact `"World.shop_reveal_{npcGuid}_{memoryId}"` after playing it. No special handling needed in this spec.
- **`GetActiveMemories()` allocation:** Returns a `ToArray()` — acceptable for dialogue/shop open events (not called in Update). If called from a hot path later, introduce a pooled list.
- **Memory authoring convention:** Asset names follow `Mem_{NpcName}_{EventSlug}.asset` (e.g., `Mem_Miller_MillQuestComplete.asset`) in `Assets/_Game/Data/NPCs/Memories/`.
- **Faction / area events:** Use `World.` prefix with descriptive snake_case keys. These are set by quest or world scripting systems — the memory system only reads them. No special scoping needed.
