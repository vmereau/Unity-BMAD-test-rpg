---
title: 'Notification Toast System'
slug: 'notification-toast-system'
created: '2026-06-04'
status: 'Completed'
stepsCompleted: [1, 2, 3, 4, 5, 6]
tech_stack: ['Unity 6000.3.10f1', 'URP 17.x', 'TextMeshPro (TMP_Text)', 'GameEventSO event channels', 'uGUI Canvas + CanvasGroup']
files_to_modify: ['CREATE Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs', 'CREATE Assets/_Game/Data/Events/OnLockUnlocked.asset (+.meta)', 'MODIFY Assets/_Game/Scripts/World/DoorSystem.cs', 'MODIFY Assets/_Game/Scripts/World/ContainerSystem.cs', 'CREATE NotificationToast entry prefab (TMP_Text + CanvasGroup)', 'EDIT UICanvas.prefab (add toast container under Game HUD layer)', 'UPDATE Assets/_Game/Scripts/UI/HUD/CLAUDE.md']
code_patterns: ['GameEventSO<T> subscribe in OnEnable / unsubscribe in OnDisable (never Start)', 'HUD UI on UICanvas Game layer (HealthBarUI/StaminaBarUI precedent)', 'GameLog.Info/Warn(TAG, ...) — never Debug.Log', 'enabled=false in Awake on missing dep + OnDisable null guard', 'Coroutine for time-based; cache WaitForSeconds; check activeInHierarchy before StartCoroutine', 'CanvasGroup.alpha for fade; uGUI not OnGUI']
test_patterns: ['No existing test infra (no Assets/_Game/Tests/) — Play-mode manual validation; optional pure static message-format helper for a future EditMode test']
---

# Tech-Spec: Notification Toast System

**Created:** 2026-06-04

## Overview

### Problem Statement

The game provides no transient on-screen feedback when meaningful gameplay events occur. When the player kills an enemy (gains XP), levels up, or unlocks a door or chest, nothing confirms it visually. The player has no immediate acknowledgement that the action registered.

### Solution

Add a HUD-resident `NotificationToastUI` controller on the **UICanvas** (Game/HUD layer, alongside `HealthBarUI` and `StaminaBarUI`). It subscribes directly to the relevant `GameEventSO` channels, formats a short message string per event, and pushes it onto a **vertical stack of toasts**. Up to **5 toasts** are visible at once (oldest evicted when a 6th arrives); each toast holds for **~3 seconds**, then **fades out**. Uses `TMP_Text` per the project UI convention.

### Scope

**In Scope:**
- New `NotificationToastUI` MonoBehaviour controller on the UICanvas HUD layer.
- Subscribes to and formats messages for three sources:
  - `OnXPGained` (`GameEventSO_Int`, existing) → `"Experience +{amount}"`
  - `OnLevelUp` (`GameEventSO_Int`, existing) → `"Level up!"`
  - A **new** lock-unlocked event channel → `"Door unlocked!"` / `"Chest unlocked!"`
- A new `GameEventSO_String` channel (`OnLockUnlocked`) raised by `DoorSystem` and `ContainerSystem` on a successful unlock, carrying the noun (`"Door"` / `"Chest"`).
- Stacked display: max 5 visible, FIFO eviction of the oldest when full.
- ~3s hold per toast (serialized, tunable), followed by a fade-out.
- TMP-based toast entry visuals on the UICanvas.

**Out of Scope:**
- Persistent notification history / log panel.
- Per-type icons or color coding (text only for v1).
- Sound effects on notification.
- Notifications for other events (quests, gold, item pickups, skills learned) — the per-event subscription pattern makes adding these trivial later, but they are not wired in this spec.
- Save/load of any notification state.

## Context for Development

### Codebase Patterns

