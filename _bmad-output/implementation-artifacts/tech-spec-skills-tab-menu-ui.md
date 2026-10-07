---
title: 'Skills Tab in Menu UI'
slug: 'skills-tab-menu-ui'
created: '2026-10-07'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (Game asmdef)', 'uGUI + TextMeshPro', 'Unity Input System (generated InputSystem_Actions)', 'NUnit EditMode tests']
files_to_modify: ['Assets/_Game/ScriptableObjects/Skills/SkillSO.cs', 'Assets/_Game/ScriptableObjects/Skills/SkillCatalogSO.cs (new)', 'Assets/_Game/Data/Skills/SkillCatalog.asset (new)', 'Assets/_Game/Data/Skills/Skill_PowerStrike.asset', 'Assets/_Game/Data/Skills/Lockpicking/Skill_Begginer_Lockpicking.asset', 'Assets/_Game/Data/Skills/Lockpicking/Skill_Expert_Lockpicking.asset', 'Assets/_Game/InputSystem_Actions.inputactions', 'Assets/_Game/InputSystem_Actions.cs', 'Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs', 'Assets/_Game/Scripts/UI/Skills/SkillsUI.cs (new)', 'Assets/_Game/Scripts/UI/Skills/SkillSlotUI.cs (new)', 'Assets/_Game/Scripts/UI/Skills/SkillDetailPanelUI.cs (new)', 'Assets/_Game/Scripts/UI/Skills/SkillDetailFormatter.cs (new)', 'Assets/_Game/Prefabs/UI/Skills/SkillsUI.prefab (new)', 'Assets/_Game/Prefabs/UI/Skills/SkillSlot.prefab (new)', 'Assets/_Game/Prefabs/UI/TabBar.prefab', 'Assets/_Game/Prefabs/UI/UICanvas.prefab', 'Assets/_Game/Prefabs/Player/Player.prefab', 'Assets/Tests/EditMode/SkillDetailFormatterTests.cs (new)', 'docs: Scripts/UI/CLAUDE.md, Scripts/UI/Screens/CLAUDE.md, Scripts/UI/Skills/CLAUDE.md (new), Data/Skills/CLAUDE.md, Prefabs/UI/CLAUDE.md, Assets/_Game/CLAUDE.md, root CLAUDE.md index']
code_patterns: ['IScreenPanel tab indexed by ScreenTab enum', 'display-only panel + pure static formatter', 'pooled ItemStatRowUI rows with ItemStatLine/StatPolarity', 'list rows instantiated from prefab + Bind()', 'GameEventSO subscribe OnEnable / unsubscribe OnDisable', 'cross-prefab refs wired as Player.prefab overrides', 'InputSystem_Actions dual-file edit']
test_patterns: ['NUnit EditMode in Assets/Tests/EditMode (Tests.EditMode asmdef refs Game + Game.Editor)', 'ScriptableObject.CreateInstance + SerializedObject SetSerialized helper, DestroyImmediate in TearDown (ItemDetailFormatterTests)']
---

# Tech-Spec: Skills Tab in Menu UI

**Created:** 2026-10-07

## Overview

### Problem Statement

The in-game menu has Inventory, Quest Log, Character Stats and Options tabs, but no way for the player
to see which skills exist, which ones they have learned, what each skill does, or what it costs to learn.
Skills (Power Strike, Beginner Lockpicking, Expert Lockpicking) are only visible indirectly through
gameplay (extra damage, opening locks) and NPC teach dialogue.

### Solution

Add a fifth menu tab, **Skills** (tab-bar button + `K` hotkey), that lists every skill from a new
`SkillCatalogSO` asset, each with a placeholder icon, unlearned skills grayed out. Selecting a skill shows
a side detail panel (pattern of `ItemDetailPanelUI` / `ItemDetailFormatter`) with: name, description,
effect (new authored `_effectDescription` field on `SkillSO`), LP cost, stat requirements (colored
met / unmet), prerequisite skills, and a learned / not learned status line. Display logic lives in a pure,
EditMode-tested `SkillDetailFormatter`.

### Scope

**In Scope:**
- `ScreenTab.Skills` enum value inserted before Options (Options becomes 4); `_tabPanelRoots` / `_tabButtons` updated
- New `SkillsToggle` input action bound to `<Keyboard>/k` (both `.inputactions` and generated `.cs`)
- `SkillCatalogSO` + `SkillCatalog` asset listing the 3 existing skills in display order
- `SkillSO._effectDescription` field; authored effect text for all 3 skills; descriptions for both lockpicking skills
- `SkillsUI` (`IScreenPanel`), `SkillSlotUI` (list row with placeholder icon, selected + grayed states),
  `SkillDetailPanelUI` (display-only), `SkillDetailFormatter` (pure static)
- Read-only access to learned skills from `PlayerSkills` (existing `HasSkill`) and player stats for requirement coloring
- UI prefab + tab button + scene wiring; EditMode tests for the formatter; folder CLAUDE.md updates

**Out of Scope:**
- Learning skills from this screen (panel is read-only)
- Real skill icons (placeholder only; `SkillSO` icon field may be added later)
- Structured skill-effect data / moving Power Strike bonus out of `ProgressionConfig`
- Renaming `Skill_Begginer_Lockpicking.asset` (typo) — noted only
- Save/load persistence of learned skills

