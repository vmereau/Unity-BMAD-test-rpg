---
title: 'Dialogue UI State Management Refactor'
slug: 'dialogue-ui-state-management'
created: '2026-04-07'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'C#', 'Unity UI (uGUI)', 'TextMeshPro', 'Unity Input System']
files_to_modify:
  - 'Assets/_Game/Scripts/UI/DialogueUI.cs'
  - 'Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab'
code_patterns:
  - 'IPointerClickHandler on MonoBehaviour root for click-anywhere UX'
  - 'DisplayState enum + SetState() for panel visibility toggling'
  - 'SetActive() on GameObjects instead of raycastTarget manipulation'
test_patterns: []
---

# Tech-Spec: Dialogue UI State Management Refactor

**Created:** 2026-04-07

---

## Overview

### Problem Statement

`DialoguePanel.prefab` has `ResponseText` and `TopicsScrollView` at identical anchor regions (y: 0.1→0.65 within the panel), with `ResponseText` rendered on top (last sibling). Since `ResponseText.raycastTarget = true`, it intercepts all pointer events before they reach the scroll view, making buttons unclickable and scroll non-functional. Beyond the blocking bug, the UI has no state concept: both areas are simultaneously active, "Continue" and "Farewell" buttons clutter the choice list, and ending a branch abruptly closes the dialogue instead of returning the player to the topic list.

### Solution

Refactor `DialogueUI` with an explicit three-value `DisplayState` enum (`Topics`, `Text`, `Choices`). Each state toggles the visibility of `ResponseText` and `TopicsScrollView` GameObjects. Text nodes use `IPointerClickHandler` for click-anywhere advancement. Reaching a null `nextNode` restores the topic list instead of closing. Escape/Cancel is the only close path.

### Scope

**In Scope:**
- `DialogueUI.cs` — state management, IPointerClickHandler, show/hide logic, remove Advance/Farewell buttons
- `DialoguePanel.prefab` — add `_topicsScrollView` serialized field wire-up, set ResponseText inactive by default, fix ResponseText `raycastTarget = false`

**Out of Scope:**
- `DialogueSystem.cs` — no changes; still drives ShowTextNode/ShowChoiceNode/Close
- Dialogue node ScriptableObjects — no changes
- `NPCDialogueGraphComponent.cs` — no changes
- Layout redesign of the panel anchors (ResponseText and TopicsScrollView overlap is acceptable because only one or both are shown based on state, never in a conflicting arrangement)
- `TopicButtonPrefab.prefab` — no changes

---

## Context for Development

### Codebase Patterns

- All game scripts are under `Assets/_Game/`, compiled into the `Game` assembly (Game.asmdef).
- Namespaces: `Game.UI` for `DialogueUI`, `Game.World` for `DialogueSystem`, `Game.Dialogue` for node SOs.
- `[SerializeField] private` for all Inspector-exposed fields.
- `GameLog.Info/Warn/Error(TAG, ...)` — never `Debug.Log`.
- Event subscriptions in `OnEnable`/`OnDisable`.
- `IPointerClickHandler` is in `UnityEngine.EventSystems` — add the using directive.
- `using UnityEngine.EventSystems;` is required for `IPointerClickHandler` and `PointerEventData`.
- Unity's `ExecuteHierarchy` for IPointerClickHandler walks up from the deepest hit; a Button child handling its own IPointerClickHandler will stop propagation, so `OnPointerClick` on the DialoguePanel root is only called when no Button child intercepts first. The `DisplayState.Text` guard provides a second safety net.

### Key Dialogue Node Data Model

```
DialogueNode (abstract SO)
├── text: string           — NPC speech or topic label
├── nextNode: DialogueNode — next in chain; null = end of branch

StartDialogueNode : DialogueNode
    — topic label button; nextNode → first content node

TextDialogueNode : DialogueNode
    — NPC speech line; nextNode → next node or null

ChoiceDialogueNode : DialogueNode
    — NPC speech line (shown in ResponseText) + choices[]
    — IsEndNode() always returns false
    — choices[i].nextNode → next node or null per choice
    — choices[i].requiredMemory → optional memory gate (filtered by NPCDialogueGraphComponent)
```

### Current DialogueUI Flow (Before Change)

```
Open()          → PopulateStartNodes → AddStartNodeButton × N + AddFarewellButton
ShowTextNode()  → ClearTopicButtons → AddAdvanceButton (Continue/Farewell label)
ShowChoiceNode()→ ClearTopicButtons → AddChoiceButton × N + AddFarewellButton
```

