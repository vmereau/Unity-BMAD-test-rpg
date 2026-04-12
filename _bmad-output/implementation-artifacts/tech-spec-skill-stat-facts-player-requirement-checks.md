---
title: 'Skill and Stat Facts for Player Requirement Checks'
slug: 'skill-stat-facts-player-requirement-checks'
created: '2026-04-11'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['C#', 'Unity ScriptableObject', 'Game.Core', 'Game.Progression', 'Game.Player']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Facts/SkillFact.cs'
  - 'Assets/_Game/ScriptableObjects/Facts/StatFact.cs'
  - 'Assets/_Game/Scripts/Core/State/WorldStateManager.cs'
  - 'Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs'
  - 'Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs'
code_patterns: ['Fact SO pattern', 'type-switch pattern match', 'WSM delegation', 'reflection injection in tests']
test_patterns: ['MakeFact<T> helper', 'reflection WSM field inject', 'reflection PlayerSkills learnedSkills inject', 'reflection PlayerStats baseStrength inject']
---

# Tech-Spec: Skill and Stat Facts for Player Requirement Checks

**Created:** 2026-04-11

## Overview

### Problem Statement

`TopicUnlockEvaluator` can only gate NPC dialogue topics on stored world facts (kills, dialogue played, quest steps). There is no mechanism to require that the player has learned a specific skill or meets a minimum stat threshold before an NPC topic unlocks or stays valid.

### Solution

Add two new `Fact` subclasses — `SkillFact` and `StatFact` — that encode player-state requirements as ScriptableObjects. Update `TopicUnlockEvaluator.AllTrue` and `AnyTrue` with a type-switch: skill and stat facts route through `WorldStateManager` (which holds serialized component references) to the authoritative check on `PlayerSkills.HasSkill` / `PlayerStats.GetStat`. The existing world-fact path is unchanged.

`WorldFactPrefix` is **not extended** — skill/stat facts are not stored in the world facts dictionary; they are evaluated at runtime against player components.

### Scope

**In Scope:**
- `SkillFact.cs` — new SO: `[SerializeField] SkillSO _skill`, `ToString()` for debugging, no `WorldFactPrefix`
- `StatFact.cs` — new SO: `[Serializable] StatRequirement` struct (StatType + int), list of requirements, `ToString()` for debugging, no `WorldFactPrefix`
- `WorldStateManager.cs` — add `[SerializeField] PlayerSkills _playerSkills` and `[SerializeField] PlayerStats _playerStats`; add `PlayerHasSkill(SkillFact)` and `PlayerStatCheck(StatFact)` methods that delegate to the components
- `TopicUnlockEvaluator.AllTrue` and `AnyTrue` — add type-switch before the default `GetFact` call
- `TopicUnlockEvaluatorTests.cs` — new tests for SkillFact and StatFact paths

**Out of Scope:**
- New `WorldFactPrefix` enum values
- Saving/loading skill or stat fact state
- Any UI changes

---

## Context for Development

### Codebase Patterns

