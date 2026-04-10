---
title: 'Dialogue UX Improvements — Number Keys, Escape Guard & IsInDialogue State'
slug: 'dialogue-ux-improvements'
created: '2026-04-09'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1', 'C#', 'Unity Input System', 'TextMeshPro']
files_to_modify:
  - 'Assets/_Game/InputSystem_Actions.inputactions'
  - 'Assets/_Game/InputSystem_Actions.cs'
  - 'Assets/_Game/Scripts/UI/DialogueUI.cs'
  - 'Assets/_Game/Scripts/Player/PlayerStateManager.cs'
  - 'Assets/_Game/Scripts/UI/UIScreenManager.cs'
  - 'Assets/_Game/Scripts/World/DialogueSystem.cs'
code_patterns:
  - 'InputSystem_Actions dual-file: both .inputactions and embedded JSON in .cs must be updated'
  - 'Event subscription pattern: _input.UI.Action.performed += Handler in OnEnable, -= in OnDisable'
  - 'Slot callbacks: System.Action[11] indexed 1-10 (1=key1, ..., 9=key9, 10=key0)'
  - 'PlayerStateManager: serialized dep on DialogueSystem via SerializeField'
  - 'GameLog.Info/Warn/Error for all logging (no Debug.Log)'
test_patterns: ['Manual playtest — no automated test infra for dialogue UI']
---

# Tech-Spec: Dialogue UX Improvements — Number Keys, Escape Guard & IsInDialogue State

**Created:** 2026-04-09

## Overview

### Problem Statement

The dialogue system has four UX gaps: (1) pressing Escape closes a dialogue mid-chain (it should only close from the Topics list), (2) there are no keyboard number shortcuts (1–9, 0) for selecting topics, choices, or advancing text — forcing mouse-only interaction, (3) there is no "Farewell" option to manually exit dialogue from the Topics screen, and (4) no `IsInDialogue` state prevents menu panels (Inventory `I`, Character `C`) from opening during conversations.

### Solution

Update `DialogueUI` to restrict Escape to `DisplayState.Topics`, wire number keys 1–9 and 0 to dynamically-assigned dialogue options, prepend numbers to button labels, show a "1. Next" button during text nodes, add a "Farewell" entry that always takes the next available number slot, and add an `IsInDialogue` bool to `PlayerStateManager` gated in `UIScreenManager.OpenTab()`.

### Scope

**In Scope:**
- Escape key only closes dialogue when `_state == DisplayState.Topics`
- 10 new `DialogueOption1`–`DialogueOption9` + `DialogueOption0` actions in the UI action map
- Number key shortcuts (1–9, 0) mapped to dynamically-populated dialogue slots
- Button labels prefixed with their slot number (e.g. `1. What is this place?`, `0. [Farewell]`)
- A pre-existing `ResponseWrapper` GameObject (child of DialoguePanel, containing `ResponseText` and `NextNodeButton`) is shown/hidden instead of `_responseText` directly — `SetState` targets `_responseWrapper` for `showResponse`
- `NextNodeButton` (pre-existing Button child of `ResponseWrapper`) wired in `ShowTextNode` to advance; also registered as slot 1 keyboard callback
- `_topicsScrollView` keeps its original show/hide behavior (visible only during Topics and Choices)
- "Farewell" button always appended to Topics list, taking slot `N+1` (wrapping to `0` after slot 9)
- `PlayerStateManager.IsInDialogue` bool with `SetInDialogue(bool)` setter
- `UIScreenManager.OpenTab()` early-returns if `IsInDialogue == true`
- `DialogueSystem` sets `IsInDialogue` true on open, false on close

**Out of Scope:**
- Changing dialogue graph ScriptableObjects or their serialized data
- Adding new dialogue content or NPC conversations
- Mouse click-to-advance on text nodes (existing `IPointerClickHandler` stays)
- Gamepad / controller support for numbered dialogue
- Any UI visual redesign beyond prepending numbers

---

## Context for Development

### Codebase Patterns