All buttons are inside `_topicsContainer` (TopicsContent inside TopicsScrollView). All buttons call `_dialogueSystem.AdvanceToNode(node)` where `node` can be null (Farewell/Continue with null nextNode), which triggers `DialogueSystem.Close()`.

### New DialogueUI Flow (After Change)

```
Open()          → cache startNodes, RestoreTopics()
RestoreTopics() → SetState(Topics), populate topic buttons (no Farewell button), clear ResponseText
ShowTextNode()  → SetState(Text), set responseText.text, store _pendingNextNode
ShowChoiceNode()→ SetState(Choices), set responseText.text, populate choice buttons (no Farewell button)
OnPointerClick()→ guard: only act in Text state → AdvanceToNode(_pendingNextNode) or RestoreTopics()
```

Null nextNode from choice button → `RestoreTopics()` directly (bypasses `AdvanceToNode(null)` / Close).  
Null nextNode from text advance (click) → `RestoreTopics()` directly.  
Close: only via Escape key → `_dialogueSystem.Close()` (unchanged).

### Files to Reference

| File | Purpose |
|------|---------|
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | Primary file to modify |
| `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab` | Prefab to update (field wiring + defaults) |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Calls ShowTextNode/ShowChoiceNode; drives AdvanceToNode/Close — read-only reference |
| `Assets/_Game/ScriptableObjects/Dialogue/DialogueNode.cs` | Base SO: text, nextNode, IsEndNode() |
| `Assets/_Game/ScriptableObjects/Dialogue/TextDialogueNode.cs` | NPC speech node (inherits nextNode) |
| `Assets/_Game/ScriptableObjects/Dialogue/ChoiceDialogueNode.cs` | NPC speech + choices[] |
| `Assets/_Game/ScriptableObjects/Dialogue/StartDialogueNode.cs` | Topic entry point (nextNode = first content) |
| `Assets/_Game/Scripts/UI/CLAUDE.md` | UI rules: CursorManager, OnDisable null guard, raycastTarget patterns |
| `Assets/_Game/Prefabs/CLAUDE.md` | Prefab structure rules; DialogueUI._dialogueSystem wired via Player.prefab overrides |

### Technical Decisions

- **State enum**: `DisplayState { Topics, Text, Choices }` inside `DialogueUI` — simple switch, no external SM library.
- **Visibility**: `SetActive()` on GO references (`_topicsScrollView`, `_responseText.gameObject`). Not `CanvasGroup.alpha` — we want to fully disable raycasting and layout recalculation.
- **Click-anywhere**: `IPointerClickHandler` on the `DialogueUI` MonoBehaviour (which lives on the panel root). Safe because: (a) in Text state no buttons exist; (b) in Topics/Choices state the guard `if (_state != DisplayState.Text) return` prevents accidental advance.
- **_topicsScrollView reference**: New `[SerializeField] private GameObject _topicsScrollView;` field, wired to the `TopicsScrollView` GO (fileID `865366693142477418` in `DialoguePanel.prefab`). Do NOT navigate via `_topicsContainer.parent.parent` — fragile.
- **ResponseText default**: Set inactive in prefab so the panel starts in a clean state even before `Open()` is called.
- **No Farewell button**: Removed. End of branch → topics. Close → Escape only.
- **No Continue button**: Removed. Text advancement → click anywhere on panel.
- **_dialogueSystem null guard**: `_dialogueSystem` is null in `DialoguePanel.prefab` but wired via `Player.prefab` nested-prefab override. Keep existing null-guard pattern on public methods.
- **AddStartNodeButton null guard**: Existing `btn.onClick` listener changes from `AdvanceToNode(captured.nextNode)` to guarded call — if `captured.nextNode == null`, no-op (log warning). Prevents accidentally calling `AdvanceToNode(null)` → Close from a topic with a missing nextNode.
- **AddChoiceButton null guard**: If `captured.nextNode == null`, call `RestoreTopics()` instead of `AdvanceToNode(null)`. This is the "end of choice branch → return to topics" behavior.

---

## Implementation Plan

### Tasks

**Task 1 — Add `_topicsScrollView` serialized field to `DialogueUI.cs`**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add one new `[SerializeField]` field below `_topicsContainer`:

```csharp
[SerializeField] private GameObject _topicsScrollView;
```

---

