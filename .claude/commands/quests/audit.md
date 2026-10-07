Your task is to audit quests and NPC memories with the Quest Explorer validator, explain every issue in plain words,
and fix the ones the user approves.

## Tools

- Unity MCP **`quest_report`** tool (fallback: `execute_code` →
  `return Game.Editor.QuestExplorer.QuestReport.Run("audit");`). Targets: `audit`, `list`, a quest (questId / asset
  name / title), a fact asset name, a memory asset name.
- Fixes go through `Game.Editor.QuestExplorer.QuestEditActions` (Undo-aware, same as the window) via `execute_code` —
  see the tool list in `.claude/commands/quests/implement.md`. In `execute_code`, write `UnityEngine.Object`.

## Step 0 — Load context

Read `Assets/_Game/Editor/QuestExplorer/CLAUDE.md` (rules V1–V17 and gotchas) and `Assets/_Game/Data/Facts/CLAUDE.md`.

## Step 1 — Run the report

- `$ARGUMENTS` empty or `all` → `quest_report audit` (every quest + every NPC memory, including memories tied to no
  quest).
- Otherwise → `quest_report <target>` for that quest / fact / memory.

If Unity isn't reachable, say so and stop — the audit needs the live AssetDatabase and loaded scenes. Remind the user
that KilledFact setters in **closed** scenes are found by a text scan, and loaded scenes give the most accurate data.

## Step 2 — Explain

Group the issues by quest (then the memory section). For each Error and Warning:
- what it means for the player (e.g. "Step 1 can never complete, so the quest is stuck"),
- the likely cause, using `quest_report <fact|memory>` to look one level deeper when needed,
- the proposed fix and which asset it changes.

List Info issues briefly at the end. Don't propose fixes for things that need design decisions (missing dialogue,
which fact should complete a step) — ask instead.

## Step 3 — Fix (with approval)

Ask which fixes to apply. Apply each with `QuestEditActions` (one Undo group per edit) or the matching NPC / quest
command (`/NPC:dialogue`, `/quests:implement`) when new content is needed. Never delete assets without asking.

## Step 4 — Re-check

Run the same report again and show the before / after counts (errors / warnings / info). Mention anything left and
why.