- **InputSystem_Actions dual-file contract (CRITICAL):** Both `InputSystem_Actions.inputactions` (plain JSON) and `InputSystem_Actions.cs` (embedded JSON with `""` double-escaped quotes) must be updated simultaneously. The `.cs` file also needs: new `private readonly InputAction m_UI_DialogueOptionX` fields, `FindAction()` calls, `public InputAction @DialogueOptionX` properties in the `UIActions` struct, and new entries in `AddCallbacks`, `UnregisterCallbacks`, `RemoveCallbacks`, `SetCallbacks`, and the `IUIActions` interface.
- **No class implements `IUIActions`** in the project — the project uses direct `+=` event subscription (`_input.UI.Cancel.performed += HandleCancel`). Extending the interface is safe and non-breaking.
- **Input subscription pattern:** Subscribe in `OnEnable`, unsubscribe in `OnDisable`, dispose in `OnDestroy`. Always null-guard `_input` in `OnDisable`.
- **Slot numbering:** Slots are 1-indexed (slot 1 = key `1`, ..., slot 9 = key `9`, slot 10 = key `0`). Use `private static string SlotLabel(int slot) => slot == 10 ? "0" : slot.ToString();` to format labels.
- **Slot callbacks array:** `private readonly System.Action[] _slotCallbacks = new System.Action[11];` — indices 1-10 used; index 0 unused. Clear in `ClearTopicButtons()` with `System.Array.Clear(_slotCallbacks, 0, _slotCallbacks.Length)`.
- **`ResponseWrapper` pattern:** A new `[SerializeField] private GameObject _responseWrapper` field replaces direct toggling of `_responseText.gameObject`. `_responseWrapper` contains both `ResponseText` (the existing TMP_Text) and `NextNodeButton` (a pre-existing Button). `SetState` shows/hides `_responseWrapper` for `showResponse`, and keeps `_topicsScrollView` unchanged (visible only for Topics and Choices).
- **`NextNodeButton` wiring:** `[SerializeField] private Button _nextNodeButton` references the pre-existing button inside `ResponseWrapper`. In `ShowTextNode`, its `onClick` listeners are cleared and re-added each call. It is also registered as `_slotCallbacks[1]` for keyboard shortcut.
- **`_topicsScrollView` visibility:** Unchanged from current behavior — shown only during `DisplayState.Topics` and `DisplayState.Choices`.
- **GameLog pattern:** Always use `GameLog.Info/Warn/Error(TAG, msg)` — never `Debug.Log`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | Main target — escape guard, number shortcuts, farewell, "1. next" button |
| `Assets/_Game/Scripts/Player/PlayerStateManager.cs` | Add `IsInDialogue` bool + `SetInDialogue(bool)` |
| `Assets/_Game/Scripts/UI/UIScreenManager.cs` | Add `IsInDialogue` guard in `OpenTab()` |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Set `IsInDialogue` on open/close; add serialized `PlayerStateManager` ref |
| `Assets/_Game/InputSystem_Actions.cs` | Add 10 new UI action fields, embedded JSON, struct props, interface methods |
| `Assets/_Game/InputSystem_Actions.inputactions` | Add 10 new UI action definitions + keyboard bindings |

### Technical Decisions

1. **New actions go in the UI map, not a new Dialogue map** — `DialogueUI` already enables `_input.UI`, so adding to that map avoids new map management. Actions are only active while DialogueUI is alive and the UI map is enabled.

2. **`PlayerStateManager.SetInDialogue` called by `DialogueSystem`** — `DialogueSystem` holds a `[SerializeField] private PlayerStateManager _playerStateManager;` reference (wired in the Editor). This follows the project's serialized-dep pattern and avoids singletons or static state.

3. **`_topicsScrollView` always visible** — Changing `showTopics = state == DisplayState.Topics || state == DisplayState.Choices` to `showTopics = true` lets the same container show "1. Next" in Text state. This is the minimal change for the desired UX.

4. **Farewell slot = `N+1` where N = regular option count, wrapping 0 after slot 9** — Slot 10 maps to key `0`. If there are 9 regular options, Farewell = key `0`. If there are 0 options, Farewell = key `1`. Farewell calls `_dialogueSystem.Close()`.

