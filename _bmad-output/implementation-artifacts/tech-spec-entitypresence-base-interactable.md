---
title: 'EntityPresence — base interactable for all entities'
slug: 'entitypresence-base-interactable'
created: '2026-06-02'
status: 'Implementation Complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1 (URP 17)', 'C# / Game.asmdef', 'Unity Prefab Variants (raw YAML edits)', 'Unity Test Framework (EditMode, NUnit)']
files_to_modify:
  - 'Assets/_Game/Scripts/World/EntityPresence.cs (NEW)'
  - 'Assets/_Game/Scripts/World/EntityPresence.cs.meta (NEW)'
  - 'Assets/_Game/Scripts/AI/NPCPresence.cs (refactor to subclass)'
  - 'Assets/_Game/Prefabs/Entities/Entity_base.prefab (add EntityPresence + Layer-8 InteractionCollider child)'
  - 'Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab (remove inherited EntityPresence + duplicate collider)'
  - 'Assets/_Game/Scripts/World/CLAUDE.md (doc)'
  - 'Assets/_Game/Scripts/AI/CLAUDE.md (doc)'
  - 'Assets/_Game/Prefabs/CLAUDE.md (doc)'
code_patterns:
  - 'IInteractable on root; Layer-8 trigger collider on child; InteractionSystem climbs via GetComponentInParent'
  - 'Optional deps (EntityHealth/ICombatStateProvider/PersistentID) polled via GetComponent, null-guarded — never event Action across boundaries'
  - 'GameLog wrapper only; TAG = "[System]"; _camelCase [SerializeField] private fields; PascalCase classes; one MonoBehaviour per file'
  - 'Shallow (1-level) inheritance only — project-context prefers composition over deep chains'
test_patterns:
  - 'Assets/Tests/EditMode/*Tests.cs, NUnit [Test], class named {System}Tests, Tests.EditMode.asmdef refs Game'
---

# Tech-Spec: EntityPresence — base interactable for all entities

**Created:** 2026-06-02

## Overview

### Problem Statement

`IInteractable` detection currently only reaches NPCs. `NPCPresence` (an `IInteractable`)
and a Layer-8 `InteractionCollider` child are *added by* `NPC_base Variant.prefab`. The base
`Entity_base.prefab` — and therefore monsters like `Monster_DarknessSpider Variant.prefab` —
have **no `IInteractable` and no Layer-8 collider**, so `InteractionSystem`'s raycast (which
masks Layer 8 and climbs via `GetComponentInParent<IInteractable>()`) can never detect them.
As a side effect, the world-space name + health bar (`EntityUI`, already present on
`Entity_base`) never appears for monsters, because the name-tag scan also requires an
`IInteractable` in the parent chain.

We want every entity (NPCs and monsters) to carry an interactable surface so that (a) the
world-space name/HP UI shows on hover for monsters, and (b) a future loot spec can plug a
"loot the corpse" interaction into a killed monster without re-introducing any detection logic.

### Solution

Extract the generic `IInteractable` plumbing out of `NPCPresence` into a new base
MonoBehaviour `EntityPresence`, placed on `Entity_base.prefab` together with a Layer-8
`InteractionCollider` child. Refactor `NPCPresence` to inherit from `EntityPresence`,
overriding only the dialogue-specific `Interact()` / `InteractPrompt`. On `NPC_base Variant`,
remove the now-inherited `EntityPresence` component (NPCPresence replaces it) and remove its
now-duplicate `InteractionCollider` child (inherited from the base). `Monster_DarknessSpider`
and any other `Entity_base` variant inherit `EntityPresence` + collider automatically.

### Scope

**In Scope:**
- New `EntityPresence.cs` (base `IInteractable`: `NameTag`, gated `CanInteract`, no-op virtual `Interact()`, empty `InteractPrompt`).
- Refactor `NPCPresence` → `NPCPresence : EntityPresence`, overriding `InteractPrompt` ("Talk"), `CanInteract` (alive & not in combat), and `Interact()` (dialogue). Keep its existing script GUID `6aadb17071c6eeb488175719a41fdb14`.
- `Entity_base.prefab`: add `EntityPresence` component to root (wire `_persistentID`) + add a Layer-8 `InteractionCollider` child (trigger CapsuleCollider, Radius 0.5 / Height 2.0 / Center Y 1.0).
- `NPC_base Variant.prefab`: remove the inherited `EntityPresence` component and the now-duplicate added `InteractionCollider` child (keep `NPCPresence`).
- Monster (`Monster_DarknessSpider Variant.prefab`): verify it inherits `EntityPresence` + collider and shows name/HP UI; no per-prefab edits expected.
- Doc updates: `Scripts/World/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Prefabs/CLAUDE.md` (interactable now lives on the base; two-collider pattern note).

