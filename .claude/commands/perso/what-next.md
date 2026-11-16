---
description: Review recent specs, the game's design goals and the deferred-work backlog, then advise what to work on next
argument-hint: "[optional focus, e.g. combat, area 1, polish, quick wins]"
---

Advise the user on what to work on next. Read the project's recent history, its design intent and its open
backlog, then recommend a short, ranked list of next steps. **This command is read-only — do not edit any file,
create a spec or start implementing.** It ends with a recommendation and a question.

Optional focus from the user: `$ARGUMENTS` (empty → no focus, consider everything).

## Sources

| What | Where | How to read it |
|---|---|---|
| Game identity & aims | `_bmad-output/gdd.md` | Read `Executive Summary`, `Goals and Context`, `Core Gameplay`, `Development Epics`, `Success Metrics`, `Out of Scope` (use `grep -n "^## "` to find them; skip the rest unless the focus needs it) |
| Story / world | `_bmad-output/narrative-design.md`, `_bmad-output/story-2026-05-03.md` | Skim headings + the opening sections — enough to know the setting, the arrival beat and the current area |
| Recent work | `_bmad-output/implementation-artifacts/spec-*.md` | Frontmatter (`title`, `type`, `created`, `status`) + the `## Intent` section |
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

## Unfinished specs
<bullet per non-done spec: title, status, created, one-line "finish / close / drop" advice — or "None">

## Recommended next steps
1. **<Short name>** — <kind, size> · <why now, in one or two sentences, tied to the design aims or recent work>
   Covers: <deferred items / spec it continues / GDD section>  ·  Start with: `gds-quick-dev` <one-line intent>
2. …  (3 to 5 items, ranked)

## Quick wins
<2-4 small deferred items that fit in a short session, one line each>

## Worth a look later
<optional: larger design gaps from the GDD not yet started, one line each>
```

Rules for the recommendations:
- Every item cites its source (deferred-work section, spec slug, GDD section) so the user can check it.
- Mix kinds when it makes sense — don't recommend five polish tasks if a core pillar is missing, and don't push
  a large new system when a progress-blocking bug is open.
- No invented work: every recommendation must come from the specs, the backlog, or a verified GDD gap.

Finish with one question: which item the user wants to start (offer to launch `gds-quick-dev` with the drafted
intent), or whether they want a different focus.
