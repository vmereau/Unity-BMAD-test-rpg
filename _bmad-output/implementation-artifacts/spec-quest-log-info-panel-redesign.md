---
title: 'Quest log redesign — structured info panel, collapsible steps, list selection'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: '902101bc424debc03f663ee2d69bdb59df23e451'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Art/ART_DIRECTION.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The quest info panel renders every line (step title, step description, parts) as same-weight white text: no step state, no hierarchy, "Start" / "Completed" / "Failed" drawn as fake numbered steps. The quest list has no selected highlight, no auto-selection and no tab counts.

**Approach:** Restyle the info panel into header (title + status + divider + briefing), collapsible step blocks with a done / in-progress state, journal-style parts, and framed "accepted" / outcome sections; add selection, auto-select and counts to the list. Code + prefab only — tints, 1-px `Image` dividers, existing fonts, no new sprites.

## Boundaries & Constraints

**Always:** Palette from `ART_DIRECTION.md` as `[SerializeField] Color` defaults (title torch `#E0A35A`, body parchment `#D8CCB0`, soft parchment `#B8A988`, brass accent `#8C7343`, completed muted green `#5A6346` lightened for text legibility, failed dried blood `#7A2A22` lightened likewise). Quest status stays derived from `QuestSO` / `WorldStateManager` — UI holds no quest state except selection and expand toggles. Null-guard every serialized dependency (existing pattern). `TMP_Text` only; ASCII / glyphs present in the existing TMP font (verify `•`).

**Ask First:** Any change to `QuestSO` / `QuestStep` / `QuestPart` data shape; any new sprite.

