# Sprint Change Proposal — 2026-03-19

**Project:** Unity-BMAD-test-rpg
**Workflow:** Correct Course
**Status:** Approved

---

## Section 1: Issue Summary

**Problem statement:** During the code review of story 7-1 (Equipment Slots), the `EquipmentPanel` was implemented as an embedded left-side child inside `InventoryPanel` via a `HorizontalLayoutGroup`. The original intent was always for `EquipmentPanel` to be a **separate, peer panel** positioned to the left of `InventoryPanel` — not a sub-component of it. Additionally, no organizational wrapper was present in `UICanvas` to group inventory-related panels, creating a flat root structure that will not scale as more screens (CraftingUI, ShopUI) are added.

**Discovered:** During sprint review of story 7-1 (status: review), prior to story 7-2 beginning development.

**Evidence:**
- Story 7-1 AC5 specified: *"The `EquipmentPanel` GameObject is a sibling of the existing `InventoryGrid` container inside `UICanvas/InventoryPanel`"* — this contradicts the intended separate-panel layout
- The `InventoryPanel` HorizontalLayoutGroup was repurposed to wrap both grid and equipment, coupling two conceptually independent panels
- `ItemDetailPanel` (a peer panel) already existed at UICanvas root, establishing the correct pattern of sibling panels — `EquipmentPanel` should follow the same pattern

---

## Section 2: Impact Analysis

### Epic Impact
- **Epic 7** (in-progress): Can still complete as planned. Only story 7-1 task 4.3 requires rework. All scripts, ScriptableObjects, and events from 7-1 are correct and unaffected.

### Story Impact
| Story | Status | Impact |
|-------|--------|--------|
| 7-1 Equipment Slots | review → **in-progress** | AC5 rewrite + task 4.3 re-opened |
| 7-2 Double-Click Primary Action | ready-for-dev | **Unaffected** — script-only changes |
| 7-3 Equipped Item Stat Effects | backlog | **Unaffected** |
| 7-4 Equipment Visual Update | backlog | **Unaffected** |

### Artifact Conflicts
| Artifact | Impact |
|----------|--------|
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | Create `InventoryUI` wrapper GO; reparent `InventoryPanel`, `ItemDetailPanel`, extracted `EquipmentPanel` under it |
| `Assets/_Game/Prefabs/UI/Inventory/InventoryPanel.prefab` | Remove `HorizontalLayoutGroup`; `EquipmentPanel` is no longer a child — `InventoryGrid` becomes its only child again |
| `_bmad-output/implementation-artifacts/7-1-equipment-slots.md` | AC5 rewrite, task 4.3 re-opened, dev notes updated |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | 7-1 moved back to `in-progress` |

### Technical Impact
None. No C# scripts, no ScriptableObjects, no events, no tests require changes. The correction is purely a **prefab layout reorganization**.

---

## Section 3: Recommended Approach

**Option selected: Direct Adjustment**

Modify story 7-1 to reflect the corrected spec, then re-implement task 4.3 in Unity. All other tasks in 7-1 and all subsequent stories are unaffected.

| Dimension | Assessment |
|-----------|-----------|
| Effort | Low — one prefab restructure, one story file update |
| Risk | Low — no script changes, no event wiring changes |
| Timeline impact | Minimal — 7-2 cannot start until 7-1 returns to `review`, but the correction is contained |
| Long-term value | High — `InventoryUI` wrapper establishes the scalable pattern for future screens (CraftingUI, ShopUI, etc.) |

---

## Section 4: Detailed Change Proposals

### A — Story 7-1 AC5 Rewrite

**File:** `_bmad-output/implementation-artifacts/7-1-equipment-slots.md`
**Section:** AC 5 — Equipment Panel Tab in Inventory Screen

**OLD layout spec:**
```
InventoryPanel (HorizontalLayoutGroup)
├── EquipmentPanel   ← new, left side (~200px wide)
│   └── [6 EquipmentSlotUI prefabs]
└── InventoryGrid    ← existing, right side (unchanged)
```

**NEW layout spec:**
```
UICanvas
├── ActionBar                        ← unchanged, stays at root
└── InventoryUI                      ← new empty wrapper GO
    ├── EquipmentPanel               ← LEFT (~200px wide), direct child of InventoryUI
    │   └── [6 EquipmentSlotUI prefabs]
    ├── InventoryPanel               ← CENTER, internal grid unchanged
    │   └── InventoryGrid
    └── ItemDetailPanel              ← RIGHT, moved from UICanvas root into InventoryUI
```

