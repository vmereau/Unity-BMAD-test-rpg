---
title: 'Tam greeting opener gates the stash hint'
type: 'feature'
created: '2026-11-15'
status: 'done'
baseline_commit: '3eb1328da52602ea061f9cb79162d7f0cc709a13'
context:
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every Tam topic, including the HiddenStash hint, is available the first time the player meets him, which makes no sense for a stranger. Dialogue lines also use em-dashes, which the project wants banned.

**Approach:** Replace Tam's greeting with an "Are you alright?" opener ending on a three-way choice (offer help / mock him / leave). Offering help unlocks the stash and camp-people topics. Mocking closes them permanently and leaves only a cold one-liner topic. Leaving keeps the opener open. In the stash topic Tam says he is hungry and would take food from anywhere; only if the player says they have no money does he point at Brann's chest, which starts the quest. (Accepting food items for the quest is a later spec.) Add a no-em-dash rule (and the opener pattern) to `DIALOGUE.md` and scrub existing em-dashes from all NPC dialogue assets.

## Boundaries & Constraints

**Always:** No em-dashes in any dialogue text. Every text node ≤300 chars. Keep Tam dry and short (see his `CLAUDE.md`): he never asks for help, he points at things. Reuse existing asset GUIDs where an asset survives (`Start_BeatenWorker_Greetings`, StashHint nodes, memories, `DialogueFact_HiddenStash_Hint`) so quest wiring and saves keep working. Save assets so every new node has an `_id:` line.

**Ask First:** Changing the `HiddenStash` quest's facts or steps, or touching `StashReported`.

**Never:** Code changes to the dialogue system. Hand-editing `NPCEntity.memories`.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| First meeting | no Tam facts | Only "Are you alright?" topic |
| Offer help | picks "Let me help you." | Refusal + warning lines; opener gone; "Who should I watch out for?" and "Is there really nothing I can do for you?" appear |
| Mock | picks "You probably had it coming. Work harder." | Short dry reply; opener gone; stash and camp people never appear; repeatable "Got a moment?" → "Not for you." topic appears |
| Leave | picks "Then I'll leave you be." | Short reply; opener still listed next time |
| Stash, will find food | picks "I'll find you something." | Short reply; no fact; stash topic stays listed, quest not started |
| Stash, no money | picks "I don't have any money for food." | Chest lines; `DialogueFact_HiddenStash_Hint` set (quest starts); stash topic gone |
| Stash taken before any talk | `World.HiddenStash.Taken` set, no hint | StashReported still hidden (needs hint), unchanged behaviour |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Greeting/` -- opener topic (start, choice, branch texts)
- `Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/StashHint/` -- hint chain, starts the quest via `DialogueFact_HiddenStash_Hint`
- `Assets/_Game/Data/NPCs/BeatenWorker/Memories/` -- topic visibility conditions
- `Assets/_Game/Data/Facts/` -- new branch facts
- `Assets/_Game/Data/Quests/HiddenStash/Quest_HiddenStash.asset` -- description / startPart entry text only
- `Assets/_Game/Data/NPCs/DIALOGUE.md` -- dialogue authoring rules
- `docs/World/Quests/HiddenStash.md`, `docs/World/NPCs/npc-002-beaten-worker.md`, `Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md` -- docs to sync

## Tasks & Acceptance

**Execution:**
- [x] `Data/Facts/DialogueFact_BeatenWorker_OfferedHelp.asset` (`_nodeId: BeatenWorker.Greeting.OfferedHelp`), `DialogueFact_BeatenWorker_Mocked.asset` (`BeatenWorker.Greeting.Mocked`) -- create -- branch tracking
- [x] `Greeting/` -- `Start_BeatenWorker_Greetings` text "Are you alright?", nextNode → new `Choice_BeatenWorker_Greetings` (text: "Tam. And yes, that was the Warden. Not my first time. I'll manage."). Choices: [0] "Let me help you." fact OfferedHelp → `Text_..._Help` "Help? You got here today. I don't see what you could do for me. Not yet, anyway." → `Text_..._Help_2` "And don't go offering help to strangers like that. Not in this place. People here take more than you offer." (end); [1] "You probably had it coming. Work harder." fact Mocked → `Text_..._Mock` "Sure. I'll tell the Warden you said so. He'll like you." (end); [2] "Then I'll leave you be." no fact → `Text_..._Leave` "Best idea anyone's had all day." (end). Delete old `Text_BeatenWorker_Greetings` -- opener
- [x] `StashHint/` -- start text "Is there really nothing I can do for you?", `dialogueFact` cleared (moves to choice [1]); `Text_BeatenWorker_StashHint` "Hm. You don't give up. Fine. I'm hungry. That's the truth of it." → new `Choice_BeatenWorker_StashHint` "Bring me something to eat, if you want. I don't care where it comes from." Choices: [0] "I'll find you something." no fact → `Text_..._StashHint_Later` "Don't take too long. I'm not getting any fatter." (end); [1] "I don't have any money for food." fact `DialogueFact_HiddenStash_Hint` → `_2` "Brann keeps bread and apples in a chest in the gang house, by the lake. He guards it all day. After six he goes to the gang fire." → `_3` "Nobody said you had to pay. And if you get caught, you never met me." (end) -- food from anywhere, chest only as the no-money fallback
- [x] `Dialogues/Cold/` + `Mem_BeatenWorker_Cold` -- `Start_BeatenWorker_Cold` "Got a moment?" (repeatable) → `Text_BeatenWorker_Cold` "Not for you." (end); memory unlock [Mocked] -- cold one-liner after mocking
- [x] `Memories/` -- Greetings: invalidation [OfferedHelp, Mocked]; CampPeople: unlock [OfferedHelp]; StashHint: unlock [OfferedHelp], invalidation [Hint, Mocked]; StashReported unchanged; run `Game/Dev/Sync All NPC Memories` -- gating
- [x] `Quest_HiddenStash.asset` -- description + startPart entry: Tam is hungry and doesn't care where food comes from; with no money, he pointed me at Brann's chest and said he never met me if I'm caught -- journal matches new framing
- [x] Other NPC assets with `—` (Blacksmith ×3, Cook ×3, RacketGuard, TentMate) -- rewrite with commas / periods, meaning unchanged -- rule compliance
- [x] `DIALOGUE.md` -- add a "Writing Rules" section: no em-dashes (use a comma, period or "..."), plus the greeting-opener pattern (opener topic → attitude choice with facts → later topics gated on those facts) -- authoring guidance
- [x] Docs -- update Tam `CLAUDE.md` (topic gating), npc-002 topics table, `HiddenStash.md` intro + facts table -- keep docs true

**Acceptance Criteria:**
- Given the edited assets, when grepping `Data/NPCs` for `—` or `—`, then no match.
- Given the quest, when running MCP `quest_report` audit on `HiddenStash`, then no new errors versus before.
- Given `NPC_BeatenWorker`, when inspecting `memories`, then it lists exactly the five Tam memories (four existing + Cold).

## Verification

**Commands:**
- `grep -rlE 'u2014|—' Assets/_Game/Data/NPCs --include=*.asset` -- expected: no output
- `grep -L '_id:' <new node assets>` -- expected: no output

**Manual checks:**
- MCP `quest_report` (`HiddenStash`, `memory` for each Tam memory) shows the conditions above; Unity console has no errors.

## Suggested Review Order

**Opener and attitude facts**

- Entry point: three attitude choices, each fact gates Tam's later topics.
  [`Choice_BeatenWorker_Greetings.asset:18`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Greeting/Choice_BeatenWorker_Greetings.asset#L18)

- Gating summary for every Tam topic in one table.
  [`BeatenWorker/CLAUDE.md:61`](../../Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md#L61)

**Topic visibility**

- Stash topic needs OfferedHelp; closed by Hint or Mocked.
  [`Mem_BeatenWorker_StashHint.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_StashHint.asset#L15)

