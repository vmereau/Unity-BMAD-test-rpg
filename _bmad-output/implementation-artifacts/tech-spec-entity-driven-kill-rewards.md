---
title: 'Entity-Driven Kill Rewards (PersistentID as Source of Truth)'
slug: 'entity-driven-kill-rewards'
created: '2026-06-04'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1 (URP 17.x)', 'C# (Game asmdef)', 'ScriptableObjects', 'Typed GameEventSO<T> channels', 'Unity Test Framework (EditMode, NUnit)']
files_to_modify: ['Assets/_Game/ScriptableObjects/Facts/KilledFact.cs', 'Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs (NEW)', 'Assets/_Game/ScriptableObjects/Events/GameEventSO_EntityKilled.cs (NEW)', 'Assets/_Game/Scripts/World/PersistentID.cs', 'Assets/_Game/Scripts/Core/State/WorldStateManager.cs', 'Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs', 'Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs', 'Assets/_Game/Data/Events/OnEntityKilledFact.asset', 'Assets/_Game/Data/Enemies/StartingTown/Generic/KilledFact_*.asset', 'Assets/Tests/EditMode/WorldStateManagerFactsTests.cs', 'Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs (DELETE - optional)']
code_patterns: ['Payload struct = [System.Serializable] struct in own file (cf. FactData.cs), namespace Game.Core', 'Event channel = GameEventSO_X : GameEventSO<X> one-liner in Events/, namespace Game.Core', 'GameEventSO assets carry no serialized data (listeners runtime-only) - re-point m_Script GUID to migrate type without losing references', 'Subscribe OnEnable / unsubscribe OnDisable', 'GameLog + const TAG, never Debug.*', 'TryGetComponent / null-guard optional deps']
test_patterns: ['Tests/EditMode/*Tests.cs, NUnit [Test]', 'Pure-logic Edit Mode tests preferred; MonoBehaviour tests use AddComponent + reflection field injection', 'WorldStateManagerFactsTests injects event SOs via reflection and asserts Raise payloads']
---

# Tech-Spec: Entity-Driven Kill Rewards (PersistentID as Source of Truth)

**Created:** 2026-06-04

## Review Notes

- Adversarial review completed (inline). Project compiles clean; 18/18 `WorldStateManagerFactsTests` pass (272-test EditMode suite green except a pre-existing, unrelated `InventorySystemTests.ItemPickup_Configure_SetsInteractPrompt` failure).
- Findings: 4 total (all Low) — 2 fixed, 2 acknowledged.
  - **F3 (fixed):** Added a `<remarks>` note on `WorldStateManager.SetFact` warning that `KilledFact` writes must go through `RegisterKill` to raise the reward event.
  - **F4 (fixed):** Removed unnecessary `using _Game.ScriptableObjects.Entities;` from `PlayerRewards.cs` and the pre-existing unused `using Game.AI;` from `PersistentID.cs`.
  - **F1 (acknowledged):** Event raise order flipped (`_onFactChanged` now before `_onEntityKilled`) — listeners are independent; benign.
  - **F2 (acknowledged):** AC1 (NPC XP) requires each NPC's `PersistentID.Entity` to be authored with an `NPCEntity` (`XpOnKill > 0`) in the Inspector — verify during manual playtest.
- Resolution approach: auto-fix real findings.
- Not yet verified in Play mode: AC2 (monster-XP regression) and AC5 (persistence on reload) — code paths reviewed, live run pending.

## Overview

### Problem Statement

The kill→reward pipeline is built around `KilledFact.MonsterType` (a `MonsterEntity`), which is now outdated:

