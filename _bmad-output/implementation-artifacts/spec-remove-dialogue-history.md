---
title: 'Remove dialogue history — one dialogue node per text window'
type: 'refactor'
created: '2026-10-23'
status: 'done'
baseline_commit: '01b5ec2'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The faded rolling history added by `spec-dialogue-readability-pass` (player "You: …" echo + previous NPC lines stacked above the current line) is hard to read and confusing in play.

**Approach:** Remove the history entirely (transcript model, the `HistoryText` widget, the player echo) so each dialogue node again shows only its own text in the window. Everything else from that pass stays.

## Boundaries & Constraints

**Always:**
- Keep the typewriter reveal, input grace, `DialogueAdvance` keys, Continue / End / Trade label, the forced-line flow, the bug fixes (teach text, teach Leave → nextNode, always-present Farewell / Leave, close-frame guard), seen markers and option tags.
- Prefab edits through Unity (MCP) — no raw YAML edit followed by `refresh_unity(force)`.

**Ask First:**
- Any change to band size / layout beyond deleting `HistoryText`.

**Never:**
- No change to `DialogueSystem`, dialogue SOs, input actions or dialogue content assets.
- Don't remove `DialogueReveal` or its tests.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Pick topic | Topic "Spiders?" → text node | Window shows only the NPC line (typing out); no "You:" line | N/A |
| Multi-node chain | Continue on node A → node B | Node A's text replaced by node B's text only | N/A |
| Choice → text | Pick a choice option | Next text node shows alone, no echo of the pick | N/A |
| Forced line | `OpenLine` (theft scold) | Line shown alone; advance / Escape closes | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- `_historyText`, `_historyCapacity`, `_historyLineMaxChars`, `_transcript`, `_npcName`, `RecordPlayerLine`, `RefreshHistory`, transcript calls in `ShowNpcLine` / `ShowChoicePrompt` / `RestoreTopics` / `Close` / `OpenLine` / option actions
- `Assets/_Game/Scripts/UI/Dialogue/DialogueTranscript.cs` (+ `.meta`) -- history model, only used by `DialogueUI`
- `Assets/Tests/EditMode/DialogueTranscriptTests.cs` (+ `.meta`) -- tests of the model
- `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab` -- `HistoryText` TMP child of `ResponseWrapper`; serialized history fields on `DialogueUI`
- `Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md`, `Assets/_Game/Art/UI/CLAUDE.md` (Dialogue metrics row + screen catalog) -- history docs

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- delete history fields, `_transcript`, `_npcName` (only used for history), `RecordPlayerLine`, `RefreshHistory`, the Awake warning and every transcript call; update `ShowNpcLine` / `ShowChoicePrompt` doc comments -- one node = one window
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueTranscript.cs`, `Assets/Tests/EditMode/DialogueTranscriptTests.cs` (+ `.meta`) -- delete -- dead code
- [x] `DialoguePanel.prefab` (via MCP) -- delete `HistoryText` GameObject; stale serialized fields drop on save -- no hidden leftover widget
- [x] `Scripts/UI/Dialogue/CLAUDE.md`, `Art/UI/CLAUDE.md` -- remove history bullet, `DialogueTranscript` row and "history" mentions -- docs match code

**Acceptance Criteria:**
- Given the project compiles, when `grep -rn "Transcript\|_historyText\|HistoryText" Assets` runs, then nothing matches.
- Given EditMode tests, when run, then all pass.
- Given a text node with a long line, when shown, then the typewriter, input grace and Continue / End label behave as before.

## Spec Change Log

## Verification

**Commands:**
- MCP `read_console` after compile -- expected: no errors
- MCP `run_tests` (EditMode) -- expected: all green

**Manual checks (if no CLI):**
- Play: talk to Guard / trainer / Innkeeper — each node shows only its own text; Continue / End / Trade, teach, Leave and forced line unchanged.

## Suggested Review Order

**Text window shows one node**

- Entry point: NPC line now only sets the label, state and reveal — no history render.
  [`DialogueUI.cs:326`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L326)

- Choice / teach prompt no longer recorded anywhere.
  [`DialogueUI.cs:336`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L336)

- Topic and choice picks call `DialogueSystem` directly, no "You:" echo.
  [`DialogueUI.cs:427`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L427)
  [`DialogueUI.cs:454`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L454)

- Close / topic restore just clear contents.
  [`DialogueUI.cs:292`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L292)
  [`DialogueUI.cs:363`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L363)

- Serialized fields: history trio gone, pacing fields kept.
  [`DialogueUI.cs:32`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L32)

**Prefab & docs**

- `HistoryText` removed from `ResponseWrapper` (Unity save, stale fields dropped).
  [`DialoguePanel.prefab:696`](../../Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab#L696)

- Rule recorded so history isn't re-added by accident.
  [`CLAUDE.md:27`](../../Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md#L27)
