---
title: 'Teach Choice Dialogue Node'
slug: 'teach-choice-dialogue-node'
created: '2026-04-10'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'C#', 'ScriptableObjects', 'Unity UI']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs'
  - 'Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs'
  - 'Assets/_Game/Scripts/World/DialogueSystem.cs'
  - 'Assets/_Game/Scripts/UI/DialogueUI.cs'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
code_patterns:
  - 'TeachChoiceOption : ChoiceOption — inherits text, requiredMemory, nextNode'
  - 'TeachChoiceDialogueNode : DialogueNode (not ChoiceDialogueNode, avoids unused choices[] in inspector)'
  - 'NPCDialogueGraphComponent uses generic FilterByMemory<T> helper to avoid duplication'
  - 'LearnSkill() handles LP deduction; DialogueSystem handles gold; CanAffordTeachChoice checks both'
  - 'For skills, effective LP cost = skill.lpCost; for stats, effective LP cost = choice.lpCost'
  - 'Unaffordable buttons disabled (not hidden); already-learned visibility via requiredMemory authoring'
  - 'DialogueSystem is on Player prefab; wire new refs there'
test_patterns:
  - 'Given/When/Then ACs'
---

# Tech-Spec: Teach Choice Dialogue Node

**Created:** 2026-04-10

## Overview

### Problem Statement

The dialogue system only supports plain choice nodes (`ChoiceDialogueNode`) with no knowledge of player economy or progression. There is no way to author NPC teaching interactions where selecting a dialogue option costs Gold and/or Learning Points and permanently upgrades a player stat or grants a skill.

### Solution

Introduce `TeachChoiceDialogueNode`, a new `DialogueNode` subclass whose choice type (`TeachChoiceOption`) extends `ChoiceOption` with Gold cost, LP cost, and a stat-upgrade or skill-learning effect. `DialogueSystem` gains serialized refs to the 4 economy/progression systems and exposes `CanAffordTeachChoice()` / `ApplyTeachChoice()` to the UI. `DialogueUI` renders cost labels and disables unaffordable buttons. Visibility of already-learned choices is delegated to `requiredMemory` authoring.

### Scope

**In Scope:**
- New `TeachChoiceOption : ChoiceOption` serializable class (adds `goldCost`, `lpCost`, `skill`, `statToUpgrade`, `statPoints`)
- New `TeachChoiceDialogueNode : DialogueNode` ScriptableObject (with `TeachChoiceOption[] choices`, `IsEndNode() => false`)
- Generic `FilterByMemory<T>()` helper in `NPCDialogueGraphComponent` — `GetAvailableTeachChoices()` + existing `GetAvailableChoices()` both use it
- `DialogueSystem`: 4 new `[SerializeField]` refs (`PlayerStats`, `GoldSystem`, `LearningPointSystem`, `PlayerSkills`) + `TeachChoiceDialogueNode` case in `AdvanceToNode()` + `CanAffordTeachChoice()` + `ApplyTeachChoice()`
- `DialogueUI.ShowTeachChoiceNode()` + `AddTeachChoiceButton()` — cost label, disabled when unaffordable
- Wire 4 new serialized fields on the **Player prefab** (not the scene)

**Out of Scope:**
- "Already learned" check in code — handled via `requiredMemory` authoring on the TeachChoiceOption
- Prerequisite skill gating
- Repeatable-purchase protection beyond what `isRepeatable` on `StartDialogueNode` provides
- UI feedback message when a disabled button is hovered
- Saving/loading progression state across sessions

---

## Context for Development

### Codebase Patterns

