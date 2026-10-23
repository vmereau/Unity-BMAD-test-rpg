---
title: 'Harvestable plants (healing plant)'
type: 'feature'
created: '2026-10-23'
status: 'done'
baseline_commit: 'a0bd713'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/Assets/_Game/Prefabs/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The future alchemy system needs gatherable ingredients, but the only world-item type is `ItemPickup`, whose prefab is also the Rigidbody drop prefab. Plants must be placed in the world, stay fixed, and be gathered.

**Approach:** Add a generic `Harvestable` interactable (Harvest / item name, outline, permanent removal, optional ownership), a new `Harvestable` physics layer and a base harvestable prefab. The first instance is a Healing Plant (consumable heal) with a separate generic Rigidbody drop prefab and an icon rendered from the plant model.

## Boundaries & Constraints

**Always:**
- Harvestable root: layer **Harvestable (9)**, no Rigidbody, **trigger** collider (the player walks through plants), `InteractionHighlight`, `SaveableObject`; optional `Ownership`.
- The prompt verb is "Harvest" ("Steal" when `Ownership.IsIllegal`). The name tag is the yield item's `itemName`.
- On harvest: add `_yieldCount` (default 1) yield items, `MarkConsumed` the save key, destroy the object. It never regrows.
- The world plant and the inventory drop prefab are different prefabs. `ItemSO.worldItemPrefab` = the generic drop prefab.
- Player `InteractionSystem._raycastMask` covers Interactable + Harvestable.
- Healing Plant item: `PotionItemSO`, name "Healing Plant", heal 12 (potion: 30), maxStacks 20, buy 4 / sell 2.

**Ask First:** Changing `ItemPickup` behaviour, the save format, or the `TheftKind` enum.

