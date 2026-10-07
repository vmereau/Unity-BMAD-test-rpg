Your task is to design a quest interactively with the user and save the result as a filled spec in
`docs/World/Quests/`.

## Step 0 — Load context

Read these files before asking anything:
- `docs/World/Quests/QUEST_SPEC_TEMPLATE.md`
- `docs/World/Quests/Quest.md`
- `Assets/_Game/Data/Quests/CLAUDE.md`
- `Assets/_Game/Data/Facts/CLAUDE.md`

Then get the current state from the Quest Explorer data with the Unity MCP **`quest_report`** tool (fallback:
`execute_code` → `return Game.Editor.QuestExplorer.QuestReport.Run("list");`):
- `quest_report list` — existing quests, their ids and issue counts
- `quest_report <QuestId>` for any quest the new one may connect to
- `quest_report <FactName>` / `<MemoryName>` when you reference an existing fact or memory — it tells you who sets
  and reads it

If Unity isn't running, fall back to scanning `Assets/_Game/Data/Facts/` and `Assets/_Game/Data/Quests/`.

---

## Step 1 — Quest identity

Ask:

> "Let's design your quest. Start with the basics:
>
> 1. **Quest ID** — unique PascalCase key (e.g. `FindHerbalist`, `BanditCampClear`)
> 2. **Title** — display name in the Quest Log
> 3. **Description** — 2–4 sentences the player reads in the log"

Check `quest_report list` for a duplicate questId and whether `docs/World/Quests/{QuestId}.md` already exists. If it
does, warn the user and ask whether to redesign it or cancel.

---

## Step 2 — Start condition

Ask:

> "What triggers the quest to start?
>
> This becomes `startPart.fact`. It can be:
> - A **DialogueFact** set when a specific NPC dialogue node is played or a choice is picked (most common)
> - A **KilledFact** set when a specific enemy is killed
> - A **QuestFact** — another quest reached a state (started / completed / failed / a step done)
> - A **WorldFact** — note: nothing in code sets WorldFacts yet, so this needs a new setter first
>
> Name the fact (e.g. `DialogueFact_Guard_SpiderQuestAccepted`) and describe what sets it. I'll note whether it needs
> to be created."

---

## Step 3 — Steps

Ask:

> "Does this quest have sub-goals shown in the Quest Log? (Steps are optional.)
>
> For each step, I need:
> 1. A short **title** (shown in the log)
> 2. An **objective description**
> 3. The **fact(s)** that mark it complete — all must be true for the step to be done; any one true shows it as active
>
> List your steps, or say 'none'."

For each fact mentioned, note whether it already exists or needs to be created. Kill objectives need **one KilledFact
per enemy** (each enemy is a part). If the description mentions a count ("kill five"), the step should have that
many parts (V13).

---

## Step 4 — Completion and failure conditions

Ask:

> "What completes the quest? (Any one fact true = completed.)
> What fails it? (Any one fact true = failed — leave blank if it can't fail.)
>
> For each, give the fact name and the text shown in the Quest Log."

---

## Step 5 — Quest dependencies

Ask:

> "Does this quest depend on another one (e.g. only starts after `FindHerbalist` is completed), or should other
> quests / NPCs react to its progress (e.g. an NPC thanks you once it's done)?"

Record them in the spec's **Quest Dependencies** section; each becomes a `QuestFact`.

---

## Step 6 — NPC involvement

Ask:

> "Is any NPC involved in this quest?
>
> For each NPC:
> 1. **Name** — does this NPC already exist in `Assets/_Game/Data/NPCs/`?
> 2. **Memory window** — under what world-state conditions should this dialogue be available? (unlock facts: all
>    must be true / invalidation facts: any one closes it for good)
> 3. **Dialogue** — write the exchange as a script (NPC: / Player: lines). Note any player choice that should set a
>    DialogueFact, and any choice that should only appear in some situations (memory-gated)."

For an existing NPC, run `quest_report` on its memories you plan to touch, and read its `CLAUDE.md` identity.
If the user mentions an NPC that doesn't exist, note that `/NPC:create` will be needed during implementation.

Design checks to raise with the user:
- A memory usually needs an invalidation, or its dialogue keeps playing forever (common: the fact its own dialogue
  sets, or the quest's completed QuestFact).
- A gated choice needs a **gate memory** (no start dialogue, only conditions) owned by the **same NPC** (V17).
- Memories with no start dialogue and no gated choice do nothing (V15).

---

## Step 7 — Rewards

Ask:

> "What does the player earn?
>
> - On **quest start** (rare — usually nothing)
> - On each **step completed** (e.g. XP for reaching a milestone)
> - On **quest completed**
> - On **quest failed** (rare)
>
> For each, specify XP / LP / Gold / stat upgrades (type + points). Say 'none' to skip."

---

## Step 8 — Review and confirm

Present the full filled spec in the template format for the user to review. Walk through the flow once, in order,
as the player would experience it (which NPC line → which fact → which step / memory changes) and point out any
dead end: a fact nobody sets, a memory that never closes, a step that can't complete. Ask:

> "Does this look right? Any changes before I save?"

Iterate until the user confirms.

---

## Step 9 — Save spec

Save the confirmed spec to `docs/World/Quests/{QuestId}.md` using `QUEST_SPEC_TEMPLATE.md` as the structure. Set
`status: ready`.

Show:

```
Quest spec saved — {QuestId}

  docs/World/Quests/{QuestId}.md

Facts to create:   <list>
Facts reused:      <list>
NPCs to create:    <list or 'none'>
NPCs already exist: <list or 'none'>

Next step: run /quests:implement {QuestId}
```