- All `Fact` subclasses implement `OnEnable()` to set `Prefix` and `ToString()` to encode the dict key, and expose `Init()` for runtime/test setup. For `SkillFact` and `StatFact`, `Prefix` is **intentionally not set** (these are not dict-backed) — add a comment stating this.
- `PlayerSkills.HasSkill(string skillId)` — reuse directly; reads `_learnedSkills` HashSet, not gated by `enabled`.
- `PlayerStats.GetStat(StatType)` — reuse directly; returns effective value (base + equipment bonuses).
- `WorldStateManager` typed delegation pattern: null-guard the component ref, log a Warn and return false if missing.
- `TopicUnlockEvaluator` is a static class; always fetches `WorldStateManager.Instance` inline.
- All log calls use `GameLog.Warn(TAG, msg)` — never `Debug.Log`.
- `[CreateAssetMenu]` required on all new Fact subclasses.
- `StatType` is in `Game.Player` namespace — `StatFact.cs` needs `using Game.Player;`.
- `SkillSO` is in `Game.Progression` namespace — `SkillFact.cs` needs `using Game.Progression;`.
- Test injection: `PlayerSkills._learnedSkills` is a `private readonly HashSet<string>` — get the instance via reflection and `.Add()` to it. `PlayerStats._baseStrength` etc. are `private int` fields — set via reflection.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Facts/Fact.cs` | Abstract base — `Prefix`, `ToString()` contract |
| `Assets/_Game/ScriptableObjects/Facts/KilledFact.cs` | Pattern reference for new fact subclasses |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | Receives new serialized fields + delegation methods |
| `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs` | AllTrue/AnyTrue receive type-switch |
| `Assets/_Game/Scripts/Player/Progression/PlayerSkills.cs` | Source of truth for `HasSkill(string skillId)`; `_learnedSkills` field |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | Source of truth for `GetStat(StatType)`; `_baseStrength`, `_baseEndurance`, etc. |
| `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` | Referenced by `SkillFact`; `skillId` property backed by `_skillId` private field |
| `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs` | Test file to extend |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | Reflection pattern reference |
| `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs` | Consumer of AllTrue/AnyTrue — no changes needed |

### Technical Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| WorldFactPrefix for new types | Not extended | Skill/Stat facts are not dict-backed — adding a prefix would imply dict storage |
| Prefix field in new facts | Intentionally not set; add comment | No `OnEnable` prefix assignment needed |
| StatRequirement shape | `[Serializable] struct` with `public StatType statType; public int value;` | Unity serializes public fields in structs; clean Inspector UX |
| Stat threshold | `>=` (equal or greater) | Spec: "returns true if Equal/Greater" |
| WSM as intermediary | `[SerializeField] PlayerSkills` + `[SerializeField] PlayerStats` on WSM | Requested pattern; consistent with existing serialized event refs on WSM |
| AnyTrue update | Same switch logic as AllTrue | User confirmed same behavior |
| StatFact all-or-nothing | Return false on first failing requirement | Spec: "If any fail, return false" |
| `StatRequirement` scope | Top-level type in `StatFact.cs`, same namespace `Game.Core` | Cleaner than nested; accessible without qualifying the outer type |

---

## Implementation Plan

### Tasks

#### Task 1 — Create `SkillFact.cs`

File: `Assets/_Game/ScriptableObjects/Facts/SkillFact.cs`

Create new file. Full content:

```csharp
using Game.Progression;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Evaluates whether the player has learned a specific skill.
    /// NOT stored in WorldStateManager._worldFacts — evaluated at runtime via PlayerSkills.HasSkill().
    /// Prefix is intentionally not set (not a world-fact key).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Skill Fact", fileName = "SkillFact_")]
    public class SkillFact : Fact
    {
        [SerializeField] private SkillSO _skill;

        public SkillSO Skill => _skill;

        /// <summary>Runtime/test initialiser.</summary>
        public SkillFact Init(SkillSO skill)
        {
            _skill = skill;
            return this;
        }

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() => $"Skill.{_skill?.skillId ?? "null"}";
    }
}
```

---

#### Task 2 — Create `StatFact.cs`

File: `Assets/_Game/ScriptableObjects/Facts/StatFact.cs`

Create new file. Full content:

```csharp
using System.Collections.Generic;
using Game.Player;
using UnityEngine;

namespace Game.Core
{
    [System.Serializable]
    public struct StatRequirement
    {
        public StatType statType;
        public int value;
    }

    /// <summary>
    /// Evaluates whether the player meets all listed stat thresholds (>= check).
    /// NOT stored in WorldStateManager._worldFacts — evaluated at runtime via PlayerStats.GetStat().
    /// Prefix is intentionally not set (not a world-fact key).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Facts/Stat Fact", fileName = "StatFact_")]
    public class StatFact : Fact
    {
        [SerializeField] private List<StatRequirement> _requirements = new List<StatRequirement>();

        public IReadOnlyList<StatRequirement> Requirements => _requirements;

        /// <summary>Runtime/test initialiser.</summary>
        public StatFact Init(params StatRequirement[] requirements)
        {
            _requirements = new List<StatRequirement>(requirements);
            return this;
        }

        /// <summary>For debugging only — not used as a world-fact dictionary key.</summary>
        public override string ToString() => $"Stat.Requirements({_requirements?.Count ?? 0})";
    }
}
```

---

#### Task 3 — Update `WorldStateManager.cs`: add using directives and serialized fields

File: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`

**3a. Add using directives** at the top (after existing `using` lines):

```csharp
using Game.Player;
using Game.Progression;
```

**3b. Add serialized fields** directly after the existing `[SerializeField] private GameEventSO_WorldFact _onWorldFactChanged;` line:

```csharp
[SerializeField] private PlayerSkills _playerSkills;
[SerializeField] private PlayerStats _playerStats;
```

---

#### Task 4 — Update `WorldStateManager.cs`: add player-check methods

