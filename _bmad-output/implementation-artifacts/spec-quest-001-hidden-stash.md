---
title: 'The Hidden Stash'
type: 'feature'
created: '2026-11-10'
status: 'done'
baseline_commit: '09b6898'
context:
  - '{project-root}/_bmad-output/project-context.md'
  - '{project-root}/docs/World/NPCs/npc-002-beaten-worker.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The camp's stealth, ownership and routine systems lack a small authored encounter that makes them useful. The Hidden Stash (quest-001) and its owner (npc-003) remain stubs.

**Approach:** Tam casually mentions a privileged worker's stash. The player can watch the owner's routine, take some loot, and tell Tam afterward. Use existing theft reactions and persistent quest facts.

## Boundaries & Constraints

**Always:** Tam offers information, never assigns a job or requests payment. Create the owner identity before dialogue: Brann, a domineering veteran worker who hoards supplies and trades information to the Company, company-adjacent without making him a Company guard. Place an unlocked owned chest inside the gang house, using existing art and food assets. Stock five bread and two apples; no replenishment. Brann stays near the stash 06:00–18:00 and stands at the gang fire 18:00–06:00, leaving an observable opening. Validate walking paths and visibility against the actual scene. Keep additions outside builder-owned roots. Persist first successful taking, independent of the hint. Completing Tam's follow-up completes the quest; the loot itself is the reward. Preserve existing unsaved scene edits when saving StartingTown.

**Ask First:** A different NPC identity or location, terrain/build-tool changes, or a design that needs new combat consequences.

**Never:** Arrival scripting, reputation, debt, confiscation, combat escalation, new lockpicking requirements, new art generation, or repeatable XP/gold rewards. Do not change other quests to fix their existing findings.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Hint | Tam's stash topic completes | Quest starts; journal gives location and opportunity | Repeat topic grants nothing |
| Transfer | An authored loot item is successfully taken from this chest | Persist taken fact once | Failed transfer preserves contents and grants no progress |
| Open only | Chest opened without taking | No taken fact | Existing witnessed-opening theft response still applies |
| Early discovery | Loot taken before hearing the hint | Preserve taken fact; hint then permits follow-up | No required respawn or refill |
| Return | Taken fact true; Tam follow-up completes | Quest completes persistently | No extra item or currency reward |
| Load | Save after hint, taking, or completion | Restore facts and remaining contents | Restore events never count as taking |
| Caught | Existing witness sees opening | Existing pursuit/scold; quest remains available | No new permanent failure branch |
| Owner dead | Brann killed | Existing ownership rule frees chest; taking still counts | Match NPC and PersistentID killed facts |

</frozen-after-approval>

## Code Map

- `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs` — successful take path; currently removes before checking AddItem success.
- `Assets/_Game/Scripts/World/ContainerInteractable.cs`, `Ownership.cs`, `SaveableObject.cs` — interaction, legality and existing container persistence.
- `Assets/_Game/Scripts/Core/State/WorldStateManager.cs` — authoritative persistent world facts.
- `Assets/_Game/Scripts/AI/NPC/NpcRoutine.cs` — scene-authored time blocks and spots.
- `Assets/_Game/Editor/QuestExplorer/` — fact setter discovery and quest audit.
- `Assets/_Game/Data/NPCs/BeatenWorker/` — Tam identity, dialogue and memory gates.

## Tasks & Acceptance

**Execution:**
- [x] `docs/World/NPCs/npc-003-stash-owner.md`, `Assets/_Game/Data/NPCs/StashOwner/CLAUDE.md` — author Brann's identity, speech and relationship to Tam before dialogue assets.
- [x] `Assets/_Game/Scripts/Core/ContainerItemTakenData.cs`, `Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerItemTaken.cs`, `Assets/_Game/Data/Events/OnContainerItemTaken.asset` — add typed source-inventory/item transfer notification, following existing event conventions.
- [x] `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs` — validate transfer and emit only after successful player addition and source removal; retain contents on rejection.
- [x] `Assets/_Game/Scripts/World/ContainerTakeFact.cs` — match source inventory and an authored item allowlist; persist configured WorldFact once. Player-deposited unrelated items must not count.
- [x] `Assets/_Game/Editor/QuestExplorer/` — recognize this authored setter where needed for accurate audit results.
- [x] `Assets/_Game/Data/NPCs/StashOwner/`, `Assets/_Game/Data/Enemies/StartingTown/Generic/` — create NPC data, greeting, witness profile and matching killed fact.
- [x] `Assets/_Game/Data/Quests/HiddenStash/`, `Assets/_Game/Data/NPCs/BeatenWorker/` — create quest, hint/follow-up dialogue facts, taken WorldFact and gated memories. Replaying topics never adds rewards.
- [x] `Assets/_Game/Scenes/StartingTown.unity` — add Brann, routine spots, owned stocked chest, highlight, unique save ID and fact listener under a hand-owned quest root; wire transfer channel on the actual ContainerUI owner.
- [x] `Assets/_Game/Prefabs/UI/QuestLog/QuestLogUI.prefab` — register quest; run NPC-memory and quest-event sync menus and verify registrations.
- [x] `Assets/Tests/EditMode/ContainerTakeFactTests.cs` — cover source/item filtering, failed transfer behavior and repeated notification; use existing test conventions.
- [x] `docs/World/Quests/HiddenStash.md`, `docs/World/world-index.md`, relevant folder `CLAUDE.md` files — record encounter and authored fact setter.