**Task 2 — Add state infrastructure to `DialogueUI.cs`**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add the following private members to the class body (after the `_input` field):

```csharp
private enum DisplayState { Topics, Text, Choices }
private DisplayState _state = DisplayState.Topics;
private DialogueNode _pendingNextNode;
private StartDialogueNode[] _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
```

Add `using UnityEngine.EventSystems;` at the top of the file.

Change the class declaration to implement `IPointerClickHandler`:

```csharp
public class DialogueUI : MonoBehaviour, IPointerClickHandler
```

---

**Task 3 — Implement `SetState()` helper**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add a private helper in the `// ── Private Helpers ─` section:

```csharp
private void SetState(DisplayState state)
{
    _state = state;
    bool showResponse = state == DisplayState.Text || state == DisplayState.Choices;
    bool showTopics   = state == DisplayState.Topics || state == DisplayState.Choices;
    if (_responseText != null)
        _responseText.gameObject.SetActive(showResponse);
    if (_topicsScrollView != null)
        _topicsScrollView.SetActive(showTopics);
}
```

---

**Task 4 — Implement `RestoreTopics()` helper**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add after `SetState()`:

```csharp
private void RestoreTopics()
{
    ClearTopicButtons();
    if (_responseText != null)
        _responseText.text = string.Empty;
    if (_cachedStartNodes != null)
        PopulateStartNodes(_cachedStartNodes);
    SetState(DisplayState.Topics);
}
```

Note: `PopulateStartNodes` already calls `ClearTopicButtons()` at the start, so calling it here before `PopulateStartNodes` is redundant but harmless. Keep it for safety in case `_cachedStartNodes` is null.

Actually simplify — `PopulateStartNodes` handles clearing, so:

```csharp
private void RestoreTopics()
{
    if (_responseText != null)
        _responseText.text = string.Empty;
    if (_cachedStartNodes != null && _cachedStartNodes.Length > 0)
        PopulateStartNodes(_cachedStartNodes);
    else
        ClearTopicButtons();
    SetState(DisplayState.Topics);
}
```

---

**Task 5 — Rewrite `Open()` to use state machine**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Replace the current `Open()` body:

```csharp
public void Open(string npcName, StartDialogueNode[] startNodes)
{
    if (_panel == null)
    {
        GameLog.Error(TAG, "Panel not assigned — cannot open dialogue UI");
        return;
    }

    _cachedStartNodes = startNodes;

    _panel.SetActive(true);

    if (_npcNameText != null)
        _npcNameText.text = npcName;

    RestoreTopics();
    GameLog.Info(TAG, $"DialogueUI opened with {startNodes.Length} topic(s)");
}
```

---

**Task 6 — Rewrite `ShowTextNode()` to use state machine**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Replace the current `ShowTextNode()` body:

```csharp
public void ShowTextNode(TextDialogueNode node)
{
    _pendingNextNode = node.nextNode;
    if (_responseText != null)
        _responseText.text = node.text;
    ClearTopicButtons();
    SetState(DisplayState.Text);
}
```

---

**Task 7 — Rewrite `ShowChoiceNode()` to use state machine**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Replace the current `ShowChoiceNode()` body:

```csharp
public void ShowChoiceNode(ChoiceDialogueNode node, ChoiceOption[] availableChoices)
{
    if (_responseText != null)
        _responseText.text = node.text;

    ClearTopicButtons();

    foreach (var choice in availableChoices)
    {
        if (choice == null) continue;
        AddChoiceButton(choice);
    }

    SetState(DisplayState.Choices);
}
```

Note: `AddFarewellButton()` is removed from this method. No farewell button in choices.

---

**Task 8 — Implement `OnPointerClick()`**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add to the `// ── Private Helpers ─` section:

```csharp
public void OnPointerClick(PointerEventData eventData)
{
    if (_state != DisplayState.Text) return;
    if (_dialogueSystem == null) return;

    if (_pendingNextNode != null)
        _dialogueSystem.AdvanceToNode(_pendingNextNode);
    else
        RestoreTopics();
}
```

---

**Task 9 — Update `AddStartNodeButton()` to guard null nextNode**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Replace the listener in `AddStartNodeButton()`:

```csharp
private void AddStartNodeButton(StartDialogueNode node)
{
    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = node.text;

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
    {
        var captured = node;
        btn.onClick.AddListener(() =>
        {
            if (captured.nextNode != null)
                _dialogueSystem.AdvanceToNode(captured.nextNode);
            else
                GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
        });
    }
}
```

