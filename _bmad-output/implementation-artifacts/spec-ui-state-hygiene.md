---
title: 'UI state hygiene: tab gate, inventory-changed event, selection, drag ghost, toast dedup'
type: 'bugfix'
created: '2026-10-28'
status: 'done'
baseline_commit: 'db2cf2c'
context: ['{project-root}/_bmad-output/project-context.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Six deferred UI state bugs: I/J/K/P opens a character tab over a world screen (container, cooking, sleep, trade) and closing it re-locks the cursor under that screen; `InventorySystem` raises no change event, so the action bar (and the open inventory's grid / detail panel) goes stale when a lockpick or an action-bar item is consumed; inventory selection stays index-based, so using the last unit selects whatever item shifted into the slot; a drag ghost leaks when its slot is disabled mid-drag; repeated identical toasts (locked-door spam) flood the 5-toast stack.

**Approach:** Gate `UIScreenManager.OpenTab` on "no tab open and cursor already unlocked = another screen owns it → ignore". Add an optional per-instance `OnPlayerInventoryChanged` (`GameEventSO_Void`) on `InventorySystem`, wired only on the player, consumed by `ActionBarUI` and the open `InventoryUI`. Make selection follow its item. `OnDisable → RemoveGhostImage()` on both slot types. Merge a toast whose text equals the newest visible toast by restarting that toast's hold.

## Boundaries & Constraints

**Always:** User decisions: tab keys over a world screen are **ignored** (world screen untouched); dedup applies to **all** toasts (identical XP gains within the hold merge); selection **follows the same item** to its new slot, cleared when the item is gone. NPC / monster `InventorySystem`s leave the channel unassigned (silent). No raise from `InventorySystem.Awake` (starting items). `InventoryUI` repaints on the event only while open. Tab switching while a tab is open keeps working.

**Ask First:** Pausing or closing world screens; changing any `InventorySystem` mutation semantics.

**Never:** Referencing `ContainerUI` / `CookingUI` / `SleepUI` / `NPCTradeUI` from `UIScreenManager`; C# events across systems; per-frame polling.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Tab over world screen | Cooking / container / sleep open, press I | Nothing happens; Esc still closes the world screen only | N/A |
| Tab switch | Inventory tab open, press J | Quest log opens | N/A |
| Lockpick consumed | Lockpick on action bar, open a locked door | Action bar count drops (slot clears at 0) immediately | N/A |
| Hotkey use, inventory open | Use action-bar food, inventory open with it selected | Grid + detail repaint (count / "First meal" row) | N/A |
| Last unit used | Selected stack of 1 used | Selection cleared, detail hidden | N/A |
| Earlier slot removed | Selected item at index 3, slot 1 consumed | Same item still selected (now index 2) | N/A |
| Mid-drag close | Drag an inventory item, press I | No ghost left on screen | N/A |
| Toast spam | E on locked door ×5 within 3 s | One "You need a lockpick" toast, hold restarted each press | N/A |
| Different toast | XP toast, then lockpick toast | Two toasts (only the newest is compared) | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs` -- `OpenTab` gate
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- mutations (`AddItem`, `RemoveItem`, `DecrementStack`, `MoveItem`, `RestoreSlots`)
- `Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs` -- refresh on the new channel
- `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` -- `RefreshSlots` (index selection, `Destroy` then `GetChild` returns old children), own redundant refreshes
- `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs`, `Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs` -- drag ghosts
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- `Show` / `RunToast`
- `Assets/_Game/Prefabs/Player/Player.prefab` (nests `UICanvas.prefab`) -- channel wiring
- `Assets/Tests/EditMode/InventorySystemTests.cs` -- add raise tests

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Data/Events/OnPlayerInventoryChanged.asset` -- create `GameEventSO_Void` -- the channel
- [x] `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- optional `[SerializeField] GameEventSO_Void _onInventoryChanged`; raise after every successful mutation (not in `Awake`) -- single change signal
- [x] `Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs` -- in `OpenTab`: `if (!_activeTab.HasValue && !CursorManager.IsLocked) return;` + doc comment -- generic world-screen gate
- [x] `Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs` -- `[SerializeField] GameEventSO_Void _onInventoryChanged` → `Refresh()` -- stale-count fix
- [x] `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` -- listen to the channel, repaint only while open (`_isOpen` set in `OnScreenOpen/Close`); drop refreshes made redundant by it; track `_selectedItem` and on refresh re-target to the first slot holding it (else clear); detach old slot children before `Destroy` so `GetChild` hits the new ones -- selection + detail fixes
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs`, `Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs` -- `OnDisable() => RemoveGhostImage();` -- ghost leak
- [x] `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- store message + `CanvasGroup` in `ActiveToast`; if the newest active toast has the same message, stop its routine, set alpha 1 and restart it without fade-in -- dedup
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` / `UICanvas.prefab` -- wire the channel on the player `InventorySystem`, `ActionBarUI`, `InventoryUI` -- wiring
- [x] `Assets/Tests/EditMode/InventorySystemTests.cs` -- channel raised by Add / Decrement / Remove / Move / Restore; not raised on a failed (null / out-of-range) call; no channel → no error
- [x] `Assets/_Game/Scripts/UI/Screens/CLAUDE.md`, `UI/HUD/CLAUDE.md`, `Inventory/CLAUDE.md` -- document the gate, channel, dedup
- [x] `_bmad-output/implementation-artifacts/deferred-work.md` -- strike the six resolved items

