# CLAUDE.md — Assets/_Game/Scripts/World

> Loaded when Claude accesses files in this folder. Interaction, dialogue, containers,
> and world-entity persistence. Namespace: `Game.World`.

---

## What's here

| File | Role |
|------|------|
| `IInteractable` | Contract for anything the player can interact with (`CanInteract`, `Interact()`); implementers under Rules |
| `EntityPresence` | Base `IInteractable` for every entity (namespace `Game.World`, file `Scripts/AI/EntityPresence.cs`) → `Scripts/AI/CLAUDE.md` |
| `InteractionSystem` | Player-side raycast: reach, occlusion, focus / highlight, theft hook, `Interact()` dispatch → `INTERACTION.md` |
| `InteractionFocus` | **Static, pure.** Focus-change detection, verb/name, `IsAlive`, layer bits, outline/crosshair colour → `INTERACTION.md` |
| `InteractionHighlight` | **Opt-in** outline on an interactable root (`Outline` rendering-layer bit) → `INTERACTION.md` |
| `InteractableObject` | Generic scene interactable. |
| `Ownership` | Owner marker on pickups / harvestables / containers / doors; `IsIllegal` drives theft → `INTERACTION.md` |
| `StolenGoodsTracker` | On `Player.prefab`; records each theft's goods in `StolenGoodsLedger` → `INTERACTION.md` |
| `RestrictedArea` / `RestrictedAreaRules` | Trespass zones (box shapes, owners, hours, warnings) + pure rules → `INTERACTION.md` |
| `Harvestable` | Fixed world resource on layer Harvestable (9), never regrows → `INTERACTION.md` |
| `ContainerSystem` / `ContainerInteractable` | Lootable container state + its interactable surface. Reads lock data from a sibling `Lockable` (optional). |
| `DoorInteractable` / `DoorSystem` | Re-closeable door + player-side locked-door resolver → `INTERACTION.md`, Locks below |
| `DoorLatch` | Inner-face latch that unlocks + opens a locked door with no skill / key / pick → `INTERACTION.md` |
| `Lockable` | Lock-data holder shared by doors and containers; absence = never locked → `INTERACTION.md`, Locks below |
| `LockpickRules` | **Static, pure.** Key / skill / lockpick evaluation + failure toast text → `INTERACTION.md` |
| `Bed` / `SleepSystem` / `SleepRules` | Bed prompt (player + NPC beds), player sleep flow, pure hour-picker math → `INTERACTION.md` |
| `DialogueSystem` | Drives NPC dialogue flow: forced lines / topics, choices, give-item, close requests → `DIALOGUE_SYSTEM.md` |
| `ChoiceItemRules` | **Static, pure.** `Locate` a choice's required item in bag or equipment → `DIALOGUE_SYSTEM.md` |
| `TopicUnlockEvaluator` | **Static, pure.** Evaluates memory unlock/invalidation by querying `WorldStateManager` world facts. No instance state. |
| `WorldFactTrigger` / `WorldFactTriggerRules` | Sets a WorldFact once when the player stands in a box (optional fact / hour / NPC gates) → `FACT_OBJECTS.md` |
| `FactRemovedObject` / `FactRemovedObjectRules` | Hides a target (e.g. an NPC leaving) while a fact is true → `FACT_OBJECTS.md` |
| `ContainerTakeFact` | Sets a WorldFact on the first take of an allowlisted item from its container → `FACT_OBJECTS.md` |
| `ClockNightLight` | Fades a light + glow quads by night level from `GameClock.Active` → `FACT_OBJECTS.md` |
| `PersistentID` | Marks an entity as permanently tracked by `WorldStateManager` → Persistence below |
| `SaveableObject` | Save participant (`ISaveable`) for world objects → Persistence below |
| `Story/ArrivalSequence` / `ArrivalSequenceRules` | event-001 arrival escort + hut briefing → `Story/CLAUDE.md` |

---

## Topic files

| File | Read when |
|------|-----------|
| `INTERACTION.md` | Touching interaction focus / reach / occlusion / highlight, ownership & theft, stolen goods, restricted areas, harvestables, doors / latches / locks, beds & sleep |
| `DIALOGUE_SYSTEM.md` | Touching `DialogueSystem` / `ChoiceItemRules`, or anything that opens or closes NPC dialogue |
| `FACT_OBJECTS.md` | Touching `WorldFactTrigger`, `FactRemovedObject`, `ContainerTakeFact` or `ClockNightLight` |