## Context for Development

### Codebase Patterns

**Screen tabs (`Scripts/UI/Screens/UIScreenManager.cs`, namespace `Game.UI`)**
- `public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Options = 3 }` indexes
  `[SerializeField] GameObject[] _tabPanelRoots` and `Button[] _tabButtons`. `OpenTab` does
  `_tabPanelRoots[idx].SetActive(true)` then `GetComponent<IScreenPanel>()?.OnScreenOpen()`; `CloseTabContent`
  calls `OnScreenClose()` then `SetActive(false)`. `UIScreenManager` itself calls `CursorManager.Unlock/Lock`.
- Each hotkey: `_input.Player.<X>Toggle.performed += Handle<X>Toggle` in `OnEnable`, `-=` in `OnDisable`
  (after `if (_input == null) return;`). Handler: `if (_activeTab == ScreenTab.X) CloseAll(); else OpenTab(ScreenTab.X);`
- Serialized arrays live on `UICanvas.prefab` (`UIScreenManager`, ~lines 469-478): 4 panel roots + 4 buttons.
  Tab buttons live in `Prefabs/UI/TabBar.prefab` (`TabButton_Inventory`, `_QuestLog`, `_CharacterStats`,
  `_Options`, labels "Inventory" / "Quests" / "Stats" / "Options").
- Panels are separate prefabs nested under `UICanvas/Menus` (`Prefabs/UI/Stats/CharacterStatsUI.prefab`,
  `QuestLog/QuestLogUI.prefab`, `Inventory/InventoryUI.prefab`), inactive by default.
