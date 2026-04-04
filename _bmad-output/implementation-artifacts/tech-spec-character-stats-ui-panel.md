---
title: 'Character Stats UI Panel'
slug: 'character-stats-ui-panel'
created: '2026-04-02'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'URP', 'TextMeshPro', 'GameEventSO', 'UGUI']
files_to_modify:
  - 'Assets/_Game/Scripts/UI/CharacterStatsUI.cs'
  - 'Assets/_Game/Prefabs/UI/UICanvas.prefab'
code_patterns:
  - 'GameEventSO subscription in OnEnable/OnDisable'
  - 'IScreenPanel interface'
  - 'TMP_Text.SetText for zero-alloc updates'
  - 'OnDisable null guard (Awake may disable before OnEnable runs)'
test_patterns:
  - 'Manual playtest via C key'
---

# Tech-Spec: Character Stats UI Panel

**Created:** 2026-04-02

---

## Overview

### Problem Statement

`CharacterStatsUI.cs` is a placeholder stub with no display logic. Players have no way to view their character's progression data (level, XP, LP) or combat attributes (stats, HP, stamina) in a dedicated screen, even though all backing systems already expose the required data.

### Solution

Implement `CharacterStatsUI.cs` as a read-only, event-driven stats panel that reads initial values on open and stays live via `GameEventSO` subscriptions while the panel is visible. Organize data under three labeled sections: **Progression**, **Vitals**, and **Attributes**. Build the panel UI hierarchy inside the existing `CharacterStats` panel root in `UICanvas.prefab`.

### Scope

**In Scope:**
- Full implementation of `CharacterStatsUI.cs` (replace stub)
- UI hierarchy inside the existing CharacterStats panel root in `UICanvas.prefab`
- Three section headers with TMP labels: Progression, Vitals, Attributes
- Fields: Level, XP / XP-to-next-level (or "MAX"), Learning Points, HP (current/max), Stamina (current/max), Strength, Dexterity, Endurance, Intelligence (Mana property), Defense
- Live label updates while panel is open, driven by existing `GameEventSO` channels

**Out of Scope:**
- Editable fields or stat allocation UI (TrainerNPC is the upgrade path)
- Mana/Intelligence bar in the HUD
- New `GameEventSO` assets (all needed events already exist)
- Skill list (`PlayerSkills` — separate story)
- Cursor management (handled entirely by `UIScreenManager`)

---

## Context for Development

### Codebase Patterns

- **GameEventSO subscription:** Always subscribe in `OnEnable`, unsubscribe in `OnDisable`. Never in `Start` or `Awake`. See `Assets/_Game/Scripts/UI/CLAUDE.md`.
- **OnDisable null guard:** When `Awake` can set `enabled = false`, `OnDisable` must guard against fields initialized in `OnEnable` being null. Required pattern (see root `CLAUDE.md`):
  ```csharp
  private void OnDisable()
  {
      if (_onLevelUp == null) return; // Guard: Awake may disable before OnEnable runs
      // ... unsubscribe
  }
  ```
