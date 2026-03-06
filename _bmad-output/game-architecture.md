---
title: 'Game Architecture'
project: 'Unity-BMAD-test-rpg'
date: '2026-02-28'
author: 'Valentin'
version: '1.0'
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8, 9]
status: 'complete'
engine: 'Unity 6.3 LTS'
platform: 'PC (Windows, Steam)'

# Source Documents
gdd: '_bmad-output/gdd.md'
epics: '_bmad-output/epics.md'
brief: null
---

# Game Architecture

## Document Status

This architecture document is being created through the BMGD Architecture Workflow.

**Steps Completed:** 9 of 9 — ✅ Complete

---

## Executive Summary

**Echoes of the Fallen** is a Gothic-inspired third-person action RPG prototype built in Unity 6.3 LTS. The architecture uses a Component-based + Event Bus hybrid pattern built around a central `WorldStateManager` that enforces the game's core "permanent world" design pillar — every killed entity, quest outcome, and NPC death persists across sessions via per-entity PersistentIDs serialized to JSON with Steam Cloud sync. Four custom patterns cover the novel mechanics (directional attack input, permanent entities, NPC schedules, topic-unlock dialogue) with explicit implementation guides to ensure consistent AI agent implementation across all 8 epics.

---

## Project Context

### Game Overview

**Echoes of the Fallen** — Gothic-inspired third-person action RPG prototype. Single player, PC (Windows) via Steam. Unity engine (established). Solo developer, prototype scope: 1 region, 1 dungeon, 8 epics.

### Technical Scope

**Platform:** PC (Windows, Steam)
**Genre:** Third-person Action RPG
**Project Level:** Prototype (intermediate developer, BMAD workflow validation)
**Multiplayer:** None

### Core Systems

| System | Complexity | Epic |
|---|---|---|
| Third-person controller & camera | Low | 1 |
| Stamina-based directional combat | High | 2 |
| Enemy AI (patrol, engage, attack) | Medium | 2 |
| XP / Level / Learning Point progression | Medium | 3 |
| Permanent world state management | High | 2–8 |
| Day/Night cycle + NPC schedules | Medium | 4 |
| Gothic topic-based dialogue + quest system | Medium | 5 |
| Inventory & equipment | Low | 6 |
| Economy (gold, shops) | Low | 6 |
| Crafting (recipe + tier gated) | Low-Medium | 7 |
| Visibility-cone stealth + pickpocket | Low-Medium | 7 |
| Save/Load system | Medium | 8 |
| Steam integration | Low | 8 |

### Technical Requirements

- **Frame Rate:** 60fps locked target, 30fps floor
- **Resolution:** 1080p native (windowed/borderless supported)
- **Load Times:** <10s initial load, seamless within regions
- **Build Size:** <5GB
- **Min Hardware:** GTX 1060 / RX 580 class GPU, 8GB RAM
- **Input:** Keyboard & mouse only

### Complexity Drivers

**High (requires custom architectural patterns):**
- Directional attack input system (mouse direction sampled at input moment)
- Permanent world state (all entity kills, quest states, NPC deaths persisted globally)
- Story Act world state machine (global event triggers reshaping world)

**Novel Concepts (no standard Unity patterns):**
- Perfect block timing window with stagger consequence
- NPC death → quest closure cascade
- Topic-unlock state-driven dialogue (not tree-based)
- NPC schedule system gating gameplay access

### Technical Risks

1. Directional attack feel — input sampling precision and animation weight are critical to core gameplay promise
2. World state persistence scalability — tracking all world changes across a full playthrough
3. NPC schedule/day-night state machine complexity
4. 60fps performance target on GTX 1060-class hardware with dynamic lighting and open world

---

## Engine & Framework

### Selected Engine

**Unity 6.3 LTS** (released December 5, 2025 — supported until December 2027)

**Rationale:** Established engine for the project. Unity 6.3 LTS chosen for maximum stability across the full development timeline. URP selected for performance on GTX 1060-class hardware at 60fps target.

### Engine-Provided Architecture

| Component | Solution | Notes |
|---|---|---|
| Rendering | URP (Universal Render Pipeline) | Performance-first; tunable for GTX 1060-class |
| Physics | PhysX (Unity Physics) | Rigidbody, colliders, raycasts for combat hit detection |
| Audio | Unity Audio Mixer | Dynamic music states, spatial audio |
| Input | Unity Input System (new) | Action-based; required for directional attack sampling |
| Animation | Unity Animator + Animation Rigging | Blend trees for movement; state machines for combat |
| AI Navigation | Unity NavMesh | Enemy patrol and engagement pathfinding |
| Terrain | Unity Terrain | Starting region and wilderness layout |
| Build | Unity Build Profiles (Unity 6) | PC Windows target |

### Project Initialization

**Unity Starter Assets — ThirdPerson** (free, official Unity package) recommended as the movement/camera foundation for Epic 1. Import from Unity Asset Store post-project creation.

### Remaining Architectural Decisions

The following must be decided in Steps 4–7:
- Save system architecture (world state persistence strategy)
- Scene loading approach (open world continuity)
- Directional attack input sampling pattern
- Dialogue/quest data storage format
- NPC schedule state machine pattern
- Game architecture pattern (component, service locator, etc.)
- Asset loading strategy (Addressables vs. direct references)

