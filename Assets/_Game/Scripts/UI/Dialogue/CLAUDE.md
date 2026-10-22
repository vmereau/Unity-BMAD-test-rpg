# CLAUDE.md — Assets/_Game/Scripts/UI/Dialogue

> NPC dialogue overlay. Rendered on the Menus Canvas (sortingOrder above HUD).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `DialogueUI` | Full dialogue panel: NPC name, history + response text, scrollable topic list, choice buttons, and keyboard shortcuts (1–9, 0). Implements `IPointerClickHandler` to advance text on click. |
| `DialogueTranscript` | Plain class: capped rolling history of the current topic ("You: …" / "<NPC>: …"), built as faded TMP rich text (palette colours). EditMode-tested. |
| `DialogueReveal` | Static pacing rules: typewriter `VisibleCount` and the post-show `IsInputAllowed` grace. EditMode-tested. |

---

## DialogueUI Architecture

- **Three display states** (`DisplayState` enum): `Topics` (initial topic list), `Text` (NPC response), `Choices` (branching choice buttons).
- Subscribes to `InputSystem_Actions` in `OnEnable`/`OnDisable`; disposes in `OnDestroy`.
- `_input` is initialized in `Awake` — the `OnDisable` null guard is **required** (see root `CLAUDE.md`).
- Topic buttons are instantiated from `_topicButtonPrefab` into `_topicsContainer`; destroyed and recreated each time the topic list refreshes.
- Keyboard slots 1–9 and 0 map to choices via `_slotCallbacks[1..10]`; index 0 is unused. One `HandleSlot` handler serves all ten `DialogueOption*` actions (`_slotActions`). Options past slot 10 are still shown, unnumbered and click-only; **Farewell / Leave are always added** so the player can't be trapped — including a `[Leave]` on a choice node whose options were all filtered out by memory gates.
- **Single advance path (Text state):** click, key 1, `UI/DialogueAdvance` (Space / Enter / Numpad Enter / E) and `_nextNodeButton` all go through `TryAdvance()` — first press completes a running typewriter reveal, the next runs `_slotCallbacks[1]` (the per-node advance action). Don't wire per-node listeners on `_nextNodeButton`; set `_slotCallbacks[1]`.
- **Input grace:** `SetState` (and completing a reveal) stamps `_shownAt`; keys, clicks and option buttons are ignored for `_inputGrace` (0.2 s, unscaled) after it. Escape is not gated.
- `_nextNodeButton` navigation is forced to **None** in `Awake`: a selected button would get the UI module's Submit (Enter) in the same frame as `DialogueAdvance` and skip a line.
- **History:** the player's pick is recorded as "You: …" before calling `DialogueSystem`; NPC lines and choice prompts are recorded after display. `_historyText` (inside `ResponseWrapper`) shows the lines **before** the current one — Text state only; Choices / Teach show just the prompt. Cleared by `RestoreTopics()` / `Close()` / `OpenLine()`. Entries are cropped to `_historyLineMaxChars` (90 ≈ one row) and wrapped in `<noparse>`; an exact repeat of the newest line is skipped. `ResponseText` keeps a 100 px min height so history can't squeeze the current line.
- **Advance label** `_nextNodeLabel`: "Continue" (more nodes), "End" (last node / forced line), "Trade" (shop node).
- **Teach `[Leave]`** plays the teach node's `nextNode` when set, else completes the topic.
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
- `ClearContents()` hides `_choiceNodeText` — every Choices-state method must re-show it via `ShowChoicePrompt()` (teach nodes once rendered with their NPC text invisible).
- The key that closes a dialogue fires in an input callback **before** `Update`, with the cursor already relocked — `PlayerStateManager.DialogueEndedThisFrame` (part of `IsBusy`, also checked by `InteractionSystem`) stops it from jumping, attacking or re-interacting that frame.
- Typewriter uses TMP `maxVisibleCharacters` with `ForceMeshUpdate(true)` to count characters (rich-text tags excluded).
- Do **not** destroy `_topicsContainer` children directly — call the dedicated clear method to avoid stale `UnityAction` callbacks on the button prefab instances.
