---
project_name: 'Unity-BMAD-test-rpg'
user_name: 'Valentin'
date: '2026-03-01'
sections_completed: ['technology_stack', 'engine_specific_rules', 'performance_rules', 'code_organization_rules', 'testing_rules', 'platform_build_rules', 'critical_rules']
status: 'complete'
rule_count: 52
optimized_for_llm: true
architecture_version: '1.0'
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing game code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- **Engine:** Unity 6000.3.10f1 (Unity 6.3 LTS)
- **Render Pipeline:** Universal Render Pipeline (URP 17.x) — PC_RPAsset / PC_Renderer
- **Input:** Unity Input System (new input system only; legacy input disabled)
- **Steam Integration:** Steamworks.NET (Epic 8) — save sync + achievements
- **Color Space:** Linear
- **Target Platform:** PC (Windows x64) via Steam

> Note: Mobile render assets exist from URP template defaults — do NOT create mobile-specific code or platform guards unless explicitly asked.

## Critical Implementation Rules

### Engine-Specific Rules

**Unity Lifecycle:**
- Use `Awake` for self-initialization (cache own references), `Start` for cross-component initialization
- Never call `GetComponent` in `Update` — cache references in `Awake`
- Prefer `OnDestroy` for cleanup of event subscriptions and allocated resources
- `[SerializeField] private` is preferred over `public` for Inspector-exposed fields

**URP Specifics:**
- Do NOT use built-in pipeline shaders — use URP Lit/Unlit shaders only
- Post-processing uses URP Volume system (Volume profiles), not legacy post-processing stack
- Renderer Features must be added to `PC_Renderer` asset, not created ad hoc

**Input System:**
- Use the generated `InputSystem_Actions` C# class — do NOT use `Input.GetKey()` / `Input.GetAxis()`
- Subscribe/unsubscribe input callbacks in `OnEnable` / `OnDisable`

**Architecture Patterns (committed):**
- **Folder root:** All custom project files live under `Assets/_Game/` (not `_Project/`)
- **Cross-system communication:** Typed `GameEventSO<T>` ScriptableObject channels ONLY — never C# `event Action` across system boundaries; never call other system scripts directly
- **Event subscription:** Always subscribe in `OnEnable`, unsubscribe in `OnDisable` — never in `Start` or `Awake`
- **Event SO naming:** `On + EventName` asset names (e.g. `OnEntityKilled.asset`, `OnActAdvanced.asset`) in `Assets/_Game/Data/Events/`
- **Same-system comms:** Direct MonoBehaviour references acceptable within the same `Scripts/[System]/` folder
- **Data:** ScriptableObjects for all authored static data (items, quests, dialogue, skills, config) — never static classes for game data
- **Config SOs:** All tunable gameplay values live in per-system config SOs (e.g. `CombatConfigSO`) — NO magic numbers in game logic scripts
- **Singletons:** Only `WorldStateManager` and `SaveSystem` are singletons — both live in `Core.unity` (always-loaded scene); all others use direct SO references
- **WorldStateManager:** Single source of runtime world truth — never cache world state (killed entities, quest states, act number) in MonoBehaviours; always query `WorldStateManager.Instance`
- **PersistentID:** Every world entity (enemy, NPC, loot container) MUST have a `PersistentID` component with a GUID assigned in the Unity editor. `Awake()` checks `WorldStateManager.IsKilled(guid)` and calls `gameObject.SetActive(false)` if true — silent, no events
- **State machines:** Enum-driven `switch` pattern for all entity states (enemy AI, NPC schedule) — no external state machine libraries
- **Character movement:** `CharacterController` for player — no Rigidbody on player
- **Object pooling:** Pool all frequently spawned objects (hit effects, VFX, floating text)

**Logging — MANDATORY:**
- NEVER use `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` directly
- ALWAYS use `GameLog` wrapper: `GameLog.Info(TAG, msg)` / `GameLog.Warn(TAG, msg)` / `GameLog.Error(TAG, msg)`
- Every class defines its own tag constant: `private const string TAG = "[SystemName]";`
- `Info` and `Warn` are stripped in Release builds; `Error` writes to `game_log.txt`

