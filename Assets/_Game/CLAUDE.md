# CLAUDE.md — Assets/_Game

> Loaded when Claude accesses files in this folder. Covers assembly setup and the InputSystem_Actions dual-file contract.

---

## Assembly Setup

`Game.asmdef` exists with:
- `"name": "Game"`, `"autoReferenced": true`
- `"references": ["Unity.InputSystem"]`

All scripts under `Assets/_Game/` compile into the **`Game` assembly** (not `Assembly-CSharp`).
`InputSystem_Actions.cs` was moved from `Assets/` root into `Assets/_Game/` so it compiles into `Game`.

**Consequence:** Any future auto-generated file placed at `Assets/` root will be in `Assembly-CSharp` and invisible to `Game` scripts — always move such files inside `Assets/_Game/`.

`Tests.EditMode.asmdef` explicitly references `"Game"` in its references array.

---

## InputSystem_Actions.cs Embeds the Full JSON (Critical)

`InputSystem_Actions.cs` is **not** just a wrapper that reads from `InputSystem_Actions.inputactions` at runtime — it **embeds the entire action map JSON as a string literal** inside the constructor. The `.inputactions` file is only used by Unity's Input Actions editor UI.

**Consequence:** When adding new actions (e.g. Block), you MUST edit **both** files:
1. `InputSystem_Actions.inputactions` — for Unity's editor and future regeneration
2. `InputSystem_Actions.cs` embedded JSON (uses `""` double-escaped quotes) — this is what actually runs

If you only edit `.inputactions`, the runtime `FindAction("Block", throwIfNotFound: true)` will throw `ArgumentException` because the embedded JSON doesn't have the new action.

---

## Code Review Checklist — Assets/_Game

| Severity | Pattern |
|----------|---------|
| HIGH | Auto-generated files (e.g. `InputSystem_Actions.cs`) left in `Assets/` root after adding a named asmdef — named assemblies can't see `Assembly-CSharp`; move them inside `Assets/_Game/` |
| MEDIUM | `Keyboard.current` / `Mouse.current` used instead of `InputSystem_Actions` action map — see `Scripts/Player/CLAUDE.md` for action map layout |