**Out of Scope:**
- The actual loot / dead-corpse interaction (future spec) — `EntityPresence.Interact()` stays a no-op.
- Persisting looted/interacted state across save & scene reload.
- Authoring `entityName` on monster Entity SOs (data task; name shows blank until set).
- Any new EditMode tests beyond confirming the refactor compiles (no behavior to assert yet — confirm at Step 2/4).

## Context for Development

### Codebase Patterns

- `InteractionSystem` (`Assets/_Game/Scripts/World/InteractionSystem.cs`): two `Physics.SphereCastNonAlloc` scans on `_raycastMask` (Layer 8 "Interactable"). Scan 1 (interaction range) picks the best `IInteractable` whose `CanInteract` is true → drives crosshair highlight + `[E] {InteractPrompt}` prompt + `Interact()` on press. Scan 2 (name range) shows `EntityUI` for any `IInteractable` in range **regardless of `CanInteract`**. This split is exactly why `CanInteract=false` on the base yields "UI shows, no prompt."
- `IInteractable` (`Assets/_Game/Scripts/World/IInteractable.cs`): `string InteractPrompt`, `string NameTag`, `bool CanInteract`, `void Interact()`.
- Detection climbs `GetComponentInParent<IInteractable>()` from the Layer-8 collider child up to the root component — so `EntityPresence` lives on the root, the trigger collider on a Layer-8 child.
- `_persistentID.Entity` returns the `Entity` SO; `Entity.entityName` is the display name (base field, not NPC-specific). `NPCPresence` only used the `NPCEntity` cast for `entityName` — so the base `Entity` suffices and **the `NPCEntity` cast can be dropped entirely**.
- Gating deps are optional and polled, never subscribed: `EntityHealth` (`IsDead`) and `ICombatStateProvider` (`IsInCombat`) via `GetComponent` — null-safe (`Game.AI/CLAUDE.md`: PersistentID/health/brain all optional, guard every access).
- `EntityUI` is already on `Entity_base.prefab` and self-subscribes to `EntityHealth.HealthChanged`; the name-tag scan calls `entityUI.SetName(candidate.NameTag)` + `Show(true)`.

### Prefab / inheritance gotcha (critical)

Because the spider is a bare `Entity_base` variant with no added components, `EntityPresence`
**must** be a concrete component on the base prefab so the spider inherits it. But
`NPCPresence : EntityPresence` is a *different* MonoBehaviour — a variant cannot re-type the
inherited `EntityPresence` component into `NPCPresence`. So `NPC_base Variant` must:
1. **Remove** the inherited `EntityPresence` component (`m_RemovedComponents`), and
2. **Keep** its added `NPCPresence` component (GUID `6aadb170…`),
so the NPC root ends up with exactly **one** `IInteractable`. Likewise the base now owns the
`InteractionCollider` child, so the NPC variant's added `InteractionCollider`
(GameObject fileID `5512345678901234561`, transform `…562`, collider `…563`) must be removed
from `m_AddedGameObjects` to avoid a duplicate collider.

> Direct `.prefab` YAML edits: after editing, refresh Unity with `if_dirty`, **never** `force`
> (force reimports cached in-memory state and discards disk edits — root CLAUDE.md).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Source of the plumbing to extract; becomes the subclass. GUID `6aadb17071c6eeb488175719a41fdb14`. |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Interface the base implements. |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Consumer; confirms the two-scan / `CanInteract` behavior. |
| `Assets/_Game/ScriptableObjects/Entities/Entity.cs` | Base `Entity.entityName` (no NPC cast needed). |
| `Assets/_Game/ScriptableObjects/Entities/NPC/NPCEntity.cs` | NPC SO (cast was unnecessary). |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | GUID `e265604e8dcaaad4c81236c415d32664`. Gets `EntityPresence` + Layer-8 `InteractionCollider`. Has `EntityUI`. |
| `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab` | Variant of Entity_base; remove inherited EntityPresence + duplicate collider. |
| `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab` | Variant of Entity_base; inherits new plumbing. |
| `Assets/_Game/Scripts/World/CLAUDE.md`, `Scripts/AI/CLAUDE.md`, `Prefabs/CLAUDE.md` | Docs to update. |