- **`DialogueSystem` is a component on the Player prefab** (`Assets/_Game/Prefabs/Player/Player.prefab`), not on a scene GameManager. `GoldSystem`, `LearningPointSystem`, `PlayerStats`, `PlayerSkills` are also on the Player prefab — wire the 4 new fields there.
- **`ChoiceOption`** (in `ChoiceDialogueNode.cs`) is `[System.Serializable]` with `text`, `requiredMemory`, `nextNode`. `TeachChoiceOption : ChoiceOption` gains these fields for free. Unity serializes all inherited fields of a `[System.Serializable]` class when the array is typed as the derived class — no polymorphism issues.
- **Why `TeachChoiceDialogueNode : DialogueNode` (not `: ChoiceDialogueNode`)**: `ChoiceDialogueNode` exposes `public ChoiceOption[] choices`. Shadowing it with `new public TeachChoiceOption[] choices` serializes both arrays, creating an empty unused `choices` slot in the Inspector. Direct inheritance from `DialogueNode` is cleaner.
- **LP cost resolution**: For skill choices, `LearnSkill()` internally calls `TrySpendLP(skill.lpCost)`. To avoid a sync requirement between `TeachChoiceOption.lpCost` and `skill.lpCost`, the effective LP cost is computed as `skill != null ? choice.skill.lpCost : choice.lpCost`. This single source of truth is used in both `CanAffordTeachChoice()` and `BuildCostLabel()`.
- **Apply flow for skills**: `TrySpend(goldCost)` → `LearnSkill(skill)`. `LearnSkill()` internally calls `TrySpendLP(skill.lpCost)` and returns false if insufficient LP or already learned. Log an error if it returns false (cannot happen if `CanAffordTeachChoice()` was checked first).
- **Apply flow for stat upgrades**: `TrySpend(goldCost)` → `TrySpendLP(choice.lpCost)` → `UpgradeStat(stat, points)`.
- **Button disable, not hide**: unaffordable choices render as non-interactable buttons. No slot callback is registered for disabled choices (pressing the number key does nothing).
- **`Defense` stat has no base field** in `PlayerStats` — authoring a stat upgrade to `Defense` logs a warning and is ignored. Document this for content authors.
- **`GameLog`** for all logging. `TAG = "[Dialogue]"` in `DialogueSystem`.
- **Null guards on new serialized fields**: missing refs log a `GameLog.Warn` and skip the corresponding operation — do NOT disable the whole `DialogueSystem`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` | `ChoiceOption` base class + node pattern to follow |
| `Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs` | Abstract base: `text`, `nextNode`, `IsEndNode()` |
| `Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs` | Add generic `FilterByMemory<T>()` + `GetAvailableTeachChoices()` |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Add refs, node case, `CanAffordTeachChoice()`, `ApplyTeachChoice()` |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | Add `ShowTeachChoiceNode()`, `AddTeachChoiceButton()`, `BuildCostLabel()` |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | `UpgradeStat(StatType, int)` — `Defense` has no base, logs warn |
| `Assets/_Game/Scripts/Economy/GoldSystem.cs` | `TrySpend(int)`, `Gold` property |
| `Assets/_Game/Scripts/Progression/LearningPointSystem.cs` | `TrySpendLP(int)`, `CurrentLP` |
| `Assets/_Game/Scripts/Progression/PlayerSkills.cs` | `LearnSkill(SkillSO)` — handles LP internally |
| `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` | `skillId`, `displayName`, `lpCost` |

### Technical Decisions

- **`TeachChoiceOption : ChoiceOption`** rather than a standalone class — reuses `text`, `requiredMemory`, `nextNode` with zero duplication. All serialization works because the array on `TeachChoiceDialogueNode` is typed as `TeachChoiceOption[]`.
- **`TeachChoiceDialogueNode : DialogueNode`** (not `: ChoiceDialogueNode`) — avoids the unused inherited `ChoiceOption[] choices` field polluting the Inspector.
- **No `GrantSkill()` addition** — use `LearnSkill()` which is already public, handles LP, and is the correct single-responsibility path for skill acquisition. Gold cost is handled by `DialogueSystem` before calling it.
- **No `HasSkill()` check in `CanAffordTeachChoice()`** — "already learned" visibility is an authoring concern, handled by setting a `requiredMemory` on the `TeachChoiceOption` that is removed after the skill is granted (or by setting `isRepeatable = false` on the parent `StartDialogueNode`).
- **`CanAffordTeachChoice()` checks**: (1) gold >= goldCost, (2) LP >= effectiveLpCost. Returns `true` if both pass.
- **Generic `FilterByMemory<T>()` helper** in `NPCDialogueGraphComponent` so `GetAvailableChoices()` and `GetAvailableTeachChoices()` share one implementation. No logic duplication.

---

## Implementation Plan

### Tasks

**Task 1 — Create `TeachChoiceDialogueNode.cs`**

File: `Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs` *(new file)*

```csharp
using Game.NPC;
using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.Dialogue
{
    [System.Serializable]
    public class TeachChoiceOption : ChoiceOption
    {
        [Header("Costs (0 = free)")]
        [Tooltip("Gold deducted on selection.")]
        public int goldCost;
        [Tooltip("LP cost for stat-upgrade choices. For skill choices this is ignored — LP cost is read from SkillSO.lpCost.")]
        public int lpCost;

        [Header("Effect — Skill OR Stat (mutually exclusive)")]
        [Tooltip("If set, this choice calls PlayerSkills.LearnSkill(). Stat fields below are ignored.")]
        public SkillSO skill;
        [Tooltip("Stat to upgrade. Used only when skill is null.")]
        public StatType statToUpgrade;
        [Tooltip("Points added to the stat. Used only when skill is null. Defense has no base value — authoring a Defense upgrade logs a warning and does nothing.")]
        [Min(1)] public int statPoints = 1;
    }

    [CreateAssetMenu(menuName = "Game/Dialogue/Teach Choice Node", fileName = "TeachChoice_")]
    public class TeachChoiceDialogueNode : DialogueNode
    {
        [Header("Teaching Choices")]
        [Tooltip("Options shown to the player. Each option teaches a stat or skill at a cost.")]
        public TeachChoiceOption[] choices;

        public override bool IsEndNode() => false;
    }
}
```

---

**Task 2 — Refactor `NPCDialogueGraphComponent.cs` with generic filter helper**

File: `Assets/_Game/Scripts/AI/NPCDialogueGraphComponent.cs`

Replace the existing `GetAvailableChoices()` body with a call to the new private helper, and add `GetAvailableTeachChoices()`:

```csharp
using System.Collections.Generic;
using Game.Core;
using Game.Dialogue;
using Game.NPC;
using UnityEngine;