---

## Architectural Decisions

### Decision Summary

| # | Category | Decision | Version | Affects Epics |
|---|---|---|---|---|
| 1 | Game Architecture Pattern | Component-based + Event Bus hybrid | N/A | All |
| 2 | World State Persistence | Per-entity PersistentID + WorldStateManager | N/A | 2–8 |
| 3 | Save System | JSON + Steam Cloud sync | Steamworks.NET (latest stable) | 3, 8 |
| 4 | Scene Loading | Additive scene loading (Core + region scenes) | N/A | 4–8 |
| 5 | Attack System | Timed combo (3-hit chain, animation windows, stamina gated) | N/A | 2 |
| 6 | Dialogue/Quest Data | ScriptableObjects + WorldStateManager runtime state | N/A | 5–7 |
| 7 | Asset Loading | Direct references (Addressables-ready structure) | N/A (Unity 6.3 LTS) | 1, 8 |

### Decision 1: Game Architecture Pattern

**Choice:** Component-based + Event Bus hybrid

**Rationale:** Unity MonoBehaviours for all game systems (natural engine fit, full ecosystem compatibility). A central `GameEventBus` implemented as a ScriptableObject channel system handles cross-system communication — combat kills notify world state, quest state changes propagate to dialogue, Story Act triggers broadcast to all listeners. Decouples systems without ECS learning curve.

**Implementation:**
- All systems are MonoBehaviour components on GameObjects
- Cross-system events use ScriptableObject `GameEventSO<T>` channels
- Systems subscribe to relevant event channels; never reference each other directly
- Example: `EnemyDeath` → raises `OnEntityKilled(string persistentID)` event → WorldStateManager listens and registers the kill

### Decision 2: World State Persistence

**Choice:** Per-entity PersistentID + central WorldStateManager

**Rationale:** Each world entity (enemy, NPC, container, door) carries a `PersistentID` component with a stable GUID assigned in the Unity editor. `WorldStateManager` (singleton, always loaded in Core scene) maintains:
- `HashSet<string> killedEntities` — dead enemies/NPCs
- `Dictionary<string, QuestState> questStates` — quest progress/outcomes
- `HashSet<string> discoveredLocations` — explored points of interest
- `int currentAct` — Story Act progression

On scene load, entities query their GUID against WorldStateManager and self-deactivate if dead/looted. This supports the permanent world requirement with O(1) lookup and trivial JSON serialization.

### Decision 3: Save System

**Choice:** JSON + Steam Cloud sync via Steamworks.NET

**Rationale:** WorldStateManager serializes to `savegame.json` at `Application.persistentDataPath`. Steam Cloud (Steamworks.NET) syncs this file automatically. Save triggers: manual save (menu), autosave on region transition, autosave on quest completion. No backend required. Debug-friendly (human-readable JSON). Compatible with the established Steam integration plan (Epic 8).

**Save file contains:**
- Full WorldStateManager state (killed entities, quest states, act, locations)
- Player stats, learning points, inventory
- Player position and last loaded region scene

### Decision 4: Scene Loading Strategy

**Choice:** Additive scene loading

**Rationale:** One persistent `Core` scene always loaded containing: player prefab, WorldStateManager, GameEventBus, Cinemachine camera rig, UI canvas, day/night controller, audio manager. Region content (starting town, wilderness, dungeon) lives in separate scenes loaded additively via `LoadSceneAsync(additive)`. Region transitions use a brief camera fade — no full loading screen. Satisfies the "seamless within regions" performance requirement while keeping each region's content organized in its own Unity scene.

### Decision 5: Timed Combo Attack System

**Choice:** Animation-window state machine with per-hit stamina gating

**Rationale:** Attacks chain via a 3-hit combo triggered by LMB. `PlayerCombat`
tracks the current combo step (0–2). On LMB press, if a combo window is open and
stamina is sufficient, the next hit fires and the animator advances to the
corresponding state. If the window has expired or stamina is zero, the combo resets.

Combo window timing is driven by Animator state events (or a simple float timer
set when each attack state is entered), exposing a designer-tunable `comboWindowDuration`
in `CombatConfigSO`. This keeps timing adjustable without code changes — the developer
tunes windows directly in the config asset during playtesting.

Attack states: `Attack_1_State`, `Attack_2_State`, `Attack_3_State` (finisher).
Each returns to Locomotion on exit time if no follow-up is registered.

### Decision 6: Dialogue & Quest Data Storage

**Choice:** ScriptableObjects for structure + WorldStateManager for runtime state

**Rationale:** Static authored data (quest definitions, dialogue topics, NPC configurations) stored as ScriptableObject assets — type-safe, inspector-editable, no parsing overhead. Runtime mutable state (quest active/complete/failed, which dialogue topics are unlocked, NPC alive status) tracked exclusively in WorldStateManager and serialized to the save file.

**ScriptableObject types:**
- `QuestSO` — ID, title, description, NPC dependencies, reward definitions, outcome branches
- `DialogueTopicSO` — topic text, unlock conditions (references quest state IDs), response text, consequences
- `TrainerSO` — available skills, LP cost, gold cost per skill

