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
| `GameEventBus`, `AudioManager` | Empty stubs — scripts arrive with their epics |

**Active scene / `RenderSettings`:** no code calls `SetActiveScene`, so in a build Core stays active and its Lighting
settings (skybox: built-in Default-Skybox) apply; in the editor the active scene is whichever you made active (often
StartingTown, skybox `SkyboxLite`). Lighting set in a region's Lighting window is therefore editor-only — tune the
look through `DayNightConfig` / Core instead.

`Core.unity` also contains a `Player.prefab` instance — the `PlayerStats` / `PlayerSkills` references in
the scene YAML are that instance's components, not separate scene components.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