### Technical Decisions

- **Architecture:** Split — base `EntityPresence` + `NPCPresence : EntityPresence` (user choice).
- **Monster behavior now:** name + HP bar on hover, **no** `[E]` prompt, `Interact()` no-op → base `CanInteract` returns `false`, base `InteractPrompt` is `""`. NPC subclass overrides `CanInteract` (alive & not in combat) and `InteractPrompt` ("Talk").
- **GUID stability:** `NPCPresence.cs` keeps its existing GUID (`6aadb17071c6eeb488175719a41fdb14`) so the NPC variant reference stays valid; `EntityPresence.cs` is a new file/GUID added to the base prefab.
- `EntityHealth`/`ICombatStateProvider`/`PersistentID` accesses stay null-guarded.
- **`NameTag` null-guard (new):** base `NameTag` must guard `_persistentID`/`Entity` because the name-tag scan can reach a disabled component (`GetComponentInParent` ignores `enabled`). Return `""` when unresolved.
- **Drop the `NPCEntity` cast:** `entityName` is on base `Entity`; `NPCPresence.Interact()` only used the cast for `npcName` → use base `Data.entityName`. No `NPCEntity` dependency remains in presence code.
- **`EntityPresence` namespace:** `Game.World` (it implements `Game.World.IInteractable` and is the world-interaction surface). `NPCPresence` stays in `Game.AI` and `using Game.World`.

### Investigation Findings (Step 2 — confirmed ground truth)

- **Layers** (`ProjectSettings/TagManager.asset`): `Characters` = 6 (root), `CharacterHitbox` = 7 (combat hitbox), `Interactable` = 8 (interaction trigger). `InteractionSystem._raycastMask` targets Layer 8.
- **`Entity_base.prefab`** (guid `e265604e8dcaaad4c81236c415d32664`) root = Layer 6, fileIDs:
  - GameObject root `8283577674775524483`, Transform `1674852574886742708`
  - `PersistentID` `1186952831256218892` (the `_persistentID` to wire into `EntityPresence`)
  - `EntityHealth` `-6003750127742918704` (disabled in base, enabled by variants)
  - `EntityBrain` `6892060459904109189`
  - `EntityUI` `2292172924411989332` (already present — name + HP bar)
  - No `IInteractable`, **no Layer-8 collider today** (root has `EntityUICanvas` + `Visual` children only).
- **`NPC_base Variant.prefab`** (guid `ea73572b6a4e79d4fbe41fdea8c1e693`, variant of Entity_base) adds, via its single `PrefabInstance` (`7816647421093773197`):
  - `m_AddedComponents`: `NPCPresence` (`2441576283753789042`, script guid `6aadb170…`), `NPCMemoryComponent`, `NPCDialogueGraphComponent`, `InventorySystem`, `GoldSystem`, `HumanoidAnimationBridge`, `HumanoidAIAnimationDriver`.
  - `m_AddedGameObjects`: an `InteractionCollider` child — GameObject `5512345678901234561` (Layer 8), Transform `5512345678901234562`, trigger `CapsuleCollider` `5512345678901234563` (Radius 0.5 / Height 2.0 / Center Y 1.0). **This becomes the duplicate to remove** once the base owns the collider.
  - NPCPresence wiring today: `_persistentID: {fileID: 8935889491633091201}` (stripped base PersistentID), `_onDialogueRequested: {guid: dea29e34f28379e4f9b312d51a1ec9e6}`.
- **`Monster_DarknessSpider Variant.prefab`** (guid `0486cf4048d340b468ff97f8a6b3a40f`, variant of Entity_base): assigns `entityType` (guid `b358889657b0cd546a2c43187cb37b41`), enables `EntityHealth`/`EntityBrain`/`NavMeshAgent`, removes base `Visual`, adds a spider mesh child. No added presence/collider today → will inherit `EntityPresence` + the new collider automatically.
- **Scene impact:** both variants are instanced in `Assets/_Game/Scenes/StartingTown.unity` with empty `m_AddedComponents`/`m_AddedGameObjects` — base-prefab additions propagate without scene edits and there are no conflicting scene overrides.
- **No code depends on the `NPCPresence` type** — only doc comments in `ICombatStateProvider.cs` and `NPCMemoryComponent.cs`. No call sites to update.
- **`PersistentID.Entity`** returns `entityType` (base `Entity`); `Start()` may `SetActive(false)` if killed — orthogonal to presence.
- **`EntityUI`**: `Awake` caches `EntityHealth` (works while disabled), activates `_healthBarRoot` when health present; `SetName(NameTag)` + `Show(true)` driven by the name-tag scan. No change needed.
- **Collider sizing caveat:** the base capsule (Height 2 / Center Y 1) is humanoid-sized; on the short spider it will sit tall/oversized but still overlaps the spherecast, so detection works. Tightening the spider's interaction collider is a cosmetic follow-up (variant override), not required for this spec.

