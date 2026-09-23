# CLAUDE.md — Assets/_Game/Scripts/Debug

> Loaded when Claude accesses files in this folder.

---

## Namespace Rule

`Game.DevTools` is the correct namespace for all files in this folder.

`Game.Debug` is **banned** — it shadows `UnityEngine.Debug` globally and breaks `Debug.Log`, `Debug.DrawLine`, etc. across the entire codebase.

---

## Test Scaffolding — EnemyRespawner (Story 3.1)

`EnemyRespawner.cs` (namespace `Game.DevTools`) is attached to `ProgressionSystem` in TestScene. It re-enables dead entities after a configurable delay (default 5s). `EntityHealth.OnEnable()` resets `IsDead` and `CurrentHealth` on reactivation.

**This is test scaffolding — superseded by Story 5-5 (no-enemy-respawn design).**
