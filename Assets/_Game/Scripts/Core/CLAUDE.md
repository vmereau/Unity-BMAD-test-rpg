# CLAUDE.md — Assets/_Game/Scripts/Core

> Loaded when Claude accesses files in this folder. Project-wide singletons and
> utilities every other system depends on. Namespace: `Game.Core`.

---

## What's here

| File | Role |
|------|------|
| `CursorManager` | **Static.** The ONLY place allowed to touch `Cursor.lockState` / `Cursor.visible`. Use `Lock()` / `Unlock()` / `IsLocked` everywhere else. |
| `GameLog` | **Static.** Project logging wrapper. `Info`/`Warn` stripped in Release; `Error` always writes. Never call `Debug.Log` directly — always `GameLog.*` with a `TAG`. |
| `GameConstants` | **Static.** Compile-time *structural* constants only. Tunable gameplay values belong in config SOs, not here. |
| `SceneLoader` | Scene/additive-scene loading. |
| `State/WorldStateManager` | Central runtime state singleton (on the `WorldStateManager` GO in `Core.unity`). Kill tracking + flat key/bool world-fact store backed by typed Fact SOs. Save/Load is Epic 8. |
| `State/WorldFactPrefix` | Canonical key prefixes (`enum`). Use the typed setters (`RegisterKill`, `SetQuestStep`, `SetWorldEvent`) — never build fact-key strings by hand at call sites. |

---

## Rules (HIGH — enforced project-wide)

- **All cursor changes go through `CursorManager`.** Direct `Cursor.*` / `CursorLockMode` use anywhere else is a HIGH review finding.
- **All logging goes through `GameLog`.** Direct `Debug.Log/LogWarning/LogError` is a finding.
- **World-fact keys are built only via `WorldStateManager`'s typed setters** + `WorldFactPrefix` — no manual string concatenation.