- **Event channels:** `GameEventSO<T>` (`Assets/_Game/ScriptableObjects/Events/`) is the decoupling mechanism. `Raise(payload)` iterates listeners in reverse; `AddListener`/`RemoveListener` are idempotent-safe. Concrete assets live in `Assets/_Game/Data/Events/`. Listeners subscribe in `OnEnable`, unsubscribe in `OnDisable` — **never** in `Start`.
- **HUD UI home:** Overlay UI (`HealthBarUI`, `StaminaBarUI`) subscribes to `GameEventSO` channels in `OnEnable`/`OnDisable`, uses `TMP_Text`, and lives on the HUD canvas (the `Game` GameObject, UI layer 5, in `UICanvas.prefab`). The toast controller follows this exact pattern. `StaminaBarUI` is the canonical minimal template (Awake null-checks → `enabled = false` + warn; subscribe/unsubscribe; private handler).
- **GameEventSO subclass rule (memory):** concrete `GameEventSO_*` types must each be in their own `.cs` file or `m_Script` breaks on domain reload. **The new unlock channel reuses the existing `GameEventSO_String` type** — no new subclass `.cs` file; only a new `.asset` pointing at `GameEventSO_String` (script GUID `44d19ccac4da84746a96c5445079f059`, `m_EditorClassIdentifier: Game:Game.Core:GameEventSO_String`).
- **Logging:** `GameLog.Info/Warn(TAG, ...)` with a `const string TAG` — never `Debug.Log`. (If `NotificationToastUI` has no `GameLog` calls, do **not** declare a dead `TAG` — project review flags that.)
- **Awake disable guard:** if `Awake` sets `enabled = false` on missing deps, `OnDisable` must null-guard fields before touching them.
- **Coroutine/time rules (project-context.md):** prefer coroutines for time-based logic; **cache `WaitForSeconds`** (never `new` per call); coroutines silently stop on disable — check `gameObject.activeInHierarchy` before `StartCoroutine`. Use `CanvasGroup.alpha` for fades; `OnGUI` is deprecated for gameplay UI.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/UI/HUD/StaminaBarUI.cs` | Canonical minimal HUD-UI template (Awake guard, OnEnable/OnDisable subscribe, private handler). Mirror its shape. |
| `Assets/_Game/Scripts/UI/HUD/ActionBarUI.cs` | Precedent for a HUD controller managing multiple child UI elements + `enabled=false` on missing deps. |
| `Assets/_Game/Scripts/Player/Progression/XPSystem.cs` | Raises `OnXPGained` (`GameEventSO_Int`) with the XP `amount` in `GiveExperience`. Source for "Experience +N". |
| `Assets/_Game/Scripts/Player/Progression/LevelSystem.cs` | Raises `OnLevelUp` (`GameEventSO_Int`) with `CurrentLevel` in `CheckLevelUp`. Source for "Level up!". |
| `Assets/_Game/Scripts/World/DoorSystem.cs` | Player-side door unlock resolver. `HandleDoorOpenRequested` calls `data.door.Unlock()` on skill-pass — hook the unlock notification here. Only locked doors reach this system. |
| `Assets/_Game/Scripts/World/ContainerSystem.cs` | Player-side container resolver. `HandleContainerOpenRequested` gates on `data.isLocked` + skill, then opens UI. It does **not** call `Unlock()`; notify only when `data.isLocked` was true and the skill check passed. |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_String.cs` | Existing `GameEventSO<string>` concrete type — reuse for `OnLockUnlocked` (no new subclass). |
| `Assets/_Game/Data/Events/OnSkillLearned.asset` | Reference for the `GameEventSO_String` `.asset` YAML layout (script GUID + `m_EditorClassIdentifier`). |
| `Assets/_Game/Prefabs/UI/UICanvas.prefab` | Target prefab. Add the toast container under the `Game` HUD GameObject (UI layer 5). |

### Technical Decisions

- **Subscribe-per-event (chosen by user):** `NotificationToastUI` references each source channel (`OnXPGained`, `OnLevelUp`, `OnLockUnlocked`) and formats text itself. Message wording stays centralized in the controller.
- **Unlock event is new + reuses `GameEventSO_String`:** no unlock event exists today. A new `OnLockUnlocked.asset` (`GameEventSO_String`) carries the **noun** (`"Door"` / `"Chest"`); the controller formats `$"{noun} unlocked!"`. Raised from the player-side resolvers (`DoorSystem`, `ContainerSystem` — both already in `Game.World`, both effectively singletons in the loaded scene), **not** from per-instance `Lockable`, so the SO is wired in exactly two places instead of on every lockable prefab.
  - `DoorSystem`: raise immediately after `data.door.Unlock()` (success path; only locked doors arrive here).
  - `ContainerSystem`: raise only inside the branch where `data.isLocked == true` and the `_playerSkills.HasSkill(...)` check passed (i.e., right before `_containerUI.Open(...)`), so already-unlocked containers don't notify.
