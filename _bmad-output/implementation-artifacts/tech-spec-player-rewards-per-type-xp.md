---
title: 'PlayerRewards — Per-Type XP via Kill Event Refactor'
slug: 'player-rewards-per-type-xp'
created: '2026-04-18'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
tech_stack: ['Unity 6.3 LTS', 'C#', 'ScriptableObject event channels', 'Unity Editor scripting']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs'
  - 'Assets/_Game/ScriptableObjects/Facts/KilledFact.cs'
  - 'Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs'
  - 'Assets/_Game/Scripts/World/PersistentID.cs'
  - 'Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs'
  - 'Assets/_Game/Scripts/Player/Progression/XPSystem.cs'
  - 'Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs'
  - 'Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
  - 'Assets/_Game/Prefabs/Enemies/Enemy_GiantViper.prefab'
  - 'Assets/_Game/Prefabs/Enemies/Enemy_GiantRat.prefab'
  - 'Assets/_Game/Prefabs/Enemies/Enemy_FantasyWolf.prefab'
  - 'Assets/_Game/Prefabs/Enemies/Enemy_DarknessSpider.prefab'
  - 'Assets/_Game/Prefabs/NPCs/NPC_Base.prefab'
  - 'Assets/_Game/Prefabs/Items/Tomes/Tome_PowerStrike.prefab'
  - 'Assets/Tests/EditMode/XPSystemTests.cs'
code_patterns:
  - 'GameEventSO<T> channel pattern — subscribe OnEnable, unsubscribe OnDisable'
  - 'OnDisable null guard when Awake disables before OnEnable runs'
  - 'GameEventSO concrete subclass must be in its own .cs file (memory rule)'
  - 'SerializedObject/FindProperty for editor scripting on ScriptableObjects'
test_patterns:
  - 'XPSystemTests uses pure formula helpers — no MonoBehaviour lifecycle needed'
---

# Tech-Spec: PlayerRewards — Per-Type XP via Kill Event Refactor

**Created:** 2026-04-18

---

## Overview

### Problem Statement

`XPSystem` directly subscribes to the `OnEntityKilled` `GameEventSO_String` event and awards a flat XP value from `ProgressionConfigSO.xpPerKill` regardless of which enemy was killed. There is no abstraction layer for player rewards, and XP cannot vary per enemy type.

### Solution

Introduce `PlayerRewards` as a new MonoBehaviour on the Player that acts as the single subscriber to kill events. Change the `_onEntityKilled` channel from `GameEventSO_String` to a new `GameEventSO_KilledFact` so the payload carries the full `KilledFact` object (including an optional `EnemyTypeSO` reference). `PlayerRewards` reads `fact.EnemyType?.XpOnKill` and delegates to `XPSystem.GiveExperience(int amount)`. `EnemyTypeSO` gains a new `XpOnKill` field, and `KilledFact` gains an optional `EnemyTypeSO` reference. The editor tool `GenerateGenericKilledFacts` is updated to auto-wire the `EnemyTypeSO` from `PersistentID` onto the generated `KilledFact`.

### Scope

**In Scope:**
- New `GameEventSO_KilledFact` event channel type (new file)
- New `OnEntityKilledFact.asset` event channel asset
- `EnemyTypeSO` — add `_xpOnKill: int` field + `XpOnKill` property
- `KilledFact` — add optional `_enemyType: EnemyTypeSO` field + `EnemyType` property; update `Init()` to accept optional EnemyTypeSO
- `PersistentID` — swap `_onEntityKilled` type from `GameEventSO_String` → `GameEventSO_KilledFact`; raise `_killedFact` directly instead of GUID string
- New `PlayerRewards.cs` — subscribes to `GameEventSO_KilledFact`, routes to `XPSystem.GiveExperience`
- `XPSystem` — remove event subscription; remove `_config` (only used for `xpPerKill`); add `public GiveExperience(int amount)`; TotalKills++ stays in `GiveExperience`
- `GenerateGenericKilledFacts` — after assigning KilledFact to PersistentID, also copy `_enemyType` from PersistentID's SerializedObject onto the KilledFact's SerializedObject
- All enemy/NPC/item prefabs with `PersistentID._onEntityKilled` — rewire to `OnEntityKilledFact.asset`
- Player prefab — remove `_onEntityKilled` from XPSystem; add `PlayerRewards` component wired to event + XPSystem
- `XPSystemTests` — update test comments; add test for `GiveExperience(0)` and per-type XP formula

