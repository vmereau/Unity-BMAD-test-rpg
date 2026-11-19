---
title: 'Timed dialogue: ElapsedFact (a fact that turns true N game hours after its source)'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '680dff87e31b5e06ac0266737e486f04dc56e575'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Data/Facts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Core/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Dialogue gates react the moment a fact turns true. There is no way to say "bring this up only after some game time has passed". Example: Hobb confronts the player about his rummaged box right after the theft, when it should come 2 in-game hours later.

**Approach:** `WorldStateManager` records the `GameClock.Now` time at which each stored fact turns false → true, and saves it. A new computed fact type, `ElapsedFact { Fact source; float hours }`, is true once `source` is true and `hours` (decimals allowed) of game time have passed since then. It works in every condition that goes through `GetFact`: memory topics, talk openers, quest parts and `_availableAfter`. Event-driven readers (quests, quest log) are told when a delay ends: `WorldStateManager` keeps the deadlines of `ElapsedFact`s it evaluated as pending and raises the usual `OnFactChanged` when the clock passes one. Hobb's suspicion opener uses a 2 h `ElapsedFact`.

## Boundaries & Constraints

**Always:** Timing is per fact. The timestamp is taken when the stored value turns false → true. Setting an already-true fact again keeps the first timestamp, and setting it false removes the timestamp. Pull readers evaluate lazily; the only clock watching is the pending-deadline check, which raises `OnFactChanged` once per `ElapsedFact` when its delay ends (registered on evaluation, so no asset list). Older saves (no timestamps) and facts set while no clock was running count as elapsed. The timestamps are an optional `SaveData` field (null → none, no version bump), restored without events. Only stored facts (Killed / Dialogue / World / Recipe) can be a source. A witnessed theft still cancels Hobb's opener at once: the witness check reads the `ElapsedFact`'s source and ignores the delay.

**Ask First:** Changing the `NpcTalkOpener` rules beyond the field type and the witness-source lookup. Any timestamp on computed facts (Quest / Skill / Stat).

**Never:** Timing a dialogue from when **all** of its conditions became true together. Per-fact coroutines or timers outside `WorldStateManager`. Day or minute units. Restarting the timer when a true fact is set again.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Before delay | Source set at 10:00, now 11:30, hours 2 | ElapsedFact false | — |
| After delay | Now 12:00 or later (walked or slept) | ElapsedFact true | — |
| Source false | Source never set or set false | false | — |
| No timestamp | Source true, no time recorded (old save, no clock) | true | — |
| No clock now | `GameClock.Active` null, source true with timestamp | true | — |
| Re-set | Source set true again at 11:00 | Keeps the 10:00 timestamp | — |
| Save / load | Saved at 11:00, loaded | Timestamp 10:00 restored, true at 12:00 | Null field → none |
| Bad asset | Source null / computed / ElapsedFact, or hours < 0 | false (null) or source-only (hours ≤ 0) | Quest Explorer validator issue |
| Delay ends (quest) | Pending ElapsedFact in a quest part, clock passes its deadline (walk or sleep) | One `OnFactChanged`; the quest re-evaluates | Source turned false meanwhile → no event |
| Hobb unseen take | Rummaged at T, Talk at T+1 h, then T+2 h | First Talk: topic list; second Talk: suspicion line first | — |
| Hobb witnessed | Chase over his goods ends at T | Opener marked played, never plays | — |

</frozen-after-approval>

## Code Map

