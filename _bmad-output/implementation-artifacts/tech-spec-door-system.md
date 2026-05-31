---
title: 'Door System — Interactable Doors + Reusable Lock'
slug: 'door-system'
created: '2026-05-31'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6000.3.10f1 (URP 17.x)', 'C# / .NET Standard 2.1', 'Unity Input System', 'NUnit EditMode tests']
files_to_modify: ['Assets/_Game/Scripts/World/ContainerInteractable.cs', 'Assets/_Game/Prefabs/Player/Player.prefab (+ UICanvas owner of DoorSystem)', 'Assets/_Game/Prefabs/World/Doors/Door Base.prefab (+ Simple door, Poor small door variants)', 'Container prefab(s) using ContainerInteractable (add Lockable, migrate inline values)']
files_to_create: ['Assets/_Game/Scripts/World/Lockable.cs', 'Assets/_Game/Scripts/World/DoorInteractable.cs', 'Assets/_Game/Scripts/World/DoorSystem.cs', 'Assets/_Game/ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs', 'Assets/_Game/Data/Events/OnDoorOpenRequested.asset', 'Assets/Tests/EditMode/LockableTests.cs']
code_patterns: ['IInteractable + InteractionSystem raycast (Layer 8, GetComponentInParent) — UNCHANGED', 'GameEventSO<T> for cross-system comms (ContainerInteractable→ContainerSystem precedent)', 'Composition via GetComponent (ContainerInteractable gets InventorySystem)', 'GameLog + TAG const, no Debug.Log', 'Coroutine for time-based motion (per-frame lerp, yield null)', 'Per-instance authoring values = [SerializeField] (not config SO)']
test_patterns: ['EditMode pure-logic only; MonoBehaviour/time-based motion not unit-tested', 'Player-side gate systems (ContainerSystem) are not unit-tested — DoorSystem follows suit']
---

# Tech-Spec: Door System — Interactable Doors + Reusable Lock

**Created:** 2026-05-31

## Overview

### Problem Statement

Doors must be usable through the existing interaction system (`IInteractable` /
`InteractionSystem`) and should **physically open** — rotating the `Visual` child node — when
interacted with, mirroring how `ContainerInteractable` provides a specific interaction behavior.

Separately, the "locked" concept is currently **inline and duplicated** inside
`ContainerInteractable` (`_isLocked`, `_requiredLockpickingSkill`, `_lockedInteractPrompt`). Doors
also need to be lockable, so this lock logic should be extracted into a **single reusable
abstraction** shared by both doors and containers.

### Solution

1. Add a reusable **`Lockable`** MonoBehaviour: the single source of truth for lock data
   (`IsLocked`, required skill, locked prompt) plus an `Unlock()` operation.
2. Add **`DoorInteractable`** (`IInteractable`): smoothly rotates the `Visual` child between
   closed/open angles (re-closeable). When **unlocked**, it toggles open locally. When **locked**, it
   raises a `GameEventSO_DoorOpenRequest`.
3. Add a player-side **`DoorSystem`** that owns `PlayerSkills`, resolves the lock on the request event,
   and (on success) unlocks + opens the specific door — **exactly mirroring `ContainerSystem`**.
4. Refactor **`ContainerInteractable`** to read its lock data from a sibling `Lockable` instead of its
   inline fields. `ContainerSystem` and the existing event flow stay unchanged.

> **Decision history:** an earlier draft changed `IInteractable.Interact()` to
> `Interact(GameObject interactor)` so a door could read `PlayerSkills` directly. Rejected — it bends two
> documented rules (`project-context.md` ln 246–251 forbids the param signature; ln 50/216 forbid
> cross-system direct references). The event + `DoorSystem` route keeps the cross-system `PlayerSkills`
> touch inside a player-side system via `[SerializeField]` injection (the `ContainerSystem` pattern),
> needs **no interface change and no rule edits**, and is symmetric with containers.

### Scope

**In Scope:**
- `Lockable` component (`Game.World`) — lock data holder + `Unlock()`.
- `DoorInteractable` (`Game.World`) — smooth, re-closeable rotation of the `Visual` node; locked doors
  route through the event, unlocked doors toggle locally.
