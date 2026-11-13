---
title: 'NPC dialogue close request'
type: 'bugfix'
created: '2026-11-13'
status: 'done'
baseline_commit: '4589296'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** AI-driven code can open a dialogue (`NPCPresence.RequestForcedTopic`) but can't close it. When Harl's toll encounter aborts mid-talk, or the region unloads during the Warden's speech or Hobb's briefing, the panel stays open with nobody behind it. Also, `DialogueSystem.NotifyTopicCompleted` logs "WorldStateManager unavailable" for every topic without a `dialogueFact`, though the manager is there.

**Approach:** Add a close request channel that NPCs raise with their own identity. `DialogueSystem` remembers which NPC opened the current dialogue and closes only for that NPC's request. `TollGate` raises it when the talk is aborted; `ArrivalSequence` raises it when it is interrupted. Split the misleading log into its two real cases.

## Boundaries & Constraints

**Always:** Close only when the request comes from the NPC whose request opened the current dialogue (reference identity, not name). Closing goes through the existing `DialogueSystem.Close()` (cursor lock, `SetInDialogue(false)`, context cleared). Player-opened dialogues ("Talk") record the NPC too, so its own close request also applies to them.

**Ask First:** Any change to how the toll is resolved (pay / defer / refuse) or to the arrival's phases beyond adding the close request.

**Never:** A close-anything request with no identity. Closing dialogues from the knockout path (already handled by `OnPlayerKnockedOut`). Polling for a destroyed / disabled NPC to auto-close. New UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Matching close | Harl's toll topic open, Harl raises a close request | Dialogue closes, cursor locked, player out of dialogue | N/A |
| Other NPC's dialogue | Player talking to Hobb, Harl raises a close request | Nothing happens | Info log only |
| Nothing open | Close request while no dialogue is open | Nothing happens | No warning |
| Forced line (no NPC) | A requester-less dialogue is open (e.g. `forcedLine`) | No close request matches it | N/A |
| Topic without fact | Repeatable topic ends, WSM present | No warning | N/A |
| WSM missing | Topic with `dialogueFact` ends, no WSM | Warning as today | Warn |

</frozen-after-approval>

## Code Map

- `Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs` -- request payload; gets the requester
- `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- raises dialogue requests; gets `RequestDialogueClose()`
- `Assets/_Game/ScriptableObjects/Events/` -- one concrete `GameEventSO_*` per file
- `Assets/_Game/Scripts/World/DialogueSystem.cs` -- open / `Close()` / `NotifyTopicCompleted`
- `Assets/_Game/Scripts/AI/NPC/TollGate.cs` -- `TickTalking` disturbed branch (aborted mid-talk)
- `Assets/_Game/Scripts/World/Story/ArrivalSequence.cs` -- `OnDisable` → `Release` (region unload mid-speech / briefing)
- `Assets/_Game/Prefabs/Player/Player.prefab` (DialogueSystem), `Prefabs/Entities/Humanoids/NPC_base Variant.prefab` + unpacked Brann in `StartingTown.unity` (NPCPresence) -- event wiring
- `Assets/_Game/Data/Events/` -- event assets (e.g. `OnPlayerKnockedOut.asset`)

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Dialogue/NPCDialogueRequestData.cs` -- add `NPCPresence requester` -- identity of the opener
- [x] `ScriptableObjects/Events/GameEventSO_DialogueCloseRequest.cs` (+ `.meta` with `MonoImporter`) -- `GameEventSO<NPCPresence>`, own file, `CreateAssetMenu` -- separate file rule for SO subclasses
- [x] `Data/Events/OnDialogueCloseRequested.asset` -- create via MCP / Unity so the meta is generated
- [x] `Scripts/AI/NPC/NPCPresence.cs` -- set `requester = this` in `RaiseDialogueRequest`; add `[SerializeField] GameEventSO_DialogueCloseRequest _onDialogueCloseRequested` and `public void RequestDialogueClose()` (warn once if unassigned) -- NPC-side API
- [x] `Scripts/World/DialogueSystem.cs` -- store `_currentRequester` on open (cleared in `ClearContext`); listen to the close channel in `OnEnable` / `OnDisable`; handler closes only when `IsOpen` and `ReferenceEquals(requester, _currentRequester)`; split `NotifyTopicCompleted` else-branch: no `dialogueFact` → no warning, WSM null → warning -- matching rule + log fix
- [x] `Scripts/AI/NPC/TollGate.cs` -- in `TickTalking`'s disturbed branch, if the dialogue is still open call `_collectorPresence.RequestDialogueClose()` and clear `s_answeringGate` before brawl / end -- no orphaned toll panel
- [x] `Scripts/World/Story/ArrivalSequence.cs` -- in `OnDisable` when running and `_topicOpened`, request close on the Warden's / tent mate's presence (null-checked) before `Release` -- region unload leaves no open speech / briefing
- [x] Prefabs -- wire the new asset on `Player.prefab` DialogueSystem, `NPC_base Variant` NPCPresence and unpacked Brann (other scene NPCs inherit it)
- [x] `Tests/EditMode/` -- test the matching rule, extracted as a pure static (e.g. `DialogueSystem.ShouldCloseFor(isOpen, current, requester)`): match, other NPC, nothing open, null current
- [x] Docs -- `Scripts/World/CLAUDE.md` / `UI/Dialogue/CLAUDE.md` (close request), `Scripts/AI/CLAUDE.md` (`RequestDialogueClose`); `deferred-work.md`: mark resolved the toll-left-open, hut-briefing unload, misleading-log (×2), knockout screens and startup `SetInCombat` NRE entries (last two fixed in `c91fb90`)

