# CLAUDE.md — Assets/_Game/Scenes

> Loaded when Claude accesses files in this folder.

---

## Core.unity Scene Stubs (Complete as of Story 1.5)

All 7 manager stub GameObjects are in `Core.unity`:
`WorldStateManager`, `GameEventBus`, `SaveSystem`, `SceneLoader`, `DayNightController`, `AudioManager`, `UI`

These are empty GameObjects only — no scripts yet. Scripts are added per-epic.

---

## MCP Tool Quirk — Scene Loading

**`manage_scene(action="load")`** only resolves scene names at `Assets/{name}.unity`. It **cannot** load scenes in sub-folders (e.g. `Assets/_Game/Scenes/Core.unity`). Edit `.unity` files directly for sub-folder scenes, then call `refresh_unity`.