5. **`IUIActions` interface extended for completeness** — The 10 new methods (`OnDialogueOption1`..`OnDialogueOption0`) are added to the interface and its `AddCallbacks`/`UnregisterCallbacks` wiring, even though no class in the project currently implements the interface.

---

## Implementation Plan

### Tasks

**Task 1 — `InputSystem_Actions.inputactions`: Add 10 new UI actions + bindings**

File: `Assets/_Game/InputSystem_Actions.inputactions`

In the `"UI"` action map, inside the `"actions"` array (after `"TrackedDeviceOrientation"`), append 10 new action objects:

```json
{
    "name": "DialogueOption1",
    "type": "Button",
    "id": "d1a10001-d1a1-d1a1-d1a1-d1a1d1a1d1a1",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption2",
    "type": "Button",
    "id": "d2a20002-d2a2-d2a2-d2a2-d2a2d2a2d2a2",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption3",
    "type": "Button",
    "id": "d3a30003-d3a3-d3a3-d3a3-d3a3d3a3d3a3",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption4",
    "type": "Button",
    "id": "d4a40004-d4a4-d4a4-d4a4-d4a4d4a4d4a4",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption5",
    "type": "Button",
    "id": "d5a50005-d5a5-d5a5-d5a5-d5a5d5a5d5a5",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption6",
    "type": "Button",
    "id": "d6a60006-d6a6-d6a6-d6a6-d6a6d6a6d6a6",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption7",
    "type": "Button",
    "id": "d7a70007-d7a7-d7a7-d7a7-d7a7d7a7d7a7",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption8",
    "type": "Button",
    "id": "d8a80008-d8a8-d8a8-d8a8-d8a8d8a8d8a8",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption9",
    "type": "Button",
    "id": "d9a90009-d9a9-d9a9-d9a9-d9a9d9a9d9a9",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
},
{
    "name": "DialogueOption0",
    "type": "Button",
    "id": "d0a00000-d0a0-d0a0-d0a0-d0a0d0a0d0a0",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```

In the `"UI"` action map, inside the `"bindings"` array (at the end, before the closing `]`), append 10 new binding objects:

```json
{
    "name": "",
    "id": "d1b10001-d1b1-d1b1-d1b1-d1b1d1b1d1b1",
    "path": "<Keyboard>/1",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption1",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d2b20002-d2b2-d2b2-d2b2-d2b2d2b2d2b2",
    "path": "<Keyboard>/2",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption2",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d3b30003-d3b3-d3b3-d3b3-d3b3d3b3d3b3",
    "path": "<Keyboard>/3",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption3",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d4b40004-d4b4-d4b4-d4b4-d4b4d4b4d4b4",
    "path": "<Keyboard>/4",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption4",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d5b50005-d5b5-d5b5-d5b5-d5b5d5b5d5b5",
    "path": "<Keyboard>/5",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption5",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d6b60006-d6b6-d6b6-d6b6-d6b6d6b6d6b6",
    "path": "<Keyboard>/6",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption6",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d7b70007-d7b7-d7b7-d7b7-d7b7d7b7d7b7",
    "path": "<Keyboard>/7",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption7",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d8b80008-d8b8-d8b8-d8b8-d8b8d8b8d8b8",
    "path": "<Keyboard>/8",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption8",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d9b90009-d9b9-d9b9-d9b9-d9b9d9b9d9b9",
    "path": "<Keyboard>/9",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption9",
    "isComposite": false,
    "isPartOfComposite": false
},
{
    "name": "",
    "id": "d0b00000-d0b0-d0b0-d0b0-d0b0d0b0d0b0",
    "path": "<Keyboard>/0",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "DialogueOption0",
    "isComposite": false,
    "isPartOfComposite": false
}
```

---

**Task 2 — `InputSystem_Actions.cs`: Mirror Task 1 changes + add C# infrastructure**

File: `Assets/_Game/InputSystem_Actions.cs`

This file uses `""` double-escaped quotes inside embedded string literals.

**2a. Embedded JSON — actions array (after `TrackedDeviceOrientation` action, before closing `}`)**

