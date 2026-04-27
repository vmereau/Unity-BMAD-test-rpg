---
title: 'Camera Look-Down Height Rise + Zoom-In'
slug: 'tech-spec-camera-look-down-height-rise'
created: '2026-04-27'
status: 'in-progress'
stepsCompleted: ['understand', 'investigate', 'spec', 'partial-implement']
files_to_modify:
  - Assets/_Game/Scripts/Player/CameraController.cs
---

# Tech-Spec: Camera Look-Down Height Rise + Zoom-In

**Created:** 2026-04-27

## Overview

### Problem Statement

When the player looks down at a steep pitch (toward `_pitchMin = −70°`), the CameraTarget pivot stays at head height (`localPosition.y = 1.6`). The camera therefore sits behind the player's back and aims at the ground, making it impossible to see objects lying in front of the player model. The player's body occludes the floor area directly ahead.

### Solution

As pitch goes positive (looking down toward `_pitchMax = 70°`), two things happen simultaneously:
1. `_cameraTarget.localPosition.y` slides upward proportionally — at max downward pitch the pivot has risen by `_lookDownHeightRise` units, clearing the player's head.
2. `CinemachineFollow.FollowOffset.z` is reduced (camera comes closer) — at max downward pitch the follow distance tightens by `_lookDownZoomIn` units, preventing the body from blocking the floor area ahead.

When pitch is zero or positive (looking up/level) both values return to their serialized defaults.

### Scope

**In Scope:**
- Pitch-driven vertical offset of `_cameraTarget.localPosition.y` in `CameraController.cs`
- Pitch-driven Z offset reduction via `CinemachineFollow.FollowOffset.z` in `CameraController.cs`
- Works in both free-look and lock-on modes

**Out of Scope:**
- Shoulder offset (X) adjustment
- Spring arm / deocclusion

---

## Context for Development

### Codebase Patterns