File: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`

Add a new section between the `// ── Typed convenience methods ──` block and the `// ── Save data (Epic 8) ──` block:

```csharp
// ── Player checks ─────────────────────────────────────────────────────

/// <summary>Returns true if the player has learned the skill referenced by this fact.
/// Delegates to PlayerSkills.HasSkill() — WorldStateManager is the intermediary only.</summary>
public bool PlayerHasSkill(SkillFact fact)
{
    if (fact == null) { GameLog.Warn(TAG, "PlayerHasSkill called with null fact"); return false; }
    if (_playerSkills == null) { GameLog.Warn(TAG, "PlayerSkills not assigned on WorldStateManager — skill check returns false"); return false; }
    return _playerSkills.HasSkill(fact.Skill.skillId);
}

/// <summary>Returns true if the player meets ALL stat thresholds in the fact (>= per requirement).
/// Delegates to PlayerStats.GetStat() — WorldStateManager is the intermediary only.</summary>
public bool PlayerStatCheck(StatFact fact)
{
    if (fact == null) { GameLog.Warn(TAG, "PlayerStatCheck called with null fact"); return false; }
    if (_playerStats == null) { GameLog.Warn(TAG, "PlayerStats not assigned on WorldStateManager — stat check returns false"); return false; }
    foreach (var req in fact.Requirements)
    {
        if (_playerStats.GetStat(req.statType) < req.value) return false;
    }
    return true;
}
```

---

#### Task 5 — Update `TopicUnlockEvaluator.cs`: add type-switch to `AllTrue`

File: `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs`

In `AllTrue()`, replace:

```csharp
if (!wsm.GetFact(fact)) return false;
```

With:

```csharp
bool result = fact switch
{
    SkillFact sf  => wsm.PlayerHasSkill(sf),
    StatFact  stf => wsm.PlayerStatCheck(stf),
    _             => wsm.GetFact(fact)
};
if (!result) return false;
```

---

#### Task 6 — Update `TopicUnlockEvaluator.cs`: add type-switch to `AnyTrue`

File: `Assets/_Game/Scripts/World/TopicUnlockEvaluator.cs`

In `AnyTrue()`, replace:

```csharp
if (wsm.GetFact(fact)) return true;
```

With:

```csharp
bool result = fact switch
{
    SkillFact sf  => wsm.PlayerHasSkill(sf),
    StatFact  stf => wsm.PlayerStatCheck(stf),
    _             => wsm.GetFact(fact)
};
if (result) return true;
```

---

#### Task 7 — Add tests to `TopicUnlockEvaluatorTests.cs`

File: `Assets/Tests/EditMode/TopicUnlockEvaluatorTests.cs`

**7a. Add using directives** at the top (alongside existing ones):

```csharp
using System.Reflection;
using Game.Progression;
using Game.Player;
```

(Note: `System.Reflection` is already imported — skip if present.)

**7b. Add two private helper methods** inside the test class (after the existing `CreateMemory` helper):

```csharp
private PlayerSkills CreatePlayerSkillsWithSkill(string skillId)
{
    var go = new GameObject("PlayerSkills_Test");
    _cleanup.Add(go);
    var ps = go.AddComponent<PlayerSkills>();
    var learnedSkills = (System.Collections.Generic.HashSet<string>)
        typeof(PlayerSkills)
            .GetField("_learnedSkills", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(ps);
    learnedSkills.Add(skillId);
    return ps;
}

private PlayerStats CreatePlayerStats(int strength = 0, int dexterity = 0,
                                      int endurance = 0, int intelligence = 0)
{
    var go = new GameObject("PlayerStats_Test");
    _cleanup.Add(go);
    var ps = go.AddComponent<PlayerStats>();
    var t = typeof(PlayerStats);
    t.GetField("_baseStrength",     BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, strength);
    t.GetField("_baseDexterity",    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, dexterity);
    t.GetField("_baseEndurance",    BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, endurance);
    t.GetField("_baseIntelligence", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ps, intelligence);
    return ps;
}
```

**7c. Add helper to inject WSM fields** (add alongside helpers):

```csharp
private void InjectWsmField(string fieldName, object value)
{
    typeof(WorldStateManager)
        .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
        .SetValue(_wsm, value);
}
```

**7d. Add helper for SkillSO creation** (needs reflection to set private `_skillId`):