**Acceptance Criteria:**
- Given Harl's toll topic is open, when he is hit or spots a hostile, then the panel closes and the encounter ends as today (brawl or end) with no stuck cursor.
- Given the Warden's speech or Hobb's briefing is open, when the region is unloaded, then the dialogue closes and the next load replays the arrival.
- Given any player-opened conversation, when an unrelated NPC requests a close, then it stays open.

## Design Notes

Match with `ReferenceEquals`, not Unity `==`: during a region unload the requester may already be destroyed, and Unity's `==` treats two destroyed objects as equal. The close is synchronous; `TollGate` / `ArrivalSequence` read `CursorManager.IsLocked` afterwards as they already do.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass, new matching tests included
- MCP `read_console` -- expected: no compile errors

**Manual checks (if no CLI):**
- Play mode: open Harl's toll, hit him → panel closes, brawl starts. Talk to Hobb → no "WorldStateManager unavailable" warning.

## Suggested Review Order

**Identity match (the design core)**

- Pure rule: close only for the opener, by reference (destroyed objects compare equal with Unity ==).
  [`DialogueSystem.cs:90`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L90)

- Handler: ignores other NPCs' requests, closes through the existing `Close()`.
  [`DialogueSystem.cs:75`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L75)

- Opener recorded on every NPC request; cleared with the context, null for forced lines.
  [`DialogueSystem.cs:130`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L130)

- NPC side: stamps itself as requester and raises the close channel.
  [`NPCPresence.cs:122`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L122)

**Callers**

- Harl disturbed mid-talk: close the toll panel, drop the answering gate.
  [`TollGate.cs:195`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L195)

- Gate disabled (region unload) mid-talk closes the panel too (review patch).
  [`TollGate.cs:98`](../../Assets/_Game/Scripts/AI/NPC/TollGate.cs#L98)

- Arrival unload: `Release` first, then close the Warden's speech / Hobb's briefing.
  [`ArrivalSequence.cs:155`](../../Assets/_Game/Scripts/World/Story/ArrivalSequence.cs#L155)

**Log fix and peripherals**

- Repeatable topics (no fact) no longer warn about a missing WorldStateManager.
  [`DialogueSystem.cs:257`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L257)

- Payload field and channel type (own file per SO subclass rule).
  [`NPCDialogueRequestData.cs:18`](../../Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs#L18)
  [`GameEventSO_DialogueCloseRequest.cs:11`](../../Assets/_Game/ScriptableObjects/Events/GameEventSO_DialogueCloseRequest.cs#L11)

- Matching-rule tests.
  [`DialogueCloseRequestTests.cs:12`](../../Assets/Tests/EditMode/DialogueCloseRequestTests.cs#L12)
