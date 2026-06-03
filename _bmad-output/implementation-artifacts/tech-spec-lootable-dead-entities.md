---
title: 'Lootable dead entities (loot corpse via container system)'
slug: 'lootable-dead-entities'
created: '2026-06-03'
status: 'implementation-complete'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1 (URP 17)', 'C# / Game.asmdef', 'Unity Prefab Variants (raw YAML / MCP)', 'GameEventSO<T> event channels', 'Unity Editor / MCP (manage_components)']
files_to_modify:
  - 'Assets/_Game/Scripts/AI/EntityPresence.cs (add dead=lootable behavior; raise container-open event)'
  - 'Assets/_Game/Scripts/AI/NPC/NPCPresence.cs (dead NPC falls through to base loot; prompt/gating)'
  - 'Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerOpenRequest.cs (add takeOnly flag to data struct)'
  - 'Assets/_Game/Scripts/World/ContainerInteractable.cs (set takeOnly=false explicitly)'
  - 'Assets/_Game/Scripts/World/ContainerSystem.cs (pass takeOnly through to ContainerUI.Open)'
  - 'Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs (take-only mode: suppress Put paths)'
  - 'Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs (respect take-only / hide Put button)'
  - 'Assets/_Game/Prefabs/Entities/Entity_base.prefab (wire EntityPresence _onLootRequested → OnContainerOpenRequested)'
  - 'Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab (wire NPCPresence _onLootRequested)'
  - 'Assets/_Game/Scripts/AI/CLAUDE.md (doc)'
  - 'Assets/_Game/Scripts/World/CLAUDE.md + UI/Inventory/CLAUDE.md (doc)'
code_patterns:
  - 'Single IInteractable per entity root — loot folded into EntityPresence; no new component/surface'
  - 'Reuse GameEventSO_ContainerOpenRequest channel (asset OnContainerOpenRequested, guid 27f639c0…) — corpse is just another raiser; ContainerSystem listener unchanged'
  - 'Inherited [SerializeField] protected/private field serializes on subclass component by name (EntityPresence._onLootRequested must be wired on BOTH Entity_base.EntityPresence and NPC variant.NPCPresence)'
  - 'GameLog wrapper only; TAG const; _camelCase [SerializeField] private; one MonoBehaviour per file; shallow (1-level) inheritance only'
  - 'No event Action / direct cross-system calls — typed GameEventSO<T> channels only (project-context.md)'
test_patterns:
  - 'Primary: manual in-Editor (StartingTown) — kill NPC & spider, loot corpse'
  - 'Optional EditMode: Assets/Tests/EditMode/*Tests.cs, NUnit [Test], class {System}Tests, Tests.EditMode.asmdef refs Game'
---

# Tech-Spec: Lootable dead entities (loot corpse via container system)

**Created:** 2026-06-03

## Overview

### Problem Statement

The prior two specs (`tech-spec-entitypresence-base-interactable.md`,
`tech-spec-inventorysystem-to-entity-base.md`) put an `IInteractable` (`EntityPresence`)
**and** an `InventorySystem` on every entity (`Entity_base.prefab`), explicitly as
groundwork for looting. Today a dead entity is a discoverable-but-inert `IInteractable`:
`EntityPresence.CanInteract => false` and `Interact()` is a no-op, and `NPCPresence`
actively *blocks* interaction when dead. So killing a monster or NPC leaves a corpse the
player cannot loot, even though the body already carries an `InventorySystem`.

We want every dead entity (monster **or** NPC) to become **Lootable**: interacting with
the corpse opens a loot UI from which the player freely takes items (no money), reusing
the existing free-inventory-interaction system that powers world containers
(`ContainerInteractable` → `ContainerSystem` → `ContainerUI`).

### Solution

Reuse the container pipeline end-to-end. A dead entity's `EntityPresence` raises the
**same** `GameEventSO_ContainerOpenRequest` channel that `ContainerInteractable` uses,
passing the entity's own `InventorySystem` as the container inventory. The existing
player-side `ContainerSystem` listener opens the existing `ContainerUI` (the two-pane
take/put screen) unchanged — no new UI, no new event channel, no second `IInteractable`.

Because the architecture enforces exactly **one** `IInteractable` per entity root, the
loot behavior is folded into the existing surface:

- **`EntityPresence` (base):** when the entity is **dead** → `CanInteract = true`,
  `InteractPrompt = "Loot"`, and `Interact()` raises the container-open event with the
  entity's `InventorySystem` and a new `takeOnly = true` flag. When alive → unchanged
  (`CanInteract = false`, no prompt). **Lootable only when dead AND the inventory holds
  items** (revised 2026-06-03) — an empty corpse is inert (`CanInteract => IsDead && HasLoot`).
- **`NPCPresence : EntityPresence`:** alive & out-of-combat → dialogue (unchanged);
  **dead → falls through to base loot** (`base.Interact()`); in-combat → no interaction.

