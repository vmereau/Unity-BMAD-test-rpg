---
title: 'Venn seen at night: pre-quest sighting, night patrol line, Pell and Venn follow-ups'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: 'c0e06e3a'
context:
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/Facts/CLAUDE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/docs/World/Quests/TitheBarnShortfall.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Meeting Venn at the barn at night before TitheBarnShortfall sets `ThiefCaught`. That opens his confrontation out of context, pays step XP early, and closes the investigation topics. Nothing in the world remembers the sighting, and Venn has no night line.

**Approach:**
- **Pre-quest sighting.** A night encounter sets a new `WorldFact_TitheBarn_VennSeenAtNight` (not `ThiefCaught`). The catch trigger gets a quest-started gate, so the catch / deal works only once the quest is on.
- **Venn's night patrol line.** Repeatable at night: he's patrolling, go back to the camp.
- **Pell.** His start topic gets a sighting option that leads to the "real proof" warning (`NeedProof`) and then the quest start.
- **Venn by day.** Once seen, his brush-off is replaced by a day topic where he shushes the player and says to meet him at the barn door after midnight.

## Boundaries & Constraints

**Always:** Gates follow the existing `WorldFactTriggerRules` pattern. `HourWindowFact` is computed, never stored, and pull-only (memory / choice conditions, never quest parts or talk openers). Venn's voice: friendly, "friend", never says "steal". No em-dashes, max 300 chars per node. The investigation topics (Pim, Gerrit, Aldric) stay unaffected by the sighting.

**Ask First:** Changing `QuestEventsManager` (it pays step rewards before a quest starts: deferred).

**Never:** No `NpcTalkOpener`. No change to Venn's routine or to the existing hour / NPC gates. No change to the endings.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|---|---|---|
| Night, no quest | 00–03, Venn at the door, player near | `VennSeenAtNight` set, `ThiefCaught` not set. Talk: night patrol topic only (greeting hidden) |
| Pell start after the sighting | Seen, topic open | Extra choice "I've seen Venn around the barn at night…" → warning line (sets `NeedProof`) → "Then I'll find you proof." (sets `Accepted`) → the usual accept lines |
| Pell start, not seen | — | Sighting choice hidden; flow unchanged |
| Day, quest on, seen | 06–21 at the gate | Venn: day "night talk" topic (shush, meet me tonight) instead of the brush-off; counts for "Ask around the farm" |
| Night, quest on | player at the door | `ThiefCaught` set, Caught topic (deal / refuse), night patrol topic hidden |
| Trigger without required fact | `_requiredFact` null | Gate passes |

</frozen-after-approval>

## Code Map

- `Scripts/World/WorldFactTrigger.cs`, `WorldFactTriggerRules.cs`; `Assets/Tests/EditMode/WorldFactTriggerRulesTests.cs`
- `ScriptableObjects/Facts/ElapsedFact.cs` -- computed-fact template; `Scripts/Core/State/WorldStateManager.cs:103` `GetFact`
- `Scripts/Core/Time/GameTime.cs:50` `IsHourInWindow`; `Editor/QuestExplorer/QuestDetailView.cs` `FactTypeName`
- `StartingTown.unity` `TitheBarnShortfall/BarnDoorTrigger` (box child `Box`)
- `Data/NPCs/FarmReeve/Dialogues/Shortfall/` (`Choice_FarmReeve_Shortfall`); `Data/NPCs/BarnGuard/` memories / dialogues

## Tasks & Acceptance

**Execution:**
- [x] `WorldFactTriggerRules` + `WorldFactTrigger` -- Optional `Fact _requiredFact`. `FactGatePasses(hasFact, factTrue)`, checked in `GatesPass`. Tests: none / false / true.
- [x] `ScriptableObjects/Facts/HourWindowFact.cs` (new) + `WorldStateManager.GetFact` + `QuestDetailView` ("Hour") -- True while `GameClock.Active` has an hour in [start, end) (wraps; start == end = all day); no clock = false. `ToString` = `Hour.{s}-{e}`. Pure rule plus `HourWindowFactTests`.
- [x] `Data/Facts/` -- `HourWindowFact_Night_00_03`, `WorldFact_TitheBarn_VennSeenAtNight`.
- [x] `StartingTown.unity` -- `BarnDoorTrigger._requiredFact` = `QuestFact_TitheBarnShortfall_Started`. Duplicate it as `BarnSightingTrigger` (same box, hours, NPC / spot gates, no required fact) → `VennSeenAtNight`.
- [x] `FarmReeve/Dialogues/Shortfall/` -- Gate memory `Mem_FarmReeve_GateSeenVenn` (unlock `VennSeenAtNight`). New choice in `Choice_FarmReeve_Shortfall`, before "Not my problem.": "I've seen Venn around the barn at night. Is that suspicious?" (requiredMemory gate, sets `NeedProof`) → `Text_FarmReeve_Shortfall_Venn` (be careful accusing a Company guard, a guard at night is a guard on duty until shown otherwise, come back with real proof) → `Choice_FarmReeve_Shortfall_Venn`, single option "Then I'll find you proof." (sets `Accepted`) → `Text_FarmReeve_Shortfall_Accept_2`.
- [x] `BarnGuard/` -- `Mem_BarnGuard_NightPatrol` + `Dialogues/NightPatrol/` ("What are you doing out here?", repeatable). Unlock [Night]; invalidate [`ThiefCaught`]. He's walking the fields for the fort, a worker has no business here at this hour, back to the camp. `Mem_BarnGuard_Greeting` also invalidated by Night.
- [x] `BarnGuard/` -- `Mem_BarnGuard_NightTalk` + `Dialogues/NightTalk/` ("About last night, at the barn."). Unlock [Started, `VennSeenAtNight`]; invalidate [`DialogueFact_BarnGuard_BarnTalk`, Night, `ThiefCaught`, Completed, Failed]. Start node sets `DialogueFact_BarnGuard_BarnTalk`. Venn shushes the player (people get ideas, ideas get repeated) and says to come to the barn door after midnight, alone, and they'll talk. `Mem_BarnGuard_BarnTalk` also invalidated by `VennSeenAtNight`. Neutralise the step part entry ("I asked Venn at the fields gate about Pell's grain.").
- [x] Docs -- `Scripts/World/CLAUDE.md` (trigger row), `Data/Facts/CLAUDE.md` (HourWindowFact row, pull-only), quest doc (beats, facts, memories, dialogue), `BarnGuard/CLAUDE.md`, `FarmReeve/CLAUDE.md`, `deferred-work.md` (`QuestEventsManager` step events before start).

