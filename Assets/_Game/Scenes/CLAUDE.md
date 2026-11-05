# CLAUDE.md — Assets/_Game/Scenes

> Loaded when Claude accesses files in this folder.

---

## Core.unity Manager GameObjects

Manager GameObjects in `Core.unity`:
`WorldStateManager`, `GameEventBus`, `SaveSystem`, `SceneLoader`, `DayNightController`, `AudioManager`

| GO | Status |
|----|--------|
| `WorldStateManager` | Scripted — `Game.Core.State.WorldStateManager` |
| `SceneLoader` | Scripted — `Game.Core.SceneLoader` |
| `SaveSystem` | Scripted — `Game.Core.SaveSystem` (refs wired to the Player instance's components and `UICanvas/ConfirmDialog`; see `Scripts/Core/CLAUDE.md` → Save) |
| `GameClock` | Scripted — `Game.Core.GameClock` (world time; `TimeConfig`, `OnHourChanged`, `OnTimeSkipped`). Referenced by `SaveSystem` and the Player instance's `SleepSystem` / `StatusEffectHost` (scene overrides — `Player.prefab` itself has no clock, so beds refuse with a warning in scenes without one, e.g. `TestScene`) |
| `DayNightController` | Scripted — `Game.Core.DayNightController` (`GameClock`, `DayNightConfig`, children `Sun` / `Moon` directional lights). **Region scenes must not contain a directional light** — the sun lives here (StartingTown's was removed). `TestScene` keeps its own (it runs without Core) |
| `AudioManager` | Scripted — `MusicPlayer` + `AudioSource` (Output = `MainMixer/Music`), `SfxPlayer` (pooled one-shots), `AudioMixerVolumes`; see `Scripts/Audio/CLAUDE.md` |
| `GameEventBus` | Empty stub |

**Active scene / `RenderSettings`:** no code calls `SetActiveScene`, so in a build Core stays active and its Lighting
settings (skybox: built-in Default-Skybox) apply; in the editor the active scene is whichever you made active (often
StartingTown, skybox `SkyboxLite`). Lighting set in a region's Lighting window is therefore editor-only — tune the
look through `DayNightConfig` / Core instead.

`Core.unity` also contains a `Player.prefab` instance — the `PlayerStats` / `PlayerSkills` references in
the scene YAML are that instance's components, not separate scene components.

---

## StartingTown = Alderath Camp (Area 1 graybox)

The scene keeps the name `StartingTown` (`SceneLoader`, `GameConstants`, save data use it) but is the Company Encampment.
New props sit under `Camp/<Zone>`. Design source: `docs/World/Areas/area-001-company-encampment.md`.

| Zone | Where (x, z) | Notes |
|------|--------------|-------|
| `CompanyHouse` | Outcrop y≈10, (0..30, −33..−45) | Hall + flags; `House` (lockable, door faces the camp) is the Warden's office; ramp on the north side |
| Company yard | South of the inner fence | Forge, anvil, chests, archery targets, `Village house (1)` as the store |
| `InnerFence` | z≈9, gate gap at x≈0 | Open gap; Guard Harl stands inside it |
| `CenterStage` | (0, 16) | Platform, punishment post, flags |
| `Canteen` | `Village house` (−14, 22) | Cook, `CookingStation_Fireplace` outside |
| `PlayersZone` | (8..22, 20..34) | Tents, `Bed`, `PlayerSpawnPoint`, Old Hobb, Tam |
| `WretchesCorner` | (−8..6, 30..40) | Smaller tents |
| `MafiaZone` | (24..36, 12..24) | Larger tents, for the stash owner later |
| `Area1_Blockout/CampEdge` | Ring at about 50 m | Graybox palisade from `Tools/World/Build Area 1 Camp Edge`; see below |
| `Area1_Blockout/Quarry` | Cleft at (255, −125) | Site roots from `Tools/World/Build Area 1 Sites`; see below |
| `Area1_Blockout/Fort`, `Docks`, `BridgeRails`, `Barricades`, `Hollow`, `HuntersFire` | Camp side | Same tool; see **Camp sites** below |

Enemies live outside the basin: spiders around (62, −35), bandits around (−70, 60). Moving them is in the deferred remote-sites work.

**Valley terrain:** 1000 m square (1025 heightmap, about 1 m per sample), origin (−500, −500), with the object at y = −30 and heights +30 so ground can sit below y = 0. The sculpt, paint and forest come from `Tools/World/Build Area 1 Terrain` (`Editor/World/Area1TerrainBuilder`). Sites, river, lake, ford and roads are read from `docs/World/Areas/area-001/layout.json`; ridges, pad heights and the camp footprint are constants in the tool, so moving the ruins or quarry far means moving their ridge too. Every run rewrites `StartingTownTerrain.asset` but keeps hand edits stored in `docs/World/Areas/area-001/terrain-sculpt.bin`: after sculpting, run `Tools/World/Capture Area 1 Terrain Sculpt`. The tool refuses to run while the terrain has uncaptured edits. The camp's ground, paint and holes come from `Art/Terrain/StartingTownTerrain_Source.asset` (the original 500 m terrain): don't delete it, and make lasting camp ground edits there. Water is one `Water_Volume` covering the valley, top at y = −2. Any non-water ground must stay above that, and `NotWalkable_West` / `NotWalkable_East` leave x 31..39 open for the ford. `Area1_Blockout` holds the pass blocker and the waterfall placeholder. Rebake the NavMesh after a re-run.

**Camp edge:** `Tools/World/Build Area 1 Camp Edge` (`Editor/World/Area1BlockoutBuilder`) reads the `outer-fence*` features of `layout.json` and replaces only `Area1_Blockout/CampEdge`; re-run it after any terrain re-run, because panels follow the ground, then rebake. The ring has four openings: arrival (−38.4, 36.3, open towers, no blocker: `PassBlocker` is the story lock), wood (−51, 5.6), quarry (55.8, −5.8) and wilderness (32.7, 51.8). The docks are outside the ring, reached through the wilderness opening. `BrokenStretch` (x −1..7 on the north fence) keeps the hunter's fire in view from the tents. Its blocker uses a collider on `Ignore Raycast`, which the player still hits and sight rays and the camera skip. Scene bakes collect render meshes, so an invisible collider never blocks the NavMesh: add a Not Walkable `NavMeshModifierVolume` where agents could reach it.

**Camp sites:** `Tools/World/Build Area 1 Sites` also replaces `Fort`, `Docks`, `BridgeRails`, `Barricades`, `Hollow` and `HuntersFire`, and never touches the hand-built `CampBridge`, `CompanyCliff` or the root `SM_Barricade_*`. In `Docks` it replaces only its own parts, by name (`DeckFloor_*`, `Boat`, `MooringPost`, `MooringRope`): the two hand-built `SM_WoodPlatformFloorKit_01a` deck pieces live there and stay. Rerun it if those move. `Fort` is a placeholder from the `fort-wall-*` and `fort-tower` features of `layout.json`: each wall line ends inside something solid (the tower, the `House`, `SM_Ruin_01`, `SM_Ruin_04`), the ruin pillars are the gate, and the lines stay inset from the rim, because a panel's bottom follows the ground and must not reach down into the chamber around (−3, −44). The wall replaced `outcrop-edge`. The deck's colliders sit 0.45 m under the planks with holes at the T junction, so `DeckFloor_*` are collider-only floors for the player. Its planks also have gaps that split a render-mesh bake, and the boards that bridged them were removed by hand: the next bake needs another way to join the deck. `SM_Barricade_*` colliders match their mesh bounds, but the meshes are sparse, so the bake leaves NavMesh inside them; `Barricades/NotWalkable_*` close that. `Hollow` is a rock fold along the south edge of the `hollow` feature, about 6 m tall, tuned to a tower-top eye at y 26.6, so moving `hollow` moves the fold. `HuntersFire` is driven by `ClockNightLight`. A point light only reaches the ground around it, so the 80–90 m read comes from the flame and halo quads, which centre near y 3 because the rim clears the (11, 35) sight line by only 0.4 m. Rebake after a run if a render mesh changed.

**Quarry and Ruins 2:** `Area1TerrainBuilder.Cliffs` raises one rock mass along the quarry's south and east edges and around Ruins 2, up to y 56. Its outer face is steep all the way round, which stops walking and agents. `PlayerController` has no slope slide and allows jumps on steep ground, so a player holding jump can still climb any cliff: `Quarry/RuinsRing` (invisible colliders on `Ignore Raycast`) and the seal's `Cap` are what stop that. The only way in is `quarry-cleft` (`layout.json`), 4 m wide and open to the sky. `Tools/World/Build Area 1 Sites` (`Editor/World/Area1SitesBuilder`) replaces only `Area1_Blockout/Quarry`, so re-run it after any terrain re-run, as the blocker's height comes from the ground. List the `quarry-cleft` points from the quarry to Ruins 2: the blocker and the ring's ends depend on that order. It owns `CleftBlocker`: `FactRemovedObject` hides its `Seal` child while `WorldFact_QuarryPassageOpened` is true. The bake ignores the blocker (`NavMeshModifier`) and a carving `NavMeshObstacle` closes the cleft at runtime, so removing it needs no rebake. That obstacle stays disabled in the scene, because a bake cuts an enabled carving obstacle into the NavMesh even under an ignore modifier; `FactRemovedObject` enables it in `Awake`. Edit-mode paths therefore go through the cleft: test sealing in play mode. Ground slope, not path grade, decides what the player can climb, so check reachability against surface slope.

**Play tests:** set `Application.runInBackground = true` in one call, then test in the next: `Time.frameCount` stays at 2 for the rest of the first call. Walk the player by `CharacterController.Move` along NavMesh corners, and start inside the opening, because camp props snag a simple walker. Include run-offs from high ground as well as pushes at the wall.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