Append the same 10 action objects as in Task 1 but with `""` escaping (e.g. `""name""`, `""Button""`, etc.).

**2b. Embedded JSON — bindings array (at the end of the UI bindings, before `]`)**

Append the same 10 binding objects with `""` escaping.

**2c. Private fields (after `m_UI_TrackedDeviceOrientation` field declaration, ~line 1708)**

Add:
```csharp
private readonly InputAction m_UI_DialogueOption1;
private readonly InputAction m_UI_DialogueOption2;
private readonly InputAction m_UI_DialogueOption3;
private readonly InputAction m_UI_DialogueOption4;
private readonly InputAction m_UI_DialogueOption5;
private readonly InputAction m_UI_DialogueOption6;
private readonly InputAction m_UI_DialogueOption7;
private readonly InputAction m_UI_DialogueOption8;
private readonly InputAction m_UI_DialogueOption9;
private readonly InputAction m_UI_DialogueOption0;
```

**2d. FindAction calls (after `m_UI_TrackedDeviceOrientation = m_UI.FindAction(...)`, ~line 1397)**

Add:
```csharp
m_UI_DialogueOption1 = m_UI.FindAction("DialogueOption1", throwIfNotFound: true);
m_UI_DialogueOption2 = m_UI.FindAction("DialogueOption2", throwIfNotFound: true);
m_UI_DialogueOption3 = m_UI.FindAction("DialogueOption3", throwIfNotFound: true);
m_UI_DialogueOption4 = m_UI.FindAction("DialogueOption4", throwIfNotFound: true);
m_UI_DialogueOption5 = m_UI.FindAction("DialogueOption5", throwIfNotFound: true);
m_UI_DialogueOption6 = m_UI.FindAction("DialogueOption6", throwIfNotFound: true);
m_UI_DialogueOption7 = m_UI.FindAction("DialogueOption7", throwIfNotFound: true);
m_UI_DialogueOption8 = m_UI.FindAction("DialogueOption8", throwIfNotFound: true);
m_UI_DialogueOption9 = m_UI.FindAction("DialogueOption9", throwIfNotFound: true);
m_UI_DialogueOption0 = m_UI.FindAction("DialogueOption0", throwIfNotFound: true);
```

**2e. UIActions struct public properties (after `TrackedDeviceOrientation` property, ~line 1759)**

Add:
```csharp
public InputAction @DialogueOption1 => m_Wrapper.m_UI_DialogueOption1;
public InputAction @DialogueOption2 => m_Wrapper.m_UI_DialogueOption2;
public InputAction @DialogueOption3 => m_Wrapper.m_UI_DialogueOption3;
public InputAction @DialogueOption4 => m_Wrapper.m_UI_DialogueOption4;
public InputAction @DialogueOption5 => m_Wrapper.m_UI_DialogueOption5;
public InputAction @DialogueOption6 => m_Wrapper.m_UI_DialogueOption6;
public InputAction @DialogueOption7 => m_Wrapper.m_UI_DialogueOption7;
public InputAction @DialogueOption8 => m_Wrapper.m_UI_DialogueOption8;
public InputAction @DialogueOption9 => m_Wrapper.m_UI_DialogueOption9;
public InputAction @DialogueOption0 => m_Wrapper.m_UI_DialogueOption0;
```

**2f. IUIActions interface (at end of interface definition block, after `OnTrackedDeviceOrientation`)**

Add:
```csharp
void OnDialogueOption1(InputAction.CallbackContext context);
void OnDialogueOption2(InputAction.CallbackContext context);
void OnDialogueOption3(InputAction.CallbackContext context);
void OnDialogueOption4(InputAction.CallbackContext context);
void OnDialogueOption5(InputAction.CallbackContext context);
void OnDialogueOption6(InputAction.CallbackContext context);
void OnDialogueOption7(InputAction.CallbackContext context);
void OnDialogueOption8(InputAction.CallbackContext context);
void OnDialogueOption9(InputAction.CallbackContext context);
void OnDialogueOption0(InputAction.CallbackContext context);
```

**2g. AddCallbacks method (after `TrackedDeviceOrientation` wiring)**

