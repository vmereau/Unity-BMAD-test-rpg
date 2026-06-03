---
title: 'Move InventorySystem to Entity_base (lootable-entity groundwork)'
slug: 'inventorysystem-to-entity-base'
created: '2026-06-03'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1 (URP 17)', 'C# / Game.asmdef', 'Unity Prefab Variants', 'Unity Editor / MCP (manage_gameobject / manage_components)']
files_to_modify:
  - 'Assets/_Game/Prefabs/Entities/Entity_base.prefab (add InventorySystem to root)'
  - 'Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab (remove added InventorySystem)'
  - 'Assets/_Game/Scenes/StartingTown.unity (retarget shopkeeper _startingItems override)'
  - 'Assets/_Game/Scripts/Inventory/CLAUDE.md (doc)'
  - 'Assets/_Game/Prefabs/CLAUDE.md (doc)'
code_patterns:
  - 'InventorySystem resolved via GetComponent<InventorySystem>() — inheritance keeps NPC resolution valid, no C# change'
  - 'Prefab variant inherits base component; remove the now-duplicate added component on the variant'
  - 'Scene m_Modifications targeting a removed variant-added component must retarget to the inherited base component fileID'
test_patterns:
  - 'Manual in-Editor (StartingTown): verify shop stock + monster inventory presence'
---

# Tech-Spec: Move InventorySystem to Entity_base (lootable-entity groundwork)

**Created:** 2026-06-03

## Overview

### Problem Statement

`InventorySystem` (the stacked item container) currently lives only on
`NPC_base Variant.prefab` as an *added* component. The base `Entity_base.prefab` —
and therefore monsters like `Monster_DarknessSpider Variant.prefab` — have **no
InventorySystem**, so a monster cannot hold items. Future work needs every entity
(monsters included) to carry an inventory so a killed monster can be looted. This is
a structural refactor only; the loot interaction itself is a separate spec.

### Solution

Move the `InventorySystem` component from `NPC_base Variant.prefab` down onto the
shared base `Entity_base.prefab` root (empty `_startingItems`). The NPC variant then
*inherits* it (so its existing trade flow still resolves via
`GetComponent<InventorySystem>()`), and monsters inherit it automatically with no
per-prefab edits. Because the shopkeeper NPC's stock is authored as scene
`m_Modifications` against the NPC's current InventorySystem fileID, those overrides
are retargeted onto the inherited base component so the shop keeps its 3-item stock.

### Scope

**In Scope:**
- `Entity_base.prefab`: add an `InventorySystem` component to the root (`_startingItems: []`).
- `NPC_base Variant.prefab`: remove the now-duplicate *added* `InventorySystem` (fileID `-8669163291337827286`) so the NPC keeps exactly one (the inherited base one).
- `StartingTown.unity`: retarget the shopkeeper's 7 `_startingItems` scene `m_Modifications` from the removed NPC InventorySystem fileID to the inherited base InventorySystem fileID (preserve the 3-item stock).
- Verify `Monster_DarknessSpider Variant.prefab` inherits the base InventorySystem (no per-prefab edit).
- Doc updates: `Scripts/Inventory/CLAUDE.md`, `Prefabs/CLAUDE.md`.

**Out of Scope:**
- The actual loot / dead-corpse interaction (future spec) — no `Interact()` behavior added here.
- Moving `GoldSystem` — it stays an added component on `NPC_base Variant` (per user decision).
- Authoring monster loot tables / starting items — monster inventories stay empty until a future data task.
- Any C# changes to `NPCPresence`, `DialogueSystem`, `ShopDialogueNode`, `NPCDialogueRequestData` — inheritance keeps `GetComponent<InventorySystem>()` resolution valid (confirmed below).
- Persisting inventory/looted state across save & scene reload.

## Context for Development

### Codebase Patterns