namespace Game.AI
{
    public class NPCDialogueGraphComponent : MonoBehaviour
    {
        private const string TAG = "[DialogueGraph]";

        public StartDialogueNode[] GetAvailableStartNodes(NPCMemoryComponent memoryComponent)
        {
            var result = memoryComponent.GetActiveStartDialogNodes();
            return result.ToArray();
        }

        public ChoiceOption[] GetAvailableChoices(ChoiceDialogueNode choiceNode, NPCMemoryComponent memoryComponent)
        {
            if (choiceNode == null || choiceNode.choices == null || choiceNode.choices.Length == 0)
                return System.Array.Empty<ChoiceOption>();
            return FilterByMemory(choiceNode.choices, memoryComponent);
        }

        public TeachChoiceOption[] GetAvailableTeachChoices(TeachChoiceDialogueNode teachNode, NPCMemoryComponent memoryComponent)
        {
            if (teachNode == null || teachNode.choices == null || teachNode.choices.Length == 0)
                return System.Array.Empty<TeachChoiceOption>();
            return FilterByMemory(teachNode.choices, memoryComponent);
        }

        private T[] FilterByMemory<T>(T[] choices, NPCMemoryComponent memoryComponent)
            where T : ChoiceOption
        {
            NPCMemoryEntrySO[] activeMemories = memoryComponent != null
                ? memoryComponent.GetActiveMemories()
                : System.Array.Empty<NPCMemoryEntrySO>();

            var result = new List<T>(choices.Length);
            foreach (var choice in choices)
            {
                if (choice == null) continue;
                if (choice.requiredMemory == null || System.Array.IndexOf(activeMemories, choice.requiredMemory) >= 0)
                    result.Add(choice);
            }
            return result.ToArray();
        }
    }
}
```

---

**Task 3 — Update `DialogueSystem.cs`**

File: `Assets/_Game/Scripts/World/DialogueSystem.cs`

**3a. Add using statements** (after `using Game.Player;`):
```csharp
using Game.Economy;
using Game.Progression;
```

**3b. Add 4 serialized fields** after `_playerStateManager`:
```csharp
[SerializeField] private PlayerStats _playerStats;
[SerializeField] private GoldSystem _goldSystem;
[SerializeField] private LearningPointSystem _lpSystem;
[SerializeField] private PlayerSkills _playerSkills;
```

**3c. Add `TeachChoiceDialogueNode` case** in `AdvanceToNode()` switch, after the `ChoiceDialogueNode` case (around line 87):
```csharp
case TeachChoiceDialogueNode teachNode:
    TeachChoiceOption[] availableTeachChoices = _currentGraph != null
        ? _currentGraph.GetAvailableTeachChoices(teachNode, _currentNPCMemory)
        : teachNode.choices ?? System.Array.Empty<TeachChoiceOption>();
    _dialogueUI.ShowTeachChoiceNode(teachNode, availableTeachChoices);
    break;