Add:
```csharp
@DialogueOption1.started += instance.OnDialogueOption1;
@DialogueOption1.performed += instance.OnDialogueOption1;
@DialogueOption1.canceled += instance.OnDialogueOption1;
// ... repeat for 2-9 and 0
```

**2h. UnregisterCallbacks method (after `TrackedDeviceOrientation` unwiring)**

Add the same with `-=` for all 10.

---

**Task 3 — `PlayerStateManager.cs`: Add `IsInDialogue` state**

File: `Assets/_Game/Scripts/Player/PlayerStateManager.cs`

After the `IsInCombat { get; private set; }` property (~line 36), add:
```csharp
/// <summary>True while the player is in an active dialogue conversation.</summary>
public bool IsInDialogue { get; private set; }
```

After `SetInCombat(bool value)` method (~line 91), add:
```csharp
/// <summary>Sets the IsInDialogue state. Called by DialogueSystem on open/close.</summary>
public void SetInDialogue(bool value)
{
    IsInDialogue = value;
    GameLog.Info(TAG, $"IsInDialogue: {value}");
}
```

---

**Task 4 — `UIScreenManager.cs`: Guard `OpenTab()` against dialogue**

File: `Assets/_Game/Scripts/UI/UIScreenManager.cs`

**4a.** Add a serialized field after the existing `[SerializeField]` fields:
```csharp
[SerializeField] private PlayerStateManager _playerStateManager;
```

Add `using Game.Player;` if not already present.

**4b.** At the top of `OpenTab(ScreenTab tab)`, before the existing `if (_activeTab == tab) return;` line, add:
```csharp
if (_playerStateManager != null && _playerStateManager.IsInDialogue) return;
```

This automatically blocks ALL tabs from opening when in dialogue, covering any future tabs too.

---

**Task 5 — `DialogueSystem.cs`: Set `IsInDialogue` on open/close**

File: `Assets/_Game/Scripts/World/DialogueSystem.cs`

**5a.** Add a serialized field:
```csharp
[SerializeField] private PlayerStateManager _playerStateManager;
```

Add `using Game.Player;` if not already present.

**5b.** In `HandleDialogueRequested`, after `IsOpen = true;`, add:
```csharp
if (_playerStateManager != null)
    _playerStateManager.SetInDialogue(true);
```

**5c.** In `Close()`, after `IsOpen = false;`, add:
```csharp
if (_playerStateManager != null)
    _playerStateManager.SetInDialogue(false);
```

---

**Task 6 — `DialogueUI.cs`: All UX changes**

File: `Assets/_Game/Scripts/UI/DialogueUI.cs`

**6a. Add slot callbacks array field** (after `_cachedStartNodes` field):
```csharp
// _slotCallbacks[1..9] = keys 1-9, [10] = key 0. Index 0 unused.
private readonly System.Action[] _slotCallbacks = new System.Action[11];
```

**6b. Update `OnEnable` — subscribe to 10 new actions:**

After `_input.UI.Cancel.performed += HandleCancel;`, add:
```csharp
_input.UI.DialogueOption1.performed += _ => InvokeSlot(1);
_input.UI.DialogueOption2.performed += _ => InvokeSlot(2);
_input.UI.DialogueOption3.performed += _ => InvokeSlot(3);
_input.UI.DialogueOption4.performed += _ => InvokeSlot(4);
_input.UI.DialogueOption5.performed += _ => InvokeSlot(5);
_input.UI.DialogueOption6.performed += _ => InvokeSlot(6);
_input.UI.DialogueOption7.performed += _ => InvokeSlot(7);
_input.UI.DialogueOption8.performed += _ => InvokeSlot(8);
_input.UI.DialogueOption9.performed += _ => InvokeSlot(9);
_input.UI.DialogueOption0.performed += _ => InvokeSlot(10);
```

**6c. Update `OnDisable` — unsubscribe:**

After `_input.UI.Cancel.performed -= HandleCancel;`, add:
```csharp
_input.UI.DialogueOption1.performed -= _ => InvokeSlot(1);
// ... etc. for 2-9 and 0
```

