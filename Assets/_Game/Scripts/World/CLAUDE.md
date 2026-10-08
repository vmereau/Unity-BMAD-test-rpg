# CLAUDE.md — Assets/_Game/Scripts/World

> Loaded when Claude accesses files in this folder. Interaction, dialogue, containers,
> and world-entity persistence. Namespace: `Game.World`.

---

## What's here

| File | Role |
|------|------|
| `IInteractable` | Contract for anything the player can interact with (`CanInteract`, `Interact()`). Implemented by `InteractableObject`, `ContainerInteractable`, `ItemPickup`, `EntityPresence` (and its subclass `NPCPresence`). |
| `EntityPresence` | Base `IInteractable` for every entity (namespace `Game.World`, but the file is `Scripts/AI/EntityPresence.cs`). Corpse loot + `NPCPresence` dialogue subclass: `Scripts/AI/CLAUDE.md`. |
| `InteractionSystem` | Player-side raycast that finds the focused `IInteractable` and dispatches `Interact()`. On focus change **or** an in-place verb/name change (e.g. a corpse becoming lootable) it toggles the target's `InteractionHighlight`, sets the global outline color, tints the crosshair and raises `OnInteractionFocusChanged` (`InteractionFocusData { target, verb, name, illegal }`, target null = no focus) for the HUD prompt card. Legality is part of change detection (an owner dying flips it in place). **Theft hook:** on E, if the target's sibling `Ownership.IsIllegal` (re-evaluated at press time) it raises `OnTheftCommitted` (`TheftCommittedData { theftId, thief, position, owner, kind, objectName }`, `theftId` from a static counter reset on play) **before** `Interact()` — a pickup destroys itself. After `Interact()` it forces a rescan next frame (closes the double-pickup window). `OnDisable` clears focus before its `_input` guard. No `OnGUI`. |
| `InteractionFocus` | **Static, pure.** Focus-change detection, verb/name resolution, `IsAlive` (Unity fake-null for destroyed targets), layer-bit math, outline/crosshair color selection. Tested directly by `InteractionSystemTests`. |
| `Ownership` | Owner marker (`NPCEntity _owner`) on an `ItemPickup` / `ContainerInteractable` / `DoorInteractable` root; `Kind` (`TheftKind`) derived from the sibling in `Awake`. `IsIllegal` = owned and owner's `NPCEntity.KilledFact` not set in `WorldStateManager` (null fact → always illegal, warned), except an **open** door (closing is free). Pure rule `IsIllegalInteraction` (`OwnershipTests`). Illegal focus → red prompt + `InteractionConfig.illegalOutlineColor` outline (wins over the per-object colour); `ItemPickup` verb becomes `Steal`, containers / doors keep theirs. Interactables never know about witnesses. Locked owned doors / containers: the attempt itself is the theft. |
| `InteractionHighlight` | **Opt-in** outline on an interactable root. `_targets` = renderers to outline (empty → all child `MeshRenderer`/`SkinnedMeshRenderer`, collected in `Awake`); optional `_overrideColor`/`_color`. `SetHighlighted` ORs / clears the `Outline` rendering-layer bit (materials never swapped). Static `AnyHighlighted` lets `InteractionOutlineFeature` skip its passes; `OnDisable` clears a stale bit/count. |
| `InteractableObject` | Generic scene interactable. |
| `DialogueSystem` | Drives NPC dialogue flow (start nodes, choices) using the NPC's memory/graph components. Requests arriving while a dialogue is open are **ignored** (warning). `NPCDialogueRequestData.forcedLine` non-empty → NPC-initiated dialogue: `DialogueUI.OpenLine(npcName, line)` (no topics; any advance / Escape closes) — used by theft confrontations (`EntityBrain` Confronting). |
| `ContainerSystem` / `ContainerInteractable` | Lootable container state + its interactable surface. Reads lock data from a sibling `Lockable` (optional). |
| `Lockable` | Reusable lock-data holder (`IsLocked`, `RequiredSkillId`, `LockedPrompt`) + `Unlock()`. Single source of lock truth shared by doors and containers via `GetComponent`; absence = never locked. |
| `DoorInteractable` / `DoorSystem` | `IInteractable` door that rotates its `Visual` child open/closed (re-closeable). Unlocked doors toggle locally; locked doors raise `OnDoorOpenRequested` to the player-side `DoorSystem`, which owns `PlayerSkills` and unlocks+opens on a passing skill check. |
| `PersistentID` | Marks an entity as permanently tracked by `WorldStateManager`. On `Awake` deactivates silently if its `KilledFact` is already set. Call `RegisterDeath()` before death effects. |
| `SaveableObject` | Save participant (`ISaveable`): captures / restores whichever siblings exist (`InventorySystem`, `GoldSystem`, `Lockable`, `DoorInteractable`, `EntityHealth`). Killed entity with saved loot → restored as a ragdoll corpse; empty → stays hidden. Alive entities restore inventory / gold only (they reset to spawn). |
| `TopicUnlockEvaluator` | **Static, pure.** Evaluates memory unlock/invalidation by querying `WorldStateManager` world facts. No instance state. |

---

## Rules

- **Every world enemy, NPC, and container MUST have a `PersistentID` with a `KilledFact` assigned** — otherwise death/looted state won't persist across save/scene reload. (`_guid` string is gone; use `_killedFact`.)
- Persistence facts are read/written only through `WorldStateManager` → see `Scripts/Core/CLAUDE.md`.
- New interactables implement `IInteractable` and are discovered by `InteractionSystem`'s raycast — no manual registration.
- **Outline is opt-in:** add `InteractionHighlight` to an interactable root; list only the meshes to outline (e.g. a door handle); no component = prompt-only. Every world item pickup carries one. The outline layer bit comes from `InteractionConfig.outlineRenderingLayer` and must match `InteractionOutlineFeature.outlineLayer` on `PC_Renderer` (both = `Outline`, bit 2) — see `Scripts/Rendering/CLAUDE.md`.
- **Lock pattern:** `Lockable` is the data; a locked interactable routes a `GameEventSO` to a player-side system that owns `PlayerSkills` (`DoorInteractable`→`DoorSystem`, `ContainerInteractable`→`ContainerSystem`) for the skill check. Never read `PlayerSkills` directly from an interactable — keep the cross-system touch in the player-side resolver (the `Interact()` signature stays parameterless).
- **Every container, door and scene-authored `ItemPickup` needs a `SaveableObject`** (otherwise its state isn't
  saved, and an authored pickup is captured as a runtime drop and duplicates on load). Entities get it from
  `Entity_base.prefab` and use their `PersistentID` `KilledFact` GUID as key — no `_saveId`. Everything else needs a
  **scene-instance** `_saveId` (never set it on a prefab asset — every instance would share it): run
  `Tools/Save/Validate Save IDs` after adding / duplicating objects (assigns missing IDs, reports duplicates; also runs
  report-only on scene save).
- `ItemPickup.Interact()` marks a saveable pickup consumed (`SaveSystem.MarkConsumed`) so it isn't respawned on load;
  runtime drops (no `SaveableObject`) are saved as `{itemId, position, rotation}`.