- `InventorySystem` (`Assets/_Game/Scripts/Inventory/InventorySystem.cs`, script guid `6bb7168ca149809449a28fc126185f40`, namespace `Game.Inventory`): `MonoBehaviour` with `[SerializeField] List<StartingItem> _startingItems`; `Awake()` pre-populates slots from `_startingItems`. Empty list ⇒ Awake is a no-op (safe on monsters).
- NPC dialogue → trade flow: `NPCPresence.Interact()` (`Assets/_Game/Scripts/AI/NPC/NPCPresence.cs`) calls `GetComponent<InventorySystem>()` and `GetComponent<GoldSystem>()`, packs them into `NPCDialogueRequestData` (`npcInventory`, `npcGoldSystem`), and raises `_onDialogueRequested`. `DialogueSystem.HandleDialogueRequested` caches `data.npcInventory`; the `ShopDialogueNode` case opens `NPCTradeUI.Open(inv, npcGold)`. **All of this resolves the inherited component unchanged — no code edit.**
- `GetComponent<T>` returns disabled components too and resolves an inherited prefab component identically to an added one — the move is transparent to call sites.
- Prefab inheritance: `NPC_base Variant` and `Monster_DarknessSpider Variant` are both variants of `Entity_base`; base-root additions propagate to variants and to their scene instances without per-instance edits (confirmed by the prior EntityPresence spec).

### Prefab / scene migration gotcha (critical)

