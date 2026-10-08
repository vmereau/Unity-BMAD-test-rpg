# CLAUDE.md — Assets/_Game/Prefabs/UI

> Loaded when Claude accesses UI prefabs. Covers the `UICanvas` hierarchy and nesting rules.
> UI scripts: `Scripts/UI/CLAUDE.md` and its sub-folders.

---

## UICanvas.prefab

Nested inside `Player.prefab`. Edit `UICanvas.prefab` directly for layout; Player-specific overrides live
on `Player.prefab`.

```
UICanvas                 (Canvas + GraphicRaycaster + UIScreenManager — NO CanvasScaler)
├── EventSystem          (EventSystem + InputSystemUIInputModule, plain Transform)
├── ContainerUI / NPCTradeUI   (nested prefabs, inactive by default)
├── Menus                (tab-based screen panels)
│   ├── TabBar           (TabBarUI)
│   └── InventoryUI / QuestLogUI / CharacterStatsUI / SkillsUI   (inactive by default)
├── Game                 (HUD — drawn AFTER Menus, i.e. on top)
│   ├── Crosshair        (Image)
│   ├── SpeechBubbleLayer   (stretch, own Canvas, SpeechBubbleUI — pools Prefabs/UI/SpeechBubble.prefab; before the prompt)
│   ├── InteractionPrompt   (nested Prefabs/UI/InteractionPrompt.prefab — own Canvas + CanvasGroup)
│   ├── ActionBar        (ActionBarUI → 6× ActionBarSlot)
│   └── HealthBar / StaminaBar / ExperienceBar / NotificationContainer (own Canvas, sort 50)
├── DialoguePanel        (nested Prefabs/UI/Dialogue/DialoguePanel.prefab)
├── GameMenu             (GameMenuUI, always active) → Panel (toggled) → Window → MainButtons / SaveSlotList / OptionsUI / BackButton
├── DeathScreen          (DeathScreenUI, always active) → Panel (toggled)
├── ConfirmDialog        (ConfirmDialogUI, inactive — shared Yes/No)
└── LoadingOverlay       (LoadingOverlayUI, own Canvas sort 100) → Panel (toggled)
```

---

## Rules

- **No `CanvasScaler`** (removed in story 6-1): HUD elements use fixed pixel sizes.
- **`EventSystem` is a child of `UICanvas`**, not a second root — a prefab asset must have a single root,
  and a second root breaks Prefab Mode. It lives here so it always ships with the Player.
- `DialoguePanel` is a nested `PrefabInstance`; `DialogueUI._dialogueSystem` ↔ `DialogueSystem._dialogueUI`
  are cross-wired via **Player.prefab** overrides — don't try to wire them inside `UICanvas.prefab` alone.
- Screens that must hear events while hidden (`GameMenu`, `DeathScreen`, `LoadingOverlay`) keep their component on an
  always-active root and toggle a `Panel` child. `DeathScreenUI._playerStateManager` is wired as a Player.prefab override.
  Save-slot rows are `Prefabs/UI/SaveSlotEntry.prefab` (`SaveSlotEntryUI`); menu buttons are `Common/ActionButton` instances.
- `ItemDetailPanel` nesting rules (host prefabs override only the root, `ActionsContainer` never recreated):
  `Scripts/UI/Inventory/CLAUDE.md`.
