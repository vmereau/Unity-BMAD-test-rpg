---
title: 'Upgrade Cinemachine 2.x to 3.x'
slug: 'upgrade-cinemachine-3x'
created: '2026-04-27'
status: 'in-progress'
stepsCompleted: [1, 2, 3]
tech_stack: ['Unity 6.3 LTS', 'C#', 'Cinemachine 3.x', 'URP 17.x']
files_to_modify:
  - Packages/manifest.json
  - Assets/_Game/Game.asmdef
  - Assets/_Game/Scripts/Player/CameraController.cs
files_to_modify_in_editor:
  - Player scene (CinemachineCamera GameObject — manual Editor reconfiguration)
  - Assets/_Game/Prefabs/Player/Player.prefab (assign new _cinemachineCamera field)
code_patterns: []
test_patterns: []
---

# Tech-Spec: Upgrade Cinemachine 2.x to 3.x

**Created:** 2026-04-27

---

## Overview

### Problem Statement

The project is on Unity 6.3 LTS but uses Cinemachine **2.10.5** — a legacy package version not designed for Unity 6. This caused two bugs in this session:
1. `using Unity.Cinemachine;` (the 3.x namespace) failed to compile because the 2.x package uses a different namespace/assembly.
2. `CinemachineFollow.FollowOffset` (the 3.x API used in the camera look-down spec) does not exist in 2.x — the equivalent is `CinemachineTransposer.m_FollowOffset`, requiring a `GetCinemachineComponent<T>()` pipeline lookup.

Cinemachine 3.x is the version designed for Unity 6. Staying on 2.x means continued API friction, risk of deprecation, and a mismatch with Unity 6's documented tooling.

### Solution

Bump `com.unity.cinemachine` in `manifest.json` to the latest 3.x version available for Unity 6. Update `Game.asmdef` to reference the new assembly name (`Unity.Cinemachine`). Update `CameraController.cs` to the 3.x API. Reconfigure the CinemachineCamera GameObject in the scene from scratch (Cinemachine 3.x component structure differs from 2.x — this is unavoidable manual Editor work).

### Scope

**In Scope:**
- `manifest.json` package version bump
- `Game.asmdef` assembly reference update
- `CameraController.cs` API migration (`using`, field types, component access)
- Manual Editor reconfiguration of the CinemachineCamera GameObject in the scene
- CLAUDE.md correction (was already wrongly documenting 3.x classes while 2.x was installed)

**Out of Scope:**
- `CinemachineBrain` settings on Main Camera (no changes needed — Brain exists in both 2.x and 3.x)
- Any camera behavior changes (this is a pure upgrade, same result)
- Timeline Cinemachine tracks (not used in this project)

---

## Context for Development

### Codebase Patterns

- **One CinemachineVirtualCamera in the project** — on the `CinemachineCamera` GameObject in the main scene, following the `CameraTarget` child of the Player prefab.
- **Current 2.x pipeline:** `CinemachineVirtualCamera` with body = `CinemachineTransposer` (offset `(0.5, 0.3, −3.5)`) and aim = `CinemachineRotateWithFollowTarget`.
- **3.x pipeline change:** Components are no longer nested inside a pipeline — `CinemachineFollow` and `CinemachineRotateWithFollowTarget` are added directly as sibling MonoBehaviours on the `CinemachineCamera` GameObject.
- **`CameraController.cs` currently uses** `CinemachineVirtualCamera` (serialized field) + `GetCinemachineComponent<CinemachineTransposer>()` to access the follow offset. In 3.x this becomes `GetComponent<CinemachineFollow>()`.
- **`Game.asmdef`** currently references `"Cinemachine"` (added during this session). Must be changed to `"Unity.Cinemachine"`.
- **No other scripts** in `Assets/_Game/` reference Cinemachine types.

### Files to Reference

| File | Purpose |
| ---- | ------- |
| `Assets/_Game/Scripts/Player/CameraController.cs` | Only script that references Cinemachine types |
| `Assets/_Game/Game.asmdef` | Assembly reference must change from `"Cinemachine"` to `"Unity.Cinemachine"` |
| `Packages/manifest.json` | Package version to bump |
| `Assets/_Game/Prefabs/Player/Player.prefab` | Contains `_cinemachineCamera` SerializeField reference — must be reassigned after upgrade |
| `Assets/_Game/Scripts/Player/CLAUDE.md` | OTS setup notes reference 3.x classes — will be correct after upgrade |

### Technical Decisions

**Why `GetComponent<CinemachineFollow>()` and not a serialized `CinemachineFollow` field?**
In 3.x, `CinemachineFollow` is a MonoBehaviour on the same GameObject as `CinemachineCamera`. We can serialize it directly as `[SerializeField] private CinemachineFollow _cinemachineFollow;` — this is cleaner than the 2.x `GetCinemachineComponent` lookup and matches how the original spec was written. Use this approach.

**What version number to use?**
Open Package Manager → Cinemachine → check "See all versions". For Unity 6.3 LTS, the latest 3.x should be `3.1.x`. Use whatever the highest available 3.x version is. Do NOT downgrade to an older 3.x — take the latest.

**Aim component in 3.1.6:**
`CinemachineRotateWithFollowTarget` is deprecated in 3.1.6. Use `CinemachineRotateWithFollowTarget` instead — same behavior, new name.

---

## Implementation Plan

### Tasks

**Task 1 — Bump package version in manifest.json**

File: `Packages/manifest.json`

Change:
```json
"com.unity.cinemachine": "2.10.5",
```
To:
```json
"com.unity.cinemachine": "<latest-3.x-version>",
```

Look up the exact version in Package Manager before editing. Unity will trigger a package download and domain reload on save.

---

**Task 2 — Update Game.asmdef assembly reference**

