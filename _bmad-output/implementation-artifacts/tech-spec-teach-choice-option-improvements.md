---
title: 'TeachChoiceOption: Teaching Type, Confirm/Deny Routing, and Stat Cap'
slug: 'teach-choice-option-improvements'
created: '2026-04-12'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'C#', 'ScriptableObjects', 'Unity Editor PropertyDrawer']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs'
  - 'Assets/_Game/Scripts/Editor/TeachChoiceOptionDrawer.cs'
  - 'Assets/_Game/Scripts/World/DialogueSystem.cs'
code_patterns:
  - 'TeachingType enum on TeachChoiceOption controls Inspector field visibility via PropertyDrawer'
  - 'confirmNextNode replaces nextNode as the success-routing field for TeachChoiceOption'
  - 'limitStat > 0 triggers denyNextNode routing in ApplyTeachChoice with no resource consumption'
  - 'PropertyDrawer uses GetPropertyHeight with line-count arithmetic — no GUILayout.Space inside OnGUI'
test_patterns:
  - 'Given/When/Then ACs'
---

# Tech-Spec: TeachChoiceOption: Teaching Type, Confirm/Deny Routing, and Stat Cap

**Created:** 2026-04-12

## Overview

### Problem Statement

`TeachChoiceOption` currently shows all fields (skill + all stat fields) at once in the Inspector, regardless of whether a choice teaches a skill or upgrades a stat. Authors must remember which fields to fill and which to ignore. Additionally, `nextNode` (inherited from `ChoiceOption`) is a confusing label in the teaching context, stat-based trainers have no upper-bound cap, and there is no author-controlled "stat at cap" dialogue path.

### Solution

Add a `TeachingType` enum to `TeachChoiceOption` and a custom `PropertyDrawer` that only draws the relevant fields for the selected type. Add `confirmNextNode` as the explicit success-routing field, add `limitStat` and `denyNextNode` for stat-based choices, and update `DialogueSystem.ApplyTeachChoice()` to check the cap and route to `denyNextNode` (without consuming any resources) when the player's stat has reached the limit.

### Scope

**In Scope:**
- Add `TeachingType { SkillBased, StatBased }` enum and `teachingType` field to `TeachChoiceOption`
- Add `confirmNextNode : DialogueNode` (replaces the inherited `nextNode` as the teaching success routing field)
- Add `limitStat : int` (0 = no cap) and `denyNextNode : DialogueNode` to `TeachChoiceOption` (StatBased only)
- New `TeachChoiceOptionDrawer.cs` custom `PropertyDrawer` that conditionally shows/hides fields based on `teachingType`
- `DialogueSystem.ApplyTeachChoice()`: check stat cap on stat path → route to `denyNextNode` with zero resource consumption; use `confirmNextNode` for all success routing
- Migration note for the existing Blacksmith teaching asset

**Out of Scope:**
- Changing `CanAffordTeachChoice()` — the cap check is apply-time only (button stays enabled so the player can receive the deny message)
- Changing `lpCost` handling — `statPoints` continues to double as LP cost for stat-based choices (existing behavior)
- Any changes to `ChoiceDialogueNode` or `ChoiceOption` base class
- UI changes to `DialogueUI.cs` — no rendering differences from this spec

---

## Context for Development

### Codebase Patterns