- **NPCs give 0 XP.** When an entity dies, `EntityHealth.Die()` → `PersistentID.RegisterDeath()` → `WorldStateManager.RegisterKill(KilledFact)` → raises `OnEntityKilled(KilledFact)` → `PlayerRewards.HandleEntityKilled` reads `fact?.MonsterType?.XpOnKill`. `MonsterType` is typed as `MonsterEntity`, so any NPC (whose `Entity` is an `NPCEntity`) resolves to `null` and grants no experience — even though `Entity.XpOnKill` already exists on the base `Entity` class for all subclasses.
- **Duplicated source of truth.** The entity reference lives in two places: `PersistentID.entityType` (an `Entity`, already used as the authority for `EntityHealth.MaxHealth`/`BaseHealth`) *and* `KilledFact.monsterType` (a `MonsterEntity`). They can drift.
- **Dead generator code.** `GenerateGenericKilledFacts` tries to copy the entity reference into the fact via `FindProperty("entityType")` on the `KilledFact`, but the field is named `monsterType` — so the copy silently fails. The fact's entity field is effectively never populated by the tool.

### Solution

Make `PersistentID.Entity` the single source of truth for the entity definition and reduce `KilledFact` to a pure persistence identity (GUID + prefix). On death, `PersistentID.RegisterDeath()` passes its `Entity` into `WorldStateManager.RegisterKill(fact, entity)`, which stores the fact for persistence and raises an enriched `EntityKilled` payload `{ Entity, KilledFact }`. `PlayerRewards` reads `payload.Entity.XpOnKill` for base XP (works for every `Entity` subclass) and keeps the `KilledFact` for per-asset bonus-reward matching.

### Scope

**In Scope:**
- Remove `monsterType` field, `MonsterType` property, and the `monsterType` parameter of `Init(...)` from `KilledFact.cs`.
- Add an `EntityKilled` payload type carrying `{ Entity entity, KilledFact fact }`, plus a matching `GameEventSO_EntityKilled` event-channel type (and its listener component, following the existing `GameEventSO_KilledFact` pattern; each concrete `GameEventSO` subclass in its own `.cs` file).
- `PersistentID.RegisterDeath()` passes `Entity` into `WorldStateManager.RegisterKill`.
- `WorldStateManager.RegisterKill(KilledFact, Entity)` stores the fact and raises the enriched event; move the `OnEntityKilled` raise out of the generic `RaiseFactEvent` fact switch.
- `PlayerRewards.HandleEntityKilled` consumes the new payload: base XP from `Entity.XpOnKill`, bonus rewards still matched against the `KilledFact`.
- Simplify `GenerateGenericKilledFacts` to identity-only (ensure each `PersistentID` has a `KilledFact`; drop the entity-copy block).
- Migrate the `OnEntityKilled` event SO asset to the new channel type and re-wire references on `WorldStateManager` (Core.unity) and `PlayerRewards` (Player prefab).
- Clean up the existing `KilledFact_*.asset` files (the now-removed `monsterType` serialized value).

**Out of Scope:**
- Save/Load / Steam Cloud (Epic 8) — `WorldStateSaveData` shape is untouched.
- Bonus rewards keyed by entity *type* (vs. the current per-`KilledFact`-asset matching) — `PlayerRewardSO` matching semantics are unchanged.
- Loot/corpse behaviour (`EntityPresence` looting) — unaffected.
- Any change to `Entity.XpOnKill` tuning values.

## Context for Development

### Codebase Patterns

- **GameEventSO concrete types must each live in their own `.cs` file** — Unity SO subclasses break `m_Script` on domain reload otherwise (see memory `feedback_gameeventso_single_file`). Follow the existing `GameEventSO_KilledFact` family layout when adding `GameEventSO_EntityKilled`.
- **World-fact reads/writes go only through `WorldStateManager` typed setters** (`RegisterKill`, etc.) + `WorldFactPrefix` — never build fact-key strings at call sites (`Scripts/Core/CLAUDE.md`).
- **Logging via `GameLog` with a `TAG`**, never `Debug.*` (`Scripts/Core/CLAUDE.md`).
- **`PersistentID`, `AIAnimationDriver`, `NavMeshAgent` are optional on an entity** — guard every access (`Scripts/AI/CLAUDE.md`).
- **`EntityHealth.MaxHealth` already derives from `PersistentID.Entity.BaseHealth`** — confirms `PersistentID.Entity` is the established authority for entity definition data.
- **Editor asset migration risk:** changing a `GameEventSO` asset's backing script type changes its `m_Script` GUID; the `.asset` and all scene/prefab references must be re-pointed. After raw YAML edits use `refresh_unity(mode="if_dirty")`, never `force` (root `CLAUDE.md`).

