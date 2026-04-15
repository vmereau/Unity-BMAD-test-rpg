---
title: 'Quest Step System'
slug: 'quest-step-system'
created: '2026-04-15'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7]
tech_stack: ['C#', 'Unity 6', 'ScriptableObjects', 'UnityEditor']
files_to_modify:
  - Assets/_Game/ScriptableObjects/Quest/QuestSO.cs
  - Assets/_Game/ScriptableObjects/Facts/QuestFact.cs
  - Assets/_Game/Scripts/Core/State/WorldStateManager.cs
  - Assets/_Game/Scripts/Quest/QuestEventsManager.cs
  - Assets/Tests/EditMode/WorldStateManagerFactsTests.cs
code_patterns:
  - QuestStep struct with title/description/List<QuestPart>
  - int-backed dynamic enum for QuestFact._questState
  - CustomEditor with conditional popup for step selection
  - QuestEventsManager snapshot with per-step bool array
test_patterns:
  - EditMode NUnit using AddComponent + reflection instance injection
  - MakeFact<T> helper for cleanup registration
---

# Tech-Spec: Quest Step System

**Created:** 2026-04-15

## Overview

### Problem Statement

The current `QuestSO` has flat `completedParts` / `failedParts` lists but no concept of intermediate progress stages. There is no way to define named sub-goals (steps) within a quest, track their completion independently, or reference a specific step completion from a `QuestFact`.

### Solution

Add a `QuestStep` struct to `QuestSO` containing a title, description, and a list of `QuestPart`s. Extend `QuestFact` to reference individual steps via a dynamic inspector popup (int-backed field replacing the compile-time `QuestState` enum). Wire `WorldStateManager.IsQuestFactTrue` to evaluate step completion. Add step-completion tracking and event firing to `QuestEventsManager`.

### Scope

**In Scope:**
- `QuestStep` struct definition inside `QuestSO.cs`
- `QuestSO.steps` list field and `IsStepCompleted(int stepIndex)` method
- Replace `QuestFact._questState` (typed `QuestState`) with `int _questState` (backward-compatible serialization: 0=IsStarted, 1=IsCompleted, 2=IsFailed, 3+=step[i-3])
- `QuestFact.IsStepState` and `QuestFact.QuestStepIndex` properties
- `QuestFactEditor` (CustomEditor): conditional popup showing base states + step titles; `_questState` disabled until `_quest` is set
- `WorldStateManager.IsQuestFactTrue` — add step-state branch calling `IsStepCompleted`
- `GameEventSO_QuestStep` new typed event SO (carries `QuestStepData` = quest + stepIndex)
- `QuestEventsManager` — add `_onQuestStepCompleted` channel + per-step snapshot tracking
- New EditMode tests for step evaluation in `WorldStateManagerFactsTests`

**Out of Scope:**
- Quest log UI rendering of steps (Story 6-5)
- Save/Load of step completion state (Epic 8)
- Reordering or enabling/disabling individual steps at runtime
- Existing test files `TopicUnlockEvaluatorTests.cs` — no changes needed

---

## Context for Development

### Codebase Patterns

- **GameEventSO rule**: concrete event SO subclasses must live in their own `.cs` file (feedback rule — `GameEventSO_Quest.cs` pattern). Never combine multiple concrete types in one file.
- **Serialized enum → int migration**: Unity serializes enums as ints. Changing field type from `QuestState` to `int` while keeping the same field name `_questState` preserves existing asset data (0/1/2 values stay valid). No `[FormerlySerializedAs]` needed.
- **CustomEditor pattern**: project uses `CustomEditor` (not `PropertyDrawer`) for SO-level conditional inspector logic — see `NPCMemoryEntrySO_Editor.cs`. For struct-level conditional drawing, `PropertyDrawer` is used — see `TeachChoiceOptionDrawer.cs`. Since `QuestFact` conditional logic depends on another field on the same SO, use `CustomEditor`.
- **Editor file location**: all editor scripts live under `Assets/_Game/Scripts/Editor/`, namespace `Game.Editor`, wrapped in `#if UNITY_EDITOR`.
- **EditMode tests**: use `AddComponent<WorldStateManager>()` + reflection to force-set `<Instance>k__BackingField`. Always register assets with `_cleanup` list.
- **Assembly**: all game code in `Assets/_Game/` compiles into `Game` assembly. Editor scripts in `Assets/_Game/Scripts/Editor/` must NOT be referenced from runtime code.
- **`QuestState` enum**: currently defined at the top of `QuestFact.cs` in `namespace Game.Core`. Keep it there — it is used by `Init(QuestSO, QuestState)` and existing tests.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` | Add QuestStep struct + steps list + IsStepCompleted method |
| `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs` | Change _questState field type; add IsStepState/QuestStepIndex properties |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | Update IsQuestFactTrue to branch on IsStepState |
| `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` | Add step event channel + per-step snapshot |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Quest.cs` | Reference for event SO pattern |
| `Assets/_Game/Scripts/Editor/NPCMemoryEntrySO_Editor.cs` | Reference for CustomEditor pattern |
| `Assets/_Game/Scripts/Editor/TeachChoiceOptionDrawer.cs` | Reference for conditional PropertyDrawer (serializedProperty approach) |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | Extend with step tests |