- Panels needing player MonoBehaviours (e.g. `CharacterStatsUI._levelSystem`, `_playerStats`) get them through
  **`Player.prefab` property overrides** on the nested UICanvas (UICanvas alone can't reference Player components).

**Panel precedent (`CharacterStatsUI`)** — `Awake` null-checks required refs → `GameLog.Error` + `enabled = false`;
optional event SOs → `GameLog.Warn`. Subscribes `GameEventSO` channels in `OnEnable`, unsubscribes in `OnDisable`,
calls `Refresh()` in `OnEnable`. `OnScreenOpen/Close` only log. TAG `"[UI]"`.

**List + detail precedent**
- `QuestListPanelUI` / `QuestButtonUI`: rows instantiated from prefab into `_contentRoot`, `Bind(data)`, click →
  parent `Select...`; old rows destroyed on refresh. Hover via `IPointerEnterHandler/ExitHandler` background tint.
- `ItemSlotUI.SetSelected(bool)` tints `_backgroundImage` between `_normalColor` / `_selectedColor`;
  `InventoryUI` tracks `_selectedSlotUI`, deselects the old one before selecting the new.
- `ItemDetailPanelUI` (display-only, paints from `ItemDetailFormatter`): header (Image `_icon` — gray tint when
  sprite null, `_nameText`, `_categoryText`) → `_statsSection` with pooled `ItemStatRowUI` rows under
  `_statRowsRoot` (instantiate only when more needed, extras `SetActive(false)`) → `_descriptionSection`
  (hidden when empty). Colors serialized: positive `(0.49,0.80,0.42)`, negative `(0.88,0.42,0.42)`,
  neutral `(0.88,0.88,0.88)`. `ColorFor(StatPolarity)`.
- `ItemStatRowUI.Set(label, value, color)` and `readonly struct ItemStatLine(Label, Value, Polarity)` +
  `enum StatPolarity { Neutral, Positive, Negative }` are generic and **reused** for skill rows
  (`Prefabs/UI/Items/ItemStatRow.prefab`).
- Pure formatter: `public static class ItemDetailFormatter` with `const string LABEL_*`,
  `BuildStatLines(x, List<ItemStatLine> into)` that clears then fills, `GameLog.Warn` on null target list.

**Skill data / runtime**
- `SkillSO` (`ScriptableObjects/Skills/SkillSO.cs`, `Game.Progression`, menu `Game/Skills/Skill`): `_skillId`,
  `_displayName`, `[TextArea] _description`, `[Min(1)] _lpCost`, `List<StatRequirement> _statsRequirements`,
  `List<SkillSO> _skillRequirements`; lowercase read-only properties (`skillId`, `displayName`, ...,
  `IReadOnlyList<>` for lists).
- `StatRequirement` (`Game.Core`, in `ScriptableObjects/Facts/StatFact.cs`): `{ StatType statType; int value; }`.
  `StatType` (`Game.Player`, `Scripts/Player/PlayerStats.cs`): `Strength, Dexterity, Endurance, Intelligence, Defense`.
  `PlayerStats.GetStat(StatType)` returns the effective value (base + equipment); `ValidateStats` uses `>=`.
- `PlayerSkills` (`Game.Progression`, on Player): `HasSkill(string id)`; raises `GameEventSO_String _onSkillLearned`
  (`Data/Events/OnSkillLearned.asset`) on learn. `CharacterStatsUI` uses `GameEventSO_Void _onStatsChanged`
  for stat changes (reuse the same asset).
- Existing skills: `Skill_PowerStrike` (id `power_strike`, 2 LP, STR 8 + END 5, description set),
  `Lockpicking/Skill_Begginer_Lockpicking` (id `beginner_lockpicking`, 3 LP, DEX 5, empty description),
  `Lockpicking/Skill_Expert_Lockpicking` (id `expert_lockpicking`, 5 LP, DEX 10, requires Beginner, empty description).
- Effects: Power Strike = `ProgressionConfig.powerStrikeDamageBonus` (= 10) added in
  `PlayerCombat.ComputeEffectiveDamage` via `HasSkill("power_strike")`. Lockpicking = `Lockable._requiredSkill`
  checked by `DoorSystem` / `ContainerSystem` (`HasSkill(requiredSkillId)` → unlock).

**Input** — `InputSystem_Actions.cs` embeds the full JSON (`""` escaped) **and** declares typed fields/properties
per action; both it and `.inputactions` must be edited. Toggle precedent: `CharacterStatsToggle`
(id `cc01...`, binding `<Keyboard>/c`), `QuestLogToggle` (ids `ql01...` / `ql02...`, `<Keyboard>/j`).
`<Keyboard>/k` is currently unbound.

**Project rules that apply** (`_bmad-output/project-context.md`, folder CLAUDE.md files)
- UI namespace `Game.UI`; `TMP_Text` only; no `Cursor.*` (the panel relies on `UIScreenManager`'s cursor calls);
  events subscribed in `OnEnable` / unsubscribed in `OnDisable`, no polling in `Update`.
- `CharacterStatsUI` precedent accepts direct MonoBehaviour refs to player systems from a UI panel (prototype
  shortcut) — `SkillsUI` follows it for `PlayerSkills` / `PlayerStats` (read-only queries).
- New `.meta` files must be generated by Unity (refresh), not hand-written without `MonoImporter`.
- After raw YAML prefab edits use `refresh_unity(mode="if_dirty")`, never `force`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs` | Tab enum, open/close flow, hotkey handlers to extend |
| `Assets/_Game/Scripts/UI/Screens/CharacterStatsUI.cs` | Panel structure: Awake validation, event subscriptions, refresh |
| `Assets/_Game/Scripts/UI/Screens/IScreenPanel.cs` | Panel contract |
| `Assets/_Game/Scripts/UI/Inventory/ItemDetailPanelUI.cs` | Detail-panel layout, row pooling, colors |
| `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs` | Pure formatter style (consts, BuildStatLines) |
| `Assets/_Game/Scripts/UI/Inventory/ItemStatRowUI.cs`, `ItemStatLine.cs` | Reused row component + line struct |
| `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs` | `SetSelected` tint pattern |
| `Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs`, `QuestButtonUI.cs` | Instantiate + Bind list rows, hover tint |
| `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` | Skill data (add effect field) |
| `Assets/_Game/ScriptableObjects/Facts/StatFact.cs` | `StatRequirement` struct |
| `Assets/_Game/Scripts/Player/PlayerStats.cs` | `StatType`, `GetStat` |
| `Assets/_Game/Scripts/Player/Progression/PlayerSkills.cs` | `HasSkill`, `_onSkillLearned` |
| `Assets/_Game/InputSystem_Actions.inputactions` / `.cs` | Add `SkillsToggle` |
| `Assets/_Game/Prefabs/UI/TabBar.prefab`, `UICanvas.prefab`, `Stats/CharacterStatsUI.prefab` | Tab button + panel hosting |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Override wiring of player refs into UICanvas panels |
| `Assets/_Game/Prefabs/UI/Items/ItemStatRow.prefab` | Row prefab reused by the skill detail panel |
| `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` | Test helper pattern (`SetSerialized`, `CreateSkill`) |

### Technical Decisions

- Effect text is an authored string field (`_effectDescription`) — chosen over structured modifiers to keep
  scope small; accepted risk that Power Strike text can drift from `ProgressionConfig`.
- Master skill list is a dedicated `SkillCatalogSO` asset (`ScriptableObjects/Skills/SkillCatalogSO.cs`,
  asset `Data/Skills/SkillCatalog.asset`) so future skills are added in one data place; list order = display order.
- Detail panel shows name, status (Learned / Not learned), description, effect, LP cost, stat requirements
  (green met / red unmet vs `PlayerStats.GetStat`) and prerequisite skills (green learned / red not learned).
- New UI sub-folder `Scripts/UI/Skills/` (+ `Prefabs/UI/Skills/`) mirroring `Quest/`, with its own CLAUDE.md.
- `SkillDetailFormatter` takes `Func<StatType,int>` / `Func<string,bool>` delegates instead of `PlayerStats` /
  `PlayerSkills`, so it is pure and EditMode-testable without MonoBehaviours.
- Reuse `ItemStatRowUI` / `ItemStatLine` / `StatPolarity` / `ItemStatRow.prefab` for requirement rows — no new
  row component.
- `ScreenTab.Skills = 3` inserted before Options (Options → 4) so the tab bar reads
  Inventory · Quests · Stats · Skills · Options. Both serialized arrays must be reordered in the same change.
- Hotkey `K` via new `SkillsToggle` action — dual-file contract.
- Unlearned rows: still selectable, rendered grayed (text + icon tint), not hidden.
- Live updates while open: subscribe `OnSkillLearned` (String) and `OnStatsChanged` (Void) → refresh list +
  selected detail. LP cost is static per skill, so `OnLPChanged` is not needed.

## Implementation Plan

### Tasks

**Data layer**

- [x] Task 1: Add the authored effect field to `SkillSO`
  - File: `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs`
  - Action: Add `[TextArea] [SerializeField] private string _effectDescription;` directly after `_description`,
    and `public string effectDescription => _effectDescription;` after `description`.
  - Notes: Purely additive — existing assets keep their values (field defaults to empty). Update the class
    `<summary>` to mention it is shown in the Skills tab.

- [x] Task 2: Create `SkillCatalogSO`
  - File: `Assets/_Game/ScriptableObjects/Skills/SkillCatalogSO.cs` (new; let Unity generate the `.meta`)
  - Action:
    ```csharp
    namespace Game.Progression
    {
        /// <summary>Ordered list of every skill in the game. Order = display order in the Skills tab.</summary>
        [CreateAssetMenu(menuName = "Game/Skills/Skill Catalog", fileName = "SkillCatalog")]
        public class SkillCatalogSO : ScriptableObject
        {
            [SerializeField] private List<SkillSO> _skills = new List<SkillSO>();
            public IReadOnlyList<SkillSO> skills => _skills;
        }
    }
    ```
  - Notes: Consumers must skip `null` entries (unassigned list slots).

- [x] Task 3: Author skill text and create the catalog asset
  - Files: `Assets/_Game/Data/Skills/Skill_PowerStrike.asset`,
    `Assets/_Game/Data/Skills/Lockpicking/Skill_Begginer_Lockpicking.asset`,
    `Assets/_Game/Data/Skills/Lockpicking/Skill_Expert_Lockpicking.asset`,
    `Assets/_Game/Data/Skills/SkillCatalog.asset` (new)
  - Action (via MCP `manage_scriptable_object` or Inspector, after Task 1-2 compile):
    - Power Strike — `_description`: "A powerful melee technique that puts your full weight behind every swing."
      `_effectDescription`: "+10 damage on all melee attacks."
    - Beginner Lockpicking — `_description`: "The basics of working a lock with picks and patience."
      `_effectDescription`: "Lets you open doors and chests secured with a beginner lock."
    - Expert Lockpicking — `_description`: "Mastery of complex mechanisms that would stump most thieves."
      `_effectDescription`: "Lets you open doors and chests secured with an expert lock."
    - Create `SkillCatalog.asset` with `_skills` = [Skill_PowerStrike, Skill_Begginer_Lockpicking, Skill_Expert_Lockpicking].
  - Notes: Power Strike's old description ("…Increases melee damage output by 10.") moves into the effect field so
    numbers live in one place on screen. The "+10" must match `ProgressionConfig.powerStrikeDamageBonus` (10) — see Notes.

**Input + tab plumbing**

- [x] Task 4: Add the `SkillsToggle` input action (dual-file contract)
  - Files: `Assets/_Game/InputSystem_Actions.inputactions`, `Assets/_Game/InputSystem_Actions.cs`
  - Action, `.inputactions` (Player map):
    - Action after `QuestLogToggle`: `"name": "SkillsToggle", "type": "Button", "id": "5c111c01-5c11-4c11-8c11-5c111c015c11", "expectedControlType": "Button", "processors": "", "interactions": "", "initialStateCheck": false`
    - Binding after the `QuestLogToggle` binding: `"name": "", "id": "5c111c02-5c11-4c11-8c11-5c111c025c11", "path": "<Keyboard>/k", "interactions": "", "processors": "", "groups": "Keyboard&Mouse", "action": "SkillsToggle", "isComposite": false, "isPartOfComposite": false`
  - Action, `.cs` (5 spots, mirror every `QuestLogToggle` occurrence):
    1. Embedded JSON action entry (`""`-escaped) after `QuestLogToggle` (~line 285)
    2. Embedded JSON binding entry after the `QuestLogToggle` binding (~line 797)
    3. Constructor: `m_Player_SkillsToggle = m_Player.FindAction("SkillsToggle", throwIfNotFound: true);`
    4. Field: `private readonly InputAction m_Player_SkillsToggle;`
    5. `PlayerActions` property: `/// <summary>Provides access to the underlying input action "Player/SkillsToggle".</summary>` + `public InputAction @SkillsToggle => m_Wrapper.m_Player_SkillsToggle;`
  - Notes: Commas between JSON entries must stay valid in both files. No `IPlayerActions` implementer exists, and the
    existing toggles have no `AddCallbacks` entries — don't add any.

- [x] Task 5: Add `ScreenTab.Skills` and the hotkey handler
  - File: `Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs`
  - Action:
    - `public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Skills = 3, Options = 4 }`
    - `OnEnable`: `_input.Player.SkillsToggle.performed += HandleSkillsToggle;` — `OnDisable`: matching `-=`.
    - `private void HandleSkillsToggle(InputAction.CallbackContext ctx)` — same body as `HandleQuestLogToggle` with
      `ScreenTab.Skills`.
  - Notes: No other code references `ScreenTab` (verified). Arrays are re-wired in Task 11.

**UI scripts (new folder `Assets/_Game/Scripts/UI/Skills/`, namespace `Game.UI`)**

- [x] Task 6: Create `SkillDetailFormatter` (pure static display logic)
  - File: `Assets/_Game/Scripts/UI/Skills/SkillDetailFormatter.cs` (new)
  - Action: `public static class SkillDetailFormatter` with `private const string TAG = "[SkillDetailFormatter]"` and consts
    `STATUS_LEARNED = "Learned"`, `STATUS_NOT_LEARNED = "Not learned"`, `LABEL_LP_COST = "LP Cost"`,
    `LABEL_REQUIRES = "Requires"`, `LABEL_STRENGTH/DEXTERITY/ENDURANCE/INTELLIGENCE/DEFENSE` = "Strength" / "Dexterity" /
    "Endurance" / "Intelligence" / "Defense". Public API:
    - `string GetStatus(bool learned)` → `STATUS_LEARNED` / `STATUS_NOT_LEARNED`.
    - `string GetDescription(SkillSO skill)` / `string GetEffect(SkillSO skill)` → trimmed text, or `null` when the skill is null or the field is null/whitespace.
    - `string GetStatLabel(StatType stat)` → label const via switch (default: `stat.ToString()`).
    - `void BuildDetailLines(SkillSO skill, bool learned, Func<StatType,int> getStat, Func<string,bool> hasSkill, List<ItemStatLine> into)`:
      - `into == null` → `GameLog.Warn` + return; otherwise `into.Clear()`; `skill == null` → return.
      - Row 1: `LP Cost` / `skill.lpCost.ToString()` / Neutral.
      - Then one row per `statsRequirements` entry in authored order: label `GetStatLabel(req.statType)`, value
        `req.value.ToString()`, polarity: `learned` or `getStat == null` → Neutral; else `getStat(req.statType) >= req.value` → Positive, otherwise Negative.
      - Then one row per non-null `skillRequirements` entry: label `Requires`, value `prereq.displayName`, polarity:
        `learned` or `hasSkill == null` → Neutral; else `hasSkill(prereq.skillId)` → Positive, otherwise Negative.
  - Notes: Reuses `ItemStatLine` / `StatPolarity` from `Scripts/UI/Inventory/ItemStatLine.cs`. Once the skill is
    learned, requirements are history, so they render Neutral (no misleading red after a later stat drop).
    No allocations other than into the caller's list.

- [x] Task 7: Create `SkillDetailPanelUI` (display-only)
  - File: `Assets/_Game/Scripts/UI/Skills/SkillDetailPanelUI.cs` (new)
  - Action: MonoBehaviour modeled on `ItemDetailPanelUI`. Serialized:
    - Header: `Image _icon`, `TMP_Text _nameText`, `TMP_Text _statusText`
    - `GameObject _descriptionSection`, `TMP_Text _descriptionText`
    - `GameObject _effectSection`, `TMP_Text _effectText`
    - `GameObject _detailsSection`, `Transform _rowsRoot`, `ItemStatRowUI _rowPrefab`
    - Colors: `_positiveColor (0.49,0.80,0.42)`, `_negativeColor (0.88,0.42,0.42)`, `_neutralColor (0.88,0.88,0.88)`,
      `_learnedStatusColor` = positive, `_notLearnedStatusColor (0.6,0.6,0.6)`, `_iconPlaceholderColor (0.5,0.5,0.5)`
    - Public `void Show(SkillSO skill, bool learned, Func<StatType,int> getStat, Func<string,bool> hasSkill)`:
      null skill → `GameLog.Warn` + `Hide()`. Paint name; status text + color; icon = placeholder (`sprite = null`,
      `color = _iconPlaceholderColor`); description / effect sections hidden when the formatter returns null; rows built
      via `SkillDetailFormatter.BuildDetailLines` into a cached `List<ItemStatLine>`, pooled exactly like
      `ItemDetailPanelUI.PaintStats` (instantiate only when more are needed, extras `SetActive(false)`, one-time warn if
      `_rowPrefab`/`_rowsRoot` missing). Then `gameObject.SetActive(true)`.
    - Public `void Hide()` → `gameObject.SetActive(false)`.
  - Notes: TAG `"[SkillDetailPanelUI]"`. All text decisions come from the formatter; this class only paints.

- [x] Task 8: Create `SkillSlotUI` (one list row)
  - File: `Assets/_Game/Scripts/UI/Skills/SkillSlotUI.cs` (new)
  - Action: MonoBehaviour implementing `IPointerEnterHandler, IPointerExitHandler`. Serialized: `Button _button`,
    `Image _backgroundImage`, `Image _icon`, `TMP_Text _nameText`, colors `_normalColor (0.15,0.15,0.15,0.8)`,
    `_hoverColor (0.30,0.30,0.30,0.9)`, `_selectedColor (0.35,0.30,0.18,0.95)`, `_learnedTextColor (0.88,0.88,0.88)`,
    `_unlearnedTextColor (0.5,0.5,0.5)`, `_iconPlaceholderColor (0.5,0.5,0.5)`, `_unlearnedIconAlpha = 0.4f`.
    - `public SkillSO Skill { get; private set; }`
    - `public void Bind(SkillSO skill, Action<SkillSlotUI> onClicked)`: store skill + callback, set `_nameText.text = skill.displayName`,
      (re)register a cached `UnityAction` on `_button.onClick` (remove the previous one first, like `QuestButtonUI`).
    - `public void SetLearned(bool learned)`: name color learned/unlearned; icon = placeholder color with alpha 1 or `_unlearnedIconAlpha`.
    - `public void SetSelected(bool selected)`: store flag, repaint background (selected wins over hover).
    - Pointer enter/exit → hover flag → repaint background.
  - Notes: Unlearned rows stay clickable. Null-guard every serialized ref with a one-time `GameLog.Warn`.

- [x] Task 9: Create `SkillsUI` (the tab panel)
  - File: `Assets/_Game/Scripts/UI/Skills/SkillsUI.cs` (new)
  - Action: `public class SkillsUI : MonoBehaviour, IScreenPanel`, TAG `"[UI]"`. Serialized:
    `SkillCatalogSO _catalog`, `PlayerSkills _playerSkills`, `PlayerStats _playerStats`, `Transform _listRoot`,
    `SkillSlotUI _slotPrefab`, `SkillDetailPanelUI _detailPanel`, `GameEventSO_String _onSkillLearned`,
    `GameEventSO_Void _onStatsChanged`.
    - `Awake`: missing `_catalog`, `_playerSkills`, `_listRoot`, `_slotPrefab` or `_detailPanel` → `GameLog.Error` + `enabled = false` + return.
      Missing `_playerStats` → `GameLog.Warn` (stat rows render Neutral). Missing events → `GameLog.Warn` (no live updates).
      Cache delegates once: `_hasSkill = _playerSkills.HasSkill;` and `_getStat = _playerStats != null ? _playerStats.GetStat : null;`
      and `_onSlotClicked = HandleSlotClicked;`.
    - `OnEnable`: `_onSkillLearned?.AddListener(HandleSkillLearned)`, `_onStatsChanged?.AddListener(HandleStatsChanged)`, then `Refresh()`.
      `OnDisable`: matching `RemoveListener`s.
    - `Refresh()`: build slots once (first call): for each non-null skill in `_catalog.skills`, `Instantiate(_slotPrefab, _listRoot)` +
      `Bind(skill, _onSlotClicked)`, keep in `List<SkillSlotUI> _slots`. Then for every slot `SetLearned(_playerSkills.HasSkill(slot.Skill.skillId))`.
      Selection: keep `_selectedSkill` if a slot still holds it, else the first slot's skill; none → `_detailPanel.Hide()`.
      Apply `SetSelected` on all slots and repaint the detail panel.
    - `HandleSlotClicked(SkillSlotUI slot)` → `_selectedSkill = slot.Skill`, update slot selection, repaint detail.
    - `HandleSkillLearned(string _)` / `HandleStatsChanged(bool _)` → `Refresh()`.
    - `OnScreenOpen()` / `OnScreenClose()` → `GameLog.Info(TAG, "Skills opened/closed")` only (cursor is handled by `UIScreenManager`; `OnEnable` already refreshes).
  - Notes: `GameEventSO_Void` is `GameEventSO<bool>` — handler takes `bool`. Selection persists across reopenings
    within a session. Catalog is static at runtime, so slots are never rebuilt.

**Prefabs + wiring (Unity Editor / MCP)**

- [x] Task 10: Build the Skills prefabs
  - Files: `Assets/_Game/Prefabs/UI/Skills/SkillSlot.prefab`, `Assets/_Game/Prefabs/UI/Skills/SkillsUI.prefab` (new)
  - Action:
    - `SkillSlot.prefab`: root (RectTransform, height 48, `Image` background, `Button`, `HorizontalLayoutGroup` padding 6 / spacing 10,
      `LayoutElement` preferredHeight 48, `SkillSlotUI`) → `Icon` (Image 36×36, no sprite, placeholder gray) → `Name` (TMP_Text, 18 pt, left-middle).
    - `SkillsUI.prefab`: copy the frame/background of `Stats/CharacterStatsUI.prefab` (same root RectTransform size/anchors and `Panel_BG`
      look) so tabs match. Root has `SkillsUI`. Children:
      - `SkillList` (left ~40% width): `ScrollRect` → `Viewport` (Mask) → `Content` (`VerticalLayoutGroup` spacing 4, `ContentSizeFitter` vertical preferred) = `_listRoot`.
      - `SkillDetailPanel` (right ~60%, `SkillDetailPanelUI`, root `VerticalLayoutGroup` spacing 8 / padding 12):
        `Header` (HorizontalLayoutGroup: `Icon` Image 64×64 + `Texts` vertical: `Name` TMP 24 pt bold, `Status` TMP 16 pt) →
        `DescriptionSection` (`Description` TMP 16 pt, word wrap, italic) → `EffectSection` (`EffectHeader` TMP "Effect" 16 pt bold + `Effect` TMP 16 pt) →
        `DetailsSection` (`DetailsHeader` TMP "Requirements" 16 pt bold + `Rows` VerticalLayoutGroup = `_rowsRoot`).
      - Wire `_rowPrefab` = `Prefabs/UI/Items/ItemStatRow.prefab` (its `ItemStatRowUI`), `_slotPrefab` = `SkillSlot.prefab`,
        `_catalog` = `Data/Skills/SkillCatalog.asset`, `_onSkillLearned` = `Data/Events/OnSkillLearned.asset`,
        `_onStatsChanged` = `Data/Events/OnStatsChanged.asset`, `_detailPanel`, `_listRoot`.
  - Notes: Fixed pixel sizes (no `CanvasScaler` on `UICanvas`). TMP_Text only.

- [x] Task 11: Add the tab button and nest the panel
  - Files: `Assets/_Game/Prefabs/UI/TabBar.prefab`, `Assets/_Game/Prefabs/UI/UICanvas.prefab`
  - Action:
    - `TabBar.prefab`: duplicate `TabButton_CharacterStats` → `TabButton_Skills`, label "Skills", sibling order
      Inventory, QuestLog, CharacterStats, **Skills**, Options (adjust layout/spacing if the bar uses manual positions).
    - `UICanvas.prefab`: nest `SkillsUI.prefab` under `Menus` next to `CharacterStatsUI`, **inactive by default**.
    - `UIScreenManager._tabPanelRoots` = [InventoryUI, QuestLogUI, CharacterStatsUI, **SkillsUI**, OptionsUI];
      `_tabButtons` = [TabButton_Inventory, TabButton_QuestLog, TabButton_CharacterStats, **TabButton_Skills**, TabButton_Options].
  - Notes: Prefer MCP/Editor edits. If YAML is edited directly, use `refresh_unity(mode="if_dirty")`, never `force`.
    Both arrays must have length 5 and be in enum order.

- [x] Task 12: Wire player references
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action: On the nested UICanvas → `SkillsUI`, add overrides `_playerSkills` = Player's `PlayerSkills`,
    `_playerStats` = Player's `PlayerStats` (same pattern as `CharacterStatsUI._playerStats` overrides).
  - Notes: Verify in Play Mode that no "missing required reference" error is logged on first open of the tab.

**Tests + docs**

- [x] Task 13: EditMode tests for the formatter
  - File: `Assets/Tests/EditMode/SkillDetailFormatterTests.cs` (new)
  - Action: Copy the `Create<T>` / `SetSerialized` / `TearDown` helpers from `ItemDetailFormatterTests`. A `CreateSkill(id, name, lpCost,
    description, effect)` helper sets `_skillId`, `_displayName`, `_lpCost`, `_description`, `_effectDescription`; requirements are set
    through `SerializedProperty` arrays (`_statsRequirements` → `statType` enumValueIndex + `value`; `_skillRequirements` → objectReferenceValue).
    Tests cover ACs 7-12 below.

- [x] Task 14: Update documentation
  - Files:
    - `Assets/_Game/Scripts/UI/Skills/CLAUDE.md` (new): scripts table, data flow (`SkillsUI` → `SkillSlotUI` × N + `SkillDetailPanelUI` ← `SkillDetailFormatter`),
      "unlearned = grayed, still selectable", requirement rows Neutral once learned, icon placeholder (future `SkillSO` icon hooks into `SkillSlotUI` / `SkillDetailPanelUI` `_icon`).
    - `Assets/_Game/Scripts/UI/Screens/CLAUDE.md`: new enum, Skills row in purpose text, toggle list.
    - `Assets/_Game/Scripts/UI/CLAUDE.md`: add `Skills/` to the sub-folder index.
    - `Assets/_Game/Data/Skills/CLAUDE.md`: `_effectDescription` row; `SkillCatalogSO` section ("every new skill must be added to `SkillCatalog.asset` or it won't appear in the Skills tab"). Also fix the stated asset menu (code says `Game/Skills/Skill`, doc says `Game/Progression/Skill`).
    - `Assets/_Game/Prefabs/UI/CLAUDE.md`: add `SkillsUI` to the `Menus` hierarchy.
    - `Assets/_Game/CLAUDE.md`: add `SkillsToggle` (K) to the Player action-map list.
    - Root `CLAUDE.md`: add `Scripts/UI/Skills/` to the UI row of the folder index.

### Acceptance Criteria

**Tab + input**
- [ ] AC 1: Given the game is running with no menu open, when the player presses `K`, then the menu opens on the Skills tab, the cursor is unlocked and the player state is `InMenu`.
- [ ] AC 2: Given the Skills tab is open, when the player presses `K` again or `Escape`, then all menus close and the cursor is locked.
- [ ] AC 3: Given any menu tab is open, when the player clicks the "Skills" tab button, then the Skills panel replaces the current panel and the Skills button shows as active; the tab bar reads Inventory · Quests · Stats · Skills · Options.
- [ ] AC 4: Given the menu is open, when the player clicks Options (or presses I / J / C), then the correct panel opens — no tab is shifted or swapped by the enum change.

**List + detail**
- [ ] AC 5: Given a new game where no skills are learned, when the Skills tab opens, then Power Strike, Beginner Lockpicking and Expert Lockpicking are listed in that order, each with a gray placeholder icon, all grayed out, and the first skill is selected with its details shown.
- [ ] AC 6: Given the player has learned Beginner Lockpicking, when the Skills tab opens, then that row renders in normal (non-gray) colors, and selecting it shows status "Learned" in the learned color.
- [ ] AC 7: Given Power Strike is selected, then the detail panel shows name "Power Strike", status, its description, effect "+10 damage on all melee attacks.", row "LP Cost 2", rows "Strength 8" and "Endurance 5".
- [ ] AC 8: Given an unlearned skill with stat requirements and the player's stat is below one requirement and meets another, then the unmet row is red (Negative) and the met row is green (Positive).
- [ ] AC 9: Given Expert Lockpicking is selected and Beginner Lockpicking is not learned, then a row "Requires · Beginner Lockpicking" is shown in red; once Beginner is learned it is green.
- [ ] AC 10: Given a learned skill, then all its requirement and prerequisite rows are Neutral.
- [ ] AC 11: Given a skill whose description or effect is empty/whitespace, then that section is hidden (no empty header).
- [ ] AC 12: Given `BuildDetailLines` is called with a null target list, then it logs a warning and does not throw; with a null skill it clears the list; null entries in `skillRequirements` are skipped.

**Live update + robustness**
- [ ] AC 13: Given the Skills tab is open, when a skill is learned (`OnSkillLearned` raised) or stats change (`OnStatsChanged` raised), then the list gray state and the selected skill's detail update without reopening the tab.
- [ ] AC 14: Given the player selected Expert Lockpicking and closed the menu, when the Skills tab is reopened, then Expert Lockpicking is still selected.
- [ ] AC 15: Given `SkillsUI` is missing a required reference (e.g. `_catalog`), when the tab opens, then a single `GameLog.Error` is logged, the panel shows without throwing, and other tabs keep working.
- [ ] AC 16: Given `SkillCatalog.asset` contains a null entry, then it is skipped (no row, no exception).
- [ ] AC 17: Given the existing EditMode suite, when all tests run, then `ItemDetailFormatterTests`, `SkillLearningTests` and the new `SkillDetailFormatterTests` pass.

## Additional Context

### Dependencies

- No new packages. Depends on existing `PlayerSkills`, `PlayerStats`, `OnSkillLearned` / `OnStatsChanged`
  event assets, `ItemStatRow.prefab`, `UIScreenManager`, `TabBar.prefab`.
- Unity Editor + MCP needed for prefab creation, asset creation and wiring.
- Task order: 1-2 must compile before 3 (assets); 4-9 before 10-12 (prefabs need the components); 13 needs 1 and 6.

### Testing Strategy

- **EditMode (automated):** `SkillDetailFormatterTests` — status strings; description/effect null/whitespace → null; LP cost row first;
  stat rows in authored order with Positive/Negative/Neutral polarity (unlearned met/unmet, learned, `getStat == null`);
  prerequisite rows (learned/unlearned/null hasSkill/null entry skipped); null list warns; null skill clears. Run the full EditMode suite.
- **Manual (Play Mode):**
  1. Press K → Skills tab, 3 grayed skills, first selected. Click each and check the text from Task 3.
  2. Press K / Escape → closes. Click every tab button and press I / J / C → correct panels (AC 4).
  3. Learn Beginner Lockpicking (Innkeeper teach dialogue or a dev tool) with the Skills tab closed → reopen: row normal, status "Learned",
     Expert's prerequisite row now green.
  4. Equip / unequip a DEX item with Expert Lockpicking selected and the tab open → requirement color updates (AC 13).
  5. Console: no errors or warnings from `SkillsUI` / `SkillDetailPanelUI` / `SkillSlotUI`.

### Notes

- **Risk — duplicated number:** "+10 damage" is authored text; `ProgressionConfig.powerStrikeDamageBonus` stays the real value. If the config
  changes, the effect text must be updated by hand. Future fix: structured skill effects (out of scope).
- **Risk — tab index shift:** Options moves from 3 to 4. Mis-ordered or short `_tabPanelRoots` / `_tabButtons` arrays cause wrong panels or
  `IndexOutOfRangeException` (the open path checks `idx < _tabPanelRoots.Length`, but `UpdateTabButtonStates` / `WireTabButtons` don't).
- **Risk — input dual file:** forgetting the embedded JSON in `InputSystem_Actions.cs` makes `FindAction("SkillsToggle", throwIfNotFound: true)` throw at startup and breaks all input.
- **Limitation:** learned skills aren't saved yet (no save/load), so the list resets each session.
- **Known data issue:** `Skill_Begginer_Lockpicking.asset` filename typo — rename later along with its referencing assets.
- **Future:** `SkillSO` icon sprite → assign in `SkillSlotUI.Bind` / `SkillDetailPanelUI.Show`; a "Learn" button in the detail panel; skill
  categories / filters (like the Quest tabs) when the catalog grows.

## Review Notes

- Adversarial review completed (2026-10-07)
- Findings: 7 total, 3 fixed, 4 skipped
- Resolution approach: auto-fix (real findings only)
- Fixed:
  - F1 (High): the Power Strike tome tooltip lost "+10 damage" once it moved to `_effectDescription`.
    `ItemDetailFormatter.GetSkillDescription` now appends the effect text; 2 tests added to `ItemDetailFormatterTests`.
  - F3 (Medium): `SkillSlotUI` hover color could stick after the tab closed under the cursor; `OnDisable` now clears it.
  - F5 (Low): `SkillsUI` skips catalog entries with a blank or duplicate `skillId` and logs a warning.
- Skipped:
  - F2: `Options` 3 → 4 shift is the spec's choice; arrays verified and no other `ScreenTab` storage exists.
  - F4: `OnScreenOpen` still logs when the panel is disabled; same as `CharacterStatsUI`.
  - F6: direct `PlayerSkills` / `PlayerStats` refs are the accepted `CharacterStatsUI` prototype shortcut.
  - F7: no `SkillsUI` PlayMode test or `GetStatLabel` fallback test; runtime behavior was checked by hand in Play Mode.