### Files to Reference

| File | Purpose / current state |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Facts/KilledFact.cs` | `: Fact`, namespace `Game.Core`. Has `_guid` + `MonsterEntity monsterType` + `Init(guid, monsterType=null)`. Remove `monsterType`/`MonsterType`/the `Init` param → pure identity. |
| `Assets/_Game/ScriptableObjects/Facts/FactData.cs` | **Pattern reference** for the new payload struct: `[System.Serializable] struct` in its own file, namespace `Game.Core`. |
| `Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs` | **NEW.** `[System.Serializable] struct EntityKilled { Entity entity; KilledFact fact; }` (+ ctor), namespace `Game.Core`. `using _Game.ScriptableObjects.Entities;` for `Entity`. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs` | **Pattern reference** (`GameEventSO_KilledFact : GameEventSO<KilledFact>`, `[CreateAssetMenu]`). After migration it has no refs/assets → optional DELETE. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_EntityKilled.cs` | **NEW.** `GameEventSO_EntityKilled : GameEventSO<EntityKilled>` one-liner + `[CreateAssetMenu(menuName="Game/Events/EntityKilled Event", ...)]`, namespace `Game.Core`. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Base. `_listeners` is a **non-serialized** runtime field → assets store no payload data; only `m_Script` GUID matters for migration. |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Has `_killedFact` (KilledFact) + `entityType` (Entity, exposed as `Entity`). `RegisterDeath()` → `WorldStateManager.Instance?.RegisterKill(_killedFact)`. Change call to pass `entityType`. |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | `_onEntityKilled` field is `GameEventSO_KilledFact` (line 23). `RegisterKill(KilledFact)` → `SetFact` → `RaiseFactEvent` switch raises `_onEntityKilled?.Raise(killedFact)` (lines 89-91). Change: field type → `GameEventSO_EntityKilled`; `RegisterKill(KilledFact, Entity entity = null)`; remove the `KilledFact` case from `RaiseFactEvent`; raise the `EntityKilled` payload from `RegisterKill` after `SetFact`. |
| `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs` | `_onEntityKilled` field is `GameEventSO_KilledFact` (line 15); `HandleEntityKilled(KilledFact fact)` reads `fact?.MonsterType?.XpOnKill` and matches bonus rewards via `reward.MatchesKilledFact(fact)`. Change: field type → `GameEventSO_EntityKilled`; `HandleEntityKilled(EntityKilled e)`; base XP from `e.entity?.XpOnKill ?? 0`; bonus match on `e.fact`. |
| `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs` | Lines 65-75 copy `entityType` into the fact via `FindProperty("entityType")` on the fact — **dead code** (fact field is `monsterType`). Remove that block; keep create/reuse/assign of `_killedFact`. |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Base `Entity` SO; `XpOnKill` (default 25) lives here → all subclasses (Monster, NPC) already carry it. |
| `Assets/_Game/ScriptableObjects/Rewards/PlayerRewardSO.cs` | `MatchesKilledFact(KilledFact)` keys on the specific `KilledFact` asset — **unchanged** (bonus rewards still per-fact). |
| `Assets/_Game/Data/Events/OnEntityKilledFact.asset` | The live channel (asset guid `297346452c9af3343b927f16cfa5fa1e`, currently `GameEventSO_KilledFact` guid `2227c4ef…`). Re-point `m_Script` GUID → new `GameEventSO_EntityKilled` GUID + update `m_EditorClassIdentifier`. Asset GUID unchanged → scene/prefab refs preserved. |
| `Assets/_Game/Scenes/Core.unity`, `Assets/_Game/Prefabs/Player/Player.prefab` | Reference `OnEntityKilledFact.asset` by guid `297346…` on WorldStateManager / PlayerRewards. No edit needed if field type + asset script are migrated together. (`Tome_PowerStrike.prefab` references a different guid — unrelated.) |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | `RegisterKill_AutoSetsKilledFact` calls `RegisterKill(fact)` single-arg → kept compiling by the optional `Entity` param. |

### Technical Decisions

1. **Payload shape:** new `EntityKilled` struct carrying `{ Entity entity, KilledFact fact }`. Both come from the single `PersistentID` component at raise time — no duplication. (Chosen: "Enrich WorldStateManager event".) The struct holds only SOs (no scene-object refs), satisfying the "no scene refs in SO-adjacent data" rule.
2. **`KilledFact` reduced to pure identity** — `monsterType` removed entirely (field, property, `Init` param). (Chosen: "Remove it fully".)
3. **Generator simplified to identity-only** — only guarantees a `KilledFact` per `PersistentID`; entity type stays solely on `PersistentID`. (Chosen: "Simplify to identity-only".)
4. **Reward event raised from `RegisterKill`, not the generic `RaiseFactEvent`** — the generic fact switch no longer special-cases `KilledFact`; persistence (`_onFactChanged`) still fires via `SetFact`. `RegisterKill` is the only producer of `KilledFact` writes, so this is safe and avoids threading `Entity` through the generic typed-write path.
5. **`Entity` param on `RegisterKill` is optional (`= null`)** — preserves the persistence-only `IsKilled`/`RegisterKill` contract and keeps the existing single-arg test compiling. A null entity yields 0 base XP (acceptable; live kills always pass `PersistentID.Entity`).
6. **Type migration is reference-safe** — because `GameEventSO` assets serialize no payload data, re-pointing `OnEntityKilledFact.asset`'s `m_Script` GUID and flipping the two field types in the same compile keeps the asset GUID (`297346…`) and all scene/prefab wiring intact. After raw `.asset` YAML edits, use `refresh_unity(mode="if_dirty")` — never `force`.

### Known Issues / Risks Surfaced

- **Pre-existing test bug (out of scope, flagged):** `WorldStateManagerFactsTests.SetWorldEvent_RaisesEvent_WithCorrectPayload` reflects a field named `_onWorldFactChanged`, but the actual field is `_onFactChanged` → `GetField` returns null and the test throws. Not introduced by this work; note for a future cleanup (or fix opportunistically if touching the test file).
- **Stale asset:** `OnEntityKilled.asset` (`GameEventSO_String`, guid `bab37ffd…`) is unreferenced anywhere — optional deletion, out of scope.
- **New `.cs` files need `.meta`/import** — after `Write`, run `refresh_unity` and check `read_console` for compile errors before the new types can be referenced by the asset migration.

## Implementation Plan

> **Compile-order note:** Tasks 1–2 are additive and compile on their own. Tasks 3–7 are
> **mutually dependent** (removing `KilledFact.MonsterType` breaks `PlayerRewards` until its
> handler is rewritten, and the field-type flips need `GameEventSO_EntityKilled` to exist) —
> apply tasks 3–7 as one batch, then compile once. Task 8 (asset re-point) needs the GUID
> produced after Task 2 compiles. See **Testing Strategy** for the exact apply/refresh sequence.

### Tasks

- [x] **Task 1: Add the `EntityKilled` payload struct**
  - File: `Assets/_Game/ScriptableObjects/Facts/EntityKilled.cs` (NEW)
  - Action: Create a `[System.Serializable] public struct EntityKilled` in namespace `Game.Core` with public fields `Entity entity` and `KilledFact fact`, plus a constructor `EntityKilled(Entity entity, KilledFact fact)`.
  - Notes: `using _Game.ScriptableObjects.Entities;` for `Entity`. Mirror `FactData.cs` style (simple serializable struct, one file). Carries only ScriptableObject refs — no scene-object refs (rule-compliant).

- [x] **Task 2: Add the `GameEventSO_EntityKilled` channel type**
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_EntityKilled.cs` (NEW)
  - Action: `public class GameEventSO_EntityKilled : GameEventSO<EntityKilled> { }` in namespace `Game.Core`, decorated with `[CreateAssetMenu(menuName = "Game/Events/EntityKilled Event", fileName = "NewEntityKilledEvent")]`.
  - Notes: Mirror `GameEventSO_KilledFact.cs` exactly (one type per file — Unity SO domain-reload rule). After creating, refresh Unity and record the generated `.meta` GUID — needed for Task 8.

