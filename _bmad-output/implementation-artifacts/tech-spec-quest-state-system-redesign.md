---
title: 'Quest State System Redesign'
slug: 'quest-state-system-redesign'
created: '2026-04-14'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C# 9', 'URP 17.x', 'ScriptableObject event channels']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Facts/QuestFact.cs'
  - 'Assets/_Game/ScriptableObjects/Quest/QuestSO.cs'
  - 'Assets/_Game/Scripts/Core/State/WorldStateManager.cs'
  - 'Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs'
  - 'Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs'
  - 'Assets/Tests/EditMode/WorldStateManagerFactsTests.cs'
code_patterns:
  - 'Computed facts (SkillFact/StatFact): not stored in _worldFacts, evaluated at runtime via WorldStateManager intermediary'
  - 'GameEventSO<T> typed channels: one concrete subclass per type in its own .cs file'
  - 'MonoBehaviour singletons: OnEnable/OnDisable for event subscriptions, DontDestroyOnLoad not used for scene managers'
  - 'TopicUnlockEvaluator: pattern-match switch on Fact subtype, WorldStateManager as intermediary'
test_patterns:
  - 'EditMode tests: AddComponent on temp GO, reflection to inject _fields and force Instance, _cleanup List<Object> + DestroyImmediate in TearDown'
  - 'MakeFact<T> helper pattern for cleanup-tracked SO instances'
---

# Tech-Spec: Quest State System Redesign

**Created:** 2026-04-14

## Overview

### Problem Statement

The current `QuestFact` is a "stored" fact (`Quest.{questId}.{stepKey}` key in `WorldStateManager._worldFacts`), disconnected from `QuestSO`. `QuestSO` is a dumb data bag with no state awareness. No mechanism exists to evaluate whether a quest is started, completed, or failed in response to world state changes, and no quest transition event system exists.

### Solution

Redesign `QuestFact` as a **computed fact** (like `SkillFact`/`StatFact`) that holds a reference to a `QuestSO` + a `QuestState` enum value (`IsStarted`/`IsCompleted`/`IsFailed`). Redesign `QuestSO` to own its state conditions (`startFact`, `completedFacts`, `failedFacts`) and expose computed `IsStarted`/`IsCompleted`/`IsFailed` properties backed by `WorldStateManager.Instance`. Add `WorldStateManager.IsQuestFactTrue()` as the intermediary. Add a `QuestFact` branch to `TopicUnlockEvaluator`. Introduce `QuestEventsManager` as a standalone scene `MonoBehaviour` that watches for world fact changes and fires typed `GameEventSO<QuestSO>` events on quest state transitions.

### Scope

**In Scope:**
- Redesign `QuestFact.cs`: remove `_questId`/`_stepKey`, add `_quest` (QuestSO) + `_questState` (QuestState enum)
- Redesign `QuestSO.cs`: add `startFact`, `completedFacts`, `failedFacts` + computed `IsStarted`/`IsCompleted`/`IsFailed`
- Add `WorldStateManager.IsQuestFactTrue(QuestFact)` method
- Update `TopicUnlockEvaluator.AllTrue()` and `AnyTrue()` with `QuestFact` case
- Create `GameEventSO_Quest.cs` typed event channel (separate file — project rule)
- Create `Scripts/Quest/QuestEventsManager.cs` MonoBehaviour
- Update existing tests that use old `QuestFact.Init(string, string)` signature
- Add new EditMode tests for `IsQuestFactTrue` and `QuestEventsManager`
- Editor work: reassign `QuestFact_FindHerbalist_Started.asset` fields; create QuestEventsManager GO + event channel assets

**Out of Scope:**
- Quest Log UI (future story)
- Save/Load integration for quest state (Epic 8)
- `QuestDialogueNode` — removed by design; quests are triggered via facts set by existing nodes (`StartDialogueNode`, `ChoiceDialogueNode`, `KilledFact` generators, etc.)
- `WorldStateManager.SetQuestStep()` — already absent from codebase; no replacement needed (the start fact is set through existing fact mechanisms)

---

## Context for Development

### Codebase Patterns

