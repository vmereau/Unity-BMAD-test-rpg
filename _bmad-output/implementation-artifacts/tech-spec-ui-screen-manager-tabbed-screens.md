---
title: 'UIScreenManager — Tabbed Screen System'
slug: 'ui-screen-manager-tabbed-screens'
created: '2026-04-02'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6.3', 'URP 17', 'Unity Input System', 'Unity UI (uGUI)']
files_to_modify:
  - Assets/_Game/Scripts/UI/InventoryUI.cs
  - Assets/_Game/InputSystem_Actions.cs
  - Assets/_Game/InputSystem_Actions.inputactions
  - Assets/_Game/Prefabs/UI/UICanvas.prefab
files_to_create:
  - Assets/_Game/Scripts/UI/UIScreenManager.cs
  - Assets/_Game/Scripts/UI/IScreenPanel.cs
  - Assets/_Game/Scripts/UI/CharacterStatsUI.cs
  - Assets/_Game/Scripts/UI/QuestLogUI.cs
  - Assets/_Game/Scripts/UI/OptionsUI.cs
code_patterns: []
test_patterns: []
---

# Tech-Spec: UIScreenManager — Tabbed Screen System

**Created:** 2026-04-02

## Overview

### Problem Statement

`InventoryUI` currently owns all screen management responsibilities: open/close logic, cursor lock/unlock (`CursorManager`), and input action subscription for the toggle key. This tightly couples screen presentation to a single feature component. Adding future screens (Quest Log, Character Stats, Options) would require duplicating this logic in each new component. There is no unified system to control which screen is visible or to switch between them.

### Solution

Extract all screen orchestration from `InventoryUI` into a new `UIScreenManager` MonoBehaviour. `UIScreenManager` owns: tab visibility, tab bar show/hide, cursor management, and input action subscriptions for toggle keys. Each screen component (Inventory, Character Stats, etc.) exposes `OnScreenOpen()` / `OnScreenClose()` via an `IScreenPanel` interface for screen-specific setup. A tab bar with one `Button` per screen appears when any screen is open and allows the player to switch tabs directly.

### Scope

**In Scope:**
- New `UIScreenManager.cs` — tab registry, `OpenTab(ScreenTab)`, `CloseAll()`, cursor, input
- New `IScreenPanel.cs` interface — `OnScreenOpen()` / `OnScreenClose()`
- Tab bar: `GameObject` with horizontal layout group + one `Button` per screen tab, visible only when a screen is open
- Placeholder panels for: Quest Log, Character Stats, Options (simple grey panel + label)
- New input action `CharacterStatsToggle` bound to `<Keyboard>/c` in both `InputSystem_Actions.cs` (embedded JSON) and `InputSystem_Actions.inputactions`
- `InventoryUI` refactored: remove open/close/cursor/toggle-key logic; implement `IScreenPanel`; keep all inventory-specific logic (slot refresh, context menu, drag-drop, equipment events)
- "I" opens/toggles Inventory tab; "C" opens/toggles Character Stats tab; Escape closes any open screen
- Pressing the same key as the active tab closes the screen; pressing a different screen key while open switches to that tab

**Out of Scope:**
- Quest Log, Character Stats, Options actual gameplay implementation
- Tab transition animations
- Saving/restoring last-open tab across sessions
- Per-tab keybindings beyond "I" and "C"

---

## Context for Development

### Codebase Patterns

