---
title: 'Fact-Based Player Reward System'
slug: 'fact-based-player-rewards'
created: '2026-04-21'
status: 'in-progress'
stepsCompleted: [1, 2, 3]
tech_stack: ['Unity 6.3', 'C#', 'ScriptableObject Event Architecture', 'URP']
files_to_modify:
  - Assets/_Game/Scripts/Core/State/WorldStateManager.cs
  - Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs
files_to_create:
  - Assets/_Game/ScriptableObjects/Events/GameEventSO_DialogueFact.cs
  - Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs
  - Assets/_Game/Scripts/Editor/PlayerRewardSOEditor.cs
code_patterns:
  - 'GameEventSO<T> — typed SO event channel (AddListener/RemoveListener in OnEnable/OnDisable)'
  - 'OnDisable null guard for fields initialized in OnEnable'
  - 'Single file per SO subclass (memory feedback rule)'
test_patterns:
  - 'Inspector drag-and-drop wiring; no Play Mode test automation'
---

# Tech-Spec: Fact-Based Player Reward System

**Created:** 2026-04-21

## Overview

### Problem Statement

`PlayerRewards.cs` only responds to `KilledFact` events and only gives XP from the killed entity's `EnemyTypeSO`. There is no way to grant XP, LP, gold, or stat upgrades in response to dialogue or quest events, nor can designers configure bonus rewards for specific kills (e.g., a boss kill that also grants LP).

### Solution

Introduce a typed `GameEventSO_DialogueFact` event channel and wire it into `WorldStateManager.RaiseFactEvent`. For quest rewards, reuse the existing `QuestEventsManager` event channels (started/completed/failed/step). Extend `PlayerRewards` to listen to all relevant channels. A new `PlayerRewardSO` ScriptableObject lets designers bind any Fact (Killed/Quest/Dialogue) to a configurable reward bundle (XP + LP + Gold + stat upgrades). On a kill, base XP from `EnemyTypeSO.XpOnKill` fires first; any matching `PlayerRewardSO` is applied afterwards.

### Scope

**In Scope:**
- `GameEventSO_DialogueFact` typed event channel (new file)
- `WorldStateManager.RaiseFactEvent` updated with a `DialogueFact` case that raises `_onDialoguePlayed` AND `_onFactChanged`
- One new `[SerializeField]` field in `WorldStateManager`: `_onDialoguePlayed`
- `PlayerRewardSO` ScriptableObject: `FactType` enum (Killed/Quest/Dialogue), one fact ref per type, reward bundle (XP, LP, Gold, `List<StatReward>`)
- `PlayerRewardSOEditor` custom Editor: hides irrelevant fact fields based on `_factType`
- `PlayerRewards.cs` extended: new event subscriptions for `_onDialoguePlayed` + 4 quest event channels; handlers for all fact types; `List<PlayerRewardSO> _rewards`; `ApplyRewards()` helper

**Out of Scope:**
- `GameEventSO_QuestFact` (not needed — `QuestEventsManager` already provides typed quest events)
- Save/load of reward state
- Reward UI feedback (VFX, sound, floating numbers)
- Limiting rewards to fire-once per Fact (deduplication)

---

## Context for Development

### Codebase Patterns

