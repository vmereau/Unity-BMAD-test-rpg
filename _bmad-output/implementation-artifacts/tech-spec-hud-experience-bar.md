---
title: 'HUD Experience Bar'
slug: 'hud-experience-bar'
created: '2026-10-06'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C#', 'uGUI (UnityEngine.UI.Image)', 'GameEventSO<T> channels', 'NUnit EditMode tests']
files_to_modify:
  - 'Assets/_Game/Scripts/Player/Progression/LevelSystem.cs'
  - 'Assets/_Game/Scripts/UI/HUD/ExperienceBarUI.cs (new)'
  - 'Assets/_Game/Data/Events/OnPlayerXPProgressChanged.asset (new)'
  - 'Assets/_Game/Prefabs/UI/ExperienceBar.prefab (new)'
  - 'Assets/_Game/Prefabs/UI/UICanvas.prefab'
  - 'Assets/_Game/Prefabs/Player/Player.prefab (LevelSystem channel wiring)'
  - 'Assets/_Game/Scripts/UI/HUD/ExperienceBarUI.cs.meta, ExperienceBar.prefab.meta, OnPlayerXPProgressChanged.asset.meta (Unity-generated)'
  - 'Assets/Tests/EditMode/LevelSystemTests.cs'
  - 'Assets/_Game/Scripts/UI/HUD/CLAUDE.md'
  - 'Assets/_Game/Scripts/Player/Progression/CLAUDE.md'
code_patterns: ['HUD bar = Image fill scaled via localScale.x, driven by one GameEventSO_Float', 'subscribe OnEnable / unsubscribe OnDisable', 'Awake validates refs: Error+disable on required, Warn on optional channel', 'pure public static helper for testable formulas']
test_patterns: ['Assets/Tests/EditMode (Tests.EditMode asmdef refs Game)', 'namespace Game.Tests.EditMode', 'NUnit [Test], pure-function tests, no scene']
---

# Tech-Spec: HUD Experience Bar

**Created:** 2026-10-06

## Overview

### Problem Statement

The HUD shows health and stamina bars, but the player gets no constant visual feedback on how close
they are to the next level. XP progress is only visible through transient "Experience +N" toasts
and the Character Stats screen.

### Solution

Add a thin, subtle `ExperienceBar` under the `StaminaBar` on the HUD. A new
`GameEventSO_Float OnPlayerXPProgressChanged` channel (normalized 0–1 progress within the current
level) is raised by `LevelSystem`. A new `ExperienceBarUI` component, which mirrors `StaminaBarUI`,
listens to it and scales a fill image.

### Scope

**In Scope:**
- New event channel asset `OnPlayerXPProgressChanged` (`GameEventSO_Float`).
- `LevelSystem` computes and raises in-level progress, `(CurrentXP − prevThreshold) / (nextThreshold − prevThreshold)`
  with `prevThreshold = 0` at level 1, after every XP gain (after level-up processing) and once at startup.
- At max level, the progress value is `1` (bar shows full).
- New `ExperienceBarUI` script in `Scripts/UI/HUD/` and a new `ExperienceBar.prefab` in `Prefabs/UI/`:
  thin bar, no text label, placed under `StaminaBar` inside `UICanvas.prefab`.
- Update `Scripts/UI/HUD/CLAUDE.md` (scripts + event channels) and `Scripts/Player/Progression/CLAUDE.md` (event chain).
- EditMode test(s) for the progress calculation.

**Out of Scope:**
- Text label (`150 / 250`) or level number display.
- Fill animations / tweening, level-up flash effects.
- Hiding the bar at max level.
- Changes to `XPSystem`, `CharacterStatsUI`, or the toast system.
- Save/load of XP.

## Context for Development

### Codebase Patterns

- **HUD bar pattern (`StaminaBarUI`)**: `[SerializeField] Image _fillImage` + one `GameEventSO_Float`
  channel. `Awake`: `GameLog.Error` + `enabled = false` if `_fillImage` null; `GameLog.Warn` if the
  channel is null. `OnEnable` → `channel?.AddListener`, `OnDisable` → `channel?.RemoveListener`.
  Handler: `_fillImage.transform.localScale = new Vector3(Mathf.Clamp01(ratio), 1f, 1f)`.
  `private const string TAG = "[UI]"`, namespace `Game.UI`.