**Computed vs Stored Facts:**
- **Stored facts** (`WorldFact`, `DialogueFact`, `KilledFact`): have a string key, stored in `WorldStateManager._worldFacts`, queried via `GetFact(fact)`.
- **Computed facts** (`SkillFact`, `StatFact`): NOT stored in `_worldFacts`. Evaluated at runtime via `WorldStateManager` intermediary methods (`PlayerHasSkill`, `PlayerStatCheck`). `ToString()` is debug-only, not a dict key.
- **QuestFact must follow the computed pattern.** After this spec, `QuestFact` evaluation goes through `WorldStateManager.IsQuestFactTrue()`, not `GetFact()`. The `WorldFactPrefix.Quest` on QuestFact can remain set for debug display parity, but it is never used as a dict key.

**TopicUnlockEvaluator switch pattern (both `AllTrue` and `AnyTrue`):**
```csharp
bool result = fact switch
{
    SkillFact sf  => wsm.PlayerHasSkill(sf),
    StatFact  stf => wsm.PlayerStatCheck(stf),
    _             => wsm.GetFact(fact)      // stored facts fallthrough
};
```
The `QuestFact` case must be inserted **before** the `_` wildcard arm.

**GameEventSO file separation rule (project memory):**
Each concrete `GameEventSO<T>` subclass must be in its own `.cs` file. Sharing a file breaks `m_Script` references on domain reload. `GameEventSO_Quest` goes in `Assets/_Game/ScriptableObjects/Events/GameEventSO_Quest.cs`.

**MonoBehaviour event subscription pattern:**
```csharp
private void OnEnable()  => _channel.AddListener(Handler);
private void OnDisable() => _channel.RemoveListener(Handler);
```
With OnDisable null guard if field may be null on Awake-disable.

**QuestSO namespace:** `Game.Quest` (matches existing file). WorldStateManager is `Game.Core`. `QuestSO` calling `WorldStateManager.Instance` requires `using Game.Core;` in QuestSO.

**QuestFact namespace:** `Game.Core` (matches existing file). `QuestFact` referencing `QuestSO` requires `using Game.Quest;` — precedent set by `SkillFact` using `using Game.Progression;`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs` | **Redesign** — computed fact |
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | **Redesign** — add state conditions + computed properties |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | **Extend** — add `IsQuestFactTrue()` |
| `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs` | **Extend** — add QuestFact case |
| `Assets/_Game/ScriptableObjects/Facts/SkillFact.cs` | Reference: computed fact pattern |
| `Assets/_Game/ScriptableObjects/Facts/StatFact.cs` | Reference: computed fact pattern |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_WorldFact.cs` | Reference: concrete GameEventSO pattern |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Reference: base GameEventSO<T> |
| `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs` | **Update** — fix broken QuestFact.Init calls |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | **Update** — fix broken QuestFact.Init calls |
| `Assets/_Game/Data/Quests/find herbalist/QuestFact_FindHerbalist_Started.asset` | **Update** in Editor — reassign fields |

### Technical Decisions

1. **QuestState enum lives in `QuestFact.cs` (namespace `Game.Core`).** Both `WorldStateManager` (Game.Core) and `QuestFact` (Game.Core) use it; no cross-namespace import needed for those files. `QuestEventsManager` (Game.Quest) does not need to reference `QuestState` directly.

2. **QuestSO.IsStarted/IsCompleted/IsFailed call WorldStateManager.Instance directly.** This mirrors the project pattern where domain SOs reference runtime managers. Guard: `if (WorldStateManager.Instance == null) return false`.

3. **IsCompleted with empty `completedFacts` returns `false`.** An empty list means "no completion conditions defined" — the quest cannot complete via this mechanism. This differs from `TopicUnlockEvaluator.AllTrue` (empty = trivially true) because for quests, "no conditions" means the quest is not in that state.

4. **QuestEventsManager is a standalone GO (not attached to WorldStateManager GO).** Placed on a new `QuestEventsManager` GO in the same scene as WorldStateManager (or in a dedicated `Quest` scene in the future).

5. **QuestEventsManager.Start() seeds `_lastState` without firing events.** This prevents spurious "quest started" events on scene load if a quest was already started (e.g., future save/load restore).

6. **GameEventSO_Quest lives in `Game.Quest` namespace** (not `Game.Core`) to avoid introducing a backward dependency from Game.Core → Game.Quest.