> **IMPORTANT:** Lambda unsubscription does NOT work with anonymous lambdas — each `+=` creates a new delegate. Instead, store 10 named handler fields or use a single registered handler approach.

**Correct pattern for 6b + 6c — use stored action references:**

Add 10 private fields:
```csharp
private InputAction.CallbackContext _dummy; // not needed, use Action fields
```

Actually, the correct Unity Input System pattern for lambda unsubscription is to store the handler as a named method or a stored delegate. Use the named method approach:

```csharp
// In OnEnable:
_input.UI.DialogueOption1.performed += HandleSlot1;
// ...
_input.UI.DialogueOption0.performed += HandleSlot10;

// In OnDisable:
_input.UI.DialogueOption1.performed -= HandleSlot1;
// ...
_input.UI.DialogueOption0.performed -= HandleSlot10;

// Private handler methods:
private void HandleSlot1(InputAction.CallbackContext _) => InvokeSlot(1);
private void HandleSlot2(InputAction.CallbackContext _) => InvokeSlot(2);
private void HandleSlot3(InputAction.CallbackContext _) => InvokeSlot(3);
private void HandleSlot4(InputAction.CallbackContext _) => InvokeSlot(4);
private void HandleSlot5(InputAction.CallbackContext _) => InvokeSlot(5);
private void HandleSlot6(InputAction.CallbackContext _) => InvokeSlot(6);
private void HandleSlot7(InputAction.CallbackContext _) => InvokeSlot(7);
private void HandleSlot8(InputAction.CallbackContext _) => InvokeSlot(8);
private void HandleSlot9(InputAction.CallbackContext _) => InvokeSlot(9);
private void HandleSlot10(InputAction.CallbackContext _) => InvokeSlot(10);
```

**6d. Add `InvokeSlot` and `SlotLabel` helpers:**

```csharp
private void InvokeSlot(int slot)
{
    if (slot >= 1 && slot <= 10 && _slotCallbacks[slot] != null)
        _slotCallbacks[slot].Invoke();
}

private static string SlotLabel(int slot) => slot == 10 ? "0" : slot.ToString();
```

**6e. Update `HandleCancel` — guard to Topics only:**

Replace:
```csharp
private void HandleCancel(InputAction.CallbackContext ctx)
{
    if (_dialogueSystem != null)
        _dialogueSystem.Close();
}
```
With:
```csharp
private void HandleCancel(InputAction.CallbackContext ctx)
{
    if (_state != DisplayState.Topics) return;
    if (_dialogueSystem != null)
        _dialogueSystem.Close();
}
```

**6f. Add new serialized fields for `ResponseWrapper` and `NextNodeButton`:**

Add two new `[SerializeField]` fields after the existing `[SerializeField] private DialogueSystem _dialogueSystem;`:
```csharp
[SerializeField] private GameObject _responseWrapper;
[SerializeField] private Button _nextNodeButton;
```

**6f2. Update `SetState` — toggle `_responseWrapper` instead of `_responseText.gameObject`:**

`_topicsScrollView` visibility is **unchanged** (`showTopics = state == DisplayState.Topics || state == DisplayState.Choices`).

Replace:
```csharp
if (_responseText != null)
    _responseText.gameObject.SetActive(showResponse);
```
With:
```csharp
if (_responseWrapper != null)
    _responseWrapper.SetActive(showResponse);
```

**6g. Update `ClearTopicButtons` — also clear slot callbacks:**

After the destroy loop, add:
```csharp
System.Array.Clear(_slotCallbacks, 0, _slotCallbacks.Length);
```

**6h. Update `RestoreTopics` — add Farewell button after topics:**

The `PopulateStartNodes` call already handles numbering (see 6i). After `PopulateStartNodes` finishes, add the Farewell button. Modify the `RestoreTopics` method to pass the current child count after populating topics to `AddFarewellButton`:

After `PopulateStartNodes(_cachedStartNodes);`, if `_cachedStartNodes` has entries, call `AddFarewellButton(_cachedStartNodes.Length + 1)`. If no topics available, Farewell = slot 1.