## Implementation Plan

> **Order:** code first (so the script GUID exists), then base prefab, then variant dedup, then verify, then docs.
> **Prefab method:** structural changes (new component, new child GO) are safest via the Unity Editor / Unity MCP (`manage_gameobject` / `manage_components`) so Unity assigns the script GUID + fileIDs. The YAML below is the **authoritative end-state** if hand-editing. After any raw `.prefab` YAML edit, refresh Unity with `if_dirty` — **never** `force` (root CLAUDE.md).

### Tasks

- [x] **Task 1: Create the base `EntityPresence` component**
  - File: `Assets/_Game/Scripts/World/EntityPresence.cs` (NEW)
  - Action: New `public class EntityPresence : MonoBehaviour, IInteractable` in namespace `Game.World`. Implement:
    ```csharp
    using _Game.ScriptableObjects.Entities;
    using Game.AI;
    using Game.Core;
    using UnityEngine;

    namespace Game.World
    {
        /// <summary>
        /// Base interactable surface for ANY entity (NPC, monster, …). Provides the generic
        /// IInteractable plumbing — name tag, alive/out-of-combat gating helper, and a no-op
        /// Interact. Lives on Entity_base.prefab so every entity is discoverable by
        /// InteractionSystem and shows its world-space EntityUI on hover. Subclasses
        /// (e.g. NPCPresence) override Interact()/InteractPrompt/CanInteract to add behaviour
        /// (dialogue now; loot-corpse in a future spec).
        /// </summary>
        public class EntityPresence : MonoBehaviour, IInteractable
        {
            private const string TAG = "[Entity]";

            [SerializeField] protected PersistentID _persistentID;

            protected Entity Data => _persistentID != null ? _persistentID.Entity : null;

            protected EntityHealth _entityHealth;
            protected ICombatStateProvider _combatState;

            // Base entities offer no prompt/interaction yet (loot is a future spec).
            public virtual string InteractPrompt => string.Empty;

            // Null-guarded: the name-tag scan can reach this via GetComponentInParent even when
            // the component is disabled (GetComponentInParent ignores `enabled`).
            public string NameTag => Data != null ? Data.entityName : string.Empty;

            // Base: nothing to interact with yet → no crosshair prompt. EntityUI (name + HP bar)
            // still shows because the name-tag scan does not check CanInteract.
            public virtual bool CanInteract => false;

            protected virtual void Awake()
            {
                if (Data == null)
                {
                    GameLog.Error(TAG, $"EntityPresence on {gameObject.name} has no Entity assigned (PersistentID.Entity is null)");
                    enabled = false;
                    return;
                }
                _entityHealth = GetComponent<EntityHealth>();
                _combatState  = GetComponent<ICombatStateProvider>(); // null-safe: entities without a brain are never in combat
            }

            // Default: no-op. The corpse intentionally remains a valid IInteractable target so the
            // future loot-corpse story can plug in without re-introducing detection logic.
            public virtual void Interact() { }

            /// <summary>True when this entity is alive and not currently in combat.</summary>
            protected bool IsAliveAndOutOfCombat =>
                (_entityHealth == null || !_entityHealth.IsDead) &&
                (_combatState  == null || !_combatState.IsInCombat);
        }
    }
    ```
  - Notes: `EntityHealth` + `ICombatStateProvider` live in `Game.AI` (the `using Game.AI;` covers them); `PersistentID` is same-namespace `Game.World`; `Entity` from `_Game.ScriptableObjects.Entities`. Let Unity generate the `.meta`; capture its guid as `<ENTITYPRESENCE_GUID>` for Task 3. If hand-creating the `.meta`, include the `MonoImporter` block (CLAUDE.md MEDIUM — avoids guid regeneration).