- **StaminaBar.prefab structure**: root `StaminaBar` (RectTransform anchors x 0.4–0.6, y 0.09–0.11,
  pivot (0.5, 0); `Image` background color (0.1,0.1,0.1,0.8), raycastTarget off; `StaminaBarUI`)
  → child `Fill` (RectTransform stretch, anchoredPosition (2,0), sizeDelta (−4,−4), **pivot (0, 0.5)**
  so localScale.x shrinks from the left; `Image` color (0.9,0.7,0.1,1), raycastTarget off).
  HealthBar sits at y 0.12–0.14. The instance in `UICanvas.prefab` sets layer 5 (UI) and repeats the
  anchors as overrides.
- **Hosting**: `UICanvas.prefab` is nested **inside `Player.prefab`**, the same prefab that holds
  `XPSystem` / `LevelSystem`. All `OnEnable`s of the prefab run before any `Start`, so a progress
  raise from `LevelSystem.Start()` always reaches `ExperienceBarUI`.
- **Progression math**: `XPSystem.CurrentXP` is cumulative. `ProgressionConfigSO.xpPerLevel`
  (`ProgressionConfig.asset`, guid `a563ac841fe0eba4299dd3728d472f34`) holds cumulative thresholds
  `{100, 250, 500, 900, 1400}`. Level L (1-based) needs `xpPerLevel[L-1]` to advance.
  `MaxLevel = xpPerLevel.Length + 1`. `LevelSystem.HandleXPGained` → `CheckLevelUp()` (a while loop,
  so multi-level jumps work). `LevelSystem.Awake` disables the component if `_config` / `_xpSystem` is null.
- **Event channels** live in `Assets/_Game/Data/Events/`. A float channel asset is a
  `GameEventSO_Float` (script guid `759cd54a7a594b644b64a4dce37f4b5a`), created via
  `Create → Game/Events → Float Event`.
- **Cross-system rule** (`project-context.md`): UI ↔ Progression via `GameEventSO<T>` only. No
  direct script refs (the refs in `CharacterStatsUI` are a documented exception, not a pattern).
- `StaminaSystem` never raises at startup. Its bar relies on the prefab's default full scale. The XP
  bar can't do that, because it starts empty, so `LevelSystem` raises once in `Start()` and the
  prefab's Fill is authored at localScale.x = 0 as a fallback.
- **Tests**: `Assets/Tests/EditMode/LevelSystemTests.cs` duplicates the level formula as a private
  helper. The new progress formula will instead be a `public static` method on `LevelSystem`, so
  tests exercise the real code (the `Tests.EditMode` asmdef references `Game`).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/HUD/StaminaBarUI.cs` | Template for `ExperienceBarUI` |
| `Assets/_Game/Prefabs/UI/StaminaBar.prefab` | Template for `ExperienceBar.prefab` (structure, colors, pivots) |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | HUD host. StaminaBar instance parent = `m_TransformParent: 7918251662037335709` |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Hosts `LevelSystem` (new channel field must be wired here) and nests `UICanvas` |
| `Assets/_Game/Scripts/Player/Progression/LevelSystem.cs` | Raises the new channel |
| `Assets/_Game/Scripts/Player/Progression/XPSystem.cs` | `CurrentXP` source (unchanged) |
| `Assets/_Game/Scripts/Player/Progression/ProgressionConfigSO.cs` | Cumulative thresholds |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs`, `GameEventSO_Float.cs` | Channel base + `GameEventSO_Float` |
| `Assets/_Game/Data/Events/OnPlayerStaminaChanged.asset` | Template for the new channel asset |
| `Assets/Tests/EditMode/LevelSystemTests.cs` | Where progress tests are added |

### Technical Decisions

- **Progress within the current level**: `prev = level == 1 ? 0 : xpPerLevel[level-2]`,
  `next = xpPerLevel[level-1]`, `progress = Clamp01((xp − prev) / (float)(next − prev))`.
  The bar resets on level-up.
- **New channel `OnPlayerXPProgressChanged` (`GameEventSO_Float`, normalized 0–1)**, raised by
  `LevelSystem` because it owns level and thresholds. Raised at the end of `HandleXPGained` (after
  `CheckLevelUp`), so a level-up never flashes a >1 value. Also raised once in `Start()`.