- **TMP_Text:** Always use `TMP_Text.SetText(string)` (not `.text = ...`) to avoid per-frame allocations.
- **String format:** Use `$"HP: {value:F0}/{max:F0}"` — no `StringBuilder` needed since updates are event-driven (not per-frame).
- **Logging:** All `Debug.Log` calls replaced by `GameLog.Info(TAG, msg)`. Every class defines `private const string TAG`.
- **IScreenPanel:** `OnScreenOpen` and `OnScreenClose` are called by `UIScreenManager` after `SetActive`. No cursor management here.
- **Cross-system MonoBehaviour refs:** `OnPlayerStaminaChanged` broadcasts only a normalized ratio (0–1), not actual stamina values. `OnXPGained` broadcasts XP *gained*, not the running total. Therefore `CharacterStatsUI` must hold direct `SerializeField` refs to `StaminaSystem` (for `CurrentStamina`/`MaxStamina`) and `XPSystem` (for `CurrentXP`). This follows the same prototype-exception pattern used in `StaminaSystem → PlayerStats` (noted with comment in code).
- **Intelligence rename (done):** `PlayerStats.Mana` has been renamed to `PlayerStats.Intelligence` (and `StatType.Mana` → `StatType.Intelligence`, `_baseIntelligence`, `_equipIntBonus`, `ProgressionConfigSO.baseIntelligence`, `EquipableItemSO.intelligenceBonus`). Use `_playerStats.Intelligence` directly.
- **XP threshold index:** `ProgressionConfigSO.xpPerLevel[currentLevel - 1]` = XP required to reach the next level from `currentLevel`. Valid range: index 0 to `xpPerLevel.Length - 1`. When `CurrentLevel >= MaxLevel`, show `"MAX"` instead.
- **MaxHealth:** `PlayerHealth.MaxHealth` is a public property — no separate `CombatConfigSO` ref needed in the UI.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/CharacterStatsUI.cs` | Stub to replace |
| `Assets/_Game/Scripts/UI/IScreenPanel.cs` | Interface contract |
| `Assets/_Game/Scripts/UI/UIScreenManager.cs` | Tab system, ScreenTab.CharacterStats = index 2 |
| `Assets/_Game/Scripts/UI/HealthBarUI.cs` | Reference pattern: GameEventSO_Float + config SO |
| `Assets/_Game/Scripts/UI/StaminaBarUI.cs` | Reference pattern: GameEventSO_Float subscription |
| `Assets/_Game/Scripts/Player/PlayerHealth.cs` | CurrentHealth (float), MaxHealth (float) |
| `Assets/_Game/Scripts/Combat/StaminaSystem.cs` | CurrentStamina (float), MaxStamina (float, dynamic) |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | Strength, Dexterity, Endurance, Mana, Defense (int) |
| `Assets/_Game/Scripts/Progression/LevelSystem.cs` | CurrentLevel (int), MaxLevel (int) |
| `Assets/_Game/Scripts/Progression/XPSystem.cs` | CurrentXP (int) |
| `Assets/_Game/Scripts/Progression/LearningPointSystem.cs` | CurrentLP (int) |
| `Assets/_Game/ScriptableObjects/Config/ProgressionConfigSO.cs` | xpPerLevel[] thresholds |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Float.cs` | Event type |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Int.cs` | Event type |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_Void.cs` | Event type |

### Technical Decisions

- **Event-driven, not polled:** All 6 event channels already exist. The panel subscribes on `OnEnable` (which fires when `SetActive(true)` via `UIScreenManager.OpenTab`) and unsubscribes on `OnDisable` (fires on `SetActive(false)` via `CloseAll`). No `Update()` loop needed.
- **Refresh on enable:** Because events only fire on *change*, `OnEnable` calls `Refresh()` immediately after subscribing to populate all labels with current values from the direct refs. This handles the case where stats changed while the panel was closed.
- **One `Refresh()` + 6 targeted methods:** `Refresh()` calls each targeted method (`RefreshLevel`, `RefreshXP`, `RefreshLP`, `RefreshHealth`, `RefreshStamina`, `RefreshStats`). Each event handler calls only its relevant method, avoiding unnecessary label rebuilds.
- **UI layout:** `VerticalLayoutGroup` on the panel root, with three child sections each containing a bold header `TMP_Text` and a nested `VerticalLayoutGroup` of stat rows. `ContentSizeFitter` on the root so the panel auto-sizes.

---

## Implementation Plan

### Tasks

**Task 1 — Update `Assets/_Game/Scripts/UI/CharacterStatsUI.cs`**

Replace the stub entirely with the following structure:

