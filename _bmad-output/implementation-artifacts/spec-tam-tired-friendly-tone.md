---
title: 'Tam speaks tired but friendly, closes up sadly when mocked'
type: 'feature'
created: '2026-11-16'
status: 'done'
baseline_commit: '197a8e6cd604bb351a960f077ebf053079c918f5'
context:
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Tam (BeatenWorker) reads as rude and sardonic ("Best idea anyone's had all day.", "Then stop wasting my time.", "Not for you."), but he is meant to be a tired, gentle man who is friendly to the player. Even when mocked he should not bite back: he closes up, sad and worn out.

**Approach:** Rewrite the text of every existing Tam dialogue node in that voice, keep the meaning, information and gating of each node, and rewrite his identity docs (Tam `CLAUDE.md`, `npc-002`) so future dialogue keeps the new voice. Mocking stays a permanent lockout; only its tone changes. The theft hint keeps pointing at Brann's chest, with a softer delivery.

## Boundaries & Constraints

**Always:** Edit only `text` fields (node speech, choice prompts). Keep player-voice labels (topic starts, choice labels) unless listed below. Keep every asset, GUID, `nextNode`, fact and memory as is. No em-dashes, each node ≤300 chars. Tam never asks outright for help, never sneers, never threatens; he is warm in a quiet, worn-out way.

**Ask First:** Adding, removing or re-gating any topic, choice or fact.

**Never:** Friendship-after-feeding variants or new topics (separate spec). Code changes. Quest journal text changes.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|----------|-------|----------|
| Mock | opener, picks "You probably had it coming." | Sad, tired reply, no jab; Cold topic afterwards, lockout unchanged |
| Cold | Mocked, "Got a moment?" | Tired, withdrawn line, not hostile |
| No money | StashHint or Feed "no money" choice | Same chest info (Brann, gang house by the lake, gang fire after six), asks the player to be careful and not name him |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/**` -- 26 node assets; only `text` changes
- `Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md` -- canonical identity: personality, speech style, rules
- `docs/World/NPCs/npc-002-beaten-worker.md` -- design doc: identity, personality, sample lines, state changes
- `_bmad-output/implementation-artifacts/deferred-work.md` -- friendship follow-up

## Tasks & Acceptance

**Execution:**
- [x] `Dialogues/Greeting/` -- Choice prompt "Tam. That was the Warden, yes. It's not the first time. I'll be fine, I just need to sit a while."; Help "That's kind of you. Really. But you only just got here, and there's not much anyone can do for me today."; Help_2 "Just be careful who you offer that to. Not everyone here is like you. Some people take more than you give."; Mock "...Maybe. I'm too tired to argue."; Leave "Thank you. I'll just sit here a while. Come by later, if you like." -- opener voice
- [x] `Dialogues/Cold/Text_BeatenWorker_Cold` -- "...Not really. I'm tired. Leave me be." -- sad withdrawal
- [x] `Dialogues/CampPeople/Text_BeatenWorker_CampPeople` -- "Voss hurts people in public, so the rest of us remember. Harl takes whatever he can carry. And keep away from the nice tents. I'd rather not see you up on that stage." -- same info, caring
- [x] `Dialogues/StashHint/` -- Text "You're kind. Alright... I'm hungry. That's the truth of it. I haven't eaten since yesterday."; Choice prompt "If you come across something to eat, I'd be grateful. I'm not picky."; Later "Thank you. I'll be here. Not going anywhere, as you can see."; _2 "Brann keeps bread and apples in a chest in the gang house, by the lake. He sits on it all day, but after six he goes to the gang fire."; _3 "I'm not asking you to steal. But nobody misses that food but him. Be careful, and if he catches you, please don't mention my name." -- soft hint
- [x] `Dialogues/Feed/` -- Choice prompt "For me? Let's see."; Food "Food. Real food. I can't remember the last time someone shared with me."; Food_2 "Thank you. Truly."; Meal "A cooked meal? Out here?"; Meal_2 "I don't know what to say. Nobody's done something like this for me in years. I won't forget it."; NotYet "That's alright. I've waited longer for less." -- grateful
- [x] `Dialogues/StashReported/Text_BeatenWorker_StashReported` -- "You really did it. Keep what you took out of sight, Brann counts everything he thinks is his. I don't want you hurt over me." -- worried approval
- [x] `BeatenWorker/CLAUDE.md` -- Traits tired, gentle, observant, quietly kind; Tone soft, weary, friendly; quirks/taboo/sample/rules to match (never sneers; when mocked he withdraws, never retaliates); Background drops "stopped pretending" cynicism -- identity
- [x] `docs/World/NPCs/npc-002-beaten-worker.md` -- personality, identity, role ("not because he's kind" → he is kind), sample lines, state-change quotes in the new voice -- design doc
- [x] `deferred-work.md` -- add: Tam friendship after feeding (how he speaks once fed; food vs meal difference) -- user-deferred

**Acceptance Criteria:**
- Given the edited assets, when grepping `Data/NPCs/BeatenWorker` for em-dashes, then no match.
- Given each node, when measuring `text`, then ≤300 chars.
- Given `git diff` on `Dialogues/`, when inspected, then only `text:` lines changed.
- Given `quest_report audit HiddenStash`, then no new errors versus before.

## Verification

**Commands:**
- `grep -rlE 'u2014|—' Assets/_Game/Data/NPCs/BeatenWorker --include=*.asset` -- expected: no output
- `git diff -U0 Assets/_Game/Data/NPCs/BeatenWorker/Dialogues | grep -E '^[+-] ' | grep -vE '^[+-]\s+(- )?text:'` -- expected: no output

**Manual checks:**
- Play: offer help / mock / leave on separate saves; read each line in the dialogue panel.

## Suggested Review Order

**Voice rules (read first)**

- Canonical identity: new traits, tone and the "withdraws when mocked" rule.
  [`BeatenWorker/CLAUDE.md:58`](../../Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md#L58)

**Mocking path**

- Mock reply: sad, worn-out acceptance instead of a jab.
  [`Text_BeatenWorker_Greetings_Mock.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Greeting/Text_BeatenWorker_Greetings_Mock.asset#L15)

- Repeatable Cold topic: tired withdrawal, lockout unchanged.
  [`Text_BeatenWorker_Cold.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Cold/Text_BeatenWorker_Cold.asset#L15)

**Friendly lines**

- Opener sets the tired, gentle voice.
  [`Choice_BeatenWorker_Greetings.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Greeting/Choice_BeatenWorker_Greetings.asset#L15)

- Theft hint kept, softened; review patch: "that food" (chest holds more than apples).
  [`Text_BeatenWorker_StashHint_3.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/StashHint/Text_BeatenWorker_StashHint_3.asset#L15)

- Meal ending: strongest gratitude.
  [`Text_BeatenWorker_Feed_Meal_2.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Feed/Text_BeatenWorker_Feed_Meal_2.asset#L15)

**Docs and backlog**

- Design doc identity rewritten.
  [`npc-002-beaten-worker.md:21`](../../docs/World/NPCs/npc-002-beaten-worker.md#L21)

- Friendship-after-feeding deferred to its own spec.
  [`deferred-work.md:773`](deferred-work.md#L773)
