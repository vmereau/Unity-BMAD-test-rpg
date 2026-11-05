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
| `Area1_Blockout/Ruins`, `QuarryDressing`, `Ruins2Dressing`, `WoodCamp`, `HuntingCamp`, `Cave` | Remote | Same tool; see **Remote sites** below |

Enemies live outside the basin: spiders in and at the mouth of the tutorial cave near (85, −45), bandit patrol around (−110, 30). They aren't tool-owned: a Sites run leaves them alone, and moving a site doesn't move them.

**Valley terrain:** 1000 m square (1025 heightmap, about 1 m per sample), origin (−500, −500), with the object at y = −30 and heights +30 so ground can sit below y = 0. The sculpt, paint and forest come from `Tools/World/Build Area 1 Terrain` (`Editor/World/Area1TerrainBuilder`). Sites, river, lake, ford and roads are read from `docs/World/Areas/area-001/layout.json`; ridges, pad heights and the camp footprint are constants in the tool, so moving the ruins or quarry far means moving their ridge too. Every run rewrites `StartingTownTerrain.asset` but keeps hand edits stored in `docs/World/Areas/area-001/terrain-sculpt.bin`: after sculpting, run `Tools/World/Capture Area 1 Terrain Sculpt`. The tool refuses to run while the terrain has uncaptured edits. The camp's ground, paint and holes come from `Art/Terrain/StartingTownTerrain_Source.asset` (the original 500 m terrain): don't delete it, and make lasting camp ground edits there. Water is one `Water_Volume` covering the valley, top at y = −2. Any non-water ground must stay above that, and `NotWalkable_West` / `NotWalkable_East` leave x 31..39 open for the ford. `Area1_Blockout` holds the pass blocker and the waterfall placeholder. Rebake the NavMesh after a re-run.

**Camp edge:** `Tools/World/Build Area 1 Camp Edge` (`Editor/World/Area1BlockoutBuilder`) reads the `outer-fence*` features of `layout.json` and replaces only `Area1_Blockout/CampEdge`; re-run it after any terrain re-run, because panels follow the ground, then rebake. The ring has four openings: arrival (−38.4, 36.3, open towers, no blocker: `PassBlocker` is the story lock), wood (−51, 5.6), quarry (55.8, −5.8) and wilderness (32.7, 51.8). The docks are outside the ring, reached through the wilderness opening. `BrokenStretch` (x −1..7 on the north fence) keeps the hunter's fire in view from the tents. Its blocker uses a collider on `Ignore Raycast`, which the player still hits and sight rays and the camera skip. Scene bakes collect render meshes, so an invisible collider never blocks the NavMesh: add a Not Walkable `NavMeshModifierVolume` where agents could reach it.

**Camp sites:** `Tools/World/Build Area 1 Sites` also replaces `Fort`, `Docks`, `BridgeRails`, `Barricades`, `Hollow` and `HuntersFire`, and never touches the hand-built `CampBridge`, `CompanyCliff` or the root `SM_Barricade_*`. In `Docks` it replaces only its own parts, by name (`DeckFloor_*`, `Boat`, `MooringPost`, `MooringRope`; never rebuild `Walkway_*`): the two hand-built `SM_WoodPlatformFloorKit_01a` deck pieces live there and stay. Rerun it if those move. `Fort` is a placeholder from the `fort-wall-*` and `fort-tower` features of `layout.json`: each wall line ends inside something solid (the tower, the `House`, `SM_Ruin_01`, `SM_Ruin_04`), the ruin pillars are the gate, and the lines stay inset from the rim, because a panel's bottom follows the ground and must not reach down into the chamber around (−3, −44). The wall replaced `outcrop-edge`. The deck's colliders sit 0.45 m under the planks with holes at the T junction, and its planks have gaps that split a render-mesh bake, so `DeckFloor_*` are the walkable surface for the player and the bake: a box with its `MeshRenderer` on `ShadowsOnly`, which draws only into the shadow map but is collected. A disabled renderer isn't collected. The deck model's own collider rises to y 1.2 at the edges, so a straight walker can snag near the T junction at (7.5, 69.3); agents and the player pass. `SM_Barricade_*` colliders match their mesh bounds, but the meshes are sparse, so the bake leaves NavMesh inside them; `Barricades/NotWalkable_*` close that. `Hollow` is a rock fold along the south edge of the `hollow` feature, about 6 m tall, tuned to a tower-top eye at y 26.6, so moving `hollow` moves the fold. `HuntersFire` is driven by `ClockNightLight`. A point light only reaches the ground around it, so the 80–90 m read comes from the flame and halo quads, which centre near y 3 because the rim clears the (11, 35) sight line by only 0.4 m. Rebake after a run if a render mesh changed.

**Remote sites:** the same tool builds them from `Editor/World/Area1RemoteSites`. `Ruins` centres on the `ruins-arch` feature (127, 146). The arch sits 2 m north of that and turns square to the tents, so its opening reads from there and (127, 143) stays walkable. Measured from the tents' eye (15, 2.17, 28), the palisade hides y 40–44 at x 118–138 and the lip is y 35, so an 18 m tower shows its top 9–13 m. Foliage partly screens the lower piece at x 140. `Cave` is a box chamber on the ground, with its mouth towards the camp and rock meshes outside it; the rocks stay clear of the interior at any yaw, and terrain holes aren't used. `Ruins2Dressing` stays within 10 m of the centre, where the pad is flat. `RuinsRing`'s north wall runs along z ≈ −118.6, so test the ring from outside it. The `Bandit` waypoints are wired into the child `bandit`'s `EntityBrain._waypoints`; the nearest is about 13 m from the wood road, and it detects at 8 m. A run replaces these roots whole, with no undo, so move any art out of a root and take that root out of the tool before rerunning. Walk times between all sites are in `_bmad-output/implementation-artifacts/spec-area-1-blockout-remote-sites.md`.

**Quarry and Ruins 2:** `Area1TerrainBuilder.Cliffs` raises one rock mass along the quarry's south and east edges and around Ruins 2, up to y 56. Its outer face is steep all the way round, which stops walking and agents. `PlayerController` has no slope slide and allows jumps on steep ground, so a player holding jump can still climb any cliff: `Quarry/RuinsRing` (invisible colliders on `Ignore Raycast`) and the seal's `Cap` are what stop that. The only way in is `quarry-cleft` (`layout.json`), 4 m wide and open to the sky. `Tools/World/Build Area 1 Sites` (`Editor/World/Area1SitesBuilder`) replaces only `Area1_Blockout/Quarry`, so re-run it after any terrain re-run, as the blocker's height comes from the ground. List the `quarry-cleft` points from the quarry to Ruins 2: the blocker and the ring's ends depend on that order. It owns `CleftBlocker`: `FactRemovedObject` hides its `Seal` child while `WorldFact_QuarryPassageOpened` is true. The bake ignores the blocker (`NavMeshModifier`) and a carving `NavMeshObstacle` closes the cleft at runtime, so removing it needs no rebake. That obstacle stays disabled in the scene, because a bake cuts an enabled carving obstacle into the NavMesh even under an ignore modifier; `FactRemovedObject` enables it in `Awake`. Edit-mode paths therefore go through the cleft: test sealing in play mode. Ground slope, not path grade, decides what the player can climb, so check reachability against surface slope.

**Play tests:** set `Application.runInBackground = true` in one call, then test in the next: `Time.frameCount` stays at 2 for the rest of the first call. Walk the player by `CharacterController.Move` along NavMesh corners, and start inside the opening, because camp props snag a simple walker. Include run-offs from high ground as well as pushes at the wall.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