```csharp
private SkillSO CreateSkillSO(string skillId)
{
    var so = ScriptableObject.CreateInstance<SkillSO>();
    typeof(SkillSO)
        .GetField("_skillId", BindingFlags.NonPublic | BindingFlags.Instance)
        .SetValue(so, skillId);
    _cleanup.Add(so);
    return so;
}
```

**7e. Add new test methods** in a `// ── SkillFact ──` and `// ── StatFact ──` region:

```csharp
// ── SkillFact ─────────────────────────────────────────────────────────

[Test]
public void AllTrue_SkillFact_PlayerHasSkill_ReturnsTrue()
{
    var skill = CreateSkillSO("power_strike");
    var playerSkills = CreatePlayerSkillsWithSkill("power_strike");
    InjectWsmField("_playerSkills", playerSkills);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
}

[Test]
public void AllTrue_SkillFact_PlayerMissingSkill_ReturnsFalse()
{
    var skill = CreateSkillSO("power_strike");
    var playerSkills = CreatePlayerSkillsWithSkill("other_skill");
    InjectWsmField("_playerSkills", playerSkills);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
}

[Test]
public void AllTrue_SkillFact_PlayerSkillsNotAssigned_ReturnsFalse()
{
    var skill = CreateSkillSO("power_strike");
    // _playerSkills intentionally not injected
    var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
}

[Test]
public void AnyTrue_SkillFact_PlayerHasSkill_ReturnsTrue()
{
    var skill = CreateSkillSO("power_strike");
    var playerSkills = CreatePlayerSkillsWithSkill("power_strike");
    InjectWsmField("_playerSkills", playerSkills);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<SkillFact>().Init(skill));
    Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[] { fact }), Is.True);
}

// ── StatFact ──────────────────────────────────────────────────────────

[Test]
public void AllTrue_StatFact_AllStatsMet_ReturnsTrue()
{
    var playerStats = CreatePlayerStats(strength: 10, endurance: 6);
    InjectWsmField("_playerStats", playerStats);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
        new StatRequirement { statType = StatType.Strength, value = 8 },
        new StatRequirement { statType = StatType.Endurance, value = 5 }
    ));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
}

[Test]
public void AllTrue_StatFact_ExactThreshold_ReturnsTrue()
{
    var playerStats = CreatePlayerStats(strength: 8, endurance: 5);
    InjectWsmField("_playerStats", playerStats);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
        new StatRequirement { statType = StatType.Strength, value = 8 },
        new StatRequirement { statType = StatType.Endurance, value = 5 }
    ));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.True);
}

[Test]
public void AllTrue_StatFact_OneStatFails_ReturnsFalse()
{
    var playerStats = CreatePlayerStats(strength: 10, endurance: 3);
    InjectWsmField("_playerStats", playerStats);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
        new StatRequirement { statType = StatType.Strength, value = 8 },
        new StatRequirement { statType = StatType.Endurance, value = 5 }
    ));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
}

[Test]
public void AllTrue_StatFact_PlayerStatsNotAssigned_ReturnsFalse()
{
    // _playerStats intentionally not injected
    var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
        new StatRequirement { statType = StatType.Strength, value = 5 }
    ));
    Assert.That(TopicUnlockEvaluator.AllTrue(new Fact[] { fact }), Is.False);
}

[Test]
public void AnyTrue_StatFact_AllStatsMet_ReturnsTrue()
{
    var playerStats = CreatePlayerStats(strength: 10);
    InjectWsmField("_playerStats", playerStats);

    var fact = MakeFact(() => ScriptableObject.CreateInstance<StatFact>().Init(
        new StatRequirement { statType = StatType.Strength, value = 8 }
    ));
    Assert.That(TopicUnlockEvaluator.AnyTrue(new Fact[] { fact }), Is.True);
}
```

---

### Acceptance Criteria

**AC1 — SkillFact unlocks topic when skill is learned**
- Given: `NPCMemoryEntrySO.unlockConditions` contains a `SkillFact` referencing `power_strike`; `PlayerSkills` has `power_strike` in its learned set; `PlayerSkills` is assigned on `WorldStateManager`
- When: `IsUnlocked()` is evaluated
- Then: Returns `true`

**AC2 — SkillFact blocks topic when skill is not learned**
- Given: Same setup as AC1, but `power_strike` is NOT in the learned set
- When: `IsUnlocked()` is evaluated
- Then: Returns `false`

