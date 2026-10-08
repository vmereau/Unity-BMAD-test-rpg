---
title: 'Stealing: Ownership, Theft Witnesses, Chase & Scold Dialogue'
slug: 'stealing-ownership-chase-scold'
created: '2026-10-08'
status: 'completed'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.6.2f1', 'C# (.NET Standard 2.1)', 'uGUI + TextMeshPro', 'NavMeshAgent', 'PhysX raycasts', 'URP RenderGraph outline (global colour)', 'NUnit EditMode tests']
files_to_modify: ['NEW Assets/_Game/Scripts/World/Ownership.cs', 'NEW Assets/_Game/ScriptableObjects/Events/GameEventSO_TheftCommitted.cs', 'NEW Assets/_Game/Data/Events/OnTheftCommitted.asset', 'NEW Assets/_Game/Scripts/Stealth/TheftDetection.cs', 'NEW Assets/_Game/Data/NPCs/Barks/Barks_TheftScold.asset', 'NEW Assets/_Game/Data/NPCs/Barks/Barks_TheftScold_Item.asset', 'NEW Assets/_Game/Data/NPCs/Barks/Barks_TheftAlert.asset', 'NEW Assets/Tests/EditMode/TheftDetectionTests.cs', 'NEW Assets/Tests/EditMode/OwnershipTests.cs', 'NEW Assets/Tests/EditMode/TheftPursuitRegistryTests.cs', 'NEW Assets/_Game/Scripts/AI/TheftPursuitRegistry.cs', 'Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs', 'Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs', 'Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs', 'Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs', 'Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs', 'Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs', 'Assets/_Game/Scripts/World/InteractionSystem.cs', 'Assets/_Game/Scripts/World/InteractionFocus.cs', 'Assets/_Game/Scripts/World/PersistentID.cs', 'Assets/_Game/Scripts/World/DialogueSystem.cs', 'Assets/_Game/Scripts/Inventory/ItemPickup.cs', 'Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs', 'Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs', 'Assets/_Game/Scripts/AI/EntityBrain.cs', 'Assets/_Game/Scripts/AI/EntityPerception.cs', 'Assets/_Game/Prefabs/Entities/Entity_base.prefab', 'Player.prefab (InteractionSystem wiring)', 'Data/Config/InteractionConfig.asset', 'Data/Config/StealthConfig.asset', 'Data/Entities/WitnessProfile_Humanoid.asset', 'Data/NPCs/*/NPC_*.asset (KilledFact)', 'StartingTown.unity (test ownership on Chest, potion, door)', 'Assets/Tests/EditMode/InteractionSystemTests.cs', 'Folder CLAUDE.md files (World, AI, Stealth, UI/HUD, UI/Dialogue, Data/NPCs)', '.claude/commands/NPC/create.md']
code_patterns: ['IInteractable + InteractionSystem focus / Interact dispatch; prompt via OnInteractionFocusChanged', 'Per-object InteractionHighlight + global outline colour (Shader.SetGlobalColor)', 'GameEventSO<T> payload struct + concrete channel in one file (Game.Core)', 'WorldStateManager = runtime world truth (KilledFact liveness)', 'EntityBrain enum switch state machine; non-combat states keep IsInCombat false', 'EntityPerception static Active list; witness profile on NPCEntity', 'DialogueSystem driven only by OnNPCDialogueRequested; DialogueUI DisplayState Topics/Text/Choices', 'Pure static helpers for testable logic; config SOs for tunables; GameLog + TAG']
test_patterns: ['NUnit EditMode in Assets/Tests/EditMode (Tests.EditMode asmdef references Game)', 'Pure static helper tests (InteractionSystemTests → InteractionFocus, StealthDetectionTests)', 'ScriptableObject.CreateInstance + DestroyImmediate for SO-backed tests (WitnessProfileTests)']
---

# Tech-Spec: Stealing: Ownership, Theft Witnesses, Chase & Scold Dialogue

**Created:** 2026-10-08

## Overview

### Problem Statement

The sneak system (`tech-spec-stealth-mode-vision-detection`, `tech-spec-neutral-npc-sneak-warnings-speech-bubbles`)
lets NPCs perceive a sneaking player, but nothing in the world can be owned, so there is no crime to commit or to be
caught for. Items, containers and doors are all free to take / open. The dialogue system can only be started by the
player interacting with an NPC (`NPCPresence` → `OnNPCDialogueRequested`), so an NPC cannot confront the player.

### Solution

1. An **ownership** marker on interactables (world items, containers, doors) referencing the owner's `NPCEntity` SO.
   Interacting with an owned object while its owner is alive is a **theft** (illegal interaction).
2. **Illegal-interaction feedback**: the interaction prompt is drawn in red with a theft verb ("Pick up" → "Steal"),
   and the interaction outline is tinted red.
3. **Theft witnesses**: when a theft happens, every non-hostile NPC with a witness profile (owner included) whose
   vision cone + line of sight currently covers the player notices it (sneaking shortens the range).
4. **Chase & scold**: the noticing NPC chases the player (non-combat); if it catches up it **engages the player in a
   forced dialogue** — one generic scold line (1–3 variations, item-specific variants such as "Give it back!") — and
   the player closes it. If the player escapes, the NPC gives up. A runtime theft counter is the hook for future
   consequences.

### Scope

**In Scope:**

- Ownership component on interactables (`ItemPickup` world items, `ContainerInteractable`, `DoorInteractable`),
  owner = `NPCEntity` SO; legal again when the owner is dead.
- Illegal check at interaction time; pickup verb becomes "Steal"; containers / doors keep their verb but in red.
- Red prompt text + red outline when the focused interactable is illegal.
- Theft event channel raised on an illegal interaction; witness check by NPCs with a witness profile (cone + LOS,
  sneak-shortened range).
- New non-combat chase state on the noticing NPC; give-up rule when the player escapes.
- Dialogue rework: NPC-initiated (forced) dialogue starting directly on a node, closable by the player.
- Generic scold lines (1–3 variations), item-pickup-specific variants.
- Runtime per-NPC theft counter (not saved).
- EditMode tests for the pure parts.

**Out of Scope:**