### Decision 7: Asset Loading Strategy

**Choice:** Direct serialized references (Addressables-ready structure)

**Rationale:** For prototype scope (1 region, 1 dungeon, ~30 items), direct inspector-assigned references on MonoBehaviours and ScriptableObjects are appropriate. `Resources.Load()` explicitly avoided (deprecated in Unity 6). Project folder structure organized by content group from day one to enable future Addressables migration without restructuring.

Addressables to be evaluated at Epic 8 if build size exceeds 5GB target or memory pressure identified on GTX 1060-class hardware.

---

## Cross-cutting Concerns

These patterns apply to ALL systems and must be followed by every implementation.

### Error Handling

**Strategy:** Hybrid — null-guards in MonoBehaviours, try-catch on I/O, global fallback

**Rules (mandatory for all systems):**
- Null-check all serialized references in `Awake()` — log error and `return` if missing
- Never use try-catch inside `Update()` or other per-frame methods
- Wrap all file I/O (save/load, Steam API) in try-catch
- Global `Application.logMessageReceived` handler writes crash logs to `Application.persistentDataPath/crash_log.txt` in release builds
- Only one player-visible error: save file corruption → dialog offering save reset

**Example:**
```csharp
// Null-guard pattern (MonoBehaviour standard)
private void Awake()
{
    if (_combatConfig == null)
    {
        GameLog.Error(TAG, "CombatConfig SO not assigned — component disabled");
        enabled = false;
        return;
    }
}

// I/O try-catch pattern
public bool SaveGame(SaveData data)
{
    try
    {
        string json = JsonUtility.ToJson(data);
        File.WriteAllText(_savePath, json);
        return true;
    }
    catch (Exception e)
    {
        GameLog.Error(TAG, $"Save failed: {e.Message}");
        return false;
    }
}
```

### Logging

**Implementation:** Static `GameLog` wrapper class with system tags

**Log levels:**
- `GameLog.Info(tag, msg)` — normal operation milestones (Editor/Development only)
- `GameLog.Warn(tag, msg)` — unexpected but handled (Editor/Development only)
- `GameLog.Error(tag, msg)` — failures (all builds; writes to `game_log.txt` in release)

**System tags (each system defines its own constant):**

| System | Tag |
|---|---|
| Combat | `[Combat]` |
| WorldState | `[WorldState]` |
| Quest | `[Quest]` |
| Save | `[Save]` |
| Dialogue | `[Dialogue]` |
| NPC | `[NPC]` |
| Scene | `[Scene]` |
| Stealth | `[Stealth]` |
| Crafting | `[Crafting]` |
| Audio | `[Audio]` |

**Example:**
```csharp
private const string TAG = "[Combat]";

GameLog.Info(TAG, $"Attack direction resolved: {direction}");
GameLog.Warn(TAG, $"No StaminaComponent on entity {id}");
GameLog.Error(TAG, $"Hit detection null collider — frame {Time.frameCount}");
```

### Configuration Management

**Balancing values:** Per-system ScriptableObject config assets (inspector-editable, no recompile to tweak)
**Player settings:** PlayerPrefs with a thin `PlayerSettings` wrapper class
**True constants:** Static `GameConstants` class with `const` values

**Config SO assets (one per system):**
- `CombatConfigSO` — stamina costs, direction threshold, sample frames, i-frame duration
- `ProgressionConfigSO` — XP thresholds per level, LP per level, stat effect multipliers
- `StealthConfigSO` — detection cone angle, crouch detection radius reduction
- `WorldConfigSO` — day/night cycle duration, act transition triggers
- `AIConfigSO` — patrol speed, engagement range, attack intervals

**Example:**
```csharp
[CreateAssetMenu(menuName = "Config/Combat")]
public class CombatConfigSO : ScriptableObject
{
    public float baseStaminaPool = 100f;
    public float attackStaminaCost = 20f;
    public float blockStaminaCostPerHit = 15f;
    public float dodgeStaminaCost = 25f;
    public float attackDirectionThreshold = 0.3f;
    public int directionSampleFrames = 5;
}
```

### Event System

**Pattern:** Specific typed ScriptableObject event channels, synchronous dispatch

**Rules:**
- Each cross-system event is its own SO asset with a strongly-typed payload
- Systems raise: `eventChannel.Raise(payload)`
- Systems subscribe in `OnEnable()`, unsubscribe in `OnDisable()`
- No string-keyed events anywhere — typos must be caught at compile time
- All event SO assets live in `Assets/_Game/Events/` organized by domain

**Core event channels:**

| Event SO | Payload | Raised By | Heard By |
|---|---|---|---|
| `OnEntityKilled` | `string persistentID` | CombatSystem | WorldStateManager, QuestSystem |
| `OnNPCDied` | `string persistentID, string npcID` | NPCHealth | WorldStateManager, QuestSystem, DialogueSystem |
| `OnQuestStateChanged` | `string questID, QuestState state` | QuestSystem | DialogueSystem, UI, WorldStateManager |
| `OnActAdvanced` | `int newActNumber` | WorldStateManager | SceneLoader, NPCSpawner, EnemySpawner, UI |
| `OnPlayerDied` | — | PlayerHealth | SaveSystem, UI |
| `OnLevelUp` | `int newLevel` | ProgressionSystem | UI, PlayerStats |
| `OnRegionTransition` | `string targetScene` | TriggerZone | SceneLoader, SaveSystem |
| `OnDayNightChanged` | `bool isDay` | DayNightController | NPCScheduler, AudioManager, LightingManager |

