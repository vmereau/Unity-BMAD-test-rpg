---
title: 'Dialogue option readability: seen state, tags, row styling'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_commit: '7c3ca9a'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Dialogue options all look the same. Players can't tell which repeatable topics or choices they've already explored, which options open trade or teaching, or why a teach option is disabled. The rows have no padding, no hover / selected state, and clip long text.

**Approach:** (1) Persist a "seen" marker when a topic or choice is **picked**, and dim seen options. (2) Prefix options with coloured tags (`[Trade]`, `[Teach]` only — no quest tag), sort topics (unseen, then seen, Farewell last), and colour the part of a teach cost the player can't pay. (3) Restyle the option row: padding, hover and selected tint, a dim number key, and a row height that grows with wrapped text.

## Boundaries & Constraints

**Always:** Seen markers are saved with the game: they live in `WorldStateManager._worldFacts` under the `Dialogue.Seen.{id}` key, built only inside `WorldStateManager`. Marking something seen raises **no** fact events, so quests and memories never react to it. Colours come from the `ART_DIRECTION.md` palette (the red is a lightened dried blood). `[Farewell]` / `[Leave]` are never dimmed or tagged and are always last. Number keys follow the displayed order.

**Ask First:** Any change to the save file format, or to `DialogueFact` keys or semantics.