- **GameEventSO<T> pattern** — concrete subclasses must be in their **own `.cs` files** (one class per file; Unity `m_Script` breaks on domain reload otherwise — memory feedback rule). `GameEventSO_KilledFact.cs` is the canonical template.
- **OnEnable / OnDisable subscription pattern** — always guard `OnDisable` when `Awake` may disable the component before `OnEnable` runs (CLAUDE.md gotcha). All new event fields must follow the same null-guard pattern as `_onEntityKilled`.
- **`RaiseFactEvent` already raises `_onFactChanged` for all non-Killed facts** — the `DialogueFact` case must also raise `_onFactChanged` so `QuestEventsManager.HandleWorldFactChanged` (which listens to it) continues to fire.
- **No `GameEventSO_QuestFact` needed** — `QuestEventsManager` already raises 4 typed quest event channels: `_onQuestStarted` / `_onQuestCompleted` / `_onQuestFailed` (`GameEventSO_Quest<QuestSO>`) and `_onQuestStepCompleted` (`GameEventSO_QuestStep<QuestStepData>`). `PlayerRewards` subscribes to the same SO assets and matches using the `QuestFact` reference on each `PlayerRewardSO`. `QuestFact` encodes all matching data: `Quest` (QuestSO ref), `IsStepState`, `QuestStepIndex`, and `QuestState`.
- **Awake disables component early** — `PlayerRewards.Awake` sets `enabled = false` if `_xpSystem` is null. New optional dependencies (`_lpSystem`, `_playerStats`, `_goldSystem`) should only `Warn`, not block.
- **StatType is in `Game.Player` namespace** — `PlayerRewardSO` (placed in `Game.Progression`) must `using Game.Player;`.
- **No `Debug.Log` directly** — use `GameLog.Info/Warn/Error`.

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs` | Template for new typed event SO |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Quest.cs` | `GameEventSO<QuestSO>` — used for started/completed/failed |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_QuestStep.cs` | `GameEventSO<QuestStepData>` — `QuestStepData { QuestSO quest; int stepIndex }` |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | `RaiseFactEvent` to update |
| `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` | Main file to extend |
| `Assets/_Game/Scripts/Player/Progression/LearningPointSystem.cs` | `GiveLp(int amount)` |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | `UpgradeStat(StatType, int)` |
| `Assets/_Game/Scripts/Economy/GoldSystem.cs` | `Add(int amount)` |
| `Assets/_Game/ScriptableObjects/Facts/QuestFact.cs` | `Quest`, `QuestState`, `IsStepState`, `QuestStepIndex` |
| `Assets/_Game/Scripts/Quest/QuestEventsManager.cs` | Raises the 4 quest event channels (read-only reference) |

### Technical Decisions

1. **Three separate Fact fields in `PlayerRewardSO`** (`_killedFact`, `_questFact`, `_dialogueFact`) rather than a polymorphic `Fact` base ref. Unity serializes concrete SO references cleanly; the custom Editor hides irrelevant fields.

2. **Quest matching via `QuestFact` properties** — for `_onQuestStarted(QuestSO q)`, match rewards where `questFact.Quest == q && !questFact.IsStepState && questFact.QuestState == QuestState.IsStarted`. Same pattern for completed/failed. For `_onQuestStepCompleted(QuestStepData d)`, match where `questFact.Quest == d.quest && questFact.IsStepState && questFact.QuestStepIndex == d.stepIndex`.

3. **`StatReward` struct** (`StatType statType`, `int points`) defined in `PlayerRewardSO.cs` as `[Serializable]` — reward-domain only, no other callers.

4. **Reward systems in `PlayerRewards` are optional** — `_lpSystem`, `_playerStats`, `_goldSystem` log a `Warn` if unassigned but do not disable the component. Only `_xpSystem` is required.

5. **`PlayerRewards` subscribes to the same quest SO event assets as `QuestEventsManager`** — designer assigns the same 4 SO assets in both Inspector slots. No new event assets need to be created.

---

## Implementation Plan

### Tasks

Ordered by dependency (lowest level first).

---

#### Task 1 — Create `GameEventSO_DialogueFact.cs`

**File:** `Assets/_Game/ScriptableObjects/Events/GameEventSO_DialogueFact.cs` *(new)*

```csharp
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Game/Events/DialogueFact Event", fileName = "NewDialogueFactEvent")]
    public class GameEventSO_DialogueFact : GameEventSO<DialogueFact> { }
}
```

---

#### Task 2 — Update `WorldStateManager.cs`

**File:** `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` *(modify)*

**2a. Add one new `[SerializeField]` field** after `_onEntityKilled`:

```csharp
[SerializeField] private GameEventSO_DialogueFact _onDialoguePlayed;
```

**2b. Replace `RaiseFactEvent`:**

```csharp
private void RaiseFactEvent(Fact fact, bool value)
{
    switch (fact)
    {
        case KilledFact killedFact:
            _onEntityKilled?.Raise(killedFact);
            break;
        case DialogueFact dialogueFact:
            _onDialoguePlayed?.Raise(dialogueFact);
            _onFactChanged?.Raise(new FactData(fact.ToString(), value));
            break;
        default:
            _onFactChanged?.Raise(new FactData(fact.ToString(), value));
            break;
    }
}
```

> `DialogueFact` raises both `_onDialoguePlayed` (new typed channel) and `_onFactChanged` (so `QuestEventsManager.HandleWorldFactChanged` still fires).

---

#### Task 3 — Create `PlayerRewardSO.cs`

**File:** `Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs` *(new — create `Rewards/` subfolder)*

```csharp
using System;
using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.Progression
{
    public enum RewardFactType { Killed, Quest, Dialogue }

    [Serializable]
    public struct StatReward
    {
        public StatType statType;
        public int points;
    }

    [CreateAssetMenu(menuName = "Game/Rewards/Player Reward", fileName = "PlayerReward_")]
    public class PlayerRewardSO : ScriptableObject
    {
        [SerializeField] private RewardFactType _factType;

        [SerializeField] private Game.Core.KilledFact   _killedFact;
        [SerializeField] private Game.Core.QuestFact    _questFact;
        [SerializeField] private Game.Core.DialogueFact _dialogueFact;

        [Header("Rewards")]
        [SerializeField] private int _xpReward;
        [SerializeField] private int _lpReward;
        [SerializeField] private int _goldReward;
        [SerializeField] private List<StatReward> _statRewards = new List<StatReward>();

        public RewardFactType           FactType     => _factType;
        public Game.Core.KilledFact   KilledFact   => _killedFact;
        public Game.Core.QuestFact    QuestFact    => _questFact;
        public Game.Core.DialogueFact DialogueFact => _dialogueFact;
        public int XpReward   => _xpReward;
        public int LpReward   => _lpReward;
        public int GoldReward => _goldReward;
        public IReadOnlyList<StatReward> StatRewards => _statRewards;

        public bool MatchesKilledFact(Game.Core.KilledFact fact)     => _factType == RewardFactType.Killed   && _killedFact   == fact;
        public bool MatchesDialogueFact(Game.Core.DialogueFact fact) => _factType == RewardFactType.Dialogue && _dialogueFact == fact;

        // Quest matching is split by QuestEventsManager channel — use these helpers per handler
        public bool MatchesQuestState(Game.Quest.QuestSO quest, Game.Core.QuestState state) =>
            _factType == RewardFactType.Quest &&
            _questFact != null &&
            !_questFact.IsStepState &&
            _questFact.Quest == quest &&
            _questFact.QuestState == state;

        public bool MatchesQuestStep(Game.Quest.QuestSO quest, int stepIndex) =>
            _factType == RewardFactType.Quest &&
            _questFact != null &&
            _questFact.IsStepState &&
            _questFact.Quest == quest &&
            _questFact.QuestStepIndex == stepIndex;
    }
}
```

---

#### Task 4 — Create `PlayerRewardSOEditor.cs`

**File:** `Assets/_Game/Scripts/Editor/PlayerRewardSOEditor.cs` *(new)*

```csharp
using UnityEditor;
using Game.Progression;

