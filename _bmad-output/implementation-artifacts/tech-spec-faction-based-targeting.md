---
title: 'Faction-Based Targeting System'
slug: 'faction-based-targeting'
created: '2026-05-28'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack:
  - 'Unity 6000.3.10f1 (Unity 6.3 LTS)'
  - 'C# / .NET Standard 2.1'
  - 'Unity AI / NavMeshAgent'
  - 'ScriptableObject data architecture'
  - 'Unity Test Framework (NUnit) — EditMode tests'
files_to_modify:
  - 'Assets/_Game/Scripts/AI/EntityBrain.cs'
  - 'Assets/_Game/Scripts/AI/EntityHealth.cs'
  - 'Assets/_Game/Scripts/Player/PlayerHealth.cs'
  - 'Assets/_Game/ScriptableObjects/Entities/Entity.cs'
  - 'Assets/_Game/Prefabs/Entities/Entity_base.prefab'
  - 'Assets/_Game/Prefabs/Player/Player.prefab'
  - 'Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab'
  - 'Assets/_Game/Data/Enemies/EnemyType_*.asset (5 monster assets)'
  - 'Assets/_Game/Data/NPCs/*/NPC_*.asset (7 NPC assets)'
files_to_create:
  - 'Assets/_Game/ScriptableObjects/Factions/FactionSO.cs'
  - 'Assets/_Game/Scripts/Combat/IDamageable.cs'
  - 'Assets/_Game/Scripts/AI/FactionMember.cs'
  - 'Assets/_Game/Scripts/AI/TargetRegistry.cs'
  - 'Assets/_Game/Data/Factions/Faction_Player.asset'
  - 'Assets/_Game/Data/Factions/Faction_Monsters.asset'
  - 'Assets/_Game/Data/Factions/Faction_Bandits.asset'
  - 'Assets/_Game/Data/Factions/Faction_Neutral.asset'
  - 'Assets/Tests/EditMode/FactionTests.cs'
  - 'Assets/Tests/EditMode/TargetRegistryTests.cs'
code_patterns:
  - 'ScriptableObject data assets in Assets/_Game/Data/<Category>/; class definitions in Assets/_Game/ScriptableObjects/<Category>/'
  - 'Static registry pattern (TargetRegistry) with OnEnable/OnDisable register/unregister and [RuntimeInitializeOnLoadMethod] reset'
  - 'Interface-based damage contract (IDamageable) implemented by both PlayerHealth and EntityHealth'
  - 'GameLog with per-class TAG constant; never Debug.Log'
  - 'PersistentID.Entity accessor as the runtime path to Entity SO fields (e.g. _persistentID.Entity.Faction)'
  - 'Chainable Init(...) method on SOs for test setup (mirrors WorldFact.Init)'
  - 'OnDisable null-guard rule: any field/state initialized in OnEnable needs a guard since Awake may set enabled=false before OnEnable runs'
test_patterns:
  - 'EditMode pure-formula tests (mirrors EnemyBrainStateTests) for FactionSO.IsHostileTo'
  - 'EditMode AddComponent + reflection pattern (mirrors WorldStateManagerFactsTests) for TargetRegistry integration with FactionMember MonoBehaviour'
  - 'ScriptableObject.CreateInstance<T>() + chainable Init() for in-memory SO construction in tests'
  - 'SetUp/TearDown with _cleanup list of UnityEngine.Object for DestroyImmediate cleanup'
---

# Tech-Spec: Faction-Based Targeting System

**Created:** 2026-05-28

## Overview

### Problem Statement

`EntityBrain` (`Assets/_Game/Scripts/AI/EntityBrain.cs`) hardcodes a single targeting policy: "engage the player or stay neutral", controlled by the `_canEngagePlayer` bool. This is implemented through `FindGameObjectWithTag("Player")` + a cached `_player` Transform + direct calls to `PlayerCombat.TryReceiveHit()` and `PlayerHealth.TakeDamage()` in `ExecuteAttack()`.

This blocks every multi-party combat scenario the game needs:

- Friendly NPCs (town guards, escort companions) cannot assist the player by attacking hostile monsters/NPCs.
- Two enemy factions cannot fight each other (e.g. a bandit camp ambushed by wolves).
- Non-player targets are structurally impossible — the brain has no concept of "who am I and who is my enemy".

### Solution

Introduce a **faction-driven targeting foundation** that decouples "who attacks whom" from "what kind of entity am I":

1. **`FactionSO`** ScriptableObject — each faction asset carries a `hostileFactions` list and an `alliedFactions` list. Anything unlisted is neutral. Symmetric by convention: if `Faction_Bandits` lists `Faction_Player` as hostile and `Faction_Player` lists `Faction_Bandits` as hostile, both attack on detection.
2. **`IDamageable`** interface — single contract for receiving damage and (optionally) defended hits. `PlayerHealth` and `EntityHealth` both implement it. `EntityBrain.ExecuteAttack()` calls through the interface; the Player keeps its Defense/block/dodge logic in `PlayerHealth.TryReceiveHit` (which delegates to `PlayerCombat.TryReceiveHit`).
3. **`FactionMember`** MonoBehaviour — placed on every targetable root GO (Player, Enemy_*, NPC_*). Exposes `FactionSO Faction` and a cached `IDamageable Damageable`. Self-registers in a static `TargetRegistry` in `OnEnable` and unregisters in `OnDisable`. Faction resolves from `PersistentID.Entity.Faction` by default, with `_factionOverride` for entities that lack a `PersistentID` (the Player).
4. **`TargetRegistry`** static class — holds the live set of all `FactionMember`s. Exposes `FindClosestHostile(FactionSO myFaction, Vector3 position, float maxRange)` which iterates members, filters by `IsHostileTo`, skips dead members, and returns the closest one within range.
5. **`EntityBrain` refactor** — `_canEngagePlayer`, `_player`, `_playerCombat`, `_playerHealth`, `FindGameObjectWithTag("Player")`, and `IsPlayerInDetectionRange()` all go away. Replaced by a `FactionMember _currentTarget` (resolved via `TargetRegistry.FindClosestHostile`) and `_currentTarget.Damageable.TryReceiveHit()` / `TakeDamage()` calls in attack execution.

Neutral entities naturally never engage because their faction's `hostileFactions` list is empty — there is no "neutral mode" toggle anymore, the data drives behavior.

### Scope

**In Scope:**