- Camp people: OfferedHelp, not Mocked (review patch).
  [`Mem_BeatenWorker_CampPeople.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_CampPeople.asset#L15)

- Cold one-liner only after mocking.
  [`Mem_BeatenWorker_Cold.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_Cold.asset#L15)

- Opener closes on either attitude fact; leave keeps it.
  [`Mem_BeatenWorker_Greetings.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_Greetings.asset#L15)

**Stash quest start**

- Quest-start fact moved onto the "no money" choice; other choice keeps topic open.
  [`Choice_BeatenWorker_StashHint.asset:18`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/StashHint/Choice_BeatenWorker_StashHint.asset#L18)

- Journal text matches the new framing.
  [`Quest_HiddenStash.asset:17`](../../Assets/_Game/Data/Quests/HiddenStash/Quest_HiddenStash.asset#L17)

**Rules and docs**

- No-em-dash rule and greeting-opener pattern.
  [`DIALOGUE.md:26`](../../Assets/_Game/Data/NPCs/DIALOGUE.md#L26)

- Quest doc: new start path and mock lockout.
  [`HiddenStash.md:8`](../../docs/World/Quests/HiddenStash.md#L8)

- NPC design doc topics table.
  [`npc-002-beaten-worker.md:88`](../../docs/World/NPCs/npc-002-beaten-worker.md#L88)

- Example of the em-dash scrub in other NPCs.
  [`Text_TentMate_Arrival_2.asset:15`](../../Assets/_Game/Data/NPCs/TentMate/Dialogues/Arrival/Text_TentMate_Arrival_2.asset#L15)