- **Stacked display, capped at 5, FIFO eviction:** a `_container` `RectTransform` with a `VerticalLayoutGroup` holds active toast entries; a serialized `_toastEntryPrefab` (root `CanvasGroup` + child `TMP_Text`) is instantiated per message. When a 6th arrives, the oldest active entry is expired/destroyed immediately. Max-5 is a serialized field (`_maxVisible`, default 5). Destroy/instantiate at this small count is acceptable per UI CLAUDE.md.
- **Per-toast lifecycle via coroutine:** each entry runs one coroutine — (optional quick fade-in) → hold `_holdSeconds` (default 3, serialized) → fade-out over `_fadeOutSeconds` (default ~0.3, serialized via `CanvasGroup.alpha`) → destroy + remove from the active list. `WaitForSeconds` instances cached on the controller. Controller checks `activeInHierarchy` before starting a coroutine.
- **Toast entry visual:** a small prefab `NotificationToast.prefab` — root with `CanvasGroup`, child `TMP_Text` (auto-size/contained), optional background `Image`. Controller sets the text and parents it under `_container`.

## Implementation Plan

Tasks are ordered lowest-dependency first: event asset → producer wiring → UI controller → prefab/scene wiring → docs.

### Tasks

- [x] **Task 1: Create the `OnLockUnlocked` event asset**
  - File: `Assets/_Game/Data/Events/OnLockUnlocked.asset` (+ generated `.meta`)
  - Action: Create a new `GameEventSO_String` asset named `OnLockUnlocked`. Preferred: in Unity, right-click in `Assets/_Game/Data/Events` → `Create → Game/Events/String Event`, rename to `OnLockUnlocked`. (Manual YAML fallback: mirror `OnSkillLearned.asset` — `m_Script` guid `44d19ccac4da84746a96c5445079f059`, `m_Name: OnLockUnlocked`, `m_EditorClassIdentifier: Game:Game.Core:GameEventSO_String`.)
  - Notes: This is a String channel whose payload is the unlocked object's **noun** (`"Door"` / `"Chest"`), NOT the full sentence — the controller appends `" unlocked!"`. No new C# subclass — reuses the existing `GameEventSO_String` type (see GameEventSO single-file memory rule).

- [x] **Task 2: Raise `OnLockUnlocked` from `DoorSystem`**
  - File: `Assets/_Game/Scripts/World/DoorSystem.cs`
  - Action: Add `[SerializeField] private GameEventSO_String _onLockUnlocked;`. In `HandleDoorOpenRequested`, immediately after the existing `data.door.Unlock();` line, add `_onLockUnlocked?.Raise("Door");`.
  - Notes: Only locked doors reach `DoorSystem` (unlocked doors toggle locally in `DoorInteractable`), and this point is past the skill gate, so it is exactly the success path. Use `?.` (event may be unassigned). Do not add a new `TAG`/log unless warning on null — keep consistent with the file's existing style (it already warns on the request event; a warn for an unassigned notify channel is optional and LOW priority).

- [x] **Task 3: Raise `OnLockUnlocked` from `ContainerSystem`**
  - File: `Assets/_Game/Scripts/World/ContainerSystem.cs`
  - Action: Add `[SerializeField] private GameEventSO_String _onLockUnlocked;`. In `HandleContainerOpenRequested`, inside the `if (data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId))` block, after the `HasSkill` check passes (i.e., the code falls through past the early `return`s) and **before** `_containerUI.Open(...)`, raise `_onLockUnlocked?.Raise("Chest");`.
  - Notes: Must NOT fire for non-locked containers. The cleanest placement: set a local `bool wasLocked = data.isLocked && !string.IsNullOrEmpty(data.requiredSkillId);` and, right before `_containerUI.Open(...)`, do `if (wasLocked) _onLockUnlocked?.Raise("Chest");`. `ContainerSystem` does not call `Unlock()` — the act of passing the gate and opening is the "unlocked" moment. (Container lock state is not persisted today — out of scope; this matches existing behavior.)

- [x] **Task 4: Create the `NotificationToast` entry prefab**
  - File: `Assets/_Game/Prefabs/UI/NotificationToast.prefab`
  - Action: Create a UI prefab: root `RectTransform` + `CanvasGroup`, optional child `Image` (semi-transparent background), and a child `TMP_Text` (`TextMeshProUGUI`). Configure the TMP text for a single short line (enable auto-size or a fixed sensible font size, horizontal alignment left/center, `raycastTarget = false` on text and background — toasts are non-interactive). Add a `ContentSizeFitter`/`LayoutElement` as needed so the `VerticalLayoutGroup` lays entries out cleanly.
  - Notes: The controller will set `CanvasGroup.alpha` for fade and write the TMP text. Keep it lightweight — no buttons, no raycast targets.

