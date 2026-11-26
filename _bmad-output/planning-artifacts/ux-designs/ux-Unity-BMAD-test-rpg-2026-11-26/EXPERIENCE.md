---
name: Echoes of the Fallen — Inventory Equipment (paper doll)
status: final
created: 2026-11-26
updated: 2026-11-26
design: ./DESIGN.md
form_factor: PC (Windows, Steam), 1920×1080 reference, mouse + keyboard
scope: Inventory screen, equipment section. Item grid, filter tabs and detail panel are unchanged except where noted.
sources:
  - Assets/_Game/Scripts/UI/Inventory/CLAUDE.md
  - Assets/_Game/ScriptableObjects/Items/EquipmentSlot.cs
  - Assets/_Game/Art/UI/UI_METRICS.md
---

# EXPERIENCE.md — Inventory equipment section (paper doll)

> How the equipment section behaves. Its look (colours, sizes, sprites) is in [DESIGN.md](./DESIGN.md) and
> referenced here as `{token}`. The visual reference is [mockups/equipment-paperdoll-states.html](./mockups/equipment-paperdoll-states.html).
> When a mock and this file disagree, this file wins.

## Foundation

- **Platform:** PC (Windows x64, Steam). The game runs at a 1920×1080 reference with fixed pixel sizes
  (`UICanvas` is Screen Space Overlay with no `CanvasScaler`).
- **Input:** mouse + keyboard only. Gamepad and keyboard focus navigation are **out of scope** for this
  redesign.
- **UI system:** Unity uGUI + TextMeshPro. The section is `EquipmentUI` with seven `EquipmentSlotUI`
  under `InventoryUI` (an `IScreenPanel`, opened with **I**), and shares `ItemDetailPanelUI` with the grid.
  This document specifies only what changes from that behaviour.
- **Slots:** exactly the `EquipmentSlot` enum: Weapon, Offhand, Helmet, Armor, Ring1, Ring2, Necklace.

## Information Architecture

The Inventory tab keeps its three areas, left to right:

1. **Equipment (paper doll)**: *what the hero wears*. Seven slots around a hero silhouette, each linked
   to its body part.
2. **Item grid** with filter tabs and gold: *what the hero carries*.
3. **Item detail panel**: *how good the selected item is* (category, stats, description, value) and what
   you can do with it (Drop / Use / Equip / Unequip).

The equipment section shows no stats. To judge an item, select it and read the detail panel. To compare,
select the equipped item, then the candidate.

## Voice and Tone

Plain, short nouns in keeping with the sober UI; no flavour text in the equipment section.

| Place | Text |
|-------|------|
| Panel header | EQUIPMENT |
| Slot captions | Helmet · Armor · Weapon · Ring · Necklace · Offhand · Ring |
| Empty well labels | HELMET · ARMOR · WEAPON · OFFHAND · NECK · RING |
| Context menu on an equipment slot | Unequip |
| Detail panel button for an equipped item | Unequip (existing `InventoryDetailActions` label) |

## Component Patterns

**Equipment slot** behaves like an inventory grid slot in a fixed place:

| Action | On an occupied slot | On an empty slot |
|--------|---------------------|------------------|
| Hover | Frame `{colors.hover}` | Frame `{colors.hover}` |
| Left click | **Selects** it: frame and leader line `{colors.selected}`; the detail panel shows the item with Drop / Unequip. Any grid selection is cleared (one selection across grid + equipment). | Clears the selection and the detail panel shows nothing. [ASSUMPTION] |
| Double-click | Unequips (item returns to the grid) | Nothing |
| Right-click | Opens a context menu with **Unequip** (same menu prefab and blocker as the grid) | Nothing |
| Drag start | Picks up the item with the standard drag ghost | Nothing |
| Drop of a grid item | Swap: see State Patterns | Equips it |

**Grid slot (changes only).** When an *equippable* grid item is selected or being dragged, every
equipment slot it can go into shows the **valid** state (`{colors.valid}` frame, caption and leader line).
The highlight clears when the selection clears or the drag ends.

**Detail panel.** Unchanged. It shows the selected item, whether it was selected in the grid or the
equipment panel.

## State Patterns

