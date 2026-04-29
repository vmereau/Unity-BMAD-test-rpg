---
title: 'Name Range & Interaction Prompt Polish'
slug: 'name-range-interaction-prompt-polish'
created: '2026-04-28'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6', 'C#', 'URP', 'IMGUI (OnGUI)']
files_to_modify:
  - 'Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs'
  - 'Assets/_Game/Scripts/World/InteractionSystem.cs'
  - 'Assets/_Game/Scripts/Inventory/ItemPickup.cs'
code_patterns:
  - 'SphereCastNonAlloc with buffer array'
  - 'OnGUI + Camera.WorldToScreenPoint for world-to-screen labels'
  - 'IInteractable implemented by ItemPickup and NPCPresence'
test_patterns: []
---

# Tech-Spec: Name Range & Interaction Prompt Polish

**Created:** 2026-04-28

---

## Overview

### Problem Statement

The interaction system uses a single `interactionRange` for both detecting interactables and showing their names. There is no wider detection radius to show entity name tags above their world-space position, and `ItemPickup.InteractPrompt` returns a verbose "Press E to pick up {name}" string rather than a clean action verb.

### Solution

Add a `nameRange` float to `InteractionConfigSO` (larger than `interactionRange`). In `InteractionSystem`, run a second sphere cast at `nameRange` each scan tick and render each detected interactable's `NameTag` above its collider in world space using `OnGUI` + `Camera.WorldToScreenPoint`. Simplify `ItemPickup.InteractPrompt` to return `"Pick Up"`.

### Scope

**In Scope:**
- Add `nameRange` field to `InteractionConfigSO`
- Second sphere cast in `InteractionSystem.Update` using `nameRange`
- World-space name tag rendering in `InteractionSystem.OnGUI` via `Camera.WorldToScreenPoint`
- Only show name tags for interactables where `NameTag` is non-empty
- Simplify `ItemPickup.InteractPrompt` → `"Pick Up"`
- Update `OnDrawGizmos` to visualize `nameRange`

**Out of Scope:**
- New UI Canvas / TextMeshPro components on prefabs
- Fading, billboarding, or occlusion culling for name tags
- Changes to `NPCPresence.InteractPrompt` (already returns `"Talk"`)
- Changes to `InteractableObject` (generic, uses serialized `_promptText`)

---

## Context for Development

### Codebase Patterns

- `InteractionConfigSO` is a plain `ScriptableObject` with public fields, no properties. New field follows the same pattern.
- `InteractionSystem` uses `Physics.SphereCastNonAlloc` with a pre-allocated `RaycastHit[16]` buffer. A separate buffer must be declared for the name range cast.
- `OnGUI` is already used in `InteractionSystem` for the interact prompt label. Name tags follow the same `GUI.Label` + `GUIStyle` pattern, using `Camera.WorldToScreenPoint` to convert world positions to screen space. **Y must be flipped**: `guiY = Screen.height - screenPos.z` is wrong; correct is `guiY = Screen.height - screenPos.y`.
- `IInteractable` already declares `string NameTag { get; }`. `ItemPickup.NameTag` returns `_item?.itemName ?? "item"`. `NPCPresence.NameTag` returns `_data.entityName`. Both are already implemented.
- `InteractionSystem` uses a `scanInterval` timer — both sphere casts run in the same timed tick.
- `GameLog` must be used instead of `Debug.Log`.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` | Add `nameRange` field |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Add second cast + name tag rendering |
| `Assets/_Game/Scripts/Inventory/ItemPickup.cs` | Simplify `InteractPrompt` |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Interface reference (no changes needed) |
| `Assets/_Game/Scripts/AI/NPCPresence.cs` | Reference — `InteractPrompt = "Talk"`, `NameTag = _data.entityName` (no changes needed) |

### Technical Decisions

1. **Two separate `SphereCastNonAlloc` calls** — one for `interactionRange` (existing), one for `nameRange` (new). A single cast at `nameRange` with a distance check would also work, but two casts keeps the interaction-detection logic unchanged and avoids subtle bugs.

2. **`OnGUI` for name tags** — consistent with the existing prompt rendering in `InteractionSystem`. No new Canvas/GameObject required. Label is centered on screen X with an upward offset from the collider bounds.

3. **Name tag world position** — use `collider.bounds.center + Vector3.up * (collider.bounds.extents.y + 0.3f)` from the second sphere cast's `RaycastHit`. This places the label just above the top of the collider bounding box.

4. **Name tag dedup** — the second cast may hit multiple colliders from the same `IInteractable` parent. Track displayed tags with a small HashSet<IInteractable> per frame (stack-allocated not available; use a reused `HashSet<IInteractable>` field cleared each tick).

5. **Visibility guard** — skip rendering if `Camera.WorldToScreenPoint` returns `z <= 0` (behind camera).

---

## Implementation Plan

### Tasks

**Task 1 — `InteractionConfigSO.cs`: Add `nameRange` field**

File: `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs`

Add a new public field under `[Header("Detection")]`, after `interactionRange`:

```csharp
public float nameRange = 8f;
```

Default `8f` is intentionally larger than `interactionRange = 3f`.

---

**Task 2 — `InteractionSystem.cs`: Declare name range fields**

File: `Assets/_Game/Scripts/World/InteractionSystem.cs`

Add at class level, after `_sphereHitBuffer`:

```csharp
private RaycastHit[] _nameTagHitBuffer = new RaycastHit[16];
private readonly HashSet<IInteractable> _nameTagSeen = new HashSet<IInteractable>();

