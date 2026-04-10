---
title: 'Typed World Facts System (ScriptableObject)'
slug: 'typed-world-facts'
created: '2026-04-10'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C#', 'NUnit (Edit Mode tests)']
files_to_modify:
  - 'Assets/_Game/Scripts/Core/WorldStateManager.cs'
  - 'Assets/_Game/Scripts/Core/WorldFactPrefix.cs'
  - 'Assets/_Game/Scripts/World/PersistentID.cs'
  - 'Assets/_Game/Scripts/World/DialogueSystem.cs'
  - 'Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs'
  - 'Assets/_Game/Scripts/AI/NPCMemoryComponent.cs'
  - 'Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs'
  - 'Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs'
  - 'Assets/Tests/EditMode/WorldStateManagerFactsTests.cs'
  - 'Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs'
files_to_create:
  - 'Assets/_Game/Scripts/Core/State/Facts/Fact.cs'
  - 'Assets/_Game/Scripts/Core/State/Facts/QuestFact.cs'
  - 'Assets/_Game/Scripts/Core/State/Facts/WorldFact.cs'
  - 'Assets/_Game/Scripts/Core/State/Facts/KilledFact.cs'
  - 'Assets/_Game/Scripts/Core/State/Facts/DialogueFact.cs'
code_patterns:
  - 'Fact : ScriptableObject with abstract override ToString()'
  - 'Init() method on each concrete Fact for runtime/test instantiation'
  - 'Dictionary<string, bool> kept internally — fact.ToString() is the key'
  - 'namespace Game.Core kept across all moved/new files'
  - 'one file per Fact subclass (GameEventSO precedent)'
  - 'KilledFact.EntityGuid exposes GUID string for event broadcasting'
test_patterns:
  - 'NUnit EditMode — AddComponent + reflection to force-set static Instance'
  - 'ScriptableObject.CreateInstance<T>().Init(...) for test fact creation'
  - 'Object.DestroyImmediate on fact instances in TearDown'
---

# Tech-Spec: Typed World Facts System (ScriptableObject)

**Created:** 2026-04-10

## Overview

### Problem Statement

`WorldStateManager` manages game state through `Dictionary<string, bool> _worldFacts` with manually
constructed string keys. `NPCMemoryEntrySO.unlockConditions` is a `string[]` — designer-editable but
untyped, with no Inspector validation and no link back to the system that consumes them. Call sites across
`PersistentID`, `DialogueSystem`, and `NPCMemoryComponent` pass raw strings with no compile-time safety.

### Solution

`Fact` becomes an abstract `ScriptableObject`. Each concrete subclass (`QuestFact`, `WorldFact`,
`KilledFact`, `DialogueFact`) is a Unity asset with `[SerializeField]` fields set in the Inspector and
a `ToString()` that produces the canonical dictionary key. `WorldStateManager` stays `Dictionary<string,
bool>` internally — `SetFact(Fact, bool)` calls `fact.ToString()` as the key. `NPCMemoryEntrySO`
`unlockConditions`/`invalidationConditions` become `Fact[]` (typed SO references). `PersistentID`
adds a `[SerializeField] KilledFact _killedFact` that owns the entity's kill identity.
`StartDialogueNode` adds a `[SerializeField] DialogueFact dialogueFact` used for played-state tracking.
No raw string keys exist at any public call site; no `internal` workarounds needed.

### Scope

**In Scope:**
- New folder `Assets/_Game/Scripts/Core/State/` + subfolder `Facts/`
- Move `WorldStateManager.cs` and `WorldFactPrefix.cs` into `State/`
- Abstract `Fact : ScriptableObject` + 4 concrete implementations with `[CreateAssetMenu]` and `Init()`
- Refactor all `WorldStateManager` public methods to accept typed `Fact` objects
- `PersistentID`: replace `_guid` field with `[SerializeField] KilledFact _killedFact`; expose `EntityGuid` via property on `KilledFact`
- `StartDialogueNode`: add `[SerializeField] public DialogueFact dialogueFact`
- `DialogueSystem.NotifyTopicCompleted()`: use `_currentStartNode.dialogueFact`
- `NPCMemoryComponent.GetActiveStartDialogNodes()`: use `node.dialogueFact`
- `NPCMemoryEntrySO`: `string[]` → `Fact[]` for unlock/invalidation conditions
- `TopicUnlockEvaluator.AllTrue/AnyTrue`: `string[]` → `Fact[]`
- Update `WorldStateManagerFactsTests` and `TopicUnlockEvaluatorTests`

