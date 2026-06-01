# CLAUDE.md — Assets/_Game/Scripts/World

> Loaded when Claude accesses files in this folder. Interaction, dialogue, containers,
> and world-entity persistence. Namespace: `Game.World`.

---

## What's here

| File | Role |
|------|------|
| `IInteractable` | Contract for anything the player can interact with (`CanInteract`, `Interact()`). Implemented by `InteractableObject`, `ContainerInteractable`, `ItemPickup`, `NPCPresence`. |
| `InteractionSystem` | Player-side raycast that finds the focused `IInteractable` and dispatches `Interact()`. |
| `InteractableObject` | Generic scene interactable. |
| `DialogueSystem` | Drives NPC dialogue flow (start nodes, choices) using the NPC's memory/graph components. |
| `ContainerSystem` / `ContainerInteractable` | Lootable container state + its interactable surface. Reads lock data from a sibling `Lockable` (optional). |
| `Lockable` | Reusable lock-data holder (`IsLocked`, `RequiredSkillId`, `LockedPrompt`) + `Unlock()`. Single source of lock truth shared by doors and containers via `GetComponent`; absence = never locked. |
| `DoorInteractable` / `DoorSystem` | `IInteractable` door that rotates its `Visual` child open/closed (re-closeable). Unlocked doors toggle locally; locked doors raise `OnDoorOpenRequested` to the player-side `DoorSystem`, which owns `PlayerSkills` and unlocks+opens on a passing skill check. |
| `PersistentID` | Marks an entity as permanently tracked by `WorldStateManager`. On `Awake` deactivates silently if its `KilledFact` is already set. Call `RegisterDeath()` before death effects. |
| `TopicUnlockEvaluator` | **Static, pure.** Evaluates memory unlock/invalidation by querying `WorldStateManager` world facts. No instance state. |

---

## Rules

- **Every world enemy, NPC, and container MUST have a `PersistentID` with a `KilledFact` assigned** — otherwise death/looted state won't persist across save/scene reload. (`_guid` string is gone; use `_killedFact`.)
- Persistence facts are read/written only through `WorldStateManager` → see `Scripts/Core/CLAUDE.md`.
- New interactables implement `IInteractable` and are discovered by `InteractionSystem`'s raycast — no manual registration.
- **Lock pattern:** `Lockable` is the data; a locked interactable routes a `GameEventSO` to a player-side system that owns `PlayerSkills` (`DoorInteractable`→`DoorSystem`, `ContainerInteractable`→`ContainerSystem`) for the skill check. Never read `PlayerSkills` directly from an interactable — keep the cross-system touch in the player-side resolver (the `Interact()` signature stays parameterless).
- Persisting unlocked/open state across save & scene reload is **not yet implemented** — deferred to `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md`.
