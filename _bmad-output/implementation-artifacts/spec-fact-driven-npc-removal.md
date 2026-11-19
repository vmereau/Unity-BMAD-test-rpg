---
title: 'Fact-driven NPC removal (FactRemovedObject on any Fact)'
type: 'feature'
created: '2026-11-19'
status: 'done'
baseline_commit: '13e4bd9d425700859905b94447372064d7b96a42'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/World/Quests/TitheBarnShortfall.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `FactRemovedObject` only takes a `WorldFact` and blindly hides its target. TitheBarnShortfall (P4) needs an
`ElapsedFact` (e.g. `ElapsedFact_FarmReeve_Shortfall_ReportedGuard_12h`) to remove a live NPC (Venn, Pim) on time,
safely and permanently.

**Approach:** Widen `_fact` to any `Fact`. It is read through `WorldStateManager.GetFact`, re-checked on
`OnFactChanged` and by a slow poll for computed facts. When the target is an NPC (it has an `EntityHealth`), the
component removes it safely: it never touches a dead NPC, waits while the NPC fights or is knocked out, and closes that
NPC's dialogue and trade before hiding it. Still one component, decided by a pure rules class.

## Boundaries & Constraints

**Always:** Hide with `SetActive(false)` on the target (the existing `EntityBrain.OnDisable` frees the bed and drops
the sleep pose). Removal never sets a `KilledFact`, never calls `RegisterDeath`, and grants no XP. The component lives on
a parent of the target (the existing contract). The serialized field name stays `_fact`, so existing scene references
survive. Logging goes through `GameLog`.

**Ask First:** Any change to the save order (`SaveSystem`, `SaveableObject`, `PersistentID`), or a new save field.