**Out of Scope:**
- Kill-count-based rewards (only XP this sprint)
- Saving/loading XP mid-session
- Multiple reward systems beyond XPSystem in PlayerRewards
- Changing how `WorldStateManager.RegisterKill` works
- Any changes to `ProgressionConfigSO.xpPerLevel` or other config fields

---

## Context for Development

### Codebase Patterns

**GameEventSO channel pattern (rule from project-context):**
- Cross-system communication uses typed `GameEventSO<T>` ScriptableObject channels ONLY
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`
- Concrete subclass (e.g. `GameEventSO_KilledFact`) must live in its **own `.cs` file** — this is a hard memory rule enforced by Unity serialization (`m_Script` breaks on domain reload if the class is in the wrong file)

**OnDisable null guard (from CLAUDE.md):**
```csharp
private void OnDisable()
{
    if (_onEntityKilled == null) return; // Guard: Awake may disable before OnEnable runs
    _onEntityKilled.RemoveListener(HandleEntityKilled);
}
```

**SerializedObject pattern for Editor tools:**
`GenerateGenericKilledFacts` already uses `SerializedObject` + `FindProperty` to write to `PersistentID._killedFact`. The same pattern is used to write `_enemyType` to the generated `KilledFact` asset.

**Event asset naming convention:** `On + EventName` at `Assets/_Game/Data/Events/`.

**`ProgressionConfigSO.xpPerKill` becomes dead code after this change.** Do NOT remove the field from `ProgressionConfigSO` — it may still be referenced in Inspector assets. Just stop reading it in `XPSystem`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Base `GameEventSO<T>` class to extend for new type |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_String.cs` | Pattern to copy for new `GameEventSO_KilledFact` |
| `Assets/_Game/ScriptableObjects/Facts/KilledFact.cs` | Add `_enemyType` optional field |
| `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs` | Add `_xpOnKill` field under new `[Header("Rewards")]` |
| `Assets/_Game/Scripts/Player/Progression/XPSystem.cs` | Strip event subscription; add `GiveExperience` |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Swap event field type; raise KilledFact not GUID |
| `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs` | Wire `_enemyType` on generated KilledFact |
| `Assets/_Game/Data/Events/OnEntityKilled.asset` | Keep as-is (GameEventSO_String — legacy, unused after this) |
| `Assets/_Game/Data/Events/` | Create new `OnEntityKilledFact.asset` here |
| `Assets/Tests/EditMode/XPSystemTests.cs` | Update tests |

### Technical Decisions

**Why change the event channel type (GameEventSO_String → GameEventSO_KilledFact)?**
`PersistentID.RegisterDeath()` already holds a reference to `_killedFact` (a `KilledFact` SO). The GUID string was the original payload before KilledFact was introduced. Raising the KilledFact directly lets `PlayerRewards` access `fact.EnemyType?.XpOnKill` without any runtime lookup — no dictionary, no `FindObjectsOfType`. This is the clean path.

**Why KilledFact reference in KilledFact is optional (`[SerializeField]`)?**
Not all killed entities are enemies (NPCs, world items like tomes). An unassigned `_enemyType` means `PlayerRewards` will call `GiveExperience(0)` — no XP, no error. `GenerateGenericKilledFacts` wires the EnemyTypeSO only when `PersistentID._enemyType` is set.

**Why TotalKills stays in XPSystem.GiveExperience (not PlayerRewards)?**
XPSystem is the authoritative tracker for progression state (CurrentXP, TotalKills). PlayerRewards is a routing layer only — it should not own progression counters.

**`ProgressionConfigSO.xpPerKill` must be removed (Task 6b).**
Remove the field from `ProgressionConfigSO.cs`. Unity will log a harmless one-time "Unknown serialized property" warning on `ProgressionConfig.asset` at next load — this is expected and clears itself after reimport. The asset value (50) is discarded intentionally; per-type XP now lives on each `EnemyTypeSO`.

**Namespace for `GameEventSO_KilledFact`:** Use `Game.Core` (same as all other GameEventSO subtypes). `KilledFact` is already in `Game.Core` so no circular dependency.

**Namespace for `PlayerRewards`:** Use `Game.Progression` (same folder as `XPSystem`). Needs `using Game.Core` (for `GameEventSO_KilledFact`, `KilledFact`, `GameLog`).

---

## Implementation Plan

### Tasks

Tasks are ordered by dependency — lowest-level data/types first.

---

**Task 1 — Add `XpOnKill` to `EnemyTypeSO`**
File: `Assets/_Game/ScriptableObjects/AI/EnemyTypeSO.cs`