**Coroutine/Async Patterns:**
- Prefer coroutines for time-based gameplay logic
- Use `async/await` only for I/O (file save/load, Steam API) — `Task` does not integrate with Unity's frame loop
- Cache `WaitForSeconds` instances — never `new WaitForSeconds()` per coroutine call
- Coroutines silently stop when MonoBehaviour is disabled — check `gameObject.activeInHierarchy` before starting

### Performance Rules

**Frame Budget:**
- Target: 60 FPS on mid-range PC (GTX 1060 / RX 580 class) — ≈16ms frame budget
- No allocations (heap) in `Update` hot paths — avoid `new`, LINQ, string concatenation per frame
- Use `StringBuilder` for any runtime string building (damage numbers, UI labels)

**Memory & Pooling:**
- Pool all frequently spawned objects (hit effects, VFX, floating damage text)
- Prefer `TryGetComponent` over `GetComponent` when result may be null — avoids exception overhead
- Unload unused assets between major scene loads with `Resources.UnloadUnusedAssets()`

**Asset Loading:**
- `Resources.Load()` is BANNED — deprecated in Unity 6; use direct serialized Inspector references
- All asset references assigned in Inspector on prefabs/SOs — no runtime path-based loading
- Addressables deferred to Epic 8 only if build exceeds 5GB or memory pressure identified

**Physics:**
- Use `Physics.SphereCastNonAlloc` / `OverlapSphereNonAlloc` (non-allocating variants) for runtime queries
- Keep Rigidbody usage minimal — `CharacterController` for player, trigger colliders for detection zones
- Set collision layers explicitly — never rely on default layer collision matrix

**Unity-Specific Hot Path Rules:**
- Cache `Camera.main` in `Awake` — it calls `FindWithTag` internally on every access
- Avoid `SendMessage` / `BroadcastMessage` — use direct references or `GameEventSO<T>` channels
- Cache `WaitForSeconds` instances used in coroutines — never `new WaitForSeconds()` per call
- `GameLog.Info` / `GameLog.Warn` calls are stripped in Release builds — safe in Update if needed for debug

### Code Organization Rules

**Folder Structure (Assets/):**
- All custom project files live under `Assets/_Game/` (underscore keeps it top of list)
- Do NOT use `Assets/_Project/` — the committed root is `_Game/`
- `_Game/Scripts/` subfolders: `Core/`, `Combat/`, `Player/`, `AI/`, `World/`, `Progression/`, `Inventory/`, `Economy/`, `Quest/`, `Dialogue/`, `Crafting/`, `Stealth/`, `Audio/`, `UI/`, `Debug/`
- `_Game/ScriptableObjects/` — SO class definitions: `Config/`, `Events/`, `Items/`, `Quest/`, `NPC/`, `Skills/`
- `_Game/Data/` — SO asset instances: `Config/`, `Events/`, `Items/`, `Quests/`, `NPCs/`, `Skills/`
- `_Game/Prefabs/` — organized by system: `Player/`, `Enemies/`, `NPCs/`, `World/`, `UI/`, `VFX/`
- `_Game/Scenes/` — `Core.unity` (persistent), `StartingTown.unity`, `Wilderness.unity`, `Dungeon.unity`, `MainMenu.unity`
- `_Game/Art/`, `_Game/Audio/` — all media assets
- `ThirdParty/` — Asset Store imports, Steamworks.NET
- `Tests/EditMode/`, `Tests/PlayMode/` — Unity Test Framework
- Do NOT move or rename: `Assets/Settings/` (URP config lives here)

**Scene Architecture:**
- `Core.unity` is ALWAYS loaded — contains: `WorldStateManager`, `SaveSystem`, `GameEventBus`, Cinemachine rig, UI canvas, `DayNightController`, `AudioManager`
- Region scenes (`StartingTown`, `Wilderness`, `Dungeon`) loaded additively via `LoadSceneAsync(additive)` — never standalone
- Never put global managers in region scenes — they belong in `Core.unity` only