**Never:** Create Venn, Pim or the quest assets. Add a new "NPC leaves" component. Hide a dead NPC or its corpse. Track
which NPC owns the dialogue in order to wait for it (the user chose: close it, don't wait).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Blocker | non-entity target, fact true / false | hidden / shown (unchanged behavior) | N/A |
| ElapsedFact due in region | delay ends while the player is nearby | `OnFactChanged` (or the poll) → NPC hidden | N/A |
| Sleep skip past deadline | `SkipTo` beyond the deadline | hidden on the next `WorldStateManager.Update` or poll | N/A |
| Load after deadline | fact true at `Start` | NPC hidden on `Start`; `SaveableObject.Restore` leaves an alive hidden NPC off | N/A |
| QuestFact / SkillFact | no fact event is raised | the poll (≤ `_pollInterval`) applies it | N/A |
| NPC in combat / unconscious | fact true | wait; re-checked on each poll; hidden once calm | N/A |
| NPC dead / KilledFact set | fact true or false | target never toggled (corpse and loot stay, PersistentID owns it) | N/A |
| Dialogue / trade open with it | fact true, NPC calm | `NPCPresence.RequestDialogueClose()` closes the dialogue, and `NPCTradeUI` closes if it trades with that NPC's inventory; then hidden | no presence → just hide |
| Fact turns false later | NPC hidden, alive | shown again (symmetric, like blockers) | N/A |
| No Core | no `WorldStateManager` | target stays, one warning | N/A |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/World/FactRemovedObject.cs` -- the component to widen
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` -- `GetFact`, `IsElapsed` deadline registration, `RaiseDueElapsedFacts`
- `Assets/_Game/Scripts/AI/EntityBrain.cs` (`IsInCombat`, `OnDisable` releases the bed), `EntityHealth.cs` (`IsDead`, `IsUnconscious`)
- `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` -- `RequestDialogueClose()` → `GameEventSO_DialogueCloseRequest`
- `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` -- opened by `DialogueSystem` after the dialogue closes; knows only the NPC's `InventorySystem`
- `Assets/_Game/Scripts/World/SaveableObject.cs`, `PersistentID.cs` -- reactivate only killed corpses; unchanged
- `Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs`, `QuestIndexTypes.cs`, `QuestReferenceIndex.cs` -- scene setter scan; `FactRemovedObject` is not indexed today
- `Assets/_Game/Editor/World/Area1SitesBuilder.cs` -- existing blocker user (assigns `_fact` via SerializedObject)

## Tasks & Acceptance

**Execution:**
- [x] `Assets/_Game/Scripts/World/FactRemovedObjectRules.cs` -- pure `static Action Decide(bool factTrue, bool isEntity, bool isDead, bool isBusy, bool targetActive)` → `None | Hide | Show | Wait` -- keeps the decision testable
- [x] `Assets/_Game/Scripts/World/FactRemovedObject.cs` -- `Fact _fact`; cache `EntityHealth`, `EntityBrain`, `NPCPresence`, `PersistentID` on the target in `Awake`; dead = `IsDead` or the KilledFact is set; busy = `IsInCombat || IsUnconscious`; apply on `Start`, on any `OnFactChanged` whose key matches, and in an `Update` poll (`_pollInterval` 1 s) for computed facts or while waiting; before hiding an NPC call `RequestDialogueClose()`; info-log removal; update the XML doc -- the core feature
- [x] `Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs` -- new `[SerializeField] GameEventSO_DialogueCloseRequest _onDialogueCloseRequested`, subscribed in `OnEnable` / unsubscribed in `OnDisable` (project event rule; only needed while the panel is open); close when open and `requester.GetComponent<InventorySystem>() == _npcInventory` -- a leaving NPC must not keep a trade open
- [x] `Assets/_Game/Prefabs/UI/UICanvas.prefab` -- wire the `OnDialogueCloseRequested` asset (the one `DialogueSystem` uses) on `NPCTradeUI` through MCP -- runtime wiring
- [x] `Assets/_Game/Editor/QuestExplorer/*` -- add `FactLinkSource.FactRemovedObject` and index it as a **reader** of `_fact` (any `Fact`) in loaded scenes and the closed-scene YAML scan (`FactGuidInBlock`) -- keeps the fact's reader list complete
- [x] `Assets/Tests/EditMode/FactRemovedObjectRulesTests.cs` -- every matrix row's decision -- pure-logic coverage
- [x] `Assets/_Game/Scripts/World/CLAUDE.md`, `Assets/_Game/Editor/QuestExplorer/CLAUDE.md`, `Assets/_Game/Scripts/UI/Inventory` rule (where `NPCTradeUI` is documented) -- document the new behavior; mark P4 done in `docs/World/Quests/TitheBarnShortfall.md` and `deferred-work.md`

**Acceptance Criteria:**
- Given the existing StartingTown blocker, when the scene loads, then its `_fact` reference is intact and it still hides or shows as before.
- Given an NPC removed by the component, when the game is saved and reloaded, then the NPC stays hidden, its bed is free, and no `KilledFact` is set.
- Given a `FactRemovedObject` in a closed region scene, when the Quest Explorer rebuilds, then its fact lists it as a reader.
- Given the EditMode suite, when it runs, then `FactRemovedObjectRulesTests` and all existing tests pass.

## Design Notes

One component instead of a dedicated "NPC leaves" one: the only NPC-specific parts are three guards and a close
request, and they are picked from the target's components. `SetActive(false)` is enough. `EntityBrain.OnDisable`
already exits the sleep pose and releases the `Bed`, and `SaveableObject.Restore` only reactivates *killed* entities.
Nothing new is saved, because the fact (with its saved set time) is the state. Polling covers the computed facts that
never raise `OnFactChanged` (Quest / Skill / Stat). For `ElapsedFact`, each `GetFact` also re-registers the deadline
after a load.

## Verification

**Commands:**
- MCP `run_tests` (EditMode) -- expected: all pass, including `FactRemovedObjectRulesTests`
- MCP `read_console` after refresh -- expected: no compile errors

**Manual checks (if no CLI):**
- `UICanvas.prefab` → `NPCTradeUI` has `_onDialogueCloseRequested` assigned; StartingTown blocker `_fact` is still set.

## Spec Change Log

- Review patches (no loopback): Show only a target this component hid (`hiddenByMe` in `Decide`), so the poll never
  undoes another system's hiding. Thief chase / confront counts as busy. The hide runs on the next `Update`, not inside
  the fact event. A wrapper target logs an error. A KilledFact used as a removal fact is no longer counted as a
  closed-scene setter. NPCTradeUI subscribes in `OnEnable` / `OnDisable` (project event rule). KEEP: the single
  component, close-don't-wait for dialogue / trade, dead NPCs never touched.

## Suggested Review Order

**Removal decision**

- Entry point: pure table — dead never touched, busy waits, only self-hidden targets come back.
  [`FactRemovedObjectRules.cs:29`](../../Assets/_Game/Scripts/World/FactRemovedObjectRules.cs#L29)

- Where the NPC state feeds the table (combat, thief chase, knockout, KilledFact).
  [`FactRemovedObject.cs:112`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L112)

- Fact event only zeroes the poll: hide happens outside the dispatch.
  [`FactRemovedObject.cs:99`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L99)

**Leaving the world safely**

- Closes the NPC's dialogue / trade, then `SetActive(false)`; bed freed by `EntityBrain.OnDisable`.
  [`FactRemovedObject.cs:131`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L131)

- Entity detection on the target root; a wrapper is reported.
  [`FactRemovedObject.cs:46`](../../Assets/_Game/Scripts/World/FactRemovedObject.cs#L46)

- Trade window closes when its NPC asks to close its dialogue.
  [`NPCTradeUI.cs:113`](../../Assets/_Game/Scripts/UI/Inventory/NPCTradeUI.cs#L113)

**Quest Explorer reader indexing**

- Loaded scenes: `FactRemovedObject._fact` added as a reader.
  [`QuestIndexCollector.cs:63`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L63)

- Closed scenes: reader from the YAML block, excluded from the KilledFact setter scan.
  [`QuestIndexCollector.cs:213`](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L213)

- Readers merged into the index.
  [`QuestReferenceIndex.cs:71`](../../Assets/_Game/Editor/QuestExplorer/QuestReferenceIndex.cs#L71)

**Peripherals**

- Event wired on the nested `NPCTradeUI` instance.
  [`UICanvas.prefab:7904`](../../Assets/_Game/Prefabs/UI/UICanvas.prefab#L7904)

- Decision table tests.
  [`FactRemovedObjectRulesTests.cs:50`](../../Assets/Tests/EditMode/FactRemovedObjectRulesTests.cs#L50)
