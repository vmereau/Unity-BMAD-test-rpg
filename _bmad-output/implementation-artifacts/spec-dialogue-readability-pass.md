---
title: 'Dialogue readability pass — player echo, history, text reveal, advance keys, bug fixes'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_commit: 'd35a497'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md'
  - '{project-root}/Assets/_Game/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Dialogue loses context (the player's chosen line is never shown, each NPC line replaces the last), lines run ~110+ chars wide, text only advances with key 1 or a click, and four bugs exist: teach-node NPC text is hidden, teach `[Leave]` ignores `node.nextNode`, Farewell/Leave vanish when slots exceed 10 (player trapped), and `TextDialogueNode`'s "Continue/Farewell" comment describes nothing real.

**Approach:** Echo the player's pick as a "You:" line, show a short faded history above the current NPC line (Text state only; cleared on return to topics), reveal NPC lines with a typewriter (first press completes, second advances), add Space/Enter/E advance with a short input grace, label the advance button Continue/End/Trade, taller band with a narrower text column, and fix the bugs.

## Boundaries & Constraints

**Always:**
- History and typewriter only in Text state. Choices / Teach states show only the NPC prompt above the options, as today.
- Farewell (topics) and Leave (teach) always exist; they get a number key only if slot ≤ 10, otherwise they are shown with no number.
- Reveal and grace use unscaled time; reveal uses TMP `maxVisibleCharacters` (rich-text safe).
- New input action edited in **both** `InputSystem_Actions.inputactions` and the embedded JSON in `InputSystem_Actions.cs`.
- Pure logic (history buffer, reveal count, grace gate) lives in plain C# classes covered by EditMode tests.

**Ask First:**
- Any change to dialogue SO serialized fields or to `DialogueSystem` public API signatures beyond what is listed here.
- If the `DialoguePanel` layout can't fit history + line + button at ~300 px height.

**Never:**
- No played/seen topic marking, option tags, pooling, or logic move to `DialogueSystem` (follow-up specs).
- No `Keyboard.current` / `Mouse.current` reads.
- No change to dialogue content assets.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pick topic | Topic "Spiders?" → text node | History shows "You: Spiders?", NPC line types out below | N/A |
| Press during reveal | Space while typing | Line completes instantly, no advance | N/A |
| Press after reveal | Space, line complete | Advances to next node | N/A |
| Grace | Key/click within 0.2 s of new content | Ignored (Escape not gated) | N/A |
| Last line | `_pendingNextNode == null` | Button reads "End"; advance completes topic, history clears | N/A |
| Teach Leave with nextNode | `node.nextNode` set | Plays that node, then topic completes | N/A |
| 12 topics | Slots exceed 10 | Topics 11–12 and Farewell unnumbered but clickable | N/A |
| E closes forced line | E advances on the forced-line close | Dialogue closes; NPC is **not** re-interacted the same frame | Frame guard |
| History cap | More lines than capacity | Oldest dropped | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- view + all changes above (state, slots, buttons)
- `Assets/_Game/Scripts/World/DialogueSystem.cs` -- flow; `AdvanceToNode`, `SelectChoice`, `ApplyTeachChoice`, `NotifyTopicCompleted`
- `Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs` -- stale comment
- `Assets/_Game/InputSystem_Actions.inputactions` + `.cs` -- UI map, add `DialogueAdvance`
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `SetInDialogue`; add dialogue-ended frame
- `Assets/_Game/Scripts/World/InteractionSystem.cs:205-215` -- Interact press read
- `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab`, `Prefabs/UI/UICanvas.prefab` -- band rect (1536×216 at 192,702)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueTranscript.cs` -- new plain class: capacity-capped list of (speaker, text, isPlayer); `Add`, `Clear`, `BuildRichText()` with older lines alpha-faded, speaker colors -- testable history
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueReveal.cs` -- new static helpers: `VisibleCount(elapsed, charsPerSecond, total)`, `IsInputAllowed(sinceShown, grace)` -- testable pacing
- [x] `Assets/_Game/InputSystem_Actions.inputactions` + `InputSystem_Actions.cs` -- add UI `DialogueAdvance` (Space, Enter, Numpad Enter, E) -- advance keys
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- `_historyText`, `_nextNodeLabel`, `_charsPerSecond` (60), `_inputGrace` (0.2) fields; echo player line on topic/choice/teach pick; render history in Text state, clear on `RestoreTopics`/`Close`; typewriter in `Update`; route click, slot 1 and `DialogueAdvance` through one advance method (complete reveal → else advance); grace gate on slots/advance/click; label Continue/End/Trade; fix teach text activation, teach Leave → `AdvanceToNode(node.nextNode)` when set; always add Farewell/Leave, unnumbered past slot 10; collapse slot handlers into an array loop
- [x] `Assets/_Game/Scripts/Player/PlayerStateManager.cs` + `Assets/_Game/Scripts/World/InteractionSystem.cs` -- record frame of `SetInDialogue(false)`; skip Interact on that frame -- prevents E close → reopen
- [x] `Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs` -- fix comment
- [x] `DialoguePanel.prefab` / `UICanvas.prefab` (via MCP) -- band height ~300 px (keep bottom edge), add `HistoryText` TMP above `ResponseText` in `ResponseWrapper`, text column max ~1000 px centred, wire new fields
- [x] `Assets/Tests/EditMode/DialogueTranscriptTests.cs`, `DialogueRevealTests.cs` -- cap/clear/fade order, reveal clamping, grace boundary
- [x] `Scripts/UI/Dialogue/CLAUDE.md`, `Art/UI/CLAUDE.md` (Dialogue metrics row) -- document history, reveal, advance input, `ClearContents` gotcha, new size

