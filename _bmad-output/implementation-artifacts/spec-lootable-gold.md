---
title: 'Lootable gold'
type: 'feature'
created: '2026-11-10'
status: 'done'
baseline_commit: 'fa720ad70ef9a3c44b555f52187289f0e7dfef57'
context:
  - '{project-root}/_bmad-output/project-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The player can earn and spend currency, but gold cannot be authored as loot in containers or collected as a world item.

**Approach:** Add a stackable Gold item. Containers and NPC inventories hold coins like other items; collecting coins into the player's inventory immediately credits the existing gold balance instead of occupying inventory slots. Add 25 coins to BrannsStash and provide the user with an icon prompt.

## Boundaries & Constraints

**Always:** One coin equals one gold. Preserve the current one-unit-per-take container interaction and existing food stock. Preserve ownership, successful-take notifications, quest progress and save IDs. Use typed ScriptableObject event channels between Inventory and Economy. Only the player converts coins into currency; NPC stock and containers retain them. Preserve unsaved scene changes during Unity authoring. The generated icon is supplied later by the user; keep the item functional in the meantime.

**Ask First:** Changes to currency denomination, automatic whole-stack looting, or a wider economy redesign.

**Never:** Add manual coin consumption, put coins into the player's ordinary item slots, generate paid art, replenish the stash, introduce currency withdrawals, or change shop prices and starting gold.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Container take | Stack contains 25 coins | Take one: balance +1, source count 24, no player item slot | Remove source only after successful credit |
| World pickup | Authored Gold pickup | Balance +1; consume pickup using existing persistence | Failed credit leaves pickup available |
| Ordinary item | Bread or apple collected | Existing inventory behavior | Existing null rejection preserved |
| Non-player inventory | Add coins to chest or NPC | Coins stack as ordinary loot | No currency event |
| Invalid destination | Gold receiver unavailable or balance cannot accept credit | Reject collection; retain source | Log actionable configuration warning; no taken notification |
| Restore | Saved currency and remaining chest stock | Restore each exactly once | Restoring coins in a container never grants player currency or quest progress |
| Stash quest | Gold successfully taken from Brann's chest | Existing taken fact can progress from gold alone | Opening or restoring never progresses quest |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/InventorySystem.cs` — shared AddItem path, starting stock and RestoreSlots.
- `Assets/_Game/Scripts/Inventory/ItemPickup.cs` — currently destroys pickup without checking AddItem success.
- `Assets/_Game/Scripts/Economy/GoldSystem.cs` — currency balance, change notification and save restoration.
- `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs` — adds before decrementing and emits OnContainerItemTaken.
- `Assets/_Game/Scripts/World/ContainerTakeFact.cs` — source and allowed-item filtering for stash progress.
- `Assets/_Game/Scenes/StartingTown.unity` — HiddenStashQuest/BrannsStash authored stock and quest listener.
- `Assets/_Game/Art/UI/Items/CLAUDE.md` — established icon prompt and import settings.

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/ScriptableObjects/Items/GoldItemSO.cs`, `Assets/_Game/Data/Items/Item_Gold.asset` — introduce a distinct stackable coin type with a unique save ID and clear automatic-collection description.
- [x] `Assets/_Game/Scripts/Core/GoldLootRequest.cs`, `Assets/_Game/ScriptableObjects/Events/GameEventSO_GoldLootRequest.cs`, `Assets/_Game/Data/Events/OnGoldLootRequested.asset` — add a synchronous typed request with explicit success acknowledgement, allowing a rejected credit to preserve source loot without direct cross-system calls.
- [x] `Assets/_Game/Scripts/Inventory/InventorySystem.cs` — add explicitly configured player-only gold routing to AddItem. Non-player inventories keep their existing stack behavior; source creation and restoration must not trigger conversion.
- [x] `Assets/_Game/Scripts/Economy/GoldSystem.cs` — subscribe/unsubscribe the optional player loot channel; acknowledge successful credit only once and reject overflow without wrapping the balance. Preserve existing trade/reward behavior.
- [x] `Assets/_Game/Scripts/Inventory/ItemPickup.cs` — consume world pickups only after AddItem succeeds.
- [x] `Assets/_Game/Prefabs/Player/Player.prefab` — wire both inventory routing and gold reception on the actual player; keep NPC reception unwired.
- [x] `Assets/_Game/Prefabs/Items/Item_Gold.prefab` — provide a simple coin-shaped placeholder using existing materials, Rigidbody, collider and configured ItemPickup on Interactable layer; wire worldItemPrefab.
- [x] `Assets/_Game/Scripts/UI/Inventory/ItemDetailFormatter.cs`, `Assets/Tests/EditMode/ItemDetailFormatterTests.cs` — display Currency category and a clear gold value for the new type; keep it in Misc where filtering applies.
- [x] `Assets/_Game/Scenes/StartingTown.unity`, `Assets/_Game/Data/Items/ItemCatalog.asset` — add 25 coins to BrannsStash, include gold in its ContainerTakeFact allowlist, sync catalog and verify scene/prefab references.
- [x] `Assets/Tests/EditMode/GoldLootTests.cs` — cover successful and rejected credit, non-player stacking, ordinary-item regression, exact-once processing, overflow and restore without payout; extend existing container tests where useful.
- [x] `docs/World/Quests/HiddenStash.md`, relevant folder `CLAUDE.md` files — document stash contents and gold routing; include the standard icon-generation prompt and target filename in completion notes.

**Acceptance Criteria:**
- Given a fresh game, when BrannsStash opens, then it contains its original five bread and two apples plus 25 gold coins.
- Given the configured player, when gold is taken through container controls or collected in the world, then the visible existing currency balance updates immediately without a Gold inventory slot.
- Given a save after taking coins, when loading it, then the gold balance and remaining loot restore without duplication.
- Given gold is the first loot taken from BrannsStash, when Tam's follow-up is evaluated, then it recognizes the stash-taking fact through the existing quest rules.

## Spec Change Log

## Design Notes

Use an acknowledged request because a fire-and-forget currency event cannot tell the inventory whether a listener actually credited the balance. The request begins unaccepted; the configured player GoldSystem accepts it only after a valid addition. Inventory returns that result to existing container and pickup callers. This keeps failed transfers lossless and avoids Inventory directly depending on Economy. Runtime callbacks remain synchronous, with listeners registered in OnEnable and removed in OnDisable.

## Verification

- Unity compilation and console: no new compilation errors.
- Targeted EditMode tests: gold routing, inventory operations, container-take filtering, item detail formatting and catalog validation pass.
- Unity integration: exercise actual stash transfer and world pickup; inspect currency, remaining stock, quest fact and rejected-transfer behavior.
- Capture/restore actual player currency and stash SaveableObject state; distinguish this check from a full disk save/load if the latter is unavailable.
- Inspect fresh authored stash stock, unique item ID, catalog entry, player-only event wiring and placeholder pickup prefab.

## Implementation evidence

- Unity compiled the new runtime and test types successfully.
- 115 targeted EditMode tests passed, zero failures/skips: GoldLoot, InventorySystem, ContainerTakeFact, ItemDetailFormatter, ItemCatalog and ItemFilterRules (job `431aef5667c4471188fd35dd9c5137bd`).
- Actual play-mode ContainerUI transfer: balance 500 to 501, chest coins 25 to 24, no player slot added, gold-only take sets HiddenStash taken fact. Opening alone did not set it.
- Actual stash SaveableObject capture, JSON serialization and restore preserved 24 remaining coins without another payout. PlayerSaveAdapter.Capture currency serialized and restored exactly. Full disk save/load was not exercised.
- Instantiated the real coin prefab: natural Start initialization, +1 gold, no inventory slot, and destruction after the next frame all passed. Unity was unfocused with background frames disabled; temporarily enabling runtime background frames resolved the initial verification timing, and the original setting was restored.
- Quest report: HiddenStash has no errors/warnings; existing informational V13 remains. Both scenes clean after play; authored stash restored to five bread, two apples and 25 gold.
- Added a muted URP coin placeholder, rendered placeholder icon, stable item ID and catalog registration. No paid art generated.
- Three independent reviewers (blind/adversarial, edge cases, acceptance) returned no actionable defects.
- Existing PlayerAnimationDriver.SetInCombat startup NullReferenceException at line 85 remains; it is documented by the earlier Hidden Stash spec and outside these changes. Burst pipe errors also appeared during compilation; no new C# compilation errors.
- Source whitespace check passed. Generated assets include their Unity-created meta files.

## Suggested Review Order

- Route collected coins through the player inventory entry point.
  [InventorySystem.cs:82](../../Assets/_Game/Scripts/Inventory/InventorySystem.cs#L82)

- Credit only the addressed wallet, reject overflow, and acknowledge once.
  [GoldSystem.cs:38](../../Assets/_Game/Scripts/Economy/GoldSystem.cs#L38)

- Preserve world pickups when an addition fails.
  [ItemPickup.cs:63](../../Assets/_Game/Scripts/Inventory/ItemPickup.cs#L63)

- Review authored stash stock and gold-only quest progression.
  [HiddenStash.md:8](../../docs/World/Quests/HiddenStash.md#L8)

- Check routing, rejection, exact-once payment and container regressions.
  [GoldLootTests.cs:61](../../Assets/Tests/EditMode/GoldLootTests.cs#L61)

- Replace the temporary icon using the project's shared art prompt.
  [Gold-Icon-Prompt.md:1](../../docs/Gold-Icon-Prompt.md#L1)