- `DoorSystem` (`Game.World`, on the player) — skill-gated lock resolution + open callback.
- `GameEventSO_DoorOpenRequest` event type (`Game.Core`) + `OnDoorOpenRequested.asset`.
- Refactor `ContainerInteractable` to consume `Lockable`; migrate container prefab(s) to add `Lockable`.
- Prefab wiring: `Door Base.prefab` + variants (`Simple door`, `Poor small door`); add `DoorSystem` to
  the Player prefab and cross-wire the event asset.

**Out of Scope:**
- Persistence of open / unlocked state across save & scene reload — **deferred to a separate spec stub**
  (`tech-spec-lockable-persistence-stub.md`, created in Step 3). Intended future answer: unlocked doors
  AND containers persist as unlocked.
- Key-item-based unlocking (skill-based only, matching the current container model).
- NPC/AI opening doors; door audio (SFX) and VFX.
- Any change to `IInteractable`, `InteractionSystem`, `ContainerSystem`, or `project-context.md`.

## Context for Development

### Codebase Patterns

- **Interaction contract** (`Scripts/World/IInteractable.cs`): `InteractPrompt`, `NameTag`,
  `CanInteract`, `Interact()` — **unchanged**. Discovered by `InteractionSystem`'s SphereCast against the
  **Interactable layer (Layer 8)** via `GetComponentInParent<IInteractable>()`; the component may live on
  the root above the collider. No manual registration.
- **`GameEventSO<T>`** (`Scripts/.../Events/GameEventSO.cs`, `Game.Core`): `Raise(payload)`,
  `AddListener`/`RemoveListener` (subscribe in `OnEnable`, unsubscribe in `OnDisable`). Concrete type +
  its payload `struct` live together in **one file** under `ScriptableObjects/Events/`
  (see `GameEventSO_ContainerOpenRequest.cs`). Each concrete `GameEventSO` subclass needs its own file
  (domain-reload `m_Script` safety). Event assets are `On<Name>.asset` in `Data/Events/`.
- **`ContainerInteractable` / `ContainerSystem` = the exact template:** the interactable holds lock data
  + raises `GameEventSO_ContainerOpenRequest{ inventory, isLocked, requiredSkillId }`; the player-side
  `ContainerSystem` holds `[SerializeField] PlayerSkills`, checks `PlayerSkills.HasSkill(requiredSkillId)`,
  and acts. `DoorSystem` copies this structure 1:1.
- **`PlayerSkills`** (`Game.Progression`, MonoBehaviour, NOT a singleton): `bool HasSkill(string skillId)`.
  `SkillSO.skillId` is the id. Lives on the Player root.
- **Door Base prefab** (`Prefabs/World/Doors/Door Base.prefab`): root **Layer 8**, holds the placeholder
  `InteractableObject` (`_promptText: "Open Door"`); child **`Visual`** carries the `BoxCollider`
  (Size 1×2×0.1, Center 0.5,1,0 — offset to one edge). Variants: `Simple door`, `Poor small door`.
- **Logging:** `GameLog.Info/Warn/Error` + `private const string TAG`; never `Debug.Log`.
- **Lifecycle:** subscribe events in `OnEnable`/unsubscribe in `OnDisable`; null-guard `OnDisable` if the
  field may be unset; null-guard public methods that use a serialized dependency.

### New & Changed Types

**`Lockable` (new, `Game.World`)** — pure lock-data holder:
- `[SerializeField] bool _isLocked`, `[SerializeField] SkillSO _requiredSkill`,
  `[SerializeField] string _lockedPrompt = "Locked"`.
- `public bool IsLocked => _isLocked;`
- `public string RequiredSkillId => _requiredSkill != null ? _requiredSkill.skillId : null;`
- `public string LockedPrompt => _lockedPrompt;`
- `public void Unlock()` → sets `_isLocked = false` + `GameLog.Info`. Skill locks are idempotent
  (no consumable), so calling when already unlocked is harmless.
- (`SkillSO` is in `Game.Progression` but is data; `ContainerInteractable` already references it from
  `Game.World`. Referencing a data SO across systems is allowed — the rule bans cross-system *script*
  calls, not data-SO references. All in the single `Game` assembly, so no asmdef change.)

