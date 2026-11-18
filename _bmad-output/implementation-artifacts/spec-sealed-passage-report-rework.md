---
title: 'Sealed Passage: report rework (truth / lie / latch) and Dace line fix'
type: 'feature'
created: '2026-11-18'
status: 'done'
baseline_commit: 'bc66f0e845cc88b6d91e650098ceede98769a46b'
context:
  - '{project-root}/docs/World/Quests/SealedPassage.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/QuarryWorker/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Bren's report scene is flat. The two player options are near-identical, the ending choice changes nothing, and Bren's "half the story" setup can't be answered with a kind lie. The most chilling fact (the cleft door opens from the inside, so Odo stayed by choice) is never said. Separately, Dace's notice line ("the men won't go near… nobody works that face") contradicts the quest canon, where Dace opened a cut there and sealed it with a door.

**Approach:** Restructure Bren's report into truth/lie, then token/nothing, plus a "door opens from inside" line shown only when the player actually lifted the `InsideLatch`. Add a third completion fact (`ReportLied`, no later payoff yet), branch the After topic per ending, add "friend" to Bren's lines, and rewrite Dace's notice line.

## Boundaries & Constraints

**Always:** Bren's voice per his `CLAUDE.md` + DIALOGUE.md Voice (full sentences, no ellipses, ≤300 chars per node). Gate-only memories have no start dialogue (pattern: `Mem_RacketGuard_TollCanDefer`). Every new fact has an indexed setter, so `quest_report SealedPassage` stays clean. Nodes created by script get `_id` = asset GUID and are saved.

**Ask First:** Changing rewards beyond the ones listed here; any change to the door/lock/theft behaviour.

**Never:** No consumer of `ReportLied` beyond Bren's After topic (a later beat may read it). No world prop changes (token on bench, cutters' clue, breathing audio are out of scope). Don't touch the Greeting / SouthFace flow except the accept line's "friend".

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|---|---|---|
| Truth + token | holds `Item_Odo_TallyToken` | Token row visible; pick → token consumed, `ReportToken`, +150 XP +10 g, After_Token |
| Truth, no token | token sold/dropped | Token row hidden; "I couldn't bring anything back." → `ReportTold`, +150 XP, After_Told |
| Lie | any | "He got out…" → `ReportLied`, quest completed, +150 XP, After_Lied; token (if held) kept |
| Left via latch | `WorldFact_SealedPassage_Unlatched` true | Latch row shown on the second choice; pick → Bren reacts, `ReportLatch` set, back to the choice with the row hidden |
| Entered by key/pick only | latch never used (door unlocked, latch not interactable) | Latch row never shown |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/DoorLatch.cs` -- inner-face latch; `Interact()` unlocks + opens
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs:1223` -- builds `InsideLatch` (rerunning it must keep the fact wiring)
- `Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs:56,159` -- scene fact-setter scan (open + closed scenes); `QuestIndexTypes.cs:32` `FactLinkSource`
- `Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/Report/`, `.../After/`, `.../SouthFace/Text_QuarryWorker_SouthFace_Accept.asset`
- `Assets/_Game/Data/NPCs/QuarryWorker/Memories/` -- `Mem_QuarryWorker_After` and the new memories
- `Assets/_Game/Data/Quests/SealedPassage/Quest_SealedPassage.asset`, `Assets/_Game/Data/Rewards/PlayerReward_SealedPassage_*`
- `Assets/_Game/Data/NPCs/QuarryOverseer/Dialogues/Notice/Text_QuarryOverseer_Notice_3.asset` -- Dace's south-face line

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/World/DoorLatch.cs` -- optional `[SerializeField] WorldFact _unlatchedFact`, set true in `Interact()` after opening -- the latch-gated report line
- [x] `Editor/QuestExplorer/QuestIndexCollector.cs` + `QuestIndexTypes.cs` -- index `DoorLatch._unlatchedFact` as a scene setter (open and closed scenes, label "Door latch: {name}") -- avoids V3 "never set"
- [x] `Editor/World/Area1SitesBuilder.cs` -- assign `WorldFact_SealedPassage_Unlatched` to the `InsideLatch`; set it on the scene instance too
- [x] `Data/Facts/` -- `WorldFact_SealedPassage_Unlatched`, `DialogueFact_QuarryWorker_ReportLied`, `DialogueFact_QuarryWorker_ReportLatch`
- [x] `QuarryWorker/Dialogues/Report/` -- rebuild per Design Notes (choice 1: truth/lie; choice 2: latch/token/nothing); reuse the existing nodes where the text survives
- [x] `QuarryWorker/Memories/` -- `Mem_QuarryWorker_KnowsLatch` (gate-only: unlock `Unlatched`, invalidate `ReportLatch`); turn `Mem_QuarryWorker_After` into `After_Told` (unlock `ReportTold`); add `After_Token`, `After_Lied` with their own `After/` chains (same label "How are you holding up?")
- [x] `Quest_SealedPassage.asset` -- completed part `ReportLied`, entry "I told Bren that Odo got out and went up to the fort. He wanted to believe it."; `Mem_QuarryWorker_Report` invalidation += `ReportLied`
- [x] `Data/Rewards/PlayerReward_SealedPassage_ReportLied.asset` -- 150 XP, 0 gold, synced to `PlayerRewards`
- [x] SouthFace accept text -- "Then I owe you, friend, and…"
- [x] `Text_QuarryOverseer_Notice_3` -- Dace line per Design Notes
- [x] Docs -- `docs/World/Quests/SealedPassage.md` (facts, completion, NPC dialogue, rewards, flow), `QuarryWorker/CLAUDE.md` Quest Role, `Data/Facts/CLAUDE.md` (DoorLatch as a WorldFact setter), `Editor/QuestExplorer/CLAUDE.md` if it lists setter sources

**Acceptance Criteria:**
- Given the quest is completed by any of the three report facts, when talking to Bren, then exactly one After topic is offered and it matches that ending.
- Given the edit, when running `quest_report SealedPassage` and `quest_report audit`, then there are no issues and no new ones.
- Given Dace's notice topic, when it plays, then no line denies that a cut was worked behind the south face.

## Design Notes

Report flow (final wording may be polished, keep ≤300 chars):

```
Choice 1 (NPC, unchanged): "You found him? Then tell me straight. I've had enough of people being kind to me with half the story."
 ├ "He's dead. He sat against the old stones, facing them, without a mark on him."
 │   → Choice 2 (NPC): "Dead, then. I knew it the morning I saw that door, and it still lands like a dropped block. Facing the stones, like he was waiting on something. Go on, friend, tell me the rest."
 │     ├ [KnowsLatch] "The door opens from the inside. He could have walked out." [ReportLatch]
 │     │   → "Opens from the inside? Then nobody shut him in at all. He sat there with the way out at his back and stayed. I've been angry at the Company for a month, friend, and now I don't know who to be angry at." → Choice_QuarryWorker_Report_Rest ("Well. That's one more thing to lie awake over. Go on, then, friend, was there anything of his left to bring back?": token / nothing) — review patch: re-entering Choice 2 repeated "Dead, then"
 │     ├ [token, hide] "Here. His tally token." → existing Token text → "I'll keep the tally, somebody should. Here, take this, friend. It's not much, but I'd rather it went to you than to Dace's ledger." [ReportToken]
 │     └ "I couldn't bring anything back." → existing Told text [ReportTold]
 └ "He got out. He's up at the fort with the other cutters."
     → "Up at the fort, with the lads I never see when I carry stone up there. Well, I'll look harder next week, then. Thank you for telling me, friend." [ReportLied]
