# CLAUDE.md — Assets/_Game/Editor/QuestExplorer

> Quest Explorer editor tool. Assembly **`Game.Editor`** (editor-only), namespace
> `Game.Editor.QuestExplorer`. Specs: `_bmad-output/implementation-artifacts/tech-spec-quest-explorer-editor-window.md`,
> `tech-spec-quest-explorer-npc-memories.md`.

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
        │                        NPC → scene objects, quest → QuestFacts targeting it, memory → gating
        │                        choices (MemoryGateLink), memory ownership, quest → involved memories
        │                        (reads / sets quest facts), static EvaluateMemory. UI-agnostic
        ▼                        (reuse it for a future graph view)
QuestValidator.Validate()       pure rules V1–V17 → ValidationIssue (sorted Error → Warning → Info).
                                V14–V17 are memory rules, scoped to memories involved in the quest
        ▼
QuestExplorerWindow / QuestDetailView (UI Toolkit, inline styles) · QuestSceneHighlighter (Scene GUI)
QuestEditActions (Undo-aware SerializedObject edits) · QuestStepRemap (pure) · QuestExplorerNamePrompt
QuestReport (Markdown text of the same data — for Claude) · ../Mcp/QuestReportTool (MCP `quest_report`)
```

---

## Using It From Claude (no UI)

Claude can't click the window — it reads the same data as text:

- **MCP tool `quest_report`** (`Assets/_Game/Editor/Mcp/`, assembly `Game.Editor.Mcp`, compiled only when the Unity
  MCP package is installed). `target`: `list` · `audit` (every quest + V14–V17 over **every** memory) · a quest
  (questId / asset name / title) · a fact asset name · a memory asset name.
- Fallback via `execute_code`: `return Game.Editor.QuestExplorer.QuestReport.Run("SpiderClear");`
- Custom queries: `var idx = Game.Editor.QuestExplorer.QuestIndexCollector.BuildIndex();` then `idx.GetSetters(f)`,
  `GetReaders`, `GetInvolvedMemories(q)`, `GetGates(m)`, `QuestValidator.Validate(q, idx, idx.Quests)`,
  `QuestValidator.ValidateAllMemories(idx)`.
- Edits: `QuestEditActions` (Undo-aware). Non-interactive creators for scripts: `CreateQuest`,
  `GetOrCreateDialogueFact / WorldFact / QuestFact`. The `Create*Fact(quest, loc)` variants open dialogs — UI only.
- In `execute_code`, `Object` is ambiguous (System vs UnityEngine) — write `UnityEngine.Object`.
- Workflow commands: `/quests:design`, `/quests:implement`, `/quests:audit`.

- Index rebuild: window open, **Refresh**, and debounced (one `delayCall`) on `projectChanged`,
  `hierarchyChanged` (ignored while playing), scene opened/closed, play-mode enter/exit, undo/redo.
- Quest detail **NPC Memories** section: memories grouped by NPC, with reason badges, editable unlock /
  invalidation conditions (`QuestEditActions.Add/Set/RemoveMemoryCondition`), start dialogue, gated
  choices, and a live state badge in Play Mode (locked / active / active · dialogue played / invalidated).
  A memory is involved if a condition reads a quest part fact (or a QuestFact targeting the quest), or its
  start dialogue chain sets a part fact. Not transitive.
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
- `TopicUnlockEvaluator` **skips null conditions**: a missing unlock reference makes a memory unlock
  early, a missing invalidation reference never closes it. That's why V14 is an Error.
- `ChoiceOption.requiredMemory` must be in the **owning NPC's** `memories` list (runtime checks only that
  NPC's active memories), or the choice never shows (V17).
- Older Unity versions' `DeleteArrayElementAtIndex` on an object-reference array nulls the element instead
  of removing it. Not seen on Unity 6.6, but `RemoveMemoryCondition` checks `arraySize` and deletes again.
- Memory condition rows capture array indices: after an edit the row is disabled until the debounced
  rebuild redraws it. An edited memory stays listed ("edited — no longer involved") for the session, even
  if the edit removed its last quest link (`QuestExplorerWindow.EditedMemories`).
- Scene links become fake-null after scene unload / play-mode transitions; link rows show "object no
  longer loaded" instead of acting on them.