**Out of Scope:**
- Creating the actual `.asset` files for existing NPCs/entities (migration — designer task after implementation)
- Epic 8 save system
- `SkillFact` / `StatFact`
- `WorldFactData` event payload shape (still `string key, bool value`)
- Custom Inspector drawers for `Fact[]` fields

---

## Context for Development

### Codebase Patterns

- **Namespace**: Keep `namespace Game.Core` on all files in `Core/State/` and `Core/State/Facts/` — folder
  hierarchy does not need to match namespace in this project.
- **One file per SO subclass**: Per the GameEventSO pattern (Unity's `m_Script` breaks on domain reload
  if subclasses share a file), each `Fact` subclass gets its own `.cs` file.
- **`Fact : ScriptableObject`**: Abstract SO — no `[CreateAssetMenu]`. Concrete subclasses each have
  `[CreateAssetMenu(menuName = "Game/Facts/...", fileName = "...")]`.
- **`Init()` pattern**: Each concrete `Fact` exposes an `Init()` method that sets its serialized fields
  and returns `this`. This is used only by runtime code and tests — asset-based usage sets fields in the
  Inspector. `Init()` is the only way to programmatically set `[SerializeField] private` fields without
  reflection.
- **`Dictionary<string, bool>` stays internal**: `fact.ToString()` produces the key. `WorldStateSaveData`
  shape is unchanged — same string keys, compatible with future Epic 8 serialization.
- **`WorldFactData` event payload unchanged**: `_onWorldFactChanged` still raises `WorldFactData(key, value)`
  where `key = fact.ToString()`. Subscribers see the same string format.
- **Move strategy**: Use `Bash mv` + `.meta` mv + `refresh_unity(mode="force")` — `manage_asset(action="move")`
  is unreliable (partial moves observed).
- **Null guards**: Every public method that accepts a `Fact` parameter guards against null with
  `GameLog.Warn` + early return. `Fact[]` array elements that are null are skipped (not an error).
- **`KilledFact.EntityGuid`**: The `PersistentID._onEntityKilled` event still broadcasts the raw GUID
  string. `KilledFact` exposes `public string EntityGuid => _guid` to provide it. `PersistentID` removes
  its own `[SerializeField] private string _guid` field — `_killedFact.EntityGuid` is the single source
  of truth.
- **`GenerateGUID` context menu**: Moves from `PersistentID` to `KilledFact` (`#if UNITY_EDITOR` block).
- **Migration note**: Existing `PersistentID` instances in scenes/prefabs will lose their `_guid` values
  (field removed). Designers must create `KilledFact` assets and assign them. Existing `StartDialogueNode`
  SOs and `NPCMemoryEntrySO` conditions must also be updated — document in a migration note comment.