- New `FactionSO` class + starter assets: `Faction_Player`, `Faction_Monsters` (Hostile to Player), `Faction_Bandits` (Hostile to Player), `Faction_Neutral` (no hostiles).
- New `IDamageable` interface with `bool IsDead`, `void TakeDamage(float)`, and `HitResult TryReceiveHit(GameObject attacker)`.
- New `FactionMember` MonoBehaviour with `[SerializeField] FactionSO _factionOverride` (optional) and Entity-SO-driven fallback via `PersistentID.Entity.Faction`.
- New static `TargetRegistry` with `Register`/`Unregister`/`FindClosestHostile` and a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset.
- Add `[SerializeField] FactionSO _faction` field + public `Faction` getter on the `Entity` base SO.
- Refactor `EntityBrain` to use `TargetRegistry.FindClosestHostile()` and `IDamageable` for attack execution. Remove `_canEngagePlayer`, all `_player*` fields, all `Player.*` type references; remove `using Game.Player;`.
- `PlayerHealth` implements `IDamageable` (delegating `TryReceiveHit` to `PlayerCombat`).
- `EntityHealth` implements `IDamageable` (`TakeDamage` already matches; `TryReceiveHit` returns `HitResult.NotBlocked` since AI entities don't block today).
- Wire `FactionMember` on the existing Player prefab (with `_factionOverride = Faction_Player`) and the `Entity_base.prefab` (faction-from-SO).
- Migrate existing Entity SO assets:
  - All 5 `EnemyType_*.asset` under `Assets/_Game/Data/Enemies/` → `Faction_Monsters`
  - `NPC_Bandit.asset` → `Faction_Bandits`
  - All other NPC assets (`NPC_Blacksmith`, `NPC_Elder`, `NPC_Innkeeper`, `NPC_Merchant`, `NPC_Guard`, `NPC_Villager`) → `Faction_Neutral`
- Strip the now-orphaned `_canEngagePlayer` value from `Entity_base.prefab` and `Monster_DarknessSpider Variant.prefab` (Unity will auto-strip on next reimport once the C# field is removed; verify YAML cleanup).
- EditMode tests for `FactionSO.IsHostileTo()`, `FactionSO.IsAlliedWith()`, and `TargetRegistry.FindClosestHostile()`.

**Out of Scope:**

- `PlayerEntity : Entity` SO (deferred — Player prefab has no `PersistentID`, so the SO would be unreachable; revisit if/when player data needs unification).
- Friendly NPC AI assist behavior (e.g. `NPC_Guard` engaging `NPC_Bandit`) — separate follow-up story; the foundation must support it but no friendly AI is built or wired here.
- Runtime faction reputation changes (e.g. player attacks a guard → guards become hostile) — data model is static for now.
- Faction-based dialogue gating, shop refusal, etc.
- Line-of-sight raycast for detection — registry returns by distance only; LoS deferred to stealth epic.
- New AI states or transitions — the brain's state machine is unchanged; only the targeting source is swapped.
- Migrating Player onto `EntityHealth` — intentionally kept separate to preserve `PlayerStats.Defense`, `OnPlayerDied`, and `OnPlayerHealthChanged` paths.
- Per-target threat/aggression weighting (closest hostile is good enough for v1).
- Spatial partitioning for the registry (current entity count makes a linear scan trivially cheap).
- Auto-targeting back at attackers (a neutral NPC that gets hit will not retaliate in v1 — would need a damage-source event hookup; explicitly deferred).

## Context for Development

### Codebase Patterns

- **ScriptableObject data architecture (project-context.md):** All authored static data is SO. SO **class definitions** live under `Assets/_Game/ScriptableObjects/<Category>/`; SO **asset instances** under `Assets/_Game/Data/<Category>/`. `FactionSO` follows this split exactly.
- **Static singleton/registry for runtime entity tracking:** Precedent set by `WorldStateManager` (in `Core.unity`, persistent singleton via `DontDestroyOnLoad`). `TargetRegistry` is lighter — a pure static class (no MonoBehaviour) because it holds only live components, no save-state. Domain-reload reset via `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- **`OnEnable`/`OnDisable` register/unregister:** Mandatory pattern (project-context.md "Event subscription"). Same applies to registry membership. **Critical OnDisable null-guard rule** (root CLAUDE.md): `Awake` may set `enabled = false` before `OnEnable` runs — must guard `OnDisable` with a `_registered` bool to avoid unregistering something that was never registered.
- **`GameLog` with per-class `TAG` constant:** Never `Debug.Log`. Every new class needs `private const string TAG = "[Faction]";` (or similar). `GameLog.Error` writes to `game_log.txt`; `GameLog.Info`/`Warn` are stripped in Release builds.
- **`PersistentID.Entity` is the runtime SO accessor** for entity GameObjects — `EntityBrain` already reads `_persistentID.Entity.DetectionRange` etc. The new `Faction` getter follows the same pattern: `_persistentID.Entity.Faction`. **Note:** Player.prefab has no `PersistentID` (verified via grep) — `FactionMember._factionOverride` is the escape hatch for that case.
- **`HitResult` enum (`Game.Combat`):** Defined inline in `PlayerCombat.cs:12` (not a separate file). Already used by `PlayerCombat.TryReceiveHit`. `IDamageable.TryReceiveHit` returns this same enum.
- **Chainable `Init(...)` on SOs for test setup:** `WorldFact.Init(string eventKey)` returns `this` so tests can do `ScriptableObject.CreateInstance<WorldFact>().Init("test")`. `FactionSO` adds an analogous `InitForTest(List<FactionSO> hostile, List<FactionSO> allied)` to keep test code clean without reflection.
- **EditMode test patterns:** Two precedents in the project:
  - **Pure-formula simulation** (`EnemyBrainStateTests.cs`): no MonoBehaviour, no scene — re-implements logic helpers in the test class. Used for `FactionSO.IsHostileTo` math.
  - **AddComponent + reflection cleanup** (`WorldStateManagerFactsTests.cs`): `new GameObject().AddComponent<T>()` in `SetUp`, `DestroyImmediate` in `TearDown`, optional reflection to reset static fields. Used for `TargetRegistry` integration tests that need real `FactionMember` MonoBehaviours.
- **Two-collider NPC layer rule** (`Prefabs/CLAUDE.md`): NPCs use a child `Hitbox` (Layer 7 — CharacterHitbox) for weapon detection and a child `InteractionCollider` (Layer 8) for interaction. `FactionMember` lives on the **root** (same GO as `EntityHealth` + `NPCPresence`) — the registry returns the root, so consumers can `GetComponent<EntityHealth>()` cleanly. Unchanged by this spec, but relevant when wiring `FactionMember` on `Entity_base.prefab`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EntityBrain.cs` | Brain to refactor — entry point for all changes. Lines 31 (`_canEngagePlayer`), 36–39 (player fields), 75–115 (`Start` player setup), 147–148 (`IsPlayerInDetectionRange`), 186–192 (`RespondToDetectedPlayer`), 194–203 (`HandleWarning`), 207–215 (`FacePlayer`), 217–241 (`HandleEngage`), 243–268 (`HandleAttack`), 277–303 (`ExecuteAttack`), 354–368 (`TransitionToEngaging`) all touch the player coupling. |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | Add `IDamageable` implementation. `TakeDamage(float)` and `IsDead` already match — add `TryReceiveHit(GameObject) → HitResult.NotBlocked`. |
| `Assets/_Game/Scripts/Player/PlayerHealth.cs` | Add `IDamageable` implementation. Add `_playerCombat` ref; delegate `TryReceiveHit` to it. |
| `Assets/_Game/Scripts/Combat/PlayerCombat.cs` | **Reference only** — read `TryReceiveHit(GameObject) → HitResult` at lines 454–490 to confirm signature parity with `IDamageable`. No changes. |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Add `[SerializeField] FactionSO _faction;` + `public FactionSO Faction => _faction;`. Add `using Game.Factions;`. |
| `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` | **No code changes** — inherits `Faction` from base. |
| `Assets/_Game/ScriptableObjects/Entities/Monsters/MonsterEntity.cs` | **No code changes** — inherits `Faction` from base. |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | **No changes** — dialogue/interaction is orthogonal to targeting; included only to confirm faction work doesn't touch it. |
| `Assets/_Game/Scripts/Combat/WeaponHitbox.cs` | **No changes** — already operates on generic `EntityHealth`. The player's weapon will continue to hit any entity with `EntityHealth`. Confirms the AI-side damage path is the only coupling needing surgery. |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Reference for the `_persistentID.Entity` accessor pattern that `FactionMember` mirrors. |
| `Assets/_Game/ScriptableObjects/Facts/WorldFact.cs` | Reference for the chainable `Init()` pattern that `FactionSO.InitForTest()` will mirror. |
| `Assets/Tests/EditMode/EnemyBrainStateTests.cs` | Reference for pure-formula EditMode test style. |
| `Assets/Tests/EditMode/WorldStateManagerFactsTests.cs` | Reference for `AddComponent` + reflection + `_cleanup` test style. |
| `Assets/Tests/EditMode/Tests.EditMode.asmdef` | Already references `Game` assembly — `FactionTests.cs` and `TargetRegistryTests.cs` slot in without asmdef changes. |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Add `FactionMember` to root; assign `_factionOverride = Faction_Player`; wire `PlayerHealth._playerCombat` to sibling `PlayerCombat`. No `PersistentID` exists or is needed. |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | Add `FactionMember` to root; assign `_persistentID`; leave `_factionOverride` null. Strip orphaned `_canEngagePlayer: 0` from line 101. |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | Strip override `_canEngagePlayer: 1` at line 127–129; faction inherited from `EnemyType_DarknessSpider.asset → Faction_Monsters`. |
| `_bmad-output/project-context.md` | Authoritative coding rules (57). Mandatory read before implementing. |
| `Assets/_Game/CLAUDE.md`, `Assets/_Game/Scripts/Player/CLAUDE.md`, `Assets/_Game/Scripts/Combat/CLAUDE.md`, `Assets/_Game/Prefabs/CLAUDE.md`, `Assets/_Game/Prefabs/Entities/Monsters/CLAUDE.md` | Folder-specific rules. Note: `refresh_unity(mode="force")` after raw YAML prefab edits destroys the edits — use `if_dirty`. |

### Technical Decisions

1. **Symmetric hostility, listed on both sides.** `FactionSO.IsHostileTo(other)` returns `true` only if `other` is in **this** faction's `hostileFactions` list — it does **not** auto-check the inverse. Designers must list hostility in both faction assets when symmetric AI behavior is wanted. Documented in `FactionSO.cs` summary.
2. **Allied list is informational for now.** No code in this story consumes `alliedFactions`; the field exists so the follow-up assist-AI story can read it without a schema change. Documented as such.
3. **`FactionMember` faction-resolution priority:** `_factionOverride` (if set) → `PersistentID.Entity.Faction` (if PersistentID + Entity SO present) → fail (log error, disable component). This handles both Player (override) and existing entities (SO-driven) without a special case.
4. **`PlayerEntity : Entity` SO deferred.** Player.prefab does not currently have a `PersistentID`. Creating a `PlayerEntity` SO it cannot be wired to would be dead code. Revisit only if a future story needs to data-drive player stats from an Entity SO.
5. **`TargetRegistry` is a pure static class.** No `MonoBehaviour`, no scene asset, no manual lifecycle. Domain reload in Editor + `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset ensure clean state on play-mode enter and after Recompile-While-Playing.
6. **Registry uses `HashSet<FactionMember>` for O(1) add/remove; query is linear scan with `sqrMagnitude`.** Acceptable for the prototype's < 100 active entities. Spatial partitioning is out of scope.
7. **`FindClosestHostile` skips dead members** by calling `member.Damageable.IsDead`. `IDamageable` therefore includes `bool IsDead { get; }`. Avoids ever returning a corpse as a target — important for the `HandleEngage` → `DisengageFromCombat` path.
8. **`EntityBrain` caches `_currentTarget` between ticks** and re-queries the registry only while `Idle` / `Patrolling`. Avoids per-frame iteration during sustained combat. During Engaging/Attacking, the cached target is reused; dropped on disengage or target-death.
9. **`FactionMember` exposes `Transform`, `IDamageable Damageable`, `FactionSO Faction`** — ergonomic accessors so `EntityBrain` reads `_currentTarget.Transform.position` and `_currentTarget.Damageable.TryReceiveHit(gameObject)` without an extra `GetComponent`.
10. **`_engageImmediately` and the `Warning` state are preserved verbatim.** They operate on `_currentTarget.Transform` instead of `_player` — no behavior change for existing monster prefabs.
11. **`NPCPresence` is NOT touched by this story.** NPCs that should be neutral get `Faction_Neutral` on their `NPCEntity` SO; `NPC_Bandit` gets `Faction_Bandits`. Dialogue/interaction is orthogonal.
12. **`HitResult` enum stays in `PlayerCombat.cs`** for v1. Optionally extract to its own file in a follow-up tidy story — not required for this work.
13. **Test pattern split:** `FactionSO` tests use pure-formula style (no MonoBehaviour, mirrors `EnemyBrainStateTests`). `TargetRegistry` tests use `AddComponent<FactionMember>` style (mirrors `WorldStateManagerFactsTests`) because the registry is keyed on real `FactionMember` instances and exercising the OnEnable/OnDisable lifecycle is the only way to catch the OnDisable null-guard bug if it regresses.
14. **Faction asset asymmetry choice:** `Faction_Bandits` is created even though no AI behavior is wired for them in this story. Reason: `NPC_Bandit.asset` already exists and shouldn't be lumped into `Faction_Monsters` (would behave like a creature). Better to land the data shape correct now and let the follow-up story add NPC AI later.
15. **`_canEngagePlayer` YAML cleanup:** When `EntityBrain.cs` no longer declares the field, Unity strips it from prefab YAML on the next domain reload. The dev should open both prefabs in the Editor, observe no warning, save, and verify the YAML — and **use `refresh_unity(mode="if_dirty")` not `force`** (root CLAUDE.md gotcha).

## Implementation Plan

> Tasks are ordered by dependency: data layer → interface → registry/component → consumers → wiring → migration → tests. Each task is self-contained and references exact files.

### Tasks

- [x] **Task 1: Create `FactionSO` class and 4 starter faction assets**
  - Files to create:
    - `Assets/_Game/ScriptableObjects/Factions/FactionSO.cs`
    - `Assets/_Game/Data/Factions/Faction_Player.asset`
    - `Assets/_Game/Data/Factions/Faction_Monsters.asset`
    - `Assets/_Game/Data/Factions/Faction_Bandits.asset`
    - `Assets/_Game/Data/Factions/Faction_Neutral.asset`
  - Action:
    1. Create both folders (`Assets/_Game/ScriptableObjects/Factions/` and `Assets/_Game/Data/Factions/`).
    2. Write `FactionSO.cs` with this exact body:
       ```csharp
       using System.Collections.Generic;
       using UnityEngine;

       namespace Game.Factions
       {
           [CreateAssetMenu(menuName = "Game/Faction", fileName = "Faction_")]
           public class FactionSO : ScriptableObject
           {
               [Tooltip("Display name shown in debug tools.")]
               public string factionName;

               [Tooltip("Factions this faction will actively engage in combat. Set symmetrically — if A lists B, B should usually list A.")]
               [SerializeField] private List<FactionSO> _hostileFactions = new();

               [Tooltip("Informational for follow-up assist behavior — not consumed in v1.")]
               [SerializeField] private List<FactionSO> _alliedFactions = new();

               public bool IsHostileTo(FactionSO other) => other != null && _hostileFactions.Contains(other);
               public bool IsAlliedWith(FactionSO other) => other != null && _alliedFactions.Contains(other);

               #if UNITY_EDITOR
               // Test-only chainable setup. Mirrors WorldFact.Init pattern.
               public FactionSO InitForTest(List<FactionSO> hostile, List<FactionSO> allied = null)
               {
                   _hostileFactions = hostile ?? new List<FactionSO>();
                   _alliedFactions = allied ?? new List<FactionSO>();
                   return this;
               }
               #endif
           }
       }
       ```
    3. In the Unity Editor, right-click `Assets/_Game/Data/Factions/` → Create → Game → Faction, and create the four assets. Assign:
       - `Faction_Player.asset` — `factionName = "Player"`, `hostileFactions = [Faction_Monsters, Faction_Bandits]`
       - `Faction_Monsters.asset` — `factionName = "Monsters"`, `hostileFactions = [Faction_Player]`
       - `Faction_Bandits.asset` — `factionName = "Bandits"`, `hostileFactions = [Faction_Player]`
       - `Faction_Neutral.asset` — `factionName = "Neutral"`, `hostileFactions = []`
  - Notes: `#if UNITY_EDITOR` guard on `InitForTest` keeps it out of Release builds. Test asmdef is Editor-only, so it can still call it.

- [x] **Task 2: Create `IDamageable` interface**
  - File to create: `Assets/_Game/Scripts/Combat/IDamageable.cs`
  - Action: Write the file with this exact body:
    ```csharp
    using UnityEngine;

    namespace Game.Combat
    {
        /// <summary>
        /// Contract for any GameObject that can receive damage from AI attacks
        /// or player weapons. Implemented by EntityHealth (AI) and PlayerHealth (Player).
        /// </summary>
        public interface IDamageable
        {
            bool IsDead { get; }
            void TakeDamage(float amount);
            HitResult TryReceiveHit(GameObject attacker);
        }
    }
    ```
  - Notes:
    - `HitResult` is already in `Game.Combat` (defined inline in `PlayerCombat.cs:12`) so no extra using needed.
    - `IsDead` is required by `TargetRegistry.FindClosestHostile` to skip corpses.

- [x] **Task 3: Add `Faction` field to `Entity` base SO**
  - File: `Assets/_Game/ScriptableObjects/Entities/Entity.cs`
  - Action:
    1. Add `using Game.Factions;` to the using block at the top.
    2. After the existing `[Header("Stats")]` block (line 16), add a new `[Header("Faction")]` section:
       ```csharp
       [Header("Faction")]
       [SerializeField] private FactionSO _faction;
       public FactionSO Faction => _faction;
       ```
       Place the public getter alongside the other `=> _xxx` getters (around line 56).
  - Notes: Existing `MonsterEntity` and `NPCEntity` assets will have `_faction = null` after the change — Task 12 migrates them.

- [x] **Task 4: Create `TargetRegistry` static class**
  - File to create: `Assets/_Game/Scripts/AI/TargetRegistry.cs`
  - Action: Write the file with this exact body:
    ```csharp
    using System.Collections.Generic;
    using Game.Factions;
    using UnityEngine;

    namespace Game.AI
    {
        /// <summary>
        /// Static runtime registry of all live FactionMember components.
        /// Queried by EntityBrain to find targets without GameObject.FindGameObjectWithTag.
        /// Reset on play-mode enter via SubsystemRegistration; not persisted across scenes intentionally
        /// (FactionMember.OnEnable re-registers on additive scene loads automatically).
        /// </summary>
        public static class TargetRegistry
        {
            private static readonly HashSet<FactionMember> _members = new();

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetOnPlay() => _members.Clear();

            public static void Register(FactionMember member)
            {
                if (member == null) return;
                _members.Add(member);
            }

            public static void Unregister(FactionMember member)
            {
                if (member == null) return;
                _members.Remove(member);
            }

            /// <summary>
            /// Returns the closest live member whose faction is hostile to <paramref name="myFaction"/>
            /// and within <paramref name="maxRange"/> of <paramref name="origin"/>, or null.
            /// </summary>
            public static FactionMember FindClosestHostile(FactionSO myFaction, Vector3 origin, float maxRange)
            {
                if (myFaction == null) return null;
                float bestSqr = maxRange * maxRange;
                FactionMember best = null;
                foreach (var m in _members)
                {
                    if (m == null) continue;
                    if (m.Faction == null) continue;
                    if (!myFaction.IsHostileTo(m.Faction)) continue;
                    if (m.Damageable == null || m.Damageable.IsDead) continue;
                    float sqr = (m.Transform.position - origin).sqrMagnitude;
                    if (sqr <= bestSqr)
                    {
                        bestSqr = sqr;
                        best = m;
                    }
                }
                return best;
            }
        }
    }
    ```
  - Notes: `sqrMagnitude` avoids square roots in the hot loop. `SubsystemRegistration` runs before any `Awake`, defending against stale `HashSet` state from Recompile-While-Playing.

- [x] **Task 5: Create `FactionMember` MonoBehaviour**
  - File to create: `Assets/_Game/Scripts/AI/FactionMember.cs`
  - Action: Write the file with this exact body:
    ```csharp
    using Game.Combat;
    using Game.Core;
    using Game.Factions;
    using Game.World;
    using UnityEngine;

    namespace Game.AI
    {
        /// <summary>
        /// Tags a GameObject as a targetable participant in faction combat.
        /// Self-registers with TargetRegistry on enable.
        /// Faction is sourced from PersistentID.Entity.Faction by default; override per-instance with _factionOverride
        /// (the Player uses _factionOverride because Player.prefab has no PersistentID).
        /// </summary>
        public class FactionMember : MonoBehaviour
        {
            private const string TAG = "[Faction]";

            [Tooltip("Optional. If null, faction is read from PersistentID.Entity.Faction.")]
            [SerializeField] private FactionSO _factionOverride;
            [Tooltip("Optional. Used to resolve faction from the Entity SO when no override is set.")]
            [SerializeField] private PersistentID _persistentID;

            private IDamageable _damageable;
            private FactionSO _faction;
            private bool _registered;

            public FactionSO Faction => _faction;
            public IDamageable Damageable => _damageable;
            public Transform Transform => transform;

            private void Awake()
            {
                _damageable = GetComponent<IDamageable>();
                if (_damageable == null)
                {
                    GameLog.Error(TAG, $"{gameObject.name}: no IDamageable component found on root — FactionMember disabled");
                    enabled = false;
                    return;
                }

                if (_factionOverride != null)
                    _faction = _factionOverride;
                else if (_persistentID != null && _persistentID.Entity != null)
                    _faction = _persistentID.Entity.Faction;

                if (_faction == null)
                {
                    GameLog.Error(TAG, $"{gameObject.name}: no faction resolved (override null and Entity SO faction null) — FactionMember disabled");
                    enabled = false;
                }
            }

            private void OnEnable()
            {
                if (_faction == null || _damageable == null) return;
                TargetRegistry.Register(this);
                _registered = true;
            }

            private void OnDisable()
            {
                if (!_registered) return; // Guard: Awake may disable before OnEnable runs (root CLAUDE.md rule)
                TargetRegistry.Unregister(this);
                _registered = false;
            }
        }
    }
    ```
  - Notes: `_registered` flag is the project's `OnDisable` null-guard pattern.

- [x] **Task 6: `EntityHealth` implements `IDamageable`**
  - File: `Assets/_Game/Scripts/AI/EntityHealth.cs`
  - Action:
    1. Add `using Game.Combat;` to the usings.
    2. Change class declaration: `public class EntityHealth : MonoBehaviour` → `public class EntityHealth : MonoBehaviour, IDamageable`
    3. Add this method (placement: just below `TakeDamage`):
       ```csharp
       public HitResult TryReceiveHit(GameObject attacker) => HitResult.NotBlocked;
       ```
  - Notes: `TakeDamage(float)` and `IsDead` already match the interface — no signature changes.

- [x] **Task 7: `PlayerHealth` implements `IDamageable`**
  - File: `Assets/_Game/Scripts/Player/PlayerHealth.cs`
  - Action:
    1. Verify `using Game.Combat;` is in the usings (it should be — already present per Read).
    2. Change class declaration: `public class PlayerHealth : MonoBehaviour` → `public class PlayerHealth : MonoBehaviour, IDamageable`
    3. Add a serialized reference to `PlayerCombat`:
       ```csharp
       [SerializeField] private PlayerCombat _playerCombat;
       ```
       Place it next to the existing `_playerStats` field. Add a `Game.Combat` using if not already present.
    4. In `Awake`, after the existing `_playerStats` null check, add:
       ```csharp
       if (_playerCombat == null)
           GameLog.Warn(TAG, "PlayerCombat not assigned — incoming hits will not check block/dodge");
       ```
    5. Add the interface method (placement: just below `TakeDamage`):
       ```csharp
       public HitResult TryReceiveHit(GameObject attacker)
       {
           if (IsDead) return HitResult.NotBlocked;
           return _playerCombat != null ? _playerCombat.TryReceiveHit(attacker) : HitResult.NotBlocked;
       }
       ```
    6. In the Inspector for `Player.prefab` (done in Task 9), wire `_playerCombat` to the sibling `PlayerCombat` component.
  - Notes: `IsDead` and `TakeDamage` already match. The delegation preserves Defense / block / dodge / perfect-block exactly as today.

- [x] **Task 8: Refactor `EntityBrain` to use registry + `IDamageable`**
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action — Imports:
    1. Add `using Game.Combat;` and `using Game.Factions;`.
    2. Remove `using Game.Player;` (no longer needed).
  - Action — Remove these declarations:
    - The `[Header("Behavior")]` block's `_canEngagePlayer` `[SerializeField]` (line 30–31) including its `[Tooltip]`. Keep `_engageImmediately`.
    - The `_player`, `_playerCombat`, `_playerHealth` fields (lines 36–39).
  - Action — Add these declarations (under the existing `[SerializeField] AIAnimationDriver _animationDriver` block):
    ```csharp
    [Tooltip("This entity's faction membership — drives detection & engagement decisions.")]
    [SerializeField] private FactionMember _selfFactionMember;

    private FactionMember _currentTarget;
    ```
  - Action — `Awake()` additions (after the existing `_entityHealth` resolution):
    ```csharp
    if (_selfFactionMember == null) _selfFactionMember = GetComponent<FactionMember>();
    if (_selfFactionMember == null)
    {
        GameLog.Error(TAG, $"{gameObject.name}: FactionMember not found on same GameObject — EntityBrain disabled");
        enabled = false;
        return;
    }
    ```
  - Action — Delete `Start()`'s player-resolution block (lines 77–103, the entire `if (_canEngagePlayer) { ... }` and the `WarningRange >= DetectionRange` runtime guard inside it). The `WarningRange` guard moves outside the deleted block:
    ```csharp
    private void Start()
    {
        if (!_engageImmediately &&
            _persistentID.Entity.WarningRange >= _persistentID.Entity.DetectionRange)
        {
            GameLog.Warn(TAG, $"{gameObject.name}: WarningRange ({_persistentID.Entity.WarningRange}) >= DetectionRange ({_persistentID.Entity.DetectionRange}) — warning band empty; entity will instant-engage. Check the Entity SO.");
        }

        if (_waypoints == null || _waypoints.Length == 0)
        {
            GameLog.Info(TAG, $"{gameObject.name}: No waypoints assigned — entering Idle wander");
            TransitionToIdle(transform.position);
            return;
        }
        _currentWaypoint = _waypoints.Length - 1;
        AdvanceToNextWaypoint();
        _state = EntityState.Patrolling;
    }
    ```
  - Action — Replace `IsPlayerInDetectionRange()` with two helpers:
    ```csharp
    private bool TryAcquireTarget()
    {
        if (_selfFactionMember.Faction == null) return false;
        _currentTarget = TargetRegistry.FindClosestHostile(
            _selfFactionMember.Faction,
            transform.position,
            _persistentID.Entity.DetectionRange);
        return _currentTarget != null;
    }

    private bool HasValidTarget() =>
        _currentTarget != null && _currentTarget.Damageable != null && !_currentTarget.Damageable.IsDead;
    ```
  - Action — Rename `RespondToDetectedPlayer()` → `RespondToDetectedTarget()`. Body becomes:
    ```csharp
    private void RespondToDetectedTarget()
    {
        if (_engageImmediately) { TransitionToEngaging(); return; }
        float dist = Vector3.Distance(transform.position, _currentTarget.Transform.position);
        if (dist <= _persistentID.Entity.WarningRange) TransitionToEngaging();
        else TransitionToWarning();
    }
    ```
  - Action — `HandleIdle()` / `HandlePatrol()`: replace `if (_canEngagePlayer && IsPlayerInDetectionRange())` with `if (TryAcquireTarget()) { RespondToDetectedTarget(); return; }`.
  - Action — `HandleWarning()`: change `if (_player == null) { CancelWarning(); return; }` → `if (!HasValidTarget()) { CancelWarning(); return; }`. Replace all `_player.position` reads with `_currentTarget.Transform.position`.
  - Action — Rename `FacePlayer()` → `FaceTarget()`. Body: `Vector3 dir = _currentTarget.Transform.position - transform.position;` (rest unchanged). Update the call site in `HandleWarning()`.
  - Action — `HandleEngage()`: replace `if (_player == null) { ... DisengageFromCombat(); return; }` with `if (!HasValidTarget()) { GameLog.Warn(TAG, "Target lost — disengaging"); DisengageFromCombat(); return; }`. Replace `_player.position` with `_currentTarget.Transform.position`.
  - Action — `HandleAttack()`: replace `if (_player == null) { DisengageFromCombat(); return; }` with `if (!HasValidTarget()) { DisengageFromCombat(); return; }`. Replace `_player.position` with `_currentTarget.Transform.position`.
  - Action — Rewrite `ExecuteAttack()`:
    ```csharp
    private void ExecuteAttack()
    {
        _animationDriver?.TriggerAttack();
        _attackCooldownTimer = _persistentID.Entity.AttackCooldown;
        GameLog.Info(TAG, $"{gameObject.name} attacks {_currentTarget.Transform.name}");

        IDamageable target = _currentTarget.Damageable;
        if (target == null || target.IsDead) return;

        HitResult result = target.TryReceiveHit(gameObject);
        switch (result)
        {
            case HitResult.PerfectBlock:
                GameLog.Info(TAG, $"{gameObject.name} attack staggered by perfect block");
                break;
            case HitResult.Blocked:
                GameLog.Info(TAG, $"{gameObject.name} attack blocked — no damage");
                break;
            case HitResult.Dodged:
                GameLog.Info(TAG, $"{gameObject.name} attack dodged — no damage");
                break;
            case HitResult.NotBlocked:
                target.TakeDamage(_persistentID.Entity.AttackDamage);
                break;
        }
    }
    ```
  - Action — `TransitionToEngaging()`: replace `_agent.SetDestination(_player.position)` with `_agent.SetDestination(_currentTarget.Transform.position)`.
  - Action — `DisengageFromCombat()`: add `_currentTarget = null;` as the first line. The next idle/patrol tick will re-acquire via `TryAcquireTarget`.
  - Notes:
    - `_canEngagePlayer = false` behavior is replaced by "this entity's faction has no hostiles" — Task 12 assigns `Faction_Neutral` to NPCs that were previously `_canEngagePlayer = false`.
    - No state-machine changes; only the targeting source and damage call site move.

- [x] **Task 9: Wire `FactionMember` on `Player.prefab`**
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action (use MCP `manage_components` or Editor Inspector):
    1. Add `FactionMember` component to the Player **root** GO.
    2. Set `_factionOverride` = `Faction_Player.asset`.
    3. Leave `_persistentID` empty (Player has no PersistentID).
    4. On the existing `PlayerHealth` component, set `_playerCombat` = sibling `PlayerCombat` reference.
  - Notes: Player root already implements `IDamageable` via `PlayerHealth` after Task 7 — `FactionMember.Awake()` will find it via `GetComponent<IDamageable>()`. No new layer or collider work; Player already has its Layer setup.

- [x] **Task 10: Wire `FactionMember` on `Entity_base.prefab`; strip `_canEngagePlayer` YAML**
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab`
  - Action:
    1. Add `FactionMember` component to the root.
    2. Set `_factionOverride` = empty.
    3. Set `_persistentID` = the existing `PersistentID` component on the root (drag from the same GO).
    4. After completing Task 8 (where `_canEngagePlayer` is removed from `EntityBrain.cs`), open `Entity_base.prefab` in the Editor — Unity will silently drop the orphaned `_canEngagePlayer: 0` field on save. Save the prefab.
    5. **Use `refresh_unity(mode="if_dirty")` NOT `force`** (root CLAUDE.md rule).
    6. Verify YAML by reading line 101 of the prefab — `_canEngagePlayer` should be gone.
  - Notes: `_persistentID.Entity` is assigned per-variant (Monster_Spider → EnemyType_DarknessSpider, NPC_base Variant → respective NPCEntity). Faction is data-driven from there.

- [x] **Task 11: Strip `_canEngagePlayer` override from `Monster_DarknessSpider Variant.prefab`**
  - File: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`
  - Action:
    1. Open the prefab in the Editor.
    2. The `_canEngagePlayer` override at lines 127–129 will be auto-stripped when the field no longer exists on `EntityBrain` (after Task 8). Confirm by reading the YAML — the override block should be gone.
    3. The variant should now inherit FactionMember from the base prefab (no per-variant override needed). Faction comes from `EnemyType_DarknessSpider.asset → Faction_Monsters` (Task 12).
    4. **Use `refresh_unity(mode="if_dirty")` NOT `force`**.
  - Notes: If Unity does NOT auto-strip (rare), manually delete the three YAML lines (127, 128, 129) and refresh in `if_dirty` mode.

- [x] **Task 12: Migrate existing Entity SO assets to factions**
  - Files (12 assets):
    - `Assets/_Game/Data/Enemies/EnemyType_Grunt.asset`
    - `Assets/_Game/Data/Enemies/EnemyType_GiantRat.asset`
    - `Assets/_Game/Data/Enemies/EnemyType_GiantViper.asset`
    - `Assets/_Game/Data/Enemies/EnemyType_FantasyWolf.asset`
    - `Assets/_Game/Data/Enemies/EnemyType_DarknessSpider.asset`
    - `Assets/_Game/Data/Entities/Entity_HumanoidNPC.asset`
    - `Assets/_Game/Data/NPCs/Bandit/NPC_Bandit.asset`
    - `Assets/_Game/Data/NPCs/BlackSmith/NPC_Blacksmith.asset`
    - `Assets/_Game/Data/NPCs/Elder/NPC_Elder.asset`
    - `Assets/_Game/Data/NPCs/Innkeeper/NPC_Innkeeper.asset`
    - `Assets/_Game/Data/NPCs/Merchant/NPC_Merchant.asset`
    - `Assets/_Game/Data/NPCs/NPC_Guard/NPC_Guard.asset`
    - `Assets/_Game/Data/NPCs/Villager/NPC_Villager.asset`
  - Action: open each asset and assign `Faction`:
    - All 5 `EnemyType_*.asset` → `Faction_Monsters`
    - `Entity_HumanoidNPC.asset` → `Faction_Neutral` (humanoid NPC base; per-variant SOs override)
    - `NPC_Bandit.asset` → `Faction_Bandits`
    - `NPC_Blacksmith.asset`, `NPC_Elder.asset`, `NPC_Innkeeper.asset`, `NPC_Merchant.asset`, `NPC_Guard.asset`, `NPC_Villager.asset` → `Faction_Neutral`
  - Notes: Any future Entity SO that's added without a faction will trigger `FactionMember`'s "no faction resolved" Error log at load — that's the migration signal.

- [x] **Task 13: EditMode tests — `FactionTests.cs` and `TargetRegistryTests.cs`**
  - Files to create:
    - `Assets/Tests/EditMode/FactionTests.cs`
    - `Assets/Tests/EditMode/TargetRegistryTests.cs`
  - Action — `FactionTests.cs` (pure-formula style, mirrors `EnemyBrainStateTests`):
    ```csharp
    using System.Collections.Generic;
    using Game.Factions;
    using NUnit.Framework;
    using UnityEngine;

    namespace Tests.EditMode
    {
        public class FactionTests
        {
            private readonly List<Object> _cleanup = new();

            private FactionSO MakeFaction(string name) =>
                Track(ScriptableObject.CreateInstance<FactionSO>(), name);

            private FactionSO Track(FactionSO f, string name)
            {
                f.factionName = name;
                _cleanup.Add(f);
                return f;
            }

            [TearDown]
            public void TearDown()
            {
                foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
                _cleanup.Clear();
            }

            [Test]
            public void IsHostileTo_ReturnsTrue_WhenOtherInHostileList()
            {
                var a = MakeFaction("A");
                var b = MakeFaction("B");
                a.InitForTest(new List<FactionSO> { b });
                Assert.That(a.IsHostileTo(b), Is.True);
            }

            [Test]
            public void IsHostileTo_ReturnsFalse_WhenOtherNotInList()
            {
                var a = MakeFaction("A");
                var b = MakeFaction("B");
                a.InitForTest(new List<FactionSO>());
                Assert.That(a.IsHostileTo(b), Is.False);
            }

            [Test]
            public void IsHostileTo_ReturnsFalse_WhenOtherIsNull()
            {
                var a = MakeFaction("A");
                a.InitForTest(new List<FactionSO>());
                Assert.That(a.IsHostileTo(null), Is.False);
            }

            [Test]
            public void IsAlliedWith_ReturnsTrue_WhenOtherInAlliedList()
            {
                var a = MakeFaction("A");
                var b = MakeFaction("B");
                a.InitForTest(null, new List<FactionSO> { b });
                Assert.That(a.IsAlliedWith(b), Is.True);
            }

            [Test]
            public void IsHostileTo_IsAsymmetric_ByDesign()
            {
                var a = MakeFaction("A");
                var b = MakeFaction("B");
                a.InitForTest(new List<FactionSO> { b });
                b.InitForTest(new List<FactionSO>());
                Assert.That(a.IsHostileTo(b), Is.True);
                Assert.That(b.IsHostileTo(a), Is.False); // documents symmetric-by-convention contract
            }
        }
    }
    ```
  - Action — `TargetRegistryTests.cs` (AddComponent style, mirrors `WorldStateManagerFactsTests`):
    ```csharp
    using System.Collections.Generic;
    using Game.AI;
    using Game.Combat;
    using Game.Factions;
    using NUnit.Framework;
    using UnityEngine;

    namespace Tests.EditMode
    {
        public class TargetRegistryTests
        {
            private readonly List<Object> _cleanup = new();

            private class StubDamageable : MonoBehaviour, IDamageable
            {
                public bool IsDead { get; set; }
                public void TakeDamage(float amount) { }
                public HitResult TryReceiveHit(GameObject attacker) => HitResult.NotBlocked;
            }

            private FactionMember MakeMember(string name, FactionSO faction, Vector3 position, bool dead = false)
            {
                var go = new GameObject(name);
                go.transform.position = position;
                var dmg = go.AddComponent<StubDamageable>();
                dmg.IsDead = dead;
                var m = go.AddComponent<FactionMember>();
                // Wire _factionOverride via reflection (private serialized field).
                typeof(FactionMember)
                    .GetField("_factionOverride", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(m, faction);
                // Force Awake + OnEnable lifecycle (AddComponent already ran them, but _factionOverride was set after — re-toggle).
                m.enabled = false;
                m.enabled = true;
                _cleanup.Add(go);
                return m;
            }

            private FactionSO MakeFaction(string name, List<FactionSO> hostile = null)
            {
                var f = ScriptableObject.CreateInstance<FactionSO>();
                f.factionName = name;
                f.InitForTest(hostile ?? new List<FactionSO>());
                _cleanup.Add(f);
                return f;
            }

            [TearDown]
            public void TearDown()
            {
                foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
                _cleanup.Clear();
            }

            [Test]
            public void FindClosestHostile_ReturnsRegisteredHostileMember()
            {
                var fA = MakeFaction("A");
                var fB = MakeFaction("B");
                fA.InitForTest(new List<FactionSO> { fB });
                MakeMember("B1", fB, new Vector3(2, 0, 0));
                var result = TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Faction, Is.EqualTo(fB));
            }

            [Test]
            public void FindClosestHostile_SkipsNonHostileFactions()
            {
                var fA = MakeFaction("A");
                var fNeutral = MakeFaction("Neutral");
                MakeMember("N1", fNeutral, new Vector3(2, 0, 0));
                Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
            }

            [Test]
            public void FindClosestHostile_ReturnsNearestWhenMultipleHostiles()
            {
                var fA = MakeFaction("A");
                var fB = MakeFaction("B");
                fA.InitForTest(new List<FactionSO> { fB });
                var far = MakeMember("Far", fB, new Vector3(8, 0, 0));
                var near = MakeMember("Near", fB, new Vector3(2, 0, 0));
                var result = TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f);
                Assert.That(result, Is.EqualTo(near));
            }

            [Test]
            public void FindClosestHostile_SkipsDeadMembers()
            {
                var fA = MakeFaction("A");
                var fB = MakeFaction("B");
                fA.InitForTest(new List<FactionSO> { fB });
                MakeMember("Dead", fB, new Vector3(2, 0, 0), dead: true);
                Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
            }

            [Test]
            public void FindClosestHostile_SkipsOutOfRange()
            {
                var fA = MakeFaction("A");
                var fB = MakeFaction("B");
                fA.InitForTest(new List<FactionSO> { fB });
                MakeMember("Far", fB, new Vector3(20, 0, 0));
                Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
            }

            [Test]
            public void FindClosestHostile_ReturnsNull_WhenMyFactionIsNull()
            {
                Assert.That(TargetRegistry.FindClosestHostile(null, Vector3.zero, 10f), Is.Null);
            }

            [Test]
            public void Unregister_RemovesMemberFromRegistry()
            {
                var fA = MakeFaction("A");
                var fB = MakeFaction("B");
                fA.InitForTest(new List<FactionSO> { fB });
                var m = MakeMember("B1", fB, new Vector3(2, 0, 0));
                Object.DestroyImmediate(m.gameObject);
                _cleanup.Remove(m.gameObject); // already destroyed
                Assert.That(TargetRegistry.FindClosestHostile(fA, Vector3.zero, 10f), Is.Null);
            }
        }
    }
    ```
  - Notes:
    - Tests reference `Game.AI`, `Game.Combat`, `Game.Factions` namespaces — all in the `Game` assembly, already referenced by `Tests.EditMode.asmdef`.
    - `StubDamageable : MonoBehaviour, IDamageable` lets us test the real `FactionMember.Awake` path which requires an `IDamageable` sibling.
    - Reflection for `_factionOverride` mirrors the project's `WorldStateManagerFactsTests` approach for setting private serialized fields.
    - `[TearDown]` cleans up GameObjects (which triggers `FactionMember.OnDisable` → `TargetRegistry.Unregister`), keeping the registry clean between tests.

### Acceptance Criteria

- [ ] **AC1 — Existing player-vs-monster combat unchanged (regression gate)**
  - **Given:** a `Monster_DarknessSpider Variant` instance in a scene, `EnemyType_DarknessSpider.asset` assigned `Faction_Monsters`, and the Player prefab with `FactionMember._factionOverride = Faction_Player` wired.
  - **When:** the player walks into the monster's `DetectionRange`.
  - **Then:** the monster transitions Idle/Patrol → Warning (when outside `WarningRange`) → Engaging → Attacking; the player takes damage from `EntityBrain.ExecuteAttack`; observed behavior matches the pre-refactor recording.

- [ ] **AC2 — Neutral entity ignores the player (regression gate)**
  - **Given:** an entity whose `Entity.Faction == Faction_Neutral` (e.g. an `NPC_Villager` placed in the world via `NPC_base Variant.prefab` + `NPC_Villager.asset`).
  - **When:** the player walks within `DetectionRange`.
  - **Then:** the entity remains in Idle/Patrol; no transition to Warning or Engaging; no `[AI]` "detected" log lines emit.

- [ ] **AC3 — Two hostile factions engage each other**
  - **Given:** a manually-constructed test scene with `Faction_A` and `Faction_B` (each listing the other as hostile), and two entities (each with `EntityBrain` + `FactionMember`) placed in mutual `DetectionRange`.
  - **When:** play mode starts.
  - **Then:** each entity acquires the other via `TargetRegistry.FindClosestHostile`; both transition to Engaging; both attack; the survivor returns to Patrol/Idle once `_currentTarget.Damageable.IsDead == true`.

- [ ] **AC4 — Dead entity is never targeted**
  - **Given:** a hostile entity that has died (`EntityHealth.IsDead == true`).
  - **When:** another hostile-to-it entity ticks `HandleIdle` / `HandlePatrol`.
  - **Then:** `TargetRegistry.FindClosestHostile` skips the dead member; the searcher behaves as if alone (no transitions, no engagement).

- [ ] **AC5 — `_canEngagePlayer` is fully removed from the codebase and prefab YAML (regression gate)**
  - **Given:** the refactor branch.
  - **When:** running `Grep` for `_canEngagePlayer` across `Assets/_Game/Scripts/` and `Assets/_Game/Prefabs/`.
  - **Then:** zero matches in scripts; zero matches in prefab files (including `Entity_base.prefab` line 101 and `Monster_DarknessSpider Variant.prefab` lines 127–129).

- [ ] **AC6 — `EntityBrain` no longer references `Game.Player`**
  - **Given:** the refactored `EntityBrain.cs`.
  - **When:** searching the file for `using Game.Player`, `PlayerCombat`, `PlayerHealth`, or `FindGameObjectWithTag("Player")`.
  - **Then:** zero matches.

- [ ] **AC7 — Player can block / dodge / perfect-block AI attacks (regression gate)**
  - **Given:** the Player in combat with a hostile monster post-refactor.
  - **When:** the player holds block, dodges, or perfect-blocks an incoming attack.
  - **Then:** `EntityBrain.ExecuteAttack` calls `_currentTarget.Damageable.TryReceiveHit(gameObject)`; the call routes through `PlayerHealth.TryReceiveHit` → `PlayerCombat.TryReceiveHit`; returns `Blocked` / `Dodged` / `PerfectBlock` as appropriate; damage is not applied for those results; the existing `[AI]` log lines (`{name} attack blocked — no damage`, etc.) appear in the console.

- [ ] **AC8 — Registry survives play-mode restart in Editor**
  - **Given:** a play session has run with entities registered in `TargetRegistry`.
  - **When:** the developer stops play mode and restarts it.
  - **Then:** the first `FindClosestHostile` query in the new session does not return any entity from the prior session (verified because `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` clears the `HashSet` before any `Awake` runs).

- [ ] **AC9 — Edit-Mode tests pass**
  - **Given:** Tasks 1–8 + 13 complete.
  - **When:** the Unity Test Runner runs the Edit Mode tab.
  - **Then:** all tests in `FactionTests.cs` (5 tests) and `TargetRegistryTests.cs` (7 tests) pass green; no other test classes regress.

- [ ] **AC10 — Existing scenes load without `[Faction]` errors**
  - **Given:** Tasks 1–12 complete.
  - **When:** opening `StartingTown.unity`, `BanditCamp.unity` (if present), and any other region scenes in the Editor.
  - **Then:** zero compile errors; zero `[Faction]` "no faction resolved" or "no IDamageable" Error logs at scene-load time. Any such error flags an Entity SO missed in Task 12 — fix in-place.

- [ ] **AC11 — `FactionMember` startup safety net**
  - **Given:** an Entity prefab placed in a scene without a faction assigned on its Entity SO and with no `_factionOverride`.
  - **When:** the scene loads.
  - **Then:** `FactionMember.Awake` emits a `[Faction]` Error log naming the GameObject and disables the component; no NullReferenceException reaches the player; the entity is silently absent from the registry.

## Additional Context

### Dependencies

- **No new packages.** Uses existing Unity AI / NavMeshAgent, ScriptableObject, and Unity Test Framework (NUnit).
- **No upstream stories blocking this.** Builds on completed Story 2.8 (Enemy AI), Story 2.9 (Health), and the warning-state work already on `main` (commit `258b612`).
- **Downstream enabler:** unlocks Story 5.4 (NPC daily routines + town guards engaging bandits) and any future "friendly companion" feature.

### Testing Strategy

- **EditMode (primary):** Faction relationship math and TargetRegistry behavior are pure logic — covered by `FactionTests.cs` (pure-formula style) and `TargetRegistryTests.cs` (`AddComponent` + `_cleanup` style).
- **PlayMode (minimal):** the existing manual smoke loop ("spawn into BanditCamp, get attacked, kill bandit, repeat") covers integration. No new automated PlayMode tests required for v1.
- **Manual scene verification:**
  - **AC1 regression:** load StartingTown / BanditCamp, walk into a monster's detection range, confirm engagement loop unchanged.
  - **AC2 regression:** walk past `NPC_Villager` / `NPC_Blacksmith`, confirm they do not engage.
  - **AC3 scratch scene:** drop two ad-hoc-faction entities hostile to each other; verify mutual engagement. Delete after verification — do not commit.
- **Regression gates:** AC1, AC2, AC5, AC7 are the must-pass set before merging.

### Notes

- **Pre-mortem — highest-risk failure modes:**
  - **Player prefab has no IDamageable visible to FactionMember.** Player.prefab has multiple MonoBehaviours; `GetComponent<IDamageable>()` returns the first that implements the interface. Currently only `PlayerHealth` will (post-Task 7). If a future component also implements `IDamageable` and is ordered above PlayerHealth, `FactionMember` could resolve to the wrong one. Mitigation: keep the interface single-source-per-GO; if a second implementation is ever added, add a `[SerializeField] private MonoBehaviour _damageableComponent` override to `FactionMember`.
  - **Stale prefab YAML for `_canEngagePlayer`.** If the dev does `refresh_unity(mode="force")` (the known footgun), the cleanup edits to `Entity_base.prefab` and `Monster_DarknessSpider Variant.prefab` get clobbered. Mitigation: AC5 grep is the gate. Always `if_dirty`.
  - **Two-faction scene needed to verify AC3.** No existing scene exercises this path. Without a scratch scene, AC3 cannot be confirmed and the foundation might ship subtly broken for the follow-up assist-AI story.
  - **OnDisable null-guard regression.** The `_registered` flag pattern is easy to forget if `FactionMember` is later edited. Pattern is called out explicitly in the file's source comment; flag it in folder CLAUDE.md after merge.
- **Why not migrate Player onto `EntityHealth`?** It would force `PlayerStats.Defense`, `OnPlayerDied`/`OnPlayerHealthChanged` events, and the `Heal()` API into `EntityHealth`, which would either bloat the base class or require a `PlayerEntityHealth` subclass and re-routing every existing reference. The `IDamageable` interface gives us 100% of the value (uniform attack code in `EntityBrain`) at 5% of the risk. Revisit only if a future story (e.g. companion-takes-damage) needs a unified HP system.
- **Why `FactionSO` reference on `Entity` base SO?** (1) The Entity SO is already the single data source for an entity's behavioral tuning (`DetectionRange`, `AttackDamage` etc.) — faction belongs in the same place. (2) Designers can change a faction's relationships from one asset rather than touching every prefab variant. The `_factionOverride` on `FactionMember` is the escape hatch for the Player (no `PersistentID`) and any future no-Entity-SO targetable.
- **Why no `IDefender` split from `IDamageable`?** `TryReceiveHit` on `EntityHealth` is a trivial `NotBlocked` return today; splitting interfaces would force the caller to check two interfaces. Single interface, single default implementation is cheaper. Re-split if a future story introduces non-damageable defenders (parry posts, etc.) — very unlikely.
- **CLAUDE.md candidates to flag after implementation:**
  - `FactionMember` registration pattern + `_registered` OnDisable guard → add to `Assets/_Game/Scripts/AI/CLAUDE.md` (create if missing).
  - `TargetRegistry`'s `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` reset pattern is reusable for any future static registry.
  - `IDamageable` as the new cross-system damage contract → flag in `Assets/_Game/Scripts/Combat/CLAUDE.md`.