**Acceptance Criteria:**
- Given a new game, when the player follows Tam's clue, then the stash and Brann are reachable and Brann's dusk departure creates an opportunity.
- Given the finished encounter, when quest audit runs, then HiddenStash has no errors and both existing quests retain their prior behavior.
- Given normal chests or corpse loot, when taking items, then only the configured stash progresses HiddenStash.

## Spec Change Log

## Verification

- Unity compilation and console: no new errors.
- Targeted EditMode tests: transfer/fact rules pass; run relevant existing inventory and quest tests.
- Quest report: baseline FindHerbalist has one error; HiddenStash introduces none.
- Play checks: hint → watch routine → take → Tam; open without taking; take before hint; witnessed opening; rejected transfer; save/load at each stage; ordinary chest and corpse-loot regression.

## Implementation evidence

- 129 targeted EditMode tests passed (ContainerTakeFact, InventorySystem, WorldStateManagerFacts, QuestReferenceIndex, QuestValidator); no failures or skips.
- Runtime checks through real ContainerUI passed: initial stock seven, open-only does not progress, early take before hint, retrospective memory gating, follow-up completion, fact/stack roundtrip, actual SaveableObject capture/restore with the real catalog, restore without false progress, owner-death legality, dusk/morning snaps. These are scripted integration checks, not a manual full UI playthrough or a disk save/reload test.
- Brann day/fire NavMesh path complete (five corners); interior screenshot inspected at Temp/HiddenStashVerification/stash-interior.png.
- Quest report after scene authoring: HiddenStash zero errors/warnings, FindHerbalist retains baseline one error, SpiderClear zero errors/warnings.
- Final extension indexes ContainerTakeFact setters in closed-scene YAML. A rerun/closed-scene audit was requested afterward but MCP disconnected, so this last collector extension has not received the final Unity rerun.
- Core scene incidental UI serialization was removed by restoring its initially clean baseline and reloading Core. Existing unsaved StartingTown edits were preserved by saving the live scene.
- Observed player startup exception: PlayerAnimationDriver.SetInCombat:85 from EquipmentVisuals.OnEnable. Those files are unchanged by this work; baseline reproduction was not separately performed.
- git diff --check reports Unity-generated blank-value trailing spaces in StartingTown YAML; source changes have no whitespace errors.

## Review and final verification

- Three independent reviewers completed blind, edge-case and acceptance reviews. Their shared actionable finding was Brann's death-disable references pointing at prefab components. Patched those three entries to his local EntityHealth, EntityBrain and NavMeshAgent. Reloading StartingTown verified all three references resolve locally. Full post-fix death/ragdoll playback remains unverified.
- Successful ContainerUI transfer integration was exercised during runtime verification; automated tests separately cover listener rules and rejection. A fully automated successful UI transfer regression remains a coverage opportunity.
- Unity MCP recovered. Final rerun after all source changes: 129 passed, zero failed/skipped (job bb0334d64bdd4aaf82c3da7203248e76).
- Closed StartingTown, collected the index, and reopened it: one ContainerTakeFact setter discovered from the closed scene. Both scenes were clean before this check.
- Full manual UI playthrough and disk save/reload remain unverified. Existing Burst pipe exception and observed player-startup exception are outside this change.

## Suggested Review Order

**Quest experience**

- Read the encounter flow, journal states and intended opportunity.
  [HiddenStash.md:1](../../docs/World/Quests/HiddenStash.md#L1)

- Review Brann's identity before his dialogue and behavior.
  [npc-003-stash-owner.md:1](../../docs/World/NPCs/npc-003-stash-owner.md#L1)

**Transfer and persistence**

- Successful takes notify the world only after adding and removing the item.
  [ContainerUI.cs:135](../../Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs#L135)

- Source and item filters protect the persistent one-time world fact.
  [ContainerTakeFact.cs:1](../../Assets/_Game/Scripts/World/ContainerTakeFact.cs#L1)

- Discover authored setters in loaded and closed scenes.
  [QuestIndexCollector.cs:50](../../Assets/_Game/Editor/QuestExplorer/QuestIndexCollector.cs#L50)

**Supporting checks**

- Review filtering, rejection and repeated-notification coverage.
  [ContainerTakeFactTests.cs:1](../../Assets/Tests/EditMode/ContainerTakeFactTests.cs#L1)
