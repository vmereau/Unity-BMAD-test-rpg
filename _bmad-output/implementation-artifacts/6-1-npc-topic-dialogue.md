# Story 6.1: NPC Topic Dialogue

Status: done

## Story

As a player,
I want to talk to NPCs and see a list of conversation topics,
so that I can learn about the world through Gothic-style topic-based dialogue.

## Acceptance Criteria

1. Pressing E on an NPC (via InteractionSystem look-at) opens a dialogue panel showing the NPC's name and a list of topic buttons.
2. Topics are sourced from `NPCMemoryComponent.GetActiveMemories()` — each active memory's `memoryId` appears as a topic button label.
3. Clicking a topic button displays the first `effects.dialogueLines[0]` of that memory as the NPC's response text.
4. A built-in "Farewell." button is always present at the bottom of the topic list and closes the dialogue.
5. Pressing Escape closes the dialogue panel.
6. While the dialogue panel is open, the cursor is unlocked (free) and the InteractionSystem cannot fire a new `Interact()` call.
7. When the dialogue closes, the cursor is re-locked and the InteractionSystem resumes normally.
8. If an NPC has no active memories (empty topic list), only the "Farewell." button appears — no crash, no null ref.
9. A demo NPC exists in StartingTown with 2-3 authored `NPCMemoryEntrySO` assets (trivially unlocked, empty `unlockConditions`) that prove the topic flow works end-to-end.

## Tasks / Subtasks