- [x] **Task 2: Refactor `NPCPresence` into a subclass**
  - File: `Assets/_Game/Scripts/AI/NPCPresence.cs`
  - Action: Change to `public class NPCPresence : EntityPresence`. Delete the now-inherited members (`_persistentID`, `_data`/`NPCEntity` cast, `_entityHealth`, `_combatState`, `NameTag`, the duplicate `Awake` body) and keep only NPC-specific overrides:
    ```csharp
    using Game.Core;
    using Game.Economy;
    using Game.Inventory;
    using Game.NPC;
    using Game.World;
    using UnityEngine;

    namespace Game.AI
    {
        public class NPCPresence : EntityPresence
        {
            private const string TAG = "[NPC]";

            [SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

            public override string InteractPrompt => "Talk";

            // NPCs are interactable while alive and not in combat (dead / in-combat → no prompt).
            public override bool CanInteract => IsAliveAndOutOfCombat;

            public override void Interact()
            {
                if (Data == null) return;
                if (_entityHealth != null && _entityHealth.IsDead)
                {
                    // TODO: replace with a loot-corpse interaction unlock once the looting story lands.
                    GameLog.Info(TAG, $"{gameObject.name} is dead — dialogue interaction blocked");
                    return;
                }
                if (_combatState != null && _combatState.IsInCombat)
                {
                    GameLog.Info(TAG, $"{gameObject.name} is in combat — dialogue interaction blocked");
                    return;
                }
                if (_onDialogueRequested == null)
                {
                    GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
                    return;
                }
                var memComponent   = GetComponent<NPCMemoryComponent>();
                var graphComponent = GetComponent<NPCDialogueGraphComponent>();
                var invComponent   = GetComponent<InventorySystem>();
                var goldComponent  = GetComponent<GoldSystem>();

                _onDialogueRequested.Raise(new NPCDialogueRequestData
                {
                    npcName       = Data.entityName,
                    memories      = memComponent,
                    graph         = graphComponent,
                    npcInventory  = invComponent,
                    npcGoldSystem = goldComponent
                });
            }
        }
    }
    ```
  - Notes: **Keep the existing script GUID `6aadb17071c6eeb488175719a41fdb14`** (do not delete/recreate the file or its `.meta`). The inherited `_persistentID` is serialized by field name, so the NPC variant's existing `_persistentID: {fileID: 8935889491633091201}` value stays valid — no prefab field re-wire needed. `_onDialogueRequested` remains a serialized field on the subclass (its existing YAML value is preserved). The `NPCEntity` cast is gone — `entityName` comes from base `Entity`.