---

## Rules

- New interactables implement `IInteractable` and are discovered by `InteractionSystem`'s raycast — no manual
  registration. Implemented by `InteractableObject`, `ContainerInteractable`, `ItemPickup`, `Harvestable`,
  `CookingStation` (`Scripts/Crafting/`), `EntityPresence` (and its subclass `NPCPresence`; corpse loot: `Scripts/AI/CLAUDE.md` →
  EntityPresence loot; dialogue subclass: `Scripts/AI/NPC/CLAUDE.md` → NPCPresence).
- **Outline is opt-in:** add `InteractionHighlight` to an interactable root; list only the meshes to outline (e.g. a
  door handle); no component = prompt-only. Every world item pickup carries one. The outline layer bit comes from
  `InteractionConfig.outlineRenderingLayer` and must match `InteractionOutlineFeature.outlineLayer` on `PC_Renderer`
  (both = `Outline`, bit 2) — see `Scripts/Rendering/CLAUDE.md`.
- Persistence facts are read/written only through `WorldStateManager` → see `Scripts/Core/CLAUDE.md`.

---

## Locks

- **Lock pattern:** `Lockable` is the data; a locked interactable routes a `GameEventSO` to a player-side system that
  owns `PlayerSkills` (`DoorInteractable`→`DoorSystem`, `ContainerInteractable`→`ContainerSystem`) for the skill
  check. Never read `PlayerSkills` directly from an interactable — keep the cross-system touch in the player-side
  resolver (the `Interact()` signature stays parameterless).
- **Lockpick gate (both resolvers):** any locked request needs the skill **and** ≥1 `_lockpickItem`
  (`Item_Lockpick`) in `_playerInventory` (`LockpickRules`). Failure → `OnLockpickFailed` (`GameEventSO_String`,
  message shown verbatim by `NotificationToastUI`), nothing consumed. Success → `InventorySystem.TryConsume` one
  lockpick **before** `Unlock()`, then `OnLockUnlocked`. A picked lock stays unlocked (no further cost). **Key:** a
  `Lockable._keyItem` travels in the open request (`keyItem`); if the player holds it, `LockpickResult.Key` wins over
  skill + pick: unlock with nothing consumed and no skill needed.

---

## Persistence and save ids

- **Every world enemy, NPC, and container MUST have a `PersistentID` with a `KilledFact` assigned** — otherwise
  death/looted state won't persist across save/scene reload.
- `PersistentID`: on `Awake` deactivates silently if its `KilledFact` is already set. Call
  `RegisterDeath(playerCredited)` before death effects (credit = `Game.AI.KillCredit`, resolved by `EntityHealth`).
- `SaveableObject` captures / restores whichever siblings exist (`InventorySystem`, `GoldSystem`, `Lockable`,
  `DoorInteractable`, `EntityHealth`). Killed entity with saved loot → restored as a ragdoll corpse; empty → stays
  hidden. Alive entities restore inventory / gold only (they reset to spawn).
- **Every container, door, harvestable and scene-authored `ItemPickup` needs a `SaveableObject`** (otherwise its
  state isn't saved, and an authored pickup is captured as a runtime drop and duplicates on load). Entities get it
  from `Entity_base.prefab` and use their `PersistentID` `KilledFact` GUID as key — no `_saveId`; so does any object
  with a `PersistentID` (owned containers such as `BrannsStash` / `HobbsBox`: `SaveableObject.SaveKey` prefers the
  fact's GUID, and the validator skips `_saveId` for them). Everything else needs a **scene-instance** `_saveId`
  (never set it on a prefab asset — every instance would share it): run `Tools/Save/Validate Save IDs` after adding /
  duplicating objects (assigns missing IDs, reports duplicates; also runs report-only on scene save).
- `ItemPickup.Interact()` marks a saveable pickup consumed (`SaveSystem.MarkConsumed`) so it isn't respawned on load;
  runtime drops (no `SaveableObject`) are saved as `{itemId, position, rotation}`.
