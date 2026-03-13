# Sprint Change Proposal — 2026-03-13

**Project:** Unity-BMAD-test-rpg (Echoes of the Fallen)
**Author:** Valentin
**Generated:** 2026-03-13
**Workflow:** correct-course

---

## Section 1: Issue Summary

### Problem Statement
The current roadmap defers all inventory, item, and interaction systems until Epic 6, after world-building (Epic 4) and quests (Epic 5). This creates a foundational gap: items (tomes, trainers) already exist as world objects in Epic 3 with ad-hoc proximity-based interaction, and all future epics (quests with item rewards, economy with item shops, crafting with item inputs) depend on a proper item system existing first.

Additionally, the current interaction model (proximity triggers) is inconsistent with the intended Gothic-style "look-at to interact" design that should be the universal interaction language for all interactable objects in the world.

### Context
- Discovered: After Epic 3 completion (all stories 3-1 through 3-6 done)
- Trigger: Strategic planning decision before starting Epic 4
- Timing: Ideal — no in-flight stories disrupted, clean insertion point

### Evidence
- `TomePickup.cs` and `Trainer_Master` use proximity/trigger-zone activation — inconsistent with intended look-at model
- Epic 6 (Inventory & Economy) had `"As a player, I have an inventory that holds all items I collect"` as its first story — this is too late if quests (Epic 5) can already reward items
- Sprint status shows epic-4 fully in backlog — zero rework cost to resequence

---

## Section 2: Impact Analysis

### Epic Impact
| Epic | Impact |
|------|--------|
| Epic 3 (done) | No changes — complete as-is |
| Former Epic 4 (World & Exploration) | Shifted to Epic 5, unchanged in content |
| Former Epic 5 (Quest & Dialogue) | Shifted to Epic 6, dependency reference updated |
| Former Epic 6 (Inventory & Economy) | Shifted to Epic 7, renamed "Equipment & Economy", scope trimmed (basic inventory moves to new Epic 4) |
| Former Epic 7 (Crafting & Stealth) | Shifted to Epic 8, dependency reference updated |
| Former Epic 8 (Content & Polish) | Shifted to Epic 9, unchanged in content |
| **New Epic 4 (Inventory, Items & Interaction)** | **Inserted — 7 stories** |

### Story Impact
- **New stories (7):** Look-at interaction system, item pickup, inventory panel, item drop physics, ItemSO definition, Tome as world item, Trainer look-at interaction
- **Removed story:** "As a player, I have an inventory that holds all items I collect" from former Epic 6 (now delivered in Epic 4)
- **Refactored objects:** `TomePickup.cs` → world item + look-at; `Trainer_Master` → look-at activation

### Technical Impact
- New `InteractionSystem` component (camera-center raycast, crosshair UI)
- New `Item` MonoBehaviour + `ItemSO` ScriptableObject
- New `Inventory` system (runtime data) + `InventoryPanel` UI
- `TomePickup.cs` refactor: remove proximity trigger, implement `IInteractable` interface
- `Trainer_Master` refactor: remove trigger zone, implement `IInteractable` interface
- Rigidbody physics required on all droppable world items

### Artifact Conflicts
| Artifact | Change Required |
|----------|----------------|
| `_bmad-output/epics.md` | Epic table renumbered, new Epic 4 added, dependencies updated |
| `_bmad-output/implementation-artifacts/sprint-status.yaml` | Epic 4 replaced with 7 new stories; epics 4→8 renumbered to 5→9 |
| `game-architecture.md` | Interaction system architecture section needed |
| `Assets/_Game/Scripts/World/TomePickup.cs` | Refactor to IInteractable + look-at |
| `Assets/_Game/Scripts/World/` (Trainer) | Refactor to IInteractable + look-at |

---

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment**

### Rationale
- Epic 3 is complete — no rollback needed
- Epic 4 is fully in backlog — zero disruption cost
- The new Epic 4 is self-contained and unblocks all downstream epics more effectively
- Look-at interaction is architecturally cleaner than scattered proximity triggers
- Estimated effort: **Medium** (7 new stories + 2 refactors)
- Risk: **Low** — no completed work undone, new system is additive

### Trade-offs Considered
- Adds one new epic (9 total vs 8) — acceptable, scope is well-bounded
- World & Exploration (now Epic 5) slightly delayed — acceptable since it has no dependency on inventory
- Equipment & Economy (now Epic 7) naturally fits after both item system (Epic 4) and NPC framework (Epic 6)

---

## Section 4: Detailed Change Proposals

### 4.1 `epics.md` — Epic Table

**OLD:**
```
| 4 | World & Exploration   | Epic 1       |
| 5 | Quest & Dialogue      | Epic 4       |
| 6 | Inventory & Economy   | Epic 3, 5    |
| 7 | Crafting & Stealth    | Epic 6       |
| 8 | Content & Polish      | All          |
```

**NEW:**
```
| 4 | Inventory, Items & Interaction | Epic 3       |
| 5 | World & Exploration            | Epic 1       |
| 6 | Quest & Dialogue               | Epic 5       |
| 7 | Equipment & Economy            | Epic 4, 6    |
| 8 | Crafting & Stealth             | Epic 7       |
| 9 | Content & Polish               | All          |
```