- **Max level → 1.0** (full bar). A degenerate span (`next <= prev`, a config error) also → 1.0.
- **Pure static helper** `LevelSystem.CalculateLevelProgress(int currentXP, int currentLevel, int[] xpPerLevel)`
  for testability.
- **Thin bar, no label**: anchors x 0.4–0.6, **y 0.08–0.086** (~⅓ the stamina bar height, just
  below it at 0.09). Fill sizeDelta (−2,−2), anchoredPosition (1,0). Background (0.1,0.1,0.1,0.6).
  Fill color soft violet (0.55,0.4,0.85,1). Fill localScale.x authored at 0.
- **No `GameLog` in the hot path**: the XP handler logs nothing extra (the level-up log already exists).
- ⚠️ `UICanvas.prefab` has an **uncommitted local modification** in the working tree (pre-existing, unrelated).
  The implementer must not revert it, and should commit the experience bar change knowing that diff is mixed in.

## Implementation Plan

### Tasks

- [x] Task 1: Create the progress event channel asset
  - File: `Assets/_Game/Data/Events/OnPlayerXPProgressChanged.asset` (+ `.meta`)
  - Action: Create a `GameEventSO_Float` asset named `OnPlayerXPProgressChanged`. Preferred route is
    Unity (MCP `manage_scriptable_object`, or `execute_code` with
    `ScriptableObject.CreateInstance<GameEventSO_Float>()` + `AssetDatabase.CreateAsset`), which keeps
    the `.meta` Unity-generated.
  - Notes: Payload = normalized 0–1 progress within the current level. Mirror `OnPlayerStaminaChanged.asset`.

- [x] Task 2: Add the pure progress formula to `LevelSystem`
  - File: `Assets/_Game/Scripts/Player/Progression/LevelSystem.cs`
  - Action: Add
    ```csharp
    /// <summary>
    /// Normalized 0–1 progress through the current level. Thresholds are cumulative XP.
    /// Returns 1 at max level or when thresholds are missing/degenerate.
    /// </summary>
    public static float CalculateLevelProgress(int currentXP, int currentLevel, int[] xpPerLevel)
    {
        if (xpPerLevel == null || xpPerLevel.Length == 0) return 1f;
        int level = Mathf.Max(1, currentLevel);
        if (level > xpPerLevel.Length) return 1f;           // max level → full
        int prev = level == 1 ? 0 : xpPerLevel[level - 2];
        int next = xpPerLevel[level - 1];
        int span = next - prev;
        if (span <= 0) return 1f;                            // config error → full
        return Mathf.Clamp01((currentXP - prev) / (float)span);
    }
    ```
  - Notes: Static and side-effect free, so EditMode tests call it directly.

- [x] Task 3: Raise progress from `LevelSystem`
  - File: `Assets/_Game/Scripts/Player/Progression/LevelSystem.cs`
  - Action:
    1. Add `[SerializeField] private GameEventSO_Float _onPlayerXPProgressChanged;` after `_onLevelUp`.
    2. In `Awake` (after the existing checks), add
       `if (_onPlayerXPProgressChanged == null) GameLog.Warn(TAG, "OnPlayerXPProgressChanged event not assigned — XP bar will not update.");`
       Warn only. Don't disable.
    3. Add `private void RaiseProgress() => _onPlayerXPProgressChanged?.Raise(CalculateLevelProgress(_xpSystem.CurrentXP, CurrentLevel, _config.xpPerLevel));`
    4. Add `private void Start() { RaiseProgress(); }`. Start only runs when the component is enabled,
       so `_config` / `_xpSystem` are guaranteed non-null there.
    5. In `HandleXPGained`, call `RaiseProgress()` **after** `CheckLevelUp()`.
    6. Update the class XML summary to mention the progress channel.
  - Notes: Don't add logging in `RaiseProgress`.

- [x] Task 4: Create `ExperienceBarUI`
  - File: `Assets/_Game/Scripts/UI/HUD/ExperienceBarUI.cs` (new; let Unity generate the `.meta` with its `MonoImporter` block)
  - Action: Copy `StaminaBarUI` exactly, renaming as follows: class `ExperienceBarUI`, field
    `[SerializeField] private GameEventSO_Float _onPlayerXPProgressChanged;`, handler
    `HandleXPProgressChanged(float progress)`. Same `Awake` validation (Error+disable on `_fillImage`,
    Warn on channel), same `OnEnable`/`OnDisable` with `?.`, same `localScale.x = Clamp01` fill.
    Namespace `Game.UI`, `TAG = "[UI]"`.
  - Notes: XML summary: "Thin HUD bar below the StaminaBar showing progress toward the next level.
    Subscribes to OnPlayerXPProgressChanged (normalized 0–1, raised by LevelSystem)."

