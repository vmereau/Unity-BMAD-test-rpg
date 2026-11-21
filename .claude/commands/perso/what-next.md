---
description: Review recent specs, design documents ready to build, the ideas inbox, the game's design goals and the deferred-work backlog, then advise what to work on next
argument-hint: "[optional focus, e.g. combat, area 1, polish, quick wins]"
---

Advise the user on what to work on next. Read the project's recent history, its design intent, the design documents
waiting to be built and its open backlog, then recommend a short, ranked list of next steps. **This command is read-only — do not edit any file,
create a spec or start implementing.** It ends with a recommendation and a question.

Optional focus from the user: `$ARGUMENTS` (empty → no focus, consider everything).

## Sources

| What | Where | How to read it |
|---|---|---|
| Game identity & aims | `_bmad-output/gdd.md` | Read `Executive Summary`, `Goals and Context`, `Core Gameplay`, `Development Epics`, `Success Metrics`, `Out of Scope` (use `grep -n "^## "` to find them; skip the rest unless the focus needs it) |
| Story / world | `_bmad-output/narrative-design.md`, `_bmad-output/story-2026-05-03.md` | Skim headings + the opening sections — enough to know the setting, the arrival beat and the current area |
| Recent work | `_bmad-output/implementation-artifacts/spec-*.md` | Frontmatter (`title`, `type`, `created`, `status`) + the `## Intent` section |
| Design handoffs | `docs/World/world-index.md`, `docs/World/**/*.md` | Design work done away from Unity (often on mobile) and waiting for a desktop implementation session. Read each doc's status line and the `Status` columns of `world-index.md` |
| Ideas inbox | `docs/IDEAS.md` | Quick ideas captured for later. Read every entry above `## Archived`; note its domain and `Status` (`brut` / `à designer`) |
| Backlog | `_bmad-output/implementation-artifacts/deferred-work.md` | Every `## From <spec> (<date>)` section; a struck-through item (`~~…~~`) is **resolved** — ignore it |
| What shipped | `git log` | Confirms what actually landed, and when |

Do **not** plan from `epics.md` or `sprint-status.yaml` — they are obsolete (see root `CLAUDE.md`). Do not read
`_bmad-output/archive/` unless an open item points there.

## Steps

### 1 — Recent work

- `git log --oneline -40` and `git log -1 --format=%cd` — what shipped lately and how fresh the history is.
- List specs by `created:` date (frontmatter, not file mtime — files get touched by later edits), e.g.
  `grep -H "^created:\|^status:\|^title:" _bmad-output/implementation-artifacts/spec-*.md`.
- Take the **~12 most recent** specs: read their `## Intent` (Problem / Approach). Group them into themes
  (e.g. "new-game flow: start menu → origins → artwork → play scenarios").
- Flag every spec whose `status:` is not `done` (draft, ready, in-progress, in-review…) — unfinished work is
  always a candidate, and a stale one may need closing or dropping rather than finishing.

### 1b — Design handoffs

Quest, NPC, dialogue and area designs are written in `docs/World/` (often from a mobile session without Unity) and
handed to the desktop by marking them `ready`. Nothing else announces them, so find them here:

- Status lines vary in format — a `status:` frontmatter key, a bare `Status: …` line, `> Status: \`ready\``, `- Status: ready`. Find them with
  `grep -rniE "^(> |- )?status:" docs/World --include=*.md` and read the value; ignore `QUEST_SPEC_TEMPLATE.md` and
  table rows (`| … | Status | … |`) inside docs.
- Also read the `Status` column of every table in `docs/World/world-index.md`.
- A doc is a **handoff** when its status is `ready` (or `ready-for-dev` / `designed`). `draft` / `proposed` is still being
  designed — mention it only if it matches the user's focus. `implemented` / `done` is finished.
- For each handoff, read enough of the doc to say what it asks for in Unity, and check whether a spec already covers
  it: `grep -l "<doc path or quest id>" _bmad-output/implementation-artifacts/spec-*.md`. A doc marked `ready` but
  already covered by a `done` spec is probably a stale status — say so instead of recommending it.