Actually, refactor `RestoreTopics` to:
```csharp
private void RestoreTopics()
{
    if (_responseText != null)
        _responseText.text = string.Empty;
    _pendingNextNode = null;

    if (_dialogueSystem != null)
        _cachedStartNodes = _dialogueSystem.GetCurrentStartNodes();

    ClearTopicButtons();

    int nextSlot = 1;
    if (_cachedStartNodes != null)
    {
        foreach (var node in _cachedStartNodes)
        {
            if (node == null) continue;
            AddStartNodeButton(node, nextSlot++);
        }
    }
    AddFarewellButton(nextSlot);
    SetState(DisplayState.Topics);
}
```

Remove the separate `PopulateStartNodes` call. Also remove `PopulateStartNodes` private method (it's inlined above).

**6i. Update `AddStartNodeButton` — accept slot, prefix label:**

Replace:
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
                _dialogueSystem.StartTopic(captured);
            else
                GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
        });
    }
}
```
With:
```csharp
private void AddStartNodeButton(StartDialogueNode node, int slot)
{
    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = $"{SlotLabel(slot)}. {node.text}";

    var captured = node;
    System.Action action = () =>
    {
        if (captured.nextNode != null)
            _dialogueSystem.StartTopic(captured);
        else
            GameLog.Warn(TAG, $"StartDialogueNode '{captured.text}' has no nextNode — ignoring click");
    };

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
        btn.onClick.AddListener(() => action());

    if (slot <= 10)
        _slotCallbacks[slot] = action;
}
```

**6j. Add `AddFarewellButton` method:**

```csharp
private void AddFarewellButton(int slot)
{
    if (_topicsContainer == null || _topicButtonPrefab == null) return;
    if (slot > 10) return; // no slot available

    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = $"{SlotLabel(slot)}. [Farewell]";

    System.Action action = () =>
    {
        if (_dialogueSystem != null)
            _dialogueSystem.Close();
    };

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
        btn.onClick.AddListener(() => action());

    _slotCallbacks[slot] = action;
}
```

**6k. Update `ShowChoiceNode` — pass slot indices to `AddChoiceButton`:**

Replace the `foreach` in `ShowChoiceNode`:
```csharp
ClearTopicButtons();
int slot = 1;
foreach (var choice in availableChoices)
{
    if (choice == null) continue;
    AddChoiceButton(choice, slot++);
}
```

**6l. Update `AddChoiceButton` — accept slot, prefix label:**

Replace:
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
            {
                _dialogueSystem.NotifyTopicCompleted();
                RestoreTopics();
            }
        });
    }
}
```
With:
```csharp
private void AddChoiceButton(ChoiceOption choice, int slot)
{
    var btnGO = Instantiate(_topicButtonPrefab, _topicsContainer);
    var label = btnGO.GetComponentInChildren<TMP_Text>();
    if (label != null) label.text = $"{SlotLabel(slot)}. {choice.text}";

    var captured = choice;
    System.Action action = () =>
    {
        if (captured.nextNode != null)
            _dialogueSystem.AdvanceToNode(captured.nextNode);
        else
        {
            _dialogueSystem.NotifyTopicCompleted();
            RestoreTopics();
        }
    };

    var btn = btnGO.GetComponent<Button>();
    if (btn != null)
        btn.onClick.AddListener(() => action());

    if (slot <= 10)
        _slotCallbacks[slot] = action;
}
```

**6m. Update `ShowTextNode` — wire `_nextNodeButton` and slot 1:**

After `ClearTopicButtons();` and setting `_responseText.text`, add wiring for `_nextNodeButton`:

```csharp
// Wire NextNodeButton (pre-existing button in ResponseWrapper)
System.Action nextAction = () =>
{
    if (_pendingNextNode != null)
        _dialogueSystem.AdvanceToNode(_pendingNextNode);
    else
    {
        _dialogueSystem.NotifyTopicCompleted();
        RestoreTopics();
    }
};

if (_nextNodeButton != null)
{
    _nextNodeButton.onClick.RemoveAllListeners();
    _nextNodeButton.onClick.AddListener(() => nextAction());
}
_slotCallbacks[1] = nextAction;
```