- `Scripts/Core/State/WorldStateManager.cs` -- `GetFact` switch (computed facts), `SetFact`, `CaptureFacts` / `RestoreFacts`
- `ScriptableObjects/Facts/SkillFact.cs` -- pattern for a computed fact (no Prefix, `Init`, debug `ToString`)
- `Scripts/Core/Time/GameClock.cs` -- `static Active`, `Now` (game seconds); `GameTime.SECONDS_PER_HOUR`
- `Scripts/Core/Save/SaveData.cs`, `SaveSystem.cs:243,441,467` -- facts / recipeCrafts capture, restore and failed-load reset (mirror for timestamps)
- `Scripts/AI/NPC/NpcTalkOpener.cs` -- `condition` is typed `WorldFact`; `NPCPresence.cs:148` `MarkWitnessedOpenersPlayed`
- `Editor/QuestExplorer/QuestValidator.cs:263` `IsStored` / `IsEvaluable`; `QuestDetailView.cs:833` `FactTypeName`
- `Data/NPCs/TentMate/Facts/WorldFact_Hobb_BelongingsRummaged` -- source for Hobb; TentMate `NPCPresence._talkOpeners[0]` in `StartingTown.unity`
- `Tests/EditMode/WorldStateManagerFactsTests.cs`, `TalkOpenerRulesTests.cs` -- existing test homes

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Facts/ElapsedFact.cs` -- computed `Fact` (`[CreateAssetMenu] Game/Facts/Elapsed Fact`), `_source`, `_hours` (min 0), `Source` / `Hours`, `Init`; plus pure static `ElapsedFactRules.IsElapsed(bool sourceTrue, double? setAt, double? now, double hours)` -- the delay rule, isolated for tests
- [x] `Scripts/Core/State/WorldStateManager.cs` -- `_factSetTimes` dictionary; `SetFact` records `GameClock.Active.Now` on false → true and removes it on false; `GetFact` handles `ElapsedFact`; `TryGetFactSetTime`, `CaptureFactSetTimes` / `RestoreFactSetTimes` (no events) -- single home for timing
- [x] `Scripts/Core/Save/SaveData.cs` + `SaveSystem.cs` -- optional `Dictionary<string, double> factSetTimes`; capture, restore with the facts, clear on a failed start-menu load -- the timers survive a reload
- [x] `Scripts/AI/NPC/NpcTalkOpener.cs` + `NPCPresence.cs` -- `condition` typed `Fact` (the serialized reference keeps working); the witness check evaluates the `ElapsedFact`'s source -- a seen theft cancels at once
- [x] `Editor/QuestExplorer/QuestValidator.cs` + `QuestDetailView.cs` -- `IsEvaluable` false for a null source; a validator issue when the source is null / not stored or hours < 0; `FactTypeName` "Elapsed" -- authoring safety
- [x] `Tests/EditMode/` -- `ElapsedFactRules` matrix rows; `WorldStateManager` timestamp first-set / re-set / unset / capture-restore -- edge cases covered
- [x] `Data/NPCs/TentMate/Facts/ElapsedFact_Hobb_BelongingsRummaged_2h.asset` + scene -- source the rummaged fact, 2 h; TentMate opener condition → this asset; save the scene -- the example
- [x] `Scripts/Core/State/WorldStateManager.cs` -- pending deadlines: `IsElapsed` registers an `ElapsedFact` whose source is true and delay not passed; `Update` raises `OnFactChanged` for each due one (re-checked through `IsElapsed`); cleared on `RestoreFacts` / `RestoreFactSetTimes`; skipped while `SaveSystem.IsLoading` -- quests and the quest log follow delays
- [x] `Tests/EditMode/ElapsedFactTests.cs` -- pending: fires once at the deadline, not before; no event when the source turned false; cleared on restore -- event path covered
- [x] Docs -- `Data/Facts/CLAUDE.md` (type row, naming `ElapsedFact_{SourceName}_{N}h`), `Scripts/Core/CLAUDE.md` (timestamps + save field), `Scripts/AI/CLAUDE.md` + `Data/NPCs/TentMate/CLAUDE.md` (delayed opener)

**Acceptance Criteria:**
- Given an unseen take from Hobb's box, when the player sleeps 2 h and talks to him, then the suspicion line plays first.
- Given a memory topic gated by an `ElapsedFact`, when the delay passes, then the topic appears with no other code change.
- Given a quest part gated by an `ElapsedFact`, when its delay ends, then the quest step completes without any other fact changing.
- Given the Quest Explorer, when an `ElapsedFact` is inspected in play mode, then it shows a read-only live badge.

## Spec Change Log

- 2026-11-19, review (blind + edge-case hunters, intent_gap, renegotiated with Valentin): quests and the quest log only re-evaluate on `OnFactChanged`, so an `ElapsedFact` quest part never noticed its delay ending. Amended the frozen Intent / Always / Never: `WorldStateManager` now raises `OnFactChanged` when a pending `ElapsedFact`'s deadline passes (the "no events when a delay ends" rule is lifted). Known-bad state avoided: quest steps stuck until an unrelated fact changes. KEEP: lazy pull evaluation, timestamps in `WorldStateManager` + `SaveData.factSetTimes`, the witness check on the undelayed source, V18, and the review patches (`_availableAfter` typed `Fact`, null-source skip in the witness check, invariant-culture debug key, a recorded time after now = elapsed).

## Design Notes

Why per fact and not per dialogue: every gate already funnels through `GetFact`, so a computed fact needs no change in the dialogue, memory or quest systems. The fact's "became true" moment is the natural start of the timer.

`ElapsedFact.ToString()` is for debugging only (`Elapsed.{source}.{hours}h`). It is never a storage key, and setting it as a fact is ignored like any other computed fact.

## Verification

**Commands:**
- Unity MCP `run_tests` EditMode -- expected: all green
- `read_console` after compile and in Play -- expected: no new errors or warnings
- MCP `quest_report` validate -- expected: no new issues

**Manual checks:**
- Play: take from Hobb's box unseen, talk at once (topics only), skip 2 h with the clock context menu, talk (suspicion line).

## Suggested Review Order

**The timed fact**

- Entry point: a computed fact, true N game hours after its source turned true.
  [`ElapsedFact.cs:13`](../../Assets/_Game/ScriptableObjects/Facts/ElapsedFact.cs#L13)

- The pure delay rule; no time, no clock or a future time count as elapsed.
  [`ElapsedFact.cs:48`](../../Assets/_Game/ScriptableObjects/Facts/ElapsedFact.cs#L48)

- Evaluation: reads the source's recorded time, registers the deadline while the delay runs.
  [`WorldStateManager.cs:241`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L241)

**Recording when facts turn true**

- First false → true keeps its time; false clears it; no clock records nothing.
  [`WorldStateManager.cs:224`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L224)

- Every stored-fact writer passes here.
  [`WorldStateManager.cs:131`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L131)

**Delay-end event (quests, quest log)**

- One `OnFactChanged` per due deadline, re-checked so stale deadlines re-register or drop.
  [`WorldStateManager.cs:265`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L265)

- Pending deadlines aren't saved: cleared on restore, rebuilt by the quest reseed.
  [`WorldStateManager.cs:352`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L352)

**Save / load**

- Optional field: older saves load with every true fact counted as long elapsed.
  [`SaveData.cs:126`](../../Assets/_Game/Scripts/Core/Save/SaveData.cs#L126)

- Restored with the facts, before the clock, same frame.
  [`SaveSystem.cs:446`](../../Assets/_Game/Scripts/Core/Save/SaveSystem.cs#L446)

**Hobb and the NPC gates**

- Opener condition accepts any fact; Hobb's points at the 2 h ElapsedFact (scene override).
  [`NpcTalkOpener.cs:18`](../../Assets/_Game/Scripts/AI/NPC/NpcTalkOpener.cs#L18)

- A witnessed theft reads the source without delay, so it cancels at once.
  [`NPCPresence.cs:156`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L156)

- `_availableAfter` can be delayed too.
  [`NPCPresence.cs:22`](../../Assets/_Game/Scripts/AI/NPC/NPCPresence.cs#L22)

**Authoring safety**

- V18: no source, nested, computed source or negative hours.
  [`QuestValidator.cs:279`](../../Assets/_Game/Editor/QuestExplorer/QuestValidator.cs#L279)

- Checked in quest parts and memory conditions.
  [`QuestValidator.cs:213`](../../Assets/_Game/Editor/QuestExplorer/QuestValidator.cs#L213)

**Peripherals**

- Rule, timestamp and delay-end event tests.
  [`ElapsedFactTests.cs:160`](../../Assets/Tests/EditMode/ElapsedFactTests.cs#L160)

- V18 tests.
  [`QuestValidatorTests.cs:139`](../../Assets/Tests/EditMode/QuestValidatorTests.cs#L139)

- Fact docs: type row, timing and event rules, naming.
  [`Facts/CLAUDE.md:20`](../../Assets/_Game/Data/Facts/CLAUDE.md#L20)
