# Sprint Change Proposal — Inventory UX: Context Menu & Item Detail Panel
**Date:** 2026-03-14
**Project:** Unity-BMAD-test-rpg (Echoes of the Fallen)
**Workflow:** correct-course

---

## Section 1: Issue Summary

**Problem Statement:**
The current right-click-to-drop interaction in the inventory panel (implemented in
Story 4.4) is too immediate — one accidental right-click permanently drops an item
into the world. Additionally, the inventory panel shows items as icon-only grid
slots with no way to inspect item details without hovering for the name tooltip.

**Discovery Context:**
Identified post-Story 4.4 completion during design review. The direct drop is a UX
regression risk for players managing valuable items. The absence of an item detail
panel also limits the inventory's utility ahead of Epic 7 (Equipment & Economy),
which will need item stat inspection.

**Evidence:**
- `ItemSlotUI.OnPointerClick(Right)` currently calls `InventoryUI.DropItem(SlotIndex)`
  with no intermediate confirmation step
- `ItemSO` has `description`, `icon`, and `itemName` fields — all populated in existing
  item assets — but this data is not surfaced anywhere in the inventory UI
- Future Epic 7 features (equip, sell, inspect) will need the context menu pattern as
  an extensible entry point

---

## Section 2: Impact Analysis

**Epic Impact:**
- Epic 4 (Inventory, Items & Interaction) — `in-progress`: two stories affected
  (4.4 code superseded; new story 4.8 added). Epic can still complete as planned.
- Epic 7 (Equipment & Economy): positively impacted — context menu pattern provides
  the hook for future "Equip" and "Use" menu options. No rework needed in Epic 7.
- All other epics: unaffected.

**Story Impact:**
| Story | Status | Impact |
|-------|--------|--------|
| 4.3 Inventory Panel | review | Code will be modified by Story 4.8 (ItemSlotUI gains SetSelected, left-click handler) |
| 4.4 Item Drop Physics | done | Right-click OnPointerClick behavior superseded; drop logic preserved via context menu |
| 4.5 Item ScriptableObject | backlog → done | ItemSO fully implemented during 4.4; story closed |
| 4.8 Inventory Context Menu & Detail Panel | new → ready-for-dev | New story created |

**Artifact Conflicts:**
- `epics.md` — updated: new story added to Epic 4 story list ✅
- `sprint-status.yaml` — updated: 4.5 closed as done; 4.8 added as ready-for-dev ✅
- `4-4-item-drop-physics.md` — updated: change log note added about superseded behavior ✅
- Architecture doc: minor note warranted about context menu as the canonical inventory
  interaction pattern (extensible for Epic 7). Non-blocking.
- No PRD/GDD changes required.

**Technical Impact:**
- Modifies `ItemSlotUI.cs` and `InventoryUI.cs` (two files from Story 4.3/4.4)
- Creates `InventoryContextMenu.prefab` (new UI prefab)
- Adds `ItemDetailPanel` hierarchy to TestScene
- No changes to game logic layer (`InventorySystem`, `ItemPickup`, `ItemSO`)
- All 132 existing Edit Mode tests remain valid

---

## Section 3: Recommended Approach

**Selected Path: Option 1 — Direct Adjustment**

Modify the two affected UI scripts and add one new story. No rollback of completed
work. No MVP scope changes.

**Rationale:**
- `ItemSO.description`, `icon`, and `itemName` are already implemented — no new
  data layer work needed
- The context menu pattern is a natural extension of the existing `OnPointerClick`
  handler — low refactor risk
- The blocker+menu approach for context menus is a well-established Unity UI pattern
- Effort: **Low** | Risk: **Low** | Timeline impact: +1 story (~1 dev session)
- Context menu extensibility benefits Epic 7 at no extra cost

---

## Section 4: Detailed Change Proposals

### Change 1 — `sprint-status.yaml`
```
4-5-item-scriptable-object: done  # ItemSO fully implemented during story 4.4
4-8-inventory-context-menu-and-detail-panel: ready-for-dev
```

### Change 2 — `epics.md` (Epic 4 Stories list, append)
```
- As a player, I can right-click an inventory slot to open a context menu with
  actions (starting with "Drop Item"), and left-click a slot to select it and
  see its name, description, and icon in a detail panel on the right side of
  the inventory, so item management feels intentional and informative
```

### Change 3 — `4-4-item-drop-physics.md` (Change Log, append)
```
- 2026-03-14: Right-click direct-drop behavior (AC 4, ItemSlotUI.OnPointerClick →
  InventoryUI.DropItem) superseded by Story 4.8 which introduces a context menu.
  The drop action is preserved as a menu option. No code rollback needed — Story 4.8
  modifies ItemSlotUI and InventoryUI directly.
```

### Change 4 — New Story File Created
`_bmad-output/implementation-artifacts/4-8-inventory-context-menu-and-detail-panel.md`

Key technical decisions in story 4.8:
- Right-click → `ShowContextMenu(slotIndex, screenPos)` with blocker+panel pattern
- Left-click → `SelectSlot(slotIndex)` with `SetSelected(bool)` on `ItemSlotUI`
- Detail panel: `_detailIcon`, `_detailName`, `_detailDescription` wired in Inspector
- `RefreshSlots()` updated to restore selection state after slot rebuild
- `Close()` dismisses context menu + clears selection
- No changes to `InventorySystem`, `ItemPickup`, or `ItemSO`

---

## Section 5: Implementation Handoff

**Change Scope: Minor** — Direct implementation by development team.

**Handoff:** Dev agent (`bmad:bmgd:workflows:dev-story`) on Story 4.8.

**Implementation order:**
1. Close Story 4.5 in sprint-status ✅ (done)
2. Implement Story 4.8 (modifies ItemSlotUI, InventoryUI; creates InventoryContextMenu.prefab)
3. Continue with Stories 4.6 (tome-as-world-item) and 4.7 (trainer-look-at) as originally planned

**Success criteria:**
- Right-click opens context menu; "Drop Item" drops item and closes menu
- Left-click selects slot; detail panel shows icon, name, description
- Click outside context menu dismisses it
- Escape / inventory close dismisses menu and clears selection
- Drag-to-reorder still works; selection survives slot refresh if item still present
- All 132 existing Edit Mode tests continue to pass

---

*Generated by: correct-course workflow | claude-sonnet-4-6 | 2026-03-14*
