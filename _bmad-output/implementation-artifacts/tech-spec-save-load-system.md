---
title: 'Save / Load System'
slug: 'save-load-system'
created: '2026-10-07'
status: 'completed'
stepsCompleted: [1, 2, 3, 4, 5, 6]
tech_stack: ['Unity 6000.6.2f1', 'C#', 'URP 17', 'Unity Input System', 'Newtonsoft Json (com.unity.nuget.newtonsoft-json 3.2.1)', 'NUnit EditMode tests']
files_to_modify: ['Packages/manifest.json', 'Scripts/Core/State/WorldStateManager.cs', 'Scripts/Core/SceneLoader.cs', 'Scripts/Player/PlayerHealth.cs', 'Scripts/Player/PlayerStats.cs', 'Scripts/Combat/StaminaSystem.cs', 'Scripts/Player/Progression/XPSystem.cs', 'Scripts/Player/Progression/LevelSystem.cs', 'Scripts/Player/Progression/LearningPointSystem.cs', 'Scripts/Player/Progression/PlayerSkills.cs', 'Scripts/Inventory/InventorySystem.cs', 'Scripts/Inventory/EquipmentSystem.cs', 'Scripts/Inventory/ActionBarSystem.cs', 'Scripts/Economy/GoldSystem.cs', 'Scripts/World/Lockable.cs', 'Scripts/World/DoorInteractable.cs', 'Scripts/AI/EntityHealth.cs', 'Scripts/World/PersistentID.cs', 'Scripts/Inventory/ItemPickup.cs', 'Scripts/Quest/QuestEventsManager.cs', 'Scripts/Player/PlayerStateManager.cs', 'Scripts/UI/HUD/NotificationToastUI.cs', 'Assets/_Game/InputSystem_Actions.cs + .inputactions', 'Prefabs/Player/Player.prefab', 'Prefabs/Entities/Entity_base.prefab', 'Scenes/StartingTown.unity', 'Scenes/TestScene.unity', 'Scripts/UI/Screens/UIScreenManager.cs', 'ScriptableObjects/Items/ItemSO.cs', 'Prefabs/UI/UICanvas.prefab', 'Scenes/Core.unity', 'NEW Scripts/Core/Save/*', 'NEW Scripts/UI/Screens/GameMenuUI.cs, SaveSlotListUI.cs, SaveSlotEntryUI.cs, ConfirmDialogUI.cs, DeathScreenUI.cs, LoadingOverlayUI.cs', 'NEW Scripts/World/SaveableObject.cs', 'NEW Scripts/Player/PlayerSaveAdapter.cs', 'NEW ScriptableObjects/Items/ItemCatalogSO.cs', 'NEW Editor/Save/*']
code_patterns: ['Core.unity singletons (WorldStateManager, SaveSystem only)', 'GameEventSO channels, subscribe OnEnable / unsubscribe OnDisable', 'GameLog with TAG, never Debug.Log', 'CursorManager for cursor', 'IScreenPanel OnScreenOpen/OnScreenClose', 'InputSystem_Actions create Awake / Dispose OnDestroy / null-guard OnDisable', 'Catalog SO pattern (SkillCatalogSO)', 'Typed facts in WorldStateManager; quest state derived from facts']
test_patterns: ['NUnit EditMode tests in Assets/Tests/EditMode (Tests.EditMode asmdef refs Game, Game.Editor)', 'new GameObject().AddComponent<T>() + reflection to set private [SerializeField] deps', 'ScriptableObject.CreateInstance for SOs, DestroyImmediate in TearDown']
---

# Tech-Spec: Save / Load System

**Created:** 2026-10-07

## Overview

### Problem Statement

The game's core pillar (Consequence) depends on a permanent world state, but nothing is persisted:
`WorldStateManager.GetSaveData()` exists but nothing calls it, and every other piece of runtime
state (player stats/progression/inventory/equipment/gold, container contents, door lock state, NPC
trade inventories, corpse loot, dropped items) lives only in memory. `PlayerHealth` raises
`OnPlayerDied` but nothing listens, so death has no recovery path. The deferred
`tech-spec-lockable-persistence-stub.md` is blocked on this system.

### Solution

A `SaveSystem` singleton in `Core.unity` collects state from a set of saveable participants into one
versioned `SaveData` object, serializes it with Newtonsoft Json to slot files under
`Application.persistentDataPath`, and loads by reloading the region scene additively, then applying
the saved state to world objects and the player. It comes with a Save/Load screen in the existing
tabbed menu, F5/F9 quicksave and quickload, autosave triggers, and a death screen that offers to load.

### Scope

**In Scope:**
- Slots: 1 quicksave (F5 / F9), 1 autosave (rotating/overwritten), N manual slots
- Save file format: versioned JSON (Newtonsoft), one file per slot + lightweight metadata
  (timestamp, region, player level, playtime) for the slot list
- Persisted state:
  - World facts (kills, quest state, dialogue played, world events — `WorldStateManager`)
  - Player: position/rotation, health (and stamina if relevant), XP/level/LP, skills, inventory,
    equipment, action bar, gold
  - Containers (contents) and doors (locked / open) — absorbs `tech-spec-lockable-persistence-stub`
  - NPC trade inventories / gold, loot on corpses, items dropped in the world
- Load model: unload + reload the current region scene, restore saved state, place the player
- New **Esc Game Menu** (Resume / Save Game / Load Game / Options / Quit) with save / load slot-list
  sub-panels (save to slot, load slot, overwrite confirmation, delete slot). Esc closes an open tab
  first; with no tab open it toggles the Game Menu. The `Options` tab leaves the tab bar and is
  reached from the Game Menu instead (`OptionsUI` stays a placeholder)
- Game Menu and death screen pause the world (`Time.timeScale = 0`); character tabs keep not pausing
- Save gate: saving blocked during dialogue / trade / looting / death / an in-progress load; allowed
  in combat
- Killed entities: on load, a killed entity whose saved inventory still has items is restored as a
  lootable corpse (death pose / ragdoll at its saved position); a killed entity with an empty
  inventory stays hidden (current `PersistentID` behaviour)
- Autosave triggers: region transition, quest completion
- Death screen: "You died" overlay → Load last save / Load quicksave / Quit
- Corrupt / incompatible save handling (error dialog, no crash), all file I/O in try/catch
- Stable IDs needed for persistence (e.g. item catalog lookup by ID for `ItemSO`, save keys for
  containers / doors / NPCs) and editor validation for missing / duplicate IDs

**Out of Scope:**
- Steam Cloud sync (Epic 8 — the file layout must stay cloud-friendly, but no Steamworks code)
- Main menu / title screen with New Game / Continue (no `MainMenu` scene yet)
- Multi-region save semantics beyond the current region list (only StartingTown / TestScene exist)
- Save-game migration between format versions (version field written + checked; mismatch = error)
- NPC schedules / AI runtime state (brain state, current target, mid-combat state) — NPCs reset to
  spawn state on load except inventory / alive state
- Save thumbnails / screenshots

## Context for Development

### Codebase Patterns

**Scene / lifecycle**
- `Core.unity` is always loaded and holds the manager GOs (`WorldStateManager`, `SceneLoader`, an empty
  `SaveSystem` stub GO already exists) **and a `Player.prefab` instance**. `UICanvas` is nested inside
  `Player.prefab`, so the player + all UI survive a region reload.
- `SceneLoader` (`Scripts/Core/SceneLoader.cs`) loads `_startupScene` ("StartingTown") additively in
  `Start()`, then teleports the `Player`-tagged GO to the `PlayerSpawnPoint` GO. It has
  `LoadRegion` / `UnloadRegion` (coroutines, no completion callback) — the load flow needs an awaitable /
  callback variant and must skip `SpawnPlayerAtPoint` when restoring a saved position.
- `WorldStateManager` uses `DontDestroyOnLoad` + `Instance` singleton. `project-context.md`: only
  `WorldStateManager` and `SaveSystem` may be singletons; all I/O wrapped in try/catch; `async/await` only
  for I/O.

**World facts (already persistent-ready)**
- `WorldStateManager._worldFacts : Dictionary<string,bool>` stores every stored fact (`Killed.{guid}`,
  `Dialogue.Played.{nodeId}`, `World.{key}`). `QuestFact` / `QuestSO.IsStarted/IsCompleted/IsFailed`
  are *computed* from these facts → quests need no separate save data. `SkillFact` / `StatFact` read
  `PlayerSkills` / `PlayerStats`. `GetSaveData()` + `WorldStateSaveData` stub exist (not wired). Restore
  needs a new `LoadSaveData(...)` that replaces the dictionary **without** raising `_onFactChanged` per
  key (raise nothing, or one bulk refresh) and without calling `RegisterKill` (no XP / reward re-grant).

**Entities / kills / corpses**
- `PersistentID` holds a per-entity-unique `KilledFact` (GUID; validator V5 forbids sharing). In
  `Start()` it calls `gameObject.SetActive(false)` if `IsKilled`. `EntityHealth.Die()` sets `IsDead`,
  stops the `NavMeshAgent`, calls `RegisterDeath()`, and `_animationDriver.TriggerDeath()`; the ragdoll is
  enabled later by `SMB_DeathState` → `AIAnimationDriver.EnableRagdoll()`. Body stays active.