### Technical Decisions

- **Int-backed `_questState`** (not a new field name): Keeps serialized asset data valid. `(QuestState)_questState` cast works for values 0–2. `IsStepState = _questState >= 3`. Step index = `_questState - 3`.
- **`QuestFactEditor` uses `SerializedProperty`** to properly handle Undo/Redo and mark the asset dirty. Access `_quest` and `_questState` via `serializedObject.FindProperty`.
- **`GameEventSO_QuestStep` payload struct `QuestStepData`**: carries `QuestSO quest` + `int stepIndex`. Defined in the same file. Not a MonoBehaviour or SO — a plain `[System.Serializable]` struct. Enables listeners to know which specific step completed.
- **`QuestEventsManager` snapshot**: extend `QuestStateSnapshot` to include `bool[] stepCompleted`. Initialize in `Start()` using `quest.steps.Count`. Resize defensively on evaluate if quest was modified.

---

## Implementation Plan

### Tasks

Tasks are ordered dependency-first.

---

**Task 1 — Add `QuestStep` struct and `steps` field to `QuestSO.cs`**

File: `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs`

1. Add `QuestStep` struct **inside** `namespace Game.Quest`, **before** `QuestSO` class (same file as `QuestPart`):
   ```csharp
   [System.Serializable]
   public struct QuestStep
   {
       [Tooltip("Short name shown in inspector dropdown and quest log.")]
       public string title;

       [Tooltip("Longer description of this step's objective.")]
       [TextArea(1, 3)]
       public string description;

       [Tooltip("All parts must be true for this step to be considered completed.")]
       public List<QuestPart> parts;
   }
   ```

2. Add field to `QuestSO` after `failedParts`:
   ```csharp
   [Header("Quest Steps")]
   [Tooltip("Optional sub-goals. Each step is completed when all its parts' facts are true.")]
   public List<QuestStep> steps = new List<QuestStep>();
   ```

3. Add method to `QuestSO`:
   ```csharp
   /// <summary>True if all parts in the step at <paramref name="stepIndex"/> are true. Returns false if index is out of range, parts list is empty, or WSM is unavailable.</summary>
   public bool IsStepCompleted(int stepIndex)
   {
       if (stepIndex < 0 || steps == null || stepIndex >= steps.Count) return false;
       if (WorldStateManager.Instance == null) return false;
       var step = steps[stepIndex];
       if (step.parts == null || step.parts.Count == 0) return false;
       foreach (var p in step.parts)
           if (p.fact == null || !WorldStateManager.Instance.GetFact(p.fact)) return false;
       return true;
   }
   ```

---

**Task 2 — Update `QuestFact.cs`: int-backed state field + new properties**

File: `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs`

1. Change `[SerializeField] private QuestState _questState;` → `[SerializeField] private int _questState;`
   - Field name stays the same → serialized asset data (0/1/2) remains valid.
   - Remove the public `QuestState QuestState => _questState;` property and replace with the three below.

2. Add properties:
   ```csharp
   /// <summary>The base quest state. Only meaningful when IsStepState is false.</summary>
   public QuestState QuestState => (QuestState)_questState;

   /// <summary>True when this fact references a QuestStep rather than a base quest state.</summary>
   public bool IsStepState => _questState >= 3;

   /// <summary>Zero-based index into QuestSO.steps. Only valid when IsStepState is true.</summary>
   public int QuestStepIndex => _questState - 3;
   ```

3. Update `Init` method signature unchanged (still accepts `QuestState`):
   ```csharp
   public QuestFact Init(QuestSO quest, QuestState state)
   {
       _quest = quest;
       _questState = (int)state;
       return this;
   }
   ```

4. Add step-state initialiser (used in tests):
   ```csharp
   /// <summary>Runtime/test initialiser for step-state facts.</summary>
   public QuestFact InitStep(QuestSO quest, int stepIndex)
   {
       _quest = quest;
       _questState = stepIndex + 3;
       return this;
   }
   ```