- **Input System:** All input via `InputSystem_Actions` (embedded JSON + `.inputactions`). Both files MUST be updated when adding new actions. Subscribe in `OnEnable`, unsubscribe in `OnDisable`, dispose in `OnDestroy`.
- **Cursor:** Always use `CursorManager.Lock()` / `CursorManager.Unlock()` — never `Cursor.lockState` directly.
- **Logging:** Use `GameLog.Info(TAG, ...)` — never `Debug.Log`.
- **Canvas:** The `UICanvas` is Screen Space Overlay (renderMode = 0) with a `GraphicRaycaster`. HUD elements and screen panels share the same canvas but should be in separate child containers.
- **OnDisable null guard:** Mandatory when `_input` is initialized in `Awake` but `OnDisable` may fire before `OnEnable` if disabled during startup.
- **GameEventSO:** `InventoryUI` keeps its subscriptions to `_onActionBarUsed` (GameEventSO_Int) and `_onEquipmentChanged` (GameEventSO_Void) — these are inventory-specific and stay in `InventoryUI`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/InventoryUI.cs` | Current screen owner — source of logic to extract |
| `Assets/_Game/Scripts/Core/CursorManager.cs` | Static cursor API |
| `Assets/_Game/InputSystem_Actions.cs` | Embedded JSON + generated action map — must edit for new action |
| `Assets/_Game/InputSystem_Actions.inputactions` | Unity Editor input action asset — must edit in sync |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | Main UI canvas prefab to update with new hierarchy |
| `Assets/_Game/Scripts/UI/CLAUDE.md` | Canvas, cursor, input, and UI code review rules |
| `Assets/_Game/CLAUDE.md` | InputSystem_Actions dual-file contract |

### Technical Decisions

- **`IScreenPanel` interface** — decouples `UIScreenManager` from concrete screen types. Each tab panel GameObject has a component implementing `IScreenPanel`. Placeholders use trivial implementations.
- **`ScreenTab` enum** — integer-indexable enum values (0,1,2,3) used to index `_tabPanelRoots[]` and `_tabButtons[]` arrays in `UIScreenManager`, avoiding a dictionary and keeping serialization simple.
- **`UIScreenManager` holds all visibility** — `_tabPanelRoots[i].SetActive(...)` is only ever called from `UIScreenManager`. `InventoryUI` never calls `_panelRoot.SetActive` directly after this refactor.
- **`_isOpen` moved to `UIScreenManager`** — `UIScreenManager` tracks `_activeTab` (nullable `ScreenTab?`). Null = no screen open.
- **Player action gating is automatic** — `PlayerStateManager.IsBusy` is `!CursorManager.IsLocked`. Calling `CursorManager.Unlock()` in `UIScreenManager.OpenTab()` is sufficient to gate all player actions (Attack, Block, Dodge, Move, Jump). No explicit `_input.Player.Disable()` needed.
- **CameraController has no Escape handler** — `CameraController.LateUpdate()` simply returns early when `!CursorManager.IsLocked`. No conflict with `UIScreenManager`'s `UI.Cancel` subscription.
- **Player map layout** — `CharacterStatsToggle` does not yet exist. It must be added after `DrawWeapon` in both files. Verified: `<Keyboard>/c` is unbound.
- **Tab bar as child of UICanvas** — positioned at the top of the screen, hidden by default, `SetActive(true)` only when `_activeTab != null`.
- **`InventoryUI` input cleanup** — `_input`, `HandleToggle`, `HandleClose`, `Open()`, `Close()` are removed. `Awake` keeps `AnyButtonClickListener` setup. `OnEnable`/`OnDisable` keep only `_onActionBarUsed` and `_onEquipmentChanged` event subscriptions.

---

## Implementation Plan

### Tasks

*(Ordered by dependency — implement top to bottom)*

#### Task 1 — Add `CharacterStatsToggle` input action

**Files:** `Assets/_Game/InputSystem_Actions.inputactions`, `Assets/_Game/InputSystem_Actions.cs`

**1a. `InputSystem_Actions.inputactions`** — add action entry in the `Player` action map's `actions` array, after `DrawWeapon`:
```json
{
    "name": "CharacterStatsToggle",
    "type": "Button",
    "id": "cc01cc01-cc01-cc01-cc01-cc01cc01cc01",
    "expectedControlType": "Button",
    "processors": "",
    "interactions": "",
    "initialStateCheck": false
}
```
Add binding entry in the `Player` action map's `bindings` array, after the `DrawWeapon` binding:
```json
{
    "name": "",
    "id": "cc02cc02-cc02-cc02-cc02-cc02cc02cc02",
    "path": "<Keyboard>/c",
    "interactions": "",
    "processors": "",
    "groups": "Keyboard&Mouse",
    "action": "CharacterStatsToggle",
    "isComposite": false,
    "isPartOfComposite": false
}
```

**1b. `InputSystem_Actions.cs` — embedded JSON** (uses `""` double-escaped quotes). Add action after `DrawWeapon` action entry in the `Player` map `actions` array:
```
                {
                    ""name"": ""CharacterStatsToggle"",
                    ""type"": ""Button"",
                    ""id"": ""cc01cc01-cc01-cc01-cc01-cc01cc01cc01"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
```
Add binding after the `DrawWeapon` binding entry in the `bindings` array:
```
                {
                    ""name"": """",
                    ""id"": ""cc02cc02-cc02-cc02-cc02-cc02cc02cc02"",
                    ""path"": ""<Keyboard>/c"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""CharacterStatsToggle"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
```

**1c. `InputSystem_Actions.cs` — C# wiring.** In the constructor, after `m_Player_DrawWeapon = ...`:
```csharp
m_Player_CharacterStatsToggle = m_Player.FindAction("CharacterStatsToggle", throwIfNotFound: true);
```
In the private fields block, after `m_Player_DrawWeapon`:
```csharp
private readonly InputAction m_Player_CharacterStatsToggle;
```
In the `PlayerActions` struct, after the `DrawWeapon` property:
```csharp
/// <summary>Provides access to the underlying input action "Player/CharacterStatsToggle".</summary>
public InputAction @CharacterStatsToggle => m_Wrapper.m_Player_CharacterStatsToggle;
```

---

#### Task 2 — Create `IScreenPanel` interface

**File:** `Assets/_Game/Scripts/UI/IScreenPanel.cs` (new file)

```csharp
namespace Game.UI
{
    public interface IScreenPanel
    {
        void OnScreenOpen();
        void OnScreenClose();
    }
}
```

---

#### Task 3 — Create `UIScreenManager.cs`

**File:** `Assets/_Game/Scripts/UI/UIScreenManager.cs` (new file)

Full implementation:

```csharp
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.UI
{
    public enum ScreenTab { Inventory = 0, QuestLog = 1, CharacterStats = 2, Options = 3 }

    public class UIScreenManager : MonoBehaviour
    {
        private const string TAG = "[UIScreenManager]";

        [SerializeField] private GameObject _tabBar;
        [SerializeField] private GameObject[] _tabPanelRoots; // indexed by ScreenTab
        [SerializeField] private Button[] _tabButtons;        // indexed by ScreenTab

        private InputSystem_Actions _input;
        private ScreenTab? _activeTab = null;

        private void Awake()
        {
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _input.UI.Enable();
        }

        private void OnEnable()
        {
            _input.Player.InventoryToggle.performed += HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed += HandleCharacterStatsToggle;
            _input.UI.Cancel.performed += HandleCancel;
            WireTabButtons();
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.Player.InventoryToggle.performed -= HandleInventoryToggle;
            _input.Player.CharacterStatsToggle.performed -= HandleCharacterStatsToggle;
            _input.UI.Cancel.performed -= HandleCancel;
        }

        private void OnDestroy()
        {
            _input?.Dispose();
        }

        private void WireTabButtons()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int tabIndex = i; // capture for closure
                _tabButtons[i].onClick.RemoveAllListeners();
                _tabButtons[i].onClick.AddListener(() => OnTabButtonClicked((ScreenTab)tabIndex));
            }
        }

        private void OnTabButtonClicked(ScreenTab tab)
        {
            if (_activeTab == tab)
                CloseAll();
            else
                OpenTab(tab);
        }

        public void OpenTab(ScreenTab tab)
        {
            // Close current tab content if switching
            if (_activeTab.HasValue && _activeTab.Value != tab)
                CloseTabContent(_activeTab.Value);

            _activeTab = tab;

            // Show tab bar
            _tabBar.SetActive(true);

            // Show requested panel
            int idx = (int)tab;
            if (idx < _tabPanelRoots.Length)
            {
                _tabPanelRoots[idx].SetActive(true);
                var panel = _tabPanelRoots[idx].GetComponent<IScreenPanel>();
                panel?.OnScreenOpen();
            }

            // Update tab button states
            UpdateTabButtonStates();

            CursorManager.Unlock();
            GameLog.Info(TAG, $"Opened tab: {tab}");
        }

        public void CloseAll()
        {
            if (!_activeTab.HasValue) return;

            CloseTabContent(_activeTab.Value);
            _activeTab = null;
            _tabBar.SetActive(false);

            CursorManager.Lock();
            GameLog.Info(TAG, "All screens closed");
        }

        private void CloseTabContent(ScreenTab tab)
        {
            int idx = (int)tab;
            if (idx < _tabPanelRoots.Length)
            {
                var panel = _tabPanelRoots[idx].GetComponent<IScreenPanel>();
                panel?.OnScreenClose();
                _tabPanelRoots[idx].SetActive(false);
            }
        }

        private void UpdateTabButtonStates()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                // Visual feedback: interactable=false on the active tab button
                _tabButtons[i].interactable = !(_activeTab.HasValue && (int)_activeTab.Value == i);
            }
        }

        private void HandleInventoryToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.Inventory)
                CloseAll();
            else
                OpenTab(ScreenTab.Inventory);
        }

        private void HandleCharacterStatsToggle(InputAction.CallbackContext ctx)
        {
            if (_activeTab == ScreenTab.CharacterStats)
                CloseAll();
            else
                OpenTab(ScreenTab.CharacterStats);
        }

        private void HandleCancel(InputAction.CallbackContext ctx)
        {
            if (_activeTab.HasValue)
                CloseAll();
        }
    }
}
```

---

#### Task 4 — Create placeholder screen components

**Files:**
- `Assets/_Game/Scripts/UI/CharacterStatsUI.cs` (new)
- `Assets/_Game/Scripts/UI/QuestLogUI.cs` (new)
- `Assets/_Game/Scripts/UI/OptionsUI.cs` (new)

All three follow the same trivial pattern (shown for `CharacterStatsUI`):

```csharp
using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public class CharacterStatsUI : MonoBehaviour, IScreenPanel
    {
        private const string TAG = "[CharacterStatsUI]";

        public void OnScreenOpen()
        {
            GameLog.Info(TAG, "Character Stats opened (placeholder)");
        }

        public void OnScreenClose()
        {
            GameLog.Info(TAG, "Character Stats closed (placeholder)");
        }
    }
}
```

Apply the same pattern for `QuestLogUI` and `OptionsUI` with their respective TAG values.

---

#### Task 5 — Refactor `InventoryUI.cs`

**File:** `Assets/_Game/Scripts/UI/InventoryUI.cs`

**Remove:**
- `private bool _isOpen;` field
- `private InputSystem_Actions _input;` field
- `_input = new InputSystem_Actions(); _input.Player.Enable(); _input.UI.Enable();` from `Awake()`
- `_input.Player.InventoryToggle.performed += HandleToggle;` from `OnEnable`
- `_input.UI.Cancel.performed += HandleClose;` from `OnEnable`
- `_input.Player.InventoryToggle.performed -= HandleToggle;` from `OnDisable`
- `_input.UI.Cancel.performed -= HandleClose;` from `OnDisable`
- `OnDestroy()` method entirely (no more `_input?.Dispose()`)
- `HandleToggle(InputAction.CallbackContext ctx)` method
- `HandleClose(InputAction.CallbackContext ctx)` method
- `private void Open()` method (moved to UIScreenManager via `IScreenPanel.OnScreenOpen`)
- `private void Close()` method (moved to UIScreenManager via `IScreenPanel.OnScreenClose`)

**Add:** `IScreenPanel` interface on the class declaration:
```csharp
public class InventoryUI : MonoBehaviour, IScreenPanel
```

**Add:** `OnScreenOpen()` and `OnScreenClose()` methods:
```csharp
public void OnScreenOpen()
{
    RefreshSlots();
    _equipmentUI?.Refresh();
    _equipmentUI?.gameObject.SetActive(true);
    GameLog.Info(TAG, "Inventory opened");
}

public void OnScreenClose()
{
    HideContextMenu();
    ClearSelection();
    _equipmentUI?.gameObject.SetActive(false);
    GameLog.Info(TAG, "Inventory closed");
}
```

**Update `OnDisable` null guard** — remove the `_input == null` guard (no more `_input`); null guard is still needed for `_onActionBarUsed` / `_onEquipmentChanged` if those could be unassigned. Since they are `[SerializeField]` and assigned in editor, the guard is only needed per the existing `?.` pattern which is already in place. The `OnDisable` becomes:
```csharp
private void OnDisable()
{
    _onActionBarUsed?.RemoveListener(HandleActionBarUsed);
    _onEquipmentChanged?.RemoveListener(HandleEquipmentSlotsChange);
}
```

**Remove unused `using` directives:** `UnityEngine.InputSystem` (no longer needed after removing input logic).

**Keep unchanged:** `_panelRoot`, `_canvas`, `_contentRoot`, `_itemSlotPrefab`, `_contextMenuPrefab`, `_detailPanelUI`, `_actionBarUI`, `_onActionBarUsed`, `_equipmentSystem`, `_equipmentUI`, `_onEquipmentChanged`, `_playerTransform`, all inventory slot/context menu/drag logic, `Awake()` (AnyButtonClickListener only), `OnEnable`/`OnDisable` for event subscriptions.

> **Note:** `_panelRoot.SetActive(true/false)` is no longer called from `InventoryUI`. The panel root's visibility is controlled entirely by `UIScreenManager` via `_tabPanelRoots[(int)ScreenTab.Inventory].SetActive(...)`. Ensure `_panelRoot` IS the same GameObject registered as the Inventory tab panel root in `UIScreenManager`'s `_tabPanelRoots[0]`.

---

#### Task 6 — Update UICanvas prefab hierarchy

Using Unity MCP tools:

**6a. Add Tab Bar container:**
- Create `GameObject` named `TabBar` as child of `UICanvas/ScreensRoot` (or top-level under `UICanvas`)
- Add `HorizontalLayoutGroup` component: `spacing=8`, `childForceExpandWidth=false`, `childForceExpandHeight=false`, `padding=8`
- Add `ContentSizeFitter`: `horizontalFit=PreferredSize`
- Anchor: stretched horizontally at top of canvas (anchorMin=(0,1), anchorMax=(1,1), pivot=(0.5,1))
- Set `active=false` by default

**6b. Add tab buttons inside `TabBar`** — 4 buttons: "Inventory", "Quest Log", "Character Stats", "Options"
- Each button: `Button` + `Image` (background) + `TMP_Text` child with the tab name
- Width=120, Height=36 per button

**6c. Add placeholder panel roots** for Quest Log, Character Stats, Options as children of `UICanvas`:
- Name: `QuestLogPanel`, `CharacterStatsPanel`, `OptionsPanel`
- Simple `Image` background (dark semi-transparent) filling most of the screen
- `TMP_Text` child with placeholder label ("Quest Log — Coming Soon", etc.)
- Each has the corresponding `QuestLogUI`, `CharacterStatsUI`, `OptionsUI` component attached
- Set `active=false` by default

**6d. Add `UIScreenManager` component** to the `UICanvas` root GameObject (or a dedicated `ScreenManager` child GO):
- Wire `_tabBar` → `TabBar` GameObject
- Wire `_tabPanelRoots[0]` → existing InventoryUI panel root (currently `_panelRoot` in InventoryUI)
- Wire `_tabPanelRoots[1]` → `QuestLogPanel`
- Wire `_tabPanelRoots[2]` → `CharacterStatsPanel`
- Wire `_tabPanelRoots[3]` → `OptionsPanel`
- Wire `_tabButtons[0..3]` → the 4 tab buttons in `TabBar`

**6e. Remove input wiring from `InventoryUI`** on the prefab — the `_input` field serialization is gone; confirm no orphaned SerializedField references remain.

---

### Acceptance Criteria

**AC-1: Tab system — single visible screen**
- Given any screen is open, when the player opens a different tab (via button or hotkey), then only the new tab's panel is visible; the previous panel is hidden and `OnScreenClose` was called on it.

**AC-2: Inventory toggle — "I" key**
- Given no screen is open, when the player presses "I", then the Inventory tab opens (cursor unlocked, tab bar visible, inventory panel active, `InventoryUI.OnScreenOpen()` called).
- Given the Inventory tab is open, when the player presses "I" again, then all screens close (cursor locked, tab bar hidden).

**AC-3: CharacterStats toggle — "C" key**
- Given no screen is open, when the player presses "C", then the CharacterStats tab opens (cursor unlocked, tab bar visible, `CharacterStatsUI.OnScreenOpen()` called, placeholder visible).
- Given the CharacterStats tab is open, when the player presses "C" again, then all screens close.

**AC-4: Escape closes any screen**
- Given any screen is open, when the player presses Escape (UI.Cancel), then all screens close (cursor locked, tab bar hidden).

**AC-5: Tab bar visible only when a screen is open**
- Given no screen is open, the tab bar `GameObject` is inactive.
- Given any screen is open, the tab bar is active.

**AC-6: Tab button active state**
- Given a screen is open, the active tab's button has `interactable=false`; all other tab buttons have `interactable=true`.

**AC-7: Cross-tab navigation via buttons**
- Given the Inventory screen is open, when the player clicks "Character Stats" in the tab bar, then `InventoryUI.OnScreenClose()` fires, the inventory panel hides, the CharacterStats panel shows, and `CharacterStatsUI.OnScreenOpen()` fires.

**AC-8: Inventory-specific logic preserved**
- Given the Inventory screen is open, inventory slot refresh, context menu, equipment panel, and drag-drop all work identically to before.
- Given `_onActionBarUsed` fires while Inventory is open, slots refresh without closing the screen.

**AC-9: Cursor management**
- Any screen open → `CursorManager.IsLocked == false`.
- No screen open → `CursorManager.IsLocked == true`.
- No direct `Cursor.lockState` calls anywhere in `UIScreenManager` or `InventoryUI`.

**AC-10: No compilation errors**
- After all changes, Unity console has no compilation errors related to modified files.

---

## Additional Context

### Dependencies

- `CursorManager` (`Assets/_Game/Scripts/Core/CursorManager.cs`) — static API, no change required
- `InputSystem_Actions` — requires dual-file update (Task 1); domain reload required before testing
- `InventoryUI` must be updated (Task 5) before the UICanvas prefab wiring (Task 6), otherwise `IScreenPanel` interface mismatch

### Testing Strategy

1. Enter Play Mode in the SampleScene
2. Verify "I" opens Inventory, "I" again closes it
3. Verify "C" opens CharacterStats placeholder, "C" again closes it
4. Verify Escape closes any open screen
5. With Inventory open, click "Character Stats" tab button — verify smooth switch
6. Verify cursor is free while any screen is open, locked when closed
7. Verify inventory functionality: open/close item context menu, equip/drop items, action bar binding

### Notes

- `AnyButtonClickListener` helper class at the bottom of `InventoryUI.cs` is unchanged and stays in that file (internal class).
- `InventoryUI._panelRoot` is now set active/inactive exclusively by `UIScreenManager`. Do NOT add any `_panelRoot.SetActive(...)` calls back into `InventoryUI`.
- If `UICanvas.prefab` already has `EquipmentUI` as a separate panel (not a child of `_panelRoot`), `InventoryUI.OnScreenClose()` must also call `_equipmentUI?.gameObject.SetActive(false)` — this is already included in Task 5.
- The `_tabButtons` array in UIScreenManager must be indexed in the EXACT same order as `ScreenTab` enum values (0=Inventory, 1=QuestLog, 2=CharacterStats, 3=Options).
