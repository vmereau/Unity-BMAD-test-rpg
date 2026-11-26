# CLAUDE.md — Assets/_Game/Scenes

> Loaded when Claude accesses files in this folder. Covers the boot flow, `Core.unity` managers, the active scene,
> the MCP scene-loading quirk and a StartingTown overview (site details live in `STARTING_TOWN.md`).

---

## Topic files

| File | Read when |
|------|-----------|
| `STARTING_TOWN.md` | Before placing, moving or wiring anything in StartingTown (per-site layout facts, hand-owned roots, constraints) |
| `../Editor/World/CLAUDE.md` | Before running any Area 1 tool (`Tools/World/...`): terrain, Camp Edge, Sites, hand edits, grading, one-shot authoring, rebake |

---

## Boot flow (MainMenu → Core)

Build order: `MainMenu` (0), `Core` (1), `StartingTown` (2). `MainMenu.unity` holds only a camera (solid near-black,
culling mask Nothing), an `EventSystem` (`InputSystemUIInputModule`) and `MainMenuCanvas` (`MainMenuUI`, `Window` with
`MainButtons` / `SaveSlotList` / `OriginPick` (`OriginPickUI`, inactive until New Game) / `BackButton`, `ConfirmDialog`).

- New Game opens the origin pick; Begin = `GameSession.SetOrigin(id)` + `LoadScene(Core, Single)`, so `SceneLoader` loads
  the startup region as before.
- Continue / Load Game set `GameSession.RequestLoad(slot)` first: `SceneLoader.Awake` sees it and skips the startup region,
  `SaveSystem.Start` takes it and loads the save's region directly (an unreadable file falls back to the New Game boot
  plus a toast).