**Key wording changes:**
- `EquipmentPanel` is a **sibling** of `InventoryPanel` and `ItemDetailPanel`, NOT a child of `InventoryPanel`
- `InventoryUI` is a new empty wrapper GO (no MonoBehaviour) grouping all inventory-screen panels
- `InventoryPanel` HorizontalLayoutGroup is reverted — `InventoryGrid` is its only child again

**Rationale:** EquipmentPanel is a peer panel with independent positioning, consistent with how `ItemDetailPanel` is structured. The `InventoryUI` wrapper enables future screens to follow the same pattern without polluting UICanvas root.

---

### B — Story 7-1 Task 4.3 Re-opened

**File:** `_bmad-output/implementation-artifacts/7-1-equipment-slots.md`
**Section:** Tasks / Subtasks — Task 4

Task 4 is re-opened. Only task 4.3 requires rework:

| Task | Was | Now |
|------|-----|-----|
| 4.1 Create `EquipmentPanel.prefab` | [x] done | [x] remains done |
| 4.2 Create `EquipmentSlot.prefab` | [x] done | [x] remains done |
| 4.3 Prefab layout wiring | [x] done (wrong) | [ ] **re-opened** |
| 4.4 Wire EquipmentUI refs in Inspector | [x] done | [x] remains done |
| 4.5 InventoryUI script ref + Open() | [x] done | [x] remains done |

New task 4.3 description:
> Create `InventoryUI` empty wrapper GO in UICanvas; reparent `InventoryPanel` and `ItemDetailPanel` under it; extract `EquipmentPanel` from inside `InventoryPanel` and place it as first child of `InventoryUI` (left side, before `InventoryPanel`). Revert `InventoryPanel` — remove `HorizontalLayoutGroup`; `InventoryGrid` is its only child again.

---

### C — Story 7-1 Dev Notes Addition

**File:** `_bmad-output/implementation-artifacts/7-1-equipment-slots.md`
**Section:** Dev Notes — new entry

```markdown
### UICanvas / InventoryUI Structure

The inventory-related panels live under an **`InventoryUI`** empty wrapper GO (child of UICanvas).
This is a pure organizational GameObject — it carries no MonoBehaviour script.

UICanvas
├── ActionBar
└── InventoryUI        ← empty GO, groups all inventory-screen panels
    ├── EquipmentPanel ← LEFT, has EquipmentUI.cs
    ├── InventoryPanel ← CENTER, has InventoryUI.cs
    └── ItemDetailPanel← RIGHT, has ItemDetailPanelUI.cs

`InventoryPanel` internally contains only `InventoryGrid` — the HorizontalLayoutGroup
that previously wrapped EquipmentPanel is removed.
Future screens (CraftingUI, ShopUI) get their own wrapper GOs at the same level as `InventoryUI`.
```

---

### D — sprint-status.yaml

```yaml
# OLD
7-1-equipment-slots: review

# NEW
7-1-equipment-slots: in-progress
```

---

## Section 5: Implementation Handoff

**Change scope: Minor** — development team implements directly, no PO/SM/Architect involvement needed.

**Implementer:** Dev agent (game-dev)
**Workflow to use:** `/bmad:bmgd:workflows:dev-story` on story 7-1

**Execution order:**
1. Apply story file changes (AC5, task 4.3, dev notes)
2. Update `sprint-status.yaml` (7-1 → in-progress)
3. In Unity: restructure UICanvas prefab per new AC5 layout
4. Validate: open inventory in Play Mode — EquipmentPanel left, InventoryGrid center, ItemDetailPanel right
5. Move 7-1 back to `review`, then proceed to 7-2

**Success criteria:**
- `UICanvas` has `InventoryUI` wrapper GO with three children in order: `EquipmentPanel`, `InventoryPanel`, `ItemDetailPanel`
- `InventoryPanel` contains only `InventoryGrid` (no HorizontalLayoutGroup wrapping EquipmentPanel)
- `ActionBar` remains a direct child of `UICanvas` root
- All existing 183 EditMode tests still pass
- Play Mode: inventory screen opens showing Equipment panel on left, inventory grid center, item detail on right
