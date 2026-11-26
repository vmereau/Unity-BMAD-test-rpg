---
title: 'Inventory equipment section as a paper doll'
type: 'feature'
created: '2026-11-26'
status: 'done'
baseline_commit: 'fe80eaa6'
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-Unity-BMAD-test-rpg-2026-11-26/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-Unity-BMAD-test-rpg-2026-11-26/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The Inventory equipment section is an unlabelled 200×64 column of slots that reads like a list, offers only click / double-click, and can't take drags, right-clicks or show where an item fits.

**Approach:** Rebuild it as the paper doll of the UX docs (EXPERIENCE.md = behaviour, DESIGN.md = look, mockup = reference; docs win): seven 88×88 `UI_SlotWell` slots around a flat Blender-rendered silhouette with dashed leader lines, hover / selected / valid / empty / offhand-ghost states, one selection shared with the grid, drag both ways, right-click Unequip. `EquipmentSystem` stays the only rule owner.

## Boundaries & Constraints

**Always:** positions/colours/sizes exactly as DESIGN.md (panel 40,140 496×800 `#2B2118` / edge `#4A3828`; silhouette 158,214 260×700; left x72 Helmet 200 · Armor 370 · Weapon 540 · Ring1 710; right x416 Necklace 286 · Offhand 540 · Ring2 710). Slots `UI_SlotWell` Sliced, multiplier 32. Keep the `EquipmentUI` component, `EquipmentPanel` root and slot objects (Player.prefab overrides target them). Double-click 0.3 s. UI calls `EquipmentSystem`; any equip legality is in `EquipmentRules`. Silhouette made locally with Blender MCP + Unity MCP.

**Ask First:** moving the item grid or detail panel; touching `InventoryPanel.prefab` (shared with container/trade); any Meshy / image-generation spend.