**Never:** A `[Quest]` tag or quest-topic flag. Dimming teach choices (they are repeatable purchases). Generating a `Button` sprite (art is the user's job; rows use a plain tinted `Image`). Keyboard arrow navigation, a conversation log or a dialogue camera.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Repeatable topic picked | Topic without `dialogueFact`, picked then completed | Shown dimmed on the topic list; still clickable | N/A |
| Choice picked | Choice at index i of node N | Shown dimmed the next time N is displayed, also after save / load | N/A |
| Topic chain reaches a shop | Start → Text → Shop | Label `[Trade] Show me your wares` | Walk stops after 32 steps or on a loop |
| Teach cost, not enough gold | 10 LP OK, 50g missing | `(10 LP, 50g)` with only `50g` in red; button disabled | N/A |
| Node without id | Asset whose `_id` is empty (not yet validated) | Never dimmed, nothing recorded | Warn once per node |
| Long choice text | 150 characters | Row wraps to 2 lines and grows; nothing clipped | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs` -- base node; gets the stable `Id`
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- fact store, captured by `SaveSystem`
- `Assets/_Game/Scripts/World/DialogueSystem.cs` -- `StartTopic`, `SelectChoice`, `CanAffordTeachChoice`
- `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs` -- builds option buttons (`AddOptionButton`, `RestoreTopics`, `BuildCostLabel`)
- `Assets/_Game/Prefabs/UI/Dialogue/TopicButtonPrefab.prefab`, `DialoguePanel.prefab` -- row and container layout

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Dialogue/DialogueNode.cs` -- add `[SerializeField] string _id` and a public `Id`. In an `#if UNITY_EDITOR` `OnValidate`, sync it to the asset's AssetDatabase GUID (so duplicated assets get a new id) -- stable runtime key
- [x] Every existing `DialogueNode` asset -- one-off `execute_code` pass that sets `_id` and saves the assets -- existing data gets ids
- [x] `Scripts/Core/State/WorldStateManager.cs` -- add `MarkDialogueSeen(string id)` and `IsDialogueSeen(string id)`; they write and read `_worldFacts` directly under `$"{WorldFactPrefix.Dialogue}.Seen.{id}"` and raise no event -- persisted through `CaptureFacts`
- [x] `Scripts/World/DialogueSystem.cs` -- `StartTopic` marks the start node seen; `SelectChoice` marks `ChoiceSeenId(node, index)` seen (pass the node and index from the UI); add `IsSeen(...)` helpers and `GetTeachCostShortfall(choice)`, which returns which of gold / LP / stats is short -- one owner for seen and affordability
- [x] `Scripts/UI/Dialogue/DialogueOptionFormat.cs` (new, pure static) -- `Classify(DialogueNode)` returns None / Trade / Teach by walking `nextNode` through Text nodes (stops at a Choice node, after 32 steps, or on a loop). Also `TagPrefix`, `DimmedLabel`, `CostLabel(choice, shortfall)` and `OrderTopics(...)` -- testable formatting
- [x] `Scripts/UI/Dialogue/DialogueUI.cs` -- use `DialogueOptionFormat` in `RestoreTopics`, `ShowChoiceNode` and `AddTeachChoiceButton`; draw the number key in dim parchment; pass the choice index through to `SelectChoice`; replace `BuildCostLabel` -- wire it in
- [x] `Prefabs/UI/Dialogue/TopicButtonPrefab.prefab` -- add a `HorizontalLayoutGroup` (padding 16 / 16 / 6 / 6; it controls child height), let the label wrap, and set the button's Color Tint: normal transparent, highlighted brass `#8C7343` at α 0.35, pressed / selected α 0.5. In `DialoguePanel.prefab`, the topics `VerticalLayoutGroup` controls child height with spacing 4 -- row styling
- [x] `Assets/Tests/EditMode/DialogueOptionFormatTests.cs` -- classify (shop, teach, loop, 32-step limit), cost-label colouring, topic order, dimming -- matrix coverage
- [x] `Scripts/UI/Dialogue/CLAUDE.md`, `Data/Facts/CLAUDE.md`, `Data/NPCs/DIALOGUE.md` -- document seen markers, tags, and the "no event" rule

**Acceptance Criteria:**
- Given a seen repeatable topic, when I save, quit and load, then it is still dimmed.
- Given a choice is picked, when quest or memory listeners run, then none of them receive an event for the seen marker.
- Given a topic list with unseen and seen topics, when it opens, then the order is unseen → seen → `[Farewell]`, numbered 1..n in that order.
- Given the mouse hovers or clicks a row, then the brass tint appears and no row clips its text.
- Given EditMode tests, when they run, then all pass, including the existing `DialogueTranscript` / `DialogueReveal` tests.

## Spec Change Log

- 2026-10-22 — user renegotiated scope during implementation: drop the `[Quest]` tag and the `questTopic` flag; topic order is unseen → seen → Farewell. Frozen intent updated by user request. KEEP: Trade / Teach auto-classification, seen persistence, row styling.

## Design Notes

Choice id = `{node.Id}.{index}`. Reordering a node's choices reassigns their seen state; that's acceptable, and it's documented in `DIALOGUE.md`. Dimmed text is `#B8A988` at α 0.6. Tag colours: Trade brass `#8C7343` lightened to `#B89A5E`, Teach mossy `#8A9466`, shortfall red `#A8473B`. Seen state is a separate key, not a `DialogueFact`, so picking a choice never changes topic visibility. Options tagged Trade / Teach are never dimmed or sorted down (services the player returns to); choices keep authored order, only topics are re-sorted. `ChoiceSeenId` resolves the index with `Array.IndexOf(node.choices, choice)`, so memory-filtered lists don't shift ids.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all green
- MCP `read_console` after compile -- expected: no errors

**Manual checks:**
- `UILayoutCapture.Capture("Dialogue")` → padded rows, dim numbers, wrapped long text
- Play mode: talk to Merchant / BlackSmith, check the Trade / Teach tags, then pick a choice, back out and see it dimmed

## Suggested Review Order

**Seen state (persisted, no events)**

- Entry point: seen markers share the saved fact store but bypass fact events.
  [`WorldStateManager.cs:124`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L124)

- Stable id synced to the asset GUID; duplicates get their own.
  [`DialogueNode.cs:24`](../../Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs#L24)

- Choice key = node id + index in the unfiltered choices array.
  [`ChoiceDialogueNode.cs:33`](../../Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs#L33)

- Marked on pick: topic in StartTopic, choice in SelectChoice (node passed by UI).
  [`DialogueSystem.cs:213`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L213)

- Missing id: untracked and warned once per node.
  [`DialogueSystem.cs:295`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L295)

**Option formatting**

- Trade / Teach detection walks nextNode, stops at choices / 32 steps.
  [`DialogueOptionFormat.cs:35`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueOptionFormat.cs#L35)

- One label builder: dim key, tag, dimmed text, escaped authored text.
  [`DialogueOptionFormat.cs:55`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueOptionFormat.cs#L55)

- Per-part red costs, fed by the new shortfall flags.
  [`DialogueSystem.cs:251`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L251)

**UI wiring**

- Topics re-sorted unseen-first; tagged services never dimmed.
  [`DialogueUI.cs:412`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L412)

- Choices dimmed in place (authored order kept).
  [`DialogueUI.cs:484`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L484)

- Unaffordable teach row text dimmed (row has no disabled tint now).
  [`DialogueUI.cs:519`](../../Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs#L519)

**Peripherals**

- Prefabs: `TopicButtonPrefab` (HLG padding, brass tint, parchment label), `DialoguePanel` (VLG controls height, spacing 4); 48 node assets gained `_id`.
  [`TopicButtonPrefab.prefab`](../../Assets/_Game/Prefabs/UI/Dialogue/TopicButtonPrefab.prefab)

- Tests: formatter + seen-store round-trip / no-event.
  [`DialogueOptionFormatTests.cs`](../../Assets/Tests/EditMode/DialogueOptionFormatTests.cs)
  [`WorldStateManagerFactsTests.cs:344`](../../Assets/Tests/EditMode/WorldStateManagerFactsTests.cs#L344)
