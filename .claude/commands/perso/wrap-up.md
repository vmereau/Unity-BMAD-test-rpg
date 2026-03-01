Analyze the current conversation and update `CLAUDE.md` with any new knowledge worth preserving across sessions.

## Steps

### 1 — Scan conversation for new knowledge

Review the full conversation history and extract:

- **Bugs fixed** that reveal a repeatable pattern (e.g. a Unity lifecycle trap, an API quirk)
- **Architectural decisions** confirmed or newly made (library choices, component topology)
- **Gotchas discovered** — Unity-specific surprises, third-party library behaviors, undocumented edge cases
- **Project facts** learned (real file paths, inspector configurations, what actually exists vs. what docs claim)
- **Workflow corrections** — cases where a command behaved differently than expected, or a BMAD workflow step was adjusted

Ignore session-specific context (current task details, in-progress work) — only record patterns that will recur.

### 2 — Read current CLAUDE.md

```bash
cat CLAUDE.md
```

Note what's already captured. Do not duplicate existing entries.

### 3 — Read project-context.md

```bash
cat _bmad-output/project-context.md
```

Determine whether each new finding belongs in:
- **CLAUDE.md** — Claude Code meta-knowledge: workflow orientation, Unity gotchas, project structure facts, code review patterns
- **`_bmad-output/project-context.md`** — game coding rules all BMAD agents must follow (architecture, naming, performance, anti-patterns)
- **Both** — if it's both a coding rule and a Claude-specific reminder

### 4 — Propose updates

For each item, state:
- The finding in one sentence
- Which file it belongs in and why
- The exact text to add (bullet, code block, or table row)

Present all proposals before writing anything. Ask for confirmation if anything is ambiguous.

### 5 — Apply confirmed updates

Edit the target files. Follow these style rules:
- Prefer bullet points and code snippets over prose
- Keep each entry self-contained (a future Claude with no session context should understand it)
- Place entries in the most specific existing section; create a new section only if no section fits
- Do not pad or editorialize — one crisp sentence per finding is enough

### 6 — Summarize

Report what was added/changed and in which file. If nothing new was found, say so clearly.