**Never:** gamepad/focus nav, stats in the panel, equip sounds, new slots, container/trade screens, drag equipment → action bar, generate_image/OpenRouter.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected | Error Handling |
|----------|---------------|----------|----------------|
| Grid select equippable | click kite shield | Offhand frame+line+caption moss; equipment selection cleared | non-equippable → no highlight |
| Equipment select | click occupied Weapon | brass frame + outer ring + 3 px brass line + bright caption; grid selection cleared; detail shows Drop / Unequip | empty slot → clears selection, detail hidden |
| Drag grid → valid slot | shield onto ghost Offhand (Flamberge equipped) | shield in Offhand, Flamberge back in grid, Weapon empty well "WEAPON" | invalid slot → refused, nothing changes |
| Drop on occupied slot | axe onto Weapon holding sword | swap, sword back to grid | — |
| Ring drop on Ring2 | Ring1 empty | ring lands in Ring2 (the slot dropped on) | — |
| Ring without drag | double-click/Equip ring | first empty ring, Ring1 first; both full → replaces Ring1 | — |
| Ring1 → Ring2 drag | both / one occupied | contents swap / move | non-ring pair → refused |
| Slot → grid drag | drag Armor onto grid area | unequipped to inventory | dropped elsewhere → nothing |
| Offhand ghost | Weapon OccupiesOffhand, Offhand empty | weapon icon 28 % gray-tinted in Offhand; click/double/right-click/drag act on Weapon | — |
| Right-click equipment | occupied slot | existing context-menu prefab with only "Unequip" | empty → nothing |
| Close screen | I / Esc / tab | selection + highlights cleared | — |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/Inventory/EquipmentSystem.cs` -- `Equip(int)` picks slot via `EquipmentRules.TryGetTargetSlot`; no slot-targeted equip or swap yet
- `Assets/_Game/Scripts/Inventory/EquipmentRules.cs` -- pure slot rules (`FitsSlot`, conflicts); tested in `Assets/Tests/EditMode/EquipmentSystemTests.cs`
- `Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs`, `EquipmentSlotUI.cs` -- current Button-based column
- `Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs` -- grid selection, context menu, detail panel owner
- `Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs`, `InventoryDetailActions.cs` -- grid drag source; detail Equip/Unequip/Drop
- `Assets/_Game/Prefabs/UI/Equipment/EquipmentPanel.prefab` (+ `EquipmentSlot.prefab`) nested in `Prefabs/UI/Inventory/InventoryUI.prefab`; `SlotOffhand` was an unpacked copy (not an `EquipmentSlot` instance), replaced by the builder
- `Assets/_Game/Editor/UI/UILayoutCapture.cs`, `Art/UI/UI_METRICS.md` -- preview + metrics
- `Assets/_Game/Art/Characters/Humanoids/models/base hero v2/` -- silhouette source model

## Tasks & Acceptance

**Execution:**
- [x] `Scripts/Inventory/EquipmentRules.cs` -- add `CanSwap(equipped, a, b)` (both items fit the other slot, empty allowed, a≠b) -- ring swap rule lives with the rules
- [x] `Scripts/Inventory/EquipmentSystem.cs` -- add `Equip(int index, EquipmentSlot slot)` (refuses `!FitsSlot`, same displacement/conflict flow; `Equip(int)` delegates) and `SwapEquipped(a, b)` (one `_onEquipmentChanged`) -- drop on a chosen slot
- [x] `Scripts/UI/Inventory/EquipmentPanelRules.cs` (new, pure) -- `IsValidTarget(item, slot)`, `IsValidSwapTarget`, `ShowsOffhandGhost(weapon, offhand)`, `ResolveFrame(selected, valid, hovered, dragging)` → Normal/Hover/Selected/Valid/ValidHover, `EmptyLabel(slot)`, `Caption(slot)`
- [x] `Scripts/UI/Inventory/EquipmentSlotUI.cs` -- replace Button with pointer/drag/drop handlers; background tint fields (normal/hover/selected as ItemSlotUI + valid + validHover), outer ring, caption, empty label, leader line refs; ghost cleanup in `OnDisable`
- [x] `Scripts/UI/Inventory/EquipmentUI.cs` -- owner link to `InventoryUI`, selection, held/drag item highlight, ghost, grid drop / slot swap / slot → grid (end-drag over grid rect) handling; drop unused `_itemDetailPanel` / `_invActions`
- [x] `Scripts/UI/Inventory/InventoryUI.cs` + `ItemSlotUI.cs` + `InventoryDetailActions.cs` -- shared selection, drag begin/end notify (optional `IItemDragListener`), equipment context menu (Unequip only), detail Drop/Unequip for equipped items, don't hide the detail on refresh while equipment is selected
- [x] `Tools/Blender/make_equipment_silhouette.py` + `Art/UI/Sprites/UI_EquipmentSilhouette.png` (+ outline sprite) -- base hero, A-pose, ortho front, flat fill; Sprite import
- [x] `Art/UI/Sprites/UI_LeaderDash.png` -- 6 on / 5 off dash, Tiled
- [x] `Editor/UI/InventoryEquipmentLayoutBuilder.cs` (new) -- idempotent builder for the prefab layout (panel, header, divider, silhouette, slots, captions, labels, rings, lines); run it
- [x] `Assets/Tests/EditMode/` -- tests for `CanSwap`, `Equip(index, slot)` ring targeting, `EquipmentPanelRules`
- [x] Docs -- `UILayoutCapture.Screens` if needed, `UI_METRICS.md`, `Scripts/UI/Inventory/CLAUDE.md`, `Art/UI/CLAUDE.md` catalog, `Tools/Blender/README.md`

**Acceptance Criteria:**
- Given the Inventory open, when captured with `UILayoutCapture.Capture("Inventory")`, then the left area matches the mockup layout with grid and detail panel unchanged.
- Given the Marek flow (EXPERIENCE.md Key Flows), when played in play mode, then each step shows the described state.
- Given any equipment slot, when hovered / selected / valid / empty, then the state is distinguishable without hue alone (ring, line width, label).

## Design Notes

Tints multiply the iron `SlotWell` frame, so moss can only be approximated (e.g. valid ≈ (0.75, 0.95, 0.6), valid-hover brighter); tune from the capture. Outer ring = a plain `Image` 3 px larger behind the slot (`#4A3C22` selected, `#3A4230` valid). Leader lines: `Image` Tiled with the dash sprite, pivot at the slot inner edge, rotated to the mockup endpoints (Helmet 160,244→248,260 · Necklace 416,330→298,322 · Armor 160,414→250,400 · Weapon 160,584→190,596 · Offhand 416,584→386,596 · Ring1 160,754→186,618 · Ring2 416,754→390,618). Slot → grid unequip is detected in the equipment slot's `OnEndDrag` (pointer inside the grid panel rect), so the shared `InventoryPanel.prefab` is untouched.