- **Pressing Play with `Core` open skips the menu** (no request = today's flow, default origin Wretch).
- The origin is applied by `SaveSystem.HandleRegionLoaded` on the startup (new-game) region, before the new-game snapshot
  (spec-player-origins).
- There's no way back to the menu from a running game yet (`SaveSystem` / `WorldStateManager` are `DontDestroyOnLoad`).

**Editor play scenarios (spec-play-scenarios):** a `Game/Play Scenario` dropdown on the main toolbar (and `Tools/Play
Scenario/...`; `Editor/PlayScenarios/PlayScenarioToolbar`) picks what Play starts, saved per machine in EditorPrefs:

- **Open Scenes** (default: no start scene, the scene's `ArrivalSequence._devStart` applies)
- **Main Menu** (`playModeStartScene` = MainMenu, arrival forced to play)
- **Arrival (Wretch)** / **After Arrival (Wretch)** (start scene Core, origin Wretch, arrival forced to play / `SkipAll`)

`playModeStartScene` isn't persisted, so it's reapplied on every editor load. The game reads the choice through
`Game.Core.PlayScenarios` (editor Play only; builds = Open Scenes).

---

## Core.unity Manager GameObjects

Manager GameObjects in `Core.unity`:
`WorldStateManager`, `GameEventBus`, `SaveSystem`, `SceneLoader`, `GameClock`, `DayNightController`, `AudioManager`,
`StealthDebugOverlay`

| GO | Status |
|----|--------|
| `WorldStateManager` | Scripted — `Game.Core.State.WorldStateManager` |
| `SceneLoader` | Scripted — `Game.Core.SceneLoader` |
| `SaveSystem` | Scripted — `Game.Core.SaveSystem` (refs wired to the Player instance's components and `UICanvas/ConfirmDialog`; see `Scripts/Core/Save/CLAUDE.md`) |
| `GameClock` | Scripted — `Game.Core.GameClock` (world time; `TimeConfig`, `OnHourChanged`, `OnTimeSkipped`). Referenced by `SaveSystem` and the Player instance's `SleepSystem` / `StatusEffectHost` (scene overrides — `Player.prefab` itself has no clock, so beds refuse with a warning in scenes without one, e.g. `TestScene`) |
| `DayNightController` | Scripted — `Game.Core.DayNightController` (`GameClock`, `DayNightConfig`, children `Sun` / `Moon` directional lights). **Region scenes must not contain a directional light** — the sun lives here (StartingTown's was removed). `TestScene` keeps its own (it runs without Core) |
| `AudioManager` | Scripted — `MusicPlayer` + `AudioSource` (Output = `MainMixer/Music`), `SfxPlayer` (pooled one-shots), `AudioMixerVolumes`; see `Scripts/Audio/CLAUDE.md` |
| `StealthDebugOverlay` | Scripted — `Game.DevTools.StealthDebugOverlay` (F3 overlay); child `StealthDebugCanvas` (Screen Space Overlay canvas, sort order 500, `Summary` text) is its `_canvas`; see `Scripts/Stealth/CLAUDE.md` |
| `GameEventBus` | Empty stub |

**Active scene / `RenderSettings`:** no code calls `SetActiveScene`, so in a build Core stays active and its Lighting
settings (skybox: built-in Default-Skybox) apply; in the editor the active scene is whichever you made active (often
StartingTown, skybox `SkyboxLite`). Lighting set in a region's Lighting window is therefore editor-only — tune the
look through `DayNightConfig` / Core instead.

`Core.unity` also contains a `Player.prefab` instance — the `PlayerStats` / `PlayerSkills` references in
the scene YAML are that instance's components, not separate scene components.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in
sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call
`refresh_unity`.

---

## StartingTown = Alderath Camp (Area 1 graybox)

> **Read `STARTING_TOWN.md` before placing or moving anything in StartingTown; read `Editor/World/CLAUDE.md` before
> running an Area 1 tool.** Tool-built roots under `Area1_Blockout` are replaced on every run.

The scene keeps the name `StartingTown` (`SceneLoader`, `GameConstants`, save data use it) but is the Company Encampment.
New props sit under `Camp/<Zone>`. Design source: `docs/World/Areas/area-001-company-encampment.md`.

| Zone | Where (x, z) | Notes |
|------|--------------|-------|
| `CompanyHouse` | Outcrop y≈10, (0..30, −33..−45) | Hall + flags; `House` (lockable, door faces the camp) is the Warden's office; ramp on the north side |
| Company yard | South of the inner fence | Chests, Storekeeper's table; the store building and Edric's forge are in the Timberworks |
| `InnerFence` | z≈9, gate at x≈0 | Only the two `SM_Gate_Side_C` posts remain. The tall inner palisade is in `CampEdge`, and `InnerGate` holds the leaves and guard hut. Guard Harl stands in the gap |
| `Area1_Blockout/TentCamp/Canteen` | (−15.75, 20) | The cookhouse (kitchen + restricted store), cook porch with `CookingStation_Fireplace`, mess shelter and back alley: an instance of the generated `Prefabs/World/Cookhouse/Cookhouse.prefab` plus `Dressing`, placed by `Tools/World/Place Area 1 Canteen` (that folder's `CLAUDE.md`); see `STARTING_TOWN.md` → Canteen. `Camp/Canteen` is now empty |
| `Area1_Blockout/TentCamp`, `CampCentre` | Worker half, (−25..40, 10..48) | Hand-owned timber housing and tool-built square; see `STARTING_TOWN.md` → Worker settlement and Home yard |
| `Area1_Blockout/CampEdge` | Ring at about 50 m | Graybox palisade from `Tools/World/Build Area 1 Camp Edge`; see `STARTING_TOWN.md` → Camp edge |
| `Area1_Blockout/Quarry` | Cleft at (255, −125) | Site roots from `Tools/World/Build Area 1 Sites`; see `STARTING_TOWN.md` → Quarry and Ruins 2 |
| `Area1_Blockout/Fort`, `RampGate`, `CampEdge/RampRail` | Fort hill, (0..50, −31..−71) | Same tool; see `STARTING_TOWN.md` → Fort hill |
| `Area1_Blockout/Docks`, `BridgeRails`, `Barricades`, `HuntersFire` | Camp side | Same tool; see `STARTING_TOWN.md` → Camp sites |
| `Area1_Blockout/FightPit` | Lake side | Hand-owned, no tool touches it; see `STARTING_TOWN.md` → Lakeside |
| `Area1_Blockout/GangHouse`, `GangDock`, `CampEdge/ShoreRail` | Lake side | Same tools; see `STARTING_TOWN.md` → Lakeside |
| `Area1_Blockout/InnerGate`, `Warehouse`, `DockGate`, `CompoundGate`, `StagingYard`, `GuardPost`, `WorkGangTents`, `Latrine`, `TimberYard`, `WorkshopYard`, `Garrison`, `Walkways` | Camp interior | Same tool; see `STARTING_TOWN.md` → Camp interior, Company compound, Stoneworks, Timberworks, Garrison quarter |
| `Area1_Blockout/Farm` | Plain south of the fort hill, (−48..44, −50..−148) | Same tool; see `STARTING_TOWN.md` → Farm |
| `Area1_Blockout/Ruins`, `QuarryDressing`, `Ruins2Dressing`, `WoodCamp`, `HuntingCamp`, `Cave` | Remote | Same tool; see `STARTING_TOWN.md` → Remote sites |
| `RestrictedAreas` | Scene root (hand-owned) | `RA_Warehouse` / `RA_Docks` / `RA_CookhouseStore` volumes; guard waypoints under `NpcPatrolRoutes/<NPC>`, routine spots under `NpcRoutineSpots`. Keep all three out of `Area1_Blockout`; see `STARTING_TOWN.md` → Restricted areas |

Other hand-owned scene roots (no tool touches them): `WorkSiteManagers`, `SealedPassage`, `HiddenStashQuest`,
`ArrivalSequence` — see `STARTING_TOWN.md`.

Enemies live outside the basin: spiders in and at the mouth of the tutorial cave near (85, −45), bandit patrol around
(−110, 30). They aren't tool-owned: a Sites run leaves them alone, and moving a site doesn't move them.