- Note the doc's last commit date (`git log -1 --format=%cd -- <doc>`) so the user sees how fresh it is.
- Pick the build command: a quest spec under `docs/World/Quests/` → `/quests:implement <QuestId>`; NPC dialogue →
  `NPC:dialogue` / `NPC:teach-dialogue` / `NPC:shop-dialogue`; a new NPC → `NPC:create`; anything else (areas,
  systems, mixed) → `gds-quick-dev` pointed at the doc path.

### 1c — Ideas inbox

Read `docs/IDEAS.md` (skip `## Archived`). Ideas are not designed yet, so they are **design-session**
candidates, not build candidates:
- An `à designer` entry is one the user wants to explore — list it, with the design tool that fits
  (`quests:design` for a quest, `NPC:create` / `NPC:dialogue` for NPCs, `bmad-forge-idea` or
  `gds-brainstorm-game` for a mechanic, `gds-quick-dev` only if the idea is already small and clear).
- A `brut` entry is mentioned only if it matches the user's focus or connects to a recommended step.
- Check whether a spec or design doc already covers it (`grep -ril "<keyword>" _bmad-output/implementation-artifacts docs/World`);
  if so, say the entry can move to `Archived` as `designé`.

### 2 — Design intent

From the GDD and narrative docs, write down in a few lines:
- What the game is (genre, pillars, core loop, the player fantasy).
- What the near-term target is (current area / vertical slice / milestone the docs point to).
- Which core pillars or GDD systems are **still missing or thin** in the code — check quickly with `Glob` /
  `Grep` under `Assets/_Game/Scripts/` rather than assuming (e.g. is there a save system? a world map? a
  second area?). Never list a system as missing without looking.

### 3 — Backlog triage

Read `deferred-work.md` in full. For each **open** item, classify:
- **Kind:** bug / feel-polish / tech-debt / test gap / missing feature.
- **Size:** quick (< 1 session), medium (one spec), large (several specs).
- **Impact:** does it hurt what a player sees in the current slice, or only edge cases / editor tools?
- **Still valid?** If the item names a file, class or behavior, spot-check that it still exists — a later spec
  may have fixed it without striking it out. Mark items you believe are obsolete as "likely resolved — verify".

Cluster related items (same system / same file) — a cluster is often one worthwhile spec.

### 4 — Recommend

Weigh candidates against: alignment with the design aims and near-term target, momentum (continuing a theme
that just shipped is cheap — context is fresh), player-visible impact, risk (bugs that corrupt state or block
progress rank high), and size. Respect the user's focus if one was given.

Output, in this order and in this shape (keep the whole answer scannable — no wall of text):

```
## Where the project stands
<3-5 lines: what the game is, the near-term target, what the last ~2 weeks delivered (themes, not a spec list)>

## Ready designs (handoffs)
<bullet per `ready` design doc: path, what it asks for, last updated, already covered? · Build with: <command> — or "None">

## Unfinished specs
<bullet per non-done spec: title, status, created, one-line "finish / close / drop" advice — or "None">

## Ideas to design
<bullet per `à designer` entry (plus focus-matching `brut` ones): title, domain, date · Explore with: <tool> — or "None">

## Recommended next steps
1. **<Short name>** — <kind, size> · <why now, in one or two sentences, tied to the design aims or recent work>
   Covers: <design doc / deferred items / spec it continues / GDD section>  ·  Start with: <`gds-quick-dev` one-line intent, or the build command from 1b>
2. …  (3 to 5 items, ranked)

## Quick wins
<2-4 small deferred items that fit in a short session, one line each>

## Worth a look later
<optional: larger design gaps from the GDD not yet started, one line each>
```

Rules for the recommendations:
- Every item cites its source (design doc path, deferred-work section, spec slug, GDD section) so the user can check it.
- A `ready` design doc is work the user has already decided on — rank it high unless it is blocked by a missing
  system or a progress-blocking bug outranks it.
- Mix kinds when it makes sense — don't recommend five polish tasks if a core pillar is missing, and don't push
  a large new system when a progress-blocking bug is open.
- No invented work: every recommendation must come from the specs, a design doc, the ideas inbox, the backlog, or a verified GDD gap.
- An idea from `docs/IDEAS.md` can be a recommended step only as a **design session** (it has no spec yet).

Finish with one question: which item the user wants to start (offer to launch `gds-quick-dev` with the drafted
intent, or the design doc's build command), or whether they want a different focus.