**Example:**
```csharp
// Raising an event
[SerializeField] private GameEventSO<string> _onEntityKilled;
_onEntityKilled.Raise(persistentID);

// Listening to an event
[SerializeField] private GameEventSO<string> _onEntityKilled;
private void OnEnable() => _onEntityKilled.AddListener(HandleEntityKilled);
private void OnDisable() => _onEntityKilled.RemoveListener(HandleEntityKilled);
private void HandleEntityKilled(string id) { /* ... */ }
```

### Debug & Development Tools

**Activation:** `Debug.isDebugBuild` — all tools active in Development Builds only; zero overhead in Release builds.

**Available tools:**

| Tool | Toggle | Build |
|---|---|---|
| Debug overlay (FPS, stamina, stats, attack buffer, act) | F1 | Development only |
| WorldState inspector (killed entities, quest states live view) | F2 | Development only |
| Combat debug overlay (hitboxes, direction buffer, i-frames) | F3 | Development only |
| NPC schedule viewer (state, time-of-day) | F4 | Development only |
| Console command system | F1 + Tab | Development only |
| Quicksave / Quickload | F5 / F9 | All builds |

**Console commands (development):**
```
give_gold <amount>        set_stat <stat> <value>    give_lp <amount>
set_act <number>          complete_quest <questID>   fail_quest <questID>
kill_all_enemies          teleport <x> <y> <z>       toggle_stealth_debug
```

---

## Project Structure

### Organization Pattern

**Pattern:** Hybrid — `_Game/` root separates project content from Unity packages and third-party assets. Type folders at top level, system subfolders within Scripts.

**Rationale:** Keeps project content isolated from Asset Store imports and Unity packages. System-based script organization maps directly to the 8 epics — each epic's code lives in a predictable folder. Asset folders mirror script organization for consistency.

### Directory Structure

