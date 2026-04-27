---
title: 'Interaction System — SphereCast Radius Detection'
slug: 'interaction-spherecast-radius-detection'
created: '2026-04-27'
status: 'ready-for-dev'
stepsCompleted: [1, 2, 3, 4]
tech_stack: ['Unity 6.3 LTS', 'C#', 'Unity Physics (SphereCastNonAlloc)']
files_to_modify:
  - Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs
  - Assets/_Game/Scripts/World/InteractionSystem.cs
code_patterns:
  - 'Config SO for all tunable values (interactionRange already exists)'
  - 'IInteractable interface — 3 implementors (NPCPresence, ItemPickup, InteractableObject)'
  - 'RaycastHit[] buffer as field initializer — zero per-frame allocation'
  - 'Fixed timer pattern: accumulate deltaTime, scan on threshold, reset to 0f'
test_patterns: ['No test files in project — manual playtesting only']
---

# Tech-Spec: Interaction System — SphereCast Radius Detection

**Created:** 2026-04-27

## Overview

### Problem Statement

`InteractionSystem.Update()` fires `Physics.Raycast` from the camera center every frame.
This requires pixel-perfect aim to hit an interactable collider; any slight miss returns `null`
and the player cannot interact. On large or oddly-placed colliders the experience is inconsistent,
and on small objects (books, levers) it is consistently frustrating.

### Solution

Replace the single `Physics.Raycast` with `Physics.SphereCastAll` along the same camera-center ray.
Run the scan on a fixed 0.2s timer (not every frame) to keep Physics cost low.
Among all returned hits, select the `IInteractable` whose collider center has the smallest angular
offset from the camera's forward ray — i.e. the most centered object wins.

### Scope

**In Scope:**
- Add `scanRadius` float field to `InteractionConfigSO` (default `0.5f`)
- Replace `Physics.Raycast` with `Physics.SphereCastAll` in `InteractionSystem`
- Throttle detection scan to a fixed interval (~0.2s / 12 frames at 60fps)
- Among multiple hits pick the `IInteractable` with smallest angle to camera center ray
- Update `OnDrawGizmos` to visualize sphere radius at range endpoint

**Out of Scope:**
- OverlapSphere fallback or line-of-sight occlusion beyond what SphereCastAll provides
- Hysteresis / sticky-selection logic
- Changes to `IInteractable` interface or `InteractableObject`
- Per-frame detection (optimization is a stated requirement)

## Context for Development

### Codebase Patterns

- All tunable values go in config SO — NO magic numbers in MonoBehaviour
- Use `GameLog` for all logging (`GameLog.Info/Warn/Error(TAG, msg)`) — never `Debug.Log`
- `[SerializeField] private` for all Inspector fields
- No allocations in hot paths — `Physics.SphereCastAll` returns `RaycastHit[]`; use the
  non-alloc variant `Physics.SphereCastNonAlloc` with a pre-allocated buffer to stay
  allocation-free per tick
