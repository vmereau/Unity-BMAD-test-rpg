---
title: 'NPC voice pass: rewrite existing dialogue and personas to the DIALOGUE.md Voice rules'
type: 'chore'
created: '2026-11-19'
status: 'done'
baseline_commit: '0e871d86'
context:
  - '{project-root}/Assets/_Game/Data/NPCs/DIALOGUE.md'
  - '{project-root}/Assets/_Game/Data/NPCs/QuarryWorker/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Voice rules added with the Sealed Passage (quarry) work, natural speech up to 300 chars with voice coming from the identity sheet, are only applied to Bren. Every other NPC still talks in the clipped "gritty" register (fragments, ellipses, one-line punches), several identity sheets prescribe that register ("short sentences", "trails off"), and Edric the blacksmith is placeholder text (typos, the frog gag, two separate teach topics).

**Approach:** Rewrite each NPC's Speech Style sheet so it gives a distinct voice compatible with the Voice rules (loosening the clipped ones: Warden, Dace, Brann, Harl, guards), then rewrite every existing dialogue text string from that sheet. Text only, except Edric: remove the frog gag and merge his two teach topics into one.

## Boundaries & Constraints

**Always:** DIALOGUE.md Writing Rules + Voice (≤300 chars per node, no em-dashes, at most one ellipsis per topic, no stage directions). Keep each NPC's established facts, names, quest information and gating meaning (what a line reveals stays revealed in the same node). Choice labels stay in the player's voice and keep their meaning (a hostile choice stays hostile). Fix typos and "word ?" spacing. Keep Edric's profanity (rewrite around it, do not soften it). Bren (QuarryWorker) is the reference voice: polish only if a line breaks a rule.

**Ask First:** Any line whose rewrite would change what the player learns or what a choice implies; any change to a fact, memory, reward or node link outside Edric.