- Consequences beyond the scold: hostility (GDD: "caught stealing turns the NPC hostile" — deferred), bounty /
  reputation, guards arresting, forbidden areas, value-based escalation, repeated-offense escalation.
- Taking the stolen item back; stolen-item tracking in the inventory.
- Pickpocketing NPCs; the GDD's separate **F** steal key (stealing uses E with a red prompt instead).
- Theft from containers per item (opening an owned container is the crime).
- Saving theft counters / ownership changes; Steal skill / Dexterity modifiers.

## Context for Development

### Codebase Patterns

**Interaction (Game.World, `Scripts/World/`)**
- `IInteractable { string InteractPrompt; string NameTag; bool CanInteract; void Interact(); }` — `Interact()` is
  parameterless (project rule). Implemented by `ItemPickup` (`Game.Inventory`, prompt `"Pick Up"` unless
  `_promptOverride`; `Interact()` adds to the player inventory, marks the `SaveableObject` consumed, destroys the GO),
  `ContainerInteractable` (prompt `_interactPrompt` "Open Container" / `Lockable.LockedPrompt`; raises
  `OnContainerOpenRequested`), `DoorInteractable` (prompt Open / Close / locked; `IsOpen`; unlocked toggles locally,
  locked raises `OnDoorOpenRequested`), `EntityPresence` / `NPCPresence`.
- `InteractionSystem` (on the Player rig, `GetComponentInParent<PlayerStateManager>()` → `_playerState`): throttled
  sphere-cast scan (`InteractionConfigSO.scanInterval`), picks `best`; no focus while the cursor is unlocked or the
  player is dead. `InteractionFocus.HasFocusChanged(prevTarget, prevVerb, prevName, next, verb, name)` → `ApplyFocus`:
  un-highlights the previous `InteractionHighlight`, `TryGetComponent` the new one only on target change, sets
  `Shader.SetGlobalColor(OutlineColorId, InteractionFocus.ResolveOutlineColor(hasOverride, override,
  _config.outlineColor))`, `SetHighlighted(true)`, tints the crosshair, raises `OnInteractionFocusChanged`
  (`InteractionFocusData { Component target; string verb; string name; }`). `LateUpdate`: E pressed on a live,
  `CanInteract` target → `CurrentInteractable.Interact()` then forces a rescan.
- `InteractionPromptUI` (`Game.UI`) sets `_verbText` / `_nameText` only in the focus handler; prompt prefab texts are
  white TMP. `InteractionConfigSO` (`Game.World`): `outlineColor` (1, 0.85, 0.45), `outlineRenderingLayer`.
- Tests: `InteractionSystemTests` call `InteractionFocus` directly (signature changes must update them).

**Liveness / identity**
- `PersistentID` (`Game.World`): `Entity` (SO) + `KilledFact`; `RegisterDeath()` → `WorldStateManager.RegisterKill`.
  `WorldStateManager.Instance.IsKilled(KilledFact)` is the runtime truth for "dead". There is **no** link from an
  `NPCEntity` to its `KilledFact` today (only `PersistentID._killedFact` on the scene object). StartingTown NPCs:
  Blacksmith, bandit, Merchant, Elder, Villager, Innkeeper, Guard — each with `KilledFact_<Name>` and its own
  `NPC_<Name>` asset.
- Test candidates in StartingTown: containers `Chest` (-7.1, 0, -2.3, next to the Merchant) and `Locked Chest`;
  doors `Simple door` (2.5, 0.6, 20.3) and `Poor small door`; items `TestItem_Health_Potion` ×2, `SwordBase_World`,
  `Axe_World`.

**Dialogue (`Scripts/World/DialogueSystem.cs`, `Scripts/UI/Dialogue/DialogueUI.cs`)**
- Only entry: `OnNPCDialogueRequested` (`NPCDialogueRequestData { npcName, memories, graph, npcInventory,
  npcGoldSystem }`, `Game.Core`, `ScriptableObjects/Dialogue/`), raised by `NPCPresence.Interact()` (alive, out of
  combat). `HandleDialogueRequested` → `DialogueUI.Open(npcName, startNodes)` (topic list), `IsOpen = true`,
  `PlayerStateManager.SetInDialogue(true)` (also clears sneak), `CursorManager.Unlock()`. `Close()` resets and locks.
- `DialogueUI` states `Topics` / `Text` / `Choices`; `ShowTextNode` end-of-chain (`nextNode == null`) →
  `NotifyTopicCompleted()` + `RestoreTopics()` (back to the topic list + `[Farewell]`); `HandleCancel` (Escape)
  closes only in `Topics`; slot 1 / `_nextNodeButton` / click advance text. No way to start on a line and close at its
  end today.

**AI (from the previous specs)**
- `EntityPerception`: per-target hostile / witness; `CanWitness` (enabled `WitnessProfileSO`), `EyePosition`,
  private `CheckLineOfSight(eye, point)` against `StealthConfig.lineOfSightMask`; static `Active` list.
- `EntityBrain` states `Idle, Patrolling, Suspicious, Warning, Engaging, Attacking, Searching, Watching, Dead`;
  `ResumeNonCombat()` returns to Idle / the same waypoint; `_disengageState` captured from Idle / Patrolling;
  Engaging chases with `_agent.SetDestination` at `Entity.EngageSpeed`; `SetCombatState` drives `IsInCombat`
  (`NPCPresence` blocks dialogue in combat). Speech bubbles via `OnSpeechBubbleRequested`; `BarkSetSO.GetRandomLine`.
- `StealthDetection.ComputeVisibility(distance, angle, sightRange, viewAngle, proximity, edge, peripheral)` (pure).

**GDD (Stealth §3)**: "Getting caught stealing turns the NPC hostile immediately"; separate F steal key — both
overridden by this spec (chase + scold, E with a red prompt).

- Interactables implement `IInteractable` (`InteractPrompt`, `NameTag`, `CanInteract`, `Interact()`); found by
  `InteractionSystem` (Layer 8 raycast) which raises `OnInteractionFocusChanged` (`InteractionFocusData` target /
  verb / name) for `InteractionPromptUI`, and toggles `InteractionHighlight` (per-object `_overrideColor`; global
  outline colour via `Shader.SetGlobalColor` from `InteractionFocus.ResolveOutlineColor`).