**Acceptance Criteria:**
- Given no quest, when the player reaches the door 00–03 with Venn there, then only `VennSeenAtNight` is set, and Pim's / Gerrit's / Aldric's topics still open once the quest starts.
- Given the sighting, when Pell's start topic is played with the sighting choice, then `NeedProof` and `Accepted` are both set, the quest starts, and the Tam / Aldric tally leads are available.
- Given the quest is started, when the player returns at night, then Venn can be caught and the deal / refuse choice works.
- EditMode tests pass, no console errors, `quest_report TitheBarnShortfall` reports no Error / Warning.

## Spec Change Log

- **Review 1 (2026-11-19):** sighting branch set `NeedProof` before `Accepted`; swapped, sighting-specific accept line added, BarnTalk hidden at night, NightPatrol closed on `ReportedGuard`, fact gate moved into `GatesPass`, unused `Init` removed.

## Design Notes

A choice sets one fact, so the sighting option sets `Accepted` (quest starts) and the one-option follow-up "Then I'll find you proof." sets `NeedProof`, then ends on its own accept line (`Text_FarmReeve_Shortfall_VennAccept`). Started always comes first, so no step completes before the quest and a cut dialogue never leaves `NeedProof` without a quest. Memory activity is never cached, so a computed invalidation (Night) acts as "hidden while true".

## Verification

**Commands:**
- MCP `run_tests` EditMode -- expected: `WorldFactTriggerRulesTests`, `HourWindowFactTests` pass
- MCP `read_console` Error -- expected: none
- MCP `quest_report TitheBarnShortfall` -- expected: no Error / Warning

**Manual checks:**
- No quest, night at the door → patrol line, no catch. Next day: Pell sighting branch. Day: Venn's shush topic. Night: caught, deal / refuse.

## Suggested Review Order

**Gates**

- Entry point: optional required fact, checked first in the shared gate pass.
  [`WorldFactTrigger.cs:108`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L108)

- Computed hour fact: pull-only, no change event.
  [`HourWindowFact.cs:23`](../../Assets/_Game/ScriptableObjects/Facts/HourWindowFact.cs#L23)

- Dispatch before the stored-fact fallback.
  [`WorldStateManager.cs:112`](../../Assets/_Game/Scripts/Core/State/WorldStateManager.cs#L112)

**Scene**

- Ungated sighting trigger beside the quest-gated catch trigger.
  [`StartingTown.unity:207250`](../../Assets/_Game/Scenes/StartingTown.unity#L207250)

**Dialogue data**

- Sighting option starts the quest; follow-up sets NeedProof.
  [`Choice_FarmReeve_Shortfall.asset:18`](../../Assets/_Game/Data/NPCs/FarmReeve/Dialogues/Shortfall/Choice_FarmReeve_Shortfall.asset#L18)

- Venn by night: patrol line, hidden once caught or reported.
  [`Mem_BarnGuard_NightPatrol.asset:15`](../../Assets/_Game/Data/NPCs/BarnGuard/Memories/Mem_BarnGuard_NightPatrol.asset#L15)

- Venn by day after the sighting: shush, meet me tonight.
  [`Mem_BarnGuard_NightTalk.asset:15`](../../Assets/_Game/Data/NPCs/BarnGuard/Memories/Mem_BarnGuard_NightTalk.asset#L15)

**Tests**

- Fact gate and hour-window rules.
  [`HourWindowFactTests.cs:1`](../../Assets/Tests/EditMode/HourWindowFactTests.cs#L1)