File: `Assets/_Game/Game.asmdef`

Change in `references` array:
```json
"Cinemachine"
```
To:
```json
"Unity.Cinemachine"
```

---

**Task 3 — Migrate CameraController.cs to 3.x API**

File: `Assets/_Game/Scripts/Player/CameraController.cs`

**3a — Update `using` directive:**
```csharp
// Remove:
using Cinemachine;

// Add:
using Unity.Cinemachine;
```

**3b — Replace field declarations:**
```csharp
// Remove:
[SerializeField] private CinemachineVirtualCamera _cinemachineCamera;
// ...
private CinemachineTransposer _transposer;

// Add:
[SerializeField] private CinemachineCamera _cinemachineCamera;
[SerializeField] private CinemachineFollow _cinemachineFollow;
```

**3c — Replace Awake caching logic:**
```csharp
// Remove:
if (_cinemachineCamera != null)
{
    _transposer = _cinemachineCamera.GetCinemachineComponent<CinemachineTransposer>();
    if (_transposer != null)
        _defaultFollowOffsetZ = _transposer.m_FollowOffset.z;
}

// Add:
if (_cinemachineFollow != null)
    _defaultFollowOffsetZ = _cinemachineFollow.FollowOffset.z;
```

(Remove the `_cinemachineCamera` field entirely if only used for the `GetCinemachineComponent` call. Keep it if it's referenced elsewhere — check the full file.)

**3d — Replace UpdateCameraTargetHeight offset write:**
```csharp
// Remove:
if (_transposer != null)
{
    Vector3 offset = _transposer.m_FollowOffset;
    offset.z = _defaultFollowOffsetZ + t * _lookDownZoomIn;
    _transposer.m_FollowOffset = offset;
}

// Add:
if (_cinemachineFollow != null)
{
    Vector3 offset = _cinemachineFollow.FollowOffset;
    offset.z = _defaultFollowOffsetZ + t * _lookDownZoomIn;
    _cinemachineFollow.FollowOffset = offset;
}
```

---

**Task 4 — Reconfigure CinemachineCamera GameObject in the scene (manual Editor work)**

This task cannot be scripted — it requires manual steps in the Unity Editor after the package upgrade compiles.

Steps:
1. Open the scene containing the player camera setup.
2. Select the `CinemachineCamera` GameObject (currently has a `CinemachineVirtualCamera` component).
3. Remove the `CinemachineVirtualCamera` component (and its nested pipeline components if present).
4. Add component: `CinemachineCamera`. Set **Follow** = `CameraTarget` Transform (child of Player).
5. Add component: `CinemachineFollow`. Set **Follow Offset** = `(0.5, 0.3, −3.5)`.
6. Add component: `CinemachineRotateWithFollowTarget` (for aim — inherits CameraTarget rotation directly).
7. On the `CameraController` component (on the Player root): assign the new `_cinemachineFollow` field to the `CinemachineFollow` component from step 5.
8. Verify `CinemachineBrain` is still present on the Main Camera (should be untouched).

---

**Task 5 — Update CLAUDE.md OTS setup note**

File: Root `CLAUDE.md` (or `Assets/_Game/Scripts/Player/CLAUDE.md`)

The Cinemachine OTS setup note currently says:
> `CinemachineFollow` with offset `(0.5, 0.3, −3.5)`

This was accurate for 3.x but misleading while 2.x was installed. After the upgrade it is now correct. Update the CLAUDE.md note to:
- Confirm the project uses **Cinemachine 3.x** (`com.unity.cinemachine` 3.x, namespace `Unity.Cinemachine`)
- Assembly reference in `Game.asmdef`: `"Unity.Cinemachine"`
- `CinemachineCamera` + `CinemachineFollow` + `CinemachineRotateWithFollowTarget` are sibling MonoBehaviours (not nested pipeline)

---

### Acceptance Criteria

**AC1 — No compilation errors after upgrade**
- Given the package is bumped and `Game.asmdef` is updated
- When Unity reimports and compiles
- Then zero compiler errors related to Cinemachine

**AC2 — Camera behavior unchanged**
- Given the scene is reconfigured with `CinemachineCamera` + `CinemachineFollow` + `CinemachineRotateWithFollowTarget`
- When entering Play mode and moving/rotating the camera
- Then camera behavior is identical to pre-upgrade (OTS offset, follow, aim all work)

**AC3 — Look-down height + zoom feature works**
- Given `_cinemachineFollow` is assigned in the Inspector
- When looking down toward `_pitchMax`
- Then `CinemachineFollow.FollowOffset.z` tightens correctly (zoom-in applies)

**AC4 — No NullReferenceException**
- If `_cinemachineFollow` is not assigned
- Then `UpdateCameraTargetHeight` skips the offset write gracefully

**AC5 — Lock-on mode unaffected**
- Given lock-on is active
- Then the camera tracks correctly with no regression

---

## Additional Context

### Dependencies

None. This is an isolated package upgrade + API migration.

### Testing Strategy

Manual in-Editor:
1. After upgrade, check console for zero errors.
2. Enter Play mode — free-look, look down/up, verify height rise + zoom.
3. Engage lock-on — verify tracking still works.
4. Inspect `_cinemachineFollow.FollowOffset` in real-time via Inspector while looking down to confirm Z changes.

### Notes

- The `_cinemachineCamera` serialized field becomes **unused** after Task 3 (all access is via `_cinemachineFollow` directly). Remove the field entirely to avoid a dead `[SerializeField]` (LOW severity code review issue).
- If Unity shows a migration dialog when the package is upgraded, accept the automatic migration for `CinemachineBrain` settings — it is safe.
- After Task 4, save both the scene and the Player prefab to persist the new component wiring.