---

**Task 10 — Update `AddChoiceButton()` to call `RestoreTopics()` on null nextNode**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Replace the listener in `AddChoiceButton()`:

```csharp
private void AddChoiceButton(ChoiceOption choice)
{
    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = choice.text;

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
    {
        var captured = choice;
        btn.onClick.AddListener(() =>
        {
            if (captured.nextNode != null)
                _dialogueSystem.AdvanceToNode(captured.nextNode);
            else
                RestoreTopics();
        });
    }
}
```

---

**Task 11 — Remove `AddAdvanceButton()` and `AddFarewellButton()` methods**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Delete both private methods entirely — they are no longer called.

---

**Task 12 — Update `Close()` to reset state**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

Add `SetState(DisplayState.Topics)` at the end of `Close()` so the panel is in a known state for the next `Open()` call (TopicsScrollView shown, ResponseText hidden):

```csharp
public void Close()
{
    if (_panel != null)
        _panel.SetActive(false);

    ClearTopicButtons();

    if (_responseText != null)
        _responseText.text = string.Empty;

    _pendingNextNode = null;
    _cachedStartNodes = System.Array.Empty<StartDialogueNode>();
    SetState(DisplayState.Topics);

    GameLog.Info(TAG, "DialogueUI closed");
}
```

---

**Task 13 — Fix `ResponseText` in `DialoguePanel.prefab`: set inactive + raycastTarget false**

File: `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab`

The `ResponseText` GameObject has fileID `8849851252467457170`. Its TMP component has fileID `2659127211681736764`.

**Change 1** — Set ResponseText GO inactive by default:
Find the GameObject block for `ResponseText` (fileID `8849851252467457170`) and change:
```yaml
  m_IsActive: 1
```
to:
```yaml
  m_IsActive: 0
```

**Change 2** — Disable raycastTarget on ResponseText TMP component (fileID `2659127211681736764`):
Within the MonoBehaviour block with `m_EditorClassIdentifier: Unity.TextMeshPro::TMPro.TextMeshProUGUI` that has `m_text: ` (empty — the ResponseText, not NPCNameText), change:
```yaml
  m_RaycastTarget: 1
```
to:
```yaml
  m_RaycastTarget: 0
```

---

**Task 14 — Wire `_topicsScrollView` field in `DialoguePanel.prefab`**

File: `Assets/_Game/Prefabs/UI/Dialogue/DialoguePanel.prefab`

In the `DialogueUI` MonoBehaviour component (fileID `7791153888841341599`), add the `_topicsScrollView` field pointing to the `TopicsScrollView` GO (fileID `865366693142477418`):

Current block:
```yaml
  m_EditorClassIdentifier: Game::Game.UI.DialogueUI
  _panel: {fileID: 3051495122593222561}
  _npcNameText: {fileID: 6859960061220436461}
  _responseText: {fileID: 2659127211681736764}
  _topicsContainer: {fileID: 1666246959837339901}
  _topicButtonPrefab: {fileID: 1459530989016116475, guid: 62c8dbe92e229244ab082bdf8f5c11d9, type: 3}
  _dialogueSystem: {fileID: 0}
```

Change to:
```yaml
  m_EditorClassIdentifier: Game::Game.UI.DialogueUI
  _panel: {fileID: 3051495122593222561}
  _npcNameText: {fileID: 6859960061220436461}
  _responseText: {fileID: 2659127211681736764}
  _topicsScrollView: {fileID: 865366693142477418}
  _topicsContainer: {fileID: 1666246959837339901}
  _topicButtonPrefab: {fileID: 1459530989016116475, guid: 62c8dbe92e229244ab082bdf8f5c11d9, type: 3}
  _dialogueSystem: {fileID: 0}
```

Note: `_dialogueSystem` remains `{fileID: 0}` here — it is wired via `Player.prefab` nested-prefab override (confirmed at Player.prefab line 1496-1498). Do NOT change it in this file.

---

### Acceptance Criteria

**AC1 — Initial open state shows topics, hides response text**

Given the dialogue is triggered by interacting with an NPC  
When `DialogueUI.Open()` is called with available start nodes  
Then `TopicsScrollView` is active and shows one button per `StartDialogueNode`  
And `ResponseText` is inactive  
And there is no "Farewell" button in the list

**AC2 — Clicking a topic button shows text node and hides scroll view**

