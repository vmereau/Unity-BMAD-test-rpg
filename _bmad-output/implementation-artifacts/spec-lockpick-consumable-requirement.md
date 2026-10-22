---
title: 'Lockpicking requires a lockpick item (consumed on success)'
type: 'feature'
created: '2026-10-22'
status: 'done'
baseline_commit: '355d20e2a04c93e1f8587d997b33e0c4e75c57d8'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Locked doors and chests only check the required skill. The new `Item_Lockpick` plays no part, and a failed attempt shows no feedback.

**Approach:** Any locked `Lockable` needs both the required skill and at least one lockpick in the player's inventory. A successful pick consumes one lockpick. A failed attempt shows a `NotificationToastUI` toast. When both are missing, only the skill toast is shown.

## Boundaries & Constraints

**Always:** Gate order is skill first, then lockpick. Consume exactly one lockpick per successful unlock, only after both checks pass. Door and container use the same pure rule. The lock check stays in the player-side resolvers (`DoorSystem`, `ContainerSystem`). Interactables never touch inventory or skills. Toast text: "You lack the skill to pick this lock" / "You need a lockpick".

**Ask First:** Changing `Lockable` serialized fields or the event payload structs (`DoorOpenRequestData`, `ContainerOpenRequestData`).

**Never:** Randomly breaking lockpicks or a lockpicking minigame. Free unlocks for locked `Lockable`s with no skill set. Touching the unrelated dirty `Core.unity` / `packages-lock.json`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Success | locked, has skill, ≥1 lockpick | unlock + open, 1 lockpick removed, "X unlocked!" toast | N/A |
| No lockpick | locked, has skill, 0 lockpicks | stays locked, toast "You need a lockpick" | N/A |
| No skill | locked, no skill, ≥1 lockpick | stays locked, toast "You lack the skill to pick this lock", nothing consumed | N/A |
| Neither | locked, no skill, 0 lockpicks | stays locked, only the skill toast | N/A |
| Misconfigured lock | locked, `_requiredSkill` null | skill check counts as passed, lockpick still required | `Lockable.Awake` logs a warning (lock must define a skill) |
| Last lockpick | exactly 1 lockpick, success | slot removed from inventory | N/A |
| Unlocked / no Lockable | — | behavior unchanged, no lockpick used | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/Lockable.cs` -- lock data. Add an Awake warning for locked + null skill.
- `Assets/_Game/Scripts/World/DoorSystem.cs` -- player-side door resolver (skill gate today).
- `Assets/_Game/Scripts/World/ContainerSystem.cs` -- player-side chest resolver (skill gate today).
- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- stacked slots. Has `DecrementStack(index)`, no item-based count/consume.
- `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- toast HUD, subscribes to `GameEventSO_String` channels.
- `Assets/_Game/Prefabs/Player/Player.prefab` -- hosts `DoorSystem`, `ContainerSystem`, player `InventorySystem`.
- `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- hosts `NotificationToastUI`.
- `Assets/_Game/Data/Items/Item_Lockpick.asset` -- the lockpick `ItemSO` (stackable, max 50).

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/World/LockpickRules.cs` -- new static pure class: `enum LockpickResult { Success, MissingSkill, MissingLockpick }` + `Evaluate(bool hasSkill, int lockpickCount)` + `FailureMessage(LockpickResult)` -- one rule shared by both resolvers, EditMode-testable.
- [x] `Assets/_Game/Scripts/Inventory/InventorySystem.cs` -- add `CountOf(ItemSO)` (sum across slots) and `TryConsume(ItemSO)` (DecrementStack on the first matching slot, false if none) -- item-based queries for consumables.
- [x] `Assets/_Game/Scripts/World/DoorSystem.cs`, `ContainerSystem.cs` -- add `[SerializeField] InventorySystem _playerInventory`, `ItemSO _lockpickItem`, `GameEventSO_String _onLockpickFailed`. Apply the rule to every locked request (empty skill id → hasSkill = true). On failure, raise the toast message and return. On success, `TryConsume` first, then the existing unlock/open path. Null-guard the new dependencies with a warning (missing inventory or item → deny unlock).
- [x] `Assets/_Game/Scripts/World/Lockable.cs` -- `Awake` warns when `_isLocked && _requiredSkill == null`; update the class summary (no longer "no consumable").
- [x] `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs` -- new `_onLockpickFailed` channel, subscribed to `Show` (same pattern as `_onSaveNotification`, incl. the Awake warning).
- [x] `Assets/_Game/Data/Events/OnLockpickFailed.asset` -- new `GameEventSO_String` asset (create via MCP).
- [x] `Player.prefab`, `UICanvas.prefab` -- wire the new fields on `DoorSystem`/`ContainerSystem` (player `InventorySystem`, `Item_Lockpick`, `OnLockpickFailed`) and `NotificationToastUI` (`OnLockpickFailed`).
- [x] `Assets/Tests/EditMode/LockpickRulesTests.cs` + `InventorySystemTests.cs` -- cover the four rule outcomes, the message mapping, and CountOf/TryConsume across multiple stacks, the last item and a missing item.
- [x] `Assets/_Game/Scripts/World/CLAUDE.md` -- update the `Lockable` / lock-pattern rows (lockpick requirement, `LockpickRules`).

