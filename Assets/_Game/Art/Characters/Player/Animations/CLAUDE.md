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

## Attack Clips, Animation Events & SMB_AttackState

> Full per-clip event timing tables, `.meta` YAML format, and `SMB_AttackState` wiring rules live in:
> `Assets/_Game/Art/Characters/Player/Animations/Combat/CLAUDE.md`

---

## Attack Layer — CombatIdle Upper Body Idle Switching (Story 7.13)

The Attack layer (`BlendingMode: 0` Override, `DefaultWeight: 1`, mask: `UpperBodyMask`) contains a `CombatIdle` state that drives weapon-specific upper-body idles when the player is in combat stance.

**Design:**
- `IsInCombat = true` → Attack layer transitions from `LockOn Locomotion` → `CombatIdle` (0.2s crossfade)
- `IsInCombat = false` → `CombatIdle` → `LockOn Locomotion` (0.2s crossfade)
- `CombatIdle` plays `Unarmed Idle` by default; `Sword_AnimatorOverride.overrideController` swaps it to `SwordIdle` when the sword is equipped
- Lower body always plays the base locomotion blend tree — upper body mask ensures no interference

**Key fileIDs (PlayerAnimatorController.controller):**

| Object | fileID |
|--------|--------|
| CombatIdle AnimatorState | `7130000000000000001` |
| Transition: LockOn Locomotion → CombatIdle | `7130000000000000002` |
| Transition: CombatIdle → LockOn Locomotion | `7130000000000000003` |

**Clip GUIDs:**

| Clip | GUID |
|------|------|
| Unarmed Idle | `60a7f0e6f935b1e4f92daf7047221083` |
| SwordIdle | `e9be9528f8dab6c4a885457f845caa71` |

**AnimatorOverrideController (AOC) pattern:**
- `Sword_AnimatorOverride.overrideController` wraps `PlayerAnimatorController` and overrides `Unarmed Idle` → `SwordIdle`
- AOC matching is **by AnimationClip asset reference**, not by state name
- If "Unarmed Idle" doesn't appear in the AOC clip list, the `CombatIdle` state's `m_Motion` GUID is wrong — verify it matches `60a7f0e6f935b1e4f92daf7047221083`
- Attack states after finish return to `LockOn Locomotion`, then re-enter `CombatIdle` via the `IsInCombat=true` transition (brief 0.2s blend through LockOn Locomotion is acceptable for the prototype)

---

## Code Review Checklist — AnimatorController Files

| Severity | Pattern |
|----------|---------|
| MEDIUM | `WriteDefaultValues: true` on animator states — causes T-pose bleed; always use `false` |
| MEDIUM | AnimatorController `.controller` file fully rewritten via `Write` tool — transitions may not be visible in Unity Animator; prefer incremental MCP edits + YAML fixes |
| HIGH | `AnimatorOverrideController` clip override list does not contain "Unarmed Idle" — means `CombatIdle` state motion GUID is wrong; fix `m_Motion` in YAML before assigning AOC to weapons |
| MEDIUM | New weapon type's AOC overrides both idle AND attack clips unnecessarily — AOC only needs to override the clips that differ from the base controller; test sword only overrides `Unarmed Idle` |
