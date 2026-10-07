---
description: Audit all CLAUDE.md files for size, folder relevance, duplication and stale facts, then restructure them (one commit per approved part)
argument-hint: "[folder to limit the audit to]"
---

> **Before proceeding, read `.claude/rules/git-conventions.md`** — every applied part is committed separately.

Your task is to audit every CLAUDE.md in this project (and the docs they point to) for size, relevance to their
folder, duplication and staleness, propose a restructuring, and apply the parts the user approves.

Optional argument: a folder to limit the audit to (e.g. `Assets/_Game/Scripts/UI`). Default: whole project.

## How Claude Code loads these files (base every decision on this)

- **Root `CLAUDE.md`** — loaded in every session. Keep it an index + truly cross-cutting rules.
- **Folder `CLAUDE.md`** — loaded when a file in that folder (or below) is read, **together with every ancestor
  folder's CLAUDE.md**. Content must be relevant to *all* files under the folder.
- **Topic files** (`PLAYER_COMBO.md`, `Data/NPCs/DIALOGUE.md`, …) — loaded only when a CLAUDE.md tells Claude to
  read them ("read X before touching Y"). Use for long recipes, timelines, reference tables.
- **`.claude/rules/*.md` with a `paths:` frontmatter** — loaded only when matching files are touched. Use for a
  contract that spans several unrelated folders (e.g. `attack-pipeline.md`). Without `paths:` → always loaded.

## Steps

### 1 — Inventory

List every CLAUDE.md, `.claude/rules/*.md` and topic `.md` referenced from them, with line and byte counts
(exclude `Library/`, `_bmad/`, `_bmad-output/`, `node_modules/`). Read them all in full. Also list the folder
tree of `Assets/_Game/` (depth 3) so you know where code and assets actually live.

### 2 — Check each file against these criteria

| Problem | Signal | Typical fix |
|---|---|---|
| **Too large** | > ~150 lines / ~10 KB, or several unrelated `##` sections | Split by sub-folder, or move long recipes to a topic file |
| **Wrong folder** | Section is about a file/prefab/asset that lives elsewhere (check with Glob — e.g. a script physically in `Scripts/AI` documented in `Scripts/World`) | Move to the folder that holds the file; leave a one-line pointer |
| **Over-broad** | Section only matters for one sub-folder, but sits in a parent | Push down to a (new) sub-folder CLAUDE.md |
| **Duplicated** | Same rule/table/warning in 2+ files | Keep one owner; others point to it. Cross-folder contract → path-scoped rule |
| **Copied source** | Code blocks reproducing method bodies | Replace with the contract as rules + file reference |
| **Generic advice** | Standard Unity/C# knowledge not specific to this project | Delete |
| **History noise** | "Story 7.x", "added in …", "previously …" | Delete unless the history explains a current constraint |
| **Stale / wrong** | Paths, class/asset names, parameter lists, timings, counts | **Verify against the code/assets** (Grep, Glob, read YAML/.meta, `.inputactions`); fix to match reality |
| **Contradiction** | Two files state opposite things | Verify which is true, fix both |
| **Broken table** | Blockquotes/blank lines inside markdown tables | Fold into the row or move below the table |
| **Index drift** | Root "Folder CLAUDE.md Index" missing or misdescribing a file | Update the index |

Never trust a doc's claim when it can be checked cheaply — stale facts are the most valuable finding.

### 3 — Report and propose (do not edit yet)

Present:
1. One short paragraph on overall state.
2. Main issues, most impactful first, each with the concrete fix (new files named with paths).
3. Stale/contradictory items found, with the evidence.
4. Files that are fine as-is.
5. The plan split into **parts**, one commit each (e.g. stale fixes · split A · split B · shared rule · moves · trims).

Ask the user which parts to apply.

### 4 — Apply approved parts, one commit per part

For each part:
- Write/edit the files. Keep each rule in exactly one place; leave one-line pointers elsewhere.
- Match the existing file style: `# CLAUDE.md — <path>` title, `> Loaded when …` summary line, `---` separators,
  `## Code Review Checklist — <area>` table last.
- **Every new `.md` under `Assets/` needs a `.meta`** (Unity `TextScriptImporter`, random 32-hex GUID) committed with it:
  ```
  fileFormatVersion: 2
  guid: <32 hex>
  TextScriptImporter:
    externalObjects: {}
    userData: 
    assetBundleName: 
    assetBundleVariant: 
  ```
- Update the root CLAUDE.md index (and "Key File Locations" for new `.claude/rules` files) in the same part.
- Stage only the files of this part (never unrelated working-tree changes) and commit:
  `docs(claude): <imperative summary>` with a body explaining what moved and why, and any stale facts corrected.

Do not push — tell the user to run `/perso:commit` or `git push` when ready.

### 5 — Summarize

List commits (hash + subject), notable corrected facts, resulting sizes of the largest files, and anything
deliberately left unchanged.