After the `[Header("Attack")]` block, add a new `[Header("Rewards")]` section:
```csharp
[Header("Rewards")]
[SerializeField] private int _xpOnKill = 25;

// ... (in the properties section at the bottom)
public int XpOnKill => _xpOnKill;
```

---

**Task 2 — Add optional `EnemyTypeSO` reference to `KilledFact`**
File: `Assets/_Game/ScriptableObjects/Facts/KilledFact.cs`

Add `using Game.AI;` at the top.

After `[SerializeField] private string _guid;`, add:
```csharp
[SerializeField] private EnemyTypeSO _enemyType;
public EnemyTypeSO EnemyType => _enemyType;
```

Update `Init(string guid)` signature to accept optional enemy type:
```csharp
public KilledFact Init(string guid, EnemyTypeSO enemyType = null)
{
    Prefix = WorldFactPrefix.Killed;
    _guid = guid;
    _enemyType = enemyType;
    return this;
}
```

---

**Task 3 — Create `GameEventSO_KilledFact` (new file)**
File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs` *(create new)*

```csharp
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Game/Events/KilledFact Event", fileName = "NewKilledFactEvent")]
    public class GameEventSO_KilledFact : GameEventSO<KilledFact> { }
}
```

This follows the exact pattern of `GameEventSO_String.cs`. Must be its own file (memory rule).

---

**Task 4 — Create `OnEntityKilledFact.asset`**
Location: `Assets/_Game/Data/Events/OnEntityKilledFact.asset`

Use the Unity Editor `Create → Game/Events/KilledFact Event` menu after compilation, or via MCP `manage_asset`. Name the asset `OnEntityKilledFact`.

> ⚠️ Do NOT delete or rename `OnEntityKilled.asset` (the legacy GameEventSO_String asset). It is harmless to leave it — no code subscribes to it after this refactor.

---

**Task 5 — Update `PersistentID` to raise `KilledFact` instead of GUID string**
File: `Assets/_Game/Scripts/World/PersistentID.cs`

Change field type and `RegisterDeath()`:

```csharp
// Before:
[SerializeField] private GameEventSO_String _onEntityKilled;
// ...
_onEntityKilled.Raise(_killedFact.EntityGuid);

// After:
[SerializeField] private GameEventSO_KilledFact _onEntityKilled;
// ...
_onEntityKilled.Raise(_killedFact);
```

No other changes to `PersistentID`. The null guard warning log stays identical.

> ⚠️ After changing the field type in C#, Unity will null out `_onEntityKilled` in all prefab instances because the serialized type no longer matches the old asset type. You must manually rewire all affected prefabs (Task 8).

---

**Task 6 — Refactor `XPSystem` + remove `xpPerKill` from `ProgressionConfigSO`**
Files: `Assets/_Game/Scripts/Player/Progression/XPSystem.cs`, `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs`

In `ProgressionConfigSO.cs`, remove the entire `[Header("XP — Story 3.1")]` block:
```csharp
// DELETE:
[Header("XP — Story 3.1")]
[Tooltip("Flat XP awarded per enemy kill.")]
public int xpPerKill = 50;
```

In `XPSystem.cs`, remove:
- `[SerializeField] private ProgressionConfigSO _config;`
- `[SerializeField] private GameEventSO_String _onEntityKilled;`
- All of `OnEnable()` and `OnDisable()` (no more event subscription)
- `private void HandleEntityKilled(string guid)` method
- The entire `Awake()` null checks for `_config` and `_onEntityKilled`
- `const string TAG = "[Progression]"` if no `GameLog` calls remain (check after removal)

Add:
```csharp
public void GiveExperience(int amount)
{
    if (amount <= 0) return;
    TotalKills++;
    CurrentXP += amount;
    GameLog.Info(TAG, $"XP gained: +{amount} (kill #{TotalKills}) — Total XP: {CurrentXP}");
    _onXPGained?.Raise(amount);
}
```

Keep:
- `[SerializeField] private GameEventSO_Int _onXPGained;`
- `public int CurrentXP { get; private set; }`
- `public int TotalKills { get; private set; }`
- The `Awake()` null check for `_onXPGained` (warn, not disable)
- `const string TAG = "[Progression]"` (still used in `GiveExperience`)

Resulting `Awake()`:
```csharp
private void Awake()
{
    if (_onXPGained == null)
        GameLog.Warn(TAG, "OnXPGained event is not assigned — XP signals will be silent.");
}
```

---

**Task 7 — Create `PlayerRewards.cs` (new file)**
File: `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` *(create new)*

```csharp
using Game.Core;
using UnityEngine;