```

**3d. Add `CanAffordTeachChoice()` public method** before `Close()`:
```csharp
/// <summary>
/// Returns true if the player currently meets the gold and LP requirements for the choice.
/// Called by DialogueUI to set button interactable state.
/// Does NOT check whether a skill is already learned — use requiredMemory authoring for that.
/// </summary>
public bool CanAffordTeachChoice(TeachChoiceOption choice)
{
    if (choice == null) return false;

    if (choice.goldCost > 0 && (_goldSystem == null || _goldSystem.Gold < choice.goldCost))
        return false;

    int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.lpCost;
    if (effectiveLpCost > 0 && (_lpSystem == null || _lpSystem.CurrentLP < effectiveLpCost))
        return false;

    return true;
}
```

**3e. Add `ApplyTeachChoice()` public method** after `CanAffordTeachChoice()`:
```csharp
/// <summary>
/// Deducts gold cost, then delegates to LearnSkill() (skill path) or TrySpendLP+UpgradeStat (stat path).
/// Must only be called after CanAffordTeachChoice() returned true.
/// Advances to choice.nextNode on success (null = close dialogue).
/// </summary>
public void ApplyTeachChoice(TeachChoiceOption choice)
{
    if (choice == null) return;

    // ── Skill path ───────────────────────────────────────────────────────────
    if (choice.skill != null)
    {
        if (_playerSkills == null)
        {
            GameLog.Warn(TAG, "ApplyTeachChoice: PlayerSkills not assigned — skill not granted");
            AdvanceToNode(choice.nextNode);
            return;
        }

        // Guard: skill already learned (author omitted requiredMemory gate).
        // Check BEFORE spending gold so no currency is lost.
        if (_playerSkills.HasSkill(choice.skill.skillId))
        {
            GameLog.Warn(TAG, $"ApplyTeachChoice: skill '{choice.skill.displayName}' already learned — requiredMemory gate missing on this TeachChoiceOption");
            AdvanceToNode(choice.nextNode);
            return;
        }

        // Spend gold first, then let LearnSkill() handle LP deduction.
        if (choice.goldCost > 0)
        {
            if (_goldSystem == null)
                GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
            else if (!_goldSystem.TrySpend(choice.goldCost))
            {
                GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                return;
            }
        }

        if (!_playerSkills.LearnSkill(choice.skill))
            GameLog.Error(TAG, $"ApplyTeachChoice: LearnSkill({choice.skill.displayName}) failed unexpectedly");
    }
    // ── Stat path ────────────────────────────────────────────────────────────
    else
    {
        if (choice.goldCost > 0)
        {
            if (_goldSystem == null)
                GameLog.Warn(TAG, "ApplyTeachChoice: GoldSystem not assigned — gold cost skipped");
            else if (!_goldSystem.TrySpend(choice.goldCost))
            {
                GameLog.Error(TAG, $"ApplyTeachChoice: TrySpend({choice.goldCost}) failed — insufficient funds at apply time");
                return;
            }
        }

        if (choice.lpCost > 0)
        {
            if (_lpSystem == null)
                GameLog.Warn(TAG, "ApplyTeachChoice: LearningPointSystem not assigned — LP cost skipped");
            else if (!_lpSystem.TrySpendLP(choice.lpCost))
            {
                GameLog.Error(TAG, $"ApplyTeachChoice: TrySpendLP({choice.lpCost}) failed — insufficient LP at apply time");
                return;
            }
        }

        if (_playerStats == null)
            GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — stat not upgraded");
        else
            _playerStats.UpgradeStat(choice.statToUpgrade, choice.statPoints);
    }

    AdvanceToNode(choice.nextNode);
}
```

---

**Task 4 — Update `DialogueUI.cs`**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

**4a. Add `ShowTeachChoiceNode()` public method** after `ShowChoiceNode()` (~line 162):
```csharp
/// <summary>Displays a TeachChoiceDialogueNode: NPC text above teaching choices with cost labels.</summary>
public void ShowTeachChoiceNode(TeachChoiceDialogueNode node, TeachChoiceOption[] availableChoices)
{
    if (_responseText != null)
        _responseText.text = node.text;

    ClearTopicButtons();

    int slot = 1;
    foreach (var choice in availableChoices)
    {
        if (choice == null) continue;
        AddTeachChoiceButton(choice, slot++);
    }

    SetState(DisplayState.Choices);
}
```

**4b. Add `AddTeachChoiceButton()` private method** after `AddChoiceButton()` (~line 303):
```csharp
private void AddTeachChoiceButton(TeachChoiceOption choice, int slot)
{
    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null)
        label.text = $"{SlotLabel(slot)}. {choice.text}{BuildCostLabel(choice)}";

    bool canAfford = _dialogueSystem != null && _dialogueSystem.CanAffordTeachChoice(choice);

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
    {
        btn.interactable = canAfford;
        if (canAfford)
        {
            var captured = choice;
            btn.onClick.AddListener(() =>
            {
                if (_dialogueSystem != null)
                    _dialogueSystem.ApplyTeachChoice(captured);
            });
        }
    }

    // Only register a slot callback for affordable choices (pressing key on disabled choice does nothing)
    if (slot <= 10 && canAfford)
    {
        var captured = choice;
        _slotCallbacks[slot] = () =>
        {
            if (_dialogueSystem != null)
                _dialogueSystem.ApplyTeachChoice(captured);
        };
    }
}