- **`TopicUnlockEvaluator`**: Only its method signatures change (`string[]` → `Fact[]`). Null-element
  handling: skip null facts (treat as "condition not present").

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Core/WorldStateManager.cs` | Refactor + move to State/ |
| `Assets/_Game/Scripts/Core/WorldFactPrefix.cs` | Move to State/ |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Replace `_guid` with `_killedFact` |
| `Assets/_Game/Scripts/World/DialogueSystem.cs:145` | `SetDialoguePlayed` call to update |
| `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs` | `string[]` → `Fact[]` params |
| `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs:67` | `IsDialoguePlayed` call to update |
| `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs` | Add `dialogueFact` field |
| `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs` | `string[]` → `Fact[]` |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | Full update to typed Facts |
| `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs` | Full update to typed Facts |

### Technical Decisions

| Decision | Rationale |
| -------- | --------- |
| `Fact : ScriptableObject` | Enables `Fact[]` in SO Inspectors (drag-and-drop, type-safe); facts are reusable shared assets |
| `abstract override string ToString()` | Keeps key computation in the fact type itself; no separate `ToKey()` indirection |
| `Init()` on concrete Facts | Only way to set `[SerializeField] private` fields at runtime/test without reflection |
| `Dictionary<string, bool>` internal store | Unchanged — string keys serialize trivially for Epic 8 |
| `KilledFact.EntityGuid` property | `PersistentID` still broadcasts GUID via event; `KilledFact` is single source of truth |
| `GenerateGUID` moves to `KilledFact` | The GUID belongs to the fact asset, not the component |
| Null elements in `Fact[]` skipped | Defensive — a misconfigured SO with an unassigned slot shouldn't block valid conditions |
| `StartDialogueNode.dialogueFact` is `public` | `DialogueSystem` and `NPCMemoryComponent` access it directly; no setter needed |

---

## Implementation Plan

### Tasks

> **Dependency order**: Fact hierarchy first → move + refactor WorldStateManager → update all callers and data types → update tests.

- [ ] **Task 1: Create `Assets/_Game/Scripts/Core/State/Facts/Fact.cs`**
  - New file, `namespace Game.Core`
  ```csharp
  using UnityEngine;

  namespace Game.Core
  {
      /// <summary>
      /// Abstract ScriptableObject base for all typed world facts.
      /// Subclasses encode their key format in ToString() — WorldStateManager calls
      /// fact.ToString() and stores the result in Dictionary&lt;string, bool&gt;.
      /// Create Fact assets via the Game/Facts/ Create Asset menus.
      /// </summary>
      public abstract class Fact : ScriptableObject
      {
          public WorldFactPrefix Prefix { get; protected set; }
          public abstract override string ToString();
      }
  }
  ```

- [ ] **Task 2: Create `Assets/_Game/Scripts/Core/State/Facts/QuestFact.cs`**
  - New file, `namespace Game.Core`
  ```csharp
  using UnityEngine;

  namespace Game.Core
  {
      /// <summary>Key format: Quest.{questId}.{stepKey}</summary>
      [CreateAssetMenu(menuName = "Game/Facts/Quest Fact", fileName = "QuestFact_")]
      public class QuestFact : Fact
      {
          [SerializeField] private string _questId;
          [SerializeField] private string _stepKey;

          /// <summary>Runtime/test initialiser. Asset-based usage sets fields via Inspector.</summary>
          public QuestFact Init(string questId, string stepKey)
          {
              Prefix = WorldFactPrefix.Quest;
              _questId = questId;
              _stepKey = stepKey;
              return this;
          }

          private void OnEnable() => Prefix = WorldFactPrefix.Quest;

          public override string ToString() => $"{WorldFactPrefix.Quest}.{_questId}.{_stepKey}";
      }
  }
  ```
  - Note: `OnEnable()` sets `Prefix` when the asset is loaded from disk (fields are deserialized before `OnEnable`).

- [ ] **Task 3: Create `Assets/_Game/Scripts/Core/State/Facts/WorldFact.cs`**
  - New file, `namespace Game.Core`
  ```csharp
  using UnityEngine;

  namespace Game.Core
  {
      /// <summary>Key format: World.{eventKey}</summary>
      [CreateAssetMenu(menuName = "Game/Facts/World Fact", fileName = "WorldFact_")]
      public class WorldFact : Fact
      {
          [SerializeField] private string _eventKey;

          public WorldFact Init(string eventKey)
          {
              Prefix = WorldFactPrefix.World;
              _eventKey = eventKey;
              return this;
          }

          private void OnEnable() => Prefix = WorldFactPrefix.World;

          public override string ToString() => $"{WorldFactPrefix.World}.{_eventKey}";
      }
  }
  ```

- [ ] **Task 4: Create `Assets/_Game/Scripts/Core/State/Facts/KilledFact.cs`**
  - New file, `namespace Game.Core`
  ```csharp
  using UnityEngine;

  namespace Game.Core
  {
      /// <summary>
      /// Key format: Killed.{guid}
      /// Assign one KilledFact asset per tracked entity. The GUID uniquely identifies
      /// the entity across sessions. Use the Generate GUID context menu to create one.
      /// </summary>
      [CreateAssetMenu(menuName = "Game/Facts/Killed Fact", fileName = "KilledFact_")]
      public class KilledFact : Fact
      {
          [SerializeField] private string _guid;

          /// <summary>The entity's unique identifier — broadcast via GameEventSO_String on death.</summary>
          public string EntityGuid => _guid;

          public KilledFact Init(string guid)
          {
              Prefix = WorldFactPrefix.Killed;
              _guid = guid;
              return this;
          }

          private void OnEnable() => Prefix = WorldFactPrefix.Killed;

          public override string ToString() => $"{WorldFactPrefix.Killed}.{_guid}";

  #if UNITY_EDITOR
          [ContextMenu("Generate GUID")]
          private void GenerateGUID()
          {
              _guid = System.Guid.NewGuid().ToString();
              UnityEditor.EditorUtility.SetDirty(this);
          }
  #endif
      }
  }
  ```

- [ ] **Task 5: Create `Assets/_Game/Scripts/Core/State/Facts/DialogueFact.cs`**
  - New file, `namespace Game.Core`
  ```csharp
  using UnityEngine;

  namespace Game.Core
  {
      /// <summary>Key format: Dialogue.Played.{nodeId}</summary>
      [CreateAssetMenu(menuName = "Game/Facts/Dialogue Fact", fileName = "DialogueFact_")]
      public class DialogueFact : Fact
      {
          [SerializeField] private string _nodeId;

          public DialogueFact Init(string nodeId)
          {
              Prefix = WorldFactPrefix.Dialogue;
              _nodeId = nodeId;
              return this;
          }

          private void OnEnable() => Prefix = WorldFactPrefix.Dialogue;

          public override string ToString() => $"{WorldFactPrefix.Dialogue}.Played.{_nodeId}";
      }
  }
  ```

- [ ] **Task 6: Move `WorldStateManager.cs` and `WorldFactPrefix.cs` to `Core/State/`**
  - `Bash mv Assets/_Game/Scripts/Core/WorldStateManager.cs Assets/_Game/Scripts/Core/State/WorldStateManager.cs`
  - `Bash mv Assets/_Game/Scripts/Core/WorldStateManager.cs.meta Assets/_Game/Scripts/Core/State/WorldStateManager.cs.meta`
  - `Bash mv Assets/_Game/Scripts/Core/WorldFactPrefix.cs Assets/_Game/Scripts/Core/State/WorldFactPrefix.cs`
  - `Bash mv Assets/_Game/Scripts/Core/WorldFactPrefix.cs.meta Assets/_Game/Scripts/Core/State/WorldFactPrefix.cs.meta`
  - `refresh_unity(mode="force")`
  - Check `read_console` for zero compilation errors before proceeding.

- [ ] **Task 7: Refactor `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`**
  - Keep: singleton, `DontDestroyOnLoad`, `[SerializeField] _onWorldFactChanged`, `_worldFacts`, `WorldStateSaveData`, `OnDestroy`.
  - **Replace entire public API** with the following. Delete all old string-parameter methods.

  ```csharp
  // ── Kill tracking ──────────────────────────────────────────────────────

  public bool IsKilled(KilledFact fact)
  {
      if (fact == null) { GameLog.Warn(TAG, "IsKilled called with null fact"); return false; }
      return GetFact(fact);
  }

  public void RegisterKill(KilledFact fact)
  {
      if (fact == null) { GameLog.Warn(TAG, "RegisterKill called with null fact"); return; }
      SetFact(fact, true);
  }

  // ── Typed read/write ───────────────────────────────────────────────────

  /// <summary>Typed read — calls fact.ToString() to look up the key.</summary>
  public bool GetFact(Fact fact)
  {
      if (fact == null) { GameLog.Warn(TAG, "GetFact called with null fact"); return false; }
      return _worldFacts.TryGetValue(fact.ToString(), out var v) && v;
  }

  /// <summary>Typed write — calls fact.ToString() as the storage key.</summary>
  public void SetFact(Fact fact, bool value)
  {
      if (fact == null) { GameLog.Warn(TAG, "SetFact called with null fact"); return; }
      SetFactInternal(fact.ToString(), value);
  }

  // ── Typed convenience methods ──────────────────────────────────────────

  public void SetQuestStep(QuestFact fact, bool value) => SetFact(fact, value);
  public void SetWorldEvent(WorldFact fact, bool value) => SetFact(fact, value);

  public void SetDialoguePlayed(DialogueFact fact)
  {
      if (fact == null) { GameLog.Warn(TAG, "SetDialoguePlayed called with null fact"); return; }
      SetFact(fact, true);
  }

  public bool IsDialoguePlayed(DialogueFact fact)
  {
      if (fact == null) { GameLog.Warn(TAG, "IsDialoguePlayed called with null fact"); return false; }
      return GetFact(fact);
  }

  // ── Internal ──────────────────────────────────────────────────────────

  private void SetFactInternal(string key, bool value)
  {
      _worldFacts[key] = value;
      GameLog.Info(TAG, $"World fact set: {key} = {value}");
      _onWorldFactChanged?.Raise(new WorldFactData(key, value));
  }
  ```

  - `GetSaveData()` unchanged — `WorldStateSaveData.worldFacts` is still `Dictionary<string, bool>`.

- [ ] **Task 8: Update `Assets/_Game/Scripts/World/PersistentID.cs`**
  - Remove `[SerializeField] private string _guid;`
  - Add `[SerializeField] private KilledFact _killedFact;`
  - Add `using Game.Core;`
  - Update `Awake()`: replace null/empty GUID check and `IsKilled(_guid)` call:
  ```csharp
  private void Awake()
  {
      if (_killedFact == null)
      {
          GameLog.Error(TAG, $"PersistentID on {gameObject.name} has no KilledFact assigned — entity will not be tracked");
          return;
      }
      if (WorldStateManager.Instance == null)
      {
          GameLog.Warn(TAG, $"WorldStateManager not found — PersistentID check skipped for {gameObject.name}");
          return;
      }
      if (WorldStateManager.Instance.IsKilled(_killedFact))
          gameObject.SetActive(false);
  }
  ```
  - Update `RegisterDeath()`: replace `RegisterKill(_guid)` and `_onEntityKilled.Raise(_guid)`:
  ```csharp
  public void RegisterDeath()
  {
      WorldStateManager.Instance?.RegisterKill(_killedFact);

      if (_onEntityKilled != null)
          _onEntityKilled.Raise(_killedFact.EntityGuid);
      else
          GameLog.Warn(TAG, $"OnEntityKilled event not assigned on {gameObject.name} — kill not broadcast");
  }
  ```
  - Remove the `#if UNITY_EDITOR GenerateGUID` block (moved to `KilledFact`).