**Never:** Alchemy / recipes / crafting, regrowth, a harvest animation or skill gate, OpenRouter `generate_image`, an HDRP material swap (the foliage shader graph already has a URP target).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Harvest | Look at plant, press E | Outline + card "Harvest / Healing Plant"; +1 Healing Plant; plant gone | N/A |
| Owned plant, owner alive | Press E | Red outline, "Steal"; theft raised (Item kind); item added | N/A |
| Save after harvest, reload | Plant consumed | Plant absent after load | N/A |
| Drop from inventory | Drop Healing Plant | Generic drop prefab falls with physics, "Pick Up" | N/A |
| Misconfigured | `_item` null or no player inventory | No prompt, no interaction | `GameLog.Error`, component disabled |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/ItemPickup.cs` -- reference pattern (prompt/name, Awake/Start guards, MarkConsumed)
- `Assets/_Game/Scripts/World/InteractionSystem.cs` -- `_raycastMask`, theft is raised before `Interact()` from a sibling `Ownership`
- `Assets/_Game/Scripts/World/Ownership.cs` -- `Awake` maps sibling interactable → `TheftKind`
- `Assets/_Game/Scripts/Core/Save/SaveSystem.cs` -- consumed keys destroy `SaveableObject`s on region apply (generic, no change)
- `Assets/_Game/Editor/ItemIconGenerator.cs` -- renders icons from `worldItemPrefab` via `AssetPreview`
- `Assets/_Game/Prefabs/Items/Base Item.prefab` -- generic drop prefab (Rigidbody, layer 8, ItemPickup)
- `Assets/Hivemind/HDRP(Default)/MedievalFantasyVillage/Art/Prefabs/SM_WildCarrot_M_01.prefab` -- plant model (LODGroup, no collider)
- `ProjectSettings/TagManager.asset` -- layer 9 free

## Tasks & Acceptance

**Execution:**
- [x] `ProjectSettings/TagManager.asset` -- name layer 9 `Harvestable` -- generic layer for all harvestables
- [x] `Assets/_Game/Scripts/World/Harvestable.cs` -- new `IInteractable` MonoBehaviour (`Game.World`): `_item`, `_yieldCount` (min 1), `Item`/`YieldCount` getters; behaviour per Always/Matrix -- generic harvest node
- [x] `Assets/_Game/Scripts/World/Ownership.cs` -- map a sibling `Harvestable` to `TheftKind.Item` -- witnesses use item-theft scolds
- [x] `Assets/_Game/Editor/ItemIconGenerator.cs` -- when an item is the yield of a harvestable prefab under `Assets/_Game/Prefabs/Harvestables`, render its icon from that prefab instead of `worldItemPrefab` -- the icon shows the real plant, not the placeholder drop
- [x] `Assets/_Game/Prefabs/Harvestables/Harvestable_Base.prefab` -- root per Always (Harvestable, trigger BoxCollider, InteractionHighlight, SaveableObject with empty id) -- base for all plants
- [x] `Assets/_Game/Prefabs/Harvestables/Harvestable_HealingPlant.prefab` -- variant of the base; nested `SM_WildCarrot_M_01` child; collider fit to the LOD0 bounds; `_item` = Healing Plant -- first harvestable
- [x] `Assets/_Game/Prefabs/Items/Item_HealingPlant.prefab` -- variant of `Base Item`, `ItemPickup._item` = Healing Plant -- generic placeholder drop
- [x] `Assets/_Game/Data/Items/Item_HealingPlant.asset` -- `PotionItemSO` with the values above, description "A bitter wild root that slowly knits small wounds.", `worldItemPrefab` = drop prefab; verify it is in `ItemCatalog` -- item data + save lookup
- [x] `Assets/_Game/Art/UI/Items/Item_HealingPlant_Icon.png` -- run `Tools/Items/Generate Missing Icons` -- icon
- [x] `Assets/_Game/Scenes/StartingTown.unity` -- place 3 Healing Plants near the town; assign save IDs (`Tools/Save/Validate Save IDs`) -- playable test
- [x] `Assets/Tests/EditMode/HarvestableTests.cs` -- prompt (Harvest / Steal), name tag, yield count clamp -- regressions
- [x] `Assets/_Game/Prefabs/CLAUDE.md`, `Assets/_Game/Scripts/World/CLAUDE.md` -- document the Harvestable layer, prefab contract and component -- other agents

**Acceptance Criteria:**
- Given a Healing Plant in StartingTown, when the player looks at it, then it gets the standard outline and the card reads "Harvest" / "Healing Plant".
- Given a harvested Healing Plant in the inventory, when it is used, then the player heals 12 HP and the stack decrements.
- Given a new harvestable type (e.g. a mana plant), when it is authored, then only a new prefab variant + ItemSO are needed, no code.

## Design Notes

No `HarvestableSO`: a plant's data is just its yield item plus its count, so a component on a prefab variant keeps authoring to one variant per plant type. A data SO can be introduced later if harvestables gain shared rules such as skill gates or tool requirements.

The theft check needs no change. `InteractionSystem` raises it from any interactable that has a sibling `Ownership`. Only the `Kind` mapping in `Ownership` changes.

## Verification

**Commands:**
- Unity MCP `read_console` -- expected: no compile errors
- Unity MCP `run_tests` (EditMode) -- expected: all pass

**Manual checks (if no CLI):**
- Play StartingTown: look at a plant, harvest it, eat it, drop one, save and reload, and confirm the matrix rows.

## Suggested Review Order

**Harvest interaction**

- Entry point: harvest adds yield, keeps node if nothing added, marks consumed, destroys.
  [`Harvestable.cs:69`](../../Assets/_Game/Scripts/World/Harvestable.cs#L69)

- Prompt / name tag reuse the pure helpers (Harvest vs Steal).
  [`Harvestable.cs:27`](../../Assets/_Game/Scripts/World/Harvestable.cs#L27)

- Missing save key is warned instead of silently respawning.
  [`Harvestable.cs:86`](../../Assets/_Game/Scripts/World/Harvestable.cs#L86)

- Owned plants count as item theft → existing scold barks.
  [`Ownership.cs:33`](../../Assets/_Game/Scripts/World/Ownership.cs#L33)

**Layer & prefab contract**

- Player interaction mask now covers Interactable + Harvestable (768).
  [`Player.prefab:392`](../../Assets/_Game/Prefabs/Player/Player.prefab#L392)

- Base / variant / drop-prefab contract for future plant types.
  [`CLAUDE.md:37`](../../Assets/_Game/Prefabs/CLAUDE.md#L37)

**Icon generation**

- Yield items render from the harvestable (real model), not the placeholder drop.
  [`ItemIconGenerator.cs:60`](../../Assets/_Game/Editor/ItemIconGenerator.cs#L60)

- Harvestable prefab scan under Prefabs/Harvestables.
  [`ItemIconGenerator.cs:236`](../../Assets/_Game/Editor/ItemIconGenerator.cs#L236)

**Peripherals**

- Pure prompt / name / yield tests.
  [`HarvestableTests.cs:1`](../../Assets/Tests/EditMode/HarvestableTests.cs#L1)

- Item data: heal 12, stack 20, buy 4 / sell 2.
  [`Item_HealingPlant.asset:1`](../../Assets/_Game/Data/Items/Item_HealingPlant.asset#L1)