```
Assets/
├── _Game/
│   ├── Scripts/
│   │   ├── Core/                   # Always-loaded singletons and managers
│   │   │   ├── WorldStateManager.cs
│   │   │   ├── GameEventBus.cs
│   │   │   ├── SaveSystem.cs
│   │   │   ├── SceneLoader.cs
│   │   │   ├── GameLog.cs
│   │   │   └── GameConstants.cs
│   │   ├── Combat/
│   │   │   ├── PlayerCombat.cs
│   │   │   ├── DirectionalAttackSampler.cs
│   │   │   ├── StaminaSystem.cs
│   │   │   ├── HitDetection.cs
│   │   │   ├── PerfectBlockHandler.cs
│   │   │   └── DodgeController.cs
│   │   ├── Player/
│   │   │   ├── PlayerController.cs
│   │   │   ├── PlayerStats.cs
│   │   │   ├── PlayerHealth.cs
│   │   │   └── CameraController.cs
│   │   ├── AI/
│   │   │   ├── EnemyBrain.cs
│   │   │   ├── EnemyHealth.cs
│   │   │   ├── PatrolBehaviour.cs
│   │   │   ├── CombatBehaviour.cs
│   │   │   └── NPCScheduler.cs
│   │   ├── World/
│   │   │   ├── PersistentID.cs
│   │   │   ├── DayNightController.cs
│   │   │   ├── RegionTransitionTrigger.cs
│   │   │   ├── InteractableObject.cs
│   │   │   └── LootContainer.cs
│   │   ├── Progression/
│   │   │   ├── XPSystem.cs
│   │   │   ├── LevelSystem.cs
│   │   │   ├── LearningPointSystem.cs
│   │   │   └── StatSystem.cs
│   │   ├── Inventory/
│   │   │   ├── InventorySystem.cs
│   │   │   ├── EquipmentSystem.cs
│   │   │   └── ItemPickup.cs
│   │   ├── Economy/
│   │   │   ├── GoldSystem.cs
│   │   │   └── ShopSystem.cs
│   │   ├── Quest/
│   │   │   ├── QuestSystem.cs
│   │   │   └── QuestTracker.cs
│   │   ├── Dialogue/
│   │   │   ├── DialogueSystem.cs
│   │   │   ├── TopicUnlockEvaluator.cs
│   │   │   └── NPCInteraction.cs
│   │   ├── Crafting/
│   │   │   ├── CraftingSystem.cs
│   │   │   └── CraftingStation.cs
│   │   ├── Stealth/
│   │   │   ├── StealthSystem.cs
│   │   │   ├── DetectionCone.cs
│   │   │   └── PickpocketHandler.cs
│   │   ├── Audio/
│   │   │   ├── AudioManager.cs
│   │   │   └── MusicStateController.cs
│   │   ├── UI/
│   │   │   ├── HUDController.cs
│   │   │   ├── InventoryUI.cs
│   │   │   ├── QuestLogUI.cs
│   │   │   ├── StatScreenUI.cs
│   │   │   ├── DialogueUI.cs
│   │   │   └── PauseMenuUI.cs
│   │   └── Debug/
│   │       ├── DebugOverlay.cs
│   │       ├── WorldStateInspector.cs
│   │       ├── CombatDebugOverlay.cs
│   │       └── DebugConsole.cs
│   │
│   ├── ScriptableObjects/
│   │   ├── Config/
│   │   │   ├── CombatConfigSO.cs
│   │   │   ├── ProgressionConfigSO.cs
│   │   │   ├── StealthConfigSO.cs
│   │   │   ├── WorldConfigSO.cs
│   │   │   └── AIConfigSO.cs
│   │   ├── Events/
│   │   │   └── GameEventSO.cs
│   │   ├── Items/
│   │   │   ├── ItemSO.cs
│   │   │   ├── WeaponSO.cs
│   │   │   ├── ArmorSO.cs
│   │   │   └── ConsumableSO.cs
│   │   ├── Quest/
│   │   │   ├── QuestSO.cs
│   │   │   └── DialogueTopicSO.cs
│   │   ├── NPC/
│   │   │   ├── NPCDataSO.cs
│   │   │   └── TrainerSO.cs
│   │   └── Skills/
│   │       └── SkillSO.cs
│   │
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── NPCs/
│   │   ├── World/
│   │   ├── UI/
│   │   └── VFX/
│   │
│   ├── Scenes/
│   │   ├── Core.unity
│   │   ├── StartingTown.unity
│   │   ├── Wilderness.unity
│   │   ├── Dungeon.unity
│   │   └── MainMenu.unity
│   │
│   ├── Data/
│   │   ├── Config/
│   │   ├── Events/
│   │   ├── Items/
│   │   ├── Quests/
│   │   ├── NPCs/
│   │   └── Skills/
│   │
│   ├── Art/
│   │   ├── Characters/
│   │   ├── Environment/
│   │   ├── UI/
│   │   └── VFX/
│   │
│   └── Audio/
│       ├── Music/
│       └── SFX/
│
├── ThirdParty/
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

### System Location Mapping

| System | Scripts | SO Classes | Data Assets |
|---|---|---|---|
| Core managers | `Scripts/Core/` | — | — |
| Combat | `Scripts/Combat/` | `ScriptableObjects/Config/CombatConfigSO.cs` | `Data/Config/CombatConfig.asset` |
| Player controller | `Scripts/Player/` | — | — |
| Enemy AI | `Scripts/AI/` | `ScriptableObjects/Config/AIConfigSO.cs` | `Data/Config/AIConfig.asset` |
| World/persistence | `Scripts/World/` | — | — |
| Progression | `Scripts/Progression/` | `ScriptableObjects/Config/ProgressionConfigSO.cs` | `Data/Config/ProgressionConfig.asset` |
| Inventory | `Scripts/Inventory/` | `ScriptableObjects/Items/ItemSO.cs` | `Data/Items/` |
| Economy | `Scripts/Economy/` | — | — |
| Quest | `Scripts/Quest/` | `ScriptableObjects/Quest/QuestSO.cs` | `Data/Quests/` |
| Dialogue | `Scripts/Dialogue/` | `ScriptableObjects/Quest/DialogueTopicSO.cs` | `Data/Quests/` |
| Crafting | `Scripts/Crafting/` | — | — |
| Stealth | `Scripts/Stealth/` | `ScriptableObjects/Config/StealthConfigSO.cs` | `Data/Config/StealthConfig.asset` |
| Audio | `Scripts/Audio/` | — | `Audio/Music/`, `Audio/SFX/` |
| UI | `Scripts/UI/` | — | `Art/UI/` |
| Events | — | `ScriptableObjects/Events/GameEventSO.cs` | `Data/Events/` |
| Debug tools | `Scripts/Debug/` | — | — |
| NPC data | `Scripts/AI/` | `ScriptableObjects/NPC/NPCDataSO.cs` | `Data/NPCs/` |
| Skills | `Scripts/Progression/` | `ScriptableObjects/Skills/SkillSO.cs` | `Data/Skills/` |

### Naming Conventions

#### Files & Scripts

| Type | Convention | Example |
|---|---|---|
| C# scripts | PascalCase | `PlayerCombat.cs`, `WorldStateManager.cs` |
| ScriptableObject classes | PascalCase + SO suffix | `CombatConfigSO.cs`, `QuestSO.cs` |
| SO asset instances | PascalCase\_Description | `Sword_IronBlade.asset`, `Quest_FindHerbalist.asset` |
| Scenes | PascalCase | `Core.unity`, `StartingTown.unity` |
| Prefabs | PascalCase | `Player.prefab`, `Enemy_Skeleton.prefab` |
| Animation clips | camelCase\_action | `player_idle`, `player_attackOverhead`, `enemy_death` |

#### Code Elements

| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `WorldStateManager`, `DirectionalAttackSampler` |
| Public methods | PascalCase | `RegisterKill()`, `AdvanceAct()` |
| Private methods | PascalCase | `EvaluateUnlockCondition()` |
| Public fields / properties | PascalCase | `CurrentAct`, `StaminaPool` |
| Private fields | `_camelCase` | `_killedEntities`, `_combatConfig` |
| Constants | UPPER\_SNAKE\_CASE | `MAX_EQUIPMENT_SLOTS`, `SAVE_FILE_NAME` |
| Event handlers | Handle + EventName | `HandleEntityKilled()`, `HandleQuestStateChanged()` |
| Coroutines | PascalCase + Coroutine suffix | `LoadRegionCoroutine()`, `FadeTransitionCoroutine()` |
| GameLog tags | `[SystemName]` | `[Combat]`, `[WorldState]`, `[Quest]` |

#### Game-specific Assets

| Type | Convention | Example |
|---|---|---|
| Event SO assets | On + EventName | `OnEntityKilled.asset`, `OnActAdvanced.asset` |
| Config SO assets | SystemName + Config | `CombatConfig.asset`, `ProgressionConfig.asset` |
| Prefab variants | Base\_Variant | `Enemy_Skeleton.prefab`, `Enemy_SkeletonArcher.prefab` |
| PersistentID values | Region\_Type\_Name | `StartingTown_NPC_Blacksmith`, `Wilderness_Enemy_Wolf01` |

---

## Architecture Validation

### Validation Summary

| Check | Result | Notes |
|---|---|---|
| Decision Compatibility | ✅ Pass | All 7 decisions compose cleanly; no conflicts found |
| GDD Coverage | ✅ Pass | All 13 systems and 5 technical requirements have architectural support |
| Pattern Completeness | ✅ Pass | 8 patterns covering all coding scenarios; 4 novel + 4 standard |
| Epic Mapping | ✅ Pass | All 8 epics mapped to explicit scripts, SOs, and data assets |
| Document Completeness | ✅ Pass | All sections present; 2 minor issues fixed during validation |

### Issues Resolved

1. **Decision table missing Version column** — Added Version column to Decision Summary table; Steamworks.NET and Unity 6.3 LTS versions noted.
2. **Missing executive summary** — Added 3-sentence executive summary at document top covering engine, architecture pattern, and core design pillar coverage.

### Coverage Report

**Systems Covered:** 13/13
**Patterns Defined:** 8 (4 novel, 4 standard)
**Decisions Made:** 7
**Epics Mapped:** 8/8
**Technical Requirements Covered:** 5/5

### Overall Status: ✅ PASS — Ready for Implementation

### Validation Date

2026-03-01

---

## Development Environment

### Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| Unity Hub | Latest | Engine version manager |
| Unity | 6.3 LTS | Game engine |
| Visual Studio 2022 or Rider | Latest | C# IDE with Unity integration |
| Git | Latest | Version control |
| Steamworks SDK / Steamworks.NET | Latest stable | Steam integration (Epic 8) |

### Project Setup

**Step 1 — Create Unity project:**
```
Unity Hub → New Project → 3D (URP) template → Name: "Echoes-of-the-Fallen" → Unity 6.3 LTS
```

**Step 2 — Import Unity Starter Assets (ThirdPerson):**
```
Unity Package Manager → Asset Store → "Starter Assets - ThirdPerson" → Import
```

**Step 3 — Create folder structure:**
```
Assets/
├── _Game/          (create manually)
├── ThirdParty/     (create manually)
└── Tests/          (create manually)
```
Then create all subfolders per the Directory Structure section.

**Step 4 — Configure URP for performance:**
```
Project Settings → Graphics → URP Asset
→ Rendering: Forward+
→ Shadows: 2 cascades, max distance 50m
→ Post Processing: minimal (Bloom off by default, tune per scene)
```

**Step 5 — Configure Input System:**
```
Project Settings → Player → Active Input Handling → Input System Package (New)
```

**Step 6 — Configure Git:**
```bash
# Add Unity .gitignore (https://github.com/github/gitignore/blob/main/Unity.gitignore)
# Ensure Library/, Temp/, Logs/ are ignored
```

### First Implementation Steps

1. **Verify movement** — Import Starter Assets ThirdPerson, confirm player moves and camera follows in a test scene
2. **Create Core scene** — Set up `Core.unity` with WorldStateManager, GameEventBus, DayNightController, AudioManager, UI canvas as empty GameObjects
3. **Implement GameLog** — First script to write; all subsequent scripts depend on it
4. **Implement PersistentID** — Second script; validates the permanent entity pattern before any content is built
5. **Build a test scene** with one enemy, one PersistentID, and verify kill → deactivate on reload cycle works before Epic 2 begins

### Architectural Boundaries

- `Scripts/Core/` may be referenced by any system — foundation layer only
- `Scripts/[System]/` scripts must **never** directly reference another system's scripts — use Events SO channels instead
- `Scripts/UI/` may read from any system but must **never** write game state directly — UI raises events or calls public system methods only
- `Scripts/Debug/` is fully isolated — no game system may depend on debug code
- All cross-system data flows through `Data/Events/` SO channels or `WorldStateManager`

---

## Implementation Patterns

These patterns ensure consistent implementation across all AI agents.

### Novel Pattern 1: Directional Attack Pattern

**Purpose:** Resolve mouse movement into one of four attack directions at the moment of input — producing weighty, intentional directional strikes.

**Components:**
- `DirectionalAttackSampler` — maintains rolling delta buffer, resolves direction on demand
- `PlayerCombat` — owns the sampler, triggers attacks, drives Animator
- `CombatConfigSO` — `attackDirectionThreshold`, `directionSampleFrames`

**Data Flow:**
```
Every Update() → DirectionalAttackSampler.RecordDelta(mouseDelta)
LMB pressed    → DirectionalAttackSampler.Resolve() → AttackDirection enum
               → PlayerCombat.ExecuteAttack(direction)
               → Animator.SetTrigger("Attack_" + direction)
               → StaminaSystem.Consume(attackCost)