**`GameEventSO_DoorOpenRequest` (new, `Game.Core`)** — mirrors the container event file:
```csharp
[System.Serializable]
public struct DoorOpenRequestData
{
    public DoorInteractable door;   // runtime scene ref passed through Raise() — NOT stored in any SO asset
    public bool isLocked;
    public string requiredSkillId;
}
[CreateAssetMenu(menuName = "Game/Events/Door Open Request", fileName = "NewDoorOpenRequestEvent")]
public class GameEventSO_DoorOpenRequest : GameEventSO<DoorOpenRequestData> { }
```
- Asset instance: `Assets/_Game/Data/Events/OnDoorOpenRequested.asset`.

**`DoorInteractable` (new, `Game.World`, `IInteractable`)**:
- `[SerializeField] Transform _visual;` `[SerializeField] float _openAngle = 90f;`
  `[SerializeField] float _openDuration = 0.5f;` `[SerializeField] string _interactPrompt = "Open Door";`
  `[SerializeField] GameEventSO_DoorOpenRequest _onDoorOpenRequested;`
- `private Lockable _lockable;` (`GetComponent` in `Awake`, optional → null = never locked).
  `private bool _isOpen;` `private Coroutine _rotateRoutine;`
- `InteractPrompt` → if `_lockable != null && _lockable.IsLocked` return `_lockable.LockedPrompt`;
  else `_isOpen ? "Close Door" : _interactPrompt`. `NameTag => ""`. `CanInteract => true`.
- `Interact()`:
  - If `_lockable == null || !_lockable.IsLocked` → **`ToggleOpen()` locally** (no event, no cross-system
    touch).
  - Else → `_onDoorOpenRequested.Raise(new DoorOpenRequestData{ door=this, isLocked=true,
    requiredSkillId=_lockable.RequiredSkillId });` (null-guard the event ref + warn).
- `public void Unlock()` → `_lockable?.Unlock();`
- `public void ToggleOpen()` → start/replace the rotation coroutine toward
  `_isOpen ? 0° : _openAngle` on `_visual.localRotation`, flip `_isOpen`. Per-frame lerp (`yield return null`).
  Guard `_visual == null`.

**`DoorSystem` (new, `Game.World`, on the player) — mirrors `ContainerSystem`**:
- `[SerializeField] GameEventSO_DoorOpenRequest _onDoorOpenRequested;` `[SerializeField] PlayerSkills _playerSkills;`
- `OnEnable`/`OnDisable`: add/remove listener (null-guard).
- `HandleDoorOpenRequested(data)`: if `data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId)`:
  null-guard `_playerSkills`; if `!_playerSkills.HasSkill(...)` → `GameLog.Info` "door locked — lacks
  skill" + return. On success (or not locked) → `data.door.Unlock(); data.door.ToggleOpen();`.
  (`DoorSystem` and `DoorInteractable` are both `Game.World` → the callback is a same-system direct call,
  which is allowed.)

**`ContainerInteractable` (refactor)**:
- Remove `_isLocked`, `_lockedInteractPrompt`, `_requiredLockpickingSkill`.
- Add `private Lockable _lockable;` (`GetComponent` in `Awake`, optional).
- `InteractPrompt` → `_lockable != null && _lockable.IsLocked ? _lockable.LockedPrompt : _interactPrompt`.
- `Interact()` payload → `isLocked = _lockable != null && _lockable.IsLocked`,
  `requiredSkillId = (that && _lockable != null) ? _lockable.RequiredSkillId : null`.
- `ContainerSystem` is **untouched** (still reads `isLocked`/`requiredSkillId` from the event).

### Door Motion & Hinge

