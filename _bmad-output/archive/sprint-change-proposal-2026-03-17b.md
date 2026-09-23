# Sprint Change Proposal — Lock-On Targeting System
**Date:** 2026-03-17
**Workflow:** correct-course
**Scope:** Moderate — new stories added to Epic 2; touches PlayerController, CameraController, PlayerStateManager, and InputSystem_Actions

---

## Section 1: Issue Summary

**Problem Statement:**
The combat system (Epic 2, complete) lacks a lock-on targeting mechanic. Without it, fighting multiple enemies requires constant manual camera adjustment, and precision 1v1 combat is harder than it should be in a melee-focused RPG. Adding lock-on is a proactive design improvement — not caused by a story failure but identified as a meaningful gameplay gap before Epic 5 (World & Exploration) begins.

**Discovery Context:**
Requested by Valentin between Epic 4 (in-progress, mostly done) and Epic 5 (backlog). Timing is ideal: Epic 2 (combat) is stable, all supporting systems are in place, and no exploration/world content has been built yet that would complicate testing.

**Feature Description:**
- **Activation:** Middle Mouse Button toggles lock-on on/off.
- **Target selection:** Nearest enemy within range in front of the player; auto-releases on target death.
- **Camera:** `CameraTarget` rotation is driven toward the locked enemy instead of free mouse look.
- **Movement:** When locked, forward/backward moves toward/away from the target; left/right strafes in a circle around the target. Player body always faces the target.
- **Visual:** A world-space indicator on the locked enemy confirms the state.

---

## Section 2: Impact Analysis

### Epic Impact
| Epic | Status | Impact |
|------|--------|--------|
| Epic 2 — Combat | done → **in-progress** | Add stories 2-10 and 2-11; all existing stories stay done |
| Epic 4 — Inventory | in-progress | No impact |
| Epic 5 — World | backlog | No impact (enemies already use layer/tag for AI targeting) |
| Epic 9 — Polish | backlog | Lock-on target indicator can be refined in HUD story (9-4); no blocker now |

### Story Impact
| Story | Status | Impact |
|-------|--------|--------|
| 2-8 `enemy-ai-patrol-engage` | done | Enemy GameObjects need to be on a detectable layer — confirmed already the case for AI detection |
| **2-10 `lock-on-targeting`** | **NEW** | Core lock-on: input, target detection, state, camera tracking |
| **2-11 `lock-on-movement`** | **NEW** | Target-relative locomotion and strafing when locked on |

### Artifact Conflicts
| Artifact | Impact | Action |
|----------|--------|--------|
| `InputSystem_Actions.cs` | Add `LockOn` action (Middle Mouse Button) to Player map embedded JSON | Update |
| `InputSystem_Actions.inputactions` | Add `LockOn` action to Player map | Update |
| `CameraController.cs` | Add locked-mode: drives `CameraTarget` toward enemy instead of mouse | Update |
| `PlayerController.cs` | Add target-relative movement and body facing when locked | Update |
| `PlayerStateManager.cs` | Add `IsLockedOn` bool + `CanLockOn()` query | Update |
| `CombatConfigSO.cs` | Add `lockOnRange` float | Update |
| **`LockOnSystem.cs`** | New script — target selection, toggle, reference ownership | Create |
| `epics.md` | Add 2 new stories to Epic 2 | Update |
| `sprint-status.yaml` | Add story entries 2-10, 2-11; revert epic-2 to in-progress | Update |
| `game-architecture.md` | Add Lock-On to Core Systems table | Update |

### Technical Impact
- `CameraController` dual-mode: mouse-driven (normal) vs. target-tracking (locked). No new Cinemachine cameras needed — same `CameraTarget` pivot is used; only what writes to it changes.
- `PlayerController` movement logic splits on `LockOnSystem.IsLockedOn` flag — no breaking change to the existing free-roam path.
- `LockOnSystem` lives in `Scripts/Combat/` (same system as combat; direct reference OK per architecture rules). It exposes `IsLockedOn` and `LockedTarget` to `CameraController` and `PlayerController` (same-system or Player system direct refs — no event bus required for per-frame state reads).
- `InputSystem_Actions` dual-file contract must be honored: **both** `.cs` embedded JSON and `.inputactions` file must be updated.

---

## Section 3: Recommended Approach

**Option 1 — Direct Adjustment** ✅ Selected

Add two new stories (2-10 and 2-11) to Epic 2. No rollback or MVP scope change required.

**Rationale:**
- Entirely additive — no existing Epic 2 behaviour is removed or replaced
- All prerequisite systems are stable (Combat, Camera, PlayerController, PlayerStateManager)
- Lock-on is a combat feel improvement that benefits the full game from Epic 5 onwards
- Risk is medium (touches three live core scripts) but well-contained by the `IsLockedOn` gate — free-roam code path is untouched when not locked
- Two stories rather than one keeps scope per-story manageable (targeting/camera vs. movement)

**Effort:** Medium | **Risk:** Medium | **Timeline Impact:** +2 stories in Epic 2 before beginning Epic 5

---

## Section 4: Detailed Change Proposals

### 4.1 — `epics.md`: Add Stories to Epic 2

**Section:** Epic 2 — Combat System, Stories list