- [ ] **Task 9: Update `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs`**
  - Add `using Game.Core;`
  - Add field after `isRepeatable`:
  ```csharp
  [Tooltip("Fact asset used to track and check played state in WorldStateManager. Required if isRepeatable = false.")]
  public DialogueFact dialogueFact;
  ```

- [ ] **Task 10: Update `Assets/_Game/Scripts/World/DialogueSystem.cs`**
  - In `NotifyTopicCompleted()`, replace the `SetDialoguePlayed` block:
  ```csharp
  if (!_currentStartNode.isRepeatable)
  {
      if (_currentStartNode.dialogueFact == null)
      {
          GameLog.Warn(TAG, $"StartDialogueNode '{_currentStartNode.name}' has no DialogueFact assigned — played state not recorded");
      }
      else if (WorldStateManager.Instance != null)
      {
          WorldStateManager.Instance.SetDialoguePlayed(_currentStartNode.dialogueFact);
          GameLog.Info(TAG, $"Dialogue topic '{_currentStartNode.name}' marked as played");
      }
      else
      {
          GameLog.Warn(TAG, $"WorldStateManager unavailable — dialogue topic '{_currentStartNode.name}' played state not recorded");
      }
  }
  ```

- [ ] **Task 11: Update `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs`**
  - In `GetActiveStartDialogNodes()`, replace the `IsDialoguePlayed` guard:
  ```csharp
  if (!node.isRepeatable
      && node.dialogueFact != null
      && WorldStateManager.Instance != null
      && WorldStateManager.Instance.IsDialoguePlayed(node.dialogueFact))
      continue;
  ```

