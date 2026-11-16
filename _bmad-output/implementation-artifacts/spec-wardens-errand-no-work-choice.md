---
title: "Warden's Errand ends on the report, with no work-site choice"
type: 'refactor'
created: '2026-11-16'
status: 'done'
baseline_commit: '87f9e6a'
context: ['{project-root}/Assets/_Game/Data/Quests/CLAUDE.md', '{project-root}/Assets/_Game/Data/Facts/CLAUDE.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Warden's Errand ends with Voss asking "Where will you work?" and the pick completes the quest. The design has no assigned work site: the player is free to go to any site, meet its manager and do their quests (Valentin, 2026-11-16). The four work facts are read by nothing except the quest's completed parts.

**Approach:** Reporting back to Voss completes the quest. His report topic gets a one-time fact, `DialogueFact_Warden_Errand_Reported`, which becomes the quest's single completed part. The report reply ends the topic. The choice node, its four reply nodes and the four `Work*` facts are deleted, and the docs say the sites are open.

## Boundaries & Constraints

**Always:** Keep the notice deliveries, the step structure, the 100 XP `PlayerReward_WardensErrand_Completed` (it keys on the quest's Completed state) and the managers' "What's the work here?" topics unchanged. Delete each removed asset together with its `.meta`. Read quest data with the MCP `quest_report` tool and finish with a clean `quest_report audit` for `WardensErrand`.

**Ask First:** Any change to Voss's other topics, the managers' dialogues or the quest reward.

**Never:** Add a replacement work-assignment system. Add C# code (this is a data-only change). Migrate old dev saves.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Report | All four notices delivered, player picks "The notices are delivered." | Voss's reply, the topic ends, the quest completes (100 XP), the report topic is gone | N/A |
| Leave mid-topic | Player closes the dialogue before the reply ends | Fact not set, quest still active, topic offered again | N/A |
| Old dev save | A save that completed the quest through a `Work*` fact | The quest shows "Report back to Voss" again and the report topic is back. Reporting completes it (XP paid again) | Accepted: dev saves only |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/Quests/WardensErrand/Quest_WardensErrand.asset` -- `completedParts` hold the four `Work*` facts; `description` mentions "decides where"
- `Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Start_Warden_ErrandReport.asset` -- report topic, `dialogueFact` empty (set by `DialogueSystem.NotifyTopicCompleted` at chain end; also hides the topic via `NPCMemoryComponent`)
- `Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Text_Warden_ErrandReport.asset` -- "Pick where you sweat." → `Choice_Warden_ErrandWork`
- `Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Choice_Warden_ErrandWork.asset` + `Text_Warden_ErrandWork_{Quarry,Timber,Hunting,Farm}.asset` -- to delete
- `Assets/_Game/Data/Facts/DialogueFact_Warden_Errand_Work{Quarry,Timber,Hunting,Farm}.asset` -- to delete (only referenced by the choice and the quest)
- `Assets/_Game/Data/NPCs/Warden/Memories/Mem_Warden_ErrandReport.asset` -- unlocked by `QuestFact_WardensErrand_Step0`, invalidated by `QuestFact_WardensErrand_Completed`; unchanged
- `docs/World/Quests/WardensErrand.md`, `docs/World/Areas/area-001-company-encampment.md`, `Assets/_Game/Data/Quests/CLAUDE.md`, `_bmad-output/implementation-artifacts/deferred-work.md` -- docs to update

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Data/Facts/DialogueFact_Warden_Errand_Reported.asset` -- create a `DialogueFact` with `_nodeId: WardensErrand.Reported` (new GUID meta) -- the completion fact
- [x] `Start_Warden_ErrandReport.asset` -- set `dialogueFact` to the new fact -- one-time topic that records the report
- [x] `Text_Warden_ErrandReport.asset` -- new text in Voss's voice saying the managers know the player now and to go earn their keep at whichever site will have them, with `nextNode` empty -- the topic ends here
- [x] `Quest_WardensErrand.asset` -- `completedParts` = one part (new fact, entry "The managers know me now. Every work site is open to me."); description drops "Before he decides where" ("He has me carry…") -- the quest completes on the report
- [x] Delete `Choice_Warden_ErrandWork`, the four `Text_Warden_ErrandWork_*` and the four `DialogueFact_Warden_Errand_Work*` assets with their metas, then `refresh_unity(if_dirty)` -- no dead data
- [x] `docs/World/Quests/WardensErrand.md` -- completion, facts table and verification route describe the report-only ending and open work sites
- [x] `docs/World/Areas/area-001-company-encampment.md` -- the "Delivering notices" row ends with every site open, no choosing
- [x] `Assets/_Game/Data/Quests/CLAUDE.md` -- the `WardensErrand` line completes on the report
- [x] `deferred-work.md` -- strike "Work shifts" in the spec-area-1-wardens-errand section (resolved by this spec: no work-site choice by design)

**Acceptance Criteria:**
- Given the four notices are delivered, when the player finishes Voss's report topic, then the quest is completed, the 100 XP is granted and the topic no longer appears.
- Given the change, when `quest_report audit` runs, then `WardensErrand` has no errors and no missing-asset / dangling references, and a grep for `Errand_Work` across `Assets/` and `docs/` returns nothing.

## Verification

**Commands:**
- MCP `quest_report` (quest `WardensErrand`, then `audit`) -- expected: completed part = `DialogueFact_Warden_Errand_Reported`, its setter = `Start_Warden_ErrandReport`, no new issues
- `grep -rn "Errand_Work\|ErrandWork" Assets docs` -- expected: no matches

**Manual checks (if no CLI):**
- Play (After Arrival scenario): deliver the four notices, report to Voss → the reply ends the talk, a quest-completed toast appears and the XP is granted

## Suggested Review Order

**Completion moves to the report**

- Entry point: the single completed part is now the one-time report fact.
  [`Quest_WardensErrand.asset:21`](../../Assets/_Game/Data/Quests/WardensErrand/Quest_WardensErrand.asset#L21)

- The report topic records the fact; it is set only when the chain ends.
  [`Start_Warden_ErrandReport.asset:18`](../../Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Start_Warden_ErrandReport.asset#L18)

- Where the fact gets written: at topic completion, never on open.
  [`DialogueSystem.cs:273`](../../Assets/_Game/Scripts/World/DialogueSystem.cs#L273)

- Voss's reply now ends the topic instead of asking for a site.
  [`Text_Warden_ErrandReport.asset:15`](../../Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Text_Warden_ErrandReport.asset#L15)

- New fact, key `WardensErrand.Reported`.
  [`DialogueFact_Warden_Errand_Reported.asset:15`](../../Assets/_Game/Data/Facts/DialogueFact_Warden_Errand_Reported.asset#L15)

- Journal description no longer promises an assignment.
  [`Quest_WardensErrand.asset:17`](../../Assets/_Game/Data/Quests/WardensErrand/Quest_WardensErrand.asset#L17)

**Docs: every site is open**

- Quest doc: report-only ending, open sites, facts table.
  [`WardensErrand.md:8`](../../docs/World/Quests/WardensErrand.md#L8)

- Area doc's Warden's Tasks row.
  [`area-001-company-encampment.md:168`](../../docs/World/Areas/area-001-company-encampment.md#L168)

- Folder rule line for the quest.
  [`CLAUDE.md:51`](../../Assets/_Game/Data/Quests/CLAUDE.md#L51)

- Backlog: "Work shifts" struck as resolved by design.
  [`deferred-work.md:678`](deferred-work.md#L678)