```

After_Token: "I keep his tally on my bench now. Some days I cut a few blocks over and tell myself they're his, and Dace's ledger doesn't know the difference." After_Told: existing line. After_Lied: "I asked after Odo up at the fort this week. Nobody knew the name, but nobody up there knows any of our names, so that's no answer either way, is it?"

Dace: "And keep off the south face. That door is Company business, and so is whatever's behind it. The men tell each other stories about that crack at night. I'd rather they cut stone than tell stories, and so would you, if you want to eat."

## Verification

**Commands:**
- Unity EditMode tests (`run_tests`) -- expected: all pass
- MCP `quest_report SealedPassage`, `quest_report audit` -- expected: no issues

**Manual checks:**
- `read_console`: no compile errors; every new/edited node `.asset` has a non-empty `_id`
- In play mode (or via facts): the report rows match each matrix scenario

## Suggested Review Order

**Report dialogue (the player-facing change)**

- Entry point: truth vs the kind lie, the lie completes the quest on its own fact.
  [`Choice_QuarryWorker_Report.asset:13`](../../Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/Report/Choice_QuarryWorker_Report.asset#L13)

- After the truth: latch row (memory-gated), token (hidden when missing), nothing.
  [`Choice_QuarryWorker_Report_Truth.asset:13`](../../Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/Report/Choice_QuarryWorker_Report_Truth.asset#L13)

- Latch reaction leads to a short follow-up instead of repeating "Dead, then".
  [`Choice_QuarryWorker_Report_Rest.asset:13`](../../Assets/_Game/Data/NPCs/QuarryWorker/Dialogues/Report/Choice_QuarryWorker_Report_Rest.asset#L13)

- Gate-only memory: unlocked by lifting the latch, closed once the line is said.
  [`Mem_QuarryWorker_KnowsLatch.asset:13`](../../Assets/_Game/Data/NPCs/QuarryWorker/Memories/Mem_QuarryWorker_KnowsLatch.asset#L13)

- Third completion part for the lie; rewards mirror the "told" ending.
  [`Quest_SealedPassage.asset:1`](../../Assets/_Game/Data/Quests/SealedPassage/Quest_SealedPassage.asset#L1)

**Latch fact (code)**

- Lifting the latch records the fact; warns instead of failing silently without Core.
  [`DoorLatch.cs:61`](../../Assets/_Game/Scripts/World/DoorLatch.cs#L61)

- Pure setter kept testable.
  [`DoorLatch.cs:67`](../../Assets/_Game/Scripts/World/DoorLatch.cs#L67)

- Scene instance wiring (the builder does the same on rebuild).
  [`Area1SitesBuilder.cs:1231`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L1231)

**Quest Explorer indexing**

- Loaded-scene scan reads `_unlatchedFact` so V3 sees the setter.
  [`QuestIndexCollector.cs:60`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L60)

- Closed-scene scan: whole-field, escaped match per setter script.
  [`QuestIndexCollector.cs:217`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L217)

- Source tag for the explorer rows.
  [`QuestDetailView.cs:753`](../../Assets/_Game/Editor/QuestExplorer/QuestDetailView.cs#L753)

**Peripherals**

- Tests: fact write and field-name matching.
  [`DoorLatchTests.cs:27`](../../Assets/Tests/EditMode/DoorLatchTests.cs#L27)

- Dace's rewritten south-face line.
  [`Text_QuarryOverseer_Notice_3.asset:15`](../../Assets/_Game/Data/NPCs/QuarryOverseer/Dialogues/Notice/Text_QuarryOverseer_Notice_3.asset#L15)

- Quest doc (dialogue, facts, Part C checklist).
  [`SealedPassage.md:352`](../../docs/World/Quests/SealedPassage.md#L352)
