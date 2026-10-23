# CLAUDE.md — Assets/_Game/Scripts/UI/Dialogue

> NPC dialogue overlay. Rendered on the Menus Canvas (sortingOrder above HUD).

---

## Scripts

| Script | Purpose |
|--------|---------|
| `DialogueUI` | Full dialogue panel: NPC name, response text (one node per window, no history), scrollable topic list, choice buttons, and keyboard shortcuts (1–9, 0). Implements `IPointerClickHandler` to advance text on click. |
| `DialogueReveal` | Static pacing rules: typewriter `VisibleCount` and the post-show `IsInputAllowed` grace. EditMode-tested. |
| `DialogueOptionFormat` | Static option-row rules: `Classify` (Trade / Teach by walking `nextNode`, ≤ 32 steps), `Label` (dim number key, tag, dimmed seen text, `<noparse>` text + raw rich suffix), `CostLabel` (unpayable parts red), `OrderUnseenFirst`. EditMode-tested. |

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
- **One node = one window:** Text state shows only the current node's line in `ResponseText` (no history, no echo of the player's pick — a faded history was tried and removed as hard to read). Choices / Teach show the NPC prompt above the options.
- **Option rows:** every label goes through `DialogueOptionFormat.Label`. Topics are ordered unseen → seen (`OrderUnseenFirst`), numbered in display order; choices keep authored order. Seen = `DialogueSystem.IsTopicSeen` / `IsChoiceSeen` (persisted, set on **pick** in `StartTopic` / `SelectChoice(choice, node)` — always pass the node). Trade / Teach-tagged options and teach choices are never dimmed. Teach rows use `GetTeachCostShortfall` for the red cost parts.
- **Row prefab** `TopicButtonPrefab`: `HorizontalLayoutGroup` (padding 16/16/6/6, controls child size) so rows grow with wrapped text; label parchment `#D8CCB0`, `raycastTarget` off; Button Color Tint normal transparent, highlighted brass α .35, pressed / selected α .5 (plain white `Image`, no sprite yet). `TopicsContent` VLG controls child height, spacing 4.
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
