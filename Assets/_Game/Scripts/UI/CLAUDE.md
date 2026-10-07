# CLAUDE.md — Assets/_Game/Scripts/UI

> Loaded when Claude accesses files in this folder. Project rules shared by all UI scripts.
> Sub-folder CLAUDE.md files cover each UI subsystem; the `UICanvas` hierarchy is in `Prefabs/UI/CLAUDE.md`.

---

## Sub-folder Index

| Folder | What's inside |
|--------|--------------|
| `HUD/` | Health / stamina / XP bars, action bar, interaction prompt card, notification toasts |
| `Inventory/` | Inventory grid, item detail panel + formatter, equipment, container (incl. loot) and trade screens |
| `Quest/` | Quest log screen, quest list / info panels |
| `Skills/` | Skills tab: skill list, skill detail panel + formatter |
| `Dialogue/` | NPC dialogue panel, topic / choice display, keyboard shortcuts |
| `Screens/` | `UIScreenManager`, `IScreenPanel` contract, character stats, options |

`EntityUI` (this folder) is the world-space name / HP display shown when an entity is hovered.

---

## Canvas

- `UICanvas` (nested in `Player.prefab`) is **Screen Space - Overlay with no `CanvasScaler`** — HUD and menu
  elements use fixed pixel sizes. World Space only for diegetic UI (`EntityUI`).
- Frequently changing elements get their **own nested Canvas** (e.g. `InteractionPrompt`) so they don't
  rebuild the whole `UICanvas`.
- Creating a Canvas via MCP: see the `renderMode` quirk in the root `CLAUDE.md`.

---

## Cursor (HIGH)

Never touch `Cursor.lockState` / `Cursor.visible` / `CursorLockMode` — use `CursorManager.Lock()` /
`Unlock()` / `IsLocked`. Panels: `OnScreenOpen()` → `Unlock()`, `OnScreenClose()` → `Lock()`.

---

## Input

UI scripts that own an `InputSystem_Actions` (`UIScreenManager`, `DialogueUI`, `ContainerUI`,
`NPCTradeUI`) follow one lifecycle:

- create it in `Awake`; enable maps + subscribe in `OnEnable`; unsubscribe in `OnDisable`;
  **`Dispose()` in `OnDestroy`** so input survives disable/re-enable cycles;
- guard both `OnEnable` and `OnDisable` with `if (_input == null) return;` (the root `CLAUDE.md` lifecycle
  gotcha — `Awake` may bail out or disable the component first).

Action names (Cancel, Click, DialogueOption*, toggles): `Assets/_Game/CLAUDE.md`.

---

## Events & Updates

- Subscribe to `GameEventSO<T>` channels in `OnEnable` / unsubscribe in `OnDisable` — never in `Start`
  (missed first raises) and never poll game state in `Update`.
- Drag & drop ghosts are parented to the root Canvas with `raycastTarget = false` (otherwise they block the
  drop target) and destroyed by whichever of `OnEndDrag` / `OnDrop` runs, null-guarded.
- Text is `TMP_Text` only.

---

## Code Review Checklist — UI Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `Cursor.lockState` / `Cursor.visible` used directly — must go through `CursorManager` |
| HIGH | `_input` used in `OnEnable` / `OnDisable` without the null guard |
| HIGH | Drag ghost missing `raycastTarget = false` |
| MEDIUM | `_input.Dispose()` in `OnDisable` instead of `OnDestroy` — input dead after the first re-enable |
| MEDIUM | `CanvasScaler` added to `UICanvas` — every fixed-pixel HUD layout would rescale |
| MEDIUM | `GetComponent*` in `Update` — cache in `Awake` |
| MEDIUM | Game-state polling in `Update` instead of a `GameEventSO` subscription |
| LOW | `new WaitForSeconds()` inside fade coroutines — cache instances |