```

**Resolution logic:**
```
buffer average magnitude < threshold  → Overhead (default/neutral)
|X| > |Y| and X < 0                  → Left
|X| > |Y| and X > 0                  → Right
|Y| > |X| and Y > 0                  → Overhead
|Y| > |X| and Y < 0                  → Thrust
```

**Example:**
```csharp
public enum AttackDirection { Overhead, Left, Right, Thrust }

public class DirectionalAttackSampler : MonoBehaviour
{
    private const string TAG = "[Combat]";
    [SerializeField] private CombatConfigSO _config;

    private Queue<Vector2> _deltaBuffer = new Queue<Vector2>();

    public void RecordDelta(Vector2 mouseDelta)
    {
        _deltaBuffer.Enqueue(mouseDelta);
        if (_deltaBuffer.Count > _config.directionSampleFrames)
            _deltaBuffer.Dequeue();
    }

    public AttackDirection Resolve()
    {
        Vector2 avg = Vector2.zero;
        foreach (var d in _deltaBuffer) avg += d;
        avg /= _deltaBuffer.Count;

        if (avg.magnitude < _config.attackDirectionThreshold)
        {
            GameLog.Info(TAG, "Direction defaulted to Overhead (below threshold)");
            return AttackDirection.Overhead;
        }

        if (Mathf.Abs(avg.x) > Mathf.Abs(avg.y))
            return avg.x < 0 ? AttackDirection.Left : AttackDirection.Right;

        return avg.y > 0 ? AttackDirection.Overhead : AttackDirection.Thrust;
    }
}
```

---

### Novel Pattern 2: Permanent Entity Pattern

**Purpose:** Every world entity self-manages its permanent death state — checking its GUID on spawn and deactivating silently if already killed in WorldStateManager.

**Components:**
- `PersistentID` — holds GUID, checks WorldStateManager on Awake, raises kill event on death
- `WorldStateManager` — registers kills, exposes `IsKilled(string id)`
- `OnEntityKilled` event channel SO

**Data Flow:**
```
Scene loads additively
  → Each entity's PersistentID.Awake()
  → WorldStateManager.IsKilled(guid) ?
      true  → gameObject.SetActive(false)   [silent, no events]
      false → entity lives normally