"Take-only" loot semantics (player can take from the corpse but not deposit into it)
are added to `ContainerUI` via a `takeOnly` flag carried on `ContainerOpenRequestData`.
Containers pass `takeOnly = false` (full take/put as today); corpses pass `true`.

### Scope

**In Scope:**
- `EntityPresence.cs`: dead-entity loot behavior — `IsDead` helper, `CanInteract`/`InteractPrompt` reflect dead state, cached `InventorySystem`, new `[SerializeField] GameEventSO_ContainerOpenRequest` (the loot/container channel), `Interact()` raises the event with `takeOnly = true` when dead.
- `NPCPresence.cs`: dead NPC routes to `base.Interact()` (loot) instead of blocking; `CanInteract` true when dead OR alive-and-out-of-combat; `InteractPrompt` = "Loot" when dead, "Talk" otherwise.
- `ContainerOpenRequestData`: add `public bool takeOnly;` field.
- `ContainerInteractable.cs`: set `takeOnly = false` in the raised data.
- `ContainerSystem.cs`: forward `data.takeOnly` to `ContainerUI.Open(...)`.
- `ContainerUI.cs`: `Open(InventorySystem, bool takeOnly)`; in take-only mode suppress all Put paths (player-side double-click, player-side context-menu Put, detail-actions Put button) — corpse loot is take-only.
- `ContainerDetailActions.cs`: respect take-only (keep the Put button hidden on the player side when looting).
- `Entity_base.prefab`: wire the `EntityPresence` container-open event field to the existing container event asset (guid `27f639c03792674408432d16e8dd2a3c`) so every entity inherits it.
- Doc updates: `Scripts/AI/CLAUDE.md`, `Scripts/World/CLAUDE.md`, `Scripts/UI/Inventory/CLAUDE.md`.

**Out of Scope:**
- Authoring monster/NPC loot tables (`_startingItems`) — corpses loot whatever the entity already carries; populating drops is a separate data task. Monster inventories stay empty for now — and under the revised empty-corpse rule an empty monster corpse is **not** lootable (no prompt) until drops are authored.
- Persisting looted/inventory state across save & scene reload (consistent with prior specs; not yet implemented).
- Lock/skill-gating on corpses (corpses are never locked: `isLocked = false`, `requiredSkillId = null`).
- Any change to the NPC shop/trade flow (`ShopDialogueNode` → `NPCTradeUI`) — looting is the container UI, not the trade UI; the alive NPC trade path is untouched.
- Removing the corpse / "empty body" cleanup, loot-all button, or loot VFX/SFX.
- Gold looting from corpses (`GoldSystem` is NPC-only and not part of this spec).

## Context for Development

### Codebase Patterns