```
OLD (last story):
- As a player, enemies and I have health that depletes on hit

NEW (append):
- As a player, enemies and I have health that depletes on hit
- As a player, I can press Middle Mouse Button to lock on to the nearest enemy
  in front of me, so the camera always faces the locked target and I can fight
  with precision without manually adjusting the camera
- As a player, when locked on, my movement is target-relative: forward/backward
  moves toward/away from the target, and left/right strafes in a circle around
  the target, while my character always faces the locked enemy
```

Rationale: Lock-on is a direct combat quality-of-life feature; Epic 2 is the
correct owner. Stories are split to keep camera tracking and movement locomotion
as separate, reviewable units.

---

### 4.2 — `game-architecture.md`: Register Lock-On in Core Systems

**Section:** Core Systems table

```
OLD:
| Third-person controller & camera | Low    | 1 |
| Stamina-based directional combat  | High   | 2 |
| Enemy AI (patrol, engage, attack) | Medium | 2 |

NEW:
| Third-person controller & camera  | Low        | 1 |
| Stamina-based directional combat  | High       | 2 |
| Lock-on targeting & camera track  | Low-Medium | 2 |
| Enemy AI (patrol, engage, attack) | Medium     | 2 |
```

---

### 4.3 — `sprint-status.yaml`: Add Story Entries

```
OLD:
  epic-2: done
  2-9-health-system: done
  epic-2-retrospective: done

NEW:
  epic-2: in-progress
  2-9-health-system: done
  2-10-lock-on-targeting: backlog
  2-11-lock-on-movement: backlog
  epic-2-retrospective: done
```

---

## Section 5: Implementation Handoff

**Change Scope: Moderate** — backlog entries managed by developer/SM; implementation by dev team.

### Story 2-10: `lock-on-targeting`

**New script:** `Assets/_Game/Scripts/Combat/LockOnSystem.cs`
- Reads `LockOn` input action (Middle Mouse Button — add to Player action map)
- On press: `Physics.OverlapSphere(player.position, lockOnRange)` → filter by enemy layer → pick closest to screen center → store as `LockedTarget`
- On press again (or target dies): clear `LockedTarget`, set `IsLockedOn = false`
- Exposes: `bool IsLockedOn`, `Transform LockedTarget`

**Update:** `CameraController.cs`
- Accept an optional `[SerializeField] LockOnSystem _lockOnSystem` reference
- In `RotateCamera()`: if `_lockOnSystem.IsLockedOn`, compute direction from `CameraTarget` to `LockedTarget`, rotate `CameraTarget` to face it (with pitch clamping); else run existing mouse-delta logic unchanged

**Update:** `PlayerStateManager.cs`
- Add `public bool IsLockedOn => _lockOnSystem != null && _lockOnSystem.IsLockedOn;`
- Add `public bool CanLockOn() => !IsBusy && !IsAirborne;`

**Update:** `CombatConfigSO.cs`
- Add `[SerializeField] public float lockOnRange = 12f;`

**Update:** `InputSystem_Actions.cs` + `.inputactions`
- Add `LockOn` action to Player map, bound to `<Mouse>/middleButton`
- Update embedded JSON string in `.cs` to match (dual-file contract)

**Success Criteria:**
- [ ] Pressing Middle Mouse Button with an enemy in range locks on: camera pivots to face enemy
- [ ] Camera continuously tracks enemy as it moves
- [ ] Pressing again unlocks: free mouse-look resumes
- [ ] If locked target dies, lock is automatically released
- [ ] No enemy in range: lock-on press does nothing (no error)
- [ ] `PlayerStateManager.IsLockedOn` returns correct value at all times

---

### Story 2-11: `lock-on-movement`

**Update:** `PlayerController.cs`
- Accept `[SerializeField] LockOnSystem _lockOnSystem`
- In `ApplyMovement()`: if `_lockOnSystem.IsLockedOn`:
  - Compute `toTarget = (target.position - transform.position).normalized` (Y=0 flattened)
  - `moveDir = toTarget * moveInput.y + Vector3.Cross(Vector3.up, toTarget) * moveInput.x`
  - Rotate body to always face target (override the free-movement `Quaternion.LookRotation(moveDir)`)
- Else: existing camera-relative movement unchanged

**Success Criteria:**
- [ ] When locked on, W moves toward enemy, S moves away
- [ ] When locked on, A/D strafes left/right in an arc around the enemy
- [ ] Player character always faces the locked enemy when locked (body rotation)
- [ ] Free-roam movement (no lock) is completely unchanged
- [ ] Sprint still works in lock-on mode (speed multiplier applies to lock-on moveDir)

---

**Handoff Recipients:** Valentin (solo dev) — implement 2-10 then 2-11 in sequence.

**Dependencies:**
- Story 2-10 must be complete before 2-11 begins (`LockOnSystem.IsLockedOn` reference required)
- `InputSystem_Actions` update in 2-10 required before any input reading in either story

**Overall Success Criteria (post both stories):**
- [ ] Middle Mouse Button toggles lock-on reliably
- [ ] Camera tracks locked target at all times while locked
- [ ] Movement is target-relative when locked, camera-relative when free
- [ ] Existing combat (combo, block, dodge, perfect block) all work correctly in lock-on mode
- [ ] All existing Epic 2 acceptance criteria remain passing