- [x] **Task 3: Add `EntityPresence` + Layer-8 `InteractionCollider` to `Entity_base.prefab`**
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab`
  - Action A — add the `EntityPresence` MonoBehaviour to the root GameObject (`8283577674775524483`), wired to the base `PersistentID` (`1186952831256218892`):
    ```yaml
    --- !u!114 &5512345678901230004
    MonoBehaviour:
      m_GameObject: {fileID: 8283577674775524483}
      m_Enabled: 1
      m_Script: {fileID: 11500000, guid: <ENTITYPRESENCE_GUID>, type: 3}
      m_EditorClassIdentifier: Game::Game.World.EntityPresence
      _persistentID: {fileID: 1186952831256218892}
    ```
    …and append `- component: {fileID: 5512345678901230004}` to the root GameObject's `m_Component` list.
  - Action B — add a Layer-8 `InteractionCollider` child (trigger capsule, matching the NPC's existing dims):
    ```yaml
    --- !u!1 &5512345678901230001
    GameObject:
      serializedVersion: 6
      m_Component:
      - component: {fileID: 5512345678901230002}
      - component: {fileID: 5512345678901230003}
      m_Layer: 8
      m_Name: InteractionCollider
      m_IsActive: 1
    --- !u!4 &5512345678901230002
    Transform:
      m_GameObject: {fileID: 5512345678901230001}
      m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
      m_LocalPosition: {x: 0, y: 0, z: 0}
      m_LocalScale: {x: 1, y: 1, z: 1}
      m_Children: []
      m_Father: {fileID: 1674852574886742708}
    --- !u!136 &5512345678901230003
    CapsuleCollider:
      m_GameObject: {fileID: 5512345678901230001}
      m_IsTrigger: 1
      m_Enabled: 1
      serializedVersion: 2
      m_Radius: 0.5
      m_Height: 2
      m_Direction: 1
      m_Center: {x: 0, y: 1, z: 0}
    ```
    …and add `- {fileID: 5512345678901230002}` to the root Transform's (`1674852574886742708`) `m_Children` list.
  - Notes: Root stays Layer 6 (Characters) for LockOn; only the child is Layer 8 (Interactable). `InteractionSystem` raycasts Layer 8 then `GetComponentInParent<IInteractable>()` climbs to `EntityPresence` on the root. Fields not shown (`m_ObjectHideFlags`, `m_CorrespondingSourceObject: {fileID: 0}`, `m_PrefabInstance: {fileID: 0}`, etc.) must match the file's standard component preamble — copying an existing block and editing values avoids omissions. Prefer doing this via Editor/MCP to get real fileIDs/guid, then verify the end-state matches.

- [x] **Task 4: Dedup on `NPC_base Variant.prefab`**
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`
  - Action: In the variant's `PrefabInstance` (`7816647421093773197`):
    - Add to `m_RemovedComponents` the inherited base `EntityPresence`:
      `- {fileID: 5512345678901230004, guid: e265604e8dcaaad4c81236c415d32664, type: 3}`
    - Add to `m_RemovedGameObjects` the inherited base `InteractionCollider`:
      `- {fileID: 5512345678901230001, guid: e265604e8dcaaad4c81236c415d32664, type: 3}`
  - Result: the NPC root keeps exactly **one** `IInteractable` (its added `NPCPresence`) and **one** `InteractionCollider` (its own added child `5512345678901234561`, which is pinned to the hips via `HumanoidAIAnimationDriver._transformsToPinToHips`).
  - Notes: **Do NOT delete the NPC's own added `InteractionCollider` (`5512345678901234561` / transform `…562` / collider `…563`)** — `HumanoidAIAnimationDriver._transformsToPinToHips` references transform `5512345678901234562`; removing it breaks death-ragdoll pinning. We remove only the *inherited base* collider/component.