**Naming Conventions:**
- Classes: `PascalCase` (e.g. `PlayerCombat`, `WorldStateManager`)
- ScriptableObject types: `PascalCase` + SO suffix (e.g. `QuestSO`, `CombatConfigSO`)
- Private fields: `_camelCase` with underscore prefix (e.g. `_health`, `_combatConfig`)
- Public methods: `PascalCase` (e.g. `RegisterKill()`, `AdvanceAct()`)
- Constants: `UPPER_SNAKE_CASE` (e.g. `MAX_EQUIPMENT_SLOTS`, `SAVE_FILE_NAME`)
- Event handler methods: `Handle` + EventName (e.g. `HandleEntityKilled()`, `HandleQuestStateChanged()`)
- Coroutines: `PascalCase` + `Coroutine` suffix (e.g. `LoadRegionCoroutine()`)
- GameLog TAG constants: `[SystemName]` in brackets (e.g. `[Combat]`, `[WorldState]`)
- SO asset instances: `PascalCase_Description` (e.g. `Sword_IronBlade.asset`, `Quest_FindHerbalist.asset`)
- Event SO assets: `On` + EventName (e.g. `OnEntityKilled.asset`, `OnActAdvanced.asset`)
- Config SO assets: `SystemName` + `Config` (e.g. `CombatConfig.asset`, `ProgressionConfig.asset`)
- Prefabs: `PascalCase` or `Category_Name` (e.g. `Player.prefab`, `Enemy_Skeleton.prefab`)
- PersistentID GUID values: `Region_Type_Name` (e.g. `StartingTown_NPC_Blacksmith`, `Wilderness_Enemy_Wolf01`)
- Animation clips: `camelCase_action` (e.g. `player_idle`, `player_attackOverhead`, `enemy_death`)
- Scenes: `PascalCase` (e.g. `Core.unity`, `StartingTown.unity`)

### Testing Rules

**Philosophy:**
- Prototype focus — Edit Mode tests for pure logic only; Play Mode tests only for complex critical systems
- Test game logic (formulas, data), not Unity lifecycle or rendering

**Test Organization:**
- `Tests/EditMode/` — pure C# logic (no MonoBehaviour, no scene required)
- `Tests/PlayMode/` — runtime integration tests (use sparingly)
- Test class naming: `[SystemName]Tests` (e.g. `CombatCalculatorTests`, `WorldStateManagerTests`)
- Requires `Tests.asmdef` assembly definition referencing `UnityEngine.TestRunner`

**Test targets (Edit Mode):**
- Damage formulas, stat calculations, XP/level-up curves
- Inventory operations (add, remove, equip)
- `WorldStateManager` state transitions (kill registration, quest state changes, act advancement)
- `TopicUnlockEvaluator` condition evaluation logic (quest states, NPC alive checks)
- `DirectionalAttackSampler` direction resolution (buffer averaging, threshold logic)
- ScriptableObject data validation (quest SO completeness, item SO stat ranges)

**Do NOT test:**
- MonoBehaviour lifecycle, UI layout, input handling, or anything requiring a running scene unless critical
- `PersistentID` Awake behavior — Play Mode only if needed
- `GameLog` output — it's a utility wrapper, not business logic

### Platform & Build Rules

**Target Platform:** PC Windows x64 — Steam distribution
- Scripting backend: IL2CPP for Release builds, Mono for Editor/dev iteration
- API Compatibility: .NET Standard 2.1
- No console, mobile, or WebGL targets — do NOT add platform guards for these

**Input:**
- All input routed through `InputSystem_Actions` — no platform-specific input guards needed
- Gamepad support not planned — keyboard & mouse only; do not add controller bindings

**Build Configurations:**
- Use `#if UNITY_EDITOR` guards only for Editor-only utilities (never for gameplay logic)
- Use `Debug.isDebugBuild` for runtime debug tools (overlays, console commands) — NOT `#if UNITY_EDITOR`
- All debug tools (F1–F4 overlays, console commands) active in Development Builds only via `Debug.isDebugBuild`
- F5/F9 quicksave/quickload active in all builds
- Always profile with Development Build + Unity Profiler before claiming a performance fix works

**Scene Management:**
- Use `SceneManager.LoadSceneAsync(additive)` for region scenes — never synchronous `LoadScene`
- `Core.unity` must always be loaded first — it hosts all global managers
- Region transitions use a camera fade, not a loading screen