Given the dialogue is open with topics visible  
When the player clicks a topic button whose `StartDialogueNode.nextNode` is a `TextDialogueNode`  
Then `TopicsScrollView` becomes inactive  
And `ResponseText` becomes active displaying `TextDialogueNode.text`  
And there are no buttons in the panel

**AC3 — Clicking anywhere on the panel advances text node**

Given a `TextDialogueNode` is being displayed  
When the player clicks anywhere on the `DialoguePanel`  
Then if `nextNode != null`: the next node is processed (ShowTextNode or ShowChoiceNode)  
And if `nextNode == null`: topics are restored (AC1 state, same NPC session)

**AC4 — Choice node shows NPC text + choice buttons**

Given a `TextDialogueNode` or `StartDialogueNode` has `nextNode` pointing to a `ChoiceDialogueNode`  
When that node is processed (via click or topic button)  
Then `ResponseText` is active showing `ChoiceDialogueNode.text`  
And `TopicsScrollView` is active showing one button per available (memory-gated) choice  
And there is no "Farewell" button

**AC5 — Clicking a choice with a nextNode processes that node**

Given a `ChoiceDialogueNode` is displayed  
When the player clicks a choice whose `ChoiceOption.nextNode` is non-null  
Then `DialogueSystem.AdvanceToNode(choice.nextNode)` is called  
And the resulting node is displayed (text or choices)

**AC6 — Clicking a choice with null nextNode restores topics**

Given a `ChoiceDialogueNode` is displayed  
When the player clicks a choice whose `ChoiceOption.nextNode` is null  
Then topics are restored (AC1 state)  
And the dialogue remains open

**AC7 — Scroll works for long choice lists**

Given a `ChoiceDialogueNode` with more choices than fit in the visible scroll area  
When the topics scroll view is shown  
Then the player can scroll vertically through the choice buttons  
And all buttons are clickable

**AC8 — Escape closes dialogue**

Given the dialogue is open in any state  
When the player presses Escape (Cancel input action)  
Then `DialogueSystem.Close()` is called  
And the dialogue panel becomes inactive  
And the cursor is locked

**AC9 — No NullReferenceException from button clicks**

Given the prefab's `_dialogueSystem` is null in `DialoguePanel.prefab` (wired at runtime via Player.prefab override)  
When buttons are clicked  
Then no NullReferenceException is thrown (existing null guard on `_dialogueSystem` covers runtime wired state)

**AC10 — Topic button with null nextNode is a no-op (defensive)**

Given a `StartDialogueNode` has no `nextNode` assigned  
When the player clicks its button  
Then no dialogue advance occurs  
And a `GameLog.Warn` is emitted with the node's text  
And the dialogue remains in Topics state

---

## Additional Context

### Dependencies

- `UnityEngine.EventSystems.IPointerClickHandler` — requires `using UnityEngine.EventSystems;`
- `DialoguePanel` root GameObject must have a raycast-able component to receive click events — `Background` Image (fileID `1373999521046610824`) with `raycastTarget: true` already provides this
- `EventSystem` with `InputSystemUIInputModule` exists as child of `UICanvas` in the Player prefab — required for all pointer events to function

### Testing Strategy

Manual in-editor play test:
1. Enter Play mode, interact with the NPC in `StartingTown.unity`
2. Verify: topics appear, no response text, no Farewell button
3. Click a topic → verify text appears, scroll view hidden, no buttons
4. Click anywhere → verify advance to next node or topic restore
5. Navigate into a ChoiceDialogueNode → verify NPC text + choice buttons visible, scroll works
6. Click a choice with nextNode → verify advance
7. Click a choice with null nextNode → verify topic restore
8. Press Escape → verify dialogue closes, cursor locked

### Notes

- `PopulateStartNodes()` calls `AddFarewellButton()` at the end — **remove that call** as part of Task 4 (`RestoreTopics` calls `PopulateStartNodes`). Farewell button must not appear in topic list.
- `_TAG` constant is used by `GameLog` — keep as-is.
- The `TAG` constant (`private const string TAG`) remains valid — `GameLog` calls remain in the refactored code.
- `DialogueSystem.AdvanceToNode(null)` still closes the dialogue (unchanged). It will no longer be called by any UI button listener, but the method contract is preserved for future use.
- After implementation, verify in the Unity Editor that `_topicsScrollView` is correctly wired in `DialoguePanel.prefab` — Unity may reset the field if a domain reload occurs before saving.