- [x] **Task 3: Reduce `KilledFact` to pure identity**
  - File: `Assets/_Game/ScriptableObjects/Facts/KilledFact.cs`
  - Action: Remove the `[SerializeField] private MonsterEntity monsterType;` field, the `public MonsterEntity MonsterType => monsterType;` property, and the `monsterType` parameter from `Init` (becomes `Init(string guid)`; drop `this.monsterType = monsterType;`). Remove the now-unused `using Game.AI;` if nothing else needs it.
  - Notes: Keep `_guid`, `EntityGuid`, `Prefix`, `OnEnable`, `ToString`, and the `Generate GUID` context menu. No other behavioural change.

- [x] **Task 4: Update `WorldStateManager` to raise the enriched event**
  - File: `Assets/_Game/Scripts/Core/State/WorldStateManager.cs`
  - Action:
    1. Change field type: `[SerializeField] private GameEventSO_EntityKilled _onEntityKilled;` (was `GameEventSO_KilledFact`).
    2. Change signature: `public void RegisterKill(KilledFact fact, Entity entity = null)`. Keep the null-fact guard. After `SetFact(fact, true);`, add `_onEntityKilled?.Raise(new EntityKilled(entity, fact));`.
    3. In `RaiseFactEvent`, **remove** the `case KilledFact killedFact: _onEntityKilled?.Raise(killedFact); break;` arm (the `DialogueFact` arm and the trailing `_onFactChanged?.Raise(...)` stay).
  - Notes: `using _Game.ScriptableObjects.Entities;` for `Entity`. `RegisterKill` is the only `KilledFact` writer, so moving the raise out of the generic switch causes no missed events. Persistence still flows through `SetFact` → `_onFactChanged`.

