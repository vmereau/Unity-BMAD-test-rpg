---
title: 'Dialogue choice node — consistent prompt style, typewriter reveal, scrollable options'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '905c739'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md'
  - '{project-root}/Assets/_Game/Prefabs/UI/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On choice / teach nodes the NPC prompt (`Choice Text`) is pure white, appears instantly, uses ellipsis overflow and sits flush on the option list, while the text node's `ResponseText` is parchment (`#D8CCB0`), types out and has breathing room. The option list scrolls at sensitivity 1 (barely moves on the wheel), has no scrollbar and keeps the previous node's scroll offset.

**Approach:** Give the prompt the exact `ResponseText` look and the same typewriter reveal; hide the option rows until the prompt is fully revealed (any advance input completes it, like the text node). Add a gap between prompt and options, a thin auto-hiding scrollbar, normal wheel speed, a minimum list height, and reset the list to the top on every new node.

## Boundaries & Constraints

**Always:**
- One reveal implementation shared by `ResponseText` and `Choice Text` (same `_charsPerSecond`, unscaled time, `maxVisibleCharacters`).
- While the prompt reveals, the option rows are invisible and unclickable, and number keys / click / `DialogueAdvance` only complete the reveal (never pick). After completion the existing input grace applies before an option can be picked.
- Empty prompt or `_charsPerSecond == 0` → options shown immediately.
- Topic list (no prompt) behaves exactly as today. Escape rules unchanged.
- Prefab edits through `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` (no raw YAML).

**Ask First:**
- Changing option-row prefab padding / colors, the panel size, or the font.

**Never:**
- No change to `DialogueSystem`, dialogue SOs or content assets.
- No new sprites; scrollbar is a plain `Image`.
- No `Keyboard.current` / `Mouse.current` reads.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Choice node opens | Prompt "What will you do?" + 3 choices | Prompt types out in parchment; rows hidden; rows appear when done | N/A |
| Skip reveal | Key 2 / Space / click during reveal | Prompt completes, rows appear, choice 2 **not** picked | N/A |
| Pick after reveal | Key 2 ≥ 0.2 s after rows appear | Choice 2 selected | N/A |
| No prompt | Empty `node.text` | No prompt shown, rows visible immediately | N/A |
| Many options | 12 choices | List scrolls with wheel at normal speed, scrollbar visible; starts at top | N/A |
| Few options | 3 choices | Scrollbar hidden, viewport full width | N/A |
| Teach node | Teach prompt + costs | Same reveal / hide behavior; `[Leave]` appears with the rows | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- reveal (`StartReveal` / `CompleteReveal` / `Update`), `ShowChoicePrompt`, input paths (`HandleSlot`, `HandleAdvance`, `OnPointerClick`, `TryAdvance`), `SetState`
- `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab` -- `ContentArea` (VLG spacing 0), `Choice Text` (white, Ellipsis, redundant `ContentSizeFitter`), `TopicsScrollView` (ScrollRect sensitivity 1, no scrollbar)
- `Assets/_Game/Scripts/UI/Dialogue/DialogueReveal.cs` -- pure pacing math, reused as-is
- `Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md` -- folder doc to update

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- make the reveal target a `TMP_Text` field (`_revealText`) set by `StartReveal(target, line)`; `ShowChoicePrompt` starts a reveal on `_choiceNodeText`; cache a `CanvasGroup` on `_topicsScrollView` (add if missing) and hide it (alpha 0, not interactable, no raycasts) while a choice prompt reveals, show it in `CompleteReveal`; route slot keys, `DialogueAdvance` and panel clicks to "complete reveal + `MarkShown`" while revealing in any state; reset the `ScrollRect` to the top after options are built -- consistency with the text node
- [x] `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab` -- `Choice Text`: color `#D8CCB0`, overflow `Overflow`, remove `ContentSizeFitter`, line spacing / size matching `ResponseText`; `ContentArea` spacing 10; `TopicsScrollView`: scroll sensitivity 30, `LayoutElement.minHeight` 100, `CanvasGroup`, thin (6 px) vertical `Scrollbar` (brass `#E0A35A` α .6 handle, transparent track) with `AutoHideAndExpandViewport` and spacing 4 -- visual parity + scrollability
- [x] `Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md` -- document prompt reveal, hidden options during reveal, scroll setup

**Acceptance Criteria:**
- Given a choice node, when it opens, then the prompt and a text-node line are visually identical in color, size and left edge.
- Given the topic list, when shown, then nothing changed except the scroll speed / scrollbar.
- Given the project compiles, when EditMode tests run, then all pass and the console shows no new errors.

## Spec Change Log

- **2026-11-16 review (patches, no loopback):** natural reveal completion now stamps the input grace (a key landing as rows appear could pick); `Choice Text` overflow is `Ellipsis` instead of `Overflow` (a >4-line prompt drew over the rows, the 100 px list minimum takes precedence); scrollbar track fully transparent per task; rows deactivated before `Destroy` + layout rebuild before the scroll reset (one-frame scrollbar flash). KEEP: one shared reveal, CanvasGroup-hidden rows, skip-never-picks.

## Design Notes

Hiding the rows during the reveal mirrors the text node ("NPC speaks, then you answer") and removes the accidental-pick risk of a number key pressed to skip the line. A `CanvasGroup` is used instead of `SetActive` so `SetState` keeps owning scroll-view activation.

## Verification

**Commands:**
- Unity MCP `read_console` (errors) after recompilation -- expected: none from `DialogueUI`
- Unity MCP `run_tests` EditMode -- expected: all pass

**Manual checks (if no CLI):**
- Play mode: talk to an NPC with a choice node and a teach node; check reveal, skip, pick, scroll with 10+ rows.

## Suggested Review Order

**Shared reveal**

- Entry point: the choice prompt now goes through the same typewriter as text nodes.
  [`DialogueUI.cs:408`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L408)

- Reveal targets any TMP text; hides the option rows while a prompt types out.
  [`DialogueUI.cs:419`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L419)

- Completion always re-shows rows; ClearContents relies on this to never leave them hidden.
  [`DialogueUI.cs:435`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L435)

- Natural completion restarts the input grace so no pick lands on unseen rows.
  [`DialogueUI.cs:124`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L124)

**Input during reveal**

- Number keys only complete a running prompt reveal, never pick.
  [`DialogueUI.cs:143`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L143)

- Panel click / advance keys complete the prompt reveal in Choices state too.
  [`DialogueUI.cs:168`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L168)

- One skip helper shared with the Text-state advance path.
  [`DialogueUI.cs:192`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L192)

**Scrolling**

- CanvasGroup cached (added if missing) on the scroll view.
  [`DialogueUI.cs:83`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L83)

- Each new list rebuilds and snaps to the top.
  [`DialogueUI.cs:388`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L388)

- Old rows deactivated before deferred Destroy to keep bounds accurate.
  [`DialogueUI.cs:634`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L634)

**Peripherals**

- Prompt style, spacing, CanvasGroup, scrollbar, sensitivity, list min height.
  [`DialoguePanel.prefab`](../../Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab)

- Folder doc for the new behavior.
  [`CLAUDE.md:28`](../../Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md#L28)