**Acceptance Criteria:**
- Given all EditMode tests, when run, then they pass.
- Given a play session in StartingTown, when each matrix scenario is played, then behavior matches the matrix with no console errors.

## Design Notes

The cursor gate works because every world screen (container, cooking, sleep incl. fade, trade, dialogue, Game Menu, death) unlocks the cursor while it owns the screen, and a tab is the only other unlocker. The same assumption already drives `_wasGameplayLastFrame`.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass
- MCP `read_console` (errors) -- expected: none after compile

## Suggested Review Order

**Inventory change signal**

- Single raise point after every successful mutation; silent during Awake and on NPC inventories.
  [`InventorySystem.cs:68`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L68)

- Optional per-instance channel — wired on Player.prefab only.
  [`InventorySystem.cs:38`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L38)

- Action bar now repaints on non-UI changes (lockpick consumed by doors / chests).
  [`ActionBarUI.cs:57`](../../Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs#L57)

- Open inventory repaints and closes a context menu whose index may have shifted.
  [`InventoryUI.cs:105`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L105)

- Own mutations rely on the channel; manual repaint only as an unwired fallback.
  [`InventoryUI.cs:113`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L113)

**Selection follows the item**

- Old slots unparented before Destroy so GetChild resolves the new ones.
  [`InventoryUI.cs:331`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L331)

- Same index if it still holds the item, else first slot holding it, else clear.
  [`InventoryUI.cs:371`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L371)

**Tab gate**

- No tab open + cursor unlocked → a world screen owns the cursor; ignore the tab key.
  [`UIScreenManager.cs:109`](../../Assets/_Game/Scripts/UI/Screens/UIScreenManager.cs#L109)

**Toasts**

- Identical newest toast restarts its hold instead of stacking.
  [`NotificationToastUI.cs:152`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L152)

- Per-toast unscaled hold replaces the shared WaitForSecondsRealtime (shared deadline broke restarts).
  [`NotificationToastUI.cs:204`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L204)

- Disable drops frozen toasts so dedup can't revive them.
  [`NotificationToastUI.cs:122`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L122)

**Drag ghosts**

- Ghost removed when the slot is disabled / destroyed mid-drag.
  [`ItemSlotUI.cs:32`](../../Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs#L32)
  [`ActionBarSlotUI.cs:163`](../../Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs#L163)

**Tests**

- Raise on every mutation, none on failed / same-slot calls, safe with no channel.
  [`InventorySystemTests.cs:504`](../../Assets/Tests/EditMode/InventorySystemTests.cs#L504)