Entity killed in combat
  → EnemyHealth.Die()
  → PersistentID.RegisterDeath()
  → _onEntityKilled.Raise(guid)
  → WorldStateManager.HandleEntityKilled(guid) → killedEntities.Add(guid)
```

**Example:**
```csharp
public class PersistentID : MonoBehaviour
{
    private const string TAG = "[WorldState]";
    [SerializeField] private string _guid;
    [SerializeField] private GameEventSO<string> _onEntityKilled;

    private void Awake()
    {
        if (string.IsNullOrEmpty(_guid))
        {
            GameLog.Error(TAG, $"PersistentID on {gameObject.name} has no GUID assigned");
            return;
        }
        if (WorldStateManager.Instance.IsKilled(_guid))
            gameObject.SetActive(false);
    }

    public void RegisterDeath() => _onEntityKilled.Raise(_guid);

    public void GenerateGUID() => _guid = System.Guid.NewGuid().ToString();
}
```

**Editor rule:** Every enemy, NPC, and container in every scene **must** have a `PersistentID` with a unique GUID. A custom editor validator checks for missing or duplicate GUIDs on scene save.

---

### Novel Pattern 3: NPC Schedule Pattern

**Purpose:** NPCs follow day/night routines that gate gameplay access — a sleeping trainer cannot teach, a tavern merchant at night won't sell.

**Components:**
- `DayNightController` — raises `OnDayNightChanged(bool isDay)`
- `NPCScheduler` — subscribes to day/night; transitions NPC between schedule states
- `NPCDataSO` — defines day/night states and waypoints per NPC
- `NPCInteraction` — checks `NPCScheduler.CurrentState` before allowing interaction

**NPC States:** `Working` | `Sleeping` | `Patrolling` | `AtTavern`

**Data Flow:**
```
DayNightController → time threshold crossed
  → _onDayNightChanged.Raise(isDay)
  → NPCScheduler.HandleDayNightChanged(isDay)
  → evaluates NPCDataSO.dayState / nightState
  → transitions NPCState, moves NPC to schedule waypoint

Player presses E near NPC
  → NPCInteraction.TryInteract()
  → Working     → open full dialogue/trade menu
  → Sleeping    → show "Come back in the morning" text
  → Patrolling / AtTavern → limited dialogue only (no trade/training)
```

**Example:**
```csharp
public enum NPCState { Working, Sleeping, Patrolling, AtTavern }

