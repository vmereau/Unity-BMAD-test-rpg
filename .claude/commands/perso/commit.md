> **Before proceeding, read `.claude/rules/git-conventions.md`** to understand the commit format and project-specific scopes required for this repository.

Your task is to inspect the current repository state, craft a well-structured conventional commit, and push it to the remote.

## Steps


### 1 — Inspect repository state
Run these commands in parallel:
- `git status` — list staged, unstaged, and untracked files
- `git diff` — show unstaged changes
- `git diff --cached` — show staged changes
- `git log --oneline -5` — review recent commit style for consistency

### 2 — Analyse changes
Review all changes and determine:
- **What** changed (files, functionality)
- **Why** it was changed (infer from context if not obvious)
- The correct **type** and **scope** from the conventions
- Whether any change is a **breaking change**

If changes clearly belong to multiple unrelated types, commit them separately (stage and commit each group individually before pushing).

### 3 — Quality checks

Run all checks before staging anything:
```bash
npm run test:run && npm run check && npm run lint && npm run format:check
```

- If `test:run` fails: fix the failing tests (or the logic they cover), then re-run before proceeding.
- If `check` or `lint` fail: fix the reported issues, then re-run before proceeding.
- If `format:check` fails: run `npm run format` to auto-fix, then re-run `format:check` to confirm.

Do not proceed to staging until all four pass.

### 4 — Stage changes
Stage all relevant changes:
```
git add <specific files>
```
Prefer specific file paths over `git add -A`. Exclude files that should not be committed (secrets, generated artefacts already in `.gitignore`, etc.).

### 5 — Write the commit message
Follow `.claude/git-conventions.md` exactly:
- `<type>(<scope>): <short description>` — imperative, lowercase, ≤ 72 chars
- Optional body explaining *what* and *why*
- `BREAKING CHANGE:` footer if applicable
- Always append the co-author footer:
  ```
  Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
  ```

### 6 — Commit
Use a heredoc to preserve formatting:
```bash
git commit -m "$(cat <<'EOF'
<type>(<scope>): <description>

<optional body>

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"
```

### 7 — Push
After the commit succeeds, push to the current remote branch:
```bash
git push
```
If the branch has no upstream yet:
```bash
git push -u origin HEAD
```

### 8 — Confirm
Run `git log --oneline -3` and show the user the final commit hash and message.
