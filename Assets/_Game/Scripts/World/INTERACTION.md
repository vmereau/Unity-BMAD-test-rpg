# Interaction — focus, theft, trespass, harvest, doors, beds

> Read when touching `InteractionSystem`, `InteractionFocus`, `InteractionHighlight`, `Ownership`,
> `StolenGoodsTracker`, `RestrictedArea(Rules)`, `Harvestable`, doors / locks (`DoorInteractable`, `DoorSystem`,
> `DoorLatch`, `Lockable`, `LockpickRules`) or `Bed` / `SleepSystem` / `SleepRules`.
> The lock pattern and lockpick gate shared by doors and containers live in `CLAUDE.md` → Locks.

---

## InteractionSystem

Player-side raycast (no focus / E while dead, incapacitated or `IsScriptedHold`) that finds the focused
`IInteractable` and dispatches `Interact()`. `_raycastMask` (Interactable + Harvestable) picks candidates only.

- **Reach:** the screen-centre ray is advanced to its point closest to `_reachOrigin` (Player `CameraTarget`,
  `InteractionFocus.ReachStartDistance`) and `InteractionConfig.interactionRange` (3) counts from there — the orbit
  camera sits metres behind the player, so a camera-origin range broke floor pickups (name tags still count from the
  camera).
- **Occlusion** is a separate per-candidate `Physics.Raycast` from the camera toward each hit point on
  `_occlusionMask` (Default; triggers ignored, stops 0.05 m short via `InteractionFocus.OcclusionProbeLength`) —
  applied to both the interaction and name-tag scans, so walls must be on Default (or a layer added to the mask) with
  a collider. A blocker belonging to the candidate itself (its Default model collider) doesn't count;
  Interactable-layer objects (closed doors, containers) never occlude others, but humanoid NPC ragdoll bones are on
  Default, so an NPC body in front does. Convex `MeshCollider` props occlude anything placed inside their hull (items
  on a lower shelf / in an open crate).
- **Focus:** on focus change **or** an in-place verb/name change (e.g. a corpse becoming lootable) it toggles the
  target's `InteractionHighlight`, sets the global outline color, tints the crosshair and raises
  `OnInteractionFocusChanged` (`InteractionFocusData { target, verb, name, illegal }`, target null = no focus) for the
  HUD prompt card. Legality is part of change detection (an owner dying flips it in place).
- **Theft hook:** on E, if the target's sibling `Ownership.IsIllegal` (re-evaluated at press time) it raises
  `OnTheftCommitted` (`TheftCommittedData { theftId, thief, position, owner, kind, objectName, item, itemCount,
  sourceInventory }` — item × count for a pickup (1) / harvest (yield), the container's inventory for a container
  theft), `theftId` from a static counter reset on play) **before** `Interact()` — a pickup destroys itself.
- After `Interact()` it forces a rescan next frame (closes the double-pickup window). `OnDisable` clears focus before
  its `_input` guard. No `OnGUI`.

## InteractionFocus

**Static, pure.** Focus-change detection, verb/name resolution, `IsAlive` (Unity fake-null for destroyed targets),
layer-bit math, outline/crosshair color selection. Tested directly by `InteractionSystemTests`.

## InteractionHighlight

**Opt-in** outline on an interactable root. `_targets` = renderers to outline (empty → all child
`MeshRenderer`/`SkinnedMeshRenderer`, collected in `Awake`); optional `_overrideColor`/`_color`. `SetHighlighted` ORs /
clears the `Outline` rendering-layer bit (materials never swapped). Static `AnyHighlighted` lets
`InteractionOutlineFeature` skip its passes; `OnDisable` clears a stale bit/count.

---

## Ownership & theft

**`Ownership`** — owner marker (`NPCEntity _owner`) on an `ItemPickup` / `Harvestable` (both `TheftKind.Item`) /
`ContainerInteractable` / `DoorInteractable` root; `Kind` (`TheftKind`) derived from the sibling in `Awake`.
`IsIllegal` = owned and owner's `NPCEntity.KilledFact` not set in `WorldStateManager` (null fact → always illegal,
warned), except an **open** door (closing is free). Pure rule `IsIllegalInteraction` (`OwnershipTests`). Illegal focus
→ red prompt + `InteractionConfig.illegalOutlineColor` outline (wins over the per-object colour); `ItemPickup` /
`Harvestable` verb becomes `Steal`, containers / doors keep theirs. Interactables never know about witnesses. Locked
owned doors / containers: the attempt itself is the theft.

**`StolenGoodsTracker`** — on `Player.prefab`. Records each theft's goods in the static `Game.Core.StolenGoodsLedger`
(theft id → item counts + gold amount, runtime only, reset on play; `StolenGoodsLedgerTests`): the pickup / harvest
item × count at the theft, and for an owned container every unit later taken from it (`OnContainerItemTaken` matched
on the remembered `sourceInventory`, `GoldItemSO` → gold amount) until any other container opens or this one reopens
without a theft (`OnContainerOpenRequested`); `OnLoadStarted` clears the whole ledger. The player's `InventorySystem` /
`GoldSystem` answer `OnStolenGoodsRequested` (`StolenGoodsRequest`: query or take, clamped to what is still carried).
Consumers: `EntityBrain` theft return (`Scripts/AI/CLAUDE.md`).

---

## Restricted areas (trespass)