- [x] Task 5: Create `ExperienceBar.prefab`
  - File: `Assets/_Game/Prefabs/UI/ExperienceBar.prefab` (+ `.meta`)
  - Action: Build it the same way as `StaminaBar.prefab`, preferably in Unity via MCP rather than YAML
    copying, to get fresh fileIDs and GUID:
    - Root `ExperienceBar` (layer 5 UI): RectTransform anchorMin (0.4, 0.08), anchorMax (0.6, 0.086),
      anchoredPosition (0,0), sizeDelta (0,0), pivot (0.5, 0). `Image` color (0.1, 0.1, 0.1, 0.6),
      raycastTarget off. `ExperienceBarUI` with `_fillImage` → child Fill's Image and
      `_onPlayerXPProgressChanged` → the Task 1 asset.
    - Child `Fill` (layer 5): RectTransform anchors (0,0)–(1,1), anchoredPosition (1, 0), sizeDelta (−2, −2),
      **pivot (0, 0.5)**, **localScale (0, 1, 1)**. `Image` color (0.55, 0.4, 0.85, 1), raycastTarget off.

- [x] Task 6: Place the bar in the HUD
  - File: `Assets/_Game/Prefabs/UI/UICanvas.prefab`
  - Action: Add an `ExperienceBar` prefab instance as a sibling of `StaminaBar` (same parent transform,
    fileID `7918251662037335709`), ordered directly after `StaminaBar`. Keep the prefab anchors.
    Prefer MCP (`manage_prefabs`, or `execute_code` with `PrefabUtility.InstantiatePrefab` + `SaveAsPrefabAsset`).
    If you edit the YAML by hand, follow up with `refresh_unity(mode="if_dirty")`, **never `force`**.
  - Notes: The file already has an unrelated uncommitted change. Preserve it.

- [x] Task 7: Wire the channel on the Player's `LevelSystem`
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action: Assign `OnPlayerXPProgressChanged.asset` to `LevelSystem._onPlayerXPProgressChanged`.
    Then verify that `TestScene.unity` / `Core.unity` Player instances have no override nulling the field.
  - Notes: `UICanvas` is nested in `Player.prefab`, so confirm the nested `ExperienceBar` shows up there too.

- [x] Task 8: EditMode tests
  - File: `Assets/Tests/EditMode/LevelSystemTests.cs`
  - Action: Add tests that call `Game.Progression.LevelSystem.CalculateLevelProgress` with `DefaultThresholds`:
    - `(0, 1)` → 0
    - `(50, 1)` → 0.5
    - `(100, 2)` → 0 (just levelled)
    - `(175, 2)` → 0.5 ((175−100)/150)
    - `(1399, 5)` → (1399−900)/500 ≈ 0.998
    - `(1400, 6)` → 1 (max level)
    - `(9999, 6)` → 1
    - null thresholds → 1
    - empty thresholds → 1
    - degenerate `{100, 100}` at level 2 → 1
    - `currentLevel = 0` → treated as level 1
  - Notes: Use `Assert.AreEqual(expected, actual, 0.001f)`. Add `using Game.Progression;`.

- [x] Task 9: Documentation
  - Files: `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`, `Assets/_Game/Scripts/Player/Progression/CLAUDE.md`
  - Action:
    - HUD CLAUDE.md: add an `ExperienceBarUI` row to Scripts, add `OnPlayerXPProgressChanged` to
      Event Channels, and extend the `Awake`-disable gotcha to include `ExperienceBarUI`.
    - Progression CLAUDE.md: add `LevelSystem → OnPlayerXPProgressChanged (0–1 in-level progress,
      raised in Start and after each XP gain) → ExperienceBarUI` to the event chain.

### Acceptance Criteria

- [ ] AC 1: Given a new game (0 XP, level 1), when play mode starts, then a thin bar is visible just
  below the StaminaBar with an empty fill.
- [ ] AC 2: Given level 1 with 0 XP, when the player gains 50 XP, then the fill is at 50%.
- [ ] AC 3: Given level 1 with 50 XP, when the player gains 60 XP (total 110 → level 2), then the fill
  shows (110−100)/(250−100) ≈ 7% and never shows a value above 100% during the transition.