## Verification

**Commands:**
- EditMode tests (`run_tests` EditMode) -- expected: all pass
- `return Game.Editor.UILayoutCapture.Capture("Inventory");` -- expected: preview written, compared with the mockup

**Manual checks:**
- Play mode: Marek flow with Flamberge, kite shield, axe; ring double-click; Ring1↔Ring2 drag; right-click Unequip; console clean.

## Suggested Review Order

**Equip rules (the only place legality lives)**

- Slot-targeted equip: refused unless `FitsSlot`, then the old displacement flow.
  [`EquipmentSystem.cs:71`](../../Assets/_Game/Scripts/Inventory/EquipmentSystem.cs#L71)

- Ring1 ↔ Ring2 swap rule shared by the system and the highlight.
  [`EquipmentRules.cs:85`](../../Assets/_Game/Scripts/Inventory/EquipmentRules.cs#L85)

**Paper-doll controller**

- Entry point: selection, highlight sources, ghost and drag state in one class.
  [`EquipmentUI.cs:1`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs#L1)

- Refresh: ghost binding, selection follows its item across slots.
  [`EquipmentUI.cs:93`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs#L93)

- Drag end over the grid rect unequips; keeps the shared InventoryPanel prefab untouched.
  [`EquipmentUI.cs:216`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentUI.cs#L216)

- Pure display rules: frame precedence, ghost, action slot, labels.
  [`EquipmentPanelRules.cs:1`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentPanelRules.cs#L1)

**Shared selection and context menu (InventoryUI)**

- Equipment refreshed first so RefreshSlots sees the current selection.
  [`InventoryUI.cs:113`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L113)

- Unequip context menu reuses the prefab and blocker via `OpenContextMenu`.
  [`InventoryUI.cs:294`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L294)

- One selection across grid and doll; DropEquipped never drops a spare copy.
  [`InventoryUI.cs:407`](../../Assets/_Game/Scripts/UI/Inventory/InventoryUI.cs#L407)

**Slot view and drag plumbing**

- State painting (tint, ring, line width, caption) and pointer forwarding.
  [`EquipmentSlotUI.cs:1`](../../Assets/_Game/Scripts/UI/Inventory/EquipmentSlotUI.cs#L1)

- Grid drag begin/end reported to hosts that opt in.
  [`ItemSlotUI.cs:24`](../../Assets/_Game/Scripts/UI/Inventory/ItemSlotUI.cs#L24)

- Detail Drop / Unequip for an equipped item.
  [`InventoryDetailActions.cs:30`](../../Assets/_Game/Scripts/UI/Inventory/InventoryDetailActions.cs#L30)

**Layout and art**

- Idempotent prefab builder: positions, leader-line ends, override cleanup.
  [`InventoryEquipmentLayoutBuilder.cs:45`](../../Assets/_Game/Editor/UI/InventoryEquipmentLayoutBuilder.cs#L45)

- Silhouette render: arm fit by hand spread, outline baked in numpy.
  [`make_equipment_silhouette.py:1`](../../Tools/Blender/make_equipment_silhouette.py#L1)

**Tests**

- Slot-targeted equip, swap, valid targets, ghost, frame precedence.
  [`EquipmentPaperDollTests.cs:1`](../../Assets/Tests/EditMode/EquipmentPaperDollTests.cs#L1)