- Every entity has exactly one `InventorySystem` on its root (`Entity_base`); NPCs (`NPC_base Variant`)
  also have a `GoldSystem` (`_startingGold: 500`). Corpse looting goes through `NPCPresence` /
  `EntityPresence` + `_onLootRequested`.
- `KilledFact` GUID is a ready-made stable key for entities; containers and doors have **no** stable ID →
  need a new `SaveableID` component (editor-assigned GUID string).

**Player state holders** (all on the `Player.prefab` root / children, all set from config in `Awake`)
- `PlayerStats` — private `_baseStrength/_baseDexterity/_baseEndurance/_baseIntelligence`; equipment
  bonuses are derived (`ApplyEquipmentBonuses` from `EquipmentSystem`) → save base values only.
- `PlayerHealth` — `CurrentHealth`, `IsDead`; `Die()` raises `_onPlayerDied` and
  **`gameObject.SetActive(false)` on the Player root — this also disables the nested `UICanvas`**, so the
  death screen cannot be shown. Must change to "keep active, block control".
- `StaminaSystem` — `_currentStamina`; not worth saving (refill to max on load).
- `XPSystem` — `CurrentXP`, `TotalKills` (private setters). `LevelSystem` — `CurrentLevel` (derived from XP
  via thresholds, but saved explicitly to avoid re-raising `_onLevelUp` → LP re-grant).
  `LearningPointSystem` — `CurrentLP`. `PlayerSkills` — `HashSet<string> _learnedSkills` (skill ids).
  Restore must set values directly and raise the *changed* events (`_onLPChanged`, XP progress) but
  **never** `_onLevelUp` / `_onSkillLearned` / `_onXPGained` (they grant LP / toasts / kill counts).
- `InventorySystem` — `List<InventorySlot>` (`readonly struct {ItemSO Item; int Count}`); `Awake` adds
  `_startingItems`. Restore = clear + rebuild (after `Awake`, so starting items are overwritten).
- `EquipmentSystem` — `Dictionary<EquipmentSlot, ItemSO> _equipped`; equipping walks inventory. Equipped
  items stay in the inventory list (verify during implementation) → save `slot → item id` and re-equip
  through the public API so `EquipmentVisuals` + stat bonuses refresh.
- `ActionBarSystem` — `ActionBarSlotData?[6]` of `{InventoryIndex, Item}`; `ValidateSlots()` exists →
  save `slot → (inventoryIndex, itemId)`, call `ValidateSlots()` after inventory restore.
- `GoldSystem` — `Gold` (private set), `_onGoldChanged`. Same component on player and NPCs.

**Items**
- `ItemSO` has **no stable ID**. Mirror `SkillCatalogSO` (`Game.Progression`, ordered `List<SkillSO>`):
  add `ItemSO.itemId` (string, auto-filled GUID in `OnValidate` if empty, editor-only) and an
  `ItemCatalogSO` listing all items, with an editor auto-sync (pattern: `Scripts/Editor/*AutoSync.cs`,
  e.g. `PlayerRewardsAutoSync`). Concrete `ItemSO` subclasses: `WeaponSO` (+ sword types), `ArmorSO`,
  `PotionItemSO`, `SkillItemSO`, `UsableItemSO`.
- Dropped items: `InventoryUI` (`Scripts/UI/Inventory/InventoryUI.cs:104-114`) `Instantiate`s
  `item.worldItemPrefab` at a drop position; `ItemPickup` (`Scripts/Inventory/ItemPickup.cs`) holds
  `_item`, `Configure(item)`, `Destroy`s itself on pickup. Scene-authored pickups and runtime drops are
  indistinguishable today → authored pickups need a `SaveableID` (to persist "picked up"); runtime drops
  are saved as `{itemId, position, rotation}` and re-instantiated.

**Containers / doors**
- `ContainerInteractable` requires a sibling `InventorySystem`; optional `Lockable`.
- `Lockable` — `[SerializeField] bool _isLocked`, `Unlock()`; no relock. `DoorInteractable` —
  `private bool _isOpen`, rotates `_visual` via coroutine from `_closedRotation`. Restore needs a
  `SetOpenImmediate(bool)` (no coroutine) and `Lockable.SetLocked(bool)` (restore only).

**UI**
- `UIScreenManager` — `enum ScreenTab { Inventory=0, QuestLog=1, CharacterStats=2, Skills=3, Options=4 }`
  indexes `_tabPanelRoots[]` / `_tabButtons[]` on `UICanvas.prefab` + `TabButton_*` in `TabBar.prefab`.
  `UI.Cancel` (Esc) only closes the active tab. Removing `Options=4` is the last enum value → no shift of
  other indices, but both arrays + the TabBar button must be trimmed in the same change.
- `IScreenPanel` contract (`OnScreenOpen` / `OnScreenClose`), cursor only via `CursorManager`, UI input
  lifecycle (create in `Awake`, enable in `OnEnable`, `Dispose` in `OnDestroy`, null-guards). Text is
  `TMP_Text`. `UICanvas` has no `CanvasScaler` (fixed pixel sizes). Existing toasts:
  `NotificationToastUI` (use for "Game saved" / errors).
- `PlayerStateManager` exposes `IsInDialogue`, `IsInCombat`, `IsBusy` (=cursor unlocked) etc. —
  use `IsInDialogue` + open container / trade UI + `PlayerHealth.IsDead` for the save gate.
- Input actions available: `Cancel` (UI map) exists; **no** `QuickSave` / `QuickLoad` / `GameMenu`
  actions → add `QuickSave` (F5) and `QuickLoad` (F9) to the Player map following the
  `InputSystem_Actions` dual-file contract (`Assets/_Game/CLAUDE.md`).