**`RestrictedArea`** — a place the player may not be. Shape = every `BoxCollider` on the GO and its children (triggers
on Ignore Raycast; only center / size are read — **no trigger events**, containment is computed on demand via
`InverseTransformPoint`). Owners = `_ownerEntities` (`NPCEntity`) and / or `_ownerFactions` (`FactionSO`); optional
hours window (`_useHours`, start incl. / end excl., wraps midnight, no `GameClock` → always restricted);
`_warningsBeforeFight` (default 1; 0 = attack on sight): runtime warning memory shared by all owners —
`RecordWarning(expiryHours)` on each scold, `ActiveWarnings` (expire `StealthConfigSO.trespassWarningExpiryHours` = 24
in-game hours after the latest; no clock → never; a save from before it → expired), `ShouldFightTrespasser` (owners
brawl instead of warn / chase / scold), `ClearWarnings()` when the player is knocked out in that fight.

- Static active list + `TryFindTrespassedArea(point, entity, faction, out area)` (owned, restricted now, contains).
  `StartGrace(s)` (owners ignore it, closes the open incident), `OpenIncident()` (negative ids so they never collide
  with `InteractionSystem`'s positive theft ids). Runtime only, not saved. Wire-box gizmos (orange, red = attack on
  sight or warnings recorded in play).
- The reaction lives in `EntityPerception` / `EntityBrain` (`Scripts/AI/CLAUDE.md` → Trespass).
- Place areas under the scene root `RestrictedAreas`, never inside a tool-built root — except the worker hut shells,
  whose builder creates an inactive `RestrictedArea` child itself (`Prefabs/World/Huts/CLAUDE.md`).

**`RestrictedAreaRules`** — **static, pure.** `IsInsideBox`, `IsRestrictedHour` (uses `GameTime.IsHourInWindow`,
shared with `NpcRoutineSchedule`), `IsOwner`, `ActiveWarnings` (expiry), `ShouldFight` (threshold).
`RestrictedAreaRulesTests`.

---

## Harvestable

Fixed world resource (plant…) on layer Harvestable (9): prompt `Harvest` (`Steal` when a sibling `Ownership` is
illegal → `TheftKind.Item`), name tag = `_item.itemName`. `Interact()` adds `_yieldCount` items, `MarkConsumed`s its
`SaveableObject`, destroys itself — never regrows. Pure `ResolvePrompt` / `ClampYield` (`HarvestableTests`). Prefab
contract: `Prefabs/CLAUDE.md`.

---

## Doors & locks

**`DoorInteractable` / `DoorSystem`** — `IInteractable` door that rotates its `Visual` child open/closed
(re-closeable). Unlocked doors toggle locally; locked doors raise `OnDoorOpenRequested` to the player-side
`DoorSystem`, which owns `PlayerSkills` + the player `InventorySystem` and runs the lockpick gate (`CLAUDE.md` →
Locks) before unlock+open.

**`Lockable`** — reusable lock-data holder (`IsLocked`, `RequiredSkillId`, `LockedPrompt`, optional `KeyItem`) +
`Unlock()`. Single source of lock truth shared by doors and containers via `GetComponent`; absence = never locked.
Every locked lock must set `_requiredSkill` (`Awake` warns otherwise; resolvers then require only the lockpick).

**`LockpickRules`** — **static, pure.** `Evaluate(hasKey, hasSkill, lockpickCount)` → `Key` (checked first) /
`Success` / `MissingSkill` / `MissingLockpick` (skill checked before the lockpick, so missing both reports only the
skill; the 2-arg overload skips the key) + `FailureMessage` toast text. Tested by `LockpickRulesTests`.

**`DoorLatch`** — `IInteractable` on a locked door's inner face (own collider on Interactable): "Lift the latch"
unlocks + opens `_door` with no skill / key / pick. Interactable only while the door is locked and the player is on its
+Z side (`IsOnLatchSide`), so it can't be reached through the door. It registers with its door in `Awake`; while it
`CanInteract`, the door leaf defers to it (`DoorInteractable.DefersToLatch`: latch prompt and action, and `Ownership`
treats it as legal), so focusing the leaf from inside is never a theft and spends no pick. Optional `_unlatchedFact`
(WorldFact) is set on lift; indexed by the Quest Explorer as a setter. Used by the quarry cleft door.

---

## Beds & sleep

**`Bed`** (`Prefabs/World/Beds/` — `Bed_Base` + one variant per look, contract in `Prefabs/CLAUDE.md`; prompt
`Sleep`) raises `OnBedSleepRequested` (`GameEventSO_Void`) — no `SaveableObject` (no state). **NPC beds** (routine
Sleep, `Scripts/AI/CLAUDE.md` → NPC sleep) also set `_npcApproach` (on the NavMesh, facing the bed) and
`_npcSleepAnchor` (root pose for the sleeping clip, `IsNpcBed`); while an NPC lies in it (`Occupant`, `TryOccupy` /
`Release`) `CanInteract` is false. Owned beds (`Ownership`, `TheftKind.Other`) outline red and count as theft on use.

**`SleepSystem`** — player-side (on `Player.prefab`; `_gameClock` is a Core.unity override). Refuses while dead or
with a live hostile (`TargetRegistry.FindClosestHostile`) within `TimeConfigSO.sleepHostileRadius` → `OnSleepRefused`
toast; otherwise opens `SleepUI`, **re-gates on the click**, computes the wake time **at the click** (the clock runs
during the fade), then at the black of the fade `GameClock.SkipTo(wake)` (skipped if the clock already passed it) +
full heal (any duration).

**`SleepRules`** (pure, `SleepRulesTests`): hour picker math — `WakeTime(targetHour, now)` = next `HH:00` (current
hour → same hour tomorrow), `HoursUntil` (label, rounded up), `StepHour` (wraps 0–23).

Beds only — no wait-anywhere.