private struct NameTagEntry { public string label; public Vector3 worldPos; }
private NameTagEntry[] _nameTagEntries = new NameTagEntry[16];
private int _nameTagCount;
```

Add `using System.Collections.Generic;` at the top if not present.

Add a second `GUIStyle` field for name tags:

```csharp
private GUIStyle _nameTagStyle;
```

---

**Task 3 — `InteractionSystem.cs`: Second sphere cast in `Update`**

In `Update`, after the existing sphere cast block (after the `if (best == _previousInteractable) return;` block), add:

```csharp
// Name-range scan
_nameTagCount = 0;
_nameTagSeen.Clear();

int nameHitCount = Physics.SphereCastNonAlloc(
    ray, _config.scanRadius,
    _nameTagHitBuffer, _config.nameRange,
    _raycastMask);

for (int i = 0; i < nameHitCount && _nameTagCount < _nameTagEntries.Length; i++)
{
    var candidate = _nameTagHitBuffer[i].collider.GetComponentInParent<IInteractable>();
    if (candidate == null) continue;
    if (string.IsNullOrEmpty(candidate.NameTag)) continue;
    if (!_nameTagSeen.Add(candidate)) continue; // dedup

    Bounds b = _nameTagHitBuffer[i].collider.bounds;
    _nameTagEntries[_nameTagCount++] = new NameTagEntry
    {
        label = candidate.NameTag,
        worldPos = b.center + Vector3.up * (b.extents.y + 0.3f)
    };
}
```

> **Important:** The early-return `if (best == _previousInteractable) return;` must be removed or the name-range scan will be skipped when the best interactable hasn't changed. Move that early-return check to only skip the crosshair color update, or restructure so both scans always run. See restructuring note in Task 3b below.

**Task 3b — Remove early-return that blocks name tag scan**

The current `Update` returns early when `best == _previousInteractable`. This must be restructured so the name-range scan always runs:

```csharp
// After computing `best`:
if (best != _previousInteractable)
{
    CurrentInteractable = best;
    _previousInteractable = best;
    _crosshairImage.color = best != null ? _highlightColor : _defaultColor;
}

// Name-range scan always runs (after the if block, not in an else)
_nameTagCount = 0;
// ... (rest of name range scan from Task 3)
```

---

**Task 4 — `InteractionSystem.cs`: Render name tags in `OnGUI`**

In `OnGUI`, after the existing interact prompt block, add:

```csharp
if (_nameTagCount > 0)
{
    if (_nameTagStyle == null)
        _nameTagStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

    for (int i = 0; i < _nameTagCount; i++)
    {
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(_nameTagEntries[i].worldPos);
        if (screenPos.z <= 0f) continue; // behind camera

        float guiY = Screen.height - screenPos.y;
        GUI.Label(
            new Rect(screenPos.x - 100f, guiY - 20f, 200f, 25f),
            _nameTagEntries[i].label,
            _nameTagStyle);
    }
}
```

---

**Task 5 — `InteractionSystem.cs`: Update `OnDrawGizmos` for `nameRange`**

In `OnDrawGizmos`, after the existing gizmo drawing, add:

```csharp
Gizmos.color = Color.cyan;
Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * _config.nameRange);
Gizmos.DrawWireSphere(
    ray.origin + ray.direction * _config.nameRange,
    _config.scanRadius);