- **Single IInteractable per entity root (critical constraint):** `EntityPresence`
  (`Assets/_Game/Scripts/AI/EntityPresence.cs`, namespace `Game.World`) is the one
  `IInteractable` on `Entity_base.prefab`. `NPCPresence : EntityPresence`
  (`Scripts/AI/NPC/NPCPresence.cs`) replaces it on the NPC variant (the inherited base
  `EntityPresence` is `m_RemovedComponents`'d). So loot logic **must** live on this
  surface — do not add a second `IInteractable`.
- **Container open pipeline (the system to reuse):**
  - `ContainerInteractable.Interact()` raises `_onContainerOpenRequested`
    (`GameEventSO_ContainerOpenRequest`) with `ContainerOpenRequestData { containerInventory, isLocked, requiredSkillId }`.
  - `ContainerSystem` (player-side, listens in `OnEnable`) → `HandleContainerOpenRequested` → after optional lock check → `_containerUI.Open(data.containerInventory)`.
  - `ContainerUI.Open(inv)` shows two grids: `_containerContentRoot` (the passed inventory) and `_playerContentRoot` (`_playerInventory`). Take = corpse/container→player; Put = player→corpse/container.
  - The event asset is shared by all containers: guid `27f639c03792674408432d16e8dd2a3c`. `ContainerSystem` listens to that one asset, so any raiser of it (corpse included) is handled identically.
- **Every entity already has an `InventorySystem`** on the `Entity_base` root (empty `_startingItems` by default). Resolve via `GetComponent<InventorySystem>()`.
- **Dead state:** `EntityHealth.IsDead` (`Scripts/AI/EntityHealth.cs`). On death the body stays in the scene (ragdoll, no `SetActive(false)`), so the corpse remains a valid raycast target. `EntityHealth`/`ICombatStateProvider` are already cached in `EntityPresence.Awake()` (`_entityHealth`, `_combatState`).
- **InteractionSystem two-scan behavior:** scan-1 (prompt + `Interact()`) only considers candidates whose `CanInteract` is true; scan-2 (name/HP UI) ignores `CanInteract`. So flipping `CanInteract => IsDead` makes the `[E] Loot` prompt appear exactly when dead, while the name/HP UI behavior is unchanged.
- **GameLog only** (`TAG` const), `_camelCase [SerializeField] private`, one MonoBehaviour per file, no `event Action` across system boundaries (use `GameEventSO<T>` — exactly what the container channel is).

### Take-only (loot) — the Put paths in ContainerUI to suppress

`ContainerUI` exposes four ways to move an item player→container that must be disabled when `takeOnly`:
1. `OnSlotDoubleClicked(index, side)` — player side calls `PutItem(index)`.
2. `ShowContextMenu(...)` — player side shows a "PutButton".
3. `ContainerDetailActions.Bind(...)` — player side activates `_putButton`.
4. `PutItem(int index)` — the method itself (guard as a backstop).
Take paths (container side) stay fully functional. The player grid stays visible (the player sees their own inventory while looting) — only the deposit action is removed.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/AI/EntityPresence.cs` | Base IInteractable on Entity_base (namespace `Game.World`). Gains dead=lootable behavior + container-open event raise. Already caches `_entityHealth`/`_combatState`/`_persistentID`. |
| `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs` | `: EntityPresence`. Dead → `base.Interact()` (loot); alive → dialogue. GUID `6aadb17071c6eeb488175719a41fdb14`. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerOpenRequest.cs` | `ContainerOpenRequestData` struct — add `bool takeOnly`. |
| `Assets/_Game/Scripts/World/ContainerInteractable.cs` | Existing raiser; set `takeOnly = false`. Container event asset guid `27f639c03792674408432d16e8dd2a3c`. |
| `Assets/_Game/Scripts/World/ContainerSystem.cs` | Player-side listener; forward `takeOnly` to `ContainerUI.Open`. |
| `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs` | Two-pane loot/container UI; add take-only mode. `IScreenPanel`. |
| `Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs` | Detail-panel Take/Put buttons; respect take-only. |
| `Assets/_Game/Scripts/AI/EntityHealth.cs` | `IsDead` source of truth. |
| `Assets/_Game/Prefabs/Entities/Entity_base.prefab` | guid `e265604e8dcaaad4c81236c415d32664`; wire EntityPresence's container-open event field. |
| `Assets/_Game/Prefabs/World/Containers/Base_Container.prefab` | Reference for how `ContainerInteractable` wires the event asset (`27f639c0...`). |

### Technical Decisions (from discovery)

- **Loot logic placement:** folded into `EntityPresence` base (user choice) — every entity is lootable when dead with zero prefab restructuring; `NPCPresence` overrides for the alive-dialogue case and delegates to `base.Interact()` when dead. No second `IInteractable`.
- **Empty corpse:** ~~always lootable when dead~~ **REVISED 2026-06-03 (user choice): an empty corpse is inert** — `CanInteract => IsLootable` where `IsLootable = IsDead && HasLoot` (`HasLoot = InventorySystem.Count > 0`). A corpse with no items (or whose last item was just looted) shows no `[E]` prompt and `Interact()` is a no-op. _Original decision (open an empty loot UI) is superseded._
- **Loot direction:** take-only (user choice) — corpses pass `takeOnly = true`; containers pass `false`. Implemented via a `takeOnly` flag on `ContainerOpenRequestData` threaded ContainerInteractable/EntityPresence → ContainerSystem → ContainerUI.
- **Event channel:** reuse the existing container event asset (guid `27f639c03792674408432d16e8dd2a3c`) — `ContainerSystem` already listens to it; the corpse becomes just another raiser. Wire it onto `EntityPresence` on the base prefab so all entities inherit the reference. (Alternative — a separate loot event + second listener — rejected: more wiring, no benefit.)
- **No lock on corpses:** `isLocked = false`, `requiredSkillId = null` — `ContainerSystem` opens directly.
- **Combat gating preserved:** an alive NPC in combat still cannot be interacted with; a dead entity is never `IsInCombat`, so loot works post-death.

### Investigation Findings (Step 2 — confirmed ground truth)

- **Container event channel = `OnContainerOpenRequested.asset`** (guid `27f639c03792674408432d16e8dd2a3c`, type `GameEventSO_ContainerOpenRequest`). Referenced by exactly: `Base_Container.prefab` (raiser, via `ContainerInteractable._onContainerOpenRequested`), `Player.prefab` (**listener** — `ContainerSystem._onContainerOpenRequested: {fileID: 11400000, guid: 27f639c0…, type: 2}`, with `_containerUI: {fileID: 576199237022889503}`), and `ContainerUI.prefab`. ⇒ Raising this same asset from a corpse's `EntityPresence` is handled by the existing `ContainerSystem` with **zero** new wiring on the player side.
- **`Entity_base.prefab`** (guid `e265604e8dcaaad4c81236c415d32664`), root GO `8283577674775524483`:
  - `EntityPresence` MonoBehaviour `&6326516008175686655` (script guid `bba749cfcaae58a4c9635e66f299f9dd`) — currently serializes **only** `_persistentID: {fileID: 1186952831256218892}`. The new `_onLootRequested` field is appended here, wired to `{fileID: 11400000, guid: 27f639c03792674408432d16e8dd2a3c, type: 2}`.
  - `InventorySystem` MonoBehaviour `&7887146153611111599` (guid `6bb7168ca149809449a28fc126185f40`, `_startingItems: []`) — the corpse inventory `EntityPresence` passes to the event. Resolve in code via `GetComponent<InventorySystem>()`.
  - `EntityHealth` `&-6003750127742918704` (disabled in base, enabled by variants) — `IsDead` source; already cached as `_entityHealth` in `EntityPresence.Awake()`.
- **`NPC_base Variant.prefab`** (guid `ea73572b6a4e79d4fbe41fdea8c1e693`): the inherited base `EntityPresence` is `m_RemovedComponents`'d; the variant's own **`NPCPresence`** `&2441576283753789042` (script guid `6aadb17071c6eeb488175719a41fdb14`) is the live `IInteractable`. It currently serializes `_persistentID: {fileID: 8935889491633091201}` and `_onDialogueRequested: {guid: dea29e34f28379e4f9b312d51a1ec9e6}`. Because `_onLootRequested` is declared on the **base** `EntityPresence`, it serializes on this subclass component by field name and **must be wired here too** → `{fileID: 11400000, guid: 27f639c0…, type: 2}`. (Monsters inherit the Entity_base wiring automatically; the NPC variant does not, since it removed the inherited base component.)
- **`EntityPresence.cs`** already caches `_entityHealth` (`EntityHealth`) and `_combatState` (`ICombatStateProvider`) in `Awake()`, and exposes `protected bool IsAliveAndOutOfCombat`. Add a `protected bool IsDead => _entityHealth != null && _entityHealth.IsDead;` and a cached `InventorySystem`.
- **`NPCPresence.cs`** today: `CanInteract => IsAliveAndOutOfCombat`; `Interact()` early-returns (logs) when dead/in-combat before raising dialogue. The dead branch must change to delegate to `base.Interact()` (loot); `CanInteract` must also return true when dead.
- **Take-only Put paths in `ContainerUI`** to suppress when `takeOnly`: (1) `OnSlotDoubleClicked` player side → `PutItem`; (2) `ShowContextMenu` player side → "PutButton"; (3) `ContainerDetailActions.Bind` player side → `_putButton`; (4) `PutItem(int)` method (backstop guard). Container/take paths unchanged. `ContainerDetailActions` (`_takeButton`/`_putButton`) needs a take-only signal — add a `bool takeOnly` parameter to `Bind` (passed by `ContainerUI.UpdateDetailPanel`).
- **project-context.md confirms:** `[SerializeField] private` preferred; **typed `GameEventSO<T>` channels ONLY** for cross-system comms (never `event Action`, never direct script refs) — reusing the container channel is the sanctioned pattern; **avoid deep inheritance** — this spec adds **no** new inheritance depth (EntityPresence→NPCPresence stays 1 level).
- **Scene impact (`StartingTown.unity`):** entity instances carry no presence overrides; base-prefab `_onLootRequested` wiring propagates to monster instances. The shopkeeper NPC instance uses the NPC variant's `NPCPresence`, which is wired at the prefab level — no scene edit expected (verify no auto-added override).

## Implementation Plan

> **Order:** lowest-level first — data struct → existing container raiser/UI → player listener → entity presence code → prefab wiring → verify → docs. Code compiles cleanly at each step (signatures updated callee-before-caller).
> **Prefab method:** wire `_onLootRequested` via the Unity Editor / MCP (`manage_components set_property`) so Unity writes the object reference; the YAML below is the authoritative end-state. After any raw `.prefab` YAML edit, `refresh_unity(mode="if_dirty")` — **never** `force` (root CLAUDE.md).

### Tasks

- [x] **Task 1: Add `takeOnly` to `ContainerOpenRequestData`**
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerOpenRequest.cs`
  - Action: Add a `public bool takeOnly;` field to the struct (after `requiredSkillId`):
    ```csharp
    [System.Serializable]
    public struct ContainerOpenRequestData
    {
        public InventorySystem containerInventory;
        public bool isLocked;
        public string requiredSkillId;
        public bool takeOnly; // true for corpse loot (no deposit); false for world containers
    }
    ```
  - Notes: `bool` defaults to `false`, so any existing raiser that doesn't set it keeps full take/put behavior. No other change to this file.

- [x] **Task 2: Set `takeOnly = false` on the container raiser**
  - File: `Assets/_Game/Scripts/World/ContainerInteractable.cs`
  - Action: In `Interact()`, add `takeOnly = false` to the `ContainerOpenRequestData` initializer:
    ```csharp
    _onContainerOpenRequested.Raise(new ContainerOpenRequestData
    {
        containerInventory = _inventory,
        isLocked = isLocked,
        requiredSkillId = isLocked ? _lockable.RequiredSkillId : null,
        takeOnly = false
    });
    ```
  - Notes: Explicit for clarity; behavior unchanged (containers stay full take/put).

- [x] **Task 3: Respect take-only in `ContainerDetailActions.Bind`**
  - File: `Assets/_Game/Scripts/UI/Inventory/ContainerDetailActions.cs`
  - Action: Add a `bool takeOnly` parameter and, on the player side, keep the Put (and Take) buttons hidden when `takeOnly`:
    ```csharp
    public void Bind(ContainerUI owner, int slotIndex, ItemSO item, ContainerSide side, bool takeOnly)
    {
        if (item == null || owner == null) { GameLog.Warn(TAG, "Bind: item or owner is null"); return; }

        if (_takeButton != null) _takeButton.onClick.RemoveAllListeners();
        if (_putButton != null) _putButton.onClick.RemoveAllListeners();

        if (side == ContainerSide.Container)
        {
            if (_takeButton != null)
            {
                _takeButton.gameObject.SetActive(true);
                _takeButton.onClick.AddListener(() => owner.TakeItem(slotIndex));
            }
            if (_putButton != null) _putButton.gameObject.SetActive(false);
        }
        else // player side
        {
            if (_takeButton != null) _takeButton.gameObject.SetActive(false);
            if (_putButton != null)
            {
                bool showPut = !takeOnly;
                _putButton.gameObject.SetActive(showPut);
                if (showPut) _putButton.onClick.AddListener(() => owner.PutItem(slotIndex));
            }
        }
    }
    ```
  - Notes: Container-side Take is always available (you can take from a corpse). Player-side Put is suppressed when looting.

- [x] **Task 4: Add take-only mode to `ContainerUI`**
  - File: `Assets/_Game/Scripts/UI/Inventory/ContainerUI.cs`
  - Action:
    1. Add field: `private bool _takeOnly;`
    2. Change `Open` to accept the flag (default keeps callers safe):
       ```csharp
       public void Open(InventorySystem containerInventory, bool takeOnly = false)
       {
           _containerInventory = containerInventory;
           _takeOnly = takeOnly;
           gameObject.SetActive(true);
           OnScreenOpen();
       }
       ```
    3. `OnSlotDoubleClicked` — block deposit in take-only mode:
       ```csharp
       public void OnSlotDoubleClicked(int index, ContainerSide side)
       {
           if (side == ContainerSide.Container) TakeItem(index);
           else if (!_takeOnly) PutItem(index);
       }
       ```
    4. `ShowContextMenu` — don't show a context menu for the player side in take-only (no deposit action available):
       ```csharp
       if (side == ContainerSide.Player && _takeOnly) return; // loot: cannot deposit into a corpse
       ```
       (place right after the `HideContextMenu(); _contextMenuSlotIndex = slotIndex;` lines, before building the menu)
    5. `PutItem` — backstop guard at the top:
       ```csharp
       public void PutItem(int index)
       {
           if (_takeOnly) return; // loot is take-only
           if (_playerInventory == null || index < 0 || index >= _playerInventory.Count) return;
           ...
       }
       ```
    6. `UpdateDetailPanel` — forward the flag to the detail actions:
       ```csharp
       _containerActions?.Bind(this, slotIndex, item, side, _takeOnly);
       ```
  - Notes: The player grid stays visible (you see your own inventory while looting); only the deposit affordances are removed. Take paths are untouched. `_takeOnly` is set on every `Open`, so a corpse-opened UI never leaks take-only state into a later container open (and vice-versa).

- [x] **Task 5: Forward `takeOnly` from the player-side listener**
  - File: `Assets/_Game/Scripts/World/ContainerSystem.cs`
  - Action: In `HandleContainerOpenRequested`, pass the flag through:
    ```csharp
    _containerUI.Open(data.containerInventory, data.takeOnly);
    ```
  - Notes: Lock-check logic is unchanged. Corpses send `isLocked = false`, so the lock branch is skipped and the UI opens directly in take-only mode.

- [x] **Task 6: Make `EntityPresence` lootable when dead**
  - File: `Assets/_Game/Scripts/AI/EntityPresence.cs`
  - Action:
    1. Add `using Game.Inventory;` (for `InventorySystem`). `GameEventSO_ContainerOpenRequest` + `ContainerOpenRequestData` are in `Game.Core` — already imported.
    2. Add serialized loot channel + cached inventory + `IsDead` helper:
       ```csharp
       [SerializeField] private GameEventSO_ContainerOpenRequest _onLootRequested;
       private InventorySystem _inventory;

       protected bool IsDead => _entityHealth != null && _entityHealth.IsDead;
       ```
    3. Change the prompt/gating so a dead body is lootable; alive base entities stay inert:
       ```csharp
       public virtual string InteractPrompt => IsDead ? "Loot" : string.Empty;
       public virtual bool CanInteract => IsDead;
       ```
    4. Resolve the inventory in `Awake()` (after `_entityHealth`/`_combatState`):
       ```csharp
       _inventory = GetComponent<InventorySystem>();
       ```
    5. Implement loot in `Interact()` + a reusable `OpenLoot()`:
       ```csharp
       public virtual void Interact()
       {
           if (IsDead) OpenLoot();
       }

       /// <summary>Opens the dead entity's inventory as a take-only container (corpse loot).</summary>
       protected void OpenLoot()
       {
           if (_onLootRequested == null)
           {
               GameLog.Warn(TAG, $"No loot event assigned on {gameObject.name} — cannot loot corpse");
               return;
           }
           if (_inventory == null)
           {
               GameLog.Warn(TAG, $"No InventorySystem on {gameObject.name} — nothing to loot");
               return;
           }
           _onLootRequested.Raise(new ContainerOpenRequestData
           {
               containerInventory = _inventory,
               isLocked = false,
               requiredSkillId = null,
               takeOnly = true
           });
       }
       ```
  - Notes: `CanInteract => IsLootable` (`IsDead && HasLoot`) means alive monsters AND emptied corpses show name/HP UI but no `[E]` prompt (scan-2 ignores `CanInteract`); the `[E] Loot` prompt appears exactly when the corpse is dead AND still holds items (revised 2026-06-03 — empty corpse is inert). `_entityHealth` is resolved via `GetComponent` so it reads `IsDead` even though `EntityHealth` is enabled by variants.

- [x] **Task 7: Route dead NPCs to loot (override in `NPCPresence`)**
  - File: `Assets/_Game/Scripts/AI/NPC/NPCPresence.cs`
  - Action: Replace the dead-blocks-dialogue branch with delegation to base loot, and widen `CanInteract`/`InteractPrompt`:
    ```csharp
    public override string InteractPrompt => IsDead ? "Loot" : "Talk";

    // Interactable when dead (loot) OR alive & out of combat (dialogue). In combat & alive → no interaction.
    public override bool CanInteract => IsDead || IsAliveAndOutOfCombat;

    public override void Interact()
    {
        if (Data == null) return;

        if (IsDead)
        {
            base.Interact(); // loot the corpse via the shared container pipeline
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
    ```
  - Notes: `IsDead` / `IsAliveAndOutOfCombat` are inherited `protected` from `EntityPresence`. `base.Interact()` uses the inherited `_onLootRequested` (wired on the NPC variant's `NPCPresence` in Task 9). The same NPC inventory that backs the shop also backs the corpse loot — looting a dead shopkeeper opens its stock as take-only.

- [x] **Task 8: Wire `_onLootRequested` on `Entity_base.prefab` (covers monsters)**
  - File: `Assets/_Game/Prefabs/Entities/Entity_base.prefab`
  - Action (Editor/MCP preferred): select the `Entity_base` root → on the `EntityPresence` component set `On Loot Requested` = `OnContainerOpenRequested` asset. Authoritative end-state — add the field to MonoBehaviour `&6326516008175686655`:
    ```yaml
    --- !u!114 &6326516008175686655
    MonoBehaviour:
      ...
      m_EditorClassIdentifier: Game::Game.World.EntityPresence
      _persistentID: {fileID: 1186952831256218892}
      _onLootRequested: {fileID: 11400000, guid: 27f639c03792674408432d16e8dd2a3c, type: 2}
    ```
  - Notes: `OnContainerOpenRequested.asset` guid = `27f639c03792674408432d16e8dd2a3c`. Monster variants (e.g. `Monster_DarknessSpider Variant`) inherit this wiring automatically. After raw YAML edit: `refresh_unity(mode="if_dirty")`.

- [x] **Task 9: Wire `_onLootRequested` on the NPC variant's `NPCPresence`**
  - File: `Assets/_Game/Prefabs/Entities/Humanoids/NPC_base Variant.prefab`
  - Action (Editor/MCP preferred): the NPC variant removed the inherited base `EntityPresence`, so its own `NPCPresence` (`&2441576283753789042`) needs the inherited `_onLootRequested` field wired. Authoritative end-state — add to that MonoBehaviour:
    ```yaml
    --- !u!114 &2441576283753789042
    MonoBehaviour:
      ...
      m_EditorClassIdentifier: Game::Game.AI.NPCPresence
      _persistentID: {fileID: 8935889491633091201}
      _onDialogueRequested: {fileID: 11400000, guid: dea29e34f28379e4f9b312d51a1ec9e6, type: 2}
      _onLootRequested: {fileID: 11400000, guid: 27f639c03792674408432d16e8dd2a3c, type: 2}
    ```
  - Notes: Without this, a dead NPC's `base.Interact()` logs "No loot event assigned" and does nothing (monsters would still loot since they inherit the base wiring). This is the one wiring step that does NOT propagate from the base. After raw YAML edit: `refresh_unity(mode="if_dirty")`.

- [~] **Task 10: Verify monster + NPC + container in `StartingTown` (no extra edits expected)** — _automated portion done (clean compile AC1, prefab wiring AC11 verified); interactive play-test (AC2–AC10: aim/kill/loot) requires manual in-Editor verification by the user._
  - Files: `Assets/_Game/Prefabs/Entities/Monsters/Monster_DarknessSpider Variant.prefab`, `Assets/_Game/Scenes/StartingTown.unity`
  - Action: Enter Play. Confirm: (a) world `Base_Container` still opens with full take/put (regression); (b) a living monster shows name/HP only (no prompt); (c) kill the spider → `[E] Loot` appears → opens take-only loot UI of its inventory; (d) a living NPC still shows `[E] Talk` and opens dialogue/shop; (e) kill the NPC → `[E] Loot` opens its inventory (shop stock) take-only. Flag any auto-added scene override on the entity instances.
  - Notes: Spider inventory is empty today → loot UI opens empty (expected). No YAML edits expected on the spider or scene.

- [x] **Task 11: Update folder CLAUDE.md docs**
  - Files: `Assets/_Game/Scripts/AI/CLAUDE.md`, `Assets/_Game/Scripts/World/CLAUDE.md`, `Assets/_Game/Scripts/UI/Inventory/CLAUDE.md`
  - Action:
    - `Scripts/World/CLAUDE.md`: update the `EntityPresence` row — base now lootable when dead (`CanInteract => IsDead`, `[E] Loot`, raises the shared `GameEventSO_ContainerOpenRequest` with `takeOnly=true`); note the corpse reuses the container pipeline (`ContainerSystem`/`ContainerUI`).
    - `Scripts/AI/CLAUDE.md`: update the `NPCPresence` row — dead NPC delegates to `base.Interact()` (loot); `CanInteract => IsDead || IsAliveAndOutOfCombat`.
    - `Scripts/UI/Inventory/CLAUDE.md`: note `ContainerUI.Open(inv, takeOnly)` and the take-only loot mode (player-side Put suppressed); `ContainerOpenRequestData.takeOnly` flag.
  - Notes: Also record the gotcha — `EntityPresence._onLootRequested` must be wired on BOTH `Entity_base` (monsters) and the NPC variant's `NPCPresence` (NPCs), because the NPC variant removes the inherited base component.

### Acceptance Criteria

- [ ] **AC1 (compile):** Given the changes, when Unity recompiles, then there are no errors; `ContainerUI.Open(inv, takeOnly)`, `ContainerDetailActions.Bind(..., takeOnly)`, and `ContainerOpenRequestData.takeOnly` resolve at all call sites.
- [ ] **AC2 (monster lootable when dead AND carrying items — REVISED 2026-06-03):** Given a killed monster whose `InventorySystem` holds ≥1 item, when the player aims at the body within interaction range, then the `[E] Loot` prompt shows and pressing Interact opens the loot UI bound to its `InventorySystem`. ⚠️ The `StartingTown` spider has an **empty** inventory today, so under the revised empty-corpse rule it shows **no** prompt (see AC9) — to verify AC2, temporarily add a `_startingItems` entry to the spider (or test on an item-carrying monster).
- [ ] **AC3 (monster inert while alive):** Given a living spider, when aimed at, then NO `[E]` prompt appears (only the world-space name/HP UI), and pressing Interact does nothing (`CanInteract == false` while alive on the base).
- [ ] **AC4 (NPC dialogue preserved while alive):** Given a living, out-of-combat NPC, when aimed at, then `[E] Talk` shows and Interact opens dialogue/shop exactly as before — the loot path does not affect the alive NPC.
- [ ] **AC5 (NPC lootable when dead AND non-empty — REVISED 2026-06-03):** Given a killed NPC whose `InventorySystem` holds ≥1 item (the shopkeeper's stock qualifies), when aimed at, then `[E] Loot` shows and Interact opens the loot UI bound to that `InventorySystem`. A dead NPC with an empty inventory is inert (no prompt) per AC9.
- [ ] **AC6 (in-combat NPC still gated):** Given a living NPC whose `ICombatStateProvider.IsInCombat` is true, when aimed at, then no prompt appears and Interact does nothing (`CanInteract` false: not dead AND not alive-out-of-combat).
- [ ] **AC7 (take-only loot):** Given an open corpse loot UI, when the player double-clicks / right-clicks a slot in their OWN inventory, then no item is deposited into the corpse (no Put double-click, no Put context button, no Put detail-action button); taking items from the corpse works normally.
- [ ] **AC8 (container regression):** Given a world `Base_Container`, when opened, then the UI behaves exactly as before — both Take (container→player) and Put (player→container) work (`takeOnly == false`).
- [ ] **AC9 (empty corpse is inert — REVISED 2026-06-03, supersedes original):** Given a dead entity with an empty `InventorySystem` (e.g. the spider today, OR a corpse whose last item was just looted), when aimed at, then NO `[E] Loot` prompt shows and `Interact()` is a no-op — `CanInteract => IsLootable` where `IsLootable = IsDead && HasLoot` (`HasLoot = InventorySystem.Count > 0`). _Reverses the original "always lootable when dead, even if empty" decision per user request._
- [ ] **AC10 (no leaked mode):** Given the player loots a corpse (take-only) and later opens a world container, when the container UI opens, then Put is available again (the `takeOnly` flag is reset per `Open`).
- [ ] **AC11 (NPC loot wiring):** Given the `NPC_base Variant`, when inspected in Prefab Mode, then its `NPCPresence` has `_onLootRequested` wired to `OnContainerOpenRequested` (guid `27f639c0…`); given a monster `Entity_base` variant, then its inherited `EntityPresence` carries the same wiring from the base.

## Additional Context

### Dependencies

- **No new external libraries, no new event channel, no new UI.** Builds entirely on existing systems: `EntityPresence`/`NPCPresence`, `EntityHealth.IsDead`, `InventorySystem`, `GameEventSO_ContainerOpenRequest` (`OnContainerOpenRequested.asset`), `ContainerSystem`, `ContainerUI`, `ContainerDetailActions`.
- **Builds on:** `tech-spec-entitypresence-base-interactable.md` (single `IInteractable` surface per entity) and `tech-spec-inventorysystem-to-entity-base.md` (every entity carries an `InventorySystem`). Both are implementation-complete.
- **Enables (future, out of scope):** monster/NPC loot-table authoring (populate `_startingItems`), corpse despawn / "looted" empty-body state, loot persistence across save/scene reload, gold looting.

### Testing Strategy

- **Primary — manual in-Editor (`StartingTown`):** verify AC2–AC10 by play-testing: kill the spider and the NPC and loot each; confirm take-only (cannot deposit); confirm a world `Base_Container` still allows put (regression); loot a corpse then open a container to confirm Put returns (AC10); confirm the living NPC's dialogue/shop is unchanged.
- **Compile gate (AC1):** clean recompile (`refresh_unity` `if_dirty`) after the script edits; confirm no console errors.
- **Optional — EditMode** (`Assets/Tests/EditMode/`): assert the polymorphic contract without scene deps — e.g. `EntityPresence.CanInteract == false` with no/alive health and `== true` when a stub `EntityHealth` reports dead; `NPCPresence.InteractPrompt == "Talk"` alive / `"Loot"` dead. Keep light (instantiate via `new GameObject(...).AddComponent<T>()`, avoid `Awake` ordering reliance). Note `IsDead` depends on `_entityHealth` resolved in `Awake`, so a test must add an `EntityHealth` (or expose a seam) before asserting the dead path.

### Notes

- **Two wiring sites (highest-risk item):** `_onLootRequested` is declared on the base `EntityPresence` but must be wired on **both** `Entity_base.prefab` (inherited by monsters) **and** `NPC_base Variant.prefab`'s own `NPCPresence` (which replaced the inherited base component). Miss the NPC one and dead NPCs log "No loot event assigned" and won't loot, while monsters work — an easy-to-miss asymmetry.
- **Take-only state reset:** always set `_takeOnly` in `ContainerUI.Open` (do not rely on a default), so a corpse open never leaves the UI stuck in take-only for the next container, and vice-versa (AC10).
- **`bool` default safety:** `ContainerOpenRequestData.takeOnly` defaults to `false`, and `ContainerUI.Open`'s parameter defaults to `false` — any not-yet-updated raiser/caller keeps full take/put behavior, so the change is backward-safe.
- **Looting a shopkeeper = its shop stock:** the NPC's `InventorySystem` backs both the shop (alive) and the corpse loot (dead) — intended. There is no "is this a shop?" distinction; a dead shop NPC's stock becomes free loot.
- **Combat gating semantics:** `CanInteract` on a dead entity is gated only by `IsDead` (dead entities are never `IsInCombat` — `EntityHealth.Die()` clears combat). Living NPCs retain the alive-and-out-of-combat gate.
- **Raw YAML refresh rule:** after hand-editing any `.prefab`, `refresh_unity(mode="if_dirty")` — `force` discards disk edits (root CLAUDE.md). Prefer Editor/MCP for the two wiring steps so Unity writes the object reference.
- **No persistence:** items taken from a corpse are removed from the runtime `InventorySystem` only; nothing persists across save/scene reload (consistent with prior specs and the `Lockable` persistence stub).
- **Out of scope (flagged):** loot tables, corpse despawn/looted-state, gold loot, save persistence — all deferred.