- [x] **Task 5: Pass the entity from `PersistentID` on death**
  - File: `Assets/_Game/Scripts/World/PersistentID.cs`
  - Action: In `RegisterDeath()`, change `WorldStateManager.Instance?.RegisterKill(_killedFact);` to `WorldStateManager.Instance?.RegisterKill(_killedFact, entityType);`.
  - Notes: `entityType` is already the serialized `Entity` field. Existing null-`_killedFact` guard stays. No new field; this is the single source of truth being threaded through.

- [x] **Task 6: Rewrite `PlayerRewards` kill handler to use the entity**
  - File: `Assets/_Game/Scripts/Player/Progression/PlayerRewards.cs`
  - Action:
    1. Change field type: `[SerializeField] private GameEventSO_EntityKilled _onEntityKilled;` (was `GameEventSO_KilledFact`).
    2. Change handler signature to `private void HandleEntityKilled(EntityKilled e)`. `AddListener/RemoveListener(HandleEntityKilled)` calls are unchanged (method group still matches).
    3. Base XP: `int baseXp = e.entity != null ? e.entity.XpOnKill : 0;` then `if (baseXp > 0) _xpSystem.GiveExperience(baseXp);`.
    4. Bonus rewards: iterate `_rewards` and call `reward.MatchesKilledFact(e.fact)` (unchanged matching, now sourced from `e.fact`).
  - Notes: `using _Game.ScriptableObjects.Entities;` for `Entity` (XpOnKill access). Works for every `Entity` subclass (Monster + NPC). `PlayerRewardSO` is **not** modified.

- [x] **Task 7: Simplify `GenerateGenericKilledFacts` to identity-only**
  - File: `Assets/_Game/Scripts/Editor/GenerateGenericKilledFacts.cs`
  - Action: Delete the entity-copy block (the `var enemyTypeProp = so.FindProperty("entityType"); … factSO.ApplyModifiedProperties();` section, lines ~65–75). Keep the create/reuse-and-assign of `_killedFact` and the `fact.Init(System.Guid.NewGuid().ToString())` call (now single-arg per Task 3).
  - Notes: Tool now only guarantees each `PersistentID` has a `KilledFact`. Entity type stays solely on `PersistentID.entityType`, assigned manually in the Inspector.