- **`TeachChoiceOption : ChoiceOption`** is `[System.Serializable]`. `ChoiceOption` provides `text`, `requiredMemory`, and `nextNode`. In the teaching context `nextNode` is never set by authors — we replace it with `confirmNextNode` (new explicit field). The inherited `nextNode` is not drawn by the PropertyDrawer and stays null; no shadowing needed.
- **`DialogueSystem.ApplyTeachChoice()`** currently routes via `AdvanceToNode(choice.nextNode)`. This changes to `AdvanceToNode(choice.confirmNextNode)` everywhere in that method.
- **`statPoints` doubles as LP cost for stat-based choices** (current design, not changed). `CanAffordTeachChoice()` already uses `choice.statPoints` as the LP value for stat choices.
- **`PlayerStats.GetStat(StatType)`** is the correct way to read the current effective stat value for the cap check.
- **`GameLog`** for all logging; `TAG = "[Dialogue]"` is already defined in `DialogueSystem`.
- **Editor scripts** in `Assets/_Game/Scripts/Editor/` — existing examples: `WireEquipmentVisuals.cs`, `GenerateGenericKilledFacts.cs`. The new `TeachChoiceOptionDrawer.cs` lives here with `using UnityEditor;` and `#if UNITY_EDITOR` guard (or in `Editor/` asmdef scope — follow existing pattern).
- **PropertyDrawer for array elements**: Unity invokes the drawer once per array element. `GetPropertyHeight()` must return the exact pixel height for the expanded element, or array rows will overlap. Use `EditorGUIUtility.singleLineHeight` + `EditorGUIUtility.standardVerticalSpacing` arithmetic. Do NOT use `GUILayout` inside `OnGUI`.
- **`FindPropertyRelative("fieldName")`** traverses the serialized property tree. All fields added to `TeachChoiceOption` (and inherited from `ChoiceOption`) are accessible by name.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs` | File to modify — adds enum, new fields |
| `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` | `ChoiceOption` base — `text`, `requiredMemory`, `nextNode` |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | `ApplyTeachChoice()` — update stat path + routing field |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | `GetStat(StatType)` — used for limitStat check |
| `Assets/_Game/Scripts/Editor/WireEquipmentVisuals.cs` | Reference for Editor script conventions |
| `Assets/_Game/Data/NPCs/BlackSmith/Teachings/Training/TeachChoice_Blacksmith_Training.asset` | Needs manual re-authoring after migration (see Notes) |

### Technical Decisions

- **`confirmNextNode` as new field, `nextNode` stays null but inherited** — no C# shadowing needed. The `PropertyDrawer` simply does not draw `nextNode`. `DialogueSystem` uses `choice.confirmNextNode`. Migration: existing assets with `nextNode` set must have `confirmNextNode` re-assigned manually in the Editor.
- **Cap check is apply-time only** — `CanAffordTeachChoice()` does not check the cap. The button remains interactable when the stat is at the limit; the player clicks it and receives the `denyNextNode` text. This is intentional: the player "talks to" the trainer who tells them they've learned all they can.
- **`limitStat = 0` means no cap** — zero is the natural default for `int` and means "unlimited", consistent with how `goldCost = 0` means free.
- **`denyNextNode` only meaningful for StatBased** — for SkillBased choices, `limitStat` and `denyNextNode` are not drawn and should remain null/0.

---

## Implementation Plan

### Tasks

**Task 1 — Update `TeachChoiceDialogueNode.cs`: add enum and new fields**

File: `Assets/_Game/ScriptableObjects/Dialogue/TeachChoiceDialogueNode.cs`

Add `TeachingType` enum before the `TeachChoiceOption` class. Add three new fields to `TeachChoiceOption`: `teachingType`, `confirmNextNode`, `limitStat`, `denyNextNode`.

Replace the entire file with:

```csharp
using Game.Player;
using Game.Progression;
using UnityEngine;

namespace Game.Dialogue
{
    public enum TeachingType { SkillBased, StatBased }

    [System.Serializable]
    public class TeachChoiceOption : ChoiceOption
    {
        [Header("Teaching Type")]
        [Tooltip("Select SkillBased to teach a skill; StatBased to upgrade a stat.")]
        public TeachingType teachingType;

        [Header("Costs (0 = free)")]
        [Tooltip("Gold deducted on selection.")]
        public int goldCost;

        [Header("Effect — Skill OR Stat (mutually exclusive; set by teachingType)")]
        [Tooltip("If set, this choice calls PlayerSkills.LearnSkill(). Visible only when teachingType = SkillBased.")]
        public SkillSO skill;
        [Tooltip("Stat to upgrade. Visible only when teachingType = StatBased.")]
        public StatType statToUpgrade;
        [Tooltip("Points added to the stat AND LP cost for this training. Visible only when teachingType = StatBased.")]
        [Min(1)] public int statPoints = 1;
        [Tooltip("If player stat >= this value, deny training (no resources consumed). 0 = no cap. Visible only when teachingType = StatBased.")]
        public int limitStat;