7. **`WorldFactPrefix.Quest` is kept on QuestFact** (`OnEnable` sets it, same as before) for debug display consistency, but it is never used as a `_worldFacts` key.

---

## Implementation Plan

### Tasks

- [x] **Task 1 — Add `QuestState` enum and redesign `QuestFact.cs`** (AC: #1, #2)
  - File: `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs`
  - Add `using Game.Quest;` import
  - Add `QuestState` enum **above** the class declaration (in namespace `Game.Core`):
    ```csharp
    public enum QuestState { IsStarted, IsCompleted, IsFailed }
    ```
  - Remove fields: `[SerializeField] private string _questId;` and `[SerializeField] private string _stepKey;`
  - Remove method: `public QuestFact Init(string questId, string stepKey)`
  - Add fields:
    ```csharp
    [SerializeField] private QuestSO _quest;
    [SerializeField] private QuestState _questState;
    ```
  - Add properties:
    ```csharp
    public QuestSO Quest => _quest;
    public QuestState QuestState => _questState;
    ```
  - Add new `Init` (for tests):
    ```csharp
    public QuestFact Init(QuestSO quest, QuestState state)
    {
        _quest = quest;
        _questState = state;
        return this;
    }
    ```
  - Update `OnEnable`: keep `Prefix = WorldFactPrefix.Quest;` (debug display only)
  - Update `ToString()`: `=> $"Quest.{_quest?.questId ?? "null"}.{_questState}";`
  - Note: QuestFact is now a computed fact — NOT stored in `_worldFacts`. `ToString()` is debug-only.

- [x] **Task 2 — Redesign `QuestSO.cs`** (AC: #3)
  - File: `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs`
  - Add import: `using System.Collections.Generic;` and `using Game.Core;`
  - Keep existing fields: `questId`, `title`, `description`
  - Add after `description`:
    ```csharp
    [Header("Quest State Conditions")]
    [Tooltip("When this fact is true, the quest is considered started.")]
    public Fact startFact;

    [Tooltip("When ALL facts in this list are true, the quest is considered completed. Empty = never completed.")]
    public List<Fact> completedFacts = new List<Fact>();

    [Tooltip("When ANY fact in this list is true, the quest is considered failed. Empty = never failed.")]
    public List<Fact> failedFacts = new List<Fact>();
    ```
  - Add computed properties:
    ```csharp
    /// <summary>True if startFact is set to true in WorldStateManager.</summary>
    public bool IsStarted
    {
        get
        {
            if (startFact == null || WorldStateManager.Instance == null) return false;
            return WorldStateManager.Instance.GetFact(startFact);
        }
    }

    /// <summary>True if all completedFacts are true. Returns false if list is empty.</summary>
    public bool IsCompleted
    {
        get
        {
            if (completedFacts == null || completedFacts.Count == 0) return false;
            if (WorldStateManager.Instance == null) return false;
            foreach (var f in completedFacts)
                if (f == null || !WorldStateManager.Instance.GetFact(f)) return false;
            return true;
        }
    }

    /// <summary>True if any failedFact is true. Returns false if list is empty.</summary>
    public bool IsFailed
    {
        get
        {
            if (failedFacts == null || WorldStateManager.Instance == null) return false;
            foreach (var f in failedFacts)
                if (f != null && WorldStateManager.Instance.GetFact(f)) return true;
            return false;
        }
    }
    ```

- [x] **Task 3 — Add `IsQuestFactTrue()` to `WorldStateManager.cs`** (AC: #4)
  - File: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`
  - Add `using Game.Quest;` to imports
  - Add method in the `// ── Typed convenience methods ──` section (after `PlayerStatCheck`):
    ```csharp
    /// <summary>Evaluates a QuestFact by delegating to the referenced QuestSO's computed properties.
    /// QuestFacts are NOT stored in _worldFacts — always evaluate via this method.</summary>
    public bool IsQuestFactTrue(QuestFact fact)
    {
        if (fact == null) { GameLog.Warn(TAG, "IsQuestFactTrue called with null fact"); return false; }
        if (fact.Quest == null) { GameLog.Warn(TAG, "QuestFact.Quest is null — assign a QuestSO in the Inspector"); return false; }
        return fact.QuestState switch
        {
            QuestState.IsStarted   => fact.Quest.IsStarted,
            QuestState.IsCompleted => fact.Quest.IsCompleted,
            QuestState.IsFailed    => fact.Quest.IsFailed,
            _                      => false
        };
    }
    ```

- [x] **Task 4 — Update `TopicUnlockEvaluator.cs` with QuestFact case** (AC: #5)
  - File: `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs`
  - In `AllTrue()`, update the switch expression — insert `QuestFact` arm before the `_` wildcard:
    ```csharp
    bool result = fact switch
    {
        SkillFact sf  => wsm.PlayerHasSkill(sf),
        StatFact  stf => wsm.PlayerStatCheck(stf),
        QuestFact qf  => wsm.IsQuestFactTrue(qf),
        _             => wsm.GetFact(fact)
    };
    ```
  - Apply the **same change** to `AnyTrue()` — identical switch expression
  - No additional `using` needed: `QuestFact` and `QuestState` are both in `Game.Core`, same namespace as TopicUnlockEvaluator's imports

- [x] **Task 5 — Create `GameEventSO_Quest.cs`** (AC: #6)
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_Quest.cs` (new file)
  - Must be in its own `.cs` file (project rule: SO subclasses in separate files)
  - Content:
    ```csharp
    using Game.Core;
    using UnityEngine;

    namespace Game.Quest
    {
        /// <summary>Typed event channel for quest state transitions (started, completed, failed).</summary>
        [CreateAssetMenu(menuName = "Game/Events/Quest Event", fileName = "OnQuest")]
        public class GameEventSO_Quest : GameEventSO<QuestSO> { }
    }
    ```

- [x] **Task 6 — Create `QuestEventsManager.cs`** (AC: #7, #8)
  - File: `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` (new file — create folder `Scripts/Quest/`)
  - Content:
    ```csharp
    using System.Collections.Generic;
    using Game.Core;
    using UnityEngine;

    namespace Game.Quest
    {
        /// <summary>
        /// Monitors registered quests for state transitions (started, completed, failed)
        /// by reacting to world fact changes. Fires GameEventSO_Quest channels on each transition.
        /// Attach to a standalone QuestEventsManager GameObject in the scene.
        /// </summary>
        public class QuestEventsManager : MonoBehaviour
        {
            private const string TAG = "[QuestEvents]";

            [SerializeField] private List<QuestSO> _quests = new List<QuestSO>();
            [SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;

            [Header("Output Event Channels")]
            [SerializeField] private GameEventSO_Quest _onQuestStarted;
            [SerializeField] private GameEventSO_Quest _onQuestCompleted;
            [SerializeField] private GameEventSO_Quest _onQuestFailed;

            private readonly Dictionary<QuestSO, QuestStateSnapshot> _lastState
                = new Dictionary<QuestSO, QuestStateSnapshot>();

            private struct QuestStateSnapshot
            {
                public bool started;
                public bool completed;
                public bool failed;
            }

            private void Start()
            {
                // Seed initial state without firing events (prevents spurious transitions on scene load).
                foreach (var quest in _quests)
                {
                    if (quest == null) continue;
                    _lastState[quest] = new QuestStateSnapshot
                    {
                        started   = quest.IsStarted,
                        completed = quest.IsCompleted,
                        failed    = quest.IsFailed
                    };
                }
            }

            private void OnEnable()
            {
                if (_onWorldFactChanged == null)
                {
                    GameLog.Warn(TAG, "OnWorldFactChanged not assigned — QuestEventsManager will not respond to fact changes");
                    return;
                }
                _onWorldFactChanged.AddListener(HandleWorldFactChanged);
            }

            private void OnDisable()
            {
                if (_onWorldFactChanged == null) return;
                _onWorldFactChanged.RemoveListener(HandleWorldFactChanged);
            }

            private void HandleWorldFactChanged(WorldFactData _)
            {
                foreach (var quest in _quests)
                {
                    if (quest == null) continue;
                    EvaluateQuest(quest);
                }
            }

            private void EvaluateQuest(QuestSO quest)
            {
                bool isStarted   = quest.IsStarted;
                bool isCompleted = quest.IsCompleted;
                bool isFailed    = quest.IsFailed;

                if (!_lastState.TryGetValue(quest, out var prev))
                    prev = default;

                if (!prev.started && isStarted)
                {
                    GameLog.Info(TAG, $"Quest started: '{quest.title}'");
                    _onQuestStarted?.Raise(quest);
                }
                if (!prev.completed && isCompleted)
                {
                    GameLog.Info(TAG, $"Quest completed: '{quest.title}'");
                    _onQuestCompleted?.Raise(quest);
                }
                if (!prev.failed && isFailed)
                {
                    GameLog.Info(TAG, $"Quest failed: '{quest.title}'");
                    _onQuestFailed?.Raise(quest);
                }

                _lastState[quest] = new QuestStateSnapshot
                {
                    started   = isStarted,
                    completed = isCompleted,
                    failed    = isFailed
                };
            }
        }
    }
    ```

- [x] **Task 7 — Update `TopicUnlockEvaluatorTests.cs`** (AC: #9)
  - File: `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs`
  - Locate test `AnyTrue_NoFactsTrue_ReturnsFalse` — it creates a QuestFact with old Init: `ScriptableObject.CreateInstance<QuestFact>().Init("Mill", "x")`
  - Replace with a valid computed QuestFact. Since the test verifies "no facts true", use a WorldFact substitute or create a QuestSO whose startFact is not set:
    ```csharp
    // Create a QuestSO with no startFact (IsStarted will always be false)
    var questSO = ScriptableObject.CreateInstance<QuestSO>();
    _cleanup.Add(questSO);
    var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>()
        .Init(questSO, QuestState.IsStarted));
    ```
  - Add `using Game.Quest;` to imports if not present
  - Add new test `AllTrue_QuestFact_QuestStarted_ReturnsTrue`:
    ```csharp
    [Test]
    public void AllTrue_QuestFact_QuestStarted_ReturnsTrue()
    {
        // Arrange: QuestSO with a WorldFact as startFact; set that fact true
        var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_quest_start"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.startFact = startFact;
        _cleanup.Add(questSO);
        _wsm.SetWorldEvent(startFact, true);

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));

        Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { questFact }), Is.True);
    }

    [Test]
    public void AllTrue_QuestFact_QuestNotStarted_ReturnsFalse()
    {
        var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_quest_start"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.startFact = startFact;
        _cleanup.Add(questSO);
        // startFact NOT set — quest not started

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));

        Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { questFact }), Is.False);
    }
    ```

- [x] **Task 8 — Update `WorldStateManagerFactsTests.cs`** (AC: #9)
  - File: `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs`
  - Check if any test uses `QuestFact.Init(string, string)` — if found, update to `Init(QuestSO, QuestState)` (same pattern as Task 7)
  - Add new tests for `IsQuestFactTrue`:
    ```csharp
    // ── IsQuestFactTrue ───────────────────────────────────────────────────────

    [Test]
    public void IsQuestFactTrue_Started_WhenStartFactSet_ReturnsTrue()
    {
        var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_start"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.startFact = startFact;
        _cleanup.Add(questSO);
        _wsm.SetWorldEvent(startFact, true);

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
    }

    [Test]
    public void IsQuestFactTrue_Started_WhenStartFactNotSet_ReturnsFalse()
    {
        var startFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_start"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.startFact = startFact;
        _cleanup.Add(questSO);
        // NOT setting startFact

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsStarted));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
    }

    [Test]
    public void IsQuestFactTrue_Completed_AllFactsTrue_ReturnsTrue()
    {
        var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herb_delivered"));
        var f2 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("elder_thanked"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.completedFacts.Add(f1);
        questSO.completedFacts.Add(f2);
        _cleanup.Add(questSO);
        _wsm.SetWorldEvent(f1, true);
        _wsm.SetWorldEvent(f2, true);

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsCompleted));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
    }

    [Test]
    public void IsQuestFactTrue_Completed_EmptyFacts_ReturnsFalse()
    {
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        _cleanup.Add(questSO);
        // completedFacts is empty

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsCompleted));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
    }

    [Test]
    public void IsQuestFactTrue_Failed_AnyFactTrue_ReturnsTrue()
    {
        var failFact = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("herbalist_dead"));
        var questSO = ScriptableObject.CreateInstance<QuestSO>();
        questSO.failedFacts.Add(failFact);
        _cleanup.Add(questSO);
        _wsm.SetWorldEvent(failFact, true);

        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(questSO, QuestState.IsFailed));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
    }

    [Test]
    public void IsQuestFactTrue_NullFact_ReturnsFalse()
    {
        Assert.That(_wsm.IsQuestFactTrue(null), Is.False);
    }

    [Test]
    public void IsQuestFactTrue_NullQuest_ReturnsFalse()
    {
        var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().Init(null, QuestState.IsStarted));
        Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
    }
    ```
  - Add `using Game.Quest;` to imports

- [x] **Task 9 — Editor work: update `QuestFact_FindHerbalist_Started.asset`** (AC: #10)
  - Asset: `Assets/_Game/Data/Quests/find herbalist/QuestFact_FindHerbalist_Started.asset`
  - The old `_questId`/`_stepKey` fields will be gone after Task 1 domain reload — the asset will show missing fields
  - Open in Inspector → assign: `_quest = Quest_FindHerbalist.asset` (in same folder), `_questState = IsStarted`
  - Note: `Quest_FindHerbalist.asset` is in `Assets/_Game/Data/Quests/find herbalist/` — assign its `startFact` field to the `QuestFact_FindHerbalist_Started` asset's owning fact (e.g., a WorldFact such as `WorldFact_FindHerbalist_Started.asset` that gets set by dialogue). If no such WorldFact exists yet, create `WorldFact_FindHerbalist_Started.asset` via `Create > Game/Facts/World Fact`, set `_eventKey = "FindHerbalist_Started"`, then assign it as `Quest_FindHerbalist.startFact`.
  - Summary of assets to create/assign in Editor:
    - Create `Assets/_Game/Data/Facts/Quests/WorldFact_FindHerbalist_Started.asset` (`_eventKey = "FindHerbalist_Started"`)
    - Assign `Quest_FindHerbalist.startFact = WorldFact_FindHerbalist_Started.asset`
    - Reassign `QuestFact_FindHerbalist_Started._quest = Quest_FindHerbalist.asset`, `_questState = IsStarted`
    - Update any dialogue node that previously held a reference to the old `QuestFact_FindHerbalist_Started` to instead set `WorldFact_FindHerbalist_Started` (via `dialogueFact` on a `StartDialogueNode` or `ChoiceOption`)

- [x] **Task 10 — Editor work: create QuestEventsManager GO + event channel assets** (AC: #7)
  - Create event channel assets:
    - `Assets/_Game/Data/Events/OnQuestStarted.asset` (type: GameEventSO_Quest)
    - `Assets/_Game/Data/Events/OnQuestCompleted.asset` (type: GameEventSO_Quest)
    - `Assets/_Game/Data/Events/OnQuestFailed.asset` (type: GameEventSO_Quest)
  - Create new `QuestEventsManager` GameObject in the scene (same scene as WorldStateManager)
  - Add `QuestEventsManager` component; assign:
    - `_quests`: add `Quest_FindHerbalist.asset`
    - `_onWorldFactChanged`: assign existing `OnWorldFactChanged.asset`
    - `_onQuestStarted`: assign `OnQuestStarted.asset`
    - `_onQuestCompleted`: assign `OnQuestCompleted.asset`
    - `_onQuestFailed`: assign `OnQuestFailed.asset`

### Acceptance Criteria

- [x] **AC1** — Given `QuestFact.cs`, when inspected, then it has `_quest` (QuestSO) and `_questState` (QuestState enum) fields; `_questId` and `_stepKey` are gone. `ToString()` returns `"Quest.{questId}.{state}"`.
- [x] **AC2** — Given a `QuestFact` created via `Init(questSO, QuestState.IsStarted)`, when `ToString()` is called, then it returns `"Quest.FindHerbalist.IsStarted"` (matching questSO.questId).
- [x] **AC3** — Given `QuestSO`, when inspected, then it has `startFact`, `completedFacts`, `failedFacts` fields. `IsStarted` returns true iff `WorldStateManager.Instance.GetFact(startFact)` is true.
- [x] **AC4** — Given a `QuestSO` with `completedFacts = []`, when `IsCompleted` is called, then it returns `false` (empty list is not "all true").
- [x] **AC5** — Given `TopicUnlockEvaluator.AllTrue([questFact])` where `questFact.QuestState = IsStarted` and the quest's startFact is true in WorldStateManager, then it returns `true`. Without the startFact set, returns `false`.
- [x] **AC6** — Given a `GameEventSO_Quest` asset created via `Create > Game/Events/Quest Event`, when `Raise(questSO)` is called, then all registered listeners receive the `QuestSO` payload.
- [x] **AC7** — Given `QuestEventsManager` with `Quest_FindHerbalist` registered and `_onWorldFactChanged` assigned, when the Find Herbalist `startFact` is set to true via `WorldStateManager.SetWorldEvent(startFact, true)`, then `_onQuestStarted.Raise(quest)` is called exactly once.
- [x] **AC8** — Given `QuestEventsManager.Start()` is called while the quest is already started, then subsequent world fact changes do NOT re-fire `_onQuestStarted` (state seeded correctly).
- [x] **AC9** — All existing EditMode tests pass (zero regressions). New tests for `IsQuestFactTrue` all pass: null-fact, null-quest, Started/Completed/Failed states. TopicUnlockEvaluator tests with QuestFact pass.
- [x] **AC10** — Given Unity Editor after implementation, `QuestFact_FindHerbalist_Started.asset` is inspectable with `_quest = Quest_FindHerbalist.asset` and `_questState = IsStarted`; no missing-script or missing-reference errors.

---

## Additional Context

### Dependencies

- `GameEventSO_Quest` must compile before `QuestEventsManager` (same compilation unit in Game assembly — just ensure `GameEventSO_Quest.cs` is created before testing)
- `QuestState` enum in `QuestFact.cs` must be compiled before `WorldStateManager.cs` uses it (all in Game assembly — same compilation pass)
- `Assets/_Game/Data/Events/OnWorldFactChanged.asset` must exist before wiring QuestEventsManager (it already exists per project conventions)

### Testing Strategy

**EditMode tests (Tasks 7 & 8):**
- Follow the `WorldStateManagerFactsTests.cs` reflection pattern: `AddComponent<WorldStateManager>()`, force-inject `<Instance>k__BackingField` via reflection, `DestroyImmediate` in TearDown
- Use `MakeFact<T>` helper for cleanup-tracked SO instances
- `QuestSO` instances: `ScriptableObject.CreateInstance<QuestSO>()`, added to `_cleanup`, fields assigned directly (public fields)

**QuestEventsManager testing:**
- No MonoBehaviour-level test required in this story — the event firing is validated through manual playtest and the fact-evaluation logic is covered by `IsQuestFactTrue` EditMode tests
- Manual playtest: set `WorldFact_FindHerbalist_Started = true` via a dialogue choice, verify `OnQuestStarted` fires (add a debug listener or check console log from QuestEventsManager)

**Regression scope:**
- Run the full EditMode test suite after Task 1 (QuestFact redesign) — the biggest breaking change
- `TopicUnlockEvaluatorTests.AnyTrue_NoFactsTrue_ReturnsFalse` will fail until Task 7 is done

### Notes

- **Breaking change — QuestFact asset format:** Any existing `QuestFact_*.asset` with old `_questId`/`_stepKey` serialization will lose their values on domain reload after Task 1. The only such asset in the project is `QuestFact_FindHerbalist_Started.asset`. Handle in Task 9.
- **WorldStateManager.SetQuestStep():** Confirmed absent from codebase. No removal needed.
- **`WorldFactPrefix.Quest` in enum:** Keep this enum value — QuestFact still sets it in `OnEnable()` for debug display consistency. It is never used as a `_worldFacts` key.
- **Future story:** Quest Log UI panel + J key binding. `QuestEventsManager` already fires the events; the UI story just needs to listen to `OnQuestStarted/Completed/Failed`.
- **Future story:** Multiple quests managed by QuestEventsManager — `_quests` list is already a List, so adding quests is an Inspector operation only.
- **High-risk item:** QuestSO calling `WorldStateManager.Instance` from a ScriptableObject. If accessed outside of Play mode (e.g. in Editor scripts or custom property drawers), `Instance` will be null and the properties return false. This is intentional and safe — never call IsStarted/IsCompleted/IsFailed from Editor code.