**Acceptance Criteria:**
- Given a teach node, when shown, then the NPC prompt text is visible above the options.
- Given the dialogue is open, when the player presses Escape mid-chain, then behavior is unchanged (no close).
- Given trade via a shop node, when advanced, then the button read "Trade" and trade UI opens as before.
- Given EditMode tests, when run, then all pass including existing ones.

## Design Notes

Single advance entry point keeps click, key 1 and `DialogueAdvance` consistent:

```csharp
private void TryAdvance() {
    if (!DialogueReveal.IsInputAllowed(Time.unscaledTime - _shownAt, _inputGrace)) return;
    if (_revealing) { CompleteReveal(); return; }
    _slotCallbacks[1]?.Invoke();
}
```

`InputAction.performed` fires before `Update`, so an E press that closes the dialogue would be read again by `InteractionSystem` the same frame with the cursor already locked — hence the frame guard.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all green
- MCP `read_console` after compile -- expected: no errors

**Manual checks (if no CLI):**
- Play: talk to Guard / trainer / Innkeeper (shop); verify echo, history fade, reveal skip, Continue/End/Trade, teach text, Leave, E closing forced line without reopening.

## Suggested Review Order

**Advance path & pacing**

- Single entry point: completes a reveal (and restarts the grace) or runs the per-node advance.
  [`DialogueUI.cs:166`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L166)

- NPC line display: history rendered first, then reveal, then recorded for the next line.
  [`DialogueUI.cs:345`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L345)

- Typewriter via TMP maxVisibleCharacters; tag-free character count.
  [`DialogueUI.cs:377`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L377)

- Per-frame reveal tick, unscaled time, no allocations.
  [`DialogueUI.cs:117`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L117)

- One handler for all ten number keys; key 1 routes to advance in Text state.
  [`DialogueUI.cs:128`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L128)

- Next button made non-navigable so Enter/Submit can't double-fire it.
  [`DialogueUI.cs:83`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L83)

**Options & escape hatches (bug fixes)**

- Shared button builder: numbered ≤ slot 10, click-only past it, grace-gated clicks.
  [`DialogueUI.cs:425`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L425)

- Leave plays the teach node's nextNode when set, else ends the topic.
  [`DialogueUI.cs:506`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L506)

- Choice node with every option filtered out gets a Leave (was a softlock).
  [`DialogueUI.cs:280`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L280)

- Prompt text re-shown for choice and teach nodes (teach text was invisible).
  [`DialogueUI.cs:357`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L357)

**History model**

- Capped, deduped, cropped, noparse-wrapped history lines.
  [`DialogueTranscript.cs:56`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueTranscript.cs#L56)

- Faded rich-text rendering, oldest first.
  [`DialogueTranscript.cs:74`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueTranscript.cs#L74)

- Pure pacing rules (reveal count, grace gate).
  [`DialogueReveal.cs:12`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueReveal.cs#L12)

**Close-frame guard (cross-system)**

- Frame stamp on dialogue close; IsBusy blocks jump/attack/move that frame.
  [`PlayerStateManager.cs:91`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L91)

- Closing E must not re-open the NPC.
  [`InteractionSystem.cs:207`](../../Assets/_Game/Scripts/World/InteractionSystem.cs#L207)

- Closing number key must not fire the action bar.
  [`ActionBarSystem.cs:174`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L174)

**Peripherals**

- New UI action (embedded JSON; .inputactions mirrors it).
  [`InputSystem_Actions.cs:1068`](../../Assets/_Game/InputSystem_Actions.cs#L1068)

- Pure-logic tests.
  [`DialogueTranscriptTests.cs:1`](../../Assets/Tests/EditMode/DialogueTranscriptTests.cs#L1)
  [`DialogueRevealTests.cs:1`](../../Assets/Tests/EditMode/DialogueRevealTests.cs#L1)