**Never:** Progress counters (`x / y`) for empty-entry parts. "New entry since last viewed" markers (deferred). Polling in `Update`. Persisting expand state across sessions / saves.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Open log, Started tab has quests | 3 started quests, nothing selected | First quest auto-selected, row highlighted, info shown | N/A |
| Tab switch to empty tab | Failed tab, 0 quests | List empty, info panel shows empty state | N/A |
| Live refresh, selected still visible | Step fact turns true while log open | Selection kept, info panel rebuilt, manual expand toggles of that quest kept | N/A |
| Selected quest leaves tab | Selected quest completes while on Started tab | First remaining visible quest selected (or empty state) | N/A |
| Completed step | All step parts true | Collapsed: marker done + dimmed title; click header → expands to description + parts; click again → collapses | N/A |
| In-progress step | Some (not all) parts true | Expanded, brass accent bar, header click collapses it | N/A |
| Step with only empty entries | Spider quest steps | Title + description shown, no part rows, no blank gap | N/A |
| Completed / failed quest | Outcome fact true | Status label tinted green / red; closing section after a divider with label "Quest completed" / "Quest failed" + outcome entries | Section omitted if no active outcome parts |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Quest/QuestLogUI.cs` -- root; owns selection, event-driven refresh
- `Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs` -- tab filter, spawns rows, tab styles
- `Assets/_Game/Scripts/UI/Quest/QuestButtonUI.cs` -- list row, hover tint
- `Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs` -- header + `BuildContent` (start / steps / outcome)
- `Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs` / `QuestPartUI.cs` -- step block / part row
- `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` -- `IsStepCompleted`, `GetActiveParts` (read only)
- `Assets/_Game/Data/Events/OnWorldFactChanged.asset` (`GameEventSO_Fact`) -- fact-changed channel for live refresh
- `Assets/_Game/Prefabs/UI/QuestLog/{QuestLogUI,QuestStepPrefab,QuestPartPrefab,QuestLogButton}.prefab`
- `Assets/_Game/Editor/UI/UILayoutCapture.cs` -- `Capture("QuestLog")` preview

## Tasks & Acceptance

**Execution:**
- [x] `QuestStepUI.cs` + `QuestStepPrefab` -- header row = `Button` (state marker `Image` square, title, `+`/`-` expand hint), body = description + `PartsRoot`, left 4-px accent `Image`. `Bind(title, description, parts, state, expanded)` with state `InProgress | Completed | Section`; `Section` (accepted / outcome) = tinted label, always expanded, no marker / hint / button; raises a toggle callback the panel records -- collapsible steps with readable state
- [x] `QuestPartUI.cs` + `QuestPartPrefab` -- `• ` prefix, soft-parchment colour, ~24 px left indent, wrapping -- journal entries read as entries
- [x] `QuestInfoPanelUI.cs` + header in `QuestLogUI.prefab` -- left-aligned title, status label (In progress / Completed / Failed, tinted), 1-px brass divider, italic soft-parchment description; content order: "Quest accepted" section (start part) → steps in list order, no numbering, completed collapsed unless toggled, in-progress expanded unless toggled → divider + outcome section; toggles stored per step index, cleared when a different quest is shown, kept on rebuild of the same quest -- structure + expand memory
- [x] `QuestButtonUI.cs` + `QuestLogButton` -- `SetSelected(bool)`, selected colour (brass-dark like `SkillSlotUI._selectedColor`) wins over hover -- visible selection
- [x] `QuestListPanelUI.cs` -- tab labels `Started (n)` / `Completed (n)` / `Failed (n)` via serialized label refs; on refresh / tab switch keep the selected quest if visible, else pick the first visible (or none) and notify `QuestLogUI` -- auto-select + counts
- [x] `QuestLogUI.cs` + prefab -- own `_selectedQuest`; on open / refresh / selection change show it or `Hide()`; subscribe `_onFactChanged` (`OnWorldFactChanged`, raised on every fact write — catches single parts, which raise no quest event) and rebuild the info panel on any quest / fact event -- live refresh with stable selection
- [x] `QuestLogUI.prefab` -- fix placeholder description (stray `\r\n` + spaces), restyle placeholder step instances so `UILayoutCapture.Capture("QuestLog")` shows one completed (collapsed) + one in-progress step -- preview reflects the design
- [x] `Scripts/UI/Quest/CLAUDE.md` -- document selection ownership, step states, expand-toggle memory, section mode -- keep folder doc current

**Acceptance Criteria:**
- Given the log opens with started quests, when nothing was selected, then the first quest is selected and highlighted and its details shown.
- Given an expanded completed step, when the player selects another quest and comes back, then that step is collapsed again.
- Given the info panel, when it renders, then no "1." / "Start" / "Completed" pseudo-step titles appear.
- Given Unity compiles, when the console is read, then there are no errors or new warnings from the Quest UI scripts.

## Design Notes

Implementation: `QuestListPanelUI` owns the selection and raises `SelectionChanged` after every refresh / tab switch / click; `QuestLogUI` forwards clicks and shows / hides the info panel from that event. `QuestInfoPanel` became a `VerticalLayoutGroup` (header preferred height, scroll flexible) so long descriptions don't overlap the steps.


Step state: `quest.IsStepCompleted(i)` → `Completed`; visible (any active part) and not completed → `InProgress`. Default expanded = `state == InProgress`. A header click stores the player's choice per step index together with the completion state it was made in; the choice is dropped when the step's completion state changes, so a step that completes while the log is open collapses to its new default. Toggling rebuilds only that block's body visibility (`SetActive`), then `LayoutRebuilder.MarkLayoutForRebuild` on the content root.

Pre-existing uncommitted diff in `QuestLogUI.prefab`: tab rects zeroed (layout-group-driven, harmless) and TMP `m_ActiveFontFeatures` re-serialization (harmless), **but** it also deactivated `QuestInfoPanel` (nothing re-activates it → blank info panel) and activated the `QuestLogUI` root (normally toggled by `UIScreenManager`). Both flags restored to their HEAD values.

Deferred: "new entry since last viewed" list marker (needs persisted seen-state) → `deferred-work.md`.

## Verification

**Commands:**
- `read_console` (errors / warnings) after `refresh_unity` -- expected: clean
- `execute_code` → `UILayoutCapture.Capture("QuestLog")` -- expected: preview shows header, status, divider, collapsed + expanded steps, bullets

**Manual checks:**
- Play mode: accept FindHerbalist and SpiderClear, press J — auto-select, counts, expand/collapse completed steps, complete a quest with the log open → selection moves.

## Suggested Review Order

**Info panel structure**

- Entry point: accepted section → steps with state + remembered expand choice → outcome section
  [`QuestInfoPanelUI.cs:96`](../../Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs#L96)

- Expand choice kept only while the step's completion state is unchanged
  [`QuestInfoPanelUI.cs:124`](../../Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs#L124)

- Overrides reset, scroll-to-top and log only when a different quest is shown
  [`QuestInfoPanelUI.cs:55`](../../Assets/_Game/Scripts/UI/Quest/QuestInfoPanelUI.cs#L55)

**Step block**

- Collapsible step: marker / accent / title tints by state, header click toggles body
  [`QuestStepUI.cs:54`](../../Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs#L54)

- Section mode: tinted label, optional divider, no toggle
  [`QuestStepUI.cs:75`](../../Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs#L75)

- No blank gap when every part entry is empty
  [`QuestStepUI.cs:128`](../../Assets/_Game/Scripts/UI/Quest/QuestStepUI.cs#L128)

- Bullet with TMP hanging indent
  [`QuestPartUI.cs:20`](../../Assets/_Game/Scripts/UI/Quest/QuestPartUI.cs#L20)

**Selection & live refresh**

- List panel owns selection; event fires after every refresh
  [`QuestListPanelUI.cs:31`](../../Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs#L31)

- Keep selected quest if visible, else first row or none
  [`QuestListPanelUI.cs:89`](../../Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs#L89)

- Fact / quest events coalesced into one refresh per frame
  [`QuestLogUI.cs:66`](../../Assets/_Game/Scripts/UI/Quest/QuestLogUI.cs#L66)

- Info panel driven purely by the selection event
  [`QuestLogUI.cs:71`](../../Assets/_Game/Scripts/UI/Quest/QuestLogUI.cs#L71)

**Peripherals**

- Tab counts
  [`QuestListPanelUI.cs:146`](../../Assets/_Game/Scripts/UI/Quest/QuestListPanelUI.cs#L146)

- Row bind reports failure; selected tint wins over hover
  [`QuestButtonUI.cs:38`](../../Assets/_Game/Scripts/UI/Quest/QuestButtonUI.cs#L38)

- Prefabs: `Prefabs/UI/QuestLog/QuestStepPrefab`, `QuestPartPrefab`, `QuestLogUI` (layout, wiring, restored active flags); folder doc `Scripts/UI/Quest/CLAUDE.md`