- [ ] AC 4: Given level 1 with 0 XP, when the player gains 600 XP at once (→ level 4), then the fill shows
  (600−500)/(900−500) = 25%.
- [ ] AC 5: Given the player reaches max level (≥1400 XP, level 6), when any XP is gained, then the fill
  stays at 100%.
- [ ] AC 6: Given `ExperienceBarUI._fillImage` is unassigned, when the scene loads, then `GameLog.Error`
  is logged, the component disables itself, and no exception is thrown.
- [ ] AC 7: Given `ExperienceBarUI._onPlayerXPProgressChanged` or `LevelSystem._onPlayerXPProgressChanged`
  is unassigned, when the scene loads, then a `GameLog.Warn` is logged and the game runs normally (bar stays empty).
- [ ] AC 8: Given `ExperienceBarUI` is disabled or destroyed, when XP is gained, then no listener
  fires on it (it unsubscribed in `OnDisable`).
- [ ] AC 9: Given the EditMode suite, when it runs, then all new `CalculateLevelProgress` tests and all
  pre-existing tests pass.
- [ ] AC 10: Given the HUD, when inspected, then the health, stamina and experience bars are aligned on the
  same horizontal span (x 0.4–0.6) and the experience bar is visibly thinner than the stamina bar.

## Additional Context

### Dependencies

- None external. Depends on the existing `XPSystem` → `OnXPGained` → `LevelSystem` chain and
  `ProgressionConfig.asset`.
- Unity Editor and MCP connection, for creating the asset and prefab and wiring them.

### Testing Strategy

- **Unit (EditMode)**: `CalculateLevelProgress` cases in Task 8. Run via MCP `run_tests` (EditMode).
- **Manual (Play Mode, TestScene or Core)**: Start the game and check the bar is empty under the stamina bar.
  Kill enemies or award XP and watch the fill grow. Cross the 100 XP threshold and check the bar resets near
  empty (a "Level up!" toast fires too). Check `read_console` for no errors or warnings from `ExperienceBarUI`
  / `LevelSystem`.
- Check the HUD at 16:9 and an ultrawide resolution. The bar is anchor-based, so its height scales with the screen.

### Notes

- **Risk: raise order at startup.** This relies on `UICanvas` being nested in `Player.prefab`, so the
  UI's `OnEnable` runs before `LevelSystem.Start`. If the UI ever moves to a separately loaded scene,
  the bar shows empty until the first XP gain. That's acceptable for now, and the save/load work can add a re-raise.
- **Risk: thin bar vs. Fill inset.** At 1080p the bar is ~6.5px tall. A 2px total vertical inset leaves
  ~4.5px of fill. If that reads too faint, reduce the inset to sizeDelta (−2, −1) or raise anchorMax.y to 0.087.
- **Limitation:** There's no save/load of XP yet. Once it exists, the loader must trigger a progress
  re-raise (e.g. `LevelSystem` recomputing level and raising).
- **Future:** an optional `150 / 250` label or level number, a fill tween, a level-up flash, and hiding the bar at max level.

## Review Notes

- Adversarial review completed (isolated subagent, diff-only context)
- Findings: 14 total. 3 fixed (F4 ordering dependency documented in HUD + Progression CLAUDE.md, F13 spec status and file list, F14 test-file formatting/summary). 11 skipped:
  - F1–F3, F12 are outside this feature (unrelated working-tree changes). F1, the guard memory pointing at a deleted killed fact, was fixed manually by the user.
  - F5, F8, F10, F11 are noise or spec decisions.
  - F6 (pre-existing `MaxLevel` null deref), F7 (raise order untested; verified in Play Mode), F9 (thin bar at 720p; `UICanvas` has no CanvasScaler) are left as follow-ups.
- Resolution approach: auto-fix
- Runtime check (Play Mode): fill 0 → 0.5 (+50) → 0.067 (110 XP, Lv 2) → 0.25 (600 XP, Lv 4) → 1.0 at max level. Bar 384 × 6.5 px vs stamina 384 × 21.6 px.
- During implementation, an external process reset `UICanvas.prefab` to HEAD. The ExperienceBar instance was re-added. The pre-existing `m_SizeDelta.y` 80 → 0 diff was lost and was not restored.