**AC3 — StatFact unlocks topic when all stats meet thresholds**
- Given: `unlockConditions` contains a `StatFact` with Strength ≥ 8 and Endurance ≥ 5; player has Strength 10 and Endurance 6; `PlayerStats` is assigned on `WorldStateManager`
- When: `IsUnlocked()` is evaluated
- Then: Returns `true`

**AC4 — StatFact blocks topic when any stat is below threshold**
- Given: Same setup as AC3, but player Endurance is 3
- When: `IsUnlocked()` is evaluated
- Then: Returns `false`

**AC5 — StatFact passes on exact threshold**
- Given: Stat requirement is Strength ≥ 8; player has Strength = 8 exactly
- When: `PlayerStatCheck` is called
- Then: Returns `true` (equal counts)

**AC6 — SkillFact in AnyTrue invalidates topic when skill is learned**
- Given: `invalidationConditions` contains a `SkillFact`; player has that skill
- When: `IsInvalidated()` is evaluated
- Then: Returns `true` — topic is invalidated

**AC7 — Existing world-fact path is unchanged**
- Given: `AllTrue` / `AnyTrue` is called with `QuestFact`, `WorldFact`, `KilledFact`, or `DialogueFact`
- When: Any existing test runs
- Then: All existing `TopicUnlockEvaluatorTests` pass without modification

**AC8 — Missing `PlayerSkills` ref on WSM returns false gracefully**
- Given: `WorldStateManager._playerSkills` is null (not assigned in Inspector)
- When: `AllTrue` evaluates a `SkillFact`
- Then: Returns `false`; a `GameLog.Warn` is emitted; no exception thrown

**AC9 — Missing `PlayerStats` ref on WSM returns false gracefully**
- Given: `WorldStateManager._playerStats` is null
- When: `AllTrue` evaluates a `StatFact`
- Then: Returns `false`; a `GameLog.Warn` is emitted; no exception thrown

**AC10 — `SkillFact` and `StatFact` assets can be created via right-click Create menu**
- Given: Unity Editor is open
- When: Right-clicking in the Project window → `Game/Facts/`
- Then: "Skill Fact" and "Stat Fact" appear as asset creation options

---

## Additional Context

### Dependencies

- `PlayerSkills` and `PlayerStats` must be assigned on the `WorldStateManager` GameObject in the scene. Since WSM is `DontDestroyOnLoad`, the designer must wire these in the scene where WSM lives (typically the Core/Bootstrap scene). If these references are missing at runtime, the checks will return `false` with a Warn log — safe but silent.
- `SkillSO` assets must exist before `SkillFact` assets can be created (the Inspector slot will be empty without them).
- No new Unity packages or assembly references needed — all types are in the `Game` assembly.

### Testing Strategy

**EditMode (automated):**
- All 11 new tests in `TopicUnlockEvaluatorTests.cs` (Tasks 7d/7e above)
- Verify all existing tests in `TopicUnlockEvaluatorTests.cs` and `WorldStateManagerFactsTests.cs` still pass after the changes (no regressions)

**In-Editor playtest (manual):**
1. Create a `SkillFact` asset referencing an existing `SkillSO` (e.g. `Skill_PowerStrike`)
2. Assign it to an `NPCMemoryEntrySO.unlockConditions` for a test NPC topic
3. Start the game **without** learning the skill — confirm the topic is locked
4. Use the debug console or tome pickup to learn the skill — confirm topic unlocks
5. Repeat steps 3–4 for a `StatFact` — set a high threshold, verify it blocks; upgrade the stat via trainer, verify it passes

**Compilation check:**
- After creating both new `.cs` files, monitor Unity console for compile errors before testing

### Notes

- `SkillFact.Skill` can be `null` if the designer forgets to assign the SO in the Inspector — `PlayerHasSkill` will throw a `NullReferenceException` on `fact.Skill.skillId`. Consider adding a null guard in `PlayerHasSkill`: `if (fact.Skill == null) { GameLog.Warn(...); return false; }`.
- `StatFact` with an empty `_requirements` list will return `true` from `PlayerStatCheck` (the foreach does nothing) — this is a vacuously-true edge case. Add a designer note to the SO tooltip or leave as-is (matches the `AllTrue` empty-array contract).
- `GetStat(StatType.Defense)` returns equipment-only defense (no base). A `StatFact` requiring `Defense ≥ N` checks equipped defense. This is correct but worth noting for designers.
- Future: if save/load (Epic 8) is added, skill/stat facts do not need serialization since they evaluate live player state — no changes needed to the save system for this feature.
