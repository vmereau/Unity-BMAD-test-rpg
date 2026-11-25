---
title: 'Action bar equip swap (weapons, shield, any equippable)'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '36d5e704e3610d4a887df94dc73cf487d010e4d6'
context:
  - '{project-root}/Assets/_Game/Scripts/Combat/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/Inventory/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An equippable item placed on the action bar does nothing when used ("not usable"). Equipping it from the
inventory removes it from the inventory, so `ValidateSlots` also clears its bar slot. Quick swaps (sword ↔ bow, adding a
shield) are impossible.

**Approach:** Using a bar slot that holds an `EquipableItemSO` equips it through `EquipmentSystem.Equip`, which replaces
whatever is in the target slot (or fills an empty one). Bar slots keep items that are equipped, and those slots are
highlighted. While the weapon is drawn, swapping a weapon or shield plays sheathe → equip → draw.

## Boundaries & Constraints

**Always:**
- Every `EquipableItemSO` qualifies (weapons, shields, armor, rings). Slot and conflict rules stay in `EquipmentRules` /
  `Equip` (a bow still pushes the shield back to the inventory).
- Using the slot of an item that is already equipped does nothing.
- Refuse the swap while the player is attacking, blocking, aiming, dodging, airborne (jump / fall), drinking or already
  drawing / sheathing. Idle, walking, running, sneaking and swimming are allowed. The existing `TryUseSlot` gates
  (dialogue, incapacitated, executing) stay. Pure gate `EvaluateCanChangeEquipment`, tested.
- The gate must not use `IsBusy`: it is true while the cursor is unlocked, so clicking a bar slot with the inventory open
  would be refused.
- Drawn stance (`IsInCombat`) and the item is a `WeaponSO` or `ShieldSO`: sheathe clip → `Equip` when the sheathe
  completes → draw clip. Through the existing `BeginWeaponSwitch` / `CompleteWeaponSwitch` flow only. If the Weapon slot
  is empty (fists drawn), skip the sheathe and equip at once, then draw.
- Sheathed, or armor / ring: equip at once with no animation.
- Inventory → Combat goes through a `GameEventSO` channel (no direct reference). Without a channel, the bar equips at once.

**Ask First:** new animator states, parameters or clips; changes to `EquipmentRules` slot logic.

