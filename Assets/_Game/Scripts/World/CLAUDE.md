# CLAUDE.md — Assets/_Game/Scripts/World

> Loaded when Claude accesses files in this folder. Interaction, dialogue, containers,
> and world-entity persistence. Namespace: `Game.World`.

---

## What's here

| File | Role |
|------|------|
| `IInteractable` | Contract for anything the player can interact with (`CanInteract`, `Interact()`). Implemented by `InteractableObject`, `ContainerInteractable`, `ItemPickup`, `EntityPresence` (and its subclass `NPCPresence`). |
| `EntityPresence` | Base `IInteractable` for **any** entity (NPC, monster, …). Lives on `Entity_base.prefab` so every entity is discoverable by `InteractionSystem` and shows its world-space `EntityUI` on hover. Provides `NameTag` (null-guarded → `""` when `PersistentID.Entity` is null). **Lootable when dead AND non-empty:** `CanInteract => IsLootable` (`IsDead && HasLoot`), `InteractPrompt` = `"Loot"` when lootable (else `""`), and `Interact()`→`OpenLoot()` raises the shared `GameEventSO_ContainerOpenRequest` (`_onLootRequested`) with the entity's `InventorySystem` and `takeOnly = true` — reusing the container pipeline (`ContainerSystem`/`ContainerUI`), no second `IInteractable`. An empty corpse (no items, or all items looted) is inert — no `[E]` prompt, `Interact()` no-op. `HasLoot` reads `InventorySystem.Count > 0`, so a corpse becomes inert the moment its last item is taken. Exposes `IsAliveAndOutOfCombat` / `IsDead` helpers for subclasses. `NPCPresence` (Game.AI) subclasses it for dialogue. **Wiring gotcha:** `_onLootRequested` must be wired on BOTH `Entity_base.prefab` (inherited by monsters) and the NPC variant's `NPCPresence` (NPCs) — the NPC variant removes the inherited base component, so the base wiring does not propagate to it. |
| `InteractionSystem` | Player-side raycast that finds the focused `IInteractable` and dispatches `Interact()`. On focus change **or** an in-place verb/name change (e.g. a corpse becoming lootable) it toggles the target's `InteractionHighlight`, sets the global outline color, tints the crosshair and raises `OnInteractionFocusChanged` (`InteractionFocusData { target, verb, name }`, target null = no focus) for the HUD prompt card. After `Interact()` it forces a rescan next frame (closes the double-pickup window). `OnDisable` clears focus before its `_input` guard. No `OnGUI`. |
| `InteractionFocus` | **Static, pure.** Focus-change detection, verb/name resolution, `IsAlive` (Unity fake-null for destroyed targets), layer-bit math, outline/crosshair color selection. Tested directly by `InteractionSystemTests`. |
| `InteractionHighlight` | **Opt-in** outline on an interactable root. `_targets` = renderers to outline (empty → all child `MeshRenderer`/`SkinnedMeshRenderer`, collected in `Awake`); optional `_overrideColor`/`_color`. `SetHighlighted` ORs / clears the `Outline` rendering-layer bit (materials never swapped). Static `AnyHighlighted` lets `InteractionOutlineFeature` skip its passes; `OnDisable` clears a stale bit/count. |
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
- **Outline is opt-in:** add `InteractionHighlight` to an interactable root; list only the meshes to outline (e.g. a door handle); no component = prompt-only. Every world item pickup carries one. The outline layer bit comes from `InteractionConfig.outlineRenderingLayer` and must match `InteractionOutlineFeature.outlineLayer` on `PC_Renderer` (both = `Outline`, bit 2) — see `Scripts/Rendering/CLAUDE.md`.
- **Lock pattern:** `Lockable` is the data; a locked interactable routes a `GameEventSO` to a player-side system that owns `PlayerSkills` (`DoorInteractable`→`DoorSystem`, `ContainerInteractable`→`ContainerSystem`) for the skill check. Never read `PlayerSkills` directly from an interactable — keep the cross-system touch in the player-side resolver (the `Interact()` signature stays parameterless).
- Persisting unlocked/open state across save & scene reload is **not yet implemented** — deferred to `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md`.
