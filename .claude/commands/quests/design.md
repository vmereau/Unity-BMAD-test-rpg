Your task is to design a quest interactively with the user and save the result as a filled spec in `docs/Quests/`.

## Step 0 — Load context

Read these files before asking anything:
- `docs/Quests/QUEST_SPEC_TEMPLATE.md`
- `Assets/_Game/Data/Quests/CLAUDE.md`
- `docs/Systems/Quest.md`

Also scan `Assets/_Game/Data/Facts/` to know which Fact assets already exist — you'll reference them during the interview.

---

## Step 1 — Quest identity

Ask:

> "Let's design your quest. Start with the basics:
>
> 1. **Quest ID** — unique PascalCase key (e.g. `FindHerbalist`, `BanditCampClear`)
> 2. **Title** — display name in the Quest Log
> 3. **Description** — 2–4 sentences the player reads in the log"

Check whether `docs/Quests/{QuestId}.md` already exists. If it does, warn the user and ask whether to redesign it or cancel.

---

## Step 2 — Start condition

Ask:

> "What triggers the quest to start?
>
> This becomes `startPart.fact`. It can be:
> - A **WorldFact** set externally (e.g. player entered a zone, a scripted event fired)
> - A **DialogueFact** set when a specific NPC dialogue node is played
> - A **KilledFact** set when an enemy is killed
>
> Name the fact (e.g. `WorldFact_TownGateOpened`) and describe what sets it. I'll note whether it needs to be created."

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

For each fact mentioned, note whether it already exists or needs to be created.

---

## Step 4 — Completion and failure conditions

Ask:

> "What completes the quest? (Any one fact true = completed.)
> What fails it? (Any one fact true = failed — leave blank if it can't fail.)
>
> For each, give the fact name and the text shown in the Quest Log."

---

## Step 5 — NPC involvement

Ask:

> "Is any NPC involved in this quest?
>
> For each NPC:
> 1. **Name** — does this NPC already exist in `Assets/_Game/Data/NPCs/`?
> 2. **Memory window** — under what world-state conditions should this dialogue be available? (unlock facts / invalidation facts)
> 3. **Dialogue** — write the exchange as a script (NPC: / Player: lines). Note any player choice that should set a DialogueFact.
>
> Say 'none' if no NPC is involved."

If the user mentions an NPC that doesn't exist, note that `/NPC:create` will be needed during implementation.

---

## Step 6 — Rewards

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

## Step 7 — Review and confirm

Present the full filled spec in the template format for the user to review. Ask:

> "Does this look right? Any changes before I save?"

Iterate until the user confirms.

---

## Step 8 — Save spec

Save the confirmed spec to `docs/Quests/{QuestId}.md` using `QUEST_SPEC_TEMPLATE.md` as the structure. Set `status: ready`.

Show:

```
Quest spec saved — {QuestId}

  docs/Quests/{QuestId}.md

Facts to create:   <list>
Facts reused:      <list>
NPCs to create:    <list or 'none'>
NPCs already exist: <list or 'none'>

Next step: run /quests:implement {QuestId}
```