> Note: `_nextNodeButton` is always visible while `_responseWrapper` is active — no instantiation needed. The existing `IPointerClickHandler.OnPointerClick` still works in parallel for click-anywhere-to-advance.

---

### Acceptance Criteria

**AC-1: Escape guard (Topics only)**
- Given: dialogue is open, player is reading a `TextDialogueNode`
- When: player presses Escape
- Then: dialogue does NOT close; text remains visible

- Given: dialogue is open and showing the Topics list
- When: player presses Escape
- Then: dialogue closes, cursor locks, `IsInDialogue` becomes `false`

**AC-2: Number keys during Topics**
- Given: dialogue is open showing 3 topics (slots 1, 2, 3) + Farewell (slot 4)
- When: player presses `2`
- Then: topic 2 is selected, dialogue advances to its first node

- Given: dialogue is open showing 9 topics (slots 1–9) + Farewell (slot 0)
- When: player presses `0`
- Then: dialogue closes (Farewell selected)

**AC-3: Number keys during Choices**
- Given: dialogue is showing a `ChoiceDialogueNode` with 2 choices (slots 1, 2)
- When: player presses `1`
- Then: choice 1 is selected, dialogue advances to its `nextNode`

**AC-4: "1. Next" during Text**
- Given: dialogue is showing a `TextDialogueNode`
- When: player presses `1`
- Then: dialogue advances to `_pendingNextNode` (or restores Topics if end of chain)

**AC-5: Button labels**
- Given: any state with options visible
- Then: every button label is prefixed with its slot number and a period+space (e.g. `1. Greet the blacksmith`, `0. [Farewell]`)

**AC-6: Farewell slot assignment**
- Given: Topics list has N visible topics (N = 0..9)
- Then: Farewell button has slot `N+1`, displayed as `(N+1).` or `0.` when N=9

**AC-7: IsInDialogue blocks menus**
- Given: player is in an active dialogue
- When: player presses `I` (Inventory) or `C` (Character Stats)
- Then: the tab panel does NOT open; `UIScreenManager.OpenTab()` silently returns

- Given: player is NOT in dialogue
- When: player presses `I`
- Then: Inventory opens normally

**AC-8: IsInDialogue lifecycle**
- Given: player interacts with an NPC
- When: dialogue opens
- Then: `PlayerStateManager.IsInDialogue == true`
- When: dialogue closes (via Farewell, Escape at Topics, or chain completion)
- Then: `PlayerStateManager.IsInDialogue == false`

---

## Additional Context

### Dependencies

- `PlayerStateManager` reference must be wired in the Unity Inspector on the `DialogueSystem` GameObject
- `PlayerStateManager` reference must be wired in the Unity Inspector on the `UIScreenManager` GameObject
- No new ScriptableObjects, prefabs, or scenes required

### Testing Strategy

Manual playtest checklist:
1. Open dialogue → verify `IsInDialogue=true` in Inspector
2. Press `I` / `C` → verify no panel opens
3. Press Escape during text → verify no close
4. Press Escape at Topics → verify closes
5. Verify all topic button labels show `1.`, `2.`, etc.
6. Verify Farewell label matches `N+1` slot
7. Press number key → verify correct topic selected
8. In a choice node, press number key → verify correct choice
9. Press `1` during text node → verify advance
10. Complete full dialogue chain → verify `IsInDialogue=false`

### Notes

- The `PopulateStartNodes` private method can be removed after `RestoreTopics` is refactored (Task 6h inlines its logic).
- `_responseWrapper` must be wired in the Unity Inspector on the `DialogueUI` component: drag the `ResponseWrapper` GameObject into the new `_responseWrapper` slot, and drag the `NextNodeButton` Button component into the `_nextNodeButton` slot.
- `Close()` in `DialogueUI` currently calls `_responseText.text = string.Empty` — this still works since `_responseText` is still a field. `SetState(DisplayState.Topics)` called at the end of `Close()` will hide `_responseWrapper` via the updated `SetState`, so no explicit hide is needed in `Close()` itself.
- If a slot > 10 options were ever needed, the current design silently drops them. This is acceptable given no dialogue in the project has more than 9 options.