| State | What the player sees |
|-------|----------------------|
| Occupied | Item icon in the well; caption under it |
| Empty | Dark well with the slot name (`{typography.empty-label}`) |
| Selected | Brass frame + outer ring, brass 3 px leader line, caption in `{colors.text}` |
| Valid target | Moss frame + outer ring, moss leader line, caption in `{colors.text}` |
| Valid target under the cursor while dragging | Frame `{colors.valid-hover}` |
| Offhand taken by a two-handed weapon / bow (`OccupiesOffhand`) | Offhand slot shows the weapon's icon faded (28 %, desaturated). Clicking or right-clicking it acts on the **Weapon** slot. [ASSUMPTION] |

**Equip rules surfaced by the UI.** The UI never adds equip rules; it shows what `EquipmentSystem` decides.
- **Drop onto an occupied slot = swap.** The dropped item is equipped and the previous one returns to the grid.
- **Shield onto the faded Offhand** (two-hander equipped): the shield is equipped and the two-handed weapon
  returns to the grid, leaving Weapon empty.
- **Rings without drag** (double-click in grid, context-menu Equip, detail-panel Equip): the ring goes into
  the first empty ring slot, Ring 1 first. With both full it replaces Ring 1.
- **Drop onto a slot that isn't valid**: refused. The item stays where it was and nothing changes.
- **Drag from an equipment slot onto the grid**: unequips. The item goes back to the inventory.

**Screen open / close.** On open, the doll shows the current equipment with nothing selected. Closing
(I, Escape, another tab) clears the selection and any highlight. Equipment changes made outside the screen
(e.g. picking up while open) repaint through `_onEquipmentChanged`, as today.

## Interaction Primitives

- **Click** select · **double-click** unequip / equip · **right-click** context menu · **drag and drop**
  between grid and equipment, both ways.
- Double-click threshold 0.3 s (existing `EquipmentSlotUI`).
- Hover and selection use the same tint pipeline as `ItemSlotUI`. Valid is one new tint.

## Input Schemes

Mouse + keyboard. The only key is **I** to open and close the Inventory, plus Escape to close.
No new bindings. Gamepad support is out of scope; if it is added later, this section needs focus order
and button prompts.

## Game Feel & Juice

Kept quiet, in line with the sober art direction: no animation, scale pop or glow on equip. Feedback is the
instant icon change in the slot and the state colours.

## Accessibility Floor

- No state relies on colour alone. Selected adds an outer ring and a thicker line, and valid recolours the
  frame, caption and line at once.
- Every action has a mouse path that needs no drag: click + Equip / Unequip button, double-click, or
  right-click menu. Drag and drop is a shortcut, never the only way.
- Captions are 17 px at 1080p, the smallest text in the section. Check them in game for legibility over the
  panel.

## HUD & Diegetic UI

The equipment panel is non-diegetic menu UI. It is styled diegetically (wood, leather, iron, ink lines, a
flat figure, like a page from an armourer's ledger) as `ART_DIRECTION.md` asks for all menus. The panel
doesn't touch the HUD; the action bar keeps working as a drop target from the grid.

## Key Flows

### Marek trades the Flamberge for sword and board

Marek is about two hours in and carries the two-handed **Flamberge**. He has just looted a **kite shield**
from a bandit.

1. He presses **I**. On the paper doll he sees the Flamberge in Weapon and its faded icon in Offhand: both
   hands are taken.
2. He clicks the kite shield in the grid. The detail panel shows its block value, and the **Offhand slot and
   its leader line turn moss green**.
3. He wonders whether to give up the big sword and clicks the **Weapon** slot. It turns brass and the
   detail panel shows the Flamberge's numbers, so he can compare.
4. He decides and **drags the shield onto the Offhand slot**. It lights brighter under the cursor.
5. **Climax:** he drops it. The shield appears in Offhand, the Flamberge goes back to the grid, and the
   Weapon slot becomes an empty well reading WEAPON. He drags his old **axe** from the grid into Weapon, and the
   doll shows axe and shield in both hands.
6. He presses **I** and walks on with a one-hander and a shield.

## Open Items (non-blocking)

- **Drag between two equipment slots** (e.g. Ring 1 → Ring 2): not decided. Default: refused unless both
  slots accept the item, in which case it swaps them. [ASSUMPTION]
- **Drag from an equipment slot onto the action bar**: not decided. Default: not supported, as now.
- **Equip / unequip sound**: none specified. Add one later through `SfxPlayer` if the screen feels too silent.
- **Silhouette sprite**: a new flat asset. Its shape must leave the hands near y ≈ 600 and the
  head near y ≈ 260 so the leader lines in DESIGN.md land on the body.
- **`UI_Panel` sprite**: in the catalog but not generated yet. Until it exists, the panel uses the
  current flat panel colour.