**Save System:**
- Save file path: `Application.persistentDataPath/savegame.json`
- Crash log path: `Application.persistentDataPath/crash_log.txt`
- Steam Cloud (Steamworks.NET) syncs `savegame.json` automatically — do not implement custom cloud sync
- Save triggers: manual save (pause menu), autosave on region transition, autosave on quest completion

**Steam Integration (Epic 8):**
- Steamworks.NET lives in `ThirdParty/Steamworks.NET/`
- All Steam API calls wrapped in try-catch (I/O rule applies)
- Steam App ID configured in `steam_appid.txt` at project root

### Critical Don't-Miss Rules

**Unity 6 Specific:**
- `FindObjectOfType` is deprecated — use `FindFirstObjectByType` or `FindAnyObjectByType`
- `OnGUI` is deprecated for gameplay UI — use uGUI (Canvas) only
- `Resources.Load()` is deprecated and BANNED — use direct serialized Inspector references
- Prefer coroutines for time-based gameplay; use `async/await` only for I/O operations

**WorldState & Persistence Anti-Patterns:**
- NEVER store world state (killed entities, quest outcomes, act number) in MonoBehaviours — use `WorldStateManager` only
- NEVER skip assigning a GUID to a `PersistentID` component — missing GUIDs cause silent permanent-death failures
- NEVER duplicate GUIDs across entities — run editor validator on scene save
- NEVER raise `OnEntityKilled` without first checking the entity has a `PersistentID` — log error and return if missing
- ScriptableObject values ARE modified at runtime in Editor (not in builds) — reset mutable SO fields on game start

**Event System Anti-Patterns:**
- NEVER reference another system's scripts directly — use `GameEventSO<T>` channels for all cross-system communication
- NEVER subscribe to event channels in `Start()` or `Awake()` — always `OnEnable`/`OnDisable`
- NEVER use string-keyed events — all event channels are typed SO assets; typos must be compile-time errors
- `UnityEvent` wired in Inspector has no code subscribers — use `GameEventSO<T>` channels for code-driven events

**Config & Data Anti-Patterns:**
- NEVER hardcode tunable gameplay values (damage, costs, thresholds, speeds) — all go in a config SO
- NEVER store scene object references inside ScriptableObjects — SOs persist across scenes, scene objects don't
- NEVER use `DontDestroyOnLoad` without a singleton duplicate guard — destroy the new instance if one already exists
- NEVER put mutable game state in MonoBehaviours that can be destroyed — state lives in `WorldStateManager`

**Logging Anti-Patterns:**
- NEVER call `Debug.Log`, `Debug.LogWarning`, or `Debug.LogError` directly — always use `GameLog`
- NEVER omit the TAG constant — every class defines `private const string TAG = "[SystemName]";`

**Novel Pattern Gotchas:**
- `DirectionalAttackSampler` buffer must be cleared/reset between attacks — stale buffer causes wrong direction on fast re-clicks
- `NPCScheduler` must subscribe to `OnDayNightChanged` in `OnEnable` — NPCs placed inactive in scene will miss the event on load; call `HandleDayNightChanged` manually in `Start` to initialize state
- `TopicUnlockEvaluator` re-evaluates AFTER each topic consequence fires — topics that unlock mid-conversation must appear immediately without closing/reopening dialogue

**Common Gotchas:**
- Coroutines silently stop when MonoBehaviour is disabled — check `gameObject.activeInHierarchy` before starting
- `Physics.Raycast` uses world space — always convert screen/local positions with `Camera.ScreenToWorldPoint` or `TransformPoint`
- Avoid deep inheritance chains for characters/enemies — prefer composition (multiple small components)
- `CharacterController.Move()` does not apply gravity automatically — implement gravity manually in the move vector

---

## Usage Guidelines

**For AI Agents:**
- Read this file before implementing any game code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- Update this file if new patterns emerge during development

**For Humans:**
- Keep this file lean and focused on agent needs
- Update when technology stack or architectural decisions change
- Remove rules that become obvious over time

_Last Updated: 2026-03-01_
_Architecture Version: 1.0 (generated from game-architecture.md)_