- [x] **Task 5: Verify monster + scene propagation (no edits expected)**
  - Files: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`, `Assets/_Game/Scenes/StartingTown.unity`
  - Action: Open the spider variant and confirm it now inherits `EntityPresence` (root) + `InteractionCollider` (Layer-8 child) from the base, with `entityType` (guid `b358889657b0cd546a2c43187cb37b41`) and `EntityHealth` enabled. Enter Play in `StartingTown` and confirm both the inherited NPC and spider instances behave per the ACs. No YAML edits expected (empty `m_AddedComponents`/`m_AddedGameObjects` on the scene instances) — flag any auto-added override.

- [x] **Task 6: Update folder CLAUDE.md docs**
  - Files: `Assets/_Game/Scripts/World/CLAUDE.md`, `Assets/_Game/Scripts/AI/CLAUDE.md`, `Assets/_Game/Prefabs/CLAUDE.md`
  - Action:
    - `Scripts/World/CLAUDE.md`: add `EntityPresence` to the "What's here" table (base `IInteractable` on `Entity_base`; `NameTag`, gated `CanInteract`, no-op `Interact`); note `NPCPresence : EntityPresence`.
    - `Scripts/AI/CLAUDE.md`: update the `NPCPresence` row to "`: EntityPresence` (Game.World) — overrides dialogue `Interact()`; gating inherited".
    - `Prefabs/CLAUDE.md`: update the layer table — interactable surface now lives on `Entity_base` (every entity is interactable/UI-visible); record the variant gotcha (base concrete component + subclass variant ⇒ variant must `m_RemovedComponents` the inherited base component; move shared child collider to base and `m_RemovedGameObjects` the variant duplicate where a subclass needs its own).

### Acceptance Criteria

- [ ] **AC1 (compile):** Given the refactor, when Unity recompiles, then there are no compile errors, `NPCPresence : EntityPresence` resolves, and no `NPCEntity` cast remains in presence code.
- [ ] **AC2 (NPC dialogue preserved):** Given a living, out-of-combat NPC in `StartingTown`, when the player aims at it within interaction range, then the `[E] Talk` prompt shows and pressing Interact raises `_onDialogueRequested` (dialogue opens) — identical to pre-refactor behavior.
- [ ] **AC3 (NPC gating):** Given an NPC that is dead OR in combat, when aimed at, then no `[E]` prompt appears and `Interact()` does not raise the dialogue event (logs the blocked reason).
- [ ] **AC4 (monster UI):** Given the spider in `StartingTown`, when the player aims within name range, then its world-space name + health bar (`EntityUI`) appear on hover.
- [ ] **AC5 (monster has no interaction yet):** Given a living or dead spider, when aimed at within interaction range, then NO `[E]` prompt appears and pressing Interact does nothing (`CanInteract == false`, `Interact()` is a no-op).
- [ ] **AC6 (detection plumbing):** Given the spider, when the interaction raycast hits its Layer-8 `InteractionCollider` child (inherited from `Entity_base`), then `GetComponentInParent<IInteractable>()` resolves to `EntityPresence` on the root.
- [ ] **AC7 (NPC single surface):** Given the `NPC_base Variant`, when inspected in Prefab Mode, then it has exactly one `IInteractable` (`NPCPresence`) and exactly one `InteractionCollider` (its own, hip-pinned); the inherited base `EntityPresence` and base `InteractionCollider` are removed.
- [ ] **AC8 (NameTag null-safety):** Given an `EntityPresence` whose `PersistentID.Entity` is null, when `NameTag` is read by the name-tag scan, then it returns `""` (no `NullReferenceException`) and `Awake` logs an error and disables the component.
- [ ] **AC9 (death/persistence intact):** Given a killed NPC, when it ragdolls, then its `InteractionCollider` still pins to the hips and `PersistentID`/death behavior is unchanged versus pre-refactor.

## Additional Context

### Dependencies

- **No new external libraries.** Builds on existing systems: `IInteractable`, `InteractionSystem`, `EntityUI`, `PersistentID`, `EntityHealth`, `ICombatStateProvider`, `GameEventSO_NPCDialogueRequest`.
- **Blocks / enables:** the future **loot-corpse spec** depends on this — it will subclass/extend `EntityPresence` (or override on a corpse component) to make a dead entity interactable (`CanInteract` true when dead) and implement `Interact()` to open loot. No other work is gated.

### Testing Strategy

- **Primary — manual in-Editor (`StartingTown`):** verify AC2–AC7, AC9 by play-testing prompts, dialogue, the spider's name/HP bar, and the absence of a spider interact prompt; kill an NPC and confirm ragdoll + collider pinning.
- **Optional — EditMode unit test** (`Assets/Tests/EditMode/EntityPresenceTests.cs`, class `EntityPresenceTests`): assert the polymorphic contract without scene deps — `EntityPresence` defaults (`CanInteract == false`, `InteractPrompt == ""`, `NameTag == ""` when no `PersistentID`) and `NPCPresence` overrides (`InteractPrompt == "Talk"`). Keep light: these are `MonoBehaviour`s; instantiate via `new GameObject(...).AddComponent<T>()` and read properties directly (avoid relying on `Awake` ordering).
- **Compile gate (AC1):** confirm a clean compile (Editor console / `refresh_unity` if_dirty) after Tasks 1–2.

### Notes

- **GUID/meta risk:** when creating `EntityPresence.cs`, let Unity generate the `.meta`; if hand-authoring it, include the `MonoImporter` block (CLAUDE.md MEDIUM) so the guid isn't regenerated on reimport, which would break the prefab reference added in Task 3.
- **Raw YAML refresh rule:** after hand-editing any `.prefab`, `refresh_unity(mode="if_dirty")` — `force` discards disk edits (root CLAUDE.md).
- **Do not delete the NPC's own `InteractionCollider`** (Task 4 note) — it's hip-pinned for ragdoll; only the inherited base copy is removed.
- **Inherited serialized field:** moving `_persistentID` into the base keeps the NPC variant's value valid (Unity serializes inherited fields by name). Verify no "missing field"/reset on first reimport.
- **Two-IInteractable hazard:** if Task 4's `m_RemovedComponents` is skipped, the NPC root carries both `EntityPresence` (inherited, `CanInteract=false`) and `NPCPresence` — the scan may treat them as separate candidates. Removing the inherited base component is required, not optional.
- **Spider collider sizing (cosmetic):** the inherited capsule (Height 2 / Center Y 1) is humanoid-sized and sits tall on the short spider; detection still works. Tightening it is a future variant-override polish, out of scope.
- **Future (loot spec):** a dead entity is currently NOT interactable (`CanInteract=false`); the loot spec must flip this for corpses and implement the real `Interact()`. This spec deliberately leaves the corpse a discoverable-but-inert `IInteractable` target so loot plugs in without touching detection.
