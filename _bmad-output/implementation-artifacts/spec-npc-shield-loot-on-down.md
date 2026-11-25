---
title: 'NPC shield goes into the lootable inventory when the NPC goes down'
type: 'feature'
created: '2026-11-25'
status: 'done'
baseline_commit: '9f102f86'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When an NPC dies or is knocked out, `NpcArmament.OnDowned` moves only the main weapon into its inventory. The shield stays equipped in the Offhand slot, and equipped items are never lootable, so a killed guard's shield can't be taken.

**Approach:** On down, unequip the equipped offhand item (shield) into the inventory along with the weapon, as the weapon rule already does. On wake-up, re-equip it if it is still there. If it was looted, the NPC fights without a shield.

## Boundaries & Constraints

**Always:** Follow the existing weapon rule: death and knockout both drop the shield. Body armor stays equipped and is not lootable. The controller is never swapped while down (`Refresh(false)`). Wake-up re-equips only the NPC's own dropped shield, never another shield the inventory holds (trade stock). A two-hander or bow in hand on wake keeps the shield stowed, using the existing `_stowedShield` path.

**Ask First:** Any change to the save format (`ObjectSaveData`).

**Never:** Spawning world pickups. Changing the player's equipment or death. Making body armor lootable.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Guard dies | Sword + shield + armor equipped | Sword and shield in the inventory (lootable); armor still equipped | N/A |
| Knockout, nothing looted | Sword + shield, down then wake | Sword and shield equipped again; inventory has neither | N/A |
| Knockout, shield looted | Shield removed from the inventory while down | Wakes with the sword, no shield; log line | N/A |
| Shield only, no weapon | Shield equipped, no weapon | Shield in the inventory on down; re-equipped on wake | N/A |
| Bow wielded at down | Shield already stowed in the inventory by the bow | Shield stays in the inventory (lootable); wake keeps it stowed while the bow is held | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/AI/NpcArmament.cs` -- `OnDowned` (drops the weapon, returns early when there is none), `OnWokeUp`, `EquipCarriedShield` (re-equips `_stowedShield` if still carried and the offhand is free), `RestoreLoadout`
- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` -- `Unequip(slot)` returns the item to the inventory; `EquippedShield`
- `Assets/_Game/Scripts/World/SaveableObject.cs` -- captures `Equipped` + `DroppedWeapon`; a corpse's inventory holds the shield, so no save change is needed
- `Assets/Tests/EditMode/NpcArmamentTests.cs` -- the existing down / wake tests

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/AI/NpcArmament.cs` -- In `OnDowned`, also unequip `EquippedShield` into the inventory and keep it as the shield to re-equip (reuse `_stowedShield`; if a bow already stowed one, that one stays). Restructure so an unarmed NPC with a shield still drops it, and run the visual refresh / hitbox reset whenever anything was dropped. `OnWokeUp` already calls `EquipCarriedShield` after the weapon is re-equipped, which covers re-equip, looted and two-hander cases. Update the class summary and the `OnDowned` doc comment -- the shield must be lootable like the weapon
- [x] `Assets/Tests/EditMode/NpcArmamentTests.cs` -- Add tests: down drops sword + shield, armor stays; wake re-equips both; looted shield → no shield after wake; shield-only NPC. Extend `OnDowned_WeaponGoesToInventory_ArmorStaysEquipped` or add alongside it
- [x] `Assets/_Game/Scripts/AI/RANGED_EQUIPMENT.md` -- Edit the **Down** and **Loadout** bullets: the shield drops with the weapon and is re-equipped on wake if not looted

**Acceptance Criteria:**
- Given a guard is killed, when the player loots the corpse, then the sword and the kite shield are both listed and can be taken
- Given a corpse whose shield was not looted is saved and reloaded, when the player loots it, then the shield is still in its loot and the NPC doesn't re-equip it

## Spec Change Log

- **Review (edge-case hunter):** `GrantMissingStartingShield` granted a fresh starting shield to a living NPC restored without one (a pre-shield-save fallback) — with shields now lootable on knockout, knockout → loot → save → reload farmed shields. User chose to drop the fallback: renamed `ReEquipCarriedStartingShield`, it only re-equips the starting shield still in the inventory. Saves written before shields existed now load guards shieldless (accepted). Also patched: a corpse granted its loadout in `Start` (no restored loadout) skips shields like weapons, so it never wears an unlootable shield. `COMBAT.md` → Blocking bullet corrected (it still said the shield stays equipped on down). KEEP: `_stowedShield` reuse in `OnDowned`.

## Design Notes

Reusing `_stowedShield` avoids a new field: it already means "my own shield, now in the inventory, put it back when the offhand is free", and `RestoreLoadout` clears it. A living NPC saved while down reloads standing, and `ReEquipCarriedStartingShield` (was `GrantMissingStartingShield`) re-equips the shield from its inventory — never granted anew. A corpse's `RestoreLoadout(killed: true)` grants nothing.

## Verification

**Commands:**
- Unity MCP `run_tests` (EditMode, filter `NpcArmamentTests`) -- expected: all pass
- Unity MCP `read_console` (errors) -- expected: no compile errors

**Manual checks (if no CLI):**
- Play: kill a guard, loot it → the sword and the shield are both in the loot

## Suggested Review Order

**Shield drops on down**

- Entry point: weapon and shield both unequipped into the loot; shield remembered as stowed.
  [`NpcArmament.cs:385`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L385)

- Reuses the stowed-shield slot, so wake-up needs no new logic.
  [`NpcArmament.cs:407`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L407)

- Wake: weapon re-equipped first, then the shield if not looted and offhand free.
  [`NpcArmament.cs:441`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L441)

**No free shields**

- Restore only re-equips a carried starting shield; never grants one (anti-farm).
  [`NpcArmament.cs:465`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L465)

- A corpse granted its loadout gets armor only — no shield it couldn't drop.
  [`NpcArmament.cs:273`](../../Assets/_Game/Scripts/AI/NpcArmament.cs#L273)

**Docs and tests**

- Down rule documented next to the weapon rule.
  [`RANGED_EQUIPMENT.md:81`](../../Assets/_Game/Scripts/AI/RANGED_EQUIPMENT.md#L81)

- Blocking section no longer claims the shield stays equipped.
  [`COMBAT.md:105`](../../Assets/_Game/Scripts/AI/COMBAT.md#L105)

- Down / wake / looted / shield-only tests.
  [`NpcArmamentTests.cs:170`](../../Assets/Tests/EditMode/NpcArmamentTests.cs#L170)