**Never:** No new topics, nodes, facts or memories (except Edric's merge). No choice reordering (seen state is keyed by index). No changes to barks (`Barks_*` shouted one-liners), quest logic, rewards or scenes. Don't touch `_bmad-output` planning docs.

</frozen-after-approval>

## Code Map

- `Assets/_Game/Data/NPCs/DIALOGUE.md` -- Voice rules (source of truth); Bren's `QuarryWorker/CLAUDE.md` -- reference sheet
- `Assets/_Game/Data/NPCs/<NPC>/CLAUDE.md` -- Speech Style per NPC; FarmReeve, LeadHunter, QuarryOverseer, TimberBoss, StashOwner use a one-paragraph form; DockGuard / WarehouseGuard have TBD quirks; Cook sample has an em-dash; BeatenWorker sheet prescribes "short sentences" + "...".
- `Assets/_Game/Data/NPCs/<NPC>/Dialogues/**`, `Cook/Teachings/Lockpicking/`, `BlackSmith/Teachings/**`, `Shared/Dialogues/TheftReturn/` -- `text:` fields of Start / Text / Choice / Shop / TeachChoice nodes
- `BlackSmith/Dialogues/Frogs/`, `FrogsInsisted/` (+ `PlayerReward_blacksmith_frog_insisted`), `Make/Text_make_frogs`, `Make/DialogueFact_FrogKillingWeapon`, `Facts/DialogueFact_blacksmith_asked_frogs`, `..._frogs_insisted`, `Memories/frogs/` -- frog gag assets
- `Assets/_Game/Prefabs/Player/Player.prefab:725` -- `PlayerRewards` list references the frog reward
- `BlackSmith/NPC_Blacksmith.asset` -- `memories` list (synced by `NPCMemoriesAutoSync`)
- `BlackSmith/Teachings/Training/TeachChoice_Blacksmith_Training.asset` -- Strength row has a dangling `requiredMemory` (guid `31a2e43…`, no asset)
- `Scripts/World/DialogueSystem.cs:611` -- teach of a known skill only warns; the Smithing row must be gated by a memory

## Tasks & Acceptance

**Execution:**
- [x] `Data/NPCs/*/CLAUDE.md` -- rewrite every Speech Style into the table form (Tone / Vocabulary / Quirks / Taboo + sample line meeting the Voice rules); loosen clipped NPCs into speakers with a reason to be short in some lines, not all; fill DockGuard / WarehouseGuard TBDs; drop Edric's frog trait, "Aye on every line" and "keep lines short" rules -- voice comes from the sheet
- [x] Dialogue text, per NPC folder (BeatenWorker, BlackSmith, Cook, FarmReeve, LeadHunter, QuarryOverseer, QuarryWorker (polish), RacketGuard, Shared/TheftReturn, StashOwner, Storekeeper, TentMate, TimberBoss, Warden) -- rewrite every `text:` from the new sheet; quote YAML strings that contain `:` or start with a quote
- [x] `BlackSmith/` frog removal -- delete the Frogs / FrogsInsisted folders, `Text_make_frogs`, the frog choice row of `Choice_Make` (last row, no index shift), `DialogueFact_FrogKillingWeapon`, both frog facts, `Memories/frogs/`; remove the reward from `Player.prefab` `PlayerRewards`; check `NPC_Blacksmith.memories` has no missing entries
- [x] `BlackSmith/Teachings/` merge -- one topic "What can you teach me?" (`Start_Blacksmith_Training` / `Mem_Blacksmith_Training`): `TeachChoice_Blacksmith_Training` gets a second row Smithing (80 g, skill + confirm node from `TeachChoice_Blacksmith_Smithing`, `requiredMemory` = `Mem_Blacksmith_Smithing`); clear the Strength row's dangling `requiredMemory`; move `Text_Blacksmith_Smithing_Confirm` into `Training/` with `nextNode` → the merged TeachChoice; delete `Start_` / `Text_` / `TeachChoice_Blacksmith_Smithing`; make `Mem_Blacksmith_Smithing` gate-only (`startdialog` null, still invalidated by `SkillFact_Smithing_Learned`)
- [x] `BlackSmith/Dialogues/Make/` -- rename topic label + rewrite the three craft answers (typos fixed)
- [x] Docs -- `BlackSmith/CLAUDE.md` Asset Reference (Teachings row), any `docs/` or folder CLAUDE.md quoting a rewritten line or the removed topics; grep `Assets/Tests/` for old strings
- [x] `refresh_unity(if_dirty)`, `AssetDatabase.SaveAssets()`, check no missing references in Blacksmith assets / `Player.prefab`

**Acceptance Criteria:**
- Given any NPC dialogue asset, when grepped, then no `text:` exceeds 300 chars, none contains `—`/`—`, and no topic has more than one "...".
- Given each NPC with dialogue, when reading its sheet and lines together, then the lines follow the sheet's quirks and the sheet's sample line meets the Voice rules.
- Given Edric before Smithing is learned, when asking "What can you teach me?", then Strength and Smithing are offered; after learning Smithing, only Strength is offered, and the recipe / shop topics unlock as before.
- Given the project, when searching for "frog", then nothing remains under `Assets/_Game` (code, data, prefabs, docs).
- Given `quest_report audit`, when run, then no new issues appear.

## Design Notes

Loosening a clipped NPC means giving the brevity a reason inside the line, not cutting the line. Warden (before): "Work. Good. A newcomer who asks saves me sending a guard." → (after): "Good. A newcomer who comes asking saves me the trouble of sending a guard to fetch him, and I notice the ones who save me trouble." He stays cold and never says thanks; the sentences are just whole.

## Verification

**Commands:**
- `grep -rhoE "text: .*" Assets/_Game/Data/NPCs --include=*.asset | awk 'length > 310'` -- expected: nothing
- `grep -rlE 'u2014|—' Assets/_Game/Data/NPCs --include=*.asset` -- expected: nothing
- Unity EditMode tests (`run_tests`) -- expected: all pass; MCP `quest_report audit` -- expected: no new issues

**Manual checks:**
- `read_console`: no errors after refresh; every edited / created node keeps a non-empty `_id`

## Suggested Review Order

**Voice sheets (the source each NPC's lines are written from)**

- Start here: a clipped NPC loosened into whole, cold sentences without losing his taboo.
  [`Warden/CLAUDE.md:33`](../../Assets/_Game/Data/NPCs/Warden/CLAUDE.md#L33)

- Tam's sheet no longer prescribes short sentences; one "..." reserved for hurt moments.
  [`BeatenWorker/CLAUDE.md:33`](../../Assets/_Game/Data/NPCs/BeatenWorker/CLAUDE.md#L33)

- Short-form manager sheet converted to the table; Dace stays terse by trait.
  [`QuarryOverseer/CLAUDE.md:14`](../../Assets/_Game/Data/NPCs/QuarryOverseer/CLAUDE.md#L14)

**Rewritten lines (sample per register)**

- Warden: same information, whole sentences, still never thanks anyone.
  [`Text_Warden_ErrandAssign.asset:15`](../../Assets/_Game/Data/NPCs/Warden/Dialogues/Errand/Text_Warden_ErrandAssign.asset#L15)

- Tam's opener: the rambling, self-effacing register the Voice rules ask for.
  [`Choice_BeatenWorker_Greetings.asset:15`](../../Assets/_Game/Data/NPCs/BeatenWorker/Dialogues/Greeting/Choice_BeatenWorker_Greetings.asset#L15)

**Edric: frog removal and teach merge**

- Smithing row added to the single teach node, gated by the gate-only memory.
  [`TeachChoice_Blacksmith_Training.asset:35`](../../Assets/_Game/Data/NPCs/BlackSmith/Teachings/Training/TeachChoice_Blacksmith_Training.asset#L35)

- Smithing memory keeps its invalidation but no longer exposes a topic.
  [`Mem_Blacksmith_Smithing.asset:19`](../../Assets/_Game/Data/NPCs/BlackSmith/Memories/Mem_Blacksmith_Smithing.asset#L19)

- Sheet: frog trait gone, "aye" now occasional, teach topology documented.
  [`BlackSmith/CLAUDE.md:70`](../../Assets/_Game/Data/NPCs/BlackSmith/CLAUDE.md#L70)

- Frog reward removed from the player's reward list.
  [`Player.prefab:724`](../../Assets/_Game/Prefabs/Player/Player.prefab#L724)

**Peripherals**

- Docs quoting rewritten lines kept in sync (Tam, Bren, guards, innkeeper).
  [`npc-002-beaten-worker.md:53`](../../docs/World/NPCs/npc-002-beaten-worker.md#L53)