**Never:** Drag from the equipment panel to the bar. A sheathe / draw sequence for armor. Changing how equipping from the
inventory screen works.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Empty slot, sheathed | bar key for a sword, no weapon equipped | sword equipped, bar slot stays and is highlighted | N/A |
| Swap, sheathed | sword equipped, bar key for bow | bow equipped, sword back in inventory; both bar slots stay, only bow highlighted | N/A |
| Swap, drawn | sword drawn, bar key for bow | sheathe clip, bow equipped at its WeaponSwap, draw clip, bow in hand | interrupt (dodge / jump / stagger) mid-sheathe → equip anyway, draw completes at once |
| Already equipped | bar key for the equipped sword | nothing | N/A |
| Busy action | attacking / blocking / aiming / dodging / airborne / switching / drinking | refused, one Info log | N/A |
| Item gone at sheathe end | swap item sold / dropped mid-sheathe | no equip, player stays sheathed | Warn |
| Shield, drawn sword | bar key for shield | sheathe → equip shield → draw, shield in hand | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/ActionBarSystem.cs` -- `TryUseSlot` / `HandleHotkeyPressed` (usable only), `ValidateSlots` (clears missing items)
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` -- `Equip(ItemSO)`, `IsEquipped`, `GetEquipped`
- `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- `EvaluateCanSwitchWeapon` pattern, `IsInCombat`, `IsSwitchingWeapon`
- `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- `OnDrawWeaponStarted`, `CompleteWeaponSwitch`, `HandleVisualsRefreshed`, switch watchdog
- `Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs`, `ActionBarSlotUI.cs` -- bar refresh, slot binding and hover colours
- `Assets/_Game/ScriptableObjects/Events/` -- `GameEventSO<T>` types (one class per file)
- `Assets/_Game/Prefabs/Player/Player.prefab` -- hosts `ActionBarSystem`, `EquipmentSystem`, `PlayerCombat`
- `Assets/Tests/EditMode/ActionBarSystemTests.cs`, `WeaponSwitchGateTests.cs` -- existing tests to extend

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Events/GameEventSO_Item.cs` + `Data/Events/OnEquipSwapRequested.asset` -- new `GameEventSO<ItemSO>` type (own file) and channel asset -- Inventory → Combat request
- [x] `Assets/_Game/Scripts/Player/PlayerStateManager.cs` -- add `CanChangeEquipment()` + pure `EvaluateCanChangeEquipment(...)` per the Always gate -- one tested gate
- [x] `Assets/_Game/Scripts/Inventory/ActionBarSystem.cs` -- `[SerializeField] EquipmentSystem _equipmentSystem`, `_onEquipSwapRequested`. `ValidateSlots` keeps a slot whose item is not in the inventory but is equipped (`InventoryIndex = -1`). `HandleHotkeyPressed` handles `EquipableItemSO`: already equipped → return. Gate refused → Info and return. Drawn and (`WeaponSO` or `ShieldSO`) and channel assigned → raise the request. Else `Equip(item)`. Then raise `_onActionBarUsed`. Add `IsSlotEquipped(int)` for the UI -- core behaviour
- [x] `Assets/_Game/Scripts/Combat/PlayerCombat.cs` -- listen to `_onEquipSwapRequested`. Re-check the gate, store `_pendingEquip`. Weapon slot empty → `Equip` then draw. Otherwise `SetInCombat(false)` + `BeginWeaponSwitch(false)` (`_pendingDraw = false`). In `CompleteWeaponSwitch`, after a sheathe with `_pendingEquip` set: clear it, equip it if still in the inventory (else Warn), then `SetInCombat(true)` + `BeginWeaponSwitch(true)`. Clear `_pendingEquip` in `OnDisable` and on death -- sheathe → equip → draw sequence
- [x] `Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs` + `ActionBarUI.cs` -- `Bind(..., bool equipped)`. Background uses `_equippedColor` when equipped, `_normalColor` otherwise, hover unchanged. Refresh the bar on `OnEquipmentChanged` -- highlight
- [x] `Player.prefab` + `UICanvas` prefab -- wire `_equipmentSystem`, `_onEquipSwapRequested` (ActionBarSystem, PlayerCombat) and `_onEquipmentChanged` (ActionBarUI) -- runtime wiring
- [x] `Assets/Tests/EditMode/ActionBarSystemTests.cs`, `WeaponSwitchGateTests.cs` -- tests: equipped item kept by `ValidateSlots`; sheathed equip replaces and returns the old item; already equipped is a no-op; gate truth table -- matrix coverage
- [x] `Assets/_Game/Scripts/Inventory/CLAUDE.md`, `Scripts/Combat/CLAUDE.md` -- document the bar equip rule and the swap sequence

**Acceptance Criteria:**
- Given a sword and a bow on the bar, when the player alternates their keys while sheathed, then the equipped weapon alternates and both slots remain.
- Given the inventory screen is open, when the player clicks an equippable bar slot, then the item equips (the cursor unlock does not block it).
- Given a load with an equipped item on the bar, when the save is restored, then the slot is kept and highlighted.

## Design Notes

Equip during the sequence raises `OnVisualsRefreshed` → `HandleVisualsRefreshed` → `CompleteWeaponSwitch`. That no-ops
because `EndWeaponSwitch` already ran. Start the draw **after** `Equip` returns: the controller swap resets every
animator trigger. The existing dodge / airborne / incapacitated net and the watchdog finish a stuck sheathe, which then
equips and draws.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode) -- expected: all green, including the new tests

**Manual checks (if no CLI):**
- Play mode: sword drawn, press the bow's bar key → sheathe, swap, draw. Shield key while the sword is drawn → shield in hand.

## Suggested Review Order

**Bar slot use and keeping equipped slots**

- Entry point: equippable items from the bar equip in place, or ask PlayerCombat when drawn.
  [`ActionBarSystem.cs:246`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L246)

- Equipped items left the inventory; the slot is kept with index -1.
  [`ActionBarSystem.cs:168`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L168)

- Equipped before RemoveItem so the inventory-changed validation sees the item somewhere.
  [`EquipmentSystem.cs:97`](../../Assets/_Game/Scripts/Inventory/EquipmentSystem.cs#L97)

- Highlight / no-op only when no copy is left in the inventory.
  [`ActionBarSystem.cs:190`](../../Assets/_Game/Scripts/Inventory/ActionBarSystem.cs#L190)

**Sheathe → equip → draw**

- Request handler: fists branch, else store the item and start the sheathe.
  [`PlayerCombat.cs:1225`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1225)

- Sheathe completion only marks the swap; never equips inside an animation callback.
  [`PlayerCombat.cs:1204`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1204)

- Update finishes once the player is free; draw skipped without a weapon.
  [`PlayerCombat.cs:1207`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L1207)

- R overrides a waiting swap.
  [`PlayerCombat.cs:325`](../../Assets/_Game/Scripts/Combat/PlayerCombat.cs#L325)

**Gate**

- Action gate without IsBusy so clicks with the inventory open still work.
  [`PlayerStateManager.cs:545`](../../Assets/_Game/Scripts/Player/PlayerStateManager.cs#L545)

**UI**

- Equipped colour, hover wins, hover reset on disable.
  [`ActionBarSlotUI.cs:102`](../../Assets/_Game/Scripts/UI/HUD/ActionBarSlotUI.cs#L102)

- Bar refreshes on equipment changes and passes the equipped flag.
  [`ActionBarUI.cs:80`](../../Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs#L80)

- An equipped bar slot is never dropped as a cooking ingredient.
  [`CookingIngredientSlotUI.cs:59`](../../Assets/_Game/Scripts/UI/Crafting/CookingIngredientSlotUI.cs#L59)

**Tests**

- Event-order regression test for the in-game inventory listener.
  [`ActionBarSystemTests.cs:181`](../../Assets/Tests/EditMode/ActionBarSystemTests.cs#L181)

- Gate truth table.
  [`WeaponSwitchGateTests.cs:44`](../../Assets/Tests/EditMode/WeaponSwitchGateTests.cs#L44)