```

---

**Task 6 — `ItemPickup.cs`: Simplify `InteractPrompt`**

File: `Assets/_Game/Scripts/Inventory/ItemPickup.cs`

Change `InteractPrompt` to:

```csharp
public string InteractPrompt =>
    string.IsNullOrEmpty(_promptOverride) ? "Pick Up" : _promptOverride;
```

This removes the verbose "Press E to pick up {name}" in favor of a clean action verb, consistent with `NPCPresence`'s `"Talk"`.

---

### Acceptance Criteria

**AC1 — Name range config**
- Given `InteractionConfigSO` is open in the Inspector
- When viewing the Detection section
- Then a `nameRange` float field is visible with default value `8`

**AC2 — Name tag appears for NPCs in name range**
- Given the player is within `nameRange` of an NPC but outside `interactionRange`
- When the player looks roughly in the NPC's direction
- Then the NPC's `entityName` (from `NPCDataSO`) is rendered above the NPC's collider in screen space
- And the crosshair does NOT highlight (not in interaction range)

**AC3 — Name tag appears for pickups in name range**
- Given the player is within `nameRange` of an `ItemPickup`
- When the scan tick fires
- Then the item's `itemName` is rendered above the pickup in screen space

**AC4 — Name tag hidden behind camera**
- Given an interactable is behind the camera
- When `Camera.WorldToScreenPoint` returns `z <= 0`
- Then no label is rendered for that entity

**AC5 — No duplicate labels for multi-collider entities**
- Given an interactable has multiple colliders (e.g. compound collider)
- When the sphere cast hits multiple colliders from the same parent IInteractable
- Then the name tag is rendered exactly once

**AC6 — Interact prompt shows action verb**
- Given an `ItemPickup` is the `CurrentInteractable` and has no `_promptOverride`
- When the player centers on it
- Then the prompt under the crosshair reads `"Pick Up"` (not "Press E to pick up …")

**AC7 — NPC prompt unchanged**
- Given an `NPCPresence` is the `CurrentInteractable`
- When the player centers on it
- Then the prompt reads `"Talk"`

**AC8 — InteractableObject with empty NameTag shows no label**
- Given an `InteractableObject` (NameTag returns `""`)
- When it is within `nameRange`
- Then no name tag label is rendered for it

**AC9 — Gizmos**
- Given the InteractionSystem GameObject is selected in the Editor
- When Gizmos are enabled
- Then a cyan wire sphere and line show `nameRange` in addition to the existing yellow/green `interactionRange` gizmo

---

## Additional Context

### Dependencies

- No new prefab changes required — `InteractionConfigSO` asset will auto-expose the new field; set `nameRange = 8` in the Inspector after the script is updated.
- `IInteractable` already has `NameTag` — no interface changes needed.

### Testing Strategy

Manual in-Editor play test:
1. Stand far from an NPC/ItemPickup (between `interactionRange` and `nameRange`) — name tag visible, crosshair not highlighted.
2. Walk close (within `interactionRange`) — name tag still visible, crosshair highlights, prompt shows "Talk" / "Pick Up".
3. Walk away beyond `nameRange` — name tag disappears.
4. Turn camera away — name tag disappears (z ≤ 0 guard).

### Notes

- `_promptStyle` lazy-init (already in the code) pattern is reused for `_nameTagStyle` — no Awake dependency needed.
- The `_nameTagSeen` HashSet allocates on first use. Since it's a class field (not stack), it persists across frames and is just `Clear()`-ed each tick — no per-frame GC allocation.
- If the team later wants styled world-space labels (TextMeshPro Billboard), this `OnGUI` approach can be replaced without changing the `IInteractable` contract or `InteractionConfigSO`.
