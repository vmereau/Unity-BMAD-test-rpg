---
title: 'Tithe barn: Pell asks for proof, leads to Brann''s tally'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '3af2f6fc'
context:
  - '{project-root}/docs/World/Quests/TitheBarnShortfall.md'
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/MEMORIES.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The quest opens on a bare "About the barn" topic with no leads to Venn. Venn has nothing to say when the player asks him. Naming Venn to Pell succeeds on the player's word alone, so the truth costs nothing and the lie (Pim) costs the same.

**Approach:** Data-only rework. Pell's start topic sends the player to the workers and to Venn. Venn gets a brush-off topic, and his refuse line becomes a threat. Naming Venn without proof gets Pell's "bring me something with his name on it". Two paid/free leads point to a new tally item in Brann's stash, and handing that item to Pell is the only way to report Venn.

## Boundaries & Constraints

**Always:** Reuse existing systems (`ChoiceOption.requiredItem` / `giveToNpc` / `hideWhenMissing` / `requiredMemory` / `goldCost`, `ContainerTakeFact`, owned-container theft). Follow `/NPC:dialogue` conventions (node naming, fact on start node or choice). Keep the quest doc in sync. `quest_report TitheBarnShortfall` stays free of Errors and Warnings.

**Ask First:** Any C# change. Moving or re-owning `BrannsStash`.