```
namespace Game.UI
class CharacterStatsUI : MonoBehaviour, IScreenPanel
  const string TAG = "[UI]"

  // TMP Labels — wire in Inspector
  [SerializeField] TMP_Text _levelLabel
  [SerializeField] TMP_Text _xpLabel
  [SerializeField] TMP_Text _lpLabel
  [SerializeField] TMP_Text _healthLabel
  [SerializeField] TMP_Text _staminaLabel
  [SerializeField] TMP_Text _strengthLabel
  [SerializeField] TMP_Text _dexterityLabel
  [SerializeField] TMP_Text _enduranceLabel
  [SerializeField] TMP_Text _intelligenceLabel
  [SerializeField] TMP_Text _defenseLabel

  // Config SO
  [SerializeField] ProgressionConfigSO _progressionConfig

  // Cross-system MonoBehaviour refs (prototype: no event channel exposes all needed values)
  [SerializeField] LevelSystem _levelSystem
  [SerializeField] LearningPointSystem _lpSystem
  [SerializeField] XPSystem _xpSystem
  [SerializeField] PlayerHealth _playerHealth
  [SerializeField] StaminaSystem _staminaSystem
  [SerializeField] PlayerStats _playerStats

  // Event SOs
  [SerializeField] GameEventSO_Int _onLevelUp
  [SerializeField] GameEventSO_Int _onLPChanged
  [SerializeField] GameEventSO_Float _onPlayerHealthChanged
  [SerializeField] GameEventSO_Float _onPlayerStaminaChanged   // ratio — used as trigger only
  [SerializeField] GameEventSO_Void _onStatsChanged
  [SerializeField] GameEventSO_Int _onXPGained

  Awake():
    if any TMP label, _progressionConfig, _levelSystem, _lpSystem, _xpSystem,
       _playerHealth, _staminaSystem, or _playerStats is null:
      GameLog.Error(TAG, "CharacterStatsUI: missing required reference — disabled")
      enabled = false
      return
    warn (not disable) if any event SO is null

  OnEnable():
    _onLevelUp?.AddListener(HandleLevelUp)
    _onLPChanged?.AddListener(HandleLPChanged)
    _onPlayerHealthChanged?.AddListener(HandleHealthChanged)
    _onPlayerStaminaChanged?.AddListener(HandleStaminaChanged)
    _onStatsChanged?.AddListener(HandleStatsChanged)
    _onXPGained?.AddListener(HandleXPGained)
    Refresh()   // populate all labels immediately on open

  OnDisable():
    if _onLevelUp == null return   // null guard: Awake may disable before OnEnable runs
    _onLevelUp.RemoveListener(HandleLevelUp)
    _onLPChanged?.RemoveListener(HandleLPChanged)
    _onPlayerHealthChanged?.RemoveListener(HandleHealthChanged)
    _onPlayerStaminaChanged?.RemoveListener(HandleStaminaChanged)
    _onStatsChanged?.RemoveListener(HandleStatsChanged)
    _onXPGained?.RemoveListener(HandleXPGained)

  // IScreenPanel
  OnScreenOpen()  → GameLog.Info(TAG, "Character Stats opened")
  OnScreenClose() → GameLog.Info(TAG, "Character Stats closed")

  // Event handlers — each calls its targeted refresh
  HandleLevelUp(int _)     → RefreshLevel(); RefreshXP()
  HandleLPChanged(int _)   → RefreshLP()
  HandleHealthChanged(float _) → RefreshHealth()
  HandleStaminaChanged(float _) → RefreshStamina()
  HandleStatsChanged(bool _)   → RefreshStats()
  HandleXPGained(int _)    → RefreshXP()

  Refresh() → RefreshLevel(); RefreshXP(); RefreshLP(); RefreshHealth(); RefreshStamina(); RefreshStats()

  RefreshLevel():
    _levelLabel.SetText($"Level: {_levelSystem.CurrentLevel}")

  RefreshXP():
    int lvl = _levelSystem.CurrentLevel
    int max = _levelSystem.MaxLevel
    string threshold = lvl >= max ? "MAX" : _progressionConfig.xpPerLevel[lvl - 1].ToString()
    _xpLabel.SetText($"XP: {_xpSystem.CurrentXP} / {threshold}")

  RefreshLP():
    _lpLabel.SetText($"Learning Points: {_lpSystem.CurrentLP}")

  RefreshHealth():
    _healthLabel.SetText($"HP: {_playerHealth.CurrentHealth:F0} / {_playerHealth.MaxHealth:F0}")

  RefreshStamina():
    _staminaLabel.SetText($"STA: {_staminaSystem.CurrentStamina:F0} / {_staminaSystem.MaxStamina:F0}")

  RefreshStats():
    _strengthLabel.SetText($"STR: {_playerStats.Strength}")
    _dexterityLabel.SetText($"DEX: {_playerStats.Dexterity}")
    _enduranceLabel.SetText($"END: {_playerStats.Endurance}")
    _intelligenceLabel.SetText($"INT: {_playerStats.Intelligence}")
    _defenseLabel.SetText($"DEF: {_playerStats.Defense}")
```

**Usings required:** `Game.Combat`, `Game.Core`, `Game.Player`, `Game.Progression`, `TMPro`, `UnityEngine`

---

**Task 2 — Build the CharacterStats panel UI hierarchy in `UICanvas.prefab`**

Locate the existing `CharacterStats` panel root GO (already referenced as `_tabPanelRoots[2]` in `UIScreenManager`). Build this child hierarchy:

```
CharacterStats (existing root — CharacterStatsUI component already here, starts SetActive false)
  └── Panel_BG (Image, semi-transparent dark background, anchored center 400×500)
        └── ScrollContent (VerticalLayoutGroup, spacing=12, padding=16, ContentSizeFitter height)
              ├── Section_Progression
              │     ├── Header_Progression (TMP_Text, bold, "— PROGRESSION —")
              │     ├── Label_Level      (TMP_Text)
              │     ├── Label_XP         (TMP_Text)
              │     └── Label_LP         (TMP_Text)
              ├── Section_Vitals
              │     ├── Header_Vitals    (TMP_Text, bold, "— VITALS —")
              │     ├── Label_HP         (TMP_Text)
              │     └── Label_Stamina    (TMP_Text)
              └── Section_Attributes
                    ├── Header_Attrs     (TMP_Text, bold, "— ATTRIBUTES —")
                    ├── Label_STR        (TMP_Text)
                    ├── Label_DEX        (TMP_Text)
                    ├── Label_END        (TMP_Text)
                    ├── Label_INT        (TMP_Text)
                    └── Label_DEF        (TMP_Text)
```