- Dialogue: `NPCPresence.Interact()` raises `OnNPCDialogueRequested` (`NPCDialogueRequestData`: npcName, memories,
  graph, inventory, gold) → `DialogueSystem.HandleDialogueRequested` → `DialogueUI.Open(npcName, startNodes)`;
  `DialogueSystem.AdvanceToNode(node)` can show a `TextDialogueNode` directly.
- Witness perception: `EntityPerception` (per-target hostile / witness mode, `WitnessProfileSO` on `NPCEntity`);
  `EntityBrain` enum state machine with Watching (non-combat). Speech bubbles + `BarkSetSO` exist.
- Cross-system communication only via `GameEventSO<T>` channels; tunables in config SOs; GameLog + TAG.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Focus scan, outline colour, prompt event, `Interact()` dispatch — theft hook + illegal state |
| `Assets/_Game/Scripts/World/InteractionFocus.cs` | Pure focus helpers (`HasFocusChanged`, `ResolveOutlineColor`) |
| `Assets/_Game/Scripts/World/InteractionHighlight.cs` | Per-object outline, colour override |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs` | Prompt payload (add `illegal`) |
| `Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs` + `Prefabs/UI/InteractionPrompt.prefab` | Red prompt text |
| `Assets/_Game/Scripts/Inventory/ItemPickup.cs` | "Steal" verb |
| `Assets/_Game/Scripts/World/ContainerInteractable.cs`, `DoorInteractable.cs` | Owned containers / doors (door: closed only) |
| `Assets/_Game/Scripts/World/PersistentID.cs`, `Scripts/Core/State/WorldStateManager.cs` | Owner liveness (`IsKilled`) |
| `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs`, `WitnessProfileSO.cs` | Owner identity + KilledFact; theft barks / sight range |
| `Assets/_Game/Scripts/World/DialogueSystem.cs`, `Scripts/UI/Dialogue/DialogueUI.cs`, `ScriptableObjects/Dialogue/NPCDialogueRequestData.cs` | Forced (NPC-initiated) line dialogue |
| `Assets/_Game/Scripts/AI/EntityBrain.cs`, `EntityPerception.cs` | Theft witness reaction, Pursuing / Confronting states, LOS helper |
| `Assets/_Game/Scripts/Stealth/StealthDetection.cs` | Visibility math reused by the theft check |
| `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` | Chase tunables |
| `Assets/Tests/EditMode/InteractionSystemTests.cs`, `StealthDetectionTests.cs`, `WitnessProfileTests.cs` | Test style / signatures to update |

### Technical Decisions

- Owner reference = `NPCEntity` SO (decided with the user): works across scenes / saves, matched to a live NPC via
  `PersistentID.Entity`.
- Witnesses = any non-hostile NPC with a witness profile who sees the player at the moment of the theft (decided).
- Containers: opening an owned container is the theft (decided).
- After scold / escape: nothing beyond a runtime theft counter; the player keeps the item (decided).
- Chase is a non-combat state (dialogue with the chasing NPC is the outcome, not a fight).
- GDD deviations (user request overrides): no immediate hostility, no separate F steal key.
- **Owner liveness via `WorldStateManager`**: `NPCEntity` gains a `KilledFact` reference (one per NPC, mirrors its
  `PersistentID._killedFact`; `PersistentID.Awake` warns on a mismatch). An owned object is illegal while the owner's
  fact is not set; null fact → always illegal (warned in `OnValidate`). Keeps one field per owned object.
- **`Ownership` component** (`Game.World`) on the interactable root, next to `ItemPickup` / `ContainerInteractable` /
  `DoorInteractable`; derives the theft kind from its sibling. Doors are illegal only while closed (closing is free).
  Locked owned doors / containers: the interaction attempt itself is the theft.
- **Central theft hook in `InteractionSystem`**: on E, if the target's `Ownership.IsIllegal`, raise `OnTheftCommitted`
  **before** `Interact()` (an item is destroyed by its pickup). Interactables stay unaware of witnesses.
- **Illegal feedback**: `InteractionFocusData.illegal`; prompt verb + name tinted red; outline colour =
  `InteractionConfigSO.illegalOutlineColor` (overrides the per-object colour); legality is part of focus-change
  detection (an owner dying flips it in place). `ItemPickup` reads its sibling `Ownership` for the "Steal" verb.
- **Witnesses**: every `EntityBrain` listens to `OnTheftCommitted`; it sees the theft if its perception `CanWitness`,
  the thief is non-hostile to it, it is in Idle / Patrolling / Suspicious / Watching, and the thief is inside its cone
  at `WitnessProfileSO.TheftSightRange` (× sneak multiplier when sneaking; sneaking proximity radius) with an immediate
  LOS raycast. **Every** seeing witness chases (decided with the user) and shouts one bubble from the profile's alert
  barks; the **first** to catch the thief confronts (forced dialogue) and the others give up and reset. Coordination:
  each theft carries a `theftId`; a static `TheftPursuitRegistry` (Game.AI, like `TargetRegistry`) lets the first
  catcher claim the incident (`TryClaim(id)`), and other pursuers end their chase when `IsClaimed(id)`.
- **Pursuing** (non-combat, `IsInCombat` false): runs to the thief at `EngageSpeed`; gives up after losing LOS for
  `theftChaseLoseSightTime`, beyond `theftChaseMaxDistance`, after `theftChaseMaxDuration`, or if the thief dies.
  **Confronting**: within `theftCatchDistance` while the cursor is locked → stop, face, raise a forced dialogue
  (`NPCDialogueRequestData.forcedLine` from the profile's scold barks, item variant for pickups); waits for the dialogue
  to close (cursor locked again) then resumes its routine. Runtime `TheftsWitnessed` counter.
- **Forced dialogue**: `DialogueSystem` opens `DialogueUI.OpenLine(npcName, line)` (Text state, advance / Escape →
  `Close()`), ignored with a warning while a dialogue is already open.

## Implementation Plan

### Tasks

**A. Data foundations**

- [x] Task 1: Owner liveness on `NPCEntity`
  - File: `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs`
  - Action: Under a new `[Header("Identity")]`, add `[Tooltip("This NPC's KilledFact (same asset as its PersistentID). Owned objects become free to take once it is set.")] [SerializeField] private KilledFact _killedFact;` + `public KilledFact KilledFact => _killedFact;` (`KilledFact` is `Game.Core`).
  - File: `Assets/_Game/Scripts/World/PersistentID.cs`
  - Action: In `Awake`, after the existing null check: `if (entityType is Game.NPC.NPCEntity npc && npc.KilledFact != _killedFact) GameLog.Warn(TAG, $"{gameObject.name}: NPCEntity '{npc.name}' KilledFact ({(npc.KilledFact != null ? npc.KilledFact.name : "none")}) differs from PersistentID ({_killedFact.name}) — ownership liveness will be wrong");`.
  - Data: set `_killedFact` on all 7 `Data/NPCs/*/NPC_*.asset` from the matching StartingTown `PersistentID` (editor script via MCP `execute_code`: for each `PersistentID` whose `Entity is NPCEntity`, assign its `KilledFact` to the asset, `SetDirty`, `SaveAssets`).

- [x] Task 2: Theft data on the witness profile
  - File: `Assets/_Game/ScriptableObjects/Entities/NPC/WitnessProfileSO.cs`
  - Action: Add `[Header("Theft")]`: `_theftSightRange = 10f` (tooltip: range (m) at which the NPC notices a theft by a standing player; shortened by `StealthConfigSO.sneakSightRangeMultiplier` while sneaking; 0 = never notices thefts), `BarkSetSO _theftAlertBarks` (shouted in a bubble when it starts chasing), `BarkSetSO _theftScoldBarks` (forced-dialogue line, any theft), `BarkSetSO _itemTheftScoldBarks` (forced-dialogue line for item pickups; falls back to `_theftScoldBarks`). Properties `TheftSightRange`, `TheftAlertBarks`, `TheftScoldBarks`, `ItemTheftScoldBarks`. `OnValidate`: `_theftSightRange = Mathf.Max(0f, …)`. Update the class summary (also covers thefts).
  - Data: NEW `Assets/_Game/Data/NPCs/Barks/Barks_TheftAlert.asset` (`Thief!`, `Hey! Stop!`, `Stop right there!`), `Barks_TheftScold.asset` (`What are you doing?`, `That's not yours!`, `Hands off my things!`), `Barks_TheftScold_Item.asset` (`Give it back!`, `Hey, that's mine!`, `Put that back, thief!`). Assign all three on `Data/Entities/WitnessProfile_Humanoid.asset`, `_theftSightRange: 10`.

- [x] Task 3: Chase tunables
  - File: `Assets/_Game/ScriptableObjects/Config/StealthConfigSO.cs` (+ `Data/Config/StealthConfig.asset`)
  - Action: `[Header("Theft Chase")]`: `theftCatchDistance = 1.8f` (flat m to confront), `theftChaseLoseSightTime = 4f` (s without LOS before giving up), `theftChaseMaxDistance = 20f` (flat m to the thief before giving up), `theftChaseMaxDuration = 30f` (s), `theftConfrontOpenTimeout = 0.5f` (s a confronting NPC waits for the dialogue to open before resuming), `theftBubbleDuration = 2.5f`, `theftAlertBubblePriority = 1` (int — above witness warnings). `OnValidate`: clamp the floats ≥ 0 (`theftCatchDistance` ≥ 0.5).

- [x] Task 4: Illegal outline colour
  - File: `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` (+ `Data/Config/InteractionConfig.asset`, locate via `t:InteractionConfigSO`)
  - Action: Under Highlight: `[Tooltip("Outline color of a focused interactable the player may not take / open (owned by a living NPC). Overrides InteractionHighlight's colour.")] public Color illegalOutlineColor = new Color(1f, 0.25f, 0.2f, 1f);`.

**B. Ownership & theft event (World)**

- [x] Task 5: Theft channel
  - File: NEW `Assets/_Game/ScriptableObjects/Events/GameEventSO_TheftCommitted.cs` (`namespace Game.Core`, one file like `GameEventSO_InteractionFocus.cs`)
  - Action: `public enum TheftKind { Other, Item, Container, Door }`; `[System.Serializable] public struct TheftCommittedData { public int theftId; public Transform thief; public Vector3 position; public Game.NPC.NPCEntity owner; public TheftKind kind; public string objectName; }` (XML-doc: `thief` is a runtime scene ref, never stored in an SO; `theftId` identifies one incident — unique per play session); `[CreateAssetMenu(menuName = "Game/Events/Theft Committed", fileName = "NewTheftCommittedEvent")] public class GameEventSO_TheftCommitted : GameEventSO<TheftCommittedData> { }`.
  - Asset: NEW `Assets/_Game/Data/Events/OnTheftCommitted.asset`.

- [x] Task 6: `Ownership` component
  - File: NEW `Assets/_Game/Scripts/World/Ownership.cs` (`namespace Game.World`, TAG `[Ownership]`)
  - Action:
    - `[Tooltip("NPC that owns this object. Taking / opening it while the owner lives is a theft. Null = not owned.")] [SerializeField] private NPCEntity _owner;`
    - Awake: cache `TryGetComponent` of `ItemPickup`, `ContainerInteractable`, `DoorInteractable` → `Kind` (`TheftKind.Item` / `Container` / `Door`, else `Other`); `_owner != null && _owner.KilledFact == null` → `GameLog.Warn` (stays illegal even after the owner dies).
    - `public NPCEntity Owner => _owner; public TheftKind Kind { get; private set; }`
    - `public bool IsIllegal => IsIllegalInteraction(_owner != null, IsOwnerKilled(), Kind == TheftKind.Door, _door != null && _door.IsOpen);` where `IsOwnerKilled()` = `_owner != null && _owner.KilledFact != null && WorldStateManager.Instance != null && WorldStateManager.Instance.IsKilled(_owner.KilledFact)`.
    - Pure `public static bool IsIllegalInteraction(bool hasOwner, bool ownerKilled, bool isDoor, bool doorOpen) => hasOwner && !ownerKilled && !(isDoor && doorOpen);` (closing an owned door is free).
    - Editor `OnValidate`: warn when no sibling `IInteractable` is found.

- [x] Task 7: "Steal" verb on owned pickups
  - File: `Assets/_Game/Scripts/Inventory/ItemPickup.cs`
  - Action: `private const string STEAL_PROMPT = "Steal";` cache `TryGetComponent(out _ownership)` in Awake; `InteractPrompt => _ownership != null && _ownership.IsIllegal ? STEAL_PROMPT : (string.IsNullOrEmpty(_promptOverride) ? "Pick Up" : _promptOverride);`. Containers and doors keep their verb (shown in red).

- [x] Task 8: Focus helpers
  - File: `Assets/_Game/Scripts/World/InteractionFocus.cs`
  - Action: `HasFocusChanged(previous, previousVerb, previousName, previousIllegal, next, nextVerb, nextName, nextIllegal)` (adds `previousIllegal != nextIllegal`); new `ResolveOutlineColor(bool illegal, Color illegalColor, bool hasOverride, Color overrideColor, Color defaultColor) => illegal ? illegalColor : ResolveOutlineColor(hasOverride, overrideColor, defaultColor)`.
  - File: `Assets/Tests/EditMode/InteractionSystemTests.cs` — update existing `HasFocusChanged` calls (pass `false, false`), add tests for an illegal-only change and the illegal colour.

- [x] Task 9: Prompt payload
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_InteractionFocus.cs`
  - Action: add `public bool illegal; // focused interaction is a theft (prompt drawn in red)` to `InteractionFocusData`.

- [x] Task 10: Illegal focus + theft hook in `InteractionSystem`
  - File: `Assets/_Game/Scripts/World/InteractionSystem.cs`
  - Action:
    - Fields: `[SerializeField] private GameEventSO_TheftCommitted _onTheftCommitted;` (Event Channels; Awake warns when null: thefts go unnoticed); runtime `Ownership _focusedOwnership; bool _focusedIllegal; static int s_nextTheftId` (reset in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`).
    - `Update` scan: `Ownership ownership = ReferenceEquals(best, _previousInteractable) ? _focusedOwnership : GetOwnership(best)` (`TryGetComponent` only on a target change); `bool illegal = ownership != null && ownership.IsIllegal;` pass to `HasFocusChanged(…, _focusedIllegal, …, illegal)` and `ApplyFocus(best, verb, targetName, illegal, ownership)`.
    - `ApplyFocus`: store `_focusedOwnership` / `_focusedIllegal`; outline colour via the new `ResolveOutlineColor(illegal, _config.illegalOutlineColor, …)`; raise `InteractionFocusData { …, illegal = illegal }`. `ClearFocus` passes `false, null`.
    - `LateUpdate`, inside the E-press branch, **before** `Interact()`: `if (_focusedOwnership != null && _focusedOwnership.IsIllegal) RaiseTheft(_focusedOwnership);` (re-evaluated at press time).
    - `RaiseTheft(Ownership o)`: `_onTheftCommitted?.Raise(new TheftCommittedData { theftId = ++s_nextTheftId, thief = _playerState != null ? _playerState.transform : transform.root, position = o.transform.position, owner = o.Owner, kind = o.Kind, objectName = InteractionFocus.ResolveName(CurrentInteractable) })` + `GameLog.Info(TAG, $"Theft #{id}: {kind} '{name}' owned by {owner.name}")`.

- [x] Task 11: Red prompt
  - File: `Assets/_Game/Scripts/UI/HUD/InteractionPromptUI.cs`
  - Action: `[Header("Style")] [SerializeField] private Color _illegalColor = new Color(1f, 0.35f, 0.3f, 1f);` cache `_verbDefaultColor` / `_nameDefaultColor` from the texts in Awake; in `HandleFocusChanged` set `_verbText.color` / `_nameText.color` to `data.illegal ? _illegalColor : default` (the `E` key label keeps its colour).

**C. Forced (NPC-initiated) dialogue**

- [x] Task 12: Request payload
  - File: `Assets/_Game/ScriptableObjects/Dialogue/NPCDialogueRequestData.cs`
  - Action: add `public string forcedLine; // non-empty → NPC-initiated: show only this line, the player closes it (no topics)`.

- [x] Task 13: `DialogueSystem` forced line
  - File: `Assets/_Game/Scripts/World/DialogueSystem.cs`
  - Action: At the top of `HandleDialogueRequested` (after the `_dialogueUI` null check): `if (IsOpen) { GameLog.Warn(TAG, $"Dialogue request from {data.npcName} ignored — a dialogue is already open"); return; }`. Then `if (!string.IsNullOrEmpty(data.forcedLine))`: clear `_currentNPCMemory/_currentGraph/_currentNPCInventory/_currentNPCGoldSystem/_currentStartNode`, `_dialogueUI.OpenLine(data.npcName, data.forcedLine)`, `IsOpen = true`, `SetInDialogue(true)`, `CursorManager.Unlock()`, log `"{npcName} confronts the player"`, return.

- [x] Task 14: `DialogueUI.OpenLine`
  - File: `Assets/_Game/Scripts/UI/Dialogue/DialogueUI.cs`
  - Action: `private bool _forcedLine;`. `public void OpenLine(string npcName, string line)`: panel null → Error + return; `_forcedLine = true`; `_cachedStartNodes = Array.Empty`; panel active; name text; `ClearContents()`; `_pendingNextNode = null`; `_responseText.text = line`; `_nextNodeButton` + `_slotCallbacks[1]` → `_dialogueSystem.Close()`; `SetState(DisplayState.Text)`. `OnPointerClick` (Text state): `if (_forcedLine) { _dialogueSystem.Close(); return; }` before the existing logic. `HandleCancel`: also close when `_forcedLine`. `Close()`: `_forcedLine = false`.

**D. Theft witnesses, pursuit, confrontation (AI)**

- [x] Task 15: Pure theft rules
  - File: NEW `Assets/_Game/Scripts/Stealth/TheftDetection.cs` (`namespace Game.Stealth`, `public static class`)
  - Action:
    - `float EffectiveTheftRange(float theftSightRange, bool sneaking, float sneakSightMultiplier)` → `StealthDetection.EffectiveSightRange(...)`.
    - `bool IsInView(float distance, float angleDeg, float sightRange, float viewAngle, float proximityRadius)` → `StealthDetection.ComputeVisibility(distance, angleDeg, sightRange, viewAngle, proximityRadius, 1f, 1f) > 0f` (geometry only; LOS is the caller's).
    - `bool ShouldGiveUp(float timeWithoutSight, float loseSightTime, float distance, float maxDistance, float elapsed, float maxDuration)` → `timeWithoutSight >= loseSightTime || distance > maxDistance || elapsed >= maxDuration`.
    - `bool CanCatch(float distance, float catchDistance, bool cursorLocked)` → `cursorLocked && distance <= catchDistance`.
  - File: NEW `Assets/_Game/Scripts/AI/TheftPursuitRegistry.cs` (`namespace Game.AI`, `public static class`, like `TargetRegistry`)
  - Action: `HashSet<int> _claimed`; `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetOnPlay()`; `public static bool TryClaim(int theftId) => _claimed.Add(theftId);` (first catcher wins); `public static bool IsClaimed(int theftId) => _claimed.Contains(theftId);`.

- [x] Task 16: Perception helpers
  - File: `Assets/_Game/Scripts/AI/EntityPerception.cs`
  - Action:
    - `public bool HasLineOfSightTo(Vector3 point) => _config != null && CheckLineOfSight(EyePosition, point);`
    - `public bool CanSeeTheft(FactionMember thief)`: false unless `IsActive && CanWitness && thief != null` and `WitnessProfile.TheftSightRange > 0`; `bool sneaking = thief.StealthTarget != null && thief.StealthTarget.IsSneaking`; `Vector3 point = thief.StealthTarget != null ? thief.StealthTarget.VisibilityPoint : thief.Transform.position + Vector3.up * _config.standingVisibilityHeight`; flat `toTarget`, `distance`, `angle = StealthDetection.AngleToTarget(transform.forward, toTarget)`; `range = TheftDetection.EffectiveTheftRange(profile.TheftSightRange, sneaking, _config.sneakSightRangeMultiplier)`; `prox = StealthDetection.EffectiveProximityRadius(Entity.ProximityRadius, sneaking, _config.sneakProximityMultiplier)`; return `TheftDetection.IsInView(distance, angle, range, Entity.ViewAngle, prox) && HasLineOfSightTo(point)`.

- [x] Task 17: Pursuing / Confronting in `EntityBrain`
  - File: `Assets/_Game/Scripts/AI/EntityBrain.cs`
  - Action:
    - Enum: add `Pursuing, Confronting` before `Dead`; class summary: theft reaction.
    - Fields (`[Header("Theft")]`): `GameEventSO_TheftCommitted _onTheftCommitted` (tooltip: listened to — every NPC that sees a theft chases the thief), `GameEventSO_NPCDialogueRequest _onDialogueRequested` (tooltip: raised to confront a caught thief — forced dialogue). Runtime: `int _pursuitTheftId; TheftKind _pursuitKind; float _pursuitElapsed; float _pursuitNoSightTimer; float _pursuitLosTimer; bool _pursuitHasLos; Vector3 _pursuitLastSeen; float _confrontTimer; bool _confrontOpened; int _lastAlertBarkIndex = -1; int _lastScoldBarkIndex = -1;`. Public `int TheftsWitnessed { get; private set; }` (runtime, escalation hook).
    - `OnEnable` / `OnDisable`: Add / RemoveListener `HandleTheftCommitted` on `_onTheftCommitted` (null-guarded; independent of the health subscription guard).
    - `HandleTheftCommitted(TheftCommittedData d)`: return unless `_state` ∈ {Idle, Patrolling, Suspicious, Watching, Pursuing} and `PerceptionActive && _perception.CanWitness`; `thief = d.thief != null ? d.thief.GetComponentInParent<FactionMember>() : null`; `!IsLive(thief)` → return; `_selfFactionMember.Faction != null && thief.Faction != null && _selfFactionMember.Faction.IsHostileTo(thief.Faction)` → return (hostiles don't police theft); `!_perception.CanSeeTheft(thief)` → return. Already Pursuing → adopt the new `theftId` / kind and reset the timers (keep chasing); else `TransitionToPursuing(thief, d)`.
    - `TransitionToPursuing`: capture `_disengageState` from Idle / Patrolling (Suspicious / Watching already captured it); `_currentTarget = thief`; `_state = Pursuing`; `_perception.ResetPerception()`; agent `isStopped = false`, `speed = Entity.EngageSpeed`, `stoppingDistance = StealthConfig.theftCatchDistance * 0.8f`, `SetDestination(thief.Transform.position)`; timers 0, `_pursuitHasLos = true`, `_pursuitLastSeen = thief position`; `TheftsWitnessed++`; **no** `SetCombatState`; raise one alert bubble (`profile.TheftAlertBarks.GetRandomLine(ref _lastAlertBarkIndex)`, priority `theftAlertBubblePriority`, duration `theftBubbleDuration`, same speaker/anchor rules as `TryWarn`; skip silently when no barks / channel); `GameLog.Info("{name} saw theft #{id} — chasing")`.
    - `HandlePursuing()`: `!IsLive(_currentTarget)` → `EndPursuit("thief lost")`; `TheftPursuitRegistry.IsClaimed(_pursuitTheftId)` → `EndPursuit("another witness caught the thief")`; every `StealthConfig.losCheckInterval` refresh `_pursuitHasLos = _perception.HasLineOfSightTo(<visibility point as in CanSeeTheft>)`; LOS → `_pursuitNoSightTimer = 0`, `_pursuitLastSeen = target pos`; else `+= dt`; `_pursuitElapsed += dt`; flat distance; `TheftDetection.ShouldGiveUp(...)` → `EndPursuit("thief escaped")`; `TheftDetection.CanCatch(distance, theftCatchDistance, CursorManager.IsLocked) && TheftPursuitRegistry.TryClaim(_pursuitTheftId)` → `TransitionToConfronting()`; else `_agent.SetDestination(_pursuitHasLos ? target pos : _pursuitLastSeen)`.
    - `TransitionToConfronting()`: `_state = Confronting`; agent stopped; `profile = Entity.WitnessProfile`; `BarkSetSO set = _pursuitKind == TheftKind.Item && profile.ItemTheftScoldBarks != null ? profile.ItemTheftScoldBarks : profile.TheftScoldBarks`; `line = set != null ? set.GetRandomLine(ref _lastScoldBarkIndex) : null`; empty → `DEFAULT_SCOLD_LINE` const `"Hey! What do you think you're doing?"` + one warning; `_onDialogueRequested == null` → Warn, `EndPursuit("no dialogue channel")`, return; raise `new NPCDialogueRequestData { npcName = Entity.entityName, forcedLine = line }`; `_confrontTimer = 0; _confrontOpened = false`; log.
    - `HandleConfronting()`: `!IsLive(_currentTarget)` → `EndPursuit`; `FacePoint(target, StealthConfig.witnessTurnSpeed)`; `_confrontTimer += dt`; `!CursorManager.IsLocked` → `_confrontOpened = true`; else if `_confrontOpened || _confrontTimer >= theftConfrontOpenTimeout` → `EndPursuit("scolded the thief")`.
    - `EndPursuit(string reason)`: `_currentTarget = null; _perception.ResetPerception(); GameLog.Info(...); ResumeNonCombat();`.
    - Switch: `case Pursuing: HandlePursuing(); case Confronting: HandleConfronting();`.
    - `HandleHealthChanged` allowed states: add `Pursuing` and `Confronting` (a hostile hit still triggers the combat reaction).
    - `IsUnawareOf` unchanged (Pursuing / Confronting are aware).
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab` — wire `_onTheftCommitted` = `OnTheftCommitted.asset`, `_onDialogueRequested` = `OnNPCDialogueRequested.asset` on `EntityBrain` (inherited by variants; verify `NPC_base Variant` and monsters).
  - Player prefab: wire `InteractionSystem._onTheftCommitted` = `OnTheftCommitted.asset` (find the prefab holding `InteractionSystem`; edit through MCP / Prefab Mode).

**E. Test content, tests, docs**

- [x] Task 18: Test ownership in StartingTown
  - File: `Assets/_Game/Scenes/StartingTown.unity` (scene-loading quirk: `Scenes/CLAUDE.md`)
  - Action: Add `Ownership` to `Chest` (owner `NPC_Merchant`), `TestItem_Health_Potion (1)` (owner `NPC_Blacksmith`), `Simple door` (owner `NPC_Villager`); save the scene.

- [x] Task 19: EditMode tests
  - Files: NEW `Assets/Tests/EditMode/TheftDetectionTests.cs`, NEW `Assets/Tests/EditMode/OwnershipTests.cs`, NEW `Assets/Tests/EditMode/TheftPursuitRegistryTests.cs`, `Assets/Tests/EditMode/InteractionSystemTests.cs`
  - Action: see Testing Strategy; run all EditMode tests.

- [x] Task 20: Docs
  - Files: `Assets/_Game/Scripts/World/CLAUDE.md` (`Ownership`, theft hook, illegal focus, forced dialogue in `DialogueSystem`), `Assets/_Game/Scripts/AI/CLAUDE.md` (theft witnesses, Pursuing / Confronting, `TheftPursuitRegistry`, `TheftsWitnessed`), `Assets/_Game/Scripts/Stealth/CLAUDE.md` (`TheftDetection`, theft range + config rows), `Assets/_Game/Scripts/UI/HUD/CLAUDE.md` (red prompt, `illegal` payload, `OnTheftCommitted`), `Assets/_Game/Scripts/UI/Dialogue/CLAUDE.md` (`OpenLine` forced mode), `Assets/_Game/Data/NPCs/CLAUDE.md` (`NPCEntity.KilledFact`, theft barks), `.claude/commands/NPC/create.md` (set `_killedFact` after the PersistentID exists).

### Acceptance Criteria

- [ ] AC 1: Given the `Chest` owned by the living Merchant, when the player focuses it, then the prompt reads "[E] Open Container / Chest" with verb and name in red and the outline is red.
- [ ] AC 2: Given `TestItem_Health_Potion (1)` owned by the Blacksmith, when focused, then the verb reads "Steal" in red; the unowned potion next to it still reads "Pick Up" in the default colour with the default outline.
- [ ] AC 3: Given the owned `Simple door` is open, when focused, then "Close Door" is shown in the default colour (closing is legal); when closed, "Open Door" is red.
- [ ] AC 4: Given an owned object whose owner is dead (KilledFact set), when focused, then the prompt and outline are normal, the pickup verb is "Pick Up", and interacting raises no theft.
- [ ] AC 5: Given the focus stays on an owned object, when its owner dies, then the prompt and outline switch to normal at the next scan without refocusing.
- [ ] AC 6: Given a neutral NPC facing the player within 10 m with clear LOS, when the player (standing) steals the potion, then the NPC shouts an alert bubble and runs toward the player (not in combat: `IsInCombat` false).
- [ ] AC 7: Given the same NPC, when the player steals while sneaking at 8 m inside its cone, then it does not notice (sneaking range 6 m); within 6 m it does.
- [ ] AC 8: Given a theft behind an NPC's back (outside the cone, beyond the sneaking proximity radius) or behind a wall, when it happens, then that NPC does not react.
- [ ] AC 9: Given two NPCs saw the theft, when both chase and one reaches the player first, then it confronts and the other gives up and resumes its routine.
- [ ] AC 10: Given a chasing NPC reaches 1.8 m of the player with no menu open, when it catches up, then it stops, faces the player and a dialogue opens with the NPC's name and one scold line ("Give it back!"-style for item pickups, generic for containers / doors), with no topic list.
- [ ] AC 11: Given the scold dialogue is open, when the player clicks, presses 1 / the next button or Escape, then the dialogue closes, the cursor locks, and the NPC resumes Idle / its patrol waypoint.
- [ ] AC 12: Given a chasing NPC, when the player stays out of its LOS for 4 s, gets more than 20 m away, or 30 s pass, then it gives up and resumes its routine.
- [ ] AC 13: Given a chasing NPC reaches the player while the inventory (or another menu / dialogue) is open, when it arrives, then it waits and confronts once the menu closes (or gives up per AC 12).
- [ ] AC 14: Given a dialogue is already open, when a forced dialogue request arrives, then it is ignored with a warning and the confronting NPC resumes its routine after the open timeout.
- [ ] AC 15: Given hostile entities (bandit, spiders) see a theft, when it happens, then they do not chase (their existing hostile behaviour is unchanged).
- [ ] AC 16: Given a chasing / confronting NPC, when a hostile monster hits it, then it reacts with the normal combat behaviour.
- [ ] AC 17: Given each theft, when raised, then `OnTheftCommitted` carries a new `theftId`, the owner, the kind and the object name, and every witness that chased increments its `TheftsWitnessed`.
- [ ] AC 18: Given an owned object without an `InteractionHighlight`, when focused, then only the prompt turns red (no error).
- [ ] AC 19: Given an `NPCEntity` whose `KilledFact` differs from its `PersistentID`'s, when the scene loads, then a warning names both facts.
- [ ] AC 20: Given normal (non-owned) interactions and NPC talk, when used, then behaviour, prompt colours and dialogue (topics, Farewell) are unchanged.

### Dependencies

- Builds on `tech-spec-stealth-mode-vision-detection` and `tech-spec-neutral-npc-sneak-warnings-speech-bubbles` (both
  completed): `EntityPerception` witness mode, `WitnessProfileSO`, `BarkSetSO`, speech bubbles,
  `PlayerStateManager.SetInDialogue` clearing sneak.
- No new packages.

### Testing Strategy

**EditMode (NUnit):**
- `TheftDetectionTests`: `EffectiveTheftRange` (standing 10 → 10, sneaking → 6 at ×0.6); `IsInView` (ahead within range
  → true; beyond range → false; outside the cone → false; behind inside proximity → true; zero range → false);
  `ShouldGiveUp` (each of the three conditions alone → true; none → false; boundary values); `CanCatch` (cursor
  unlocked → false; distance at / beyond catch distance).
- `OwnershipTests` (`Ownership.IsIllegalInteraction`): no owner → false; owner alive → true; owner killed → false;
  door open → false; door closed → true; non-door "open" flag ignored.
- `TheftPursuitRegistryTests`: first `TryClaim(id)` true, second false; `IsClaimed` before / after; two ids independent
  (use distinct large ids — the registry is static).
- `InteractionSystemTests`: updated `HasFocusChanged` signature; illegal-only change → true; `ResolveOutlineColor`
  illegal wins over the override; legal falls back to the old rule.

**Manual (StartingTown):**
1. Look at the Chest, the owned potion, the open / closed Simple door, and the unowned potion / sword: prompt colours,
   verbs and outlines match AC 1–3.
2. Steal the potion in front of the Blacksmith → alert bubble, chase, catch, scold "Give it back!"-style, close → he
   walks back.
3. Steal while sneaking at the edge of the range, and from behind → no reaction.
4. Steal, then sprint away / break LOS → the NPC gives up.
5. Open the Chest next to the Merchant (and Elder nearby) → several chase, the first confronts, the others reset.
6. Kill test: set `KilledFact_Merchant` (Quest Explorer / debug) → the Chest becomes legal.
7. Talk to an NPC normally and steal near the bandit / spiders → unchanged behaviour.

### Notes

- **Risk — NPC dialogue while being chased.** Pursuing is non-combat, so the player can press E on the chasing NPC and
  open its normal topic dialogue; the NPC then waits (cursor unlocked) and scolds right after it closes. Acceptable
  for now; a future pass can make `NPCPresence` route to the scold.
- **Risk — catch distance vs agent avoidance.** NavMesh avoidance can hold the NPC ~1 m away (see the spider note in
  `Scripts/AI/CLAUDE.md`); `stoppingDistance = 0.8 × catchDistance` keeps it inside 1.8 m. Tune in play.
- **Risk — player speed.** Running / sprinting outpaces `EngageSpeed` (4) — escaping is intended to be possible;
  tune `EngageSpeed` / the give-up rules if chases feel trivial.
- Locked owned doors / containers: the unlock attempt is the theft, even if the skill check fails.
- Stolen items are not marked; the player keeps them; dropping one creates an unowned world item.
- `TheftsWitnessed`, claims and theft ids are runtime only (not saved).
- GDD deviations: no immediate hostility, no F steal key — revisit with the consequence system.
- **Future (out of scope):** consequences by value / forbidden area / repeat offence (`TheftsWitnessed`), hostility
  or guards, taking the item back, marking stolen items, per-item container theft, pickpocketing, Steal skill /
  Dexterity modifiers, owner-specific scold lines (owner vs bystander).

## Review Notes

- Adversarial review completed (inline self-review of the diff from `cc026c2`).
- Findings: 6 total, 3 fixed, 3 skipped. Resolution approach: auto-fix.
  - Fixed F1: a hostile spotted by the radius scan while Pursuing takes over (normal combat reaction, like Watching).
  - Fixed F4: `ItemPickup.CanInteract => enabled` — a pickup disabled by Awake / Start (no item / no player inventory)
    offers no prompt, so no theft is raised for a no-op pickup.
  - Fixed F5: `Ownership` warns about a missing owner `KilledFact` only in `Awake` (not on every Inspector edit).
  - Skipped F2 (talking to a chasing NPC, then scolded — accepted risk in Notes), F3 (every failed unlock attempt on an
    owned lock is a new theft — by design), F6 (redundant `CanWitness` check — noise).
- Verified in Play Mode (StartingTown): theft in front of the Blacksmith → Blacksmith, Guard, Merchant pursue (not in
  combat), bandit / spiders ignore; Blacksmith catches → forced line "Give it back!", others resume Idle; closing the
  dialogue → Blacksmith back to Idle. Manual checks still open: prompt colours (AC 1–3), sneak range (AC 7), escape
  rules (AC 12).