- The NPC currently *adds* its own InventorySystem (`-8669163291337827286`). If we add InventorySystem to the base **and leave the NPC's added one**, the NPC root ends up with **two** InventorySystems and `GetComponent<InventorySystem>()` may return the wrong (empty) one. So the NPC's added InventorySystem **must be removed**.
- The shopkeeper NPC in `StartingTown.unity` overrides `_startingItems` via 7 `m_Modifications` whose `target` is `{fileID: -8669163291337827286, guid: ea73572b6a4e79d4fbe41fdea8c1e693, type: 3}`. Removing that component orphans those overrides → the shop would lose its stock. They must be retargeted to `{fileID: <BASE_INVENTORY_FILEID>, guid: ea73572b6a4e79d4fbe41fdea8c1e693, type: 3}` (guid stays the directly-instanced NPC_base Variant; fileID becomes the inherited base component's). Editing via the Unity Editor (re-adding the 3 items on the instance) auto-writes correct overrides; raw YAML must retarget the fileID by hand.
- `Base_Container.prefab` (guid `a94b10553cd3eb14fb173a9a0f1b381f`) also has an InventorySystem and its own scene `_startingItems` overrides (fileID `5065574312572025887`) — it is **not** an `Entity_base` variant, so it is **out of scope and must not be touched**.
- Direct `.prefab`/`.unity` YAML edits: after editing, refresh Unity with `if_dirty`, **never** `force` (root CLAUDE.md).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | guid `e265604e8dcaaad4c81236c415d32664`; root GO `8283577674775524483`. Gains the InventorySystem. |
| `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` | guid `ea73572b6a4e79d4fbe41fdea8c1e693`; remove added InventorySystem `-8669163291337827286`. |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | guid `0486cf4048d340b468ff97f8a6b3a40f`; inherits the new InventorySystem (verify only). |
| `Assets/_Game/Scenes/StartingTown.unity` | Shopkeeper override retarget (7 `_startingItems` modifications at ~line 3632). |
| `Assets/_Game/Scripts/Inventory/InventorySystem.cs` | The component being moved (script guid `6bb7168ca149809449a28fc126185f40`). |
| `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` | Confirms `GetComponent<InventorySystem>()` consumer — no edit. |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | Confirms ShopDialogueNode → trade UI consumer — no edit. |

### Technical Decisions

- **Scope:** move `InventorySystem` only; `GoldSystem` stays NPC-only (user decision).
- **Edit method:** Unity Editor / MCP (`manage_gameobject` / `manage_components`) so Unity assigns the base component fileID and auto-remaps scene overrides; YAML end-state documented for verification (user decision).
- **No C# changes:** inheritance keeps `GetComponent<InventorySystem>()` valid for the trade flow.
- **Base InventorySystem state:** `m_Enabled: 1` (enabled), `_startingItems: []`. Enabled matches NPC behavior; empty list is a no-op in `Awake` for monsters.
- **GoldSystem unchanged:** remains added on `NPC_base Variant` (`30696407886650039`, `_startingGold: 500`).

### Investigation Findings (Step 2 — confirmed ground truth)

- **`Entity_base.prefab`** root GameObject `8283577674775524483` (Layer 6) — current `m_Component` list (10 entries): Transform `1674852574886742708`, PersistentID `1186952831256218892`, MonsterAnimationDriver `3299999205233303053`, EntityHealth `-6003750127742918704` (disabled), EntityBrain `6892060459904109189` (disabled), MonsterAnimationBridge `7557825151662695134`, FactionMember `3156794405520552273`, EntityUI `2292172924411989332`, (one more) and **EntityPresence `6326516008175686655`** (last). The new `InventorySystem` MonoBehaviour is **appended** to this list with a fresh fileID and wired to the base PersistentID is **not** needed (InventorySystem has no `_persistentID` field — only `_startingItems`).
  - InteractionCollider child already present (`951893364672279125`, Layer 8) from the prior EntityPresence spec — orthogonal, untouched.
- **`NPC_base Variant.prefab`** (guid `ea73572b6a4e79d4fbe41fdea8c1e693`): the InventorySystem to remove is `m_AddedComponents` entry `addedObject: {fileID: -8669163291337827286}` (block at ~line 1697, `m_EditorClassIdentifier: Game::Game.Inventory.InventorySystem`, `_startingItems: []`). Sibling added components to KEEP: `NPCPresence` `2441576283753789042`, `NPCMemoryComponent` `4585579539212275391`, `NPCDialogueGraphComponent` `1448907008804725736`, **GoldSystem** `30696407886650039`, `HumanoidAnimationBridge` `3168899758345796514`, `HumanoidAIAnimationDriver` `3766970546440902038`.
- **Scene `StartingTown.unity`** — exactly **one** entity InventorySystem override (the shopkeeper, guid `ea73…`, fileID `-8669163291337827286`, at ~line 3632). The shop stock to preserve:
  - `_startingItems.Array.size = 3`
  - `data[0].item` = guid `67a927b8f3dd5fd41953e506ae9643d9`, `count = 1`
  - `data[1].item` = guid `376b91fe377aeaa4d9b2128cc8805a89`, `count = 1`
  - `data[2].item` = guid `e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0`, `count = 5`
  - The other two `_startingItems` overrides in the scene (fileID `5065574312572025887`, guid `a94b10553cd3eb14fb173a9a0f1b381f`) belong to **`Base_Container.prefab`** instances — **out of scope, do not touch.**
- **`Monster_DarknessSpider Variant.prefab`** (guid `0486cf4048d340b468ff97f8a6b3a40f`): its `Entity_base` PrefabInstance has `m_AddedComponents: []` — it adds **no** InventorySystem and will inherit the new base one automatically (verify only).
- **No C# references** to the InventorySystem-on-NPC arrangement other than `GetComponent<InventorySystem>()` in `NPCPresence.Interact()`; the trade flow (`NPCDialogueRequestData.npcInventory` → `DialogueSystem` ShopDialogueNode case → `NPCTradeUI.Open`) is identity-agnostic and needs no edit.

## Implementation Plan

> **Method:** Unity Editor / MCP (`manage_gameobject` / `manage_components`) so Unity assigns the new base component fileID and re-authors scene overrides cleanly. The YAML blocks below are the **authoritative end-state** for verification / hand-edit fallback. After any raw `.prefab` / `.unity` YAML edit, refresh Unity with `if_dirty` — **never** `force` (root CLAUDE.md).
> **Order:** capture shop stock → add to base → remove from NPC → restore shop stock on scene → verify monster + scene → docs.

### Tasks

- [x] **Task 1: Capture the shopkeeper's current shop stock (no edit yet)**
  - File: `Assets/_Game/Scenes/StartingTown.unity` (read-only)
  - Action: Confirm the shopkeeper NPC instance (guid `ea73572b6a4e79d4fbe41fdea8c1e693`) has these 3 `_startingItems` overrides on InventorySystem fileID `-8669163291337827286`, so they can be restored after the move:
    - size `3`; `data[0].item` guid `67a927b8f3dd5fd41953e506ae9643d9` count `1`; `data[1].item` guid `376b91fe377aeaa4d9b2128cc8805a89` count `1`; `data[2].item` guid `e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0` count `5`.
  - Notes: This is the only entity shop override in the scene. Record it now because removing the NPC's InventorySystem (Task 3) orphans the override and the Editor will drop it.

- [x] **Task 2: Add `InventorySystem` to `Entity_base.prefab` root**
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab`
  - Action (Editor/MCP): Open `Entity_base` in Prefab Mode → Add Component → `InventorySystem` on the root `Entity_base` GameObject. Leave `_startingItems` empty. Enabled.
  - Authoritative end-state YAML — append a new MonoBehaviour and add it to the root's `m_Component` list:
    ```yaml
    --- !u!114 &<BASE_INVENTORY_FILEID>
    MonoBehaviour:
      m_ObjectHideFlags: 0
      m_CorrespondingSourceObject: {fileID: 0}
      m_PrefabInstance: {fileID: 0}
      m_PrefabAsset: {fileID: 0}
      m_GameObject: {fileID: 8283577674775524483}
      m_Enabled: 1
      m_EditorHideFlags: 0
      m_Script: {fileID: 11500000, guid: 6bb7168ca149809449a28fc126185f40, type: 3}
      m_Name: 
      m_EditorClassIdentifier: Game::Game.Inventory.InventorySystem
      _startingItems: []
    ```
    …and append `- component: {fileID: <BASE_INVENTORY_FILEID>}` to root GameObject `8283577674775524483`'s `m_Component` list.
  - Notes: `InventorySystem` has **only** a `_startingItems` field — there is **no `_persistentID`** to wire (unlike EntityPresence/EntityHealth). Let the Editor assign `<BASE_INVENTORY_FILEID>`; record it for Tasks 3 & 4. Script guid is `6bb7168ca149809449a28fc126185f40`.

- [x] **Task 3: Remove the now-duplicate added `InventorySystem` from `NPC_base Variant.prefab`**
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`
  - Action (Editor/MCP): Open `NPC_base Variant` in Prefab Mode. The root now shows **two** InventorySystems — the inherited base one and the variant's own added one. Remove the **added** one (the variant's own, fileID `-8669163291337827286`, `_startingItems: []`) via the component context menu → "Remove Component". Keep the inherited base one.
  - Authoritative end-state YAML: delete the `m_AddedComponents` entry whose `addedObject: {fileID: -8669163291337827286}` and delete the corresponding `--- !u!114 &-8669163291337827286` MonoBehaviour block. Leave all sibling added components intact: NPCPresence `2441576283753789042`, NPCMemoryComponent `4585579539212275391`, NPCDialogueGraphComponent `1448907008804725736`, **GoldSystem `30696407886650039`**, HumanoidAnimationBridge `3168899758345796514`, HumanoidAIAnimationDriver `3766970546440902038`.
  - Notes: Result — the NPC root carries exactly **one** InventorySystem (the inherited base one). **Do NOT remove GoldSystem** — it stays NPC-only per decision. Because the variant adds nothing else for inventory, no `m_RemovedComponents` entry is needed (we are deleting the variant's *added* component, not suppressing an inherited one).

- [x] **Task 4: Restore the shopkeeper's stock on the scene instance**
  - File: `Assets/_Game/Scenes/StartingTown.unity`
  - Action (Editor): Open `StartingTown`, select the shopkeeper NPC instance, and on its (now inherited) InventorySystem set `_startingItems` back to the 3 items captured in Task 1 (sizes/counts identical). Apply as a scene override on the instance (not on the prefab).
  - Authoritative end-state YAML (hand-edit fallback): retarget the 7 `_startingItems` modifications — change every `target: {fileID: -8669163291337827286, guid: ea73572b6a4e79d4fbe41fdea8c1e693, type: 3}` to `target: {fileID: <BASE_INVENTORY_FILEID>, guid: ea73572b6a4e79d4fbe41fdea8c1e693, type: 3}` (guid stays `ea73…` — the directly-instanced NPC_base Variant; fileID becomes the inherited base component's). Item guids/counts unchanged.
  - Notes: `<BASE_INVENTORY_FILEID>` is the fileID Unity assigned in Task 2. The **Base_Container** overrides (fileID `5065574312572025887`, guid `a94b10553cd3eb14fb173a9a0f1b381f`) must remain untouched.

- [x] **Task 5: Verify monster + scene propagation (no edits expected)**
  - Files: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`, `Assets/_Game/Scenes/StartingTown.unity`
  - Action: Open the spider variant and confirm it now inherits a single `InventorySystem` (empty `_startingItems`) from the base, with no added/removed inventory overrides. Enter Play in `StartingTown`; confirm the spider instance has an `InventorySystem` at runtime (`GetComponent<InventorySystem>()` non-null) and the shopkeeper's trade still lists the 3 items. Flag any auto-added override on the scene instances.
  - Notes: No YAML edits expected (spider `m_AddedComponents: []`).

- [x] **Task 6: Update folder CLAUDE.md docs**
  - Files: `Assets/_Game/Scripts/Inventory/CLAUDE.md`, `Assets/_Game/Prefabs/CLAUDE.md`
  - Action:
    - `Scripts/Inventory/CLAUDE.md`: note that `InventorySystem` now lives on `Entity_base.prefab` (every entity — NPC and monster — owns one); NPCs inherit it for trade, monsters for future looting; `GoldSystem` stays NPC-only.
    - `Prefabs/CLAUDE.md`: in the entity layer/structure section, record that `Entity_base` now carries `InventorySystem` (empty stock) on the root; NPC variant no longer adds its own (inherits the base); shop stock is authored as a scene-instance `_startingItems` override on the inherited component. Add the migration gotcha: moving a variant-added component down to the base requires deleting the variant's added copy AND retargeting any scene `m_Modifications` to the new base component fileID.

### Acceptance Criteria

- [x] **AC1 (base carries inventory):** Given `Entity_base.prefab`, when inspected in Prefab Mode, then its root has exactly one `InventorySystem` (script guid `6bb7168ca149809449a28fc126185f40`, `_startingItems` empty, enabled).
- [x] **AC2 (NPC single inventory):** Given `NPC_base Variant.prefab`, when inspected in Prefab Mode, then the root has exactly **one** `InventorySystem` (inherited from base) and still has its `GoldSystem` (`_startingGold: 500`); the previously-added InventorySystem `-8669163291337827286` is gone.
- [x] **AC3 (shop stock preserved):** Given the shopkeeper NPC in `StartingTown`, when the player opens trade via the `ShopDialogueNode`, then `NPCTradeUI` lists the same 3 items as before (`67a927b8…` ×1, `376b91fe…` ×1, `e5f6a7b8…` ×5).
- [x] **AC4 (trade flow unchanged, no code edit):** Given a shop NPC, when `NPCPresence.Interact()` runs, then `GetComponent<InventorySystem>()` resolves the inherited component and `NPCDialogueRequestData.npcInventory` is non-null — identical behavior to pre-refactor, with no C# changes.
- [x] **AC5 (monster inherits inventory):** Given `Monster_DarknessSpider Variant` (and any other `Entity_base` variant), when instantiated, then it has exactly one `InventorySystem` (empty) inherited from the base with no per-prefab override.
- [x] **AC6 (container untouched):** Given `Base_Container.prefab` and its scene instances, when inspected, then their own `InventorySystem` (fileID `5065574312572025887`) and `_startingItems` overrides are unchanged.
- [x] **AC7 (compile/clean reimport):** Given the prefab/scene edits, when Unity reimports (`refresh_unity` `if_dirty`), then there are no console errors, no "missing component"/orphaned-override warnings on the shopkeeper or spider instances.
- [x] **AC8 (GoldSystem scope intact):** Given the refactor, when `Entity_base` and the spider are inspected, then neither has a `GoldSystem` (it remains added only on `NPC_base Variant`).

## Additional Context

### Dependencies

- **No new external libraries** and **no C# changes.** Builds on existing systems: `InventorySystem`, `NPCPresence`, `DialogueSystem`/`ShopDialogueNode`/`NPCTradeUI`, `NPCDialogueRequestData`, Unity Prefab Variants.
- **Enables:** a future **loot-corpse spec** — a dead entity already has an `InventorySystem` to read/transfer items from; that spec can subclass/extend `EntityPresence` for the corpse `Interact()` without re-introducing the inventory component. Monster loot-table authoring (populating `_startingItems` or a drop system) is a separate follow-up.
- **Builds on:** the prior `EntityPresence` refactor (`tech-spec-entitypresence-base-interactable.md`) — same base prefab, same variant-propagation pattern.

### Testing Strategy

- **Primary — manual in-Editor (`StartingTown`):** verify AC2–AC6 by play-testing: open the shopkeeper's trade and confirm the 3-item stock; confirm the spider instance returns a non-null `InventorySystem` at runtime; confirm `Base_Container` loot is unchanged.
- **Reimport gate (AC7):** after the prefab/scene edits, `refresh_unity(mode="if_dirty")` and confirm a clean console with no orphaned-override or missing-component warnings.
- **No new EditMode tests:** there is no new code/behavior to assert (the component merely changed host prefab); the existing `InventorySystem` logic is untouched. Add tests only if a reviewer requests coverage of the trade-resolution path.

### Notes

- **Order matters (pre-mortem):** if Task 3 (remove from NPC) is done before Task 1 captures the stock, the Editor will silently drop the orphaned scene override and the shop loses its items. Always capture first, restore in Task 4.
- **Two-InventorySystem hazard:** after Task 2 and before Task 3, the NPC root transiently has two InventorySystems; `GetComponent<InventorySystem>()` would return the first found (likely the empty inherited one), so the shop could look empty. This is expected mid-migration — Task 3 resolves it. Do not ship in this state.
- **Raw YAML refresh rule:** after hand-editing any `.prefab`/`.unity`, `refresh_unity(mode="if_dirty")` — `force` discards disk edits (root CLAUDE.md).
- **`.meta`/GUID stability:** no script files are created or moved, so no new `.meta` and no GUID risk. `InventorySystem.cs` keeps guid `6bb7168ca149809449a28fc126185f40`.
- **Scene-override fileID subtlety:** the retargeted modification keeps `guid: ea73…` (the NPC_base Variant being instanced) but swaps the fileID to the inherited base component — letting the Editor re-author the override (re-enter the 3 items) is the safest way to get this right; hand-YAML must change all 7 target fileIDs.
- **Out of scope (flagged):** GoldSystem move, monster loot tables, save/persistence of inventory, and the actual loot interaction — all deferred to future specs.
- **CLAUDE.md candidate** (surface after implementation): "Moving a prefab-variant-added component down to the base requires (a) deleting the variant's added copy and (b) retargeting any scene `m_Modifications` from the old added-component fileID to the inherited base component fileID — otherwise instance overrides are silently dropped." Confirm wording in Step 4 / wrap-up.