- [ ] **Task 12: Update `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs`**
  - Add `using Game.Core;`
  - Change field types:
  ```csharp
  // Before:
  public string[] unlockConditions;
  public string[] invalidationConditions;

  // After:
  [Tooltip("ALL of these facts must be true for this memory to be active.")]
  public Fact[] unlockConditions;
  [Tooltip("If ANY of these facts is true, this memory is permanently closed.")]
  public Fact[] invalidationConditions;
  ```

- [ ] **Task 13: Update `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs`**
  - Add `using Game.Core;`
  - Change method signatures from `string[]` to `Fact[]`. Null elements are skipped:
  ```csharp
  public static bool AllTrue(Fact[] facts)
  {
      if (facts == null || facts.Length == 0) return true;
      var wsm = WorldStateManager.Instance;
      if (wsm == null)
      {
          GameLog.Warn(TAG, "WorldStateManager not available — conditions evaluated as false");
          return false;
      }
      foreach (var fact in facts)
      {
          if (fact == null) continue;
          if (!wsm.GetFact(fact)) return false;
      }
      return true;
  }

  public static bool AnyTrue(Fact[] facts)
  {
      if (facts == null || facts.Length == 0) return false;
      var wsm = WorldStateManager.Instance;
      if (wsm == null)
      {
          GameLog.Warn(TAG, "WorldStateManager not available — invalidation conditions evaluated as false");
          return false;
      }
      foreach (var fact in facts)
      {
          if (fact == null) continue;
          if (wsm.GetFact(fact)) return true;
      }
      return false;
  }
  ```