5. Update `ToString()`:
   ```csharp
   public override string ToString()
   {
       if (IsStepState)
           return $"Quest.{_quest?.questId ?? "null"}.Step{QuestStepIndex}";
       return $"Quest.{_quest?.questId ?? "null"}.{(QuestState)_questState}";
   }
   ```

6. Add `OnValidate()` to clamp stale `_questState` when steps are removed from the referenced quest (**F5/F13 fix**):
   ```csharp
   #if UNITY_EDITOR
   private void OnValidate()
   {
       if (_quest == null || _quest.steps == null) return;
       int maxIndex = 2 + _quest.steps.Count; // 2 = IsFailed (highest base-state index)
       if (_questState > maxIndex)
       {
           Debug.LogWarning($"[QuestFact] '{name}': _questState {_questState} out of range for assigned quest — clamped to 0 (IsStarted)");
           _questState = 0;
       }
   }
   #endif
   ```
   This fires automatically in the editor whenever `QuestFact` or its referenced `QuestSO` is modified, preventing silent stale indices.

---

**Task 3 — Create `GameEventSO_QuestStep.cs`**

File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_QuestStep.cs` *(new file)*

```csharp
using Game.Core;
using Game.Quest;
using UnityEngine;

namespace Game.Quest
{
    [System.Serializable]
    public struct QuestStepData
    {
        public QuestSO quest;
        public int stepIndex;
    }

    /// <summary>Typed event channel fired when a QuestStep transitions to completed.</summary>
    [CreateAssetMenu(menuName = "Game/Events/Quest Step Event", fileName = "OnQuestStep")]
    public class GameEventSO_QuestStep : GameEventSO<QuestStepData> { }
}
```

---

**Task 4 — Update `WorldStateManager.cs`: step-state branch in `IsQuestFactTrue`**

File: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`

Replace the body of `IsQuestFactTrue`:
```csharp
public bool IsQuestFactTrue(QuestFact fact)
{
    if (fact == null) { GameLog.Warn(TAG, "IsQuestFactTrue called with null fact"); return false; }
    if (fact.Quest == null) { GameLog.Warn(TAG, "QuestFact.Quest is null — assign a QuestSO in the Inspector"); return false; }

    if (fact.IsStepState)
        return fact.Quest.IsStepCompleted(fact.QuestStepIndex);

    return fact.QuestState switch
    {
        QuestState.IsStarted   => fact.Quest.IsStarted,
        QuestState.IsCompleted => fact.Quest.IsCompleted,
        QuestState.IsFailed    => fact.Quest.IsFailed,
        _                      => false
    };
}
```

No other changes to `WorldStateManager.cs`.

---

**Task 5 — Update `QuestEventsManager.cs`: step event channel + per-step tracking**

File: `Assets/_Game/Scripts/Quest/QuestEventsManager.cs`

1. Add serialized field under `_onQuestFailed`:
   ```csharp
   [SerializeField] private GameEventSO_QuestStep _onQuestStepCompleted;
   ```

2. Extend `QuestStateSnapshot`:
   ```csharp
   private struct QuestStateSnapshot
   {
       public bool started;
       public bool completed;
       public bool failed;
       public bool[] stepCompleted; // indexed by quest.steps index
   }
   ```

3. Update `Start()` to initialise step snapshot:
   ```csharp
   _lastState[quest] = new QuestStateSnapshot
   {
       started   = quest.IsStarted,
       completed = quest.IsCompleted,
       failed    = quest.IsFailed,
       stepCompleted = BuildStepSnapshot(quest)
   };
   ```

4. Add private helper `BuildStepSnapshot`:
   ```csharp
   private static bool[] BuildStepSnapshot(QuestSO quest)
   {
       if (quest.steps == null || quest.steps.Count == 0)
           return System.Array.Empty<bool>();
       var arr = new bool[quest.steps.Count];
       for (int i = 0; i < arr.Length; i++)
           arr[i] = quest.IsStepCompleted(i);
       return arr;
   }
   ```