- **No magic numbers:** new tunables must be `[SerializeField] private float` directly on `CameraController`, consistent with `_mouseSensitivity`, `_pitchMin`, `_pitchMax`.
- **`_cameraTarget` is a child of the Player root** — `localPosition` is in player-local space. Safe to mutate Y directly.
- **LateUpdate drives all camera updates** — both `RotateCamera()` and `TrackLockedTarget()` run there. Both height and zoom updates must happen in both paths.
- **Awake caches refs** — both the default Y and the default follow offset Z must be cached in `Awake` so runtime modifications don't drift.
- **`_pitch` is always up-to-date** before height is needed — both camera paths write `_pitch` then apply `_cameraTarget.rotation`. Updates run after rotation, using the already-updated `_pitch`.
- **Pitch sign:** positive = looking down (toward `_pitchMax = 70`), negative = looking up.
- `GameLog` must be used instead of `Debug.Log` (namespace `Game.Core`).
- `CinemachineFollow` is in namespace `Unity.Cinemachine` (Cinemachine 3.x).

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Player/CameraController.cs` | Only file modified |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Verify CameraTarget default localPosition after changes |
| `Assets/_Game/Scripts/Player/CLAUDE.md` | CameraController rules, Cinemachine OTS setup |
| `Assets/_Game/Scenes/*.unity` | Assign `_cinemachineFollow` field in the Player prefab instance |

### Technical Decisions

**Why localPosition.y and not a separate pivot Transform?**
CameraTarget is already the Cinemachine follow target. Shifting its world position upward is exactly what we want — Cinemachine reads that position and places the camera offset from it. No new GameObjects or hierarchy changes needed.

**Why adjust the Cinemachine offset directly (accepted coupling)?**
The Z distance reduction requires modifying `CinemachineFollow.FollowOffset.z` at runtime. This adds a direct reference from `CameraController` to `CinemachineFollow`, but the coupling is contained in one serialized field and one method. The result (camera tightens toward the character when looking down) is not achievable by moving the pivot alone.

**Pitch sign convention (important — spec was initially wrong):**
In this codebase, `_pitch` goes **positive** when looking down and **negative** when looking up. `_pitchMax = 70` is the maximum downward look; `_pitchMin = -70` is the maximum upward look. Both height and zoom-in trigger on `_pitch > 0` and lerp toward `_pitchMax`.

**Why apply in both RotateCamera and TrackLockedTarget?**
Lock-on also drives `_pitch` toward the target's elevation. If the locked target is below the player, pitch goes positive and the same occlusion problem occurs. Consistent behavior is expected.

---

## Implementation Plan

### Tasks

**Task 1 — Add fields and cache defaults in Awake** ✅ done

File: `Assets/_Game/Scripts/Player/CameraController.cs`

Fields added after `_pitchMax`:
```csharp
[SerializeField] private float _lookDownHeightRise = 0.7f;
[SerializeField] private float _lookDownZoomIn = 1.5f;
[SerializeField] private CinemachineFollow _cinemachineFollow;
```

Private cache fields after `_pitch`:
```csharp
private float _defaultCameraTargetLocalY;
private float _defaultFollowOffsetZ;
```

In `Awake()`, after the `_pitch` initialization:
```csharp
_defaultCameraTargetLocalY = _cameraTarget.localPosition.y;
if (_cinemachineFollow != null)
    _defaultFollowOffsetZ = _cinemachineFollow.FollowOffset.z;
```

---

**Task 2 — `UpdateCameraTargetHeight` drives both height and zoom** ✅ done (height only) → update to include zoom

```csharp
private void UpdateCameraTargetHeight()
{
    float t = _pitch > 0f ? Mathf.InverseLerp(0f, _pitchMax, _pitch) : 0f;

    Vector3 pos = _cameraTarget.localPosition;
    pos.y = _defaultCameraTargetLocalY + t * _lookDownHeightRise;
    _cameraTarget.localPosition = pos;

    if (_cinemachineFollow != null)
    {
        Vector3 offset = _cinemachineFollow.FollowOffset;
        offset.z = _defaultFollowOffsetZ + t * _lookDownZoomIn;
        _cinemachineFollow.FollowOffset = offset;
    }
}
```

**How it works:**
- `_pitch > 0` → looking down. `Mathf.InverseLerp(0, 70, _pitch)` returns `0` at pitch=0, `1` at pitch=70°.
- `_pitch <= 0` → looking up or level. `t = 0`, both values return to defaults.
- `_defaultFollowOffsetZ` is negative (−3.5). Adding a positive `_lookDownZoomIn` makes it less negative → camera comes closer.

---

**Task 3 — Calls in both camera paths** ✅ already done

Both `RotateCamera()` and `TrackLockedTarget()` already call `UpdateCameraTargetHeight()`.

---

**Task 4 — Wire `_cinemachineFollow` in the Inspector**

Assign the `CinemachineFollow` component (on the `CinemachineCamera` GameObject) to the `_cinemachineFollow` field on `CameraController` in the Player prefab.

---

### Acceptance Criteria

**AC1 — Flat look (pitch = 0)**
- Given the player is looking straight forward (pitch = 0)
- Then `_cameraTarget.localPosition.y` equals the serialized default and follow offset Z equals its default

**AC2 — Look down rises and zooms proportionally**
- Given pitch reaches 35° (half of 70°)
- Then `localPosition.y ≈ defaultY + 0.5 * _lookDownHeightRise`
- And `followOffset.z ≈ defaultZ + 0.5 * _lookDownZoomIn`

**AC3 — Max look down**
- Given pitch reaches `_pitchMax` (70°)
- Then `localPosition.y = defaultY + _lookDownHeightRise` and `followOffset.z = defaultZ + _lookDownZoomIn`
- And the camera clears the player's head, making the floor in front visible

**AC4 — Look up has no effect**
- Given the player tilts the camera upward (pitch < 0)
- Then both values stay at their defaults

**AC5 — Lock-on mode**
- Given lock-on is active and the locked target is below the player
- When pitch goes positive, both height and zoom apply identically to free-look

**AC6 — No drift**
- Both values always evaluate from cached defaults, never accumulate

**AC7 — Graceful without CinemachineFollow assigned**
- If `_cinemachineFollow` is null, only the height rise applies; no NullReferenceException

---

## Additional Context

### Dependencies

None. This is a self-contained change to `CameraController.cs`.

### Testing Strategy

Manual in-Editor play test:
1. Enter Play mode, look straight ahead — verify no visual change vs. current behavior.
2. Slowly tilt camera down — verify the camera rises smoothly and the floor ahead becomes visible.
3. Tilt all the way to −70° — verify the player model is no longer occluding the floor in front.
4. Tilt back up to 0° and above — verify no overshoot or jitter.
5. Lock on to an enemy that is below the player on a slope — verify height rise still applies.
6. Tune `_lookDownHeightRise` in the Inspector during Play mode to find the best feel; commit the chosen value in the prefab.

### Notes

- Recommended starting values: `_lookDownHeightRise = 0.7f`, `_lookDownZoomIn = 1.5f`. At max pitch: pivot rises to `2.3m`, follow Z tightens from `−3.5` to `−2.0`. Tune both in Play mode.
- If the camera feels like it "jumps" when looking down quickly, consider adding `Mathf.SmoothDamp` on both values in a future iteration — start with direct assignment to validate the mechanic first.
- `_lookDownZoomIn` is additive on the Z offset (which is negative). A value of `1.5` reduces distance by 1.5 units. Set to `0` to disable zoom without removing the field.