- [ ] **Task 14: Update `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs`**
  - Add `using Game.Core;` at the top.
  - Add to `_cleanup` list pattern: all `CreateInstance` calls must be cleaned up in `TearDown`.
  - Replace all test bodies using the `CreateInstance<T>().Init(...)` pattern:
    - `_wsm.SetWorldEvent("test", true)` → `_wsm.SetWorldEvent(ScriptableObject.CreateInstance<WorldFact>().Init("test"), true)`
    - `_wsm.GetFact("World.test")` → `_wsm.GetFact(ScriptableObject.CreateInstance<WorldFact>().Init("test"))`
    - `_wsm.GetFact("World.nonexistent")` → `_wsm.GetFact(ScriptableObject.CreateInstance<WorldFact>().Init("nonexistent"))`
    - `_wsm.GetFact(null)` → `_wsm.GetFact((Fact)null)` — test renamed `GetFact_NullFact_ReturnsFalse`
    - `_wsm.SetQuestStep("Mill", "monster_killed", true)` → `_wsm.SetQuestStep(ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "monster_killed"), true)`
    - `_wsm.GetFact("Quest.Mill.monster_killed")` → `_wsm.GetFact(ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "monster_killed"))`
    - `_wsm.RegisterKill("StartingTown_NPC_Guard")` → `_wsm.RegisterKill(ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))`
    - `_wsm.IsKilled("StartingTown_NPC_Guard")` → `_wsm.IsKilled(ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))`
    - `_wsm.GetFact("Killed.StartingTown_NPC_Guard")` → `_wsm.GetFact(ScriptableObject.CreateInstance<KilledFact>().Init("StartingTown_NPC_Guard"))`
    - `_wsm.SetWorldEvent("mill_cleared", true)` → `_wsm.SetWorldEvent(ScriptableObject.CreateInstance<WorldFact>().Init("mill_cleared"), true)`
  - `received.key` event assertion: `Is.EqualTo("World.mill_cleared")` — **unchanged** (event payload is still a string key).
  - Add helper in test class to reduce verbosity (optional but recommended):
    ```csharp
    private T Fact<T>(System.Func<T> factory) where T : UnityEngine.Object
    {
        var f = factory();
        _cleanup.Add(f);
        return f;
    }
    ```

- [ ] **Task 15: Update `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs`**
  - Add `using Game.Core;`
  - Update WSM setup calls to typed facts (same `CreateInstance.Init` pattern).
  - Update `AllTrue`/`AnyTrue` calls to pass `Fact[]` instead of `string[]`.
  - Specific replacements:
    - `_wsm.SetQuestStep("Mill", "a", true)` → `_wsm.SetQuestStep(ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "a"), true)`
    - `_wsm.SetWorldEvent("b", true)` → `_wsm.SetWorldEvent(ScriptableObject.CreateInstance<WorldFact>().Init("b"), true)`
    - `_wsm.SetWorldEvent("mill_burned", true)` → `_wsm.SetWorldEvent(ScriptableObject.CreateInstance<WorldFact>().Init("mill_burned"), true)`
    - `_wsm.SetQuestStep("Mill", "monster_killed", true)` → `_wsm.SetQuestStep(ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "monster_killed"), true)`
    - `_wsm.SetWorldEvent("quest_failed", true)` → `_wsm.SetWorldEvent(ScriptableObject.CreateInstance<WorldFact>().Init("quest_failed"), true)`
    - `TopicUnlockEvaluator.AllTrue(new[] { "Quest.Mill.a", "World.b" })` →
      `TopicUnlockEvaluator.AllTrue(new Fact[] { ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "a"), ScriptableObject.CreateInstance<WorldFact>().Init("b") })`
    - All string array literals in `AllTrue`/`AnyTrue` calls updated similarly.
  - All fact instances created in tests must be added to `_cleanup` for `DestroyImmediate` in `TearDown`.

