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
| `GameEventBus`, `SaveSystem`, `DayNightController`, `AudioManager` | Empty stubs — scripts arrive with their epics (save/load = story 9-5, day/night = story 5-6) |

`Core.unity` also hosts `PlayerStats` and `PlayerSkills` components.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