- `private const string TAG` is required if any `GameLog.*` calls are present
- Timer pattern: accumulate `Time.deltaTime` in a `float _scanTimer` field; scan when
  `_scanTimer >= _config.scanInterval`; reset to `0f` after scan

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/World/InteractionSystem.cs` | Primary file to modify |
| `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs` | Add `scanRadius` and `scanInterval` fields |
| `Assets/_Game/Scripts/World/IInteractable.cs` | Read-only reference — do not modify |

### Technical Decisions

| Decision | Rationale |
| --------- | --------- |
| `Physics.SphereCastNonAlloc` over `SphereCastAll` | Zero heap allocation per tick; pre-allocated buffer in `Awake` |
| Fixed timer throttle (0.2s) over per-frame scan | Physics casts are expensive; player doesn't need sub-frame update |
| Angle-to-ray as selection metric | Angular offset = screen-space proximity to crosshair; most intuitive "closest to center" |
| `scanRadius` and `scanInterval` in config SO | Keeps values tunable from Inspector without code changes |

## Implementation Plan

### Tasks

**Task 1 — Add fields to `InteractionConfigSO`**
File: `Assets/_Game/ScriptableObjects/Config/InteractionConfigSO.cs`

Add under the existing `[Header("Detection")]` block:

```csharp
public float scanRadius = 0.5f;     // sphere radius for SphereCastNonAlloc
public float scanInterval = 0.2f;   // seconds between detection scans
```

---

**Task 2 — Rework `InteractionSystem.cs`**
File: `Assets/_Game/Scripts/World/InteractionSystem.cs`

**2a. Add fields** (after existing private fields):

```csharp
private float _scanTimer;
private RaycastHit[] _sphereHitBuffer = new RaycastHit[16];
```

`_sphereHitBuffer` is allocated once in field initializer — no `Awake` change needed.

**2b. Replace `Update()` body:**

```csharp
private void Update()
{
    _scanTimer += Time.deltaTime;
    if (_scanTimer < _config.scanInterval) return;
    _scanTimer = 0f;

    Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    int hitCount = Physics.SphereCastNonAlloc(ray, _config.scanRadius,
                                              _sphereHitBuffer, _config.interactionRange,
                                              _raycastMask);

    IInteractable best = null;
    float bestAngle = float.MaxValue;

    for (int i = 0; i < hitCount; i++)
    {
        var candidate = _sphereHitBuffer[i].collider.GetComponentInParent<IInteractable>();
        if (candidate == null) continue;

        Vector3 toCollider = (_sphereHitBuffer[i].collider.bounds.center - ray.origin).normalized;
        float angle = Vector3.Angle(ray.direction, toCollider);
        if (angle < bestAngle)
        {
            bestAngle = angle;
            best = candidate;
        }
    }

    if (best == _previousInteractable) return;

    CurrentInteractable = best;
    _previousInteractable = best;
    _crosshairImage.color = best != null ? _highlightColor : _defaultColor;
}
```

**2c. Update `OnDrawGizmos()`** — add sphere visualization at ray end:

```csharp
private void OnDrawGizmos()
{
    Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
    if (cam == null || _config == null) return;

    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    bool hit = CurrentInteractable != null;

    Gizmos.color = hit ? Color.green : Color.yellow;
    Gizmos.DrawLine(ray.origin, ray.origin + ray.direction * _config.interactionRange);
    Gizmos.DrawWireSphere(ray.origin + ray.direction * _config.interactionRange,
                          _config.scanRadius);
}
```

---

### Acceptance Criteria

**AC-1: Radius tolerance**
- Given an interactable is within `interactionRange` but off-center by up to `scanRadius` in world space
- When the player aims roughly toward it (not pixel-perfect)
- Then `CurrentInteractable` is set to that interactable and the crosshair highlights

**AC-2: Closest-to-center wins**
- Given two interactables both within range
- When one is more centered on screen than the other
- Then `CurrentInteractable` is the more-centered one regardless of world-space distance

**AC-3: Scan throttle**
- Given the player is not moving
- When `scanInterval` time has not yet elapsed since last scan
- Then no Physics call is made and `CurrentInteractable` remains unchanged

**AC-4: No interactable in range**
- Given nothing within `interactionRange + scanRadius`
- When the scan fires
- Then `CurrentInteractable` is null and crosshair returns to default color

**AC-5: Gizmo shows sphere**
- Given Scene view with Gizmos enabled
- When selecting the Player GameObject
- Then a wire sphere of radius `scanRadius` is visible at the ray endpoint

**AC-6: No per-frame allocation**
- Given the game running at 60fps
- When the scan fires every 0.2s
- Then no `GC.Alloc` appears in the Profiler for `InteractionSystem.Update`

## Additional Context

### Dependencies

- `InteractionConfigSO` asset at `Assets/_Game/Data/Config/InteractionConfig.asset` must be
  re-saved in the Editor after adding fields so the new `scanRadius` and `scanInterval` values
  are serialized into the asset with their defaults.

### Testing Strategy

- Open a scene with 2+ interactable objects placed close together
- Aim between them: confirm the more-centered one highlights
- Aim well off-axis (> `scanRadius`): confirm no highlight
- Use Unity Profiler → CPU → check `InteractionSystem.Update` fires full body only every ~12 frames
- Enable Gizmos in Scene view: confirm wire sphere at ray endpoint

### Notes

- `Physics.SphereCastNonAlloc` writes into `_sphereHitBuffer` and returns the count of hits;
  entries beyond the count are stale from prior scans — always iterate `0..hitCount-1` only.
- `_scanTimer = 0f` reset (not `_scanTimer -= _config.scanInterval`) avoids timer drift
  accumulation if a frame takes longer than `scanInterval`.
- The `_sphereHitBuffer` size of 16 is generous for a typical scene; if more than 16 interactables
  can overlap in one sphere, increase the buffer size in `InteractionConfigSO` and resize the array
  in `Awake`.