---

### 4.2 `epics.md` — New Epic 4 Section (insert before current Epic 4)

```markdown
## Epic 4: Inventory, Items & Interaction

### Goal
Establish the item system, inventory, and look-at interaction model that all
future systems (economy, crafting, quests with item rewards) depend on.

### Scope
**Includes:** Look-at interaction system (camera-center raycast with visual cue),
world item system (pickable/interactable GameObjects with Rigidbody physics),
basic inventory (pick up, drop, move items), simple inventory UI panel,
item data ScriptableObject, refactor TomePowerStrike as a world item activated
via look-at, refactor Trainer_Master NPC interaction to look-at model.

**Excludes:** Equipment slots and stat effects (Epic 7), shop NPCs and gold
economy (Epic 7), crafting items (Epic 8), quest item rewards (Epic 6).

### Dependencies
Epic 3 (progression system — tomes and trainers already implemented there,
need refactoring; item pickup could award XP in future).

### Deliverable
Player can look at world objects (items, NPCs) and see a crosshair interaction
cue. Pressing interact picks up items into a simple inventory panel. Dropping
an item spawns it in the world with physics. Tome and trainer interaction now
use the unified look-at model.

### Stories
- As a player, I see a crosshair/reticle at the center of the screen that
  highlights when I am looking at an interactable object, so I always know
  what I can interact with
- As a player, I can press Interact (E) while looking at a world item to pick
  it up into my inventory, so items enter my possession naturally
- As a player, I can open an inventory panel (I key) showing all held items,
  and drag/move them within it
- As a player, I can drop an item from my inventory and it falls to the floor
  with physics, so items feel like real objects in the world
- As a developer, items are defined by an ItemSO ScriptableObject (name, icon,
  description, isStackable) and any world item has an Item component + Rigidbody
- As a player, the TomePowerStrike is now a world item I look at and interact
  with to trigger skill learning, replacing the old proximity trigger
- As a player, the Trainer_Master NPC is activated by looking at them and
  pressing Interact, replacing the old proximity/trigger zone model
```

---

### 4.3 `epics.md` — Renumber Epics 4→8 to 5→9

| Old Header | New Header | Dependency Change |
|-----------|------------|-------------------|
| `## Epic 4: World & Exploration` | `## Epic 5: World & Exploration` | None |
| `## Epic 5: Quest & Dialogue` | `## Epic 6: Quest & Dialogue` | "Epic 4" → "Epic 5" |
| `## Epic 6: Inventory & Economy` | `## Epic 7: Equipment & Economy` | "Epic 3, 5" → "Epic 4, 6"; remove inventory story; update scope |
| `## Epic 7: Crafting & Stealth` | `## Epic 8: Crafting & Stealth` | "Epic 6" → "Epic 7" |
| `## Epic 8: Content & Polish` | `## Epic 9: Content & Polish` | "All" unchanged |

Epic 7 scope change — remove from Includes:
> ~~"Unlimited inventory system"~~ (delivered in Epic 4)

Epic 7 story removal:
> ~~"As a player, I have an inventory that holds all items I collect"~~ (delivered in Epic 4)

---

### 4.4 `sprint-status.yaml` — Full Replacement of epic-4 through epic-8

**NEW epic-4 (7 stories):**
```yaml
epic-4: backlog
4-1-look-at-interaction-system: backlog
4-2-item-pickup: backlog
4-3-inventory-panel: backlog
4-4-item-drop-physics: backlog
4-5-item-scriptable-object: backlog
4-6-tome-as-world-item: backlog
4-7-trainer-look-at-interaction: backlog
epic-4-retrospective: optional
```

**Epics 5→9:** Renumbered from former 4→8, story IDs updated to match new epic numbers. Full content in sprint-status.yaml.

---

## Section 5: Implementation Handoff

### Scope Classification: **Moderate**
Requires backlog reorganization (epics.md + sprint-status.yaml edits) before development begins.

### Handoff Plan

| Role | Responsibility |
|------|---------------|
| **Scrum Master (create-story)** | Generate story files for each of the 7 new Epic 4 stories in order |
| **Developer (dev-story)** | Implement stories sequentially; start with 4-1 (look-at system) as it unblocks all others |
| **Developer** | Refactor `TomePickup.cs` as part of story 4-6; refactor trainer as part of story 4-7 |

### Implementation Order (within Epic 4)
1. `4-5-item-scriptable-object` — define ItemSO first (other stories depend on item data model)
2. `4-1-look-at-interaction-system` — core interaction, needed before pickup works
3. `4-2-item-pickup` — depends on interaction system + ItemSO
4. `4-3-inventory-panel` — depends on item pickup
5. `4-4-item-drop-physics` — depends on inventory panel
6. `4-6-tome-as-world-item` — refactor, depends on item system
7. `4-7-trainer-look-at-interaction` — refactor, depends on interaction system

### Success Criteria
- Player can look at any `IInteractable` and see visual feedback
- All picked-up items appear in inventory panel
- Dropped items fall with physics and can be picked up again
- TomePowerStrike and Trainer_Master use the new interaction model
- Existing Epic 3 progression tests still pass

---

*Sprint Change Proposal generated by correct-course workflow — 2026-03-13*
