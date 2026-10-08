# CLAUDE.md — Assets/_Game/Scripts/UI/Dialogue

> NPC dialogue overlay. Rendered on the Menus Canvas (sortingOrder above HUD).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `DialogueUI` | Full dialogue panel: NPC name, response text, scrollable topic list, choice buttons, and keyboard shortcuts (1–9, 0). Implements `IPointerClickHandler` to advance text on click. |

---

## DialogueUI Architecture

- **Three display states** (`DisplayState` enum): `Topics` (initial topic list), `Text` (NPC response), `Choices` (branching choice buttons).
- Subscribes to `InputSystem_Actions` in `OnEnable`/`OnDisable`; disposes in `OnDestroy`.
- `_input` is initialized in `Awake` — the `OnDisable` null guard is **required** (see root `CLAUDE.md`).
- Topic buttons are instantiated from `_topicButtonPrefab` into `_topicsContainer`; destroyed and recreated each time the topic list refreshes.
- Keyboard slots 1–9 and 0 map to choices via `_slotCallbacks[1..10]`; index 0 is unused.
- **Forced line** (`OpenLine(npcName, line)`, NPC-initiated, e.g. a theft scold): opens straight in `Text` with no topic list; `_forcedLine` makes click, slot 1, `_nextNodeButton` and Escape all call `DialogueSystem.Close()`. Reset by `Open()` / `Close()`.

## Cursor

- Opens panel → `CursorManager.Unlock()`.
- Closes panel → `CursorManager.Lock()`.

## Dependencies

- `DialogueSystem` — provides `StartDialogueNode[]` and drives state transitions.
- `GameEventSO` channels for dialogue open/close wired via `DialogueSystem`.

## Gotchas

- `_cachedStartNodes` defaults to `Array.Empty<StartDialogueNode>()` — safe to iterate before any dialogue loads.
- `_nextNodeButton` advances text-state nodes; it is shown/hidden depending on `DisplayState`.
- Do **not** destroy `_topicsContainer` children directly — call the dedicated clear method to avoid stale `UnityAction` callbacks on the button prefab instances.