- [x] Task 1 — Create event channel data types (AC: #1, #6)
  - [x] Create `NPCDialogueRequestData` struct in `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs`
  - [x] Create `GameEventSO_NPCDialogueRequest` in `Assets/_Game/ScriptableObjects/Events/GameEventSO_NPCDialogueRequest.cs`
  - [x] Create asset instance `Assets/_Game/Data/Events/OnNPCDialogueRequested.asset`

- [x] Task 2 — Create `DialogueSystem.cs` (AC: #1, #6, #7)
  - [x] MonoBehaviour in `Assets/_Game/Scripts/World/DialogueSystem.cs`, namespace `Game.World`
  - [x] Subscribe to `_onDialogueRequested` event in `OnEnable`, unsubscribe in `OnDisable`
  - [x] On receive: call `_dialogueUI.Open(data.npcName, data.memories.GetActiveMemories())`; set `IsOpen = true`; call `CursorManager.Unlock()`
  - [x] Expose `public bool IsOpen { get; private set; }` and `public void Close()` method
  - [x] `Close()`: call `_dialogueUI.Close()`; set `IsOpen = false`; call `CursorManager.Lock()`

- [x] Task 3 — Create `DialogueUI.cs` (AC: #1, #2, #3, #4, #5, #8)
  - [x] MonoBehaviour in `Assets/_Game/Scripts/UI/DialogueUI.cs`, namespace `Game.UI`
  - [x] `Open(string npcName, NPCMemoryEntrySO[] topics)` — activates panel, populates buttons, shows NPC name
  - [x] `Close()` — deactivates panel, destroys spawned topic buttons, clears response text
  - [x] Dynamically instantiate `_topicButtonPrefab` per topic; wire `button.onClick` to show `effects.dialogueLines[0]`
  - [x] Always append one final "Farewell." button that calls `_dialogueSystem.Close()`
  - [x] Subscribe `_input.UI.Cancel.performed` → `_dialogueSystem.Close()` in `OnEnable`, unsubscribe in `OnDisable`
  - [x] Dispose `_input` in `OnDestroy` (not `OnDisable`)
  - [x] Handle empty `dialogueLines` array gracefully (show `"..."` fallback)

- [x] Task 4 — Create DialoguePanel prefab (AC: #1)
  - [x] Canvas: Screen Space Overlay, sortingOrder=10 (above HUD), `GraphicRaycaster` enabled
  - [x] Background: semi-transparent `Image` (`Color(0,0,0,0.75)`)
  - [x] NPC name: `TMP_Text` at top
  - [x] Topics scroll area: `ScrollRect` with `VerticalLayoutGroup` content — topic buttons added here at runtime
  - [x] Response text: `TMP_Text` (right or bottom section)
  - [x] Wire all `[SerializeField]` fields in DialogueUI
  - [x] Save as `Assets/_Game/Prefabs/UI/DialoguePanel.prefab`

- [x] Task 5 — Create TopicButton prefab (AC: #1, #4)
  - [x] Simple `Button` with `TMP_Text` child for topic label
  - [x] Save as `Assets/_Game/Prefabs/UI/TopicButtonPrefab.prefab`

- [x] Task 6 — Modify `NPCPresence.Interact()` (AC: #1)
  - [x] Add `[SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested`
  - [x] In `Interact()`: get `NPCMemoryComponent` via `GetComponent<NPCMemoryComponent>()` (nullable); raise event with `new NPCDialogueRequestData { npcName = _data.npcName, memories = memComponent }`
  - [x] If no `NPCMemoryComponent`, raise with `memories = null` — `DialogueSystem` handles the null case

- [x] Task 7 — Modify `InteractionSystem.LateUpdate()` (AC: #6)
  - [x] Add `[SerializeField] private DialogueSystem _dialogueSystem`
  - [x] In `LateUpdate()`, before calling `CurrentInteractable.Interact()`: add guard `if (_dialogueSystem != null && _dialogueSystem.IsOpen) return;`

- [x] Task 8 — Wire scene objects (AC: #1)
  - [x] Add `DialogueSystem` component to the **Player root** in `Player.prefab` (not a separate scene GO)
  - [x] Add `DialoguePanel.prefab` as a **nested PrefabInstance inside `UICanvas.prefab`** (independent of Player hierarchy)
  - [x] Add `EventSystem` + `InputSystemUIInputModule` as a **second root object in `UICanvas.prefab`** (ensures pointer events work whenever UICanvas is present)
  - [x] Assign `_onDialogueRequested` asset to `DialogueSystem`, `InteractionSystem`, and all NPC prefabs using this system
  - [x] Wire `_dialogueSystem` on `InteractionSystem` → `DialogueSystem` (both on Player root, wired via `Player.prefab` serialized field)
  - [x] Wire `_dialogueUI` on `DialogueSystem` → `DialogueUI` inside `UICanvas.prefab`'s `DialoguePanel` (set as a Player.prefab nested-prefab override)
  - [x] Wire `_dialogueSystem` on `DialogueUI` → `DialogueSystem` on Player (set as Player.prefab nested-prefab override targeting UICanvas → DialoguePanel)

- [x] Task 9 — Author demo NPC and memory SOs (AC: #9)
  - [x] Create `NPCDataSO` at `Assets/_Game/Data/NPCs/NPC_Villager.asset`
  - [x] Create 3 `NPCMemoryEntrySO` in `Assets/_Game/Data/NPCs/Memories/`:
    - `Mem_Villager_Default.asset` — `memoryId: "Greetings"`, empty `unlockConditions`, `dialogueLines: ["Good day, traveller. These are troubled times."]`
    - `Mem_Villager_Town.asset` — `memoryId: "About this place"`, empty `unlockConditions`, `dialogueLines: ["This is Alderath, a quiet town — or it was, before the troubles began."]`
    - `Mem_Villager_Work.asset` — `memoryId: "What do you do?"`, empty `unlockConditions`, `dialogueLines: ["I tend the fields. Not much else left to do around here."]`
  - [x] Place an NPC in StartingTown scene: duplicate `NPC_Base.prefab`, assign `NPCDataSO`, add `NPCMemoryComponent`, assign 3 memory SOs + `OnWorldFactChanged.asset` to `NPCMemoryComponent`, assign `_onDialogueRequested` to `NPCPresence`

## Dev Notes

### Architecture Overview

```
NPCPresence.Interact()
  → raises GameEventSO_NPCDialogueRequest (OnNPCDialogueRequested.asset)
      ↓
  DialogueSystem  ← on Player root (Player.prefab)
  - calls CursorManager.Unlock()
  - sets IsOpen = true
  - calls DialogueUI.Open(npcName, memories.GetActiveMemories())
      ↓
  DialogueUI  ← inside DialoguePanel.prefab, nested in UICanvas.prefab
  - instantiates TopicButtonPrefab per active memory
  - appends "Farewell." button → DialogueSystem.Close()
  - subscribes UI.Cancel → DialogueSystem.Close()
      ↓
  DialogueSystem.Close()
  - calls DialogueUI.Close()
  - sets IsOpen = false
  - calls CursorManager.Lock()

InteractionSystem.LateUpdate()  ← on Player root (Player.prefab)
  - checks: if (_dialogueSystem != null && _dialogueSystem.IsOpen) return;
```

### Prefab Placement (post-refactor)

| Component | Location |
|-----------|----------|
| `DialogueSystem` | Player root GameObject in `Player.prefab` |
| `DialoguePanel` (DialogueUI) | Nested `PrefabInstance` inside `UICanvas.prefab` |
| `EventSystem` + `InputSystemUIInputModule` | Child of UICanvas in `UICanvas.prefab` (plain Transform, not RectTransform) |
| `InteractionSystem._dialogueSystem` | Wired in `Player.prefab` (both components on Player root) |
| `DialogueUI._dialogueSystem` | Set via Player.prefab nested-prefab override (UICanvas → DialoguePanel) |
| `DialogueSystem._dialogueUI` | Set via Player.prefab nested-prefab override (UICanvas → DialoguePanel) |

### Existing Systems to Reuse (Do NOT Reinvent)

| System | Location | Usage |
|--------|----------|-------|
| `NPCMemoryComponent.GetActiveMemories()` | `Assets/_Game/Scripts/AI/NPCMemoryComponent.cs` | Returns `NPCMemoryEntrySO[]` of active memories — call this when opening dialogue |
| `NPCMemoryEntrySO.memoryId` | `Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs` | Topic display name in the button list |
| `NPCMemoryEntrySO.effects.dialogueLines` | same | NPC's response text; use `[0]`; guard empty array with `"..."` fallback |
| `CursorManager.Unlock()` / `Lock()` | `Assets/_Game/Scripts/Core/CursorManager.cs` | MANDATORY — never call `Cursor.lockState` directly |
| `GameEventSO<T>` | `Assets/_Game/ScriptableObjects/Events/GameEventSO.cs` | Base for new event type — follow `GameEventSO_String.cs` as the model for file structure |
| `GameEventSO_WorldFact` + `WorldFactData` | `Assets/_Game/ScriptableObjects/Events/` | Reference model for creating `GameEventSO_NPCDialogueRequest` + `NPCDialogueRequestData` |
| `NPCPresence` | `Assets/_Game/Scripts/AI/NPCPresence.cs` | **Modify** `Interact()` — has placeholder comment for Epic 6; currently logs "is busy" |
| `InteractionSystem` | `Assets/_Game/Scripts/World/InteractionSystem.cs` | **Modify** `LateUpdate()` — add dialogue guard before `CurrentInteractable.Interact()` |
| `UIScreenManager` | `Assets/_Game/Scripts/UI/UIScreenManager.cs` | Reference model for cursor + input pattern — DialogueUI is NOT part of the tab system |

### New Files to Create

| File | Namespace | Type |
|------|-----------|------|
| `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs` | `Game.Core` | struct |
| `Assets/_Game/ScriptableObjects/Events/GameEventSO_NPCDialogueRequest.cs` | `Game.Core` | SO subclass |
| `Assets/_Game/Scripts/World/DialogueSystem.cs` | `Game.World` | MonoBehaviour |
| `Assets/_Game/Scripts/UI/DialogueUI.cs` | `Game.UI` | MonoBehaviour |
| `Assets/_Game/Prefabs/UI/DialoguePanel.prefab` | — | Canvas prefab |
| `Assets/_Game/Prefabs/UI/TopicButtonPrefab.prefab` | — | Button prefab |
| `Assets/_Game/Data/Events/OnNPCDialogueRequested.asset` | — | GameEventSO_NPCDialogueRequest instance |
| `Assets/_Game/Data/NPCs/NPC_Villager.asset` | — | NPCDataSO |
| `Assets/_Game/Data/NPCs/Memories/Mem_Villager_*.asset` (×3) | — | NPCMemoryEntrySO |

### Critical Patterns

**GameEventSO concrete types — SEPARATE FILES (project memory)**
`NPCDialogueRequestData` struct and `GameEventSO_NPCDialogueRequest` class must be in **separate `.cs` files**. Unity's `m_Script` reference breaks on domain reload if they share a file (previously burned on this). The struct goes in `NPCDialogueRequestData.cs`; the SO subclass goes in `GameEventSO_NPCDialogueRequest.cs`.

**Singleton rule — DialogueSystem is NOT a singleton**
Project rule: only `WorldStateManager` and `SaveSystem` use the singleton + `DontDestroyOnLoad` pattern. `DialogueSystem` is a scene-local MonoBehaviour. Inter-system communication uses the `GameEventSO` event channel for NPC→Dialogue, and a direct `[SerializeField]` reference from `InteractionSystem` to `DialogueSystem` for the `IsOpen` guard.

**OnDisable null guard**
`DialogueUI` creates an `InputSystem_Actions` instance. If `Awake` can set `enabled = false`, `OnDisable` must guard `if (_input == null) return;`. For DialogueUI, `Awake` should not disable itself, so this guard may be omitted — but add it as defensive code if any Inspector null-check disables the component.

**Input subscribe/unsubscribe in OnEnable/OnDisable**
`DialogueUI` subscribes `_input.UI.Cancel.performed` in `OnEnable`, unsubscribes in `OnDisable`. Dispose in `OnDestroy`. Same pattern as `UIScreenManager.cs`.

**IInteractable.Interact() signature**
Interface signature is `void Interact()` — NO parameters. Never `Interact(GameObject interactor)`. Confirmed in `IInteractable.cs`.

**TMP_Text everywhere**
Use `TMPro.TMP_Text` for all text fields, never `UnityEngine.UI.Text`.

**MCP Canvas quirk**
If creating the Canvas prefab via `manage_gameobject(create)`, the Canvas defaults to `renderMode = 2` (World Space). Immediately follow up with `manage_components set_property renderMode 0` to set Screen Space Overlay.

**Logging**
Always `GameLog.Info(TAG, ...)` — never `Debug.Log`. Define `private const string TAG = "[Dialogue]"` in `DialogueSystem` and `DialogueUI`.

### NPCDialogueRequestData struct

```csharp
// File: Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs
using Game.AI;

namespace Game.Core
{
    [System.Serializable]
    public struct NPCDialogueRequestData
    {
        public string npcName;
        public NPCMemoryComponent memories; // null-safe — DialogueSystem guards
    }
}
```

### GameEventSO_NPCDialogueRequest

```csharp
// File: Assets/_Game/ScriptableObjects/Events/GameEventSO_NPCDialogueRequest.cs
using UnityEngine;

namespace Game.Core
{
    [CreateAssetMenu(menuName = "Game/Events/NPC Dialogue Request", fileName = "NewNPCDialogueRequestEvent")]
    public class GameEventSO_NPCDialogueRequest : GameEventSO<NPCDialogueRequestData> { }
}
```

### NPCPresence.Interact() — modified section

```csharp
[SerializeField] private GameEventSO_NPCDialogueRequest _onDialogueRequested;

public void Interact()
{
    if (_data == null) return;
    if (_onDialogueRequested == null)
    {
        GameLog.Warn(TAG, $"No dialogue event assigned on {gameObject.name} — cannot open dialogue");
        return;
    }
    var memComponent = GetComponent<NPCMemoryComponent>(); // may be null — handled by DialogueSystem
    _onDialogueRequested.Raise(new NPCDialogueRequestData
    {
        npcName = _data.npcName,
        memories = memComponent
    });
}
```

### InteractionSystem — LateUpdate guard

```csharp
[SerializeField] private DialogueSystem _dialogueSystem; // optional — assign in Inspector

private void LateUpdate()
{
    if (_dialogueSystem != null && _dialogueSystem.IsOpen) return; // new line
    if (CurrentInteractable != null && _input.Player.Interact.WasPressedThisFrame())
        CurrentInteractable.Interact();
}
```

### Project Structure Notes

- `DialogueSystem.cs` → `Assets/_Game/Scripts/World/` (scene interaction systems live here: `InteractionSystem.cs`, `PersistentID.cs`)
- `DialogueUI.cs` → `Assets/_Game/Scripts/UI/` (all UI scripts live here)
- Event assets → `Assets/_Game/Data/Events/` (alongside `OnWorldFactChanged.asset`)
- Memory SO assets → `Assets/_Game/Data/NPCs/Memories/` (per tech-spec convention `Mem_{NpcName}_{EventSlug}.asset`)
- Prefabs → `Assets/_Game/Prefabs/UI/` (for panel and button prefabs)

### References

- Epic 6 story definition: [Source: _bmad-output/epics.md#Epic 6: Quest & Dialogue]
- NPC Memory system full API: [Source: _bmad-output/implementation-artifacts/tech-spec-game-world-state-npc-memory.md]
- NPCMemoryEntrySO schema: [Source: Assets/_Game/ScriptableObjects/NPC/NPCMemoryEntrySO.cs]
- NPCMemoryComponent API: [Source: Assets/_Game/Scripts/AI/NPCMemoryComponent.cs]
- NPCPresence placeholder: [Source: Assets/_Game/Scripts/AI/NPCPresence.cs#Interact()]
- InteractionSystem LateUpdate: [Source: Assets/_Game/Scripts/World/InteractionSystem.cs#LateUpdate()]
- UIScreenManager (input/cursor pattern): [Source: Assets/_Game/Scripts/UI/UIScreenManager.cs]
- GameEventSO model: [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO.cs]
- GameEventSO_String model (two-file pattern): [Source: Assets/_Game/ScriptableObjects/Events/GameEventSO_String.cs]
- Singleton rule: [Source: _bmad-output/project-context.md — "Singletons: Only WorldStateManager and SaveSystem"]
- GameEventSO single-file memory: [Source: memory — GameEventSO concrete types must be in separate files]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- MCP validator falsely detects method *calls* as duplicate signatures — used Write tool directly for DialogueUI.cs as workaround.
- `manage_asset(action="move")` quirk noted: used mkdir + refresh_unity to register new Memories subfolder with asset DB.
- Canvas created via `manage_gameobject` defaults to renderMode=2 (World Space); immediately followed up with `set_property renderMode 0`.
- Scene instance IDs change on reload; always re-query by name after scene reload.

### Completion Notes List

- **Task 1**: `NPCDialogueRequestData` struct + `GameEventSO_NPCDialogueRequest` SO subclass created in separate files (per project memory). `OnNPCDialogueRequested.asset` created in `Assets/_Game/Data/Events/`.
- **Task 2**: `DialogueSystem.cs` implements event subscription pattern, `IsOpen` property, and `Close()` — all cursor management via `CursorManager`. Placed as child GO `Player/DialogueSystem` in Core.unity.
- **Task 3**: `DialogueUI.cs` dynamically spawns TopicButton per active memory, always appends Farewell, handles empty dialogueLines with "..." fallback, subscribes UI.Cancel in OnEnable/unsubscribes in OnDisable, disposes in OnDestroy.
- **Task 4**: `DialoguePanel.prefab` — Canvas (Screen Space Overlay, sortingOrder=10), Background Image (0,0,0,0.75), NPCNameText, TopicsScrollView+TopicsContent (VerticalLayoutGroup), ResponseText. DialogueUI fully wired.
- **Task 5**: `TopicButtonPrefab.prefab` — Button + Image + Label (TMP_Text child).
- **Task 6**: `NPCPresence.Interact()` now raises `OnNPCDialogueRequested` event with npcName + nullable NPCMemoryComponent. Old placeholder log removed.
- **Task 7**: `InteractionSystem.LateUpdate()` now guards against dialogue-open state before calling `CurrentInteractable.Interact()`.
- **Task 8 (post-refactor)**: `DialogueSystem` moved to Player root in `Player.prefab` (no longer a scene-level GO). `DialoguePanel.prefab` nested directly inside `UICanvas.prefab` (independent of Player hierarchy). `EventSystem` + `InputSystemUIInputModule` added as a second root in `UICanvas.prefab`. All cross-references wired as Player.prefab nested-prefab overrides — no scene-level wiring required. `InteractionSystem._dialogueSystem` wired via Player.prefab serialized field.
- **Task 9**: NPC_Villager.asset + 3 NPCMemoryEntrySO assets created. Demo NPC `StartingTown_NPC_Villager` placed in StartingTown at (5,0,5) with NPCMemoryComponent + all 3 memories + OnWorldFactChanged + persistent GUID.

### File List

- `Assets/_Game/ScriptableObjects/Events/NPCDialogueRequestData.cs` (new)
- `Assets/_Game/ScriptableObjects/Events/GameEventSO_NPCDialogueRequest.cs` (new)
- `Assets/_Game/Scripts/World/DialogueSystem.cs` (new)
- `Assets/_Game/Scripts/UI/DialogueUI.cs` (new)
- `Assets/_Game/Prefabs/UI/DialoguePanel.prefab` (new)
- `Assets/_Game/Prefabs/UI/TopicButtonPrefab.prefab` (new)
- `Assets/_Game/Data/Events/OnNPCDialogueRequested.asset` (new)
- `Assets/_Game/Data/NPCs/NPC_Villager.asset` (new)
- `Assets/_Game/Data/NPCs/Memories/Mem_Villager_Default.asset` (new)
- `Assets/_Game/Data/NPCs/Memories/Mem_Villager_Town.asset` (new)
- `Assets/_Game/Data/NPCs/Memories/Mem_Villager_Work.asset` (new)
- `Assets/_Game/Scripts/AI/NPCPresence.cs` (modified — added event raise in Interact())
- `Assets/_Game/Scripts/World/InteractionSystem.cs` (modified — added dialogue guard in LateUpdate())
- `Assets/_Game/Prefabs/UI/UICanvas.prefab` (modified — DialoguePanel.prefab nested inside; EventSystem+InputSystemUIInputModule added as second root; CanvasScaler removed; UICanvas RectTransform anchors/pivot/scale corrected)
- `Assets/_Game/Scenes/Core.unity` (modified — Player prefab instance with dialogue wiring; UICanvas scale and _dialogueSystem scene overrides removed)
- `Assets/_Game/Scenes/StartingTown.unity` (modified — Villager NPC + OnNPCDialogueRequested on all NPCs)

## Change Log

- 2026-04-04: Initial implementation complete — Gothic-style topic dialogue system with EventSO channels, DialogueSystem + DialogueUI scripts, DialoguePanel + TopicButton prefabs, NPC wiring, and Villager demo NPC with 3 authored memories.
