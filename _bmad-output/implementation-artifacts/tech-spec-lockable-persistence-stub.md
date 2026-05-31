---
title: 'Lockable / Door State Persistence (STUB)'
slug: 'lockable-persistence'
created: '2026-05-31'
status: 'stub'
stepsCompleted: []
parent_spec: 'tech-spec-door-system'
---

# Tech-Spec (STUB): Lockable / Door State Persistence

> **This is a deferred stub split out of the Door System spec (`door-system`).** It captures intent so
> it can be picked up later — it is NOT ready for development and needs its own quick-spec pass.

## Problem Statement

The Door System ships `Lockable` (lock data + `Unlock()`), `DoorInteractable` (open/closed rotation),
and `DoorSystem` (skill-gated unlock) **without persistence**. On save + scene reload:

- an unlocked door reverts to **locked**, and
- an open door reverts to **closed**,
- a picked-lock container reverts to **locked** (and the container's lock is re-checked every open today —
  it never flips to unlocked at all).

**Intended behavior:** once the player unlocks a door OR a container, it stays unlocked across save/reload;
a door's open/closed state should also persist.

## Intended Approach (to validate during the real spec)

- Persist via **`WorldStateManager`** facts (the project's single source of runtime world truth — never
  cache world state in MonoBehaviours), keyed per instance. Doors are not enemies/NPCs/containers, so they
  currently have no `PersistentID`; this work likely needs a per-instance persistence key for doors
  (either add `PersistentID`, or a lighter door-specific id) — decide in the real spec.
- Likely facts: `<doorKey>_Unlocked`, `<doorKey>_Open`, and `<containerKey>_Unlocked`.
- `Lockable` already centralizes the unlocked flag — a persistence hook likely lives here or in
  `DoorInteractable` / `DoorSystem`, reading/writing `WorldStateManager` on enable/unlock/open.
- **Reconcile the door/container asymmetry** noted in the parent spec: give containers a real `Unlock()`
  flip too (today `ContainerSystem` only receives `InventorySystem` via the event, not the specific
  `Lockable`), so both honor a persisted unlocked state.

## Out of Scope (until specced)

- Exact fact-key naming scheme and `PersistentID` decision for doors.
- Save/load serialization format changes (owned by the Save System / Steam Cloud work).

## Open Questions

- Add `PersistentID` to doors, or a lighter door-only key? (Doors can be numerous — weigh cost.)
- Does open/closed state need to persist, or only the unlocked flag? (Parent-spec answer leaned: persist
  unlocked for both; open/closed for doors is desirable but confirm.)
- How does the container side get a reference to its `Lockable` so it can flip + persist unlocked?