**JSON**
- `com.unity.nuget.newtonsoft-json` 3.2.1 is only a transitive dependency → add it explicitly to
  `Packages/manifest.json`. Its DLL is auto-referenced, so `Game.asmdef` (`overrideReferences: false`)
  needs no change. Use `JsonConvert` with `Formatting.Indented` (human-readable, debug-friendly).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` | Fact store, `GetSaveData()` stub, singleton pattern |
| `Assets/_Game/Scripts/Core/SceneLoader.cs` | Additive region load + `PlayerSpawnPoint` teleport |
| `Assets/_Game/Scripts/World/PersistentID.cs` | Killed-entity hide on `Start()` |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | Entity death flow (corpse restore entry point) |
| `Assets/_Game/Scripts/Core/Animations/AIAnimationDriver.cs` | `TriggerDeath` / `EnableRagdoll` |
| `Assets/_Game/Scripts/Player/PlayerHealth.cs` | `Die()` deactivates root — must change |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | Base stats (private fields) |
| `Assets/_Game/Scripts/Player/PlayerStateManager.cs` | `IsInDialogue` etc. for the save gate |
| `Assets/_Game/Scripts/Player/Progression/*.cs` | XP / Level / LP / Skills state + events |
| `Assets/_Game/Scripts/Inventory/{InventorySystem,EquipmentSystem,ActionBarSystem,ItemPickup}.cs` | Inventory state |
| `Assets/_Game/Scripts/Economy/GoldSystem.cs` | Gold (player + NPC) |
| `Assets/_Game/Scripts/World/{ContainerInteractable,Lockable,DoorInteractable}.cs` | Container / door state |
| `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` | Runtime item drop instantiation |
| `Assets/_Game/Scripts/UI/Screens/{UIScreenManager,IScreenPanel,OptionsUI}.cs` | Tab system, Esc handling |
| `Assets/_Game/ScriptableObjects/Items/ItemSO.cs` | Needs stable `itemId` |
| `Assets/_Game/ScriptableObjects/Skills/SkillCatalogSO.cs` | Catalog SO pattern to mirror |
| `Assets/_Game/Scripts/Editor/PlayerRewardsAutoSync.cs` | Editor auto-sync pattern |
| `Assets/_Game/Scenes/CLAUDE.md`, `Prefabs/UI/CLAUDE.md`, `Scripts/UI/Screens/CLAUDE.md` | Core scene, UICanvas, tab rules |
| `Assets/Tests/EditMode/EquipmentSystemTests.cs`, `WorldStateManagerFactsTests.cs` | Test style |
| `_bmad-output/game-architecture.md` (Decision 3) | Save architecture decision |
| `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md` | Absorbed stub (set to `superseded`) |

### Technical Decisions

- **Slots:** quick (F5/F9) + auto + manual; files `save_quick.json`, `save_auto.json`,
  `save_slot_{n}.json` in `Application.persistentDataPath/Saves/`. Write to `*.tmp` then replace, so a
  crash mid-write never corrupts an existing save. Slot metadata (timestamp, region, level, playtime)
  is stored in the file's header block and read for the slot list.
- **Format:** one `SaveData` root `{ int version; SaveMeta meta; string region; Dictionary<string,bool>
  worldFacts; PlayerSaveData player; Dictionary<string, ObjectSaveData> objects; List<DroppedItemSaveData>
  droppedItems }`, Newtonsoft Json, indented. Version mismatch → refuse to load with an error message (no
  migration in v1).
- **Participant model:** one `ISaveable` interface (`SaveKey`, `ObjectSaveData Capture()`,
  `Restore(ObjectSaveData)`) implemented by a single generic `SaveableObject` component that captures
  whichever siblings exist (`InventorySystem`, `GoldSystem`, `Lockable`, `DoorInteractable`,
  `EntityHealth`). Entities use their `KilledFact` GUID as key; containers / doors / authored pickups use
  an editor-assigned `_saveId`. Player state is captured / restored by one `PlayerSaveAdapter` on the
  Player root. Unity types are wrapped (`SVector3` / `SQuaternion`) because Newtonsoft loops on
  `Vector3.normalized`.
- **Session object cache:** `SaveSystem` keeps object states for every region in memory (captured on
  region unload and on save), so region transitions keep state even before a save exists.
- **Load = reload region:** `SaveSystem` restores world facts first, then unloads the current region,
  loads the saved region additively, restores objects once the scene is loaded (after `Awake`,
  before or right after `Start` — ordering to be specified in tasks, accounting for `PersistentID.Start()`
  hiding killed entities), then restores the player and places it at the saved position.
- **Corpses:** killed entity with non-empty saved inventory → restored as a corpse (dead, brain / agent
  off, ragdoll enabled at saved root position); empty → stays hidden.
- **Death:** `PlayerHealth.Die()` no longer deactivates the Player root; control is blocked and the
  death screen (pausing) offers Load last save / Load quicksave / Quit.
- **Pause:** Game Menu + death screen set `Time.timeScale = 0` and restore on close; load resets it.
- **Save gate:** blocked in dialogue / trade / looting / death / during load; allowed in combat.
- **Menu:** new Esc Game Menu; `Options` removed from the tab bar.
- **Newtonsoft** over `JsonUtility` (dictionary support, already in the package graph).
- `tech-spec-lockable-persistence-stub.md` is absorbed by this spec.

## Implementation Plan

All paths below are relative to `Assets/_Game/` unless they start with `Assets/`, `Packages/` or
`_bmad-output/`. Runtime code: namespace per folder (`Game.Core`, `Game.Player`, `Game.World`,
`Game.Inventory`, `Game.UI`, …), `GameLog` with a `TAG`, cursor only via `CursorManager`. Editor tools go
in `Editor/Save/` (`Game.Editor` asmdef — **not** `Scripts/Editor/`).

The phases are ordered by dependency; each phase ends in a compilable, Play-Mode-checkable state.

### Tasks

#### Phase A — Foundations (IDs, data model, file store, input)

- [x] Task 1: Add Newtonsoft Json as a direct dependency
  - File: `Packages/manifest.json`
  - Action: add `"com.unity.nuget.newtonsoft-json": "3.2.1"` to `dependencies`.
  - Notes: DLL is auto-referenced → no `Game.asmdef` change. Verify `using Newtonsoft.Json;` compiles in `Game`.

- [x] Task 2: Save constants
  - File: `Scripts/Core/GameConstants.cs`
  - Action: add `SAVE_FORMAT_VERSION = 1`, `SAVE_FOLDER_NAME = "Saves"`, `SAVE_FILE_PREFIX = "save_"`,
    `SAVE_FILE_EXTENSION = ".json"`, `SAVE_SLOT_QUICK = "quick"`, `SAVE_SLOT_AUTO = "auto"`,
    `SAVE_MANUAL_SLOT_PREFIX = "slot_"`, `SAVE_MANUAL_SLOT_COUNT = 10`.
  - Notes: structural constants only — fits the `GameConstants` rule.

- [x] Task 3: Stable item IDs
  - File: `ScriptableObjects/Items/ItemSO.cs`
  - Action: add `public string itemId;` (first field, `[Tooltip]` "Stable save ID — auto-generated, never
    edit"). Add `#if UNITY_EDITOR protected virtual void OnValidate()` that assigns
    `System.Guid.NewGuid().ToString("N")` when `itemId` is null/empty and marks the asset dirty
    (`UnityEditor.EditorUtility.SetDirty(this)`).
  - Notes: if a subclass already has `OnValidate`, make it `override` and call `base.OnValidate()` (grep
    the `ItemSO` subclasses). Existing item assets get IDs the first time they're validated — Task 5's
    sync forces this for all of them.

- [x] Task 4: Item catalog
  - File (new): `ScriptableObjects/Items/ItemCatalogSO.cs` (namespace `Game.Inventory`)
  - Action: `[CreateAssetMenu(menuName = "Items/Item Catalog", fileName = "ItemCatalog")]` with
    `[SerializeField] List<ItemSO> _items`, `public IReadOnlyList<ItemSO> Items`, and
    `public ItemSO FindById(string id)` backed by a lazily built `Dictionary<string, ItemSO>` (rebuilt if
    the count changed; skips null / empty-id entries; logs a warning on duplicate IDs).
  - Notes: mirrors `SkillCatalogSO`.

- [x] Task 5: Item catalog auto-sync + ID validation (editor)
  - File (new): `Editor/Save/ItemCatalogAutoSync.cs` (namespace `Game.Editor.Save`)
  - Action: an `AssetPostprocessor` (pattern: `Scripts/Editor/PlayerRewardsAutoSync.cs`) that, when any
    `ItemSO` asset is imported / moved / deleted, finds every `ItemSO` via
    `AssetDatabase.FindAssets("t:ItemSO")`, assigns missing `itemId`s, reports duplicate `itemId`s with
    `GameLog.Error` naming both asset paths, and writes the sorted list into the single `ItemCatalogSO`
    asset. Add a menu item `Tools/Save/Sync Item Catalog` that runs the same routine on demand.
  - File (new asset): `Data/Items/ItemCatalog.asset` — create via the menu, then run the sync.
  - Notes: duplicate IDs happen when an item asset is duplicated in the Project window — the error
    message must tell the user to clear the copy's `itemId` so it regenerates.

- [x] Task 6: Saveable object identity
  - File (new): `Scripts/World/SaveableObject.cs` (namespace `Game.World`)
  - Action: `[DisallowMultipleComponent] public class SaveableObject : MonoBehaviour, ISaveable` with
    `[SerializeField] string _saveId`. `SaveKey` = the sibling `PersistentID`'s
    `KilledFact.EntityGuid` when a `PersistentID` with a fact is present, otherwise `_saveId`.
    Capture / restore behaviour is defined in Task 16.
  - File: `Scripts/World/PersistentID.cs` — add `public KilledFact KilledFact => _killedFact;`.
  - Notes: entities never need a `_saveId`; containers, doors and **scene-authored** `ItemPickup`s do.

- [x] Task 7: Save-ID validator (editor)
  - File (new): `Editor/Save/SaveableObjectValidator.cs` (namespace `Game.Editor.Save`)
  - Action: menu `Tools/Save/Validate Save IDs` that scans every `SaveableObject` in the open scenes
    (`FindObjectsByType(FindObjectsInactive.Include, FindObjectsSortMode.None)`), and: assigns a new GUID
    to any object without a `PersistentID` and with an empty `_saveId`; reports duplicates (same
    `SaveKey`) and offers to regenerate the later ones; reports `SaveableObject`s on entities whose
    `PersistentID` has no fact. Write via `SerializedObject` + `Undo` so prefab-instance overrides are
    recorded, then mark the scene dirty. Also hook `EditorSceneManager.sceneSaving` to run the same check
    (report-only, no auto-fix) on scene save.
  - File (new): `Editor/Save/SaveableObjectEditor.cs` — custom inspector showing the effective `SaveKey`,
    a "Regenerate ID" button, and a help box when the GO is part of a prefab **asset** (IDs must only be
    set on scene instances).

- [x] Task 8: Save data model
  - File (new): `Scripts/Core/Save/SaveData.cs` (namespace `Game.Core`)
  - Action: plain `[Serializable]` classes (no `UnityEngine` types inside — Newtonsoft loops on
    `Vector3.normalized`):
    - `SVector3 { float x, y, z }` + `SQuaternion { float x, y, z, w }` with `From(...)` / `To()` helpers.
    - `ItemStackSaveData { string itemId; int count; }`
    - `ActionBarSlotSaveData { int slotIndex; int inventoryIndex; string itemId; }`
    - `SaveMeta { string slotId; string savedAtUtc (ISO 8601 "o"); string region; int playerLevel;
      float playtimeSeconds; }`
    - `PlayerSaveData { SVector3 position; SQuaternion rotation; float health; int baseStrength,
      baseDexterity, baseEndurance, baseIntelligence; int xp; int totalKills; int level; int learningPoints;
      List<string> skills; List<ItemStackSaveData> inventory; Dictionary<string,string> equipped
      (EquipmentSlot name → itemId); List<ActionBarSlotSaveData> actionBar; int gold; }`
    - `ObjectSaveData { bool isDead; bool hasTransform; SVector3 position; SQuaternion rotation;
      List<ItemStackSaveData> inventory (null = no InventorySystem); int? gold; bool? isLocked;
      bool? isOpen; }`
    - `DroppedItemSaveData { string region; string itemId; SVector3 position; SQuaternion rotation; }`
    - `SaveData { int version; SaveMeta meta; string region; Dictionary<string,bool> worldFacts;
      PlayerSaveData player; Dictionary<string, ObjectSaveData> objects; List<string> consumedObjects;
      List<DroppedItemSaveData> droppedItems; }`
  - File (new): `Scripts/Core/Save/ISaveable.cs` — `interface ISaveable { string SaveKey { get; }
    ObjectSaveData Capture(); void Restore(ObjectSaveData data); }`
  - File (new): `Scripts/Core/Save/ItemStackConverter.cs` — static helpers
    `List<ItemStackSaveData> ToSave(IReadOnlyList<InventorySlot>)` and
    `List<(ItemSO item, int count)> FromSave(List<ItemStackSaveData>, ItemCatalogSO)` (unknown `itemId` →
    `GameLog.Warn` + skip; `count <= 0` → skip).

- [x] Task 9: Save file store
  - File (new): `Scripts/Core/Save/SaveFileStore.cs` (namespace `Game.Core`, static)
  - Action:
    - `string FolderPath` = `Path.Combine(Application.persistentDataPath, SAVE_FOLDER_NAME)`;
      `PathFor(slotId)`.
    - `bool TryWrite(SaveData data, out string error)`: serialize with
      `JsonConvert.SerializeObject(data, Formatting.Indented)`; `Directory.CreateDirectory`; write
      `{path}.tmp`; if the target exists `File.Replace(tmp, path, null)` else `File.Move(tmp, path)`.
    - `bool TryRead(string slotId, out SaveData data, out string error)`: missing file → false + "No save";
      JSON exception → false + "Save file is corrupted"; `version != SAVE_FORMAT_VERSION` → false +
      "Save was made with an incompatible version"; null `player` / `worldFacts` → corrupted.
    - `List<SlotInfo> ListSlots()` → for `quick`, `auto`, `slot_1..slot_N`: `SlotInfo { slotId, exists,
      isValid, SaveMeta meta, string error }` (reads each file; files are small).
    - `bool TryDelete(string slotId, out string error)`.
    - `SlotInfo? MostRecentValid()` (by `meta.savedAtUtc`).
  - Notes: every file call inside try/catch → `GameLog.Error("[Save]", …)` + false; never throws.
    Synchronous on purpose (files are a few KB; avoids `Task` / frame-loop issues in `project-context.md`).

- [x] Task 10: QuickSave / QuickLoad input actions
  - Files: `InputSystem_Actions.inputactions` **and** the embedded JSON in `InputSystem_Actions.cs`
    (dual-file contract, `Assets/_Game/CLAUDE.md`), plus the generated C# accessors in the `.cs`
    (`m_Player_QuickSave`, `QuickSave` property, `IPlayerActions.OnQuickSave`, same for `QuickLoad`).
  - Action: add `QuickSave` (Button, `<Keyboard>/f5`) and `QuickLoad` (Button, `<Keyboard>/f9`) to the
    `Player` map.
  - Notes: follow exactly how `SkillsToggle` was added (most recent action) — grep it in both files.

#### Phase B — Restore APIs on existing systems

Each restore method sets state directly, raises only the *value-changed* event that refreshes UI, and
**never** raises `_onLevelUp`, `_onSkillLearned`, `_onXPGained`, `_onEntityKilled` or calls
`RegisterKill` / `RegisterDeath` (those grant LP, XP, toasts and rewards).

- [x] Task 11: World facts restore
  - File: `Scripts/Core/State/WorldStateManager.cs`
  - Action: replace the `WorldStateSaveData` struct + `GetSaveData()` stub with
    `public Dictionary<string,bool> CaptureFacts()` (copy) and
    `public void RestoreFacts(Dictionary<string,bool> facts)` (clear + copy, null → empty, **no events**).
    Update the class summary (drop the "Epic 8" notes).
  - File: `Scripts/Quest/QuestEventsManager.cs`
  - Action: extract the `Start()` seeding loop into `public void ReseedState()` (clears `_lastState`, then
    seeds); `Start()` calls it. Prevents spurious started / completed events after a load.

- [x] Task 12: Player stats, health, stamina restore
  - `Scripts/Player/PlayerStats.cs`: `public void RestoreBaseStats(int str, int dex, int end, int intl)` —
    sets the four `_base*` fields, raises `_onStatsChanged`.
  - `Scripts/Player/PlayerHealth.cs`: `public void RestoreHealth(float health)` — clamps to
    `[1, MaxHealth]`, `IsDead = false`, raises `_onPlayerHealthChanged`.
  - `Scripts/Combat/StaminaSystem.cs`: `public void RefillToMax()` — `_currentStamina = MaxStamina`,
    raises its existing changed event (same one as in the regen path).

- [x] Task 13: Progression restore
  - `XPSystem.cs`: `public void RestoreState(int xp, int totalKills)` — no event.
  - `LevelSystem.cs`: `public void RestoreLevel(int level)` — clamp `[1, MaxLevel]`, then `RaiseProgress()`.
    No `_onLevelUp`.
  - `LearningPointSystem.cs`: `public void RestoreLP(int lp)` — clamp `>= 0`, raise `_onLPChanged`.
  - `PlayerSkills.cs`: `public IReadOnlyCollection<string> LearnedSkillIds` and
    `public void RestoreSkills(IEnumerable<string> ids)` — clear + add, no `_onSkillLearned`.

- [x] Task 14: Inventory, equipment, action bar, gold restore
  - `Scripts/Inventory/InventorySystem.cs`: `public void RestoreSlots(IEnumerable<(ItemSO item, int count)>)`
    — clears `_slots`, appends one slot per entry with `count` clamped to `[1, item.maxStacks]` (no
    `AddItem` re-stacking, so slot order is preserved — the action bar relies on indices).
  - `Scripts/Inventory/EquipmentSystem.cs`: `public IReadOnlyDictionary<EquipmentSlot, ItemSO> Equipped`
    and `public void RestoreEquipped(IReadOnlyDictionary<EquipmentSlot, ItemSO> equipped)` — replaces
    `_equipped` **without touching the inventory** (equipped items are not in the inventory list — `Equip`
    removes them), skips items that are not `EquipableItemSO`, raises `_onEquipmentChanged`, calls
    `RecomputeAndApplyBonuses()`.
  - `Scripts/Inventory/ActionBarSystem.cs`: `public void RestoreSlots(IEnumerable<(int slot, int invIndex,
    ItemSO item)>)` — clears all six, `Assign` each, then `ValidateSlots()`. Ensure `ActionBarUI` refreshes
    (check how it picks up `Assign` today; if it only refreshes on its own events, raise / call the same
    refresh path).
  - `Scripts/Economy/GoldSystem.cs`: `public void RestoreGold(int gold)` — clamp `>= 0`, raise
    `_onGoldChanged`.

- [x] Task 15: World object restore hooks
  - `Scripts/World/Lockable.cs`: `public void RestoreLocked(bool locked)` (no log spam, no event).
  - `Scripts/World/DoorInteractable.cs`: `public bool IsOpen => _isOpen;` and
    `public void SetOpenImmediate(bool open)` — stops `_rotateRoutine`, sets `_isOpen`, snaps `_visual`
    to `_closedRotation` or the open rotation (same math as the coroutine end-state).
  - `Scripts/AI/EntityHealth.cs`: `public void RestoreAsCorpse()` — `IsDead = true`, health 0, stop +
    disable `NavMeshAgent`, `_animationDriver?.SetInCombat(false)`, `_animationDriver?.EnableRagdoll()`.
    No `RegisterDeath`, no `TriggerDeath`. `EntityBrain` then switches itself to `Dead` on its next tick
    (`EntityBrain.cs:147`) — `TransitionToDead` only stops the agent and ends attacks, so it is safe.
  - `Scripts/Inventory/ItemPickup.cs`: `public ItemSO Item => _item;`. In `Interact()`, before
    `Destroy(gameObject)`: if a sibling `SaveableObject` exists, call
    `SaveSystem.Instance?.MarkConsumed(saveable.SaveKey)`.

- [x] Task 16: SaveableObject capture / restore
  - File: `Scripts/World/SaveableObject.cs`
  - Action: cache siblings in `Awake` (`InventorySystem`, `GoldSystem`, `Lockable`, `DoorInteractable`,
    `EntityHealth`, `PersistentID`). `Capture()` fills `ObjectSaveData` with what is present:
    `inventory`, `gold`, `isLocked`, `isOpen`, and for entities `isDead` + `hasTransform` + root
    position / rotation. `Restore(data)`:
    1. inventory → `InventorySystem.RestoreSlots` (via `ItemStackConverter` + `SaveSystem.Instance.Catalog`);
       gold → `RestoreGold`; lock → `RestoreLocked`; door → `SetOpenImmediate`.
    2. Entity that is killed (`WorldStateManager.Instance.IsKilled(PersistentID.KilledFact)`):
       if the restored inventory is non-empty → `gameObject.SetActive(true)`, set root position / rotation
       (disable the `NavMeshAgent` first so it doesn't snap to the mesh), `EntityHealth.RestoreAsCorpse()`;
       if empty → leave inactive (`PersistentID` already hid it).
    3. Alive entities: inventory / gold only — position is **not** restored (they reset to spawn).

#### Implementation notes — Phases A+B (2026-10-07)

Deviations / facts for whoever implements Phase C onward:
- Newtonsoft pinned to **3.2.2** (already resolved transitively; a direct 3.2.1 would downgrade it).
  `Tests.EditMode.asmdef` (`overrideReferences: true`) gained `Newtonsoft.Json.dll` in `precompiledReferences`.
- `SaveData` classes are **not** `[Serializable]` (Newtonsoft doesn't need it; the attribute triggers
  Unity serialization-analyzer warnings on dictionaries / nullables).
- `SaveFileStore.TryRead` parses with `DateParseHandling.None` (default parsing rewrites `savedAtUtc`),
  checks `version` before mapping (wrong version → "incompatible", not "corrupted"), and treats a null
  `meta` as corrupted too. Error strings are public constants (`ERROR_NO_SAVE` / `_CORRUPTED` / `_INCOMPATIBLE`).
  Test hook `FolderOverride` is `internal`; `Assets/_Game/AssemblyInfo.cs` adds `InternalsVisibleTo("Tests.EditMode")`.
- `SaveSystem.cs` exists as a **skeleton** (singleton, `_catalog` / `Catalog`, `_consumed` + `MarkConsumed`) because
  Task 15/16 call it. Task 19 extends this file. It is not yet on the `SaveSystem` GO in `Core.unity`.
- QuickSave / QuickLoad follow the SkillsToggle precedent: JSON + field + `FindAction` + property only, no
  `IPlayerActions.OnQuickSave/OnQuickLoad` callbacks (none of the recent actions have them).
- Corpse placement uses `NavMeshAgent.Warp` instead of disabling the agent — `EntityBrain.TransitionToDead()`
  sets `_agent.isStopped` unconditionally, which errors on a disabled agent. `EntityHealth.RestoreAsCorpse()` also
  clears the warning pose and invokes `HealthChanged`.
- `ActionBarSystem.RestoreSlots` raises `OnActionBarUsed(-1)`: its only listeners (`ActionBarUI`, `InventoryUI`)
  ignore the index and refresh — this also refreshes the inventory grid after the player restore.
- `RestoreSkills` raises nothing, so the Skills tab only refreshes on open; `RestoreState` (XP) raises nothing either
  — `LevelSystem.RestoreLevel` refreshes the XP bar. Restore XP before level (adapter order already does).
- `ItemCatalogAutoSync` force-saves items whose file lacks their in-memory `itemId` (`OnValidate` fills IDs without
  reliably dirtying the asset). All 10 items now have IDs; `Data/Items/ItemCatalog.asset` created and synced.
- `SaveableObject.SaveKey` resolves `PersistentID` uncached until `Awake` (edit-mode tools see component changes).

- Review fixes (adversarial review, auto-fixed): `ObjectSaveData.isDead` **removed** (dead state comes from the
  KilledFact); a killed entity with no saved inventory stays hidden (never revives with authored loot);
  `EntityBrain.TransitionToDead` guards `isStopped` (off-mesh corpse agents); `ActionBarSystem.REFRESH_ONLY_SLOT`
  names the `-1` refresh payload — new `OnActionBarUsed` listeners must ignore it; `PlayerSkills.GetLearnedSkillIds()`
  returns a copy (replaces the `LearnedSkillIds` property); `RestoreEquipped` rejects items in the wrong slot;
  `ListSlots` doesn't log (errors live in `SlotInfo.error`); `TryRead` forces `meta.slotId` to the file's slot.
- **Open for Phase C:** (F3) re-activating a corpse runs `OnEnable` on brain / health / registries before
  `RestoreAsCorpse()` — verify nothing targets or patrols for that frame. (F13) QuickSave / QuickLoad are in the
  **Player** map — if dialogue / menus / death disable it, F9-while-dead won't fire; SaveSystem's own
  `InputSystem_Actions` instance must keep its Player map enabled or the actions move to a map that stays on.

#### Phase C — SaveSystem orchestration and region flow

- [x] Task 17: SceneLoader events and region reload
  - File: `Scripts/Core/SceneLoader.cs`
  - Action:
    - `public string CurrentRegion { get; private set; }` (set after a region finishes loading; set in the
      "already open in editor" path too).
    - `public event Action<string> RegionUnloading;` raised before `UnloadSceneAsync`;
      `public event Action<string, bool> RegionLoaded;` (sceneName, isStartup) raised **one frame after**
      the load op completes (so every `Start()` — incl. `PersistentID` hiding killed entities — has run),
      replacing the direct `SpawnPlayerAtPoint()` call order: spawn first, then raise.
    - `public void ReloadRegion(string sceneName, Action onLoaded)` — unloads every loaded scene except the
      one containing this `SceneLoader` (Core), then loads `sceneName` additively, does **not** call
      `SpawnPlayerAtPoint`, raises `RegionLoaded(sceneName, false)`, then invokes `onLoaded`.
    - `public bool IsBusy` while any load / unload coroutine runs.
  - Notes: all waits are `yield return op` / `yield return null` — they work at `Time.timeScale = 0`.

- [x] Task 18: Player save adapter
  - File (new): `Scripts/Player/PlayerSaveAdapter.cs` (namespace `Game.Player`), on the `Player.prefab`
    root.
  - Action: `[SerializeField]` refs to `PlayerStats`, `PlayerHealth`, `StaminaSystem`, `XPSystem`,
    `LevelSystem`, `LearningPointSystem`, `PlayerSkills`, `InventorySystem`, `EquipmentSystem`,
    `ActionBarSystem`, `GoldSystem`, `PlayerStateManager`, `CharacterController`.
    `PlayerSaveData Capture()` reads all of them. `void Restore(PlayerSaveData d, ItemCatalogSO catalog)`
    in this order: base stats → skills → XP / level / LP → inventory → equipment → action bar → gold →
    health (`RestoreHealth`) → stamina (`RefillToMax`, after stats because max depends on Endurance) →
    `PlayerStateManager.SetDead(false)` → teleport (disable `CharacterController`, set position / rotation,
    re-enable). Null-guard every ref with a `GameLog.Warn`.

- [x] Task 19: SaveSystem singleton
  - File (new): `Scripts/Core/Save/SaveSystem.cs` (namespace `Game.Core`), on the existing empty
    `SaveSystem` GO in `Core.unity`.
  - Fields: `[SerializeField] SceneLoader _sceneLoader; PlayerSaveAdapter _player; ItemCatalogSO _catalog;
    PlayerStateManager _playerState; PlayerHealth _playerHealth; ContainerUI _containerUI;
    GameEventSO_Quest _onQuestCompleted; GameEventSO_String _onSaveNotification;`
    `public static SaveSystem Instance`, `public ItemCatalogSO Catalog`, `public bool IsLoading`,
    `public float PlaytimeSeconds`.
  - In-memory session state: `Dictionary<string, ObjectSaveData> _objectStates`, `HashSet<string>
    _consumed`, `List<DroppedItemSaveData> _dropped`, `SaveData _newGameSnapshot`, `bool _autosavePending`.
  - Singleton: same pattern as `WorldStateManager` (duplicate → destroy). No `DontDestroyOnLoad` needed
    beyond what `Core.unity` already gives — match `WorldStateManager` exactly for consistency.
  - Behaviour:
    - `public bool CanSave(out string reason)` → false when `IsLoading`, `_sceneLoader.IsBusy`,
      `_playerHealth.IsDead`, `_playerState.IsInDialogue` (covers trade, which opens from dialogue),
      `_containerUI.IsOpen` (containers + corpse looting). Combat is allowed.
    - `public bool Save(string slotId)`: gate → `CaptureRegion(CurrentRegion)` → build `SaveData`
      (`version`, `meta` (slotId, `DateTime.UtcNow.ToString("o")`, region, level, playtime), `region`,
      `worldFacts = WorldStateManager.CaptureFacts()`, `player = _player.Capture()`, copies of
      `_objectStates` / `_consumed` / `_dropped`) → `SaveFileStore.TryWrite` → raise `_onSaveNotification`
      ("Game saved" / "Quicksave" / "Autosave" / error text). Returns success.
    - `CaptureRegion(region)`: for every `ISaveable` (`SaveableObject`) in that scene
      (`FindObjectsByType<SaveableObject>(FindObjectsInactive.Include, …)` filtered by
      `gameObject.scene.name == region`) → `_objectStates[key] = Capture()`; replace all `_dropped` entries
      of that region with every `ItemPickup` in the scene **without** a `SaveableObject` (runtime drops).
      Skip empty keys with `GameLog.Warn`.
    - `ApplyRegion(region)`: for every `SaveableObject` in the scene: key in `_consumed` → `Destroy` the
      GO; else entry in `_objectStates` → `Restore`. Then instantiate each `_dropped` entry for that region
      from `item.worldItemPrefab` (`Configure(item)`, `SceneManager.MoveGameObjectToScene` into the region
      scene). Unknown item → warn + skip.
    - `public void MarkConsumed(string key)`.
    - `public void Load(string slotId)`: refuse if `IsLoading` or `_sceneLoader.IsBusy`. `TryRead` — on
      failure raise `_onSaveNotification` with the error and **leave the world untouched**. Otherwise
      `BeginLoad(data)`.
    - `BeginLoad(SaveData data)`: `IsLoading = true`; show loading overlay (Task 24); close every menu
      (`UIScreenManager.CloseAll()`, `GameMenuUI.Close()`, `DeathScreenUI.Hide()` — via
      `[SerializeField]` refs or a `GameEventSO_Void _onLoadStarted` that those panels listen to; prefer
      the event); `WorldStateManager.RestoreFacts(data.worldFacts)`; replace `_objectStates` /
      `_consumed` / `_dropped` from `data`; `PlaytimeSeconds = data.meta.playtimeSeconds`;
      `_sceneLoader.ReloadRegion(data.region, () => FinishLoad(data))`.
    - `FinishLoad(data)`: (objects already applied by the `RegionLoaded` handler) →
      `_player.Restore(data.player, _catalog)` → `QuestEventsManager.ReseedState()` (find via
      `[SerializeField]`) → hide overlay → `Time.timeScale = 1` → `CursorManager.Lock()` →
      `IsLoading = false` → notification "Game loaded".
    - `RegionUnloading` handler: if not `IsLoading` → `CaptureRegion(region)` (region transitions keep state
      in memory). `RegionLoaded(region, isStartup)` handler: `ApplyRegion(region)`; if `isStartup` and not
      loading → `_newGameSnapshot = BuildSaveData("newgame")` (in memory only, never written); if not
      startup and not loading → request autosave.
    - `public void RestartNewGame()` → `BeginLoad(_newGameSnapshot)` (used by the death screen when no save
      exists).
    - Autosave: `_onQuestCompleted` listener and region transitions set `_autosavePending = true`.
      `Update()`: `PlaytimeSeconds += Time.unscaledDeltaTime` when `Time.timeScale > 0` and not loading;
      if `_autosavePending && CanSave(out _)` → `Save(SAVE_SLOT_AUTO)`, clear flag. (Quests usually complete
      inside dialogue, so autosave runs right after the dialogue closes.)
    - Quick slots: own an `InputSystem_Actions` (UI-input lifecycle rules) for `Player.QuickSave` →
      `Save(SAVE_SLOT_QUICK)` and `Player.QuickLoad` → `Load(SAVE_SLOT_QUICK)` (QuickLoad ignored while the
      Game Menu's confirm dialog is open; allowed while dead).
  - Notes: subscribe to `SceneLoader` C# events in `OnEnable` / unsubscribe in `OnDisable`; the first
    `RegionLoaded` from `SceneLoader.Start()` arrives after `SaveSystem.OnEnable` because both are in
    `Core.unity` and the event is raised at least one frame later.

#### Phase D — Death flow

- [x] Task 20: Keep the player active on death
  - File: `Scripts/Player/PlayerHealth.cs` — `Die()` no longer calls `gameObject.SetActive(false)`; it
    calls `_playerStateManager?.SetDead(true)` (new `[SerializeField] PlayerStateManager`, same-system
    ref) after raising `_onPlayerDied`. Update the class summary.
  - File: `Scripts/Player/PlayerStateManager.cs` — `public bool IsDead { get; private set; }`,
    `public void SetDead(bool)`, and include `IsDead` in `IsBusy` (`IsBusy => IsDead || !CursorManager.IsLocked`)
    so `CanMove/CanAttack/CanDodge/CanJump/CanBlock` all return false.
  - Notes: `TargetRegistry` / AI must stop targeting a dead player — check `PlayerHealth.IsDead` is already
    what `IDamageable.IsDead` exposes (it is; `EntityBrain.cs:197` checks `Damageable.IsDead`). Play the
    player's death animation only if one already exists; adding one is out of scope.

#### Implementation notes — Phases C+D (2026-10-07)

- `SceneLoader.ReloadRegion(name, Action<bool> onDone)` — `onDone(false)` when the load fails mid-way;
  `SaveSystem.FailLoad` then resets `IsLoading` / `timeScale` and raises `OnLoadFinished`, so the loading overlay
  never sticks. `SceneLoader.CanLoadRegion(name)` (build-settings check) runs **before** facts are touched.
  The startup path also spawns the player one frame later than before (all region `Start()`s run first).
- `SaveSystem` input: its own `InputSystem_Actions` enables **only** `Player.QuickSave` / `Player.QuickLoad`, so other
  systems disabling their Player maps can't silence F5 / F9 (resolves review F13). Phase E still has to ignore
  QuickLoad while the Game Menu confirm dialog is open (`SaveSystem` has no hook for it yet).
- Fields wired in Task 26: `_sceneLoader`, `_catalog`, `_player`, `_playerState`, `_playerHealth`, `_containerUI`,
  `_questEvents`, `_onQuestCompleted`, `_onSaveNotification`, `_onLoadStarted`, `_onLoadFinished`.
  `PlayerSaveAdapter` and `PlayerHealth._playerStateManager` fall back to `GetComponent` on the Player root when unwired.
- `CanSave` also checks `PlayerStateManager.IsDead` and an empty `CurrentRegion`; its reasons are player-facing
  strings (shown by F5 / the Save button). Notifications: "Game saved" / "Quicksave" / "Autosave" / "Game loaded" /
  "Restarted" / "No quicksave" / error text.
- Death (Task 20): `PlayerStateManager.SetDead(true)` clears block / attack / dodge and plays the shared humanoid
  `Death` trigger; `SetDead(false)` (from `PlayerSaveAdapter.Restore`) calls `HumanoidAnimationBridge.ResetToDefaultState()`
  (`Animator.Rebind`) and re-applies `IsInCombat`. `UIScreenManager.OpenTab` refuses tabs while dead.
  `InteractionSystem` also refuses focus / interaction while `PlayerStateManager.IsDead`.
- Review fixes (C+D, auto-fixed): `Load` / `RestartNewGame` refuse while in dialogue / trade or looting (those windows
  reference objects the reload destroys and nothing closes them) — still allowed while dead; every load sheathes the
  weapon (stance isn't saved); the teleport zeroes `PlayerController` fall velocity (`ResetVerticalVelocity`).
  Known limitation: a region that fails to load after the old one unloaded leaves an empty world (toast only).
- Play-Mode smoke test (runtime-attached SaveSystem, StartingTown): save slot → change gold / HP / position → load
  restored all three through a full region reload; F9 with no quicksave → "No quicksave"; death keeps the Player active.
  **Until Task 26 adds `SaveableObject` to scene-authored pickups, those are captured as runtime drops and duplicate
  on load** — expected, fixed by the wiring.

#### Phase E — UI

- [x] Task 21: Remove the Options tab
  - Files: `Scripts/UI/Screens/UIScreenManager.cs` (`ScreenTab` loses `Options = 4`),
    `Prefabs/UI/UICanvas.prefab` (trim `_tabPanelRoots` / `_tabButtons` to 4 entries; reparent the
    `OptionsPanel` root under the new Game Menu, Task 22), `TabBar.prefab` (delete `TabButton_Options`).
  - Notes: last enum value → no index shift. Grep for `ScreenTab.Options` before deleting.

- [x] Task 22: Game Menu
  - File (new): `Scripts/UI/Screens/GameMenuUI.cs` (namespace `Game.UI`, implements `IScreenPanel`)
  - Layout (under `UICanvas`, full-screen dim background + centered column, fixed pixel sizes, `TMP_Text`):
    title "Game Menu", buttons **Resume**, **Save Game**, **Load Game**, **Options**, **Quit**; sub-panels
    `SaveSlotListUI` (Task 23) and the reparented `OptionsPanel` (`OptionsUI`) with a **Back** button.
  - Behaviour: `Open()` → `Time.timeScale = 0`, `CursorManager.Unlock()`, show main buttons;
    `Close()` → hide everything, `Time.timeScale = 1`, `CursorManager.Lock()`. `IsOpen` property.
    **Save Game** disabled with a tooltip/subtitle reason when `!SaveSystem.CanSave(out reason)`.
    **Quit** → confirm dialog "Quit without saving?" → `Application.Quit()` (`#if UNITY_EDITOR`
    `UnityEditor.EditorApplication.isPlaying = false`). Esc inside a sub-panel goes back one level; Esc on
    the main buttons = Resume.
  - File: `Scripts/UI/Screens/UIScreenManager.cs` — route Esc: `HandleCancel` → if a tab is open
    `CloseAll()`; else if the Game Menu is open → `GameMenuUI.HandleBack()`; else if
    `_wasGameplayLastFrame` → `GameMenuUI.Open()`. `_wasGameplayLastFrame` is sampled in `LateUpdate` as
    `CursorManager.IsLocked && !_playerStateManager.IsDead && !SaveSystem.Instance.IsLoading` — this
    prevents the same Esc press that closes dialogue / container / trade (their own `Cancel` handlers lock
    the cursor in the same frame) from also opening the menu. Tab toggles (I/J/C/K) are ignored while the
    Game Menu is open.

- [x] Task 23: Save / Load slot list
  - File (new): `Scripts/UI/Screens/SaveSlotListUI.cs` (+ a `SaveSlotEntryUI.cs` row component and a
    `SaveSlotEntry.prefab` under `Prefabs/UI/`)
  - Action: `Show(Mode mode)` (`Save` | `Load`) rebuilds rows from `SaveFileStore.ListSlots()`.
    - Row text: slot label ("Quicksave", "Autosave", "Slot 3"), then for a valid save
      `"{region} — Level {playerLevel} — {local date/time} — {playtime h:mm}"`, "Empty" for a missing file,
      "Corrupted — {error}" for an invalid one.
    - Save mode: `quick` and `auto` rows are hidden (written only by F5 / autosave); clicking a manual row
      saves (confirm "Overwrite Slot N?" when it exists); after saving, refresh the rows.
    - Load mode: all rows shown; empty / corrupted rows not clickable; clicking a valid row → confirm
      "Load this save? Unsaved progress will be lost." → `SaveSystem.Load(slotId)`.
    - Each existing row has a **Delete** button → confirm → `SaveFileStore.TryDelete` → refresh.
  - Notes: shared confirm dialog component (`ConfirmDialogUI`, new, Yes/No + message) used by Tasks 22–24.

- [x] Task 24: Death screen + loading overlay
  - File (new): `Scripts/UI/Screens/DeathScreenUI.cs` — listens to `_onPlayerDied` (`GameEventSO_Void`)
    in `OnEnable` / `OnDisable`; after a 2 s **realtime** delay (`WaitForSecondsRealtime`, cached) shows
    "You died", sets `Time.timeScale = 0`, `CursorManager.Unlock()`. Buttons: **Load last save**
    (`SaveFileStore.MostRecentValid()`; hidden if none), **Load quicksave** (hidden if no valid quick
    save), **Restart** (only shown when no valid save exists → `SaveSystem.RestartNewGame()`), **Quit**.
    Hides itself on the load-started event.
  - File (new): `Scripts/UI/Screens/LoadingOverlayUI.cs` — full-screen black panel with "Loading…",
    own nested Canvas with a high sort order, `Show()` / `Hide()`; listens to load started / finished
    events.
  - File: `Scripts/UI/HUD/NotificationToastUI.cs` — add `[SerializeField] GameEventSO_String
    _onSaveNotification` → shows the string as a toast (same entry prefab).

#### Phase F — Assets, wiring, docs

- [x] Task 25: Event channel assets
  - Create (same folder as the other event SOs, e.g. `ScriptableObjects/Events/` assets): 
    `OnSaveNotification` (`GameEventSO_String`), `OnLoadStarted` and `OnLoadFinished`
    (`GameEventSO_Void`).

- [x] Task 26: Scene and prefab wiring
  - `Core.unity`: add `SaveSystem` component to the existing `SaveSystem` GO and wire all refs
    (`SceneLoader`, Player's `PlayerSaveAdapter` / `PlayerStateManager` / `PlayerHealth`, `ContainerUI`,
    `ItemCatalog`, `QuestEventsManager`, quest-completed + notification + load events).
  - `Prefabs/Player/Player.prefab`: add + wire `PlayerSaveAdapter`; wire `PlayerHealth._playerStateManager`.
  - `Prefabs/UI/UICanvas.prefab`: add `GameMenu` (+ `SaveSlotList`, reparented `OptionsPanel`,
    `ConfirmDialog`), `DeathScreen`, `LoadingOverlay`; wire `UIScreenManager._gameMenu`; wire the toast's
    `_onSaveNotification`.
  - `Prefabs/Entities/Entity_base.prefab`: add `SaveableObject` (no `_saveId` — key comes from
    `PersistentID`).
  - `StartingTown.unity` and `TestScene.unity`: add `SaveableObject` to every container, door and
    scene-authored `ItemPickup`; run `Tools/Save/Validate Save IDs` and save the scenes.
  - Notes: follow the YAML-edit / `refresh_unity(if_dirty)` rule from the root `CLAUDE.md` if editing
    prefabs on disk.

- [x] Task 27: Docs and spec bookkeeping
  - `Scripts/Core/CLAUDE.md`: add a `Save/` section (SaveSystem flow, restore-without-events rule,
    `SaveFileStore`, slot IDs).
  - `Scripts/World/CLAUDE.md`: `SaveableObject` + key rules (entities use the `KilledFact` GUID, others need
    a scene-instance `_saveId`; run the validator).
  - `Scripts/UI/Screens/CLAUDE.md`: tab enum without `Options`, Game Menu / Esc routing, pause rule,
    death screen.
  - `Scenes/CLAUDE.md`: `SaveSystem` GO is now scripted.
  - `ScriptableObjects/Items/CLAUDE.md`: `itemId` + `ItemCatalog` sync.
  - `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md`: set
    `status: 'superseded'` with a pointer to this spec.

#### Implementation notes — Phases E+F (2026-10-08)

- Screens with listeners that must run while hidden (`GameMenuUI`, `DeathScreenUI`, `LoadingOverlayUI`) sit on an
  always-active root and toggle a `Panel` child. `ConfirmDialogUI` is a plain inactive GO (`Show` / `Cancel` / `Hide`).
- `GameMenuUI` hides on `OnLoadStarted` **without** touching timeScale / cursor — a load from the paused menu stays paused
  while the region reloads (scene loading works at `timeScale = 0`; the player can't fall). `SaveSystem.FailLoad` now also
  locks the cursor. `SaveSystem._confirmDialog` (new ref) makes F9 a no-op while a confirm dialog is open.
- `UIScreenManager` gained `_gameMenu`, `_onLoadStarted` (closes tabs) and `_onPlayerDied` (closes tabs); `ContainerUI`
  gained `_onPlayerDied` (closes the loot window). Without this, Esc on an open tab / loot window after death re-locked
  the cursor under the death screen, and an open loot window blocked `Load` (`CanLoad`).
- `NotificationContainer` got its own Canvas (override sorting 50) so save / load toasts draw above the Game Menu;
  `LoadingOverlay` uses sorting 100.
- Slot rows: `Prefabs/UI/SaveSlotEntry.prefab`; menu buttons are `Prefabs/UI/Common/ActionButton` instances.
  `OptionsUI` was reparented under `GameMenu/Panel/Window` with a "coming soon" label. `TabButton_Options` deleted from
  `TabBar.prefab`; tab arrays trimmed to 4 in `UICanvas.prefab` (no Player.prefab / Core.unity overrides on them).
- Wiring: `SaveSystem` component added + wired in `Core.unity`; `PlayerSaveAdapter` added to `Player.prefab` (all refs
  wired), `PlayerHealth._playerStateManager` and `DeathScreenUI._playerStateManager` wired there; `SaveableObject` on
  `Entity_base.prefab` and on every container / door / authored pickup in StartingTown (8) and TestScene (7 pickups —
  TestScene has no containers / doors); `Validate Save IDs` assigned the IDs (no duplicates reported).
- New events: `Data/Events/OnSaveNotification` (`GameEventSO_String`), `OnLoadStarted` / `OnLoadFinished` (`GameEventSO_Void`).
- Tests: `SaveSlotListFormatTests` (slot label / details / timestamp / playtime); full EditMode suite 539/539.
- Play-Mode smoke test (StartingTown, driven through code, not real key presses): Game Menu open pauses + unlocks,
  Options → Back → Resume restores; save to an empty slot writes directly, a used slot asks "Overwrite Slot 1?";
  `HandleBack` walks confirm → sub-panel → close; death with the inventory open closes it, death screen after 2 s with
  the world paused; Load last save and Restart (no saves) both revive the player through a region reload; no
  authored-pickup duplication after load. **Not yet checked by hand:** real Esc / F5 / F9 key presses (incl. Esc
  closing dialogue / container without opening the menu), autosave after a quest completes in dialogue, corpse / door /
  chest round-trips (AC 4–9), corrupted / incompatible file rows (AC 17–18).

## Review Notes

- Adversarial review (Phases E+F) done inline: 6 findings — 3 fixed, 3 noise.
  - F1 (High, fixed): dying with a tab or loot window open — closing it (Esc) re-locked the cursor under the death
    screen, and an open loot window made every death-screen load fail `CanLoad`. Tabs and `ContainerUI` now close on `OnPlayerDied`.
  - F2 (Low, fixed): `?.` on serialized Unity component refs in `GameMenuUI` / `DeathScreenUI` (fake-null in the editor)
    → explicit null checks (`GameMenuUI.Wire`, `SetMainVisible`, `HideSubPanels`, `SetVisible`).
  - F3 (Low, fixed): duplicated quit code → shared `GameMenuUI.QuitGame`.
  - F4 (noise): `UIScreenManager.LateUpdate` samples state every frame — prescribed by the spec, allocation-free.
  - F5 (noise): toasts (sort 50) draw over the confirm dialog — intended so feedback stays visible.
  - F6 (noise / known limitation): dialogue / trade windows don't close on death; enemies rarely reach the player there.
    If it happens, Esc closes the window and re-locks the cursor under the death screen.
- Resolution approach: auto-fix.

### Acceptance Criteria

**Save / load round-trip**
- [ ] AC 1: Given the player has moved, gained XP, levelled up, learned a skill, raised a stat, changed
  inventory / equipment / action bar / gold, when they save to Slot 1, change all of those again, and load
  Slot 1, then position, rotation, health, base stats, XP, level, LP, skills, inventory (order and
  counts), equipped items (with visuals and stat bonuses), action-bar slots and gold all match the save.
- [ ] AC 2: Given a load, when it finishes, then no level-up, skill-learned, XP, kill-reward or
  quest-started / completed toast or event fires, and LP is not granted again.
- [ ] AC 3: Given a quest was completed and a dialogue played before saving, when the player resets that
  state in Play Mode (or continues further) and loads, then the quest log and NPC dialogue options reflect
  the saved facts, and the next real fact change does not fire a stale "quest completed" event.

**World objects**
- [ ] AC 4: Given a chest from which the player took items and that the player unlocked, when the game is
  saved and loaded, then the chest still lacks those items and is unlocked.
- [ ] AC 5: Given a door that was unlocked and left open, when saved and loaded, then it is unlocked and
  open with no opening animation playing.
- [ ] AC 6: Given the player sold items to an NPC trader, when saved and loaded, then the trader's inventory
  and gold match the moment of saving.
- [ ] AC 7: Given a killed enemy whose corpse still holds loot, when saved and loaded, then the corpse lies
  (ragdoll) at its death position, shows "Loot", and holds exactly the remaining items; no XP is granted.
- [ ] AC 8: Given a killed enemy whose corpse was fully looted, when saved and loaded, then it is absent.
- [ ] AC 9: Given the player picked up a scene-placed item and dropped another inventory item on the ground,
  when saved and loaded, then the picked-up item does not reappear and the dropped item is at its drop
  position and can be picked up.
- [ ] AC 10: Given a living, never-attacked enemy that walked away from its spawn, when loaded, then it is
  back at its authored spawn position (AI state is not persisted).

**Slots, triggers, gating**
- [ ] AC 11: Given gameplay with no menu open, when the player presses F5, then `save_quick.json` is
  written and a "Quicksave" toast appears; when they press F9, then the quicksave is loaded.
- [ ] AC 12: Given F9 is pressed and no quicksave exists, when the key is handled, then a "No quicksave"
  toast appears and nothing else happens.
- [ ] AC 13: Given the player completes a quest inside a dialogue, when the dialogue closes, then
  `save_auto.json` is written once and an "Autosave" toast appears.
- [ ] AC 14: Given the player is in dialogue, trading, looting a container or corpse, dead, or a load is in
  progress, when they press F5 or open Save Game, then no file is written (F5 shows the blocking reason as
  a toast; the Save Game button is disabled with the reason).
- [ ] AC 15: Given the player is in combat with an enemy, when they press F5, then the save succeeds.
- [ ] AC 16: Given a save in progress crashes or the app is killed mid-write, when the game restarts, then
  the previous version of that slot file is still intact (temp-file + replace).

**Errors**
- [ ] AC 17: Given a slot file was hand-edited into invalid JSON, when the Load list opens, then that row
  shows "Corrupted", cannot be loaded, and can be deleted; when F9 targets a corrupted quicksave, then an
  error toast appears and the current game continues untouched.
- [ ] AC 18: Given a save whose `version` differs from `SAVE_FORMAT_VERSION`, when loading it, then it is
  refused with "incompatible version" and the current game is untouched.
- [ ] AC 19: Given a save references an `itemId` no longer in the catalog, when it is loaded, then the load
  completes, that item is skipped, and a warning is logged naming the ID.

**Menu**
- [ ] AC 20: Given gameplay with no tab open, when Esc is pressed, then the Game Menu opens, the world is
  paused (`Time.timeScale == 0`) and the cursor is unlocked; when Resume or Esc is pressed, then the game
  resumes and the cursor locks.
- [ ] AC 21: Given a tab (e.g. Inventory) is open, when Esc is pressed, then only the tab closes; given a
  dialogue / container / trade window is open, when Esc is pressed, then only that window closes and the
  Game Menu does not open.
- [ ] AC 22: Given the Game Menu's Save Game panel, when a used manual slot is clicked, then an overwrite
  confirmation appears and the slot is overwritten only on Yes; the row then shows the new timestamp.
- [ ] AC 23: Given the Load Game panel, then Quicksave, Autosave and all manual slots are listed with
  region, level, date/time and playtime (or "Empty"); Delete removes the file after confirmation.
- [ ] AC 24: Given the tab bar, then it shows Inventory, Quests, Stats and Skills only; Options is reachable
  from the Game Menu and Back returns to the main buttons.

**Death**
- [ ] AC 25: Given the player's health reaches 0, when they die, then the Player GO stays active, input
  can't move / attack / dodge, enemies stop targeting them, and after ~2 s the death screen appears with
  the world paused.
- [ ] AC 26: Given the death screen with at least one valid save, when "Load last save" is clicked, then
  the most recent valid save (by timestamp, any slot) is loaded and the player is alive with the saved
  health; "Load quicksave" is shown only when a valid quicksave exists.
- [ ] AC 27: Given the player dies with no valid save at all, then the death screen shows Restart; clicking
  it restores the session's initial state (start-of-session facts, player and region) without writing any
  file.

**Editor**
- [ ] AC 28: Given two scene containers share a `_saveId` (Ctrl+D duplicate), when the scene is saved or
  `Tools/Save/Validate Save IDs` runs, then a duplicate error names both objects, and the validator can
  regenerate the second one's ID.
- [ ] AC 29: Given a new `ItemSO` asset is created, when it is imported, then it receives an `itemId` and
  appears in `ItemCatalog.asset`; a duplicated item asset with the same ID logs an error naming both paths.

## Additional Context

### Dependencies

- `com.unity.nuget.newtonsoft-json` 3.2.1 (already in the package graph; made explicit by Task 1).
- No dependency on other open specs. Absorbs `tech-spec-lockable-persistence-stub.md`.
- Steam Cloud (Epic 8) will later sync `Application.persistentDataPath/Saves/` — keep all saves in that one
  folder, no absolute paths in the files.

### Testing Strategy

**EditMode (NUnit, `Assets/Tests/EditMode/`, existing style: `AddComponent` + reflection for private
`[SerializeField]`s, `ScriptableObject.CreateInstance`, `DestroyImmediate` in `TearDown`):**
- `SaveDataSerializationTests` — a fully populated `SaveData` round-trips through
  `JsonConvert` unchanged (facts dictionary, nullable fields, `SVector3` / `SQuaternion`); the JSON has
  no Unity types.
- `SaveFileStoreTests` — write / read / delete / list in a temp folder (make `FolderPath` overridable via
  an `internal static` test hook); corrupted JSON → `isValid = false`; wrong version → refused; missing
  file → "No save"; `MostRecentValid` picks the newest.
- `ItemCatalogTests` — `FindById` hit / miss / null entries / duplicate warning.
- `ItemStackConverterTests` — unknown ID skipped, counts clamped, order preserved.
- `WorldStateManagerFactsTests` (extend) — `RestoreFacts` replaces the dictionary and raises no
  `_onFactChanged`; `CaptureFacts` returns a copy.
- `PlayerRestoreTests` — `RestoreBaseStats`, `RestoreLevel`, `RestoreLP`, `RestoreSkills`, `RestoreSlots`,
  `RestoreEquipped`, `RestoreGold`, `RestoreHealth` set state and raise only the documented events
  (subscribe test listeners to `_onLevelUp` / `_onSkillLearned` and assert they stay silent).
- `EquipmentSystemTests` (extend) — `RestoreEquipped` does not touch the inventory and applies stat
  bonuses.
- `QuestEventsManager` reseed — after `RestoreFacts` + `ReseedState`, a later unrelated fact change raises
  no quest events.
- Update any test that asserts `ScreenTab.Options` or `PlayerHealth` deactivating the GO
  (grep `Assets/Tests/` for `ScreenTab.Options`, `SetActive(false)` on the player, `activeSelf`).

**Manual Play-Mode checklist (StartingTown):** walk through AC 1, 4–9, 11, 13–15, 20–27 in order; inspect
`%USERPROFILE%/AppData/LocalLow/<company>/<product>/Saves/` to verify file names, temp-file cleanup and
readable JSON; hand-corrupt one file for AC 17.

### Notes

**High-risk items (pre-mortem):**
- **Event re-raising on restore** — the biggest risk is a restore path that reuses `AddItem`,
  `LearnSkill`, `GiveExperience`, `Equip` or `RegisterKill` and silently grants LP / XP / rewards or
  duplicates items. Restore methods must write state directly (Phase B rule).
- **Ordering with `PersistentID.Start()`** — object restore must run after it (that's why
  `RegionLoaded` fires one frame after the load op). If restore ran first, a corpse re-activated by
  `SaveableObject` would be hidden again.
- **Esc double-handling** — several UIs subscribe to `UI.Cancel`; the `_wasGameplayLastFrame` sample is
  what keeps one Esc press from closing a window *and* opening the Game Menu.
- **`Time.timeScale = 0` leaks** — every exit path (Resume, load, Restart, Quit-cancel) must restore it;
  `FinishLoad` always forces `1`. Coroutines used while paused must use realtime waits.
- **Prefab-baked save IDs** — a `_saveId` set on a prefab asset is shared by every instance; the inspector
  warns and the validator catches duplicates.
- **Equipment not in inventory** — `Equip` removes the item from the inventory list; saving inventory and
  equipment separately is correct, and restoring through `Equip()` would be wrong.

**Known limitations:**
- Living NPCs / monsters reset to spawn position and AI state on load; only alive / dead, inventory and
  gold persist.
- Corpses are restored by enabling the ragdoll at the saved root position, so the body re-settles rather
  than keeping its exact limb pose.
- Restart (no-save death) restores the in-memory start-of-session snapshot; there is no title-screen
  New Game yet.
- No save migration: bumping `SAVE_FORMAT_VERSION` invalidates older saves.
- Playtime counts unpaused real time from the start of the session or loaded save.

**Future considerations:** Steam Cloud sync, main menu with Continue / New Game, save thumbnails,
per-region object-state pruning, migrating saves between format versions, persisting NPC schedules once
day/night exists.