namespace Game.Editor
{
    [CustomEditor(typeof(PlayerRewardSO))]
    public class PlayerRewardSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var factTypeProp = serializedObject.FindProperty("_factType");
            EditorGUILayout.PropertyField(factTypeProp);

            switch ((RewardFactType)factTypeProp.intValue)
            {
                case RewardFactType.Killed:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_killedFact"));
                    break;
                case RewardFactType.Quest:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_questFact"));
                    break;
                case RewardFactType.Dialogue:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_dialogueFact"));
                    break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_xpReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_lpReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_goldReward"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_statRewards"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
```

---

#### Task 5 — Rewrite `PlayerRewards.cs`

**File:** `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` *(modify)*

```csharp
using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.Player;
using Game.Quest;
using UnityEngine;

namespace Game.Progression
{
    public class PlayerRewards : MonoBehaviour
    {
        private const string TAG = "[PlayerRewards]";

        // ── Required ──────────────────────────────────────────────────────────
        [SerializeField] private GameEventSO_KilledFact   _onEntityKilled;
        [SerializeField] private XPSystem                 _xpSystem;

        // ── Dialogue event channel ────────────────────────────────────────────
        [SerializeField] private GameEventSO_DialogueFact _onDialoguePlayed;

        // ── Quest event channels (same SO assets as QuestEventsManager) ───────
        [SerializeField] private GameEventSO_Quest     _onQuestStarted;
        [SerializeField] private GameEventSO_Quest     _onQuestCompleted;
        [SerializeField] private GameEventSO_Quest     _onQuestFailed;
        [SerializeField] private GameEventSO_QuestStep _onQuestStepCompleted;

        // ── Optional reward systems ───────────────────────────────────────────
        [SerializeField] private LearningPointSystem _lpSystem;
        [SerializeField] private PlayerStats         _playerStats;
        [SerializeField] private GoldSystem          _goldSystem;

        // ── Reward definitions ────────────────────────────────────────────────
        [SerializeField] private List<PlayerRewardSO> _rewards = new List<PlayerRewardSO>();

        private void Awake()
        {
            if (_xpSystem == null)
            {
                GameLog.Error(TAG, "XPSystem not assigned — PlayerRewards disabled.");
                enabled = false;
                return;
            }
            if (_onEntityKilled      == null) GameLog.Warn(TAG, "OnEntityKilled not assigned — no XP from kills.");
            if (_onDialoguePlayed    == null) GameLog.Warn(TAG, "OnDialoguePlayed not assigned — no dialogue rewards.");
            if (_onQuestStarted      == null) GameLog.Warn(TAG, "OnQuestStarted not assigned — no quest-started rewards.");
            if (_onQuestCompleted    == null) GameLog.Warn(TAG, "OnQuestCompleted not assigned — no quest-completed rewards.");
            if (_onQuestFailed       == null) GameLog.Warn(TAG, "OnQuestFailed not assigned — no quest-failed rewards.");
            if (_onQuestStepCompleted== null) GameLog.Warn(TAG, "OnQuestStepCompleted not assigned — no quest-step rewards.");
            if (_lpSystem            == null) GameLog.Warn(TAG, "LearningPointSystem not assigned — LP rewards skipped.");
            if (_playerStats         == null) GameLog.Warn(TAG, "PlayerStats not assigned — stat rewards skipped.");
            if (_goldSystem          == null) GameLog.Warn(TAG, "GoldSystem not assigned — gold rewards skipped.");
        }

        private void OnEnable()
        {
            if (_onEntityKilled      != null) _onEntityKilled.AddListener(HandleEntityKilled);
            if (_onDialoguePlayed    != null) _onDialoguePlayed.AddListener(HandleDialoguePlayed);
            if (_onQuestStarted      != null) _onQuestStarted.AddListener(HandleQuestStarted);
            if (_onQuestCompleted    != null) _onQuestCompleted.AddListener(HandleQuestCompleted);
            if (_onQuestFailed       != null) _onQuestFailed.AddListener(HandleQuestFailed);
            if (_onQuestStepCompleted!= null) _onQuestStepCompleted.AddListener(HandleQuestStepCompleted);
        }

        private void OnDisable()
        {
            // Guard: Awake may disable before OnEnable runs
            if (_onEntityKilled      != null) _onEntityKilled.RemoveListener(HandleEntityKilled);
            if (_onDialoguePlayed    != null) _onDialoguePlayed.RemoveListener(HandleDialoguePlayed);
            if (_onQuestStarted      != null) _onQuestStarted.RemoveListener(HandleQuestStarted);
            if (_onQuestCompleted    != null) _onQuestCompleted.RemoveListener(HandleQuestCompleted);
            if (_onQuestFailed       != null) _onQuestFailed.RemoveListener(HandleQuestFailed);
            if (_onQuestStepCompleted!= null) _onQuestStepCompleted.RemoveListener(HandleQuestStepCompleted);
        }

        // ── Kill handler ──────────────────────────────────────────────────────

        private void HandleEntityKilled(KilledFact fact)
        {
            // Base XP from the entity's EnemyTypeSO (always fires first)
            int baseXp = fact?.EnemyType?.XpOnKill ?? 0;
            if (baseXp > 0)
                _xpSystem.GiveExperience(baseXp);

            // Bonus rewards from matching PlayerRewardSO (e.g. special boss also gives LP)
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesKilledFact(fact))
                    ApplyRewards(reward);
            }
        }

        // ── Dialogue handler ──────────────────────────────────────────────────

        private void HandleDialoguePlayed(DialogueFact fact)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesDialogueFact(fact))
                    ApplyRewards(reward);
            }
        }

        // ── Quest handlers ────────────────────────────────────────────────────

        private void HandleQuestStarted(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsStarted))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestCompleted(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsCompleted))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestFailed(QuestSO quest)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestState(quest, QuestState.IsFailed))
                    ApplyRewards(reward);
            }
        }

        private void HandleQuestStepCompleted(QuestStepData data)
        {
            foreach (var reward in _rewards)
            {
                if (reward != null && reward.MatchesQuestStep(data.quest, data.stepIndex))
                    ApplyRewards(reward);
            }
        }

        // ── Reward applicator ─────────────────────────────────────────────────

        private void ApplyRewards(PlayerRewardSO reward)
        {
            if (reward.XpReward > 0)
                _xpSystem.GiveExperience(reward.XpReward);

            if (reward.LpReward > 0 && _lpSystem != null)
                _lpSystem.GiveLp(reward.LpReward);

            if (reward.GoldReward > 0 && _goldSystem != null)
                _goldSystem.Add(reward.GoldReward);

            if (_playerStats != null)
            {
                foreach (var statReward in reward.StatRewards)
                {
                    if (statReward.points > 0)
                        _playerStats.UpgradeStat(statReward.statType, statReward.points);
                }
            }
        }
    }
}
```

---

### Acceptance Criteria

#### AC1 — `GameEventSO_DialogueFact` compiles and appears in Create menu

**Given** the new file is created  
**When** Unity compiles  
**Then** `Game/Events/DialogueFact Event` appears in the asset Create menu

---

#### AC2 — `WorldStateManager` raises `_onDialoguePlayed` when dialogue is set

**Given** `_onDialoguePlayed` is assigned in the Inspector  
**When** `SetDialoguePlayed(dialogueFact)` is called  
**Then** `_onDialoguePlayed.Raise(dialogueFact)` fires AND `_onFactChanged` also fires

---

#### AC3 — Kill: base XP fires first, bonus reward applies after

**Given** `EnemyTypeSO.XpOnKill = 50` and a `PlayerRewardSO` matching that `KilledFact` with `LpReward = 10`  
**When** `HandleEntityKilled` fires  
**Then** `_xpSystem.GiveExperience(50)` is called first, then `_lpSystem.GiveLp(10)`

---

#### AC4 — Dialogue reward applies on matching fact

**Given** a `PlayerRewardSO` with `FactType = Dialogue`, `_dialogueFact = X`, `GoldReward = 100`  
**When** `_onDialoguePlayed` raises `X`  
**Then** `_goldSystem.Add(100)` is called

---

#### AC5 — Quest-completed reward applies on matching quest

**Given** a `PlayerRewardSO` with `FactType = Quest`, `_questFact.Quest = QuestA`, `_questFact.QuestState = IsCompleted`, `XpReward = 200`  
**When** `_onQuestCompleted` raises `QuestA`  
**Then** `_xpSystem.GiveExperience(200)` is called

---

#### AC6 — Quest step reward applies on matching step

**Given** a `PlayerRewardSO` with `FactType = Quest`, `_questFact.IsStepState = true`, `_questFact.QuestStepIndex = 1`, `LpReward = 5`  
**When** `_onQuestStepCompleted` raises `{ quest = QuestA, stepIndex = 1 }`  
**Then** `_lpSystem.GiveLp(5)` is called

---

#### AC7 — Unmatched facts trigger no rewards

**Given** a `PlayerRewardSO` bound to `DialogueFact A`  
**When** `_onDialoguePlayed` raises `DialogueFact B`  
**Then** no reward methods are called

---

#### AC8 — Missing optional systems skip gracefully

**Given** `_goldSystem` is null  
**When** a matching reward SO with `GoldReward = 50` fires  
**Then** no NullReferenceException; gold is silently skipped; XP/LP rewards still apply

---

#### AC9 — Inspector hides irrelevant fact fields

**Given** a `PlayerRewardSO` asset is open in the Inspector  
**When** `FactType = Quest` is selected  
**Then** only `_questFact` is shown; `_killedFact` and `_dialogueFact` are hidden

---

## Additional Context

### Dependencies

- **Compile-order**: Task 1 (`GameEventSO_DialogueFact`) must exist before Task 2 (`WorldStateManager`) references it.
- **Task 3 before Task 5**: `PlayerRewardSO` type and its matching helpers must exist before `PlayerRewards` references them.
- **Inspector wiring (post-compile)**:
  - `WorldStateManager` GameObject: create one `DialogueFact Event` SO asset (`Game/Events/DialogueFact Event`), assign to `_onDialoguePlayed`
  - `PlayerRewards` GameObject: assign `_onDialoguePlayed` (same asset), assign `_onQuestStarted` / `_onQuestCompleted` / `_onQuestFailed` / `_onQuestStepCompleted` using the **same SO assets already assigned in `QuestEventsManager`**
  - Assign `_lpSystem`, `_playerStats`, `_goldSystem` component references

### Testing Strategy

- Compile-check after each task group — zero errors expected
- Kill reward: trigger kill on Spider, verify base XP; add a `PlayerRewardSO` for that `KilledFact` with LP bonus, verify LP increments
- Dialogue reward: trigger `WorldStateManager.SetDialoguePlayed(fact)`, verify reward fires
- Quest reward: complete a quest in Play Mode, verify `_onQuestCompleted` fires and matching reward SO applies

### Notes

No trigger gap for quest rewards — `QuestEventsManager` already fires the correct typed events automatically when quest state transitions are detected from world fact changes. `PlayerRewards` simply subscribes to the same event SO assets.