- [x] **Task 5: Implement `NotificationToastUI` controller**
  - File: `Assets/_Game/Scripts/UI/HUD/NotificationToastUI.cs`
  - Action: New `MonoBehaviour` in namespace `Game.UI`. Mirror `StaminaBarUI`'s shape.
  - Serialized fields:
    - `[SerializeField] private GameEventSO_Int _onXPGained;`
    - `[SerializeField] private GameEventSO_Int _onLevelUp;`
    - `[SerializeField] private GameEventSO_String _onLockUnlocked;`
    - `[SerializeField] private RectTransform _container;` (has a `VerticalLayoutGroup`)
    - `[SerializeField] private GameObject _toastEntryPrefab;` (the Task 4 prefab)
    - `[SerializeField] private int _maxVisible = 5;`
    - `[SerializeField] private float _holdSeconds = 3f;`
    - `[SerializeField] private float _fadeInSeconds = 0.15f;`
    - `[SerializeField] private float _fadeOutSeconds = 0.3f;`
  - Behavior:
    - `Awake`: if `_container == null` or `_toastEntryPrefab == null`, `GameLog.Error(TAG, ...)` and `enabled = false; return;`. Warn (don't disable) if any event channel is null. Cache `WaitForSeconds` for hold (`new WaitForSeconds(_holdSeconds)`).
    - `OnEnable`: `_onXPGained?.AddListener(HandleXPGained);` `_onLevelUp?.AddListener(HandleLevelUp);` `_onLockUnlocked?.AddListener(HandleUnlocked);`
    - `OnDisable`: null-guard then `RemoveListener` each (guard pattern: `if (_onXPGained == null && _onLevelUp == null && _onLockUnlocked == null) return;` or guard each `?.RemoveListener`). Use `?.RemoveListener` to stay safe when `Awake` disabled before `OnEnable`.
    - Handlers: `HandleXPGained(int amount) => Show($"Experience +{amount}");`, `HandleLevelUp(int _) => Show("Level up!");` (payload is the new level but message is fixed), `HandleUnlocked(string noun) => Show($"{noun} unlocked!");`.
    - `private void Show(string message)`: guard `if (!gameObject.activeInHierarchy) return;` (coroutines won't run while inactive). Maintain `private readonly List<GameObject> _active = new();`. If `_active.Count >= _maxVisible`, expire the oldest immediately (stop its coroutine, destroy, remove at index 0). Instantiate `_toastEntryPrefab` under `_container` (`SetParent(_container, false)`), set its `TMP_Text` (`GetComponentInChildren<TMP_Text>()`), set `CanvasGroup.alpha = 0`, add to `_active`, and `StartCoroutine(RunToast(entry))`.
    - `private IEnumerator RunToast(GameObject entry)`: fade `CanvasGroup.alpha` 0→1 over `_fadeInSeconds`; `yield return _holdWait;` (cached); fade 1→0 over `_fadeOutSeconds`; then `_active.Remove(entry); Destroy(entry);`.
  - Notes: `TAG = "[UI]"` only if `GameLog` is actually used (it is, in Awake) — keep it. Don't read state in `Update`. Newest toast appears at the bottom of the vertical list (Unity adds new children last); acceptable for v1. Use `Mathf.Clamp01` while lerping alpha. Coroutines are auto-stopped if the GameObject is disabled — on re-enable the `_active` list may hold destroyed/leftover entries; clearing `_active` and any orphaned children in `OnEnable` is a nice-to-have but not required for v1 (HUD is not toggled at runtime).

- [x] **Task 6: Wire the controller into the UICanvas HUD**
  - File: `Assets/_Game/Prefabs/UI/UICanvas.prefab`
  - Action: Under the `Game` HUD GameObject (UI layer 5), add a child `NotificationContainer` (`RectTransform` anchored where toasts should appear — e.g. top-center or bottom-left) with a `VerticalLayoutGroup` (+ optional `ContentSizeFitter`). Add the `NotificationToastUI` component (on the `Game` object or a dedicated child), assign `_container` = `NotificationContainer`, `_toastEntryPrefab` = `NotificationToast.prefab`, and the three event channel assets (`OnXPGained`, `OnLevelUp`, `OnLockUnlocked`).
  - Notes: Follow the existing HUD wiring (same canvas as HealthBar/Stamina). If editing prefab YAML directly, `refresh_unity(mode="if_dirty")` afterward — never `force` (per root CLAUDE.md). Prefer doing this in the Editor/MCP to avoid GUID hand-wiring.

- [x] **Task 7: Wire the unlock channel onto the producer GameObjects**
  - File: scene/prefab carrying `DoorSystem` and `ContainerSystem` (player-side systems; check `Core.unity` / Player area where the existing request-event channels are wired).
  - Action: Assign the new `OnLockUnlocked` asset to the `_onLockUnlocked` field on both the `DoorSystem` and `ContainerSystem` components.
  - Notes: Same object(s) that already have `_onDoorOpenRequested` / `_onContainerOpenRequested` assigned.

- [x] **Task 8: Document the new HUD component**
  - File: `Assets/_Game/Scripts/UI/HUD/CLAUDE.md`
  - Action: Add `NotificationToastUI` to the Scripts table and `OnLockUnlocked` (`GameEventSO_String`, raised by `DoorSystem`/`ContainerSystem`) to the Event Channels list. Note the "Chest unlocked!" gating quirk (fires only on locked-container success path).
  - Notes: Keep it terse, matching the existing table style.

### Acceptance Criteria

- [ ] **AC1 (XP toast):** Given the HUD is active and `OnXPGained` is wired, when an enemy is killed and `XPSystem.GiveExperience(50)` raises `OnXPGained`, then a toast reading `Experience +50` appears, holds ~3s, and fades out.
- [ ] **AC2 (Level-up toast):** Given the player gains enough XP to cross a level threshold, when `LevelSystem` raises `OnLevelUp`, then a toast reading `Level up!` appears (independent of the XP toast that fired the same frame).
- [ ] **AC3 (Door unlock toast):** Given a locked door whose required skill the player has, when the player interacts and `DoorSystem` unlocks+opens it, then a toast reading `Door unlocked!` appears exactly once.
- [ ] **AC4 (Chest unlock toast):** Given a locked container whose required skill the player has, when the player opens it and the skill gate passes, then a toast reading `Chest unlocked!` appears exactly once — and Given an **un**locked container, when opened, then NO unlock toast appears.
- [ ] **AC5 (FIFO order):** Given several events fire in sequence, when their toasts are created, then they appear in the order the events were raised (first raised is the oldest/top of the stack).
- [ ] **AC6 (Cap at 5 + eviction):** Given 5 toasts are already visible, when a 6th event fires, then the oldest toast is removed immediately and the new toast is shown, keeping at most 5 on screen.
- [ ] **AC7 (Hold + fade):** Given a toast is shown, when `_holdSeconds` (default 3) elapse, then it fades out over `_fadeOutSeconds` and is destroyed (removed from the active list, no leaked GameObjects).
- [ ] **AC8 (Graceful missing wiring):** Given any of `_onXPGained` / `_onLevelUp` / `_onLockUnlocked` is unassigned, when the game runs, then `NotificationToastUI` logs a warning for that channel and continues without errors (no `NullReferenceException`); given `_container` or `_toastEntryPrefab` is unassigned, then `Awake` logs an error and disables the component.
- [ ] **AC9 (No console violations):** Given the feature is implemented, then no direct `Debug.Log*` calls exist (only `GameLog`), no direct `Cursor.*` calls, and toasts are non-interactive (`raycastTarget = false`) so they never block clicks on other UI.

## Additional Context

### Dependencies

- **No external libraries.** Uses only existing project infra: `GameEventSO`/`GameEventSO_Int`/`GameEventSO_String` (`Assets/_Game/ScriptableObjects/Events/`), TextMeshPro (already in project), uGUI `VerticalLayoutGroup`/`CanvasGroup`.
- **Existing producers already raise** `OnXPGained` (`XPSystem`) and `OnLevelUp` (`LevelSystem`) — no changes needed there.
- **Task ordering:** Task 1 (asset) precedes Tasks 2/3 (which serialize-reference it) and Task 5 (controller references it); Task 4 (entry prefab) precedes Task 6 (wiring). Tasks 2/3 (code) can land before their scene wiring (Task 7) — the `?.Raise` is null-safe until wired.

### Testing Strategy

- **Manual Play-mode (primary — no test infra exists):**
  1. Enter Play, kill an enemy → confirm `Experience +N` toast (AC1).
  2. Grind kills to a level threshold → confirm `Level up!` toast appears alongside/after the XP toast (AC2).
  3. Interact with a locked door the player can open → `Door unlocked!` once (AC3).
  4. Open a locked container the player can open → `Chest unlocked!` once; open an unlocked container → no toast (AC4).
  5. Trigger 6+ events rapidly (e.g. multiple quick kills) → confirm max 5 visible, oldest evicted, FIFO order (AC5, AC6).
  6. Watch a single toast → confirm ~3s hold then fade-out and no lingering GameObject in the Hierarchy under `NotificationContainer` (AC7).
  7. Temporarily clear a serialized channel → confirm warning (not exception) on play; clear `_container` → confirm error + component disabled (AC8).
- **Optional unit test (future):** if message formatting is extracted to a `static string Format*(...)` helper, an EditMode test could assert `"Experience +50"`, `"Level up!"`, `"Door unlocked!"`, `"Chest unlocked!"`. Not required for this spec (no `Assets/_Game/Tests/` exists yet).

### Notes

- **User-confirmed decisions (Step 1):** subscribe-per-event architecture; UICanvas HUD location; stacked list capped at 5; 3s hold with fade-out.
- **Pre-mortem risks:**
  - *Leaked toast GameObjects* if a coroutine is interrupted (HUD disabled mid-fade). Mitigation: HUD isn't toggled at runtime in v1; if it ever is, clear `_active` + destroy orphans in `OnEnable`.
  - *Layout jank* from `VerticalLayoutGroup` rebuilds on every instantiate/destroy. Acceptable at ≤5 entries (UI CLAUDE.md). If it stutters, pool entries instead of destroy/instantiate.
  - *Chest false-positive* if the unlock raise is placed outside the locked-branch — Task 3 explicitly gates on `wasLocked`.
  - *Double Door toast* if a future change makes `DoorInteractable` route unlocked doors through `DoorSystem` too — currently safe (only locked doors arrive).
- **Future considerations (out of scope):** per-type icons/colors, notification sound, a persistent notification/history panel, and additional sources (gold gained, item picked up, quest started/completed, skill learned) — all addable by subscribing to their existing channels in `NotificationToastUI` without structural change.

## Review Notes

- Adversarial review completed (Step 5) — 6 findings.
- Findings: 6 total, 3 fixed, 3 acknowledged/skipped.
- Resolution approach: auto-fix (real findings only).

**Fixed:**
- **F1 (Medium) — "Chest unlocked!" re-fired on every open.** `ContainerSystem` never unlocked the container, so a locked chest re-passed the gate (and re-toasted) on every interaction. Fixed by mirroring the door pattern: `ContainerOpenRequestData` now carries a runtime `ContainerInteractable container` ref; `ContainerInteractable.Interact()` sets `container = this` and exposes `Unlock()` (delegates to the sibling `Lockable`); `ContainerSystem` calls `data.container?.Unlock()` on a passing skill check before raising the toast. The chest is now permanently unlocked, so AC4's "exactly once" holds. **Supersedes** the old Task 3 note and Decision bullet stating "ContainerSystem does not call `Unlock()`". (Runtime-only unlock; cross-session persistence is still deferred to the lockable-persistence stub.)
- **F2 (Low) — `_maxVisible <= 0` threw `IndexOutOfRangeException`** in the FIFO eviction loop (`_active[0]` on an empty list). Fixed by clamping `_maxVisible` to a minimum of 1 in `Awake`.
- **F3 (Low) — toast fades/hold froze at `timeScale = 0`.** Switched fade lerps to `Time.unscaledDeltaTime` and the hold to `WaitForSecondsRealtime`, so HUD toasts still animate and expire while the game is paused.

**Acknowledged / not changed:**
- **F4 (Low, intentional) — AC8 deviation:** `_container` auto-defaults to `transform as RectTransform` when unassigned (the controller lives on the container by convention). Deliberate — avoids fragile self-wiring that MCP couldn't set. Still errors + disables if there is no RectTransform.
- **F5 (Low) — no visual positioning:** container/toast use default centered RectTransform anchors. Editor-tuning task (anchor the `NotificationContainer` to the desired HUD corner); not a code issue.
- **F6 (Info) — `DoorSystem` doesn't re-check `data.isLocked` before raising "Door":** by design — only locked doors are routed to `DoorSystem` (unlocked doors toggle locally in `DoorInteractable`).