- [ ] **Task 16: Verify compilation and run tests**
  - `read_console` — zero errors after Tasks 1–15.
  - Run `WorldStateManagerFactsTests` and `TopicUnlockEvaluatorTests` in Unity Test Runner — all pass.
  - Enter Play Mode — no NullRef or missing-type errors in Console.

### Acceptance Criteria

- [ ] **AC 1**: Given a `QuestFact` asset with `_questId = "Mill"` and `_stepKey = "monster_killed"`, when `ToString()` is called, then the result is `"Quest.Mill.monster_killed"`.
- [ ] **AC 2**: Given a `KilledFact` asset with `_guid = "abc"`, when `ToString()` is called, then `"Killed.abc"`; when `EntityGuid` is accessed, then `"abc"`.
- [ ] **AC 3**: Given a `DialogueFact` asset with `_nodeId = "node_01"`, when `ToString()` is called, then `"Dialogue.Played.node_01"`.
- [ ] **AC 4**: Given `SetFact(worldFact, true)` is called, when `GetFact(worldFact)` is called, then it returns `true`.
- [ ] **AC 5**: Given `null` is passed to `GetFact(Fact)`, when called, then it returns `false` and logs a warning.
- [ ] **AC 6**: Given `SetWorldEvent(new WorldFact().Init("mill_cleared"), true)` is called, when the `_onWorldFactChanged` event fires, then `WorldFactData.key == "World.mill_cleared"` (string format preserved).
- [ ] **AC 7**: Given `NPCMemoryEntrySO.unlockConditions` contains a `QuestFact` asset and that fact is true in `WorldStateManager`, when `IsActive()` is called, then it returns `true`.
- [ ] **AC 8**: Given `PersistentID._killedFact` is assigned and `RegisterDeath()` is called, when `WorldStateManager.IsKilled(_killedFact)` is called afterward, then it returns `true`.
- [ ] **AC 9**: Given `StartDialogueNode.dialogueFact` is assigned and `NotifyTopicCompleted()` is called on a non-repeatable node, when `WorldStateManager.IsDialoguePlayed(dialogueFact)` is called, then it returns `true`.
- [ ] **AC 10**: Given all 15 tasks are complete, when Edit Mode tests in `WorldStateManagerFactsTests` and `TopicUnlockEvaluatorTests` are run, then all pass.

---

## Additional Context

### Dependencies

- No new Unity packages required.
- `WorldFactPrefix.cs` must compile before `Fact.cs` and subclasses. Unity compiles all in one pass — no issue.
- All five `Fact` files (Tasks 1–5) must compile before `WorldStateManager` refactor (Task 7).

### Testing Strategy

- **Compile check**: After Tasks 1–7, `read_console` must show zero errors before touching callers.
- **Test pattern**: `ScriptableObject.CreateInstance<T>().Init(...)` for all fact creation in tests.
  Every created instance must be added to `_cleanup` and destroyed in `TearDown`.
- **Existing test logic preserved**: All existing test scenarios remain — only the API call syntax changes.
- **Manual smoke**: Enter Play Mode, interact with an NPC that has memory conditions — confirm correct
  activation/invalidation behavior.

### Notes

**Designer migration (not in this story's scope):**
After implementation, designers must:
1. Create `KilledFact` assets (one per tracked entity) and assign to `PersistentID` components in prefabs/scenes
2. Create `DialogueFact` assets for non-repeatable `StartDialogueNode` SOs and assign `dialogueFact`
3. Replace `string[]` entries in `NPCMemoryEntrySO.unlockConditions`/`invalidationConditions` with `Fact[]` asset references

Existing string values will be lost from those fields — document the migration before implementing.

**Save system compatibility (Epic 8):**
Internal `Dictionary<string, bool>` is unchanged. `WorldStateSaveData` serializes the same string keys.
When Epic 8 is implemented, `Fact.ToString()` output is the serialized form — no migration needed.

**Future `SkillFact` / `StatFact`:**
Add them as new `Fact : ScriptableObject` subclasses. `WorldStateManager.GetFact(Fact)` can dispatch
on `fact.Prefix` to read from a different source (e.g. `PlayerSkills`) when those facts arrive.
`[CreateAssetMenu]` and `Init()` follow the same pattern established here.

**`OnEnable()` sets `Prefix`:**
`Prefix` is not `[SerializeField]` — it's set in `Init()` for runtime instances and in `OnEnable()` for
asset-loaded instances. This ensures `Prefix` is always valid regardless of how the fact was created.