**Acceptance Criteria:**
- Given the player has the skill and 3 lockpicks, when they open a locked chest, then the chest opens, the inventory shows 2 lockpicks, and reopening it later costs nothing (it stays unlocked).
- Given an unlocked door, when the player toggles it, then no lockpick is consumed and no toast appears.

## Design Notes

`ContainerSystem` and `DoorSystem` currently skip the gate when `requiredSkillId` is empty. That bypass is removed: `isLocked` alone triggers the rule. Consume before `Unlock()` so a failed consume (race/misconfig) never opens the lock for free.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass, incl. new `LockpickRulesTests` and the extended `InventorySystemTests`.
- MCP `read_console` -- expected: no compile errors.

**Manual checks:**
- Play mode: try a locked door with and without the skill or lockpicks. Check the toasts and the lockpick count in the inventory.

## Suggested Review Order

**Lockpick rule**

- Entry point: the single pure rule, where the skill check comes first, so missing both reports only the skill.
  [`LockpickRules.cs:20`](../../Assets/_Game/Scripts/World/LockpickRules.cs#L20)

- Toast text for each failure result.
  [`LockpickRules.cs:28`](../../Assets/_Game/Scripts/World/LockpickRules.cs#L28)

**Resolvers (door / chest)**

- Every locked request now goes through the gate; the old empty-skill bypass is gone.
  [`DoorSystem.cs:47`](../../Assets/_Game/Scripts/World/DoorSystem.cs#L47)

- An empty skill id counts as having the skill, so only the lockpick is needed.
  [`DoorSystem.cs:57`](../../Assets/_Game/Scripts/World/DoorSystem.cs#L57)

- Consume before unlocking, so a failed consume never opens a lock for free.
  [`DoorSystem.cs:66`](../../Assets/_Game/Scripts/World/DoorSystem.cs#L66)

- Same gate on containers, plus a null-container guard before consuming.
  [`ContainerSystem.cs:47`](../../Assets/_Game/Scripts/World/ContainerSystem.cs#L47)

**Inventory consumption**

- Item-based count across stacks.
  [`InventorySystem.cs:135`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L135)

- Consumes from the first matching slot through DecrementStack.
  [`InventorySystem.cs:148`](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L148)

- Review patch: re-sync action-bar indices before use, since consuming can shift slots.
  [`ActionBarSystem.cs:182`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L182)

**Feedback and config**

- Failure messages are shown verbatim as toasts.
  [`NotificationToastUI.cs:86`](../../Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs#L86)

- Warns when a lock is locked but has no skill.
  [`Lockable.cs:26`](../../Assets/_Game/Scripts/World/Lockable.cs#L26)

**Tests**

- The four rule outcomes and the message mapping.
  [`LockpickRulesTests.cs:1`](../../Assets/Tests/EditMode/LockpickRulesTests.cs#L1)

- CountOf / TryConsume: across stacks, the last unit, and a missing or null item.
  [`InventorySystemTests.cs:432`](../../Assets/Tests/EditMode/InventorySystemTests.cs#L432)