public class NPCScheduler : MonoBehaviour
{
    private const string TAG = "[NPC]";
    [SerializeField] private NPCDataSO _data;
    [SerializeField] private GameEventSO<bool> _onDayNightChanged;

    public NPCState CurrentState { get; private set; }

    private void OnEnable() => _onDayNightChanged.AddListener(HandleDayNightChanged);
    private void OnDisable() => _onDayNightChanged.RemoveListener(HandleDayNightChanged);

    private void HandleDayNightChanged(bool isDay)
    {
        CurrentState = isDay ? _data.dayState : _data.nightState;
        GameLog.Info(TAG, $"{_data.npcName} schedule → {CurrentState}");
        // NavMesh agent moves to schedule waypoint
    }
}
```

---

### Novel Pattern 4: Topic-Unlock Dialogue Pattern

**Purpose:** Gothic-style conversation where topic availability is driven entirely by world/quest state — topics appear and disappear based on what the player has done.

**Components:**
- `DialogueTopicSO` — topic text, unlock conditions, response text, consequences
- `TopicUnlockEvaluator` — evaluates each SO's conditions against WorldStateManager
- `DialogueSystem` — orchestrates conversation, presents available topics to UI
- `DialogueUI` — displays topic list, receives selection, returns to DialogueSystem

**Unlock Condition Types:**
```
QuestActive(questID)      → quest must be active
QuestCompleted(questID)   → quest must be completed
NPCAlive(persistentID)    → specific NPC must be alive
ActReached(actNumber)     → Story Act must be >= value
HasItem(itemID)           → player has item in inventory
Always                    → always visible
```

**Data Flow:**
```
Player presses E near NPC
  → DialogueSystem.OpenDialogue(npcDataSO)
  → TopicUnlockEvaluator.GetAvailableTopics(npcDataSO.allTopics)
      → evaluate each topic's conditions vs WorldStateManager
      → return filtered available list
  → DialogueUI.ShowTopicList(availableTopics)
  → Player selects topic
  → DialogueSystem.ExecuteTopic(topicSO)
      → show response text
      → apply consequences (start quest, give item, update state flag)
      → re-evaluate topics (may unlock/lock after consequence)
  → show updated topic list OR close if none remain
```

**Example:**
```csharp
public class TopicUnlockEvaluator : MonoBehaviour
{
    private const string TAG = "[Dialogue]";

    public List<DialogueTopicSO> GetAvailableTopics(List<DialogueTopicSO> allTopics)
    {
        var available = new List<DialogueTopicSO>();
        foreach (var topic in allTopics)
        {
            if (AllConditionsMet(topic.unlockConditions))
                available.Add(topic);
        }
        GameLog.Info(TAG, $"{available.Count}/{allTopics.Count} topics available");
        return available;
    }

    private bool AllConditionsMet(List<UnlockCondition> conditions)
    {
        foreach (var condition in conditions)
            if (!condition.Evaluate(WorldStateManager.Instance)) return false;
        return true;
    }
}
```

---

### Standard Patterns

#### Component Communication

**Pattern:** Event Bus (SO channels) — never direct cross-system references.
All cross-system communication flows through typed `GameEventSO<T>` channels.
Same-system communication uses direct MonoBehaviour references (e.g. `PlayerCombat` → `StaminaSystem` directly — both are in `Scripts/Combat/`).

#### Entity Creation

**Pattern:** Hand-placed prefab instances with editor-assigned PersistentIDs.
World entities (enemies, NPCs, containers) are placed directly in region scenes — not spawned at runtime. Only dynamic `Instantiate()` use: loot drop prefabs, VFX, projectiles.

#### State Transitions

**Pattern:** Enum-driven state machine with explicit `switch` in MonoBehaviours.
```csharp
private enum EnemyState { Idle, Patrolling, Engaging, Attacking, Dead }
private EnemyState _state = EnemyState.Patrolling;

private void Update()
{
    switch (_state)
    {
        case EnemyState.Patrolling: HandlePatrol(); break;
        case EnemyState.Engaging:   HandleEngage(); break;
        case EnemyState.Attacking:  HandleAttack(); break;
    }
}
```

#### Data Access

**Pattern:** ScriptableObject direct inspector references — no global data managers, no `Resources.Load()`, no singletons for data. Each MonoBehaviour holds serialized SO references assigned in the inspector. Only `WorldStateManager` and `SaveSystem` are singletons (Core scene only).

---

### Consistency Rules

| Pattern | Rule | Enforcement |
|---|---|---|
| Cross-system communication | Always via typed SO event channels | Code review — no direct cross-system `GetComponent` |
| Null checks | All `[SerializeField]` refs checked in `Awake()` | All MonoBehaviours follow null-guard template |
| Event subscription | `OnEnable` subscribe, `OnDisable` unsubscribe | Never subscribe in `Start()` or `Awake()` |
| Logging | Always use `GameLog` with system TAG constant | Never use `Debug.Log` directly |
| State machines | Enum + switch pattern | No external state machine libraries |
| PersistentID | Every world entity has GUID assigned in editor | Editor validator tool on scene save |
| Config values | All tunable values in config SO — never hardcoded | No magic numbers in game logic scripts |
