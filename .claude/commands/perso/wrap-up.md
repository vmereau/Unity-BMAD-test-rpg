Analyze the current conversation and record any new knowledge worth preserving across sessions in the **right**
documentation file — usually a folder CLAUDE.md, not the root one.

## Where knowledge lives

| Target | What goes there | Loaded |
|---|---|---|
| Root `CLAUDE.md` | Index of folder files, cross-cutting rules (lifecycle gotchas, MCP quirks, workflow) | every session |
| Folder `CLAUDE.md` (`Assets/_Game/**`) | Rules/gotchas for the files in that folder — see the root "Folder CLAUDE.md Index" | when a file in that folder is read |
| Topic file (e.g. `Scripts/Combat/PLAYER_COMBO.md`, `Data/NPCs/DIALOGUE.md`) | Long recipes / flows already split out of a folder CLAUDE.md | when the folder CLAUDE.md says to read it |
| `.claude/rules/*.md` with `paths:` | Contracts spanning several unrelated folders (e.g. `attack-pipeline.md`) | when matching files are touched |
| `_bmad-output/project-context.md` | Game coding rules all BMAD agents must follow (architecture, naming, performance, anti-patterns) | by BMAD workflows |

Each rule has **exactly one owner**. Never copy a rule into a second file — add a one-line pointer instead.

## Steps

### 1 — Scan conversation for new knowledge

Review the full conversation history and extract:

- **Bugs fixed** that reveal a repeatable pattern (e.g. a Unity lifecycle trap, an API quirk)
- **Architectural decisions** confirmed or newly made (library choices, component topology)
- **Gotchas discovered** — Unity-specific surprises, third-party library behaviors, undocumented edge cases
- **Project facts** learned (real file paths, inspector configurations, what actually exists vs. what docs claim)
- **Workflow corrections** — cases where a command behaved differently than expected, or a BMAD workflow step was adjusted
- **Stale docs** — anything the session proved wrong in an existing CLAUDE.md (fix it, don't add next to it)
- Any `[CLAUDE.md candidate]` notes raised during the session

Ignore session-specific context (current task details, in-progress work) — only record patterns that will recur.

### 2 — Pick the owner file for each finding

For each finding, choose the **most specific** target from the table above:
- Identify the files/assets it concerns, and the deepest folder containing them that has (or should have) a
  CLAUDE.md — use the root "Folder CLAUDE.md Index". Spans several unrelated folders → existing
  `.claude/rules/*.md` file, or the root if it's truly project-wide.
- Is it a coding rule every BMAD agent needs? → `project-context.md` (CLAUDE.md does not repeat it).

### 3 — Read the target files

Read the root `CLAUDE.md`, `_bmad-output/project-context.md`, and **every target file chosen in step 2** (plus
topic files they point to for that subject). Note what's already captured — do not duplicate; update an
existing entry if it's incomplete or wrong.

### 4 — Propose updates

For each item, state:
- The finding in one sentence
- The target file and why it is the owner (folder / scope)
- The exact text to add or change (bullet, code block, or table row)

If a target folder has no CLAUDE.md yet, propose creating it (and its root index row). If a target file would
grow past ~150 lines, say so and suggest `/perso:claude-md-audit` instead of piling on.

Present all proposals before writing anything. Ask for confirmation if anything is ambiguous.

### 5 — Apply confirmed updates

Edit the target files. Follow these style rules:
- Prefer bullet points over prose; reference code by file/method instead of pasting method bodies
- Keep each entry self-contained (a future Claude with no session context should understand it)
- Place entries in the most specific existing section; create a new section only if no section fits
  (code-review patterns go in that file's `## Code Review Checklist` table)
- Do not pad or editorialize — one crisp sentence per finding is enough; no story numbers or "added in …" history
- New `.md` under `Assets/` → also create its `.meta` (`TextScriptImporter`, random 32-hex GUID — format in
  `/perso:claude-md-audit`) and add a row to the root "Folder CLAUDE.md Index"

### 6 — Summarize

Report what was added/changed and in which file. If nothing new was found, say so clearly.