- Coroutine lerps `_visual.localRotation` between closed (0°) and `_openAngle` over `_openDuration`
  (`yield return null` per frame — a per-frame lerp, so the cached-`WaitForSeconds` rule doesn't apply).
- **Hinge gotcha:** rotation pivots around the `Visual` origin. In `Door Base`, `Visual` sits at the root
  origin and the collider is edge-offset (Center x=0.5) → origin is the hinge edge (correct). **Variants
  override the collider and add a rotated mesh child** (`Simple door` adds `SM_Door` at Y=90°, collider
  Center 0,1.17,0.47), so each variant's `Visual` pivot must be verified to sit on the intended hinge edge
  during wiring, or the swing will look wrong.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Contract `DoorInteractable` implements (unchanged). |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Caller (`:166`), Layer-8 raycast (unchanged). |
| `Assets/_Game/Scripts/World/ContainerInteractable.cs` | Refactor target + the lock-data/event template. |
| `Assets/_Game/Scripts/World/ContainerSystem.cs` | The exact pattern `DoorSystem` mirrors. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_ContainerOpenRequest.cs` | Template for the new event file. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Base API (`Raise`/`AddListener`/`RemoveListener`). |
| `Assets/_Game/Scripts/Player/Progression/PlayerSkills.cs` | `bool HasSkill(string)`. |
| `Assets/_Game/ScriptableObjects/Skills/SkillSO.cs` | `string skillId` getter. |
| `Assets/_Game/Data/Events/OnContainerOpenRequested.asset` | Naming/location template for the new asset. |
| `Assets/_Game/Prefabs/World/Doors/Door Base.prefab` (+ variants) | Wiring target. |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Add `DoorSystem` + wire `PlayerSkills` + event. |

### Technical Decisions

- **Lock abstraction = `Lockable` component** (composition), not an interface — single source of lock
  data + behavior; doors and containers `GetComponent<Lockable>()`; absence = never locked.
- **Lock resolution = event + `DoorSystem`** (mirrors `ContainerSystem`), NOT an `Interact()` signature
  change. Keeps the cross-system `PlayerSkills` touch in a player-side system; no interface/rule edits.
- **Unlocked doors toggle locally** (no event) — the event channel is used only when a real cross-system
  skill check is required, minimizing coupling.
- **Door flips its lock on a successful pick** (`Unlock()`), so subsequent opens take the local path. The
  container keeps re-checking each open (its `Unlock()` would need `ContainerSystem` to hold the specific
  `Lockable` — out of scope); the deferred-persistence stub reconciles both.
- **Per-door values are `[SerializeField]`**, not a config SO (per-instance authoring data).
- **Persistence deferred** to `tech-spec-lockable-persistence-stub.md`.

## Implementation Plan

> Order is dependency-first. Tasks 1–4 create scripts that compile **together** in the single `Game`
> assembly (the event struct references `DoorInteractable` and vice-versa — this is fine within one
> assembly), so do the `read_console` compile check only after Task 4.

### Tasks

- [ ] **Task 1: Create `Lockable` component.**
  - File: `Assets/_Game/Scripts/World/Lockable.cs` (new)
  - Action: `namespace Game.World`. MonoBehaviour with `private const string TAG = "[Lockable]";`.
    Fields: `[SerializeField] private bool _isLocked;`, `[SerializeField] private SkillSO _requiredSkill;`
    (`using Game.Progression;`), `[SerializeField] private string _lockedPrompt = "Locked";`.
    Props: `public bool IsLocked => _isLocked;`,
    `public string RequiredSkillId => _requiredSkill != null ? _requiredSkill.skillId : null;`,
    `public string LockedPrompt => _lockedPrompt;`.
    Method: `public void Unlock() { if (!_isLocked) return; _isLocked = false; GameLog.Info(TAG, $"{gameObject.name} unlocked"); }`.
  - Notes: For `LockableTests`, add an `internal` test-only setter or `[SerializeField]`-via-reflection
    path so a test can set `_isLocked = true` before asserting `Unlock()` (decide implementation; do not
    add a public setter to production API).

- [ ] **Task 2: Create the door-open event type.**
  - File: `Assets/_Game/ScriptableObjects/Events/GameEventSO_DoorOpenRequest.cs` (new)
  - Action: `namespace Game.Core`, `using Game.World;`. Define
    `[System.Serializable] public struct DoorOpenRequestData { public DoorInteractable door; public bool isLocked; public string requiredSkillId; }`
    and `[CreateAssetMenu(menuName = "Game/Events/Door Open Request", fileName = "NewDoorOpenRequestEvent")] public class GameEventSO_DoorOpenRequest : GameEventSO<DoorOpenRequestData> { }`.
  - Notes: Mirrors `GameEventSO_ContainerOpenRequest.cs` exactly. Struct + concrete class share this one
    file (per the project's event-file convention).

- [ ] **Task 3: Create `DoorInteractable`.**
  - File: `Assets/_Game/Scripts/World/DoorInteractable.cs` (new)
  - Action: `namespace Game.World`, `IInteractable`, `private const string TAG = "[Door]";`.
    Serialized: `_visual` (Transform), `_openAngle` (float, default 90), `_openDuration` (float, default
    0.5), `_interactPrompt` (string, "Open Door"), `_onDoorOpenRequested` (GameEventSO_DoorOpenRequest).
    Private: `_lockable` (`GetComponent<Lockable>()` in `Awake`), `_isOpen`, `_rotateRoutine`.
    `Awake` also warns via `GameLog.Warn` if `_visual == null`.
    `InteractPrompt` → locked ⇒ `_lockable.LockedPrompt`; else `_isOpen ? "Close Door" : _interactPrompt`.
    `NameTag => ""`, `CanInteract => true`.
    `Interact()` → if `_lockable == null || !_lockable.IsLocked` then `ToggleOpen()`; else if
    `_onDoorOpenRequested == null` `GameLog.Warn` + return; else `Raise(new DoorOpenRequestData{ door=this,
    isLocked=true, requiredSkillId=_lockable.RequiredSkillId })`.
    `public void Unlock() => _lockable?.Unlock();`
    `public void ToggleOpen()` → guard `_visual == null`; if `_rotateRoutine != null` stop it; compute
    target = `_isOpen ? 0f : _openAngle`; flip `_isOpen`; `_rotateRoutine = StartCoroutine(RotateCoroutine(target));`.
    `RotateCoroutine(targetY)` → lerp `_visual.localRotation` from start to `Quaternion.Euler(0, targetY, 0)`
    over `_openDuration` using `yield return null`; snap to target at end.
  - Notes: `ToggleOpen` is `public` so the same-system `DoorSystem` can call it. Coroutine guard:
    if `gameObject.activeInHierarchy` is false, don't start (project rule).

- [ ] **Task 4: Create `DoorSystem` (player-side resolver).**
  - File: `Assets/_Game/Scripts/World/DoorSystem.cs` (new)
  - Action: `namespace Game.World`, `private const string TAG = "[DoorSystem]";`. Serialized:
    `_onDoorOpenRequested` (GameEventSO_DoorOpenRequest), `_playerSkills` (`Game.Progression.PlayerSkills`).
    `OnEnable` → if event null `GameLog.Warn` + return; else `AddListener(HandleDoorOpenRequested)`.
    `OnDisable` → if event null return; else `RemoveListener`.
    `HandleDoorOpenRequested(DoorOpenRequestData data)` → if `data.door == null` return;
    if `data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId)`: if `_playerSkills == null`
    `GameLog.Warn` + return; if `!_playerSkills.HasSkill(data.requiredSkillId)` `GameLog.Info`
    "door locked — lacks skill" + return. Then `data.door.Unlock(); data.door.ToggleOpen();`.
  - Notes: Structurally identical to `ContainerSystem.HandleContainerOpenRequested`.

- [ ] **Task 5: Compile check + create event asset.**
  - Action: `read_console` — resolve any compile errors before continuing. Then create
    `Assets/_Game/Data/Events/OnDoorOpenRequested.asset` (instance of `GameEventSO_DoorOpenRequest`) via
    `Create → Game/Events/Door Open Request` (or MCP `manage_scriptable_object`). Name it
    `OnDoorOpenRequested`.

- [ ] **Task 6: Refactor `ContainerInteractable` to use `Lockable`.**
  - File: `Assets/_Game/Scripts/World/ContainerInteractable.cs`
  - Action: Remove `_isLocked`, `_lockedInteractPrompt`, `_requiredLockpickingSkill` fields and the
    `using Game.Progression;` if now unused. Add `private Lockable _lockable;` set in `Awake` via
    `GetComponent<Lockable>()` (optional → null = unlocked). Update `InteractPrompt` to
    `(_lockable != null && _lockable.IsLocked) ? _lockable.LockedPrompt : _interactPrompt`. In `Interact()`
    build the payload as `isLocked = _lockable != null && _lockable.IsLocked`,
    `requiredSkillId = (isLocked && _lockable != null) ? _lockable.RequiredSkillId : null`.
  - Notes: `ContainerSystem` is **not** modified. Keep the existing `_inventory`/event null-guards.

- [ ] **Task 7: Compile check.**
  - Action: `read_console` — confirm clean compile after the container refactor.

- [ ] **Task 8: Wire `DoorSystem` onto the Player prefab.**
  - File: `Assets/_Game/Prefabs/Player/Player.prefab`
  - Action: Add a `DoorSystem` component to the Player root (same level as `InteractionSystem` /
    `ContainerSystem`). Assign `_playerSkills` → the Player's `PlayerSkills`; assign `_onDoorOpenRequested`
    → `OnDoorOpenRequested.asset`.
  - Notes: Edit the `.prefab` via MCP (`manage_components`) or open in Prefab Mode. If editing YAML
    directly, follow the project rule: `refresh_unity(mode="if_dirty")`, never `force`.

- [ ] **Task 9: Wire the door prefabs.**
  - Files: `Assets/_Game/Prefabs/World/Doors/Door Base.prefab` (+ `Simple door`, `Poor small door` variants)
  - Action: On `Door Base`, remove the placeholder `InteractableObject` and add `DoorInteractable`; assign
    `_visual` → the `Visual` child Transform; assign `_onDoorOpenRequested` → `OnDoorOpenRequested.asset`;
    set `_interactPrompt`/`_openAngle`/`_openDuration` as desired. For a **locked** door, also add a
    `Lockable` component and set `_isLocked`/`_requiredSkill`/`_lockedPrompt`. Verify each variant's
    `Visual` pivot sits on the intended hinge edge (variants override the collider + add a rotated mesh).
  - Notes: Root must remain **Layer 8 (Interactable)**. Variants inherit `DoorInteractable` from the base
    automatically; only override per-variant authoring values + hinge.

- [ ] **Task 10: Migrate container prefab(s).**
  - File: `Assets/_Game/Prefabs/World/Containers/Base_Container.prefab` (+ any locked container variants)
  - Action: `Base_Container` is currently unlocked (`_isLocked: 0`, no skill) → after the refactor it needs
    **no** `Lockable` (absence = unlocked); just verify it still opens. For any container variant that was
    locked, add a `Lockable` and migrate its old inline `_isLocked` / `_requiredLockpickingSkill` /
    `_lockedInteractPrompt` values onto it.

- [ ] **Task 11: Add `LockableTests`.**
  - File: `Assets/Tests/EditMode/LockableTests.cs` (new)
  - Action: `namespace Tests.EditMode`. Cover: fresh `Lockable` reports `IsLocked == false`;
    `RequiredSkillId == null` when no skill assigned; after setting locked, `Unlock()` sets
    `IsLocked == false`. Use the Task-1 test-only setter/reflection to flip `_isLocked` true.
  - Notes: Door rotation + `DoorSystem` gate are runtime/MonoBehaviour → not Edit-mode tested
    (consistent with `ContainerSystem`).

- [ ] **Task 12: Create the deferred persistence stub spec.**
  - File: `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md` (new)
  - Action: A short stub (problem + intended approach) for persisting unlocked doors AND containers (and
    door open/closed state) via `WorldStateManager` facts + per-instance keys. (Created alongside this spec.)

- [ ] **Task 13: Update folder CLAUDE.md.**
  - File: `Assets/_Game/Scripts/World/CLAUDE.md`
  - Action: Add `Lockable`, `DoorInteractable`, `DoorSystem` to the "What's here" table, and a one-line
    note on the lock pattern (`Lockable` = data; locked interactables route a `GameEventSO` to a
    player-side system that owns `PlayerSkills`).

### Acceptance Criteria

- [ ] **AC1 (open):** Given the player faces an **unlocked** door within interaction range, when they
  press Interact, then the `Visual` node rotates from 0° to `_openAngle` over `_openDuration` and the door
  ends open.
- [ ] **AC2 (re-close):** Given an open door, when the player presses Interact again, then the `Visual`
  node rotates back to 0° and the door ends closed.
- [ ] **AC3 (locked, lacks skill):** Given a locked door whose required skill the player does NOT have,
  when they press Interact, then the door does not move, a `GameLog.Info` ("locked — lacks skill") is
  written, and the prompt shows `Lockable.LockedPrompt`.
- [ ] **AC4 (locked, has skill):** Given a locked door whose required skill the player HAS, when they
  press Interact, then `Lockable.IsLocked` becomes false and the door opens; a subsequent Interact toggles
  it locally (no event raised).
- [ ] **AC5 (prompt state):** Given a locked door the prompt equals `LockedPrompt`; given an unlocked
  closed door the prompt equals `_interactPrompt`; given an open door the prompt equals "Close Door".
- [ ] **AC6 (no Lockable):** Given a `DoorInteractable` with no sibling `Lockable`, when the player
  interacts, then it toggles open locally and **never** raises `OnDoorOpenRequested`.
- [ ] **AC7 (missing event ref):** Given a **locked** door whose `_onDoorOpenRequested` is unassigned,
  when the player interacts, then a `GameLog.Warn` is written and no `NullReferenceException` occurs.
- [ ] **AC8 (container parity):** Given the refactored `ContainerInteractable` with a `Lockable` marked
  locked + a required skill, when a player lacking the skill opens it, then `ContainerSystem` blocks it
  exactly as before; given `Base_Container` (unlocked), when interacted, then the container UI opens
  (regression: no behavior change).
- [ ] **AC9 (tests):** Given EditMode tests, when run, then `LockableTests` pass (`Unlock()` flips
  `IsLocked`; `RequiredSkillId` maps from `SkillSO`/null) and **all pre-existing EditMode tests still
  pass** (interface untouched).

## Additional Context

### Dependencies

- All `IInteractable` implementers confirmed (exhaustive grep): `InteractableObject`,
  `ContainerInteractable`, `ItemPickup`, `NPCPresence` (+ a test stub). The signature is unchanged, so
  none of these (nor their tests) need edits except `ContainerInteractable`'s lock refactor.
- `PlayerSkills` (`Game.Progression`) — `HasSkill(string)`; `SkillSO.skillId`.
- Everything compiles into the single `Game` assembly — no asmdef changes.
- Container prefab(s) using `ContainerInteractable` must be located and given a `Lockable` (migrating the
  old inline `_isLocked`/skill/prompt values) so the refactor doesn't silently drop lock data.

### Testing Strategy

- **`LockableTests` (EditMode, pure logic):** `IsLocked` reflects the serialized field; `Unlock()` flips
  `IsLocked` → false; `RequiredSkillId` is null when no skill assigned. (Setting `_isLocked=true` for the
  flip test needs a test-only setter or reflection — decide in Step 3.)
- **`DoorInteractable` rotation + `DoorSystem` gate are MonoBehaviour/runtime** → not Edit-mode unit-tested
  (consistent with `ContainerSystem`, which is also untested); verify manually in Play mode.
- **No existing tests change** (the interface is untouched).
- After script changes: `read_console` for compile errors before using new types / wiring prefabs.

### Notes

- Deferred persistence stub: `_bmad-output/implementation-artifacts/tech-spec-lockable-persistence-stub.md`
  (created in Step 3) — must cover persisting BOTH unlocked doors and unlocked containers (and likely the
  open/closed state of doors) via `WorldStateManager` facts + a per-instance key.
- Doors are NOT enemies/NPCs/loot-containers, so they do **not** require `PersistentID` now
  (`World/CLAUDE.md`); persistence is the stub's concern.
- `[CLAUDE.md candidate]` for sign-off: the new `Lockable` + door event/`DoorSystem` trio belongs in
  `Scripts/World/CLAUDE.md`'s "What's here" table.