**Never:** No hostility or immediate consequence on the refuse threat (it's dialogue only). No proof requirement for blaming Pim. No change to the endings' rewards, removals or fail rule.

## I/O & Edge-Case Matrix

| Scenario | State | Expected |
|---|---|---|
| Report without proof | Caught Venn, no tally | "I caught him at the door" → Pell's disbelief line, sets `NeedProof`, quest stays open |
| Report with tally | Holds `Item_BrannsTally` (caught or not) | Proof choice shown, tally handed over, `ReportedGuard` set |
| Tally without the stakeout | Tally held, `ThiefCaught` false | Report topic is open (unlocked on Started) and the proof choice works |
| Nothing yet | Started only | Report topic shows only "Not yet." |
| Tally dropped / sold | `TallyTaken` set, item gone | Proof choice hidden; step stays done (accepted) |

</frozen-after-approval>

## Code Map

- `Data/NPCs/FarmReeve/Dialogues/Shortfall/` + `Memories/Mem_FarmReeve_Shortfall` -- quest start topic
- `Data/NPCs/FarmReeve/Dialogues/ShortfallReport/` + `Mem_FarmReeve_ShortfallReport` -- report choices
- `Data/NPCs/BarnGuard/Dialogues/Caught/Text_BarnGuard_Refused` -- refuse line
- `Data/NPCs/Cook/Dialogues/BarnTip/Choice_Cook_BarnTip` -- `goldCost` 20 → 5
- `Data/NPCs/BeatenWorker/` -- Tam, already has `StashHint`; new topic goes beside it
- `Data/Items/Quest/Item_Notice_Farm.asset` -- quest `ItemSO` to copy
- `Scenes/StartingTown.unity` `BrannsStash` (under `HiddenStashQuest`) -- owned container; existing `ContainerTakeFact` uses are the wiring reference
- `Data/Quests/TitheBarnShortfall/Quest_TitheBarnShortfall.asset` -- steps

## Tasks & Acceptance

**Execution:**
- [x] `FarmReeve/Dialogues/Shortfall/` -- Topic "Anything I can do for you around the fields?" Pell explains the dawn shortfall and the unforced lock, says the player is useful because nobody knows him yet, and sends him to ask the field workers and Venn at the fields gate. Keep the Accept / decline choice and `DialogueFact_FarmReeve_Shortfall_Accepted`.
- [x] `BarnGuard/` -- New `Mem_BarnGuard_BarnTalk` + `Dialogues/BarnTalk/` (topic "About Pell's missing grain."). Unlock: Started. Invalidate: own fact, `ThiefCaught`, Completed, Failed. Start node sets the new `DialogueFact_BarnGuard_BarnTalk`. Venn: it's one of the poor workers, go ask them, he's seen nothing suspicious, stop bothering him.
- [x] `BarnGuard/Dialogues/Caught/Text_BarnGuard_Refused` -- Threat: nobody will believe a newcomer over a Company guard, and the men he sells to remember faces.
- [x] `FarmReeve` report -- `Mem_FarmReeve_ShortfallReport` unlocks on Started only (same invalidations). New gate memory `Mem_FarmReeve_GateCaught` (unlock `ThiefCaught`). Choices, in order: (1) proof "It's Venn. Here's Brann's tally, with his mark on it." (`requiredItem` tally, `giveToNpc`, `hideWhenMissing`) → existing guard text, sets `ReportedGuard`; (2) "It's Venn. I caught him at the barn door." (`requiredMemory` GateCaught) → new `Text_FarmReeve_Report_NoProof`, sets new `DialogueFact_FarmReeve_Shortfall_NeedProof`; (3) Pim, unchanged; (4) "Not yet." Update the guard text so Pell reads the tally ("O.V., three sacks…"). Add Pell's no-proof line: "A Company guard, on a newcomer's word? If he takes it, he sells it. Find who buys my grain, and bring me something with his name on it."
- [x] `Cook/` -- `Choice_Cook_BarnTip` `goldCost` 5 (choice text "Five coin. Tell me."). New `Mem_Cook_Tally` + `Dialogues/Tally/` (unlock `NeedProof`; invalidate own fact, `TallyTaken`, Completed, Failed). 5 gold choice sets `DialogueFact_Cook_Tally`: Brann pays nobody without chalking it down and keeps the slips in his stash box.
- [x] `BeatenWorker/` -- New `Mem_BeatenWorker_BarnTally` + `Dialogues/BarnTally/` (unlock `NeedProof`; invalidate own fact, `TallyTaken`, Completed, Failed). Free. Start node sets `DialogueFact_BeatenWorker_BarnTally`. Brann counts everything twice and keeps tallies in the box by the gang house. Hands off, if you like your fingers.
- [x] `Data/Items/Quest/Item_BrannsTally` -- Quest `ItemSO` (name "Brann's Grain Tally", description of chalked "O.V. — 3 sacks — 12" lines). Icon: reuse the notice icon, and give the user an `items:icon-prompt` follow-up instead of generating one.
- [x] `StartingTown.unity` `BrannsStash` -- Add 1× tally to its inventory and a `ContainerTakeFact` (tally only) → new `WorldFact_GangStash_TallyTaken`. Save the scene.
- [x] `Quest_TitheBarnShortfall` -- Add `DialogueFact_BarnGuard_BarnTalk` as a part of step "Ask around the farm". Add step "Something with his name on it" after "Watch the barn" (parts: `NeedProof`, `TallyTaken`). Check `onStepCompleted` indices still point at "Watch the barn".
- [x] `docs/World/Quests/TitheBarnShortfall.md`, NPC `CLAUDE.md` asset lists, `Data/Facts` -- Sync beats, facts, memories, steps and dialogue lines. Create the new fact assets.

**Acceptance Criteria:**
- Given the arrival is done, when the player opens Pell's topic list, then "Anything I can do for you around the fields?" starts the quest and names the workers and Venn as people to ask.
- Given the quest has started, when the player asks Venn about the grain, then he blames the workers, brushes the player off, and the "Ask around the farm" step records it.
- Given Venn is caught and the player refuses his deal, then his reply threatens retribution and nothing else changes.
- Given `NeedProof` is set, then Aldric (5 gold) and Tam (free) each offer a tally lead, and taking the tally from `BrannsStash` sets `TallyTaken` (a theft if seen).
- `quest_report TitheBarnShortfall` reports no Error or Warning.

## Design Notes

A memory's unlock list is AND-only. That's why the report topic unlocks on Started and each choice gates itself: the item gates the proof choice and `GateCaught` gates the no-proof one, so the tally works with or without the stakeout. Blaming Pim stays proof-free on purpose: Pell believes the convenient lie and demands proof for the inconvenient truth.

## Verification

**Commands:**
- MCP `quest_report TitheBarnShortfall` -- expected: no Error / Warning
- MCP `read_console` (Error) -- expected: none after the scene save

**Manual checks:**
- Play: start → Venn brush-off → caught → refuse threat → no-proof report → Tam lead → steal tally → proof report completes

## Suggested Review Order

**Proof gate at the report**

- Entry point: proof choice needs the tally (consumed), no-proof line sets NeedProof, Pim unchanged.
  [`Choice_FarmReeve_ShortfallReport.asset:18`](../../Assets/_Game/Data/NPCs/FarmReeve/Dialogues/ShortfallReport/Choice_FarmReeve_ShortfallReport.asset#L18)

- No-proof line plays once: closes on NeedProof or once the tally is taken.
  [`Mem_FarmReeve_GateCaught.asset:15`](../../Assets/_Game/Data/NPCs/FarmReeve/Memories/Mem_FarmReeve_GateCaught.asset#L15)

**Leads to Brann's stash**

- Aldric's paid lead (5 gold), unlocked by NeedProof.
  [`Mem_Cook_Tally.asset:15`](../../Assets/_Game/Data/NPCs/Cook/Memories/Mem_Cook_Tally.asset#L15)

- Tam's free lead, behind his greeting-opener gate.
  [`Mem_BeatenWorker_BarnTally.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Memories/Mem_BeatenWorker_BarnTally.asset#L15)

- Second take-fact on the stash, tally-only allowlist.
  [`StartingTown.unity:478628`](../../Assets/_Game/Scenes/StartingTown.unity#L478628)

- The proof item itself (quest item, synced into ItemCatalog).
  [`Item_BrannsTally.asset:16`](../../Assets/_Game/Data/Items/Quest/Item_BrannsTally.asset#L16)

**Quest start and Venn**

- Pell's new opener sends the player to the workers and Venn.
  [`Text_FarmReeve_Shortfall.asset:15`](../../Assets/_Game/Data/NPCs/FarmReeve/Dialogues/Shortfall/Text_FarmReeve_Shortfall.asset#L15)

- Venn's brush-off: blames the workers, stop bothering me.
  [`Text_BarnGuard_BarnTalk.asset:15`](../../Assets/_Game/Data/NPCs/BarnGuard/Dialogues/BarnTalk/Text_BarnGuard_BarnTalk.asset#L15)

- Refuse threat, dialogue only.
  [`Text_BarnGuard_Refused.asset:15`](../../Assets/_Game/Data/NPCs/BarnGuard/Dialogues/Caught/Text_BarnGuard_Refused.asset#L15)

**Journal and docs**

- Two one-part steps so the tally-first route still ticks a step.
  [`Quest_TitheBarnShortfall.asset:49`](../../Assets/_Game/Data/Quests/TitheBarnShortfall/Quest_TitheBarnShortfall.asset#L49)

- Quest doc beats 6 to 8 and the proof design note.
  [`TitheBarnShortfall.md:34`](../../docs/World/Quests/TitheBarnShortfall.md#L34)