namespace Game.Progression
{
    public class PlayerRewards : MonoBehaviour
    {
        private const string TAG = "[Progression]";

        [SerializeField] private GameEventSO_KilledFact _onEntityKilled;
        [SerializeField] private XPSystem _xpSystem;

        private void Awake()
        {
            if (_xpSystem == null)
            {
                GameLog.Error(TAG, "XPSystem not assigned — PlayerRewards disabled.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_onEntityKilled != null)
                _onEntityKilled.AddListener(HandleEntityKilled);
        }

        private void OnDisable()
        {
            if (_onEntityKilled == null) return; // Guard: Awake may disable before OnEnable runs
            _onEntityKilled.RemoveListener(HandleEntityKilled);
        }

        private void HandleEntityKilled(KilledFact fact)
        {
            int xp = fact?.EnemyType?.XpOnKill ?? 0;
            if (xp > 0)
                _xpSystem.GiveExperience(xp);
        }
    }
}
```

---

**Task 8 — Rewire prefabs: `_onEntityKilled` → `OnEntityKilledFact.asset`**

All `PersistentID` components that had `_onEntityKilled` wired to `OnEntityKilled.asset` must now point to `OnEntityKilledFact.asset` (the new `GameEventSO_KilledFact` asset from Task 4).

Affected prefabs — open each in the Unity Editor and rewire the `PersistentID._onEntityKilled` field:

| Prefab | Path |
|--------|------|
| `Enemy_GiantViper` | `Assets/_Game/Prefabs/Enemies/Enemy_GiantViper.prefab` |
| `Enemy_GiantRat` | `Assets/_Game/Prefabs/Enemies/Enemy_GiantRat.prefab` |
| `Enemy_FantasyWolf` | `Assets/_Game/Prefabs/Enemies/Enemy_FantasyWolf.prefab` |
| `Enemy_DarknessSpider` | `Assets/_Game/Prefabs/Enemies/Enemy_DarknessSpider.prefab` |
| `NPC_Base` | `Assets/_Game/Prefabs/NPCs/NPC_Base.prefab` |
| `Tome_PowerStrike` | `Assets/_Game/Prefabs/Items/Tomes/Tome_PowerStrike.prefab` |

> Note: `Player.prefab` line 406 referenced `_onEntityKilled` on the **XPSystem** component (subscribe side). After Task 6, `XPSystem` no longer has that field — Unity will auto-null it. No manual rewiring needed for the XPSystem slot. The `PlayerRewards` component (Task 9) brings the new subscription.

---

**Task 9 — Update Player prefab: add `PlayerRewards` component**
File: `Assets/_Game/Prefabs/Player/Player.prefab`

Add `PlayerRewards` MonoBehaviour to the Player root GameObject (alongside XPSystem):
- `_onEntityKilled` → `OnEntityKilledFact.asset`
- `_xpSystem` → `XPSystem` component on the same GameObject

Use MCP `manage_components` to add the component, then `manage_components set_property` to wire the references.

---

**Task 10 — Update `GenerateGenericKilledFacts` to wire `EnemyTypeSO` on `KilledFact`**
File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`

After the block that assigns `prop.objectReferenceValue = fact` (line ~62), add code to copy `_enemyType` from `PersistentID` to the `KilledFact`:

```csharp
// After: prop.objectReferenceValue = fact; so.ApplyModifiedProperties();
// Add:
var enemyTypeProp = so.FindProperty("_enemyType");
if (enemyTypeProp != null)
{
    var factSO = new SerializedObject(fact);
    var factEnemyProp = factSO.FindProperty("_enemyType");
    if (factEnemyProp != null)
    {
        factEnemyProp.objectReferenceValue = enemyTypeProp.objectReferenceValue;
        factSO.ApplyModifiedProperties();
    }
}
```

> Note: `PersistentID` already has `[SerializeField] private EnemyTypeSO _enemyType` (added in a prior story). `FindProperty("_enemyType")` on the PersistentID SerializedObject will find it. The `KilledFact._enemyType` field is new (Task 2), so this only works after Task 2 is compiled.

---

**Task 11 — Update `XPSystemTests`**
File: `Assets/Tests/EditMode/XPSystemTests.cs`

The existing tests use pure formula helpers (`CalculateXPForKills`, `AccumulateXP`) — they do not test `XPSystem` directly and will not break. Update the test class:

1. Update the summary comment — replace "flat XP per kill from ProgressionConfigSO" with "GiveExperience(amount) routes per-enemy XP"
2. Update `DifferentConfig_AwardsCorrectXP` test name → `DifferentAmount_AwardsCorrectXP`
3. Add two new tests:
   - `ZeroXpOnKill_DoesNotAccumulateXP` — `CalculateXPForKills(1, 0)` equals `0`
   - `PerEnemyXP_AccumulatesCorrectly` — simulate 2 kills at 30 XP + 1 kill at 50 XP = 110 total

---

### Acceptance Criteria

**AC-1: Per-type XP is awarded correctly**
- **Given** an enemy with `EnemyTypeSO.XpOnKill = 30` kills a player (i.e., `PersistentID.RegisterDeath()` is called)
- **When** the `OnEntityKilledFact` event fires with the `KilledFact` that references that `EnemyTypeSO`
- **Then** `XPSystem.CurrentXP` increases by exactly 30, `TotalKills` increases by 1, `OnXPGained` event fires with payload `30`

**AC-2: Kill with no EnemyType reference awards zero XP**
- **Given** a `KilledFact` with `EnemyType == null` (e.g., an NPC or tome pick-up)
- **When** `PlayerRewards.HandleEntityKilled(fact)` is called
- **Then** `XPSystem.GiveExperience` is NOT called (no XP, no log spam, no error)

**AC-3: PlayerRewards null-guards survive Awake→OnDisable edge case**
- **Given** `PlayerRewards` has `_onEntityKilled` unassigned in Inspector
- **When** `Awake` runs (XPSystem still assigned) and `OnDisable` runs before `OnEnable`
- **Then** no `NullReferenceException` is thrown

**AC-4: GenerateGenericKilledFacts wires EnemyTypeSO**
- **Given** scene has an enemy with `PersistentID._enemyType` assigned to an `EnemyTypeSO` asset
- **When** `Game/World/Generate Missing KilledFacts` menu item is run
- **Then** the generated (or reused) `KilledFact` asset has `EnemyType` matching the one set on `PersistentID`

**AC-5: No compile errors after full implementation**
- All 5 enemy/NPC/item prefabs compile without missing script references
- No `[SerializeField]` fields show "None (Missing)" in Inspector after rewiring

---

## Additional Context

### Dependencies

- Tasks 1–2 must complete before Task 3 (KilledFact needs EnemyTypeSO; GameEventSO_KilledFact uses KilledFact)
- Task 3 must compile before Task 4 (asset can only be created from a compiled concrete type)
- Tasks 3–4 must complete before Tasks 5–7 (PersistentID and PlayerRewards reference the new type)
- Task 6 (XPSystem refactor) must complete before Task 7 (PlayerRewards references XPSystem.GiveExperience)
- Task 5 must compile before Task 8 (prefab field type changed; Unity will null the old reference)
- Tasks 4 + 8 + 9 must all be done together in one prefab save pass (Editor state)
- Task 2 must compile before Task 10 (GenerateGenericKilledFacts writes to KilledFact._enemyType)

### Testing Strategy

The existing `XPSystemTests` uses pure formula helpers — update comments and add 2 formula tests for zero-XP and mixed-amount scenarios (Task 11). No new MonoBehaviour test harness needed.

Manual smoke test in PlayMode:
1. Enter scene → kill one enemy → confirm XP HUD updates by `EnemyTypeSO.XpOnKill` amount (not 50)
2. Kill an NPC → confirm no XP awarded
3. Open Console — confirm no `NullReferenceException` or missing-field warnings on `PlayerRewards`/`XPSystem`

### Notes

- `ProgressionConfigSO.xpPerKill` must be **removed** from `ProgressionConfigSO.cs` (Task 6b). Unity will emit a one-time "Unknown serialized property 'xpPerKill'" warning on `ProgressionConfig.asset` at next load — expected, clears on reimport, no data loss.
- `OnEntityKilled.asset` (the old GameEventSO_String) can be deleted in a future cleanup sprint once confirmed no scene references remain. Do NOT delete it now.
- All enemy type `EnemyTypeSO` assets will need `XpOnKill` tuned by hand in the Inspector after this implementation (default = 25).
- PersistentID already had `_enemyType: EnemyTypeSO` field from a prior story — the generator just wasn't reading it. No change needed to PersistentID's existing `_enemyType` field.