private static string BuildCostLabel(TeachChoiceOption choice)
{
    int effectiveLpCost = choice.skill != null ? choice.skill.lpCost : choice.lpCost;
    int gold = choice.goldCost;
    if (effectiveLpCost <= 0 && gold <= 0) return string.Empty;

    var sb = new System.Text.StringBuilder(" (");
    bool first = true;
    if (effectiveLpCost > 0)
    {
        sb.Append($"{effectiveLpCost} LP");
        first = false;
    }
    if (gold > 0)
    {
        if (!first) sb.Append(", ");
        sb.Append($"{gold}g");
    }
    sb.Append(')');
    return sb.ToString();
}
```

---

**Task 5 — Wire serialized fields on the Player prefab**

File: `Assets/_Game/Prefabs/Player/Player.prefab` *(open in Inspector — do not edit YAML directly)*

All 5 systems (`GoldSystem`, `LearningPointSystem`, `PlayerStats`, `PlayerSkills`, and `DialogueSystem`) are already confirmed as components on the Player prefab. Open the prefab in the Unity Editor, select the `DialogueSystem` component, and drag the following sibling components into the 4 new exposed slots:

| Field | Component to assign |
|-------|---------------------|
| `_playerStats` | `PlayerStats` component on the Player |
| `_goldSystem` | `GoldSystem` component on the Player |
| `_lpSystem` | `LearningPointSystem` component on the Player |
| `_playerSkills` | `PlayerSkills` component on the Player |

Save the prefab.

---

### Acceptance Criteria

**AC-1 — TeachChoiceDialogueNode renders correctly**
```
Given: An NPC dialogue chain leads to a TeachChoiceDialogueNode with 2 options
When: The player opens dialogue and navigates to that node
Then: NPC text displays above the choice list
And: Each button shows "{slot}. {text} ({effectiveLpCost} LP, {goldCost}g)"
And: If both costs are 0, no parenthetical is shown
And: For skill choices, the LP cost displayed matches skill.lpCost (not choice.lpCost)
```

**AC-2 — Unaffordable choice is disabled, not hidden**
```
Given: A choice costs 200g and the player has 100g
When: The TeachChoiceDialogueNode is displayed
Then: The button for that choice is rendered but non-interactable (visually greyed out)
And: Pressing the corresponding number key does nothing
```

**AC-3 — Stat upgrade path**
```
Given: A TeachChoiceOption with statToUpgrade=Strength, statPoints=1, goldCost=200, lpCost=1, skill=null
And: Player has >= 200g and >= 1 LP
When: Player selects that choice
Then: PlayerStats.Strength increases by 1
And: GoldSystem.Gold decreases by 200
And: LearningPointSystem.CurrentLP decreases by 1
And: Dialogue advances to choice.nextNode
```

**AC-4 — Skill learning path**
```
Given: A TeachChoiceOption with skill=NoviceBlacksmithingSO (lpCost=3), goldCost=500, skill!=null
And: Player has >= 500g and >= 3 LP and has NOT learned the skill
When: Player selects that choice
Then: PlayerSkills.HasSkill("novice_blacksmithing") returns true
And: GoldSystem.Gold decreases by 500
And: LearningPointSystem.CurrentLP decreases by 3
And: Dialogue advances to choice.nextNode
```

**AC-5 — Memory gate filters choices**
```
Given: A TeachChoiceOption with requiredMemory set to MemoryX
And: MemoryX is NOT active on the NPC
When: The TeachChoiceDialogueNode is displayed
Then: That choice does not appear in the UI at all
```

**AC-6 — Free choices show no cost label**
```
Given: A TeachChoiceOption with goldCost=0 and lpCost=0 and skill=null
When: The TeachChoiceDialogueNode is displayed
Then: The button text is just "{slot}. {choice.text}" with no parenthetical
```

**AC-7 — Null nextNode closes dialogue**
```
Given: A TeachChoiceOption with nextNode=null
When: Player selects that choice (affordable)
Then: Dialogue closes after the effect is applied
```

**AC-8 — Memory gate shared code path**
```
Given: A TeachChoiceDialogueNode with one memory-gated choice
When: GetAvailableTeachChoices() is called
Then: The same FilterByMemory<T> helper runs (verified by: removing memory gate → choice appears; adding memory → choice hidden)
```

---

## Additional Context

### Dependencies

- `GoldSystem.TrySpend()` / `.Gold` — no changes needed
- `LearningPointSystem.TrySpendLP()` / `.CurrentLP` — no changes needed
- `PlayerSkills.LearnSkill()` — no changes needed; handles LP internally
- `PlayerStats.UpgradeStat()` — no changes needed
- `ChoiceOption` — no changes needed; `TeachChoiceOption` inherits from it
- `NPCDialogueGraphComponent.GetAvailableChoices()` — refactored to use shared helper, behavior unchanged

### Testing Strategy

Manual playtesting steps:
1. Create a `TeachChoiceDialogueNode` asset: `Assets > Create > Game > Dialogue > Teach Choice Node`
2. Add two options: stat upgrade (STR+1, 1 LP, 200g) and skill (NoviceBlacksmithing, 500g; LP from SkillSO)
3. Wire into a Blacksmith NPC chain and open dialogue in Play Mode
4. **Gold/LP check**: set player gold to 0 → both buttons greyed out; set gold 200+, LP 1+ → STR button enables
5. **Stat apply**: select STR option → verify STR+1 in CharacterStats UI, Gold-200, LP-1
6. **Skill apply**: set LP to 3, gold 500+ → select skill option → verify skill in PlayerSkills
7. **Slot key**: verify pressing key `1` triggers the same action as clicking button 1
8. **Null nextNode**: wire nextNode=null → selecting the choice should close dialogue

### Notes

- `Defense` has no base value in `PlayerStats` — `UpgradeStat(Defense, n)` logs a warning and does nothing. Do not author `TeachChoiceOptions` with `statToUpgrade = Defense`.
- For "already learned" skill gating: set `requiredMemory` on the `TeachChoiceOption` to a memory that is deactivated after the skill is learned. Alternatively, set `isRepeatable = false` on the parent `StartDialogueNode` to hide the entire teaching topic after it completes.
- `TeachChoiceOption.lpCost` is only meaningful for stat-upgrade choices (when `skill == null`). For skill choices, the displayed and checked LP cost is always `skill.lpCost`.