        [Header("Navigation")]
        [Tooltip("Node to advance to when teaching executes successfully.")]
        public DialogueNode confirmNextNode;
        [Tooltip("Node to advance to when player stat is at the limit (no cost consumed). Visible only when teachingType = StatBased.")]
        public DialogueNode denyNextNode;
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

**Task 2 — Create `TeachChoiceOptionDrawer.cs`: conditional PropertyDrawer**

File: `Assets/_Game/Scripts/Editor/TeachChoiceOptionDrawer.cs` *(new file)*

This PropertyDrawer draws `TeachChoiceOption` array elements in the Inspector, showing only the fields relevant to the selected `teachingType`. Inherited fields (`text`, `requiredMemory`) are always drawn; the inherited `nextNode` field is never drawn (it stays null).

```csharp
#if UNITY_EDITOR
using Game.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    [CustomPropertyDrawer(typeof(TeachChoiceOption))]
    public class TeachChoiceOptionDrawer : PropertyDrawer
    {
        private const float LINE = 18f;   // EditorGUIUtility.singleLineHeight
        private const float SPACING = 2f; // EditorGUIUtility.standardVerticalSpacing
        private const float STEP = LINE + SPACING;

        // Fields always visible:
        // foldout, text, requiredMemory, teachingType, goldCost, confirmNextNode
        private const int ALWAYS_LINES = 6; // foldout header + 5 fields

        // StatBased extras: statToUpgrade, statPoints, limitStat, denyNextNode  →  4
        // SkillBased extras: skill  →  1

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return LINE;

            int extra = GetExtraLineCount(property);
            return LINE + (ALWAYS_LINES + extra) * STEP;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Foldout header
            var headerRect = new Rect(position.x, position.y, position.width, LINE);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + STEP;

                y = DrawField(position, y, property, "text");
                y = DrawField(position, y, property, "requiredMemory");
                y = DrawField(position, y, property, "teachingType");
                y = DrawField(position, y, property, "goldCost");
                y = DrawField(position, y, property, "confirmNextNode");

                var typeProp = property.FindPropertyRelative("teachingType");
                bool isSkillBased = typeProp.enumValueIndex == (int)TeachingType.SkillBased;

                if (isSkillBased)
                {
                    y = DrawField(position, y, property, "skill");
                }
                else // StatBased
                {
                    y = DrawField(position, y, property, "statToUpgrade");
                    y = DrawField(position, y, property, "statPoints");
                    y = DrawField(position, y, property, "limitStat");
                    y = DrawField(position, y, property, "denyNextNode");
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static float DrawField(Rect root, float y, SerializedProperty parent, string fieldName)
        {
            var prop = parent.FindPropertyRelative(fieldName);
            if (prop == null) return y; // field not found — skip silently
            var rect = new Rect(root.x, y, root.width, LINE);
            EditorGUI.PropertyField(rect, prop);
            return y + STEP;
        }

        private static int GetExtraLineCount(SerializedProperty property)
        {
            var typeProp = property.FindPropertyRelative("teachingType");
            if (typeProp == null) return 0;
            return typeProp.enumValueIndex == (int)TeachingType.SkillBased ? 1 : 4;
        }
    }
}
#endif
```

---

**Task 3 — Update `DialogueSystem.cs`: stat cap check and confirm/deny routing**

File: `Assets/_Game/Scripts/World/DialogueSystem.cs`

Two targeted edits:

**3a. Stat path in `ApplyTeachChoice()` — insert cap check before resource consumption**

Locate the `// ── Stat path ──` block (currently starting around line 237). Before the gold spend block, insert:

```csharp
// Stat cap check — no resources consumed if at or above limit
if (choice.limitStat > 0)
{
    if (_playerStats == null)
    {
        GameLog.Warn(TAG, "ApplyTeachChoice: PlayerStats not assigned — cannot evaluate limitStat; proceeding without cap check");
    }
    else if (_playerStats.GetStat(choice.statToUpgrade) >= choice.limitStat)
    {
        GameLog.Info(TAG, $"ApplyTeachChoice: {choice.statToUpgrade} at cap ({_playerStats.GetStat(choice.statToUpgrade)}/{choice.limitStat}) — routing to denyNextNode");
        AdvanceToNode(choice.denyNextNode);
        return;
    }
}
```

**3b. Replace all `AdvanceToNode(choice.nextNode)` with `AdvanceToNode(choice.confirmNextNode)` in `ApplyTeachChoice()`**

There are currently 3 call sites inside `ApplyTeachChoice()`:
- Line ~208: skill path — missing `PlayerSkills` ref early-out
- Line ~219: skill path — already-learned guard early-out  
- Line ~262: skill path and stat path — final success routing

Replace all three occurrences of `AdvanceToNode(choice.nextNode)` → `AdvanceToNode(choice.confirmNextNode)`.

---

**Task 4 — Manual asset migration: Blacksmith teaching asset**

File: `Assets/_Game/Data/NPCs/BlackSmith/Teachings/Training/TeachChoice_Blacksmith_Training.asset`

This is a manual Editor step, NOT a code change.

Open the asset in the Unity Editor. For each `TeachChoiceOption` in the `choices` array:
1. Set `teachingType` to the correct value (`SkillBased` for the Power Strike choice, `StatBased` for the +1 Strength choice)
2. Set `confirmNextNode` to the node that was previously in `nextNode` for that choice
3. For the `StatBased` (Strength) choice: optionally set `limitStat` and `denyNextNode` if a cap is desired
4. Save the asset

The old `nextNode` data on each choice element is now unreachable from `DialogueSystem` (it uses `confirmNextNode`) — it will remain null in the YAML but is harmless.

---

### Acceptance Criteria

**AC-1 — SkillBased choice shows only skill-relevant fields**
```
Given: A TeachChoiceOption with teachingType = SkillBased is expanded in the Inspector
When: The author inspects the array element
Then: Fields shown are: text, requiredMemory, teachingType, goldCost, confirmNextNode, skill
And:  Fields NOT shown: statToUpgrade, statPoints, limitStat, denyNextNode
```

**AC-2 — StatBased choice shows only stat-relevant fields**
```
Given: A TeachChoiceOption with teachingType = StatBased is expanded in the Inspector
When: The author inspects the array element
Then: Fields shown are: text, requiredMemory, teachingType, goldCost, confirmNextNode, statToUpgrade, statPoints, limitStat, denyNextNode
And:  Fields NOT shown: skill
```

**AC-3 — Stat cap: denyNextNode routed without consuming resources**
```
Given: A StatBased TeachChoiceOption with limitStat = 5 and denyNextNode = TextNode_AtLimit
And:  Player has Strength = 5 (at the cap), and >= goldCost gold and >= statPoints LP
When: Player selects the choice (button is interactable)
Then: No gold is deducted
And:  No LP is deducted
And:  Dialogue advances to TextNode_AtLimit
```

**AC-4 — Stat cap: training proceeds normally below the cap**
```
Given: A StatBased TeachChoiceOption with limitStat = 5
And:  Player has Strength = 4 (below cap), and >= goldCost gold and >= statPoints LP
When: Player selects the choice
Then: Strength increases by statPoints
And:  Gold and LP are deducted
And:  Dialogue advances to confirmNextNode
```

**AC-5 — limitStat = 0 means no cap**
```
Given: A StatBased TeachChoiceOption with limitStat = 0
And:  Player stat is any value (e.g. 100)
When: Player selects the choice (affordable)
Then: Stat upgrade is applied normally; cap branch is never entered
And:  Dialogue advances to confirmNextNode
```

**AC-6 — SkillBased success routing uses confirmNextNode**
```
Given: A SkillBased TeachChoiceOption with confirmNextNode = TextNode_SkillLearned
And:  Player can afford the choice and has not yet learned the skill
When: Player selects the choice
Then: Skill is granted
And:  Dialogue advances to TextNode_SkillLearned (the confirmNextNode)
```

**AC-7 — Cap check button stays enabled**
```
Given: A StatBased TeachChoiceOption where player stat >= limitStat
When: The TeachChoiceDialogueNode is displayed
Then: The button is rendered as interactable (CanAffordTeachChoice does not check the cap)
And:  Clicking it routes to denyNextNode per AC-3
```

---

## Additional Context

### Dependencies

- `PlayerStats.GetStat(StatType)` — already public, no changes needed
- `TeachChoiceOption.confirmNextNode` and `TeachChoiceOption.denyNextNode` — new fields; no other system reads them yet
- `TeachChoiceOption.limitStat` — new field; only read in `DialogueSystem.ApplyTeachChoice()`
- No changes to `DialogueUI`, `NPCDialogueGraphComponent`, `PlayerSkills`, `GoldSystem`, or `LearningPointSystem`

### Testing Strategy

Manual playtesting steps:
1. Open `TeachChoice_Blacksmith_Training` asset in Inspector — verify only the appropriate fields show per teachingType
2. Toggle `teachingType` between SkillBased and StatBased — verify the fields swap without Inspector errors
3. Set a Strength choice with `limitStat = 2`, wire `denyNextNode` to a text asset
4. In Play Mode: set player Strength to 1 → buy training → verify stat+1, gold/LP consumed, confirmNextNode reached
5. Set player Strength to 2+ → try training → verify NO gold/LP consumed, denyNextNode reached
6. Set `limitStat = 0` → verify training applies at any stat value (no cap)
7. Verify SkillBased choices are unaffected by limitStat/denyNextNode

### Notes

- **Data migration required**: The existing `TeachChoice_Blacksmith_Training.asset` has `nextNode` set on each choice element. After deploying this spec, that data is orphaned — `DialogueSystem` now reads `confirmNextNode`. The asset must be re-authored in the Editor (Task 4). Since this is a test/placeholder NPC the migration cost is acceptable.
- **`nextNode` (inherited) stays serialized but null**: `TeachChoiceOption` still inherits `nextNode` from `ChoiceOption`. It is not drawn by the PropertyDrawer and never set by authors. It remains null in all `TeachChoiceOption` instances and is harmless.
- **`statPoints` = LP cost (existing design)**: For stat-based choices, `statPoints` serves as both the stat increment and the LP cost. This is existing behavior and is not changed by this spec.
- **Limiting to `StatBased` only**: `limitStat` and `denyNextNode` have no meaning for `SkillBased` choices — the "already learned" guard in `ApplyTeachChoice` already handles that case. Do not author `limitStat > 0` on a SkillBased choice.
