# CLAUDE.md — Assets/_Game/Art/Characters/Player/Animations

> Loaded when Claude accesses files in this folder. Covers AnimatorController authoring rules and gotchas.

---

## Animator Controller Best Practices

- **Always set `WriteDefaultValues: false`** on all animator states. With `true`, states write T-pose defaults for bones they don't animate, causing pose corruption during transitions.
- **Smooth transition durations:** Use 0.1–0.2s crossfade for most transitions. Instant (0s) transitions look snappy/jarring.
- **IsRising detection:** Use `velocity.y > 0.1f` (not `> 0f`) to avoid float noise triggering false positives at rest.

---

## MCP Tool Quirks — AnimatorController

- **`manage_animation(controller_add_transition)`** sets wrong `conditionMode` for bools: uses `3` (Equals) instead of `2` (IfNot/false) or `1` (If/true). Always verify and fix via direct YAML edits.
- **AnimatorController YAML-only transitions** may not be visible in Unity's Animator tab. When rewriting `.controller` files entirely via `Write`, transitions defined only in YAML may need to be manually re-created in the Unity Animator window. Prefer using MCP tools for individual transitions, then fix known bugs in YAML.

---

## Code Review Checklist — AnimatorController Files

| Severity | Pattern |
|----------|---------|
| MEDIUM | `WriteDefaultValues: true` on animator states — causes T-pose bleed; always use `false` |
| MEDIUM | AnimatorController `.controller` file fully rewritten via `Write` tool — transitions may not be visible in Unity Animator; prefer incremental MCP edits + YAML fixes |
