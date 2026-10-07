# CLAUDE.md — Assets/_Game/Scripts/UI/Skills

> Skills tab of the menu (`ScreenTab.Skills`, hotkey `K`). Read-only: lists every skill and shows its details.
> Prefabs: `Prefabs/UI/Skills/` (`SkillsUI`, `SkillSlot`). Skill data: `Data/Skills/CLAUDE.md`.

---

## Scripts

| Script | Purpose |
|--------|---------|
| `SkillsUI` | `IScreenPanel` on the tab root. Builds one `SkillSlotUI` per non-null `SkillCatalogSO` entry (once — the catalog is static), tracks the selected skill, repaints on `OnSkillLearned` / `OnStatsChanged`. |
| `SkillSlotUI` | One list row: placeholder icon + name. `Bind(skill, onClicked)`, `SetLearned`, `SetSelected`; hover tint via pointer handlers. |
| `SkillDetailPanelUI` | Display-only detail: header (icon, name, status), description, effect, requirement rows. Paints from the formatter. |
| `SkillDetailFormatter` | Pure static display logic — EditMode-tested in `SkillDetailFormatterTests`. |

## Data Flow

```
SkillCatalogSO ─► SkillsUI ─► SkillSlotUI × N
                     │
                     └─► SkillDetailPanelUI.Show(skill, learned, getStat, hasSkill)
                              └─ SkillDetailFormatter.BuildDetailLines → pooled ItemStatRowUI rows
```

- Player state reaches the formatter as delegates (`PlayerStats.GetStat`, `PlayerSkills.HasSkill`), cached once in
  `SkillsUI.Awake` — keeps the formatter testable without MonoBehaviours.
- `_playerSkills` / `_playerStats` are wired as **`Player.prefab` overrides** (like `CharacterStatsUI`); the
  `SkillsUI.prefab` / `UICanvas.prefab` alone have them empty.
- Requirement rows reuse `ItemStatRowUI` / `ItemStatLine` / `StatPolarity` and `Prefabs/UI/Items/ItemStatRow.prefab`.

## Rules

- **Unlearned = grayed, still selectable** — never hidden.
- Requirement colors: unlearned → met green / unmet red; **learned → all Neutral** (requirements are history; no red
  after a later stat drop). Null delegate → Neutral.
- Description / effect sections hide when the text is blank.
- Selection persists across closing / reopening the tab (first slot only when nothing valid is selected).
- **Icons are placeholders** (`sprite = null`, gray tint). When `SkillSO` gets an icon field, assign it in
  `SkillSlotUI.SetLearned` / `Bind` and `SkillDetailPanelUI.PaintHeader` (`_icon`).
- A skill missing from `Data/Skills/SkillCatalog.asset` does not appear in the tab; catalog entries with a blank or
  duplicate `skillId` are skipped with a warning.
- `SkillSlotUI.OnDisable` clears hover — uGUI sends no pointer-exit when a row is deactivated under the cursor.