Each `Section_*` is a child `VerticalLayoutGroup` GO with `spacing=6`.

Wire all 10 `TMP_Text` label refs on the `CharacterStatsUI` component, plus all 6 event SO assets (from `Assets/_Game/Data/Events/`) and all 6 system MonoBehaviour refs (drag from Player prefab instance in scene).

---

### Acceptance Criteria

**AC1 — Panel shows correct data on open (happy path)**
- Given: Player is Level 2, has 150 XP (threshold for level 2→3 is 250), 3 LP, 70/100 HP, 120/150 STA, STR 7, DEX 5, END 6, Mana 5, Defense 2
- When: Player presses C (CharacterStatsToggle key)
- Then:
  - "Level: 2" is displayed
  - "XP: 150 / 250" is displayed
  - "Learning Points: 3" is displayed
  - "HP: 70 / 100" is displayed
  - "STA: 120 / 150" is displayed
  - "STR: 7", "DEX: 5", "END: 6", "INT: 5", "DEF: 2" are displayed
  - Cursor is unlocked (verified via `CursorManager.IsLocked == false`)

**AC2 — HP label updates live while panel is open**
- Given: CharacterStats panel is open, player has 70/100 HP
- When: Enemy deals 20 damage (OnPlayerHealthChanged fires with 50f)
- Then: HP label updates to "HP: 50 / 100" without closing/reopening the panel

**AC3 — Level and XP update live on level-up**
- Given: CharacterStats panel is open, player is Level 1 with 90 XP
- When: Player kills an enemy, gains 10 XP, crosses the level-1 threshold (OnLevelUp fires with 2, OnXPGained fires)
- Then: Label shows "Level: 2" and XP label updates to show the level-2 threshold

**AC4 — Max level XP display**
- Given: Player is at MaxLevel (6 with default xpPerLevel config of length 5)
- When: CharacterStats panel opens
- Then: XP label shows "XP: {currentXP} / MAX"

**AC5 — Stats update live while panel is open**
- Given: CharacterStats panel is open, STR is 5
- When: TrainerNPC upgrades STR by 1 (OnStatsChanged fires)
- Then: STR label updates to "STR: 6" immediately

**AC6 — Reopen shows current values**
- Given: Panel was open, player took 30 damage (HP: 70→40), panel was closed
- When: Player reopens the panel
- Then: HP label shows "HP: 40 / 100" on the first frame it appears (populated by Refresh() in OnEnable)

**AC7 — Missing ref disables component gracefully**
- Given: Any required SerializeField ref is null in the Inspector
- When: The scene starts
- Then: A `GameLog.Error` is emitted and `CharacterStatsUI` disables itself; no NullReferenceException in Update or event handlers

---

## Additional Context

### Dependencies

All event SO assets already exist in `Assets/_Game/Data/Events/`:
- `OnLevelUp.asset` (GameEventSO_Int)
- `OnLPChanged.asset` (GameEventSO_Int)
- `OnXPGained.asset` (GameEventSO_Int)
- `OnPlayerHealthChanged.asset` (GameEventSO_Float)
- `OnPlayerStaminaChanged.asset` (GameEventSO_Float)
- `OnStatsChanged.asset` (GameEventSO_Void)

All backing MonoBehaviours live on the Player prefab root. `CharacterStatsUI` sits in `UICanvas.prefab` — refs must be wired in the scene (drag Player GO component refs onto UICanvas GO in the Inspector), not in a prefab.

### Testing Strategy

Manual playtest only — no automated tests for this story.

1. Open the scene, press C → verify all labels populate correctly
2. Take damage while panel is open → verify HP label updates
3. Kill an enemy while panel is open → verify XP label updates; if level-up occurs, verify Level label updates
4. Close and reopen panel after taking damage → verify HP reflects post-damage value
5. Reach MaxLevel → verify XP shows "MAX"

### Notes

- Defense is purely equipment-derived (`PlayerStats.Defense => _equipDefBonus`); it will read 0 until equipment is worn, which is expected behavior.
- The `OnDisable` null guard must check `_onLevelUp == null` (the first event SO subscribed) as the sentinel — if `Awake` disabled the component, `_onLevelUp` will be non-null if it was assigned in the Inspector even though `OnEnable` never ran. A safer alternative: guard each `Remove` call individually with a null check (`_onLevelUp?.RemoveListener(...)`). This is the preferred approach for multiple event SOs, as each may independently be null.