5. Update `EvaluateQuest` to check steps after the existing started/completed/failed checks:
   > **Event ordering (F3):** Step events fire *after* started/completed/failed events within the same `EvaluateQuest` call. Listeners to `_onQuestCompleted` should not suppress or depend on step events in the same frame.
   ```csharp
   // Step completion — fires after base-state events in this call
   var prevSteps = prev.stepCompleted ?? System.Array.Empty<bool>();
   for (int i = 0; i < quest.steps.Count; i++)
   {
       bool nowDone = quest.IsStepCompleted(i);
       bool wasDone = i < prevSteps.Length && prevSteps[i];
       if (!wasDone && nowDone)
       {
           GameLog.Info(TAG, $"Quest step completed: '{quest.title}' step [{i}] '{quest.steps[i].title}'");
           _onQuestStepCompleted?.Raise(new QuestStepData { quest = quest, stepIndex = i });
       }
   }

   _lastState[quest] = new QuestStateSnapshot
   {
       started       = isStarted,
       completed     = isCompleted,
       failed        = isFailed,
       stepCompleted = BuildStepSnapshot(quest)
   };
   ```

---

**Task 6 — Create `QuestFactEditor.cs`**

File: `Assets/_Game/Scripts/Editor/QuestFactEditor.cs` *(new file)*

```csharp
#if UNITY_EDITOR
using Game.Core;
using Game.Quest;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomEditor(typeof(QuestFact))]
    public class QuestFactEditor : UnityEditor.Editor
    {
        private SerializedProperty _questProp;
        private SerializedProperty _questStateProp;

        private void OnEnable()
        {
            _questProp      = serializedObject.FindProperty("_quest");
            _questStateProp = serializedObject.FindProperty("_questState");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the _quest object field
            EditorGUILayout.PropertyField(_questProp, new GUIContent("Quest"));

            // Build the combined label list
            var questSO = _questProp.objectReferenceValue as QuestSO;
            bool hasQuest = questSO != null;

            string[] labels = BuildLabels(questSO);
            int current = _questStateProp.intValue;
            // Clamp to valid range and persist if steps were removed from the quest (**F5/F13 fix**)
            int max = labels.Length - 1;
            if (current > max)
            {
                Debug.LogWarning($"[QuestFactEditor] '{target.name}': state index {current} is out of range — clamped to 0 (IsStarted)");
                current = 0;
                _questStateProp.intValue = 0; // persist immediately so the asset is not left in an invalid state
            }

            using (new EditorGUI.DisabledScope(!hasQuest))
            {
                int selected = EditorGUILayout.Popup(
                    new GUIContent("Quest State", "IsStarted / IsCompleted / IsFailed or a step title"),
                    current,
                    labels
                );

                if (selected != current)
                    _questStateProp.intValue = selected;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static string[] BuildLabels(QuestSO quest)
        {
            int stepCount = quest != null && quest.steps != null ? quest.steps.Count : 0;
            var labels = new string[3 + stepCount];
            labels[0] = "IsStarted";
            labels[1] = "IsCompleted";
            labels[2] = "IsFailed";
            for (int i = 0; i < stepCount; i++)
            {
                string title = quest.steps[i].title;
                labels[3 + i] = string.IsNullOrEmpty(title)
                    ? $"Step {i} (no title)"
                    : $"Step: {title}";
            }
            return labels;
        }
    }
}
#endif
```

---

**Task 7 — Add EditMode tests for step evaluation**

File: `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs`

Add a new test region `// ── IsQuestFactTrue — Step states ───────────────────────────────────────` after the existing `IsQuestFactTrue` tests:

```csharp
[Test]
public void IsQuestFactTrue_StepState_AllPartsFulfilled_ReturnsTrue()
{
    var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("step0_done"));
    var questSO = ScriptableObject.CreateInstance<QuestSO>();
    questSO.steps.Add(new QuestStep
    {
        title = "Step A",
        parts = new List<QuestPart> { new QuestPart { fact = f1 } }
    });
    _cleanup.Add(questSO);
    _wsm.SetWorldEvent(f1, true);

    var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
    Assert.That(_wsm.IsQuestFactTrue(questFact), Is.True);
}

[Test]
public void IsQuestFactTrue_StepState_PartNotFulfilled_ReturnsFalse()
{
    var f1 = MakeFact(() => ScriptableObject.CreateInstance<WorldFact>().Init("step0_done"));
    var questSO = ScriptableObject.CreateInstance<QuestSO>();
    questSO.steps.Add(new QuestStep
    {
        title = "Step A",
        parts = new List<QuestPart> { new QuestPart { fact = f1 } }
    });
    _cleanup.Add(questSO);
    // f1 NOT set

    var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
    Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
}

[Test]
public void IsQuestFactTrue_StepState_EmptyParts_ReturnsFalse()
{
    var questSO = ScriptableObject.CreateInstance<QuestSO>();
    questSO.steps.Add(new QuestStep { title = "Empty Step", parts = new List<QuestPart>() });
    _cleanup.Add(questSO);

    var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 0));
    Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
}

[Test]
public void IsQuestFactTrue_StepState_OutOfRangeIndex_ReturnsFalse()
{
    var questSO = ScriptableObject.CreateInstance<QuestSO>();
    _cleanup.Add(questSO);
    // steps list is empty

    var questFact = MakeFact(() => ScriptableObject.CreateInstance<QuestFact>().InitStep(questSO, 99));
    Assert.That(_wsm.IsQuestFactTrue(questFact), Is.False);
}
```

