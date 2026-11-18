---
title: 'Sealed Passage — door route (key support, ring trigger, Bren, Odo)'
type: 'feature'
created: '2026-11-18'
status: 'done'
baseline_commit: 'aa3ff73551ab376eb649af4018ee91a8ebe0424a'
context:
  - '{project-root}/docs/World/Quests/SealedPassage.md'
  - '{project-root}/Assets/_Game/Scripts/World/CLAUDE.md'
  - '{project-root}/Assets/_Game/Scenes/CLAUDE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Quest-002 `SealedPassage` exists as data only (b3e4d18c): Bren isn't in the world, the cleft is a solid
seal, nothing sets `WorldFact_SealedPassage_RingEntered` / `_TokenTaken`, locks can't take a key, and a completed
quest would also read as failed if Bren dies afterwards.

**Approach:** Make the quest playable end to end through the door: a locked Company door in the cleft (beginner
lockpick, or Dace's key from his hut), an inside latch, a ring-entry fact trigger, Odo's remains with the token, and
Bren placed with a routine and his KilledFact as the failed part. The goat track is a separate spec (deferred-work).

## Boundaries & Constraints

**Always:** Cross-system rules of `project-context.md`; pure rules in static helpers with EditMode tests; every door /
container / authored pickup gets a `SaveableObject` with a scene `_saveId` (`Tools/Save/Validate Save IDs`); no
em-dashes in player-facing text. The cleft change lives in `Area1SitesBuilder` so a Sites rebuild keeps it.
`FactRemovedObject` on `WorldFact_QuarryPassageOpened` stays (a later Company event can still remove the whole seal).

**Ask First:** Any terrain sculpt; changing an existing door / container prefab asset; any NavMesh rebake.

**Never:** The goat track, `RuinsRing` gaps, creatures (B2). Opening the Ruins 2 ring by any other route. Final art.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Key | Locked lock with `_keyItem`, player holds key | Unlocks + opens; nothing consumed; no skill needed | N/A |
| Key + pick | Holds key and skill + lockpick | Key wins; lockpick kept | N/A |
| No key | Lock has a key, player lacks it | Normal lockpick rules / toasts | N/A |
| Inside latch | Player at the latch, door locked | "Lift the latch" unlocks + opens | Unlocked door → latch not interactable |
| Ring entry | Player inside a trigger box, fact unset | Fact set once, component idles | No player / no WSM → no-op |
| Completed then failed | Completed fact true, Bren killed | `IsFailed` false; quest stays in Completed only | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/Lockable.cs` -- lock data; gains optional `_keyItem`
- `Assets/_Game/Scripts/World/LockpickRules.cs` -- pure gate; gains the key case
- `Assets/_Game/Scripts/World/DoorSystem.cs`, `ContainerSystem.cs` -- player-side resolvers (skill, lockpick, consume)
- `Assets/_Game/Scripts/World/DoorInteractable.cs`, `ContainerInteractable.cs` -- raise the open requests
- `Assets/_Game/ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs`, `GameEventSO_ContainerOpenRequest.cs` -- request structs
- `Assets/_Game/ScriptableObjects/Quest/QuestSO.cs` -- `IsCompleted` / `IsFailed`
- `Assets/_Game/Scripts/World/RestrictedArea.cs` + `RestrictedAreaRules.IsInsideBox` -- polled box containment pattern (no trigger events in this project)
- `Assets/_Game/Scripts/World/ContainerTakeFact.cs` -- token → `TokenTaken` setter
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` (`BuildCleftBlocker`) -- builds `Quarry/CleftBlocker/Seal` (+ `Cap`)
- StartingTown `WorkSiteManagers/QuarryOverseer` -- Dace instance + spot, template for Bren

## Tasks & Acceptance

**Execution:**
- [x] `ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs`, `GameEventSO_ContainerOpenRequest.cs` -- add `ItemSO keyItem` -- resolvers need it without reading the interactable
- [x] `Scripts/World/Lockable.cs` -- optional `[SerializeField] ItemSO _keyItem` + `KeyItem` getter -- one lock-data source
- [x] `Scripts/World/LockpickRules.cs` -- `Evaluate(bool hasKey, bool hasSkill, int lockpicks)` → new `LockpickResult.Key` checked first; keep the 2-arg overload -- key beats skill
- [x] `Scripts/World/DoorInteractable.cs`, `ContainerInteractable.cs` -- pass `keyItem` in the request -- data flow
- [x] `Scripts/World/DoorSystem.cs`, `ContainerSystem.cs` -- count the key; on `Key` unlock without consuming -- key route
- [x] `Scripts/World/DoorLatch.cs` -- new `IInteractable` on the door's inner face: prompt "Lift the latch" while the target door is locked, then `Unlock()` + `ToggleOpen()`; `CanInteract` false when unlocked -- no trap inside
- [x] `Scripts/World/WorldFactTrigger.cs` -- new: child `BoxCollider`s (Ignore Raycast, trigger) as shape, polls `TargetRegistry.Player` every `_pollInterval` with `RestrictedAreaRules.IsInsideBox`, `SetWorldEvent(_fact, true)` once; skips while the fact is true -- ring-entry setter
- [x] `ScriptableObjects/Quest/QuestSO.cs` -- `IsFailed` returns false when `IsCompleted` -- completion wins
- [x] `Tests/EditMode/LockpickRulesTests.cs`, `WorldStateManagerFactsTests.cs` (completed-then-failed), new `DoorLatchTests.cs` -- matrix rows Key / Key + pick / No key / Completed then failed
- [x] `Editor/World/Area1SitesBuilder.cs` -- Seal = planks-and-rubble wall with a door opening holding a locked Company door (`Simple door` prefab or graybox door: `DoorInteractable`, `Lockable` beginner lockpicking + `Item_Key_QuarrySouthDoor`, `Ownership` Dace, `SaveableObject`) + `DoorLatch` behind it; `Cap` starts above the door frame; NavMesh obstacle unchanged -- door survives rebuilds
- [x] StartingTown, new hand-owned root `SealedPassage` -- Dace's hut (graybox shed near his spot) with an owned key pickup (`Ownership` Dace, `SaveableObject`; key world prefab copied from `Item_Lockpick.prefab`), new-cut dressing past the door (timber scaffold, survey marks, left tools), Odo's remains (graybox seated body + bundle container from `Base_Container`, `ContainerTakeFact` → `TokenTaken`, contains `Item_Odo_TallyToken` + 2 worthless items) facing the ancient stone, `WorldFactTrigger` → `RingEntered` just past the cut -- quest objects
- [x] StartingTown `WorkSiteManagers` -- Bren (`NPC_base Variant`, `NPC_QuarryWorker`) with an `NpcRoutine`: quarry floor by day, a tent-camp spot by night; generate `KilledFact_QuarryWorker`, set on `NPC_QuarryWorker._killedFact`; add it as the quest failed part ("Bren is dead. There is no one left to tell.") -- quest giver + failure
- [x] Docs -- `Scripts/World/CLAUDE.md` (key, latch, `WorldFactTrigger`), `Data/Facts/CLAUDE.md` setters, `Scenes/CLAUDE.md` (cleft door, `SealedPassage` root), quest spec checklist + status `implemented`

**Acceptance Criteria:**
- Given Bren friendly and the quest accepted, when the player opens the door by lockpick or key and walks into the ring, then step 0 completes (+50 XP).
- Given the player takes the token from Odo's remains, when they report to Bren with or without it, then the quest completes and the rewards match the quest spec.
- Given the door is locked and the player is inside, when they use the latch, then the door opens.
- Given a Sites rebuild, when it finishes, then the door, latch and cap are rebuilt and the NavMesh stays closed at the cleft.
- `quest_report SealedPassage` reports no Error / Warning.

## Design Notes

The latch makes "he never tried the door" literal: Odo could have left. Polling replaces trigger events (none exist in
the codebase; `RestrictedArea` polls the same way). Key wins over lockpick so a key holder never spends a pick.

## Verification

**Commands:**
- EditMode tests via MCP `run_tests` -- all pass
- `quest_report SealedPassage` and `quest_report audit` -- no new issues

**Manual checks:**
- Play mode (After Arrival scenario): talk to Bren, pick the door, enter the ring, take the token, report; reload a save mid-way and check the door / container / pickup states.

## Suggested Review Order

**Key route**

- Entry point: the key is checked before skill and lockpick; the old overload stays.
  [`LockpickRules.cs:22`](../../Assets/_Game/Scripts/World/LockpickRules.cs#L22)

- Resolver checks the key before the lockpick wiring; nothing consumed on Key.
  [`DoorSystem.cs:57`](../../Assets/_Game/Scripts/World/DoorSystem.cs#L57)

- Same flow mirrored for locked containers.
  [`ContainerSystem.cs:61`](../../Assets/_Game/Scripts/World/ContainerSystem.cs#L61)

- Lock data gains the optional key item.
  [`Lockable.cs:23`](../../Assets/_Game/Scripts/World/Lockable.cs#L23)

- The request carries the key, so resolvers never read the interactable.
  [`GameEventSO_DoorOpenRequest.cs:13`](../../Assets/_Game/ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs#L13)
  [`DoorInteractable.cs:73`](../../Assets/_Game/Scripts/World/DoorInteractable.cs#L73)

**New world components**

- Latch works only while locked and from the inside face.
  [`DoorLatch.cs:25`](../../Assets/_Game/Scripts/World/DoorLatch.cs#L25)

- Polled containment sets the ring fact once (no trigger events in the project).
  [`WorldFactTrigger.cs:32`](../../Assets/_Game/Scripts/World/WorldFactTrigger.cs#L32)

**Quest state**

- Completion wins over failure, so a late death never relabels a done quest.
  [`QuestSO.cs:101`](../../Assets/_Game/ScriptableObjects/Quest/QuestSO.cs#L101)

- Quest Explorer indexes WorldFactTrigger as a setter, loaded and closed scenes.
  [`QuestIndexCollector.cs:59`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L59)
  [`QuestIndexCollector.cs:162`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L162)

**Cleft door (builder)**

- Wall round a doorway; the doorway is walled up if the door can't be built.
  [`Area1SitesBuilder.cs:676`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L676)

- Cap now starts above the door frame.
  [`Area1SitesBuilder.cs:689`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L689)

- Door instance: locked, keyed, owned by Dace, fixed save id, plus the latch.
  [`Area1SitesBuilder.cs:1141`](../../Assets/_Game/Editor/World/Area1SitesBuilder.cs#L1141)

**Tests**

- Key / key + pick / no key rows of the I/O matrix.
  [`LockpickRulesTests.cs:36`](../../Assets/Tests/EditMode/LockpickRulesTests.cs#L36)

- Completed then failed stays completed.
  [`WorldStateManagerFactsTests.cs:196`](../../Assets/Tests/EditMode/WorldStateManagerFactsTests.cs#L196)

- WorldFactTrigger shows up as a fact setter.
  [`QuestReferenceIndexTests.cs:77`](../../Assets/Tests/EditMode/QuestReferenceIndexTests.cs#L77)
