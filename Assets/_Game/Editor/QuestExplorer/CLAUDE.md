# CLAUDE.md — Assets/_Game/Editor/QuestExplorer

> Quest Explorer editor tool. Assembly **`Game.Editor`** (editor-only), namespace
> `Game.Editor.QuestExplorer`. Spec: `_bmad-output/implementation-artifacts/tech-spec-quest-explorer-editor-window.md`.

---

## Menus

- `Tools/Quests/Quest Explorer` — the window (Quests | Facts modes)
- `Assets/Show in Quest Explorer` — on a `QuestSO` or any `Fact` asset
- Inspector header context: `CONTEXT/QuestSO/Open in Quest Explorer`, `CONTEXT/Fact/Show in Quest Explorer`,
  `CONTEXT/PersistentID/Show KilledFact in Quest Explorer`

---

## Architecture (each layer usable alone)

```
QuestIndexCollector.Collect()   editor I/O: AssetDatabase, loaded scenes, prefabs, closed-scene text scan,
        │                        QuestEventsManager / QuestLogUI prefab lists (SerializedObject reads)
        ▼
IndexSources                    plain arrays/lists — tests fill it by hand
        ▼
QuestReferenceIndex.Build()     pure: Fact → setters / readers, dialogue node → (NPC, memory),
        │                        NPC → scene objects, quest → QuestFacts targeting it. UI-agnostic
        ▼                        (reuse it for a future graph view)
QuestValidator.Validate()       pure rules V1–V13 → ValidationIssue (sorted Error → Warning → Info)
        ▼
QuestExplorerWindow / QuestDetailView (UI Toolkit, inline styles) · QuestSceneHighlighter (Scene GUI)
QuestEditActions (Undo-aware SerializedObject edits) · QuestStepRemap (pure) · QuestExplorerNamePrompt
```

- Index rebuild: window open, **Refresh**, and debounced (one `delayCall`) on `projectChanged`,
  `hierarchyChanged` (ignored while playing), scene opened/closed, play-mode enter/exit, undo/redo.
- Closed scenes (`Assets/_Game/Scenes/` not loaded) are scanned as text for KilledFact asset GUIDs →
  `ClosedScene` setters ("Open scene & select" opens additively after the save prompt).

---

## Add a New Fact Type / Setter Source

1. Add a `FactLinkSource` value (`QuestIndexTypes.cs`) and a tag in `LinkRowFactory.SourceTag`.
2. Collect the raw data in `QuestIndexCollector` (into `IndexSources`, add a field if needed).
3. Map it to `FactLink`s in `QuestReferenceIndex.Build`.
4. If the type is stored and can be orphaned, include it in `QuestValidator.IsStored` (V3) and add a rule if needed.
5. Add a case to `ExplorerStyles.FactTypeName` and a test in `Assets/Tests/EditMode/QuestReferenceIndexTests.cs`.

---

## Rules / Gotchas

- `QuestPart` / `QuestStep` are **structs** — edit via `SerializedObject` only (`QuestEditActions`).
- Removing / moving a step remaps step-state `QuestFact._questState` in the same Undo group. The
  QuestFacts are written **before** the quest because `QuestFact.OnValidate` clamps `_questState`
  against the quest's current step count (undo replays in reverse, so the order stays safe).
- Play-mode **Toggle** = raw `WorldStateManager.SetFact` (fires `OnFactChanged`, no XP / rewards, no
  despawn). Only stored facts (Killed / Dialogue / World) get a toggle.
- Asset creation (KilledFact / DialogueFact / WorldFact / QuestFact) is not undoable — only the
  assignment is. Created files must be deleted by hand.
- In the editor, `GetComponent<T>()` returns a *fake null* when missing — `??` does not catch it; use
  `TryGetComponent` (see `AssignKilledFactFromSelection`).
- Scene links become fake-null after scene unload / play-mode transitions; link rows show "object no
  longer loaded" instead of acting on them.