These tests require `using System.Collections.Generic;` (already present) and `using Game.Quest;` (already present).

---

### Acceptance Criteria

**AC-1: QuestStep struct available on QuestSO**
- Given a `QuestSO` asset in the Inspector, When I expand the `Steps` list, Then I can add entries each with `title`, `description`, and `parts` sub-list.

**AC-2: IsStepCompleted returns correct result**
- Given a `QuestStep` with 2 parts, When both facts are true in WSM, Then `IsStepCompleted(0)` returns true.
- Given a `QuestStep` with 2 parts and one fact false, Then `IsStepCompleted(0)` returns false.
- Given `stepIndex` out of range, Then `IsStepCompleted` returns false.
- Given `parts` list is empty, Then `IsStepCompleted` returns false.

**AC-3: QuestFact inspector — conditional state dropdown**
- Given a `QuestFact` asset with `_quest` unassigned, When viewed in the Inspector, Then the `Quest State` dropdown is greyed out (disabled).
- Given `_quest` is assigned to a `QuestSO` with 2 steps titled "Find Herbalist" and "Deliver Herbs", Then the dropdown shows: `IsStarted | IsCompleted | IsFailed | Step: Find Herbalist | Step: Deliver Herbs`.
- Given a step option is selected and `_quest` is changed to a quest with fewer steps, Then the value clamps to 0 (IsStarted) without throwing.

**AC-4: WorldStateManager evaluates step facts**
- Given a `QuestFact` with `IsStepState == true` and step index 0, When `IsQuestFactTrue` is called, Then it delegates to `quest.IsStepCompleted(0)` and returns the correct result.
- Given a `QuestFact` with base state `IsStarted`, Then the existing switch-case path is unchanged.

**AC-5: QuestEventsManager fires step event**
- Given a quest with one step and `_onQuestStepCompleted` channel wired, When the step's fact becomes true (via a world-fact change), Then `_onQuestStepCompleted` is raised with `quest = <the quest>` and `stepIndex = 0`.
- Given the step was already completed before `Start()`, Then no spurious event is fired on scene load.

**AC-6: Tests pass**
- All 4 new EditMode tests in `WorldStateManagerFactsTests` pass.
- All existing tests continue to pass (no regressions).

---

## Additional Context

### Dependencies

- `QuestSO.IsStepCompleted` depends on `WorldStateManager.Instance` being non-null (same pattern as `IsStarted`/`IsCompleted`/`IsFailed`).
- `QuestFactEditor` depends on `QuestSO.steps` being a public `List<QuestStep>` field.
- `QuestEventsManager` step tracking depends on `QuestSO.steps.Count` being stable during `EvaluateQuest`. It is: steps are data-only, not runtime-mutated.

### Testing Strategy

- Unit: 4 new NUnit EditMode tests in `WorldStateManagerFactsTests` cover the IsStepCompleted evaluation path.
- Manual: Open any `QuestFact` asset in the Project window and verify the conditional dropdown in the Inspector.
- Manual: Add steps to a `QuestSO`, open a `QuestFact` referencing it, and confirm step titles appear in the dropdown.

### Notes

- The `QuestState` enum is **kept** in `QuestFact.cs` — it is used by the `Init(QuestSO, QuestState)` method and all existing tests. Do not remove it.
- Existing `QuestFact` assets serialized with `_questState = 0/1/2` are unaffected by the field type change from `QuestState` to `int`.
- The `QuestFactEditor` must call `serializedObject.Update()` at the top and `serializedObject.ApplyModifiedProperties()` at the bottom of `OnInspectorGUI` for Undo/Redo to work correctly.
- `GameEventSO_QuestStep.cs` defines both `QuestStepData` struct and `GameEventSO_QuestStep` in the same file — this is acceptable because `QuestStepData` is the direct payload type for `GameEventSO_QuestStep` and not reused elsewhere. The feedback rule "separate files for SO subclasses" applies to the SO class itself, which is in its own file ✓.
