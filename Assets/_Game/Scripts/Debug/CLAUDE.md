# CLAUDE.md — Assets/_Game/Scripts/Debug

> Loaded when Claude accesses files in this folder.

---

## Namespace Rule

`Game.DevTools` is the correct namespace for all files in this folder.

`Game.Debug` is **banned** — it shadows `UnityEngine.Debug` globally and breaks `Debug.Log`, `Debug.DrawLine`, etc. across the entire codebase.

---

## StealthDebugOverlay (F3)

`StealthDebugOverlay` (GO in **`Core.unity`**) — runtime detection overlay, toggled by the
`ToggleStealthDebug` action (F3). Gated by `Debug.isDebugBuild` (editor + development builds; disabled and
its canvas hidden in release). Draws cones / LOS / last seen with GL lines in
`RenderPipelineManager.endCameraRendering` (gizmos don't render in builds) using `M_DebugLines.mat`
(`Hidden/Internal-Colored`, referenced so builds include the shader), pooled `StealthDebugLabel` TMP labels and
a summary panel. Allocation-free: numbers are formatted by hand into a cached `StringBuilder`
(`StringBuilder.Append(int/float)` allocates on Mono).

---

## Test Scaffolding — EnemyRespawner (Story 3.1)

`EnemyRespawner.cs` (namespace `Game.DevTools`) is attached to `ProgressionSystem` in TestScene. It re-enables dead entities after a configurable delay (default 5s). `EntityHealth.OnEnable()` resets `IsDead` and `CurrentHealth` on reactivation.

**This is test scaffolding — superseded by Story 5-5 (no-enemy-respawn design).**
