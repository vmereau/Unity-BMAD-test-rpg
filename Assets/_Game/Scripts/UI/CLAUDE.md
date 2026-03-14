# CLAUDE.md — Assets/_Game/Scripts/UI

> Loaded when Claude accesses files in this folder. Covers Unity UI patterns, best practices, and pitfalls for this project.

---

## Canvas Setup

- **Screen Space - Overlay** (renderMode = 0) for HUD and menus. World Space only for diegetic in-world UI.
- One Canvas per logical layer (HUD, Menus, Tooltips) with different `sortingOrder`.
- `CanvasScaler` set to `Scale With Screen Size` at 1920×1080 reference resolution.
- Add `GraphicRaycaster` only on Canvases that need pointer events — every raycaster costs CPU per frame.

**MCP Quirk:** `manage_gameobject(create)` always creates Canvas with `renderMode = 2` (World Space). Always follow up with `manage_components set_property renderMode 0`.

---

## Cursor Management (HIGH — project rule)

- **NEVER** call `Cursor.lockState`, `Cursor.visible`, or `CursorLockMode` directly in UI scripts.
- Always use `CursorManager.Lock()` / `CursorManager.Unlock()` / `CursorManager.IsLocked`.
- Pattern: panel `Open()` → `CursorManager.Unlock()`, panel `Close()` → `CursorManager.Lock()`.

---

## Input Handling

- Subscribe to `InputSystem_Actions` callbacks in `OnEnable`, unsubscribe in `OnDisable`.
- Dispose in `OnDestroy` (not `OnDisable`) so input survives disable/re-enable cycles.
- **Mandatory OnDisable null guard** when `Awake` can set `enabled = false`:

```csharp
private void OnDisable()
{
    if (_input == null) return; // Guard: Awake may disable before OnEnable runs
    _input.UI.Disable();
    _input.Player.Disable();
    _input.Dispose();
}
```

---

## Layout & Rebuild Performance

- Avoid changing `RectTransform`, layout groups, or text every frame — triggers expensive Canvas vertex rebuilds.
- Separate static and dynamic elements into **separate Canvases** — Unity rebuilds the full Canvas buffer when any child changes.
- Prefer `SetActive(false)` on panels over destroy/recreate when content doesn't change.
- Destroying and recreating slots on every refresh (`RefreshSlots()` pattern) is fine for small counts; pool or update in-place for large inventories to avoid GC spikes.

---

## Drag & Drop

- Implement `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, `IDropHandler` on the draggable element.
- Create a **ghost image** parented to the root Canvas (`SetAsLastSibling()`) with `raycastTarget = false` — without this the ghost blocks pointer events to drop targets.
- Destroy the ghost in both `OnEndDrag` (source) AND when `OnDrop` fires on the target. Always null-guard before `Destroy`.

---

## Pointer Events

- Use `IPointerEnterHandler` / `IPointerExitHandler` for hover — cheaper than polling mouse position.
- `IPointerClickHandler` provides `PointerEventData.InputButton` to distinguish left/right/middle.
- `GetComponentInParent<T>()` from a child slot is acceptable for same-system child→parent calls, but cache it in `Awake` if called frequently.

---

## Text

- Always use **TextMeshPro** (`TMP_Text`) — never `UnityEngine.UI.Text`.
- Avoid runtime string concatenation per frame — use `StringBuilder`.
- Toggle visibility with `gameObject.SetActive(false/true)` rather than `text = ""` (empty string still triggers a mesh rebuild).

---

## Event System Integration

- UI panels reacting to game state (health bar, quest tracker) must subscribe to `GameEventSO<T>` channels in `OnEnable`/`OnDisable` — never poll state in `Update`.
- Never subscribe in `Start` — use `OnEnable` so no events are missed on first activation.

---

## Code Review Checklist — UI Scripts

| Severity | Pattern |
|----------|---------|
| HIGH | `Cursor.lockState` / `Cursor.visible` used directly — must go through `CursorManager` |
| HIGH | Missing `OnDisable` null guard when `Awake` can set `enabled = false` |
| HIGH | Drag ghost missing `raycastTarget = false` — blocks drop targets |
| MEDIUM | `GetComponent` / `GetComponentInParent` called in `Update` — cache in `Awake` |
| MEDIUM | Canvas created via MCP defaulting to World Space (renderMode = 2) |
| MEDIUM | Slots destroyed/recreated on every refresh at scale — pool or update in-place |
| MEDIUM | `Debug.Log` in UI handlers — use `GameLog.Info(TAG, ...)` |
| LOW | Dynamic UI not isolated in its own Canvas — causes full Canvas rebuilds |
| LOW | `new WaitForSeconds()` inside UI coroutines (fade-in/out) — cache instances |