- [x] **Task 8: Migrate the `OnEntityKilledFact.asset` channel type**
  - File: `Assets/_Game/Data/Events/OnEntityKilledFact.asset`
  - Action: Replace `m_Script: {fileID: 11500000, guid: 2227c4ef6e2ca504f81089d6909b3e1f, type: 3}` with the new `GameEventSO_EntityKilled` script GUID (from Task 2's `.meta`). Update `m_EditorClassIdentifier:` to `Game:Game.Core:GameEventSO_EntityKilled`.
  - Notes: Asset GUID (`297346452c9af3343b927f16cfa5fa1e`) is unchanged, so `Core.unity` / `Player.prefab` references survive. **After this raw YAML edit, run `refresh_unity(mode="if_dirty")` — never `force`** (force reimports from cache and discards the edit). Then re-open Core.unity / Player prefab and confirm the `_onEntityKilled` slots still show the asset (not "Missing").

- [x] **Task 9: Clean up existing `KilledFact_*.asset` files**
  - Files: `Assets/_Game/Data/Enemies/StartingTown/Generic/KilledFact_*.asset` (and any other `KilledFact_*.asset`)
  - Action: After Task 3 compiles, Unity will drop the orphaned `monsterType` serialized line on next save/reimport. Verify the `monsterType:` line is gone from each asset (re-save via the Editor or let reimport handle it); ensure `_guid` values are preserved.
  - Notes: 3 such assets already show as modified in git. No GUID changes; this is serialized-field cleanup only.

- [x] **Task 10: Keep tests compiling and add kill-routing coverage**
  - File: `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs`
  - Action: `RegisterKill_AutoSetsKilledFact` already passes a single arg — confirmed compiling via the optional `Entity` param. Add a test `RegisterKill_RaisesEntityKilled_WithEntityAndFact`: inject a `GameEventSO_EntityKilled` into `_onEntityKilled` via reflection, register a kill with a `KilledFact` + a `ScriptableObject.CreateInstance<MonsterEntity>()`, assert the received payload's `fact` and `entity` match.
  - Notes: Follow the existing reflection-injection pattern. (Optional, in-file) fix the pre-existing `_onWorldFactChanged` → `_onFactChanged` reflection typo in `SetWorldEvent_RaisesEvent_WithCorrectPayload` while here.

- [x] **Task 11 (optional): Delete the now-orphaned `GameEventSO_KilledFact`**
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_KilledFact.cs` (+ `.meta`)
  - Action: After Task 8, this type has no code references and no assets. Delete it (and its `.meta`) to remove dead code.
  - Notes: LOW priority. Skip if any out-of-scope asset still references guid `2227c4ef…` (grep first).

### Acceptance Criteria

- [ ] **AC1 (NPC XP — the core fix):** Given an NPC entity whose `PersistentID.Entity` is an `NPCEntity` with `XpOnKill = N (>0)`, when it dies and `RegisterDeath()` fires, then `PlayerRewards.HandleEntityKilled` calls `_xpSystem.GiveExperience(N)`.
- [ ] **AC2 (Monster XP unchanged):** Given a monster whose `PersistentID.Entity` is a `MonsterEntity` with `XpOnKill = N`, when it dies, then the player still receives exactly `N` base XP (no regression).
- [ ] **AC3 (Bonus rewards preserved):** Given a `PlayerRewardSO` of type `Killed` whose `KilledFact` matches the dying entity's fact, when the entity dies, then both the base XP (from `Entity.XpOnKill`) and the bonus rewards (LP/gold/stats from the matching SO) are applied.
- [ ] **AC4 (Single source of truth):** Given the refactor is complete, when inspecting `KilledFact`, then it exposes no entity/monster reference (`MonsterType` removed) and `PersistentID.Entity` is the only place the entity SO is authored.
- [ ] **AC5 (Persistence intact):** Given an entity with a `KilledFact`, when it is killed and the scene is reloaded, then `WorldStateManager.IsKilled(fact)` returns true and the entity deactivates on `Start` (unchanged behaviour).
- [ ] **AC6 (Reference migration):** Given `OnEntityKilledFact.asset` is re-pointed to `GameEventSO_EntityKilled`, when Core.unity and Player.prefab are opened, then the `_onEntityKilled` fields on `WorldStateManager` and `PlayerRewards` resolve to the asset (no "Missing (GameEventSO)" slots) and the project compiles with no console errors.
- [ ] **AC7 (Null-entity safety):** Given `RegisterKill(fact, null)` is called (e.g. the existing single-arg test path), when the event is raised, then no `NullReferenceException` occurs and 0 base XP is granted, while the fact is still recorded as killed.
- [ ] **AC8 (Generator scope):** Given a `PersistentID` with no `KilledFact`, when "Generate Missing KilledFacts" runs, then a `KilledFact` asset is created/reused and assigned, and **no** entity-type field is written to the fact.

## Additional Context

### Dependencies

- **No new packages.** Uses existing `Game.Core` (`GameEventSO<T>`, `WorldStateManager`, facts), `_Game.ScriptableObjects.Entities` (`Entity`/`XpOnKill`), and `Game.Progression` (`PlayerRewards`/`XPSystem`).
- **Internal dependency between tasks:** the new types (Tasks 1–2) must exist and compile before the asset re-point (Task 8) and before the field-type flips in Tasks 4 & 6 resolve.
- **Editor/MCP:** asset GUID lookup for the new script comes from its generated `.meta`; use `refresh_unity` + `read_console` to gate progress on a clean compile.

### Testing Strategy

**Apply/refresh sequence (avoids transient broken references):**
1. Create Task 1 + Task 2 scripts → `refresh_unity` (default/force-on-create is fine here, no raw YAML yet) → `read_console` until compile clean → read `GameEventSO_EntityKilled.cs.meta` for its GUID.
2. Apply Tasks 3–7 (all code edits) **and** Task 8 (raw `.asset` YAML re-point) together → `refresh_unity(mode="if_dirty")` → `read_console` until clean.
3. Verify Task 9 (`KilledFact_*.asset` lost their `monsterType` line) and re-open Core.unity / Player.prefab for AC6.

**Edit Mode (NUnit) tests:**
- Keep `RegisterKill_AutoSetsKilledFact` green (optional `Entity` param).
- Add `RegisterKill_RaisesEntityKilled_WithEntityAndFact` (reflection-injected `GameEventSO_EntityKilled`, assert payload `entity` + `fact`).
- Existing `XPSystemTests` (pure formula) remain valid — base XP is still `count * xpPerKill` semantics.

**Manual play test (in `StartingTown`):**
- Kill a Villager/NPC → confirm XP gained (watch `GameLog` / level/LP UI) → **previously granted 0**.
- Kill a Darkness Spider/monster → confirm same XP as before (no regression).
- Kill an entity tied to a bonus `PlayerRewardSO` → confirm base XP + bonus both apply.
- Kill a tracked entity, reload the scene → confirm it stays dead (persistence).

### Notes

- **Highest-risk step is Task 8 (asset re-point).** If the `_onEntityKilled` slots show "Missing" after refresh, the script GUID was wrong or `force` refresh discarded the edit — re-apply the YAML and use `if_dirty`. Because `GameEventSO` serializes no payload data, no runtime state is lost in migration.
- **Pre-existing bug (not introduced here):** `WorldStateManagerFactsTests` reflects `_onWorldFactChanged` while the field is `_onFactChanged`, so `SetWorldEvent_RaisesEvent_WithCorrectPayload` throws. Optionally fix in Task 10; otherwise leave for a dedicated cleanup.
- **Stale asset (out of scope):** `OnEntityKilled.asset` (`GameEventSO_String`, guid `bab37ffd…`) is unreferenced — safe to delete later.
- **Future consideration (out of scope):** if bonus rewards should ever key on entity *type* rather than a specific `KilledFact` asset, `PlayerRewardSO` could gain an `Entity`-based match — deliberately not done here to keep matching semantics stable.
- **Naming:** the channel asset keeps the name `OnEntityKilledFact.asset` to avoid re-wiring churn; the canonical `OnEntityKilled.asset` name is occupied by the stale String asset. Rename only if that stale asset is removed first.
